"""Independently check the H33-U clock, projected forces, momentum and work."""

import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path


def read(path):
    with (gzip.open(path, "rt", encoding="utf-8") if path.suffix == ".gz"
          else path.open(encoding="utf-8-sig")) as stream:
        return json.load(stream)


def vec(value):
    return complex(value["X"], value["Y"])


def dot(a, b):
    return (a.conjugate() * b).real


def axis(angle):
    return complex(math.cos(angle), math.sin(angle))


def energy(state, geometry, gravity):
    mass, rider = geometry["Mass"], geometry["RiderMass"]
    reduced = rider * (1 - rider / mass)
    r = vec(state["Rider"]["Position"])
    v = vec(state["Rider"]["Velocity"])
    pc, pw = state["CrankMomentum"], state["WheelMomentum"]
    pitch_i = geometry["PitchCentral"] + reduced * abs(r) ** 2
    yaw_i = geometry["YawCentral"] + reduced * (r * axis(state["Pitch"])).real ** 2
    pitch = (state["PitchMomentum"] + pc + pw - reduced * dot(1j * r, v)) / pitch_i
    return .5 * (mass * abs(vec(state["Plane"]["Velocity"])) ** 2 + state["VerticalMomentum"] ** 2 / mass
                 + geometry["PitchCentral"] * pitch ** 2 + reduced * abs(v + 1j * r * pitch) ** 2
                 + state["Plane"]["YawMomentum"] ** 2 / yaw_i
                 + pc ** 2 / geometry["CrankInertia"] + pw ** 2 / geometry["WheelInertia"]) + mass * gravity * state["Height"]


