using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;

namespace SharedWorldSpeedway;

public sealed record CcdProbeRow(bool BulletA, bool BulletB, int InternalSubsteps,
    bool ExternalProximitySubsteps, bool TransverseOrderReversed, RunResult Result);

public static class Audit
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var scenarios = new[] { Scenarios.A(), Scenarios.B(), Scenarios.B(false), Scenarios.C(), Scenarios.D(),
            Scenarios.E(), Scenarios.F(), Scenarios.G(), Scenarios.A(4), Scenarios.DenseStart(), Scenarios.Support() };
        var results = new List<RunResult>();
        foreach (var scenario in scenarios)
        foreach (var step in new[] { .02, .01 }) results.Add(Scenarios.Run(scenario, step));
        foreach (var scenario in new[] { Scenarios.C(), Scenarios.D(), Scenarios.E(), Scenarios.DenseStart() })
            results.Add(Scenarios.Run(scenario, .005));
        Save("scenarios.json", results);
        var determinism = new List<object>();
        foreach (var scenario in scenarios)
        {
            var original = results.Single(r => r.Name == scenario.Name && r.StepSeconds == .02);
            var repeat = Scenarios.Run(scenario, .02); var reversed = Scenarios.Run(scenario, .02, reverse: true);
            determinism.Add(new { scenario.Name, Repeat = original.Fingerprint == repeat.Fingerprint,
                Reversed = original.Fingerprint == reversed.Fingerprint, original.Fingerprint });
            if (original.Fingerprint != repeat.Fingerprint || original.Fingerprint != reversed.Fingerprint)
                throw new InvalidOperationException("Determinism regression: " + scenario.Name);
        }
        Save("determinism.json", determinism);
        Save("ccd.json", CcdProbe());
        // Only four requested workload families plus the adverse crowded start; bounded measurements.
        var measures = new List<object>();
        foreach (var scenario in new[] { Scenarios.A(), Scenarios.G(), Scenarios.Support(), Scenarios.A(4), Scenarios.DenseStart() })
        foreach (var step in new[] { .02, .01 })
        {
            var measured = Measure(() => Scenarios.Run(scenario, step), 2, 7, 2);
            measures.Add(new { scenario.Name, StepSeconds = step, measured.ElapsedMilliseconds,
                measured.CpuMilliseconds, measured.AllocatedBytes, measured.Samples, measured.Batch,
                SimulatedSeconds = measured.Last.TimeSeconds,
                MillisecondsPerSimulatedSecond = measured.ElapsedMilliseconds / measured.Last.TimeSeconds,
                Work = measured.Last.Work, measured.Last.Fingerprint });
        }
        Save("benchmark.json", measures);
        var legacy = new List<object>();
        foreach (var adaptive in new[] { false, true })
        {
            var measured = Measure(() => Legacy(adaptive), 1, 3, 1);
            legacy.Add(new { Model = adaptive ? "main-AdaptiveDecisionModel" : "main-fixed-lane",
                measured.ElapsedMilliseconds, measured.CpuMilliseconds, measured.AllocatedBytes, measured.Last });
        }
        Save("legacy.json", legacy);
        Save("environment.json", new { Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            Processors = Environment.ProcessorCount, TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            Package = "Box2D.NET 3.1.654", PackageSourceCommit = "5efc96def866edbb4e5a9368d84de5bf8c2dcaca",
            CpuTimer = "Process.TotalProcessorTime; accumulated across all measured calls; coarse Windows resolution",
            TimingScope = "World construction, simulation, diagnostics, binary hashing and disposal; CLI/JSON output excluded" });
        Console.WriteLine($"Saved {results.Count} executions, 10 CCD probes and bounded benchmarks to {Path.GetFullPath(directory)}");
        void Save(string name, object value) => File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, Json) + "\n");
    }
    public static IReadOnlyList<CcdProbeRow> CcdProbe()
    {
        var ccd = new List<CcdProbeRow>();
        foreach (var bullets in new[] { (false, false), (true, false), (true, true) })
        foreach (var internalSteps in new[] { 1, 4, 8 })
        {
            var p = new WorldParameters { NearbyMaximumTravelMeters = 1000, SolverSubsteps = internalSteps };
            var r = Scenarios.Run(Scenarios.E(bullets.Item1, bullets.Item2) with { DurationSeconds = .02 }, .02, p);
            ccd.Add(new(bullets.Item1, bullets.Item2, internalSteps, false,
                r.Riders[0].Road.OffsetMeters > r.Riders[1].Road.OffsetMeters, r));
        }
        var safe = Scenarios.Run(Scenarios.E() with { DurationSeconds = .02 }, .02);
        ccd.Add(new(false, false, 4, true, safe.Riders[0].Road.OffsetMeters > safe.Riders[1].Road.OffsetMeters, safe));
        return ccd;
    }
    private sealed record Measurement<T>(double ElapsedMilliseconds, double CpuMilliseconds,
        long AllocatedBytes, int Samples, int Batch, T Last);
    private static Measurement<T> Measure<T>(Func<T> run, int warmup, int samples, int batch)
    {
        for (var i = 0; i < warmup; i++) run();
        using var process = Process.GetCurrentProcess();
        var elapsed = new List<double>(); long allocated = 0; double cpu = 0; T last = default!;
        for (var i = 0; i < samples; i++)
        {
            var startAllocation = GC.GetAllocatedBytesForCurrentThread();
            var startCpu = process.TotalProcessorTime; var start = Stopwatch.GetTimestamp();
            for (var j = 0; j < batch; j++) last = run();
            elapsed.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds / batch);
            cpu += (process.TotalProcessorTime - startCpu).TotalMilliseconds;
            allocated += GC.GetAllocatedBytesForCurrentThread() - startAllocation;
        }
        elapsed.Sort();
        return new(elapsed[samples / 2], cpu / (samples * batch), allocated / (samples * batch), samples, batch, last);
    }
    private static object Legacy(bool adaptive)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack(); var specs = Scenarios.A(4).Riders;
        var riders = StartingGrid.Create(track, specs.Select((s, i) => new StartingGateAssignment(s.Profile, (StartingGate)i)).ToArray()).ToList();
        var simulator = new HeatSimulator(adaptive ? new AdaptiveDecisionModel() : new FixedLane());
        simulator.SimulateHeat(track, TrackState.CreateDefault(track), riders,
            new() { Seed = 19, Laps = 4, IncidentFrequency = 0, EnableLogging = false });
        return riders.Select(r => new { r.RiderId, r.ElapsedTimeSeconds, r.Speed, Status = r.Status.ToString(), r.LapsCompleted }).ToArray();
    }
    private sealed class FixedLane : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane); }
}
