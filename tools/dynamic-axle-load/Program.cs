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
    private static readonly Process Clock = Process.GetCurrentProcess();
    internal static double Cpu => Clock.TotalProcessorTime.TotalSeconds;
}

internal sealed record Case176(string Id, string Archive, double BranchSeconds, double BranchReference,
    BendVisit Bend, double Goal, double BranchKappa);
internal sealed record Trial176(string Id, string Stage, double Height, Case176? Case, Outcome172 Outcome,
    double? GoalSeconds, double? BaselineSeconds, double? GainSeconds, int PrefixSamples,
    int PrefixReviews, string? BranchHash, double? PreviousAx, bool FixedCommands, double? MinFront,
    double? MinRear, double MaxDecisionMilliseconds)
{
    public bool LoadAware { get; init; }
}

internal sealed class Study176(string directory)
{
    internal const string Tool = "tools/dynamic-axle-load";
    internal const string Old = "docs/experiments/three-lap-global-planning";
    internal const string BaseSha = "2a32aec30a50b8640c8dcb34a7b573d12395fecb";
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    private readonly TrackFrame frame = NativeWorld172.Frame();
    private string At(string name) => Path.Combine(directory, name);
    private static double Height(string label) => label == "T35" ? .35 : label == "T55" ? .55 : throw new ArgumentException("Height");
    private static string Tag(double height) => height == .35 ? "T35" : height == .55 ? "T55" : "BASE";

