"""Independent BASE commit, impulse, energy and queue checks for study 177."""
import hashlib
import io
import json
import math
from pathlib import Path
from statistics import median
import sys
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / "docs/experiments/trajectory-control"
OLD = ROOT / "docs/experiments/three-lap-global-planning"
sys.path.insert(0, str(ROOT / "tools/three-lap-global-planning"))
from analyze172 import queue_audit

MASS, INERTIA = 147, 52.72097


def read(archive, name):
    return json.loads(archive.read(name + ".json"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dot(a, b):
    return sum(a[k] * b[k] for k in ("X", "Y"))


def kinetic(q):
    v = q["Input"]["Rider"]
    return .5 * (MASS * dot(v["Velocity"], v["Velocity"]) + INERTIA * v["AngularVelocity"]**2 +
                 .9 * q["Input"]["Omega"]**2 + .01 * q["CrankOmega"]**2)


def committed(a, b):
    """Use solved post-step values, so stale state as in #176 fails independently."""
    p, f = b["Powertrain"], b["Forces"]
    w = p["Wheel"]
    assert p["BeforeOmega"] == a["CrankOmega"] and p["AfterOmega"] == b["CrankOmega"], "crank commit"
    assert w["BeforeOmega"] == a["Input"]["Omega"] and w["AfterOmega"] == b["Input"]["Omega"], "wheel commit"
    assert f["SteeringBefore"] == a["Input"]["Steering"] and f["SteeringAfter"] == b["Input"]["Steering"], "steering commit"


def midpoint(a, b, arm):
    yaw = (a["AngularVelocity"] + b["AngularVelocity"]) / 2
    return dict(X=(a["Velocity"]["X"] + b["Velocity"]["X"]) / 2 - yaw * arm["Y"],
                Y=(a["Velocity"]["Y"] + b["Velocity"]["Y"]) / 2 + yaw * arm["X"])


def audit(native, decisions):
    rows = native["Riders"][0]["Samples"]
    queue_audit(native, decisions)
    assert native["NativeSteps"] == len(rows) - 1 and not native["Contacts"]
    total = 0
    max_impulse, max_yaw = 0, 0
    for a, b in zip(rows, rows[1:]):
        committed(a, b)
        p, f = b["Powertrain"], b["Forces"]
        w = p["Wheel"]
        dt = p["Seconds"]
        assert 0 < dt <= .010000001 and abs(dt - (b["Input"]["Time"] - a["Input"]["Time"])) < 1e-9
        assert f.get("AxleLoad") is None
        assert abs(f["TyreLoads"]["EffectiveFrictionCoefficient"] - 1.2845000267028808) < 1e-9
        assert p["ClutchHeatJoules"] >= -1e-6 and w["LongitudinalLossJoules"] >= -1e-6
        assert abs(p["EnergyResidualJoules"]) < 1e-6
        va, vb = a["Input"]["Rider"], b["Input"]["Rider"]
        dv = max(abs(vb["Velocity"][k] - va["Velocity"][k] - f["NetForce"][k] * dt / MASS) for k in ("X", "Y"))
        dw = abs(vb["AngularVelocity"] - va["AngularVelocity"] - f["NetTorque"] * dt / INERTIA)
        max_impulse, max_yaw = max(max_impulse, dv), max(max_yaw, dw)
        assert dv < 4e-5 and dw < 4e-5, (dv, dw)
        heat = p["ClutchHeatJoules"] + p["TransmissionLossJoules"] + p["CrankDragWorkJoules"]
        heat += w["LongitudinalLossJoules"] + w["BearingLossJoules"]
        heat -= dt * (dot(f["FrontForce"], midpoint(va, vb, f["FrontArm"])) +
                      f["RearLateral"] * dot(f["BodyLeft"], midpoint(va, vb, f["RearArm"])) +
                      dot(f["Resistance"], midpoint(va, vb, dict(X=0, Y=0))))
        delta = kinetic(b) - kinetic(a)
        excess = delta - p["CombustionWorkJoules"]
        assert abs(excess - b["EnergyExcessJoules"]) < .02
        assert excess < .1
        total -= excess + heat
    assert abs(total) < 12, total
    return dict(intervals=len(rows)-1, reviews=len(decisions), energy_residual_J=total,
                max_velocity_impulse_error_mps=max_impulse, max_yaw_impulse_error_rad_s=max_yaw)


def regression(rows):
    a, b = next((a, b) for a, b in zip(rows, rows[1:])
                if a["Input"]["Omega"] != b["Input"]["Omega"] and a["Input"]["Steering"] != b["Input"]["Steering"])
    for key in ("Omega", "Steering"):
        broken = dict(b, Input=dict(b["Input"], **{key: a["Input"][key]}))
        try:
            committed(a, broken)
        except AssertionError:
            continue
        raise AssertionError("stale #176 " + key + " escaped")


def preflight(path):
    with ZipFile(path) as trial, ZipFile(OLD / "172-development.zip") as source:
        with ZipFile(io.BytesIO(source.read("c3b-K4-D1.zip"))) as original:
            original_native = read(original, "native")
            old_commands = read(original, "decisions")
        native, decisions, result = (read(trial, n) for n in ("native", "decisions", "trial"))
        rows = native["Riders"][0]["Samples"]
        assert rows == original_native["Riders"][0]["Samples"][:len(rows)]
        assert [q["Action"] for q in decisions] == [q["Action"] for q in old_commands[:len(decisions)]]
        assert len(rows) == result["PrefixSamples"] and result["Outcome"]["ReachedStop"]
        regression(rows)
        return dict(verified=True, stale_176_test=True, archive_sha256=sha(path), **audit(native, decisions))


def audit_development(directory):
    rows = []
    for path in sorted(directory.glob("dev-*.zip")):
        with ZipFile(path) as archive:
            trial = read(archive, "trial")
            assert trial["PrefixSamples"] > 0
            rows.append(dict(id=path.stem, sha256=sha(path), local=trial["LocalPass"],
                             **audit(read(archive, "native"), read(archive, "decisions"))))
    assert any(q["local"] for q in rows)
    return rows


def sealed():
    for line in (DOC / "SHA256SUMS").read_text(encoding="utf-8").splitlines():
        digest, name = line.split("  ", 1)
        assert sha(ROOT / name) == digest, name
    with ZipFile(DOC / "177-protokol.zip") as protocol:
        freeze, settings = read(protocol, "freeze"), read(protocol, "protocol")
        held = DOC / "held177.json"
        assert freeze["HeldSha256"] == settings["HeldSha256"] == sha(held).upper()
        for name, digest in freeze["Sources"].items():
            # Frozen sources from the older checkout may use Windows line endings.
            data = (ROOT / name).read_bytes()
            assert digest in {hashlib.sha256(x).hexdigest().upper()
                              for x in (data, data.replace(b"\r\n", b"\n"), data.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n"))}, name
        reservations = [name for name in protocol.namelist() if name.endswith(".reserved.json")]
        assert len(reservations) == 16
        for stage, count in (("preflight", 2), ("development", 4), ("validation", 10)):
            assert sum(json.loads(protocol.read(name))["Stage"] == stage for name in reservations) == count
        preflight_hash = read(protocol, "integration")["archive_sha256"]
    with ZipFile(DOC / "177-natywne.zip") as archive:
        assert len(archive.namelist()) == 6
        control = archive.read("base-control.zip")
        assert hashlib.sha256(control).hexdigest() == preflight_hash
        # ZipFile accepts both filesystem paths and BytesIO objects.
        with ZipFile(io.BytesIO(control)) as z, ZipFile(OLD / "172-development.zip") as baseline:
            with ZipFile(io.BytesIO(baseline.read("c3b-K4-D1.zip"))) as old:
                run = read(z, "native")
                samples = run["Riders"][0]["Samples"]
                assert samples == read(old, "native")["Riders"][0]["Samples"][:len(samples)]
                regression(samples)
                audit(run, read(z, "decisions"))
        with ZipFile(io.BytesIO(archive.read("preflight-c3a.zip"))) as z:
            failure = read(z, "trial")["Outcome"]
            assert not failure["ReachedStop"] and "Frozen command prefix mismatch" in failure["Reason"]
        local = []
        for name in (x for x in archive.namelist() if x.startswith("dev-")):
            with ZipFile(io.BytesIO(archive.read(name))) as z:
                trial = read(z, "trial")
                audit(read(z, "native"), read(z, "decisions"))
                if trial["LocalPass"]:
                    assert trial["Terminal"] and trial["NewGoal"] < trial["BaseGoal"]
                    local.append(name)
        assert local == ["dev-E-L1-late.zip", "dev-X2-L1-late.zip"]
    times = {"BASE": [], "NEW": []}
    conditions = json.loads(held.read_bytes())
    with ZipFile(DOC / "177-walidacja.zip") as archive:
        assert len(archive.namelist()) == 10
        for scenario in conditions:
            pair = []
            for arm in ("BASE", "NEW"):
                name = "held-" + scenario["Id"] + "-" + arm + ".zip"
                with ZipFile(io.BytesIO(archive.read(name))) as z:
                    native, result = read(z, "native"), read(z, "trial")
                    audit(native, read(z, "decisions"))
                    outcome = result["Outcome"]
                    assert all(outcome["Start"][key] == value for key, value in scenario.items())
                    assert outcome["S3"] and outcome["Reserve"] and outcome["ReachedStop"] and outcome["MinSweep"] >= .5
                    assert len(native["Crossings"]) == 3
                    assert native["EndSeconds"] - native["Crossings"][-1]["Time"] >= 1
                    assert native["Crossings"][-1]["BeforeReference"] < 3 * native["LapMeters"] <= native["Crossings"][-1]["AfterReference"]
                    assert outcome["FinishTime"] == native["Crossings"][-1]["Time"]
                    assert (result["AppliedJoint"] > 0) == (arm == "NEW")
                    times[arm].append(outcome["FinishTime"])
                    pair.append(native["Riders"][0]["Samples"][0]["Input"]["Rider"])
            assert pair[0] == pair[1]
    base, new = median(times["BASE"]), median(times["NEW"])
    assert all(a > b for a, b in zip(times["BASE"], times["NEW"]))
    gain = 100*(base-new)/base
    summary = json.loads((DOC / "summary177.json").read_bytes())
    assert summary["medians_s"] == {"BASE": base, "NEW": new} and summary["median_gain_pct"] == gain
    assert 0 < gain < 5
    return {"worlds": 16, "valid_s3_pairs": 5, "median_gain_pct": gain, "sports": "BELOW_THRESHOLD"}


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] in ("preflight", "development"):
        directory = Path(sys.argv[2])
        if sys.argv[1] == "preflight":
            name, result = "integration.json", preflight(directory / "base-control.zip")
        else:
            name, result = "development-audit.json", audit_development(directory)
        with (directory / name).open("x", encoding="utf-8") as stream:
            json.dump(result, stream, indent=2)
        print(result)
    elif len(sys.argv) == 1:
        print(json.dumps(sealed()))
    else:
        raise SystemExit("Use preflight/development EVIDENCE_DIRECTORY.")
