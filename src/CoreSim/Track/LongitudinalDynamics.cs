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
/// Deterministic positive-drive traversal of one eligible advanced TurnExit.
/// The entry acceleration is diagnostic only; traversal evaluates acceleration
/// independently at every shared longitudinal midpoint step.
/// </summary>
public readonly record struct TurnExitDriveProfile(
    float ExitSpeedMetersPerSecond,
    float PeakSpeedMetersPerSecond,
    float TravelTimeSeconds,
    float AccelerationDistanceMeters,
    float CruiseDistanceMeters,
    float EntryNetAccelerationMetersPerSecondSquared);

/// <summary>
/// Deterministic first-phase traversal of an advanced TurnEntry. The exit
/// speed is resolved before the residual corner constraint is applied.
/// </summary>
public readonly record struct TurnEntryScrubProfile(
    float ExitSpeedMetersPerSecond,
    float TravelTimeSeconds,
    float DecelerationDistanceMeters,
    float CarryDistanceMeters);

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

    // PROVISIONAL / NOT REAL-WORLD CALIBRATED GAME MODEL INPUTS. These
    // values provide only the force-based positive-drive foundation shared by
    // TurnExit and Straight; mass is nominal system mass, not a rider attribute.
    public const float ProvisionalNominalSystemMassKilograms = 142f;
    public const float ProvisionalBaseResistanceForceNewtons = 40f;
    public const float ProvisionalQuadraticResistanceCoefficient = 0.20f;
    public const float ProvisionalPositiveDriveReferenceSpeedMetersPerSecond = 16f;
    public const float ProvisionalDriveOrientedForceFadePerMeterPerSecond = 0.0175f;
    public const float ProvisionalSpeedOrientedForceFadePerMeterPerSecond = 0.0050f;

    // PROVISIONAL / NUMERICAL INTEGRATION RESOLUTION. This is not a gameplay
    // parameter; it bounds each deterministic longitudinal distance step.
    public const float ProvisionalLongitudinalIntegrationStepMeters = 1f;
    public const float ProvisionalStraightIntegrationStepMeters =
        ProvisionalLongitudinalIntegrationStepMeters;

    private const float LongitudinalPhaseSpeedToleranceMetersPerSecond = 1e-6f;

    // PROVISIONAL / NOT REAL-WORLD CALIBRATED coarse phase split. The first
    // half of the actually remaining TurnEntry represents setting, roll-off,
    // slide entry and speed scrub rather than conventional mechanical braking.
    public const float ProvisionalTurnEntryScrubDistanceFraction = 0.50f;

    // PROVISIONAL / NOT REAL-WORLD CALIBRATED first-model parameters.
    // These bound only positive drive; they are not telemetry-derived final
    // speedway-motorcycle speeds or a hard limiter for existing overspeed.
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

    public static float CalculateLongitudinalResistanceForceNewtons(
        float speedMetersPerSecond)
    {
        ValidateNonNegativeFinite(speedMetersPerSecond, nameof(speedMetersPerSecond));

        var resistanceForceNewtons =
            ProvisionalBaseResistanceForceNewtons
            + ProvisionalQuadraticResistanceCoefficient
              * (double)speedMetersPerSecond * speedMetersPerSecond;
        if (!double.IsFinite(resistanceForceNewtons)
            || resistanceForceNewtons > float.MaxValue)
        {
            throw new OverflowException(
                "Longitudinal resistance exceeds the finite single-precision domain.");
        }

        var result = (float)resistanceForceNewtons;
        ValidatePositiveFinite(result, "result");
        return result;
    }

    public static float CalculateAccelerationFromForcesMetersPerSecondSquared(
        float availableDriveForceNewtons,
        float resistanceForceNewtons,
        float systemMassKilograms)
    {
        ValidateNonNegativeFinite(
            availableDriveForceNewtons,
            nameof(availableDriveForceNewtons));
        ValidateNonNegativeFinite(resistanceForceNewtons, nameof(resistanceForceNewtons));
        ValidatePositiveFinite(systemMassKilograms, nameof(systemMassKilograms));

        var positiveDriveNetForceNewtons = Math.Max(
            0d,
            (double)availableDriveForceNewtons - resistanceForceNewtons);
        var accelerationMetersPerSecondSquared =
            positiveDriveNetForceNewtons / systemMassKilograms;
        if (!double.IsFinite(accelerationMetersPerSecondSquared)
            || accelerationMetersPerSecondSquared > float.MaxValue)
        {
            throw new OverflowException(
                "Acceleration exceeds the finite single-precision domain.");
        }

        return (float)accelerationMetersPerSecondSquared;
    }

    public static float CalculateTurnExitAvailableDriveForceNewtons(
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        var referenceAcceleration = CalculateTurnExitAccelerationMetersPerSecondSquared(
            skills,
            setup,
            surface);
        var referenceResistance = CalculateLongitudinalResistanceForceNewtons(
            ProvisionalPositiveDriveReferenceSpeedMetersPerSecond);
        var availableDriveForceNewtons =
            ProvisionalNominalSystemMassKilograms * referenceAcceleration
            + referenceResistance;
        ValidateNonNegativeFinite(availableDriveForceNewtons, "result");
        return availableDriveForceNewtons;
    }

    public static float CalculatePositiveDriveEnvelopeMultiplier(
        float speedMetersPerSecond,
        BikeSetup setup)
    {
        ValidateNonNegativeFinite(speedMetersPerSecond, nameof(speedMetersPerSecond));
        ArgumentNullException.ThrowIfNull(setup);

        if (speedMetersPerSecond <= ProvisionalPositiveDriveReferenceSpeedMetersPerSecond)
            return 1f;

        var fadeRatePerMeterPerSecond =
            ProvisionalDriveOrientedForceFadePerMeterPerSecond
            + (ProvisionalSpeedOrientedForceFadePerMeterPerSecond
               - ProvisionalDriveOrientedForceFadePerMeterPerSecond)
              * (double)setup.Gearing;
        var excessSpeedMetersPerSecond =
            (double)speedMetersPerSecond
            - ProvisionalPositiveDriveReferenceSpeedMetersPerSecond;
        var envelope = Math.Clamp(
            1d - fadeRatePerMeterPerSecond * excessSpeedMetersPerSecond,
            0d,
            1d);
        var result = (float)envelope;
        ValidateNonNegativeFinite(result, "result");
        return result;
    }

    public static float CalculateTurnExitDriveEnvelopeMultiplier(
        float speedMetersPerSecond,
        BikeSetup setup)
        => CalculatePositiveDriveEnvelopeMultiplier(speedMetersPerSecond, setup);

    public static float CalculateAvailableDriveForceAtSpeedNewtons(
        float referenceAvailableDriveForceNewtons,
        float speedMetersPerSecond,
        BikeSetup setup)
    {
        ValidateNonNegativeFinite(
            referenceAvailableDriveForceNewtons,
            nameof(referenceAvailableDriveForceNewtons));
        var envelope = CalculatePositiveDriveEnvelopeMultiplier(speedMetersPerSecond, setup);
        var availableDriveForceAtSpeedNewtons =
            (double)referenceAvailableDriveForceNewtons * envelope;
        if (!double.IsFinite(availableDriveForceAtSpeedNewtons)
            || availableDriveForceAtSpeedNewtons > float.MaxValue)
        {
            throw new OverflowException(
                "Available drive force exceeds the finite single-precision domain.");
        }

        var result = (float)availableDriveForceAtSpeedNewtons;
        ValidateNonNegativeFinite(result, "result");
        return result;
    }

    public static float CalculateTurnExitAvailableDriveForceAtSpeedNewtons(
        float speedMetersPerSecond,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        var referenceAvailableDriveForceNewtons =
            CalculateTurnExitAvailableDriveForceNewtons(skills, setup, surface);
        return CalculateAvailableDriveForceAtSpeedNewtons(
            referenceAvailableDriveForceNewtons,
            speedMetersPerSecond,
            setup);
    }

    public static float CalculateTurnExitNetAccelerationMetersPerSecondSquared(
        float speedMetersPerSecond,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        var referenceAvailableDriveForceNewtons =
            CalculateTurnExitAvailableDriveForceNewtons(skills, setup, surface);
        return CalculateNetPositiveDriveAccelerationMetersPerSecondSquared(
            speedMetersPerSecond,
            referenceAvailableDriveForceNewtons,
            setup);
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

    public static float CalculateStraightReferenceDriveAccelerationMetersPerSecondSquared(
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var straightAcceleration = CalculateStraightAccelerationMetersPerSecondSquared(
            skills,
            surface);
        var gearingDriveMultiplier = LowGearingDriveMultiplier
            + (HighGearingDriveMultiplier - LowGearingDriveMultiplier) * setup.Gearing;
        var referenceAcceleration = straightAcceleration * gearingDriveMultiplier;
        ValidateNonNegativeFinite(referenceAcceleration, "result");
        return referenceAcceleration;
    }

    public static float CalculateStraightAvailableDriveForceNewtons(
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        var referenceAcceleration =
            CalculateStraightReferenceDriveAccelerationMetersPerSecondSquared(
                skills,
                setup,
                surface);
        var referenceResistance = CalculateLongitudinalResistanceForceNewtons(
            ProvisionalPositiveDriveReferenceSpeedMetersPerSecond);
        var referenceAvailableForceNewtons =
            ProvisionalNominalSystemMassKilograms * referenceAcceleration
            + referenceResistance;
        ValidateNonNegativeFinite(referenceAvailableForceNewtons, "result");
        return referenceAvailableForceNewtons;
    }

    public static float CalculateStraightNetAccelerationMetersPerSecondSquared(
        float speedMetersPerSecond,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        var referenceAvailableForceNewtons = CalculateStraightAvailableDriveForceNewtons(
            skills,
            setup,
            surface);
        return CalculateNetPositiveDriveAccelerationMetersPerSecondSquared(
            speedMetersPerSecond,
            referenceAvailableForceNewtons,
            setup);
    }

    public static float CalculateNetPositiveDriveAccelerationMetersPerSecondSquared(
        float speedMetersPerSecond,
        float referenceAvailableDriveForceNewtons,
        BikeSetup setup)
    {
        ValidateNonNegativeFinite(speedMetersPerSecond, nameof(speedMetersPerSecond));
        ValidateNonNegativeFinite(
            referenceAvailableDriveForceNewtons,
            nameof(referenceAvailableDriveForceNewtons));
        ArgumentNullException.ThrowIfNull(setup);

        var availableDriveForceNewtons = CalculateAvailableDriveForceAtSpeedNewtons(
            referenceAvailableDriveForceNewtons,
            speedMetersPerSecond,
            setup);
        var resistanceForceNewtons = CalculateLongitudinalResistanceForceNewtons(
            speedMetersPerSecond);
        return CalculateAccelerationFromForcesMetersPerSecondSquared(
            availableDriveForceNewtons,
            resistanceForceNewtons,
            ProvisionalNominalSystemMassKilograms);
    }

    public static float CalculateMidpointPositiveDriveEndSpeedMetersPerSecond(
        float currentSpeedMetersPerSecond,
        float stepDistanceMeters,
        float speedCeilingMetersPerSecond,
        float referenceAvailableDriveForceNewtons,
        BikeSetup setup)
    {
        ValidateNonNegativeFinite(
            currentSpeedMetersPerSecond,
            nameof(currentSpeedMetersPerSecond));
        ValidateNonNegativeFinite(stepDistanceMeters, nameof(stepDistanceMeters));
        ValidatePositiveFinite(speedCeilingMetersPerSecond, nameof(speedCeilingMetersPerSecond));
        ValidateNonNegativeFinite(
            referenceAvailableDriveForceNewtons,
            nameof(referenceAvailableDriveForceNewtons));
        ArgumentNullException.ThrowIfNull(setup);

        var accelerationAtStart = CalculateNetPositiveDriveAccelerationMetersPerSecondSquared(
            currentSpeedMetersPerSecond,
            referenceAvailableDriveForceNewtons,
            setup);
        var predictedSpeedMetersPerSecond = AccelerateOverDistanceWithSpeedCeiling(
            currentSpeedMetersPerSecond,
            accelerationAtStart,
            stepDistanceMeters,
            speedCeilingMetersPerSecond);
        var midpointSpeedMetersPerSecond = (float)(
            ((double)currentSpeedMetersPerSecond + predictedSpeedMetersPerSecond) * 0.5d);
        var accelerationAtMidpoint = CalculateNetPositiveDriveAccelerationMetersPerSecondSquared(
            midpointSpeedMetersPerSecond,
            referenceAvailableDriveForceNewtons,
            setup);
        return AccelerateOverDistanceWithSpeedCeiling(
            currentSpeedMetersPerSecond,
            accelerationAtMidpoint,
            stepDistanceMeters,
            speedCeilingMetersPerSecond);
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

    public static float CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
        float settledTargetSpeedMetersPerSecond,
        float decelerationMetersPerSecondSquared,
        float availableTurnEntryDistanceMeters)
    {
        ValidateNonNegativeFinite(
            settledTargetSpeedMetersPerSecond,
            nameof(settledTargetSpeedMetersPerSecond));
        ValidatePositiveFinite(
            decelerationMetersPerSecondSquared,
            nameof(decelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(
            availableTurnEntryDistanceMeters,
            nameof(availableTurnEntryDistanceMeters));

        var scrubDistanceMeters = (double)availableTurnEntryDistanceMeters
            * ProvisionalTurnEntryScrubDistanceFraction;
        var maximumApproachSpeedSquared =
            (double)settledTargetSpeedMetersPerSecond * settledTargetSpeedMetersPerSecond
            + 2d * decelerationMetersPerSecondSquared * scrubDistanceMeters;
        var maximumApproachSpeed = (float)Math.Sqrt(maximumApproachSpeedSquared);
        if (!float.IsFinite(maximumApproachSpeed))
        {
            throw new OverflowException(
                "Maximum TurnEntry approach speed exceeds the finite single-precision domain.");
        }

        return maximumApproachSpeed;
    }

    public static TurnEntryScrubProfile CalculateTurnEntryScrubProfile(
        float initialSpeedMetersPerSecond,
        float settledTargetSpeedMetersPerSecond,
        float decelerationMetersPerSecondSquared,
        float availableTurnEntryDistanceMeters)
    {
        ValidateNonNegativeFinite(
            initialSpeedMetersPerSecond,
            nameof(initialSpeedMetersPerSecond));
        ValidateNonNegativeFinite(
            settledTargetSpeedMetersPerSecond,
            nameof(settledTargetSpeedMetersPerSecond));
        ValidatePositiveFinite(
            decelerationMetersPerSecondSquared,
            nameof(decelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(
            availableTurnEntryDistanceMeters,
            nameof(availableTurnEntryDistanceMeters));

        var scrubDistanceMeters = availableTurnEntryDistanceMeters
            * ProvisionalTurnEntryScrubDistanceFraction;
        if (scrubDistanceMeters == 0f)
        {
            return new TurnEntryScrubProfile(
                initialSpeedMetersPerSecond,
                0f,
                0f,
                0f);
        }

        if (initialSpeedMetersPerSecond <= settledTargetSpeedMetersPerSecond)
        {
            var carryTimeSeconds = CalculateCruiseTimeSeconds(
                scrubDistanceMeters,
                initialSpeedMetersPerSecond);
            return new TurnEntryScrubProfile(
                initialSpeedMetersPerSecond,
                carryTimeSeconds,
                0f,
                scrubDistanceMeters);
        }

        var requiredDecelerationDistanceMeters =
            ((double)initialSpeedMetersPerSecond * initialSpeedMetersPerSecond
             - (double)settledTargetSpeedMetersPerSecond * settledTargetSpeedMetersPerSecond)
            / (2d * decelerationMetersPerSecondSquared);
        if (requiredDecelerationDistanceMeters >= scrubDistanceMeters)
        {
            var exitSpeedMetersPerSecond = DecelerateOverDistance(
                initialSpeedMetersPerSecond,
                decelerationMetersPerSecondSquared,
                scrubDistanceMeters);
            var travelTimeSeconds = CalculatePhaseTimeSeconds(
                scrubDistanceMeters,
                initialSpeedMetersPerSecond,
                exitSpeedMetersPerSecond);
            return new TurnEntryScrubProfile(
                exitSpeedMetersPerSecond,
                travelTimeSeconds,
                scrubDistanceMeters,
                0f);
        }

        var decelerationDistanceMeters = (float)requiredDecelerationDistanceMeters;
        var carryDistanceMeters = scrubDistanceMeters - decelerationDistanceMeters;
        var decelerationTimeSeconds = CalculatePhaseTimeSeconds(
            decelerationDistanceMeters,
            initialSpeedMetersPerSecond,
            settledTargetSpeedMetersPerSecond);
        var carryTimeSecondsAtTarget = CalculateCruiseTimeSeconds(
            carryDistanceMeters,
            settledTargetSpeedMetersPerSecond);
        var travelTimeSecondsAtTarget = decelerationTimeSeconds + carryTimeSecondsAtTarget;
        if (!float.IsFinite(travelTimeSecondsAtTarget))
        {
            throw new OverflowException(
                "TurnEntry scrub travel time exceeds the finite single-precision domain.");
        }

        return new TurnEntryScrubProfile(
            settledTargetSpeedMetersPerSecond,
            travelTimeSecondsAtTarget,
            decelerationDistanceMeters,
            carryDistanceMeters);
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

    public static StraightSpeedProfile CalculateForceBasedStraightSpeedProfile(
        float initialSpeedMetersPerSecond,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface,
        float cornerEntryDecelerationMetersPerSecondSquared,
        float distanceMeters,
        float speedCeilingMetersPerSecond,
        float? targetExitSpeedMetersPerSecond = null)
    {
        ValidateNonNegativeFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);
        ValidatePositiveFinite(
            cornerEntryDecelerationMetersPerSecondSquared,
            nameof(cornerEntryDecelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));
        ValidatePositiveFinite(speedCeilingMetersPerSecond, nameof(speedCeilingMetersPerSecond));
        if (targetExitSpeedMetersPerSecond is { } target)
            ValidateNonNegativeFinite(target, nameof(targetExitSpeedMetersPerSecond));

        if (distanceMeters == 0f)
        {
            return new StraightSpeedProfile(
                initialSpeedMetersPerSecond,
                initialSpeedMetersPerSecond,
                0f,
                0f,
                0f,
                0f);
        }

        var integrationStepsMeters = CreateLongitudinalIntegrationSteps(distanceMeters);
        var allowedSpeedEnvelope = targetExitSpeedMetersPerSecond is { } targetSpeed
            ? CreateBackwardAllowedSpeedEnvelope(
                integrationStepsMeters,
                targetSpeed,
                cornerEntryDecelerationMetersPerSecondSquared)
            : null;

        var currentSpeedMetersPerSecond = initialSpeedMetersPerSecond;
        var peakSpeedMetersPerSecond = initialSpeedMetersPerSecond;
        var travelTimeSeconds = 0d;
        var accelerationDistanceMeters = 0d;
        var cruiseDistanceMeters = 0d;
        var decelerationDistanceMeters = 0d;
        var lastPhase = StraightDistancePhase.Cruise;
        var referenceAvailableDriveForceNewtons = CalculateStraightAvailableDriveForceNewtons(
            skills,
            setup,
            surface);

        for (var stepIndex = 0; stepIndex < integrationStepsMeters.Length; stepIndex++)
        {
            var stepDistanceMeters = (float)integrationStepsMeters[stepIndex];
            var fullDriveEndSpeedMetersPerSecond =
                CalculateMidpointPositiveDriveEndSpeedMetersPerSecond(
                currentSpeedMetersPerSecond,
                stepDistanceMeters,
                speedCeilingMetersPerSecond,
                referenceAvailableDriveForceNewtons,
                setup);

            var endSpeedMetersPerSecond = fullDriveEndSpeedMetersPerSecond;
            if (allowedSpeedEnvelope is not null)
            {
                var allowedEndSpeedMetersPerSecond = (float)allowedSpeedEnvelope[stepIndex + 1];
                if (fullDriveEndSpeedMetersPerSecond > allowedEndSpeedMetersPerSecond)
                {
                    var maximumDecelerationEndSpeedMetersPerSecond = DecelerateOverDistance(
                        currentSpeedMetersPerSecond,
                        cornerEntryDecelerationMetersPerSecondSquared,
                        stepDistanceMeters);
                    endSpeedMetersPerSecond = Math.Max(
                        allowedEndSpeedMetersPerSecond,
                        maximumDecelerationEndSpeedMetersPerSecond);
                }
            }

            var speedSumMetersPerSecond =
                (double)currentSpeedMetersPerSecond + endSpeedMetersPerSecond;
            if (speedSumMetersPerSecond <= 0d)
            {
                throw new InvalidOperationException(
                    "A positive Straight distance cannot be traversed at zero speed.");
            }

            travelTimeSeconds += 2d * stepDistanceMeters / speedSumMetersPerSecond;
            var speedChangeMetersPerSecond =
                endSpeedMetersPerSecond - currentSpeedMetersPerSecond;
            if (speedChangeMetersPerSecond > LongitudinalPhaseSpeedToleranceMetersPerSecond)
            {
                accelerationDistanceMeters += stepDistanceMeters;
                lastPhase = StraightDistancePhase.Acceleration;
            }
            else if (speedChangeMetersPerSecond < -LongitudinalPhaseSpeedToleranceMetersPerSecond)
            {
                decelerationDistanceMeters += stepDistanceMeters;
                lastPhase = StraightDistancePhase.Deceleration;
            }
            else
            {
                cruiseDistanceMeters += stepDistanceMeters;
                lastPhase = StraightDistancePhase.Cruise;
            }

            currentSpeedMetersPerSecond = endSpeedMetersPerSecond;
            peakSpeedMetersPerSecond = Math.Max(
                peakSpeedMetersPerSecond,
                currentSpeedMetersPerSecond);
        }

        ReconcileStraightPhaseDistance(
            distanceMeters,
            lastPhase,
            ref accelerationDistanceMeters,
            ref cruiseDistanceMeters,
            ref decelerationDistanceMeters);

        if (!double.IsFinite(travelTimeSeconds) || travelTimeSeconds > float.MaxValue)
            throw new OverflowException("Straight travel time exceeds the finite single-precision domain.");

        return new StraightSpeedProfile(
            currentSpeedMetersPerSecond,
            peakSpeedMetersPerSecond,
            (float)travelTimeSeconds,
            (float)accelerationDistanceMeters,
            (float)cruiseDistanceMeters,
            (float)decelerationDistanceMeters);
    }

    public static TurnExitDriveProfile CalculateForceBasedTurnExitDriveProfile(
        float initialSpeedMetersPerSecond,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface,
        float distanceMeters,
        float speedCeilingMetersPerSecond)
    {
        ValidateNonNegativeFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));
        ValidatePositiveFinite(speedCeilingMetersPerSecond, nameof(speedCeilingMetersPerSecond));

        var referenceAvailableDriveForceNewtons =
            CalculateTurnExitAvailableDriveForceNewtons(skills, setup, surface);
        var entryNetAccelerationMetersPerSecondSquared =
            CalculateNetPositiveDriveAccelerationMetersPerSecondSquared(
                initialSpeedMetersPerSecond,
                referenceAvailableDriveForceNewtons,
                setup);
        if (distanceMeters == 0f)
        {
            return new TurnExitDriveProfile(
                initialSpeedMetersPerSecond,
                initialSpeedMetersPerSecond,
                0f,
                0f,
                0f,
                entryNetAccelerationMetersPerSecondSquared);
        }

        var integrationStepsMeters = CreateLongitudinalIntegrationSteps(distanceMeters);
        var currentSpeedMetersPerSecond = initialSpeedMetersPerSecond;
        var peakSpeedMetersPerSecond = initialSpeedMetersPerSecond;
        var travelTimeSeconds = 0d;
        var accelerationDistanceMeters = 0d;
        var cruiseDistanceMeters = 0d;
        var lastStepAccelerated = false;

        foreach (var integrationStepMeters in integrationStepsMeters)
        {
            var stepDistanceMeters = (float)integrationStepMeters;
            var endSpeedMetersPerSecond = CalculateMidpointPositiveDriveEndSpeedMetersPerSecond(
                currentSpeedMetersPerSecond,
                stepDistanceMeters,
                speedCeilingMetersPerSecond,
                referenceAvailableDriveForceNewtons,
                setup);
            var speedSumMetersPerSecond =
                (double)currentSpeedMetersPerSecond + endSpeedMetersPerSecond;
            if (speedSumMetersPerSecond <= 0d)
            {
                throw new InvalidOperationException(
                    "A positive TurnExit distance cannot be traversed at zero speed.");
            }

            travelTimeSeconds += 2d * stepDistanceMeters / speedSumMetersPerSecond;
            lastStepAccelerated =
                endSpeedMetersPerSecond - currentSpeedMetersPerSecond
                > LongitudinalPhaseSpeedToleranceMetersPerSecond;
            if (lastStepAccelerated)
                accelerationDistanceMeters += stepDistanceMeters;
            else
                cruiseDistanceMeters += stepDistanceMeters;

            currentSpeedMetersPerSecond = endSpeedMetersPerSecond;
            peakSpeedMetersPerSecond = Math.Max(
                peakSpeedMetersPerSecond,
                currentSpeedMetersPerSecond);
        }

        var distanceCorrectionMeters =
            distanceMeters - accelerationDistanceMeters - cruiseDistanceMeters;
        if (lastStepAccelerated)
            accelerationDistanceMeters += distanceCorrectionMeters;
        else
            cruiseDistanceMeters += distanceCorrectionMeters;

        if (!double.IsFinite(travelTimeSeconds) || travelTimeSeconds > float.MaxValue)
        {
            throw new OverflowException(
                "TurnExit travel time exceeds the finite single-precision domain.");
        }

        return new TurnExitDriveProfile(
            currentSpeedMetersPerSecond,
            peakSpeedMetersPerSecond,
            (float)travelTimeSeconds,
            (float)accelerationDistanceMeters,
            (float)cruiseDistanceMeters,
            entryNetAccelerationMetersPerSecondSquared);
    }

    private static double[] CreateLongitudinalIntegrationSteps(float distanceMeters)
    {
        var fullStepCount = Math.Floor(
            (double)distanceMeters / ProvisionalLongitudinalIntegrationStepMeters);
        var remainderMeters = distanceMeters
            - fullStepCount * ProvisionalLongitudinalIntegrationStepMeters;
        var stepCount = fullStepCount + (remainderMeters > 0d ? 1d : 0d);
        if (stepCount > int.MaxValue)
        {
            throw new OverflowException(
                "Longitudinal distance requires too many numerical integration steps.");
        }

        var steps = new double[(int)stepCount];
        for (var index = 0; index < (int)fullStepCount; index++)
            steps[index] = ProvisionalLongitudinalIntegrationStepMeters;
        if (remainderMeters > 0d)
            steps[^1] = remainderMeters;
        return steps;
    }

    private static double[] CreateBackwardAllowedSpeedEnvelope(
        IReadOnlyList<double> integrationStepsMeters,
        float targetExitSpeedMetersPerSecond,
        float cornerEntryDecelerationMetersPerSecondSquared)
    {
        var allowedSpeeds = new double[integrationStepsMeters.Count + 1];
        allowedSpeeds[^1] = targetExitSpeedMetersPerSecond;
        for (var stepIndex = integrationStepsMeters.Count - 1; stepIndex >= 0; stepIndex--)
        {
            var allowedStartSpeedSquared =
                allowedSpeeds[stepIndex + 1] * allowedSpeeds[stepIndex + 1]
                + 2d * cornerEntryDecelerationMetersPerSecondSquared
                  * integrationStepsMeters[stepIndex];
            var allowedStartSpeed = Math.Sqrt(allowedStartSpeedSquared);
            if (!double.IsFinite(allowedStartSpeed) || allowedStartSpeed > float.MaxValue)
            {
                throw new OverflowException(
                    "Straight backward allowed-speed envelope exceeds the finite domain.");
            }

            allowedSpeeds[stepIndex] = allowedStartSpeed;
        }

        return allowedSpeeds;
    }

    private static void ReconcileStraightPhaseDistance(
        float distanceMeters,
        StraightDistancePhase lastPhase,
        ref double accelerationDistanceMeters,
        ref double cruiseDistanceMeters,
        ref double decelerationDistanceMeters)
    {
        var correctionMeters = distanceMeters
            - accelerationDistanceMeters
            - cruiseDistanceMeters
            - decelerationDistanceMeters;
        switch (lastPhase)
        {
            case StraightDistancePhase.Acceleration:
                accelerationDistanceMeters += correctionMeters;
                break;
            case StraightDistancePhase.Deceleration:
                decelerationDistanceMeters += correctionMeters;
                break;
            default:
                cruiseDistanceMeters += correctionMeters;
                break;
        }
    }

    private enum StraightDistancePhase
    {
        Acceleration,
        Cruise,
        Deceleration,
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
