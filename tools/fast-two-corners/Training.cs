using System.Text.Json;
using LearnedCorner166;
using SharedWorldSpeedway;
using Scenario = LearnedCorner166.Scenario;

namespace FastTwoCorners167;

internal sealed record Candidate(int Seed, int Generation, Tuning Tuning, Result[] Results)
{
    public int Completions => Results.Count(x => x.Pass);
    public double? MeanSeconds => Completions == 0 ? null : Results.Where(x => x.Pass).Average(x => x.Goal!.Seconds);
    public double WorstProgress => Results.Min(x => x.EndReference);
    public double MinimumSwept => Results.Min(x => x.MinimumSwept ?? 0);
    public double TerminalOutward => Results.Max(x => Math.Abs(x.AtGoal?.OutwardMetersPerSecond ?? 0));
    public long PredictionWork => Results.Sum(x => x.ForecastContactQueries);
}

internal sealed partial class Study
{
    private static readonly int[] Seeds = [167043, 167099];
    private static readonly Scenario[] TrainingStarts =
    [
        new("T1", "A", -.30, 1.55f),
        new("T2", "B", -.55, 3.65f),
        new("T3", "C", -.8, 5.70f),
        new("T4", "D", -.20, 8.75f)
    ];
    private static readonly Scenario[] SelectionStarts =
    [
        new("S1", "A", -.70, 1.40f),
        new("S2", "D", -1.85, 8.85f)
    ];
    private const int Finalists = 4;
    private static readonly int Generations = (Budget.InitialEpisodeLimit - Finalists * SelectionStarts.Length)
        / (Seeds.Length * TrainingStarts.Length);

    private void TrainAndValidate()
    {
        if (held.Length != Budget.ValidationLimit ||
            TrainingStarts.Concat(SelectionStarts).Any(x => held.Any(h => SameStart(x, h))) ||
            TrainingStarts.Any(x => SelectionStarts.Any(s => SameStart(x, s))))
            throw new InvalidOperationException("Training, selection and validation starts must be disjoint.");

        Save(PathTo("protocol.json"), new { Budget.Plan, Seeds, Generations, TrainingStarts, SelectionStarts,
            ValidationStarts = held, ComparisonIds = new[] { "H1", "H2", "H5", "H8" },
            Screening = "All episodes require both full bends, 20 m and autonomous reserve. Perturb T2/T4 once via the real queue." });
        Save(PathTo("geometry.json"), ReplayGeometry.Create(frame));
        var candidates = new List<Candidate>();
        var random = Seeds.Select(x => new Random(x)).ToArray();
        for (int generation = 0; generation < Generations; generation++)
        for (int stream = 0; stream < Seeds.Length; stream++)
        {
            if (budget.Used + 15 >= Budget.TrainingLimit) break;
            int seed = Seeds[stream];
            var leaders = Rank(candidates.Where(x => x.Seed == seed)).Take(4).ToArray();
            var rng = random[stream];
            var tuning = generation == 0
                ? new Tuning(1.07, .58, 1.11, .60, 18, 1.75, .97, .75, .2 + stream * .7)
                : Mutate(leaders[rng.Next(leaders.Length)].Tuning, rng, generation);
            var rows = TrainingStarts.Select((scenario, index) =>
                Execute($"T-{seed}-{generation:00}-{scenario.Id}", "training", scenario,
                    Budget.TrainingLimit, archive: false, tuning, perturb: index % 2 == 1)).ToArray();
            var candidate = new Candidate(seed, generation, tuning, rows);
            candidates.Add(candidate);
            budget.Log(new { Event = "generation", candidate.Seed, candidate.Generation, candidate.Tuning,
                candidate.Completions, candidate.MeanSeconds, candidate.WorstProgress, candidate.MinimumSwept });
        }

        var finalists = Seeds.SelectMany(seed => Rank(candidates.Where(x => x.Seed == seed)).Take(Finalists / Seeds.Length)).ToArray();
        var selection = new List<Candidate>();
        foreach (var candidate in finalists)
        {
            if (budget.Used + 10 >= Budget.TrainingLimit) break;
            var rows = SelectionStarts.Select(scenario =>
                Execute($"S-{candidate.Seed}-{candidate.Generation:00}-{scenario.Id}", "selection", scenario,
                    Budget.TrainingLimit, archive: false, candidate.Tuning)).ToArray();
            selection.Add(candidate with { Results = rows });
        }
        var selected = Rank(selection).FirstOrDefault() ??
            throw new InvalidOperationException("No candidate evaluated on selection starts.");
        var sources = Directory.EnumerateFiles("tools/fast-two-corners", "*.cs").Order()
            .ToDictionary(path => Path.GetFileName(path), Hash);
        Save(PathTo("freeze.json"), new { Selected = selected, Finalists = selection, Sources = sources,
            HeldSha256 = Hash($"{Historical}/held.json"), BuildSha256 = Hash(typeof(Study).Assembly.Location),
            CpuAtFreeze = budget.Used, TrainedEpisodes = candidates.Sum(x => x.Results.Length),
            SelectionEpisodes = selection.Sum(x => x.Results.Length) });

        foreach (var scenario in held)
            Execute("167-" + scenario.Id, "validation", scenario, Budget.FinalAdmission, archive: true, selected.Tuning);
        foreach (var id in new[] { "H1", "H2", "H5", "H8" })
            Execute("166-" + id, "comparison", held.Single(s => s.Id == id), Budget.FinalAdmission, archive: true);
        ArchiveReferences();
    }

