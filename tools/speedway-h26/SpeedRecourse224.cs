using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record SpeedFork44(int Id, Fork40 Source, double First100);

    private static void SpeedRecourse44(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        var study = Read199<Stage44>(Path.Combine(output, "speed-search.json.gz"));
        var line = ReadPlain214<Piece44[]>(Path.Combine(output, "line-selected-policy.json"))[0].Route;
        var root = Archived44(lab, "speedway222/frontier/speed-frontier", "H41 reached frontier");
        double entry = lab.Bend.NextEntry - lab.Bend.Entry;
        double exit = SecondExit44(lab);
        double end = exit + PostMarkerMeters43;
        var clock = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var candidateIds = study.Proposals.Where(x => x.PeakBeforeEnd >= 100)
            .OrderByDescending(x => x.Station).Take(2)
            .Concat(study.Proposals.Where(x => x.PeakBeforeEnd >= 100).OrderByDescending(x => x.PeakBeforeEnd).Take(1))
            .DistinctBy(x => x.Id).ToArray();
        if (candidateIds.Length == 0)
        {
            Save(Path.Combine(output, "speed-recourse.json.gz"),
                new { Sources = Array.Empty<int>(), Proposals = Array.Empty<int>(), Exact = Array.Empty<int>(), Winner = (Candidate44?)null, NativeWorlds = 0 }, true);
            return;
        }
        var forks = candidateIds.Select(p =>
        {
            var r = Ride44(lab, g, root, p.Schedule[0].Start, p.Schedule, end);
            var source = r.Motion;
            int i = Array.FindIndex(source.Flight.Distances, (d) => d >= lab.Bend.Length && d <= entry);
            while (i >= 0 && i < source.Flight.Steps.Length && source.Flight.Distances[i] <= entry &&
                   Speed38(source.Flight.Steps[i].After, g) < 100) i++;
            if (i < 0 || i >= source.Flight.Steps.Length || source.Flight.Distances[i] > entry)
                throw new InvalidOperationException("The measured speed fork no longer reaches 100 km/h.");
            return new SpeedFork44(p.Id, source, source.Flight.Distances[i]);
        }).ToArray();
        string checkpoint = Path.Combine(output, "speed-recourse-checkpoints");
        Directory.CreateDirectory(checkpoint);
        var trials = forks.SelectMany((f, i) => Enumerable.Range(0, 8).Select(j => (Fork: f, Proposal: Plan(f, i, j)))).ToArray();
        var order = Comparer<Candidate44>.Create((a, b) => Compare44(a, b, fast: true));
        var screened = trials.AsParallel().WithDegreeOfParallelism(Jobs44).Select(x => Persist44(checkpoint, x.Proposal.Id, () =>
        {
            var r = Ride44(lab, g, x.Fork.Source, x.Proposal.ForkMeters, x.Proposal.Schedule, end);
            return new ReplanTest44(x.Proposal, Measure44(lab, g, r, true));
        })).ToArray();
        var ids = screened.OrderBy(x => x.Outcome, order).Take(5).Select(x => x.Proposal.Id).ToHashSet();
        var exact = trials.Where(x => ids.Contains(x.Proposal.Id)).AsParallel().WithDegreeOfParallelism(Jobs44).Select(x =>
        {
            var r = ExactReplay44(lab, g, Ride44(lab, g, x.Fork.Source, x.Proposal.ForkMeters, x.Proposal.Schedule, end));
            return new Exact44(Measure44(lab, g, r, true), r);
        }).OrderBy(x => x.Score, order).ToArray();
        var best = exact[0];
        Save(Path.Combine(output, "speed-recourse.json.gz"), new
        {
            Sources = forks.Select(r => new { r.Id, r.First100 }),
            Proposals = screened.OrderBy(x => x.Proposal.Id),
            Exact = exact.Select(x => x.Score), Winner = best.Score,
            CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds, WallSeconds = clock.Elapsed.TotalSeconds, NativeWorlds = 0
        }, true);
        var motion = best.Run.Motion;
        WriteEvidence39(lab, output, "speed-recourse-selected", g, motion.Seed, motion.Tape, motion.Flight);
        Console.WriteLine($"H43 speed recourse: {screened.Length} screened, {exact.Count(x => x.Score.Pass)} exact linked, " +
            $"top {best.Score.Station:F2} m with {best.Score.PeakBeforeEnd:F2} km/h.");

        ReplanProposal44 Plan(SpeedFork44 f, int i, int option)
        {
            int id = 400 + i * 8 + option;
            double last = f.Source.Flight.Final.Distance;
            double fork = Math.Min(last - .30, f.First100 + new[] { .04, .15, .35, 1.6, 3.5, 5.8, 7.9, 11.4 }[option]);
            var at = AtPhase43(lab, g, f.Source, fork);
            double offset = Math.Clamp(at.Offset, 3.5, 10);
            var entryPolicy = line with
            {
                Id = id, StartStation = fork, EntryOutside = offset + new[] { -.1, .6, -.2, 1.4, -.6, .7, .25, .6 }[option],
                Inside = Math.Clamp(offset - (option % 2 == 0 ? .75 : 1.35), 3.6, 8.4),
                StraightKph = 78 + 2.4 * option, TurnAcceleration = 7.2 + .49 * option, LeadMeters = 42 + 2 * (option % 4),
                PreviewMeters = line.PreviewMeters + (option % 4 - 1) * 2.8, Deceleration = 2.2 + .31 * option,
                EarlyHeading = 5.5 + .65 * option, Damping = 13 + (option % 4) * 1.4,
                PreserveFrontier = false, Guide = (Guide44)(option % 4)
            };
            var recovery = line with
            {
                Id = id, StartStation = exit - 11, StraightKph = 94 + option, ExitDrive = .78, TurnAcceleration = 8.2,
                PreviewMeters = line.PreviewMeters + .9, Damping = 15
            };
            return new(id, fork, [new(fork, entryPolicy), new(recovery.StartStation, recovery)]);
        }
    }
}
