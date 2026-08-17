using CoreSim.Setup;
using CoreSim.Race;

namespace CoreSim;

/// <summary>Mutable rider state for one heat.</summary>
public sealed class RiderState
{
    private float _morale;
    private float _managerTrust;
    private RiderPosition _position;
    private RiderRaceStatus _status;

    public int RiderId => Profile.Id;
    public RiderProfile Profile { get; }
    public RiderPosition Position => _position;
    public int LapNumber => _status == RiderRaceStatus.Finished
        ? Math.Max(1, LapsCompleted)
        : LapsCompleted + 1;
    public int SegmentIndex => _position.SegmentIndex;
    public float SegmentProgress => _position.SegmentProgress;
    public double CanonicalProgress => _position.TotalSegmentProgress;
    public int CurrentSegmentId => SegmentIndex;
    public int Lane { get; set; }
    public float LateralPosition { get; set; }
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

    /// <summary>Marks an active rider as retired without exposing arbitrary status mutation.</summary>
    public void Retire()
    {
        if (_status == RiderRaceStatus.Retired)
            return;
        if (_status is RiderRaceStatus.Finished or RiderRaceStatus.Crashed)
            throw new InvalidOperationException($"A rider with status {_status} cannot retire.");

        _status = RiderRaceStatus.Retired;
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

    internal void SetStatus(RiderRaceStatus status) => _status = status;

    public void ResetForHeat(int lane)
    {
        LaneModel.ValidateLane(lane);
        _position = _position.SegmentCount == 0
            ? default
            : RiderPosition.Start(_position.SegmentCount);
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
