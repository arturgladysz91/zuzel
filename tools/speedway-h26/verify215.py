
"""Verify H34-R MODEL trace receipts independently of the C# integrator."""
import gzip
import json
import math
import sys
from pathlib import Path


def vec(obj):
    return tuple(obj.get(k, 0) for k in ("X", "Y", "Z"))


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def scale(a, scalar):
    return tuple(x * scalar for x in a)


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    x, y, z = a
    u, v, w = b
    return y * w - z * v, z * u - x * w, x * v - y * u


def norm(a):
    return math.sqrt(dot(a, a))


def rotate(q, a, inverse=False):
    v = scale(vec(q["V"]), -1 if inverse else 1)
    double_cross = scale(cross(v, a), 2)
    return add(a, add(scale(double_cross, q["W"]), cross(v, double_cross)))


def tyre_force(tyre):
    patch = tyre["Patch"]
    return add((0, 0, tyre["Normal"]),
               add(scale(vec(patch["Forward"]), tyre["Longitudinal"]),
                   scale(vec(patch["Side"]), tyre["Lateral"])))


def rider_rate(state, base, roll_inertia, axes):
    r = vec(state["Rider"])
    p = vec(state["RiderMomentum"])
    fraction = base["RiderMass"] / base["Mass"]
    mu = base["RiderMass"] * (1 - fraction)
    i0 = [roll_inertia, base["PitchCentral"], base["YawCentral"]]
    rhs = sub(sub(rotate(state["Pose"], vec(state["Angular"]), inverse=True), cross(r, p)),
              (0, state["Crank"] + state["Wheel"], 0))
    columns = [(1, 0, 0), (0, 1, 0), (0, 0, 1)]
    locked = [cross(r, e) for bit, e in enumerate(columns) if not axes & (1 << bit)]
    a = [[(i0[i] if i == j else 0) + mu * sum(c[i] * c[j] for c in locked)
          for j in range(3)] + [rhs[i]] for i in range(3)]
    for i in range(3):
        pivot = a[i][i]
        a[i] = [v / pivot for v in a[i]]
        for j in range(3):
            if i != j:
                d = a[j][i]
                a[j] = [v - d * w for v, w in zip(a[j], a[i])]
    relative = sub(scale(p, 1 / mu), cross(tuple(row[-1] for row in a), r))
    return tuple(v if axes & (1 << bit) else 0 for bit, v in enumerate(relative))


