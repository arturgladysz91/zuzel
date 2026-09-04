using CoreSim;
using CoreSim.Race;
using Xunit;
using static CoreSim.Tests.StandingStartFixture;

namespace CoreSim.Tests;

public sealed class StandingStartSimulationTests
{
    [Fact]
    public void StandingStartEligibleRiderBeginsAtExactlyZeroSpeed()
    {
        var step = Resolve();
        var before = Assert.Single(step.Snapshot.Riders);
        var change = Assert.Single(step.Changes);
        Assert.Equal(RiderRaceStatus.NotStarted, before.Status);
        Assert.Equal(0d, before.CanonicalProgress);
        Assert.Equal(0f, before.DistanceMeters);
        Assert.Equal(0f, change.EntrySpeed);
        Assert.Equal(0f, change.PhysicsSpeed);
        Assert.Equal(RiderRaceStatus.Racing, change.Status);
        Assert.Equal(Launch(step).ExitSpeedMetersPerSecond, change.Speed);
    }

    [Fact]
    public void StandingStartRemovesAdvancedSpeedBootstrapOnMarkedStartSegment()
    {
        var marked = Resolve();
        var unmarked = Resolve(new Track(new[] { new TrackSegment(0, SegmentType.Straight, 30f) }));
        Assert.Equal(0f, Assert.Single(marked.Changes).EntrySpeed);
        Assert.True(Assert.Single(unmarked.Changes).EntrySpeed > 0f);
        Assert.True(Assert.Single(marked.Changes).ElapsedTimeSeconds > Assert.Single(unmarked.Changes).ElapsedTimeSeconds);
    }

    [Fact]
    public void UnmarkedTrackPreservesExistingZeroSpeedBootstrap()
    {
        var straight = Resolve(new Track(new[] { new TrackSegment(0, SegmentType.Straight) }));
        Assert.Equal(SegmentPhysics.MaxSafeTurnSpeed(0) * 0.95f, Assert.Single(straight.Changes).EntrySpeed);
        Assert.Null(Assert.Single(straight.Diagnostics).StandingStartLaunchProfile);
        var old = Resolve(Track.CreateExample());
        var rider = Rider();
        var expected = SegmentPhysics.MaxSafeTurnSpeed(0f, Track.CreateExample().Geometry, Perfect, rider.Profile.Skills, rider.ActiveSetup) * (0.90f + 0.5f * 0.16f);
        Assert.Equal(expected, Assert.Single(old.Changes).EntrySpeed);
    }

    [Fact]
    public void LegacyPhysicsPreservesExistingBootstrap()
    {
        var marked = Resolve(StartTrack(), legacy: true);
        var unmarked = Resolve(new Track(new[] { new TrackSegment(101, SegmentType.Straight, 30f), new TrackSegment(202, SegmentType.Straight, 30f) }), legacy: true);
        Assert.Equal(marked.Changes, unmarked.Changes);
        Assert.Null(Assert.Single(marked.Diagnostics).StandingStartLaunchProfile);
        Assert.True(Assert.Single(marked.Changes).EntrySpeed > 0f);
    }

    [Fact]
    public void StandingStartLaunchProfileIsUsedOnlyOnLapZeroSegmentZero()
    {
        var run = Run();
        foreach (var step in run.Steps)
        foreach (var diagnostic in step.Diagnostics)
            Assert.Equal(step.Snapshot.Step.LapIndex == 0 && step.Snapshot.Step.SegmentIndex == 0,
                diagnostic.StandingStartLaunchProfile.HasValue);
    }

    [Fact]
    public void StandingStartSegmentOnLapTwoUsesNormalStraightProfile()
    {
        var step = Run().Steps.Single(s => s.Snapshot.Step.LapIndex == 1 && s.Snapshot.Step.SegmentIndex == 0);
        Assert.All(step.Diagnostics, d => { Assert.Null(d.StandingStartLaunchProfile); Assert.NotNull(d.StraightProfile); });
    }

    [Fact]
    public void StandingStartStepDoesNotAlsoCreateStraightProfile()
    {
        var d = Assert.Single(Resolve().Diagnostics);
        Assert.NotNull(d.StandingStartLaunchProfile);
        Assert.Null(d.StraightProfile);
        Assert.Null(d.TurnExitDriveProfile);
        Assert.Null(d.TurnEntryScrubProfile);
    }

