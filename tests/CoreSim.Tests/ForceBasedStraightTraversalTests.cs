using CoreSim;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class ForceBasedStraightTraversalTests
{
    private static readonly RiderSkills NeutralSkills =
        new(50f, 50f, 50f, 50f, 50f, 50f);
    private static readonly BikeSetup NeutralSetup =
        new(gearing: 0.5f, tractionBias: 0.5f);
    private static readonly TrackSurfaceState PerfectSurface = new(1f, 0f, 0.35f);

    [Fact]
    public void StraightReferenceDriveAccelerationUsesExistingStraightCalibration()
    {
        var existing = LongitudinalDynamics.CalculateStraightAccelerationMetersPerSecondSquared(
            NeutralSkills,
            PerfectSurface);

        var reference = LongitudinalDynamics
            .CalculateStraightReferenceDriveAccelerationMetersPerSecondSquared(
                NeutralSkills,
                NeutralSetup,
                PerfectSurface);

        Assert.Equal(existing, reference, 5);
    }

    [Fact]
    public void DriveOrientedGearingRaisesStraightReferenceDrive()
    {
        var driveOriented = StraightReferenceAcceleration(gearing: 0f);
        var speedOriented = StraightReferenceAcceleration(gearing: 1f);

        Assert.Equal(1.10f / 0.90f, driveOriented / speedOriented, 5);
        Assert.True(driveOriented > speedOriented);
    }

    [Fact]
    public void StraightReferenceForcePreservesReferenceAccelerationAtSixteenMetersPerSecond()
    {
        var speed = LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond;
        var referenceForce = LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(
            NeutralSkills,
            NeutralSetup,
            PerfectSurface);
        var resistance = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed);
        var expectedAcceleration = StraightReferenceAcceleration(gearing: 0.5f);

        var actualAcceleration = LongitudinalDynamics
            .CalculateAccelerationFromForcesMetersPerSecondSquared(
                referenceForce,
                resistance,
                LongitudinalDynamics.ProvisionalNominalSystemMassKilograms);

        Assert.Equal(expectedAcceleration, actualAcceleration, 5);
    }

    [Fact]
    public void StraightNetAccelerationUsesSameEnvelopeShapeAsTurnExit()
    {
        const float speed = 24f;
        var referenceForce = LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(
            NeutralSkills,
            NeutralSetup,
            PerfectSurface);
        var expectedEnvelope = LongitudinalDynamics.CalculateTurnExitDriveEnvelopeMultiplier(
            speed,
            NeutralSetup);
        var expectedAvailableForce = LongitudinalDynamics
            .CalculateAvailableDriveForceAtSpeedNewtons(
            referenceForce,
            speed,
            NeutralSetup);
        var expectedResistance = LongitudinalDynamics
            .CalculateLongitudinalResistanceForceNewtons(speed);
        var expectedAcceleration = LongitudinalDynamics
            .CalculateAccelerationFromForcesMetersPerSecondSquared(
                expectedAvailableForce,
                expectedResistance,
                LongitudinalDynamics.ProvisionalNominalSystemMassKilograms);

        var actualAcceleration = LongitudinalDynamics
            .CalculateStraightNetAccelerationMetersPerSecondSquared(
                speed,
                NeutralSkills,
                NeutralSetup,
                PerfectSurface);

        Assert.Equal(referenceForce * expectedEnvelope, expectedAvailableForce, 5);
        Assert.Equal(expectedAcceleration, actualAcceleration);
        Assert.Equal(
            expectedEnvelope,
            LongitudinalDynamics.CalculatePositiveDriveEnvelopeMultiplier(speed, NeutralSetup));
    }

    [Fact]
    public void StraightNetAccelerationFallsAboveReferenceSpeed()
    {
        var atReference = StraightNetAcceleration(
            LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond,
            gearing: 0.5f);
        var aboveReference = StraightNetAcceleration(23f, gearing: 0.5f);

        Assert.True(aboveReference < atReference);
    }

    [Fact]
    public void StraightSpeedOrientedDriveRetainsForceLongerAtHighSpeed()
    {
        var driveOriented = StraightNetAcceleration(28f, gearing: 0f);
        var speedOriented = StraightNetAcceleration(28f, gearing: 1f);

        Assert.True(speedOriented > driveOriented);
        Assert.True(speedOriented > 0f);
    }

    [Fact]
    public void ForceBasedStraightZeroDistancePreservesSpeedAndZeroTime()
    {
        var profile = Profile(initialSpeed: 19f, distance: 0f, ceiling: 23f, target: 12f);

        Assert.Equal(19f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(19f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(0f, profile.TravelTimeSeconds);
        Assert.Equal(0f, TotalClassifiedDistance(profile));
    }

    [Fact]
    public void ForceBasedStraightConsumesExactRequestedDistance()
    {
        const float distance = 60.4f;

        var profile = Profile(initialSpeed: 10f, distance, ceiling: 30f);

        Assert.Equal(distance, TotalClassifiedDistance(profile));
    }

    [Fact]
    public void ForceBasedStraightUsesOneMeterStepsAndFinalRemainderWithoutDroppingDistance()
    {
        const float distance = 60.4f;
        var whole = Profile(initialSpeed: 10f, distance, ceiling: 30f);
        var currentSpeed = 10f;
        var composedTime = 0f;
        var composedAccelerationDistance = 0f;
        var composedCruiseDistance = 0f;
        var composedDecelerationDistance = 0f;

        for (var index = 0; index < 60; index++)
        {
            var step = Profile(currentSpeed, distance: 1f, ceiling: 30f);
            currentSpeed = step.ExitSpeedMetersPerSecond;
            composedTime += step.TravelTimeSeconds;
            composedAccelerationDistance += step.AccelerationDistanceMeters;
            composedCruiseDistance += step.CruiseDistanceMeters;
            composedDecelerationDistance += step.DecelerationDistanceMeters;
        }

        var remainder = Profile(currentSpeed, distance - 60f, ceiling: 30f);
        currentSpeed = remainder.ExitSpeedMetersPerSecond;
        composedTime += remainder.TravelTimeSeconds;
        composedAccelerationDistance += remainder.AccelerationDistanceMeters;
        composedCruiseDistance += remainder.CruiseDistanceMeters;
        composedDecelerationDistance += remainder.DecelerationDistanceMeters;

        Assert.Equal(whole.ExitSpeedMetersPerSecond, currentSpeed);
        Assert.Equal(whole.TravelTimeSeconds, composedTime, 4);
        Assert.Equal(whole.AccelerationDistanceMeters, composedAccelerationDistance, 4);
        Assert.Equal(whole.CruiseDistanceMeters, composedCruiseDistance, 4);
        Assert.Equal(whole.DecelerationDistanceMeters, composedDecelerationDistance, 4);
    }

    [Fact]
    public void StraightTravelTimeEqualsSumOfStepTimes()
    {
        var whole = Profile(initialSpeed: 10f, distance: 12.4f, ceiling: 30f);
        var currentSpeed = 10f;
        var expectedTime = 0d;

        for (var index = 0; index < 12; index++)
        {
            var step = Profile(currentSpeed, distance: 1f, ceiling: 30f);
            expectedTime += 2d / (currentSpeed + step.ExitSpeedMetersPerSecond);
            currentSpeed = step.ExitSpeedMetersPerSecond;
        }

        var remainderDistance = 0.4f;
        var remainder = Profile(currentSpeed, remainderDistance, ceiling: 30f);
        expectedTime += 2d * remainderDistance
            / (currentSpeed + remainder.ExitSpeedMetersPerSecond);

        Assert.Equal((float)expectedTime, whole.TravelTimeSeconds, 5);
    }

    [Fact]
    public void MidpointIntegrationIsDeterministic()
    {
        var first = Profile(initialSpeed: 11f, distance: 93.7f, ceiling: 24f, target: 14f);
        var second = Profile(initialSpeed: 11f, distance: 93.7f, ceiling: 24f, target: 14f);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ForceBasedStraightCanReachRetainedCeilingOnLongEnoughStraight()
    {
        var profile = Profile(initialSpeed: 10f, distance: 500f, ceiling: 23f);

        Assert.Equal(23f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(23f, profile.PeakSpeedMetersPerSecond);
        Assert.True(profile.AccelerationDistanceMeters > 0f);
        Assert.True(profile.CruiseDistanceMeters > 0f);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
    }

    [Fact]
    public void InitialOverspeedAboveCeilingIsPreservedWithoutTarget()
    {
        var profile = Profile(initialSpeed: 25f, distance: 60f, ceiling: 23f);

        Assert.Equal(25f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(25f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(60f, profile.CruiseDistanceMeters);
    }

    [Fact]
    public void ReachableNextTurnTargetIsMetWithoutOvershoot()
    {
        const float target = 14f;

        var profile = Profile(initialSpeed: 10f, distance: 100f, ceiling: 23f, target);

        Assert.Equal(target, profile.ExitSpeedMetersPerSecond, 5);
        Assert.True(profile.PeakSpeedMetersPerSecond > target);
        Assert.True(profile.AccelerationDistanceMeters > 0f);
        Assert.True(profile.DecelerationDistanceMeters > 0f);
    }

    [Fact]
    public void InsufficientDistanceLeavesResidualOverspeed()
    {
        const float initialSpeed = 25f;
        const float target = 10f;
        const float distance = 2f;
        var deceleration = CornerEntryDeceleration();
        var expected = LongitudinalDynamics.DecelerateOverDistance(
            initialSpeed,
            deceleration,
            distance);

        var profile = Profile(initialSpeed, distance, ceiling: 30f, target);

        Assert.True(profile.ExitSpeedMetersPerSecond > target);
        Assert.Equal(expected, profile.ExitSpeedMetersPerSecond, 5);
        Assert.Equal(distance, profile.DecelerationDistanceMeters);
    }

    [Fact]
    public void TargetAboveCeilingDoesNotCauseUnnecessaryDeceleration()
    {
        var profile = Profile(initialSpeed: 10f, distance: 500f, ceiling: 23f, target: 25f);

        Assert.Equal(23f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
    }

    [Fact]
    public void MidpointIntegrationIsCloseToFineDistanceReference()
    {
        const float initialSpeed = 10f;
        const float distance = 60f;
        const float ceiling = 30f;
        var profile = Profile(initialSpeed, distance, ceiling);
        var fine = IntegrateWithoutTarget(initialSpeed, distance, ceiling, stepMeters: 0.05f);

        Assert.InRange(
            MathF.Abs(profile.ExitSpeedMetersPerSecond - fine.ExitSpeedMetersPerSecond),
            0f,
            0.01f);
        Assert.InRange(
            MathF.Abs(profile.TravelTimeSeconds - fine.TravelTimeSeconds),
            0f,
            0.01f);
    }

    [Fact]
    public void ForceBasedStraightPhaseDistancesSumToTotalDistance()
    {
        const float distance = 300.75f;
        var profile = Profile(initialSpeed: 10f, distance, ceiling: 23f, target: 12f);

        Assert.True(profile.AccelerationDistanceMeters > 0f);
        Assert.True(profile.CruiseDistanceMeters > 0f);
        Assert.True(profile.DecelerationDistanceMeters > 0f);
        Assert.Equal(distance, TotalClassifiedDistance(profile));
    }

    [Fact]
    public void ForceBasedStraightAcceleratesWithoutNextTurnTarget()
    {
        var profile = Profile(initialSpeed: 10f, distance: 60f, ceiling: 23f);

        Assert.True(profile.ExitSpeedMetersPerSecond > 10f);
        Assert.True(profile.TravelTimeSeconds > 0f);
    }

    [Fact]
    public void ForceBasedStraightDoesNotPositiveDrivePastRetainedCeiling()
    {
        var profile = Profile(initialSpeed: 10f, distance: 1_000f, ceiling: 22f);

        Assert.Equal(22f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(22f, profile.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void LongStraightCanExposeGearingDriveSpeedTradeOff()
    {
        var driveSetup = new BikeSetup(gearing: 0f, tractionBias: 0.5f);
        var speedSetup = new BikeSetup(gearing: 1f, tractionBias: 0.5f);
        var driveShort = ProfileForSetup(10f, 10f, driveSetup);
        var speedShort = ProfileForSetup(10f, 10f, speedSetup);
        var driveLong = ProfileForSetup(10f, 500f, driveSetup);
        var speedLong = ProfileForSetup(10f, 500f, speedSetup);

        Assert.True(driveShort.ExitSpeedMetersPerSecond > speedShort.ExitSpeedMetersPerSecond);
        Assert.True(speedLong.ExitSpeedMetersPerSecond > driveLong.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void BackwardEnvelopeNeverRequiresMoreThanAvailableDeceleration()
    {
        const float initialSpeed = 25f;
        const float distance = 2f;
        var profile = Profile(initialSpeed, distance, ceiling: 30f, target: 10f);
        var maximumSquaredSpeedReduction = 2f * CornerEntryDeceleration() * distance;
        var actualSquaredSpeedReduction = initialSpeed * initialSpeed
            - profile.ExitSpeedMetersPerSecond * profile.ExitSpeedMetersPerSecond;

        Assert.True(actualSquaredSpeedReduction <= maximumSquaredSpeedReduction + 0.001f);
    }

    [Fact]
    public void TargetAboveFullDriveExitDoesNotCauseUnnecessaryPreparation()
    {
        var withoutTarget = Profile(initialSpeed: 10f, distance: 10f, ceiling: 23f);
        var withHighTarget = Profile(
            initialSpeed: 10f,
            distance: 10f,
            ceiling: 23f,
            target: 22f);

        Assert.Equal(withoutTarget, withHighTarget);
    }

    [Fact]
    public void InitialOverspeedCanDecelerateTowardTarget()
    {
        var profile = Profile(initialSpeed: 25f, distance: 100f, ceiling: 23f, target: 14f);

        Assert.True(profile.ExitSpeedMetersPerSecond < 25f);
        Assert.True(profile.DecelerationDistanceMeters > 0f);
        Assert.Equal(25f, profile.PeakSpeedMetersPerSecond);
    }

    [Fact]
    public void FastestFeasibleProfileAcceleratesBeforePreparingWhenDistanceAllows()
    {
        var profile = Profile(initialSpeed: 10f, distance: 100f, ceiling: 23f, target: 14f);

        Assert.True(profile.AccelerationDistanceMeters > 0f);
        Assert.True(profile.DecelerationDistanceMeters > 0f);
    }

    [Fact]
    public void PeakSpeedOccursBeforeTargetPreparationWhenBothPhasesExist()
    {
        const float initialSpeed = 10f;
        const float target = 14f;
        var profile = Profile(initialSpeed, distance: 100f, ceiling: 23f, target);

        Assert.True(profile.AccelerationDistanceMeters > 0f);
        Assert.True(profile.DecelerationDistanceMeters > 0f);
        Assert.True(profile.PeakSpeedMetersPerSecond > initialSpeed);
        Assert.True(profile.PeakSpeedMetersPerSecond > target);
    }

    [Fact]
    public void StraightTravelTimeIsFiniteAndPositiveForPositiveDistance()
    {
        var profile = Profile(initialSpeed: 10f, distance: 60.4f, ceiling: 23f);

        Assert.True(float.IsFinite(profile.TravelTimeSeconds));
        Assert.True(profile.TravelTimeSeconds > 0f);
    }

    private static StraightSpeedProfile Profile(
        float initialSpeed,
        float distance,
        float ceiling,
        float? target = null)
        => LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            initialSpeed,
            NeutralSkills,
            NeutralSetup,
            PerfectSurface,
            CornerEntryDeceleration(),
            distance,
            ceiling,
            target);

    private static StraightSpeedProfile ProfileForSetup(
        float initialSpeed,
        float distance,
        BikeSetup setup)
        => LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            initialSpeed,
            NeutralSkills,
            setup,
            PerfectSurface,
            CornerEntryDeceleration(),
            distance,
            LongitudinalDynamics.CalculateAttainableTopSpeedMetersPerSecond(
                NeutralSkills,
                setup));

    private static float StraightReferenceAcceleration(float gearing)
        => LongitudinalDynamics.CalculateStraightReferenceDriveAccelerationMetersPerSecondSquared(
            NeutralSkills,
            new BikeSetup(gearing, tractionBias: 0.5f),
            PerfectSurface);

    private static float StraightNetAcceleration(float speed, float gearing)
        => LongitudinalDynamics.CalculateStraightNetAccelerationMetersPerSecondSquared(
            speed,
            NeutralSkills,
            new BikeSetup(gearing, tractionBias: 0.5f),
            PerfectSurface);

    private static float CornerEntryDeceleration()
        => LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(
            NeutralSkills,
            PerfectSurface);

    private static float TotalClassifiedDistance(StraightSpeedProfile profile)
        => profile.AccelerationDistanceMeters
            + profile.CruiseDistanceMeters
            + profile.DecelerationDistanceMeters;

    private static FineProfile IntegrateWithoutTarget(
        float initialSpeed,
        float distance,
        float ceiling,
        float stepMeters)
    {
        var currentSpeed = initialSpeed;
        var travelTime = 0d;
        var remainingDistance = (double)distance;
        while (remainingDistance > 0d)
        {
            var stepDistance = (float)Math.Min(stepMeters, remainingDistance);
            var accelerationAtStart = LongitudinalDynamics
                .CalculateStraightNetAccelerationMetersPerSecondSquared(
                    currentSpeed,
                    NeutralSkills,
                    NeutralSetup,
                    PerfectSurface);
            var predictedSpeed = LongitudinalDynamics.AccelerateOverDistanceWithSpeedCeiling(
                currentSpeed,
                accelerationAtStart,
                stepDistance,
                ceiling);
            var midpointSpeed = (currentSpeed + predictedSpeed) * 0.5f;
            var midpointAcceleration = LongitudinalDynamics
                .CalculateStraightNetAccelerationMetersPerSecondSquared(
                    midpointSpeed,
                    NeutralSkills,
                    NeutralSetup,
                    PerfectSurface);
            var endSpeed = LongitudinalDynamics.AccelerateOverDistanceWithSpeedCeiling(
                currentSpeed,
                midpointAcceleration,
                stepDistance,
                ceiling);
            travelTime += 2d * stepDistance / (currentSpeed + endSpeed);
            currentSpeed = endSpeed;
            remainingDistance -= stepDistance;
        }

        return new FineProfile(currentSpeed, (float)travelTime);
    }

    private sealed record FineProfile(
        float ExitSpeedMetersPerSecond,
        float TravelTimeSeconds);
}
