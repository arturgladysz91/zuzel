using CoreSim;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class SegmentPhysicsTests
{
    [Fact]
    public void Straight_ReturnsOk_WithoutChangingSpeed()
    {
        var segment = new TrackSegment(1, SegmentType.Straight);
        var lane = LaneModel.MinLane;
        const float speed = 10f;

        var result = SegmentPhysics.Apply(segment, lane, speed);

        Assert.Equal(SegmentOutcome.Ok, result.Outcome);
        Assert.Equal(lane, result.Lane);
        Assert.Equal(speed, result.Speed, 3);
    }

    [Fact]
    public void Turn_WithSpeedAtOrBelowMax_ReturnsOk()
    {
        var segment = new TrackSegment(2, SegmentType.TurnMiddle);
        var lane = 2;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);

        var result = SegmentPhysics.Apply(segment, lane, max);

        Assert.Equal(SegmentOutcome.Ok, result.Outcome);
        Assert.Equal(lane, result.Lane);
        Assert.Equal(max, result.Speed, 3);
    }
    
    [Fact]
    public void OuterLane_TooFast_Crashes_WhenNoRoomToRunWide()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var lane = LaneModel.MaxLane;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);

        var result = SegmentPhysics.Apply(segment, lane, max * 1.20f);

        Assert.Equal(SegmentOutcome.Crash, result.Outcome);
        Assert.Equal(lane, result.Lane);
        Assert.Equal(0f, result.Speed, 3);
    }

    [Fact]
    public void Turn_WithSpeedInBrakeRange_ReturnsBrakeAndCapsSpeed()
    {
        var segment = new TrackSegment(3, SegmentType.TurnMiddle);
        var lane = 1;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);
        var speed = max * SegmentPhysics.BrakeSpeedFactor;

        var result = SegmentPhysics.Apply(segment, lane, speed);

        Assert.Equal(SegmentOutcome.Brake, result.Outcome);
        Assert.Equal(lane, result.Lane);
        Assert.Equal(max, result.Speed, 3);
    }

    [Fact]
    public void LegacyRunWide_UsesNeutralOverspeedRetentionAndMovesOutward()
    {
        var segment = new TrackSegment(4, SegmentType.TurnMiddle);
        const int plannedLane = 1;
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(plannedLane);
        var entrySpeed = maxSafeSpeed
            * ((SegmentPhysics.BrakeSpeedFactor + SegmentPhysics.RunWideSpeedFactor) / 2f);
        var expectedSpeed = maxSafeSpeed + (entrySpeed - maxSafeSpeed) * 0.50f;

        var result = SegmentPhysics.Apply(segment, plannedLane, entrySpeed);

        Assert.Equal(SegmentOutcome.RunWide, result.Outcome);
        Assert.Equal(plannedLane + 1, result.Lane);
        Assert.Equal(expectedSpeed, result.Speed, 4);
        Assert.True(result.Speed < entrySpeed);
        Assert.True(result.Speed >= maxSafeSpeed);
        Assert.True(result.Speed > 0f);
        Assert.True(float.IsFinite(result.Speed));
    }

    [Fact]
    public void AdvancedRunWide_ProducesContinuousTargetWithoutChangingInputSpeed()
    {
        var segment = new TrackSegment(5, SegmentType.TurnMiddle);
        const int lane = 1;
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var skills = RiderSkills.Balanced;
        var max = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            TrackGeometry.Default,
            surface,
            skills,
            BikeSetup.Neutral);
        var entrySpeed = max * 1.20f;

        var result = SegmentPhysics.Apply(new SegmentPhysicsContext(
            segment,
            lane,
            entrySpeed,
            TrackGeometry.Default,
            surface,
            skills,
            Morale: 0.5f,
            Setup: BikeSetup.Neutral));

        Assert.Equal(SegmentOutcome.RunWide, result.Outcome);
        Assert.Equal(lane + 1, result.Lane);
        Assert.Equal(entrySpeed, result.Speed);
        var target = Assert.IsType<float>(result.ContinuousCorrectionTargetSpeedMetersPerSecond);
        Assert.True(target < entrySpeed);
        Assert.InRange(target, max, entrySpeed);
        Assert.True(result.Speed > 0f);
        Assert.True(float.IsFinite(result.Speed));
    }

    [Theory]
    [InlineData(0f, 0.35f)]
    [InlineData(50f, 0.50f)]
    [InlineData(100f, 0.65f)]
    public void AdvancedRunWide_RetainsExpectedFractionOfIndividualOverspeed(
        float slideControl,
        float expectedRetainedFraction)
    {
        var segment = new TrackSegment(6, SegmentType.TurnMiddle);
        const int plannedLane = 1;
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var skills = new RiderSkills(50f, 50f, slideControl, 50f, 50f, 50f);
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            plannedLane,
            TrackGeometry.Default,
            surface,
            skills,
            BikeSetup.Neutral);
        var entrySpeed = maxSafeSpeed * 1.16f;

        var result = SegmentPhysics.Apply(new SegmentPhysicsContext(
            segment,
            plannedLane,
            entrySpeed,
            TrackGeometry.Default,
            surface,
            skills,
            Morale: 0.5f,
            Setup: BikeSetup.Neutral));
        var correctionTarget = Assert.IsType<float>(
            result.ContinuousCorrectionTargetSpeedMetersPerSecond);
        var actualRetainedFraction = (correctionTarget - maxSafeSpeed)
            / (entrySpeed - maxSafeSpeed);

        Assert.Equal(SegmentOutcome.RunWide, result.Outcome);
        Assert.Equal(plannedLane + 1, result.Lane);
        Assert.InRange(
            MathF.Abs(actualRetainedFraction - expectedRetainedFraction),
            0f,
            0.0001f);
        Assert.Equal(entrySpeed, result.Speed);
        Assert.True(correctionTarget < entrySpeed);
        Assert.True(correctionTarget >= maxSafeSpeed);
        Assert.True(result.Speed > 0f);
        Assert.True(float.IsFinite(result.Speed));
    }

    [Fact]
    public void Turn_OnMaxLane_WithSpeedInRunWideRange_UsesFixedLogic()
    {
        var segment = new TrackSegment(5, SegmentType.TurnMiddle);
        var lane = LaneModel.MaxLane;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);
        var speed = max * SegmentPhysics.RunWideSpeedFactor;

        var result = SegmentPhysics.Apply(segment, lane, speed);

        Assert.Equal(SegmentOutcome.Crash, result.Outcome);
        Assert.Equal(lane, result.Lane);
        Assert.Equal(0f, result.Speed, 3);
    }

    [Fact]
    public void Turn_WithSpeedAboveRunWideRange_CrashesAndZeroesSpeed()
    {
        var segment = new TrackSegment(7, SegmentType.TurnMiddle);
        const int lane = 1;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);
        var speed = max * (SegmentPhysics.RunWideSpeedFactor + 0.01f);

        var result = SegmentPhysics.Apply(segment, lane, speed);

        Assert.Equal(SegmentOutcome.Crash, result.Outcome);
        Assert.Equal(lane, result.Lane);
        Assert.Equal(0f, result.Speed);
    }

}
