using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public static class ActiveCorrectionControlLossExperimentReport
{
    public static string Render(ActiveCorrectionControlLossExperimentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var b = new StringBuilder();
        void Line(string value = "") => b.Append(value).Append('\n');

        Line("# Gameplay corner-control loss during active correction");
        Line();
        Line("## A. Design philosophy");
        Line();
        Line("Gameplay comes first, followed by believable speedway behavior, balance, real-data guardrails, and only then physics detail. `ActiveCorrectionControlLoss` is an internal gameplay abstraction, not a tyre-slip, tyre-temperature, aerodynamic-drag, or engine-braking model.");
        Line();
        Line("## B. What #41–#43 established");
        Line();
        Line("#41 showed that more straight drive raises Vmax and shortens an already-fast lap (`StraightEnvelopeShapeInsufficient`). #42 placed ordinary reduced-drive resistance mainly post-apex (`LossLocationMismatch`). #43 allowed scrub only after correction returned distance and observed only 0.000053 m at S100 (`ConservativeScrubDistanceInsufficient`). #44 therefore measures a cost inside existing active correction without claiming another metre.");
        Line();
        Line("## C. Gameplay roles of rider skills");
        Line();
        Line("`Speed` retains its existing speed/capability role. `SlideControl` remains the existing physical control and correction-capability skill without a second direct bonus. `Adaptability` only moderates the difficult-surface component. `TrackReading` remains decision/perception-only, `RiderStyle` remains preference/risk-only, and `PairRiding` is outside this formula.");
        Line();
        Line("## D. Active correction control-load model");
        Line();
        Line("Production first calls the unchanged `CalculateCornerSpeedCorrectionProfile`. Then `ControlLoad = clamp(RequiredCorrectionDistanceMeters / AvailableStepDistanceMeters, 0, 1)`. Correction target, capability, required distance, applied distance, and canonical endpoint are observed before loss. Control loss uses that same correction-owned distance and creates no distance bucket or second correction call.");
        Line();
        Line("`CorrectionEnergyRemoved = 0.5 * 142 * max(0, v_in^2 - v_production_exit^2)`; `ControlLossEnergy = CorrectionEnergyRemoved * MaxControlLossFraction * ControlLossPressure`; `v_after = sqrt(max(0, v_production_exit^2 - 2*ControlLossEnergy/142))`.");
        Line();
        Line("## E. Surface challenge and Adaptability");
        Line();
        Line("`SurfaceChallenge = clamp((0.90 - EffectiveGrip) / 0.30, 0, 1)`. `SurfaceAdaptationPenalty = SurfaceChallenge * (1 - Adaptability/100)`. `ControlLossPressure = ControlLoad * (0.75 + 0.25 * SurfaceAdaptationPenalty)`. EffectiveGrip at or above 0.90 makes the Adaptability component exactly zero.");
        Line();
        Line("| Surface | EffectiveGrip | SurfaceChallenge |");
        Line("|---|---:|---:|");
        foreach (var surface in CalibrationScenarioCatalog.Surfaces)
            Line($"| {surface.Id} | {N(surface.Surface.EffectiveGrip)} | {N(ActiveCorrectionControlLossExperiment.SurfaceChallenge(surface.Surface.EffectiveGrip))} |");
        Line();
        Line("## F. Candidate menu");
        Line();
        Line("| Candidate | MaxControlLossFraction |");
        Line("|---|---:|");
        foreach (var item in result.Candidates)
            Line($"| {item.Id} | {N(item.MaxControlLossFraction)} |");
        Line();
        Line("No candidate is selected for production.");
        Line();
        Line("## G. Production/C0 equality");
        Line();
        var f = result.Freeze;
        Line($"Production default exact: **{Yes(f.ProductionDefaultExact)}**. C0 trace exact: **{Yes(f.C0ExactProduction)}**. Zero-force machinery kinematically exact: **{Yes(f.ZeroForceExperimentalPathExact)}**. Canonical step endpoints exact: **{Yes(f.CanonicalStepEndpointsExact)}**. Full production scenario suite exact: **{Yes(f.FullProductionScenarioSuiteExact)}**.");
        Line();
        Line("## H. Primary Motoarena fixture");
        Line();
        Line("Motoarena 2026, 16.6 m width, 31/31 m split, Dry baseline surface, neutral setup, all skills 50, HoldLane, seed 390039, L1, zero incidents.");
        Line();
        Line("| Candidate | Vmax km/h | FlyingMedian s | FlyingL2 s | Heat s | Avg m/s | StraightPeak | Entry | TrueApex | Minimum | Min p | Exit | Correction m | Correction s | Correction energy J | Control loss J | Load mean/max | Pressure mean/max | B/RW/C |");
        Line("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var item in result.Primary)
            Line($"| {item.CandidateId} | {N(item.VmaxKilometersPerHour)} | {N(item.FlyingLapMedianSeconds)} | {N(item.FlyingL2Seconds)} | {N(item.HeatTimeSeconds)} | {N(item.AverageSpeedMetersPerSecond)} | {N(item.StraightPeakSpeedMetersPerSecond)} | {N(item.CornerEntrySpeedMetersPerSecond)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond)} | {N(item.MinimumSpeedCornerProgress)} | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.CorrectionDistanceMeters)} | {N(item.CorrectionTimeSeconds)} | {N(item.CorrectionEnergyRemovedJoules)} | {N(item.ControlLossEnergyJoules)} | {N(item.MeanControlLoad)}/{N(item.MaximumControlLoad)} | {N(item.MeanControlLossPressure)}/{N(item.MaximumControlLossPressure)} | {item.BrakeCount}/{item.RunWideCount}/{item.CrashCount} |");
        Line();
        Line("## I. Active-correction diagnostics");
        Line();
        Line("Full active-step detail is intentionally limited to primary L1 C0/C10/C20. `Available` is the unchanged canonical step distance; loss distance is not added to distance accounting.");
        Line();
        Line("| Candidate | Corner | p | Entry | Target | Required m | Available m | Correction m | Load | Grip | Challenge | Adaptability | Adapt penalty | Pressure | Correction J | Loss J | Production exit | Final exit |");
        Line("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var item in result.Primary.Where(item => item.CandidateId is "C0" or "C10" or "C20"))
        foreach (var step in item.ControlSteps)
            Line($"| {step.CandidateId} | {step.CornerNumber} | {N(step.CornerProgress)} | {N(step.EntrySpeedMetersPerSecond)} | {N(step.TargetSpeedMetersPerSecond)} | {N(step.RequiredCorrectionDistanceMeters)} | {N(step.AvailableStepDistanceMeters)} | {N(step.AppliedCorrectionDistanceMeters)} | {N(step.ControlLoad)} | {N(step.EffectiveGrip)} | {N(step.SurfaceChallenge)} | {N(step.Adaptability)} | {N(step.SurfaceAdaptationPenalty)} | {N(step.ControlLossPressure)} | {N(step.CorrectionEnergyRemovedJoules)} | {N(step.ControlLossEnergyJoules)} | {N(step.ProductionCorrectionExitSpeedMetersPerSecond)} | {N(step.FinalCorrectionExitSpeedMetersPerSecond)} |");
        Line();
        Line("## J. True apex vs minimum");
        Line();
        Line("True apex always means canonical `p=0.50`; the actual minimum remains a separate observation.");
        Line();
        Line("| Candidate | TrueApex | Minimum | Minimum p | Corner | Exit |");
        Line("|---|---:|---:|---:|---:|---:|");
        foreach (var item in result.Primary)
            Line($"| {item.CandidateId} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond)} | {N(item.MinimumSpeedCornerProgress)} | {item.MinimumSpeedCornerNumber} | {N(item.CornerExitSpeedMetersPerSecond)} |");
        Line();
        Line($"RecoveryLocationRisk: **{Yes(result.RecoveryLocationRisk)}**.");
        Line();
        Line("## K. Time budget");
        Line();
        Line("`FlyingL2` is one modeled flying lap and remains distinct from `FlyingMedian`.");
        Line();
        Line("| Candidate | Straight | Pre-apex | Post-apex | Corner total | FlyingL2 | FlyingMedian | dPre vs C0 | dPost vs C0 |");
        Line("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        var p0 = result.Primary.Single(item => item.CandidateId == "C0");
        foreach (var item in result.Primary)
            Line($"| {item.CandidateId} | {N(item.TimeBudget.StraightTimeSeconds)} | {N(item.TimeBudget.PreApexTimeSeconds)} | {N(item.TimeBudget.PostApexTimeSeconds)} | {N(item.TimeBudget.TotalCornerTimeSeconds)} | {N(item.FlyingL2Seconds)} | {N(item.FlyingLapMedianSeconds)} | {N(item.TimeBudget.PreApexTimeSeconds - p0.TimeBudget.PreApexTimeSeconds)} | {N(item.TimeBudget.PostApexTimeSeconds - p0.TimeBudget.PostApexTimeSeconds)} |");
        Line();
        Line("## L. Archetype matrix");
        Line();
        Matrix(result.ArchetypeSurfaceMatrix.Where(item => item.SurfaceId == "baseline"));
        Line();
        Line("Baseline pairwise C20 Flying differences (first minus second):");
        Line();
        Pairwise("FastLoose", "Technical");
        Pairwise("FastLoose", "Adaptive");
        Pairwise("Technical", "Adaptive");
        Line();
        Line("## M. Surface matrix");
        Line();
        foreach (var surface in new[] { "baseline", "grip_080", "moisture_070", "ruts_025" })
        {
            Line($"### {surface}");
            Line();
            Matrix(result.ArchetypeSurfaceMatrix.Where(item => item.SurfaceId == surface));
            Line();
        }
        Line("## N. SlideControl sweep");
        Line();
        SimpleSweep(result.SlideControlSweep, "SC", item => item.SkillSlideControl,
            includeCapability: true);
        Line();
        Line("## O. Adaptability sweep");
        Line();
        SimpleSweep(result.AdaptabilitySweep, "Adaptability", item => item.SkillAdaptability);
        Line();
        var baselineAdapt = result.AdaptabilitySweep.Where(item => item.SurfaceId == "baseline" && item.CandidateId == "C20").ToArray();
        var baselineAdaptSpread = baselineAdapt.Max(item => item.ControlLossEnergyJoules) - baselineAdapt.Min(item => item.ControlLossEnergyJoules);
        Line($"Good-surface Adaptability control-loss-energy spread: `{N(baselineAdaptSpread)} J`; exact neutral: **{Yes(baselineAdaptSpread == 0d)}**.");
        Line();
        Line("## P. Speed sweep");
        Line();
        SimpleSweep(result.SpeedSweep, "Speed", item => item.SkillSpeed);
        Line();
        Line("## Q. Line sweep and line rankings");
        Line();
        Line("| Surface | Candidate | Line | Distance m | Flying | Vmax | Entry | Apex | Exit | Correction m | Loss J |");
        Line("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var item in result.LineSweep.OrderBy(item => item.SurfaceId, StringComparer.Ordinal)
                     .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
                     .ThenBy(item => item.LateralPosition))
            Line($"| {item.SurfaceId} | {item.CandidateId} | L{N(item.LateralPosition)} | {N(item.ModeledFourLapDistanceMeters)} | {N(item.FlyingLapMedianSeconds)} | {N(item.VmaxKilometersPerHour)} | {N(item.CornerEntrySpeedMetersPerSecond)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.CorrectionDistanceMeters)} | {N(item.ControlLossEnergyJoules)} |");
        Line();
        foreach (var group in result.LineSweep.GroupBy(item => (item.SurfaceId, item.CandidateId))
                     .OrderBy(item => item.Key.SurfaceId, StringComparer.Ordinal)
                     .ThenBy(item => item.Key.CandidateId, StringComparer.Ordinal))
            Line($"{group.Key.SurfaceId} {group.Key.CandidateId}: {string.Join(" -> ", group.OrderBy(item => item.FlyingLapMedianSeconds).Select(item => $"L{N(item.LateralPosition)}"))}.");
        Line();
        Line("## R. Line-spread guardrail");
        Line();
        Line("| Surface | C0 spread | C20 spread | Ratio | Flattening risk | Over-amplification risk |");
        Line("|---|---:|---:|---:|---|---|");
        foreach (var item in result.LineSpreads)
            Line($"| {item.SurfaceId} | {N(item.C0SpreadSeconds)} | {N(item.C20SpreadSeconds)} | {N(item.Ratio)} | {Yes(item.LineChoiceFlatteningRisk)} | {Yes(item.LineChoiceOverAmplificationRisk)} |");
        Line();
        Line("These thresholds are diagnostic gameplay guardrails, not automatic failures.");
        Line();
        Line("## S. Setup trade-off");
        Line();
        Line("| Surface | Candidate | TractionBias | Flying | Apex | Exit | Loss J |");
        Line("|---|---|---:|---:|---:|---:|---:|");
        foreach (var item in result.SetupSweep.OrderBy(item => item.SurfaceId, StringComparer.Ordinal)
                     .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
                     .ThenBy(item => item.TractionBias))
            Line($"| {item.SurfaceId} | {item.CandidateId} | {N(item.TractionBias)} | {N(item.FlyingLapMedianSeconds)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.ControlLossEnergyJoules)} |");
        Line();
        Line("No #44 setup multiplier exists; this table observes the unchanged setup model.");
        Line();
        Line("## T. Decision-model sanity");
        Line();
        Line("| Archetype | Surface | AdaptiveDecisionModel lane | Risk |");
        Line("|---|---|---:|---:|");
        foreach (var item in result.DecisionModelSanity)
            Line($"| {item.RiderProfileId} | {item.SurfaceId} | L{item.ChosenLane} | {N(item.Risk)} |");
        Line();
        Line("The decision model is unchanged. TrackReading remains in perceived-surface/lane choice and is absent from physical control-loss calculations.");
        Line();
        Line("## U. Skill-double-counting guardrail");
        Line();
        double Spread(string id)
        {
            var values = result.SlideControlSweep.Where(item => item.CandidateId == id).ToArray();
            return values.Single(item => item.SkillSlideControl == 20f).FlyingLapMedianSeconds
                - values.Single(item => item.SkillSlideControl == 80f).FlyingLapMedianSeconds;
        }
        Line($"SlideControlTimeSpread C0: `{N(Spread("C0"))} s`; C20: `{N(Spread("C20"))} s`; amplification: `{N(result.SlideControlSpreadAmplification)}`; SkillDoubleCountingRisk: **{Yes(result.SkillDoubleCountingRisk)}**.");
        Line();
        Line("## V. Extreme checks");
        Line();
        Line("| Candidate | Case | Vmax | Apex | Minimum/p | Exit | Correction m | Loss J | B/RW/C | Zero speed | Finite |");
        Line("|---|---|---:|---:|---:|---:|---:|---:|---|---|---|");
        foreach (var item in result.Extremes)
            Line($"| {item.CandidateId} | {item.RiderProfileId} | {N(item.VmaxKilometersPerHour)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond)}/{N(item.MinimumSpeedCornerProgress)} | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.CorrectionDistanceMeters)} | {N(item.ControlLossEnergyJoules)} | {item.BrakeCount}/{item.RunWideCount}/{item.CrashCount} | {Yes(item.AnyZeroSpeedEvent)} | {Yes(Finite(item))} |");
        Line();
        Line("## W. Frozen systems");
        Line();
        Line($"StandingStart: **{Yes(f.StandingStartExact)}**; isolated Straight: **{Yes(f.StraightExact)}**; #41/#42/#43 adjustments absent: **{Yes(f.PreviousExperimentsAbsent)}**; Legacy: **{Yes(f.LegacyExact)}**; SegmentPhysics thresholds: **{Yes(f.SegmentPhysicsThresholdsExact)}**; LateralMovementModel: **{Yes(f.LateralMovementModelExact)}**; AdaptiveDecisionModel: **{Yes(f.AdaptiveDecisionModelExact)}**; production scenario suite: **{Yes(f.FullProductionScenarioSuiteExact)}**.");
        Line();
        Line("Historical pge-v1 and reports #38–#43 are guarded by canonical SHA-256 regression tests and remain content-identical.");
        Line();
        Line("## X. Realism guardrails");
        Line();
        Line("Motoarena context only: Vmax P10/P50/P90 = 108.6/113.7/116.9 km/h; Flying = 14.56/14.87/15.15 s. Skill 50 is not PGE P50, no candidate is fitted to these medians, and real data remains a distribution guardrail rather than the gameplay objective.");
        Line();
        Line("## Y. Gameplay interpretation");
        Line();
        var c0 = result.Primary.Single(item => item.CandidateId == "C0");
        var c20 = result.Primary.Single(item => item.CandidateId == "C20");
        Line($"C20 changes FlyingMedian by `{N(c20.FlyingLapMedianSeconds - c0.FlyingLapMedianSeconds)} s`, true-apex speed by `{N(c20.TrueApexSpeedMetersPerSecond - c0.TrueApexSpeedMetersPerSecond)} m/s`, exit speed by `{N(c20.CornerExitSpeedMetersPerSecond - c0.CornerExitSpeedMetersPerSecond)} m/s`, and removes `{N(c20.ControlLossEnergyJoules)} J` on the primary flying lap. The effect stays attached to correction demand; Adaptability is free of a good-surface bonus, line geometry still changes route length, and no new lane/setup/skill multiplier was introduced.");
        Line();
        Line("## Z. Next-model decision");
        Line();
        Line($"Classification: **`{result.Classification}`**.");
        Line();
        Line($"Recommended single subsystem for #45: **{result.NextSubsystem}**.");
        Line();
        Line("This experiment does not select C05/C10/C15/C20 for production.");

        return b.ToString();

        void Matrix(IEnumerable<ActiveCorrectionHeatObservation> observations)
        {
            Line("| Candidate | Archetype | Surface | Flying | Vmax | Entry | Apex | Exit | Correction m | Loss J | B/RW/C |");
            Line("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|");
            foreach (var item in observations.OrderBy(item => item.CandidateId, StringComparer.Ordinal)
                         .ThenBy(item => item.RiderProfileId, StringComparer.Ordinal))
                Line($"| {item.CandidateId} | {item.RiderProfileId} | {item.SurfaceId} | {N(item.FlyingLapMedianSeconds)} | {N(item.VmaxKilometersPerHour)} | {N(item.CornerEntrySpeedMetersPerSecond)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.CorrectionDistanceMeters)} | {N(item.ControlLossEnergyJoules)} | {item.BrakeCount}/{item.RunWideCount}/{item.CrashCount} |");
        }

        void Pairwise(string first, string second)
        {
            var values = result.ArchetypeSurfaceMatrix.Where(item => item.CandidateId == "C20"
                && item.SurfaceId == "baseline").ToArray();
            Line($"- {first} vs {second}: `{N(values.Single(item => item.RiderProfileId == first).FlyingLapMedianSeconds - values.Single(item => item.RiderProfileId == second).FlyingLapMedianSeconds)} s`.");
        }

        void SimpleSweep(IEnumerable<ActiveCorrectionHeatObservation> observations,
            string axis, Func<ActiveCorrectionHeatObservation, float> value,
            bool includeCapability = false)
        {
            Line($"| Surface | Candidate | {axis} | Flying | Vmax | Apex | Exit | Correction m | Loss J{(includeCapability ? " | Existing capability m/s^2" : string.Empty)} |");
            Line($"|---|---|---:|---:|---:|---:|---:|---:|---:{(includeCapability ? "|---:" : string.Empty)}|");
            foreach (var item in observations.OrderBy(item => item.SurfaceId, StringComparer.Ordinal)
                         .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
                         .ThenBy(value))
            {
                var capability = includeCapability
                    ? " | " + N(2f + 1.2f * item.SkillSlideControl / 100f)
                    : string.Empty;
                Line($"| {item.SurfaceId} | {item.CandidateId} | {N(value(item))} | {N(item.FlyingLapMedianSeconds)} | {N(item.VmaxKilometersPerHour)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.CorrectionDistanceMeters)} | {N(item.ControlLossEnergyJoules)}{capability} |");
            }
        }
    }

    private static bool Finite(ActiveCorrectionHeatObservation item) => new[]
    {
        item.VmaxKilometersPerHour,
        item.FlyingLapMedianSeconds,
        item.TrueApexSpeedMetersPerSecond,
        item.MinimumSpeedMetersPerSecond,
        item.CornerExitSpeedMetersPerSecond,
        item.CorrectionDistanceMeters,
        item.ControlLossEnergyJoules,
    }.All(double.IsFinite);

    private static string N(float value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
    private static string N(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
    private static string Yes(bool value) => value ? "YES" : "NO";
}
