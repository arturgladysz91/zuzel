using SharedWorldSpeedway;
using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private enum Pulse40 { Retain, Short, Split, SlipLimited }
    private sealed record Power40(int Id, double Onset, double DurationMeters, Pulse40 Pulse, double FrontReserve,
        double InwardShift, bool Boot);

    private static void PoweredLinks40(Lab38 lab, string output)
    {
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var model = Reconstruct39(lab);
        var g = model.Reference.Geometry;
        var parent = model.Attempts.Single(a => a.Plan.Id == 24);
        var witness = model.Attempts.Single(a => a.Plan.Id == 57);
        var original = new Run40(new(57, "archive", 0, 0, Prep40.Legacy, 0, 0), witness.Seed, witness.Initial,
            witness.InitialCommands, witness.Commands, witness.Trail, []);
        var frontier = ReadLink40(lab, "full-slide-frontier");
        int id = 1000;
        var setups = (from onset in new[] { 55d, 64d, 70d, 74d }
                      from pulse in Enum.GetValues<Pulse40>()
                      from variant in Enumerable.Range(0, 4)
                      select new Power40(++id, onset, variant % 2 == 0 ? 13 : 18, pulse,
                          variant < 2 ? 180 : 310, variant < 2 ? .15 : .58, variant % 2 == 0)).ToArray();
        var scores = new object[setups.Length];
        var ranked = new List<(Power40 Setup, Run40 Run, Score40 Score)>();
        var stage = new Setup40(0, "power", 40, 6.6, Prep40.Coast, 105, 74);
        int finished = 0;
        Parallel.For(0, setups.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
        {
            var p = setups[i];
            var run = PowerPrefix40(lab, model, g, parent.Seed, parent.Commands, parent.Trail, p, stage);
            var s = MeasureLink40(lab, g, run);
            scores[i] = new { Setup = p, run.Root, run.Fork, run.EarlierCommands, run.Commands, Score = s,
                Last = run.Decisions.LastOrDefault() };
            lock (ranked)
            {
                ranked.Add((p, run, s));
                ranked.Sort((a, b) => PowerRank40(a.Score, b.Score));
                if (ranked.Count > 8) ranked.RemoveAt(8);
            }
            int n = Interlocked.Increment(ref finished);
            if (n % 8 == 0) Console.WriteLine($"H40 early powered search {n}/{setups.Length}.");
        });
        Save(Path.Combine(output, "powered-candidates.json.gz"), scores, true);
        Save(Path.Combine(output, "powered-witness.json"), new
        {
            Inherited57 = MeasureLink40(lab, g, original),
            Frontier220 = MeasureLink40(lab, g, new(stage, frontier.Seed, frontier.Seed, [], frontier.Tape, frontier.Flight, [])),
            Samples = new[] { 55d, 64, 70, 74, 83, 90, 92, 95, 100, 105 }.Select(m =>
            {
                int i = Array.FindIndex(witness.Trail.Distances, d => d >= m);
                var p = witness.Trail.Steps[i];
                return new { Meters = witness.Trail.Distances[i], Active = p.Before.Command, State = p.After,
                    Drive = p.Before.Command.Bike.Drive, p.Forces.RearSlip, Front = p.Forces.Front,
                    Rear = p.Forces.Rear, p.Forces.FrontRollBand, p.Forces.FrontYawBand,
                    RollRate = RollRate36(p.After, g.Motion(p.After).BodyRate), p.Forces.LeftFoot, p.RollPitchYawMoment };
            })
        });
        var advanced = ranked.ToArray().AsParallel().WithDegreeOfParallelism(6).Select(t =>
        {
            var combined = ExtendPower40(lab, g, t.Run);
            var replay = ExactLink40(lab, g, combined);
            return (t.Setup, Run: replay, Score: MeasureLink40(lab, g, replay));
        }).ToArray();
        Array.Sort(advanced, (a, b) =>
        {
            int qualified = b.Score.Linked.CompareTo(a.Score.Linked);
            return qualified != 0 ? qualified :
                a.Score.Linked ? CompareLink40(a.Score, b.Score) : PowerRank40(a.Score, b.Score);
        });
        var best = advanced[0];
        var h = new[] { new HeldSeed38("low and inside", -.38, -.062, .004), new HeldSeed38("fast and wide", .31, .083, .007),
            new HeldSeed38("wide and slow", -.26, .096, .010), new HeldSeed38("fast and inside", .27, -.108, .013) };
        Save(Path.Combine(output, "powered-freeze.json"), new { best.Setup, LinkedSetup = best.Run.Setup, Holdouts = h,
            MatchedNominal = best.Score, ExactShortlist = advanced.Select(x => x.Score) });
        WriteEvidence39(lab, output, "powered-selected", g, best.Run.Root, best.Run.Commands, best.Run.Flight);
        var hold = new Score40[4];
        var p24 = ReadPlain214<RecourseArchive39>(Path.Combine(lab.Root, "data/experiments/speedway219/recourse.json"))
            .Trials.Single(x => x.Settings.Id == 24).Settings;
        Parallel.For(0, hold.Length, i =>
        {
            var perturb = h[i];
            var straight = SteadyCircle215(lab.Engine(g), g, lab.Fixture.Rest(g, NominalSupport216),
                (40 + perturb.SpeedKph) / 3.6, 0, parent.Seed.State.Time, lab.Fixture.Ratio, balanceRider: true);
            if (straight.Residual > .005) throw new InvalidOperationException("Unreachable powered perturbation.");
            var seed = new Checkpoint38(Place36(straight.State, lab.Bend.Entry + parent.Seed.Distance,
                8.20 + perturb.LaneMeters), parent.Seed.Distance);
            var fifo = parent.InitialCommands.Select(x => x with { Time = x.Time + perturb.QueueDelaySeconds }).ToArray();
            var early = ReplayTape38(lab, g, seed, fifo, parent.Initial.State.Time);
            if (early.Stop == "verified interval")
            {
                var controlled = PulseRun38(lab, model.Reference, model.Parent, p24, g, early.Final, fifo, false, model.EquilibriumDrive);
                early = controlled.Trail with { Steps = [..early.Steps, ..controlled.Trail.Steps],
                    Distances = [..early.Distances, ..controlled.Trail.Distances],
                    Boundary = Math.Min(early.Boundary, controlled.Trail.Boundary),
                    Ground = Math.Min(early.Ground, controlled.Trail.Ground) };
                fifo = controlled.Commands;
            }
            var raw = early.Final.Distance >= best.Setup.Onset ? PowerPrefix40(lab, model, g, seed, fifo, early, best.Setup, stage)
                : new Run40(stage, seed, early.Final, fifo, fifo, early, []);
            var exact = ExactLink40(lab, g, ExtendPower40(lab, g, raw));
            hold[i] = MeasureLink40(lab, g, exact);
            WriteEvidence39(lab, output, $"powered-holdout-{i + 1}", g, exact.Root, exact.Commands, exact.Flight);
        });
        Save(Path.Combine(output, "powered-qualification.json"), new { best.Setup, Nominal = best.Score, Holdouts = hold,
            Candidates = setups.Length, CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
            WallSeconds = timer.Elapsed.TotalSeconds, NativeWorlds = 0 });
    }

    private static int PowerRank40(Score40 a, Score40 b)
    {
        static int Rank(Score40 x) => x.Linked ? 5 : x.First20 ? 4 : x.LastStation >= 116 ? 3 : x.First.Exit is not null ? 2 : 1;
        int rank = Rank(b).CompareTo(Rank(a));
        if (rank != 0) return rank;
        if (Rank(a) >= 3)
        {
            rank = Nullable.Compare(a.First.Time, b.First.Time);
            if (rank != 0) return rank;
        }
        rank = b.LastStation.CompareTo(a.LastStation);
        return rank == 0 ? b.First.Full.CompareTo(a.First.Full) : rank;
    }

    private static Run40 ExtendPower40(Lab38 lab, Geometry215 g, Run40 prefix)
    {
        if (prefix.Flight.Final.Distance < lab.Bend.Length + 20) return prefix;
        var f = prefix.Flight;
        var suffix = RunLink40(lab, g, prefix.Root, f.Final, prefix.Commands, f.Steps, f.Distances, prefix.Setup);
        return suffix with { Fork = prefix.Fork, EarlierCommands = prefix.EarlierCommands,
            Decisions = [..prefix.Decisions, ..suffix.Decisions] };
    }

    private static Run40 PowerPrefix40(Lab38 lab, Witnesses39 book, Geometry215 g, Checkpoint38 root,
        Activation215[] source, Flight38 prior, Power40 setup, Setup40 linked)
    {
        int count = Array.FindIndex(prior.Distances, d => d >= setup.Onset) + 1;
        var at = new Checkpoint38(prior.Steps[count - 1].After, prior.Distances[count - 1]);
        var state = at;
        var steps = new List<Step215>(prior.Steps[..count]);
        var distances = new List<double>(prior.Distances[..count]);
        var decisions = new List<Decision40>();
        var original = source.Where(x => x.Requested < at.State.Time - 1e-10).ToArray();
        var queue = original;
        var model = lab.Engine(g);
        var force = steps[^1].Forces;
        double swept = H36Footprint.Clearance(lab.Track, g, at.State, false);
        double hard = H36Footprint.GroundMargin(g, at.State);
        double end = lab.Bend.Length + 20, limit = at.State.Time + 7;
        double? straightLane = null;
        string stop = "verified interval";
        while (state.Distance < end && state.State.Time < limit && stop == "verified interval")
        {
            var s = state;
            var t = Stopwatch.GetTimestamp();
            var mark = lab.Course.Mark(s.State, g);
            double spin = ContactSlip39(g, s.State);
            var k = g.Motion(s.State);
            double rollRate = RollRate36(s.State, k.BodyRate);
            double progress = s.Distance - setup.Onset;
            double finish = setup.Pulse == Pulse40.Short ? setup.DurationMeters * .62 : setup.DurationMeters;
            bool pulse = progress <= finish && (setup.Pulse != Pulse40.Split || progress < 4 || progress > 8);
            var tune = book.Parent.Tune;
            bool upright = s.Distance >= lab.Bend.Length + 7;
            if (upright)
            {
                straightLane ??= mark.Road.OffsetMeters - .45;
                tune = tune with { Lane = straightLane.Value, Apex = 0, Lookahead = 24, RollGain = 19, Damping = 10,
                    Fore = 0, Side = 0, Vertical = 0, Boot = 0 };
            }
            RollReference36 target = default;
            var c = H36Control(s.State, g, model, mark, lab.Bend.Entry + s.Distance, lab.Bend, tune, 80,
                book.EquilibriumDrive, queue, lab.Course, referenceBasis: Plan);
            decisions.Add(new(s.State.Time, s.Distance, target.Drive, target.Roll, force.Front.Normal,
                force.Front.Utilization, force.Rear.Utilization, rollRate, spin, target.Curvature, 0, c.Steps,
                Stopwatch.GetElapsedTime(t).TotalMilliseconds, c.Stop));
            if (c.Command is not { } command) { stop = "candidate exhaustion: " + c.Stop; break; }
            queue = Submit38(s, queue, command);
            var w = Window38(lab, g, model, s, queue, Cadence38, finishAt: end);
            state = w.Final;
            steps.AddRange(w.Steps);
            distances.AddRange(w.Distances);
            force = w.Steps.LastOrDefault()?.Forces ?? force;
            swept = Math.Min(swept, w.Boundary);
            hard = Math.Min(hard, w.Ground);
            stop = w.Stop;

            RollReference36 Plan(RiderView body, double reference)
            {
                var q = upright ? Reference36(body, reference, lab.Bend, tune, 81, book.EquilibriumDrive) :
                    Reference37(body, reference, lab.Bend, tune, book.Parent.TargetKph, book.EquilibriumDrive, book.Parent.Plan);
                double rampExit = Smooth37((s.Distance - lab.Bend.Length + 13) / 19);
                double drive = q.Drive;
                if (pulse)
                {
                    drive = 1;
                    if (setup.Pulse == Pulse40.SlipLimited)
                        drive = Math.Clamp(1.07 - .95 * Math.Max(0, spin - .31), .23, 1);
                }
                else if (!upright && (force.Front.Normal < setup.FrontReserve || spin > .57 || Math.Abs(rollRate) > .9))
                    drive = Math.Min(drive, .19 + .22 * rampExit);
                if (setup.Pulse != Pulse40.Retain && pulse && force.Front.Normal < setup.FrontReserve)
                    drive *= Math.Clamp(force.Front.Normal / setup.FrontReserve, .2, 1);
                if (upright && (force.Front.Normal < 180 || Math.Abs(rollRate) > .85)) drive = Math.Min(drive, .27);
                var rider = q.Rider + new Vec215(.058, -.020, -.034) * (setup.InwardShift * (1 - rampExit));
                double wish = q.Roll * (upright ? .60 : 1 - .36 * rampExit);
                target = q with { Drive = (float)drive, Roll = ClearanceRoll37(g, s.State, wish, .008), Rider = rider,
                    Boot = setup.Boot ? q.Boot * (1 - rampExit) : q.Boot * .48 * (1 - rampExit) };
                return target;
            }
        }
        if (stop == "verified interval") stop = state.Distance >= end ? "powered exit checkpoint reached" : "powered horizon";
        return new(linked with { Id = setup.Id }, root, at, original, queue, new(state, steps.ToArray(), distances.ToArray(),
            stop, swept, hard), decisions.ToArray());
    }
}
