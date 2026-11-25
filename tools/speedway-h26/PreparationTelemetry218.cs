
using System.Numerics;
using System.Text.Json;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record TracePoint37(double Time, double Distance, double SpeedKph, double Offset, double Height,
        double Roll, double RollRate, double RollAcceleration, double RollMoment, double FrontRollMoment,
        double RearRollMoment, double LegRollMoment, double Yaw, double YawRate, double Sideslip,
        double RearTyreSlipAngle, double VelocityCurvature, double LateralAcceleration, double TangentialAcceleration,
        double Steer, double SteerRate, double FrontNormal, double RearNormal, double FrontFx, double FrontFy,
        double RearFx, double RearFy, double FrontUtilization, double RearUtilization, double FrontRollBand,
        float ActiveDrive, double Rpm, double Torque, double RearWheelspin, double RearRpm,
        Vec215 Rider, Vec215 RiderVelocity, Vec215 ActiveRiderTarget, double MuscleForce, double MusclePower,
        double FootNormal, double Ground, double SweptGround, double Boundary, double SweptBoundary,
        double EnergyResidual, double GyroscopicRollCouple, int NextActivation, int LatestRequest);
    private sealed record RequestPoint37(int Index, double RequestedAt, double ActivatedAt, double Distance,
        RollReference36? Target, Command215 Command, string Source, bool Reproduced, int[] Pending);
    private sealed record SampleAt37(double RequestedDistance, TracePoint37? State, RequestPoint37? Request,
        Activation215[] Pending);
    private sealed record Evidence37(Corner36 Summary, Preparation37? Strategy, Metric37 Metrics, State215 Seed,
        Activation215[] Commands, RequestPoint37[] Requests, TracePoint37[] Steps, SampleAt37[] Stations);

    private static void Reconstruct37(string root, string output, Fixture216 fixture, RacingLineGeometry track,
        CornerCourse216 course, BendVisit bend, float equilibriumDrive)
    {
        string data = Path.Combine(root, "data/experiments/speedway217/h36");
        string[] names = ["trajectory-40", "trajectory-50", "trajectory-60", "trajectory-70",
            "pace-trajectory-80", "trim-trajectory-80", "prepare-trajectory-90"];
        var archive = names.Select(name => Read199<Tape37>(Path.Combine(data, name + ".trajectory.json.gz"))).ToArray();
        var reviews = new List<(Trace36 Trace, Evidence37 Evidence, Geometry215 Geometry)>();
        foreach (var (i, label) in new[] { (4, "archive-77"), (6, "archive-91") })
        {
            var prior = archive[i];
            var tune = prior.Summary.Tune;
            var g = new Geometry215(fixture.Base, Axes215.All, tune.Leg ? new Leg216() : null, H36Footprint);
            var replay = RunH36(prior.Summary.Id, label, prior.Summary.NominalEntryKph, tune, g,
                fixture.Engine(g, NominalSupport216, Actuator215.Limited, true), prior.Seed, bend, track, course,
                equilibriumDrive, prior.Commands, true);
            if (replay.Summary.Steps != prior.Summary.Steps || replay.Summary.Stop != prior.Summary.Stop ||
                replay.Summary.Finish != prior.Summary.Finish)
                throw new InvalidOperationException($"Historical tape {names[i]} did not reproduce.");
            CheckArchive37(Path.Combine(data, names[i] + ".trajectory.json.gz"), replay);
            var owners = names.Zip(archive).Take(i).ToArray();
            var evidence = WriteComparison37(output, label, fixture, g, track, course, bend, replay, null,
                equilibriumDrive, owners);
            reviews.Add((replay, evidence, g));
        }
        var circles = CircleAudit37(fixture, bend, output);
        var probes = reviews.SelectMany(r => Interventions37(fixture, r.Geometry, track, course, bend, r.Trace,
            r.Evidence, equilibriumDrive)).ToArray();
        Save(Path.Combine(output, "diagnosis.json.gz"), new
        {
            Archive = reviews.Select(x => new { x.Trace.Summary, x.Evidence.Stations,
                MatchingRequests = x.Evidence.Requests.Count(t => t.Reproduced),
                RecordedRequests = x.Evidence.Requests.Length }),
            SteadyCircles = circles, Probes = probes,
            ActualProbeSteps = probes.Sum(p => p.Steps),
            Base = "6bf7b4afc773e8f6875fd3ebf6c691ddd545a651",
            NativeWorlds = 0, Actuator = Actuator215.Limited, Steering = Steering217, ReactionSeconds216,
            StepSeconds216, Gravity216, fixture.Base.Mass, ApproachStations = Stations37
        }, true);
    }

    private static void CheckArchive37(string path, Trace36 replay)
    {
        using var stream = new System.IO.Compression.GZipStream(File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(stream);
        var states = doc.RootElement.GetProperty("AcceptedStates");
        if (states.GetArrayLength() != replay.Trace.Length) throw new InvalidOperationException("Historical replay length.");
        foreach (var (expected, actual) in states.EnumerateArray().Zip(replay.Trace))
        {
            var s = actual.After;
            double[] values = [s.Time, s.Center.X, s.Center.Y, s.Center.Z, s.Momentum.X, s.Momentum.Y, s.Momentum.Z,
                s.Pose.W, s.Pose.V.X, s.Pose.V.Y, s.Pose.V.Z, s.Angular.X, s.Angular.Y, s.Angular.Z,
                s.Rider.X, s.Rider.Y, s.Rider.Z, s.RiderMomentum.X, s.RiderMomentum.Y, s.RiderMomentum.Z,
                s.Crank, s.Wheel, s.Steer, s.Leg.Extension, s.Leg.Momentum, s.Command.Bike.Drive];
            for (int j = 0; j < values.Length; ++j)
            {
                // The archive serializes Drive as float. Its shortest decimal may differ
                // from reading that same value first as a double.
                double x = j == 25 ? expected[j].GetSingle() : expected[j].GetDouble();
                if (x != values[j]) throw new InvalidOperationException($"Historical trace mismatch at {s.Time:R}s, field {j}.");
            }
        }
    }

    private static readonly double[] Stations37 = [-50, -40, -30, -20, -10, -5, 0, 5, 10, 15, 20];
    private static Evidence37 WriteComparison37(string output, string label, Fixture216 fixture,
        Geometry215 g, RacingLineGeometry track, CornerCourse216 course, BendVisit bend, Trace36 replay,
        Preparation37? plan, float equilibriumDrive, (string First, Tape37 Second)[]? parents = null)
    {
        var engine = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
        var requests = new List<RequestPoint37>();
        var observations = new List<TracePoint37>(replay.Trace.Length);
        var queue = replay.Commands;
        int command = 0;
        double s = replay.Summary.Start.Reference;
        double boundary = H36Footprint.Clearance(track, g, replay.Seed);
        double ground = H36Footprint.GroundMargin(g, replay.Seed);
        foreach (var row in replay.Trace)
        {
            var before = course.Mark(row.Before, g);
            while (command < queue.Length && queue[command].Requested <= row.Before.Time + 1e-10)
            {
                var ev = queue[command];
                var owner = parents?.FirstOrDefault(x => command < x.Second.Commands.Length &&
                    x.Second.Seed == replay.Seed && queue.Take(command + 1).SequenceEqual(x.Second.Commands.Take(command + 1)));
                var own = owner?.Second;
                var prior = queue[..command];
                RollReference36? guide = null;
                var policy = H36Control(row.Before, g, engine, before, s, bend, own?.Summary.Tune ?? replay.Summary.Tune,
                    own?.Summary.NominalEntryKph ?? replay.Summary.NominalEntryKph, equilibriumDrive, prior, course,
                    plan, x => guide = x);
                requests.Add(new(command, ev.Requested, ev.Time, s - bend.Entry, guide, ev.Command,
                    own is null ? label : owner!.Value.First, policy.Command == ev.Command,
                    Enumerable.Range(row.Before.Next, Math.Max(0, command - row.Before.Next)).ToArray()));
                command++;
            }
            var tail = course.Mark(row.After, g);
            s += Frame.UnwrapDelta(before.Road.ReferenceMeters, tail.Road.ReferenceMeters);
            var f = row.Forces;
            var m = g.Motion(row.Midpoint);
            var velocity = row.Midpoint.Momentum.Plane / g.Base.Mass;
            var accel = (row.After.Momentum - row.Before.Momentum).Plane / (g.Base.Mass * Duration215(row));
            double lateral = velocity.Cross(accel) / velocity.Norm;
            double rollAccel = (RollRate36(row.After, g.Motion(row.After).BodyRate) -
                RollRate36(row.Before, g.Motion(row.Before).BodyRate)) / Duration215(row);
            var basis = row.Midpoint.Pose.Apply(Vec215.Forward);
            double footMoment = f.LeftFoot is { } boot ? basis.Dot(boot.Arm.Cross(boot.Force)) : 0;
            var wheelP = row.Midpoint.Pose.Apply(Vec215.Left) * (row.Midpoint.Crank + row.Midpoint.Wheel);
            double gyro = -basis.Dot(m.WorldRate.Cross(wheelP));
            double gapAfter = H36Footprint.Clearance(track, g, row.After);
            double groundAfter = H36Footprint.GroundMargin(g, row.After);
            observations.Add(new(row.After.Time, s - bend.Entry, tail.Speed * 3.6, tail.Road.OffsetMeters, row.After.Center.Z,
                row.After.Pose.Roll, RollRate36(row.After, g.Motion(row.After).BodyRate), rollAccel,
                basis.Dot(row.RollPitchYawMoment), basis.Dot(f.Front.Patch.Arm.Cross(f.Front.Force)),
                basis.Dot(f.Rear.Patch.Arm.Cross(f.Rear.Force)), footMoment, row.After.Pose.Yaw, tail.AngularVelocity,
                tail.SlideRadians, Math.Atan2(f.Rear.Patch.Velocity.Dot(f.Rear.Patch.Side), f.Rear.Patch.Velocity.Dot(f.Rear.Patch.Forward)),
                lateral / velocity.Dot(velocity), lateral, velocity.Dot(accel) / velocity.Norm, row.After.Steer, f.SteerRate,
                f.Front.Normal, f.Rear.Normal, f.Front.Longitudinal, f.Front.Lateral, f.Rear.Longitudinal, f.Rear.Lateral,
                f.Front.Utilization, f.Rear.Utilization, f.FrontRollBand, row.Before.Command.Bike.Drive, f.EngineRpm,
                f.NetEngineTorque, f.RearSlip, (row.Midpoint.Wheel / g.Base.WheelInertia - m.BodyRate.Y) * 30 / Math.PI,
                row.After.Rider, m.RiderRate, row.Before.Command.Target,
                f.Rider.Norm + Math.Abs(f.LeftFoot?.MuscleForce ?? 0),
                f.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(row.ActuatorPower), f.LeftFoot?.Contact.NormalForce ?? 0,
                groundAfter, Math.Min(ground, groundAfter) - H36Footprint.TransportBound(g, row, groundOnly: true) / 2,
                gapAfter, Math.Min(boundary, gapAfter) - H36Footprint.TransportBound(g, row) / 2,
                row.EnergyResidual, gyro, row.After.Next, command - 1));
            boundary = gapAfter;
            ground = groundAfter;
        }

        var stations = Stations37.Select(d =>
        {
            var state = observations.FirstOrDefault(x => x.Distance >= d);
            var request = state is not null && state.LatestRequest >= 0 ? requests[state.LatestRequest] : null;
            var pending = state is null ? [] : queue.Skip(state.NextActivation).Take(state.LatestRequest + 1 - state.NextActivation).ToArray();
            return new SampleAt37(d, state, request, pending);
        }).ToArray();
        var evidence = new Evidence37(replay.Summary, plan, Metrics37(replay, g.Base.Mass, bend), replay.Seed, queue,
            requests.ToArray(), observations.ToArray(), stations);
        Save(Path.Combine(output, label + ".json.gz"), evidence, true);
        return evidence;
    }

    private static object[] CircleAudit37(Fixture216 fixture, BendVisit bend, string output)
    {
        double trackRadius = Frame.RoadAt((bend.Entry + bend.Exit) / 2, 6.5).RadiusMeters;
        var g = new Geometry215(fixture.Base, Axes215.All, spatial: H36Footprint);
        var engine = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
        var observations = new List<object>();
        foreach (var (kph, a) in new[] { (40, 0d), (55, 0d), (70, 0d), (70, 4d), (80, 4d), (90, 4d), (100, 4d) })
        {
            double v = kph / 3.6;
            double radius = a == 0 ? trackRadius : v * v / a;
            double expectedAccel = v * v / radius;
            double idealLean = Math.Atan(expectedAccel / Gravity216);
            var b = SteadyCircle215(engine, g, fixture.Rest(g, NominalSupport216), v, v / radius,
                fixture.Release + 1, fixture.Ratio, balanceRider: true);
            if (b.Residual > .005)
            {
                observations.Add(new { Kph = kph, radius, expectedAccel, idealLean, b.Residual, b.Iterations,
                    Result = "No converged steady-circle solution in the equilibrium solver bounds." });
                continue;
            }
            var run = MechanicalTrial216($"circle-{kph}-{a}", "speed-dependent", g, engine, b.State, 1.25,
                StepSeconds216, [], null, Gravity216);
            if (run.Steps.Length == 0)
                throw new InvalidOperationException($"Circle {kph}/{a} accepted no steps.");
            Save(Path.Combine(output, $"circle-{kph}-{a}.json.gz"), run, true);
            var row = run.Steps[run.Steps.Length / 2];
            var v2 = row.Midpoint.Momentum.Plane / g.Base.Mass;
            var change = (row.After.Momentum - row.Before.Momentum).Plane / (g.Base.Mass * Duration215(row));
            var f = row.Forces;
            var cop = (f.Front.Patch.Arm * f.Front.Normal + f.Rear.Patch.Arm * f.Rear.Normal) /
                (f.Front.Normal + f.Rear.Normal);
            var gyro = g.Motion(row.Midpoint).WorldRate.Cross(row.Midpoint.Pose.Apply(Vec215.Left) *
                (row.Midpoint.Crank + row.Midpoint.Wheel));
            observations.Add(new { Kph = kph, radius, expectedAccel, idealLean, b.Residual, b.Iterations,
                ActualAccel = v2.Cross(change) / v2.Norm, ActualCurvature = v2.Cross(change) / Math.Pow(v2.Norm, 3),
                BodyRoll = row.Midpoint.Pose.Roll, EffectiveComLean = Math.Atan2(-v2.Cross(cop.Plane) / v2.Norm, -cop.Z),
                ForceLean = Math.Atan2(v2.Cross(f.GroundForce.Plane) / v2.Norm, f.GroundForce.Z),
                RollingAxleContribution = g.Motion(row.Midpoint).BodyRate.Y, row.Midpoint.Wheel, row.Midpoint.Crank,
                GyroscopicRollCouple = -gyro.Dot(row.Midpoint.Pose.Apply(Vec215.Forward)), row.Midpoint.Rider,
                f.Front.Normal, RearNormal = f.Rear.Normal, f.FrontRollBand, f.RearSlip,
                run.Summary.Stop, run.Summary.Seconds, RollDrift = run.Summary.Final.Pose.Roll - b.State.Pose.Roll,
                run.Summary.LinearDefect, run.Summary.AngularDefect, run.Summary.PeakEnergyDefect });
        }
        return observations.ToArray();
    }
}
