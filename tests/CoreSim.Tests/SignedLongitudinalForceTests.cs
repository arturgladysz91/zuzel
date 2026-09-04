using CoreSim;
using CoreSim.Setup;
using Xunit;
using static CoreSim.LongitudinalDynamics;

namespace CoreSim.Tests;

public sealed class SignedLongitudinalForceTests
{
    private static readonly TrackSurfaceState Perfect = new(1f, 0f, 0.35f);
    private static float StraightForce(BikeSetup? setup = null, TrackSurfaceState? surface = null)
        => CalculateStraightAvailableDriveForceNewtons(RiderSkills.Balanced, setup ?? BikeSetup.Neutral, surface ?? Perfect);
    private static float Eq(float? force = null, BikeSetup? setup = null)
        => CalculateFullDriveEquilibriumSpeedMetersPerSecond(force ?? StraightForce(setup), setup ?? BikeSetup.Neutral);
    private static float Net(float speed) => CalculateNetDriveAccelerationMetersPerSecondSquared(speed, StraightForce(), BikeSetup.Neutral);
    private static float Step(float speed, float ds = 1f) => CalculateMidpointDriveEndSpeedMetersPerSecond(speed, ds, StraightForce(), BikeSetup.Neutral);

    [Fact]
    public void AccelerationFromForcesReturnsPositiveWhenDriveExceedsResistance()
        => Assert.Equal(2f, CalculateAccelerationFromForcesMetersPerSecondSquared(300f, 100f, 100f));

    [Fact]
    public void AccelerationFromForcesReturnsZeroAtEqualForces()
        => Assert.Equal(0f, CalculateAccelerationFromForcesMetersPerSecondSquared(300f, 300f, 142f));

    [Fact]
    public void AccelerationFromForcesReturnsNegativeWhenResistanceExceedsDrive()
        => Assert.Equal(-2f, CalculateAccelerationFromForcesMetersPerSecondSquared(100f, 300f, 100f));

    [Fact]
    public void SignedAccelerationRejectsInvalidInputs()
    {
        foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CalculateAccelerationFromForcesMetersPerSecondSquared(invalid, 40f, 142f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CalculateAccelerationFromForcesMetersPerSecondSquared(100f, invalid, 142f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CalculateAccelerationFromForcesMetersPerSecondSquared(100f, 40f, invalid));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => CalculateAccelerationFromForcesMetersPerSecondSquared(100f, 40f, 0f));
        Assert.Throws<OverflowException>(() => CalculateAccelerationFromForcesMetersPerSecondSquared(float.MaxValue, 0f, float.Epsilon));
    }

    [Fact]
    public void NetDriveAccelerationCanBecomeNegativeAboveEquilibrium() => Assert.True(Net(Eq() + 5f) < 0f);

    [Fact]
    public void NetDriveAccelerationIsPositiveBelowEquilibrium() => Assert.True(Net(Eq() - 5f) > 0f);

    [Fact]
    public void NetDriveAccelerationIsApproximatelyZeroAtEquilibrium() => Assert.InRange(MathF.Abs(Net(Eq())), 0f, 0.00001f);

    [Fact]
    public void ZeroDriveSignedAccelerationIsNegative()
    {
        foreach (var speed in new[] { 0f, 1f, 16f, 30f, 100f })
        {
            var acceleration = CalculateZeroDriveSignedAccelerationMetersPerSecondSquared(speed);
            Assert.True(acceleration < 0f);
            Assert.Equal(-(40f + 0.20f * speed * speed) / 142f, acceleration, 5);
        }
    }

    [Fact]
    public void ZeroDriveResistanceDecelerationMagnitudeIncreasesWithSpeed()
    {
        var accelerations = new[] { 0f, 1f, 16f, 30f, 100f }.Select(CalculateZeroDriveSignedAccelerationMetersPerSecondSquared).ToArray();
        for (var i = 1; i < accelerations.Length; i++) Assert.True(accelerations[i] < accelerations[i - 1]);
    }

