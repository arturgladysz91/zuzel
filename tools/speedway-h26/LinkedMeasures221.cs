using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static Score40 MeasureLink40(Lab38 lab, Geometry215 g, Run40 trial)
    {
        var b = Map199.At(2, 1);
        double firstExit = lab.Bend.Length, nextEntry = b.Entry - lab.Bend.Entry, nextExit = b.Exit - lab.Bend.Entry;
        var f = trial.Flight;
        var old = Measure39(lab, g, new(new(0, 0, 0, 0, Throttle39.Partial, Recovery39.Room, 0, 0, false),
            trial.Root, trial.Fork, trial.EarlierCommands, trial.Commands, f, 0, 0, 0));
        var a = At(0);
        var first8 = At(firstExit + 8);
        var e2 = At(nextExit);
        var second8 = At(nextExit + 8);
        var second20 = At(nextExit + 20);
        var beforeNext = At(nextEntry - Math.Max(4, trial.Setup.PreparationMeters));
        var straight = new List<(double Speed, double Meters, double Time, float Drive)>();
        double engine = 0, loss = 0, budget = 0, defect = 0, capacityForce = 0, capacityPower = 0;
        double previous = trial.Root.Distance;
        foreach (var (p, meters) in f.Steps.Zip(f.Distances))
        {
            double dt = Duration215(p);
            if (meters > firstExit && meters <= nextEntry)
                straight.Add((Speed38(p.After, g), Math.Max(0, meters - previous), dt, p.Before.Command.Bike.Drive));
            previous = meters;
            engine += p.Forces.EnginePower * dt;
            loss += p.Forces.Loss * dt;
            budget += p.EnergyResidual;
            defect = Math.Max(defect, Math.Abs(budget));
            capacityForce = Math.Max(capacityForce, p.Forces.Rider.Norm + Math.Abs(p.Forces.LeftFoot?.MuscleForce ?? 0));
            capacityPower = Math.Max(capacityPower, p.Forces.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(p.ActuatorPower));
        }
        double peak = straight.Select(x => x.Speed).DefaultIfEmpty().Max();
        double t = straight.Sum(x => x.Time);
        bool accelerated = first8 is not null && beforeNext is not null && peak >= Speed38(first8.After, g) + 1 &&
            (trial.Setup.Direct || beforeNext.After.Time >= first8.After.Time);
        var final = f.Steps.LastOrDefault();
        var ending = final is null ? [] : f.Steps.Where(x => x.After.Time >= final.After.Time - .25).ToArray();
        bool recovered = e2 is not null && second8 is not null && second20 is not null && ending.Length > 0 &&
            ending.All(x => Math.Abs(x.After.Pose.Roll) <= .22 &&
                Math.Abs(lab.Course.Mark(x.After, g).AngularVelocity) <= .28) &&
            Speed38(second20.After, g) >= Speed38(second8.After, g) + .5;
        bool linked = a is not null && old.Bend && accelerated && recovered &&
            f.Boundary >= .50 && f.Ground > 0 && f.Final.Distance >= nextExit + 21.99;
        var times = trial.Decisions.Select(x => x.Milliseconds).Order().ToArray();
        string category = f.Stop switch
        {
            "completed linked horizon" => linked ? "linked pass" : "linked terminal balance or acceleration",
            var s when s.Contains("candidate exhaustion") => "controller candidate exhaustion",
            var s when s.Contains("hard-part") || s.Contains("ground") => "hard-part clearance",
            var s when s.Contains("boundary") => "boundary clearance",
            var s when s.Contains("orientation envelope") || s.Contains("travel envelope") => "state envelope",
            var s when s.Contains("quadrature") || s.Contains("converge") || s.Contains("subdivision") => "numerical certification",
            _ => f.Stop
        };
        return new(trial.Setup.Id, f.Stop, category, f.Final.Distance, old.Plus20Gate, old.LaterGate, accelerated,
            e2 is not null, recovered, linked, a is null || final is null ? null : final.After.Time - a.After.Time,
            Bend(0, firstExit), Bend(nextEntry, nextExit), peak, t == 0 ? 0 : straight.Sum(x => x.Speed * x.Time) / t,
            straight.Where(x => x.Drive >= .999f).Sum(x => x.Time), t == 0 ? 0 : straight.Sum(x => x.Drive * x.Time) / t,
            new[] { 100, 110, 115, 120 }.Select(level => new SpeedBand40(level,
                straight.Where(x => x.Speed >= level).Sum(x => x.Time),
                straight.Where(x => x.Speed >= level).Sum(x => x.Meters))).ToArray(),
            f.Boundary, f.Ground, capacityForce, capacityPower, engine, loss, defect, f.Steps.Length,
            trial.Decisions.Sum(x => x.ModelSteps), Q(.50), Q(.95), Q(.99), Q(1));

        Step215? At(double distance)
        {
            int i = Array.FindIndex(f.Distances, x => x >= distance);
            return i < 0 ? null : f.Steps[i];
        }

        BendStats40 Bend(double begin, double end)
        {
            var start = At(begin);
            var finish = At(end);
            double loaded = 0, p25 = 0, p50 = 0, p85 = 0, full = 0, air = 0;
            double minimum = double.PositiveInfinity;
            foreach (var (x, d) in f.Steps.Zip(f.Distances))
            {
                if (d < begin || d > end) continue;
                double dt = Duration215(x);
                minimum = Math.Min(minimum, Speed38(x.After, g));
                if (x.Forces.Front.Normal <= 1) air += dt;
                if (!Slide38(x)) continue;
                loaded += dt;
                if (x.Forces.RearSlip < PoweredSlip38) continue;
                float drive = x.Before.Command.Bike.Drive;
                if (drive >= .25f) p25 += dt;
                if (drive >= .50f) p50 += dt;
                if (drive >= .85f) p85 += dt;
                if (drive >= .999f) full += dt;
            }
            return new(start is null || finish is null ? null : finish.After.Time - start.After.Time,
                Sp(start), double.IsFinite(minimum) ? minimum : null, Sp(At((begin + end) / 2)), Sp(finish),
                loaded, p25, p50, p85, full, air);
        }

        double? Sp(Step215? step) => step is null ? null : Speed38(step.After, g);
        double Q(double p) => times.Length == 0 ? 0 : times[Math.Clamp((int)Math.Ceiling(p * times.Length) - 1, 0, times.Length - 1)];
    }

    private static int CompareLink40(Score40 x, Score40 y)
    {
        int rank = y.Linked.CompareTo(x.Linked);
        if (rank != 0) return rank;
        if (x.Linked)
        {
            rank = Nullable.Compare(x.TotalSeconds, y.TotalSeconds);
            if (rank != 0) return rank;
        }
        rank = y.SecondRecovery.CompareTo(x.SecondRecovery);
        if (rank != 0) return rank;
        rank = y.LastStation.CompareTo(x.LastStation);
        return rank == 0 ? x.Id.CompareTo(y.Id) : rank;
    }

    private static void ValidateLinkPolicy40(Lab38 lab, string output, Setup40 setup, Book40 book, Run40 reference,
        Score40 baseline, bool refined)
    {
        var g = lab.Geometry(true);
        var next = Map199.At(2, 1);
        var source = setup.Tape == "fast" ? book.Fast : book.Nominal;
        double d = next.Entry - lab.Bend.Entry - setup.PreparationMeters;
        using var proc = Process.GetCurrentProcess();
        var cleanCosts = new object[3];
        foreach (bool cheap in new[] { false, true })
        {
            var cpu = proc.TotalProcessorTime;
            var clock = Stopwatch.StartNew();
            var run = FromLink40(lab, g, source, setup with { Cheap = cheap }, d);
            cleanCosts[cheap ? 1 : 0] = new { Cheap = cheap, CpuSeconds = (proc.TotalProcessorTime - cpu).TotalSeconds,
                WallSeconds = clock.Elapsed.TotalSeconds, Score = MeasureLink40(lab, g, run),
                CadenceSeconds = Cadence38, ReactionSeconds = ReactionSeconds216, CompleteRaceMeasured = false };
        }
        var verifiedCache = WarmStart40(reference);
        var warmCpu = proc.TotalProcessorTime;
        var warmClock = Stopwatch.StartNew();
        var cached = FromLink40(lab, g, source, setup, d, verifiedCache);
        double warmWall = warmClock.Elapsed.TotalSeconds;
        double warmSeconds = (proc.TotalProcessorTime - warmCpu).TotalSeconds;
        var cachedExact = ExactLink40(lab, g, cached);
        cleanCosts[2] = new { Mode = "warm verified horizon", CpuSeconds = warmSeconds, WallSeconds = warmWall,
            Score = MeasureLink40(lab, g, cachedExact), CacheHits = cached.Decisions.Count(x => x.ModelSteps == 0),
            CadenceSeconds = Cadence38, ReactionSeconds = ReactionSeconds216, CompleteRaceMeasured = false };
        Save(Path.Combine(output, "online-cost.json"), cleanCosts);
        var geometryChecks = new List<object>();
        foreach (var (name, envelope) in new[] { ("compact", Compact38), ("nominal", H36Footprint), ("large", Large38) })
        {
            var shape = lab.Geometry(true, envelope);
            var r = name == "nominal" ? reference : ExactLink40(lab, shape, reference, envelope);
            geometryChecks.Add(new { Name = name, Score = MeasureLink40(lab, shape, r) });
        }

        var holdouts = refined ? new[] { new HeldSeed38("inside trim", -.22, -.058, .004),
            new HeldSeed38("outside faster", .27, .072, .007), new HeldSeed38("outside slower", -.31, .087, .009),
            new HeldSeed38("inside faster", .20, -.093, .011) } : new[] { new HeldSeed38("inside slower", -.29, -.049, .005),
            new HeldSeed38("outside faster", .33, .054, .008),
            new HeldSeed38("outside slower", -.19, .074, .010),
            new HeldSeed38("inside faster", .24, -.081, .012) };
        Save(Path.Combine(output, "link-freeze.json"), new { Policy = setup, NominalTraining = baseline,
            Holdouts = holdouts, PrefixPolicy = "H38 feedback from approach -22 m; H40 feedback from +20 m" });

        var cases = Reconstruct39(lab);
        var archived = cases.Attempts.Single(x => x.Plan.Id == 24);
        var input = ReadPlain214<RecourseArchive39>(Path.Combine(lab.Root, "data/experiments/speedway219/recourse.json"))
            .Trials.Single(x => x.Settings.Id == 24).Settings;
        var c = new Score40[holdouts.Length + 1];
        Parallel.For(0, c.Length, new ParallelOptions { MaxDegreeOfParallelism = 5 }, i =>
        {
            var h = i == 0 ? new HeldSeed38("adaptive nominal", 0, 0, 0) : holdouts[i - 1];
            var real = RunAdaptiveLink40(lab, g, cases, archived, input, h, setup);
            var verified = ExactLink40(lab, g, real);
            c[i] = MeasureLink40(lab, g, verified);
            WriteEvidence39(lab, output, i == 0 ? "linked-adaptive" : $"linked-holdout-{i}", g, verified.Root,
                verified.Commands, verified.Flight);
        });
        Save(Path.Combine(output, "link-qualification.json"), new { Setup = setup, ExactNominal = baseline,
            Geometry = geometryChecks, AdaptiveNominal = c[0], Holdouts = c[1..], NativeWorlds = 0 });
    }

    private static Run40 RunAdaptiveLink40(Lab38 lab, Geometry215 g, Witnesses39 cases, Attempt38 archived,
        PulsePlan38 plan, HeldSeed38 h, Setup40 setup, Plan39? carrier = null)
    {
        var rested = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216),
            (40 + h.SpeedKph) / 3.6, 0, archived.Seed.State.Time, lab.Fixture.Ratio, balanceRider: true);
        if (rested.Residual > .005) throw new InvalidOperationException("Unreachable linked approach perturbation.");
        var seed = new Checkpoint38(Place36(rested.State, lab.Bend.Entry + archived.Seed.Distance, 8.2 + h.LaneMeters),
            archived.Seed.Distance);
        var pending = archived.InitialCommands.Select(q => q with { Time = q.Time + h.QueueDelaySeconds }).ToArray();
        var incoming = ReplayTape38(lab, g, seed, pending, archived.Initial.State.Time);
        if (incoming.Stop != "verified interval")
            return new(setup, seed, incoming.Final, pending, pending, incoming, []);
        var arc = PulseRun38(lab, cases.Reference, cases.Parent, plan, g, incoming.Final, pending, false, cases.EquilibriumDrive);
        var arcSteps = incoming.Steps.Concat(arc.Trail.Steps).ToArray();
        var arcDistance = incoming.Distances.Concat(arc.Trail.Distances).ToArray();
        if (carrier is not null && arc.Trail.Final.Distance >= carrier.Start)
        {
            int split = Array.FindIndex(arcDistance, d => d >= carrier.Start);
            var fastStart = new Checkpoint38(arcSteps[split].After, arcDistance[split]);
            var old = arc.Commands.Where(q => q.Requested < fastStart.State.Time - 1e-10).ToArray();
            double finish = lab.Bend.NextEntry - lab.Bend.Entry - setup.PreparationMeters;
            var advanced = RunExit39(lab, null, g, carrier, seed, fastStart, old,
                arcSteps[..(split + 1)], arcDistance[..(split + 1)], finishOverride: finish);
            if (advanced.Flight.Final.Distance < finish)
                return new(setup, seed, fastStart, old, advanced.Commands, advanced.Flight, []);
            return RunLink40(lab, g, seed, advanced.Flight.Final, advanced.Commands,
                advanced.Flight.Steps, advanced.Flight.Distances, setup);
        }
        if (arc.Trail.Final.Distance >= lab.Bend.Length + 20)
            return RunLink40(lab, g, seed, arc.Trail.Final, arc.Commands, arcSteps, arcDistance, setup);
        return new(setup, seed, arc.Trail.Final, pending, arc.Commands,
            arc.Trail with { Steps = arcSteps, Distances = arcDistance, Boundary = Math.Min(incoming.Boundary, arc.Trail.Boundary),
                Ground = Math.Min(incoming.Ground, arc.Trail.Ground) }, []);
    }
}
