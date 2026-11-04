using System.IO.Compression;
using SharedWorldSpeedway;
using Speed148;

namespace FullDrive169;

internal sealed partial class Study
{
    private bool LoadProbes(ZipArchive history)
    {
        var surface = MultiActionExperiment135.Surface(frame);
        var parameters = FreeLearningExperiment.Parameters();
        var rows = new List<object>();
        bool passes = false;
        foreach (var (id, time) in new[] { ("AB-70-zero-100", 1.44), ("C-166-fixed-100", 10.64), ("C-167-fixed-100", 10.44) })
        {
            var saved = Read<Run144>(history, id + ".json").Riders.Single();
            var q = SampleAt(saved.Samples, time);
            int index = Array.IndexOf(saved.Samples, q);
            var prev = saved.Samples[index - 1];
            var dt = (float)(q.Input.Time - prev.Input.Time);
            double ax = Contact169.Acceleration(prev.Input.Rider, q.Input.Rider, dt);
            var input = q.Input;
            var steer = input.Active.SteeringRadians;
            var body = input.Rider with { Drive = input.Active.Drive };
            var crank = new CrankClutch144(omega: q.CrankOmega);
            var wheel = ForecastFork148.Wheel(input.Omega, parameters.RearWheel);
            var original = Contact144.Forces(body, saved.Spec, frame, surface, parameters, input.Time, 147,
                wheel, new(input.Steering), .01f, steeringOverride: steer, inertia: 52.72097f, powertrain: crank,
                releaseAt: ReactionSeconds);
            foreach (var transfer in Transfers)
            {
                Guard();
                crank = new(omega: q.CrankOmega);
                var altered = Contact169.Forces(body, saved.Spec, frame, surface, parameters, input.Time, 147,
                    wheel, new(input.Steering), .01f, steer, crank, ReactionSeconds, transfer, ax);
                var oldLoads = original.TyreLoads!;
                var newLoads = altered.TyreLoads!;
                if (Math.Abs(oldLoads.FrontNormalNewtons + oldLoads.RearNormalNewtons -
                    newLoads.FrontNormalNewtons - newLoads.RearNormalNewtons) > 1e-7 ||
                    newLoads.FrontNormalNewtons < 0 || newLoads.RearNormalNewtons < 0 ||
                    newLoads.EffectiveFrictionCoefficient != oldLoads.EffectiveFrictionCoefficient ||
                    crank.Prepared.Wheel.RearUtilization > 1.000001 ||
                    crank.Prepared.Wheel.LongitudinalLossJoules < -1e-7)
                    throw new InvalidOperationException("Invalid quasi-static contact probe.");
                double effect = Math.Max(Math.Abs(altered.RearLongitudinal - original.RearLongitudinal),
                    Math.Max(Math.Abs(altered.RearLateral - original.RearLateral),
                        Math.Abs(newLoads.FrontCapacityNewtons - oldLoads.FrontCapacityNewtons)));
                passes |= effect >= 50;
                rows.Add(new { id, Time = input.Time, Ax = ax, transfer.HeightMeters, EffectNewtons = effect,
                    Original = original, Variant = altered, crank.Prepared });
            }
        }
        Save("load-probes.json", new { Passed = passes, Rows = rows });
        return passes;
    }

    private void ContactGrid()
    {
        const float mass = 147;
        var geometry = TwoContactBikeParameters.Default;
        var grip = MultiActionExperiment135.Surface(frame).SampleSurface(0, 2);
        var loads = LoadBasedTyreGrip.Budget(mass, grip, geometry);
        float budget = (float)loads.TotalCapacityNewtons;
        var wheel = RearWheelParameters.Default;
        var grid = new List<object>();
        foreach (var kph in Speeds)
        foreach (var kappa in new[] { 0d, .05, .1, .2, .4, .6, 1 })
        foreach (var alpha in new[] { 0d, 1, 2, 5, 10, 15 })
        foreach (var variant in new[] { Original, Tyres[0], Tyres[1] })
        {
            Guard();
            float u = (float)(kph / 3.6);
            double slip = kappa * Math.Sqrt(u * u + .25);
            double side = u * Math.Tan(alpha * Math.PI / 180);
            RearWheelStep At(double s, double w, RearWheelParameters wp) =>
                Contact169.Wheel(variant.Tyre, u, w, (u + s) / wp.WheelRadiusMeters,
                    budget, mass, 0, 0, 0, wp, loads);

            var p = At(slip, side, wheel);
            var reversed = At(-slip, -side, wheel);
            if (p.LongitudinalNewtons < -1e-6 || p.RearLateralNewtons > 1e-6 ||
                p.RearUtilization > 1.000001 || p.LongitudinalNewtons * slip - p.RearLateralNewtons * side < -1e-6 ||
                Math.Abs(p.LongitudinalNewtons + reversed.LongitudinalNewtons) > 1e-4 ||
                Math.Abs(p.RearLateralNewtons + reversed.RearLateralNewtons) > 1e-4 ||
                (kappa == 0 && Math.Abs(p.LongitudinalNewtons) > 1e-6) ||
                (alpha == 0 && Math.Abs(p.RearLateralNewtons) > 1e-6))
                throw new InvalidOperationException("Contact grid violated sign, passivity or norm.");
            const double epsilon = 1e-5;
            double perturb = u * Math.Tan(epsilon);
            double cr = (At(slip, -perturb, wheel).RearLateralNewtons -
                At(slip, perturb, wheel).RearLateralNewtons) / (2 * epsilon);
            double cf = loads.FrontCapacityNewtons * u / Math.Sqrt(Contact169.ScaleY2(u, budget / mass));
            var pure = At(slip, 0, wheel);
            double cross = geometry.FrontAxleFromComMeters * cf - geometry.RearAxleFromComMeters * cr;
            const double inertia = 52.72097;
            double aa = -(cf + cr + pure.LongitudinalNewtons) / (mass * u);
            double dd = -(geometry.FrontAxleFromComMeters * geometry.FrontAxleFromComMeters * cf +
                geometry.RearAxleFromComMeters * geometry.RearAxleFromComMeters * cr) / (inertia * u);
            double off = (1 + cross / (mass * u * u)) * cross / inertia;
            double eigen = (aa + dd + Math.Sqrt(Math.Max(0, (aa - dd) * (aa - dd) + 4 * off))) / 2;
            var doubleInertia = At(slip, side, wheel with { WheelInertiaKgMetersSquared = 2 * wheel.WheelInertiaKgMetersSquared });
            grid.Add(new { Kph = kph, Kappa = kappa, AlphaDegrees = alpha, Variant = variant.Name,
                Fx = p.LongitudinalNewtons, Fy = p.RearLateralNewtons, p.RearUtilization, Cr = cr,
                MaxLateralAtFx = Math.Sqrt(Math.Max(0, loads.RearCapacityNewtons * loads.RearCapacityNewtons -
                    p.LongitudinalNewtons * p.LongitudinalNewtons)), DissipationWatts = p.LongitudinalNewtons * slip - p.RearLateralNewtons * side,
                MaxEigenRealPerSecond = eigen, FxWithDoubleInertia = doubleInertia.LongitudinalNewtons,
                FyWithDoubleInertia = doubleInertia.RearLateralNewtons, Fz = loads.RearNormalNewtons, Br = loads.RearCapacityNewtons });
        }
        Save("contact-grid.json", grid);
    }
}
