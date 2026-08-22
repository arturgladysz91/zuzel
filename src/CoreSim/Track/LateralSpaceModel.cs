namespace CoreSim;

/// <summary>
/// Converts continuous lane-unit positions into physical lateral separation.
/// </summary>
public static class LateralSpaceModel
{
    /// <summary>
    /// Provisional decision-occupancy distance preserving the previous threshold
    /// for the default 1 m lane spacing. It is not a final rider or motorcycle width.
    /// </summary>
    public const float ProvisionalOccupancyThresholdMeters = 0.55f;

    public static float LateralDistanceMeters(
        float firstLateralPosition,
        float secondLateralPosition,
        TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        LateralMovementModel.ValidateLateralPosition(firstLateralPosition, nameof(firstLateralPosition));
        LateralMovementModel.ValidateLateralPosition(secondLateralPosition, nameof(secondLateralPosition));

        return MathF.Abs(firstLateralPosition - secondLateralPosition) * geometry.LaneSpacingMeters;
    }

    public static bool IsWithinProvisionalOccupancyThreshold(
        float firstLateralPosition,
        float secondLateralPosition,
        TrackGeometry geometry)
        => LateralDistanceMeters(firstLateralPosition, secondLateralPosition, geometry)
            < ProvisionalOccupancyThresholdMeters;
}
