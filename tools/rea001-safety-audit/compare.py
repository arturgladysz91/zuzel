"""REA-001: exact final states, strict C traces and pinned native B diagnostics."""
import gzip
import json
from pathlib import Path
import re
import sys

NATIVE_B_EVIDENCE = Path(__file__).resolve().parents[2] / "tests/fixtures/rea001-native-b-portability.json"


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


def typed_leaf(value):
    # Exact.Leaves encodes floating-point numbers as raw bits, never JSON floats.
    if type(value) is str:
        if re.fullmatch(r"float:[0-9A-F]{8}|double:[0-9A-F]{16}", value):
            kind, bits = value.split(":")
            return {"Type": kind, "Bits": bits}
        if ":" in value:
            kind, payload = value.split(":", 1)
            return {"Type": kind, "Value": payload}
        raise ValueError("Untyped trace string")
    kinds = {type(None): "null", bool: "boolean", int: "integer"}
    if type(value) not in kinds:
        raise ValueError("Untyped trace number")
    return {"Type": kinds[type(value)], "Value": value}


def divergence_map(left, right):
    missing = {"Type": "missing"}
    differences = {}
    for path in sorted(left.keys() | right.keys()):
        w = typed_leaf(left[path]) if path in left else missing
        u = typed_leaf(right[path]) if path in right else missing
        if w != u:
            differences[path] = {"Windows": w, "Ubuntu": u}
    return differences


def compare_platforms(windows, ubuntu, expected=None, report_path=None):
    if expected is None:
        expected = json.loads(NATIVE_B_EVIDENCE.read_text(encoding="utf-8"))
    evidence, observed = {}, {}
    for configuration in "BC":
        left = read(windows, "after", configuration)
        right = read(ubuntu, "after", configuration)
        name = f"trace-outside-7-{configuration}.json.gz"
        with gzip.open(Path(windows) / f"after-{configuration}" / name) as stream:
            left_trace = json.load(stream)
        with gzip.open(Path(ubuntu) / f"after-{configuration}" / name) as stream:
            right_trace = json.load(stream)
        differences = divergence_map(left_trace, right_trace)
        if differences:
            observed[f"outside/7/{configuration}"] = differences
        evidence[configuration] = {
            "Completed": not left["Failed"] and not right["Failed"],
            "FinalStateExact": left["FinalHash"] == right["FinalHash"],
            "SummaryBehaviorExact": left["BehaviorHash"] == right["BehaviorHash"],
            "SummaryBehaviorHashes": {"Windows": left["BehaviorHash"], "Ubuntu": right["BehaviorHash"]},
            "FullAuditExact": not differences,
            "TraceLeaves": {"Windows": len(left_trace), "Ubuntu": len(right_trace)},
        }
    report = {"Cases": evidence, "ExpectedDivergences": expected["Divergences"],
              "ObservedDivergences": observed}
    if report_path is not None:
        Path(report_path).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if any(not row["Completed"] or not row["FinalStateExact"] for row in evidence.values()):
        raise ValueError("Windows/Ubuntu completed final production states differ")
    if not evidence["C"]["SummaryBehaviorExact"] or not evidence["C"]["FullAuditExact"]:
        raise ValueError("Strict C behavior/complete typed FullAudit trace differs")
    # B uses unchanged native geometry when physical consequences are OFF. Main
    # aborted before this complete trace existed; this is separate new evidence,
    # not a replacement for any historical golden or the old 22-case map.
    if evidence["B"]["SummaryBehaviorHashes"] != expected["SummaryBehaviorHashes"]:
        raise ValueError("Exact per-OS B summary behavior changed")
    if observed != expected["Divergences"]:
        raise ValueError("Native B divergence map changed (case/path/type/value/IEEE bits)")
    return report


if __name__ == "__main__":
    if len(sys.argv) == 2:
        compare(sys.argv[1])
    else:
        if len(sys.argv) not in (3, 5) or (len(sys.argv) == 5 and sys.argv[3] != "--report"):
            raise ValueError("Usage: compare.py WINDOWS [UBUNTU [--report PATH]]")
        compare(sys.argv[1]); compare(sys.argv[2])
        report = compare_platforms(sys.argv[1], sys.argv[2], report_path=sys.argv[4] if len(sys.argv) == 5 else None)
        print("Strict C trace exact; completed B/C final states exact; native B diagnostic divergence map unchanged.")
    print("REA-001: original failures reproduced, A bit-exact, B/C completed with exact order/observer parity.")
