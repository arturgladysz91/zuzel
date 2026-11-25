
"""Check the packaged non-native Study #219 evidence and write a file manifest."""
from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

BEND_LENGTH = 97.38937497138977


def read(path):
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as stream:
            return json.load(stream)
    return json.loads(path.read_text(encoding="utf-8"))


def check_log(path):
    evidence = read(path)
    rows = evidence["States"]
    commands = evidence["Commands"]
    result = evidence["Result"]
    assert len(rows) == result["ActualSteps"]
    assert all(x["Requested"] <= y["Requested"] for x, y in zip(commands, commands[1:]))
    assert commands[:len(evidence["EarlierCommands"])] == evidence["EarlierCommands"]
    assert all(.04999999 <= a["Time"] - a["Requested"] <= .06500001 for a in commands)
    state = evidence["OriginalSeed"]
    accepted_steps = 0
    energy = 0.0
    energy_peak = 0.0
    for row in rows:
        after = row["State"]
        assert 0 < after["Time"] - state["Time"] <= .002500001
        assert row["SweptEdge"] >= .5 and row["SweptGround"] > 0
        assert min(row["FrontNormal"], row["RearNormal"], row["FootNormal"]) >= 0
        assert max(row["FrontUse"], row["RearUse"]) <= 1.000001
        assert 0 <= row["ActiveDrive"] <= 1
        assert math.isclose(row["ActiveDrive"], after["Command"]["Bike"]["Drive"], abs_tol=1e-8)
        assert math.sqrt(sum(x * x for x in row["RiderForce"].values())) <= 700.00001
        assert abs(row["ActivePower"]) <= 350.00001
        energy += row["LedgerResidual"]
        energy_peak = max(energy_peak, abs(energy))
        accepted_steps += 1
        state = after
    if rows:
        assert abs(rows[-1]["Distance"] - result["Distance"]) < 1e-8
    if result["StableTwenty"]:
        assert result["BalancedBend"] and result["Distance"] >= BEND_LENGTH + 20
        assert abs(result["TwentyRoll"]) <= .22 and abs(result["TwentyYawRate"]) <= .28
        assert result["TwentyKph"] >= result["ExitKph"] + .5
    return {"steps": accepted_steps, "commands": len(commands), "energyPeakJoules": energy_peak,
            "balancedBend": result["BalancedBend"], "stableTwenty": result["StableTwenty"]}


def regression(path):
    doc = ET.fromstring(gzip.decompress(path.read_bytes()))
    ns = {"v": doc.tag.split("}")[0][1:]}
    counters = doc.find(".//v:Counters", ns)
    failures = {}
    for row in doc.findall(".//v:UnitTestResult[@outcome='Failed']", ns):
        msg = row.find(".//v:Message", ns).text
        failures[row.get("testName")] = msg
    return {k: int(counters.get(k)) for k in ("total", "passed", "failed")}, failures


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    args = parser.parse_args()
    root = args.root
    baseline = read(root / "baseline.json")
    assert baseline["NativeWorlds"] == 0
    assert len(baseline["Held"]) == 4 and all(a["Recovered"] for a in baseline["Held"])
    assert [a["Summary"]["Balanced"] for a in baseline["Arcs"]] == [True, True, True, False, False, False]
    assert baseline["Straight"]["Stable"]
    force = read(root / "force-maps.json.gz")
    active = force["Cells"]
    assert len(active) == 15960 and {r["SpeedKph"] for r in active} == set(range(60, 121, 10))
    assert all(0 <= x["RearUtilization"] <= 1.000001 and x["DissipatedWatts"] >= -1e-8 for x in active)
    assert len(read(root / "synthetic-circles.json.gz")) == 54
    assert len(read(root / "posture.json.gz")) == 64
    matched = read(root / "matched.json.gz")["Matches"]
    assert len(matched) == 60
    forks = {}
    for m in matched:
        group = m["Archive"], m["PhaseMeters"]
        if group not in forks:
            forks[group] = (m["Initial"], m["Pending"])
        assert forks[group] == (m["Initial"], m["Pending"])
        if m["Action"] is not None:
            assert m["Action"]["Bike"]["Drive"] == m["Drive"]
            assert m["Commands"][:len(m["Pending"])] == m["Pending"]
    assert len(forks) == 10
    whole = read(root / "whole.json")
    recourse = read(root / "recourse.json")
    freeze = read(root / "policy-freeze.json")
    final_freeze = read(root / "recourse-freeze.json")
    assert len(whole["Proposals"]) == len(recourse["Trials"]) == 60
    assert len(whole["Holdouts"]) == len(recourse["Holdouts"]) == 4
    assert [x["Name"] for x in recourse["Sensitivity"]] == ["compact", "nominal", "large"]
    assert freeze["FrozenBeforeHoldoutExecution"] and final_freeze["FrozenBeforeHoldouts"]
    assert whole["Replay"]["FinalPositionDifference"] < 1e-4
    for filename, outcomes in (
        ("whole-commands.json.gz", whole["Proposals"]),
        ("recourse-commands.json.gz", [x["Result"] for x in recourse["Trials"]]),
    ):
        histories = read(root / filename)
        assert len(histories) == len(outcomes) == 60
        for h, expected in zip(histories, outcomes):
            assert h["Result"] == expected
            assert len(h["Commands"]) == expected["Requests"]
            assert h["Commands"][:len(h["EarlierCommands"])] == h["EarlierCommands"]
    straight = read(root / "straight-comparison.json")
    assert straight["ComparisonSeconds"] == 2.10 and len(straight["Final"]) == 5
    for extension in straight["Final"]:
        name = extension["Name"]
        plan = read(root / f"recourse-{name}.json.gz")
        suffix = read(root / f"qualified-straight-{name}.json.gz")
        assert plan["States"][-1]["State"] == suffix["OriginalSeed"]
        assert plan["Commands"] == suffix["EarlierCommands"]
        assert extension["Steps"] == len(suffix["States"])
    parent, failures = regression(root / "parent.trx.gz")
    current, other = regression(root / "current.trx.gz")
    assert parent == current and failures == other
    assert all("Box2D construction forbidden" in x for x in failures.values())
    series = [p for p in root.glob("*.json.gz") if p.name.startswith(
        ("whole-training", "reference-powered", "holdout-", "recourse-nominal", "recourse-holdout-", "qualified-"))]
    results = {
        "archives": len(baseline["Arcs"]), "baselineSteps": baseline["ActualSteps"], "forceCells": len(active),
        "syntheticStarts": 54, "postureTrials": 64, "matchedRequests": 60, "proposalCommandHistories": 120,
        "extendedStraightPasses": sum(r["Complete"] for r in straight["Final"]),
        "parent": parent, "current": current, "guardedNativeBlocked": sorted(failures),
        "series": {p.name: check_log(p) for p in sorted(series)},
    }
    (root / "verification.json").write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8", newline="\n")
    manifest = {
        f.name: {"bytes": f.stat().st_size, "sha256": hashlib.file_digest(f.open("rb"), "sha256").hexdigest()}
        for f in sorted(root.iterdir()) if f.is_file() and f.name != "manifest.json"
    }
    (root / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Verified {len(series)} trajectory archives and {len(manifest)} evidence digests.")


if __name__ == "__main__":
    main()
