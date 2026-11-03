using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using LineThrottle156;
using S3Performance162;
using SharedWorldSpeedway;

namespace LearnedCorner166;

internal sealed record Fit(Teacher A, Teacher B);
internal sealed record Candidate(int Seed, int Index, Weights Weights, Outcome[] Outcomes)
{
    public int Legal => Outcomes.Count(x => x.Legal);
    public int Terminal => Outcomes.Count(x => x.TerminalScreen);
    public double Mean => Outcomes.Where(x => x.Legal).Select(x => x.SectionSeconds!.Value)
        .DefaultIfEmpty(100).Average();
    public double Progress => Outcomes.Average(x => x.EndReference);
    public double Margin => Outcomes.Min(x => x.MinSweep ?? 0);
}

internal static class Program
{
    private const string Docs = "docs/experiments/autonomous-fast-corner";
    private const double Admission = 880;
    private const double TrainingAdmission = 720;
    private static readonly int[] Seeds = [166042, 166097];
    private static readonly Process Clock = Process.GetCurrentProcess();
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    private static readonly JsonSerializerOptions Pretty = new(Json) { WriteIndented = true };
    private static readonly Scenario[] Training = [
        new("T1", "A", 0, 2.2f), new("T2", "B", 0, 3.5f),
        new("T3", "C", -.75, 5.3f), new("T4", "D", 0, 7.8f)
    ];
    private static double Cpu => Clock.TotalProcessorTime.TotalSeconds;
    private static string Output = "";
    private static string At(string name) => Path.Combine(Output, name);

