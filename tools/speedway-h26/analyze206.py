"""Summarize archived decisions and certification evidence without starting a simulator."""

import argparse
from collections import Counter
import gzip
import json
from pathlib import Path
from statistics import correlation, mean


def read(path):
    with (gzip.open(path, "rt", encoding="utf-8") if path.suffix == ".gz"
          else path.open(encoding="utf-8")) as stream:
        return json.load(stream)


def profile(data):
    summary = data["Summary"]
    detail = data["Detail"]
    if not detail:
        return summary
    slow = [row for row in detail if row["WallMs"] > 50]
    times = [row["WallMs"] for row in detail]
    fields = {}
    for name in detail[0]["Guards"]:
        values = [row["Guards"][name] + row["Worker"][name] for row in detail]
        slow_values = [row["Guards"][name] + row["Worker"][name] for row in slow]
        fields[name] = {
            "total": sum(values),
            "meanPerDecision": mean(values),
            "meanAbove50": mean(slow_values) if slow_values else None,
            "pearsonVsWall": correlation(values, times) if min(values) < max(values) else None,
        }
    return {**summary, "profile": fields, "workerWallTicks": sum(row["WorkerTicks"] for row in detail),
            "firstDecisionWallMs": detail[0]["WallMs"],
            "slowPhases": dict(Counter(row["Phase"] for row in data["SlowStates"]))}


def frozen(root):
    path = root / "data/experiments/speedway205"
    native_summary = read(path / "native-summary.json")
    tapes = ["H28-C-learned-C", "H28-C-learned-C-hold-minus"]
    style = read(path / "native-parity-and-style.json")["Checks"]
    preflight = read(path / "preflight-model.json.gz")
    ledger = (path / "native/world-ledger.jsonl").read_text(encoding="utf-8").splitlines()
    throttle = read(path / "throttle-summary.json")
    return {
        "finishes": [
            {"fixture": tape, "measured": native_summary[tape]["result"],
             "residualEvaluations": native_summary[tape]["solveQueries"],
             "certificateBoxes": sum(step["Precision"]["Combined"]["ProofBoxes"]
                                     for step in read(path / f"native/{tape}.json.gz")["Loads"][0])}
            for tape in tapes
        ],
        "constructions": [json.loads(line) for line in ledger],
        "style": style,
        "offsetPreflights": [
            {key: row[key] for key in ("Fixture", "Outcome", "End", "Sweep")}
            for row in preflight if row["Variant"] == "H28-C" and row["Label"] == "relearned"
        ],
        "throttlePaired": throttle["paired"],
        "throttlePhases": [
            dict(physics=row["Physics"], lap=row["Lap"], index=row["Index"],
                 baseline=row["Baseline"]["Outcome"], full=row["Full"]["Outcome"])
            for row in throttle["phases"]
        ],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    paths = parser.parse_args()
    directory = paths.evidence
    root = Path(__file__).resolve().parents[2]
    benches = {path.stem.split(".json")[0]: profile(read(path))
               for path in sorted((directory / "profiling").glob("*.gz"))}
    archives = read(directory / "parity.json")["Archives"]
    parity = {key: sum(row[key] for row in archives)
              for key in ("Total", "FrozenExact", "PairExact", "Determinism", "ClassificationsChanged")}
    for variant in ("H28C", "H28F"):
        parity[variant] = {key: sum(row[variant][key] for row in archives)
                           for key in ("Accepted", "Residuals", "WheelQueries", "FullBoxes", "VisitedBoxes",
                                       "EarlyExclusions", "ForceTicks", "SolverTicks", "PowerTicks", "IntervalTicks",
                                       "CheapTicks", "IsolationTicks", "RefinementTicks", "BoundaryTicks")}
    search = read(directory / "controlled-search.json.gz")
    summary = dict(frozen=frozen(root), profiling=benches, parity=parity, candidateSearch={
        "attempts": len(search["Attempts"]), "nominalPassed": sum(row["Legal"] for row in search["Attempts"]),
        "nominalFailureCounts": dict(Counter(row["Stop"] for row in search["Attempts"] if not row["Legal"])),
        "candidateFixtures": [
            {"id": row["Candidate"]["Id"],
             "predictions": [
                 {"fixture": f["Fixture"], "outcome": f["Outcome"], "time": f["End"]["Time"],
                  "s": f["End"]["S"], "swept": f["Swept"]} for f in row["Validation"]
             ]}
            for row in search["Offsets"]
        ],
        "winner": search["Winner"],
    })
    with (directory / "summary.json").open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(summary, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.write("\n")


if __name__ == "__main__":
    main()
