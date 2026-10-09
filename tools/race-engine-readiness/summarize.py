"""Summarize observations; never calibrate physics or impose a realism pass threshold."""
import argparse
import csv
import gzip
import hashlib
import json
import struct
from pathlib import Path


def quantiles(values):
    # The existing CalibrationDistribution.LinearQuantile definition: (n-1)*p.
    ordered = sorted(values)
    if not ordered:
        return {"N": 0}
    def at(p):
        position = (len(ordered) - 1) * p
        lower = int(position)
        upper = min(lower + 1, len(ordered) - 1)
        return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower)
    return {"N": len(ordered), **{f"P{p}": at(p / 100) for p in (10, 25, 50, 75, 90)}}


def union_duration(intervals):
    end = None
    total = 0
    for start, finish in sorted(intervals):
        if finish < start:
            raise ValueError("Backwards observation interval")
        total += max(0, finish - max(start, end if end is not None else start))
        end = max(end if end is not None else finish, finish)
    return total


def validate(manifest, runs):
    if manifest.get("Schema") != "race-readiness-v1" or manifest.get("Completed") is not True:
        raise ValueError("Incomplete or unknown capture")
    keys = [(r["Id"], r["Seed"], r["Configuration"]) for r in runs]
    if len(set(keys)) != len(keys) or len(keys) != len(manifest["Cases"]):
        raise ValueError("Duplicate or missing capture")
    if set(keys) != {(c["Id"], c["Seed"], c["Configuration"]) for c in manifest["Cases"]}:
        raise ValueError("Manifest/capture mismatch")
    if not all(r["ReversedExact"] for r in runs):
        raise ValueError("Unverified input order")
    for r in runs:
        flags = r["Options"]
        on = "System.Boolean:True"
        expected = (r["Configuration"] != "A", r["Configuration"] == "C")
        actual = (flags["root.EnableContestedSpaceResponses"] == on,
                  flags["root.EnablePhysicalContactConsequences"] == on)
        if actual != expected:
            raise ValueError("Mislabeled feature configuration")


