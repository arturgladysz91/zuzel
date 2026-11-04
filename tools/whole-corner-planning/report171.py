"""Audit #171 native receipts, compare archived K0, and render the evidence."""

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
from statistics import median
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED

from audit171 import ARCHIVE, load_entry, section, trace_of


def load(path):
    return json.loads(path.read_bytes())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def verify(run, trial):
    rider = run["Riders"][0]
    samples = rider["Samples"]
    last = samples[-1]["Input"]["Time"]
    start = samples[0]["Input"]
    assert start["Rider"]["Speed"] == start["Omega"] == 0
    assert run["Fingerprint"] == trial["Result"]["Fingerprint"]
    zero = {"SteeringRadians": 0, "Drive": 0}
    commands = []
    assert len(trial["Reviews"]) == len(rider["Decisions"])
    for i, (review, native) in enumerate(zip(trial["Reviews"], rider["Decisions"])):
        choice = native["Choice"]
        t = choice["Time"]
        assert abs(t - i * .2) < 1e-8 and t == review["Time"] and choice["Action"] == review["Chosen"]
        assert choice["ForceEvaluations"] == review["Queries"] and choice["Forecasts"] == review["Forecasts"]
        commands.append(dict(ObservedAt=t, ActivationAt=t + .24, Action=choice["Action"]))
    assert [c for c in commands if c["ActivationAt"] < last - 1e-8] == rider["Activations"]
    for sample in samples:
        observation = sample["Input"]
        t = observation["Time"]
        observed = [c for c in commands if c["ObservedAt"] < t - 1e-8] if t > 1e-8 else commands[:1]
        active = [c["Action"] for c in observed if c["ActivationAt"] < t - 1e-8]
        pending = [c for c in observed if c["ActivationAt"] >= t - 1e-8]
        assert observation["Pending"] == pending
        assert observation["Active"] == (active[-1] if active else zero)


def high_drive(trace, start, end):
    if start is None:
        return None
    intervals = []
    for a, b in zip(trace, trace[1:]):
        lo, hi = max(start, a["t"]), min(end or trace[-1]["t"], b["t"])
        if hi <= lo or b["active"] < .95:
            continue
        pos = lambda q, t: a[q] + (b[q] - a[q]) * (t - a["t"]) / (b["t"] - a["t"])
        if intervals and abs(intervals[-1]["end"] - lo) < 1e-8:
            intervals[-1].update(end=hi, end_kph=pos("kph", hi))
        else:
            intervals.append(dict(start=lo, end=hi, reference=pos("s", lo), start_kph=pos("kph", lo),
                                  end_kph=pos("kph", hi), peak=b["active"]))
        intervals[-1]["peak"] = max(intervals[-1]["peak"], b["active"])
    return intervals


