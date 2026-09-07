using System.Globalization;
using System.Text;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

internal static class ContinuousCornerCorrectionImpactReportWriter
{
    private static readonly TrackSurfaceState PerfectNeutralSurface = new(1f, 0f, 0.35f);

    private static readonly ScenarioSnapshot HistoricalBalanced = new(
        76.440358d,
        18.142772d,
        1.056545d,
        68.129131d,
        3.473694d,
        7.702803d,
        0.941988d,
        2.396242d,
        144,
        0,
        0,
        0);

    private static readonly IReadOnlyDictionary<string, SweepSnapshot> HistoricalSweeps =
        new Dictionary<string, SweepSnapshot>(StringComparer.Ordinal)
        {
            ["speed_0"] = new(73.185284d, 0d, 16.923858d, 0d, 0.924995d, 0d),
            ["speed_50"] = new(76.440358d, 0d, 18.142772d, 0d, 1.056545d, 0d),
            ["speed_100"] = new(82.043818d, 0d, 19.281636d, 0d, 1.147342d, 0d),
            ["slide_control_0"] = new(74.31656d, 0d, 17.625194d, 0d, 1.018971d, 0d),
            ["slide_control_50"] = new(76.440358d, 0d, 18.142772d, 0d, 1.056545d, 0d),
            ["slide_control_100"] = new(78.482967d, 0d, 18.628987d, 0d, 1.084136d, 0d),
        };

