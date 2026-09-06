namespace CoreSim;

/// <summary>
/// Time-based execution along a dimensionless normalized cross-track coordinate.
/// Physical distances are derived from the current segment's local width.
/// </summary>
public static class LateralMovementModel
{
    /// <summary>Provisional minimum normalized traversal rate for a rider with no execution skill.</summary>
    public const float MinLateralTraversalRateLaneUnitsPerSecond = 0.35f;

    /// <summary>Provisional maximum normalized traversal rate for a rider with full execution skill.</summary>
    public const float MaxLateralTraversalRateLaneUnitsPerSecond = 0.65f;

    /// <summary>Share of physical execution supplied by normalized slide control.</summary>
    public const float SlideControlExecutionWeight = 0.50f;

    /// <summary>Share of physical execution supplied by normalized adaptability.</summary>
    public const float AdaptabilityExecutionWeight = 0.50f;

    /// <summary>Provisional minimum multiplier applied on a surface with no effective grip.</summary>
    public const float MinGripMultiplier = 0.65f;

    /// <summary>Provisional effective-grip contribution to the physical movement multiplier.</summary>
    public const float EffectiveGripMultiplierRange = 0.35f;

    /// <summary>Physical distance from a discrete lane that counts as arrival at that lane.</summary>
    public const float LaneArrivalToleranceMeters = 0.05f;

    public static int CalculatePlannedLane(
        int currentLane,
        float currentLateralPosition,
        int targetLane,
        SegmentType segmentType,
        TrackGeometry geometry,
        bool useContinuousPlanning)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        LaneModel.ValidateLane(currentLane);
        LaneModel.ValidateLane(targetLane);
        ValidateLateralPosition(currentLateralPosition, nameof(currentLateralPosition));

        if (!useContinuousPlanning)
        {
            var legacyDirection = Math.Sign(targetLane - currentLane);
            return LaneModel.ClampLane(currentLane + legacyDirection);
        }

        if (targetLane > currentLateralPosition)
        {
            var nearestOuterLane = (int)MathF.Ceiling(currentLateralPosition);
            if (HasArrivedAtLane(currentLateralPosition, nearestOuterLane, segmentType, geometry))
                nearestOuterLane++;

            return LaneModel.ClampLane(Math.Min(nearestOuterLane, targetLane));
        }

        if (targetLane < currentLateralPosition)
        {
            var nearestInnerLane = (int)MathF.Floor(currentLateralPosition);
            if (HasArrivedAtLane(currentLateralPosition, nearestInnerLane, segmentType, geometry))
                nearestInnerLane--;

            return LaneModel.ClampLane(Math.Max(nearestInnerLane, targetLane));
        }

