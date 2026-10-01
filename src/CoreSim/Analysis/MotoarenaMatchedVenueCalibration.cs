using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record MatchedVenueDatasetCoverage(
    int MatchCount,
    IReadOnlyList<string> MatchIds,
    int RiderHeatRowCount,
    int CompleteTelemetryCount,
    int CleanPhysicsCount,
    int EventfulCount,
    int AuditOnlyCount,
    int FourRiderCleanPhysicsAttemptCount);

public sealed record MatchedVenueRiderObservation(
    CalibrationSkillRiderObservation Performance,
    string VmaxLocation,
    int VmaxSegmentId,
    SegmentType VmaxSegmentType,
    float? VmaxCornerProgress,
    float MinimumCornerSpeedMetersPerSecond,
    float MedianCornerEntrySpeedMetersPerSecond,
    float MedianCornerExitSpeedMetersPerSecond);

public sealed class MatchedVenueHeatResult
{
    private readonly ReadOnlyCollection<MatchedVenueRiderObservation> riders;

    public string ScenarioId { get; }
    public float StartLineToFirstCornerMeters { get; }
    public CalibrationTrace Trace { get; }
    public IReadOnlyList<MatchedVenueRiderObservation> Riders => riders;

    internal MatchedVenueHeatResult(string scenarioId, float startLineToFirstCornerMeters,
        CalibrationTrace trace, IEnumerable<MatchedVenueRiderObservation> observations)
    {
        ScenarioId = scenarioId;
        StartLineToFirstCornerMeters = startLineToFirstCornerMeters;
        Trace = trace;
        riders = Array.AsReadOnly(observations.OrderBy(item => item.Performance.RiderId).ToArray());
    }
}

public sealed record MatchedVenueLineResult(
    int LateralPosition,
    float LapDistanceMeters,
    MatchedVenueHeatResult Heat);

public sealed record MatchedVenueStartSplitResult(
    float StartLineToFirstCornerMeters,
    float FinishStraightMeters,
    float LapDistanceMeters,
    double L1Seconds,
    double FlyingLapMedianSeconds,
    double HeatTimeSeconds,
    double FirstCornerEntrySpeedMetersPerSecond,
    double VmaxMetersPerSecond);

public sealed record MatchedVenueWidthSensitivityResult(
    float SymmetricTurnWidthMeters,
    string Classification,
    IReadOnlyList<float> LapDistancesMeters,
    MatchedVenueHeatResult BalancedHeat,
    double VmaxKilometersPerHour,
    double AverageSpeedMetersPerSecond,
    double FlyingLapMedianSeconds,
    double HeatTimeSeconds,
    double TotalDistanceMeters);

public sealed record MatchedVenueFlyingLapPoint(
    string Point,
    float CumulativeDistanceMeters,
    SegmentType SegmentType,
    float? CornerProgress,
    float ActualSpeedMetersPerSecond,
    float? LocalEnvelopeMetersPerSecond,
    float? DriveAvailability);

public sealed class MotoarenaMatchedVenueCalibrationResult
{
    public MatchedVenueProfile Profile { get; }
    public RealWorldCalibrationDataset VenueDataset { get; }
    public MatchedVenueDatasetCoverage Coverage { get; }
    public MatchedVenueHeatResult StandingExampleBalanced { get; }
    public MatchedVenueHeatResult MotoarenaBalanced { get; }
    public IReadOnlyList<MatchedVenueStartSplitResult> StartSplitSensitivity { get; }
    public IReadOnlyList<MatchedVenueHeatResult> SpeedSweep { get; }
    public IReadOnlyList<MatchedVenueHeatResult> SlideControlSweep { get; }
    public IReadOnlyList<MatchedVenueLineResult> Lines { get; }
    public IReadOnlyList<MatchedVenueWidthSensitivityResult> WidthSensitivity { get; }
    public IReadOnlyList<string> PermutationTraceSha256 { get; }
    public IReadOnlyList<MatchedVenueFlyingLapPoint> FlyingLapProfile { get; }

