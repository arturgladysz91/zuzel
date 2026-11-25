
"""Render aligned Study #218 distance traces from the accepted-step archives."""
import argparse
import gzip
import json
from pathlib import Path
from math import degrees

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt

STATIONS = [-50, -40, -30, -20, -10, -5, 0, 5, 10, 15, 20]
COLORS = ["#17717e", "#b94b38", "#66559e", "#b9832b"]

KINEMATICS = [
    ("Speed · km/h", [("SpeedKph", None), ("SpeedTargetKph", "plan")]),
    ("Frame roll · °", [("Roll", "angle"), ("Roll", "plan_angle")]),
    ("Roll rate · °/s", [("RollRate", "angle")]),
    ("Roll acceleration · rad/s²", [("RollAcceleration", None)]),
    ("External roll moment · N m", [("RollMoment", None), ("FrontRollMoment", "secondary")]),
    ("Yaw relative to −50 m · °", [("Yaw", "relative_angle")]),
    ("Yaw rate · °/s", [("YawRate", "angle")]),
    ("Vehicle / rear tyre slip angle · °", [("Sideslip", "angle"), ("RearTyreSlipAngle", "secondary_angle")]),
    ("Actual / planned curvature · m⁻¹", [("VelocityCurvature", None), ("Curvature", "plan")]),
    ("Lateral / longitudinal acceleration · m/s²", [("LateralAcceleration", None), ("TangentialAcceleration", "secondary")]),
]

LOADS = [
    ("Steer angle · °", [("Steer", "angle"), ("SteeringRadians", "command_angle")]),
    ("Steer rate · °/s", [("SteerRate", "angle")]),
    ("Front / rear normal load · N", [("FrontNormal", None), ("RearNormal", "secondary")]),
    ("Front longitudinal / lateral force · N", [("FrontFx", None), ("FrontFy", "secondary")]),
    ("Rear longitudinal / lateral force · N", [("RearFx", None), ("RearFy", "secondary")]),
    ("Available front roll moment · N m", [("FrontRollBand", None)]),
    ("Drive · fraction", [("ActiveDrive", None), ("Drive", "command")]),
    ("Engine speed · RPM", [("Rpm", None)]),
    ("Net engine torque · N m", [("Torque", None)]),
    ("Normalized rear wheelspin", [("RearWheelspin", None)]),
]

RIDER_AND_CLEARANCE = [
    ("Rider fore–aft displacement · m", [("Rider.X", None), ("Target.X", "command")]),
    ("Rider lateral displacement · m", [("Rider.Y", None), ("Target.Y", "command")]),
    ("Rider vertical displacement · m", [("Rider.Z", None), ("Target.Z", "command")]),
    ("Active rider force · N", [("MuscleForce", None)]),
    ("Active rider power · W", [("MusclePower", None)]),
    ("Left foot normal load · N", [("FootNormal", None)]),
    ("Swept chassis ground clearance · m", [("SweptGround", None)]),
    ("Swept track clearance · m", [("SweptBoundary", None)]),
    ("Rider lateral track offset · m", [("Offset", None)]),
    ("Pending command count", [("Pending", "pending")]),
]


def read(path):
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as stream:
            return json.load(stream)
    return json.loads(path.read_text(encoding="utf-8"))


def field(row, key):
    for token in key.split("."):
        row = row[token]
    return row


def series(data, key, mode):
    if mode and mode.startswith("plan"):
        rows = [r for r in data["Requests"] if r["Target"] is not None]
        return [r["Distance"] for r in rows], [
            degrees(r["Target"][key]) if mode.endswith("angle") else r["Target"][key] for r in rows]
    if mode and mode.startswith("command"):
        rows = data["Requests"]
        location = "Bike." + key if key in ("SteeringRadians", "Drive") else key
        return [r["Distance"] for r in rows], [
            degrees(field(r["Command"], location)) if mode.endswith("angle") else field(r["Command"], location)
            for r in rows]
    rows = data["Steps"]
    if mode == "pending":
        values = [r["LatestRequest"] + 1 - r["NextActivation"] for r in rows]
    else:
        values = [field(r, key) for r in rows]
        if mode and mode.endswith("angle"):
            offset = field(next(r for r in rows if r["Distance"] >= -50), key) if mode == "relative_angle" else 0
            values = [degrees(x - offset) for x in values]
    return [r["Distance"] for r in rows], values


