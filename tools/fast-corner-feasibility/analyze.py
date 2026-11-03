"""Read frozen #165 evidence and render plots. No physics runs."""
import csv
import gzip
import hashlib
import importlib.util
import json
import math
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / "docs/experiments/fast-corner-feasibility"
EVIDENCE = DOC / "evidence"
PLOTS = DOC / "plots"
REFERENCE = ROOT / "docs/experiments/trajectory-continuation/native/native-dev.json.gz"

module_spec = importlib.util.spec_from_file_location(
    "energy145", ROOT / "tools/corner-speed-loss/analyze145.py"
)
energy = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(energy)


def load(path):
    return json.loads(path.read_bytes())


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def archive(id_, expected_hash):
    path = EVIDENCE / "native" / f"{id_}.zip"
    assert digest(path.read_bytes()) == expected_hash
    with ZipFile(path) as bundle:
        assert bundle.testzip() is None
        index = json.loads(bundle.read("index.json"))
        assert index["Format"] == "S3Evidence162-v1"
        policies = bundle.read("policies.jsonl").splitlines()
        assert len(policies) == len(index["Policies"])
        for line, expected in zip(policies, index["Policies"]):
            payload = line.split(b',"payload":', 1)[1][:-1]
            assert json.loads(line)["key"] == expected["Key"]
            assert len(payload) == expected["Bytes"] and digest(payload) == expected["SHA256"]
        return json.loads(bundle.read("native.json")), json.loads(bundle.read("result.json"))


def wrap(angle):
    return (angle + math.pi) % (2 * math.pi) - math.pi


def components(samples):
    for a, b in zip(samples, samples[1:]):
        q = energy.interval(a, b)
        v = energy.rider(b)
        va = energy.rider(a)
        f = b["Forces"]
        q["beta_dot_deg_s"] = math.degrees(wrap(v["SlideRadians"] - va["SlideRadians"])) / q["dt_s"]
        q["chi_deg"] = math.degrees(math.atan2(v["Velocity"]["Y"], v["Velocity"]["X"]))
        q["theta_deg"] = math.degrees(v["Heading"])
        q["road_outward_m_s"] = energy.dot(v["Velocity"], v["Road"]["Outward"])
        q["body_side_m_s"] = v["SideSpeed"]
        q["active_requested_steering_deg"] = math.degrees(b["Input"]["Active"]["SteeringRadians"])
        q["front_fx_contact_N"] = energy.dot(
            f["FrontForce"], {"X": f["FrontLeft"]["Y"], "Y": -f["FrontLeft"]["X"]}
        )
        q["front_fy_contact_N"] = f["FrontLateral"]
        q["xy_distance_m"] = v["DistanceMeters"]
        q["swept_m"] = b["Certificate"]
        yield q


def trimmed(samples, end):
    return samples[:next((index + 1 for index, sample in enumerate(samples)
                          if energy.ref(sample) >= end), len(samples))]


def write_rows(path, rows):
    with gzip.GzipFile(filename="", fileobj=path.open("wb"), mode="wb", mtime=0) as out:
        import io
        with io.TextIOWrapper(out, encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)


def metrics(item, run, section, rows):
    trial = item["Trial"]
    measures = trial["Metrics"]
    samples = run["Riders"][0]["Samples"]
    end = measures["Goal"]["Time"] if measures["Goal"] else energy.time(samples[-1])
    start = energy.point(samples, section["Entry"] - 15)
    entry = measures["Entry"]
    exit_ = measures["Exit"]
    result = {
        "Id": trial["Id"], "EntryKph": entry["Kph"] if entry else None,
        "MaxObservedKph": max(q["speed_kph"] for q in rows),
        "Last": rows[-1], "StartOfMeasuredSection": start,
        "MaxAbsBetaFirstBend": measures["BendMaxAbsBeta"],
        "SignedBetaFirstBend": [
            min(q["beta_deg"] for q in rows if entry["Time"] <= q["time_s"] <= exit_["Time"]),
            max(q["beta_deg"] for q in rows if entry["Time"] <= q["time_s"] <= exit_["Time"])
        ] if entry and exit_ else None,
    }
    ranges = {
        "from_rest": (0, end),
        "manoeuvre": (start["time_s"], end) if start else None,
        "bend": (entry["Time"], exit_["Time"]) if entry and exit_ else None,
        "next_entry": (measures["After62"]["Time"], end) if measures["After62"] else None,
    }
    for name, limits in ranges.items():
        result[name] = energy.phase(rows, *limits) if limits else None
    return result


