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

internal static class Study172
{
    private static readonly Process Process = Process.GetCurrentProcess();
    internal static double Cpu => Process.TotalProcessorTime.TotalSeconds;
}

internal sealed record Case177(string Id, BendVisit Bend, CyclicDecision172 Review, StartSample144 Sample);
internal sealed record Reservation177(string Id, string Stage, double Goal, string? Line, string? State);
internal sealed record Trial177(Outcome172 Outcome, string Stage, string Arm, string? Case, double? BranchTime,
    int PrefixSamples, int PrefixReviews, double? BaseGoal, double? NewGoal, bool Terminal, bool LocalPass,
    double DecisionMaxMs, int AppliedJoint, string[] AdmittedLines);

internal sealed class Study177
{
    private const string Docs172 = "docs/experiments/three-lap-global-planning";
    private const string Tool = "tools/trajectory-control";
    private const string Held = "docs/experiments/trajectory-control/held177.json";
    private const float Mass = 147, Inertia = 52.72097f;
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    private readonly TrackFrame frame = NativeWorld172.Frame();
    private readonly TrackStateSnapshot surface;
    private readonly WorldParameters parameters = FreeLearningExperiment.Parameters();
    private readonly BendMap map;
    private readonly string dir;
    private readonly Teacher a, b;
    private readonly Weights weights;
    private readonly Integration tuning;
    private readonly Nominal[][] profiles;
    private readonly Run144 development;
    private readonly CyclicDecision172[] historical;
    private readonly Scenario start;
    private readonly Case177[] cases;