def draw(assets, output, name, heading, panels, window=(-51, 20)):
    figure, axes = plt.subplots(5, 2, figsize=(12, 11), sharex=True)
    figure.subplots_adjust(left=.07, right=.98, bottom=.065, top=.90, wspace=.16, hspace=.29)
    for axis, (title, channels) in zip(axes.flat, panels):
        title = title.replace("\u207b\u00b9", "$^{-1}$")
        axis.set_title(title, loc="left", fontsize=10)
        for s in STATIONS:
            axis.axvline(s, color="#899a9f" if s == 0 else "#dce4e6", lw=1.1 if s == 0 else .6, zorder=0)
        axis.grid(axis="y", color="#edf0f1", lw=.7, zorder=0)
        if title.startswith("Swept track clearance"):
            axis.axhline(.5, color="#70777a", lw=.7, ls=":")
        for color, (label, data) in zip(COLORS, assets):
            for key, mode in channels:
                x, y = series(data, key, mode)
                selected = [(u, v) for u, v in zip(x, y) if window[0] - .3 <= u <= window[1] + .3]
                if not selected:
                    continue
                px, py = zip(*selected)
                requested = mode and mode.startswith(("plan", "command"))
                axis.plot(px, py, color=color, lw=1.15, ls="--" if mode and mode.startswith(
                    ("plan", "command", "secondary")) else "-",
                    drawstyle="steps-post" if requested else "default", zorder=2)
        axis.set_xlim(*window)
        axis.tick_params(axis="both", labelsize=8, length=2)
        axis.spines[["right", "top"]].set_visible(False)
    axes[-1, 0].set_xlabel("Distance from corner entry · m")
    axes[-1, 1].set_xlabel("Distance from corner entry · m")
    handles = [plt.Line2D([0], [0], color=color, lw=2, label=label) for color, (label, _) in zip(COLORS, assets)]
    handles.append(plt.Line2D([0], [0], color="#626b70", lw=1.2, ls="--", label="Requested or second series"))
    figure.legend(handles=handles, loc="upper center", bbox_to_anchor=(.5, .953),
                  ncol=len(handles), fontsize=8, frameon=False)
    figure.suptitle(heading, y=.983, fontweight="bold", fontsize=16)
    figure.savefig(output / name, dpi=147, facecolor="white")
    plt.close(figure)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--compare", type=Path, help="Optional accepted H37 candidate archive.")
    parser.add_argument("--near-90", type=Path, help="Optional H37 preparation witness in the 90 km/h band.")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    assets = [("77.50 km/h · completed", read(args.source / "archive-77.json.gz")),
              ("91.16 km/h · stopped", read(args.source / "archive-91.json.gz"))]
    draw(assets, args.output, "approach-kinematics.png", "Study #218 · Approach and chassis motion", KINEMATICS)
    draw(assets, args.output, "approach-loads.png", "Study #218 · Steering, tyre loads and power", LOADS)
    draw(assets, args.output, "approach-rider.png", "Study #218 · Rider movement and clearance", RIDER_AND_CLEARANCE)
    if args.compare:
        comparison = read(args.compare)
        speed = comparison["Summary"]["Entry"]["Kph"]
        draw([assets[0], (f"{speed:.2f} km/h · H37", comparison)], args.output, "complete-maneuver.png",
             "Study #218 · Complete corner and exit", KINEMATICS, (-30, 105))
    if args.near_90:
        comparison = read(args.near_90)
        speed = comparison["Summary"]["Entry"]["Kph"]
        draw([assets[1], (f"{speed:.2f} km/h · H37", comparison)], args.output, "preparation-90.png",
             "Study #218 · Earlier preparation near 90 km/h", KINEMATICS)


if __name__ == "__main__":
    main()
