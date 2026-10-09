using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Tests;

// Shared unchanged with the audit built against the reviewed and corrected engines.
// A's state always comes from Commit. Contact partners have explicit frozen entry
// conditions; this is a controlled sequence of exposures, not a complete heat.
internal static class ContactRecoverySequenceFixtures
{
    internal enum Sequence { StrongWeak, WeakStrong, StrongBrush, StrongNone, Simultaneous, PersistentOverlap, StrongCrash, Finish }
    internal sealed record PairState(int A, int B, PhysicalPairGeneration State);
    internal sealed record RiderStateCapture(RiderSnapshot Rider, ContactRecoveryState? Recovery);
    internal sealed record Phase(int Number, SimulationSnapshot Snapshot, ResolvedSimulationStep Step,
        IReadOnlyList<RiderStateCapture> Incoming, IReadOnlyList<RiderStateCapture> AfterResolve,
        IReadOnlyList<RiderStateCapture> Committed, IReadOnlyList<PairState> BeforePairs,
        IReadOnlyList<PairState> AfterResolvePairs, IReadOnlyList<PairState> CommittedPairs);
    internal sealed record Trace(Sequence Case, int Seed, WeatherState Weather, IReadOnlyList<Phase> Phases,
        ResolvedSimulationStep MatchedThird, RiderStateCapture MatchedCommitted,
        ResolvedSimulationStep IncomingSolo, ResolvedSimulationStep UnimpairedSolo,
        ContactRecoveryState? ExpectedPending);

