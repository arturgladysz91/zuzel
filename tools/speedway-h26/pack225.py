"""Pack selected H44 evidence and render XY, steering, speed and rider support plots."""
import argparse
import gzip
import hashlib
import json
import shutil
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt


def read(path):
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8-sig") as source:
            return json.load(source)
    return json.loads(path.read_text(encoding="utf-8-sig"))


def compressed(path, payload):
    path.write_bytes(gzip.compress(json.dumps(payload, allow_nan=False, separators=(",", ":")).encode(), mtime=0))


def compact(raw, output, name):
    archive = read(raw / f"{name}.json.gz")
    rows = archive["Rows"]
    prior = archive["Commands"]
    assert rows and all(a["Requested"] <= a["Time"] for a in prior)
    assert all(a["Requested"] <= b["Requested"] and a["Time"] <= b["Time"] for a, b in zip(prior, prior[1:]))
    terminal = rows[-1]["State"]
    assert terminal["Next"] <= len(prior)
    seed_station = read(raw / "standing.json")["StandingStart"] if name.startswith("standing-") else -64
    compressed(output / f"{name}-tape.json.gz", {
        "Name": name, "Stop": archive["Stop"], "Seed": archive["Seed"], "SeedStation": seed_station, "Commands": prior,
        "AcceptedSteps": len(rows), "FinalStation": rows[-1]["ReferenceMeters"], "Terminal": terminal,
        "PendingAtTerminal": [r for r in prior[terminal["Next"]:] if r["Requested"] <= terminal["Time"] + 1.e-10],
        "MinimumSwept": min(r["SweptBoundary"] for r in rows),
        "MinimumHard": min(r["SweptGround"] for r in rows),
    })
    return archive


def chart(directory, lines):
    spans = [
        ("Offset", "Actual offset (m)"),
        ("Speed", "Speed (km/h)"),
        ("Curvature", "Actual curvature (1/m)"),
        ("ActiveDrive", "Active Drive"),
    ]
    fig, axes = plt.subplots(len(spans), 1, figsize=(11, 10), sharex=True, layout="constrained")
    xy, ax = plt.subplots(figsize=(8, 6), layout="constrained")
    for name, values in lines:
        phase = [(r["Station"], r) for r in values if 244.60 <= r["Station"] <= 637.56]
        if len(phase) < 4:
            continue
        s, rows = zip(*phase)
        for target, (key, label) in zip(axes, spans):
            target.plot(s, [r[key] for r in rows], lw=1.25, label=name)
            target.set_ylabel(label)
            target.grid(alpha=.22)
        ax.plot([r["Center"]["X"] for r in rows], [r["Center"]["Y"] for r in rows], lw=1.6, label=name)
    for target in axes:
        for start, end in [(318.779, 416.168), (478.168, 575.557)]:
            target.axvspan(start, end, alpha=.08, color="#80562d")
    axes[0].legend(ncols=2)
    axes[-1].set_xlabel("Reference station from the first bend entry (m)")
    fig.suptitle("Study #225 actual motion from the handoff through the common lap")
    fig.savefig(directory / "racing-line-metrics.png", dpi=150)
    ax.set(xlabel="World X (m)", ylabel="World Y (m)", title="Actual race trajectories")
    ax.set_aspect("equal")
    ax.grid(alpha=.22)
    ax.legend()
    xy.savefig(directory / "racing-trajectories.png", dpi=160)


def rider_chart(directory, history):
    original = next(x for x in history["Traces"] if x["Name"] == "initial four")["Last"]
    original = [r for r in original if 724.5 <= r["Station"]]
    keys = [
        ("ActiveDrive", "Active Drive", lambda r: r["ActiveDrive"]),
        ("Front and pads", "Load (N)", lambda r: r["Front"]),
        ("Pitch", "Pitch (rad)", lambda r: r["Pitch"]),
        ("Rider travel", "Relative rider X (m)", lambda r: r["RelativePosition"]["X"]),
        ("Power", "Muscle power (W)", lambda r: r["AbsolutePower"]),
    ]
    fig, axes = plt.subplots(len(keys), 1, figsize=(10, 9), sharex=True, layout="constrained")
    for axis, (name, units, value) in zip(axes, keys):
        axis.plot([r["Station"] for r in original], [value(r) for r in original], label=name)
        axis.set_ylabel(units)
        axis.grid(alpha=.23)
    pads = ["Seat", "LeftPeg", "RightPeg", "LeftGrip", "RightGrip"]
    axes[1].plot([r["Station"] for r in original],
                 [sum(max(0, (r[p] or {}).get("Z", 0)) for p in pads) for r in original], label="Pad vertical total")
    axes[1].legend()
    axes[3].axhline(-.36, color="#aa2d2d", linestyle="--")
    axes[4].axhline(350, color="#aa2d2d", linestyle="--")
    axes[-1].set_xlabel("Reference station from the first bend entry (m)")
    fig.suptitle("Study #224 repeated line before rider travel rejection")
    fig.savefig(directory / "rider-exit-event.png", dpi=150)


def publish(raw, target):
    target.mkdir(parents=True, exist_ok=True)
    history = raw / "rider-history.json.gz"
    if history.exists():
        audit = read(history)
        profiles = [("Frozen H43", next(x for x in audit["Traces"] if x["Name"] == "frozen H43")["History"])]
        rider_chart(target, audit)
    else:
        profiles = []
    for path in sorted(raw.glob("*.json*")):
        if path.name.endswith("-decisions.json.gz") or path.name.startswith("discrepancy-"):
            shutil.copyfile(path, target / path.name)
            continue
        if path.name in {"references.json", "rider-history.json.gz", "guide-geometry.json.gz", "discrepancies.json.gz",
                         "matched-limits.json.gz", "lines.json.gz", "transition.json.gz", "speed.json.gz",
                         "support.json.gz", "support-exact.json.gz", "sustain.json.gz", "speed-recovery.json.gz", "runtime.json", "feedback-freeze.json", "holdouts.json",
                         "standing.json", "reproduced-feedback.json"}:
            shutil.copyfile(path, target / path.name)
            continue
        stem = path.name.removesuffix(".json.gz")
        if path.name.endswith(".json.gz") and stem.startswith(("lines-", "transition-", "speed-", "support-", "sustain-", "runtime-",
                                                               "holdout-", "standing-")):
            compact(raw, target, stem)
    for stage in ("lines", "transition"):
        file = raw / f"{stage}.json.gz"
        if not file.exists():
            continue
        seen = set()
        for record in read(file)["Exact"]:
            plan = record["Plan"]
            family = plan["Line"]
            if family in seen:
                continue
            seen.add(family)
            name = f"{stage}-{plan['Id']}"
            receipt = raw / f"{name}-decisions.json.gz"
            if receipt.exists():
                profiles.append((f"{'ABCD'[family]} {plan['Id']}", read(receipt)["Profile"]))
    if profiles:
        chart(target, profiles)
    checksums = {}
    for path in sorted(target.iterdir()):
        if path.is_file() and path.name != "manifest.json":
            with path.open("rb") as source:
                checksums[path.name] = {"bytes": path.stat().st_size, "sha256": hashlib.file_digest(source, "sha256").hexdigest()}
    (target / "manifest.json").write_text(json.dumps(checksums, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument("raw", type=Path)
    args.add_argument("target", type=Path)
    options = args.parse_args()
    publish(options.raw, options.target)