    private void ArchiveReferences()
    {
        var rows = new List<object>();
        foreach (var start in held)
        {
            if (start.Reference161 is not { } label)
            {
                rows.Add(new { start.Id, Available = false, Reason = "No matched full saved #161 history" });
                continue;
            }
            string path = $"docs/experiments/trajectory-continuation/native/{label}.json.gz";
            using var file = File.OpenRead(path);
            using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress);
            var run = JsonSerializer.Deserialize<Run144>(gzip, Json)!;
            if (JsonSerializer.Serialize(run.Riders.Single().Spec, Json) != JsonSerializer.Serialize(start.Spec, Json))
                throw new InvalidOperationException("Unmatched saved #161 start " + label);
            // These 13/9 histories extend far beyond the evaluation interval.
            // Retain just the measured sections and bind them to the original archive.
            var rider = run.Riders.Single();
            int end = Array.FindIndex(rider.Samples, q => Runner.Ref(q) >= course.Goal);
            if (end < 0)
            {
                rows.Add(new { start.Id, Available = false, Reason = "Archive lacks full section", Source = path });
                continue;
            }
            var cutoff = rider.Samples[end].Input.Time + Course.Reserve;
            var samples = rider.Samples.TakeWhile(q => q.Input.Time < cutoff).ToList();
            samples.Add(rider.Samples[samples.Count]);
            var slice = run with { Name = "161-" + start.Id, Outcome = "experiment timeout",
                Riders = [rider with { Samples = samples.ToArray(),
                    Decisions = rider.Decisions.Where(d => d.Choice.Time <= samples[^1].Input.Time).ToArray() }],
                Contacts = run.Contacts.Where(x => x.TimeSeconds <= samples[^1].Input.Time).ToArray() };
            rows.Add(new { start.Id, Available = true, Source = path, Sha256 = Hash(path),
                Result = course.Inspect(slice, start, true, 0, 0) });
        }
        Save(PathTo("reference161.json"), rows);
    }

    private static bool SameStart(Scenario a, Scenario b) =>
        a.Field == b.Field && a.Start == b.Start && a.Offset == b.Offset;

    private static IOrderedEnumerable<Candidate> Rank(IEnumerable<Candidate> candidates) => candidates
        .OrderByDescending(x => x.Completions)
        .ThenBy(x => x.MeanSeconds ?? double.PositiveInfinity)
        .ThenByDescending(x => x.WorstProgress)
        .ThenByDescending(x => x.MinimumSwept)
        .ThenBy(x => x.TerminalOutward)
        .ThenBy(x => x.PredictionWork);

    private static Tuning Mutate(Tuning previous, Random random, int generation)
    {
        double spread = generation < 12 ? 1 : .55;
        double J(double center, double radius, double low, double high) =>
            random.NextDouble() < .22 ? center :
                Math.Clamp(center + (2 * random.NextDouble() - 1) * radius * spread, low, high);
        return new(J(previous.FirstSpeedGain, .105, 1.00, 1.30),
            J(previous.FirstMarginMeters, .42, .20, 1.60),
            J(previous.BendSpeedScale, .15, 1.00, 1.38),
            J(previous.BendMarginMeters, .48, .23, 1.75),
            J(previous.PrepareMeters, 7, 9, 32),
            J(previous.CoastMetersPerSecondSquared, 1.05, .75, 4),
            J(previous.LateralBudgetShare, .12, .78, 1.16),
            J(previous.Feedback, .23, .43, 1.23),
            J(previous.EntryWidthMeters, .75, 0, 2.5));
    }
}
