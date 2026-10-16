namespace CoreSim.Decisions;

/// <summary>
/// Experimental, one-traversal command. Deadline is absolute heat time, not progress.
/// Caller supplies an already known command; this performs no opponent observation.
/// </summary>
internal sealed record ScheduledControlChange
{
    internal int RiderId { get; }
    internal double ActivationElapsedTimeSeconds { get; }
    internal RiderDriveControl DriveControl { get; }
    internal bool? HoldLateralPosition { get; }
    internal InteractionLateralTarget? PhysicalTarget { get; }
    internal bool Enabled { get; }

    internal ScheduledControlChange(int riderId, double activationElapsedTimeSeconds,
        RiderDriveControl driveControl, bool? holdLateralPosition = null,
        InteractionLateralTarget? physicalTarget = null, bool enabled = true)
    {
        if (!double.IsFinite(activationElapsedTimeSeconds) || activationElapsedTimeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(activationElapsedTimeSeconds));
        RiderId = riderId; ActivationElapsedTimeSeconds = activationElapsedTimeSeconds;
        DriveControl = driveControl; HoldLateralPosition = holdLateralPosition;
        PhysicalTarget = physicalTarget; Enabled = enabled;
    }
}

/// <summary>The accepted point is retained; the command controls its outgoing step.</summary>
internal sealed record ScheduledControlActivation(int RiderId, double RequestedElapsedTimeSeconds,
    double ActualElapsedTimeSeconds, float LocalTimeSeconds, int AcceptedStepIndex);
