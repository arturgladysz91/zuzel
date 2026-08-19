using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class TrackGeometryTests
{
    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }

    public static IEnumerable<object[]> InvalidGeometryCases()
    {
        var invalidValues = new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        foreach (var value in invalidValues)
        {
            yield return new object[] { value, 24f, 1f, MathF.PI / 3f };
            yield return new object[] { 60f, value, 1f, MathF.PI / 3f };
            yield return new object[] { 60f, 24f, value, MathF.PI / 3f };
            yield return new object[] { 60f, 24f, 1f, value };
        }
    }

    [Fact]
    public void DifferentTrackGeometryProducesDifferentTurnSegmentAndLaneLengths()
    {
        var segment = new TrackSegment(10, SegmentType.TurnMiddle);
        var compact = new Track(
            new[] { segment },
            new TrackGeometry(52f, 20f, 0.8f, MathF.PI / 4f));
        var wide = new Track(
            new[] { segment },
            new TrackGeometry(68f, 30f, 1.2f, MathF.PI / 3f));

        var compactInner = LaneModel.SegmentLengthMeters(segment, LaneModel.MinLane, compact.Geometry);
        var compactOuter = LaneModel.SegmentLengthMeters(segment, LaneModel.MaxLane, compact.Geometry);
        var wideInner = LaneModel.SegmentLengthMeters(segment, LaneModel.MinLane, wide.Geometry);
        var wideOuter = LaneModel.SegmentLengthMeters(segment, LaneModel.MaxLane, wide.Geometry);

        Assert.NotEqual(compactInner, wideInner);
        Assert.NotEqual(compactOuter, wideOuter);
        Assert.NotEqual(compactOuter - compactInner, wideOuter - wideInner);
    }

    [Fact]
    public void OuterTurnLaneHasLargerRadiusAndLongerPath()
    {
        var geometry = new TrackGeometry(58f, 25f, 1.15f, MathF.PI / 3f);
        var innerRadius = LaneModel.TurnArcRadiusMeters(LaneModel.MinLane, geometry);
        var outerRadius = LaneModel.TurnArcRadiusMeters(LaneModel.MaxLane, geometry);
        var innerLength = LaneModel.TurnArcLengthMeters(LaneModel.MinLane, geometry);
        var outerLength = LaneModel.TurnArcLengthMeters(LaneModel.MaxLane, geometry);

        Assert.True(outerRadius > innerRadius);
        Assert.True(outerLength > innerLength);
    }

    [Fact]
    public void ChangingStraightLengthChangesLapLength()
    {
        var example = Track.CreateExample();
        var shortStraights = new Track(
            example.Segments,
            new TrackGeometry(50f, 24f, 1f, MathF.PI / 3f));
        var longStraights = new Track(
            example.Segments,
            new TrackGeometry(70f, 24f, 1f, MathF.PI / 3f));

        var shortLap = LapLengthMeters(shortStraights, lane: 2);
        var longLap = LapLengthMeters(longStraights, lane: 2);

        Assert.Equal(40f, longLap - shortLap, 3);
    }

    [Theory]
    [MemberData(nameof(InvalidGeometryCases))]
    public void InvalidGeometryIsRejected(
        float straightLengthMeters,
        float innerRadiusMeters,
        float laneSpacingMeters,
        float turnSegmentAngleRadians)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackGeometry(
            straightLengthMeters,
            innerRadiusMeters,
            laneSpacingMeters,
            turnSegmentAngleRadians));
    }

    [Fact]
    public void SnapshotPreservesReadOnlyTrackGeometry()
    {
        var geometry = new TrackGeometry(54f, 22f, 0.9f, MathF.PI / 4f);
        var track = new Track(new[] { new TrackSegment(7, SegmentType.TurnEntry) }, geometry);
        var rider = new RiderState(1, lane: 1);
        var engine = new SimulationEngine(new HoldLaneDecisionModel());

        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track),
            new[] { rider },
            new SimulationStepContext(1, 0, 0, 0, 17, 1));

        Assert.NotSame(track, snapshot.Track);
        Assert.Equal(geometry, snapshot.Track.Geometry);
        Assert.All(
            typeof(TrackGeometry).GetProperties(),
            property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void SnapshotGeometryDoesNotDependOnRiderOrder()
    {
        var geometry = new TrackGeometry(57f, 26f, 1.1f, MathF.PI / 3f);
        var track = new Track(new[] { new TrackSegment(2, SegmentType.Straight) }, geometry);
        var firstRiders = new[] { new RiderState(1, 0), new RiderState(2, 3) };
        var reversedRiders = firstRiders.Reverse().ToArray();
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var step = new SimulationStepContext(2, 0, 0, 0, 21, 1);

        var first = engine.CaptureSnapshot(track, TrackState.CreateDefault(track), firstRiders, step);
        var reversed = engine.CaptureSnapshot(track, TrackState.CreateDefault(track), reversedRiders, step);

        Assert.Equal(first.Track.Geometry, reversed.Track.Geometry);
        Assert.Equal(
            LaneModel.SegmentLengthMeters(first.Segment, LaneModel.MinLane, first.Track.Geometry),
            LaneModel.SegmentLengthMeters(reversed.Segment, LaneModel.MinLane, reversed.Track.Geometry));
    }

    [Fact]
    public void SimulationUsesTrackSpecificStraightLength()
    {
        var shortTrack = SingleStraightTrack(40f);
        var longTrack = SingleStraightTrack(75f);

        var shortDistance = SimulateSingleStraight(shortTrack);
        var longDistance = SimulateSingleStraight(longTrack);

        Assert.Equal(40f, shortDistance, 3);
        Assert.Equal(75f, longDistance, 3);
    }

    [Fact]
    public void LegacyTrackAndLaneModelApisRetainExampleCompatibleGeometry()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var track = new Track(new[] { segment });

        Assert.Equal(LaneModel.StraightLengthMeters, track.Geometry.StraightLengthMeters);
        Assert.Equal(LaneModel.InnerRadiusMeters, track.Geometry.InnerRadiusMeters);
        Assert.Equal(LaneModel.LaneWidthMeters, track.Geometry.LaneSpacingMeters);
        Assert.Equal(LaneModel.TurnSegmentAngleRadians, track.Geometry.TurnSegmentAngleRadians);
        Assert.Equal(
            LaneModel.TurnArcLengthMeters(2),
            LaneModel.TurnArcLengthMeters(2, track.Geometry));
        Assert.Equal(
            LaneModel.SegmentLengthMeters(segment, 2),
            LaneModel.SegmentLengthMeters(segment, 2, track.Geometry));
    }

    private static Track SingleStraightTrack(float straightLengthMeters)
        => new(
            new[] { new TrackSegment(0, SegmentType.Straight) },
            new TrackGeometry(straightLengthMeters, 24f, 1f, MathF.PI / 3f));

    private static float SimulateSingleStraight(Track track)
    {
        var rider = new RiderState(1, lane: 0) { Speed = 10f };
        new HeatSimulator(new HoldLaneDecisionModel()).Simulate(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { rider });
        return rider.DistanceMeters;
    }

    private static float LapLengthMeters(Track track, int lane)
        => track.Segments.Sum(segment => LaneModel.SegmentLengthMeters(segment, lane, track.Geometry));
}
