using CausalAudit;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;
using RaceReadiness;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class CommonTimeMotionWindowTests
{
    private static readonly HeatSimulationOptions Options = new() { Seed = 19, IncidentFrequency = 0 };
    private static SimulationEngine Engine() => new(new Fixtures.Hold());
    private static SimulationSnapshot DifferentSegments()
    {
        var track = Track.CreateExample();
        var riders = new[] { 1, 3, 5, 7 }.Select((segment, index) => new RiderSnapshot(index + 1,
            RiderProfile.CreateDefault(index + 1), RiderPosition.Create(1, segment, 0, track.Segments.Count),
            -1, 2, 2, 16, 0, RiderRaceStatus.Racing, 0, BikeSetup.Neutral, .5f, .5f));
        return new(new(91, 0, 0, 0, 19, 4), track, TrackState.CreateDefault(track).Snapshot(), riders);
    }
    private static RiderIntent Hold(int id) => new(id, new RiderDecision(2) { HoldLateralPosition = true });
    private static TrackState Surface(SimulationSnapshot snapshot) => new(snapshot.TrackState.SegmentCount,
        snapshot.TrackState.LinesCount, snapshot.TrackState.GetSurface);
    private static void Stage(CommonTimeMotionWindow window, IEnumerable<int> ids, int count)
    {
        for (var step = 0; step < count; step++)
            foreach (var id in ids) window.StageSelectedMotion(Hold(id), step);
    }

    [Fact]
    public void DifferentSegmentsShareCoverageBeforePublishingActualMotionAndWearOnce()
    {
        var origin = DifferentSegments(); var engine = Engine(); var surface = Surface(origin);
        var live = origin.Riders.Select(r => r.ToMutableCopy()).ToArray(); var log = new SimLog();
        var window = new CommonTimeMotionWindow(engine, origin, Options);
        var before = Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges });
        Stage(window, new[] { 4, 2, 1, 3 }, 3);
        Assert.Equal(before, Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges }));
        Assert.Equal(0, window.CommittedWatermarkSeconds); Assert.Empty(window.CommittedHistory);
        Assert.Equal(4, window.Continuations.Select(c => c.Pending[0].Resolution.Snapshot.Step.SegmentIndex).Distinct().Count());
        var first = window.Continuations.Select(c => c.Pending[0].Resolution).ToArray();
        var expectedSurface = Surface(origin); var expectedLog = new SimLog();
        var expected = origin.Riders.Select(r => r.ToMutableCopy()).ToArray();
        foreach (var segment in first)
            engine.Commit(segment, expected.Where(r => r.RiderId == segment.Changes.Single().RiderId).ToArray(), expectedSurface, expectedLog);

        window.CommitFirstSegments(live.Reverse().ToArray(), surface, log);
        Assert.Equal(Exact.Hash(expected), Exact.Hash(live));
        Assert.Equal(Exact.Hash(expectedSurface.Snapshot()), Exact.Hash(surface.Snapshot()));
        Assert.Equal(expectedLog.Lines, log.Lines); Assert.Equal(expectedLog.SurfaceChanges, log.SurfaceChanges);
        Assert.Equal(live.Min(r => (double)r.ElapsedTimeSeconds), window.CommittedWatermarkSeconds);
        Assert.True(window.CertifiedThroughSeconds >= live.Max(r => r.ElapsedTimeSeconds));
        Assert.Equal(4, window.CommittedHistory.Count);
        Assert.All(window.Continuations, c => { Assert.Equal(2, c.Pending.Count);
            Assert.Equal(c.Committed, c.Pending[0].Resolution.Snapshot.Riders.Single()); });
        var after = Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges });
        Assert.Throws<InvalidOperationException>(() => window.CommitFirstSegments(live, surface, log));
        Assert.Throws<InvalidOperationException>(() => window.StageSelectedMotion(Hold(1), 3));
        Assert.Equal(after, Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges }));
    }

    [Fact]
    public void MissingOrShortUnaffectedRiderCoverageCannotAuthorizeOthersLaterEndpoints()
    {
        var origin = DifferentSegments(); var window = new CommonTimeMotionWindow(Engine(), origin, Options);
        var live = origin.Riders.Select(r => r.ToMutableCopy()).ToArray(); var surface = Surface(origin); var log = new SimLog();
        Stage(window, new[] { 1, 2, 3 }, 1);
        Assert.Equal(CommonTimeCommitRefusal.InsufficientCoverage,
            Assert.Throws<CommonTimeCommitException>(() => window.CommitFirstSegments(live, surface, log)).Reason);
        window.StageSelectedMotion(Hold(4), 0);
        Assert.Equal(CommonTimeCommitRefusal.InsufficientCoverage,
            Assert.Throws<CommonTimeCommitException>(() => window.CommitFirstSegments(live, surface, log)).Reason);
        Assert.All(live, r => Assert.Equal(0, r.ElapsedTimeSeconds)); Assert.Empty(log.Lines);
        Assert.Empty(window.CommittedHistory); Assert.Equal(0, window.CommittedWatermarkSeconds);
    }

    [Theory]
    [InlineData(.105f)] [InlineData(.125f)]
    public void InitialAndInteriorContactsRefuseBeforeAnyStatusWearEventOrHistoryPublication(float ahead)
    {
        var origin = Fixtures.Rear(ahead: ahead, count: 4); var options = Options;
        var window = new CommonTimeMotionWindow(Engine(), origin, options);
        var live = origin.Riders.Select(r => r.ToMutableCopy()).ToArray(); var surface = Surface(origin); var log = new SimLog();
        Stage(window, new[] { 1, 2, 3, 4 }, 2);
        var before = Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges });
        Assert.Equal(CommonTimeCommitRefusal.PartialProductionCursorRequired,
            Assert.Throws<CommonTimeCommitException>(() => window.CommitFirstSegments(live, surface, log)).Reason);
        Assert.Equal(before, Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges }));
        Assert.Equal(0, window.CommittedWatermarkSeconds); Assert.Null(window.CertifiedThroughSeconds);
        Assert.Empty(window.CommittedHistory); Assert.All(window.Continuations, c => Assert.Equal(2, c.Pending.Count));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StaleRiderOrSurfaceVersionIsRejectedBeforeMutation(bool surfaceChange)
    {
        var origin = DifferentSegments(); var window = new CommonTimeMotionWindow(Engine(), origin, Options);
        Stage(window, new[] { 1, 2, 3, 4 }, 3);
        var live = origin.Riders.Select(r => r.ToMutableCopy()).ToArray(); var surface = Surface(origin); var log = new SimLog();
        if (surfaceChange) surface.ApplySurfaceDelta(0, 0, 0, .01f, 0, "test", 91, 0, new SimLog(false));
        else live[2].Condition = .8f;
        var before = Exact.Hash(new { live, Surface = surface.Snapshot() });
        Assert.Throws<InvalidOperationException>(() => window.CommitFirstSegments(live, surface, log));
        Assert.Equal(before, Exact.Hash(new { live, Surface = surface.Snapshot() })); Assert.Empty(log.Lines);
    }

    [Fact]
    public void StoredControlsAndRngAddressResumeExactlyAtRealSegmentBoundariesInEitherOrder()
    {
        var origin = DifferentSegments(); var normal = new CommonTimeMotionWindow(Engine(), origin, Options);
        var reverse = new CommonTimeMotionWindow(Engine(), origin, Options);
        Stage(normal, new[] { 1, 2, 3, 4 }, 3); Stage(reverse, new[] { 4, 3, 2, 1 }, 3);
        Assert.Equal(Exact.Hash(normal.Continuations), Exact.Hash(reverse.Continuations));
        foreach (var cursor in normal.Continuations)
        {
            foreach (var pending in cursor.Pending)
            {
                Assert.Equal(19, pending.Resolution.Snapshot.Step.Seed);
                Assert.Same(origin.TrackState, pending.Resolution.Snapshot.TrackState);
                var replay = Engine().ResolveProduction(pending.Resolution.Snapshot, new[] { pending.Intent }, Options, legacyContacts: false);
                Assert.Equal(Exact.Hash(pending.Resolution), Exact.Hash(replay));
            }
        }
        Assert.Throws<ArgumentException>(() => normal.StageSelectedMotion(Hold(1), 2));
        var asynchronous = new SimulationSnapshot(origin.Step, origin.Track, origin.TrackState,
            origin.Riders.Select(r => r.RiderId == 3 ? r with { ElapsedTimeSeconds = .1f } : r));
        Assert.Throws<ArgumentException>(() => new CommonTimeMotionWindow(Engine(), asynchronous, Options));
    }

    [Fact]
    public void TerminalCompletionPublishesActualFinalStatusAndRejectsUnresolvedTouchAtTheEndpoint()
    {
        var fixture = DifferentSegments();
        var origin = new SimulationSnapshot(fixture.Step with { RequiredLaps = 1 }, fixture.Track,
            fixture.TrackState, fixture.Riders.Where(r => r.RiderId == 4));
        var window = new CommonTimeMotionWindow(Engine(), origin, Options with { Laps = 1 });
        var segment = window.StageSelectedMotion(Hold(4), 0);
        var change = Assert.Single(segment.Changes);
        Assert.Equal(RiderRaceStatus.Finished, change.Status);
        Assert.Equal(CommonTimeCommitRefusal.PartialProductionCursorRequired,
            Assert.Throws<CommonTimeCommitException>(() => CommonTimeCommitGuard.Require(segment.Changes,
                change.ElapsedTimeSeconds, change.ElapsedTimeSeconds)).Reason);
        var live = origin.Riders.Select(r => r.ToMutableCopy()).ToArray();
        var surface = Surface(origin); var log = new SimLog();
        window.CommitFirstSegments(live, surface, log);
        Assert.Equal(RiderRaceStatus.Finished, live[0].Status);
        Assert.Equal(change.Position, live[0].Position); Assert.Null(live[0].ContactRecovery);
        Assert.Empty(window.Continuations.Single().Pending);
    }

    [Fact]
    public void RealControl19CRequiresLookaheadBeforeStep7CommitAndBlocksEarlierStep8Contact()
    {
        var scenario = Catalog.All().Single(s => s.Id == "control"); var (track, surface, live) = scenario.Create();
        var observer = new UntilStep8();
        Assert.Throws<CaptureComplete>(() => new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(track, surface, live,
            new() { Seed = 19, EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = true,
                PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit }, 91, observer));
        var step7 = observer.Steps.Single(s => s.Snapshot.Step.StepNumber == 7);
        var step8 = observer.Steps.Single(s => s.Snapshot.Step.StepNumber == 8);
        var pair = Assert.Single(step8.Interaction!.PhysicalContactConsequences!.AppliedPairs);
        Assert.Equal(1, pair.RiderA); Assert.Equal(2, pair.RiderB);
        Assert.Equal(13.194772190598087, pair.FirstTouchCommonTimeSeconds);
        var motion = step7.Motions.Single(m => m.RiderId == 1);
        var touchLocalTime = pair.FirstTouchCommonTimeSeconds - motion.StartElapsedTimeSeconds;
        Assert.Contains(motion.Nodes.Zip(motion.Nodes.Skip(1)), n =>
            n.First.LocalTimeSeconds < touchLocalTime && touchLocalTime < n.Second.LocalTimeSeconds);
        // A3 pauses accepted Straight steps only. This historical TurnExit contact lies
        // strictly inside a numerical step: neither sampling nor the new cursor resolves it.
        var rider1Snapshot = new SimulationSnapshot(step7.Snapshot.Step, track, step7.Snapshot.TrackState,
            step7.Snapshot.Riders.Where(r => r.RiderId == 1));
        Assert.Throws<RiderIntegrationRefusalException>(() => Engine().BeginRiderIntegration(rider1Snapshot,
            new(1, new RiderDecision(step7.Changes.Single(c => c.RiderId == 1).Lane)), Options, 1));
        // The production observer stops BEFORE Commit(step8). Step7's publication is already
        // too far ahead for riders 1,3,4. A forward window must first buffer that step.
        Assert.Equal(new[] { 1, 3, 4 }, step7.Changes.Where(c => c.ElapsedTimeSeconds > pair.FirstTouchCommonTimeSeconds).Select(c => c.RiderId));
        var coverageAfter7 = step7.Changes.Min(c => (double)c.ElapsedTimeSeconds);
        Assert.Equal(CommonTimeCommitRefusal.InsufficientCoverage,
            Assert.Throws<CommonTimeCommitException>(() => CommonTimeCommitGuard.Require(step7.Changes, coverageAfter7, null)).Reason);
        var before = Exact.Hash(new { Snapshot = step7.Snapshot, step7.Changes, Surface = surface.Snapshot(), live });
        Assert.Equal(CommonTimeCommitRefusal.PartialProductionCursorRequired,
            Assert.Throws<CommonTimeCommitException>(() => CommonTimeCommitGuard.Require(step7.Changes,
                step8.Changes.Min(c => (double)c.ElapsedTimeSeconds), pair.FirstTouchCommonTimeSeconds)).Reason);
        Assert.Equal(before, Exact.Hash(new { Snapshot = step7.Snapshot, step7.Changes, Surface = surface.Snapshot(), live }));
        Assert.Equal(13.278690338134766, step7.Changes.Single(c => c.RiderId == 1).ElapsedTimeSeconds);
    }

    private sealed class CaptureComplete : Exception;
    private sealed class UntilStep8 : ISimulationStepObserver
    {
        internal List<ResolvedSimulationStep> Steps { get; } = new();
        public void OnStepResolved(ResolvedSimulationStep step)
        { Steps.Add(step); if (step.Snapshot.Step.StepNumber == 8) throw new CaptureComplete(); }
    }
}