    [Fact]
    public void StandingStartElapsedTimeIncludesReactionDelay()
    {
        var step = Resolve();
        var launch = Launch(step);
        Assert.Equal(launch.MovementTimeSeconds + launch.ReactionTimeSeconds, Assert.Single(step.Changes).ElapsedTimeSeconds);
        Assert.Equal(30f, Assert.Single(step.Changes).Position.DistanceMeters);
        Assert.Equal(1d, Assert.Single(step.Changes).Position.TotalSegmentProgress);
    }

    [Fact]
    public void StandingStartLateralMovementBudgetExcludesReactionDelay()
    {
        // Short traversal ensures neither budget saturates at the target lane.
        var track = StartTrack(1f);
        var rider = Rider();
        var step = Resolve(track, new[] { rider }, targetLane: 1);
        var change = Assert.Single(step.Changes);
        var profile = Launch(step);
        var expected = LateralMovementModel.MoveTowards(0f, change.Lane, profile.MovementTimeSeconds,
            track.Geometry, Perfect, rider.Profile.Skills);
        var incorrect = LateralMovementModel.MoveTowards(0f, change.Lane, profile.TotalTimeSeconds,
            track.Geometry, Perfect, rider.Profile.Skills);
        Assert.Equal(expected, change.LateralPosition);
        Assert.True(change.LateralPosition < incorrect);
        Assert.Equal(0f, rider.LateralPosition); // Resolve has not committed anything.
    }

    [Fact]
    public void StandingStartUsesActualRemainingSegmentDistance()
    {
        var step = Resolve(StartTrack(12.25f));
        var profile = Launch(step);
        Assert.Equal(12.25f, profile.AccelerationDistanceMeters + profile.CruiseDistanceMeters);
        Assert.Equal(12.25f, Assert.Single(step.Changes).Position.DistanceMeters);
        // A restored partial segment is NOT the canonical start and must not relaunch.
        var rider = Rider();
        rider.RestorePosition(RiderPosition.Create(1, 0, 0.5f, 2, 6.125f));
        var partial = Resolve(StartTrack(12.25f), new[] { rider });
        Assert.Null(Assert.Single(partial.Diagnostics).StandingStartLaunchProfile);
        Assert.Equal(6.125f, Assert.Single(partial.Diagnostics).TravelledMeters);
    }

    [Fact]
    public void StandingStartUsesEntrySampledSurface()
    {
        var track = StartTrack();
        var state = new TrackState(track.Segments.Count, LaneModel.LanesCount,
            (_, lane) => lane == 1 ? new TrackSurfaceState(0.2f, 0.3f, 0.7f) : Perfect);
        var rider = Rider(lane: 1);
        rider.LateralPosition = 1.25f;
        var step = Resolve(track, new[] { rider }, state, targetLane: 2);
        var surface = step.Snapshot.TrackState.SampleSurface(0, 1.25f);
        var expected = LongitudinalDynamics.CalculateStandingStartLaunchProfile(rider.Profile.Skills,
            rider.ActiveSetup, surface, 30f, 23f);
        Assert.Equal(expected, Launch(step));
        Assert.Equal(surface, Assert.Single(step.Diagnostics).EntrySurface);
        Assert.NotEqual(surface, step.Snapshot.TrackState.SampleSurface(0, Assert.Single(step.Changes).LateralPosition));
    }

    [Fact]
    public void StandingStartExitSpeedFeedsFirstTurnEntry()
    {
        var samples = Run().Trace!.StepSamples.Where(s => s.RiderId == 1).ToArray();
        Assert.Equal(SegmentType.TurnEntry, samples[1].SegmentType);
        Assert.Equal(samples[0].ExitSpeedMetersPerSecond, samples[1].EntrySpeedMetersPerSecond);
        Assert.Equal(samples[0].EndTimeSeconds, samples[1].StartTimeSeconds);
        Assert.True(samples[1].StartTimeSeconds > 0f);
    }

    [Fact]
    public void FirstTurnStillUsesExistingTurnEntryScrub()
    {
        var run = Run();
        var step = run.Steps[1];
        Assert.All(step.Diagnostics, d => Assert.NotNull(d.TurnEntryScrubProfile));
        var d = step.Diagnostics[0];
        Assert.Equal(d.TravelledMeters * 0.5f,
            d.TurnEntryScrubProfile!.Value.CarryDistanceMeters + d.TurnEntryScrubProfile.Value.DecelerationDistanceMeters, 4);
    }