    [Fact]
    public void ExistingReferenceAccelerationContractAtSixteenMetersPerSecondIsPreserved()
    {
        foreach (var speed in new[] { 0f, 50f, 100f })
        foreach (var gearing in new[] { 0f, 0.5f, 1f })
        foreach (var surface in new[] { Perfect, new TrackSurfaceState(0.5f, 0.1f, 0.35f) })
        {
            var skills = new RiderSkills(50f, speed, 50f, 50f, 50f, 50f);
            var setup = new BikeSetup(gearing, 0.5f);
            var multiplier = (1.10f + (0.90f - 1.10f) * gearing) * (0.75f + 0.25f * surface.EffectiveGrip);
            Assert.InRange(MathF.Abs((0.80f + 0.80f * speed / 100f) * multiplier
                - CalculateStraightNetAccelerationMetersPerSecondSquared(16f, skills, setup, surface)), 0f, 0.000001f);
            Assert.InRange(MathF.Abs((0.60f + 0.80f * speed / 100f) * multiplier
                - CalculateTurnExitNetAccelerationMetersPerSecondSquared(16f, skills, setup, surface)), 0f, 0.000001f);
        }
    }

    [Fact]
    public void FullDriveEquilibriumIsFiniteAndPositive()
    {
        foreach (var gearing in new[] { 0f, 0.5f, 1f })
        foreach (var force in new[] { 41f, 100f, StraightForce(), 1460f })
        {
            var equilibrium = Eq(force, new BikeSetup(gearing, 0.5f));
            Assert.True(float.IsFinite(equilibrium) && equilibrium > 0f);
        }
    }

    [Fact]
    public void FullDriveEquilibriumSolvesDriveEqualsResistance()
    {
        foreach (var gearing in new[] { 0f, 0.5f, 1f })
        foreach (var force in new[] { 41f, 100f, StraightForce(), 1460f })
        {
            var setup = new BikeSetup(gearing, 0.5f);
            var v = Eq(force, setup);
            var drive = force * CalculatePositiveDriveEnvelopeMultiplier(v, setup);
            Assert.InRange(MathF.Abs(drive - CalculateLongitudinalResistanceForceNewtons(v)), 0f, 0.001f);
        }
    }

    [Fact]
    public void EquilibriumSolverIsDeterministic()
    {
        var expected = Eq();
        for (var i = 0; i < 100; i++) Assert.Equal(expected, Eq());
    }

    [Fact]
    public void EquilibriumDoesNotDependOnInitialSpeed()
    {
        foreach (var initial in new[] { 0f, 10f, Eq(), Eq() + 5f, 100f })
            Assert.Equal(Eq(), CalculateForceBasedStraightSpeedProfile(initial, RiderSkills.Balanced, BikeSetup.Neutral,
                Perfect, 2.6f, 60f).FullDriveEquilibriumSpeedMetersPerSecond);
    }

    [Fact]
    public void DriveOrientedGearingHasMoreLowSpeedDrive()
        => Assert.True(StraightForce(new BikeSetup(0f, 0.5f)) > StraightForce(new BikeSetup(1f, 0.5f)));

    [Fact]
    public void SpeedOrientedGearingRetainsDriveLonger()
    {
        var drive = new BikeSetup(0f, 0.5f);
        var speed = new BikeSetup(1f, 0.5f);
        Assert.True(CalculatePositiveDriveEnvelopeMultiplier(40f, speed) > CalculatePositiveDriveEnvelopeMultiplier(40f, drive));
        Assert.True(StraightForce(speed) * CalculatePositiveDriveEnvelopeMultiplier(40f, speed)
            > StraightForce(drive) * CalculatePositiveDriveEnvelopeMultiplier(40f, drive));
    }

    [Fact]
    public void BetterEffectiveGripRaisesCurrentEffectiveModelEquilibrium()
        => Assert.True(Eq(StraightForce(surface: Perfect)) > Eq(StraightForce(surface: new TrackSurfaceState(0.3f, 0f, 0.35f))));

    [Fact]
    public void SignedMidpointAcceleratesBelowEquilibrium() => Assert.True(Step(Eq() - 5f) > Eq() - 5f);

