using System.Diagnostics;
using System.Text.Json;
using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using TimeStepPhysics;

if (args is ["--probe"])
{
    var actual = Scenarios.CaptureReal();
    foreach (var step in new[] { .005, .01, .02 })
    {
        var race = Scenarios.Import(actual); race.Run(2, step);
        Console.WriteLine(JsonSerializer.Serialize(new { step, race.Time, race.StopReason, race.LastRejectedWindow,
            Contacts = race.Contacts.Count, race.Work.SupportImpulses, race.Work.NarrowTests }));
    }
    return;
}
if (args.Length != 1) throw new ArgumentException("Supply an output JSON path, or --probe for the actual origin.");
var origin = Scenarios.CaptureReal(); // Capture/planning is outside every continuation measurement.
using var process = Process.GetCurrentProcess();
double Median(IEnumerable<double> values) => values.Order().ElementAt(1);
object States(TimeStepRace race) => race.Bikes.Select(b => new { Id = b.Rider.RiderId, b.Position, b.Velocity,
    b.Speed, b.LongitudinalSpeed, b.LateralSpeed, b.Heading, b.AngularVelocity, b.Status, b.Distance }).ToArray();
var rows = new List<object>(); var checkpoints = new Dictionary<(string, double), DynamicBike[]>();
foreach (var step in new[] { .005, .01, .02 }) foreach (var scenario in Scenarios.Names)
{
    TimeStepRace Create() => Scenarios.Create(scenario, origin);
    var checkpoint = scenario == "real-R3-R4" ? .02 : scenario == "encroach" ? .2 : 1;
    var warm = Create(); warm.Run(2, step);
    var samples = new List<(double Elapsed, double Cpu, long Bytes)>(); TimeStepRace? final = null;
    for (var sample = 0; sample < 3; sample++)
    {
        var race = Create(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        var cpu = process.TotalProcessorTime; var watch = Stopwatch.StartNew();
        race.Run(2, step);
        watch.Stop(); var usedCpu = (process.TotalProcessorTime - cpu).TotalMilliseconds;
        samples.Add((watch.Elapsed.TotalMilliseconds, usedCpu, GC.GetAllocatedBytesForCurrentThread() - allocated));
        final = race;
    }
    var comparison = Create(); comparison.Run(checkpoint, step);
    checkpoints[(scenario, step)] = comparison.Bikes.ToArray(); // outside the timed section
    var r = final!;
    rows.Add(new { Scenario = scenario, StepMilliseconds = step * 1000,
        SimulatedSeconds = r.Time - (scenario == "real-R3-R4" ? origin.First.Riders.Max(s => s.ExecutedThroughSeconds) : 0),
        ElapsedMedianMilliseconds = Median(samples.Select(s => s.Elapsed)), CpuMedianMilliseconds = Median(samples.Select(s => s.Cpu)),
        AllocatedMedianBytes = Median(samples.Select(s => (double)s.Bytes)), r.Work, r.MaximumPenetration,
        r.MaximumContactRefinementSeconds, r.StopReason, r.LastRejectedWindow, Contacts = r.Contacts.ToArray(), States = States(r) });
    Console.WriteLine($"measured {scenario} / {step * 1000} ms");
}
var convergence = Scenarios.Names.SelectMany(name => new[] { .01, .02 }.Select(step =>
{
    var reference = checkpoints[(name, .005)]; var candidate = checkpoints[(name, step)];
    return new { Scenario = name, StepMilliseconds = step * 1000,
        CommonHorizonSeconds = name == "real-R3-R4" ? .02 : name == "encroach" ? .2 : 1,
        MaximumPositionDifferenceMeters = candidate.Select((b, i) => (b.Position - reference[i].Position).Length).Max(),
        MaximumSpeedDifference = candidate.Select((b, i) => Math.Abs(b.Speed - reference[i].Speed)).Max() };
})).ToArray();

// Same imported scenario and common-end horizon; new tyre dynamics differ.
// No comparison of a rejected 3 ms old run with a completed 2 s new run.
var commonEnd = 4.1836476179597195;
var oldSamples = new List<(double Ms, double Cpu, long Bytes)>();
var newSamples = new List<(double Ms, double Cpu, long Bytes)>();
for (var iteration = -1; iteration < 3; iteration++)
{
    var old = Scenarios.CaptureReal(); var fresh = Scenarios.Import(old);
    var cpu = process.TotalProcessorTime; var bytes = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
    var result = old.Coordinator.ContinueAfterFirstContact(commonEnd - 1e-10);
    watch.Stop();
    if (result.Status != CausalRunStatus.HorizonReached) throw new InvalidOperationException("Comparable old horizon changed.");
    var oldSample = (watch.Elapsed.TotalMilliseconds, (process.TotalProcessorTime - cpu).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - bytes);
    cpu = process.TotalProcessorTime; bytes = GC.GetAllocatedBytesForCurrentThread(); watch.Restart();
    fresh.Run(commonEnd - fresh.Time, .01); watch.Stop();
    var newSample = (watch.Elapsed.TotalMilliseconds, (process.TotalProcessorTime - cpu).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - bytes);
    if (iteration >= 0) { oldSamples.Add(oldSample); newSamples.Add(newSample); }
}
var counted = Scenarios.CaptureReal(); var counts = new Dictionary<ProjectionMaterialization, int>();
var before = counted.Coordinator.Work.ObservedNativeStepStarts;
var narrowBefore = counted.Coordinator.Work.NarrowEvaluations;
var certificatesBefore = counted.Coordinator.Work.AllRiderCertificates + counted.Coordinator.Work.PairCertificates;
try
{
    ProjectionCaptureAudit.Observer = (kind, n) => counts[kind] = counts.GetValueOrDefault(kind) + n;
    counted.Coordinator.ContinueAfterFirstContact(commonEnd - 1e-10);
}
finally { ProjectionCaptureAudit.Observer = null; }
var comparableNew = Scenarios.Import(origin); comparableNew.Run(commonEnd - comparableNew.Time);
object Summary(List<(double Ms, double Cpu, long Bytes)> samples) => new
{ ElapsedMedianMilliseconds = Median(samples.Select(s => s.Ms)), CpuMedianMilliseconds = Median(samples.Select(s => s.Cpu)),
    AllocatedMedianBytes = Median(samples.Select(s => (double)s.Bytes)) };

// An isolated existing #54 planning probe, not part of the PoC controller or its timings.
var planner = new AdaptiveDecisionModel(); var rider = origin.First.Riders[2];
var planningSnapshot = new SimulationSnapshot(rider.Origin!.Step, origin.Heat.Track, origin.Heat.Surface, origin.First.Riders.Select(s => s.Rider));
var context = new RiderDecisionContext(planningSnapshot, rider.Rider);
planner.Decide(context); var planning = new List<double>();
for (var i = 0; i < 3; i++) { var watch = Stopwatch.StartNew(); planner.Decide(context); watch.Stop(); planning.Add(watch.Elapsed.TotalMilliseconds); }

var report = new { BaseSha = "5008f5f8e3fad997b5bc2eff3d8066b14e5d04d5", Warmups = 1, Repetitions = 3,
    Timer = "Stopwatch elapsed; process TotalProcessorTime (coarse CPU resolution); current-thread allocation",
    PocRoutePlannerCalls = 0, SeparateExistingPlannerMedianMilliseconds = Median(planning), Matrix = rows, Convergence = convergence,
    Comparable = new { CommonEndSeconds = commonEnd, NewOriginSeconds = origin.First.Riders.Max(s => s.ExecutedThroughSeconds),
        Old = Summary(oldSamples), New = Summary(newSamples),
        OldUntimedCounts = counts.ToDictionary(p => p.Key.ToString(), p => p.Value),
        OldNativeStepStarts = counted.Coordinator.Work.ObservedNativeStepStarts - before,
        OldNarrowTests = counted.Coordinator.Work.NarrowEvaluations - narrowBefore,
        OldGeometryCalls = counted.Coordinator.Work.AllRiderCertificates + counted.Coordinator.Work.PairCertificates - certificatesBefore,
        NewWork = comparableNew.Work } };
var path = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
Console.WriteLine(path);
