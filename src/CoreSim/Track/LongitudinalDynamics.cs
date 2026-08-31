using CoreSim.Setup;

namespace CoreSim;

/// <summary>
/// Deterministic traversal result for one advanced-physics straight. Distances
/// describe the acceleration, positive-drive-ceiling cruise and corner-entry
/// deceleration phases actually used.
/// </summary>
public readonly record struct StraightSpeedProfile(
    float ExitSpeedMetersPerSecond,
    float PeakSpeedMetersPerSecond,
    float TravelTimeSeconds,
    float AccelerationDistanceMeters,
    float CruiseDistanceMeters,
    float DecelerationDistanceMeters);

/// <summary>
/// Deterministic longitudinal motion helpers. The current acceleration values
/// are provisional first-model parameters, not final motorcycle performance data.
/// </summary>
public static class LongitudinalDynamics
{
    public const float MinTurnExitAccelerationMetersPerSecondSquared = 0.60f;
    public const float MaxTurnExitAccelerationMetersPerSecondSquared = 1.40f;
    public const float LowGearingDriveMultiplier = 1.10f;
    public const float HighGearingDriveMultiplier = 0.90f;
    public const float MinSurfaceDriveMultiplier = 0.75f;
    public const float SurfaceDriveMultiplierRange = 0.25f;
    public const float MinStraightAccelerationMetersPerSecondSquared = 0.80f;
    public const float MaxStraightAccelerationMetersPerSecondSquared = 1.60f;
    public const float MinCornerEntryDecelerationMetersPerSecondSquared = 2.00f;
    public const float MaxCornerEntryDecelerationMetersPerSecondSquared = 3.20f;

    // PROVISIONAL first-model parameters. These bound only positive drive;
    // they are not final speedway-motorcycle data or a hard speed limiter.
    public const float MinAttainableTopSpeedMetersPerSecond = 21.0f;
    public const float MaxAttainableTopSpeedMetersPerSecond = 25.0f;
    public const float LowGearingTopSpeedMultiplier = 0.94f;
    public const float HighGearingTopSpeedMultiplier = 1.06f;

