using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record Step214(State214 Before, State214 Midpoint, State214 After, Forces214 Forces, HorizontalPlan214 Plan,
    HorizontalReceipt214 Horizontal, WheelGeometry214 Front, WheelGeometry214 Rear, Landing214? Landing,
    Vec214 RiderForce, double RiderWork, double NormalWork, double EnergyBefore, double EnergyAfter,
    double EnergyResidual, double PitchImpulseResidual, double VerticalImpulseResidual, int Subdivisions);

internal sealed class UnifiedModel214(Geometry214 geometry, ForcesModel214 forces, IHorizontal214 horizontal, double release)
{
    // Event certification budget for the demonstrated reduced domain. State sampling below guards the
    // corresponding angular-rate and curvature envelope. A larger envelope requires a fresh bound.
    private const double GapCurvatureBound = 4096;
    private const double MinimumInterval = 1e-10;
    private const double ReleaseResolution = 2e-6;
    private static readonly ModelHorizontal214 Preview = new();

    internal Step214 Step(State214 input, double requestedInterval, double impairment)
    {
        if (!double.IsFinite(requestedInterval) || requestedInterval is <= 0 or > .020001)
            throw new ArgumentOutOfRangeException(nameof(requestedInterval));
        var start = input.Activate(geometry.RiderNeutral);
        double duration = requestedInterval;
        if (start.Pending.Length > 0) duration = Math.Min(duration, start.Pending[0].Activates - start.Time);
        if (start.Stroke.End > start.Time + 1e-10) duration = Math.Min(duration, start.Stroke.End - start.Time);
        if (release > start.Time + 1e-10) duration = Math.Min(duration, release - start.Time);
        int subdivisions = 0;

        while (true)
        {
            try
            {
                var initial = forces.Solve(start, start, duration, null, impairment);
                var support = initial.Normal.Mode;
                var (current, midpoint) = Midpoint(start, initial, duration, support, impairment);
                var end = Pose(start, current, duration, support);
                var tail = forces.NormalAt(end, current, support);
                var head = forces.NormalAt(start, current);
                bool signChange = tail.Front < -1e-7 || tail.Rear < -1e-7 || head.Mode != support;
                if (current.Normal.Front < -1e-7 || current.Normal.Rear < -1e-7 ||
                    (signChange && duration > ReleaseResolution))
                    throw new Subdivide214("normal reaction changes inside an interval");
                if (current.Normal.Front < 0 || current.Normal.Rear < 0)
                    throw new Subdivide214("negative midpoint normal reaction");

                double? impactTime = FirstImpact(start, current, support, duration);
                if (impactTime is double hit && hit < duration - 1e-9)
                {
                    if (hit < MinimumInterval && duration > MinimumInterval)
                    {
                        duration = Math.Max(hit, MinimumInterval);
                        subdivisions++;
                        continue;
                    }
                    if (hit >= MinimumInterval)
                    {
                        duration = hit;
                        subdivisions++;
                        continue;
                    }
                    // At a contact boundary use the exact sub-microsecond event on the prepared force path.
                    duration = hit;
                    end = Pose(start, current, duration, support);
                    midpoint = Pose(start, current, duration / 2, support);
                }
                return Accept(start, midpoint, end, current, duration, support, impactTime.HasValue, subdivisions);
            }
            catch (Subdivide214) when (duration > MinimumInterval)
            {
                duration /= 2;
                subdivisions++;
            }
        }
    }

    private (Forces214 Force, State214 Midpoint) Midpoint(State214 start, Forces214 initial, double duration,
        WheelContact213 support, double impairment)
    {
        var estimate = Pose(start, initial, duration / 2, support);
        for (int i = 0; i < 16; i++)
        {
            var force = forces.Solve(start, estimate, duration, support, impairment);
            var updated = Pose(start, force, duration / 2, support);
            if ((estimate.Plane.Velocity - updated.Plane.Velocity).Norm < 1e-8 &&
                Math.Abs(estimate.Plane.Yaw - updated.Plane.Yaw) < 1e-9 &&
                Math.Abs(estimate.Plane.YawMomentum - updated.Plane.YawMomentum) < 1e-7 &&
                Math.Abs(estimate.Pitch - updated.Pitch) < 1e-9 &&
                Math.Abs(geometry.PitchRate(estimate) - geometry.PitchRate(updated)) < 1e-8)
                return (force, estimate);
            estimate = updated;
        }
        throw new Subdivide214("H33-U midpoint state and projected force did not converge.");
    }

