using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class ContinuousSurfaceSamplingTests
{
    private static readonly TrackSurfaceState NeutralSurface = new(0.80f, 0.20f, 0.35f);

    private sealed class FixedTargetDecisionModel(int targetLane, float risk = 0f) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(targetLane, risk);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SampleSurfaceReturnsExactReferenceCell(int lane)
    {
        var snapshot = Snapshot(
            new TrackSurfaceState(0.95f, 0.05f, 0.15f),
            new TrackSurfaceState(0.85f, 0.15f, 0.25f),
            new TrackSurfaceState(0.75f, 0.25f, 0.35f),
            new TrackSurfaceState(0.65f, 0.35f, 0.45f),
            new TrackSurfaceState(0.55f, 0.45f, 0.55f));
        var stored = snapshot.GetSurface(0, lane);

        var sampled = snapshot.SampleSurface(0, lane);

        Assert.Equal(stored, sampled);
        Assert.Equal(stored.Grip, sampled.Grip);
        Assert.Equal(stored.Ruts, sampled.Ruts);
        Assert.Equal(stored.Moisture, sampled.Moisture);
        Assert.Equal(stored.EffectiveGrip, sampled.EffectiveGrip);
    }

    [Fact]
    public void SampleSurfaceInterpolatesRawValues()
    {
        var snapshot = Snapshot(
            NeutralSurface,
            new TrackSurfaceState(0.80f, 0.20f, 0.30f),
            new TrackSurfaceState(0.40f, 0.60f, 0.70f),
            NeutralSurface,
            NeutralSurface);

        var sampled = snapshot.SampleSurface(0, 1.25f);

        Assert.Equal(0.70f, sampled.Grip, 5);
        Assert.Equal(0.30f, sampled.Ruts, 5);
        Assert.Equal(0.40f, sampled.Moisture, 5);
    }

    [Fact]
    public void SampleSurfaceMidpointIsRawMidpoint()
    {
        var inner = new TrackSurfaceState(0.90f, 0.10f, 0.20f);
        var outer = new TrackSurfaceState(0.50f, 0.70f, 0.80f);
        var snapshot = Snapshot(NeutralSurface, inner, outer, NeutralSurface, NeutralSurface);

        var sampled = snapshot.SampleSurface(0, 1.5f);

        Assert.Equal((inner.Grip + outer.Grip) * 0.5f, sampled.Grip, 5);
        Assert.Equal((inner.Ruts + outer.Ruts) * 0.5f, sampled.Ruts, 5);
        Assert.Equal((inner.Moisture + outer.Moisture) * 0.5f, sampled.Moisture, 5);
    }

    [Fact]
    public void SampleSurfaceRecomputesEffectiveGripAfterRawInterpolation()
    {
        var inner = new TrackSurfaceState(1f, 0f, 0f);
        var outer = new TrackSurfaceState(1f, 0f, 0.70f);
        var snapshot = Snapshot(NeutralSurface, inner, outer, NeutralSurface, NeutralSurface);
        var expectedRawMidpoint = new TrackSurfaceState(1f, 0f, 0.35f);
        var interpolatedEffectiveGrip = (inner.EffectiveGrip + outer.EffectiveGrip) * 0.5f;

        var sampled = snapshot.SampleSurface(0, 1.5f);

        Assert.Equal(expectedRawMidpoint.EffectiveGrip, sampled.EffectiveGrip, 5);
        Assert.NotEqual(interpolatedEffectiveGrip, sampled.EffectiveGrip);
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(2.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void SampleSurfaceRejectsInvalidPosition(float lateralPosition)
    {
        var snapshot = Snapshot(NeutralSurface, NeutralSurface, NeutralSurface);

        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.SampleSurface(0, lateralPosition));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void SampleSurfacePreservesSegmentIndexValidation(int segmentIndex)
    {
        var snapshot = Snapshot(NeutralSurface, NeutralSurface, NeutralSurface);

        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.SampleSurface(segmentIndex, 1f));
    }

    [Fact]
    public void SampleSurfaceDoesNotMutateStoredCells()
    {
        var inner = new TrackSurfaceState(0.80f, 0.20f, 0.30f);
        var outer = new TrackSurfaceState(0.40f, 0.60f, 0.70f);
        var snapshot = Snapshot(NeutralSurface, inner, outer, NeutralSurface, NeutralSurface);
        var innerBefore = snapshot.GetSurface(0, 1);
        var outerBefore = snapshot.GetSurface(0, 2);

        _ = snapshot.SampleSurface(0, 1.25f);

        Assert.Equal(innerBefore, snapshot.GetSurface(0, 1));
        Assert.Equal(outerBefore, snapshot.GetSurface(0, 2));
    }

    [Fact]
    public void AdvancedPhysicsUsesEntrySurfaceInsteadOfPlannedLaneSurface()
    {
        const float entryPosition = 1.5f;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var geometry = TrackGeometry.Default;
        var state = SurfaceState(
            new TrackSurfaceState(1f, 0f, 0.35f),
            new TrackSurfaceState(0.40f, 0.60f, 0.80f));

        var inward = ResolveSingle(
            segment,
            geometry,
            state,
            Rider(1, lane: 2, entryPosition, speed: 8f),
            targetLane: 1);
        var outward = ResolveSingle(
            segment,
            geometry,
            state,
            Rider(1, lane: 2, entryPosition, speed: 8f),
            targetLane: 2);

        Assert.Equal(1, inward.PlannedLane);
        Assert.Equal(2, outward.PlannedLane);
        Assert.Equal(SegmentOutcome.Ok, inward.Outcome);
        Assert.Equal(inward.Outcome, outward.Outcome);
        Assert.Equal(inward.PhysicsSpeed, outward.PhysicsSpeed);
        Assert.Equal(inward.Risk, outward.Risk);
    }

    [Fact]
    public void AdvancedSurfaceSamplingAffectsSafeSpeedContinuously()
    {
        const float entryPosition = 1.5f;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var geometry = TrackGeometry.Default;
        var goodSurface = new TrackSurfaceState(1f, 0f, 0.35f);
        var badSurface = new TrackSurfaceState(0.35f, 0.70f, 0.90f);
        var goodState = SurfaceState(goodSurface, goodSurface);
        var badState = SurfaceState(badSurface, badSurface);
        var sample = badState.Snapshot().SampleSurface(0, entryPosition);
        var maxOnBadSurface = SegmentPhysics.MaxSafeTurnSpeed(
            entryPosition,
            geometry,
            sample,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        var entrySpeed = maxOnBadSurface * 1.05f;

        var good = ResolveSingle(
            segment,
            geometry,
            goodState,
            Rider(1, lane: 2, entryPosition, entrySpeed),
            targetLane: 2);
        var bad = ResolveSingle(
            segment,
            geometry,
            badState,
            Rider(1, lane: 2, entryPosition, entrySpeed),
            targetLane: 2);

        Assert.Equal(SegmentOutcome.Ok, good.Outcome);
        Assert.Equal(SegmentOutcome.Brake, bad.Outcome);
        Assert.Equal(entrySpeed, good.PhysicsSpeed);
        Assert.Equal(entrySpeed, bad.PhysicsSpeed);
        Assert.True(good.Speed > bad.Speed);
    }

    [Fact]
    public void AdvancedLateralMovementUsesSampledEntrySurface()
    {
        const float entryPosition = 1.5f;
        const float entrySpeed = 8f;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var geometry = new TrackGeometry(60f, 24f, 1f, 0.20f);
        var state = SurfaceState(
            new TrackSurfaceState(1f, 0f, 0.35f),
            new TrackSurfaceState(0.40f, 0.60f, 0.80f));
        var rider = Rider(1, lane: 2, entryPosition, entrySpeed);
        var sampledSurface = state.Snapshot().SampleSurface(0, entryPosition);

        var change = ResolveSingle(segment, geometry, state, rider, targetLane: 2);
        var travelled = LaneModel.SegmentLengthMeters(segment, entryPosition, geometry);
        var segmentTravelTime = travelled / entrySpeed;
        var expected = LateralMovementModel.MoveTowards(
            entryPosition,
            resolvedLane: 2,
            segmentTravelTime,
            geometry,
            sampledSurface,
            rider.Profile.Skills);

        Assert.Equal(SegmentOutcome.Ok, change.Outcome);
        Assert.Equal(2, change.Lane);
        Assert.Equal(expected, change.LateralPosition, 5);
    }

    [Fact]
    public void StraightAdvancedMovementUsesContinuousSurfaceSample()
    {
        const float entryPosition = 1.5f;
        const float entrySpeed = 8f;
        var segment = new TrackSegment(0, SegmentType.Straight);
        var geometry = new TrackGeometry(4f, 24f, 1f, 0.20f);
        var state = SurfaceState(
            new TrackSurfaceState(1f, 0f, 0.35f),
            new TrackSurfaceState(0.40f, 0.60f, 0.80f));
        var rider = Rider(1, lane: 2, entryPosition, entrySpeed);
        var sampledSurface = state.Snapshot().SampleSurface(0, entryPosition);

        var change = ResolveSingle(segment, geometry, state, rider, targetLane: 2);
        var deceleration = LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(
            rider.Profile.Skills,
            sampledSurface);
        var profile = LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            entrySpeed,
            rider.Profile.Skills,
            rider.ActiveSetup,
            sampledSurface,
            deceleration,
            geometry.StraightLengthMeters);
        var expected = LateralMovementModel.MoveTowards(
            entryPosition,
            resolvedLane: 2,
            profile.TravelTimeSeconds,
            geometry,
            sampledSurface,
            rider.Profile.Skills);

        Assert.Equal(geometry.StraightLengthMeters, change.Position.DistanceMeters);
        Assert.Equal(expected, change.LateralPosition, 5);
    }

    [Fact]
    public void LegacyPhysicsStillUsesDiscreteSurfaceContract()
    {
        const float decisionRisk = 0.10f;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var geometry = TrackGeometry.Default;
        var inner = new TrackSurfaceState(1f, 0f, 0.35f);
        var planned = new TrackSurfaceState(0.40f, 0.60f, 0.80f);
        var state = SurfaceState(inner, planned);

        var change = ResolveSingle(
            segment,
            geometry,
            state,
            Rider(1, lane: 2, lateralPosition: 1.5f, speed: 8f),
            targetLane: 2,
            decisionRisk,
            useLegacyPhysics: true);
        var expectedRisk = ExpectedLegacySurfaceRisk(planned, decisionRisk);
        var sampledRisk = ExpectedLegacySurfaceRisk(state.Snapshot().SampleSurface(0, 1.5f), decisionRisk);

        Assert.Equal(2, change.PlannedLane);
        Assert.Equal(2, change.Lane);
        Assert.Equal(2f, change.LateralPosition);
        Assert.Equal(expectedRisk, change.Risk, 5);
        Assert.NotEqual(sampledRisk, change.Risk);
    }

    [Fact]
    public void ContinuousSurfaceSamplingIsIndependentOfRiderCollectionOrder()
    {
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var geometry = TrackGeometry.Default;
        var state = SurfaceState(
            new TrackSurfaceState(1f, 0f, 0.35f),
            new TrackSurfaceState(0.40f, 0.60f, 0.80f));
        var riders = new[]
        {
            Rider(1, lane: 1, lateralPosition: 1.2f, speed: 8f),
            Rider(2, lane: 2, lateralPosition: 1.8f, speed: 8f, elapsedTimeSeconds: 5f),
        };

        var forward = ResolveMany(segment, geometry, state, riders, targetLane: 2);
        var reversed = ResolveMany(segment, geometry, state, riders.Reverse().ToArray(), targetLane: 2);

        Assert.Equal(forward.Changes, reversed.Changes);
        Assert.Equal(forward.Events, reversed.Events);
    }

    private static TrackStateSnapshot Snapshot(params TrackSurfaceState[] surfaces)
        => new TrackState(1, surfaces.Length, (_, lane) => surfaces[lane]).Snapshot();

    private static TrackState SurfaceState(TrackSurfaceState inner, TrackSurfaceState outer)
        => new(
            segmentCount: 1,
            linesCount: TrackSegment.LanesCount,
            (_, lane) => lane switch
            {
                1 => inner,
                2 => outer,
                _ => NeutralSurface,
            });

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
        TrackGeometry geometry,
        TrackState state,
        RiderState rider,
        int targetLane,
        float decisionRisk = 0f,
        bool useLegacyPhysics = false)
        => Assert.Single(ResolveMany(
            segment,
            geometry,
            state,
            new[] { rider },
            targetLane,
            decisionRisk,
            useLegacyPhysics).Changes);

    private static ResolvedSimulationStep ResolveMany(
        TrackSegment segment,
        TrackGeometry geometry,
        TrackState state,
        IReadOnlyList<RiderState> riders,
        int targetLane,
        float decisionRisk = 0f,
        bool useLegacyPhysics = false)
    {
        var track = new Track(new[] { segment }, geometry);
        var engine = new SimulationEngine(new FixedTargetDecisionModel(targetLane, decisionRisk));
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 123,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };
        var snapshot = engine.CaptureSnapshot(
            track,
            state,
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

    private static float ExpectedLegacySurfaceRisk(TrackSurfaceState surface, float baseRisk)
    {
        var surfaceRisk = (1f - surface.Grip) * 0.7f + surface.Ruts * 0.3f;
        if (surface.Moisture > 0.5f)
            surfaceRisk += (surface.Moisture - 0.5f) * 0.2f;
        return TrackSurfaceState.Clamp01(baseRisk + TrackSurfaceState.Clamp01(surfaceRisk));
    }
}
