
"""Independently check signed work, 3D impulses, contact cones and the #216 budgets."""
import gzip
import json
import math
import sys
from pathlib import Path

from verify215 import add, cross, dot, norm, scale, sub, tyre_force, vec


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def contact_check(contact, friction):
    force = vec(contact["Force"])
    normal = vec(contact["Normal"])
    load = contact["NormalForce"]
    assert abs(norm(normal) - 1) < 1e-10
    assert load >= 0 and contact["Dissipation"] >= 0 and contact["StoredEnergy"] >= 0
    if contact["Gap"] >= 0:
        assert force == (0, 0, 0) and load == 0 and contact["StoredEnergy"] == 0
    tangent = sub(force, scale(normal, load))
    assert abs(dot(force, normal) - load) < 2e-8 and norm(tangent) <= friction * load + 2e-8


def check_trace(path, protocol):
    with gzip.open(path, "rt", encoding="utf-8") as stream:
        trial = json.load(stream)
    summary = trial["Summary"]
    rows = trial.get("Trace", trial.get("Steps"))
    assert rows is not None and len(rows) == summary["Steps"]
    mass = protocol["Geometry"]["Mass"]
    gravity = 0 if "free-flight" in path.name else protocol["Gravity216"]
    support_friction = 1.35 if "high-friction" in path.name else .65 if "low-friction" in path.name else 1
    ground_friction = .48 if "high-boot-friction" in path.name else .20 if "low-boot-friction" in path.name else .32
    statistics = dict.fromkeys(("linearError", "angularError", "relativeLegError", "energyRowError",
                                "peakEnergyRemainder", "peakActiveForce", "peakActivePower"), 0.0)
    stats = statistics
    previous = None
    energy = 0.0
    wheel_events = boot_events = 0
    for row in rows:
        before, after = row["Before"], row["After"]
        dt = after["Time"] - before["Time"]
        assert 0 < dt <= .005001 and row["Horizontal"]["End"] == after["Time"]
        if previous is not None:
            assert before["Time"] == previous["Time"]
            assert all(v == previous.get(k) for k, v in before.items() if k not in {"Command", "Next"})
        previous = after
        result = row["Horizontal"]
        f = row["Forces"]
        assert before["Command"] == after["Command"] and vec(result["CollisionImpulse"]) == (0, 0, 0)
        assert result["CollisionYawImpulse"] == 0
        force = vec(f["Drag"])
        torque = (0, 0, 0)
        loss = sum(f[k] for k in ("EngineLoss", "TransmissionLoss", "BearingLoss", "SlideLoss"))
        for name in ("Front", "Rear"):
            tyre = f[name]
            load = tyre_force(tyre)
            if tyre["Patch"]["Gap"] >= 0:
                assert load == (0, 0, 0)
            assert tyre["Normal"] >= 0 and tyre["NormalLoss"] >= 0
            force = add(force, load)
            torque = add(torque, cross(vec(tyre["Patch"]["Arm"]), load))
            loss += tyre["NormalLoss"]
        for name, coefficient in (("Seat", .56), ("LeftPeg", .75), ("RightPeg", .75), ("LeftGrip", .56), ("RightGrip", .56)):
            pad = f["PassiveSupport"][name]["Contact"]
            contact_check(pad, coefficient * support_friction)
            loss += pad["Dissipation"]
        active = norm(vec(f["Rider"]))
        absolute_power = abs(row["ActuatorPower"])
        if foot := f.get("LeftFoot"):
            boot = foot["Contact"]
            contact_check(boot, ground_friction)
            force = add(force, vec(boot["Force"]))
            torque = add(torque, cross(vec(foot["Arm"]), vec(boot["Force"])))
            assert abs(boot["Gap"] - (row["Midpoint"]["Center"]["Z"] + foot["Arm"]["Z"])) < 1e-10
            assert norm(sub(vec(row["Plan"]["Foot"]["Force"]), (*vec(boot["Force"])[:2], 0))) < 1e-10
            loss += boot["Dissipation"] + foot["StopLoss"]
            active += abs(foot["MuscleForce"])
            absolute_power = foot["AbsoluteMusclePower"]
            q0 = before.get("Leg", {}).get("Momentum", 0)
            q1 = after.get("Leg", {}).get("Momentum", 0)
            stats["relativeLegError"] = max(stats["relativeLegError"], abs(q1 - q0 - dt * foot["MomentumDerivative"]))
            assert abs(row["ActuatorPower"] - foot["MusclePower"]) < 1e-10
        total = add(force, (0, 0, -mass * gravity))
        stats["linearError"] = max(stats["linearError"],
                                   norm(sub(sub(vec(after["Momentum"]), vec(before["Momentum"])), scale(total, dt))))
        stats["angularError"] = max(stats["angularError"],
                                    norm(sub(sub(vec(after["Angular"]), vec(before["Angular"])), scale(torque, dt))))
        independently_summed_work = (f["EnginePower"] + row["ActuatorPower"] + f["SteeringPower"] - loss) * dt
        energy_change = row["EnergyAfter"] - row["EnergyBefore"]
        residual = energy_change - independently_summed_work
        stats["energyRowError"] = max(stats["energyRowError"], abs(residual - row["EnergyResidual"]))
        energy += residual
        stats["peakEnergyRemainder"] = max(stats["peakEnergyRemainder"], abs(energy))
        stats["peakActiveForce"] = max(stats["peakActiveForce"], active)
        stats["peakActivePower"] = max(stats["peakActivePower"], absolute_power)
        for event in row["Contacts"]:
            assert 0 <= event["Seconds"] <= dt + 1e-12
            if event.get("Foot"):
                boot_events += 1
            else:
                wheel_events += 1
    for k, tolerance in (("linearError", 1e-8), ("angularError", 1e-8), ("relativeLegError", 1e-8),
                         ("energyRowError", 1e-8), ("peakActiveForce", 700.00001), ("peakActivePower", 350.00001)):
        assert stats[k] <= tolerance, (path.name, k, stats[k])
    expect = summary.get("EnergyDefect", summary.get("EnergyRemainder"))
    assert math.isclose(energy, expect, abs_tol=1e-7), path.name
    return dict(stats, steps=len(rows), wheelEvents=wheel_events, bootEvents=boot_events)


def main():
    root, receipt = map(Path, sys.argv[1:])
    protocol = read(root / "protocol.json")
    traces = {p.name: check_trace(p, protocol) for p in sorted(root.glob("*.json.gz"))}
    assert traces and read(root / "mechanics.json")["Pass"]
    search = read(root / "corner-search.json")
    counts = {}
    for row in search["Attempts"]:
        kph = str(row["RequestedKph"])
        counts[kph] = counts.get(kph, 0) + 1
        assert row["QualifiedCorner"] == (row["Stop"] == "completed corner")
        if row["QualifiedCorner"]:
            assert row["Entry"] and row["Exit"] and row["ExitSpeedKph"] is not None
            assert row["FinalReference"] >= search["Visit"]["Exit"] + 8
            assert row["MaxContinuousSlide"] >= .12 and row["MinimumSwept"] >= .50
    assert sorted(map(float, counts)) == [80, 90, 100, 110, 120]
    assert max(counts.values()) <= protocol["MaximumProposalsPerSpeed"]
    receipt.write_text(json.dumps({"traces": traces, "proposalsPerSpeed": counts}, indent=2) + "\n",
                       encoding="utf-8", newline="\n")
    print(f"Validated {len(traces)} traces, {sum(t['steps'] for t in traces.values())} accepted steps "
          f"and {sum(counts.values())} corner proposals.")


if __name__ == "__main__":
    main()
