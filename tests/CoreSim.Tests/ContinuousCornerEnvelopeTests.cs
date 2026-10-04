using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class ContinuousCornerEnvelopeTests
{
    private static ContinuousCornerEnvelope Model(float apex = 16f, float length = 100f,
        float gearing = .5f) => new(apex, length, 2.6f,
            LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(RiderSkills.Balanced,
                new BikeSetup(gearing, .5f), CornerTestSupport.Surface), new BikeSetup(gearing, .5f));

    [Fact] public void EntryRecoverabilityUsesWholeDistanceToApex()
    {
        var e = Model();
        Assert.Equal(MathF.Sqrt(16f * 16f + 2f * 2.6f * 50f), e.SpeedMetersPerSecond(0f), 5);
        Assert.True(e.SpeedMetersPerSecond(0f) > e.ApexSpeedMetersPerSecond);
    }

    [Fact] public void PreApexEnvelopeDecreasesMonotonically()
    {
        var e = Model();
        for (var i = 1; i <= 500; i++) Assert.True(e.SpeedMetersPerSecond(i / 1000f) < e.SpeedMetersPerSecond((i - 1) / 1000f));
    }

    [Fact] public void ApexEqualsSettledCapabilityExactly() => Assert.Equal(16f, Model().SpeedMetersPerSecond(.5f));

    [Fact] public void PostApexReusesSignedMidpointDriveAtOneMeterResolution()
    {
        var e = Model();
        var speed = e.ApexSpeedMetersPerSecond;
        for (var metre = 0; metre < 50; metre++)
            speed = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(speed, 1f,
                e.FullDriveReferenceForceNewtons, e.Setup, ContinuousCornerEnvelope.DriveAvailability(.5f + (metre + .5f) / 100f));
        Assert.Equal(speed, e.SpeedMetersPerSecond(1f));
        Assert.True(speed > e.ApexSpeedMetersPerSecond);
    }

    [Theory] [InlineData(0f)] [InlineData(.125f)] [InlineData(.4999f)] [InlineData(.5f)]
    public void PreApexAvailabilityIsNeutral(float p) => Assert.Equal(0f, ContinuousCornerEnvelope.DriveAvailability(p));

    [Fact] public void SmoothAvailabilityIsMonotonic()
    {
        for (var i = 501; i <= 1000; i++) Assert.True(ContinuousCornerEnvelope.DriveAvailability(i / 1000f)
            >= ContinuousCornerEnvelope.DriveAvailability((i - 1) / 1000f));
    }

    [Theory] [InlineData(.84f)] [InlineData(.875f)] [InlineData(1f)]
    public void FullDriveEndpointIsExistingSignedModel(float p)
    {
        var e = Model();
        Assert.Equal(1f, ContinuousCornerEnvelope.DriveAvailability(p));
        Assert.Equal(LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(23f,
            e.FullDriveReferenceForceNewtons, e.Setup), e.NetDriveAccelerationMetersPerSecondSquared(23f, p));
    }

    [Fact] public void AvailabilityExposureIsOneThirdWithoutIncreasingTotalDriveBudget()
    {
        double integral = 0;
        for (var i = 0; i < 10000; i++) integral += ContinuousCornerEnvelope.DriveAvailability((i + .5f) / 10000f) / 10000d;
        Assert.InRange(Math.Abs(integral - 1d / 3d), 0d, 1e-6d);
    }

    [Theory] [InlineData(1f / 3f)] [InlineData(2f / 3f)]
    public void OldBoundariesHaveContinuousEnvelopeAvailabilityAccelerationAndTarget(float p)
    {
        var track = CornerTestSupport.Track();
        var rider = new RiderState(1, 1);
        ContinuousCornerEnvelope At(float progress)
        {
            var index = (int)(progress * 3f);
            return CornerTestSupport.Envelope(track, rider, index: index, progress: progress * 3f - index);
        }
        var left = At(p - 1e-6f);
        var right = At(p + 1e-6f);
        Assert.Equal(left.TotalLengthMeters, right.TotalLengthMeters);
        Near(left.SpeedMetersPerSecond(p - 1e-6f), right.SpeedMetersPerSecond(p + 1e-6f), .0001f);
        Near(ContinuousCornerEnvelope.DriveAvailability(p - 1e-6f), ContinuousCornerEnvelope.DriveAvailability(p + 1e-6f), .0001f);
        Near(left.NetDriveAccelerationMetersPerSecondSquared(17f, p - 1e-6f), right.NetDriveAccelerationMetersPerSecondSquared(17f, p + 1e-6f), .0001f);
    }

    [Fact] public void LabelsDoNotSelectLongitudinalLaws()
    {
        var rider = new RiderState(1, 1);
        var types = new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit };
        var profiles = types.Select(type =>
        {
            var track = new Track(new[] { new TrackSegment(0, type) });
            return CornerTestSupport.Envelope(track, rider).Traverse(15f, 0f,
                LaneModel.SegmentLengthMeters(track.Segments[0], 1f, track.Geometry));
        }).ToArray();
        Assert.Equal(profiles[0], profiles[1]);
        Assert.Equal(profiles[1], profiles[2]);
    }

    [Fact] public void TurnMiddleAlreadyReceivesDriveAfterApex()
    {
        var result = CornerTestSupport.Probe(.625f, .95f);
        Assert.Equal(SegmentType.TurnMiddle, result.Scenario.SegmentType);
        Assert.True(result.Diagnostics.ContinuousCornerProfile!.DriveDistanceMeters > 0f);
        Assert.True(result.Change.Speed > result.Change.EntrySpeed);
    }

    [Theory] [InlineData(0f, 100f)] [InlineData(.125f, 13.375f)] [InlineData(.49f, 2.3f)] [InlineData(.875f, .25f)]
    public void PhysicalDistanceAndTimeBucketsAreDisjoint(float start, float distance)
    {
        var e = Model();
        var p = e.Traverse(e.SpeedMetersPerSecond(start), start, distance);
        Near(distance, p.CorrectionDistanceMeters + p.CarryDistanceMeters + p.DriveDistanceMeters, .0001f);
        Near(p.TravelTimeSeconds, p.CorrectionTimeSeconds + p.CarryTimeSeconds + p.DriveTimeSeconds, .00001f);
        Assert.Equal(start, p.Nodes[0].CornerProgress);
        Near(start + distance / e.TotalLengthMeters, p.Nodes[^1].CornerProgress, 1e-6f);
        Assert.All(p.Nodes.Zip(p.Nodes.Skip(1)), pair =>
            Assert.InRange((pair.Second.CornerProgress - pair.First.CornerProgress) * e.TotalLengthMeters, 0f, 1.00001f));
    }

    [Fact] public void InsufficientDistanceUsesBoundedCorrectionWithoutTeleport()
    {
        var e = Model();
        var p = e.Traverse(25f, .499f, .1f);
        Assert.False(p.TargetReached);
        Assert.True(p.ResidualOverspeedMetersPerSecond > 0f);
        Near(MathF.Sqrt(25f * 25f - 2f * 2.6f * .1f), p.ExitSpeedMetersPerSecond, .00001f);
        Near(.1f, p.CorrectionDistanceMeters, .00001f);
        Assert.Equal(0f, p.DriveDistanceMeters);
    }

    [Fact] public void SlowerRiderIsNotTeleportedUpToEnvelope()
    {
        var p = Model().Traverse(10f, 0f, 40f);
        Assert.Equal(10f, p.ExitSpeedMetersPerSecond);
        Assert.Equal(40f, p.CarryDistanceMeters);
        Assert.Equal(4f, p.TravelTimeSeconds);
    }

    [Fact] public void AboveEquilibriumHasSignedDecelerationNotSpeedCap()
    {
        var e = Model(apex: 50f, gearing: 0f);
        var equilibrium = LongitudinalDynamics.CalculateFullDriveEquilibriumSpeedMetersPerSecond(e.FullDriveReferenceForceNewtons, e.Setup);
        var p = e.Traverse(equilibrium + 5f, .9f, 1f);
        Assert.True(p.ExitSpeedMetersPerSecond < p.EntrySpeedMetersPerSecond);
        Assert.True(p.ExitSpeedMetersPerSecond > equilibrium);
        Assert.Equal(1f, p.DriveDistanceMeters);
        Assert.Equal(1f, p.DecelerationDistanceMeters);
        Assert.Equal(0f, p.CorrectionDistanceMeters);
    }

    [Theory] [InlineData(.98f, SegmentOutcome.Ok)] [InlineData(1.01f, SegmentOutcome.Ok)]
    [InlineData(1.05f, SegmentOutcome.Brake)] [InlineData(1.15f, SegmentOutcome.RunWide)] [InlineData(1.4f, SegmentOutcome.Crash)]
    public void EventBandsReferToLocalEnvelope(float factor, SegmentOutcome expected)
        => Assert.Equal(expected, CornerTestSupport.Probe(.125f, factor).Change.Outcome);

    [Fact] public void OuterRunWideStillCrashes() => Assert.Equal(SegmentOutcome.Crash, CornerTestSupport.Probe(.75f, 1.15f, 4f).Change.Outcome);

    [Fact] public void RunWideRetentionFactorsRemainUnchanged()
    {
        foreach (var skill in new[] { 0f, 50f, 100f })
        {
            var rider = new RiderState(new RiderProfile(1, "r", new RiderSkills(50, 50, skill, 50, 50, 50), RiderStyle.Balanced), 1);
            var track = CornerTestSupport.Track();
            var phase = track.CornerTopology.Resolve(0, 0f, 1f, track.Geometry)!.Value;
            var envelope = CornerTestSupport.Envelope(track, rider).SpeedMetersPerSecond(0f);
            var speed = envelope * 1.16f;
            var result = SegmentPhysics.Apply(new SegmentPhysicsContext(track.Segments[0], 1, speed, track.Geometry,
                CornerTestSupport.Surface, rider.Profile.Skills, .5f, rider.ActiveSetup, CornerPhase: phase));
            Assert.Equal(SegmentOutcome.RunWide, result.Outcome);
            Near(envelope + (speed - envelope) * (.35f + .30f * skill / 100f), result.ContinuousCorrectionTargetSpeedMetersPerSecond!.Value, .00001f);
        }
    }

    [Fact] public void SurfaceChangesCapabilityWithoutChangingProgressLaw()
    {
        var track = CornerTestSupport.Track();
        var rider = new RiderState(1, 1);
        var good = CornerTestSupport.Envelope(track, rider);
        var poor = CornerTestSupport.Envelope(track, rider, new TrackSurfaceState(.8f, 0f, .35f));
        Assert.True(good.ApexSpeedMetersPerSecond > poor.ApexSpeedMetersPerSecond);
        Assert.True(good.SpeedMetersPerSecond(.25f) > poor.SpeedMetersPerSecond(.25f));
    }

    [Fact] public void RadiusTradeOffIsLongerAndFasterOutside()
    {
        var track = CornerTestSupport.Track();
        var inner = CornerTestSupport.Envelope(track, new RiderState(1, 0));
        var outer = CornerTestSupport.Envelope(track, new RiderState(1, 4));
        Assert.True(outer.TotalLengthMeters > inner.TotalLengthMeters);
        Assert.True(outer.ApexSpeedMetersPerSecond > inner.ApexSpeedMetersPerSecond);
    }

    [Fact] public void MinimumAndPeakObserveActualNodesNotForcedApex()
    {
        var p = Model().Traverse(10f, 0f, 100f);
        Assert.Equal(0f, p.MinimumSpeedCornerProgress); // Low entry is already the minimum.
        Assert.Equal(p.Nodes.Min(n => n.SpeedMetersPerSecond), p.MinimumSpeedMetersPerSecond);
        Assert.Equal(p.Nodes.Max(n => n.SpeedMetersPerSecond), p.PeakSpeedMetersPerSecond);
    }

    [Theory] [InlineData(-.1f)] [InlineData(1.1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void InvalidProgressIsRejected(float progress)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Model().SpeedMetersPerSecond(progress));
        Assert.Throws<ArgumentOutOfRangeException>(() => ContinuousCornerEnvelope.DriveAvailability(progress));
    }

    [Fact] public void FractionalAvailabilityDoesNotChangeFullDrivePrimitive()
    {
        var e = Model();
        Assert.Equal(20f, LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(20f, 1f, e.FullDriveReferenceForceNewtons, e.Setup, 0f));
        Assert.Equal(LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(20f, 1f, e.FullDriveReferenceForceNewtons, e.Setup),
            LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(20f, 1f, e.FullDriveReferenceForceNewtons, e.Setup, 1f));
    }

    private static void Near(float expected, float actual, float tolerance) => Assert.InRange(MathF.Abs(expected - actual), 0f, tolerance);
}
