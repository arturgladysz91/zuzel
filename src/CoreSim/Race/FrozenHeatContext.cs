using CoreSim.Logging;

namespace CoreSim.Race;

/// <summary>Explicit experimental heat lifetime. No ordinary HeatSimulator caller.</summary>
internal sealed class FrozenHeatContext
{
    private sealed record Passage(SimulationSnapshot Origin, int RiderId, IReadOnlyList<ExecutedPathStep> Steps,
        double DistanceMeters, bool Complete);
    private readonly Dictionary<(int Step, int Rider), Passage> _passages = new();
    private sealed record CausalJournal(Dictionary<(int Step, int Rider), Passage> Passages, CausalPublication Publication);
    private CausalJournal? _causalJournal;
    private bool _causalOwned;
    private Dictionary<(int Step, int Rider), Passage> CurrentPassages => Volatile.Read(ref _causalJournal)?.Passages ?? _passages;
    internal CausalPublication? CausalPublication => Volatile.Read(ref _causalJournal)?.Publication;
    private bool _settled;
    internal Track Track { get; }
    internal TrackStateSnapshot Surface { get; }
    internal HeatSimulationOptions Options { get; }
    internal int HeatId { get; }
    internal int PassageCount => CurrentPassages.Count;
    internal int AcceptedNumericalSteps => CurrentPassages.Values.Sum(p => p.Steps.Count);
    internal double AcceptedDistanceMeters => CurrentPassages.OrderBy(p => p.Key.Step).ThenBy(p => p.Key.Rider).Sum(p => p.Value.DistanceMeters);
    internal int WeatherUpdates { get; private set; }

    internal FrozenHeatContext(Track track, TrackState surface, HeatSimulationOptions options, int heatId)
    {
        options.Validate();
        if (options.Laps <= 0 || surface.SegmentCount != track.Segments.Count || surface.LinesCount != LaneModel.LanesCount)
            throw new ArgumentException("Heat surface dimensions must match the track.");
        Track = track; Surface = surface.Snapshot(); Options = options; HeatId = heatId;
    }

    internal SimulationSnapshot Capture(IReadOnlyList<RiderState> riders, SimulationStepContext step)
    {
        RequireOpen();
        if (step.HeatId != HeatId || step.Seed != Options.Seed || step.RequiredLaps != Options.Laps || step.UseLegacyPhysics)
            throw new ArgumentException("Frozen heat capture must preserve its original addressed context.");
        if (riders.Count == 0 || riders.Select(r => r.RiderId).Distinct().Count() != riders.Count)
            throw new ArgumentException("Frozen capture requires distinct riders.");
        return new(step with { FreezeSurfaceForHeat = true }, Track, Surface, SimulationEngine.CaptureRiders(Track, riders));
    }

    internal void AcceptComplete(ResolvedSimulationStep step, IReadOnlyList<RiderState> riders, SimLog log,
        double coveredThroughSeconds, double? firstUnresolvedContactSeconds)
    {
        RequireOrdinaryJournal();
        RequireOrigin(step.Snapshot);
        CommonTimeCommitGuard.Require(step.Changes, coveredThroughSeconds, firstUnresolvedContactSeconds);
        if (!riders.Select(r => r.RiderId).Order().SequenceEqual(step.Snapshot.Riders.Select(r => r.RiderId)))
            throw new InvalidOperationException("Accepted riders must match the captured request.");
        if (!SimulationEngine.CaptureRiders(Track, riders).OrderBy(r => r.RiderId).SequenceEqual(step.Snapshot.Riders))
            throw new InvalidOperationException("Accepted step no longer owns the captured rider state.");
        var passages = step.Changes.Select(change =>
        {
            var diagnostic = step.Diagnostics.Single(d => d.RiderId == change.RiderId);
            return new Passage(step.Snapshot, change.RiderId, diagnostic.ExecutedPath?.Steps ?? Array.Empty<ExecutedPathStep>(),
                diagnostic.ExecutedPath?.Steps.Sum(s => (double)s.DistanceMeters) ?? diagnostic.TravelledMeters, true);
        }).ToArray();
        foreach (var passage in passages) RequirePrefix(passage);
        foreach (var passage in passages) _passages[(passage.Origin.Step.StepNumber, passage.RiderId)] = passage;
        foreach (var change in step.Changes) SimulationEngine.CommitRiderChange(change, riders.Single(r => r.RiderId == change.RiderId));
        foreach (var item in step.Events) log.Add(item.Text);
        step.CommitInteractionState?.Invoke();
    }

    internal void AcceptPrefix(RiderIntegrationCursor cursor, double coveredThroughSeconds, double? firstUnresolvedContactSeconds)
    {
        RequireOrdinaryJournal();
        RequireOrigin(cursor.Snapshot);
        var end = cursor.Snapshot.Riders[0].ElapsedTimeSeconds + cursor.Boundary.TimeSeconds;
        CommonTimeCommitGuard.RequireExecutedTime(end, coveredThroughSeconds, firstUnresolvedContactSeconds);
        var passage = new Passage(cursor.Snapshot, cursor.Snapshot.Riders[0].RiderId, cursor.Boundary.Steps,
            cursor.Boundary.DistanceMeters, false);
        RequirePrefix(passage);
        _passages[(passage.Origin.Step.StepNumber, passage.RiderId)] = passage;
    }

