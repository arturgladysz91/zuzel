"""Summarize accepted H28-F telemetry with exact distance-clipped segment times."""

import argparse
import gzip
import json
import math
from collections import Counter
from pathlib import Path


def read(path):
    with (gzip.open(path, "rt", encoding="utf-8") if path.suffix == ".gz"
          else path.open(encoding="utf-8")) as stream:
        return json.load(stream)


def dot(a, b):
    return a["X"] * b["X"] + a["Y"] * b["Y"]


def max_state(stats, key, value):
    stats[key] = max(stats.get(key, -math.inf), value)


def min_state(stats, key, value):
    stats[key] = min(stats.get(key, math.inf), value)


def classify(race):
    if race["Complete"]:
        return "complete"
    failure = race.get("Failure") or {}
    gate = failure.get("Gate")
    stop = race["Stop"] or ""
    if gate in {"course", "corridor-yaw", "technique"}:
        return "artificial technique rejection"
    if gate == "swept":
        if min(failure["Outer"], (race.get("End") or {}).get("Inner", math.inf)) < 0:
            return "genuine physical loss of control under model assumptions"
        return "controller planning deficiency: insufficient swept reserve"
    if "NoPhysicallyAdmissibleRoot" in stop or stop in {"beta", "progress"} or "wheel unloading" in stop.lower():
        return "genuine physical loss of control under model assumptions"
    if any(word in stop.lower() for word in ("certificate", "convergence", "isolation", "enclosure",
                                            "unresolved", "numericaldiscontinuity", "ambiguousormultipleroots")):
        return "numerical certification failure"
    return "undetermined"


def add(stats, key, value):
    stats[key] = stats.get(key, 0) + value


def intervals(protocol, after_start):
    goal = protocol["Goal"]
    cuts = [(0, "launch-straight")]
    for bend in protocol["Bends"]:
        cuts.extend([(bend["Entry"], "bend"),
                     (bend["Exit"], "finishing-straight"
                      if bend is protocol["Bends"][-1] else "ordinary-straight")])
    return [(max(after_start, s), min(goal, end), label)
            for (s, label), (end, _) in zip(cuts, cuts[1:] + [(goal, "")])
            if max(after_start, s) < min(goal, end)]


