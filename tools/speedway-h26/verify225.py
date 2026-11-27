"""Check Study #225 evidence and the paired native gate."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path

from verify219 import regression
from verify222 import read


def check_tape(path):
    motion = read(path)
    q = motion["Commands"]
    assert motion["Seed"]["Time"] <= motion["Terminal"]["Time"]
    assert all(command["Requested"] <= command["Time"] for command in q)
    assert all(x["Requested"] <= y["Requested"] and x["Time"] <= y["Time"] for x, y in zip(q, q[1:]))
    assert motion["PendingAtTerminal"] == [c for c in q[motion["Terminal"]["Next"]:]
                                             if c["Requested"] <= motion["Terminal"]["Time"] + 1.e-10]
    assert motion["MinimumSwept"] >= .5
    assert motion["MinimumHard"] > 0
    return motion["FinalStation"]


def check_stage(root, name, expected):
    stage = read(root / f"{name}.json.gz")
    checked = {r["Plan"]["Id"] for r in stage["Screen"]}
    assert len(checked) == len(stage["Screen"]) == expected
    assert stage["Exact"]
    for r in stage["Exact"]:
        assert r["Plan"]["Id"] in checked
        if r["Finished"]:
            assert r["Station"] >= r["Finish"] - .001 and r["Clearance"] >= .5 and r["Hard"] > 0
    return {"proposals": len(checked), "exact": len(stage["Exact"]),
            "fullHorizons": sum(r["Finished"] for r in stage["Exact"])}


def verify(root):
    stages = {n: check_stage(root, n, count)
              for n, count in [("lines", 64), ("transition", 64), ("support", 56), ("speed", 60)]}
    speed = read(root / "speed.json.gz")
    assert {r["Plan"]["SpeedBand"] for r in speed["Screen"]} == {100, 105, 110, 115, 120}
    continuation = read(root / "speed-recovery.json.gz")
    assert len(continuation["Results"]) == 56 and continuation["Exact"]
    tapes = {p.name: check_tape(p) for p in sorted(root.glob("*-tape.json.gz")) if not p.name.startswith("discrepancy-")}
    references = read(root / "references.json")
    assert references["NativeWorlds"] == 0 and len(references["Reproduced"]) == 8
    assert all(r["PositionError"] < .002 and r["MomentumError"] <= .12 for r in references["Reproduced"])
    holdouts = read(root / "holdouts.json")
    assert holdouts["freeze"] == read(root / "feedback-freeze.json")
    assert len(holdouts["Trials"]) == 4
    assert [r["Seed"] for r in holdouts["Trials"]] == holdouts["freeze"]["Starts"]
    parent = regression(root / "parent.trx.gz")
    current = regression(root / "current.trx.gz")
    assert parent == current
    manifest = read(root / "manifest.json")
    for path, expected in manifest.items():
        with (root / path).open("rb") as source:
            assert hashlib.file_digest(source, "sha256").hexdigest() == expected["sha256"]
        assert (root / path).stat().st_size == expected["bytes"]
    return {"stages": stages, "tapes": tapes, "regression": current, "checkedDigests": len(manifest)}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    args = parser.parse_args()
    print(json.dumps(verify(args.root), indent=2))
