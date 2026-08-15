using CoreSim;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class RaceEngineV2Tests
{
    private static readonly WeatherState NeutralWeather = new(WeatherCondition.Cloudy, 0f, 0f);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }

    [Fact]
    public void InputCollectionOrderDoesNotChangeResultOrLog()
    {
        var track = Track.CreateExample();
        var options = Options(seed: 812, laps: 4, incidentFrequency: 1f);
        var firstOrder = CreateDistinctRiders();
        var reversedOrder = CreateDistinctRiders();
        reversedOrder.Reverse();

        var first = new HeatSimulator(new AdaptiveDecisionModel(91)).SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            firstOrder,
            options,
            heatId: 12);
        var reversed = new HeatSimulator(new AdaptiveDecisionModel(91)).SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            reversedOrder,
            options,
            heatId: 12);

        Assert.Equal(first.Classification, reversed.Classification);
        Assert.Equal(first.Log.Lines, reversed.Log.Lines);
        Assert.Equal(first.Log.RaceEvents, reversed.Log.RaceEvents);
        Assert.Equal(first.Log.SurfaceChanges, reversed.Log.SurfaceChanges);
    }

    [Fact]
    public void SameLaneRidersCannotCommitTheSamePhysicalPosition()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var leader = PositionedRider(1, lane: 2, elapsed: 0f, speed: 15f, cornerExitSpeed: 15f);
        var trailing = PositionedRider(2, lane: 2, elapsed: 0.01f, speed: 15f, cornerExitSpeed: 15f);

        new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { trailing, leader },
            Options(seed: 20, laps: 1, incidentFrequency: 0f));

        Assert.Equal(leader.Lane, trailing.Lane);
        Assert.NotEqual(leader.SegmentProgressMeters, trailing.SegmentProgressMeters);
        Assert.True(trailing.GapToRiderAheadSeconds > 0f);
    }

    [Fact]
    public void AdvancedCornerPhysicsCapsSlightOverspeed()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var rider = RiderState.CreateDefault(1, 0);
        var max = SegmentPhysics.MaxSafeTurnSpeed(
            0,
            TrackSurfaceState.Default,
            rider.Profile.Skills,
            rider.Morale,
            rider.ActiveSetup);

        var result = SegmentPhysics.Apply(new SegmentPhysicsContext(
            segment,
            0,
            max * 1.05f,
            TrackSurfaceState.Default,
            rider.Profile.Skills,
            rider.Morale,
            rider.ActiveSetup));

        Assert.Equal(SegmentOutcome.Brake, result.Outcome);
        Assert.Equal(max, result.Speed, 3);
    }

    [Fact]
    public void TooFastTightEntryRunsWideAndLosesSpeed()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var rider = RiderState.CreateDefault(1, 0);
        var max = SegmentPhysics.MaxSafeTurnSpeed(
            0,
            TrackSurfaceState.Default,
            rider.Profile.Skills,
            rider.Morale,
            rider.ActiveSetup);
        var entrySpeed = max * 1.20f;

        var result = SegmentPhysics.Apply(new SegmentPhysicsContext(
            segment,
            0,
            entrySpeed,
            TrackSurfaceState.Default,
            rider.Profile.Skills,
            rider.Morale,
            rider.ActiveSetup));

        Assert.Equal(SegmentOutcome.RunWide, result.Outcome);
        Assert.Equal(1, result.Lane);
        Assert.True(result.Speed < entrySpeed);
    }

    [Fact]
    public void FasterCornerExitProducesSuccessfulPassThroughOpenLane()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var leader = PositionedRider(1, lane: 0, elapsed: 0f, speed: 14f, cornerExitSpeed: 14f);
        var attacker = PositionedRider(2, lane: 0, elapsed: 0.05f, speed: 18f, cornerExitSpeed: 18f);

        var result = new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { attacker, leader },
            Options(seed: 30, laps: 1, incidentFrequency: 0f));

        Assert.Equal(2, result.Classification[0].RiderId);
        Assert.Contains(result.Log.RaceEvents, raceEvent =>
            raceEvent.Type == RaceEventType.AttackSucceeded && raceEvent.RiderId == 2);
        Assert.Contains(result.Log.Overtakes, overtake =>
            overtake.RiderId == 2 && overtake.PassedRiderId == 1);
    }

    [Fact]
    public void OccupiedOnlyPassingLaneBlocksAttack()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var leader = PositionedRider(1, lane: 4, elapsed: 0f, speed: 14f, cornerExitSpeed: 14f);
        var attacker = PositionedRider(2, lane: 4, elapsed: 0.05f, speed: 18f, cornerExitSpeed: 18f);
        var blocker = PositionedRider(3, lane: 3, elapsed: 0.06f, speed: 15f, cornerExitSpeed: 15f);

        var result = new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { blocker, attacker, leader },
            Options(seed: 31, laps: 1, incidentFrequency: 0f));

        Assert.Contains(result.Log.RaceEvents, raceEvent =>
            raceEvent.Type == RaceEventType.AttackBlocked
            && raceEvent.RiderId == 2
            && raceEvent.Detail == "no free reachable lane");
        Assert.DoesNotContain(result.Log.RaceEvents, raceEvent =>
            raceEvent.Type == RaceEventType.AttackSucceeded && raceEvent.RiderId == 2);
    }

    [Fact]
    public void UnsuccessfulAttackCostsSpeedAndTime()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var leader = PositionedRider(1, lane: 0, elapsed: 0f, speed: 16f, cornerExitSpeed: 16f);
        var attacker = PositionedRider(2, lane: 0, elapsed: 0.35f, speed: 16.7f, cornerExitSpeed: 16.7f);

        var result = new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { leader, attacker },
            Options(seed: 32, laps: 1, incidentFrequency: 0f));

        Assert.Contains(result.Log.RaceEvents, raceEvent =>
            raceEvent.Type == RaceEventType.AttackFailed && raceEvent.RiderId == 2);
        Assert.True(attacker.Speed < 16.7f);
        Assert.True(attacker.ElapsedTimeSeconds > leader.ElapsedTimeSeconds);
    }

    [Fact]
    public void AggressiveStyleAttemptsMarginalAttackThatCautiousStyleDeclines()
    {
        var cautious = RunMarginalStyleScenario(riskTolerance: 0f);
        var aggressive = RunMarginalStyleScenario(riskTolerance: 1f);

        Assert.DoesNotContain(cautious.RaceEvents, raceEvent =>
            raceEvent.Type == RaceEventType.AttackStarted && raceEvent.RiderId == 2);
        Assert.Contains(aggressive.RaceEvents, raceEvent =>
            raceEvent.Type == RaceEventType.AttackStarted && raceEvent.RiderId == 2);
    }

    [Fact]
    public void LowerGearingImprovesCornerExitButIsNotUniversalEquipmentTier()
    {
        var lowGearing = RunSetupExitScenario(new BikeSetup(0.2f, 0.5f));
        var highGearing = RunSetupExitScenario(new BikeSetup(0.8f, 0.5f));

        Assert.True(lowGearing > highGearing);
    }

    [Fact]
    public void StartSkillAndFieldGripHaveCausalStartEffect()
    {
        var highSkill = RunStartScenario(startSkill: 90f, grip: 1f);
        var lowSkill = RunStartScenario(startSkill: 20f, grip: 1f);
        var lowGrip = RunStartScenario(startSkill: 90f, grip: 0.55f);

        Assert.True(highSkill.Speed > lowSkill.Speed);
        Assert.True(highSkill.ElapsedTimeSeconds < lowSkill.ElapsedTimeSeconds);
        Assert.True(highSkill.Speed > lowGrip.Speed);
    }

    [Fact]
    public void SegmentOrderAndBattleEventsExplainRaceProgress()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var leader = PositionedRider(1, lane: 0, elapsed: 0f, speed: 14f, cornerExitSpeed: 14f);
        var attacker = PositionedRider(2, lane: 0, elapsed: 0.05f, speed: 18f, cornerExitSpeed: 18f);

        var result = new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { leader, attacker },
            Options(seed: 33, laps: 1, incidentFrequency: 0f));

        var order = Assert.Single(result.Log.SegmentOrderSnapshots);
        Assert.Equal(new[] { 2, 1 }, order.Order.Select(entry => entry.RiderId));
        Assert.Contains(result.Log.Lines, line => line.StartsWith("SEGMENT_ORDER lap=1"));
        Assert.Contains(result.Log.RaceEvents, raceEvent => raceEvent.Type == RaceEventType.AttackStarted);
    }

    private static SimLog RunMarginalStyleScenario(float riskTolerance)
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var leader = PositionedRider(1, 0, 0f, 16f, 16f);
        var profile = new RiderProfile(
            2,
            "Attacker",
            RiderSkills.Balanced,
            new RiderStyle(riskTolerance, 0.5f, 0.5f, 0.5f));
        var attacker = new RiderState(profile, 0)
        {
            ElapsedTimeSeconds = 0.25f,
            Speed = 16.65f,
            CornerExitSpeed = 16.65f,
            DistanceMeters = 1f,
        };

        return new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { leader, attacker },
            Options(seed: 40, laps: 1, incidentFrequency: 0f)).Log;
    }

    private static float RunSetupExitScenario(BikeSetup setup)
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnExit) });
        var rider = PositionedRider(1, 1, 0f, 14f, 0f);
        rider.ActiveSetup = setup;
        new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { rider },
            Options(seed: 50, laps: 1, incidentFrequency: 0f));
        return rider.Speed;
    }

    private static RiderState RunStartScenario(float startSkill, float grip)
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnEntry) });
        var profile = new RiderProfile(
            1,
            "Starter",
            new RiderSkills(startSkill, 50f, 50f, 50f, 50f, 50f),
            RiderStyle.Balanced);
        var rider = new RiderState(profile, 0);
        var trackState = TrackState.CreateDefault(track, new TrackSurfaceState(grip, 0f, 0.35f));
        new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track,
            trackState,
            new List<RiderState> { rider },
            Options(seed: 60, laps: 1, incidentFrequency: 0f));
        return rider;
    }

    private static RiderState PositionedRider(
        int id,
        int lane,
        float elapsed,
        float speed,
        float cornerExitSpeed)
        => new(id, lane)
        {
            ElapsedTimeSeconds = elapsed,
            Speed = speed,
            CornerExitSpeed = cornerExitSpeed,
            DistanceMeters = 1f,
        };

    private static HeatSimulationOptions Options(int seed, int laps, float incidentFrequency)
        => new()
        {
            Seed = seed,
            Laps = laps,
            Weather = NeutralWeather,
            IncidentFrequency = incidentFrequency,
        };

    private static List<RiderState> CreateDistinctRiders()
        => new()
        {
            new(new RiderProfile(11, "A", new RiderSkills(82, 72, 66, 75, 62, 70), RiderStyle.Balanced), 0),
            new(new RiderProfile(22, "B", new RiderSkills(61, 84, 79, 58, 70, 74), new RiderStyle(0.75f, 0.7f, 0.7f, 0.5f)), 1),
            new(new RiderProfile(33, "C", new RiderSkills(75, 68, 72, 88, 80, 64), new RiderStyle(0.35f, 0.4f, 0.25f, 0.5f)), 2),
            new(new RiderProfile(44, "D", new RiderSkills(69, 76, 84, 71, 74, 82), RiderStyle.Balanced), 3),
        };
}
