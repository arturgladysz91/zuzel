
using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private enum Throttle39 { Full, Partial, Release, Ramp }
    private enum Recovery39 { Latch, Momentum, Room }
    private sealed record Plan39(int Id, double Start, double ReleaseAt, double RecoverAt, Throttle39 Throttle,
        Recovery39 Objective, double StraightDrive, int Posture, bool RetainWitness, double? FrontCoastMeters = null);
    private sealed record Score39(int Id, string Stop, int Steps, int ForecastSteps, double DecisionElapsedSeconds,
        int Decisions, double LastMeters, bool Bend, bool Plus20Gate, bool LaterGate, bool NextReachable,
        double? EntryKph, double? BendSeconds, double? MinimumKph, double? ExitKph, double? TwentyKph,
        double? FortyKph, double? SixtyKph, double? NextEntryKph, double? LaterKph, double? TwentyRoll,
        double? TwentyYawRate, double Track, double Hard, double LoadedSideslip, double Powered25, double Powered50,
        double Powered85, double FullSlide, double LongestFullSlide, double StraightFull, double EngineWork,
        double Dissipation, double EnergyDefect, double PeakMuscleForce, double PeakMusclePower);
    private sealed record Trial39(Plan39 Plan, Checkpoint38 Seed, Checkpoint38 Fork, Activation215[] Prefix,
        Activation215[] Commands, Flight38 Flight, int ForecastSteps, int Decisions, long DecisionTicks);
    private sealed record Option39(Command215? Command, int Steps, double Score);

    private static Plan39[] Grid39()
    {
        int id = 0;
        var retained = from d in new[] { 86d, 90.0, 94.5, 98.0, 103.0 }
                       from objective in Enum.GetValues<Recovery39>()
                       from drive in new[] { .66, 1.0 }
                       select new Plan39(++id, 74, d, d, Throttle39.Full, objective, drive, (int)objective, true);
        var windows = from onset in new[] { 55d, 64, 74, 83, 90 }
                      from style in Enum.GetValues<Throttle39>()
                      from arrival in new[] { 91.0, 95.0 }
                      let objective = (Recovery39)(((int)style + (int)onset) % 3)
                      select new Plan39(++id, onset, Math.Min(94, onset + (onset < 65 ? 18 : 15)), arrival,
                          style, objective, style == Throttle39.Partial ? .85 : 1, (int)style % 3, false);
        return retained.Concat(windows).ToArray();
    }

    private static Plan39[] SettleGrid39()
    {
        int id = 100;
        return (from fork in new[] { 92.91, 94.5, 97.2 }
                from coast in new[] { 5d, 9, 13 }
                from objective in Enum.GetValues<Recovery39>()
                select new Plan39(++id, 74, fork, fork, Throttle39.Full, objective, .75, (int)objective, true, coast))
            .ToArray();
    }

    private static void SearchExit39(Lab38 lab, string output, Witnesses39 cases, bool settle = false)
    {
        using var process = Process.GetCurrentProcess();
        var clock = Stopwatch.StartNew();
        var cpu = process.TotalProcessorTime;
        var seed57 = cases.Attempts.Single(r => r.Plan.Id == 57);
        var g = cases.Reference.Geometry;
        var settings = settle ? SettleGrid39() : Grid39();
        var measured = new Score39[settings.Length];
        var history = new object[settings.Length];
        var top = new List<(Trial39 Trial, Score39 Score)>();
        int checkedCount = 0;
        Parallel.For(0, settings.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) }, i =>
        {
            var plan = settings[i];
            var fork = ExitFork39(plan, seed57, cases);
            var run = RunExit39(lab, cases, g, plan, fork.Root, fork.Initial, fork.Pending, fork.Prefix, fork.Distances);
            var score = Measure39(lab, g, run);
            measured[i] = score;
            history[i] = new { Plan = plan, run.Seed, run.Fork, EarlierCommands = run.Prefix, run.Commands, Score = score };
            lock (top)
            {
                top.Add((run, score));
                top.Sort((a, b) => Rank39(a.Score, b.Score));
                if (top.Count > 6) top.RemoveAt(top.Count - 1);
            }
            if (Interlocked.Increment(ref checkedCount) % 10 == 0) Console.WriteLine($"H39 exit search {checkedCount}/{settings.Length}.");
        });
        Save(Path.Combine(output, "exit-search.json"), new
        {
            Proposals = settings.Zip(measured).Select(x => new { Plan = x.First, Score = x.Second }),
            CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds, WallSeconds = clock.Elapsed.TotalSeconds,
            ForecastSteps = measured.Sum(x => x.ForecastSteps), AcceptedStepsIncludingReusedPrefix = measured.Sum(x => x.Steps),
            OnlineDecisionElapsedSeconds = measured.Sum(x => x.DecisionElapsedSeconds)
        });
        Save(Path.Combine(output, "exit-command-histories.json.gz"), history, true);

        var verified = new List<(Trial39 Trial, Score39 Score)>();
        foreach (var (trial, score) in top)
        {
            var r = VerifyTrial39(lab, g, trial, H36Footprint);
            var s = Measure39(lab, g, r);
            verified.Add((r, s));
            if (s.LaterGate && s.Plus20Gate && s.NextReachable && s.LongestFullSlide >= 1.0724) break;
        }
        var best = verified.OrderBy(x => x.Score, Comparer<Score39>.Create(Rank39)).First();
        var frozen = best.Trial.Plan;
        var holds = new[] { new HeldSeed38("inner/slower", -.42, -.075, .004),
            new HeldSeed38("outer/faster", .32, .080, .006), new HeldSeed38("outer/slower", -.27, .11, .008),
            new HeldSeed38("inner/faster", .21, -.10, .010) };
        if (settle)
            holds = [new("inner/slower", -.36, -.050, .003), new("outer/faster", .29, .064, .005),
                new("outer/slower", -.19, .095, .007), new("inner/faster", .17, -.080, .009)];
        Save(Path.Combine(output, "freeze.json"), new { Selected = frozen, Nominal = best.Score, Holdouts = holds,
            Freeze = "Selection uses only training results, before all holdouts", Checked = verified.Select(x => x.Score) });
        WriteEvidence39(lab, output, "selected", g, best.Trial.Seed, best.Trial.Commands, best.Trial.Flight);
        var sensitivity = new List<object>();
        foreach (var (name, envelope) in new[] { ("compact", Compact38), ("nominal", H36Footprint), ("large", Large38) })
        {
            var gg = lab.Geometry(true, envelope);
            var r = name == "nominal" ? best.Trial : VerifyTrial39(lab, gg, best.Trial, envelope);
            sensitivity.Add(new { Name = name, Outcome = Measure39(lab, gg, r) });
        }

        var fresh = new Score39[holds.Length];
        Parallel.For(0, holds.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            var h = holds[i];
            var parent = ExitFork39(frozen, seed57, cases);
            var b = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216), (40 + h.SpeedKph) / 3.6, 0,
                parent.Root.State.Time, lab.Fixture.Ratio, balanceRider: true);
            if (b.Residual > .005) throw new InvalidOperationException("Infeasible perturbation equilibrium.");
            var offset = lab.Course.Mark(parent.Root.State, g).Road.OffsetMeters + h.LaneMeters;
            var seed = new Checkpoint38(Place36(b.State, lab.Bend.Entry + parent.Root.Distance, offset), parent.Root.Distance);
            var history = parent.Pending.Where(c => c.Requested < parent.Initial.State.Time - 1e-10)
                .Select(c => c with { Time = c.Time + h.QueueDelaySeconds }).ToArray();
            var first = ReplayTape38(lab, g, seed, history, parent.Initial.State.Time);
            Trial39 candidate;
            if (first.Stop == "verified interval")
                candidate = RunExit39(lab, cases, g, frozen, seed, first.Final, history, first.Steps, first.Distances);
            else
                candidate = new(frozen, seed, seed, [], history, first, 0, 0, 0);
            var exact = VerifyTrial39(lab, g, candidate, H36Footprint);
            fresh[i] = Measure39(lab, g, exact);
            WriteEvidence39(lab, output, $"holdout-{i + 1}", g, exact.Seed, exact.Commands, exact.Flight);
        });
        Save(Path.Combine(output, "qualification.json"), new
        {
            Selected = frozen, Nominal = best.Score, Sensitivity = sensitivity, Holdout = fresh, NativeWorlds = 0,
            NextEntryReference = lab.Bend.NextEntry - lab.Bend.Entry, TotalCpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
            WallSeconds = clock.Elapsed.TotalSeconds
        });
        var onlineFork = ExitFork39(frozen, seed57, cases);
        var online = Stopwatch.StartNew();
        var onlineCpu = process.TotalProcessorTime;
        var onlineRun = RunExit39(lab, cases, g, frozen, onlineFork.Root, onlineFork.Initial,
            onlineFork.Pending, onlineFork.Prefix, onlineFork.Distances);
        Save(Path.Combine(output, "online-cost.json"), new
        {
            WholeRaceMeasured = false, DecisionCadenceSeconds = Cadence38, NativeWorlds = 0,
            IntegrationAndDecisionCpuSeconds = (process.TotalProcessorTime - onlineCpu).TotalSeconds,
            WallSeconds = online.Elapsed.TotalSeconds, Run = Measure39(lab, g, onlineRun)
        });
    }

    private sealed record StoredExitTape39(Plan39 Plan, Checkpoint38 Seed, Checkpoint38 Fork,
        Activation215[] EarlierCommands, Activation215[] Commands, Score39 Score);

    private static void VerifyPreservedSlide39(Lab38 lab, string output)
    {
        string path = Path.Combine(output, "exit-command-histories.json.gz");
        if (!File.Exists(path)) return;
        var all = Read199<StoredExitTape39[]>(path);
        var best = all.Where(a => a.Score.Bend && a.Score.LongestFullSlide >= 1.0724)
            .OrderByDescending(a => a.Score.LastMeters).ThenBy(a => a.Plan.Id).FirstOrDefault();
        if (best is null) return;
        var g = lab.Geometry(true);
        var f = ReplayTape38(lab, g, best.Seed, best.Commands, best.Commands[^1].Requested + Cadence38,
            exact: true, endDistance: best.Score.LastMeters - 1e-8);
        var verified = new Trial39(best.Plan, best.Seed, best.Fork, best.EarlierCommands, best.Commands, f, 0, 0, 0);
        Save(Path.Combine(output, "full-slide-frontier.json"),
            new { best.Plan, Search = best.Score, Verified = Measure39(lab, g, verified) });
        WriteEvidence39(lab, output, "full-slide-frontier", g, best.Seed, best.Commands, f);
    }

    private sealed record Fork39(Checkpoint38 Root, Checkpoint38 Initial, Activation215[] Pending, Step215[] Prefix, double[] Distances);

    private static Fork39 ExitFork39(Plan39 plan, Attempt38 witness, Witnesses39 cases)
    {
        if (plan.RetainWitness)
        {
            int i = Array.FindIndex(witness.Trail.Distances, x => x >= plan.RecoverAt);
            var root = new Checkpoint38(witness.Trail.Steps[i].After, witness.Trail.Distances[i]);
            return new(witness.Seed, root, witness.Commands.Where(q => q.Requested < root.State.Time - 1e-10).ToArray(),
                witness.Trail.Steps[..(i + 1)], witness.Trail.Distances[..(i + 1)]);
        }
        else
        {
            var source = cases.Attempts.Single(x => x.Plan.Id == 24);
            int i = Array.FindIndex(source.Trail.Distances, x => x >= plan.Start);
            var root = new Checkpoint38(source.Trail.Steps[i].After, source.Trail.Distances[i]);
            return new(source.Seed, root, source.Commands.Where(q => q.Requested < root.State.Time - 1e-10).ToArray(),
                source.Trail.Steps[..(i + 1)], source.Trail.Distances[..(i + 1)]);
        }
    }

    private static Trial39 RunExit39(Lab38 lab, Witnesses39? f, Geometry215 g, Plan39 plan, Checkpoint38 seed,
        Checkpoint38 initial, Activation215[] pending, Step215[] prefix, double[] prefixDistances)
    {
        var model = lab.Engine(g);
        var state = initial;
        var commands = pending;
        var steps = new List<Step215>(prefix);
        var distances = new List<double>(prefixDistances);
        double edge = H36Footprint.Clearance(lab.Track, g, state.State, false);
        double hard = H36Footprint.GroundMargin(g, state.State);
        double end = lab.Bend.NextEntry - lab.Bend.Entry + 4;
        double endTime = state.State.Time + 12;
        double lane = Lane39(lab, g, state, plan.Objective);
        int forecast = 0, decisions = 0;
        long decisionTicks = 0;
        string stop = "verified interval";
        while (state.Distance < end && state.State.Time < endTime && stop == "verified interval")
        {
            long begin = Stopwatch.GetTimestamp();
            Option39 next;
            if (state.Distance < plan.RecoverAt && f is not null)
                next = BendDrive39(lab, f, g, model, plan, state, commands);
            else
            {
                float drive = StraightDrive39(lab, g, plan, state);
                next = ChooseRecovery39(lab, g, model, state, commands, drive, lane, plan.Objective, plan.Posture,
                    plan.FrontCoastMeters is not null);
            }
            decisionTicks += Stopwatch.GetTimestamp() - begin;
            decisions++;
            forecast += next.Steps;
            if (next.Command is not { } c) { stop = "no feasible proposed recovery"; break; }
            commands = Submit38(state, commands, c);
            var window = Window38(lab, g, model, state, commands, Cadence38, finishAt: end);
            state = window.Final;
            steps.AddRange(window.Steps);
            distances.AddRange(window.Distances);
            edge = Math.Min(edge, window.Boundary);
            hard = Math.Min(hard, window.Ground);
            stop = window.Stop;
        }
        if (stop == "verified interval") stop = state.Distance >= end ? "next-bend preparation reached" : "horizon exhausted";
        return new(plan, seed, initial, pending, commands, new(state, steps.ToArray(), distances.ToArray(), stop, edge, hard),
            forecast, decisions, decisionTicks);
    }

    private static Option39 BendDrive39(Lab38 lab, Witnesses39 f, Geometry215 g, UnifiedModel215 model, Plan39 plan,
        Checkpoint38 s, Activation215[] commands)
    {
        var mark = lab.Course.Mark(s.State, g);
        var guide = H36Control(s.State, g, model, mark, lab.Bend.Entry + s.Distance, lab.Bend, f.Parent.Tune,
            f.Parent.TargetKph, f.EquilibriumDrive, commands, lab.Course, f.Parent.Plan);
        if (guide.Command is not { } baseline)
            return ChooseRecovery39(lab, g, model, s, commands, .18f, mark.Road.OffsetMeters, plan.Objective, plan.Posture);
        double spin = ContactSlip39(g, s.State);
        double progress = Math.Max(0, s.Distance - plan.Start);
        float drive = s.Distance >= plan.ReleaseAt ? .22f : plan.Throttle switch
        {
            Throttle39.Full => 1,
            Throttle39.Partial => (float)Math.Clamp(.74 + .6 * (.14 - spin), .54, .85),
            Throttle39.Release => progress > 4 && progress < 7 ? .20f : 1,
            _ => (float)Math.Clamp(.42 + .145 * progress, .42, 1)
        };
        var q = baseline with { Bike = baseline.Bike with { Drive = Math.Max(drive, baseline.Bike.Drive) },
            Target = baseline.Target + ExitPosture39(plan.Posture, .5),
            LeftFoot = Math.Max(0, baseline.LeftFoot - .035 * Math.Max(0, s.Distance - 84)) };
        int steps = guide.Steps;
        foreach (double steerDelta in new[] { 0d, .017, -.017 })
        {
            var candidate = q with { Bike = q.Bike with
                { SteeringRadians = Steering39(q.Bike.SteeringRadians + steerDelta) } };
            var pred = Window38(lab, g, model, s, Submit38(s, commands, candidate), .13, forecast: true);
            steps += pred.Steps.Length;
            if (pred.Stop == "verified interval" && pred.Ground >= .006)
                return new(candidate, steps, 0);
        }
        return new(baseline, steps, 0);
    }

    private static Option39 ChooseRecovery39(Lab38 lab, Geometry215 g, UnifiedModel215 model, Checkpoint38 s,
        Activation215[] queue, float drive, double lane, Recovery39 objective, int posture, bool settle = false)
    {
        double begin = lab.Bend.Length;
        double remaining = lab.Bend.NextEntry - lab.Bend.Entry - s.Distance;
        bool next = remaining <= 11;
        var mark = lab.Course.Mark(s.State, g);
        double speed = s.State.Momentum.Plane.Norm / g.Base.Mass;
        var pose = s.State.Pose;
        double p = RollRate36(s.State, g.Motion(s.State).BodyRate);
        double spin = ContactSlip39(g, s.State);
        var (kp, damping) = objective switch
        {
            Recovery39.Latch => (17d, 8d),
            Recovery39.Momentum => (24d, 15d),
            _ => (16d, 11d)
        };
        var look = objective == Recovery39.Room ? 24d : 19d;
        var tune = new Tune36(lane, look, kp, damping, 0, 0, 0, 0, 0, true);
        var visit = next ? Map199.At(2, 1) : lab.Bend;
        float measuredDrive = (float)Math.Clamp(drive - .6 * Math.Max(0, spin - .24), 0, 1);
        double blend = Math.Clamp((begin + 12 - s.Distance) / 17, 0, 1);
        var rhs = ExitPosture39(posture, blend);
        double q = Math.Clamp(s.State.Command.LeftFoot * Math.Clamp((begin + 9 - s.Distance) / 13, 0, .87), 0, 1);
        Command215? projected = settle ? new Command215(new(0, measuredDrive), rhs) { LeftFoot = q } : null;
        var guide = H36Control(s.State, g, model, mark, lab.Bend.Entry + s.Distance, visit, tune,
            90, .13f, queue, lab.Course, rollReferenceScale: next ? .75 : .60, commandBasis: projected);
        double desiredLean = next ? -.16 : settle
            ? -.78 * Math.Clamp((begin + 8 - s.Distance) / 12, 0, 1)
            : s.Distance < begin ? -.24 : 0;
        double upright = s.State.Steer + .075 * (-p + 4 * (desiredLean - pose.Roll));
        var knobs = new double[] { guide.Command?.Bike.SteeringRadians ?? upright, upright,
            (guide.Command?.Bike.SteeringRadians ?? upright) + Math.CopySign(.029, -p), s.State.Steer * .44 };
        int count = guide.Steps;
        var best = new Option39(null, 0, double.PositiveInfinity);
        // The order preserves the full-Drive experiment when its steering choices
        // verify. Lower Drive is a contingency only when no requested action verifies.
        foreach (var throttle in new[] { measuredDrive, Math.Min(measuredDrive, .35f), Math.Min(measuredDrive, .12f) }.Distinct())
        {
            foreach (double steer in knobs.Distinct())
            {
                var input = new Command215(new(Steering39(steer), throttle), rhs) { LeftFoot = q };
                var f = Window38(lab, g, model, s, Submit38(s, queue, input), .18, forecast: true);
                count += f.Steps.Length;
                if (f.Stop != "verified interval" || f.Steps.Length == 0) continue;
                var last = f.Final.State;
                var finish = lab.Course.Mark(last, g);
                var w = RollRate36(last, g.Motion(last).BodyRate);
                var aim = Frame.At(lab.Bend.Entry + f.Final.Distance + look, lane).Position;
                var v = aim - finish.Position;
                var heading = Math.Atan2(v.Y, v.X);
                double yaw = Difference38(Heading38(last), heading);
                double slip = SlipAngle38(last);
                var force = f.Steps[^1].Forces;
                double unbalanced = last.Pose.Roll - desiredLean + .19 * w;
                double cost = (objective == Recovery39.Momentum ? 7 : 4.1) * unbalanced * unbalanced +
                    .50 * w * w + 1.3 * yaw * yaw + .36 * slip * slip + .34 * finish.AngularVelocity * finish.AngularVelocity +
                    .09 * Math.Pow(Math.Max(0, 280 - force.Front.Normal) / 280, 2) +
                    (objective == Recovery39.Room ? .32 : .11) * Math.Pow(Math.Max(0, 1.0 - f.Boundary) / .5, 2) +
                    .12 * Math.Pow(Math.Max(0, .020 - f.Ground) / .020, 2) -
                    .013 * (last.Momentum.Plane.Norm / g.Base.Mass - speed);
                if (settle)
                {
                    double noseRise = Math.Max(0, last.Pose.Pitch - s.State.Pose.Pitch);
                    cost += 5 * noseRise * noseRise + .32 * Math.Pow(Math.Max(0, 320 - force.Front.Normal) / 320, 2);
                }
                if (cost < best.Score) best = new(input, 0, cost);
            }
            if (best.Command is not null) break;
        }
        return best with { Steps = count };
    }

    private static float StraightDrive39(Lab38 lab, Geometry215 g, Plan39 plan, Checkpoint38 state)
    {
        double x = state.Distance - lab.Bend.Length;
        double untilNext = lab.Bend.NextEntry - lab.Bend.Entry - state.Distance;
        var v = g.Motion(state.State);
        double speed = Speed38(state.State, g);
        double yaw = Math.Abs(SlipAngle38(state.State));
        double roll = Math.Abs(state.State.Pose.Roll);
        double rate = Math.Abs(RollRate36(state.State, v.BodyRate));
        double stable = Math.Clamp(1 - .48 * roll - .33 * rate - .8 * yaw, .25, 1);
        double exitBlend = Math.Clamp((x + 3) / 15, 0, 1);
        double ready = plan.StraightDrive * (.56 + .44 * exitBlend) * stable;
        if (x >= 20 && roll < .14 && rate < .32 && yaw < .07) ready = plan.StraightDrive;
        // A subsequent bend needs measurable front support and yaw authority.
        // Reduce the request based on actual remaining distance, never set speed.
        if (untilNext < 17)
            ready = Math.Min(ready, Math.Clamp(.17 + .013 * Math.Max(0, untilNext) - .014 * Math.Max(0, speed - 84), .10, .42));
        if (plan.FrontCoastMeters is { } coast)
        {
            if (x < coast) return 0;
            var front = g.Contact(state.State, v, true, 0);
            if (front.Gap > .003 || front.NormalSpeed > .20) ready = Math.Min(ready, .12);
        }
        return (float)Math.Clamp(ready, 0, 1);
    }

    private static double Lane39(Lab38 lab, Geometry215 g, Checkpoint38 s, Recovery39 objective)
    {
        double lane = lab.Course.Mark(s.State, g).Road.OffsetMeters;
        return Math.Clamp(lane + (objective == Recovery39.Room ? -.65 : objective == Recovery39.Momentum ? -.25 : 0), 2.5, 9.1);
    }

    private static Vec215 ExitPosture39(int index, double scale) => index switch
    {
        1 => new(.035 * scale, .027 * scale, -.018 * scale),
        2 => new(.015 * scale, -.013 * scale, -.042 * scale),
        _ => default
    };

    private static float Steering39(double x) => (float)Math.Clamp(x,
        -Steering217.MaximumSteeringRadians, Steering217.MaximumSteeringRadians);
}