def exit_windows(result, trace):
    a, b = result["First"], result["Second"]
    return [high_drive(trace, x["Apex"]["Seconds"] if x["Apex"] else None, end)
            for x, end in ((a, b["Entry"]["Seconds"] if b["Entry"] else None), (b, None))]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--audit-only", action="store_true")
    args = parser.parse_args()
    e, out = args.evidence, args.output
    protocol, frozen = load(e / "protocol.json"), load(e / "freeze.json")
    for name, digest in frozen["Sources"].items():
        assert sha(Path(name)) == digest, name
    assert frozen["Sources"]["docs/experiments/whole-corner-planning/held.json"] == protocol["Holdout"]
    ledger = [json.loads(line) for line in (e / "ledger.jsonl").read_bytes().splitlines()]
    reservations = [x["id"] for x in ledger if x["Event"] == "reserved"]
    results = [x for x in ledger if x["Event"] == "result"]
    assert reservations == [x["id"] for x in results] and len(set(reservations)) == len(results) <= 80
    cpu = load(e / "validate.done.json")["TotalCpuSeconds"]
    assert cpu <= 300
    mapping = load(e / "mapping.json")
    assert [(x["Visit"]["Lap"], x["Visit"]["Index"]) for x in mapping] == [(a, b) for a in range(3) for b in range(2)]

    traces, reviews, rows = {}, {}, []
    with ZipFile(ARCHIVE) as archive:
        geometry = json.loads(archive.read("geometry.json"))
        for result in protocol["Archived"]:
            start = result["Start"]["Id"]
            run = load_entry(archive, f"reserve-speed-reserve-speed-{start}.zip")
            assert run["Fingerprint"] == result["Fingerprint"]
            traces["K0-" + start] = trace_of(run)
    turns = [g for g in geometry["Segments"] if g["Type"] != "Straight"]
    for item in results:
        label = item["id"]
        path = e / (label + ".zip")
        assert sha(path) == item["Sha256"]
        with ZipFile(path) as archive:
            run = json.loads(archive.read("native.json"))
            trial = json.loads(archive.read("trial.json"))
            plans = json.loads(archive.read("plans.json"))
        verify(run, trial)
        trace = trace_of(run)
        traces[label] = trace
        reviews[label] = trial.pop("Reviews")
        result = trial["Result"]
        bad = result["BadSeconds"]
        before = next((r for r in reversed(reviews[label]) if r["Time"] < bad), None) if bad else None
        bad_sample = next((q["Input"] for q in run["Riders"][0]["Samples"] if q["Input"]["Time"] >= bad - 1e-8),
                          None) if bad else None
        decisions = len(reviews[label])
        trial.update(
            queue_verified=True,
            sections=[section(trace, b) for b in turns],
            exit_drive=exit_windows(result, trace),
            review_count=decisions,
            tracking_counts=dict(Counter(r["Gate"] for r in reviews[label])),
            mean_decision_ms=sum(r["DecisionMilliseconds"] for r in reviews[label]) / decisions,
            mean_prediction_ms=sum(r["PredictionMilliseconds"] for r in reviews[label]) / decisions,
            prediction_wall_ms=sum(r["PredictionMilliseconds"] for r in reviews[label]),
            world_cpu=run["TotalCpuSeconds"], decision_cpu=run["ControllerCpuSeconds"],
            native_cpu=run["NativeAndObservationCpuSeconds"], first_bad_command=before,
            first_bad_sample=bad_sample, distance_at_goal=result["Goal"]["Distance"] if result["Goal"] else None,
            plan_events=[{k: x for k, x in plan.items() if k != "Plans"} for plan in plans])
        rows.append(trial)

    pairs = []
    finals = [r for r in rows if r["Stage"] == "validate"]
    if frozen["Winner"]:
        assert len(finals) == 16
        for a, b in zip(finals[::2], finals[1::2]):
            assert a["Strategy"] == "K0" and a["Result"]["Start"] == b["Result"]["Start"]
            if a["Result"]["Pass"] and b["Result"]["Pass"]:
                a0 = a["Result"]["Goal"]["Seconds"]
                b0 = b["Result"]["Goal"]["Seconds"]
                pairs.append(dict(start=a["Result"]["Start"]["Id"], k0=a0, candidate=b0, gain=1 - b0 / a0))
    else:
        assert not finals
    stats = dict(worlds=len(rows), cpu_seconds=cpu, candidate=frozen["Winner"],
                 n_final=len(finals), baseline_pass=sum(r["Result"]["Pass"] for r in finals[::2]),
                 candidate_pass=sum(r["Result"]["Pass"] for r in finals[1::2]),
                 paired_gain=median([p["gain"] for p in pairs]) if pairs else None)
    print(json.dumps(stats, indent=2))
    if args.audit_only:
        return
    out.mkdir(parents=True, exist_ok=True)
    info = dict(stats=stats, pairs=pairs, rows=rows, archived=protocol["Archived"],
                mapping=mapping, screening=load(e / "screen-timing.json"), frozen=frozen)
    (out / "summary.json").write_text(json.dumps(info, indent=2) + "\n", encoding="utf-8")
    (out / "STRATEGIE.md").write_text(table(rows, protocol["Archived"]), encoding="utf-8")
    render(e, out, geometry, turns, traces, rows, reviews)
    with ZipFile(out / "171-dowody.zip", "w", ZIP_DEFLATED, compresslevel=5) as archive:
        for p in sorted(e.iterdir()):
            if p.suffix in (".json", ".jsonl", ".zip"):
                archive.write(p, p.name, compress_type=ZIP_STORED if p.suffix == ".zip" else ZIP_DEFLATED)


