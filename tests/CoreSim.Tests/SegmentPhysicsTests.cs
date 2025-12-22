using CoreSim;
using Xunit;

namespace CoreSim.Tests;

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
    public void Turn_WithSpeedInRunWideRange_ReturnsRunWideAndMovesOutward()
    {
        var segment = new TrackSegment(4, SegmentType.TurnMiddle);
        var lane = 1;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);
        var speed = max * SegmentPhysics.RunWideSpeedFactor;

        var result = SegmentPhysics.Apply(segment, lane, speed);

        Assert.Equal(SegmentOutcome.RunWide, result.Outcome);
        Assert.Equal(lane + 1, result.Lane);
        Assert.Equal(speed, result.Speed, 3);
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
}
