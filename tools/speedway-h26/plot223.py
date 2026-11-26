"""Plot the first-turn and periodic-entry states from exact accepted steps."""

import argparse
import math
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt

from verify222 import read


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("directory", type=Path)
    root = cli.parse_args().directory
    summary = read(root / "periodic-summary.json")
    lap = summary["Gates"][1]["Station"]
    previous = root.parent / "speedway222" / "cycle" / "cycle-flying.json.gz"
    old = read(previous)["Rows"]
    new = read(root / "periodic-selected.json.gz")["Rows"]
    fig, axes = plt.subplots(2, 2, figsize=(10.5, 6.2), sharex=True, layout="constrained")
    series = [
        ("First flying entry", old, 0, "#147c78", "-"),
        ("#222 next entry", old, lap, "#9d345b", "--"),
        ("#223 direct continuation", new, lap, "#d47824", "-"),
    ]
    for label, data, start, colour, style in series:
        rows = [p for p in data if -35 <= p["ReferenceMeters"] - start <= 40]
        x = [p["ReferenceMeters"] - start for p in rows]
        speed = [3.6 * math.hypot(p["State"]["Momentum"]["X"], p["State"]["Momentum"]["Y"]) / 147 for p in rows]
        values = [speed, [p["HeadingError"] for p in rows], [p["Roll"] for p in rows],
                  [p["SweptBoundary"] for p in rows]]
        for ax, y in zip(axes.flat, values):
            ax.plot(x, y, label=label, color=colour, linestyle=style, linewidth=1.65)
    labels = ["Speed (km/h)", "Velocity heading relative to track (rad)", "Roll (rad)",
              "Swept boundary reserve (m)"]
    for ax, label in zip(axes.flat, labels):
        ax.set_ylabel(label)
        ax.axvline(0, color="#596678", ls=":", lw=.95)
        ax.grid(True, color="#dfe5eb", linewidth=.65)
        ax.set_xlim(-35, 36)
    axes[1, 1].axhline(.5, color="#6a1638", linestyle=":", linewidth=1)
    axes[0, 0].legend(loc="upper right", framealpha=.95, fontsize=8)
    for ax in axes[1]:
        ax.set_xlabel("Course metres relative to turn entry")
    fig.suptitle("H42-O · What changes at the next flying entry", fontsize=15, fontweight="bold")
    fig.savefig(root / "flying-entry.png", dpi=170)


if __name__ == "__main__":
    main()
