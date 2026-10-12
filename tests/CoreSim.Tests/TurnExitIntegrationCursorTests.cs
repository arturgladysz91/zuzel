using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;
using RaceReadiness;
using Xunit;
using Xunit.Abstractions;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class TurnExitIntegrationCursorTests(ITestOutputHelper output)
{
    private static readonly HeatSimulationOptions Options = new() { Seed = 19, IncidentFrequency = 0 };
    private static SimulationEngine Engine() => new(new AdaptiveDecisionModel());
    private static TrackState Surface(SimulationSnapshot snapshot) => new(snapshot.TrackState.SegmentCount,
        snapshot.TrackState.LinesCount, snapshot.TrackState.GetSurface);
    private static SimulationSnapshot Origin(bool correction = false, bool apexInside = false)
    {
        var track = apexInside ? new Track(new TrackSegment[] { new(0, SegmentType.TurnExit), new(1, SegmentType.Straight) })
            : Track.CreateExample();
        var segment = apexInside ? 0 : 6; var lateral = apexInside ? .95f : correction ? 2.8f : .8f;
        var rider = new RiderSnapshot(1, RiderProfile.CreateDefault(1), RiderPosition.Create(1, segment, 0, track.Segments.Count),
            -1, LaneModel.ClampLane((int)MathF.Round(lateral)), lateral, apexInside ? 14 : 18, 0, RiderRaceStatus.Racing, 5, BikeSetup.Neutral, .5f, .5f);
        var surface = TrackState.CreateDefault(track).Snapshot();
        if (correction)
        {
            var phase = track.CornerTopology.Resolve(segment, 0, lateral, track.Geometry)!.Value;
            rider = rider with { Speed = ContinuousCornerEnvelope.Create(phase, lateral, track.Geometry,
                surface.SampleSurface(segment, lateral), rider.Profile.Skills, rider.ActiveSetup).SpeedMetersPerSecond(phase.CornerProgress) * 1.005f };
        }
        return new(new(91, 37, 0, segment, 19, 4), track, surface, new[] { rider });
    }

    [Theory]
    [InlineData(false, false, 5, 10)]
    [InlineData(true, false, 2, 13)]
    [InlineData(false, true, 2, 16)]
    public void TwoTurnExitPausesPreserveProductionPhysicsAndAllEnvelopeWork(bool correction, bool apexInside, int firstStop, int secondStop)
    {
        var origin = Origin(correction, apexInside); var engine = Engine();
        var intent = new RiderIntent(1, new RiderDecision(apexInside ? 1 : correction ? 0 : 2));
        var continuousCounts = new Dictionary<ProjectionMaterialization, int>();
        var resumedCounts = new Dictionary<ProjectionMaterialization, int>();
        ResolvedSimulationStep continuous; ResolvedSimulationStep resumed;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, count) => continuousCounts[kind] = continuousCounts.GetValueOrDefault(kind) + count;
            continuous = engine.ResolveProduction(origin, new[] { intent }, Options, legacyContacts: false);
            ProjectionCaptureAudit.Observer = (kind, count) => resumedCounts[kind] = resumedCounts.GetValueOrDefault(kind) + count;
            var first = engine.BeginRiderIntegration(origin, intent, Options, firstStop).Cursor!;
            Assert.Equal(firstStop, first.Boundary.CompletedSteps);
            var corner = first.Boundary.Corner!;
            Assert.Equal(continuous.Diagnostics.Single().ExecutedPath!.Nodes[0].EnvelopeSpeedMetersPerSecond, corner.InitialEnvelopeSpeed);
            Assert.Equal(first.Boundary.Nodes[^1].CornerProgress, corner.CornerProgress);
            Assert.Equal(continuous.Diagnostics.Single().CornerPhaseContext, first.Context.CornerPhase);
            var second = first.Resume(origin.TrackState, secondStop).Cursor!;
            Assert.Equal(firstStop + secondStop, second.Boundary.CompletedSteps);
            Assert.Same(corner.Cache, second.Boundary.Corner!.Cache);
            Assert.Equal(first.Boundary.Nodes, second.Boundary.Nodes.Take(firstStop + 1));
            Assert.Equal(first.Boundary.Steps, second.Boundary.Steps.Take(firstStop));
            if (apexInside)
            {
                Assert.Equal(continuous.Diagnostics.Single().ExecutedPath!.ApexNode!.LateralPosition, second.Boundary.Corner.ApexLateralPosition);
                Assert.NotEmpty(second.Boundary.Corner.Cache.Envelopes);
            }
            else Assert.Null(second.Boundary.Corner.ApexLateralPosition);
            resumed = second.Resume(origin.TrackState).Completed!;
            Assert.Throws<RiderIntegrationRefusalException>(() => first.Resume(origin.TrackState));
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        Assert.Equal(Exact.Hash(continuous), Exact.Hash(resumed));
        Assert.Equal(continuousCounts.OrderBy(p => p.Key), resumedCounts.OrderBy(p => p.Key));
        var path = continuous.Diagnostics.Single().ExecutedPath!;
        Assert.True(path.Steps.Sum(s => s.DriveDistanceMeters) > 0);
        if (correction) Assert.True(path.Steps.Sum(s => s.CorrectionDistanceMeters) > 0);
        AssertCommittedEquality(engine, continuous, resumed);
    }

    private static void AssertCommittedEquality(SimulationEngine engine, ResolvedSimulationStep continuous, ResolvedSimulationStep resumed)
    {
        var origin = continuous.Snapshot; var expected = origin.Riders.Single().ToMutableCopy(); var actual = origin.Riders.Single().ToMutableCopy();
        var expectedSurface = Surface(origin); var actualSurface = Surface(origin); var expectedLog = new SimLog(); var actualLog = new SimLog();
        engine.Commit(continuous, new[] { expected }, expectedSurface, expectedLog);
        engine.Commit(resumed, new[] { actual }, actualSurface, actualLog);
        Assert.Equal(Exact.Hash(expected), Exact.Hash(actual)); Assert.Equal(expected.ContactRecovery, actual.ContactRecovery);
        Assert.Equal(Exact.Hash(expectedSurface.Snapshot()), Exact.Hash(actualSurface.Snapshot()));
        Assert.Equal(expectedLog.Lines, actualLog.Lines); Assert.Equal(expectedLog.SurfaceChanges, actualLog.SurfaceChanges);
    }

    [Fact]
    public void TurnExitSurfaceMismatchDoesNotConsumeOrAlterPausedExecution()
    {
        var origin = Origin(); var intent = new RiderIntent(1, new RiderDecision(2)); var engine = Engine();
        var cursor = engine.BeginRiderIntegration(origin, intent, Options, 5).Cursor!;
        var before = Exact.Hash(cursor.Boundary); var changed = Surface(origin);
        changed.ApplySurfaceDelta(6, 1, 0, .01f, 0, "test", 91, 37, new SimLog(false));
        Assert.Throws<RiderIntegrationRefusalException>(() => cursor.Resume(changed.Snapshot()));
        Assert.Equal(before, Exact.Hash(cursor.Boundary));
        var resumed = cursor.Resume(origin.TrackState).Completed!;
        Assert.Equal(Exact.Hash(engine.ResolveProduction(origin, new[] { intent }, Options, legacyContacts: false)), Exact.Hash(resumed));
    }

    [Fact]
    public void TurnExitWindowPublishesWearEventsAndStatusOnlyAfterCompleteExecutionOnce()
    {
        var origin = Origin(); var engine = Engine(); var window = new CommonTimeMotionWindow(engine, origin, Options);
        var intent = new RiderIntent(1, new RiderDecision(2)); var live = new[] { origin.Riders[0].ToMutableCopy() };
        var surface = Surface(origin); var log = new SimLog();
        var before = Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges });
        window.StageSelectedMotionWithPause(intent, 37, 5);
        Assert.NotNull(window.Continuations.Single().ExecutionCursor!.Boundary.Corner);
        Assert.Throws<CommonTimeCommitException>(() => window.CommitFirstSegments(live, surface, log));
        window.ResumeSelectedMotion(1, origin.TrackState, 10);
        Assert.Equal(before, Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges }));
        Assert.Equal(5, window.CommittedWatermarkSeconds); Assert.Empty(window.CommittedHistory);
        var finished = window.ResumeSelectedMotion(1, origin.TrackState).Completed!;
        var expectedSurface = Surface(origin); var expectedLog = new SimLog(); var expected = origin.Riders[0].ToMutableCopy();
        engine.Commit(finished, new[] { expected }, expectedSurface, expectedLog);
        window.CommitFirstSegments(live, surface, log);
        Assert.Equal(Exact.Hash(expected), Exact.Hash(live[0]));
        Assert.Equal(Exact.Hash(expectedSurface.Snapshot()), Exact.Hash(surface.Snapshot()));
        Assert.Equal(expectedLog.Lines, log.Lines); Assert.Equal(expectedLog.SurfaceChanges, log.SurfaceChanges);
        Assert.Single(window.CommittedHistory);
        var after = Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges });
        Assert.Throws<InvalidOperationException>(() => window.CommitFirstSegments(live, surface, log));
        Assert.Equal(after, Exact.Hash(new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges }));
    }

    [Fact]
    public void IndependentTurnExitCursorsRemainExactUnderReversedInputAndResumeOrders()
    {
        var fixture = Origin();
        var riders = Enumerable.Range(1, 4).Select(id => fixture.Riders[0] with { RiderId = id,
            Profile = RiderProfile.CreateDefault(id), Position = RiderPosition.Create(1, id % 2 == 0 ? 2 : 6, 0, 8) }).ToArray();
        var forward = new CommonTimeMotionWindow(Engine(), new(fixture.Step, fixture.Track, fixture.TrackState, riders), Options);
        var reverse = new CommonTimeMotionWindow(Engine(), new(fixture.Step, fixture.Track, fixture.TrackState, riders.Reverse()), Options);
        foreach (var id in new[] { 1, 2, 3, 4 }) forward.StageSelectedMotionWithPause(new(id, new RiderDecision(2)), 37, 5);
        foreach (var id in new[] { 4, 3, 2, 1 }) reverse.StageSelectedMotionWithPause(new(id, new RiderDecision(2)), 37, 5);
        foreach (var id in new[] { 3, 1, 4, 2 }) forward.ResumeSelectedMotion(id, fixture.TrackState);
        foreach (var id in new[] { 2, 4, 1, 3 }) reverse.ResumeSelectedMotion(id, fixture.TrackState);
        Assert.Equal(Exact.Hash(forward.Continuations.OrderBy(c => c.Committed.RiderId)),
            Exact.Hash(reverse.Continuations.OrderBy(c => c.Committed.RiderId)));
    }

    [Fact]
    public void RealControl19CStopsAfterStep24BeforeUnresolvedContactInsideStep25()
    {
        var scenario = Catalog.All().Single(s => s.Id == "control");
        var (track, surface, live) = scenario.Create();
        var observer = new UntilStep8(); var decisions = new CaptureDecisions();
        var options = new HeatSimulationOptions { Seed = 19, EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = true,
            PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit };
        Assert.Throws<CaptureComplete>(() => new HeatSimulator(decisions).SimulateHeat(track, surface, live, options, 91, observer));
        var step7 = observer.Steps.Single(s => s.Snapshot.Step.StepNumber == 7);
        var step8 = observer.Steps.Single(s => s.Snapshot.Step.StepNumber == 8);
        var pair = Assert.Single(step8.Interaction!.PhysicalContactConsequences!.AppliedPairs);
        Assert.Equal(13.194772190598087, pair.FirstTouchCommonTimeSeconds);
        var diagnostic = step7.Diagnostics.Single(d => d.RiderId == 1);
        var motion = step7.Motions.Single(m => m.RiderId == 1);
        var before = motion.Nodes.TakeWhile(n => n.LocalTimeSeconds < pair.FirstTouchCommonTimeSeconds - motion.StartElapsedTimeSeconds).Count() - 1;
        Assert.Equal(SegmentType.TurnExit, step7.Snapshot.Segment.Type); Assert.NotNull(diagnostic.ExecutedPath);
        Assert.Equal(SegmentOutcome.Ok, step7.Changes.Single(c => c.RiderId == 1).Outcome);
        Assert.Null(step7.Snapshot.Rider(1).ContactRecovery); Assert.Null(diagnostic.PhysicalContactConsequence);
        var intent = decisions.Intents[(7, 1)];
        var selected = Assert.Single(step7.Interaction!.Episodes.SelectMany(e => e.SelectedResponses).Where(r => r.RiderId == 1));
        Assert.Equal(InteractionResponse.KeepIntent, selected.Response); Assert.Null(selected.DriveControl);
        Assert.False(selected.HoldLateralPosition); Assert.Null(selected.PhysicalTarget);
        Assert.Equal(intent.Decision.Trajectory, selected.Intent);
        var origin = new SimulationSnapshot(step7.Snapshot.Step, track, step7.Snapshot.TrackState, new[] { step7.Snapshot.Rider(1) });
        var engine = Engine(); var continuous = engine.ResolveProduction(origin, new[] { intent }, options, legacyContacts: false);
        // Verify the actual C execution, rather than substituting a convenient test intent.
        Assert.Equal(diagnostic, continuous.Diagnostics.Single()); Assert.Equal(motion, continuous.Motions.Single());
        Assert.Equal(step7.Changes.Single(c => c.RiderId == 1), continuous.Changes.Single());
        Assert.Equal(24, before);
        var cursor = engine.BeginRiderIntegration(origin, intent, options, before).Cursor!;
        Assert.Equal(24, cursor.Boundary.CompletedSteps);
        Assert.Equal(diagnostic.ExecutedPath!.Nodes.Take(25), cursor.Boundary.Nodes);
        Assert.Equal(diagnostic.ExecutedPath.Steps.Take(24), cursor.Boundary.Steps);
        var beforeTime = origin.Riders[0].ElapsedTimeSeconds + cursor.Boundary.TimeSeconds;
        Assert.Equal(13.16668989157769, beforeTime);
        Assert.True(beforeTime < pair.FirstTouchCommonTimeSeconds);
        Assert.Equal(motion.Nodes[24].LocalTimeSeconds, (float)cursor.Boundary.TimeSeconds);
        // This next step executes speculatively to show the bracket; it cannot publish
        // across touch. There is still no API that executes a fraction of this step.
        var next = cursor.Resume(origin.TrackState, 1).Cursor!;
        var afterTime = origin.Riders[0].ElapsedTimeSeconds + next.Boundary.TimeSeconds;
        Assert.Equal(13.21535040820075, afterTime);
        Assert.True(pair.FirstTouchCommonTimeSeconds < afterTime);
        Assert.Equal(25, next.Boundary.CompletedSteps);
        var resumed = next.Resume(origin.TrackState).Completed!;
        Assert.Equal(Exact.Hash(continuous), Exact.Hash(resumed)); AssertCommittedEquality(engine, continuous, resumed);
        Assert.Equal(CommonTimeCommitRefusal.PartialProductionCursorRequired,
            Assert.Throws<CommonTimeCommitException>(() => CommonTimeCommitGuard.Require(resumed.Changes,
                resumed.Changes.Single().ElapsedTimeSeconds, pair.FirstTouchCommonTimeSeconds)).Reason);
        output.WriteLine(FormattableString.Invariant($"Accepted=24; before={beforeTime:R}; touch={pair.FirstTouchCommonTimeSeconds:R}; after={afterTime:R}; remaining={pair.FirstTouchCommonTimeSeconds - beforeTime:R}; interval={afterTime - beforeTime:R}; progress={cursor.Boundary.CanonicalProgress:R}; distance={cursor.Boundary.DistanceMeters:R}; speed={cursor.Boundary.Speed:R}; lateral={cursor.Boundary.LateralPosition:R}"));
    }

    private sealed class CaptureComplete : Exception;
    private sealed class UntilStep8 : ISimulationStepObserver
    {
        internal List<ResolvedSimulationStep> Steps { get; } = new();
        public void OnStepResolved(ResolvedSimulationStep step)
        { Steps.Add(step); if (step.Snapshot.Step.StepNumber == 8) throw new CaptureComplete(); }
    }
    private sealed class CaptureDecisions : IRiderDecisionModel
    {
        private readonly AdaptiveDecisionModel _model = new();
        internal Dictionary<(int Step, int Rider), RiderIntent> Intents { get; } = new();
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => _model.Decide(segment, rider);
        public RiderDecision Decide(RiderDecisionContext context)
        {
            var decision = _model.Decide(context);
            Intents.Add((context.StepNumber, context.Rider.RiderId), new(context.Rider.RiderId, decision));
            return decision;
        }
    }
}
