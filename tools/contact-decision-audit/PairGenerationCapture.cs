using System.Collections;
using System.Reflection;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;

// The same source runs against reviewed fe3f8c6 and the correction. Reflection
// observes the added internal lifecycle; it never changes production behavior.
internal static class PairGenerationCapture
{
    internal static object Run(int seed, WeatherState weather, bool reverse)
    {
        var scenario = ContestedSpaceResponseEvidence.OwnershipScenarios().Single(s => s.Name == "two-disjoint-unresolved")
            with { Seed = seed };
        scenario = scenario with { Riders = scenario.Riders.Select(r => r.Id == 1
            ? r with { Speed = 22 } : r.Id == 2 ? r with { Lateral = .9f } : r).ToArray() };
        var basis = ContestedSpaceResponseEvidence.Snapshot(scenario, reverse);
        var surface = new TrackState(basis.Track.Segments.Count, 5, basis.TrackState.GetSurface);
        TrackEvolution.ApplyWeather(basis.Track, surface, weather, basis.Step.HeatId, 0, new SimLog(false));
        var snapshot = new SimulationSnapshot(basis.Step, basis.Track, surface.Snapshot(), basis.Riders);
        var tracker = new InteractionEpisodeTracker(); var engine = new SimulationEngine(new FrozenTarget());
        var options = PhysicalContactConsequenceEvidence.Options with { Seed = seed, Weather = weather };
        var intents = scenario.Riders.Select(r => new RiderIntent(r.Id,
            new(r.Intent.TargetFor(snapshot.Segment.Type)) { Trajectory = r.Intent })).ToArray();
        if (reverse) Array.Reverse(intents);
        var phases = new List<object>();
        var first = engine.Resolve(snapshot, intents, options, tracker);
        var live = snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray();
        engine.Commit(first, live, surface, new SimLog(false));
        phases.Add(new { Phase = "first-contact", Step = Capture(first), States = States(tracker) });
        tracker.Engage(new[] { 1, 2, 3, 4 }, 100, InteractionContext.MechanicalConflict);
        var pending = tracker.Clone();
        var shortClear = Separation(100, 100.3, reverse);
        for (var repeat = 0; repeat < 8; repeat++) pending.ObserveClearance(100.3, shortClear.Intervals, new());
        phases.Add(new { Phase = "short-clearance-repeated", States = States(pending) });
        pending.ObserveClearance(100.6, Separation(100.3, 100.6, reverse).Intervals, new());
        phases.Add(new { Phase = "certified-release", States = States(pending), LiveStates = States(tracker) });
        tracker.CommitFrom(pending);
        var later = new SimulationSnapshot(snapshot.Step with { StepNumber = 10 }, snapshot.Track, snapshot.TrackState,
            snapshot.Riders.Select(r => r with { ElapsedTimeSeconds = 101 }));
        var second = engine.Resolve(later, intents, options, tracker);
        phases.Add(new { Phase = "uncommitted-recontact", Step = Capture(second), States = States(tracker) });
        engine.Commit(second, live, surface, new SimLog(false));
        phases.Add(new { Phase = "committed-recontact", States = States(tracker) });
        var repeatSnapshot = new SimulationSnapshot(later.Step with { StepNumber = 11 }, later.Track, later.TrackState,
            later.Riders.Select(r => r with { ElapsedTimeSeconds = 106 }));
        phases.Add(new { Phase = "repeated-overlap", Step = Capture(engine.Resolve(repeatSnapshot, intents, options, tracker)),
            States = States(tracker) });
        return phases;
    }

    private static object Capture(ResolvedSimulationStep step)
        => new { step.Changes, step.Motions, step.Diagnostics, step.Events, step.Interaction,
            Analysis = step.Interaction!.PhysicalContactAnalysis, Applied = step.Interaction.PhysicalContactConsequences };

    private static object? States(InteractionEpisodeTracker tracker)
    {
        var dictionary = typeof(InteractionEpisodeTracker).GetProperty("PhysicalPairs", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(tracker) as IDictionary;
        return dictionary?.Keys.Cast<object>().Select(pairKey =>
        {
            var key = ((int A, int B))pairKey;
            return new { key.A, key.B, State = dictionary[pairKey] };
        }).OrderBy(e => e.A).ThenBy(e => e.B).ToArray();
    }

    private static ContestedSpaceReport Separation(double start, double end, bool reverse)
    {
        PhysicalPoseInterval Pose(int id, double y) => new LinearBikePoseInterval(
            new(id, "pair-release", new(0, y), new(start, 0, 0), SpeedwayBikeDimensions.Reference) { DeterministicArithmetic = true },
            new(id, "pair-release", new(20 * (end - start), y), new(end, 0, 0), SpeedwayBikeDimensions.Reference) { DeterministicArithmetic = true });
        var poses = new[] { Pose(1, 0), Pose(2, 2), Pose(3, 2.1) };
        return CommonTimePoseHistory.Observe(reverse ? poses.Reverse() : poses);
    }

    private sealed class FrozenTarget : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }
}
