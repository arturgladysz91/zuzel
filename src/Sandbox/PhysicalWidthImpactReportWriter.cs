using System.Globalization;
using System.Text;
using CoreSim;
using CoreSim.Analysis;

internal static class PhysicalWidthImpactReportWriter
{
    private static readonly ScenarioSnapshot HistoricalBalanced = new(
        73.812906d,
        17.33039d,
        1.024132d,
        66.984001d,
        1.109383d,
        2.736893d,
        0.303368d,
        0.838881d,
        0,
        0,
        0);

    private static readonly IReadOnlyDictionary<string, SweepSnapshot> HistoricalSweeps =
        new Dictionary<string, SweepSnapshot>(StringComparer.Ordinal)
        {
            ["speed_0"] = new(70.615407d, 0d, 16.161275d, 0d, 0.895885d, 0d),
            ["speed_50"] = new(73.812906d, 0d, 17.33039d, 0d, 1.024132d, 0d),
            ["speed_100"] = new(79.18968d, 0d, 18.417853d, 0d, 1.11051d, 0d),
            ["slide_control_0"] = new(71.681304d, 0d, 16.828265d, 0d, 0.986497d, 0d),
            ["slide_control_50"] = new(73.812906d, 0d, 17.33039d, 0d, 1.024132d, 0d),
            ["slide_control_100"] = new(75.957351d, 0d, 17.799085d, 0d, 1.050641d, 0d),
        };

