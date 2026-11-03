using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim;
using LineThrottle156;
using S3Performance162;
using SharedWorldSpeedway;

namespace FastCorner165;

internal sealed record Trial(string Id, Plan Plan, bool Legal, string Outcome, double CpuSeconds,
    double EndReference, double EndTime, Audit Safety, JsonElement Metrics, StartSample144 Initial, string Fingerprint,
    FreeRacingAction[] Commands);

internal static class Program
{
    private sealed record Started(string id, Plan plan);

    private const string Docs = "docs/experiments/fast-corner-feasibility";
    private const string Reference = "docs/experiments/trajectory-continuation/native/native-dev.json.gz";
    private static readonly string[] SeedNames = ["first-A-T1-final", "first-B-T1-final", "first-C-T1-final"];
    private static readonly int[] Targets = [60, 70, 80, 90];
    private const double ToleranceKph = 2.5;
    private const double MaxCpu = 900;
    private const double AdmissionCpu = 880;
    private static readonly Process Process = Process.GetCurrentProcess();
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    private static readonly JsonSerializerOptions Pretty = new(Json) { WriteIndented = true };

    private static double Cpu => Process.TotalProcessorTime.TotalSeconds;
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string SeedPath(string name) => $"docs/experiments/line-throttle-physics/native/{name}.json.gz";

    private static void Write(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(stream, value, Pretty);
    }

    private static void Append(string path, object value) =>
        File.AppendAllText(path, JsonSerializer.Serialize(value, Json) + "\n");

    private static Trial[] ReadTrials(string output) => File.ReadLines(Path.Combine(output, "trials.jsonl"))
        .Select(line => JsonSerializer.Deserialize<Trial>(line, Json)!).ToArray();