def plot(sets, track):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update({"font.family": "DejaVu Sans", "axes.spines.top": False,
                         "axes.spines.right": False, "axes.labelcolor": "#24354a",
                         "text.color": "#24354a", "grid.color": "#dce5ee",
                         "axes.grid": True, "figure.facecolor": "white"})
    PLOTS.mkdir(exist_ok=True)
    fig, (xy, profile) = plt.subplots(1, 2, figsize=(13, 5.4), layout="constrained")
    for edge in ("Inner", "Outer"):
        xy.plot([p[edge]["X"] for p in track], [p[edge]["Y"] for p in track],
                color="#a7b4c3", linewidth=1, zorder=1)
    for label, color, measures, rows, _ in sets:
        xy.plot([r["x_m"] for r in rows], [r["y_m"] for r in rows], label=label,
                color=color, linewidth=1.65, zorder=3)
        for name, marker in (("Entry", ">"), ("Apex", "D"), ("Exit", "s"), ("Goal", "o")):
            q = measures[name]
            if q:
                xy.scatter(q["X"], q["Y"], marker=marker, s=34, color=color,
                           edgecolors="white", linewidths=.5, zorder=4)
        xy.scatter(rows[0]["x_m"], rows[0]["y_m"], marker="|", s=62, color=color, zorder=5)
        if measures["Goal"] is None:
            xy.scatter(rows[-1]["x_m"], rows[-1]["y_m"], marker="x", s=44, color=color, zorder=5)
        profile.plot([r["xy_distance_m"] for r in rows], [r["speed_kph"] for r in rows],
                     color=color, linewidth=1.5)
        for name, marker in (("Entry", ">"), ("Apex", "D"), ("Exit", "s"), ("Goal", "o")):
            q = measures[name]
            if q:
                profile.scatter(q["Distance"], q["Kph"], marker=marker, s=34, color=color,
                                edgecolors="white", linewidths=.5, zorder=4)
    xy.legend(loc="upper center", fontsize=8.5)
    xy.set(title="Ślad i rzeczywiste granice progressive-b", xlabel="X [m]", ylabel="Y [m]", aspect="equal")
    profile.set(title="Prędkość po rzeczywistej drodze XY", xlabel="Droga od własnego startu [m]",
                ylabel="Prędkość [km/h]")
    fig.supxlabel("Znaczniki   | start   ▶ wejście   ◆ apex   ■ wyjście   ● koniec   × odrzucenie", fontsize=9)
    fig.savefig(PLOTS / "xy-speed.png", dpi=170)
    plt.close(fig)

    for title, file, signals in [
        ("Sterowanie i stan motocykla", "controls.png", [
            ("Gaz", ["requested_gas", "active_gas"], ["żądany", "aktywny"]),
            ("Skręt [°]", ["requested_steering_deg", "steering_deg"], ["żądany", "wykonany"]),
            ("β [°]", ["beta_deg"], ["β"]), ("Yaw [rad/s]", ["yaw_rads"], ["yaw"]),
            ("Poślizg wzdłużny", ["wheel_slip"], ["koło tylne"]),
            ("Prędkość boczna [m/s]", ["road_outward_m_s"], ["względem toru"]),
        ]),
        ("Siły w osiach kontaktów i wykorzystanie", "tyres.png", [
            ("Przód [N]", ["front_fx_contact_N", "front_fy_contact_N"], ["Fx", "Fy"]),
            ("Tył [N]", ["rear_fx_N", "rear_fy_N"], ["Fx", "Fy"]),
            ("Wykorzystanie", ["front_util", "rear_util"], ["przód", "tył"]),
        ]),
    ]:
        fig, axes = plt.subplots(len(signals), 1, figsize=(11, 2 + 1.38 * len(signals)),
                                sharex=True, layout="constrained")
        for ax, (units, keys, names) in zip(axes, signals):
            for label, color, measures, rows, _ in sets:
                start = measures["Entry"]["Time"]
                times = [r["time_s"] - start for r in rows]
                for j, (key, name) in enumerate(zip(keys, names)):
                    ax.plot(times, [r[key] for r in rows], color=color, linewidth=1.15,
                            linestyle=("-", "--")[j], label=f"{label} · {name}")
            ax.axvline(0, color="#94a3b8", linewidth=.9)
            ax.set_ylabel(units)
        axes[0].legend(loc="upper center", bbox_to_anchor=(.5, 1.47), ncol=3, fontsize=7.2)
        axes[-1].set_xlabel("Czas względem wejścia w pierwszy łuk [s]")
        fig.suptitle(title, fontsize=15)
        fig.savefig(PLOTS / file, dpi=170)
        plt.close(fig)

    balance = [
        ("Spalanie", "combustion_J"), ("Δ translacja", "translation_change_J"),
        ("Δ yaw", "yaw_change_J"), ("Δ koło", "wheel_change_J"), ("Δ wał", "crank_change_J"),
        ("Poślizg przód", "front_slip_loss_J"), ("Poślizg boczny tył", "rear_lateral_slip_loss_J"),
        ("Wheelspin", "rear_long_slip_loss_J"), ("Opory ruchu", "resistance_J"),
        ("Sprzęgło", "clutch_heat_J"), ("Przekładnia", "transmission_loss_J"), ("Łożysko", "bearing_loss_J"),
    ]
    fig, ax = plt.subplots(figsize=(11, 6), layout="constrained")
    comparable = [q for q in sets if q[2]["Goal"] and q[4]["manoeuvre"]]
    for j, (label, color, _, _, info) in enumerate(comparable):
        totals = info["manoeuvre"]["energy"]
        xs = [totals[k] / (-1000 if k == "resistance_J" else 1000) for _, k in balance]
        ax.barh([i + .23 * j for i in range(len(balance))], xs, height=.21, color=color, label=label)
    ax.set_yticks(range(len(balance)), [name for name, _ in balance])
    ax.invert_yaxis()
    ax.set(title="Energia od 15 m przed łukiem do końca odcinka", xlabel="Energia [kJ]")
    ax.legend()
    fig.savefig(PLOTS / "energy.png", dpi=170)
    plt.close(fig)


