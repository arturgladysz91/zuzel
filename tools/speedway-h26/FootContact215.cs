
namespace SpeedwayHandling;

// Kinematic and impulse rows for a future leg model. The active wheel solver
// supplies neither a foot reaction nor a prescribed correction of rider posture.
internal readonly record struct FootContact215(Vec215 Arm, Vec215 Velocity, Vec215 Normal, Attitude215 Pose,
    double RiderWeight, Vec215[] LegColumns, double Gap)
{
    internal double ClosingVelocity => Velocity.Dot(Normal);

    internal static FootContact215 At(Geometry215 g, State215 s, Vec215 footFromRider,
        Vec215[] bodyLegColumns, double[] legRates, Vec215 unitNormal, double planeOffset,
        Kinematics215? coupledMotion = null)
    {
        if (bodyLegColumns.Length != legRates.Length || Math.Abs(unitNormal.Norm - 1) > 1e-9)
            throw new ArgumentException("Foot contact geometry requires one velocity per leg column and a unit normal.");
        double weight = 1 - g.Base.RiderFraction;
        var arm = s.Pose.Apply(s.Rider * weight + footFromRider);
        var columns = bodyLegColumns.Select(s.Pose.Apply).ToArray();
        var motion = coupledMotion ?? g.Motion(s);
        var velocity = s.Momentum / g.Base.Mass + motion.WorldRate.Cross(arm) + s.Pose.Apply(motion.RiderRate) * weight;
        for (int i = 0; i < columns.Length; i++) velocity += columns[i] * legRates[i];
        return new(arm, velocity, unitNormal, s.Pose, weight, columns, (s.Center + arm).Dot(unitNormal) - planeOffset);
    }

    internal (Vec215 Linear, Vec215 Angular, Vec215 Rider, double[] Leg) Transpose(Vec215 force) =>
        (force, Arm.Cross(force), Pose.Body(force) * RiderWeight, LegColumns.Select(c => c.Dot(force)).ToArray());
}
