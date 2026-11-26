
using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void WriteOriginalStraight39(Lab38 lab, string output)
    {
        string source = Path.Combine(lab.Root, "data/experiments/speedway219");
        var bend = Read199<FullLog38>(Path.Combine(source, "recourse-nominal.json.gz"));
        var tail = Read199<FullLog38>(Path.Combine(source, "qualified-straight-nominal.json.gz"));
        var g = lab.Geometry(true);
        var root = new Checkpoint38(bend.OriginalSeed, -PreparationStart37);
        var replay = ReplayTape38(lab, g, root, tail.Commands, tail.States[^1].State.Time, exact: true);
        WriteEvidence39(lab, output, "original-straight", g, root, tail.Commands, replay);
        var late = replay.Steps.Zip(replay.Distances).Where(x => x.Second >= lab.Bend.Length + 20)
            .Select(x => x.First).ToArray();
        double duration = late.Sum(Duration215);
        double Mean(Func<Step215, double> watts) => late.Sum(x => Duration215(x) * watts(x)) / duration;
        Save(Path.Combine(output, "straight-loss-ledger.json"), new
        {
            Seconds = duration,
            EngineGrossWatts = Mean(x => x.Forces.EnginePower),
            EngineLossWatts = Mean(x => x.Forces.EngineLoss),
            ClutchTransmissionLossWatts = Mean(x => x.Forces.TransmissionLoss),
            BearingLossWatts = Mean(x => x.Forces.BearingLoss),
            FrontScrubWatts = Mean(x => -x.Forces.Front.Force.Plane.Dot(x.Forces.Front.Patch.Velocity.Plane)),
            RearLateralSlipWatts = Mean(x => -x.Forces.Rear.Lateral * x.Forces.Rear.Patch.Velocity.Dot(x.Forces.Rear.Patch.Side)),
            RearLongitudinalSlipWatts = Mean(x => x.Forces.Rear.Longitudinal *
                (g.Base.Radius * (x.Midpoint.Wheel / g.Base.WheelInertia - g.Motion(x.Midpoint).BodyRate.Y) -
                    x.Forces.Rear.Patch.Velocity.Dot(x.Forces.Rear.Patch.Forward))),
            DragWatts = Mean(x => -x.Forces.Drag.Dot(x.Midpoint.Momentum / g.Base.Mass)),
            SteeringActuatorWatts = Mean(x => x.Forces.SteeringPower),
            ContactAndDragWatts = Mean(x => x.Forces.SlideLoss)
        });
    }

    private sealed record StraightTape39(Plan39 Plan, Checkpoint38 Matched,
        Activation215[] PriorCommands, Activation215[] Commands, Score39 Score);
    private sealed record StraightArchive39(StraightTape39[] Comparisons, Plan39 SelectedPlan);

    private static void VerifyFastestStraight39(Lab38 lab, string output)
    {
        string path = Path.Combine(output, "straight-experiment.json");
        if (!File.Exists(path)) return;
        var saved = ReadPlain214<StraightArchive39>(path);
        var ranked = saved.Comparisons.Where(x => x.Score.NextReachable && x.Score.StraightFull > 0)
            .OrderByDescending(x => x.Score.NextEntryKph).ThenBy(x => x.Plan.Id);
        var fastest = ranked.FirstOrDefault();
        if (fastest is null) return;
        var old = Read199<FullLog38>(Path.Combine(lab.Root, "data/experiments/speedway219/recourse-nominal.json.gz"));
        var g = lab.Geometry(true);
        var seed = new Checkpoint38(old.OriginalSeed, -PreparationStart37);
        double finish = lab.Bend.NextEntry - lab.Bend.Entry + 4;
        var f = ReplayTape38(lab, g, seed, fastest.Commands, fastest.Commands[^1].Requested + Cadence38,
            exact: true, endDistance: finish);
        if (f.Final.Distance >= finish) f = f with { Stop = "next-bend preparation reached" };
        var trial = new Trial39(fastest.Plan, seed, fastest.Matched, fastest.PriorCommands, fastest.Commands, f, 0, 0, 0);
        Save(Path.Combine(output, "straight-full-diagnostic.json"),
            new { fastest.Plan, Forecasted = fastest.Score, Replayed = Measure39(lab, g, trial) });
        WriteEvidence39(lab, output, "straight-full-diagnostic", g, seed, fastest.Commands, f);
    }

    private static void StraightExperiments39(Lab38 lab, string output)
    {
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        string source = Path.Combine(lab.Root, "data/experiments/speedway219");
        var bend = Read199<FullLog38>(Path.Combine(source, "recourse-nominal.json.gz"));
        var tail = Read199<FullLog38>(Path.Combine(source, "qualified-straight-nominal.json.gz"));
        var g = lab.Geometry(true);
        var root = new Checkpoint38(bend.OriginalSeed, -PreparationStart37);
        var original = ReplayTape38(lab, g, root, tail.Commands, tail.States[^1].State.Time);
        var old = new Trial39(new(0, 0, 0, 0, Throttle39.Partial, Recovery39.Latch, .2, 0, false),
            root, root, [], tail.Commands, original, 0, 0, 0);
        var ordinary = original.Steps.Zip(original.Distances).Where(x => x.Second >= lab.Bend.Length + 20).ToArray();
        double seconds = ordinary.Sum(r => Duration215(r.First));
        double Weighted(Func<Step215, double> sample) => ordinary.Sum(x => sample(x.First) * Duration215(x.First)) / seconds;
        var plateau = new
        {
            AverageDrive = Weighted(x => x.Before.Command.Bike.Drive), Rpm = Weighted(x => x.Forces.EngineRpm),
            EnginePower = Weighted(x => x.Forces.EnginePower), Torque = Weighted(x => x.Forces.NetEngineTorque),
            EngineLoss = Weighted(x => x.Forces.EngineLoss), TransmissionLoss = Weighted(x => x.Forces.TransmissionLoss),
            FrontNormal = Weighted(x => x.Forces.Front.Normal), Wheelspin = Weighted(x => x.Forces.RearSlip),
            AbsoluteSideslip = Weighted(x => Math.Abs(SlipAngle38(x.After))), RollRms = Math.Sqrt(Weighted(x => x.After.Pose.Roll * x.After.Pose.Roll)),
            AbsoluteSteeringRate = Weighted(x => Math.Abs(x.Forces.SteerRate)), SteeringPower = Weighted(x => x.Forces.SteeringPower),
            ContactAndDrag = Weighted(x => x.Forces.SlideLoss), RoadSpeed = Speed38(original.Final.State, g),
            OverallRatio = lab.Fixture.Ratio, DocumentedDriveSpeedTargetKph = 81
        };
        var capture = new List<(Score39 Score, Trial39 Trial)>();
        int id = 0;
        var choices = (from offset in new[] { 8d, 15, 20, 30, 40 }
                       from command in new[] { .35, .66, .85, 1.0 }
                       from objective in Enum.GetValues<Recovery39>()
                       select new Plan39(++id, lab.Bend.Length + offset, 0, lab.Bend.Length + offset, Throttle39.Partial,
                           objective, command, (int)objective, true)).ToArray();
        var rows = new object[choices.Length];
        Parallel.For(0, choices.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) }, i =>
        {
            var p = choices[i];
            int j = Array.FindIndex(original.Distances, x => x >= p.Start);
            var at = new Checkpoint38(original.Steps[j].After, original.Distances[j]);
            var prior = tail.Commands.Where(c => c.Requested < at.State.Time - 1e-10).ToArray();
            var trial = RunExit39(lab, null, g, p, root, at, prior, original.Steps[..(j + 1)], original.Distances[..(j + 1)]);
            var score = Measure39(lab, g, trial);
            rows[i] = new { Plan = p, Matched = at, PriorCommands = prior, trial.Commands, Score = score };
            lock (capture)
            {
                capture.Add((score, trial));
                capture.Sort((a, b) => Order(a.Score, b.Score));
                if (capture.Count > 3) capture.RemoveAt(capture.Count - 1);
            }
        });
        var shortlist = capture.Select(x =>
        {
            var replay = VerifyTrial39(lab, g, x.Trial, H36Footprint);
            return (Trial: replay, Score: Measure39(lab, g, replay));
        }).ToArray();
        Array.Sort(shortlist, (a, b) => Order(a.Score, b.Score));
        var best = shortlist[0];
        WriteEvidence39(lab, output, "straight-selected", g, root, best.Trial.Commands, best.Trial.Flight);
        Save(Path.Combine(output, "straight-experiment.json"), new
        {
            Plateau = plateau, Original = Measure39(lab, g, old), Selected = best.Score, SelectedPlan = best.Trial.Plan,
            Checked = shortlist.Select(x => x.Score),
            Comparisons = rows, CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
            WallSeconds = timer.Elapsed.TotalSeconds
        });
        VerifyFastestStraight39(lab, output);
        Console.WriteLine("H39 finished 60 ordinary-straight comparisons.");

        static int Order(Score39 a, Score39 b)
        {
            int Gate(Score39 s) => (s.NextReachable ? 4 : 0) + (s.Plus20Gate ? 2 : 0) + (s.LaterGate ? 1 : 0);
            int c = Gate(b).CompareTo(Gate(a));
            if (c == 0) c = Nullable.Compare(b.FortyKph, a.FortyKph);
            if (c == 0) c = b.LastMeters.CompareTo(a.LastMeters);
            return c == 0 ? a.Id.CompareTo(b.Id) : c;
        }
    }

    private static void StraightHoldouts39(Lab38 lab, string output, Witnesses39 cases)
    {
        var mechanical = cases.Reference.Geometry;
        var selected = ReadPlain214<StraightArchive39>(Path.Combine(output, "straight-experiment.json")).SelectedPlan;
        var archive24 = cases.Attempts.Single(r => r.Plan.Id == 24);
        var pulse = ReadPlain214<RecourseArchive39>(Path.Combine(lab.Root, "data/experiments/speedway219/recourse.json"))
            .Trials.Single(r => r.Settings.Id == 24).Settings;
        HeldSeed38[] held = [new("inside slower", -.35, -.068, .004), new("outside faster", .37, .071, .006),
            new("outside slower", -.24, .088, .009), new("inside faster", .23, -.092, .012)];
        Save(Path.Combine(output, "straight-holdout-freeze.json"), new { Selected = selected, Holds = held });
        var scores = new Score39[held.Length];
        Parallel.For(0, held.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            var h = held[i];
            var model = lab.Engine(mechanical);
            var root = archive24.Seed;
            var rest = SteadyCircle215(model, mechanical, lab.Fixture.Rest(mechanical, NominalSupport216),
                (40 + h.SpeedKph) / 3.6, 0, root.State.Time, lab.Fixture.Ratio, balanceRider: true);
            if (rest.Residual > .005) throw new InvalidOperationException("Unreachable straight holdout equilibrium.");
            var origin = new Checkpoint38(Place36(rest.State, lab.Bend.Entry + root.Distance, 8.2 + h.LaneMeters), root.Distance);
            var pending = archive24.InitialCommands.Select(c => c with { Time = c.Time + h.QueueDelaySeconds }).ToArray();
            var init = ReplayTape38(lab, mechanical, origin, pending, archive24.Initial.State.Time);
            var newFlight = init;
            var q = pending;
            if (init.Stop == "verified interval")
            {
                var arc = PulseRun38(lab, cases.Reference, cases.Parent, pulse, mechanical, init.Final, pending, false,
                    cases.EquilibriumDrive);
                newFlight = Merge(init, arc.Trail);
                q = arc.Commands;
            }
            if (newFlight.Final.Distance >= lab.Bend.Length + 20)
            {
                var fixedLane = lab.Course.Mark(newFlight.Steps[Array.FindIndex(newFlight.Distances,
                    d => d >= lab.Bend.Length + 8)].After, mechanical).Road.OffsetMeters - .5;
                var tune = new Tune36(fixedLane, 23, 17, 8, 0, 0, 0, 0, 0, true);
                var state = newFlight.Final;
                var suffix = new List<Step215>();
                var stations = new List<double>();
                double edge = newFlight.Boundary, ground = newFlight.Ground;
                string stop = "verified interval";
                double next = q[^1].Requested + Cadence38;
                double limit = state.State.Time + 2.5;
                while (state.Distance < selected.Start && state.State.Time < limit && stop == "verified interval")
                {
                    if (state.State.Time >= next - 1e-10)
                    {
                        var mark = lab.Course.Mark(state.State, mechanical);
                        var control = H36Control(state.State, mechanical, model, mark, lab.Bend.Entry + state.Distance,
                            lab.Bend, tune, 81, cases.EquilibriumDrive, q, lab.Course, rollReferenceScale: .60);
                        if (control.Command is not { } command) { stop = control.Stop ?? "straight controller abstention"; break; }
                        q = Submit38(state, q, command);
                        next += Cadence38;
                    }
                    var f = Window38(lab, mechanical, model, state, q, next - state.State.Time, finishAt: selected.Start);
                    state = f.Final;
                    suffix.AddRange(f.Steps);
                    stations.AddRange(f.Distances);
                    edge = Math.Min(edge, f.Boundary);
                    ground = Math.Min(ground, f.Ground);
                    stop = f.Stop;
                }
                if (stop == "verified interval" && state.Distance < selected.Start) stop = "straight fork horizon exhausted";
                newFlight = Merge(newFlight, new(state, suffix.ToArray(), stations.ToArray(), stop, edge, ground));
            }
            var trial = newFlight.Final.Distance >= selected.Start
                ? RunExit39(lab, null, mechanical, selected, origin, newFlight.Final, q, newFlight.Steps, newFlight.Distances)
                : new Trial39(selected, origin, newFlight.Final, q, q, newFlight, 0, 0, 0);
            var verified = VerifyTrial39(lab, mechanical, trial, H36Footprint);
            scores[i] = Measure39(lab, mechanical, verified);
            WriteEvidence39(lab, output, $"straight-holdout-{i + 1}", mechanical, origin, verified.Commands, verified.Flight);
        });
        Save(Path.Combine(output, "straight-holdouts.json"), new { Selected = selected, Outcomes = scores, NativeWorlds = 0 });

        static Flight38 Merge(Flight38 a, Flight38 b) => b with
        {
            Steps = [.. a.Steps, .. b.Steps], Distances = [.. a.Distances, .. b.Distances],
            Boundary = Math.Min(a.Boundary, b.Boundary), Ground = Math.Min(a.Ground, b.Ground)
        };
    }

    private sealed record Sensitivity39(double Distance, string Variant, double ContactFx, double ContactFy,
        double ResultantUse, double SlipDissipationWatts, double AddedYawNm, double AddedRollNm,
        double AngularReactionWatts, double ApproximateContactBudget);

    private static void MechanicalSensitivity39(Lab38 lab, string output)
    {
        var cases = Reconstruct39(lab);
        var run = cases.Attempts.Single(x => x.Plan.Id == 57);
        var g = cases.Reference.Geometry;
        var rows = new List<Sensitivity39>();
        double memoryX = 0, memoryY = 0;
        foreach (var (step, distance) in run.Trail.Steps.Zip(run.Trail.Distances))
        {
            var f = step.Forces;
            var state = step.Midpoint;
            var frame = g.Motion(state);
            var patch = f.Rear.Patch;
            var cap = f.Rear.Capacity;
            var u = patch.Velocity.Dot(patch.Forward);
            var v = patch.Velocity.Dot(patch.Side);
            double slip = g.Base.Radius * (state.Wheel / g.Base.WheelInertia - frame.BodyRate.Y) - u;
            double dt = Duration215(step);
            double fx = f.Rear.Longitudinal, fy = f.Rear.Lateral;
            memoryX += (fx - memoryX) * (1 - Math.Exp(-dt / .055));
            memoryY += (fy - memoryY) * (1 - Math.Exp(-dt / .100));
            if (distance < 55 || cap < 1) continue;
            double yawVelocity = lab.Course.Mark(state, g).AngularVelocity;
            Write("stock", fx, fy);
            // Contact-level perturbations share the nominal coefficient and normal
            // load. They are observations, never inserted into a qualifying solve.
            Write("camber 0.10 C/rad", fx, fy + .10 * cap * Math.Asin(patch.Axle.Z));
            Write("camber 0.25 C/rad", fx, fy + .25 * cap * Math.Asin(patch.Axle.Z));
            Write("twist arm 0.020 m", fx, fy, -cap * .02 * Math.Tanh(yawVelocity / .9) *
                Math.Sqrt(Math.Max(0, 1 - f.Rear.Utilization * f.Rear.Utilization)));
            Write("separate 55/100 ms", memoryX, memoryY);
            Write("loose direction 15 percent", .94 * fx, fy * (1 + .15 * Math.Abs(slip) /
                (Math.Abs(slip) + Math.Abs(v) + .001)));
            var front = f.Front.Patch;
            double frontRate = front.Velocity.Dot(front.Forward) / g.Base.Radius;
            var steerRate = state.Pose.Apply(Vec215.Up) * f.SteerRate;
            var gyroReaction = -(frame.WorldRate + steerRate).Cross(front.Axle * (.50 * frontRate));
            Write("front gyro 0.50 kg m2", fx, fy, 0, gyroReaction);

            void Write(string name, double x, double y, double twist = 0, Vec215 gyro = default)
            {
                double scale = Math.Min(1, cap / Math.Max(1e-9, Math.Sqrt(x * x + y * y)));
                x *= scale;
                y *= scale;
                var moment = patch.Arm.Cross(patch.Forward * (x - fx) + patch.Side * (y - fy)) + Vec215.Up * twist + gyro;
                rows.Add(new(distance, name, x, y, Math.Sqrt(x * x + y * y) / cap,
                    x * slip - y * v - twist * yawVelocity, moment.Z, state.Pose.Body(moment).X,
                    gyro.Dot(frame.WorldRate), cap));
            }
        }
        Save(Path.Combine(output, "tyre-shadow.json.gz"), rows, true);
        RiderSupportAudit39(lab, cases, output);
        Console.WriteLine($"H39 computed {rows.Count} isolated, non-integrated tyre sensitivities.");
    }

    private static void RiderSupportAudit39(Lab38 lab, Witnesses39 cases, string output)
    {
        var source = cases.Attempts.Single(x => x.Plan.Id == 57);
        var g = cases.Reference.Geometry;
        var combinations = (from station in new[] { 40d, 51, 64 }
                            from drive in new[] { .55f, .85f, 1f }
                            from posture in new[] { 0, 1, 2, 3, 4, 5 }
                            select (station, drive, posture)).ToArray();
        var results = new object[combinations.Length];
        Parallel.For(0, combinations.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) }, i =>
        {
            var (station, drive, posture) = combinations[i];
            int j = Array.FindIndex(source.Trail.Distances, x => x >= station);
            var s = new Checkpoint38(source.Trail.Steps[j].After, source.Trail.Distances[j]);
            var q = source.Commands.Where(c => c.Requested < s.State.Time - 1e-10).ToArray();
            var prefix = q;
            var c = s.State.Command;
            var move = posture switch
            {
                1 or 4 => new Vec215(.085, 0, 0),
                2 or 5 => new Vec215(.041, .046, -.035),
                3 => new Vec215(.04, -.03, -.065),
                _ => default
            };
            if (posture is 4 or 5)
                q = Submit38(s, q, c with { Target = new(0, 0, .033), Bike = c.Bike with { Drive = drive } });
            else
                q = Submit38(s, q, c with { Target = move, Bike = c.Bike with { Drive = drive } });
            var model = lab.Engine(g);
            var first = Window38(lab, g, model, s, q, .21);
            if (posture is 4 or 5 && first.Stop == "verified interval")
                q = Submit38(first.Final, q, c with { Target = move, Bike = c.Bike with { Drive = drive } });
            var second = first.Stop == "verified interval" ? Window38(lab, g, model, first.Final, q, .40) : first;
            var contacts = first == second ? first.Steps : first.Steps.Concat(second.Steps).ToArray();
            var receipt = Measure38(contacts, g, second.Stop);
            results[i] = new { Station = station, Drive = drive, Posture = posture, Requested = move, Initial = s,
                Prefix = prefix, Commands = q, Receipt = receipt, Travel = second.Final.State.Rider - s.State.Rider,
                InitialSeat = contacts.FirstOrDefault()?.Forces.PassiveSupport?.Seat.Contact.NormalForce,
                EndSeat = contacts.LastOrDefault()?.Forces.PassiveSupport?.Seat.Contact.NormalForce,
                EndPegs = contacts.LastOrDefault()?.Forces.PassiveSupport?.LeftPeg.Contact.NormalForce,
                Status = second.Stop };
        });
        Save(Path.Combine(output, "rider-support.json.gz"), results, true);
    }
}
