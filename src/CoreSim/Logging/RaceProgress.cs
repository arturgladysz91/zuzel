using CoreSim.Race;

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
    RiderRaceStatus Status)
{
    public RiderOrderEntry(int riderId, int position, float gapSeconds, bool Crashed)
        : this(
            riderId,
            position,
            gapSeconds,
            Crashed ? RiderRaceStatus.Crashed : RiderRaceStatus.Racing)
    {
    }

    public bool Crashed => Status == RiderRaceStatus.Crashed;
    public bool Retired => Status == RiderRaceStatus.Retired;
    public bool Dnf => Crashed || Retired;
}

public sealed record RaceOrderSnapshot(
    int Lap,
    int SegmentId,
    IReadOnlyList<RiderOrderEntry> Order);
