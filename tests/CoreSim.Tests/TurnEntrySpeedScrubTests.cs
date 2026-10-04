using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class TurnEntrySpeedScrubTests
{
    private static readonly TrackSurfaceState PerfectSurface = new(1f, 0f, 0.35f);
    private static readonly TrackSurfaceState PoorSurface = new(0.35f, 0.65f, 0.80f);

    [Fact]
    public void StraightAndTurnEntryFormOneControlledSequence()
    {
        var track = TrackOf(120f, SegmentType.Straight, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var straightRider = Rider(1, lane: 1, lateralPosition: 1f, speed: 10f);
        var settledSafeSpeed = SettledSafeSpeed(track, state, straightRider, segmentIndex: 1);

        var straight = ResolveSingle(
            track,
            state,
            straightRider,
            targetLane: 1,
            segmentIndex: 0);
        var turnEntryRider = RiderAt(
            1,
            lane: straight.Change.Lane,
            lateralPosition: straight.Change.LateralPosition,
            speed: straight.Change.Speed,
            segmentIndex: 1,
            track);
        turnEntryRider.ElapsedTimeSeconds = straight.Change.ElapsedTimeSeconds;

        var turnEntry = ResolveSingle(
            track,
            state,
            turnEntryRider,
            targetLane: 1,
            segmentIndex: 1);

        Assert.True(straight.Change.Speed > settledSafeSpeed);
        Assert.Equal(SegmentOutcome.Ok, turnEntry.Change.Outcome);
        Assert.True(turnEntry.Change.Speed > settledSafeSpeed); // The logical corner also rebuilds after its apex.
    }

    [Fact]
    public void RecoverableHighEntryUsesBoundedTraversalAndReturnsOk()
    {
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 0f);
        var settledSafeSpeed = SettledSafeSpeed(track, state, rider, segmentIndex: 0);
        rider.Speed = MaximumApproachSpeed(track, state, rider, segmentIndex: 0);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.True(result.Change.EntrySpeed > settledSafeSpeed);
        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        Assert.Equal(rider.Speed, result.Change.PhysicsSpeed);
        Assert.True(result.Change.Speed > settledSafeSpeed);
        Assert.True(result.Change.Speed < rider.Speed);
    }

    [Fact]
    public void TurnEntryClassificationUsesSameRecoverableEnvelopeAsTraversal()
    {
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 0f);
        rider.Speed = MaximumApproachSpeed(track, state, rider, segmentIndex: 0);
        var directConstraint = ApplyAdvancedConstraint(
            track,
            state,
            rider,
            segmentIndex: 0,
            speed: rider.Speed,
            lane: 1);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.Equal(SegmentOutcome.Ok, directConstraint.Outcome);
        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
    }

    [Fact]
    public void ResidualOverspeedStillUsesSegmentPhysics()
    {
        const float residualSpeedFactor = 1.15f;
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 0f);
        var settledSafeSpeed = SettledSafeSpeed(track, state, rider, segmentIndex: 0);
        var deceleration = ScrubDeceleration(state, rider, segmentIndex: 0);
        var scrubDistance = TurnEntryLength(track, rider)
            * LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction;
        var desiredResidualSpeed = settledSafeSpeed * residualSpeedFactor;
        rider.Speed = MathF.Sqrt(
            desiredResidualSpeed * desiredResidualSpeed
            + 2f * deceleration * scrubDistance);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.Equal(SegmentOutcome.RunWide, result.Change.Outcome);
        Assert.Equal(2, result.Change.Lane);
        Assert.True(result.Change.PhysicsSpeed > settledSafeSpeed);
    }

    [Fact]
    public void ExtremeResidualOverspeedCanStillCrash()
    {
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 40f);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.Equal(SegmentOutcome.Crash, result.Change.Outcome);
        Assert.Equal(0f, result.Change.Speed);
    }

    [Fact]
    public void OuterLaneResidualRunWideRangeStillCrashesWhenNoRoom()
    {
        const float residualSpeedFactor = 1.15f;
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 4, lateralPosition: 4f, speed: 0f);
        var settledSafeSpeed = SettledSafeSpeed(track, state, rider, segmentIndex: 0);
        var deceleration = ScrubDeceleration(state, rider, segmentIndex: 0);
        var scrubDistance = TurnEntryLength(track, rider)
            * LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction;
        var desiredResidualSpeed = settledSafeSpeed * residualSpeedFactor;
        rider.Speed = MathF.Sqrt(
            desiredResidualSpeed * desiredResidualSpeed
            + 2f * deceleration * scrubDistance);

        var result = ResolveSingle(track, state, rider, targetLane: 4);

        Assert.Equal(SegmentOutcome.Crash, result.Change.Outcome);
        Assert.Equal(4, result.Change.Lane);
    }

    [Fact]
    public void BetterSlideControlCanRecoverHigherEntrySpeed()
    {
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var highControl = Rider(
            1,
            lane: 1,
            lateralPosition: 1f,
            speed: 0f,
            slideControl: 100f);
        highControl.Speed = MaximumApproachSpeed(track, state, highControl, segmentIndex: 0);
        var lowControl = Rider(
            2,
            lane: 1,
            lateralPosition: 1f,
            speed: highControl.Speed,
            slideControl: 0f);

        var high = ResolveSingle(track, state, highControl, targetLane: 1);
        var low = ResolveSingle(track, state, lowControl, targetLane: 1);

        Assert.Equal(SegmentOutcome.Ok, high.Change.Outcome);
        Assert.NotEqual(SegmentOutcome.Ok, low.Change.Outcome);
        Assert.True(high.Change.EntrySpeed > MaximumApproachSpeed(track, state, lowControl, 0));
    }

    [Fact]
    public void CurrentTurnEntrySurfaceControlsActualScrub()
    {
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var goodState = UniformState(track, PerfectSurface);
        var poorState = UniformState(track, PoorSurface);
        var goodRider = Rider(1, lane: 1, lateralPosition: 1f, speed: 0f);
        goodRider.Speed = MaximumApproachSpeed(track, goodState, goodRider, segmentIndex: 0);
        var poorRider = Clone(goodRider, riderId: 2);

        var good = ResolveSingle(track, goodState, goodRider, targetLane: 1);
        var poor = ResolveSingle(track, poorState, poorRider, targetLane: 1);

        Assert.Equal(SegmentOutcome.Ok, good.Change.Outcome);
        Assert.NotEqual(SegmentOutcome.Ok, poor.Change.Outcome);
        Assert.NotEqual(good.Change.PhysicsSpeed, poor.Change.PhysicsSpeed);
    }

    [Fact]
    public void PartialTurnEntryUsesOnlyRemainingDistance()
    {
        var r = CornerTestSupport.Probe(.125f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.Equal(r.AvailableDistanceMeters, r.Change.Position.DistanceMeters, 5);
        Assert.Equal(.125f, p.Nodes[0].CornerProgress, 6);
        Assert.Equal(1f / 3f, p.Nodes[^1].CornerProgress, 6);
    }

    [Fact]
    public void TurnEntryTimeComesFromContinuousDistanceSteps()
    {
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, 1, 1f, 0f);
        rider.Speed = MaximumApproachSpeed(track, state, rider, 0);
        var distance = TurnEntryLength(track, rider);
        var p = CornerTestSupport.Envelope(track, rider).Traverse(rider.Speed, 0f, distance);
        var result = ResolveSingle(track, state, rider, 1);
        Assert.Equal(p.TravelTimeSeconds, result.Change.ElapsedTimeSeconds, 5);
        Assert.NotEqual(distance / ((rider.Speed + p.ExitSpeedMetersPerSecond) * .5f), result.Change.ElapsedTimeSeconds, 4);
    }

    [Fact]
    public void PhysicsCrashKeepsExistingPartialAdvanceWithoutSeparateScrub()
    {
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var rider = Rider(1, 1, 1f, 40f);
        var result = ResolveSingle(track, UniformState(track, PerfectSurface), rider, 1);
        Assert.Equal(SegmentOutcome.Crash, result.Change.Outcome);
        var distance = TurnEntryLength(track, rider) * .5f; // Existing crash partial advance, not a scrub phase.
        Assert.Equal(distance, result.Change.Position.DistanceMeters, 5);
        Assert.Equal(distance / (rider.Speed * .5f), result.Change.ElapsedTimeSeconds, 5);
    }

    [Fact]
    public void TurnEntryProfileTimeControlsLateralMovementBudget()
    {
        var geometry = new TrackGeometry(60f, 24f, 4f, TrackGeometry.Default.TurnSegmentAngleRadians);
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnEntry) }, geometry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, 1, 1f, 0f);
        rider.Speed = MaximumApproachSpeed(track, state, rider, 0);
        var p = CornerTestSupport.Envelope(track, rider).Traverse(rider.Speed, 0f, TurnEntryLength(track, rider));
        var result = ResolveSingle(track, state, rider, 4);
        var expected = LateralMovementModel.MoveTowards(1f, 2, p.TravelTimeSeconds, geometry, PerfectSurface, rider.Profile.Skills);
        var path = Assert.IsType<ExecutedSegmentPath>(result.Diagnostics.ExecutedPath);
        Assert.Equal(path.TravelTimeSeconds, result.Change.ElapsedTimeSeconds);
        Assert.Equal(path.Nodes[^1].LateralPosition, result.Change.LateralPosition);
        Assert.NotEqual(TurnEntryLength(track, rider), path.DistanceMeters);
    }

    [Fact]
    public void TurnMiddleDoesNotUseTurnEntryScrub()
    {
        var track = TrackOf(60f, SegmentType.TurnMiddle);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 19f);
        var expected = ApplyAdvancedConstraint(track, state, rider, 0, rider.Speed, lane: 1);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.Equal(expected.Outcome, result.Change.Outcome);
        Assert.Equal(expected.Speed, result.Change.PhysicsSpeed, 5);
    }

    [Fact]
    public void TurnExitDoesNotUseTurnEntryScrub()
    {
        var track = TrackOf(60f, SegmentType.TurnExit);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 19f);
        var expected = ApplyAdvancedConstraint(track, state, rider, 0, rider.Speed, lane: 1);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.Equal(expected.Outcome, result.Change.Outcome);
        Assert.Equal(expected.Speed, result.Change.PhysicsSpeed, 5);
    }

    [Fact]
    public void LegacyTurnEntryRemainsUnchanged()
    {
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 18f);
        var expected = SegmentPhysics.Apply(track.Segments[0], lane: 1, speed: rider.Speed);

        var result = ResolveSingle(
            track,
            state,
            rider,
            targetLane: 1,
            useLegacyPhysics: true);

        Assert.Equal(expected.Outcome, result.Change.Outcome);
        Assert.Equal(expected.Speed, result.Change.PhysicsSpeed, 5);
        Assert.Equal(expected.Speed, result.Change.Speed, 5);
    }

    [Fact]
    public void TurnEntryScrubIsIndependentOfRiderCollectionOrder()
    {
        var track = TrackOf(60f, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var first = Rider(1, lane: 0, lateralPosition: 0f, speed: 0f, slideControl: 100f);
        first.Speed = MaximumApproachSpeed(track, state, first, segmentIndex: 0);
        var second = Rider(2, lane: 4, lateralPosition: 4f, speed: 23f, slideControl: 20f);
        var riders = new[] { first, second };
        var targets = new Dictionary<int, int> { [1] = 0, [2] = 4 };

        var forward = Resolve(track, state, riders, new PerRiderTargetDecisionModel(targets));
        var reversed = Resolve(
            track,
            state,
            riders.Reverse().Select(rider => Clone(rider, rider.RiderId)).ToArray(),
            new PerRiderTargetDecisionModel(targets));

        Assert.Equal(Project(forward), Project(reversed));
    }

    private static float SettledSafeSpeed(
        Track track,
        TrackState state,
        RiderState rider,
        int segmentIndex)
        => SegmentPhysics.MaxSafeTurnSpeed(
            rider.LateralPosition,
            track.Geometry,
            state.Snapshot().SampleSurface(segmentIndex, rider.LateralPosition),
            rider.Profile.Skills,
            rider.ActiveSetup);

    private static float ScrubDeceleration(
        TrackState state,
        RiderState rider,
        int segmentIndex)
        => LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(
            rider.Profile.Skills,
            state.Snapshot().SampleSurface(segmentIndex, rider.LateralPosition));

    private static float TurnEntryLength(Track track, RiderState rider)
        => LaneModel.SegmentLengthMeters(
            track.Segments[rider.SegmentIndex],
            rider.LateralPosition,
            track.Geometry);

    private static float MaximumApproachSpeed(
        Track track,
        TrackState state,
        RiderState rider,
        int segmentIndex)
        => CornerTestSupport.Envelope(track, rider, state.Snapshot().SampleSurface(segmentIndex, rider.LateralPosition),
            segmentIndex, rider.SegmentProgress).SpeedMetersPerSecond(
                track.CornerTopology.Resolve(segmentIndex, rider.SegmentProgress, rider.LateralPosition, track.Geometry)!.Value.CornerProgress);

    private static TurnEntryScrubProfile ScrubProfile(
        Track track,
        TrackState state,
        RiderState rider,
        int segmentIndex,
        float availableDistance)
        => LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            rider.Speed,
            SettledSafeSpeed(track, state, rider, segmentIndex),
            ScrubDeceleration(state, rider, segmentIndex),
            availableDistance);

    private static SegmentResolution ApplyAdvancedConstraint(
        Track track,
        TrackState state,
        RiderState rider,
        int segmentIndex,
        float speed,
        int lane)
    {
        var surface = state.Snapshot().SampleSurface(segmentIndex, rider.LateralPosition);
        return SegmentPhysics.Apply(new SegmentPhysicsContext(
            track.Segments[segmentIndex],
            lane,
            speed,
            track.Geometry,
            surface,
            rider.Profile.Skills,
            rider.Morale,
            rider.ActiveSetup,
            DecisionRisk: 0f,
            LateralPosition: rider.LateralPosition,
            CornerPhase: track.CornerTopology.Resolve(segmentIndex, rider.SegmentProgress, rider.LateralPosition, track.Geometry)));
    }

    private static Track TrackOf(float straightLengthMeters, params SegmentType[] segmentTypes)
        => new(
            segmentTypes.Select((type, index) => new TrackSegment(index, type)).ToArray(),
            new TrackGeometry(
                straightLengthMeters,
                TrackGeometry.Default.InnerRadiusMeters,
                TrackGeometry.Default.StraightWidthMeters,
                TrackGeometry.Default.TurnWidthMeters,
                TrackGeometry.Default.TurnSegmentAngleRadians));

    private static TrackState UniformState(Track track, TrackSurfaceState surface)
        => new(track.Segments.Count, TrackSegment.LanesCount, (_, _) => surface);

    private static RiderState Rider(
        int riderId,
        int lane,
        float lateralPosition,
        float speed,
        float slideControl = 50f)
        => new(
            new RiderProfile(
                riderId,
                $"Rider {riderId}",
                new RiderSkills(50f, 50f, slideControl, 50f, 50f, 50f),
                RiderStyle.Balanced),
            lane)
        {
            LateralPosition = lateralPosition,
            Speed = speed,
            ActiveSetup = new BikeSetup(gearing: 0.5f, tractionBias: 0.5f),
        };

    private static RiderState RiderAt(
        int riderId,
        int lane,
        float lateralPosition,
        float speed,
        int segmentIndex,
        Track track,
        float segmentProgress = 0f)
    {
        var rider = Rider(riderId, lane, lateralPosition, speed);
        rider.RestorePosition(RiderPosition.Create(
            lapNumber: 1,
            segmentIndex,
            segmentProgress,
            track.Segments.Count));
        return rider;
    }

    private static RiderState Clone(RiderState rider, int riderId)
    {
        var clone = Rider(
            riderId,
            rider.Lane,
            rider.LateralPosition,
            rider.Speed,
            rider.Profile.Skills.SlideControl);
        if (rider.Position.SegmentCount > 0)
            clone.RestorePosition(rider.Position);
        clone.ElapsedTimeSeconds = rider.ElapsedTimeSeconds;
        return clone;
    }

    private static ResolvedResult ResolveSingle(
        Track track,
        TrackState state,
        RiderState rider,
        int targetLane,
        int segmentIndex = 0,
        bool useLegacyPhysics = false)
    {
        var resolved = Resolve(
            track,
            state,
            new[] { rider },
            new FixedTargetDecisionModel(targetLane),
            segmentIndex,
            useLegacyPhysics);
        return new ResolvedResult(Assert.Single(resolved.Changes), resolved.Snapshot, Assert.Single(resolved.Diagnostics));
    }

    private static ResolvedSimulationStep Resolve(
        Track track,
        TrackState state,
        IReadOnlyList<RiderState> riders,
        IRiderDecisionModel decisionModel,
        int segmentIndex = 0,
        bool useLegacyPhysics = false)
    {
        var engine = new SimulationEngine(decisionModel);
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 987,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };
        var snapshot = engine.CaptureSnapshot(
            track,
            state,
            riders,
            new SimulationStepContext(
                HeatId: 1,
                StepNumber: segmentIndex,
                LapIndex: 0,
                SegmentIndex: segmentIndex,
                Seed: options.Seed,
                RequiredLaps: 1,
                UseLegacyPhysics: useLegacyPhysics));
        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    private static Projection[] Project(ResolvedSimulationStep result)
        => result.Changes
            .OrderBy(change => change.RiderId)
            .Select(change => new Projection(
                change.RiderId,
                change.Outcome,
                change.PhysicsSpeed,
                change.Speed,
                change.ElapsedTimeSeconds,
                change.LateralPosition,
                change.Position.DistanceMeters,
                change.Lane))
            .ToArray();

    private sealed class FixedTargetDecisionModel(int targetLane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(targetLane, 0f);
    }

    private sealed class PerRiderTargetDecisionModel(IReadOnlyDictionary<int, int> targets)
        : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(targets[rider.RiderId], 0f);
    }

    private sealed record ResolvedResult(RiderStateChange Change, SimulationSnapshot Snapshot, RiderStepDiagnostics Diagnostics);

    private sealed record Projection(
        int RiderId,
        SegmentOutcome Outcome,
        float PhysicsSpeed,
        float Speed,
        float ElapsedTime,
        float LateralPosition,
        float DistanceMeters,
        int Lane);
}
