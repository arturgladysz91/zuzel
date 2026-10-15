using CoreSim.Decisions;

namespace CoreSim;

/// <summary>Exact accumulators after an accepted coupled step; never reconstructed from sampled poses.</summary>
internal sealed record ExecutedTraversalBoundary(double CanonicalProgress, double EndCanonicalProgress,
    double TimeSeconds, double DistanceMeters, float Speed, float LateralPosition,
    TrackSurfaceState NodeSurface, int CompletedSteps, int TimeSolveSubdivisions,
    IReadOnlyList<ExecutedPathNode> Nodes, IReadOnlyList<ExecutedPathStep> Steps,
    ExecutedCornerBoundary? Corner = null, ExecutedPartialStep? PartialStep = null,
    IReadOnlyList<ExecutedStepEndpoint>? ExecutionEndpoints = null);

/// <summary>The old step end is a discarded forecast, not an executable post-event remainder.</summary>
internal sealed record ExecutedPartialStep(double StartCanonicalProgress, double UnsplitEndCanonicalProgress,
    double StartTimeSeconds, double UnsplitEndTimeSeconds, double LowerCanonicalProgress,
    double LowerTimeSeconds, int EndpointTrials);

internal readonly record struct ExecutedStepEndpoint(double CanonicalProgress, double TimeSeconds,
    double DistanceMeters, float Speed, float LateralPosition);
internal sealed record PartialStepSearch(Func<ExecutedStepEndpoint, bool> Reached, bool ContinueBeforeBracket = false,
    double TimeBracketToleranceSeconds = ExecutedPathTraversal.TimeToleranceSeconds);

/// <summary>Immutable accepted corner values plus exclusively owned reusable envelope computations.</summary>
internal sealed record ExecutedCornerBoundary(float InitialEnvelopeSpeed, float? ApexLateralPosition,
    float CornerProgress, ExecutedPathTraversal.CornerExecutionCache Cache);

internal sealed record TraversalPauseRequest(int? AdditionalSteps, ExecutedTraversalBoundary? Boundary = null,
    PartialStepSearch? PartialStep = null, bool AfterContact = false)
{
    internal bool CausalHeat { get; init; }
}
internal sealed class TraversalPausedException(ExecutedTraversalBoundary boundary) : Exception
{
    internal ExecutedTraversalBoundary Boundary { get; } = boundary;
}

internal class RiderIntegrationRefusalException(string detail) : InvalidOperationException(detail);

internal sealed record RiderIntegrationContext(SegmentResolution Resolution, float EntrySpeed,
    int PlannedLane, float CanonicalAdvance, float Risk, TrackSurfaceState EntrySurface,
    CornerPhaseContext? CornerPhase, float PositiveDriveFraction, bool HoldLateralPosition,
    float LateralAuthority01, float? InteractionTargetPosition);

// Retains the classified physical context and the production completion function, not a replay recipe.
internal sealed class RiderIntegrationSession(SimulationSnapshot snapshot, RiderIntent intent)
{
    internal SimulationSnapshot Snapshot { get; } = snapshot;
    internal RiderIntent Intent { get; } = intent;
    internal RiderIntegrationContext Context { get; set; } = null!;
    internal Func<TraversalPauseRequest, ExecutedTraversalResult> Continue { get; set; } = null!;
    internal Func<TraversalPauseRequest, CoreSim.Interactions.RiderContactConsequence?, ExecutedTraversalResult> ContinueWithContact { get; set; } = null!;
    internal Func<ExecutedTraversalResult, ResolvedSimulationStep> Complete { get; set; } = null!;
    internal TraversalPauseRequest Request { get; set; } = null!;
    internal CoreSim.Interactions.RiderContactConsequence? Contact { get; set; }
    internal bool CausalHeat { get; init; }
}

internal sealed record RiderIntegrationProgress(ResolvedSimulationStep? Completed, RiderIntegrationCursor? Cursor);

