using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class SteppedTurnExitSimulationTests
{
    private static readonly TrackSurfaceState PerfectSurface = new(1f, 0f, 0.35f);

    [Fact]
    public void TurnExitEligibilityRemainsOkOrBrakeOnly()
    {
        foreach (var (ratio, outcome) in new[]
            { (.9f, SegmentOutcome.Ok), (1.05f, SegmentOutcome.Brake), (1.20f, SegmentOutcome.RunWide), (1.30f, SegmentOutcome.Crash) })
        {
            var step = Resolve(SegmentType.TurnExit, new[] { OverspeedRider(ratio) });
            Assert.Equal(outcome, step.Changes[0].Outcome);
            Assert.Null(step.Diagnostics[0].TurnExitDriveProfile);
            if (outcome == SegmentOutcome.Crash) Assert.Null(step.Diagnostics[0].ContinuousCornerProfile);
            else Assert.Equal(outcome != SegmentOutcome.RunWide, step.Diagnostics[0].ContinuousCornerProfile!.DriveDistanceMeters > 0f);
        }
    }

    [Fact]
    public void EligibleTurnExitSpeedComesFromProfileExitSpeed()
    {
        var resolved = Resolve(SegmentType.TurnExit, new[] { Rider(1, 1, 1f, 12f) });
        var change = Assert.Single(resolved.Changes);
        var profile = Assert.IsType<ContinuousCornerTraversalProfile>(Assert.Single(resolved.Diagnostics).ContinuousCornerProfile);
        Assert.Equal(profile.ExitSpeedMetersPerSecond, change.Speed);
    }

    [Fact]
    public void EligibleTurnExitTravelTimeComesFromProfile()
    {
        var resolved = Resolve(SegmentType.TurnExit, new[] { Rider(1, 1, 1f, 12f) });
        var diagnostics = Assert.Single(resolved.Diagnostics);
        var profile = Assert.IsType<ContinuousCornerTraversalProfile>(diagnostics.ContinuousCornerProfile);
        Assert.Equal(profile.TravelTimeSeconds, diagnostics.TravelTimeSeconds);
    }

    [Fact]
    public void TurnExitProfileTimeControlsLateralMovementBudget()
    {
        var rider = Rider(1, 0, 0f, 12f);
        var resolved = Resolve(
            SegmentType.TurnExit,
            new[] { rider },
            targetLanes: new Dictionary<int, int> { [1] = 1 });
        var change = Assert.Single(resolved.Changes);
        var diagnostics = Assert.Single(resolved.Diagnostics);
        var profile = Assert.IsType<ContinuousCornerTraversalProfile>(diagnostics.ContinuousCornerProfile);
        var expected = LateralMovementModel.MoveTowards(
            0f,
            change.Lane,
            profile.TravelTimeSeconds,
            TrackGeometry.Default,
            diagnostics.EntrySurface,
            rider.Profile.Skills);
        Assert.Equal(expected, change.LateralPosition);
    }

    [Fact]
    public void RunWideStillDoesNotReceiveTurnExitDriveProfile()
    {
        var rider = OverspeedRider(1.20f);
        var diagnostics = Assert.Single(Resolve(SegmentType.TurnExit, new[] { rider }).Diagnostics);
        Assert.Null(diagnostics.TurnExitDriveProfile);
    }

    [Fact]
    public void CrashStillDoesNotReceiveTurnExitDriveProfile()
    {
        var rider = OverspeedRider(1.30f);
        var diagnostics = Assert.Single(Resolve(SegmentType.TurnExit, new[] { rider }).Diagnostics);
        Assert.Null(diagnostics.TurnExitDriveProfile);
    }

    [Fact]
    public void TurnMiddleDoesNotReceiveTurnExitDriveProfile()
        => Assert.Null(Assert.Single(Resolve(
            SegmentType.TurnMiddle, new[] { Rider(1, 1, 1f, 12f) }).Diagnostics).TurnExitDriveProfile);

    [Fact]
    public void TurnEntryDoesNotReceiveTurnExitDriveProfile()
        => Assert.Null(Assert.Single(Resolve(
            SegmentType.TurnEntry, new[] { Rider(1, 1, 1f, 12f) }).Diagnostics).TurnExitDriveProfile);

    [Fact]
    public void LegacyTurnExitDoesNotReceiveTurnExitDriveProfile()
        => Assert.Null(Assert.Single(Resolve(
            SegmentType.TurnExit,
            new[] { Rider(1, 1, 1f, 12f) },
            useLegacyPhysics: true).Diagnostics).TurnExitDriveProfile);

    [Fact]
    public void BrakeTurnExitStartsSignedDriveFromResolvedPhysicsSpeed()
    {
        var rider = OverspeedRider(1.05f);
        var resolved = Resolve(SegmentType.TurnExit, new[] { rider });
        var diagnostics = Assert.Single(resolved.Diagnostics);
        Assert.Equal(SegmentOutcome.Brake, resolved.Changes[0].Outcome);
        Assert.Equal(rider.Speed, resolved.Changes[0].PhysicsSpeed);
        Assert.Equal(CornerTestSupport.Expected(resolved), diagnostics.ContinuousCornerProfile);
    }

    [Fact]
    public void ExistingOverspeedAboveEquilibriumNaturallyDecelerates()
    {
        var geometry = new TrackGeometry(60f, 100f, 1f, 1f);
        var force = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
            RiderSkills.Balanced, BikeSetup.Neutral, PerfectSurface);
        var equilibrium = LongitudinalDynamics.CalculateFullDriveEquilibriumSpeedMetersPerSecond(force, BikeSetup.Neutral);
        var rider = Rider(1, 4, 4f, equilibrium + 2f);
        var resolved = Resolve(SegmentType.TurnExit, new[] { rider }, geometry: geometry);
        var speed = Assert.Single(resolved.Changes).Speed;
        Assert.True(speed < rider.Speed);
        Assert.True(speed > equilibrium);
    }

    [Fact]
    public void TurnExitSteppedTraversalSamplesActualSurface()
    {
        var track = SingleTrack(SegmentType.TurnExit);
        var state = new TrackState(1, LaneModel.LanesCount, (_, lane) =>
            lane == 1 ? new TrackSurfaceState(.2f, 0f, .35f) : PerfectSurface);
        var resolved = Resolve(track, state, new[] { Rider(1, 1, 1.25f, 12f) });
        Assert.Equal(resolved.Snapshot.TrackState.SampleSurface(0, 1.25f), resolved.Diagnostics[0].EntrySurface);
        var path = Assert.IsType<ExecutedSegmentPath>(resolved.Diagnostics[0].ExecutedPath);
        Assert.Contains(path.Steps, s => s.SampledSurface != resolved.Diagnostics[0].EntrySurface);
        Assert.Equal(path.ExitSpeedMetersPerSecond, resolved.Changes[0].Speed);
    }

    [Fact]
    public void TurnExitSteppedTraversalUsesContinuousEntryPathDistance()
    {
        var inner = Resolve(SegmentType.TurnExit, new[] { Rider(1, 1, 1f, 12f) });
        var outer = Resolve(SegmentType.TurnExit, new[] { Rider(1, 3, 3f, 12f) });
        var innerDiagnostics = Assert.Single(inner.Diagnostics);
        var outerDiagnostics = Assert.Single(outer.Diagnostics);

        Assert.True(outerDiagnostics.TravelledMeters > innerDiagnostics.TravelledMeters);
        Assert.True(
            outerDiagnostics.ContinuousCornerProfile!.ExitSpeedMetersPerSecond
            > innerDiagnostics.ContinuousCornerProfile!.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void TurnExitSteppedTraversalIsIndependentOfRiderCollectionOrder()
    {
        var riders = new[] { Rider(1, 0, 0f, 12f), Rider(2, 4, 4f, 13f) };
        var forward = Resolve(SegmentType.TurnExit, riders);
        var reversed = Resolve(SegmentType.TurnExit, riders.Reverse().ToArray());
        Assert.Equal(Project(forward), Project(reversed));
    }

    [Fact]
    public void SameSeedAndInitialStateRemainDeterministic()
    {
        var first = Resolve(SegmentType.TurnExit, new[] { Rider(1, 1, 1f, 12f) });
        var second = Resolve(SegmentType.TurnExit, new[] { Rider(1, 1, 1f, 12f) });
        Assert.Equal(Project(first), Project(second));
    }

    [Fact]
    public void AllTwentyFourRiderPermutationsRemainIdentical()
    {
        var expected = Project(Resolve(
            SegmentType.TurnExit,
            Enumerable.Range(1, 4).Select(id => Rider(id, id - 1, id - 1, 11f + id)).ToArray()));
        foreach (var permutation in Permutations(new[] { 1, 2, 3, 4 }))
        {
            var riders = permutation.Select(id => Rider(id, id - 1, id - 1, 11f + id)).ToArray();
            Assert.Equal(expected, Project(Resolve(SegmentType.TurnExit, riders)));
        }
    }

    private static ResolvedSimulationStep Resolve(
        SegmentType type,
        IReadOnlyList<RiderState> riders,
        IReadOnlyDictionary<int, int>? targetLanes = null,
        bool useLegacyPhysics = false,
        TrackGeometry? geometry = null)
    {
        var track = SingleTrack(type, geometry);
        return Resolve(track, TrackState.CreateDefault(track, PerfectSurface), riders, targetLanes, useLegacyPhysics);
    }

    private static ResolvedSimulationStep Resolve(
        Track track,
        TrackState state,
        IReadOnlyList<RiderState> riders,
        IReadOnlyDictionary<int, int>? targetLanes = null,
        bool useLegacyPhysics = false)
    {
        targetLanes ??= riders.ToDictionary(item => item.RiderId, item => item.Lane);
        var engine = new SimulationEngine(new TargetDecisionModel(targetLanes));
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 246,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };
        var snapshot = engine.CaptureSnapshot(
            track,
            state,
            riders,
            new SimulationStepContext(1, 0, 0, 0, options.Seed, 1, useLegacyPhysics));
        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    private static Track SingleTrack(SegmentType type, TrackGeometry? geometry = null)
        => new(new[] { new TrackSegment(0, type) }, geometry ?? TrackGeometry.Default);

    private static RiderState OverspeedRider(float multiplier)
    {
        var rider = Rider(1, 1, 1f, 0f);
        rider.Speed = CornerTestSupport.Envelope(SingleTrack(SegmentType.TurnExit), rider).SpeedMetersPerSecond(0f) * multiplier;
        return rider;
    }

    private static RiderState Rider(int id, int lane, float lateralPosition, float speed)
        => new(
            new RiderProfile(
                id,
                $"Rider {id}",
                new RiderSkills(50f, 50f, 50f, 50f, 50f, 50f),
                RiderStyle.Balanced),
            lane)
        {
            LateralPosition = lateralPosition,
            Speed = speed,
            ActiveSetup = new BikeSetup(0.5f, 0.5f),
        };

    private static Projection[] Project(ResolvedSimulationStep resolved)
        => resolved.Changes.OrderBy(item => item.RiderId)
            .Zip(
                resolved.Diagnostics.OrderBy(item => item.RiderId),
                (change, diagnostics) => new Projection(
                    change.RiderId,
                    change.Outcome,
                    change.Speed,
                    change.ElapsedTimeSeconds,
                    change.LateralPosition,
                    diagnostics.ContinuousCornerProfile))
            .ToArray();

    private static IEnumerable<int[]> Permutations(int[] values)
    {
        if (values.Length == 1)
        {
            yield return values;
            yield break;
        }

        for (var index = 0; index < values.Length; index++)
        {
            var tail = values.Where((_, tailIndex) => tailIndex != index).ToArray();
            foreach (var permutation in Permutations(tail))
                yield return new[] { values[index] }.Concat(permutation).ToArray();
        }
    }

    private sealed class TargetDecisionModel(IReadOnlyDictionary<int, int> targets)
        : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(targets[rider.RiderId], 0f);
    }

    private sealed record Projection(
        int RiderId,
        SegmentOutcome Outcome,
        float Speed,
        float Time,
        float LateralPosition,
        ContinuousCornerTraversalProfile? Profile);
}
