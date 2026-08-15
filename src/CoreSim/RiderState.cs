using CoreSim.Setup;

namespace CoreSim;

/// <summary>Mutable rider state for one heat.</summary>
public sealed class RiderState
{
    private float _morale;
    private float _managerTrust;

    public int RiderId => Profile.Id;
    public RiderProfile Profile { get; }
    public int StartingGate { get; set; }
    public int CurrentLap { get; set; }
    public int CurrentSegmentIndex { get; set; }
    public int CurrentSegmentId { get; set; }
    public float SegmentProgressMeters { get; set; }
    public int Lane { get; set; }
    public float LateralPosition { get; set; }
    public float Speed { get; set; }
    public float CornerExitSpeed { get; set; }
    public float Risk { get; set; }
    public bool IsCrashed { get; set; }
    public float ElapsedTimeSeconds { get; set; }
    public float DistanceMeters { get; set; }
    public int LapsCompleted { get; set; }
    public float GapToRiderAheadSeconds { get; set; } = float.PositiveInfinity;
    public float GapToRiderAheadMeters { get; set; } = float.PositiveInfinity;
    public IReadOnlyList<int> OccupiedLanes { get; internal set; } = Array.Empty<int>();
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
        StartingGate = lane;
        Lane = lane;
        LateralPosition = lane;
        Morale = morale;
        ManagerTrust = managerTrust;
    }

    public void ApplyMoraleDelta(float delta) => Morale += delta;

    public void ResetForHeat(int lane)
    {
        LaneModel.ValidateLane(lane);
        StartingGate = lane;
        CurrentLap = 0;
        CurrentSegmentIndex = 0;
        CurrentSegmentId = 0;
        SegmentProgressMeters = 0f;
        Lane = lane;
        LateralPosition = lane;
        Speed = 0f;
        CornerExitSpeed = 0f;
        Risk = 0f;
        IsCrashed = false;
        ElapsedTimeSeconds = 0f;
        DistanceMeters = 0f;
        LapsCompleted = 0;
        GapToRiderAheadSeconds = float.PositiveInfinity;
        GapToRiderAheadMeters = float.PositiveInfinity;
        OccupiedLanes = Array.Empty<int>();
    }

    public static RiderState CreateDefault(int riderId, int lane)
        => new(riderId, lane);
}
