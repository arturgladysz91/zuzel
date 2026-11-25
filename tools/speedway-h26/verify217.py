
"""Check matched initial states, delayed controls and unchanged forces in the #217 MODEL audit."""
import gzip
import json
import sys
from collections import Counter
from pathlib import Path

from verify215 import add, cross, norm, scale, sub, tyre_force, vec


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def check_raw(path, seed, protocol):
    with gzip.open(path, "rt", encoding="utf-8") as stream:
        run = json.load(stream)
    rows = run["Trace"]
    assert run["Summary"]["Rollout"]["Initial"] == seed["State"]
    assert len(rows) == run["Summary"]["Rollout"]["Steps"]
    policy = run["Summary"]["Policy"]
    delay = protocol["ReactionSeconds216"]
    queue = list(seed["Pending"]) + [
        {"Time": d["Time"] + delay, "Command": d["Chosen"]}
        for d in run["Decisions"] if d["Chosen"] is not None
    ]
    next_event = seed["State"]["Next"]
    active = seed["State"]["Command"]
    prior = seed["State"]
    mass = protocol["Geometry"]["Mass"]
    min_verification = None
    impulse = angular = 0
    for row in rows:
        before, after = row["Before"], row["After"]
        dt = after["Time"] - before["Time"]
        while next_event < len(queue) and queue[next_event]["Time"] <= before["Time"] + 5e-12:
            active = queue[next_event]["Command"]
            next_event += 1
        assert before["Next"] == next_event and before["Command"] == after["Command"] == active
        if next_event < len(queue):
            assert after["Time"] <= queue[next_event]["Time"] + 1e-11
        assert all(before.get(k) == prior.get(k) for k in before.keys() | prior.keys()
                   if k not in {"Next", "Command"})
        assert 0 < dt <= protocol["ExecutionStep"] + 1e-11
        prior = after
        f = row["Forces"]
        force = vec(f["Drag"])
        torque = (0, 0, 0)
        for n in ("Front", "Rear"):
            tyre = f[n]
            applied = tyre_force(tyre)
            assert tyre["Normal"] >= 0
            if tyre["Patch"]["Gap"] >= 0:
                assert norm(applied) == 0
            force = add(force, applied)
            torque = add(torque, cross(vec(tyre["Patch"]["Arm"]), applied))
        if foot := f.get("LeftFoot"):
            applied = vec(foot["Contact"]["Force"])
            if foot["Contact"]["Gap"] >= 0:
                assert norm(applied) == 0
            force = add(force, applied)
            torque = add(torque, cross(vec(foot["Arm"]), applied))
        expected = scale(add(force, (0, 0, -mass * protocol["Gravity216"])), dt)
        impulse = max(impulse, norm(sub(sub(vec(after["Momentum"]), vec(before["Momentum"])), expected)))
        angular = max(angular, norm(sub(sub(vec(after["Angular"]), vec(before["Angular"])), scale(torque, dt))))
    assert impulse < 1e-8 and angular < 1e-8
    if policy == "Predictive":
        verified = [d["Preview"]["PredictedMinimumSwept"] for d in run["Decisions"] if d["Chosen"] is not None]
        if verified:
            min_verification = min(verified)
            assert min_verification >= protocol["RequiredClearance"]
    return {"acceptedSteps": len(rows), "linearImpulseError": impulse, "angularImpulseError": angular,
            "minimumVerifiedPredictionClearance": min_verification}


def main():
    root = Path(sys.argv[1])
    output = Path(sys.argv[2])
    protocol = read(root / "protocol.json")
    seeds = {s["Name"]: s for s in read(root / "seeds.json")}
    outcomes = read(root / "outcomes.json")
    baseline = {r["Seed"]: r for r in outcomes if r["Policy"] == "Baseline"}
    policies = set(protocol["LegacyAblations"]) | {"Predictive"}
    covered = Counter()
    for r in outcomes:
        seed = seeds[r["Seed"]]
        assert r["Rollout"]["Initial"] == seed["State"], (r["Seed"], r["Policy"])
        assert r["Rollout"]["InitialReference"] == seed["Reference"]
        covered[r["Seed"], r["Policy"]] += 1
        physics = r["Mechanical"]
        assert physics["MaxSteer"] <= protocol["ModelSteer"]["MaximumSteeringRadians"] + 1e-7
        assert physics["MaxSteerRate"] <= protocol["ModelSteer"]["SteeringRateRadiansPerSecond"] + 1e-7
        assert physics["MaxActiveForce"] <= protocol["Muscle"]["MaxForce"] + 1e-5
        assert physics["MaxActivePower"] <= protocol["Muscle"]["MaxPower"] + 1e-5
        assert physics["MaxTyreUtilization"] <= 1.000001 and physics["MaxAirborneContactForce"] == 0
        if first := r["First"]:
            assert first["Legacy"] == baseline[r["Seed"]]["First"]["Legacy"]
            if r["Policy"] == "NoEntryDriveCut" and first["Legacy"]["EntryCutApplied"]:
                assert first["Chosen"]["Bike"]["Drive"] == first["Legacy"]["PlannedDrive"]
                assert first["Chosen"]["Bike"]["SteeringRadians"] == first["Legacy"]["Command"]["Bike"]["SteeringRadians"]
                assert first["Chosen"]["Target"] == first["Legacy"]["Command"]["Target"]
                assert first["Chosen"].get("LeftFoot") == first["Legacy"]["Command"].get("LeftFoot")
    assert covered == Counter((seed, policy) for seed in seeds for policy in policies)
    assert any(s["Name"].endswith("-gate") and s["Pending"] for s in seeds.values())
    traces = {}
    for path in sorted(root.glob("trace-*.json.gz")):
        with gzip.open(path, "rt", encoding="utf-8") as handle:
            key = json.load(handle)["Summary"]["Seed"]
        traces[path.name] = check_raw(path, seeds[key], protocol)
    assert traces
    result = {"matchedSeeds": len(seeds), "rollouts": len(outcomes), "diagnosticOnly": True, "traces": traces}
    output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Verified {len(seeds)} matched states, {len(outcomes)} rollouts and "
          f"{sum(t['acceptedSteps'] for t in traces.values())} raw steps.")


if __name__ == "__main__":
    main()
