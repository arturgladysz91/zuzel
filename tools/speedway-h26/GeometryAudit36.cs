
using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record ArchivedClearance36(int Id, string LegacyStop, double LegacyMinimum, double NewSweep,
        double NewGround, double NewClearanceAtEnd, double CompactGroundAtRejected, double LargeGroundAtRejected,
        double RollAtRejected, string Finding);
    private sealed record Lean36(double Degrees, bool OldGroundClear, double Nominal, double Compact, double Large);

    private static void H36Geometry(string root, string output, Fixture216 fixture, CornerCourse216 course, RacingLineGeometry track)
    {
        string baseData = Path.Combine(root, "data/experiments/speedway216/corner-search.json");
        var archive = ReadPlain214<SearchArchive217>(baseData);
        var compact = new SpatialBike36(.70, .095, -.120, .24, .90);
        var large = new SpatialBike36(.90, .135, -.180, .32, 1.10);
        var geometry = fixture.Geometry();
        var upright = fixture.Rest(geometry, NominalSupport216);
        var screen = new List<Lean36>();
        for (int degrees = 0; degrees <= 60; degrees++)
        {
            var leant = upright with { Pose = Attitude215.Pose(0, 0, -degrees * Math.PI / 180) };
            leant = geometry.Supported(leant, Gravity216, UnifiedModel215.Stiffness);
            screen.Add(new(degrees, geometry.ChassisClear(leant), H36Footprint.GroundMargin(geometry, leant),
                compact.GroundMargin(geometry, leant), large.GroundMargin(geometry, leant)));
        }

        var trials = archive.Attempts.Where(r => r.Stop is "swept boundary clearance" or "chassis-ground envelope").ToArray();
        var results = new ArchivedClearance36[trials.Length];
        var watch = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var started = process.TotalProcessorTime;
        double worstDense = 0;
        int denseSamples = 0;
        var guard = new object();
        Parallel.For(0, trials.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
        {
            var old = trials[i];
            var g = fixture.Geometry(old.Settings.Leg ? new Leg216() : null);
            var engine = fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
            State215? rejected = null;
            var result = RunCorner216(old.Id, old.Scenario, old.RequestedKph, g, engine, old.Initial, old.InitialReference,
                archive.Visit, old.Settings, 9, course, rejectedChassis: e => rejected = e.Candidate);
            if (old.Stop != result.Summary.Stop || old.Final != result.Summary.Final)
                throw new InvalidOperationException($"legacy geometry audit did not reproduce archived row {old.Id}");
            double clearance = H36Footprint.Clearance(track, g, old.Initial, exact: false);
            double min = clearance, minGround = H36Footprint.GroundMargin(g, old.Initial);
            foreach (var row in result.Trace)
            {
                double after = H36Footprint.Clearance(track, g, row.After, exact: false);
                double motion = H36Footprint.TransportBound(g, row) / 2;
                double lower = Math.Min(clearance, after) - motion;
                min = Math.Min(min, lower);
                minGround = Math.Min(minGround,
                    Math.Min(H36Footprint.GroundMargin(g, row.Before), H36Footprint.GroundMargin(g, row.After)) -
                    H36Footprint.TransportBound(g, row, groundOnly: true) / 2);
                if (i % 29 == 0 && ((row.After.Time - old.Initial.Time) > .11))
                {
                    var m = g.Motion(row.Midpoint);
                    for (int slice = 1; slice <= 3; slice++)
                    {
                        double dt = Duration215(row) * slice / 4;
                        var dense = row.Before with
                        {
                            Center = row.Before.Center + (row.After.Center - row.Before.Center) * (slice / 4d),
                            Pose = row.Before.Pose.Advance(m.WorldRate, dt), Rider = row.Before.Rider + m.RiderRate * dt,
                            Steer = row.Before.Steer + row.Forces.SteerRate * dt,
                            Leg = row.Before.Leg with { Extension = row.Before.Leg.Extension + m.LegRate * dt }
                        };
                        double gap = H36Footprint.Clearance(track, g, dense);
                        lock (guard) { denseSamples++; worstDense = Math.Max(worstDense, lower - gap); }
                    }
                }
                clearance = after;
            }
            var tail = rejected ?? result.Summary.Final;
            double ground = H36Footprint.GroundMargin(g, tail);
            string finding = old.Stop == "swept boundary clearance"
                ? min >= SpatialBike36.RequiredSweptMeters
                    ? minGround > 0 ? "projection-only rejection for complete archived prefix" : "spatial track clear; ground bound unresolved"
                    : "retained spatial clearance restriction"
                : ground > 0 ? "legacy chassis witness only at rejected endpoint" : "retained hard-part ground contact";
            results[i] = new(old.Id, old.Stop, old.MinimumSwept, min, minGround,
                H36Footprint.Clearance(track, g, tail), compact.GroundMargin(g, tail), large.GroundMargin(g, tail),
                tail.Pose.Roll, finding);
        });
        if (worstDense > 1e-6) throw new InvalidOperationException("sampled trajectory violated the swept lower bound");
        Save(Path.Combine(output, "geometry-h36.json"), new
        {
            Names = new { Nominal = H36Footprint, Compact = compact, Large = large },
            LeanScreen = screen, Historical = results, DenseProjectionSamples = denseSamples, WorstOptimisticMeters = worstDense,
            Counts = results.GroupBy(r => r.Finding).ToDictionary(g => g.Key, g => g.Count()),
            CpuSeconds = (process.TotalProcessorTime - started).TotalSeconds, WallSeconds = watch.Elapsed.TotalSeconds,
            Scope = "Historical replay to the original rejection frontier. Spatial clearance acceptance does not certify the continuation."
        });
        Console.WriteLine($"Geometry reproduced {results.Length} archived refusals with {denseSamples} dense projection checks.");
    }
}
