namespace CoreSim;

/// <summary>Compact continuation and observations from the common production rider core.</summary>
internal sealed record SoloProjectionResult(SimulationSnapshot Snapshot, RiderStateChange Change,
    float TravelTimeSeconds, float DistanceMeters, float? ApexLateralPosition, ProjectionWear Wear, int TimeSolveSubdivisions = 0);

/// <summary>Unnormalized weights retain the exact multiplication order of production wear.</summary>
internal readonly record struct ProjectionWear(float[] Weights, float Scale, bool Executed, int Inner, int Outer);

internal readonly record struct ExecutedWearSample(float DistanceMeters, float LateralPosition);

internal readonly record struct ExecutedTraversalResult(ExecutedSegmentPath? RichPath,
    float DistanceMeters, float TravelTimeSeconds, float FinalLateralPosition, float ExitSpeedMetersPerSecond,
    float? ApexLateralPosition, ProjectionWear Wear, int TimeSolveSubdivisions);
