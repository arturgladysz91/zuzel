"""Audit complete native S3 histories, delayed commands and cyclic planning receipts."""

from bisect import bisect_left
from collections import Counter
import gzip
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
from statistics import median
import sys
import argparse
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "whole-corner-planning"))
from audit171 import trace_of, section
from report171 import high_drive

DOCS = Path("docs/experiments/three-lap-global-planning")
EPS = 1e-8


def load(path):
    return json.loads(path.read_bytes())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def cross(trace, position):
    references = [q["s"] for q in trace]
    j = bisect_left(references, position)
    if j == 0:
        return trace[0] if abs(trace[0]["s"] - position) < EPS else None
    if j == len(trace):
        return None
    a, b = trace[j - 1], trace[j]
    fraction = (position - a["s"]) / (b["s"] - a["s"])
    return {k: a[k] + (b[k] - a[k]) * fraction for k in ("t", "s", "kph", "distance", "offset", "beta", "yaw")}


def value_at_time(rows, moment, column):
    if moment <= rows[0][0]:
        return 0
    j = bisect_left([r[0] for r in rows], moment)
    if j == len(rows):
        return rows[-1][column]
    a, b = rows[j - 1], rows[j]
    return a[column] + (b[column] - a[column]) * (moment - a[0]) / (b[0] - a[0])


def high_share(trace, start, end):
    if end is None:
        end = trace[-1]["t"]
    high = sum(max(0, min(end, b["t"]) - max(start, a["t"])) for a, b in zip(trace, trace[1:])
               if b["active"] >= .95)
    return high / (end - start) if end > start else 0


def queue_audit(native, decisions):
    rider = native["Riders"][0]
    samples = rider["Samples"]
    zero = {"SteeringRadians": 0, "Drive": 0}
    assert samples[0]["Input"]["Rider"]["Speed"] == samples[0]["Input"]["Omega"] == 0
    assert len(decisions) == len(rider["Decisions"])
    commands = []
    for index, (review, d) in enumerate(zip(decisions, rider["Decisions"])):
        choice = d["Choice"]
        t = choice["Time"]
        assert abs(t - index * .2) < EPS and review["Time"] == t
        assert choice["Action"] == review["Action"]
        assert choice["Forecasts"] == review["Forecasts"] and choice["ForceEvaluations"] == review["Queries"]
        commands.append(dict(ObservedAt=t, ActivationAt=t + .24, Action=choice["Action"]))
    last = samples[-1]["Input"]["Time"]
    assert rider["Activations"] == [c for c in commands if c["ActivationAt"] < last - EPS]
    for sample in samples:
        state = sample["Input"]
        t = state["Time"]
        observed = commands[:1] if t <= EPS else [c for c in commands if c["ObservedAt"] < t - EPS]
        pending = [c for c in observed if c["ActivationAt"] >= t - EPS]
        active = [c["Action"] for c in observed if c["ActivationAt"] < t - EPS]
        assert state["Active"] == (active[-1] if active else zero) and state["Pending"] == pending
        if t < .24 - EPS:
            assert state["Rider"]["Speed"] == state["Omega"] == 0
        assert state["Rider"]["Activity"] == 0
    return len(commands)


