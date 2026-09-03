using CoreSim.Decisions;
using CoreSim.Logging;

namespace CoreSim.Race;

/// <summary>Orchestrates a heat; SimulationEngine executes each atomic segment step.</summary>
public sealed class HeatSimulator
{
    private readonly SimulationEngine _engine;

    public HeatSimulator(IRiderDecisionModel decision)
        => _engine = new SimulationEngine(decision);

    /// <summary>
    /// Backward-compatible single pass using the original neutral-surface
    /// physics contract, now with simultaneous per-segment commits.
    /// </summary>
    public SimLog Simulate(Track track, TrackState trackState, List<RiderState> riders, int heatId = 0)
    {
        ValidateInputs(track, trackState, riders);
        var log = new SimLog();
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 0,
            Weather = WeatherState.Dry,
            IncidentFrequency = 0f,
        };

        for (var segmentIndex = 0; segmentIndex < track.Segments.Count; segmentIndex++)
        {
            var step = new SimulationStepContext(
                heatId,
                StepNumber: segmentIndex,
                LapIndex: 0,
                SegmentIndex: segmentIndex,
                Seed: options.Seed,
                RequiredLaps: 1,
                UseLegacyPhysics: true);
            var snapshot = _engine.CaptureSnapshot(track, trackState, riders, step);
            var intents = _engine.Decide(snapshot);
            var resolved = _engine.Resolve(snapshot, intents, options);
            _engine.Commit(resolved, riders, trackState, log);
        }

        return log;
    }

    public HeatResult SimulateHeat(
        Track track,
        TrackState trackState,
        List<RiderState> riders,
        HeatSimulationOptions? options = null,
        int heatId = 0,
        ISimulationStepObserver? observer = null)
    {
        ValidateInputs(track, trackState, riders);
        options ??= new HeatSimulationOptions();
        options.Validate();

        var log = new SimLog(options.EnableLogging);
        var progress = options.EnableLogging ? new RaceProgressTracker() : null;
        progress?.InitializeStartingGrid(riders);
        var stepNumber = 0;

        for (var lapIndex = 0; lapIndex < options.Laps; lapIndex++)
        {
            for (var segmentIndex = 0; segmentIndex < track.Segments.Count; segmentIndex++, stepNumber++)
            {
                TrackEvolution.ApplyWeather(
                    track,
                    trackState,
                    options.Weather,
                    heatId,
                    stepNumber,
                    log);

                var step = new SimulationStepContext(
                    heatId,
                    stepNumber,
                    lapIndex,
                    segmentIndex,
                    options.Seed,
                    options.Laps);
                var snapshot = _engine.CaptureSnapshot(track, trackState, riders, step);
                var intents = _engine.Decide(snapshot);
                var resolved = _engine.Resolve(snapshot, intents, options);
                observer?.OnStepResolved(resolved);
                _engine.Commit(resolved, riders, trackState, log);

                var lapComplete = segmentIndex == track.Segments.Count - 1;
                progress?.CaptureSegment(
                    lapIndex + 1,
                    track.Segments[segmentIndex].Id,
                    lapComplete,
                    riders,
                    log);
            }
        }

        var classification = RaceClassification.Build(riders, options.Laps);
        ApplyMoraleConsequences(riders, classification);
        return new HeatResult(heatId, classification, log);
    }

    private static void ApplyMoraleConsequences(
        IReadOnlyList<RiderState> riders,
        IReadOnlyList<RiderHeatResult> classification)
    {
        foreach (var result in classification)
        {
            var rider = riders.Single(item => item.RiderId == result.RiderId);
            var delta = result.Crashed
                ? -0.04f
                : result.Position switch
                {
                    1 => 0.04f,
                    2 => 0.015f,
                    3 => -0.01f,
                    _ => -0.025f,
                };
            rider.ApplyMoraleDelta(delta);
        }
    }

    private static void ValidateInputs(Track track, TrackState trackState, List<RiderState> riders)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(trackState);
        ArgumentNullException.ThrowIfNull(riders);
        if (trackState.SegmentCount != track.Segments.Count || trackState.LinesCount != LaneModel.LanesCount)
            throw new ArgumentException("Track state dimensions must match the track.", nameof(trackState));
        if (riders.Count == 0)
            throw new ArgumentException("A heat must contain at least one rider.", nameof(riders));
        if (riders.Select(rider => rider.RiderId).Distinct().Count() != riders.Count)
            throw new ArgumentException("Every rider in a heat must have a unique id.", nameof(riders));
    }
}

public static class RaceClassification
{
    public static IReadOnlyList<RiderHeatResult> Build(
        IReadOnlyList<RiderState> riders,
        int requiredLaps)
    {
        ArgumentNullException.ThrowIfNull(riders);
        if (requiredLaps <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredLaps));

        var ordered = riders
            .OrderByDescending(rider =>
                rider.Status == RiderRaceStatus.Finished
                && rider.LapsCompleted >= requiredLaps)
            .ThenByDescending(rider => rider.CanonicalProgress)
            .ThenBy(rider => rider.IsCrashed ? float.MaxValue : rider.ElapsedTimeSeconds)
            .ThenBy(rider => rider.RiderId)
            .ToArray();

        return ordered
            .Select((rider, index) =>
            {
                var position = index + 1;
                return new RiderHeatResult(
                    rider.RiderId,
                    position,
                    position switch { 1 => 3, 2 => 2, 3 => 1, _ => 0 },
                    rider.Status,
                    rider.ElapsedTimeSeconds,
                    rider.DistanceMeters,
                    rider.LapsCompleted);
            })
            .ToArray();
    }
}
