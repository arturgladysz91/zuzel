
"""Plot H38's constitutive sweeps and driven-corner traces from the published evidence."""
from __future__ import annotations

import argparse
import gzip
import json
from collections import defaultdict
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt

COLORS = ["#116879", "#bb5435", "#5a55a2", "#257747", "#ac7d24"]


def read(root, file):
    path = root / file
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as stream:
            return json.load(stream)
    return json.loads(path.read_text(encoding="utf-8"))


def render_maps(root):
    archive = read(root, "force-maps.json.gz")
    groups = defaultdict(list)
    for r in archive["Cells"]:
        if (r["SpeedKph"] == 80 and r["RearNormal"] == 900 and r["SteeringDegrees"] == 0 and
                r["RollDegrees"] == -28 and r["Input"] == "imposed axle torque" and
                r["RequestedInput"] == 0 and r["LateralRepresentation"] == "active H38"):
            groups[r["RearSlipAngleDegrees"]].append(r)
    fig, axes = plt.subplots(1, 3, figsize=(12, 4.5))
    fig.subplots_adjust(left=.075, right=.985, bottom=.17, top=.80, wspace=.36)
    fig.suptitle("Study #219 · Stock rear-tyre force map", fontsize=16, fontweight="bold", y=.97)
    for i, ((angle, samples), color) in enumerate(zip(sorted(groups.items()), COLORS)):
        rows = sorted(samples, key=lambda r: r["RequestedSlip"])
        x = [r["RequestedSlip"] for r in rows]
        for ax, key, title in zip(axes, ["RearFx", "RearFy", "RearYawMoment"],
                                  ["Longitudinal force · N", "Lateral force · N", "Rear yaw moment · N m"]):
            ax.plot(x, [r[key] for r in rows], color=color, marker="o", markersize=2.6,
                    label=f"{angle:g}° rear slip angle" if i >= 0 else "")
            ax.axhline(0, color="#d5dfe1", linewidth=.7, zorder=0)
            ax.set_title(title, loc="left", fontsize=10)
            ax.set_xlabel("Requested initial rear slip ratio")
            ax.grid(alpha=.20)
            ax.spines[["right", "top"]].set_visible(False)
    handles, labels = axes[0].get_legend_handles_labels()
    fig.legend(handles, labels, loc="upper center", bbox_to_anchor=(.5, .90), ncol=5, frameon=False, fontsize=8)
    fig.text(.075, .035, "80 km/h · 900 N rear load · −28° frame roll · zero imposed axle torque",
             color="#44525a", fontsize=9)
    fig.savefig(root / "rear-force-map.png", dpi=154, facecolor="white")
    plt.close(fig)


def render_runs(root):
    plots = [("Independent controller", "whole-training-replay.json.gz", COLORS[0]),
             ("H37 powered comparison", "reference-powered.json.gz", COLORS[1]),
             ("H38 checkpoint recourse", "recourse-nominal.json.gz", COLORS[2])]
    fig, axes = plt.subplots(3, 2, figsize=(10.7, 7.4), sharex=True)
    fig.subplots_adjust(left=.083, right=.98, bottom=.09, top=.87, hspace=.36, wspace=.27)
    fig.suptitle("Study #219 · Corner response by distance", fontsize=16, fontweight="bold", y=.98)
    fields = [("ActiveDrive", "Active Drive · fraction"), ("Wheelspin", "Normalized rear wheelspin"),
              ("InwardAcceleration", "Lateral acceleration · m/s²"), ("VehicleSlip", "Vehicle sideslip · °"),
              ("FrontNormal", "Front normal load · N"), ("SweptGround", "Swept hard-part clearance · m")]
    for title, file, color in plots:
        rows = read(root, file)["States"]
        for ax, (key, caption) in zip(axes.flat, fields):
            x = [r["Distance"] for r in rows]
            y = [r[key] * (180 / 3.141592653589793 if key == "VehicleSlip" else 1) for r in rows]
            ax.plot(x, y, color=color, lw=1.05, label=title)
            ax.axvline(0, color="#66777f", lw=.65, zorder=0)
            ax.axvline(97.389, color="#66777f", lw=.65, zorder=0)
            ax.set_title(caption, loc="left", fontsize=10)
            ax.grid(alpha=.20)
            ax.spines[["right", "top"]].set_visible(False)
    for ax in axes[-1]:
        ax.set_xlabel("Reference distance from entry · m")
    handles, labels = axes[0, 0].get_legend_handles_labels()
    fig.legend(handles, labels, loc="upper center", bbox_to_anchor=(.5, .932), ncol=3, frameon=False)
    fig.savefig(root / "corner-comparison.png", dpi=154, facecolor="white")
    plt.close(fig)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    args = parser.parse_args()
    render_maps(args.root)
    render_runs(args.root)


if __name__ == "__main__":
    main()
