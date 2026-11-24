"""Check H32-P trajectory ledgers and produce a portable validation receipt."""

import argparse
import gzip
import hashlib
import json
from pathlib import Path


def load(path):
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8") as stream:
            return json.load(stream)
    return json.loads(path.read_text(encoding="utf-8-sig"))


def length(vector):
    return (vector["X"] ** 2 + vector["Y"] ** 2) ** .5


def verify(path, protocol):
    run = load(path)
    records = run.get("Trace")
    if not records or "Forces" not in records[0] or "EndPitch" not in records[0]["Forces"]:
        return None
    out = dict(steps=len(records), impacts=0, minGapMeters=0, maxAirTyreForceNewtons=0,
               maxNormalWorkJoules=0, maxEnergyResidualJoules=0, maxImpactResidualJoules=0,
               maxImpactRotorSpeedError=0, maxPlanarImpulseErrorNewtonSeconds=0,
               maxYawImpulseErrorNewtonMeterSeconds=0, maxVerticalImpulseErrorNewtonSeconds=0,
               maxPitchImpulseErrorNewtonMeterSeconds=0, maxTyreUtilization=0)
    mu = 1.4 * protocol["Grip"]["EffectiveGrip"] * 1.25
    mass = protocol["Geometry"]["neutral"]["Mass"]
    yaw_inertia = protocol["YawInertia"]
    for row in records:
        before, after, forces = row["Before"], row["After"], row["Forces"]
        dt = after["Time"] - before["Time"]
        normal = forces["Loads"]["Normal"]
        assert normal["Mode"] == forces["Support"], (path, before["Time"], normal, forces["Support"])
        out["minGapMeters"] = min(out["minGapMeters"], row["FrontGap"], row["RearGap"])
        for wheel in ("Front", "Rear"):
            assert normal[wheel] >= 0, (path, row)
            if normal[wheel] == 0:
                out["maxAirTyreForceNewtons"] = max(out["maxAirTyreForceNewtons"], length(forces[wheel]))
            else:
                use = length(forces[wheel]) / (mu * normal[wheel])
                out["maxTyreUtilization"] = max(out["maxTyreUtilization"], use)
        for channel in ("EngineLoss", "ClutchLoss", "TransmissionLoss", "BearingLoss",
                        "FrontSlipLoss", "RearSlipLoss", "DragLoss"):
            assert forces[channel] >= -1e-7, (path, before["Time"], channel, forces[channel])
        for name, field in (
            ("maxNormalWorkJoules", forces["NormalWork"]),
            ("maxEnergyResidualJoules", row["EnergyResidual"]),
            ("maxVerticalImpulseErrorNewtonSeconds", row["VerticalImpulseResidual"]),
            ("maxPitchImpulseErrorNewtonMeterSeconds", row["PitchImpulseResidual"]),
            ("maxYawImpulseErrorNewtonMeterSeconds",
             yaw_inertia * (after["Body"]["AngularVelocity"] - before["Body"]["AngularVelocity"]) - dt * forces["YawMoment"]),
        ):
            out[name] = max(out[name], abs(field))
        for axis in ("X", "Y"):
            dv = after["Body"]["Velocity"][axis] - before["Body"]["Velocity"][axis]
            impulse = dt * sum(forces[channel][axis] for channel in ("Front", "Rear", "Drag"))
            out["maxPlanarImpulseErrorNewtonSeconds"] = max(out["maxPlanarImpulseErrorNewtonSeconds"],
                                                            abs(mass * dv - impulse))
        hit = row["Impact"]
        if hit is None:
            continue
        out["impacts"] += 1
        assert hit["EnergyLostJoules"] >= -1e-9 and hit["Impulse"]["Front"] >= 0 and hit["Impulse"]["Rear"] >= 0
        assert hit["Before"]["Angle"] == hit["After"]["Angle"]
        assert hit["Before"]["Height"] == hit["After"]["Height"]
        assert abs(hit["MomentumError"]) < 1e-9 and abs(hit["PitchImpulseError"]) < 1e-9
        out["maxImpactResidualJoules"] = max(out["maxImpactResidualJoules"], abs(hit["WorkIdentityError"]))
        for coordinate in ("Crank", "Wheel"):
            impulse_error = (after[coordinate] - after["Pitch"]["Rate"]) - (
                forces[coordinate + "After"] - forces["EndPitch"]["Rate"])
            out["maxImpactRotorSpeedError"] = max(out["maxImpactRotorSpeedError"], abs(impulse_error))
    assert out["minGapMeters"] >= -2e-9, (path, out)
    assert out["maxAirTyreForceNewtons"] == 0 and out["maxNormalWorkJoules"] < 1e-8, (path, out)
    assert out["maxImpactResidualJoules"] < 1e-9 and out["maxImpactRotorSpeedError"] < 1e-9, (path, out)
    assert out["maxTyreUtilization"] < 1.000001, (path, out)
    assert out["maxPlanarImpulseErrorNewtonSeconds"] < .002, (path, out)
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("receipt", type=Path)
    args = parser.parse_args()
    protocol = load(args.evidence / "protocol.json")
    checks = {p.name: result for p in sorted(args.evidence.glob("*.json.gz")) if (result := verify(p, protocol))}
    convergence = load(args.evidence / "convergence.json")
    assert convergence["Deterministic"]
    assert all(r["Stop"] == "horizon" and r["Impacts"] >= 1 and r["FrontAirSeconds"] > .1
               for r in convergence["Runs"])
    replay = load(args.evidence / "disabled.json")
    assert replay["Match"]
    result = dict(trajectories=checks, exactDisabledParity=replay,
                  deterministicReplay=convergence["Deterministic"], actualNativeWorldConstructions=0,
                  sha256={p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                          for p in sorted(args.evidence.iterdir()) if p.is_file() and p != args.receipt})
    args.receipt.write_text(json.dumps(result, indent=2, allow_nan=False) + "\n", encoding="utf-8", newline="\n")
    print(f"Verified {len(checks)} trajectories and {sum(x['steps'] for x in checks.values())} accepted steps.")


if __name__ == "__main__":
    main()