    [Fact]
    public void StandingStartDoesNotChangeTurnEntryFormula()
    {
        var step = Run().Steps[1];
        foreach (var d in step.Diagnostics)
        {
            var rider = step.Snapshot.Rider(d.RiderId);
            var safe = SegmentPhysics.MaxSafeTurnSpeed(rider.LateralPosition, step.Snapshot.Track.Geometry,
                d.EntrySurface, rider.Profile.Skills, rider.ActiveSetup);
            var deceleration = LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(rider.Profile.Skills, d.EntrySurface);
            Assert.Equal(LongitudinalDynamics.CalculateTurnEntryScrubProfile(rider.Speed, safe, deceleration, d.TravelledMeters), d.TurnEntryScrubProfile);
        }
    }

    [Fact]
    public void StandingStartDoesNotChangeTurnExitFormula()
    {
        var step = Run().Steps[3];
        foreach (var d in step.Diagnostics)
        {
            var rider = step.Snapshot.Rider(d.RiderId);
            var change = step.Changes.Single(c => c.RiderId == d.RiderId);
            Assert.Equal(LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(change.PhysicsSpeed,
                rider.Profile.Skills, rider.ActiveSetup, d.EntrySurface, d.TravelledMeters,
                d.AttainableTopSpeedMetersPerSecond!.Value), d.TurnExitDriveProfile);
        }
    }

    [Fact]
    public void StandingStartDoesNotChangeNormalStraightFormula()
    {
        var rider = Rider();
        rider.Speed = 12f;
        var step = Resolve(StartTrack(), new[] { rider }); // next is Straight: no lookahead across it
        var d = Assert.Single(step.Diagnostics);
        Assert.Null(d.StandingStartLaunchProfile);
        var deceleration = LongitudinalDynamics.CalculateCornerEntryDecelerationMetersPerSecondSquared(rider.Profile.Skills, Perfect);
        Assert.Equal(LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(12f, rider.Profile.Skills,
            rider.ActiveSetup, Perfect, deceleration, 30f, 23f, null), d.StraightProfile);
    }

    [Fact]
    public void StandingStartTrackCrossesLapBoundaryAtStartFinishLine()
    {
        var run = Run();
        Assert.All(run.Steps[7].Changes, c => Assert.Equal(0, c.Position.LapsCompleted));
        Assert.All(run.Steps[8].Changes, c => { Assert.Equal(1, c.Position.LapsCompleted); Assert.Equal(0, c.Position.SegmentIndex); });
        foreach (var lap in run.Trace!.LapSummaries)
        {
            var boundary = run.Trace.StepSamples.Single(s => s.RiderId == lap.RiderId && s.LapIndex == lap.LapNumber - 1 && s.SegmentIndex == 8);
            var beginning = run.Trace.StepSamples.Single(s => s.RiderId == lap.RiderId && s.LapIndex == lap.LapNumber - 1 && s.SegmentIndex == 0);
            Assert.Equal(boundary.EndTimeSeconds - beginning.StartTimeSeconds, lap.LapTimeSeconds);
        }
    }

    [Fact]
    public void StandingStartTrackDoesNotAddExtraLaunchDistance()
    {
        var run = Run();
        var lap = run.Trace!.StepSamples.Where(s => s.RiderId == 1 && s.LapIndex == 0).ToArray();
        Assert.Equal(9, lap.Length);
        Assert.Equal(120f, lap.Where(s => s.SegmentType == SegmentType.Straight).Sum(s => s.TravelledMeters));
        Assert.Equal(StandingStartTrackTests.LapDistance(Track.CreateExample(), 0f), lap.Sum(s => s.TravelledMeters), 3);
    }

    [Fact]
    public void FourLapStandingStartHeatCompletesExactlyFourTopologyLaps()
    {
        var run = Run();
        Assert.Equal(144, run.Trace!.StepSamples.Count);
        Assert.Equal(16, run.Trace.LapSummaries.Count);
        Assert.All(run.Riders, rider => { Assert.Equal(RiderRaceStatus.Finished, rider.Status); Assert.Equal(4, rider.LapsCompleted); Assert.Equal(36d, rider.CanonicalProgress); });
    }

    [Fact]
    public void FourLapStandingStartHeatPhysicalDistanceDoesNotContainExtraThirtyMeters()
    {
        var run = Run();
        foreach (var rider in run.Riders)
        {
            var samples = run.Trace!.StepSamples.Where(s => s.RiderId == rider.RiderId).ToArray();
            Assert.Equal(480f, samples.Where(s => s.SegmentType == SegmentType.Straight).Sum(s => s.TravelledMeters));
            Assert.InRange(MathF.Abs(rider.DistanceMeters - samples.Sum(s => s.TravelledMeters)), 0f, 0.002f);
            Assert.InRange(MathF.Abs(rider.DistanceMeters - 4f * StandingStartTrackTests.LapDistance(Track.CreateExample(), rider.RiderId - 1)), 0f, 0.002f);
        }
    }

