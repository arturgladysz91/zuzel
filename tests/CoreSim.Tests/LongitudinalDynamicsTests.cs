using CoreSim;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class LongitudinalDynamicsTests
{
    private static readonly TrackSurfaceState PerfectDriveSurface = new(1f, 0f, 0.35f);

    [Fact]
    public void AccelerateOverDistanceUsesKinematicEquation()
    {
        var result = LongitudinalDynamics.AccelerateOverDistance(
            initialSpeedMetersPerSecond: 10f,
            accelerationMetersPerSecondSquared: 2f,
            distanceMeters: 25f);

        Assert.Equal(MathF.Sqrt(200f), result, 5);
    }

    [Fact]
    public void ZeroDistancePreservesSpeed()
    {
        var result = LongitudinalDynamics.AccelerateOverDistance(10f, 2f, 0f);

        Assert.Equal(10f, result);
    }

    [Fact]
    public void ZeroAccelerationPreservesSpeed()
    {
        var result = LongitudinalDynamics.AccelerateOverDistance(10f, 0f, 25f);

        Assert.Equal(10f, result);
    }

    [Fact]
    public void DecelerateOverDistanceUsesKinematicEquation()
    {
        var result = LongitudinalDynamics.DecelerateOverDistance(
            initialSpeedMetersPerSecond: 20f,
            decelerationMetersPerSecondSquared: 2f,
            distanceMeters: 25f);

        Assert.Equal(MathF.Sqrt(300f), result, 5);
    }

    [Fact]
    public void DecelerateZeroDistancePreservesSpeed()
    {
        var result = LongitudinalDynamics.DecelerateOverDistance(20f, 2f, 0f);

        Assert.Equal(20f, result);
    }

    [Fact]
    public void DecelerateZeroRatePreservesSpeed()
    {
        var result = LongitudinalDynamics.DecelerateOverDistance(20f, 0f, 25f);

        Assert.Equal(20f, result);
    }

    [Fact]
    public void DecelerateOverDistanceStopsAtZeroWithoutNaN()
    {
        var result = LongitudinalDynamics.DecelerateOverDistance(1f, 10f, 10f);

        Assert.Equal(0f, result);
        Assert.True(float.IsFinite(result));
    }

    [Theory]
    [InlineData(-1f, 1f, 1f)]
    [InlineData(float.NaN, 1f, 1f)]
    [InlineData(float.PositiveInfinity, 1f, 1f)]
    [InlineData(float.NegativeInfinity, 1f, 1f)]
    [InlineData(1f, -1f, 1f)]
    [InlineData(1f, float.NaN, 1f)]
    [InlineData(1f, float.PositiveInfinity, 1f)]
    [InlineData(1f, float.NegativeInfinity, 1f)]
    [InlineData(1f, 1f, -1f)]
    [InlineData(1f, 1f, float.NaN)]
    [InlineData(1f, 1f, float.PositiveInfinity)]
    [InlineData(1f, 1f, float.NegativeInfinity)]
    public void InvalidKinematicInputsAreRejected(
        float initialSpeedMetersPerSecond,
        float accelerationMetersPerSecondSquared,
        float distanceMeters)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LongitudinalDynamics.AccelerateOverDistance(
                initialSpeedMetersPerSecond,
                accelerationMetersPerSecondSquared,
                distanceMeters));
    }

    [Theory]
    [InlineData(-1f, 1f, 1f)]
    [InlineData(float.NaN, 1f, 1f)]
    [InlineData(float.PositiveInfinity, 1f, 1f)]
    [InlineData(float.NegativeInfinity, 1f, 1f)]
    [InlineData(1f, -1f, 1f)]
    [InlineData(1f, float.NaN, 1f)]
    [InlineData(1f, float.PositiveInfinity, 1f)]
    [InlineData(1f, float.NegativeInfinity, 1f)]
    [InlineData(1f, 1f, -1f)]
    [InlineData(1f, 1f, float.NaN)]
    [InlineData(1f, 1f, float.PositiveInfinity)]
    [InlineData(1f, 1f, float.NegativeInfinity)]
    public void DecelerateRejectsInvalidInputs(
        float initialSpeedMetersPerSecond,
        float decelerationMetersPerSecondSquared,
        float distanceMeters)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LongitudinalDynamics.DecelerateOverDistance(
                initialSpeedMetersPerSecond,
                decelerationMetersPerSecondSquared,
                distanceMeters));
    }

    [Fact]
    public void HigherSpeedSkillProducesMoreTurnExitAcceleration()
    {
        var low = CalculateAcceleration(speedSkill: 0f, gearing: 0.5f, PerfectDriveSurface);
        var neutral = CalculateAcceleration(speedSkill: 50f, gearing: 0.5f, PerfectDriveSurface);
        var high = CalculateAcceleration(speedSkill: 100f, gearing: 0.5f, PerfectDriveSurface);

        Assert.Equal(0.60f, low, 5);
        Assert.Equal(1.00f, neutral, 5);
        Assert.Equal(1.40f, high, 5);
        Assert.True(high > low);
    }

    [Fact]
    public void LowerGearingProducesMoreTurnExitDrive()
    {
        var lowGearing = CalculateAcceleration(speedSkill: 50f, gearing: 0f, PerfectDriveSurface);
        var neutralGearing = CalculateAcceleration(speedSkill: 50f, gearing: 0.5f, PerfectDriveSurface);
        var highGearing = CalculateAcceleration(speedSkill: 50f, gearing: 1f, PerfectDriveSurface);

        Assert.Equal(1.10f, lowGearing, 5);
        Assert.Equal(1.00f, neutralGearing, 5);
        Assert.Equal(0.90f, highGearing, 5);
        Assert.True(lowGearing > highGearing);
    }

    [Fact]
    public void BetterEffectiveGripProducesMoreDrive()
    {
        var noEffectiveGrip = new TrackSurfaceState(0f, 0f, 0.35f);
        var low = CalculateAcceleration(speedSkill: 50f, gearing: 0.5f, noEffectiveGrip);
        var high = CalculateAcceleration(speedSkill: 50f, gearing: 0.5f, PerfectDriveSurface);

        Assert.Equal(0.75f, low, 5);
        Assert.Equal(1.00f, high, 5);
        Assert.True(high > low);
    }

    [Fact]
    public void ResistanceAtZeroSpeedEqualsBaseResistance()
    {
        var resistance = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(0f);

        Assert.Equal(LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons, resistance);
    }

    [Fact]
    public void ResistanceUsesQuadraticSpeedTerm()
    {
        const float speed = 20f;
        var expected = LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons
            + LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient * speed * speed;

        var resistance = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed);

        Assert.Equal(expected, resistance, 5);
    }

    [Fact]
    public void ResistanceIncreasesWithSpeed()
    {
        var atTen = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(10f);
        var atTwenty = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(20f);
        var atThirty = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(30f);

        Assert.True(atThirty > atTwenty);
        Assert.True(atTwenty > atTen);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ResistanceRejectsInvalidSpeed(float speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed));
    }

    [Fact]
    public void AccelerationFromForcesUsesFEqualsMA()
    {
        var acceleration = LongitudinalDynamics
            .CalculateAccelerationFromForcesMetersPerSecondSquared(
                availableDriveForceNewtons: 300f,
                resistanceForceNewtons: 100f,
                systemMassKilograms: 100f);

        Assert.Equal(2f, acceleration);
    }

    [Fact]
    public void AccelerationFromForcesNeverReturnsNegative()
    {
        var acceleration = LongitudinalDynamics
            .CalculateAccelerationFromForcesMetersPerSecondSquared(
                availableDriveForceNewtons: 100f,
                resistanceForceNewtons: 300f,
                systemMassKilograms: 100f);

        Assert.Equal(0f, acceleration);
    }

    [Theory]
    [InlineData(-1f, 0f, 100f)]
    [InlineData(float.NaN, 0f, 100f)]
    [InlineData(float.PositiveInfinity, 0f, 100f)]
    [InlineData(float.NegativeInfinity, 0f, 100f)]
    [InlineData(100f, -1f, 100f)]
    [InlineData(100f, float.NaN, 100f)]
    [InlineData(100f, float.PositiveInfinity, 100f)]
    [InlineData(100f, float.NegativeInfinity, 100f)]
    [InlineData(100f, 0f, 0f)]
    [InlineData(100f, 0f, -1f)]
    [InlineData(100f, 0f, float.NaN)]
    [InlineData(100f, 0f, float.PositiveInfinity)]
    [InlineData(100f, 0f, float.NegativeInfinity)]
    public void AccelerationFromForcesRejectsInvalidInputs(
        float availableDriveForceNewtons,
        float resistanceForceNewtons,
        float systemMassKilograms)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LongitudinalDynamics.CalculateAccelerationFromForcesMetersPerSecondSquared(
                availableDriveForceNewtons,
                resistanceForceNewtons,
                systemMassKilograms));
    }

    [Fact]
    public void ReferenceSpeedPreservesExistingTurnExitAcceleration()
    {
        var existing = CalculateAcceleration(50f, 0.5f, PerfectDriveSurface);
        var forceBased = NetTurnExitAcceleration(
            LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond,
            50f,
            0.5f,
            PerfectDriveSurface);

        Assert.Equal(existing, forceBased, 5);
    }

    [Theory]
    [InlineData(0f, 0f, 0.35f, 0.6f, 0.75f)]
    [InlineData(50f, 0.5f, 0.65f, 0.3f, 0.5f)]
    [InlineData(100f, 1f, 1f, 0f, 0.35f)]
    public void ReferenceSpeedInvariantAcrossSkillSetupAndSurface(
        float speedSkill,
        float gearing,
        float grip,
        float ruts,
        float moisture)
    {
        var surface = new TrackSurfaceState(grip, ruts, moisture);
        var existing = CalculateAcceleration(speedSkill, gearing, surface);
        var forceBased = NetTurnExitAcceleration(
            LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond,
            speedSkill,
            gearing,
            surface);

        Assert.Equal(existing, forceBased, 5);
    }

    [Fact]
    public void LowerSpeedHasMoreNetPositiveDriveThanReference()
    {
        var lower = NetTurnExitAcceleration(10f, 50f, 0.5f, PerfectDriveSurface);
        var reference = NetTurnExitAcceleration(16f, 50f, 0.5f, PerfectDriveSurface);

        Assert.True(lower > reference);
    }

    [Fact]
    public void HigherSpeedHasLessNetPositiveDriveThanReference()
    {
        var reference = NetTurnExitAcceleration(16f, 50f, 0.5f, PerfectDriveSurface);
        var higher = NetTurnExitAcceleration(25f, 50f, 0.5f, PerfectDriveSurface);

        Assert.True(higher < reference);
    }

    [Fact]
    public void SufficientlyHighSpeedCanReducePositiveDriveToZero()
    {
        var acceleration = NetTurnExitAcceleration(100f, 50f, 0.5f, PerfectDriveSurface);

        Assert.Equal(0f, acceleration);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(10f)]
    [InlineData(16f)]
    [InlineData(25f)]
    [InlineData(50f)]
    [InlineData(100f)]
    public void NetPositiveDriveNeverBecomesNegativeAcrossWideSpeedRange(float speed)
    {
        var acceleration = NetTurnExitAcceleration(
            speed,
            50f,
            0.5f,
            PerfectDriveSurface);

        Assert.True(acceleration >= 0f);
    }

    [Fact]
    public void HigherSpeedSkillProducesMoreAvailableTurnExitDriveForce()
    {
        var low = AvailableTurnExitDriveForce(0f, 0.5f, PerfectDriveSurface);
        var high = AvailableTurnExitDriveForce(100f, 0.5f, PerfectDriveSurface);

        Assert.True(high > low);
    }

    [Fact]
    public void DriveOrientedGearingProducesMoreAvailableTurnExitDriveForce()
    {
        var driveOriented = AvailableTurnExitDriveForce(50f, 0f, PerfectDriveSurface);
        var speedOriented = AvailableTurnExitDriveForce(50f, 1f, PerfectDriveSurface);

        Assert.True(driveOriented > speedOriented);
    }

    [Fact]
    public void BetterGripProducesMoreAvailableTurnExitDriveForce()
    {
        var noEffectiveGrip = new TrackSurfaceState(0f, 0f, 0.35f);
        var poor = AvailableTurnExitDriveForce(50f, 0.5f, noEffectiveGrip);
        var good = AvailableTurnExitDriveForce(50f, 0.5f, PerfectDriveSurface);

        Assert.True(good > poor);
    }

    [Fact]
    public void GearingIsNotDoubleCountedInAvailableDriveForce()
    {
        const float gearing = 0.2f;
        var referenceAcceleration = CalculateAcceleration(50f, gearing, PerfectDriveSurface);
        var referenceResistance = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(
            LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond);
        var expected = LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
            * referenceAcceleration
            + referenceResistance;

        var actual = AvailableTurnExitDriveForce(50f, gearing, PerfectDriveSurface);

        Assert.Equal(expected, actual, 5);
    }

    [Fact]
    public void AttainableTopSpeedUsesSpeedSkill()
    {
        var low = AttainableTopSpeed(speedSkill: 0f, gearing: 0.5f);
        var neutral = AttainableTopSpeed(speedSkill: 50f, gearing: 0.5f);
        var high = AttainableTopSpeed(speedSkill: 100f, gearing: 0.5f);

        Assert.Equal(21f, low, 5);
        Assert.Equal(23f, neutral, 5);
        Assert.Equal(25f, high, 5);
        Assert.True(high > low);
    }

    [Fact]
    public void HigherGearingRaisesAttainableTopSpeed()
    {
        var lowGearing = AttainableTopSpeed(speedSkill: 50f, gearing: 0f);
        var highGearing = AttainableTopSpeed(speedSkill: 50f, gearing: 1f);

        Assert.Equal(23f * 0.94f, lowGearing, 5);
        Assert.Equal(23f * 1.06f, highGearing, 5);
        Assert.True(highGearing > lowGearing);
    }

    [Fact]
    public void GearingCreatesRealTradeOff()
    {
        var lowGearingDrive = CalculateAcceleration(50f, gearing: 0f, PerfectDriveSurface);
        var highGearingDrive = CalculateAcceleration(50f, gearing: 1f, PerfectDriveSurface);
        var lowGearingTopSpeed = AttainableTopSpeed(50f, gearing: 0f);
        var highGearingTopSpeed = AttainableTopSpeed(50f, gearing: 1f);

        Assert.True(lowGearingDrive > highGearingDrive);
        Assert.True(lowGearingTopSpeed < highGearingTopSpeed);
    }

    [Fact]
    public void SpeedCeilingHelperStopsPositiveDriveAtCeiling()
    {
        var result = LongitudinalDynamics.AccelerateOverDistanceWithSpeedCeiling(
            initialSpeedMetersPerSecond: 20f,
            accelerationMetersPerSecondSquared: 2f,
            distanceMeters: 100f,
            speedCeilingMetersPerSecond: 23f);

        Assert.Equal(23f, result);
    }

    [Fact]
    public void SpeedCeilingHelperDoesNotReduceExistingOverspeed()
    {
        var result = LongitudinalDynamics.AccelerateOverDistanceWithSpeedCeiling(
            initialSpeedMetersPerSecond: 25f,
            accelerationMetersPerSecondSquared: 2f,
            distanceMeters: 100f,
            speedCeilingMetersPerSecond: 23f);

        Assert.Equal(25f, result);
    }

    [Theory]
    [InlineData(10f, 2f, 0f)]
    [InlineData(10f, 0f, 25f)]
    public void SpeedCeilingHelperPreservesZeroDistanceAndZeroAcceleration(
        float initialSpeed,
        float acceleration,
        float distance)
    {
        var result = LongitudinalDynamics.AccelerateOverDistanceWithSpeedCeiling(
            initialSpeed,
            acceleration,
            distance,
            speedCeilingMetersPerSecond: 23f);

        Assert.Equal(initialSpeed, result);
    }

    [Theory]
    [InlineData(-1f, 1f, 1f, 20f)]
    [InlineData(float.NaN, 1f, 1f, 20f)]
    [InlineData(float.PositiveInfinity, 1f, 1f, 20f)]
    [InlineData(float.NegativeInfinity, 1f, 1f, 20f)]
    [InlineData(1f, -1f, 1f, 20f)]
    [InlineData(1f, float.NaN, 1f, 20f)]
    [InlineData(1f, float.PositiveInfinity, 1f, 20f)]
    [InlineData(1f, float.NegativeInfinity, 1f, 20f)]
    [InlineData(1f, 1f, -1f, 20f)]
    [InlineData(1f, 1f, float.NaN, 20f)]
    [InlineData(1f, 1f, float.PositiveInfinity, 20f)]
    [InlineData(1f, 1f, float.NegativeInfinity, 20f)]
    [InlineData(1f, 1f, 1f, 0f)]
    [InlineData(1f, 1f, 1f, -1f)]
    [InlineData(1f, 1f, 1f, float.NaN)]
    [InlineData(1f, 1f, 1f, float.PositiveInfinity)]
    [InlineData(1f, 1f, 1f, float.NegativeInfinity)]
    public void SpeedCeilingHelperRejectsInvalidInputs(
        float initialSpeed,
        float acceleration,
        float distance,
        float ceiling)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LongitudinalDynamics.AccelerateOverDistanceWithSpeedCeiling(
                initialSpeed,
                acceleration,
                distance,
                ceiling));
    }

    [Fact]
    public void StraightAccelerationUsesSpeedSkillAndEntryGrip()
    {
        var noEffectiveGrip = new TrackSurfaceState(0f, 0f, 0.35f);
        var lowSkill = StraightAcceleration(speedSkill: 0f, PerfectDriveSurface);
        var highSkill = StraightAcceleration(speedSkill: 100f, PerfectDriveSurface);
        var poorGrip = StraightAcceleration(speedSkill: 50f, noEffectiveGrip);
        var goodGrip = StraightAcceleration(speedSkill: 50f, PerfectDriveSurface);

        Assert.True(highSkill > lowSkill);
        Assert.True(goodGrip > poorGrip);
    }

    [Fact]
    public void CornerEntryDecelerationUsesSlideControlAndGrip()
    {
        var noEffectiveGrip = new TrackSurfaceState(0f, 0f, 0.35f);
        var lowControl = CornerEntryDeceleration(slideControl: 0f, PerfectDriveSurface);
        var highControl = CornerEntryDeceleration(slideControl: 100f, PerfectDriveSurface);
        var poorGrip = CornerEntryDeceleration(slideControl: 50f, noEffectiveGrip);
        var goodGrip = CornerEntryDeceleration(slideControl: 50f, PerfectDriveSurface);

        Assert.True(highControl > lowControl);
        Assert.True(goodGrip > poorGrip);
    }

    [Fact]
    public void StraightBelowCeilingKeepsPureAcceleration()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            initialSpeedMetersPerSecond: 10f,
            accelerationMetersPerSecondSquared: 1f,
            cornerEntryDecelerationMetersPerSecondSquared: 2f,
            distanceMeters: 20f,
            speedCeilingMetersPerSecond: 30f,
            targetExitSpeedMetersPerSecond: 15f);
        var expectedExit = MathF.Sqrt(140f);

        Assert.Equal(20f, profile.AccelerationDistanceMeters);
        Assert.Equal(0f, profile.CruiseDistanceMeters);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
        Assert.Equal(expectedExit, profile.PeakSpeedMetersPerSecond, 5);
        Assert.Equal(expectedExit, profile.ExitSpeedMetersPerSecond, 5);
    }

    [Fact]
    public void EntireDistanceDecelerationWhenAlreadyTooFastAndDistanceInsufficient()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            initialSpeedMetersPerSecond: 20f,
            accelerationMetersPerSecondSquared: 1f,
            cornerEntryDecelerationMetersPerSecondSquared: 2f,
            distanceMeters: 10f,
            speedCeilingMetersPerSecond: 30f,
            targetExitSpeedMetersPerSecond: 15f);

        Assert.Equal(0f, profile.AccelerationDistanceMeters);
        Assert.Equal(0f, profile.CruiseDistanceMeters);
        Assert.Equal(10f, profile.DecelerationDistanceMeters);
        Assert.Equal(20f, profile.PeakSpeedMetersPerSecond);
        Assert.True(profile.ExitSpeedMetersPerSecond > 15f);
        Assert.Equal(MathF.Sqrt(360f), profile.ExitSpeedMetersPerSecond, 5);
    }

    [Fact]
    public void AnalyticPeakBelowCeilingPreservesTwoPhaseProfile()
    {
        const float initialSpeed = 12f;
        const float targetSpeed = 14f;
        const float acceleration = 1.2f;
        const float deceleration = 2.4f;
        const float distance = 80f;
        var expectedPeakSquared =
            (2f * acceleration * deceleration * distance
             + deceleration * initialSpeed * initialSpeed
             + acceleration * targetSpeed * targetSpeed)
            / (acceleration + deceleration);
        var expectedPeak = MathF.Sqrt(expectedPeakSquared);
        var expectedAccelerationDistance =
            (expectedPeakSquared - initialSpeed * initialSpeed) / (2f * acceleration);
        var expectedDecelerationDistance =
            (expectedPeakSquared - targetSpeed * targetSpeed) / (2f * deceleration);

        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            initialSpeed,
            acceleration,
            deceleration,
            distance,
            speedCeilingMetersPerSecond: 30f,
            targetExitSpeedMetersPerSecond: targetSpeed);

        Assert.Equal(expectedPeak, profile.PeakSpeedMetersPerSecond, 5);
        Assert.Equal(expectedAccelerationDistance, profile.AccelerationDistanceMeters, 4);
        Assert.Equal(0f, profile.CruiseDistanceMeters);
        Assert.Equal(expectedDecelerationDistance, profile.DecelerationDistanceMeters, 4);
        Assert.Equal(distance, profile.AccelerationDistanceMeters + profile.DecelerationDistanceMeters, 4);
        Assert.Equal(targetSpeed, profile.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void ProfileTravelTimeUsesBothPhases()
    {
        const float initialSpeed = 14f;
        const float targetSpeed = 14f;
        const float distance = 80f;
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            initialSpeed,
            accelerationMetersPerSecondSquared: 1.2f,
            cornerEntryDecelerationMetersPerSecondSquared: 2.4f,
            distance,
            speedCeilingMetersPerSecond: 30f,
            targetExitSpeedMetersPerSecond: targetSpeed);
        var expectedTime =
            2f * profile.AccelerationDistanceMeters
            / (initialSpeed + profile.PeakSpeedMetersPerSecond)
            + 2f * profile.DecelerationDistanceMeters
            / (profile.PeakSpeedMetersPerSecond + targetSpeed);
        var endpointAverageTime = distance / ((initialSpeed + targetSpeed) * 0.5f);

        Assert.True(profile.PeakSpeedMetersPerSecond > initialSpeed);
        Assert.Equal(expectedTime, profile.TravelTimeSeconds, 5);
        Assert.NotEqual(endpointAverageTime, profile.TravelTimeSeconds, 4);
    }

    [Fact]
    public void StraightReachesCeilingThenCruises()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            initialSpeedMetersPerSecond: 10f,
            accelerationMetersPerSecondSquared: 2f,
            cornerEntryDecelerationMetersPerSecondSquared: 3f,
            distanceMeters: 200f,
            speedCeilingMetersPerSecond: 20f);

        Assert.Equal(20f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(20f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(75f, profile.AccelerationDistanceMeters, 5);
        Assert.Equal(125f, profile.CruiseDistanceMeters, 5);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
        Assert.Equal(200f, profile.AccelerationDistanceMeters + profile.CruiseDistanceMeters, 5);
    }

    [Fact]
    public void InitialSpeedAtCeilingCruisesEntireDistance()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            20f, 2f, 3f, distanceMeters: 100f, speedCeilingMetersPerSecond: 20f);

        Assert.Equal(0f, profile.AccelerationDistanceMeters);
        Assert.Equal(100f, profile.CruiseDistanceMeters);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
        Assert.Equal(20f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(20f, profile.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void InitialSpeedAboveCeilingIsNotClamped()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            25f, 2f, 3f, distanceMeters: 100f, speedCeilingMetersPerSecond: 23f);

        Assert.Equal(0f, profile.AccelerationDistanceMeters);
        Assert.Equal(100f, profile.CruiseDistanceMeters);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
        Assert.Equal(25f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(25f, profile.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void TargetAboveCeilingDoesNotCauseUnnecessaryDeceleration()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            15f, 1f, 2f, distanceMeters: 200f, speedCeilingMetersPerSecond: 22f,
            targetExitSpeedMetersPerSecond: 25f);

        Assert.Equal(22f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(22f, profile.ExitSpeedMetersPerSecond);
        Assert.True(profile.CruiseDistanceMeters > 0f);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
    }

    [Fact]
    public void CeilingCreatesAccelerateCruiseDecelerateProfile()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            10f, 1f, 2f, distanceMeters: 300f, speedCeilingMetersPerSecond: 20f,
            targetExitSpeedMetersPerSecond: 12f);

        Assert.Equal(20f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(12f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(150f, profile.AccelerationDistanceMeters, 5);
        Assert.Equal(86f, profile.CruiseDistanceMeters, 5);
        Assert.Equal(64f, profile.DecelerationDistanceMeters, 5);
        Assert.Equal(
            300f,
            profile.AccelerationDistanceMeters
            + profile.CruiseDistanceMeters
            + profile.DecelerationDistanceMeters,
            5);
    }

    [Fact]
    public void InitialAboveCeilingCanCruiseThenDecelerate()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            25f, 1f, 2f, distanceMeters: 200f, speedCeilingMetersPerSecond: 23f,
            targetExitSpeedMetersPerSecond: 15f);

        Assert.Equal(0f, profile.AccelerationDistanceMeters);
        Assert.Equal(100f, profile.CruiseDistanceMeters, 5);
        Assert.Equal(100f, profile.DecelerationDistanceMeters, 5);
        Assert.Equal(25f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(15f, profile.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void TravelTimeIncludesCruisePhase()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            10f, 1f, 2f, distanceMeters: 300f, speedCeilingMetersPerSecond: 20f,
            targetExitSpeedMetersPerSecond: 12f);
        var expected = 2f * profile.AccelerationDistanceMeters / (10f + 20f)
            + profile.CruiseDistanceMeters / 20f
            + 2f * profile.DecelerationDistanceMeters / (20f + 12f);

        Assert.Equal(expected, profile.TravelTimeSeconds, 5);
    }

    [Fact]
    public void ZeroDistanceProfilePreservesSpeed()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            25f, 1f, 2f, distanceMeters: 0f, speedCeilingMetersPerSecond: 23f,
            targetExitSpeedMetersPerSecond: 12f);

        Assert.Equal(25f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(25f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(0f, profile.AccelerationDistanceMeters);
        Assert.Equal(0f, profile.CruiseDistanceMeters);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
        Assert.Equal(0f, profile.TravelTimeSeconds);
    }

    [Fact]
    public void MaximumApproachSpeedExceedsSettledSpeedWhenScrubDistanceExists()
    {
        var approach = LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
            settledTargetSpeedMetersPerSecond: 16f,
            decelerationMetersPerSecondSquared: 2.5f,
            availableTurnEntryDistanceMeters: 30f);

        Assert.True(approach > 16f);
    }

    [Fact]
    public void MaximumApproachSpeedUsesHalfTurnEntryDistance()
    {
        const float target = 16f;
        const float deceleration = 2.5f;
        const float distance = 30f;
        var expected = MathF.Sqrt(
            target * target
            + 2f * deceleration * distance
            * LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction);

        var approach = LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
            target,
            deceleration,
            distance);

        Assert.Equal(expected, approach, 5);
    }

    [Fact]
    public void ZeroTurnEntryDistanceMakesApproachEqualSettledTarget()
    {
        var approach = LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
            16f,
            2.5f,
            0f);

        Assert.Equal(16f, approach);
    }

    [Fact]
    public void MoreScrubCapabilityRaisesRecoverableApproachSpeed()
    {
        var lower = LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
            16f,
            2f,
            30f);
        var higher = LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
            16f,
            3.2f,
            30f);

        Assert.True(higher > lower);
    }

    [Fact]
    public void LongerTurnEntryRaisesRecoverableApproachSpeed()
    {
        var shorter = LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
            16f,
            2.5f,
            20f);
        var longer = LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
            16f,
            2.5f,
            40f);

        Assert.True(longer > shorter);
    }

    [Fact]
    public void EntryBelowSettledTargetDoesNotDecelerate()
    {
        var profile = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            initialSpeedMetersPerSecond: 14f,
            settledTargetSpeedMetersPerSecond: 16f,
            decelerationMetersPerSecondSquared: 2.5f,
            availableTurnEntryDistanceMeters: 30f);

        Assert.Equal(14f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
        Assert.Equal(15f, profile.CarryDistanceMeters, 5);
        Assert.Equal(15f / 14f, profile.TravelTimeSeconds, 5);
    }

    [Fact]
    public void EntryExactlyAtTargetCarriesThroughScrubPhase()
    {
        var profile = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            initialSpeedMetersPerSecond: 16f,
            settledTargetSpeedMetersPerSecond: 16f,
            decelerationMetersPerSecondSquared: 2.5f,
            availableTurnEntryDistanceMeters: 30f);

        Assert.Equal(16f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
        Assert.Equal(15f, profile.CarryDistanceMeters, 5);
    }

    [Fact]
    public void InsufficientScrubDistanceLeavesResidualOverspeed()
    {
        var profile = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            initialSpeedMetersPerSecond: 22f,
            settledTargetSpeedMetersPerSecond: 16f,
            decelerationMetersPerSecondSquared: 2f,
            availableTurnEntryDistanceMeters: 20f);

        Assert.True(profile.ExitSpeedMetersPerSecond > 16f);
        Assert.Equal(10f, profile.DecelerationDistanceMeters, 5);
        Assert.Equal(0f, profile.CarryDistanceMeters);
    }

    [Fact]
    public void SufficientScrubReachesTargetThenCarries()
    {
        const float initial = 18f;
        const float target = 16f;
        const float deceleration = 2.5f;
        const float availableDistance = 40f;
        var requiredDistance = (initial * initial - target * target) / (2f * deceleration);

        var profile = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            initial,
            target,
            deceleration,
            availableDistance);

        Assert.Equal(target, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(requiredDistance, profile.DecelerationDistanceMeters, 5);
        Assert.Equal(
            availableDistance * LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction
            - requiredDistance,
            profile.CarryDistanceMeters,
            5);
    }

    [Fact]
    public void ScrubPhaseDistancesSumExactlyToHalfAvailableDistance()
    {
        const float availableDistance = 37f;
        var profile = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            18f,
            16f,
            2.5f,
            availableDistance);

        Assert.Equal(
            availableDistance * LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction,
            profile.DecelerationDistanceMeters + profile.CarryDistanceMeters,
            5);
    }

    [Fact]
    public void InsufficientScrubTimeUsesEndpointKinematicTime()
    {
        const float initial = 22f;
        const float availableDistance = 20f;
        var profile = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            initial,
            16f,
            2f,
            availableDistance);
        var scrubDistance = availableDistance
            * LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction;
        var expected = 2f * scrubDistance / (initial + profile.ExitSpeedMetersPerSecond);

        Assert.Equal(expected, profile.TravelTimeSeconds, 5);
    }

    [Fact]
    public void ReachTargetThenCarryTimeUsesBothPhases()
    {
        const float initial = 18f;
        const float target = 16f;
        var profile = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            initial,
            target,
            2.5f,
            40f);
        var expected = 2f * profile.DecelerationDistanceMeters / (initial + target)
            + profile.CarryDistanceMeters / target;

        Assert.Equal(expected, profile.TravelTimeSeconds, 5);
    }

    [Fact]
    public void MaximumApproachSpeedScrubsExactlyToTarget()
    {
        const float target = 16f;
        const float deceleration = 2.5f;
        const float availableDistance = 30f;
        var approach = LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
            target,
            deceleration,
            availableDistance);

        var profile = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            approach,
            target,
            deceleration,
            availableDistance);

        Assert.Equal(target, profile.ExitSpeedMetersPerSecond, 5);
    }

    [Fact]
    public void ZeroAvailableDistancePreservesSpeedAndZeroTime()
    {
        var profile = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            22f,
            16f,
            2.5f,
            0f);

        Assert.Equal(22f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(0f, profile.TravelTimeSeconds);
        Assert.Equal(0f, profile.DecelerationDistanceMeters);
        Assert.Equal(0f, profile.CarryDistanceMeters);
    }

    [Theory]
    [InlineData(-1f, 16f, 2.5f, 30f)]
    [InlineData(float.NaN, 16f, 2.5f, 30f)]
    [InlineData(float.PositiveInfinity, 16f, 2.5f, 30f)]
    [InlineData(float.NegativeInfinity, 16f, 2.5f, 30f)]
    [InlineData(18f, -1f, 2.5f, 30f)]
    [InlineData(18f, float.NaN, 2.5f, 30f)]
    [InlineData(18f, float.PositiveInfinity, 2.5f, 30f)]
    [InlineData(18f, float.NegativeInfinity, 2.5f, 30f)]
    [InlineData(18f, 16f, -1f, 30f)]
    [InlineData(18f, 16f, 0f, 30f)]
    [InlineData(18f, 16f, float.NaN, 30f)]
    [InlineData(18f, 16f, float.PositiveInfinity, 30f)]
    [InlineData(18f, 16f, float.NegativeInfinity, 30f)]
    [InlineData(18f, 16f, 2.5f, -1f)]
    [InlineData(18f, 16f, 2.5f, float.NaN)]
    [InlineData(18f, 16f, 2.5f, float.PositiveInfinity)]
    [InlineData(18f, 16f, 2.5f, float.NegativeInfinity)]
    public void TurnEntryScrubRejectsInvalidInputs(
        float initialSpeed,
        float targetSpeed,
        float deceleration,
        float availableDistance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LongitudinalDynamics.CalculateTurnEntryScrubProfile(
                initialSpeed,
                targetSpeed,
                deceleration,
                availableDistance));
    }

    [Theory]
    [InlineData(-1f, 2.5f, 30f)]
    [InlineData(float.NaN, 2.5f, 30f)]
    [InlineData(float.PositiveInfinity, 2.5f, 30f)]
    [InlineData(float.NegativeInfinity, 2.5f, 30f)]
    [InlineData(16f, -1f, 30f)]
    [InlineData(16f, 0f, 30f)]
    [InlineData(16f, float.NaN, 30f)]
    [InlineData(16f, float.PositiveInfinity, 30f)]
    [InlineData(16f, float.NegativeInfinity, 30f)]
    [InlineData(16f, 2.5f, -1f)]
    [InlineData(16f, 2.5f, float.NaN)]
    [InlineData(16f, 2.5f, float.PositiveInfinity)]
    [InlineData(16f, 2.5f, float.NegativeInfinity)]
    public void MaximumTurnEntryApproachSpeedRejectsInvalidInputs(
        float targetSpeed,
        float deceleration,
        float availableDistance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
                targetSpeed,
                deceleration,
                availableDistance));
    }

    private static float CalculateAcceleration(
        float speedSkill,
        float gearing,
        TrackSurfaceState surface)
        => LongitudinalDynamics.CalculateTurnExitAccelerationMetersPerSecondSquared(
            new RiderSkills(50f, speedSkill, 50f, 50f, 50f, 50f),
            new BikeSetup(gearing, tractionBias: 0.5f),
            surface);

    private static float NetTurnExitAcceleration(
        float speed,
        float speedSkill,
        float gearing,
        TrackSurfaceState surface)
        => LongitudinalDynamics.CalculateTurnExitNetAccelerationMetersPerSecondSquared(
            speed,
            new RiderSkills(50f, speedSkill, 50f, 50f, 50f, 50f),
            new BikeSetup(gearing, tractionBias: 0.5f),
            surface);

    private static float AvailableTurnExitDriveForce(
        float speedSkill,
        float gearing,
        TrackSurfaceState surface)
        => LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
            new RiderSkills(50f, speedSkill, 50f, 50f, 50f, 50f),
            new BikeSetup(gearing, tractionBias: 0.5f),
            surface);

    private static float StraightAcceleration(float speedSkill, TrackSurfaceState surface)
        => LongitudinalDynamics.CalculateStraightAccelerationMetersPerSecondSquared(
            new RiderSkills(50f, speedSkill, 50f, 50f, 50f, 50f),
            surface);

    private static float CornerEntryDeceleration(float slideControl, TrackSurfaceState surface)
        => LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(
            new RiderSkills(50f, 50f, slideControl, 50f, 50f, 50f),
            surface);

    private static float AttainableTopSpeed(float speedSkill, float gearing)
        => LongitudinalDynamics.CalculateAttainableTopSpeedMetersPerSecond(
            new RiderSkills(50f, speedSkill, 50f, 50f, 50f, 50f),
            new BikeSetup(gearing, tractionBias: 0.5f));
}