def table(rows, archived):
    lines = ["| Wariant | Start | Łuk 1 [s] | Vmax prostej [km/h] | Łuk 2 [s] | Czas całości [s] | Droga zapisu [m] | Max β [°] | Min swept [m] | Wynik |",
             "|---|---|---:|---:|---:|---:|---:|---:|---:|---|"]
    def fmt(x):
        return "—" if x is None else f"{x:.3f}"
    for r in [dict(Strategy="K0", Stage="archiwum", Result=x, Distance=None) for x in archived] + rows:
        x = r["Result"]
        outcome = "pełny + rezerwa" if x["Pass"] else (
            f'drugie wejście w {r["SecondEntry"]["Seconds"]:.3f} s'
            if r.get("ReachedSecondEntry") and r["Stage"] == "short" else x["Reason"])
        values = [x["First"]["Seconds"], x["StraightMaximumKph"], x["Second"]["Seconds"],
                  x["Goal"]["Seconds"] if x["Goal"] else None, r["Distance"], x["MaximumBetaDegrees"], x["MinimumSwept"]]
        lines.append(f'| {r["Strategy"]} · {r["Stage"]} | {x["Start"]["Id"]} | ' +
                     " | ".join(fmt(v) for v in values) + f" | {outcome} |")
    return "\n".join(lines) + "\n"


