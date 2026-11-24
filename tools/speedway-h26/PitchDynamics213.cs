using SharedWorldSpeedway;

namespace SpeedwayHandling;

[Flags]
internal enum WheelContact213 { Air = 0, Front = 1, Rear = 2, Both = Front | Rear }

internal readonly record struct PitchState213(double Height, double VerticalSpeed, double Angle, double Rate);
internal readonly record struct Axle213(double X, double Y, double Radius)
{
    internal Axle213 At(double pitch) => new(
        X * Math.Cos(pitch) - Y * Math.Sin(pitch), X * Math.Sin(pitch) + Y * Math.Cos(pitch), Radius);
    internal double Gap(PitchState213 pose) => pose.Height + Y - Radius;
    internal double NormalSpeed(PitchState213 pose) => pose.VerticalSpeed + X * pose.Rate;
}

internal sealed record PitchBike213(double Mass, double Inertia, double Height, Axle213 Front, Axle213 Rear)
{
    internal static PitchBike213 FixedRider(double mass, double riderMass, TwoContactBikeParameters bike,
        double radius, double travel, double lowered)
    {
        const double neutralHeight = .55, bikeHeight = .45, bikeGyration = .50, riderGyration = .32;
        if (riderMass <= 0 || riderMass >= mass || Math.Abs(travel) > .18 || lowered is < 0 or > .14)
            throw new ArgumentException("Fixed rider configuration is outside the study geometry.");
        double bikeMass = mass - riderMass;
        double riderHeight = (mass * neutralHeight - bikeMass * bikeHeight) / riderMass - lowered;
        double height = (bikeMass * bikeHeight + riderMass * riderHeight) / mass;
        double shift = travel * riderMass / mass;
        double separation = riderHeight - bikeHeight;
        double inertia = bikeMass * bikeGyration * bikeGyration + riderMass * riderGyration * riderGyration +
            bikeMass * riderMass / mass * (separation * separation + travel * travel);
        return new(mass, inertia, height, new(bike.FrontAxleFromComMeters - shift, radius - height, radius),
            new(-bike.RearAxleFromComMeters - shift, radius - height, radius));
    }

    internal PitchState213 Rest => new(Height, 0, 0, 0);
}

internal readonly record struct Reactions213(WheelContact213 Mode, double Front, double Rear);
internal readonly record struct PitchLoads213(Reactions213 Normal, double PitchAcceleration,
    double VerticalAcceleration, double FrontGapAcceleration, double RearGapAcceleration);
internal sealed record Impact213(PitchState213 Before, PitchState213 After, Reactions213 Impulse,
    double EnergyLostJoules, double MomentumError, double PitchImpulseError, double WorkIdentityError);

internal static class PitchDynamics213
{
    internal const double Gravity = LoadBasedTyreGrip.GravityMetersPerSecondSquared;
    internal const double GapToleranceMeters = 2e-9;
    private const double RestingNormalSpeed = 2e-7;

    internal static PitchLoads213 Loads(PitchBike213 bike, PitchState213 pose, double freeMoment,
        WheelContact213 eligible = WheelContact213.Both)
    {
        var f = bike.Front.At(pose.Angle);
        var r = bike.Rear.At(pose.Angle);
        double a = freeMoment / bike.Inertia;
        double Free(Axle213 wheel) => -Gravity + wheel.X * a - wheel.Y * pose.Rate * pose.Rate;
        bool Resting(Axle213 wheel) => wheel.Gap(pose) <= GapToleranceMeters &&
            Math.Abs(wheel.NormalSpeed(pose)) <= RestingNormalSpeed;
        var n = Resolve(bike, f, r, Free(f), Free(r),
            eligible.HasFlag(WheelContact213.Front) && Resting(f), eligible.HasFlag(WheelContact213.Rear) && Resting(r));
        double acceleration = a + (f.X * n.Front + r.X * n.Rear) / bike.Inertia;
        double vertical = -Gravity + (n.Front + n.Rear) / bike.Mass;
        double GapAcceleration(Axle213 wheel) => vertical + wheel.X * acceleration - wheel.Y * pose.Rate * pose.Rate;
        return new(n, acceleration, vertical, GapAcceleration(f), GapAcceleration(r));
    }