    [Fact]
    public void FinalStandingStartTrackSegmentFinishesHeatBeforeAnotherLaunchSegment()
    {
        var run = Run();
        Assert.Equal(36, run.Steps.Count);
        Assert.Equal(8, run.Steps[^1].Snapshot.Step.SegmentIndex);
        Assert.All(run.Steps[^1].Changes, c => Assert.Equal(RiderRaceStatus.Finished, c.Status));
    }

    [Fact]
    public void NoStandingStartProfileIsCreatedAfterFinish()
    {
        var run = Run();
        var after = Resolve(run.Track, run.Riders, run.State, lap: 4);
        Assert.Empty(after.Changes);
        Assert.Empty(after.Diagnostics);
    }

    [Fact]
    public void StandingStartIsDeterministicForSameSeedAndInputs()
    {
        var first = Resolve(riders: Riders());
        var second = Resolve(riders: Riders());
        Assert.Equal(first.Changes, second.Changes);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(first.Events, second.Events);
    }

    [Fact]
    public void StandingStartIsIndependentOfRiderCollectionOrder()
    {
        var first = Resolve(riders: Riders());
        var reverse = Resolve(riders: Riders(new[] { 4, 3, 2, 1 }));
        Assert.Equal(first.Changes, reverse.Changes);
        Assert.Equal(first.Diagnostics, reverse.Diagnostics);
        Assert.Equal(first.Events, reverse.Events);
    }

    [Fact]
    public void AllTwentyFourRiderPermutationsPreserveStandingStartResults()
    {
        var expected = Run();
        var permutations = Permutations(new[] { 1, 2, 3, 4 }).ToArray();
        Assert.Equal(24, permutations.Length);
        foreach (var permutation in permutations)
        {
            var actual = Run(order: permutation);
            Assert.Equal(expected.Trace!.StepSamples, actual.Trace!.StepSamples);
            Assert.Equal(expected.Result.Classification, actual.Result.Classification);
        }
    }

    [Fact]
    public void StandingStartRequiresNotStartedStatus()
    {
        var rider = Rider();
        rider.IsCrashed = true;
        rider.IsCrashed = false; // public transition to Racing, still at canonical zero
        Assert.Equal(RiderRaceStatus.Racing, rider.Status);
        Assert.Null(Assert.Single(Resolve(riders: new[] { rider }).Diagnostics).StandingStartLaunchProfile);
    }

    [Fact]
    public void StandingStartRequiresExactCanonicalAndPhysicalStart()
    {
        var rider = Rider();
        rider.RestorePosition(RiderPosition.Create(1, 0, 0f, 9, 0.001f));
        Assert.Null(Assert.Single(Resolve(riders: new[] { rider }).Diagnostics).StandingStartLaunchProfile);
        rider.RestorePosition(RiderPosition.Create(1, 0, 0.001f, 9));
        Assert.Null(Assert.Single(Resolve(riders: new[] { rider }).Diagnostics).StandingStartLaunchProfile);
    }

    [Fact]
    public void StandingStartDoesNotRelaunchRestoredLaterLapAtZeroSpeed()
    {
        var rider = Rider();
        rider.RestorePosition(RiderPosition.Create(2, 0, 0f, 9));
        Assert.Null(Assert.Single(Resolve(riders: new[] { rider }, lap: 1).Diagnostics).StandingStartLaunchProfile);
    }

    [Fact]
    public void StandingStartAcceptsNonPositiveSpeedButPassesZeroToPhysics()
    {
        var rider = Rider();
        rider.Speed = -0.1f;
        var step = Resolve(riders: new[] { rider });
        Assert.Equal(0f, Assert.Single(step.Changes).PhysicsSpeed);
        Assert.Equal(Launch(Resolve()), Launch(step));
    }

    [Fact]
    public void StandingStartMoraleAndGateDoNotAlterReactionOrLaunch()
    {
        var first = Rider(lane: 0);
        first.Morale = 0f;
        var last = Rider(lane: 4);
        last.Morale = 1f;
        Assert.Equal(Launch(Resolve(riders: new[] { first })), Launch(Resolve(riders: new[] { last })));
    }
}