    public static int Main(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("prepare" or "preflight" or "probe" or "develop" or "validate"))
        {
            Console.Error.WriteLine("Study #177 OFF. Explicit prepare/preflight/probe/develop/validate and evidence directory required.");
            return 2;
        }
        Directory.CreateDirectory(args[1]);
        using var mutex = new FileStream(Path.Combine(args[1], ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var study = new Study177(args[1]);
        double began = Study172.Cpu;
        switch (args)
        {
            case ["prepare", _]: study.Prepare(); break;
            case ["preflight", _]: study.Preflight(); break;
            case ["probe", _, var label] when label.All(char.IsAsciiLetterOrDigit): study.Probe(label); break;
            case ["develop", _, var id, var line]: study.Develop(id, line); break;
            case ["validate", _, var choice]: study.Validate(choice); break;
            default: throw new ArgumentException("Invalid explicit research command.");
        }
        Console.WriteLine($"Stage CPU: {Study172.Cpu - began:F3} s");
        return 0;
    }

    private Study177(string directory)
    {
        dir = directory;
        map = new(frame);
        surface = MultiActionExperiment135.Surface(frame);
        if (parameters.EnableDynamicAxleLoad) throw new InvalidOperationException("BASE physics only.");
        var history = Read<JsonElement>(Docs172 + "/summary.json").GetProperty("freeze");
        foreach (string group in new[] { "Sources", "PhysicsSha256" })
        foreach (var source in history.GetProperty(group).EnumerateObject())
            if (Hash(source.Name) != source.Value.GetString())
                throw new InvalidOperationException("Frozen #172 source mismatch: " + source.Name);
        profiles = Read<Nominal[][]>("docs/experiments/whole-corner-planning/profile.json");
        const string teacher = "docs/experiments/autonomous-fast-corner/evidence/";
        var fit = Read<JsonElement>(teacher + "fit.json");
        var seal = Read<JsonElement>(teacher + "freeze.json");
        if (Hash(teacher + "fit.json") != seal.GetProperty("FitSha256").GetString())
            throw new InvalidOperationException("Frozen teacher mismatch.");
        a = fit.GetProperty("A").Deserialize<Teacher>(Json)!;
        b = fit.GetProperty("B").Deserialize<Teacher>(Json)!;
        weights = seal.GetProperty("Selected").GetProperty("Weights").Deserialize<Weights>(Json)!;
        using var archive = ZipFile.OpenRead("docs/experiments/two-corner-full-drive/170-dowody.zip");
        tuning = Read<JsonElement>(archive, "freeze.json").GetProperty("Tuning").Deserialize<Integration>(Json)!;
        using var dev = ZipFile.OpenRead(Docs172 + "/172-development.zip");
        using var memory = new MemoryStream();
        using (var source = dev.GetEntry("c3b-K4-D1.zip")!.Open()) source.CopyTo(memory);
        memory.Position = 0;
        using var run = new ZipArchive(memory);
        development = Read<Run144>(run, "native.json");
        historical = Read<CyclicDecision172[]>(run, "decisions.json");
        start = Read<Outcome172>(run, "result.json").Start;
        var bend1 = map.At(0, 0);
        var bend2 = map.At(0, 1);
        var next = map.At(1, 0);
        cases =
        [
            Case("P", bend2, bend2.Entry - 32), Case("E", next, next.Entry - 4.8),
            Case("X1", bend2, bend1.Exit + 2), Case("X2", next, bend2.Exit + 2)
        ];

        Case177 Case(string id, BendVisit bend, double minimum)
        {
            var review = historical.First(q => q.Reference >= minimum);
            return new(id, bend, review, development.Riders.Single().Samples.MinBy(q => Math.Abs(q.Input.Time - review.Time))!);
        }
    }

    private string At(string name) => Path.Combine(dir, name);
    private CyclicController172 K4(RiderSpec spec) => new(a, b, weights, tuning, profiles, "K4", spec, frame, surface, parameters, Mass, Inertia);
    private Controller177 New(CyclicController172 k4, RiderSpec spec, string[] lines) =>
        new(k4, frame, surface, parameters, spec, Mass, Inertia, profiles, lines);

    private void Prepare()
    {
        var geometry = new { frame.LapMeters, Starts = Enumerable.Range(0, frame.Track.Segments.Count)
            .Select(frame.SegmentStartMeters).Append(frame.LapMeters).ToArray(), Geometry = ReplayGeometry.Create(frame) };
        Save(At("geometry.json"), geometry);
        Save(At("protocol.json"), new
        {
            Base172 = "c5f0a4cc4de34fc4c8c47ef912cafabf34055f29", Base176 = "f55fb06ceb7685306dd07c4d8dcbe3e55fbe6896",
            HeldSha256 = Hash(Held), Family = LinePlanner177.Family, PreflightStop = map.At(0, 0).Exit + 20,
            ReactionSeconds = .24, ReviewSeconds = .20, Swept = .5, BetaDegrees = 45, ReserveSeconds = 1,
            Cases = cases, DevelopmentArchive = "c3b-K4-D1", DevelopmentArchiveHash = Hash(Docs172 + "/172-development.zip"),
            MaximumWorlds = 24, MaximumDevelopment = 8, MaximumValidation = 10, MaximumReserve = 6
        });
    }

    private void Preflight()
    {
        var protocol = Read<JsonElement>(At("protocol.json"));
        Run("base-control", "preflight", "BASE", start, protocol.GetProperty("PreflightStop").GetDouble(), null, [], false, true);
    }

    private void Probe(string label)
    {
        NeedPreflight();
        double began = Study172.Cpu;
        var rider = development.Riders.Single();
        var k4 = K4(rider.Spec);
        var controller = New(k4, rider.Spec, []);
        var observations = new List<object>();
        foreach (var old in historical)
        {
            var s = rider.Samples.MinBy(q => Math.Abs(q.Input.Time - old.Time))!;
            var observation = Observe(s, old.Time);
            var crank = new CrankClutch144(omega: s.CrankOmega);
            var replay = k4.Choose(observation, crank);
            if (replay.Action != old.Action) throw new InvalidOperationException("Archived K4 observation mismatch.");
            var match = cases.SingleOrDefault(c => c.Review.Time == old.Time);
            if (match is not null)
            {
                var screen = controller.Evaluate(observation, crank, k4, replay, match.Bend, true);
                observations.Add(new { match.Id, Screen = screen });
                Console.WriteLine($"{match.Id}: {screen.Chosen ?? "no admission"} | " +
                    string.Join(", ", screen.Paths.Select(p => $"{p.Line}={p.Seconds:F3} [{p.Sweep:F2}]")));
            }
            if (old.Time == cases.Max(x => x.Review.Time)) break;
        }
        Save(At("probe-" + label + ".json"), new { CpuSeconds = Study172.Cpu - began, Cases = observations });
    }

    private void Develop(string id, string line)
    {
        NeedPreflight();
        if (File.Exists(At("freeze.json"))) throw new InvalidOperationException("Study frozen.");
        if (!LinePlanner177.Family.Skip(1).Any(x => x.Id == line))
            throw new ArgumentException("Unknown line.");
        var selected = cases.Single(x => x.Id == id);
        Run("dev-" + id + "-" + line, "development", "NEW", start, selected.Bend.Exit + 20, selected, [line], false, false);
    }

    private void Validate(string line)
    {
        NeedPreflight();
        var audited = Read<JsonElement[]>(At("development-audit.json"));
        var trials = Directory.GetFiles(dir, "dev-*.result.json").Select(Read<Trial177>).ToArray();
        bool success = trials.Any(q => q.LocalPass && q.AdmittedLines.Contains(line) &&
            audited.Any(a => a.GetProperty("id").GetString() == q.Outcome.Id && a.GetProperty("local").GetBoolean() &&
                string.Equals(a.GetProperty("sha256").GetString(), Hash(At(q.Outcome.Id + ".zip")), StringComparison.OrdinalIgnoreCase)));
        if (!success) throw new InvalidOperationException("No legal faster development continuation for this line.");
        if (trials.Any(q => q.Outcome.Reason.StartsWith("technical invariant", StringComparison.Ordinal)))
            throw new InvalidOperationException("Unresolved native integration error.");
        var protocol = Read<JsonElement>(At("protocol.json"));
        string heldHash = Hash(Held);
        if (protocol.GetProperty("HeldSha256").GetString() != heldHash) throw new InvalidOperationException("Holdout mismatch.");
        var sources = Directory.GetFiles(Tool, "*.cs").Concat(Directory.GetFiles(Tool, "*.csproj"))
            .Concat(Directory.GetFiles("tools/state-feasible-throttle", "Fork174.*.cs"))
            .Concat(new[] { Tool + "/packages.lock.json", Held }).Order().ToDictionary(p => p.Replace('\\', '/'), Hash);
        // Freeze the controller and holdout hash before parsing the held scenarios.
        Save(At("freeze.json"), new { Line = line, Sources = sources, HeldSha256 = heldHash });
        var held = Read<Scenario[]>(Held);
        if (held.Length != 5 || held.Select(q => (q.Start, q.Offset)).Distinct().Count() != 5)
            throw new InvalidOperationException("Five independent conditions required.");
        foreach (var scenario in held)
        {
            double goal = 3 * frame.LapMeters;
            Run("held-" + scenario.Id + "-BASE", "validation", "BASE", scenario, goal, null, [], true, false);
            Run("held-" + scenario.Id + "-NEW", "validation", "NEW", scenario, goal, null, [line], true, false);
        }
    }

    private void Run(string id, string stage, string arm, Scenario scenario, double goal, Case177? branch,
        string[] lines, bool reserve, bool preflight)
    {
        var ledger = Directory.GetFiles(dir, "*.reserved.json").Select(Read<Reservation177>).ToArray();
        int cap = stage == "preflight" ? 6 : stage == "development" ? 8 : 10;
        if (ledger.Length >= 24 || ledger.Count(q => q.Stage == stage) >= cap)
            throw new InvalidOperationException("Native world budget.");
        Save(At(id + ".reserved.json"), new Reservation177(id, stage, goal, lines.SingleOrDefault(), branch?.Id));
        var spec = scenario.Spec;
        var baseline = K4(spec);
        var modern = lines.Length == 0 ? null : New(baseline, spec, lines);
        var decisions = new List<CyclicDecision172>();
        var old = development.Riders.Single().Samples;
        int prefixReviews = 0;
        Decision144 Choose(FreeRacingInput input, CrankClutch144 crank, RiderSpec rider, TrackFrame nativeFrame,
            TrackStateSnapshot nativeSurface, WorldParameters nativeParams, float mass, float inertia, string variant)
        {
            if (mass != Mass || Math.Abs(inertia - Inertia) > 1e-5 || nativeParams.EnableDynamicAxleLoad ||
                RiderController.ReactionSeconds(rider) != .24)
                throw new InvalidOperationException("BASE parameter parity.");
            bool before = branch is not null && input.Time < branch.Review.Time - 1e-8;
            bool compare = preflight || before || branch is not null && Math.Abs(input.Time - branch.Review.Time) < 1e-8;
            if (compare)
            {
                var sample = old.MinBy(q => Math.Abs(q.Input.Time - input.Time))!;
                if (!Same(input, Observe(sample, input.Time)) || crank.Omega != sample.CrankOmega)
                    throw new InvalidOperationException("Frozen observation / crank / pending prefix mismatch.");
                prefixReviews++;
            }
            var decision = modern?.Choose(input, crank, !before,
                branch is not null && Math.Abs(input.Time - branch.Review.Time) < 1e-8 ? branch.Bend : null)
                ?? baseline.Choose(input, crank);
            if ((preflight || before) && decision.Action != historical[decisions.Count].Action)
                throw new InvalidOperationException("Frozen command prefix mismatch.");
            decisions.Add(decision);
            return new(new(input.Time, decision.Action, FreeActionSafety.Uncertain, false, [], decision.Queries, decision.Forecasts), decision.CpuSeconds);
        }

        double began = Study172.Cpu;
        using var world = new NativeWorld172(new(Enabled: true, RequiredLaps: 3, MaximumSeconds: 100,
            SoloGate: scenario.Field), diagnosticFrame: frame, diagnosticRider: spec,
            safetyHalfMeter: true, stopReference: goal, postSeconds: reserve ? 1 : 0);
        var run = world.Run(id, "K1", Choose);
        var rider = run.Riders.Single();
        var prefix = preflight ? rider.Samples : branch is null ? [] :
            rider.Samples.Where(x => x.Input.Time <= branch.Review.Time + .24 + 1e-8).ToArray();
        bool equal = prefix.Select((x, i) => Same(x, old[i])).All(x => x);
        if (branch is not null)
        {
            // Request and pending records diverge at the branch review. The
            // accepted physical state remains identical through activation.
            equal = prefix.Select((x, i) => Same(Physical(x), Physical(old[i]))).All(x => x);
        }
        var plans = baseline.Plans.ToArray();
        var outcome = Inspect172.Analyze(run, stage, arm, scenario, frame, goal, decisions.ToArray(), plans, Study172.Cpu - began);
        double? baseTime = branch is null ? null : Cross(old, goal);
        double? time = Cross(rider.Samples, goal);
        var last = rider.Samples[^1].Input.Rider;
        var terminalBase = old.First(x => x.Input.Rider.TotalReferenceMeters >= Math.Min(goal, old[^1].Input.Rider.TotalReferenceMeters)).Input.Rider;
        bool terminal = branch is not null && outcome.ReachedStop &&
            Math.Abs(last.SlideRadians) < .20 && Math.Abs(last.AngularVelocity) < .5 &&
            Math.Abs(BikeAngles.Wrap(Math.Atan2(last.Velocity.Y, last.Velocity.X) - last.Road.Heading)) < .18 &&
            last.Speed >= terminalBase.Speed - .65 && Math.Abs(last.Road.OffsetMeters - terminalBase.Road.OffsetMeters) < 1.7;
        var result = new Trial177(outcome, stage, arm, branch?.Id, branch?.Review.Time,
            equal ? prefix.Length : -1, prefixReviews, baseTime, time, terminal,
            equal && terminal && baseTime is { } tb && time is { } tn && tn + .001 < tb,
            decisions.Count == 0 ? 0 : decisions.Max(q => q.DecisionMilliseconds),
            decisions.Count(q => q.Transition.EndsWith(" · joint", StringComparison.Ordinal)),
            modern?.Screens.Where(q => q.Chosen is not null).Select(q => q.Chosen!).ToArray() ?? []);
        using (var z = ZipFile.Open(At(id + ".zip"), ZipArchiveMode.Create))
        {
            Add(z, "native.json", run);
            Add(z, "trial.json", result);
            Add(z, "decisions.json", decisions);
            Add(z, "plans.json", plans);
            Add(z, "screens.json", modern?.Screens.ToArray() ?? []);
        }
        Save(At(id + ".result.json"), result);
        Console.WriteLine($"{id}: goal={outcome.ReachedStop} S3={outcome.S3} reserve={outcome.Reserve} " +
            $"base={baseTime:F6} new={time:F6} local={result.LocalPass} actions={result.AppliedJoint} " +
            $"prefix={result.PrefixSamples} swept={outcome.MinSweep:F4} cpu={outcome.CpuSeconds:F3} {outcome.Reason}");
        if (!equal || outcome.Reason.StartsWith("technical invariant", StringComparison.Ordinal))
            throw new InvalidOperationException("Integration guard failed; evidence retained.");
    }

    private void NeedPreflight()
    {
        var receipt = Read<JsonElement>(At("integration.json"));
        if (!receipt.GetProperty("verified").GetBoolean() ||
            !string.Equals(receipt.GetProperty("archive_sha256").GetString(), Hash(At("base-control.zip")), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Independent BASE integration proof required.");
    }

    private static FreeRacingInput Observe(StartSample144 sample, double time) => sample.Input with
    {
        Time = time, NextReview = time == 0 ? 0 : sample.Input.NextReview,
        Pending = sample.Input.Pending.Where(q => q.ObservedAt < time - 1e-8).ToArray(),
        Rider = sample.Input.Rider with { Drive = sample.Input.Active.Drive }
    };
    private static object Physical(StartSample144 q) => new
    {
        q.Input.Time, q.Input.Rider, q.Input.Omega, q.Input.Steering, q.Input.Active, q.CrankOmega, q.Forces, q.Powertrain
    };
    private static double? Cross(StartSample144[] rows, double goal)
    {
        int i = Array.FindIndex(rows, x => x.Input.Rider.TotalReferenceMeters >= goal);
        if (i < 1) return null;
        var a = rows[i - 1].Input;
        var b = rows[i].Input;
        return a.Time + (b.Time - a.Time) * (goal - a.Rider.TotalReferenceMeters) /
            (b.Rider.TotalReferenceMeters - a.Rider.TotalReferenceMeters);
    }
    private static bool Same<T>(T a, T b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)!;
    private static T Read<T>(ZipArchive archive, string name)
    {
        using var s = archive.GetEntry(name)!.Open();
        return JsonSerializer.Deserialize<T>(s, Json)!;
    }
    private static void Add(ZipArchive archive, string name, object value)
    {
        using var s = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        JsonSerializer.Serialize(s, value, Json);
    }
    private static void Save(string path, object value)
    {
        using var s = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(s, value, Json);
    }
}
