using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record RecoveryPatch43(int Id, string Phase, Knot43 Requested, double RecoveryStation,
        double TimeShift, Knot43[] Correction, double InitialDefect, double FinalDefect, int ValidRollouts,
        SearchResult43 Exact);

    private static void RepairShooting43(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        string file = Path.Combine(output, "shoot-selected.json.gz");
        var source = Read223(lab, file, "best initial H42 trajectory");
        var old = Read223(lab, ArchivePath43(lab, "speedway222/frontier/speed-frontier"), "95.63 km/h witness");
        double exit = lab.Bend.Length;
        double end = Map199.At(2, 1).Exit - lab.Bend.Entry + 22;
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var focus = new (string Label, Fork40 Parent, double Station, double Width, double Drive, bool Absolute, double Gain)[]
        {
            ("slide initiation", source, 19, 4, .23, false, 1),
            ("established slide", source, 52, 4, .33, false, 1),
            ("established full pulse", source, 55, 4, 1, true, .7),
            ("late slide", source, 82, 5, .25, false, 1),
            ("exit drive", source, exit + 2, 5, .16, false, 1),
            ("exit full pulse", source, exit + 7, 6, 1, true, 1),
            ("straight before preparation", old, exit + 19, 7, .17, false, .8),
            ("straight early full pulse", old, exit + 22, 6, 1, true, .8),
            ("straight concurrent acceleration", old, exit + 26, 7, .26, false, .9),
            ("preparation leading countersteer", old, exit + 32, 6, .19, false, 1),
            ("late acceleration and yaw", old, exit + 37, 6, .35, false, .65),
            ("second-turn exit gain", source, end - 27, 5, .15, false, .75)
        };
        var tested = focus.Select((v, i) => (v, i)).AsParallel().WithDegreeOfParallelism(Parallel43).Select(x =>
        {
            var (label, parent, s, w, drive, absolute, gain) = x.v;
            var desired = new Knot43(s, w, drive, 0, default, 0, absolute, absolute ? .45 : 0);
            double recover = Math.Min(end - 1, s + w + 20 * gain);
            return RepairPatch43(lab, g, parent, 700 + x.i, label, desired, recover, end);
        }).OrderBy(x => x.Record.Id).ToArray();
        var ordered = tested.OrderBy(x => x.Record.Exact, Comparer<SearchResult43>.Create(Rank43)).ToArray();
        var best = ordered[0];
        var baseline = Score43(lab, g, source);
        bool improved = best.Record.Exact.Score.Linked && Physical43(best.Record.Exact) &&
            best.Record.Exact.Score.TotalSeconds < baseline.TotalSeconds - .0001;
        if (improved)
            WriteEvidence39(lab, output, "shoot-repair-selected", g, best.Motion.Seed, best.Motion.Tape, best.Motion.Flight);
        Save(Path.Combine(output, "shoot-repair.json.gz"), new
        {
            Baseline = baseline, Improved = improved, Selected = best.Record,
            Tests = tested.Select(x => x.Record), CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
            WallSeconds = timer.Elapsed.TotalSeconds, NativeWorlds = 0
        }, true);
        Console.WriteLine($"Study 223 targeted repair: {tested.Count(x => x.Record.Exact.Score.Linked)}/{tested.Length} linked, " +
            $"promoted {improved}, best {best.Record.Exact.Score.TotalSeconds:F3} s.");
    }

    private static (RecoveryPatch43 Record, Fork40 Motion) RepairPatch43(Lab38 lab, Geometry215 g, Fork40 parent,
        int id, string label, Knot43 requested, double recoveryStation, double end)
    {
        var plan = new Proposal43(id, parent.Name, null, [requested]);
        var (tape, first) = Commands43(parent, plan.Knots);
        int row = Index43(parent.Flight.Distances, requested.Station - requested.Radius - 3);
        var seed = row < 0 ? parent.Seed : new(parent.Flight.Steps[row].After, parent.Flight.Distances[row]);
        int ri = Index43(parent.Flight.Distances, recoveryStation);
        var expected = new Checkpoint38(parent.Flight.Steps[ri].After, parent.Flight.Distances[ri]);
        var startS = requested.Station + .45 * requested.Radius;
        double endS = recoveryStation - 2;
        double width = (endS - startS) * .46;
        double firstS = startS + .24 * (endS - startS);
        double secondS = startS + .67 * (endS - startS);
        double latest = recoveryStation - .4;
        const int dim = 10;
        var scales = new[] { .014, .014, .019, .15, .12, .02, .022, .017, .13, .010 };
        double[] coeff = new double[dim];
        int valid = 0;
        double initial = double.PositiveInfinity;
        double last = double.PositiveInfinity;
        Activation215[] selected = tape;
        var initialTrial = Propagate(coeff, out _);
        if (initialTrial is null)
        {
            // Reduce just the requested pulse when it leaves no physically valid interval for fitting.
            requested = requested with { Drive = requested.AbsoluteDrive ? .36 : requested.Drive * .38, AbsoluteDrive = false };
            plan = plan with { Knots = [requested] };
        }

        for (int iteration = 0; iteration < 4; iteration++)
        {
            var zero = Propagate(coeff, out selected);
            if (zero is null) break;
            double error = Norm(zero);
            if (iteration == 0) initial = error;
            last = error;
            var columns = new double[dim][];
            for (int j = 0; j < dim; j++)
            {
                var delta = (double[])coeff.Clone();
                delta[j] += .15;
                var next = Propagate(delta, out _);
                if (next is not null)
                {
                    columns[j] = next.Zip(zero, (a, b) => (a - b) / .15).ToArray();
                    continue;
                }
                delta[j] -= .30;
                var previous = Propagate(delta, out _);
                columns[j] = previous is null ? new double[zero.Length] : zero.Zip(previous, (a, b) => (a - b) / .15).ToArray();
            }
            var step = LeastSquares43(columns, zero);
            bool accept = false;
            foreach (double sample in new[] { 1d, .5, .25 })
            {
                var guess = coeff.Zip(step, (a, d) => Math.Clamp(a + Math.Clamp(d, -1.6, 1.6) * sample, -2.25, 2.25)).ToArray();
                var trial = Propagate(guess, out var commands);
                if (trial is null || Norm(trial) >= error - .0001) continue;
                coeff = guess;
                selected = commands;
                last = Norm(trial);
                accept = true;
                break;
            }
            if (!accept) break;
        }
        var correction = Corrections(coeff);
        plan = plan with { Knots = [requested, .. correction] };
        var (patched, patchFirst) = Commands43(parent, plan.Knots);
        if (double.IsFinite(last)) patched = selected;
        var exact = Integrate43(lab, g, parent, plan, patched, patchFirst, end, exact: true);
        return (new(id, label, requested, recoveryStation, coeff[9] * scales[9], correction,
            double.IsFinite(initial) ? initial : -1, double.IsFinite(last) ? last : -1, valid, exact.Result), exact.Motion);

        Knot43[] Corrections(double[] v) =>
            [new(firstS, width, v[3] * scales[3], v[0] * scales[0], default, 0),
             new(secondS, width, v[4] * scales[4], v[1] * scales[1],
                new(v[5] * scales[5], v[6] * scales[6], v[7] * scales[7]), v[8] * scales[8]),
             new(latest - 4, 6, 0, v[2] * scales[2], default, 0)];

        double[]? Propagate(double[] v, out Activation215[] commands)
        {
            var all = new[] { requested }.Concat(Corrections(v)).ToArray();
            (commands, _) = Commands43(parent, all);
            double shift = v[9] * scales[9];
            if (shift != 0)
            {
                for (int j = first; j < commands.Length; j++)
                {
                    var c = commands[j];
                    double s = parent.Flight.Distances[CommandState43(parent, c.Requested)];
                    double elapsed = shift * Smooth37((s - firstS) / Math.Max(1, recoveryStation - firstS));
                    commands[j] = c with { Requested = c.Requested + elapsed, Time = c.Time + elapsed };
                }
            }
            double stopTime = expected.State.Time + shift;
            var local = ReplayTape38(lab, g, seed, commands, stopTime);
            if (local.Stop != "verified interval") return null;
            valid++;
            var a = local.Final.State;
            var b = expected.State;
            var motionA = g.Motion(a);
            var motionB = g.Motion(b);
            var qa = lab.Course.Mark(a, g);
            var qb = lab.Course.Mark(b, g);
            var dr = a.Rider - b.Rider;
            var rr = motionA.RiderRate - motionB.RiderRate;
            var dv = (a.Momentum - b.Momentum) / g.Base.Mass;
            double angular = Difference38(a.Pose.Yaw, b.Pose.Yaw);
            double angle = Difference38(Heading38(a), Heading38(b));
            return [.35 * (local.Final.Distance - expected.Distance), 1.5 * (qa.Road.OffsetMeters - qb.Road.OffsetMeters),
                .13 * dv.X, .13 * dv.Y, .20 * dv.Z, 4 * angular, 6 * angle, 8 * (a.Pose.Roll - b.Pose.Roll),
                4 * (a.Pose.Pitch - b.Pose.Pitch), .80 * (motionA.BodyRate.X - motionB.BodyRate.X),
                .55 * (motionA.BodyRate.Z - motionB.BodyRate.Z), 4 * dr.X, 5 * dr.Y, 3 * dr.Z,
                .65 * rr.X, .65 * rr.Y, .55 * rr.Z, .04 * (a.Crank - b.Crank),
                .075 * (a.Wheel - b.Wheel), 1.4 * (a.Steer - b.Steer), .9 * (a.Leg.Extension - b.Leg.Extension)];
        }

        static double Norm(double[] v) => Math.Sqrt(v.Sum(Sq43));
    }

    private static double[] LeastSquares43(double[][] columns, double[] defect)
    {
        int size = columns.Length;
        var matrix = new double[size, size + 1];
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
                matrix[i, j] = Dot(columns[i], columns[j]) + (i == j ? .0015 : 0);
            matrix[i, size] = -Dot(columns[i], defect);
        }
        for (int pivot = 0; pivot < size; pivot++)
        {
            int top = Enumerable.Range(pivot, size - pivot).MaxBy(i => Math.Abs(matrix[i, pivot]));
            for (int j = pivot; j <= size; j++) (matrix[pivot, j], matrix[top, j]) = (matrix[top, j], matrix[pivot, j]);
            double divisor = matrix[pivot, pivot];
            for (int j = pivot; j <= size; j++) matrix[pivot, j] /= divisor;
            for (int row = 0; row < size; row++)
            {
                if (row == pivot) continue;
                double multiple = matrix[row, pivot];
                for (int j = pivot; j <= size; j++) matrix[row, j] -= multiple * matrix[pivot, j];
            }
        }
        return Enumerable.Range(0, size).Select(i => matrix[i, size]).ToArray();

        static double Dot(double[] a, double[] b) => a.Zip(b, (x, y) => x * y).Sum();
    }
}
