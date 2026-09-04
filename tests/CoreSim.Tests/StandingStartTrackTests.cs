using CoreSim;
using Xunit;

namespace CoreSim.Tests;

public sealed class StandingStartTrackTests
{
    [Fact]
    public void StraightLengthOverrideDefaultsToTrackGeometry()
    {
        var segment = new TrackSegment(12, SegmentType.Straight);
        Assert.Null(segment.StraightLengthMetersOverride);
        Assert.False(segment.IsStandingStartSegment);
        Assert.Equal(73f, LaneModel.SegmentLengthMeters(segment, 2, new TrackGeometry(73f, 24f, 1f, 1f)));
    }

    [Fact]
    public void StraightLengthOverrideChangesOnlyThatStraightSegment()
    {
        var track = new Track(new[] { new TrackSegment(1, SegmentType.Straight, 30f), new TrackSegment(2, SegmentType.Straight) });
        Assert.Equal(30f, Length(track, 0));
        Assert.Equal(track.Geometry.StraightLengthMeters, Length(track, 1));
    }

    [Fact]
    public void StraightLengthOverrideRejectsInvalidValues()
    {
        foreach (var value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackSegment(0, SegmentType.Straight, value));
    }

    [Fact]
    public void NonStraightSegmentRejectsStraightLengthOverride()
    {
        foreach (var type in new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit })
            Assert.Throws<ArgumentException>(() => new TrackSegment(0, type, 30f));
    }

    [Fact]
    public void TrackImmutableCopyPreservesStraightLengthOverride()
    {
        var source = Track.CreateStandingStartExample();
        var copy = new Track(source.Segments, source.Geometry);
        Assert.NotSame(source.Segments[0], copy.Segments[0]);
        Assert.Equal(30f, copy.Segments[0].StraightLengthMetersOverride);
        Assert.True(copy.Segments[0].IsStandingStartSegment);
        Assert.Equal(30f, copy.Segments[^1].StraightLengthMetersOverride);
    }

    [Fact]
    public void StandingStartFlagRequiresStraightSegment()
    {
        foreach (var type in new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit })
            Assert.Throws<ArgumentException>(() => new TrackSegment(0, type, isStandingStartSegment: true));
    }

    [Fact]
    public void TrackRejectsMultipleStandingStartSegments()
        => Assert.Throws<ArgumentException>(() => new Track(new[]
        {
            new TrackSegment(0, SegmentType.Straight, isStandingStartSegment: true),
            new TrackSegment(1, SegmentType.Straight, isStandingStartSegment: true),
        }));

    [Fact]
    public void StandingStartSegmentMustBeTopologyIndexZero()
        => Assert.Throws<ArgumentException>(() => new Track(new[]
        {
            new TrackSegment(10, SegmentType.Straight),
            new TrackSegment(0, SegmentType.Straight, isStandingStartSegment: true),
        }));

    [Fact]
    public void StandingStartTrackRequiresFinalStraight()
        => Assert.Throws<ArgumentException>(() => new Track(new[]
        {
            new TrackSegment(0, SegmentType.Straight, isStandingStartSegment: true),
            new TrackSegment(1, SegmentType.TurnEntry),
        }));

    [Fact]
    public void CreateExampleRemainsEightSegmentCompatibilityLayout()
    {
        var track = Track.CreateExample();
        Assert.Equal(new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit, SegmentType.Straight,
            SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit, SegmentType.Straight },
            track.Segments.Select(s => s.Type));
        Assert.All(track.Segments, s => { Assert.Null(s.StraightLengthMetersOverride); Assert.False(s.IsStandingStartSegment); });
    }

    [Fact]
    public void CreateStandingStartExampleHasNineSegments()
    {
        var track = Track.CreateStandingStartExample();
        Assert.Equal(new[] { SegmentType.Straight, SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit,
            SegmentType.Straight, SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit, SegmentType.Straight },
            track.Segments.Select(s => s.Type));
        Assert.Equal(Enumerable.Range(0, 9), track.Segments.Select(s => s.Id));
    }

    [Fact]
    public void CreateStandingStartExampleStartsWithMarkedThirtyMeterStraight()
    {
        var track = Track.CreateStandingStartExample();
        Assert.True(track.Segments[0].IsStandingStartSegment);
        Assert.Single(track.Segments.Where(s => s.IsStandingStartSegment));
        Assert.Equal(30f, Length(track, 0));
    }

    [Fact]
    public void CreateStandingStartExampleEndsWithThirtyMeterStraight()
    {
        var track = Track.CreateStandingStartExample();
        Assert.False(track.Segments[^1].IsStandingStartSegment);
        Assert.Equal(30f, Length(track, 8));
    }

    [Fact]
    public void StandingStartExampleBackStraightRemainsSixtyMeters()
    {
        var track = Track.CreateStandingStartExample();
        Assert.Null(track.Segments[4].StraightLengthMetersOverride);
        Assert.Equal(60f, Length(track, 4));
    }

    [Fact]
    public void StandingStartExampleLapDistanceMatchesCompatibilityExample()
    {
        var oldTrack = Track.CreateExample();
        var newTrack = Track.CreateStandingStartExample();
        Assert.Equal(oldTrack.Geometry, newTrack.Geometry);
        foreach (var lateral in new[] { 0f, 0.25f, 1f, 2.5f, 3f, 4f })
            Assert.Equal(LapDistance(oldTrack, lateral), LapDistance(newTrack, lateral), 4);
    }

    internal static float LapDistance(Track track, float lateral)
        => track.Segments.Sum(s => LaneModel.SegmentLengthMeters(s, lateral, track.Geometry));

    private static float Length(Track track, int index)
        => LaneModel.SegmentLengthMeters(track.Segments[index], 2, track.Geometry);
}
