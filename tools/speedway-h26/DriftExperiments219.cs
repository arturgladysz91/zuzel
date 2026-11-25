
using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void PostureStudy38(Lab38 lab, string output, Archive38[] archives)
    {
        var jobs = (from archive in new[] { archives[0], archives[2] }
                    from lead in new[] { .30, .65, 1.0, 1.5 }
                    from posture in Enumerable.Range(0, 4)
                    from drive in new[] { .50f, .85f } select (archive, lead, posture, drive)).ToArray();
        const double initiation = -12;
        var rows = new object[jobs.Length];
        Parallel.For(0, jobs.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) }, i =>
        {
            var (archive, lead, posture, drive) = jobs[i];
            double when = archive.Sample(initiation).After.Time - lead;
            int index = Array.FindIndex(archive.Run.Trace, row => row.After.Time >= when);
            var root = archive.Run.Trace[index].After;
            double distance = archive.ActualDistance(index);
            var start = new Checkpoint38(root, distance);
            var pending = archive.Run.Commands.Where(x => x.Requested <= root.Time + 1e-10).ToArray();
            Vec215 target = BodyChoice38(posture);
            var g = archive.Geometry;
            var model = lab.Engine(g);
            var queue = Submit38(start, pending, root.Command with { Target = target, Bike = root.Command.Bike with { Drive = drive } });
            var trace = Window38(lab, g, model, start, queue, lead + .25);
            var measurements = Measure38(trace.Steps, g, trace.Stop);
            var end = trace.Final.State;
            var frameShift = root.Pose.Body(end.Center - root.Center - (root.Momentum / g.Base.Mass) * measurements.Seconds);
            var actuatorRelativeShift = end.Pose.Apply(end.Rider) * g.Base.RiderFraction -
                root.Pose.Apply(root.Rider) * g.Base.RiderFraction;
            rows[i] = new
            {
                Archive = archive.Id, LeadSeconds = lead, Posture = posture, Drive = drive, RequestedTarget = target,
                Seed = root, Commands = queue, DistanceAtStart = distance, DistanceAtEnd = trace.Final.Distance,
                EndRider = end.Rider, ActualRiderChange = end.Rider - root.Rider,
                RiderColocatedLeverChangeWorld = actuatorRelativeShift, ComVelocityReferenceDefectBody = frameShift,
                FrontAtEnd = trace.Steps.LastOrDefault()?.Forces.Front.Normal, Receipt = measurements,
                Series = trace.Steps.Select(x => new { x.After.Time, x.After.Rider, x.After.Center, x.Forces.Front.Normal,
                    x.Forces.Rear, x.Forces.PassiveSupport, ActiveForce = x.Forces.Rider.Norm, x.ActuatorPower }).ToArray()
            };
        });
        Save(Path.Combine(output, "posture.json.gz"), rows, true);
        Console.WriteLine($"H38 finished {rows.Length} paired preparation and posture trials.");
    }

    private sealed record MatchResult38(string Archive, double PhaseMeters, float Drive, Checkpoint38 Initial,
        Activation215[] Pending, Command215? Action, double LocalScore, int ForecastSteps, Receipt38? Local,
        double? Ground, double? Boundary, Outcome38? Continuation, Activation215[]? Commands);

    private static void MatchedStudy38(Lab38 lab, string output, Archive38[] archives)
    {
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var jobs = (from archive in new[] { archives[0], archives[2] }
                    from phase in new[] { -17d, 0, 14, 48, 87 }
                    from drive in Drives38 select (archive, phase, drive)).ToArray();
        var matches = new MatchResult38[jobs.Length];
        Parallel.For(0, jobs.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) }, i =>
        {
            var (archive, phase, drive) = jobs[i];
            var g = archive.Geometry;
            var (start, pending) = ForkAt38(archive, phase);
            var local = Choose38(lab, archive, g, lab.Engine(g), start, pending, drive, default, true);
            if (local.Command is not { } selected || local.Prediction is not { } forecast)
            {
                matches[i] = new(archive.Id, phase, drive, start, pending, null, 0, local.Steps, null, null, null, null, null);
                return;
            }
            var plan = new Plan38(i + 1, archive.Id, DriveIntent38.Archive, 0, phase);
            var continued = Continue38(lab, archive, g, plan, start, pending, includePrefix: true, first: selected);
            var executed = continued.Trail.Steps.Where(x => x.Before.Time >= start.State.Time - 1e-10 &&
                x.After.Time <= start.State.Time + .24 + 1e-10).ToArray();
            matches[i] = new(archive.Id, phase, drive, start, pending, selected, local.Score, local.Steps,
                Measure38(executed, g, executed.Length == 0 ? continued.Trail.Stop : "executed local window"),
                forecast.Ground, forecast.Boundary, Summarize38(lab, g, continued), continued.Commands);
        });
        Save(Path.Combine(output, "matched.json.gz"), new
        {
            Matches = matches, CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
            WallSeconds = timer.Elapsed.TotalSeconds,
            ForecastSteps = matches.Sum(x => x.ForecastSteps + (x.Continuation?.ForecastSteps ?? 0)),
            ActualContinuationSteps = matches.Sum(x => (x.Continuation?.ActualSteps ?? 0) -
                (x.Continuation is null ? 0 : archives.Single(a => a.Id == x.Archive).At(x.PhaseMeters) + 1))
        }, true);
        Console.WriteLine($"H38 completed {matches.Length} matched Drive requests, with {matches.Count(x => x.Continuation?.BalancedBend == true)} balanced continuations.");
    }

    private sealed record ReplayResult38(string Name, Outcome38 Result, double FinalTimeDifference,
        double FinalPositionDifference, double FinalMomentumDifference);
    private sealed record HeldSeed38(string Name, double SpeedKph, double LaneMeters, double QueueDelaySeconds);
    private static readonly HeldSeed38[] Holdouts38 =
    [
        new("slower inside", -.53, -.13, .009),
        new("faster outside", .71, .16, .012),
        new("slower outside", -.82, .19, .004),
        new("faster inside", .48, -.17, .015)
    ];

    private static void WholeStudy38(Lab38 lab, string output, Archive38[] archives, bool historyOnly = false)
    {
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        int id = 0;
        var plans = (from source in archives
                     from drive in new[] { DriveIntent38.EntryPulse, DriveIntent38.Distributed, DriveIntent38.SlipRegulated,
                         DriveIntent38.AfterFormation, DriveIntent38.ExitPower }
                     from posture in Enumerable.Range(0, 4)
                     select new Plan38(++id, source.Id, drive, posture)).ToArray();
        var attempts = new Outcome38[plans.Length];
        var histories = new CommandHistory38[plans.Length];
        var retained = new List<(Outcome38 Result, Attempt38 Attempt)>();
        int completed = 0;
        Parallel.For(0, plans.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) }, i =>
        {
            var p = plans[i];
            var a = archives.Single(x => x.Id == p.Archive);
            var (start, pre) = ForkAt38(a, p.ForkMeters);
            var run = Continue38(lab, a, a.Geometry, p, start, pre, true);
            var result = Summarize38(lab, a.Geometry, run);
            attempts[i] = result;
            histories[i] = new(result, run.Seed, run.Initial, run.InitialCommands, run.Commands);
            lock (retained)
            {
                retained.Add((result, run));
                retained.Sort((left, right) => Compare38(left.Result, right.Result));
                if (retained.Count > 3) retained.RemoveAt(retained.Count - 1);
            }
            int count = Interlocked.Increment(ref completed);
            if (count % 10 == 0) Console.WriteLine($"H38 whole maneuver: {count}/{plans.Length} proposals.");
        });
        Save(Path.Combine(output, "whole-commands.json.gz"), histories, true);
        if (historyOnly) return;

        var winner = retained[0];
        var archive = archives.Single(x => x.Id == winner.Result.Plan.Archive);
        Save(Path.Combine(output, "policy-freeze.json"), new
        {
            winner.Result.Plan, Training = winner.Result,
            Criterion = "Stable 20 m and longest powered slide >= 0.15 s; then balanced + powered; then full balance; then progress. Within a qualifying class prefer corner time.",
            PoweredDefinition = "Rear load >20 N, abs vehicle sideslip >=6 degrees, positive rear normalized spin >=0.06, active Drive >=0.25.",
            FreshHoldouts = Holdouts38, FrozenBeforeHoldoutExecution = true, NativeWorlds = 0
        });
        var replay = Replay38(lab, archive.Geometry, winner.Attempt, H36Footprint, exact: true);
        WriteRun38(lab, output, "whole-training-replay", archive.Geometry, replay);

        var sensitivity = new List<object>();
        foreach (var (name, shape) in new[] { ("compact", Compact38), ("nominal", H36Footprint), ("large", Large38) })
        {
            var g = lab.Geometry(archive.Geometry.Leg != null, shape);
            var candidate = name == "nominal" ? replay : Replay38(lab, g, winner.Attempt, shape, exact: true);
            sensitivity.Add(new { Name = name, Envelope = shape, Result = Summarize38(lab, g, candidate),
                Final = candidate.Trail.Final.State, Commands = candidate.Commands.Length });
        }

        var held = new Outcome38[Holdouts38.Length];
        Parallel.For(0, Holdouts38.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            var h = Holdouts38[i];
            var a = archive;
            var g = a.Geometry;
            var engine = lab.Engine(g);
            double startDistance = a.Run.Summary.Start.Reference - lab.Bend.Entry;
            var eq = SteadyCircle215(engine, g, lab.Fixture.Rest(g, NominalSupport216), (40 + h.SpeedKph) / 3.6,
                0, a.Run.Seed.Time, lab.Fixture.Ratio, balanceRider: true);
            var seed = new Checkpoint38(Place36(eq.State, lab.Bend.Entry + startDistance, a.Run.Summary.Start.Offset + h.LaneMeters), startDistance);
            var prefix = a.Run.Commands.Where(c => c.Requested < winner.Attempt.Initial.State.Time)
                .Select(c => c with { Time = c.Time + h.QueueDelaySeconds }).ToArray();
            var first = ReplayTape38(lab, g, seed, prefix, winner.Attempt.Initial.State.Time, exact: false);
            if (first.Stop != "verified interval")
            {
                var failed = new Attempt38(winner.Result.Plan, seed, seed, [], prefix, first, 0, true);
                held[i] = Summarize38(lab, g, failed);
                WriteRun38(lab, output, $"holdout-{i + 1}", g, failed);
                return;
            }
            var continuation = Continue38(lab, a, g, winner.Result.Plan, first.Final, prefix, false);
            var combined = continuation with
            {
                Seed = seed, PrefixIncluded = true,
                Trail = continuation.Trail with {
                    Steps = [.. first.Steps, .. continuation.Trail.Steps], Distances = [.. first.Distances, .. continuation.Trail.Distances],
                    Boundary = Math.Min(first.Boundary, continuation.Trail.Boundary), Ground = Math.Min(first.Ground, continuation.Trail.Ground)
                }
            };
            var verified = Replay38(lab, g, combined, H36Footprint, exact: true);
            held[i] = Summarize38(lab, g, verified);
            WriteRun38(lab, output, $"holdout-{i + 1}", g, verified);
        });

        var legacyPowered = Study218PoweredWitness38(lab, output);
        Save(Path.Combine(output, "whole.json"), new
        {
            Proposals = attempts, Nominal = Summarize38(lab, archive.Geometry, replay), Sensitivity = sensitivity, Holdouts = held,
            ReferencePoweredWitness = legacyPowered, NativeWorlds = 0, Replay = new ReplayResult38("uninterrupted",
                Summarize38(lab, archive.Geometry, replay), replay.Trail.Final.State.Time - winner.Attempt.Trail.Final.State.Time,
                (replay.Trail.Final.State.Center - winner.Attempt.Trail.Final.State.Center).Norm,
                (replay.Trail.Final.State.Momentum - winner.Attempt.Trail.Final.State.Momentum).Norm),
            ForecastSteps = attempts.Sum(x => x.ForecastSteps), ActualTrainingSteps = attempts.Sum(x =>
                x.ActualSteps - archives.Single(a => a.Id == x.Plan.Archive).At(x.Plan.ForkMeters) - 1),
            CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds, WallSeconds = timer.Elapsed.TotalSeconds
        });
        Console.WriteLine($"H38 finished {plans.Length} whole-maneuver proposals and four frozen-policy holdouts.");
    }

    private static int Compare38(Outcome38 x, Outcome38 y)
    {
        int Class(Outcome38 r) => r.StableTwenty && r.InBend.LongestPowered >= .15 ? 4 :
            r.BalancedBend && r.InBend.LongestPowered >= .15 ? 3 : r.BalancedBend ? 2 : 1;
        int order = Class(y).CompareTo(Class(x));
        if (order != 0) return order;
        if (Class(x) == 1)
        {
            order = y.Distance.CompareTo(x.Distance);
            if (order != 0) return order;
        }
        else
        {
            order = Nullable.Compare(x.Time, y.Time);
            if (order != 0) return order;
        }
        return x.Plan.Id.CompareTo(y.Plan.Id);
    }

    private static Attempt38 Replay38(Lab38 lab, Geometry215 g, Attempt38 attempt, SpatialBike36 envelope, bool exact)
    {
        var flight = ReplayTape38(lab, g, attempt.Seed, attempt.Commands, attempt.Trail.Final.State.Time,
            envelope, exact, lab.Bend.Length + 20);
        return attempt with { Trail = flight with { Stop = flight.Final.Distance >= lab.Bend.Length + 20
            ? "twenty metres past exit" : flight.Stop } };
    }

    private static Flight38 ReplayTape38(Lab38 lab, Geometry215 g, Checkpoint38 seed, Activation215[] tape,
        double until, SpatialBike36? shape = null, bool exact = false, double endDistance = double.PositiveInfinity)
    {
        shape ??= H36Footprint;
        var engine = lab.Engine(g);
        var state = seed;
        var all = new List<Step215>();
        var distances = new List<double>();
        double boundary = shape.Clearance(lab.Track, g, seed.State, exact);
        double ground = shape.GroundMargin(g, seed.State);
        string stop = "verified interval";
        foreach (double split in tape.Select(x => x.Requested).Where(t => t > seed.State.Time + 1e-10 && t < until - 1e-10).Append(until))
        {
            if (state.Distance >= endDistance) break;
            if (split - state.State.Time <= 1e-9) continue;
            var flight = Window38(lab, g, engine, state, tape, split - state.State.Time, shape, exact, finishAt: endDistance);
            all.AddRange(flight.Steps);
            distances.AddRange(flight.Distances);
            state = flight.Final;
            boundary = Math.Min(boundary, flight.Boundary);
            ground = Math.Min(ground, flight.Ground);
            stop = flight.Stop;
            if (stop != "verified interval") break;
        }
        return new(state, all.ToArray(), distances.ToArray(), stop, boundary, ground);
    }

    private static Outcome38 Study218PoweredWitness38(Lab38 lab, string output)
    {
        string path = Path.Combine(lab.Root, "data/experiments/speedway218/stage-80.json");
        var setup = ReadPlain214<StageTrials37>(path).Outcomes.First(o => o.Setup.Id == 30).Setup;
        var g = lab.Geometry(setup.Tune.Leg);
        var initial = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216), 40 / 3.6, 0,
            lab.Fixture.Release + 1, lab.Fixture.Ratio, balanceRider: true);
        double d = -PreparationStart37;
        var seed = Place36(initial.State, lab.Bend.Entry + d, 8.2);
        var bend = RunH36(30, "H37 powered comparison", 80, setup.Tune, g, lab.Engine(g), seed, lab.Bend, lab.Track, lab.Course,
            initial.State.Command.Bike.Drive, null, false, preparation: setup.Plan, startingMeters: PreparationStart37);
        var distances = bend.ReferenceSamples!.Select(x => x - lab.Bend.Entry).ToArray();
        var a = new Archive38("H37-30", bend, distances, g, setup.Plan);
        var origin = new Checkpoint38(seed, d);
        var last = new Checkpoint38(bend.Trace[^1].After, distances[^1]);
        var plan = new Plan38(30, a.Id, DriveIntent38.Archive, 0, last.Distance);
        Attempt38 trace;
        if (bend.Summary.Balanced)
        {
            var tail = Continue38(lab, a, g, plan, last, bend.Commands, true);
            trace = Replay38(lab, g, tail, H36Footprint, exact: true);
        }
        else
        {
            var section = new Flight38(last, bend.Trace, distances, bend.Summary.Stop, bend.Summary.MinimumSweep, bend.Summary.GroundMargin);
            trace = new(plan, origin, origin, [], bend.Commands, section, bend.Summary.ForecastSteps, true);
        }
        WriteRun38(lab, output, "reference-powered", g, trace);
        return Summarize38(lab, g, trace);
    }
}
