"""Derive heading, travelled curvature, and force diagnostics from accepted replays."""

import argparse
import bisect
import gzip
import json
import math
from pathlib import Path


def read(path):
    with gzip.open(path, "rt", encoding="utf-8-sig") as stream:
        return json.load(stream)


def transcribe(path, entry):
    event = read(path)
    source = event["Rows"]
    distances = []
    total = 0.0
    p0 = event["Seed"]["Center"]
    for p in source:
        p1 = p["State"]["Center"]
        total += math.hypot(p1["X"] - p0["X"], p1["Y"] - p0["Y"])
        distances.append(total)
        p0 = p1
    rows = []
    for i, sample in enumerate(source):
        x = sample["ReferenceMeters"] - entry
        if not -52 <= x <= 28:
            continue
        j = bisect.bisect_left(distances, distances[i] - .4)
        k = min(len(source) - 1, bisect.bisect_left(distances, distances[i] + .4))
        span = distances[k] - distances[j]
        heading = sample["VelocityHeading"]
        dv = math.remainder(source[k]["VelocityHeading"] - source[j]["VelocityHeading"], math.tau)
        curvature = dv / span if span > 0 else 0.0
        p = sample["State"]["Momentum"]
        speed = math.hypot(p["X"], p["Y"]) / 147
        rows.append({
            "relativeEntryM": x, "timeS": sample["State"]["Time"], "speedKph": 3.6 * speed,
            "velocityHeadingRad": heading, "frameHeadingRad": sample["Yaw"],
            "roadHeadingErrorRad": sample["HeadingError"], "curvaturePerM": curvature,
            "inwardAccelerationMps2": speed * speed * curvature, "rollRad": sample["Roll"],
            "rollRateRadps": sample["RollRate"], "yawRateRadps": sample["YawRate"],
            "frontNormalN": sample["FrontNormal"], "rearNormalN": sample["RearNormal"],
            "activeDrive": sample["ActiveDrive"], "boundaryM": sample["SweptBoundary"],
            "hardPartM": sample["SweptGround"], "sideSlipRad": sample["VehicleSlip"],
            "rearSlip": sample["ContactSlip"],
        })
    beginning = next((p for p in rows if p["relativeEntryM"] >= -12 and p["roadHeadingErrorRad"] < -.1), None)
    return {"source": path.name, "stop": event["Stop"], "outwardThreshold": beginning, "last": rows[-1], "rows": rows}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    root = parser.parse_args().root
    sibling = root.parent / "speedway221"
    entry = json.loads((sibling / "stationing.json").read_text())["SecondEntry"]
    sources = [sibling / "links" / name for name in ("direct-12.json.gz", "direct-45.json.gz")]
    sources.append(root / "qualification" / "nominal.json.gz")
    result = {("highestEntry", "unloaded", "adaptive")[i]: transcribe(p, entry) for i, p in enumerate(sources)}
    with (root / "heading-audit.json.gz").open("wb") as out:
        with gzip.GzipFile(filename="", mode="wb", fileobj=out, mtime=0) as f:
            f.write(json.dumps(result, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8"))


if __name__ == "__main__":
    main()
