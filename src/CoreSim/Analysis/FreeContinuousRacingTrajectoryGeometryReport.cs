using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public static class FreeContinuousRacingTrajectoryGeometryReport
{
    public static string Render(FreeContinuousRacingTrajectoryGeometryExperimentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var output = new StringBuilder();
        void L(string value = "") => output.Append(value).Append('\n');
        static string F(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
        static string E(double value) => value.ToString("0.000000000E+0", CultureInfo.InvariantCulture);
        static string Signed(double value) => value.ToString("+0.000000;-0.000000;0.000000", CultureInfo.InvariantCulture);
        static string Yn(bool value) => value ? "YES" : "NO";
        static string Vector(IEnumerable<float> values) => "[" + string.Join(", ",
            values.Select(value => F(value))) + "]";

        var best = result.BestFound;
        var bestConstant = result.BestConstant;
        var inner = result.ConstantInner;
        var old = result.OldWinnerAfterRepair;
        var priorReviewed = result.PriorReviewedWinnerAfterContinuityRepair;
        var priorBroad = result.PriorReviewedBroadLateWinnerAfterEnvelopeRepair;
        var bestPath = best.Path ?? throw new InvalidOperationException("The best trajectory must have geometry.");

        L("# Free Continuous Racing Trajectory Geometry Experiment — complete sampled turning-demand envelope");
        L();
        L("## A. Scope and status");
        L();
        L("This is an analysis-only periodic-sector experiment on the frozen Motoarena fixture. It does not change production corner traversal, rider decisions, track evolution, contact, incidents, RNG or calibration constants. The common fixed entry speed remains the observed #46 value; repeated-lap closure is secondary.");
        L();
        L("## B. Pre-repair blocker");
        L();
        L("Pre-repair best: `FCT-CEAACA7835FE`, 6.463669 s — **NOT PHYSICS-INTERPRETABLE due variable-curvature envelope blocker**. The old evaluator converted each sample's local radius to a local safe speed and then treated that value as a fresh canonical-p=.5 apex capability. At about p=.30 the old path carried roughly 25.62 m/s through a local settled capability of roughly 16.95 m/s, yet remained valid. The repaired result is not tuned to preserve that time.");
        L();
        L("## C. Why local safe speed is not canonical-apex speed");
        L();
        L("`MaxSafeTurnSpeedForRadius` returns local settled turning capability. It is neither a hard instantaneous cap nor a transferable p=.5 apex value. A capability derived at p=.30 is therefore never inserted into a new `ContinuousCornerEnvelope` centered at p=.5. Controlled entry overspeed remains legal only through the unchanged production advanced outcome bands and correction capability.");
        L();
        L("## D. Frozen production baseline and support refactor");
        L();
        L($"BaseMainSha: `{result.BaseSha}`.");
        L("ProductionSupportRefactor: **YES** — `SegmentPhysics.MaxSafeTurnSpeedForRadius` remains the pure radius extraction, and `SegmentPhysics.ResolveAdvancedForExplicitTarget` is the shared pure advanced-outcome primitive. Existing `SegmentPhysics.Apply` delegates to it.");
        L("ProductionNumericBehaviorChanged: **NO** — production calculates the same target first and then executes the identical arithmetic in the extracted helper; bit-exact regression covers Ok, quiet correction, Brake, RunWide and Crash.");
        L($"#46Reproduced: **{Yn(result.Reproduces46)}**. ConstantInner={F(result.ConstantInnerProductionFlyingLapSeconds)} s; E0-A1-X0={F(result.E0A1X0ProductionFlyingLapSeconds)} s; E0-A1-X1={F(result.E0A1X1ProductionFlyingLapSeconds)} s.");
        L($"Common observed entry speed: **{F(result.EntrySpeedMetersPerSecond)} m/s**.");
        L();
        L("## E. Continuous 2D trajectory geometry");
        L();
        L($"Eleven neutral physical-offset stations at 0%, 10%, ..., 100% define a natural cubic C2 spline. Curvature is derived from Cartesian derivatives; physical path length is integrated from {FreeContinuousTrajectoryGeometry.CartesianSampleDivisions} chords. Best maximum chord is {F(bestPath.MaximumSampleSpacingMeters)} m against the {F(FreeContinuousRacingTrajectoryGeometryExperiment.PathSampleResolutionLimitMeters)} m limit. Boundaries and self-intersection are rejection gates, never clamps. No apex input exists.");
        L();
        L("## F. Turning-demand constraint extraction");
        L();
        L($"Every non-constant path evaluates all {FreeContinuousTrajectoryGeometry.CartesianSampleDivisions + 1} deterministic Cartesian samples; no local-peak window, prominence percentage or curvature threshold selects the demand points. At every sample, the canonical-only replay supplies the baseline speed and the accepted differential-v² rule contributes `min(0, localSettled² - canonicalSettled²)` to that baseline speed squared. Equal or easier curvature therefore does not restrict the canonical replay, while any tighter curvature contributes continuously. Constant paths retain their exact single canonical p=.5 replay.");
        L();
        L("## G. Variable-curvature lookahead envelope");
        L();
        L("For every sampled future point j the evaluator forms `B(j) = Veff(j)² + 2*a*D(j)`, where `a` is exactly `LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared`. A backward scan retains only strict suffix minima of B. At node i, the first retained future minimum gives `sqrt(B(j) - 2*a*D(i))`; this is algebraically identical to taking the minimum of `sqrt(Veff(j)² + 2*a*(D(j)-D(i)))` over every future sample. The reduced-envelope versus brute-force regression covers sharp and broad early/late demand, unequal separated peaks and a shallow near-constant variation. After the final sampled demand, the existing production drive integration resumes. No turning, slip, yaw, steering, curvature-change or oscillation cost was added.");
        L();
        L("DriveAvailabilityRemainsProductionProgressBased: **YES** — `ContinuousCornerEnvelope.DriveAvailability` retains its frozen p=.5→5/6 progression. This remains an explicit diagnostic limitation, not a repaired curvature-dependent throttle model.");
        L();
        L("## H. Advanced corner-control path feasibility");
        L();
        L("Every integration node resolves the path-level explicit target through production advanced semantics. Ok and Brake continue on the planned spline; Brake uses the production correction profile. RunWide invalidates the candidate as `CornerControlPathDeparture`; Crash invalidates it as `CornerControlCrash`. No threshold is copied into Analysis.");
        L($"LocalCornerControlConstraintActive: **{Yn(result.LocalCornerControlConstraintActive)}**. Search invalidations: departure={result.Search.InvalidCornerControlDepartureCandidates}, crash={result.Search.InvalidCrashCandidates}. Best brake steps={best.BrakeStepCount}; best RunWide/Crash invalidations={best.RunWideInvalidationCount}/{best.CrashInvalidationCount}.");
        L();
        L("## I. Periodic straight lateral closure");
        L();
        L("The old aligned 20 m approach check is removed. Each candidate must traverse the following straight from exit lateral to its own next-entry lateral. The analysis-only straight path is `sqrt(StraightLength^2 + lateralDelta^2)` and `LateralMovementModel` must cover that displacement in actual straight travel time. Failure is `StraightRepositionConstraint`; there is no lateral teleport.");
        L($"Best exit→next-entry lateral: {F(best.Profile[^1].LateralOffsetMeters)}→{F(best.NextEntryLateralMeters)} m; straight path {F(best.StraightRepositionDistanceMeters)} m versus geometric straight {F(result.Geometry.StraightLengthMeters)} m; lateral headroom {F(best.StraightRepositionLateralHeadroomMeters)} m. StraightRepositionConstraintActive: **{Yn(result.StraightRepositionConstraintActive)}**.");
        L();
        L("## J. Constant-path replay gate");
        L();
        L("Control | Production corner s | Replay corner s | Production straight s | Replay straight s | Production sector s | Replay sector s | Delta s | Exit delta m/s | Max profile delta m/s | Pass");
        L("---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:");
        foreach (var item in result.ConstantReplay)
            L($"`{item.Id}` | {F(item.ProductionCornerTimeSeconds)} | {F(item.ExperimentalCornerTimeSeconds)} | {F(item.ProductionStraightTimeSeconds)} | {F(item.ExperimentalStraightTimeSeconds)} | {F(item.ProductionSectorTimeSeconds)} | {F(item.ExperimentalSectorTimeSeconds)} | {Signed(item.SectorDeltaSeconds)} | {Signed(item.ExitSpeedDeltaMetersPerSecond)} | {F(item.MaximumCornerSpeedProfileDeltaMetersPerSecond)} | {Yn(item.Passed)}");
        L();
        L($"ConstantReplayValidation: **{(result.ConstantReplayValidationPassed ? "PASS" : "FAIL")}**. Gates: <= {F(FreeContinuousRacingTrajectoryGeometryExperiment.ConstantReplayToleranceSeconds)} s and <= {F(FreeContinuousRacingTrajectoryGeometryExperiment.ConstantReplaySpeedToleranceMetersPerSecond)} m/s.");
        L();
        L("## K. Variable-curvature sanity controls");
        L();
        L("Control | Valid | Reason | Constraints | First correction p | Peak settled overspeed | Semantics pass");
        L("---|---:|---:|---:|---:|---:|---:");
        foreach (var control in result.SanityControls)
        {
            var value = control.Evaluation;
            L($"{control.Name} | {Yn(value.IsValid)} | {(value.IsValid ? "—" : value.InvalidReason)} | {value.TurningDemandConstraints.Count} | {F(value.FirstCorrectionProgress)} | {F(value.PeakSettledOverspeedRatio)} @ {F(value.PeakSettledOverspeedProgress)} | {Yn(control.SemanticsPassed)}");
        }
        L();
        L($"VariableCurvatureEnvelopeConsistent: **{Yn(result.VariableCurvatureEnvelopeConsistent)}**.");
        foreach (var control in result.SanityControls)
        {
            L();
            L($"### {control.Name} local turning constraint trace");
            ConstraintTrace(control.Evaluation, L, F);
        }
        L();
        L("## L. Old winner after repair");
        L();
        L($"Old winner ID: `{old.Candidate.Id}`. OldWinnerValidityAfterRepair: **{Yn(old.IsValid)}**. OldWinnerNewSectorTime: **{(old.IsValid ? F(old.SectorTimeSeconds) : "N/A")}**. OldWinnerInvalidReason: **{(old.IsValid ? "none" : old.InvalidReason)}**.");
        L($"OldWinnerStillValidAfterRepair: **{Yn(result.OldWinnerStillValidAfterRepair)}**. OldWinnerRejectedAfterRepair: **{Yn(result.OldWinnerRejectedAfterRepair)}**. Peak settled overspeed ratio: {F(old.PeakSettledOverspeedRatio)} at p={F(old.PeakSettledOverspeedProgress)}; peak envelope overspeed ratio: {F(old.PeakEnvelopeOverspeedRatio)} at p={F(old.PeakEnvelopeOverspeedProgress)}.");
        L();
        L("Full detected old-winner turning-constraint list:");
        ConstraintList(old, L, F);
        if (old.IsValid)
        {
            L();
            L("Old-winner execution trace:");
            ConstraintTrace(old, L, F);
        }
        else
        {
            L();
            L("Execution stopped at the reported production-equivalent path-departure/crash gate, so no fictional post-invalidation trace or time is emitted.");
        }
        L();
        L("## M. Expanded deterministic search");
        L();
        L($"Structured starts: **{result.Search.StructuredStartCount}**. Neutral low-discrepancy Halton starts: **{result.Search.LowDiscrepancyStartCount}**. Generated: **{result.Search.StartsGenerated}**; valid initial: **{result.Search.ValidInitialStarts}**; initial invalid geometry/lateral/corner-control/straight-reposition: **{result.Search.InvalidInitialGeometry}/{result.Search.InvalidInitialLateral}/{result.Search.InvalidInitialCornerControl}/{result.Search.InvalidInitialStraightReposition}**.");
        L($"A geometry-diverse deterministic subset of **{result.Search.RefinedStartCount}** valid starts receives whole-path and two coordinate sweeps at each step: `{Vector(result.Search.FinalRefinementStepsMeters)}` m. Memoization is by invariant CandidateId. The search uses no RNG and no external dependency.");
        L();
        L("## N. Search budget and invalidations");
        L();
        L($"CandidatesEvaluated: **{result.Search.CandidatesEvaluated}**; Valid: **{result.Search.ValidCandidates}**; InvalidGeometry: **{result.Search.InvalidGeometryCandidates}**; InvalidLateralExecution: **{result.Search.InvalidLateralExecutionCandidates}**; InvalidCornerControlDeparture: **{result.Search.InvalidCornerControlDepartureCandidates}**; InvalidCrash: **{result.Search.InvalidCrashCandidates}**; InvalidStraightReposition: **{result.Search.InvalidStraightRepositionCandidates}**. Valid refined finals: **{result.Search.FamilyResults.Count}**; refinement rounds: **{result.Search.RefinementRounds}**.");
        L();
        L("## O. Objective convergence");
        L();
        L($"Unchanged-search archive: `{result.Search.ArchivedUnclosedBest.Candidate.Id}` from `{result.Search.ArchivedUnclosedBest.Candidate.SeedFamily}`, controls `{Vector(result.Search.ArchivedUnclosedBest.Candidate.ControlOffsetsMeters)}`, sector **{F(result.Search.ArchivedUnclosedBest.SectorTimeSeconds)} s**. Its initial exhaustive best single-coordinate ±0.125/±0.0625 m improvement was **{F(result.Search.InitialLocalPerturbationImprovementSeconds)} s**; its bounded best-three adjacent-pair ±0.0625 m improvement was **{F(result.Search.InitialPairPerturbationImprovementSeconds)} s**. Additional accepted closure moves: **{result.Search.AdditionalAcceptedLocalMoves}**.");
        L($"Independent refined starts within 0.02 s: **{result.Search.WithinToleranceFamilyCount}**; same-basin count: **{result.Search.SameBasinFamilyCount}**. Final residual best single-coordinate improvement: **{F(result.Search.LocalPerturbationImprovementSeconds)} s**. Final residual adjacent-pair improvement: **{F(result.Search.PairPerturbationImprovementSeconds)} s**. LocalSearchConverged: **{Yn(result.Search.LocalSearchConverged)}**. ObjectiveConvergence: **{Yn(result.Search.ObjectiveConvergence)}**.");
        L();
        L("## P. Geometry convergence");
        L();
        L($"Distinct near-optimal RMS shape basins: **{result.Search.DistinctTopShapeCount}**. GeometryConvergence: **{Yn(result.Search.GeometryConvergence)}**. Geometry convergence is reported separately and is not required when distinct physical paths have equivalent objective values.");
        L();
        L("## Q. Multiple near-optimal basins");
        L();
        L($"MultipleNearOptimalBasins: **{Yn(result.Search.MultipleNearOptimalBasins)}**. SearchConvergenceUncertain: **{Yn(result.Search.SearchConvergenceUncertain)}**. Uncertainty depends on objective convergence, final local improvement, and isolated-start dominance—not merely different shapes.");
        L();
        L("## R. Frozen and #46 controls");
        L();
        L("Control | Valid | Reason | Path m | Corner s | Exit m/s | Straight s | Periodic sector s");
        L("---|---:|---:|---:|---:|---:|---:|---:");
        foreach (var item in result.Controls)
            L($"{item.Candidate.SeedFamily} | {Yn(item.IsValid)} | {(item.IsValid ? "—" : item.InvalidReason)} | {F(item.Path?.TotalLengthMeters ?? 0f)} | {F(item.CornerTimeSeconds)} | {F(item.ExitSpeedMetersPerSecond)} | {F(item.FollowingStraightTimeSeconds)} | {F(item.SectorTimeSeconds)}");
        L();
        L("## S. Refined start finals");
        L();
        L("Seed | Final ID | Sector s | Delta from best s");
        L("---|---:|---:|---:");
        foreach (var family in result.Search.FamilyResults.OrderBy(item => item.Best.SectorTimeSeconds)
                     .ThenBy(item => item.Best.Candidate.Id, StringComparer.Ordinal))
            L($"{family.Family} | `{family.Best.Candidate.Id}` | {F(family.Best.SectorTimeSeconds)} | {Signed(family.Best.SectorTimeSeconds - best.SectorTimeSeconds)}");
        L();
        L("## T. Top 20 repaired trajectories");
        L();
        L("Rank | ID | Seed | Controls m | Path m | Corner s | Exit m/s | Straight s | Sector s | Min R @ p | Constraints | Peak settled ratio | Headroom m");
        L("---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:");
        foreach (var pair in result.Search.TopTwenty.Select((item, index) => (item, index)))
        {
            var item = pair.item;
            L($"{pair.index + 1} | `{item.Candidate.Id}` | {item.Candidate.SeedFamily} | `{Vector(item.Candidate.ControlOffsetsMeters)}` | {F(item.Path!.TotalLengthMeters)} | {F(item.CornerTimeSeconds)} | {F(item.ExitSpeedMetersPerSecond)} | {F(item.FollowingStraightTimeSeconds)} | {F(item.SectorTimeSeconds)} | {F(item.MinimumRadiusMeters)} @ {F(item.MinimumRadiusProgress)} | {item.TurningDemandConstraints.Count} | {F(item.PeakSettledOverspeedRatio)} | {F(item.MinimumLateralExecutionHeadroomMeters)}");
        }
        L();
        L("## U. Repaired best-found trajectory");
        L();
        L($"Best: `{best.Candidate.Id}` from `{best.Candidate.SeedFamily}`. Controls m: `{Vector(best.Candidate.ControlOffsetsMeters)}`.");
        L($"Path {F(bestPath.TotalLengthMeters)} m; entry/exit/next-entry lateral {F(best.Profile[0].LateralOffsetMeters)}/{F(best.Profile[^1].LateralOffsetMeters)}/{F(best.NextEntryLateralMeters)} m. Minimum radius {F(best.MinimumRadiusMeters)} m at p={F(best.MinimumRadiusProgress)}. Minimum speed {F(best.MinimumSpeedMetersPerSecond)} m/s at p={F(best.MinimumSpeedProgress)}. Peak settled overspeed {F(best.PeakSettledOverspeedRatio)} at p={F(best.PeakSettledOverspeedProgress)}; peak envelope overspeed {F(best.PeakEnvelopeOverspeedRatio)} at p={F(best.PeakEnvelopeOverspeedProgress)}. Correction distance/time {F(best.CorrectionDistanceMeters)} m/{F(best.CorrectionTimeSeconds)} s.");
        L($"Corner {F(best.CornerTimeSeconds)} s; following straight {F(best.FollowingStraightTimeSeconds)} s; periodic sector {F(best.SectorTimeSeconds)} s. Exit speed {F(best.ExitSpeedMetersPerSecond)} m/s; +10/+20/+40/end straight {F(best.SpeedTenMetersAfterCornerMetersPerSecond)}/{F(best.SpeedTwentyMetersAfterCornerMetersPerSecond)}/{F(best.SpeedFortyMetersAfterCornerMetersPerSecond)}/{F(best.EndStraightSpeedMetersPerSecond)} m/s.");
        L();
        L("Best retained turning-demand constraints (all binding suffix minima plus the canonical anchor):");
        ConstraintList(best, L, F);
        L();
        L("## V. Repaired best 5%-trajectory table");
        L();
        L("p | Lateral m | Radius m | Settled m/s | Lookahead m/s | Actual m/s | Outcome | Correction | Drive | Net a m/s²");
        L("---|---:|---:|---:|---:|---:|---:|---:|---:|---:");
        foreach (var point in best.Profile)
            L($"{F(point.Progress)} | {F(point.LateralOffsetMeters)} | {F(point.LocalRadiusMeters)} | {F(point.LocalSafeSpeedMetersPerSecond)} | {F(point.LookaheadTargetMetersPerSecond)} | {F(point.SpeedMetersPerSecond)} | {point.CornerControlOutcome} | {Yn(point.CorrectionActive)} | {F(point.DriveAvailability)} | {Signed(point.NetLongitudinalAccelerationMetersPerSecondSquared)}");
        L();
        L("## W. Repaired comparisons");
        L();
        L("### Reviewed-head FCT-49 broad-late-demand diagnostic");
        L();
        L($"`{priorBroad.Candidate.Id}` validity after the complete envelope: **{(priorBroad.IsValid ? "valid" : priorBroad.InvalidReason)}**; corner time: **{(priorBroad.IsValid ? F(priorBroad.CornerTimeSeconds) + " s" : "N/A")}**; path: **{F(priorBroad.Path!.TotalLengthMeters)} m**; sector time: **{(priorBroad.IsValid ? F(priorBroad.SectorTimeSeconds) + " s" : "N/A")}**. The retained list explicitly includes the post-p=.5 broad tightening; validity or ranking is not prescribed.");
        ConstraintList(priorBroad, L, F);
        L();
        L("### Reviewed-head FCT-0B differential-demand diagnostic");
        L();
        L($"`{priorReviewed.Candidate.Id}` exit speed: **{F(priorReviewed.ExitSpeedMetersPerSecond)} m/s**; periodic sector: **{F(priorReviewed.SectorTimeSeconds)} s**. The table exposes the canonical p=.5 radius/capability and every retained sample's canonical replay baseline, local settled capability, squared-capability delta, final effective capability and actual speed. A modest late tightening therefore adjusts the recovered post-apex baseline; it does not reset that baseline to the absolute local settled capability.");
        ConstraintList(priorReviewed, L, F);
        L();
        ComparisonTable(inner, best, "ConstantInner", "BestFoundFreeTrajectory", L, F, Signed);
        L();
        ComparisonTable(bestConstant, best, bestConstant.Candidate.SeedFamily,
            "BestFoundFreeTrajectory", L, F, Signed);
        L();
        if (priorReviewed.IsValid)
        {
            ComparisonTable(priorReviewed, best, "ReviewedHeadWinner-FCT-0B03DE6AD605",
                "BestFoundAfterContinuityRepair", L, F, Signed);
            L();
        }
        else
        {
            L($"Reviewed-head suspect winner `{priorReviewed.Candidate.Id}` is invalid after the continuity repair: `{priorReviewed.InvalidReason}`.");
            L();
        }
        L($"Best constant periodic sector: **{F(bestConstant.SectorTimeSeconds)} s**. Free-minus-best-constant: **{Signed(best.SectorTimeSeconds - bestConstant.SectorTimeSeconds)} s**. FreeTrajectoryBeatsBestConstant: **{Yn(result.FreeTrajectoryBeatsBestConstant)}**. FreeTrajectoryTiesBestConstant: **{Yn(result.FreeTrajectoryTiesBestConstant)}**. BestFoundTrajectoryIsNonConstant: **{Yn(best.IsNonConstant)}**.");
        L();
        L("## X. Periodic convergence and revised interpretation");
        L();
        var repeatedFree = result.RepeatedBestFound.Converged
            ? $"{F(result.RepeatedBestFound.FlyingLapTimeSeconds)} s"
            : "N/A";
        var repeatedConstant = result.RepeatedBestConstant.Converged
            ? $"{F(result.RepeatedBestConstant.FlyingLapTimeSeconds)} s"
            : "N/A";
        var repeatedDelta = result.RepeatedBestFound.Converged && result.RepeatedBestConstant.Converged
            ? $"{Signed(result.RepeatedBestFound.FlyingLapTimeSeconds - result.RepeatedBestConstant.FlyingLapTimeSeconds)} s"
            : "N/A";
        L($"Repeated-lap free: {repeatedFree}, stable entry {F(result.RepeatedBestFound.StableCornerEntrySpeedMetersPerSecond)} m/s, iterations {result.RepeatedBestFound.Iterations}, converged {Yn(result.RepeatedBestFound.Converged)}. Repeated-lap constant: {repeatedConstant}, stable entry {F(result.RepeatedBestConstant.StableCornerEntrySpeedMetersPerSecond)} m/s, iterations {result.RepeatedBestConstant.Iterations}, converged {Yn(result.RepeatedBestConstant.Converged)}. Delta free-constant: {repeatedDelta}. Both replays carry speed through the same physical exit→next-entry straight geometry; lateral position is closed by construction.");
        L($"VariableCurvatureReplayHealthy: **{Yn(result.VariableCurvatureReplayHealthy)}**. FreeTrajectoryGeometrySignalHealthy: **{Yn(result.FreeTrajectoryGeometrySignalHealthy)}**. Classification: `{result.Classification}`. Revised first bottleneck: **{result.FirstActualBottleneck}**.");
        L();
        L("## Y. Revised next subsystem");
        L();
        L($"DecisionCase: **{result.DecisionCase}**. Recommended single next subsystem: **{result.RecommendedSubsystem}**.");
        if (result.DecisionCase == "A")
            L("The repaired evaluator is semantically healthy, the objective converged, and a stable non-constant path beats the best constant. The next step is only a separately reviewed production continuous trajectory geometry integration experiment—not a direct production change.");
        else if (result.DecisionCase is "B" or "C")
            L("The repaired evaluator and objective converged without a decisive geometry-only gain, so the next isolated question is corner turning/slip cost.");
        else if (result.DecisionCase == "E")
            L("Execution feasibility, rather than path economics, dominates the rejected search space; the next isolated question is continuous trajectory execution/planning.");
        else
            L("Evaluator sanity or objective convergence is not yet sufficient for physics interpretation; more evaluator/search repair is required before selecting a new production subsystem.");
        L($"Does continuous geometry alone suffice? **{(result.FreeTrajectoryGeometrySignalHealthy ? (result.FreeTrajectoryBeatsBestConstant ? "YES" : "NO") : "UNCERTAIN")}**. Is turning/slip cost needed now? **{(result.FreeTrajectoryGeometrySignalHealthy ? (result.RecommendedSubsystem == "corner turning/slip cost experiment" ? "YES" : "NO") : "NOT YET DETERMINED")}**. Fixed p=.5 production drive availability remains a diagnostic limitation: **YES**.");
        L();
        L("## Z. Freeze, boundaries and provenance");
        L();
        L("Production freeze: `AdaptiveDecisionModel`, `RiderDecision`, `LateralMovementModel` numeric behavior, `ContinuousCornerEnvelope` numeric behavior, `LongitudinalDynamics`, `TrackEvolution`, `TrackState`, StandingStart, Legacy, mass, resistance, start constants, surface, skills, setup, wear, traffic, contact, decisions, incidents and RNG are unchanged. The support refactor is semantics-preserving and shared by existing production callers. Historical pge-v1 and #38–#46 artifacts remain hash-frozen. The free trajectory remains experiment-only.");
        L();
        L("## AA. Corner-speed capability sensitivity diagnostic");
        L();
        var sensitivity = result.CornerSpeedSensitivity;
        L($"This analysis-only sweep varies the pure advanced settled-turn reference over `[{string.Join(", ", sensitivity.ReferenceSweepMetersPerSecond.Select(value => value.ToString("0", CultureInfo.InvariantCulture)))}] m/s`. Production `AdvancedReferenceTurnSpeedMetersPerSecond` remains exactly **{F(SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond)} m/s**. The 96-start free optimizer and its best result are not inputs to this diagnostic. Every sensitivity path intentionally keeps one fixed canonical turning constraint at p=.5 so the 19–23 capability question stays isolated from optimizer selection and from the independently tested supplemental-constraint semantics; this mode is private to the diagnostic and does not alter the main #47 evaluator.");
        L();
        L("### Fixed controlled paths");
        L();
        L("Control | Kind | Controls m | Exact fixed shape");
        L("---|---|---|---");
        foreach (var control in sensitivity.Controls)
            L($"{control.Name} | {(control.IsDynamic ? "controlled dynamic" : "constant")} | `{Vector(control.Candidate.ControlOffsetsMeters)}` | {control.ShapeDescription}");
        L();
        L("The two `#46` rows are explicitly labelled C2 proxies: they preserve the averaged #46 waypoint pattern at a single fixed 65% amplitude that keeps all five sweep points executable in this finer-grained geometry and periodic straight closure, but they are not a replay of #46's segment-entry sampling. The scale, clearance and every manual control are fixed before the sweep; no shape is selected or optimized per reference value.");
        L();
        L("### Primary sweep");
        L();
        L("Ref m/s | Ref km/h | Settled @ R=31 m/s | ConstantInner sector s | E0-A1-X1 sector s | X1−inner s | Best controlled dynamic | Dynamic sector s | Dynamic−best constant s | ConstantInner flying s | Vmax km/h | Average m/s");
        L("---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|---:");
        foreach (var point in sensitivity.Points)
        {
            var innerPoint = point.Observations.Single(item => item.Name == "ConstantInner").Evaluation;
            L($"{F(point.ReferenceTurnSpeedMetersPerSecond)} | {F(point.ReferenceTurnSpeedMetersPerSecond * 3.6f)} | {F(point.SettledCapabilityAt31MetersPerSecond)} | {F(innerPoint.SectorTimeSeconds)} | {F(point.E0A1X1.Evaluation.SectorTimeSeconds)} | {Signed(point.E0A1X1.Evaluation.SectorTimeSeconds - innerPoint.SectorTimeSeconds)} | {point.BestControlledDynamic.Name} | {F(point.BestControlledDynamic.Evaluation.SectorTimeSeconds)} | {Signed(point.BestControlledDynamic.Evaluation.SectorTimeSeconds - point.BestConstant.Evaluation.SectorTimeSeconds)} | {F(point.ConstantInnerRepeatedLap.FlyingLapTimeSeconds)} | {F(point.ConstantInnerRepeatedLap.MaximumSpeedMetersPerSecond * 3.6f)} | {F(point.ConstantInnerRepeatedLap.AverageSpeedMetersPerSecond)}");
        }
        L();
        L("### All controlled-case outputs");
        L();
        L("Ref | Control | Corner s | Straight s | Sector s | Path m | Min v | Exit v | +10 v | +20 v | +40 v | End v | Min R | Correction m | Correction s");
        L("---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:");
        foreach (var point in sensitivity.Points)
        foreach (var observation in point.Observations)
        {
            var value = observation.Evaluation;
            L($"{F(point.ReferenceTurnSpeedMetersPerSecond)} | {observation.Name} | {F(value.CornerTimeSeconds)} | {F(value.FollowingStraightTimeSeconds)} | {F(value.SectorTimeSeconds)} | {F(value.Path!.TotalLengthMeters)} | {F(value.MinimumSpeedMetersPerSecond)} | {F(value.ExitSpeedMetersPerSecond)} | {F(value.SpeedTenMetersAfterCornerMetersPerSecond)} | {F(value.SpeedTwentyMetersAfterCornerMetersPerSecond)} | {F(value.SpeedFortyMetersAfterCornerMetersPerSecond)} | {F(value.EndStraightSpeedMetersPerSecond)} | {F(value.MinimumRadiusMeters)} | {F(value.CorrectionDistanceMeters)} | {F(value.CorrectionTimeSeconds)}");
        }
        L();
        L("### Required comparisons");
        L();
        L("Ref | Role | Control | Sector s | Δ best constant s | Δ ConstantInner s | Δ exit v | Δ path m | Δ corner s | Δ straight s");
        L("---:|---|---|---:|---:|---:|---:|---:|---:|---:");
        foreach (var point in sensitivity.Points)
        {
            var baseline = point.BestConstant.Evaluation;
            var innerPoint = point.Observations.Single(item => item.Name == "ConstantInner").Evaluation;
            SensitivityComparisonRow(point.ReferenceTurnSpeedMetersPerSecond, "best constant",
                point.BestConstant.Name, baseline, baseline, innerPoint, L, F, Signed);
            SensitivityComparisonRow(point.ReferenceTurnSpeedMetersPerSecond, "#46 wide-exit proxy",
                point.E0A1X1.Name, point.E0A1X1.Evaluation, baseline, innerPoint, L, F, Signed);
            SensitivityComparisonRow(point.ReferenceTurnSpeedMetersPerSecond, "best controlled dynamic",
                point.BestControlledDynamic.Name, point.BestControlledDynamic.Evaluation,
                baseline, innerPoint, L, F, Signed);
        }
        L();
        L("### Absolute-speed and real-data guardrail");
        L();
        L("Ref m/s | Settled @ R=31 m/s | Settled km/h | Fixed corner entry m/s | Minimum corner m/s | Exit m/s | Repeated flying s | Δ flying vs ref 19 s | Vmax km/h | Average m/s");
        L("---:|---:|---:|---:|---:|---:|---:|---:|---:|---:");
        var referenceLap = sensitivity.Points[0].ConstantInnerRepeatedLap.FlyingLapTimeSeconds;
        foreach (var point in sensitivity.Points)
        {
            var innerPoint = point.Observations.Single(item => item.Name == "ConstantInner").Evaluation;
            L($"{F(point.ReferenceTurnSpeedMetersPerSecond)} | {F(point.SettledCapabilityAt31MetersPerSecond)} | {F(point.SettledCapabilityAt31MetersPerSecond * 3.6f)} | {F(result.EntrySpeedMetersPerSecond)} | {F(innerPoint.MinimumSpeedMetersPerSecond)} | {F(innerPoint.ExitSpeedMetersPerSecond)} | {F(point.ConstantInnerRepeatedLap.FlyingLapTimeSeconds)} | {Signed(point.ConstantInnerRepeatedLap.FlyingLapTimeSeconds - referenceLap)} | {F(point.ConstantInnerRepeatedLap.MaximumSpeedMetersPerSecond * 3.6f)} | {F(point.ConstantInnerRepeatedLap.AverageSpeedMetersPerSecond)}");
        }
        L();
        L($"Repository Motoarena clean-data guardrails used descriptively here are approximately Vmax **{F(FreeContinuousRacingTrajectoryGeometryExperiment.RealMotoarenaMaximumSpeedGuardrailKilometersPerHour)} km/h**, average speed **{F(FreeContinuousRacingTrajectoryGeometryExperiment.RealMotoarenaAverageSpeedGuardrailMetersPerSecond)} m/s**, and flying lap **{F(FreeContinuousRacingTrajectoryGeometryExperiment.RealMotoarenaFlyingLapGuardrailSeconds)} s**. They are not pass/fail targets. The dataset contains no measured speed at specific corner positions, so it cannot directly determine the correct `AdvancedReferenceTurnSpeed`.");
        L();
        L("### Trend, crossover and interpretation");
        L();
        var firstSensitivity = sensitivity.Points[0];
        var lastSensitivity = sensitivity.Points[^1];
        var firstDelta = firstSensitivity.BestControlledDynamic.Evaluation.SectorTimeSeconds
            - firstSensitivity.BestConstant.Evaluation.SectorTimeSeconds;
        var lastDelta = lastSensitivity.BestControlledDynamic.Evaluation.SectorTimeSeconds
            - lastSensitivity.BestConstant.Evaluation.SectorTimeSeconds;
        var trend = lastDelta < firstDelta - .02f ? "controlled dynamics gain as capability rises"
            : lastDelta > firstDelta + .02f ? "controlled dynamics lose as capability rises"
            : "controlled-path deltas change only weakly";
        L($"Best-controlled-dynamic minus best-constant changes from **{Signed(firstDelta)} s** at 19 m/s to **{Signed(lastDelta)} s** at 23 m/s ({Signed(lastDelta - firstDelta)} s across the sweep): **{trend}**. Crossover exists: **{Yn(sensitivity.ApproximateCrossoverReferenceSpeedMetersPerSecond is not null)}**; approximate crossover reference: **{(sensitivity.ApproximateCrossoverReferenceSpeedMetersPerSecond is { } crossover ? F(crossover) + " m/s" : "N/A through 23 m/s")}**. The optional crossover search uses fixed 0.25 m/s steps and linear interpolation only between the bracketing deltas.");
        L($"The descriptive S4 global-speed guard band is ±15% around the repository Motoarena values. It is not a fit criterion or pass/fail threshold. ConstantInner at the interpolated crossover clearly breaks that band: **{Yn(sensitivity.GlobalSpeedGuardrailBreaksAtCrossover)}**. First 0.25 m/s sweep point that breaks it: **{(sensitivity.FirstGlobalSpeedGuardrailBreakReferenceMetersPerSecond is { } firstBreak ? F(firstBreak) + " m/s" : "none through 23 m/s")}**; guardrail breaks before the line-economics crossover: **{Yn(sensitivity.GlobalSpeedGuardrailBreaksBeforeCrossover)}**.");
        L("Classification semantics are explicit: S1 = Strong sensitivity (material line-economics crossover before any global-speed break); S2 = Moderate sensitivity (material change without crossover); S3 = Weak sensitivity (no material change); S4 = Global speed breaks first (the guardrail breaks before the observed line-economics crossover). Opposite-direction movement alone never produces S4.");
        L($"Sensitivity classification: **{sensitivity.Classification}** — `{sensitivity.Interpretation}`. Corner-speed capability materially controls line economics: **{Yn(sensitivity.MateriallyControlsLineEconomics)}**. Does raising it alone look like the correct production fix? **{sensitivity.RaisingCapabilityAloneLooksLikeCorrectFix}**.");
        L();
        L("This diagnostic does not choose a production value. Production remains at 19 m/s, and PR #47 remains Draft for independent re-review.");
        L();
        L("## AB. Near-constant turning-demand continuity diagnostic");
        L();
        var continuity = result.TurningDemandContinuity;
        L("A single deterministic natural-cubic C2 family scales the reviewed-head `FCT-0B03DE6AD605` shape continuously to zero. Zero scale is exactly ConstantInner. Each nonzero scale is found by deterministic bisection against the curvature actually sampled from the resulting Cartesian path; no curvature value is fabricated. The family remains inside track bounds and produces the intended late/global curvature peak once the signal is above floating-point noise.");
        L();
        L("Case | Target range 1/m | Actual max−min 1/m | Scale m | Controls m | Retained/binding constraints | Maximum supplemental restriction m/s | Corner s | Straight s | Sector s | Min v | Exit v | +10 v | +20 v | +40 v | End v | First correction p | Brake steps | Validity reason");
        L("---|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---");
        foreach (var observation in continuity.Observations)
        {
            var value = observation.Evaluation;
            var bindingCount = value.TurningDemandConstraints.Count(item => item.IsBindingEnvelopePoint);
            var maximumRestriction = value.TurningDemandConstraints
                .Where(item => !item.IsCanonicalBaseline)
                .Select(item => item.CanonicalBaselineSpeedMetersPerSecond
                    - item.SettledCapabilityMetersPerSecond)
                .DefaultIfEmpty(0f).Max();
            L($"{observation.Name} | {E(observation.TargetCurvatureRangePerMeter)} | {E(observation.ActualCurvatureRangePerMeter)} | {E(observation.PerturbationScaleMeters)} | `{Vector(observation.ControlOffsetsMeters)}` | {value.TurningDemandConstraints.Count}/{bindingCount} | {F(maximumRestriction)} | {F(value.CornerTimeSeconds)} | {F(value.FollowingStraightTimeSeconds)} | {F(value.SectorTimeSeconds)} | {F(value.MinimumSpeedMetersPerSecond)} | {F(value.ExitSpeedMetersPerSecond)} | {F(value.SpeedTenMetersAfterCornerMetersPerSecond)} | {F(value.SpeedTwentyMetersAfterCornerMetersPerSecond)} | {F(value.SpeedFortyMetersAfterCornerMetersPerSecond)} | {F(value.EndStraightSpeedMetersPerSecond)} | {F(value.FirstCorrectionProgress)} | {value.BrakeStepCount} | {(value.IsValid ? "valid" : value.InvalidReason)}");
        }
        L();
        L($"Canonical p=.5 baseline present in every case: **{Yn(continuity.CanonicalBaselineAlwaysPresent)}**. Maximum sector delta from ConstantInner across the entire near-constant family through 2× the former tolerance: **{F(continuity.MaximumNearConstantSectorDeltaFromConstantSeconds)} s**. The 2× case's maximum supplemental speed restriction is nonzero but only **{F(continuity.TwiceToleranceSupplementalRestrictionMetersPerSecond)} m/s**; <=0.010 m/s and <=0.005 s no-steep-penalty gate: **{Yn(continuity.NearConstantPenaltyGatePassed)}**. Differential restriction converges to zero: **{Yn(continuity.DifferentialRestrictionConverges)}**. Smallest perturbation vs constant: corner Δ **{F(continuity.SmallestPerturbationCornerDeltaFromConstantSeconds)} s**, sector Δ **{F(continuity.SmallestPerturbationSectorDeltaFromConstantSeconds)} s**, exit-speed Δ **{F(continuity.SmallestPerturbationExitSpeedDeltaFromConstantMetersPerSecond)} m/s**, maximum 5%-profile speed Δ **{F(continuity.SmallestPerturbationMaximumSpeedProfileDeltaFromConstantMetersPerSecond)} m/s**; convergence gate: **{Yn(continuity.ConvergesToConstant)}**.");
        L($"Just below vs just above the former {E(continuity.OldTolerancePerMeter)} 1/m branch boundary: sector Δ **{F(continuity.BelowAboveSectorDeltaSeconds)} s**, maximum 5%-profile speed Δ **{F(continuity.BelowAboveMaximumSpeedProfileDeltaMetersPerSecond)} m/s**; <=0.005/<=0.005 gate: **{Yn(continuity.OldThresholdContinuityPassed)}**. No discrete advantage or penalty is introduced by crossing the former threshold.");
        return output.ToString();
    }

    private static void ConstraintList(
        FreeTrajectoryEvaluation evaluation,
        Action<string> line,
        Func<double, string> format)
    {
        var canonical = evaluation.TurningDemandConstraints.Single(item => item.IsCanonicalBaseline);
        line("Constraint p | Role | Binding | Distance from entry m | Local radius m | Canonical radius m | Canonical baseline m/s | Local settled m/s | Delta v² (m/s)² | Final effective m/s | Backward-reachable entry target m/s | Actual m/s");
        line("---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:");
        foreach (var item in evaluation.TurningDemandConstraints)
        {
            var trace = evaluation.TurningConstraintTrace.FirstOrDefault(candidate =>
                MathF.Abs(candidate.ConstraintProgress - item.Progress) <= 1e-6f);
            line($"{format(item.Progress)} | {(item.IsCanonicalBaseline ? "canonical anchor" : "sampled demand")} | {(item.IsBindingEnvelopePoint ? "YES" : "NO")} | {format(item.DistanceMeters)} | {format(item.LocalRadiusMeters)} | {format(canonical.LocalRadiusMeters)} | {format(item.CanonicalBaselineSpeedMetersPerSecond)} | {format(item.LocalSettledCapabilityMetersPerSecond)} | {format(item.SettledCapabilitySquaredDeltaMetersSquaredPerSecondSquared)} | {format(item.SettledCapabilityMetersPerSecond)} | {(item.IsBindingEnvelopePoint ? format(item.BackwardReachableEntryTargetMetersPerSecond) : "not binding")} | {(trace is null ? "not reached" : format(trace.ActualSpeedMetersPerSecond))}");
        }
    }

    private static void ConstraintTrace(
        FreeTrajectoryEvaluation evaluation,
        Action<string> line,
        Func<double, string> format)
    {
        line("Constraint p | Local radius m | Settled capability m/s | Lookahead target at entry m/s | Actual speed m/s | Outcome | Distance to constraint m");
        line("---|---:|---:|---:|---:|---:|---:");
        if (evaluation.TurningConstraintTrace.Count == 0)
        {
            foreach (var item in evaluation.TurningDemandConstraints)
                line($"{format(item.Progress)} | {format(item.LocalRadiusMeters)} | {format(item.SettledCapabilityMetersPerSecond)} | — | — | not reached | {format(item.DistanceMeters)}");
            return;
        }
        foreach (var item in evaluation.TurningConstraintTrace)
            line($"{format(item.ConstraintProgress)} | {format(item.LocalRadiusMeters)} | {format(item.SettledCapabilityMetersPerSecond)} | {format(item.LookaheadTargetAtEntryMetersPerSecond)} | {format(item.ActualSpeedMetersPerSecond)} | {item.Outcome} | {format(item.DistanceToConstraintMeters)}");
    }

    private static void ComparisonTable(
        FreeTrajectoryEvaluation first,
        FreeTrajectoryEvaluation second,
        string firstName,
        string secondName,
        Action<string> line,
        Func<double, string> format,
        Func<double, string> signed)
    {
        line("Metric | " + firstName + " | " + secondName + " | Free delta");
        line("---|---:|---:|---:");
        line($"Entry lateral m | {format(first.Profile[0].LateralOffsetMeters)} | {format(second.Profile[0].LateralOffsetMeters)} | {signed(second.Profile[0].LateralOffsetMeters - first.Profile[0].LateralOffsetMeters)}");
        line($"Exit lateral m | {format(first.Profile[^1].LateralOffsetMeters)} | {format(second.Profile[^1].LateralOffsetMeters)} | {signed(second.Profile[^1].LateralOffsetMeters - first.Profile[^1].LateralOffsetMeters)}");
        line($"Next-entry lateral m | {format(first.NextEntryLateralMeters)} | {format(second.NextEntryLateralMeters)} | {signed(second.NextEntryLateralMeters - first.NextEntryLateralMeters)}");
        line($"Corner path m | {format(first.Path!.TotalLengthMeters)} | {format(second.Path!.TotalLengthMeters)} | {signed(second.Path.TotalLengthMeters - first.Path.TotalLengthMeters)}");
        line($"Straight reposition path m | {format(first.StraightRepositionDistanceMeters)} | {format(second.StraightRepositionDistanceMeters)} | {signed(second.StraightRepositionDistanceMeters - first.StraightRepositionDistanceMeters)}");
        line($"Corner time s | {format(first.CornerTimeSeconds)} | {format(second.CornerTimeSeconds)} | {signed(second.CornerTimeSeconds - first.CornerTimeSeconds)}");
        line($"Exit speed m/s | {format(first.ExitSpeedMetersPerSecond)} | {format(second.ExitSpeedMetersPerSecond)} | {signed(second.ExitSpeedMetersPerSecond - first.ExitSpeedMetersPerSecond)}");
        line($"Following straight s | {format(first.FollowingStraightTimeSeconds)} | {format(second.FollowingStraightTimeSeconds)} | {signed(second.FollowingStraightTimeSeconds - first.FollowingStraightTimeSeconds)}");
        line($"Periodic sector s | {format(first.SectorTimeSeconds)} | {format(second.SectorTimeSeconds)} | {signed(second.SectorTimeSeconds - first.SectorTimeSeconds)}");
        line($"Minimum radius m | {format(first.MinimumRadiusMeters)} | {format(second.MinimumRadiusMeters)} | {signed(second.MinimumRadiusMeters - first.MinimumRadiusMeters)}");
        line($"Peak settled overspeed ratio | {format(first.PeakSettledOverspeedRatio)} | {format(second.PeakSettledOverspeedRatio)} | {signed(second.PeakSettledOverspeedRatio - first.PeakSettledOverspeedRatio)}");
        line($"Correction distance/time | {format(first.CorrectionDistanceMeters)} / {format(first.CorrectionTimeSeconds)} | {format(second.CorrectionDistanceMeters)} / {format(second.CorrectionTimeSeconds)} | —");
    }

    private static void SensitivityComparisonRow(
        float reference,
        string role,
        string name,
        FreeTrajectoryEvaluation value,
        FreeTrajectoryEvaluation bestConstant,
        FreeTrajectoryEvaluation constantInner,
        Action<string> line,
        Func<double, string> format,
        Func<double, string> signed)
    {
        line($"{format(reference)} | {role} | {name} | {format(value.SectorTimeSeconds)} | {signed(value.SectorTimeSeconds - bestConstant.SectorTimeSeconds)} | {signed(value.SectorTimeSeconds - constantInner.SectorTimeSeconds)} | {signed(value.ExitSpeedMetersPerSecond - bestConstant.ExitSpeedMetersPerSecond)} | {signed(value.Path!.TotalLengthMeters - bestConstant.Path!.TotalLengthMeters)} | {signed(value.CornerTimeSeconds - bestConstant.CornerTimeSeconds)} | {signed(value.FollowingStraightTimeSeconds - bestConstant.FollowingStraightTimeSeconds)}");
    }
}
