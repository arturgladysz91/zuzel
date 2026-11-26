using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Frontier42(Lab38 lab, string output)
    {
        var source = ReadArchive42(lab, "220/straight-full-diagnostic");
        var g = lab.Geometry(true);
        var refLine = SetupBaseline42() with { Id = 225, PreparationMeters = 32, Lane = 6.4, CornerRequestKph = 70 };
        var control = new Policy42(200, Family42.Archive, refLine, 0, 0, 0, 0, 0);
        int id = 200;
        var settings = new[] { control }.Concat(
            from distance in new[] { 26d, 30, 32, 36 }
            from shape in new[] { 1, 2, 3, 4 }
            let n = ++id
            let approach = refLine with { PreparationMeters = distance, Id = n }
            select new Policy42(n, Family42.Concurrent, approach, 2, .035, .12, .012, 0, EntryShape: shape)).ToArray();
        using var process = Process.GetCurrentProcess();
        var clock = Stopwatch.StartNew();
        var cpu = process.TotalProcessorTime;
        var r = settings.AsParallel().WithDegreeOfParallelism(8).Select(p =>
        {
            var trial = RunFrom42(lab, g, source, p);
            var score = Score42(lab, g, trial, p);
            return Summarize42(lab, p, trial, score);
        }).ToArray();
        Save(Path.Combine(output, "frontier-trials.json.gz"), r.OrderBy(t => t.Policy.Id).Select(t =>
            new { t.Policy, t.Score, t.ConcurrentSeconds, t.Run.Root, t.Run.Fork, t.Run.Commands, t.Run.EarlierCommands }), true);
        var fastest = r.Where(t => t.Score.Linked).OrderByDescending(t => t.Score.StraightPeak).ThenBy(t => t.Score.TotalSeconds)
            .Take(3).ToArray();
        var proof = fastest.AsParallel().WithDegreeOfParallelism(3).Select(x =>
        {
            var replay = ExactLink40(lab, g, x.Run);
            return Summarize42(lab, x.Policy, replay, Score42(lab, g, replay, x.Policy));
        }).OrderByDescending(t => t.Score.Linked).ThenByDescending(t => t.Score.StraightPeak).ToArray();
        if (proof.Length != 0)
        {
            var best = proof[0];
            WriteEvidence39(lab, output, "speed-frontier", g, best.Run.Root, best.Run.Commands, best.Run.Flight);
        }
        Save(Path.Combine(output, "frontier.json"), new { Control = control, Exact = proof.Select(t =>
            new { t.Policy, t.Score, t.ConcurrentSeconds }), CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
            WallSeconds = clock.Elapsed.TotalSeconds, NativeWorlds = 0 });
    }
}
