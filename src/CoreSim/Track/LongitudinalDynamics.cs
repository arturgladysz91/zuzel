using CoreSim.Setup;

namespace CoreSim;

/// <summary>
/// Deterministic traversal result for one advanced-physics straight. Distances
/// describe acceleration, cruise and speed decrease (natural signed force or
/// explicit corner preparation). Equilibrium is observation, never a limiter.
/// </summary>
public readonly record struct StraightSpeedProfile(
    float ExitSpeedMetersPerSecond,
    float PeakSpeedMetersPerSecond,
    float TravelTimeSeconds,
    float AccelerationDistanceMeters,
    float CruiseDistanceMeters,
    float DecelerationDistanceMeters,
    // Null only for the non-production constant-acceleration compatibility utility.
    float? FullDriveEquilibriumSpeedMetersPerSecond = null)
{
    // Populated only by the internal #41 experiment path. The default production
    // profile remains allocation-free and keeps its public contract unchanged.
    internal IReadOnlyList<StraightDriveStepObservation>? CalibrationSteps { get; init; }
}

/// <summary>
/// Deterministic signed full-drive traversal of one eligible advanced TurnExit.
/// The entry acceleration is diagnostic only; traversal evaluates acceleration
/// independently at every shared longitudinal midpoint step.
/// </summary>
public readonly record struct TurnExitDriveProfile(
    float ExitSpeedMetersPerSecond,
    float PeakSpeedMetersPerSecond,
    float TravelTimeSeconds,
    float AccelerationDistanceMeters,
    float CruiseDistanceMeters,
    float DecelerationDistanceMeters,
    float EntryNetAccelerationMetersPerSecondSquared,
    float FullDriveEquilibriumSpeedMetersPerSecond);

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
/// Deterministic correction phase for a recoverable advanced corner constraint.
/// Remaining distance is deliberately not consumed by this profile.
/// </summary>
public readonly record struct CornerSpeedCorrectionProfile(
    float EntrySpeedMetersPerSecond,
    float TargetSpeedMetersPerSecond,
    float ExitSpeedMetersPerSecond,
    float TravelTimeSeconds,
    float RequiredCorrectionDistanceMeters,
    float CorrectionDistanceMeters,
    float RemainingDistanceMeters,
    float DecelerationMetersPerSecondSquared,
    bool TargetReached);

/// <summary>
/// Pre-contact traversal from rest, measured from tape movement. Reaction is
/// stationary; only MovementTimeSeconds contributes to lateral movement.
/// Start metrics are observed outputs, never physics calibration targets.
/// </summary>
public readonly record struct StandingStartLaunchProfile(
    float ReactionTimeSeconds,
    float MovementTimeSeconds,
    float TotalTimeSeconds,
    float ExitSpeedMetersPerSecond,
    float PeakSpeedMetersPerSecond,
    float AccelerationDistanceMeters,
    float CruiseDistanceMeters,
    float PreparationDistanceMeters,
    float EntryNetAccelerationMetersPerSecondSquared,
    float? TimeTo70KphSeconds,
    float? SpeedAtTwoSecondsMetersPerSecond,
    float FullDriveEquilibriumSpeedMetersPerSecond);

/// <summary>
/// Deterministic longitudinal motion helpers. The current acceleration values
/// are provisional first-model parameters, not final motorcycle performance data.
/// </summary>
public static class LongitudinalDynamics
{
    // PROVISIONAL / NOT REAL-WORLD CALIBRATED. No RNG, morale, gate bonus,
    // clutch, wheelspin or TractionBias multiplier is part of this foundation.
    public const float ProvisionalStandingStartSlowReactionSeconds = 0.28f;
    public const float ProvisionalStandingStartFastReactionSeconds = 0.20f;
    public const float ProvisionalStandingStartMinimumReferenceAccelerationMetersPerSecondSquared = 9.0f;
    public const float ProvisionalStandingStartMaximumReferenceAccelerationMetersPerSecondSquared = 11.0f;

    // Telemetry observation thresholds, NOT physics calibration constants.
    public const float StandingStartTelemetry70KphMetersPerSecond = 70f / 3.6f;
    public const float StandingStartTelemetryObservationTimeSeconds = 2f;

    // Calibrated together in #36 from deterministic finite-distance, force,
    // equilibrium, gearing-crossover and full-heat diagnostics. The model form
    // remains the signed one-gear foundation and is still a coarse abstraction.
    public const float MinTurnExitAccelerationMetersPerSecondSquared = 1.20f;
    public const float MaxTurnExitAccelerationMetersPerSecondSquared = 2.80f;
    public const float LowGearingDriveMultiplier = 1.10f;
    public const float HighGearingDriveMultiplier = 0.90f;
    public const float MinSurfaceDriveMultiplier = 0.75f;
    public const float SurfaceDriveMultiplierRange = 0.25f;
    public const float MinStraightAccelerationMetersPerSecondSquared = 1.60f;
    public const float MaxStraightAccelerationMetersPerSecondSquared = 3.20f;
    public const float MinCornerEntryDecelerationMetersPerSecondSquared = 2.00f;
    public const float MaxCornerEntryDecelerationMetersPerSecondSquared = 3.20f;

