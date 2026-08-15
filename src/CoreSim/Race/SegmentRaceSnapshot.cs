using CoreSim.Setup;

namespace CoreSim.Race;

/// <summary>Immutable rider view shared by every calculation in one segment.</summary>
public sealed record RiderSegmentSnapshot(
    int RiderId,
    RiderProfile Profile,
    BikeSetup ActiveSetup,
    int StartingGate,
    int CurrentLap,
    int SegmentIndex,
    int SegmentId,
    float SegmentProgressMeters,
    int Lane,
    float LateralPosition,
    float Speed,
    float CornerExitSpeed,
    float ElapsedTimeSeconds,
    float DistanceMeters,
    float Morale,
    float ManagerTrust,
    bool IsCrashed,
    int RunningPosition,
    float GapAheadSeconds,
    float GapAheadMeters,
    IReadOnlySet<int> OccupiedLanes)
{
    internal RiderState ToDecisionState()
        => new(Profile, Lane, Morale, ManagerTrust)
        {
            ActiveSetup = ActiveSetup,
            StartingGate = StartingGate,
            CurrentLap = CurrentLap,
            CurrentSegmentIndex = SegmentIndex,
            CurrentSegmentId = SegmentId,
            SegmentProgressMeters = SegmentProgressMeters,
            LateralPosition = LateralPosition,
            Speed = Speed,
            CornerExitSpeed = CornerExitSpeed,
            ElapsedTimeSeconds = ElapsedTimeSeconds,
            DistanceMeters = DistanceMeters,
            IsCrashed = IsCrashed,
            GapToRiderAheadSeconds = GapAheadSeconds,
            GapToRiderAheadMeters = GapAheadMeters,
            OccupiedLanes = OccupiedLanes.OrderBy(lane => lane).ToArray(),
        };
}

public sealed record SegmentRaceSnapshot(
    int HeatId,
    int Lap,
    int SegmentIndex,
    TrackSegment Segment,
    IReadOnlyList<TrackSurfaceState> Surfaces,
    IReadOnlyList<RiderSegmentSnapshot> Riders)
{
    public RiderSegmentSnapshot Rider(int riderId)
        => Riders.Single(rider => rider.RiderId == riderId);
}
