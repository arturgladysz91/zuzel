using CoreSim.Race;

namespace CoreSim.Analysis;

/// <summary>Collects typed observations from the production Resolve result.</summary>
public sealed class CalibrationTraceCollector : ISimulationStepObserver
{
    private readonly int _heatId;
    private readonly Track _track;
    private readonly HeatSimulationOptions _options;
    private readonly List<CalibrationStepSample> _samples = new();
    private bool _completed;

    public CalibrationTraceCollector(Track track, HeatSimulationOptions options, int heatId = 0)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _heatId = heatId;
        _track = new Track(track.Segments, track.Geometry);
        _options = options with { };
    }

    public void OnStepResolved(ResolvedSimulationStep resolvedStep)
    {
        ArgumentNullException.ThrowIfNull(resolvedStep);
        if (_completed)
            throw new InvalidOperationException("A completed calibration collector cannot accept more steps.");
        if (resolvedStep.Snapshot.Step.HeatId != _heatId)
            throw new InvalidOperationException("The resolved step belongs to another heat.");
        if (resolvedStep.Snapshot.Track.Segments.Count != _track.Segments.Count)
            throw new InvalidOperationException("The resolved step belongs to another track topology.");

        var diagnosticsByRider = resolvedStep.Diagnostics.ToDictionary(item => item.RiderId);
        foreach (var change in resolvedStep.Changes.OrderBy(item => item.RiderId))
        {
            var snapshot = resolvedStep.Snapshot;
            var rider = snapshot.Rider(change.RiderId);
            var diagnostics = diagnosticsByRider[change.RiderId];
            var duration = change.ElapsedTimeSeconds - rider.ElapsedTimeSeconds;
            var distanceDelta = change.Position.DistanceMeters - rider.DistanceMeters;
            ValidateEquivalent(duration, diagnostics.TravelTimeSeconds, "travel time");
            ValidateEquivalent(distanceDelta, diagnostics.TravelledMeters, "travelled distance");

            _samples.Add(CreateSample(snapshot, rider, change, diagnostics));
        }
    }

    public CalibrationTrace Complete(HeatResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (_completed)
            throw new InvalidOperationException("The calibration collector has already been completed.");
        if (result.HeatId != _heatId)
            throw new ArgumentException("The heat result belongs to another heat.", nameof(result));

        _completed = true;
        var samples = _samples
            .OrderBy(item => item.StepNumber)
            .ThenBy(item => item.RiderId)
            .ToArray();
        var laps = BuildLapSummaries(samples);
        var riders = BuildRiderSummaries(samples, result.Classification);
        return new CalibrationTrace(
            _heatId,
            _track.Geometry,
            _options,
            samples,
            laps,
            riders,
            result.Classification);
    }

    private static CalibrationStepSample CreateSample(
        SimulationSnapshot snapshot,
        RiderSnapshot rider,
        RiderStateChange change,
        RiderStepDiagnostics diagnostics)
    {
        var straight = diagnostics.StraightProfile;
        var turnExit = diagnostics.TurnExitDriveProfile;
        var scrub = diagnostics.TurnEntryScrubProfile;
        var launch = diagnostics.StandingStartLaunchProfile;
        var correction = diagnostics.CornerSpeedCorrectionProfile;
        var surface = diagnostics.EntrySurface;
        return new CalibrationStepSample(
            snapshot.Step.HeatId,
            snapshot.Step.StepNumber,
            snapshot.Step.LapIndex,
            snapshot.Step.SegmentIndex,
            snapshot.Segment.Id,
            snapshot.Segment.Type,
            rider.RiderId,
            rider.ElapsedTimeSeconds,
            change.ElapsedTimeSeconds,
            change.ElapsedTimeSeconds - rider.ElapsedTimeSeconds,
            rider.DistanceMeters,
            change.Position.DistanceMeters,
            diagnostics.TravelledMeters,
            change.EntrySpeed,
            change.PhysicsSpeed,
            change.Speed,
            diagnostics.PeakSpeedMetersPerSecond,
            change.Outcome,
            change.Status,
            change.Position.LapsCompleted,
            change.BeforeLane,
            change.PlannedLane,
            change.TargetLane,
            change.Lane,
            rider.LateralPosition,
            change.LateralPosition,
            rider.ActiveSetup.Gearing,
            rider.ActiveSetup.TractionBias,
            rider.Profile.Skills.Start,
            rider.Profile.Skills.Speed,
            rider.Profile.Skills.SlideControl,
            rider.Profile.Skills.TrackReading,
            rider.Profile.Skills.PairRiding,
            rider.Profile.Skills.Adaptability,
            surface.Grip,
            surface.Ruts,
            surface.Moisture,
            surface.EffectiveGrip,
            diagnostics.FullDriveEquilibriumSpeedMetersPerSecond,
            diagnostics.TurnExitNetAccelerationMetersPerSecondSquared,
            turnExit?.AccelerationDistanceMeters,
            turnExit?.CruiseDistanceMeters,
            turnExit?.TravelTimeSeconds,
            straight?.AccelerationDistanceMeters,
            straight?.CruiseDistanceMeters,
            straight?.DecelerationDistanceMeters,
            straight?.TravelTimeSeconds,
            scrub?.DecelerationDistanceMeters,
            scrub?.CarryDistanceMeters,
            scrub?.TravelTimeSeconds,
            launch?.ReactionTimeSeconds,
            launch?.MovementTimeSeconds,
            launch?.TotalTimeSeconds,
            launch?.AccelerationDistanceMeters,
            launch?.CruiseDistanceMeters,
            launch?.EntryNetAccelerationMetersPerSecondSquared,
            launch?.TimeTo70KphSeconds,
            launch?.SpeedAtTwoSecondsMetersPerSecond,
            launch?.PreparationDistanceMeters,
            turnExit?.DecelerationDistanceMeters,
            correction?.EntrySpeedMetersPerSecond,
            correction?.TargetSpeedMetersPerSecond,
            correction?.ExitSpeedMetersPerSecond,
            correction?.TravelTimeSeconds,
            correction?.RequiredCorrectionDistanceMeters,
            correction?.CorrectionDistanceMeters,
            correction?.RemainingDistanceMeters,
            correction?.DecelerationMetersPerSecondSquared,
            correction?.TargetReached,
            diagnostics.CornerPhaseContext,
            diagnostics.ContinuousCornerProfile,
            diagnostics.ContinuousCornerProfile is { } corner
                ? diagnostics.PeakSpeedMetersPerSecond > corner.PeakSpeedMetersPerSecond
                    ? diagnostics.CornerPhaseContext?.CornerProgress : corner.PeakCornerProgress
                : diagnostics.CornerPhaseContext?.CornerProgress);
    }

    private CalibrationLapSummary[] BuildLapSummaries(
        IReadOnlyList<CalibrationStepSample> samples)
    {
        var summaries = new List<CalibrationLapSummary>();
        foreach (var riderGroup in samples.GroupBy(item => item.RiderId).OrderBy(item => item.Key))
        {
            var previousTime = riderGroup.Min(item => item.StartTimeSeconds);
            var previousDistance = riderGroup.Min(item => item.StartDistanceMeters);
            foreach (var lapGroup in riderGroup
                         .GroupBy(item => item.LapIndex)
                         .OrderBy(item => item.Key))
            {
                var boundary = lapGroup.SingleOrDefault(item =>
                    item.SegmentIndex == _track.Segments.Count - 1
                    && item.LapsCompleted >= item.LapIndex + 1);
                if (boundary is null)
                    continue;

                summaries.Add(new CalibrationLapSummary(
                    riderGroup.Key,
                    boundary.LapIndex + 1,
                    boundary.EndTimeSeconds - previousTime,
                    boundary.EndDistanceMeters - previousDistance,
                    lapGroup.Max(item => item.PeakSpeedMetersPerSecond)));
                previousTime = boundary.EndTimeSeconds;
                previousDistance = boundary.EndDistanceMeters;
            }
        }

        return summaries.ToArray();
    }

    private static CalibrationRiderSummary[] BuildRiderSummaries(
        IReadOnlyList<CalibrationStepSample> samples,
        IReadOnlyList<RiderHeatResult> classification)
    {
        return classification
            .OrderBy(item => item.RiderId)
            .Select(result =>
            {
                var riderSamples = samples.Where(item => item.RiderId == result.RiderId).ToArray();
                var maxSpeed = riderSamples.Length == 0
                    ? 0f
                    : riderSamples.Max(item => item.PeakSpeedMetersPerSecond);
                return new CalibrationRiderSummary(
                    result.RiderId,
                    result.Status,
                    result.LapsCompleted,
                    result.TimeSeconds,
                    result.DistanceMeters,
                    maxSpeed,
                    result.TimeSeconds > 0f ? result.DistanceMeters / result.TimeSeconds : null);
            })
            .ToArray();
    }

    private static void ValidateEquivalent(float actual, float expected, string name)
    {
        var tolerance = MathF.Max(1e-5f, MathF.Max(MathF.Abs(actual), MathF.Abs(expected)) * 1e-5f);
        if (MathF.Abs(actual - expected) > tolerance)
        {
            throw new InvalidOperationException(
                $"Resolved {name} does not match its canonical state delta.");
        }
    }
}
