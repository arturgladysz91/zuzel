"""Render numerical observation tables from the committed audit evidence; no fitting."""
import gzip
import json
import sys
from pathlib import Path


def table(headers, rows):
    return "\n".join(["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |"]
                     + ["| " + " | ".join(str(v) for v in row) + " |" for row in rows]) + "\n"


def values(distribution):
    return [distribution.get(k, "—") for k in ("N", "P10", "P25", "P50", "P75", "P90")]


def main():
    folder = Path(sys.argv[1])
    summary = json.loads((folder / "race-engine-readiness.json").read_text(encoding="utf-8"))
    traces = json.loads((folder / "trace-evidence.json").read_text(encoding="utf-8"))
    with gzip.open(folder / "cases.json.gz", "rt", encoding="utf-8") as f:
        runs = json.load(f)
    def case(name, cfg="C", seed=19):
        return next(r for r in runs if (r["Id"], r["Configuration"], r["Seed"]) == (name, cfg, seed))
    output = ["# Numerical observations\n", f"Baseline `{summary['SourceCommit']}`. Generated from the machine-readable evidence with `tools/race-engine-readiness/report.py`. Tables preserve stored numeric values; exact source bits and paths remain in the typed traces. These are observations, not calibration targets.\n",
              "## Starts, gates and first bend\n"]
    row = case("balanced-motoarena", "A")
    output.append(table(["Gate / rider", "Reaction / movement onset s", "Speed at 1 s m/s", "Speed at 2 s m/s", "Profile 70 km/h s", "First segment movement s"],
                        [(f"{'ABCD'[s['RiderId']-1]} / {s['RiderId']}", s["ReactionTimeSeconds"], s["SpeedAt1Second"], s["SpeedAt2Seconds"], s["Profile"]["TimeTo70KphSeconds"], s["Profile"]["MovementTimeSeconds"]) for s in row["Starts"]]))
    output.append("Identical neutral riders have the same reaction input. Gate geometry creates small launch differences and substantial bend-exit differences; collection reversal does not reassign gates. These scenarios do not estimate a universal fair gate balance.\n")
    rows = []
    for name in ("balanced-example", "balanced-motoarena", "overall", "starter-distance"):
        r = case(name)
        orders = r["BoundaryOrders"][:2]
        rows.append((name, [[g["Time"], g["Riders"]] for g in orders[0]["ArrivalGroups"]],
                     [[g["Time"], g["Riders"]] for g in orders[1]["ArrivalGroups"]],
                     [(x["RiderId"], x["Status"]) for x in r["Classification"]]))
    output.append(table(["Seed 19 / C", "First-bend entry [clock, riders]", "First-bend exit [clock, riders]", "Classification"], rows))
    output.append("The outside gate D rider in `overall` reaches the first bend first; Start 80 rider 1 in `starter-distance` reaches entry/exit first but finishes behind the others. Faster distance riding and incidents can overcome launch position. `Starts.Profile` retains the actual acceleration profile; `Segments.Acceleration`, state transitions and first-bend contact episodes retain sudden changes and evasions. At tape release standing riders are one exact-progress tie group, not ordered by ID. Rolling initial progress is recorded individually.\n")
    output.append("## Complete corner observations\n")
    rows = []
    for name, rider in (("same-line", 1), ("rain-motoarena", 1), ("rolling-C", 1), ("rolling-C", 2)):
        r = case(name)
        seg = [s for s in r["Segments"] if s["RiderId"] == rider and s["Lap"] == 0 and s["Phase"] and s["Phase"]["CornerId"] == 1]
        apex = next(x for t in traces["Cases"] if t["Id"] == name for x in t["ApexBrackets"] if x["Lap"] == 1 and x["CornerId"] == 1 and x["RiderId"] == rider)
        # Extracted IEEE strings are left intact for the apex; no estimated exact apex time.
        rows.append((f"{name} / {rider}", seg[0]["EntrySpeed"], min(s["MinSpeed"] for s in seg),
                     apex["BeforeTyped"]["SpeedMetersPerSecond"] + " → " + apex["AfterTyped"]["SpeedMetersPerSecond"],
                     seg[-1]["ExitSpeed"], sum(s["TotalDistanceMeters"] for s in seg), sum(s["TotalTimeSeconds"] for s in seg),
                     [apex["StartSeconds"], apex["EndSeconds"]]))
    output.append(table(["First corner / C / seed 19", "Entry m/s", "Minimum m/s", "Apex speed IEEE bracket", "Exit m/s", "Sum native path distances m", "Sum native durations s", "Apex common-clock bracket s"], rows))
    rows = []
    for name, rider in (("same-line", 1), ("rain-motoarena", 1), ("rolling-C", 1), ("rolling-C", 2)):
        seg = [s for s in case(name)["Segments"] if s["RiderId"] == rider and s["Lap"] == 0 and s["Phase"] and s["Phase"]["CornerId"] == 1]
        rows.append((f"{name} / {rider}", [min(s["MinLateral"] for s in seg), max(s["MaxLateral"] for s in seg)],
                     [min(s["MinCurvature"] for s in seg), max(s["MaxCurvature"] for s in seg)],
                     [min(s["MinSampledGrip"] for s in seg), max(s["MaxSampledGrip"] for s in seg)],
                     [min(s["Acceleration"]["Min"] for s in seg), max(s["Acceleration"]["Max"] for s in seg)],
                     [s["TurnExitAcceleration"] for s in seg if s["TurnExitAcceleration"] is not None]))
    output.append(table(["First corner / C / seed 19", "Lateral offset range m", "Curvature range 1/m", "Sampled grip range", "Node acceleration range m/s²", "Exit net acceleration m/s²"], rows))
    output.append("The apex is the canonical halfway point of the existing corner topology; its surrounding executed nodes supply a bracket. Minimum speed can occur elsewhere. Per-segment evidence also retains longitudinal acceleration bounds, physical lateral range, curvature, route-sampled grip, correction and exit drive. The rolling outside case pays a longer path while retaining higher speed than the inner leader. This establishes a physical distance/speed tradeoff in this fixture, not a claim that every wider line is advantageous. No ordinary reference-point edge violation was witnessed in completed cases; full-bike uncertified intervals and unswept width changes remain an evidence gap.\n")
    output.append("## Nine maneuver probes\n")
    descriptions = [("Faster follower on straight", "rolling-A", "Verified rider 2 over 1 on segment 0."),
                    ("Faster follower entering bend", "bend-catch", "Catch completes on segment 3 after the bend; not a verified in-bend pass."),
                    ("Inside attack", "rolling-B", "Verified rider 2 over 1 on bend entry segment 1."),
                    ("Outside attack", "rolling-C", "Verified rider 2 over 1 before the bend; compare subsequent physical corner costs. No general outside-pass impossibility."),
                    ("Defensive line occupation/widening", "rolling-E", "Blocked-inner opportunity; no pass in this seed. A selected response or lane change alone does not prove successful defense or forced opponent widening."),
                    ("Side-by-side corner entry", "rolling-D", "Positive alongside exposure; no strict pass in this seed."),
                    ("Three-rider squeeze", "three-squeeze-binary", "Completed companion records repeated lead changes with no applied contact consequences. Original decimal scenario is P1 REA-009 in every seed/configuration."),
                    ("Four-rider close racing", "four-close-regain-binary", "Multiple strict pair lead changes in a completed four-rider heat. Original decimal scenario is P1 REA-009."),
                    ("Regaining a position", "four-close-regain-binary", "Rider 1 passes 3, loses to 3, and passes 3 again; brackets are in trace-evidence.json. This proves re-passing, not attribution to a specific tactical intent.")]
    rows = []
    for label, name, conclusion in descriptions:
        r = case(name)
        first = r["Order"]["Passes"][:1]
        rows.append((label, name, len(r["Order"]["Passes"]), [(p["Ahead"], p["Behind"], p["BracketStart"], p["BracketEnd"]) for p in first], conclusion))
    output.append(table(["Maneuver", "Seed 19 / C", "Strict racing transitions", "First [ahead, behind, bracket]", "Interpretation"], rows))
    output.append("All A/B/C and seeds 7/19/83 remain separate in cases.json.gz. Corner/lap crossing orders are boundary arrival groups. Common-clock passes use actual executed canonical progress; close/tied intervals and missing coverage are reported. Retirement gains are classification effects, not passes. Tiny start/side-by-side lead reversals are not clear-bike, sustained-position or intention-success counts. The current observer cannot establish a causal forced-widening undercut solely from a selected CutInside candidate; physical paths and FullAudit alternatives support independent review.\n")
    output.append("## Interaction incidence, severity and recovery\n")
    rows = []
    for cfg in "ABC":
        for pop in ("ordinary standing start", "ordinary rolling fixture", "extreme stress (excluded from ordinary rates)"):
            p = summary["Configurations"][cfg][pop]
            rows.append((cfg, pop, p["CompletedHeats"], p["FailedHeats"], p["Statuses"], p["Episodes"], p["PredictedMechanicalEpisodes"], p["AvoidedPredictedMechanicalEpisodes"], p["AppliedPairs"], p["Severities"]))
    output.append(table(["Config", "Population", "Complete", "Failed", "Rider status counts", "Episodes", "Predicted mechanical", "Avoided predicted mechanical", "Applied pairs", "Applied rider severity"], rows))
    output.append("Ordinary standing C has 12 crashes among 176 riders in completed heats, including 11 applied Crash consequences; the other crash is a normal segment incident. A/B have 7/1 crashes among 180/176 completed-heat riders. These are small deliberately varied controlled matrices, not ordinary league crash-rate estimates. Stress C has 6 crashed and 6 finished riders across three extreme heats, with nine applied pairs and three frontiers containing three or more riders. Ordinary standing C records six repeated-overlap suppressions and no duplicated applied pair generation. Different run/capture denominators must not be mixed.\n")
    output.append(table(["C / population / metric", "N", "P10", "P25", "P50", "P75", "P90"],
                        [(f"{pop} / {metric}", *values(summary["Configurations"]["C"][pop][metric]))
                         for pop in ("ordinary standing start", "extreme stress (excluded from ordinary rates)")
                         for metric in ("SeverityRatio", "RecoverySegmentSeconds", "EndpointApplicationDelaySeconds", "NearExposureBracketSeconds")]))
    output.append("Near/alongside durations are sums of per-pair interval unions, hence pair-seconds upper brackets, not a heat-clock occupancy estimate. Recovery is one next active segment and varies in seconds; fresh contact may replace it. Endpoint application delay measures the committed endpoint minus verified first-touch frontier. Existing #59 intentionally changes endpoint speed without reintegrating the intervening remainder. It conserves the shadow impulse contract, but does not validate continuous impact timing. FullAudit candidate rejection reasons and selected responses are retained in the compressed diagnostic artifact; Summary captures do not provide all rejected candidates. No evidence here establishes a general defensive trap or an empirically excessive-yield frequency.\n")
    output.append("## Controlled rider sensitivities\n")
    output.append(table(["Variable", "Values", "Rider 1 total times s, C", "Rider 1 peak speeds m/s, C", "Reaction times s, C"],
                        [(variable, [x["Value"] for x in rows["C"]], [x["Rider1"]["TotalTimeSeconds"] for x in rows["C"]],
                          [x["Rider1"]["MaxSpeedMetersPerSecond"] for x in rows["C"]], [x["Start"]["ReactionTimeSeconds"] for x in rows["C"]])
                         for variable, rows in summary["Sensitivity"].items()]))
    output.append("Speed and SlideControl shorten time across these three tested inputs; Start reaction is monotonic but whole-heat time is not. TrackReading plateaus at 50/80 here; Adaptability and risk style are not monotonically faster. Technique/Strength/Mass/Condition/PairRiding yield identical rider-1 totals in this quiet seed-19 sweep, with no applied impact on rider 1. This bounds evidence in this context, not absence of their implemented consumers. Existing fixed-contact physiology fixtures isolate mass/reserve responses without arbitrary strength speed bonuses; whole-heat outcome dominance or acceptable effect sizes need a broader matched population. The controlled-contact fixture output is reproduced by batch, hashed in EvidenceFiles, and its impulse/reserve contract is covered by unchanged tests.\n")
    rows = []
    for fixture in summary["controlled-contact"]:
        if fixture["Name"].startswith(("G-technique", "H-strength", "I-condition", "J-mass")):
            rider = next(r for r in fixture["Analysis"]["Riders"] if r["RiderId"] == 1)
            pair = fixture["Analysis"]["AuditPairs"][0]
            rows.append((fixture["Name"], rider["StabilityReserve"], rider["CombinedStabilityDemand"], rider["SeverityRatio"],
                         pair["Impulse"]["MassAKg"], pair["Impulse"]["DeltaVAMetersPerSecond"]["Length"]))
    output.append(table(["Existing fixed-contact fixture", "Rider 1 reserve", "Demand", "Severity ratio", "System A mass kg", "DeltaV A magnitude m/s"], rows))
    output.append("## Motoarena reference distributions\n")
    metrics = ("pge_clean_vmax", "pge_clean_average_speed", "pge_clean_l1_penalty", "pge_clean_heat_time", "pge_clean_l1_time", "pge_four_rider_heat_time_spread", "pge_four_rider_vmax_spread", "pge_four_rider_l1_spread")
    real = {d["Definition"]["MetricId"]: d for d in summary["real-reference"]["Motoarena2026"]}
    rows = []
    for metric in metrics:
        r = real[metric]
        rows.append((metric, "Real", r["N"], *(r["Quantiles"][k] for k in ("P10", "P25", "P50", "P75", "P90"))))
        for evaluation in summary["pgee-evaluation"]:
            component = next(c for c in evaluation["VenueEvaluation"]["Components"] if c["Definition"]["MetricId"] == metric)
            counts = summary["PGEEAuditSampleCounts"][evaluation["Configuration"]]
            denominator = ("CompleteFourRiderHeats" if "spread" in metric else "FourLapPenaltyRecords" if "penalty" in metric else "L1Records" if metric.endswith("l1_time") else "FinishedRiderHeats")
            rows.append((metric, evaluation["Configuration"], counts[denominator], *(x["SimulationValue"] for x in component["QuantileComparisons"])))
    output.append(table(["Metric (Vmax/spread km/h; average m/s; other s)", "Population", "N", "P10", "P25", "P50", "P75", "P90"], rows))
    output.append("Only Motoarena standing scenarios enter this numerical comparison: 18/17/17 completed heats for A/B/C. Vmax/average/heat times use finished riders; first-lap observations may include a rider who later crashes; penalties require four laps; heat spreads require all four finished riders and 16 lap records. The real subset has 407 clean rider records and 93 four-attempt heat records. Its four-attempt grouping is the unchanged dataset definition, not a manufactured matching selection. Whole-dataset evaluator results are context only. Neutral/mixed-grip/rain audit cohorts are not mapped professionals; the measured peak/time differences cannot identify a force constant to change.\n")
    output.append(table(["Rider-relative own-median offsets / context", "N", "P10", "P25", "P50", "P75", "P90"],
                        [("Real / " + metric, *values(q)) for metric, q in summary["RiderRelativeEvidence"]["RealCentered"].items()]
                        + [(cfg + " / " + metric, *values(q)) for cfg, metrics in summary["RiderRelativeEvidence"]["SimulationBalancedDryAndRain"].items() for metric, q in metrics.items()]))
    output.append("Real offsets cover repeated named records at the same exact venue, centered on each rider's own median; simulation offsets cover identical fixed-gate profiles across dry/rain and three seeds. No real names are mapped to invented ratings. Sample counts per real rider, distinct/repeated rider counts and original CSV hash remain in RiderRelativeEvidence. PGEE rank fields cannot validate reaction, 2 s physical speed, corner shape or impacts.\n")
    output.append("## Workload and exact correctness\n")
    rows = []
    for cfg in "ABC":
        for pop in ("ordinary standing start", "ordinary rolling fixture", "extreme stress (excluded from ordinary rates)"):
            p = summary["Configurations"][cfg][pop]
            for metric in ("HeatMilliseconds", "AllocatedBytes"):
                rows.append((cfg + " / " + pop + " / " + metric, *values(p[metric])))
    output.append(table(["Complete-heat metric (failed captures excluded)", "N", "P10", "P25", "P50", "P75", "P90"], rows))
    output.append(table(["Config / population", "Unique alternative projections", "Actual verifications", "Production resolutions", "Narrow-phase evaluations"],
                        [(cfg + " / " + pop, *(summary["Configurations"][cfg][pop]["Work"].get(k, 0) for k in
                            ("UniqueRiderAlternativeProjections", "ActualProductionVerifications", "ProductionResolutions", "NarrowPhaseEvaluations")))
                         for cfg in "BC" for pop in ("ordinary standing start", "ordinary rolling fixture", "extreme stress (excluded from ordinary rates)")]))
    output.append(f"Offline wall budget used {summary['WallSeconds']} s of 5400 s. Production timings include cheap capture/logging and allocations include worker threads; post-hoc native exposure, hashing and serialization are separate. They are machine-specific measurements, not a #61 speedup comparison. All {summary['ExactReverseChecks']} input reversals agree exactly; {summary['ExactObserverChecks']} offline unobserved checks agree. The six-case pilot additionally checks all six observer-free cases. All {traces['FullDiagnosticReruns']} named FullAudit reruns have exact Summary final-state parity, and their Summary hashes equal the batch. There are {summary['ExactAccumulationAndOwnership']['Checks']} exact clock/distance checks, zero residuals and zero duplicated applied pair generations. The only recorded invariant witnesses are the 20 explicit production aborts. The reused-simulator test checks state isolation after contact stress. CI independently requires typed-hash baseline parity on both platforms, unchanged src/data and every historical golden/comparator. Green CI validates contracts and instrumentation; it cannot declare the P1 failed races successful.\n")
    (folder / "observed-results.md").write_text("\n".join(output), encoding="utf-8")
    print("Rendered numerical tables from audit evidence")


if __name__ == "__main__":
    main()