        return targetLane;
    }

    /// <summary>Compatibility overload for equal-width synthetic geometry.</summary>
    public static int CalculatePlannedLane(
        int currentLane,
        float currentLateralPosition,
        int targetLane,
        TrackGeometry geometry,
        bool useContinuousPlanning)
    {
        TrackGeometry.RequireEqualWidthCompatibility(geometry);
        return CalculatePlannedLane(
            currentLane,
            currentLateralPosition,
            targetLane,
            SegmentType.Straight,
            geometry,
            useContinuousPlanning);
    }

    public static float CalculateNormalizedLateralTraversalRateLaneUnitsPerSecond(RiderSkills skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var slideControl = RiderSkills.Normalize(skills.SlideControl);
        var adaptability = RiderSkills.Normalize(skills.Adaptability);
        var execution = SlideControlExecutionWeight * slideControl
            + AdaptabilityExecutionWeight * adaptability;
        return MinLateralTraversalRateLaneUnitsPerSecond
            + (MaxLateralTraversalRateLaneUnitsPerSecond
                - MinLateralTraversalRateLaneUnitsPerSecond) * execution;
    }

    public static float CalculateMaxLateralDistanceMeters(
        float segmentTravelTimeSeconds,
        SegmentType segmentType,
        TrackGeometry geometry,
        TrackSurfaceState surface,
        RiderSkills skills)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(skills);
        if (!float.IsFinite(segmentTravelTimeSeconds) || segmentTravelTimeSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(segmentTravelTimeSeconds),
                segmentTravelTimeSeconds,
                "Segment travel time must be finite and non-negative.");
        }

        var laneRate = CalculateNormalizedLateralTraversalRateLaneUnitsPerSecond(skills);
        var effectiveGrip = surface.EffectiveGrip;
        if (!float.IsFinite(effectiveGrip))
            throw new ArgumentOutOfRangeException(nameof(surface), "Effective grip must be finite.");

        var gripMultiplier = MinGripMultiplier + EffectiveGripMultiplierRange * effectiveGrip;
        var maxLaneDelta = (double)laneRate
            * segmentTravelTimeSeconds
            * gripMultiplier;
        var physicalMeters = maxLaneDelta
            * LaneModel.ReferenceLaneSpacingMeters(segmentType, geometry);
        if (!double.IsFinite(physicalMeters) || physicalMeters < 0d)
            throw new ArgumentOutOfRangeException(nameof(skills), "Movement skills must produce a finite execution value.");

        return (float)Math.Min(physicalMeters, float.MaxValue);
    }

    /// <summary>Compatibility result in normalized lane units for equal-width synthetic geometry.</summary>
    public static float CalculateMaxLateralDelta(
        float segmentTravelTimeSeconds,
        TrackGeometry geometry,
        TrackSurfaceState surface,
        RiderSkills skills)
    {
        TrackGeometry.RequireEqualWidthCompatibility(geometry);
        return CalculateMaxLateralDistanceMeters(
                segmentTravelTimeSeconds,
                SegmentType.Straight,
                geometry,
                surface,
                skills)
            / LaneModel.ReferenceLaneSpacingMeters(SegmentType.Straight, geometry);
    }

    public static float MoveTowards(
        float currentLateralPosition,
        int resolvedLane,
        float segmentTravelTimeSeconds,
        SegmentType segmentType,
        TrackGeometry geometry,
        TrackSurfaceState surface,
        RiderSkills skills)
    {
        ValidateLateralPosition(currentLateralPosition, nameof(currentLateralPosition));
        LaneModel.ValidateLane(resolvedLane);

        if (segmentTravelTimeSeconds == 0f)
            return currentLateralPosition;

        var maxDistance = CalculateMaxLateralDistanceMeters(
            segmentTravelTimeSeconds,
            segmentType,
            geometry,
            surface,
            skills);
        var currentOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            currentLateralPosition,
            segmentType,
            geometry);
        var targetOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            resolvedLane,
            segmentType,
            geometry);
        var difference = targetOffset - currentOffset;
        var appliedDistance = MathF.Min(MathF.Abs(difference), maxDistance);
        var nextOffset = currentOffset + MathF.CopySign(appliedDistance, difference);
        var next = LaneModel.LateralPositionFromPhysicalOffsetMeters(nextOffset, segmentType, geometry);
        ValidateLateralPosition(next, "result");
        return next;
    }

    /// <summary>Compatibility overload for equal-width synthetic geometry.</summary>
    public static float MoveTowards(
        float currentLateralPosition,
        int resolvedLane,
        float segmentTravelTimeSeconds,
        TrackGeometry geometry,
        TrackSurfaceState surface,
        RiderSkills skills)
    {
        TrackGeometry.RequireEqualWidthCompatibility(geometry);
        return MoveTowards(
            currentLateralPosition,
            resolvedLane,
            segmentTravelTimeSeconds,
            SegmentType.Straight,
            geometry,
            surface,
            skills);
    }

    /// <summary>
    /// Applies an outward physical displacement and caps it at a supplied
    /// continuous reference position within the track domain.
    /// </summary>
    public static float MoveOutwardByPhysicalDistance(
        float currentLateralPosition,
        float physicalDistanceMeters,
        float maximumLateralPosition,
        SegmentType segmentType,
        TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateLateralPosition(currentLateralPosition, nameof(currentLateralPosition));
        ValidateLateralPosition(maximumLateralPosition, nameof(maximumLateralPosition));
        if (!float.IsFinite(physicalDistanceMeters) || physicalDistanceMeters < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalDistanceMeters),
                physicalDistanceMeters,
                "Physical distance must be finite and non-negative.");
        }

        if (maximumLateralPosition <= currentLateralPosition)
            return currentLateralPosition;

        var currentOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            currentLateralPosition,
            segmentType,
            geometry);
        var maximumOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            maximumLateralPosition,
            segmentType,
            geometry);
        var nextOffset = Math.Min((double)currentOffset + physicalDistanceMeters, maximumOffset);
        var next = LaneModel.LateralPositionFromPhysicalOffsetMeters((float)nextOffset, segmentType, geometry);
        ValidateLateralPosition(next, "result");
        return next;
    }

    /// <summary>Compatibility overload for equal-width synthetic geometry.</summary>
    public static float MoveOutwardByPhysicalDistance(
        float currentLateralPosition,
        float physicalDistanceMeters,
        float maximumLateralPosition,
        TrackGeometry geometry)
    {
        TrackGeometry.RequireEqualWidthCompatibility(geometry);
        return MoveOutwardByPhysicalDistance(
            currentLateralPosition,
            physicalDistanceMeters,
            maximumLateralPosition,
            SegmentType.Straight,
            geometry);
    }

    public static void ValidateLateralPosition(float lateralPosition, string parameterName)
    {
        if (!float.IsFinite(lateralPosition)
            || lateralPosition < LaneModel.MinLane
            || lateralPosition > LaneModel.MaxLane)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                lateralPosition,
                $"Lateral position must be finite and between {LaneModel.MinLane} and {LaneModel.MaxLane} inclusive.");
        }
    }

    private static bool HasArrivedAtLane(
        float currentLateralPosition,
        int referenceLane,
        SegmentType segmentType,
        TrackGeometry geometry)
        => LateralSpaceModel.LateralDistanceMeters(
                currentLateralPosition,
                referenceLane,
                segmentType,
                geometry)
            <= LaneArrivalToleranceMeters;
}
