using CoreSim;
using FullDrive169;
using SharedWorldSpeedway;

namespace Cyclic172;

internal static class AxleContact176
{
    internal static string? InvalidLoad(double acceptedAx, double heightMeters)
    {
        var estimate = DynamicAxleLoad.AtAcceleration(acceptedAx, TwoContactBikeParameters.Default, heightMeters);
        if (estimate.FrontFraction <= 0)
            return FormattableString.Invariant($"FRONT_LIFT: unclamped Fz fraction {estimate.FrontFraction:R}, ax {acceptedAx:R} m/s2");
        if (estimate.RearFraction <= 0)
            return FormattableString.Invariant($"REAR_LIFT: unclamped Fz fraction {estimate.RearFraction:R}, ax {acceptedAx:R} m/s2");
        return null;
    }

    internal static TwoContactForces Forces(RiderView rider, RiderSpec spec, TrackFrame frame, TrackStateSnapshot surface,
        WorldParameters parameters, double time, float mass, RearWheelState wheel, TwoContactBikeState steering,
        float dt, float request, CrankClutch144 crank, double release, double heightMeters, double acceptedAx)
    {
        if (InvalidLoad(acceptedAx, heightMeters) is { } failure)
            throw new InvalidOperationException(failure);
        var f = Contact169.Forces(rider, spec, frame, surface, parameters, time, mass, wheel, steering, dt, request,
            crank, release, new PhysicsVariant("transfer-only", heightMeters), acceptedAx);
        var loads = f.TyreLoads ?? throw new InvalidOperationException("Missing axle loads.");
        var normal = mass * LoadBasedTyreGrip.GravityMetersPerSecondSquared;
        if (loads.FrontNormalNewtons <= 0 || loads.RearNormalNewtons <= 0 ||
            Math.Abs(loads.FrontNormalNewtons + loads.RearNormalNewtons - normal) > 1e-7 ||
            Math.Abs(loads.TotalCapacityNewtons - loads.EffectiveFrictionCoefficient * normal) > 1e-7 ||
            Math.Abs(f.FrontLateral) > loads.FrontCapacityNewtons + .001 ||
            Math.Sqrt(f.RearLateral * (double)f.RearLateral + f.RearLongitudinal * (double)f.RearLongitudinal) >
                loads.RearCapacityNewtons + .001)
            throw new InvalidOperationException("Axle-load or tyre-budget invariant failed.");
        return f;
    }
}
