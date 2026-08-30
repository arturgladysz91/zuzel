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
    public void FullDistanceAccelerationWhenTargetCannotBeReachedOrExceeded()
    {
        var profile = LongitudinalDynamics.CalculateStraightSpeedProfile(
            initialSpeedMetersPerSecond: 10f,
            accelerationMetersPerSecondSquared: 1f,
            cornerEntryDecelerationMetersPerSecondSquared: 2f,
            distanceMeters: 20f,
            targetExitSpeedMetersPerSecond: 15f);
        var expectedExit = MathF.Sqrt(140f);

        Assert.Equal(20f, profile.AccelerationDistanceMeters);
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
            targetExitSpeedMetersPerSecond: 15f);

        Assert.Equal(0f, profile.AccelerationDistanceMeters);
        Assert.Equal(10f, profile.DecelerationDistanceMeters);
        Assert.Equal(20f, profile.PeakSpeedMetersPerSecond);
        Assert.True(profile.ExitSpeedMetersPerSecond > 15f);
        Assert.Equal(MathF.Sqrt(360f), profile.ExitSpeedMetersPerSecond, 5);
    }

    [Fact]
    public void AccelerateThenDecelerateUsesAnalyticPeak()
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
            targetSpeed);

        Assert.Equal(expectedPeak, profile.PeakSpeedMetersPerSecond, 5);
        Assert.Equal(expectedAccelerationDistance, profile.AccelerationDistanceMeters, 4);
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
            targetSpeed);
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

    private static float CalculateAcceleration(
        float speedSkill,
        float gearing,
        TrackSurfaceState surface)
        => LongitudinalDynamics.CalculateTurnExitAccelerationMetersPerSecondSquared(
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
}
