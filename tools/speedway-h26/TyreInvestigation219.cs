
using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record ForceCell38(int SpeedKph, double RequestedSlip, double RearSlipAngleDegrees,
        double RearNormal, double RollDegrees, double SteeringDegrees, string LateralRepresentation,
        string Input, double RequestedInput, double ActualAxleTorque, double EngineRpm, double ClutchTorque,
        double FrontFx, double FrontFy, double RearFx, double RearFy, double RearForceHeadingDegrees,
        double RearUtilization, double DissipatedWatts, double FrontYawMoment, double RearYawMoment,
        double FrontRollMoment, double RearRollMoment, double FrontYawBand, double FrontRollBand);
    private sealed record RigResult38(string Mode, int SpeedKph, double Radius, State215 Seed,
        Command215 Request, double InitialSlip, double InitialSideslip, string Stop, int Steps, double Seconds,
        double ActualCurvature, double ActualLateralAcceleration, double RequiredLateralAcceleration,
        double MeanFrontLoad, double RearInwardFraction, double MeanYawBand, double MeanRollBand,
        double MinimumChassis, double PeakActiveForce, double PeakActivePower, double MaximumEnergyDefect,
        double SlideSeconds, double PoweredSeconds);

    private static void ForceInvestigation38(Lab38 lab, string output)
    {
        var clock = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var g = lab.Geometry(false);
        var ground = lab.Fixture.Rest(g, NominalSupport216, 70 / 3.6);
        var calibrationSeed = ground with
        {
            Time = lab.Fixture.Release + 1,
            Wheel = g.Base.WheelInertia * (70 / 3.6) / g.Base.Radius,
            Crank = g.Base.CrankInertia * (70 / 3.6) / g.Base.Radius * lab.Fixture.Ratio
        };
        var warm = lab.Engine(g).Step(g.WithRates(calibrationSeed, default, default), [], .001);
        double grip = warm.Forces.Rear.Capacity / warm.Forces.Rear.Normal;
        var p = Physics.RearWheel ?? RearWheelParameters.Default;
        var steering = Physics.TwoContactBike ?? TwoContactBikeParameters.Default;
        var engine = Engine500Parameters.Default;
        var loss = Settings202("H28-F").H26!.EngineLoss!;
        var cells = new List<ForceCell38>();
        foreach (int kph in new[] { 60, 70, 80, 90, 100, 110, 120 })
        foreach (double slip in new[] { -.1, 0, .04, .08, .12, .20, .35, .55 })
        foreach (double angle in new[] { 0d, 4, 10, 16, 24 })
        foreach (double normal in new[] { 650d, 900, 1150 })
        foreach (double roll in new[] { 0d, -28, -47 })
        foreach (double steer in new[] { -6d, 0, 6 })
        {
            double speed = kph / 3.6;
            double u = speed * Math.Cos(Radians38(angle));
            double v = -speed * Math.Sin(Radians38(angle));
            double omega = (u + slip * Math.Sqrt(u * u + p.LowSpeedMetersPerSecond * p.LowSpeedMetersPerSecond)) / p.WheelRadiusMeters;
            var state = ground with { Pose = Attitude215.Pose(0, 0, Radians38(roll)), Steer = Radians38(steer) };
            var rates = g.Motion(state);
            var rear = g.Contact(state, rates, false, 0);
            double nf = g.Base.Mass * Gravity216 - normal;
            double capacity = grip * normal, frontCapacity = grip * nf;
            const double dt = StepSeconds216;
            double accelGrip = grip * Gravity216;
            double actualSide = v / HandlingContact.Scale(u, .104, accelGrip);
            Emit("active H38", "imposed axle torque", 0, 0, 0, 0, actualSide);
            if (normal != 900 || steer != 0) continue;
            double leanFactor = Math.Clamp(Math.Abs(roll) / 60, 0, 1);
            double sensitivitySide = v / HandlingContact.Scale(u, .104 * (1 - .25 * leanFactor), accelGrip);
            Emit("inactive lean scaling sensitivity", "imposed axle torque", 0, 0, 0, 0, sensitivitySide);
            foreach (double torque in new[] { 50d, 110, 170 })
                Emit("active H38", "imposed axle torque", torque, torque, 0, 0, actualSide);
            foreach (float drive in Drives38)
            {
                double crank = Math.Max(0, omega * lab.Fixture.Ratio);
                var shaft = new CrankClutch144(LaunchParameters144.Default, crank, loss);
                var trial = shaft.Evaluate64(drive, lab.Fixture.Ratio, engine.Efficiency, dt, true,
                    torque => RearWheelDynamics.EvaluateCombined64(u, actualSide, omega, g.Base.Mass,
                        capacity, torque, 0, dt, p), shaft.EngineMidpointEvaluator64(drive, dt));
                double axle = trial.Torque * lab.Fixture.Ratio *
                    (trial.Torque >= 0 ? engine.Efficiency : 1 / engine.Efficiency);
                Record("active H38", "coupled Drive", drive, axle, trial.EngineMid * 30 / Math.PI,
                    trial.Torque, actualSide, trial.Wheel);
            }

            void Emit(string lateralRepresentation, string input, double request, double axle, double rpm, double clutch, double side)
            {
                var trial = RearWheelDynamics.EvaluateCombined64(u, side, omega, g.Base.Mass, capacity, axle, 0, dt, p);
                Record(lateralRepresentation, input, request, axle, rpm, clutch, side, trial);
            }

            void Record(string representation, string input, double request, double axle, double rpm, double clutch,
                double side, RearWheelTrial64 trial)
            {
                var rearFy = RearWheelDynamics.CoupledLateralForce(capacity, side, trial);
                var rearForce = rear.Forward * trial.Force + rear.Side * rearFy + Vec215.Up * normal;
                var rearMoment = state.Pose.Body(rear.Arm.Cross(rearForce));
                var (frontMoment, fy) = Front(state.Steer);
                var (left, _) = Front(Math.Max(-steering.MaximumSteeringRadians, state.Steer - dt * steering.SteeringRateRadiansPerSecond / 2));
                var (right, _) = Front(Math.Min(steering.MaximumSteeringRadians, state.Steer + dt * steering.SteeringRateRadiansPerSecond / 2));
                cells.Add(new(kph, slip, angle, normal, roll, steer, representation, input, request, axle, rpm, clutch,
                    0, fy, trial.Force, rearFy, Math.Atan2(rearFy, trial.Force) * 180 / Math.PI,
                    Math.Sqrt(trial.Force * trial.Force + rearFy * rearFy) / capacity,
                    trial.Force * trial.ConstitutiveSlip - rearFy * v - FyLoss(fy, state.Steer),
                    frontMoment.Z, rearMoment.Z, frontMoment.X, rearMoment.X,
                    Span38(left.Z, frontMoment.Z, right.Z), Span38(left.X, frontMoment.X, right.X)));
            }

            (Vec215 Moment, double Lateral) Front(double steerRad)
            {
                var patch = g.Contact(state with { Steer = steerRad }, rates, true, 0);
                double sideVel = -u * Math.Sin(steerRad) + v * Math.Cos(steerRad);
                double forwardVel = u * Math.Cos(steerRad) + v * Math.Sin(steerRad);
                double delta = HandlingContact.Scale(forwardVel, .115, accelGrip);
                double fy = -frontCapacity * sideVel / Math.Sqrt(sideVel * sideVel + delta * delta);
                return (state.Pose.Body(patch.Arm.Cross(Vec215.Up * nf + patch.Side * fy)), fy);
            }

            double FyLoss(double fy, double steerRad) => fy * (-u * Math.Sin(steerRad) + v * Math.Cos(steerRad));
        }
        Save(Path.Combine(output, "force-maps.json.gz"), new
        {
            Model = "H38 stock coupled tyre and clutch library",
            grip, Wheel = p, Steering = steering,
            ActiveCamber = "Ground geometry rotates the wheel and contact arm; contact force has no independent camber thrust.",
            Sensitivity = "Inactive historical rear lean scaling, displayed separately. No qualifying rollout uses it.",
            Cells = cells
        }, true);
        var rigs = CircleRigs38(lab, output);
        Save(Path.Combine(output, "force-audit.json"), new
        {
            Grip = grip, CellCount = cells.Count, DynamicRigCount = rigs.Length, RigSteps = rigs.Sum(x => x.Steps),
            CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds, WallSeconds = clock.Elapsed.TotalSeconds
        });
        Console.WriteLine($"H38 mapped {cells.Count} tyre states and {rigs.Length} synthetic circle starts.");
    }

    private static RigResult38[] CircleRigs38(Lab38 lab, string output)
    {
        string[] names = ["balanced", "moderate spin", "large spin", "active rider", "active boot", "coast then drive"];
        var jobs = (from lane in new[] { 1.5, 5.2, 8.7 }
                    from speed in new[] { 60, 70, 80 }
                    from kind in Enumerable.Range(0, names.Length) select (lane, speed, kind)).ToArray();
        var results = new RigResult38[jobs.Length];
        Parallel.For(0, jobs.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) }, i =>
        {
            var (lane, speed, kind) = jobs[i];
            var g = lab.Geometry(kind == 4);
            double reference = (lab.Bend.Entry + lab.Bend.Exit) / 2;
            double radius = Frame.RoadAt(reference, lane).RadiusMeters;
            double v = speed / 3.6, yawRate = v / radius;
            double slip = kind switch { 0 => 0, 2 => .34, _ => .12 };
            double beta = kind == 0 ? 0 : Radians38(9);
            var rider = kind >= 3 ? new Vec215(.085, .055, -.055) : default;
            float drive = kind switch { 0 => .18f, 2 => .85f, 5 => .13f, _ => .45f };
            var rest = lab.Fixture.Rest(g, NominalSupport216);
            double bank = -Math.Min(.84, Math.Atan(v * yawRate / Gravity216));
            var pose = Attitude215.Pose(beta, 0, bank);
            double omega = v * (1 + slip) / g.Base.Radius;
            var command = new Command215(new((float)Math.Atan(1.3 / radius) - (float)(beta * .34), drive), rider)
                { LeftFoot = kind == 4 ? .75 : 0 };
            var local = g.Supported(rest with { Time = lab.Fixture.Release + 1, Pose = pose,
                Momentum = new(v * g.Base.Mass, 0, 0), Wheel = omega * g.Base.WheelInertia,
                Crank = omega * lab.Fixture.Ratio * g.Base.CrankInertia, Rider = rest.Rider + rider,
                Steer = command.Bike.SteeringRadians, Command = command }, Gravity216, UnifiedModel215.Stiffness);
            if (kind == 0)
            {
                var steady = SteadyCircle215(lab.Engine(g), g, rest, v, yawRate, local.Time, lab.Fixture.Ratio, balanceRider: true);
                local = steady.Residual <= .005 ? steady.State :
                    g.WithRates(local, pose.Body(Vec215.Up * yawRate), default);
            }
            else
                local = g.WithRates(local, pose.Body(Vec215.Up * yawRate), default);
            var seed = Place36(local, reference, lane);
            var q = kind == 5 ? new[] { new Activation215(seed.Time + .19, seed.Time + .19 + ReactionSeconds216,
                command with { Bike = command.Bike with { Drive = .60f } }) } : [];
            var result = Window38(lab, g, lab.Engine(g), new(seed, reference - lab.Bend.Entry), q, .62);
            var receipt = Measure38(result.Steps, g, result.Stop);
            double duration = result.Final.State.Time - seed.Time;
            double required = v * v / radius;
            results[i] = new(names[kind], speed, radius, seed, command, slip, beta,
                result.Stop, result.Steps.Length, duration, receipt.VelocityCurvature, receipt.InwardAcceleration, required,
                receipt.FrontLoad, receipt.RearInwardFraction, receipt.YawBand, receipt.RollBand, result.Ground,
                receipt.PeakForce, receipt.PeakPower, receipt.MaximumEnergyDefect, receipt.Slide, receipt.Powered);
        });
        Save(Path.Combine(output, "synthetic-circles.json.gz"), results, true);
        return results;
    }

    private static double Radians38(double degrees) => degrees * Math.PI / 180;
    private static double Span38(double a, double b, double c) => Math.Max(a, Math.Max(b, c)) - Math.Min(a, Math.Min(b, c));
}
