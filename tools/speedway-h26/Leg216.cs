
namespace SpeedwayHandling;

internal readonly record struct LegState216(double Extension, double Momentum);
internal readonly record struct LegGeometry216(Vec215 Separation, Vec215 Derivative, Vec215 SecondDerivative,
    Vec215 Shoe, Vec215 ShoeColumn, Vec215 TorsoShift, Vec215 TorsoColumn, double ReducedMass)
{
    internal Vec215 Coupling => Separation.Cross(Derivative) * ReducedMass;
    internal double Inertia => ReducedMass * Derivative.Dot(Derivative);
    internal Vec215 RelativeVelocity(Vec215 bodyRate, double extensionRate) =>
        bodyRate.Cross(Separation) + Derivative * extensionRate;
}

internal sealed record LegLoads216(CompliantContact216 Contact, Vec215 Arm, Vec215 RelativeForce,
    double GroundGeneralizedForce, double MuscleForce, double MusclePower, double AbsoluteMusclePower, double Rate,
    double StopEnergy, double StopLoss, double MomentumDerivative)
{
    internal Vec215 Force => Contact.Force;
}

internal sealed record Leg216(double DistalMassKg = 2.6, double GroundStiffness = 25_000,
    double GroundDamping = 420, double GroundFriction = .32)
{
    private static readonly Vec215 FoldedShoe = new(-.16, .205, -.321);
    private static readonly Vec215 PathFirst = new(.04, .60, .07);
    private static readonly Vec215 PathSecond = new(.04, -.30, -.45);
    private static readonly Vec215 HipFromTorso = new(-.13, .11, -.105);
    private const double UpperLengthMeters = .41, LowerLengthMeters = .42;
    private const double StopStiffness = 6000, StopDamping = 60;

    internal LegGeometry216 At(Geometry215 g, double q)
    {
        double fraction = DistalMassKg / g.Base.RiderMass, torsoPart = 1 - fraction;
        var shoe = FoldedShoe + PathFirst * q + PathSecond * (q * q);
        var column = PathFirst + PathSecond * (2 * q);
        var separation = shoe / torsoPart;
        return new(separation, column / torsoPart, PathSecond * (2 / torsoPart), shoe, column,
            (FoldedShoe - shoe) * (fraction / torsoPart), column * (-fraction / torsoPart), DistalMassKg * torsoPart);
    }

    internal FootContact215 Foot(Geometry215 g, State215 s, Kinematics215 motion, LegGeometry216 leg) =>
        FootContact215.At(g, s, leg.Shoe, [leg.ShoeColumn], [motion.LegRate], Vec215.Up, 0, motion);

    internal SupportLoads216 Pads(RiderSupport216 supports, Geometry215 g, State215 s, Kinematics215 m, double steerRate)
    {
        var leg = At(g, s.Leg.Extension);
        return supports.Evaluate(s.Rider + leg.TorsoShift, m.RiderRate + leg.TorsoColumn * m.LegRate, s.Steer, steerRate,
            leg.TorsoColumn, s.Rider + leg.Shoe, m.RiderRate + leg.ShoeColumn * m.LegRate, leg.ShoeColumn);
    }

    internal double GapBound(Geometry215 g, State215 start, Kinematics215 motion, double dt)
    {
        var a = At(g, start.Leg.Extension);
        var b = At(g, start.Leg.Extension + dt * motion.LegRate);
        double bodyWeight = 1 - g.Base.RiderFraction;
        double armSpeed = motion.RiderRate.Norm * bodyWeight +
            Math.Max(a.ShoeColumn.Norm, b.ShoeColumn.Norm) * Math.Abs(motion.LegRate);
        double arm = (start.Rider * bodyWeight + a.Shoe).Norm + armSpeed * dt;
        double rate = motion.WorldRate.Norm;
        double pathCurvature = a.SecondDerivative.Norm * (1 - DistalMassKg / g.Base.RiderMass);
        return rate * rate * arm + 2 * rate * armSpeed + pathCurvature * motion.LegRate * motion.LegRate;
    }

    internal double Potential(Geometry215 g, State215 s, Kinematics215 motion)
    {
        double compression = Math.Max(0, -Foot(g, s, motion, At(g, s.Leg.Extension)).Gap);
        return .5 * GroundStiffness * compression * compression + Stop(s.Leg.Extension, 0).Energy;
    }

    internal void Validate(Geometry215 g, State215 s, Kinematics215 m)
    {
        double q = s.Leg.Extension;
        if (!double.IsFinite(q + m.LegRate) || q is < -.14 or > 1.14)
            throw new InvalidOperationException("left leg travel envelope");
        var leg = At(g, q);
        var hipToShoe = leg.Separation - HipFromTorso;
        // Knee flexion is inferred by the two link lengths in the chosen outward/downward gesture.
        double cosKnee = (UpperLengthMeters * UpperLengthMeters + LowerLengthMeters * LowerLengthMeters -
            hipToShoe.Dot(hipToShoe)) / (2 * UpperLengthMeters * LowerLengthMeters);
        if (cosKnee < Math.Cos(164 * Math.PI / 180) || cosKnee > Math.Cos(22 * Math.PI / 180))
            throw new InvalidOperationException("left knee range");
        if (Foot(g, s, m, leg).Gap < -.085)
            throw new InvalidOperationException("left boot compression envelope");
    }

    internal (Vec215 Torso, LegLoads216 Loads) Evaluate(Geometry215 g, State215 s, Kinematics215 m,
        Vec215 seated, Actuator215 active, SupportLoads216? pads)
    {
        var leg = At(g, s.Leg.Extension);
        var torsoRate = m.RiderRate + leg.TorsoColumn * m.LegRate;
        var trunk = g.Free((seated + s.Command.Target - s.Rider - leg.TorsoShift) * active.PositionGain -
            torsoRate * active.VelocityGain);
        double strokeSpeed = m.LegRate * leg.Derivative.Norm;
        double muscle = 460 * (s.Command.LeftFoot - s.Leg.Extension) - 88 * strokeSpeed;
        double power = Math.Abs(trunk.Dot(torsoRate)) + Math.Abs(muscle * strokeSpeed);
        double factor = Math.Min(1, Math.Min(active.MaxForce / Math.Max(1e-12, trunk.Norm + Math.Abs(muscle)),
            active.MaxPower / Math.Max(1e-12, power)));
        trunk *= factor;
        muscle *= factor;
        double muscleWork = trunk.Dot(torsoRate) + muscle * strokeSpeed;
        var point = Foot(g, s, m, leg);
        var ground = CompliantContact216.Evaluate(point.Gap, point.Normal, point.Velocity,
            GroundStiffness, GroundDamping, GroundFriction);
        var reaction = point.Transpose(ground.Force);
        var stop = Stop(s.Leg.Extension, m.LegRate);
        double inertial = leg.ReducedMass * leg.RelativeVelocity(m.BodyRate, m.LegRate).Dot(
            m.BodyRate.Cross(leg.Derivative) + leg.SecondDerivative * m.LegRate);
        double derivative = inertial + reaction.Leg[0] + (pads?.LegForce ?? 0) + stop.Force +
            trunk.Dot(leg.TorsoColumn) + muscle * leg.Derivative.Norm;
        return (trunk, new(ground, point.Arm, reaction.Rider, reaction.Leg[0], muscle, muscleWork, power * factor, m.LegRate,
            stop.Energy, stop.Loss, derivative));
    }

    private static (double Force, double Energy, double Loss) Stop(double q, double rate)
    {
        double excess = q - Math.Clamp(q, 0, 1);
        double closing = Math.Max(0, Math.Sign(excess) * rate);
        double damper = StopDamping * Math.Min(1, Math.Abs(excess) / .015) * closing;
        return (-StopStiffness * excess - Math.Sign(excess) * damper,
            .5 * StopStiffness * excess * excess, damper * closing);
    }
}
