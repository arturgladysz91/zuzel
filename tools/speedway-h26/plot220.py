"""Plot the accepted H39 MODEL telemetry by synchronized track station."""

import argparse
import gzip
import json
import math
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt


STYLES = {
    "archive-57": ("H38 proposal 57", "#343b4b"),
    "archive-59": ("H38 proposal 59", "#ad6d42"),
    "archive-60": ("H38 proposal 60", "#7562a5"),
    "full-slide-frontier": ("H39 full-slide frontier", "#007970"),
    "settle/selected": ("H39 front-support continuation", "#971e62"),
    "original-straight": ("H38 ordinary straight", "#343b4b"),
    "straight-selected": ("H39 straight selection", "#1c58a9"),
    "straight-full-diagnostic": ("H39 full-Drive diagnostic", "#971e62"),
}


def plot(root, title, names, panels, limits, output, origin=0):
    fig, axes = plt.subplots(len(panels) // 2, 2, figsize=(13, 2.25 * len(panels) / 2),
                             sharex=True, layout="constrained")
    fig.suptitle(title, fontsize=16, fontweight="bold")
    for name in names:
        if not (root / f"{name}.json.gz").is_file():
            continue
        label, color = STYLES[name]
        with gzip.open(root / f"{name}.json.gz", "rt", encoding="utf-8-sig") as stream:
            rows = [r for r in json.load(stream)["Rows"]
                    if limits[0] - 1 <= r["ReferenceMeters"] - origin <= limits[1] + 1]
        x = [r["ReferenceMeters"] - origin for r in rows]
        for ax, (_, field, scale, _, comparison) in zip(axes.flat, panels, strict=True):
            ax.plot(x, [read(r, field) * scale for r in rows], color=color, linewidth=1.5, label=label)
            if comparison and name == "archive-57":
                ax.plot(x, [read(r, comparison) * scale for r in rows], color=color, linewidth=.9,
                        linestyle="--", label=f"57 · {comparison}")
    for ax, (label, _, _, threshold, _) in zip(axes.flat, panels, strict=True):
        ax.set(ylabel=label, xlim=limits)
        ax.grid(alpha=.22)
        if threshold is not None:
            ax.axhline(threshold, color="#918179", linestyle=":", linewidth=.85)
    axes.flat[0].legend(fontsize=8, ncol=2)
    if panels[2][-1]:
        axes.flat[2].legend(fontsize=7, ncol=2)
    for ax in axes[-1]:
        ax.set_xlabel("Reference metres from bend exit" if origin else "Reference metres from bend entry")
    fig.savefig(root / output, dpi=155, facecolor="white")
    plt.close(fig)


def read(row, field):
    if field == "Steer":
        return row["State"]["Steer"]
    if field == "SpeedKph":
        momentum = row["State"]["Momentum"]
        return math.hypot(momentum["X"], momentum["Y"]) * 3.6 / 147
    return row[field]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("--exit", type=float, required=True)
    args = parser.parse_args()
    plot(args.evidence, "H39-X | Loaded slide and exit recovery",
         ["archive-57", "archive-59", "archive-60", "full-slide-frontier", "settle/selected"], [
            ("Drive (%)", "ActiveDrive", 100, None, "RequestedDrive"),
            ("Engine speed (RPM)", "Rpm", 1, None, None),
            ("Rear wheelspin ratio", "ContactSlip", 1, .06, "ConstructorRearSlip"),
            ("Rear patch forward speed (m/s)", "RearLongitudinalVelocity", 1, None, None),
            ("Yaw rate (rad/s)", "YawRate", 1, 0, None),
            ("Roll rate (rad/s)", "RollRate", 1, 0, None),
            ("Front normal load (N)", "FrontNormal", 1, 0, None),
            ("Rear normal load (N)", "RearNormal", 1, 0, None),
            ("Steering angle (rad)", "Steer", 1, 0, None),
            ("Vehicle sideslip (rad)", "VehicleSlip", 1, .10472, None),
            ("Swept boundary reserve (m)", "SweptBoundary", 1, .50, None),
            ("Swept hard-part margin (m)", "SweptGround", 1, 0, None),
         ], (72, args.exit + 13), "exit-telemetry.png")
    plot(args.evidence, "H39-X | Ordinary straight and next-bend preparation",
         ["original-straight", "straight-selected", "straight-full-diagnostic"], [
            ("Speed (km/h)", "SpeedKph", 1, None, None),
            ("Drive (%)", "ActiveDrive", 100, None, None),
            ("Engine power (kW)", "EnginePower", .001, None, None),
            ("Contact and drag loss (kW)", "ContactAndDragLoss", .001, None, None),
            ("Front normal load (N)", "FrontNormal", 1, 0, None),
            ("Rear wheelspin ratio", "ContactSlip", 1, .06, None),
            ("Roll (rad)", "Roll", 1, 0, None),
            ("Swept boundary reserve (m)", "SweptBoundary", 1, .50, None),
         ], (0, 68), "ordinary-straight.png", args.exit)


if __name__ == "__main__":
    main()
