
namespace SpeedwayHandling;

internal readonly record struct CompliantContact216(Vec215 Force, Vec215 Normal, double Gap, double NormalForce,
    double StoredEnergy, double Dissipation)
{
    internal static CompliantContact216 Evaluate(double gap, Vec215 gradient, Vec215 velocity,
        double stiffness, double damping, double friction)
    {
        double compression = Math.Max(0, -gap);
        double length = gradient.Norm;
        var normal = gradient / length;
        if (compression == 0) return new(default, normal, gap, 0, 0, 0);

        // Smooth onset lets force vanish at separation even for rapid impacts.
        double rate = gradient.Dot(velocity);
        double damper = damping * Math.Min(1, compression / .003) * Math.Max(0, -rate);
        double normalForce = (stiffness * compression + damper) * length;
        var tangent = velocity - normal * velocity.Dot(normal);
        const double frictionTransitionMetersPerSecond = .045;
        var tangential = tangent * (-friction * normalForce /
            Math.Sqrt(tangent.Dot(tangent) + frictionTransitionMetersPerSecond * frictionTransitionMetersPerSecond));
        return new(normal * normalForce + tangential, normal, gap, normalForce,
            .5 * stiffness * compression * compression, -damper * rate - tangential.Dot(tangent));
    }
}

internal readonly record struct SupportPad216(CompliantContact216 Contact, double SteeringPower, double LegForce)
{
    internal Vec215 Force => Contact.Force;
}

internal sealed record SupportLoads216(SupportPad216 Seat, SupportPad216 LeftPeg, SupportPad216 RightPeg,
    SupportPad216 LeftGrip, SupportPad216 RightGrip)
{
    internal Vec215 Force => Seat.Force + LeftPeg.Force + RightPeg.Force + LeftGrip.Force + RightGrip.Force;
    internal double Energy => Seat.Contact.StoredEnergy + LeftPeg.Contact.StoredEnergy + RightPeg.Contact.StoredEnergy +
        LeftGrip.Contact.StoredEnergy + RightGrip.Contact.StoredEnergy;
    internal double Loss => Seat.Contact.Dissipation + LeftPeg.Contact.Dissipation + RightPeg.Contact.Dissipation +
        LeftGrip.Contact.Dissipation + RightGrip.Contact.Dissipation;
    internal double SteerPower => LeftGrip.SteeringPower + RightGrip.SteeringPower;
    internal double LegForce => Seat.LegForce + LeftPeg.LegForce + RightPeg.LegForce + LeftGrip.LegForce + RightGrip.LegForce;
}

internal sealed record RiderSupport216(double StiffnessScale = 1, double DampingScale = 1, double FrictionScale = 1)
{
    private static readonly Vec215 SteeringPivot = new(.45, 0, .19);
    // Coordinates are measured from the frame or rider COM as indicated, in meters.
    private static readonly Pad Seat = new(new(-.12, 0, .145), new(-.12, 0, -.065),
        .22, .175, .030, .005, 24_000, 570, .56);
    private static readonly Pad LeftPeg = new(new(-.16, .205, -.115), new(-.16, .205, -.321),
        .15, .115, .044, 0, 11_000, 260, .75);
    private static readonly Pad RightPeg = LeftPeg.Reflected();
    private static readonly Pad LeftGrip = new(new(.48, .285, .205), new(.48, .285, .016),
        .13, .14, .042, 0, 7_000, 155, .56);
    private static readonly Pad RightGrip = LeftGrip.Reflected();

    internal SupportLoads216 Evaluate(Vec215 torso, Vec215 rate, double steer, double steerRate,
        Vec215 torsoLegColumn = default, Vec215? leftShoe = null, Vec215 leftShoeRate = default,
        Vec215 leftLegColumn = default) =>
        new(Patch(Seat, torso + Seat.Landmark, rate, torsoLegColumn),
            Patch(LeftPeg, leftShoe ?? torso + LeftPeg.Landmark, leftShoe.HasValue ? leftShoeRate : rate,
                leftShoe.HasValue ? leftLegColumn : torsoLegColumn),
            Patch(RightPeg, torso + RightPeg.Landmark, rate, torsoLegColumn),
            Patch(LeftGrip, torso + LeftGrip.Landmark, rate, torsoLegColumn, steer, steerRate, true),
            Patch(RightGrip, torso + RightGrip.Landmark, rate, torsoLegColumn, steer, steerRate, true));