def summarize(manifest, runs):
    validate(manifest, runs)
    configurations = {}
    for cfg in "ABC":
        group = [r for r in runs if r["Configuration"] == cfg]
        populations = {}
        for population in sorted({r["Population"] for r in group}):
            rows = [r for r in group if r["Population"] == population]
            completed = [r for r in rows if not r.get("Failed", False)]
            riders = [x for r in rows for x in r["Riders"]]
            works = [w for r in rows for w in r["Work"]]
            contacts = [c["Consequence"] for r in rows for c in r["Applied"]]
            exposure = [i for r in rows for i in r["Exposure"]["Intervals"]]
            populations[population] = {
                "Heats": len(rows), "CompletedHeats": sum(not r.get("Failed", False) for r in rows),
                "FailedHeats": sum(r.get("Failed", False) for r in rows), "Riders": len(riders),
                "Statuses": {s: sum(x["Status"] == s for x in riders) for s in sorted({x["Status"] for x in riders})},
                "Passes": {p: sum(x["Phase"] == p for r in rows for x in r["Order"]["Passes"]) for p in ("start", "racing", "first-bend boundary")},
                "PassCoverageBreaks": sum(r["Order"]["CoverageBreaks"] for r in rows),
                "TiedIntervals": sum(r["Order"]["TiedIntervals"] for r in rows),
                "Incidents": {s: sum(x["Outcome"] == s for r in rows for x in r["Incidents"]) for s in ("Brake", "RunWide", "Crash")},
                "ContactEvents": sum(len(r["Events"]) for r in rows),
                "AppliedRiderConsequences": len(contacts), "AppliedPairs": sum(len(r["Pairs"]) for r in rows),
                "Severities": {s: sum(x["Severity"] == s for x in contacts) for s in sorted({x["Severity"] for x in contacts})},
                "SeverityRatio": quantiles([x["SeverityRatio"] for x in contacts]),
                "ForwardDeltaVelocityMetersPerSecond": quantiles([x["DeltaForwardMetersPerSecond"] for x in contacts]),
                "RecoverySegmentSeconds": quantiles([f32(x["Duration"]) for r in completed for x in r["Recoveries"] if x["Duration"] is not None]),
                "EndpointApplicationDelaySeconds": quantiles([x["DelaySeconds"] for r in completed for x in impact_timing(r)]),
                "SimultaneousFrontiersWithThreeOrMoreRiders": sum(len(ids) >= 3 for r in completed for _, _, ids in
                    {(p["Step"], p["FrontierStartTimeSeconds"], tuple(p["FrontierRiderIds"])) for p in r["Pairs"]}),
                "Suppressions": sum(r["Suppressions"] for r in rows),
                "Episodes": sum(len(r["Episodes"]) for r in rows),
                "PredictedMechanicalEpisodes": sum(x["InitialMinimumSeparationMeters"] <= 0 for r in rows for x in r["Episodes"]),
                "ClearedEpisodes": sum(x["ResolvedWithoutMechanicalContact"] for r in rows for x in r["Episodes"]),
                "AvoidedPredictedMechanicalEpisodes": sum(x["InitialMinimumSeparationMeters"] <= 0 and x["ResolvedWithoutMechanicalContact"]
                                                          for r in rows for x in r["Episodes"]),
                "Responses": {s: sum(x["Response"] == s for r in rows for e in r["Episodes"] for x in e["SelectedResponses"])
                              for s in sorted({x["Response"] for r in rows for e in r["Episodes"] for x in e["SelectedResponses"]})},
                "Work": {k: sum(w[k] for w in works) for k in sorted({k for w in works for k in w})},
                "HeatMilliseconds": quantiles([r["Milliseconds"] for r in completed]),
                "AllocatedBytes": quantiles([r["AllocatedBytes"] for r in completed]),
                "FailedCaptureMilliseconds": quantiles([r["Milliseconds"] for r in rows if r.get("Failed", False)]),
                "UncertifiedFootprints": sum(len(r["UncertifiedFootprints"]) for r in rows),
                "InvariantWitnesses": sum(len(r["Violations"]) for r in rows),
                "NearExposureBracketSeconds": quantiles([exposure_time(r) for r in completed]),
                "AlongsideBracketIntervals": sum(i["AOverlap"] == "SideBySide" or i["BOverlap"] == "SideBySide" for i in exposure),
            }
        configurations[cfg] = populations
    sensitivity = {}
    for variable in ("Start", "Speed", "SlideControl", "TrackReading", "PairRiding", "Adaptability", "Technique", "Strength", "Mass", "Condition", "Style"):
        sensitivity[variable] = {}
        for cfg in "ABC":
            group = sorted((r for r in runs if r["Configuration"] == cfg and r["Id"].startswith(f"sweep-{variable}-")),
                           key=lambda r: float(r["Id"].rsplit("-", 1)[1]))
            sensitivity[variable][cfg] = [
                {"Value": float(r["Id"].rsplit("-", 1)[1]), "Seed": r["Seed"],
                 "Rider1": next(x for x in r["Riders"] if x["RiderId"] == 1),
                 "Laps": [x for x in r["Laps"] if x["RiderId"] == 1],
                 "Start": next((x for x in r["Starts"] if x["RiderId"] == 1), None),
                 "Applied": [x for x in r["Applied"] if x["Consequence"]["RiderId"] == 1],
                 "Passes": [x for x in r["Order"]["Passes"] if x["Ahead"] == 1 or x["Behind"] == 1]}
                for r in group]
    return {"Schema": "race-readiness-summary-v1", "SourceCommit": manifest["SourceCommit"],
            "RunCount": len(runs), "ConfigurationCounts": {c: sum(r["Configuration"] == c for r in runs) for c in "ABC"},
            "CompletedProductionHeats": sum(not r.get("Failed", False) for r in runs),
            "ScenarioCount": len({r["Id"] for r in runs}), "Seeds": sorted({r["Seed"] for r in runs}),
            "Environment": manifest["Environment"], "WallSeconds": manifest["WallSeconds"],
            "ExactReverseChecks": sum(r["ReversedExact"] for r in runs),
            "FailedProductionHeats": [{"Id": r["Id"], "Seed": r["Seed"], "Configuration": r["Configuration"], "Failure": r["Failure"]}
                                      for r in runs if r.get("Failed", False)],
            "ExactObserverChecks": sum(r["ObserverExact"] is True for r in runs),
            "Configurations": configurations, "Sensitivity": sensitivity,
            "InvariantWitnesses": [{"Id": r["Id"], "Seed": r["Seed"], "Configuration": r["Configuration"], "Witnesses": r["Violations"]}
                                   for r in runs if r["Violations"]],
            "ExactAccumulationAndOwnership": check_accumulation(runs),
            "ImpactTiming": {"Contract": "Existing endpoint abstraction: first-touch impulse is applied to the normal endpoint speed; traversed remainder is not reintegrated. Not an exact impact-time trajectory.",
                             "Rows": [dict(Id=r["Id"], Seed=r["Seed"], Configuration=r["Configuration"], **x) for r in runs for x in impact_timing(r)]},
            "PGEEAuditSampleCounts": pgee_counts(runs)}


