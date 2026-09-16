using System.Collections.ObjectModel;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record RealStartSourceCoverage(
    int GlobalMatchCount,
    int GlobalAttemptCount,
    int GlobalRiderObservationCount,
    int MotoarenaMatchCount,
    int MotoarenaAttemptCount,
    int MotoarenaRiderObservationCount);

public sealed class RealStartSplitDiagnostic
{
    public float StartLineToFirstCornerMeters { get; }
    public double ReactionTimeSeconds { get; }
    public double? TimeTo70KphSeconds { get; }
    public double? SpeedAtTwoSecondsMetersPerSecond { get; }
    public double FirstCornerEntrySpeedMetersPerSecond { get; }
    public double PeakSpeedBeforeFirstCornerMetersPerSecond { get; }
    public double TimeToFirstCornerSeconds { get; }
    public double? DistanceAtTwoSecondsMeters { get; }
    public double PreparationDistanceMeters { get; }
    public CalibrationTrace Trace { get; }

    internal RealStartSplitDiagnostic(
        float startLineToFirstCornerMeters,
        double reactionTimeSeconds,
        double? timeTo70KphSeconds,
        double? speedAtTwoSecondsMetersPerSecond,
        double firstCornerEntrySpeedMetersPerSecond,
        double peakSpeedBeforeFirstCornerMetersPerSecond,
        double timeToFirstCornerSeconds,
        double? distanceAtTwoSecondsMeters,
        double preparationDistanceMeters,
        CalibrationTrace trace)
    {
        StartLineToFirstCornerMeters = startLineToFirstCornerMeters;
        ReactionTimeSeconds = reactionTimeSeconds;
        TimeTo70KphSeconds = timeTo70KphSeconds;
        SpeedAtTwoSecondsMetersPerSecond = speedAtTwoSecondsMetersPerSecond;
        FirstCornerEntrySpeedMetersPerSecond = firstCornerEntrySpeedMetersPerSecond;
        PeakSpeedBeforeFirstCornerMetersPerSecond = peakSpeedBeforeFirstCornerMetersPerSecond;
        TimeToFirstCornerSeconds = timeToFirstCornerSeconds;
        DistanceAtTwoSecondsMeters = distanceAtTwoSecondsMeters;
        PreparationDistanceMeters = preparationDistanceMeters;
        Trace = trace;
    }
}

public sealed class RealStartTelemetryCalibrationResult
{
    private readonly ReadOnlyCollection<RealStartSplitDiagnostic> splits;

    public MatchedVenueProfile Profile { get; }
    public RealStartSourceCoverage Coverage { get; }
    public IReadOnlyList<RealStartSplitDiagnostic> Splits => splits;

    public double ReactionTimeSplitSpanSeconds =>
        splits.Max(item => item.ReactionTimeSeconds) - splits.Min(item => item.ReactionTimeSeconds);

    public double SpeedAtTwoSecondsSplitSpanMetersPerSecond
    {
        get
        {
            var values = splits.Where(item => item.SpeedAtTwoSecondsMetersPerSecond.HasValue)
                .Select(item => item.SpeedAtTwoSecondsMetersPerSecond!.Value).ToArray();
            return values.Length == 0 ? double.NaN : values.Max() - values.Min();
        }
    }

    internal RealStartTelemetryCalibrationResult(
        MatchedVenueProfile profile,
        RealStartSourceCoverage coverage,
        IEnumerable<RealStartSplitDiagnostic> splitDiagnostics)
    {
        Profile = profile;
        Coverage = coverage;
        splits = Array.AsReadOnly(splitDiagnostics.OrderBy(item => item.StartLineToFirstCornerMeters).ToArray());
    }
}

/// <summary>
/// Read-only #40 diagnostics.  All motion comes from CalibrationRunner ->
/// HeatSimulator; this class contains no launch or corner physics model.
/// </summary>
public static class RealStartTelemetryCalibration
{
    public const int FixedSeed = MotoarenaMatchedVenueCalibration.FixedSeed;
    public const int HeatId = 40;

