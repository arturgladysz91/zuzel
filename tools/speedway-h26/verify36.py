
"""Independent H36 trajectory, actuator, work and momentum ledger checks."""
from __future__ import annotations

import gzip
import json
import math
import sys
from pathlib import Path

from verify215 import add, cross, norm, scale, sub, tyre_force, vec


def load(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8") as stream:
        return json.load(stream)


def trajectory(path, protocol):
    example = load(path)
    if "Commands" not in example:
        raise ValueError(f"{path.name} is missing a command tape")
    s = example["Summary"]
    queue = example["Commands"]
    state = example["Seed"]
    trace = example["Trace"]
    next_event = state["Next"]
    active = state["Command"]
    total_work = 0.0
    metrics = dict(linear=0., angular=0., energy=0., force=0., power=0., utilization=0., air=0.)
    mass = protocol["Base"]["Mass"]
    for row in trace:
        start, end = (row[key] for key in ("Before", "After"))
        dt = end["Time"] - start["Time"]
        assert dt > 0 and dt <= protocol["StepSeconds216"] + 1e-10
        assert all(state.get(key) == start.get(key) for key in state.keys() | start.keys()
                   if key not in {"Command", "Next"}), path.name
        while next_event < len(queue) and queue[next_event]["Time"] <= start["Time"] + 5e-12:
            active = queue[next_event]["Command"]
            next_event += 1
        assert start["Command"] == end["Command"] == active and start["Next"] == next_event
        if next_event < len(queue):
            assert end["Time"] <= queue[next_event]["Time"] + 1e-10
        state = end

        f = row["Forces"]
        force = add(vec(f["Drag"]), (0., 0., -mass * protocol["Gravity216"]))
        moment = (0., 0., 0.)
        loss = sum(f[key] for key in ("EngineLoss", "TransmissionLoss", "BearingLoss", "SlideLoss"))
        for tyre in (f["Front"], f["Rear"]):
            applied = tyre_force(tyre)
            force = add(force, applied)
            moment = add(moment, cross(vec(tyre["Patch"]["Arm"]), applied))
            loss += tyre["NormalLoss"]
            assert tyre["Normal"] >= 0
            if tyre["Patch"]["Gap"] >= 0:
                metrics["air"] = max(metrics["air"], norm(applied))
            if tyre["Capacity"] > 0:
                metrics["utilization"] = max(metrics["utilization"],
                    math.hypot(tyre["Longitudinal"], tyre["Lateral"]) / tyre["Capacity"])
        if support := f.get("PassiveSupport"):
            loss += sum(part["Contact"]["Dissipation"] for part in support.values())
        force_used = norm(vec(f["Rider"]))
        power_used = abs(row["ActuatorPower"])
        if boot := f.get("LeftFoot"):
            contact = boot["Contact"]
            applied = vec(contact["Force"])
            force = add(force, applied)
            moment = add(moment, cross(vec(boot["Arm"]), applied))
            loss += contact["Dissipation"] + boot["StopLoss"]
            assert contact["NormalForce"] >= 0
            if contact["Gap"] >= 0:
                metrics["air"] = max(metrics["air"], norm(applied))
            force_used += abs(boot["MuscleForce"])
            power_used = boot["AbsoluteMusclePower"]
        metrics["force"] = max(metrics["force"], force_used)
        metrics["power"] = max(metrics["power"], power_used)
        metrics["linear"] = max(metrics["linear"],
            norm(sub(sub(vec(end["Momentum"]), vec(start["Momentum"])), scale(force, dt))))
        metrics["angular"] = max(metrics["angular"],
            norm(sub(sub(vec(end["Angular"]), vec(start["Angular"])), scale(moment, dt))))
        predicted_work = (f["EnginePower"] - loss + row["ActuatorPower"] + f["SteeringPower"]) * dt
        residual = row["EnergyAfter"] - row["EnergyBefore"] - predicted_work
        assert abs(residual - row["EnergyResidual"]) < 1e-7
        total_work += residual
        metrics["energy"] = max(metrics["energy"], abs(total_work))
    assert metrics["linear"] < 1e-8 and metrics["angular"] < 1e-8
    assert metrics["force"] <= 700.00001 and metrics["power"] <= 350.00001
    assert metrics["air"] == 0 and metrics["utilization"] <= 1.000001
    assert len(trace) == s["Steps"] and math.isclose(metrics["energy"], s["MaxEnergyError"], abs_tol=1e-6)
    if s["Balanced"]:
        assert s["Approach"] and s["Entry"] and s["Apex"] and s["Exit"]
        assert s["MinimumSweep"] >= protocol["RequiredSweptMeters"] and s["GroundMargin"] > 0
        assert s["Finish"]["Reference"] >= protocol["visit"]["Exit"] + 8
        assert abs(s["Finish"]["Roll"]) <= .22 and abs(s["Finish"]["YawRate"]) <= .28
    return dict(metrics, steps=len(trace), balanced=s["Balanced"])


def main(root):
    protocol = load(root / "protocol-h36.json")
    geometry = load(root / "geometry-h36.json")
    curriculum = load(root / "curriculum-h36.json")
    assert len(geometry["Historical"]) == 470 and geometry["WorstOptimisticMeters"] <= 1e-6
    assert [row["RequestedKph"] for row in curriculum] == list(range(40, 40 + 10 * len(curriculum), 10))
    assert all(50 <= row["Proposals"] <= 100 or
               row["Proposals"] == 4 and (root / "trim-h36.json").is_file() or
               row["Proposals"] == 48 and (root / "prepare-h36.json").is_file()
               for row in curriculum)
    assert all(row["Outcome"] == "balanced at requested band" for row in curriculum[:-1])
    paths = ("trajectory-*", "highest-entry-*", "full-drive-*", "foot-pair-*",
             "pace-trajectory-*", "pace-highest-entry-*", "pace-full-drive-*", "pace-foot-pair-*",
             "trim-trajectory-*", "trim-highest-entry-*", "trim-full-drive-*",
             "prepare-trajectory-*", "prepare-highest-entry-*", "prepare-full-drive-*", "prepare-foot-pair-*")
    traces = {p.name: trajectory(p, protocol) for pattern in paths for p in sorted(root.glob(pattern + ".json.gz"))}
    report = {"verifiedTrajectories": traces, "verifiedSteps": sum(v["steps"] for v in traces.values()),
              "geometryCases": len(geometry["Historical"]), "denseGeometrySamples": geometry["DenseProjectionSamples"]}
    (root / "validation-h36.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Verified {len(traces)} command tapes and {report['verifiedSteps']} mechanical steps.")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
