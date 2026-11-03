"""Read the frozen #166 archives, audit SHA256 and plot actual samples."""
import bisect
from collections import Counter
import gzip
import hashlib
import json
import math
from pathlib import Path
import statistics
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / "docs/experiments/autonomous-fast-corner"
E = DOC / "evidence"
HISTORICAL = ROOT / "docs/experiments/fast-corner-feasibility"


def read(path):
    return json.loads(path.read_bytes())


def lines(path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines()]


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def native(path, expected=None):
    if expected:
        assert digest(path.read_bytes()) == expected
    with ZipFile(path) as bundle:
        if expected:
            assert bundle.testzip() is None
            index = json.loads(bundle.read("index.json"))
            rows = bundle.read("policies.jsonl").splitlines()
            assert len(rows) == len(index["Policies"])
            for row, item in zip(rows, index["Policies"]):
                payload = row.split(b',"payload":', 1)[1][:-1]
                assert len(payload) == item["Bytes"] and digest(payload) == item["SHA256"]
                assert json.loads(row)["key"] == item["Key"]
        return json.loads(bundle.read("native.json"))


def view(s):
    return s["Input"]["Rider"]


def time(s):
    return s["Input"]["Time"]


def ref(s):
    return view(s)["TotalReferenceMeters"]


def course(v):
    return math.atan2(v["Velocity"]["Y"], v["Velocity"]["X"])


def dot(a, b):
    return a["X"] * b["X"] + a["Y"] * b["Y"]


def observation(s, previous):
    v, i = view(s), s["Input"]
    f, p = s["Forces"], s["Powertrain"]
    dt = time(s) - time(previous)
    return {
        "time": time(s), "reference": ref(s), "distanceXY": v["DistanceMeters"],
        "position": v["Position"], "speedKph": v["Speed"] * 3.6, "heading": v["Heading"],
        "chi": course(v), "beta": v["SlideRadians"], "yaw": v["AngularVelocity"],
        "betaDot": (v["SlideRadians"] - view(previous)["SlideRadians"]) / dt if dt > 0 else None,
        "offset": v["Road"]["OffsetMeters"], "outward": dot(v["Velocity"], v["Road"]["Outward"]),
        "active": i["Active"], "pending": i["Pending"], "steering": i["Steering"],
        "wheelOmega": i["Omega"], "crankOmega": s["CrankOmega"],
        "powertrain": p, "forces": f, "pose": min(s["Inner"], s["Outer"]), "swept": s["Certificate"],
    }


