using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.Race;
using RaceReadiness;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class OptionalSafetyProjectionTests
{
    private static readonly InteractionAlternative BackOut = new(1, InteractionResponse.BackOut,
        new(0, 0, 0), RiderDriveControl.LiftThrottle, 0, "test", true);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OutsideSeedSevenCompletesWithExactReversalObserverAndRealConsequences(bool physical)
    {
        var first = Run(false, true);
        Assert.Equal(first.Hash, Run(true, true).Hash);
        Assert.Equal(first.Hash, Run(false, false).Hash);
        Assert.NotEmpty(first.Steps);
        var failed = first.Steps.SelectMany(s => s.Interaction!.Episodes).SelectMany(e => e.Candidates)
            .Where(c => c.Rejection.StartsWith("Infeasible optional production safety projection")).ToArray();
        Assert.NotEmpty(failed);
        Assert.All(failed, c => { Assert.False(c.Feasible); Assert.Null(c.Cost); Assert.Null(c.MinimumSeparationMeters); });
        Assert.All(first.Classification, r => { Assert.True(float.IsFinite(r.TimeSeconds)); Assert.True(r.Finished || r.Dnf); });
        Assert.Contains(first.Steps.SelectMany(s => s.Interaction!.Episodes), e => !e.ResolvedWithoutMechanicalContact
            && e.UnresolvedMechanicalContacts.Count > 0);
        var applied = first.Steps.SelectMany(s => s.Interaction!.PhysicalContactConsequences?.Riders
            ?? Array.Empty<RiderContactConsequence>()).ToArray();
        if (physical)
        {
            Assert.NotEmpty(applied);
            Assert.Contains(applied, r => r.PreContactSpeed != r.PostContactSpeed);
            Assert.Contains(applied, r => r.Recovery is not null);
            Assert.DoesNotContain(first.Steps.SelectMany(s => s.Interaction!.Episodes), e => e.LegacyFallbackUsed);
        }
        else Assert.Empty(applied);

        (string Hash, IReadOnlyList<RiderHeatResult> Classification, List<ResolvedSimulationStep> Steps) Run(bool reverse, bool observe)
        {
            var (track, surface, riders) = Catalog.All().Single(s => s.Id == "outside").Create();
            if (reverse) riders.Reverse();
            var observer = new Observer();
            var result = new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(track, surface, riders,
                new() { Seed = 7, EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = physical,
                    InteractionDiagnostics = InteractionDiagnosticsLevel.FullAudit }, 91, observe ? observer : null);
            return (Exact.Hash(new { result.Classification, result.Log, Riders = riders.OrderBy(r => r.Profile.Id).ToArray(),
                Surface = surface.Snapshot() }), result.Classification, observer.Steps);
        }
    }

    [Fact]
    public void DuplicateFailedPhysicalRequestUsesOneAttemptAndHasNoValue()
    {
        var attempts = 0;
        var cache = new OptionalSafetyProjectionCache<object>(_ => { attempts++; throw new ExecutedPathTraversal.TimeSolveDiscontinuityException(); });
        var result = cache.Get(BackOut);
        Assert.Same(result, cache.Get(BackOut with { Response = InteractionResponse.EmergencyAvoid, Reason = "another label" }));
        Assert.Null(result.Value); Assert.Equal(SafetyProjectionOutcome.InfeasibleProductionProjection, result.Outcome);
        Assert.Equal(1, attempts); Assert.Equal(1, cache.FailedCount); Assert.Equal(1, cache.Count); Assert.Equal(1, cache.CacheHits);
    }

    [Fact]
    public void RepeatingNaturalSolverFailureDoesNotResolveOrMutateAgain()
    {
        var (track, surface, riders) = Catalog.All().Single(s => s.Id == "outside").Create();
        var engine = new SimulationEngine(new Hold());
        var snapshot = engine.CaptureSnapshot(track, surface, new[] { riders[0] }, new(91, 0, 0, 0, 7, 4));
        var original = Exact.Hash(new { snapshot, Riders = riders, Surface = surface.Snapshot() });
        var attempts = 0;
        var cache = new OptionalSafetyProjectionCache<ResolvedSimulationStep>(alternative =>
        {
            attempts++;
            return engine.ResolveProduction(snapshot, new[] { new RiderIntent(1, new(0) { Trajectory = alternative.Intent,
                DriveControl = alternative.DriveControl, HoldLateralPosition = alternative.HoldLateralPosition }) },
                new() { Seed = 7, EnableContestedSpaceResponses = true }, legacyContacts: false);
        });
        var failure = cache.Get(BackOut);
        Assert.Null(failure.Value);
        Assert.Same(failure, cache.Get(BackOut with { Response = InteractionResponse.EmergencyAvoid }));
        Assert.Equal(1, attempts); Assert.Equal(1, cache.FailedCount);
        Assert.Equal(original, Exact.Hash(new { snapshot, Riders = riders, Surface = surface.Snapshot() }));
    }

    [Fact]
    public void CanonicalIdentityIncludesEveryPhysicalInputAndCachesSuccesses()
    {
        var cache = new OptionalSafetyProjectionCache<object>(_ => new object());
        var first = cache.Get(BackOut);
        Assert.Same(first, cache.Get(BackOut with { Response = InteractionResponse.Hold }));
        foreach (var distinct in new[] { BackOut with { RiderId = 2 }, BackOut with { Intent = new(0, 1, 0) },
            BackOut with { DriveControl = new(.5f) }, BackOut with { HoldLateralPosition = false } })
            Assert.NotSame(first, cache.Get(distinct));
        Assert.Equal(5, cache.Attempts); Assert.Equal(0, cache.FailedCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnexpectedInvalidOperationAndArgumentFailuresPropagate(bool argument)
    {
        var error = argument ? (Exception)new ArgumentOutOfRangeException("bad geometry") : new InvalidOperationException("bad state");
        var cache = new OptionalSafetyProjectionCache<object>(_ => throw error);
        Assert.Same(error, Assert.ThrowsAny<Exception>(() => cache.Get(BackOut)));
        Assert.Equal(0, cache.FailedCount); Assert.Equal(0, cache.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryNewCorrectionFailureRetainsFrozenActualContactAndCommitOwnership(bool physical)
    {
        var scenario = ContestedSpaceResponseEvidence.OwnershipScenarios().Single(s => s.Name == "two-disjoint-unresolved");
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(scenario);
        var engine = new SimulationEngine(new Hold()); var owner = new InteractionEpisodeTracker();
        var intents = scenario.Riders.Select(r => new RiderIntent(r.Id, new(r.Intent.TargetFor(snapshot.Segment.Type)) { Trajectory = r.Intent })).ToArray();
        var options = PhysicalContactConsequenceEvidence.Options with { EnablePhysicalContactConsequences = physical,
            InteractionDiagnostics = InteractionDiagnosticsLevel.FullAudit };
        var keys = new HashSet<ProjectionKey>(); var attempts = 0;
        var before = Exact.Hash(new { snapshot, owner.PhysicalPairs, owner.History });
        var step = new ContestedSpaceInteractionCoordinator(owner.Clone(), resolveOptionalSafety: (solo, intent, _) =>
        {
            attempts++;
            Assert.True(keys.Add(ProjectionKey.From(new(intent.RiderId, InteractionResponse.BackOut,
                intent.Decision.Trajectory!.Value, intent.Decision.DriveControl, 0, "", intent.Decision.HoldLateralPosition))));
            throw new ExecutedPathTraversal.TimeSolveDiscontinuityException();
        }).Resolve(engine, snapshot, intents, options, owner);
        Assert.True(attempts > 0); Assert.Equal(before, Exact.Hash(new { snapshot, owner.PhysicalPairs, owner.History }));
        Assert.All(step.Interaction!.Episodes, e =>
        {
            Assert.False(e.ResolvedWithoutMechanicalContact); Assert.NotEmpty(e.UnresolvedMechanicalContacts);
            Assert.All(e.UnresolvedMechanicalContacts, c => Assert.Contains("retaining previously executed actual motion", c.Reason));
            Assert.Equal(e.Pass1SelectedResponses, e.SelectedResponses);
        });
        // Required replay of precisely the retained requests has the same real positions, clocks and wear.
        var frozen = step.Interaction.Episodes.SelectMany(e => e.Pass1SelectedResponses).DistinctBy(a => a.RiderId).ToDictionary(a => a.RiderId);
        var actual = engine.ResolveProduction(snapshot, intents.Select(i => new RiderIntent(i.RiderId, i.Decision with
            { Trajectory = frozen[i.RiderId].Intent, TargetLane = frozen[i.RiderId].Intent.TargetFor(snapshot.Segment.Type),
                DriveControl = frozen[i.RiderId].DriveControl, HoldLateralPosition = frozen[i.RiderId].HoldLateralPosition })).ToArray(), options, legacyContacts: false);
        Assert.Equal(Exact.Hash(actual.Diagnostics.Select(d => d.ExecutedPath).ToArray()),
            Exact.Hash(step.Diagnostics.Select(d => d.ExecutedPath).ToArray()));
        foreach (var change in step.Changes)
        {
            var original = actual.Changes.Single(c => c.RiderId == change.RiderId);
            Assert.Equal(original.Position, change.Position);
            var losses = step.Events.Count(e => e.RiderId == change.RiderId && e.Type == SimulationEventType.ContactLostRhythm);
            Assert.InRange(losses, 0, 1);
            Assert.Equal(original.ElapsedTimeSeconds + (losses == 1 ? .20f : 0f), change.ElapsedTimeSeconds);
        }
        if (physical) Assert.Equal(2, step.Interaction.PhysicalContactConsequences!.AppliedPairs.Count);
        else Assert.Equal(2, step.Interaction.Work.LegacyFallbackAttempts);
        var live = snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray();
        engine.Commit(step, live, new(snapshot.Track.Segments.Count, 5, snapshot.TrackState.GetSurface), new SimLog(false));
        if (physical) Assert.All(owner.PhysicalPairs.Values, pair => Assert.True(pair.Consumed));
    }

    [Fact]
    public void MixedFailuresKeepTheCertifiedDeterministicWinnerSelectable()
    {
        var scenario = ContestedSpaceResponseEvidence.Scenarios().Single(s => s.Name == "B-close-too-late");
        ResolvedSimulationStep ResolveExpected()
        {
            var basis = ContestedSpaceResponseEvidence.Snapshot(scenario);
            var snapshot = new SimulationSnapshot(basis.Step with { Seed = 276 }, basis.Track, basis.TrackState, basis.Riders);
            return new SimulationEngine(new Hold()).Resolve(snapshot, scenario.Riders.Select(r => new RiderIntent(r.Id,
                new(r.Intent.TargetFor(snapshot.Segment.Type)) { Trajectory = r.Intent })).ToArray(),
                new() { IncidentFrequency = 2, EnableContestedSpaceResponses = true, InteractionDiagnostics = InteractionDiagnosticsLevel.FullAudit });
        }
        var expected = ResolveExpected();
        var selected = expected.Interaction!.Episodes.SelectMany(e => e.SelectedResponses).ToArray();
        Assert.Contains(expected.Interaction.Episodes, e => e.PassCount == 2 && e.ResolvedWithoutMechanicalContact);
        var selectedKeys = selected.Select(ProjectionKey.From).ToHashSet();
        string Run(bool reverse)
        {
            var basis = ContestedSpaceResponseEvidence.Snapshot(scenario, reverse);
            var snapshot = new SimulationSnapshot(basis.Step with { Seed = 276 }, basis.Track, basis.TrackState, basis.Riders);
            var engine = new SimulationEngine(new Hold()); var rejected = 0;
            var intents = scenario.Riders.Select(r => new RiderIntent(r.Id,
                new(r.Intent.TargetFor(snapshot.Segment.Type)) { Trajectory = r.Intent })).ToArray();
            if (reverse) Array.Reverse(intents);
            var step = new ContestedSpaceInteractionCoordinator(new(), resolveOptionalSafety: (solo, intent, options) =>
            {
                var key = new ProjectionKey(intent.RiderId, intent.Decision.Trajectory!.Value,
                    intent.Decision.DriveControl?.PositiveDriveFraction ?? 1f, intent.Decision.HoldLateralPosition);
                if (!selectedKeys.Contains(key)) { rejected++; throw new ExecutedPathTraversal.TimeSolveDiscontinuityException(); }
                return engine.ResolveProduction(solo, new[] { intent }, options, legacyContacts: false);
            }).Resolve(engine, snapshot, intents, new() { Seed = 276, IncidentFrequency = 2,
                EnableContestedSpaceResponses = true, InteractionDiagnostics = InteractionDiagnosticsLevel.FullAudit });
            Assert.True(rejected > 0);
            Assert.Equal(selected, step.Interaction!.Episodes.SelectMany(e => e.SelectedResponses).ToArray());
            Assert.All(step.Interaction.Episodes, e => Assert.True(e.ResolvedWithoutMechanicalContact));
            Assert.Equal(expected.Changes, step.Changes); Assert.Equal(expected.Motions, step.Motions);
            return Exact.Hash(new { step.Changes, step.Motions, step.Events, step.Interaction.Episodes });
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void GenuineRequiredOutsideBackOutFailureRemainsFatal()
    {
        var (track, surface, riders) = Catalog.All().Single(s => s.Id == "outside").Create();
        var engine = new SimulationEngine(new Hold());
        var snapshot = engine.CaptureSnapshot(track, surface, new[] { riders[0] }, new(91, 0, 0, 0, 7, 4));
        var original = Exact.Hash(new { Riders = riders, Surface = surface.Snapshot() });
        Assert.ThrowsAny<ExecutedPathTraversal.TimeSolveFeasibilityException>(() => engine.Resolve(snapshot,
            new[] { new RiderIntent(1, new(0) { Trajectory = new(0, 0, 0), DriveControl = RiderDriveControl.LiftThrottle,
                HoldLateralPosition = true }) }, new() { Seed = 7, EnableContestedSpaceResponses = true }));
        Assert.Equal(original, Exact.Hash(new { Riders = riders, Surface = surface.Snapshot() }));
    }

    private sealed class Observer : ISimulationStepObserver
    {
        internal readonly List<ResolvedSimulationStep> Steps = new();
        public void OnStepResolved(ResolvedSimulationStep step) => Steps.Add(step);
    }
    private sealed class Hold : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
        public RiderDecision Decide(RiderDecisionContext context) => new(context.Rider.Lane);
    }
}