    private double? FirstImpact(State214 start, Forces214 f, WheelContact213 support, double duration)
    {
        if (support == WheelContact213.Both) return null;
        double? first = null;
        foreach (var side in new[] { WheelContact213.Front, WheelContact213.Rear })
        {
            if (support.HasFlag(side)) continue;
            var hit = GapEvent214.First(t =>
            {
                var s = Pose(start, f, t, support, planar: false);
                var q = geometry.Contact(s, side);
                if (Math.Abs(geometry.PitchRate(s)) > 10 || Math.Abs(s.Pitch) > 1.1)
                    throw new InvalidOperationException("H33-U pitch event-envelope limit.");
                return (q.Gap, q.GapRate);
            }, duration, GapCurvatureBound);
            if (hit.HasValue && (!first.HasValue || hit < first)) first = hit;
        }
        return first;
    }

    private Step214 Accept(State214 start, State214 middle, State214 end, Forces214 f, double dt,
        WheelContact213 support, bool landing, int subdivisions)
    {
        var plan = Plan(start, f, dt, geometry.YawInertia(middle.Rider.Position, middle.Pitch));
        var receipt = horizontal.Preview(plan);
        StepContract214.Validate(plan, receipt);
        var preImpact = end with { Plane = receipt.Plane, Steer = f.Steer, Lean = f.Lean, Mode = support };
        var hit = landing ? Contact214.Impact(geometry, preImpact, forces.Gravity) : null;
        var final = hit is { } l ? l.After with { Mode = l.Impulse.Mode } : preImpact;
        var contactF = geometry.Contact(final, WheelContact213.Front);
        var contactR = geometry.Contact(final, WheelContact213.Rear);
        if (Math.Min(contactF.Gap, contactR.Gap) < -Contact214.GapTolerance)
            throw new InvalidOperationException("An H33-U accepted step penetrated the ground envelope.");

        var mf = geometry.Contact(middle, WheelContact213.Front);
        var mr = geometry.Contact(middle, WheelContact213.Rear);
        double moment = mf.Axle.X * f.Normal.Front + mr.Axle.X * f.Normal.Rear;
        double dPitch = forces.FreeMoment(middle, f.PitchTorque, f.CrankTorque, f.WheelTorque);
        double dd = (dPitch + moment) / geometry.PitchInertia(middle.Rider.Position);
        var riderForce = RiderForce(middle, f, dd);
        double riderWork = dt * riderForce.Dot(middle.Rider.Velocity);
        double normalWork = dt * (mf.GapRate * f.Normal.Front + mr.GapRate * f.Normal.Rear);
        double initialEnergy = geometry.Energy(start, forces.Gravity);
        double finalEnergy = geometry.Energy(final, forces.Gravity);
        double defect = finalEnergy - initialEnergy - (f.EnginePower - f.Dissipation) * dt -
            riderWork - normalWork - (hit?.RiderWork ?? 0) + (hit?.Dissipated ?? 0);
        double verticalDefect = preImpact.VerticalMomentum - start.VerticalMomentum -
            (f.Normal.Front + f.Normal.Rear - geometry.Mass * forces.Gravity) * dt;
        double pitchDefect = preImpact.PitchMomentum - start.PitchMomentum -
            (f.PitchTorque + moment + geometry.YawPitchDerivative(middle)) * dt;
        horizontal.Commit(plan, receipt);
        return new(start, middle, final, f, plan, receipt, contactF, contactR, hit, riderForce, riderWork, normalWork,
            initialEnergy, finalEnergy, defect, pitchDefect, verticalDefect, subdivisions);
    }