/// <summary>Single-use speculative execution token. The snapshot object identifies the frozen surface version.</summary>
internal sealed class RiderIntegrationCursor(RiderIntegrationSession session, ExecutedTraversalBoundary boundary)
{
    private bool _consumed;
    private readonly HashSet<ExecutedTraversalBoundary> _trials = new(ReferenceEqualityComparer.Instance);
    internal ExecutedTraversalBoundary Boundary { get; } = boundary;
    internal SimulationSnapshot Snapshot => session.Snapshot;
    internal RiderIntent Intent => session.Intent;
    internal RiderIntegrationContext Context => session.Context;
    internal TrackStateSnapshot SurfaceVersion => Snapshot.TrackState;
    internal double OriginTimeSeconds => Snapshot.Step.ExecutionOriginTimeSeconds ?? Snapshot.Riders[0].ElapsedTimeSeconds;
    internal double ExecutedThroughSeconds => OriginTimeSeconds + Boundary.TimeSeconds;

    // Isolated counterfactual token on the same accepted immutable prefix. No
    // classification, RNG or integration; only exact-input memoized queries are shared.
    internal RiderIntegrationCursor ForkFrozenExperiment()
    {
        if (_consumed || Boundary.PartialStep is not null || session.Contact is not null || !Snapshot.Step.FreezeSurfaceForHeat)
            throw new RiderIntegrationRefusalException("Experiment fork requires an unused ordinary frozen-heat prefix.");
        var fork = new RiderIntegrationSession(Snapshot, Intent)
        { Context = Context, Request = session.Request, Complete = session.Complete, ContinueWithContact = session.ContinueWithContact,
            CausalHeat = session.CausalHeat };
        fork.Continue = request => fork.ContinueWithContact(request, fork.Contact);
        return new(fork, Boundary);
    }

    internal ExecutedTraversalBoundary PreviewToTime(double elapsedTimeSeconds,
        double timeBracketToleranceSeconds = ExecutedPathTraversal.TimeToleranceSeconds)
    {
        if (_consumed || Boundary.PartialStep is not null)
            throw new RiderIntegrationRefusalException("Time trial requires an unused ordinary cursor.");
        var local = elapsedTimeSeconds - OriginTimeSeconds;
        if (!double.IsFinite(local) || local <= Boundary.TimeSeconds)
            throw new RiderIntegrationRefusalException("Time trial must advance beyond the accepted prefix.");
        if (!double.IsFinite(timeBracketToleranceSeconds) || timeBracketToleranceSeconds <= 0
            || timeBracketToleranceSeconds > ExecutedPathTraversal.TimeToleranceSeconds)
            throw new ArgumentOutOfRangeException(nameof(timeBracketToleranceSeconds));
        try { session.Continue(new(null, Boundary, new(e => e.TimeSeconds >= local, true, timeBracketToleranceSeconds))); }
        catch (TraversalPausedException paused) { _trials.Add(paused.Boundary); return paused.Boundary; }
        throw new RiderIntegrationRefusalException("Requested time is outside this segment.");
    }

    internal RiderIntegrationCursor AcceptTimeTrial(TrackStateSnapshot surfaceVersion, ExecutedTraversalBoundary trial)
    {
        if (_consumed || !ReferenceEquals(surfaceVersion, SurfaceVersion) || !_trials.Contains(trial) || trial.PartialStep is null
            || !Boundary.Steps.SequenceEqual(trial.Steps.Take(Boundary.Steps.Count)))
            throw new RiderIntegrationRefusalException("A time trial must retain this unused cursor and frozen surface.");
        _consumed = true; _trials.Clear();
        return new(session, trial);
    }

    internal RiderIntegrationProgress ResumeAfterContact(CoreSim.Interactions.RiderContactConsequence consequence,
        int pauseAfterSteps, double eventTimeToleranceSeconds = ExecutedPathTraversal.TimeToleranceSeconds)
        => ResumeEvent(consequence, pauseAfterSteps, eventTimeToleranceSeconds);

    // Counterfactual comparison only: same executed partition, no impulse/recovery and no publication.
    internal RiderIntegrationProgress ResumeEventWithoutConsequence(int pauseAfterSteps)
        => ResumeEvent(null, pauseAfterSteps);

    internal void Seal() { _consumed = true; _trials.Clear(); }

