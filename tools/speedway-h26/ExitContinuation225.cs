namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Fork45(int Source, Fork40 Motion, double Station, Phase43 Phase);
    private sealed record Recovery45(Fork45 Fork, Plan45 Plan);

    private static void Downstream45(Lab38 lab, Geometry215 g, string output)
    {
        var archive = Read199<Stage45>(Path.Combine(output, "speed.json.gz"));
        var baseline = Baseline45(lab);
        double exit = lab.Bend.Length, next = Half44, finish = exit + next + PostMarkerMeters43;
        var proofs = archive.Exact.Where(x => x.StraightPeakKph >= 100 &&
            File.Exists(Path.Combine(output, $"speed-{x.Plan.Id}.json.gz"))).ToArray();
        if (proofs.Length == 0) throw new InvalidOperationException("No exact ordinary-straight crossing to continue.");

        var selected = proofs.OrderByDescending(p => p.Station).Take(1)
            .Concat(proofs.OrderByDescending(p => p.StraightPeakKph).Take(1)).DistinctBy(p => p.Plan.Id).ToArray();
        var forks = selected.Select(p =>
        {
            var source = ReadMotion42(lab, Path.Combine(output, $"speed-{p.Plan.Id}.json.gz"), "exact speed reach");
            int i = Array.FindIndex(source.Flight.Distances, d => d >= exit);
            while (i < source.Flight.Distances.Length && source.Flight.Distances[i] < next &&
                   Speed38(source.Flight.Steps[i].After, g) < 100) i++;
            if (i >= source.Flight.Distances.Length || source.Flight.Distances[i] >= next ||
                Speed38(source.Flight.Steps[i].After, g) < 100)
                throw new InvalidOperationException("The archived ordinary-straight speed crossing diverged.");
            return new Fork45(p.Plan.Id, source, source.Flight.Distances[i], PhaseAt43(lab, g, source, i));
        }).ToArray();

        var proposals = Enumerable.Range(0, 56).Select(i =>
        {
            var source = forks[i % forks.Length];
            double U(int r) => Halton43(i / forks.Length + 1, r);
            int id = 700 + i;
            var route = baseline.Route with { Id = id, EntryOutside = 8.9 + 2.4 * U(2), Inside = 6.1 + 2.55 * U(3),
                ExitOutside = 7.5 + 2.7 * U(5), PreviewMeters = 16.2 + 11.0 * U(7), StraightKph = 94 + 12 * U(11),
                TurnAcceleration = 7.1 + 3.7 * U(13), Damping = 11.0 + 5.0 * U(17),
                RiderBias = .022 + .06 * U(19), EarlyHeading = 4.8 + 4.7 * U(23) };
            var p = baseline with { Id = id, Line = (Line45)(i / forks.Length % 4), Route = route,
                SpeedBand = 100, DriveReserve = .12 + .58 * U(29), BankingReserve = .08 + .17 * U(31),
                RiderReserve = .12 + .78 * U(37), ApexMeters = exit * (.44 + .17 * U(41)),
                ReleaseMeters = exit - 14 + 23 * U(43), EntryCorrection = .30 + 1.7 * U(47),
                TurnPhaseMeters = -6 + 11 * U(53) };
            return new Recovery45(source, p);
        }).ToArray();
        string checkpoint = Path.Combine(output, "speed-recovery-checkpoints");
        Directory.CreateDirectory(checkpoint);
        var results = proposals.AsParallel().WithDegreeOfParallelism(Math.Min(6, Environment.ProcessorCount)).Select(p =>
            Persist44(checkpoint, p.Plan.Id, () =>
            {
                var r = Ride45(lab, g, p.Fork.Motion, p.Fork.Station, p.Plan, finish, exact: false);
                var outcome = Summarize45(lab, g, r, finish, exit);
                Console.WriteLine($"recovery {p.Plan.Id}: {outcome.Station:F2} m {outcome.Stop}");
                return outcome;
            })).OrderBy(x => x.Plan.Id).ToArray();
        var ranks = results.OrderByDescending(x => x.Finished).ThenBy(x => x.ExitToFinishSeconds ?? double.PositiveInfinity)
            .ThenByDescending(x => x.Station).Take(6).ToArray();
        var exact = ranks.Select(s =>
        {
            var p = proposals.Single(x => x.Plan.Id == s.Plan.Id);
            var r = Certify45(lab, g, Ride45(lab, g, p.Fork.Motion, p.Fork.Station, p.Plan, finish, exact: true));
            return (Run: r, Score: Summarize45(lab, g, r, finish, exit));
        }).OrderByDescending(x => x.Score.Finished).ThenBy(x => x.Score.ExitToFinishSeconds ?? double.PositiveInfinity)
            .ThenByDescending(x => x.Score.Station).ToArray();
        SaveRun45(lab, g, output, "speed-recovery-selected", exact[0].Run, exact[0].Score);
        Save(Path.Combine(output, "speed-recovery.json.gz"), new
        {
            Forks = forks.Select(f => new { f.Source, f.Station, f.Phase,
                State = Slice42(f.Motion, f.Station).At.State, Pending = Pending43(f.Motion, Slice42(f.Motion, f.Station).At, false) }),
            Results = results, Exact = exact.Select(x => x.Score), FourLaps = false, Finish = finish
        }, true);
    }
}
