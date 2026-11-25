using System.Text.Json.Serialization;

namespace SpeedwayHandling;

internal readonly record struct ForcePoint214(Vec214 Force, Vec214 Arm)
{
    internal double YawMoment => Arm.Cross(Force);
}

internal readonly record struct HorizontalPlan214(double Start, double Duration, Plane214 Initial, double Mass,
    double MidpointYawInertia, ForcePoint214 Front, ForcePoint214 Rear, Vec214 Drag)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ForcePoint214 Foot { get; init; }
    internal Vec214 Force => Front.Force + Rear.Force + Drag + Foot.Force;
    internal double Moment => Front.YawMoment + Rear.YawMoment + Foot.YawMoment;
}

internal sealed record HorizontalReceipt214(double End, Plane214 Plane, Vec214 AppliedImpulse, double AppliedYawImpulse,
    Vec214 CollisionImpulse, double CollisionYawImpulse);

// A concrete native backend needs an atomic event preview/accept pair and collision time in the same clock.
internal interface IHorizontal214
{
    HorizontalReceipt214 Preview(HorizontalPlan214 interval);
    void Commit(HorizontalPlan214 interval, HorizontalReceipt214 receipt);
}

internal sealed class ModelHorizontal214 : IHorizontal214
{
    public HorizontalReceipt214 Preview(HorizontalPlan214 step)
    {
        double dt = step.Duration;
        var linear = step.Force * dt;
        double angular = step.Moment * dt;
        var initial = step.Initial;
        var velocity = initial.Velocity + linear / step.Mass;
        var plane = new Plane214(initial.Position + (initial.Velocity + velocity) * (dt / 2), velocity,
            initial.Yaw + dt * (initial.YawMomentum + angular / 2) / step.MidpointYawInertia,
            initial.YawMomentum + angular);
        return new(step.Start + dt, plane, linear, angular, default, 0);
    }

    public void Commit(HorizontalPlan214 interval, HorizontalReceipt214 receipt) { }
}

internal static class StepContract214
{
    internal static void Validate(HorizontalPlan214 request, HorizontalReceipt214 result)
    {
        if (result.CollisionImpulse != default || result.CollisionYawImpulse != 0)
            throw new InvalidOperationException("H33-U collision response requires synchronized event preview and rollback.");
        var velocityProof = (result.Plane.Velocity - request.Initial.Velocity) * request.Mass;
        double yawProof = result.Plane.YawMomentum - request.Initial.YawMomentum;
        if (result.End != request.Start + request.Duration ||
            (result.AppliedImpulse - request.Force * request.Duration).Norm > 1e-9 ||
            Math.Abs(result.AppliedYawImpulse - request.Moment * request.Duration) > 1e-9 ||
            (result.AppliedImpulse - velocityProof).Norm > 1e-9 ||
            Math.Abs(result.AppliedYawImpulse - yawProof) > 1e-9)
            throw new InvalidOperationException("H33-U horizontal receipt failed clock or impulse balance.");
    }
}

internal static class GapEvent214
{
    // With |g''| <= B the gap cannot go below a chord by more than B*h*h/8.
    // Return the first closing event, including a negative interior with positive endpoints.
    internal static double? First(Func<double, (double Gap, double Rate)> curve, double duration, double bound)
    {
        const double depthTolerance = 1e-12;
        return Search(0, duration, curve(0), curve(duration));

        double? Search(double lo, double hi, (double Gap, double Rate) a, (double Gap, double Rate) b)
        {
            double width = hi - lo;
            double error = bound * width * width / 8;
            if (Math.Min(a.Gap, b.Gap) - error >= -depthTolerance ||
                (a.Gap >= -depthTolerance && a.Rate > bound * width)) return null;
            if (error < depthTolerance / 2 || width <= 1e-10)
            {
                if (b.Gap >= -depthTolerance || b.Rate >= 0) return null;
                for (int i = 0; i < 35 && hi - lo > 1e-11; i++)
                {
                    double mid = (hi + lo) / 2;
                    if (curve(mid).Gap > 0) lo = mid; else hi = mid;
                }
                return hi;
            }
            double center = (lo + hi) / 2;
            var middle = curve(center);
            return Search(lo, center, a, middle) ?? Search(center, hi, middle, b);
        }
    }
}

// Reserved contact geometry boundary. Additional coordinates must supply their own Jacobians,
// inertia and work terms before joining the accepted contact solve.
internal interface IContactGeometryExtension214
{
    IReadOnlyList<ExtensionContact214> Geometry(State214 state);
}

internal sealed record ExtensionContact214(string Name, double Gap, double NormalSpeed,
    IReadOnlyList<double> NormalJacobian);
