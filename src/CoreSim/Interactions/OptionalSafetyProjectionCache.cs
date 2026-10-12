using CoreSim.Decisions;

namespace CoreSim.Interactions;

// Physical identity deliberately excludes response labels and diagnostic reasons.
internal readonly record struct ProjectionKey(int RiderId, TrajectoryIntent Intent, float Drive, bool Hold, InteractionLateralTarget? PhysicalTarget = null)
{
    internal static ProjectionKey From(InteractionAlternative a)
        => new(a.RiderId, a.Intent, a.DriveControl?.PositiveDriveFraction ?? 1f, a.HoldLateralPosition, a.PhysicalTarget);
}

internal enum SafetyProjectionOutcome { Resolved, InfeasibleProductionProjection }

internal sealed record SafetyProjectionResult<T> where T : class
{
    internal SafetyProjectionOutcome Outcome { get; }
    internal T? Value { get; }
    internal string? FailureType { get; }
    private SafetyProjectionResult(SafetyProjectionOutcome outcome, T? value, string? failureType)
        => (Outcome, Value, FailureType) = (outcome, value, failureType);
    internal static SafetyProjectionResult<T> Resolved(T value)
        => new(SafetyProjectionOutcome.Resolved, value, null);
    internal static SafetyProjectionResult<T> Infeasible(ExecutedPathTraversal.TimeSolveFeasibilityException error)
        => new(SafetyProjectionOutcome.InfeasibleProductionProjection, null, error.GetType().FullName);
}

/// <summary>One immutable optional safety evaluation; failures have no motion or certificate.</summary>
internal sealed class OptionalSafetyProjectionCache<T>(Func<InteractionAlternative, T> resolve) where T : class
{
    private readonly Dictionary<ProjectionKey, SafetyProjectionResult<T>> _results = new();
    internal int Count => _results.Count;
    internal int FailedCount { get; private set; }
    internal int CacheHits { get; private set; }
    internal int Attempts { get; private set; }
    internal SafetyProjectionResult<T> Get(InteractionAlternative alternative)
    {
        var key = ProjectionKey.From(alternative);
        if (_results.TryGetValue(key, out var cached))
        { CacheHits++; ProjectionCaptureAudit.Record(ProjectionMaterialization.SafetyProjectionCacheHit); return cached; }
        Attempts++;
        ProjectionCaptureAudit.Record(ProjectionMaterialization.SafetyProjectionRequest);
        SafetyProjectionResult<T> result;
        try { result = SafetyProjectionResult<T>.Resolved(resolve(alternative)); }
        catch (ExecutedPathTraversal.TimeSolveFeasibilityException error)
        {
            result = SafetyProjectionResult<T>.Infeasible(error);
            FailedCount++;
            ProjectionCaptureAudit.Record(ProjectionMaterialization.SafetyProjectionFailure);
        }
        _results.Add(key, result);
        return result;
    }
}
