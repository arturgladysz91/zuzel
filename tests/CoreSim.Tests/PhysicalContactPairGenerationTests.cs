using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using System.Text.Json;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class PhysicalContactPairGenerationTests
{
    private static PhysicalContactPairAnalysis Pair(long episode, double time = 0, int a = 1, int b = 2)
        => PhysicalContactAnalyzer.AnalyzePair(PhysicalContactEvidence.TimedContact(a, b, time)) with { EpisodeId = episode };

    private static void Consume(InteractionEpisodeTracker tracker, PhysicalContactPairAnalysis pair)
        => tracker.ConsumePhysical(new(Array.Empty<RiderContactConsequence>(), new[] { pair }, 0));

    private static ContestedSpaceReport Separation(double start, double end)
    {
        PhysicalPoseInterval Pose(int id, double y) => new LinearBikePoseInterval(
            new(id, "pair-release", new(0, y), new(start, 0, 0), SpeedwayBikeDimensions.Reference),
            new(id, "pair-release", new(20 * (end - start), y), new(end, 0, 0), SpeedwayBikeDimensions.Reference));
        // A is separated from B; B-C keep the parent component physically engaged.
        return CommonTimePoseHistory.Observe(new[] { Pose(1, 0), Pose(2, 2), Pose(3, 2.1) });
    }

    [Fact]
    public void CertifiedPairReleaseAllowsSecondImpactWhileParentEpisodeRemainsActive()
    {
        var tracker = new InteractionEpisodeTracker();
        var episode = tracker.Engage(new[] { 1, 2, 3 }, 0, InteractionContext.MechanicalConflict);
        Consume(tracker, Pair(episode.Id));
        Assert.False(tracker.CanApplyPhysical(Pair(episode.Id, .5)));
        var clear = Separation(1, 1.5);
        Assert.All(clear.Intervals.Where(r => r.RiderA == 1 && r.RiderB == 2), r =>
        {
            Assert.True(r.NumericallyResolved);
            Assert.True(r.MinimumSeparationLowerBoundMeters > .65);
        });
        tracker.ObserveClearance(1.5, clear.Intervals, new());
        Assert.Equal(episode.Id, Assert.Single(tracker.Active).Id);
        var released = tracker.PhysicalPairs[(1, 2)];
        Assert.Equal(1, released.Generation); Assert.False(released.Consumed);
        Assert.Equal(1, released.ClearSinceSeconds); Assert.Equal(1.45, released.ArmedAtSeconds);
        Assert.True(tracker.CanApplyPhysical(Pair(episode.Id, 2)));
        Consume(tracker, Pair(episode.Id, 2));
        Assert.Equal(1, tracker.PhysicalPairs[(1, 2)].Generation);
        Assert.True(tracker.PhysicalPairs[(1, 2)].Consumed);
        Assert.False(tracker.CanApplyPhysical(Pair(episode.Id, 2.1)));
    }

    [Fact]
    public void ShortAndRepeatedHistoricalSeparationDoNotReleasePairButContinuousExtensionDoes()
    {
        var tracker = new InteractionEpisodeTracker();
        var episode = tracker.Engage(new[] { 1, 2, 3 }, 0, InteractionContext.MechanicalConflict);
        Consume(tracker, Pair(episode.Id));
        var shortClear = Separation(1, 1.3);
        for (var repeat = 0; repeat < 8; repeat++) tracker.ObserveClearance(1.3, shortClear.Intervals, new());
        tracker.Engage(new[] { 1, 2, 3 }, 100, InteractionContext.MechanicalConflict); // a forecast is not clearance
        Assert.Equal(0, tracker.PhysicalPairs[(1, 2)].Generation);
        Assert.Equal(1, tracker.PhysicalPairs[(1, 2)].ClearSinceSeconds);
        Assert.Equal(1.3, tracker.PhysicalPairs[(1, 2)].ObservedUntilSeconds);
        Assert.False(tracker.CanApplyPhysical(Pair(episode.Id, 1.4)));
        tracker.ObserveClearance(1.6, Separation(1.3, 1.6).Intervals, new());
        Assert.Equal(episode.Id, Assert.Single(tracker.Active).Id);
        Assert.True(tracker.CanApplyPhysical(Pair(episode.Id, 2)));
    }

    [Theory]
    [InlineData("overlap")]
    [InlineData("lower-bound")]
    [InlineData("threshold")]
    [InlineData("unresolved")]
    [InlineData("ambiguous")]
    [InlineData("missing")]
    [InlineData("gap")]
    public void BrokenCertificatesResetProgressAndCannotRearm(string broken)
    {
        var tracker = new InteractionEpisodeTracker();
        var e = tracker.Engage(new[] { 1, 2, 3 }, 0, InteractionContext.MechanicalConflict);
        Consume(tracker, Pair(e.Id));
        tracker.ObservePhysicalClearance(Separation(1, 1.3), new());
        var report = Separation(1.3, 1.4);
        var rows = report.Intervals.Select(r => r.RiderA != 1 || r.RiderB != 2 ? r : broken switch
        {
            "overlap" => r with { Kind = SpaceConflictKind.ParallelOverlap, MinimumSeparationLowerBoundMeters = -.1 },
            "lower-bound" => r with { MinimumSeparationLowerBoundMeters = .4 },
            "threshold" => r with { MinimumSeparationLowerBoundMeters = .65 },
            "unresolved" => r with { NumericallyResolved = false },
            "ambiguous" => r with { Kind = SpaceConflictKind.BoundaryAmbiguous },
            _ => r
        }).Where(r => broken != "missing" || r.RiderA != 1 || r.RiderB != 2).ToArray();
        var gaps = broken == "gap" ? new[] { new FrameCoverageGap(1, 2, 1.3, 1.4, "a", "b") } : report.FrameCoverageGaps;
        tracker.ObservePhysicalClearance(report with { Intervals = rows, FrameCoverageGaps = gaps }, new());
        tracker.ObservePhysicalClearance(Separation(1.4, 1.7), new());
        var state = tracker.PhysicalPairs[(1, 2)];
        Assert.True(state.Consumed); Assert.Equal(0, state.Generation);
        Assert.Equal(1.4, state.ClearSinceSeconds);
        Assert.False(tracker.CanApplyPhysical(Pair(e.Id, 2)));
    }

    [Fact]
    public void DuplicateOverlappingRowsAndObservationOrderCannotCountPreImpactTime()
    {
        var tracker = new InteractionEpisodeTracker();
        var e = tracker.Engage(new[] { 1, 2, 3 }, 0, InteractionContext.MechanicalConflict);
        var impact = Pair(e.Id, 1);
        Consume(tracker, impact);
        var clear = Separation(0, impact.FirstTouchCommonTimeSeconds + .3);
        tracker.ObservePhysicalClearance(clear with { Intervals = clear.Intervals.Concat(clear.Intervals).Reverse().ToArray() }, new());
        Assert.True(tracker.PhysicalPairs[(1, 2)].Consumed);
        Assert.Equal(impact.FirstTouchCommonTimeSeconds, tracker.PhysicalPairs[(1, 2)].ClearSinceSeconds);
        tracker.ObservePhysicalClearance(Separation(0, .5), new()); // old observation cannot rewind the cursor
        Assert.True(tracker.PhysicalPairs[(1, 2)].Consumed);
        Assert.Equal(impact.FirstTouchCommonTimeSeconds + .3, tracker.PhysicalPairs[(1, 2)].ObservedUntilSeconds);
    }

    [Fact]
    public void HistoricalGapEndsAtNewClearanceAndPointAmbiguityCannotCompleteRelease()
    {
        var tracker = new InteractionEpisodeTracker();
        var e = tracker.Engage(new[] { 1, 2, 3 }, 0, InteractionContext.MechanicalConflict);
        Consume(tracker, Pair(e.Id));
        tracker.ObservePhysicalClearance(Separation(1, 1.3), new());
        var clear = Separation(1.4, 1.9);
        tracker.ObservePhysicalClearance(clear with { FrameCoverageGaps = new[] { new FrameCoverageGap(1, 2, 1.3, 1.4, "a", "b") } }, new());
        Assert.False(tracker.PhysicalPairs[(1, 2)].Consumed);
        Assert.Equal(1.4, tracker.PhysicalPairs[(1, 2)].ClearSinceSeconds);
        Consume(tracker, Pair(e.Id, 2));
        var exact = Separation(3, 3 + new ContestedSpaceParameters().ReleaseDelaySeconds);
        tracker.ObservePhysicalClearance(exact with { FrameCoverageGaps = new[]
            { new FrameCoverageGap(1, 2, exact.Intervals[0].IntervalEndSeconds, exact.Intervals[0].IntervalEndSeconds, "a", "b") } }, new());
        Assert.True(tracker.PhysicalPairs[(1, 2)].Consumed);
        tracker.ObservePhysicalClearance(Separation(3.45, 3.75), new());
        Assert.True(tracker.PhysicalPairs[(1, 2)].Consumed);
    }

    [Fact]
    public void VerificationBeforeFrontierCanReleaseButLaterOrSameFrontierClearanceCannot()
    {
        var tracker = new InteractionEpisodeTracker();
        var e = tracker.Engage(new[] { 1, 2, 3 }, 0, InteractionContext.MechanicalConflict);
        Consume(tracker, Pair(e.Id));
        var observation = new PhysicalPairObservation(Separation(1, 1.6), new());
        Assert.False(tracker.CanApplyPhysical(Pair(e.Id, 1.2), observation));
        Assert.False(tracker.CanApplyPhysical(Pair(e.Id, 2) with { FrontierStartTimeSeconds = 1.2 }, observation));
        var next = Pair(e.Id, 2);
        Assert.True(tracker.CanApplyPhysical(next, observation));
        Assert.Equal(0, tracker.PhysicalPairs[(1, 2)].Generation); // eligibility is a pure query
        tracker.ConsumePhysical(new(Array.Empty<RiderContactConsequence>(), new[] { next }, 0), observation);
        Assert.Equal(1, tracker.PhysicalPairs[(1, 2)].Generation);
        Assert.False(tracker.CanApplyPhysical(next, observation));
        Assert.Throws<InvalidOperationException>(() => Consume(tracker, next));
    }

    [Fact]
    public void IndependentGenerationsSurviveMergeCloneAndCommitWithoutChangingFallbackOwnership()
    {
        var tracker = new InteractionEpisodeTracker();
        var ab = tracker.Engage(new[] { 1, 2 }, 0, InteractionContext.MechanicalConflict);
        var cd = tracker.Engage(new[] { 3, 4 }, 0, InteractionContext.MechanicalConflict);
        ab.FallbackAttempted = true;
        Consume(tracker, Pair(ab.Id)); Consume(tracker, Pair(cd.Id, a: 3, b: 4));
        tracker.ObservePhysicalClearance(Separation(1, 1.3), new());
        var clone = tracker.Clone();
        var merged = clone.Engage(new[] { 1, 2, 3, 4 }, 1.3, InteractionContext.MechanicalConflict);
        clone.ObservePhysicalClearance(Separation(1.3, 1.6), new());
        Assert.Equal(1, clone.PhysicalPairs[(1, 2)].Generation);
        Assert.Equal(0, clone.PhysicalPairs[(3, 4)].Generation);
        Assert.True(clone.PhysicalPairs[(3, 4)].Consumed);
        Assert.Equal(0, tracker.PhysicalPairs[(1, 2)].Generation);
        Assert.Equal(2, tracker.Active.Count());
        Assert.True(merged.FallbackOrigins[ab.Id].Attempted);
        Assert.False(merged.FallbackOrigins[cd.Id].Attempted);
        tracker.CommitFrom(clone);
        Assert.Equal(clone.PhysicalPairs, tracker.PhysicalPairs);
        Assert.Equal(merged.Id, Assert.Single(tracker.Active).Id);
        Assert.True(tracker.CanApplyPhysical(Pair(merged.Id, 2, 2, 1))); // canonical reversed pair
        Consume(tracker, Pair(merged.Id, 2));
        Assert.Equal(1, tracker.PhysicalPairs[(1, 2)].Generation);
        Assert.Equal(1, clone.PhysicalPairs[(1, 2)].Generation);
        Assert.False(clone.PhysicalPairs[(1, 2)].Consumed); // commit does not alias mutable state
    }

    [Theory]
    [InlineData(7, false, true)] [InlineData(19, false, true)]
    [InlineData(7, true, true)] [InlineData(19, true, true)]
    [InlineData(7, false, false)] [InlineData(19, false, false)]
    [InlineData(7, true, false)] [InlineData(19, true, false)]
    public void ProductionRecontactAppliesOneNewPairAndSpeculationCannotConsumeIt(int seed, bool rain, bool certified)
    {
        object Run(bool reverse)
        {
            var weather = rain ? WeatherState.LightRain : WeatherState.Dry;
            var scenario = ContestedSpaceResponseEvidence.OwnershipScenarios().Single(s => s.Name == "two-disjoint-unresolved")
                with { Seed = seed };
            scenario = scenario with { Riders = scenario.Riders.Select(r => r.Id == 1
                ? r with { Speed = 22 } : r.Id == 2 ? r with { Lateral = .9f } : r).ToArray() };
            var basis = ContestedSpaceResponseEvidence.Snapshot(scenario);
            var weatherSurface = new TrackState(basis.Track.Segments.Count, 5, basis.TrackState.GetSurface);
            TrackEvolution.ApplyWeather(basis.Track, weatherSurface, weather, basis.Step.HeatId, 0, new SimLog(false));
            var snapshot = new SimulationSnapshot(basis.Step, basis.Track, weatherSurface.Snapshot(),
                reverse ? basis.Riders.Reverse() : basis.Riders);
            var tracker = new InteractionEpisodeTracker(); var engine = new SimulationEngine(new FrozenTarget());
            var options = PhysicalContactConsequenceEvidence.Options with { Seed = seed, Weather = weather };
            var intents = scenario.Riders.Select(r => new RiderIntent(r.Id,
                new(r.Intent.TargetFor(snapshot.Segment.Type)) { Trajectory = r.Intent })).ToArray();
            if (reverse) Array.Reverse(intents);
            var first = engine.Resolve(snapshot, intents, options, tracker);
            Assert.Equal(2, first.Interaction!.PhysicalContactConsequences!.AppliedPairs.Count);
            Assert.True(first.Interaction.PhysicalContactConsequences.AppliedPairs.Single(p => p.RiderA == 1).Impulse!.ImpulseMagnitudeNewtonSeconds > 0);
            var live = snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray();
            var surface = new TrackState(snapshot.Track.Segments.Count, 5, snapshot.TrackState.GetSurface);
            engine.Commit(first, live, surface, new SimLog(false));
            var e = tracker.Engage(new[] { 1, 2, 3, 4 }, 100, InteractionContext.MechanicalConflict);
            var pending = tracker.Clone();
            pending.ObservePhysicalClearance(Separation(100, certified ? 100.5 : 100.3), new());
            Assert.Equal(certified ? 1 : 0, pending.PhysicalPairs[(1, 2)].Generation);
            Assert.True(pending.PhysicalPairs[(3, 4)].Consumed);
            Assert.Equal(0, tracker.PhysicalPairs[(1, 2)].Generation);
            tracker.CommitFrom(pending);
            var later = new SimulationSnapshot(snapshot.Step with { StepNumber = 10 }, snapshot.Track, snapshot.TrackState,
                snapshot.Riders.Select(r => r with { ElapsedTimeSeconds = 101 }));
            var next = engine.Resolve(later, intents, options, tracker);
            if (!certified)
            {
                Assert.Empty(next.Interaction!.PhysicalContactConsequences!.AppliedPairs);
                Assert.True(tracker.PhysicalPairs[(1, 2)].Consumed);
                Assert.Equal(0, tracker.PhysicalPairs[(1, 2)].Generation);
                return new { next.Changes, next.Interaction.PhysicalContactConsequences };
            }
            var applied = Assert.Single(next.Interaction!.PhysicalContactConsequences!.AppliedPairs);
            Assert.True(applied.Impulse!.ImpulseMagnitudeNewtonSeconds > 0);
            Assert.Equal((1, 2), (applied.RiderA, applied.RiderB));
            Assert.Equal(e.Id, applied.EpisodeId);
            Assert.False(tracker.PhysicalPairs[(1, 2)].Consumed);
            var abandoned = engine.Resolve(later, intents, options, tracker);
            Assert.Equal(JsonSerializer.Serialize(next.Changes), JsonSerializer.Serialize(abandoned.Changes));
            engine.Commit(next, live, surface, new SimLog(false));
            Assert.True(tracker.PhysicalPairs[(1, 2)].Consumed);
            Assert.Equal(1, tracker.PhysicalPairs[(1, 2)].Generation);
            var repeated = new SimulationSnapshot(later.Step with { StepNumber = 11 }, later.Track, later.TrackState,
                later.Riders.Select(r => r with { ElapsedTimeSeconds = 106 }));
            Assert.Empty(engine.Resolve(repeated, intents, options, tracker).Interaction!.PhysicalContactConsequences!.AppliedPairs);
            return new { first.Changes, Next = next.Changes, next.Interaction.PhysicalContactConsequences,
                States = tracker.PhysicalPairs.OrderBy(p => p.Key).Select(p => new { p.Key.A, p.Key.B, p.Value }) };
        }
        Assert.Equal(JsonSerializer.Serialize(Run(false)), JsonSerializer.Serialize(Run(true)));
    }

    [Fact]
    public void FourRiderBridgeReleasesPairsIndependentlyUnderReversedIdAssignment()
    {
        foreach (var ids in new[] { new[] { 1, 2, 3, 4 }, new[] { 4, 3, 2, 1 } })
        {
            var tracker = new InteractionEpisodeTracker();
            var ab = tracker.Engage(ids.Take(2).ToArray(), 0, InteractionContext.MechanicalConflict);
            var cd = tracker.Engage(ids.Skip(2).ToArray(), 0, InteractionContext.MechanicalConflict);
            Consume(tracker, Pair(ab.Id, a: ids[0], b: ids[1]));
            Consume(tracker, Pair(cd.Id, a: ids[2], b: ids[3]));
            ContestedSpaceReport ClearPair(double start, double end, int a, int b)
            {
                var basis = Separation(start, end);
                var row = basis.Intervals.Single(r => r.RiderA == 1 && r.RiderB == 2);
                return basis with { Intervals = new[] { row with { RiderA = a, RiderB = b } } };
            }
            tracker.ObservePhysicalClearance(ClearPair(1, 1.3, ids[0], ids[1]), new());
            var episode = tracker.Engage(ids, 1.3, InteractionContext.MechanicalConflict);
            tracker.ObservePhysicalClearance(ClearPair(1.3, 1.6, ids[1], ids[0]), new());
            var keyAB = InteractionEpisodeTracker.PhysicalPairKey(ids[0], ids[1]);
            var keyCD = InteractionEpisodeTracker.PhysicalPairKey(ids[2], ids[3]);
            Assert.Equal(1, tracker.PhysicalPairs[keyAB].Generation);
            Assert.Equal(0, tracker.PhysicalPairs[keyCD].Generation);
            Consume(tracker, Pair(episode.Id, 2, ids[0], ids[1]));
            tracker.ObservePhysicalClearance(ClearPair(2.5, 3.1, ids[2], ids[3]), new());
            Assert.False(tracker.CanApplyPhysical(Pair(episode.Id, 4, ids[0], ids[1])));
            Assert.True(tracker.CanApplyPhysical(Pair(episode.Id, 4, ids[2], ids[3])));
            Consume(tracker, Pair(episode.Id, 4, ids[2], ids[3]));
            Assert.All(tracker.PhysicalPairs.Values, s => { Assert.True(s.Consumed); Assert.Equal(1, s.Generation); });
        }
    }

    [Theory]
    [InlineData(7, false)] [InlineData(19, false)] [InlineData(7, true)] [InlineData(19, true)]
    public void ProductionCommitAloneCertifiesReleaseAcrossAnActualBridgeAndConsumesRecontact(int seed, bool rain)
    {
        object Run(bool reverse)
        {
            var basis = ContestedSpaceResponseEvidence.OwnershipScenarios().Single(s => s.Name == "two-disjoint-unresolved") with { Seed = seed };
            basis = basis with { Riders = basis.Riders.Select(r => r.Id == 1 ? r with { Speed = 22 }
                : r.Id == 2 ? r with { Lateral = .9f } : r).ToArray() };
            var tracker = new InteractionEpisodeTracker(); var engine = new SimulationEngine(new FrozenTarget());
            var options = PhysicalContactConsequenceEvidence.Options with { Seed = seed, Weather = rain ? WeatherState.LightRain : WeatherState.Dry };
            var phases = new List<object>(); long parent = 0, other = 0;
            for (var phase = 0; phase < 4; phase++)
            {
                // Frozen production phases contain explicit unobserved time gaps.
                // Those gaps cannot release ownership; only the executed clear step can.
                var scenario = phase == 1 ? basis with { Riders = basis.Riders.Select(r => r with
                {
                    Lateral = r.Id == 1 ? .7f : r.Id == 2 ? 3 : r.Id == 3 ? 3.125f : 3.25f,
                    Progress = 0, Intent = r.Id == 1 ? new(1, 1, 1) : new(3, 3, 3)
                }).ToArray() } : phase >= 2 ? basis with { Riders = basis.Riders.Select(r => r.Id >= 3
                    ? r with { Lateral = r.Id == 3 ? 1.1f : 1.225f, Intent = new(1, 1, 1) } : r).ToArray() } : basis;
                var raw = ContestedSpaceResponseEvidence.Snapshot(scenario, reverse);
                var surface = new TrackState(raw.Track.Segments.Count, 5, raw.TrackState.GetSurface);
                TrackEvolution.ApplyWeather(raw.Track, surface, options.Weather, raw.Step.HeatId, phase, new SimLog(false));
                var snapshot = new SimulationSnapshot(raw.Step with { StepNumber = phase }, raw.Track, surface.Snapshot(),
                    raw.Riders.Select(r => r with { ElapsedTimeSeconds = phase * 5 }));
                var intents = scenario.Riders.Select(r => new RiderIntent(r.Id,
                    new(r.Intent.TargetFor(snapshot.Segment.Type)) { Trajectory = r.Intent })).ToArray();
                if (reverse) Array.Reverse(intents);
                var prior = tracker.PhysicalPairs.GetValueOrDefault((1, 2));
                var resolved = engine.Resolve(snapshot, intents, options, tracker);
                Assert.Equal(prior, tracker.PhysicalPairs.GetValueOrDefault((1, 2)));
                var plan = resolved.Interaction!.PhysicalContactConsequences!;
                if (phase == 0) Assert.Equal(2, plan.AppliedPairs.Count);
                if (phase == 1)
                {
                    Assert.Contains(resolved.Interaction.Episodes, e => e.MergedEpisodeIds.Contains(other));
                    Assert.Contains(plan.AppliedPairs, p => p.RiderA == 2 && p.RiderB == 3);
                }
                if (phase == 2)
                {
                    var contact = Assert.Single(plan.AppliedPairs);
                    Assert.Equal((1, 2), (contact.RiderA, contact.RiderB));
                    Assert.Equal(parent, contact.EpisodeId);
                    Assert.True(contact.Impulse!.ImpulseMagnitudeNewtonSeconds > 0);
                }
                if (phase == 3) Assert.Empty(plan.AppliedPairs);
                engine.Commit(resolved, snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray(), surface, new SimLog(false));
                var state = tracker.PhysicalPairs[(1, 2)];
                if (phase == 0)
                {
                    Assert.Equal(0, state.Generation); Assert.True(state.Consumed);
                    parent = tracker.Active.Single(e => e.Riders.Contains(1)).Id;
                    other = tracker.Active.Single(e => e.Riders.Contains(3)).Id;
                }
                else
                {
                    Assert.Equal(parent, Assert.Single(tracker.Active).Id);
                    Assert.Equal(new[] { 1, 2, 3, 4 }, Assert.Single(tracker.Active).Riders);
                    Assert.Equal(1, state.Generation); Assert.Equal(phase != 1, state.Consumed);
                    Assert.Equal(0, tracker.PhysicalPairs[(3, 4)].Generation);
                    Assert.True(tracker.PhysicalPairs[(3, 4)].Consumed);
                }
                if (phase == 1)
                {
                    Assert.Equal(5, state.ClearSinceSeconds); Assert.Equal(5.45, state.ArmedAtSeconds);
                    var clear = CommonTimePoseHistory.Observe(tracker.History).Intervals.Where(r => r.RiderA == 1 && r.RiderB == 2
                        && r.IntervalStartSeconds >= 5 && r.IntervalEndSeconds <= state.ObservedUntilSeconds).ToArray();
                    Assert.NotEmpty(clear);
                    Assert.All(clear, r => { Assert.True(r.NumericallyResolved); Assert.False(r.HasConflict);
                        Assert.True(r.MinimumSeparationLowerBoundMeters > .65); });
                }
                if (phase >= 2)
                {
                    var bridge = tracker.PhysicalPairs[(2, 3)];
                    Assert.Equal(0, bridge.Generation); Assert.True(bridge.Consumed);
                    Assert.Null(bridge.ClearSinceSeconds);
                    Assert.Contains(CommonTimePoseHistory.Observe(tracker.History).Intervals, r => r.RiderA == 2 && r.RiderB == 3
                        && r.IntervalStartSeconds >= phase * 5 && r.NumericallyResolved && r.HasConflict);
                }
                phases.Add(new { resolved.Changes, resolved.Motions, resolved.Events, Plan = plan,
                    States = tracker.PhysicalPairs.OrderBy(p => p.Key).Select(p => new { p.Key.A, p.Key.B, p.Value }).ToArray() });
            }
            return phases;
        }
        Assert.Equal(JsonSerializer.Serialize(Run(false)), JsonSerializer.Serialize(Run(true)));
    }

    [Fact]
    public void AsynchronousCoverageAndDiscontinuityMustHaveACompleteNewReleaseInterval()
    {
        var tracker = new InteractionEpisodeTracker();
        var e = tracker.Engage(new[] { 1, 2, 3 }, 0, InteractionContext.MechanicalConflict);
        Consume(tracker, Pair(e.Id));
        PhysicalPoseInterval Pose(int id, double start, double end, double y, bool discontinuity = false)
            => new LinearBikePoseInterval(new(id, "async", new(20 * start, y), new(start, 0, 0), SpeedwayBikeDimensions.Reference),
                new(id, "async", new(20 * end, y), new(end, 0, 0), SpeedwayBikeDimensions.Reference), discontinuity);
        var incomplete = CommonTimePoseHistory.Observe(new[] { Pose(1, 1, 2, 0), Pose(2, 1, 1.3, 2) });
        tracker.ObservePhysicalClearance(incomplete, new());
        Assert.Equal(1.3, tracker.PhysicalPairs[(1, 2)].ObservedUntilSeconds);
        Assert.True(tracker.PhysicalPairs[(1, 2)].Consumed);
        // Missing 1.3..1.4 coverage and a declared state jump cannot join the old clock.
        var discontinuous = CommonTimePoseHistory.Observe(new[] { Pose(1, 1.4, 1.6, 0, true), Pose(2, 1.4, 1.6, .1) });
        Assert.Contains(discontinuous.Intervals, r => !r.NumericallyResolved || r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous));
        tracker.ObservePhysicalClearance(discontinuous, new());
        tracker.ObservePhysicalClearance(Separation(1.6, 1.9), new());
        Assert.True(tracker.PhysicalPairs[(1, 2)].Consumed);
        tracker.ObservePhysicalClearance(Separation(1.9, 2.2), new());
        Assert.Equal(1, tracker.PhysicalPairs[(1, 2)].Generation);
        Assert.True(tracker.CanApplyPhysical(Pair(e.Id, 3)));
    }

    [Theory]
    [InlineData(RiderRaceStatus.Finished)] [InlineData(RiderRaceStatus.Crashed)] [InlineData(RiderRaceStatus.Retired)]
    public void InactiveParticipantDoesNotErasePairOwnershipOrEndRemainingBattle(RiderRaceStatus status)
    {
        var scenario = ContestedSpaceResponseEvidence.Scenarios().Single(s => s.Name == "H-three-squeeze");
        var basis = ContestedSpaceResponseEvidence.Snapshot(scenario);
        var snapshot = new SimulationSnapshot(basis.Step, basis.Track, basis.TrackState,
            basis.Riders.Select(r => r.RiderId == 1 ? r with { Status = status } : r));
        var tracker = new InteractionEpisodeTracker();
        var e = tracker.Engage(new[] { 1, 2, 3 }, 0, InteractionContext.MechanicalConflict);
        Consume(tracker, Pair(e.Id));
        var state = tracker.PhysicalPairs[(1, 2)];
        tracker.ObserveClearance(1, Array.Empty<ContestedSpaceEvent>(), new(), snapshot);
        Assert.Equal(e.Id, Assert.Single(tracker.Active).Id);
        Assert.Equal(state, tracker.PhysicalPairs[(1, 2)]);
        Assert.False(tracker.CanApplyPhysical(Pair(e.Id, 2)));
    }

    private sealed class FrozenTarget : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }
}
