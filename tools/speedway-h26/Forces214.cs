using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record Forces214(Ground214 Normal, ForcePoint214 Front, ForcePoint214 Rear, Vec214 Drag,
    double PitchTorque, double CrankTorque, double WheelTorque, double Steer, double Lean,
    double EngineRpm, double NetCrankTorque, double RearSlip, double FrontYawMinimum, double FrontYawMaximum,
    double EnginePower, double EngineLoss, double ClutchLoss, double TransmissionLoss, double BearingLoss,
    double FrontSlipLoss, double RearSlipLoss, double DragLoss, double Residual, int Evaluations)
{
    internal double Dissipation => EngineLoss + ClutchLoss + TransmissionLoss + BearingLoss +
        FrontSlipLoss + RearSlipLoss + DragLoss;
}

internal sealed class ForcesModel214(Geometry214 geometry, RiderSpec rider, HandlingSettings settings,
    WorldParameters physics, TrackSurfaceState surface, double release, bool isolated, double? gravity = null)
{
    private readonly RearWheelParameters wheel = physics.RearWheel ?? RearWheelParameters.Default;
    private readonly TwoContactBikeParameters steer = physics.TwoContactBike ?? TwoContactBikeParameters.Default;
    private readonly Engine500Parameters engine = Engine500Parameters.Default;
    private readonly LaunchParameters144 launch = LaunchParameters144.Default;
    private readonly double mu = LoadBasedTyreGrip.Budget((float)geometry.Mass, surface,
        physics.TwoContactBike ?? TwoContactBikeParameters.Default, physics.LoadBasedTyres).EffectiveFrictionCoefficient *
        settings.GripScale;
    private readonly EngineLossH26 loss = settings.H26!.EngineLoss!;

    internal double Gravity => gravity ?? (isolated ? 0 : LoadBasedTyreGrip.GravityMetersPerSecondSquared);

    internal double FreeMoment(State214 s, double pitchTorque, double crankTorque, double wheelTorque)
    {
        var r = s.Rider;
        double change = 2 * geometry.ReducedMass * r.Position.Dot(r.Velocity);
        double cross = geometry.ReducedMass * r.Position.Left.Dot(r.Acceleration);
        return pitchTorque + geometry.YawPitchDerivative(s) + crankTorque + wheelTorque -
            change * geometry.PitchRate(s) - cross;
    }

    internal Ground214 NormalAt(State214 s, Forces214 f, WheelContact213? fixedMode = null) =>
        Contact214.Loads(geometry, s, FreeMoment(s, f.PitchTorque, f.CrankTorque, f.WheelTorque), -Gravity, fixedMode);

    internal Forces214 Solve(State214 state, State214 sample, double dt, WheelContact213? fixedMode, double impairment)
    {
        double rate = geometry.PitchRate(sample), yaw = geometry.YawRate(sample);
        var forward = Vec214.Axis(sample.Plane.Yaw);
        var left = forward.Left;
        var bike = state.Active.Bike;
        var frontGeometry = geometry.Contact(sample, WheelContact213.Front);
        var rearGeometry = geometry.Contact(sample, WheelContact213.Rear);
        double ratio = engine.ShortOverallRatio + (engine.LongOverallRatio - engine.ShortOverallRatio) * rider.Setup.Gearing;
        double throttle = bike.Drive * (1 - impairment);
        double speed = sample.Plane.Velocity.Norm;
        double gripAcceleration = mu * Gravity;
        double maxYaw = 1.27 * gripAcceleration / Math.Max(4, speed);
        double beta = speed > .01 ? CoreSim.PhysicalSpace.BikeAngles.Wrap(
            sample.Plane.Yaw - Math.Atan2(sample.Plane.Velocity.Y, sample.Plane.Velocity.X)) : 0;
        double yawTarget = Math.Clamp(speed * Math.Tan(bike.SteeringRadians) / (geometry.FrameFront.X - geometry.FrameRear.X),
            -maxYaw, maxYaw) - 2.8 * Math.CopySign(Math.Max(0, Math.Abs(beta) - .24), beta);
        double momentTarget = geometry.YawInertia(sample.Rider.Position, sample.Pitch) *
            (yawTarget - yaw) / settings.YawResponseSeconds;
        double leanTarget = Gravity > 0 ? Math.Clamp(Math.Atan2(speed * yawTarget, Gravity),
            -settings.MaximumLeanRadians, settings.MaximumLeanRadians) : 0;
        double lean = state.Lean + Math.Clamp((leanTarget - state.Lean) * dt / settings.LeanResponseSeconds,
            -settings.LeanRateRadiansPerSecond * dt, settings.LeanRateRadiansPerSecond * dt);
        double leanFraction = Math.Abs((state.Lean + lean) / 2) / settings.MaximumLeanRadians;
        double slipFront = .115 * (1 - .13 * leanFraction), slipRear = .104 * (1 - .25 * leanFraction);
        double riderRecoil = -geometry.RiderFraction * sample.Rider.Velocity.Rotate(sample.Pitch).X;
        Vec214 ContactSpeed(WheelGeometry214 contact) => sample.Plane.Velocity +
            left * (yaw * contact.Axle.X) + forward * (contact.PitchLever * rate + riderRecoil);
        var vf = ContactSpeed(frontGeometry);
        var vr = ContactSpeed(rearGeometry);
        double rearU = vr.Dot(forward), side = vr.Dot(left);
        double rearSide = side / HandlingContact.Scale(rearU, slipRear, gripAcceleration);
        var drag = isolated ? default : sample.Plane.Velocity * (-(LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons +
            LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient * speed * speed) / Math.Sqrt(speed * speed + .25));
        var shaft = new CrankClutch144(launch, state.CrankMomentum / geometry.CrankInertia + rate, loss);
        var crankMid = isolated ? null : shaft.EngineMidpointEvaluator64(throttle, dt);
        bool released = state.Time >= release - 1e-10;
        double guess = FreeMoment(sample, 0, 0, 0);
        for (int i = 1; i <= 22; i++)
        {
            var trial = Calculate(guess, i);
            double actual = FreeMoment(sample, trial.PitchTorque, trial.CrankTorque, trial.WheelTorque);
            if (Math.Abs(actual - guess) <= 2e-5) return trial with { Residual = actual - guess };
            guess += .66 * (actual - guess);
        }

        // Local continuation can cross a support/traction kink. Use a bounded sign bracket as a fallback.
        // A sign bracket witnesses the selected root and makes no global uniqueness assertion.
        double radius = 20, low = 0, high = 0, pl = 0, ph = 0;
        for (int i = 0; i < 12; i++, radius *= 2)
        {
            low = guess - radius;
            high = guess + radius;
            pl = Defect(low);
            ph = Defect(high);
            if (pl <= 0 && ph >= 0) break;
        }
        if (pl > 0 || ph < 0) throw new Subdivide214("normal/tyre root could not be bracketed");
        for (int i = 0; i < 48; i++)
        {
            double mid = (low + high) / 2;
            var trial = Calculate(mid, 23 + i);
            double defect = mid - FreeMoment(sample, trial.PitchTorque, trial.CrankTorque, trial.WheelTorque);
            if (Math.Abs(defect) <= 2e-5) return trial with { Residual = -defect };
            if (defect < 0) low = mid; else high = mid;
        }
        throw new Subdivide214("normal/tyre root did not reach the residual tolerance");

        double Defect(double moment)
        {
            var trial = Calculate(moment, 0);
            return moment - FreeMoment(sample, trial.PitchTorque, trial.CrankTorque, trial.WheelTorque);
        }

        Forces214 Calculate(double moment, int evaluations)
        {
            var support = Contact214.Loads(geometry, sample, moment, -Gravity, fixedMode);
            double frontCapacity = isolated ? 0 : mu * Math.Max(0, support.Front);
            double rearCapacity = isolated ? 0 : mu * Math.Max(0, support.Rear);
            ClutchTrial64 power = isolated ? default : shaft.Evaluate64(throttle, ratio, engine.Efficiency, dt, released,
                torque => RearWheelDynamics.EvaluateCombined64(rearU, rearSide,
                    state.WheelMomentum / geometry.WheelInertia + rate, geometry.Mass, rearCapacity, torque, 0, dt, wheel),
                crankMid);
            double fxR = isolated ? 0 : power.Wheel.Force;
            double fyR = isolated ? 0 : RearWheelDynamics.CoupledLateralForce(rearCapacity, rearSide, power.Wheel);
            var rear = new ForcePoint214(forward * fxR + left * fyR, forward * rearGeometry.Axle.X);
            double angle = SteerTarget(state.Steer, bike.SteeringRadians, vf, forward, left, frontCapacity,
                (momentTarget - rear.YawMoment) / Math.Max(.1, frontGeometry.Axle.X),
                slipFront, gripAcceleration, dt);
            Vec214 FrontAt(double turn)
            {
                var axis = Vec214.Axis(sample.Plane.Yaw + turn);
                var normal = axis.Left;
                double lateral = vf.Dot(normal);
                double scale = HandlingContact.Scale(vf.Dot(axis), slipFront, gripAcceleration);
                return normal * (-frontCapacity * lateral / Math.Sqrt(lateral * lateral + scale * scale));
            }
            var front = new ForcePoint214(FrontAt((state.Steer + angle) / 2), forward * frontGeometry.Axle.X);
            double rim = isolated ? state.WheelMomentum / geometry.WheelInertia + rate : power.Wheel.MidOmega;
            double crank = isolated ? state.CrankMomentum / geometry.CrankInertia + rate : power.EngineMid;
            double rpm = crank * 30 / Math.PI;
            double viscous = isolated ? 0 : loss.Mechanical(crank) + loss.Pumping(crank, throttle);
            double gross = isolated ? 0 : throttle * (Engine500.Torque(rpm) + loss.Mechanical(crank));
            double drive = power.Torque * ratio * (power.Torque >= 0 ? engine.Efficiency : 1 / engine.Efficiency);
            double bearing = isolated ? 0 : wheel.BearingResistanceNewtonMeterSeconds * rim;
            double cTorque = gross - viscous - power.Torque;
            double wTorque = drive - bearing - geometry.Radius * fxR;
            double pitch = frontGeometry.PitchLever * front.Force.Dot(forward) + rearGeometry.PitchLever * fxR;
            double s = geometry.Radius * rim - rearU;
            double low = 0, high = 0;
            if (frontCapacity > 0 && evaluations > 0)
            {
                low = double.PositiveInfinity;
                high = double.NegativeInfinity;
                for (int j = -4; j <= 4; j++)
                {
                    double trial = Math.Clamp(state.Steer + j * dt * steer.SteeringRateRadiansPerSecond / 8,
                        -steer.MaximumSteeringRadians, steer.MaximumSteeringRadians);
                    double q = front.Arm.Cross(FrontAt(trial));
                    low = Math.Min(low, q);
                    high = Math.Max(high, q);
                }
            }
            return new(support, front, rear, drag, pitch, cTorque, wTorque, angle, lean, rpm, gross - viscous,
                s / Math.Sqrt(rearU * rearU + wheel.LowSpeedMetersPerSecond * wheel.LowSpeedMetersPerSecond),
                low, high, gross * crank, viscous * crank, power.Torque * (crank - ratio * rim),
                (power.Torque * ratio - drive) * rim, bearing * rim, -front.Force.Dot(vf), fxR * s - fyR * side,
                -drag.Dot(sample.Plane.Velocity), 0, evaluations);
        }
    }

    private double SteerTarget(double previous, double request, Vec214 v, Vec214 forward, Vec214 left,
        double capacity, double target, double slip, double gripAcceleration, double dt)
    {
        double angle = request, bound = steer.MaximumSteeringRadians;
        if (capacity > 0 && v.Norm >= .5)
        {
            double velocityAngle = Math.Atan2(v.Dot(left), v.Dot(forward));
            double scale = HandlingContact.Scale(v.Norm, slip, gripAcceleration);
            for (int i = 0; i < 3; i++)
            {
                double utilization = Math.Clamp(target / (capacity * Math.Cos(angle)), -.98, .98);
                double side = scale * utilization / Math.Sqrt(1 - utilization * utilization);
                angle = Math.Clamp(velocityAngle + Math.Asin(Math.Clamp(side / v.Norm, -.98, .98)), -bound, bound);
            }
        }
        double travel = steer.SteeringRateRadiansPerSecond * dt;
        return previous + Math.Clamp(Math.Clamp(angle, -bound, bound) - previous, -travel, travel);
    }
}

internal sealed class Subdivide214(string message) : InvalidOperationException(message);
