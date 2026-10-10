"""Explicit target-change evidence; historical goldens and their default gates remain frozen."""
import gzip
import importlib.util
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]


def module(path):
    spec = importlib.util.spec_from_file_location("yield_" + Path(path).stem, ROOT / path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.suffix == ".gz":
        with gzip.open(path, "wt", encoding="utf-8", newline="\n") as stream:
            json.dump(value, stream, indent=2)
    else:
        path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def delta(left, right):
    return {p: {"BeforePresent": p in left, "Before": left.get(p),
                "AfterPresent": p in right, "After": right.get(p)}
            for p in sorted(left.keys() | right.keys())
            if p not in left or p not in right or left[p] != right[p]}


def has_target(leaves):
    return any(p.endswith(".PhysicalTarget.OffsetFromInnerReferenceMeters") and v is not None for p, v in leaves.items())


def verify_target_delta(case, left, right):
    changes = delta(left, right)
    if changes:
        disabled = "/False/" in case or "interactions-OFF" in case
        if disabled or not has_target(right):
            raise ValueError("Unrelated behavior changed: " + case)
    return changes


def contested(directory):
    exact = module("tools/contested-performance/compare.py")
    directory = Path(directory)
    before, after = exact.read_manifest(directory, "before"), exact.read_manifest(directory, "after")
    assert before.keys() == after.keys()
    changes = {}
    for case in before:
        left = exact.read_leaves(directory, "before", case, before[case])
        right = exact.read_leaves(directory, "after", case, after[case])
        row = verify_target_delta(case, left, right)
        if row:
            changes[case] = row
        if case.endswith("/False"):
            assert after[case] == after[case.removesuffix("/False") + "/True"], case
    write(directory / "minimal-yield-intentional-changes.json.gz", changes)
    return {"Cases": len(after), "ChangedCases": len(changes), "ChangedLeaves": sum(map(len, changes.values())),
            "UnrelatedCasesExact": True, "RiderOrderExact": True}


def flatten(value, path="root"):
    if isinstance(value, dict):
        return {p: v for key, item in value.items() for p, v in flatten(item, path + "." + key).items()}
    if isinstance(value, list):
        return {path + ".Count": len(value), **{p: v for i, item in enumerate(value)
                for p, v in flatten(item, f"{path}[{i}]").items()}}
    return {path: value}


def presentation(before_path, after_path, output):
    before, after = [json.loads(Path(p).read_text()) for p in (before_path, after_path)]
    assert before.keys() == after.keys()
    changes = {}
    for section, old in before.items():
        new = after[section]
        if not isinstance(old, list):
            assert old == new, section
            continue
        assert len(old) == len(new), section
        for index, (a, b) in enumerate(zip(old, new)):
            left, right = flatten(a), flatten(b)
            # New inert nullable API and work counter have no behavioral value.
            for path, value in right.items():
                if path not in left and ((path.endswith(".PhysicalTarget") and value is None)
                                         or (path.endswith(".OutwardTargetTrials") and value == 0)):
                    left[path] = value
            row = delta(left, right)
            if row:
                has_trials = any(p.endswith(".OutwardTargetTrials") and v > 0 for p, v in right.items())
                assert has_target(right) or has_trials, (section, index, "Unrelated presentation change")
                changes[f"{section}/{index}"] = row
    write(output, changes)
    return {"ChangedRows": len(changes), "ChangedLeaves": sum(map(len, changes.values())), "UnrelatedRowsExact": True}


def consequence_totals(heats):
    summed = ("VerifiedContacts", "AppliedRiders", "AppliedContacts", "ContactCrashes",
              "RepeatedOverlapSuppressions", "Deferred", "GeometryUnresolved")
    totals = {key: sum(row[key] for row in heats) for key in summed}
    totals.update(Heats=len(heats), ContactsPerHeat=totals["VerifiedContacts"] / len(heats) if heats else 0,
                  Classes={key: sum(row["Classes"].get(key, 0) for row in heats)
                           for key in ("Brush", "Disturbed", "LostRhythm", "MajorSave", "Crash")})
    return totals


def contact_production_changes(before, after, trials, sections, consequences=False):
    """Keep physical kernels exact; require observed target work for each changed production row."""
    assert before.keys() == after.keys()
    changes, expected = {}, set()
    for section in before:
        if section not in sections:
            if consequences and section == "Totals":
                continue
            assert before[section] == after[section], (section, "Physical contact kernel changed")
            continue
        old, new = before[section], after[section]
        assert len(old) == len(new), section
        identity = ("Scenario", "Seed", "Weather") if section == "Heats" else ("Name", "Seed")
        for left, right in zip(old, new):
            assert tuple(left[k] for k in identity) == tuple(right[k] for k in identity), section
            case = section + "/" + "/".join(str(right[k]) for k in identity)
            assert case not in expected, case
            expected.add(case)
            assert type(trials[case]) is int and trials[case] >= 0, case
            row = delta(flatten(left), flatten(right))
            if row:
                assert trials[case] > 0, (case, "Unrelated production contact change")
                changes[case] = row
    assert trials.keys() == expected
    if consequences:
        for report in (before, after):
            assert report["Totals"] == consequence_totals(report["Heats"]), "Contact totals do not match heat rows"
        row = delta(flatten(before["Totals"]), flatten(after["Totals"]))
        if row:
            assert any(case.startswith("Heats/") for case in changes), "Unexplained contact totals change"
            changes["Totals"] = row
    return changes


def contact_presentation(golden, capture, trials_path, output, consequences=False):
    before, after, trials = [json.loads(Path(p).read_text(encoding="utf-8-sig"))
                            for p in (golden, capture, trials_path)]
    sections = ("LegacyComparison", "Heats") if consequences else ("LegacyComparisons",)
    changes = contact_production_changes(before, after, trials, sections, consequences)
    write(output, changes)
    return {"ChangedRows": len(changes), "ChangedLeaves": sum(map(len, changes.values())),
            "UnrelatedRowsExact": True, "PhysicalContactKernelsExact": True}


def contested_platforms(root):
    exact = module("tools/contested-performance/compare.py")
    root = Path(root)
    windows = root / "determinism-windows-latest/contested-performance"
    ubuntu = root / "determinism-ubuntu-latest/contested-performance"
    report = {"Windows": contested(windows), "Ubuntu": contested(ubuntu)}
    old = exact.divergence_map(windows, ubuntu, "before")
    new = exact.divergence_map(windows, ubuntu, "after")
    # Unaffected cases retain their complete native geometry divergence map.
    # A target case must keep all float/domain values exact; only the already
    # native #55 double geometry remains platform-specific in configuration B.
    manifest = exact.read_manifest(windows, "after")
    for case in old.keys() | new.keys():
        leaves = exact.read_leaves(windows, "after", case, manifest[case])
        if not has_target(leaves):
            assert old.get(case) == new.get(case), case
        else:
            for path, row in new.get(case, {}).items():
                assert row["Windows"]["Type"] == row["Ubuntu"]["Type"] == "double", (case, path)
    write(root / "minimal-yield-platform-differences.json.gz", {"Before": old, "After": new})
    write(root / "minimal-yield-compatibility.json", report)
    return report


def readiness(root):
    root = Path(root)
    captures = [json.loads((root / phase / "manifest.json").read_text()) for phase in ("before", "after")]
    expected = {(track, 19, cfg) for track in ("balanced-example", "balanced-motoarena") for cfg in "ABC"}
    rows = []
    for capture in captures:
        assert capture["Completed"] and capture["Mode"] == "smoke"
        assert {(r["Id"], r["Seed"], r["Configuration"]) for r in capture["Cases"]} == expected
    for left, right in zip(captures[0]["Cases"], captures[1]["Cases"]):
        assert (left["Id"], left["Configuration"]) == (right["Id"], right["Configuration"])
        assert not right.get("Failed") and right["ReversedExact"] and right["ObserverExact"]
        changed = any(left[k] != right[k] for k in ("FinalHash", "BehaviorHash"))
        if changed:
            assert right["Configuration"] != "A", "Feature-OFF changed"
            with gzip.open(root / "after" / f"case-{right['Id']}-19-{right['Configuration']}.json.gz") as stream:
                raw = json.load(stream)
            assert not raw["Violations"] and sum(w.get("OutwardTargetTrials", 0) for w in raw["Work"]) > 0
        rows.append({"Id": right["Id"], "Configuration": right["Configuration"], "Changed": changed,
                     "BeforeFinalHash": left["FinalHash"], "AfterFinalHash": right["FinalHash"],
                     "BeforeBehaviorHash": left["BehaviorHash"], "AfterBehaviorHash": right["BehaviorHash"]})
    write(root / "minimal-yield-smoke-changes.json", rows)
    return rows


def rea001(windows, ubuntu, report_path):
    old = module("tools/rea001-safety-audit/compare.py")
    old.compare(windows)
    old.compare(ubuntu)
    report = {}
    for cfg in "BC":
        left, right = old.read(windows, "after", cfg), old.read(ubuntu, "after", cfg)
        assert left["FinalHash"] == right["FinalHash"], cfg
        name = f"trace-outside-7-{cfg}.json.gz"
        with gzip.open(Path(windows) / f"after-{cfg}" / name) as stream:
            a = json.load(stream)
        with gzip.open(Path(ubuntu) / f"after-{cfg}" / name) as stream:
            b = json.load(stream)
        changes = old.divergence_map(a, b)
        if cfg == "C":
            assert not changes and left["BehaviorHash"] == right["BehaviorHash"]
        else:
            assert has_target(a)
            assert all(row["Windows"]["Type"] == row["Ubuntu"]["Type"] == "double" for row in changes.values())
        report[cfg] = {"FinalStateExact": True, "ObservedNativeGeometryDifferences": changes,
                       "SummaryHashes": [left["BehaviorHash"], right["BehaviorHash"]]}
    write(report_path, report)
    return {cfg: {"FinalStateExact": True, "DivergentLeaves": len(row["ObservedNativeGeometryDifferences"])} for cfg, row in report.items()}


def rea009(windows, ubuntu, report_path):
    old = module("tools/rea009-handoff-audit/compare.py")
    exact = module("tools/rea001-safety-audit/compare.py")
    report = {}
    for scenario in ("three-squeeze", "four-close-regain"):
        for cfg in "ABC":
            name = f"after-{scenario}-19-{cfg}"
            left, right = [old.require_repro(Path(root) / name) for root in (windows, ubuntu)]
            assert all((r["Id"], r["Seed"], r["Configuration"]) == (scenario, 19, cfg) for r in (left, right))
            assert left["FinalHash"] == right["FinalHash"], name
            traces = []
            for root in (windows, ubuntu):
                with gzip.open(Path(root) / name / f"trace-{scenario}-19-{cfg}.json.gz") as stream:
                    traces.append(json.load(stream))
            changes = exact.divergence_map(*traces)
            if cfg in "AC":
                assert not changes and left["BehaviorHash"] == right["BehaviorHash"], name
            else:
                assert has_target(traces[0]), name
                assert all(row["Windows"]["Type"] == row["Ubuntu"]["Type"] == "double" for row in changes.values()), name
            report[name] = {"FinalStateExact": True, "ObservedNativeGeometryDifferences": changes,
                            "SummaryHashes": [left["BehaviorHash"], right["BehaviorHash"]]}
    frozen = json.loads((ROOT / "tests/fixtures/rea009-native-b-portability.json").read_text())
    write(report_path, {"HistoricalNativeB": frozen, "MinimalYield": report})
    return {name: {"FinalStateExact": True, "DivergentLeaves": len(row["ObservedNativeGeometryDifferences"])}
            for name, row in report.items()}


if __name__ == "__main__":
    mode, *args = sys.argv[1:]
    print(json.dumps({"contested": contested, "platforms": contested_platforms, "readiness": readiness,
                      "rea001": rea001, "rea009": rea009, "presentation": presentation}[mode](*args), indent=2))
