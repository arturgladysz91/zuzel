
namespace SpeedwayHandling;

internal static partial class Study191
{
    private static bool ValidateLeg216(Fixture216 fixture, string output)
    {
        var support = NominalSupport216;
        var g = fixture.Geometry(new Leg216());
        var model = fixture.Engine(g, support, Actuator215.Limited, false);
        var baseState = fixture.Rest(g, support, speed: 5, roll: -.36);
        var flight = g.WithRates(baseState with
        {
            Center = new(0, 0, 3), Momentum = new(15, -6, 0), Pose = Attitude215.Pose(.29, .15, -.29),
            Crank = 1.6, Wheel = 10
        }, new(.42, -.18, .26), new(.06, -.02, .01));
        var runs = new List<MechanicalRun216>();
        var free = Run("leg-free-flight", "deploy", g, fixture.Engine(g, support, Actuator215.Limited, false, 0),
            flight, .55, StepSeconds216, .85, 0);
        var noLeg = fixture.Geometry();
        var noLegModel = fixture.Engine(noLeg, support, Actuator215.Limited, false);
        const double pairedHorizon = .36;
        var noBoot = Run("boot-pair", "without-leg", noLeg, noLegModel, baseState, pairedHorizon, StepSeconds216, 0);
        var parked = Run("boot-pair", "parked", g, model, baseState, pairedHorizon, StepSeconds216, 0);
        var deployed = Run("boot-pair", "deployed", g, model, baseState, pairedHorizon, StepSeconds216, .92);
        var refined = Run("boot-pair", "deployed", g, model, baseState, pairedHorizon, .00125, .92);
        // Preserve the terminal modes of the original challenge. The shorter shared
        // window above is only a contact, force and timestep diagnostic.
        Run("boot-loss-of-balance", "without-leg", noLeg, noLegModel, baseState, .49, StepSeconds216, 0);
        Run("boot-loss-of-balance", "parked", g, model, baseState, .49, StepSeconds216, 0);
        Run("boot-loss-of-balance", "deployed", g, model, baseState, .49, StepSeconds216, .92);
        var lowGrip = new Leg216(GroundFriction: .20);
        var highGrip = new Leg216(GroundFriction: .48);
        foreach (var (name, leg) in new[] { ("low-boot-friction", lowGrip), ("high-boot-friction", highGrip) })
        {
            var changed = fixture.Geometry(leg);
            Run("boot-pair", name, changed, fixture.Engine(changed, support, Actuator215.Limited, false),
                baseState, pairedHorizon, StepSeconds216, .92);
        }
        var gates = new Dictionary<string, bool>
        {
            ["airborne-foot-zero-ground-load"] = free is { Stop: "horizon", FootNormalPeak: 0 } &&
                free.Final.Leg.Extension > .20 && free.LinearDefect < 1e-7 && free.AngularDefect < 1e-7,
            ["unilateral-boot-friction"] = runs.All(r => r.Steps.All(row => GoodContact(row.Forces.LeftFoot?.Contact))),
            ["paired-contact"] = deployed is { Stop: "horizon", FootContactSeconds: > .025, FootNormalPeak: > 20 } &&
                noBoot is { Stop: "horizon", FootContactSeconds: 0 } && parked is { Stop: "horizon", FootContactSeconds: 0 },
            ["coupled-time-refinement"] = refined is { Stop: "horizon", FootTouchdownSeconds: > 0 } &&
                Math.Abs(deployed.FootTouchdownSeconds - refined.FootTouchdownSeconds) < .004 &&
                (deployed.FootImpulse - refined.FootImpulse).Norm < 1.5,
            ["load-energy-muscle-budget"] = runs.All(r => r.Summary.LinearDefect < 1e-7 &&
                r.Summary.AngularDefect < 1e-7 && r.Summary.PeakEnergyDefect < .15 &&
                r.Summary.ActiveForce <= 700.00001 && r.Summary.ActivePower <= 350.00001)
        };
        bool pass = gates.Values.All(v => v);
        Save(Path.Combine(output, "leg.json"), new { Pass = pass, Gates = gates, Runs = runs.Select(r => r.Summary).ToArray() });
        Console.WriteLine(string.Join(", ", gates.Select(pair => $"{pair.Key}={pair.Value}")));
        return pass;

        static bool GoodContact(CompliantContact216? contact)
        {
            if (contact is not { } c) return true;
            if (c.Gap >= 0) return c.Force == default && c.Dissipation == 0;
            var tangent = c.Force - c.Normal * c.NormalForce;
            return c.NormalForce >= 0 && c.Dissipation >= 0 && tangent.Norm <= .48000001 * c.NormalForce + 1e-10;
        }

        Mechanical216 Run(string name, string variant, Geometry215 geometry, UnifiedModel215 engine, State215 seed,
            double horizon, double dt, double target, double gravity = Gravity216)
        {
            var command = seed.Command with { LeftFoot = target };
            var run = MechanicalTrial216(name, variant, geometry, engine, seed, horizon, dt,
                Move216(seed, command), null, gravity);
            runs.Add(run);
            Save(Path.Combine(output, $"{name}-{variant}-{Math.Round(dt * 1e6):F0}.json.gz"), run, true);
            var row = run.Summary;
            Console.WriteLine($"{name}/{variant}: {row.Stop}, t={row.Seconds:F3}, " +
                $"q={row.Final.Leg.Extension:F3}, boot={row.FootNormalPeak:F2}N over {row.FootContactSeconds:F4}s");
            return row;
        }
    }
}
