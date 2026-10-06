namespace CoreSim.Decisions;

public sealed record RiderDecision(
    int TargetLane,
    float Risk = 0f,
    string? Reason = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public TrajectoryIntent? Trajectory { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public RiderDriveControl? DriveControl { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HoldLateralPosition { get; init; }
}

/// <summary>Transient positive-drive request. Resistance and correction remain production physics.</summary>
public readonly record struct RiderDriveControl
{
    public float PositiveDriveFraction { get; }
    public RiderDriveControl(float positiveDriveFraction)
    {
        if (!float.IsFinite(positiveDriveFraction) || positiveDriveFraction is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(positiveDriveFraction));
        PositiveDriveFraction = positiveDriveFraction;
    }
    public static RiderDriveControl LiftThrottle => new(0f);
}