    private Vec214 RiderForce(State214 s, Forces214 f, double pitchAcceleration)
    {
        var m = s.Rider;
        double rate = geometry.PitchRate(s), yaw = geometry.YawRate(s);
        double mu = geometry.ReducedMass;
        var relative = (m.Velocity + m.Position.Left * rate) * mu;
        var change = (m.Acceleration + m.Position.Left * pitchAcceleration + m.Velocity.Left * rate) * mu;
        double x = m.Position.Rotate(s.Pitch).X;
        var kineticGradient = relative.Left * -rate + new Vec214(Math.Cos(s.Pitch), -Math.Sin(s.Pitch)) * (mu * x * yaw * yaw);
        var external = new Vec214((f.Front.Force + f.Rear.Force).Dot(Vec214.Axis(s.Plane.Yaw)),
            f.Normal.Front + f.Normal.Rear).Rotate(-s.Pitch) * -geometry.RiderFraction;
        return change - kineticGradient - external;
    }

    private HorizontalPlan214 Plan(State214 start, Forces214 f, double dt, double yawInertia) =>
        new(start.Time, dt, start.Plane, geometry.Mass, yawInertia, f.Front, f.Rear, f.Drag);

    private State214 Pose(State214 start, Forces214 f, double dt, WheelContact213 support, bool planar = true)
    {
        bool air = support == WheelContact213.Air, both = support == WheelContact213.Both;
        double prior = start.PitchMomentum;
        if (!air && !both)
            prior -= geometry.Contact(start, support).Axle.X * start.VerticalMomentum;
        if (both) return Lift(dt, start.Pitch, prior, planar);
        var k1 = Rates(0, start.Pitch, prior);
        var k2 = Rates(dt / 2, start.Pitch + dt / 2 * k1.X, prior + dt / 2 * k1.Y);
        var k3 = Rates(dt / 2, start.Pitch + dt / 2 * k2.X, prior + dt / 2 * k2.Y);
        var k4 = Rates(dt, start.Pitch + dt * k3.X, prior + dt * k3.Y);
        var sum = (k1 + (k2 + k3) * 2 + k4) * (dt / 6);
        return Lift(dt, start.Pitch + sum.X, prior + sum.Y, planar);

        Vec214 Rates(double time, double theta, double reduced)
        {
            var s = Lift(time, theta, reduced, planar: false);
            double w = geometry.PitchRate(s);
            double moment = f.PitchTorque + geometry.YawPitchDerivative(s);
            if (air) return new(w, moment);
            var ax = geometry.Contact(s, support).Axle;
            double axleSpeed = -ax.Y * w - geometry.RiderFraction * s.Rider.Velocity.Rotate(s.Pitch).X;
            return new(w, moment + ax.X * geometry.Mass * forces.Gravity - s.VerticalMomentum * axleSpeed);
        }

        State214 Lift(double time, double theta, double reduced, bool planar)
        {
            double clock = start.Time + time;
            var r = start.Stroke.At(clock);
            double pitchInertia = geometry.PitchInertia(r.Position);
            double yawInertia = geometry.YawInertia(r.Position, theta);
            double crank = start.CrankMomentum + f.CrankTorque * time;
            double wheel = start.WheelMomentum + f.WheelTorque * time;
            double cross = geometry.CrossMomentum(r);
            double v0 = start.VerticalMomentum / geometry.Mass;
            double z = start.Height + time * (v0 - .5 * forces.Gravity * time);
            double pz = start.VerticalMomentum - time * geometry.Mass * forces.Gravity;
            double pitch = reduced;
            if (!air)
            {
                var ax = geometry.Axle(r.Position, theta, both ? WheelContact213.Rear : support);
                double recoil = geometry.RiderFraction * r.Velocity.Rotate(theta).Y;
                double rate = both ? 0 :
                    (reduced + crank + wheel - cross + geometry.Mass * ax.X * recoil) /
                    (pitchInertia + geometry.Mass * ax.X * ax.X);
                z = geometry.Radius - ax.Y;
                pz = geometry.Mass * (recoil - ax.X * rate);
                pitch = pitchInertia * rate + cross - crank - wheel;
            }
            var plane = planar ? Preview.Preview(Plan(start, f, time, yawInertia)).Plane :
                start.Plane with { YawMomentum = start.Plane.YawMomentum + time * (f.Front.YawMoment + f.Rear.YawMoment) };
            return start with { Time = clock, Plane = plane, Height = z, VerticalMomentum = pz, Pitch = theta,
                PitchMomentum = pitch, CrankMomentum = crank, WheelMomentum = wheel, Mode = support };
        }
    }
}
