
"""Verify manifest, command chronology, dynamics receipts and outcome scope for #218."""
from __future__ import annotations
import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

ENTRY = 672.5574998855591
BEND_LENGTH = 97.38937497138977
APPROACH = [-50, -40, -30, -20, -10, -5, 0, 5, 10, 15, 20]


def read(path):
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as stream:
            return json.load(stream)
    return json.loads(path.read_text(encoding="utf-8"))


def check_commands(tape):
    assert all(a["Requested"] < b["Requested"] for a, b in zip(tape, tape[1:]))
    assert all(math.isclose(c["Time"] - c["Requested"], .05, rel_tol=0, abs_tol=1e-11) for c in tape)
    assert all(0 <= c["Command"]["Bike"]["Drive"] <= 1 for c in tape)
    assert all(abs(c["Command"]["Bike"]["SteeringRadians"]) <= .610866 for c in tape)


def verify_trace(path):
    d = read(path)
    steps, tape, requests, summary = (d[k] for k in ("Steps", "Commands", "Requests", "Summary"))
    check_commands(tape)
    assert len(steps) == summary["Steps"] and len(steps) > 0
    assert len(requests) <= len(tape) <= len(requests) + 1
    assert max(abs(x["RollMoment"] - x["FrontRollMoment"] - x["RearRollMoment"] - x["LegRollMoment"])
               for x in steps) < 1e-8
    assert all(x["Ground"] > 0 and x["SweptGround"] > 0 and x["SweptBoundary"] >= .5 for x in steps)
    assert all(x["FrontNormal"] >= 0 and x["RearNormal"] >= 0 for x in steps)
    assert all(x["FrontUtilization"] <= 1.000001 and x["RearUtilization"] <= 1.000001 for x in steps)
    assert all(x["MuscleForce"] <= 700.00001 and x["MusclePower"] <= 350.00001 for x in steps)
    expected_state, active = d["Seed"]["Next"], d["Seed"]["Command"]
    before = d["Seed"]["Time"]
    energy = 0.0
    for x in steps:
        dt = x["Time"] - before
        assert 0 < dt <= .0025000001
        while expected_state < len(tape) and tape[expected_state]["Time"] <= before + 5e-12:
            active = tape[expected_state]["Command"]
            expected_state += 1
        assert x["NextActivation"] == expected_state
        assert active["Bike"]["Drive"] == x["ActiveDrive"]
        energy += x["EnergyResidual"]
        before = x["Time"]
    assert abs(energy) <= summary["MaxEnergyError"] + 1e-9
    assert math.isclose(steps[-1]["Distance"], summary["Finish"]["Reference"] - ENTRY, abs_tol=1e-9)
    assert len(d["Stations"]) == len(APPROACH)
    for target, station in zip(APPROACH, d["Stations"]):
        assert target == station["RequestedDistance"]
        if station["State"] is None:
            assert steps[-1]["Distance"] < target
            continue
        row = station["State"]
        assert row["Distance"] >= target
        assert station["Pending"] == tape[row["NextActivation"]:row["LatestRequest"] + 1]
    if summary["Balanced"]:
        assert all(summary[x] is not None for x in ("Approach", "Entry", "Apex", "Exit"))
        assert steps[-1]["Distance"] >= BEND_LENGTH + 8
        assert abs(steps[-1]["Roll"]) <= .22 and abs(steps[-1]["YawRate"]) <= .28
    return {"steps": len(steps), "balanced": summary["Balanced"],
            "exactRequests": sum(r["Reproduced"] for r in requests), "requests": len(requests)}


def verify_runout(path):
    d = read(path)
    check_commands(d["Commands"])
    if d["Stable"]:
        assert d["FinalKph"] >= d["InitialKph"] + 1
        expected = 2.10 if path.name.startswith(("settle-80-", "terminal-80-")) else 1.15
        assert math.isclose(d["FinalTime"] - d["InitialTime"], expected, abs_tol=1e-8)
    else:
        assert path.name.startswith(("exit-80-", "settle-80-", "terminal-80-"))
    assert d["MinimumHardGround"] > 0 and d["MinimumSwept"] >= .5
    return {"steps": len(d["Trace"]), "stable": d["Stable"]}