def check_run(name, run, protocol):
    summary = run["Summary"]
    rows = run["Trace"]
    assert len(rows) == summary["Steps"], name
    base = protocol["baseGeometry"]
    mass = base["Mass"]
    axes = next((r["Axes"] for r in protocol["Arms"] if r["Name"] == summary["Arm"]), 7)
    gravity = 0 if summary["Case"] in ("isolated-angular-momentum", "lateral-recoil", "free-flight-moving") else protocol["Gravity215"]
    ledger = dict.fromkeys(("positionError", "linearImpulseError", "angularImpulseError", "rollMomentError",
                            "riderPowerError", "normalLawError", "energyReceiptError", "airborneForce",
                            "maxCompression", "maxUtilization", "cumulativeEnergyRemainder"), 0.0)
    previous, pending, events, energy = None, None, 0, 0.0

    def record(key, value):
        ledger[key] = max(ledger[key], abs(value))

    for row in rows:
        before, mid, after = (row[k] for k in ("Before", "Midpoint", "After"))
        step = row["Plan"]
        dt = step["Duration"]
        receipt = row["Horizontal"]
        assert abs(before["Time"] + dt - after["Time"]) < 1e-12 and dt > 0, name
        assert receipt["End"] == after["Time"] and all(v == 0 for v in vec(receipt["CollisionImpulse"])), name
        assert receipt["CollisionYawImpulse"] == 0, name
        assert after["Command"] == before["Command"] == mid["Command"], name
        if previous is not None:
            assert before["Time"] == previous["Time"], name
            assert all(v == previous[k] for k, v in before.items() if k not in {"Command", "Next"}), name
            assert before["Next"] >= pending, name
        previous, pending = after, before["Next"]
        f = row["Forces"]
        force = add(add(tyre_force(f["Front"]), tyre_force(f["Rear"])), vec(f["Drag"]))
        moment = (0, 0, 0)
        damping_loss = 0.0
        for k in ("Front", "Rear"):
            tyre = f[k]
            patch = tyre["Patch"]
            force_t = tyre_force(tyre)
            moment = add(moment, cross(vec(patch["Arm"]), force_t))
            assert tyre["Normal"] >= 0 and max(0, patch["Gap"]) * tyre["Normal"] == 0, name
            assert abs(patch["Gap"] - (mid["Center"]["Z"] + patch["Arm"]["Z"])) < 1e-11, name
            assert abs(dot(vec(patch["Axle"]), vec(patch["Radial"]))) < 1e-11, name
            compression = max(0, -patch["Gap"])
            closing = max(0, -patch["NormalSpeed"])
            damper = protocol["Damping"] * min(1, compression / .002) * closing
            record("normalLawError", tyre["Normal"] - compression * protocol["Stiffness"] - damper)
            record("normalLawError", tyre["NormalLoss"] - damper * closing)
            damping_loss += damper * closing
            record("maxCompression", compression)
            if compression == 0:
                record("airborneForce", norm(force_t))
            if tyre["Capacity"] > 0:
                record("maxUtilization", math.hypot(tyre["Longitudinal"], tyre["Lateral"]) / tyre["Capacity"])
        record("positionError", norm(sub(sub(vec(after["Center"]), vec(before["Center"])),
                                         scale(vec(mid["Momentum"]), dt / mass))))
        record("linearImpulseError", norm(sub(sub(vec(after["Momentum"]), vec(before["Momentum"])),
                                              scale(add(force, (0, 0, -mass * gravity)), dt))))
        record("angularImpulseError", norm(sub(sub(vec(after["Angular"]), vec(before["Angular"])), scale(moment, dt))))
        record("rollMomentError", norm(sub(moment, vec(row["RollPitchYawMoment"]))))
        rate = rider_rate(mid, base, protocol["RollInertia"], axes)
        rider_power = dot(rate, vec(f["Rider"]))
        record("riderPowerError", rider_power - row["ActuatorPower"])
        engine_net = f["EnginePower"] - sum(f[k] for k in ("EngineLoss", "TransmissionLoss", "BearingLoss", "SlideLoss"))
        work = (engine_net - damping_loss + f["SteeringPower"] + rider_power) * dt
        residual = row["EnergyAfter"] - row["EnergyBefore"] - work
        record("energyReceiptError", residual - row["EnergyResidual"])
        energy += residual
        record("cumulativeEnergyRemainder", energy)
        for e in row["Contacts"]:
            assert 0 <= e["Seconds"] <= dt, name
            events += 1

    for key, bound in {
        "positionError": 1e-8, "linearImpulseError": 1e-8, "angularImpulseError": 1e-8,
        "rollMomentError": 1e-8, "riderPowerError": 1e-7, "normalLawError": 1e-7,
        "energyReceiptError": 1e-7, "airborneForce": 0, "maxCompression": .075, "maxUtilization": 1 + 1e-8,
    }.items():
        assert ledger[key] <= bound, (name, key, ledger[key])
    assert abs(energy - summary["FinalEnergyResidual"]) < 1e-6, name
    return dict(ledger, steps=len(rows), contactEvents=events)


def main(root, output):
    protocol = json.loads((root / "protocol.json").read_text())
    trails = {}
    for path in sorted(root.glob("*.json.gz")):
        with gzip.open(path, "rt", encoding="utf-8") as stream:
            run = json.load(stream)
        trails[path.name] = check_run(path.name, run, protocol)
    assert trails
    output.write_text(json.dumps({"trajectories": trails}, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Verified {len(trails)} H34-R traces and {sum(r['steps'] for r in trails.values())} accepted steps.")


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]))
