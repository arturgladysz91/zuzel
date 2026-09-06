namespace CoreSim;

/// <summary>
/// Converts normalized cross-track positions into segment-local physical separation.
/// </summary>
public static class LateralSpaceModel
{
    /// <summary>
    /// Provisional decision-occupancy distance preserving the previous threshold
    /// for the default 1 m lane spacing. It is not a final rider or motorcycle width.
    /// </summary>
    public const float ProvisionalOccupancyThresholdMeters = 0.55f;

    /// <summary>
    /// Provisional contact-candidate distance. It intentionally remains separate
    /// from decision occupancy so both values can be calibrated independently.
    /// </summary>
    public const float ProvisionalContactThresholdMeters = 0.55f;

    public static float LateralDistanceMeters(
        float firstLateralPosition,
        float secondLateralPosition,
        SegmentType segmentType,
        TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        LateralMovementModel.ValidateLateralPosition(firstLateralPosition, nameof(firstLateralPosition));
        LateralMovementModel.ValidateLateralPosition(secondLateralPosition, nameof(secondLateralPosition));

        var firstOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            firstLateralPosition,
            segmentType,
            geometry);
        var secondOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            secondLateralPosition,
            segmentType,
            geometry);
        return MathF.Abs(firstOffset - secondOffset);
    }

    /// <summary>Compatibility overload for equal-width synthetic geometry.</summary>
    public static float LateralDistanceMeters(
        float firstLateralPosition,
        float secondLateralPosition,
        TrackGeometry geometry)
    {
        TrackGeometry.RequireEqualWidthCompatibility(geometry);
        return LateralDistanceMeters(firstLateralPosition, secondLateralPosition, SegmentType.Straight, geometry);
    }

    public static bool IsWithinProvisionalOccupancyThreshold(
        float firstLateralPosition,
        float secondLateralPosition,
        SegmentType segmentType,
        TrackGeometry geometry)
        => LateralDistanceMeters(firstLateralPosition, secondLateralPosition, segmentType, geometry)
            < ProvisionalOccupancyThresholdMeters;

    /// <summary>Compatibility overload for equal-width synthetic geometry.</summary>
    public static bool IsWithinProvisionalOccupancyThreshold(
        float firstLateralPosition,
        float secondLateralPosition,
        TrackGeometry geometry)
    {
        TrackGeometry.RequireEqualWidthCompatibility(geometry);
        return IsWithinProvisionalOccupancyThreshold(
            firstLateralPosition,
            secondLateralPosition,
            SegmentType.Straight,
            geometry);
    }

    public static bool IsWithinProvisionalContactThreshold(
        float firstLateralPosition,
        float secondLateralPosition,
        SegmentType segmentType,
        TrackGeometry geometry)
        => LateralDistanceMeters(firstLateralPosition, secondLateralPosition, segmentType, geometry)
            < ProvisionalContactThresholdMeters;

    /// <summary>Compatibility overload for equal-width synthetic geometry.</summary>
    public static bool IsWithinProvisionalContactThreshold(
        float firstLateralPosition,
        float secondLateralPosition,
        TrackGeometry geometry)
    {
        TrackGeometry.RequireEqualWidthCompatibility(geometry);
        return IsWithinProvisionalContactThreshold(
            firstLateralPosition,
            secondLateralPosition,
            SegmentType.Straight,
            geometry);
    }
}