def test_counts(path):
    root = ET.fromstring(gzip.decompress(path.read_bytes()))
    namespace = root.tag.split("}")[0] + "}"
    result = root.find(".//" + namespace + "Counters")
    return {k: int(result.get(k)) for k in ("total", "passed", "failed")}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("data", type=Path)
    args = parser.parse_args()
    root = args.data
    traces = {p.name: verify_trace(p) for p in sorted(root.glob("archive-*.json.gz"))}
    assert [traces[name]["balanced"] for name in ("archive-77.json.gz", "archive-91.json.gz")] == [True, False]
    assert all(row["exactRequests"] == row["requests"] for row in traces.values())
    candidate_traces = [p for pattern in ("speed-*.json.gz", "wide-90-*.json.gz", "trim-90-*.json.gz",
                                         "exit-80-*.json.gz", "settle-80-*.json.gz")
                        for p in sorted(root.glob(pattern))]
    for p in candidate_traces:
        if not p.name.endswith("-runout.json.gz"):
            traces[p.name] = verify_trace(p)
    for p in sorted(root.glob("boot-*.json.gz")):
        traces[p.name] = verify_trace(p)
    for speed in (70, 80):
        parked = read(root / f"boot-{speed}-parked.json.gz")
        deployed = read(root / f"boot-{speed}-deployed.json.gz")
        assert parked["Seed"] == deployed["Seed"]
        assert parked["Strategy"] == deployed["Strategy"]
        assert all(c["Command"].get("LeftFoot", 0) == 0 for c in parked["Commands"])
    terminal_path = root / "terminal-80-runout.json.gz"
    runout = {p.name: verify_runout(p) for p in [*candidate_traces, terminal_path] if p.name.endswith("-runout.json.gz")}
    stages = {p.stem: read(p) for p in sorted(root.glob("stage-*.json"))}
    assert {d["TargetKph"] for d in stages.values()} == {70, 80, 90, 100}
    for stage in stages.values():
        settings = [row["Setup"] for row in stage["Outcomes"]]
        assert len(settings) == stage["RequestedProposals"] == 50
        assert {(p["Plan"]["Strategy"], p["Plan"]["OnsetMeters"]) for p in settings} == {
            (family, meters) for family in range(5) for meters in (10, 20, 30, 40, 50)}
        assert stage["SearchActualSteps"] == sum(x["Corner"]["Steps"] for x in stage["Outcomes"])
        for result in stage["Rechecked"]:
            assert result["Summary"]["Balanced"] and result["Runout"]["Stable"]
            assert len(result["Robustness"]) == 4 and result["Runout"]["FinalKph"] > result["Runout"]["InitialKph"]
    for name, count in [("recourse-90.json", 36), ("trim-90.json", 12)]:
        recourse = read(root / name)
        assert recourse["TargetKph"] == 90
        assert recourse["RequestedProposals"] == len(recourse["Outcomes"]) == count
    outcomes_80 = {o["Setup"]["Id"]: o["Corner"] for o in stages["stage-80"]["Outcomes"]}
    for report in ("exit-80.json", "settle-80.json"):
        for r in read(root / report)["Reviews"]:
            parent = outcomes_80[r["setup"]["Id"]]
            assert r["Summary"]["Entry"] == parent["Entry"] and r["Summary"]["Finish"] == parent["Finish"]
    terminal = read(root / "terminal-80.json")
    assert len(terminal["Outcomes"]) == 24
    parent = read(root / f"exit-80-proposal-{terminal['setup']['Id']}.json.gz")
    tape = parent["Commands"]
    assert read(terminal_path)["Commands"][:len(tape)] == tape

    diagnosis = read(root / "diagnosis.json.gz")
    assert diagnosis["NativeWorlds"] == 0
    assert len(diagnosis["Probes"]) >= 90
    assert all(p["InitialState"] == q["InitialState"]
               for p in diagnosis["Probes"] for q in diagnosis["Probes"]
               if (p["Archive"], p["Distance"]) == (q["Archive"], q["Distance"]))
    assert sum(p["Steps"] for p in diagnosis["Probes"]) == diagnosis["ActualProbeSteps"]
    for fork in diagnosis["Probes"]:
        check_commands(fork["Commands"])
        assert len(fork["Timeline"]) == fork["Steps"]
    tests = test_counts(root / "focused-218.trx.gz")
    assert tests == {"total": 30, "passed": 30, "failed": 0}
    result = {"traces": traces, "runouts": runout, "focusedTests": tests,
              "detailedApproachSteps": sum(r["steps"] for r in traces.values()),
              "matchedProbes": len(diagnosis["Probes"]), "probeSteps": diagnosis["ActualProbeSteps"]}
    verification = root / "verification.json"
    verification.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
    manifest = {p.name: {"bytes": p.stat().st_size, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                for p in sorted(root.iterdir()) if p.is_file() and p.name != "manifest.json"}
    (root / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
