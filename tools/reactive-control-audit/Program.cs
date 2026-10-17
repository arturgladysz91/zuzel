using System.Diagnostics;
using System.Text.Json;
using CoreSim;
using CoreSim.Decisions;
using MidSegmentControl;
using ReactiveControl;

if (args.Length != 1) throw new ArgumentException("Supply an output JSON path.");
var rows = new List<object>(); var pairTrials = new List<object>(); var references = new List<object>();
object? initialA = null;
foreach (var name in new[] { "straight", "moving", "fixed-straight" })
{
    var context = ControlCases.Prepare(name); Measure(name, "no-source", context);
#if !BASELINE
    if (name.StartsWith("fixed")) continue;
    foreach (var mode in new[] { "quiet-own-pulses", "own-state-delivery-only" })
    {
        var source = mode == "quiet-own-pulses" ? (Func<ExecutedPathNode, ScheduledControlChange?>)(_ => null)
            : ReactiveEvidence.DeliverySource(context, name == "moving");
        Measure(name, mode, context with { Intents = new[] { context.Intents[0] with { Decision = context.Intents[0].Decision with { ControlSource = source } } } });
    }
#endif
}
foreach (var name in new[] { "A", "B", "C", "D" })
{
    var c = ReactiveEvidence.Pair(name); var step = ControlCases.Run(c); var geometry = ControlCases.Audit(step);
    references.Add(new { Scenario = name, Information = "OFFLINE_REFERENCE_NOT_REACTIVE_OBSERVATION", Fingerprint = ControlCases.Fingerprint(step),
        geometry.Work, Witnesses = geometry.Intervals.Where(i => i.MinimumSeparationMeters < -CoreSim.PhysicalSpace.GeometryNumerics.ContactDistanceMeters)
            .Select(i => new { i.MinimumSeparationCommonTimeSeconds, i.MinimumSeparationMeters }).Take(1), step.Changes });
    if (name == "A")
    {
        var initial = c.Engine.Resolve(c.Snapshot, c.Intents, ControlCases.Options);
        var audit = ControlCases.Audit(initial);
        initialA = new { Information = "INITIAL_SNAPSHOT_AND_KNOWN_INTENTS_WITH_56B_FORECASTS", initial.Changes,
            Responses = initial.Interaction!.Episodes.SelectMany(e => e.SelectedResponses), audit.Work,
            Fingerprint = ControlCases.Fingerprint(initial) };
    }
#if !BASELINE
    ReactiveEvidence.AttemptPair(c, name); var times = new List<double>(); var allocations = new List<long>();
    ReactiveEvidence.BlockedPair result = null!;
    for (var repeat = 0; repeat < 7; repeat++)
    {
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        result = ReactiveEvidence.AttemptPair(c, name); watch.Stop();
        times.Add(watch.Elapsed.TotalMilliseconds); allocations.Add(GC.GetAllocatedBytesForCurrentThread() - bytes);
    }
    pairTrials.Add(new { Result = result, MedianRefusalMilliseconds = times.Order().ElementAt(3), MedianRefusalBytes = allocations.Order().ElementAt(3),
        ObservedAt = (double?)null, DecisionAt = (double?)null, RequestedActivation = (double?)null, ActualActivation = (double?)null,
        Contacts = (int?)null, CheapPairChecks = 0, Alarms = (int?)null, Decisions56B = 0, AdditionalForecasts = 0, Geometry55 = 0, ControlChanges = 0 });
#endif
}
var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(new { Base = "d16df13cba27fb5f2aa270337d1d0f28fe1dc0a0", WarmupCalls = 4,
    Samples = 7, CallsPerSample = 4, Rows = rows, PairTrials = pairTrials, OfflineReferences = references,
    InitialA56B = initialA }, new JsonSerializerOptions { WriteIndented = true }));

void Measure(string name, string mode, ControlCases.Context context)
{
    for (var i = 0; i < 4; i++) ControlCases.Run(context);
    var times = new List<double>(); var bytes = new List<long>(); ResolvedSimulationStep step = null!;
    for (var sample = 0; sample < 7; sample++)
    {
        var allocation = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        for (var call = 0; call < 4; call++) step = ControlCases.Run(context);
        watch.Stop(); times.Add(watch.Elapsed.TotalMilliseconds / 4); bytes.Add((GC.GetAllocatedBytesForCurrentThread() - allocation) / 4);
    }
    var work = new Dictionary<string, int>();
    try { ProjectionCaptureAudit.Observer = (k, n) => work[k.ToString()] = work.GetValueOrDefault(k.ToString()) + n; ControlCases.Run(context); }
    finally { ProjectionCaptureAudit.Observer = null; }
    object? activation = null; double? observed = null, requested = null; var sourceCalls = 0;
#if !BASELINE
    activation = step.Diagnostics[0].ControlActivation;
    if (mode != "no-source")
    {
        var original = ControlCases.Prepare(name);
        var source = mode == "quiet-own-pulses" ? (Func<ExecutedPathNode, ScheduledControlChange?>)(_ => { sourceCalls++; return null; })
            : ReactiveEvidence.DeliverySource(original, name == "moving", (a, b) => { observed = a; requested = b; }, () => sourceCalls++);
        ControlCases.Run(original with { Intents = new[] { original.Intents[0] with { Decision = original.Intents[0].Decision with { ControlSource = source } } } });
    }
#endif
    rows.Add(new { Case = name, Mode = mode, MedianMilliseconds = times.Order().ElementAt(3), MedianBytes = bytes.Order().ElementAt(3),
        AcceptedSteps = step.Diagnostics[0].ExecutedPath?.Steps.Count ?? step.Motions[0].Nodes.Count - 1, Work = work,
        SourceCalls = sourceCalls, ObservedAt = observed, DecisionAt = observed, RequestedActivation = requested, Activation = activation,
        Fingerprint = ControlCases.Fingerprint(step), step.Changes });
}