    public static void Write(string datasetDirectory, string historicalBaselinePath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(historicalBaselinePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (StringComparer.OrdinalIgnoreCase.Equals(
                Path.GetFullPath(historicalBaselinePath), Path.GetFullPath(outputPath)))
            throw new ArgumentException("The impact report must not overwrite the historical baseline.", nameof(outputPath));

        ValidateHistoricalBaseline(File.ReadAllText(historicalBaselinePath));
        var dataset = RealWorldCalibrationDataset.ParseCsv(
            File.ReadAllText(Path.Combine(datasetDirectory, "pge_rider_heats.csv")));
        var sweeps = CalibrationSkillSweep.RunRequired();
        var evaluations = sweeps.ToDictionary(
            sweep => sweep.Scenario.ScenarioId,
            sweep => RealWorldCalibrationEvaluator.Evaluate(dataset, sweep.ToSimulationCalibrationResult()),
            StringComparer.Ordinal);
        var balanced = sweeps.Single(sweep => sweep.Scenario.ScenarioId == "balanced");
        var currentBalanced = Snapshot(balanced);
        var standingTrack = Track.CreateStandingStartExample();
        var geometry = standingTrack.Geometry;
        var historicalGeometry = new TrackGeometry(
            geometry.StraightLengthMeters, geometry.InnerRadiusMeters, 6f, 6f,
            geometry.TurnSegmentAngleRadians);

        var builder = new StringBuilder();
        builder.AppendLine("# Physical track-width impact");
        builder.AppendLine();
        builder.AppendLine("This deterministic observation compares the historical PR #32 production baseline with the PR #33 segment-local physical-width geometry. It uses the unchanged versioned PGEE dataset, `RealWorldCalibrationEvaluator`, and the production-path `CalibrationSkillSweep`; it does not tune any model constant.");
        builder.AppendLine();
        builder.AppendLine("## Geometry");
        builder.AppendLine();
        builder.AppendLine("`LateralPosition` remains a dimensionless continuous coordinate from 0 to 4. The old effective reference used 1 m between every reference position. The new conversion derives physical metres from the current segment width, with a 1 m FIM inner measurement/reference offset and a separate provisional 1 m game margin at the outer edge.");
        builder.AppendLine();
        builder.AppendLine("| Measure | Before | After |");
        builder.AppendLine("|---|---:|---:|");
        builder.AppendLine($"| Straight physical width | 6 m effective compatibility width | {F(geometry.StraightWidthMeters)} m |");
        builder.AppendLine($"| Turn physical width | 6 m effective compatibility width | {F(geometry.TurnWidthMeters)} m |");
        builder.AppendLine($"| Straight usable span | 4 m | {F(LaneModel.UsableRacingWidthMeters(SegmentType.Straight, geometry))} m |");
        builder.AppendLine($"| Turn usable span | 4 m | {F(LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, geometry))} m |");
        builder.AppendLine($"| Straight reference spacing | 1 m | {F(LaneModel.ReferenceLaneSpacingMeters(SegmentType.Straight, geometry))} m |");
        builder.AppendLine($"| Turn reference spacing | 1 m | {F(LaneModel.ReferenceLaneSpacingMeters(SegmentType.TurnMiddle, geometry))} m |");
        builder.AppendLine("| Turn radii at positions 0/1/2/3/4 | 24 / 25 / 26 / 27 / 28 m | 24 / 27 / 30 / 33 / 36 m |");
        builder.AppendLine();
        builder.AppendLine("The standing example's 10 m straight and 14 m turn are an FIM-minimum-width example, not global dimensions for every real track. `CreateExample()` remains a 6 m / 6 m synthetic compatibility fixture.");
        builder.AppendLine();
        builder.AppendLine("## Lap distances");
        builder.AppendLine();
        builder.AppendLine("The before column reconstructs the historical effective geometry `130 + 2π × (24 + LateralPosition)`. The after column sums the current physical segments. Position 0 remains the canonical 1 m inner measurement/reference trajectory; position 4 is not an official track-length measurement.");
        builder.AppendLine();
        builder.AppendLine("| LateralPosition | Before m | After m | Delta m |");
        builder.AppendLine("|---:|---:|---:|---:|");
        foreach (var position in new[] { 0f, 1f, 2f, 3f, 4f })
        {
            var before = standingTrack.Segments.Sum(segment =>
                (double)LaneModel.SegmentLengthMeters(segment, position, historicalGeometry));
            var after = standingTrack.Segments.Sum(segment =>
                (double)LaneModel.SegmentLengthMeters(segment, position, geometry));
            builder.AppendLine($"| {F(position)} | {F(before)} | {F(after)} | {F(after - before)} |");
        }
        builder.AppendLine();
        builder.AppendLine("## Balanced production fixture");
        builder.AppendLine();
        builder.AppendLine($"The fixture uses HoldLane decisions, incidents disabled, seed {CalibrationSkillSweep.FixedSeed}, dry weather, neutral setup, perfect/neutral surface, and all six skills at 50.");
        builder.AppendLine();
        builder.AppendLine("| Metric | Before | After | Delta |");
        builder.AppendLine("|---|---:|---:|---:|");
        AddBalanced("Vmax median (km/h)", HistoricalBalanced.VmaxKph, currentBalanced.VmaxKph);
        AddBalanced("Average-speed median (m/s)", HistoricalBalanced.AverageSpeedMetersPerSecond, currentBalanced.AverageSpeedMetersPerSecond);
        AddBalanced("L1-penalty median (s)", HistoricalBalanced.L1PenaltySeconds, currentBalanced.L1PenaltySeconds);
        AddBalanced("Total-heat-time median (s)", HistoricalBalanced.TotalHeatTimeSeconds, currentBalanced.TotalHeatTimeSeconds);
        AddBalanced("Four-rider heat-time spread (s)", HistoricalBalanced.HeatTimeSpreadSeconds, currentBalanced.HeatTimeSpreadSeconds);
        AddBalanced("Four-rider Vmax spread (km/h)", HistoricalBalanced.VmaxSpreadKph, currentBalanced.VmaxSpreadKph);
        AddBalanced("Four-rider L1 spread (s)", HistoricalBalanced.L1SpreadSeconds, currentBalanced.L1SpreadSeconds);
        AddBalanced("Four-rider average-speed spread (m/s)", HistoricalBalanced.AverageSpeedSpreadMetersPerSecond, currentBalanced.AverageSpeedSpreadMetersPerSecond);
        AddBalanced("RunWide count", HistoricalBalanced.RunWideCount, currentBalanced.RunWideCount);
        AddBalanced("Brake count", HistoricalBalanced.BrakeCount, currentBalanced.BrakeCount);
        AddBalanced("Crash count", HistoricalBalanced.CrashCount, currentBalanced.CrashCount);
        builder.AppendLine();
        builder.AppendLine("## Selected skill-sweep percentile positions");
        builder.AppendLine();
        builder.AppendLine("Percentiles locate each four-rider model median in the real PGEE distribution. They are observations, not targets or hard caps.");
        builder.AppendLine();
        builder.AppendLine("| Scenario | Vmax before / after km/h | Vmax percentile before / after | Average before / after m/s | Average percentile before / after | L1 penalty before / after s | L1 percentile before / after |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (var scenarioId in new[]
                 {
                     "speed_0", "speed_50", "speed_100",
                     "slide_control_0", "slide_control_50", "slide_control_100",
                 })
        {
            var before = HistoricalSweeps[scenarioId];
            var sweep = sweeps.Single(item => item.Scenario.ScenarioId == scenarioId);
            var after = Sweep(sweep, evaluations[scenarioId]);
            builder.AppendLine($"| `{scenarioId}` | {F(before.VmaxKph)} / {F(after.VmaxKph)} | {F(before.VmaxPercentile)} / {F(after.VmaxPercentile)} | {F(before.AverageSpeedMetersPerSecond)} / {F(after.AverageSpeedMetersPerSecond)} | {F(before.AverageSpeedPercentile)} / {F(after.AverageSpeedPercentile)} | {F(before.L1PenaltySeconds)} / {F(after.L1PenaltySeconds)} | {F(before.L1PenaltyPercentile)} / {F(after.L1PenaltyPercentile)} |");
        }
        builder.AppendLine();
        LateralTraversalSection(builder, geometry);
        PhysicsInvariantSection(builder);
        builder.AppendLine("## Interpretation and boundaries");
        builder.AppendLine();
        builder.AppendLine("Wider normalized positions now have longer arcs and larger curvature radii, while physical occupancy/contact and displacement use the local segment width. Changes in timing, speed, incidents, and real-data percentile position are therefore geometry observations, not a reason to tune this PR.");
        builder.AppendLine();
        builder.AppendLine("The five surface bands remain normalized and their wear/grip formulas are unchanged. Segment boundaries reinterpret the same normalized position against the local width without a synthetic lateral event, width-transition spline, or additional distance/time. Active lateral movement still has no diagonal/spiral path-length correction. Physical A/B/C/D starting gates, rider/motorcycle width, and calibration remain out of scope.");
        builder.AppendLine();
        builder.AppendLine("Regenerate from the repository root with:");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine("dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- physical-width-impact-report data/calibration/pge/v1 docs/calibration/current-model-baseline.md docs/calibration/physical-width-impact.md");
        builder.AppendLine("```");

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (directory is not null)
            Directory.CreateDirectory(directory);
        File.WriteAllText(outputPath, builder.ToString().ReplaceLineEndings("\n"), new UTF8Encoding(false));
        return;

        void AddBalanced(string label, double before, double after)
            => builder.AppendLine($"| {label} | {F(before)} | {F(after)} | {F(after - before)} |");
    }

    private static void LateralTraversalSection(StringBuilder builder, TrackGeometry geometry)
    {
        var rate = LateralMovementModel.CalculateNormalizedLateralTraversalRateLaneUnitsPerSecond(RiderSkills.Balanced);
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var straightMetersPerSecond = LateralMovementModel.CalculateMaxLateralDistanceMeters(
            1f, SegmentType.Straight, geometry, surface, RiderSkills.Balanced);
        var turnMetersPerSecond = LateralMovementModel.CalculateMaxLateralDistanceMeters(
            1f, SegmentType.TurnMiddle, geometry, surface, RiderSkills.Balanced);
        var straightSpacing = LaneModel.ReferenceLaneSpacingMeters(SegmentType.Straight, geometry);
        var turnSpacing = LaneModel.ReferenceLaneSpacingMeters(SegmentType.TurnMiddle, geometry);

        builder.AppendLine("## Balanced lateral-traversal diagnostic");
        builder.AppendLine();
        builder.AppendLine("The diagnostic uses the perfect/neutral surface from the controlled fixture, so the unchanged grip multiplier is 1.");
        builder.AppendLine();
        builder.AppendLine("| Measure | Value |");
        builder.AppendLine("|---|---:|");
        builder.AppendLine($"| Normalized traversal rate | {F(rate)} lane-units/s |");
        builder.AppendLine($"| Physical equivalent on 10 m straight | {F(straightMetersPerSecond)} m/s |");
        builder.AppendLine($"| Physical equivalent on 14 m turn | {F(turnMetersPerSecond)} m/s |");
        builder.AppendLine($"| Lane 0 -> Lane 1 time on straight | {F(straightSpacing / straightMetersPerSecond)} s |");
        builder.AppendLine($"| Lane 0 -> Lane 1 time on turn | {F(turnSpacing / turnMetersPerSecond)} s |");
        builder.AppendLine();
        builder.AppendLine("Formula: `execution = 0.5 × SlideControlNorm + 0.5 × AdaptabilityNorm`; `laneRate = 0.35 + (0.65 - 0.35) × execution`; `maxLaneDelta = laneRate × time × gripMultiplier`; `physicalMeters = maxLaneDelta × localReferenceSpacing`. Values 0.35-0.65 were not tuned.");
        builder.AppendLine();
    }

    private static void PhysicsInvariantSection(StringBuilder builder)
    {
        builder.AppendLine("## Physics invariant");
        builder.AppendLine();
        builder.AppendLine("**NO SPEED/PERFORMANCE CONSTANTS CHANGED. NO CALIBRATION WAS PERFORMED.** The checked production constants remain:");
        builder.AppendLine();
        builder.AppendLine("| Constant/formula | Before | After |");
        builder.AppendLine("|---|---:|---:|");
        AddNumber("SegmentPhysics.ReferenceTurnRadiusMeters", SegmentPhysics.ReferenceTurnRadiusMeters);
        AddNumber("SegmentPhysics.ReferenceTurnSpeedMetersPerSecond", SegmentPhysics.ReferenceTurnSpeedMetersPerSecond);
        AddNumber("SegmentPhysics.BrakeSpeedFactor", SegmentPhysics.BrakeSpeedFactor);
        AddNumber("SegmentPhysics.RunWideSpeedFactor", SegmentPhysics.RunWideSpeedFactor);
        AddText("standing launch acceleration (m/s²)", $"{F(LongitudinalDynamics.ProvisionalStandingStartMinimumReferenceAccelerationMetersPerSecondSquared)}-{F(LongitudinalDynamics.ProvisionalStandingStartMaximumReferenceAccelerationMetersPerSecondSquared)}");
        AddText("reaction time (s)", $"{F(LongitudinalDynamics.ProvisionalStandingStartSlowReactionSeconds)}-{F(LongitudinalDynamics.ProvisionalStandingStartFastReactionSeconds)}");
        AddText("Straight acceleration (m/s²)", $"{F(LongitudinalDynamics.MinStraightAccelerationMetersPerSecondSquared)}-{F(LongitudinalDynamics.MaxStraightAccelerationMetersPerSecondSquared)}");
        AddText("TurnExit acceleration (m/s²)", $"{F(LongitudinalDynamics.MinTurnExitAccelerationMetersPerSecondSquared)}-{F(LongitudinalDynamics.MaxTurnExitAccelerationMetersPerSecondSquared)}");
        AddText("corner preparation deceleration (m/s²)", $"{F(LongitudinalDynamics.MinCornerEntryDecelerationMetersPerSecondSquared)}-{F(LongitudinalDynamics.MaxCornerEntryDecelerationMetersPerSecondSquared)}");
        AddNumber("nominal system mass (kg)", LongitudinalDynamics.ProvisionalNominalSystemMassKilograms);
        AddText("resistance (N)", $"{F(LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons)} + {F(LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient)} × v²");
        AddText("force fade", $"{F(LongitudinalDynamics.ProvisionalDriveOrientedForceFadePerMeterPerSecond)} -> {F(LongitudinalDynamics.ProvisionalSpeedOrientedForceFadePerMeterPerSecond)}");
        AddNumber("TurnEntry scrub fraction", LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction);
        AddText("turn control multiplier", "0.97 + control × 0.06");
        AddText("turn speed multiplier", "0.94 + speedAbility × 0.12");
        AddText("turn surface multiplier", "0.74 + effectiveGrip × 0.26");
        AddText("turn setup multiplier", "1.03 - setupError × 0.06");
        builder.AppendLine();
        return;

        void AddNumber(string label, double value) => AddText(label, F(value));
        void AddText(string label, string value) => builder.AppendLine($"| `{label}` | {value} | {value} |");
    }

    private static ScenarioSnapshot Snapshot(CalibrationSkillSweepResult sweep)
        => new(
            Median(sweep.Riders.Select(rider => (double)CalibrationUnits.MetersPerSecondToKph(rider.MaximumSpeedMetersPerSecond))),
            Median(sweep.Riders.Select(rider => (double)rider.AverageSpeedMetersPerSecond!.Value)),
            Median(sweep.Riders.Select(rider => (double)rider.FirstLapPenaltySeconds!.Value)),
            Median(sweep.Riders.Select(rider => (double)rider.TotalTimeSeconds)),
            Spread(sweep.Riders.Select(rider => (double)rider.TotalTimeSeconds)),
            Spread(sweep.Riders.Select(rider => (double)CalibrationUnits.MetersPerSecondToKph(rider.MaximumSpeedMetersPerSecond))),
            Spread(sweep.Riders.Select(rider => (double)rider.L1Seconds!.Value)),
            Spread(sweep.Riders.Select(rider => (double)rider.AverageSpeedMetersPerSecond!.Value)),
            sweep.Riders.Sum(rider => rider.RunWideCount),
            sweep.Riders.Sum(rider => rider.BrakeCount),
            sweep.Riders.Sum(rider => rider.CrashCount));

    private static SweepSnapshot Sweep(CalibrationSkillSweepResult sweep, CalibrationEvaluationReport evaluation)
        => new(
            Median(sweep.Riders.Select(rider => (double)CalibrationUnits.MetersPerSecondToKph(rider.MaximumSpeedMetersPerSecond))),
            P50Rank(evaluation, "pge_clean_vmax"),
            Median(sweep.Riders.Select(rider => (double)rider.AverageSpeedMetersPerSecond!.Value)),
            P50Rank(evaluation, "pge_clean_average_speed"),
            Median(sweep.Riders.Select(rider => (double)rider.FirstLapPenaltySeconds!.Value)),
            P50Rank(evaluation, "pge_clean_l1_penalty"));

    private static double P50Rank(CalibrationEvaluationReport report, string metricId)
        => report.Components.Single(component => component.Definition.MetricId == metricId)
            .QuantileComparisons.Single(comparison => comparison.Percentile == 50)
            .SimulationValueRealEmpiricalPercentile;

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        return CalibrationDistribution.LinearQuantile(ordered, 0.5d);
    }