    internal static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("prepare" or "tape" or "live" or "aware" or "pilot" or "validate" or "repair"))
        {
            Console.Error.WriteLine("Study #176 OFF. Choose an explicit research stage and evidence directory.");
            return 2;
        }
        Directory.CreateDirectory(args[1]);
        using var gate = new FileStream(Path.Combine(args[1], ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        new Study176(args[1]).Stage(args[0]);
        return 0;
    }

    private void Stage(string stage)
    {
        if (File.Exists(At("closed.json"))) throw new InvalidOperationException("Study closed.");
        if (Directory.GetFiles(directory, "*.started.json").Length != Directory.GetFiles(directory, "*.done.json").Length)
            throw new InvalidOperationException("Unclosed reservation.");
        double started = Study172.Cpu;
        var sources = Directory.GetFiles(Tool, "*.cs").Concat(Directory.GetFiles(Tool, "*.py"))
            .Order().ToDictionary(p => p.Replace('\\', '/'), Hash);
        Save(At(stage + ".started.json"), new { Stage = stage, Sources = sources, PreviousWorlds = Worlds() });
        using (var z = ZipFile.Open(At(stage + "-source.zip"), ZipArchiveMode.Create))
            foreach (var source in sources.Keys) z.CreateEntryFromFile(source, Path.GetFileName(source));
        string? failure = null;
        try
        {
            if (stage == "prepare") Prepare();
            else
            {
                var protocol = Read<Case176[]>(At("cases.json"));
                string expectedHeld = Read<JsonElement>(At("protocol.json")).GetProperty("HeldSha256").GetString()!;
                if (Hash(At("held.json")) != expectedHeld) throw new InvalidOperationException("Held states changed.");
                switch (stage)
                {
                    case "repair":
                        var invalid = Read<JsonElement>(At("invalid-integration.json"));
                        if (Worlds() != 12 || invalid.GetProperty("InvalidWorlds").GetInt32() != 12)
                            throw new InvalidOperationException("Two reserve worlds require the sealed integration-failure audit.");
                        foreach (double h in new[] { .35, .55 })
                            ExecuteCase(stage, protocol.Single(c => c.Id == "X"), h, true);
                        break;
                    case "tape":
                        foreach (var c in protocol)
                        foreach (var v in new[] { "T35", "T55" }) ExecuteCase(stage, c, Height(v), true);
                        break;
                    case "live":
                        var tape = Results("tape");
                        if (tape.Length != 6) throw new InvalidOperationException("Complete all fixed-command comparisons first.");
                        var ranked = Rank(tape, false);
                        foreach (var c in protocol) ExecuteCase(stage, c, ranked[0], false);
                        break;
                    case "aware":
                        var original = Results("live");
                        if (original.Length != 3 || Rank(original, true).Length > 0)
                            throw new InvalidOperationException("Adaptation requires a failed original-K4 full-bend qualification.");
                        double selected = original.Select(q => q.Height).Distinct().Single();
                        Save(At("adaptation.json"), new
                        {
                            Height = selected, Cases = protocol.Select(x => x.Id),
                            Change = "Use the #169 delayed-load model in the four existing K4 forecast call sites. Same targets, weights, control limits and pending.",
                            Gate = "All three complete legal goals, positive median gain and intact Fz, then a legal S3 pilot."
                        });
                        foreach (var c in protocol) ExecuteCase(stage, c, selected, false, true);
                        break;
                    case "pilot":
                        var local = Results("live");
                        if (local.Length != 3) throw new InvalidOperationException("Require three live K4 trials.");
                        var qualifying = Rank(local, true);
                        if (qualifying.Length == 0)
                        {
                            local = Results("aware");
                            if (local.Length != 3) throw new InvalidOperationException("Require three load-aware local trials.");
                            qualifying = Rank(local, true);
                        }
                        if (qualifying.Length == 0)
                            throw new InvalidOperationException("No live variant passed all three full bends with positive median gain.");
                        var (baseRun, _, _) = Archived("v1-K4-S1");
                        var r = baseRun.Riders.Single();
                        Execute("pilot", ScenarioFor("S1", r.Spec, r.Gate), r.Spec, r.Gate, qualifying[0], null,
                            null, null, 3 * frame.LapMeters, true, false, baseRun.Riders[0].FinishSeconds, local[0].LoadAware);
                        break;
                    case "validate":
                        var pilot = Results("pilot").Single();
                        if (!pilot.Outcome.S3 || !pilot.Outcome.Reserve || pilot.Outcome.FinishTime > pilot.BaselineSeconds * 1.01)
                            throw new InvalidOperationException("Pilot did not establish a legal autonomous S3 within the 1% regression bound.");
                        Save(At("freeze.json"), new { Variant = Tag(pilot.Height), pilot.Height,
                            Controller = pilot.LoadAware ? "K4 with load-aware forecasts" : "original causal K4",
                            HeldSha256 = expectedHeld, Sources = sources, Selection = "all 3 live full bends legal and positive median gain, then legal S3 pilot" });
                        foreach (var scenario in Read<Scenario[]>(At("held.json")))
                        {
                            var baseline = Execute(stage, scenario, scenario.Spec, scenario.Field, 0, null, null, null,
                                3 * frame.LapMeters, true, false, null);
                            Execute(stage, scenario, scenario.Spec, scenario.Field, pilot.Height, null, null, null,
                                3 * frame.LapMeters, true, false, baseline.Outcome.FinishTime, pilot.LoadAware);
                        }
                        break;
                }
            }
        }
        catch (Exception e)
        {
            failure = e.GetType().Name + ": " + e.Message;
            throw;
        }
        finally
        {
            Save(At(stage + ".done.json"), new { Stage = stage, CpuSeconds = Study172.Cpu - started,
                TotalWorlds = Worlds(), Failure = failure });
        }
    }

    private void Prepare()
    {
        var map = new BendMap(frame);
        var cases = new List<Case176>();
        foreach (var (id, name, lap, bend) in new[]
        {
            ("E1", "v1-K4-S1", 1, 0), ("E2", "v1-K4-S4", 0, 1), ("X", "v1-K4-S2", 1, 0)
        })
        {
            var (run, decisions, result) = Archived(name);
            if (!result.S3 || !result.Reserve) throw new InvalidDataException("BASE archive lacks S3 and reserve.");
            var samples = run.Riders.Single().Samples;
            var location = map.At(lap, bend);
            double Kappa(CyclicDecision172 d) =>
                samples.MinBy(s => Math.Abs(s.Input.Time - d.Time))!.Powertrain!.Wheel.InstantSlipRatio;
            var branch = id == "X"
                ? decisions.Where(d => d.Reference >= location.Exit - 5 && d.Reference <= location.Exit + 12).MaxBy(Kappa)!
                : decisions.Last(d => d.Reference <= location.Entry - 2);
            if (id != "X" && branch.Reference < location.Entry - 9)
                throw new InvalidDataException("Entry branch too far from bend.");
            var target = id == "X" ? map.Locate(location.Exit + 1).Next : location;
            cases.Add(new(id, name, branch.Time, branch.Reference, target, target.Exit + 20, Kappa(branch)));
        }
        Save(At("cases.json"), cases);
        Save(At("held.json"), new[]
        {
            new Scenario("H1", "A", -.5863, 1.5261f), new Scenario("H2", "B", -.5351, 4.4213f),
            new Scenario("H3", "C", -.6187, 7.1429f), new Scenario("H4", "D", -.4783, 9.6137f),
            new Scenario("H5", "A", -.4491, 1.6823f)
        });
        Save(At("protocol.json"), new
        {
            Base = BaseSha, Reference = "c5f0a4cc4de34fc4c8c47ef912cafabf34055f29",
            Cases = cases, HeldSha256 = Hash(At("held.json")), MaximumWorlds = 24, TapeWorlds = 6, LiveWorlds = 3,
            PilotWorlds = 1, ValidationWorlds = 10, HeightsMeters = new[] { .35, .55 },
            Goal = "next complete bend and 20m", Review = .2, Reaction = .24, Pose = .5, Swept = .5, BetaDegrees = 45,
            Selection = "Rank tape by legal count, median gain among legal paired goals, then lower h. Live K4 on selected height. Require 3/3 legal and positive median gain, then a legal pilot no more than 1% slower before five fresh pairs.",
            Controller = "K4 frozen; tape strictly diagnostic. Autonomous variants query original K4 on actual observed states.",
            Limit = "No decision wall-time or CPU gate. Invalid raw Fz terminates before credit."
        });
        foreach (var c in cases)
            Console.WriteLine(FormattableString.Invariant($"{c.Id} {c.Archive}: t={c.BranchSeconds:F2} s={c.BranchReference:F3} kappa={c.BranchKappa:F3} goal={c.Goal:F3}"));
    }

    private void ExecuteCase(string stage, Case176 c, double height, bool tape, bool loadAware = false)
    {
        var (run, decisions, _) = Archived(c.Archive);
        var r = run.Riders.Single();
        Execute(stage, ScenarioFor(c.Id, r.Spec, r.Gate), r.Spec, r.Gate, height, c, run, decisions,
            c.Goal, false, tape, Crossing(r.Samples, c.Goal), loadAware);
    }

    private Trial176 Execute(string stage, Scenario scenario, RiderSpec spec, StartingGate gate, double height,
        Case176? c, Run144? archive, CyclicDecision172[]? archivedDecisions, double goal, bool reserve,
        bool tape, double? baseline, bool loadAware = false)
    {
        string id = stage + "-" + scenario.Id + "-" + Tag(height);
        int cap = stage switch { "tape" => 6, "live" => 3, "aware" => 3, "pilot" => 1, "validate" => 10, "repair" => 2, _ => 0 };
        if (Worlds() >= 24 || Directory.GetFiles(directory, stage + "-*.reserved.json").Length >= cap)
            throw new InvalidOperationException("Native world budget exhausted.");
        Save(At(id + ".reserved.json"), new { Id = id, Stage = stage, Height = height, Goal = goal, World = Worlds() + 1 });
        var begin = Study172.Cpu;
        var choices = new List<CyclicDecision172>();
        CyclicController172? k4 = tape ? null : Controller(spec);
        var old = archive?.Riders.Single().Samples;
        int reviews = 0;
        string? branchHash = null;
        double? previousAx = null;
        using var world = new NativeWorld172(new(Enabled: true, RequiredLaps: 3, MaximumSeconds: 95,
            SoloGate: gate), diagnosticFrame: frame, diagnosticRider: spec, safetyHalfMeter: true,
            stopReference: goal, postSeconds: reserve ? 1 : 0, heightMeters: height, transferFrom: c?.BranchSeconds ?? 0);
        Decision144 Choose(FreeRacingInput input, CrankClutch144 crank, RiderSpec rider, TrackFrame track,
            TrackStateSnapshot surface, WorldParameters physics, float mass, float inertia, string variant)
        {
            var started = Stopwatch.GetTimestamp();
            double cpu = Study172.Cpu;
            CyclicDecision172? frozen = null;
            if (archivedDecisions is not null && (tape || input.Time <= c!.BranchSeconds + 1e-8))
            {
                frozen = archivedDecisions[choices.Count];
                if (Math.Abs(input.Time - frozen.Time) > 1e-8)
                    throw new InvalidOperationException("Command clock differs from the archived 0.20 s clock.");
            }
            if (c is not null && input.Time <= c.BranchSeconds + 1e-8)
            {
                var saved = old!.MinBy(q => Math.Abs(q.Input.Time - input.Time))!;
                var reference = Observe(saved, input.Time);
                if (Serialize(new { Input = input, CrankOmega = crank.Omega }) !=
                    Serialize(new { Input = reference, saved.CrankOmega }))
                    throw new InvalidOperationException("Mismatch in full prefix observation or pending.");
                reviews++;
                if (Math.Abs(input.Time - c.BranchSeconds) < 1e-8)
                {
                    previousAx = world.AcceptedAx;
                    branchHash = HashText(Serialize(new { Input = input, CrankOmega = crank.Omega, Spec = rider, PreviousAx = previousAx }));
                }
            }
            bool adapted = loadAware && input.Time >= (c?.BranchSeconds ?? 0) - 1e-8;
            var decision = tape ? frozen! with { Reference = input.Rider.TotalReferenceMeters,
                DecisionMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds, CpuSeconds = Study172.Cpu - cpu,
                Forecasts = 0, Queries = 0, Transition = "matched archived time-based command" } :
                adapted ? ForecastDispatch176.Choose(k4!,input,crank,height,world.AcceptedAx) : k4!.Choose(input, crank);
            if (frozen is not null && !adapted && decision.Action != frozen.Action)
                throw new InvalidOperationException("K4 prefix action changed.");
            choices.Add(decision);
            return new(new(input.Time, decision.Action, FreeActionSafety.Uncertain, false, [],
                decision.Queries, decision.Forecasts), decision.CpuSeconds);
        }
        var run = world.Run(id, "K1", Choose);
        var samples = run.Riders.Single().Samples;
        var plans = k4?.Plans.ToArray() ?? [];
        var outcome = Inspect172.Analyze(run, stage, Tag(height), scenario, frame, goal, choices.ToArray(), plans, Study172.Cpu - begin);
        int prefix = 0;
        if (c is not null)
        {
            var part = samples.TakeWhile(q => q.Input.Time < c.BranchSeconds - 1e-8).ToArray();
            var expected = old!.TakeWhile(q => q.Input.Time < c.BranchSeconds - 1e-8).ToArray();
            prefix = part.Length;
            if (!part.Select(Serialize).SequenceEqual(expected.Select(Serialize)) || branchHash is null)
            {
                // Keep the offending run before rejecting the comparison.
                outcome = outcome with { ReachedStop = false, S3 = false, Reserve = false, Reason = "PREFIX_MISMATCH" };
            }
        }
        var t = outcome.ReachedStop ? Crossing(samples, goal) : null;
        var loads = samples.Where(q => q.Input.Time > (c?.BranchSeconds ?? 0) + 1e-8 && q.Forces?.TyreLoads is not null)
            .Select(q => q.Forces!.Value.TyreLoads!).ToArray();
        var result = new Trial176(id, stage, height, c, outcome, t, baseline,
            t is not null && baseline is not null ? baseline.Value - t.Value : null, prefix, reviews, branchHash,
            previousAx, tape, loads.Length == 0 ? null : loads.Min(l => l.FrontNormalNewtons),
            loads.Length == 0 ? null : loads.Min(l => l.RearNormalNewtons), choices.Select(d => d.DecisionMilliseconds).DefaultIfEmpty().Max())
        { LoadAware = loadAware };
        using (var z = ZipFile.Open(At(id + ".zip"), ZipArchiveMode.Create))
        {
            Entry(z, "native.json", run);
            Entry(z, "result.json", outcome);
            Entry(z, "trial.json", result);
            Entry(z, "decisions.json", choices);
            Entry(z, "plans.json", plans);
        }
        Save(At(id + "-result.json"), result);
        Console.WriteLine(FormattableString.Invariant(
            $"{id}: goal={outcome.ReachedStop} S3={outcome.S3} reserve={outcome.Reserve} gain={result.GainSeconds:F6} end={outcome.EndReference:F3} minF={result.MinFront:F2} swept={outcome.MinSweep:F3} beta={outcome.MaxBetaDegrees:F2} {outcome.Reason}"));
        return result;
    }

    private CyclicController172 Controller(RiderSpec spec)
    {
        const string teacher = "docs/experiments/autonomous-fast-corner/evidence/";
        var fit = Read<JsonElement>(teacher + "fit.json");
        var frozen = Read<JsonElement>(teacher + "freeze.json");
        if (Hash(teacher + "fit.json") != frozen.GetProperty("FitSha256").GetString())
            throw new InvalidOperationException("Frozen teacher mismatch.");
        var a = fit.GetProperty("A").Deserialize<Teacher>(Json)!;
        var b = fit.GetProperty("B").Deserialize<Teacher>(Json)!;
        var weights = frozen.GetProperty("Selected").GetProperty("Weights").Deserialize<Weights>(Json)!;
        using var z = ZipFile.OpenRead("docs/experiments/two-corner-full-drive/170-dowody.zip");
        var tuning = Read<JsonElement>(z, "freeze.json").GetProperty("Tuning").Deserialize<Integration>(Json)!;
        var profile = Read<Nominal[][]>("docs/experiments/whole-corner-planning/profile.json");
        return new(a, b, weights, tuning, profile, "K4", spec, frame, MultiActionExperiment135.Surface(frame),
            FreeLearningExperiment.Parameters(), 147, 52.72097f);
    }

    private int Worlds() => Directory.GetFiles(directory, "*.reserved.json").Length;
    private Trial176[] Results(string stage) => Directory.GetFiles(directory, stage + "-*-result.json")
        .Select(Read<Trial176>).ToArray();
    private static double[] Rank(Trial176[] trials, bool qualifying) => trials.GroupBy(x => x.Height)
        .Select(g => new { Height = g.Key, Count = g.Count(x => x.GainSeconds is not null),
            Median = Median(g.Where(x => x.GainSeconds is not null).Select(x => x.GainSeconds!.Value)) })
        .Where(g => !qualifying || g.Count == 3 && g.Median > 0)
        .OrderByDescending(g => g.Count).ThenByDescending(g => g.Median).ThenBy(g => g.Height).Select(g => g.Height).ToArray();
    private static double Median(IEnumerable<double> items)
    {
        var a = items.Order().ToArray();
        return a.Length == 0 ? double.NegativeInfinity : (a[(a.Length-1)/2] + a[a.Length/2]) / 2;
    }
    private static Scenario ScenarioFor(string id, RiderSpec s, StartingGate gate)
        => new(id, gate.ToString(), s.ReferenceMeters, s.OffsetMeters);
    private static FreeRacingInput Observe(StartSample144 sample, double time) => sample.Input with
    {
        Time = time, NextReview = time == 0 ? 0 : sample.Input.NextReview,
        Pending = sample.Input.Pending.Where(q => q.ObservedAt < time - 1e-8).ToArray(),
        Rider = sample.Input.Rider with { Drive = sample.Input.Active.Drive }
    };
    private static double? Crossing(StartSample144[] s, double goal)
    {
        int i = Array.FindIndex(s, q => q.Input.Rider.TotalReferenceMeters >= goal);
        if (i < 1) return null;
        var a = s[i-1].Input;
        var b = s[i].Input;
        double fraction = (goal-a.Rider.TotalReferenceMeters)/(b.Rider.TotalReferenceMeters-a.Rider.TotalReferenceMeters);
        return a.Time + fraction * (b.Time-a.Time);
    }
    private static (Run144, CyclicDecision172[], Outcome172) Archived(string name)
    {
        using var outer = ZipFile.OpenRead(Old + "/172-validation-K4.zip");
        using var stream = outer.GetEntry(name + ".zip")!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        using var z = new ZipArchive(buffer);
        return (Read<Run144>(z, "native.json"), Read<CyclicDecision172[]>(z, "decisions.json"), Read<Outcome172>(z, "result.json"));
    }
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string HashText(string text) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)!;
    private static T Read<T>(ZipArchive z, string name)
    {
        using var stream = z.GetEntry(name)!.Open();
        return JsonSerializer.Deserialize<T>(stream, Json)!;
    }
    private static void Save(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(stream, value, Json);
    }
    private static void Entry(ZipArchive z, string name, object value)
    {
        using var stream = z.CreateEntry(name, CompressionLevel.Optimal).Open();
        JsonSerializer.Serialize(stream, value, Json);
    }
}
