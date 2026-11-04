"""Audit native #170 histories and generate the study tables and plots without new worlds."""

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
from statistics import median
import sys
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "fast-two-corners"))
from analyze167 import energy, section_time, telemetry, turn_onset

ZERO = {"SteeringRadians": 0, "Drive": 0}
EPS = 1e-8
HIGH = .95


def load(path):
    return json.loads(path.read_bytes())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def check_queue(run, trial, protocol):
    rider = run["Riders"][0]
    commands = []
    delay, period = protocol["Delay"], protocol["ReviewSeconds"]
    decisions = rider["Decisions"]
    reviews = trial["Reviews"]
    require(len(reviews) == len(decisions), "review count")
    for index, (item, review) in enumerate(zip(decisions, reviews)):
        d = item["Choice"]
        require(abs(d["Time"] - index * period) < EPS and d["Time"] == review["Time"],
                "native decision period")
        require(d["Action"] == review["Chosen"] and d["ForceEvaluations"] == review["Queries"]
                and d["Forecasts"] == review["Forecasts"], "native decision differs from chosen action")
        commands.append({"ObservedAt": d["Time"], "ActivationAt": d["Time"] + delay, "Action": d["Action"]})

    samples = rider["Samples"]
    last = samples[-1]["Input"]["Time"]
    expected = [c for c in commands if c["ActivationAt"] < last - EPS]
    require(expected == rider["Activations"], "native activations")
    for q in samples:
        i = q["Input"]
        t = i["Time"]
        observed = [c for c in commands if c["ObservedAt"] < t - EPS] if t > EPS else commands[:1]
        active = [c["Action"] for c in observed if c["ActivationAt"] < t - EPS]
        pending = [c for c in observed if c["ActivationAt"] >= t - EPS]
        require(i["Active"] == (active[-1] if active else ZERO) and i["Pending"] == pending,
                f"accepted queue or active command at {t}")
        requested = pending[-1]["Action"] if pending else i["Active"]
        require(q["RequestedDrive"] == requested["Drive"] and q["RequestedSteering"] == requested["SteeringRadians"],
                f"reported requested command at {t}")
    return len(expected)


def time_window(trace, start, end):
    if start is None:
        return None
    end = end if end is not None else trace[-1]["t"]
    if end <= start:
        return None
    high, peak, spans = 0, 0, []
    for a, b in zip(trace, trace[1:]):
        lo, hi = max(a["t"], start), min(b["t"], end)
        if hi <= lo:
            continue
        peak = max(peak, b["active"])
        if b["active"] < HIGH:
            continue
        high += hi - lo
        dt = b["t"] - a["t"]
        def value(key, t):
            return a[key] + (b[key] - a[key]) * (t - a["t"]) / dt
        if spans and abs(spans[-1]["end_t"] - lo) <= EPS:
            spans[-1].update(end_t=hi, end_s=value("s", hi), end_kph=value("kph", hi))
        else:
            spans.append(dict(start_t=lo, end_t=hi, start_s=value("s", lo), end_s=value("s", hi),
                              start_kph=value("kph", lo), end_kph=value("kph", hi)))
    for span in spans:
        span["seconds"] = span["end_t"] - span["start_t"]
        span["gain_kph"] = span["end_kph"] - span["start_kph"]
    return dict(seconds=end - start, high_seconds=high, high_share=high / (end - start), peak=peak,
                first=spans[0] if spans else None, longest=max(spans, key=lambda row: row["seconds"]) if spans else None,
                intervals=spans)


