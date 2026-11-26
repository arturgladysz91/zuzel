using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Rig221(string Name, FrontContact221 Spec, double StepSeconds, int Steps, double SimSeconds,
        string Stop, State215 Initial, State215 Final, double LinearDefect, double AngularDefect, double MaxEnergyDefect,
        double StoredEnergyDelta, double EngineWork, double DissipatedWork, double SteeringWork, double RotorSteerWork,
        double FrontSpinMomentumDelta, double AxialGroundImpulse, double PeakFrontUse, double PeakFrontLoad,
        double AirSeconds, double LoadedSeconds, double MaxRoll, double MaxRollRate, double PeakFrontSlip,
        int Touchdowns, int Lifts, double YawRate, double SpeedKph);

    private static Geometry215 Geometry221(Lab38 lab, FrontContact221 options) =>
        new(lab.Fixture.Base, Axes215.All, new Leg216(), H36Footprint, options);

    private static State215 AddFrontState221(Geometry215 g, State215 original, double rollingFactor = 1,
        double shearMeters = 0)
    {
        if (g.FrontContact.PolarInertiaKgM2 <= 0) return original with { FrontShearMeters = shearMeters };
        var motion = g.Motion(original);
        var patch = g.Contact(original, motion, true, 0);
        double spin = rollingFactor * patch.Velocity.Dot(patch.Forward) / g.Base.Radius;
        double p = g.FrontContact.PolarInertiaKgM2 * (spin + motion.WorldRate.Dot(patch.Axle));
        return original with { FrontWheel = p, Angular = original.Angular + patch.Axle * p };
    }

    private static void Mechanics221(Lab38 lab, string output)
    {
        FrontContact221 stock = default;
        var options = new[] { stock, new FrontContact221(.25), new FrontContact221(.45), new FrontContact221(.65),
            new FrontContact221(RelaxationMeters: .35), new FrontContact221(RelaxationMeters: .75) };
        var g0 = lab.Geometry(true);
        var original = lab.Fixture.Rest(g0, NominalSupport216);
        var balanced = SteadyCircle215(lab.Engine(g0), g0, original, 12, .23, lab.Fixture.Release + 1,
            lab.Fixture.Ratio, balanceRider: true);
        if (balanced.Residual > .005) throw new InvalidOperationException("Stock circle fixture was not balanced.");
        var optZero = Geometry221(lab, default);
        var parity0 = lab.Engine(g0).Step(balanced.State, [], StepSeconds216);
        var parity1 = lab.Engine(optZero).Step(balanced.State, [], StepSeconds216);
        bool parity = parity0.After == parity1.After && parity0.Midpoint == parity1.Midpoint &&
            parity0.Forces == parity1.Forces && parity0.EnergyResidual == parity1.EnergyResidual;

        var rigs = new List<Rig221>();
        var receipts = new List<object>();
        foreach (var option in options)
        {
            var g = Geometry221(lab, option);
            var circle = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216), 12, .23,
                balanced.State.Time, lab.Fixture.Ratio, balanceRider: true);
            receipts.Add(new { Option = option, circle.Residual, circle.Iterations, circle.Balance });
            double start = balanced.State.Time;
            var airborneStock = balanced.State with { Center = new(0, 0, 1.4), Momentum = new(12 * g.Base.Mass, 0, 0),
                Command = new(new(.24f, 0), default), Next = 0 };
            var airborne = AddFrontState221(g, airborneStock, shearMeters: option.RelaxationMeters > 0 ? .005 : 0);
            var fall = AddFrontState221(g, balanced.State with { Center = new(0, 0, balanced.State.Center.Z + .10),
                Momentum = balanced.State.Momentum - Vec215.Up * (.65 * g.Base.Mass), Next = 0 }, rollingFactor: .72);
            var countersteer = new[] { new Activation215(start, start + .05,
                circle.State.Command with { Bike = circle.State.Command.Bike with { SteeringRadians = (float)(circle.State.Steer - .032) } }),
                new(start + .075, start + .125, circle.State.Command) };
            foreach (double dt in new[] { .0025, .00125 })
            {
                var m = lab.Fixture.Engine(g, NominalSupport216, Actuator215.Limited, false, gravity: 0);
                rigs.Add(RunRig221("airborne steering", g, m, airborne, [], .40, dt, 0));
                rigs.Add(RunRig221("rolling touchdown", g, lab.Engine(g), fall, [], .70, dt, Gravity216));
                if (circle.Residual <= .005)
                {
                    rigs.Add(RunRig221("steady circle", g, lab.Engine(g), circle.State, [], .75, dt, Gravity216));
                    rigs.Add(RunRig221("countersteering", g, lab.Engine(g), circle.State, countersteer, .29, dt, Gravity216));
                }
            }
        }
        var gates = new
        {
            ZeroInertiaAndZeroRelaxationParity = parity,
            LinearAngularConservation = rigs.All(r => r.LinearDefect < 1e-7 && r.AngularDefect < 1e-7),
            AirMaintainsAxialRotorMomentum = rigs.Where(r => r.Name == "airborne steering")
                .All(r => r.AirSeconds >= .399 && Math.Abs(r.FrontSpinMomentumDelta) < 1e-9 && r.PeakFrontLoad == 0),
            EnergyLedgers = rigs.All(r => r.MaxEnergyDefect < .15),
            ForceBudgets = rigs.All(r => r.PeakFrontUse <= 1.0000001),
            FrontAxialContactImpulse = rigs.All(r => Math.Abs(r.FrontSpinMomentumDelta - r.AxialGroundImpulse) < 1e-8),
            FeasibleCircle = rigs.Where(r => r.Name == "steady circle").Select(r => r.Spec).Distinct().Count() == options.Length,
            CompletedRigs = rigs.All(r => r.Stop == "verified interval"),
            ResolvedWheelLiftAndTouchdown = rigs.Where(r => r.Name == "rolling touchdown")
                .All(r => r.AirSeconds > .02 && r.LoadedSeconds > .1 && r.Touchdowns > 0)
        };
        Save(Path.Combine(output, "lab-validation.json"), new { Gates = gates, Rigs = rigs, Equilibria = receipts,
            RotorAssumptions = new { EffectiveMassRadiusMeters = .28, LumpedRotatingMassKg = new[] { 3.2, 5.7, 8.3 },
                PolarInertiaKgM2 = new[] { .25, .45, .65 } },
            TransientAssumptions = new { RelaxationLengthMeters = new[] { .35, .75 },
                ReferenceSpeedMetersPerSecond = 20, SmallSlipReferenceStiffnessNewtonSecondsPerMeter = 750 },
            NativeWorlds = 0 });

        var book = ReadLinkBook40(lab);
        var old = Reconstruct39(lab);
        var recourse57 = old.Attempts.Single(a => a.Plan.Id == 57);
        var recourse24 = old.Attempts.Single(a => a.Plan.Id == 24);
        var a57 = new Fork40("original full drive", recourse57.Seed, recourse57.Trail, recourse57.Commands);
        var a24 = new Fork40("original powered bend", recourse24.Seed, recourse24.Trail, recourse24.Commands);
        var probes = new[] { (Source: a24, At: lab.Bend.Length - 4, Name: "h38-exit"),
            (Source: a57, At: 83d, Name: "h38-full"),
            (Source: a57, At: 97d, Name: "h38-full-exit"),
            (Source: book.Fast, At: lab.Bend.NextEntry - lab.Bend.Entry - 14, Name: "h39-high-prep"),
            (Source: book.Fast, At: lab.Bend.NextEntry - lab.Bend.Entry + 2, Name: "h39-next-entry") };
        var summaries = new object[probes.Length * options.Length];
        Parallel.For(0, summaries.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var probe = probes[i / options.Length];
            var option = options[i % options.Length];
            var geom = Geometry221(lab, option);
            var at = AtLink40(probe.Source, probe.At);
            var sourceStep = probe.Source.Flight.Steps[Array.FindIndex(probe.Source.Flight.Distances, s => s >= probe.At)];
            double strain = option.RelaxationMeters > 0 ? -sourceStep.Forces.Front.Lateral / option.ShearStiffness : 0;
            var matching = at with { State = AddFrontState221(geom, at.State, shearMeters: strain) };
            var trace = ReplayTape38(lab, geom, matching, probe.Source.Tape, matching.State.Time + .8, exact: true);
            var key = $"{probe.Name}-{i % options.Length}";
            WriteEvidence39(lab, output, key, geom, matching, probe.Source.Tape, trace);
            var receipt = FinishRig221(key, geom, matching.State, trace.Steps, trace.Stop, StepSeconds216, Gravity216);
            summaries[i] = new { Key = key, Model = option, Matched = at, ModelInitial = matching,
                FinalStation = trace.Final.Distance, Energy = receipt, Boundary = trace.Boundary, Hard = trace.Ground,
                SteerReaction = trace.Steps.Select(x => Math.Abs(x.Forces.FrontResponse.SteeringReactionWatts)).DefaultIfEmpty().Max(),
                RollingMomentum = trace.Final.State.FrontWheel };
        });
        Save(Path.Combine(output, "coupled-comparisons.json"), summaries);
    }

    private static Rig221 RunRig221(string name, Geometry215 g, UnifiedModel215 model, State215 seed, Activation215[] queue,
        double duration, double dt, double gravity)
    {
        var s = seed;
        var rows = new List<Step215>();
        string stop = "verified interval";
        try
        {
            double end = seed.Time + duration;
            while (s.Time < end - 1e-9)
            {
                var r = model.Step(s, queue, Math.Min(dt, end - s.Time));
                rows.Add(r);
                s = r.After;
            }
        }
        catch (InvalidOperationException error) { stop = error.Message; }
        return FinishRig221(name, g, seed, rows, stop, dt, gravity);
    }

    private static Rig221 FinishRig221(string name, Geometry215 g, State215 seed, IReadOnlyList<Step215> rows,
        string stop, double dt, double gravity)
    {
        var state = rows.LastOrDefault()?.After ?? seed;
        Vec215 linear = default, angular = default;
        double def = 0, maxDef = 0, air = 0, loaded = 0, peakRoll = 0, peakRate = 0,
            engine = 0, dissipated = 0, steer = 0, rotorWork = 0, axle = 0;
        double cap = 0, peakLoad = 0, spin = 0;
        int touches = 0, lifts = 0;
        bool? wasLoaded = null;
        foreach (var row in rows)
        {
            double span = Duration215(row);
            var f = row.Forces;
            linear += (f.GroundForce + f.Drag - Vec215.Up * (g.Base.Mass * gravity)) * span;
            angular += row.RollPitchYawMoment * span;
            def += row.EnergyResidual;
            maxDef = Math.Max(maxDef, Math.Abs(def));
            axle += f.FrontResponse.RotorTorqueNm * span;
            cap = Math.Max(cap, f.Front.Utilization);
            peakLoad = Math.Max(peakLoad, f.Front.Normal);
            spin = Math.Max(spin, Math.Abs(f.FrontResponse.RollingSlipMetersPerSecond));
            peakRoll = Math.Max(peakRoll, Math.Abs(row.After.Pose.Roll));
            peakRate = Math.Max(peakRate, Math.Abs(RollRate36(row.After, g.Motion(row.After).BodyRate)));
            bool load = f.Front.Normal > 1;
            if (load) loaded += span;
            else air += span;
            if (wasLoaded == false && load) touches++;
            if (wasLoaded == true && !load) lifts++;
            wasLoaded = load;
            engine += f.EnginePower * span;
            dissipated += f.Loss * span;
            steer += f.SteeringPower * span;
            rotorWork += f.FrontResponse.SteeringReactionWatts * span;
        }
        return new(name, g.FrontContact, dt, rows.Count, state.Time - seed.Time, stop, seed, state,
            (state.Momentum - seed.Momentum - linear).Norm, (state.Angular - seed.Angular - angular).Norm, maxDef,
            rows.Count == 0 ? 0 : rows[^1].EnergyAfter - rows[0].EnergyBefore, engine, dissipated, steer, rotorWork,
            state.FrontWheel - seed.FrontWheel, axle, cap, peakLoad, air, loaded, peakRoll, peakRate, spin, touches, lifts,
            g.Motion(state).WorldRate.Z, Speed38(state, g));
    }
}