    internal void BeginCausalOwnership()
    {
        RequireOpen();
        if (_causalOwned || _passages.Count != 0)
            throw new InvalidOperationException("Causal heat ownership requires an unused journal.");
        _causalOwned = true;
    }

    // Validate the entire journal before the coordinator's single publication.
    // This copies a small passage index, never riders, surface or physics state.
    internal Action<CausalPublication> PrepareCausalPrefixes(IReadOnlyList<RiderIntegrationCursor> cursors, double coveredThroughSeconds,
        bool resolvedContact)
    {
        if (!_causalOwned || cursors.Count is < 1 or > 4 || cursors.Select(c => c.Snapshot.Riders[0].RiderId).Distinct().Count() != cursors.Count
            || !double.IsFinite(coveredThroughSeconds) || coveredThroughSeconds < 0)
            throw new InvalidOperationException("Causal admission requires distinct actual riders and a finite certified frontier.");
        var previous = Volatile.Read(ref _causalJournal);
        var passages = previous?.Passages ?? _passages;
        var pending = new Dictionary<(int Step, int Rider), Passage>(passages);
        foreach (var cursor in cursors)
        {
            RequireOrigin(cursor.Snapshot);
            if (!cursor.Snapshot.Step.FreezeSurfaceForHeat || !cursor.Snapshot.Step.ExecutionOriginTimeSeconds.HasValue
                || cursor.ExecutedThroughSeconds > coveredThroughSeconds
                    + (resolvedContact ? TwoRiderContactExecution.MaximumEventTimeToleranceSeconds : 0))
                throw new InvalidOperationException("Causal passage exceeds the certified frontier.");
            var passage = new Passage(cursor.Snapshot, cursor.Snapshot.Riders[0].RiderId, cursor.Boundary.Steps,
                cursor.Boundary.DistanceMeters, false);
            var key = (passage.Origin.Step.StepNumber, passage.RiderId);
            if (passages.TryGetValue(key, out var old) && ReferenceEquals(old.Steps, passage.Steps)) continue;
            RequirePrefix(passage);
            pending[key] = passage;
        }
        return publication =>
        {
            var next = new CausalJournal(pending, publication);
            if (!ReferenceEquals(Interlocked.CompareExchange(ref _causalJournal, next, previous), previous))
                throw new InvalidOperationException("Stale causal ledger transaction.");
        };
    }

    private void RequirePrefix(Passage next)
    {
        var rider = next.Origin.Rider(next.RiderId);
        if (rider.SegmentIndex != next.Origin.Step.SegmentIndex || !double.IsFinite(next.DistanceMeters) || next.DistanceMeters < 0)
            throw new InvalidOperationException("Accepted passage must use its actual segment and finite distance.");
        if (CurrentPassages.TryGetValue((next.Origin.Step.StepNumber, next.RiderId), out var previous)
            && (previous.Complete || !ReferenceEquals(previous.Origin, next.Origin) || next.Steps.Count <= previous.Steps.Count
                || !previous.Steps.SequenceEqual(next.Steps.Take(previous.Steps.Count)) || next.DistanceMeters <= previous.DistanceMeters))
            throw new InvalidOperationException("Passage must extend the original accepted prefix exactly once.");
    }

    internal void SettleAfterHeat(TrackState surface, SimLog log)
    {
        RequireOrdinaryJournal(); // A11's interrupted/first-event journal awaits A12 completion.
        RequireOpen();
        if (surface.SegmentCount != Surface.SegmentCount || surface.LinesCount != Surface.LinesCount)
            throw new InvalidOperationException("Post-heat publication requires the original surface.");
        for (var segment = 0; segment < Surface.SegmentCount; segment++)
        for (var lane = 0; lane < Surface.LinesCount; lane++)
            if (surface.GetSurface(segment, lane) != Surface.GetSurface(segment, lane))
                throw new InvalidOperationException("External surface mutation during a frozen heat.");
        // One detached preflight per heat, never a world copy per time trial.
        var next = new TrackState(Surface.SegmentCount, Surface.LinesCount, Surface.GetSurface);
        var pendingLog = new SimLog(log.Enabled);
        foreach (var passage in _passages.OrderBy(p => p.Key.Step).ThenBy(p => p.Key.Rider).Select(p => p.Value))
            SimulationEngine.ApplyDeferredPassageWear(passage.Origin, passage.RiderId, passage.Steps, passage.DistanceMeters, next, pendingLog);
        TrackEvolution.ApplyWeather(Track, next, Options.Weather, HeatId, Options.Laps * Track.Segments.Count, pendingLog);
        surface.CopyCellsFrom(next);
        foreach (var line in pendingLog.Lines) log.Add(line);
        foreach (var delta in pendingLog.SurfaceChanges) log.Add(delta);
        _settled = true; WeatherUpdates = 1;
    }

    private void RequireOrigin(SimulationSnapshot origin)
    {
        RequireOpen();
        if (!ReferenceEquals(origin.Track, Track) || !ReferenceEquals(origin.TrackState, Surface)
            || origin.Step.HeatId != HeatId || origin.Step.Seed != Options.Seed || origin.Step.RequiredLaps != Options.Laps)
            throw new InvalidOperationException("Passage belongs to another heat or surface version.");
    }
    private void RequireOpen()
    {
        if (_settled) throw new InvalidOperationException("Heat already settled.");
    }
    private void RequireOrdinaryJournal()
    {
        if (_causalOwned) throw new InvalidOperationException("The causal owner must publish the whole four-rider transaction; A11 cannot settle an incomplete heat.");
    }
}
