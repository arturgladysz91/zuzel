namespace CoreSim.Decisions;

public sealed record RiderDecision(
    int TargetLane,
    float Risk = 0f,
    string? Reason = null);
