"""Exact per-platform original-main/new-head audit parity, excluding measurement cost."""
import json
from pathlib import Path
import sys


def compare(before, after):
    def cases(manifest):
        if manifest.get("Completed") is not True or manifest.get("Schema") != "race-readiness-v1":
            raise ValueError("Incomplete capture")
        result = {}
        for row in manifest["Cases"]:
            key = (row["Id"], row["Seed"], row["Configuration"])
            if key in result:
                raise ValueError("Duplicate case")
            if row["ReversedExact"] is not True or row["ObserverExact"] is not True:
                raise ValueError("Smoke parity check missing")
            if row.get("Failed", False):
                raise ValueError("A failed heat cannot pass the smoke comparison")
            result[key] = row["FinalHash"], row["BehaviorHash"]
        return result
    if cases(before) != cases(after):
        raise ValueError("Exact baseline/head physics, decision, logging or surface divergence")


if __name__ == "__main__":
    root = Path(sys.argv[1])
    before = json.loads((root / "before/manifest.json").read_text())
    after = json.loads((root / "after/manifest.json").read_text())
    expected = {(track, 19, cfg) for track in ("balanced-example", "balanced-motoarena") for cfg in "ABC"}
    for capture in (before, after):
        if capture["Mode"] != "smoke" or {(r["Id"], r["Seed"], r["Configuration"]) for r in capture["Cases"]} != expected:
            raise ValueError("The complete six-case smoke matrix is required")
    compare(before, after)
    print("Six audit smoke cases: exact original-main/head, input order and observer parity.")
