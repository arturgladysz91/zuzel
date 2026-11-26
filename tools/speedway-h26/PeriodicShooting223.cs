using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double PostMarkerMeters43 = 22;

    private static void Periodic43(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        var old = Read223(lab, ArchivePath43(lab, "speedway222/cycle/cycle-flying"), "H41 policy 301");
        var selection = Path.Combine(output, "shoot-selected.json.gz");
        var chosen = File.Exists(selection) ? Read223(lab, selection, "H42 linked") :
            Read223(lab, ArchivePath43(lab, "speedway222/qualification/nominal"), "H41 policy 58");
        var clock = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        int passes = 2;
        double lap = Frame.LapMeters;
        double end = passes * lap + PostMarkerMeters43;
        var plans = Enumerable.Range(0, Proposals43).Select(i => CyclePlan43(old.Name, lap, i)).ToArray();
        var screening = plans.AsParallel().WithDegreeOfParallelism(Parallel43).Select(x =>
        {
            var (command, split) = PeriodicTape43(lab, g, old, chosen, x, passes);
            return Integrate43(lab, g, old, x, command, split, end, false, periodic: true).Result;
        }).ToArray();
        var noms = screening.OrderBy(x => x, Comparer<SearchResult43>.Create(PeriodicRank43)).Take(6)
            .Select(p =>
            {
                var (t, split) = PeriodicTape43(lab, g, old, chosen, p.Plan, passes);
                return Integrate43(lab, g, old, p.Plan, t, split, end, true, periodic: true);
            }).ToArray();
        var best = noms.OrderBy(x => x.Result, Comparer<SearchResult43>.Create(PeriodicRank43)).First();
        var pair = best.Result.ManeuverDistance >= end && Physical43(best.Result);
        Shooting43? four = null;
        if (pair)
        {
            var (t, split) = PeriodicTape43(lab, g, old, chosen, best.Result.Plan, 4);
            four = Integrate43(lab, g, old, best.Result.Plan, t, split, 4 * lap + PostMarkerMeters43, true, periodic: true);
        }
        Save(Path.Combine(output, "periodic-search.json.gz"), new
        {
            Screening = screening.OrderBy(r => r.Plan.Id), Exact = noms.Select(x => x.Result), Winner = best.Result, Four = four?.Result,
            TwoWithRecovery = pair, LapMeters = lap, PostMarkerMeters43, CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
            WallSeconds = clock.Elapsed.TotalSeconds, NativeWorlds = 0
        }, true);
        var verified = four ?? best;
        Save(Path.Combine(output, "periodic-summary.json"), new
        {
            verified.Result, Gates = LapGates43(lab, g, verified.Motion), TwoWithRecovery = pair,
            FourWithRecovery = four is not null && four.Result.ManeuverDistance >= 4 * lap + PostMarkerMeters43 && Physical43(four.Result),
            NativeWorlds = 0
        });
        WriteEvidence39(lab, output, "periodic-selected", g, verified.Motion.Seed, verified.Motion.Tape, verified.Motion.Flight);
        Console.WriteLine($"Study 223 periodic: {screening.Length} proposals, exact station {verified.Result.ManeuverDistance:F3} m, two laps {pair}.");
    }

    private static int PeriodicRank43(SearchResult43 a, SearchResult43 b)
    {
        int count(SearchResult43 x) => (int)Math.Floor((x.ManeuverDistance - PostMarkerMeters43) / Frame.LapMeters);
        int laps = count(b).CompareTo(count(a));
        if (laps != 0) return laps;
        double utility(SearchResult43 x) => x.ManeuverDistance >= Frame.LapMeters
            ? x.ManeuverDistance - .35 * x.PhaseError : x.ManeuverDistance;
        int value = utility(b).CompareTo(utility(a));
        return value != 0 ? value : a.Plan.Id.CompareTo(b.Plan.Id);
    }

    private static Proposal43 CyclePlan43(string source, double lap, int i)
    {
        double splice = lap + new[] { 3d, 8, 13, 18, 21, 24, 26, 28 }[i / 8];
        int mode = i % 8;
        double amount(int b) => 2 * Halton43(i + 1, b) - 1;
        double steer = mode == 0 ? 0 : .022 * amount(3);
        double gas = mode < 2 ? 0 : .15 * amount(2);
        return new(500 + i, source, null,
            [new(splice, 12, gas, steer, new(.024 * amount(5), .024 * amount(7), .022 * amount(11)), .15 * amount(13)),
             new(lap + 38, 16, .13 * amount(19), .024 * amount(17),
                 new(.015 * amount(19), .028 * amount(23), -.025 * amount(29)), .20 * amount(31))],
            new[] { .95, 1, 1.035, 1.065, 1.10, 1.145, .92, 1.18 }[mode]);
    }

    private static (Activation215[] Commands, int First) PeriodicTape43(Lab38 lab, Geometry215 g, Fork40 cycle, Fork40 linked,
        Proposal43 plan, int laps)
    {
        double lap = Frame.LapMeters;
        double at = plan.Knots[0].Station;
        // Requests before the splice are irrevocable. Only new commands get a new timestamp.
        double t = AtPhase43(lab, g, cycle, at).Time;
        var queue = cycle.Tape.TakeWhile(x => x.Requested < t - 1e-9).ToList();
        int first = queue.Count;
        if (first > 0) t = Math.Max(t, queue[^1].Time - ReactionSeconds216 + 1e-6);
        int i = Index43(linked.Flight.Distances, at - lap);
        double sourceTime = linked.Flight.Steps[i].After.Time;
        double exit = Map199.At(2, 1).Exit - lab.Bend.Entry;
        var finish = AtPhase43(lab, g, linked, exit + 7);
        var tail = AtPhase43(lab, g, cycle, exit + 7);
        var seam = AtPhase43(lab, g, cycle, lap);
        var entry = AtPhase43(lab, g, linked, 0);
        double iteration = t;
        Add(linked, sourceTime, finish.Time, lap, plan.CycleScale, true);
        Add(cycle, tail.Time, seam.Time, lap, 1, false);
        for (int n = 2; n <= laps; n++)
        {
            Add(linked, entry.Time, finish.Time, n * lap, 1, true);
            Add(cycle, tail.Time, seam.Time, n * lap, 1, false);
        }
        return (queue.ToArray(), first);

        void Add(Fork40 source, double from, double to, double lapOffset, double scale, bool tune)
        {
            foreach (var request in source.Tape.Where(x => x.Requested >= from - 1e-9 && x.Requested < to - 1e-9))
            {
                double submit = iteration + Math.Max(0, request.Requested - from) * scale;
                if (queue.Count > 0) submit = Math.Max(submit, queue[^1].Time - ReactionSeconds216 + 1e-6);
                var phase = source.Flight.Distances[CommandState43(source, request.Requested)];
                var command = tune ? KnotDrive43(request.Command, plan.Knots, phase + lapOffset) : request.Command;
                queue.Add(new(submit, submit + ReactionSeconds216, command));
            }
            iteration += (to - from) * scale;
        }
    }

    private static object[] LapGates43(Lab38 lab, Geometry215 g, Fork40 motion)
    {
        var gates = Enumerable.Range(0, 5).Select(n => n * Frame.LapMeters)
            .SelectMany(x => x == 0 ? new[] { x } : new[] { x, x + PostMarkerMeters43 });
        return gates.Select(x =>
        {
            var state = x <= motion.Flight.Final.Distance ? AtPhase43(lab, g, motion, x) : null;
            return (object)new { Station = x, State = state };
        }).ToArray();
    }
}