    public static void Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("--prepare" or "--diagnose" or "--train" or "--extend" or "--validate" or "--pair"))
            throw new ArgumentException("Study166 OFF. Use an explicit research stage and evidence directory.");
        Output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(Output);
        var frame = NativeWorld166.Frame();
        switch (args[0])
        {
            case "--prepare": Prepare(frame); return;
            case "--diagnose": Diagnose(frame, Load<Fit>("fit.json")); return;
            case "--train": Train(frame, Load<Fit>("fit.json"), false); return;
            case "--extend": Train(frame, Load<Fit>("fit.json"), true); return;
            case "--validate": Validate(frame, Load<Fit>("fit.json")); return;
            case "--pair": Pair(frame, Load<Fit>("fit.json")); return;
        }
    }

    private static T Load<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(At(name)), Json)!;

    private static void Save(string name, object value)
    {
        using var stream = new FileStream(At(name), FileMode.CreateNew);
        JsonSerializer.Serialize(stream, value, Pretty);
    }

    private static void Append(string name, object value) =>
        File.AppendAllText(At(name), JsonSerializer.Serialize(value, Json) + "\n");

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static double Charged(string name) => File.Exists(At(name + ".json"))
        ? Load<JsonElement>(name + ".json").GetProperty("CpuSeconds").GetDouble()
        : File.Exists(At(name + ".started.json")) ? throw new InvalidOperationException("Unclosed reservation: " + name) : 0;

    private static void Guard(double prior, double entered, double limit)
    {
        if (prior + Cpu - entered >= limit)
            throw new InvalidOperationException("study166 CPU stop");
    }

    private static void Prepare(TrackFrame frame)
    {
        var before = Cpu;
        var exit = frame.SegmentStartMeters(4);
        var fit = new Fit(Teacher.Fit("C-B101", exit), Teacher.Fit("C-B245", exit));
        Save("fit.json", fit);
        var rows = new List<object>();
        foreach (string label in new[] { "C-B101", "C-B245" })
        {
            var run = Teacher.ReadDemonstration(label);
            var rider = run.Riders.Single();
            var surface = MultiActionExperiment135.Surface(frame);
            var policy = new Policy(fit.A, fit.B, Initial(label == "C-B101" ? 0 : 1), rider.Spec,
                frame, surface, FreeLearningExperiment.Parameters(), 147, 52.72097f, pilot: true);
            foreach (var target in new[] { 26d, 48d, 80d, 118d, 165d, 193d, 202d })
            {
                var sample = rider.Samples.First(s => Runner.Ref(s) >= target);
                var index = Array.IndexOf(rider.Samples, sample);
                var input = sample.Input;
                var previous = rider.Samples[index - 1];
                var dt = input.Time - previous.Input.Time;
                var bike = input.Rider;
                var section = target < 170 ? "positive observed prefix" : "terminal risk, excluded from fit";
                var local = policy.Choose(input, new(omega: sample.CrankOmega));
                // Counterfactual observations only. No body is moved and no world is run.
                var p = bike.Position + bike.Road.Outward * .35f;
                var shifted = bike with { Position = p, Road = frame.Project(p) };
                var offsetAction = policy.Choose(input with { Rider = shifted }, new(omega: sample.CrankOmega));
                var yawAction = policy.Choose(input with { Rider = bike with { AngularVelocity = bike.AngularVelocity + .08f } },
                    new(omega: sample.CrankOmega));
                rows.Add(new { label, target, section, Sample = sample, local, offsetAction, yawAction,
                    BetaDotRadiansPerSecond = (bike.SlideRadians - previous.Input.Rider.SlideRadians) / dt,
                    CourseRadians = Math.Atan2(bike.Velocity.Y, bike.Velocity.X),
                    OutwardMetersPerSecond = System.Numerics.Vector2.Dot(bike.Velocity, bike.Road.Outward),
                    Preview = new[] { 4, 8, 16, 32 }.Select(dx => frame.RoadAt(bike.TotalReferenceMeters + dx, bike.Road.OffsetMeters)) });
            }
        }
        var changed = rows.Count > 0;
        Save("preparation.json", new { CpuSeconds = Cpu - before, Worlds = 0, Examples = rows, Recorded = changed,
            Sources = new[] { "C-B101", "C-B245" }.ToDictionary(x => x, x =>
                Hash($"docs/experiments/fast-corner-feasibility/evidence/native/{x}.zip")),
            EstimatedNativeEpisodeCpuSeconds = new[] { .25, 3.5 },
            Scope = "Archived observations and proposal probes. The shifted observations are not physical initialisations." });
    }

    private static Weights Initial(int family) =>
        new(family == 0 ? .1 : .76, .42, .9, 1, 1, 1, .98, .28, 1.7, 17, .91, .15, 30);

    private static Weights InitialTraining(int family) => Initial(family) with { Feedback = family == 0 ? .47 : .57 };

    private static void Diagnose(TrackFrame frame, Fit fit)
    {
        var before = Cpu;
        var prior = Charged("preparation");
        var runner = new Runner(frame, fit.A, fit.B);
        Save("diagnostics.started.json", new { Worlds = 4, PriorCpuSeconds = prior, Limit = Admission });
        var results = new List<object>();
        for (int i = 0; i < 4; i++)
        {
            var w = Initial(i % 2);
            var scenario = Training[(i / 2) * 2 + 1];
            var id = $"D{i + 1}";
            Append("started.jsonl", new { id, Stage = "diagnostics", scenario, w });
            var (run, result) = runner.Execute(id, scenario, w, () => Guard(prior, before, TrainingAdmission), verify: true, pilot: true);
            StoreNative(run, result, w, "diagnostics");
            results.Add(new { result, w });
            Print(result);
        }
        Save("diagnostics.json", new { CpuSeconds = Cpu - before, Results = results });
    }

    private static IEnumerable<Candidate> Rank(IEnumerable<Candidate> candidates) => candidates
        .OrderByDescending(x => x.Legal).ThenByDescending(x => x.Terminal)
        .ThenBy(x => x.Mean).ThenByDescending(x => x.Progress).ThenByDescending(x => x.Margin);

    private static Weights Mutate(Weights w, Random random, double scale)
    {
        double J(double current, double low, double high, double extent) =>
            random.NextDouble() < .20 ? current :
            Math.Clamp(current + scale * extent * (random.NextDouble() * 2 - 1), low, high);
        return new(J(w.Blend, 0, 1, .45), J(w.Margin, .05, 1.8, .65),
            J(w.Feedback, .15, 1.15, .40), J(w.Course, .55, 1.6, .52),
            J(w.Heading, .54, 1.5, .50), J(w.Yaw, .52, 1.45, .50),
            J(w.SpeedScale, .74, 1.12, .15), J(w.DriveFeedback, .14, .62, .25),
            J(w.StraightBoost, .1, 4.5, 1.9), J(w.PrepareMeters, 7, 28, 8),
            J(w.TerminalSpeedScale, .73, 1.10, .13), J(w.WheelSlipFeedback, 0, .45, .20),
            J(w.LaunchMergeMeters, 18, 39, 8));
    }

    private static Weights Refine(Candidate[] ranked, int index, Random random)
    {
        if (index < 6)
        {
            var anchor = ranked[0].Weights;
            return anchor with { LaneRetention = new[] { .25, .5, .75, .95, 1.1, 1.25 }[index],
                Margin = index == 5 ? Math.Max(.4, anchor.Margin) : anchor.Margin };
        }
        var w = ranked[random.Next(Math.Min(3, ranked.Length))].Weights;
        double J(double current, double extent, double low, double high) =>
            Math.Clamp(current + (random.NextDouble() * 2 - 1) * extent, low, high);
        for (int k = 0; k < 3; k++)
        {
            w = random.Next(12) switch
            {
                0 => w with { LaneRetention = J(w.LaneRetention, .42, 0, 1.35) },
                1 => w with { Margin = J(w.Margin, .38, .05, 1.8) },
                2 => w with { Feedback = J(w.Feedback, .19, .15, 1.3) },
                3 => w with { Course = J(w.Course, .22, .50, 1.6) },
                4 => w with { Heading = J(w.Heading, .20, .42, 1.5) },
                5 => w with { Yaw = J(w.Yaw, .20, .35, 1.5) },
                6 => w with { SpeedScale = J(w.SpeedScale, .09, .71, 1.1) },
                7 => w with { DriveFeedback = J(w.DriveFeedback, .15, .13, .66) },
                8 => w with { StraightBoost = J(w.StraightBoost, 1.5, .1, 4.5) },
                9 => w with { LaunchMergeMeters = J(w.LaunchMergeMeters, 6, 18, 45) },
                10 => w with { Blend = J(w.Blend, .20, 0, 1) },
                _ => w with { TerminalSpeedScale = J(w.TerminalSpeedScale, .12, .73, 1.1) }
            };
        }
        return w;
    }
    private static Candidate[] ReadCandidates(string name) => File.ReadLines(At(name))
        .Select(x => JsonSerializer.Deserialize<Candidate>(x, Json)!).ToArray();

    private static void Train(TrackFrame frame, Fit fit, bool extend)
    {
        var before = Cpu;
        var prior = Charged("preparation") + Charged("diagnostics") + Charged("training");
        var phase = extend ? "extension" : "training";
        var runner = new Runner(frame, fit.A, fit.B);
        var earlier = extend ? ReadCandidates("training-candidates.jsonl") : [];
        if (extend && (earlier.Length != 50 || !earlier.GroupBy(x => x.Seed).Any(g =>
        {
            var initial = g.First();
            var best = Rank(g).First();
            return best.Legal > initial.Legal || best.Terminal > initial.Terminal ||
                best.Legal == 4 && best.Mean < initial.Mean * .99;
        }))) throw new InvalidOperationException("Extension requires 200 completed trials and documented progress.");
        Save(phase + ".started.json", new { MaximumEpisodes = 200, Training, Seeds, PriorCpuSeconds = prior, Limit = TrainingAdmission });
        var next = Seeds.Select((seed, i) => new Random(seed + (extend ? 1 : 0))).ToArray();
        var pool = earlier.ToList();
        int episodes = 0;
        for (int index = 0; index < 25 && prior + Cpu - before < TrainingAdmission; index++)
        for (int j = 0; j < Seeds.Length && prior + Cpu - before < TrainingAdmission; j++)
        {
            var seed = Seeds[j];
            var ranked = Rank(pool.Where(x => x.Seed == seed)).Take(4).ToArray();
            var random = next[j];
            var scale = index < 11 ? 1 : .52;
            var w = ranked.Length == 0 ? InitialTraining(j) : Mutate(ranked[random.Next(ranked.Length)].Weights, random, scale);
            if (extend) w = Refine(ranked, index, random);
            if (pool.Any(x => x.Weights == w))
                w = w with { Margin = w.Margin + .000001 * (index + 1) };
            var outcomes = new List<Outcome>();
            foreach (var scenario in Training)
            {
                if (prior + Cpu - before >= TrainingAdmission) break;
                var id = $"{(extend ? "E" : "B")}{j + 1}-{index + 1:00}-{scenario.Id}";
                Append("started.jsonl", new { id, Stage = phase, seed, Index = index, scenario, w });
                var (_, result) = runner.Execute(id, scenario, w, () => Guard(prior, before, TrainingAdmission));
                Append("training-episodes.jsonl", new { seed, Index = index, phase, w, result });
                outcomes.Add(result);
                episodes++;
            }
            if (outcomes.Count != Training.Length) break;
            var candidate = new Candidate(seed, index + (extend ? 25 : 0), w, outcomes.ToArray());
            pool.Add(candidate);
            Append(phase + "-candidates.jsonl", candidate);
            Console.WriteLine($"{phase} {seed}/{candidate.Index}: legal={candidate.Legal}/4 terminal={candidate.Terminal}/4 " +
                $"mean={candidate.Mean:F3} ref={candidate.Progress:F1} CPU={prior + Cpu - before:F2}");
        }
        Save(phase + ".json", new { CpuSeconds = Cpu - before, Episodes = episodes,
            BestBySeed = pool.GroupBy(x => x.Seed).Select(g => Rank(g).First()).ToArray(),
            Selected = Rank(pool).FirstOrDefault() });
    }

    private static void Print(Outcome x) => Console.WriteLine(
        $"{x.Id}: legal={x.Legal} reserve={x.ReservePass} t={x.SectionSeconds:F3} ref={x.EndReference:F2} CPU={x.CpuSeconds:F3} {x.Reason}");

    private static void StoreNative(Run144 run, Outcome result, Weights w, string stage)
    {
        var path = At($"native/{run.Name}.zip");
        using (var archive = new EvidenceBundle162(path))
        {
            foreach (var d in run.Riders[0].Decisions)
                archive.Policy(d.Choice.Time.ToString("R", System.Globalization.CultureInfo.InvariantCulture), d);
            archive.Complete(run, result, run.Riders[0].Activations,
                new { Stage = stage, w, FitSha256 = Hash(At("fit.json")), BudgetSha256 = Hash(Docs + "/budget.json") });
        }
        Append("archives.jsonl", new { Path = $"native/{run.Name}.zip", Sha256 = Hash(path), stage });
    }

    private static void Validate(TrackFrame frame, Fit fit)
    {
        var before = Cpu;
        var prior = Charged("preparation") + Charged("diagnostics") + Charged("training") + Charged("extension");
        var runner = new Runner(frame, fit.A, fit.B);
        var phase = File.Exists(At("extension.json")) ? "extension" : "training";
        var selected = Load<JsonElement>(phase + ".json").GetProperty("Selected").Deserialize<Candidate>(Json)
            ?? throw new InvalidOperationException("No trained model to freeze");
        var held = Study156.Read<Scenario[]>(Docs + "/held.json");
        Save("freeze.json", new { Selected = selected, Held = held, FitSha256 = Hash(At("fit.json")),
            HeldSha256 = Hash(Docs + "/held.json"), Sources = PolicySources(), PriorCpuSeconds = prior });
        Save("validation.started.json", new { PolicyWorlds = 8, ExtraWorlds = 2, PriorCpuSeconds = prior });
        var rows = new List<object>();
        foreach (var scenario in held)
        {
            if (prior + Cpu - before >= Admission) break;
            Append("started.jsonl", new { id = scenario.Id, Stage = "validation", scenario, selected.Weights });
            var (run, result) = runner.Execute(scenario.Id, scenario, selected.Weights,
                () => Guard(prior, before, Admission), verify: true);
            StoreNative(run, result, selected.Weights, "held");
            Print(result);
            Outcome? reference = null;
            string? source = null;
            if (scenario.Reference161 is { } label)
            {
                source = $"docs/experiments/trajectory-continuation/native/{label}.json.gz";
                var original = Study156.Read<Run144>(source);
                var saved = original.Riders.Single();
                if (JsonSerializer.Serialize(saved.Spec, Json) != JsonSerializer.Serialize(scenario.Spec, Json) ||
                    saved.Gate != scenario.Field)
                    throw new InvalidOperationException("Unpaired archived baseline initial condition");
                var samples = saved.Samples.TakeWhile(q => Runner.Ref(q) < runner.Goal).ToList();
                samples.Add(saved.Samples[samples.Count]);
                reference = runner.Analyze(original with { Riders = [saved with { Samples = samples.ToArray() }] },
                    scenario, independent: true);
            }
            rows.Add(new { result, reference, source, SourceSha256 = source is null ? null : Hash(source),
                Gain = result.ReservePass && reference?.Legal == true ? 1 - result.SectionSeconds / reference.SectionSeconds : null });
        }
        Outcome? correction = null;
        var example = held.FirstOrDefault(x => x.Id == "H2");
        if (example is not null && prior + Cpu - before < Admission)
        {
            Append("started.jsonl", new { id = "P1", Stage = "physical disturbance", scenario = example, selected.Weights });
            var (run, result) = runner.Execute("P1", example, selected.Weights, () => Guard(prior, before, Admission),
                verify: true, perturb: true);
            StoreNative(run, result, selected.Weights, "disturbance");
            correction = result;
            Print(result);
        }
        Outcome? replay = null;
        var bestTraining = selected.Outcomes.Where(x => x.Legal).MinBy(x => x.SectionSeconds);
        if (bestTraining is not null && prior + Cpu - before < Admission)
        {
            var scenario = bestTraining.Scenario;
            Append("started.jsonl", new { id = "R1", Stage = "frozen training-witness confirmation", scenario, selected.Weights });
            var (run, result) = runner.Execute("R1", scenario, selected.Weights,
                () => Guard(prior, before, Admission), verify: true);
            StoreNative(run, result, selected.Weights, "training-witness");
            replay = result;
            Print(result);
        }
        Save("validation.json", new { CpuSeconds = Cpu - before, ChargedCpuSeconds = prior + Cpu - before,
            Rows = rows, Perturbation = correction, TrainingWitness = replay });
    }

    private static Dictionary<string, string> PolicySources() => new[] { "Policy.cs", "Teacher.cs", "Runner.cs",
        "Program.cs", "NativeWorld166.Generated.cs" }.ToDictionary(x => x, x => Hash("tools/autonomous-fast-corner/" + x));

    private static void Pair(TrackFrame frame, Fit fit)
    {
        var before = Cpu;
        var prior = Charged("preparation") + Charged("diagnostics") + Charged("training") +
            Charged("extension") + Charged("validation");
        var held = Load<JsonElement>("freeze.json").GetProperty("Held").Deserialize<Scenario[]>(Json)!;
        var missing = held.Where(x => x.Reference161 is null).ToArray();
        // Resolve the exact archived selector before admitting a native world.
        _ = MultiActionExperiment135.Policy();
        var recovered = File.Exists(At("pairing.started.json"));
        if (recovered)
            prior += Charged("pairing-interruption");
        if (recovered && (File.Exists(At("pairing.json")) || Charged("pairing-interruption") <= 0))
            throw new InvalidOperationException("Cannot overwrite an earlier pairing");
        var runner = new Runner(frame, fit.A, fit.B);
        Save(recovered ? "pairing.resumed.json" : "pairing.started.json", new { BaselineArms = missing.Length, PriorCpuSeconds = prior, Limit = Admission });
        var results = new List<object>();
        for (var i = 0; i < missing.Length; i++)
        {
            var scenario = missing[i];
            var start = Cpu;
            var remaining = Admission - prior - (Cpu - before);
            if (remaining < 15) break;
            var share = (remaining - 8) / (missing.Length - i);
            var id = (recovered ? "REF2-" : "REF-") + scenario.Id;
            Append("started.jsonl", new { id, Stage = "paired baseline arm", scenario, AllocatedCpuSeconds = share });
            void Budget()
            {
                Guard(prior, before, Admission);
                if (Cpu - start >= share)
                    throw new InvalidOperationException("paired #161 CPU allocation exhausted");
            }

            Continuation161.Planner161? planner = null;
            int reviews = 0;
            Decision144 Decide(FreeRacingInput input, CrankClutch144 crank, RiderSpec spec, TrackFrame track,
                TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, string variant)
            {
                Budget();
                planner ??= new(spec, false, new(Enabled: true, MaximumRoots: 3), Budget,
                    settings: new(Enabled: true, Adaptive: true));
                Decision144 decision;
                try { decision = planner.Choose(input, crank); }
                catch (IOException error) { throw new InvalidOperationException("baseline evidence dependency: " + error.Message, error); }
                var last = planner.Last;
                if (last is null || !last.ConditionalCertificate || !last.Independent ||
                    last.Tree.Length != 13 || last.Terminals.Length != 9)
                    throw new InvalidOperationException("missing original #161 certificate");
                reviews++;
                return decision;
            }

            Run144 run;
            using (var world = new NativeWorld166(new(Enabled: true, RequiredLaps: 1, MaximumSeconds: 27,
                SoloGate: scenario.Field), diagnosticFrame: frame, diagnosticRider: scenario.Spec,
                safetyHalfMeter: true, stopReference: runner.Goal))
            {
                run = world.Run(id, "K1", Decide);
            }
            var result = runner.Analyze(run, scenario, true, cpu: Cpu - start);
            var path = At($"native/{id}.zip");
            using (var bundle = new EvidenceBundle162(path))
            {
                foreach (var decision in run.Riders[0].Decisions)
                    bundle.Policy(decision.Choice.Time.ToString("R", System.Globalization.CultureInfo.InvariantCulture), decision);
                bundle.Complete(run, result, run.Riders[0].Activations, new { Original161 = true, Reviews = reviews, share });
            }
            Append("archives.jsonl", new { Path = $"native/{id}.zip", Sha256 = Hash(path), stage = "baseline" });
            results.Add(new { result, Reviews = reviews, AllocatedCpuSeconds = share, ChargedCpuSeconds = Cpu - start });
            Append("pairing-progress.jsonl", results[^1]);
            Print(result);
        }
        Save("pairing.json", new { CpuSeconds = Cpu - before, ChargedCpuSeconds = prior + Cpu - before, Results = results });
    }
}
