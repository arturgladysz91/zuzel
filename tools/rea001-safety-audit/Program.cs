using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CoreSim;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Race;
using RaceReadiness;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var output = args[0];
var baseline = args.Contains("--baseline");
var repeats = int.Parse(args.ElementAtOrDefault(1) ?? "5", CultureInfo.InvariantCulture);
var rows = new List<object>();
foreach (var (id, seed, configuration) in new[] { ("balanced-motoarena", 19, "C"), ("contact-heavy", 19, "C"), ("outside", 7, "B"), ("outside", 7, "C") })
{
    var scenario = Catalog.All().Single(s => s.Id == id);
    var runs = new List<object>();
    var operations = new SortedDictionary<string, long>(StringComparer.Ordinal);
    for (var repeat = -1; repeat < repeats; repeat++) // one unmeasured warmup per population
    {
        var (track, surface, riders) = scenario.Create();
        var observer = new WorkObserver();
        // Count only the separate warmup. Timed/allocated runs have no materialization observer.
        ProjectionCaptureAudit.Observer = repeat == -1 ? (kind, count) =>
        {
            var name = kind.ToString();
            if (name.StartsWith("Safety", StringComparison.Ordinal) || name.EndsWith("AtFailure", StringComparison.Ordinal))
                operations[name] = operations.GetValueOrDefault(name) + count;
        } : null;
        var allocation = GC.GetTotalAllocatedBytes(true); var timer = Stopwatch.StartNew();
        string? failure = null; string? hash = null;
        try
        {
            var result = new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(track, surface, riders,
                new() { Seed = seed, EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = configuration == "C" }, 91, observer);
            timer.Stop(); allocation = GC.GetTotalAllocatedBytes(true) - allocation;
            hash = Exact.Hash(new { result.Classification, result.Log, Riders = riders.OrderBy(r => r.Profile.Id).ToArray(), Surface = surface.Snapshot() });
        }
        catch (Exception error) when (baseline && id == "outside")
        {
            timer.Stop(); allocation = GC.GetTotalAllocatedBytes(true) - allocation;
            failure = error.GetType().FullName + ": " + error.Message;
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        if (repeat >= 0) runs.Add(new { timer.Elapsed.TotalMilliseconds, AllocatedBytes = allocation, Failed = failure is not null,
            Failure = failure, FinalHash = hash, observer.Work, Operations = operations });
    }
    rows.Add(new { Id = id, Seed = seed, Configuration = configuration, Runs = runs });
    Console.WriteLine($"{id}/{seed}/{configuration}: {repeats} measured runs");
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(new { Baseline = baseline, Warmups = 1, Repeats = repeats, CountersSeparateWarmup = true,
    Environment = new { OS = Environment.OSVersion.ToString(), Runtime = Environment.Version.ToString(), Stopwatch.Frequency }, Cases = rows },
    new JsonSerializerOptions { WriteIndented = true }));

sealed class WorkObserver : ISimulationStepObserver
{
    internal SortedDictionary<string, long> Work { get; } = new(StringComparer.Ordinal);
    public void OnStepResolved(ResolvedSimulationStep step)
    {
        foreach (var property in typeof(InteractionWork).GetProperties())
            if (property.GetValue(step.Interaction?.Work ?? new(0, 1, 0, 0, 0)) is int count)
                Work[property.Name] = Work.GetValueOrDefault(property.Name) + count;
    }
}