    internal Vec215 SeatedReference(Geometry215 g, double gravity)
    {
        double lo = -.10, hi = .08;
        for (int i = 0; i < 45; i++)
        {
            double z = (lo + hi) / 2;
            var sample = g.Neutral + Vec215.Up * z;
            if (Evaluate(sample, default, 0, 0).Force.Z > g.Base.RiderMass * gravity) lo = z;
            else hi = z;
        }
        return g.Neutral + Vec215.Up * ((lo + hi) / 2);
    }

    internal State215 PassiveEquilibrium(Geometry215 g, State215 seed, double gravity)
    {
        // The supported motorcycle has tyre sag and a small static pitch. Resolve the
        // two in-plane residuals of the rider, using pad geometry in that pitched frame.
        var r = seed.Rider;
        for (int i = 0; i < 12; i++)
        {
            var candidate = Trial(r);
            var residual = Residual(candidate);
            if (residual.Norm < 1e-8) return candidate;
            const double h = .00001;
            var x = (Residual(Trial(r + Vec215.Forward * h)) - residual) / h;
            var z = (Residual(Trial(r + Vec215.Up * h)) - residual) / h;
            double d = x.X * z.Z - x.Z * z.X;
            if (Math.Abs(d) < 1e-6) break;
            r -= new Vec215((residual.X * z.Z - z.X * residual.Z) / d, 0,
                (x.X * residual.Z - residual.X * x.Z) / d);
        }
        throw new InvalidOperationException("passive static support equilibrium did not converge");

        State215 Trial(Vec215 at) => g.Supported(seed with { Rider = at }, gravity, UnifiedModel215.Stiffness);
        Vec215 Residual(State215 s) => Evaluate(s.Rider, default, 0, 0).Force -
            s.Pose.Body(Vec215.Up) * (g.Base.RiderMass * gravity);
    }

    private SupportPad216 Patch(Pad pad, Vec215 point, Vec215 velocity, Vec215 legColumn,
        double steer = 0, double steerRate = 0, bool steered = false)
    {
        Vec215 steerColumn = steered ? Vec215.Up.Cross(point - SteeringPivot) : default;
        var turned = Attitude215.Pose(steer, 0, 0);
        var delta = steered ? turned.Body(point - SteeringPivot) - (pad.Anchor - SteeringPivot) : point - pad.Anchor;
        var localVelocity = steered ? turned.Body(velocity - steerColumn * steerRate) : velocity;
        double x = delta.X / pad.HalfLength, y = delta.Y / pad.HalfWidth;
        double x2 = x * x, y2 = y * y;
        double gap = delta.Z + pad.EdgeDrop * (x2 * x2 + y2 * y2) - pad.Groove * (x2 + y2);
        var gradient = new Vec215(x * (4 * pad.EdgeDrop * x2 - 2 * pad.Groove) / pad.HalfLength,
            y * (4 * pad.EdgeDrop * y2 - 2 * pad.Groove) / pad.HalfWidth, 1);
        var contact = CompliantContact216.Evaluate(gap, gradient, localVelocity,
            pad.Stiffness * StiffnessScale, pad.Damping * DampingScale, pad.Friction * FrictionScale);
        if (steered) contact = contact with { Force = turned.Apply(contact.Force), Normal = turned.Apply(contact.Normal) };
        return new(contact, contact.Force.Dot(steerColumn) * steerRate, contact.Force.Dot(legColumn));
    }

    private readonly record struct Pad(Vec215 Anchor, Vec215 Landmark, double HalfLength, double HalfWidth,
        double EdgeDrop, double Groove, double Stiffness, double Damping, double Friction)
    {
        internal Pad Reflected() => this with
        {
            Anchor = Anchor with { Y = -Anchor.Y },
            Landmark = Landmark with { Y = -Landmark.Y }
        };
    }
}
