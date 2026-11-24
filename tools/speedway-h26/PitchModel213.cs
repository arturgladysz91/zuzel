using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record State213(double Time, RiderView Body, PitchState213 Pitch, double Crank,
    double Wheel, double Steer, double Lean);
internal sealed record Forces213(PitchLoads213 Loads, WheelContact213 Support, PitchState213 EndPitch,
    Vector2 Front, Vector2 Rear, Vector2 Drag, double YawMoment, double FreePitchMoment,
    double FrontMomentMinimum, double FrontMomentMaximum, double Steer, double Lean,
    double CrankAfter, double WheelAfter, double Rpm, double GrossTorque, double NetTorque, double ClutchTorque,
    double TransmissionTorque, double WheelSlip, double RearLongitudinal, double RearLateral,
    double EngineWork, double EngineLoss, double ClutchLoss, double TransmissionLoss, double BearingLoss,
    double FrontSlipLoss, double RearSlipLoss, double DragLoss, double NormalWork, int Iterations);
internal sealed record Step213(State213 Before, State213 After, FreeRacingAction Active, Forces213 Forces,
    Impact213? Impact, double FrontGap, double RearGap, double EnergyResidual, double VerticalImpulseResidual,
    double PitchImpulseResidual, double PlanarSweep);

/// <summary>Opt-in, contact-resolving vertical/pitch model with planar yaw and fixed rider mass distribution.</summary>
internal sealed class PitchModel213(PitchBike213 bike, TrackFrame frame, RiderSpec rider, TrackSurfaceState surface,
    HandlingSettings settings, WorldParameters physics, double yawInertia, double release)
{
    private readonly RearWheelParameters wheel = physics.RearWheel ?? RearWheelParameters.Default;
    private readonly TwoContactBikeParameters steering = physics.TwoContactBike ?? TwoContactBikeParameters.Default;
    private readonly double mu = LoadBasedTyreGrip.Budget((float)bike.Mass, surface,
        physics.TwoContactBike ?? TwoContactBikeParameters.Default, physics.LoadBasedTyres).EffectiveFrictionCoefficient *
        settings.GripScale;
    private readonly RacingLineGeometry boundary = new(frame);
    private readonly Engine500Parameters transmission = Engine500Parameters.Default;
    private readonly EngineLossH26 loss = settings.H26!.EngineLoss!;
    private readonly LaunchParameters144 launch = LaunchParameters144.Default;
    private const double MinimumInterval = 1e-10;
    private const double MomentScale = .02;
    private readonly record struct Coupling213(Vector2 Acceleration, double Yaw, double Moment)
    {
        internal double[] Coordinates() => [Acceleration.X, Acceleration.Y, Yaw, Moment * MomentScale];
        internal static Coupling213 At(double[] x) => new(new((float)x[0], (float)x[1]), x[2], x[3] / MomentScale);
        internal double[] Defect(Coupling213 other) => [other.Acceleration.X - Acceleration.X,
            other.Acceleration.Y - Acceleration.Y, other.Yaw - Yaw, (other.Moment - Moment) * MomentScale];
    }

    internal Step213 Advance(State213 state, FreeRacingAction action, double seconds)
    {
        if (seconds is <= 0 or > .020001) throw new ArgumentOutOfRangeException(nameof(seconds));
        action.Validate();
        var forces = Prepare(state, action, seconds);
        bool landing = ClosingGap(forces) < -1e-12;
        if (landing)
        {
            double lo = 0, hi = seconds;
            for (int i = 0; i < 40 && hi - lo > MinimumInterval; i++)
            {
                double mid = (hi + lo) / 2;
                var trial = Prepare(state, action, mid);
                if (ClosingGap(trial) <= 0) { hi = mid; forces = trial; }
                else lo = mid;
            }
            seconds = hi;
        }
        var pose = forces.EndPitch;
        Impact213? impact = landing ? PitchDynamics213.Land(bike, pose) : null;
        double jump = impact is null ? 0 : impact.After.Rate - pose.Rate;
        var net = forces.Front + forces.Rear + forces.Drag;
        var body = state.Body;
        double dt = seconds;
        var dv = net * (float)(dt / bike.Mass);
        var position = body.Position + (body.Velocity + dv / 2) * (float)dt;
        var road = frame.Project(position);
        double reference = body.TotalReferenceMeters + frame.UnwrapDelta(body.Road.ReferenceMeters, road.ReferenceMeters);
        double yawDelta = dt * forces.YawMoment / yawInertia;
        var next = body with
        {
            Position = position, Velocity = body.Velocity + dv, Road = road, TotalReferenceMeters = reference,
            DistanceMeters = body.DistanceMeters + Vector2.Distance(body.Position, position),
            Heading = (float)BikeAngles.Wrap(body.Heading + dt * (body.AngularVelocity + yawDelta / 2)),
            AngularVelocity = (float)(body.AngularVelocity + yawDelta), Drive = action.Drive,
            Context = frame.Context(road, reference)
        };
        var after = new State213(state.Time + dt, next, impact?.After ?? pose, forces.CrankAfter + jump,
            forces.WheelAfter + jump, forces.Steer, forces.Lean);
        double dissipated = forces.EngineLoss + forces.ClutchLoss + forces.TransmissionLoss + forces.BearingLoss +
            forces.FrontSlipLoss + forces.RearSlipLoss + forces.DragLoss + (impact?.EnergyLostJoules ?? 0);
        var n = forces.Loads.Normal;
        double verticalError = bike.Mass * (pose.VerticalSpeed - state.Pitch.VerticalSpeed) -
            dt * (n.Front + n.Rear - bike.Mass * PitchDynamics213.Gravity);
        var center = PitchDynamics213.Move(bike, state.Pitch, forces.Support, forces.FreePitchMoment, dt / 2);
        double pitchError = bike.Inertia * (pose.Rate - state.Pitch.Rate) - dt * (forces.FreePitchMoment +
            bike.Front.At(center.Angle).X * n.Front + bike.Rear.At(center.Angle).X * n.Rear);
        double sweep = Safety153.Clearance153.SweepLowerBound(body, next, dt, boundary.Clearance(body), boundary.Clearance(next));
        return new(state, after, action, forces, impact, bike.Front.At(after.Pitch.Angle).Gap(after.Pitch),
            bike.Rear.At(after.Pitch.Angle).Gap(after.Pitch),
            Energy(after) - Energy(state) - forces.EngineWork + dissipated - forces.NormalWork,
            verticalError, pitchError, sweep);
    }

    internal double Energy(State213 state)
    {
        var p = state.Pitch;
        double crank = state.Crank - p.Rate, rim = state.Wheel - p.Rate;
        double x = state.Body.Velocity.X, y = state.Body.Velocity.Y, yaw = state.Body.AngularVelocity;
        return .5 * (bike.Mass * (x * x + y * y + p.VerticalSpeed * p.VerticalSpeed) +
            yawInertia * yaw * yaw + bike.Inertia * p.Rate * p.Rate +
            launch.CrankInertiaKgMetersSquared * crank * crank + wheel.WheelInertiaKgMetersSquared * rim * rim) +
            bike.Mass * PitchDynamics213.Gravity * p.Height;
    }

    private double ClosingGap(Forces213 trial)
    {
        var pose = trial.EndPitch;
        double f = trial.Support.HasFlag(WheelContact213.Front) ? double.PositiveInfinity :
            bike.Front.At(pose.Angle).Gap(pose);
        double r = trial.Support.HasFlag(WheelContact213.Rear) ? double.PositiveInfinity :
            bike.Rear.At(pose.Angle).Gap(pose);
        return Math.Min(f, r);
    }

    private Forces213 Prepare(State213 state, FreeRacingAction action, double dt)
    {
        double half = dt / 2, mass = bike.Mass;
        double gripAcceleration = mu * PitchDynamics213.Gravity;
        double ratio = transmission.ShortOverallRatio +
            (transmission.LongOverallRatio - transmission.ShortOverallRatio) * rider.Setup.Gearing;
        double throttle = action.Drive * (1d - state.Body.Impairment);
        bool released = state.Time >= release - 1e-10;
        var body = state.Body;
        double speed = Math.Max(0, body.Speed);
        double maxYaw = 1.27 * gripAcceleration / Math.Max(4, speed);
        double yawTarget = Math.Clamp(speed * Math.Tan(action.SteeringRadians) / (bike.Front.X - bike.Rear.X), -maxYaw, maxYaw)
            - 2.8 * Math.CopySign(Math.Max(0, Math.Abs(body.SlideRadians) - .24), body.SlideRadians);
        double momentTarget = yawInertia * (yawTarget - body.AngularVelocity) / settings.YawResponseSeconds;
        double leanTarget = Math.Clamp(Math.Atan2(speed * yawTarget, PitchDynamics213.Gravity),
            -settings.MaximumLeanRadians, settings.MaximumLeanRadians);
        double lean = state.Lean + Math.Clamp((leanTarget - state.Lean) * dt / settings.LeanResponseSeconds,
            -settings.LeanRateRadiansPerSecond * dt, settings.LeanRateRadiansPerSecond * dt);
        double leanFraction = Math.Abs((state.Lean + lean) / 2) / settings.MaximumLeanRadians;
        double frontSlip = .115 * (1 - .13 * leanFraction);
        double rearSlip = .104 * (1 - .25 * leanFraction);

        Coupling213 guess = default;
        double difference = double.PositiveInfinity;
        for (int i = 1; i <= 150; i++)
        {
            var (result, forces) = Evaluate(guess, i);
            double[] defect = guess.Defect(result);
            difference = Norm(defect);
            if (difference <= 2e-5) return forces! with { Iterations = i };
            if (i > 24 && i % 4 == 1 && TryNewton(guess, defect, Evaluate, out var improved))
            {
                guess = improved;
                continue;
            }
            double relaxation = i <= 24 ? .65 : i <= 70 ? .30 : .12;
            double[] coordinate = guess.Coordinates();
            for (int j = 0; j < coordinate.Length; j++) coordinate[j] += relaxation * defect[j];
            guess = Coupling213.At(coordinate);
        }
        throw new PitchConvergence213(difference);

        (Coupling213 Next, Forces213? Report) Evaluate(Coupling213 value, int report)
        {
            var (acceleration, yaw, moment) = value;
            var support = PitchDynamics213.Loads(bike, state.Pitch, moment).Normal.Mode;
            var p = PitchDynamics213.Move(bike, state.Pitch, support, moment, half);
            var end = PitchDynamics213.Move(bike, state.Pitch, support, moment, dt);
            var normal = PitchDynamics213.Loads(bike, p, moment, support);
            double pitchChange = end.Rate - state.Pitch.Rate;
            var f = bike.Front.At(p.Angle);
            var r = bike.Rear.At(p.Angle);
            double heading = body.Heading + half * (body.AngularVelocity + .5 * half * yaw);
            double yawRate = body.AngularVelocity + half * yaw;
            Vector2 forward = Axis(heading), left = Left(forward);
            Vector2 v = body.Velocity + acceleration * (float)half;
            Vector2 vf = v + (float)(f.X * yawRate) * left + (float)((f.Radius - f.Y) * p.Rate) * forward;
            Vector2 vr = v + (float)(r.X * yawRate) * left - (float)(r.Y * p.Rate) * forward;
            double vR = Vector2.Dot(vr, forward) + r.Radius * p.Rate;
            double sideR = Vector2.Dot(vr, left);
            double lateral = sideR / HandlingContact.Scale(vR, rearSlip, gripAcceleration);
            double capacity = mu * normal.Normal.Rear;
            var shaft = new CrankClutch144(launch, state.Crank + pitchChange / 2, loss);
            var power = shaft.Evaluate64(throttle, ratio, transmission.Efficiency, dt, released,
                torque => RearWheelDynamics.EvaluateCombined64(vR, lateral, state.Wheel + pitchChange / 2,
                    mass, capacity, torque, 0, dt, wheel), shaft.EngineMidpointEvaluator64(throttle, dt));
            double fxR = power.Wheel.Force;
            double fyR = RearWheelDynamics.CoupledLateralForce(capacity, lateral, power.Wheel);
            Vector2 rear = (float)fxR * forward + (float)fyR * left;
            double frontCapacity = mu * normal.Normal.Front;
            double steer = SteeringTarget(state.Steer, action.SteeringRadians, vf, forward, left, frontCapacity,
                (momentTarget - r.X * fyR) / Math.Max(.1, f.X), frontSlip, gripAcceleration, dt);
            Vector2 FrontAt(double angle)
            {
                var axis = Axis(heading + angle);
                var cross = Left(axis);
                double side = Vector2.Dot(vf, cross);
                double scale = HandlingContact.Scale(Vector2.Dot(vf, axis), frontSlip, gripAcceleration);
                return (float)(-frontCapacity * side / Math.Sqrt(side * side + scale * scale)) * cross;
            }
            Vector2 front = FrontAt((steer + state.Steer) / 2);
            double frontForwardForce = Vector2.Dot(front, forward);
            double yawMoment = f.X * Vector2.Dot(front, left) + r.X * fyR;
            double rpm = power.EngineMid * 30 / Math.PI;
            double mechanical = loss.Mechanical(power.EngineMid);
            double pump = loss.Pumping(power.EngineMid, throttle);
            double gross = throttle * (Engine500.Torque(rpm) + mechanical);
            double netTorque = gross - mechanical - pump;
            double wheelTorque = power.Torque * ratio * (power.Torque >= 0 ? transmission.Efficiency : 1 / transmission.Efficiency);
            double bearing = wheel.BearingResistanceNewtonMeterSeconds * power.Wheel.MidOmega;
            double pitchTorque = (f.Radius - f.Y) * frontForwardForce + (r.Radius - r.Y) * fxR +
                netTorque - power.Torque + wheelTorque - bearing - r.Radius * fxR;
            Vector2 drag = v * (float)(-(LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons +
                LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient * Vector2.Dot(v, v)) /
                Math.Sqrt(Vector2.Dot(v, v) + .25));
            Vector2 nextAcceleration = (front + rear + drag) / (float)mass;
            double nextYaw = yawMoment / yawInertia;
            var updated = new Coupling213(nextAcceleration, nextYaw, pitchTorque);
            if (report > 0 && Norm(value.Defect(updated)) <= 2e-5)
            {
                double rim = power.Wheel.MidOmega, engine = power.EngineMid;
                double slipping = r.Radius * rim - vR;
                double clearanceRange = steering.SteeringRateRadiansPerSecond * half;
                var limits = Enumerable.Range(0, 9).Select(j => f.X * Vector2.Dot(FrontAt(
                    Math.Clamp(state.Steer + clearanceRange * (j / 4d - 1), -steering.MaximumSteeringRadians,
                        steering.MaximumSteeringRadians)), left)).ToArray();
                return (updated, new(normal, support, end, front, rear, drag, yawMoment, moment, limits.Min(), limits.Max(),
                    steer, lean, 2 * engine - state.Crank, 2 * rim - state.Wheel, rpm, gross, netTorque,
                    power.Torque, wheelTorque, slipping / Math.Sqrt(vR * vR + wheel.LowSpeedMetersPerSecond * wheel.LowSpeedMetersPerSecond),
                    fxR, fyR, gross * engine * dt, (mechanical + pump) * engine * dt,
                    power.Torque * (engine - ratio * rim) * dt, (power.Torque * ratio - wheelTorque) * rim * dt,
                    bearing * rim * dt, -Vector2.Dot(front, vf) * dt,
                    (fxR * slipping - fyR * sideR) * dt, -Vector2.Dot(drag, v) * dt,
                    (normal.Normal.Front * f.NormalSpeed(p) + normal.Normal.Rear * r.NormalSpeed(p)) * dt, report));
            }
            return (updated, null);
        }
    }

    private static double Norm(double[] vector) => vector.Max(x => Math.Abs(x));

    private static bool TryNewton(Coupling213 at, double[] residual,
        Func<Coupling213, int, (Coupling213 Next, Forces213? Report)> evaluate, out Coupling213 result)
    {
        const int n = 4;
        const double probe = .0004;
        double[] center = at.Coordinates();
        double[,] jacobian = new double[n, n + 1];
        for (int column = 0; column < n; column++)
        {
            var sample = (double[])center.Clone();
            sample[column] += probe;
            var plus = Coupling213.At(sample);
            var error = plus.Defect(evaluate(plus, 0).Next);
            for (int row = 0; row < n; row++) jacobian[row, column] = (error[row] - residual[row]) / probe;
        }
        for (int row = 0; row < n; row++) jacobian[row, n] = -residual[row];
        result = at;
        for (int column = 0; column < n; column++)
        {
            int pivot = column;
            for (int row = column + 1; row < n; row++)
                if (Math.Abs(jacobian[row, column]) > Math.Abs(jacobian[pivot, column])) pivot = row;
            if (Math.Abs(jacobian[pivot, column]) < 1e-6) return false;
            for (int k = column; k <= n; k++)
                (jacobian[pivot, k], jacobian[column, k]) = (jacobian[column, k], jacobian[pivot, k]);
            for (int row = 0; row < n; row++)
            {
                if (row == column) continue;
                double factor = jacobian[row, column] / jacobian[column, column];
                for (int k = column; k <= n; k++) jacobian[row, k] -= factor * jacobian[column, k];
            }
        }
        for (double scale = 1; scale >= .125; scale /= 2)
        {
            var candidate = (double[])center.Clone();
            for (int i = 0; i < n; i++) candidate[i] += scale * jacobian[i, n] / jacobian[i, i];
            var trial = Coupling213.At(candidate);
            if (Norm(trial.Defect(evaluate(trial, 0).Next)) >= Norm(residual)) continue;
            result = trial;
            return true;
        }
        return false;
    }

    private double SteeringTarget(double previous, double request, Vector2 v, Vector2 forward, Vector2 left,
        double capacity, double target, double slip, double gripAcceleration, double dt)
    {
        double steer = request, bound = steering.MaximumSteeringRadians;
        double speed = v.Length();
        if (capacity > 0 && speed >= .5)
        {
            double angle = Math.Atan2(Vector2.Dot(v, left), Vector2.Dot(v, forward));
            double scale = HandlingContact.Scale(speed, slip, gripAcceleration);
            for (int i = 0; i < 3; i++)
            {
                double utilization = Math.Clamp(target / (capacity * Math.Cos(steer)), -.98, .98);
                double side = scale * utilization / Math.Sqrt(1 - utilization * utilization);
                steer = Math.Clamp(angle + Math.Asin(Math.Clamp(side / speed, -.98, .98)), -bound, bound);
            }
        }
        double travel = steering.SteeringRateRadiansPerSecond * dt;
        return previous + Math.Clamp(Math.Clamp(steer, -bound, bound) - previous, -travel, travel);
    }

    private static Vector2 Axis(double angle) => new((float)Math.Cos(angle), (float)Math.Sin(angle));
    private static Vector2 Left(Vector2 v) => new(-v.Y, v.X);
}

internal sealed class PitchConvergence213(double residual)
    : InvalidOperationException($"H32-P coupled contact iteration did not converge (scaled residual {residual:G6}).");
