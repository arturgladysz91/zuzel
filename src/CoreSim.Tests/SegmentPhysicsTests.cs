using CoreSim;
using Xunit;

public sealed class SegmentPhysicsTests
{
    [Fact]
    public void TightLane_HighSpeed_Brakes_WhenSlightlyAboveLimit()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var lane = LaneModel.MinLane;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);

        var result = SegmentPhysics.Apply(segment, lane, max * 1.05f);

        Assert.Equal(SegmentOutcome.Brake, result.Outcome);
        Assert.Equal(lane, result.Lane);
        Assert.Equal(max, result.Speed, 3);
    }

    [Fact]
    public void TightLane_TooFast_RunsWide()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var lane = LaneModel.MinLane;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);

        var result = SegmentPhysics.Apply(segment, lane, max * 1.20f);

        Assert.Equal(SegmentOutcome.RunWide, result.Outcome);
        Assert.Equal(lane + 1, result.Lane);
        Assert.True(result.Speed < max * 1.20f);
        Assert.InRange(result.Speed, max, max * 1.20f);
        Assert.True(result.Speed > 0f);
        Assert.True(float.IsFinite(result.Speed));
    }

    [Fact]
    public void TightLane_ExcessiveSpeed_Crashes()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var lane = LaneModel.MinLane;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);

        var result = SegmentPhysics.Apply(segment, lane, max * 1.60f);

        Assert.Equal(SegmentOutcome.Crash, result.Outcome);
        Assert.Equal(lane, result.Lane);
        Assert.Equal(0f, result.Speed, 3);
    }

    [Fact]
    public void Straight_DoesNotAddFreeAdvantage()
    {
        var segment = new TrackSegment(0, SegmentType.Straight);

        var slow = SegmentPhysics.Apply(segment, LaneModel.MinLane, 10f);
        var fast = SegmentPhysics.Apply(segment, LaneModel.MinLane, 12f);

        Assert.Equal(SegmentOutcome.Ok, slow.Outcome);
        Assert.Equal(SegmentOutcome.Ok, fast.Outcome);
        Assert.Equal(10f, slow.Speed, 3);
        Assert.Equal(12f, fast.Speed, 3);
        Assert.Equal(2f, fast.Speed - slow.Speed, 3);
    }

    [Fact]
    public void TurnArcLength_Increases_WithOuterLane()
    {
        var inner = LaneModel.TurnArcLengthMeters(LaneModel.MinLane);
        var outer = LaneModel.TurnArcLengthMeters(LaneModel.MaxLane);

        Assert.True(outer > inner);
    }
}
