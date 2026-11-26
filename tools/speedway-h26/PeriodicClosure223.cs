using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record ClosureTrial43(int Id, double StartBeforeSeam, double InitialDefect, double FinalDefect,
        double SeamSeconds, int ValidEvaluations, SearchResult43 Replay);

    private static void ClosePeriodic43(Lab38 lab, string output, bool local = false)
    {
        var g = lab.Geometry(true);
        var archive = Read223(lab, ArchivePath43(lab, "speedway222/cycle/cycle-flying"), "H41 continuous lap");
        string adjusted = Path.Combine(output, "shoot-repair-selected.json.gz");
        var linked = Read223(lab, local && File.Exists(adjusted) ? adjusted : Path.Combine(output, "shoot-selected.json.gz"), "H42 line");
        int count = local ? 6 : 10;
        string file = local ? "periodic-closure-local" : "periodic-closure";
        var shortlisted = Enumerable.Range(0, count).AsParallel().WithDegreeOfParallelism(Parallel43)
            .Select(i => CloseSeam43(lab, g, archive, linked, i, local))
            .OrderBy(x => x.Record.Replay, Comparer<SearchResult43>.Create(PeriodicRank43)).ToArray();
        var best = shortlisted[0];
        var chosen = best.Shooting;
        double lap = Frame.LapMeters;
        bool two = chosen.Result.ManeuverDistance >= 2 * lap + PostMarkerMeters43 && Physical43(chosen.Result);
        // Repeat the accepted cycle's own submitted-command pattern for a third and fourth attempt.
        // Appending its requests is an open-loop witness attempt with no phase reset.
        if (two)
        {
            var m = chosen.Motion;
            double t1 = AtPhase43(lab, g, m, lap).Time, t2 = AtPhase43(lab, g, m, 2 * lap).Time;
            var pattern = m.Tape.Where(x => x.Requested >= t1 - 1e-9 && x.Requested < t2 - 1e-9).ToArray();
            var prior = m.Tape.TakeWhile(x => x.Requested < t2 - 1e-9).ToArray();
            var append = Enumerable.Range(1, 3).SelectMany(k => pattern.Select(a =>
                a with { Requested = a.Requested + k * (t2 - t1), Time = a.Time + k * (t2 - t1) }));
            chosen = Integrate43(lab, g, m, chosen.Result.Plan, [.. prior, .. append],
                prior.Length, 4 * lap + PostMarkerMeters43, true, periodic: true);
        }
        Save(Path.Combine(output, file + ".json.gz"), new
        {
            Trials = shortlisted.Select(x => x.Record), Selected = best.Record, Continuous = chosen.Result,
            TwoWithRecovery = two, FourWithRecovery = two && chosen.Result.ManeuverDistance >= 4 * lap + PostMarkerMeters43 &&
                Physical43(chosen.Result), Gates = LapGates43(lab, g, chosen.Motion), NativeWorlds = 0
        }, true);
        WriteEvidence39(lab, output, file + "-selected", g, chosen.Motion.Seed, chosen.Motion.Tape, chosen.Motion.Flight);
        Console.WriteLine($"Study 223 periodic closure: {shortlisted.Length} fits, selected continuous station " +
            $"{chosen.Result.ManeuverDistance:F3} m, seam error {chosen.Result.PhaseError:F3}, two {two}.");
    }

    private static (ClosureTrial43 Record, Shooting43 Shooting) CloseSeam43(Lab38 lab, Geometry215 g, Fork40 source,
        Fork40 repeat, int i, bool local)
    {
        double lap = Frame.LapMeters;
        double lead = (local ? new[] { 47d, 41, 36, 32, 27, 23 }
            : new[] { 48d, 45, 42, 37, 35, 31, 29, 28, 26, 24 })[i];
        double matchPhase = i is 2 or 5 or 8 ? 3 : 0;
        double difference = local ? .018 : .12;
        double correctionLimit = local ? .25 : 1.7;
        double[] lineSearch = local ? [1, .50, .20, .07, .025] : [.9, .45, .20];
        var target = AtPhase43(lab, g, repeat, matchPhase);
        var trigger = AtPhase43(lab, g, source, lap + matchPhase);
        var cut = Slice42(source, lap - lead - 3);
        var widths = new[] { .28 * lead, .28 * lead, .30 * lead, .26 * lead, .22 * lead };
        var shifts = new[] { .78 * lead, .58 * lead, .38 * lead, .23 * lead, .10 * lead };
        const int dimensions = 12;
        double[] factors = new double[dimensions];
        int accepted = 0;
        long start = Stopwatch.GetTimestamp();
        var best = Error(factors);
        double baseline = best is null ? -1 : Norm(best);
        double current = baseline;
        for (int iteration = 0; iteration < 6 && best is not null; iteration++)
        {
            var sensitivity = new double[dimensions][];
            for (int j = 0; j < dimensions; j++)
            {
                var delta = (double[])factors.Clone();
                delta[j] += difference;
                var probe = Error(delta);
                if (probe is not null)
                {
                    sensitivity[j] = probe.Zip(best, (x, y) => (x - y) / difference).ToArray();
                    continue;
                }
                delta[j] -= 2 * difference;
                var negative = Error(delta);
                sensitivity[j] = negative is null ? new double[best.Length] :
                    best.Zip(negative, (x, y) => (x - y) / difference).ToArray();
            }
            var update = LeastSquares43(sensitivity, best);
            bool progress = false;
            foreach (double damping in lineSearch)
            {
                var proposal = factors.Zip(update, (a, b) => Math.Clamp(a + damping * Math.Clamp(b, -correctionLimit, correctionLimit), -3, 3)).ToArray();
                var now = Error(proposal);
                if (now is null || Norm(now) >= current - .001) continue;
                current = Norm(now);
                factors = proposal;
                best = now;
                progress = true;
                break;
            }
            if (!progress) break;
        }
        var patches = Patch(factors);
        double spliceTime = trigger.Time + .11 * factors[11];
        var altered = Commands43(source, patches).Tape;
        var before = altered.TakeWhile(a => a.Requested < spliceTime - 1e-9).ToArray();
        string label = $"cyclic seam fit {i}";
        var plan = new Proposal43((local ? 820 : 800) + i, label, null, patches);
        var tape = Complete(before, spliceTime);
        int fork = 0;
        while (fork < before.Length && before[fork] == source.Tape[fork]) fork++;
        var trial = Integrate43(lab, g, source, plan, tape, fork, 2 * lap + PostMarkerMeters43, true, periodic: true);
        Console.WriteLine($"Study 223 closure {i} {baseline:F3} Ă˘â€ â€™ {current:F3}, {trial.Result.ManeuverDistance:F2} m, " +
            $"{Stopwatch.GetElapsedTime(start).TotalSeconds:F2} wall s.");
        return (new(plan.Id, lead, baseline, current, spliceTime, accepted, trial.Result), trial);

        Knot43[] Patch(double[] x) =>
            [new(lap - shifts[0], widths[0], .29 * x[5], .026 * x[0], new(.038 * x[8], 0, 0), 0),
             new(lap - shifts[1], widths[1], .29 * x[6], .026 * x[1], new(0, .043 * x[9], 0), 0),
             new(lap - shifts[2], widths[2], .15 * x[7], .026 * x[2], new(0, 0, .05 * x[10]), 0),
             new(lap - shifts[3], widths[3], 0, .026 * x[3], default, 0),
             new(lap - shifts[4], widths[4], 0, .026 * x[4], default, 0)];

        double[]? Error(double[] u)
        {
            var queue = Commands43(source, Patch(u)).Tape;
            double time = trigger.Time + .11 * u[11];
            var segment = ReplayTape38(lab, g, cut.At, queue, time);
            if (segment.Stop != "verified interval" || segment.Steps.Length == 0) return null;
            accepted++;
            var frame = new Fork40(source.Name, cut.At, segment, queue);
            var a = PhaseAt43(lab, g, frame, segment.Steps.Length - 1);
            return [.40 * (a.Station - lap - matchPhase), 1.4 * (a.Offset - target.Offset),
                .46 * (a.RoadSpeed - target.RoadSpeed), .47 * (a.SideSpeed - target.SideSpeed),
                5 * Difference38(a.Direction, target.Direction), 4 * Difference38(a.FrameYaw, target.FrameYaw),
                7 * (a.Roll - target.Roll), 3.5 * (a.Pitch - target.Pitch), .8 * (a.BodyRate.X - target.BodyRate.X),
                .4 * (a.BodyRate.Z - target.BodyRate.Z), .07 * (a.Crank - target.Crank), .03 * (a.RearWheel - target.RearWheel),
                1.1 * (a.Steer - target.Steer), 1.7 * (a.Rider - target.Rider).Norm,
                .015 * (a.RiderMomentum - target.RiderMomentum).Norm, .20 * (a.Active.Bike.Drive - target.Active.Bike.Drive)];
        }

        Activation215[] Complete(Activation215[] earlier, double begin)
        {
            double exit = Map199.At(2, 1).Exit - lab.Bend.Entry;
            double linkEnd = AtPhase43(lab, g, repeat, exit + 7).Time;
            double oldStart = AtPhase43(lab, g, source, exit + 7).Time;
            double oldEnd = AtPhase43(lab, g, source, lap).Time;
            double third = AtPhase43(lab, g, repeat, 0).Time;
            var events = earlier.ToList();
            Append(repeat, target.Time, linkEnd);
            Append(source, oldStart, oldEnd);
            Append(repeat, third, AtPhase43(lab, g, repeat, PostMarkerMeters43 + 10).Time);
            return events.ToArray();

            void Append(Fork40 refMotion, double from, double to)
            {
                foreach (var event43 in refMotion.Tape.Where(a => a.Requested >= from - 1e-9 && a.Requested < to - 1e-9))
                {
                    double when = begin + Math.Max(0, event43.Requested - from);
                    if (events.Count != 0) when = Math.Max(when, events[^1].Time - ReactionSeconds216 + 1e-6);
                    events.Add(new(when, when + ReactionSeconds216, event43.Command));
                }
                begin += to - from;
            }
        }

        static double Norm(double[] v) => Math.Sqrt(v.Sum(Sq43));
    }
}