    internal static Trace Run(Sequence sequence, int seed, WeatherState weather, bool reverse)
    {
        var track = new Track(Enumerable.Range(0, 3).Select(i => new TrackSegment(i, SegmentType.Straight, 20)).ToArray(), TrackGeometry.Default);
        var surface = new TrackState(3, 5, (_, _) => new(1, 0, 0));
        TrackEvolution.ApplyWeather(track, surface, weather, 59, 0, new SimLog(false));
        var options = PhysicalContactConsequenceEvidence.Options with { Seed = seed, Weather = weather };
        RiderSnapshot Partner(int id, int segment, float speed, float time, float lateral = 2)
            => new(id, RiderProfile.CreateDefault(id), RiderPosition.Create(1, segment, .0625f, 3), segment - 1,
                2, lateral, speed, 0, RiderRaceStatus.Racing, time, BikeSetup.Neutral, .5f, .5f);
        var firstSpeed = sequence == Sequence.WeakStrong ? 16f : sequence == Sequence.Simultaneous ? 17f : 18f;
        var receiver = Partner(1, 0, firstSpeed, 0) with { Position = RiderPosition.Start(3) };
        var initial = new List<RiderSnapshot> { receiver, Partner(2, 0, 15, 0) };
        if (sequence == Sequence.Simultaneous) initial.Add(Partner(3, 0, 15, 0) with { Position = RiderPosition.Create(1, 0, .078125f, 3) });
        var context = new SimulationStepContext(59, 0, 0, 0, seed, sequence == Sequence.Finish ? 1 : 4);
        var snapshot = new SimulationSnapshot(context, track, surface.Snapshot(), reverse ? initial.AsEnumerable().Reverse() : initial);
        var live = snapshot.Riders.Select(r => r.ToMutableCopy()).ToList();
        var tracker = new InteractionEpisodeTracker();
        var engine = new SimulationEngine(new Target());
        var phases = new List<Phase>();
        PairState[] Pairs() => tracker.PhysicalPairs.OrderBy(p => p.Key).Select(p => new PairState(p.Key.A, p.Key.B, p.Value)).ToArray();
        RiderStateCapture[] Capture(SimulationSnapshot value) => value.Riders.OrderBy(r => r.RiderId)
            .Select(r => new RiderStateCapture(r, r.ContactRecovery)).ToArray();
        SimulationSnapshot LiveSnapshot(IEnumerable<RiderState> riders, int phase)
            => engine.CaptureSnapshot(track, surface, riders.OrderBy(r => reverse ? -r.RiderId : r.RiderId).ToArray(), context with { StepNumber = phase, SegmentIndex = phase });
        ContactRecoveryState? expectedPending = null;
        ResolvedSimulationStep? incomingSolo = null, unimpairedSolo = null, matchedThird = null;
        RiderStateCapture? matchedCommitted = null;
        for (var phase = 0; phase < 3; phase++)
        {
            if (phase == 1)
            {
                var a = live.Single(r => r.RiderId == 1);
                if (sequence is Sequence.StrongWeak or Sequence.WeakStrong or Sequence.StrongBrush or Sequence.StrongCrash or Sequence.Finish)
                {
                    var closing = sequence == Sequence.WeakStrong ? 3f : sequence == Sequence.StrongBrush ? .2f : sequence == Sequence.StrongCrash ? 4f : 1f;
                    live.Add(Partner(3, 1, a.Speed - closing, a.ElapsedTimeSeconds).ToMutableCopy());
                }
                snapshot = LiveSnapshot(sequence == Sequence.StrongNone ? live.Where(r => r.RiderId == 1) : live, phase);
                var solo = new SimulationSnapshot(snapshot.Step, track, snapshot.TrackState, new[] { snapshot.Rider(1) });
                var intent = new[] { new RiderIntent(1, new(3)) };
                incomingSolo = engine.ResolveProduction(solo, intent, options, legacyContacts: false);
                var clean = new SimulationSnapshot(solo.Step, track, solo.TrackState, new[] { solo.Rider(1) with { ContactRecovery = null } });
                unimpairedSolo = engine.ResolveProduction(clean, intent, options, legacyContacts: false);
            }
            if (phase == 2)
            {
                snapshot = LiveSnapshot(live.Where(r => r.RiderId == 1), phase);
                var matched = new SimulationSnapshot(snapshot.Step, track, snapshot.TrackState,
                    new[] { snapshot.Rider(1) with { ContactRecovery = expectedPending } });
                var detached = matched.Riders.Select(r => r.ToMutableCopy()).ToArray();
                var matchedSurface = new TrackState(3, 5, snapshot.TrackState.GetSurface);
                var matchedIntents = matched.Rider(1).IsActive ? new[] { new RiderIntent(1, new RiderDecision(3)) } : Array.Empty<RiderIntent>();
                matchedThird = engine.ResolveProduction(matched, matchedIntents, options, legacyContacts: false);
                engine.Commit(matchedThird, detached, matchedSurface, new SimLog(false));
                var committed = engine.CaptureSnapshot(track, matchedSurface, detached, snapshot.Step);
                matchedCommitted = Capture(committed).Single();
            }
            var incoming = Capture(snapshot);
            var beforePairs = Pairs();
            var intents = phase == 2
                ? snapshot.Rider(1).IsActive ? new[] { new RiderIntent(1, new RiderDecision(3)) } : Array.Empty<RiderIntent>()
                : engine.Decide(snapshot).ToArray();
            if (reverse) Array.Reverse(intents);
            // The solo exposure has no other rider's motion or new contact. Use
            // the same production core without a traffic-history reconciliation.
            var step = phase == 2 || sequence == Sequence.StrongNone && phase == 1
                ? engine.ResolveProduction(snapshot, intents, options, legacyContacts: false)
                : engine.Resolve(snapshot, intents, options, tracker);
            var activeLive = live.Where(r => snapshot.Riders.Any(s => s.RiderId == r.RiderId)).ToArray();
            var afterResolve = Capture(LiveSnapshot(activeLive, phase));
            var afterPairs = Pairs();
            if (phase == 1)
            {
                var next = step.Interaction?.PhysicalContactConsequences?.Riders.SingleOrDefault(c => c.RiderId == 1);
                if (next is not null && next.Severity is not (PhysicalContactSeverity.Brush or PhysicalContactSeverity.Crash)
                    && step.Changes.Single(c => c.RiderId == 1).Status == RiderRaceStatus.Racing)
                    expectedPending = new(next.SourceEpisodeIds.Count == 0 ? null : next.SourceEpisodeIds[0], next.FrontierTimeSeconds,
                        next.SeverityRatio, next.Severity, next.ControlLoss01);
            }
            engine.Commit(step, activeLive, surface, new SimLog(false));
            phases.Add(new(phase, snapshot, step, incoming, afterResolve, Capture(LiveSnapshot(activeLive, phase)),
                beforePairs, afterPairs, Pairs()));
        }
        return new(sequence, seed, weather, phases, matchedThird!, matchedCommitted!, incomingSolo!, unimpairedSolo!, expectedPending);
    }

    private sealed class Target : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(2);
    }
}
