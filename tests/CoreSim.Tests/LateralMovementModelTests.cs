using CoreSim;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class LateralMovementModelTests
{
    private static readonly TrackSurfaceState IdealSurface = new(1f, 0f, 0.35f);

    private sealed class TargetDecisionModel(int targetLane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(targetLane, 0f);
    }

    private sealed class PerRiderDecisionModel(IReadOnlyDictionary<int, int> targetLanes) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(targetLanes[rider.RiderId], 0f);
    }

    [Fact]
    public void MaxDeltaUsesExecutionTravelTimeGripAndLaneSpacingFormula()
    {
        var geometry = Geometry(laneSpacingMeters: 1.5f);
        var surface = new TrackSurfaceState(0.82f, 0.15f, 0.40f);
        var skills = Skills(slideControl: 80f, adaptability: 40f);
        const float travelTimeSeconds = 2.25f;
        const float execution = 0.50f * 0.80f + 0.50f * 0.40f;
        var lateralSpeedMetersPerSecond = 0.35f + 0.30f * execution;
        var gripMultiplier = 0.65f + 0.35f * surface.EffectiveGrip;
        var expected = lateralSpeedMetersPerSecond
            * travelTimeSeconds
            * gripMultiplier
            / geometry.LaneSpacingMeters;

        var actual = LateralMovementModel.CalculateMaxLateralDelta(
            travelTimeSeconds,
            geometry,
            surface,
            skills);

        Assert.Equal(expected, actual, 5);
        Assert.True(float.IsFinite(actual));
        Assert.True(actual > 0f);
    }

    [Fact]
    public void MoveTowardsDoesNotOvershootTarget()
    {
        var result = LateralMovementModel.MoveTowards(
            currentLateralPosition: 1.9f,
            resolvedLane: 2,
            segmentTravelTimeSeconds: 100f,
            Geometry(),
            IdealSurface,
            RiderSkills.Balanced);

        Assert.Equal(2f, result);
    }

    [Fact]
    public void MoveTowardsIsSymmetricInwardAndOutward()
    {
        var geometry = Geometry();
        var outward = LateralMovementModel.MoveTowards(
            1f,
            3,
            0.75f,
            geometry,
            IdealSurface,
            RiderSkills.Balanced);
        var inward = LateralMovementModel.MoveTowards(
            3f,
            1,
            0.75f,
            geometry,
            IdealSurface,
            RiderSkills.Balanced);

        Assert.Equal(outward - 1f, 3f - inward, 5);
        Assert.InRange(outward, 1f, 3f);
        Assert.InRange(inward, 1f, 3f);
    }

    [Fact]
    public void ZeroTravelTimeDoesNotMoveLateralPosition()
    {
        var result = LateralMovementModel.MoveTowards(
            1.25f,
            4,
            0f,
            Geometry(),
            IdealSurface,
            RiderSkills.Balanced);

        Assert.Equal(1.25f, result);
    }

    [Fact]
    public void LongerTravelTimeAllowsMoreMovement()
    {
        var geometry = Geometry();
        var shortTravel = LateralMovementModel.MoveTowards(
            0f,
            4,
            0.5f,
            geometry,
            IdealSurface,
            RiderSkills.Balanced);
        var longTravel = LateralMovementModel.MoveTowards(
            0f,
            4,
            1.5f,
            geometry,
            IdealSurface,
            RiderSkills.Balanced);

        Assert.True(longTravel > shortTravel);
    }

    [Fact]
    public void WiderLaneSpacingReducesDeltaInLaneUnits()
    {
        var narrow = LateralMovementModel.CalculateMaxLateralDelta(
            1f,
            Geometry(laneSpacingMeters: 1f),
            IdealSurface,
            RiderSkills.Balanced);
        var wide = LateralMovementModel.CalculateMaxLateralDelta(
            1f,
            Geometry(laneSpacingMeters: 2f),
            IdealSurface,
            RiderSkills.Balanced);

        Assert.Equal(narrow / 2f, wide, 5);
        Assert.True(wide < narrow);
    }

    [Theory]
    [InlineData(100f, 0f)]
    [InlineData(0f, 100f)]
    public void HigherExecutionSkillAllowsMoreMovement(float slideControl, float adaptability)
    {
        var low = LateralMovementModel.CalculateMaxLateralDelta(
            1f,
            Geometry(),
            IdealSurface,
            Skills(slideControl: 0f, adaptability: 0f));
        var high = LateralMovementModel.CalculateMaxLateralDelta(
            1f,
            Geometry(),
            IdealSurface,
            Skills(slideControl, adaptability));

        Assert.True(high > low);
    }

    [Fact]
    public void BetterGripAllowsMoreControlledMovement()
    {
        var poorGrip = LateralMovementModel.CalculateMaxLateralDelta(
            1f,
            Geometry(),
            new TrackSurfaceState(0.40f, 0f, 0.35f),
            RiderSkills.Balanced);
        var idealGrip = LateralMovementModel.CalculateMaxLateralDelta(
            1f,
            Geometry(),
            IdealSurface,
            RiderSkills.Balanced);

        Assert.True(idealGrip > poorGrip);
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(4.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RiderStateRejectsInvalidLateralPosition(float invalidPosition)
    {
        var rider = new RiderState(1, lane: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => rider.LateralPosition = invalidPosition);
        Assert.Equal(2f, rider.LateralPosition);
    }

    [Fact]
    public void LaneArrivalToleranceIsMeasuredInPhysicalMeters()
    {
        var geometry = Geometry(laneSpacingMeters: 2f);

        var outsideTolerance = LateralMovementModel.CalculatePlannedLane(
            currentLane: 1,
            currentLateralPosition: 0.97f,
            targetLane: 4,
            geometry,
            useContinuousPlanning: true);
        var insideTolerance = LateralMovementModel.CalculatePlannedLane(
            currentLane: 1,
            currentLateralPosition: 0.98f,
            targetLane: 4,
            geometry,
            useContinuousPlanning: true);

        Assert.Equal(1, outsideTolerance);
        Assert.Equal(2, insideTolerance);
    }

    [Fact]
    public void SimulationEngineUsesOnlyCurrentSegmentTravelTimeAndDefersMutationUntilCommit()
    {
        var track = TrackWithTurns(segmentCount: 1, turnAngleRadians: 0.20f);
        var trackState = TrackState.CreateDefault(track, IdealSurface);
        var rider = new RiderState(1, lane: 0)
        {
            Speed = 10f,
            ElapsedTimeSeconds = 20f,
        };
        var engine = new SimulationEngine(new TargetDecisionModel(1));
        var options = Options();
        var snapshot = engine.CaptureSnapshot(
            track,
            trackState,
            new[] { rider },
            new SimulationStepContext(1, 0, 0, 0, options.Seed, options.Laps));

        var resolved = engine.Resolve(snapshot, engine.Decide(snapshot), options);
        var change = Assert.Single(resolved.Changes);
        var travelledMeters = LaneModel.SegmentLengthMeters(track.Segments[0], change.Lane, track.Geometry);
        var averageSpeedMetersPerSecond = (change.EntrySpeed + change.Speed) * 0.5f;
        var segmentTravelTimeSeconds = travelledMeters / averageSpeedMetersPerSecond;
        var expectedLateralPosition = LateralMovementModel.MoveTowards(
            0f,
            change.Lane,
            segmentTravelTimeSeconds,
            track.Geometry,
            IdealSurface,
            rider.Profile.Skills);

        Assert.Equal(0f, rider.LateralPosition);
        Assert.Equal(20f, rider.ElapsedTimeSeconds);
        Assert.Equal(segmentTravelTimeSeconds, change.ElapsedTimeSeconds - rider.ElapsedTimeSeconds, 5);
        Assert.Equal(expectedLateralPosition, change.LateralPosition, 5);

        engine.Commit(resolved, new[] { rider }, trackState, new SimLog());

        Assert.Equal(change.LateralPosition, rider.LateralPosition);
        Assert.Equal(change.ElapsedTimeSeconds, rider.ElapsedTimeSeconds);
    }

    [Fact]
    public void MoraleAndLaneChangeTendencyDoNotChangePhysicalMovement()
    {
        var track = TrackWithTurns(segmentCount: 1, turnAngleRadians: 0.20f);
        var skills = Skills(slideControl: 60f, adaptability: 70f);
        var baseline = new RiderState(
            new RiderProfile(1, "Baseline", skills, RiderStyle.Balanced),
            lane: 0,
            morale: 0.5f)
        {
            Speed = 10f,
        };
        var differentTendency = new RiderState(
            new RiderProfile(2, "Different tendency", skills, new RiderStyle(0.5f, 1f, 0.5f, 0.5f)),
            lane: 0,
            morale: 0.5f)
        {
            Speed = 10f,
        };
        var differentMorale = new RiderState(
            new RiderProfile(3, "Different morale", skills, RiderStyle.Balanced),
            lane: 0,
            morale: 1f)
        {
            Speed = 10f,
        };

        var baselineChange = ResolveSingle(track, baseline, targetLane: 1);
        var tendencyChange = ResolveSingle(track, differentTendency, targetLane: 1);
        var moraleChange = ResolveSingle(track, differentMorale, targetLane: 1);

        Assert.Equal(baselineChange.LateralPosition, tendencyChange.LateralPosition);
        Assert.Equal(baselineChange.LateralPosition, moraleChange.LateralPosition);
    }

    [Fact]
    public void AdvancedPlanningWaitsAtCurrentLaneUntilLateralPositionArrives()
    {
        var track = TrackWithTurns(segmentCount: 2, turnAngleRadians: 0.10f);
        var trackState = TrackState.CreateDefault(track, IdealSurface);
        var rider = new RiderState(1, lane: 0) { Speed = 10f };
        var engine = new SimulationEngine(new TargetDecisionModel(4));
        var options = Options();

        var first = ResolveStep(engine, track, trackState, rider, options, stepNumber: 0, segmentIndex: 0);
        engine.Commit(first, new[] { rider }, trackState, new SimLog());
        var firstChange = Assert.Single(first.Changes);
        Assert.Equal(1, firstChange.PlannedLane);
        Assert.Equal(1, firstChange.Lane);
        Assert.InRange(firstChange.LateralPosition, 0f, 0.99f);

        var second = ResolveStep(engine, track, trackState, rider, options, stepNumber: 1, segmentIndex: 1);
        var secondChange = Assert.Single(second.Changes);

        Assert.Equal(1, secondChange.BeforeLane);
        Assert.Equal(1, secondChange.PlannedLane);
        Assert.Equal(1, secondChange.Lane);
        Assert.True(secondChange.LateralPosition > firstChange.LateralPosition);
    }

    [Fact]
    public void AdvancedPlanningAllowsImmediateReversalBeforePreviousLaneArrival()
    {
        var track = TrackWithTurns(segmentCount: 2, turnAngleRadians: 0.10f);
        var trackState = TrackState.CreateDefault(track, IdealSurface);
        var rider = new RiderState(1, lane: 0) { Speed = 10f };
        var options = Options();
        var outwardEngine = new SimulationEngine(new TargetDecisionModel(4));

        var outward = ResolveStep(
            outwardEngine,
            track,
            trackState,
            rider,
            options,
            stepNumber: 0,
            segmentIndex: 0);
        outwardEngine.Commit(outward, new[] { rider }, trackState, new SimLog());
        var outwardChange = Assert.Single(outward.Changes);
        Assert.Equal(1, rider.Lane);
        Assert.InRange(rider.LateralPosition, 0.01f, 0.99f);

        var inwardEngine = new SimulationEngine(new TargetDecisionModel(0));
        var inward = ResolveStep(
            inwardEngine,
            track,
            trackState,
            rider,
            options,
            stepNumber: 1,
            segmentIndex: 1);
        var inwardChange = Assert.Single(inward.Changes);

        Assert.Equal(0, inwardChange.PlannedLane);
        Assert.Equal(0, inwardChange.Lane);
        Assert.True(inwardChange.LateralPosition < outwardChange.LateralPosition);
    }

    [Fact]
    public void RunWideDoesNotSkipAnUnreachedReferenceLaneOnTheNextStep()
    {
        var track = TrackWithTurns(segmentCount: 2, turnAngleRadians: 0.10f);
        var trackState = TrackState.CreateDefault(track, IdealSurface);
        var rider = new RiderState(1, lane: 0);
        rider.Speed = SegmentPhysics.MaxSafeTurnSpeed(
            1,
            track.Geometry,
            IdealSurface,
            rider.Profile.Skills,
            rider.ActiveSetup) * 1.12f;
        var engine = new SimulationEngine(new TargetDecisionModel(4));
        var options = Options();

        var first = ResolveStep(engine, track, trackState, rider, options, stepNumber: 0, segmentIndex: 0);
        var firstChange = Assert.Single(first.Changes);
        Assert.Equal(1, firstChange.PlannedLane);
        Assert.Equal(SegmentOutcome.RunWide, firstChange.Outcome);
        Assert.Equal(2, firstChange.Lane);
        Assert.True(firstChange.LateralPosition < 1f);
        engine.Commit(first, new[] { rider }, trackState, new SimLog());

        var second = ResolveStep(engine, track, trackState, rider, options, stepNumber: 1, segmentIndex: 1);
        var secondChange = Assert.Single(second.Changes);

        Assert.Equal(2, secondChange.BeforeLane);
        Assert.Equal(4, secondChange.TargetLane);
        Assert.Equal(1, secondChange.PlannedLane);
    }

    [Fact]
    public void ReversingAfterRunWideImmediatelyMovesTowardTheInnerTarget()
    {
        var track = TrackWithTurns(segmentCount: 2, turnAngleRadians: 0.10f);
        var trackState = TrackState.CreateDefault(track, IdealSurface);
        var rider = new RiderState(1, lane: 0);
        rider.Speed = SegmentPhysics.MaxSafeTurnSpeed(
            1,
            track.Geometry,
            IdealSurface,
            rider.Profile.Skills,
            rider.ActiveSetup) * 1.12f;
        var options = Options();
        var outwardEngine = new SimulationEngine(new TargetDecisionModel(4));

        var outward = ResolveStep(
            outwardEngine,
            track,
            trackState,
            rider,
            options,
            stepNumber: 0,
            segmentIndex: 0);
        var outwardChange = Assert.Single(outward.Changes);
        Assert.Equal(SegmentOutcome.RunWide, outwardChange.Outcome);
        Assert.Equal(2, outwardChange.Lane);
        Assert.True(outwardChange.LateralPosition < 1f);
        outwardEngine.Commit(outward, new[] { rider }, trackState, new SimLog());
        var beforeReversal = rider.LateralPosition;

        var inwardEngine = new SimulationEngine(new TargetDecisionModel(0));
        var inward = ResolveStep(
            inwardEngine,
            track,
            trackState,
            rider,
            options,
            stepNumber: 1,
            segmentIndex: 1);
        var inwardChange = Assert.Single(inward.Changes);

        Assert.Equal(2, inwardChange.BeforeLane);
        Assert.Equal(0, inwardChange.TargetLane);
        Assert.Equal(0, inwardChange.PlannedLane);
        Assert.True(inwardChange.LateralPosition < beforeReversal);
    }

    [Fact]
    public void RunWideMovesLateralPositionTowardResolvedLaneWithoutChangingPhysicsSpeed()
    {
        var track = TrackWithTurns(segmentCount: 1, turnAngleRadians: TrackGeometry.Default.TurnSegmentAngleRadians);
        var rider = new RiderState(1, lane: 1) { Speed = 0f };
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            1,
            track.Geometry,
            IdealSurface,
            rider.Profile.Skills,
            rider.ActiveSetup);
        var entrySpeed = maxSafeSpeed * 1.20f;
        rider.Speed = entrySpeed;
        var expected = SegmentPhysics.Apply(new SegmentPhysicsContext(
            track.Segments[0],
            1,
            entrySpeed,
            track.Geometry,
            IdealSurface,
            rider.Profile.Skills,
            rider.Morale,
            rider.ActiveSetup));

        var change = ResolveSingle(track, rider, targetLane: 1);

        Assert.Equal(SegmentOutcome.RunWide, expected.Outcome);
        Assert.Equal(expected.Outcome, change.Outcome);
        Assert.Equal(expected.Lane, change.Lane);
        Assert.Equal(expected.Speed, change.PhysicsSpeed);
        Assert.InRange(change.LateralPosition, 1.0001f, expected.Lane);
    }

    [Fact]
    public void LegacyPhysicsStillMovesLateralPositionImmediatelyToResolvedLane()
    {
        var track = TrackWithTurns(segmentCount: 1, turnAngleRadians: 0.10f);
        var rider = new RiderState(1, lane: 0) { Speed = 10f };
        var change = ResolveSingle(track, rider, targetLane: 4, useLegacyPhysics: true);

        Assert.Equal(1, change.PlannedLane);
        Assert.Equal(1, change.Lane);
        Assert.Equal(1f, change.LateralPosition);
    }

    [Fact]
    public void ResolveIsIndependentOfRiderCollectionOrder()
    {
        var track = TrackWithTurns(segmentCount: 1, turnAngleRadians: 0.20f);
        var first = new RiderState(1, lane: 0) { Speed = 10f };
        var second = new RiderState(2, lane: 3) { Speed = 10f };
        var engine = new SimulationEngine(new PerRiderDecisionModel(new Dictionary<int, int>
        {
            [first.RiderId] = 1,
            [second.RiderId] = 2,
        }));

        ResolvedSimulationStep Resolve(IReadOnlyList<RiderState> riders)
        {
            var options = Options();
            var snapshot = engine.CaptureSnapshot(
                track,
                TrackState.CreateDefault(track, IdealSurface),
                riders,
                new SimulationStepContext(1, 0, 0, 0, options.Seed, options.Laps));
            return engine.Resolve(snapshot, engine.Decide(snapshot), options);
        }

        var forward = Resolve(new[] { first, second });
        var reversed = Resolve(new[] { second, first });

        Assert.Equal(forward.Changes, reversed.Changes);
        Assert.Equal(forward.Events, reversed.Events);
    }

    private static RiderStateChange ResolveSingle(
        Track track,
        RiderState rider,
        int targetLane,
        bool useLegacyPhysics = false)
    {
        var options = Options();
        var engine = new SimulationEngine(new TargetDecisionModel(targetLane));
        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track, IdealSurface),
            new[] { rider },
            new SimulationStepContext(
                1,
                0,
                0,
                0,
                options.Seed,
                options.Laps,
                UseLegacyPhysics: useLegacyPhysics));
        return Assert.Single(engine.Resolve(snapshot, engine.Decide(snapshot), options).Changes);
    }

    private static ResolvedSimulationStep ResolveStep(
        SimulationEngine engine,
        Track track,
        TrackState trackState,
        RiderState rider,
        HeatSimulationOptions options,
        int stepNumber,
        int segmentIndex)
    {
        var snapshot = engine.CaptureSnapshot(
            track,
            trackState,
            new[] { rider },
            new SimulationStepContext(1, stepNumber, 0, segmentIndex, options.Seed, options.Laps));
        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    private static Track TrackWithTurns(int segmentCount, float turnAngleRadians)
        => new(
            Enumerable.Range(0, segmentCount)
                .Select(index => new TrackSegment(index, SegmentType.TurnMiddle))
                .ToArray(),
            new TrackGeometry(60f, 24f, 1f, turnAngleRadians));

    private static TrackGeometry Geometry(float laneSpacingMeters = 1f)
        => new(60f, 24f, laneSpacingMeters, 0.20f);

    private static RiderSkills Skills(float slideControl, float adaptability)
        => new(50f, 50f, slideControl, 50f, 50f, adaptability);

    private static HeatSimulationOptions Options() => new()
    {
        Laps = 1,
        Seed = 123,
        Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
        IncidentFrequency = 0f,
    };
}
