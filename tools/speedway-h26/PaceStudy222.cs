using System.Diagnostics;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Base42 = "7f71af085c9e3bcff1b2a19447cb654f27f13a09";
    private enum Family42 { Archive, FullThenPrepare, Concurrent, Directional, Offline }
    private sealed record Policy42(int Id, Family42 Family, Setup40 Line, double CoastBias, double BendGain,
        double ExitDrive, double BodyBias, double SteeringBias, Limit42 Ablation = Limit42.None, int EntryShape = 0,
        double RollBackup = 0, double DampingAdd = 0);
    private sealed record Result42(Policy42 Policy, Run40 Run, Score40 Score, double ConcurrentSeconds);
    private sealed record Qualification42(Policy42 Policy, Score40 Nominal, Score40[] Holdouts);

    private static void EnvelopeStudy222(string checkout, string output, string mode)
    {
        var lab = MakeLinkLab40(checkout);
        switch (mode)
        {
            case "references": Reproduce42(lab, output); break;
            case "maps": MapEnvelope42(lab, output); break;
            case "race": Search42(lab, output); break;
            case "frontier": Frontier42(lab, output); break;
            case "cycle": Cycle42(lab, output); break;
            case "complete": Qualify42(lab, output); break;
            case "mechanics": Isolate42(lab, output); break;
            default: throw new ArgumentException("Use references, maps, race, frontier, cycle, complete or mechanics.");
        }
    }

    private static Fork40 ReadArchive42(Lab38 lab, string source)
    {
        string path = Path.Combine(lab.Root, "data/experiments/speedway" + source + ".json.gz");
        return ReadMotion42(lab, path, source);
    }

    private static Fork40 ReadMotion42(Lab38 lab, string path, string source)
    {
        var evidence = Read199<Evidence39>(path);
        var seed = new Checkpoint38(evidence.Seed, -PreparationStart37);
        var terminal = evidence.Rows[^1];
        var g = lab.Geometry(true);
        var wave = ReplayTape38(lab, g, seed, evidence.Commands, terminal.State.Time, exact: true);
        var a = wave.Final.State;
        var b = terminal.State;
        if ((a.Center - b.Center).Norm > .002 || (a.Momentum - b.Momentum).Norm > .12 ||
            (a.Angular - b.Angular).Norm > .12 || (a.Pose.V - b.Pose.V).Norm > .0004 ||
            (a.Rider - b.Rider).Norm > .0004 || (a.RiderMomentum - b.RiderMomentum).Norm > .060 ||
            Math.Abs(a.Time - b.Time) > 1e-8 || Math.Abs(a.Crank - b.Crank) > .02 ||
            Math.Abs(a.Wheel - b.Wheel) > .06 || Math.Abs(a.Leg.Momentum - b.Leg.Momentum) > .06 ||
            Math.Abs(a.Leg.Extension - b.Leg.Extension) > .001 || Math.Abs(a.Steer - b.Steer) > .001 ||
            a.Command != b.Command || a.Next != b.Next)
            throw new InvalidOperationException($"Full archived state {source} diverged at {a.Time:R} " +
                $"({wave.Stop}, position error {(a.Center - b.Center).Norm:R}).");
        return new(source, seed, wave with { Stop = evidence.Stop }, evidence.Commands);
    }

    private static Setup40 SetupBaseline42() => new(251, "fast", 40, 5.6, Prep40.Coast, 115, 77);
    private static Policy42 Baseline42() => new(0, Family42.Archive, SetupBaseline42(), 0, 0, 0, 0, 0);

    private static void Reproduce42(Lab38 lab, string output)
    {
        string[] names = ["220/original-straight", "220/straight-full-diagnostic", "220/full-slide-frontier",
            "221/refined/linked-training", "221/refined/linked-speed-frontier", "221/transfer/transfer-nominal",
            "221/transfer/transfer-holdout-1", "221/transfer/transfer-holdout-2", "221/transfer/transfer-holdout-3",
            "221/transfer/transfer-holdout-4", "221/powered/powered-selected"];
        var result = new object[names.Length];
        Parallel.For(0, names.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var f = ReadArchive42(lab, names[i]);
            var r = new Run40(SetupBaseline42(), f.Seed, f.Seed, [], f.Tape, f.Flight, []);
            var past = Read199<Evidence39>(Path.Combine(lab.Root, "data/experiments/speedway" + names[i] + ".json.gz")).Rows[^1];
            result[i] = new { f.Name, Score = MeasureLink40(lab, lab.Geometry(true), r),
                FullStateEquality = f.Flight.Final.State == past.State,
                StateDistance = (f.Flight.Final.State.Center - past.State.Center).Norm,
                After = f.Flight.Final, Pending = f.Tape[f.Flight.Final.State.Next..] };
        });
        var reference = ReadArchive38(lab, "70");
        Save(Path.Combine(output, "references.json"), new { Base = Base42, H36 = reference.Run.Summary,
            Replayed = result, NativeWorlds = 0 });
    }

    private static IEnumerable<Policy42> Policies42()
    {
        int id = 0;
        yield return Baseline42();
        foreach (var method in new[] { Family42.FullThenPrepare, Family42.Concurrent, Family42.Directional, Family42.Offline })
        {
            foreach (double preparation in new[] { 32d, 37, 40, 43 })
            {
                foreach (int option in Enumerable.Range(0, 4))
                {
                    var line = SetupBaseline42() with { Id = ++id, PreparationMeters = preparation,
                        Lane = option < 2 ? 5.6 : 6.35 };
                    double boost = option % 2 == 0 ? .025 : .07;
                    yield return new Policy42(id, method, line, option < 2 ? 1.4 : 2.5,
                        boost, option % 2 == 0 ? .17 : .31, option < 2 ? .013 : .028,
                        method == Family42.Offline ? (option % 2 == 0 ? .007 : -.007) : 0);
                }
            }
        }
    }

    private static (Checkpoint38 At, Step215[] Prefix, double[] Distances, Activation215[] Commands) Slice42(
        Fork40 source, double station)
    {
        int i = Array.FindIndex(source.Flight.Distances, d => d >= station);
        if (i < 0) throw new InvalidOperationException("Matched station was not reached.");
        var at = new Checkpoint38(source.Flight.Steps[i].After, source.Flight.Distances[i]);
        return (at, source.Flight.Steps[..(i + 1)], source.Flight.Distances[..(i + 1)],
            source.Tape.Where(q => q.Requested < at.State.Time - 1e-10).ToArray());
    }

    private static Run40 RunFrom42(Lab38 lab, Geometry215 g, Fork40 source, Policy42 policy)
    {
        double entry = lab.Bend.NextEntry - lab.Bend.Entry;
        var slice = Slice42(source, entry - policy.Line.PreparationMeters);
        return Ride42(lab, g, source.Seed, slice.At, slice.Prefix, slice.Distances, slice.Commands, policy);
    }

    private static void Search42(Lab38 lab, string output)
    {
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var source = ReadArchive42(lab, "220/straight-full-diagnostic");
        var g = lab.Geometry(true);
        double entry = lab.Bend.NextEntry - lab.Bend.Entry;
        var plans = Policies42().ToArray();
        var audit = Enum.GetValues<Limit42>().Where(x => x != Limit42.None).Select((limit, i) =>
            Baseline42() with { Id = 100 + i, Ablation = limit }).ToArray();
        var runs = new object[plans.Length + audit.Length];
        var best = new List<Result42>();
        Result42? frontier = null;
        var all = plans.Concat(audit).ToArray();
        int finished = 0;
        Parallel.For(0, all.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
        {
            var p = all[i];
            var r = RunFrom42(lab, g, source, p);
            var score = MeasureLink40(lab, g, r) with { Id = p.Id };
            var handled = Summarize42(lab, p, r, score);
            runs[i] = new { p, r.Root, r.Fork, r.EarlierCommands, r.Commands, Score = score, handled.ConcurrentSeconds,
                FinalDecision = r.Decisions.LastOrDefault() };
            if (i < plans.Length)
            {
                lock (best)
                {
                    best.Add(handled);
                    best.Sort((a, b) => CompareLink40(a.Score, b.Score));
                    if (best.Count > 7) best.RemoveAt(7);
                    if (score.Linked && (frontier is null || score.StraightPeak > frontier.Score.StraightPeak))
                        frontier = handled;
                }
            }
            int n = Interlocked.Increment(ref finished);
            if (n % 10 == 0) Console.WriteLine($"H41 continuous search {n}/{all.Length}.");
        });
        Save(Path.Combine(output, "candidates.json.gz"), runs, true);
        if (frontier is not null && best.All(x => x.Policy.Id != frontier.Policy.Id)) best.Add(frontier);
        var proof = best.AsParallel().WithDegreeOfParallelism(6).Select(x =>
        {
            var r = ExactLink40(lab, g, x.Run);
            return Summarize42(lab, x.Policy, r, MeasureLink40(lab, g, r) with { Id = x.Policy.Id });
        }).OrderBy(x => x.Score, Comparer<Score40>.Create(CompareLink40)).ToArray();
        var selected = proof[0];
        foreach (var x in new[] { selected, proof.Where(a => a.Score.Linked).OrderByDescending(a => a.Score.StraightPeak)
                     .FirstOrDefault() ?? selected }.DistinctBy(x => x.Policy.Id))
            WriteEvidence39(lab, output, $"race-{x.Policy.Id}", g, x.Run.Root, x.Run.Commands, x.Run.Flight);
        Save(Path.Combine(output, "race-selection.json"), new { Baseline = Baseline42(), Selected = selected.Policy,
            Exact = proof.Select(p => new { p.Policy, p.Score, p.ConcurrentSeconds }), Decisions = selected.Run.Decisions,
            CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds, WallSeconds = timer.Elapsed.TotalSeconds,
            Training = plans.Length, Ablations = audit.Length, NativeWorlds = 0 });
    }

    private static Result42 Summarize42(Lab38 lab, Policy42 policy, Run40 run, Score40 score)
    {
        double entry = lab.Bend.NextEntry - lab.Bend.Entry;
        double concurrent = run.Flight.Steps.Zip(run.Flight.Distances)
            .Where(x => x.Second > entry - policy.Line.PreparationMeters && x.Second < entry &&
                x.First.Before.Command.Bike.Drive > .20f && x.First.After.Pose.Roll < -.05)
            .Sum(x => Duration215(x.First));
        return new(policy, run, score, concurrent);
    }
}