    [Fact]
    public void SignedMidpointDeceleratesAboveEquilibrium() => Assert.True(Step(Eq() + 5f) < Eq() + 5f);

    [Fact]
    public void SignedMidpointPreservesNearEquilibriumSpeed() => Assert.InRange(MathF.Abs(Step(Eq()) - Eq()), 0f, 0.00001f);

    [Fact]
    public void SignedMidpointNeverProducesNaN()
    {
        foreach (var speed in new[] { 0f, 0.01f, 16f, Eq(), 100f, 1000f })
        foreach (var distance in new[] { 0f, 0.01f, 1f, 100f })
            Assert.True(float.IsFinite(Step(speed, distance)));
        Assert.Equal(0f, ApplySignedAccelerationOverDistance(1f, -10f, 1f));
    }

    [Fact]
    public void SignedMidpointIsCloseToFineDistanceReference()
    {
        var exitForce = CalculateTurnExitAvailableDriveForceNewtons(RiderSkills.Balanced, BikeSetup.Neutral, Perfect);
        foreach (var (initial, force) in new[] { (10f, StraightForce()), (Eq() + 5f, StraightForce()), (Eq(exitForce) + 5f, exitForce) })
        {
            var speed = initial;
            var time = 0d;
            for (var i = 0; i < 60; i++)
            {
                var next = CalculateMidpointDriveEndSpeedMetersPerSecond(speed, 1f, force, BikeSetup.Neutral);
                time += 2d / (speed + (double)next);
                speed = next;
            }
            var fine = SignedForceReference.Integrate(initial, 60d, force);
            Assert.InRange(Math.Abs(speed - fine[^1].End), 0d, 0.001d);
            Assert.InRange(Math.Abs(time - fine.Sum(n => n.Time)), 0d, 0.001d);
        }
    }

    [Fact]
    public void SignedMidpointDoesNotClampDirectlyToEquilibrium()
    {
        Assert.True(Step(Eq() + 5f) > Eq());
        Assert.True(Step(Eq() - 5f) < Eq());
    }

    [Fact]
    public void CurrentProductionReferenceForcesCannotStopNormalLowSpeedFullDrive()
    {
        foreach (var skill in new[] { 0f, 50f, 100f })
        foreach (var gearing in new[] { 0f, 0.5f, 1f })
        foreach (var surface in new[] { Perfect, new TrackSurfaceState(0f, 0f, 0.35f) })
        {
            var skills = new RiderSkills(skill, skill, 50f, 50f, 50f, 50f);
            var setup = new BikeSetup(gearing, 0.5f);
            var forces = new[] { CalculateStraightAvailableDriveForceNewtons(skills, setup, surface),
                CalculateTurnExitAvailableDriveForceNewtons(skills, setup, surface),
                CalculateStandingStartAvailableDriveForceNewtons(skills, setup, surface) };
            foreach (var force in forces)
            foreach (var initial in new[] { 0f, 0.01f, 1f, 16f })
            {
                Assert.True(CalculateNetDriveAccelerationMetersPerSecondSquared(initial, force, setup) > 0f);
                Assert.True(CalculateMidpointDriveEndSpeedMetersPerSecond(initial, 1f, force, setup) > initial);
            }
        }
    }

    [Fact]
    public void EquilibriumRejectsInvalidInputsAndUnbracketedForce()
    {
        foreach (var force in new[] { -1f, 0f, 39f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => Eq(force));
        Assert.Equal(0f, Eq(40f));
        Assert.Throws<ArgumentNullException>(() => CalculateFullDriveEquilibriumSpeedMetersPerSecond(100f, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Eq(100f, new BikeSetup(float.NaN, 0.5f)));
    }

    [Fact]
    public void TractionBiasDoesNotAffectForceOrEquilibrium()
    {
        var low = new BikeSetup(0.5f, 0f);
        var high = new BikeSetup(0.5f, 1f);
        Assert.Equal(StraightForce(low), StraightForce(high));
        Assert.Equal(Eq(setup: low), Eq(setup: high));
    }
}
