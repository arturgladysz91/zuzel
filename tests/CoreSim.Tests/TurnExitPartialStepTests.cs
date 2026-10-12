using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using CoreSim.Setup;
using RaceReadiness;
using Xunit;
using Xunit.Abstractions;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class TurnExitPartialStepTests(ITestOutputHelper output)
{
    private static SimulationEngine Engine() => new(new AdaptiveDecisionModel());
    private static TrackState Surface(SimulationSnapshot snapshot) => new(snapshot.TrackState.SegmentCount,
        snapshot.TrackState.LinesCount, snapshot.TrackState.GetSurface);
    private static readonly HeatSimulationOptions Options = new() { Seed = 19, IncidentFrequency = 0 };
    private static SimulationSnapshot Origin(bool correction = false)
    {
        var track = Track.CreateExample(); var surface = TrackState.CreateDefault(track).Snapshot();
        var lateral = correction ? 2.8f : .8f;
        var rider = new RiderSnapshot(1, RiderProfile.CreateDefault(1), RiderPosition.Create(1, 6, 0, 8),
            -1, 1, lateral, 18, 0, RiderRaceStatus.Racing, 5, BikeSetup.Neutral, .5f, .5f);
        if (correction)
        {
            var phase = track.CornerTopology.Resolve(6, 0, lateral, track.Geometry)!.Value;
            rider = rider with { Speed = ContinuousCornerEnvelope.Create(phase, lateral, track.Geometry,
                surface.SampleSurface(6, lateral), rider.Profile.Skills, rider.ActiveSetup).SpeedMetersPerSecond(phase.CornerProgress) * 1.005f };
        }
        return new(new(91, 37, 0, 6, 19, 4), track, surface, new[] { rider });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialStepExecutesOnlyOneNewAcceptedStepWithOriginalEquations(bool correction)
    {
        var origin = Origin(correction); var engine = Engine(); var intent = new RiderIntent(1, new RiderDecision(correction ? 0 : 2));
        var probe = engine.BeginRiderIntegration(origin, intent, Options, 1).Cursor!;
        var full = probe.Resume(origin.TrackState, 1).Cursor!;
        var cursor = engine.BeginRiderIntegration(origin, intent, Options, 1).Cursor!;
        var prefix = cursor.Boundary;
        var requested = origin.Riders[0].ElapsedTimeSeconds + (prefix.TimeSeconds + full.Boundary.TimeSeconds) * .5d;
        var counts = new Dictionary<ProjectionMaterialization, int>(); RiderIntegrationCursor stopped;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, count) => counts[kind] = counts.GetValueOrDefault(kind) + count;
            stopped = cursor.SplitNextStepAtTime(origin.TrackState, requested);
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        var boundary = stopped.Boundary; var step = boundary.Steps[^1];
        Assert.Equal(prefix.Nodes, boundary.Nodes.Take(prefix.Nodes.Count));
        Assert.Equal(prefix.Steps, boundary.Steps.Take(prefix.Steps.Count));
        Assert.Equal(prefix.CompletedSteps + 1, boundary.CompletedSteps);
        Assert.Equal(1, counts.GetValueOrDefault(ProjectionMaterialization.ExecutedStep));
        Assert.Equal(1, counts.GetValueOrDefault(ProjectionMaterialization.ExecutedNode));
        foreach (var kind in new[] { ProjectionMaterialization.PhysicalEvaluation, ProjectionMaterialization.ResolvedStep,
            ProjectionMaterialization.RichMotion, ProjectionMaterialization.Event, ProjectionMaterialization.PrivateCommit })
            Assert.Equal(0, counts.GetValueOrDefault(kind));
        Assert.InRange(origin.Riders[0].ElapsedTimeSeconds + boundary.TimeSeconds - requested, 0, ExecutedPathTraversal.TimeToleranceSeconds);
        Assert.InRange(boundary.TimeSeconds, Math.BitIncrement(prefix.TimeSeconds), Math.BitDecrement(full.Boundary.TimeSeconds));
        Assert.InRange(boundary.DistanceMeters, Math.BitIncrement(prefix.DistanceMeters), Math.BitDecrement(full.Boundary.DistanceMeters));
        Assert.InRange(boundary.CanonicalProgress, Math.BitIncrement(prefix.CanonicalProgress), Math.BitDecrement(full.Boundary.CanonicalProgress));
        Assert.Equal(prefix.DistanceMeters + step.DistanceMeters, boundary.DistanceMeters);
        Assert.Equal(ExecutedPathTraversal.GeometricDistance(origin.Segment, origin.Track.Geometry,
            prefix.LateralPosition, boundary.LateralPosition, boundary.CanonicalProgress - prefix.CanonicalProgress), step.DistanceMeters);
        Assert.Equal(origin.TrackState.SampleSurface(6, step.SampledLateralPosition), step.SampledSurface);
        Assert.Equal(prefix.Corner!.InitialEnvelopeSpeed, boundary.Corner!.InitialEnvelopeSpeed);
        Assert.Same(prefix.Corner.Cache, boundary.Corner.Cache);
        Assert.Equal(full.Boundary.CanonicalProgress, boundary.PartialStep!.UnsplitEndCanonicalProgress);
        Assert.Equal(full.Boundary.TimeSeconds, boundary.PartialStep.UnsplitEndTimeSeconds);
        Assert.InRange(boundary.TimeSeconds - boundary.PartialStep.LowerTimeSeconds, 0, ExecutedPathTraversal.TimeToleranceSeconds);
        if (step.CorrectionDistanceMeters == 0)
        {
            var phase = origin.Track.CornerTopology.Resolve(6, (float)((prefix.CanonicalProgress + boundary.CanonicalProgress) * .5d),
                boundary.LateralPosition, origin.Track.Geometry)!.Value;
            var expected = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(prefix.Speed,
                step.DistanceMeters, step.ReferenceDriveForceNewtons, origin.Riders[0].ActiveSetup,
                ContinuousCornerEnvelope.DriveAvailability(phase.CornerProgress));
            Assert.Equal(expected, boundary.Speed);
            Assert.Equal(2d * step.DistanceMeters / ((double)prefix.Speed + expected), boundary.TimeSeconds - prefix.TimeSeconds, 12);
        }
        else Assert.True(step.CorrectionTimeSeconds > 0);
        Assert.Throws<RiderIntegrationRefusalException>(() => cursor.Resume(origin.TrackState));
        Assert.Throws<RiderIntegrationRefusalException>(() => cursor.SplitNextStepAtTime(origin.TrackState, requested));
        Assert.Throws<RiderIntegrationRefusalException>(() => stopped.Resume(origin.TrackState));
        Assert.Throws<RiderIntegrationRefusalException>(() => stopped.SplitNextStepAtTime(origin.TrackState, requested + .001));
        output.WriteLine($"Correction={correction}; endpointTrials={boundary.PartialStep.EndpointTrials}; coupledEvaluations={counts.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation)}");
    }

    [Fact]
    public void PartialStepIsDeterministicAndRejectsSurfaceVersionAndMissingBracket()
    {
        var origin = Origin(); var engine = Engine(); var intent = new RiderIntent(1, new RiderDecision(2));
        var first = engine.BeginRiderIntegration(origin, intent, Options, 3).Cursor!;
        var second = engine.BeginRiderIntegration(origin, intent, Options, 3).Cursor!;
        var requested = origin.Riders[0].ElapsedTimeSeconds + first.Boundary.TimeSeconds + .01;
        var before = Exact.Hash(first.Boundary);
        Assert.Throws<RiderIntegrationRefusalException>(() => first.SplitNextStepAtTime(Surface(origin).Snapshot(), requested));
        Assert.Equal(before, Exact.Hash(first.Boundary));
        var a = first.SplitNextStepAtTime(origin.TrackState, requested);
        var b = second.SplitNextStepAtTime(origin.TrackState, requested);
        Assert.Equal(Exact.Hash(a.Boundary), Exact.Hash(b.Boundary));
        var outside = engine.BeginRiderIntegration(origin, intent, Options, 3).Cursor!;
        Assert.Throws<RiderIntegrationRefusalException>(() => outside.SplitNextStepAtTime(origin.TrackState, requested + 10));
        Assert.Throws<RiderIntegrationRefusalException>(() => outside.Resume(origin.TrackState));
        var straightRider = origin.Riders[0] with { Position = RiderPosition.Create(1, 7, 0, 8) };
        var straight = new SimulationSnapshot(origin.Step with { SegmentIndex = 7 }, origin.Track, origin.TrackState, new[] { straightRider });
        var straightCursor = engine.BeginRiderIntegration(straight, intent, Options, 3).Cursor!;
        Assert.Throws<RiderIntegrationRefusalException>(() => straightCursor.SplitNextStepAtTime(origin.TrackState, requested));
    }

    [Fact]
    public void FourRiderPartialExecutionIsIndependentOfInputOrder()
    {
        var origin = Origin(); var engine = Engine();
        var riders = Enumerable.Range(1, 4).Select(id => origin.Riders[0] with { RiderId = id,
            Profile = RiderProfile.CreateDefault(id), Position = RiderPosition.Create(1, id % 2 == 0 ? 2 : 6, 0, 8) }).ToArray();
        Dictionary<int, ExecutedTraversalBoundary> Execute(IEnumerable<RiderSnapshot> input)
        {
            var result = new Dictionary<int, ExecutedTraversalBoundary>();
            foreach (var rider in input)
            {
                var snapshot = new SimulationSnapshot(origin.Step with { SegmentIndex = rider.SegmentIndex }, origin.Track,
                    origin.TrackState, new[] { rider });
                var cursor = engine.BeginRiderIntegration(snapshot, new(rider.RiderId, new RiderDecision(2)), Options, 3).Cursor!;
                result.Add(rider.RiderId, cursor.SplitNextStepAtTime(origin.TrackState,
                    rider.ElapsedTimeSeconds + cursor.Boundary.TimeSeconds + .01).Boundary);
            }
            return result;
        }
        Assert.Equal(Exact.Hash(Execute(riders).OrderBy(p => p.Key)), Exact.Hash(Execute(riders.Reverse()).OrderBy(p => p.Key)));
    }

    [Fact]
    public void FirstTouchSplitRefusesMissingCoverageAndAnAlreadyOverlappingPair()
    {
        var origin = Origin(); var engine = Engine(); var intent = new RiderIntent(1, new RiderDecision(2));
        var peer = origin.Riders[0] with { RiderId = 2, Profile = RiderProfile.CreateDefault(2) };
        var peerSnapshot = new SimulationSnapshot(origin.Step, origin.Track, origin.TrackState, new[] { peer });
        var motion = engine.ResolveProduction(peerSnapshot, new[] { new RiderIntent(2, new RiderDecision(2)) }, Options,
            legacyContacts: false).Motions.Single();
        var history = ResolvedBikePoses.FromMotion(motion, origin.Track,
            embedding: new TrackMetricEmbedding(origin.Track, deterministicArithmetic: true));
        var missing = engine.BeginRiderIntegration(origin, intent, Options, 3).Cursor!;
        Assert.Throws<RiderIntegrationRefusalException>(() => missing.SplitNextStepAtFirstTouch(origin.TrackState, history.Take(1).ToArray()));
        Assert.Throws<RiderIntegrationRefusalException>(() => missing.Resume(origin.TrackState));
        var overlap = engine.BeginRiderIntegration(origin, intent, Options, 3).Cursor!;
        Assert.Throws<RiderIntegrationRefusalException>(() => overlap.SplitNextStepAtFirstTouch(origin.TrackState, history));
        Assert.Throws<RiderIntegrationRefusalException>(() => overlap.Resume(origin.TrackState));
    }

    [Fact]
    public void RealControl19CExecutesToNewlyCertifiedTouchWithoutPublishingFuture()
    {
        var scenario = Catalog.All().Single(s => s.Id == "control"); var (track, surface, live) = scenario.Create();
        var capture = new Capture(); var decisions = new Decisions();
        var options = new HeatSimulationOptions { Seed = 19, EnableContestedSpaceResponses = true,
            EnablePhysicalContactConsequences = true, PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit };
        Assert.Throws<CaptureComplete>(() => new HeatSimulator(decisions).SimulateHeat(track, surface, live, options, 91, capture));
        var step7 = capture.Steps[7]; var step8 = capture.Steps[8];
        var oldTouch = step8.Interaction!.PhysicalContactConsequences!.AppliedPairs.Single().FirstTouchCommonTimeSeconds;
        Assert.Equal(13.194772190598087, oldTouch);
        var intent = decisions.Intents[(7, 1)]; var engine = Engine();
        var origin = new SimulationSnapshot(step7.Snapshot.Step, track, step7.Snapshot.TrackState, new[] { step7.Snapshot.Rider(1) });
        var embedding = new TrackMetricEmbedding(track, deterministicArithmetic: true);
        var opponent = capture.Steps.SelectMany(s => s.Motions).Where(m => m.RiderId == 2)
            .SelectMany(m => ResolvedBikePoses.FromMotion(m, track, embedding: embedding)).ToArray();
        var window = new CommonTimeMotionWindow(engine, origin, options);
        var cursor = window.StageSelectedMotionWithPause(intent, 7, 24).Cursor!; var prefix = cursor.Boundary;
        Assert.Equal(13.16668989157769, origin.Riders[0].ElapsedTimeSeconds + prefix.TimeSeconds);
        Assert.Equal(step7.Diagnostics.Single(d => d.RiderId == 1).ExecutedPath!.Nodes.Take(25), prefix.Nodes);
        var privateLive = new[] { origin.Riders[0].ToMutableCopy() }; var privateSurface = Surface(origin); var log = new SimLog();
        var unchanged = Exact.Hash(new { privateLive, Surface = privateSurface.Snapshot(), log.Lines, log.SurfaceChanges });
        var counts = new Dictionary<ProjectionMaterialization, int>(); RiderContactStepStop stop;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, count) => counts[kind] = counts.GetValueOrDefault(kind) + count;
            stop = window.SplitSelectedMotionAtFirstTouch(1, origin.TrackState, opponent);
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        var boundary = stop.Cursor.Boundary; var commonEnd = origin.Riders[0].ElapsedTimeSeconds + boundary.TimeSeconds;
        Assert.Equal(prefix.Nodes, boundary.Nodes.Take(25)); Assert.Equal(prefix.Steps, boundary.Steps.Take(24));
        Assert.Equal(25, boundary.CompletedSteps); Assert.Equal(26, boundary.Nodes.Count);
        Assert.InRange(boundary.PartialStep!.EndpointTrials, 2, ExecutedPathTraversal.BisectionIterations + 1);
        Assert.Equal(1, counts.GetValueOrDefault(ProjectionMaterialization.ExecutedStep));
        Assert.Equal(1, counts.GetValueOrDefault(ProjectionMaterialization.ExecutedNode));
        Assert.Equal(0, counts.GetValueOrDefault(ProjectionMaterialization.RichMotion));
        Assert.InRange(commonEnd, 13.16668989157769, 13.21535040820075);
        Assert.InRange(stop.FirstTouchSeconds, boundary.PartialStep!.LowerTimeSeconds + origin.Riders[0].ElapsedTimeSeconds - ExecutedPathTraversal.TimeToleranceSeconds,
            commonEnd);
        Assert.InRange(commonEnd - stop.FirstTouchSeconds, 0, ExecutedPathTraversal.TimeToleranceSeconds);
        Assert.All(stop.Verification.Intervals, row => Assert.True(row.NumericallyResolved));
        Assert.Contains(stop.Verification.Intervals, row => row.HasConflict);
        Assert.True(boundary.CanonicalProgress < boundary.PartialStep.UnsplitEndCanonicalProgress);
        Assert.True(boundary.Steps[^1].DistanceMeters < step7.Diagnostics.Single(d => d.RiderId == 1).ExecutedPath!.Steps[24].DistanceMeters);
        Assert.Same(stop.Cursor, window.Continuations.Single().ExecutionCursor);
        Assert.Equal(origin.Riders[0], window.Continuations.Single().Speculative);
        Assert.Equal(origin.Riders[0].ElapsedTimeSeconds, window.CommittedWatermarkSeconds); Assert.Empty(window.CommittedHistory);
        Assert.Throws<RiderIntegrationRefusalException>(() => cursor.Resume(origin.TrackState));
        Assert.Throws<RiderIntegrationRefusalException>(() => window.ResumeSelectedMotion(1, origin.TrackState));
        Assert.Throws<CommonTimeCommitException>(() => window.CommitFirstSegments(privateLive, privateSurface, log));
        Assert.Throws<CommonTimeCommitException>(() => window.CommitFirstSegments(privateLive, privateSurface, log));
        Assert.Equal(unchanged, Exact.Hash(new { privateLive, Surface = privateSurface.Snapshot(), log.Lines, log.SurfaceChanges }));
        var reverse = engine.BeginRiderIntegration(origin, intent, options, 24).Cursor!
            .SplitNextStepAtFirstTouch(origin.TrackState, opponent.Reverse().ToArray());
        Assert.Equal(Exact.Hash(boundary), Exact.Hash(reverse.Cursor.Boundary));
        Assert.Equal(Exact.Hash(stop.Verification), Exact.Hash(reverse.Verification));
        output.WriteLine(FormattableString.Invariant($"before={origin.Riders[0].ElapsedTimeSeconds + prefix.TimeSeconds:R}; oldTouch={oldTouch:R}; newTouch={stop.FirstTouchSeconds:R}; stop={commonEnd:R}; progress={boundary.CanonicalProgress:R}; distance={boundary.DistanceMeters:R}; speed={boundary.Speed:R}; lateral={boundary.LateralPosition:R}; discardedEnd={boundary.PartialStep.UnsplitEndCanonicalProgress:R}; discardedTime={origin.Riders[0].ElapsedTimeSeconds + boundary.PartialStep.UnsplitEndTimeSeconds:R}; trials={boundary.PartialStep.EndpointTrials}; coupledEvaluations={counts.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation)}; minimumGap={stop.Verification.Intervals.Min(r => r.MinimumSeparationMeters):R}"));
    }

    private sealed class CaptureComplete : Exception;
    private sealed class Capture : ISimulationStepObserver
    {
        internal List<ResolvedSimulationStep> Steps { get; } = new();
        public void OnStepResolved(ResolvedSimulationStep step)
        { Steps.Add(step); if (step.Snapshot.Step.StepNumber == 8) throw new CaptureComplete(); }
    }
    private sealed class Decisions : IRiderDecisionModel
    {
        private readonly AdaptiveDecisionModel _model = new();
        internal Dictionary<(int, int), RiderIntent> Intents { get; } = new();
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => _model.Decide(segment, rider);
        public RiderDecision Decide(RiderDecisionContext context)
        {
            var decision = _model.Decide(context); Intents.Add((context.StepNumber, context.Rider.RiderId), new(context.Rider.RiderId, decision)); return decision;
        }
    }
}
