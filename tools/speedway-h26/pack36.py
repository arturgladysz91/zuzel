
"""Package every accepted state and work sample without repeating solver internals."""
import gzip
import hashlib
import json
import sys
from pathlib import Path

from verify215 import vec


STATE = ("time", "x", "y", "z", "px", "py", "pz", "qw", "qx", "qy", "qz", "lx", "ly", "lz",
         "rx", "ry", "rz", "prx", "pry", "prz", "crankMomentum", "wheelMomentum", "steer",
         "legExtension", "legMomentum", "activeDrive", "steerDemand", "targetX", "targetY", "targetZ",
         "leftFootDemand", "nextActivation")
LOADS = ("frontNormal", "frontFx", "frontFy", "rearNormal", "rearFx", "rearFy",
         "rearNormalizedSlip", "engineRpm", "netEngineTorque", "bootNormal", "bootFx", "bootFy",
         "muscleX", "muscleY", "muscleZ", "bootMuscle", "musclePower", "energy", "workError",
         "lossesEngine", "lossesTransmission", "lossesBearing", "lossesSlideAndDrag",
         "lossesFrontNormal", "lossesRearNormal", "lossesPassiveSupport", "lossesBoot", "lossesLegStop",
         "steeringWorkRate", "frontCapacity", "rearCapacity", "frontYawBand", "frontRollBand",
         "enginePower")


def components(state):
    pose = state["Pose"]
    leg = state.get("Leg", {})
    command = state["Command"]
    return [state["Time"], *vec(state["Center"]), *vec(state["Momentum"]),
            pose["W"], *vec(pose["V"]), *vec(state["Angular"]), *vec(state["Rider"]),
            *vec(state["RiderMomentum"]), state["Crank"], state["Wheel"], state["Steer"],
            leg.get("Extension", 0), leg.get("Momentum", 0), command["Bike"]["Drive"],
            command["Bike"]["SteeringRadians"], *vec(command["Target"]), command.get("LeftFoot", 0), state["Next"]]


def loads(row):
    force = row["Forces"]
    front = force["Front"]
    rear = force["Rear"]
    leg = force.get("LeftFoot") or {}
    boot = leg.get("Contact", {})
    boot_force = vec(boot.get("Force", {}))
    passive_loss = sum(p["Contact"]["Dissipation"] for p in force.get("PassiveSupport", {}).values())
    return [front["Normal"], front["Longitudinal"], front["Lateral"], rear["Normal"], rear["Longitudinal"],
            rear["Lateral"], force["RearSlip"], force["EngineRpm"], force["NetEngineTorque"], boot.get("NormalForce", 0),
            boot_force[0], boot_force[1], *vec(force["Rider"]), leg.get("MuscleForce", 0), row["ActuatorPower"],
            row["EnergyAfter"], row["EnergyResidual"], force["EngineLoss"], force["TransmissionLoss"], force["BearingLoss"],
            force["SlideLoss"], front["NormalLoss"], rear["NormalLoss"], passive_loss, boot.get("Dissipation", 0),
            leg.get("StopLoss", 0), force["SteeringPower"], front["Capacity"], rear["Capacity"],
            force["FrontYawBand"], force["FrontRollBand"], force["EnginePower"]]


def pack(source, target):
    with gzip.open(source, "rt", encoding="utf-8") as handle:
        archive = json.load(handle)
    steps = archive.pop("Trace", None)
    if steps is None:
        steps = archive.pop("Steps")
    events = [[i, row["Contacts"]] for i, row in enumerate(steps) if row["Contacts"]]
    archive.update(StateColumns=STATE, LoadColumns=LOADS,
                   AcceptedStates=[components(row["After"]) for row in steps],
                   StepLoads=[loads(row) for row in steps], ContactEvents=events)
    if steps:
        archive["InitialState"] = components(steps[0]["Before"])
    encoded = json.dumps(archive, separators=(",", ":"), ensure_ascii=False, allow_nan=False).encode("utf-8")
    packed = gzip.compress(encoded, compresslevel=9, mtime=0)
    target.write_bytes(packed)
    return {"AcceptedStates": len(steps), "CompactBytes": len(packed),
            "CompactSha256": hashlib.sha256(packed).hexdigest(),
            "RawArchiveSha256": hashlib.sha256(source.read_bytes()).hexdigest()}


def main(source, target):
    target.mkdir(parents=True, exist_ok=True)
    manifest = {}
    for path in sorted(source.glob("*.json.gz")):
        name = path.name.replace(".json.gz", ".trajectory.json.gz")
        manifest[name] = pack(path, target / name)
    (target / "trajectory-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n",
                                                     encoding="utf-8", newline="\n")
    print(f"Packed {len(manifest)} complete state series and "
          f"{sum(row['AcceptedStates'] for row in manifest.values())} accepted states.")


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]))
