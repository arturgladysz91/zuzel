using CoreSim;
using CoreSim.Decisions;
using CoreSim.Logging;
using Xunit;

namespace CoreSim.Tests;

public sealed class AdaptiveDecisionModelTests
{
    [Fact]
    public void SkilledReaderChoosesCleanerAdjacentLane()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnMiddle) });
        var state = TrackState.CreateDefault(track, new TrackSurfaceState(0.75f, 0.5f, 0.8f));
        var surfaceLog = new SimLog();
        state.ApplySurfaceDelta(0, 3, 0.25f, -0.5f, -0.45f, "prepared", 0, 0, surfaceLog);

        var profile = new RiderProfile(
            7,
            "Reader",
            new RiderSkills(50f, 50f, 60f, 100f, 50f, 70f),
            RiderStyle.Balanced);
        var rider = new RiderState(profile, lane: 2);
        var model = new AdaptiveDecisionModel(seed: 42);
        var context = new RiderDecisionContext(track.Segments[0], 0, state, rider, new[] { rider }, 1, 0);

        var decision = model.Decide(context);

        Assert.Equal(3, decision.TargetLane);
    }

    [Fact]
    public void UniformTrackDoesNotFreezeBalancedRiderOnOutsideGate()
    {
        var track = Track.CreateExample();
        var state = TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, 0.35f));
        var rider = new RiderState(
            new RiderProfile(
                8,
                "Balanced",
                new RiderSkills(60f, 60f, 60f, 100f, 60f, 60f),
                RiderStyle.Balanced),
            lane: 3);
        var model = new AdaptiveDecisionModel(seed: 42);
        var context = new RiderDecisionContext(
            track.Segments[0], 0, state, rider, new[] { rider }, 1, 0, track);

        var decision = model.Decide(context);

        Assert.Equal(2, decision.TargetLane);
    }

    [Fact]
    public void ConcreteTrackGeometryChangesRouteChoiceDeterministically()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var tightRadiusTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 10f, 2f, 0.8f));
        var referenceRadiusTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 24f, 1.5f, 0.8f));
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var rider = new RiderState(
            new RiderProfile(
                12,
                "Geometry reader",
                new RiderSkills(60f, 60f, 60f, 100f, 60f, 60f),
                RiderStyle.Balanced),
            lane: 2);
        var rival = new RiderState(21, lane: 1) { ElapsedTimeSeconds = 1f };
        var step = new SimulationStepContext(
            HeatId: 4,
            StepNumber: 7,
            LapIndex: 0,
            SegmentIndex: 0,
            Seed: 31415,
            RequiredLaps: 1);
        var engine = new SimulationEngine(new AdaptiveDecisionModel(seed: 42));

        var tightRadiusDecision = DecideFor(
            engine,
            tightRadiusTrack,
            surface,
            new[] { rider, rival },
            rider.RiderId,
            step);
        var referenceRadiusDecision = DecideFor(
            engine,
            referenceRadiusTrack,
            surface,
            new[] { rival, rider },
            rider.RiderId,
            step);

        Assert.Equal(LaneModel.MaxLane, tightRadiusDecision.TargetLane);
        Assert.Equal(2, referenceRadiusDecision.TargetLane);
        Assert.NotEqual(tightRadiusDecision.Reason, referenceRadiusDecision.Reason);
    }

    [Fact]
    public void MovementCostUsesContinuousLateralPosition()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnMiddle) });
        var state = TrackState.CreateDefault(track, new TrackSurfaceState(0.75f, 0.5f, 0.8f));
        state.ApplySurfaceDelta(0, 3, 0.25f, -0.5f, -0.45f, "prepared", 0, 0, new SimLog());
        var profile = new RiderProfile(
            31,
            "Reader",
            new RiderSkills(50f, 50f, 60f, 100f, 50f, 70f),
            RiderStyle.Balanced);
        var centered = new RiderState(profile, lane: 2) { LateralPosition = 2f };
        var alreadyMovingOutward = new RiderState(profile, lane: 2) { LateralPosition = 2.9f };
        var model = new AdaptiveDecisionModel(seed: 42);

        var centeredDecision = model.Decide(new RiderDecisionContext(
            track.Segments[0], 0, state, centered, new[] { centered }, 1, 0));
        var movingDecision = model.Decide(new RiderDecisionContext(
            track.Segments[0], 0, state, alreadyMovingOutward, new[] { alreadyMovingOutward }, 1, 0));

        Assert.Equal(3, centeredDecision.TargetLane);
        Assert.Equal(3, movingDecision.TargetLane);
        Assert.NotEqual(centeredDecision.Reason, movingDecision.Reason);
    }

    [Fact]
    public void AdaptiveDecisionModelUsesConcreteGeometryForOccupancy()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var oneMeterSpacingTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 24f, 1f, MathF.PI / 3f));
        var twoMeterSpacingTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 24f, 2f, MathF.PI / 3f));
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var profile = new RiderProfile(
            41,
            "Occupancy reader",
            new RiderSkills(50f, 50f, 50f, 100f, 50f, 50f),
            RiderStyle.Balanced);
        var rider = new RiderState(profile, lane: 2);
        var rival = new RiderState(42, lane: 2) { LateralPosition = 2.4f };
        var step = new SimulationStepContext(
            HeatId: 5,
            StepNumber: 8,
            LapIndex: 0,
            SegmentIndex: 0,
            Seed: 27182,
            RequiredLaps: 1);
        var engine = new SimulationEngine(new AdaptiveDecisionModel(seed: 42));

        var oneMeterBaseline = DecideFor(engine, oneMeterSpacingTrack, surface, new[] { rider }, rider.RiderId, step);
        var twoMeterBaseline = DecideFor(engine, twoMeterSpacingTrack, surface, new[] { rider }, rider.RiderId, step);
        var occupiedDecision = DecideFor(
            engine,
            oneMeterSpacingTrack,
            surface,
            new[] { rider, rival },
            rider.RiderId,
            step);
        var clearDecision = DecideFor(
            engine,
            twoMeterSpacingTrack,
            surface,
            new[] { rival, rider },
            rider.RiderId,
            step);

        Assert.Equal(2, oneMeterBaseline.TargetLane);
        Assert.Equal(oneMeterBaseline.TargetLane, twoMeterBaseline.TargetLane);
        Assert.Equal(1, occupiedDecision.TargetLane);
        Assert.Equal(2, clearDecision.TargetLane);
    }

    [Theory]
    [InlineData(SegmentType.Straight)]
    [InlineData(SegmentType.TurnEntry)]
    [InlineData(SegmentType.TurnMiddle)]
    [InlineData(SegmentType.TurnExit)]
    public void OccupancyIntegrationUsesCurrentSegmentWidth(SegmentType type)
    {
        var track = new Track(new[] { new TrackSegment(0, type) },
            new TrackGeometry(60f, 24f, 10f, 14f, MathF.PI / 3f));
        var profile = new RiderProfile(41, "Local width reader",
            new RiderSkills(50f, 50f, 50f, 100f, 50f, 50f), RiderStyle.Balanced);
        var rider = new RiderState(profile, lane: 2);
        var engine = new SimulationEngine(new AdaptiveDecisionModel(seed: 42));
        var step = new SimulationStepContext(5, 8, 0, 0, 27182, 1);
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var baseline = DecideFor(engine, track, surface, new[] { rider }, 41, step);
        // Put the rival beside the strategy's own preferred reference so this
        // test isolates occupancy rather than imposing an inner/outer preference.
        var rival = new RiderState(42, baseline.TargetLane)
        {
            LateralPosition = baseline.TargetLane == 4
                ? baseline.TargetLane - 0.25f : baseline.TargetLane + 0.25f,
        };
        var withRival = DecideFor(engine, track, surface, new[] { rider, rival }, 41, step);

        if (type == SegmentType.Straight)
            Assert.NotEqual(baseline.TargetLane, withRival.TargetLane);
        else
            Assert.Equal(baseline.TargetLane, withRival.TargetLane);
    }

    private static RiderDecision DecideFor(
        SimulationEngine engine,
        Track track,
        TrackSurfaceState surface,
        IReadOnlyList<RiderState> riders,
        int riderId,
        SimulationStepContext step)
    {
        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track, surface),
            riders,
            step);
        return engine.Decide(snapshot).Single(intent => intent.RiderId == riderId).Decision;
    }
}