    private static double Spread(IEnumerable<double> values)
    {
        var materialized = values.ToArray();
        return materialized.Max() - materialized.Min();
    }

    private static void ValidateHistoricalBaseline(string report)
    {
        var expected = new[]
        {
            "| `balanced` | 0.24 | 2.22934 | 73.812906 | 0 | 17.33039 | 0 | 1.024132 | 0 | 1.109383 | 2.736893 | 0 | 0 | 0 |",
            "| Four-rider L1 spread | ComparableEnvelope | s | 0.53 | 0.303368 |",
            "| Four-rider average-speed spread | ComparableEnvelope | m/s | 0.927094 | 0.838881 |",
            "| Heat time | ContextOnlyUntilTrackGeometry | s | 64.688 | 66.984001 |",
        };
        foreach (var fragment in expected)
        {
            if (!report.Contains(fragment, StringComparison.Ordinal))
                throw new InvalidDataException("Historical PR #32 baseline does not match the expected pre-geometry snapshot.");
        }
        foreach (var (scenarioId, snapshot) in HistoricalSweeps)
        {
            var row = report.Split('\n').SingleOrDefault(line =>
                line.StartsWith($"| `{scenarioId}` |", StringComparison.Ordinal));
            var cells = row?.Split('|').Select(cell => cell.Trim()).ToArray();
            if (cells is null || cells.Length != 16
                || cells[4] != F(snapshot.VmaxKph)
                || cells[5] != F(snapshot.VmaxPercentile)
                || cells[6] != F(snapshot.AverageSpeedMetersPerSecond)
                || cells[7] != F(snapshot.AverageSpeedPercentile)
                || cells[8] != F(snapshot.L1PenaltySeconds)
                || cells[9] != F(snapshot.L1PenaltyPercentile))
                throw new InvalidDataException($"Historical sweep '{scenarioId}' does not match the pre-geometry snapshot.");
        }
    }

    private static string F(double value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);

    private sealed record ScenarioSnapshot(
        double VmaxKph,
        double AverageSpeedMetersPerSecond,
        double L1PenaltySeconds,
        double TotalHeatTimeSeconds,
        double HeatTimeSpreadSeconds,
        double VmaxSpreadKph,
        double L1SpreadSeconds,
        double AverageSpeedSpreadMetersPerSecond,
        int RunWideCount,
        int BrakeCount,
        int CrashCount);

    private sealed record SweepSnapshot(
        double VmaxKph,
        double VmaxPercentile,
        double AverageSpeedMetersPerSecond,
        double AverageSpeedPercentile,
        double L1PenaltySeconds,
        double L1PenaltyPercentile);
}
