using CoreSim;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class SteppedTurnExitTraversalTests
{
    private static readonly TrackSurfaceState PerfectSurface = new(1f, 0f, 0.35f);

    [Fact]
    public void SharedNetDriveAccelerationPreservesTurnExitResults()
    {
        var skills = Skills();
        var setup = Setup();
        var referenceForce = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
            skills, setup, PerfectSurface);

        var shared = LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(
            18f, referenceForce, setup);
        var wrapper = LongitudinalDynamics.CalculateTurnExitNetAccelerationMetersPerSecondSquared(
            18f, skills, setup, PerfectSurface);

        Assert.Equal(wrapper, shared);
    }

    [Fact]
    public void SharedNetDriveAccelerationPreservesStraightResults()
    {
        var skills = Skills();
        var setup = Setup();
        var referenceForce = LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(
            skills, setup, PerfectSurface);

        var shared = LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(
            18f, referenceForce, setup);
        var wrapper = LongitudinalDynamics.CalculateStraightNetAccelerationMetersPerSecondSquared(
            18f, skills, setup, PerfectSurface);

        Assert.Equal(wrapper, shared);
    }

    [Fact]
    public void SharedMidpointStepPreservesExistingStraightTraversal()
    {
        var skills = Skills();
        var setup = Setup();
        var referenceForce = LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(
            skills, setup, PerfectSurface);
        var expected = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
            12f, 1f, referenceForce, setup);
        var profile = LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            12f, skills, setup, PerfectSurface, 2.5f, 1f);

        Assert.Equal(expected, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(2f / (12f + expected), profile.TravelTimeSeconds, 6);
    }

    [Fact]
    public void LongitudinalIntegrationStepRemainsOneMeter()
        => Assert.Equal(1f, LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters);

    [Fact]
    public void StraightCompatibilityIntegrationConstantAliasesSharedConstant()
        => Assert.Equal(
            LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters,
            LongitudinalDynamics.ProvisionalStraightIntegrationStepMeters);

    [Fact]
    public void TurnExitProfileZeroDistancePreservesSpeedAndZeroTime()
    {
        var profile = Profile(14f, 0f);

        Assert.Equal(14f, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(14f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(0f, profile.TravelTimeSeconds);
        Assert.Equal(0f, profile.AccelerationDistanceMeters);
        Assert.Equal(0f, profile.CruiseDistanceMeters);
    }

    [Fact]
    public void TurnExitProfileConsumesExactRequestedDistance()
    {
        var profile = Profile(12f, 25.4f);
        Assert.Equal(25.4f, profile.AccelerationDistanceMeters + profile.CruiseDistanceMeters + profile.DecelerationDistanceMeters);
    }

    [Fact]
    public void TurnExitProfilePhaseDistancesSumToTotalDistance()
    {
        var profile = Profile(18f, 37.75f);
        Assert.Equal(37.75f, profile.AccelerationDistanceMeters + profile.CruiseDistanceMeters + profile.DecelerationDistanceMeters);
    }

    [Fact]
    public void TurnExitProfileUsesOneMeterStepsAndFinalRemainder()
    {
        var skills = Skills();
        var setup = Setup();
        var referenceForce = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
            skills, setup, PerfectSurface);
        var expected = Integrate(12f, 25.4f, 1f, referenceForce, setup);
        var profile = Profile(12f, 25.4f);

        Assert.Equal(expected.ExitSpeed, profile.ExitSpeedMetersPerSecond);
        Assert.Equal(expected.Time, profile.TravelTimeSeconds, 6);
    }

    [Fact]
    public void TurnExitProfileAcceleratesWithPositiveDrive()
        => Assert.True(Profile(12f, 25.4f).ExitSpeedMetersPerSecond > 12f);

    [Fact]
    public void TurnExitProfileCanStartFromZeroWhenPositiveDriveExists()
    {
        var profile = Profile(0f, 1f);
        Assert.True(profile.ExitSpeedMetersPerSecond > 0f);
        Assert.True(float.IsFinite(profile.TravelTimeSeconds));
        Assert.True(profile.TravelTimeSeconds > 0f);
    }

    [Fact]
    public void TurnExitProfilePeakEqualsExitWhenMonotonicallyAccelerating()
    {
        var profile = Profile(12f, 25.4f);
        Assert.Equal(profile.ExitSpeedMetersPerSecond, profile.PeakSpeedMetersPerSecond);
    }

    [Fact]
    public void TurnExitProfileUsesSpeedDependentAccelerationAcrossDistance()
    {
        var skills = Skills();
        var setup = Setup();
        var acceleration = LongitudinalDynamics.CalculateTurnExitNetAccelerationMetersPerSecondSquared(
            12f, skills, setup, PerfectSurface);
        var oneShot = LongitudinalDynamics.ApplySignedAccelerationOverDistance(
            12f, acceleration, 25.4f);
        var stepped = Profile(12f, 25.4f).ExitSpeedMetersPerSecond;

        Assert.NotEqual(oneShot, stepped);
        Assert.True(stepped < oneShot);
    }

    [Fact]
    public void TurnExitSteppedTraversalDoesNotOverstateOneShotDrive()
    {
        var skills = Skills();
        var setup = Setup();
        var acceleration = LongitudinalDynamics.CalculateTurnExitNetAccelerationMetersPerSecondSquared(
            16f, skills, setup, PerfectSurface);
        var oneShot = LongitudinalDynamics.ApplySignedAccelerationOverDistance(
            16f, acceleration, 25.4f);

        Assert.True(Profile(16f, 25.4f).ExitSpeedMetersPerSecond <= oneShot);
    }

    [Fact]
    public void TurnExitProfilePreservesReferencePointAccelerationContract()
    {
        var skills = Skills();
        var setup = Setup();
        var profile = Profile(16f, 25.4f, skills, setup);
        var expected = LongitudinalDynamics.CalculateTurnExitAccelerationMetersPerSecondSquared(
            skills, setup, PerfectSurface);

        Assert.Equal(expected, profile.EntryNetAccelerationMetersPerSecondSquared, 6);
    }

    [Fact]
    public void TurnExitProfileNaturallyDecreasesExistingOverspeedAboveEquilibrium()
    {
        var profile = Profile(35f, 25.4f);
        Assert.True(profile.ExitSpeedMetersPerSecond < 35f);
        Assert.True(profile.ExitSpeedMetersPerSecond > profile.FullDriveEquilibriumSpeedMetersPerSecond);
        Assert.Equal(35f, profile.PeakSpeedMetersPerSecond);
        Assert.Equal(0f, profile.AccelerationDistanceMeters);
        Assert.Equal(25.4f, profile.DecelerationDistanceMeters);
        Assert.True(profile.TravelTimeSeconds > 25.4f / 35f);
    }

    [Fact]
    public void TurnExitProfileDeceleratesWhenResistanceExceedsDrive()
    {
        var profile = Profile(40f, 25.4f);
        Assert.True(profile.EntryNetAccelerationMetersPerSecondSquared < 0f);
        Assert.True(profile.ExitSpeedMetersPerSecond < 40f);
        Assert.True(profile.ExitSpeedMetersPerSecond > profile.FullDriveEquilibriumSpeedMetersPerSecond);
        Assert.Equal(0f, profile.AccelerationDistanceMeters);
        Assert.Equal(0f, profile.CruiseDistanceMeters);
        Assert.Equal(25.4f, profile.DecelerationDistanceMeters);
    }

    [Fact]
    public void TurnExitProfilePhaseDistancesRemainNonNegative()
    {
        var profile = Profile(20f, 25.4f);
        Assert.True(profile.ExitSpeedMetersPerSecond >= 20f);
        Assert.True(profile.AccelerationDistanceMeters >= 0f);
        Assert.True(profile.CruiseDistanceMeters >= 0f);
    }

    [Fact]
    public void TurnExitProfileTravelTimeUsesAllDistanceSteps()
    {
        var skills = Skills();
        var setup = Setup();
        var referenceForce = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
            skills, setup, PerfectSurface);
        var expected = Integrate(12f, 25.4f, 1f, referenceForce, setup);

        Assert.Equal(expected.Time, Profile(12f, 25.4f).TravelTimeSeconds, 6);
    }

    [Fact]
    public void TurnExitProfileTravelTimeIsFiniteAndPositiveForPositiveDistance()
    {
        var time = Profile(12f, 25.4f).TravelTimeSeconds;
        Assert.True(float.IsFinite(time));
        Assert.True(time > 0f);
    }

    [Fact]
    public void TurnExitMidpointIntegrationIsDeterministic()
        => Assert.Equal(Profile(12f, 25.4f), Profile(12f, 25.4f));

    [Fact]
    public void TurnExitOneMeterMidpointIsCloseToFineDistanceReference()
    {
        var skills = Skills();
        var setup = Setup();
        var referenceForce = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
            skills, setup, PerfectSurface);
        var fine = Integrate(16f, 25.4f, 0.05f, referenceForce, setup);
        var profile = Profile(16f, 25.4f);

        Assert.InRange(MathF.Abs(profile.ExitSpeedMetersPerSecond - fine.ExitSpeed), 0f, 0.001f);
        Assert.InRange(MathF.Abs(profile.TravelTimeSeconds - fine.Time), 0f, 0.0001f);
    }

    [Fact]
    public void DriveOrientedTurnExitStillWinsAtLowerSpeed()
    {
        var low = Profile(12f, 25.4f, Skills(), Setup(0f));
        var high = Profile(12f, 25.4f, Skills(), Setup(1f));
        Assert.True(low.ExitSpeedMetersPerSecond > high.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void SpeedOrientedTurnExitStillRetainsDriveLongerAtHighSpeed()
    {
        var skills = Skills();
        var low = Setup(0f);
        var high = Setup(1f);
        var lowAcceleration = LongitudinalDynamics.CalculateTurnExitNetAccelerationMetersPerSecondSquared(
            25f, skills, low, PerfectSurface);
        var highAcceleration = LongitudinalDynamics.CalculateTurnExitNetAccelerationMetersPerSecondSquared(
            25f, skills, high, PerfectSurface);
        Assert.True(highAcceleration > lowAcceleration);
    }

    [Fact]
    public void BetterGripStillRaisesTurnExitSteppedExitSpeed()
    {
        var poor = new TrackSurfaceState(0f, 0f, 0.35f);
        var skills = Skills();
        var setup = Setup();
        var poorProfile = LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
            12f, skills, setup, poor, 25.4f);
        var goodProfile = Profile(12f, 25.4f, skills, setup);
        Assert.True(goodProfile.ExitSpeedMetersPerSecond > poorProfile.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void HigherSpeedSkillStillRaisesTurnExitSteppedExitSpeed()
    {
        var lowSkills = Skills(0f);
        var highSkills = Skills(100f);
        var setup = Setup();
        var low = Profile(12f, 25.4f, lowSkills, setup);
        var high = Profile(12f, 25.4f, highSkills, setup);
        Assert.True(high.ExitSpeedMetersPerSecond > low.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void MoraleStillDoesNotAffectTurnExitDrive()
    {
        // Morale is deliberately absent from the pure traversal inputs.
        Assert.Equal(Profile(12f, 25.4f), Profile(12f, 25.4f));
    }

    private static TurnExitDriveProfile Profile(
        float initialSpeed,
        float distance,
        RiderSkills? skills = null,
        BikeSetup? setup = null)
    {
        skills ??= Skills();
        setup ??= Setup();
        return LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
            initialSpeed,
            skills,
            setup,
            PerfectSurface,
            distance);
    }

    private static (float ExitSpeed, float Time) Integrate(
        float initialSpeed,
        float distance,
        float maximumStep,
        float referenceForce,
        BikeSetup setup)
    {
        var speed = initialSpeed;
        var time = 0d;
        var remaining = (double)distance;
        while (remaining > 0d)
        {
            var step = (float)Math.Min(maximumStep, remaining);
            var end = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
                speed, step, referenceForce, setup);
            time += 2d * step / (speed + end);
            speed = end;
            remaining -= step;
        }

        return (speed, (float)time);
    }

    private static RiderSkills Skills(float speed = 50f)
        => new(50f, speed, 50f, 50f, 50f, 50f);

    private static BikeSetup Setup(float gearing = 0.5f)
        => new(gearing, 0.5f);

}
