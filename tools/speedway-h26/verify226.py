"""Render and check H45-N evidence from verified MODEL runs."""
import argparse
import gzip
import hashlib
import itertools
import json
from pathlib import Path

from verify219 import regression


def load(path):
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8-sig") as stream:
            return json.load(stream)
    return json.loads(path.read_text(encoding="utf-8-sig"))


def plot_air(root):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    air = load(root / "airborne-exit.json.gz")["Cases"][0]["Meshes"]
    figure, axes = plt.subplots(4, 1, sharex=True, figsize=(9.8, 9), layout="constrained")
    origin = 47.65
    series = [
        ("Rear", "Rear normal load (N)"),
        ("VerticalMomentum", "Vertical momentum (kg m/s)"),
        ("Pitch", "Pitch (rad)"),
        ("RiderRate", "Relative fore-aft speed (m/s)"),
    ]
    for trial in [air[0], air[-1]]:
        rows = [r for r in trial["Rows"] if r["After"]["Time"] >= origin]
        for axis, (key, unit) in zip(axes, series, strict=True):
            axis.plot([r["After"]["Time"] - origin for r in rows],
                      [r[key]["X"] if key == "RiderRate" else r[key] for r in rows],
                      linewidth=1.4, label=f"{trial['StepMilliseconds']:g} ms")
            axis.set_ylabel(unit)
            axis.grid(alpha=.23)
    zero = ((r["Front"] == r["Rear"] == 0, r) for r in air[-1]["Rows"])
    airborne = [list(x) for key, x in itertools.groupby(zero, key=lambda x: x[0]) if key]
    longest = max(airborne, key=lambda group: sum(r["Duration"] for _, r in group))
    first, last = longest[0][1], longest[-1][1]
    for axis in axes:
        axis.axvspan(first["After"]["Time"] - first["Duration"] - origin, last["After"]["Time"] - origin,
                     color="#2e7490", alpha=.10)
    axes[0].legend(title="Maximum step")
    axes[-1].set_xlabel("Seconds after t = 47.65 s")
    figure.suptitle("Two-wheel flight and rear landing on the unchanged MODEL")
    figure.savefig(root / "airborne-convergence.png", dpi=160)


def plot_divergence(root):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    chart, (a, b) = plt.subplots(2, 1, sharex=True, figsize=(9.5, 6), layout="constrained")
    for name in ("H43-475", "H43-476", "H43-477", "A224", "D802"):
        q = root / f"{name}-legacy.json.gz"
        if not q.exists():
            continue
        problem = load(q)["Difference"]
        rows = problem["Growth"] if problem else []
        if not rows:
            continue
        start = rows[0]["Time"]
        for axis, field in [(a, "PositionM"), (b, "Momentum")]:
            axis.semilogy([r["Time"] - start for r in rows],
                          [max(1.e-15, r[field]) for r in rows], label=name, linewidth=1.45)
            axis.grid(alpha=.24)
    a.set_ylabel("Center position difference (m)")
    b.set_ylabel("Momentum difference (kg m/s)")
    b.set_xlabel("Seconds since the first differing accepted state")
    a.legend(ncols=3)
    chart.suptitle("Legacy online-window vs full-root replay")
    chart.savefig(root / "legacy-divergence-growth.png", dpi=155)


def verify(root):
    grid = load(root / "determinism-summary.json")
    assert len(grid["Cases"]) >= 3 and all(p["exactSuffix"] and p["IndependentCommandReplay"]
                                             and p["RecordedStepReplay"] for p in grid["Cases"])
    for r in grid["Cases"]:
        s = load(root / f"{r['Name']}-independent-process.json")
        assert s["Equal"] and s["Expected"] == s["Result"] == r["baseline"]
    source = load(root / "declared-review-alignment.json")
    for r in source["Results"]:
        assert r["scheduleEqual"] and r["resumed"]
        independent = load(root / f"{r['Name']}-aligned-independent-process.json")
        assert independent["Equal"] and independent["Result"] == r["result"]
    legacy = load(root / "legacy-summary.json")
    assert len(legacy["Outcomes"]) == 12
    assert abs(next(x for x in legacy["Outcomes"] if x["Name"] == "H43-longest")["ReplayStation"] - 970.902) < .003
    ground = load(root / "swept-ground.json.gz")
    for c in ground["Cases"]:
        assert c["Stock"]["Reproduced"]
        assert c["Meshes"][2]["Reached"] and c["Meshes"][2]["Ground"] > 0.0
        assert c["Adaptive"]["Ground"] > 0.0 and c["Adaptive"]["Boundary"] >= .50
    for a in load(root / "airborne-exit.json.gz")["Cases"]:
        assert len(a["Meshes"]) == 3 and all(.29 < t["DoubleAirSeconds"] < .32 for t in a["Meshes"])
        assert all(t["Stop"] == "rider travel envelope" for t in a["Meshes"])
    assert load(root / "matched-drive.json.gz")["NowSafe"] == 17
    for file in ("feedback-holdouts.json.gz", "declared-holdouts.json.gz"):
        data = load(root / file)
        keys = "Held" if "Held" in data else "Cases"
        assert len(data[keys]) == 4
    qualification = load(root / "qualification.json")
    assert len(qualification["Candidates"]) == 43 and qualification["NativeWorlds"] == 0
    old, old_fail = regression(root / "parent.trx.gz")
    new, new_fail = regression(root / "current.trx.gz")
    assert old_fail == new_fail and new["passed"] - old["passed"] == 4
    assert all("Box2D construction forbidden" in error for error in new_fail.values())
    return {"FixedDeterminism": len(grid["Cases"]), "DeclaredDeterminism": len(source["Results"]),
            "HistoricalReproductions": len(legacy["Outcomes"]), "LocalPasses": 17, "Parent": old, "Current": new}


def manifest(root):
    result = {}
    for path in sorted(root.iterdir()):
        if path.is_dir() or path.name == "manifest.json":
            continue
        with path.open("rb") as stream:
            result[path.name] = {"bytes": path.stat().st_size, "sha256": hashlib.file_digest(stream, "sha256").hexdigest()}
    return result


if __name__ == "__main__":
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument("root", type=Path)
    args.add_argument("--plots", action="store_true")
    args.add_argument("--seal", action="store_true")
    options = args.parse_args()
    if options.plots:
        plot_air(options.root)
        plot_divergence(options.root)
    receipt = verify(options.root)
    if options.seal:
        (options.root / "manifest.json").write_text(json.dumps(manifest(options.root), indent=2) + "\n",
                                                    encoding="utf-8", newline="\n")
    else:
        assert manifest(options.root) == load(options.root / "manifest.json")
    print(json.dumps(receipt, indent=2))