    public static float AccelerateOverDistance(
        float initialSpeedMetersPerSecond,
        float accelerationMetersPerSecondSquared,
        float distanceMeters)
    {
        ValidateNonNegativeFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));
        ValidateNonNegativeFinite(
            accelerationMetersPerSecondSquared,
            nameof(accelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));

        if (accelerationMetersPerSecondSquared == 0f || distanceMeters == 0f)
            return initialSpeedMetersPerSecond;

        var finalSpeedSquared = (double)initialSpeedMetersPerSecond * initialSpeedMetersPerSecond
            + 2d * accelerationMetersPerSecondSquared * distanceMeters;
        var finalSpeed = (float)Math.Sqrt(finalSpeedSquared);
        if (!float.IsFinite(finalSpeed))
            throw new OverflowException("Final speed exceeds the finite single-precision domain.");
        return finalSpeed;
    }

    public static float DecelerateOverDistance(
        float initialSpeedMetersPerSecond,
        float decelerationMetersPerSecondSquared,
        float distanceMeters)
    {
        ValidateNonNegativeFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));
        ValidateNonNegativeFinite(
            decelerationMetersPerSecondSquared,
            nameof(decelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));

        if (decelerationMetersPerSecondSquared == 0f || distanceMeters == 0f)
            return initialSpeedMetersPerSecond;

        var finalSpeedSquared = Math.Max(
            0d,
            (double)initialSpeedMetersPerSecond * initialSpeedMetersPerSecond
            - 2d * decelerationMetersPerSecondSquared * distanceMeters);
        return (float)Math.Sqrt(finalSpeedSquared);
    }

    public static float AccelerateOverDistanceWithSpeedCeiling(
        float initialSpeedMetersPerSecond,
        float accelerationMetersPerSecondSquared,
        float distanceMeters,
        float speedCeilingMetersPerSecond)
    {
        ValidateNonNegativeFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));
        ValidateNonNegativeFinite(
            accelerationMetersPerSecondSquared,
            nameof(accelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));
        ValidatePositiveFinite(speedCeilingMetersPerSecond, nameof(speedCeilingMetersPerSecond));

        if (initialSpeedMetersPerSecond >= speedCeilingMetersPerSecond
            || accelerationMetersPerSecondSquared == 0f
            || distanceMeters == 0f)
        {
            return initialSpeedMetersPerSecond;
        }

        var candidateSpeedSquared =
            (double)initialSpeedMetersPerSecond * initialSpeedMetersPerSecond
            + 2d * accelerationMetersPerSecondSquared * distanceMeters;
        var candidateSpeed = Math.Sqrt(candidateSpeedSquared);
        return (float)Math.Min(candidateSpeed, speedCeilingMetersPerSecond);
    }

    public static float CalculateAttainableTopSpeedMetersPerSecond(
        RiderSkills skills,
        BikeSetup setup)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);

        var speedSkill = RiderSkills.Normalize(skills.Speed);
        var baseTopSpeed = MinAttainableTopSpeedMetersPerSecond
            + (MaxAttainableTopSpeedMetersPerSecond
               - MinAttainableTopSpeedMetersPerSecond) * speedSkill;
        var gearingTopSpeedMultiplier = LowGearingTopSpeedMultiplier
            + (HighGearingTopSpeedMultiplier - LowGearingTopSpeedMultiplier) * setup.Gearing;
        var attainableTopSpeed = baseTopSpeed * gearingTopSpeedMultiplier;
        ValidatePositiveFinite(attainableTopSpeed, "result");
        return attainableTopSpeed;
    }

    public static float CalculateTurnExitAccelerationMetersPerSecondSquared(
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);

        var speedSkill = RiderSkills.Normalize(skills.Speed);
        var baseAcceleration = MinTurnExitAccelerationMetersPerSecondSquared
            + (MaxTurnExitAccelerationMetersPerSecondSquared
               - MinTurnExitAccelerationMetersPerSecondSquared) * speedSkill;
        var gearingDriveMultiplier = LowGearingDriveMultiplier
            + (HighGearingDriveMultiplier - LowGearingDriveMultiplier) * setup.Gearing;
        var surfaceDriveMultiplier = MinSurfaceDriveMultiplier
            + SurfaceDriveMultiplierRange * surface.EffectiveGrip;
        var acceleration = baseAcceleration * gearingDriveMultiplier * surfaceDriveMultiplier;
        ValidateNonNegativeFinite(acceleration, "result");
        return acceleration;
    }

    public static float CalculateStraightAccelerationMetersPerSecondSquared(
        RiderSkills skills,
        TrackSurfaceState surface)
    {
        ArgumentNullException.ThrowIfNull(skills);

        var speedSkill = RiderSkills.Normalize(skills.Speed);
        var baseAcceleration = MinStraightAccelerationMetersPerSecondSquared
            + (MaxStraightAccelerationMetersPerSecondSquared
               - MinStraightAccelerationMetersPerSecondSquared) * speedSkill;
        var surfaceDriveMultiplier = MinSurfaceDriveMultiplier
            + SurfaceDriveMultiplierRange * surface.EffectiveGrip;
        var acceleration = baseAcceleration * surfaceDriveMultiplier;
        ValidateNonNegativeFinite(acceleration, "result");
        return acceleration;
    }

    public static float CalculateCornerEntryDecelerationMetersPerSecondSquared(
        RiderSkills skills,
        TrackSurfaceState surface)
    {
        ArgumentNullException.ThrowIfNull(skills);

        var control = RiderSkills.Normalize(skills.SlideControl);
        var baseDeceleration = MinCornerEntryDecelerationMetersPerSecondSquared
            + (MaxCornerEntryDecelerationMetersPerSecondSquared
               - MinCornerEntryDecelerationMetersPerSecondSquared) * control;
        var surfaceMultiplier = MinSurfaceDriveMultiplier
            + SurfaceDriveMultiplierRange * surface.EffectiveGrip;
        var deceleration = baseDeceleration * surfaceMultiplier;
        ValidateNonNegativeFinite(deceleration, "result");
        return deceleration;
    }

    public static StraightSpeedProfile CalculateStraightSpeedProfile(
        float initialSpeedMetersPerSecond,
        float accelerationMetersPerSecondSquared,
        float cornerEntryDecelerationMetersPerSecondSquared,
        float distanceMeters,
        float speedCeilingMetersPerSecond,
        float? targetExitSpeedMetersPerSecond = null)
    {
        ValidateNonNegativeFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));
        ValidatePositiveFinite(
            accelerationMetersPerSecondSquared,
            nameof(accelerationMetersPerSecondSquared));
        ValidatePositiveFinite(
            cornerEntryDecelerationMetersPerSecondSquared,
            nameof(cornerEntryDecelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));
        ValidatePositiveFinite(speedCeilingMetersPerSecond, nameof(speedCeilingMetersPerSecond));
        if (targetExitSpeedMetersPerSecond is { } target)
            ValidateNonNegativeFinite(target, nameof(targetExitSpeedMetersPerSecond));

        if (distanceMeters == 0f)
        {
            return CreateProfile(
                initialSpeedMetersPerSecond,
                initialSpeedMetersPerSecond,
                initialSpeedMetersPerSecond,
                0f,
                0f,
                0f);
        }

        var fullDriveProfile = CreateFullDriveProfile(
            initialSpeedMetersPerSecond,
            accelerationMetersPerSecondSquared,
            distanceMeters,
            speedCeilingMetersPerSecond);
        if (targetExitSpeedMetersPerSecond is not { } targetExitSpeed
            || fullDriveProfile.ExitSpeedMetersPerSecond <= targetExitSpeed)
        {
            return fullDriveProfile;
        }

        if (initialSpeedMetersPerSecond > targetExitSpeed)
        {
            var requiredDecelerationDistance =
                ((double)initialSpeedMetersPerSecond * initialSpeedMetersPerSecond
                 - (double)targetExitSpeed * targetExitSpeed)
                / (2d * cornerEntryDecelerationMetersPerSecondSquared);
            if (requiredDecelerationDistance >= distanceMeters)
            {
                var exitSpeed = DecelerateOverDistance(
                    initialSpeedMetersPerSecond,
                    cornerEntryDecelerationMetersPerSecondSquared,
                    distanceMeters);
                return CreateProfile(
                    initialSpeedMetersPerSecond,
                    initialSpeedMetersPerSecond,
                    exitSpeed,
                    0f,
                    0f,
                    distanceMeters);
            }
        }

        var initialSpeedSquared = (double)initialSpeedMetersPerSecond * initialSpeedMetersPerSecond;
        var targetSpeedSquared = (double)targetExitSpeed * targetExitSpeed;
        var peakSpeedSquared =
            (2d * accelerationMetersPerSecondSquared
             * cornerEntryDecelerationMetersPerSecondSquared
             * distanceMeters
             + cornerEntryDecelerationMetersPerSecondSquared * initialSpeedSquared
             + accelerationMetersPerSecondSquared * targetSpeedSquared)
            / (accelerationMetersPerSecondSquared
               + cornerEntryDecelerationMetersPerSecondSquared);
        var analyticPeakSpeed = Math.Sqrt(peakSpeedSquared);
        var maxPositiveDrivePeak = initialSpeedMetersPerSecond < speedCeilingMetersPerSecond
            ? speedCeilingMetersPerSecond
            : initialSpeedMetersPerSecond;

        if (analyticPeakSpeed <= maxPositiveDrivePeak)
        {
            var accelerationDistance = (float)(
                (peakSpeedSquared - initialSpeedSquared)
                / (2d * accelerationMetersPerSecondSquared));
            var decelerationDistance = (float)(
                (peakSpeedSquared - targetSpeedSquared)
                / (2d * cornerEntryDecelerationMetersPerSecondSquared));

            return CreateProfile(
                initialSpeedMetersPerSecond,
                (float)analyticPeakSpeed,
                targetExitSpeed,
                accelerationDistance,
                0f,
                decelerationDistance);
        }

        var cappedPeakSpeedSquared = (double)maxPositiveDrivePeak * maxPositiveDrivePeak;
        var cappedAccelerationDistance = maxPositiveDrivePeak > initialSpeedMetersPerSecond
            ? (cappedPeakSpeedSquared - initialSpeedSquared)
              / (2d * accelerationMetersPerSecondSquared)
            : 0d;
        var cappedDecelerationDistance = maxPositiveDrivePeak > targetExitSpeed
            ? (cappedPeakSpeedSquared - targetSpeedSquared)
              / (2d * cornerEntryDecelerationMetersPerSecondSquared)
            : 0d;
        var cruiseDistance = ResolveCruiseDistance(
            distanceMeters,
            cappedAccelerationDistance,
            cappedDecelerationDistance);

        return CreateProfile(
            initialSpeedMetersPerSecond,
            maxPositiveDrivePeak,
            targetExitSpeed,
            (float)cappedAccelerationDistance,
            (float)cruiseDistance,
            (float)cappedDecelerationDistance);
    }

    private static StraightSpeedProfile CreateFullDriveProfile(
        float initialSpeedMetersPerSecond,
        float accelerationMetersPerSecondSquared,
        float distanceMeters,
        float speedCeilingMetersPerSecond)
    {
        if (initialSpeedMetersPerSecond >= speedCeilingMetersPerSecond)
        {
            return CreateProfile(
                initialSpeedMetersPerSecond,
                initialSpeedMetersPerSecond,
                initialSpeedMetersPerSecond,
                0f,
                distanceMeters,
                0f);
        }

        var distanceToCeiling =
            ((double)speedCeilingMetersPerSecond * speedCeilingMetersPerSecond
             - (double)initialSpeedMetersPerSecond * initialSpeedMetersPerSecond)
            / (2d * accelerationMetersPerSecondSquared);
        if (distanceToCeiling >= distanceMeters)
        {
            var exitSpeed = AccelerateOverDistanceWithSpeedCeiling(
                initialSpeedMetersPerSecond,
                accelerationMetersPerSecondSquared,
                distanceMeters,
                speedCeilingMetersPerSecond);
            return CreateProfile(
                initialSpeedMetersPerSecond,
                exitSpeed,
                exitSpeed,
                distanceMeters,
                0f,
                0f);
        }

        return CreateProfile(
            initialSpeedMetersPerSecond,
            speedCeilingMetersPerSecond,
            speedCeilingMetersPerSecond,
            (float)distanceToCeiling,
            (float)(distanceMeters - distanceToCeiling),
            0f);
    }

    private static StraightSpeedProfile CreateProfile(
        float initialSpeedMetersPerSecond,
        float peakSpeedMetersPerSecond,
        float exitSpeedMetersPerSecond,
        float accelerationDistanceMeters,
        float cruiseDistanceMeters,
        float decelerationDistanceMeters)
    {
        var accelerationTime = CalculatePhaseTimeSeconds(
            accelerationDistanceMeters,
            initialSpeedMetersPerSecond,
            peakSpeedMetersPerSecond);
        var cruiseTime = CalculateCruiseTimeSeconds(
            cruiseDistanceMeters,
            peakSpeedMetersPerSecond);
        var decelerationTime = CalculatePhaseTimeSeconds(
            decelerationDistanceMeters,
            peakSpeedMetersPerSecond,
            exitSpeedMetersPerSecond);
        var travelTime = accelerationTime + cruiseTime + decelerationTime;
        if (!float.IsFinite(travelTime))
            throw new OverflowException("Straight travel time exceeds the finite single-precision domain.");

        return new StraightSpeedProfile(
            exitSpeedMetersPerSecond,
            peakSpeedMetersPerSecond,
            travelTime,
            accelerationDistanceMeters,
            cruiseDistanceMeters,
            decelerationDistanceMeters);
    }

    private static double ResolveCruiseDistance(
        double totalDistanceMeters,
        double accelerationDistanceMeters,
        double decelerationDistanceMeters)
    {
        var cruiseDistance = totalDistanceMeters
            - accelerationDistanceMeters
            - decelerationDistanceMeters;
        var tolerance = Math.Max(1e-9d, totalDistanceMeters * 1e-7d);
        if (cruiseDistance < -tolerance)
        {
            throw new InvalidOperationException(
                "Straight phase distances exceed the available distance.");
        }

        return cruiseDistance < 0d ? 0d : cruiseDistance;
    }

    private static float CalculateCruiseTimeSeconds(
        float distanceMeters,
        float speedMetersPerSecond)
    {
        if (distanceMeters == 0f)
            return 0f;
        if (speedMetersPerSecond <= 0f)
            throw new InvalidOperationException("A positive cruise distance requires positive speed.");
        return distanceMeters / speedMetersPerSecond;
    }

    private static float CalculatePhaseTimeSeconds(
        float distanceMeters,
        float startSpeedMetersPerSecond,
        float endSpeedMetersPerSecond)
    {
        if (distanceMeters == 0f)
            return 0f;

        var speedSum = startSpeedMetersPerSecond + endSpeedMetersPerSecond;
        if (speedSum <= 0f)
            throw new InvalidOperationException("A positive distance cannot be traversed at zero speed.");
        return 2f * distanceMeters / speedSum;
    }

    private static void ValidateNonNegativeFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be finite and non-negative.");
        }
    }

    private static void ValidatePositiveFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be finite and positive.");
        }
    }
}
