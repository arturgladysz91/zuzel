using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class ContinuousCornerRadiusTests
{
    private static readonly TrackSurfaceState IdealSurface = new(1f, 0f, 0.35f);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }

    [Fact]
    public void ContinuousRadiusUsesExactLateralPosition()
    {
        var geometry = new TrackGeometry(60f, 24f, 2f, MathF.PI / 3f);

        var radius = LaneModel.TurnArcRadiusMeters(1.5f, geometry);

        Assert.Equal(27f, radius);
    }

    [Fact]
    public void ContinuousRadiusInterpolatesBetweenReferenceLanes()
    {
        var geometry = new TrackGeometry(60f, 21f, 1.4f, MathF.PI / 3f);
        var innerRadius = LaneModel.TurnArcRadiusMeters(1, geometry);
        var outerRadius = LaneModel.TurnArcRadiusMeters(2, geometry);

        var midpointRadius = LaneModel.TurnArcRadiusMeters(1.5f, geometry);

        Assert.Equal((innerRadius + outerRadius) * 0.5f, midpointRadius, 5);
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(4.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ContinuousRadiusRejectsInvalidPosition(float lateralPosition)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LaneModel.TurnArcRadiusMeters(lateralPosition, TrackGeometry.Default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void IntegerReferencePositionsPreserveDiscreteRadius(int lane)
    {
        var geometry = new TrackGeometry(60f, 22f, 1.25f, MathF.PI / 3f);

        Assert.Equal(
            LaneModel.TurnArcRadiusMeters(lane, geometry),
            LaneModel.TurnArcRadiusMeters((float)lane, geometry));
    }

    [Fact]
    public void ContinuousSafeSpeedUsesSquareRootOfActualRadius()
    {
        const float innerPosition = 1.25f;
        const float outerPosition = 2.75f;
        var geometry = new TrackGeometry(60f, 24f, 1.3f, MathF.PI / 3f);
        var innerRadius = LaneModel.TurnArcRadiusMeters(innerPosition, geometry);
        var outerRadius = LaneModel.TurnArcRadiusMeters(outerPosition, geometry);
        var innerSpeed = SegmentPhysics.MaxSafeTurnSpeed(innerPosition, geometry);
        var outerSpeed = SegmentPhysics.MaxSafeTurnSpeed(outerPosition, geometry);

        Assert.Equal(MathF.Sqrt(outerRadius / innerRadius), outerSpeed / innerSpeed, 5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void IntegerReferencePositionsPreserveDiscreteSafeSpeed(int lane)
    {
        var geometry = new TrackGeometry(60f, 23f, 1.15f, MathF.PI / 3f);
        var surface = new TrackSurfaceState(0.84f, 0.12f, 0.40f);
        var skills = new RiderSkills(55f, 68f, 72f, 50f, 50f, 50f);
        var setup = new BikeSetup(0.65f, 0.35f);

        Assert.Equal(
            SegmentPhysics.MaxSafeTurnSpeed(lane, geometry),
            SegmentPhysics.MaxSafeTurnSpeed((float)lane, geometry));
        Assert.Equal(
            SegmentPhysics.MaxSafeTurnSpeed(lane, geometry, surface, skills, setup),
            SegmentPhysics.MaxSafeTurnSpeed((float)lane, geometry, surface, skills, setup));
    }

    [Fact]
    public void SegmentPhysicsContextUsesContinuousPositionWhenProvided()
    {
        const int lane = 2;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var geometry = TrackGeometry.Default;
        var maxAtInnerEdge = SegmentPhysics.MaxSafeTurnSpeed(
            0f,
            geometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        var entrySpeed = maxAtInnerEdge * 1.05f;

        var inner = SegmentPhysics.Apply(Context(segment, lane, entrySpeed, geometry, 0f));
        var outer = SegmentPhysics.Apply(Context(segment, lane, entrySpeed, geometry, 4f));

        Assert.Equal(SegmentOutcome.Brake, inner.Outcome);
        Assert.Equal(SegmentOutcome.Ok, outer.Outcome);
        Assert.Equal(lane, inner.Lane);
        Assert.Equal(lane, outer.Lane);
        Assert.Equal(entrySpeed, inner.Speed);
        Assert.Equal(entrySpeed, outer.Speed);
        Assert.NotNull(inner.ContinuousCorrectionTargetSpeedMetersPerSecond);
        Assert.Null(outer.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void SegmentPhysicsContextWithoutContinuousPositionPreservesLegacyContextBehavior()
    {
        const int lane = 2;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            TrackGeometry.Default,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        var entrySpeed = maxSafeSpeed * 1.20f;
        var withoutContinuousPosition = new SegmentPhysicsContext(
            segment,
            lane,
            entrySpeed,
            TrackGeometry.Default,
            IdealSurface,
            RiderSkills.Balanced,
            Morale: 0.5f,
            Setup: BikeSetup.Neutral);
        var explicitReferencePosition = withoutContinuousPosition with
        {
            LateralPosition = lane,
        };

        Assert.Equal(
            SegmentPhysics.Apply(withoutContinuousPosition),
            SegmentPhysics.Apply(explicitReferencePosition));
    }

    [Fact]
    public void SimulationEngineUsesEntryLateralPositionForCornerConstraint()
    {
        const int lane = 2;
        const float innerPosition = 1.2f;
        const float outerPosition = 1.8f;
        var geometry = TrackGeometry.Default;
        var innerMaxSafeSpeed = CornerTestSupport.SingleEnvelopeSpeed(
            innerPosition,
            geometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        var entrySpeed = innerMaxSafeSpeed * 1.02f;

        var inner = ResolveSingle(SegmentType.TurnMiddle, Rider(1, lane, innerPosition, entrySpeed), geometry);
        var outer = ResolveSingle(SegmentType.TurnMiddle, Rider(1, lane, outerPosition, entrySpeed), geometry);

        Assert.Equal(lane, inner.PlannedLane);
        Assert.Equal(lane, outer.PlannedLane);
        Assert.Equal(entrySpeed, inner.EntrySpeed);
        Assert.Equal(entrySpeed, outer.EntrySpeed);
        Assert.Equal(SegmentOutcome.Brake, inner.Outcome);
        Assert.Equal(SegmentOutcome.Ok, outer.Outcome);
        Assert.Equal(entrySpeed, inner.PhysicsSpeed);
        Assert.Equal(entrySpeed, outer.PhysicsSpeed);
        Assert.True(outer.Speed > inner.Speed);
    }

    [Fact]
    public void AdvancedEntrySpeedFromRestUsesEntryLateralPosition()
    {
        const int lane = 2;
        const float innerPosition = 1.2f;
        const float outerPosition = 1.8f;
        var geometry = TrackGeometry.Default;
        var innerRider = Rider(1, lane, innerPosition, speed: 0f);
        var outerRider = Rider(1, lane, outerPosition, speed: 0f);
        var startMultiplier = 0.90f + RiderSkills.Normalize(innerRider.Profile.Skills.Start) * 0.16f;

        var inner = ResolveSingle(SegmentType.TurnMiddle, innerRider, geometry);
        var outer = ResolveSingle(SegmentType.TurnMiddle, outerRider, geometry);
        var expectedInnerSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            innerPosition,
            geometry,
            IdealSurface,
            innerRider.Profile.Skills,
            innerRider.ActiveSetup) * startMultiplier;
        var expectedOuterSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            outerPosition,
            geometry,
            IdealSurface,
            outerRider.Profile.Skills,
            outerRider.ActiveSetup) * startMultiplier;

        Assert.Equal(lane, inner.PlannedLane);
        Assert.Equal(lane, outer.PlannedLane);
        Assert.Equal(expectedInnerSpeed, inner.EntrySpeed, 5);
        Assert.Equal(expectedOuterSpeed, outer.EntrySpeed, 5);
        Assert.True(outer.EntrySpeed > inner.EntrySpeed);
    }

    [Fact]
    public void StraightPhysicsDoesNotDependOnContinuousTurnRadius()
    {
        const int lane = 2;
        var geometry = new TrackGeometry(60f, 31f, 1.4f, MathF.PI / 3f);

        var inner = ResolveSingle(SegmentType.Straight, Rider(1, lane, 1.2f, speed: 0f), geometry);
        var outer = ResolveSingle(SegmentType.Straight, Rider(1, lane, 1.8f, speed: 0f), geometry);

        Assert.Equal(lane, inner.PlannedLane);
        Assert.Equal(lane, outer.PlannedLane);
        Assert.Equal(inner.EntrySpeed, outer.EntrySpeed);
        Assert.Equal(inner.PhysicsSpeed, outer.PhysicsSpeed);
        Assert.Equal(inner.Outcome, outer.Outcome);
    }

    [Fact]
    public void ContinuousCornerConstraintIsIndependentOfRiderCollectionOrder()
    {
        const int lane = 2;
        var geometry = TrackGeometry.Default;
        var entrySpeed = SegmentPhysics.MaxSafeTurnSpeed(
            1.2f,
            geometry,
            IdealSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral) * 1.02f;
        var riders = new[]
        {
            Rider(1, lane, 1.2f, entrySpeed),
            Rider(2, lane, 1.8f, entrySpeed, elapsedTimeSeconds: 5f),
        };

        var forward = ResolveMany(riders, geometry);
        var reversed = ResolveMany(riders.Reverse().ToArray(), geometry);

        Assert.Equal(forward.Changes, reversed.Changes);
        Assert.Equal(forward.Events, reversed.Events);
    }

    private static SegmentPhysicsContext Context(
        TrackSegment segment,
        int lane,
        float speed,
        TrackGeometry geometry,
        float lateralPosition)
        => new(
            Segment: segment,
            Lane: lane,
            Speed: speed,
            Geometry: geometry,
            Surface: IdealSurface,
            Skills: RiderSkills.Balanced,
            Morale: 0.5f,
            Setup: BikeSetup.Neutral,
            DecisionRisk: 0f,
            LateralPosition: lateralPosition);

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
        SegmentType segmentType,
        RiderState rider,
        TrackGeometry geometry)
        => Assert.Single(ResolveMany(new[] { rider }, geometry, segmentType).Changes);

    private static ResolvedSimulationStep ResolveMany(
        IReadOnlyList<RiderState> riders,
        TrackGeometry geometry,
        SegmentType segmentType = SegmentType.TurnMiddle)
    {
        var track = new Track(new[] { new TrackSegment(0, segmentType) }, geometry);
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
            new SimulationStepContext(1, 0, 0, 0, options.Seed, options.Laps));

        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }
}