    public static void Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("--search" or "--extend" or "--resume" or "--verify"))
            throw new ArgumentException("Study165 OFF. Explicit --search | --extend | --resume | --verify <evidence-directory> required.");
        if (!File.Exists(Path.Combine(Docs, "budget.json")))
            throw new InvalidOperationException("Run from the repository root with the declared budget.");
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var frame = NativeWorld156.Frame();
        var section = Section.First(frame);
        if (Math.Abs(section.NextEntry - section.Exit - 62) > 1e-8)
            throw new InvalidOperationException("Unexpected geometry");
        if (args[0] == "--verify") Verify(output, frame, section);
        else Search(output, frame, section, args[0] != "--search", args[0] == "--resume");
    }

    private static void Search(string output, TrackFrame frame, Section section, bool extend, bool resume)
    {
        var phasePath = Path.Combine(output, extend ? "extension.json" : "search.json");
        var trialPath = Path.Combine(output, "trials.jsonl");
        var startedPath = Path.Combine(output, "started.jsonl");
        var began = Cpu;
        var seeds = SeedNames.ToDictionary(name => name, name => Study156.Read<Run144>(SeedPath(name)).Riders.Single());
        var saved = extend ? ReadTrials(output).ToList() : [];
        if (extend && (saved.Count < 100 || !resume && saved.Count != 100 || !saved.Take(100).Any(t => t.Legal) || File.Exists(Path.Combine(output, "selection.json"))))
            throw new InvalidOperationException("Extension requires 100 completed ledger rows, a legal full candidate and no frozen selection.");
        if (!extend && (File.Exists(trialPath) || File.Exists(startedPath)))
            throw new InvalidOperationException("Existing trial ledger");
        if (!extend)
        {
            var baseline = Study156.Read<Run144>(Reference);
            var window = baseline.Riders.Single().Samples.TakeWhile(s =>
                s.Input.Rider.TotalReferenceMeters < section.Goal).ToList();
            window.Add(baseline.Riders.Single().Samples[window.Count]);
            Write(Path.Combine(output, "reference.json"), new
            {
                Source = Reference, Sha256 = Hash(Reference), section, baseline.Fingerprint,
                Metrics = section.Metrics(window.ToArray()),
                Audit = Section.Inspect(baseline with { Riders = [baseline.Riders[0] with { Samples = window.ToArray() }] }, frame, true)
            });
            var count = (int)Math.Ceiling(frame.LapMeters / .05);
            Write(Path.Combine(output, "track.json"), Enumerable.Range(0, count + 1).Select(i =>
            {
                var reference = i * frame.LapMeters / count;
                var s = frame.Shape!.At(reference);
                return new
                {
                    Inner = new { X = s.Position.X - Math.Sin(s.Heading), Y = s.Position.Y + Math.Cos(s.Heading) },
                    Outer = new { X = s.Position.X + Math.Sin(s.Heading) * (frame.Shape.Width(reference) - 1),
                        Y = s.Position.Y - Math.Cos(s.Heading) * (frame.Shape.Width(reference) - 1) }
                };
            }));
        }

        var reserved = extend ? ReadCpu(Path.Combine(output, "search.json")) : 0;
        var interruptionPath = Path.Combine(output, "interruption.json");
        var previousStarts = File.Exists(startedPath) ? File.ReadLines(startedPath)
            .Select(line => JsonSerializer.Deserialize<Started>(line, Json)!).ToArray() : [];
        var interruptedCharge = resume ? ReadCpu(interruptionPath) : 0;
        if (resume && (!File.Exists(phasePath + ".started") || File.Exists(phasePath) ||
            interruptedCharge <= 0 || previousStarts.Length <= saved.Count))
            throw new InvalidOperationException("Resume needs a recorded interrupted extension, including its consumed CPU allowance and started IDs.");
        reserved += interruptedCharge;
        var limit = extend ? 250 : 100;
        Write(phasePath + (resume ? ".resumed" : ".started"), new { Limit = limit, reserved, MaxCpu, AdmissionCpu });
        var keys = previousStarts.Select(t => Key(t.plan)).ToHashSet();
        var admitted = previousStarts.Length;
        var initial = (extend ? ExtendedPlans() : InitialPlans()).GetEnumerator();
        var random = new Random(extend ? 16502 : 16501);
        var newCount = 0;
        var pool = Array.Empty<Trial>();
        while (admitted < limit && reserved + Cpu - began < AdmissionCpu)
        {
            Plan plan;
            if (initial.MoveNext())
                plan = initial.Current;
            else
            {
                if (newCount % 8 == 0 || pool.Length == 0)
                    pool = LocalParents(saved);
                plan = Mutate(pool[random.Next(pool.Length)].Plan, random, extend);
            }
            if (!keys.Add(Key(plan)))
                continue;
            var id = $"B{++admitted:000}";
            var spec = Spec(plan);
            Append(startedPath, new { id, plan, Spec = spec });
            var watch = Cpu;
            void Guard()
            {
                if (reserved + Cpu - began >= AdmissionCpu)
                    throw new InvalidOperationException("study CPU stop");
            }
            var run = Execute(id, plan, spec, frame, section, seeds.GetValueOrDefault(plan.Family), null, Guard);
            var row = Summarize(run, plan, section, frame, false, watch);
            if (saved.Any(t => t.Fingerprint == row.Fingerprint))
                Console.WriteLine($"{id}: different proposal produced a previously seen native trajectory");
            Append(trialPath, row);
            saved.Add(row);
            newCount++;
            Console.WriteLine($"{id} {plan.Family} nominal={plan.TargetKph} entry={Metric(row, "Entry"):F2} " +
                $"at={row.EndReference:F2} {row.Outcome} CPU={reserved + Cpu - began:F2}");
        }
        Write(phasePath, new { Count = newCount, Total = saved.Count, Admitted = admitted, CpuSeconds = Cpu - began, InterruptedCpuAllowance = interruptedCharge,
            Sources = SeedNames.ToDictionary(SeedPath, name => Hash(SeedPath(name))), Limit = limit });
    }

    private static IEnumerable<Plan> InitialPlans()
    {
        for (var i = 0; i < SeedNames.Length; i++)
            yield return new(SeedNames[i], new[] { 60, 70, 60 }[i], 0, 3.5f, new double[6]);

        // The seed's measured trace supplies only the initial search direction.
        // Cross the approach speed, line, throttle profile and signed steer pulse.
        var index = 0;
        foreach (int target in Targets)
        {
            for (var i = 0; i < 12; i++)
            {
                var start = target >= 80 ? -26 : i % 2 == 0 ? 0 : -14;
                double wide = i % 3;
                double coast = i / 3;
                yield return new("free", target, start, target >= 80 ? 3.5f + (float)wide * 2 : 3.5f,
                    [.43 + .08 * (i % 2), .014 + .004 * (i % 3), .065,
                        new[] { -.035, 0, .04 }[(i + index) % 3], 1 - coast * 2,
                        .03 + .09 * (i % 2), 6 + coast * 2, .20 + .04 * wide,
                        6 + coast * 5, .65 + .12 * wide, .04 + .05 * (i % 2),
                        .40 + .07 * (i % 3), 4.5 + wide * 1.5, 2.2 + wide * 2.7,
                        4.5 + wide * 1.4, 4.5 + wide, 5 + wide, 5 + wide]);
                index++;
            }
        }
        foreach (var family in SeedNames.Take(2))
        foreach (var approach in new[] { -.13, -.065, .02 })
        foreach (var straight in new[] { -.08, .06 })
        {
            var target = family == SeedNames[0] ? 60 : 70;
            yield return new(family, target, 0, 3.5f,
                [.65, approach, 0, straight, approach < -.1 ? -.004 : 0, .001]);
        }
    }

    private static IEnumerable<Plan> ExtendedPlans()
    {
        // Keep the known launch and refine the joint sequence around the full
        // witness before searching another family of geometry and entry states.
        foreach (var family in SeedNames.Take(2))
        foreach (var approach in new[] { 0, -.003, -.009, -.21 })
        foreach (var straight in new[] { -.012, .015, .045 })
        {
            var target = family == SeedNames[0] ? 60 : 70;
            yield return new(family, target, 0, 3.5f, [0, approach, 0, straight, 0, .00015]);
        }

        var random = new Random(16511);
        double Between(double a, double b) => a + (b - a) * random.NextDouble();
        for (int i = 0; i < 25; i++)
        foreach (var target in Targets)
        {
            var start = target >= 80 ? -26 : i % 3 == 0 ? 0 : -19;
            var offset = i % 5 == 0 ? 3.5f : (float)Between(2.5, 9);
            var apex = Math.Clamp(offset + Between(-3, 2), .7, 11);
            var activeBrake = target == 90 ? Between(-4, 5) : Between(-13, 3);
            yield return new("damped", target, start, offset,
                [Between(.37,.82), Between(.008,.022), Between(.065,.20), Between(-.035,.065),
                 activeBrake, Between(0,.2), Between(6,19), Between(.14,.33),
                 Between(7,29), Between(.60,1), Between(0,.23), Between(.36,.65),
                 offset, apex, Between(2.8,9), Between(3.5,8), Between(4,9), Between(3.5,8),
                 Between(-.7,.7)]);
        }
    }
    private static string Key(Plan plan) => JsonSerializer.Serialize(
        new { plan.Family, plan.StartMeters, plan.StartOffset, Target = plan.Family is "free" or "damped" ? plan.TargetKph : 0, plan.Knobs }, Json);

    private static Trial[] LocalParents(List<Trial> trials) => Targets.SelectMany(target =>
    {
        var tagged = trials.Where(t => t.Plan.TargetKph == target).ToArray();
        return tagged.OrderBy(t => !t.Legal).ThenBy(t => InBin(t, target) ? 0 : 1)
            .ThenBy(t => t.Legal ? Duration(t) ?? 30 : -t.EndReference)
            .ThenBy(t => Math.Abs((Metric(t, "Entry") ?? 0) - target)).Take(4);
    }).DistinctBy(t => t.Id).ToArray();

    private static Plan Mutate(Plan parent, Random random, bool fine)
    {
        var knobs = parent.Knobs.ToArray();
        double Signed() => 2 * random.NextDouble() - 1;
        if (parent.Family is "free" or "damped")
        {
            var step = fine ? .45 : 1;
            (double Low, double High, double Scale)[] bounds =
                [(.26,.85,.09),(.004,.05,.009),(.015,.20,.04),(-.15,.15,.055),
                 (-14,6,5),(0,.5,.17),(2,30,8),(0,.65,.16),(0,32,10),(.15,1,.25),(0,.55,.17),
                 (.25,.7,.12),(1,10,2),(0,12,2),(1,12,2),(1,10,2),(1,10,2),(1,10,2)];
            for (int i = 0; i < bounds.Length; i++)
            {
                var b = bounds[i];
                if (random.NextDouble() < .45)
                    knobs[i] = Math.Clamp(knobs[i] + Signed() * b.Scale * step, b.Low, b.High);
            }
            return parent with { Knobs = knobs };
        }

        double size = fine ? .65 : 1;
        double[] scales = [.4, .12, .045, .12, .012, .01];
        double[] ranges = [1.35, .35, .24, .6, .08, .08];
        for (int i = 0; i < knobs.Length; i++)
            if (random.NextDouble() < .55)
                knobs[i] = Math.Clamp(knobs[i] + Signed() * scales[i] * size, -ranges[i], ranges[i]);
        return parent with { Knobs = knobs };
    }

    private static RiderSpec Spec(Plan plan) => NativeWorld156.Rider(StartingGate.B) with
    {
        ReferenceMeters = plan.StartMeters, OffsetMeters = plan.StartOffset, TargetOffsetMeters = plan.StartOffset
    };

    private static Run144 Execute(string id, Plan plan, RiderSpec spec, TrackFrame frame, Section section,
        RiderRun144? seed, FreeRacingAction[]? commands, Action guard)
    {
        int next = 0;
        Decision144 Choose(FreeRacingInput input, CrankClutch144 crank, RiderSpec rider, TrackFrame worldFrame,
            TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, string variant)
        {
            guard();
            var action = commands is not null
                ? next < commands.Length ? commands[next++] : throw new InvalidOperationException("frozen commands exhausted")
                : seed is not null ? Guide.Seed(plan, seed, input, section)
                : plan.Family == "damped" ? Guide.Damped(plan, input, worldFrame, section)
                : Guide.Free(plan, input, worldFrame, section);
            action.Validate();
            return new(new(input.Time, action, FreeActionSafety.SafeInHorizon, false, [], 0, 0), 0);
        }

        using var world = new NativeWorld156(new(Enabled: true, RequiredLaps: 1, MaximumSeconds: 30),
            diagnosticFrame: frame, diagnosticRider: spec, safetyHalfMeter: true, stopReference: section.Goal);
        return world.Run(id, "K1", Choose);
    }

    private static Trial Summarize(Run144 run, Plan plan, Section section, TrackFrame frame, bool verify, double watch)
    {
        var rider = run.Riders.Single();
        var end = rider.Samples[^1];
        var start = rider.Samples[0];
        var audit = Section.Inspect(run, frame, verify);
        var reached = Section.Cross(rider.Samples, section.Goal) is not null;
        var stopped = start.Input.Rider.Speed == 0 && start.Input.Rider.AngularVelocity == 0 &&
            start.Input.Omega == 0 && start.Input.Steering == 0 && start.Input.Active == new FreeRacingAction(0, 0);
        var cleanStop = run.Outcome == "experiment timeout" && reached;
        bool legal = stopped && cleanStop && audit.Reason is null && run.Contacts.Length == 0 &&
            audit.SweptMeters >= .5 && audit.SweptMismatchMeters <= 1e-9;
        return new(run.Name, plan, legal, legal ? "complete and legal" :
            audit.Reason ?? (run.Outcome == "experiment timeout" ? "goal not reached" : run.Outcome),
            Cpu - watch, end.Input.Rider.TotalReferenceMeters, end.Input.Time, audit,
            JsonSerializer.SerializeToElement(section.Metrics(rider.Samples), Json), start, run.Fingerprint,
            rider.Decisions.Select(d => d.Choice.Action).ToArray());
    }

    private static double? Metric(Trial trial, string key, string field = "Kph") =>
        trial.Metrics.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null &&
        value.TryGetProperty(field, out var scalar) ? scalar.GetDouble() : null;

    private static double? Duration(Trial trial) => trial.Metrics.GetProperty("SectionSeconds").ValueKind == JsonValueKind.Null
        ? null : trial.Metrics.GetProperty("SectionSeconds").GetDouble();

    private static bool InBin(Trial trial, int target) =>
        Metric(trial, "Entry") is { } entry && Math.Abs(entry - target) <= ToleranceKph;

    private static double ReadCpu(string path) => File.Exists(path)
        ? JsonDocument.Parse(File.ReadAllBytes(path)).RootElement.GetProperty("CpuSeconds").GetDouble() : 0;

    private static bool AuditQueue(RiderRun144 rider)
    {
        const double delay = .24;
        const double tolerance = 1e-8;
        var scheduled = rider.Decisions.Select(d =>
            new FreeRacingCommand(d.Choice.Time, d.Choice.Time + delay, d.Choice.Action)).ToArray();
        if (rider.Activations.Length > scheduled.Length ||
            !rider.Activations.SequenceEqual(scheduled.Take(rider.Activations.Length)))
            return false;
        var end = rider.Samples[^1].Input;
        var active = rider.Activations.LastOrDefault()?.Action ?? new FreeRacingAction(0, 0);
        return end.Pending.SequenceEqual(scheduled.Skip(rider.Activations.Length)) &&
            end.Active == active &&
            rider.Activations.All(c => c.ActivationAt <= end.Time + tolerance) &&
            end.Pending.All(c => c.ActivationAt >= end.Time - tolerance);
    }

    private static void Verify(string output, TrackFrame frame, Section section)
    {
        var began = Cpu;
        var reserved = ReadCpu(Path.Combine(output, "search.json")) + ReadCpu(Path.Combine(output, "extension.json")) +
            ReadCpu(Path.Combine(output, "interruption.json"));
        var trials = ReadTrials(output);
        var candidates = trials.Where(t => !t.Plan.Anchor).ToArray();
        var selected = new List<Trial>();
        foreach (var target in Targets)
        {
            var bin = candidates.Where(t => InBin(t, target)).ToArray();
            if (bin.Length == 0)
                continue;
            var fastest = bin.Where(t => t.Legal).MinBy(Duration);
            selected.Add(fastest is null
                ? bin.OrderByDescending(t => t.EndReference).ThenBy(t => t.EndTime).First()
                : bin.Where(t => t.Legal && Duration(t) <= Duration(fastest) + .02)
                    .OrderByDescending(t => Metric(t, "Exit")).ThenByDescending(t => t.Safety.SweptMeters)
                    .ThenBy(Duration).First());
        }
        var bestOverall = candidates.Where(t => t.Legal && selected.All(s => s.Id != t.Id))
            .MinBy(Duration);
        if (bestOverall is not null)
            selected.Add(bestOverall);
        var fastestRejected = candidates.Where(t => !t.Legal && Metric(t, "Entry").HasValue &&
            selected.All(s => s.Id != t.Id)).MaxBy(t => Metric(t, "Entry"));
        if (fastestRejected is not null)
            selected.Add(fastestRejected);
        var frozen = selected.DistinctBy(t => t.Fingerprint).Take(6).ToArray();
        Write(Path.Combine(output, "selection.json"), new { Entries = frozen, reserved, Cap = 6,
            Rules = "Best full legal first in each actual entry bin; otherwise longest legal prefix. Then fastest remaining legal and highest entry speed among remaining failures." });
        var verification = new List<object>();
        foreach (var picked in frozen)
        {
            if (reserved + Cpu - began >= AdmissionCpu)
                break;
            var id = "C-" + picked.Id;
            Append(Path.Combine(output, "native-started.jsonl"), new { id, Trial = picked.Id, picked.Plan });
            var watch = Cpu;
            void Guard()
            {
                if (reserved + Cpu - began >= AdmissionCpu)
                    throw new InvalidOperationException("study CPU stop");
            }
            var run = Execute(id, picked.Plan, Spec(picked.Plan), frame, section, null, picked.Commands, Guard);
            var observed = Summarize(run, picked.Plan, section, frame, true, watch);
            var parity = run.Fingerprint == picked.Fingerprint && observed.Commands.SequenceEqual(picked.Commands);
            var queue = AuditQueue(run.Riders[0]);
            var result = new { Trial = observed, Selected = picked.Id, ExactSearchParity = parity, QueueParity = queue,
                ConfirmedLegal = parity && queue && observed.Legal, Scope = "One fresh Box2D world, physical replay and independent post-step footprint/swept audit. No 13/9." };
            using (var evidence = new EvidenceBundle162(Path.Combine(output, "native", id + ".zip")))
            {
                for (int index = 0; index < run.Riders[0].Decisions.Length; index++)
                    evidence.Policy($"command-{index:0000}", run.Riders[0].Decisions[index]);
                evidence.Complete(run, result, run.Riders[0].Activations,
                    new { section, BudgetSha256 = Hash(Path.Combine(Docs, "budget.json")), picked.Plan });
            }
            var path = Path.Combine(output, "native", id + ".zip");
            verification.Add(new { result, ArchiveSha256 = Hash(path), ChargedCpuSeconds = Cpu - watch });
            Console.WriteLine($"{id}: {observed.Outcome}, parity={parity}, CPU={reserved + Cpu - began:F2}");
        }
        Write(Path.Combine(output, "verification.json"), new { Trials = verification,
            CpuSeconds = Cpu - began, CombinedCpuSeconds = reserved + Cpu - began });
    }
}
