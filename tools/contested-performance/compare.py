"""Require exact per-OS preservation and an unchanged complete platform divergence map."""
import hashlib
import json
from pathlib import Path
import re
import sys


def read_manifest(directory, phase):
    manifest = json.loads((Path(directory) / f"{phase}.json").read_text(encoding="utf-8"))
    assert manifest, "Empty capture"
    filenames = [case.replace("/", "_") for case in manifest]
    assert len(filenames) == len(set(filenames)), "Ambiguous raw capture filenames"
    for case, row in manifest.items():
        assert set(row) == {"Leaves", "IEEELeaves", "SHA256"}, f"Invalid manifest: {case}"
        assert type(row["Leaves"]) is int and type(row["IEEELeaves"]) is int, case
    return manifest


def compare(directory):
    before = read_manifest(directory, "before")
    after = read_manifest(directory, "after")
    assert before.keys() == after.keys(), "Capture case coverage changed"
    raw = (Path(__file__).resolve().parents[2] / "tests/fixtures/rea009-historical-boundaries.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "dd254d60d06b2ac97498aaf745aedfd65f3fda67f6d4318686b46ca36e631ed1"
    expected = json.loads(raw)
    assert expected["Schema"] == "rea009-historical-topology-v1"
    assert expected["BaseMainSha"] == "cd19c399dfc97d7abbd2aca03d50aa2c94a76262"
    observed = {}
    for case in before:
        # Entire typed captures stay exact, except the complete separately recorded
        # REA-009 topology-only delta. No physical leaf or historical golden is replaced.
        if before[case] != after[case]:
            left = read_leaves(directory, "before", case, before[case])
            right = read_leaves(directory, "after", case, after[case])
            observed[case] = {p: {"BeforePresent": p in left, "Before": left.get(p),
                                  "AfterPresent": p in right, "After": right.get(p)}
                              for p in sorted(left.keys() | right.keys())
                              if p not in left or p not in right or left[p] != right[p]}
        if case.endswith("/False"):
            reversed_case = case.removesuffix("/False") + "/True"
            assert after[case] == after[reversed_case], f"Rider-order dependence: {case}"
    required = {case: delta for case, delta in expected["Changes"].items() if case in before}
    assert observed == required, "Exact original-main behavior changed outside recorded REA-009 topology delta"
    return {"Cases": len(after), "Leaves": sum(row["Leaves"] for row in after.values()),
            "IEEELeaves": sum(row["IEEELeaves"] for row in after.values()),
            "OriginalMainExactOutsideRea009": True, "Rea009ChangedCases": len(observed),
            "Rea009ChangedLeaves": sum(len(d) for d in observed.values()), "RiderOrderExact": True}


def typed_leaf(value):
    """Keep capture types explicit; never parse IEEE values as Python floats."""
    if type(value) is str:
        if re.fullmatch(r"float:[0-9A-F]{8}|double:[0-9A-F]{16}", value):
            kind, bits = value.split(":")
            return {"Type": kind, "Bits": bits}
        if value.startswith("string:"):
            return {"Type": "string", "Value": value.removeprefix("string:")}
        if value.startswith("enum:"):
            return {"Type": "enum", "Value": value.removeprefix("enum:")}
        if value.startswith("char:"):
            return {"Type": "char", "Codepoint": value.removeprefix("char:")}
        if value.startswith("decimal:"):
            return {"Type": "decimal", "Bits": value.removeprefix("decimal:")}
        return {"Type": "untyped-string", "Value": value}
    kinds = {type(None): "null", bool: "boolean", int: "integer"}
    assert type(value) in kinds, f"Unsupported/untyped capture leaf: {value!r}"
    return {"Type": kinds[type(value)], "Value": value}


def read_leaves(directory, phase, case, manifest_row):
    path = Path(directory) / f"{phase}.json.{case.replace('/', '_')}.json"
    assert path.is_file(), f"Missing complete raw capture: {path} (capture with --raw)"
    raw = path.read_bytes()
    assert hashlib.sha256(raw).hexdigest().upper() == manifest_row["SHA256"], f"Raw capture hash mismatch: {path}"
    leaves = json.loads(raw)
    assert len(leaves) == manifest_row["Leaves"], f"Raw leaf coverage mismatch: {path}"
    ieee_count = sum(type(value) is str and value.startswith(("float:", "double:")) for value in leaves.values())
    assert ieee_count == manifest_row["IEEELeaves"], f"Raw IEEE coverage mismatch: {path}"
    return leaves


def divergence_map(windows, ubuntu, phase):
    """Return every differing case/path with both exact typed values, including missing paths."""
    w_manifest = read_manifest(windows, phase)
    u_manifest = read_manifest(ubuntu, phase)
    assert w_manifest.keys() == u_manifest.keys(), f"Platform case coverage differs: {phase}"
    result = {}
    missing = {"Type": "missing"}
    for case in sorted(w_manifest):
        left = read_leaves(windows, phase, case, w_manifest[case])
        right = read_leaves(ubuntu, phase, case, u_manifest[case])
        differences = {}
        for path in sorted(left.keys() | right.keys()):
            w = typed_leaf(left[path]) if path in left else missing
            u = typed_leaf(right[path]) if path in right else missing
            if w != u:
                differences[path] = {"Windows": w, "Ubuntu": u}
        if differences:
            result[case] = differences
    return result


def compare_divergences(before, after):
    assert before == after, "Cross-platform divergence map changed (introduced, removed or changed case/path/type/value/bits)"
    return {"CrossPlatformDivergencesUnchanged": True, "DivergentCases": len(before),
            "DivergentLeaves": sum(len(paths) for paths in before.values())}


def compare_platforms(directory):
    root = Path(directory)
    windows = root / "determinism-windows-latest/contested-performance"
    ubuntu = root / "determinism-ubuntu-latest/contested-performance"
    evidence = {"Windows": compare(windows), "Ubuntu": compare(ubuntu)}
    before = divergence_map(windows, ubuntu, "before")
    after = divergence_map(windows, ubuntu, "after")
    # Retain both full maps even on map failure; no whitelist or truncated leaf samples.
    report = {**evidence, "BeforeDivergences": before, "AfterDivergences": after}
    (root / "contested-performance-comparison.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8")
    evidence.update(compare_divergences(before, after))
    return evidence


if __name__ == "__main__":
    if len(sys.argv) == 2:
        evidence = compare(sys.argv[1])
    else:
        assert len(sys.argv) == 3 and sys.argv[2] == "--platforms", "Usage: compare.py DIRECTORY [--platforms]"
        evidence = compare_platforms(sys.argv[1])
    print(json.dumps(evidence, indent=2))
