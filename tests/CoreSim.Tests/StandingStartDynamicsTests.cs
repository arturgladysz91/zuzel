using CoreSim;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class StandingStartDynamicsTests
{
    private static readonly TrackSurfaceState Perfect = new(1f, 0f, 0.35f);
    private static RiderSkills Skills(float start = 50f) => new(start, 50f, 50f, 50f, 50f, 50f);
    private static float Reaction(float start) => LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(Skills(start));
    private static float Acceleration(float start = 50f, float gearing = 0.5f, TrackSurfaceState? surface = null)
        => LongitudinalDynamics.CalculateStandingStartReferenceDriveAccelerationMetersPerSecondSquared(
            Skills(start), new BikeSetup(gearing, 0.5f), surface ?? Perfect);
    private static StandingStartLaunchProfile Profile(float distance = 30f, float start = 50f,
        float gearing = 0.5f, TrackSurfaceState? surface = null, float? target = null)
        => LongitudinalDynamics.CalculateStandingStartLaunchProfile(Skills(start), new BikeSetup(gearing, 0.5f),
            surface ?? Perfect, distance, target);

    [Fact] public void StandingStartReactionTimeIsPointTwoEightAtStartSkillZero() => Assert.Equal(0.28f, Reaction(0));
    [Fact] public void StandingStartReactionTimeIsPointTwoFourAtStartSkillFifty() => Assert.Equal(0.24f, Reaction(50), 6);
    [Fact] public void StandingStartReactionTimeIsPointTwoAtStartSkillHundred() => Assert.Equal(0.20f, Reaction(100));
    [Fact] public void HigherStartSkillReducesReactionTime() => Assert.True(Reaction(0) > Reaction(50) && Reaction(50) > Reaction(100));

    [Fact]
    public void MoraleDoesNotAffectStandingStartReaction()
    {
        var rider = new RiderState(1, 0) { Morale = 0f };
        var first = LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(rider.Profile.Skills);
        rider.Morale = 1f;
        Assert.Equal(first, LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(rider.Profile.Skills));
    }

    [Fact]
    public void StandingStartReferenceAccelerationUsesStartSkill()
    {
        Assert.Equal(9f, Acceleration(0));
        Assert.Equal(10f, Acceleration(50));
        Assert.Equal(11f, Acceleration(100));
    }

    [Fact]
    public void StandingStartReferenceAccelerationUsesExistingGearingDriveDirection()
    {
        Assert.Equal(10f * 1.10f, Acceleration(gearing: 0));
        Assert.Equal(10f * 0.90f, Acceleration(gearing: 1));
    }

    [Fact]
    public void StandingStartReferenceAccelerationUsesEntrySurface()
    {
        var surface = new TrackSurfaceState(0.4f, 0.2f, 0.6f);
        Assert.Equal(10f * (0.75f + 0.25f * surface.EffectiveGrip), Acceleration(surface: surface));
    }

    [Fact]
    public void StandingStartReferenceForcePreservesLaunchAccelerationAtZeroSpeed()
    {
        var force = LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(Skills(), BikeSetup.Neutral, Perfect);
        Assert.Equal(1460f, force);
        Assert.Equal(Acceleration(), LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(0f, force, BikeSetup.Neutral));
    }

    [Fact]
    public void StandingStartUsesSharedOneGearEnvelope()
    {
        foreach (var gearing in new[] { 0f, 0.5f, 1f })
        {
            var setup = new BikeSetup(gearing, 0.5f);
            var force = LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(Skills(), setup, Perfect);
            var fade = 0.0175f + (0.0050f - 0.0175f) * gearing;
            var expectedForce = force * (1f - fade * (22f - 16f));
            Assert.Equal(expectedForce, LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(force, 22f, setup), 4);
            Assert.Equal(Profile(100f, gearing: gearing).ExitSpeedMetersPerSecond,
                SharedSteps(100f, gearing: gearing)[^1].EndSpeed);
        }
    }

    [Fact]
    public void StandingStartUsesSharedResistanceModel()
    {
        var force = LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(Skills(), BikeSetup.Neutral, Perfect);
        Assert.Equal((force - (40f + 0.20f * 12f * 12f)) / 142f,
            LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(12f, force, BikeSetup.Neutral), 5);
        Assert.True(Profile(10f).ExitSpeedMetersPerSecond < MathF.Sqrt(2f * 10f * 10f));
    }

    [Fact]
    public void StandingStartTractionBiasDoesNotYetAffectLaunchForce()
    {
        var low = new BikeSetup(0.5f, 0f);
        var high = new BikeSetup(0.5f, 1f);
        Assert.Equal(LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(Skills(), low, Perfect),
            LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(Skills(), high, Perfect));
        Assert.Equal(LongitudinalDynamics.CalculateStandingStartLaunchProfile(Skills(), low, Perfect, 30f),
            LongitudinalDynamics.CalculateStandingStartLaunchProfile(Skills(), high, Perfect, 30f));
    }

    [Fact]
    public void StandingStartMoraleDoesNotAffectLaunchForce()
    {
        var rider = new RiderState(1, 0) { Morale = 0f };
        var force = LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(rider.Profile.Skills, rider.ActiveSetup, Perfect);
        rider.Morale = 1f;
        Assert.Equal(force, LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(rider.Profile.Skills, rider.ActiveSetup, Perfect));
    }

    [Fact]
    public void StandingStartProfileStartsAtZeroSpeed()
    {
        var profile = Profile(0.25f);
        Assert.Equal(2f * 0.25f / profile.ExitSpeedMetersPerSecond, profile.MovementTimeSeconds);
        Assert.Equal(10f, profile.EntryNetAccelerationMetersPerSecondSquared);
    }

    [Fact]
    public void StandingStartProfileConsumesExactRequestedDistance()
    {
        foreach (var distance in new[] { 0.001f, 0.75f, 1f, 2.125f, 30f, 100.25f })
        {
            var p = Profile(distance);
            Assert.Equal(distance, p.AccelerationDistanceMeters + p.CruiseDistanceMeters + p.PreparationDistanceMeters);
        }
    }

    [Fact]
    public void StandingStartProfileUsesSharedOneMeterStepsAndFinalRemainder()
    {
        var steps = SharedSteps(30.25f);
        Assert.Equal(31, steps.Count);
        Assert.Equal(0.25f, steps[^1].Distance);
        Assert.Equal(1f, LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters);
        Assert.Equal(steps[^1].EndSpeed, Profile(30.25f).ExitSpeedMetersPerSecond);
        Assert.Equal((float)steps.Sum(s => s.Time), Profile(30.25f).MovementTimeSeconds);
    }

    [Fact]
    public void StandingStartProfilePhaseDistancesSumToDistance()
    {
        var p = Profile(100.25f, target: 5f);
        Assert.True(p.AccelerationDistanceMeters > 0 && p.PreparationDistanceMeters > 0);
        Assert.Equal(100.25f, p.AccelerationDistanceMeters + p.CruiseDistanceMeters + p.PreparationDistanceMeters);
    }

    [Fact]
    public void StandingStartProfileMovementTimeIsFiniteAndPositive()
    {
        var p = Profile();
        Assert.True(float.IsFinite(p.MovementTimeSeconds) && p.MovementTimeSeconds > 0f);
    }

    [Fact] public void StandingStartProfileTotalTimeEqualsReactionPlusMovement()
    {
        var p = Profile();
        Assert.Equal(p.ReactionTimeSeconds + p.MovementTimeSeconds, p.TotalTimeSeconds);
    }

    [Fact] public void StandingStartProfilePeakEqualsExitForMonotonicLaunch()
    {
        var p = Profile(120f);
        Assert.Equal(p.ExitSpeedMetersPerSecond, p.PeakSpeedMetersPerSecond);
        Assert.All(SharedSteps(120f), s => Assert.True(s.EndSpeed >= s.StartSpeed));
    }

    [Fact]
    public void StandingStartNoLongerUsesArtificialCeiling()
    {
        foreach (var gearing in new[] { 0f, 0.5f, 1f })
        {
            var profile = Profile(1000f, gearing: gearing);
            var formerCeiling = 23f * (0.94f + 0.12f * gearing);
            Assert.True(profile.ExitSpeedMetersPerSecond > formerCeiling);
            Assert.True(profile.ExitSpeedMetersPerSecond <= profile.FullDriveEquilibriumSpeedMetersPerSecond + 0.0001f);
            Assert.True(Math.Abs(profile.ExitSpeedMetersPerSecond - profile.FullDriveEquilibriumSpeedMetersPerSecond)
                < Math.Abs(Profile(30f, gearing: gearing).ExitSpeedMetersPerSecond - profile.FullDriveEquilibriumSpeedMetersPerSecond));
        }
    }

    [Fact] public void DriveOrientedGearingImprovesEarlyLaunch() => Assert.True(Profile(10f, gearing: 0).ExitSpeedMetersPerSecond > Profile(10f, gearing: 1).ExitSpeedMetersPerSecond);
    [Fact] public void HigherStartSkillImprovesLaunchExitSpeed() => Assert.True(Profile(10f, start: 100).ExitSpeedMetersPerSecond > Profile(10f, start: 50).ExitSpeedMetersPerSecond && Profile(10f, start: 50).ExitSpeedMetersPerSecond > Profile(10f, start: 0).ExitSpeedMetersPerSecond);
    [Fact] public void BetterStartSurfaceImprovesLaunchExitSpeed() => Assert.True(Profile().ExitSpeedMetersPerSecond > Profile(surface: new TrackSurfaceState(0.2f, 0.5f, 0.7f)).ExitSpeedMetersPerSecond);
    [Fact] public void StandingStartProfileIsDeterministic() => Assert.Equal(Profile(), Profile());

    [Fact]
    public void StandingStartOneMeterMidpointIsCloseToFineDistanceReference()
    {
        foreach (var gearing in new[] { 0f, 0.5f, 1f })
        foreach (var distance in new[] { 30f, 100.25f })
        {
            var fine = FineReference(distance, gearing);
            var p = Profile(distance, gearing: gearing);
            Assert.InRange(Math.Abs(p.ExitSpeedMetersPerSecond - fine.Speed), 0d, 0.01d);
            Assert.InRange(Math.Abs(p.MovementTimeSeconds - fine.Time), 0d, 0.01d);
        }
    }

    [Fact] public void TimeTo70IsNullWhenThresholdIsNotReached() => Assert.Null(Profile(5f).TimeTo70KphSeconds);

    [Fact]
    public void TimeTo70IncludesReactionTime()
    {
        var steps = SharedSteps(100f);
        var node = steps.First(s => s.EndSpeed >= LongitudinalDynamics.StandingStartTelemetry70KphMetersPerSecond);
        var movementCrossing = node.StartTime + (LongitudinalDynamics.StandingStartTelemetry70KphMetersPerSecond - node.StartSpeed) / node.Acceleration;
        Assert.Equal((float)(Reaction(50) + movementCrossing), Profile(100f).TimeTo70KphSeconds!.Value, 5);
    }

    [Fact]
    public void TimeTo70CrossingIsInterpolatedWithinDistanceStep()
    {
        var node = SharedSteps(100f).First(s => s.EndSpeed >= LongitudinalDynamics.StandingStartTelemetry70KphMetersPerSecond);
        var crossing = Profile(100f).TimeTo70KphSeconds!.Value;
        Assert.True(crossing > Reaction(50) + node.StartTime);
        Assert.True(crossing < Reaction(50) + node.StartTime + node.Time);
        Assert.Equal(LongitudinalDynamics.StandingStartTelemetry70KphMetersPerSecond,
            (float)(node.StartSpeed + node.Acceleration * (crossing - Reaction(50) - node.StartTime)), 4);
    }

    [Fact]
    public void SpeedAtTwoSecondsIncludesReactionDelay()
    {
        var p = Profile();
        var noReactionNode = SharedSteps(30f).Single(s => s.StartTime <= 2d && s.StartTime + s.Time > 2d);
        var ignoringReaction = noReactionNode.StartSpeed + noReactionNode.Acceleration * (2d - noReactionNode.StartTime);
        Assert.True(p.SpeedAtTwoSecondsMetersPerSecond!.Value < ignoringReaction);
    }

    [Fact]
    public void SpeedAtTwoSecondsIsInterpolatedInsideCorrectedStep()
    {
        foreach (var target in new float?[] { null, 12f })
        {
            var node = SharedSteps(20f, target: target).Single(s => s.StartTime + Reaction(50) <= 2d
                && s.StartTime + Reaction(50) + s.Time > 2d);
            if (target.HasValue)
                Assert.True(node.EndSpeed < node.StartSpeed);
            var expected = node.StartSpeed + node.Acceleration * (2d - Reaction(50) - node.StartTime);
            Assert.InRange(Math.Abs(expected - Profile(20f, target: target).SpeedAtTwoSecondsMetersPerSecond!.Value), 0d, 1e-5d);
        }
    }

    [Fact] public void SpeedAtTwoSecondsIsNullWhenLaunchSegmentEndsBeforeTwoSeconds() => Assert.Null(Profile(1f).SpeedAtTwoSecondsMetersPerSecond);

    [Fact]
    public void ZeroDistanceLaunchContainsReactionButNoMovement()
    {
        var p = Profile(0f);
        Assert.Equal(0.24f, p.ReactionTimeSeconds, 6);
        Assert.Equal(p.ReactionTimeSeconds, p.TotalTimeSeconds);
        Assert.Equal(0f, p.MovementTimeSeconds);
        Assert.Equal(0f, p.ExitSpeedMetersPerSecond);
        Assert.Equal(0f, p.PeakSpeedMetersPerSecond);
        Assert.Equal(0f, p.AccelerationDistanceMeters + p.CruiseDistanceMeters + p.PreparationDistanceMeters);
        Assert.Null(p.TimeTo70KphSeconds);
        Assert.Null(p.SpeedAtTwoSecondsMetersPerSecond);
    }

    [Fact]
    public void StandingStartProfileRejectsInvalidDistanceAndTarget()
    {
        foreach (var value in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => Profile(value));
        foreach (var value in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => Profile(target: value));
        Assert.Throws<ArgumentNullException>(() => LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(Skills(float.NaN)));
        Assert.Throws<ArgumentNullException>(() => LongitudinalDynamics.CalculateStandingStartLaunchProfile(Skills(), null!, Perfect, 30f));
    }

    // Verifies production wiring and within-step metrics using the public shared step.
    // The fine-resolution reference below is deliberately independent of this helper.
    private static List<Step> SharedSteps(float distance, float gearing = 0.5f, float? target = null)
    {
        var setup = new BikeSetup(gearing, 0.5f);
        var force = LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(Skills(), setup, Perfect);
        var steps = new List<Step>();
        var speed = 0f;
        var time = 0d;
        for (var position = 0d; position < distance;)
        {
            var ds = (float)Math.Min(1d, distance - position);
            var end = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(speed, ds, force, setup);
            if (target is { } approach)
            {
                var allowed = (float)Math.Sqrt((double)approach * approach + 2d * 2.6f * (distance - position - ds));
                if (end > allowed)
                    end = Math.Min(end, Math.Max(allowed, LongitudinalDynamics.DecelerateOverDistance(speed, 2.6f, ds)));
            }
            var dt = 2d * ds / (speed + (double)end);
            steps.Add(new Step(ds, speed, end, time, dt));
            speed = end;
            position += ds;
            time += dt;
        }
        return steps;
    }

    private sealed record Step(float Distance, float StartSpeed, float EndSpeed, double StartTime, double Time)
    {
        public double Acceleration => ((double)EndSpeed * EndSpeed - (double)StartSpeed * StartSpeed) / (2d * Distance);
    }

    // Independent 0.05 m reference: no production integrator or force helper.
    private static (double Speed, double Time) FineReference(double distance, double gearing)
    {
        var referenceForce = 142d * 10d * (1.10d - 0.20d * gearing) + 40d;
        var fade = 0.0175d + (0.0050d - 0.0175d) * gearing;
        double AccelerationAt(double v) =>
            (referenceForce * Math.Max(0d, 1d - fade * Math.Max(0d, v - 16d)) - 40d - 0.20d * v * v) / 142d;
        var speed = 0d;
        var time = 0d;
        for (var position = 0d; position < distance;)
        {
            var ds = Math.Min(0.05d, distance - position);
            var predicted = Math.Sqrt(Math.Max(0d, speed * speed + 2d * AccelerationAt(speed) * ds));
            var midpoint = (speed + predicted) / 2d;
            var end = Math.Sqrt(Math.Max(0d, speed * speed + 2d * AccelerationAt(midpoint) * ds));
            time += 2d * ds / (speed + end);
            speed = end;
            position += ds;
        }
        return (speed, time);
    }
}