    private static readonly float[] StartSplits = { 25f, 31f, 37f };

    public static RealStartTelemetryCalibrationResult Run(RealWorldCalibrationDataset globalDataset)
    {
        ArgumentNullException.ThrowIfNull(globalDataset);
        var profile = MatchedVenueProfiles.Motoarena2026;
        var venue = globalDataset.FilterByExactVenue(profile.Season, profile.SourceTrackLabel);
        var coverage = new RealStartSourceCoverage(
            globalDataset.Rows.Select(item => item.MatchId).Distinct(StringComparer.Ordinal).Count(),
            globalDataset.Rows.Select(item => (item.MatchId, item.HeatUid)).Distinct().Count(),
            globalDataset.Rows.Count,
            venue.Rows.Select(item => item.MatchId).Distinct(StringComparer.Ordinal).Count(),
            venue.Rows.Select(item => (item.MatchId, item.HeatUid)).Distinct().Count(),
            venue.Rows.Count);

        var splits = StartSplits.Select(split => Observe(split, RunHeat(profile.CreateTrack(split)))).ToArray();
        return new RealStartTelemetryCalibrationResult(profile, coverage, splits);
    }

    private static RealStartSplitDiagnostic Observe(float split, CalibrationTrace trace)
    {
        var launches = trace.StepSamples
            .Where(item => item.LapIndex == 0 && item.SegmentIndex == 0)
            .OrderBy(item => item.RiderId)
            .ToArray();
        if (launches.Length != 4 || launches.Any(item => item.StandingStartReactionTimeSeconds is null))
            throw new InvalidOperationException("The controlled fixture did not produce four standing-start observations.");
        var firstCorners = trace.StepSamples
            .Where(item => item.LapIndex == 0 && item.SegmentType == SegmentType.TurnEntry)
            .GroupBy(item => item.RiderId)
            .Select(group => group.OrderBy(item => item.SegmentIndex).First())
            .OrderBy(item => item.RiderId)
            .ToArray();
        if (firstCorners.Length != 4)
            throw new InvalidOperationException("The controlled fixture did not reach the first corner.");

        return new RealStartSplitDiagnostic(
            split,
            Median(launches.Select(item => item.StandingStartReactionTimeSeconds!.Value)),
            MedianNullable(launches.Select(item => item.StandingStartTimeTo70KphSeconds)),
            MedianNullable(launches.Select(item => item.StandingStartSpeedAtTwoSecondsMetersPerSecond)),
            Median(firstCorners.Select(item => item.EntrySpeedMetersPerSecond)),
            Median(launches.Select(item => item.PeakSpeedMetersPerSecond)),
            Median(launches.Select(item => item.StandingStartProfileTotalTimeSeconds
                ?? throw new InvalidOperationException("Standing-start total time was not observed."))),
            null,
            Median(launches.Select(item => item.StandingStartPreparationDistanceMeters ?? 0f)),
            trace);
    }

    private static CalibrationTrace RunHeat(Track track)
    {
        var riders = Enumerable.Range(1, 4).Select(riderId =>
            new RiderState(
                new RiderProfile(riderId, $"Real-start diagnostic rider {riderId}", RiderSkills.Balanced, RiderStyle.Balanced),
                riderId - 1)
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
        return CalibrationRunner.RunHeat(
            track,
            TrackState.CreateDefault(track, CalibrationScenarioCatalog.Baseline.Surface),
            riders,
            new HoldLaneDecisionModel(),
            options,
            HeatId);
    }

    private static double Median(IEnumerable<float> values)
    {
        var ordered = values.Select(item => (double)item).Order().ToArray();
        if (ordered.Length == 0)
            throw new InvalidOperationException("Median requires at least one observation.");
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2d : ordered[middle];
    }

    private static double? MedianNullable(IEnumerable<float?> values)
    {
        var materialized = values.ToArray();
        return materialized.Any(item => item is null)
            ? null
            : Median(materialized.Select(item => item!.Value));
    }

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane, 0f);
        public RiderDecision Decide(RiderDecisionContext context) => new(context.Rider.Lane, 0f);
    }
}
