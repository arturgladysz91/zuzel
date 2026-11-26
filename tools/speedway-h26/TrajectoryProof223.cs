namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record SearchFile43(SearchResult43[] Screening, SearchResult43[] Verified);

    private static void ProveTrajectories43(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        var band = Read199<SearchFile43>(Path.Combine(output, "stage-B.json.gz"));
        var maximal = band.Verified.OrderByDescending(x => x.Score.StraightPeak).First();
        string parent = maximal.Plan.Parent == "H41 95.63 km/h" ?
            "speedway222/frontier/speed-frontier" : "speedway222/qualification/nominal";
        var baseMotion = Read223(lab, ArchivePath43(lab, parent), maximal.Plan.Parent);
        double finish = Map199.At(2, 1).Exit - lab.Bend.Entry + PostMarkerMeters43;
        var high = Prove43(lab, g, baseMotion, maximal.Plan, finish);
        if (Math.Abs(high.Result.Score.StraightPeak - maximal.Score.StraightPeak) > .00002)
            throw new InvalidOperationException("Stage B speed witness failed the independent replay.");
        WriteEvidence39(lab, output, "speed-highest", g, high.Motion.Seed, high.Motion.Tape, high.Motion.Flight);
        var keys = new[] { "shoot-selected", "shoot-repair-selected", "speed-highest", "periodic-selected", "periodic-closure-selected", "periodic-closure-local-selected", "feedback-nominal" };
        var motions = new Dictionary<string, Fork40>();
        var figures = keys
            .Where(key => File.Exists(Path.Combine(output, key + ".json.gz")))
            .Select(key =>
            {
                string path = Path.Combine(output, key + ".json.gz");
                var archive = Read199<Evidence39>(path);
                var verified = Read223(lab, path, key);
                motions.Add(key, verified);
                var expected = archive.Rows[^1].State;
                var observed = verified.Flight.Final.State;
                var proof = Score43(lab, g, verified);
                return new
                {
                    Name = key, ExactTerminal = expected == observed, PositionDefect = (expected.Center - observed.Center).Norm,
                    LinearMomentumDefect = (expected.Momentum - observed.Momentum).Norm,
                    AngularMomentumDefect = (expected.Angular - observed.Angular).Norm,
                    proof.Swept, proof.Hard, proof.EnergyError, verified.Flight.Final.Distance, Verified = verified.Flight.Steps.Length
                };
            }).ToArray();
        Save(Path.Combine(output, "independent-proof.json"), new { Retested = figures, HighestDiagnostic = high.Result, NativeWorlds = 0 });
        Save(Path.Combine(output, "path-profile.json.gz"), motions.ToDictionary(x => x.Key, x =>
        {
            var m = x.Value;
            return m.Flight.Steps.Select((step, i) => (step, i)).Where(v => v.i % 10 == 0 || v.i == m.Flight.Steps.Length - 1)
                .Select(v =>
                {
                    var s = v.step;
                    var current = lab.Course.Mark(s.After, g);
                    var velocity = s.Midpoint.Momentum.Plane / g.Base.Mass;
                    double dt = Duration215(s);
                    var accel = (s.After.Momentum - s.Before.Momentum).Plane / (g.Base.Mass * dt);
                    return new
                    {
                        Station = m.Flight.Distances[v.i], s.After.Time, OffsetMeters = current.Road.OffsetMeters,
                        SpeedKph = 3.6 * current.Speed, VelocityHeading = Heading38(s.After), BodyYaw = s.After.Pose.Yaw,
                        Curvature = velocity.Cross(accel) / Math.Max(.01, Math.Pow(velocity.Norm, 3)),
                        LongitudinalAcceleration = velocity.Dot(accel) / Math.Max(.01, velocity.Norm),
                        s.Before.Command.Bike.Drive, s.Forces.RearSlip, VehicleSlip = SlipAngle38(s.After),
                        RearTyreSlip = Math.Atan2(s.Forces.Rear.Patch.Velocity.Dot(s.Forces.Rear.Patch.Side),
                            s.Forces.Rear.Patch.Velocity.Dot(s.Forces.Rear.Patch.Forward)),
                        s.After.Pose.Roll, s.After.Angular, FrontLoad = s.Forces.Front.Normal, RearLoad = s.Forces.Rear.Normal,
                        GroundReserve = H36Footprint.GroundMargin(g, s.After),
                        BoundaryReserve = H36Footprint.Clearance(lab.Track, g, s.After, false)
                    };
                }).ToArray();
        }), true);
    }
}
