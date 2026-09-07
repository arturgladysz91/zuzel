using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class DistanceLimitedTurnExitDriveTests
{
    private static readonly TrackSurfaceState PerfectDriveSurface = new(1f, 0f, 0.35f);

    private sealed class FixedTargetDecisionModel(int targetLane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(targetLane, 0f);
    }

    private sealed class PerRiderTargetDecisionModel(IReadOnlyDictionary<int, int> targetLanes)
        : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(targetLanes[rider.RiderId], 0f);
    }

    [Fact]
    public void AdvancedTurnExitUsesSteppedForceBasedProfile()
    {
        const float entryPosition = 1.25f;
        const float entrySpeed = 10f;
        var result = ResolveSingle(
            SegmentType.TurnExit,
            Rider(1, lane: 1, entryPosition, entrySpeed),
            targetLane: 1);
        var travelled = LaneModel.SegmentLengthMeters(
            new TrackSegment(0, SegmentType.TurnExit),
            entryPosition,
            TrackGeometry.Default);
        var expected = LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
            entrySpeed,
            result.Snapshot.Rider(1).Profile.Skills,
            result.Snapshot.Rider(1).ActiveSetup,
            PerfectDriveSurface,
            travelled);

        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        Assert.Equal(entrySpeed, result.Change.PhysicsSpeed);
        Assert.True(result.Change.Speed > result.Change.PhysicsSpeed);
        Assert.Equal(expected.ExitSpeedMetersPerSecond, result.Change.Speed, 5);
        Assert.Equal(expected.TravelTimeSeconds, result.Change.ElapsedTimeSeconds, 5);
    }

    [Fact]
    public void ReferenceSpeedTurnExitPreservesPreviousAccelerationContract()
    {
        var rider = Rider(
            1,
            lane: 1,
            lateralPosition: 1f,
            speed: LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond);
        var result = ResolveSingle(SegmentType.TurnExit, rider, targetLane: 1);
        var travelled = LaneModel.SegmentLengthMeters(
            new TrackSegment(0, SegmentType.TurnExit),
            rider.LateralPosition,
            TrackGeometry.Default);
        var referenceAcceleration = LongitudinalDynamics
            .CalculateTurnExitAccelerationMetersPerSecondSquared(
                rider.Profile.Skills,
                rider.ActiveSetup,
                PerfectDriveSurface);
        var profile = LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
            result.Change.PhysicsSpeed,
            rider.Profile.Skills,
            rider.ActiveSetup,
            PerfectDriveSurface,
            travelled);

        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        Assert.Equal(
            LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond,
            result.Change.PhysicsSpeed);
        Assert.Equal(referenceAcceleration, profile.EntryNetAccelerationMetersPerSecondSquared, 6);
        Assert.Equal(profile.ExitSpeedMetersPerSecond, result.Change.Speed, 5);
    }

    [Fact]
    public void WiderPhysicalTurnExitGetsMoreDriveFromLongerPath()
    {
        const float entrySpeed = 10f;
        var inner = ResolveSingle(
            SegmentType.TurnExit,
            Rider(1, lane: 1, lateralPosition: 1f, entrySpeed),
            targetLane: 1);
        var outer = ResolveSingle(
            SegmentType.TurnExit,
            Rider(1, lane: 3, lateralPosition: 3f, entrySpeed),
            targetLane: 3);

        Assert.Equal(SegmentOutcome.Ok, inner.Change.Outcome);
        Assert.Equal(inner.Change.Outcome, outer.Change.Outcome);
        Assert.Equal(entrySpeed, inner.Change.PhysicsSpeed);
        Assert.Equal(inner.Change.PhysicsSpeed, outer.Change.PhysicsSpeed);
        Assert.True(outer.Change.Position.DistanceMeters > inner.Change.Position.DistanceMeters);
        Assert.True(outer.Change.Speed > inner.Change.Speed);
    }

    [Fact]
    public void TurnEntryDoesNotReceivePositiveDrive()
        => AssertDoesNotReceivePositiveTurnExitDrive(SegmentType.TurnEntry);

    [Fact]
    public void TurnMiddleDoesNotReceivePositiveDrive()
        => AssertDoesNotReceivePositiveTurnExitDrive(SegmentType.TurnMiddle);

    [Fact]
    public void AdvancedStraightUsesForceBasedProfile()
    {
        const float entrySpeed = 10f;
        var rider = Rider(1, lane: 1, lateralPosition: 1f, entrySpeed);
        var result = ResolveSingle(
            SegmentType.Straight,
            rider,
            targetLane: 1);
        var cornerEntryDeceleration = LongitudinalDynamics
            .CalculateCornerEntryDecelerationMetersPerSecondSquared(
                rider.Profile.Skills,
                PerfectDriveSurface);
        var expected = LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            entrySpeed,
            rider.Profile.Skills,
            rider.ActiveSetup,
            PerfectDriveSurface,
            cornerEntryDeceleration,
            TrackGeometry.Default.StraightLengthMeters);

        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        Assert.Equal(entrySpeed, result.Change.PhysicsSpeed);
        Assert.Equal(expected.ExitSpeedMetersPerSecond, result.Change.Speed, 5);
        Assert.Equal(expected.TravelTimeSeconds, result.Change.ElapsedTimeSeconds, 5);
    }

    private static void AssertDoesNotReceivePositiveTurnExitDrive(SegmentType segmentType)
    {
        const float entrySpeed = 10f;
        var result = ResolveSingle(
            segmentType,
            Rider(1, lane: 1, lateralPosition: 1f, entrySpeed),
            targetLane: 1);

        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        Assert.Equal(entrySpeed, result.Change.PhysicsSpeed);
        Assert.Equal(result.Change.PhysicsSpeed, result.Change.Speed);
    }

    [Fact]
    public void RunWideDoesNotReceiveTurnExitDrive()
    {
        const float entryPosition = 1f;
        var rider = Rider(1, lane: 1, entryPosition, speed: 0f);
        var maxSafeSpeed = MaxSafeSpeed(rider, entryPosition, PerfectDriveSurface);
        rider.Speed = maxSafeSpeed * 1.20f;

        var result = ResolveSingle(SegmentType.TurnExit, rider, targetLane: 1);

        Assert.Equal(SegmentOutcome.RunWide, result.Change.Outcome);
        Assert.Equal(2, result.Change.Lane);
        Assert.Equal(rider.Speed, result.Change.PhysicsSpeed);
        Assert.Equal(
            maxSafeSpeed + (rider.Speed - maxSafeSpeed)
            * SegmentPhysics.NeutralRunWideOverspeedRetention,
            result.Change.Speed,
            5);
    }

    [Fact]
    public void CrashRemainsZeroSpeed()
    {
        const float entryPosition = 1f;
        var rider = Rider(1, lane: 1, entryPosition, speed: 0f);
        var maxSafeSpeed = MaxSafeSpeed(rider, entryPosition, PerfectDriveSurface);
        rider.Speed = maxSafeSpeed * 1.30f;

        var result = ResolveSingle(SegmentType.TurnExit, rider, targetLane: 1);

        Assert.Equal(SegmentOutcome.Crash, result.Change.Outcome);
        Assert.Equal(0f, result.Change.PhysicsSpeed);
        Assert.Equal(0f, result.Change.Speed);
    }

    [Fact]
    public void BrakeCanRecoverSpeedOnTurnExit()
    {
        const float entryPosition = 1f;
        var rider = Rider(1, lane: 1, entryPosition, speed: 0f);
        var maxSafeSpeed = MaxSafeSpeed(rider, entryPosition, PerfectDriveSurface);
        rider.Speed = maxSafeSpeed * 1.05f;

        var result = ResolveSingle(SegmentType.TurnExit, rider, targetLane: 1);
        var travelled = LaneModel.SegmentLengthMeters(
            new TrackSegment(0, SegmentType.TurnExit),
            entryPosition,
            TrackGeometry.Default);
        var correction = LongitudinalDynamics.CalculateCornerSpeedCorrectionProfile(
            rider.Speed,
            maxSafeSpeed,
            LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
                rider.Profile.Skills,
                PerfectDriveSurface),
            travelled);
        var expected = LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
            correction.ExitSpeedMetersPerSecond,
            rider.Profile.Skills,
            rider.ActiveSetup,
            PerfectDriveSurface,
            correction.RemainingDistanceMeters);

        Assert.Equal(SegmentOutcome.Brake, result.Change.Outcome);
        Assert.Equal(rider.Speed, result.Change.PhysicsSpeed, 5);
        Assert.True(correction.TargetReached);
        Assert.True(result.Change.Speed > maxSafeSpeed);
        Assert.Equal(expected.ExitSpeedMetersPerSecond, result.Change.Speed, 5);
    }

    [Fact]
    public void EntrySurfaceControlsTurnExitAcceleration()
    {
        var noEffectiveGrip = new TrackSurfaceState(0f, 0f, 0.35f);
        var rider = Rider(1, lane: 1, lateralPosition: 1.5f, speed: 10f);

        var poor = ResolveSingle(
            SegmentType.TurnExit,
            Clone(rider),
            targetLane: 1,
            surface: noEffectiveGrip);
        var good = ResolveSingle(
            SegmentType.TurnExit,
            Clone(rider),
            targetLane: 1,
            surface: PerfectDriveSurface);

        Assert.Equal(10f, poor.Change.PhysicsSpeed);
        Assert.Equal(poor.Change.PhysicsSpeed, good.Change.PhysicsSpeed);
        Assert.True(good.Change.Speed > poor.Change.Speed);
    }

    [Fact]
    public void MoraleDoesNotChangeTurnExitDrive()
    {
        var lowMorale = ResolveSingle(
            SegmentType.TurnExit,
            Rider(1, lane: 1, lateralPosition: 1f, speed: 10f, morale: 0f),
            targetLane: 1);
        var highMorale = ResolveSingle(
            SegmentType.TurnExit,
            Rider(1, lane: 1, lateralPosition: 1f, speed: 10f, morale: 1f),
            targetLane: 1);

        Assert.Equal(lowMorale.Change.Outcome, highMorale.Change.Outcome);
        Assert.Equal(lowMorale.Change.PhysicsSpeed, highMorale.Change.PhysicsSpeed);
        Assert.Equal(lowMorale.Change.Speed, highMorale.Change.Speed);
        Assert.Equal(lowMorale.Change.ElapsedTimeSeconds, highMorale.Change.ElapsedTimeSeconds);
        Assert.Equal(lowMorale.Change.LateralPosition, highMorale.Change.LateralPosition);
    }

    [Fact]
    public void ExistingOverspeedAboveEquilibriumNaturallyDeceleratesWithoutTeleport()
    {
        var geometry = new TrackGeometry(
            straightLengthMeters: TrackGeometry.Default.StraightLengthMeters,
            innerRadiusMeters: 200f,
            straightWidthMeters: TrackGeometry.Default.StraightWidthMeters,
            turnWidthMeters: TrackGeometry.Default.TurnWidthMeters,
            turnSegmentAngleRadians: TrackGeometry.Default.TurnSegmentAngleRadians);
        var rider = Rider(
            1,
            lane: 4,
            lateralPosition: 4f,
            speed: 0f,
            speedSkill: 50f,
            gearing: 0f);
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            rider.LateralPosition,
            geometry,
            PerfectDriveSurface,
            rider.Profile.Skills,
            rider.ActiveSetup);
        var equilibrium = LongitudinalDynamics.CalculateFullDriveEquilibriumSpeedMetersPerSecond(
            LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
                rider.Profile.Skills, rider.ActiveSetup, PerfectDriveSurface), rider.ActiveSetup);
        rider.Speed = equilibrium + 5f;

        var result = ResolveSingle(
            SegmentType.TurnExit,
            rider,
            targetLane: 4,
            geometry: geometry);

        Assert.True(rider.Speed > equilibrium);
        Assert.True(rider.Speed < maxSafeSpeed);
        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        Assert.Equal(rider.Speed, result.Change.PhysicsSpeed);
        Assert.True(result.Change.Speed < rider.Speed);
        Assert.True(result.Change.Speed > equilibrium);
    }

    [Fact]
    public void LegacyTurnExitDoesNotUseDistanceLimitedDrive()
    {
        var result = ResolveSingle(
            SegmentType.TurnExit,
            Rider(1, lane: 1, lateralPosition: 1f, speed: 10f),
            targetLane: 1,
            useLegacyPhysics: true);

        Assert.Equal(SegmentOutcome.Ok, result.Change.Outcome);
        Assert.Equal(10f, result.Change.PhysicsSpeed);
        Assert.Equal(result.Change.PhysicsSpeed, result.Change.Speed);
    }

    [Fact]
    public void TurnExitDriveIsIndependentOfRiderCollectionOrder()
    {
        var forward = ResolveMany(reverseRiders: false);
        var reversed = ResolveMany(reverseRiders: true);

        Assert.Equal(Project(forward), Project(reversed));
        Assert.Equal(forward.Events, reversed.Events);
    }

    private static ResolvedResult ResolveSingle(
        SegmentType segmentType,
        RiderState rider,
        int targetLane,
        TrackSurfaceState? surface = null,
        bool useLegacyPhysics = false,
        TrackGeometry? geometry = null)
    {
        var resolved = Resolve(
            segmentType,
            new[] { rider },
            new FixedTargetDecisionModel(targetLane),
            surface ?? PerfectDriveSurface,
            useLegacyPhysics,
            geometry);
        return new ResolvedResult(Assert.Single(resolved.Changes), resolved.Snapshot);
    }

    private static ResolvedSimulationStep ResolveMany(bool reverseRiders)
    {
        var riders = new[]
        {
            Rider(1, lane: 0, lateralPosition: 0f, speed: 10f, speedSkill: 0f),
            Rider(2, lane: 4, lateralPosition: 4f, speed: 10f, speedSkill: 100f),
        };
        if (reverseRiders)
            Array.Reverse(riders);

        return Resolve(
            SegmentType.TurnExit,
            riders,
            new PerRiderTargetDecisionModel(new Dictionary<int, int> { [1] = 0, [2] = 4 }),
            PerfectDriveSurface,
            useLegacyPhysics: false);
    }

    private static ResolvedSimulationStep Resolve(
        SegmentType segmentType,
        IReadOnlyList<RiderState> riders,
        IRiderDecisionModel decisionModel,
        TrackSurfaceState surface,
        bool useLegacyPhysics,
        TrackGeometry? geometry = null)
    {
        var track = new Track(
            new[] { new TrackSegment(0, segmentType) },
            geometry ?? TrackGeometry.Default);
        var trackState = TrackState.CreateDefault(track, surface);
        var engine = new SimulationEngine(decisionModel);
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 246,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };
        var snapshot = engine.CaptureSnapshot(
            track,
            trackState,
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

    private static RiderState Rider(
        int riderId,
        int lane,
        float lateralPosition,
        float speed,
        float speedSkill = 50f,
        float gearing = 0.5f,
        float morale = 0.5f)
    {
        var rider = new RiderState(
            new RiderProfile(
                riderId,
                $"Rider {riderId}",
                new RiderSkills(50f, speedSkill, 50f, 50f, 50f, 50f),
                RiderStyle.Balanced),
            lane,
            morale)
        {
            LateralPosition = lateralPosition,
            Speed = speed,
            ActiveSetup = new BikeSetup(gearing, tractionBias: 0.5f),
        };
        return rider;
    }

    private static RiderState Clone(RiderState source)
        => Rider(
            source.RiderId,
            source.Lane,
            source.LateralPosition,
            source.Speed,
            source.Profile.Skills.Speed,
            source.ActiveSetup.Gearing,
            source.Morale);

    private static float MaxSafeSpeed(
        RiderState rider,
        float lateralPosition,
        TrackSurfaceState surface)
        => SegmentPhysics.MaxSafeTurnSpeed(
            lateralPosition,
            TrackGeometry.Default,
            surface,
            rider.Profile.Skills,
            rider.ActiveSetup);

    private static DriveProjection[] Project(ResolvedSimulationStep resolved)
        => resolved.Changes
            .OrderBy(change => change.RiderId)
            .Select(change => new DriveProjection(
                change.RiderId,
                change.Outcome,
                change.PhysicsSpeed,
                change.Speed,
                change.ElapsedTimeSeconds,
                change.LateralPosition,
                change.Position.DistanceMeters,
                change.Lane))
            .ToArray();

    private sealed record ResolvedResult(RiderStateChange Change, SimulationSnapshot Snapshot);

    private sealed record DriveProjection(
        int RiderId,
        SegmentOutcome Outcome,
        float PhysicsSpeed,
        float Speed,
        float ElapsedTimeSeconds,
        float LateralPosition,
        float DistanceMeters,
        int Lane);
}
