using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Stage44(Score40 Baseline, Candidate44[] Proposals, Candidate44[] Exact, Candidate44 Winner);
    private sealed record ReplanProposal44(int Id, double ForkMeters, Piece44[] Schedule);
    private sealed record ReplanTest44(ReplanProposal44 Proposal, Candidate44 Outcome);
    private sealed record Frozen44(string Basis, Piece44[] Schedule, double ForkMeters, double AttemptedHorizon,
        Policy42 Parent, HeldSeed38[] Holdouts);

    private static void RepeatLine44(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        var search = Read199<Stage44>(Path.Combine(output, "line-search.json.gz"));
        if (!search.Winner.Pass)
            throw new InvalidOperationException("The exact initial line must pass two laps before a four-lap test.");
        var selected = search.Winner.Schedule;
        var seed = Archived44(lab, "speedway222/qualification/nominal", "frozen H41 carrier source");
        double finish = 4 * Frame.LapMeters + PostMarkerMeters43;
        var run = ExactReplay44(lab, g, Ride44(lab, g, seed, selected[0].Start, selected, finish));
        var facts = Measure44(lab, g, run, false);
        var m = run.Motion;
        Save(Path.Combine(output, "initial-four.json"), new
        {
            Strategy = selected, Exact = facts, Finish = finish, FourPass = facts.Station >= finish - .001,
            run.ControllerCpu, run.IntegrationCpu, run.WallSeconds, NativeWorlds = 0
        });
        WriteEvidence39(lab, output, "initial-four", g, m.Seed, m.Tape, m.Flight);
        Console.WriteLine($"H43 initial four-lap test: {facts.Station:F3} m, {facts.Stop}.");
    }

    private static void Replan44(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        var initial = Read199<Stage44>(Path.Combine(output, "line-search.json.gz"));
        double lap = Frame.LapMeters, finish = 2 * lap + PostMarkerMeters43;
        var parent = ReadMotion42(lab, Path.Combine(output, "line-selected.json.gz"), "best verified line");
        var controllerRoot = Archived44(lab, "speedway222/qualification/nominal", "frozen H41 beginning");
        string basis = "best verified line";
        if (parent.Flight.Final.Distance < Frame.LapMeters + 21)
        {
            parent = Archived44(lab, "speedway223/periodic-selected", "verified H42 flying lap");
            basis = parent.Name;
        }
        var old = parent;
        var guides = ReadPlain214<Piece44[]>(Path.Combine(output, "line-selected-policy.json"));
        var primary = guides[^1].Route;
        double failedAt = old.Flight.Final.Distance;
        double nextBend = Half44 * Math.Ceiling((failedAt - 8) / Half44);
        if (nextBend > failedAt + 50) nextBend -= Half44;
        double reentry = initial.Winner.Pass ? Frame.LapMeters : Math.Max(Half44, nextBend);
        int projection = initial.Winner.Id;
        var trials = Enumerable.Range(0, Proposals44).Select(i => Build(i, reentry)).ToArray();
        using var proc = Process.GetCurrentProcess();
        var cpu = proc.TotalProcessorTime;
        var timer = Stopwatch.StartNew();
        string checkpoints = Path.Combine(output, "recourse-checkpoints");
        Directory.CreateDirectory(checkpoints);
        var tried = trials.AsParallel().WithDegreeOfParallelism(Jobs44).Select(p =>
            Persist44(checkpoints, p.Id, () =>
            {
                var run = Ride44(lab, g, old, p.ForkMeters, p.Schedule, finish);
                return new ReplanTest44(p, Measure44(lab, g, run, false));
            })).ToArray();
        var comparator = Comparer<Candidate44>.Create((a, b) => Compare44(a, b));
        var ids = tried.OrderBy(x => x.Outcome, comparator).Take(6).Select(r => r.Proposal.Id).ToHashSet();
        var exact = trials.Where(p => ids.Contains(p.Id)).AsParallel().WithDegreeOfParallelism(Jobs44).Select(p =>
        {
            var r = ExactReplay44(lab, g, Ride44(lab, g, old, p.ForkMeters, p.Schedule, finish));
            return (Plan: p, Ride: r, Score: Measure44(lab, g, r, false));
        }).OrderBy(x => x.Score, comparator).ToArray();
        var top = exact[0];
        bool keepLine = basis == "best verified line" && Compare44(initial.Winner, top.Score) <= 0;
        Candidate44 overall = keepLine ? initial.Winner : top.Score;
        var combined = keepLine ? guides : basis == "best verified line"
            ? guides.Where(s => s.Start < top.Plan.ForkMeters - .01).Concat(top.Plan.Schedule).ToArray()
            : top.Plan.Schedule;
        double referenceStart = Math.Min(guides[0].Start, controllerRoot.Flight.Final.Distance - .09);
        var four = !overall.Pass ? null : keepLine
            ? ExactReplay44(lab, g, Ride44(lab, g, controllerRoot, guides[0].Start, guides, 4 * lap + PostMarkerMeters43))
            : ExactReplay44(lab, g, Ride44(lab, g, old, top.Plan.ForkMeters, top.Plan.Schedule, 4 * lap + PostMarkerMeters43));
        Candidate44? fourScore = four is null ? null : Measure44(lab, g, four, false);
        var rollout = top.Ride.Motion;
        Save(Path.Combine(output, "recourse-search.json.gz"), new
        {
            BaselineSource = basis, BaselinePlan = projection, BaselineLastStation = failedAt, ForkFrom = reentry,
            PrecedingPolicyStart = referenceStart, Proposals = tried.OrderBy(x => x.Proposal.Id),
            Exact = exact.Select(x => new { x.Plan, x.Score }), Selected = top.Plan, Winner = top.Score,
            KeptInitialLine = keepLine, Overall = overall, Four = fourScore, FourPass = fourScore?.Station >= 4 * lap + PostMarkerMeters43 - .001,
            CpuSeconds = (proc.TotalProcessorTime - cpu).TotalSeconds, WallSeconds = timer.Elapsed.TotalSeconds, NativeWorlds = 0
        }, true);
        WriteEvidence39(lab, output, "recourse-selected", g, rollout.Seed, rollout.Tape, rollout.Flight);
        if (four is not null)
            WriteEvidence39(lab, output, "four-attempt", g, four.Motion.Seed, four.Motion.Tape, four.Motion.Flight);
        Save(Path.Combine(output, "recourse-combined-policy.json"), combined);
        Console.WriteLine($"H43 recourse: {tried.Count(x => x.Outcome.Pass)}/{tried.Length} screened; " +
            $"{exact.Count(x => x.Score.Pass)} exact pairs, farthest selected {top.Score.Station:F3} m.");

        ReplanProposal44 Build(int i, double target)
        {
            int block = i / 8, option = i % 8;
            int id = 300 + i;
            double q(int b) => Halton43(id + 1, b);
            if (initial.Winner.Pass)
            {
                double seam = target + (block >= 5 ? Half44 : 0);
                double fork = seam - new[] { 50d, 43, 35, 27, 18, 46, 28, 12 }[block];
                var prepare = primary with { Id = id, StartStation = fork,
                    EntryOutside = primary.EntryOutside + ((option % 4) - 1.5) * .40,
                    Inside = primary.Inside + (q(2) - .5) * .7,
                    StraightKph = primary.StraightKph + (block is 0 or 5 ? 0 : 3) + 13 * q(3),
                    TurnAcceleration = primary.TurnAcceleration + .05 + 1.9 * q(5),
                    Deceleration = primary.Deceleration + (q(7) - .5) * 1.35,
                    EarlyHeading = primary.EarlyHeading + (q(11) - .5) * 1.1,
                    ExitDrive = primary.ExitDrive + .22 * q(13) };
                double shift = seam + new[] { -3d, 9, 22, 40, 65, 94, 110, 134 }[option];
                var thereafter = primary with { Id = id, StartStation = shift,
                    StraightKph = primary.StraightKph + (option < 3 ? 1.5 : 3.0 + 9 * q(17)),
                    TurnAcceleration = primary.TurnAcceleration + .15 + 1.4 * q(19),
                    PreviewMeters = primary.PreviewMeters + (q(23) - .5) * 4.4,
                    Damping = primary.Damping + (q(29) - .5) * 3.3 };
                return new(id, fork, [new(fork, prepare), new(shift, thereafter)]);
            }

            double start = Math.Max(SecondExit44(lab) - 16,
                Math.Min(failedAt - 11 - 3 * (block % 2), target - new[] { 56d, 48, 41, 33, 27, 21, 15, 10 }[block]));
            var fresh = Plan44(lab, (13 * i + 29) % Proposals44, false) with { Id = id, StartStation = start };
            var aim = (block % 3) switch
            {
                0 => fresh with { EntryOutside = 8.1 + 2.4 * q(2), Inside = 5.5 + 2.1 * q(11), Damping = 13 + 4 * q(17) },
                1 => primary with { Id = id, StartStation = start, EntryOutside = primary.EntryOutside + (option - 3.5) * .53,
                    EarlyHeading = 1 + 8 * q(2), LeadMeters = 37 + 20 * q(3), StraightKph = 74 + 13 * q(5) },
                _ => fresh with { Guide = Guide44.PriorBlend, TurnAcceleration = 7.3 + 2.4 * q(3),
                    StraightKph = 67 + 21 * q(5), EarlyHeading = 4 + 5 * q(7), LeanScale = .93 + .10 * q(11) }
            };
            var recovery = fresh with { Id = id, Guide = (Guide44)(option % 4), Damping = 11.5 + q(13) * 5.5,
                EntryOutside = 7.4 + 2 * q(2), Inside = 5.2 + 2.5 * q(3), StraightKph = 70 + 19 * q(5) };
            double switchAt = target + new[] { -2d, 6, 13, 23, 34, 47, 62, 79 }[option];
            return new(id, start, [new(start, aim), new(Math.Max(start + 12, switchAt), recovery)]);
        }
    }

    private static void Hold44(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        var baseFile = Path.Combine(lab.Root, "data/experiments/speedway222/qualification/freeze.json");
        var baseline = ReadPlain214<Qualification42>(Path.Combine(lab.Root, "data/experiments/speedway222/qualification/qualification.json")).Policy;
        var input = MatchInputs42(lab);
        var schedule = ReadPlain214<Piece44[]>(Path.Combine(output, "line-selected-policy.json"));
        double linkEnd = SecondExit44(lab) + PostMarkerMeters43;
        double horizon = 2 * Frame.LapMeters + PostMarkerMeters43;
        double feasibleFork = Math.Min(schedule[0].Start, linkEnd - .15);
        HeldSeed38[] held = [new("new inside lower", -.23, -.057, .0043), new("new outside faster", .24, .064, .0060),
            new("new outside lower", -.15, .084, .0082), new("new inside faster", .32, -.091, .0102)];
        var frozen = new Frozen44("H41 policy 58 carrier with the first exact H43 two-lap line", schedule,
            feasibleFork, horizon, baseline, held);
        Save(Path.Combine(output, "feedback-freeze.json"), new { frozen, ParentPolicyFile = Path.GetFileName(Path.GetDirectoryName(baseFile)),
            CarrierRetainedThroughSecondExitPlus22 = true, NativeWorlds = 0 });

        var cases = new[] { new HeldSeed38("nominal", 0, 0, 0) }.Concat(held).ToArray();
        var receipts = new object[cases.Length];
        RunCase(0);
        Parallel.For(1, cases.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, RunCase);
        Save(Path.Combine(output, "feedback-qualification.json"), new { frozen, Trials = receipts, NativeWorlds = 0 });

        void RunCase(int i)
        {
            var h = cases[i];
            var clock = Stopwatch.StartNew();
            long cpu = ThreadTicks43();
            var arc = FirstArc42(lab, g, input, h);
            var prior = Adaptive42(lab, g, arc, input.Carrier, baseline);
            double parentCpu = (ThreadTicks43() - cpu) / 1e7;
            double parentWall = clock.Elapsed.TotalSeconds;
            var baseExact = ExactLink40(lab, g, prior);
            var parentMotion = new Fork40(h.Name, baseExact.Root, baseExact.Flight, baseExact.Commands);
            var baseScore = Score42(lab, g, baseExact, baseline);
            double chosenFork = Math.Min(feasibleFork, parentMotion.Flight.Final.Distance - .14);
            if (baseScore.Linked && chosenFork < 0)
                throw new InvalidOperationException("A passing carrier has no feasible handoff.");
            Ride44Result trial;
            if (parentMotion.Flight.Final.Distance < chosenFork + .10 || chosenFork < 0)
                trial = new(parentMotion, schedule, [], 0, 0, 0);
            else
            {
                var plan = schedule.Select((p, n) => n == 0 ? p with { Start = chosenFork } : p).ToArray();
                trial = Ride44(lab, g, parentMotion, chosenFork, plan, horizon);
            }
            var verified = ExactReplay44(lab, g, trial);
            var result = Measure44(lab, g, verified, false);
            Ride44Result? retained = null;
            Candidate44? retainedScore = null;
            if (baseScore.Linked)
            {
                double afterRecovery = parentMotion.Flight.Final.Distance;
                var valid = schedule.LastOrDefault(x => x.Start <= afterRecovery) ?? schedule[0];
                var retainedPlan = new[] { valid with { Start = afterRecovery } }
                    .Concat(schedule.Where(x => x.Start > afterRecovery)).ToArray();
                retained = Ride44(lab, g, parentMotion, afterRecovery, retainedPlan, horizon);
                retained = ExactReplay44(lab, g, retained);
                retainedScore = Measure44(lab, g, retained, false);
            }
            var d = trial.Decisions;
            string key = i == 0 ? "feedback-nominal" : $"feedback-holdout-{i}";
            var motion = verified.Motion;
            Save(Path.Combine(output, key + "-decisions.json.gz"), new
            {
                Condition = h, Root = motion.Seed, motion.Tape, motion.Flight.Final, motion.Flight.Stop,
                H41Decisions = prior.Decisions, H43Decisions = d, result, Parent = baseScore,
                Samples = motion.Flight.Steps.Select((row, n) => (row, n)).Where(x => x.n % 40 == 0)
                    .Select(x => new { Station = motion.Flight.Distances[x.n], State = x.row.After,
                        Front = x.row.Forces.Front.Normal, Rear = x.row.Forces.Rear.Normal,
                        x.row.Forces.RearSlip, x.row.Forces.Rider }),
                ParentLast = parentMotion.Flight.Final, ParentCpu = parentCpu, ParentWall = parentWall,
                RetainedController = retained?.Decisions, RetainedTape = retained?.Motion.Tape, RetainedScore = retainedScore,
                H43ControllerCpu = trial.ControllerCpu, H43IntegrationCpu = trial.IntegrationCpu, H43Wall = trial.WallSeconds
            }, true);
            if (i == 0)
            {
                WriteEvidence39(lab, output, key, g, motion.Seed, motion.Tape, motion.Flight);
                if (retained is not null)
                    WriteEvidence39(lab, output, "carrier-retained-nominal", g, retained.Motion.Seed,
                        retained.Motion.Tape, retained.Motion.Flight);
            }
            receipts[i] = new
            {
                Condition = h, Fork = chosenFork, Parent = baseScore, H43 = result, H43Timing = Timing44(d),
                H41Timing = Timing44(prior.Decisions.Select(x => x.Milliseconds).ToArray()),
                H41CompleteCpu = parentCpu, H41Wall = parentWall, H43ControllerCpu = trial.ControllerCpu,
                H43IntegrationCpu = trial.IntegrationCpu, H43Wall = trial.WallSeconds,
                CarrierRetained = retainedScore, RetainedTiming = retained is null ? null : Timing44(retained.Decisions),
                RetainedControlCpu = retained?.ControllerCpu, RetainedIntegrationCpu = retained?.IntegrationCpu,
                RetainedWall = retained?.WallSeconds
            };
            Console.WriteLine($"H43 frozen {h.Name}: carrier linked {baseScore.Linked}, H43 linked {result.Link.Linked}, " +
                $"station {result.Station:F2} m ({result.Stop}).");
        }
    }

    private static object Timing44(StateDecision44[] decisions) => Timing44(decisions.Select(x => x.Milliseconds).ToArray());

    private static object Timing44(double[] times)
    {
        Array.Sort(times);
        double? Q(double p) => times.Length == 0 ? null : times[(int)Math.Ceiling(times.Length * p) - 1];
        return new
        {
            Count = times.Length, P50 = Q(.50), P95 = Q(.95), P99 = Q(.99), Max = Q(1),
            Missed50 = times.Count(x => x > 50)
        };
    }
}