def impact_timing(run):
    segments = {(s["Step"], s["RiderId"]): s for s in run["Segments"]}
    result = []
    for application in run["Applied"]:
        c = application["Consequence"]
        s = segments[(application["Step"], c["RiderId"])]
        endpoint = f32(f32(s["StartElapsedTimeSeconds"]) + f32(s["TotalTimeSeconds"]))
        result.append({"Step": application["Step"], "RiderId": c["RiderId"], "Severity": c["Severity"],
                       "FrontierSeconds": c["FrontierTimeSeconds"], "CommittedEndpointSeconds": endpoint,
                       "DelaySeconds": endpoint - c["FrontierTimeSeconds"],
                       "PreEndpointSpeedMetersPerSecond": c["PreContactSpeed"],
                       "PostEndpointSpeedMetersPerSecond": c["PostContactSpeed"]})
    return result


def pgee_counts(runs):
    result = {}
    for cfg in "ABC":
        rows = [r for r in runs if r["Configuration"] == cfg and r["Track"] == "motoarena" and not r["Sweep"] and not r.get("Failed", False)]
        finished = sum(x["Status"] == "Finished" for r in rows for x in r["Riders"])
        complete = sum(len(r["Laps"]) == 16 and all(x["Status"] == "Finished" for x in r["Riders"]) for r in rows)
        result[cfg] = {"CompletedStandingStartHeats": len(rows), "FinishedRiderHeats": finished,
                       "L1Records": sum(l["LapNumber"] == 1 for r in rows for l in r["Laps"]),
                       "FourLapPenaltyRecords": sum(sum(l["RiderId"] == rid for l in r["Laps"]) == 4 for r in rows for rid in (1, 2, 3, 4)),
                       "CompleteFourRiderHeats": complete}
    return result


def f32(value):
    """Reproduce the existing single-precision accumulation contract, not a comparison tolerance."""
    return struct.unpack("<f", struct.pack("<f", value))[0]


