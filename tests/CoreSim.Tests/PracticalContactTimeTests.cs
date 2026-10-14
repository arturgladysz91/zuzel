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
public sealed class PracticalContactTimeTests(ITestOutputHelper output)
{
    [Fact]
    public void RealControl19ComparesPointOneOneAndTenMicrosecondsOnExecutedGeometry()
    {
        var (track, surface, live) = Catalog.All().Single(s => s.Id == "control").Create();
        var options = new HeatSimulationOptions { Seed = 19, EnableContestedSpaceResponses = true,
            EnablePhysicalContactConsequences = true, PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit };
        var heat = new FrozenHeatContext(track, surface, options, 91);
        var engine = new SimulationEngine(new AdaptiveDecisionModel());
        var initial = heat.Capture(live, new(91, 0, 0, 0, 19, options.Laps));
        var original = Exact.Hash(new { live, Surface = surface.Snapshot() });
        var speculative = initial.Riders.Select(r => r.ToMutableCopy()).ToList();
        var forecasts = new List<ResolvedSimulationStep>();
        var selected = new Dictionary<(int Step, int Rider), RiderIntent>();
        var cold = new Dictionary<ProjectionMaterialization, int>();
        var tracker = new InteractionEpisodeTracker();
        try
        {
            ProjectionCaptureAudit.Observer = (kind, n) => cold[kind] = cold.GetValueOrDefault(kind) + n;
            for (var step = 0; step <= 8; step++)
            {
                var snapshot = heat.Capture(speculative, new(91, step, 0, step, 19, options.Laps));
                var intents = engine.Decide(snapshot); var resolved = engine.Resolve(snapshot, intents, options, tracker);
                foreach (var intent in intents)
                {
                    var response = resolved.Interaction!.Episodes.SelectMany(e => e.SelectedResponses).LastOrDefault(r => r.RiderId == intent.RiderId);
                    selected[(step, intent.RiderId)] = response is null ? intent : intent with { Decision = intent.Decision with
                    { TargetLane = response.Intent.TargetFor(snapshot.Segment.Type), Trajectory = response.Intent,
                        DriveControl = response.DriveControl, HoldLateralPosition = response.HoldLateralPosition, InteractionTarget = response.PhysicalTarget } };
                }
                forecasts.Add(resolved);
                foreach (var change in resolved.Changes) SimulationEngine.CommitRiderChange(change, speculative.Single(r => r.RiderId == change.RiderId));
                resolved.CommitInteractionState?.Invoke();
            }
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        var embedding = new TrackMetricEmbedding(track, deterministicArithmetic: true);
        var history = CommonTimePoseHistory.Stitch(forecasts.SelectMany(f => f.Motions)
            .SelectMany(m => ResolvedBikePoses.FromMotion(m, track, embedding: embedding)));
        var backgrounds = history.Where(p => p.RiderId is 3 or 4).ToArray();
        var from = forecasts[7].Snapshot.Riders.Max(r => (double)r.ElapsedTimeSeconds);
        var setupGeometry = ContestedSpaceResolver.Observe(history);
        Assert.Equal((1, 2), setupGeometry.Intervals.Where(r => r.HasConflict).OrderBy(r => r.FirstTouchCommonTimeSeconds)
            .Select(r => (r.RiderA, r.RiderB)).First());

        var preparation = new Dictionary<ProjectionMaterialization, int>();
        var window = new CommonTimeMotionWindow(engine, initial, options);
        RiderIntegrationCursor masterR1, masterR2;
        ResolvedSimulationStep priorR2;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, n) => preparation[kind] = preparation.GetValueOrDefault(kind) + n;
            foreach (var step in Enumerable.Range(0, 7))
                foreach (var id in new[] { 1, 2 }) window.StageSelectedMotion(selected[(step, id)], step);
            priorR2 = window.StageSelectedMotion(selected[(7, 2)], 7);
            masterR1 = window.StageSelectedMotionWithPause(selected[(7, 1)], 7, 24).Cursor!;
            masterR2 = window.StageSelectedMotionWithPause(selected[(8, 2)], 8, 0).Cursor!;
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        var priorPoses = ResolvedBikePoses.FromMotion(priorR2.Motions.Single(), track, embedding: embedding);
        var entryJump = forecasts[8].Motions.Single(m => m.RiderId == 2).EntryBoundary!.HasPhysicalOffsetDiscontinuity;
        var masterHash = Exact.Hash(new[] { masterR1.Boundary, masterR2.Boundary });

        Attempt Run(double tolerance, bool reverse = false, bool rejectOther = false)
        {
            var counts = new Dictionary<ProjectionMaterialization, int>();
            var searches = new List<SpaceSearchDiagnostic>();
            var allReports = new List<(bool Continuation, ContestedSpaceReport Report)>();
            TwoRiderContactResult? result = null; PairContactCertificationRefusal? failure = null;
            RiderIntegrationCursor r1, r2;
            try
            {
                ProjectionCaptureAudit.Observer = (kind, n) => counts[kind] = counts.GetValueOrDefault(kind) + n;
                r1 = masterR1.ForkFrozenExperiment(); r2 = masterR2.ForkFrozenExperiment();
                Assert.Same(masterR1.Boundary, r1.Boundary); Assert.Same(masterR2.Boundary, r2.Boundary);
                Assert.Empty(counts);
                Assert.Equal(forecasts[7].Diagnostics.Single(d => d.RiderId == 1).ExecutedPath!.Steps.Take(24), r1.Boundary.Steps);
                var retained = Exact.Hash(new[] { r1.Boundary, r2.Boundary });
                bool Others(IReadOnlyList<PhysicalPoseInterval> pair, double through, bool continuation)
                {
                    var begin = continuation ? pair.GroupBy(p => p.RiderId).Max(g => g.Min(p => p.StartTimeSeconds)) : from;
                    var end = pair.GroupBy(p => p.RiderId).Min(g => g.Max(p => p.EndTimeSeconds));
                    var clipped = CommonTimePoseHistory.Stitch(backgrounds.Concat(pair)
                        .Concat(continuation ? Array.Empty<PhysicalPoseInterval>() : priorPoses)
                        .Where(p => p.StartTimeSeconds < end && p.EndTimeSeconds > begin)
                        .Select(p => new Clip(p, Math.Max(begin, p.StartTimeSeconds), Math.Min(end, p.EndTimeSeconds),
                            entryJump && p.RiderId == 2 && p.StartTimeSeconds == r2.Snapshot.Riders[0].ElapsedTimeSeconds)));
                    foreach (var id in Enumerable.Range(1, 4))
                    {
                        var covered = begin;
                        foreach (var p in clipped.Where(p => p.RiderId == id).OrderBy(p => p.StartTimeSeconds))
                        { if (p.StartTimeSeconds > covered) return false; covered = p.EndTimeSeconds; }
                        if (covered < end) return false;
                    }
                    var report = ContestedSpaceResolver.ObserveExecutedContactWindow(clipped);
                    allReports.Add((continuation, report)); Assert.Equal(6, report.Work.RiderPairs);
                    if (report.FrameCoverageGaps.Count != 0 || report.Intervals.Any(r => !r.NumericallyResolved
                        || r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)
                        || r.MinimumSeparationLowerBoundMeters < -GeometryNumerics.ContactDistanceMeters)) return false;
                    if (report.Intervals.Any(r => r.HasConflict && (r.RiderA, r.RiderB) != (1, 2))) return false;
                    if (continuation) return true;
                    if (rejectOther) return false;
                    var first = report.Intervals.Where(r => r.HasConflict).OrderBy(r => r.FirstTouchCommonTimeSeconds).First();
                    return (first.RiderA, first.RiderB) == (1, 2)
                        && Math.Abs(first.FirstTouchCommonTimeSeconds!.Value - through) <= GeometryNumerics.TimeToleranceSeconds;
                }
                SpaceSearchDiagnostics.Observer = searches.Add;
                try
                {
                    var cursors = reverse ? new[] { r2, r1 } : new[] { r1, r2 };
                    result = tolerance == TwoRiderContactExecution.MaximumEventTimeToleranceSeconds
                        ? TwoRiderContactExecution.ExecutePractical(engine, cursors, options, 1, Others)
                        : TwoRiderContactExecution.Execute(engine, cursors, options, 1, Others, tolerance);
                }
                catch (PairContactCertificationRefusal refusal) { failure = refusal; }
                Assert.Equal(retained, Exact.Hash(new[] { r1.Boundary, r2.Boundary }));
                Assert.Throws<CommonTimeCommitException>(() => window.CommitFirstSegments(live, surface, new SimLog()));
            }
            finally { ProjectionCaptureAudit.Observer = null; SpaceSearchDiagnostics.Observer = null; }
            Assert.Equal(original, Exact.Hash(new { live, Surface = surface.Snapshot() })); Assert.Equal(0, heat.PassageCount);
            Assert.Equal(masterHash, Exact.Hash(new[] { masterR1.Boundary, masterR2.Boundary }));
            var pairCoupled = counts.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation);
            foreach (var (kind, n) in preparation) counts[kind] = counts.GetValueOrDefault(kind) + n;
            var attempt = new Attempt(result, failure, r1, r2, counts, searches, allReports);
            var touch = result?.ContactTimeSeconds ?? failure!.Report.Intervals.Where(r => r.HasConflict).Min(r => r.FirstTouchCommonTimeSeconds)!.Value;
            var bounds = result?.Before.Select(c => c.Boundary).ToArray() ?? failure!.Candidates;
            var times = bounds.Select((b, i) => (i == 0 ? r1 : r2).Snapshot.Riders[0].ElapsedTimeSeconds + b.TimeSeconds).ToArray();
            var actual = bounds.SelectMany((b, i) => TwoRiderContactExecution.Poses((i == 0 ? r1 : r2).Snapshot, b, embedding)).ToArray();
            var displacement = times.Select((end, i) =>
            {
                var span = actual.Last(p => p.RiderId == i + 1 && p.StartTimeSeconds <= touch && p.EndTimeSeconds >= touch);
                return (span.Sample(span.EndTimeSeconds).Position - span.Sample(touch).Position).Length;
            }).Max();
            output.WriteLine($"tolerance={tolerance:R}; accepted={result is not null}; touch={touch:R}; maxTimeGap={times.Max() - touch:R}; maxCenterDisplacement={displacement:R}; trials={result?.PairTimeTrials ?? failure!.PairTimeTrials}; physics={counts.GetValueOrDefault(ProjectionMaterialization.PhysicalEvaluation)}; coupledIncludingSharedPreparation={counts.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation)}; pairCoupled={pairCoupled}; narrow={searches.Sum(s => s.Evaluations)}; splits={searches.Sum(s => s.Evaluations - 2)}; otherNarrow={allReports.Sum(r => r.Report.Work.NarrowPhaseEvaluations)}; otherSplits={allReports.Sum(r => r.Report.Work.AdaptiveSubdivisions)}; continuation={result?.ContinuationCertified}; reason={failure?.Message}");
            if (result is not null)
            {
                output.WriteLine($"effects={string.Join(';', result.Plan.Riders.Select(r => $"R{r.RiderId}:{r.PreContactSpeed:R}->{r.PostContactSpeed:R},severity={r.Severity},loss={r.ControlLoss01:R}"))}; postMin={result.ContinuationGeometry.Intervals.Select(r => (double?)r.MinimumSeparationMeters).Min():R}; postLower={result.ContinuationGeometry.Intervals.Select(r => (double?)r.MinimumSeparationLowerBoundMeters).Min():R}; agreement={result.TimeAgreement}; continuationRefusal={result.ContinuationRefusal}");
                Assert.Single(result.Plan.AppliedPairs); Assert.True(result.Ownership.Consumed);
                Assert.False(result.Ownership.CanApply(result.ContactTimeSeconds, result.CertifiedThroughSeconds));
                Assert.All(result.Before.Select((c, i) => c.Snapshot.Riders[0].ElapsedTimeSeconds + c.Boundary.TimeSeconds), t => Assert.InRange(t - touch, 0, tolerance));
                Assert.NotNull(result.ExecutedWindowGeometry);
                Assert.All(result.ExecutedWindowGeometry.Intervals, r => { Assert.True(r.NumericallyResolved); Assert.True(r.MinimumSeparationLowerBoundMeters >= -GeometryNumerics.ContactDistanceMeters); });
                output.WriteLine($"eventWindowMin={result.ExecutedWindowGeometry.Intervals.Min(r => r.MinimumSeparationMeters):R}; eventWindowLower={result.ExecutedWindowGeometry.Intervals.Min(r => r.MinimumSeparationLowerBoundMeters):R}; eventNarrow={result.ExecutedWindowGeometry.Work.NarrowPhaseEvaluations}; postNarrow={result.ContinuationGeometry.Work.NarrowPhaseEvaluations}");
                Assert.All(result.After, c => Assert.Throws<RiderIntegrationRefusalException>(() => c.Resume(heat.Surface)));
                if (result.After.Count == 0)
                {
                    Assert.Contains(result.AfterEvent, r => r.Status == RiderRaceStatus.Crashed && r.Speed == 0);
                    Assert.NotNull(result.ContinuationRefusal);
                    Assert.False(result.ContinuationCertified);
                    Assert.All(result.Before, c => Assert.Throws<RiderIntegrationRefusalException>(() => c.ResumeAfterContact(result.Plan.Riders.Single(r => r.RiderId == c.Snapshot.Riders[0].RiderId), 1, tolerance)));
                }
                else foreach (var after in result.After)
                {
                    var before = result.Before.Single(c => c.Snapshot.Riders[0].RiderId == after.Snapshot.Riders[0].RiderId);
                    Assert.Equal(before.Boundary.Steps, after.Boundary.Steps.Take(before.Boundary.CompletedSteps));
                    Assert.Equal(1, after.Boundary.CompletedSteps - before.Boundary.CompletedSteps);
                    var effect = result.Plan.Riders.Single(r => r.RiderId == after.Snapshot.Riders[0].RiderId);
                    Assert.Equal(effect.PostContactSpeed, after.Boundary.ExecutionEndpoints![before.Boundary.CompletedSteps].Speed);
                    output.WriteLine($"post R{effect.RiderId}: time={after.Snapshot.Riders[0].ElapsedTimeSeconds + after.Boundary.TimeSeconds:R}; distance={after.Boundary.DistanceMeters - before.Boundary.DistanceMeters:R}; speed={after.Boundary.Speed:R}; recoveryDrive={effect.Recovery?.DriveAvailability01:R}; recoveryLateral={effect.Recovery?.LateralAuthority01:R}");
                }
            }
            return attempt;
        }

        var strict = Run(1e-7); Assert.NotNull(strict.Failure); Assert.Contains("Actual event states", strict.Failure.Message);
        Assert.Equal(28, strict.Failure.PairTimeTrials);
        var one = Run(1e-6);
        var ten = Run(1e-5);
        Assert.NotNull(one.Result); Assert.NotNull(ten.Result);
        Assert.True(ten.Result.ContinuationCertified); Assert.Single(ten.Result.After);
        Assert.Equal(2, ten.Result.After.Single().Snapshot.Riders[0].RiderId);
        Assert.Equal(PhysicalContactSeverity.Crash, ten.Result.Plan.Riders.Single(r => r.RiderId == 1).Severity);
        Assert.Equal(PhysicalContactSeverity.MajorSave, ten.Result.Plan.Riders.Single(r => r.RiderId == 2).Severity);
        Assert.All(ten.Others, r => Assert.Equal(0, r.Report.Work.UnresolvedIntervals));
        var reversed = Run(1e-5, true);
        object Evidence(Attempt a) => new { a.Result!.ContactTimeSeconds, Before = a.Result.Before.Select(c => c.Boundary),
            After = a.Result.After.Select(c => c.Boundary), a.Result.AfterEvent, a.Result.Plan, a.Result.ContactGeometry,
            a.Result.ExecutedWindowGeometry, a.Result.ContinuationGeometry, a.Result.ContinuationCertified,
            a.Result.CertifiedThroughSeconds, a.Result.PairTimeTrials };
        Assert.Equal(Exact.Hash(Evidence(ten)), Exact.Hash(Evidence(reversed)));
        var blocked = Run(1e-5, rejectOther: true);
        Assert.Null(blocked.Result); Assert.NotNull(blocked.Failure); Assert.Contains("Another rider", blocked.Failure.Message);
        Assert.All(new[] { blocked.R1, blocked.R2 }, c => Assert.Throws<RiderIntegrationRefusalException>(() => c.Resume(heat.Surface)));
        // The heat ledger charges only admitted distance, once. No attempted
        // forecast or stationary terminal occupancy receives passage wear.
        var admittedThrough = ten.Result.CertifiedThroughSeconds;
        heat.AcceptPrefix(ten.R1, admittedThrough, ten.Result.ContactTimeSeconds);
        heat.AcceptPrefix(ten.R2, admittedThrough, ten.Result.ContactTimeSeconds);
        foreach (var c in ten.Result.Before) heat.AcceptPrefix(c, admittedThrough, null);
        foreach (var c in ten.Result.After) heat.AcceptPrefix(c, admittedThrough, null);
        Assert.Equal(2, heat.PassageCount); Assert.Equal(27, heat.AcceptedNumericalSteps);
        Assert.Equal(ten.Result.Before.Single(c => c.Snapshot.Riders[0].RiderId == 1).Boundary.DistanceMeters
            + ten.Result.After.Single().Boundary.DistanceMeters, heat.AcceptedDistanceMeters);
        Assert.Throws<InvalidOperationException>(() => heat.AcceptPrefix(ten.Result.After.Single(), admittedThrough, null));
        Assert.Equal(original, Exact.Hash(new { live, Surface = surface.Snapshot() })); Assert.Equal(0, heat.WeatherUpdates);
        Assert.All(ten.Result.Plan.Riders, effect => Assert.Throws<InvalidOperationException>(() => PhysicalContactConsequenceResolver.Apply(
            ten.Result.AfterEvent.Single(r => r.RiderId == effect.RiderId), effect)));
        masterR1.Seal(); masterR2.Seal();
        output.WriteLine($"coldPhysics={cold.GetValueOrDefault(ProjectionMaterialization.PhysicalEvaluation)}; coldCoupled={cold.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation)}; sharedPreparationPhysics={preparation.GetValueOrDefault(ProjectionMaterialization.PhysicalEvaluation)}; sharedPreparationCoupled={preparation.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation)}; setupNarrow={setupGeometry.Work.NarrowPhaseEvaluations}; acceptedPrefixReplaysAcrossComparison=0");
    }

    private sealed record Attempt(TwoRiderContactResult? Result, PairContactCertificationRefusal? Failure,
        RiderIntegrationCursor R1, RiderIntegrationCursor R2, Dictionary<ProjectionMaterialization, int> Counts,
        List<SpaceSearchDiagnostic> Searches, List<(bool Continuation, ContestedSpaceReport Report)> Others);

    private sealed class Clip(PhysicalPoseInterval source, double start, double end, bool entryJump)
        : PhysicalPoseInterval(source.RiderId, source.FrameId, start, end, source.Dimensions,
            (source.StartsAtDiscontinuity || entryJump) && start == source.StartTimeSeconds, source.Source, source.SupportsLapWrap)
    {
        internal override bool DeterministicArithmetic => source.DeterministicArithmetic;
        public override PhysicalBikePose Sample(double time) { Fraction(time); return source.Sample(time); }
        internal override BikePoseValue SampleValue(double time) { Fraction(time); return source.SampleValue(time); }
        public override PoseRateBounds RateBounds(double from, double to) { Fraction(from); Fraction(to); return source.RateBounds(from, to); }
    }
}
