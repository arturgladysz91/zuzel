using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Base44 = "3d44ba9fea8a030b61f5d40b425fef38b96ac2a9";
    private const int Proposals44 = 64;
    private const int Jobs44 = 8;
    private const double BrakeReaction44 = .155;
    private const int MeasureStride44 = 16;

    private enum Guide44 { Continuous, PriorBlend, Lookahead, Measured }
    private sealed record Route44(int Id, Guide44 Guide, double EntryOutside, double Inside, double ExitOutside,
        double LeadMeters, double PreviewMeters, double StraightKph, double TurnAcceleration, double Deceleration,
        double LeanScale, double Damping, double ThrottleBias, double RiderBias, double EarlyHeading, double ExitDrive,
        double StartStation, double? Band = null, bool PreserveFrontier = false, double SupportReserve = 0);
    private sealed record Piece44(double Start, Route44 Route);
    private sealed record StateDecision44(double Time, double Station, int Route, float Drive, double Steering,
        double TargetOffset, double TargetRoll, double AimSpeed, double Heading, double Aperture,
        double Milliseconds, int ForecastSteps, int RecoveryAttempts, string? Refusal);
    private sealed record Ride44Result(Fork40 Motion, Piece44[] Schedule, StateDecision44[] Decisions,
        double ControllerCpu, double IntegrationCpu, double WallSeconds);
    private sealed record ArcReceipt44(double Begin, double End, double? EntryKph, double? MinimumKph, double? ExitKph,
        double? Seconds, double? PeakFollowingKph, double? Clearance, double? Ground, double? ExitPlus22Kph);
    private sealed record LapReceipt44(int Lap, double? Seconds, double? PathMeters, double? PeakKph,
        ArcReceipt44[] Arcs, double? Boundary, double? Ground, bool Plus22);
    private sealed record Candidate44(int Id, Piece44[] Schedule, string Stop, double Station, double Seconds,
        double Swept, double Hard, int Reviews, int Fallbacks, int ForecastSteps, double PathMeters,
        Score40 Link, LapReceipt44[] Laps, bool Pass, double PeakBeforeEnd, double SeamError);
    private sealed record Exact44(Candidate44 Score, Ride44Result Run);

    private static void PeriodicStudy224(string checkout, string output, string mode)
    {
        var lab = MakeLinkLab40(checkout);
        if (mode is "references" or "all") Reproduce44(lab, output);
        if (mode is "speed-references" or "all") SpeedReferences44(lab, output);
        if (mode == "review-references") TrackerSet44(lab, output, queued: false);
        if (mode is "queued-references" or "all") TrackerSet44(lab, output, queued: true);
        if (mode is "line" or "all") Explore44(lab, output, highSpeed: false);
        if (mode is "speed" or "all") Explore44(lab, output, highSpeed: true);
        if (mode is "repeat" or "all") RepeatLine44(lab, output);
        if (mode == "support-recourse" || (mode == "all" && File.Exists(Path.Combine(output, "initial-four.json.gz"))))
            SupportRecourse44(lab, output);
        if (mode == "support-checks" || (mode == "all" && File.Exists(Path.Combine(output, "support-recourse.json"))))
            CertifySupport44(lab, output);
        if (mode is "recourse" or "all") Replan44(lab, output);
        if (mode is "speed-recourse" or "all") SpeedRecourse44(lab, output);
        if (mode is "feedback" or "all") Hold44(lab, output);
        if (mode is "verify" or "all") VerifyStudy44(lab, output);
        if (mode is not ("references" or "speed-references" or "queued-references" or "review-references" or "line" or "speed" or "repeat" or "support-recourse" or "support-checks" or "recourse" or "speed-recourse" or "feedback" or "verify" or "all"))
            throw new ArgumentException("Use references, speed-references, queued-references, review-references, line, speed, repeat, support-recourse, support-checks, recourse, speed-recourse, feedback, verify or all.");
    }

    private static double SecondExit44(Lab38 lab) => Map199.At(2, 1).Exit - lab.Bend.Entry;
    private static double Half44 => Frame.LapMeters * .5;

    private static Fork40 Archived44(Lab38 lab, string path, string label) =>
        ReadMotion42(lab, ArchivePath43(lab, path), label);

    private static Ride44Result Ride44(Lab38 lab, Geometry215 g, Fork40 parent, double fork, Piece44[] schedule,
        double finish, SpatialBike36? shape = null, int recordedDecisions = int.MaxValue)
    {
        var prefix = Slice42(parent, fork);
        var model = lab.Engine(g);
        var envelope = shape ?? H36Footprint;
        var rows = new List<Step215>(prefix.Prefix);
        var distance = new List<double>(prefix.Distances);
        var decisions = new List<StateDecision44>();
        var tape = prefix.Commands;
        var state = prefix.At;
        var last = rows.LastOrDefault()?.Forces;
        var timer = Stopwatch.StartNew();
        long cpuControl = 0, cpuPlant = 0;
        double boundary = envelope.Clearance(lab.Track, g, state.State, false);
        double ground = envelope.GroundMargin(g, state.State);
        int piece = 0;
        string stop = "verified interval";
        double expires = state.State.Time + 105;
        while (state.Distance < finish && state.State.Time < expires && stop == "verified interval" &&
            decisions.Count < recordedDecisions)
        {
            while (piece + 1 < schedule.Length && state.Distance >= schedule[piece + 1].Start) piece++;
            var p = schedule[piece].Route;
            long start = Stopwatch.GetTimestamp(), cpu = ThreadTicks43();
            var pick = Choose44(lab, g, model, envelope, state, last, tape, p);
            cpuControl += ThreadTicks43() - cpu;
            decisions.Add(pick.Note with { Milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds });
            if (pick.Command is not { } command)
            {
                stop = pick.Note.Refusal ?? "adaptive candidate exhaustion";
                break;
            }
            cpu = ThreadTicks43();
            tape = Submit38(state, tape, command);
            var r = Window38(lab, g, model, state, tape, Cadence38, envelope, finishAt: finish);
            cpuPlant += ThreadTicks43() - cpu;
            rows.AddRange(r.Steps);
            distance.AddRange(r.Distances);
            state = r.Final;
            last = r.Steps.LastOrDefault()?.Forces ?? last;
            boundary = Math.Min(boundary, r.Boundary);
            ground = Math.Min(ground, r.Ground);
            stop = r.Stop;
        }
        if (stop == "verified interval")
            stop = state.Distance >= finish ? "completed linked horizon" : "decision or time horizon";

        var trace = new Flight38(state, rows.ToArray(), distance.ToArray(), stop, boundary, ground);
        return new(new(schedule[0].Route.Id.ToString(), parent.Seed, trace, tape), schedule, decisions.ToArray(),
            cpuControl / 1e7, cpuPlant / 1e7, timer.Elapsed.TotalSeconds);
    }

    private static Ride44Result ExactReplay44(Lab38 lab, Geometry215 g, Ride44Result candidate, SpatialBike36? shape = null)
    {
        var m = candidate.Motion;
        var exact = ReplayTape38(lab, g, m.Seed, m.Tape, m.Flight.Final.State.Time, shape, exact: true);
        if (exact.Stop == "verified interval") exact = exact with { Stop = m.Flight.Stop };
        return candidate with { Motion = m with { Flight = exact } };
    }

    private static Candidate44 Measure44(Lab38 lab, Geometry215 g, Ride44Result r, bool linked)
    {
        var motion = r.Motion;
        var f = motion.Flight;
        double station = f.Final.Distance;
        double lap = Frame.LapMeters;
        double first = AtPhase43(lab, g, motion, 0).Time;
        double linkEnd = SecondExit44(lab) + PostMarkerMeters43;
        int i = Index43(f.Distances, linkEnd);
        var cut = motion with
        {
            Flight = f with { Final = new(f.Steps[i].After, f.Distances[i]), Steps = f.Steps[..(i + 1)],
                Distances = f.Distances[..(i + 1)] }
        };
        var score = Score43(lab, g, cut);
        var laps = Enumerable.Range(0, Math.Clamp((int)Math.Floor(station / lap) + 1, 1, 5))
            .Select(n => Lap44(lab, g, motion, n)).ToArray();
        bool passes = linked ? score.Linked : station >= 2 * lap + PostMarkerMeters43 - .001 &&
            f.Boundary >= .50 && f.Ground > 0;
        double seam = station >= lap ? SeamError43(AtPhase43(lab, g, motion, 0), AtPhase43(lab, g, motion, lap)) : 0;
        return new(r.Schedule[0].Route.Id, r.Schedule, f.Stop, station, f.Final.State.Time - first, f.Boundary, f.Ground,
            r.Decisions.Length, r.Decisions.Sum(x => x.RecoveryAttempts), r.Decisions.Sum(x => x.ForecastSteps),
            PathLength43(motion), score, laps, passes, score.StraightPeak, seam);
    }

    private static LapReceipt44 Lap44(Lab38 lab, Geometry215 g, Fork40 m, int n)
    {
        double lap = Frame.LapMeters;
        var f = m.Flight;
        double begin = n * lap, finish = begin + lap;
        int index = Index43(f.Distances, begin);
        int close = Index43(f.Distances, finish);
        var steps = f.Steps[index..(close + 1)];
        var a = Arc(begin, begin + lab.Bend.Length);
        var b = Arc(begin + Half44, begin + SecondExit44(lab));
        return new(n + 1, finish <= f.Final.Distance ? steps[^1].After.Time - steps[0].After.Time : null,
            finish <= f.Final.Distance ? steps.Sum(r => (r.After.Center - r.Before.Center).Plane.Norm) : null,
            steps.Max(x => Speed38(x.After, g)), [a, b],
            Sampled(steps).Min(x => H36Footprint.Clearance(lab.Track, g, x.After, false) - H36Footprint.TransportBound(g, x) * .5),
            Sampled(steps).Min(x => H36Footprint.GroundMargin(g, x.After) - H36Footprint.TransportBound(g, x, groundOnly: true) * .5),
            finish + PostMarkerMeters43 <= f.Final.Distance);

        IEnumerable<Step215> Sampled(Step215[] span) =>
            span.Where((_, n) => n % MeasureStride44 == 0 || n == span.Length - 1);

        ArcReceipt44 Arc(double entry, double exit)
        {
            double? speed(double at) => f.Final.Distance >= at ? Speed38(f.Steps[Index43(f.Distances, at)].After, g) : null;
            var selected = entry <= f.Final.Distance ? f.Steps[Index43(f.Distances, entry)..(Index43(f.Distances, exit) + 1)] : [];
            var straight = exit < f.Final.Distance ? f.Steps[Index43(f.Distances, exit)..(Index43(f.Distances, entry + Half44) + 1)] : [];
            return new(entry, exit, speed(entry), selected.Length == 0 ? null : selected.Min(x => Speed38(x.After, g)), speed(exit),
                speed(exit) is null ? null : selected[^1].After.Time - selected[0].After.Time,
                straight.Length == 0 ? null : straight.Max(x => Speed38(x.After, g)),
                selected.Length == 0 ? null : Sampled(selected).Min(x => H36Footprint.Clearance(lab.Track, g, x.After, false)),
                selected.Length == 0 ? null : Sampled(selected).Min(x => H36Footprint.GroundMargin(g, x.After)), speed(exit + 22));
        }
    }

    private static int Compare44(Candidate44 a, Candidate44 b, bool fast = false)
    {
        int status = b.Pass.CompareTo(a.Pass);
        if (status != 0) return status;
        int rank;
        if (a.Pass && b.Pass)
        {
            if (fast && (a.PeakBeforeEnd >= 100) != (b.PeakBeforeEnd >= 100))
                return (b.PeakBeforeEnd >= 100).CompareTo(a.PeakBeforeEnd >= 100);
            rank = a.Seconds.CompareTo(b.Seconds);
        }
        else
        {
            rank = b.Station.CompareTo(a.Station);
            if (rank == 0) rank = a.SeamError.CompareTo(b.SeamError);
        }
        return rank != 0 ? rank : a.Id.CompareTo(b.Id);
    }

    private static Route44 Plan44(Lab38 lab, int i, bool speed)
    {
        int id = (speed ? 200 : 100) + i;
        double q(int b) => Halton43(i + 1, b);
        var method = (Guide44)(i % 4);
        double outside = 6.7 + 4.1 * q(2), inside = 4.4 + 3.4 * q(3), exit = 6.0 + 3.9 * q(5);
        double lead = 25 + 32 * q(7), preview = 13 + 20 * q(11);
        double? band = speed ? new[] { 98d, 100, 103, 106, 110, 115, 120 }[i % 7] : null;
        double pace = band ?? (i % 4 == 0 ? 65 + 13 * q(13) : 76 + 20 * q(13));
        double a = speed ? 9.2 + 4.6 * q(17) : i % 4 == 0 ? 6.8 + 2.3 * q(17) : 7.9 + 3.4 * q(17);
        double start = speed ? lab.Bend.Length - 10 + 27 * q(29) : SecondExit44(lab) - 18 + 28 * q(29);
        return new(id, method, outside, inside, exit, lead, preview, pace, a, 1.8 + 2.7 * q(19),
            .87 + .20 * q(23), 9 + 9 * q(31), .04 + .18 * q(37), -.035 + .09 * q(41), 1.1 + 8 * q(43),
            .08 + .52 * q(47), start, band, speed && i % 4 == 3);
    }

    private static T Persist44<T>(string directory, int id, Func<T> run)
    {
        string file = Path.Combine(directory, id.ToString() + ".json");
        if (File.Exists(file)) return ReadPlain214<T>(file);
        var result = run();
        string temporary = file + "-" + Guid.NewGuid().ToString("N") + ".tmp";
        Save(temporary, result);
        File.Move(temporary, file);
        return result;
    }

    private static void Explore44(Lab38 lab, string output, bool highSpeed)
    {
        var g = lab.Geometry(true);
        var source = highSpeed ? Archived44(lab, "speedway222/frontier/speed-frontier", "H41 95.63 frontier") :
            Archived44(lab, "speedway222/qualification/nominal", "H41 linked nominal");
        var guides = Enumerable.Range(0, Proposals44).Select(i => Plan44(lab, i, highSpeed)).ToArray();
        double finish = highSpeed ? SecondExit44(lab) + PostMarkerMeters43 : 2 * Frame.LapMeters + PostMarkerMeters43;
        using var proc = Process.GetCurrentProcess();
        var cpu = proc.TotalProcessorTime;
        var clock = Stopwatch.StartNew();
        string file = highSpeed ? "speed" : "line";
        string checkpoints = Path.Combine(output, file + "-checkpoints");
        Directory.CreateDirectory(checkpoints);
        var candidates = guides.AsParallel().WithDegreeOfParallelism(Jobs44).Select(p =>
            Persist44(checkpoints, p.Id, () =>
            {
                var r = Ride44(lab, g, source, p.StartStation, [new(p.StartStation, p)], finish);
                return Measure44(lab, g, r, highSpeed);
            })).ToArray();
        var order = Comparer<Candidate44>.Create((a, b) => Compare44(a, b, highSpeed));
        var picks = candidates.OrderBy(x => x, order).Take(highSpeed ? 5 : 6).Select(x => x.Id);
        if (highSpeed)
            picks = picks.Concat(candidates.GroupBy(x => x.Schedule[0].Route.Band).Select(xs =>
                xs.OrderByDescending(x => x.Pass).ThenBy(x => Math.Abs(x.PeakBeforeEnd - xs.Key!.Value))
                    .ThenByDescending(x => x.Station).First().Id));
        var ids = picks.Distinct().ToHashSet();
        var replays = guides.Where(p => ids.Contains(p.Id)).AsParallel().WithDegreeOfParallelism(Jobs44).Select(p =>
        {
            var r = ExactReplay44(lab, g, Ride44(lab, g, source, p.StartStation, [new(p.StartStation, p)], finish));
            return new Exact44(Measure44(lab, g, r, highSpeed), r);
        }).OrderBy(x => x.Score, order).ToArray();
        var best = replays[0];
        Save(Path.Combine(output, file + "-search.json.gz"), new
        {
            Baseline = Score43(lab, g, source), Finish = finish,
            Proposals = candidates.OrderBy(x => x.Id), Exact = replays.Select(x => x.Score), Winner = best.Score,
            CpuSeconds = (proc.TotalProcessorTime - cpu).TotalSeconds, WallSeconds = clock.Elapsed.TotalSeconds,
            NativeWorlds = 0
        }, true);
        Save(Path.Combine(output, file + "-selected-policy.json"), best.Score.Schedule);
        WriteEvidence39(lab, output, file + "-selected", g, best.Run.Motion.Seed, best.Run.Motion.Tape, best.Run.Motion.Flight);
        if (highSpeed)
        {
            var peak = replays.OrderByDescending(x => x.Score.PeakBeforeEnd).First();
            WriteEvidence39(lab, output, "speed-peak-diagnostic", g, peak.Run.Motion.Seed, peak.Run.Motion.Tape, peak.Run.Motion.Flight);
        }
        Console.WriteLine($"H43 {file}: {candidates.Count(x => x.Pass)}/{candidates.Length} screened, " +
            $"{replays.Count(x => x.Score.Pass)}/{replays.Length} exact, top station {best.Score.Station:F3} m.");
    }
}