def check_accumulation(runs):
    observations = []
    checks = 0
    duplicate_generations = []
    for r in runs:
        for rider in r["Riders"]:
            segments = sorted((s for s in r["Segments"] if s["RiderId"] == rider["RiderId"]), key=lambda s: s["Step"])
            initial = next(x for x in r["Initial"] if x["Profile"]["Id"] == rider["RiderId"])
            distance = f32(initial["Position"]["DistanceMeters"])
            for s in segments:
                distance = f32(distance + f32(s["TotalDistanceMeters"]))
            if segments:
                checks += 1
                if distance != f32(rider["TotalDistanceMeters"]):
                    observations.append(dict(Id=r["Id"], Seed=r["Seed"], Configuration=r["Configuration"], Rider=rider["RiderId"],
                                             Kind="distance accumulation", Expected=distance, Observed=f32(rider["TotalDistanceMeters"])))
                for s, next_s in zip(segments, segments[1:]):
                    checks += 1
                    end = f32(f32(s["StartElapsedTimeSeconds"]) + f32(s["TotalTimeSeconds"]))
                    if end != f32(next_s["StartElapsedTimeSeconds"]):
                        observations.append(dict(Id=r["Id"], Seed=r["Seed"], Configuration=r["Configuration"], Rider=rider["RiderId"],
                                                 Kind="clock accumulation", Step=s["Step"], Expected=end, Observed=f32(next_s["StartElapsedTimeSeconds"])))
        seen = set()
        for pair in r["Pairs"]:
            a, b = sorted((pair["Pair"]["RiderA"], pair["Pair"]["RiderB"]))
            states = next(g["States"] for g in r["Generations"] if g["Step"] == pair["Step"])
            resolved = next(s for s in states if s["Role"] == "resolved")
            generation = next(s["Generation"] for s in resolved["Pairs"] if (s["A"], s["B"]) == (a, b))
            key = a, b, generation
            if key in seen:
                duplicate_generations.append(dict(Id=r["Id"], Seed=r["Seed"], Configuration=r["Configuration"], Pair=key, Step=pair["Step"]))
            seen.add(key)
    return {"Checks": checks, "AccumulationResiduals": observations, "DuplicatedAppliedPairGenerations": duplicate_generations,
            "Interpretation": "Exact float accumulation checks; residuals retain values and need diagnosis, never tolerance suppression."}


def compact(run):
    result = dict(run)
    result["Incidents"] = [{k: v for k, v in incident.items() if k not in ("ContinuousCornerProfile", "ExecutedPath")}
                           for incident in run["Incidents"]]
    exposure = dict(result["Exposure"])
    intervals = exposure.pop("Intervals")
    pairs = sorted({(i["RiderA"], i["RiderB"]) for i in intervals})
    exposure["PairBracketSummaries"] = [
        {"RiderA": a, "RiderB": b,
         "NearBracketSeconds": union_duration([(i["IntervalStartSeconds"], i["IntervalEndSeconds"]) for i in intervals if (i["RiderA"], i["RiderB"]) == (a, b)]),
         "AlongsideBracketSeconds": union_duration([(i["IntervalStartSeconds"], i["IntervalEndSeconds"]) for i in intervals if (i["RiderA"], i["RiderB"]) == (a, b)
                                                    and (i["AOverlap"] == "SideBySide" or i["BOverlap"] == "SideBySide")]),
         "IntervalCount": sum((i["RiderA"], i["RiderB"]) == (a, b) for i in intervals)} for a, b in pairs]
    exposure["VerifiedTouchBrackets"] = [i for i in intervals if i["FirstTouchCommonTimeSeconds"] is not None]
    exposure["NearSamples"] = sorted(intervals, key=lambda i: i["MinimumSeparationMeters"])[:12]
    exposure["RawReproduction"] = "Run the offline batch for complete near-interval rows. No observations are silently synthesized."
    result["Exposure"] = exposure
    return result