def render(e, out, geometry, turns, traces, rows, reviews):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    paths = load(e / "screen.json")
    color = dict(K0="#334155", K1="#a85d1d", K2="#7c3eb7", K3="#078675", K4="#1d62a4")
    held = [x for x in rows if x["Stage"] == "validate" and x["Strategy"] != "K0" and x["Result"]["Pass"]]
    full = [x for x in rows if x["Stage"] == "full" and x["Result"]["Pass"]]
    representative = (held or full or [x for x in rows if x["Strategy"] == "K3"])[0]
    label = representative["Result"]["Id"]
    start = representative["Result"]["Start"]["Id"]
    baseline = ("validate-K0-" if held else "K0-") + start
    selected = {baseline: ("K0", traces[baseline]), label: (representative["Strategy"], traces[label])}
    boundary = [p for section in geometry["Segments"] for p in section["Sections"]]
    fig, (overview, zoom) = plt.subplots(1, 2, figsize=(13, 5), layout="constrained")
    for wall in ("Inner", "Outer"):
        overview.plot([p[wall]["X"] for p in boundary], [p[wall]["Y"] for p in boundary], color="#bac4cc", lw=1)
    plotted = set()
    for plan in paths:
        name = plan["Choice"]["Name"]
        ps = plan["Points"]
        axis_line = "-" if plan["Feasible"] else ":"
        overview.plot([p["XY"]["X"] for p in ps], [p["XY"]["Y"] for p in ps], ls=axis_line,
                      lw=1.25, color=color[name], alpha=.8, label=name if name not in plotted else None)
        plotted.add(name)
        if plan["Visit"]["Index"] == 0:
            zoom.plot([p["S"] for p in ps], [p["Offset"] for p in ps], ls=axis_line, lw=1.25, color=color[name])
    for b in turns[:3]:
        s = b["StartReferenceMeters"]
        zoom.axvline(s, color="#94a3b8", ls="--", lw=1)
        zoom.axvline(s + b["LengthReferenceMeters"], color="#94a3b8", ls="--", lw=1)
    for name, trace in selected.values():
        overview.plot([p["x"] for p in trace], [p["y"] for p in trace], lw=2, ls="--",
                      color=color[name], label=f"{name} · natywny {start}")
    overview.set(aspect="equal", xlabel="X [m]", ylabel="Y [m]", title="Kandydaci geometryczni obu łuków")
    overview.legend(ncols=4, loc="lower center", fontsize=8)
    zoom.set(xlabel="Postęp referencyjny [m]", ylabel="Offset [m]", title="Trzy sekcje pierwszego łuku")
    zoom.grid(alpha=.2)
    fig.savefig(out / "171-linie.png", dpi=150)
    plt.close(fig)


    fig, axes = plt.subplots(5, 2, figsize=(13, 13), sharex=True, layout="constrained")
    panels = [("Krzywizna [1/m]", ("curvature",)), ("V [km/h]", ("kph",)),
              ("Gaz żądany / aktywny", ("requested", "active")), ("β [°]", ("beta",)),
              ("Yaw [rad/s]", ("yaw",)), ("Fx/Fy tył [N]", ("rear_fx", "rear_fy")),
              ("Przód / tył · udział budżetu", ("front", "rear")),
              ("Prześwit wewn. / zewn. [m]", ("inner", "outer")),
              ("Straty netto [kJ]", ("net_kj",)), ("Offset [m]", ("offset",))]
    for name, trace in selected.values():
        loss = 0
        for p in trace:
            loss += p["losses"].get("net", 0)
            p["net_kj"] = loss / 1000
        for axis, (title, fields) in zip(axes.flat, panels):
            for field, style in zip(fields, ("-", "--")):
                axis.plot([q["s"] for q in trace], [q[field] for q in trace], style,
                          color=color[name], lw=1, label=name + " · " + field)
            axis.set_ylabel(title)
            axis.legend(fontsize=7, loc="best", ncols=2)
    for axis in axes.flat:
        for b in turns:
            axis.axvline(b["StartReferenceMeters"], color="#cbd5e1", lw=.7)
        axis.grid(alpha=.2)
    for axis in axes[-1]:
        axis.set_xlabel("Postęp referencyjny [m]")
    fig.savefig(out / "171-telemetria.png", dpi=140)
    plt.close(fig)

    fig, (energy, runtime) = plt.subplots(1, 2, figsize=(12, 4.5), layout="constrained")
    for index, (name, trace) in enumerate(selected.values()):
        pieces = [section(trace, b) for b in turns]
        xs = [i + (-.17 if index == 0 else .17) for i in range(6)]
        net = [p["energy"]["net"] / 1000 if p else math.nan for p in pieces]
        lateral = [(p["energy"]["front_lateral"] + p["energy"]["rear_lateral"]) / 1000 if p else math.nan for p in pieces]
        energy.bar(xs, net, width=.33, alpha=.35, color=color[name], label=name + " netto")
        energy.bar(xs, lateral, width=.33, color=color[name], label=name + " boczne")
    energy.set(xticks=range(6), xticklabels=["1.1", "1.2", "1.3", "2.1", "2.2", "2.3"],
               xlabel="Łuk i sekcja", ylabel="Strata energii [kJ]")
    energy.legend(fontsize=8)
    rr = reviews[label]
    for key, name, tint in (("DecisionMilliseconds", "decyzja", "#334155"),
                            ("PredictionMilliseconds", "prognoza", "#078675"),
                            ("PlanningMilliseconds", "planowanie", "#b45309")):
        runtime.plot([r["Reference"] for r in rr], [r[key] for r in rr], label=name, lw=1, color=tint)
    runtime.set(xlabel="Postęp referencyjny [m]", ylabel="Czas ścienny [ms]", yscale="symlog", ylim=(0, None))
    runtime.legend(fontsize=8)
    for axis in (energy, runtime):
        axis.grid(alpha=.2)
    fig.savefig(out / "171-energia-koszt.png", dpi=150)
    plt.close(fig)


if __name__ == "__main__":
    main()
