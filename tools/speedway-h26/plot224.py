"""Summarize measured routes around the failed and recovered turn entries."""

import argparse
import math
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt

from verify222 import read


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    root = parser.parse_args().root
    results = read(root / "route-profiles.json.gz")
    outcome = read(root / "recourse-search.json.gz")
    history = read(root / "reference-phases.json")["Motions"][0]["Phases"]
    seam = 318.77874994277954
    winner = "line-selected" if outcome["KeptInitialLine"] else "recourse-selected"
    old = []
    for point in history:
        if seam - 52 <= point["Station"] <= seam + 40:
            old.append({"Station": point["Station"], "Offset": point["Offset"],
                        "Speed": 3.6 * math.hypot(point["RoadSpeed"], point["SideSpeed"]),
                        "HeadingError": point["Direction"], "Roll": point["Roll"]})
    fig, axes = plt.subplots(2, 2, figsize=(10.7, 6.6), sharex=True, layout="constrained")
    routes = [("#222 return", old, "#8e365a", "--"),
              ("#224 selected return", results[winner], "#137a73", "-")]
    if "four-attempt" in results and any(p["Station"] >= 2 * seam for p in results["four-attempt"]):
        fourth = [{**p, "Station": p["Station"] - 2 * seam} for p in results["four-attempt"]
                  if 3 * seam - 52 <= p["Station"] <= 3 * seam + 40]
        if fourth:
            routes.append(("#224 approach to lap four", fourth, "#c66b1b", "-."))
    for label, data, colour, style in routes:
        rows = [r for r in data if -52 <= r["Station"] - seam <= 40]
        x = [r["Station"] - seam for r in rows]
        for ax, field in zip(axes.flat, ("Offset", "Speed", "HeadingError", "Roll")):
            ax.plot(x, [r[field] for r in rows], label=label, color=colour, linestyle=style, linewidth=1.6)
    for ax, label in zip(axes.flat, ("Offset from inside edge (m)", "Speed (km/h)",
                                    "Velocity heading relative to road (rad)", "Roll (rad)")):
        ax.set_ylabel(label)
        ax.axvline(0, color="#54657b", linestyle=":", linewidth=.8)
        ax.grid(True, color="#e0e7ec", linewidth=.62)
    axes[0, 0].legend(loc="best", fontsize=8)
    for ax in axes[1]:
        ax.set_xlabel("Course metres from flying entry")
    fig.suptitle("H43-P · Repeated bend approach and recovery", fontsize=15, fontweight="bold")
    fig.savefig(root / "periodic-entry.png", dpi=165)


if __name__ == "__main__":
    main()
