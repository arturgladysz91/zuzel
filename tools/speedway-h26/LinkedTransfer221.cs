namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void TransferLink40(Lab38 lab, string output)
    {
        var book = Reconstruct39(lab);
        var g = lab.Geometry(true);
        var archived = book.Attempts.Single(x => x.Plan.Id == 24);
        var p24 = ReadPlain214<RecourseArchive39>(Path.Combine(lab.Root, "data/experiments/speedway219/recourse.json"))
            .Trials.Single(x => x.Settings.Id == 24).Settings;
        var carrier = Read199<StraightArchive39>(Prior40(lab, "straight-experiment")).Comparisons
            .Single(x => x.Plan.Id == 12).Plan;
        var file = Path.Combine(output, "transfer-training-set.json");
        var options = File.Exists(file) ? ReadPlain214<Setup40[]>(file) :
            new[] { new Setup40(27, "fast", 40, 6.6, Prep40.Coast, 115, 74),
                new Setup40(301, "fast", 39, 6.4, Prep40.Coast, 115, 74),
                new Setup40(302, "fast", 40, 6.6, Prep40.Coast, 115, 71) };
        var eq = new HeldSeed38("adaptive nominal", 0, 0, 0);
        var training = options.AsParallel().WithDegreeOfParallelism(Math.Min(5, options.Length)).Select(s =>
        {
            var run = ExactLink40(lab, g, RunAdaptiveLink40(lab, g, book, archived, p24, eq, s, carrier));
            return (Setup: s, Run: run, Score: MeasureLink40(lab, g, run));
        }).OrderBy(x => x.Score, Comparer<Score40>.Create(CompareLink40)).ToArray();

        var best = training[0];
        HeldSeed38[] holds = [new("inside slower", -.25, -.059, .004), new("outside faster", .28, .063, .006),
            new("outside slower", -.21, .091, .008), new("inside faster", .24, -.089, .010)];
        Save(Path.Combine(output, "transfer-freeze.json"), new { best.Setup, Carrier = carrier, Holdouts = holds,
            Training = training.Select(t => new { t.Setup, t.Score }),
            Scheduler = "H38 closed-loop entry to +8 m, H39 acceleration to linked fork, H40 closed-loop preparation and second bend" });
        WriteEvidence39(lab, output, "transfer-nominal", g, best.Run.Root, best.Run.Commands, best.Run.Flight);
        var scores = new Score40[4];
        Parallel.For(0, holds.Length, i =>
        {
            var r = ExactLink40(lab, g, RunAdaptiveLink40(lab, g, book, archived, p24, holds[i], best.Setup, carrier));
            scores[i] = MeasureLink40(lab, g, r);
            WriteEvidence39(lab, output, $"transfer-holdout-{i + 1}", g, r.Root, r.Commands, r.Flight);
        });
        var geometry = new[] { ("compact", Compact38), ("nominal", H36Footprint), ("large", Large38) }
            .AsParallel().Select(item =>
            {
                var adapted = lab.Geometry(true, item.Item2);
                var exact = item.Item1 == "nominal" ? best.Run : ExactLink40(lab, adapted, best.Run, item.Item2);
                return new { Name = item.Item1, Score = MeasureLink40(lab, adapted, exact) };
            }).OrderBy(x => x.Name).ToArray();
        Save(Path.Combine(output, "transfer-qualification.json"), new { best.Setup, Nominal = best.Score,
            Holdouts = scores, Geometry = geometry, NativeWorlds = 0 });
    }
}
