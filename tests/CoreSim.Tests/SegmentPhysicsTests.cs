using CoreSim;
using CoreSim.Setup;
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
        Assert.True(result.Speed < speed);
        Assert.InRange(result.Speed, max, speed);
        Assert.True(result.Speed > 0f);
        Assert.True(float.IsFinite(result.Speed));
    }

    [Fact]
    public void AdvancedRunWide_CorrectsSpeedWithoutDroppingBelowIndividualLimit()
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
        Assert.True(result.Speed < entrySpeed);
        Assert.InRange(result.Speed, max, entrySpeed);
        Assert.True(result.Speed > 0f);
        Assert.True(float.IsFinite(result.Speed));
    }

    [Fact]
    public void AdvancedRunWide_HigherSlideControlRetainsMoreOfIndividualOverspeed()
    {
        var segment = new TrackSegment(6, SegmentType.TurnMiddle);
        const int lane = 1;
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var lowControlSkills = new RiderSkills(50f, 50f, 0f, 50f, 50f, 50f);
        var highControlSkills = new RiderSkills(50f, 50f, 100f, 50f, 50f, 50f);

        var lowControlResult = ResolveAdvancedRunWide(segment, lane, surface, lowControlSkills);
        var highControlResult = ResolveAdvancedRunWide(segment, lane, surface, highControlSkills);

        Assert.Equal(SegmentOutcome.RunWide, lowControlResult.Result.Outcome);
        Assert.Equal(SegmentOutcome.RunWide, highControlResult.Result.Outcome);
        Assert.True(lowControlResult.Result.Speed < lowControlResult.EntrySpeed);
        Assert.True(highControlResult.Result.Speed < highControlResult.EntrySpeed);
        Assert.True(lowControlResult.Result.Speed >= lowControlResult.MaxSafeSpeed);
        Assert.True(highControlResult.Result.Speed >= highControlResult.MaxSafeSpeed);

        var lowRetainedFraction = (lowControlResult.Result.Speed - lowControlResult.MaxSafeSpeed)
            / (lowControlResult.EntrySpeed - lowControlResult.MaxSafeSpeed);
        var highRetainedFraction = (highControlResult.Result.Speed - highControlResult.MaxSafeSpeed)
            / (highControlResult.EntrySpeed - highControlResult.MaxSafeSpeed);

        Assert.True(highRetainedFraction > lowRetainedFraction);
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

    private static (SegmentResolution Result, float MaxSafeSpeed, float EntrySpeed) ResolveAdvancedRunWide(
        TrackSegment segment,
        int lane,
        TrackSurfaceState surface,
        RiderSkills skills)
    {
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            TrackGeometry.Default,
            surface,
            skills,
            BikeSetup.Neutral);
        var entrySpeed = maxSafeSpeed * 1.15f;
        var result = SegmentPhysics.Apply(new SegmentPhysicsContext(
            segment,
            lane,
            entrySpeed,
            TrackGeometry.Default,
            surface,
            skills,
            Morale: 0.5f,
            Setup: BikeSetup.Neutral));

        return (result, maxSafeSpeed, entrySpeed);
    }
}
