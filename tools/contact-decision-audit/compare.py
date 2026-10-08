"""Exact enabled captures and a complete, typed account of the P1 correction."""
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

spec = importlib.util.spec_from_file_location(
    "contested_exact", Path(__file__).resolve().parents[1] / "contested-performance/compare.py")
exact = importlib.util.module_from_spec(spec)
spec.loader.exec_module(exact)


def read_leaves(directory, phase, case, row):
    path = Path(directory) / f"{phase}.json.{case.replace('/', '_')}.json.gz"
    raw = gzip.decompress(path.read_bytes())
    assert hashlib.sha256(raw).hexdigest().upper() == row["SHA256"], f"Capture hash mismatch: {path}"
    leaves = json.loads(raw)
    assert len(leaves) == row["Leaves"], f"Leaf coverage mismatch: {path}"
    assert sum(isinstance(v, str) and v.startswith(("float:", "double:")) for v in leaves.values()) == row["IEEELeaves"], path
    for value in leaves.values():
        exact.typed_leaf(value)
    return leaves


def differences(left_dir, left_phase, right_dir, right_phase):
    left = exact.read_manifest(left_dir, left_phase)
    right = exact.read_manifest(right_dir, right_phase)
    assert left.keys() == right.keys(), "Enabled capture case coverage changed"
    result = {}
    for case in sorted(left):
        a = read_leaves(left_dir, left_phase, case, left[case])
        b = read_leaves(right_dir, right_phase, case, right[case])
        delta = {}
        for path in sorted(a.keys() | b.keys()):
            av = exact.typed_leaf(a[path]) if path in a else {"Type": "missing"}
            bv = exact.typed_leaf(b[path]) if path in b else {"Type": "missing"}
            if av != bv:
                delta[path] = {"Before": av, "After": bv}
        if delta:
            result[case] = delta
    return result


def verify_order(directory, phase):
    manifest = exact.read_manifest(directory, phase)
    for case in manifest:
        if case.endswith("/False"):
            reverse = case.removesuffix("/False") + "/True"
            assert manifest[case] == manifest[reverse], f"Enabled rider-order dependence: {case}"
    return {"Cases": len(manifest), "Leaves": sum(r["Leaves"] for r in manifest.values()),
            "IEEELeaves": sum(r["IEEELeaves"] for r in manifest.values()), "RiderOrderExact": True}


def write_changes(path, changes):
    with gzip.open(path, "wt", encoding="utf-8", newline="\n") as stream:
        json.dump(changes, stream, indent=2)
        stream.write("\n")


def compare_platforms(directory):
    root = Path(directory)
    windows = root / "determinism-windows-latest/contact-decision"
    ubuntu = root / "determinism-ubuntu-latest/contact-decision"
    report = {"Windows": verify_order(windows, "after"), "Ubuntu": verify_order(ubuntu, "after")}
    # Both correction maps and any platform differences are retained without truncation.
    for name, left, lp, right, rp in (
        ("WindowsChanges", windows, "before", windows, "after"),
        ("UbuntuChanges", ubuntu, "before", ubuntu, "after"),
        ("BeforePlatformDifferences", windows, "before", ubuntu, "before"),
        ("AfterPlatformDifferences", windows, "after", ubuntu, "after"),
    ):
        delta = differences(left, lp, right, rp)
        write_changes(root / f"contact-decision-{name}.json.gz", delta)
        report[name] = {"Cases": len(delta), "Leaves": sum(len(v) for v in delta.values())}
    (root / "contact-decision-comparison.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    assert report["AfterPlatformDifferences"]["Cases"] == 0, "Enabled contact decision cross-platform drift"
    return report


if __name__ == "__main__":
    if len(sys.argv) == 5 and sys.argv[1] == "--changes":
        delta = differences(sys.argv[2], "before", sys.argv[3], "after")
        write_changes(sys.argv[4], delta)
        print(json.dumps({"ChangedCases": len(delta), "ChangedLeaves": sum(len(v) for v in delta.values())}))
    else:
        assert len(sys.argv) == 2, "Usage: compare.py CAPTURES | --changes BEFORE_DIR AFTER_DIR OUTPUT.gz"
        print(json.dumps(compare_platforms(sys.argv[1]), indent=2))
