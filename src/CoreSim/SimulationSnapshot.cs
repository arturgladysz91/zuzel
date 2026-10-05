using CoreSim.Race;
using CoreSim.Setup;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace CoreSim;

public sealed record SimulationStepContext(
    int HeatId,
    int StepNumber,
    int LapIndex,
    int SegmentIndex,
    int Seed,
    int RequiredLaps,
    bool UseLegacyPhysics = false);

/// <summary>Immutable rider value captured at the beginning of one simulation step.</summary>
public sealed record RiderSnapshot(
    int RiderId,
    RiderProfile Profile,
    RiderPosition Position,
    int LastResolvedSegmentId,
    int Lane,
    float LateralPosition,
    float Speed,
    float Risk,
    RiderRaceStatus Status,
    float ElapsedTimeSeconds,
    BikeSetup ActiveSetup,
    float Morale,
    float ManagerTrust)
{
    public int LapNumber => Status == RiderRaceStatus.Finished
        ? Math.Max(1, Position.LapsCompleted)
        : Position.LapsCompleted + 1;
    public int SegmentIndex => Position.SegmentIndex;
    public float SegmentProgress => Position.SegmentProgress;
    public double CanonicalProgress => Position.TotalSegmentProgress;
    /// <summary>TrackSegment.Id of the most recently committed segment, not its index.</summary>
    public int CurrentSegmentId => LastResolvedSegmentId;
    public float DistanceMeters => Position.DistanceMeters;
    public int LapsCompleted => Position.LapsCompleted;
    public bool IsCrashed => Status == RiderRaceStatus.Crashed;
    public bool IsActive => Status is RiderRaceStatus.NotStarted or RiderRaceStatus.Racing;
    public StartingGateBounds? StartingPosition { get; init; }
    public StartingGate? StartingGate => StartingPosition?.Gate;
    [JsonIgnore]
    public float Condition { get; init; } = 1f;

    internal RiderSnapshot Apply(RiderStateChange change) => this with
    {
        Position = change.Position, LastResolvedSegmentId = change.LastResolvedSegmentId,
        Lane = change.Lane, LateralPosition = change.LateralPosition, Speed = change.Speed,
        Risk = change.Risk, Status = change.Status, ElapsedTimeSeconds = change.ElapsedTimeSeconds,
        Morale = change.Morale,
    };

    internal RiderState ToMutableCopy()
    {
        ProjectionCaptureAudit.Record(ProjectionMaterialization.RiderStateCopy);
        var copy = new RiderState(Profile, Lane, Morale, ManagerTrust)
        {
            LateralPosition = LateralPosition,
            Speed = Speed,
            Risk = Risk,
            ElapsedTimeSeconds = ElapsedTimeSeconds,
            ActiveSetup = ActiveSetup,
            Condition = Condition,
        };
        copy.RestorePosition(Position);
        copy.SetLastResolvedSegmentId(LastResolvedSegmentId);
        copy.SetStatus(Status);
        copy.RestoreStartingPosition(StartingPosition);
        return copy;
    }
}

/// <summary>Detached value copy of all per-segment/per-lane surface cells.</summary>
public sealed class TrackStateSnapshot
{
    private readonly TrackSurfaceState[] _surfaces;

    public int SegmentCount { get; }
    public int LinesCount { get; }

    internal TrackStateSnapshot(TrackState source)
    {
        ProjectionCaptureAudit.Record(ProjectionMaterialization.TrackStateSnapshot);
        ArgumentNullException.ThrowIfNull(source);
        SegmentCount = source.SegmentCount;
        LinesCount = source.LinesCount;
        _surfaces = new TrackSurfaceState[SegmentCount * LinesCount];
        for (var segment = 0; segment < SegmentCount; segment++)
        for (var lane = 0; lane < LinesCount; lane++)
            _surfaces[Offset(segment, lane)] = source.GetSurface(segment, lane);
    }

    public TrackSurfaceState GetSurface(int segmentIndex, int lineIndex)
    {
        ValidateIndices(segmentIndex, lineIndex);
        return _surfaces[Offset(segmentIndex, lineIndex)];
    }

    public TrackSurfaceState SampleSurface(int segmentIndex, float lateralPosition)
    {
        if (!float.IsFinite(lateralPosition)
            || lateralPosition < 0f
            || lateralPosition > LinesCount - 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lateralPosition),
                lateralPosition,
                $"Lateral position must be finite and between 0 and {LinesCount - 1} inclusive.");
        }

        var innerLane = (int)MathF.Floor(lateralPosition);
        var outerLane = (int)MathF.Ceiling(lateralPosition);
        var inner = GetSurface(segmentIndex, innerLane);
        if (innerLane == outerLane)
            return inner;

        var outer = GetSurface(segmentIndex, outerLane);
        var fraction = lateralPosition - innerLane;
        return new TrackSurfaceState(
            inner.Grip + (outer.Grip - inner.Grip) * fraction,
            inner.Ruts + (outer.Ruts - inner.Ruts) * fraction,
            inner.Moisture + (outer.Moisture - inner.Moisture) * fraction);
    }

    private int Offset(int segmentIndex, int lineIndex) => segmentIndex * LinesCount + lineIndex;

    private void ValidateIndices(int segmentIndex, int lineIndex)
    {
        if (segmentIndex < 0 || segmentIndex >= SegmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        if (lineIndex < 0 || lineIndex >= LinesCount)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
    }
}

/// <summary>
/// Immutable, detached input shared by every rider decision in one step.
/// </summary>
public sealed class SimulationSnapshot
{
    private readonly ReadOnlyCollection<RiderSnapshot> _riders;

    public SimulationStepContext Step { get; }
    public Track Track { get; }
    public TrackSegment Segment => Track.Segments[Step.SegmentIndex];
    public TrackStateSnapshot TrackState { get; }
    public IReadOnlyList<RiderSnapshot> Riders => _riders;

    internal SimulationSnapshot(
        SimulationStepContext step,
        Track track,
        TrackStateSnapshot trackState,
        IEnumerable<RiderSnapshot> riders)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(trackState);
        ArgumentNullException.ThrowIfNull(riders);
        if (step.SegmentIndex < 0 || step.SegmentIndex >= track.Segments.Count)
            throw new ArgumentOutOfRangeException(nameof(step), "Segment index is outside the track.");

        Step = step;
        ProjectionCaptureAudit.Record(ProjectionMaterialization.SimulationSnapshot);
        // Track already owns immutable copied segments, geometry and topology.
        Track = track;
        TrackState = trackState;
        _riders = Array.AsReadOnly(riders.OrderBy(rider => rider.RiderId).ToArray());
    }

    public RiderSnapshot Rider(int riderId)
        => _riders.Single(rider => rider.RiderId == riderId);
}
