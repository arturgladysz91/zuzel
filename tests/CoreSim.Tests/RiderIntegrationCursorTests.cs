using CausalAudit;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;
using RaceReadiness;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class RiderIntegrationCursorTests
{
    private static readonly HeatSimulationOptions Options = new() { Seed = 19, Laps = 1, IncidentFrequency = 0 };
    private static SimulationEngine Engine() => new(new Fixtures.Hold());
    private static RiderIntent Intent(int id = 1) => new(id, new RiderDecision(2));
    private static SimulationSnapshot Origin(int segment = 7, float progress = .137f)
    {
        var track = Track.CreateExample();
        var rider = new RiderSnapshot(1, RiderProfile.CreateDefault(1),
            RiderPosition.Create(1, segment, progress, track.Segments.Count), -1, 1, .8f, 19, 0,
            RiderRaceStatus.Racing, 5, BikeSetup.Neutral, .5f, .5f);
        return new(new(91, 37, 0, segment, 19, 1), track, TrackState.CreateDefault(track).Snapshot(), new[] { rider });
    }
    private static TrackState Surface(SimulationSnapshot snapshot) => new(snapshot.TrackState.SegmentCount,
        snapshot.TrackState.LinesCount, snapshot.TrackState.GetSurface);

    [Theory]
    [InlineData(7, .137f, 5)]
    [InlineData(3, 0f, 32)]
    public void ProductionTraversalResumesExactlyWithoutReevaluatingAcceptedSteps(int segment, float progress, int stop)
    {
        var origin = Origin(segment, progress); var engine = Engine();
        var continuousCounts = new Dictionary<ProjectionMaterialization, int>();
        var resumedCounts = new Dictionary<ProjectionMaterialization, int>();
        ResolvedSimulationStep continuous; ResolvedSimulationStep resumed;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, count) => continuousCounts[kind] = continuousCounts.GetValueOrDefault(kind) + count;
            continuous = engine.ResolveProduction(origin, new[] { Intent() }, Options, legacyContacts: false);
            ProjectionCaptureAudit.Observer = (kind, count) => resumedCounts[kind] = resumedCounts.GetValueOrDefault(kind) + count;
            var paused = engine.BeginRiderIntegration(origin, Intent(), Options, stop);
            Assert.Null(paused.Completed);
            resumed = paused.Cursor!.Resume(origin.TrackState).Completed!;
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        Assert.Equal(Exact.Hash(continuous), Exact.Hash(resumed));
        Assert.Equal(continuousCounts.OrderBy(p => p.Key), resumedCounts.OrderBy(p => p.Key));
        Assert.Equal(continuous.Diagnostics.Single().ExecutedPath, resumed.Diagnostics.Single().ExecutedPath);
        var expectedSurface = Surface(origin); var actualSurface = Surface(origin);
        var expectedRider = origin.Riders.Single().ToMutableCopy(); var actualRider = origin.Riders.Single().ToMutableCopy();
        var expectedLog = new SimLog(); var actualLog = new SimLog();
        engine.Commit(continuous, new[] { expectedRider }, expectedSurface, expectedLog);
        engine.Commit(resumed, new[] { actualRider }, actualSurface, actualLog);
        Assert.Equal(Exact.Hash(new { expectedRider, Surface = expectedSurface.Snapshot(), expectedLog.Lines, expectedLog.SurfaceChanges }),
            Exact.Hash(new { expectedRider = actualRider, Surface = actualSurface.Snapshot(), Lines = actualLog.Lines, SurfaceChanges = actualLog.SurfaceChanges }));
    }

    [Fact]
    public void TwoPausesRetainExactAccumulatorsContextControlsAndFrozenRngAddress()
    {
        var origin = Origin(); var engine = Engine(); var intent = Intent();
        var continuous = engine.ResolveProduction(origin, new[] { intent }, Options, legacyContacts: false);
        var path = continuous.Diagnostics.Single().ExecutedPath!;
        var first = engine.BeginRiderIntegration(origin, intent, Options, 5).Cursor!;
        var boundaryHash = Exact.Hash(first.Boundary);
        Assert.Same(origin, first.Snapshot); Assert.Equal(intent, first.Intent);
        Assert.Same(origin.TrackState, first.SurfaceVersion);
        Assert.Equal(new SimulationStepContext(91, 37, 0, 7, 19, 1), first.Snapshot.Step);
        Assert.Equal(5, first.Boundary.CompletedSteps);
        Assert.Equal(path.Nodes.Take(6), first.Boundary.Nodes); Assert.Equal(path.Steps.Take(5), first.Boundary.Steps);
        Assert.Equal((float)first.Boundary.TimeSeconds, path.Nodes[5].ElapsedTimeSeconds);
        Assert.Equal((float)first.Boundary.DistanceMeters, path.Nodes[5].DistanceMeters);
        Assert.Equal((float)first.Boundary.CanonicalProgress, path.Nodes[5].SegmentProgress);
        Assert.Equal(path.Nodes[5].SpeedMetersPerSecond, first.Boundary.Speed);
        Assert.Equal(path.Nodes[5].LateralPosition, first.Boundary.LateralPosition);
        Assert.Equal(path.Nodes[5].Surface, first.Boundary.NodeSurface);
        Assert.NotEqual((double)(float)first.Boundary.TimeSeconds, first.Boundary.TimeSeconds);
        Assert.NotEqual((double)(float)first.Boundary.CanonicalProgress, first.Boundary.CanonicalProgress);
        Assert.Equal(1, first.Context.PositiveDriveFraction); Assert.Equal(SegmentOutcome.Ok, first.Context.Resolution.Outcome);
        var second = first.Resume(origin.TrackState, 13).Cursor!;
        Assert.Equal(18, second.Boundary.CompletedSteps);
        Assert.Same(first.Context, second.Context); Assert.Same(first.Snapshot, second.Snapshot);
        Assert.Equal(first.Boundary.Nodes, second.Boundary.Nodes.Take(6));
        Assert.Equal(boundaryHash, Exact.Hash(first.Boundary));
        Assert.Throws<RiderIntegrationRefusalException>(() => first.Resume(origin.TrackState));
        Assert.Equal(Exact.Hash(continuous), Exact.Hash(second.Resume(origin.TrackState).Completed));
        Assert.Throws<RiderIntegrationRefusalException>(() => second.Resume(origin.TrackState));
    }

    [Fact]
    public void WrongSurfaceVersionRefusesWithoutConsumingTheToken()
    {
        var origin = Origin(); var cursor = Engine().BeginRiderIntegration(origin, Intent(), Options, 5).Cursor!;
        var boundary = Exact.Hash(cursor.Boundary);
        var changed = Surface(origin);
        changed.ApplySurfaceDelta(7, 1, 0, .01f, 0, "test", 91, 37, new SimLog(false));
        Assert.Throws<RiderIntegrationRefusalException>(() => cursor.Resume(changed.Snapshot()));
        // Equal detached cells also belong to a different frozen world version.
        Assert.Throws<RiderIntegrationRefusalException>(() => cursor.Resume(Surface(origin).Snapshot()));
        Assert.Equal(boundary, Exact.Hash(cursor.Boundary));
        Assert.NotNull(cursor.Resume(origin.TrackState).Completed);
    }

    [Fact]
    public void PausedWindowPublishesNothingAndCompletedTraversalPublishesWearAndEventsOnce()
    {
        var origin = Origin(); var engine = Engine(); var window = new CommonTimeMotionWindow(engine, origin, Options);
        var live = new[] { origin.Riders.Single().ToMutableCopy() }; var surface = Surface(origin); var log = new SimLog();
        var before = Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges });
        window.StageSelectedMotionWithPause(Intent(), 37, 5);
        Assert.Equal(5, window.Continuations.Single().ExecutionCursor!.Boundary.CompletedSteps);
        Assert.Empty(window.Continuations.Single().Pending);
        Assert.Equal(origin.Riders.Single(), window.Continuations.Single().Speculative);
        Assert.Throws<RiderIntegrationRefusalException>(() => window.StageSelectedMotion(Intent(), 38));
        Assert.Throws<CommonTimeCommitException>(() => window.CommitFirstSegments(live, surface, log));
        window.ResumeSelectedMotion(1, origin.TrackState, 13);
        Assert.Equal(before, Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges }));
        Assert.Equal(5, window.CommittedWatermarkSeconds); Assert.Empty(window.CommittedHistory);
        var finished = window.ResumeSelectedMotion(1, origin.TrackState).Completed!;
        Assert.Null(window.Continuations.Single().ExecutionCursor);
        var expectedSurface = Surface(origin); var expectedLog = new SimLog();
        engine.Commit(finished, new[] { origin.Riders.Single().ToMutableCopy() }, expectedSurface, expectedLog);
        window.CommitFirstSegments(live, surface, log);
        Assert.Equal(Exact.Hash(expectedSurface.Snapshot()), Exact.Hash(surface.Snapshot()));
        Assert.Equal(expectedLog.SurfaceChanges, log.SurfaceChanges); Assert.Equal(expectedLog.Lines, log.Lines);
        Assert.Equal(RiderRaceStatus.Finished, live[0].Status); Assert.Single(window.CommittedHistory);
        var after = Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges });
        Assert.Throws<InvalidOperationException>(() => window.CommitFirstSegments(live, surface, log));
        Assert.Equal(after, Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges }));
    }

    [Fact]
    public void FourIndependentExecutionsAreDeterministicAcrossInputAndResumeOrder()
    {
        var fixture = Origin();
        var riders = Enumerable.Range(1, 4).Select(id => fixture.Riders[0] with
        { RiderId = id, Profile = RiderProfile.CreateDefault(id), Position = RiderPosition.Create(1, id % 2 == 0 ? 3 : 7, .137f, 8) }).ToArray();
        var forward = new CommonTimeMotionWindow(Engine(), new(fixture.Step, fixture.Track, fixture.TrackState, riders), Options);
        var reverse = new CommonTimeMotionWindow(Engine(), new(fixture.Step, fixture.Track, fixture.TrackState, riders.Reverse()), Options);
        foreach (var id in new[] { 1, 2, 3, 4 }) forward.StageSelectedMotionWithPause(Intent(id), 37, 5);
        foreach (var id in new[] { 4, 3, 2, 1 }) reverse.StageSelectedMotionWithPause(Intent(id), 37, 5);
        foreach (var id in new[] { 3, 1, 4, 2 }) forward.ResumeSelectedMotion(id, fixture.TrackState);
        foreach (var id in new[] { 2, 4, 1, 3 }) reverse.ResumeSelectedMotion(id, fixture.TrackState);
        Assert.Equal(Exact.Hash(forward.Continuations.OrderBy(c => c.Committed.RiderId)),
            Exact.Hash(reverse.Continuations.OrderBy(c => c.Committed.RiderId)));
    }

    [Fact]
    public void UnsupportedProductionPathsRefuseExplicitly()
    {
        var origin = Origin(); var engine = Engine();
        Assert.Throws<RiderIntegrationRefusalException>(() => engine.BeginRiderIntegration(Origin(5), Intent(), Options, 5));
        Assert.Throws<RiderIntegrationRefusalException>(() => engine.BeginRiderIntegration(origin, Intent(), Options with { IncidentFrequency = 1 }, 5));
        var fixedLine = new SimulationSnapshot(origin.Step, origin.Track, origin.TrackState,
            origin.Riders.Select(r => r with { LateralPosition = 1 }));
        Assert.Throws<RiderIntegrationRefusalException>(() => engine.BeginRiderIntegration(fixedLine,
            new(1, new RiderDecision(1)), Options, 5));
        Assert.Throws<RiderIntegrationRefusalException>(() => engine.BeginRiderIntegration(origin,
            new(1, new RiderDecision(2) { HoldLateralPosition = true }), Options with { EnableContestedSpaceResponses = true }, 5));
        Assert.Throws<RiderIntegrationRefusalException>(() => engine.BeginRiderIntegration(origin,
            new(1, new RiderDecision(2) { DriveControl = RiderDriveControl.LiftThrottle }), Options with { EnableContestedSpaceResponses = true }, 5));
        var standing = new SimulationSnapshot(origin.Step, origin.Track, origin.TrackState,
            origin.Riders.Select(r => r with { Speed = 0, Status = RiderRaceStatus.NotStarted }));
        Assert.Throws<RiderIntegrationRefusalException>(() => engine.BeginRiderIntegration(standing, Intent(), Options, 5));
    }
}
