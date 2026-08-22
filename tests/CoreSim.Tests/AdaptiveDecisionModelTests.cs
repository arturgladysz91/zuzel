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
