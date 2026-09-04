using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class StraightSpeedProfileTests
{
    private static readonly TrackSurfaceState PerfectSurface = new(1f, 0f, 0.35f);
    private static readonly TrackSurfaceState PoorSurface = new(0.35f, 0.65f, 0.80f);

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

    [Fact]
    public void AdvancedStraightAccelerates()
    {
        var track = TrackOf(60f, SegmentType.Straight);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 10f);

        var result = ResolveSingle(track, UniformState(track, PerfectSurface), rider, targetLane: 1);

        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        Assert.Equal(10f, result.Change.PhysicsSpeed);
        Assert.True(result.Change.Speed > result.Change.PhysicsSpeed);
    }

    [Fact]
    public void StraightTargetsRecoverableTurnEntryApproachInsteadOfSettledSafeSpeed()
    {
        var track = TrackOf(100f, SegmentType.Straight, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 10f);
        var settledSafeSpeed = NextTurnSettledSafeSpeed(track, state, rider, nextSegmentIndex: 1);
        var expectedTarget = NextTurnApproachSpeed(track, state, rider, nextSegmentIndex: 1);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.Equal(10f, result.Change.PhysicsSpeed);
        Assert.True(expectedTarget > settledSafeSpeed);
        Assert.True(result.Change.Speed > settledSafeSpeed);
        Assert.Equal(expectedTarget, result.Change.Speed, 5);
    }

    [Fact]
    public void StraightLookaheadDoesNotSkipAnIntermediateSegment()
    {
        var track = TrackOf(
            100f,
            SegmentType.Straight,
            SegmentType.Straight,
            SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 10f);
        var expected = ProfileWithoutTurnTarget(
            rider,
            PerfectSurface,
            track.Geometry.StraightLengthMeters);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.Equal(expected.ExitSpeedMetersPerSecond, result.Change.Speed, 5);
    }

    [Fact]
    public void WorseNextTurnSurfaceLowersStraightExitTarget()
    {
        var track = TrackOf(100f, SegmentType.Straight, SegmentType.TurnEntry);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 10f);
        var goodState = StateWithNextTurnSurface(track, PerfectSurface);
        var poorState = StateWithNextTurnSurface(track, PoorSurface);

        var good = ResolveSingle(track, goodState, Clone(rider), targetLane: 1);
        var poor = ResolveSingle(track, poorState, Clone(rider), targetLane: 1);
        var goodTarget = NextTurnApproachSpeed(track, goodState, rider, nextSegmentIndex: 1);
        var poorTarget = NextTurnApproachSpeed(track, poorState, rider, nextSegmentIndex: 1);

        Assert.Equal(goodTarget, good.Change.Speed, 5);
        Assert.Equal(poorTarget, poor.Change.Speed, 5);
        Assert.True(poorTarget < goodTarget);
        Assert.True(poor.Change.Speed < good.Change.Speed);
    }

    [Fact]
    public void LookaheadUsesPhysicalEntryPositionNotTargetLane()
    {
        const float entryPosition = 1.25f;
        var track = TrackOf(100f, SegmentType.Straight, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var inward = ResolveSingle(
            track,
            state,
            Rider(1, lane: 1, entryPosition, speed: 10f),
            targetLane: 0);
        var outward = ResolveSingle(
            track,
            state,
            Rider(1, lane: 1, entryPosition, speed: 10f),
            targetLane: 4);

        Assert.NotEqual(inward.Change.PlannedLane, outward.Change.PlannedLane);
        Assert.Equal(inward.Change.PhysicsSpeed, outward.Change.PhysicsSpeed);
        Assert.Equal(inward.Change.Speed, outward.Change.Speed, 5);
    }

    [Fact]
    public void InsufficientStraightDistanceLeavesOverspeedForNextTurn()
    {
        var track = TrackOf(5f, SegmentType.Straight, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 20f);
        var target = NextTurnApproachSpeed(track, state, rider, nextSegmentIndex: 1);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.True(result.Change.Speed > target);
        Assert.True(result.Change.Speed < result.Change.PhysicsSpeed);
    }

    [Fact]
    public void NonFinalLastStraightWrapsLookaheadToSegmentZero()
    {
        var track = StandardOrderTrack(100f);
        var state = UniformState(track, PerfectSurface);
        var rider = RiderAt(1, lane: 1, lateralPosition: 1f, speed: 10f, lapNumber: 1, segmentIndex: 3, track);
        var target = NextTurnApproachSpeed(track, state, rider, nextSegmentIndex: 0);

        var result = ResolveSingle(
            track,
            state,
            rider,
            targetLane: 1,
            lapIndex: 0,
            segmentIndex: 3,
            requiredLaps: 2);

        Assert.Equal(target, result.Change.Speed, 5);
        Assert.Equal(RiderRaceStatus.Racing, result.Change.Status);
    }

    [Fact]
    public void FinalRaceStraightDoesNotDecelerateForNonexistentNextLap()
    {
        var track = StandardOrderTrack(100f);
        var state = UniformState(track, PerfectSurface);
        var nonFinalRider = RiderAt(
            1, lane: 1, lateralPosition: 1f, speed: 10f, lapNumber: 1, segmentIndex: 3, track);
        var finalRider = RiderAt(
            1, lane: 1, lateralPosition: 1f, speed: 10f, lapNumber: 2, segmentIndex: 3, track);

        var nonFinal = ResolveSingle(
            track, state, nonFinalRider, 1, lapIndex: 0, segmentIndex: 3, requiredLaps: 2);
        var final = ResolveSingle(
            track, state, finalRider, 1, lapIndex: 1, segmentIndex: 3, requiredLaps: 2);

        Assert.True(final.Change.Speed > nonFinal.Change.Speed);
        Assert.Equal(RiderRaceStatus.Racing, nonFinal.Change.Status);
        Assert.Equal(RiderRaceStatus.Finished, final.Change.Status);
    }

    [Fact]
    public void PartialStraightUsesOnlyRemainingDistance()
    {
        var track = TrackOf(100f, SegmentType.Straight, SegmentType.Straight);
        var state = UniformState(track, PerfectSurface);
        var fullRider = RiderAt(
            1, lane: 1, lateralPosition: 1f, speed: 10f, lapNumber: 1, segmentIndex: 0, track);
        var partialRider = RiderAt(
            1, lane: 1, lateralPosition: 1f, speed: 10f, lapNumber: 1, segmentIndex: 0, track, segmentProgress: 0.5f);

        var full = ResolveSingle(track, state, fullRider, 1, requiredLaps: 1);
        var partial = ResolveSingle(track, state, partialRider, 1, requiredLaps: 1);

        Assert.Equal(100f, full.Change.Position.DistanceMeters, 5);
        Assert.Equal(50f, partial.Change.Position.DistanceMeters, 5);
        Assert.True(partial.Change.Speed < full.Change.Speed);
        Assert.True(partial.Change.ElapsedTimeSeconds < full.Change.ElapsedTimeSeconds);
    }

    [Fact]
    public void StraightTravelTimeComesFromProfileNotEndpointAverage()
    {
        var track = TrackOf(100f, SegmentType.Straight, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 0f);
        var target = NextTurnApproachSpeed(track, state, rider, nextSegmentIndex: 1);
        rider.Speed = target;
        var deceleration = LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(
            rider.Profile.Skills,
            PerfectSurface);
        var expectedProfile = LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            target,
            rider.Profile.Skills,
            rider.ActiveSetup,
            PerfectSurface,
            deceleration,
            track.Geometry.StraightLengthMeters,
            target);

        var result = ResolveSingle(track, state, rider, targetLane: 1);
        var endpointAverageTime = track.Geometry.StraightLengthMeters
            / ((result.Change.EntrySpeed + result.Change.Speed) * 0.5f);

        Assert.True(expectedProfile.PeakSpeedMetersPerSecond > target);
        Assert.Equal(expectedProfile.TravelTimeSeconds, result.Change.ElapsedTimeSeconds, 5);
        Assert.NotEqual(endpointAverageTime, result.Change.ElapsedTimeSeconds, 4);
    }

    [Fact]
    public void StraightProfileTimeControlsLateralMovementBudget()
    {
        const float entryPosition = 1f;
        var geometry = new TrackGeometry(100f, 24f, 4f, TrackGeometry.Default.TurnSegmentAngleRadians);
        var track = new Track(
            new[]
            {
                new TrackSegment(0, SegmentType.Straight),
                new TrackSegment(1, SegmentType.TurnEntry),
            },
            geometry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, entryPosition, speed: 10f);
        var target = NextTurnApproachSpeed(track, state, rider, nextSegmentIndex: 1);
        var deceleration = LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(
            rider.Profile.Skills,
            PerfectSurface);
        var profile = LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            rider.Speed,
            rider.Profile.Skills,
            rider.ActiveSetup,
            PerfectSurface,
            deceleration,
            geometry.StraightLengthMeters,
            target);
        var expectedLateralPosition = LateralMovementModel.MoveTowards(
            entryPosition,
            resolvedLane: 2,
            profile.TravelTimeSeconds,
            geometry,
            PerfectSurface,
            rider.Profile.Skills);

        var result = ResolveSingle(track, state, rider, targetLane: 4);

        Assert.Equal(2, result.Change.PlannedLane);
        Assert.Equal(profile.TravelTimeSeconds, result.Change.ElapsedTimeSeconds, 5);
        Assert.Equal(expectedLateralPosition, result.Change.LateralPosition, 5);
    }

    [Fact]
    public void AdvancedStraightConvergesToForceEquilibriumBeyondFormerCeiling()
    {
        var track = TrackOf(10_000f, SegmentType.Straight);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 10f);
        var equilibrium = Equilibrium(rider);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.InRange(Math.Abs(equilibrium - result.Change.Speed), 0f, 0.001f);
        Assert.True(result.Change.Speed > 23f);
    }

    [Fact]
    public void LongFinalStraightConvergesTowardEquilibrium()
    {
        var track = StandardOrderTrack(10_000f);
        var state = UniformState(track, PerfectSurface);
        var rider = RiderAt(
            1, lane: 1, lateralPosition: 1f, speed: 10f, lapNumber: 1, segmentIndex: 3, track);
        var deceleration = LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(
            rider.Profile.Skills,
            PerfectSurface);
        var expected = LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            rider.Speed,
            rider.Profile.Skills,
            rider.ActiveSetup,
            PerfectSurface,
            deceleration,
            track.Geometry.StraightLengthMeters);

        var result = ResolveSingle(
            track,
            state,
            rider,
            targetLane: 1,
            lapIndex: 0,
            segmentIndex: 3,
            requiredLaps: 1);

        Assert.True(expected.CruiseDistanceMeters > 0f);
        Assert.Equal(expected.ExitSpeedMetersPerSecond, result.Change.Speed, 5);
        Assert.Equal(expected.TravelTimeSeconds, result.Change.ElapsedTimeSeconds, 5);
        Assert.Equal(RiderRaceStatus.Finished, result.Change.Status);
    }

    [Fact]
    public void HigherGearingCanReachHigherStraightSpeedOnLongEnoughStraight()
    {
        var track = TrackOf(10_000f, SegmentType.Straight);
        var state = UniformState(track, PerfectSurface);
        var lowGearing = ResolveSingle(
            track,
            state,
            Rider(1, 1, 1f, 10f, gearing: 0f),
            targetLane: 1);
        var highGearing = ResolveSingle(
            track,
            state,
            Rider(2, 1, 1f, 10f, gearing: 1f),
            targetLane: 1);

        Assert.True(highGearing.Change.Speed > lowGearing.Change.Speed);
    }

    [Fact]
    public void ShortStraightRewardsDriveOrientedGearing()
    {
        var track = TrackOf(10f, SegmentType.Straight);
        var state = UniformState(track, PerfectSurface);
        var lowGearing = ResolveSingle(
            track,
            state,
            Rider(1, 1, 1f, 10f, gearing: 0f),
            targetLane: 1);
        var highGearing = ResolveSingle(
            track,
            state,
            Rider(2, 1, 1f, 10f, gearing: 1f),
            targetLane: 1);

        Assert.True(lowGearing.Change.Speed > highGearing.Change.Speed);
    }

    [Fact]
    public void LowGearingStillHasMoreTurnExitDriveBelowEquilibrium()
    {
        var track = TrackOf(60f, SegmentType.TurnExit);
        var state = UniformState(track, PerfectSurface);
        var lowGearing = ResolveSingle(
            track,
            state,
            Rider(1, 1, 1f, 10f, gearing: 0f),
            targetLane: 1);
        var highGearing = ResolveSingle(
            track,
            state,
            Rider(2, 1, 1f, 10f, gearing: 1f),
            targetLane: 1);

        Assert.Equal(SegmentOutcome.Ok, lowGearing.Change.Outcome);
        Assert.Equal(SegmentOutcome.Ok, highGearing.Change.Outcome);
        Assert.True(lowGearing.Change.Speed > highGearing.Change.Speed);
        Assert.True(lowGearing.Change.Speed < Equilibrium(speedSkill: 50f, gearing: 0f));
        Assert.True(highGearing.Change.Speed < Equilibrium(speedSkill: 50f, gearing: 1f));
    }

    [Fact]
    public void TurnExitDriveConvergesBeyondFormerCeiling()
    {
        var geometry = new TrackGeometry(
            straightLengthMeters: 60f,
            innerRadiusMeters: 24f,
            laneSpacingMeters: 1f,
            turnSegmentAngleRadians: 200f);
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnExit) }, geometry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 10f);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        var profile = LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
            rider.Speed,
            rider.Profile.Skills,
            rider.ActiveSetup,
            PerfectSurface,
            LaneModel.SegmentLengthMeters(track.Segments[0], rider.LateralPosition, geometry));
        Assert.Equal(profile.ExitSpeedMetersPerSecond, result.Change.Speed, 5);
        Assert.True(result.Change.Speed > 23f);
        Assert.InRange(Math.Abs(result.Change.Speed - profile.FullDriveEquilibriumSpeedMetersPerSecond), 0f, 0.001f);
    }

    [Fact]
    public void TurnExitSignedDriveNaturallyReducesExistingOverspeed()
    {
        var geometry = new TrackGeometry(60f, 100f, 1f, 1f);
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnExit) }, geometry);
        var rider = Rider(1, 1, 1f, 0f);
        var equilibrium = Equilibrium(rider, turnExit: true);
        rider.Speed = equilibrium + 2f;
        var result = ResolveSingle(track, UniformState(track, PerfectSurface), rider, targetLane: 1);
        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        Assert.Equal(rider.Speed, result.Change.PhysicsSpeed);
        Assert.True(result.Change.Speed < rider.Speed);
        Assert.True(result.Change.Speed > equilibrium);
    }

    [Fact]
    public void NextTurnTargetBelowEquilibriumStillControlsStraightExit()
    {
        var track = TrackOf(100f, SegmentType.Straight, SegmentType.TurnEntry);
        var state = UniformState(track, PerfectSurface);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 10f);
        var target = NextTurnApproachSpeed(track, state, rider, nextSegmentIndex: 1);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.True(target < Equilibrium(rider));
        Assert.Equal(target, result.Change.Speed, 5);
    }

    [Fact]
    public void WorseSurfaceLowersEffectiveForceEquilibrium()
    {
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 10f);
        var good = ProfileWithoutTurnTarget(rider, PerfectSurface, 10_000f);
        var poor = ProfileWithoutTurnTarget(rider, PoorSurface, 10_000f);
        Assert.True(good.FullDriveEquilibriumSpeedMetersPerSecond > poor.FullDriveEquilibriumSpeedMetersPerSecond);
        Assert.InRange(Math.Abs(good.ExitSpeedMetersPerSecond - good.FullDriveEquilibriumSpeedMetersPerSecond!.Value), 0f, 0.001f);
        Assert.InRange(Math.Abs(poor.ExitSpeedMetersPerSecond - poor.FullDriveEquilibriumSpeedMetersPerSecond!.Value), 0f, 0.001f);
        Assert.True(poor.TravelTimeSeconds > good.TravelTimeSeconds);
    }

    [Fact]
    public void PartialStraightIntegratesForcesOverRemainingDistanceOnly()
    {
        var track = TrackOf(400f, SegmentType.Straight, SegmentType.Straight);
        var state = UniformState(track, PerfectSurface);
        var rider = RiderAt(
            1,
            lane: 1,
            lateralPosition: 1f,
            speed: 10f,
            lapNumber: 1,
            segmentIndex: 0,
            track,
            segmentProgress: 0.5f);
        var expected = ProfileWithoutTurnTarget(rider, PerfectSurface, distanceMeters: 200f);

        var result = ResolveSingle(track, state, rider, targetLane: 1);

        Assert.Equal(200f, result.Change.Position.DistanceMeters, 5);
        Assert.Equal(expected.ExitSpeedMetersPerSecond, result.Change.Speed, 5);
        Assert.Equal(expected.TravelTimeSeconds, result.Change.ElapsedTimeSeconds, 5);
    }

    [Fact]
    public void LegacyStraightIgnoresForceEquilibrium()
    {
        var track = TrackOf(60f, SegmentType.Straight);
        var rider = Rider(1, lane: 1, lateralPosition: 1f, speed: 30f);

        var result = ResolveSingle(
            track,
            UniformState(track, PerfectSurface),
            rider,
            targetLane: 1,
            useLegacyPhysics: true);

        Assert.NotEqual(rider.Speed, Equilibrium(rider));
        Assert.Equal(30f, result.Change.PhysicsSpeed);
        Assert.Equal(result.Change.PhysicsSpeed, result.Change.Speed);
        Assert.Equal(2f, result.Change.ElapsedTimeSeconds, 5);
        Assert.Equal(60f, result.Change.Position.DistanceMeters, 5);
    }

    [Fact]
    public void StraightForceTraversalIsIndependentOfRiderCollectionOrder()
    {
        var track = TrackOf(500f, SegmentType.Straight);
        var state = UniformState(track, PerfectSurface);
        var riders = new[]
        {
            Rider(1, lane: 0, lateralPosition: 0f, speed: 10f, speedSkill: 20f, slideControl: 80f, gearing: 0f),
            Rider(2, lane: 4, lateralPosition: 4f, speed: 12f, speedSkill: 80f, slideControl: 20f, gearing: 1f),
        };
        var targets = new Dictionary<int, int> { [1] = 0, [2] = 4 };

        var forward = Resolve(
            track, state, riders, new PerRiderTargetDecisionModel(targets));
        var reversed = Resolve(
            track, state, riders.Reverse().ToArray(), new PerRiderTargetDecisionModel(targets));

        Assert.Equal(Project(forward), Project(reversed));
        Assert.Equal(forward.Events, reversed.Events);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void FinalStraightBehaviorIsGenericAcrossRequiredLapCount(int requiredLaps)
    {
        var track = StandardOrderTrack(75f);
        var state = UniformState(track, PerfectSurface);
        var rider = RiderAt(
            1,
            lane: 1,
            lateralPosition: 1f,
            speed: 10f,
            lapNumber: requiredLaps,
            segmentIndex: 3,
            track);
        var expected = ProfileWithoutTurnTarget(
            rider,
            PerfectSurface,
            track.Geometry.StraightLengthMeters);

        var result = ResolveSingle(
            track,
            state,
            rider,
            targetLane: 1,
            lapIndex: requiredLaps - 1,
            segmentIndex: 3,
            requiredLaps);

        Assert.Equal(expected.ExitSpeedMetersPerSecond, result.Change.Speed, 5);
        Assert.Equal(RiderRaceStatus.Finished, result.Change.Status);
    }

    private static Track TrackOf(float straightLengthMeters, params SegmentType[] segmentTypes)
        => new(
            segmentTypes.Select((type, index) => new TrackSegment(index, type)).ToArray(),
            new TrackGeometry(
                straightLengthMeters,
                TrackGeometry.Default.InnerRadiusMeters,
                TrackGeometry.Default.LaneSpacingMeters,
                TrackGeometry.Default.TurnSegmentAngleRadians));

    private static Track StandardOrderTrack(float straightLengthMeters)
        => TrackOf(
            straightLengthMeters,
            SegmentType.TurnEntry,
            SegmentType.TurnMiddle,
            SegmentType.TurnExit,
            SegmentType.Straight);

    private static TrackState UniformState(Track track, TrackSurfaceState surface)
        => new(track.Segments.Count, TrackSegment.LanesCount, (_, _) => surface);

    private static TrackState StateWithNextTurnSurface(Track track, TrackSurfaceState nextTurnSurface)
        => new(
            track.Segments.Count,
            TrackSegment.LanesCount,
            (segmentIndex, _) => segmentIndex == 1 ? nextTurnSurface : PerfectSurface);

    private static float NextTurnSettledSafeSpeed(
        Track track,
        TrackState state,
        RiderState rider,
        int nextSegmentIndex)
        => SegmentPhysics.MaxSafeTurnSpeed(
            rider.LateralPosition,
            track.Geometry,
            state.Snapshot().SampleSurface(nextSegmentIndex, rider.LateralPosition),
            rider.Profile.Skills,
            rider.ActiveSetup);

    private static float NextTurnApproachSpeed(
        Track track,
        TrackState state,
        RiderState rider,
        int nextSegmentIndex)
    {
        var surface = state.Snapshot().SampleSurface(nextSegmentIndex, rider.LateralPosition);
        var settledSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            rider.LateralPosition,
            track.Geometry,
            surface,
            rider.Profile.Skills,
            rider.ActiveSetup);
        var scrubDeceleration = LongitudinalDynamics
            .CalculateCornerEntryDecelerationMetersPerSecondSquared(
                rider.Profile.Skills,
                surface);
        var turnEntryLength = LaneModel.SegmentLengthMeters(
            track.Segments[nextSegmentIndex],
            rider.LateralPosition,
            track.Geometry);
        return LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
            settledSafeSpeed,
            scrubDeceleration,
            turnEntryLength);
    }

    private static float Equilibrium(RiderState rider, bool turnExit = false)
        => Equilibrium(rider.Profile.Skills.Speed, rider.ActiveSetup.Gearing, turnExit);

    private static float Equilibrium(float speedSkill, float gearing, bool turnExit = false)
    {
        var skills = new RiderSkills(50f, speedSkill, 50f, 50f, 50f, 50f);
        var setup = new BikeSetup(gearing, 0.5f);
        var force = turnExit
            ? LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(skills, setup, PerfectSurface)
            : LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(skills, setup, PerfectSurface);
        return LongitudinalDynamics.CalculateFullDriveEquilibriumSpeedMetersPerSecond(force, setup);
    }

    private static StraightSpeedProfile ProfileWithoutTurnTarget(
        RiderState rider,
        TrackSurfaceState surface,
        float distanceMeters)
        => LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            rider.Speed,
            rider.Profile.Skills,
            rider.ActiveSetup,
            surface,
            LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(
                rider.Profile.Skills,
                surface),
            distanceMeters);

    private static RiderState Rider(
        int riderId,
        int lane,
        float lateralPosition,
        float speed,
        float speedSkill = 50f,
        float slideControl = 50f,
        float gearing = 0.5f)
        => new(
            new RiderProfile(
                riderId,
                $"Rider {riderId}",
                new RiderSkills(50f, speedSkill, slideControl, 50f, 50f, 50f),
                RiderStyle.Balanced),
            lane)
        {
            LateralPosition = lateralPosition,
            Speed = speed,
            ActiveSetup = new BikeSetup(gearing, tractionBias: 0.5f),
        };

    private static RiderState RiderAt(
        int riderId,
        int lane,
        float lateralPosition,
        float speed,
        int lapNumber,
        int segmentIndex,
        Track track,
        float segmentProgress = 0f)
    {
        var rider = Rider(riderId, lane, lateralPosition, speed);
        rider.RestorePosition(RiderPosition.Create(
            lapNumber,
            segmentIndex,
            segmentProgress,
            track.Segments.Count));
        return rider;
    }

    private static RiderState Clone(RiderState rider)
        => Rider(
            rider.RiderId,
            rider.Lane,
            rider.LateralPosition,
            rider.Speed,
            rider.Profile.Skills.Speed,
            rider.Profile.Skills.SlideControl,
            rider.ActiveSetup.Gearing);

    private static ResolvedResult ResolveSingle(
        Track track,
        TrackState state,
        RiderState rider,
        int targetLane,
        int lapIndex = 0,
        int segmentIndex = 0,
        int requiredLaps = 1,
        bool useLegacyPhysics = false)
    {
        var resolved = Resolve(
            track,
            state,
            new[] { rider },
            new FixedTargetDecisionModel(targetLane),
            lapIndex,
            segmentIndex,
            requiredLaps,
            useLegacyPhysics);
        return new ResolvedResult(Assert.Single(resolved.Changes), resolved.Snapshot);
    }

    private static ResolvedSimulationStep Resolve(
        Track track,
        TrackState state,
        IReadOnlyList<RiderState> riders,
        IRiderDecisionModel decisionModel,
        int lapIndex = 0,
        int segmentIndex = 0,
        int requiredLaps = 1,
        bool useLegacyPhysics = false)
    {
        var engine = new SimulationEngine(decisionModel);
        var options = new HeatSimulationOptions
        {
            Laps = requiredLaps,
            Seed = 321,
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
                LapIndex: lapIndex,
                SegmentIndex: segmentIndex,
                Seed: options.Seed,
                RequiredLaps: requiredLaps,
                UseLegacyPhysics: useLegacyPhysics));
        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    private static ProfileProjection[] Project(ResolvedSimulationStep result)
        => result.Changes
            .OrderBy(change => change.RiderId)
            .Select(change => new ProfileProjection(
                change.RiderId,
                change.Outcome,
                change.PhysicsSpeed,
                change.Speed,
                change.ElapsedTimeSeconds,
                change.LateralPosition,
                change.Position.DistanceMeters))
            .ToArray();

    private sealed record ResolvedResult(RiderStateChange Change, SimulationSnapshot Snapshot);

    private sealed record ProfileProjection(
        int RiderId,
        SegmentOutcome Outcome,
        float PhysicsSpeed,
        float Speed,
        float ElapsedTimeSeconds,
        float LateralPosition,
        float DistanceMeters);
}
