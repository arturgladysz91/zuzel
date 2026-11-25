
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed class ChassisEnvelope36(State215 candidate) : InvalidOperationException("chassis-ground envelope")
{
    internal State215 Candidate => candidate;
}

internal readonly record struct SpatialCapsule36(Vec215 A, Vec215 B, double Radius, bool GroundHard)
{
    internal MechanicalCapsule Project() => new(new(A.X, A.Y), new(B.X, B.Y), Radius);
    internal double Bottom => Math.Min(A.Z, B.Z) - Radius;
}

internal sealed record SpatialBike36(double BarWidth = .80, double EngineRadius = .115,
    double EngineCenterHeight = -.145, double RightPegReach = .28, double RiderScale = 1)
{
    internal const double RequiredSweptMeters = .50;
    internal const int RingSections = 12;
    private const double TyreSection = .05;
    private static readonly Vec215 SteeringPivot = new(.45, 0, .19);
    internal const double FastDiskRadius = 1.65;
    private readonly record struct BodyPart(Vec215 A, Vec215 B, double Radius);

    private BodyPart[] HardParts() =>
    [
        new(new(-.31, 0, EngineCenterHeight), new(.27, 0, EngineCenterHeight), EngineRadius),
        new(new(-.53, 0, -.060), new(-.18, 0, .17), .070),
        new(new(-.15, 0, .17), new(.48, 0, .11), .075),
        new(new(-.16, -.055, -.115), new(-.16, -RightPegReach, -.115), .022),
        new(new(-.16, .055, -.115), new(-.16, .215, -.115), .022),
        new(new(.39, 0, .14), new(.69, 0, -.087), .049)
    ];

    internal double GroundMargin(Geometry215 g, State215 s)
    {
        var frame = s.Center - s.Pose.Apply(s.Rider) * g.Base.RiderFraction;
        double minimum = double.PositiveInfinity;
        foreach (var part in HardParts())
            minimum = Math.Min(minimum, frame.Z + Math.Min(s.Pose.Apply(part.A).Z, s.Pose.Apply(part.B).Z) - part.Radius);
        var bar = Bar(s.Steer);
        return Math.Min(minimum, frame.Z + Math.Min(s.Pose.Apply(bar.A).Z, s.Pose.Apply(bar.B).Z) - bar.Radius);
    }

    internal SpatialCapsule36[] Parts(Geometry215 g, State215 s)
    {
        var parts = new List<SpatialCapsule36>(40);
        var frame = s.Center - s.Pose.Apply(s.Rider) * g.Base.RiderFraction;
        var rider = s.Center + s.Pose.Apply(s.Rider) * (1 - g.Base.RiderFraction);
        var leg = g.Leg?.At(g, s.Leg.Extension);
        Vec215 torso = leg?.TorsoShift ?? default;
        foreach (var part in HardParts()) FramePart(part);
        FramePart(Bar(s.Steer));
        Wheel(true);
        Wheel(false);
        RiderPart(torso + new Vec215(-.11, 0, .04), torso + new Vec215(.16, 0, .34), .175);
        RiderPart(torso + new Vec215(.20, 0, .42), torso + new Vec215(.24, 0, .49), .13);
        for (int sign = -1; sign <= 1; sign += 2)
        {
            var hand = SteeringPivot + Steer(new(.03, sign * (BarWidth / 2 - .035), .015), s.Steer);
            parts.Add(new(rider + s.Pose.Apply(torso + new Vec215(.14, sign * .19, .28)),
                frame + s.Pose.Apply(hand), .072 * RiderScale, false));
            var hip = torso + new Vec215(-.10, sign * .13, -.09);
            var shoe = sign == 1 && leg is { } active ? active.Shoe : torso + new Vec215(-.16, sign * .205, -.321);
            // A knee envelope spans the flexed and extended leg. The exact boot point is
            // the contact Jacobian's shoe point, with the protective volume above its sole.
            var knee = (shoe + hip) * .5 + new Vec215(.22, sign * .075, .045);
            RiderPart(hip, knee, .108);
            RiderPart(knee, shoe + Vec215.Up * .064, .080);
            RiderPart(shoe + new Vec215(-.075, 0, .060), shoe + new Vec215(.105, 0, .060), .064);
        }
        return parts.ToArray();

        void FramePart(BodyPart part) =>
            parts.Add(new(frame + s.Pose.Apply(part.A), frame + s.Pose.Apply(part.B), part.Radius, true));

        void RiderPart(Vec215 a, Vec215 b, double r) =>
            parts.Add(new(rider + s.Pose.Apply(a), rider + s.Pose.Apply(b), r * RiderScale, false));

        void Wheel(bool front)
        {
            var point = front ? g.Base.FrameFront : g.Base.FrameRear;
            var hub = frame + s.Pose.Apply(new Vec215(point.X, 0, point.Y));
            var direction = s.Pose.Apply(front ? Steer(Vec215.Forward, s.Steer) : Vec215.Forward);
            var up = s.Pose.Apply(Vec215.Up);
            double centerRadius = g.Base.Radius - TyreSection;
            double sagitta = centerRadius * (1 - Math.Cos(Math.PI / RingSections));
            var prior = hub + direction * centerRadius;
            for (int i = 1; i <= RingSections; i++)
            {
                double angle = 2 * Math.PI * i / RingSections;
                var next = hub + (direction * Math.Cos(angle) + up * Math.Sin(angle)) * centerRadius;
                parts.Add(new(prior, next, TyreSection + sagitta, false));
                prior = next;
            }
        }
    }

    internal double TransportBound(Geometry215 g, Step215 row, bool groundOnly = false)
    {
        double seconds = row.After.Time - row.Before.Time;
        var m = g.Motion(row.Midpoint);
        // All endpoints start within this arm length. Along an accepted dense step
        // rider position and joint coordinate vary linearly, so a first derivative
        // bound covers their intermediate offsets as well.
        double radius = Math.Max(MaxArm(g, row.Before), MaxArm(g, row.After));
        double legSpeed = 0;
        if (g.Leg is { } leg)
        {
            var a = leg.At(g, row.Before.Leg.Extension);
            var b = leg.At(g, row.After.Leg.Extension);
            legSpeed = Math.Max(a.ShoeColumn.Norm, b.ShoeColumn.Norm) * Math.Abs(m.LegRate);
            legSpeed = Math.Max(legSpeed, Math.Max(a.TorsoColumn.Norm, b.TorsoColumn.Norm) * Math.Abs(m.LegRate));
        }
        double relative = m.RiderRate.Norm * Math.Max(g.Base.RiderFraction, 1 - g.Base.RiderFraction) + legSpeed;
        double steer = Math.Abs(row.Forces.SteerRate) * (BarWidth / 2 + .35);
        var translation = row.After.Center - row.Before.Center;
        return (groundOnly ? Math.Abs(translation.Z) : translation.Norm) +
            seconds * (m.WorldRate.Norm * (radius + seconds * (relative + steer)) + relative + steer);
    }

    internal double Clearance(RacingLineGeometry track, Geometry215 g, State215 s, bool exact = true)
    {
        if (!exact)
        {
            var point = new MeterPoint(s.Center.X, s.Center.Y);
            double broad = track.ProjectedClearance([new(point, point, FastDiskRadius)]);
            if (broad > 1) return broad;
        }
        var parts = Parts(g, s);
        Span<MechanicalCapsule> projected = stackalloc MechanicalCapsule[parts.Length];
        for (int i = 0; i < parts.Length; i++) projected[i] = parts[i].Project();
        return track.ProjectedClearance(projected);
    }

    private double MaxArm(Geometry215 g, State215 s) =>
        // Maximum undeformed distance of any frame or rider landmark from its own COM.
        // Steering can move the handlebars and ring about their respective anchors.
        Math.Max(1.12 + g.Base.RiderFraction * s.Rider.Norm,
            .95 * Math.Max(1, RiderScale) + (1 - g.Base.RiderFraction) * s.Rider.Norm);

    private BodyPart Bar(double steer) => new(
        SteeringPivot + Steer(new(.03, -(BarWidth / 2 - .025), .015), steer),
        SteeringPivot + Steer(new(.03, BarWidth / 2 - .025, .015), steer), .025);

    private static Vec215 Steer(Vec215 p, double steer) =>
        new(Math.Cos(steer) * p.X - Math.Sin(steer) * p.Y, Math.Sin(steer) * p.X + Math.Cos(steer) * p.Y, p.Z);
}
