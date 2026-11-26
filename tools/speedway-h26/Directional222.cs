using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private enum Limit42 { None, AccelerationCap, ScalarReserve, CurvatureFloor, Deceleration, CornerCeiling,
        BendDrop, ExitReference, Coast, RearSlip, GroundReserve }
    private const double Grace42 = .10;
    private const double Grip42 = 1.605625;
    private sealed record Authority42(double MaximumInward, double CurrentInward, double OutwardMaximum,
        double YawMinimum, double YawMaximum, double RollMinimum, double RollMaximum,
        double ForwardAcceleration, double MinimumFrontLongitudinal, Vec215 Front, Vec215 Rear, Vec215 Foot);

    private static Authority42 ProjectForce42(Geometry215 g, State215 s, Forces215 f)
    {
        var m = g.Motion(s);
        var v = s.Momentum.Plane;
        var forward = v / Math.Max(.001, v.Norm);
        var tangent = new Vec215(forward.X, forward.Y, 0);
        var inward = Vec215.Up.Cross(tangent);
        var boot = f.LeftFoot?.Force ?? default;
        double sideFriction = f.Front.Capacity;
        var unchanged = f.Rear.Force + boot + f.Drag;
        var fixedMoment = f.Rear.Patch.Arm.Cross(f.Rear.Force) + (f.LeftFoot is { } leg ? leg.Arm.Cross(boot) : default);
        double acceleration = (f.Front.Force + unchanged).Dot(inward) / g.Base.Mass;
        double tangential = (f.Front.Force + unchanged).Dot(tangent) / g.Base.Mass;
        double steeringTravel = Steering217.SteeringRateRadiansPerSecond * Grace42;
        double minYaw = double.PositiveInfinity, maxYaw = double.NegativeInfinity, minRoll = double.PositiveInfinity;
        double maxRoll = double.NegativeInfinity, maxIn = acceleration, minIn = acceleration;
        for (int n = -2; n <= 2; n++)
        {
            double angle = Steering39(s.Steer + steeringTravel * n / 2);
            var patch = g.Contact(s with { Steer = angle }, m, true, 0);
            double side = patch.Velocity.Dot(patch.Side);
            double scale = HandlingContact.Scale(patch.Velocity.Dot(patch.Forward), .115, Grip42 * Gravity216);
            var ff = patch.Side * (-side * sideFriction / Math.Sqrt(side * side + scale * scale)) + Vec215.Up * f.Front.Normal;
            double normal = (ff + unchanged).Dot(inward) / g.Base.Mass;
            var moment = fixedMoment + patch.Arm.Cross(ff);
            double roll = s.Pose.Body(moment).X;
            maxIn = Math.Max(maxIn, normal);
            minIn = Math.Min(minIn, normal);
            minYaw = Math.Min(minYaw, moment.Z);
            maxYaw = Math.Max(maxYaw, moment.Z);
            minRoll = Math.Min(minRoll, roll);
            maxRoll = Math.Max(maxRoll, roll);
        }
        return new(maxIn, acceleration, -minIn, minYaw, maxYaw, minRoll, maxRoll, tangential, f.Front.Longitudinal,
            f.Front.Force, f.Rear.Force, boot);
    }

    private static double LineCurvature42(double reference, double lane)
    {
        var a = Frame.At(reference - 2, lane).Position;
        var b = Frame.At(reference, lane).Position;
        var c = Frame.At(reference + 2, lane).Position;
        var u = b - a;
        var v = c - b;
        return 2 * ((double)u.X * v.Y - (double)u.Y * v.X) /
            Math.Max(1e-8, (double)u.Length() * v.Length() * (c - a).Length());
    }

    private static void MapEnvelope42(Lab38 lab, string output)
    {
        var geometry = lab.Geometry(true);
        var next = Map199.At(2, 1);
        double entry = next.Entry - lab.Bend.Entry;
        var bad = ReadArchive42(lab, "221/links/direct-12");
        var unload = ReadArchive42(lab, "221/links/direct-45");
        var best = ReadArchive42(lab, "221/transfer/transfer-nominal");
        var frontier = ReadArchive42(lab, "221/refined/linked-speed-frontier");
        var checkpoints = new List<(string Name, Checkpoint38 Mark, Step215 Step, Activation215[] Commands)>();
        foreach (var source in new[] { bad, unload, best, frontier })
        {
            foreach (double d in new[] { -38d, -28, -18, -10, -4, 0, 4, 8, 12, 17, 20, 30, 48, 73 })
            {
                int i = Array.FindIndex(source.Flight.Distances, x => x >= entry + d);
                if (i < 0) continue;
                var step = source.Flight.Steps[i];
                var state = new Checkpoint38(step.After, source.Flight.Distances[i]);
                checkpoints.Add((source.Name, state, step, source.Tape.Where(x => x.Requested < state.State.Time - 1e-10).ToArray()));
            }
        }
        var lines = new[] { 3.5, 5.6, 6.4, 8.5, 9.25 };
        var grade = (from kmh in new[] { 70d, 80, 90, 100, 107.33, 110, 115, 120 }
                     from line in lines
                     from offset in new[] { -12d, 0, 8, 24, 48, 82 }
                     let curve = LineCurvature42(next.Entry + offset, line)
                     select new { Kph = kmh, Lane = line, Offset = offset, Curvature = curve,
                         Demand = Math.Pow(kmh / 3.6, 2) * curve, ScalarUpperBound = Grip42 * Gravity216 })
            .ToArray();
        var probe = new object[checkpoints.Count];
        Parallel.For(0, probe.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var (name, at, step, pending) = checkpoints[i];
            var s = at.State;
            var motion = geometry.Motion(s);
            var mark = lab.Course.Mark(s, geometry);
            var instantaneous = ProjectForce42(geometry, s, step.Forces);
            var model = lab.Engine(geometry);
            var reach = new List<object>();
            foreach (float drive in new[] { 0f, .40f, 1f })
            foreach (double offset in new[] { -.11, 0, .11 })
            {
                var command = s.Command with { Bike = new(Steering39(s.Steer + offset), drive) };
                var flown = Window38(lab, geometry, model, at, Submit38(at, pending, command), .16, exact: true);
                double maxIn = flown.Steps.Select(x => LateralAcceleration38(x, geometry.Base.Mass)).DefaultIfEmpty().Max();
                double minimumHard = flown.Ground;
                var receipt = Measure38(flown.Steps, geometry, flown.Stop);
                var end = flown.Final.State;
                reach.Add(new { Drive = drive, Delta = offset, End = flown.Final, Receipt = receipt,
                    PeakInward = maxIn, MinimumHard = minimumHard, flown.Boundary,
                    EndYaw = lab.Course.Mark(end, geometry).AngularVelocity,
                    EndRollRate = RollRate36(end, geometry.Motion(end).BodyRate) });
            }
            double speed = Speed38(s, geometry);
            var tire = step.Forces;
            double curvature = LateralAcceleration38(step, geometry.Base.Mass) / Math.Pow(speed / 3.6, 2);
            var xyz = mark.Road.Tangent;
            var velocity = s.Momentum.Plane;
            double heading = Math.Atan2(velocity.Y, velocity.X);
            double path = Math.Atan2(xyz.Y, xyz.X);
            double reserve = ((1 - tire.Front.Utilization) * tire.Front.Normal + (1 - tire.Rear.Utilization) * tire.Rear.Normal)
                * Grip42 / geometry.Base.Mass;
            double legacy = Math.Min(11.4, 7.8 + .45 * Math.Max(0, reserve));
            probe[i] = new { Name = name, State = s, Pending = pending, at.Distance, Speed = speed, Heading = heading,
                HeadingError = Difference38(heading, path), FrameHeading = s.Pose.Yaw, RollRate = RollRate36(s, motion.BodyRate),
                YawRate = mark.AngularVelocity, mark.Road.OffsetMeters, Curvature = curvature, RearSpin = tire.RearSlip,
                Sideslip = SlipAngle38(s), FrontNormal = tire.Front.Normal, RearNormal = tire.Rear.Normal,
                FzFoot = tire.LeftFoot?.Contact.NormalForce ?? 0, RiderForce = tire.Rider,
                tire.FrontYawBand, tire.FrontRollBand, ScalarUnused = reserve, LegacyLimit = legacy,
                SweptAtState = H36Footprint.Clearance(lab.Track, geometry, s, true),
                HardAtState = H36Footprint.GroundMargin(geometry, s),
                Physical = instantaneous, Reachable = reach,
                LineDemand = lines.Select(lane => new { Lane = lane, Curvature = LineCurvature42(lab.Bend.Entry + at.Distance, lane),
                    Ahead = PreviewCurvature36(mark, lab.Bend.Entry + at.Distance + 19, lane) * Math.Pow(speed / 3.6, 2) })
            };
        });
        Save(Path.Combine(output, "directional-envelope.json.gz"), new { Geometry = grade, Probes = probe, NativeWorlds = 0 }, true);
        var steady = new object[24];
        Parallel.For(0, steady.Length, i =>
        {
            double speed = new[] { 70d, 80, 90, 100, 107.33, 110, 115, 120 }[i / 3];
            double lane = new[] { 0d, 5.6, 8.5 }[i % 3];
            double curve = lane == 0 ? 0 : LineCurvature42(next.Entry + 45, lane);
            var g = lab.Geometry(true);
            var model = lab.Engine(g);
            var equilibrium = SteadyCircle215(model, g, lab.Fixture.Rest(g, NominalSupport216),
                speed / 3.6, curve * speed / 3.6, lab.Fixture.Release + 1, lab.Fixture.Ratio, balanceRider: true);
            Forces215? f = equilibrium.Residual <= .005 ? model.Step(equilibrium.State, [], .0005).Forces : null;
            steady[i] = new { Kph = speed, Lane = lane, Curvature = curve, equilibrium.Residual,
                FeasibleRoot = f is not null, equilibrium.State, Forces = f,
                Clearance = H36Footprint.GroundMargin(g, equilibrium.State) };
        });
        Save(Path.Combine(output, "equilibria.json"), steady);
    }
}
