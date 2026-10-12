using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;

namespace CoreSim.Race;

internal enum CommonTimeCommitRefusal
{
    InsufficientCoverage,
    UncertifiedCoverage,
    PartialProductionCursorRequired,
}

internal sealed class CommonTimeCommitException(CommonTimeCommitRefusal reason, string detail)
    : InvalidOperationException($"{reason}: {detail}")
{
    internal CommonTimeCommitRefusal Reason { get; } = reason;
}

/// <summary>The coverage bound is a safety certificate, not an executed rider position.</summary>
internal static class CommonTimeCommitGuard
{
    internal static void Require(IReadOnlyList<RiderStateChange> candidates, double coveredThroughSeconds,
        double? firstUnresolvedContactSeconds)
    {
        if (!double.IsFinite(coveredThroughSeconds) || coveredThroughSeconds < 0
            || firstUnresolvedContactSeconds is { } contact && (!double.IsFinite(contact) || contact < 0)
            || candidates.Count == 0 || candidates.Any(c => !float.IsFinite(c.ElapsedTimeSeconds) || c.ElapsedTimeSeconds < 0))
            throw new ArgumentOutOfRangeException(nameof(coveredThroughSeconds));
        // Strict at touch: publication at the event also needs its resolved event state.
        if (firstUnresolvedContactSeconds is { } time && candidates.Any(c => c.ElapsedTimeSeconds >= time))
            throw new CommonTimeCommitException(CommonTimeCommitRefusal.PartialProductionCursorRequired,
                $"Unresolved first touch at {time:R} s; no rider may publish through it.");
        if (candidates.Any(c => c.ElapsedTimeSeconds > coveredThroughSeconds))
            throw new CommonTimeCommitException(CommonTimeCommitRefusal.InsufficientCoverage,
                $"All riders must have verified motion through {candidates.Max(c => c.ElapsedTimeSeconds):R} s.");
    }
}

/// <summary>
/// Exact segment-boundary continuation, including addressed controls and frozen surface provenance.
/// This is NOT an interior ExecutedPathTraversal pause token.
/// </summary>
internal sealed record PendingRiderSegment(RiderIntent Intent, ResolvedSimulationStep Resolution);
internal sealed record RiderMotionContinuation(RiderSnapshot Committed, RiderSnapshot Speculative,
    IReadOnlyList<PendingRiderSegment> Pending, RiderIntegrationCursor? ExecutionCursor = null);

/// <summary>
/// Internal, explicitly invoked REA-004A2 foundation. Executes selected controls with production
/// physics on one frozen world version. Certifies all riders before publishing their first complete
/// segments once. No traffic planning, contact fallback, weather ticks or general heat loop.
/// </summary>
internal sealed class CommonTimeMotionWindow
{
    private readonly SimulationEngine _engine;
    private readonly SimulationSnapshot _origin;
    private readonly HeatSimulationOptions _options;
    private readonly Dictionary<int, List<PendingRiderSegment>> _pending;
    private readonly Dictionary<int, RiderSnapshot> _forecast;
    private readonly Dictionary<int, RiderIntegrationCursor> _paused = new();
    private readonly TrackMetricEmbedding _embedding;
    private bool _committed;
    private IReadOnlyList<ResolvedRiderMotion> _committedHistory = Array.Empty<ResolvedRiderMotion>();

    // Published complete segments cover all riders through this minimum. Faster riders can
    // end later only inside the certified no-contact horizon, never past an unresolved contact.
    internal double CommittedWatermarkSeconds { get; private set; }
    internal double? CertifiedThroughSeconds { get; private set; }
    internal IReadOnlyList<ResolvedRiderMotion> CommittedHistory => _committedHistory;
    internal IReadOnlyList<RiderMotionContinuation> Continuations => _origin.Riders.Select(r =>
        new RiderMotionContinuation(_committed ? r.Apply(_pending[r.RiderId][0].Resolution.Changes.Single()) : r,
            _forecast[r.RiderId], Array.AsReadOnly(_pending[r.RiderId].Skip(_committed ? 1 : 0).ToArray()),
            _paused.GetValueOrDefault(r.RiderId))).ToArray();

    internal CommonTimeMotionWindow(SimulationEngine engine, SimulationSnapshot origin, HeatSimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(engine); ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(options); options.Validate();
        if (origin.Step.UseLegacyPhysics || origin.Riders.Count is < 1 or > 4
            || origin.Riders.Select(r => r.RiderId).Distinct().Count() != origin.Riders.Count
            || origin.Riders.Any(r => !r.IsActive || !float.IsFinite(r.ElapsedTimeSeconds) || r.ElapsedTimeSeconds < 0)
            || origin.Riders.Select(r => r.ElapsedTimeSeconds).Distinct().Count() != 1)
            throw new ArgumentException("A new window requires one exact shared active origin; asynchronous origins need retained executed prefixes.", nameof(origin));
        if (origin.Step.Seed != options.Seed || origin.Step.RequiredLaps != options.Laps)
            throw new ArgumentException("Window options must preserve the captured seed and required laps.", nameof(options));
        _engine = engine; _origin = origin; _options = options;
        _pending = origin.Riders.ToDictionary(r => r.RiderId, _ => new List<PendingRiderSegment>());
        _forecast = origin.Riders.ToDictionary(r => r.RiderId);
        _embedding = new(origin.Track, deterministicArithmetic: true);
        CommittedWatermarkSeconds = origin.Riders[0].ElapsedTimeSeconds;
    }

