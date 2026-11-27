using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static double Four45 => 4 * Frame.LapMeters + PostMarkerMeters43;
    private static double Pair45 => 2 * Frame.LapMeters + PostMarkerMeters43;

    private static IEnumerable<Plan45> Plans45(Lab38 lab, Plan45 baseline, string stage)
    {
        if (stage is "lines" or "transition")
        {
            // Nested sampling balances all four families, with the frozen A anchor retained.
            for (int i = 0; i < 64; i++)
            {
                var type = stage == "lines" ? (Line45)(i % 4) : (Line45)(1 + i % 3);
                int n = i / (stage == "lines" ? 4 : 3);
                double U(int radix) => Halton43(n + 1, radix);
                var p = baseline with { Id = (stage == "lines" ? 100 : 600) + i, Line = type };
                if (stage == "lines" && i == 0) { yield return p; continue; }
                var r = p.Route with { Id = p.Id };
                var line = type switch
                {
                    Line45.A => r,
                    Line45.B => r with { EntryOutside = 9.3 + 1.5 * U(2), Inside = 5.9 + 1.6 * U(3),
                        ExitOutside = 8.5 + 1.5 * U(5), EarlyHeading = 5 + 3 * U(7) },
                    Line45.C => r with { EntryOutside = 10 + 1.2 * U(2), Inside = 8 + 1.25 * U(3),
                        ExitOutside = 9.0 + 1.65 * U(5), EarlyHeading = 3 + 3 * U(7) },
                    _ => r with { EntryOutside = 8.4 + 2.6 * U(2), Inside = 6.05 + 2.4 * U(3),
                        ExitOutside = 6.3 + 3.9 * U(5), EarlyHeading = 3.4 + 5.8 * U(7) }
                };
                if (stage == "transition")
                    line = r with { Inside = line.Inside, ExitOutside = line.ExitOutside,
                        EntryOutside = baseline.Route.EntryOutside, EarlyHeading = baseline.Route.EarlyHeading };
                yield return p with { Route = line, ApexMeters = lab.Bend.Length * (.45 + .18 * U(11)),
                    ReleaseMeters = lab.Bend.Length + 13 * U(13) - 6,
                    TurnInMeters = 20 + 14 * U(17), DriveReserve = .48 * U(19),
                    RiderReserve = .42 * U(23), BankingReserve = .07 * U(29),
                    TransitionMeters = stage == "transition" ? 60 + 78 * U(31) : 0,
                    Blend = stage == "transition" ? .65 + .34 * U(37) : 1,
                    InheritedLine = stage == "transition" ? baseline.Route : null };
            }
            yield break;
        }

        int count = stage == "speed" ? 60 : 56;
        for (int i = 0; i < count; i++)
        {
            int id = (stage == "support" ? 200 : 300) + i;
            double U(int radix) => Halton43(i + 1, radix);
            if (stage == "support")
            {
                double support = .18 + .82 * U(2);
                double guard = .012 + .125 * U(3);
                var route = baseline.Route with { Id = id, Damping = baseline.Route.Damping + 2.8 * U(5) - .7,
                    PreviewMeters = baseline.Route.PreviewMeters + 3.0 * U(7) - 1.15 };
                yield return baseline with { Id = id, Route = route, RiderReserve = support, BankingReserve = guard,
                    DriveReserve = .04 + .44 * U(11), FeedbackSeconds = i % 8 == 1 ? .033 : .065 };
            }
            else
            {
                double band = 100 + 5 * (i % 5);
                var family = (Line45)((i / 5) % 4);
                double lift = U(17);
                var route = baseline.Route with { Id = id, StraightKph = band + 1 + 5 * U(2),
                    TurnAcceleration = 8.25 + 4 * U(3), Deceleration = 2.4 + 4 * U(5),
                    EntryOutside = 8.6 + 2.7 * U(7), Inside = 6.3 + 2.25 * U(11), ExitOutside = 7.15 + 3.0 * U(13),
                    PreviewMeters = 15 + 12 * U(19), EarlyHeading = 4 + 4.3 * U(23), Damping = 10.0 + 5.0 * U(29),
                    LeadMeters = 33 + 15 * U(31), ExitDrive = .45 + .5 * U(37) };
                yield return baseline with { Id = id, Route = route, Line = family, SpeedBand = band,
                    ApexMeters = lab.Bend.Length * (.42 + .20 * U(41)),
                    ReleaseMeters = lab.Bend.Length + 5 - 14 * U(43), TurnInMeters = 22 + 17 * U(47),
                    DriveReserve = .53 + .46 * lift, BankingReserve = .055 + .09 * U(53), RiderReserve = .12 + .58 * U(59),
                    DirectionalPace = true };
            }
        }
    }

    private static void Search45(Lab38 lab, Geometry215 g, string output, string stage)
    {
        bool speed = stage == "speed", support = stage == "support";
        var baseline = Baseline45(lab);
        if (support && File.Exists(Path.Combine(output, "lines.json.gz")))
        {
            var line = Read199<Stage45>(Path.Combine(output, "lines.json.gz"));
            baseline = line.Pick ?? baseline;
        }
        var root = speed ? Archived44(lab, "speedway222/frontier/speed-frontier", "H41 ordinary straight frontier")
            : Archived44(lab, "speedway222/qualification/nominal", "H41 common flying carrier");
        double firstExit = speed ? lab.Bend.Length : SecondExit44(lab);
        double fork = speed ? firstExit - 20 : baseline.Route.StartStation;
        double finish = speed ? firstExit + Half44 + PostMarkerMeters43 : support ? Four45 : Pair45;
        var plans = Plans45(lab, baseline, stage).ToArray();
        var list = PlanSearch45(lab, g, output, stage, root, plans, fork, firstExit, finish);
        Save(Path.Combine(output, stage + ".json.gz"), list, true);
    }

    private static Stage45 PlanSearch45(Lab38 lab, Geometry215 g, string output, string stage, Fork40 root,
        Plan45[] plans, double fork, double firstExit, double finish)
    {
        bool speed = stage.StartsWith("speed", StringComparison.Ordinal);
        var keys = Path.Combine(output, stage + "-checkpoints");
        Directory.CreateDirectory(keys);
        int jobs = Math.Min(6, Environment.ProcessorCount);
        var screen = plans.AsParallel().WithDegreeOfParallelism(jobs).Select(plan =>
            Persist44(keys, plan.Id, () =>
            {
                var r = Ride45(lab, g, root, fork, plan, finish, exact: false);
                var result = Summarize45(lab, g, r, finish, firstExit);
                Console.WriteLine($"{stage} {plan.Id}: {result.Station:F2} m, {result.StraightPeakKph:F2} km/h, {result.Stop}");
                return result;
            })).OrderBy(r => r.Plan.Id).ToArray();

        IEnumerable<Summary45> Ranked(IEnumerable<Summary45> trials) => trials
            .OrderByDescending(x => speed ? x.Finished && x.StraightPeakKph >= x.Plan.SpeedBand : x.Finished)
            .ThenByDescending(x => x.Finished).ThenByDescending(x => x.Finished ? 0 : x.Station)
            .ThenBy(x => (speed ? x.ExitToFinishSeconds : x.Laps.ElementAtOrDefault(1)?.Seconds) ?? double.PositiveInfinity)
            .ThenByDescending(x => x.StraightPeakKph).ThenBy(x => x.Plan.Id);

        var contenders = stage is "lines" or "transition" ? screen.GroupBy(x => x.Plan.Line).SelectMany(a => Ranked(a).Take(2))
            : speed ? screen.GroupBy(x => x.Plan.SpeedBand).SelectMany(a => Ranked(a).Take(1)
                .Concat(a.OrderByDescending(x => x.StraightPeakKph).Take(1))) : Ranked(screen).Take(8);
        var top = contenders.DistinctBy(x => x.Plan.Id).ToArray();
        var receipts = top.AsParallel().WithDegreeOfParallelism(jobs).Select(item =>
        {
            var r = Certify45(lab, g, Ride45(lab, g, root, fork, item.Plan, finish, exact: true));
            return (Run: r, Summary: Summarize45(lab, g, r, finish, firstExit));
        }).ToArray();
        var ranked = Ranked(receipts.Select(x => x.Summary)).ToArray();
        var keep = stage is "lines" or "transition" ? ranked.GroupBy(x => x.Plan.Line).Select(a => a.First()) :
            speed ? ranked.GroupBy(x => x.Plan.SpeedBand).Select(a => a.First()).Concat(ranked.OrderByDescending(x => x.StraightPeakKph).Take(1)) :
            ranked.Take(2);
        foreach (var item in keep.DistinctBy(x => x.Plan.Id))
        {
            var run = receipts.Single(x => x.Summary.Plan.Id == item.Plan.Id).Run;
            SaveRun45(lab, g, output, $"{stage}-{item.Plan.Id}", run, item);
        }
        Console.WriteLine($"{stage}: {screen.Length} screened, {ranked.Count(x => x.Finished)} exact horizons");
        return new(screen, ranked, ranked.FirstOrDefault()?.Plan, fork, finish);
    }

    private static void Sustain45(Lab38 lab, Geometry215 g, string output)
    {
        var line = Read199<Stage45>(Path.Combine(output, "lines.json.gz"));
        var gradual = Read199<Stage45>(Path.Combine(output, "transition.json.gz"));
        var support = Read199<Stage45>(Path.Combine(output, "support.json.gz"));
        var anchors = new[] { gradual.Exact.First(x => x.Finished && x.Plan.Line == Line45.B).Plan,
            gradual.Exact.First(x => x.Finished && x.Plan.Line == Line45.D).Plan,
            line.Exact.First(x => x.Finished && x.Plan.Line == Line45.D).Plan, support.Exact[0].Plan };
        var routes = Enumerable.Range(0, 56).Select(i =>
        {
            int n = i / anchors.Length, id = 800 + i;
            var p = anchors[i % anchors.Length];
            if (n == 0) return p with { Id = id };
            double U(int b) => Halton43(n, b);
            var route = p.Route with { Id = id,
                Damping = p.Route.Damping + 2.2 * (U(7) - .35),
                PreviewMeters = p.Route.PreviewMeters + 3.4 * (U(13) - .5) };
            return p with { Id = id, Route = route,
                RiderReserve = Math.Clamp(p.RiderReserve + .38 * (U(2) - .30), .025, 1),
                BankingReserve = Math.Clamp(p.BankingReserve + .11 * U(3), 0, .30),
                DriveReserve = p.DriveReserve * (1.06 - .56 * U(5)),
                EntryCorrection = .13 + .50 * U(17), TurnPhaseMeters = 6 * (U(19) - .5) };
        }).ToArray();
        var root = Archived44(lab, "speedway222/qualification/nominal", "exact sustain carrier");
        var stage = PlanSearch45(lab, g, output, "sustain", root, routes, line.Fork, SecondExit44(lab), Four45);
        Save(Path.Combine(output, "sustain.json.gz"), stage, true);
    }

    private sealed record SupportProof45(int Id, double OnlineStation, string OnlineStop, double OnlineHard,
        bool TerminalStateEquality, Summary45 Exact);

    private static void CertifySupport45(Lab38 lab, Geometry215 g, string output)
    {
        var support = Read199<Stage45>(Path.Combine(output, "support.json.gz"));
        var already = support.Exact.Select(x => x.Plan.Id).ToHashSet();
        var remaining = support.Screen.Where(x => x.Finished && !already.Contains(x.Plan.Id)).ToArray();
        var root = Archived44(lab, "speedway222/qualification/nominal", "remaining full support traces");
        var result = remaining.AsParallel().WithDegreeOfParallelism(Math.Min(6, Environment.ProcessorCount)).Select(p =>
        {
            var online = Ride45(lab, g, root, support.Fork, p.Plan, Four45, exact: true);
            var r = Certify45(lab, g, online);
            var s = Summarize45(lab, g, r, Four45, SecondExit44(lab));
            Console.WriteLine($"support {p.Plan.Id}: online {online.Motion.Flight.Final.Distance:F3} m, exact {s.Station:F3} m");
            return (Run: r, Proof: new SupportProof45(p.Plan.Id, online.Motion.Flight.Final.Distance, online.Motion.Flight.Stop,
                online.Motion.Flight.Ground, online.Motion.Flight.Final.State == r.Motion.Flight.Final.State, s));
        }).OrderByDescending(x => x.Proof.Exact.Station).ToArray();
        foreach (var item in result.Take(2).Concat(result.Where(x => x.Proof.Exact.Finished)).DistinctBy(x => x.Proof.Id))
            SaveRun45(lab, g, output, "support-exact-" + item.Proof.Id, item.Run, item.Proof.Exact);
        Save(Path.Combine(output, "support-exact.json.gz"), new { Proofs = result.Select(x => x.Proof) }, true);
    }

    private static void BestLaps45(Lab38 lab, Geometry215 g, string output)
    {
        var paths = new[] { "lines", "transition", "support", "sustain" };
        var reports = paths.Select(n => Read199<Stage45>(Path.Combine(output, n + ".json.gz"))).ToArray();
        var supplemental = Read199<SupportCertificate45>(Path.Combine(output, "support-exact.json.gz"));
        var collected = reports.SelectMany(x => x.Exact).Concat(supplemental.Proofs.Select(x => x.Exact))
            .Where(x => x.Station >= Pair45 - .001 && x.Laps.Length >= 2 && x.Laps[1].Seconds.HasValue).ToArray();
        var byLap = collected.OrderBy(x => x.Laps[1].Seconds).ToArray();
        var byPair = collected.OrderBy(x => x.Laps[0].Seconds + x.Laps[1].Seconds).ToArray();
        var root = Archived44(lab, "speedway222/qualification/nominal", "best complete lap carrier");
        var outputScores = new List<Summary45>();
        foreach (var score in new[] { byLap[0], byPair[0] }.DistinctBy(x => x.Plan.Id))
        {
            var run = Certify45(lab, g, Ride45(lab, g, root, reports[0].Fork, score.Plan, Pair45, exact: true));
            var result = Summarize45(lab, g, run, Pair45, SecondExit44(lab));
            if (!result.Finished || result.Laps[1].Seconds is not { } seconds ||
                Math.Abs(score.Laps[1].Seconds.GetValueOrDefault() - seconds) > .003)
                throw new InvalidOperationException($"Complete lap {score.Plan.Id} failed its independent roll-out.");
            SaveRun45(lab, g, output, "best-lap-" + score.Plan.Id, run, result);
            outputScores.Add(result);
        }
        Save(Path.Combine(output, "best-laps.json.gz"), new { ByLap = byLap, ByPair = byPair, Reproduced = outputScores }, true);
    }

    private static void SaveRun45(Lab38 lab, Geometry215 g, string output, string name, Running45 run, Summary45 score)
    {
        var m = run.Motion;
        WriteEvidence39(lab, output, name, g, m.Seed, m.Tape, m.Flight);
        Save(Path.Combine(output, name + "-decisions.json.gz"), new
        {
            score, run.Decisions, Timing = Timing44(run.Decisions),
            Rejected = DescribeRejection45(g, run.Rejected), m.Flight.Final, Pending = Pending43(m, m.Flight.Final, true),
            Profile = RiderHistory45(lab, g, m, 20)
        }, true);
    }

    private static void Runtime45(Lab38 lab, Geometry215 g, string output)
    {
        var lines = Read199<Stage45>(Path.Combine(output, "lines.json.gz"));
        var root = Archived44(lab, "speedway222/qualification/nominal", "single-worker reference");
        var supported = Read199<Stage45>(Path.Combine(output, "support.json.gz"));
        var full = supported.Exact.FirstOrDefault(x => x.Finished);
        var gradual = Read199<Stage45>(Path.Combine(output, "transition.json.gz"));
        var selectedLine = lines.Exact.Concat(gradual.Exact).Where(x => x.Finished)
            .OrderBy(x => x.Laps[1].Seconds).FirstOrDefault();
        var p = full?.Plan ?? selectedLine?.Plan ?? Baseline45(lab);
        double finish = full is not null ? Four45 : Pair45;
        var trials = new[] { p.FeedbackSeconds, .040, .030, .022, .016 }.Distinct().Select((dt, n) =>
        {
            var trial = p with { Id = 500 + n, FeedbackSeconds = dt };
            var r = Certify45(lab, g, Ride45(lab, g, root, lines.Fork, trial, finish, exact: true));
            var s = Summarize45(lab, g, r, finish, SecondExit44(lab));
            SaveRun45(lab, g, output, "runtime-" + trial.Id, r, s);
            Console.WriteLine($"runtime {trial.Id}: {s.Station:F3} m {s.Stop}");
            return (Score: s, Timing: Timing44(r.Decisions), P99: P9945(r.Decisions));
        }).ToArray();
        var pick = trials.Where(x => x.Score.Finished).OrderBy(x => x.P99).FirstOrDefault();
        Plan45 policy = pick.Score?.Plan ?? p;
        Save(Path.Combine(output, "runtime.json"), new
        {
            WorkerCount = 1, PhysicalReactionSeconds = ReactionSeconds216, Integration = "full spatial evaluation at stock step",
            Trials = trials.Select(x => new { x.Score, x.Timing }), Selected = policy
        });
        Save(Path.Combine(output, "feedback-freeze.json"), new Freeze45(policy, lines.Fork, finish, HoldoutSeeds45(),
            "Fastest P99 among exact target-horizon witnesses, with untuned holdouts."));
    }

    private static double P9945(StateDecision44[] decisions) => decisions.Length == 0 ? double.PositiveInfinity :
        decisions.Select(x => x.Milliseconds).Order().ElementAt((int)Math.Ceiling(decisions.Length * .99) - 1);

    private sealed record Freeze45(Plan45 Plan, double Fork, double Finish, HeldSeed38[] Starts, string Selection);

    private static HeldSeed38[] HoldoutSeeds45() =>
    [
        new("new A", -.34, -.067, .0034), new("new B", .29, .056, .0054),
        new("new C", -.19, .100, .0076), new("new D", .39, -.074, .0096)
    ];

    private static void Holdouts45(Lab38 lab, Geometry215 g, string output)
    {
        var freeze = ReadPlain214<Freeze45>(Path.Combine(output, "feedback-freeze.json"));
        var comparator = ReadPlain214<Piece44[]>(Input45(lab, "line-selected-policy.json"));
        var carrier = ReadPlain214<Qualification42>(
            Path.Combine(lab.Root, "data/experiments/speedway222/qualification/qualification.json")).Policy;
        var input = MatchInputs42(lab);
        var trials = freeze.Starts.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select((h, n) =>
        {
            long cpu = ThreadTicks43();
            var parent = Adaptive42(lab, g, FirstArc42(lab, g, input, h), input.Carrier, carrier);
            double carrierCpu = (ThreadTicks43() - cpu) / 1e7;
            var motion = new Fork40(h.Name, parent.Root, parent.Flight, parent.Commands);
            if (motion.Flight.Final.Distance < freeze.Fork)
                return new { Seed = h, CarrierCpu = carrierCpu, Carrier = Score42(lab, g, parent, carrier),
                    H43 = (Candidate44?)null, H44 = (Summary45?)null, Timing = (object?)null };

            var legacy = ExactReplay44(lab, g, Ride44(lab, g, motion, freeze.Fork, comparator, freeze.Finish));
            var trial = Certify45(lab, g, Ride45(lab, g, motion, freeze.Fork, freeze.Plan, freeze.Finish, exact: true));
            var score = Summarize45(lab, g, trial, freeze.Finish, SecondExit44(lab));
            SaveRun45(lab, g, output, $"holdout-{n + 1}", trial, score);
            var old = legacy.Motion;
            WriteEvidence39(lab, output, $"holdout-{n + 1}-H43", g, old.Seed, old.Tape, old.Flight);
            Console.WriteLine($"{h.Name}: new {score.Station:F2} m, H43 {old.Flight.Final.Distance:F2} m");
            return new { Seed = h, CarrierCpu = carrierCpu, Carrier = Score42(lab, g, parent, carrier),
                H43 = (Candidate44?)Measure44(lab, g, legacy, false), H44 = (Summary45?)score,
                Timing = (object?)Timing44(trial.Decisions) };
        }).ToArray();
        Save(Path.Combine(output, "holdouts.json"), new { freeze, Trials = trials });
    }
}
