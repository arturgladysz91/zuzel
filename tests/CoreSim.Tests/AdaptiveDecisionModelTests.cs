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
}