    /// <summary>Each rider owns its own segment/lap and RNG step address. Resolve mutates no live state.</summary>
    internal ResolvedSimulationStep StageSelectedMotion(RiderIntent intent, int decisionStepNumber)
    {
        var snapshot = PrepareSelectedMotion(intent, decisionStepNumber);
        // The caller selects controls before staging. Coordinator endpoint consequences are
        // deliberately unavailable here; #55 certifies the actual staged movement below.
        var resolved = _engine.ResolveProduction(snapshot, new[] { intent }, _options, legacyContacts: false);
        AcceptCompleted(intent, resolved);
        return resolved;
    }

    internal RiderIntegrationProgress StageSelectedMotionWithPause(RiderIntent intent, int decisionStepNumber,
        int pauseAfterSteps)
    {
        var snapshot = PrepareSelectedMotion(intent, decisionStepNumber);
        var progress = _engine.BeginRiderIntegration(snapshot, intent, _options, pauseAfterSteps);
        RetainProgress(intent, progress);
        return progress;
    }

    internal RiderIntegrationProgress ResumeSelectedMotion(int riderId, TrackStateSnapshot surfaceVersion,
        int? pauseAfterSteps = null)
    {
        var cursor = _paused[riderId];
        var progress = cursor.Resume(surfaceVersion, pauseAfterSteps);
        _paused.Remove(riderId);
        RetainProgress(cursor.Intent, progress);
        return progress;
    }

    internal RiderContactStepStop SplitSelectedMotionAtFirstTouch(int riderId, TrackStateSnapshot surfaceVersion,
        IReadOnlyList<PhysicalPoseInterval> opponentHistory)
    {
        var stopped = _paused[riderId].SplitNextStepAtFirstTouch(surfaceVersion, opponentHistory);
        _paused[riderId] = stopped.Cursor;
        return stopped;
    }

    private void RetainProgress(RiderIntent intent, RiderIntegrationProgress progress)
    {
        if (progress.Cursor is { } cursor) _paused.Add(intent.RiderId, cursor);
        else AcceptCompleted(intent, progress.Completed!);
    }

    private SimulationSnapshot PrepareSelectedMotion(RiderIntent intent, int decisionStepNumber)
    {
        if (_committed) throw new InvalidOperationException("A published window is sealed; its retained suffix needs a new reviewed world version.");
        if (_paused.ContainsKey(intent.RiderId))
            throw new RiderIntegrationRefusalException("Resume the pending numerical cursor before selecting another segment.");
        var rider = _forecast[intent.RiderId];
        var queue = _pending[intent.RiderId];
        if (!rider.IsActive || decisionStepNumber < _origin.Step.StepNumber
            || queue.Count != 0 && decisionStepNumber <= queue[^1].Resolution.Snapshot.Step.StepNumber)
            throw new ArgumentException("A continuation needs an active rider and a fresh addressed decision step.");
        var step = _origin.Step with { StepNumber = decisionStepNumber,
            LapIndex = rider.LapsCompleted, SegmentIndex = rider.SegmentIndex };
        return new SimulationSnapshot(step, _origin.Track, _origin.TrackState, new[] { rider });
    }

    private void AcceptCompleted(RiderIntent intent, ResolvedSimulationStep resolved)
    {
        var rider = _forecast[intent.RiderId];
        var change = resolved.Changes.Single();
        if (change.ElapsedTimeSeconds <= rider.ElapsedTimeSeconds)
            throw new InvalidOperationException("A staged segment requires positive executed time.");
        _pending[intent.RiderId].Add(new(intent, resolved));
        _forecast[intent.RiderId] = rider.Apply(change);
    }

