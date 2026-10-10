using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Race;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var mode = args[0];
var output = args[1];
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
if (mode == "type-probe")
{
    // Exercise the actual C# capture serializer, not synthetic Python-only values.
    var input = new Dictionary<string, object?>
    {
        ["integer"] = 1, ["integer_text"] = "1",
        ["boolean"] = true, ["boolean_text"] = "True",
        ["enum"] = InteractionResponse.Hold, ["enum_text"] = "Hold",
        ["character"] = '1', ["character_text"] = "1",
        ["decimal"] = 1.0m, ["decimal_text"] = "1.0",
        ["float"] = 1f, ["double"] = 1d, ["null"] = null,
    };
    var typeProbeValues = new SortedDictionary<string, object?>(StringComparer.Ordinal);
    foreach (var item in input) Walk(item.Value, item.Key, typeProbeValues);
    Write(output, typeProbeValues);
    return;
}
var scenarios = ContestedSpaceResponseEvidence.Scenarios().Concat(ContestedSpaceResponseEvidence.OwnershipScenarios()).ToList();
scenarios.Add(new("four-separated", 4, new[] {
    new ContestedRiderInput(1, 1, .05f, 22, new(1,1,1)),
    new ContestedRiderInput(2, 3, .30f, 22, new(3,3,3)),
    new ContestedRiderInput(3, 1, .55f, 22, new(1,1,1)),
    new ContestedRiderInput(4, 3, .80f, 22, new(3,3,3)) }));
scenarios.Add(scenarios.Single(s => s.Name == "B-close-too-late") with { Name = "safety-correction", Seed = 276, IncidentFrequency = 2 });
var tied = scenarios.Single(s => s.Name == "H-three-squeeze");
scenarios.Add(tied with {Name = "equal-tactical-costs", Riders = tied.Riders.Select(r=>r with {Combativeness = 0}).ToArray()});
scenarios.Add(tied with {Name = "adjacent-tactical-costs", Riders = tied.Riders.Select(r=>r with {Combativeness = MathF.BitIncrement(.5f)}).ToArray()});
var heat = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "I");
var stress = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "H");
var cases = new List<Case>();
foreach (var name in new[] {"four-separated", "H-three-squeeze", "G-four-first-bend", "real-bridge", "safety-correction"})
{
    var scenario = scenarios.Single(s => s.Name == name);
    cases.Add(new(name, capture => Resolve(scenario, true, false, capture)));
}
cases.Add(new("I-heat-ON", capture => RunHeat(heat, true, false, 7, WeatherState.Dry, capture)));
cases.Add(new("I-heat-OFF", capture => RunHeat(heat, false, false, 7, WeatherState.Dry, capture)));
cases.Add(new("repeated-contact", capture => {
    var tracker = new InteractionEpisodeTracker();
    for (var repeat = 0; repeat < 8; repeat++) Resolve(scenarios.Single(s => s.Name == "imminent-overlap"), true, false, capture, tracker, repeat*5);
}));
cases.Add(new("solo-heat", capture => RunHeat(heat with {Riders = heat.Riders.Take(1).ToArray()}, false, false, 7, WeatherState.Dry, capture)));
cases.Add(new("contact-diagnostics", capture => Resolve(scenarios.Single(s => s.Name == "imminent-overlap"), true, false, capture,
    physicalDiagnostics: true)));