def main():
    reference = load(EVIDENCE / "reference.json")
    verification = load(EVIDENCE / "verification.json")
    section = reference["section"]
    ref_run = json.loads(gzip.decompress(REFERENCE.read_bytes()))
    assert digest(REFERENCE.read_bytes()) == reference["Sha256"]
    ref_run["Riders"][0]["Samples"] = trimmed(ref_run["Riders"][0]["Samples"], section["Goal"])
    ref_rows = list(components(ref_run["Riders"][0]["Samples"]))
    ref_info = metrics({"Trial": {"Id": "161", "Metrics": reference["Metrics"]}}, ref_run, section, ref_rows)
    sets = [("#161", "#475569", reference["Metrics"], ref_rows, ref_info)]
    info = []
    full = []
    for row in verification["Trials"]:
        result = row["result"]
        trial = result["Trial"]
        id_ = trial["Id"]
        run, archived = archive(id_, row["ArchiveSha256"])
        assert result == archived
        native_rows = list(components(run["Riders"][0]["Samples"]))
        details = metrics(result, run, section, native_rows)
        info.append(details)
        full.append((result, trial, native_rows, details))
        write_rows(EVIDENCE / f"{id_}-intervals.csv.gz", native_rows)
    legal = [q for q in full if q[0]["ConfirmedLegal"]]
    fastest = min(legal, key=lambda q: q[1]["Metrics"]["SectionSeconds"], default=None)
    high_legal = max(legal, key=lambda q: q[1]["Metrics"]["Entry"]["Kph"], default=None)
    rejected = max((q for q in full if not q[0]["ConfirmedLegal"] and q[1]["Metrics"]["Entry"]),
                   key=lambda q: q[1]["Metrics"]["Entry"]["Kph"], default=None)
    seen = set()
    for item, color in ((high_legal, "#007e78"), (fastest, "#2865bd"), (rejected, "#ca532c")):
        if item and item[1]["Id"] not in seen:
            trial = item[1]
            seen.add(trial["Id"])
            sets.append((trial["Id"], color, trial["Metrics"], item[2], item[3]))
    plot(sets, load(EVIDENCE / "track.json"))
    (EVIDENCE / "analysis.json").write_text(json.dumps({"Reference": ref_info, "Native": info},
        ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
