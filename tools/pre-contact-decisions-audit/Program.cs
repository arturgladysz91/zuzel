using System.Diagnostics;
using System.Text.Json;
using CoreSim;
using PreContactDecisions;

if (args.Length != 1) throw new ArgumentException("Supply an output JSON path.");
var rows = new List<object>();
foreach (var scenario in DecisionTimingProbe.Scenarios())
foreach (var waiting in new[] { false, true })
{
    var context = DecisionTimingProbe.Prepare(scenario);
    DecisionTimingProbe.Run(context, waiting); // warmup
    var samples = new List<(double Ms, long Bytes)>();
    DecisionTimingProbe.RunResult? result = null;
    for (var i = 0; i < 3; i++)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        result = DecisionTimingProbe.Run(context, waiting); watch.Stop();
        samples.Add((watch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated));
    }
    var counters = new Dictionary<string, int>();
    try
    {
        ProjectionCaptureAudit.Observer = (kind, n) => counters[kind.ToString()] = counters.GetValueOrDefault(kind.ToString()) + n;
        DecisionTimingProbe.Run(context, waiting); // separate untimed work capture
    }
    finally { ProjectionCaptureAudit.Observer = null; }
    var r = result!; var step = r.Step; var audit = r.DirectAudit ?? DecisionTimingProbe.Audit(step);
    var contacts = audit.Intervals.Where(c => c.HasConflict).GroupBy(c => (c.RiderA, c.RiderB))
        .Select(g => g.OrderBy(c => c.FirstTouchCommonTimeSeconds).First()).ToArray();
    var actualSamples = audit.Intervals.Where(c => c.MinimumSeparationMeters < -CoreSim.PhysicalSpace.GeometryNumerics.ContactDistanceMeters
        && !c.Kind.HasFlag(CoreSim.PhysicalSpace.SpaceConflictKind.BoundaryAmbiguous)).ToArray();
    var free = DecisionTimingProbe.Run(context, true).Step;
    rows.Add(new { Scenario = scenario.Name, Policy = waiting ? "next-real-boundary" : "main-56B",
        Cue = r.Cue ?? DecisionTimingProbe.Observe(context.Snapshot, context.Intents), r.ResponseApplied,
        ElapsedMedianMilliseconds = samples.OrderBy(s => s.Ms).ElementAt(1).Ms,
        AllocatedMedianBytes = samples.OrderBy(s => s.Bytes).ElementAt(1).Bytes, Counters = counters,
        GeometryNarrowEvaluations = step.Interaction?.Work.NarrowPhaseEvaluations ?? audit.Work.NarrowPhaseEvaluations,
        TrajectoryAlternativeEvaluations = step.Interaction?.Work.UniqueRiderAlternativeProjections ?? 0,
        AdditionalResponseSelections = step.Interaction?.Episodes.SelectMany(e => e.SelectedResponses)
            .Count(a => a.Response != CoreSim.Interactions.InteractionResponse.KeepIntent) ?? 0,
        CheapCueEvaluations = waiting ? 1 : 0, RoutePlannerCalls = 0,
        ContactPairsWithOverlapWitness = actualSamples.Select(c => (c.RiderA, c.RiderB)).Distinct().Count(),
        Work = step.Interaction?.Work, Changes = step.Changes,
        CounterfactualDeltas = step.Changes.Select(c => new { c.RiderId,
            TimeSeconds = c.ElapsedTimeSeconds - free.Changes.Single(f => f.RiderId == c.RiderId).ElapsedTimeSeconds,
            SpeedMetersPerSecond = c.Speed - free.Changes.Single(f => f.RiderId == c.RiderId).Speed,
            LateralMeters = c.LateralPosition * CoreSim.LaneModel.ReferenceLaneSpacingMeters(step.Snapshot.Segment.Type, step.Snapshot.Track.Geometry)
                - step.Snapshot.Rider(c.RiderId).LateralPosition * CoreSim.LaneModel.ReferenceLaneSpacingMeters(step.Snapshot.Segment.Type, step.Snapshot.Track.Geometry) }),
        Responses = step.Interaction?.Episodes.SelectMany(e => e.SelectedResponses),
        ExactAudit = new { audit.Work, Contacts = contacts, audit.FrameCoverageGaps, OverlapSample = actualSamples.FirstOrDefault(),
            FirstPotentialInterval = audit.Intervals.FirstOrDefault(c => c.HasConflict || !c.NumericallyResolved),
            CounterfactualNextDecisionSeconds = step.Motions.ToDictionary(m => m.RiderId, m => m.StartElapsedTimeSeconds + m.TotalTimeSeconds) } });
    Console.WriteLine($"{scenario.Name} / {(waiting ? "waiting" : "main")}: contacts={contacts.Length}");
}
var quiet = DecisionTimingProbe.Prepare(DecisionTimingProbe.Scenarios().Single(s => s.Name == "D-close-parallel"));
var quietSamples = new List<object>();
foreach (var addedCue in new[] { false, true })
{
    var elapsed = new List<double>(); var allocations = new List<long>();
    for (var repetition = -1; repetition < 3; repetition++)
    {
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        if (addedCue) DecisionTimingProbe.Observe(quiet.Snapshot, quiet.Intents);
        DecisionTimingProbe.Run(quiet, false); watch.Stop();
        var used = GC.GetAllocatedBytesForCurrentThread() - bytes;
        if (repetition >= 0) { elapsed.Add(watch.Elapsed.TotalMilliseconds); allocations.Add(used); }
    }
    quietSamples.Add(new { AddedAdvisoryCue = addedCue, ElapsedMedianMilliseconds = elapsed.Order().ElementAt(1),
        AllocatedMedianBytes = allocations.Order().ElementAt(1), AdditionalGeometry = 0, AdditionalTrajectories = 0, AdditionalResponses = 0 });
}
var path = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, JsonSerializer.Serialize(new { BaseSha = "561c06b6e486b4cbb61db66a3a2ad1fab33a0227",
    DecisionTimingProbe.ReactionDelaySeconds, Warmups = 1, Repetitions = 3,
    Rows = rows, NoRiskAdvisoryOverhead = quietSamples }, new JsonSerializerOptions { WriteIndented = true }));
