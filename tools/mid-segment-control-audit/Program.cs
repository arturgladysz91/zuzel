using System.Diagnostics;
using System.Text.Json;
using CoreSim;
using CoreSim.Decisions;
using MidSegmentControl;

if (args.Length != 1) throw new ArgumentException("Supply an output JSON path.");
var rows = new List<object>();
foreach (var name in new[] { "straight", "corner", "moving", "fixed-straight", "fixed-corner" })
{
    var context = ControlCases.Prepare(name);
    Measure(name, "none", context);
#if !BASELINE
    var rider = context.Snapshot.Riders.Single();
    ControlCases.Context With(ScheduledControlChange change) => context with
    { Intents = context.Intents.Select(i => i with { Decision = i.Decision with { ScheduledControl = change } }).ToArray() };
    Measure(name, "disabled", With(new(rider.RiderId, .2, RiderDriveControl.LiftThrottle, enabled: false)));
    if (!name.StartsWith("fixed"))
    {
        Measure(name, "after-end", With(new(rider.RiderId, 1000, RiderDriveControl.LiftThrottle)));
        Measure(name, "at-0.2", With(new(rider.RiderId, .2, straight(name) ? RiderDriveControl.LiftThrottle : new(1),
            holdLateralPosition: straight(name), physicalTarget: straight(name) ? null : new InteractionLateralTarget(9))));
    }
#endif
}
var pairRows = new List<object>(); var fixedComparisons = new List<object>();
#if !BASELINE
var pair = ControlCases.Pair();
var freePair = ControlCases.Run(pair);
var immediatePair = pair.Engine.Resolve(pair.Snapshot, pair.Intents, ControlCases.Options);
var response = immediatePair.Interaction!.Episodes.SelectMany(e => e.SelectedResponses).Single(a => a.RiderId == 2);
var delayedPair = ControlCases.Run(pair with { Intents = pair.Intents.Select(i => i.RiderId != 2 ? i : i with
    { Decision = i.Decision with { ScheduledControl = new(2, .2, response.DriveControl ?? new(1),
        response.HoldLateralPosition, response.PhysicalTarget) } }).ToArray() });
foreach (var (variant, step) in new[] { ("none", freePair), ("immediate-56B", immediatePair), ("at-0.2", delayedPair) })
{
    var geometry = ControlCases.Audit(step);
    var witness = geometry.Intervals.Where(r => r.MinimumSeparationMeters < -CoreSim.PhysicalSpace.GeometryNumerics.ContactDistanceMeters
        && !r.Kind.HasFlag(CoreSim.PhysicalSpace.SpaceConflictKind.BoundaryAmbiguous)).OrderBy(r => r.MinimumSeparationCommonTimeSeconds).FirstOrDefault();
    pairRows.Add(new { Variant = variant, SelectedResponse = response, Activations = step.Diagnostics.Select(d => d.ControlActivation).Where(a => a is not null),
        geometry.Work, geometry.FrameCoverageGaps, ContactWitness = witness,
        ContactPairs = geometry.Intervals.Where(r => r.MinimumSeparationMeters < -CoreSim.PhysicalSpace.GeometryNumerics.ContactDistanceMeters)
            .Select(r => (r.RiderA, r.RiderB)).Distinct().Count(), step.Changes, step.Motions });
}
foreach (var name in new[] { "fixed-straight", "fixed-corner" })
{
    var c = ControlCases.Prepare(name); var fixedMotion = ControlCases.Run(c).Motions[0];
    var coupledMotion = ControlCases.Run(c with { Intents = c.Intents.Select(i => i with
        { Decision = i.Decision with { HoldLateralPosition = true } }).ToArray() }).Motions[0];
    var pairs = fixedMotion.Nodes.Zip(coupledMotion.Nodes).ToArray();
    fixedComparisons.Add(new { Case = name, FixedNodes = fixedMotion.Nodes.Count, CoupledNodes = coupledMotion.Nodes.Count,
        MaxIndexedTimeDifferenceSeconds = pairs.Max(p => Math.Abs(p.First.LocalTimeSeconds - p.Second.LocalTimeSeconds)),
        MaxIndexedSpeedDifferenceMetersPerSecond = pairs.Max(p => Math.Abs(p.First.SpeedMetersPerSecond - p.Second.SpeedMetersPerSecond)),
        MaxIndexedDistanceDifferenceMeters = pairs.Max(p => Math.Abs(p.First.TravelledMeters - p.Second.TravelledMeters)) });
}
#endif
var path = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, JsonSerializer.Serialize(new { Base = "561c06b6e486b4cbb61db66a3a2ad1fab33a0227",
    WarmupCalls = 4, Repetitions = 7, CallsPerSample = 4, Rows = rows, PairA = pairRows, FixedComparisons = fixedComparisons }, new JsonSerializerOptions { WriteIndented = true }));

#if !BASELINE
static bool straight(string name) => name == "straight";
#endif
void Measure(string name, string variant, ControlCases.Context context)
{
    for (var i = 0; i < 4; i++) ControlCases.Run(context);
    var samples = new List<(double Ms, long Bytes)>(); ResolvedSimulationStep step = null!;
    for (var i = 0; i < 7; i++)
    {
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        for (var call = 0; call < 4; call++) step = ControlCases.Run(context);
        watch.Stop();
        samples.Add((watch.Elapsed.TotalMilliseconds / 4, (GC.GetAllocatedBytesForCurrentThread() - bytes) / 4));
    }
    var counts = new Dictionary<string, int>();
    try
    {
        ProjectionCaptureAudit.Observer = (kind, n) => counts[kind.ToString()] = counts.GetValueOrDefault(kind.ToString()) + n;
        ControlCases.Run(context);
    }
    finally { ProjectionCaptureAudit.Observer = null; }
    var executed = step.Diagnostics[0].ExecutedPath;
    rows.Add(new { Case = name, Variant = variant, Milliseconds = samples.OrderBy(s => s.Ms).ElementAt(3).Ms,
        AllocatedBytes = samples.OrderBy(s => s.Bytes).ElementAt(3).Bytes,
        AcceptedSteps = executed?.Steps.Count ?? step.Motions[0].Nodes.Count - 1,
        CoupledEvaluations = counts.GetValueOrDefault("CoupledEvaluation"), PhysicalEvaluations = counts.GetValueOrDefault("PhysicalEvaluation"),
        Subdivisions = executed?.Steps.Sum(s => s.TimeSolveSubdivisions) ?? 0,
#if !BASELINE
        Activation = step.Diagnostics[0].ControlActivation,
#endif
        Fingerprint = ControlCases.Fingerprint(step), step.Changes,
        FinalDistanceMeters = step.Motions[0].TotalDistanceMeters, FinalTimeSeconds = step.Motions[0].TotalTimeSeconds });
}