def inspect(native, result, decisions, plans, cpu_marks, geometry):
    queue_audit(native, decisions)
    samples = native["Riders"][0]["Samples"]
    trace = trace_of(native)
    for q, sample in zip(trace, samples):
        q.update(steer=sample["Input"]["Steering"] * 180 / math.pi,
                 steer_active=sample["Input"]["Active"]["SteeringRadians"] * 180 / math.pi,
                 segment=sample["Input"]["Rider"]["Road"]["Segment"])
    assert native["Fingerprint"] == result["Fingerprint"]
    length = native["LapMeters"]
    laps, finishes = [], []
    first = samples[0]["Input"]["Rider"]["TotalReferenceMeters"]
    for crossing in native["Crossings"]:
        lap = crossing["Lap"]
        target = lap * length
        measured = cross(trace, target)
        assert measured is not None and abs(measured["t"] - crossing["Time"]) < EPS
        assert crossing["BeforeReference"] < target <= crossing["AfterReference"]
        i = next(i for i, q in enumerate(trace) if q["s"] >= target)
        a, b = samples[i - 1], samples[i]
        assert b["Input"]["Rider"]["DistanceMeters"] >= a["Input"]["Rider"]["DistanceMeters"]
        finishes.append(dict(lap=lap, seconds=crossing["Time"], wheel_before=a["Input"]["Omega"],
                             wheel_after=b["Input"]["Omega"], crank_before=a["CrankOmega"], crank_after=b["CrankOmega"],
                             pending=b["Input"]["Pending"], active=b["Input"]["Active"],
                             reference_before=trace[i - 1]["s"], reference_after=trace[i]["s"]))
    for lap in range(3):
        origin = lap * length
        entry_t = 0 if lap == 0 else (cross(trace, origin) or {}).get("t")
        end = cross(trace, origin + length)
        if entry_t is None:
            break
        end_t = end["t"] if end else trace[-1]["t"]
        complete = end is not None
        lower = first if lap == 0 else origin
        upper = origin + length
        points = [q for q in trace if lower <= q["s"] <= upper]
        if not points:
            continue
        sections = []
        for segment in geometry["Segments"]:
            if segment["Type"] == "Straight":
                continue
            shifted = dict(segment, StartReferenceMeters=origin + segment["StartReferenceMeters"])
            sections.append(section(trace, shifted))
        bends = []
        for index in (0, 1):
            entry = origin + geometry["Segments"][1 if index == 0 else 5]["StartReferenceMeters"]
            exit = origin + geometry["Segments"][4 if index == 0 else 8]["StartReferenceMeters"]
            x, y = cross(trace, entry), cross(trace, exit)
            between = [q for q in trace if entry <= q["s"] <= exit]
            bends.append(dict(entry=entry, exit=exit, entry_kph=x["kph"] if x else None,
                              exit_kph=y["kph"] if y else None, seconds=y["t"] - x["t"] if x and y else None,
                              minimum_kph=min((q["kph"] for q in between), default=None),
                              energy_j={key: sum(p["energy"].get(key, 0) for p in sections[index * 3:index * 3 + 3] if p)
                                        for key in ("net", "front_lateral", "rear_lateral", "rear_longitudinal")}))
        main_start = origin + geometry["Segments"][4]["StartReferenceMeters"]
        main_end = origin + geometry["Segments"][5]["StartReferenceMeters"]
        home_start = origin + geometry["Segments"][8]["StartReferenceMeters"]
        home_end = origin + length + geometry["Segments"][1]["StartReferenceMeters"]
        straights = []
        for lo, hi in ((main_start, main_end), (home_start, home_end)):
            start = cross(trace, lo)
            finish = cross(trace, hi)
            straight_points = [p for p in trace if lo <= p["s"] <= hi]
            windows = high_drive(trace, start["t"] if start else None, finish["t"] if finish else None)
            straights.append(dict(start=lo, end=hi, complete=finish is not None,
                                  high_intervals=windows, high_seconds=sum(p["end"] - p["start"] for p in windows or []),
                                  maximum_kph=max((p["kph"] for p in straight_points), default=None)))
        distance_start = 0 if lap == 0 else cross(trace, origin)["distance"]
        laps.append(dict(lap=lap + 1, complete=complete, seconds=end_t - entry_t,
                         distance=(end["distance"] if end else trace[-1]["distance"]) - distance_start,
                         entry_t=entry_t, end_t=end_t, bends=bends, sections=sections, straights=straights,
                         high_share=high_share(trace, entry_t, end_t),
                         maximum_beta=max(abs(q["beta"]) for q in points),
                         min_pose=min(min(q["inner"], q["outer"]) for q in points),
                         min_swept=min(q["swept"] for q in points if q["swept"] is not None),
                         cpu_seconds=value_at_time(cpu_marks, end_t, 1) - value_at_time(cpu_marks, entry_t, 1),
                         decision_cpu_seconds=sum(q["CpuSeconds"] for q in decisions if entry_t <= q["Time"] < end_t)))
    bad = result["FirstBadTime"]
    failure = None
    if bad is not None:
        i = next(i for i, q in enumerate(trace) if q["t"] >= bad - EPS)
        last = next((q for q in reversed(decisions) if q["Time"] < bad), None)
        failure = dict(state=samples[i], previous=samples[max(0, i - 1)], decision=last, trace=trace[i])
    final = samples[-1]
    finish = cross(trace, 3 * length)
    accepted = {(p["Plans"][0]["Visit"]["Lap"], p["Plans"][0]["Visit"]["Index"]) for p in plans
                if p["Chosen"] and 0 <= p["Plans"][0]["Visit"]["Lap"] < 3}
    if "AcceptedBends" in result:
        assert result["AcceptedBends"] == len(accepted)
    if result["S3"]:
        assert len(finishes) == 3 and [x["lap"] for x in finishes] == [1, 2, 3]
    if result["Reserve"]:
        i = next(i for i, q in enumerate(trace) if q["s"] >= 3 * length)
        assert trace[-1]["t"] - trace[i]["t"] >= 1 - EPS
        assert decisions[-1]["Time"] > trace[i]["t"]
    effects = []
    for lap in range(3):
        for index in (0, 1):
            lo = lap * length + geometry["Segments"][1 if index == 0 else 5]["StartReferenceMeters"]
            hi = lap * length + geometry["Segments"][4 if index == 0 else 8]["StartReferenceMeters"]
            effects.append(sum(lo <= q["Reference"] < hi and q["Tracking"]["Policy"] == "K4" and
                               q["Action"] != q["Tracking"]["Baseline"] for q in decisions))
    if result["Stage"] == "validate" and result["Controller"] == "K4":
        assert all(effects), "K4 must affect applied decisions in all six bends"
    return trace, dict(result=result, laps=laps, finishes=finishes, failure=failure, last_state=final,
                       changed_k4_decisions_by_bend=effects,
                       actual_s3_distance=finish["distance"] if finish else None,
                       average_kph=finish["distance"] / finish["t"] * 3.6 if finish else None,
                       plan_requests=len(plans), accepted_bends=len(accepted),
                       planning_cpu_seconds=sum(p["CpuSeconds"] for p in plans),
                       planning_wall_ms=sum(p["Milliseconds"] for p in plans),
                       prediction_wall_ms=sum(p["Tracking"]["PredictionMilliseconds"] for p in decisions),
                       decisions=len(decisions), mean_decision_wall_ms=sum(p["DecisionMilliseconds"] for p in decisions) / len(decisions),
                       maximum_decision_wall_ms=max(p["DecisionMilliseconds"] for p in decisions),
                       interventions=dict(Counter(p["Transition"] for p in decisions)),
                       plan_events=[{k: v for k, v in p.items() if k != "Plans"} for p in plans],
                       source_profile_counts=dict(Counter(p["Profile"] for p in decisions)))