def inspect(run, trial, protocol):
    result = trial["Result"]
    samples = run["Riders"][0]["Samples"]
    trace = telemetry(samples)
    for q, p in zip(samples, trace):
        f = q["Forces"]
        p.update(front_fx=0, front_fy=f["FrontLateral"] if f else 0,
                 rear_fx=f["RearLongitudinal"] if f else 0, rear_fy=f["RearLateral"] if f else 0,
                 active_steer=math.degrees(q["Input"]["Active"]["SteeringRadians"]))
    require(result["Fingerprint"] == run["Fingerprint"], "native fingerprint")
    require(run["Forecasts"] == sum(row["Forecasts"] for row in trial["Reviews"]) and
            run["ForecastContactQueries"] == sum(row["Queries"] for row in trial["Reviews"]), "native counters")
    activations = check_queue(run, trial, protocol)
    start = samples[0]["Input"]
    require(start["Rider"]["Speed"] == start["Omega"] == 0, "mechanically stationary start")
    sections = {"first": protocol["First"], "straight": {"Entry": protocol["First"]["Exit"],
                "Exit": protocol["Second"]["Entry"]}, "second": protocol["Second"],
                "final_straight": {"Entry": protocol["Second"]["Exit"], "Exit": protocol["Goal"]}}
    details = {}
    for name, boundaries in sections.items():
        lo, hi = boundaries["Entry"], boundaries["Exit"]
        entered, exited = section_time(trace, lo), section_time(trace, hi)
        points = [p for p in trace if lo <= p["s"] <= hi]
        details[name] = dict(entry_t=entered, exit_t=exited,
                             throttle=time_window(trace, entered, exited),
                             energy_j=energy(trace, boundaries),
                             peaks={k: max((abs(p[k]) for p in points), default=None)
                                    for k in ("beta", "beta_dot", "yaw", "side", "slip",
                                              "front_fx", "front_fy", "rear_fx", "rear_fy")},
                             min_pose_m=min((min(p["inner"], p["outer"]) for p in points), default=None),
                             min_swept_m=min((p["swept"] for p in points if p["swept"] is not None), default=None))
    total = result["Goal"]["Seconds"] if result["Goal"] else None
    exits = []
    for bend, end in ((result["First"], details["straight"]["exit_t"]), (result["Second"], None)):
        apex = bend["Apex"]
        exits.append(time_window(trace, apex["Seconds"] if apex else None, end))
    bad = result["BadSeconds"]
    before_bad = None
    if bad is not None:
        i = next(i for i, p in enumerate(trace) if p["t"] >= bad - EPS)
        q = samples[i]["Input"]
        before_bad = {"time": q["Time"], "active": q["Active"], "pending": q["Pending"],
                      "last_review": next((x for x in reversed(trial["Reviews"]) if x["Time"] < bad), None),
                      "beta_degrees": trace[i]["beta"], "yaw": trace[i]["yaw"]}
    changes = [abs(b["Chosen"]["SteeringRadians"] - a["Chosen"]["SteeringRadians"]) for a, b in
               zip(trial["Reviews"], trial["Reviews"][1:])]
    count = len(trial["Reviews"])
    return trace, dict(result=result, controller=trial["Controller"], stage=trial["Stage"],
                       sections=details, high=time_window(trace, 0, total), exits=exits,
                       turn_onset=[turn_onset(trace, protocol[key]["Entry"]) for key in ("First", "Second")],
                       first_failure=before_bad, activations=activations, queue_verified=True,
                       steering_corrections=sum(x >= .0015 for x in changes),
                       max_steering_change_degrees=math.degrees(max(changes, default=0)),
                       modes=dict(Counter(row["Mode"] for row in trial["Reviews"])),
                       decisions=count, native_steps=run["NativeSteps"], forecasts=run["Forecasts"],
                       prediction_queries=run["ForecastContactQueries"],
                       prediction_wall_ms=sum(row["PredictionMilliseconds"] for row in trial["Reviews"]),
                       decision_wall_ms=trial["DecisionWallMilliseconds"],
                       decision_wall_ms_mean=trial["DecisionWallMilliseconds"] / count,
                       world_cpu_s=run["TotalCpuSeconds"], decision_cpu_s=run["ControllerCpuSeconds"],
                       physics_observation_cpu_s=run["NativeAndObservationCpuSeconds"])


