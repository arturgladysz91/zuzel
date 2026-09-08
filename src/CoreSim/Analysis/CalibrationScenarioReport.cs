using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

/// <summary>Pure deterministic Markdown export. File I/O and provenance acquisition belong to the caller.</summary>
public static class CalibrationScenarioReport
{
    public static string Render(IEnumerable<CalibrationScenarioResult> results,
        CalibrationEvaluationReport realWorldContext, string baselineMainSha)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(realWorldContext);
        if (baselineMainSha is null || baselineMainSha.Length != 40 || baselineMainSha.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("A full 40-character baseline SHA is required.", nameof(baselineMainSha));
        var ordered = results.OrderBy(result => result.Metadata.ScenarioId, StringComparer.Ordinal).ToArray();
        if (ordered.Select(result => result.Metadata.ScenarioId).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new ArgumentException("Duplicate scenario ids.", nameof(results));
        var heats = ordered.OfType<CalibrationHeatResult>().ToArray();
        var baseline = heats.Single(result => result.Metadata.ScenarioId == "full_heat/baseline");
        if (realWorldContext.ScenarioId != baseline.Metadata.ScenarioId)
            throw new ArgumentException("Real-world context must evaluate the full-heat baseline.", nameof(realWorldContext));
        var b = new StringBuilder();
        Line("# Calibration scenarios — pre-tuning baseline after PR #34");
        Line();
        Line($"Baseline main SHA: {baselineMainSha}");
        Line();
        Line("Historical measurement artifact for PR #35. Later tuning PRs must create a new report, not overwrite this baseline. NO PHYSICS CONSTANTS WERE TUNED.");
        Heading("Provenance");
        var geometry = Track.CreateStandingStartExample().Geometry;
        Line($"{ordered.Length} scenario definitions. Fixed seed {CalibrationSkillSweep.FixedSeed}; Dry weather; incidents 0; HoldLane with risk 0; RiderStyle.Balanced; four laps. All skills 50 and BikeSetup.Neutral (Gearing 0.5, TractionBias 0.5) unless an input row says otherwise. Skill 50 is only a fixture, not an average real PGEE rider. Gearing is a normalized game abstraction, not real sprockets.");
        Line();
        Line($"Track.CreateStandingStartExample: inner reference radius {F(geometry.InnerRadiusMeters)} m, straight/turn widths {F(geometry.StraightWidthMeters)}/{F(geometry.TurnWidthMeters)} m, turn-segment angle {F(geometry.TurnSegmentAngleRadians)} rad; 35 m launch/home-straight halves and {F(geometry.StraightLengthMeters)} m back straight. Offsets below are from the inner reference trajectory, not the physical inner edge.");
        Line();
        Line("Start uses the production launch profile: prepared probes obtain it from SimulationEngine.Resolve (including production lookahead); pure_launch has no preparation target. Straight uses CalculateForceBasedStraightSpeedProfile with no downstream corner. Corner probes use CaptureSnapshot → Decide → Resolve and retain its original profiles; no alternate traversal is calculated. Full heats/line runs use CalibrationRunner → HeatSimulator → SimulationEngine, including Commit and normal surface evolution. Isolated probes sample a fresh uniform surface; full heats start uniform but do not freeze wear/weather. The full heat is not four independent fixed-surface probes.");
        Line();
        Line("One-axis sweeps hold all other inputs fixed. Corner axis sweeps hold the baseline incoming speed fixed, not an overspeed factor; therefore classifications can change. Band inputs use observed production transition speeds. TurnEntry input is derived with the production approach-speed helper so scrub ends inside that band. Transition diagnostics are adjacent-float black-box observations of SegmentPhysics.Apply, not copied threshold formulas or parameter fitting. insufficient_distance starts at legal segment progress 0.99.");
        Line();
        Line("Explicit interaction probes: gearing × distances 10/60/600 m, traction bias endpoints × moisture 0.70, and three synthetic within-heat patterns. The 600 m straight is a diagnostic distance to observe approach to equilibrium, not example-track geometry. Repeated baseline-valued axes are intentionally separately labelled controls. '—' means no production profile/not reached/not applicable, never a zero-valued inferred measurement. Speed units are m/s unless marked km/h; times s, distances m, force N, acceleration m/s².");
        Line();
        Table("Surface | Grip | Ruts | Moisture | EffectiveGrip", CalibrationScenarioCatalog.Surfaces.Select(surface =>
            Row(surface.Id, surface.Surface.Grip, surface.Surface.Ruts, surface.Surface.Moisture, surface.Surface.EffectiveGrip)));
        Heading("Inputs");
        Line("Skill vector order: Start / Speed / SlideControl / TrackReading / PairRiding / Adaptability. For synthetic heats, individual vectors below override the common fixture. The table is the complete scenario input inventory; geometry/speed/progress inputs are also printed in the respective output tables.");
        Line();
        Table("Scenario | Skills | Gearing | TractionBias | Grip | Ruts | Moisture | EffectiveGrip", ordered.Select(result =>
        {
            var fixture = Scenario(result).Fixture;
            return Row(result.Metadata.ScenarioId, Skills(fixture.Skills), fixture.Setup.Gearing, fixture.Setup.TractionBias,
                fixture.Surface.Grip, fixture.Surface.Ruts, fixture.Surface.Moisture, fixture.Surface.EffectiveGrip);
        }));

        Heading("Start");
        Table("Scenario | Mode | Distance | Reaction | Movement | Total | TimeTo70 | SpeedAt2s | Exit | Peak | Accel m | Cruise m | Prep m | Entry net accel | Reference net accel | Reference force | Equilibrium",
            ordered.OfType<CalibrationStartResult>().Select(result =>
            {
                var p = result.Profile;
                return Row(result.Metadata.ScenarioId, result.Scenario.Mode, result.DistanceMeters,
                    p.ReactionTimeSeconds, p.MovementTimeSeconds, p.TotalTimeSeconds, p.TimeTo70KphSeconds,
                    p.SpeedAtTwoSecondsMetersPerSecond, p.ExitSpeedMetersPerSecond, p.PeakSpeedMetersPerSecond,
                    p.AccelerationDistanceMeters, p.CruiseDistanceMeters, p.PreparationDistanceMeters,
                    p.EntryNetAccelerationMetersPerSecondSquared, result.ReferenceNetAccelerationMetersPerSecondSquared,
                    result.ReferenceDriveForceNewtons, p.FullDriveEquilibriumSpeedMetersPerSecond);
            }));
        Heading("Straight");
        Line("Free-drive phase 'Decel' can include signed resistance-dominated deceleration; it is not a preparation distance when no downstream target is supplied. Equilibrium is diagnostic only, never a speed cap.");
        Line();
        Table("Scenario | Distance | Entry | Exit | Peak | Time | Accel m | Cruise m | Decel m | Reference force | Entry net accel | Equilibrium",
            ordered.OfType<CalibrationStraightResult>().Select(result =>
            {
                var p = result.Profile;
                return Row(result.Metadata.ScenarioId, result.Scenario.DistanceMeters, result.Scenario.EntrySpeedMetersPerSecond,
                    p.ExitSpeedMetersPerSecond, p.PeakSpeedMetersPerSecond, p.TravelTimeSeconds, p.AccelerationDistanceMeters,
                    p.CruiseDistanceMeters, p.DecelerationDistanceMeters, result.ReferenceDriveForceNewtons,
                    result.EntryNetAccelerationMetersPerSecondSquared, p.FullDriveEquilibriumSpeedMetersPerSecond);
            }));
        foreach (var type in new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit })
        {
            Heading(type.ToString());
            var turns = ordered.OfType<CalibrationTurnResult>().Where(result => result.Scenario.SegmentType == type).ToArray();
            Line(type == SegmentType.TurnEntry ? "Production order: scrub → post-scrub constraint → correction → residual carry. Scrub m includes its deceleration and any scrub carry."
                : type == SegmentType.TurnMiddle ? "Production order: constraint → correction → carry. NO positive drive."
                : "Production order: constraint → correction → remaining-distance drive. RunWide uses correction + carry, NO DRIVE. Full correction consumption also leaves NO DRIVE.");
            Line();
            Line("Crash probes retain existing half-segment crash semantics. Available m is legal remaining segment length; travelled m is production distance. Do not interpret crashes as a modeled crash trajectory or apply recoverable full-distance equality to them. At outer lane 4 production crashes instead of RunWide, so the recoverable transition and observed retention are absent, not inferred from an inner lane.");
            Line();
            Table("Scenario | Lateral | Progress | Radius | Available m | Travelled m | Incoming | Max safe | Correction capability | First Brake | First RunWide | First Crash | Observed retention | Outcome",
                turns.Select(result => Row(result.Metadata.ScenarioId, result.Scenario.LateralPosition, result.Scenario.SegmentProgress,
                    result.RadiusMeters, result.AvailableDistanceMeters, result.Diagnostics.TravelledMeters,
                    result.Change.EntrySpeed, result.Capability.MaxSafeSpeedMetersPerSecond,
                    result.Capability.CorrectionDecelerationMetersPerSecondSquared, result.Capability.FirstBrakeSpeedMetersPerSecond,
                    result.Capability.FirstRunWideSpeedMetersPerSecond, result.Capability.FirstCrashSpeedMetersPerSecond,
                    result.Capability.ObservedRunWideOverspeedRetention, result.Change.Outcome)));
            Table("Scenario | Scrub decel m | Scrub carry m | Scrub exit | Scrub time | Constraint input | Target | Required correction m | Correction m | Correction exit | Correction time | Remaining m | Reached | Residual | Carry m | Carry time | Final exit | Total time",
                turns.Select(result =>
                {
                    var s = result.Diagnostics.TurnEntryScrubProfile;
                    var c = result.Diagnostics.CornerSpeedCorrectionProfile;
                    return Row(result.Metadata.ScenarioId, s?.DecelerationDistanceMeters, s?.CarryDistanceMeters,
                        s?.ExitSpeedMetersPerSecond, s?.TravelTimeSeconds, s?.ExitSpeedMetersPerSecond ?? result.Change.EntrySpeed,
                        c?.TargetSpeedMetersPerSecond, c?.RequiredCorrectionDistanceMeters, c?.CorrectionDistanceMeters,
                        c?.ExitSpeedMetersPerSecond, c?.TravelTimeSeconds, c?.RemainingDistanceMeters,
                        c?.TargetReached, result.ResidualOverspeedMetersPerSecond, result.CarryDistanceMeters,
                        result.CarryTimeSeconds, result.Change.Speed, result.Diagnostics.TravelTimeSeconds);
                }));
            if (type == SegmentType.TurnExit)
                Table("Scenario | Drive m | Drive entry | Drive exit | Drive peak | Drive time | Drive accel m | Drive cruise m | Drive decel m | Entry net accel | Equilibrium",
                    turns.Select(result =>
                    {
                        var d = result.Diagnostics.TurnExitDriveProfile;
                        return Row(result.Metadata.ScenarioId, result.DriveDistanceMeters,
                            d.HasValue ? result.Diagnostics.CornerSpeedCorrectionProfile?.ExitSpeedMetersPerSecond ?? result.Change.PhysicsSpeed : null,
                            d?.ExitSpeedMetersPerSecond, d?.PeakSpeedMetersPerSecond, d?.TravelTimeSeconds,
                            d?.AccelerationDistanceMeters, d?.CruiseDistanceMeters, d?.DecelerationDistanceMeters,
                            d?.EntryNetAccelerationMetersPerSecondSquared, d?.FullDriveEquilibriumSpeedMetersPerSecond);
                    }));
        }
        Heading("Line Geometry");
        Line("Single-rider free runs request a fixed HoldLane reference 0–4. The observer never forcibly resets lateral position or disables production consequences; observed min/max verifies whether the line stayed fixed. Lap trajectory is a canonical geometry measurement; actual accumulated distance is reported separately.");
        Line();
        var lines = ordered.OfType<CalibrationLineResult>().ToArray();
        Table("Scenario | Lateral | Normalized fraction | Straight offset | Turn offset | Radius | Arc m | Reference lap m | Max safe | Observed min | Observed max",
            lines.Select(result => Row(result.Metadata.ScenarioId, result.Scenario.LateralPosition, result.NormalizedLateralFraction,
                result.StraightOffsetFromInnerReferenceMeters, result.TurnOffsetFromInnerReferenceMeters, result.RadiusMeters,
                result.TurnArcLengthMeters, result.ReferenceLapDistanceMeters, result.MaxSafeSpeedMetersPerSecond,
                result.Rider.MinimumObservedLateralPosition, result.Rider.MaximumObservedLateralPosition)));
        PerformanceTable(lines.Select(result => (result.Metadata.ScenarioId, result.Rider)));
        Heading("Full Heat");
        Line("Baseline = four identical riders; each skill/setup/surface axis remains a controlled fixture, not a real rider mapping. L1 penalty = L1 − median(L2,L3,L4). Correction residual is sampled before any subsequent TurnExit drive, not inferred from final overspeed.");
        Line();
        PerformanceTable(heats.SelectMany(heat => heat.Riders.Select(rider => (heat.Metadata.ScenarioId, rider))));
        Table("Scenario | Rider | Initial lane | Actual skills | Corrections | Residual count | Max residual | Observed min lateral | Observed max lateral",
            heats.SelectMany(heat => heat.Riders.Select(rider =>
            {
                var input = heat.Scenario.Riders.Single(item => item.RiderId == rider.Performance.RiderId);
                return Row(heat.Metadata.ScenarioId, input.RiderId, input.Lane, Skills(input.Skills), rider.CorrectionCount,
                    rider.ResidualOverspeedCount, rider.MaximumResidualOverspeedMetersPerSecond,
                    rider.MinimumObservedLateralPosition, rider.MaximumObservedLateralPosition);
            })));
        Heading("Within-Heat");
        Line("Identical baseline measures fixed-line geometry plus normal production evolution. Synthetic [25,50,50,75] skill patterns on lanes [0,1,2,3] measure the combined line/skill result; differences from the matched baseline are not an unconfounded skill-only causal estimate. All spreads are max − min within one controlled four-rider heat, not a population distribution.");
        Line();
        Table("Scenario | Heat-time spread s | Vmax spread km/h | L1 spread s | Average-speed spread m/s",
            heats.Select(heat => Row(heat.Metadata.ScenarioId, heat.Spreads.HeatTimeSeconds, heat.Spreads.VmaxKph,
                heat.Spreads.L1Seconds, heat.Spreads.AverageSpeedMetersPerSecond)));
        Heading("Real-world context");
        Line("Read-only versioned PGEE dataset; classifications come from the existing evaluator. Comparable distributions are envelopes, never caps or equality targets. Absolute heat/lap times and total distance remain ContextOnlyUntilTrackGeometry. Unsupported individual reaction/SpeedAt2s/first-curve targets are not inferred. Four deterministic riders and one spread observation do not estimate real population quantiles.");
        Line();
        Table("Metric | Unit | Comparability | Real P10 | Real P50 | Real P90 | Baseline P50 | Notes",
            realWorldContext.Components.OrderBy(component => component.Definition.MetricId, StringComparer.Ordinal).Select(component =>
            {
                var comparisons = component.QuantileComparisons;
                return Row(component.Definition.MetricId, component.Definition.Unit, component.Definition.Comparability,
                    comparisons.SingleOrDefault(q => q.Percentile == 10)?.RealValue,
                    comparisons.SingleOrDefault(q => q.Percentile == 50)?.RealValue,
                    comparisons.SingleOrDefault(q => q.Percentile == 90)?.RealValue,
                    comparisons.SingleOrDefault(q => q.Percentile == 50)?.SimulationValue, component.Notes);
            }));
        Heading("Interpretation");
        var startBase = ordered.OfType<CalibrationStartResult>().Single(result => result.Metadata.ScenarioId == "start/baseline");
        var straightBase = ordered.OfType<CalibrationStraightResult>().Single(result => result.Metadata.ScenarioId == "straight/baseline");
        Line($"Prepared baseline launch: reaction {F(startBase.Profile.ReactionTimeSeconds)} s, peak {F(startBase.Profile.PeakSpeedMetersPerSecond)} m/s, exit {F(startBase.Profile.ExitSpeedMetersPerSecond)} m/s, preparation {F(startBase.Profile.PreparationDistanceMeters)} m. Free straight at 16 m/s over 30 m exits at {F(straightBase.Profile.ExitSpeedMetersPerSecond)} m/s versus diagnostic equilibrium {F(straightBase.Profile.FullDriveEquilibriumSpeedMetersPerSecond)} m/s. These isolate launch/preparation and finite-distance straight response, not a fitted performance target.");
        Line();
        Line($"The controlled heat has {baseline.Riders.Sum(rider => rider.CorrectionCount)} correction profiles and {baseline.Riders.Sum(rider => rider.ResidualOverspeedCount)} residual-overspeed observations. Compare TurnEntry scrub/correction losses with TurnExit's actual remaining drive distance in the tables; no distance is granted twice. Higher capabilities do not imply monotonic total race time in every geometry/contact fixture.");
        Line();
        foreach (var id in new[] { "pge_clean_vmax", "pge_clean_average_speed", "pge_clean_l1_penalty" })
        {
            var metric = realWorldContext.Components.Single(component => component.Definition.MetricId == id);
            var median = metric.QuantileComparisons.Single(q => q.Percentile == 50);
            var low = metric.QuantileComparisons.Single(q => q.Percentile == 10).RealValue;
            var high = metric.QuantileComparisons.Single(q => q.Percentile == 90).RealValue;
            var position = median.SimulationValue < low ? "below" : median.SimulationValue > high ? "above" : "inside";
            Line($"Baseline {id} P50 = {F(median.SimulationValue)} {metric.Definition.Unit}, {position} the source P10–P90 envelope [{F(low)}, {F(high)}]. This observation is not an instruction to change a constant.");
            Line();
        }
        Line("TrackReading, Adaptability and PairRiding: No production effect in this scenario (isolated start/straight/corner probes, verified by endpoint comparisons). This does not claim they are unused by decisions, contact or the entire game. SlideControl thresholds/retention/correction and Speed's corner capability are observed directly. Gearing affects response and equilibrium; it is not a universal upgrade. TractionBias is probed only in corners where production uses it. Geometry line differences and synthetic skill spreads are measurements, not errors to equalize.");
        Line();
        Line("No tuning recommendations, optimizer, loss/OverallAccuracy score, new RNG, rider mapping, caps, gate bonuses, surface model or surrogate physics. Production physics files are unchanged.");
        return b.ToString();

        void Line(string value = "") => b.Append(value).Append('\n');
        void Heading(string title) { Line(); Line($"## {title}"); Line(); }
        void Table(string header, IEnumerable<object?[]> rows)
        {
            Line($"| {header} |");
            Line($"| {string.Join(" | ", header.Split('|').Select(_ => "---"))} |");
            foreach (var row in rows) Line($"| {string.Join(" | ", row.Select(F))} |");
            Line();
        }
        void PerformanceTable(IEnumerable<(string Id, CalibrationHeatRiderObservation Rider)> rows)
            => Table("Scenario | Rider | Vmax km/h | Average m/s | L1 | L2 | L3 | L4 | Flying median | L1 penalty | Heat time | Distance | RunWide | Brake | Crash",
                rows.Select(row =>
                {
                    var p = row.Rider.Performance;
                    return Row(row.Id, p.RiderId, CalibrationUnits.MetersPerSecondToKph(p.MaximumSpeedMetersPerSecond),
                        p.AverageSpeedMetersPerSecond, p.L1Seconds, p.L2Seconds, p.L3Seconds, p.L4Seconds,
                        p.FlyingLapMedianSeconds, p.FirstLapPenaltySeconds, p.TotalTimeSeconds, p.TotalDistanceMeters,
                        p.RunWideCount, p.BrakeCount, p.CrashCount);
                }));
    }

    private static CalibrationScenario Scenario(CalibrationScenarioResult result) => result switch
    {
        CalibrationStartResult start => start.Scenario, CalibrationStraightResult straight => straight.Scenario,
        CalibrationTurnResult turn => turn.Scenario, CalibrationLineResult line => line.Scenario,
        CalibrationHeatResult heat => heat.Scenario, _ => throw new ArgumentException("Unknown result type.", nameof(result)),
    };
    private static string Skills(RiderSkills skills) => string.Join("/", new[]
        { skills.Start, skills.Speed, skills.SlideControl, skills.TrackReading, skills.PairRiding, skills.Adaptability }.Select(value => F(value)));
    private static object?[] Row(params object?[] values) => values;
    private static string F(object? value) => value switch
    {
        null => "—", float number => number.ToString("0.######", CultureInfo.InvariantCulture),
        double number => number.ToString("0.######", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false", IFormattable item => item.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" "),
    };
}
