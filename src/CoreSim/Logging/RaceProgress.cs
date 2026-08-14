namespace CoreSim.Logging;

public sealed record OvertakeEvent(
    int Lap,
    int SegmentId,
    int RiderId,
    int PassedRiderId,
    int FromPosition,
    int ToPosition);

public sealed record RiderOrderEntry(
    int RiderId,
    int Position,
    float GapSeconds,
    bool Crashed);

public sealed record RaceOrderSnapshot(
    int Lap,
    int SegmentId,
    IReadOnlyList<RiderOrderEntry> Order);
