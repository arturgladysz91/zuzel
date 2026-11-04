using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using FastTwoCorners167;
using LearnedCorner166;
using LineThrottle156;
using SharedWorldSpeedway;
using Scenario = LearnedCorner166.Scenario;

namespace FullDrive170;

internal sealed record Trial(Result Result, string Controller, string Stage, Review[] Reviews,
    double DecisionWallMilliseconds, double NativeCpuSeconds, double PhysicsCpuSeconds);

internal sealed class Study
{
    private const string Historical = "docs/experiments/autonomous-fast-corner";
    private const string Research = "docs/experiments/two-corner-full-drive";
    private const string Tool = "tools/two-corner-full-drive";
    private const double CpuLimit = 350;
    private const double ValidationReserve = 100;
    private const double Delay = .24;
    private const double ReviewSeconds = .2;
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    private static readonly Process ProcessClock = Process.GetCurrentProcess();
    private static readonly Integration FirstExit = new(false, .75, 9);
    private static readonly Integration Recourse = FirstExit with { UseRecourse = true };
    private static readonly Integration RollOff = Recourse with { StabilizeRelease = true };
    private static readonly Integration BothExits = RollOff with { SecondExit = true };
    private static readonly Integration MoreHeadroom = BothExits with { SpeedHeadroom = 1.6 };
    private static readonly Integration ReserveSpeed = MoreHeadroom with { SecondExtraHeadroom = 1 };
    private static readonly Integration EarlierExit = BothExits with { ExitLeadMeters = 13 };
    private static readonly Dictionary<string, Integration> Configurations = new()
    {
        ["first"] = FirstExit, ["recourse"] = Recourse, ["rolloff"] = RollOff, ["both"] = BothExits, ["headroom"] = MoreHeadroom, ["earlier"] = EarlierExit, ["reserve-speed"] = ReserveSpeed
    };
    private static double Cpu => ProcessClock.TotalProcessorTime.TotalSeconds;
    private readonly TrackFrame frame = NativeWorld166.Frame();
    private readonly Course course;
    private readonly string directory;
    private readonly string stage;
    private readonly double started, charged, limit;
    private readonly Teacher a, b;
    private readonly Weights weights;
    private readonly string holdoutSha;
    private readonly Dictionary<string, string> sources;
    private string At(string name) => Path.Combine(directory, name);

    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("baseline" or "first" or "recourse" or "rolloff" or "both" or "headroom" or "earlier" or "reserve-speed" or "validate"))
        {
            Console.Error.WriteLine("Study #170 OFF. Choose baseline, first, recourse, rolloff, both, headroom, earlier, reserve-speed or validate and an evidence directory.");
            return 2;
        }
        string directory = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(directory);
        using var phaseLock = new FileStream(Path.Combine(directory, ".lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        new Study(directory, args[0]).Run();
        return 0;
    }

    private Study(string directory, string stage)
    {
        started = Cpu;
        this.directory = directory;
        this.stage = stage;
        course = new(frame);
        var prior = Directory.GetFiles(directory, "*.started.json")
            .Select(path => path.Replace(".started.json", ".done.json", StringComparison.Ordinal)).ToArray();
        if (prior.Any(path => !File.Exists(path)))
            throw new InvalidOperationException("An unclosed reservation blocks new native worlds.");
        charged = prior.Sum(path => Read<JsonElement>(path).GetProperty("CpuSeconds").GetDouble());
        limit = stage == "validate" ? CpuLimit : CpuLimit - ValidationReserve;
        if (stage != "baseline" && !File.Exists(At("baseline.done.json")))
            throw new InvalidOperationException("Run the untouched reference first.");
        if (stage is "both" or "headroom" or "earlier" or "reserve-speed" &&
            ReadTrials("rolloff").Count(t => t.Result.Pass) != 2)
            throw new InvalidOperationException("The first exit integration must pass both development starts.");
        if (stage is "headroom" or "earlier" or "reserve-speed" && ReadTrials("both").Count(t => t.Result.Pass) != 2)
            throw new InvalidOperationException("Both-exit integration must pass before tuning.");

        var freeze = Read<JsonElement>(Historical + "/evidence/freeze.json");
        string fit = Historical + "/evidence/fit.json";
        if (Hash(fit) != freeze.GetProperty("FitSha256").GetString())
            throw new InvalidOperationException("The #166 fit does not match its frozen bytes.");
        foreach (var file in freeze.GetProperty("Sources").EnumerateObject().Where(x => x.Name != "Program.cs"))
            if (Hash("tools/autonomous-fast-corner/" + file.Name) != file.Value.GetString())
                throw new InvalidOperationException("Frozen #166 source SHA mismatch for " + file.Name);
        var profile = Read<JsonElement>(fit);
        a = profile.GetProperty("A").Deserialize<Teacher>(Json)!;
        b = profile.GetProperty("B").Deserialize<Teacher>(Json)!;
        weights = freeze.GetProperty("Selected").GetProperty("Weights").Deserialize<Weights>(Json)!;
        holdoutSha = Hash(Research + "/held.json");
        sources = new[] { Tool + "/Program.cs", Tool + "/ExitPolicy.cs",
            "tools/fast-two-corners/Course.cs", "tools/safe-fast-contingency/PredictorA153.Generated.cs" }
            .ToDictionary(path => path, Hash);
        if (stage == "recourse" && !File.Exists(At("first.done.json")))
            throw new InvalidOperationException("Run the isolated first-exit integration first.");
        if (stage == "rolloff" && !File.Exists(At("recourse.done.json")))
            throw new InvalidOperationException("Run the recourse diagnosis first.");
        if (stage == "baseline")
        {
            Save(At("geometry.json"), ReplayGeometry.Create(frame));
            Save(At("protocol.json"), new { HoldoutSha256 = holdoutSha, Sources = sources,
                FitSha256 = Hash(fit), course.First, course.Second, course.Goal, Course.Reserve,
                frame.LapMeters, Delay, ReviewSeconds, CpuLimit, Configurations });
        }
        var protocol = Read<JsonElement>(At("protocol.json"));
        if (protocol.GetProperty("HoldoutSha256").GetString() != holdoutSha)
            throw new InvalidOperationException("Holdout changed after the protocol was sealed.");
    }

    private void Run()
    {
        Guard();
        Save(At(stage + ".started.json"), new { Stage = stage, PriorCpuSeconds = charged, Sources = sources });
        if (stage == "validate")
        {
            var development = Configurations.Keys.Where(key => File.Exists(At(key + ".done.json")))
                .Select(key => new { Name = key, Rows = ReadTrials(key) }).ToArray();
            var reference = ReadTrials("baseline");
            var eligible = development.Where(x => x.Rows.Length == reference.Length &&
                x.Rows.All(t => t.Result.Pass) && x.Rows.Count(t => t.Result.Pass) >= reference.Count(t => t.Result.Pass))
                .OrderBy(x => x.Rows.Average(t => t.Result.Goal!.Seconds)).FirstOrDefault();
            string? candidate = eligible?.Name;
            bool promoteOnDevelopment = eligible is not null &&
                eligible.Rows.Average(t => t.Result.Goal!.Seconds) <=
                1.01 * reference.Where(t => t.Result.Pass).Average(t => t.Result.Goal!.Seconds);
            Save(At("freeze.json"), new { Candidate = candidate, BaselineEligible = true, PromoteOnDevelopment = promoteOnDevelopment,
                Tuning = candidate is null ? null : Configurations[candidate], HoldoutSha256 = holdoutSha, Sources = sources });
            var held = Read<Scenario[]>(Research + "/held.json");
            if (held.Length != 8 || held.Select(x => (x.Start, x.Offset)).Distinct().Count() != 8)
                throw new InvalidOperationException("Eight distinct sealed holdout starts are required.");
            foreach (var scenario in held)
            {
                Execute(scenario, "baseline", null);
                if (candidate is not null)
                    Execute(scenario, candidate, Configurations[candidate]);
            }
        }
        else
        {
            var historical = Read<Scenario[]>(Historical + "/held.json");
            foreach (var id in new[] { "H3", "H4" })
                Execute(historical.Single(x => x.Id == id), stage, Configurations.GetValueOrDefault(stage));
        }
        Save(At(stage + ".done.json"), new { CpuSeconds = Cpu - started, TotalCpuSeconds = charged + Cpu - started });
    }

    private void Execute(Scenario scenario, string controller, Integration? tuning)
    {
        Guard();
        string id = $"{stage}-{controller}-{scenario.Id}";
        Log(new { Event = "reserved", id, stage, controller, scenario, CpuSeconds = charged + Cpu - started });
        double begin = Cpu;
        Policy? baseline = null;
        ExitPolicy? updated = null;
        var reviews = new List<Review>();
        double decisionWall = 0;
        Decision144 Decide(FreeRacingInput input, CrankClutch144 crank, RiderSpec spec, TrackFrame track,
            TrackStateSnapshot surface, WorldParameters p, float mass, float inertia, string variant)
        {
            Guard();
            long wall = Stopwatch.GetTimestamp();
            double cpu = Cpu;
            baseline ??= new(a, b, weights, spec, track, surface, p, mass, inertia);
            Review review;
            if (tuning is null)
            {
                long before = baseline.ForecastContactQueries;
                var action = baseline.Choose(input, crank);
                int queries = checked((int)(baseline.ForecastContactQueries - before));
                review = new(input.Time, input.Rider.TotalReferenceMeters, "reference", action, action,
                    0, "reference", queries > 0 ? 1 : 0, queries, Stopwatch.GetElapsedTime(wall).TotalMilliseconds);
            }
            else
            {
                updated ??= new(baseline, a, b, weights, tuning, course, spec, track, surface, p, mass, inertia);
                review = updated.Choose(input, crank);
            }
            reviews.Add(review);
            review.Chosen.Validate();
            decisionWall += Stopwatch.GetElapsedTime(wall).TotalMilliseconds;
            return new(new(input.Time, review.Chosen, FreeActionSafety.Uncertain, false, [], review.Queries, review.Forecasts), Cpu - cpu);
        }

        using var world = new NativeWorld166(new(Enabled: true, RequiredLaps: 2,
            MaximumSeconds: Course.MaximumSeconds, SoloGate: scenario.Field), diagnosticFrame: frame,
            diagnosticRider: scenario.Spec, safetyHalfMeter: true, stopReference: course.Goal, postSeconds: Course.Reserve);
        var run = world.Run(id, "K1", Decide);
        var result = course.Inspect(run, scenario, independent: true, reviews.Sum(r => (long)r.Queries), Cpu - begin);
        var trial = new Trial(result, controller, stage, reviews.ToArray(), decisionWall,
            run.TotalCpuSeconds, run.NativeAndObservationCpuSeconds);
        string path = At(id + ".zip");
        using (var output = new FileStream(path, FileMode.CreateNew))
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
        {
            using (var entry = archive.CreateEntry("native.json").Open()) JsonSerializer.Serialize(entry, run, Json);
            using (var entry = archive.CreateEntry("trial.json").Open()) JsonSerializer.Serialize(entry, trial, Json);
        }
        Log(new { Event = "result", id, stage, controller, result, Sha256 = Hash(path), TotalCpuSeconds = charged + Cpu - started });
        Console.WriteLine($"{id}: {result.Pass} t={result.Goal?.Seconds:F3} s={result.EndReference:F2} "
            + $"swept={result.MinimumSwept:F3} high={reviews.Count(r => r.Chosen.Drive >= .95)} "
            + $"CPU={charged + Cpu - started:F2} {result.Reason}");
    }

    private Trial[] ReadTrials(string phase) => Directory.GetFiles(directory, phase + "-*.zip").Select(path =>
    {
        using var zip = ZipFile.OpenRead(path);
        using var entry = zip.GetEntry("trial.json")!.Open();
        return JsonSerializer.Deserialize<Trial>(entry, Json)!;
    }).ToArray();

    private void Guard()
    {
        if (charged + Cpu - started >= limit - 1)
            throw new InvalidOperationException("Study #170 CPU limit reached.");
    }

    private void Log(object value) => File.AppendAllText(At("ledger.jsonl"), JsonSerializer.Serialize(value, Json) + "\n");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)!;
    private static void Save(string path, object data)
    {
        using var file = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(file, data, new JsonSerializerOptions(Json) { WriteIndented = true });
    }
}

