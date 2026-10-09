using CausalAudit;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.Race;
using RaceReadiness;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class CausalImpactPrototypeTests
{
    private static StartFrontierPrototype.Result Run(SimulationSnapshot snapshot, InteractionEpisodeTracker tracker, bool reverse = false)
    {
        var engine = new SimulationEngine(new Fixtures.Hold());
        var intents = engine.Decide(snapshot).ToArray(); if (reverse) Array.Reverse(intents);
        return StartFrontierPrototype.Resolve(engine, snapshot, intents, Fixtures.Options, tracker);
    }

    [Theory, InlineData(.2f, 2), InlineData(1f, 2), InlineData(1f, 3), InlineData(1f, 4)]
    public void StartFrontierChangesFirstProductionMovementAndCommitsOnce(float closing, int count)
    {
        var snapshot = Fixtures.Rear(closing, count: count); var owner = new InteractionEpisodeTracker();
        var engine = new SimulationEngine(new Fixtures.Hold());
        var live = snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray();
        var surface = new TrackState(2, 5, snapshot.TrackState.GetSurface);
        var before = Exact.Hash(new { Riders = live, Surface = surface.Snapshot(), Tracker = Fixtures.State(owner) });
        var result = Run(snapshot, owner);
        Assert.Equal(before, Exact.Hash(new { Riders = live, Surface = surface.Snapshot(), Tracker = Fixtures.State(owner) }));
        Assert.Equal(1, result.EventFrontiers); Assert.Equal(0, result.PartialTraversals); Assert.Equal(1, result.PostImpactReplays);
        Assert.Equal(count - 1, result.EventPlan!.AppliedPairs.Count);
        Assert.All(result.EventPlan.AppliedPairs, p =>
        {
            Assert.Equal(0, p.FirstTouchCommonTimeSeconds); Assert.Equal(0, p.FrontierStartTimeSeconds);
            Assert.Equal(count, p.FrontierRiderIds.Count);
            Assert.Equal(p.Impulse!.ImpulseOnANewtonSeconds * -1, p.Impulse.ImpulseOnBNewtonSeconds);
            var preMomentum = p.Impulse.PreNormalMomentumKgMetersPerSecond;
            Assert.InRange(Math.Abs(preMomentum - p.Impulse.PostNormalMomentumKgMetersPerSecond), 0,
                2 * Math.Abs(Math.BitIncrement(preMomentum) - preMomentum));
        });
        foreach (var c in result.EventPlan.Riders)
        {
            var pre = result.PreImpact.Rider(c.RiderId); var post = result.PostImpact!.Rider(c.RiderId);
            Assert.Equal(pre.Position, post.Position); Assert.Equal(pre.ElapsedTimeSeconds, post.ElapsedTimeSeconds);
            Assert.Equal(pre.Speed, c.PreContactSpeed); Assert.Equal(c.PostContactSpeed, post.Speed);
            var motion = result.Executed.Motions.Single(m => m.RiderId == c.RiderId);
            Assert.Equal(post.Speed, motion.Initial.SpeedMetersPerSecond);
            Assert.Equal(0, motion.Initial.LocalTimeSeconds); Assert.Equal(0, motion.Initial.TravelledMeters);
            Assert.DoesNotContain(motion.StateTransitions, e => e.Kind == MotionStateTransitionKind.PhysicalContact);
            Assert.Equal(1, result.Executed.Changes.Single(r => r.RiderId == c.RiderId).Position.TotalSegmentProgress);
        }
        var firstC = result.EndpointC.Motions[0].Nodes.First(n => n.LocalTimeSeconds > 0);
        var firstD = result.Executed.Motions[0].Nodes.First(n => n.LocalTimeSeconds > 0);
        Assert.NotEqual(firstC.SpeedMetersPerSecond, firstD.SpeedMetersPerSecond);
        Assert.NotEqual(result.EndpointC.Changes[0].ElapsedTimeSeconds, result.Executed.Changes[0].ElapsedTimeSeconds);
        // Independent replay from the event state uses the exact same production request.
        var matched = engine.ResolveProduction(result.PostImpact!, result.PostImpact!.Riders.Select(r => new RiderIntent(r.RiderId,
            new RiderDecision(2) { HoldLateralPosition = true, DriveControl = RiderDriveControl.LiftThrottle })).ToArray(), Fixtures.Options, legacyContacts: false);
        Assert.Equal(Exact.Hash(matched.Motions), Exact.Hash(result.Executed.Motions));
        var matchedSurface = new TrackState(2, 5, snapshot.TrackState.GetSurface);
        engine.Commit(matched, result.PostImpact!.Riders.Select(r => r.ToMutableCopy()).ToArray(), matchedSurface, new SimLog(false));
        result.Commit(engine, live, surface, new SimLog(false));
        Assert.Equal(Exact.Hash(matchedSurface.Snapshot()), Exact.Hash(surface.Snapshot()));
        Assert.Equal(count - 1, owner.PhysicalPairs.Count);
        Assert.All(owner.PhysicalPairs.Values, p => { Assert.Equal(0, p.Generation); Assert.True(p.Consumed); });
        var after = Exact.Hash(new { Riders = live, Surface = surface.Snapshot(), Tracker = Fixtures.State(owner) });
        Assert.Throws<InvalidOperationException>(() => result.Commit(engine, live, surface, new SimLog(false)));
        Assert.Equal(after, Exact.Hash(new { Riders = live, Surface = surface.Snapshot(), Tracker = Fixtures.State(owner) }));
        foreach (var c in result.Executed.Changes)
        { Assert.Equal(c.Speed, live.Single(r => r.RiderId == c.RiderId).Speed); Assert.Equal(c.Position, live.Single(r => r.RiderId == c.RiderId).Position); }
    }

    [Fact]
    public void ContactCrashAtCommonStartDoesNotExecutePropulsionDistanceTimeOrWear()
    {
        var snapshot = Fixtures.Rear(4); var owner = new InteractionEpisodeTracker(); var result = Run(snapshot, owner);
        var surface = new TrackState(2, 5, snapshot.TrackState.GetSurface); var hash = Exact.Hash(surface.Snapshot());
        var live = snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray();
        Assert.All(result.EventPlan!.Riders, c => Assert.Equal(PhysicalContactSeverity.Crash, c.Severity));
        Assert.All(result.Executed.Changes, c => { Assert.Equal(0, c.ElapsedTimeSeconds); Assert.Equal(0, c.Speed);
            Assert.False(c.ApplySurfaceWear); Assert.Equal(snapshot.Rider(c.RiderId).Position, c.Position); });
        result.Commit(new SimulationEngine(new Fixtures.Hold()), live, surface, new SimLog(false));
        Assert.Equal(hash, Exact.Hash(surface.Snapshot())); Assert.All(live, r => Assert.True(r.IsCrashed));
    }

    [Fact]
    public void NoContactReturnsTheActualCResolutionBitExactly()
    {
        var snapshot = Fixtures.Rear(ahead: .4f, lateralGap: 1); var result = Run(snapshot, new());
        Assert.Same(result.EndpointC, result.Executed); Assert.Equal(0, result.EventFrontiers);
        Assert.Equal(Exact.Hash(result.EndpointC), Exact.Hash(result.Executed));
    }

    [Theory, InlineData(.125f), InlineData(.285f)]
    public void InteriorAndNearEndpointRequireAProductionCursorRatherThanInterpolatedExecution(float ahead)
    {
        var snapshot = Fixtures.Rear(closing: ahead == .285f ? 4 : 1, ahead: ahead);
        var owner = new InteractionEpisodeTracker(); var before = Exact.Hash(Fixtures.State(owner));
        var c = new SimulationEngine(new Fixtures.Hold()).Resolve(snapshot,
            new SimulationEngine(new Fixtures.Hold()).Decide(snapshot), Fixtures.Options, owner);
        var pair = Assert.Single(c.Interaction!.PhysicalContactConsequences!.AppliedPairs);
        Assert.True(pair.FirstTouchCommonTimeSeconds > 0);
        if (ahead == .285f) Assert.InRange(c.Motions[1].TotalTimeSeconds - pair.FirstTouchCommonTimeSeconds, 0, .2);
        var failure = Assert.Throws<UnsupportedCausalReplayException>(() => Run(snapshot, owner));
        Assert.Equal("PartialProductionCursorRequired", failure.Message);
        Assert.Equal(before, Exact.Hash(Fixtures.State(owner)));
    }

    [Fact]
    public void NewCommonTimeStateCannotRewindAnyCommittedRiderIncludingAnUninvolvedOne()
    {
        var snapshot = Fixtures.Rear();
        foreach (var id in new[] { 1, 2 })
        {
            var asynchronous = new SimulationSnapshot(snapshot.Step, snapshot.Track, snapshot.TrackState,
                snapshot.Riders.Select(r => r.RiderId == id ? r with { ElapsedTimeSeconds = .1f } : r));
            var failure = Assert.Throws<UnsupportedCausalReplayException>(() => CausalBoundary.RequireCommonStart(asynchronous, 0));
            Assert.StartsWith("CommittedBeyondFirstTouch", failure.Message);
        }
    }

    [Fact]
    public void RealProductionHistorySpansFirstTouchAndRequestedCausalExecutionFailsWithoutMutation()
    {
        var scenario = Catalog.All().Single(s => s.Id == "control"); var (track, surface, riders) = scenario.Create();
        var observer = new TimingObserver();
        new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(track, surface, riders, new() { Seed = 19,
            EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = true,
            PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit }, 91, observer);
        var contact = Assert.Single(observer.Complete().Where(c => c.HistoricalParticipant));
        Assert.Equal(8, contact.Step); Assert.Equal(1, contact.RiderId); Assert.Equal(7, contact.EventSegmentIndex);
        Assert.True(contact.EventSegmentStartSeconds < contact.FirstTouchSeconds);
        Assert.True(contact.FirstTouchSeconds < contact.CurrentSegmentStartSeconds);
        Assert.InRange(contact.CurrentSegmentStartSeconds - contact.FirstTouchSeconds, .0839, .0840);
        var step = observer.Steps.Single(s => s.Snapshot.Step.StepNumber == contact.Step);
        var before = Exact.Hash(step.Snapshot);
        var failure = Assert.Throws<UnsupportedCausalReplayException>(() => CausalBoundary.RequireCommonStart(step.Snapshot, contact.FirstTouchSeconds));
        Assert.Equal("CommittedBeyondFirstTouch: 1,3,4", failure.Message);
        Assert.Equal(before, Exact.Hash(step.Snapshot));
    }

    [Fact]
    public void FrozenFrontierAndCommittedResultAreIndependentOfIntentOrderAndObservers()
    {
        foreach (var count in new[] { 2, 3, 4 })
        {
            var snapshot = Fixtures.Rear(count: count); var normal = Run(snapshot, new()); var reversed = Run(snapshot, new(), true);
            Assert.Equal(Exact.Hash(new { normal.EventPlan, normal.Executed.Changes, normal.Executed.Motions }),
                Exact.Hash(new { reversed.EventPlan, reversed.Executed.Changes, reversed.Executed.Motions }));
            Assert.Equal(Exact.Hash(normal.EventPlan), Exact.Hash(reversed.EventPlan));
        }
    }

    [Fact]
    public void StaleResolutionIsRefusedBeforeRiderWearOrOwnershipMutation()
    {
        var snapshot = Fixtures.Rear(); var owner = new InteractionEpisodeTracker(); var result = Run(snapshot, owner);
        var live = snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray(); live[0].Speed += 1;
        var surface = new TrackState(2, 5, snapshot.TrackState.GetSurface);
        var before = Exact.Hash(new { live, Surface = surface.Snapshot(), Tracker = Fixtures.State(owner) });
        Assert.Throws<InvalidOperationException>(() => result.Commit(new SimulationEngine(new Fixtures.Hold()), live, surface, new SimLog(false)));
        Assert.Equal(before, Exact.Hash(new { live, Surface = surface.Snapshot(), Tracker = Fixtures.State(owner) }));
    }
    [Fact]
    public void SidePenetrationCannotBecomeInventedScalarOrLateralMotion()
    {
        var snapshot = Fixtures.Rear(ahead: 0, lateralGap: .3f);
        var owner = new InteractionEpisodeTracker(); var before = Exact.Hash(Fixtures.State(owner));
        var failure = Assert.Throws<UnsupportedCausalReplayException>(() => Run(snapshot, owner));
        Assert.Equal("ScalarLongitudinalTouchOnly", failure.Message);
        Assert.Equal(before, Exact.Hash(Fixtures.State(owner)));
    }

    [Fact]
    public void DisabledPrerequisitesDoNotSilentlyRunEndpointFallbackAsAnExperiment()
    {
        var snapshot = Fixtures.Rear(); var engine = new SimulationEngine(new Fixtures.Hold());
        Assert.Throws<UnsupportedCausalReplayException>(() => StartFrontierPrototype.Resolve(engine, snapshot,
            engine.Decide(snapshot), new(), new()));
    }

}
