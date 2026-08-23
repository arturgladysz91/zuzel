namespace CoreSim;

/// <summary>
/// Time-based execution of movement between the track's discrete reference lanes.
/// Lateral positions and deltas use lane units; physical distances use meters.
/// </summary>
public static class LateralMovementModel
{
    /// <summary>Provisional minimum physical lateral speed for a rider with no execution skill.</summary>
    public const float MinLateralSpeedMetersPerSecond = 0.35f;

    /// <summary>Provisional maximum physical lateral speed for a rider with full execution skill.</summary>
    public const float MaxLateralSpeedMetersPerSecond = 0.65f;

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
            if (HasArrivedAtLane(currentLateralPosition, nearestOuterLane, geometry))
                nearestOuterLane++;

            return LaneModel.ClampLane(Math.Min(nearestOuterLane, targetLane));
        }

        if (targetLane < currentLateralPosition)
        {
            var nearestInnerLane = (int)MathF.Floor(currentLateralPosition);
            if (HasArrivedAtLane(currentLateralPosition, nearestInnerLane, geometry))
                nearestInnerLane--;

            return LaneModel.ClampLane(Math.Max(nearestInnerLane, targetLane));
        }

        return targetLane;
    }

    public static float CalculateMaxLateralDelta(
        float segmentTravelTimeSeconds,
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

        var slideControl = RiderSkills.Normalize(skills.SlideControl);
        var adaptability = RiderSkills.Normalize(skills.Adaptability);
        var execution = SlideControlExecutionWeight * slideControl
            + AdaptabilityExecutionWeight * adaptability;
        var lateralSpeedMetersPerSecond = MinLateralSpeedMetersPerSecond
            + (MaxLateralSpeedMetersPerSecond - MinLateralSpeedMetersPerSecond) * execution;
        var effectiveGrip = surface.EffectiveGrip;
        if (!float.IsFinite(effectiveGrip))
            throw new ArgumentOutOfRangeException(nameof(surface), "Effective grip must be finite.");

        var gripMultiplier = MinGripMultiplier + EffectiveGripMultiplierRange * effectiveGrip;
        var maxDelta = (double)lateralSpeedMetersPerSecond
            * segmentTravelTimeSeconds
            * gripMultiplier
            / geometry.LaneSpacingMeters;
        if (double.IsNaN(maxDelta) || maxDelta < 0d)
            throw new ArgumentOutOfRangeException(nameof(skills), "Movement skills must produce a finite execution value.");

        return (float)Math.Min(maxDelta, float.MaxValue);
    }

    public static float MoveTowards(
        float currentLateralPosition,
        int resolvedLane,
        float segmentTravelTimeSeconds,
        TrackGeometry geometry,
        TrackSurfaceState surface,
        RiderSkills skills)
    {
        ValidateLateralPosition(currentLateralPosition, nameof(currentLateralPosition));
        LaneModel.ValidateLane(resolvedLane);

        if (segmentTravelTimeSeconds == 0f)
            return currentLateralPosition;

        var maxDelta = CalculateMaxLateralDelta(segmentTravelTimeSeconds, geometry, surface, skills);
        var difference = resolvedLane - currentLateralPosition;
        var appliedDelta = MathF.Min(MathF.Abs(difference), maxDelta);
        var next = currentLateralPosition + MathF.CopySign(appliedDelta, difference);
        next = Math.Clamp(next, LaneModel.MinLane, LaneModel.MaxLane);
        ValidateLateralPosition(next, "result");
        return next;
    }

    /// <summary>
    /// Applies an outward physical displacement and caps it at a supplied
    /// continuous reference position within the track domain.
    /// </summary>
    public static float MoveOutwardByPhysicalDistance(
        float currentLateralPosition,
        float physicalDistanceMeters,
        float maximumLateralPosition,
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

        var deltaLaneUnits = (double)physicalDistanceMeters / geometry.LaneSpacingMeters;
        var next = (float)Math.Min(currentLateralPosition + deltaLaneUnits, maximumLateralPosition);
        next = Math.Clamp(next, LaneModel.MinLane, LaneModel.MaxLane);
        ValidateLateralPosition(next, "result");
        return next;
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
        TrackGeometry geometry)
        => MathF.Abs(referenceLane - currentLateralPosition) * geometry.LaneSpacingMeters
            <= LaneArrivalToleranceMeters;
}