    internal void CommitFirstSegments(IReadOnlyList<RiderState> riders, TrackState surface, SimLog log)
    {
        ArgumentNullException.ThrowIfNull(riders); ArgumentNullException.ThrowIfNull(surface); ArgumentNullException.ThrowIfNull(log);
        if (_committed) throw new InvalidOperationException("Window already published.");
        RequireOriginalWorld(riders, surface);
        if (_paused.Count != 0)
            throw new CommonTimeCommitException(CommonTimeCommitRefusal.InsufficientCoverage,
                "A paused speculative prefix has no publishable segment or certified contact state.");
        if (_pending.Values.Any(q => q.Count == 0))
            throw new CommonTimeCommitException(CommonTimeCommitRefusal.InsufficientCoverage, "Every rider requires executed forecast coverage.");

        var candidates = _pending.OrderBy(p => p.Key).Select(p => p.Value[0].Resolution).ToArray();
        var changes = candidates.SelectMany(s => s.Changes).ToArray();
        var poses = CommonTimePoseHistory.Stitch(_pending.Values.SelectMany(q => q)
            .SelectMany(p => p.Resolution.Motions).SelectMany(m => ResolvedBikePoses.FromMotion(m, _origin.Track, embedding: _embedding)));
        var coveredThrough = RequireContinuousCoverage(poses);
        // A rider's final pose cannot certify another rider's later endpoint.
        CommonTimeCommitGuard.Require(changes, coveredThrough, null);
        var clipped = poses.Where(p => p.StartTimeSeconds < coveredThrough)
            .Select(p => p.EndTimeSeconds <= coveredThrough ? p : new ClippedPose(p, coveredThrough)).ToArray();
        var report = CommonTimePoseHistory.Observe(clipped);
        var firstContact = report.Intervals.Where(p => p.HasConflict)
            .Select(p => p.FirstTouchCommonTimeSeconds).Min();
        CommonTimeCommitGuard.Require(changes, coveredThrough, firstContact);
        var publicationEnd = changes.Max(c => (double)c.ElapsedTimeSeconds);
        if (report.FrameCoverageGaps.Any(g => g.StartCommonTimeSeconds < publicationEnd)
            || report.Intervals.Any(p => p.IntervalStartSeconds <= publicationEnd
                && (!p.NumericallyResolved || p.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous))))
            throw new CommonTimeCommitException(CommonTimeCommitRefusal.UncertifiedCoverage, "#55 coverage is unresolved or frame ambiguous.");

        // Complete production commit on detached state is the preflight. Wear, statuses,
        // addressed incidents and events are published only after all validation succeeds.
        var privateSurface = surface.Clone();
        var privateLog = new SimLog(log.Enabled);
        foreach (var candidate in candidates)
        {
            var privateRider = candidate.Snapshot.Riders.Single().ToMutableCopy();
            _engine.Commit(candidate, new[] { privateRider }, privateSurface, privateLog);
        }
        var byId = riders.ToDictionary(r => r.RiderId);
        foreach (var change in changes) SimulationEngine.CommitRiderChange(change, byId[change.RiderId]);
        surface.CopyCellsFrom(privateSurface);
        foreach (var line in privateLog.Lines) log.Add(line);
        foreach (var change in privateLog.SurfaceChanges) log.Add(change);
        _committedHistory = Array.AsReadOnly(candidates.SelectMany(c => c.Motions).ToArray());
        CertifiedThroughSeconds = coveredThrough;
        CommittedWatermarkSeconds = changes.Min(c => (double)c.ElapsedTimeSeconds);
        _committed = true;
    }

    private double RequireContinuousCoverage(IReadOnlyList<PhysicalPoseInterval> poses)
    {
        var ends = new List<double>();
        foreach (var rider in _origin.Riders)
        {
            var end = CommittedWatermarkSeconds;
            foreach (var pose in poses.Where(p => p.RiderId == rider.RiderId))
            {
                // Zero-time boundary reinterpretations do not invent a swept interval.
                if (pose.StartTimeSeconds != end)
                    throw new CommonTimeCommitException(CommonTimeCommitRefusal.UncertifiedCoverage, $"R{rider.RiderId} has a physical time gap at {end:R} s.");
                end = pose.EndTimeSeconds;
            }
            ends.Add(end);
        }
        return ends.Min();
    }

    private void RequireOriginalWorld(IReadOnlyList<RiderState> riders, TrackState surface)
    {
        var current = _engine.CaptureSnapshot(_origin.Track, surface, riders, _origin.Step);
        if (!current.Riders.SequenceEqual(_origin.Riders))
            throw new InvalidOperationException("Window no longer owns the captured rider state.");
        for (var segment = 0; segment < surface.SegmentCount; segment++)
        for (var lane = 0; lane < surface.LinesCount; lane++)
            if (surface.GetSurface(segment, lane) != _origin.TrackState.GetSurface(segment, lane))
                throw new InvalidOperationException("Window no longer owns the frozen surface/weather version.");
    }

    // Clips only the observation domain; this never creates an executed prefix or rider state.
    private sealed class ClippedPose(PhysicalPoseInterval source, double end) : PhysicalPoseInterval(source.RiderId,
        source.FrameId, source.StartTimeSeconds, end, source.Dimensions, source.StartsAtDiscontinuity, source.Source, source.SupportsLapWrap)
    {
        internal override bool DeterministicArithmetic => source.DeterministicArithmetic;
        public override PhysicalBikePose Sample(double time) { Fraction(time); return source.Sample(time); }
        internal override BikePoseValue SampleValue(double time) { Fraction(time); return source.SampleValue(time); }
        public override PoseRateBounds RateBounds(double start, double finish)
        { Fraction(start); Fraction(finish); return source.RateBounds(start, finish); }
    }
}
