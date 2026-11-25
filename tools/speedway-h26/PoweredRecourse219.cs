
using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record PulsePlan38(int Id, double From, double To, float Drive, int Posture);
    private sealed record PulseOutcome38(PulsePlan38 Settings, Outcome38 Result);
    private static readonly HeldSeed38[] RecourseHolds38 =
    [
        new("slower inside", -.57, -.08, .006),
        new("faster outside", .46, .10, .011),
        new("slower outside", -.68, .12, .005),
        new("faster inside", .62, -.11, .013)
    ];

    private static void PoweredRecourse38(Lab38 lab, string output, bool historyOnly = false)
    {
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var file = Path.Combine(lab.Root, "data/experiments/speedway218/stage-80.json");
        var parent = ReadPlain214<StageTrials37>(file).Outcomes.Single(x => x.Setup.Id == 30).Setup;
        var g = lab.Geometry(true);
        var origin = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216), 40 / 3.6, 0,
            lab.Fixture.Release + 1, lab.Fixture.Ratio, balanceRider: true);
        var seed = Place36(origin.State, lab.Bend.Entry - PreparationStart37, 8.20);
        var baseRun = RunH36(30, "H37 slide reference", 80, parent.Tune, g, lab.Engine(g), seed, lab.Bend,
            lab.Track, lab.Course, origin.State.Command.Bike.Drive, null, false, preparation: parent.Plan,
            startingMeters: PreparationStart37);
        if (!baseRun.Summary.Balanced) throw new InvalidOperationException("Powered reference did not complete.");
        var distances = baseRun.ReferenceSamples!.Select(r => r - lab.Bend.Entry).ToArray();
        var archive = new Archive38("H37-30", baseRun, distances, g, parent.Plan);
        var powered = baseRun.Trace.Zip(distances).Where(r => r.Second >= 0 && r.Second < lab.Bend.Length && Powered38(r.First)).ToArray();
        double established = powered.Length == 0 ? 35 : powered[powered.Length / 2].Second;
        // Three genuinely different phases. The drive injection and geometry
        // reserves are decided from the propagated state, not a pace sweep.
        (double From, double To)[] spans = [(0, 14), (Math.Max(15, established - 6), Math.Min(82, established + 10)), (74, 92)];
        int id = 0;
        var settings = (from span in spans
                        from drive in new[] { .25f, .4f, .55f, .75f, 1 }
                        from p in Enumerable.Range(0, 4)
                        select new PulsePlan38(++id, span.From, span.To, drive, p)).ToArray();
        var trials = new PulseOutcome38[settings.Length];
        var histories = new CommandHistory38[settings.Length];
        var runners = new List<(PulseOutcome38, Attempt38)>();
        int completed = 0;
        Parallel.For(0, settings.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) }, i =>
        {
            var p = settings[i];
            var (start, pre) = ForkAt38(archive, -22);
            var trial = PulseRun38(lab, archive, parent, p, g, start, pre, true, origin.State.Command.Bike.Drive);
            var summary = new PulseOutcome38(p, Summarize38(lab, g, trial));
            trials[i] = summary;
            histories[i] = new(summary.Result, trial.Seed, trial.Initial, trial.InitialCommands, trial.Commands);
            lock (runners)
            {
                runners.Add((summary, trial));
                runners.Sort((l, r) => Compare38(l.Item1.Result, r.Item1.Result));
                if (runners.Count > 3) runners.RemoveAt(runners.Count - 1);
            }
            int count = Interlocked.Increment(ref completed);
            if (count % 10 == 0) Console.WriteLine($"H38 powered checkpoint recourse: {count}/{settings.Length}.");
        });
        Save(Path.Combine(output, "recourse-commands.json.gz"), histories, true);
        if (historyOnly) return;

        var (result, winner) = runners[0];
        Save(Path.Combine(output, "recourse-freeze.json"), new
        {
            Settings = result.Settings, Training = result.Result, Reference = baseRun.Summary, Holds = RecourseHolds38,
            FrozenBeforeHoldouts = true
        });
        var replay = Replay38(lab, g, winner, H36Footprint, exact: true);
        WriteRun38(lab, output, "recourse-nominal", g, replay);
        var sensitivity = new List<object>();
        foreach (var (name, shape) in new[] { ("compact", Compact38), ("nominal", H36Footprint), ("large", Large38) })
        {
            var geometry = lab.Geometry(true, shape);
            var certification = name == "nominal" ? replay : Replay38(lab, geometry, winner, shape, exact: true);
            sensitivity.Add(new { Name = name, Shape = shape, Result = Summarize38(lab, geometry, certification) });
        }
        var hold = new Outcome38[RecourseHolds38.Length];
        Parallel.For(0, hold.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            var h = RecourseHolds38[i];
            var root = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216), (40 + h.SpeedKph) / 3.6,
                0, baseRun.Seed.Time, lab.Fixture.Ratio, balanceRider: true);
            var start = new Checkpoint38(Place36(root.State, lab.Bend.Entry - PreparationStart37, 8.20 + h.LaneMeters), -PreparationStart37);
            double forkTime = winner.Initial.State.Time;
            var prior = baseRun.Commands.Where(c => c.Requested < forkTime).Select(c => c with { Time = c.Time + h.QueueDelaySeconds }).ToArray();
            var first = ReplayTape38(lab, g, start, prior, forkTime);
            Attempt38 combined;
            if (first.Stop == "verified interval")
            {
                var continuation = PulseRun38(lab, archive, parent, result.Settings, g, first.Final, prior, false, origin.State.Command.Bike.Drive);
                combined = continuation with
                {
                    Seed = start, PrefixIncluded = true,
                    Trail = continuation.Trail with {
                        Steps = [.. first.Steps, .. continuation.Trail.Steps],
                        Distances = [.. first.Distances, .. continuation.Trail.Distances],
                        Boundary = Math.Min(first.Boundary, continuation.Trail.Boundary), Ground = Math.Min(first.Ground, continuation.Trail.Ground)
                    }
                };
            }
            else combined = new(winner.Plan, start, start, [], prior, first, 0, true);
            var verified = Replay38(lab, g, combined, H36Footprint, exact: true);
            hold[i] = Summarize38(lab, g, verified);
            WriteRun38(lab, output, $"recourse-holdout-{i + 1}", g, verified);
        });
        Save(Path.Combine(output, "recourse.json"), new
        {
            Trials = trials, Nominal = Summarize38(lab, g, replay), Sensitivity = sensitivity, Holdouts = hold,
            Reference = Measure38(baseRun.Trace.Where((s, i) => distances[i] >= 0 && distances[i] <= lab.Bend.Length).ToArray(),
                g, baseRun.Summary.Stop),
            ForecastSteps = trials.Sum(x => x.Result.ForecastSteps), ActualSteps = trials.Sum(x =>
                x.Result.ActualSteps - archive.At(-22) - 1), CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
            WallSeconds = timer.Elapsed.TotalSeconds
        });
        Console.WriteLine("H38 finished matched-window powered recourse and replay.");
    }

    private static Attempt38 PulseRun38(Lab38 lab, Archive38 archive, Candidate37 parent, PulsePlan38 plan, Geometry215 g,
        Checkpoint38 start, Activation215[] before, bool includePrefix, float steadyDrive)
    {
        var model = lab.Engine(g);
        var s = start;
        var queue = before;
        var states = new List<Step215>();
        var distances = new List<double>();
        if (includePrefix)
            for (int i = 0; i < archive.Run.Trace.Length && archive.Run.Trace[i].After.Time <= start.State.Time + 1e-10; i++)
            {
                states.Add(archive.Run.Trace[i]);
                distances.Add(archive.Distances[i]);
            }
        int forecast = 0;
        double ground = H36Footprint.GroundMargin(g, start.State), boundary = H36Footprint.Clearance(lab.Track, g, start.State, false);
        string stop = "verified interval";
        var seed = includePrefix ? new Checkpoint38(archive.Run.Seed, -PreparationStart37) : start;
        double stopAt = lab.Bend.Length + 20, end = s.State.Time + 13;
        double? straightLane = null;
        while (stop == "verified interval" && s.Distance < stopAt && s.State.Time < end - 1e-9)
        {
            var mark = lab.Course.Mark(s.State, g);
            var tune = parent.Tune;
            bool straight = s.Distance >= lab.Bend.Length + 8;
            if (straight)
            {
                straightLane ??= mark.Road.OffsetMeters - .5;
                tune = tune with { Lane = straightLane.Value, Lookahead = 23, RollGain = 17, Damping = 8, Fore = 0,
                    Side = 0, Vertical = 0, ApexPaceDropKph = 0, ApproachSpeedCorrectionKph = 0, Boot = 0 };
            }
            var guide = H36Control(s.State, g, model, mark, lab.Bend.Entry + s.Distance, lab.Bend, tune,
                straight ? 81 : parent.TargetKph, steadyDrive, queue, lab.Course, straight ? null : parent.Plan,
                rollReferenceScale: straight ? .6 : 1);
            forecast += guide.Steps;
            if (guide.Command is not { } c) { stop = guide.Stop ?? "powered recourse abstention"; break; }
            if (s.Distance >= plan.From && s.Distance <= plan.To)
            {
                var selected = PulseCommand38(lab, archive, g, model, s, queue, c, plan);
                forecast += selected.Steps;
                if (selected.Command is { } on) c = on;
            }
            queue = Submit38(s, queue, c);
            var flight = Window38(lab, g, model, s, queue, Cadence38, finishAt: stopAt);
            s = flight.Final;
            states.AddRange(flight.Steps);
            distances.AddRange(flight.Distances);
            ground = Math.Min(ground, flight.Ground);
            boundary = Math.Min(boundary, flight.Boundary);
            stop = flight.Stop;
        }
        if (stop == "verified interval")
            stop = s.Distance >= stopAt ? "twenty metres past exit" : "maneuver horizon";
        var nominal = new Plan38(plan.Id, archive.Id, DriveIntent38.AfterFormation, plan.Posture, -22);
        return new(nominal, seed, start, before, queue, new(s, states.ToArray(), distances.ToArray(), stop, boundary, ground),
            forecast, includePrefix);
    }

    private static ControlPick38 PulseCommand38(Lab38 lab, Archive38 archive, Geometry215 g, UnifiedModel215 model,
        Checkpoint38 now, Activation215[] previous, Command215 baseCommand, PulsePlan38 plan)
    {
        double desired = Math.Max(plan.Drive, baseCommand.Bike.Drive);
        var targets = plan.Posture switch
        {
            1 => baseCommand.Target + new Vec215(.045, 0, 0),
            2 => baseCommand.Target + new Vec215(.010, .020, -.035),
            3 => baseCommand.Target + new Vec215(.005, .045, -.005),
            _ => baseCommand.Target
        };
        double best = double.PositiveInfinity;
        int count = 0;
        Command215? selected = null;
        Flight38? bestPrediction = null;
        foreach (double offset in new[] { 0d, -.023, .023 })
        {
            var proposed = baseCommand with
            {
                Bike = new((float)Math.Clamp(baseCommand.Bike.SteeringRadians + offset,
                    -Steering217.MaximumSteeringRadians, Steering217.MaximumSteeringRadians), (float)desired),
                Target = targets,
                LeftFoot = plan.Posture == 3 ? Math.Min(.86, baseCommand.LeftFoot + .10) : baseCommand.LeftFoot
            };
            var prediction = Window38(lab, g, model, now, Submit38(now, previous, proposed), .162, forecast: true);
            count += prediction.Steps.Length;
            if (prediction.Stop != "verified interval" || prediction.Steps.Length == 0 || prediction.Ground < .007) continue;
            var last = prediction.Steps[^1];
            var reference = archive.Sample(prediction.Final.Distance);
            var end = prediction.Final.State;
            var motion = g.Motion(end);
            var target = archive.Geometry.Motion(reference.After);
            double score = 5 * Math.Pow(end.Pose.Roll - reference.After.Pose.Roll, 2) +
                .51 * Math.Pow(RollRate36(end, motion.BodyRate) - RollRate36(reference.After, target.BodyRate), 2) +
                .15 * Math.Pow((end.Angular.Z - reference.After.Angular.Z) / g.Base.YawCentral, 2) +
                .90 * Math.Pow(Difference38(Heading38(end), Heading38(reference.After)), 2) +
                .007 * Math.Pow(LateralAcceleration38(last, g.Base.Mass) - LateralAcceleration38(reference, g.Base.Mass), 2) +
                .025 * Math.Pow(Math.Max(0, 230 - last.Forces.Front.Normal) / 230, 2) -
                .020 * Math.Min(.30, Math.Max(0, last.Forces.RearSlip));
            if (score < best)
            {
                best = score;
                selected = proposed;
                bestPrediction = prediction;
            }
        }
        return new(selected, bestPrediction, count, best);
    }
}
