using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class HeatSimulatorPhysicsTests
{
    private sealed class FixedDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane, 0f);
    }

    [Fact]
    public void Simulate_BrakesWhenSpeedSlightlyAboveLimit()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.TurnMiddle) });
        var trackState = TrackState.CreateDefault(track);
        var lane = 2;
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);
        var rider = RiderState.CreateDefault(7, lane);
        rider.Speed = max * SegmentPhysics.BrakeSpeedFactor;

        var sim = new HeatSimulator(new FixedDecisionModel());
        var log = sim.Simulate(track, trackState, new List<RiderState> { rider });

        Assert.Equal(lane, rider.Lane);
        Assert.Equal(max, rider.Speed, 3);
        Assert.Contains("outcome=Brake", log.Lines[0]);
    }

    [Fact]
    public void Simulate_RunsWideWhenTooFast()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.TurnMiddle) });
        var trackState = TrackState.CreateDefault(track);
        const int startLane = 1;
        var max = SegmentPhysics.MaxSafeTurnSpeed(startLane);
        var rider = RiderState.CreateDefault(8, startLane);
        rider.Speed = max * SegmentPhysics.RunWideSpeedFactor;

        var sim = new HeatSimulator(new FixedDecisionModel());
        var log = sim.Simulate(track, trackState, new List<RiderState> { rider });

        Assert.Equal(startLane + 1, rider.Lane);
        Assert.Equal(max * SegmentPhysics.RunWideSpeedFactor, rider.Speed, 3);
        Assert.Contains("outcome=RunWide", log.Lines[0]);
    }

    [Fact]
    public void Simulate_CrashesWhenNoRoomToRunWide()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.TurnMiddle) });
        var trackState = TrackState.CreateDefault(track);
        var lane = LaneModel.MaxLane;
        var rider = RiderState.CreateDefault(9, lane);
        var max = SegmentPhysics.MaxSafeTurnSpeed(lane);
        rider.Speed = max * SegmentPhysics.RunWideSpeedFactor;

        var sim = new HeatSimulator(new FixedDecisionModel());
        var log = sim.Simulate(track, trackState, new List<RiderState> { rider });

        Assert.Equal(lane, rider.Lane);
        Assert.Equal(0f, rider.Speed, 3);
        Assert.Contains("outcome=Crash", log.Lines[0]);
    }
}
