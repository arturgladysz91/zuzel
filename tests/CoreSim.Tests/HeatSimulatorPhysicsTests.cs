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
    private sealed class TargetDecisionModel : IRiderDecisionModel
    {
        private readonly int _targetLane;
        private readonly float _risk;

        public TargetDecisionModel(int targetLane, float risk = 0f)
        {
            _targetLane = targetLane;
            _risk = risk;
        }

        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(_targetLane, _risk);
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
    public void Simulate_RunWideMovesOutFromPlannedLane()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.TurnMiddle) });
        var trackState = TrackState.CreateDefault(track);
        const int startLane = 1;
        const int targetLane = 4;
        const int plannedLane = startLane + 1;
        var max = SegmentPhysics.MaxSafeTurnSpeed(plannedLane);

        var rider = RiderState.CreateDefault(10, startLane);
        rider.Speed = max * 1.20f;

        var sim = new HeatSimulator(new TargetDecisionModel(targetLane));
        var log = sim.Simulate(track, trackState, new List<RiderState> { rider });

        Assert.Equal(plannedLane + 1, rider.Lane);
        Assert.Equal(max * 1.20f, rider.Speed, 3);
        Assert.Contains("outcome=RunWide", log.Lines[0]);
        Assert.Contains($"lane {startLane}->{plannedLane}->{plannedLane + 1}", log.Lines[0]);
    }

    [Fact]
    public void Simulate_BrakeKeepsPlannedLane()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.TurnMiddle) });
        var trackState = TrackState.CreateDefault(track);
        const int startLane = 0;
        const int targetLane = 3;
        const int plannedLane = startLane + 1;
        var max = SegmentPhysics.MaxSafeTurnSpeed(plannedLane);

        var rider = RiderState.CreateDefault(11, startLane);
        rider.Speed = max * 1.05f;

        var sim = new HeatSimulator(new TargetDecisionModel(targetLane));
        var log = sim.Simulate(track, trackState, new List<RiderState> { rider });

        Assert.Equal(plannedLane, rider.Lane);
        Assert.Equal(max, rider.Speed, 3);
        Assert.Contains("outcome=Brake", log.Lines[0]);
        Assert.Contains($"lane {startLane}->{plannedLane}->{plannedLane}", log.Lines[0]);
    }
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
