using System.Diagnostics;
using System.Text.Json;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Evidence211 = "data/experiments/speedway211";
    private const string Parent211 = "490e3578e7e67c837da88c2788857e49d136991d";
    private const string Frozen210Sha = "AE98FCE5026C5F84388AF0ADF70550B4EE9BC76B56FF5232D980C2FD4FE6185B";
    private static readonly Policy211 Reference211 = new(Frozen210Sha, 1, []);
    private sealed record Policy211(string BaseSha256, double FirstRecovery, CornerLink211[] Links);
    private sealed record Source211(Context210 Parent, Policy210 Frozen);
    private sealed record Passage211(int Visit, double GateS, Start197 State, double StepSweep);
    private sealed record Attempt211(Race210 Race, double PeakKph, double Over100, double Over105, double Over110,
        double EarlyPeakKph, Passage211[] Marks);
    private sealed record Sample211(int Id, int Parent, string Stage, Policy211 Policy, Attempt211[] Results);

    private static Source211 SourceFor211(string root)
    {
        string path = Path.Combine(root, Evidence210, "frozen-candidate.json");
        if (Hash210(path) != Frozen210Sha) throw new InvalidDataException("Unexpected #210 policy hash.");
        return new(ContextFor210(root), Read210(path));
    }

    private static Policy211 Read211(string path)
    {
        var plan = JsonSerializer.Deserialize<Policy211>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException("Missing #211 policy.");
        if (plan.BaseSha256 != Frozen210Sha || !double.IsFinite(plan.FirstRecovery) || plan.FirstRecovery is < 0 or > 1 ||
            plan.Links.Select(x => x.Visit).Distinct().Count() != plan.Links.Length ||
            plan.Links.Any(x => x.Visit is < 0 or > 5 || x.ReleaseMeters is < -4 or > 47))
            throw new InvalidDataException("Invalid #211 policy.");
        return plan;
    }

    private static Func<Start197, FreeRacingAction> Law211(Source211 source, Policy211 plan, bool windowRecovery = true)
    {
        var recovery = plan.FirstRecovery == 1 ? source.Frozen.Recovery :
            plan.FirstRecovery == 0 ? [] :
            source.Frozen.Recovery.Select(w => w with { Gas = w.Gas * plan.FirstRecovery }).ToArray();
        var baseline = Law210(source.Parent, source.Frozen with { Recovery = recovery },
            optionalRecovery: windowRecovery, inheritedRecovery: windowRecovery);
        var coupled = new LinkFeedback211(Map199, plan.Links);
        return state => coupled.Apply(state, baseline(state));
    }

    private static Root210[] Development211(string root, bool screen) =>
        (screen ? TrainingStarts207.Concat(Screening208.Keys) : TrainingStarts207)
            .Select(f => RootFor210(root, f)).ToArray();

    private static Attempt211 Measure211(Root210 origin, Func<Start197, FreeRacingAction> law, Source211 source, int[] visits)
    {
        var gates = visits.SelectMany(i =>
        {
            var b = source.Parent.Source.Policy.Bend(i + 1);
            return new[] { (Visit: i, S: b.Entry), (Visit: i, S: b.Entry + b.Length * .5),
                (Visit: i, S: b.Exit + 12) };
        }).Where(g => g.S <= origin.Goal).OrderBy(g => g.S).ToArray();
        var records = new List<Passage211>();
        var cursor = origin.Model.BeginFeedback207(origin.State, law, origin.Goal, .50);
        double lastTime = origin.State.State.Time, peak = origin.State.State.Rider.Speed * 3.6, early = peak;
        double full100 = 0, full105 = 0, full110 = 0;
        double lastExit = source.Parent.Source.Policy.Bend(5).Exit;
        int mark = 0;
        while (mark < gates.Length && gates[mark].S < origin.State.State.Rider.TotalReferenceMeters) mark++;
        while (cursor.Outcome is null && cursor.Steps < 10000)
        {
            cursor.Advance(1);
            if (cursor.Outcome is not (null or "goal")) break;
            var sample = cursor.Snapshot;
            double speed = sample.Speed * 3.6, dt = sample.Time - lastTime;
            peak = Math.Max(peak, speed);
            if (sample.S < lastExit) early = Math.Max(early, speed);
            if (speed > 100) full100 += dt;
            if (speed > 105) full105 += dt;
            if (speed > 110) full110 += dt;
            lastTime = sample.Time;
            while (mark < gates.Length && sample.S >= gates[mark].S)
            {
                records.Add(new(gates[mark].Visit, gates[mark].S, cursor.Checkpoint(), cursor.LastSweep));
                mark++;
            }
        }
        var race = new Race210(origin.Fixture, cursor.Reached, cursor.Reached ? cursor.GoalTime : null,
            cursor.Outcome ?? "step cap", Math.Min(origin.Sweep, cursor.LegalSweep207), cursor.LastLegalS207,
            cursor.Steps, cursor.Snapshot, cursor.StrictFailure206, cursor.ContactFailure);
        return new(race, peak, full100, full105, full110, early, records.ToArray());
    }

    private static double Worst211(Sample211 trial) => trial.Results.Max(r => r.Race.Finish ?? double.PositiveInfinity);
    private static double Reserve211(Sample211 trial) => trial.Results.Min(r => r.Race.Sweep);
    private static bool Legal211(Sample211 trial) => trial.Results.All(r => r.Race.Complete);
    private static IOrderedEnumerable<Sample211> Rank211(IEnumerable<Sample211> trials) => trials
        .OrderByDescending(Legal211).ThenByDescending(t => t.Results.Count(r => r.Race.Complete))
        .ThenByDescending(t => Legal211(t) ? 0 : t.Results.Min(r => r.Race.LastLegalS))
        .ThenBy(Worst211).ThenByDescending(Reserve211)
        .ThenBy(t => t.Results[0].Race.Finish).ThenBy(t => t.Id);

    private static Sample211 Choose211(IEnumerable<Sample211> records)
    {
        var legal = Rank211(records.Where(Legal211)).ToArray();
        if (legal.Length == 0) throw new InvalidOperationException("No complete training policy.");
        return legal.FirstOrDefault(t => Reserve211(t) >= .55 && Worst211(t) <= Worst211(legal[0]) + .15) ?? legal[0];
    }

    private static CornerLink211 Neutral211(int visit) => new(visit, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0);
    private static Policy211 Replace211(Policy211 plan, CornerLink211 link) =>
        plan with { Links = plan.Links.Where(l => l.Visit != link.Visit).Append(link).OrderBy(l => l.Visit).ToArray() };

    private static CornerLink211 Mutate211(CornerLink211 p, Random rng, double scale, int mode)
    {
        double J(double x, double width, double lo, double hi) =>
            Math.Clamp(x + (2 * rng.NextDouble() - 1) * width * scale, lo, hi);
        return p with
        {
            ExitDrive = J(p.ExitDrive, .14, -.60, .90),
            StraightDrive = J(p.StraightDrive, mode == 0 ? .31 : .15, -.7, 1),
            ReleaseMeters = J(p.ReleaseMeters, 8, 14, 47),
            ReleaseDrive = J(p.ReleaseDrive, .13, -.80, .65),
            ExitSteer = J(p.ExitSteer, .007, -.045, .045),
            PrepareSteer = J(p.PrepareSteer, .010, -.065, .065),
            TurnSteer = J(p.TurnSteer, .009, -.08, .08),
            ApexSteer = J(p.ApexSteer, .008, -.06, .06),
            PrepareLineMeters = J(p.PrepareLineMeters, .12, -.7, .7),
            AlignmentGain = J(p.AlignmentGain, .055, -.4, .4),
        };
    }

    private static void Search211(string root, string output, string arm, string? folder)
    {
        var source = SourceFor211(root);
        var training = Development211(root, false);
        var screen = Development211(root, true);
        int visit = arm switch { "B" => 1, "C" => 4, "D1" => 0, "D3" => 2, "D4" => 3, "D6" => 5, "E" or "E2" or "R" or "A" => -1,
            _ => throw new ArgumentException("Unknown Study #211 arm.") };
        int[] checkedLinks = visit >= 0 ? [visit] : [0, 1, 2, 3, 4, 5];
        var trials = new List<Sample211>();
        var screened = new List<Sample211>();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        int next = 0;
        var baseline = Evaluate(++next, 0, "reference", Reference211, training);
        trials.Add(baseline);
        var roots = training;
        double? prefixMeters = null;
        if (visit > 0)
        {
            prefixMeters = LinkFeedback211.Begin(source.Parent.Source.Policy.Bend(visit)) - 3;
            roots = training.Select(r => Prefix210(r, Law211(source, Reference211), prefixMeters.Value)).ToArray();
            var identity = Evaluate(++next, baseline.Id, "identical prefix", Reference211, roots);
            if (identity.Results.Zip(baseline.Results).Any(p => p.First.Race.Finish != p.Second.Race.Finish ||
                Math.Abs(p.First.Race.Sweep - p.Second.Race.Sweep) > 1e-9))
                throw new InvalidOperationException("Cached prefix is not the original lineage.");
            trials.Add(identity);
        }
        var seeds = new List<Policy211>();
        if (visit >= 0)
        {
            foreach (double gas in new[] { .10, .28, .54, .82, 1 })
            foreach (double release in new[] { 22d, 33, 43 })
            foreach (double direction in new[] { -.009, .009 })
                seeds.Add(Replace211(Reference211, Neutral211(visit) with { StraightDrive = gas, ExitDrive = gas * .20,
                    ReleaseMeters = release, ReleaseDrive = -.2 * gas, PrepareSteer = direction, TurnSteer = -direction * .5 }));
            if (visit == 0)
                foreach (double gain in new[] { 0d, .4, .60, .78, .90 })
                    seeds.Add(Reference211 with { FirstRecovery = gain });
        }
        if (arm == "E")
        {
            if (folder is null) throw new ArgumentException("E needs the component search folder.");
            var parts = new[] { "B", "C", "D1", "D3", "D4", "D6" }
                .Select(n => Read211(Path.Combine(folder, n, "candidate.json"))).ToArray();
            foreach (var part in parts) seeds.Add(part);
            for (int mask = 1; mask < 64; mask++)
            {
                var set = parts.Where((p, i) => (mask & (1 << i)) != 0).ToArray();
                seeds.Add(Reference211 with { Links = set.SelectMany(x => x.Links).OrderBy(x => x.Visit).ToArray(),
                    FirstRecovery = set.Min(x => x.FirstRecovery) });
            }
        }
        if (arm == "E2")
        {
            if (folder is null) throw new ArgumentException("E2 needs the first joint search folder.");
            using var zip = new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(folder, "E", "search.json.gz")),
                System.IO.Compression.CompressionMode.Decompress);
            using var archive = JsonDocument.Parse(zip);
            var parents = archive.RootElement.GetProperty("Screened").Deserialize<Sample211[]>(Json)!;
            foreach (var parent in Rank211(parents.Where(Legal211)).Take(4))
            {
                seeds.Add(parent.Policy);
                foreach (double recovery in new[] { .54, .68, .79, .88 })
                    seeds.Add(parent.Policy with { FirstRecovery = recovery });
            }
        }
        if (arm == "R")
        {
            if (folder is null) throw new ArgumentException("R needs the first development selection archive.");
            using var zip = new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(folder, "selection.json.gz")),
                System.IO.Compression.CompressionMode.Decompress);
            using var archive = JsonDocument.Parse(zip);
            var parents = archive.RootElement.GetProperty("Trials").Deserialize<Sample211[]>(Json)!.Where(Legal211).ToArray();
            var robust = Rank211(parents.Where(p => Reserve211(p) >= .55)).First().Policy;
            var fast = Rank211(parents).First().Policy;
            seeds.Add(robust);
            foreach (double recovery in new[] { .54, .60, .64, .68, .72 })
            foreach (double scale in new[] { .24, .42, .64, .82 })
            {
                var shaped = fast.Links.Select(l => l with { ExitDrive = l.ExitDrive * scale,
                    StraightDrive = l.StraightDrive * scale, ReleaseDrive = l.ReleaseDrive * scale,
                    ExitSteer = l.ExitSteer * scale, PrepareSteer = l.PrepareSteer * scale,
                    TurnSteer = l.TurnSteer * scale, ApexSteer = l.ApexSteer * scale,
                    PrepareLineMeters = l.PrepareLineMeters * scale, AlignmentGain = l.AlignmentGain * scale }).ToArray();
                seeds.Add(fast with { FirstRecovery = recovery, Links = shaped });
                var focus = shaped.FirstOrDefault(l => l.Visit == 1);
                if (focus is not null && recovery is .60 or .68)
                    seeds.Add(Replace211(robust with { FirstRecovery = recovery }, focus));
            }
        }
        seeds = seeds.DistinctBy(s => JsonSerializer.Serialize(s, Json)).ToList();
        var random = new Random(21100 + arm.Sum(c => (int)c));
        const int proposals = 84, workers = 4, batch = 12, maxSteps = 3_000_000;
        const double maxCpu = 420, maxWall = 540;
        long usedSteps = trials.Sum(t => t.Results.Sum(r => r.Race.Steps));
        for (int offset = 0; arm != "A" && offset < proposals && usedSteps < maxSteps &&
            wall.Elapsed.TotalSeconds < maxWall && (process.TotalProcessorTime - cpu).TotalSeconds < maxCpu; offset += batch)
        {
            var pool = Pool();
            var jobs = new (int Id, int Parent, Policy211 Plan)[Math.Min(batch, proposals - offset)];
            for (int i = 0; i < jobs.Length; i++)
            {
                int index = offset + i;
                var parent = pool[i < 6 ? 0 : random.Next(pool.Length)];
                int linkVisit = visit >= 0 ? visit : random.Next(6);
                var plan = parent.Policy;
                if (index < seeds.Count) plan = seeds[index];
                else
                {
                    double scale = offset < 48 ? .85 : .48;
                    if (i % 4 == 0) scale *= .52;
                    var link = plan.Links.FirstOrDefault(p => p.Visit == linkVisit) ?? Neutral211(linkVisit);
                    plan = Replace211(plan, arm is "E2" or "R" ? Perturb(link, random, scale) : Mutate211(link, random, scale, i % 4));
                    if ((visit == 0 || visit < 0) && i % 5 == 0)
                        plan = plan with { FirstRecovery = Math.Clamp(plan.FirstRecovery + .20 * (random.NextDouble() - .65), 0, 1) };
                }
                jobs[i] = (++next, index < seeds.Count ? baseline.Id : parent.Id, plan);
            }
            var result = new Sample211[jobs.Length];
            Parallel.For(0, jobs.Length, new ParallelOptions { MaxDegreeOfParallelism = workers }, i =>
            {
                var (id, parent, plan) = jobs[i];
                result[i] = Evaluate(id, parent, "joint transition", plan, roots);
            });
            trials.AddRange(result);
            usedSteps += result.Sum(t => t.Results.Sum(r => (long)r.Race.Steps));
            var best = arm == "R" ? Choose211(trials) : Rank211(trials).First();
            Console.WriteLine($"{arm} {offset + jobs.Length}/{proposals}: trial {best.Id}, worst {Worst211(best):F6}, swept {Reserve211(best):F4}");
        }
        foreach (var t in Pool().Concat([baseline]).DistinctBy(t => JsonSerializer.Serialize(t.Policy, Json)))
            screened.Add(Evaluate(t.Id, t.Parent, "time-zero development screen", t.Policy, screen));
        var selected = Choose211(screened);
        Save(Path.Combine(output, "candidate.json"), selected.Policy);
        Save(Path.Combine(output, "search.json.gz"), new { Arm = arm, TrialCap = proposals, Batch = batch, Workers = workers,
            MaxSteps = maxSteps, MaxCpu = maxCpu, MaxWall = maxWall, PrefixMeters = prefixMeters,
            Roots = roots.Select(r => new { r.Fixture, r.State, r.Sweep }), Trials = trials, Screened = screened,
            Winner = selected.Id, ScoredSteps = usedSteps, Cpu = (process.TotalProcessorTime - cpu).TotalSeconds,
            Wall = wall.Elapsed.TotalSeconds, NativeWorlds = 0 }, true);
        Console.WriteLine($"{arm} winner {selected.Id}, worst {Worst211(selected):F6}, swept {Reserve211(selected):F6}");

        Sample211 Evaluate(int id, int parent, string stage, Policy211 policy, Root210[] starts)
        {
            var law = Law211(source, policy);
            return new(id, parent, stage, policy, starts.Select(r => Measure211(r, law, source, checkedLinks)).ToArray());
        }

        Sample211[] Pool()
        {
            var eligible = trials.Where(Legal211).ToArray();
            var fastest = Rank211(eligible).Take(5);
            if (arm == "R") fastest = Rank211(eligible.Where(t => Reserve211(t) >= .55)).Take(5).Concat(fastest.Take(3));
            return fastest
                .Concat(eligible.OrderByDescending(Reserve211).Take(3))
                .Concat(eligible.OrderByDescending(t => t.Results.Min(r => r.EarlyPeakKph)).Take(3))
                .DistinctBy(t => JsonSerializer.Serialize(t.Policy, Json)).ToArray();
        }
    }

    private static CornerLink211 Perturb(CornerLink211 move, Random random, double scale)
    {
        double delta = (2 * random.NextDouble() - 1) * scale;
        return random.Next(6) switch
        {
            0 => move with { StraightDrive = Math.Clamp(move.StraightDrive + .17 * delta, -.7, 1),
                ReleaseMeters = Math.Clamp(move.ReleaseMeters - 4 * delta, 14, 47) },
            1 => move with { ExitDrive = Math.Clamp(move.ExitDrive + .11 * delta, -.6, .9), ExitSteer = move.ExitSteer + .005 * delta },
            2 => move with { PrepareSteer = move.PrepareSteer + .005 * delta, TurnSteer = move.TurnSteer - .004 * delta },
            3 => move with { ApexSteer = move.ApexSteer + .005 * delta, TurnSteer = move.TurnSteer + .005 * delta },
            4 => move with { ReleaseDrive = Math.Clamp(move.ReleaseDrive + .12 * delta, -.8, .65),
                PrepareLineMeters = move.PrepareLineMeters + .08 * delta },
            _ => move with { AlignmentGain = move.AlignmentGain + .035 * delta, ExitSteer = move.ExitSteer - .004 * delta },
        };
    }
}
