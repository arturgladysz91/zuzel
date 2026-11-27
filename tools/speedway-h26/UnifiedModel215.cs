
using CoreSim;
using SharedWorldSpeedway;
using System.Text.Json.Serialization;

namespace SpeedwayHandling;

internal readonly record struct Tyre215(Patch215 Patch, double Normal, double Longitudinal, double Lateral,
    double Capacity, double NormalLoss)
{
    internal Vec215 Force => Vec215.Up * Normal + Patch.Forward * Longitudinal + Patch.Side * Lateral;
    internal double Utilization => Capacity > 0 ? Math.Sqrt(Longitudinal * Longitudinal + Lateral * Lateral) / Capacity : 0;
    internal double EffectiveGap => Math.Max(0, Patch.Gap);
}

internal sealed record Forces215(Tyre215 Front, Tyre215 Rear, Vec215 Rider, Vec215 Drag, double EngineRpm,
    double NetEngineTorque, double CrankDerivative, double WheelDerivative, double RearSlip, double SteerRate,
    double EnginePower, double EngineLoss, double TransmissionLoss, double BearingLoss, double SlideLoss,
    double SteeringPower, double FrontYawBand, double FrontRollBand)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SupportLoads216? PassiveSupport { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LegLoads216? LeftFoot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public FrontResponse221 FrontResponse { get; init; }
    internal double Loss => EngineLoss + TransmissionLoss + BearingLoss + SlideLoss + Front.NormalLoss + Rear.NormalLoss +
        (PassiveSupport?.Loss ?? 0) + (LeftFoot is { } foot ? foot.StopLoss + foot.Contact.Dissipation : 0) +
        FrontResponse.RelaxationLossWatts;
    internal Vec215 GroundForce => Front.Force + Rear.Force + (LeftFoot?.Force ?? default);
}

internal sealed record GeometryReceipt215(Attitude215 Before, Attitude215 Midpoint, Attitude215 After,
    double FrontCurvatureBound, double RearCurvatureBound, double ProjectedBodyYaw, Vec215 FrontArm, Vec215 RearArm);
internal readonly record struct ContactEvent215(bool Front, bool Touchdown, double Seconds)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Foot { get; init; }
}
internal sealed record Step215(State215 Before, State215 Midpoint, State215 After, Forces215 Forces,
    HorizontalPlan214 Plan, HorizontalReceipt214 Horizontal, GeometryReceipt215 Geometry, ContactEvent215[] Contacts,
    double EnergyBefore, double EnergyAfter, double ActuatorPower, double EnergyResidual, Vec215 RollPitchYawMoment,
    Vec215 RiderAcceleration, Vec215 AngularAcceleration, int Subdivisions);

