using System.Diagnostics;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Base40 = "48d2e517b4e1a7780fbe1aabbeecf4ab5e8094cc";
    private enum Prep40 { Legacy, Force, Coast, Pace }
    private sealed record Setup40(int Id, string Tape, double PreparationMeters, double Lane, Prep40 Method,
        double PeakRequestKph, double CornerRequestKph, double Boost = .68, bool Direct = false, bool Cheap = false);
    private sealed record Fork40(string Name, Checkpoint38 Seed, Flight38 Flight, Activation215[] Tape);
    private sealed record Decision40(double Time, double Station, float Drive, double RollTarget, double FrontLoad,
        double FrontUtilization, double RearUtilization, double RollRate, double RearSlip, double Curve,
        double ReservedAcceleration, int ModelSteps, double Milliseconds, string? Refusal);
    private sealed record Run40(Setup40 Setup, Checkpoint38 Root, Checkpoint38 Fork, Activation215[] EarlierCommands,
        Activation215[] Commands, Flight38 Flight, Decision40[] Decisions);
    private sealed record BendStats40(double? Time, double? Entry, double? Minimum, double? Midpoint, double? Exit,
        double Loaded, double Powered25, double Powered50, double Powered85, double Full, double FrontAir);
    private sealed record SpeedBand40(int Threshold, double Seconds, double Meters);
    private sealed record Score40(int Id, string Stop, string Category, double LastStation, bool First20,
        bool FirstLater, bool UsableAcceleration, bool SecondBend, bool SecondRecovery, bool Linked, double? TotalSeconds,
        BendStats40 First, BendStats40 Second, double StraightPeak, double StraightAverage, double StraightFull,
        double StraightMeanDrive, SpeedBand40[] SpeedBands, double Swept, double Hard, double PeakForce,
        double PeakPower, double EngineWork, double Loss, double EnergyError, int Steps, int ForecastSteps,
        double Milliseconds50, double Milliseconds95, double Milliseconds99, double MillisecondsMax);
    private sealed record Book40(Fork40 Fast, Fork40 Nominal);

    private static Lab38 MakeLinkLab40(string checkout)
    {
        string root = Path.GetFullPath(checkout);
        var (_, spec) = CourseFixture208(root, "C");
        var track = new RacingLineGeometry(Frame);
        return new(root, new Fixture216(spec), Map199.At(2, 0), track,
            new CornerCourse216(track, RootFor210(root, "C").State.State.Rider));
    }

    private static void LinkedStudy221(string checkout, string output, string mode)
    {
        var lab = MakeLinkLab40(checkout);
        switch (mode)
        {
            case "links": SearchLinks40(lab, output); break;
            case "refine": SearchLinks40(lab, output, refined: true); break;
            case "references": AuditReferences40(lab, output); break;
            case "powered": PoweredLinks40(lab, output); break;
            case "mechanics": Mechanics221(lab, output); break;
            case "transfer": TransferLink40(lab, output); break;
            case "stationing": WriteLinkStations40(lab, output); break;
            default: throw new ArgumentException("Use references, links, refine, powered, mechanics, transfer or stationing.");
        }
    }

    private static void WriteLinkStations40(Lab38 lab, string output)
    {
        var next = Map199.At(2, 1);
        Save(Path.Combine(output, "stationing.json"), new { FirstExit = lab.Bend.Length,
            SecondEntry = next.Entry - lab.Bend.Entry, SecondExit = next.Exit - lab.Bend.Entry,
            MassKg = lab.Fixture.Base.Mass, CadenceSeconds = Cadence38, ReactionSeconds = ReactionSeconds216 });
    }

    private static string Prior40(Lab38 lab, string name) =>
        Path.Combine(lab.Root, "data/experiments/speedway220", name + ".json.gz");

    private static Fork40 ReadLink40(Lab38 lab, string name)
    {
        var evidence = Read199<Evidence39>(Prior40(lab, name));
        var seed = new Checkpoint38(evidence.Seed, -PreparationStart37);
        var expected = evidence.Rows[^1];
        var geometry = lab.Geometry(true);
        var replay = ReplayTape38(lab, geometry, seed, evidence.Commands, expected.State.Time, exact: true);
        var actual = replay.Final.State;
        if ((actual.Center - expected.State.Center).Norm > .002 ||
            (actual.Momentum - expected.State.Momentum).Norm > .12 ||
            (actual.Angular - expected.State.Angular).Norm > .12 ||
            (actual.Rider - expected.State.Rider).Norm > .001 ||
            (actual.RiderMomentum - expected.State.RiderMomentum).Norm > .12 ||
            Math.Abs(actual.Wheel - expected.State.Wheel) > .08 || Math.Abs(actual.Crank - expected.State.Crank) > .02 ||
            Math.Abs(actual.Leg.Momentum - expected.State.Leg.Momentum) > .12 ||
            Math.Abs(actual.Leg.Extension - expected.State.Leg.Extension) > .001 ||
            Math.Abs(actual.Steer - expected.State.Steer) > .001 ||
            (actual.Pose.V - expected.State.Pose.V).Norm > .001 ||
            Math.Abs(actual.Time - expected.State.Time) > 1e-8 ||
            actual.Next != expected.State.Next || actual.Command != expected.State.Command)
            throw new InvalidOperationException($"Full-state replay of {name} diverged at {actual.Time:R}: " +
                $"{replay.Stop}, position {(actual.Center - expected.State.Center).Norm:R} m.");
        return new(name, seed, replay, evidence.Commands);
    }

    private static Book40 ReadLinkBook40(Lab38 lab)
    {
        Fork40[] refs = new Fork40[2];
        Parallel.Invoke(() => refs[0] = ReadLink40(lab, "straight-full-diagnostic"),
            () => refs[1] = ReadLink40(lab, "straight-selected"));
        return new(refs[0], refs[1]);
    }

    private static void AuditReferences40(Lab38 lab, string output)
    {
        string[] names = ["original-straight", "full-slide-frontier", "straight-selected", "straight-full-diagnostic",
            "straight-holdout-1", "straight-holdout-2", "straight-holdout-3", "straight-holdout-4"];
        var items = new object[names.Length];
        Parallel.For(0, names.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var run = ReadLink40(lab, names[i]);
            var old = Read199<Evidence39>(Prior40(lab, names[i]));
            var state = run.Flight.Final.State;
            var original = old.Rows[^1].State;
            var trace = new Run40(new(0, names[i], 0, 0, Prep40.Legacy, 0, 0), run.Seed, run.Seed, [],
                run.Tape, run.Flight, []);
            items[i] = new { run.Name, Score = MeasureLink40(lab, lab.Geometry(true), trace),
                PositionError = (state.Center - original.Center).Norm, MomentumError = (state.Momentum - original.Momentum).Norm,
                AngularError = (state.Angular - original.Angular).Norm,
                RiderMomentumError = (state.RiderMomentum - original.RiderMomentum).Norm,
                RotorError = new { Crank = state.Crank - original.Crank, Rear = state.Wheel - original.Wheel },
                RecordedContacts = new { old.Rows[^1].FrontNormal, old.Rows[^1].RearNormal, old.Rows[^1].FootForce },
                ActualContacts = new { Front = run.Flight.Steps[^1].Forces.Front.Normal,
                    Rear = run.Flight.Steps[^1].Forces.Rear.Normal, Foot = run.Flight.Steps[^1].Forces.LeftFoot?.Force },
                RemainingFifo = run.Tape[state.Next..], ExactFinal = state == original };
        });
        var a = ReadArchive38(lab, "70");
        var b = ReadArchive38(lab, "78");
        var root = Path.Combine(lab.Root, "data/experiments/speedway218");
        var t = ReadPlain214<Terminal218>(Path.Combine(root, "terminal-80.json"));
        var published = Read199<Runout37>(Path.Combine(root, "terminal-80-runout.json.gz"));
        var tail = CheckRunout37(lab.Fixture, b.Geometry, lab.Track, lab.Course, lab.Bend, b.Run, true, 2.10, t.Selection);
        if (tail.Final != published.Final || !tail.Trace.Select(x => x.After).SequenceEqual(published.Trace.Select(x => x.After)))
            throw new InvalidOperationException("H37 straight replay failed.");
        var published12 = ReadPlain214<LinkedBaseline40>(Path.Combine(lab.Root,
            "data/experiments/speedway220/straight-full-diagnostic.json"));
        Save(Path.Combine(output, "references.json"), new { Base = Base40, H36 = a.Run.Summary, H37 = b.Run.Summary,
            H37Straight = new { tail.FinalKph, tail.Stable }, Replayed = items, OriginalProposal12 = published12.Replayed,
            NativeWorlds = 0 });
    }

    private sealed record LinkedBaseline40(Score39 Replayed);

    private static Setup40[] LinkGrid40(bool refined)
    {
        int id = refined ? 200 : 0;
        if (refined)
            return (from lead in new[] { 28d, 32, 36, 40 }
                    from lane in new[] { 5.6, 6, 6.4, 6.8 }
                    from corner in new[] { 70d, 74, 77, 80 }
                    select new Setup40(++id, "fast", lead, lane, Prep40.Coast, 115, corner)).ToArray();
        return (from name in new[] { "fast", "nominal" }
                from lead in new[] { 10d, 20, 30, 40 }
                from lane in new[] { 6.6, 8.15 }
                from policy in Enum.GetValues<Prep40>()
                let corner = policy is Prep40.Force or Prep40.Legacy ? 82d : 74d
                let peak = name == "fast" ? 115d : 100d
                select new Setup40(++id, name, lead, lane, policy, peak, corner)).ToArray();
    }

    private static Checkpoint38 AtLink40(Fork40 source, double station)
    {
        int index = Array.FindIndex(source.Flight.Distances, x => x >= station);
        if (index < 0) throw new InvalidOperationException($"No matched state at {station:R} m.");
        return new(source.Flight.Steps[index].After, source.Flight.Distances[index]);
    }

    private static Run40 FromLink40(Lab38 lab, Geometry215 g, Fork40 source, Setup40 setup, double station, WarmLink40? cache = null)
    {
        int count = Array.FindIndex(source.Flight.Distances, x => x >= station) + 1;
        if (count == 0) throw new InvalidOperationException($"No linked fork at {station:R} m.");
        var start = new Checkpoint38(source.Flight.Steps[count - 1].After, source.Flight.Distances[count - 1]);
        return RunLink40(lab, g, source.Seed, start, source.Tape.Where(a => a.Requested < start.State.Time - 1e-10).ToArray(),
            source.Flight.Steps[..count], source.Flight.Distances[..count], setup, cache);
    }

    private static void SearchLinks40(Lab38 lab, string output, bool refined = false)
    {
        var book = ReadLinkBook40(lab);
        var g = lab.Geometry(true);
        var next = Map199.At(2, 1);
        double entry = next.Entry - lab.Bend.Entry;
        if (!refined)
        {
            var direct = new[] { new Setup40(-12, "fast", 10, 8.15, Prep40.Legacy, 110, 82, Direct: true),
                new Setup40(-13, "fast", 10, 8.15, Prep40.Force, 110, 74, Direct: true),
                new Setup40(-45, "nominal", 10, 8.15, Prep40.Force, 100, 74, Direct: true) };
            var directRuns = new Run40[direct.Length];
            Parallel.For(0, direct.Length, i =>
            {
                var source = direct[i].Tape == "fast" ? book.Fast : book.Nominal;
                var trace = FromLink40(lab, g, source, direct[i], source.Flight.Distances[^1]);
                directRuns[i] = ExactLink40(lab, g, trace);
                WriteEvidence39(lab, output, $"direct-{Math.Abs(direct[i].Id)}", g, trace.Root,
                    directRuns[i].Commands, directRuns[i].Flight);
            });
            Save(Path.Combine(output, "direct.json"), directRuns.Select(x =>
                new { x.Setup, x.Fork, Score = MeasureLink40(lab, g, x), x.Decisions }));
            Console.WriteLine("H40 finished exact +4 m continuation probes.");
        }

        var clock = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        TimeSpan cpu = process.TotalProcessorTime;
        var setups = LinkGrid40(refined);
        var search = new object[setups.Length];
        var ranked = new List<(Run40 Run, Score40 Score)>();
        int finished = 0;
        Run40? highestSpeed = null;
        double peakLinked = 0;
        Parallel.For(0, setups.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
        {
            var s = setups[i];
            var run = FromLink40(lab, g, s.Tape == "fast" ? book.Fast : book.Nominal, s, entry - s.PreparationMeters);
            var score = MeasureLink40(lab, g, run);
            search[i] = new { run.Setup, run.Root, run.Fork, run.EarlierCommands, run.Commands, Score = score,
                Last = run.Decisions.LastOrDefault() };
            lock (ranked)
            {
                if (score.Linked && score.StraightPeak > peakLinked)
                {
                    highestSpeed = run;
                    peakLinked = score.StraightPeak;
                }
                ranked.Add((run, score));
                ranked.Sort((a, b) => CompareLink40(a.Score, b.Score));
                if (ranked.Count > 8) ranked.RemoveAt(8);
            }
            int done = Interlocked.Increment(ref finished);
            if (done % 8 == 0) Console.WriteLine($"H40 linked search {done}/{setups.Length}.");
        });
        Save(Path.Combine(output, "link-candidates.json.gz"), search, true);
        Save(Path.Combine(output, "search-cost.json"), new { Candidates = setups.Length,
            CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds, WallSeconds = clock.Elapsed.TotalSeconds });

        var shortlist = ranked.Take(6).Select(x => x.Run).ToList();
        if (highestSpeed is not null && !shortlist.Any(x => x.Setup.Id == highestSpeed.Setup.Id))
            shortlist.Add(highestSpeed);
        var confirmed = shortlist.AsParallel().WithDegreeOfParallelism(6)
            .Select(x => ExactLink40(lab, g, x)).Select(x => (Run: x, Score: MeasureLink40(lab, g, x)))
            .OrderBy(x => x.Score, Comparer<Score40>.Create(CompareLink40)).ToArray();
        var best = confirmed[0];
        Save(Path.Combine(output, "training-selection.json"), new { best.Run.Setup, best.Score,
            ExactShortlist = confirmed.Select(x => x.Score), best.Run.Decisions });
        WriteEvidence39(lab, output, "linked-training", g, best.Run.Root, best.Run.Commands, best.Run.Flight);
        var high = confirmed.Where(x => x.Score.Linked).OrderByDescending(x => x.Score.StraightPeak).FirstOrDefault();
        if (high.Run is not null)
        {
            Save(Path.Combine(output, "speed-frontier.json"), new { high.Run.Setup, high.Score });
            if (high.Run.Setup.Id != best.Run.Setup.Id)
                WriteEvidence39(lab, output, "linked-speed-frontier", g, high.Run.Root, high.Run.Commands, high.Run.Flight);
        }
        ValidateLinkPolicy40(lab, output, best.Run.Setup, book, best.Run, best.Score, refined);
    }
}
