"""Plot synchronized physical telemetry from the H40-L evidence package."""

import argparse
import gzip
import json
import math
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt


def plot(root, output, title, traces, fields, stations, limits):
    fig, axes = plt.subplots(len(fields) // 2, 2, figsize=(12, 2.35 * (len(fields) // 2)),
                             sharex=True, layout="constrained")
    fig.suptitle(title, fontsize=16, fontweight="bold")
    for key, label, color in traces:
        p = root / (key + ".json.gz")
        if not p.is_file():
            continue
        with gzip.open(p, "rt", encoding="utf-8-sig") as file:
            rows = [r for r in json.load(file)["Rows"] if limits[0] - 1 <= r["ReferenceMeters"] <= limits[1] + 1]
        for ax, (caption, value, gate) in zip(axes.flat, fields, strict=True):
            ax.plot([r["ReferenceMeters"] for r in rows], [value(r) for r in rows], linewidth=1.5,
                    label=label, color=color)
            ax.set_ylabel(caption)
    for ax, (_, _, gate) in zip(axes.flat, fields, strict=True):
        for station in stations:
            if limits[0] < station < limits[1]:
                ax.axvline(station, color="#8f8b83", linewidth=.8, linestyle=":")
        if gate is not None:
            ax.axhline(gate, color="#9e6960", linewidth=.85, linestyle="--")
        ax.set_xlim(*limits)
        ax.grid(alpha=.19)
    axes.flat[0].legend(loc="best", fontsize=8, ncol=2)
    for ax in axes[-1]:
        ax.set_xlabel("Course reference metres from first bend entry")
    fig.savefig(root / output, dpi=155, facecolor="white")
    plt.close(fig)


def speed(r):
    p = r["State"]["Momentum"]
    return math.hypot(p["X"], p["Y"]) * 3.6 / 147


def field(name, scale=1):
    return lambda row: row[name] * scale


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    root = parser.parse_args().root
    q = json.loads((root / "stationing.json").read_text())
    stations = [q[k] for k in ("FirstExit", "SecondEntry", "SecondExit")]
    plot(root, "linked-telemetry.png", "H40-L | Two bends and the connecting straight",
         [("links/direct-12", "#220 fastest-suffix continuation", "#9b3967"),
          ("links/linked-training", "Initial linked selection", "#345269"),
          ("refined/linked-training", "Fastest refined linked trial", "#0b756a"),
          ("refined/linked-speed-frontier", "Highest valid straight peak", "#c17a46"),
          ("transfer/transfer-nominal", "Adaptive transfer nominal", "#6057a5")],
         [("Speed (km/h)", speed, 100), ("Active Drive (%)", field("ActiveDrive", 100), None),
          ("Roll (rad)", field("Roll"), None), ("Yaw rate (rad/s)", field("YawRate"), None),
          ("Rear longitudinal slip ratio", field("ContactSlip"), .06),
          ("Front normal load (N)", field("FrontNormal"), 0),
          ("Swept boundary reserve (m)", field("SweptBoundary"), .50),
          ("Swept hard-part margin (m)", field("SweptGround"), 0)], stations, (0, stations[-1] + 24))

    plot(root, "front-mechanics.png", "H40-L | Front-wheel rotor and lateral relaxation",
         [("mechanics/h39-high-prep-0", "Stock", "#354453"),
          ("mechanics/h39-high-prep-2", "Rotor 0.45 kg·m²", "#0b756a"),
          ("mechanics/h39-high-prep-4", "Relaxation 0.35 m", "#a6516d"),
          ("mechanics/h39-high-prep-5", "Relaxation 0.75 m", "#c17a46")],
         [("Roll (rad)", field("Roll"), 0), ("Roll rate (rad/s)", field("RollRate"), 0),
          ("Yaw rate (rad/s)", field("YawRate"), 0), ("Front normal load (N)", field("FrontNormal"), 0),
          ("Front utilization", field("FrontUse"), 1),
          ("Front axial momentum (kg·m²/s)", lambda r: r["State"].get("FrontWheel", 0), 0)],
         stations, (stations[1] - 14, stations[1] + 9))


if __name__ == "__main__":
    main()
