
using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double StepSeconds216 = .0025;
    private const double ReactionSeconds216 = .05;
    private const double Gravity216 = LoadBasedTyreGrip.GravityMetersPerSecondSquared;
    private static readonly RiderSupport216 NominalSupport216 = new();
    private static readonly Actuator215 Inactive216 = Actuator215.Limited with { MaxForce = 0, MaxPower = 0 };

    private sealed record Mechanical216(string Case, string Variant, double StepSeconds, State215 Initial, State215 Final,
        string Stop, double Seconds, int Steps, double ActiveForce, double ActivePower, double ActiveWork,
        double PassiveDissipation, double Seat, double LeftPeg, double RightPeg, double LeftGrip, double RightGrip,
        double FrontAirSeconds, double FrontTouchdownSeconds, double RearTouchdownSeconds, double MinFront, double MinRear,
        double EnergyDefect, double PeakEnergyDefect, double LinearDefect, double AngularDefect,
        double FootTouchdownSeconds, double FootContactSeconds, double FootNormalPeak, Vec215 FootImpulse);
    private sealed record MechanicalRun216(Mechanical216 Summary, Step215[] Steps);
    private sealed record MechanicsReceipt216(bool Pass, Dictionary<string, bool> Gates, double CircleResidual,
        Mechanical216[] Runs);

    private sealed class Fixture216(RiderSpec spec)
    {
        internal Geometry214 Base { get; } = Geometry214.Fixture(Mass, spec.Profile.Gameplay.Physical.MassKg,
            Physics.TwoContactBike ?? TwoContactBikeParameters.Default, Physics.RearWheel ?? RearWheelParameters.Default, Inertia);
        internal double Release => RiderController.ReactionSeconds(spec);
        internal double Ratio => Engine500Parameters.Default.ShortOverallRatio +
            (Engine500Parameters.Default.LongOverallRatio - Engine500Parameters.Default.ShortOverallRatio) * spec.Setup.Gearing;
        internal Geometry215 Geometry(Leg216? leg = null) => new(Base, Axes215.All, leg);

        internal UnifiedModel215 Engine(Geometry215 geometry, RiderSupport216? support, Actuator215 actuator,
            bool drive, double gravity = Gravity216) => new(geometry, spec, Settings202("H28-F"), Physics,
                Surface.SampleSurface(0, 2), actuator, Release, drive, gravity, support: support);

        internal State215 Rest(Geometry215 geometry, RiderSupport216 support, double speed = 0, double roll = 0)
        {
            var r = support.SeatedReference(geometry, Gravity216);
            var s = new State215(0, new(0, 0, .55), new(Base.Mass * speed, 0, 0), Attitude215.Pose(0, 0, roll),
                default, r, default, 0, 0, 0, default);
            return geometry.Supported(s, Gravity216, UnifiedModel215.Stiffness);
        }
    }

    private static void Support216(string root, string output, string stage)
    {
        if (stage is not ("all" or "mechanics" or "leg")) throw new ArgumentException("Use all, mechanics or leg.");
        var (_, spec) = CourseFixture208(root, "C");
        var fixture = new Fixture216(spec);
        Save(Path.Combine(output, "protocol.json"), new
        {
            Base = "6ca0c177dec1c097c8fef0ffb9eb0cc93e1574a7", Geometry = fixture.Base, Gravity216, StepSeconds216,
            ReactionSeconds216, RollInertia = Geometry215.RollInertia, Actuator = Actuator215.Limited,
            Supports = NominalSupport216, Boot = new Leg216(), MaximumProposalsPerSpeed = 100, NativeWorlds = 0
        });
        var mechanics = ValidateMechanics216(fixture, output);
        if (!mechanics.Pass)
        {
            Console.WriteLine("H35-S halted before the leg and racing stages.");
            return;
        }
        if (stage == "mechanics") return;
        var leg = ValidateLeg216(fixture, output);
        if (stage == "all" && leg)
            CornerStudy216(root, output, fixture);
    }

    private static MechanicsReceipt216 ValidateMechanics216(Fixture216 fixture, string output)
    {
        var g = fixture.Geometry();
        var supports = NominalSupport216;
        var rest = fixture.Rest(g, supports);
        var runs = new List<Mechanical216>();
        var grounded = fixture.Engine(g, supports, Actuator215.Limited, false);
        var passive = fixture.Engine(g, supports, Inactive216, false);
        var equilibrium = Run("equilibrium", "nominal", passive, supports.PassiveEquilibrium(g, rest, Gravity216), .35);
        var equilibriumActuator = Run("equilibrium-active", "nominal", grounded, rest, .35);
        var airborne = g.WithRates(rest with
        {
            Center = new(0, 0, 3), Momentum = new(75, -12, 0),
            Pose = Attitude215.Pose(.25, .12, -.21), Crank = 1.6, Wheel = 10
        }, new(.43, -.32, .28), new(.15, -.12, .06));
        var isolated = Run("free-flight", "nominal", fixture.Engine(g, supports, Actuator215.Limited, false, 0),
            airborne, .45, queue: Move216(airborne, new(new(0, 0), new(.12, .12, -.09))), gravity: 0);
        var straight = fixture.Rest(g, supports, speed: 10);
        var left = Run("countersteer-left", "nominal", grounded, straight, .21, queue: Move216(straight, new(new(.11f, 0), default)));
        var right = Run("countersteer-right", "nominal", grounded, straight, .21, queue: Move216(straight, new(new(-.11f, 0), default)));
        var recover = fixture.Rest(g, supports, speed: 11, roll: -.086);
        var recovery = Run("roll-recovery", "nominal", grounded, recover, 1.10,
            feedback: (s, pending) => Recovery216(g, grounded, s, pending));
        var engine = fixture.Engine(g, supports, Actuator215.Limited, true);
        var balance = SteadyCircle215(engine, g, rest, 12, .23, fixture.Release + 1, fixture.Ratio, balanceRider: true);
        Save(Path.Combine(output, "circle.json"), balance);
        Mechanical216? circle = null;
        if (balance.Residual < .005)
            circle = Run("steady-circle", "nominal", engine, balance.State, 1.25);
        var launch = fixture.Rest(g, supports, roll: -.07) with
        {
            Command = new(new(0, 1), default), Crank = 900 * Math.PI / 30 * g.Base.CrankInertia
        };
        var impact = fixture.Rest(g, supports, roll: -.32);
        impact = impact with
        {
            Center = impact.Center + Vec215.Up * .30, Momentum = -Vec215.Up * (g.Base.Mass * .70)
        };
        var posture = Move216(impact, new(new(0, 0), new(.16, .18, -.11)));
        double[] grid = [.005, StepSeconds216, .00125];
        var launchGrid = grid.Select(dt => Run("full-drive-launch", "nominal", engine, launch, .82, dt)).ToArray();
        var dropGrid = grid.Select(dt => Run("leaned-touchdown", "nominal", grounded, impact, .40, dt, posture)).ToArray();
        foreach (var (name, sensitivity) in new (string, RiderSupport216)[]
        {
            ("soft", supports with { StiffnessScale = .65 }), ("stiff", supports with { StiffnessScale = 1.5 }),
            ("low-damping", supports with { DampingScale = .5 }), ("high-damping", supports with { DampingScale = 1.5 }),
            ("low-friction", supports with { FrictionScale = .65 }), ("high-friction", supports with { FrictionScale = 1.35 })
        })
        {
            var varied = fixture.Engine(g, sensitivity, Actuator215.Limited, false);
            var variedRest = sensitivity.PassiveEquilibrium(g, fixture.Rest(g, sensitivity), Gravity216);
            Run("equilibrium", name, fixture.Engine(g, sensitivity, Inactive216, false), variedRest, .35);
            Run("leaned-touchdown", name, varied, impact with
            {
                Rider = sensitivity.SeatedReference(g, Gravity216)
            }, .40, queue: posture);
        }
        var gates = new Dictionary<string, bool>
        {
            ["passive-static-weight"] = equilibrium.Stop == "horizon" && equilibrium.ActiveForce == 0 &&
                equilibrium.Final.Momentum.Norm < 1e-6 && equilibrium.Final.RiderMomentum.Norm < 1e-6 &&
                Math.Abs(equilibrium.Seat + equilibrium.LeftPeg + equilibrium.RightPeg - g.Base.RiderMass * Gravity216) < .001 &&
                equilibriumActuator.ActiveForce < 1,
            ["free-flight-momenta"] = isolated.Stop == "horizon" && isolated.LinearDefect < 1e-7 &&
                isolated.AngularDefect < 1e-7 && isolated.Seat > 1 && Math.Abs(isolated.EnergyDefect) < .04,
            ["countersteer"] = left.Stop == "horizon" && right.Stop == "horizon" &&
                left.Final.Pose.Roll > .025 && right.Final.Pose.Roll < -.025 &&
                Math.Abs(left.Final.Pose.Roll + right.Final.Pose.Roll) < .002,
            ["recovery"] = recovery.Stop == "horizon" && Math.Abs(recovery.Final.Pose.Roll) < .04,
            ["steady-circle"] = circle is { Stop: "horizon" } &&
                Math.Abs(circle.Final.Pose.Roll - circle.Initial.Pose.Roll) < .025 &&
                Math.Abs(circle.Final.Momentum.Plane.Norm / g.Base.Mass - 12) < .1,
            ["launch"] = launchGrid.All(r => r.Stop == "horizon" && r.Final.Momentum.Plane.Norm > 200),
            ["front-touchdown"] = dropGrid.All(r => r.Stop == "horizon" && r.FrontTouchdownSeconds > .1),
            ["time-refinement"] = Math.Abs(dropGrid[1].FrontTouchdownSeconds - dropGrid[2].FrontTouchdownSeconds) < .003 &&
                (launchGrid[1].Final.Momentum - launchGrid[2].Final.Momentum).Norm / g.Base.Mass < .035 &&
                (dropGrid[1].Final.Momentum - dropGrid[2].Final.Momentum).Norm / g.Base.Mass < .035,
            ["energy-and-external-impulse"] = runs.All(r => r.PeakEnergyDefect < .20 &&
                r.LinearDefect < 1e-7 && r.AngularDefect < 1e-7 && r.ActiveForce <= 700.00001 &&
                r.ActivePower <= 350.00001)
        };
        var result = new MechanicsReceipt216(gates.Values.All(v => v), gates, balance.Residual, runs.ToArray());
        Save(Path.Combine(output, "mechanics.json"), result);
        Console.WriteLine(string.Join(", ", gates.Select(pair => $"{pair.Key}={pair.Value}")));
        return result;

        Mechanical216 Run(string name, string variant, UnifiedModel215 model, State215 start, double seconds,
            double dt = StepSeconds216, Activation215[]? queue = null, Func<State215, Activation215[], Command215>? feedback = null,
            double gravity = Gravity216)
        {
            var result = MechanicalTrial216(name, variant, g, model, start, seconds, dt, queue ?? [], feedback, gravity);
            runs.Add(result.Summary);
            Save(Path.Combine(output, $"{name}-{variant}-{Math.Round(dt * 1e6):F0}.json.gz"), result, true);
            var r = result.Summary;
            Console.WriteLine($"{name}/{variant} {dt:R}: {r.Stop} {r.Seconds:F3}s, roll={r.Final.Pose.Roll:F3}, " +
                $"touch={r.FrontTouchdownSeconds:F4}, active={r.ActiveForce:F1}N, energy={r.PeakEnergyDefect:F5}J");
            return r;
        }
    }

    private static Activation215[] Move216(State215 s, Command215 cmd) => [new(s.Time, s.Time + ReactionSeconds216, cmd)];

    private static Command215 Recovery216(Geometry215 geometry, UnifiedModel215 model, State215 s, Activation215[] pending)
    {
        var withoutLead = s.Command with { Bike = new(0, 0), Target = default };
        var best = withoutLead;
        double cost = double.PositiveInfinity;
        // The actual angle servo receives one causal command. Candidates only predict what holding
        // that command after the same 50 ms latency does to roll and its angular velocity.
        foreach (float angle in new[] { -.24f, -.18f, -.12f, -.075f, -.040f, -.018f, 0, .018f, .040f, .075f, .12f, .18f, .24f })
        {
            var command = withoutLead with { Bike = new(angle, 0) };
            Activation215[] events = [.. pending, new(s.Time, s.Time + ReactionSeconds216, command)];
            var forecast = s;
            double end = s.Time + .155, merit = 0;
            try
            {
                while (forecast.Time < end - 1e-9)
                {
                    var row = model.Step(forecast, events, Math.Min(.005, end - forecast.Time));
                    forecast = row.After;
                    merit += forecast.Pose.Roll * forecast.Pose.Roll * Duration215(row);
                }
            }
            catch (InvalidOperationException) { continue; }
            var motion = geometry.Motion(forecast);
            merit += forecast.Pose.Roll * forecast.Pose.Roll + .015 * motion.BodyRate.X * motion.BodyRate.X +
                .002 * motion.WorldRate.Z * motion.WorldRate.Z + .001 * angle * angle;
            if (merit >= cost) continue;
            cost = merit;
            best = command;
        }
        return best;
    }

    private static MechanicalRun216 MechanicalTrial216(string name, string variant, Geometry215 g, UnifiedModel215 model,
        State215 start, double seconds, double dt, Activation215[] queue, Func<State215, Activation215[], Command215>? feedback,
        double gravity)
    {
        var state = start;
        var trace = new List<Step215>();
        double next = start.Time, terminal = start.Time + seconds;
        string stop = "horizon";
        while (state.Time < terminal - 2e-10 && trace.Count < 25_000)
        {
            if (feedback != null && state.Time >= next - 1e-10)
            {
                queue = [.. queue, new(state.Time, state.Time + ReactionSeconds216, feedback(state, queue))];
                next += .025;
            }
            try
            {
                var step = model.Step(state, queue, Math.Min(dt, terminal - state.Time));
                if (step.After.Time <= state.Time) throw new InvalidOperationException("clock stalled");
                state = step.After;
                trace.Add(step);
            }
            catch (InvalidOperationException e) { stop = e.Message; break; }
        }
        if (trace.Count >= 25_000) stop = "step budget";
        double peak = 0, energy = 0, front = -1, rear = -1, foot = -1;
        Vec215 p = default, l = default, boot = default;
        foreach (var row in trace)
        {
            double h = Duration215(row);
            energy += row.EnergyResidual;
            peak = Math.Max(peak, Math.Abs(energy));
            p += (row.Forces.GroundForce + row.Forces.Drag -
                Vec215.Up * (g.Base.Mass * gravity)) * h;
            l += row.RollPitchYawMoment * h;
            boot += (row.Forces.LeftFoot?.Force ?? default) * h;
            foreach (var e in row.Contacts.Where(c => c.Touchdown))
            {
                double time = row.Before.Time + e.Seconds - start.Time;
                if (e.Foot && foot < 0) foot = time;
                if (!e.Foot && e.Front && front < 0) front = time;
                if (!e.Foot && !e.Front && rear < 0) rear = time;
            }
        }
        double Max(Func<Step215, double> f) => trace.Select(f).DefaultIfEmpty().Max();
        double Min(Func<Step215, double> f) => trace.Select(f).DefaultIfEmpty().Min();
        double MaxPad(Func<SupportLoads216, SupportPad216> pad) =>
            Max(r => r.Forces.PassiveSupport is { } loads ? pad(loads).Contact.NormalForce : 0);
        var result = new Mechanical216(name, variant, dt, start, state, stop, state.Time - start.Time, trace.Count,
            Max(r => r.Forces.Rider.Norm + Math.Abs(r.Forces.LeftFoot?.MuscleForce ?? 0)),
            Max(r => r.Forces.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(r.ActuatorPower)),
            trace.Sum(r => r.ActuatorPower * Duration215(r)),
            trace.Sum(r => (r.Forces.PassiveSupport?.Loss ?? 0) * Duration215(r)),
            MaxPad(s => s.Seat), MaxPad(s => s.LeftPeg), MaxPad(s => s.RightPeg), MaxPad(s => s.LeftGrip), MaxPad(s => s.RightGrip),
            trace.Where(r => r.Forces.Front.Normal == 0).Sum(Duration215), front, rear,
            Min(r => r.Forces.Front.Normal), Min(r => r.Forces.Rear.Normal),
            energy, peak, (state.Momentum - start.Momentum - p).Norm, (state.Angular - start.Angular - l).Norm,
            foot, trace.Where(r => r.Forces.LeftFoot?.Contact.NormalForce > 0).Sum(Duration215),
            Max(r => r.Forces.LeftFoot?.Contact.NormalForce ?? 0), boot);
        return new(result, trace.ToArray());
    }
}