    internal MotoarenaMatchedVenueCalibrationResult(
        MatchedVenueProfile profile,
        RealWorldCalibrationDataset venueDataset,
        MatchedVenueDatasetCoverage coverage,
        MatchedVenueHeatResult standingExampleBalanced,
        MatchedVenueHeatResult motoarenaBalanced,
        IEnumerable<MatchedVenueStartSplitResult> startSplitSensitivity,
        IEnumerable<MatchedVenueHeatResult> speedSweep,
        IEnumerable<MatchedVenueHeatResult> slideControlSweep,
        IEnumerable<MatchedVenueLineResult> lines,
        IEnumerable<MatchedVenueWidthSensitivityResult> widthSensitivity,
        IEnumerable<string> permutationTraceSha256,
        IEnumerable<MatchedVenueFlyingLapPoint> flyingLapProfile)
    {
        Profile = profile;
        VenueDataset = venueDataset;
        Coverage = coverage;
        StandingExampleBalanced = standingExampleBalanced;
        MotoarenaBalanced = motoarenaBalanced;
        StartSplitSensitivity = Array.AsReadOnly(startSplitSensitivity.ToArray());
        SpeedSweep = Array.AsReadOnly(speedSweep.ToArray());
        SlideControlSweep = Array.AsReadOnly(slideControlSweep.ToArray());
        Lines = Array.AsReadOnly(lines.ToArray());
        WidthSensitivity = Array.AsReadOnly(widthSensitivity.ToArray());
        PermutationTraceSha256 = Array.AsReadOnly(permutationTraceSha256.ToArray());
        FlyingLapProfile = Array.AsReadOnly(flyingLapProfile.ToArray());
    }
}

/// <summary>
/// Historical #39 matched-venue measurement orchestration, frozen at its 31/31 input.
/// Not the current canonical start fixture. Every heat runs through the
/// production CalibrationRunner -> HeatSimulator path with frozen physics.
/// </summary>
public static class MotoarenaMatchedVenueCalibration
{
    public const int FixedSeed = 390039;
    public const int HeatId = 39;

    private static readonly float[] SweepValues = { 0f, 25f, 50f, 75f, 100f };
    private static readonly float[] StartSplits = { 25f, 31f, 37f };

    public static MotoarenaMatchedVenueCalibrationResult Run(RealWorldCalibrationDataset globalDataset)
    {
        ArgumentNullException.ThrowIfNull(globalDataset);
        var profile = MatchedVenueProfiles.MotoarenaHistorical39;
        var venueDataset = globalDataset.FilterByExactVenue(profile.Season, profile.SourceTrackLabel);
        var coverage = Coverage(venueDataset);
        var primaryTrack = profile.CreateTrack(MatchedVenueProfiles.MotoarenaHistorical39StartLineToFirstCornerMeters);
        var balancedSpecs = BalancedRiders();
        var primary = RunHeat("motoarena/balanced", primaryTrack, balancedSpecs);
        var standing = RunHeat("standing-example/balanced", Track.CreateStandingStartExample(), balancedSpecs);

        var splits = StartSplits.Select(split =>
        {
            var track = profile.CreateTrack(split);
            var heat = RunHeat($"motoarena/start-split/{Id(split)}", track, balancedSpecs);
            return new MatchedVenueStartSplitResult(
                split,
                profile.StraightLengthMeters - split,
                LapDistance(track, 1f),
                Median(heat.Riders.Select(item => item.Performance.L1Seconds!.Value)),
                Median(heat.Riders.Select(item => item.Performance.FlyingLapMedianSeconds!.Value)),
                Median(heat.Riders.Select(item => item.Performance.TotalTimeSeconds)),
                Median(heat.Riders.Select(item => item.Performance.FirstCurveEntrySpeedMetersPerSecond!.Value)),
                Median(heat.Riders.Select(item => item.Performance.MaximumSpeedMetersPerSecond)));
        }).ToArray();

        var speed = SweepValues.Select(value => RunHeat(
            $"motoarena/speed/{Id(value)}", primaryTrack, Riders(Skills(speed: value)))).ToArray();
        var slide = SweepValues.Select(value => RunHeat(
            $"motoarena/slide-control/{Id(value)}", primaryTrack, Riders(Skills(slideControl: value)))).ToArray();
        var lines = Enumerable.Range(0, LaneModel.LanesCount).Select(line =>
        {
            var heat = RunHeat($"motoarena/line/{line}", primaryTrack,
                new[] { new RiderFixture(1, line, RiderSkills.Balanced) });
            return new MatchedVenueLineResult(line, LapDistance(primaryTrack, line), heat);
        }).ToArray();

        var widths = new[]
        {
            profile.PublishedSecondBendWidthMeters,
            profile.ModeledSymmetricTurnWidthMeters,
            profile.PublishedFirstBendWidthMeters,
            profile.OlderArticleBendWidthMeters,
        }.Select(width =>
        {
            var track = profile.CreateTrack(MatchedVenueProfiles.MotoarenaHistorical39StartLineToFirstCornerMeters, width);
            var heat = width == profile.ModeledSymmetricTurnWidthMeters
                ? primary
                : RunHeat($"motoarena/turn-width/{width.ToString("0.0", CultureInfo.InvariantCulture)}", track, balancedSpecs);
            var classification = width == profile.ModeledSymmetricTurnWidthMeters
                ? profile.ModeledTurnWidthClassification + " / PRIMARY"
                : width == profile.OlderArticleBendWidthMeters
                    ? profile.OlderArticleBendWidthClassification
                    : "CurrentPublishedBendWidthSensitivity";
            return new MatchedVenueWidthSensitivityResult(
                width,
                classification,
                Array.AsReadOnly(Enumerable.Range(0, LaneModel.LanesCount)
                    .Select(line => LapDistance(track, line)).ToArray()),
                heat,
                Median(heat.Riders.Select(item =>
                    CalibrationUnits.MetersPerSecondToKph(item.Performance.MaximumSpeedMetersPerSecond))),
                Median(heat.Riders.Select(item => item.Performance.AverageSpeedMetersPerSecond!.Value)),
                Median(heat.Riders.Select(item => item.Performance.FlyingLapMedianSeconds!.Value)),
                Median(heat.Riders.Select(item => item.Performance.TotalTimeSeconds)),
                Median(heat.Riders.Select(item => item.Performance.TotalDistanceMeters)));
        }).ToArray();

        var permutationHashes = Permutations(new[] { 1, 2, 3, 4 })
            .Select(order => RunHeat("motoarena/balanced/permutation", primaryTrack, balancedSpecs, order))
            .Select(TraceHash)
            .ToArray();

        return new MotoarenaMatchedVenueCalibrationResult(
            profile,
            venueDataset,
            coverage,
            standing,
            primary,
            splits,
            speed,
            slide,
            lines,
            widths,
            permutationHashes,
            BuildFlyingLapProfile(primaryTrack, primary.Trace, riderId: 2, lapIndex: 1));
    }