internal sealed class UnifiedModel215(Geometry215 geometry, RiderSpec rider, HandlingSettings settings,
    WorldParameters physics, TrackSurfaceState surface, Actuator215 actuator, double release,
    bool drivetrain, double gravity, bool inheritedLeanScale = false, double stiffness = UnifiedModel215.Stiffness,
    RiderSupport216? support = null)
{
    internal const double Stiffness = 150_000;
    internal const double Damping = 1500;
    private const double DamperRampMeters = .002;
    private const double EventTimeTolerance = 2e-7;
    private const double MinimumStep = 2e-8;
    private const double SupportWorkErrorWatts = .25;
    private readonly ModelHorizontal214 horizontal = new();
    private readonly RearWheelParameters wheel = physics.RearWheel ?? RearWheelParameters.Default;
    private readonly TwoContactBikeParameters steer = physics.TwoContactBike ?? TwoContactBikeParameters.Default;
    private readonly EngineLossH26 loss = settings.H26!.EngineLoss!;
    private readonly Vec215? seatedReference = support?.SeatedReference(geometry, LoadBasedTyreGrip.GravityMetersPerSecondSquared);
    private readonly double grip = LoadBasedTyreGrip.Budget((float)geometry.Base.Mass, surface,
        physics.TwoContactBike ?? TwoContactBikeParameters.Default, physics.LoadBasedTyres).EffectiveFrictionCoefficient *
        settings.GripScale;

    internal Step215 Step(State215 start, Activation215[] queue, double duration, double impairment = 0,
        SolveTrace46? audit = null)
    {
        if (!double.IsFinite(duration) || duration is <= 0 or > .005001)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (audit is not null) audit.Requested = duration;
        while (start.Next < queue.Length && queue[start.Next].Time <= start.Time + 5e-12)
        {
            var command = queue[start.Next];
            if (command.Requested > command.Time || command.Time < start.Time - 5e-10)
                throw new InvalidOperationException("rider command chronology");
            start = start with { Command = command.Command, Next = start.Next + 1 };
        }
        start.Command.Validate();

        if (start.Next < queue.Length) duration = Math.Min(duration, queue[start.Next].Time - start.Time);
        if (release > start.Time + 5e-12) duration = Math.Min(duration, release - start.Time);
        var parameters = geometry.Base;
        double target = Math.Clamp(start.Command.Bike.SteeringRadians, -steer.MaximumSteeringRadians, steer.MaximumSteeringRadians);
        double steeringTravel = target - start.Steer;
        double steeringRate = Math.Abs(steeringTravel) > 1e-10 ? Math.CopySign(steer.SteeringRateRadiansPerSecond, steeringTravel) : 0;
        if (steeringRate != 0) duration = Math.Min(duration, Math.Abs(steeringTravel / steeringRate));
        if (audit is not null)
        {
            audit.Active = start;
            audit.Capped = duration;
            audit.NextActivation = start.Next < queue.Length ? queue[start.Next] : null;
        }
        Validate(start);
        for (int subdivisions = 0; subdivisions <= 28; subdivisions++)
        {
            var mid = start;
            var motion = geometry.Motion(mid);
            var f = Evaluate(start, mid, motion, duration, steeringRate, impairment);
            bool converged = false;
            int iteration = 0;
            double error = 0;
            for (; iteration < 36; iteration++)
            {
                var next = Advance(start, mid, motion, f, duration / 2);
                error = (next.Momentum - mid.Momentum).Norm + (next.Angular - mid.Angular).Norm +
                    (next.RiderMomentum - mid.RiderMomentum).Norm + Math.Abs(next.Crank - mid.Crank) + Math.Abs(next.Wheel - mid.Wheel) +
                    (next.Center - mid.Center).Norm + (next.Rider - mid.Rider).Norm + (next.Pose.V - mid.Pose.V).Norm +
                    Math.Abs(next.Leg.Momentum - mid.Leg.Momentum) + Math.Abs(next.Leg.Extension - mid.Leg.Extension);
                if (geometry.FrontContact.Enabled) error += Math.Abs(next.FrontWheel - mid.FrontWheel) +
                    Math.Abs(next.FrontShearMeters - mid.FrontShearMeters);
                mid = next;
                motion = geometry.Motion(mid);
                f = Evaluate(start, mid, motion, duration, steeringRate, impairment);
                if (error < 1e-9) { converged = true; break; }
            }

            if (!converged && duration > MinimumStep)
            {
                audit?.Attempts.Add(new(duration, iteration, error, "midpoint bisection", [], null));
                duration /= 2;
                continue;
            }
            if (!converged)
            {
                audit?.Attempts.Add(new(duration, iteration, error, "midpoint rejected", [], null));
                throw new InvalidOperationException("coupled midpoint did not converge");
            }
            var drift = start with { Momentum = mid.Momentum };
            double fb = geometry.GapBound(drift, motion, true, steeringRate, duration);
            double rb = geometry.GapBound(drift, motion, false, steeringRate, duration);
            var events = new[] { Find(true, fb), Find(false, rb), FindFoot() }.OfType<ContactEvent215>()
                .OrderBy(e => e.Seconds).ThenByDescending(e => e.Front).ToArray();
            if (events.Length > 0 && events[0].Seconds > EventTimeTolerance &&
                events[0].Seconds < duration - EventTimeTolerance)
            {
                audit?.Attempts.Add(new(duration, iteration + 1, error, "contact split", events, null));
                // If several nearby contacts keep moving the root on re-solving,
                // advance a demonstrably earlier prefix and recheck the event there.
                duration = support != null && subdivisions >= 5 ? Math.Min(events[0].Seconds, duration / 2) : events[0].Seconds;
                continue;
            }

            var end = Advance(start, mid, motion, f, duration);
            if (audit is not null) audit.Candidate = end;
            Validate(end);
            double e0 = StoredEnergy(start), e1 = StoredEnergy(end);
            double work = f.LeftFoot?.MusclePower ?? f.Rider.Dot(motion.RiderRate);
            double energyResidual = e1 - e0 - (f.EnginePower - f.Loss + work + f.SteeringPower) * duration;
            // Fast engagement of a pad can otherwise fall between force evaluations.
            // Reject the interval before committing any impulses or advancing the clock.
            if (support != null && Math.Abs(energyResidual) > SupportWorkErrorWatts * duration + 1e-8)
            {
                audit?.Attempts.Add(new(duration, iteration + 1, error, "work bisection", events, energyResidual));
                if (duration <= MinimumStep) throw new InvalidOperationException("support work quadrature did not converge");
                duration /= 2;
                continue;
            }
            var front = new ForcePoint214(f.Front.Force.Plane, f.Front.Patch.Arm.Plane);
            var rear = new ForcePoint214(f.Rear.Force.Plane, f.Rear.Patch.Arm.Plane);
            var initialPlane = new Plane214(start.Center.Plane, (start.Momentum / parameters.Mass).Plane,
                start.Pose.Yaw, start.Angular.Z);
            var request = new HorizontalPlan214(start.Time, duration, initialPlane, parameters.Mass,
                parameters.YawCentral, front, rear, f.Drag.Plane)
            {
                Foot = f.LeftFoot is { } foot ? new(foot.Force.Plane, foot.Arm.Plane) : default
            };
            var receipt = horizontal.Preview(request);
            // Yaw angle is a projection of the single integrated orientation. Only L_z is delegated
            // to the shared planar impulse accounting; the scalar H33 yaw drift is superseded.
            receipt = receipt with { Plane = receipt.Plane with { Yaw = end.Pose.Yaw } };
            StepContract214.Validate(request, receipt);
            if ((end.Center.Plane - receipt.Plane.Position).Norm > 1e-9 ||
                ((end.Momentum / parameters.Mass).Plane - receipt.Plane.Velocity).Norm > 1e-9 ||
                Math.Abs(end.Angular.Z - receipt.Plane.YawMomentum) > 1e-9)
                throw new InvalidOperationException("H34-R shared projection drift");
            end = end with
            {
                Time = receipt.End,
                Center = new(receipt.Plane.Position.X, receipt.Plane.Position.Y, end.Center.Z),
                Momentum = new(receipt.Plane.Velocity.X * parameters.Mass,
                    receipt.Plane.Velocity.Y * parameters.Mass, end.Momentum.Z),
                Angular = end.Angular with { Z = receipt.Plane.YawMomentum }
            };
            horizontal.Commit(request, receipt);

            var beforeMotion = geometry.Motion(start);
            var afterMotion = geometry.Motion(end);
            e1 = StoredEnergy(end);
            energyResidual = e1 - e0 - (f.EnginePower - f.Loss + work + f.SteeringPower) * duration;
            var projected = new GeometryReceipt215(start.Pose, mid.Pose, end.Pose, fb, rb, end.Pose.Yaw,
                f.Front.Patch.Arm, f.Rear.Patch.Arm);
            audit?.Attempts.Add(new(duration, iteration + 1, error, "accepted", events, energyResidual));
            return new(start, mid, end, f, request, receipt, projected, events, e0, e1, work, energyResidual,
                Moment(f), (afterMotion.RiderRate - beforeMotion.RiderRate) / duration,
                (afterMotion.WorldRate - beforeMotion.WorldRate) / duration, subdivisions);

            ContactEvent215? Find(bool frontWheel, double bound)
            {
                var initial = geometry.Contact(drift, motion, frontWheel, steeringRate);
                double sign = initial.Gap >= 0 ? 1 : -1;
                var when = GapEvent214.First(t =>
                {
                    var pose = Drift(drift, motion, steeringRate, t);
                    var p = geometry.Contact(pose, motion, frontWheel, steeringRate);
                    return (sign * p.Gap, sign * p.NormalSpeed);
                }, duration, bound);
                return when.HasValue ? new(frontWheel, sign > 0, when.Value) : null;
            }

            ContactEvent215? FindFoot()
            {
                if (geometry.Leg is not { } leg) return null;
                var first = leg.Foot(geometry, drift, motion, leg.At(geometry, drift.Leg.Extension));
                double sign = first.Gap >= 0 ? 1 : -1;
                double bound = leg.GapBound(geometry, drift, motion, duration);
                var hit = GapEvent214.First(t =>
                {
                    var pose = Drift(drift, motion, steeringRate, t);
                    var p = leg.Foot(geometry, pose, motion, leg.At(geometry, pose.Leg.Extension));
                    return (sign * p.Gap, sign * p.ClosingVelocity);
                }, duration, bound);
                return hit.HasValue ? new ContactEvent215(false, sign > 0, hit.Value) { Foot = true } : null;
            }
        }
        throw new InvalidOperationException("H34-R contact event subdivision budget");
    }

    private State215 Drift(State215 s, Kinematics215 m, double steerRate, double dt) => s with
    {
        Time = s.Time + dt, Center = s.Center + s.Momentum * (dt / geometry.Base.Mass),
        Pose = s.Pose.Advance(m.WorldRate, dt), Rider = s.Rider + m.RiderRate * dt,
        Steer = s.Steer + steerRate * dt, Leg = s.Leg with { Extension = s.Leg.Extension + m.LegRate * dt }
    };

    private State215 Advance(State215 start, State215 mid, Kinematics215 motion, Forces215 f, double dt)
    {
        var wheels = f.Front.Force + f.Rear.Force;
        var force = f.GroundForce + f.Drag - Vec215.Up * (geometry.Base.Mass * gravity);
        var relativeForce = geometry.Free(f.Rider + (f.PassiveSupport?.Force ?? default) - motion.BodyRate.Cross(motion.RelativeMomentum) -
            mid.Pose.Body(wheels) * geometry.Base.RiderFraction + (f.LeftFoot?.RelativeForce ?? default));
        var position = Drift(start with { Momentum = mid.Momentum }, motion, f.SteerRate, dt);
        return position with
        {
            Momentum = start.Momentum + force * dt, Angular = start.Angular + Moment(f) * dt,
            RiderMomentum = start.RiderMomentum + relativeForce * dt, Crank = start.Crank + f.CrankDerivative * dt,
            Wheel = start.Wheel + f.WheelDerivative * dt,
            FrontWheel = start.FrontWheel + f.FrontResponse.RotorTorqueNm * dt,
            FrontShearMeters = start.FrontShearMeters + f.FrontResponse.StrainRateMetersPerSecond * dt,
            Leg = position.Leg with { Momentum = start.Leg.Momentum + (f.LeftFoot?.MomentumDerivative ?? 0) * dt }
        };
    }

    private static Vec215 Moment(Forces215 f) => f.Front.Patch.Arm.Cross(f.Front.Force) + f.Rear.Patch.Arm.Cross(f.Rear.Force) +
        (f.LeftFoot is { } foot ? foot.Arm.Cross(foot.Force) : default);

    private double StoredEnergy(State215 state) => geometry.Energy(state, gravity, stiffness) +
        (Pads(state, default, 0)?.Energy ?? 0);

    private SupportLoads216? Pads(State215 state, Kinematics215 motion, double steerRate) =>
        support == null ? null :
        geometry.Leg is { } leg ? leg.Pads(support, geometry, state, motion, steerRate) :
        support.Evaluate(state.Rider, motion.RiderRate, state.Steer, steerRate);

    private void Validate(State215 s)
    {
        var m = geometry.Motion(s);
        var offset = s.Rider - geometry.Neutral;
        if (!double.IsFinite(s.Center.Norm + s.Angular.Norm + m.BodyRate.Norm + m.RiderRate.Norm) ||
            Math.Abs(s.Pose.Roll) > Geometry215.MaxRoll || Math.Abs(s.Pose.Pitch) > Geometry215.MaxPitch)
            throw new InvalidOperationException("orientation envelope");
        if (!geometry.ChassisClear(s)) throw new ChassisEnvelope36(s);
        if (Math.Abs(offset.X) > .36 || Math.Abs(offset.Y) > .34 || Math.Abs(offset.Z) > .30)
            throw new InvalidOperationException("rider travel envelope");
        if (geometry.Contact(s, m, true, 0).Gap < -Geometry215.MaxCompression ||
            geometry.Contact(s, m, false, 0).Gap < -Geometry215.MaxCompression)
            throw new InvalidOperationException("tyre compression envelope");
        geometry.Leg?.Validate(geometry, s, m);
    }

    private Forces215 Evaluate(State215 initial, State215 s, Kinematics215 m, double dt, double steeringRate, double impairment)
    {
        var front = geometry.Contact(s, m, true, steeringRate);
        var rear = geometry.Contact(s, m, false, steeringRate);
        var (nf, df) = Support(front);
        var (nr, dr) = Support(rear);
        double cf = grip * nf, cr = grip * nr, gripAcceleration = grip * gravity;
        double lean = inheritedLeanScale ? Math.Min(1, Math.Abs(s.Pose.Roll) / settings.MaximumLeanRadians) : 0;
        double sf = .115 * (1 - .13 * lean), sr = .104 * (1 - .25 * lean);
        double u = rear.Velocity.Dot(rear.Forward), v = rear.Velocity.Dot(rear.Side);
        double side = v / HandlingContact.Scale(u, sr, gripAcceleration);
        var engine = Engine500Parameters.Default;
        double ratio = engine.ShortOverallRatio + (engine.LongOverallRatio - engine.ShortOverallRatio) * rider.Setup.Gearing;
        double throttle = s.Command.Bike.Drive * (1 - impairment);
        double crank = s.Crank / geometry.Base.CrankInertia - m.BodyRate.Y;
        double rim = s.Wheel / geometry.Base.WheelInertia - m.BodyRate.Y;
        double shiftedCrank = initial.Crank / geometry.Base.CrankInertia - m.BodyRate.Y;
        double shiftedWheel = initial.Wheel / geometry.Base.WheelInertia - m.BodyRate.Y;
        ClutchTrial64 trial = default;
        if (drivetrain)
        {
            var shaft = new CrankClutch144(LaunchParameters144.Default, shiftedCrank, loss);
            trial = shaft.Evaluate64(throttle, ratio, engine.Efficiency, dt, initial.Time >= release - 5e-12,
                torque => RearWheelDynamics.EvaluateCombined64(u, side, shiftedWheel, geometry.Base.Mass, cr, torque, 0, dt, wheel),
                shaft.EngineMidpointEvaluator64(throttle, dt));
        }
        double fx = drivetrain ? trial.Wheel.Force : 0;
        double fy = drivetrain ? RearWheelDynamics.CoupledLateralForce(cr, side, trial.Wheel) : -cr * side / Math.Sqrt(1 + side * side);
        var ff = FrontTyre(front, out var frontResponse);
        var rr = new Tyre215(rear, nr, fx, fy, cr, dr);
        double rpm = crank * 30 / Math.PI;
        double viscous = drivetrain ? loss.Mechanical(crank) + loss.Pumping(crank, throttle) : 0;
        double gross = drivetrain ? throttle * (Engine500.Torque(rpm) + loss.Mechanical(crank)) : 0;
        double drive = trial.Torque * ratio * (trial.Torque >= 0 ? engine.Efficiency : 1 / engine.Efficiency);
        double bearing = drivetrain ? wheel.BearingResistanceNewtonMeterSeconds * rim : 0;
        double crankDerivative = gross - viscous - trial.Torque;
        double wheelDerivative = drive - bearing + rear.Axle.Dot(rear.Radial.Cross(rr.Force));
        if (!drivetrain) wheelDerivative = 0;
        double slip = geometry.Base.Radius * rim - u;
        double slideLoss = geometry.FrontContact.Enabled ? frontResponse.ContactLossWatts + fx * slip - fy * v :
            -ff.Force.Plane.Dot(front.Velocity.Plane) + fx * slip - fy * v;
        var p = s.Momentum / geometry.Base.Mass;
        double speed = p.Plane.Norm;
        var drag = drivetrain ? new Vec215(p.X, p.Y, 0) * (-(LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons +
            LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient * speed * speed) / Math.Sqrt(speed * speed + .25)) : default;
        // Tangential forces over the available half-step steering range, at the same kinematics.
        var left = SteeringMoment(-1);
        var right = SteeringMoment(1);
        var center = SteeringMoment(0);
        var passive = Pads(s, m, steeringRate);
        var (muscle, legLoads) = geometry.Leg is { } leg
            ? leg.Evaluate(geometry, s, m, seatedReference ?? geometry.Neutral, actuator, passive)
            : (actuator.Force(geometry, s, m.RiderRate, seatedReference), (LegLoads216?)null);
        double steeringPower = -ff.Force.Dot(front.SteerJacobian) * steeringRate;
        if (passive is { } pads) steeringPower += pads.SteerPower;
        if (geometry.FrontContact.PolarInertiaKgM2 > 0) steeringPower += frontResponse.SteeringReactionWatts;
        return new(ff, rr, muscle, drag, rpm, gross - viscous,
            crankDerivative, wheelDerivative, slip / Math.Sqrt(u * u + wheel.LowSpeedMetersPerSecond * wheel.LowSpeedMetersPerSecond),
            steeringRate, gross * crank, viscous * crank,
            trial.Torque * (crank - ratio * rim) + (trial.Torque * ratio - drive) * rim,
            bearing * rim, slideLoss - drag.Dot(p), steeringPower,
            Span(left.Z, center.Z, right.Z), Span(left.X, center.X, right.X)) { PassiveSupport = passive, LeftFoot = legLoads, FrontResponse = frontResponse };

        Vec215 SteeringMoment(int sign)
        {
            double angle = Math.Clamp(initial.Steer + sign * steer.SteeringRateRadiansPerSecond * dt / 2,
                -steer.MaximumSteeringRadians, steer.MaximumSteeringRadians);
            var patch = geometry.Contact(s with { Steer = angle }, m, true, steeringRate);
            var tyre = FrontTyre(patch, out _);
            return s.Pose.Body(patch.Arm.Cross(tyre.Force));
        }

        Tyre215 FrontTyre(Patch215 patch, out FrontResponse221 response)
        {
            double sideSpeed = patch.Velocity.Dot(patch.Side);
            double scale = HandlingContact.Scale(patch.Velocity.Dot(patch.Forward), sf, gripAcceleration);
            if (geometry.FrontContact.Enabled)
                return geometry.FrontContact.Evaluate(s, m, patch, nf, df, cf, scale, steeringRate, out response);
            response = default;
            return new(patch, nf, 0, -cf * sideSpeed / Math.Sqrt(sideSpeed * sideSpeed + scale * scale), cf, df);
        }
    }

    private (double Normal, double DampingLoss) Support(Patch215 p)
    {
        if (p.Gap >= 0) return default;
        double compression = -p.Gap, closing = Math.Max(0, -p.NormalSpeed);
        double damper = Damping * Math.Min(1, compression / DamperRampMeters) * closing;
        return (stiffness * compression + damper, damper * closing);
    }

    private static double Span(double a, double b, double c) => Math.Max(a, Math.Max(b, c)) - Math.Min(a, Math.Min(b, c));
}
