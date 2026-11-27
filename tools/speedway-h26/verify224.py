"""Validate H43 MODEL receipts, independent replays and file digests."""

import argparse
import gzip
import hashlib
import math
import re
from pathlib import Path

from verify219 import regression
from verify222 import read, tape, verified, write


def references(root):
    history = read(root / "reference-phases.json")
    assert history["NativeWorlds"] == 0 and len(history["Motions"]) == 6
    assert all(m["PositionDifference"] < 1e-8 for m in history["Motions"])
    assert math.isclose(history["Motions"][0]["Station"], 348.2499121668, abs_tol=1e-8)
    assert math.isclose(history["Motions"][1]["Station"], 352.0348197018, abs_tol=1e-8)
    refs = read(root / "matched-speed-references.json")
    assert refs["NativeWorlds"] == 0 and len(refs["References"]) == 3
    assert all(m["PositionDifference"] < 1e-8 for m in refs["References"])
    for result in refs["References"]:
        assert result["Samples"][-1]["State"]["Station"] == result["Final"]["Distance"]
    seam = read(root / "matched-seam.json")
    assert len(seam) == 5
    assert all(r["Fork"] == seam[0]["Fork"] and r["Pending"] == seam[0]["Pending"] for r in seam)
    for name in ("matched-tracker.json", "queued-tracker.json"):
        cases = read(root / name)
        assert len(cases) == 5
        for case in cases:
            assert len(case["Result"]) == 12
            if name.startswith("queued"):
                assert case["Pending"]
            assert all(math.isclose(p["ResponseSeconds"], p["Seconds"] - .05, abs_tol=1e-10)
                       for p in case["Result"])


def stages(root):
    result = {}
    for name in ("line", "speed", "recourse"):
        stage = read(root / (name + "-search.json.gz"))
        assert stage["NativeWorlds"] == 0
        assert len(stage["Proposals"]) == 64 and len(stage["Exact"]) >= 5
        proposals = stage["Proposals"]
        exact = stage["Exact"]
        if name == "recourse":
            proposals = [item["Outcome"] for item in proposals]
            exact = [item["Score"] for item in exact]
        ids = {p["Id"] for p in proposals}
        assert len(ids) == 64 and all(p["Id"] in ids for p in exact)
        if name == "speed":
            bands = {p["Schedule"][0]["Route"]["Band"] for p in proposals}
            assert bands == {98, 100, 103, 106, 110, 115, 120}
        for p in exact:
            if p["Pass"]:
                assert p["Swept"] >= .5 and p["Hard"] > 0
                assert p["Link"]["PeakForce"] <= 700.00001 and p["Link"]["PeakPower"] <= 350.00001
        result[name] = {"screened": len(proposals), "verified": len(exact),
                        "fastestAcceptedSeconds": min((x["Seconds"] for x in exact if x["Pass"]), default=None),
                        "passed": sum(p["Pass"] for p in exact)}
    if (root / "support-certification.json").is_file():
        assist = read(root / "support-recourse.json")
        cert = read(root / "support-certification.json")
        assert len(assist["Proposals"]) == 3 and len(cert["Exact"]) == 2
        assert cert["InitialExact"] == assist["Exact"] and cert["NativeWorlds"] == 0
    return result


def feedback(root):
    freeze = read(root / "feedback-freeze.json")["frozen"]
    review = read(root / "feedback-qualification.json")
    assert review["NativeWorlds"] == 0 and freeze == review["frozen"]
    assert freeze["Parent"]["Id"] == 58 and len(freeze["Holdouts"]) == 4
    trials = review["Trials"]
    assert len(trials) == 5 and [r["Condition"] for r in trials[1:]] == freeze["Holdouts"]
    for i, case in enumerate(trials):
        label = "nominal" if i == 0 else f"holdout-{i}"
        event = read(root / f"feedback-{label}-decisions.json.gz")
        assert case["Condition"] == event["Condition"]
        if i == 0 and (root / "feedback-nominal-equivalence.json").is_file():
            pair = read(root / "feedback-nominal-equivalence.json")
            trace = root / pair["ArchivedTrace"]
            with gzip.open(trace, "rb") as stream:
                start = re.sub(rb'^\{"Name":"[^"]+",', b"{", stream.read(300), count=1)
                digest = hashlib.sha256(start)
                while block := stream.read(1 << 20):
                    digest.update(block)
            assert pair["ExactTelemetryExcludingDisplayName"] and digest.hexdigest() == pair["CanonicalMotionSha256"]
            motion = read(trace)
            assert motion["Seed"] == event["Root"]["State"] and motion["Commands"] == event["Tape"]
            assert motion["Rows"][-1]["State"] == event["Final"]["State"]
        tape(event["Tape"])
        assert case["H43Timing"]["Count"] == len(event["H43Decisions"])
        if case["CarrierRetained"] is not None:
            tape(event["RetainedTape"])
        for field in ("H43Timing", "H41Timing", "RetainedTiming"):
            if case[field] is not None:
                t = case[field]
                assert 0 <= t["Missed50"] <= t["Count"]
                if t["Count"]:
                    assert 0 <= t["P50"] <= t["P95"] <= t["P99"] <= t["Max"]
    return {"carrierLinked": sum(x["Parent"]["Linked"] for x in trials),
            "experimentalLinked": sum(x["H43"]["Link"]["Linked"] for x in trials),
            "retainedLinked": sum(x["CarrierRetained"] and x["CarrierRetained"]["Link"]["Linked"] or False for x in trials)}


def proofs(root):
    repeat = read(root / "replay-proof.json")
    assert repeat["NativeWorlds"] == 0 and len(repeat["Proofs"]) >= 5
    results = {}
    for item in repeat["Proofs"]:
        assert item["PositionError"] < .002
        assert item["LinearError"] <= .12 and item["AngularError"] <= .12
        assert item["Ground"] > 0 and item["Boundary"] >= .5
        assert item["MaximumMuscleForce"] <= 700.00001 and item["MaximumMusclePower"] <= 350.00001
        result = verified(root / (item["Name"] + ".json.gz"))
        assert item["Steps"] == result["rows"]
        assert item["Commands"] == result["commands"]
        assert math.isclose(item["MaximumEnergyError"], result["maximumEnergyErrorJ"], abs_tol=1e-7)
        results[item["Name"]] = result
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    root = parser.parse_args().root
    references(root)
    stage = stages(root)
    control = feedback(root)
    independent = proofs(root)
    prior, parent_failures = regression(root / "parent.trx.gz")
    current, current_failures = regression(root / "current.trx.gz")
    assert prior == current and current["passed"] == 50 and current["failed"] == 10
    assert parent_failures == current_failures
    assert all("Box2D construction forbidden" in failure for failure in current_failures.values())
    write(root / "verification.json", {"stages": stage, "feedback": control, "replays": independent,
                                       "parent": prior, "current": current})
    manifest = {}
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.name != "manifest.json":
            with path.open("rb") as stream:
                manifest[path.relative_to(root).as_posix()] = {
                    "bytes": path.stat().st_size, "sha256": hashlib.file_digest(stream, "sha256").hexdigest()}
    write(root / "manifest.json", manifest)
    print(f"{len(independent)} independently replayed trajectories, 192 staged proposals, {len(manifest)} evidence files.")


if __name__ == "__main__":
    main()
