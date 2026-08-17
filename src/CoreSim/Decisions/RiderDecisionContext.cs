namespace CoreSim.Decisions;

public sealed class RiderDecisionContext
{
    private readonly bool _trackWasProvided;

    public SimulationSnapshot Snapshot { get; }
    public TrackSegment Segment => Snapshot.Segment;
    public int SegmentIndex => Snapshot.Step.SegmentIndex;
    public TrackStateSnapshot TrackState => Snapshot.TrackState;
    public RiderSnapshot Rider { get; }
    public IReadOnlyList<RiderSnapshot> Riders => Snapshot.Riders;
    public int HeatId => Snapshot.Step.HeatId;
    public int Lap => Snapshot.Step.LapIndex;
    public int StepNumber => Snapshot.Step.StepNumber;
    public int Seed => Snapshot.Step.Seed;
    public Track? Track => _trackWasProvided ? Snapshot.Track : null;

    internal RiderDecisionContext(SimulationSnapshot snapshot, RiderSnapshot rider, bool trackWasProvided = true)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Rider = rider ?? throw new ArgumentNullException(nameof(rider));
        if (!snapshot.Riders.Any(item => item.RiderId == rider.RiderId))
            throw new ArgumentException("Rider is not part of the snapshot.", nameof(rider));
        _trackWasProvided = trackWasProvided;
    }

    /// <summary>Compatibility constructor. Mutable inputs are copied immediately.</summary>
    public RiderDecisionContext(
        TrackSegment segment,
        int segmentIndex,
        TrackState trackState,
        RiderState rider,
        IReadOnlyList<RiderState> riders,
        int heatId,
        int lap,
        Track? track = null)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(trackState);
        ArgumentNullException.ThrowIfNull(rider);
        ArgumentNullException.ThrowIfNull(riders);

        var decisionTrack = track ?? CreateCompatibilityTrack(segment, segmentIndex, trackState.SegmentCount);
        var step = new SimulationStepContext(
            heatId,
            lap * decisionTrack.Segments.Count + segmentIndex,
            lap,
            segmentIndex,
            Seed: 0,
            RequiredLaps: Math.Max(1, lap + 1));
        var riderSnapshots = riders.Select(item => CaptureRider(item, decisionTrack.Segments.Count)).ToArray();
        Snapshot = new SimulationSnapshot(step, decisionTrack, trackState.Snapshot(), riderSnapshots);
        Rider = Snapshot.Rider(rider.RiderId);
        _trackWasProvided = track is not null;
    }

    private static RiderSnapshot CaptureRider(RiderState rider, int segmentCount)
        => new(
            rider.RiderId,
            rider.Profile,
            rider.PositionForTrack(segmentCount),
            rider.LastResolvedSegmentId,
            rider.Lane,
            rider.LateralPosition,
            rider.Speed,
            rider.Risk,
            rider.Status,
            rider.ElapsedTimeSeconds,
            rider.ActiveSetup,
            rider.Morale,
            rider.ManagerTrust);

    private static Track CreateCompatibilityTrack(TrackSegment segment, int segmentIndex, int segmentCount)
    {
        if (segmentIndex < 0 || segmentIndex >= segmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));

        var segments = Enumerable.Range(0, segmentCount)
            .Select(index => index == segmentIndex
                ? new TrackSegment(segment.Id, segment.Type)
                : new TrackSegment(index, SegmentType.Straight))
            .ToArray();
        return new Track(segments);
    }
}