def tables(rows, frozen):
    def value(number, places=2):
        return "—" if number is None else f"{number:.{places}f}"
    lines = ["| Start | Kontroler | Łuk 1 [s] | V wyjścia 1 | Vmax prostej | Łuk 2 [s] | V wyjścia 2 | Czas [s] | Aktywny ≥95% | Min swept [m] | Wynik |",
             "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|"]
    for row in rows:
        r = row["result"]
        a, b = r["First"], r["Second"]
        high = row["high"]["high_share"] * 100
        status = "cel + rezerwa" if r["Pass"] else f'DNF · {r["EndReference"]:.2f} m'
        v1 = a["Exit"]["Kph"] if a["Exit"] else None
        v2 = b["Exit"]["Kph"] if b["Exit"] else None
        t = r["Goal"]["Seconds"] if r["Goal"] else None
        lines.append(f'| {r["Start"]["Id"]} | {row["controller"]} | {value(a["Seconds"], 3)} | {value(v1)} | '
                     f'{value(r["StraightMaximumKph"])} | {value(b["Seconds"], 3)} | {value(v2)} | '
                     f'{value(t, 3)} | {high:.1f}% | {value(r["MinimumSwept"], 3)} | {status} |')
    lines += ["", "| Start | Kontroler | Łuk 1 wejście/min/apex/wyjście [km/h] | Łuk 2 wejście/min/apex/wyjście [km/h] |",
              "|---|---|---:|---:|"]
    for row in rows:
        r = row["result"]
        def speeds(b):
            return " / ".join(value(x, 1) for x in (
                b["Entry"]["Kph"] if b["Entry"] else None, b["MinimumKph"],
                b["Apex"]["Kph"] if b["Apex"] else None, b["Exit"]["Kph"] if b["Exit"] else None))
        lines.append(f'| {r["Start"]["Id"]} | {row["controller"]} | {speeds(r["First"])} | {speeds(r["Second"])} |')
    lines += ["", "| Start | Polityka zamrożona | Wyjście 1 · t [s], s [m], okres [s], ΔV [km/h] | Wyjście 2 · t [s], s [m], okres [s], ΔV [km/h] |",
              "|---|---|---|---|"]
    for row in rows:
        if row["stage"] != "validate" or row["controller"] != frozen:
            continue
        def high(window):
            hit = window["first"] if window else None
            return (" / ".join(f"{hit[key]:.2f}" for key in ("start_t", "start_s", "seconds", "gain_kph"))
                    if hit else f'— (max {100 * window["peak"]:.0f}%)' if window else "—")
        lines.append(f'| {row["result"]["Start"]["Id"]} | {frozen} | {high(row["exits"][0])} | {high(row["exits"][1])} |')
    return "\n".join(lines) + "\n"