if (mode == "capture")
{
    var manifests = new SortedDictionary<string, object>(StringComparer.Ordinal);
    void Capture(string name, object value)
    {
        var values = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        Walk(value,"root",values);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(values);
        manifests.Add(name,new {Leaves=values.Count, IEEELeaves=values.Values.Count(v=>v is string s && (s.StartsWith("float:",StringComparison.Ordinal)||s.StartsWith("double:",StringComparison.Ordinal))),
            SHA256=Convert.ToHexString(SHA256.HashData(bytes))});
        if (args.Contains("--raw") || args.Contains("--raw-case="+name))
            File.WriteAllBytes(output+"."+name.Replace('/','_')+".json",bytes);
    }
    foreach (var scenario in scenarios)
    foreach (var enabled in new[] {false, true})
    foreach (var reverse in new[] {false, true})
    {
        var collector = new Collector(true);
        Resolve(scenario, enabled, reverse, collector);
        Capture($"{scenario.Name}/{enabled}/{reverse}",collector.Values);
    }
    foreach (var fixture in new[] {heat, stress})
    foreach (var weather in new[] {WeatherState.Dry, WeatherState.LightRain})
    foreach (var seed in new[] {7,19})
    foreach (var reverse in new[] {false,true})
    {
        var collector = new Collector(true);
        RunHeat(fixture, true, reverse, seed, weather, collector);
        Capture($"heat/{fixture.Id}/{weather.Condition}/{seed}/{reverse}",collector.Values);
    }
    foreach (var reverse in new[] {false,true})
    {
        var collector = new Collector(true);
        RunHeat(heat, false, reverse, 7, WeatherState.Dry, collector);
        Capture($"heat/I/interactions-OFF/Dry/7/{reverse}",collector.Values);
    }
    Capture("ownership",ContestedSpaceResponseEvidence.OwnershipEvidence());
    foreach(var s in ContestedSpaceResponseEvidence.OwnershipScenarios())
        Capture("shadow/"+s.Name,PhysicalContactEvidence.Resolve(s,PhysicalContactDiagnosticsLevel.FullAudit));
    var repeated = new Collector(true); cases.Single(c=>c.Name=="repeated-contact").Run(repeated); Capture("persistent-overlap",repeated.Values);
    Write(output,manifests);
    Console.WriteLine($"Captured {manifests.Count} exact behavior manifests.");
    return;
}
var selected = args.Length > 2 ? cases.Where(c => c.Name == args[2]).ToArray() : cases.ToArray();
var samples = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 9;
var measurements = new List<object>();
using var process = Process.GetCurrentProcess();
var probe = typeof(SimulationEngine).Assembly.GetType("CoreSim.PerformanceProbe");
foreach (var test in selected)
{
    var warmupStart = Stopwatch.GetTimestamp();
    var warmups = 0;
    do { test.Run(new(false)); warmups++; }
    while (warmups < 8 || Stopwatch.GetElapsedTime(warmupStart).TotalSeconds < 3);
    var rows = new List<object>();
    for (var i = 0; i < samples; i++)
    {
        var collector = new Collector(false);
        probe?.GetMethod("Reset")!.Invoke(null,null);
        var gc = Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();
        var cpu = process.TotalProcessorTime;
        var allocated = GC.GetTotalAllocatedBytes(true);
        var start = Stopwatch.GetTimestamp();
        test.Run(collector);
        var wall = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var bytes = GC.GetTotalAllocatedBytes(true) - allocated;
        var cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
        rows.Add(new {WallMilliseconds = wall, CpuMilliseconds = cpuMs, AllocatedBytes = bytes,
            GC = Enumerable.Range(0,3).Select(g => GC.CollectionCount(g) - gc[g]).ToArray(), Work = collector.Work,
            Stages = ReadProbe(probe) });
    }
    measurements.Add(new {test.Name, Warmups = warmups, Samples = rows});
    Write(output, new {Schema = "contested-performance-v1", Measurements = measurements});
    Console.WriteLine($"Measured {test.Name} ({samples} samples).");
}
Write(output, new {Schema = "contested-performance-v1", Environment = new {
    OS = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription,
    Environment.ProcessorCount, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    Stopwatch.Frequency }, WarmupProtocol = "At least eight complete runs and at least three seconds per case", Samples = samples, Measurements = measurements});

static void Resolve(ContestedScenario scenario, bool enabled, bool reverse, Collector collector, InteractionEpisodeTracker? tracker=null,float time=0,
    bool physicalDiagnostics=false)
{
    var basis = ContestedSpaceResponseEvidence.Snapshot(scenario.Name=="safety-correction" ? scenario with {Seed=57} : scenario,reverse);
    var snapshot = new SimulationSnapshot(scenario.Name=="safety-correction" ? basis.Step with {Seed=276} : basis.Step,
        basis.Track,basis.TrackState,basis.Riders.Select(r=>r with {ElapsedTimeSeconds=time}));
    var intents = scenario.Riders.Select(r=>new RiderIntent(r.Id,new RiderDecision(r.Intent.TargetFor(snapshot.Segment.Type)){Trajectory=r.Intent})).ToArray();
    var engine = new SimulationEngine(new FixedDecision());
    var step = engine.Resolve(snapshot,reverse ? intents.Reverse().ToArray() : intents,new() {
        EnableContestedSpaceResponses=enabled,IncidentFrequency=scenario.IncidentFrequency,
        InteractionDiagnostics=collector.Capture ? InteractionDiagnosticsLevel.FullAudit : InteractionDiagnosticsLevel.Summary,
        PhysicalContactDiagnostics=physicalDiagnostics ? PhysicalContactDiagnosticsLevel.FullAudit : PhysicalContactDiagnosticsLevel.None},tracker);
    collector.OnStepResolved(step);
    if(tracker is not null) engine.Commit(step,snapshot.Riders.Select(r=>r.ToMutableCopy()).ToArray(),
        new TrackState(snapshot.Track.Segments.Count,5,snapshot.TrackState.GetSurface),new CoreSim.Logging.SimLog(false));
}
static void RunHeat(BehaviorScenario scenario, bool enabled, bool reverse, int seed, WeatherState weather, Collector collector)
{
    var riders = scenario.Riders.Select(r => r.Create(scenario.Track)).ToList();
    var surface = scenario.CreateSurface();
    var result = new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track, surface,
        (reverse ? riders.AsEnumerable().Reverse() : riders).ToList(), new() { Seed = seed, Weather = weather,
            EnableContestedSpaceResponses = enabled, InteractionDiagnostics = InteractionDiagnosticsLevel.Summary,
            EnableLogging = collector.Capture }, 57, collector);
    if (collector.Capture) collector.Values.Add(new {result.Classification, result.Log, Riders=riders.ToArray(), Surface = surface.Snapshot()});
}
static void Write(string path, object value) => File.WriteAllText(path,
    JsonSerializer.Serialize(value, new JsonSerializerOptions {WriteIndented = true}) + "\n");
