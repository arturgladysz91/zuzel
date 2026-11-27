
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Checkpoint38(State215 State, double Distance);
    private sealed record Flight38(Checkpoint38 Final, Step215[] Steps, double[] Distances, string Stop,
        double Boundary, double Ground);
    private sealed record Receipt38(string Stop, int Steps, double Seconds, double VelocityCurvature,
        double InwardAcceleration, double TangentialAcceleration, double FrontLoad, double RearLoad,
        double RearInwardFraction, double RearFx, double RearFy, double YawBand, double RollBand,
        double RearSpin, double SignedSideslip, double AverageFrontUtilization, double AverageRearUtilization,
        double PeakForce, double PeakPower, double ForceLimitSeconds, double PowerLimitSeconds,
        double Slide, double Powered, double LongestPowered, double FullDrivePowered, double FullDriveDeceleration,
        double MaximumEnergyDefect, double MeanSeat, double MeanPegs, double MeanGrip, double MeanFoot,
        Vec215 MeanRider, Vec215 MeanTarget);

    private static Flight38 Window38(Lab38 lab, Geometry215 g, UnifiedModel215 model,
        Checkpoint38 start, Activation215[] commands, double seconds, SpatialBike36? envelope = null,
        bool exact = false, bool forecast = false, double finishAt = double.PositiveInfinity,
        Action<RejectedStep45>? rejected = null, Action<LegacyStep46>? observed = null)
    {
        var shape = envelope ?? H36Footprint;
        var state = start.State;
        var mark = lab.Course.Mark(state, g);
        double t = start.Distance, end = state.Time + seconds;
        double edge = shape.Clearance(lab.Track, g, state, exact);
        double floor = shape.GroundMargin(g, state), minEdge = edge, minGround = floor;
        var steps = new List<Step215>();
        var distances = new List<double>();
        string reason = "verified interval";
        while (state.Time < end - 1e-9 && t < finishAt)
        {
            if (steps.Count > 2200) { reason = "integration step budget"; break; }
            double duration = Math.Min(end - state.Time, forecast ? .005 : StepSeconds216);
            try
            {
                var row = model.Step(state, commands, duration);
                observed?.Invoke(new(state, row, end, duration, t));
                var tail = lab.Course.Mark(row.After, g);
                double nextEdge = shape.Clearance(lab.Track, g, row.After, exact);
                double nextGround = shape.GroundMargin(g, row.After);
                double sweptEdge = Math.Min(edge, nextEdge) - shape.TransportBound(g, row) / 2;
                double sweptGround = Math.Min(floor, nextGround) - shape.TransportBound(g, row, groundOnly: true) / 2;
                string? failure = sweptGround <= 0 ? "swept hard-part ground" : CornerCourse216.Violation(tail, sweptEdge);
                if (failure is not null)
                {
                    reason = failure;
                    rejected?.Invoke(new(failure, t, duration, state, row.After, row, sweptEdge, sweptGround));
                    break;
                }
                t += Frame.UnwrapDelta(mark.Road.ReferenceMeters, tail.Road.ReferenceMeters);
                state = row.After;
                mark = tail;
                edge = nextEdge;
                floor = nextGround;
                minEdge = Math.Min(minEdge, sweptEdge);
                minGround = Math.Min(minGround, sweptGround);
                steps.Add(row);
                distances.Add(t);
            }
            catch (InvalidOperationException error)
            {
                reason = error.Message;
                rejected?.Invoke(new(reason, t, duration, state, (error as ChassisEnvelope36)?.Candidate, null, null, null));
                break;
            }
        }
        return new(new(state, t), steps.ToArray(), distances.ToArray(), reason, minEdge, minGround);
    }

    private static Receipt38 Measure38(Step215[] steps, Geometry215 g, string stop)
    {
        double seconds = 0, curvature = 0, north = 0, tangent = 0, front = 0, rear = 0, shear = 0,
            fx = 0, fy = 0, yaw = 0, roll = 0, spin = 0, beta = 0, fUse = 0, rUse = 0,
            peakForce = 0, peakPower = 0, forceLimit = 0, powerLimit = 0, slide = 0, power = 0,
            longest = 0, continuous = 0, wot = 0, slowFull = 0, ledger = 0, energy = 0, seat = 0, pegs = 0, grips = 0, foot = 0;
        Vec215 rider = default, target = default;
        foreach (var row in steps)
        {
            double dt = Duration215(row);
            var f = row.Forces;
            var velocity = row.Midpoint.Momentum.Plane / g.Base.Mass;
            var dv = (row.After.Momentum - row.Before.Momentum).Plane / (g.Base.Mass * dt);
            double a = velocity.Norm > 1e-9 ? velocity.Cross(dv) / velocity.Norm : 0;
            double ax = velocity.Norm > 1e-9 ? velocity.Dot(dv) / velocity.Norm : 0;
            seconds += dt;
            curvature += dt * a / Math.Max(1e-9, velocity.Dot(velocity));
            north += dt * a;
            tangent += dt * ax;
            front += dt * f.Front.Normal;
            rear += dt * f.Rear.Normal;
            shear += dt * velocity.Cross(f.Rear.Force.Plane) / Math.Max(1e-9, velocity.Norm);
            fx += dt * f.Rear.Longitudinal;
            fy += dt * f.Rear.Lateral;
            yaw += dt * f.FrontYawBand;
            roll += dt * f.FrontRollBand;
            spin += dt * f.RearSlip;
            beta += dt * SlipAngle38(row.After);
            fUse += dt * f.Front.Utilization;
            rUse += dt * f.Rear.Utilization;
            double force = f.Rider.Norm + Math.Abs(f.LeftFoot?.MuscleForce ?? 0);
            double p = f.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(row.ActuatorPower);
            peakForce = Math.Max(peakForce, force);
            peakPower = Math.Max(peakPower, p);
            if (force >= Actuator215.Limited.MaxForce - 1e-6) forceLimit += dt;
            if (p >= Actuator215.Limited.MaxPower - 1e-6) powerLimit += dt;
            if (Slide38(row)) slide += dt;
            if (Powered38(row))
            {
                power += dt;
                continuous += dt;
                longest = Math.Max(longest, continuous);
                if (row.Before.Command.Bike.Drive >= .999f) wot += dt;
            }
            else continuous = 0;
            if (row.Before.Command.Bike.Drive >= .999f && ax < 0) slowFull += dt;
            ledger += row.EnergyResidual;
            energy = Math.Max(energy, Math.Abs(ledger));
            if (f.PassiveSupport is { } s)
            {
                seat += dt * s.Seat.Contact.NormalForce;
                pegs += dt * (s.LeftPeg.Contact.NormalForce + s.RightPeg.Contact.NormalForce);
                grips += dt * (s.LeftGrip.Contact.NormalForce + s.RightGrip.Contact.NormalForce);
            }
            foot += dt * (f.LeftFoot?.Contact.NormalForce ?? 0);
            rider += row.Midpoint.Rider * dt;
            target += row.Before.Command.Target * dt;
        }
        double duration = Math.Max(1e-9, seconds);
        return new(stop, steps.Length, seconds, curvature / duration, north / duration, tangent / duration, front / duration,
            rear / duration, Math.Abs(north) > .01 ? shear / (north * g.Base.Mass) : 0,
            fx / duration, fy / duration, yaw / duration, roll / duration, spin / duration, beta / duration,
            fUse / duration, rUse / duration, peakForce, peakPower, forceLimit, powerLimit, slide, power, longest, wot,
            slowFull, energy, seat / duration, pegs / duration, grips / duration, foot / duration, rider / duration, target / duration);
    }

    private static double Speed38(State215 state, Geometry215 g) => 3.6 * state.Momentum.Plane.Norm / g.Base.Mass;
    private static double Heading38(State215 state) => Math.Atan2(state.Momentum.Y, state.Momentum.X);
    private static double Difference38(double a, double b) => Math.IEEERemainder(a - b, 2 * Math.PI);
    private static Vec215 BodyChoice38(int posture) => posture switch
    {
        1 => new(.105, 0, 0),
        2 => new(.025, 0, -.07),
        3 => new(.045, .065, -.025),
        _ => default
    };

    private static (Checkpoint38 Initial, Activation215[] Pending) ForkAt38(Archive38 a, double distance)
    {
        int i = a.At(distance);
        var state = a.Run.Trace[i].After;
        return (new(state, a.ActualDistance(i)), a.Run.Commands.Where(c => c.Requested <= state.Time + 1e-10).ToArray());
    }

    private static Activation215[] Submit38(Checkpoint38 state, Activation215[] prior, Command215 command) =>
        [.. prior, new(state.State.Time, state.State.Time + ReactionSeconds216, command)];
}
