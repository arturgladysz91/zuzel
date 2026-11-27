namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record TraceProof44(string Name, bool ExactTerminal, double PositionError, double LinearError,
        double AngularError, double Boundary, double Ground, double MaximumEnergyError,
        double MaximumMuscleForce, double MaximumMusclePower, double Station, int Steps, int Commands,
        double AcceptedPathMeters, LapReceipt44[] Laps, Phase43[] Seams, string Stop);

    private static void VerifyStudy44(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        var names = new[] { "line-selected", "speed-selected", "speed-peak-diagnostic", "recourse-selected",
            "initial-four", "support-recourse-selected", "four-attempt", "speed-recourse-selected", "feedback-nominal", "carrier-retained-nominal" };
        var recovered = names.Where(n => File.Exists(Path.Combine(output, n + ".json.gz"))).AsParallel().AsOrdered()
            .WithDegreeOfParallelism(Jobs44).Select(name =>
        {
            string path = Path.Combine(output, name + ".json.gz");
            var archive = Read199<Evidence39>(path);
            var m = ReadMotion42(lab, path, name);
            var a = archive.Rows[^1].State;
            var b = m.Flight.Final.State;
            var records = m.Flight.Steps;
            double kinetic = 0, work = 0, force = 0, power = 0;
            int mark = Index43(m.Flight.Distances, 0);
            foreach (var r in records)
            {
                kinetic += r.EnergyResidual;
                work = Math.Max(work, Math.Abs(kinetic));
                force = Math.Max(force, r.Forces.Rider.Norm + Math.Abs(r.Forces.LeftFoot?.MuscleForce ?? 0));
                power = Math.Max(power, r.Forces.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(r.ActuatorPower));
            }
            var f = m.Flight;
            double station = f.Final.Distance;
            int laps = Math.Clamp((int)Math.Ceiling(station / Frame.LapMeters), 1, 5);
            var proof = new TraceProof44(name, a == b, (a.Center - b.Center).Norm, (a.Momentum - b.Momentum).Norm,
                (a.Angular - b.Angular).Norm, f.Boundary, f.Ground, work, force, power, station, records.Length, m.Tape.Length,
                records.Skip(mark + 1).Sum(r => (r.After.Center - r.Before.Center).Plane.Norm),
                Enumerable.Range(0, laps).Select(n => Lap44(lab, g, m, n)).ToArray(),
                Enumerable.Range(0, laps + 1).Select(n => n * Frame.LapMeters)
                    .Where(mark => mark <= station).Select(mark => AtPhase43(lab, g, m, mark)).ToArray(),
                f.Stop);
            return (Motion: m, Data: proof);
        }).ToArray();
        Save(Path.Combine(output, "replay-proof.json"), new { Base = Base44, Proofs = recovered.Select(r => r.Data), NativeWorlds = 0 });
        Save(Path.Combine(output, "route-profiles.json.gz"), recovered.ToDictionary(x => x.Data.Name, x =>
        {
            var rows = x.Motion.Flight.Steps;
            return rows.Select((r, n) => (r, n)).Where(v => v.n % 10 == 0 || v.n == rows.Length - 1).Select(v =>
            {
                var r = v.r;
                var mark = lab.Course.Mark(r.After, g);
                var velocity = r.Midpoint.Momentum.Plane / g.Base.Mass;
                var a = (r.After.Momentum - r.Before.Momentum).Plane / (g.Base.Mass * Duration215(r));
                var rear = r.Forces.Rear.Patch;
                var front = r.Forces.Front.Patch;
                double tyreAngle(Patch215 patch) => Math.Atan2(patch.Velocity.Dot(patch.Side), patch.Velocity.Dot(patch.Forward));
                return new
                {
                    Station = x.Motion.Flight.Distances[v.n], r.After.Time, Offset = mark.Road.OffsetMeters,
                    Speed = Speed38(r.After, g), Curvature = velocity.Cross(a) / Math.Max(.001, Math.Pow(velocity.Norm, 3)),
                    ForwardAcceleration = velocity.Dot(a) / Math.Max(.001, velocity.Norm),
                    Heading = Heading38(r.After), HeadingError = Difference38(Heading38(r.After),
                        Math.Atan2(mark.Road.Tangent.Y, mark.Road.Tangent.X)),
                    r.After.Pose.Roll, r.After.Pose.Yaw, YawRate = mark.AngularVelocity,
                    RollMomentum = r.After.Pose.Body(r.After.Angular).X, YawMomentum = r.After.Angular.Z,
                    VehicleSideslip = SlipAngle38(r.After), RearSideslip = tyreAngle(rear), FrontSideslip = tyreAngle(front),
                    r.Forces.RearSlip, r.Before.Command.Bike.Drive, r.After.Steer, r.After.Rider,
                    FrontNormal = r.Forces.Front.Normal, RearNormal = r.Forces.Rear.Normal,
                    Boundary = H36Footprint.Clearance(lab.Track, g, r.After, false), Ground = H36Footprint.GroundMargin(g, r.After)
                };
            }).ToArray();
        }), true);
    }
}
