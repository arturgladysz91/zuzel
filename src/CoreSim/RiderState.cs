using CoreSim.Setup;
using CoreSim.Race;
using System.Text.Json.Serialization;

namespace CoreSim;

/// <summary>Mutable rider state for one heat.</summary>
public sealed class RiderState
{
    private float _morale;
    private float _condition = 1f;
    private float _managerTrust;
    private RiderPosition _position;
    private RiderRaceStatus _status;
    private int _lastResolvedSegmentId;
    private float _lateralPosition;

    public int RiderId => Profile.Id;
    public RiderProfile Profile { get; }
    public RiderPosition Position => _position;
    public int LapNumber => _status == RiderRaceStatus.Finished
        ? Math.Max(1, LapsCompleted)
        : LapsCompleted + 1;
    public int SegmentIndex => _position.SegmentIndex;
    public float SegmentProgress => _position.SegmentProgress;
    public double CanonicalProgress => _position.TotalSegmentProgress;
    /// <summary>TrackSegment.Id committed for the most recently resolved segment; never a topology key.</summary>
    public int LastResolvedSegmentId => _lastResolvedSegmentId;
    /// <summary>Backward-compatible alias for LastResolvedSegmentId.</summary>
    public int CurrentSegmentId => LastResolvedSegmentId;
    public int Lane { get; set; }
    /// <summary>Initial field identity and bounds only; never a constraint on racing movement.</summary>
    public StartingGateBounds? StartingPosition { get; private set; }
    public StartingGate? StartingGate => StartingPosition?.Gate;
    public float LateralPosition
    {
        get => _lateralPosition;
        set
        {
            LateralMovementModel.ValidateLateralPosition(value, nameof(LateralPosition));
            _lateralPosition = value;
        }
    }
    public float Speed { get; set; }
    public float Risk { get; set; }
    public RiderRaceStatus Status => _status;
    public bool IsCrashed
    {
        get => _status == RiderRaceStatus.Crashed;
        set
        {
            if (value)
                _status = RiderRaceStatus.Crashed;
            else if (_status == RiderRaceStatus.Crashed)
                _status = RiderRaceStatus.Racing;
        }
    }
    public float ElapsedTimeSeconds { get; set; }
    public float DistanceMeters => _position.DistanceMeters;
    public int LapsCompleted => _position.LapsCompleted;
    public BikeSetup ActiveSetup { get; set; } = BikeSetup.Neutral;

    public float Morale
    {
        get => _morale;
        set => _morale = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Dynamic physical condition (Kondycja). Preserved between heats; inert in #56A.</summary>
    [JsonIgnore] // Keep the historical simulation/evidence JSON contract unchanged.
    public float Condition
    {
        get => _condition;
        set
        {
            if (!float.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(Condition), value, "Condition must be finite.");
            _condition = Math.Clamp(value, 0f, 1f);
        }
    }

    public float ManagerTrust
    {
        get => _managerTrust;
        set => _managerTrust = Math.Clamp(value, 0f, 1f);
    }

    public RiderState(int riderId, int lane)
        : this(RiderProfile.CreateDefault(riderId), lane)
    {
    }

    public RiderState(RiderProfile profile, int lane, float morale = 0.5f, float managerTrust = 0.5f)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        LaneModel.ValidateLane(lane);
        Lane = lane;
        LateralPosition = lane;
        Morale = morale;
        ManagerTrust = managerTrust;
    }

    public void ApplyMoraleDelta(float delta) => Morale += delta;
    public void ApplyConditionDelta(float delta) => Condition += delta;

    public RiderState(RiderProfile profile, StartingGate gate, Track track,
        float morale = 0.5f, float managerTrust = 0.5f)
        : this(profile, 0, morale, managerTrust)
        => ResetForHeat(gate, track);

    public void ResetForHeat(StartingGate gate, Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (!track.Segments[0].IsStandingStartSegment)
            throw new ArgumentException("Explicit starting gates require a marked standing-start track.", nameof(track));
        var lateral = StartingGateGeometry.CenterLateralPosition(gate, track.Geometry);
        var field = new StartingGateGeometry(track.Geometry.StraightWidthMeters).Field(gate);
        // Lane remains a discrete racing/decision reference, not the gate number.
        ResetForHeat((int)MathF.Round(lateral));
        _position = track.StartFinishLine;
        LateralPosition = lateral;
        StartingPosition = field;
    }

    /// <summary>Marks an active rider as retired without exposing arbitrary status mutation.</summary>
    public void Retire()
    {
        if (_status is RiderRaceStatus.Finished or RiderRaceStatus.Crashed)
            throw new InvalidOperationException($"A rider with status {_status} cannot retire.");

        _status = RiderRaceStatus.Retired;
        Speed = 0f;
    }

    /// <summary>Restores a validated canonical position, for example from a save game.</summary>
    public void RestorePosition(RiderPosition position)
    {
        if (position.SegmentCount <= 0)
            throw new ArgumentException("Position must be initialized for a track.", nameof(position));
        _position = position;
    }

    internal RiderPosition PositionForTrack(int segmentCount)
    {
        if (_position.SegmentCount == 0)
            return RiderPosition.Start(segmentCount);
        if (_position.SegmentCount != segmentCount)
            throw new InvalidOperationException("Rider position belongs to a track with a different segment count.");
        return _position;
    }

    internal void CommitPosition(RiderPosition position) => _position = position;

    internal void SetLastResolvedSegmentId(int segmentId) => _lastResolvedSegmentId = segmentId;

    internal void SetStatus(RiderRaceStatus status) => _status = status;
    internal void RestoreStartingPosition(StartingGateBounds? position) => StartingPosition = position;

    public void ResetForHeat(int lane)
    {
        LaneModel.ValidateLane(lane);
        StartingPosition = null;
        _position = _position.SegmentCount == 0
            ? default
            : RiderPosition.Start(_position.SegmentCount);
        _lastResolvedSegmentId = 0;
        Lane = lane;
        LateralPosition = lane;
        Speed = 0f;
        Risk = 0f;
        _status = RiderRaceStatus.NotStarted;
        ElapsedTimeSeconds = 0f;
    }

    public static RiderState CreateDefault(int riderId, int lane)
        => new(riderId, lane);
}
