using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using LearnedCorner166;
using LineThrottle156;
using SharedWorldSpeedway;
using WholeCorner171;
using Scenario = LearnedCorner166.Scenario;

namespace Cyclic172;

internal sealed class Study172
{
    private const string Docs = "docs/experiments/three-lap-global-planning";
    private const string Tool = "tools/three-lap-global-planning";
    private const string Source171 = "docs/experiments/whole-corner-planning";
    private const string Source166 = "docs/experiments/autonomous-fast-corner";
    private static readonly Process Clock = Process.GetCurrentProcess();
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    public static double Cpu => Clock.TotalProcessorTime.TotalSeconds;
    private readonly string stage, label, directory;
    private readonly double started = Cpu;
    private readonly double prior;
    private readonly TrackFrame frame = NativeWorld172.Frame();
    private readonly BendMap map;
    private readonly Teacher a, b;
    private readonly Weights weights;
    private readonly Integration tuning;
    private readonly Nominal[][] profiles;
    private readonly Scenario development = new("D1", "C", -.6, 6.12f);
    private readonly Dictionary<string, string> sources;
    private int worlds;
    private string At(string name) => Path.Combine(directory, name);

    public static int Main(string[] args)
    {
        if (args.Length != 3 || args[0] is not ("prepare" or "C1" or "C2" or "C3" or "probe" or "validate"))
        {
            Console.Error.WriteLine("Study #172 OFF. Choose prepare/C1/C2/C3/validate, unique label and evidence directory.");
            return 2;
        }
        Directory.CreateDirectory(args[2]);
        using var gate = new FileStream(Path.Combine(args[2], ".lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        new Study172(args[0], args[1], args[2]).Run();
        return 0;
    }

    private Study172(string stage, string label, string directory)
    {
        this.stage = stage; this.label = label; this.directory = directory;
        if (label.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Simple evidence label required");
        map = new(frame);
        var reserved = Directory.GetFiles(directory, "*.started.json");
        var completed = reserved.Select(p => p.Replace(".started.json", ".done.json", StringComparison.Ordinal)).ToArray();
        if (completed.Any(p => !File.Exists(p))) throw new InvalidOperationException("Unclosed reservation");
        prior = completed.Sum(p => Read<JsonElement>(p).GetProperty("CpuSeconds").GetDouble());
        worlds = File.Exists(At("ledger.jsonl")) ?
            File.ReadLines(At("ledger.jsonl")).Count(line => line.Contains("\"Event\":\"reserved\"", StringComparison.Ordinal)) : 0;
        var files = Directory.GetFiles(Tool, "*.cs").Concat(new[] { Tool + "/generate172.py", Docs + "/held.json",
            Source171 + "/profile.json" }).Order();
        sources = files.ToDictionary(p => p.Replace('\\', '/'), Hash);
        profiles = Read<Nominal[][]>(Source171 + "/profile.json");
        var fit = Read<JsonElement>(Source166 + "/evidence/fit.json");
        a = fit.GetProperty("A").Deserialize<Teacher>(Json)!;
        b = fit.GetProperty("B").Deserialize<Teacher>(Json)!;
        var seal166 = Read<JsonElement>(Source166 + "/evidence/freeze.json");
        if (Hash(Source166 + "/evidence/fit.json") != seal166.GetProperty("FitSha256").GetString())
            throw new InvalidOperationException("Frozen fit mismatch");
        weights = seal166.GetProperty("Selected").GetProperty("Weights").Deserialize<Weights>(Json)!;
        using var archive = ZipFile.OpenRead("docs/experiments/two-corner-full-drive/170-dowody.zip");
        using var stream = archive.GetEntry("freeze.json")!.Open();
        var frozen = JsonSerializer.Deserialize<JsonElement>(stream, Json);
        tuning = frozen.GetProperty("Tuning").Deserialize<Integration>(Json)!;
        foreach (var source in frozen.GetProperty("Sources").EnumerateObject())
            if (Hash(source.Name) != source.Value.GetString()) throw new InvalidOperationException("Frozen source " + source.Name);
    }

    private void Run()
    {
        Guard();
        if (stage != "prepare" && Hash(Docs + "/held.json") != Read<JsonElement>(At("protocol.json")).GetProperty("HeldSha256").GetString())
            throw new InvalidOperationException("Sealed validation states changed");
        if (File.Exists(At("freeze.json"))) throw new InvalidOperationException("Controller frozen. Research is closed.");
        Save(At(label + ".started.json"), new { Stage = stage, PreviousCpuSeconds = prior, Sources = sources });
        switch (stage)
        {
            case "prepare": Prepare(); break;
            case "C1": Execute(development, "K4", frame.LapMeters + map.At(0, 0).Entry + 10, false); break;
            case "C2":
                Require("C1");
                Execute(development, "K4", 2 * frame.LapMeters + map.At(0, 0).Entry + 10, false);
                break;
            case "C3":
                Require("C2");
                Execute(development, "K4", 3 * frame.LapMeters, true);
                break;
            case "probe": Probe(); break;
            case "validate":
                Require("C3");
                var held = Read<Scenario[]>(Docs + "/held.json");
                if (held.Length != 5 || held.Select(s => (s.Start, s.Offset)).Distinct().Count() != 5)
                    throw new InvalidOperationException("Five distinct holdouts required");
                Save(At("freeze.json"), new { Sources = sources, Held = held, PriorCpuSeconds = prior,
                    PhysicsSha256 = Historical(), Settings = new { Reaction = .24, Review = .2, Clearance = .5, Beta = 45,
                        Reserve = 1, MaximumCpuSeconds = 500, MaximumWorlds = 30 } });
                foreach (var scenario in held)
                {
                    Execute(scenario, "K4", 3 * frame.LapMeters, true);
                    Execute(scenario, "C0", 3 * frame.LapMeters, true);
                }
                break;
        }
        Save(At(label + ".done.json"), new { Stage = stage, CpuSeconds = Cpu - started,
            TotalCpuSeconds = prior + Cpu - started, Worlds = worlds });
    }

    private void Probe()
    {
        using var archive = ZipFile.OpenRead(At("c3a-K4-D1.zip"));
        using var stream = archive.GetEntry("native.json")!.Open();
        var native = JsonSerializer.Deserialize<Run144>(stream, Json)!;
        var samples = native.Riders.Single().Samples;
        var planner = new Planner172(frame, MultiActionExperiment135.Surface(frame), FreeLearningExperiment.Parameters(),
            development.Spec, 147, 52.72097f, profiles);
        var output = new List<object>();
        for (int lap = 1; lap < 3; lap++)
        for (int corner = 0; corner < 2; corner++)
        {
            var visit = map.At(lap, corner);
            foreach (double distance in new[] { -20d, -12, -4, 4, 12 })
            {
                var sample = samples.First(q => q.Input.Rider.TotalReferenceMeters >= visit.Entry + distance);
                var plans = planner.Build(visit, sample.Input.Rider, sample.CrankOmega, sample.Input.Omega);
                var feasible = plans.Where(p => p.Feasible).Select(p => p.Choice.Name).ToArray();
                output.Add(new { visit, distance, sample.Input.Time, Speed = sample.Input.Rider.Speed, Feasible = feasible,
                    Geometry = plans.Select(p => new { p.Choice, p.MinPose, p.MinSweep, p.Feasible }) });
                Console.WriteLine($"lap={lap} corner={corner} ds={distance} v={sample.Input.Rider.Speed * 3.6:F2} feasible={string.Join(",",feasible)}");
            }
        }
        Save(At(label + "-probe.json"), output);
    }
    private void Require(string previous)
    {
        bool passed = Directory.GetFiles(directory, "*-result.json").Select(Read<Outcome172>)
            .Any(x => x.Stage == previous && (previous == "C3" ? x.S3 && x.Reserve && x.AcceptedBends == 6 : x.ReachedStop));
        if (!passed) throw new InvalidOperationException(previous + " must pass first");
    }

    private void Prepare()
    {
        var targetMap = new CyclicReference172(a, b, weights, development.Spec, frame);
        var surface = MultiActionExperiment135.Surface(frame);
        var parameters = FreeLearningExperiment.Parameters();
        var policy = new Policy172(a, b, weights, development.Spec, frame, surface, parameters, 147, 52.72097f, targetMap);
        var records = new List<object>();
        const double epsilon = 1e-5;
        for (int lap = 0; lap < 3; lap++)
        {
            for (int corner = 0; corner < map.Count; corner++)
            {
                var visit = map.At(lap, corner);
                var sections = map.Sections(visit);
                for (int i = 0; i < sections.Length - 1; i++)
                {
                    double centre = (sections[i] + sections[i + 1]) / 2;
                    var location = map.Locate(centre);
                    if (location.Current != visit || location.Road.Segment != frame.Track.CornerTopology.Corners[corner].StartSegmentIndex + i)
                        throw new InvalidOperationException("Cyclic section identity");
                    records.Add(new { Visit = visit, Section = i + 1, SourceProfile =
                        visit.Entry == map.At(0, 0).Entry ? "standing first" : corner == 0 ? "flying first" : "flying second",
                        Target = policy.Target(centre) });
                }
            }
            double finish = (lap + 1) * frame.LapMeters;
            var left = map.Locate(finish - epsilon);
            var right = map.Locate(finish + epsilon);
            if (left.Lap != lap || right.Lap != lap + 1 || left.Next != right.Next ||
                targetMap.Follow(finish - epsilon) != targetMap.Follow(finish + epsilon))
                throw new InvalidOperationException("Finish mapping continuity");
        }
        var boundaries = Enumerable.Range(0, 4).SelectMany(lap =>
            Enumerable.Range(0, frame.Track.Segments.Count).Select(i => lap * frame.LapMeters + frame.SegmentStartMeters(i))).ToArray();
        foreach (double s in boundaries.Where(s => s > 0))
        {
            var lo = policy.Target(s - epsilon); var hi = policy.Target(s + epsilon);
            double[] jumps = [hi.Offset - lo.Offset, BikeAngles.Wrap(hi.Course - lo.Course), hi.Beta - lo.Beta,
                hi.Yaw - lo.Yaw, hi.Speed - lo.Speed, hi.Drive - lo.Drive];
            if (jumps.Any(x => !double.IsFinite(x) || Math.Abs(x) > .001))
                throw new InvalidOperationException("Reference discontinuity at " + s + " " + string.Join(",", jumps));
        }
        var before = map.Locate(-.5);
        if (before.Lap != -1 || before.Next != map.At(0, 0) ||
            policy.Target(frame.LapMeters + map.At(0, 0).Entry).Speed <= policy.Target(map.At(0, 0).Entry).Speed)
            throw new InvalidOperationException("Prestart or flying-speed profile");
        Save(At("mapping.json"), new { Records = records, Boundaries = boundaries, AllContinuous = true,
            FlyingEntrySpeed = targetMap.FlyingEntrySpeed });
        Save(At("geometry.json"), ReplayGeometry.Create(frame));
        Save(At("protocol.json"), new { HeldSha256 = Hash(Docs + "/held.json"), InitialSources = sources,
            FrozenHistory = Historical(), Development = development, frame.LapMeters,
            GlobalBudgetCpuSeconds = 500, GlobalBudgetWorlds = 30, ReserveValidationCpuSeconds = 150,
            First = map.At(0, 0), Second = map.At(0, 1) });
        Console.WriteLine($"18 sections, 6 bends, 3 finishes and all profile boundaries passed. L={frame.LapMeters:R}");
    }

    private Dictionary<string, string> Historical() => new[] {
        "tools/autonomous-fast-corner/Policy.cs", "tools/autonomous-fast-corner/Teacher.cs",
        "tools/autonomous-fast-corner/NativeWorld166.Generated.cs", "tools/two-corner-full-drive/ExitPolicy.cs",
        "tools/whole-corner-planning/BendMap.cs", "tools/whole-corner-planning/Planner.cs",
        "tools/whole-corner-planning/Tracker.cs", "tools/shared-world-speedway/StandingStart144/Contact144.cs",
        "tools/shared-world-speedway/RearWheelDynamics.cs", "tools/shared-world-speedway/Engine500.cs",
        "tools/shared-world-speedway/StandingStart144/CrankClutch144.cs", "tools/shared-world-speedway/TrackFrame.cs",
        "tools/shared-world-speedway/ExperimentalTrackShape.cs", "tools/shared-world-speedway/LoadBasedTyreGrip.cs"
    }.ToDictionary(p => p, Hash);

    private void Execute(Scenario scenario, string controller, double stop, bool reserve)
    {
        Guard();
        if (worlds >= (stage == "validate" ? 30 : 20)) throw new InvalidOperationException("Native world admission");
        worlds++;
        string id = label + "-" + controller + "-" + scenario.Id;
        Log(new { Event = "reserved", id, Stage = stage, scenario, controller, Stop = stop, reserve,
            TotalCpuSeconds = prior + Cpu - started, World = worlds, Sources = sources });
        CyclicController172? rider = null;
        var decisions = new List<CyclicDecision172>();
        var cpuMarks = new List<double[]>();
        double begin = Cpu;
        Decision144 Decide(FreeRacingInput input, CrankClutch144 crank, RiderSpec spec, TrackFrame geometry,
            TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, string variant)
        {
            Guard();
            rider ??= new(a, b, weights, tuning, profiles, controller == "C0" ? "K0" : "K4",
                spec, geometry, surface, parameters, mass, inertia);
            var decision = rider.Choose(input, crank);
            decision.Action.Validate();
            decisions.Add(decision);
            return new(new(input.Time, decision.Action, FreeActionSafety.Uncertain, false, [],
                decision.Queries, decision.Forecasts), decision.CpuSeconds);
        }
        using var world = new NativeWorld172(new(Enabled: true, RequiredLaps: 3, MaximumSeconds: 100,
            SoloGate: scenario.Field), diagnosticFrame: frame, diagnosticRider: scenario.Spec,
            safetyHalfMeter: true, stopReference: stop, postSeconds: reserve ? 1 : 0);
        var run = world.Run(id, "K1", Decide, (time, cpu) => cpuMarks.Add([time, cpu]));
        var plans = rider?.Plans.ToArray() ?? [];
        var result = Inspect172.Analyze(run, stage, controller, scenario, frame, stop, decisions.ToArray(), plans, Cpu - begin);
        string path = At(id + ".zip");
        using (var file = new FileStream(path, FileMode.CreateNew))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            Entry(archive, "native.json", run);
            Entry(archive, "result.json", result);
            Entry(archive, "decisions.json", decisions);
            Entry(archive, "plans.json", plans);
            Entry(archive, "cpu.json", cpuMarks);
        }
        Save(At(id + "-result.json"), result);
        Log(new { Event = "result", id, Stage = stage, result, Sha256 = Hash(path), TotalCpuSeconds = prior + Cpu - started });
        Console.WriteLine($"{id}: stop={result.ReachedStop} S3={result.S3} reserve={result.Reserve} laps={result.Crossings} " +
            $"t={result.FinishTime:F3} end={result.EndReference:F2} swept={result.MinSweep:F3} beta={result.MaxBetaDegrees:F2} " +
            $"plans={result.AcceptedBends} CPU={prior + Cpu - started:F2} {result.Reason}");
    }

    private void Guard()
    {
        if (prior + Cpu - started >= (stage == "validate" ? 500 : 350) - 2)
            throw new InvalidOperationException("Study172 CPU budget");
    }

    private void Log(object value) => File.AppendAllText(At("ledger.jsonl"), JsonSerializer.Serialize(value, Json) + "\n");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)!;
    private static void Save(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(stream, value, Json);
    }
    private static void Entry(ZipArchive archive, string name, object value)
    {
        using var stream = archive.CreateEntry(name).Open();
        JsonSerializer.Serialize(stream, value, Json);
    }
}

