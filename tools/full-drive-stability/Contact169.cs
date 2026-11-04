using System.Numerics;
using CoreSim;
using SharedWorldSpeedway;

namespace FullDrive169;

internal enum TyreVariant { Original, Separated, LateralAllocation }

internal sealed record PhysicsVariant(string Name, double HeightMeters = 0, TyreVariant Tyre = TyreVariant.Original)
{
    public static readonly PhysicsVariant Original = new("original");
    public bool Active => HeightMeters > 0 || Tyre != TyreVariant.Original;
}

internal static class Contact169
{
    public static double Acceleration(RiderView before, RiderView after, float seconds)
        => DynamicAxleLoad.Accepted(before, after, seconds, TwoContactBikeParameters.Default).LongitudinalAcceleration;

    // Same steered point geometry and crank solver as Contact144, restricted to the
    // armed nominal 500 cc / passive-clutch path with explicit steering requests.
    public static TwoContactForces Forces(RiderView body, RiderSpec spec, TrackFrame track,
        TrackStateSnapshot surface, WorldParameters parameters, double time, float mass, RearWheelState wheel,
        TwoContactBikeState steering, float dt, float request, CrankClutch144 crank, double releaseAt,
        PhysicsVariant variant, double acceptedAx)
    {
        if (!variant.Active || !parameters.Enable500CcEngine || !parameters.EnableLoadBasedTyreGrip ||
            parameters.EnableDynamicAxleLoad || parameters.ExperimentalDrive is not null)
            throw new ArgumentException("Explicit experimental 500 cc contact required.");
        var geometry = parameters.TwoContactBike ?? TwoContactBikeParameters.Default;
        var road = body.Road;
        var lane = (float)road.OffsetMeters / LaneModel.UsableRacingWidthMeters(
            track.Track.Segments[road.Segment].Type, track.Track.Geometry) * 4;
        var grip = surface.SampleSurface(road.Segment, Math.Clamp(lane, 0, 4));
        var loads = LoadBasedTyreGrip.Budget(mass, grip, geometry, parameters.LoadBasedTyres);
        AxleLoad? transfer = null;
        if (variant.HeightMeters > 0)
        {
            var estimate = DynamicAxleLoad.AtAcceleration(acceptedAx, geometry, variant.HeightMeters);
            double rearFraction = Math.Clamp(estimate.RearFraction, 0, 1);
            double normal = loads.FrontNormalNewtons + loads.RearNormalNewtons;
            double rear = normal * rearFraction;
            loads = loads with { RearNormalNewtons = rear, FrontNormalNewtons = normal - rear };
            transfer = estimate with { RearFraction = rearFraction };
        }
        float budget = (float)loads.TotalCapacityNewtons;
        float bounded = Math.Clamp(request, -geometry.MaximumSteeringRadians, geometry.MaximumSteeringRadians);
        float after = steering.SteeringRadians + Math.Clamp(bounded - steering.SteeringRadians,
            -geometry.SteeringRateRadiansPerSecond * dt, geometry.SteeringRateRadiansPerSecond * dt);
        float used = (steering.SteeringRadians + after) / 2;
        var forward = new Vector2(MathF.Cos(body.Heading), MathF.Sin(body.Heading));
        var left = new Vector2(-forward.Y, forward.X);
        var frontForward = new Vector2(MathF.Cos(body.Heading + used), MathF.Sin(body.Heading + used));
        var frontLeft = new Vector2(-frontForward.Y, frontForward.X);
        var fp = body.Position + forward * geometry.FrontAxleFromComMeters;
        var rp = body.Position - forward * geometry.RearAxleFromComMeters;
        var fa = fp - body.Position;
        var ra = rp - body.Position;
        var vf = TwoContactForces.PointVelocity(body.Velocity, body.AngularVelocity, fa);
        var vr = TwoContactForces.PointVelocity(body.Velocity, body.AngularVelocity, ra);
        float uf = Vector2.Dot(vf, frontForward);
        float ur = Vector2.Dot(vr, forward);
        float wr = Vector2.Dot(vr, left);
        float fyFront = RearWheelDynamics.FrontLateralForce(Vector2.Dot(vf, frontLeft),
            loads.FrontCapacityNewtons, ScaleY2(uf, budget / mass));
        var front = frontLeft * fyFront;
        var speed = body.Speed;
        var resistance = -body.Velocity * (LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed)
            / MathF.Sqrt(speed * speed + .25f));
        var engine = Engine500Parameters.Default;
        double ratio = engine.ShortOverallRatio + (engine.LongOverallRatio - engine.ShortOverallRatio) * spec.Setup.Gearing;
        var throttle = body.Activity is RiderActivity.Finished or RiderActivity.Crashed ? 0 : body.Drive * (1 - body.Impairment);
        var engineCommand = crank.Prepare(throttle, ratio, engine.Efficiency, dt, time >= releaseAt - 1e-10,
            torque => Wheel(variant.Tyre, ur, wr, wheel.AngularVelocityRadPerSecond, budget, mass, torque,
                Vector2.Dot(resistance + front, forward), dt, wheel.Parameters, loads));
        var solved = crank.Prepared.Wheel;
        var rearForce = forward * solved.LongitudinalNewtons + left * solved.RearLateralNewtons;
        return new(fp, rp, fa, ra, vf, vr, front, rearForce, resistance, forward, frontLeft, left,
            fyFront, solved.RearLateralNewtons, solved.LongitudinalNewtons, budget, request,
            steering.SteeringRadians, used, after, 0)
        {
            AxleLoad = transfer, Engine = engineCommand, TyreLoads = loads
        };
    }

    // Mirrors the #144 front and original rear scale. The separated rear laws
    // use the already frozen #140 material scales instead.
    internal static float ScaleY2(float forward, float accelerationBudget)
        => SlipCoupledDynamics.SlipStiffnessRatio * SlipCoupledDynamics.SlipStiffnessRatio
            * (forward * forward + SlipCoupledDynamics.LowSpeedMetersPerSecond * SlipCoupledDynamics.LowSpeedMetersPerSecond)
            + accelerationBudget * accelerationBudget * SlipCoupledDynamics.MinimumLateralRelaxationSeconds
            * SlipCoupledDynamics.MinimumLateralRelaxationSeconds;

    internal static RearWheelStep Wheel(TyreVariant variant, float ur, double wr, double omega, float budget,
        float mass, double torque, double external, float dt, RearWheelParameters wheel, LoadBasedTyreBudget loads)
    {
        if (variant == TyreVariant.Original)
            return RearWheelDynamics.Evaluate(ur, wr, omega, budget, mass, ScaleY2(ur, budget / mass),
                torque, Math.Abs(torque), torque, external, dt, wheel,
                rearBudgetNewtons: Math.Min(budget, loads.RearCapacityNewtons));

        if (variant == TyreVariant.Separated)
            return WheelSolver140.Evaluate(ur, wr, omega, budget, mass, torque, Math.Abs(torque), torque,
                external, dt, wheel, loads.RearNormalNewtons, loads.EffectiveFrictionCoefficient);

        // Lateral allocation sensitivity. Reserve Fy for the uncombined lateral
        // curve, then solve Fx on the remaining circle. Each pure-slip curve is
        // identical to #140. For any side slip, Fx stays monotone in mid-slip.
        var scales = TyreLaw140.Scales(ur, TyreMaterial140.Reference);
        double b = wr / scales.Y;
        double share = 1 / Math.Sqrt(1 + b * b);
        float fy = (float)(-loads.RearCapacityNewtons * b * share);
        var result = WheelSolver140.Evaluate(ur, 0, omega, budget, mass, torque, Math.Abs(torque), torque,
            external, dt, wheel, loads.RearNormalNewtons * share, loads.EffectiveFrictionCoefficient);
        double utilization = loads.RearCapacityNewtons == 0 ? 0 : Math.Sqrt(
            (double)result.LongitudinalNewtons * result.LongitudinalNewtons + (double)fy * fy) / loads.RearCapacityNewtons;
        return result with { RearLateralNewtons = fy, RearUtilization = utilization };
    }
}