static object? ReadProbe(Type? probe)
{
    if (probe is null) return null;
    var rows = (IDictionary)probe.GetField("Rows")!.GetValue(null)!;
    var result = new SortedDictionary<string,object>(StringComparer.Ordinal);
    foreach (DictionaryEntry row in rows)
    {
        var type = row.Value!.GetType();
        result[(string)row.Key] = new {
            Milliseconds = (long)type.GetField("Ticks")!.GetValue(row.Value)! * 1000.0 / Stopwatch.Frequency,
            AllocatedBytes = (long)type.GetField("Bytes")!.GetValue(row.Value)!,
            Calls = (long)type.GetField("Calls")!.GetValue(row.Value)! };
    }
    return result;
}
static void Walk(object? value, string path, IDictionary<string,object?> values)
{
    if (value is float single) {values[path] = $"float:{BitConverter.SingleToInt32Bits(single):X8}"; return;}
    if (value is double number) {values[path] = $"double:{BitConverter.DoubleToInt64Bits(number):X16}"; return;}
    if (value is null) {values[path] = null; return;}
    if (value is string text) {values[path] = "string:" + text; return;}
    if (value is char character) {values[path] = $"char:{(int)character:X4}"; return;}
    if (value is decimal dec)
    {
        // Preserve decimal sign and scale, not just its human-readable value.
        var bits = decimal.GetBits(dec);
        values[path] = $"decimal:{bits[0]:X8}:{bits[1]:X8}:{bits[2]:X8}:{bits[3]:X8}";
        return;
    }
    var type = value.GetType();
    if (type.IsEnum)
    {
        values[path] = $"enum:{type.FullName}:{Enum.Format(type, value, "D")}";
        return;
    }
    // Preserve integer and boolean JSON primitive types.
    if (type.IsPrimitive) {values[path] = value; return;}
    if (value is InteractionWork) return; // Only explicitly instrumentation-only work is excluded.
    if (value is TrackStateSnapshot surface)
    {
        for (var s = 0; s < surface.SegmentCount; s++) for (var lane = 0; lane < surface.LinesCount; lane++)
            Walk(surface.GetSurface(s,lane), $"{path}[{s},{lane}]", values);
        return;
    }
    if (value is IEnumerable sequence)
    {var i=0; foreach(var item in sequence) Walk(item, $"{path}[{i++}]", values); values[path+".Count"] = i; return;}
    foreach (var property in value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
        Walk(property.GetValue(value), path+"."+property.Name, values);
    // #56C2 adds nullable state/diagnostics absent from original main. Materialize
    // absence as null only in the old assembly; never omit a current value. Thus
    // OFF captures retain every historical leaf and reject any non-null new state.
    if (FeatureOffSchema.AddedNullableMembers.TryGetValue(type.FullName!, out var members))
        foreach (var name in members)
            if (type.GetProperty(name) is null) Walk(null, path+"."+name, values);
}
static class FeatureOffSchema
{
    internal static readonly IReadOnlyDictionary<string,string[]> AddedNullableMembers = new Dictionary<string,string[]>
    {
        ["CoreSim.RiderState"] = new[]{"ContactRecovery"},
        ["CoreSim.RiderSnapshot"] = new[]{"ContactRecovery"},
        ["CoreSim.RiderStateChange"] = new[]{"ContactRecovery"},
        ["CoreSim.SimulationStepEvent"] = new[]{"PhysicalContactConsequence"},
        ["CoreSim.RiderStepDiagnostics"] = new[]{"PhysicalContactConsequence","FinalSpeedMetersPerSecond","FinalStatus"},
        ["CoreSim.Interactions.InteractionResolution"] = new[]{"PhysicalContactConsequences"},
        ["CoreSim.Interactions.InteractionAlternative"] = new[]{"PhysicalTarget"},
    };
}
sealed record Case(string Name, Action<Collector> Run);
sealed class FixedDecision : IRiderDecisionModel
{public RiderDecision Decide(TrackSegment segment,RiderState rider)=>new(rider.Lane);}
sealed class Collector(bool capture) : ISimulationStepObserver
{
    public bool Capture => capture;
    public List<object> Values {get;} = new();
    public Dictionary<string,long> Work {get;} = new();
    public void OnStepResolved(ResolvedSimulationStep step)
    {
        var work = step.Interaction?.Work ?? new InteractionWork(0,1,0,0,0);
        foreach (var p in typeof(InteractionWork).GetProperties())
            if (p.GetValue(work) is int count) Work[p.Name] = Work.GetValueOrDefault(p.Name) + count;
        if (capture) Values.Add(new {Snapshot = new {step.Snapshot.Step, step.Snapshot.Riders, step.Snapshot.TrackState},
            step.Changes, step.Diagnostics, step.Motions, step.Events, step.Interaction});
    }
}