def trajectory(path, protocol):
    run = read(path)
    if "Summary" not in run:
        return None
    summary, rows = run["Summary"], run["Trace"]
    assert summary["Steps"] == len(rows) and rows, path
    g = protocol["Geometry"]
    mass, rider = g["Mass"], g["RiderMass"]
    gravity = 0 if summary["Case"] in {"free-flight", "free-yaw"} else protocol["Gravity"]
    evidence = dict(steps=len(rows), impacts=0, clockDefect=0., momentumDefect=0., yawDefect=0.,
                    projectedArmDefect=0., maxAirForce=0., maxNormalWork=0., minGap=0.,
                    maxRecordedEnergyDifference=0., maxAccumulatedWorkDefect=0., finalWorkDefect=0.,
                    maxImpactEnergyDefect=0., maxVerticalImpulseDefect=0., maxPitchImpulseDefect=0.,
                    maxVerticalCarryDefect=0., maxPitchCarryDefect=0., maxAirborneYawAuthority=0.,
                    peakRiderSpeed=0., peakRiderAcceleration=0.)

    def maximize(name, amount):
        evidence[name] = max(evidence[name], abs(amount))

    def axle(s, side):
        local = vec(g["Frame" + side]) - vec(s["Rider"]["Position"]) * rider / mass
        return (local * axis(s["Pitch"])).real

    previous = rows[0]["Before"]["Time"]
    cumulative = 0
    for row in rows:
        b, a, mid = row["Before"], row["After"], row["Midpoint"]
        f, plan, receipt = row["Forces"], row["Plan"], row["Horizontal"]
        dt = a["Time"] - b["Time"]
        assert dt > 0, path
        maximize("clockDefect", max(abs(previous - b["Time"]), abs(receipt["End"] - a["Time"]),
                                    abs(plan["Duration"] - dt)))
        previous = a["Time"]
        force = vec(f["Front"]["Force"]) + vec(f["Rear"]["Force"]) + vec(f["Drag"])
        measured = (vec(a["Plane"]["Velocity"]) - vec(b["Plane"]["Velocity"])) * mass
        maximize("momentumDefect", measured - force * dt)
        for side in ("Front", "Rear"):
            n = f["Normal"][side]
            assert n >= 0, (path, b["Time"], n)
            if n == 0:
                maximize("maxAirForce", vec(f[side]["Force"]))
                if side == "Front":
                    maximize("maxAirborneYawAuthority", abs(f["FrontYawMaximum"]) + abs(f["FrontYawMinimum"]))
            expected_arm = axle(mid, side) * axis(mid["Plane"]["Yaw"])
            maximize("projectedArmDefect", expected_arm - vec(plan[side]["Arm"]))
            evidence["minGap"] = min(evidence["minGap"], row[side]["Gap"])
        yaw = sum((vec(f[s]["Arm"]).conjugate() * vec(f[s]["Force"])).imag for s in ("Front", "Rear")) * dt
        maximize("yawDefect", yaw - (a["Plane"]["YawMomentum"] - b["Plane"]["YawMomentum"]))
        losses = [f[k] for k in ("EngineLoss", "ClutchLoss", "TransmissionLoss", "BearingLoss", "FrontSlipLoss",
                                 "RearSlipLoss", "DragLoss")]
        assert min(losses) >= -1e-6, (path, b["Time"], losses)
        ledger = energy(a, g, gravity) - energy(b, g, gravity)
        ledger -= (f["EnginePower"] - sum(losses)) * dt + row["RiderWork"] + row["NormalWork"]
        landing = row["Landing"]
        if landing:
            evidence["impacts"] += 1
            ledger += landing["Dissipated"] - landing["RiderWork"]
            j = landing["Impulse"]
            assert landing["Dissipated"] >= -1e-10 and j["Front"] >= 0 and j["Rear"] >= 0
            pre, post = landing["Before"], landing["After"]
            for key in ("Height", "Pitch", "CrankMomentum", "WheelMomentum", "Plane", "Rider"):
                assert pre[key] == post[key], (path, key)
            maximize("maxImpactEnergyDefect", energy(post, g, gravity) - energy(pre, g, gravity) -
                     landing["RiderWork"] + landing["Dissipated"])
            maximize("maxVerticalImpulseDefect", post["VerticalMomentum"] - pre["VerticalMomentum"] - j["Front"] - j["Rear"])
            maximize("maxPitchImpulseDefect", post["PitchMomentum"] - pre["PitchMomentum"] -
                     sum(axle(pre, s) * j[s] for s in ("Front", "Rear")))
        maximize("maxRecordedEnergyDifference", ledger - row["EnergyResidual"])
        maximize("maxNormalWork", row["NormalWork"])
        maximize("maxVerticalCarryDefect", row["VerticalImpulseResidual"])
        maximize("maxPitchCarryDefect", row["PitchImpulseResidual"])
        for s in (b, mid, a):
            maximize("peakRiderSpeed", vec(s["Rider"]["Velocity"]))
            maximize("peakRiderAcceleration", vec(s["Rider"]["Acceleration"]))
            for coordinate, lo, hi in (("X", -.18, .18), ("Y", -.14, .04)):
                offset = s["Rider"]["Position"][coordinate] - g["RiderNeutral"][coordinate]
                assert lo - 1e-12 <= offset <= hi + 1e-12, (path, s)
        cumulative += ledger
        maximize("maxAccumulatedWorkDefect", cumulative)
    evidence["finalWorkDefect"] = cumulative
    assert evidence["clockDefect"] < 1e-11 and evidence["minGap"] >= -2e-9, (path, evidence)
    assert evidence["momentumDefect"] < 1e-8 and evidence["yawDefect"] < 1e-9, (path, evidence)
    # At a split impact the last force preview can precede the event by a substep tolerance.
    assert evidence["projectedArmDefect"] < 1e-8, (path, evidence)
    assert evidence["maxAirForce"] == evidence["maxAirborneYawAuthority"] == 0, (path, evidence)
    assert evidence["maxNormalWork"] < 1e-8, (path, evidence)
    assert evidence["maxRecordedEnergyDifference"] < 1e-8 and evidence["maxAccumulatedWorkDefect"] < .03, (path, evidence)
    assert max(evidence[k] for k in ("maxImpactEnergyDefect", "maxVerticalImpulseDefect", "maxPitchImpulseDefect")) < 1e-9
    assert evidence["peakRiderSpeed"] <= protocol["MaximumSpeed"] + 1e-12
    assert evidence["peakRiderAcceleration"] <= protocol["MaximumAcceleration"] + 1e-12
    return evidence


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("receipt", type=Path)
    args = parser.parse_args()
    protocol = read(args.evidence / "protocol.json")
    checks = {p.name: result for p in sorted(args.evidence.glob("*.json.gz"))
              if (result := trajectory(p, protocol)) is not None}
    assert checks
    steps = sorted((r for r in read(args.evidence / "summary.json") if r["Case"] == "timestep-launch"),
                   key=lambda r: -r["Dt"])
    assert len(steps) == 4 and all(r["Stop"] == "horizon" and r["Impacts"] == 1 for r in steps)
    assert max(r["LastTouchdown"] for r in steps) - min(r["LastTouchdown"] for r in steps) < .001
    errors = [abs(r["FinalEnergyDefect"]) for r in steps]
    assert all(b < a for a, b in zip(errors, errors[1:]))
    result = dict(trajectories=checks, timestepLanding=[(r["Dt"], r["LastTouchdown"], r["FinalEnergyDefect"])
                  for r in steps], sha256={p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                  for p in sorted(args.evidence.iterdir()) if p.is_file() and p != args.receipt})
    args.receipt.write_text(json.dumps(result, indent=2, allow_nan=False) + "\n", encoding="utf-8", newline="\n")
    print(f"Verified {len(checks)} H33-U trajectories and {sum(x['steps'] for x in checks.values())} accepted steps.")


if __name__ == "__main__":
    main()