    private RiderIntegrationProgress ResumeEvent(CoreSim.Interactions.RiderContactConsequence? consequence,
        int pauseAfterSteps, double eventTimeToleranceSeconds = ExecutedPathTraversal.TimeToleranceSeconds)
    {
        if (!double.IsFinite(eventTimeToleranceSeconds) || eventTimeToleranceSeconds < ExecutedPathTraversal.TimeToleranceSeconds
            || eventTimeToleranceSeconds > CoreSim.Race.TwoRiderContactExecution.MaximumEventTimeToleranceSeconds
            || eventTimeToleranceSeconds > ExecutedPathTraversal.TimeToleranceSeconds && !Snapshot.Step.FreezeSurfaceForHeat)
            throw new ArgumentOutOfRangeException(nameof(eventTimeToleranceSeconds));
        if (_consumed || Boundary.PartialStep is null || session.Contact is not null
            || consequence is not null && (consequence.RiderId != Snapshot.Riders[0].RiderId || consequence.PreContactSpeed != Boundary.Speed
            || Math.Abs(consequence.FrontierTimeSeconds - ExecutedThroughSeconds) > eventTimeToleranceSeconds))
            throw new RiderIntegrationRefusalException("Post-event continuation requires this exact unconsumed contact state.");
        if (consequence is not null && (consequence.Severity == CoreSim.Interactions.PhysicalContactSeverity.Crash || consequence.PostContactSpeed <= 0))
            throw new RiderIntegrationRefusalException("Terminal contact ends motion at the event; it cannot resume.");
        if (pauseAfterSteps <= 0) throw new ArgumentOutOfRangeException(nameof(pauseAfterSteps));
        _consumed = true; session.Contact = consequence;
        var speed = consequence?.PostContactSpeed ?? Boundary.Speed;
        var nodes = Boundary.Nodes.ToArray();
        nodes[^1] = nodes[^1] with { SpeedMetersPerSecond = speed };
        var endpoints = Boundary.ExecutionEndpoints!.ToArray();
        endpoints[^1] = endpoints[^1] with { Speed = speed };
        var after = Boundary with { Speed = speed, Nodes = Array.AsReadOnly(nodes), PartialStep = null,
            ExecutionEndpoints = Array.AsReadOnly(endpoints) };
        try { return new(session.Complete(session.Continue(new(pauseAfterSteps, after))), null); }
        catch (TraversalPausedException paused) { return new(null, new(session, paused.Boundary)); }
    }

    internal RiderIntegrationProgress Resume(TrackStateSnapshot surfaceVersion, int? pauseAfterSteps = null)
    {
        if (_consumed) throw new RiderIntegrationRefusalException("Execution token already consumed.");
        if (Boundary.PartialStep is not null)
            throw new RiderIntegrationRefusalException("A partial event boundary requires explicit post-event controls/state; ordinary resume is unavailable.");
        if (!ReferenceEquals(surfaceVersion, SurfaceVersion))
            throw new RiderIntegrationRefusalException("Resume requires the same frozen surface version.");
        if (pauseAfterSteps is <= 0) throw new ArgumentOutOfRangeException(nameof(pauseAfterSteps));
        _consumed = true;
        try { return new(session.Complete(session.Continue(new(pauseAfterSteps, Boundary))), null); }
        catch (TraversalPausedException pause) { return new(null, new(session, pause.Boundary)); }
    }

    internal RiderIntegrationCursor SplitNextStepAtTime(TrackStateSnapshot surfaceVersion, double elapsedTimeSeconds)
    {
        if (!double.IsFinite(elapsedTimeSeconds) || elapsedTimeSeconds <= ExecutedThroughSeconds)
            throw new ArgumentOutOfRangeException(nameof(elapsedTimeSeconds));
        var localTime = elapsedTimeSeconds - OriginTimeSeconds;
        return SplitNextStep(surfaceVersion, new(e => e.TimeSeconds >= localTime));
    }

    internal RiderIntegrationCursor SplitNextStep(TrackStateSnapshot surfaceVersion, PartialStepSearch search)
    {
        ArgumentNullException.ThrowIfNull(search);
        if (_consumed) throw new RiderIntegrationRefusalException("Execution token already consumed.");
        if (!ReferenceEquals(surfaceVersion, SurfaceVersion))
            throw new RiderIntegrationRefusalException("Partial execution requires the same frozen surface version.");
        if (Snapshot.Segment.Type != SegmentType.TurnExit || Boundary.PartialStep is not null)
            throw new RiderIntegrationRefusalException("Partial execution requires a completed-step TurnExit cursor before an event.");
        _consumed = true;
        try
        {
            session.Continue(new(null, Boundary, search));
            throw new InvalidOperationException("Partial execution must stop before segment completion.");
        }
        catch (TraversalPausedException pause) { return new(session, pause.Boundary); }
    }
}
