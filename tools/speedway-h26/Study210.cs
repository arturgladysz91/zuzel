using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Evidence210 = "data/experiments/speedway210";
    private const string BaseCommit210 = "3f668b43a5ead239133a52f5c9a0149d24f5c1ad";
    private const string FrozenSha209 = "F5604A0F89374D14520078C92D7FEAAD04D488D336652603ED4B87D2CBD5D0DD";
    private sealed record Policy210(string BaseSha256, BendPace210[] Bends, RecoveryWindow209[] Recovery);
    private sealed record Context210(Base208 Source, GeometryPolicy207 Geometry, CandidatePlan208 Frozen)
    {
        internal Func<Start197, FreeRacingAction> FrozenLaw => Law209(Source, Geometry, Frozen);
    }
    private sealed record Root210(string Fixture, Predictor198 Model, Start197 State, double Goal, double Sweep = 100);
    private sealed record Race210(string Fixture, bool Complete, double? Finish, string Stop, double Sweep,
        double LastLegalS, int Steps, HeadingSample198 End, RacingFailure206? Failure, ContactFailure203? Contact);
    private sealed record Trial210(int Id, int Parent, string Stage, Policy210 Policy, Race210[] Races);
    private static readonly Policy210 Baseline210 = new(FrozenSha209, [], []);

    private static Context210 ContextFor210(string root)
    {
        string path = Path.Combine(root, Evidence209, "frozen-candidate.json");
        if (Hash210(path) != FrozenSha209) throw new InvalidDataException("Study #209 candidate hash differs.");
        return new(ReadBase208(root), Geometry207(root), ReadPlan208(path));
    }

    private static string Hash210(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static Policy210 Read210(string path)
    {
        var policy = JsonSerializer.Deserialize<Policy210>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException("Invalid Study #210 policy.");
        if (policy.BaseSha256 != FrozenSha209 || policy.Bends.Any(b => b.Visit is < 0 or > 5) ||
            policy.Bends.Select(b => b.Visit).Distinct().Count() != policy.Bends.Length ||
            policy.Recovery.Any(w => w.Visit is < 0 or > 4 || w.From >= w.To))
            throw new InvalidDataException("Invalid Study #210 baseline, bend or recovery window.");
        return policy;
    }

    private static Func<Start197, FreeRacingAction> Law210(Context210 source, Policy210 policy, bool optionalRecovery = true,
        bool inheritedRecovery = true)
    {
        var original = inheritedRecovery ? source.FrozenLaw :
            Law209(source.Source, source.Geometry, source.Frozen with { Recovery209 = null });
        var edits = new ExitPace210(Map199, policy.Bends);
        FreeRacingAction Feedback(Start197 state) => edits.At(state, original(state), Map199);
        if (!optionalRecovery || policy.Recovery.Length == 0) return Feedback;
        var model = Model208(Spec193(StartingGate.D, 1, 8));
        var windows = policy.Recovery.Select(w => (Window:w, Bend: source.Source.Policy.Bend(w.Visit))).ToArray();
        return state =>
        {
            var action = Feedback(state);
            foreach (var (window, bend) in windows)
            {
                double relative = state.State.Rider.TotalReferenceMeters - bend.Exit;
                if (relative <= window.From || relative >= window.To) continue;
                var proposed = Recovery209.Apply(state, action, window, bend.Exit);
                return Recovery209.Select(state, action, proposed, model, Feedback, 3 * Frame.LapMeters);
            }
            return action;
        };
    }

    private static Root210 RootFor210(string root, string fixture)
    {
        var (seed, spec) = CourseFixture208(root, fixture);
        return new(fixture, Model208(spec), seed.Start, seed.Goal);
    }

    private static Root210 Prefix210(Root210 root, Func<Start197, FreeRacingAction> law, double meters)
    {
        var cursor = root.Model.BeginFeedback207(root.State, law, root.Goal, .50);
        while (cursor.Snapshot.S < meters && cursor.Outcome is null && cursor.Steps < 10000)
            cursor.Advance(1);
        if (cursor.Outcome is not null || cursor.Snapshot.S < meters)
            throw new InvalidOperationException($"Ineligible prefix: {root.Fixture}, {cursor.Outcome}.");
        return root with { State = cursor.Checkpoint(), Sweep = cursor.LegalSweep207 };
    }

    private static Race210 RaceFrom210(Root210 root, Func<Start197, FreeRacingAction> law)
    {
        var cursor = root.Model.BeginFeedback207(root.State, law, root.Goal, .50);
        cursor.Advance(10000);
        return new(root.Fixture, cursor.Reached, cursor.Reached ? cursor.GoalTime : null,
            cursor.Outcome ?? "step cap", Math.Min(root.Sweep, cursor.LegalSweep207), cursor.LastLegalS207,
            cursor.Steps, cursor.Snapshot, cursor.StrictFailure206, cursor.ContactFailure);
    }

    private static IOrderedEnumerable<Trial210> Rank210(IEnumerable<Trial210> trials) => trials
        .OrderByDescending(x => x.Races.All(r => r.Complete))
        .ThenByDescending(x => x.Races.Count(r => r.Complete))
        .ThenByDescending(x => x.Races.All(r => r.Complete) ? 0 : x.Races.Min(r => r.LastLegalS))
        .ThenBy(x => x.Races.Max(r => r.Finish ?? double.PositiveInfinity))
        .ThenByDescending(x => x.Races.Min(r => r.Sweep))
        .ThenBy(x => x.Races[0].Finish ?? double.PositiveInfinity)
        .ThenBy(x => x.Id);

    private static void Search210(string root, string output, string arm, string? componentFolder)
    {
        var source = ContextFor210(root);
        var training = TrainingStarts207.Select(f => RootFor210(root, f)).ToArray();
        var screen = training.Concat(Screening208.Keys.Select(f => RootFor210(root, f))).ToArray();
        var stages = arm switch
        {
            "A" => Array.Empty<int[]>(),
            "B" => [[0], [1], [0, 1]],
            "C" => [[2], [3], [2, 3]],
            "D" => [[4], [4]],
            "E" => [[5], [0, 1, 2, 3, 4, 5], [0, 1, 2, 3, 4, 5]],
            _ => throw new ArgumentException("Use arm A, B, C, D or E."),
        };
        int baseBudget = 192, jointBudget = 144, batchSize = 12, workers = 4;
        const int stepBudget = 11_000_000;
        const double cpuBudget = 1050, wallBudget = 1050;
        var random = new Random(21000 + arm[0]);
        var trials = new List<Trial210>();
        var screens = new List<Trial210>();
        var prefixes = new List<object>();
        int id = 0;
        long steps = 0;
        Directory.CreateDirectory(output);
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        double Cpu() => (process.TotalProcessorTime - cpu).TotalSeconds;
        bool WithinBudget() => Cpu() < cpuBudget && wall.Elapsed.TotalSeconds < wallBudget && steps < stepBudget;

        Trial210 Evaluate(int trialId, int parent, string stage, Policy210 policy, Root210[] origins)
        {
            var law = Law210(source, policy);
            return new(trialId, parent, stage, policy, origins.Select(r => RaceFrom210(r, law)).ToArray());
        }

        void Store(IEnumerable<Trial210> rows)
        {
            foreach (var row in rows)
            {
                trials.Add(row);
                steps += row.Races.Sum(r => (long)r.Steps);
            }
        }

        Trial210 incumbent = Evaluate(++id, 0, "reference", Baseline210, training);
        Store([incumbent]);
        if (arm == "E")
        {
            if (componentFolder is null) throw new ArgumentException("E requires the B/C/D output parent.");
            var components = new[] { "B", "C", "D" }.Select(x => Read210(Path.Combine(componentFolder, x, "candidate.json"))).ToArray();
            for (int mask = 1; mask <= 7; mask++)
            foreach (double scale in new[] { 1d, .75, .5 })
            {
                var bends = components.Where((_, i) => (mask & (1 << i)) != 0).SelectMany(x => x.Bends)
                    .Select(b => b with { Pulses = b.Pulses.Select(p => new PacePulse210(p.Drive * scale,
                        p.Steer * scale, p.Line * scale, p.Alignment * scale)).ToArray() }).ToArray();
                Store([Evaluate(++id, 0, $"combination {mask}, scale {scale}", Baseline210 with { Bends = bends }, training)]);
            }
            incumbent = Rank210(trials).First();
        }

        for (int stageIndex = 0; stageIndex < stages.Length && WithinBudget(); stageIndex++)
        {
            var visits = stages[stageIndex];
            string stage = $"{arm}{stageIndex + 1}";
            var stageBasis = incumbent;
            var basisLaw = Law210(source, stageBasis.Policy);
            double boundary = ExitPace210.Gates(source.Source.Policy.Bend(visits.Min()))[0].Start - 3;
            var origins = training.Select(r => Prefix210(r, basisLaw, boundary)).ToArray();
            prefixes.Add(new { Stage = stage, Parent = stageBasis.Id, Visits = visits, Boundary = boundary,
                Roots = origins.Select(o => new { o.Fixture, o.State, o.Sweep }) });
            // The cached checkpoint precedes every editable pulse and carries the exact
            // delayed inputs and observer history of the unchanged common prefix.
            var identity = Evaluate(++id, stageBasis.Id, stage, stageBasis.Policy, origins);
            if (identity.Races.Zip(stageBasis.Races).Any(p => p.First.Complete != p.Second.Complete ||
                p.First.Finish != p.Second.Finish || Math.Abs(p.First.Sweep - p.Second.Sweep) > 1e-9))
                throw new InvalidDataException($"{stage}: cached prefix does not reproduce its time-zero successor.");
            var bank = new List<Trial210> { identity };
            Store([identity]);
            int count = visits.Length == 1 ? baseBudget : jointBudget;
            for (int offset = 0; offset < count && WithinBudget(); offset += batchSize)
            {
                var jobs = new (int Id, int Parent, Policy210 Policy)[Math.Min(batchSize, count - offset)];
                for (int i = 0; i < jobs.Length; i++)
                {
                    var parent = i % 4 == 0 ? bank[random.Next(bank.Count)] : bank[0];
                    double scale = offset < count / 3 ? 1 : offset < count * 2 / 3 ? .60 : .32;
                    if (i % 6 == 0) scale *= 1.8;
                    jobs[i] = (++id, parent.Id, Mutate210(parent.Policy, visits, random, scale, i));
                }
                var rows = new Trial210[jobs.Length];
                Parallel.For(0, jobs.Length, new ParallelOptions { MaxDegreeOfParallelism = workers }, i =>
                {
                    var job = jobs[i];
                    rows[i] = Evaluate(job.Id, job.Parent, stage, job.Policy, origins);
                });
                Store(rows);
                bank = Rank210(bank.Concat(rows)).Take(8).ToList();
                var best = bank[0];
                Console.WriteLine($"{stage} {offset + jobs.Length}/{count} best {best.Id}: worst {best.Races.Max(r => r.Finish):F6}, sweep {best.Races.Min(r => r.Sweep):F4}; {Cpu():F1} CPU s");
            }
            incumbent = bank[0];
            Save(Path.Combine(output, stage + ".checkpoint.json.gz"), new { Stage = stage, Best = incumbent,
                Cpu = Cpu(), Wall = wall.Elapsed.TotalSeconds, Steps = steps }, true);
        }

        var shortlist = Rank210(trials.Where(x => x.Races.All(r => r.Complete)))
            .DistinctBy(x => JsonSerializer.Serialize(x.Policy, Json)).Take(12).ToList();
        if (shortlist.All(x => x.Id != 1)) shortlist.Add(trials[0]);
        foreach (var candidate in shortlist)
            screens.Add(Evaluate(candidate.Id, candidate.Parent, "time-zero screen", candidate.Policy, screen));
        var winner = Rank210(screens).First();
        Save(Path.Combine(output, "candidate.json"), winner.Policy);
        Save(Path.Combine(output, "search.json.gz"), new { Arm = arm, Training = TrainingStarts207,
            Screens = screen.Select(r => r.Fixture), Bounds = new { baseBudget, jointBudget, batchSize, workers,
                stepBudget, cpuBudget, wallBudget, MaximumScreened = 13 }, Trials = trials, Screening = screens,
            Winner = winner.Id, Prefixes = prefixes, Steps = steps,
            ScreeningSteps = screens.Sum(x => x.Races.Sum(r => (long)r.Steps)), Cpu = Cpu(),
            Wall = wall.Elapsed.TotalSeconds, NativeWorlds = 0 }, true);
        Console.WriteLine($"{arm} winner {winner.Id}: {winner.Races.Count(r => r.Complete)}/7; worst {winner.Races.Max(r => r.Finish):F6}; {Cpu():F2} CPU s");
    }

    private static Policy210 Mutate210(Policy210 parent, int[] visits, Random random, double scale, int member)
    {
        var edits = parent.Bends.ToDictionary(b => b.Visit, b => b.Pulses);
        int count = member % 5 == 0 ? 3 : member % 3 == 0 ? 2 : 1;
        for (int n = 0; n < count; n++)
        {
            int visit = visits[random.Next(visits.Length)];
            var pulses = edits.TryGetValue(visit, out var original) ? (PacePulse210[])original.Clone() : new PacePulse210[ExitPace210.PulseCount];
            int index = random.Next(pulses.Length);
            var old = pulses[index];
            double Jitter(double width) => (2 * random.NextDouble() - 1) * width * scale;
            int mode = random.Next(6);
            pulses[index] = new(
                mode != 1 ? Math.Clamp(old.Drive + Jitter(index >= 4 ? .25 : .19), -.8, 1) : old.Drive,
                mode != 0 ? Math.Clamp(old.Steer + Jitter(index >= 4 ? .009 : .013), -.065, .065) : old.Steer,
                mode is 3 or 5 ? Math.Clamp(old.Line + Jitter(.15), -.75, .75) : old.Line,
                mode >= 4 && index >= 3 ? Math.Clamp(old.Alignment + Jitter(.07), -.4, .4) : old.Alignment);
            edits[visit] = pulses;
        }
        return parent with { Bends = edits.OrderBy(p => p.Key).Select(p => new BendPace210(p.Key, p.Value)).ToArray() };
    }
}
