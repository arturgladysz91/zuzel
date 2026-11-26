namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record FrontTorque42(double Time, double StateRoll, double Steering, double FrontLongitudinal,
        double FrontLateral, double Normal, double AxialMomentum, double RollingTorque, Vec215 FrameTransport,
        Vec215 SteeringTransport, Vec215 GroundMoment, double SteeringReactionWatts, double EnergyResidual);

    private static void Isolate42(Lab38 lab, string output)
    {
        FrontContact221[] variants = [default, new(.25), new(.45), new(.65),
            new(RelaxationMeters: .35, SteadyLateral: true), new(RelaxationMeters: .35),
            new(RelaxationMeters: .75, SteadyLateral: true), new(RelaxationMeters: .75)];
        var g0 = lab.Geometry(true);
        var rest = lab.Fixture.Rest(g0, NominalSupport216);
        var straight = SteadyCircle215(lab.Engine(g0), g0, rest, 15, 0, lab.Fixture.Release + 1,
            lab.Fixture.Ratio, balanceRider: true);
        if (straight.Residual > .005) throw new InvalidOperationException("Physics isolation fixture did not settle.");
        var labRuns = new List<object>();
        foreach (var variant in variants)
        {
            var g = Geometry221(lab, variant);
            var engine = lab.Fixture.Engine(g, NominalSupport216, Actuator215.Limited, false, gravity: 0);
            var seed = AddFrontState221(g, straight.State with { Center = new(0, 0, 1.60),
                Command = straight.State.Command with { Bike = new(.20f, 0) } });
            var air = RunRig221("rotor-only airborne reaction", g, engine, seed, [], .4, .00125, 0);
            var steady = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216), 12, .23,
                straight.State.Time, lab.Fixture.Ratio, balanceRider: true);
            var s = steady.State;
            var turn = new Activation215[] { new(s.Time, s.Time + .05, s.Command with
                { Bike = s.Command.Bike with { SteeringRadians = (float)(s.Steer - .038) } }),
                new(s.Time + .084, s.Time + .134, s.Command) };
            var rig = RunRig221("circle matched pulse", g, lab.Engine(g), s, turn, .40, .00125, Gravity216);
            var coarse = RunRig221("circle matched pulse", g, lab.Engine(g), s, turn, .40, .0025, Gravity216);
            var f = lab.Engine(g).Step(s, [], .0005).Forces;
            labRuns.Add(new { Variant = variant, Air = air, Countersteer = rig, Coarse = coarse, steady.Residual,
                StaticFrontForce = f.Front,
                PositionConvergence = (coarse.Final.Center - rig.Final.Center).Norm });
        }
        Save(Path.Combine(output, "front-isolation-rigs.json"), new { Probes = labRuns, Style = "axial friction and gyroscopic transport separated", NativeWorlds = 0 });

        var fast = ReadArchive42(lab, "220/straight-full-diagnostic");
        var mild = ReadArchive42(lab, "221/transfer/transfer-nominal");
        double entry = lab.Bend.NextEntry - lab.Bend.Entry;
        var stations = new[] { (Source: fast, Station: entry - 20, Label: "approach"), (Source: fast, Station: entry + 4, Label: "next-entry"),
            (Source: mild, Station: 92.0, Label: "first-exit") };
        var outcomes = new object[stations.Length * variants.Length];
        Parallel.For(0, outcomes.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var work = stations[i / variants.Length];
            int v = i % variants.Length;
            var spec = variants[v];
            var g = Geometry221(lab, spec);
            var source = Slice42(work.Source, work.Station);
            var initial = Initialize42(g, source.At);
            string key = work.Label + "-" + v;
            var trial = ReplayTape38(lab, g, initial, work.Source.Tape, initial.State.Time + .60, exact: true);
            var rig = FinishRig221(key, g, initial.State, trial.Steps, trial.Stop, StepSeconds216, Gravity216);
            var rows = trial.Steps.Select(x => Excitation42(g, x)).ToArray();
            Save(Path.Combine(output, $"forces-{key}.json.gz"), rows, true);
            outcomes[i] = new { Key = key, Variant = spec, Matched = source.At, Initial = initial, trial.Final, trial.Boundary,
                trial.Ground, Rig = rig, LongitudinalFrontImpulse = trial.Steps.Sum(x => Duration215(x) * x.Forces.Front.Longitudinal),
                GyroTorquePeak = rows.Select(x => (x.FrameTransport + x.SteeringTransport).Norm).DefaultIfEmpty().Max(),
                SteeringWork = trial.Steps.Sum(x => Duration215(x) * x.Forces.FrontResponse.SteeringReactionWatts) };
            if (v is 0 or 2 or 4 or 5)
                WriteEvidence39(lab, output, key, g, initial, work.Source.Tape, trial);
        });
        Save(Path.Combine(output, "front-isolation-matched.json"), outcomes);

        // Retune each opt-in plant using a separate objective and the same reached checkpoint.
        // These are local variant experiments. They do not enter the stock qualification.
        var tuned = new object[variants.Length];
        Parallel.For(0, variants.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var spec = variants[i];
            var g = Geometry221(lab, spec);
            var found = new List<(Policy42 Policy, Run40 Run, double Seconds)>();
            var selected = Slice42(fast, entry - 40);
            var start = Initialize42(g, selected.At);
            int id = 20;
            foreach (double lane in new[] { 5.6, 6.5 })
            foreach (double boost in new[] { 0d, .05, .095 })
            {
                var setup = SetupBaseline42() with { Lane = lane, Id = ++id, CornerRequestKph = boost > .09 ? 78 : 77 };
                var p = new Policy42(id, boost == 0 ? Family42.Archive : Family42.Concurrent, setup, 2, boost, .12, 0, 0);
                var run = Ride42(lab, g, start, start, [], [], selected.Commands, p);
                found.Add((p, run, run.Flight.Final.State.Time - start.State.Time));
            }
            double end = Map199.At(2, 1).Exit - lab.Bend.Entry + 22;
            var best = found.OrderByDescending(t => t.Run.Flight.Final.Distance >= end)
                .ThenBy(t => t.Run.Flight.Final.Distance >= end ? t.Seconds : -t.Run.Flight.Final.Distance).First();
            var verified = ExactLink40(lab, g, best.Run);
            var f = verified.Flight;
            WriteEvidence39(lab, output, $"retuned-{i}", g, start, verified.Commands, f);
            tuned[i] = new { Variant = spec, Selected = best.Policy, Exact = f.Final, f.Stop, f.Boundary, f.Ground,
                SecondAndExitCompleted = f.Final.Distance >= end - .001, f.Final.State.Time,
                Compared = found.Select(r => new { r.Policy, r.Seconds, r.Run.Flight.Stop, r.Run.Flight.Final,
                    r.Run.Flight.Boundary, r.Run.Flight.Ground, r.Run.Commands }) };
        });
        Save(Path.Combine(output, "variant-retuning.json.gz"), tuned, true);
    }

    private static Checkpoint38 Initialize42(Geometry215 geometry, Checkpoint38 s)
    {
        if (!geometry.FrontContact.HasLateralMemory)
            return s with { State = AddFrontState221(geometry, s.State) };
        var patch = geometry.Contact(s.State, geometry.Motion(s.State), true, 0);
        double u = patch.Velocity.Dot(patch.Forward), v = patch.Velocity.Dot(patch.Side);
        double q = v * geometry.FrontContact.RelaxationMeters / Math.Sqrt(u * u + .75 * .75);
        double cap = Math.Max(0, -UnifiedModel215.Stiffness * patch.Gap) * Grip42;
        double k = geometry.FrontContact.ShearStiffness;
        if (k * Math.Abs(q) > cap) q = Math.CopySign(Math.Sqrt(cap * Math.Abs(q) / k), q);
        return s with { State = AddFrontState221(geometry, s.State, shearMeters: q) };
    }

    private static FrontTorque42 Excitation42(Geometry215 g, Step215 step)
    {
        var s = step.Midpoint;
        var q = step.Forces;
        var wheel = q.Front.Patch;
        var m = g.Motion(s);
        var body = -m.WorldRate.Cross(wheel.Axle) * s.FrontWheel;
        var steer = -s.Pose.Apply(Vec215.Up).Cross(wheel.Axle) * (q.SteerRate * s.FrontWheel);
        return new(step.After.Time, s.Pose.Roll, s.Steer, q.Front.Longitudinal, q.Front.Lateral, q.Front.Normal,
            s.FrontWheel, q.FrontResponse.RotorTorqueNm, body, steer, wheel.Arm.Cross(q.Front.Force),
            q.FrontResponse.SteeringReactionWatts, step.EnergyResidual);
    }
}