def historical(geometry, held):
    root = Path("docs/experiments/trajectory-continuation/native")
    comparison = []
    for path in sorted(root.glob("held-*.json.gz")):
        with gzip.open(path, "rb") as f:
            native = json.load(f)
        rider = native["Riders"][0]
        spec = rider["Spec"]
        comparison.append(dict(file=str(path), sha256=sha(path), lap_meters=native["LapMeters"],
                               initial_spec=spec, gate=rider["Gate"], finish_seconds=rider["FinishSeconds"],
                               new_states=[q["Id"] for q in held if q["Start"] == spec["ReferenceMeters"] and
                                           abs(q["Offset"] - spec["OffsetMeters"]) < 1e-7]))
    return dict(reference_median_seconds=79.603, records=comparison,
                geometry_lap_meters=geometry["LapMeters"],
                directly_paired=any(q["new_states"] for q in comparison))


def repeatability(evidence):
    """Compare repeated native development prefixes, excluding performance clocks."""
    runs = []
    for label in ("c1b-K4-D1", "c2b-K4-D1", "c3b-K4-D1"):
        with ZipFile(evidence / (label + ".zip")) as archive:
            runs.append(json.loads(archive.read("native.json"))["Riders"][0])
    proof = []
    for a, b in zip(runs, runs[1:]):
        prefix = a["Samples"]
        assert prefix == b["Samples"][:len(prefix)], "Native prefix changed"
        last = prefix[-1]["Input"]["Time"]
        decisions = lambda run: [q["Choice"] for q in run["Decisions"] if q["Choice"]["Time"] < last - EPS]
        assert decisions(a) == decisions(b)
        digest = hashlib.sha256(json.dumps(prefix, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
        proof.append(dict(samples=len(prefix), through_seconds=last, sha256=digest))
    return proof


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--audit-only", action="store_true")
    args = parser.parse_args()
    e, out = args.evidence, args.output
    freeze, protocol, geometry = load(e / "freeze.json"), load(e / "protocol.json"), load(e / "geometry.json")
    for name, expected in freeze["Sources"].items():
        assert sha(Path(name)) == expected, name
    for name, expected in freeze["PhysicsSha256"].items():
        assert sha(Path(name)) == expected, name
    assert sha(DOCS / "held.json") == protocol["HeldSha256"]
    ledger = [json.loads(line) for line in (e / "ledger.jsonl").read_bytes().splitlines()]
    entered = [q["id"] for q in ledger if q["Event"] == "reserved"]
    completed = [q for q in ledger if q["Event"] == "result"]
    assert entered == [q["id"] for q in completed] and len(set(entered)) == len(entered) <= 30
    done = [load(p) for p in e.glob("*.done.json")]
    cpu = sum(p["CpuSeconds"] for p in done)
    assert cpu < 500
    rows, traces, reviews = [], {}, {}
    for receipt in completed:
        label = receipt["id"]
        path = e / (label + ".zip")
        assert sha(path) == receipt["Sha256"]
        with ZipFile(path) as archive:
            native = json.loads(archive.read("native.json"))
            result = json.loads(archive.read("result.json"))
            decision = json.loads(archive.read("decisions.json"))
            plans = json.loads(archive.read("plans.json"))
            marks = json.loads(archive.read("cpu.json"))
        traces[label], row = inspect(native, result, decision, plans, marks, geometry)
        rows.append(row)
        reviews[label] = decision
    final = [r for r in rows if r["result"]["Stage"] == "validate"]
    assert len(final) == 10
    by = {name: [r for r in final if r["result"]["Controller"] == name] for name in ("K4", "C0")}
    assert len(by["K4"]) == len(by["C0"]) == 5
    pairs = []
    for a, b in zip(by["C0"], by["K4"]):
        x, y = a["result"], b["result"]
        assert x["Start"] == y["Start"]
        if x["S3"] and x["Reserve"] and y["S3"] and y["Reserve"]:
            pairs.append(dict(start=x["Start"]["Id"], c0=x["FinishTime"], k4=y["FinishTime"],
                              gain=1 - y["FinishTime"] / x["FinishTime"]))
    stats = dict(worlds=len(rows), cpu_seconds=cpu, pass_k4=sum(r["result"]["S3"] and r["result"]["Reserve"] for r in by["K4"]),
                 pass_c0=sum(r["result"]["S3"] and r["result"]["Reserve"] for r in by["C0"]),
                 accepted_six=sum(r["accepted_bends"] == 6 for r in by["K4"]),
                 median_c0=median([r["c0"] for r in pairs]) if pairs else None,
                 median_k4=median([r["k4"] for r in pairs]) if pairs else None,
                 median_paired_gain=median([r["gain"] for r in pairs]) if pairs else None)
    repeated = repeatability(e)
    print(json.dumps(stats, indent=2))
    if args.audit_only:
        return
    out.mkdir(parents=True, exist_ok=True)
    summary = dict(stats=stats, pairs=pairs, rows=rows, protocol=protocol, freeze=freeze, repeated_native_prefixes=repeated,
                   mapping=load(e / "mapping.json"), historical=historical(geometry, freeze["Held"]))
    (out / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    (out / "PRZEJAZDY.md").write_text(tables(rows), encoding="utf-8")
    figures(out, geometry, rows, traces, reviews)
    groups = {"172-development.zip": [q["id"] for q in completed if q["Stage"] != "validate"],
              "172-validation-K4.zip": [r["result"]["Id"] for r in by["K4"]],
              "172-validation-C0.zip": [r["result"]["Id"] for r in by["C0"]]}
    for name, identifiers in groups.items():
        with ZipFile(out / name, "w", ZIP_STORED) as archive:
            for label in identifiers:
                archive.write(e / (label + ".zip"), label + ".zip")
    with ZipFile(out / "172-protocol.zip", "w", ZIP_DEFLATED) as archive:
        for path in sorted(e.iterdir()):
            if path.suffix in (".json", ".jsonl"):
                archive.write(path, path.name)


def tables(rows):
    def fmt(x):
        return "—" if x is None else f"{x:.3f}"
    text = ["| Próba | Kontroler | Okrążenia | Czas S3 [s] | Okr. 1 | Okr. 2 | Okr. 3 | Droga S3 [m] | Max β [°] | Min swept [m] | Rezerwa | Wynik |",
            "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|"]
    for row in rows:
        r = row["result"]
        times = [p["seconds"] if p["complete"] else None for p in row["laps"]]
        times += [None] * (3 - len(times))
        text.append(f'| {r["Id"]} | {r["Controller"]} | {r["Crossings"]} | ' +
                    " | ".join(fmt(x) for x in [r["FinishTime"], *times, row["actual_s3_distance"], r["MaxBetaDegrees"], r["MinSweep"]]) +
                    f' | {r["Reserve"]} | {r["Reason"]} |')
    text += ["", "| Próba | Okr. | Czas [s] | Łuk 1 / 2 [s] | Vmin 1 / 2 [km/h] | V wyj. 1 / 2 | Vmax prostych | Gaz aktywny ≥95% | Min pose / swept [m] | CPU [s] |",
             "|---|---:|---:|---|---|---|---|---:|---|---:|"]
    for row in rows:
        for lap in row["laps"]:
            bends, straights = lap["bends"], lap["straights"]
            paired = lambda key: " / ".join(fmt(p[key]) for p in bends)
            vmax = " / ".join(fmt(p["maximum_kph"]) for p in straights)
            text.append(f'| {row["result"]["Id"]} | {lap["lap"]} | {fmt(lap["seconds"])} | {paired("seconds")} | '
                        f'{paired("minimum_kph")} | {paired("exit_kph")} | {vmax} | {lap["high_share"]*100:.1f}% | '
                        f'{lap["min_pose"]:.3f} / {lap["min_swept"]:.3f} | {lap["cpu_seconds"]:.3f} |')
    return "\n".join(text) + "\n"


def figures(out, geometry, rows, traces, reviews):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    full = [r for r in rows if r["result"]["Stage"] == "validate" and r["result"]["Controller"] == "K4"]
    selected = full[0]
    start = selected["result"]["Start"]["Id"]
    other = next(r for r in rows if r["result"]["Stage"] == "validate" and r["result"]["Controller"] == "C0" and r["result"]["Start"]["Id"] == start)
    colors = {"K4": "#087f78", "C0": "#475569"}
    length = geometry["LapMeters"]
    frame = [q for segment in geometry["Segments"] for q in segment["Sections"]]
    fig, axes = plt.subplots(1, 3, figsize=(14, 4.7), layout="constrained")
    trace = traces[selected["result"]["Id"]]
    for lap, ax in enumerate(axes):
        for wall in ("Inner", "Outer"):
            ax.plot([p[wall]["X"] for p in frame], [p[wall]["Y"] for p in frame], color="#a8b4c0", lw=1)
        for row in full:
            line = [p for p in traces[row["result"]["Id"]] if lap * length <= p["s"] <= (lap + 1) * length]
            ax.plot([p["x"] for p in line], [p["y"] for p in line], color=colors["K4"], lw=1, alpha=.5)
        ax.plot([0, 0], [-11, 1], color="#b45309", ls=":", lw=1.2)
        ax.text(0, -15, f"Meta {lap+1}", ha="center", fontsize=8, color="#b45309")
        ax.text(62, 36, f"Łuk {lap*2+1}", ha="center", fontsize=8)
        ax.text(-49, 36, f"Łuk {lap*2+2}", ha="center", fontsize=8)
        ax.set(aspect="equal", title=f"Okrążenie {lap+1}", xlabel="X [m]", ylabel="Y [m]")
    fig.savefig(out / "172-xy.png", dpi=150)
    plt.close(fig)
    fig, axes = plt.subplots(5, 2, figsize=(14, 13), sharex=True, layout="constrained")
    panels = [("Prędkość [km/h]", ("kph",)), ("Gaz żądany / aktywny", ("requested", "active")),
              ("β / βdot [°, °/s]", ("beta", "beta_dot")), ("Yaw [rad/s]", ("yaw",)),
              ("Skręt żądany / aktywny [°]", ("steer_request", "steer_active")),
              ("Wheelspin κ", ("slip",)), ("Fy przód [N]", ("front_fy",)),
              ("Fx / Fy tył [N]", ("rear_fx", "rear_fy")), ("Prześwit [m]", ("pose", "swept")),
              ("Praca poślizgu [kJ]", ("loss",))]
    for row in (other, selected):
        trace = traces[row["result"]["Id"]]
        cumulative = 0
        for q in trace:
            cumulative += sum(q["losses"].get(k, 0) for k in ("front_lateral", "rear_lateral", "rear_longitudinal"))
            q.update(pose=min(q["inner"], q["outer"]), loss=cumulative/1000)
        for ax, (title, fields) in zip(axes.flat, panels):
            for field, style in zip(fields, ("-", "--")):
                ax.plot([q["s"] for q in trace], [q[field] for q in trace], ls=style,
                        color=colors[row["result"]["Controller"]], lw=.9,
                        label=row["result"]["Controller"]+" · "+field)
            ax.set_ylabel(title)
            ax.legend(fontsize=7, loc="best", ncols=2)
    for ax in axes.flat:
        for lap in range(3):
            for lo, hi in ((geometry["Segments"][1]["StartReferenceMeters"],geometry["Segments"][4]["StartReferenceMeters"]),
                           (geometry["Segments"][5]["StartReferenceMeters"],geometry["Segments"][8]["StartReferenceMeters"])):
                ax.axvspan(lap*length+lo, lap*length+hi, alpha=.07, color="#64748b")
            ax.axvline((lap+1)*length, color="#b45309", ls=":", lw=1)
        ax.grid(alpha=.15)
    for ax in axes[-1]:
        ax.set_xlabel("Całkowity postęp referencyjny [m]")
    fig.savefig(out / "172-telemetria.png", dpi=145)
    plt.close(fig)
    fig, (bends, cost) = plt.subplots(1, 2, figsize=(12, 4.8), layout="constrained")
    for row in (other, selected):
        values = [p["seconds"] for lap in row["laps"] for p in lap["bends"]]
        bends.plot(range(1,len(values)+1), values, marker="o", color=colors[row["result"]["Controller"]], label=row["result"]["Controller"])
    bends.set(xlabel="Łuk", ylabel="Czas [s]", xticks=range(1,7))
    bends.legend()
    decisions = reviews[selected["result"]["Id"]]
    for key, label, tint in (("DecisionMilliseconds","decyzja","#475569"),):
        cost.plot([q["Reference"] for q in decisions], [q[key] for q in decisions], color=tint, label=label)
    cost.plot([q["Reference"] for q in decisions], [q["Tracking"]["PlanningMilliseconds"] for q in decisions],
              color="#087f78", label="planowanie")
    cost.set(xlabel="Całkowity postęp [m]", ylabel="Czas ścienny [ms]", yscale="symlog")
    cost.legend()
    for lap in range(3):
        cost.axvline((lap+1)*length, ls=":", color="#b45309")
        for lo, hi in ((geometry["Segments"][1]["StartReferenceMeters"],geometry["Segments"][4]["StartReferenceMeters"]),
                       (geometry["Segments"][5]["StartReferenceMeters"],geometry["Segments"][8]["StartReferenceMeters"])):
            cost.axvspan(lap*length+lo, lap*length+hi, alpha=.07, color="#64748b")
    for ax in (bends,cost):
        ax.grid(alpha=.2)
    fig.savefig(out / "172-luki-koszt.png", dpi=150)
    plt.close(fig)


if __name__ == "__main__":
    main()