def relative_evidence(runs, csv_path):
    with csv_path.open(encoding="utf-8", newline="") as f:
        real = [r for r in csv.DictReader(f) if r["season"] == "2026"
                and r["source_track_label"] == "Motoarena im. Mariana Rosego" and r["clean_physics"] == "1"]
    groups = {}
    for row in real:
        groups.setdefault(row["rider_id"], []).append(row)
    repeated = {rid: rows for rid, rows in groups.items() if len(rows) > 1}
    real_centered = {}
    for metric in ("max_speed_kph", "average_speed_mps", "l1_penalty_s"):
        offsets = []
        for rows in repeated.values():
            values = [float(row[metric]) for row in rows]
            center = quantiles(values)["P50"]
            offsets.extend(v - center for v in values)
        real_centered[metric] = quantiles(offsets)
    simulation = {}
    for cfg in "ABC":
        rows = [r for r in runs if r["Configuration"] == cfg and r["Id"] in ("balanced-motoarena", "rain-motoarena")
                and not r.get("Failed", False)]
        per_rider = {rid: [next(x for x in r["Riders"] if x["RiderId"] == rid) for r in rows] for rid in (1, 2, 3, 4)}
        centered = {}
        for metric, field, scale in (("max_speed_kph", "MaxSpeedMetersPerSecond", 3.6),
                                     ("average_speed_mps", "AverageSpeedMetersPerSecond", 1)):
            offsets = []
            for rider in per_rider.values():
                values = [f32(x[field]) * scale for x in rider if x["Status"] == "Finished"]
                if len(values) > 1:
                    center = quantiles(values)["P50"]
                    offsets.extend(v - center for v in values)
            centered[metric] = quantiles(offsets)
        offsets = []
        for rid in (1, 2, 3, 4):
            values = []
            for row in rows:
                laps = [l for l in row["Laps"] if l["RiderId"] == rid]
                if len(laps) == 4:
                    flying = sorted(f32(l["LapTimeSeconds"]) for l in laps if l["LapNumber"] > 1)
                    values.append(f32(next(l["LapTimeSeconds"] for l in laps if l["LapNumber"] == 1)) - flying[1])
            if len(values) > 1:
                center = quantiles(values)["P50"]
                offsets.extend(v - center for v in values)
        centered["l1_penalty_s"] = quantiles(offsets)
        simulation[cfg] = centered
    return {"Interpretation": "Context only: same-rider offsets from own venue median. No ratings inferred; synthetic fixed-gate Skill50 profiles are not professionals.",
            "RealVenueCleanRows": len(real), "RealDistinctRiders": len(groups), "RealRepeatedRiders": len(repeated),
            "RealPerRiderN": quantiles([len(rows) for rows in repeated.values()]),
            "RealCentered": real_centered, "SimulationBalancedDryAndRain": simulation,
            "DatasetSHA256": hashlib.sha256(csv_path.read_bytes()).hexdigest()}


def exposure_time(run):
    by_pair = {}
    for i in run["Exposure"]["Intervals"]:
        key = i["RiderA"], i["RiderB"]
        by_pair.setdefault(key, []).append((i["IntervalStartSeconds"], i["IntervalEndSeconds"]))
    return sum(union_duration(v) for v in by_pair.values())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("capture", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--findings", type=Path, default=Path("docs/calibration/race-engine-readiness/findings.json"))
    args = parser.parse_args()
    manifest = json.loads((args.capture / "manifest.json").read_text())
    with gzip.open(args.capture / "runs.json.gz", "rt", encoding="utf-8") as f:
        runs = json.load(f)
    result = summarize(manifest, runs)
    findings = json.loads(args.findings.read_text(encoding="utf-8"))
    if findings["SourceCommit"] != manifest["SourceCommit"]:
        raise ValueError("Findings refer to a different production baseline")
    result["Assessment"] = findings
    result["RiderRelativeEvidence"] = relative_evidence(runs, Path("data/calibration/pge/v1/pge_rider_heats.csv"))
    result["EvidenceFiles"] = {p.name: {"Bytes": p.stat().st_size, "SHA256": hashlib.sha256(p.read_bytes()).hexdigest()}
                               for p in sorted(args.capture.iterdir()) if p.is_file()}
    for name in ("real-reference.json", "pgee-evaluation.json", "controlled-contact.json"):
        result[name.removesuffix(".json")] = json.loads((args.capture / name).read_text())
    compact_path = args.output.with_name("cases.json.gz")
    with gzip.GzipFile(filename=str(compact_path), mode="wb", mtime=0) as f:
        f.write(json.dumps([compact(r) for r in runs], separators=(",", ":"), allow_nan=False).encode("utf-8"))
    result["CompactCases"] = {"Path": compact_path.name, "Bytes": compact_path.stat().st_size,
                              "SHA256": hashlib.sha256(compact_path.read_bytes()).hexdigest()}
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False) + "\n", encoding="utf-8")
    print(f"Summarized {len(runs)} cases; invariant witness cases={len(result['InvariantWitnesses'])}")


if __name__ == "__main__":
    main()
