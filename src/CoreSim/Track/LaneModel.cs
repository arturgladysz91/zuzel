// Lane/LateralPosition 0..4 are dimensionless normalized cross-track
// coordinates. Physical positions are derived from segment-local track width.
namespace CoreSim;

public static class LaneModel
{
    public const int MinLane = 0;
    public const int MaxLane = 4;
    public const int LanesCount = 5;

    // Compatibility constants for callers that do not yet provide a concrete
    // track. New simulation code must use the TrackGeometry overloads below.
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

    public static float PhysicalTrackWidthMeters(SegmentType segmentType, TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return segmentType switch
        {
            SegmentType.Straight => geometry.StraightWidthMeters,
            SegmentType.TurnEntry or SegmentType.TurnMiddle or SegmentType.TurnExit => geometry.TurnWidthMeters,
            _ => throw new ArgumentOutOfRangeException(nameof(segmentType)),
        };
    }

    public static float UsableRacingWidthMeters(SegmentType segmentType, TrackGeometry geometry)
    {
        var width = PhysicalTrackWidthMeters(segmentType, geometry)
            - TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters
            - TrackGeometry.ProvisionalOuterReferenceOffsetFromTrackEdgeMeters;
        if (!float.IsFinite(width) || width <= 0f)
            throw new ArgumentOutOfRangeException(nameof(geometry), "Usable racing width must be positive and finite.");

        return width;
    }

    public static float ReferenceLaneSpacingMeters(SegmentType segmentType, TrackGeometry geometry)
        => UsableRacingWidthMeters(segmentType, geometry) / MaxLane;

    public static float NormalizedLateralFraction(float lateralPosition)
    {
        ValidateLateralPosition(lateralPosition);
        return lateralPosition / MaxLane;
    }

    public static float PhysicalLateralOffsetFromInnerReferenceMeters(
        float lateralPosition,
        SegmentType segmentType,
        TrackGeometry geometry)
        => NormalizedLateralFraction(lateralPosition)
            * UsableRacingWidthMeters(segmentType, geometry);

    public static float PhysicalLateralOffsetFromInnerEdgeMeters(
        float lateralPosition,
        SegmentType segmentType,
        TrackGeometry geometry)
        => TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters
            + PhysicalLateralOffsetFromInnerReferenceMeters(lateralPosition, segmentType, geometry);

    public static float LateralPositionFromPhysicalOffsetMeters(
        float offsetFromInnerReferenceMeters,
        SegmentType segmentType,
        TrackGeometry geometry)
    {
        var usableWidth = UsableRacingWidthMeters(segmentType, geometry);
        if (!float.IsFinite(offsetFromInnerReferenceMeters)
            || offsetFromInnerReferenceMeters < 0f
            || offsetFromInnerReferenceMeters > usableWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetFromInnerReferenceMeters),
                offsetFromInnerReferenceMeters,
                "Physical offset must be finite and within the usable racing width.");
        }

        var lateralPosition = offsetFromInnerReferenceMeters / usableWidth * MaxLane;
        ValidateLateralPosition(lateralPosition);
        return lateralPosition;
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
        ValidateLateralPosition(lateralPosition);
        return geometry.InnerRadiusMeters
            + PhysicalLateralOffsetFromInnerReferenceMeters(
                lateralPosition,
                SegmentType.TurnMiddle,
                geometry);
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
            ? segment.StraightLengthMetersOverride ?? geometry.StraightLengthMeters
            : TurnArcLengthMeters(lateralPosition, geometry);
    }

    private static void ValidateLateralPosition(float lateralPosition)
    {
        if (!float.IsFinite(lateralPosition)
            || lateralPosition < MinLane
            || lateralPosition > MaxLane)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lateralPosition),
                lateralPosition,
                "Lateral position must be finite and between 0 and 4 inclusive.");
        }
    }
}
