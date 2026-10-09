namespace CoreSim;

internal enum ProjectionMaterialization
{
    PhysicalEvaluation, RichMotion, MotionSample, RiderDiagnostics, Event, FormattedLog,
    ExecutedPath, ExecutedNode, ExecutedStep, LongitudinalNode, ResolvedStep, CornerNode,
    SimulationSnapshot, TrackStateSnapshot, RiderStateCopy, TrackStateCopy, PrivateCommit, PrefixCacheHit,
    CoupledEvaluation, EnvelopeCreation, ApexMetreIntegration, ScalarEnvelopeQuery, ApexRemainderIntegration,
    SafetyProjectionRequest, SafetyProjectionCacheHit, SafetyProjectionFailure, SafetyProductionAttempt, SafetyFrozenReuse,
}

/// <summary>Optional synchronous test observer. Normal runs retain no counters or audit state.</summary>
internal static class ProjectionCaptureAudit
{
    [ThreadStatic] internal static Action<ProjectionMaterialization, int>? Observer;
    internal static void Record(ProjectionMaterialization kind, int count = 1) => Observer?.Invoke(kind, count);
}
