using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public static class DynamicCornerTrajectoryGeometryReport
{
    public static string Render(DynamicCornerTrajectoryGeometryExperimentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var b = new StringBuilder();
        void H(char letter, string title) => b.Append("## ").Append(letter).Append(". ").Append(title).Append("\n\n");
        void L(string value = "") => b.Append(value).Append('\n');
        void Table(string header, IEnumerable<string> rows)
        {
            L(header);
            L(string.Join('|', header.Split('|').Select((_, index) => index == 0 ? "---" : "---:")));
            foreach (var row in rows) L(row);
            L();
        }

        L("# Dynamic corner trajectory geometry baseline (#46)");
        L();
        L($"Base main: `{DynamicCornerTrajectoryGeometryExperiment.BaseMainSha}`. Analysis-only production observation; all calibration-only physics adjustments are null.");
        L();

        H('A', "Game design objective");
        L("This manager-game benchmark asks whether the current production simulation creates useful entry → apex-waypoint → exit trajectory economics. Gameplay variety is the objective; realism is a guardrail, and no candidate is promoted to production.");
        L();

        H('B', "Why constant lane is not an optimal-trajectory proof");
        L("#45 constant-lane physical best is a reference-path result, not a proof of globally optimal speedway trajectory. `HoldLane L0` means a small radius throughout the corner; it does not mean an optimal inner speedway path.");
        L();

        H('C', "Existing production trajectory architecture");
        L("The diagnostic model supplies only the existing integer `TargetLane`. Production still resolves `TargetLane → PlannedLane → Lane → continuous LateralPosition` through the unchanged planner, `LateralMovementModel`, `SegmentPhysics`, `ContinuousCornerEnvelope`, and `HeatSimulator`. Actual positions below are observations, never target aliases.");
        L();

        H('D', "Diagnostic waypoint model");
        L("Straight targets Entry; TurnEntry targets Apex; TurnMiddle and TurnExit target Exit; the following straight prepares the next Entry. The model is internal, stateless, analysis-only, and does not add Entry/Apex/Exit fields to `RiderDecision`.");
        Table("Reference | Target plan", DynamicCornerTrajectoryGeometryExperiment.NamedReferences
            .Select(item => $"{item.Key} | `{item.Value.Id}`"));

        H('E', "125-plan uniform search");
        L($"Target plans: **{result.TargetPlans.Count}**. Every E/A/X coordinate spans 0..4 in deterministic E-major, A-middle, X-minor order. Each candidate starts from a fresh baseline `TrackState`, neutral setup, Balanced 50/50/50/50/50/50 skills, one rider, four laps, Dry weather, zero incidents and seed {DynamicCornerTrajectoryGeometryExperiment.FixedSeed}.");
        L();

        H('F', "Unique actual trajectory count");
        L($"UniqueActualTrajectoryCount: **{result.UniqueUniformTrajectories.Count}**; duplicate target plans: **{result.DuplicateTargetPlanCount}**; planner-bound plans: **{result.PlannerBoundPlanCount}**.");
        L($"TrajectoryPlannerResolutionTooCoarse: **{Yes(result.TrajectoryPlannerResolutionTooCoarse)}**.");
        L();

        H('G', "Constant reference trajectories");
        Table("Reference | Plan | Actual fingerprint | 4-lap distance m | L2 | L3 | L4 | Flying s | Vmax m/s", ConstantRows(result));
        L($"#45 constant reference reproduction: **{Yes(result.ConstantReferencesReproduce45)}**. Distances remain strictly increasing L0 → L4.");
        L();

        H('H', "Named dynamic trajectories");
        Table("Reference | Plan | Actual E/A/X | Exit release | Flying s | Delta to ConstantInner s", NamedRows(result));

        H('I', "Top 15 actual trajectories");
        Table("Rank | Target | Actual fingerprint | E/A/X | Flying s | Heat s | 4-lap m | Vmax | Entry | Apex | Min@p | Exit | Correction m/s | Brake/RunWide/Crash", TopRows(result));

        H('J', "Best dynamic vs ConstantInner");
        var dynamicDelta = result.BestDynamic.FlyingLapMedianSeconds - result.ConstantInner.FlyingLapMedianSeconds;
        L($"BestDynamicTrajectory: `{result.BestDynamic.Plan.Id}` / `{result.BestDynamic.ActualTrajectoryFingerprint}`.");
        L($"BestDynamicFlying: **{N(result.BestDynamic.FlyingLapMedianSeconds)} s**; ConstantInnerFlying: **{N(result.ConstantInner.FlyingLapMedianSeconds)} s**; Delta dynamic - inner: **{Signed(dynamicDelta)} s**.");
        L($"DynamicTrajectoryAdvantageObserved: **{Yes(result.DynamicTrajectoryAdvantageObserved)}**; ConstantInnerDominates: **{Yes(result.ConstantInnerDominates)}**; gameplay diagnostic tolerance: {N(DynamicCornerTrajectoryGeometryExperiment.GameplayTieToleranceSeconds)} s/flying lap.");
        L();

        H('K', "Wide-exit trajectories");
        L("ExitRelease = actual exit lateral position - actual entry lateral position. `< -0.25` is inward finish, `-0.25..+0.25` approximately constant, and `> +0.25` outward release / wide exit; this grouping gives no bonus.");
        if (result.BestWideExit is { } wide)
        {
            L($"BestWideExitTrajectory: `{wide.Plan.Id}` / `{wide.ActualTrajectoryFingerprint}`; Flying **{N(wide.FlyingLapMedianSeconds)} s**; delta to overall **{Signed(wide.FlyingLapMedianSeconds - result.OverallBest.FlyingLapMedianSeconds)} s**; delta to ConstantInner **{Signed(wide.FlyingLapMedianSeconds - result.ConstantInner.FlyingLapMedianSeconds)} s**.");
        }
        else L("No observed actual trajectory crossed the +0.25 wide-exit threshold.");
        L($"IsOverallBestWideExit: **{Yes(result.OverallBest.IsWideExit)}**; WideExitBenefitObserved: **{Yes(result.WideExitBenefitObserved)}**.");
        L();

        H('L', "Entry/apex/exit actual geometry");
        Table("Rank | Corner | Entry | p~.333 | p=.500 | p~.667 | Exit | Entry r | Apex r | Exit r | Entry v | Apex v | Exit v", GeometryRows(result));

        H('M', "Radius diagnostics");
        Table("Rank | Plan | Min radius m | Mean time-weighted radius m | Apex radius m | Exit radius m", result.TopFifteen.Select((item, index) =>
            $"{index + 1} | `{item.Plan.Id}` | {N(item.MinimumRadiusMeters)} | {N(item.MeanTimeWeightedRadiusMeters)} | {N(item.ApexRadiusMeters)} | {N(item.ExitRadiusMeters)}"));
        L("Radii use the existing `LaneModel.TurnArcRadiusMeters(actual LateralPosition)` mapping. No new geometry is introduced.");
        L();

        H('N', "Correction burden");
        Table("Rank | Plan | Correction distance m | Correction time s | Pre-apex m | Post-apex m", result.TopFifteen.Select((item, index) =>
            $"{index + 1} | `{item.Plan.Id}` | {N(item.CorrectionDistanceMeters)} | {N(item.CorrectionTimeSeconds)} | {N(item.PreApexCorrectionDistanceMeters)} | {N(item.PostApexCorrectionDistanceMeters)}"));

        H('O', "Exit quality");
        Table("Rank | Plan | Exit speed m/s | Speed +10 m m/s | Apex→exit s | First 20 m straight s", result.TopFifteen.Select((item, index) =>
            $"{index + 1} | `{item.Plan.Id}` | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.SpeedTenMetersAfterCornerMetersPerSecond)} | {N(item.ApexToExitTimeSeconds)} | {N(item.FirstTwentyMetersStraightTimeSeconds)}"));
        L("The +10 m and 20 m values use deterministic interpolation between production trace endpoints and do not change production stepping or timing.");
        L();

        H('P', "Lateral-acceleration proxy");
        Table("Rank | Plan | Peak v²/r m/s² | Mean time-weighted v²/r m/s²", result.TopFifteen.Select((item, index) =>
            $"{index + 1} | `{item.Plan.Id}` | {N(item.PeakLateralAccelerationProxyMetersPerSecondSquared)} | {N(item.MeanTimeWeightedLateralAccelerationProxyMetersPerSecondSquared)}"));
        L("`v²/r` is named only a lateral-acceleration proxy. It is not tyre force, lean, yaw, slip angle or slip ratio.");
        L();

        H('Q', "Planner binding");
        Table("Rank | Plan | Target transitions | Planned transitions | Actual waypoint arrivals | Binding count | Binding fraction", result.TopFifteen.Select((item, index) =>
            $"{index + 1} | `{item.Plan.Id}` | {item.TargetTransitions} | {item.PlannedLaneTransitions} | {item.ActualWaypointArrivals} | {item.PlannerCapBindingCount} | {N(item.PlannerCapBindingFraction)}"));
        L("TrajectoryPlannerResolutionTooCoarse remains a separate search-resolution diagnostic; the global bound-plan fraction does not by itself identify why ConstantInner wins.");
        L();

        L("## Planner-independent trajectory subset");
        L();
        L($"PlannerIndependentTrajectoryCount: **{result.PlannerIndependentTrajectories.Count}**. Every member has `PlannerCapBindingCount == 0`.");
        L($"Best planner-independent overall: `{result.BestPlannerIndependent.Plan.Id}`; best dynamic: `{result.BestPlannerIndependentDynamic?.Plan.Id ?? "—"}`; best wide exit: `{result.BestPlannerIndependentWideExit?.Plan.Id ?? "—"}`.");
        Table("Rank | Plan | Actual fingerprint | E/A/X | Flying s | 4-lap m | Exit m/s | Binding count",
            result.TopTenPlannerIndependent.Select((item, index) =>
                $"{index + 1} | `{item.Plan.Id}` | `{item.ActualTrajectoryFingerprint}` | {Triplet(item)} | {N(item.FlyingLapMedianSeconds)} | {N(item.FourLapDistanceMeters)} | {N(item.CornerExitSpeedMetersPerSecond)} | {item.PlannerCapBindingCount}"));

        L("## Planner-independent ConstantInner comparison");
        L();
        Table("Role | Target plan | Actual fingerprint | Flying s | 4-lap production distance m | Diagonal extra m/lap | Diagonal time s | Actual E/A/X",
            PlannerIndependentIdentityRows(result));
        Table("Role | Entry/Apex/Exit radius m | Minimum/mean radius m | Entry/Apex/Minimum@progress/Exit speed m/s",
            PlannerIndependentGeometryRows(result));
        Table("Role | Correction distance/time | Pre/post-apex correction m | Apex→exit s | +10 m speed | First 20 m s | Peak/mean v²/r | Brake/RunWide/Crash | Binding count/fraction",
            PlannerIndependentEconomicsRows(result));

        L("## Exit benefit versus total-lap cost");
        L();
        if (result.WideExitBenefitCost is { } benefit && result.BestPlannerIndependentWideExit is { } benefitWide)
        {
            L($"Compared with ConstantInner, planner-independent wide exit `{benefitWide.Plan.Id}` gains **{Signed(benefit.ExitSpeedGainMetersPerSecond)} m/s** at corner exit and **{Signed(benefit.TenMeterSpeedGainMetersPerSecond)} m/s** after 10 m, saving **{Signed(benefit.FirstTwentyMetersStraightTimeGainSeconds)} s** over the first 20 m of the straight. It nevertheless loses **{Signed(benefit.FlyingLossSeconds)} s** on the flying lap while production records **{Signed(benefit.ProductionDistanceDifferenceMeters)} m** more over four laps.");
            Table("Metric | Value", new[]
            {
                $"ExitSpeedGain | {Signed(benefit.ExitSpeedGainMetersPerSecond)} m/s",
                $"TenMeterSpeedGain | {Signed(benefit.TenMeterSpeedGainMetersPerSecond)} m/s",
                $"First20mStraightTimeGain | {Signed(benefit.FirstTwentyMetersStraightTimeGainSeconds)} s",
                $"FlyingLoss | {Signed(benefit.FlyingLossSeconds)} s",
                $"ProductionDistanceDifference | {Signed(benefit.ProductionDistanceDifferenceMeters)} m / four laps",
            });
        }
        else L("No planner-independent wide-exit comparison is available.");
        L();

        L("## Can planner explain ConstantInner dominance?");
        L();
        L($"PlannerIndependentDynamicTrajectoryExists: **{Yes(result.PlannerIndependentDynamicTrajectoryExists)}**.");
        L($"PlannerIndependentWideExitExists: **{Yes(result.PlannerIndependentWideExitExists)}**.");
        L($"PlannerCanExplainConstantInnerDominance: **{Yes(result.PlannerCanExplainConstantInnerDominance)}**. A planner-independent dynamic path is sufficient to test the basic mechanism even though **{result.PlannerBoundPlanCount}/{result.TargetPlans.Count}** target plans remain bound.");
        L();

        H('R', "Diagonal-distance omission estimate");
        Table("Rank | Plan | Extra diagonal m/lap | Time equivalent s | Margin vs inner s", result.TopFifteen.Select((item, index) =>
            $"{index + 1} | `{item.Plan.Id}` | {N(item.EstimatedExtraDiagonalDistancePerLapMeters)} | {N(item.EstimatedExtraTimeEquivalentSeconds)} | {Signed(item.FlyingLapMedianSeconds - result.ConstantInner.FlyingLapMedianSeconds)}"));
        L($"DiagonalDistanceOmissionCouldExplainWinner: **{Yes(result.DiagonalDistanceOmissionCouldExplainWinner)}**. This `sqrt(ds²+dy²)-ds` estimate is observation-only and never feeds a simulated time.");
        L();

        H('S', "Surface-dependent trajectory ranking");
        Table("Surface | Severity | Best | Best constant | Best wide exit | Best-constant s | Changed vs Uniform", result.SurfaceResults.Select(item =>
            $"{item.ProfileId} | {N(item.Severity)} | `{item.BestTrajectory.Plan.Id}` | `{item.BestConstantTrajectory.Plan.Id}` | {(item.BestWideExitTrajectory is null ? "—" : $"`{item.BestWideExitTrajectory.Plan.Id}`")} | {Signed(item.BestVsConstantDeltaSeconds)} | {Yes(item.DidOptimalTrajectoryChange)}"));
        L($"SurfaceDependentTrajectoryOptimum: **{Yes(result.SurfaceDependentTrajectoryOptimum)}**.");
        var strongContrast = result.SurfaceResults.Where(item => item.Severity == .5f)
            .All(item => !item.DidOptimalTrajectoryChange)
            && result.SurfaceResults.Where(item => item.Severity == 1f)
                .Any(item => item.DidOptimalTrajectoryChange);
        L($"LineSwitchRequiresStrongSurfaceContrast: **{Yes(strongContrast)}**.");
        L();

        H('T', "Rider-skill-dependent trajectory ranking");
        Table("Archetype | Best | Top 3 | Top-3 spread s", result.SkillResults.Select(item =>
            $"{item.Archetype} | `{item.TopThree[0].Plan.Id}` | {string.Join(" → ", item.TopThree.Select(value => $"`{value.Plan.Id}` ({N(value.FlyingLapMedianSeconds)})"))} | {N(item.TopThreeSpreadSeconds)}"));
        L($"SkillDependentTrajectoryOptimum: **{Yes(result.SkillDependentTrajectoryOptimum)}**. SlideControl receives no added speed multiplier.");
        L();

        H('U', "Setup sanity");
        Table("TractionBias | Best trajectory | Flying s | Apex m/s | Exit m/s | Correction m", result.SetupResults.Select(item =>
            $"{N(item.TractionBias)} | `{item.BestTrajectory.Plan.Id}` | {N(item.BestTrajectory.FlyingLapMedianSeconds)} | {N(item.BestTrajectory.TrueApexSpeedMetersPerSecond)} | {N(item.BestTrajectory.CornerExitSpeedMetersPerSecond)} | {N(item.BestTrajectory.CorrectionDistanceMeters)}"));
        L("Gearing is neutral and production setup formulas are unchanged.");
        L();

        H('V', "Gameplay interpretation");
        var widerExit = result.BestWideExit;
        L($"1. Is constant L0 globally best among the studied trajectories? **{Yes(result.OverallBest.Plan == DynamicCornerTrajectoryGeometryExperiment.NamedReferences["ConstantInner"])}**.");
        L($"2. Can a narrow-entry/wide-exit actual trajectory beat constant L0? **{Yes(widerExit is not null && widerExit.FlyingLapMedianSeconds < result.ConstantInner.FlyingLapMedianSeconds)}**.");
        L($"3. Does the best wide-exit path have a higher exit speed than constant L0? **{Yes(widerExit is not null && widerExit.CornerExitSpeedMetersPerSecond > result.ConstantInner.CornerExitSpeedMetersPerSecond)}**.");
        L($"4. Does that speed compensate for production distance? **{Yes(widerExit is not null && widerExit.FlyingLapMedianSeconds < result.ConstantInner.FlyingLapMedianSeconds)}**.");
        L($"5. Does optimum change with surface? **{Yes(result.SurfaceDependentTrajectoryOptimum)}**.");
        L($"6. Does optimum change with rider profile? **{Yes(result.SkillDependentTrajectoryOptimum)}**.");
        L($"7. Does the planner limit trajectory realization? **{Yes(result.TrajectoryPlannerResolutionTooCoarse)}**.");
        L($"8. Can missing diagonal distance change interpretation? **{Yes(result.DiagonalDistanceOmissionCouldExplainWinner)}**.");
        L($"9. Does a planner-independent dynamic trajectory exist? **{Yes(result.PlannerIndependentDynamicTrajectoryExists)}**.");
        L($"10. Does a planner-independent wide exit exist? **{Yes(result.PlannerIndependentWideExitExists)}**.");
        L($"11. Can planner binding explain ConstantInner dominance? **{Yes(result.PlannerCanExplainConstantInnerDominance)}**.");
        L($"12. Does the winning comparison use within-segment lateral change? **{Yes(result.DynamicPathUsesWithinSegmentLateralChange)}**.");
        L($"13. Is changing radius fully integrated inside each segment? **{Yes(!result.WithinSegmentChangingRadiusNotFullyIntegrated)}**.");
        L($"14. Is a corner-trajectory economics mismatch observed? **{Yes(result.CornerTrajectoryEconomicsMismatch)}**.");
        L("Smaller radius requires more turning demand; wider release can allow higher modeled corner/exit speed. No real trajectory telemetry exists here, and the PGE dataset cannot select the #46 winner.");
        L();

        H('W', "Production limitations");
        L("Surface, radius, wear and path distance are sampled from segment-entry lateral position; production does not integrate changing within-segment radius/surface or diagonal/spiral distance. Actual waypoint positions between segment endpoints use a canonical observation interpolation. There is no yaw, lean, slip angle, tyre slip, traffic, blocking, overtaking or natural-meeting wear comparison in this benchmark.");
        L();

        L("## Segment-entry trajectory sampling limitation");
        L();
        L($"WithinSegmentTrajectorySamplingLimitation: **{Yes(result.WithinSegmentTrajectorySamplingLimitation)}**. Production resolves surface, segment length and `ContinuousCornerEnvelope.Create(...)` from segment-entry `LateralPosition`; it does not continuously resample radius, safe speed, surface or path length as lateral position changes within that segment.");
        L($"DynamicPathUsesWithinSegmentLateralChange: **{Yes(result.DynamicPathUsesWithinSegmentLateralChange)}**.");
        var sampledPaths = new[]
        {
            (Role: "Best planner-independent dynamic", Value: result.BestPlannerIndependentDynamic),
            (Role: "Best planner-independent wide exit", Value: result.BestPlannerIndependentWideExit),
        }
        .Where(item => item.Value is not null)
        .GroupBy(item => item.Value!.ActualTrajectoryFingerprint, StringComparer.Ordinal)
        .Select(group => group.First())
        .ToArray();
        foreach (var sampled in sampledPaths)
        {
            var path = sampled.Value!;
            var changing = path.TurnSegments.Count(item => item.HasWithinSegmentLateralChange);
            L($"{sampled.Role} `{path.Plan.Id}` changes lateral position inside **{changing}/{path.TurnSegments.Count}** observed turn segments.");
            Table("Corner | Segment | Type | Entry lateral | Observed exit lateral | Delta lateral | Physical change m | Entry radius m | Observed exit radius m | Production entry surface G/R/M/E | Changed within segment",
                TurnSegmentRows(path));
        }
        L("These observations can expose a delayed modeled benefit from opening the path; they diagnose sampling granularity and do not prove that it explains the entire time loss.");
        L();

        L("## Within-segment radius limitation");
        L();
        L($"WithinSegmentChangingRadiusNotFullyIntegrated: **{Yes(result.WithinSegmentChangingRadiusNotFullyIntegrated)}**. When actual lateral position changes during a turn segment, production still uses the entry-position radius, safe-speed context, surface and segment length for that segment rather than integrating them continuously along the observed path.");
        L("This leaves coarse within-segment radius, surface and path-geometry sampling as an unresolved explanation alongside correction economics and a possible later turning/slip-cost hypothesis.");
        L();

        H('X', "Failure-layer diagnosis");
        L("## Revised failure-layer diagnosis");
        L();
        L($"Classification: `{result.Classification}`.");
        L($"First actual bottleneck: **{result.FirstActualBottleneck}**. The rule checks, in order: any dynamic actual path; a planner-independent dynamic path; a planner-independent outward-release path; its radius/apex/exit-speed signal; diagonal/path-distance accounting; and only then unresolved corner-trajectory economics.");
        L($"PlannerCanExplainConstantInnerDominance: **{Yes(result.PlannerCanExplainConstantInnerDominance)}**; CornerTrajectoryEconomicsMismatch: **{Yes(result.CornerTrajectoryEconomicsMismatch)}**.");
        L($"CornerAsymmetryUnexpected: **{Yes(result.CornerAsymmetryUnexpected)}**; TrajectoryGeometrySignalWeak: **{Yes(result.TrajectoryGeometrySignalWeak)}**.");
        L();

        H('Y', "Recommended #47 subsystem");
        L("## Revised #47 recommendation");
        L();
        L($"Open exactly one next subsystem: **{result.RecommendedSubsystem}**. This is an experiment before any production rewrite. Do not infer a definitive slip model requirement or change `AdaptiveDecisionModel` until continuous trajectory/radius sampling has isolated the remaining economics.");
        L("Future #47 question: what happens to the ConstantInner versus narrow-entry / wide-exit ranking when the existing production corner model is observed in an experimental variant where effective radius, surface and path geometry change together with actual continuous `LateralPosition` inside the arc?");
        L("Fallback hypothesis: if continuous trajectory/radius sampling still leaves ConstantInner decisively best, investigate a separate `corner turning/slip cost` for tight-radius or higher-turning-demand riding. #46 does not implement or claim that model is required.");
        L();

        H('Z', "Frozen systems");
        L("Production behavior changed: **NO**. `AdaptiveDecisionModel`, `RiderDecision`, `LateralMovementModel`, `SegmentPhysics`, `ContinuousCornerEnvelope`, `LongitudinalDynamics`, `TrackEvolution`, `TrackState`, StandingStart, setup, wear, incidents, contact, Legacy, historical reports and `pge-v1` are unchanged. All calibration-only adjustments remain null. The report is deterministic, invariant-culture, LF-only, and derived from the production path.");

        return b.ToString();
    }

    private static IEnumerable<string> ConstantRows(DynamicCornerTrajectoryGeometryExperimentResult result)
    {
        foreach (var pair in DynamicCornerTrajectoryGeometryExperiment.NamedReferences.Take(5))
        {
            var value = result.UniformPlans.Single(item => item.Plan == pair.Value);
            yield return $"{pair.Key} | `{value.Plan.Id}` | `{value.ActualTrajectoryFingerprint}` | {N(value.FourLapDistanceMeters)} | {N(value.FlyingLapTimesSeconds[0])} | {N(value.FlyingLapTimesSeconds[1])} | {N(value.FlyingLapTimesSeconds[2])} | {N(value.FlyingLapMedianSeconds)} | {N(value.MaximumSpeedMetersPerSecond)}";
        }
    }

    private static IEnumerable<string> NamedRows(DynamicCornerTrajectoryGeometryExperimentResult result)
    {
        foreach (var pair in DynamicCornerTrajectoryGeometryExperiment.NamedReferences.Skip(5))
        {
            var value = result.UniformPlans.Single(item => item.Plan == pair.Value);
            yield return $"{pair.Key} | `{value.Plan.Id}` | {Triplet(value)} | {Signed(value.ExitRelease)} | {N(value.FlyingLapMedianSeconds)} | {Signed(value.FlyingLapMedianSeconds - result.ConstantInner.FlyingLapMedianSeconds)}";
        }
    }

    private static IEnumerable<string> TopRows(DynamicCornerTrajectoryGeometryExperimentResult result) =>
        result.TopFifteen.Select((item, index) =>
            $"{index + 1} | `{item.Plan.Id}` | `{item.ActualTrajectoryFingerprint}` | {Triplet(item)} | {N(item.FlyingLapMedianSeconds)} | {N(item.HeatTimeSeconds)} | {N(item.FourLapDistanceMeters)} | {N(item.MaximumSpeedMetersPerSecond)} | {N(item.CornerEntrySpeedMetersPerSecond)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond)}@{N(item.MinimumSpeedCornerProgress)} | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.CorrectionDistanceMeters)}/{N(item.CorrectionTimeSeconds)} | {item.BrakeCount}/{item.RunWideCount}/{item.CrashCount}");

    private static IEnumerable<string> GeometryRows(DynamicCornerTrajectoryGeometryExperimentResult result) =>
        result.TopFifteen.SelectMany((item, index) => item.Corners.Select(corner =>
            $"{index + 1} | C{corner.CornerId + 1} | {N(corner.EntryLateralPosition)} | {N(corner.OneThirdLateralPosition)} | {N(corner.ApexLateralPosition)} | {N(corner.TwoThirdsLateralPosition)} | {N(corner.ExitLateralPosition)} | {N(corner.EntryRadiusMeters)} | {N(corner.ApexRadiusMeters)} | {N(corner.ExitRadiusMeters)} | {N(corner.EntrySpeedMetersPerSecond)} | {N(corner.ApexSpeedMetersPerSecond)} | {N(corner.ExitSpeedMetersPerSecond)}"));

    private static IEnumerable<(string Role, TrajectoryObservation Value)> PlannerIndependentComparisons(
        DynamicCornerTrajectoryGeometryExperimentResult result)
    {
        yield return ("ConstantInner", result.ConstantInner);
        if (result.BestPlannerIndependentDynamic is { } dynamic)
            yield return ("Best PI dynamic", dynamic);
        if (result.BestPlannerIndependentWideExit is { } wide)
            yield return ("Best PI wide exit", wide);
    }

    private static IEnumerable<string> PlannerIndependentIdentityRows(
        DynamicCornerTrajectoryGeometryExperimentResult result) =>
        PlannerIndependentComparisons(result).Select(item =>
            $"{item.Role} | `{item.Value.Plan.Id}` | `{item.Value.ActualTrajectoryFingerprint}` | {N(item.Value.FlyingLapMedianSeconds)} | {N(item.Value.FourLapDistanceMeters)} | {N(item.Value.EstimatedExtraDiagonalDistancePerLapMeters)} | {N(item.Value.EstimatedExtraTimeEquivalentSeconds)} | {Triplet(item.Value)}");

    private static IEnumerable<string> PlannerIndependentGeometryRows(
        DynamicCornerTrajectoryGeometryExperimentResult result) =>
        PlannerIndependentComparisons(result).Select(item =>
            $"{item.Role} | {N(item.Value.EntryRadiusMeters)}/{N(item.Value.ApexRadiusMeters)}/{N(item.Value.ExitRadiusMeters)} | {N(item.Value.MinimumRadiusMeters)}/{N(item.Value.MeanTimeWeightedRadiusMeters)} | {N(item.Value.CornerEntrySpeedMetersPerSecond)}/{N(item.Value.TrueApexSpeedMetersPerSecond)}/{N(item.Value.MinimumSpeedMetersPerSecond)}@{N(item.Value.MinimumSpeedCornerProgress)}/{N(item.Value.CornerExitSpeedMetersPerSecond)}");

    private static IEnumerable<string> PlannerIndependentEconomicsRows(
        DynamicCornerTrajectoryGeometryExperimentResult result) =>
        PlannerIndependentComparisons(result).Select(item =>
            $"{item.Role} | {N(item.Value.CorrectionDistanceMeters)}/{N(item.Value.CorrectionTimeSeconds)} | {N(item.Value.PreApexCorrectionDistanceMeters)}/{N(item.Value.PostApexCorrectionDistanceMeters)} | {N(item.Value.ApexToExitTimeSeconds)} | {N(item.Value.SpeedTenMetersAfterCornerMetersPerSecond)} | {N(item.Value.FirstTwentyMetersStraightTimeSeconds)} | {N(item.Value.PeakLateralAccelerationProxyMetersPerSecondSquared)}/{N(item.Value.MeanTimeWeightedLateralAccelerationProxyMetersPerSecondSquared)} | {item.Value.BrakeCount}/{item.Value.RunWideCount}/{item.Value.CrashCount} | {item.Value.PlannerCapBindingCount}/{N(item.Value.PlannerCapBindingFraction)}");

    private static IEnumerable<string> TurnSegmentRows(TrajectoryObservation item) =>
        item.TurnSegments.Select(segment =>
            $"C{segment.CornerId + 1} | {segment.SegmentIndex} | {segment.SegmentType} | {N(segment.EntryLateralPosition)} | {N(segment.ExitLateralPosition)} | {N(segment.LateralChange)} | {N(segment.PhysicalLateralChangeMeters)} | {N(segment.EntryRadiusMeters)} | {N(segment.ObservedExitRadiusMeters)} | {N(segment.EntrySurfaceGrip)}/{N(segment.EntrySurfaceRuts)}/{N(segment.EntrySurfaceMoisture)}/{N(segment.EntrySurfaceEffectiveGrip)} | {Yes(segment.HasWithinSegmentLateralChange)}");

    private static string Triplet(TrajectoryObservation item) =>
        $"{N(item.EntryLateralPosition)}/{N(item.ApexLateralPosition)}/{N(item.ExitLateralPosition)}";

    private static string N(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
    private static string Signed(double value) => value.ToString("+0.000000;-0.000000;0.000000", CultureInfo.InvariantCulture);
    private static string Yes(bool value) => value ? "YES" : "NO";
}
