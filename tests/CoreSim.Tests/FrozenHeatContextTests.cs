using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using RaceReadiness;
using Xunit;
using Xunit.Abstractions;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class FrozenHeatContextTests(ITestOutputHelper output)
{
    private sealed class Hold : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane); }

    [Fact]
    public void PlanningForecastsAlsoUseFrozenSurfaceWithoutPrivateWearOrBranchCopies()
    {
        var (track, surface, riders) = Catalog.All().Single(s => s.Id == "control").Create();
        var heat = new FrozenHeatContext(track, surface, new() { Seed = 19 }, 91);
        var snapshot = heat.Capture(riders, new(91, 0, 0, 0, 19, 4)); var before = Exact.Hash(heat.Surface);
        var counts = new Dictionary<ProjectionMaterialization, int>();
        var evaluator = new TrajectoryEvaluator(new RiderDecisionContext(snapshot, snapshot.Rider(1)));
        TrajectoryTraversal lean, rich;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, n) => counts[kind] = counts.GetValueOrDefault(kind) + n;
            lean = evaluator.Evaluate(new(1, 1, 1)); rich = evaluator.Evaluate(new(1, 1, 1), retainResolvedMotions: true);
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        Assert.Equal(0, counts.GetValueOrDefault(ProjectionMaterialization.PrivateCommit));
        Assert.Equal(0, counts.GetValueOrDefault(ProjectionMaterialization.TrackStateCopy));
        Assert.Equal(lean.PredictedTraversalTimeSeconds, rich.PredictedTraversalTimeSeconds);
        Assert.Equal(lean.PhysicalDistanceMeters, rich.PhysicalDistanceMeters);
        Assert.Equal(lean.PhaseEndpoints, rich.PhaseEndpoints); Assert.Equal(before, Exact.Hash(heat.Surface));
    }

    [Fact]
    public void EveryRiderAndLapReadsOneSurfaceAndWeatherSettlesOnceAfterHeat()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight, 20), new TrackSegment(1, SegmentType.Straight, 20) }, TrackGeometry.Default);
        var surface = TrackState.CreateDefault(track); var options = new HeatSimulationOptions { Seed = 19, IncidentFrequency = 0 };
        var heat = new FrozenHeatContext(track, surface, options, 91); var original = Exact.Hash(surface.Snapshot());
        var riders = Enumerable.Range(1, 4).Select(id => new RiderState(RiderProfile.CreateDefault(id), id - 1) { Speed = 18 }).ToArray();
        var engine = new SimulationEngine(new Hold()); var log = new SimLog();
        for (var step = 0; step < options.Laps * track.Segments.Count; step++)
        {
            var snapshot = heat.Capture(riders, new(91, step, step / 2, step % 2, 19, options.Laps));
            Assert.Same(heat.Surface, snapshot.TrackState);
            var result = engine.ResolveProduction(snapshot, engine.Decide(snapshot), options, legacyContacts: false);
            var report = ContestedSpaceResolver.Observe(result.Motions.SelectMany(m => ResolvedBikePoses.FromMotion(m, track)));
            Assert.DoesNotContain(report.Intervals, r => r.HasConflict || !r.NumericallyResolved);
            heat.AcceptComplete(result, riders, log, result.Changes.Min(c => (double)c.ElapsedTimeSeconds), null);
            Assert.Equal(original, Exact.Hash(surface.Snapshot())); Assert.Empty(log.SurfaceChanges);
        }
        Assert.Equal(32, heat.PassageCount); Assert.Equal(0, heat.WeatherUpdates);
        heat.SettleAfterHeat(surface, log);
        Assert.Equal(1, heat.WeatherUpdates); Assert.NotEqual(original, Exact.Hash(surface.Snapshot()));
        Assert.Equal(original, Exact.Hash(heat.Surface));
        Assert.Equal(10, log.SurfaceChanges.Count(d => d.Reason.StartsWith("weather:")));
        var settled = Exact.Hash(new { Surface = surface.Snapshot(), log.SurfaceChanges });
        Assert.Throws<InvalidOperationException>(() => heat.SettleAfterHeat(surface, log));
        Assert.Equal(settled, Exact.Hash(new { Surface = surface.Snapshot(), log.SurfaceChanges }));
        var next = new FrozenHeatContext(track, surface, options, 92);
        Assert.Equal(Exact.Hash(surface.Snapshot()), Exact.Hash(next.Surface)); Assert.NotSame(heat.Surface, next.Surface);
    }

    [Fact]
    public void PartialPassageIsDistanceScaledAndPrefixIsNotChargedAgainOrPublishedPastContact()
    {
        var track = Track.CreateExample(); var surface = TrackState.CreateDefault(track);
        var options = new HeatSimulationOptions { Seed = 19, IncidentFrequency = 1 };
        var heat = new FrozenHeatContext(track, surface, options, 91);
        var rider = new RiderState(RiderProfile.CreateDefault(1), 1) { Speed = 18, LateralPosition = .8f };
        rider.RestorePosition(RiderPosition.Create(1, 7, 0, track.Segments.Count)); rider.SetStatus(RiderRaceStatus.Racing);
        var origin = heat.Capture(new[] { rider }, new(91, 7, 0, 7, 19, options.Laps));
        var engine = new SimulationEngine(new Hold()); var intent = new RiderIntent(1, new RiderDecision(2));
        var zero = engine.BeginRiderIntegration(origin, intent, options, 0).Cursor!;
        Assert.Empty(zero.Boundary.Steps); Assert.Single(zero.Boundary.ExecutionEndpoints!);
        var first = zero.Resume(origin.TrackState, 1).Cursor!;
        heat.AcceptPrefix(first, first.Boundary.TimeSeconds, null);
        Assert.Throws<InvalidOperationException>(() => heat.AcceptPrefix(first, first.Boundary.TimeSeconds, null));
        var second = first.Resume(origin.TrackState, 1).Cursor!;
        Assert.Throws<CommonTimeCommitException>(() => heat.AcceptPrefix(second, second.Boundary.TimeSeconds, first.Boundary.TimeSeconds));
        Assert.Equal(1, heat.AcceptedNumericalSteps);
        heat.AcceptPrefix(second, second.Boundary.TimeSeconds, null);
        Assert.Equal(2, heat.AcceptedNumericalSteps); Assert.Equal(second.Boundary.DistanceMeters, heat.AcceptedDistanceMeters);
        Assert.Equal(1, heat.PassageCount); Assert.Equal(0f, surface.GetSurface(7, 1).Ruts);
        var entry = new float[5]; SimulationEngine.AddInterpolatedWearKernel(entry, .8f, 1);
        var expected = .004 * entry.Sum() * second.Boundary.DistanceMeters / 60;
        heat.SettleAfterHeat(surface, new SimLog());
        var ruts = Enumerable.Range(0, 5).Sum(l => (double)surface.GetSurface(7, l).Ruts);
        Assert.InRange(ruts, expected - 1e-9, expected + 1e-9);
        Assert.True(ruts < .004 * entry.Sum() * .04);
        output.WriteLine($"partialDistance={second.Boundary.DistanceMeters:R}; totalRuts={ruts:R}; fullSegmentBudget={.004 * entry.Sum():R}");
    }

    [Fact]
    public void PostHeatSettlementIsIndependentOfPassageAdmissionOrder()
    {
        object Run(bool reverse)
        {
            var track = Track.CreateExample(); var surface = TrackState.CreateDefault(track);
            var options = new HeatSimulationOptions { Seed = 19, IncidentFrequency = 0 };
            var heat = new FrozenHeatContext(track, surface, options, 91); var engine = new SimulationEngine(new Hold());
            foreach (var id in reverse ? new[] { 4, 3, 2, 1 } : new[] { 1, 2, 3, 4 })
            {
                var rider = new RiderState(RiderProfile.CreateDefault(id), id - 1) { Speed = 18, LateralPosition = (id - 1) * .8f };
                rider.RestorePosition(RiderPosition.Create(1, 7, 0, track.Segments.Count));
                var snapshot = heat.Capture(new[] { rider }, new(91, 7, 0, 7, 19, options.Laps));
                var resolved = engine.ResolveProduction(snapshot, engine.Decide(snapshot), options, legacyContacts: false);
                heat.AcceptComplete(resolved, new[] { rider }, new SimLog(false), resolved.Changes.Single().ElapsedTimeSeconds, null);
            }
            var log = new SimLog(); heat.SettleAfterHeat(surface, log);
            return new { Surface = surface.Snapshot(), log.SurfaceChanges, heat.AcceptedDistanceMeters };
        }
        Assert.Equal(Exact.Hash(Run(false)), Exact.Hash(Run(true)));
    }

    [Fact]
    public void FrozenControl19FourRiderForecastFindsEarliestUnresolvedPairBeforePublication()
    {
        var (track, surface, live) = Catalog.All().Single(s => s.Id == "control").Create();
        var options = new HeatSimulationOptions { Seed = 19, EnableContestedSpaceResponses = true,
            EnablePhysicalContactConsequences = true, PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit };
        var heat = new FrozenHeatContext(track, surface, options, 91); var original = Exact.Hash(heat.Surface);
        var engine = new SimulationEngine(new AdaptiveDecisionModel()); var tracker = new InteractionEpisodeTracker();
        var forecasts = new List<ResolvedSimulationStep>();
        var selectedIntents = new Dictionary<(int Step, int Rider), RiderIntent>();
        var captureCounts = new Dictionary<ProjectionMaterialization, int>();
        // Detached full segments are forecasts, never accepted movement or charged passages.
        var liveHash = Exact.Hash(live);
        var speculative = heat.Capture(live, new(91, 0, 0, 0, 19, options.Laps)).Riders.Select(r => r.ToMutableCopy()).ToList();
        try
        {
        ProjectionCaptureAudit.Observer = (kind, n) => captureCounts[kind] = captureCounts.GetValueOrDefault(kind) + n;
        for (var step = 0; step <= 8; step++)
        {
            var snapshot = heat.Capture(speculative, new(91, step, 0, step, 19, options.Laps));
            Assert.Same(heat.Surface, snapshot.TrackState);
            var originalIntents = engine.Decide(snapshot);
            var resolved = engine.Resolve(snapshot, originalIntents, options, tracker);
            foreach (var intent in originalIntents)
            {
                var selected = resolved.Interaction!.Episodes.SelectMany(e => e.SelectedResponses).LastOrDefault(r => r.RiderId == intent.RiderId);
                selectedIntents[(step, intent.RiderId)] = selected is null ? intent : intent with { Decision = intent.Decision with
                { TargetLane = selected.Intent.TargetFor(snapshot.Segment.Type), Trajectory = selected.Intent,
                    DriveControl = selected.DriveControl, HoldLateralPosition = selected.HoldLateralPosition, InteractionTarget = selected.PhysicalTarget } };
            }
            forecasts.Add(resolved);
            foreach (var change in resolved.Changes) SimulationEngine.CommitRiderChange(change, speculative.Single(r => r.RiderId == change.RiderId));
            resolved.CommitInteractionState?.Invoke();
        }
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        var embedding = new TrackMetricEmbedding(track, deterministicArithmetic: true);
        var poses = forecasts.SelectMany(f => f.Motions).SelectMany(m => ResolvedBikePoses.FromMotion(m, track, embedding: embedding)).ToArray();
        var report = CommonTimePoseHistory.Observe(poses);
        var first = report.Intervals.Where(r => r.HasConflict).OrderBy(r => r.FirstTouchCommonTimeSeconds).First();
        Assert.True(first.NumericallyResolved); Assert.Equal(original, Exact.Hash(surface.Snapshot())); Assert.Equal(0, heat.PassageCount);
        Assert.Equal(liveHash, Exact.Hash(live));
        Assert.Equal((1, 2), (first.RiderA, first.RiderB));
        Assert.Equal(SegmentType.TurnExit, first.SourceA!.SegmentType); Assert.Equal(SegmentType.Straight, first.SourceB!.SegmentType);
        // The requested experiment begins before Commit 7. Earlier coarse width-boundary
        // quarantines belong to the setup replay, not a certificate of an entire causal heat.
        var windowStart = forecasts[7].Snapshot.Riders.Max(r => (double)r.ElapsedTimeSeconds);
        Assert.Equal(6, report.Intervals.Where(r => r.IntervalEndSeconds > windowStart && r.IntervalStartSeconds < first.FirstTouchCommonTimeSeconds)
            .Select(r => (r.RiderA, r.RiderB)).Distinct().Count());
        Assert.DoesNotContain(report.Intervals.Where(r => r.IntervalEndSeconds > windowStart && r.IntervalStartSeconds < first.FirstTouchCommonTimeSeconds),
            r => !r.NumericallyResolved || r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous));
        output.WriteLine($"first={first.RiderA}-{first.RiderB}; touch={first.FirstTouchCommonTimeSeconds:R}; interval={first.IntervalStartSeconds:R}..{first.IntervalEndSeconds:R}; kind={first.Kind}; syncEvaluations={report.Work.NarrowPhaseEvaluations}; forecasts={forecasts.Sum(f => f.Changes.Count)}");
        foreach (var pair in report.Intervals.GroupBy(r => (r.RiderA, r.RiderB)))
            output.WriteLine($"pair={pair.Key}; first={pair.Where(r => r.HasConflict).Min(r => r.FirstTouchCommonTimeSeconds):R}");
        foreach (var step in new[] { 7, 8 })
            foreach (var r in forecasts[step].Snapshot.Riders)
                output.WriteLine($"step={step}; R{r.RiderId}; start={r.ElapsedTimeSeconds:R}; segment={r.SegmentIndex}; speed={r.Speed:R}; lateral={r.LateralPosition:R}");

        var initial = heat.Capture(live, new(91, 0, 0, 0, 19, options.Laps));
        var backgrounds = poses.Where(p => p.RiderId is 3 or 4).ToArray();
        var syncEvaluations = report.Work.NarrowPhaseEvaluations;
        (PairContactCertificationRefusal Failure, RiderIntegrationCursor R1, RiderIntegrationCursor R2) Execute(bool reverse)
        {
            var window = new CommonTimeMotionWindow(engine, initial, options);
            foreach (var step in Enumerable.Range(0, 7))
                foreach (var id in reverse ? new[] { 2, 1 } : new[] { 1, 2 })
                    window.StageSelectedMotion(selectedIntents[(step, id)], step);
            var secondSegment = window.StageSelectedMotion(selectedIntents[(7, 2)], 7);
            Assert.Equal(forecasts[7].Diagnostics.Single(d => d.RiderId == 2).ExecutedPath, secondSegment.Diagnostics.Single().ExecutedPath);
            var startR2 = secondSegment.Changes.Single().ElapsedTimeSeconds;
            var originalR1 = forecasts[7].Snapshot.Rider(1);
            var count = forecasts[7].Diagnostics.Single(d => d.RiderId == 1).ExecutedPath!.Nodes
                .Count(n => originalR1.ElapsedTimeSeconds + (double)n.ElapsedTimeSeconds < startR2) - 1;
            var r1 = window.StageSelectedMotionWithPause(selectedIntents[(7, 1)], 7, count).Cursor!;
            output.WriteLine($"selectedR2={selectedIntents[(8, 2)].Decision}");
            var r2 = window.StageSelectedMotionWithPause(selectedIntents[(8, 2)], 8, 0).Cursor!;
            Assert.Same(heat.Surface, r1.SurfaceVersion); Assert.Same(r1.SurfaceVersion, r2.SurfaceVersion);
            Assert.Equal(count, r1.Boundary.CompletedSteps); Assert.Empty(r2.Boundary.Steps);
            Assert.Equal(forecasts[7].Diagnostics.Single(d => d.RiderId == 1).ExecutedPath!.Steps.Take(count), r1.Boundary.Steps);
            Assert.True(r1.Snapshot.Riders[0].ElapsedTimeSeconds + r1.Boundary.TimeSeconds < startR2);
            Assert.NotEqual(r1.Snapshot.Riders[0].ElapsedTimeSeconds, r2.Snapshot.Riders[0].ElapsedTimeSeconds);
            Assert.Throws<CommonTimeCommitException>(() => window.CommitFirstSegments(live, surface, new SimLog()));
            var prefixHash = Exact.Hash(r1.Boundary);
            bool CertifyOthers(IReadOnlyList<PhysicalPoseInterval> actual, double through, bool continuation)
            {
                var from = continuation ? actual.GroupBy(p => p.RiderId).Max(g => g.Min(p => p.StartTimeSeconds)) : (double)startR2;
                var to = actual.GroupBy(p => p.RiderId).Min(g => g.Max(p => p.EndTimeSeconds));
                var clipped = backgrounds.Concat(actual).Where(p => p.StartTimeSeconds < to && p.EndTimeSeconds > from)
                    .Select(p => new Clip(p, Math.Max(from, p.StartTimeSeconds), Math.Min(to, p.EndTimeSeconds))).ToArray();
                foreach (var id in Enumerable.Range(1, 4))
                {
                    var covered = from;
                    foreach (var pose in clipped.Where(p => p.RiderId == id).OrderBy(p => p.StartTimeSeconds))
                    { if (pose.StartTimeSeconds > covered) return false; covered = pose.EndTimeSeconds; }
                    if (covered < to) return false;
                }
                var all = CommonTimePoseHistory.Observe(clipped); syncEvaluations += all.Work.NarrowPhaseEvaluations;
                if (all.FrameCoverageGaps.Count != 0 || all.Intervals.Any(r => !r.NumericallyResolved || r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)))
                    return false;
                if (continuation) return all.Intervals.All(r => r.MinimumSeparationLowerBoundMeters >= -GeometryNumerics.MinimumSeparationToleranceMeters
                    && r.MinimumSeparationMeters >= -GeometryNumerics.ContactDistanceMeters);
                var earliest = all.Intervals.Where(r => r.HasConflict).OrderBy(r => r.FirstTouchCommonTimeSeconds).First();
                return earliest.RiderA == 1 && earliest.RiderB == 2
                    && Math.Abs(earliest.FirstTouchCommonTimeSeconds!.Value - through) <= ExecutedPathTraversal.TimeToleranceSeconds;
            }
            var cursors = reverse ? new[] { r2, r1 } : new[] { r1, r2 };
            var result = Assert.Throws<PairContactCertificationRefusal>(() => TwoRiderContactExecution.Execute(engine, cursors, options, 1, CertifyOthers));
            Assert.Equal(prefixHash, Exact.Hash(r1.Boundary));
            Assert.Equal(r1.Boundary.Steps, result.Candidates[0].Steps.Take(count));
            Assert.All(cursors, c => Assert.Throws<RiderIntegrationRefusalException>(() => c.Resume(heat.Surface)));
            Assert.Equal(liveHash, Exact.Hash(live)); Assert.Equal(original, Exact.Hash(surface.Snapshot()));
            output.WriteLine($"retainedSteps={count}; prefixTime={r1.Snapshot.Riders[0].ElapsedTimeSeconds + r1.Boundary.TimeSeconds:R}; startR2={startR2:R}");
            return (result, r1, r2);
        }
        var counts = new Dictionary<ProjectionMaterialization, int>();
        (PairContactCertificationRefusal Failure, RiderIntegrationCursor R1, RiderIntegrationCursor R2) causal;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, n) => counts[kind] = counts.GetValueOrDefault(kind) + n;
            causal = Execute(false);
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        Assert.Contains(causal.Failure.Report.Intervals, r => !r.NumericallyResolved);
        Assert.Equal(0, heat.PassageCount); // No attempted boundary has been accepted or charged.
        heat.AcceptPrefix(causal.R1, causal.Failure.RequestedTimeSeconds, first.FirstTouchCommonTimeSeconds);
        heat.AcceptPrefix(causal.R2, causal.Failure.RequestedTimeSeconds, first.FirstTouchCommonTimeSeconds);
        Assert.Equal(causal.R1.Boundary.CompletedSteps, heat.AcceptedNumericalSteps);
        object Evidence(PairContactCertificationRefusal r) => new { r.RequestedTimeSeconds, r.Candidates, r.Report, r.PairTimeTrials };
        var firstSyncCost = syncEvaluations;
        Assert.Equal(Exact.Hash(Evidence(causal.Failure)), Exact.Hash(Evidence(Execute(true).Failure)));
        foreach (var i in new[] { 0, 1 })
        {
            var b = causal.Failure.Candidates[i]; var origin = i == 0 ? causal.R1.Snapshot : causal.R2.Snapshot;
            Assert.InRange(origin.Riders[0].ElapsedTimeSeconds + b.TimeSeconds - causal.Failure.RequestedTimeSeconds, 0, ExecutedPathTraversal.TimeToleranceSeconds);
            output.WriteLine($"unaccepted R{i + 1}: time={origin.Riders[0].ElapsedTimeSeconds + b.TimeSeconds:R}; progress={b.CanonicalProgress:R}; distance={b.DistanceMeters:R}; lateral={b.LateralPosition:R}; speed={b.Speed:R}");
        }
        output.WriteLine($"refusedAt={causal.Failure.RequestedTimeSeconds:R}; observedTouch={causal.Failure.Report.Intervals.Where(r => r.HasConflict).Min(r => r.FirstTouchCommonTimeSeconds):R}; pairTrials={causal.Failure.PairTimeTrials}; physicalEvaluations={counts.GetValueOrDefault(ProjectionMaterialization.PhysicalEvaluation)}; coupledEvaluations={counts.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation)}; capturePhysicalEvaluations={captureCounts.GetValueOrDefault(ProjectionMaterialization.PhysicalEvaluation)}; captureCoupledEvaluations={captureCounts.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation)}; syncEvaluations={firstSyncCost}; pairNarrowPhase={causal.Failure.Report.Work.NarrowPhaseEvaluations}; unacceptedCaptureSegments=36; discardedPairForecasts=2; discardedPairTimeTrials={causal.Failure.PairTimeTrials}; acceptedSteps={heat.AcceptedNumericalSteps}");
    }

    private sealed class Clip(PhysicalPoseInterval source, double start, double end) : PhysicalPoseInterval(source.RiderId,
        source.FrameId, start, end, source.Dimensions, source.StartsAtDiscontinuity && start == source.StartTimeSeconds, source.Source, source.SupportsLapWrap)
    {
        internal override bool DeterministicArithmetic => source.DeterministicArithmetic;
        public override PhysicalBikePose Sample(double time) { Fraction(time); return source.Sample(time); }
        internal override BikePoseValue SampleValue(double time) { Fraction(time); return source.SampleValue(time); }
        public override PoseRateBounds RateBounds(double from, double to) { Fraction(from); Fraction(to); return source.RateBounds(from, to); }
    }
}
