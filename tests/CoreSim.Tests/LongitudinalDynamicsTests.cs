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

    private static float CalculateAcceleration(
        float speedSkill,
        float gearing,
        TrackSurfaceState surface)
        => LongitudinalDynamics.CalculateTurnExitAccelerationMetersPerSecondSquared(
            new RiderSkills(50f, speedSkill, 50f, 50f, 50f, 50f),
            new BikeSetup(gearing, tractionBias: 0.5f),
            surface);
}