    internal static Impact213 Land(PitchBike213 bike, PitchState213 pose)
    {
        var f = bike.Front.At(pose.Angle);
        var r = bike.Rear.At(pose.Angle);
        double beforeF = f.NormalSpeed(pose), beforeR = r.NormalSpeed(pose);
        var j = Resolve(bike, f, r, beforeF, beforeR, f.Gap(pose) <= GapToleranceMeters, r.Gap(pose) <= GapToleranceMeters);
        double dv = (j.Front + j.Rear) / bike.Mass;
        double dw = (f.X * j.Front + r.X * j.Rear) / bike.Inertia;
        var after = pose with { VerticalSpeed = pose.VerticalSpeed + dv, Rate = pose.Rate + dw };
        double lost = Kinetic(bike, pose) - Kinetic(bike, after);
        double work = -.5 * (j.Front * (beforeF + f.NormalSpeed(after)) + j.Rear * (beforeR + r.NormalSpeed(after)));
        return new(pose, after, j, lost,
            bike.Mass * (after.VerticalSpeed - pose.VerticalSpeed) - j.Front - j.Rear,
            bike.Inertia * (after.Rate - pose.Rate) - f.X * j.Front - r.X * j.Rear, lost - work);
    }

    private static double Kinetic(PitchBike213 bike, PitchState213 pose) =>
        .5 * (bike.Mass * pose.VerticalSpeed * pose.VerticalSpeed + bike.Inertia * pose.Rate * pose.Rate);

    private static Reactions213 Resolve(PitchBike213 bike, Axle213 f, Axle213 r, double bF, double bR,
        bool eligibleF, bool eligibleR)
    {
        double aFF = 1 / bike.Mass + f.X * f.X / bike.Inertia;
        double aRR = 1 / bike.Mass + r.X * r.X / bike.Inertia;
        double aFR = 1 / bike.Mass + f.X * r.X / bike.Inertia;
        double determinant = aFF * aRR - aFR * aFR;
        const double tolerance = 1e-9;
        if (eligibleF && eligibleR && determinant > 0)
        {
            double jf = (aFR * bR - aRR * bF) / determinant;
            double jr = (aFR * bF - aFF * bR) / determinant;
            if (jf > 0 && jr > 0) return new(WheelContact213.Both, jf, jr);
        }
        if (eligibleF && bF < 0)
        {
            double jf = -bF / aFF;
            if (!eligibleR || bR + aFR * jf >= -tolerance) return new(WheelContact213.Front, jf, 0);
        }
        if (eligibleR && bR < 0)
        {
            double jr = -bR / aRR;
            if (!eligibleF || bF + aFR * jr >= -tolerance) return new(WheelContact213.Rear, 0, jr);
        }
        if ((!eligibleF || bF >= -tolerance) && (!eligibleR || bR >= -tolerance))
            return new(WheelContact213.Air, 0, 0);
        throw new InvalidOperationException("No nonnegative contact complementarity solution.");
    }

    internal static PitchState213 Move(PitchBike213 bike, PitchState213 pose, WheelContact213 support,
        double freeMoment, double seconds)
    {
        if (support == WheelContact213.Both) return pose;
        if (support == WheelContact213.Air)
        {
            double a = freeMoment / bike.Inertia;
            return new(pose.Height + seconds * (pose.VerticalSpeed - .5 * Gravity * seconds),
                pose.VerticalSpeed - Gravity * seconds, pose.Angle + seconds * (pose.Rate + .5 * a * seconds),
                pose.Rate + a * seconds);
        }
        var wheel = support == WheelContact213.Front ? bike.Front : bike.Rear;
        double Accel(double angle, double rate)
        {
            var axle = wheel.At(angle);
            return (freeMoment + bike.Mass * axle.X * (Gravity + axle.Y * rate * rate)) /
                (bike.Inertia + bike.Mass * axle.X * axle.X);
        }
        // Integrate the remaining coordinate on the supported manifold.
        double h = seconds / 2;
        double v1 = pose.Rate, a1 = Accel(pose.Angle, v1);
        double v2 = v1 + h * a1, a2 = Accel(pose.Angle + h * v1, v2);
        double v3 = v1 + h * a2, a3 = Accel(pose.Angle + h * v2, v3);
        double v4 = v1 + seconds * a3, a4 = Accel(pose.Angle + seconds * v3, v4);
        double finalAngle = pose.Angle + seconds * (v1 + 2 * (v2 + v3) + v4) / 6;
        double finalRate = v1 + seconds * (a1 + 2 * (a2 + a3) + a4) / 6;
        var contact = wheel.At(finalAngle);
        return new(contact.Radius - contact.Y, -contact.X * finalRate, finalAngle, finalRate);
    }
}
