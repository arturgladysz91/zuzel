
using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Base39 = "422504e34dc09462bf10ea7b204ba5221349878c";
    private sealed record RecourseArchive39(PulseOutcome38[] Trials);
    private sealed record Witnesses39(Archive38 Reference, Candidate37 Parent, float EquilibriumDrive, Attempt38[] Attempts);
    private sealed record Sample39(double ReferenceMeters, State215 State, float ActiveDrive, float RequestedDrive,
        double Rpm, double Torque, double EnginePower, double EngineLoss, double TransmissionLoss, double BearingLoss,
        double ContactAndDragLoss, double SteeringPower, double ActivePower, double EnergyResidual,
        double RearLongitudinalVelocity, double RearLateralVelocity, double RelativeRearRate, double ConstructorRearSlip,
        double ContactSlip, double SolverSlip, double RearTyreAngle, double VehicleSlip, double VelocityHeading,
        double HeadingError, double Yaw, double YawRate, double Roll, double RollRate, double Pitch, double PitchRate,
        double SteerRate, double FrontNormal, double RearNormal, Vec215 FrontForce, Vec215 RearForce, Vec215 FootForce,
        double FrontYawBand, double FrontRollBand, Vec215 ActualMoment, Vec215 InternalRiderForce,
        double FrontUse, double RearUse, double SeatLoad, double PegLoad, double GripLoad,
        double SweptBoundary, double SweptGround, int PendingFrom, int PendingUntil);
    private sealed record Evidence39(string Name, string Stop, State215 Seed, Activation215[] Commands, Sample39[] Rows);

    private static void ExitStudy220(string workspace, string output, string mode)
    {
        if (mode is not ("all" or "audit" or "search" or "straight" or "sensitivity" or "tape" or "settle" or "straight-holds"))
            throw new ArgumentException("Use all, audit, search, straight, sensitivity, tape, settle, or straight-holds.");
        string root = Path.GetFullPath(workspace);
        var (_, spec) = CourseFixture208(root, "C");
        var fixture = new Fixture216(spec);
        var track = new RacingLineGeometry(Frame);
        var lab = new Lab38(root, fixture, Map199.At(2, 0), track,
            new CornerCourse216(track, RootFor210(root, "C").State.State.Rider));
        Save(Path.Combine(output, "stationing.json"), new
        {
            MassKg = lab.Fixture.Base.Mass, ExitReferenceMeters = lab.Bend.Length,
            NextEntryReferenceMeters = lab.Bend.NextEntry - lab.Bend.Entry, NativeWorlds = 0
        });
        if (mode == "tape")
        {
            WriteOriginalStraight39(lab, output);
            VerifyFastestStraight39(lab, output);
            VerifyPreservedSlide39(lab, output);
            return;
        }
        if (mode == "sensitivity")
        {
            MechanicalSensitivity39(lab, output);
            return;
        }
        if (mode == "straight")
        {
            StraightExperiments39(lab, output);
            return;
        }
        var cases = Reconstruct39(lab);
        if (mode is "all" or "audit") Audit39(lab, output, cases);
        if (mode is "all" or "search") SearchExit39(lab, output, cases);
        if (mode == "settle") SearchExit39(lab, output, cases, settle: true);
        if (mode == "straight-holds") StraightHoldouts39(lab, output, cases);
        if (mode == "all")
        {
            StraightExperiments39(lab, output);
            MechanicalSensitivity39(lab, output);
        }
    }

    private static Witnesses39 Reconstruct39(Lab38 lab)
    {
        string data = Path.Combine(lab.Root, "data/experiments");
        var parent = ReadPlain214<StageTrials37>(Path.Combine(data, "speedway218/stage-80.json")).Outcomes.Single(x => x.Setup.Id == 30).Setup;
        var stored = ReadPlain214<RecourseArchive39>(Path.Combine(data, "speedway219/recourse.json"));
        var tapes = Read199<CommandHistory38[]>(Path.Combine(data, "speedway219/recourse-commands.json.gz"));
        var g = lab.Geometry(true);
        var b = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216), 40 / 3.6, 0,
            lab.Fixture.Release + 1, lab.Fixture.Ratio, balanceRider: true);
        var seed = Place36(b.State, lab.Bend.Entry - PreparationStart37, 8.20);
        float equilibrium = b.State.Command.Bike.Drive;
        var refRun = RunH36(30, "H37 p30 reference", 80, parent.Tune, g, lab.Engine(g), seed, lab.Bend,
            lab.Track, lab.Course, equilibrium, null, false, preparation: parent.Plan, startingMeters: PreparationStart37);
        var a = new Archive38("H37-30", refRun, refRun.ReferenceSamples!.Select(r => r - lab.Bend.Entry).ToArray(), g, parent.Plan);
        int[] ids = [24, 57, 59, 60];
        var attempts = new Attempt38[ids.Length];
        Parallel.For(0, ids.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            int id = ids[i];
            var plan = stored.Trials.Single(x => x.Settings.Id == id).Settings;
            var (initial, pending) = ForkAt38(a, -22);
            var run = PulseRun38(lab, a, parent, plan, g, initial, pending, true, equilibrium);
            var expected = tapes.Single(t => t.Result.Plan.Id == id);
            var actual = Summarize38(lab, g, run);
            if (expected.Seed != run.Seed || expected.Fork != run.Initial ||
                !expected.EarlierCommands.SequenceEqual(run.InitialCommands) || !expected.Commands.SequenceEqual(run.Commands) ||
                expected.Result != actual)
                throw new InvalidOperationException($"Stored H38 recourse {id} did not reproduce exactly.");
            attempts[i] = run;
        });
        return new(a, parent, equilibrium, attempts);
    }

    private static void Audit39(Lab38 lab, string output, Witnesses39 cases)
    {
        var clock = Stopwatch.StartNew();
        var g = cases.Reference.Geometry;
        var a = ReadArchive38(lab, "70");
        var b = ReadArchive38(lab, "78");
        var publishedTail = Read199<Runout37>(Path.Combine(lab.Root, "data/experiments/speedway218/terminal-80-runout.json.gz"));
        var t = ReadPlain214<Terminal218>(Path.Combine(lab.Root, "data/experiments/speedway218/terminal-80.json"));
        var tail = CheckRunout37(lab.Fixture, b.Geometry, lab.Track, lab.Course, lab.Bend, b.Run, true, 2.10, t.Selection);
        if (!tail.Stable || tail.Final != publishedTail.Final ||
            !tail.Trace.Select(x => x.After).SequenceEqual(publishedTail.Trace.Select(x => x.After)))
            throw new InvalidOperationException("#218 straight continuation did not reproduce.");
        var inherited = ReplayInherited39(lab);

        foreach (var run in cases.Attempts)
            WriteEvidence39(lab, output, $"archive-{run.Plan.Id}", g, run.Seed, run.Commands, run.Trail);
        var diagnostics = ProbeFrontier39(lab, cases);
        var src = cases.Attempts.Single(r => r.Plan.Id == 57);
        var data = Read199<Evidence39>(Path.Combine(output, "archive-57.json.gz"));
        var phases = new[] { (74d, 92d), (92d, lab.Bend.Length), (lab.Bend.Length, src.Trail.Final.Distance + .001) }
            .Select(interval =>
            {
                var segment = data.Rows.Where(r => r.ReferenceMeters >= interval.Item1 && r.ReferenceMeters < interval.Item2).ToArray();
                return new
                {
                    From = interval.Item1, To = interval.Item2,
                    Samples = segment.Length,
                    ComSlipErrorRms = Math.Sqrt(segment.Average(r => Math.Pow(r.ConstructorRearSlip - r.SolverSlip, 2))),
                    ModelSlipErrorPeak = segment.Max(r => Math.Abs(r.ContactSlip - r.SolverSlip)),
                    ComSlipErrorPeak = segment.Max(r => Math.Abs(r.ConstructorRearSlip - r.SolverSlip)),
                    PeakPitch = segment.Max(r => Math.Abs(r.Pitch)),
                    PeakYawRate = segment.Max(r => Math.Abs(r.YawRate)),
                    PeakRollRate = segment.Max(r => Math.Abs(r.RollRate))
                };
            }).ToArray();
        Save(Path.Combine(output, "baseline-and-diagnosis.json"), new
        {
            Parent = Base39, NativeWorlds = 0, Benchmark217 = a.Run.Summary, Benchmark218 = b.Run.Summary,
            Straight218 = new { tail.InitialKph, tail.FinalKph, tail.Stable },
            Stored219 = cases.Attempts.Select(r => Summarize38(lab, g, r)), Inherited = inherited, SlipAudit = phases,
            MatchedRecovery = diagnostics, ElapsedSeconds = clock.Elapsed.TotalSeconds
        });
        Console.WriteLine("H39 reproduced 57/59/60, the reference arcs, and all five #219 continuations.");
    }

    private static object[] ReplayInherited39(Lab38 lab)
    {
        string root = Path.Combine(lab.Root, "data/experiments/speedway219");
        var result = new object[5];
        Parallel.For(0, result.Length, new ParallelOptions { MaxDegreeOfParallelism = 5 }, i =>
        {
            string name = i == 0 ? "nominal" : $"holdout-{i}";
            var initial = Read199<FullLog38>(Path.Combine(root, $"recourse-{name}.json.gz"));
            var tail = Read199<FullLog38>(Path.Combine(root, $"qualified-straight-{name}.json.gz"));
            if (!initial.Commands.SequenceEqual(tail.EarlierCommands) || initial.States[^1].State != tail.OriginalSeed)
                throw new InvalidOperationException($"Invalid inherited FIFO handoff: {name}.");
            var g = lab.Geometry(true);
            var seed = new Checkpoint38(initial.OriginalSeed, -PreparationStart37);
            var end = tail.States[^1].State;
            var r = ReplayTape38(lab, g, seed, tail.Commands, end.Time, exact: true);
            double error = (r.Final.State.Center - end.Center).Norm;
            double momentum = (r.Final.State.Momentum - end.Momentum).Norm;
            if (error > .002 || momentum > .12 || Math.Abs(end.Time - r.Final.State.Time) > 1e-7)
                throw new InvalidOperationException($"Inherited H38 {name} differs: {error:R} m, {momentum:R} kg m/s.");
            result[i] = new { Name = name, ErrorMeters = error, ErrorMomentum = momentum, Steps = r.Steps.Length,
                Boundary = r.Boundary, Ground = r.Ground, Speed = Speed38(r.Final.State, g),
                Distance = r.Final.Distance, EndTime = r.Final.State.Time, PendingCommands = tail.Commands.Length - r.Final.State.Next };
        });
        return result;
    }

    private static double ContactSlip39(Geometry215 g, State215 s)
    {
        var motion = g.Motion(s);
        var patch = g.Contact(s, motion, false, 0);
        return RearWheelDynamics.SlipRatio(patch.Velocity.Dot(patch.Forward),
            s.Wheel / g.Base.WheelInertia - motion.BodyRate.Y, Physics.RearWheel ?? RearWheelParameters.Default);
    }

    private static void WriteEvidence39(Lab38 lab, string output, string key, Geometry215 g, Checkpoint38 origin,
        Activation215[] commands, Flight38 flight, SpatialBike36? envelope = null)
    {
        var shape = envelope ?? H36Footprint;
        var p = Physics.RearWheel ?? RearWheelParameters.Default;
        double oldEdge = shape.Clearance(lab.Track, g, origin.State, false);
        double oldGround = shape.GroundMargin(g, origin.State);
        var rows = new List<Sample39>(flight.Steps.Length);
        int latest = 0;
        foreach (var (row, distance) in flight.Steps.Zip(flight.Distances))
        {
            var after = row.After;
            var middle = row.Midpoint;
            var f = row.Forces;
            while (latest < commands.Length && commands[latest].Requested <= after.Time + 1e-10) latest++;
            var motion = g.Motion(after);
            var contact = f.Rear.Patch;
            double u = contact.Velocity.Dot(contact.Forward), v = contact.Velocity.Dot(contact.Side);
            double rim = middle.Wheel / g.Base.WheelInertia - g.Motion(middle).BodyRate.Y;
            double com = middle.Momentum.Plane.Norm / g.Base.Mass;
            double obsolete = (p.WheelRadiusMeters * middle.Wheel / g.Base.WheelInertia - com) / Math.Max(1, com);
            double edge = shape.Clearance(lab.Track, g, after, false), ground = shape.GroundMargin(g, after);
            var mark = lab.Course.Mark(after, g);
            double heading = Heading38(after), road = Math.Atan2(mark.Road.Tangent.Y, mark.Road.Tangent.X);
            double pitchRate = motion.WorldRate.Cross(after.Pose.Apply(Vec215.Forward)).Z / Math.Max(.1, Math.Cos(after.Pose.Pitch));
            rows.Add(new(distance, after, row.Before.Command.Bike.Drive,
                latest > 0 ? commands[latest - 1].Command.Bike.Drive : row.Before.Command.Bike.Drive,
                f.EngineRpm, f.NetEngineTorque, f.EnginePower, f.EngineLoss, f.TransmissionLoss, f.BearingLoss,
                f.SlideLoss, f.SteeringPower, row.ActuatorPower, row.EnergyResidual, u, v, rim, obsolete,
                RearWheelDynamics.SlipRatio(u, rim, p), f.RearSlip, Math.Atan2(v, u), SlipAngle38(after), heading,
                Difference38(heading, road), after.Pose.Yaw, mark.AngularVelocity, after.Pose.Roll,
                RollRate36(after, motion.BodyRate), after.Pose.Pitch, pitchRate, f.SteerRate, f.Front.Normal, f.Rear.Normal,
                f.Front.Force, f.Rear.Force, f.LeftFoot?.Force ?? default, f.FrontYawBand, f.FrontRollBand, row.RollPitchYawMoment,
                f.Rider, f.Front.Utilization, f.Rear.Utilization, f.PassiveSupport?.Seat.Contact.NormalForce ?? 0,
                (f.PassiveSupport?.LeftPeg.Contact.NormalForce ?? 0) + (f.PassiveSupport?.RightPeg.Contact.NormalForce ?? 0),
                (f.PassiveSupport?.LeftGrip.Contact.NormalForce ?? 0) + (f.PassiveSupport?.RightGrip.Contact.NormalForce ?? 0),
                Math.Min(edge, oldEdge) - shape.TransportBound(g, row) / 2,
                Math.Min(ground, oldGround) - shape.TransportBound(g, row, groundOnly: true) / 2,
                after.Next, latest));
            oldEdge = edge;
            oldGround = ground;
        }
        Save(Path.Combine(output, key + ".json.gz"), new Evidence39(key, flight.Stop, origin.State, commands, rows.ToArray()), true);
    }
}
