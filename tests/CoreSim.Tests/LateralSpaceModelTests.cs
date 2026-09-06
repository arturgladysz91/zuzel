using CoreSim;
using Xunit;

namespace CoreSim.Tests;

public sealed class LateralSpaceModelTests
{
    [Fact]
    public void DefaultGeometryPreservesPreviousOccupancyDistance()
    {
        Assert.Equal(1f, LaneModel.ReferenceLaneSpacingMeters(
            SegmentType.Straight,
            TrackGeometry.Default));

        Assert.True(LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(
            2f,
            2.54f,
            TrackGeometry.Default));
        Assert.False(LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(
            2f,
            2.56f,
            TrackGeometry.Default));
    }

    [Fact]
    public void OccupancyUsesPhysicalMetersNotLaneUnits()
    {
        var oneMeterSpacing = Geometry(laneSpacingMeters: 1f);
        var twoMeterSpacing = Geometry(laneSpacingMeters: 2f);

        var oneMeterDistance = LateralSpaceModel.LateralDistanceMeters(2f, 2.4f, oneMeterSpacing);
        var twoMeterDistance = LateralSpaceModel.LateralDistanceMeters(2f, 2.4f, twoMeterSpacing);

        Assert.Equal(0.4f, oneMeterDistance, 5);
        Assert.Equal(0.8f, twoMeterDistance, 5);
        Assert.True(LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(2f, 2.4f, oneMeterSpacing));
        Assert.False(LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(2f, 2.4f, twoMeterSpacing));
    }

    [Fact]
    public void EquivalentPhysicalDistanceIsGeometryIndependent()
    {
        var oneMeterSpacing = Geometry(laneSpacingMeters: 1f);
        var twoMeterSpacing = Geometry(laneSpacingMeters: 2f);

        var oneMeterResult = LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(
            2f,
            2.4f,
            oneMeterSpacing);
        var twoMeterResult = LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(
            2f,
            2.2f,
            twoMeterSpacing);

        Assert.Equal(
            LateralSpaceModel.LateralDistanceMeters(2f, 2.4f, oneMeterSpacing),
            LateralSpaceModel.LateralDistanceMeters(2f, 2.2f, twoMeterSpacing),
            5);
        Assert.Equal(oneMeterResult, twoMeterResult);
    }

    [Fact]
    public void OccupancyIsSymmetricAroundReferenceLane()
    {
        var geometry = Geometry(laneSpacingMeters: 1.25f);

        var innerResult = LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(2f, 1.6f, geometry);
        var outerResult = LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(2f, 2.4f, geometry);

        Assert.Equal(
            LateralSpaceModel.LateralDistanceMeters(2f, 1.6f, geometry),
            LateralSpaceModel.LateralDistanceMeters(2f, 2.4f, geometry),
            5);
        Assert.Equal(innerResult, outerResult);
    }

    [Fact]
    public void ContactThresholdUsesPhysicalMeters()
    {
        var oneMeterSpacing = Geometry(laneSpacingMeters: 1f);
        var twoMeterSpacing = Geometry(laneSpacingMeters: 2f);

        Assert.True(LateralSpaceModel.IsWithinProvisionalContactThreshold(2f, 2.4f, oneMeterSpacing));
        Assert.True(LateralSpaceModel.IsWithinProvisionalContactThreshold(2f, 1.6f, oneMeterSpacing));
        Assert.False(LateralSpaceModel.IsWithinProvisionalContactThreshold(2f, 2.4f, twoMeterSpacing));
        Assert.False(LateralSpaceModel.IsWithinProvisionalContactThreshold(2f, 1.6f, twoMeterSpacing));
    }

    private static TrackGeometry Geometry(float laneSpacingMeters)
        => new(60f, 24f, laneSpacingMeters, 0.8f);
}