def plots(out, geometry, protocol, traces, trials, frozen):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    palette = {"baseline": "#334155", "final": "#087f78", "dnf": "#b45309"}
    valid = [row for row in trials.values() if row["Stage"] == "validate" and row["Controller"] == frozen]
    representative = valid[0]["Result"]["Id"] if valid else "rolloff-rolloff-H3"
    start = valid[0]["Result"]["Start"]["Id"] if valid else "H3"
    baseline = "validate-baseline-" + start if valid else "baseline-baseline-" + start
    selected = {baseline: ("#166 " + start, palette["baseline"]),
                representative: ("#170 " + start, palette["final"])}
    if "first-first-H3" in traces:
        selected["first-first-H3"] = ("B1 H3 · DNF", palette["dnf"])
    fig, (xy, speed) = plt.subplots(1, 2, figsize=(12, 5), layout="constrained")
    frame = [q for segment in geometry["Segments"] for q in segment["Sections"]]
    for wall in ("Inner", "Outer"):
        xy.plot([q[wall]["X"] for q in frame], [q[wall]["Y"] for q in frame], color="#a3aeba", lw=1)
    for row in valid:
        trace = traces[row["Result"]["Id"]]
        xy.plot([p["x"] for p in trace], [p["y"] for p in trace], color=palette["final"], alpha=.2)
    for name, (label, color) in selected.items():
        p = traces[name]
        xy.plot([q["x"] for q in p], [q["y"] for q in p], color=color, lw=1.7, label=label)
        speed.plot([q["s"] for q in p], [q["kph"] for q in p], color=color, lw=1.6, label=label)
        if name == representative:
            high = [q for q in p if q["active"] >= HIGH and q["s"] > protocol["First"]["Entry"]]
            xy.scatter([q["x"] for q in high], [q["y"] for q in high], s=5, color="#e11d48",
                       label="aktywny ≥95% po starcie")
            speed.scatter([q["s"] for q in high], [q["kph"] for q in high], s=5, color="#e11d48")
    xy.set(xlabel="X [m]", ylabel="Y [m]", aspect="equal", title="Trajektoria na progressive-b")
    xy.legend(loc="lower center", fontsize=8)
    speed.set(xlabel="Droga referencyjna [m]", ylabel="Prędkość [km/h]", title="Prędkość i przekroje")
    for bend in ("First", "Second"):
        speed.axvspan(protocol[bend]["Entry"], protocol[bend]["Exit"], alpha=.12, color="#94a3b8")
    speed.axvline(protocol["Goal"], color="#64748b", ls=":")
    speed.grid(alpha=.2)
    speed.legend(fontsize=8)
    fig.savefig(out / "170-xy-predkosc.png", dpi=150)
    plt.close(fig)

    panels = [
        ("Gaz [0–1]", (("requested", "żądany"), ("active", "aktywny"))),
        ("Skręt [°]", (("steer_request", "żądany"), ("active_steer", "aktywna komenda"), ("steer", "aktuator"))),
        ("β [°] i βdot [°/s]", (("beta", "β"), ("beta_dot", "βdot"))),
        ("Yaw [rad/s] i v boczna [m/s]", (("yaw", "yaw"), ("side", "v boczna"))),
        ("Siła przedniej opony [N]", (("front_fx", "Fx"), ("front_fy", "Fy"))),
        ("Siła tylnej opony [N]", (("rear_fx", "Fx"), ("rear_fy", "Fy"))),
        ("Wheelspin κ", (("slip", "κ"),)),
        ("Prześwit [m]", (("pose", "pose"), ("swept", "swept"))),
        ("Skumulowane straty [kJ]", (("slip_loss", "poślizg"), ("net_loss", "całkowite"))),
    ]
    fig, axes = plt.subplots(5, 2, figsize=(13, 12), layout="constrained", sharex=True)
    styles = ("-", "--", ":")
    for name, (label, color) in selected.items():
        trace = traces[name]
        slip = net = 0
        for p in trace:
            slip += sum(p["losses"].get(k, 0) for k in ("front_lateral", "rear_lateral", "rear_longitudinal"))
            net += p["losses"].get("net", 0)
            p.update(pose=min(p["inner"], p["outer"]), slip_loss=slip / 1000, net_loss=net / 1000)
        for ax, (title, fields) in zip(axes.flat, panels):
            for (key, legend), style in zip(fields, styles):
                ax.plot([p["s"] for p in trace], [p[key] for p in trace], color=color,
                        linestyle=style, lw=1, label=label + " · " + legend)
            ax.set_ylabel(title)
            ax.legend(fontsize=6, loc="best", ncols=2)
    axes[-1, -1].set_visible(False)
    axes[-1, 0].set_xlabel("Droga referencyjna [m]")
    axes[-2, 1].set_xlabel("Droga referencyjna [m]")
    for ax in axes.flat:
        ax.grid(alpha=.18)
        for bend in ("First", "Second"):
            ax.axvspan(protocol[bend]["Entry"], protocol[bend]["Exit"], alpha=.08, color="#94a3b8")
    fig.savefig(out / "170-telemetria.png", dpi=140)
    plt.close(fig)

    rows = trials[representative]["Reviews"]
    modes = list(dict.fromkeys(q["Mode"] for q in rows))
    fig, (state, cpu) = plt.subplots(2, 1, figsize=(11, 5), sharex=True, layout="constrained")
    state.step([q["Reference"] for q in rows], [modes.index(q["Mode"]) for q in rows], where="post", color=palette["final"])
    state.set_yticks(range(len(modes)), modes)
    state.set_title("Tryb na kolejnych decyzjach · " + start)
    cpu.plot([q["Reference"] for q in rows], [q["PredictionMilliseconds"] for q in rows], color=palette["final"])
    cpu.set(xlabel="Droga referencyjna [m]", ylabel="Czas prognoz [ms]")
    count = cpu.twinx()
    count.step([q["Reference"] for q in rows], [q["Queries"] for q in rows], where="post", color="#64748b", alpha=.6)
    count.set_ylabel("Zapytania o kontakt")
    for ax in (state, cpu):
        ax.grid(alpha=.2)
    fig.savefig(out / "170-tryby-koszt.png", dpi=150)
    plt.close(fig)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--audit-only", action="store_true")
    args = parser.parse_args()
    e, out = args.evidence, args.output
    protocol, frozen = load(e / "protocol.json"), load(e / "freeze.json")
    require(sha(Path("docs/experiments/two-corner-full-drive/held.json")) == frozen["HoldoutSha256"] ==
            protocol["HoldoutSha256"], "holdout changed")
    for name, digest in frozen["Sources"].items():
        require(sha(Path(name)) == digest, "frozen source changed: " + name)
    ledger = [json.loads(line) for line in (e / "ledger.jsonl").read_bytes().splitlines()]
    reservations = [row["id"] for row in ledger if row["Event"] == "reserved"]
    files = [row for row in ledger if row["Event"] == "result"]
    require(len(reservations) <= 108 and reservations == [row["id"] for row in files] and
            len(set(reservations)) == len(reservations), "native world ledger")
    charges = load(e / "validate.done.json")
    require(charges["TotalCpuSeconds"] <= 350, "CPU limit")
    traces, trials, rows = {}, {}, []
    for item in files:
        path = e / (item["id"] + ".zip")
        require(sha(path) == item["Sha256"], "native archive hash")
        with ZipFile(path) as archive:
            run = json.load(archive.open("native.json"))
            trial = json.load(archive.open("trial.json"))
        traces[item["id"]], row = inspect(run, trial, protocol)
        trials[item["id"]] = trial
        rows.append(row)
    selected = frozen["Candidate"]
    reference = [r for r in rows if r["stage"] == "validate" and r["controller"] == "baseline"]
    candidates = [r for r in rows if r["stage"] == "validate" and r["controller"] == selected]
    require(len(reference) == 8 and len(candidates) == (8 if selected else 0), "held trial count")
    pairs = []
    for a, b in zip(reference, candidates):
        x, y = a["result"], b["result"]
        require(x["Start"] == y["Start"], "matched start")
        if x["Pass"] and y["Pass"]:
            t0, t1 = x["Goal"]["Seconds"], y["Goal"]["Seconds"]
            pairs.append(dict(start=x["Start"]["Id"], baseline_seconds=t0, candidate_seconds=t1,
                              difference_seconds=t1 - t0, gain=1 - t1 / t0))
    stats = dict(worlds=len(rows), cpu_seconds=charges["TotalCpuSeconds"],
                 baseline_pass=sum(row["result"]["Pass"] for row in reference),
                 candidate_pass=sum(row["result"]["Pass"] for row in candidates),
                 full_gas_exit_counts=[sum(bool(row["exits"][i] and row["exits"][i]["first"])
                                           for row in candidates) for i in (0, 1)],
                 median_paired_gain=median([x["gain"] for x in pairs]) if pairs else None,
                 median_baseline=median([x["baseline_seconds"] for x in pairs]) if pairs else None,
                 median_candidate=median([x["candidate_seconds"] for x in pairs]) if pairs else None)
    print(json.dumps(stats, indent=2))
    if args.audit_only:
        return
    out.mkdir(parents=True, exist_ok=True)
    (out / "summary.json").write_text(json.dumps(dict(stats=stats, pairs=pairs, protocol=protocol,
                                               freeze=frozen, rows=rows), indent=2) + "\n", encoding="utf-8")
    (out / "TABELE.md").write_text(tables(rows, selected), encoding="utf-8")
    plots(out, load(e / "geometry.json"), protocol, traces, trials, selected)
    with ZipFile(out / "170-dowody.zip", "w", ZIP_DEFLATED, compresslevel=5) as archive:
        for path in sorted(e.iterdir()):
            if path.suffix in (".json", ".jsonl", ".zip"):
                archive.write(path, path.name, compress_type=ZIP_STORED if path.suffix == ".zip" else ZIP_DEFLATED)


if __name__ == "__main__":
    main()
