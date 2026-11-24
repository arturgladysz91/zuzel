using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal readonly record struct Vec214(double X, double Y)
{
    internal double Dot(Vec214 b) => X * b.X + Y * b.Y;
    internal double Cross(Vec214 b) => X * b.Y - Y * b.X;
    internal double Norm => Math.Sqrt(Dot(this));
    internal Vec214 Left => new(-Y, X);
    internal Vec214 Rotate(double radians) => Axis(radians) * X + Axis(radians).Left * Y;
    internal static Vec214 Axis(double angle) => new(Math.Cos(angle), Math.Sin(angle));
    public static Vec214 operator +(Vec214 a, Vec214 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec214 operator -(Vec214 a, Vec214 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec214 operator *(Vec214 a, double s) => new(a.X * s, a.Y * s);
    public static Vec214 operator /(Vec214 a, double s) => a * (1 / s);
}

internal readonly record struct RiderMotion214(Vec214 Position, Vec214 Velocity, Vec214 Acceleration);
internal readonly record struct Stroke214(double Start, double End, Vec214 From, Vec214 To)
{
    internal const double MaximumSpeed = .70;
    internal const double MaximumAcceleration = 6;
    internal static Stroke214 Stationary(double time, Vec214 at) => new(time, time, at, at);
    internal static Stroke214 Begin(double time, Vec214 from, Vec214 to)
    {
        // Bounds of the quintic first and second derivatives on [0, 1].
        double distance = (to - from).Norm;
        if (distance < 1e-12) return Stationary(time, from);
        double duration = Math.Max(1.875 * distance / MaximumSpeed,
            Math.Sqrt((10 / Math.Sqrt(3)) * distance / MaximumAcceleration));
        return new(time, time + duration, from, to);
    }

    internal RiderMotion214 At(double time)
    {
        if (time >= End) return new(To, default, default);
        if (time <= Start) return new(From, default, default);
        double d = End - Start;
        double t = (time - Start) / d, t2 = t * t, t3 = t2 * t;
        var delta = To - From;
        double blend = t3 * (10 + t * (-15 + 6 * t));
        double rate = 30 * t2 * (1 - t) * (1 - t) / d;
        double accel = 60 * t * (1 - t) * (1 - 2 * t) / (d * d);
        return new(From + delta * blend, delta * rate, delta * accel);
    }
}

internal readonly record struct Command214(FreeRacingAction Bike, Vec214 RiderOffset)
{
    internal void Validate()
    {
        Bike.Validate();
        if (!double.IsFinite(RiderOffset.X) || !double.IsFinite(RiderOffset.Y) ||
            Math.Abs(RiderOffset.X) > .18 || RiderOffset.Y is < -.14 or > .04)
            throw new ArgumentException("Rider request is outside the finite H33-U travel envelope.");
    }
}

internal readonly record struct Queued214(double Requested, double Activates, Command214 Command);
internal readonly record struct Plane214(Vec214 Position, Vec214 Velocity, double Yaw, double YawMomentum);
internal sealed record State214(double Time, Plane214 Plane, double Height, double VerticalMomentum, double Pitch,
    double PitchMomentum, double CrankMomentum, double WheelMomentum, double Steer, double Lean,
    Stroke214 Stroke, Command214 Active, Queued214[] Pending, WheelContact213 Mode, double ReferenceOrigin = 0)
{
    public RiderMotion214 Rider => Stroke.At(Time);

    internal State214 Request(Command214 request, double delay)
    {
        request.Validate();
        if (!double.IsFinite(delay) || delay < 0)
            throw new ArgumentException("Finite nonnegative command latency required.");
        double due = Time + delay;
        if (Pending.Length > 0 && due < Pending[^1].Activates)
            throw new InvalidOperationException("Commands must preserve activation order.");
        return this with { Pending = [.. Pending, new(Time, due, request)] };
    }

    internal State214 Activate(Vec214 neutral)
    {
        int count = 0;
        while (count < Pending.Length && Pending[count].Activates <= Time + 1e-11) count++;
        var current = count == 0 ? this : this with
        {
            Active = Pending[count - 1].Command,
            Pending = Pending[count..]
        };
        if (current.Stroke.End > Time + 1e-11) return current;
        var target = neutral + current.Active.RiderOffset;
        if ((target - current.Stroke.To).Norm < 1e-12) return current;
        return current with { Stroke = Stroke214.Begin(Time, current.Stroke.To, target) };
    }
}

internal readonly record struct WheelGeometry214(Vec214 Axle, double Radius, double Gap, double GapRate)
{
    internal double PitchLever => Radius - Axle.Y;
}

internal sealed record Geometry214(double Mass, double RiderMass, double FrameHeight, double PitchCentral,
    double YawCentral, Vec214 RiderNeutral, Vec214 FrameFront, Vec214 FrameRear, double Radius,
    double CrankInertia, double WheelInertia)
{
    internal double RiderFraction => RiderMass / Mass;
    internal double ReducedMass => RiderMass * (1 - RiderFraction);

    internal static Geometry214 Fixture(double mass, double riderKg, TwoContactBikeParameters b, RearWheelParameters w,
        double yaw)
    {
        double body = mass - riderKg;
        return new(mass, riderKg, .45, .25 * body + .32 * .32 * riderKg, yaw,
            new(0, (.55 - .45) * mass / riderKg),
            new(b.FrontAxleFromComMeters, w.WheelRadiusMeters - .45),
            new(-b.RearAxleFromComMeters, w.WheelRadiusMeters - .45), w.WheelRadiusMeters,
            LaunchParameters144.Default.CrankInertiaKgMetersSquared, w.WheelInertiaKgMetersSquared);
    }

    internal double PitchInertia(Vec214 r) => PitchCentral + ReducedMass * r.Dot(r);
    internal double YawInertia(Vec214 r, double theta)
    {
        double x = r.Rotate(theta).X;
        return YawCentral + ReducedMass * x * x;
    }
    internal double CrossMomentum(RiderMotion214 rider) => ReducedMass * rider.Position.Left.Dot(rider.Velocity);
    internal double PitchRate(State214 state) =>
        (state.PitchMomentum + state.CrankMomentum + state.WheelMomentum - CrossMomentum(state.Rider)) /
        PitchInertia(state.Rider.Position);
    internal double YawRate(State214 state) => state.Plane.YawMomentum / YawInertia(state.Rider.Position, state.Pitch);

    internal WheelGeometry214 Contact(State214 state, WheelContact213 side)
    {
        var rider = state.Rider;
        var axle = Axle(rider.Position, state.Pitch, side);
        var motion = rider.Velocity.Rotate(state.Pitch);
        return new(axle, Radius, state.Height + axle.Y - Radius,
            state.VerticalMomentum / Mass + axle.X * PitchRate(state) - RiderFraction * motion.Y);
    }

    internal Vec214 Axle(Vec214 r, double theta, WheelContact213 side) =>
        ((side == WheelContact213.Front ? FrameFront : FrameRear) - r * RiderFraction).Rotate(theta);

    internal double YawPitchDerivative(State214 state)
    {
        var d = state.Rider.Position.Rotate(state.Pitch);
        double rate = YawRate(state);
        return -ReducedMass * d.X * d.Y * rate * rate;
    }

    internal double Energy(State214 s, double gravity)
    {
        double pitch = PitchRate(s), vertical = s.VerticalMomentum / Mass;
        var rider = s.Rider;
        var orbital = rider.Velocity + rider.Position.Left * pitch;
        double yaw = YawRate(s);
        return .5 * (Mass * (s.Plane.Velocity.Dot(s.Plane.Velocity) + vertical * vertical)
            + PitchCentral * pitch * pitch + YawInertia(rider.Position, s.Pitch) * yaw * yaw +
            ReducedMass * orbital.Dot(orbital) + s.CrankMomentum * s.CrankMomentum / CrankInertia +
            s.WheelMomentum * s.WheelMomentum / WheelInertia) + Mass * gravity * s.Height;
    }
}

internal readonly record struct Ground214(WheelContact213 Mode, double Front, double Rear);
internal sealed record Landing214(Ground214 Impulse, State214 Before, State214 After,
    Vec214 RiderImpulse, double RiderWork, double Dissipated, double EnergyResidual);

internal static class Contact214
{
    internal const double GapTolerance = 2e-9;
    private const double NormalSpeedTolerance = 4e-7;

    internal static Ground214 Loads(Geometry214 g, State214 s, double pitchHard, double verticalFree,
        WheelContact213? fixedMode = null)
    {
        var f = g.Contact(s, WheelContact213.Front);
        var r = g.Contact(s, WheelContact213.Rear);
        var motion = s.Rider;
        var sr = motion.Velocity.Rotate(s.Pitch);
        var ar = motion.Acceleration.Rotate(s.Pitch);
        double omega = g.PitchRate(s);
        double i = g.PitchInertia(motion.Position);
        bool AtRest(WheelGeometry214 w) => w.Gap <= GapTolerance && Math.Abs(w.GapRate) < NormalSpeedTolerance;
        double Free(WheelGeometry214 w) => verticalFree + w.Axle.X * pitchHard / i -
            w.Axle.Y * omega * omega - g.RiderFraction * (ar.Y + 2 * omega * sr.X);
        return Balance(g.Mass, i, f.Axle.X, r.Axle.X, Free(f), Free(r),
            fixedMode ?? ((AtRest(f) ? WheelContact213.Front : 0) | (AtRest(r) ? WheelContact213.Rear : 0)),
            fixedMode.HasValue);
    }

    internal static Landing214 Impact(Geometry214 g, State214 s, double gravity)
    {
        var f = g.Contact(s, WheelContact213.Front);
        var r = g.Contact(s, WheelContact213.Rear);
        var j = Balance(g.Mass, g.PitchInertia(s.Rider.Position), f.Axle.X, r.Axle.X, f.GapRate, r.GapRate,
            (f.Gap <= GapTolerance ? WheelContact213.Front : 0) | (r.Gap <= GapTolerance ? WheelContact213.Rear : 0), false);
        var after = s with
        {
            VerticalMomentum = s.VerticalMomentum + j.Front + j.Rear,
            PitchMomentum = s.PitchMomentum + f.Axle.X * j.Front + r.Axle.X * j.Rear
        };
        double dw = g.PitchRate(after) - g.PitchRate(s);
        var normalJac = new Vec214(-g.RiderFraction * Math.Sin(s.Pitch), -g.RiderFraction * Math.Cos(s.Pitch));
        var riderImpulse = s.Rider.Position.Left * (g.ReducedMass * dw) - normalJac * (j.Front + j.Rear);
        double actuatorWork = riderImpulse.Dot(s.Rider.Velocity);
        double dissipation = -.5 * (j.Front * (f.GapRate + g.Contact(after, WheelContact213.Front).GapRate) +
            j.Rear * (r.GapRate + g.Contact(after, WheelContact213.Rear).GapRate));
        return new(j, s, after, riderImpulse, actuatorWork, dissipation,
            g.Energy(after, gravity) - g.Energy(s, gravity) - actuatorWork + dissipation);
    }

    private static Ground214 Balance(double mass, double inertia, double x, double y, double b, double c,
        WheelContact213 enabled, bool fixedMode)
    {
        double a = 1 / mass + x * x / inertia, d = 1 / mass + y * y / inertia;
        double m = 1 / mass + x * y / inertia;
        const double tolerance = 1e-9;
        if (enabled == WheelContact213.Both)
        {
            double determinant = a * d - m * m;
            double f = (m * c - d * b) / determinant, r = (m * b - a * c) / determinant;
            if (fixedMode || (f > 0 && r > 0)) return new(enabled, f, r);
        }
        if (fixedMode)
            return new(enabled, enabled == WheelContact213.Front ? -b / a : 0, enabled == WheelContact213.Rear ? -c / d : 0);
        if (enabled.HasFlag(WheelContact213.Front) && b < 0 &&
            (!enabled.HasFlag(WheelContact213.Rear) || c - m * b / a >= -tolerance))
            return new(WheelContact213.Front, -b / a, 0);
        if (enabled.HasFlag(WheelContact213.Rear) && c < 0 &&
            (!enabled.HasFlag(WheelContact213.Front) || b - m * c / d >= -tolerance))
            return new(WheelContact213.Rear, 0, -c / d);
        if ((!enabled.HasFlag(WheelContact213.Front) || b >= -tolerance) &&
            (!enabled.HasFlag(WheelContact213.Rear) || c >= -tolerance)) return default;
        throw new InvalidOperationException("H33-U normal complementarity failed.");
    }
}
