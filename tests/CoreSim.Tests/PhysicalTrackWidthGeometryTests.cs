using CoreSim;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class PhysicalTrackWidthGeometryTests
{
    private static readonly TrackSurfaceState IdealSurface = new(1f, 0f, 0.35f);

    [Fact]
    public void NormalizedLateralFractionMapsZeroToZero()
        => Assert.Equal(0f, LaneModel.NormalizedLateralFraction(0f));

    [Fact]
    public void NormalizedLateralFractionMapsFourToOne()
        => Assert.Equal(1f, LaneModel.NormalizedLateralFraction(4f));

    [Theory]
    [InlineData(1f, 0.25f)]
    [InlineData(2f, 0.50f)]
    [InlineData(2.5f, 0.625f)]
    [InlineData(3f, 0.75f)]
    public void NormalizedLateralFractionMapsContinuousValues(float position, float fraction)
        => Assert.Equal(fraction, LaneModel.NormalizedLateralFraction(position), 6);

    [Fact]
    public void StandingStartStraightWidthIsTenMeters()
        => Assert.Equal(10f, Track.CreateStandingStartExample().Geometry.StraightWidthMeters);

    [Fact]
    public void StandingStartTurnWidthIsFourteenMeters()
        => Assert.Equal(14f, Track.CreateStandingStartExample().Geometry.TurnWidthMeters);

    [Fact]
    public void StandingStartStraightUsableWidthIsEightMeters()
        => Assert.Equal(8f, Usable(SegmentType.Straight));

    [Fact]
    public void StandingStartTurnUsableWidthIsTwelveMeters()
        => Assert.Equal(12f, Usable(SegmentType.TurnMiddle));

    [Fact]
    public void CompatibilityGeometryPreservesOneMeterReferenceSpacing()
    {
        var geometry = Track.CreateExample().Geometry;
        Assert.Equal(6f, geometry.StraightWidthMeters);
        Assert.Equal(6f, geometry.TurnWidthMeters);
        Assert.Equal(1f, LaneModel.ReferenceLaneSpacingMeters(SegmentType.Straight, geometry));
        Assert.Equal(1f, LaneModel.ReferenceLaneSpacingMeters(SegmentType.TurnMiddle, geometry));
    }

    [Fact]
    public void StandingStartStraightReferenceSpacingIsTwoMeters()
        => Assert.Equal(2f, Spacing(SegmentType.Straight));

    [Fact]
    public void StandingStartTurnReferenceSpacingIsThreeMeters()
        => Assert.Equal(3f, Spacing(SegmentType.TurnEntry));

    [Theory]
    [InlineData(2f, 14f)]
    [InlineData(1.99f, 14f)]
    [InlineData(10f, 2f)]
    [InlineData(10f, 1.99f)]
    [InlineData(float.NaN, 14f)]
    [InlineData(10f, float.PositiveInfinity)]
    public void InvalidWidthSmallerThanReferenceMarginsIsRejected(float straightWidth, float turnWidth)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TrackGeometry(
            60f,
            24f,
            straightWidth,
            turnWidth,
            MathF.PI / 3f));

    [Fact]
    public void StraightAndTurnWidthsAreIndependent()
    {
        var geometry = new TrackGeometry(60f, 24f, 8f, 17f, MathF.PI / 3f);
        Assert.Equal(8f, LaneModel.PhysicalTrackWidthMeters(SegmentType.Straight, geometry));
        Assert.Equal(17f, LaneModel.PhysicalTrackWidthMeters(SegmentType.TurnExit, geometry));
        Assert.Equal(1.5f, LaneModel.ReferenceLaneSpacingMeters(SegmentType.Straight, geometry));
        Assert.Equal(3.75f, LaneModel.ReferenceLaneSpacingMeters(SegmentType.TurnExit, geometry));
    }

    [Fact]
    public void StandingStraightLaneOffsetsFromInnerEdgeAreOneThreeFiveSevenNine()
        => Assert.Equal(
            new[] { 1f, 3f, 5f, 7f, 9f },
            Enumerable.Range(0, 5).Select(lane => OffsetFromEdge(lane, SegmentType.Straight)));

    [Fact]
    public void StandingTurnLaneOffsetsFromInnerEdgeAreOneFourSevenTenThirteen()
        => Assert.Equal(
            new[] { 1f, 4f, 7f, 10f, 13f },
            Enumerable.Range(0, 5).Select(lane => OffsetFromEdge(lane, SegmentType.TurnMiddle)));

    [Fact]
    public void ContinuousLateralOffsetInterpolatesPhysically()
    {
        var geometry = StandingGeometry;
        Assert.Equal(5f, LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(2.5f, SegmentType.Straight, geometry));
        Assert.Equal(7.5f, LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(2.5f, SegmentType.TurnMiddle, geometry));
        Assert.Equal(8.5f, LaneModel.PhysicalLateralOffsetFromInnerEdgeMeters(2.5f, SegmentType.TurnMiddle, geometry));
    }

    [Theory]
    [InlineData(SegmentType.Straight, 0f)]
    [InlineData(SegmentType.Straight, 1.25f)]
    [InlineData(SegmentType.TurnEntry, 2.5f)]
    [InlineData(SegmentType.TurnExit, 4f)]
    public void PhysicalOffsetInverseRoundTrips(SegmentType type, float position)
    {
        var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(position, type, StandingGeometry);
        Assert.Equal(position, LaneModel.LateralPositionFromPhysicalOffsetMeters(offset, type, StandingGeometry), 6);
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(12.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void PhysicalOffsetInverseRejectsOutsideUsableSpan(float offset)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            LaneModel.LateralPositionFromPhysicalOffsetMeters(offset, SegmentType.TurnMiddle, StandingGeometry));

    [Fact]
    public void SameNormalizedPositionMapsToDifferentPhysicalOffsetsOnStraightAndTurn()
    {
        Assert.Equal(4f, LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(2f, SegmentType.Straight, StandingGeometry));
        Assert.Equal(6f, LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(2f, SegmentType.TurnMiddle, StandingGeometry));
    }

    [Fact]
    public void StandingTurnLaneZeroRadiusIsTwentyFour()
        => Assert.Equal(24f, LaneModel.TurnArcRadiusMeters(0f, StandingGeometry), 6);

    [Fact]
    public void StandingTurnLaneOneRadiusIsTwentySeven()
        => Assert.Equal(27f, LaneModel.TurnArcRadiusMeters(1f, StandingGeometry), 6);

    [Fact]
    public void StandingTurnLaneTwoRadiusIsThirty()
        => Assert.Equal(30f, LaneModel.TurnArcRadiusMeters(2f, StandingGeometry), 6);

    [Fact]
    public void StandingTurnLaneThreeRadiusIsThirtyThree()
        => Assert.Equal(33f, LaneModel.TurnArcRadiusMeters(3f, StandingGeometry), 6);

    [Fact]
    public void StandingTurnLaneFourRadiusIsThirtySix()
        => Assert.Equal(36f, LaneModel.TurnArcRadiusMeters(4f, StandingGeometry), 6);

    [Fact]
    public void TurnRadiusInterpolatesContinuousLateralPosition()
        => Assert.Equal(31.5f, LaneModel.TurnArcRadiusMeters(2.5f, StandingGeometry), 6);

    [Fact]
    public void TurnRadiusIsMonotonicAcrossWidth()
    {
        var radii = new[] { 0f, 0.5f, 1f, 2.5f, 3f, 4f }
            .Select(position => LaneModel.TurnArcRadiusMeters(position, StandingGeometry))
            .ToArray();
        Assert.All(radii.Zip(radii.Skip(1)), pair => Assert.True(pair.First < pair.Second));
    }

    [Fact]
    public void StandingMeasurementReferenceLapRemainsAbout280Point796Meters()
        => Assert.Equal(130f + 2f * MathF.PI * 24f, LapDistance(StandingTrack, 0f), 3);

    [Fact]
    public void StandingOuterReferenceLapIsAbout356Point195Meters()
        => Assert.Equal(130f + 2f * MathF.PI * 36f, LapDistance(StandingTrack, 4f), 3);

    [Fact]
    public void LapDistanceIncreasesMonotonicallyAcrossLateralPosition()
    {
        var distances = new[] { 0f, 0.25f, 1f, 2f, 2.5f, 3f, 4f }
            .Select(position => LapDistance(StandingTrack, position))
            .ToArray();
        Assert.All(distances.Zip(distances.Skip(1)), pair => Assert.True(pair.First < pair.Second));
    }

    [Fact]
    public void StraightLengthDoesNotDependOnLateralPosition()
    {
        var straight = new TrackSegment(0, SegmentType.Straight);
        Assert.Equal(
            LaneModel.SegmentLengthMeters(straight, 0f, StandingGeometry),
            LaneModel.SegmentLengthMeters(straight, 4f, StandingGeometry));
    }

    [Fact]
    public void TurnLengthUsesPhysicalRadius()
    {
        var turn = new TrackSegment(0, SegmentType.TurnMiddle);
        Assert.Equal(31.5f * MathF.PI / 3f, LaneModel.SegmentLengthMeters(turn, 2.5f, StandingGeometry), 5);
    }

    [Fact]
    public void CompatibilityExampleDistancesRemainUnchanged()
    {
        var track = Track.CreateExample();
        foreach (var position in new[] { 0f, 0.25f, 1f, 2.5f, 3f, 4f })
        {
            var formerDistance = 120f + 2f * MathF.PI * (24f + position);
            Assert.Equal(formerDistance, LapDistance(track, position), 3);
        }
    }

    [Fact]
    public void PhysicalLateralMovementUsesStraightWidth()
        => Assert.Equal(1f, MaxPhysicalDistance(SegmentType.Straight), 6);

    [Fact]
    public void PhysicalLateralMovementUsesTurnWidth()
        => Assert.Equal(1.5f, MaxPhysicalDistance(SegmentType.TurnMiddle), 6);

    [Fact]
    public void SameNormalizedDeltaCoversMoreMetersOnWiderTurn()
        => Assert.True(MaxPhysicalDistance(SegmentType.TurnMiddle) > MaxPhysicalDistance(SegmentType.Straight));

    [Fact]
    public void MoveTowardsConvertsPhysicalDistanceBackToNormalizedPosition()
    {
        var straight = LateralMovementModel.MoveTowards(0f, 4, 2f, SegmentType.Straight, StandingGeometry, IdealSurface, RiderSkills.Balanced);
        var turn = LateralMovementModel.MoveTowards(0f, 4, 2f, SegmentType.TurnMiddle, StandingGeometry, IdealSurface, RiderSkills.Balanced);
        Assert.Equal(1f, straight, 6);
        Assert.Equal(1f, turn, 6);
    }

    [Fact]
    public void ArrivalToleranceIsMeasuredInPhysicalMeters()
    {
        Assert.Equal(1, LateralMovementModel.CalculatePlannedLane(1, 0.974f, 4, SegmentType.Straight, StandingGeometry, true));
        Assert.Equal(2, LateralMovementModel.CalculatePlannedLane(1, 0.975f, 4, SegmentType.Straight, StandingGeometry, true));
    }

    [Fact]
    public void OutwardPhysicalDisplacementProducesSmallerNormalizedDeltaOnWiderSegment()
    {
        var straight = LateralMovementModel.MoveOutwardByPhysicalDistance(2f, 0.5f, 3f, SegmentType.Straight, StandingGeometry);
        var turn = LateralMovementModel.MoveOutwardByPhysicalDistance(2f, 0.5f, 3f, SegmentType.TurnMiddle, StandingGeometry);
        Assert.Equal(0.25f, straight - 2f, 6);
        Assert.Equal(1f / 6f, turn - 2f, 6);
        Assert.True(turn < straight);
    }

    [Fact]
    public void LateralMovementRemainsDeterministic()
    {
        var first = LateralMovementModel.MoveTowards(1.25f, 4, 1.234f, SegmentType.TurnExit, StandingGeometry, IdealSurface, RiderSkills.Balanced);
        var second = LateralMovementModel.MoveTowards(1.25f, 4, 1.234f, SegmentType.TurnExit, StandingGeometry, IdealSurface, RiderSkills.Balanced);
        Assert.Equal(first, second);
    }

    [Fact]
    public void PhysicalSeparationUsesStraightWidth()
        => Assert.Equal(2f, LateralSpaceModel.LateralDistanceMeters(1f, 2f, SegmentType.Straight, StandingGeometry), 6);

    [Fact]
    public void PhysicalSeparationUsesTurnWidth()
        => Assert.Equal(3f, LateralSpaceModel.LateralDistanceMeters(1f, 2f, SegmentType.TurnMiddle, StandingGeometry), 6);

    [Fact]
    public void SameNormalizedGapHasLargerPhysicalSeparationOnTurn()
        => Assert.True(
            LateralSpaceModel.LateralDistanceMeters(1f, 2f, SegmentType.TurnMiddle, StandingGeometry)
            > LateralSpaceModel.LateralDistanceMeters(1f, 2f, SegmentType.Straight, StandingGeometry));

    [Fact]
    public void OccupancyThresholdUsesPhysicalMeters()
    {
        Assert.True(LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(2f, 2.25f, SegmentType.Straight, StandingGeometry));
        Assert.False(LateralSpaceModel.IsWithinProvisionalOccupancyThreshold(2f, 2.25f, SegmentType.TurnMiddle, StandingGeometry));
    }

    [Fact]
    public void ContactThresholdUsesPhysicalMeters()
    {
        Assert.True(LateralSpaceModel.IsWithinProvisionalContactThreshold(2f, 2.25f, SegmentType.Straight, StandingGeometry));
        Assert.False(LateralSpaceModel.IsWithinProvisionalContactThreshold(2f, 2.25f, SegmentType.TurnMiddle, StandingGeometry));
    }

    [Fact]
    public void ContactThresholdDoesNotUseLegacyGlobalSpacing()
    {
        var sameGapStraight = LateralSpaceModel.LateralDistanceMeters(2f, 2.25f, SegmentType.Straight, StandingGeometry);
        var sameGapTurn = LateralSpaceModel.LateralDistanceMeters(2f, 2.25f, SegmentType.TurnMiddle, StandingGeometry);
        Assert.Equal(0.5f, sameGapStraight, 6);
        Assert.Equal(0.75f, sameGapTurn, 6);
        Assert.Equal(0.55f, LateralSpaceModel.ProvisionalContactThresholdMeters);
    }

    [Fact]
    public void StandingStartExampleUsesFIMMinimumWidthEnvelope()
        => Assert.True(StandingGeometry.IsWithinFIMSpeedwayWidthEnvelope());

    [Fact]
    public void CompatibilityExampleIsNotRegulatoryGeometry()
        => Assert.False(Track.CreateExample().Geometry.IsWithinFIMSpeedwayWidthEnvelope());

    private static Track StandingTrack => Track.CreateStandingStartExample();
    private static TrackGeometry StandingGeometry => StandingTrack.Geometry;

    private static float Usable(SegmentType type)
        => LaneModel.UsableRacingWidthMeters(type, StandingGeometry);

    private static float Spacing(SegmentType type)
        => LaneModel.ReferenceLaneSpacingMeters(type, StandingGeometry);

    private static float OffsetFromEdge(float position, SegmentType type)
        => LaneModel.PhysicalLateralOffsetFromInnerEdgeMeters(position, type, StandingGeometry);

    private static float LapDistance(Track track, float lateralPosition)
        => track.Segments.Sum(segment => LaneModel.SegmentLengthMeters(segment, lateralPosition, track.Geometry));

    private static float MaxPhysicalDistance(SegmentType type)
        => LateralMovementModel.CalculateMaxLateralDistanceMeters(
            1f,
            type,
            StandingGeometry,
            IdealSurface,
            RiderSkills.Balanced);
}
