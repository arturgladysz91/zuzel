
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal readonly record struct Vec215(double X, double Y, double Z)
{
    internal static readonly Vec215 Up = new(0, 0, 1), Left = new(0, 1, 0), Forward = new(1, 0, 0);
    internal double Dot(Vec215 b) => X * b.X + Y * b.Y + Z * b.Z;
    internal Vec215 Cross(Vec215 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    internal double Norm => Math.Sqrt(Dot(this));
    internal Vec214 Plane => new(X, Y);
    public static Vec215 operator +(Vec215 a, Vec215 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec215 operator -(Vec215 a, Vec215 b) => a + -b;
    public static Vec215 operator -(Vec215 a) => a * -1;
    public static Vec215 operator *(Vec215 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public static Vec215 operator /(Vec215 a, double k) => a * (1 / k);
}

internal readonly record struct Attitude215(double W, Vec215 V)
{
    internal Vec215 Apply(Vec215 x)
    {
        var t = V.Cross(x) * 2;
        return x + t * W + V.Cross(t);
    }

    internal Vec215 Body(Vec215 x) => new Attitude215(W, -V).Apply(x);
    internal double Roll => Math.Atan2(Apply(Vec215.Left).Z, Apply(Vec215.Up).Z);
    internal double Pitch => Math.Asin(Math.Clamp(Apply(Vec215.Forward).Z, -1, 1));
    internal double Yaw => Math.Atan2(Apply(Vec215.Forward).Y, Apply(Vec215.Forward).X);
    internal Attitude215 Advance(Vec215 worldRate, double dt) => Rotate(worldRate, dt).Multiply(this);
    internal static Attitude215 Pose(double yaw, double pitch, double roll) =>
        Rotate(Vec215.Up, yaw).Multiply(Rotate(Vec215.Left, -pitch)).Multiply(Rotate(Vec215.Forward, roll));

    private static Attitude215 Rotate(Vec215 axis, double dt)
    {
        double speed = axis.Norm;
        double angle = speed * dt / 2;
        return new(Math.Cos(angle), axis * (speed > 1e-14 ? Math.Sin(angle) / speed : dt / 2));
    }

    private Attitude215 Multiply(Attitude215 b)
    {
        double w = W * b.W - V.Dot(b.V);
        var v = b.V * W + V * b.W + V.Cross(b.V);
        double norm = Math.Sqrt(w * w + v.Dot(v));
        return new(w / norm, v / norm);
    }
}

[Flags]
internal enum Axes215 { Fixed = 0, Longitudinal = 1, Lateral = 2, Vertical = 4, Sagittal = 5, All = 7 }

internal readonly record struct Command215(FreeRacingAction Bike, Vec215 Target)
{
    internal void Validate()
    {
        if (!float.IsFinite(Bike.SteeringRadians) || !float.IsFinite(Bike.Drive) || Bike.Drive is < 0 or > 1 ||
            !double.IsFinite(Target.Norm) || Math.Abs(Target.X) > .22 || Math.Abs(Target.Y) > .20 || Math.Abs(Target.Z) > .18)
            throw new ArgumentOutOfRangeException(nameof(Command215));
    }
}
internal readonly record struct Activation215(double Requested, double Time, Command215 Command);

internal sealed record State215(double Time, Vec215 Center, Vec215 Momentum, Attitude215 Pose, Vec215 Angular,
    Vec215 Rider, Vec215 RiderMomentum, double Crank, double Wheel, double Steer, Command215 Command, int Next = 0);

internal readonly record struct Kinematics215(Vec215 BodyRate, Vec215 WorldRate, Vec215 RiderRate, Vec215 RelativeMomentum);
internal readonly record struct Patch215(Vec215 Arm, Vec215 Radial, Vec215 Axle, Vec215 Forward, Vec215 Side,
    Vec215 Velocity, Vec215 SteerJacobian, double Gap, double NormalSpeed);

internal sealed class Geometry215(Geometry214 inherited, Axes215 axes)
{
    internal const double RollInertia = 12;
    internal const double MaxRoll = Math.PI / 3;
    internal const double MaxPitch = Math.PI / 5;
    internal const double MaxCompression = .075;
    internal Geometry214 Base => inherited;
    internal Axes215 Axes => axes;
    internal Vec215 Neutral => new(inherited.RiderNeutral.X, 0, inherited.RiderNeutral.Y);
    internal Vec215 Central => new(RollInertia, inherited.PitchCentral, inherited.YawCentral);
    internal Vec215 Free(Vec215 v) => new(axes.HasFlag(Axes215.Longitudinal) ? v.X : 0,
        axes.HasFlag(Axes215.Lateral) ? v.Y : 0, axes.HasFlag(Axes215.Vertical) ? v.Z : 0);

    internal Kinematics215 Motion(State215 s)
    {
        double mu = inherited.ReducedMass;
        var p = Free(s.RiderMomentum);
        var right = s.Pose.Body(s.Angular) - s.Rider.Cross(p) - Vec215.Left * (s.Crank + s.Wheel);
        var diag = Central;
        double d12 = 0, d13 = 0, d23 = 0;
        if (!axes.HasFlag(Axes215.Longitudinal)) Add(s.Rider.Cross(Vec215.Forward));
        if (!axes.HasFlag(Axes215.Lateral)) Add(s.Rider.Cross(Vec215.Left));
        if (!axes.HasFlag(Axes215.Vertical)) Add(s.Rider.Cross(Vec215.Up));
        // LDLᵀ solve of the positive definite locked-coordinate inertia.
        double l21 = d12 / diag.X, l31 = d13 / diag.X;
        double d2 = diag.Y - l21 * d12, d3Half = diag.Z - l31 * d13;
        double r2 = right.Y - l21 * right.X, r3 = right.Z - l31 * right.X;
        double c23 = d23 - l31 * d12, l32 = c23 / d2;
        double z = (r3 - l32 * r2) / (d3Half - l32 * c23);
        double y = r2 / d2 - l32 * z;
        var w = new Vec215(right.X / diag.X - l21 * y - l31 * z, y, z);
        var wr = w.Cross(s.Rider);
        return new(w, s.Pose.Apply(w), Free(p / mu - wr), p + (wr - Free(wr)) * mu);

        void Add(Vec215 row)
        {
            diag += new Vec215(row.X * row.X, row.Y * row.Y, row.Z * row.Z) * mu;
            d12 += mu * row.X * row.Y;
            d13 += mu * row.X * row.Z;
            d23 += mu * row.Y * row.Z;
        }
    }

    internal State215 WithRates(State215 s, Vec215 bodyRate, Vec215 riderRate)
    {
        var p = (Free(riderRate) + bodyRate.Cross(s.Rider)) * inherited.ReducedMass;
        var i = Central;
        var spin = new Vec215(i.X * bodyRate.X, i.Y * bodyRate.Y, i.Z * bodyRate.Z);
        return s with { RiderMomentum = Free(p), Angular = s.Pose.Apply(spin + s.Rider.Cross(p) + Vec215.Left * (s.Crank + s.Wheel)) };
    }

    internal Patch215 Contact(State215 s, Kinematics215 motion, bool front, double steerRate)
    {
        var axle = s.Pose.Apply(front ? new(-Math.Sin(s.Steer), Math.Cos(s.Steer), 0) : Vec215.Left);
        var direction = axle.Cross(Vec215.Up);
        if (direction.Norm < .25) throw new InvalidOperationException("thin-ring geometry envelope");
        var radial = (Vec215.Up - axle * axle.Z) * (-inherited.Radius / direction.Norm);
        var point = front ? inherited.FrameFront : inherited.FrameRear;
        var arm = s.Pose.Apply(new Vec215(point.X, 0, point.Y) - s.Rider * inherited.RiderFraction) + radial;
        var steerJacobian = front ? s.Pose.Apply(Vec215.Up).Cross(radial) : default;
        var velocity = s.Momentum / inherited.Mass + motion.WorldRate.Cross(arm) -
            s.Pose.Apply(motion.RiderRate) * inherited.RiderFraction + steerJacobian * steerRate;
        var forward = direction / direction.Norm;
        return new(arm, radial, axle, forward, Vec215.Up.Cross(forward), velocity, steerJacobian,
            s.Center.Z + arm.Z, velocity.Z);
    }

    internal double GapBound(State215 center, Kinematics215 m, bool front, double steerRate, double dt)
    {
        var a = front ? inherited.FrameFront : inherited.FrameRear;
        double speed = m.WorldRate.Norm;
        double axisRate = speed + (front ? Math.Abs(steerRate) : 0);
        double maxAxleZ = Math.Abs(Contact(center, m, front, steerRate).Axle.Z) + axisRate * dt;
        double root = Math.Sqrt(Math.Max(0, 1 - maxAxleZ * maxAxleZ));
        if (root < .25) throw new InvalidOperationException("thin-ring dense geometry envelope");
        double maxArm = (new Vec215(a.X, 0, a.Y) - center.Rider * inherited.RiderFraction).Norm +
            inherited.RiderFraction * m.RiderRate.Norm * dt;
        return speed * speed * maxArm + 2 * speed * inherited.RiderFraction * m.RiderRate.Norm +
            inherited.Radius * axisRate * axisRate * (maxAxleZ / root + 1 / (root * root * root));
    }

    internal double Energy(State215 s, double gravity, double stiffness)
    {
        var m = Motion(s);
        var i = Central;
        var w = m.BodyRate;
        var f = Math.Min(0, Contact(s, m, true, 0).Gap);
        var r = Math.Min(0, Contact(s, m, false, 0).Gap);
        return .5 * (s.Momentum.Dot(s.Momentum) / inherited.Mass + i.X * w.X * w.X +
            i.Y * w.Y * w.Y + i.Z * w.Z * w.Z + m.RelativeMomentum.Dot(m.RelativeMomentum) / inherited.ReducedMass +
            s.Crank * s.Crank / inherited.CrankInertia + s.Wheel * s.Wheel / inherited.WheelInertia +
            stiffness * (f * f + r * r)) + inherited.Mass * gravity * s.Center.Z;
    }

    internal bool ChassisClear(State215 s)
    {
        // Four frame-envelope witnesses with dimensions in metres. Rubber contact is handled separately.
        double Height(Vec215 x) => s.Center.Z + s.Pose.Apply(x - s.Rider * inherited.RiderFraction).Z;
        return Height(new(.46, .19, -.21)) > 0 && Height(new(.46, -.19, -.21)) > 0 &&
            Height(new(-.51, .19, -.21)) > 0 && Height(new(-.51, -.19, -.21)) > 0;
    }

    internal State215 Supported(State215 s, double gravity, double stiffness)
    {
        // Solve static load split from the compressed tyres and zero net pitch moment.
        double lo = -.035, hi = .035;
        for (var i = 0; i < 48; i++)
        {
            double pitch = (lo + hi) / 2;
            var a = s with { Pose = Attitude215.Pose(s.Pose.Yaw, pitch, s.Pose.Roll) };
            var m = Motion(a);
            var f = Contact(a, m, true, 0);
            var r = Contact(a, m, false, 0);
            var forward = Vec214.Axis(a.Pose.Yaw);
            double xf = f.Arm.Plane.Dot(forward), xr = r.Arm.Plane.Dot(forward);
            double nf = -xr / (xf - xr) * inherited.Mass * gravity;
            double z = a.Center.Z - f.Gap - nf / stiffness;
            if (i == 47) return a with { Center = a.Center with { Z = z } };
            if (r.Gap + z - a.Center.Z + (inherited.Mass * gravity - nf) / stiffness > 0) lo = pitch;
            else hi = pitch;
        }
        throw new InvalidOperationException("static support bisection");
    }
}

internal readonly record struct Actuator215(double MaxForce, double MaxPower, double PositionGain, double VelocityGain)
{
    internal static readonly Actuator215 Responsive = new(6000, 4000, 8200, 1000);
    internal static readonly Actuator215 Limited = new(700, 350, 8200, 1000);

    internal Vec215 Force(Geometry215 g, State215 s, Vec215 rate)
    {
        var force = g.Free((g.Neutral + s.Command.Target - s.Rider) * PositionGain - rate * VelocityGain);
        // Bound positive and negative mechanical power. Zero speed only invokes the force bound.
        double power = Math.Abs(force.Dot(rate));
        double factor = Math.Min(1, Math.Min(MaxForce / Math.Max(1e-12, force.Norm), MaxPower / Math.Max(1e-12, power)));
        return force * factor;
    }
}