    public static void Write(string datasetDirectory, string historicalWidthReportPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(historicalWidthReportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (StringComparer.OrdinalIgnoreCase.Equals(
                Path.GetFullPath(historicalWidthReportPath), Path.GetFullPath(outputPath)))
        {
            throw new ArgumentException(
                "The continuous-correction report must not overwrite the historical #33 report.",
                nameof(outputPath));
        }

        ValidateHistoricalWidthReport(File.ReadAllText(historicalWidthReportPath));
        var dataset = RealWorldCalibrationDataset.ParseCsv(
            File.ReadAllText(Path.Combine(datasetDirectory, "pge_rider_heats.csv")));
        var sweeps = CalibrationSkillSweep.RunRequired();
        var evaluations = sweeps.ToDictionary(
            sweep => sweep.Scenario.ScenarioId,
            sweep => RealWorldCalibrationEvaluator.Evaluate(
                dataset,
                sweep.ToSimulationCalibrationResult()),
            StringComparer.Ordinal);
        var balancedSweep = sweeps.Single(sweep => sweep.Scenario.ScenarioId == "balanced");
        var balancedTrace = RunControlledHeat(RiderSkills.Balanced);
        var currentBalanced = Snapshot(balancedSweep, balancedTrace);
        var probes = ControlledProbes();

        var builder = new StringBuilder();
        builder.AppendLine("# Continuous corner-speed correction impact");
        builder.AppendLine();
        builder.AppendLine("This deterministic PR #34 report is generated through production `SimulationEngine` and `CalibrationRunner -> HeatSimulator` paths. It compares the immutable PR #33 physical-width report with continuous, distance-limited corner correction. No physics calibration was performed.");
        builder.AppendLine();
        builder.AppendLine("## Constraint and correction model");
        builder.AppendLine();
        builder.AppendLine("Advanced `SegmentPhysics` now classifies `Ok`, `Brake`, `RunWide`, or `Crash` and emits a nullable correction target. Recoverable outcomes keep their input speed at this discrete stage. `LongitudinalDynamics` then applies `requiredDistance = (entry² - target²) / (2 × deceleration)` over actual remaining metres and uses `time = 2 × distance / (entry + exit)`. An unreachable target leaves residual overspeed. Legacy resolution remains instantaneous.");
        builder.AppendLine();
        builder.AppendLine("TurnEntry retains its first-half scrub and gives only post-scrub distance to correction. TurnMiddle corrects and then carries. TurnExit corrects first and gives only correction remainder to drive; RunWide carries instead and receives no drive.");
        builder.AppendLine();
        builder.AppendLine("## Controlled corner probes");
        builder.AppendLine();
        builder.AppendLine("Balanced rider, neutral setup, perfect/neutral surface, standing-example physical width, lane/position 1, incidents disabled. Band speeds are derived from the current quiet, balanced Brake, and balanced RunWide factors. TurnEntry's displayed entry speed is chosen so its unchanged first-half scrub exits in the requested constraint band.");
        builder.AppendLine();
        builder.AppendLine("| Segment | Band | Entry m/s | Max safe m/s | Outcome | Target m/s | Available correction m | Required correction m | Actual correction m | Target reached | Correction exit m/s | Remaining m | Drive m | Final exit m/s | Travel time s |");
        builder.AppendLine("|---|---|---:|---:|---|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|");
        foreach (var probe in probes)
        {
            builder.AppendLine($"| {probe.Segment} | {probe.Band} | {F(probe.EntrySpeed)} | {F(probe.MaxSafeSpeed)} | {probe.Outcome} | {FN(probe.CorrectionTarget)} | {FN(probe.AvailableCorrectionDistance)} | {FN(probe.RequiredCorrectionDistance)} | {FN(probe.ActualCorrectionDistance)} | {BN(probe.TargetReached)} | {FN(probe.CorrectionExitSpeed)} | {FN(probe.RemainingDistance)} | {F(probe.DriveDistance)} | {F(probe.FinalExitSpeed)} | {F(probe.TravelTime)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Balanced fixture: PR #33 to PR #34");
        builder.AppendLine();
        builder.AppendLine($"The current fixture uses HoldLane decisions, incidents disabled, seed {CalibrationSkillSweep.FixedSeed}, dry weather, neutral setup, perfect/neutral surface, and all skills at 50. The before values are validated against the committed #33 report.");
        builder.AppendLine();
        builder.AppendLine("| Metric | #33 | #34 | Delta |");
        builder.AppendLine("|---|---:|---:|---:|");
        AddBalanced("Vmax median (km/h)", HistoricalBalanced.VmaxKph, currentBalanced.VmaxKph);
        AddBalanced("Average-speed median (m/s)", HistoricalBalanced.AverageSpeedMetersPerSecond, currentBalanced.AverageSpeedMetersPerSecond);
        AddBalanced("L1-penalty median (s)", HistoricalBalanced.L1PenaltySeconds, currentBalanced.L1PenaltySeconds);
        AddBalanced("Total-heat-time median (s)", HistoricalBalanced.TotalHeatTimeSeconds, currentBalanced.TotalHeatTimeSeconds);
        AddBalanced("Four-rider heat-time spread (s)", HistoricalBalanced.HeatTimeSpreadSeconds, currentBalanced.HeatTimeSpreadSeconds);
        AddBalanced("Four-rider Vmax spread (km/h)", HistoricalBalanced.VmaxSpreadKph, currentBalanced.VmaxSpreadKph);
        AddBalanced("Four-rider L1 spread (s)", HistoricalBalanced.L1SpreadSeconds, currentBalanced.L1SpreadSeconds);
        AddBalanced("Four-rider average-speed spread (m/s)", HistoricalBalanced.AverageSpeedSpreadMetersPerSecond, currentBalanced.AverageSpeedSpreadMetersPerSecond);
        AddBalanced("Ok count", HistoricalBalanced.OkCount, currentBalanced.OkCount);
        AddBalanced("Brake count", HistoricalBalanced.BrakeCount, currentBalanced.BrakeCount);
        AddBalanced("RunWide count", HistoricalBalanced.RunWideCount, currentBalanced.RunWideCount);
        AddBalanced("Crash count", HistoricalBalanced.CrashCount, currentBalanced.CrashCount);

        builder.AppendLine();
        builder.AppendLine("## Speed and SlideControl diagnostics");
        builder.AppendLine();
        builder.AppendLine("Real percentile positions are deterministic midranks in the versioned PGEE distributions. They are observations, not fitting targets.");
        builder.AppendLine();
        builder.AppendLine("| Scenario | #33 / #34 Vmax km/h | #34 real percentile | #33 / #34 average m/s | #34 real percentile | #33 / #34 L1 penalty s | #34 real percentile |");
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
            builder.AppendLine($"| `{scenarioId}` | {F(before.VmaxKph)} / {F(after.VmaxKph)} | {F(after.VmaxPercentile)} | {F(before.AverageSpeedMetersPerSecond)} / {F(after.AverageSpeedMetersPerSecond)} | {F(after.AverageSpeedPercentile)} | {F(before.L1PenaltySeconds)} / {F(after.L1PenaltySeconds)} | {F(after.L1PenaltyPercentile)} |");
        }

        builder.AppendLine();
        CorrectionDiagnosticsSection(builder, balancedTrace);
        PhysicsInvariantSection(builder);
        builder.AppendLine("## Interpretation and boundary");
        builder.AppendLine();
        builder.AppendLine("PR #34 changes the realization of recoverable corner constraints, not their thresholds. Correction uses the entry-sampled surface and entry `LateralPosition`; it does not resample per metre, change radius during lateral movement, add RNG, or model brakes, engine RPM, torque, clutch, wheelspin, banking, diagonal paths, contact redesign, or crash trajectories. Random incident probability, channels, severity, immediate 0.88 consequence, and crash semantics remain separate. No constants were tuned in response to these results.");
        builder.AppendLine();
        builder.AppendLine("Regenerate from the repository root with:");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine("dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- continuous-corner-correction-impact-report data/calibration/pge/v1 docs/calibration/physical-width-impact.md docs/calibration/continuous-corner-correction-impact.md");
        builder.AppendLine("```");

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (directory is not null)
            Directory.CreateDirectory(directory);
        File.WriteAllText(
            outputPath,
            builder.ToString().ReplaceLineEndings("\n"),
            new UTF8Encoding(false));
        return;

        void AddBalanced(string label, double before, double after)
            => builder.AppendLine($"| {label} | {F(before)} | {F(after)} | {F(after - before)} |");
    }

    private static IReadOnlyList<ProbeSnapshot> ControlledProbes()
    {
        var control = RiderSkills.Normalize(RiderSkills.Balanced.SlideControl);
        var brakeFactor = SegmentPhysics.MinAdvancedBrakeSpeedFactor
            + control * SegmentPhysics.AdvancedBrakeSpeedFactorRange;
        var runWideFactor = SegmentPhysics.MinAdvancedRunWideSpeedFactor
            + control * SegmentPhysics.AdvancedRunWideSpeedFactorRange;
        var bands = new[]
        {
            ("below max", 0.98f),
            ("quiet correction", (1f + SegmentPhysics.AdvancedQuietCorrectionSpeedFactor) * 0.5f),
            ("Brake", (SegmentPhysics.AdvancedQuietCorrectionSpeedFactor + brakeFactor) * 0.5f),
            ("RunWide", (brakeFactor + runWideFactor) * 0.5f),
        };
        return new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit }
            .SelectMany(segment => bands.Select(band => RunProbe(segment, band.Item1, band.Item2)))
            .ToArray();
    }

