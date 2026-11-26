using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record CyclePolicy42(Policy42 Policy, double SwitchAfterExitMeters);

    private static void Cycle42(Lab38 lab, string output)
    {
        var source = ReadMotion42(lab, Path.Combine(output, "nominal.json.gz"), "frozen linked nominal");
        var g = lab.Geometry(true);
        double lap = Frame.LapMeters;
        double exit = Map199.At(2, 1).Exit - lab.Bend.Entry;
        var baseline = SetupBaseline42();
        int id = 300;
        var plans = (from point in new[] { 7d, 14, 21.9 }
                     from effort in Enumerable.Range(0, 4)
                     let n = ++id
                     let setup = baseline with { Id = n, PreparationMeters = new[] { 47d, 52, 55, 44 }[effort],
                         CornerRequestKph = new[] { 74d, 69, 72, 72 }[effort], Lane = new[] { 5.6, 6.2, 5.6, 6.8 }[effort] }
                     select new CyclePolicy42(new Policy42(n, Family42.Concurrent, setup, 1.15, .013, .10, 0, 0,
                         RollBackup: new[] { .72, .95, .63, .72 }[effort], DampingAdd: new[] { 6d, 9, 11, 7 }[effort]), point))
            .ToArray();
        var reduced = plans.AsParallel().WithDegreeOfParallelism(8).Select(p =>
        {
            var r = Ride(p, lap + 22);
            var f = r.Flight;
            return new
            {
                Plan = p, r.Fork, r.Root, r.EarlierCommands, r.Commands, f.Final, f.Stop, f.Boundary, f.Ground,
                Passed = f.Final.Distance >= lap + 22 - .001,
                Seconds = f.Final.State.Time - r.Root.State.Time
            };
        }).OrderByDescending(x => x.Passed)
            .ThenBy(x => x.Passed ? x.Seconds : -x.Final.Distance)
            .ThenBy(x => x.Plan.Policy.Id).ToArray();
        Save(Path.Combine(output, "cycle-trials.json.gz"), reduced, true);
        var best = reduced[0].Plan;
        Save(Path.Combine(output, "cycle-freeze.json"), best);
        using var process = Process.GetCurrentProcess();
        var start = process.TotalProcessorTime;
        var stop = Stopwatch.StartNew();
        var raw = Ride(best, reduced[0].Passed ? 2 * lap + 22 : lap + 22);
        double cpu = (process.TotalProcessorTime - start).TotalSeconds;
        double wall = stop.Elapsed.TotalSeconds;
        var replay = ExactLink40(lab, g, raw);
        var fly = replay.Flight;
        var gates = new[] { 0d, lap, lap + 22, 2 * lap, 2 * lap + 22 }.Select(d =>
        {
            int i = Array.FindIndex(fly.Distances, x => x >= d);
            return new { Station = d, Time = i < 0 ? (double?)null : fly.Steps[i].After.Time,
                Kph = i < 0 ? (double?)null : Speed38(fly.Steps[i].After, g) };
        }).ToArray();
        Save(Path.Combine(output, "cycle.json"), new { Policy = best, LapMeters = lap, Gates = gates, fly.Final,
            fly.Stop, fly.Boundary, fly.Ground, SearchPasses = reduced.Count(x => x.Passed), CpuSeconds = cpu, WallSeconds = wall,
            Timing = Score42(lab, g, replay, best.Policy),
            Missed50 = raw.Decisions.Count(x => x.Milliseconds > 50), Count = raw.Decisions.Length, NativeWorlds = 0 });
        WriteEvidence39(lab, output, "cycle-flying", g, replay.Root, replay.Commands, fly);

        Run40 Ride(CyclePolicy42 p, double end)
        {
            var prefix = Slice42(source, exit + p.SwitchAfterExitMeters);
            return Ride42(lab, g, source.Seed, prefix.At, prefix.Prefix, prefix.Distances, prefix.Commands, p.Policy, end);
        }
    }
}
