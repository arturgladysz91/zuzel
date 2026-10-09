using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using RaceReadiness;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "trajectory")]
public sealed class CanonicalSegmentHandoffTests
{
    private static readonly float[] Fractions = [0f, .1f, .2f, .4f, .5f, .9f, .125f, .375f,
        MathF.BitDecrement(1f), 1e-8f];
    private sealed class Hold : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }

    public static IEnumerable<object[]> CompletionMatrix()
    {
        foreach (var progress in Fractions)
        foreach (var index in new[] { 0, 1, 2, 3, 7, 8 })
        foreach (var lap in new[] { 1, 3 })
        foreach (var moving in new[] { false, true })
            yield return [progress, index, lap, moving];
    }

    [Theory]
    [MemberData(nameof(CompletionMatrix))]
    public void ResolveAlreadyOwnsExactBoundaryAndCommitAddsOnlyExecutedDistance(float progress, int index, int lap, bool moving)
    {
        var track = Track.CreateStandingStartExample();
        var rider = TrajectoryEvaluatorTests.At(track, index, 2, 18f, progress, lap);
        if (moving) rider.LateralPosition = 2.21f;
        var state = TrackState.CreateDefault(track);
        var engine = new SimulationEngine(new Hold());
        var input = engine.CaptureSnapshot(track, state, new[] { rider }, new(91, 0, lap, index, 19, 4));
        var before = Exact.Hash(new { Rider = rider, Surface = state.Snapshot(), input });
        var options = new HeatSimulationOptions { IncidentFrequency = 0f, Laps = 4 };
        var resolved = engine.Resolve(input, engine.Decide(input), options);
        var lean = SimulationEngine.ResolveSoloProjection(input, input.Riders[0], new(2), options);
        var change = resolved.Changes.Single(); var motion = resolved.Motions.Single();
        var boundary = (double)lap * track.Segments.Count + index + 1;
        Assert.Equal(before, Exact.Hash(new { Rider = rider, Surface = state.Snapshot(), input }));
        Assert.Equal(change, lean.Change);
        Assert.Equal(boundary, change.Position.TotalSegmentProgress);
        Assert.Equal(0f, change.Position.SegmentProgress);
        Assert.Equal((index + 1) % track.Segments.Count, change.Position.SegmentIndex);
        Assert.Equal(lap + (index == track.Segments.Count - 1 ? 1 : 0), change.Position.LapsCompleted);
        Assert.Equal(boundary == 4 * track.Segments.Count ? RiderRaceStatus.Finished : RiderRaceStatus.Racing, change.Status);
        Assert.Equal(input.Riders[0].CanonicalProgress, motion.Initial.CanonicalProgress);
        Assert.Equal(boundary, motion.Final.CanonicalProgress);
        Assert.Equal(1f, motion.Final.SegmentProgress);
        Assert.Equal(rider.DistanceMeters + resolved.Diagnostics[0].TravelledMeters, change.Position.DistanceMeters);
        Assert.Equal(change.ElapsedTimeSeconds - rider.ElapsedTimeSeconds, motion.TotalTimeSeconds);
        Assert.Equal(resolved.Diagnostics[0].TravelledMeters, motion.TotalDistanceMeters);
        Assert.Equal(motion.TotalDistanceMeters, lean.DistanceMeters);
        Assert.Equal(motion.TotalTimeSeconds, lean.TravelTimeSeconds);
        Assert.True(change.ApplySurfaceWear);
        for (var i = 1; i < motion.Nodes.Count; i++)
        {
            Assert.True(motion.Nodes[i].LocalTimeSeconds >= motion.Nodes[i - 1].LocalTimeSeconds);
            Assert.True(motion.Nodes[i].CanonicalProgress >= motion.Nodes[i - 1].CanonicalProgress);
            Assert.True(motion.Nodes[i].TravelledMeters >= motion.Nodes[i - 1].TravelledMeters);
        }
        if (resolved.Diagnostics[0].ExecutedPath is { } path)
        {
            Assert.Equal(path.DistanceMeters, motion.TotalDistanceMeters);
            Assert.Equal(path.Nodes.Count, path.Steps.Count + 1);
            Assert.Equal(path.Nodes[^1].LateralPosition, motion.Final.LateralPosition);
            // The adapter already reconciles rounded path progress at identical distance/time.
            Assert.Equal(1f, path.Nodes[^1].SegmentProgress);
        }
        var leanState = state.Clone(); var leanRider = input.Riders[0].ToMutableCopy();
        SimulationEngine.CommitSoloProjection(lean, leanRider, leanState);
        engine.Commit(resolved, new[] { rider }, state, new SimLog(false));
        Assert.Equal(change.Position, rider.Position);
        Assert.Equal(Exact.Hash(new { Rider = rider, Surface = state.Snapshot() }),
            Exact.Hash(new { Rider = leanRider, Surface = leanState.Snapshot() }));
        if (change.Status == RiderRaceStatus.Finished) return;
        var nextInput = engine.CaptureSnapshot(track, state, new[] { rider }, input.Step with
        { StepNumber = 1, SegmentIndex = rider.SegmentIndex, LapIndex = rider.LapsCompleted });
        var next = engine.Resolve(nextInput, engine.Decide(nextInput), options).Motions.Single();
        Assert.Equal(boundary, next.Initial.CanonicalProgress);
        Assert.Equal(change.ElapsedTimeSeconds, next.StartElapsedTimeSeconds);
        Assert.Equal(0f, next.Initial.LocalTimeSeconds);
        Assert.Equal(0f, next.Initial.TravelledMeters);
        Assert.Equal(motion.ExitBoundary, next.EntryBoundary);
    }

    [Theory]
    [InlineData(.1f, false)] [InlineData(.4f, false)]
    [InlineData(.1f, true)] [InlineData(.4f, true)]
    public void DecimalFractionCanContinueAcrossProductionTrajectoryHorizon(float progress, bool moving)
    {
        var track = Track.CreateStandingStartExample();
        var rider = TrajectoryEvaluatorTests.At(track, 0, 2, 20f, progress);
        if (moving) rider.LateralPosition = 2.21f;
        var context = TrajectoryEvaluatorTests.Context(track, rider);
        LeanProjectionTests.Verify(context, new(2, 2, 2));
        var traversal = new TrajectoryEvaluator(context).Evaluate(new(2, 2, 2), true);
        Assert.True(traversal.ResolvedMotions.Count > 1);
        Assert.Equal(1d, traversal.ResolvedMotions[0].Final.CanonicalProgress);
        Assert.Equal(1d, traversal.ResolvedMotions[1].Initial.CanonicalProgress);
    }

    [Theory]
    [InlineData(.1f)] [InlineData(.4f)]
    public void CachedRichLeanAndColdBranchesAgreeAcrossCornersLapAndFinish(float progress)
    {
        var track = Track.CreateStandingStartExample();
        foreach (var index in new[] { 0, 1, 2, 3, 7, 8 })
        foreach (var lap in new[] { 0, 3 })
        foreach (var moving in new[] { false, true })
        {
            var rider = TrajectoryEvaluatorTests.At(track, index, 2, 18f, progress, lap);
            if (moving) rider.LateralPosition = 2.21f;
            var context = TrajectoryEvaluatorTests.Context(track, rider);
            var before = Exact.Hash(context.Snapshot);
            var evaluator = new TrajectoryEvaluator(context);
            foreach (var intent in new[] { new TrajectoryIntent(2, 2, 2), new(2, 1, 2), new(3, 2, 3) })
            {
                LeanProjectionTests.Verify(context, intent);
                var lean = evaluator.Evaluate(intent);
                Assert.Equal(Exact.Hash(lean), Exact.Hash(new TrajectoryEvaluator(context, reuseProductionPrefixes: false).Evaluate(intent)));
                var count = evaluator.ProductionResolutionCount;
                Assert.Equal(Exact.Hash(lean), Exact.Hash(evaluator.Evaluate(intent)));
                Assert.Equal(count, evaluator.ProductionResolutionCount);
                var rich = evaluator.Evaluate(intent, true);
                Assert.Equal(Exact.Hash(lean), Exact.Hash(rich with { ResolvedMotions = Array.Empty<ResolvedRiderMotion>() }));
                Assert.All(rich.ResolvedMotions, m => Assert.Equal(Math.Floor(m.Initial.CanonicalProgress) + 1d, m.Final.CanonicalProgress));
            }
            Assert.Equal(before, Exact.Hash(context.Snapshot));
        }
    }

    [Theory]
    [InlineData(.1f, false)] [InlineData(.4f, false)] [InlineData(.9f, false)]
    [InlineData(.1f, true)] [InlineData(.4f, true)] [InlineData(.9f, true)]
    public void CrashKeepsActualHalfRemainderWithoutLapOrWear(float progress, bool moving)
    {
        var track = Track.CreateStandingStartExample();
        var rider = TrajectoryEvaluatorTests.At(track, 7, 4, 80f, progress, 3);
        if (moving) rider.LateralPosition = 3.99f;
        var state = TrackState.CreateDefault(track); var beforeSurface = Exact.Hash(state.Snapshot());
        var context = TrajectoryEvaluatorTests.Context(track, rider, state);
        var engine = new SimulationEngine(new Hold());
        var resolved = engine.Resolve(context.Snapshot, [new(rider.RiderId, new(4))], new() { IncidentFrequency = 0f });
        var change = resolved.Changes.Single();
        var expected = rider.Position.Advance((1f - context.Rider.SegmentProgress) * .5f, resolved.Diagnostics[0].TravelledMeters);
        Assert.Equal(SegmentOutcome.Crash, change.Outcome); Assert.Equal(RiderRaceStatus.Crashed, change.Status);
        Assert.Equal(expected, change.Position); Assert.Equal(7, change.Position.SegmentIndex);
        Assert.Equal(3, change.Position.LapsCompleted); Assert.False(change.ApplySurfaceWear);
        Assert.Equal(change.Position.TotalSegmentProgress, resolved.Motions.Single().Final.CanonicalProgress);
        engine.Commit(resolved, [rider], state, new SimLog(false));
        Assert.Equal(beforeSurface, Exact.Hash(state.Snapshot()));
    }

    [Fact]
    public void CompletionDoesNotChangeGeneralAdvanceOrConstructorValidation()
    {
        var start = RiderPosition.Create(1, 0, .1f, 8, 10f);
        var partial = start.Advance(1f - start.SegmentProgress, 12f);
        Assert.Equal(.9999999776482582d, partial.TotalSegmentProgress);
        Assert.Equal(0, partial.SegmentIndex);
        var completed = start.AdvanceToSegmentEnd(12f);
        Assert.Equal(1d, completed.TotalSegmentProgress); Assert.Equal(22f, completed.DistanceMeters);
        Assert.Equal(.10000000149011612d, start.TotalSegmentProgress);
        Assert.Throws<InvalidOperationException>(() => default(RiderPosition).AdvanceToSegmentEnd(1f));
        foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => start.AdvanceToSegmentEnd(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => RiderPosition.Start(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RiderPosition.Create(1, 0, 1f, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => RiderPosition.Create(1, 0, -.1f, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => RiderPosition.Create(1, 0, .1f, 8, -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => RiderPosition.Create(1, 0, .1f, 8, float.MaxValue).AdvanceToSegmentEnd(float.MaxValue));
        Assert.Throws<InvalidOperationException>(() => RiderPosition.Create(1, 0, float.NaN, 8).AdvanceToSegmentEnd(1f));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LegacyAndControlledProductionAlsoCompleteBeforeCommit(bool legacy)
    {
        var track = Track.CreateStandingStartExample();
        var rider = TrajectoryEvaluatorTests.At(track, 0, 2, 20f, .1f);
        var context = TrajectoryEvaluatorTests.Context(track, rider);
        var input = new SimulationSnapshot(context.Snapshot.Step with { UseLegacyPhysics = legacy }, track, context.TrackState, context.Snapshot.Riders);
        var engine = new SimulationEngine(new Hold());
        var resolved = engine.ResolveProduction(input, [new(rider.RiderId, new(2)
        { DriveControl = new(.5f), HoldLateralPosition = true })],
            new() { IncidentFrequency = 0f, EnableContestedSpaceResponses = !legacy }, legacyContacts: false);
        Assert.Equal(1d, resolved.Changes.Single().Position.TotalSegmentProgress);
        if (!legacy)
        {
            var projected = new TrajectoryEvaluator(context, driveControl: new(.5f), holdLateralPosition: true).Evaluate(new(2, 2, 2), true);
            Assert.Equal(1d, projected.ResolvedMotions[0].Final.CanonicalProgress);
        }
    }

    [Theory]
    [InlineData("three-squeeze", "A")] [InlineData("three-squeeze", "B")] [InlineData("three-squeeze", "C")]
    [InlineData("four-close-regain", "A")] [InlineData("four-close-regain", "B")] [InlineData("four-close-regain", "C")]
    public void BoundedOriginalProductionHeatsCompleteWithReversalAndObserverParity(string scenario, string configuration)
    {
        var first = Run(false, true);
        Assert.Equal(first, Run(true, true)); Assert.Equal(first, Run(false, false)); Assert.Equal(first, Run(true, false));
        string Run(bool reverse, bool observed)
        {
            var (track, surface, riders) = Catalog.All().Single(s => s.Id == scenario).Create();
            Assert.Contains(riders, r => r.Position.SegmentProgress == .1f);
            if (reverse) riders.Reverse();
            var observer = new Observer();
            var result = new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(track, surface, riders,
                new() { Seed = 19, EnableContestedSpaceResponses = configuration != "A", EnablePhysicalContactConsequences = configuration == "C" },
                91, observed ? observer : null);
            Assert.Equal(4, result.Classification.Count);
            Assert.All(result.Classification, r => Assert.True(r.Finished || r.Dnf));
            if (observed)
            {
                Assert.NotEmpty(observer.Steps);
                Assert.All(observer.Steps.SelectMany(s => s.Changes).Where(c => c.Outcome != SegmentOutcome.Crash),
                    c => Assert.Equal(Math.Floor(c.Position.TotalSegmentProgress), c.Position.TotalSegmentProgress));
            }
            return Exact.Hash(new { result.Classification, result.Log, Riders = riders.OrderBy(r => r.Profile.Id).ToArray(), Surface = surface.Snapshot() });
        }
    }
    private sealed class Observer : ISimulationStepObserver
    {
        internal List<ResolvedSimulationStep> Steps { get; } = new();
        public void OnStepResolved(ResolvedSimulationStep step) => Steps.Add(step);
    }
}
