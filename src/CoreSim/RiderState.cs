// Zmienny stan zawodnika w trakcie biegu (linia, prędkość, ryzyko).
namespace CoreSim;

public sealed class RiderState
{
    public int RiderId { get; }
    public int CurrentSegmentId { get; set; }
    public int Lane { get; set; }
    public float Speed { get; set; }
    public float Risk { get; set; }

    public RiderState(int riderId, int lane)
    {
        RiderId = riderId;
        Lane = lane;
        Speed = 0f;
        Risk = 0f;
        CurrentSegmentId = 0;
    }

    public static RiderState CreateDefault(int riderId, int lane)
        => new RiderState(riderId, lane);
}