def describe(path, protocol, after_start):
    trace = read(path)
    clearance = trace["Sweep"]
    times, sectors, witness = [], {}, []
    ranges = intervals(protocol, after_start)
    run = None
    for tick in trace["Trace"] or []:
        before, after = tick["Before"], tick["After"]
        state = before["State"]
        rider = state["Rider"]
        s, end = rider["TotalReferenceMeters"], after["S"]
        dt = after["Time"] - state["Time"]
        if end <= s or dt <= 0:
            continue
        forces, power = tick["Forces"], tick["Power"]
        wheel = power["Wheel"]
        throttle = before["Active"]["Drive"]
        requested = tick["Command"]["Selected"]["Drive"] if tick["Command"] else throttle
        rpm = (power["BeforeOmega"] + power["AfterOmega"]) * 15 / math.pi
        beta_degrees = math.degrees(after["Beta"])
        for lo, hi, label in ranges:
            clipped_lo, clipped_hi = max(s, lo), min(end, hi)
            if clipped_hi <= clipped_lo:
                continue
            fraction = (clipped_hi - clipped_lo) / (end - s)
            seconds = dt * fraction
            a = sectors.setdefault(label, {})
            add(a, "seconds", seconds)
            add(a, "requestedWotSeconds", seconds if requested >= .9999 else 0)
            add(a, "activeWotSeconds", seconds if throttle >= .9999 else 0)
            speeds = [rider["Speed"] + (after["Speed"] - rider["Speed"]) * (at - s) / (end - s)
                      for at in (clipped_lo, clipped_hi)]
            max_state(a, "peakSpeedKph", max(speeds) * 3.6)
            max_state(a, "peakRpm", rpm)
            max_state(a, "peakRearSlip", wheel["InstantSlipRatio"])
            min_state(a, "minAccelerationMps2", tick["Acceleration"])
            min_state(a, "minSweepMeters", after["Sweep"])
            max_state(a, "peakAbsYawRadPerSec", abs(after["Yaw"]))
            max_state(a, "peakAbsBetaDeg", abs(beta_degrees))
            min_state(a, "minFrontNormalN", forces["TyreLoads"]["FrontNormalNewtons"])
            min_state(a, "minRearNormalN", forces["TyreLoads"]["RearNormalNewtons"])
            min_state(a, "minSteeringAngleReserveDeg", 35 - abs(math.degrees(after["Steer"])))
            steering_rate = abs(forces["SteeringAfter"] - forces["SteeringBefore"]) / dt
            add(a, "steeringRateLimitedSeconds", seconds if steering_rate > math.radians(89.9) else 0)
            add(a, "rearLongSlipLossJ", wheel["LongitudinalLossJoules"] * fraction)
            add(a, "wheelBearingLossJ", wheel["BearingLossJoules"] * fraction)
            add(a, "clutchLossJ", power["ClutchHeatJoules"] * fraction)
            add(a, "transmissionLossJ", power["TransmissionLossJoules"] * fraction)
            add(a, "rearLateralLossJ", -forces["RearLateral"] * dot(forces["RearVelocity"], forces["BodyLeft"]) * seconds)
            add(a, "frontLateralLossJ", -dot(forces["FrontForce"], forces["FrontVelocity"]) * seconds)
            add(a, "roadAndAirLossJ", -dot(forces["Resistance"], rider["Velocity"]) * seconds)
            engine = power["H26"]
            add(a, "engineMechanicalLossJ", engine["MechanicalWorkJoules"] * fraction)
            add(a, "enginePumpingLossJ", engine["PumpingWorkJoules"] * fraction)
            max_state(a, "maxAbsShaftEnergyResidualJ", abs(engine["ResidualJoules"]))
            for key, value in (
                ("requestedDrive", requested), ("activeDrive", throttle),
                ("actualNetCrankTorqueNm", engine["GrossCombustionTorqueNewtonMeters"]
                 - engine["MechanicalTorqueNewtonMeters"] - engine["PumpingTorqueNewtonMeters"]),
                ("combustionTorqueNm", engine["GrossCombustionTorqueNewtonMeters"]),
                ("wheelDriveTorqueNm", wheel["DriveTorqueNewtonMeters"]),
                ("frontFxBodyN", dot(forces["FrontForce"], forces["BodyForward"])),
                ("rearFxBodyN", forces["RearLongitudinal"]),
                ("frontFyBodyN", dot(forces["FrontForce"], forces["BodyLeft"])),
                ("rearFyBodyN", forces["RearLateral"]),
                ("riderForeAftMeters", before.get("RiderForeAftMeters", 0)),
            ):
                add(a, "mean" + key[0].upper() + key[1:], value * seconds)
            t0 = state["Time"] + dt * (clipped_lo - s) / (end - s)
            t1 = t0 + seconds
            if label == "bend" and throttle >= .9999 and tick["Acceleration"] < -.05 and (
                    beta_degrees >= 6 and after["Yaw"] > .20):
                if run is None or abs(run["toTime"] - t0) > 1e-7:
                    run = {"fromTime": t0, "fromS": clipped_lo, "minAx": tick["Acceleration"]}
                    witness.append(run)
                run.update(toTime=t1, toS=clipped_hi, seconds=t1-run["fromTime"],
                           minAx=min(run["minAx"], tick["Acceleration"]))
        for lap in (2, 3):
            cross = lap * protocol["LapMeters"]
            if s < cross <= end:
                times.append(state["Time"] + dt * (cross - s) / (end - s))
    for a in sectors.values():
        for key in a:
            if key.startswith("mean"):
                a[key] /= a["seconds"]
    return {"complete": trace["Complete"], "stop": trace["Stop"], "finish": trace["Finish"],
            "classification": classify(trace) if trace["Stop"] != "horizon" else "local horizon",
            "minimumSweptMeters": clearance, "lastLegalS": trace["LastLegalS"], "crossingTimes": times,
            "sectors": sectors, "wotDeceleratingSlide": [r for r in witness if r["seconds"] >= .12]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runs", type=Path)
    parser.add_argument("slides", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--laps", type=Path)
    args = parser.parse_args()
    protocol = read(args.runs / "protocol.json")
    files = sorted(args.runs.glob("*.json.gz")) + sorted(args.slides.glob("*.json.gz"))
    if args.laps is not None:
        files.extend(sorted(args.laps.glob("*-pulse-*.json.gz")))
    results = {}
    flying_start = protocol["Start"]["State"]["Rider"]["TotalReferenceMeters"]
    for path in files:
        if path.name == "search.json.gz":
            continue
        start = 0 if path.name == "baseline.json.gz" else flying_start
        results[path.name] = describe(path, protocol, start)
        if path.name == "baseline.json.gz":
            results["baseline-flying"] = describe(path, protocol, flying_start)
    args.output.write_text(json.dumps(results, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    scans = [args.runs / "search.json.gz"]
    if args.laps is not None:
        scans.append(args.laps / "pulse-search.json.gz")
    cases = []
    for scan in scans:
        for index, row in enumerate(read(scan)):
            cases.append({"archive": scan.name, "row": index, "arm": row["Arm"], "profile": row["Profile"],
                          "classification": classify(row["Race"]), "stop": row["Race"]["Stop"]})
    labels = Counter(row["classification"] for row in cases)
    args.output.with_name("classifications.json").write_text(
        json.dumps({"counts": labels, "cases": cases}, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
