using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record ExactCandidate42(Policy42 Policy, Score40 Score, double ConcurrentSeconds);
    private sealed record Selection42(Policy42 Baseline, Policy42 Selected, ExactCandidate42[] Exact);
    private sealed record Input42(Witnesses39 Base, Attempt38 Reference, PulsePlan38 Prefix, Plan39 Carrier);

    private static Input42 MatchInputs42(Lab38 lab)
    {
        var book = Reconstruct39(lab);
        var parent = book.Attempts.Single(x => x.Plan.Id == 24);
        var pulse = ReadPlain214<RecourseArchive39>(Path.Combine(lab.Root, "data/experiments/speedway219/recourse.json"))
            .Trials.Single(x => x.Settings.Id == 24).Settings;
        var carrier = Read199<StraightArchive39>(Path.Combine(lab.Root, "data/experiments/speedway220/straight-experiment.json.gz"))
            .Comparisons.Single(x => x.Plan.Id == 12).Plan;
        return new(book, parent, pulse, carrier);
    }

    private static Fork40 FirstArc42(Lab38 lab, Geometry215 g, Input42 book, HeldSeed38 h,
        Action<LegacyStep46>? observed = null)
    {
        var rest = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216), (40 + h.SpeedKph) / 3.6, 0,
            book.Reference.Seed.State.Time, lab.Fixture.Ratio, balanceRider: true);
        if (rest.Residual > .005) throw new InvalidOperationException("H41 seed equilibrium failed.");
        var seed = new Checkpoint38(Place36(rest.State, lab.Bend.Entry + book.Reference.Seed.Distance, 8.2 + h.LaneMeters),
            book.Reference.Seed.Distance);
        var prior = book.Reference.InitialCommands.Select(x => x with { Time = x.Time + h.QueueDelaySeconds }).ToArray();
        var prefix = ReplayTape38(lab, g, seed, prior, book.Reference.Initial.State.Time, observed: observed);
        if (prefix.Stop != "verified interval") return new(h.Name, seed, prefix, prior);
        var arc = PulseRun38(lab, book.Base.Reference, book.Base.Parent, book.Prefix, g, prefix.Final, prior, false,
            book.Base.EquilibriumDrive, observed);
        return new(h.Name, seed, arc.Trail with { Steps = [.. prefix.Steps, .. arc.Trail.Steps],
            Distances = [.. prefix.Distances, .. arc.Trail.Distances],
            Boundary = Math.Min(prefix.Boundary, arc.Trail.Boundary), Ground = Math.Min(prefix.Ground, arc.Trail.Ground) },
            arc.Commands);
    }

    private static Run40 Adaptive42(Lab38 lab, Geometry215 g, Fork40 arc, Plan39 carrier, Policy42 policy,
        double? end = null, VerifiedCache42? cache = null, bool keepPoweredExit = false,
        Action<LegacyStep46>? observed = null)
    {
        double station = keepPoweredExit ? lab.Bend.Length + 20 : carrier.Start;
        if (arc.Flight.Final.Distance < station)
            return new(policy.Line, arc.Seed, arc.Flight.Final, [], arc.Tape, arc.Flight, []);
        var start = Slice42(arc, station);
        double activate = lab.Bend.NextEntry - lab.Bend.Entry - policy.Line.PreparationMeters;
        if (start.At.Distance >= activate)
            return Ride42(lab, g, arc.Seed, start.At, start.Prefix, start.Distances, start.Commands, policy, end, cache, observed);
        var drive = RunExit39(lab, null, g, carrier, arc.Seed, start.At, start.Commands, start.Prefix, start.Distances,
            finishOverride: activate, observed: observed);
        var f = drive.Flight;
        if (f.Final.Distance < activate)
            return new(policy.Line, arc.Seed, start.At, start.Commands, drive.Commands, f, []);
        return Ride42(lab, g, arc.Seed, f.Final, f.Steps, f.Distances, drive.Commands, policy, end, cache, observed);
    }

    private static Score40 Score42(Lab38 lab, Geometry215 geometry, Run40 r, Policy42 policy) =>
        MeasureLink40(lab, geometry, r) with { Id = policy.Id };

    private static void Qualify42(Lab38 lab, string output)
    {
        string selection = Path.Combine(output, "race-selection.json");
        var old = ReadPlain214<Selection42>(selection);
        var input = MatchInputs42(lab);
        var g = lab.Geometry(true);
        var center = new HeldSeed38("nominal feedback", 0, 0, 0);
        var arc = FirstArc42(lab, g, input, center);
        var candidates = old.Exact.Where(x => x.Score.Linked).Select(x => x.Policy).Take(4)
            .Append(Baseline42()).DistinctBy(p => p.Id).ToArray();
        var fitting = candidates.AsParallel().WithDegreeOfParallelism(Math.Min(5, candidates.Length)).Select(p =>
        {
            var r = ExactLink40(lab, g, Adaptive42(lab, g, arc, input.Carrier, p));
            return (Plan: p, Run: r, Score: Score42(lab, g, r, p));
        }).OrderBy(x => x.Score, Comparer<Score40>.Create(CompareLink40)).ToArray();
        var best = fitting[0];
        HeldSeed38[] fresh = [new("inside lower", -.28, -.049, .0035), new("outside faster", .25, .049, .0065),
            new("outside lower", -.18, .077, .0095), new("inside faster", .30, -.080, .0105)];
        Save(Path.Combine(output, "freeze.json"), new { Policy = best.Plan, Holdouts = fresh,
            Training = fitting.Select(x => new { x.Plan, x.Score }), NativeWorlds = 0 });
        WriteEvidence39(lab, output, "nominal", g, best.Run.Root, best.Run.Commands, best.Run.Flight);

        var heldArcs = new Fork40[fresh.Length];
        var held = new Score40[fresh.Length];
        Parallel.For(0, fresh.Length, i =>
        {
            var first = FirstArc42(lab, g, input, fresh[i]);
            heldArcs[i] = first;
            var r = ExactLink40(lab, g, Adaptive42(lab, g, first, input.Carrier, best.Plan));
            held[i] = Score42(lab, g, r, best.Plan);
            WriteEvidence39(lab, output, $"holdout-{i + 1}", g, r.Root, r.Commands, r.Flight);
        });
        var geometry = new[] { ("compact", Compact38), ("nominal", H36Footprint), ("large", Large38) }
            .AsParallel().Select(p =>
            {
                var shape = lab.Geometry(true, p.Item2);
                var run = p.Item1 == "nominal" ? best.Run : ExactLink40(lab, shape, best.Run, p.Item2);
                return new { Name = p.Item1, Score = Score42(lab, shape, run, best.Plan) };
            }).OrderBy(x => x.Name).ToArray();
        Save(Path.Combine(output, "qualification.json"), new { Policy = best.Plan, Nominal = best.Score,
            Holdouts = held, Geometry = geometry, NativeWorlds = 0 });

        if (best.Score.Linked)
        {
            double lap = Frame.LapMeters;
            var longRun = ExactLink40(lab, g, Adaptive42(lab, g, arc, input.Carrier, best.Plan, 2 * lap + 22));
            var r = longRun.Flight;
            var gates = new[] { 0, lap, 2 * lap, 2 * lap + 22 }.Select(d =>
            {
                int i = Array.FindIndex(r.Distances, x => x >= d);
                return new { Station = d, Time = i < 0 ? (double?)null : r.Steps[i].After.Time,
                    Speed = i < 0 ? (double?)null : Speed38(r.Steps[i].After, g) };
            }).ToArray();
            Save(Path.Combine(output, "lap.json"), new { Policy = best.Plan, LapMeters = lap, Gates = gates,
                Last = r.Final, r.Stop, r.Boundary, r.Ground, NativeWorlds = 0 });
            WriteEvidence39(lab, output, "flying", g, longRun.Root, longRun.Commands, r);
        }
        AuditTiming42(lab, output, g, arc, heldArcs[0], input.Carrier, best.Plan);
        Powered42(lab, output, g, input, arc, heldArcs, best.Plan);
    }

    private static void AuditTiming42(Lab38 lab, string output, Geometry215 g, Fork40 nominal, Fork40 holdout,
        Plan39 carrier, Policy42 policy)
    {
        using var process = Process.GetCurrentProcess();
        var r = Timed(nominal, null, "spatial feedback", out var naked);
        var warm = MakeCache42(lab, g, policy, r);
        Timed(nominal, warm, "verified station cache", out var cached);
        Timed(holdout, warm, "fresh state and FIFO fallback", out var perturbed);
        var legacy = WarmStart40(r);
        var a = warm.Acts[Math.Min(12, warm.Acts.Length - 1)];
        var parentQueue = r.Commands.Take(a.State.Next + a.Pending.Length).ToArray();
        bool Modern(State215 s, Activation215[] q, Geometry215 geometry, Forces215? contacts) =>
            MatchCache42(lab, geometry, new(s, a.Station), contacts, q, policy, warm) is not null;
        bool Old(State215 s) => CachedDecision40(g, new(s, a.Station), a.Contact, legacy) is not null;
        var yaw = a.State.Pose.Yaw;
        var p = a.State.Pose.Pitch;
        var l = a.State.Pose.Roll;
        var timeSlip = parentQueue.Length == a.State.Next
            ? [.. parentQueue, new Activation215(a.State.Time - .025, a.State.Time + .025, a.State.Command)]
            : parentQueue.Select((t, i) => i < a.State.Next ? t : t with { Time = t.Time + .004 }).ToArray();
        var alternateContact = a.Contact is { } f ? f with { Front = f.Front with { Normal = f.Front.Normal * .55 } } : null;
        var controls = new[] {
            ("identical state", a.State, parentQueue, g, a.Contact),
            ("yaw", a.State with { Pose = Attitude215.Pose(yaw + .04, p, l) }, parentQueue, g, a.Contact),
            ("pitch", a.State with { Pose = Attitude215.Pose(yaw, p + .04, l) }, parentQueue, g, a.Contact),
            ("linear momentum", a.State with { Momentum = a.State.Momentum + new Vec215(12, 0, -7) }, parentQueue, g, a.Contact),
            ("angular momentum", a.State with { Angular = a.State.Angular + Vec215.Up * .3 }, parentQueue, g, a.Contact),
            ("crank momentum", a.State with { Crank = a.State.Crank + .7 }, parentQueue, g, a.Contact),
            ("rear momentum", a.State with { Wheel = a.State.Wheel + .5 }, parentQueue, g, a.Contact),
            ("rider momentum", a.State with { RiderMomentum = a.State.RiderMomentum + Vec215.Left * 4 }, parentQueue, g, a.Contact),
            ("leg momentum", a.State with { Leg = a.State.Leg with { Momentum = a.State.Leg.Momentum + .15 } }, parentQueue, g, a.Contact),
            ("active command", a.State with { Command = a.State.Command with { Bike = a.State.Command.Bike with { Drive = .57f } } }, parentQueue, g, a.Contact),
            ("pending FIFO activation", a.State, timeSlip, g, a.Contact),
            ("changed tyre normal", a.State, parentQueue, g, alternateContact),
            ("different chassis geometry", a.State, parentQueue, lab.Geometry(true, Compact38), a.Contact),
            ("different front contact model", a.State, parentQueue, Geometry221(lab, new(.45)), a.Contact)
        };
        Save(Path.Combine(output, "runtime.json"), new { Trials = new[] { naked, cached, perturbed },
            CacheChecks = controls.Select(x => new { Change = x.Item1, LegacyAccepted = Old(x.Item2),
                StateFifoContactAccepted = Modern(x.Item2, x.Item3, x.Item4, x.Item5) }),
            PhysicsReactionSeconds = ReactionSeconds216, CadenceSeconds = Cadence38, TargetDeadlineMilliseconds = 50,
            CompleteRaceMeasured = false });

        Run40 Timed(Fork40 source, VerifiedCache42? table, string label, out object result)
        {
            var elapsed = Stopwatch.StartNew();
            var cpu = process.TotalProcessorTime;
            var run = Adaptive42(lab, g, source, carrier, policy, cache: table);
            double spent = (process.TotalProcessorTime - cpu).TotalSeconds;
            result = new { Name = label, CpuSeconds = spent, WallSeconds = elapsed.Elapsed.TotalSeconds,
                Score = Score42(lab, g, run, policy), Count = run.Decisions.Length,
                Cached = run.Decisions.Count(x => x.ModelSteps == 0), DeadlineMisses = run.Decisions.Count(x => x.Milliseconds > 50),
                CumulativeDecisionMilliseconds = run.Decisions.Sum(x => x.Milliseconds) };
            return run;
        }
    }

    private static void Powered42(Lab38 lab, string output, Geometry215 g, Input42 input, Fork40 nominal,
        Fork40[] holds, Policy42 policy)
    {
        int id = 700;
        var settings = (from at in new[] { 16d, 46, 55, 64, 73, 81, 91, 98 }
                        from style in new[] { Pulse40.Short, Pulse40.SlipLimited }
                        select new Power40(++id, at, style == Pulse40.Short ? 15 : 11, style, 250, .14, false)).ToArray();
        var r = settings.AsParallel().WithDegreeOfParallelism(8).Select(p =>
        {
            var ride = Test(nominal, p);
            return new { Setup = p, ride.Fork, ride.Root, ride.EarlierCommands, ride.Commands,
                Score = Score42(lab, g, ride, policy) with { Id = p.Id }, Run = ride };
        }).OrderBy(t => t.Score, Comparer<Score40>.Create(CompareLink40)).ToArray();
        Save(Path.Combine(output, "powered-search.json.gz"), r.Select(t => new
        { t.Setup, t.Score, t.Root, t.Fork, t.EarlierCommands, t.Commands }), true);
        var chosen = r.First();
        var n = ExactLink40(lab, g, chosen.Run);
        var score = Score42(lab, g, n, policy) with { Id = chosen.Setup.Id };
        Save(Path.Combine(output, "powered-freeze.json"), new { chosen.Setup, Reference = policy, Nominal = score });
        WriteEvidence39(lab, output, "powered-nominal", g, n.Root, n.Commands, n.Flight);
        var held = new Score40[holds.Length];
        Parallel.For(0, holds.Length, i =>
        {
            var h = ExactLink40(lab, g, Test(holds[i], chosen.Setup));
            held[i] = Score42(lab, g, h, policy) with { Id = chosen.Setup.Id };
            WriteEvidence39(lab, output, $"powered-holdout-{i + 1}", g, h.Root, h.Commands, h.Flight);
        });
        Save(Path.Combine(output, "powered-qualification.json"), new { chosen.Setup, Nominal = score, Holdouts = held });

        Run40 Test(Fork40 first, Power40 p)
        {
            if (first.Flight.Final.Distance < p.Onset)
                return new(policy.Line, first.Seed, first.Flight.Final, [], first.Tape, first.Flight, []);
            var arc = PowerPrefix40(lab, input.Base, g, first.Seed, first.Tape, first.Flight, p, policy.Line);
            return Adaptive42(lab, g, new Fork40("powered held prefix", arc.Root, arc.Flight, arc.Commands),
                input.Carrier, policy, keepPoweredExit: true);
        }
    }
}
