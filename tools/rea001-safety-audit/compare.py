"""REA-001 correction only: exact feature-OFF preservation and completed B/C parity."""
import gzip
import json
from pathlib import Path
import sys


def read(root, phase, configuration):
    directory = Path(root) / f"{phase}-{configuration}"
    manifest = json.loads((directory / "manifest.json").read_text())
    if manifest.get("Schema") != "race-readiness-v1" or manifest.get("Completed") is not True:
        raise ValueError("Incomplete REA-001 evidence")
    if manifest["Mode"] != "repro" or len(manifest["Cases"]) != 1:
        raise ValueError("Exactly one fresh outside/7 repro required")
    row = manifest["Cases"][0]
    if (row["Id"], row["Seed"], row["Configuration"]) != ("outside", 7, configuration):
        raise ValueError("Wrong REA-001 fixture")
    if row["ReversedExact"] is not True or row["ObserverExact"] is not True:
        raise ValueError("Missing exact input/observer parity")
    with gzip.open(directory / f"case-outside-7-{configuration}.json.gz") as stream:
        case = json.load(stream)
    if any(case[key] != row[key] for key in ("Failed", "FinalHash", "BehaviorHash")):
        raise ValueError("Manifest/raw evidence mismatch")
    return case


def compare_cases(before, after, configuration):
    if after["Failed"]:
        raise ValueError("REA-001 still aborts production")
    if configuration == "A":
        if before["Failed"] or any(before[key] != after[key] for key in ("FinalHash", "BehaviorHash")):
            raise ValueError("Exact original-main feature-OFF behavior changed")
    else:
        failure = before.get("Failure") or {}
        if not before["Failed"] or failure.get("Type") != "CoreSim.ExecutedPathTraversal+TimeSolveDiscontinuityException" or failure.get("Message") != "Coupled time solve did not converge within its bound.":
            raise ValueError("Original REA-001 was not reproduced")
        if len(after["Classification"]) != 4 or after["Violations"]:
            raise ValueError("Invalid completed production evidence")
        if configuration == "C" and not after["Applied"]:
            raise ValueError("Real physical contact consequences were suppressed")


def compare(root):
    for configuration in "ABC":
        compare_cases(read(root, "before", configuration), read(root, "after", configuration), configuration)


def compare_platforms(windows, ubuntu):
    # New enabled correction cases must satisfy strict typed behavior parity.
    for configuration in "BC":
        left = read(windows, "after", configuration)
        right = read(ubuntu, "after", configuration)
        if left["Failed"] or right["Failed"] or any(left[key] != right[key] for key in ("FinalHash", "BehaviorHash")):
            raise ValueError("Windows/Ubuntu corrected production behavior differs")
        name = f"trace-outside-7-{configuration}.json.gz"
        with gzip.open(Path(windows) / f"after-{configuration}" / name) as stream:
            left_trace = json.load(stream)
        with gzip.open(Path(ubuntu) / f"after-{configuration}" / name) as stream:
            right_trace = json.load(stream)
        if left_trace != right_trace:
            raise ValueError("Complete FullAudit typed trace/IEEE bits differ")


if __name__ == "__main__":
    if len(sys.argv) == 2:
        compare(sys.argv[1])
    else:
        compare(sys.argv[1]); compare(sys.argv[2]); compare_platforms(sys.argv[1], sys.argv[2])
    print("REA-001: original failures reproduced, A bit-exact, B/C completed with exact order/observer parity.")
