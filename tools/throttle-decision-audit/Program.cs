using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LearnedCorner166;
using LineThrottle156;
using SharedWorldSpeedway;
using WholeCorner171;

namespace Cyclic172;

internal sealed class Study173
{
    internal const string Docs172 = "docs/experiments/three-lap-global-planning";
    internal static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    private readonly double began = Study172.Cpu;
    private readonly string directory, label;
    private readonly double prior;
    private readonly int priorWorlds;
    private int nativeWorlds;
    internal readonly TrackFrame Frame = TrackAwarenessExperiment134.Frame("progressive-b");
    internal readonly Teacher A, B;
    internal readonly Weights Weights;
    internal readonly Integration Tuning;
    internal readonly Nominal[][] Profiles;
    private string At(string suffix) => Path.Combine(directory, label + suffix);

    public static int Main(string[] args)
    {
        if (args.Length != 3 || args[0] is not ("replay" or "counterfactual" or "native" or "geometry"))
        { Console.Error.WriteLine("Study #173 OFF. Explicit replay/counterfactual/native/geometry, unique label, evidence directory required."); return 2; }
        Directory.CreateDirectory(args[2]);
        using var gate = new FileStream(Path.Combine(args[2], ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        new Study173(args[1], args[2]).Execute(args[0]);
        return 0;
    }

    private Study173(string label, string directory)
    {
        this.label = label; this.directory = directory;
        if (label.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("Simple label required");
        var receipts = Directory.GetFiles(directory, "*.done.json").Select(Read<JsonElement>).ToArray();
        if (Directory.GetFiles(directory, "*.started.json").Length != receipts.Length)
            throw new InvalidOperationException("Unclosed reservation: charge the interrupted process before proceeding");
        prior = receipts.Sum(q => q.GetProperty("CpuSeconds").GetDouble());
        priorWorlds = receipts.Sum(q => q.GetProperty("NativeWorlds").GetInt32());
        prior += Directory.GetFiles(directory, "*.cpu.json").Select(Read<JsonElement>).Sum(q => q.GetProperty("CpuSeconds").GetDouble());
        var archivedAnalysis = Path.Combine(directory, "archive-analysis-cpu.json");
        if (File.Exists(archivedAnalysis)) prior += Read<JsonElement>(archivedAnalysis).GetProperty("CpuSeconds").GetDouble();
        Trace173.Guard = () => { if (prior + Study172.Cpu - began > 95) throw new InvalidOperationException("100 CPU-s budget reserve reached"); };
        var summary = Read<JsonElement>(Docs172 + "/summary.json");
        foreach (string group in new[] { "Sources", "PhysicsSha256" })
            foreach (var source in summary.GetProperty("freeze").GetProperty(group).EnumerateObject())
                if (Hash(source.Name) != source.Value.GetString()) throw new InvalidOperationException("Frozen #172 source mismatch " + source.Name);
        const string fitPath = "docs/experiments/autonomous-fast-corner/evidence/fit.json";
        var freeze166 = Read<JsonElement>("docs/experiments/autonomous-fast-corner/evidence/freeze.json");
        if (Hash(fitPath) != freeze166.GetProperty("FitSha256").GetString())
            throw new InvalidOperationException("Frozen #166 fit mismatch");
        var fit = Read<JsonElement>(fitPath);
        A = fit.GetProperty("A").Deserialize<Teacher>(Json)!; B = fit.GetProperty("B").Deserialize<Teacher>(Json)!;
        Weights = freeze166
            .GetProperty("Selected").GetProperty("Weights").Deserialize<Weights>(Json)!;
        Profiles = Read<Nominal[][]>("docs/experiments/whole-corner-planning/profile.json");
        using var zip = ZipFile.OpenRead("docs/experiments/two-corner-full-drive/170-dowody.zip");
        using var input = zip.GetEntry("freeze.json")!.Open();
        Tuning = JsonSerializer.Deserialize<JsonElement>(input, Json).GetProperty("Tuning").Deserialize<Integration>(Json)!;
    }

    internal CyclicController172 Controller(RiderSpec spec) => new(A, B, Weights, Tuning, Profiles, "K4", spec,
        Frame, MultiActionExperiment135.Surface(Frame), FreeLearningExperiment.Parameters(), 147, 52.72097f);

    private void Execute(string stage)
    {
        if (stage == "native" && File.Exists(Path.Combine(directory, "native-closed.json")))
            throw new InvalidOperationException("Native diagnosis closed; no additional rollouts authorized by this protocol");
        Save(At(".started.json"), new { Stage = stage, PreviousCpuSeconds = prior, NativeWorlds = 0,
            Sources = Directory.GetFiles("tools/throttle-decision-audit").Where(p => p.EndsWith(".cs") || p.EndsWith(".py") || p.EndsWith(".csproj"))
                .ToDictionary(p => p.Replace('\\', '/'), Hash), Archives = new[] { "K4", "C0" }.ToDictionary(x => x, x => Hash(Docs172 + "/172-validation-" + x + ".zip")) });
        string? error = null;
        try
        {
            if (stage == "replay") Replay();
            else if (stage == "counterfactual") Counterfactual173.Run(this, directory, label);
            else if (stage == "native") Native173.Run(this, directory, label);
            else Geometry();
        }
        catch (Exception e) { error = e.Message; throw; }
        finally { Save(At(".done.json"), new { Stage = stage, CpuSeconds = Study172.Cpu - began, NativeWorlds = nativeWorlds, Error = error }); }
    }

    private void Replay()
    {
        using var outer = ZipFile.OpenRead(Docs172 + "/172-validation-K4.zip");
        foreach (var entry in outer.Entries.OrderBy(q => q.FullName))
        {
            Trace173.Guard();
            using var source = entry.Open(); using var memory = new MemoryStream(); source.CopyTo(memory); memory.Position = 0;
            using var zip = new ZipArchive(memory);
            var native = Read<Run144>(zip, "native.json");
            var expected = Read<CyclicDecision172[]>(zip, "decisions.json");
            var rider = native.Riders.Single(); var controller = Controller(rider.Spec);
            using var file = new FileStream(At("-" + Path.GetFileNameWithoutExtension(entry.FullName) + ".jsonl.gz"), FileMode.CreateNew);
            using var compressed = new GZipStream(file, CompressionLevel.Optimal);
            using var writer = new StreamWriter(compressed);
            int matched = 0;
            foreach (var old in expected)
            {
                Trace173.Begin(old.Time);
                var sample = rider.Samples.MinBy(s => Math.Abs(s.Input.Time - old.Time))!;
                if (Math.Abs(sample.Input.Time - old.Time) > 1e-8) throw new InvalidOperationException("Missing review sample");
                var input = Observation(sample, old.Time);
                var actual = controller.Choose(input, new CrankClutch144(omega: sample.CrankOmega));
                bool equal = actual.Action == old.Action && actual.Proposal == old.Proposal && actual.Transition == old.Transition &&
                    actual.Tracking.Baseline == old.Tracking.Baseline && actual.Tracking.Policy == old.Tracking.Policy && actual.Tracking.Gate == old.Tracking.Gate &&
                    actual.Forecasts == old.Forecasts && actual.Queries == old.Queries;
                if (!equal) throw new InvalidOperationException($"Replay mismatch {entry.FullName} t={old.Time:R}: " +
                    JsonSerializer.Serialize(new { ActualAction = actual.Action, OldAction = old.Action, actual.Tracking, OldTracking = old.Tracking, actual.Forecasts, OldForecasts = old.Forecasts }, Json));
                matched++;
                writer.WriteLine(JsonSerializer.Serialize(new { Trial = Path.GetFileNameWithoutExtension(entry.FullName),
                    Reconstruction = true, Matched = equal, Observation = input, sample.CrankOmega,
                    Archived = old, Events = Trace173.Events }, Json));
            }
            Console.WriteLine(entry.FullName + ": " + matched + " exact action, mode, forecast and query matches");
        }
    }

    internal static FreeRacingInput Observation(StartSample144 sample, double time) => sample.Input with {
        Time = time, NextReview = time == 0 ? 0 : sample.Input.NextReview,
        Pending = sample.Input.Pending.Where(q => q.ObservedAt < time - 1e-8).ToArray(),
        Rider = sample.Input.Rider with { Drive = sample.Input.Active.Drive },
    };

    internal static T Read<T>(ZipArchive zip, string name) { using var stream = zip.GetEntry(name)!.Open(); return JsonSerializer.Deserialize<T>(stream, Json)!; }
    internal static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)!;
    internal static void Save(string path, object value) { using var stream = new FileStream(path, FileMode.CreateNew); JsonSerializer.Serialize(stream, value, Json); }
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private void Geometry()
    {
        var map = new BendMap(Frame);
        var rows = new List<object>();
        for (int lap = 0; lap < 3; lap++)
        for (int corner = 0; corner < 2; corner++)
        {
            var visit = map.At(lap, corner);
            var sections = map.Sections(visit);
            rows.Add(new { visit, Sections = sections, Samples = sections.SkipLast(1).SelectMany((lo, i) =>
                new[] { lo + 1e-4, (lo + sections[i + 1]) / 2, sections[i + 1] - 1e-4 }).Select(s => {
                    var road = Frame.RoadAt(s, 0);
                    return new { Reference = s, road.RadiusMeters, Curvature = road.RadiusMeters > 0 ? 1 / road.RadiusMeters : 0,
                        road.Segment, road.Heading }; }).ToArray() });
        }
        Save(At("-geometry.json"), new { Frame.LapMeters, NativeWorlds = 0, ReferenceOffset = 0, Bends = rows });
    }
    internal void AdmitWorld()
    {
        Trace173.Guard();
        if (priorWorlds + nativeWorlds >= 6) throw new InvalidOperationException("Six native diagnostic rollouts maximum");
        nativeWorlds++;
        Save(At("-world-" + nativeWorlds + ".reserved.json"), new { World = priorWorlds + nativeWorlds, CpuSeconds = prior + Study172.Cpu - began });
    }
}