def audit_teachers(exit_s):
    edges = read(HISTORICAL / "evidence/track.json")[:-1]
    resolution = 318.77874994277954 / len(edges)

    def preview(reference):
        def heading(i):
            point = edges[i % len(edges)]
            dx = point["Outer"]["X"] - point["Inner"]["X"]
            dy = point["Outer"]["Y"] - point["Inner"]["Y"]
            return math.atan2(dx, -dy)
        result = []
        for metres in (4, 8, 16, 32):
            i = round((reference + metres) / resolution)
            change = (heading(i + 1) - heading(i - 1) + math.pi) % (2 * math.pi) - math.pi
            result.append({"ahead": metres, "heading": heading(i),
                           "curvaturePerMeter": change / (2 * resolution),
                           **edges[i % len(edges)]})
        return result

    data = []
    for id_ in ("C-B101", "C-B245"):
        path = HISTORICAL / "evidence/native" / f"{id_}.zip"
        run = native(path)["Riders"][0]
        samples = run["Samples"]
        times = [time(s) for s in samples]
        activations = run["Activations"]
        issued = [{"ObservedAt": d["Choice"]["Time"], "ActivationAt": d["Choice"]["Time"] + .24,
                   "Action": d["Choice"]["Action"]} for d in run["Decisions"]]
        for decision in run["Decisions"]:
            d = decision["Choice"]
            tick = d["Time"]
            i = bisect.bisect_left(times, tick - 1e-8)
            assert i < len(samples) and abs(times[i] - tick) < 1e-7
            row = observation(samples[i], samples[max(0, i - 1)])
            row["pending"] = [c for c in issued
                              if c["ObservedAt"] < tick - 1e-8 and c["ActivationAt"] > tick + 1e-8]
            prior_active = [c["Action"] for c in activations if c["ActivationAt"] <= tick + 1e-8]
            row["active"] = prior_active[-1] if prior_active else {"SteeringRadians": 0, "Drive": 0}
            continuation = []
            for span in (.24, .44, .84):
                j = bisect.bisect_left(times, tick + span - 1e-8)
                if j < len(samples):
                    continuation.append(observation(samples[j], samples[max(0, j - 1)]))
            band = "observed legal first-bend prefix" if row["reference"] <= exit_s + 4 else (
                "terminal risk" if row["reference"] >= 170 else "straight evidence outside fitted prefix")
            data.append({"source": id_, "request": d["Action"], "observation": row,
                         "continuation": continuation, "preview": preview(row["reference"]), "label": band})
    with (E / "demo-decisions.jsonl.gz").open("wb") as f:
        with gzip.GzipFile(filename="", fileobj=f, mode="wb", mtime=0) as z:
            for row in data:
                z.write((json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n").encode())
    return Counter(x["label"] for x in data)


def action_at(run, tick):
    r = run["Riders"][0]
    d = min(r["Decisions"], key=lambda q: abs(q["Choice"]["Time"] - tick))["Choice"]
    s = min(r["Samples"], key=lambda q: abs(time(q) - d["Time"]))
    return {"time": d["Time"], "action": d["Action"], "reference": ref(s),
            "offset": view(s)["Road"]["OffsetMeters"], "yaw": view(s)["AngularVelocity"],
            "beta": view(s)["SlideRadians"], "speedKph": view(s)["Speed"] * 3.6}


def intervention(base, perturbed):
    d0, d1 = (r["Riders"][0]["Decisions"] for r in (base, perturbed))
    first = next((a["Choice"]["Time"] for a, b in zip(d0, d1)
                  if a["Choice"]["Action"] != b["Choice"]["Action"]), None)
    if first is None:
        return None
    return {"Injection": first, "Activation": first + .24,
            "Comparison": [{"base": action_at(base, first + i * .2),
                            "perturbed": action_at(perturbed, first + i * .2)} for i in range(7)]}


def audit_queue(run):
    rider = run["Riders"][0]
    scheduled = [{"ObservedAt": d["Choice"]["Time"], "ActivationAt": d["Choice"]["Time"] + .24,
                  "Action": d["Choice"]["Action"]} for d in rider["Decisions"]]
    active = rider["Activations"]
    assert len(active) <= len(scheduled)
    assert active == scheduled[:len(active)]
    end = rider["Samples"][-1]["Input"]
    assert end["Pending"] == scheduled[len(active):]
    assert all(q["ActivationAt"] <= end["Time"] + 1e-8 for q in active)
    assert all(q["ActivationAt"] >= end["Time"] - 1e-8 for q in end["Pending"])
    return {"Issued": len(scheduled), "Activated": len(active), "Pending": len(end["Pending"])}

def stats(episodes, validation, pair, native_runs):
    def counts(rows):
        outcomes = [q["result"] for q in rows]
        completed = [o["SectionSeconds"] for o in outcomes if o["Legal"]]
        return {"Episodes": len(rows), "Legal": len(completed),
                "Screen": sum(o["TerminalScreen"] for o in outcomes),
                "MeanLegalSeconds": statistics.mean(completed) if completed else None,
                "BestLegalSeconds": min(completed) if completed else None,
                "Rejections": Counter(o["Reason"] for o in outcomes if not o["Legal"]),
                "CpuSeconds": sum(o["CpuSeconds"] for o in outcomes)}
    data = {f"{seed}/{phase}": counts([q for q in episodes if q["seed"] == seed and q["phase"] == phase])
            for seed in (166042, 166097) for phase in ("training", "extension")}
    baseline = {q["result"]["Scenario"]["Id"]: q["result"] for q in pair.get("Results", [])}
    gains = []
    for row in validation["Rows"]:
        r = row["result"]
        b = row["reference"] or baseline.get(r["Id"])
        if r["ReservePass"] and b and b["Legal"]:
            gains.append({"Id": r["Id"], "Seconds": r["SectionSeconds"], "ReferenceSeconds": b["SectionSeconds"],
                          "Gain": 1 - r["SectionSeconds"] / b["SectionSeconds"]})
    return {"Training": data, "Paired": gains,
            "MedianGain": statistics.median(x["Gain"] for x in gains) if gains else None,
            "MedianNewSeconds": statistics.median(q["result"]["SectionSeconds"] for q in validation["Rows"]
                                                if q["result"]["ReservePass"]),
            "Intervention": intervention(native_runs["H2"], native_runs["P1"])}


def plots(candidates, native_runs, validation, disturbance):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update({"font.family": "DejaVu Sans", "axes.spines.top": False,
                         "axes.spines.right": False, "axes.grid": True, "grid.color": "#dce5ee"})
    p = DOC / "plots"
    p.mkdir(exist_ok=True)
    colors = ("#087f77", "#d56931")
    fig, axes = plt.subplots(2, 1, figsize=(10, 5.6), sharex=True, layout="constrained")
    for seed, color in zip((166042, 166097), colors):
        c = [x for x in candidates if x["Seed"] == seed]
        x = [(q["Index"] + 1) * 4 for q in c]
        y = [q["Legal"] for q in c]
        axes[0].scatter(x, y, s=14, color=color, alpha=.38)
        axes[0].step(x, [max(y[:i + 1]) for i in range(len(y))], where="post", color=color, label=str(seed))
        valid = [q for q in c if q["Legal"] == 4]
        if valid:
            v = [q["Mean"] for q in valid]
            axes[1].scatter([(q["Index"] + 1) * 4 for q in valid], v, s=17, color=color, alpha=.58)
    for ax in axes:
        ax.axvline(100, color="#64748b", linestyle="--", linewidth=1)
    axes[0].set(ylabel="Legalne ukończenia / 4", yticks=range(5))
    axes[0].legend(title="Seed")
    axes[1].set(xlabel="Epizody w danym seedzie", ylabel="Śr. czas polityk 4/4 [s]")
    fig.suptitle("Uczenie na dwóch niezależnych populacjach")
    fig.savefig(p / "learning.png", dpi=160)
    plt.close(fig)

    track = read(HISTORICAL / "evidence/track.json")
    fig, (xy, speeds) = plt.subplots(1, 2, figsize=(12.5, 5), layout="constrained")
    for edge in ("Inner", "Outer"):
        xy.plot([q[edge]["X"] for q in track], [q[edge]["Y"] for q in track], color="#94a3b8", lw=.9)
    palette = plt.get_cmap("tab10")
    for i, row in enumerate(validation["Rows"]):
        r = row["result"]
        samples = native_runs[r["Id"]]["Riders"][0]["Samples"]
        color = palette(i)
        xy.plot([view(s)["Position"]["X"] for s in samples], [view(s)["Position"]["Y"] for s in samples],
                color=color, lw=1.5, label=r["Id"], alpha=.80)
        speeds.plot([ref(s) for s in samples], [view(s)["Speed"] * 3.6 for s in samples], color=color, lw=1.1)
    reference = validation["Rows"][1]["reference"]
    if reference:
        path = ROOT / validation["Rows"][1]["source"]
        old = json.loads(gzip.decompress(path.read_bytes()))["Riders"][0]["Samples"]
        old = [q for q in old if ref(q) <= 194.3894]
        speeds.plot([ref(s) for s in old], [view(s)["Speed"] * 3.6 for s in old],
                    color="#263246", linestyle="--", linewidth=1.4, label="#161 H2")
    for position in (35, 132.3894, 194.3894):
        speeds.axvline(position, color="#b0bbc8", linewidth=.75)
    speeds.legend()
    xy.legend(ncol=4, fontsize=8, loc="upper center")
    xy.set(title="Osiem odłożonych trajektorii", xlabel="X [m]", ylabel="Y [m]", aspect="equal")
    speeds.set(title="Profil prędkości i przekroje", xlabel="Referencja [m]", ylabel="km/h")
    fig.savefig(p / "xy-speed.png", dpi=160)
    plt.close(fig)

    def telemetry(s, key):
        v, i = view(s), s["Input"]
        f, power = s["Forces"], s["Powertrain"]
        if key == "gas": return i["Active"]["Drive"]
        if key == "gas request": return s["RequestedDrive"]
        if key == "steer": return math.degrees(i["Steering"])
        if key == "steer request": return math.degrees(s["RequestedSteering"])
        if key == "beta": return math.degrees(v["SlideRadians"])
        if key == "yaw": return v["AngularVelocity"]
        if key == "slip": return power["Wheel"]["InstantSlipRatio"]
        if key == "front FY": return f["FrontLateral"]
        if key == "front FX": return dot(f["FrontForce"], {"X": f["FrontLeft"]["Y"], "Y": -f["FrontLeft"]["X"]})
        if key == "rear FY": return f["RearLateral"]
        if key == "rear FX": return f["RearLongitudinal"]
        if key == "front use":
            return math.hypot(f["FrontForce"]["X"], f["FrontForce"]["Y"]) / f["TyreLoads"]["FrontCapacityNewtons"]
        if key == "rear use": return power["Wheel"]["RearUtilization"]
        raise KeyError(key)

    for title, name, signals in (
        ("Polecenia i ruch w H2 i H4", "controls.png", [
            ("Gaz", ["gas request", "gas"]), ("Skręt [°]", ["steer request", "steer"]),
            ("β [°]", ["beta"]), ("Yaw [rad/s]", ["yaw"]), ("Wheelspin", ["slip"])]),
        ("Siły i wykorzystanie kontaktów", "forces.png", [
            ("Przód [N]", ["front FY", "front FX"]), ("Tył [N]", ["rear FY", "rear FX"]),
            ("Wykorzystanie", ["front use", "rear use"])])
    ):
        fig, axes = plt.subplots(len(signals), 1, figsize=(10.5, len(signals) * 1.46 + 1),
                                sharex=True, layout="constrained")
        for ax, (units, keys) in zip(axes, signals):
            for id_, color in zip(("H2", "H4"), colors):
                ss = [s for s in native_runs[id_]["Riders"][0]["Samples"] if s["Forces"]]
                entry = next(q["result"]["Entry"]["Seconds"] for q in validation["Rows"] if q["result"]["Id"] == id_)
                for j, key in enumerate(keys):
                    ax.plot([time(s) - entry for s in ss], [telemetry(s, key) for s in ss], color=color,
                            linestyle=("-", "--")[j], lw=1.1, label=f"{id_} · {key}")
            ax.axvline(0, color="#94a3b8", linewidth=.7)
            ax.set_ylabel(units)
            ax.legend(loc="upper right", ncol=2, fontsize=7)
        axes[-1].set_xlabel("Czas od wejścia [s]")
        fig.suptitle(title)
        fig.savefig(p / name, dpi=160)
        plt.close(fig)

    if disturbance:
        t0 = disturbance["Injection"]
        fig, axes = plt.subplots(2, 1, figsize=(9, 4.6), sharex=True, layout="constrained")
        for id_, color in zip(("H2", "P1"), colors):
            ss = [s for s in native_runs[id_]["Riders"][0]["Samples"] if t0 - .3 <= time(s) <= t0 + 1.6]
            axes[0].plot([time(s) - t0 for s in ss], [telemetry(s, "steer request") for s in ss], color=color, label=id_)
            axes[1].plot([time(s) - t0 for s in ss], [telemetry(s, "yaw") for s in ss], color=color)
        for ax in axes:
            ax.axvline(.24, color="#64748b", linestyle="--", lw=1)
        axes[0].legend()
        axes[0].set_ylabel("Żądany skręt [°]")
        axes[1].set(ylabel="Yaw [rad/s]", xlabel="Czas od zażądania impulsu [s]")
        fig.suptitle("Odchylenie śródłukowe i następne decyzje")
        fig.savefig(p / "disturbance.png", dpi=160)
        plt.close(fig)


def main():
    validation = read(E / "validation.json")
    pair_path = E / "pairing.json"
    pair = read(pair_path) if pair_path.exists() else {"Results": []}
    episodes = lines(E / "training-episodes.jsonl")
    candidates = lines(E / "training-candidates.jsonl") + lines(E / "extension-candidates.jsonl")
    native_runs = {}
    for row in lines(E / "archives.jsonl"):
        path = E / row["Path"]
        native_runs[path.stem] = native(path, row["Sha256"])
    result = stats(episodes, validation, pair, native_runs)
    result["DemonstrationDecisions"] = audit_teachers(132.38937497138977)
    result["QueueAudit"] = {key: audit_queue(run) for key, run in native_runs.items()}
    (E / "analysis.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    plots(candidates, native_runs, validation, result["Intervention"])


if __name__ == "__main__":
    main()
