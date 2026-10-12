using CoreSim.Decisions;

namespace CoreSim;

/// <summary>Exact accumulators after an accepted coupled step; never reconstructed from sampled poses.</summary>
internal sealed record ExecutedTraversalBoundary(double CanonicalProgress, double EndCanonicalProgress,
    double TimeSeconds, double DistanceMeters, float Speed, float LateralPosition,
    TrackSurfaceState NodeSurface, int CompletedSteps, int TimeSolveSubdivisions,
    IReadOnlyList<ExecutedPathNode> Nodes, IReadOnlyList<ExecutedPathStep> Steps);

internal sealed record TraversalPauseRequest(int? AdditionalSteps, ExecutedTraversalBoundary? Boundary = null);
internal sealed class TraversalPausedException(ExecutedTraversalBoundary boundary) : Exception
{
    internal ExecutedTraversalBoundary Boundary { get; } = boundary;
}

internal sealed class RiderIntegrationRefusalException(string detail) : InvalidOperationException(detail);

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
    internal Func<ExecutedTraversalResult, ResolvedSimulationStep> Complete { get; set; } = null!;
    internal TraversalPauseRequest Request { get; set; } = null!;
}

internal sealed record RiderIntegrationProgress(ResolvedSimulationStep? Completed, RiderIntegrationCursor? Cursor);

/// <summary>Single-use speculative execution token. The snapshot object identifies the frozen surface version.</summary>
internal sealed class RiderIntegrationCursor(RiderIntegrationSession session, ExecutedTraversalBoundary boundary)
{
    private bool _consumed;
    internal ExecutedTraversalBoundary Boundary { get; } = boundary;
    internal SimulationSnapshot Snapshot => session.Snapshot;
    internal RiderIntent Intent => session.Intent;
    internal RiderIntegrationContext Context => session.Context;
    internal TrackStateSnapshot SurfaceVersion => Snapshot.TrackState;

    internal RiderIntegrationProgress Resume(TrackStateSnapshot surfaceVersion, int? pauseAfterSteps = null)
    {
        if (_consumed) throw new RiderIntegrationRefusalException("Execution token already consumed.");
        if (!ReferenceEquals(surfaceVersion, SurfaceVersion))
            throw new RiderIntegrationRefusalException("Resume requires the same frozen surface version.");
        if (pauseAfterSteps is <= 0) throw new ArgumentOutOfRangeException(nameof(pauseAfterSteps));
        _consumed = true;
        try { return new(session.Complete(session.Continue(new(pauseAfterSteps, Boundary))), null); }
        catch (TraversalPausedException pause) { return new(null, new(session, pause.Boundary)); }
    }
}
