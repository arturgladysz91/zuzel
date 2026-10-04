using CoreSim;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class ContinuousCornerConstraintTests
{
    private static readonly TrackSegment Turn = new(1, SegmentType.TurnMiddle);
    private static readonly TrackSurfaceState Surface = new(1f, 0f, 0.35f);

    [Fact]
    public void AdvancedBelowMaxKeepsSpeedAndNeedsNoCorrection()
    {
        var max = Max();
        var result = Advanced(max - 1f);

        Assert.Equal(SegmentOutcome.Ok, result.Outcome);
        Assert.Equal(max - 1f, result.Speed);
        Assert.Null(result.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void AdvancedQuietOverspeedKeepsInputSpeedAndTargetsMax()
    {
        var max = Max();
        var speed = max * 1.01f;
        var result = Advanced(speed);

        Assert.Equal(SegmentOutcome.Ok, result.Outcome);
        Assert.Equal(speed, result.Speed);
        Assert.Equal(max, result.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void AdvancedBrakeKeepsInputSpeedAndTargetsMax()
    {
        var max = Max();
        var speed = max * 1.04f;
        var result = Advanced(speed);

        Assert.Equal(SegmentOutcome.Brake, result.Outcome);
        Assert.Equal(speed, result.Speed);
        Assert.Equal(max, result.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void AdvancedRunWideKeepsInputSpeedAndTargetsRetainedOverspeed()
    {
        var max = Max();
        var speed = max * 1.15f;
        var result = Advanced(speed);
        var expected = max + (speed - max) * SegmentPhysics.NeutralRunWideOverspeedRetention;

        Assert.Equal(SegmentOutcome.RunWide, result.Outcome);
        Assert.Equal(speed, result.Speed);
        Assert.Equal(expected, result.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void AdvancedRunWideStillForcesOneLaneOutward()
        => Assert.Equal(2, Advanced(Max() * 1.15f).Lane);

    [Fact]
    public void AdvancedOuterLaneRunWideBandStillCrashes()
    {
        var max = Max(lane: LaneModel.MaxLane, lateralPosition: LaneModel.MaxLane);
        var result = Advanced(
            max * 1.15f,
            lane: LaneModel.MaxLane,
            lateralPosition: LaneModel.MaxLane);

        Assert.Equal(SegmentOutcome.Crash, result.Outcome);
        Assert.Equal(0f, result.Speed);
        Assert.Null(result.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void AdvancedAboveRunWideThresholdStillCrashes()
    {
        var result = Advanced(Max() * 1.27f);

        Assert.Equal(SegmentOutcome.Crash, result.Outcome);
        Assert.Equal(0f, result.Speed);
        Assert.Null(result.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void MoraleDoesNotChangeCorrectionTarget()
    {
        var speed = Max() * 1.04f;

        Assert.Equal(
            Advanced(speed, morale: 0f).ContinuousCorrectionTargetSpeedMetersPerSecond,
            Advanced(speed, morale: 1f).ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void GeometryChangesMaxButNotCorrectionSemantics()
    {
        var broad = new TrackGeometry(
            TrackGeometry.Default.StraightLengthMeters,
            innerRadiusMeters: 36f,
            TrackGeometry.Default.StraightWidthMeters,
            TrackGeometry.Default.TurnWidthMeters,
            TrackGeometry.Default.TurnSegmentAngleRadians);
        var tightMax = Max();
        var broadMax = Max(geometry: broad);
        var tight = Advanced(tightMax * 1.04f);
        var broadResult = Advanced(broadMax * 1.04f, geometry: broad);

        Assert.True(broadMax > tightMax);
        Assert.Equal(SegmentOutcome.Brake, tight.Outcome);
        Assert.Equal(tight.Outcome, broadResult.Outcome);
        Assert.Equal(tightMax, tight.ContinuousCorrectionTargetSpeedMetersPerSecond);
        Assert.Equal(broadMax, broadResult.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void LegacyOkStillClampsExactlyAsBefore()
    {
        var max = SegmentPhysics.MaxSafeTurnSpeed(1);
        var result = SegmentPhysics.Apply(Turn, 1, max);
        Assert.Equal(new SegmentResolution(SegmentOutcome.Ok, 1, max), result);
    }

    [Fact]
    public void LegacyBrakeStillClampsToMax()
    {
        var max = SegmentPhysics.MaxSafeTurnSpeed(1);
        var result = SegmentPhysics.Apply(Turn, 1, max * 1.05f);
        Assert.Equal(SegmentOutcome.Brake, result.Outcome);
        Assert.Equal(max, result.Speed);
        Assert.Null(result.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void LegacyRunWideStillUsesNeutralRetentionImmediately()
    {
        var max = SegmentPhysics.MaxSafeTurnSpeed(1);
        var speed = max * 1.2f;
        var result = SegmentPhysics.Apply(Turn, 1, speed);

        Assert.Equal(SegmentOutcome.RunWide, result.Outcome);
        Assert.Equal(max + (speed - max) * 0.5f, result.Speed);
        Assert.Null(result.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void LegacyCrashStillReturnsZero()
    {
        var max = SegmentPhysics.MaxSafeTurnSpeed(1);
        var result = SegmentPhysics.Apply(Turn, 1, max * 1.31f);
        Assert.Equal(SegmentOutcome.Crash, result.Outcome);
        Assert.Equal(0f, result.Speed);
    }

    private static float Max(
        int lane = 1,
        float lateralPosition = 1f,
        TrackGeometry? geometry = null)
        => SegmentPhysics.MaxSafeTurnSpeed(
            lateralPosition,
            geometry ?? TrackGeometry.Default,
            Surface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);

    private static SegmentResolution Advanced(
        float speed,
        int lane = 1,
        float lateralPosition = 1f,
        float morale = 0.5f,
        TrackGeometry? geometry = null)
        => SegmentPhysics.Apply(new SegmentPhysicsContext(
            Turn,
            lane,
            speed,
            geometry ?? TrackGeometry.Default,
            Surface,
            RiderSkills.Balanced,
            morale,
            BikeSetup.Neutral,
            LateralPosition: lateralPosition));
}
