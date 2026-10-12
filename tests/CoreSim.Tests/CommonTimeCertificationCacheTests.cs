using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using RaceReadiness;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class CommonTimeCertificationCacheTests
{
    [Fact]
    public void CanonicalSamplerInterningPreservesAllPosesBoundsAndSeparatesGeometryAttitudeAndDimensions()
    {
        var scenario = ContestedSpaceResponseEvidence.Scenarios().Single(s => s.Name == "D-inside-overlap");
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(scenario);
        var intents = scenario.Riders.Select(r => new RiderIntent(r.Id, new RiderDecision(r.Intent.TargetFor(snapshot.Segment.Type))
            { Trajectory = r.Intent })).ToArray();
        var step = new SimulationEngine(new Hold()).ResolveProduction(snapshot, intents, new() { IncidentFrequency = 0 }, legacyContacts: false);
        var motion = step.Motions[0]; var embedding = new TrackMetricEmbedding(snapshot.Track);
        var first = ResolvedBikePoses.FromMotion(motion, snapshot.Track, embedding: embedding);
        var copy = ResolvedBikePoses.FromMotion(motion, snapshot.Track, embedding: embedding);
        var pool = new ResolvedBikePoses.IntervalPool();
        for (var index = 0; index < first.Count; index++)
        {
            Assert.Same(first[index], pool.Get(first[index]));
            Assert.Same(first[index], pool.Get(copy[index]));
            var time = (first[index].StartTimeSeconds + first[index].EndTimeSeconds) / 2;
            Assert.Equal(Exact.Hash(new { Pose = copy[index].Sample(time), Bounds = copy[index].RateBounds(time, time) }),
                Exact.Hash(new { Pose = pool.Get(copy[index]).Sample(time), Bounds = pool.Get(copy[index]).RateBounds(time, time) }));
        }
        foreach (var distinct in new[] {
            ResolvedBikePoses.FromMotion(motion, snapshot.Track, embedding: new TrackMetricEmbedding(snapshot.Track)),
            ResolvedBikePoses.FromMotion(motion, snapshot.Track, embedding: new TrackMetricEmbedding(snapshot.Track, true)),
            ResolvedBikePoses.FromMotion(motion, snapshot.Track, new SpeedwayBikeDimensions(2.2, .3, .8, .65, .04), embedding: embedding),
            ResolvedBikePoses.FromMotion(motion, snapshot.Track, attitude: new ReferenceBikeAttitude(.05, .5, .35, .55, .95), embedding: embedding) })
            Assert.NotSame(first[0], pool.Get(distinct[0]));
        Assert.NotSame(first[0], new ResolvedBikePoses.IntervalPool().Get(copy[0]));
    }

    [Theory]
    [InlineData("A")] [InlineData("C")] [InlineData("D")] [InlineData("H")] [InlineData("I")] [InlineData("J")]
    public void ExactCertificatesRetainRootsMinimaAttributionAndLogicalWork(string scenario)
    {
        var poses = PhysicalSpaceEvidence.Controlled(scenario);
        var context = new CommonTimePoseHistory.CertificationContext();
        var expected = CommonTimePoseHistory.Observe(poses);
        AssertReport(expected, CommonTimePoseHistory.Observe(poses, context));
        var repeated = CommonTimePoseHistory.Observe(poses.Reverse(), context);
        AssertReport(expected, repeated);
        Assert.True(repeated.Work.NarrowPhaseEvaluations <= expected.Work.NarrowPhaseEvaluations);
        if (scenario is "D" or "H" or "I")
            Assert.True(repeated.Work.NarrowPhaseEvaluations < expected.Work.NarrowPhaseEvaluations);
        // Summary filtering follows certification; attribution has a separate identity.
        foreach (var ready in new[] { 0d, .5d, 1d })
        {
            var plain = CommonTimePoseHistory.Compatibility(poses, ready);
            var cached = CommonTimePoseHistory.Compatibility(poses, ready, context);
            Assert.Equal(Exact.Hash(new { plain.MinimumSeparation, plain.HasBoundaryAmbiguity, plain.HasCoverageGap,
                plain.IneligibleIntervals, plain.EligibleIntervals, plain.Contacts }),
                Exact.Hash(new { cached.MinimumSeparation, cached.HasBoundaryAmbiguity, cached.HasCoverageGap,
                    cached.IneligibleIntervals, cached.EligibleIntervals, cached.Contacts }));
        }
    }

    [Fact]
    public void EarlierMinimumAndDifferentTimeCutsCannotReuseAnotherSearch()
    {
        var suffix = new[] { Linear(1, 1, 2, 0, 0), Linear(2, 1, 2, 2, 0) };
        var context = new CommonTimePoseHistory.CertificationContext();
        var near = new[] { Linear(1, 0, 1, 0, 0), Linear(2, 0, 1, .2, .2) };
        var far = new[] { Linear(1, 0, 1, 0, 0), Linear(2, 0, 1, 4, 4) };
        foreach (var prefix in new[] { near, far, near })
            AssertReport(CommonTimePoseHistory.Observe(prefix.Concat(suffix)),
                CommonTimePoseHistory.Observe(prefix.Concat(suffix), context));
        var a = suffix[0]; var b = suffix[1];
        var key = new ContestedSpaceResolver.IntervalKey(a, b, Bits(1), Bits(2), Bits(double.PositiveInfinity), false);
        Assert.NotEqual(key, key with { PairMinimum = Bits(-.3) });
        Assert.NotEqual(key, key with { Start = Bits(Math.BitIncrement(1d)) });
        Assert.NotEqual(key, key with { End = Bits(Math.BitDecrement(2d)) });
        Assert.NotEqual(key, key with { Attribution = true });
        Assert.NotEqual(key, key with { A = Linear(1, 1, 2, 0, 0) });
        Assert.NotEqual(key with { Start = Bits(0d) }, key with { Start = Bits(-0d) });
        AssertReport(CommonTimePoseHistory.Observe(suffix), CommonTimePoseHistory.Observe(suffix,
            new CommonTimePoseHistory.CertificationContext()));
    }

    [Fact]
    public void TrimmingAndJumpQuarantineRemainExactIncludingEscapeAndRecontact()
    {
        var context = new CommonTimePoseHistory.CertificationContext();
        var poses = new[] { Linear(1, 0, 1 + 1e-8, 0, 0), Linear(2, 0, 1 + 1e-8, 3, 3),
            Linear(1, 1, 2, 0, 0), Linear(2, 1, 2, .2, 3, true),
            Linear(1, 2, 3, 0, 0), Linear(2, 2, 3, 3, .2) };
        var stitched = CommonTimePoseHistory.Stitch(poses, context);
        Assert.Same(stitched[0], CommonTimePoseHistory.Stitch(poses.Reverse(), context)[0]);
        var expected = CommonTimePoseHistory.Observe(poses);
        Assert.Contains(expected.Intervals, r => r.Kind == SpaceConflictKind.BoundaryAmbiguous);
        Assert.Contains(expected.Intervals, r => r.EligibleForFutureInteraction);
        AssertReport(expected, CommonTimePoseHistory.Observe(poses, context));
        AssertReport(expected, CommonTimePoseHistory.Observe(poses.Reverse(), context));
        Assert.InRange(context.Cache.Intervals.Count, 0, ContestedSpaceResolver.CertificationCache.MaximumCachedIntervals);
        Assert.InRange(context.Cache.Clear.Count, 0, ContestedSpaceResolver.CertificationCache.MaximumCachedClearSearches);
        var substantive = new[] { Linear(1, 0, 1.1, 0, 0), Linear(1, 1, 2, 0, 0) };
        Assert.Throws<ArgumentException>(() => CommonTimePoseHistory.Observe(substantive, context));
    }

    [Theory]
    [InlineData("D-inside-overlap", false)] [InlineData("H-three-squeeze", false)]
    [InlineData("G-four-first-bend", false)] [InlineData("edge-trapped", false)]
    [InlineData("B-close-too-late", false)] [InlineData("imminent-overlap", false)]
    [InlineData("imminent-overlap", true)] [InlineData("G-four-first-bend", true)]
    public void CoordinatorReuseKeepsEveryDecisionPathContactAndConsequenceBitExact(string name, bool physical)
    {
        var scenario = ContestedSpaceResponseEvidence.Scenarios().Concat(ContestedSpaceResponseEvidence.OwnershipScenarios())
            .Single(s => s.Name == name);
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(scenario);
        var intents = scenario.Riders.Select(r => new RiderIntent(r.Id, new RiderDecision(r.Intent.TargetFor(snapshot.Segment.Type))
            { Trajectory = r.Intent })).ToArray();
        var options = new HeatSimulationOptions { EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = physical,
            IncidentFrequency = scenario.IncidentFrequency, InteractionDiagnostics = InteractionDiagnosticsLevel.FullAudit };
        var engine = new SimulationEngine(new Hold());
        var warm = new ContestedSpaceInteractionCoordinator(new()).Resolve(engine, snapshot, intents, options);
        var cold = new ContestedSpaceInteractionCoordinator(new(), reuseCertificationResults: false)
            .Resolve(engine, snapshot, intents.Reverse().ToArray(), options);
        object Behavior(ResolvedSimulationStep step) => new { step.Changes, step.Diagnostics, step.Motions, step.Events,
            step.Interaction!.Episodes, step.Interaction.PhysicalContactAnalysis, step.Interaction.PhysicalContactConsequences };
        Assert.Equal(Exact.Hash(Behavior(cold)), Exact.Hash(Behavior(warm)));
        Assert.Equal(cold.Interaction!.Work with { NarrowPhaseEvaluations = 0 }, warm.Interaction!.Work with { NarrowPhaseEvaluations = 0 });
        Assert.True(warm.Interaction.Work.NarrowPhaseEvaluations <= cold.Interaction.Work.NarrowPhaseEvaluations);
    }

    private static void AssertReport(ContestedSpaceReport expected, ContestedSpaceReport actual)
    {
        Assert.Equal(Exact.Hash(new { expected.Intervals, expected.FrameCoverageGaps }),
            Exact.Hash(new { actual.Intervals, actual.FrameCoverageGaps }));
        Assert.Equal(expected.Work with { NarrowPhaseEvaluations = 0, AdaptiveSubdivisions = 0, RootIterations = 0 },
            actual.Work with { NarrowPhaseEvaluations = 0, AdaptiveSubdivisions = 0, RootIterations = 0 });
    }
    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);
    private static PhysicalPoseInterval Linear(int id, double start, double end, double y0, double y1, bool jump = false)
        => PhysicalSpaceEvidence.Linear(id, new(start * 20, y0), new(end * 20, y1), startTime: start, endTime: end, discontinuity: jump);
    private sealed class Hold : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane); }
}
