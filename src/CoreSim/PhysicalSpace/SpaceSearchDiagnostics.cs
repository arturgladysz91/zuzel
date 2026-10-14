namespace CoreSim.PhysicalSpace;

[Flags]
internal enum SpaceSearchRefusal
{
    None = 0, UncertainFirst = 1, MinimumBound = 2, SubdivisionDepth = 4, EvaluationLimit = 8, Penetration = 16
}

internal readonly record struct SpaceSearchDiagnostic(int RiderA, int RiderB, double StartSeconds, double EndSeconds,
    SpaceSearchRefusal Refusal, double UncertainFirstSeconds, double? FirstTouchSeconds, double MinimumMeters,
    double LipschitzMetersPerSecond, int Evaluations, int MaximumDepth,
    double FirstUncertainLowerMeters, double FirstUncertainWidthSeconds)
{
    internal double ContactToleranceMeters => GeometryNumerics.ContactDistanceMeters;
    internal double MinimumToleranceMeters => GeometryNumerics.MinimumSeparationToleranceMeters;
    internal double TimeToleranceSeconds => GeometryNumerics.TimeToleranceSeconds;
    internal int DepthLimit => GeometryNumerics.MaximumSubdivisionDepth;
    internal int EvaluationLimit => GeometryNumerics.MaximumEvaluationsPerInterval;
}

// Test-only hook: no records or logging are created when no observer is installed.
internal static class SpaceSearchDiagnostics
{
    [ThreadStatic] internal static Action<SpaceSearchDiagnostic>? Observer;
}
