namespace CoreSim.Logging;

public enum RaceEventType
{
    Start,
    AttackStarted,
    Defense,
    AttackBlocked,
    AttackSucceeded,
    AttackFailed,
    Overtake,
    Contact,
    RunWide,
    LaneChanged,
}

public sealed record RaceEvent(
    RaceEventType Type,
    int Lap,
    int SegmentId,
    int RiderId,
    int? OtherRiderId = null,
    int? Lane = null,
    float? GapSeconds = null,
    float? Speed = null,
    string? Detail = null);

public sealed record SegmentOrderSnapshot(
    int Lap,
    int SegmentId,
    IReadOnlyList<RiderOrderEntry> Order);
