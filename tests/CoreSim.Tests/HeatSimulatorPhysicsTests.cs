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

    private sealed class PerRiderDecisionModel(IReadOnlyDictionary<int, int> targetLanes) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(targetLanes[rider.RiderId], 0f);
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
        var entrySpeed = max * 1.20f;

        var rider = RiderState.CreateDefault(10, startLane);
        rider.Speed = entrySpeed;

        var sim = new HeatSimulator(new TargetDecisionModel(targetLane));
        var log = sim.Simulate(track, trackState, new List<RiderState> { rider });

        Assert.Equal(plannedLane + 1, rider.Lane);
        Assert.True(rider.Speed < entrySpeed);
        Assert.InRange(rider.Speed, max, entrySpeed);
        Assert.True(rider.Speed > 0f);
        Assert.True(float.IsFinite(rider.Speed));
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

    [Fact]
    public void Simulate_RunsWideWhenTooFast()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.TurnMiddle) });
        var trackState = TrackState.CreateDefault(track);
        const int startLane = 1;
        var max = SegmentPhysics.MaxSafeTurnSpeed(startLane);
        var entrySpeed = max * SegmentPhysics.RunWideSpeedFactor;
        var rider = RiderState.CreateDefault(8, startLane);
        rider.Speed = entrySpeed;

        var sim = new HeatSimulator(new FixedDecisionModel());
        var log = sim.Simulate(track, trackState, new List<RiderState> { rider });

        Assert.Equal(startLane + 1, rider.Lane);
        Assert.True(rider.Speed < entrySpeed);
        Assert.InRange(rider.Speed, max, entrySpeed);
        Assert.Contains("outcome=RunWide", log.Lines[0]);
    }

    [Fact]
    public void SimulationEngine_DistinguishesPlannedWideLineFromForcedRunWide()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnMiddle) });
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var plannedWideRider = RiderState.CreateDefault(20, lane: 1);
        var forcedRunWideRider = RiderState.CreateDefault(30, lane: 1);
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            plannedWideRider.LateralPosition,
            track.Geometry,
            surface,
            plannedWideRider.Profile.Skills,
            plannedWideRider.ActiveSetup);
        var plannedWideEntrySpeed = maxSafeSpeed * 0.95f;
        var forcedRunWideEntrySpeed = maxSafeSpeed * 1.20f;
        plannedWideRider.Speed = plannedWideEntrySpeed;
        forcedRunWideRider.Speed = forcedRunWideEntrySpeed;

        var engine = new SimulationEngine(new PerRiderDecisionModel(new Dictionary<int, int>
        {
            [plannedWideRider.RiderId] = LaneModel.MaxLane,
            [forcedRunWideRider.RiderId] = LaneModel.MaxLane,
        }));
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 123,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };

        RiderStateChange[] Resolve(IReadOnlyList<RiderState> riders)
        {
            var snapshot = engine.CaptureSnapshot(
                track,
                TrackState.CreateDefault(track, surface),
                riders,
                new SimulationStepContext(1, 0, 0, 0, options.Seed, options.Laps));
            return engine.Resolve(snapshot, engine.Decide(snapshot), options).Changes.ToArray();
        }

        var forward = Resolve(new[] { plannedWideRider, forcedRunWideRider });
        var reversed = Resolve(new[] { forcedRunWideRider, plannedWideRider });

        Assert.Equal(forward, reversed);

        var plannedWide = Assert.Single(forward, change => change.RiderId == plannedWideRider.RiderId);
        Assert.Equal(1, plannedWide.BeforeLane);
        Assert.Equal(2, plannedWide.PlannedLane);
        Assert.Equal(LaneModel.MaxLane, plannedWide.TargetLane);
        Assert.Equal(plannedWide.PlannedLane, plannedWide.Lane);
        Assert.Equal(SegmentOutcome.Ok, plannedWide.Outcome);
        Assert.Equal(plannedWideEntrySpeed, plannedWide.EntrySpeed);
        Assert.Equal(plannedWideEntrySpeed, plannedWide.PhysicsSpeed);

        var forcedRunWide = Assert.Single(forward, change => change.RiderId == forcedRunWideRider.RiderId);
        Assert.Equal(1, forcedRunWide.BeforeLane);
        Assert.Equal(2, forcedRunWide.PlannedLane);
        Assert.Equal(LaneModel.MaxLane, forcedRunWide.TargetLane);
        Assert.Equal(forcedRunWide.PlannedLane + 1, forcedRunWide.Lane);
        Assert.Equal(SegmentOutcome.RunWide, forcedRunWide.Outcome);
        Assert.Equal(forcedRunWideEntrySpeed, forcedRunWide.EntrySpeed);
        Assert.True(forcedRunWide.PhysicsSpeed < forcedRunWide.EntrySpeed);
        Assert.True(forcedRunWide.PhysicsSpeed >= maxSafeSpeed);
        Assert.True(forcedRunWide.PhysicsSpeed > 0f);
        Assert.True(float.IsFinite(forcedRunWide.PhysicsSpeed));
        Assert.Equal(forcedRunWide.PhysicsSpeed, forcedRunWide.Speed);
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
