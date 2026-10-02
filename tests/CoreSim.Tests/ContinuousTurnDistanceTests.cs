using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class ContinuousTurnDistanceTests
{
    private static readonly TrackSurfaceState IdealSurface = new(1f, 0f, 0.35f);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }

    [Theory]
    [InlineData(1f, 27f)]
    [InlineData(0.75f, 20.25f)]
    public void ContinuousTurnArcLengthUsesExactLateralPosition(float angleRadians, float expectedLength)
    {
        var geometry = new TrackGeometry(60f, 24f, 2f, angleRadians);

        var length = LaneModel.TurnArcLengthMeters(1.5f, geometry);

        Assert.Equal(expectedLength, length, 5);
    }

    [Fact]
    public void ContinuousTurnArcLengthInterpolatesBetweenReferenceLanes()
    {
        var geometry = new TrackGeometry(60f, 21f, 1.4f, 0.85f);
        var innerLength = LaneModel.TurnArcLengthMeters(1f, geometry);
        var outerLength = LaneModel.TurnArcLengthMeters(2f, geometry);

        var midpointLength = LaneModel.TurnArcLengthMeters(1.5f, geometry);

        Assert.Equal((innerLength + outerLength) * 0.5f, midpointLength, 5);
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(4.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ContinuousTurnArcLengthRejectsInvalidPosition(float lateralPosition)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LaneModel.TurnArcLengthMeters(lateralPosition, TrackGeometry.Default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void IntegerReferencePositionsPreserveDiscreteTurnLength(int lane)
    {
        var geometry = new TrackGeometry(60f, 22f, 1.25f, 0.9f);

        Assert.Equal(
            LaneModel.TurnArcLengthMeters(lane, geometry),
            LaneModel.TurnArcLengthMeters((float)lane, geometry));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.25f)]
    [InlineData(2.5f)]
    [InlineData(4f)]
    public void ContinuousSegmentLengthPreservesStraightLength(float lateralPosition)
    {
        var segment = new TrackSegment(0, SegmentType.Straight);
        var geometry = new TrackGeometry(57f, 24f, 1.2f, 0.9f);

        var length = LaneModel.SegmentLengthMeters(segment, lateralPosition, geometry);

        Assert.Equal(geometry.StraightLengthMeters, length);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void LegacyIntegerStraightSegmentLengthStillIgnoresLane(int lane)
    {
        var segment = new TrackSegment(0, SegmentType.Straight);
        var geometry = new TrackGeometry(57f, 24f, 1.2f, 0.9f);

        var length = LaneModel.SegmentLengthMeters(segment, lane, geometry);

        Assert.Equal(geometry.StraightLengthMeters, length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void IntegerReferencePositionsPreserveDiscreteSegmentLength(int lane)
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var geometry = new TrackGeometry(60f, 23f, 1.15f, 0.8f);

        Assert.Equal(
            LaneModel.SegmentLengthMeters(segment, lane, geometry),
            LaneModel.SegmentLengthMeters(segment, (float)lane, geometry));
    }

    [Fact]
    public void SimulationEngineIntegratesExecutedLateralPositionForTurnDistance()
    {
        const int lane = 2;
        const float entrySpeed = 8f;
        var geometry = TrackGeometry.Default;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);

        var inner = ResolveSingle(segment, Rider(1, lane, 1.2f, entrySpeed), geometry);
        var outer = ResolveSingle(segment, Rider(1, lane, 1.8f, entrySpeed), geometry);
        var expectedInner = LaneModel.SegmentLengthMeters(segment, 1.2f, geometry);
        var expectedOuter = LaneModel.SegmentLengthMeters(segment, 1.8f, geometry);

        Assert.Equal(SegmentOutcome.Ok, inner.Outcome);
        Assert.Equal(SegmentOutcome.Ok, outer.Outcome);
        Assert.Equal(entrySpeed, inner.EntrySpeed);
        Assert.Equal(entrySpeed, outer.EntrySpeed);
        Assert.True(inner.Speed > entrySpeed);
        Assert.True(outer.Speed > entrySpeed);
        Assert.True(inner.Position.DistanceMeters > expectedInner);
        Assert.True(outer.Position.DistanceMeters > expectedOuter);
        Assert.True(outer.Position.DistanceMeters > inner.Position.DistanceMeters);
        Assert.True(outer.ElapsedTimeSeconds > inner.ElapsedTimeSeconds);
    }

    [Fact]
    public void SameDiscreteLaneDifferentContinuousPositionsHaveDifferentTurnDistance()
    {
        const int lane = 2;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);

        var inner = ResolveSingle(segment, Rider(1, lane, 1.2f, 8f), TrackGeometry.Default);
        var outer = ResolveSingle(segment, Rider(1, lane, 1.8f, 8f), TrackGeometry.Default);

        Assert.Equal(lane, inner.PlannedLane);
        Assert.Equal(lane, outer.PlannedLane);
        Assert.NotEqual(inner.Position.DistanceMeters, outer.Position.DistanceMeters);
    }

    [Fact]
    public void RunWideDistanceUsesExecutedOutwardTrajectory()
    {
        const int plannedLane = 2;
        const float entryLateralPosition = 1.2f;
        var geometry = TrackGeometry.Default;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var maxSafeSpeed = CornerTestSupport.SingleEnvelopeSpeed(
            entryLateralPosition,
            geometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);

        var change = ResolveSingle(
            segment,
            Rider(1, plannedLane, entryLateralPosition, maxSafeSpeed * 1.20f),
            geometry);
        var expectedDistance = LaneModel.SegmentLengthMeters(segment, entryLateralPosition, geometry);
        var resolvedLaneDistance = LaneModel.SegmentLengthMeters(segment, change.Lane, geometry);

        Assert.Equal(SegmentOutcome.RunWide, change.Outcome);
        Assert.Equal(plannedLane, change.PlannedLane);
        Assert.Equal(plannedLane + 1, change.Lane);
        Assert.True(change.Position.DistanceMeters > expectedDistance);
        Assert.NotEqual(resolvedLaneDistance, change.Position.DistanceMeters);
    }

    [Fact]
    public void CrashUsesExecutedDistanceWithExistingPartialAdvance()
    {
        const int lane = 2;
        const float entryLateralPosition = 1.2f;
        var geometry = TrackGeometry.Default;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var maxSafeSpeed = CornerTestSupport.SingleEnvelopeSpeed(
            entryLateralPosition,
            geometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);

        var change = ResolveSingle(
            segment,
            Rider(1, lane, entryLateralPosition, maxSafeSpeed * 1.30f),
            geometry);
        var expectedDistance = LaneModel.SegmentLengthMeters(segment, entryLateralPosition, geometry) * 0.5f;

        Assert.Equal(SegmentOutcome.Crash, change.Outcome);
        Assert.True(change.Position.DistanceMeters > expectedDistance);
        Assert.Equal(0.5d, change.Position.TotalSegmentProgress, 5);
    }

    [Fact]
    public void StraightSimulationDistanceIncludesExecutedLateralDisplacement()
    {
        const int lane = 2;
        const float entrySpeed = 8f;
        var geometry = new TrackGeometry(57f, 24f, 1.2f, 0.9f);
        var segment = new TrackSegment(0, SegmentType.Straight);

        var inner = ResolveSingle(segment, Rider(1, lane, 1.2f, entrySpeed), geometry);
        var outer = ResolveSingle(segment, Rider(1, lane, 1.8f, entrySpeed), geometry);

        Assert.True(inner.Position.DistanceMeters > geometry.StraightLengthMeters);
        Assert.True(outer.Position.DistanceMeters > geometry.StraightLengthMeters);
        Assert.NotEqual(inner.Position.DistanceMeters, outer.Position.DistanceMeters);

    }

    [Fact]
    public void ContinuousDistanceIsIndependentOfRiderCollectionOrder()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var geometry = TrackGeometry.Default;
        var riders = new[]
        {
            Rider(1, lane: 2, lateralPosition: 1.2f, speed: 8f),
            Rider(2, lane: 2, lateralPosition: 1.8f, speed: 8f, elapsedTimeSeconds: 5f),
        };

        var forward = ResolveMany(segment, riders, geometry);
        var reversed = ResolveMany(segment, riders.Reverse().ToArray(), geometry);

        Assert.Equal(forward.Changes, reversed.Changes);
        Assert.Equal(forward.Events, reversed.Events);
    }

    [Fact]
    public void LegacyDistanceStillUsesDiscreteResolvedLane()
    {
        const int plannedLane = 2;
        var geometry = TrackGeometry.Default;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var entrySpeed = SegmentPhysics.MaxSafeTurnSpeed(plannedLane, geometry) * 1.20f;

        var change = ResolveSingle(
            segment,
            Rider(1, plannedLane, 1.2f, entrySpeed),
            geometry,
            useLegacyPhysics: true);

        Assert.Equal(SegmentOutcome.RunWide, change.Outcome);
        Assert.Equal(plannedLane + 1, change.Lane);
        Assert.Equal(
            LaneModel.SegmentLengthMeters(segment, change.Lane, geometry),
            change.Position.DistanceMeters,
            5);
    }

    private static RiderState Rider(
        int riderId,
        int lane,
        float lateralPosition,
        float speed,
        float elapsedTimeSeconds = 0f)
        => new(riderId, lane)
        {
            LateralPosition = lateralPosition,
            Speed = speed,
            ElapsedTimeSeconds = elapsedTimeSeconds,
        };

    private static RiderStateChange ResolveSingle(
        TrackSegment segment,
        RiderState rider,
        TrackGeometry geometry,
        bool useLegacyPhysics = false)
        => Assert.Single(ResolveMany(segment, new[] { rider }, geometry, useLegacyPhysics).Changes);

    private static ResolvedSimulationStep ResolveMany(
        TrackSegment segment,
        IReadOnlyList<RiderState> riders,
        TrackGeometry geometry,
        bool useLegacyPhysics = false)
    {
        var track = new Track(new[] { segment }, geometry);
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 123,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };
        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track, IdealSurface),
            riders,
            new SimulationStepContext(
                HeatId: 1,
                StepNumber: 0,
                LapIndex: 0,
                SegmentIndex: 0,
                Seed: options.Seed,
                RequiredLaps: options.Laps,
                UseLegacyPhysics: useLegacyPhysics));

        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }
}
