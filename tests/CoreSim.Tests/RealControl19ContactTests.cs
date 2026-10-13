using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.Race;
using RaceReadiness;
using Xunit;
using Xunit.Abstractions;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class RealControl19ContactTests(ITestOutputHelper output)
{
    private static SimulationEngine Engine() => new(new AdaptiveDecisionModel());

    [Fact]
    public void OriginalStraightOptionsResumeExactlyAndPublishWearOnce()
    {
        var fixture = CaptureReal(); var origin = fixture.Origins[1]; var intent = fixture.Intents[1];
        Assert.Equal(1f, fixture.Options.IncidentFrequency);
        Assert.Equal(SegmentType.Straight, origin.Segment.Type);
        Assert.Equal(8, origin.Step.StepNumber); Assert.Equal(8, origin.Riders.Single().SegmentIndex);
        Assert.InRange(origin.Riders.Single().ElapsedTimeSeconds, 13.18744f, 13.18745f);
        var engine = Engine(); var counts = new Dictionary<ProjectionMaterialization, int>();
        var continuous = engine.ResolveProduction(origin, new[] { intent }, fixture.Options, legacyContacts: false);
        Assert.Equal(fixture.Steps[8].Diagnostics.Single(d => d.RiderId == 2).ExecutedPath,
            continuous.Diagnostics.Single().ExecutedPath);
        var expectedCounts = new Dictionary<ProjectionMaterialization, int>();
        ResolvedSimulationStep resumed;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, n) => expectedCounts[kind] = expectedCounts.GetValueOrDefault(kind) + n;
            continuous = engine.ResolveProduction(origin, new[] { intent }, fixture.Options, legacyContacts: false);
            ProjectionCaptureAudit.Observer = (kind, n) => counts[kind] = counts.GetValueOrDefault(kind) + n;
            var first = engine.BeginRiderIntegration(origin, intent, fixture.Options, 1).Cursor!;
            Assert.Same(origin, first.Snapshot); Assert.Equal(intent, first.Intent);
            Assert.Same(origin.TrackState, first.SurfaceVersion);
            var firstEnd = origin.Riders[0].ElapsedTimeSeconds + first.Boundary.TimeSeconds;
            Assert.True(firstEnd > fixture.ReferenceTouch);
            Assert.Throws<CommonTimeCommitException>(() => CommonTimeCommitGuard.Require(
                new[] { TwoRiderContactExecution.State(first) }, firstEnd, fixture.ReferenceTouch));
            output.WriteLine(FormattableString.Invariant($"Straight first step ends={firstEnd:R}; referenceTouch={fixture.ReferenceTouch:R}"));
            var second = first.Resume(origin.TrackState, 5).Cursor!;
            Assert.Same(first.Context, second.Context);
            Assert.Equal(first.Boundary.Steps, second.Boundary.Steps.Take(1));
            resumed = second.Resume(origin.TrackState).Completed!;
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        Assert.Equal(Exact.Hash(continuous), Exact.Hash(resumed));
        Assert.Equal(expectedCounts.OrderBy(p => p.Key), counts.OrderBy(p => p.Key));
        Assert.Equal(Exact.Hash(continuous), Exact.Hash(engine.ResolveProduction(origin, new[] { intent },
            fixture.Options with { IncidentFrequency = 0 }, legacyContacts: false)));
        object Commit(ResolvedSimulationStep step)
        {
            var live = origin.Riders.Single().ToMutableCopy();
            var surface = new TrackState(origin.TrackState.SegmentCount, origin.TrackState.LinesCount, origin.TrackState.GetSurface);
            var log = new SimLog(); engine.Commit(step, new[] { live }, surface, log);
            return new { live, Surface = surface.Snapshot(), log.Lines, log.SurfaceChanges };
        }
        Assert.Equal(Exact.Hash(Commit(continuous)), Exact.Hash(Commit(resumed)));
        output.WriteLine($"Straight: steps={resumed.Diagnostics.Single().ExecutedPath!.Steps.Count}; coupledEvaluations={counts.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation)}");
    }

    [Fact]
    public void OriginalPairRetainsDifferentWorldVersionsAndRefusesBeforeContactExecution()
    {
        var fixture = CaptureReal(); var a = fixture.Origins[0]; var b = fixture.Origins[1];
        Assert.Equal(SegmentType.TurnExit, a.Segment.Type); Assert.Equal(7, a.Step.StepNumber);
        Assert.Equal(SegmentType.Straight, b.Segment.Type); Assert.Equal(8, b.Step.StepNumber);
        Assert.NotEqual(a.Riders.Single().ElapsedTimeSeconds, b.Riders.Single().ElapsedTimeSeconds);
        Assert.NotSame(a.TrackState, b.TrackState);
        var changed = Enumerable.Range(0, a.TrackState.SegmentCount).SelectMany(segment =>
            Enumerable.Range(0, a.TrackState.LinesCount).Select(lane => (segment, lane)))
            .Where(cell => a.TrackState.GetSurface(cell.segment, cell.lane) != b.TrackState.GetSurface(cell.segment, cell.lane)).ToArray();
        Assert.NotEmpty(changed);
        Assert.Equal(45, changed.Length);
        Assert.Equal(.3800001f, a.TrackState.GetSurface(8, 1).Moisture);
        Assert.Equal(.36500013f, b.TrackState.GetSurface(8, 1).Moisture);
        // Step-8 surface includes ALL four completed step-7 passages plus the next weather tick.
        var evolved = new TrackState(a.TrackState.SegmentCount, a.TrackState.LinesCount, a.TrackState.GetSurface);
        var commitLog = new SimLog();
        Engine().Commit(fixture.Steps[7], fixture.Steps[7].Snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray(), evolved, commitLog);
        TrackEvolution.ApplyWeather(a.Track, evolved, fixture.Options.Weather, 91, 8, commitLog);
        Assert.Equal(Exact.Hash(b.TrackState), Exact.Hash(evolved.Snapshot()));
        var oldEnd = fixture.Steps[7].Changes.Single(c => c.RiderId == 1).ElapsedTimeSeconds;
        Assert.True(oldEnd > fixture.ReferenceTouch);
        // Capture the already executed production prefix. It stays on its original world version.
        var prefix = Engine().BeginRiderIntegration(a, fixture.Intents[0], fixture.Options, 24).Cursor!;
        Assert.Equal(fixture.Steps[7].Diagnostics.Single(d => d.RiderId == 1).ExecutedPath!.Steps.Take(24), prefix.Boundary.Steps);
        Assert.Equal(13.16668989157769, a.Riders.Single().ElapsedTimeSeconds + prefix.Boundary.TimeSeconds);
        var before = Exact.Hash(new { fixture.Origins, fixture.Intents, prefix.Boundary });
        var counts = new Dictionary<ProjectionMaterialization, int>();
        try
        {
            ProjectionCaptureAudit.Observer = (kind, n) => counts[kind] = counts.GetValueOrDefault(kind) + n;
            foreach (var reverse in new[] { false, true })
            {
                var origins = reverse ? fixture.Origins.Reverse().ToArray() : fixture.Origins;
                var intents = reverse ? fixture.Intents.Reverse().ToArray() : fixture.Intents;
                var refusal = Assert.Throws<RiderIntegrationRefusalException>(() =>
                    TwoRiderContactExecution.Execute(Engine(), origins, intents, fixture.Options));
                Assert.Contains("surface versions", refusal.Message);
            }
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        Assert.Empty(counts); Assert.Equal(before, Exact.Hash(new { fixture.Origins, fixture.Intents, prefix.Boundary }));
        Assert.NotNull(prefix.Resume(a.TrackState).Completed); // Refusal did not consume/replay the retained token.
        foreach (var origin in fixture.Origins)
            output.WriteLine(FormattableString.Invariant($"rider={origin.Riders[0].RiderId}; step={origin.Step.StepNumber}; segment={origin.Step.SegmentIndex}; start={origin.Riders[0].ElapsedTimeSeconds:R}; speed={origin.Riders[0].Speed:R}; lateral={origin.Riders[0].LateralPosition:R}; intent={fixture.Intents.Single(i => i.RiderId == origin.Riders[0].RiderId).Decision}"));
        foreach (var cell in changed.Where(cell => cell.segment is 7 or 8))
            output.WriteLine($"surface[{cell.segment},{cell.lane}]: {a.TrackState.GetSurface(cell.segment, cell.lane)} -> {b.TrackState.GetSurface(cell.segment, cell.lane)}");
        output.WriteLine($"originalRider1End={oldEnd:R}; referenceTouch={fixture.ReferenceTouch:R}; pairTimeTrials=0; pairCoupledEvaluations=0; changedCells={changed.Length}");
    }

    private static (SimulationSnapshot[] Origins, RiderIntent[] Intents, HeatSimulationOptions Options,
        IReadOnlyList<ResolvedSimulationStep> Steps, double ReferenceTouch) CaptureReal()
    {
        var (track, surface, riders) = Catalog.All().Single(s => s.Id == "control").Create();
        var capture = new Capture(); var decisions = new Decisions();
        var options = new HeatSimulationOptions { Seed = 19, EnableContestedSpaceResponses = true,
            EnablePhysicalContactConsequences = true, PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit };
        Assert.Throws<Done>(() => new HeatSimulator(decisions).SimulateHeat(track, surface, riders, options, 91, capture));
        var origins = new[] { (Step: 7, Rider: 1), (Step: 8, Rider: 2) }.Select(key =>
        {
            var source = capture.Steps[key.Step].Snapshot;
            return new SimulationSnapshot(source.Step, source.Track, source.TrackState, new[] { source.Rider(key.Rider) });
        }).ToArray();
        var intents = new[] { decisions.Intents[(7, 1)], decisions.Intents[(8, 2)] };
        foreach (var origin in origins)
        {
            var intent = intents.Single(i => i.RiderId == origin.Riders[0].RiderId);
            var selected = capture.Steps[origin.Step.StepNumber].Interaction!.Episodes.SelectMany(e => e.SelectedResponses)
                .Last(r => r.RiderId == intent.RiderId);
            Assert.Equal(InteractionResponse.KeepIntent, selected.Response);
            Assert.Equal(intent.Decision.Trajectory, selected.Intent); Assert.Null(selected.DriveControl);
            Assert.False(selected.HoldLateralPosition); Assert.Null(selected.PhysicalTarget);
        }
        var touch = capture.Steps[8].Interaction!.PhysicalContactConsequences!.AppliedPairs.Single().FirstTouchCommonTimeSeconds;
        Assert.Equal(13.194772190598087, touch);
        return (origins, intents, options, capture.Steps, touch);
    }

    private sealed class Done : Exception;
    private sealed class Capture : ISimulationStepObserver
    {
        internal List<ResolvedSimulationStep> Steps { get; } = new();
        public void OnStepResolved(ResolvedSimulationStep step)
        { Steps.Add(step); if (step.Snapshot.Step.StepNumber == 8) throw new Done(); }
    }
    private sealed class Decisions : IRiderDecisionModel
    {
        private readonly AdaptiveDecisionModel _model = new();
        internal Dictionary<(int, int), RiderIntent> Intents { get; } = new();
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => _model.Decide(segment, rider);
        public RiderDecision Decide(RiderDecisionContext context)
        {
            var decision = _model.Decide(context); Intents.Add((context.StepNumber, context.Rider.RiderId), new(context.Rider.RiderId, decision));
            return decision;
        }
    }
}
