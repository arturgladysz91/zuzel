"""Render H41 linked trajectories and the approach heading comparison."""

import argparse
import gzip
import json
from pathlib import Path

from plot221 import field, plot, plt, speed


def heading(root):
    with gzip.open(root / "heading-audit.json.gz", "rt") as f:
        record = json.load(f)
    fig, axes = plt.subplots(3, 2, figsize=(12, 8.3), sharex=True, layout="constrained")
    fig.suptitle("H41-V | Entry direction, load and recoverability", fontsize=16, fontweight="bold")
    names = [("highestEntry", "107.33 km/h entry", "#933961"),
             ("unloaded", "91.59 km/h entry", "#7453aa"),
             ("adaptive", "H41 adaptive nominal", "#117871")]
    fields = [("Speed (km/h)", "speedKph"), ("Velocity-heading error (rad)", "roadHeadingErrorRad"),
              ("Actual velocity curvature (1/m)", "curvaturePerM"), ("Roll rate (rad/s)", "rollRateRadps"),
              ("Front normal load (N)", "frontNormalN"), ("Swept boundary reserve (m)", "boundaryM")]
    for ax, (title, key) in zip(axes.flat, fields, strict=True):
        for name, label, colour in names:
            rows = record[name]["rows"]
            ax.plot([r["relativeEntryM"] for r in rows], [r[key] for r in rows],
                    color=colour, label=label, linewidth=1.55)
        ax.axvline(0, linewidth=.9, linestyle=":", color="#817b75")
        if key == "boundaryM":
            ax.axhline(.5, linewidth=.9, linestyle="--", color="#933961")
        ax.set(ylabel=title, xlim=(-36, 26))
        ax.grid(alpha=.22)
    axes.flat[0].legend(loc="best", fontsize=8)
    for ax in axes[-1]:
        ax.set_xlabel("Reference metres relative to second entry")
    fig.savefig(root / "heading-and-clearance.png", dpi=155, facecolor="white")
    plt.close(fig)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    root = parser.parse_args().root
    heading(root)
    prev = json.loads((root.parent/"speedway221/stationing.json").read_text())
    marks = [prev[k] for k in ("FirstExit", "SecondEntry", "SecondExit")]
    plot(root, "linked-pace.png", "H41-V | Linked racing pace under the stock contact model",
         [("../speedway221/transfer/transfer-nominal", "H40 adaptive baseline", "#425470"),
          ("qualification/nominal", "H41 fastest adaptive", "#17776e"),
          ("frontier/speed-frontier", "Highest speed diagnostic", "#c98143")],
         [("Speed (km/h)", speed, 100), ("Active Drive (%)", field("ActiveDrive", 100), None),
          ("Front normal load (N)", field("FrontNormal"), 0), ("Roll (rad)", field("Roll"), None),
          ("Actual rear slip ratio", field("ContactSlip"), .06), ("Swept ground margin (m)", field("SweptGround"), 0)],
         marks, (0, marks[-1] + 24))


if __name__ == "__main__":
    main()
