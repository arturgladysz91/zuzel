namespace CoreSim.Decisions;

public sealed record RiderDecisionContext(
    TrackSegment Segment,
    int SegmentIndex,
    TrackState TrackState,
    RiderState Rider,
    IReadOnlyList<RiderState> Riders,
    int HeatId,
    int Lap);