    // Coarse game-model inputs. #36 calibrates only the two acceleration ranges
    // and both fade endpoints; mass, resistance and reference speed remain frozen.
    // Mass is nominal system mass, not a rider attribute.
    public const float ProvisionalNominalSystemMassKilograms = 142f;
    public const float ProvisionalBaseResistanceForceNewtons = 40f;
    public const float ProvisionalQuadraticResistanceCoefficient = 0.20f;
    public const float ProvisionalPositiveDriveReferenceSpeedMetersPerSecond = 16f;
    public const float ProvisionalDriveOrientedForceFadePerMeterPerSecond = 0.0350f;
    public const float ProvisionalSpeedOrientedForceFadePerMeterPerSecond = 0.0100f;

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

    public static float ApplySignedAccelerationOverDistance(
        float speedMetersPerSecond,
        float signedAccelerationMetersPerSecondSquared,
        float distanceMeters)
    {
        ValidateNonNegativeFinite(speedMetersPerSecond, nameof(speedMetersPerSecond));
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));
        if (!float.IsFinite(signedAccelerationMetersPerSecondSquared))
            throw new ArgumentOutOfRangeException(nameof(signedAccelerationMetersPerSecondSquared));
        var endSpeedSquared = Math.Max(0d,
            (double)speedMetersPerSecond * speedMetersPerSecond
            + 2d * signedAccelerationMetersPerSecondSquared * distanceMeters);
        var endSpeed = (float)Math.Sqrt(endSpeedSquared);
        if (!float.IsFinite(endSpeed))
            throw new OverflowException("Final speed exceeds the finite single-precision domain.");
        return endSpeed;
    }

    public static float AccelerateOverDistance(
        float initialSpeedMetersPerSecond,
        float accelerationMetersPerSecondSquared,
        float distanceMeters)
    {
        ValidateNonNegativeFinite(accelerationMetersPerSecondSquared, nameof(accelerationMetersPerSecondSquared));
        return ApplySignedAccelerationOverDistance(
            initialSpeedMetersPerSecond, accelerationMetersPerSecondSquared, distanceMeters);
    }

    public static float DecelerateOverDistance(
        float initialSpeedMetersPerSecond,
        float decelerationMetersPerSecondSquared,
        float distanceMeters)
    {
        ValidateNonNegativeFinite(decelerationMetersPerSecondSquared, nameof(decelerationMetersPerSecondSquared));
        return ApplySignedAccelerationOverDistance(
            initialSpeedMetersPerSecond, -decelerationMetersPerSecondSquared, distanceMeters);
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

    public static float CalculateStandingStartReactionTimeSeconds(RiderSkills skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var reactionTime = ProvisionalStandingStartSlowReactionSeconds
            + (ProvisionalStandingStartFastReactionSeconds - ProvisionalStandingStartSlowReactionSeconds)
            * RiderSkills.Normalize(skills.Start);
        ValidateNonNegativeFinite(reactionTime, "result");
        return reactionTime;
    }

    public static float CalculateStandingStartReferenceDriveAccelerationMetersPerSecondSquared(
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);
        var baseAcceleration = ProvisionalStandingStartMinimumReferenceAccelerationMetersPerSecondSquared
            + (ProvisionalStandingStartMaximumReferenceAccelerationMetersPerSecondSquared
                - ProvisionalStandingStartMinimumReferenceAccelerationMetersPerSecondSquared)
            * RiderSkills.Normalize(skills.Start);
        var gearingDriveMultiplier = LowGearingDriveMultiplier
            + (HighGearingDriveMultiplier - LowGearingDriveMultiplier) * setup.Gearing;
        var surfaceDriveMultiplier = MinSurfaceDriveMultiplier
            + SurfaceDriveMultiplierRange * surface.EffectiveGrip;
        var acceleration = baseAcceleration * gearingDriveMultiplier * surfaceDriveMultiplier;
        ValidateNonNegativeFinite(acceleration, "result");
        return acceleration;
    }

    public static float CalculateStandingStartAvailableDriveForceNewtons(
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        var force = ProvisionalNominalSystemMassKilograms
            * CalculateStandingStartReferenceDriveAccelerationMetersPerSecondSquared(skills, setup, surface)
            + CalculateLongitudinalResistanceForceNewtons(0f);
        ValidateNonNegativeFinite(force, "result");
        return force;
    }

    public static StandingStartLaunchProfile CalculateStandingStartLaunchProfile(
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface,
        float distanceMeters,
        float? targetExitSpeedMetersPerSecond = null)
        => CalculateStandingStartLaunchProfile(skills, setup, surface, distanceMeters,
            targetExitSpeedMetersPerSecond, null);

    // Read-only observation of the existing integration, never a second traversal.
    internal static StandingStartLaunchProfile CalculateStandingStartLaunchProfile(
        RiderSkills skills, BikeSetup setup, TrackSurfaceState surface, float distanceMeters,
        float? targetExitSpeedMetersPerSecond, ICollection<LongitudinalMotionNode>? motionNodes)
    {
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));
        if (targetExitSpeedMetersPerSecond is { } target)
            ValidateNonNegativeFinite(target, nameof(targetExitSpeedMetersPerSecond));
        var reactionTime = CalculateStandingStartReactionTimeSeconds(skills);
        var referenceForce = CalculateStandingStartAvailableDriveForceNewtons(skills, setup, surface);
        var equilibrium = CalculateFullDriveEquilibriumSpeedMetersPerSecond(referenceForce, setup);
        var entryAcceleration = CalculateNetDriveAccelerationMetersPerSecondSquared(0f, referenceForce, setup);
        var speed = 0f;
        var peakSpeed = 0f;
        var movementTime = 0d;
        var observedDistance = 0d;
        motionNodes?.Add(new(0f, 0f, 0f));
        var accelerationDistance = 0d;
        var cruiseDistance = 0d;
        var preparationDistance = 0d;
        var lastPhase = StraightDistancePhase.Cruise;
        float? timeTo70 = null;
        float? speedAtTwoSeconds = reactionTime >= StandingStartTelemetryObservationTimeSeconds ? 0f : null;

        var integrationSteps = CreateLongitudinalIntegrationSteps(distanceMeters);
        var preparationDeceleration = CalculateCornerEntryDecelerationMetersPerSecondSquared(skills, surface);
        var allowedSpeedEnvelope = targetExitSpeedMetersPerSecond is { } targetSpeed
            ? CreateBackwardAllowedSpeedEnvelope(integrationSteps, targetSpeed, preparationDeceleration)
            : null;
        for (var index = 0; index < integrationSteps.Length; index++)
        {
            var ds = (float)integrationSteps[index];
            var fullDriveEndSpeed = CalculateMidpointDriveEndSpeedMetersPerSecond(
                speed, ds, referenceForce, setup);
            var endSpeed = ApplyPreparationBoundary(speed, fullDriveEndSpeed, ds,
                preparationDeceleration, allowedSpeedEnvelope is null ? null : (float)allowedSpeedEnvelope[index + 1]);
            var speedSum = (double)speed + endSpeed;
            if (speedSum <= 0d)
                throw new InvalidOperationException("A positive launch distance cannot be traversed at zero speed.");
            var stepTime = 2d * ds / speedSum;
            var stepStartTime = reactionTime + movementTime;
            // Kinematics of this corrected production step, including any
            // preparation. Do not interpolate over the whole launch profile.
            var effectiveAcceleration = ((double)endSpeed * endSpeed - (double)speed * speed) / (2d * ds);
            if (timeTo70 is null && speed < StandingStartTelemetry70KphMetersPerSecond
                && endSpeed >= StandingStartTelemetry70KphMetersPerSecond)
            {
                timeTo70 = (float)(stepStartTime
                    + (StandingStartTelemetry70KphMetersPerSecond - speed) / effectiveAcceleration);
            }
            if (speedAtTwoSeconds is null
                && StandingStartTelemetryObservationTimeSeconds >= stepStartTime
                && StandingStartTelemetryObservationTimeSeconds <= stepStartTime + stepTime)
            {
                var dt = StandingStartTelemetryObservationTimeSeconds - stepStartTime;
                speedAtTwoSeconds = (float)Math.Clamp(speed + effectiveAcceleration * dt,
                    Math.Min(speed, endSpeed), Math.Max(speed, endSpeed));
            }

            movementTime += stepTime;
            var speedChange = endSpeed - speed;
            if (speedChange > LongitudinalPhaseSpeedToleranceMetersPerSecond)
            {
                accelerationDistance += ds;
                lastPhase = StraightDistancePhase.Acceleration;
            }
            else if (speedChange < -LongitudinalPhaseSpeedToleranceMetersPerSecond)
            {
                preparationDistance += ds;
                lastPhase = StraightDistancePhase.Deceleration;
            }
            else
            {
                cruiseDistance += ds;
                lastPhase = StraightDistancePhase.Cruise;
            }
            speed = endSpeed;
            peakSpeed = Math.Max(peakSpeed, speed);
            observedDistance += ds;
            motionNodes?.Add(new((float)observedDistance, (float)movementTime, speed));
        }

        ReconcileStraightPhaseDistance(distanceMeters, lastPhase,
            ref accelerationDistance, ref cruiseDistance, ref preparationDistance);
        if (!double.IsFinite(movementTime) || movementTime + reactionTime > float.MaxValue)
            throw new OverflowException("Launch time exceeds the finite single-precision domain.");
        var movementTimeSeconds = (float)movementTime;
        return new StandingStartLaunchProfile(
            reactionTime, movementTimeSeconds, reactionTime + movementTimeSeconds,
            speed, peakSpeed, (float)accelerationDistance, (float)cruiseDistance, (float)preparationDistance,
            entryAcceleration, timeTo70, speedAtTwoSeconds, equilibrium);
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
        ValidateNonNegativeFinite(availableDriveForceNewtons, nameof(availableDriveForceNewtons));
        ValidateNonNegativeFinite(resistanceForceNewtons, nameof(resistanceForceNewtons));
        ValidatePositiveFinite(systemMassKilograms, nameof(systemMassKilograms));
        var signedNetForceNewtons = (double)availableDriveForceNewtons - resistanceForceNewtons;
        var result = (float)(signedNetForceNewtons / systemMassKilograms);
        if (!float.IsFinite(result))
            throw new OverflowException("Signed acceleration exceeds the finite single-precision domain.");
        return result;
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

        var fadeRatePerMeterPerSecond = CalculateDriveForceFadeRatePerMeterPerSecond(setup);
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

    internal static float CalculateStraightDriveEnvelopeMultiplier(
        float speedMetersPerSecond,
        BikeSetup setup,
        StraightDriveEnvelopeAdjustment adjustment)
    {
        var baseline = CalculatePositiveDriveEnvelopeMultiplier(speedMetersPerSecond, setup);
        if (adjustment.IsProductionBaseline)
            return baseline;

        var result = (float)Math.Clamp(
            (double)baseline + adjustment.Delta(speedMetersPerSecond),
            0d,
            1d);
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
        return CalculateNetDriveAccelerationMetersPerSecondSquared(
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
        return CalculateNetDriveAccelerationMetersPerSecondSquared(
            speedMetersPerSecond,
            referenceAvailableForceNewtons,
            setup);
    }

    public static float CalculateNetDriveAccelerationMetersPerSecondSquared(
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

    internal static float CalculateStraightNetDriveAccelerationMetersPerSecondSquared(
        float speedMetersPerSecond,
        float referenceAvailableDriveForceNewtons,
        BikeSetup setup,
        StraightDriveEnvelopeAdjustment adjustment)
    {
        if (adjustment.IsProductionBaseline)
        {
            return CalculateNetDriveAccelerationMetersPerSecondSquared(
                speedMetersPerSecond,
                referenceAvailableDriveForceNewtons,
                setup);
        }

        ValidateNonNegativeFinite(speedMetersPerSecond, nameof(speedMetersPerSecond));
        ValidateNonNegativeFinite(
            referenceAvailableDriveForceNewtons,
            nameof(referenceAvailableDriveForceNewtons));
        ArgumentNullException.ThrowIfNull(setup);
        var envelope = CalculateStraightDriveEnvelopeMultiplier(
            speedMetersPerSecond,
            setup,
            adjustment);
        var availableDriveForceNewtons = (float)(referenceAvailableDriveForceNewtons * (double)envelope);
        return CalculateAccelerationFromForcesMetersPerSecondSquared(
            availableDriveForceNewtons,
            CalculateLongitudinalResistanceForceNewtons(speedMetersPerSecond),
            ProvisionalNominalSystemMassKilograms);
    }

    public static float CalculateMidpointDriveEndSpeedMetersPerSecond(
        float currentSpeedMetersPerSecond,
        float stepDistanceMeters,
        float referenceAvailableDriveForceNewtons,
        BikeSetup setup,
        float netDriveAvailability = 1f)
    {
        ValidateNonNegativeFinite(currentSpeedMetersPerSecond, nameof(currentSpeedMetersPerSecond));
        ValidateNonNegativeFinite(stepDistanceMeters, nameof(stepDistanceMeters));
        if (!float.IsFinite(netDriveAvailability) || netDriveAvailability < 0f || netDriveAvailability > 1f)
            throw new ArgumentOutOfRangeException(nameof(netDriveAvailability));
        var accelerationAtStart = CalculateNetDriveAccelerationMetersPerSecondSquared(
            currentSpeedMetersPerSecond, referenceAvailableDriveForceNewtons, setup) * netDriveAvailability;
        var predictedSpeed = ApplySignedAccelerationOverDistance(
            currentSpeedMetersPerSecond, accelerationAtStart, stepDistanceMeters);
        var midpointSpeed = (float)(((double)currentSpeedMetersPerSecond + predictedSpeed) * 0.5d);
        var accelerationAtMidpoint = CalculateNetDriveAccelerationMetersPerSecondSquared(
            midpointSpeed, referenceAvailableDriveForceNewtons, setup) * netDriveAvailability;
        return ApplySignedAccelerationOverDistance(
            currentSpeedMetersPerSecond, accelerationAtMidpoint, stepDistanceMeters);
    }

    private static StraightDriveStepObservation CalculateStraightMidpointDriveStep(
        float startDistanceMeters,
        float currentSpeedMetersPerSecond,
        float stepDistanceMeters,
        float referenceAvailableDriveForceNewtons,
        BikeSetup setup,
        StraightDriveEnvelopeAdjustment adjustment,
        float? allowedEndSpeedMetersPerSecond,
        float preparationDecelerationMetersPerSecondSquared)
    {
        var accelerationAtStart = CalculateStraightNetDriveAccelerationMetersPerSecondSquared(
            currentSpeedMetersPerSecond,
            referenceAvailableDriveForceNewtons,
            setup,
            adjustment);
        var predictedSpeed = ApplySignedAccelerationOverDistance(
            currentSpeedMetersPerSecond,
            accelerationAtStart,
            stepDistanceMeters);
        var midpointSpeed = (float)(((double)currentSpeedMetersPerSecond + predictedSpeed) * 0.5d);
        var accelerationAtMidpoint = CalculateStraightNetDriveAccelerationMetersPerSecondSquared(
            midpointSpeed,
            referenceAvailableDriveForceNewtons,
            setup,
            adjustment);
        var fullDriveExitSpeed = adjustment.IsProductionBaseline
            ? CalculateMidpointDriveEndSpeedMetersPerSecond(
                currentSpeedMetersPerSecond,
                stepDistanceMeters,
                referenceAvailableDriveForceNewtons,
                setup)
            : ApplySignedAccelerationOverDistance(
                currentSpeedMetersPerSecond,
                accelerationAtMidpoint,
                stepDistanceMeters);
        var preparationReachableSpeed = DecelerateOverDistance(
            currentSpeedMetersPerSecond,
            preparationDecelerationMetersPerSecondSquared,
            stepDistanceMeters);
        var exitSpeed = ApplyPreparationBoundary(
            currentSpeedMetersPerSecond,
            fullDriveExitSpeed,
            stepDistanceMeters,
            preparationDecelerationMetersPerSecondSquared,
            allowedEndSpeedMetersPerSecond);
        var experimentalEnvelope = CalculateStraightDriveEnvelopeMultiplier(
            midpointSpeed,
            setup,
            adjustment);
        var availableDriveForce = (float)(referenceAvailableDriveForceNewtons * (double)experimentalEnvelope);

        return new StraightDriveStepObservation(
            startDistanceMeters,
            startDistanceMeters + stepDistanceMeters,
            currentSpeedMetersPerSecond,
            predictedSpeed,
            midpointSpeed,
            fullDriveExitSpeed,
            exitSpeed,
            CalculatePositiveDriveEnvelopeMultiplier(midpointSpeed, setup),
            experimentalEnvelope,
            availableDriveForce,
            CalculateLongitudinalResistanceForceNewtons(midpointSpeed),
            accelerationAtMidpoint,
            allowedEndSpeedMetersPerSecond,
            preparationReachableSpeed,
            exitSpeed < fullDriveExitSpeed - LongitudinalPhaseSpeedToleranceMetersPerSecond);
    }

    // Pure zero-drive aggregate resistance, NOT a final engine-braking model and
    // NOT a replacement for the effective corner-entry preparation capability.
    public static float CalculateZeroDriveSignedAccelerationMetersPerSecondSquared(float speedMetersPerSecond)
        => CalculateAccelerationFromForcesMetersPerSecondSquared(0f,
            CalculateLongitudinalResistanceForceNewtons(speedMetersPerSecond),
            ProvisionalNominalSystemMassKilograms);

    // Numerical diagnostic tolerance, not a physics/calibration parameter.
    public const double EquilibriumSolverSpeedToleranceMetersPerSecond = 1e-6d;

    /// <summary>
    /// Observes the unique nonnegative root of full-drive net force. Traversal
    /// never uses this result as a speed cap or a stopping criterion.
    /// Forces below resistance at rest have no nonnegative root and are rejected.
    /// </summary>
    public static float CalculateFullDriveEquilibriumSpeedMetersPerSecond(
        float referenceAvailableDriveForceNewtons,
        BikeSetup setup)
    {
        ValidateNonNegativeFinite(referenceAvailableDriveForceNewtons, nameof(referenceAvailableDriveForceNewtons));
        ArgumentNullException.ThrowIfNull(setup);
        var fadeRate = CalculateDriveForceFadeRatePerMeterPerSecond(setup);
        if (!double.IsFinite(fadeRate) || fadeRate <= 0d)
            throw new ArgumentOutOfRangeException(nameof(setup), "Equilibrium requires a finite positive drive fade rate.");
        var netAtRest = CalculateNetDriveAccelerationMetersPerSecondSquared(
            0f, referenceAvailableDriveForceNewtons, setup);
        if (netAtRest < 0f)
            throw new ArgumentOutOfRangeException(nameof(referenceAvailableDriveForceNewtons),
                "Reference force below resistance at rest has no nonnegative full-drive equilibrium.");
        if (netAtRest == 0f)
            return 0f;

        var lower = 0d;
        var upper = ProvisionalPositiveDriveReferenceSpeedMetersPerSecond
            + 1d / fadeRate;
        // At the upper bracket drive is zero and net force is negative.
        for (var iteration = 0; iteration < 64
             && upper - lower > EquilibriumSolverSpeedToleranceMetersPerSecond; iteration++)
        {
            var midpoint = (lower + upper) * 0.5d;
            if (CalculateNetDriveAccelerationMetersPerSecondSquared(
                    (float)midpoint, referenceAvailableDriveForceNewtons, setup) > 0f)
                lower = midpoint;
            else
                upper = midpoint;
        }
        return (float)((lower + upper) * 0.5d);
    }

    internal static float CalculateStraightFullDriveEquilibriumSpeedMetersPerSecond(
        float referenceAvailableDriveForceNewtons,
        BikeSetup setup,
        StraightDriveEnvelopeAdjustment adjustment)
    {
        if (adjustment.IsProductionBaseline)
            return CalculateFullDriveEquilibriumSpeedMetersPerSecond(referenceAvailableDriveForceNewtons, setup);

        ValidateNonNegativeFinite(referenceAvailableDriveForceNewtons, nameof(referenceAvailableDriveForceNewtons));
        ArgumentNullException.ThrowIfNull(setup);
        var netAtRest = CalculateStraightNetDriveAccelerationMetersPerSecondSquared(
            0f,
            referenceAvailableDriveForceNewtons,
            setup,
            adjustment);
        if (netAtRest < 0f)
            throw new ArgumentOutOfRangeException(nameof(referenceAvailableDriveForceNewtons));
        if (netAtRest == 0f)
            return 0f;

        var fadeRate = CalculateDriveForceFadeRatePerMeterPerSecond(setup);
        var lower = 0d;
        var upper = ProvisionalPositiveDriveReferenceSpeedMetersPerSecond + 1d / fadeRate;
        for (var iteration = 0; iteration < 64
             && upper - lower > EquilibriumSolverSpeedToleranceMetersPerSecond; iteration++)
        {
            var midpoint = (lower + upper) * 0.5d;
            if (CalculateStraightNetDriveAccelerationMetersPerSecondSquared(
                    (float)midpoint,
                    referenceAvailableDriveForceNewtons,
                    setup,
                    adjustment) > 0f)
                lower = midpoint;
            else
                upper = midpoint;
        }

        return (float)((lower + upper) * 0.5d);
    }

    private static double CalculateDriveForceFadeRatePerMeterPerSecond(BikeSetup setup)
        => ProvisionalDriveOrientedForceFadePerMeterPerSecond
            + (ProvisionalSpeedOrientedForceFadePerMeterPerSecond
               - ProvisionalDriveOrientedForceFadePerMeterPerSecond) * (double)setup.Gearing;

    public static float CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
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

    /// <summary>Compatibility alias for the canonical corner-correction capability.</summary>
    public static float CalculateCornerEntryDecelerationMetersPerSecondSquared(
        RiderSkills skills,
        TrackSurfaceState surface)
        => CalculateCornerCorrectionDecelerationMetersPerSecondSquared(skills, surface);

    public static CornerSpeedCorrectionProfile CalculateCornerSpeedCorrectionProfile(
        float initialSpeedMetersPerSecond,
        float targetSpeedMetersPerSecond,
        float decelerationMetersPerSecondSquared,
        float availableDistanceMeters)
    {
        ValidateNonNegativeFinite(
            initialSpeedMetersPerSecond,
            nameof(initialSpeedMetersPerSecond));
        ValidateNonNegativeFinite(
            targetSpeedMetersPerSecond,
            nameof(targetSpeedMetersPerSecond));
        ValidatePositiveFinite(
            decelerationMetersPerSecondSquared,
            nameof(decelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(
            availableDistanceMeters,
            nameof(availableDistanceMeters));

        var targetAlreadyReached = initialSpeedMetersPerSecond <= targetSpeedMetersPerSecond;
        var requiredDistance = targetAlreadyReached
            ? 0d
            : ((double)initialSpeedMetersPerSecond * initialSpeedMetersPerSecond
               - (double)targetSpeedMetersPerSecond * targetSpeedMetersPerSecond)
              / (2d * decelerationMetersPerSecondSquared);
        if (!double.IsFinite(requiredDistance) || requiredDistance > float.MaxValue)
        {
            throw new OverflowException(
                "Corner correction distance exceeds the finite single-precision domain.");
        }

        var requiredDistanceMeters = (float)requiredDistance;
        if (availableDistanceMeters == 0f)
        {
            return new CornerSpeedCorrectionProfile(
                initialSpeedMetersPerSecond,
                targetSpeedMetersPerSecond,
                initialSpeedMetersPerSecond,
                0f,
                requiredDistanceMeters,
                0f,
                0f,
                decelerationMetersPerSecondSquared,
                targetAlreadyReached);
        }

        if (targetAlreadyReached)
        {
            return new CornerSpeedCorrectionProfile(
                initialSpeedMetersPerSecond,
                targetSpeedMetersPerSecond,
                initialSpeedMetersPerSecond,
                0f,
                0f,
                0f,
                availableDistanceMeters,
                decelerationMetersPerSecondSquared,
                true);
        }

        var targetReached = requiredDistance <= availableDistanceMeters;
        var correctionDistanceMeters = targetReached
            ? MathF.Min(requiredDistanceMeters, availableDistanceMeters)
            : availableDistanceMeters;
        var exitSpeedMetersPerSecond = targetReached
            ? targetSpeedMetersPerSecond
            : DecelerateOverDistance(
                initialSpeedMetersPerSecond,
                decelerationMetersPerSecondSquared,
                correctionDistanceMeters);
        var remainingDistanceMeters = targetReached
            ? availableDistanceMeters - correctionDistanceMeters
            : 0f;
        var travelTimeSeconds = CalculatePhaseTimeSeconds(
            correctionDistanceMeters,
            initialSpeedMetersPerSecond,
            exitSpeedMetersPerSecond);

        // A positive double residual below half a float ULP is not observable
        // in the public float speed domain. The entire available distance was
        // still consumed; no speed assignment or extra correction is performed.
        if (!targetReached && exitSpeedMetersPerSecond == targetSpeedMetersPerSecond)
            targetReached = true;
        if (!targetReached && exitSpeedMetersPerSecond < targetSpeedMetersPerSecond)
        {
            throw new InvalidOperationException(
                "An insufficient corner-correction distance must retain residual overspeed.");
        }

        return new CornerSpeedCorrectionProfile(
            initialSpeedMetersPerSecond,
            targetSpeedMetersPerSecond,
            exitSpeedMetersPerSecond,
            travelTimeSeconds,
            requiredDistanceMeters,
            correctionDistanceMeters,
            remainingDistanceMeters,
            decelerationMetersPerSecondSquared,
            targetReached);
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

    /// <summary>
    /// Segment-aware compatibility bridge. The geometric corner phase is
    /// validated and the unchanged TurnEntry calculation remains the single
    /// numerical implementation.
    /// </summary>
    public static TurnEntryScrubProfile CalculateTurnEntryScrubProfile(
        CornerPhaseContext cornerPhase,
        float initialSpeedMetersPerSecond,
        float settledTargetSpeedMetersPerSecond,
        float decelerationMetersPerSecondSquared,
        float availableTurnEntryDistanceMeters)
    {
        RequireCompatibilityCornerPhase(
            cornerPhase,
            SegmentType.TurnEntry,
            nameof(cornerPhase));
        return CalculateTurnEntryScrubProfile(
            initialSpeedMetersPerSecond,
            settledTargetSpeedMetersPerSecond,
            decelerationMetersPerSecondSquared,
            availableTurnEntryDistanceMeters);
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

    /// <summary>
    /// Non-production compatibility utility for an explicitly specified constant
    /// acceleration and speed constraint. It has no force model or equilibrium;
    /// production advanced physics uses CalculateForceBasedStraightSpeedProfile.
    /// </summary>
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
        float? targetExitSpeedMetersPerSecond = null)
        => CalculateForceBasedStraightSpeedProfileCore(
            initialSpeedMetersPerSecond,
            skills,
            setup,
            surface,
            cornerEntryDecelerationMetersPerSecondSquared,
            distanceMeters,
            targetExitSpeedMetersPerSecond,
            null);

    internal static StraightSpeedProfile CalculateForceBasedStraightSpeedProfile(
        float initialSpeedMetersPerSecond,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface,
        float cornerEntryDecelerationMetersPerSecondSquared,
        float distanceMeters,
        float? targetExitSpeedMetersPerSecond,
        StraightDriveEnvelopeAdjustment adjustment)
        => CalculateForceBasedStraightSpeedProfileCore(
            initialSpeedMetersPerSecond,
            skills,
            setup,
            surface,
            cornerEntryDecelerationMetersPerSecondSquared,
            distanceMeters,
            targetExitSpeedMetersPerSecond,
            adjustment);

    internal static StraightSpeedProfile CalculateForceBasedStraightSpeedProfile(
        float initialSpeedMetersPerSecond, RiderSkills skills, BikeSetup setup,
        TrackSurfaceState surface, float cornerEntryDecelerationMetersPerSecondSquared,
        float distanceMeters, float? targetExitSpeedMetersPerSecond,
        ICollection<LongitudinalMotionNode>? motionNodes, StraightDriveEnvelopeAdjustment? adjustment = null)
        => CalculateForceBasedStraightSpeedProfileCore(initialSpeedMetersPerSecond, skills, setup,
            surface, cornerEntryDecelerationMetersPerSecondSquared, distanceMeters,
            targetExitSpeedMetersPerSecond, adjustment, motionNodes);

    private static StraightSpeedProfile CalculateForceBasedStraightSpeedProfileCore(
        float initialSpeedMetersPerSecond,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface,
        float cornerEntryDecelerationMetersPerSecondSquared,
        float distanceMeters,
        float? targetExitSpeedMetersPerSecond,
        StraightDriveEnvelopeAdjustment? adjustment,
        ICollection<LongitudinalMotionNode>? motionNodes = null)
    {
        ValidateNonNegativeFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);
        ValidatePositiveFinite(
            cornerEntryDecelerationMetersPerSecondSquared,
            nameof(cornerEntryDecelerationMetersPerSecondSquared));
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));
        if (targetExitSpeedMetersPerSecond is { } target)
            ValidateNonNegativeFinite(target, nameof(targetExitSpeedMetersPerSecond));

        var referenceAvailableDriveForceNewtons = CalculateStraightAvailableDriveForceNewtons(
            skills,
            setup,
            surface);
        var equilibrium = adjustment is { } experimentalAdjustment
            ? CalculateStraightFullDriveEquilibriumSpeedMetersPerSecond(
                referenceAvailableDriveForceNewtons,
                setup,
                experimentalAdjustment)
            : CalculateFullDriveEquilibriumSpeedMetersPerSecond(referenceAvailableDriveForceNewtons, setup);

        if (distanceMeters == 0f)
        {
            return new StraightSpeedProfile(
                initialSpeedMetersPerSecond,
                initialSpeedMetersPerSecond,
                0f,
                0f,
                0f,
                0f,
                equilibrium)
            {
                CalibrationSteps = adjustment.HasValue
                    ? Array.AsReadOnly(Array.Empty<StraightDriveStepObservation>())
                    : null,
            };
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
        var distanceProgressMeters = 0f;
        var observedDistance = 0d;
        motionNodes?.Add(new(0f, 0f, initialSpeedMetersPerSecond));
        var calibrationSteps = adjustment.HasValue
            ? new List<StraightDriveStepObservation>(integrationStepsMeters.Length)
            : null;

        for (var stepIndex = 0; stepIndex < integrationStepsMeters.Length; stepIndex++)
        {
            var stepDistanceMeters = (float)integrationStepsMeters[stepIndex];
            var allowedEndSpeed = allowedSpeedEnvelope is null
                ? (float?)null
                : (float)allowedSpeedEnvelope[stepIndex + 1];
            float endSpeedMetersPerSecond;
            if (adjustment is { } activeAdjustment)
            {
                var observedStep = CalculateStraightMidpointDriveStep(
                    distanceProgressMeters,
                    currentSpeedMetersPerSecond,
                    stepDistanceMeters,
                    referenceAvailableDriveForceNewtons,
                    setup,
                    activeAdjustment,
                    allowedEndSpeed,
                    cornerEntryDecelerationMetersPerSecondSquared);
                calibrationSteps!.Add(observedStep);
                endSpeedMetersPerSecond = observedStep.ExitSpeedMetersPerSecond;
            }
            else
            {
                var fullDriveEndSpeedMetersPerSecond = CalculateMidpointDriveEndSpeedMetersPerSecond(
                    currentSpeedMetersPerSecond,
                    stepDistanceMeters,
                    referenceAvailableDriveForceNewtons,
                    setup);
                endSpeedMetersPerSecond = ApplyPreparationBoundary(
                    currentSpeedMetersPerSecond,
                    fullDriveEndSpeedMetersPerSecond,
                    stepDistanceMeters,
                    cornerEntryDecelerationMetersPerSecondSquared,
                    allowedEndSpeed);
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
            distanceProgressMeters += stepDistanceMeters;
            observedDistance += stepDistanceMeters;
            motionNodes?.Add(new((float)observedDistance, (float)travelTimeSeconds, currentSpeedMetersPerSecond));
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
            (float)decelerationDistanceMeters,
            equilibrium)
        {
            CalibrationSteps = calibrationSteps is null
                ? null
                : Array.AsReadOnly(calibrationSteps.ToArray()),
        };
    }

    /// <summary>
    /// Segment-aware compatibility bridge. The geometric corner phase is
    /// validated and the unchanged TurnExit calculation remains the single
    /// numerical implementation.
    /// </summary>
    public static TurnExitDriveProfile CalculateForceBasedTurnExitDriveProfile(
        CornerPhaseContext cornerPhase,
        float initialSpeedMetersPerSecond,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface,
        float distanceMeters)
    {
        RequireCompatibilityCornerPhase(
            cornerPhase,
            SegmentType.TurnExit,
            nameof(cornerPhase));
        return CalculateForceBasedTurnExitDriveProfile(
            initialSpeedMetersPerSecond,
            skills,
            setup,
            surface,
            distanceMeters);
    }

    public static TurnExitDriveProfile CalculateForceBasedTurnExitDriveProfile(
        float initialSpeedMetersPerSecond,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface,
        float distanceMeters)
    {
        ValidateNonNegativeFinite(initialSpeedMetersPerSecond, nameof(initialSpeedMetersPerSecond));
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(setup);
        ValidateNonNegativeFinite(distanceMeters, nameof(distanceMeters));

        var referenceAvailableDriveForceNewtons =
            CalculateTurnExitAvailableDriveForceNewtons(skills, setup, surface);
        var equilibrium = CalculateFullDriveEquilibriumSpeedMetersPerSecond(referenceAvailableDriveForceNewtons, setup);
        var entryNetAccelerationMetersPerSecondSquared =
            CalculateNetDriveAccelerationMetersPerSecondSquared(
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
                0f,
                entryNetAccelerationMetersPerSecondSquared,
                equilibrium);
        }

        var integrationStepsMeters = CreateLongitudinalIntegrationSteps(distanceMeters);
        var currentSpeedMetersPerSecond = initialSpeedMetersPerSecond;
        var peakSpeedMetersPerSecond = initialSpeedMetersPerSecond;
        var travelTimeSeconds = 0d;
        var accelerationDistanceMeters = 0d;
        var cruiseDistanceMeters = 0d;
        var decelerationDistanceMeters = 0d;
        var lastPhase = StraightDistancePhase.Cruise;

        foreach (var integrationStepMeters in integrationStepsMeters)
        {
            var stepDistanceMeters = (float)integrationStepMeters;
            var endSpeedMetersPerSecond = CalculateMidpointDriveEndSpeedMetersPerSecond(
                currentSpeedMetersPerSecond,
                stepDistanceMeters,
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
            var speedChange = endSpeedMetersPerSecond - currentSpeedMetersPerSecond;
            if (speedChange > LongitudinalPhaseSpeedToleranceMetersPerSecond)
            {
                accelerationDistanceMeters += stepDistanceMeters;
                lastPhase = StraightDistancePhase.Acceleration;
            }
            else if (speedChange < -LongitudinalPhaseSpeedToleranceMetersPerSecond)
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

        ReconcileStraightPhaseDistance(distanceMeters, lastPhase,
            ref accelerationDistanceMeters, ref cruiseDistanceMeters, ref decelerationDistanceMeters);

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
            (float)decelerationDistanceMeters,
            entryNetAccelerationMetersPerSecondSquared,
            equilibrium);
    }

    // Shared by ordinary Straight and standing start. Select the fastest feasible
    // corrected step without exceeding full drive or adding unavailable roll-off.
    internal static float ApplyPreparationBoundary(
        float startSpeedMetersPerSecond,
        float fullDriveEndSpeedMetersPerSecond,
        float distanceMeters,
        float decelerationMetersPerSecondSquared,
        float? allowedEndSpeedMetersPerSecond)
    {
        if (allowedEndSpeedMetersPerSecond is not { } boundary
            || fullDriveEndSpeedMetersPerSecond <= boundary)
            return fullDriveEndSpeedMetersPerSecond;
        // Natural resistance may slow even more strongly than the preparation
        // capability. Preparation must never raise that full-drive candidate.
        return Math.Min(fullDriveEndSpeedMetersPerSecond, Math.Max(boundary, DecelerateOverDistance(
            startSpeedMetersPerSecond, decelerationMetersPerSecondSquared, distanceMeters)));
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
            var exitSpeed = Math.Min(speedCeilingMetersPerSecond, AccelerateOverDistance(
                initialSpeedMetersPerSecond, accelerationMetersPerSecondSquared, distanceMeters));
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

    private static void RequireCompatibilityCornerPhase(
        CornerPhaseContext cornerPhase,
        SegmentType requiredCompatibilityType,
        string parameterName)
    {
        if (cornerPhase.CompatibilitySegmentType != requiredCompatibilityType)
        {
            throw new ArgumentException(
                $"Corner phase must describe {requiredCompatibilityType}.",
                parameterName);
        }
    }
}