    public static float LapDistance(Track track, float lateralPosition)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (!float.IsFinite(lateralPosition) || lateralPosition < 0f || lateralPosition > LaneModel.MaxLane)
            throw new ArgumentOutOfRangeException(nameof(lateralPosition));
        return track.Segments.Sum(segment => LaneModel.SegmentLengthMeters(segment, lateralPosition, track.Geometry));
    }

    private static MatchedVenueDatasetCoverage Coverage(RealWorldCalibrationDataset dataset)
    {
        var matchIds = dataset.Rows.Select(row => row.MatchId).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        var attempts = dataset.Rows.Where(row => row.CleanPhysics)
            .GroupBy(row => (row.MatchId, row.HeatUid))
            .Count(group => group.Count() == 4 && group.Select(row => row.RiderId).Distinct(StringComparer.Ordinal).Count() == 4);
        return new(
            matchIds.Length,
            Array.AsReadOnly(matchIds),
            dataset.Rows.Count,
            dataset.Rows.Count(row => row.CompleteTelemetry),
            dataset.Rows.Count(row => row.CleanPhysics),
            dataset.Rows.Count(row => row.Eventful),
            dataset.Rows.Count(row => row.AuditOnly),
            attempts);
    }

    private static MatchedVenueHeatResult RunHeat(
        string scenarioId,
        Track track,
        IReadOnlyList<RiderFixture> fixtures,
        IEnumerable<int>? riderOrder = null)
    {
        var order = (riderOrder ?? fixtures.Select(item => item.RiderId)).ToArray();
        if (!order.Order().SequenceEqual(fixtures.Select(item => item.RiderId).Order()))
            throw new ArgumentException("Rider order must contain every fixture rider exactly once.", nameof(riderOrder));
        var riders = order.Select(id => fixtures.Single(item => item.RiderId == id)).Select(item =>
            new RiderState(
                new RiderProfile(item.RiderId, $"Matched venue rider {item.RiderId}", item.Skills, RiderStyle.Balanced),
                item.Lane)
            {
                ActiveSetup = BikeSetup.Neutral,
            }).ToList();
        var options = new HeatSimulationOptions
        {
            Laps = 4,
            Seed = FixedSeed,
            Weather = WeatherState.Dry,
            IncidentFrequency = 0f,
            EnableLogging = false,
        };
        var trace = CalibrationRunner.RunHeat(
            track,
            TrackState.CreateDefault(track, CalibrationScenarioCatalog.Baseline.Surface),
            riders,
            new HoldLaneDecisionModel(),
            options,
            HeatId);
        return new MatchedVenueHeatResult(scenarioId,
            track.Segments[0].StraightLengthMetersOverride ?? track.Geometry.StraightLengthMeters,
            trace,
            ObserveRiders(trace));
    }

    private static IReadOnlyList<MatchedVenueRiderObservation> ObserveRiders(CalibrationTrace trace)
        => Array.AsReadOnly(CalibrationSkillSweep.ObserveRiders(trace).Select(performance =>
        {
            var samples = trace.StepSamples.Where(sample => sample.RiderId == performance.RiderId).ToArray();
            var peak = samples.OrderByDescending(sample => sample.PeakSpeedMetersPerSecond)
                .ThenBy(sample => sample.StepNumber).First();
            var cornerSamples = samples.Where(sample => sample.ContinuousCornerProfile is not null).ToArray();
            var minimum = cornerSamples.SelectMany(sample => sample.ContinuousCornerProfile!.Nodes)
                .Min(node => node.SpeedMetersPerSecond);
            return new MatchedVenueRiderObservation(
                performance,
                peak.SegmentType == SegmentType.Straight ? "Straight" : "Corner",
                peak.SegmentId,
                peak.SegmentType,
                peak.SegmentType == SegmentType.Straight ? null : peak.PeakCornerProgress,
                minimum,
                (float)Median(samples.Where(sample => sample.SegmentType == SegmentType.TurnEntry)
                    .Select(sample => sample.EntrySpeedMetersPerSecond)),
                (float)Median(samples.Where(sample => sample.SegmentType == SegmentType.TurnExit)
                    .Select(sample => sample.ExitSpeedMetersPerSecond)));
        }).OrderBy(item => item.Performance.RiderId).ToArray());

    private static IReadOnlyList<MatchedVenueFlyingLapPoint> BuildFlyingLapProfile(
        Track track,
        CalibrationTrace trace,
        int riderId,
        int lapIndex)
    {
        var samples = trace.StepSamples.Where(sample => sample.RiderId == riderId && sample.LapIndex == lapIndex)
            .OrderBy(sample => sample.SegmentIndex).ToArray();
        if (samples.Length != track.Segments.Count)
            throw new InvalidOperationException("The selected flying lap is incomplete.");
        var points = new List<MatchedVenueFlyingLapPoint>();
        var cumulative = 0f;
        foreach (var sample in samples)
        {
            if (sample.SegmentType == SegmentType.Straight)
            {
                var prefix = sample.SegmentIndex switch
                {
                    0 => "home-start",
                    4 => "back-straight",
                    _ => "home-finish",
                };
                foreach (var fraction in new[] { 0f, .25f, .5f, .75f, 1f })
                {
                    points.Add(new MatchedVenueFlyingLapPoint(
                        $"{prefix}/{Id(fraction * 100)}%",
                        cumulative + sample.TravelledMeters * fraction,
                        SegmentType.Straight,
                        null,
                        StraightSpeedAt(sample, fraction),
                        null,
                        null));
                }
                cumulative += sample.TravelledMeters;
                continue;
            }

            if (sample.SegmentType != SegmentType.TurnEntry)
                continue;
            var cornerId = sample.CornerPhase!.Value.CornerId;
            var cornerSamples = samples.Where(item => item.CornerPhase?.CornerId == cornerId).ToArray();
            var totalLength = cornerSamples[0].CornerPhase!.Value.TotalCornerLengthMeters;
            var nodes = cornerSamples.SelectMany(item => item.ContinuousCornerProfile!.Nodes)
                .OrderBy(node => node.CornerProgress).ToArray();
            foreach (var progress in new[] { 0f, .125f, .25f, .375f, .5f, .625f, .75f, .875f, 1f })
            {
                var node = Interpolate(nodes, progress);
                var segmentType = cornerSamples.First(item => progress <= item.CornerPhase!.Value.SegmentEndCornerProgress + 1e-6f)
                    .SegmentType;
                points.Add(new MatchedVenueFlyingLapPoint(
                    $"corner-{cornerId}/{progress.ToString("0.000", CultureInfo.InvariantCulture)}",
                    cumulative + progress * totalLength,
                    segmentType,
                    progress,
                    node.SpeedMetersPerSecond,
                    node.EnvelopeSpeedMetersPerSecond,
                    node.DriveAvailability));
            }
            cumulative += totalLength;
        }
        return Array.AsReadOnly(points.OrderBy(item => item.CumulativeDistanceMeters).ThenBy(item => item.Point, StringComparer.Ordinal).ToArray());
    }

    private static ContinuousCornerNode Interpolate(IReadOnlyList<ContinuousCornerNode> nodes, float progress)
    {
        var upper = 0;
        while (upper < nodes.Count && nodes[upper].CornerProgress < progress) upper++;
        if (upper == 0) return nodes[0] with { CornerProgress = progress };
        if (upper == nodes.Count) return nodes[^1] with { CornerProgress = progress };
        var a = nodes[upper - 1];
        var b = nodes[upper];
        if (MathF.Abs(b.CornerProgress - a.CornerProgress) <= 1e-7f)
            return b with { CornerProgress = progress };
        var t = (progress - a.CornerProgress) / (b.CornerProgress - a.CornerProgress);
        return new ContinuousCornerNode(
            progress,
            Lerp(a.SpeedMetersPerSecond, b.SpeedMetersPerSecond, t),
            Lerp(a.EnvelopeSpeedMetersPerSecond, b.EnvelopeSpeedMetersPerSecond, t),
            ContinuousCornerEnvelope.DriveAvailability(progress),
            Lerp(a.NetDriveAccelerationMetersPerSecondSquared, b.NetDriveAccelerationMetersPerSecondSquared, t));
    }

    private static float StraightSpeedAt(CalibrationStepSample sample, float fraction)
    {
        if (fraction <= 0f) return sample.EntrySpeedMetersPerSecond;
        if (fraction >= 1f) return sample.ExitSpeedMetersPerSecond;
        var distance = sample.TravelledMeters * fraction;
        var acceleration = sample.StraightAccelerationDistanceMeters ?? sample.StandingStartAccelerationDistanceMeters ?? 0f;
        var cruise = sample.StraightCruiseDistanceMeters ?? sample.StandingStartCruiseDistanceMeters ?? 0f;
        var deceleration = sample.StraightDecelerationDistanceMeters ?? sample.StandingStartPreparationDistanceMeters ?? 0f;
        if (distance <= acceleration && acceleration > 0f)
            return EnergyLerp(sample.EntrySpeedMetersPerSecond, sample.PeakSpeedMetersPerSecond, distance / acceleration);
        if (distance <= acceleration + cruise || deceleration <= 0f)
            return sample.PeakSpeedMetersPerSecond;
        return EnergyLerp(sample.PeakSpeedMetersPerSecond, sample.ExitSpeedMetersPerSecond,
            Math.Clamp((distance - acceleration - cruise) / deceleration, 0f, 1f));
    }

    private static string TraceHash(MatchedVenueHeatResult heat)
    {
        var payload = CalibrationCsvExporter.ExportSteps(heat.Trace) + "\n"
            + string.Join("\n", heat.Trace.RiderSummaries.Select(item => string.Join(",",
                item.RiderId,
                item.Status,
                item.LapsCompleted,
                item.TotalTimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                item.TotalDistanceMeters.ToString("R", CultureInfo.InvariantCulture),
                item.MaxSpeedMetersPerSecond.ToString("R", CultureInfo.InvariantCulture))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static IEnumerable<int[]> Permutations(int[] values)
    {
        var copy = values.ToArray();
        return Generate(0);

        IEnumerable<int[]> Generate(int index)
        {
            if (index == copy.Length)
            {
                yield return copy.ToArray();
                yield break;
            }
            for (var swap = index; swap < copy.Length; swap++)
            {
                (copy[index], copy[swap]) = (copy[swap], copy[index]);
                foreach (var permutation in Generate(index + 1)) yield return permutation;
                (copy[index], copy[swap]) = (copy[swap], copy[index]);
            }
        }
    }

    private static IReadOnlyList<RiderFixture> BalancedRiders() => Riders(RiderSkills.Balanced);
    private static IReadOnlyList<RiderFixture> Riders(RiderSkills skills)
        => Array.AsReadOnly(Enumerable.Range(1, 4).Select(id => new RiderFixture(id, id - 1, skills)).ToArray());
    private static RiderSkills Skills(float speed = 50f, float slideControl = 50f)
        => new(50f, speed, slideControl, 50f, 50f, 50f);
    private static double Median(IEnumerable<float> values)
        => CalibrationDistribution.LinearQuantile(values.Select(value => (double)value).Order().ToArray(), .5d);
    private static double Median(IEnumerable<double> values)
        => CalibrationDistribution.LinearQuantile(values.Order().ToArray(), .5d);
    private static float EnergyLerp(float from, float to, float t)
        => MathF.Sqrt(MathF.Max(0f, from * from + (to * to - from * from) * t));
    private static float Lerp(float from, float to, float t) => from + (to - from) * t;
    private static string Id(float value) => value.ToString("000", CultureInfo.InvariantCulture);

    private sealed record RiderFixture(int RiderId, int Lane, RiderSkills Skills);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane, 0f);
        public RiderDecision Decide(RiderDecisionContext context) => new(context.Rider.Lane, 0f);
    }
}
