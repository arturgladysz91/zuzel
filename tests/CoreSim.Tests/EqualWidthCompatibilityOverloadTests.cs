using CoreSim;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

public sealed class EqualWidthCompatibilityOverloadTests
{
    private static readonly TrackGeometry EqualWidthGeometry = new(
        60f, 24f, 10f, 10f, MathF.PI / 3f);

    private static readonly TrackGeometry UnequalWidthGeometry = new(
        60f, 24f, 10f, 14f, MathF.PI / 3f);

    private static readonly TrackSurfaceState IdealSurface = new(1f, 0f, 0f);

    [Fact]
    public void EqualWidthCompatibilityOverloadsStillWork()
    {
        Assert.Equal(2f, LateralSpaceModel.LateralDistanceMeters(1f, 2f, EqualWidthGeometry), 6);
        Assert.True(LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(1f, 1.25f, EqualWidthGeometry));
        Assert.True(LateralSpaceModel.IsWithinProvisionalContactThreshold(1f, 1.25f, EqualWidthGeometry));
        Assert.Equal(2, LateralMovementModel.CalculatePlannedLane(1, 1f, 3, EqualWidthGeometry, true));
        Assert.True(LateralMovementModel.CalculateMaxLateralDelta(1f, EqualWidthGeometry, IdealSurface, RiderSkills.Balanced) > 0f);
        Assert.True(LateralMovementModel.MoveTowards(0f, 4, 1f, EqualWidthGeometry, IdealSurface, RiderSkills.Balanced) > 0f);
        Assert.Equal(0.25f, LateralMovementModel.MoveOutwardByPhysicalDistance(0f, 0.5f, 4f, EqualWidthGeometry), 6);

        var withinTolerance = new TrackGeometry(
            60f,
            24f,
            10f,
            MathF.BitIncrement(10f),
            MathF.PI / 3f);
        Assert.Equal(2f, LateralSpaceModel.LateralDistanceMeters(1f, 2f, withinTolerance), 5);
    }

    [Fact]
    public void UnequalWidthCompatibilityLateralDistanceIsRejected()
        => AssertCompatibilityGeometryRejected(
            () => LateralSpaceModel.LateralDistanceMeters(1f, 2f, UnequalWidthGeometry));

    [Fact]
    public void UnequalWidthCompatibilityOccupancyIsRejected()
        => AssertCompatibilityGeometryRejected(
            () => LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(1f, 2f, UnequalWidthGeometry));

    [Fact]
    public void UnequalWidthCompatibilityContactIsRejected()
        => AssertCompatibilityGeometryRejected(
            () => LateralSpaceModel.IsWithinProvisionalContactThreshold(1f, 2f, UnequalWidthGeometry));

    [Fact]
    public void UnequalWidthCompatibilityPlannedLaneIsRejected()
        => AssertCompatibilityGeometryRejected(
            () => LateralMovementModel.CalculatePlannedLane(1, 1f, 3, UnequalWidthGeometry, true));

    [Fact]
    public void UnequalWidthCompatibilityMaxLateralDeltaIsRejected()
        => AssertCompatibilityGeometryRejected(
            () => LateralMovementModel.CalculateMaxLateralDelta(
                1f, UnequalWidthGeometry, IdealSurface, RiderSkills.Balanced));

    [Fact]
    public void UnequalWidthCompatibilityMoveTowardsIsRejected()
        => AssertCompatibilityGeometryRejected(
            () => LateralMovementModel.MoveTowards(
                0f, 4, 1f, UnequalWidthGeometry, IdealSurface, RiderSkills.Balanced));

    [Fact]
    public void UnequalWidthCompatibilityOutwardDisplacementIsRejected()
        => AssertCompatibilityGeometryRejected(
            () => LateralMovementModel.MoveOutwardByPhysicalDistance(0f, 0.5f, 4f, UnequalWidthGeometry));

    [Fact]
    public void SegmentAwareOverloadsStillAcceptUnequalWidths()
    {
        Assert.Equal(2f, LateralSpaceModel.LateralDistanceMeters(
            1f, 2f, SegmentType.Straight, UnequalWidthGeometry), 6);
        Assert.True(LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(
            1f, 1.25f, SegmentType.Straight, UnequalWidthGeometry));
        Assert.True(LateralSpaceModel.IsWithinProvisionalContactThreshold(
            1f, 1.25f, SegmentType.Straight, UnequalWidthGeometry));
        Assert.Equal(2, LateralMovementModel.CalculatePlannedLane(
            1, 1f, 3, SegmentType.TurnMiddle, UnequalWidthGeometry, true));
        Assert.True(LateralMovementModel.CalculateMaxLateralDistanceMeters(
            1f, SegmentType.TurnMiddle, UnequalWidthGeometry, IdealSurface, RiderSkills.Balanced) > 0f);
        Assert.True(LateralMovementModel.MoveTowards(
            0f, 4, 1f, SegmentType.TurnMiddle, UnequalWidthGeometry, IdealSurface, RiderSkills.Balanced) > 0f);
        Assert.True(LateralMovementModel.MoveOutwardByPhysicalDistance(
            0f, 0.5f, 4f, SegmentType.TurnMiddle, UnequalWidthGeometry) > 0f);
    }

    [Fact]
    public void StandingExampleProductionPathStillAcceptsUnequalWidths()
    {
        var geometry = Track.CreateStandingStartExample().Geometry;
        var result = CalibrationSkillSweep.RunScenario(
            new CalibrationSkillScenario("unequal-width-production-path", RiderSkills.Balanced));

        Assert.Equal(10f, geometry.StraightWidthMeters);
        Assert.Equal(14f, geometry.TurnWidthMeters);
        Assert.Equal(4, result.Riders.Count);
        Assert.All(result.Riders, rider => Assert.True(float.IsFinite(rider.TotalTimeSeconds)));
    }

    private static void AssertCompatibilityGeometryRejected(Action action)
    {
        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("geometry", exception.ParamName);
        Assert.Contains("without SegmentType", exception.Message);
        Assert.Contains("segment-aware overload", exception.Message);
    }
}