    private static ProbeSnapshot RunProbe(SegmentType segmentType, string band, float constraintSpeedFactor)
    {
        var geometry = Track.CreateStandingStartExample().Geometry;
        var segment = new TrackSegment(0, segmentType);
        var track = new Track(new[] { segment }, geometry);
        var rider = new RiderState(
            new RiderProfile(1, "Controlled probe", RiderSkills.Balanced, RiderStyle.Balanced),
            lane: 1)
        {
            LateralPosition = 1f,
            ActiveSetup = BikeSetup.Neutral,
        };
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            rider.LateralPosition,
            geometry,
            PerfectNeutralSurface,
            rider.Profile.Skills,
            rider.ActiveSetup);
        var desiredConstraintSpeed = maxSafeSpeed * constraintSpeedFactor;
        rider.Speed = desiredConstraintSpeed;
        if (segmentType == SegmentType.TurnEntry && desiredConstraintSpeed > maxSafeSpeed)
        {
            var distance = LaneModel.SegmentLengthMeters(segment, rider.LateralPosition, geometry);
            var deceleration = LongitudinalDynamics
                .CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
                    rider.Profile.Skills,
                    PerfectNeutralSurface);
            var scrubDistance = distance
                * LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction;
            rider.Speed = MathF.Sqrt(
                desiredConstraintSpeed * desiredConstraintSpeed
                + 2f * deceleration * scrubDistance);
        }

        var options = ControlledOptions();
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track, PerfectNeutralSurface),
            new[] { rider },
            new SimulationStepContext(34, 0, 0, 0, options.Seed, options.Laps));
        var resolved = engine.Resolve(snapshot, engine.Decide(snapshot), options);
        var change = resolved.Changes.Single();
        var diagnostics = resolved.Diagnostics.Single();
        var correction = diagnostics.CornerSpeedCorrectionProfile;
        var driveDistance = diagnostics.TurnExitDriveProfile is { } drive
            ? drive.AccelerationDistanceMeters + drive.CruiseDistanceMeters
              + drive.DecelerationDistanceMeters
            : 0f;
        return new ProbeSnapshot(
            segmentType,
            band,
            change.EntrySpeed,
            maxSafeSpeed,
            change.Outcome,
            correction?.TargetSpeedMetersPerSecond,
            correction is { } profile
                ? profile.CorrectionDistanceMeters + profile.RemainingDistanceMeters
                : null,
            correction?.RequiredCorrectionDistanceMeters,
            correction?.CorrectionDistanceMeters,
            correction?.TargetReached,
            correction?.ExitSpeedMetersPerSecond,
            correction?.RemainingDistanceMeters,
            driveDistance,
            change.Speed,
            diagnostics.TravelTimeSeconds);
    }

    private static CalibrationTrace RunControlledHeat(RiderSkills skills)
    {
        var track = Track.CreateStandingStartExample();
        var riders = Enumerable.Range(1, 4)
            .Select(riderId => new RiderState(
                new RiderProfile(riderId, $"Calibration Rider {riderId}", skills, RiderStyle.Balanced),
                riderId - 1)
            {
                ActiveSetup = BikeSetup.Neutral,
            })
            .ToList();
        return CalibrationRunner.RunHeat(
            track,
            TrackState.CreateDefault(track, PerfectNeutralSurface),
            riders,
            new HoldLaneDecisionModel(),
            ControlledOptions(),
            heatId: 34);
    }

    private static HeatSimulationOptions ControlledOptions()
        => new()
        {
            Laps = 4,
            Seed = CalibrationSkillSweep.FixedSeed,
            Weather = WeatherState.Dry,
            IncidentFrequency = 0f,
            EnableLogging = false,
        };

    private static void CorrectionDiagnosticsSection(StringBuilder builder, CalibrationTrace trace)
    {
        var corrections = trace.StepSamples
            .Where(sample => sample.CornerCorrectionTargetSpeedMetersPerSecond.HasValue)
            .ToArray();
        var reached = corrections.Count(sample => sample.CornerCorrectionTargetReached == true);
        var residual = corrections.Length - reached;
        var averageDistance = corrections.Length == 0
            ? 0d
            : corrections.Average(sample => (double)sample.CornerCorrectionDistanceMeters!.Value);
        var maximumResidual = corrections
            .Where(sample => sample.CornerCorrectionTargetReached == false)
            .Select(sample => (double)(sample.CornerCorrectionExitSpeedMetersPerSecond!.Value
                - sample.CornerCorrectionTargetSpeedMetersPerSecond!.Value))
            .DefaultIfEmpty(0d)
            .Max();
        var correctionTime = corrections.Sum(
            sample => (double)sample.CornerCorrectionTravelTimeSeconds!.Value);
        var totalRiderTime = trace.RiderSummaries.Sum(summary => (double)summary.TotalTimeSeconds);

        builder.AppendLine("## Correction diagnostics in the controlled balanced heat");
        builder.AppendLine();
        builder.AppendLine("| Measure | Value |");
        builder.AppendLine("|---|---:|");
        builder.AppendLine($"| Correction profiles | {corrections.Length} |");
        builder.AppendLine($"| Mean correction distance (m) | {F(averageDistance)} |");
        builder.AppendLine($"| Target reached count | {reached} |");
        builder.AppendLine($"| Target reached rate | {F(corrections.Length == 0 ? 0d : (double)reached / corrections.Length)} |");
        builder.AppendLine($"| Residual overspeed count | {residual} |");
        builder.AppendLine($"| Maximum residual overspeed (m/s) | {F(maximumResidual)} |");
        builder.AppendLine($"| Correction time contribution (rider-s) | {F(correctionTime)} |");
        builder.AppendLine($"| Correction share of summed rider time | {F(totalRiderTime == 0d ? 0d : correctionTime / totalRiderTime)} |");
        builder.AppendLine();
    }

    private static void PhysicsInvariantSection(StringBuilder builder)
    {
        builder.AppendLine("## Physics invariant");
        builder.AppendLine();
        builder.AppendLine("**ALL LISTED CONSTANTS ARE UNCHANGED. NO CALIBRATION WAS PERFORMED.**");
        builder.AppendLine();
        builder.AppendLine("| Constant/formula | Value |");
        builder.AppendLine("|---|---:|");
        Add("Reference turn radius (m)", F(SegmentPhysics.ReferenceTurnRadiusMeters));
        Add("Reference turn speed (m/s)", F(SegmentPhysics.ReferenceTurnSpeedMetersPerSecond));
        Add("Advanced quiet correction factor", F(SegmentPhysics.AdvancedQuietCorrectionSpeedFactor));
        Add("Legacy Brake factor", F(SegmentPhysics.BrakeSpeedFactor));
        Add("Legacy RunWide factor", F(SegmentPhysics.RunWideSpeedFactor));
        Add("Advanced Brake factors", $"{F(SegmentPhysics.MinAdvancedBrakeSpeedFactor)} -> {F(SegmentPhysics.MaxAdvancedBrakeSpeedFactor)}");
        Add("Advanced RunWide factors", $"{F(SegmentPhysics.MinAdvancedRunWideSpeedFactor)} -> {F(SegmentPhysics.MaxAdvancedRunWideSpeedFactor)}");
        Add("RunWide retention", $"{F(SegmentPhysics.MinRunWideOverspeedRetention)} -> {F(SegmentPhysics.MaxRunWideOverspeedRetention)}");
        Add("TurnEntry scrub fraction", F(LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction));
        Add("Corner correction deceleration (m/s²)", $"{F(LongitudinalDynamics.MinCornerEntryDecelerationMetersPerSecondSquared)} -> {F(LongitudinalDynamics.MaxCornerEntryDecelerationMetersPerSecondSquared)}");
        Add("Standing launch reference acceleration (m/s²)", $"{F(LongitudinalDynamics.ProvisionalStandingStartMinimumReferenceAccelerationMetersPerSecondSquared)} -> {F(LongitudinalDynamics.ProvisionalStandingStartMaximumReferenceAccelerationMetersPerSecondSquared)}");
        Add("Reaction time (s)", $"{F(LongitudinalDynamics.ProvisionalStandingStartSlowReactionSeconds)} -> {F(LongitudinalDynamics.ProvisionalStandingStartFastReactionSeconds)}");
        Add("Straight reference acceleration (m/s²)", $"{F(LongitudinalDynamics.MinStraightAccelerationMetersPerSecondSquared)} -> {F(LongitudinalDynamics.MaxStraightAccelerationMetersPerSecondSquared)}");
        Add("TurnExit reference acceleration (m/s²)", $"{F(LongitudinalDynamics.MinTurnExitAccelerationMetersPerSecondSquared)} -> {F(LongitudinalDynamics.MaxTurnExitAccelerationMetersPerSecondSquared)}");
        Add("Nominal system mass (kg)", F(LongitudinalDynamics.ProvisionalNominalSystemMassKilograms));
        Add("Resistance (N)", $"{F(LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons)} + {F(LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient)} × v²");
        Add("Force fade", $"{F(LongitudinalDynamics.ProvisionalDriveOrientedForceFadePerMeterPerSecond)} -> {F(LongitudinalDynamics.ProvisionalSpeedOrientedForceFadePerMeterPerSecond)}");
        builder.AppendLine();
        return;

        void Add(string name, string value) => builder.AppendLine($"| {name} | {value} |");
    }

    private static ScenarioSnapshot Snapshot(
        CalibrationSkillSweepResult sweep,
        CalibrationTrace trace)
        => new(
            Median(sweep.Riders.Select(rider => (double)CalibrationUnits.MetersPerSecondToKph(rider.MaximumSpeedMetersPerSecond))),
            Median(sweep.Riders.Select(rider => (double)rider.AverageSpeedMetersPerSecond!.Value)),
            Median(sweep.Riders.Select(rider => (double)rider.FirstLapPenaltySeconds!.Value)),
            Median(sweep.Riders.Select(rider => (double)rider.TotalTimeSeconds)),
            Spread(sweep.Riders.Select(rider => (double)rider.TotalTimeSeconds)),
            Spread(sweep.Riders.Select(rider => (double)CalibrationUnits.MetersPerSecondToKph(rider.MaximumSpeedMetersPerSecond))),
            Spread(sweep.Riders.Select(rider => (double)rider.L1Seconds!.Value)),
            Spread(sweep.Riders.Select(rider => (double)rider.AverageSpeedMetersPerSecond!.Value)),
            trace.StepSamples.Count(sample => sample.Outcome == SegmentOutcome.Ok),
            trace.StepSamples.Count(sample => sample.Outcome == SegmentOutcome.Brake),
            trace.StepSamples.Count(sample => sample.Outcome == SegmentOutcome.RunWide),
            trace.StepSamples.Count(sample => sample.Outcome == SegmentOutcome.Crash));

    private static SweepSnapshot Sweep(
        CalibrationSkillSweepResult sweep,
        CalibrationEvaluationReport evaluation)
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
        => CalibrationDistribution.LinearQuantile(values.Order().ToArray(), 0.5d);

    private static double Spread(IEnumerable<double> values)
    {
        var materialized = values.ToArray();
        return materialized.Max() - materialized.Min();
    }

    private static void ValidateHistoricalWidthReport(string report)
    {
        var requiredFragments = new[]
        {
            "# Physical track-width impact",
            "| Vmax median (km/h) | 73.812906 | 76.440358 | 2.627452 |",
            "| Average-speed median (m/s) | 17.33039 | 18.142772 | 0.812382 |",
            "| Total-heat-time median (s) | 66.984001 | 68.129131 | 1.14513 |",
            "| `speed_100` | 79.18968 / 82.043818 |",
            "| `slide_control_100` | 75.957351 / 78.482967 |",
        };
        foreach (var fragment in requiredFragments)
        {
            if (!report.Contains(fragment, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Historical PR #33 report does not match its expected immutable baseline.");
            }
        }
    }

    private static string F(double value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string FN(double? value) => value is { } actual ? F(actual) : "—";
    private static string BN(bool? value) => value is { } actual ? actual.ToString() : "—";

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane, 0f);

        public RiderDecision Decide(RiderDecisionContext context)
            => new(context.Rider.Lane, 0f);
    }

    private sealed record ProbeSnapshot(
        SegmentType Segment,
        string Band,
        double EntrySpeed,
        double MaxSafeSpeed,
        SegmentOutcome Outcome,
        double? CorrectionTarget,
        double? AvailableCorrectionDistance,
        double? RequiredCorrectionDistance,
        double? ActualCorrectionDistance,
        bool? TargetReached,
        double? CorrectionExitSpeed,
        double? RemainingDistance,
        double DriveDistance,
        double FinalExitSpeed,
        double TravelTime);

    private sealed record ScenarioSnapshot(
        double VmaxKph,
        double AverageSpeedMetersPerSecond,
        double L1PenaltySeconds,
        double TotalHeatTimeSeconds,
        double HeatTimeSpreadSeconds,
        double VmaxSpreadKph,
        double L1SpreadSeconds,
        double AverageSpeedSpreadMetersPerSecond,
        int OkCount,
        int BrakeCount,
        int RunWideCount,
        int CrashCount);

    private sealed record SweepSnapshot(
        double VmaxKph,
        double VmaxPercentile,
        double AverageSpeedMetersPerSecond,
        double AverageSpeedPercentile,
        double L1PenaltySeconds,
        double L1PenaltyPercentile);
}
