using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class CornerPhysicsRadiusTests
{
    private static readonly TrackSurfaceState IdealSurface = new(1f, 0f, 0.35f);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }

    [Fact]
    public void LargerRadiusRaisesSafeSpeedForTheSameRiderAndLane()
    {
        const int lane = 2;
        var tightGeometry = new TrackGeometry(60f, 18f, 1f, MathF.PI / 3f);
        var broadGeometry = new TrackGeometry(60f, 34f, 1f, MathF.PI / 3f);

        var tightSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            tightGeometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        var broadSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            broadGeometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);

        Assert.True(broadSpeed > tightSpeed);
    }

    [Fact]
    public void RadiusSpeedRatioUsesSquareRootScaling()
    {
        const int lane = LaneModel.MinLane;
        var referenceGeometry = new TrackGeometry(60f, 24f, 1f, MathF.PI / 3f);
        var largerGeometry = new TrackGeometry(60f, 54f, 1f, MathF.PI / 3f);

        var referenceSpeed = SegmentPhysics.MaxSafeTurnSpeed(lane, referenceGeometry);
        var largerSpeed = SegmentPhysics.MaxSafeTurnSpeed(lane, largerGeometry);
        var expectedRatio = MathF.Sqrt(
            LaneModel.TurnArcRadiusMeters(lane, largerGeometry)
            / LaneModel.TurnArcRadiusMeters(lane, referenceGeometry));

        Assert.Equal(
            SegmentPhysics.ReferenceTurnRadiusMeters,
            LaneModel.TurnArcRadiusMeters(lane, referenceGeometry));
        Assert.Equal(SegmentPhysics.ReferenceTurnSpeedMetersPerSecond, referenceSpeed, 3);
        Assert.Equal(expectedRatio, largerSpeed / referenceSpeed, 5);
    }

    [Fact]
    public void TurnSegmentAngleDoesNotChangeSafeSpeed()
    {
        const int lane = 3;
        var shortSegmentGeometry = new TrackGeometry(60f, 26f, 1.1f, 0.45f);
        var longSegmentGeometry = new TrackGeometry(60f, 26f, 1.1f, 1.35f);

        var shortSegmentSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            shortSegmentGeometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        var longSegmentSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            longSegmentGeometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);

        Assert.Equal(shortSegmentSpeed, longSegmentSpeed);
    }

    [Fact]
    public void MoraleDoesNotChangePhysicalSafeSpeed()
    {
        const int lane = 2;
        var lowMoraleSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            IdealSurface,
            RiderSkills.Balanced,
            morale: 0f,
            setup: BikeSetup.Neutral);
        var highMoraleSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            IdealSurface,
            RiderSkills.Balanced,
            morale: 1f,
            setup: BikeSetup.Neutral);
        var canonicalSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            TrackGeometry.Default,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);

        Assert.Equal(lowMoraleSpeed, highMoraleSpeed);
        Assert.Equal(canonicalSpeed, lowMoraleSpeed);
    }

    [Fact]
    public void MoraleStillChangesIncidentRiskWithoutChangingResolvedSpeed()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var lowMorale = SegmentPhysics.Apply(new SegmentPhysicsContext(
            Segment: segment,
            Lane: LaneModel.MinLane,
            Speed: SegmentPhysics.ReferenceTurnSpeedMetersPerSecond,
            Geometry: TrackGeometry.Default,
            Surface: IdealSurface,
            Skills: RiderSkills.Balanced,
            Morale: 0f,
            Setup: BikeSetup.Neutral));
        var highMorale = SegmentPhysics.Apply(new SegmentPhysicsContext(
            Segment: segment,
            Lane: LaneModel.MinLane,
            Speed: SegmentPhysics.ReferenceTurnSpeedMetersPerSecond,
            Geometry: TrackGeometry.Default,
            Surface: IdealSurface,
            Skills: RiderSkills.Balanced,
            Morale: 1f,
            Setup: BikeSetup.Neutral));

        Assert.Equal(lowMorale.Outcome, highMorale.Outcome);
        Assert.Equal(lowMorale.Speed, highMorale.Speed);
        Assert.True(lowMorale.IncidentRisk > highMorale.IncidentRisk);
    }

    [Fact]
    public void SameEntrySpeedRunsWideOnTightTurnAndIsOkOnBroadTurn()
    {
        const int lane = LaneModel.MinLane;
        const float entrySpeedMetersPerSecond = 18f;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var tightGeometry = new TrackGeometry(60f, 24f, 1f, MathF.PI / 3f);
        var broadGeometry = new TrackGeometry(60f, 54f, 1f, MathF.PI / 3f);

        var tightResult = SegmentPhysics.Apply(segment, lane, entrySpeedMetersPerSecond, tightGeometry);
        var broadResult = SegmentPhysics.Apply(segment, lane, entrySpeedMetersPerSecond, broadGeometry);

        Assert.Equal(SegmentOutcome.RunWide, tightResult.Outcome);
        Assert.Equal(SegmentOutcome.Ok, broadResult.Outcome);
    }

    [Fact]
    public void SimulationEngineUsesConcreteGeometryIndependentlyOfRiderOrder()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var tightTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 24f, 1f, MathF.PI / 3f));
        var broadTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 54f, 1f, MathF.PI / 3f));

        var tightForward = ResolveSingleStep(tightTrack, reverseRiders: false);
        var tightReversed = ResolveSingleStep(tightTrack, reverseRiders: true);
        var broadForward = ResolveSingleStep(broadTrack, reverseRiders: false);
        var broadReversed = ResolveSingleStep(broadTrack, reverseRiders: true);

        Assert.Equal(tightForward, tightReversed);
        Assert.Equal(broadForward, broadReversed);
        Assert.Equal(
            SegmentOutcome.RunWide,
            tightForward.Single(change => change.RiderId == 1).Outcome);
        Assert.Equal(
            SegmentOutcome.Ok,
            broadForward.Single(change => change.RiderId == 1).Outcome);

        var tightRunWide = tightForward.Single(change => change.RiderId == 1);
        var tightMaxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            tightRunWide.PlannedLane,
            tightTrack.Geometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        Assert.Equal(tightRunWide.EntrySpeed, tightRunWide.PhysicsSpeed);
        Assert.True(tightRunWide.Speed < tightRunWide.PhysicsSpeed);
        Assert.True(tightRunWide.Speed >= tightMaxSafeSpeed);
    }

    [Fact]
    public void AdvancedEntrySpeedUsesConcreteGeometry()
    {
        const int lane = LaneModel.MinLane;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var tightTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 24f, 1f, MathF.PI / 3f));
        var broadTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 54f, 1f, MathF.PI / 3f));

        var tightChange = ResolveFromRest(tightTrack, lane);
        var broadChange = ResolveFromRest(broadTrack, lane);
        var expectedRatio = SegmentPhysics.MaxSafeTurnSpeed(
                lane,
                broadTrack.Geometry,
                IdealSurface,
                RiderSkills.Balanced,
                BikeSetup.Neutral)
            / SegmentPhysics.MaxSafeTurnSpeed(
                lane,
                tightTrack.Geometry,
                IdealSurface,
                RiderSkills.Balanced,
                BikeSetup.Neutral);

        Assert.True(broadChange.EntrySpeed > tightChange.EntrySpeed);
        Assert.Equal(expectedRatio, broadChange.EntrySpeed / tightChange.EntrySpeed, 5);
    }

    [Fact]
    public void StraightEntrySpeedFromRestDoesNotDependOnTurnRadius()
    {
        const int lane = 2;
        var segment = new TrackSegment(0, SegmentType.Straight);
        var tightTurnGeometryTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 18f, 1f, MathF.PI / 3f));
        var broadTurnGeometryTrack = new Track(
            new[] { segment },
            new TrackGeometry(60f, 54f, 1f, MathF.PI / 3f));

        var tightTurnGeometryChange = ResolveFromRest(tightTurnGeometryTrack, lane);
        var broadTurnGeometryChange = ResolveFromRest(broadTurnGeometryTrack, lane);

        Assert.Equal(tightTurnGeometryChange.EntrySpeed, broadTurnGeometryChange.EntrySpeed);
        Assert.Equal(tightTurnGeometryChange.PhysicsSpeed, broadTurnGeometryChange.PhysicsSpeed);
        Assert.Equal(tightTurnGeometryChange.Speed, broadTurnGeometryChange.Speed);
        Assert.Equal(tightTurnGeometryChange.Outcome, broadTurnGeometryChange.Outcome);
    }

    [Fact]
    public void LegacyOverloadsUseDefaultGeometry()
    {
        const int lane = 2;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var expectedSpeed = SegmentPhysics.MaxSafeTurnSpeed(lane, TrackGeometry.Default);
        var entrySpeed = expectedSpeed * 1.2f;

        Assert.Equal(expectedSpeed, SegmentPhysics.MaxSafeTurnSpeed(lane));
        Assert.Equal(
            SegmentPhysics.Apply(segment, lane, entrySpeed, TrackGeometry.Default),
            SegmentPhysics.Apply(segment, lane, entrySpeed));
    }

    private static RiderStateChange[] ResolveSingleStep(Track track, bool reverseRiders)
    {
        var riders = new List<RiderState>
        {
            new(1, lane: 0) { Speed = 18f },
            new(2, lane: 4) { Speed = 10f, ElapsedTimeSeconds = 1f },
        };
        if (reverseRiders)
            riders.Reverse();

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
            new SimulationStepContext(3, 0, 0, 0, options.Seed, options.Laps));

        return engine.Resolve(snapshot, engine.Decide(snapshot), options)
            .Changes
            .OrderBy(change => change.RiderId)
            .ToArray();
    }

    private static RiderStateChange ResolveFromRest(Track track, int lane)
    {
        var rider = new RiderState(1, lane);
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
            new[] { rider },
            new SimulationStepContext(3, 0, 0, 0, options.Seed, options.Laps));

        return Assert.Single(engine.Resolve(snapshot, engine.Decide(snapshot), options).Changes);
    }
}
