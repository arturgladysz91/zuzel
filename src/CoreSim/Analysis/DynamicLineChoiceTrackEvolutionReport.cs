using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public static class DynamicLineChoiceTrackEvolutionReport
{
    public static string Render(DynamicLineChoiceTrackEvolutionExperimentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var b = new StringBuilder();
        void Line(string value = "") => b.Append(value).Append('\n');
        var staticLines = result.StaticSweeps.SelectMany(item => item.Lines).ToArray();
        var uniform = result.StaticSweeps.Single(item =>
            item.ProfileId == DynamicLineChoiceTrackEvolutionExperiment.UniformProfile);

        Line("# Dynamic Line Choice & Track Evolution Gameplay Baseline");
        Line();
        Line($"Base main: `{DynamicLineChoiceTrackEvolutionExperiment.BaseMainSha}`. Motoarena 2026 uses the matched 16.6 m symmetric-turn approximation and 31/31 m start split. Every physical benchmark is a production `CalibrationRunner -> HeatSimulator` one-rider four-lap heat with neutral setup, all skills 50, zero incidents, a fixed lane, and every calibration-only physics adjustment null.");
        Line();
        Line("## A. Game design objective");
        Line();
        Line("This experiment asks whether the current manager-game architecture already creates state-dependent line choice, imperfect information, skill-dependent execution, persistent wear, and meaningful manager intervention. Realism is a guardrail; gameplay is the objective. No inner penalty, outer bonus, cushion multiplier, preferred-line speed multiplier, or final-turn bonus is introduced.");
        Line();
        Line("## B. Existing production mechanisms");
        Line();
        Line("The experiment uses the existing five-band `TrackState`, `TrackStateSnapshot.SampleSurface`, continuous surface interpolation and wear, `AdaptiveDecisionModel`, seeded TrackReading noise, `LateralMovementModel`, `TrackEvolution.ApplyWeather`, `TrackEvolution.ApplyTrackWork`, geometry, setup, contact, and `HeatSimulator`. Lane 0..4 remains a tactical grid; physical `LateralPosition` remains continuous 0..4.");
        Line();
        Line("## C. Why uniform-surface L0 is not sufficient evidence");
        Line();
        Line($"Uniform ranking is `{Ranking(uniform.Lines)}`. Equal surface naturally favors the shorter inner route, so this alone neither proves line blindness nor justifies a physics change. Controlled unequal surfaces are required before diagnosis.");
        Line();
        Line("## D. Fixed-line methodology");
        Line();
        Line("`PHYSICAL BEST LINE` is measured separately from `AI CHOSEN LINE`. Each static state starts from a fresh surface, runs HoldLane L0-L4 through the production heat, and ranks the flying-lap median. Surface values are actual integer-position samples from `SampleSurface`; benchmarks of evolved states run on copies and cannot wear the persistent meeting state.");
        Line();
        Line("## E. Uniform benchmark");
        Line();
        StaticTable(uniform.Lines);
        Line();
        Line("## F. Outside-cushion severity sweep");
        Line();
        Line("`OutsideCushionStrong` is the severity 1 endpoint. All turn segments receive the specified lane deltas; straights remain baseline and moisture is unchanged.");
        Line();
        foreach (var sweep in result.StaticSweeps.Where(item => item.ProfileId ==
                     DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile).OrderBy(item => item.Severity))
        {
            Line($"### severity {N(sweep.Severity)} — best L{sweep.BestLane}, second L{sweep.SecondBestLane}, gap {N(sweep.BestVsSecondGapSeconds)} s");
            Line();
            StaticTable(sweep.Lines);
            Line();
        }
        Line("## G. Middle-cushion severity sweep");
        Line();
        foreach (var sweep in result.StaticSweeps.Where(item => item.ProfileId ==
                     DynamicLineChoiceTrackEvolutionExperiment.MiddleProfile).OrderBy(item => item.Severity))
        {
            Line($"### severity {N(sweep.Severity)} — best L{sweep.BestLane}, second L{sweep.SecondBestLane}, gap {N(sweep.BestVsSecondGapSeconds)} s");
            Line();
            StaticTable(sweep.Lines);
            Line();
        }
        Line("## H. Line-switch thresholds");
        Line();
        Line("| Profile | FirstNonInnerBestSeverity | FirstOuterHalfBestSeverity | Best-line sequence severity 0/.25/.50/.75/1 |\n|---|---:|---:|---|");
        ThresholdRow(DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile,
            result.OutsideFirstNonInnerBestSeverity, result.OutsideFirstOuterHalfBestSeverity);
        ThresholdRow(DynamicLineChoiceTrackEvolutionExperiment.MiddleProfile,
            result.MiddleFirstNonInnerBestSeverity, result.MiddleFirstOuterHalfBestSeverity);
        Line();
        Line("The threshold is an observed diagnostic, not a required winner.");
        Line();
        Line("### Physical line sensitivity");
        Line();
        Line($"LineSwitchRequiresStrongSurfaceContrast: **{Yes(result.LineSwitchRequiresStrongSurfaceContrast)}**. A switch only at severity 1 is not an automatic failure; it shows that the current constant-reference physical economy strongly rewards shorter lines.");
        Line();
        Line("## I. Effective-grip matrix");
        Line();
        Line("| Profile | Severity | Lane | Grip | Ruts | Moisture | EffectiveGrip |\n|---|---:|---:|---:|---:|---:|---:|");
        foreach (var item in result.StaticSurfaceMatrix.OrderBy(item => item.ProfileId, StringComparer.Ordinal)
                     .ThenBy(item => item.Severity).ThenBy(item => item.Lane))
            Line($"| {item.ProfileId} | {N(item.Severity)} | L{item.Lane} | {N(item.Grip)} | {N(item.Ruts)} | {N(item.Moisture)} | {N(item.EffectiveGrip)} |");
        Line();
        Line("## J. AdaptiveDecisionModel static choices");
        Line();
        Line("The probe is captured on the home-straight segment immediately before the next logical corner.");
        Line();
        Line("| Profile | Severity | Actual best | AI target | Regret s |\n|---|---:|---:|---:|---:|");
        foreach (var item in result.StaticDecisions)
            Line($"| {item.ProfileId} | {N(item.Severity)} | L{item.ActualBestLane} | L{item.ChosenLane} | {N(item.RegretSeconds)} |");
        Line();
        Line("### Decision oracle at TrackReading 100");
        Line();
        Line("TrackReading 100 makes the existing observation-noise multiplier zero. The oracle is therefore the current production decision objective under exact surface perception, not an oracle for the physically fastest fixed line.");
        Line();
        Line("`RiderStyle.Balanced` has `OutsidePreference = 0.5`, which production maps to a preferred lane near L2. Physical regret measures distance to the fastest fixed line, while the production decision objective also contains rider-style preference, movement cost, occupancy, surface risk, and projected route time.");
        Line();
        Line("| Profile | Severity | Physical best | TR100 oracle | Seed invariant | Oracle physical regret | TR100 L0/L1/L2/L3/L4 |");
        Line("|---|---:|---:|---:|---|---:|---|");
        foreach (var item in result.DecisionOracles)
            Line($"| {item.ProfileId} | {N(item.Severity)} | L{item.PhysicalBestLane} | L{item.OracleLane} | {Yes(item.SeedInvariant)} | {N(item.OraclePhysicalRegretSeconds)} | {Counts(item.ChosenLaneHistogram)} |");
        Line();
        Line("### Perception agreement vs physical regret");
        Line();
        Line("| Profile | Severity | Physical best | TR100 oracle | Oracle regret | TR20 agreement | TR50 agreement | TR80 agreement | TR20 physical regret | TR50 physical regret | TR80 physical regret |");
        Line("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var oracle in result.DecisionOracles)
        {
            var rows = result.TrackReadingMetrics.Where(item => item.ProfileId == oracle.ProfileId
                && BitConverter.SingleToInt32Bits(item.Severity)
                == BitConverter.SingleToInt32Bits(oracle.Severity)).ToArray();
            var tr20 = rows.Single(item => item.TrackReading == 20f);
            var tr50 = rows.Single(item => item.TrackReading == 50f);
            var tr80 = rows.Single(item => item.TrackReading == 80f);
            Line($"| {oracle.ProfileId} | {N(oracle.Severity)} | L{oracle.PhysicalBestLane} | L{oracle.OracleLane} | {N(oracle.OraclePhysicalRegretSeconds)} | {N(tr20.OracleAgreementPercent)}% | {N(tr50.OracleAgreementPercent)}% | {N(tr80.OracleAgreementPercent)}% | {N(tr20.MeanRegretSeconds)} | {N(tr50.MeanRegretSeconds)} | {N(tr80.MeanRegretSeconds)} |");
        }
        Line();
        Line("### Decision-objective mismatch");
        Line();
        Line($"DecisionObjectiveVsFastestLineMismatch: **{Yes(result.DecisionObjectiveVsFastestLineMismatch)}**.");
        Line();
        Line("## K. TrackReading seed sweep");
        Line();
        Line($"Each row uses the stable {result.DecisionSeeds.Count}-seed set `{result.DecisionSeeds[0]}..{result.DecisionSeeds[^1]}` and only the existing deterministic RNG.");
        Line();
        DecisionTable();
        Line();
        Line("## L. Decision regret");
        Line();
        Line("`PhysicalRegret = FixedLineFlying(ChosenLane) - FixedLineFlying(PhysicalBestLane)`, clamped only for sub-tolerance floating error. `OracleAgreement` instead measures convergence to the seed-invariant TR100 production decision. These answer different questions; TrackReading remains perception/decision-only.");
        Line();
        foreach (var tr in new[] { 20f, 50f, 80f })
        {
            var rows = result.TrackReadingMetrics.Where(item => item.TrackReading == tr).ToArray();
            Line($"- TR{N0(tr)} aggregate mean/median/P90 regret: `{N(rows.Average(item => item.MeanRegretSeconds))}` / `{N(rows.Average(item => item.MedianRegretSeconds))}` / `{N(rows.Average(item => item.P90RegretSeconds))}` s; perfect `{N(rows.Average(item => item.PerfectChoicePercent))}%`; within 0.05 s `{N(rows.Average(item => item.WithinPointZeroFiveSecondsPercent))}%`.");
        }
        Line($"- TrackReadingPerceptionSignalHealthy: **{Yes(result.TrackReadingPerceptionSignalHealthy)}**.");
        Line($"- TrackReadingPerceptionSignalWeak: **{Yes(result.TrackReadingPerceptionSignalWeak)}**.");
        Line($"- TrackReadingPerceptionReversal: **{Yes(result.TrackReadingPerceptionReversal)}**.");
        Line($"- DecisionObjectiveVsFastestLineMismatch: **{Yes(result.DecisionObjectiveVsFastestLineMismatch)}**.");
        Line();
        Line("## M. Lateral execution matrix");
        Line();
        Line("### Planner waypoint binding");
        Line();
        Line("| SC | Adaptability | Profile/severity | Target | Planned | Start | Entry | To planned m | To target m | Max move m | Actual move m | Cap binding | Arrival error m | Arrived | Flying s |\n|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---|---:|");
        foreach (var item in result.ExecutionMatrix)
            ExecutionRow(item);
        Line();
        Line("### Execution capacity vs realized movement");
        Line();
        Line($"ExecutionCapacitySpread: `{N(result.ExecutionCapacitySpreadMeters)} m`; max/min ratio `{N(result.ExecutionCapacityRatio)}`. ExecutionSkillCapacityExists: **{Yes(result.ExecutionSkillCapacityExists)}**. ExecutionSkillCapacityWeak: **{Yes(result.ExecutionSkillCapacityWeak)}**. ExecutionPlannerBound: **{Yes(result.ExecutionPlannerBound)}**. PlanningHorizonLimitsExecutionExpression: **{Yes(result.PlanningHorizonLimitsExecutionExpression)}**.");
        Line();
        Line("### Adjacent-lane control");
        Line();
        Line("| SC | Adaptability | Target | Planned | Max move m | Actual move m | Cap binding | Arrived |\n|---:|---:|---:|---:|---:|---:|---|---|");
        foreach (var item in result.AdjacentExecutionControl)
            Line($"| {item.SlideControl} | {item.Adaptability} | L{item.TargetLane} | L{item.PlannedLaneOnPreparationSegment} | {N(item.MaximumPossibleLateralMeters)} | {N(item.PhysicalLateralMetersMoved)} | {Yes(item.PlannerCapBinding)} | {Yes(item.Arrived)} |");
        Line();
        Line($"AdjacentExecutionFixtureNonDiscriminating: **{Yes(result.AdjacentExecutionFixtureNonDiscriminating)}**. Arrival uses the unchanged exact `{N(LateralMovementModel.LaneArrivalToleranceMeters)} m` contract. This control is reported, not tuned to manufacture separation.");
        Line();
        Line("Planning-horizon answer: the current preparation segment resolves a distant target through the next reference-lane waypoint. In this fixture the waypoint cap, rather than available movement capacity, determines realized movement; whether a rider can begin early enough across multiple segments remains a future planning-horizon question.");
        Line();
        Line("## N. Natural 12-heat evolution");
        Line();
        Line("The meeting begins from Uniform, uses one persistent TrackState, four balanced riders per heat, `AdaptiveDecisionModel`, zero incident frequency, neutral setup, no weather delta, and the predeclared seeds 45001..45012. Rider state is recreated per heat; surface state is not reset. Evolution results are conditional on current production decision behavior, including the decision-objective alignment measured in sections J-L.");
        Line();
        Line("| State | Best | L0 | L1 | L2 | L3 | L4 | Best gap | AI most-chosen |\n|---|---:|---:|---:|---:|---:|---:|---:|---|");
        EvolutionComparison(result.Evolution.Single(item => item.Heat == 0));
        EvolutionComparison(result.Evolution.Single(item => item.Heat == 6));
        EvolutionComparison(result.Evolution.Single(item => item.Heat == 12));
        var afterWork = result.TrackWork.AfterLines;
        var afterBest = afterWork.OrderBy(item => item.FlyingLapMedianSeconds).First();
        var afterSecond = afterWork.OrderBy(item => item.FlyingLapMedianSeconds).Skip(1).First();
        Line($"| after track work | L{afterBest.Lane} | {LineTimes(afterWork)} | {N(afterSecond.FlyingLapMedianSeconds - afterBest.FlyingLapMedianSeconds)} | L{result.TrackWork.AiLaneAfter} |");
        Line();
        Line("## O. Surface evolution by lane");
        Line();
        Line("Raw diagnostics preserve both logical corners. Values are the mean of the three production segment cells within that logical corner, sampled at the integer reference position.");
        Line();
        Line("| Heat | Corner | Lane | Grip | Ruts | Moisture | EffectiveGrip |\n|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var item in result.Evolution.SelectMany(item => item.CornerSurfaces))
            Line($"| {item.Heat} | {item.CornerId} | L{item.Lane} | {N(item.Grip)} | {N(item.Ruts)} | {N(item.Moisture)} | {N(item.EffectiveGrip)} |");
        Line();
        Line("Initial -> final mean-corner grip changes: " + Vector(result.InitialToFinalGripChanges) + ".");
        Line("Initial -> final mean-corner ruts changes: " + Vector(result.InitialToFinalRutsChanges) + ".");
        Line();
        Line("## P. Line usage by heat");
        Line();
        Line("Usage bins actual continuous turn-entry/turn traversal positions to the nearest reference only for diagnostic counting; wear itself remains production continuous wear.");
        Line();
        Line("| Heat | Usage L0/L1/L2/L3/L4 | Most used | Target L0/L1/L2/L3/L4 | Entry-bin L0/L1/L2/L3/L4 | Mean entry lateral |\n|---:|---|---|---|---|---:|");
        foreach (var item in result.Evolution)
            Line($"| {item.Heat} | {Counts(item.LaneUsageCounts)} | {(item.MostUsedLane.HasValue ? $"L{item.MostUsedLane}" : "N/A")} | {Counts(item.TargetLaneCounts)} | {Counts(item.CornerEntryLaneHistogram)} | {N(item.MeanCornerEntryLateralPosition)} |");
        Line();
        Line("## Q. Fixed-line optimum after each heat");
        Line();
        Line("| Heat | Best | Ranking | Best Flying | Second gap | L0 | L1 | L2 | L3 | L4 |\n|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var item in result.Evolution)
            Line($"| {item.Heat} | L{item.BestPhysicalLane} | {Ranking(item.FixedLines)} | {N(item.BestFlyingSeconds)} | {N(item.SecondBestGapSeconds)} | {LineTimes(item.FixedLines)} |");
        Line();
        Line($"FirstEvolutionLineSwitchHeat: **{Nullable(result.FirstEvolutionLineSwitchHeat)}**. InitialLineSpread `{N(result.InitialLineSpreadSeconds)} s`; FinalLineSpread `{N(result.FinalLineSpreadSeconds)} s`.");
        Line();
        Line("## R. AI response to evolving optimum");
        Line();
        Line("Physical best sequence H0-H12: `" + string.Join(" -> ", result.Evolution.Select(item => $"L{item.BestPhysicalLane}")) + "`.");
        Line("Most-used sequence H1-H12: `" + string.Join(" -> ", result.Evolution.Skip(1).Select(item => item.MostUsedLane.HasValue ? $"L{item.MostUsedLane}" : "NONE")) + "`.");
        Line("Target distributions and actual entry-position distributions are reported in section P, keeping perception, intent, and execution separate.");
        Line();
        Line("## S. Feedback-loop / oscillation guardrails");
        Line();
        Line($"Healthy popularity -> wear -> line-change feedback observed: **{Yes(result.HealthyFeedbackLoop)}**. TrackEvolutionOscillationRisk (>4 physical-best switches): **{Yes(result.TrackEvolutionOscillationRisk)}**. TrackEvolutionTooWeak: **{Yes(result.TrackEvolutionTooWeak)}**. TrackEvolutionTooStrong: **{Yes(result.TrackEvolutionTooStrong)}**. The bounded strong flag is raised when any fixed-line flying shift exceeds 1.00 s or any final corner EffectiveGrip falls below 0.25; the weak flag requires wear plus no best-line switch and less than 0.01 s spread change. These are diagnostic thresholds, not calibration failures.");
        Line("Wear distribution and feedback-loop interpretation are conditional on current production decision behavior; they are not an independent calibration verdict while the decision objective differs from the fastest fixed-line benchmark.");
        Line();
        Line("## T. Track-work manager intervention");
        Line();
        Line($"`Pack`, intensity 0.50, turn segments only. The two lanes selected automatically by heat-12 mean turn ruts are `{string.Join(", ", result.TrackWork.SelectedLanes.Select(lane => $"L{lane}"))}`.");
        Line();
        Line($"Before ranking: `{Ranking(result.TrackWork.BeforeLines)}`. After ranking: `{Ranking(result.TrackWork.AfterLines)}`. AI before/after: `L{result.TrackWork.AiLaneBefore} -> L{result.TrackWork.AiLaneAfter}`.");
        Line();
        Line("| Lane | Grip delta | Ruts delta | Moisture delta | EffectiveGrip delta | Flying delta s |\n|---:|---:|---:|---:|---:|---:|");
        foreach (var delta in result.TrackWork.SurfaceDeltas)
        {
            var before = result.TrackWork.BeforeLines.Single(item => item.Lane == delta.Lane);
            var after = result.TrackWork.AfterLines.Single(item => item.Lane == delta.Lane);
            Line($"| L{delta.Lane} | {N(delta.GripDelta)} | {N(delta.RutsDelta)} | {N(delta.MoistureDelta)} | {N(delta.EffectiveGripDelta)} | {N(after.FlyingLapMedianSeconds - before.FlyingLapMedianSeconds)} |");
        }
        Line();
        Line("## U. Weather sanity");
        Line();
        Line($"One existing `ApplyWeather` tick uses `{result.Weather.Condition}`, rain intensity `{N(result.Weather.RainIntensity)}`. Lane exposure creates different turn surfaces: **{Yes(result.Weather.ExposureCreatesDifferentSurfaces)}**.");
        Line();
        Line("| Lane | Moisture before | Moisture after | Delta |\n|---:|---:|---:|---:|");
        foreach (var item in result.Weather.TurnLanes)
            Line($"| L{item.Lane} | {N(item.BeforeMoisture)} | {N(item.AfterMoisture)} | {N(item.MoistureDelta)} |");
        Line();
        Line("## V. Frozen production systems");
        Line();
        Line("Production behavior changed: **NO**. `SegmentPhysics`, `ContinuousCornerEnvelope`, `LongitudinalDynamics`, `LateralMovementModel`, `AdaptiveDecisionModel`, `TrackEvolution`, `TrackState`, wear/surface/setup/incident formulas, `StandingStart`, and Legacy are unchanged. `StraightDriveEnvelopeAdjustment`, `CornerReducedDriveResistanceAdjustment`, `PreApexScrubLossAdjustment`, and `ActiveCorrectionControlLossAdjustment` are all null. Historical pge-v1 and reports #38-#44 are SHA-256 guarded.");
        Line();
        Line("## W. Gameplay interpretation");
        Line();
        var nonInner = result.StaticSweeps.Any(item => item.BestLane != 0);
        var middleBest = result.StaticSweeps.Any(item => item.BestLane is 2 or 3);
        var outerBest = result.StaticSweeps.Any(item => item.BestLane == 4);
        var aiResponse = result.StaticDecisions.Any(item => item.ActualBestLane != 0 && item.ChosenLane != 0);
        var aggregate20 = result.TrackReadingMetrics.Where(item => item.TrackReading == 20f).Average(item => item.MeanRegretSeconds);
        var aggregate80 = result.TrackReadingMetrics.Where(item => item.TrackReading == 80f).Average(item => item.MeanRegretSeconds);
        var lowReadingWrong = result.TrackReadingMetrics.Where(item => item.TrackReading == 20f)
            .Any(item => item.OracleAgreementPercent < 100d);
        var executionMatters = result.ExecutionSkillCapacityExists;
        var wearSwitch = result.FirstEvolutionLineSwitchHeat.HasValue;
        var aiEvolution = result.Evolution.Skip(1).Select(item => item.MostUsedLane).Distinct().Count() > 1;
        var workChangesEconomy = result.TrackWork.BeforeLines.Zip(result.TrackWork.AfterLines)
            .Any(pair => Math.Abs(pair.First.FlyingLapMedianSeconds - pair.Second.FlyingLapMedianSeconds) > 1e-6d);
        var universal = result.StaticSweeps.Select(item => item.BestLane).Distinct().Count() == 1;
        Answer(1, "Can production physics make L1/L2/L3/L4 faster than L0 on lane-specific surface?", nonInner);
        Line($"2. First surface threshold: Outside non-inner `{Nullable(result.OutsideFirstNonInnerBestSeverity)}`, Outside outer-half `{Nullable(result.OutsideFirstOuterHalfBestSeverity)}`, Middle non-inner `{Nullable(result.MiddleFirstNonInnerBestSeverity)}`, Middle outer-half `{Nullable(result.MiddleFirstOuterHalfBestSeverity)}`.");
        Answer(3, "Can a middle line naturally become best?", middleBest);
        Answer(4, "Can the outer line naturally become best?", outerBest);
        Answer(5, "Does AdaptiveDecisionModel notice a non-inner optimum?", aiResponse);
        Answer(6, "Does high TrackReading converge toward the TR100 production oracle?", result.TrackReadingPerceptionSignalHealthy);
        Answer(7, "Does low TrackReading sometimes differ from the production oracle?", lowReadingWrong);
        Answer(8, "Do Adaptability/SlideControl change available execution capacity?", executionMatters);
        Answer(9, "Can natural wear move the optimum during 12 heats?", wearSwitch);
        Answer(10, "Does AI usage respond during evolution?", aiEvolution);
        Answer(11, "Is a healthy feedback loop observed?", result.HealthyFeedbackLoop);
        Answer(12, "Is oscillation risk present?", result.TrackEvolutionOscillationRisk);
        Answer(13, "Does manager track work change line economy?", workChangesEconomy);
        Answer(14, "Is one lane universally best in every static state?", universal);
        Line($"15. First actual bottleneck: **{result.FirstActualBottleneck}**.");
        Line($"Physical regret worsens from TR20 aggregate `{N(aggregate20)}` s to TR80 `{N(aggregate80)}` s while oracle agreement improves: **{Yes(aggregate80 > aggregate20 && result.TrackReadingPerceptionSignalHealthy)}**. This is decision-objective mismatch evidence, not perception failure.");
        Line();
        Line("## X. Failure-layer diagnosis");
        Line();
        Line($"1. Physical line economy: `{(nonInner ? "responds, but requires strong surface contrast" : "insufficient")}`.");
        Line($"2. Decision objective alignment: `{(result.DecisionObjectiveVsFastestLineMismatch ? "TR100 oracle differs materially from fastest fixed line" : "aligned within 0.05 s")}`.");
        Line($"3. TrackReading perception: `{(result.TrackReadingPerceptionSignalHealthy ? "healthy convergence to TR100 oracle" : result.TrackReadingPerceptionSignalWeak ? "weak" : result.TrackReadingPerceptionReversal ? "reversal" : "inconclusive")}`.");
        Line($"4. Planning horizon: `{(result.PlanningHorizonLimitsExecutionExpression ? "waypoint cap limits expression" : "not limiting in fixture")}`.");
        Line($"5. Execution capacity: `{(result.ExecutionSkillCapacityExists ? "skill-dependent capacity exists" : "capacity signal weak")}`.");
        Line($"6. Evolution: `{(result.TrackEvolutionTooWeak ? "too weak" : result.TrackEvolutionTooStrong ? "too strong, conditional on current decisions" : "within diagnostic guardrails")}`. Manager work: `{(workChangesEconomy ? "noticeable" : "no measurable line-time effect")}`.");
        Line();
        Line($"Classification: **`{result.Classification}`**.");
        Line();
        Line("## Y. Recommended #46 subsystem");
        Line();
        Line($"Recommended single subsystem: **{result.RecommendedSubsystem}**. This follows the mandatory diagnostic order and does not implement the fix in #45.");
        Line();
        Line("## Z. Limitations");
        Line();
        Line("This is a bounded gameplay-architecture experiment, not a fit to real lane trajectories. PGE telemetry has no per-point lane/surface truth. `surface sampled at current production granularity; not continuously resampled every physical metre`. The `constant-reference-line benchmark does not model diagonal/spiral racing trajectory`; therefore L4 not being fastest does not rule out a future effective outside trajectory. The current model also keeps five reference bands, entry-position corner geometry and wear, provisional occupancy/contact widths, and no diagonal/spiral distance correction. Weather receives one sanity tick only. Traffic is present in the meeting probe but excluded from one-rider physical benchmarks.");

        return b.ToString();

        void StaticTable(IEnumerable<FixedLineBenchmarkObservation> lines)
        {
            Line("| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var item in lines.OrderBy(item => item.Lane))
                Line($"| {item.ProfileId} | {N(item.Severity)} | L{item.Lane} | {N(item.Surface.EffectiveGrip)} | {N(item.FourLapDistanceMeters)} | {N(item.FlyingLapMedianSeconds)} | {N(item.HeatTimeSeconds)} | {N(item.MaximumSpeedMetersPerSecond)} | {N(item.CornerEntrySpeedMetersPerSecond)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.CornerExitSpeedMetersPerSecond)} | {item.Rank} | {N(item.DeltaToBestSeconds)} |");
        }

        void ThresholdRow(string profile, float? first, float? outer)
        {
            var sequence = result.StaticSweeps.Where(item => item.ProfileId == profile)
                .OrderBy(item => item.Severity).Select(item => $"L{item.BestLane}");
            Line($"| {profile} | {Nullable(first)} | {Nullable(outer)} | {string.Join(" -> ", sequence)} |");
        }

        void DecisionTable()
        {
            Line("| Profile | Severity | TR | Seeds | Physical-perfect % | Within .05s % | Mean physical regret | Median | P90 | Oracle | Oracle agreement % | L0/L1/L2/L3/L4 choices | Oracle delta -4/-3/-2/-1/0/+1/+2/+3/+4 |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|");
            foreach (var item in result.TrackReadingMetrics)
                Line($"| {item.ProfileId} | {N(item.Severity)} | {N0(item.TrackReading)} | {item.SeedCount} | {N(item.PerfectChoicePercent)} | {N(item.WithinPointZeroFiveSecondsPercent)} | {N(item.MeanRegretSeconds)} | {N(item.MedianRegretSeconds)} | {N(item.P90RegretSeconds)} | L{item.OracleLane} | {N(item.OracleAgreementPercent)} | {Counts(item.ChosenLaneHistogram)} | {Counts(item.OracleDifferenceHistogram)} |");
        }

        void ExecutionRow(LateralExecutionObservation item)
        {
            Line($"| {item.SlideControl} | {item.Adaptability} | {item.ProfileId}/{N(item.Severity)} | L{item.TargetLane} | L{item.PlannedLaneOnPreparationSegment} | {N(item.StartingLateralPosition)} | {N(item.CornerEntryLateralPosition)} | {N(item.DistanceToPlannedLaneMeters)} | {N(item.DistanceToTargetLaneMeters)} | {N(item.MaximumPossibleLateralMeters)} | {N(item.PhysicalLateralMetersMoved)} | {Yes(item.PlannerCapBinding)} | {N(item.ArrivalErrorMeters)} | {Yes(item.Arrived)} | {N(item.FlyingLapMedianSeconds)} |");
        }

        void EvolutionComparison(EvolutionHeatObservation item)
        {
            Line($"| heat {item.Heat} | L{item.BestPhysicalLane} | {LineTimes(item.FixedLines)} | {N(item.SecondBestGapSeconds)} | {(item.MostUsedLane.HasValue ? $"L{item.MostUsedLane}" : "N/A")} |");
        }

        void Answer(int number, string question, bool answer) =>
            Line($"{number}. {question} **{Yes(answer)}**.");
    }

    private static string Ranking(IEnumerable<FixedLineBenchmarkObservation> values) =>
        string.Join(" -> ", values.OrderBy(item => item.FlyingLapMedianSeconds)
            .ThenBy(item => item.Lane).Select(item => $"L{item.Lane}"));

    private static string LineTimes(IEnumerable<FixedLineBenchmarkObservation> values) =>
        string.Join(" | ", values.OrderBy(item => item.Lane)
            .Select(item => N(item.FlyingLapMedianSeconds)));

    private static string Counts(IEnumerable<int> values) => string.Join('/', values);
    private static string Vector(IEnumerable<float> values) => string.Join(", ", values.Select((value, lane) => $"L{lane} {N(value)}"));
    private static string Nullable(float? value) => value.HasValue ? N(value.Value) : "NONE";
    private static string Nullable(int? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NONE";
    private static string N(float value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
    private static string N(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
    private static string N0(float value) => value.ToString("0", CultureInfo.InvariantCulture);
    private static string Yes(bool value) => value ? "YES" : "NO";
}
