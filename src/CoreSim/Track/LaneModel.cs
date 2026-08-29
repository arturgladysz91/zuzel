// Kontrakt linii: indeks 0..4 oznacza środek ścieżki jazdy od krawędzi wewnętrznej (0) do zewnętrznej (4).
// Długość w łuku liczona jest po łuku o promieniu R = Rwew + lane * odstęp_linii.
namespace CoreSim;

public static class LaneModel
{
    public const int MinLane = 0;
    public const int MaxLane = 4;
    public const int LanesCount = 5;

    // Compatibility constants for callers that do not yet provide a concrete
    // track. New simulation code must use the TrackGeometry overloads below.
    public const float LaneWidthMeters = 1.0f;
    public const float InnerRadiusMeters = 24.0f;
    public const float TurnSegmentAngleRadians = MathF.PI / 3.0f;
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
        => TurnArcRadiusMeters(lane, TrackGeometry.Default);

    public static float TurnArcRadiusMeters(int lane, TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateLane(lane);
        return TurnArcRadiusMeters((float)lane, geometry);
    }

    public static float TurnArcRadiusMeters(float lateralPosition, TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (!float.IsFinite(lateralPosition)
            || lateralPosition < MinLane
            || lateralPosition > MaxLane)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lateralPosition),
                lateralPosition,
                "Lateral position must be finite and between 0 and 4 inclusive.");
        }

        return geometry.InnerRadiusMeters + lateralPosition * geometry.LaneSpacingMeters;
    }

    public static float TurnArcLengthMeters(int lane)
        => TurnArcLengthMeters(lane, TrackGeometry.Default);

    public static float TurnArcLengthMeters(int lane, TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateLane(lane);
        return TurnArcLengthMeters((float)lane, geometry);
    }

    public static float TurnArcLengthMeters(float lateralPosition, TrackGeometry geometry)
        => TurnArcRadiusMeters(lateralPosition, geometry) * geometry.TurnSegmentAngleRadians;

    public static float SegmentLengthMeters(TrackSegment segment, int lane)
        => SegmentLengthMeters(segment, lane, TrackGeometry.Default);

    public static float SegmentLengthMeters(TrackSegment segment, int lane, TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateLane(lane);
        return SegmentLengthMeters(segment, (float)lane, geometry);
    }

    public static float SegmentLengthMeters(
        TrackSegment segment,
        float lateralPosition,
        TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(geometry);
        return segment.Type == SegmentType.Straight
            ? geometry.StraightLengthMeters
            : TurnArcLengthMeters(lateralPosition, geometry);
    }
}
