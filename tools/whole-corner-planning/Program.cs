using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using FastTwoCorners167;
using FullDrive170;
using LearnedCorner166;
using LineThrottle156;
using SharedWorldSpeedway;
using Scenario = LearnedCorner166.Scenario;

namespace WholeCorner171;

internal sealed record Trial171(string Strategy, string Stage, double Tempo, Result Result, bool ReachedSecondEntry,
    Cross? SecondEntry, double Distance, double CpuSeconds, Tracking[] Reviews, int Plans, double PlanCpuSeconds);

internal sealed class Study
{
    private const string OutputDocs = "docs/experiments/whole-corner-planning";
    private const string Study170 = "docs/experiments/two-corner-full-drive";
    private const string Study166 = "docs/experiments/autonomous-fast-corner";
    private const string Tool = "tools/whole-corner-planning";
    private const double LimitCpuSeconds = 300;
    private const double ValidationReserveSeconds = 90;
    private static readonly Process Clock = Process.GetCurrentProcess();
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    internal static double Cpu => Clock.TotalProcessorTime.TotalSeconds;
    private readonly TrackFrame frame = NativeWorld166.Frame();
    private readonly Course course;
    private readonly BendMap map;
    private readonly Nominal[][] profiles = Read<Nominal[][]>(OutputDocs + "/profile.json");
    private readonly Scenario[] development = Read<Scenario[]>(Study166 + "/held.json")
        .Where(x => x.Id is "H3" or "H4").ToArray();
    private readonly Dictionary<string, string> sourceHashes;
    private readonly string directory, stage;
    private readonly double started = Cpu;
    private readonly double prior;
    private int reservedWorlds;
    private readonly Teacher a, b;
    private readonly Weights weights;
    private readonly Integration reference;
    private string At(string name) => Path.Combine(directory, name);

    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("screen" or "short" or "full" or "tempo" or "validate"))
        {
            Console.Error.WriteLine("Study #171 OFF. Choose screen, short, full, tempo or validate and an evidence directory.");
            return 2;
        }
        Directory.CreateDirectory(args[1]);
        using var phaseLock = new FileStream(Path.Combine(args[1], ".lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        new Study(args[0], args[1]).Run();
        return 0;
    }

    private Study(string stage, string directory)
    {
        this.stage = stage;
        this.directory = directory;
        course = new(frame);
        map = new(frame);
        reservedWorlds = File.Exists(At("ledger.jsonl")) ?
            File.ReadLines(At("ledger.jsonl")).Count(line => line.Contains("\"Event\":\"reserved\"", StringComparison.Ordinal)) : 0;
        var completed = Directory.GetFiles(directory, "*.started.json")
            .Select(path => path.Replace(".started.json", ".done.json", StringComparison.Ordinal)).ToArray();
        if (completed.Any(path => !File.Exists(path)))
            throw new InvalidOperationException("Unclosed phase reservation.");
        prior = completed.Sum(path => Read<JsonElement>(path).GetProperty("CpuSeconds").GetDouble());
        var sourceFiles = new[] { Tool + "/BendMap.cs", Tool + "/Planner.cs", Tool + "/Tracker.cs",
            Tool + "/Program.cs", OutputDocs + "/profile.json", OutputDocs + "/held.json" };
        sourceHashes = sourceFiles.ToDictionary(path => path, Hash);

        using var archive = ZipFile.OpenRead(Study170 + "/170-dowody.zip");
        using var freezeStream = archive.GetEntry("freeze.json")!.Open();
        var freeze170 = JsonSerializer.Deserialize<JsonElement>(freezeStream, Json);
        foreach (var source in freeze170.GetProperty("Sources").EnumerateObject())
            if (source.Value.GetString() != Hash(source.Name))
                throw new InvalidOperationException("Frozen #170 source changed: " + source.Name);
        reference = freeze170.GetProperty("Tuning").Deserialize<Integration>(Json)!;
        var frozen166 = Read<JsonElement>(Study166 + "/evidence/freeze.json");
        string fitPath = Study166 + "/evidence/fit.json";
        if (frozen166.GetProperty("FitSha256").GetString() != Hash(fitPath))
            throw new InvalidOperationException("Frozen #166 fit changed.");
        weights = frozen166.GetProperty("Selected").GetProperty("Weights").Deserialize<Weights>(Json)!;
        var fit = Read<JsonElement>(fitPath);
        a = fit.GetProperty("A").Deserialize<Teacher>(Json)!;
        b = fit.GetProperty("B").Deserialize<Teacher>(Json)!;
    }

    private void Run()
    {
        Guard();
        if (stage != "screen")
        {
            var protocol = Read<JsonElement>(At("protocol.json"));
            if (protocol.GetProperty("Holdout").GetString() != sourceHashes[OutputDocs + "/held.json"])
                throw new InvalidOperationException("Holdout changed after sealing.");
        }
        Save(At(stage + ".started.json"), new { Stage = stage, PreviousCpu = prior, Sources = sourceHashes });
        switch (stage)
        {
            case "screen": Screen(); break;
            case "short":
                var screen = Read<Plan[]>(At("screen.json"));
                foreach (string id in new[] { "K1", "K2", "K3", "K4" })
                    if (screen.Any(x => x.Choice.Name == id && x.Visit.Index == 0 && x.Feasible))
                        foreach (var scenario in development) Execute(scenario, id, 0, firstOnly: true);
                break;
            case "full":
                var ranked = ReadTrials("short").GroupBy(x => x.Strategy)
                    .Where(g => g.Count() == 2 && g.All(x => x.ReachedSecondEntry))
                    .Select(g => new { Id = g.Key, Advantage = g.Average(x =>
                        Archived(x.Result.Start.Id).Second.Entry!.Seconds - x.SecondEntry!.Seconds) })
                    .OrderByDescending(x => x.Advantage).ToArray();
                Save(At("short-ranking.json"), ranked);
                foreach (var row in ranked.Where(x => x.Advantage > .01).Take(2))
                    foreach (var scenario in development) Execute(scenario, row.Id, 0);
                break;
            case "tempo":
                var previous = BestFull(requireGain: false);
                if (previous is not null)
                    foreach (var scenario in development) Execute(scenario, previous.Strategy, .3);
                break;
            case "validate":
                var winner = BestFull(requireGain: true);
                Save(At("freeze.json"), new { Winner = winner, BaselineEligible = true, Sources = sourceHashes });
                if (winner is not null)
                {
                    var held = Read<Scenario[]>(OutputDocs + "/held.json");
                    if (held.Length != 8 || held.Select(s => (s.Start, s.Offset)).Distinct().Count() != 8)
                        throw new InvalidOperationException("Eight distinct start states are required.");
                    foreach (var scenario in held)
                    {
                        Execute(scenario, "K0", 0);
                        Execute(scenario, winner.Strategy, winner.Tempo);
                    }
                }
                break;
        }
        Save(At(stage + ".done.json"), new { CpuSeconds = Cpu - started, TotalCpuSeconds = prior + Cpu - started });
    }

    private sealed record Ranked(string Strategy, double Tempo, double Gain);

    private Ranked? BestFull(bool requireGain) =>
        ReadTrials("full").Concat(ReadTrials("tempo")).GroupBy(x => (x.Strategy, x.Tempo))
            .Where(g => g.Count() == 2 && g.All(x => x.Result.Pass))
            .Select(g => new Ranked(g.Key.Strategy, g.Key.Tempo, g.Average(x =>
                Archived(x.Result.Start.Id).Goal!.Seconds - x.Result.Goal!.Seconds)))
            .Where(x => !requireGain || x.Gain > .01)
            .OrderByDescending(x => x.Gain).FirstOrDefault();

    private void Screen()
    {
        var table = new List<object>();
        double tolerance = 1e-5;
        for (int lap = 0; lap < 3; lap++)
        {
            double finish = (lap + 1) * frame.LapMeters;
            var before = map.Locate(finish - tolerance);
            var after = map.Locate(finish + tolerance);
            var at = map.Locate(finish);
            if (before.Lap != lap || at.Lap != lap + 1 || at.Local != 0 || after.Lap != lap + 1 ||
                before.Next != map.At(lap + 1, 0) || after.Next != before.Next)
                throw new InvalidOperationException("Finish line mapping");
            for (int index = 0; index < map.Count; index++)
            {
                var visit = map.At(lap, index);
                var sections = map.Sections(visit);
                for (int j = 0; j < sections.Length - 1; j++)
                {
                    var left = map.Locate(sections[j] + tolerance);
                    var middle = map.Locate((sections[j] + sections[j + 1]) / 2);
                    var right = map.Locate(sections[j + 1] - tolerance);
                    if (left.Current != visit || middle.Current != visit || right.Current != visit ||
                        left.Road.Segment != right.Road.Segment ||
                        frame.Track.CornerTopology.Corners[index].StartSegmentIndex + j != left.Road.Segment)
                        throw new InvalidOperationException("Section or bend mapping");
                    double progress = middle.Local - map.At(0, index).Entry;
                    if (Math.Abs(progress - ((sections[j] + sections[j + 1]) / 2 - visit.Entry)) > tolerance)
                        throw new InvalidOperationException("Profile phase mapping");
                }
                table.Add(new { Visit = visit, Sections = sections, Finish = finish, Next = map.Locate(visit.Exit + tolerance).Next });
            }
        }
        if (map.Locate(-.5).Lap != -1 || map.Locate(-.5).Next != map.At(0, 0))
            throw new InvalidOperationException("Pre-start wrap");
        Save(At("mapping.json"), table);

        var offline = new Planner(frame, MultiActionExperiment135.Surface(frame), FreeLearningExperiment.Parameters(),
            development[0].Spec, 147, 52.72097f, profiles);
        long time = Stopwatch.GetTimestamp();
        double cpu = Cpu;
        var candidates = Enumerable.Range(0, map.Count).SelectMany(i => offline.Build(map.At(0, i))).ToArray();
        Save(At("screen.json"), candidates);
        Save(At("screen-timing.json"), new { CpuSeconds = Cpu - cpu, WallMilliseconds = Stopwatch.GetElapsedTime(time).TotalMilliseconds });
        foreach (var p in candidates)
            Console.WriteLine($"{p.Visit.Index + 1} {p.Choice.Name} {p.Choice.Entry:F2}/{p.Choice.Middle:F2}/{p.Choice.Exit:F2} " +
                $"T={p.Seconds:F3} d={p.Distance:F2} k={p.MaxCurvature:F4} swept~{p.MinSweep:F3} admissible={p.Feasible}");

        Save(At("protocol.json"), new { Holdout = sourceHashes[OutputDocs + "/held.json"], Sources = sourceHashes,
            course.First, course.Second, course.Goal, frame.LapMeters, Archived = development.Select(s => Archived(s.Id)) });
    }

    private Result Archived(string id)
    {
        using var evidence = ZipFile.OpenRead(Study170 + "/170-dowody.zip");
        using var inner = evidence.GetEntry($"reserve-speed-reserve-speed-{id}.zip")!.Open();
        using var archive = new ZipArchive(inner, ZipArchiveMode.Read);
        using var stream = archive.GetEntry("trial.json")!.Open();
        return JsonSerializer.Deserialize<JsonElement>(stream, Json).GetProperty("Result").Deserialize<Result>(Json)!;
    }

    private void Execute(Scenario scenario, string strategy, double tempo, bool firstOnly = false)
    {
        Guard();
        if (reservedWorlds >= (stage == "validate" ? 80 : 60))
            throw new InvalidOperationException("Study #171 native world budget");
        reservedWorlds++;
        string id = $"{stage}-{strategy}-{scenario.Id}";
        Log(new { Event = "reserved", id, scenario, strategy, tempo, CpuSeconds = prior + Cpu - started });
        var reviews = new List<Tracking>();
        Tracker? tracker = null;
        double cpu = Cpu;
        Decision144 Decide(FreeRacingInput input, CrankClutch144 crank, RiderSpec spec, TrackFrame geometry,
            TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, string variant)
        {
            Guard();
            double start = Cpu;
            if (tracker is null)
            {
                var original = new Policy(a, b, weights, spec, geometry, surface, parameters, mass, inertia);
                var exit = new ExitPolicy(original, a, b, weights, reference, course, spec, geometry, surface, parameters, mass, inertia);
                var planner = new Planner(geometry, surface, parameters, spec, mass, inertia, profiles);
                tracker = new Tracker(exit, planner, map, strategy, tempo, spec, geometry, surface, parameters, mass, inertia);
            }
            var review = tracker.Choose(input, crank);
            review.Chosen.Validate();
            reviews.Add(review);
            return new(new(input.Time, review.Chosen, FreeActionSafety.Uncertain, false, [],
                review.Queries, review.Forecasts), Cpu - start);
        }
        using var world = new NativeWorld166(new(Enabled: true, RequiredLaps: 2, MaximumSeconds: Course.MaximumSeconds,
            SoloGate: scenario.Field), diagnosticFrame: frame, diagnosticRider: scenario.Spec, safetyHalfMeter: true,
            stopReference: firstOnly ? course.Second.Entry : course.Goal, postSeconds: firstOnly ? 0 : Course.Reserve);
        var run = world.Run(id, "K1", Decide);
        var result = course.Inspect(run, scenario, independent: true, reviews.Sum(q => (long)q.Queries), Cpu - cpu);
        var samples = run.Riders.Single().Samples;
        var crossing = Runner.At(samples, course.Second.Entry);
        bool partialPass = crossing is not null && result.BadSeconds is null && run.Contacts.Length == 0;
        var plans = tracker?.Plans.ToArray() ?? [];
        var row = new Trial171(strategy, stage, tempo, result, partialPass, crossing,
            samples[^1].Input.Rider.DistanceMeters, Cpu - cpu, reviews.ToArray(),
            plans.Count(x => x.Chosen is not null), plans.Sum(x => x.CpuSeconds));
        string file = At(id + ".zip");
        using (var stream = new FileStream(file, FileMode.CreateNew))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "trial.json", row);
            WriteEntry(zip, "native.json", run);
            WriteEntry(zip, "plans.json", plans);
        }
        Log(new { Event = "result", id, strategy, firstOnly, Sha256 = Hash(file), CpuSeconds = prior + Cpu - started });
        string time = (firstOnly ? crossing?.Seconds : result.Goal?.Seconds)?.ToString("F3") ?? "—";
        string reason = firstOnly && partialPass ? "second entry" : result.Reason;
        Console.WriteLine($"{id}: pass={ (firstOnly ? partialPass : result.Pass) } t={time} s={result.EndReference:F2} " +
            $"β={result.MaximumBetaDegrees:F2} swept={result.MinimumSwept:F3} plans={row.Plans} CPU={prior + Cpu - started:F2} {reason}");
    }

    private Trial171[] ReadTrials(string phase) => Directory.GetFiles(directory, phase + "-*.zip").Order().Select(path =>
    {
        using var archive = ZipFile.OpenRead(path);
        using var stream = archive.GetEntry("trial.json")!.Open();
        return JsonSerializer.Deserialize<Trial171>(stream, Json)!;
    }).ToArray();

    private void Guard()
    {
        if (prior + Cpu - started >= (stage == "validate" ? LimitCpuSeconds : LimitCpuSeconds - ValidationReserveSeconds) - 1)
            throw new InvalidOperationException("Study #171 CPU budget");
    }

    private void Log(object row) => File.AppendAllText(At("ledger.jsonl"), JsonSerializer.Serialize(row, Json) + "\n");
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)!;
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Save(string path, object value)
    {
        using var file = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(file, value, Json);
    }
    private static void WriteEntry(ZipArchive zip, string name, object value)
    {
        using var stream = zip.CreateEntry(name).Open();
        JsonSerializer.Serialize(stream, value, Json);
    }
}

