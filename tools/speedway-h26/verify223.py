"""Check the Study 223 evidence bundle and write committed-file digests."""

import argparse
import hashlib
import math
from pathlib import Path

from verify219 import regression
from verify222 import read, tape, verified, write


def check_results(root):
    refs = read(root / "reference-continuity.json")
    assert refs["NativeWorlds"] == 0 and len(refs["Replayed"]) == 9
    assert max(r["PositionDefect"] for r in refs["Replayed"]) < 1e-8
    cycle = refs["Lap"]
    assert math.isclose(cycle["FirstSeconds"], 17.901, abs_tol=.001)
    assert math.isclose(cycle["Last"]["Station"], 348.24991, abs_tol=.0001)
    assert "chassis-ground" in cycle["Stop"]
    summary = {}
    for name in ("A", "B", "C", "D"):
        stage = read(root / f"stage-{name}.json.gz")
        screening = stage["Screening"]
        assert stage["Stage"] == name and stage["NativeWorlds"] == 0 and len(screening) == 64
        assert len({p["Plan"]["Id"] for p in screening}) == 64
        assert all(p["IntegratedSteps"] > 0 for p in screening)
        assert all(p["Exact"] for p in stage["Verified"])
        summary[name] = {"screened": len(screening), "exact": len(stage["Verified"]),
                         "linked": sum(p["Score"]["Linked"] for p in screening)}
        if name == "B":
            assert {p["Plan"]["StraightBand"] for p in screening} == {95, 100, 105, 110, 115, 120}
    stage_e = read(root / "periodic-search.json.gz")
    assert len(stage_e["Screening"]) == 64 and stage_e["NativeWorlds"] == 0
    assert all(p["Exact"] for p in stage_e["Exact"])
    closure = read(root / "periodic-closure.json.gz")
    assert len(closure["Trials"]) == 10 and closure["NativeWorlds"] == 0
    local = read(root / "periodic-closure-local.json.gz")
    assert len(local["Trials"]) == 6 and local["NativeWorlds"] == 0
    repair = read(root / "shoot-repair.json.gz")
    assert len(repair["Tests"]) == 12 and repair["NativeWorlds"] == 0
    assert all(x["FinalDefect"] <= x["InitialDefect"] + 1e-8 for x in repair["Tests"] if x["InitialDefect"] >= 0)
    return summary


def check_feedback(root):
    freeze = read(root / "feedback-freeze.json")
    q = read(root / "feedback-qualification.json")
    assert q["NativeWorlds"] == 0 and q["Policy"] == freeze["Policy"]
    assert len(freeze["Holdouts"]) == 4 and len(q["Trials"]) == 8
    assert {r["Name"] for r in q["Trials"] if r["Type"] == "geometry"} == {"nominal", "compact", "large"}
    for case in q["Trials"]:
        run = case["Outcome"]
        assert 0 <= run["Missed50"] <= run["Decisions"]
        assert run["P50"] <= run["P95"] <= run["P99"] <= run["PMax"]
        assert run["SpatialReferenceCacheHits"] + run["SpatialReferenceCacheMisses"] == run["Decisions"]
        assert run["CachedCommands"] == 0 and run["ControllerCpu"] >= 0 and run["IntegrationCpu"] >= 0
        assert (run["Ground"] > 0 and run["Boundary"] >= .5) or not run["Score"]["Linked"]
    names = ["nominal"] + [f"holdout-{i}" for i in range(1, 5)]
    for name in names:
        data = read(root / f"feedback-{name}-decisions.json.gz")
        assert data["Policy"] == freeze["Policy"]
        tape(data["Tape"])
        timing = data["condition"]["AdditionalDelaySeconds"] + freeze["ReactionSeconds216"]
        assert all(math.isclose(p["Time"] - p["Requested"], timing, abs_tol=1e-9) for p in data["Tape"])
    return q


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("root", type=Path)
    args = cli.parse_args()
    root = args.root
    stages = check_results(root)
    feedback = check_feedback(root)
    total_parent, failures_parent = regression(root / "parent.trx.gz")
    total_current, failures_current = regression(root / "current.trx.gz")
    assert total_parent == total_current and total_parent["passed"] == 50 and total_parent["failed"] == 10
    assert failures_parent == failures_current
    assert all("Box2D construction forbidden" in failed for failed in failures_current.values())
    proof = read(root / "independent-proof.json")
    assert proof["NativeWorlds"] == 0
    outcomes = {}
    for record in proof["Retested"]:
        assert record["PositionDefect"] < .002 and record["LinearMomentumDefect"] <= .12
        assert record["AngularMomentumDefect"] <= .12
        assert record["EnergyError"] < 1
        path = root / (record["Name"] + ".json.gz")
        outcomes[record["Name"]] = verified(path)
    assert {"shoot-selected", "speed-highest", "periodic-selected", "periodic-closure-selected", "feedback-nominal"} <= outcomes.keys()
    frozen = [r["Outcome"] for r in feedback["Trials"] if r["Type"] == "frozen"]
    write(root / "verification.json", {"stages": stages, "replays": outcomes,
        "parent": total_parent, "current": total_current, "frozenRuns": len(frozen), "frozenLinked": sum(r["Score"]["Linked"] for r in frozen)})
    manifest = {}
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.name != "manifest.json":
            with path.open("rb") as stream:
                manifest[path.relative_to(root).as_posix()] = {
                    "bytes": path.stat().st_size, "sha256": hashlib.file_digest(stream, "sha256").hexdigest()}
    write(root / "manifest.json", manifest)
    print(f"{len(outcomes)} trajectories, 320 staged proposals, 5 frozen feedback trials, {len(manifest)} file digests")


if __name__ == "__main__":
    main()
