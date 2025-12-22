// Kontrakt linii: indeks 0..4 oznacza środek ścieżki jazdy od krawędzi wewnętrznej (0) do zewnętrznej (4).
// Długość w łuku liczona jest po łuku o promieniu R = Rwew + lane * szerokość_linii.
namespace CoreSim;

public static class LaneModel
{
    public const int MinLane = 0;
    public const int MaxLane = 4;
    public const int LanesCount = 5;

    public const float LaneWidthMeters = 1.0f;
    public const float InnerRadiusMeters = 24.0f;
    public const float TurnSegmentAngleRadians = MathF.PI / 3.0f; // 60° na segment łuku (3 segmenty = 180°).
    public const float StraightLengthMeters = 60.0f;

    public static int ClampLane(int lane) => Math.Clamp(lane, MinLane, MaxLane);

    public static void ValidateLane(int lane)
    {
        if (lane < MinLane || lane > MaxLane)
        {
            throw new ArgumentOutOfRangeException(nameof(lane), lane, "Lane must be between 0 and 4 inclusive.");
        }
    }

    public static float TurnArcRadiusMeters(int lane)
    {
        ValidateLane(lane);
        return InnerRadiusMeters + lane * LaneWidthMeters;
    }

    public static float TurnArcLengthMeters(int lane) => TurnArcRadiusMeters(lane) * TurnSegmentAngleRadians;

    public static float SegmentLengthMeters(TrackSegment segment, int lane)
        => segment.Type == SegmentType.Straight ? StraightLengthMeters : TurnArcLengthMeters(lane);
}
