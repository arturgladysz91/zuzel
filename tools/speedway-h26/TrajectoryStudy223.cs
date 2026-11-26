using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Base43 = "150aafb128e47bbac4bf6fb19a22ac3b5bb5ba42";
    private const int Proposals43 = 64;
    private const int Parallel43 = 8;

    private sealed record Knot43(double Station, double Radius, double Drive, double Steering,
        Vec215 Rider, double Foot, bool AbsoluteDrive = false, double Flat = 0);

    private sealed record Proposal43(int Id, string Parent, double? StraightBand, Knot43[] Knots, double CycleScale = 1);
    private sealed record SearchResult43(Proposal43 Plan, Score40 Score, double ManeuverDistance,
        double PhaseError, double PathMeters, string Stop, int ReusedSteps, int IntegratedSteps, bool Exact);
    private sealed record Shooting43(SearchResult43 Result, Fork40 Motion);
    private sealed record Phase43(double Station, double Time, double Offset, double RoadSpeed, double SideSpeed,
        double Direction, double FrameYaw, double Pitch, double Roll, Vec215 BodyRate, Vec215 RoadAngular,
        Vec215 Rider, Vec215 RiderMomentum, Vec215 RiderRate, double Crank, double RearWheel, double FrontWheel,
        double Steer, LegState216 Leg, Vec215 FootForce, double FrontLoad, double RearLoad, Command215 Active,
        RelativeCommand43[] Pending);
    private sealed record RelativeCommand43(double Requested, double Time, Command215 Command);

    private static string ArchivePath43(Lab38 lab, string path) => Path.Combine(lab.Root, "data/experiments", path + ".json.gz");

    private static void TrajectoryStudy223(string checkout, string output, string mode)
    {
        var lab = MakeLinkLab40(checkout);
        if (mode is "references" or "all") References43(lab, output);
        if (mode is "shoot" or "all") SearchManeuver43(lab, output);
        if (mode is "repair" or "all") RepairShooting43(lab, output);
        if (mode is "periodic" or "all") Periodic43(lab, output);
        if (mode is "closure" or "all") ClosePeriodic43(lab, output);
        if (mode is "closure-local" or "all") ClosePeriodic43(lab, output, local: true);
        if (mode is "feedback" or "all") QualifyTracking43(lab, output);
        if (mode is "verify" or "all") ProveTrajectories43(lab, output);
        if (mode is not ("references" or "shoot" or "repair" or "periodic" or "closure" or "closure-local" or "feedback" or "verify" or "all"))
            throw new ArgumentException("Use references, shoot, repair, periodic, closure, closure-local, feedback, verify or all.");
    }

    private static void References43(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        Reproduce42(lab, output);
        string[] sources = ["speedway222/qualification/nominal", "speedway222/qualification/holdout-1",
            "speedway222/qualification/holdout-2", "speedway222/qualification/holdout-3",
            "speedway222/qualification/holdout-4", "speedway222/qualification/powered-nominal",
            "speedway222/cycle/cycle-flying", "speedway220/full-slide-frontier",
            "speedway221/powered/powered-selected"];
        var references = sources.AsParallel().AsOrdered().WithDegreeOfParallelism(Parallel43).Select(source =>
        {
            string path = ArchivePath43(lab, source);
            var saved = Read199<Evidence39>(path);
            var replay = ReadMotion42(lab, path, source);
            var tail = replay.Flight.Final;
            var expected = saved.Rows[^1].State;
            return new
            {
                Source = source, StateEqual = tail.State == expected, PositionDefect = (tail.State.Center - expected.Center).Norm,
                Initial = replay.Seed, Last = tail, ArchivedTail = expected, Queue = Pending43(replay, tail, true),
                replay.Flight.Boundary, replay.Flight.Ground, replay.Flight.Stop,
                Score = Score43(lab, g, replay)
            };
        }).ToArray();
        var lap = ReadMotion42(lab, ArchivePath43(lab, sources[6]), "H41 policy 301");
        var start = AtPhase43(lab, g, lap, 0);
        var seam = AtPhase43(lab, g, lap, Frame.LapMeters);
        var last = PhaseAt43(lab, g, lap, lap.Flight.Steps.Length - 1);
        Save(Path.Combine(output, "reference-continuity.json"), new
        {
            Base = Base43, Replayed = references, Lap = new
            {
                FirstSeconds = seam.Time - start.Time, Start = start, Seam = seam, Last = last,
                SeamError = SeamError43(start, seam), lap.Flight.Stop, lap.Flight.Boundary, lap.Flight.Ground
            }, NativeWorlds = 0
        });
    }

    private static RelativeCommand43[] Pending43(Fork40 motion, Checkpoint38 at, bool includeSameTime)
    {
        double t = at.State.Time;
        return motion.Tape.Skip(at.State.Next).TakeWhile(x => includeSameTime
                ? x.Requested <= t + 1e-9 : x.Requested < t - 1e-9)
            .Select(x => new RelativeCommand43(x.Requested - t, x.Time - t, x.Command)).ToArray();
    }

    private static Phase43 AtPhase43(Lab38 lab, Geometry215 g, Fork40 motion, double station) =>
        PhaseAt43(lab, g, motion, Index43(motion.Flight.Distances, station));

    private static int Index43(double[] samples, double station)
    {
        int index = Array.BinarySearch(samples, station);
        return Math.Clamp(index >= 0 ? index : ~index, 0, samples.Length - 1);
    }

    private static Phase43 PhaseAt43(Lab38 lab, Geometry215 g, Fork40 motion, int index)
    {
        var row = motion.Flight.Steps[index];
        var s = row.After;
        var mark = lab.Course.Mark(s, g);
        double road = Math.Atan2(mark.Road.Tangent.Y, mark.Road.Tangent.X);
        double speed = Speed38(s, g) / 3.6;
        double direction = Difference38(Heading38(s), road);
        var kin = g.Motion(s);
        var rotation = new Vec215(Math.Cos(road), Math.Sin(road), 0);
        var left = new Vec215(-rotation.Y, rotation.X, 0);
        Vec215 roadAngular = new(s.Angular.Dot(rotation), s.Angular.Dot(left), s.Angular.Z);
        return new(motion.Flight.Distances[index], s.Time, mark.Road.OffsetMeters, speed * Math.Cos(direction),
            speed * Math.Sin(direction), direction, Difference38(s.Pose.Yaw, road), s.Pose.Pitch, s.Pose.Roll,
            kin.BodyRate, roadAngular, s.Rider, s.RiderMomentum, kin.RiderRate, s.Crank, s.Wheel, s.FrontWheel,
            s.Steer, s.Leg, row.Forces.LeftFoot?.Contact.Force ?? default, row.Forces.Front.Normal,
            row.Forces.Rear.Normal, s.Command, Pending43(motion, new(s, motion.Flight.Distances[index]), true));
    }

    // The weights express scales of a track-relative terminal error for ranking. They do
    // not alter the plant or promote a soft state match over a physical clearance gate.
    private static double SeamError43(Phase43 a, Phase43 b)
    {
        double pending = Math.Abs(a.Pending.Length - b.Pending.Length);
        for (int i = 0; i < Math.Min(a.Pending.Length, b.Pending.Length); i++)
        {
            var p = a.Pending[i];
            var q = b.Pending[i];
            pending += Math.Abs(p.Time - q.Time) / ReactionSeconds216 + CommandGap43(p.Command, q.Command);
        }
        return Math.Abs(a.Offset - b.Offset) + .30 * Math.Abs(a.RoadSpeed - b.RoadSpeed)
            + .35 * Math.Abs(a.SideSpeed - b.SideSpeed) + 2 * Math.Abs(Difference38(a.FrameYaw, b.FrameYaw))
            + 3 * Math.Abs(Difference38(a.Direction, b.Direction)) + 2 * Math.Abs(a.Roll - b.Roll)
            + Math.Abs(a.Pitch - b.Pitch) + .35 * (a.BodyRate - b.BodyRate).Norm
            + .005 * (a.RoadAngular - b.RoadAngular).Norm + 2 * (a.Rider - b.Rider).Norm
            + .03 * (a.RiderMomentum - b.RiderMomentum).Norm + .10 * Math.Abs(a.Crank - b.Crank)
            + .03 * Math.Abs(a.RearWheel - b.RearWheel) + .03 * Math.Abs(a.FrontWheel - b.FrontWheel)
            + .5 * Math.Abs(a.Steer - b.Steer) + Math.Abs(a.Leg.Extension - b.Leg.Extension)
            + .015 * Math.Abs(a.Leg.Momentum - b.Leg.Momentum) + .002 * (a.FootForce - b.FootForce).Norm
            + .10 * CommandGap43(a.Active, b.Active) + .06 * pending;
    }

    private static double CommandGap43(Command215 a, Command215 b) =>
        Math.Abs(a.Bike.Drive - b.Bike.Drive) + Math.Abs(a.Bike.SteeringRadians - b.Bike.SteeringRadians)
        + (a.Target - b.Target).Norm + Math.Abs(a.LeftFoot - b.LeftFoot);

    private static Score40 Score43(Lab38 lab, Geometry215 g, Fork40 motion, int id = 0, Decision40[]? decisions = null) =>
        MeasureLink40(lab, g, new(SetupBaseline42() with { Id = id }, motion.Seed, motion.Seed,
            [], motion.Tape, motion.Flight, decisions ?? []));

    private static double PathLength43(Fork40 motion) => motion.Flight.Steps.Sum(x =>
        (x.After.Center - x.Before.Center).Plane.Norm);

    private static Fork40 Read223(Lab38 lab, string path, string name) => ReadMotion42(lab, path, name);

    private static void SearchManeuver43(Lab38 lab, string output)
    {
        var g = lab.Geometry(true);
        double entry = lab.Bend.NextEntry - lab.Bend.Entry;
        double secondExit = Map199.At(2, 1).Exit - lab.Bend.Entry;
        var parent = Read223(lab, ArchivePath43(lab, "speedway222/qualification/nominal"), "H41 policy 58");
        var fast = Read223(lab, ArchivePath43(lab, "speedway222/frontier/speed-frontier"), "H41 95.63 km/h");
        var incumbent = parent;
        var winners = new List<SearchResult43>();
        Save(Path.Combine(output, "shoot-baselines.json"), new
        {
            Nominal = Score43(lab, g, parent), Frontier = Score43(lab, g, fast),
            Finish = secondExit + 22, SecondEntry = entry, Selection = "linked gates, elapsed time, recovery reserve"
        });

        foreach (char stage in "ABCD")
        {
            var clock = Stopwatch.StartNew();
            using var process = Process.GetCurrentProcess();
            var cpu = process.TotalProcessorTime;
            var parents = new[] { incumbent, fast }.DistinctBy(x => x.Name).ToDictionary(x => x.Name);
            var proposals = Enumerable.Range(0, Proposals43)
                .Select(i => Plan43(lab, incumbent.Name, fast.Name, stage, i)).ToArray();
            var tested = proposals.AsParallel().WithDegreeOfParallelism(Parallel43).Select(p =>
                Shoot43(lab, g, parents[p.Parent], p, secondExit + 22, exact: false)).ToArray();
            var exactIds = tested.OrderBy(x => x, Comparer<SearchResult43>.Create(Rank43)).Take(5).Select(x => x.Plan.Id)
                .Concat(tested.Where(x => x.Plan.StraightBand.HasValue)
                    .GroupBy(x => x.Plan.StraightBand!.Value).Select(b => b.OrderByDescending(x => x.Score.Linked)
                        .ThenBy(x => Math.Abs(x.Score.StraightPeak - b.Key)).ThenByDescending(x => x.ManeuverDistance)
                        .ThenBy(x => x.Plan.Id).First().Plan.Id)).ToHashSet();
            var exact = proposals.Where(p => exactIds.Contains(p.Id)).AsParallel().WithDegreeOfParallelism(Parallel43)
                .Select(p => Prove43(lab, g, parents[p.Parent], p, secondExit + 22)).ToArray();
            var best = exact.OrderBy(x => x.Result, Comparer<SearchResult43>.Create(Rank43)).First();
            var old = Score43(lab, g, incumbent);
            bool promoted = Physical43(best.Result) && best.Result.Score.Linked &&
                best.Result.Score.TotalSeconds < old.TotalSeconds - .0001;
            if (promoted)
            {
                incumbent = best.Motion with { Name = $"H42 stage {stage} #{best.Result.Plan.Id}" };
                winners.Add(best.Result);
                WriteEvidence39(lab, output, $"shoot-{stage}-accepted", g, incumbent.Seed, incumbent.Tape, incumbent.Flight);
            }
            if (stage == 'B')
            {
                var quickest = exact.Where(x => x.Result.Score.Linked).OrderByDescending(x => x.Result.Score.StraightPeak)
                    .ThenBy(x => x.Result.Score.TotalSeconds).FirstOrDefault();
                if (quickest is not null)
                    WriteEvidence39(lab, output, "shoot-B-speed", g, quickest.Motion.Seed, quickest.Motion.Tape, quickest.Motion.Flight);
            }
            Save(Path.Combine(output, $"stage-{stage}.json.gz"), new
            {
                Stage = stage, Initial = old, Promoted = promoted, Selected = best.Result,
                Screening = tested.OrderBy(x => x.Plan.Id), Verified = exact.Select(x => x.Result).OrderBy(x => x.Plan.Id),
                Incumbent = incumbent.Name, CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds,
                WallSeconds = clock.Elapsed.TotalSeconds, NativeWorlds = 0
            }, true);
            Console.WriteLine($"Study 223 stage {stage}: {tested.Count(x => x.Score.Linked)}/{tested.Length} screened linked, " +
                $"{exact.Count(x => x.Result.Score.Linked)}/{exact.Length} exact linked, promoted {promoted}, " +
                $"best {best.Result.Score.TotalSeconds:F3} s at {best.Result.Score.StraightPeak:F2} km/h.");
        }
        WriteEvidence39(lab, output, "shoot-selected", g, incumbent.Seed, incumbent.Tape, incumbent.Flight);
        Save(Path.Combine(output, "shoot-summary.json"), new
        {
            Incumbent = incumbent.Name, Improved = winners.Count > 0, Winners = winners,
            Score = Score43(lab, g, incumbent), PathMeters = PathLength43(incumbent), NativeWorlds = 0
        });
    }

    private static bool Physical43(SearchResult43 x) => x.Score.Swept >= .5 && x.Score.Hard > 0 &&
        x.Score.EnergyError < 1 && x.Score.PeakForce <= 700.00001 && x.Score.PeakPower <= 350.00001;

    private static int Rank43(SearchResult43 a, SearchResult43 b)
    {
        int complete = (Physical43(b) && b.Score.Linked).CompareTo(Physical43(a) && a.Score.Linked);
        if (complete != 0) return complete;
        int rank = a.Score.Linked && b.Score.Linked
            ? Nullable.Compare(a.Score.TotalSeconds, b.Score.TotalSeconds) : b.ManeuverDistance.CompareTo(a.ManeuverDistance);
        if (rank != 0) return rank;
        rank = b.Score.Hard.CompareTo(a.Score.Hard);
        return rank != 0 ? rank : a.Plan.Id.CompareTo(b.Plan.Id);
    }

    private static Proposal43 Plan43(Lab38 lab, string timeParent, string speedParent, char stage, int i)
    {
        double bendEnd = lab.Bend.Length;
        double next = lab.Bend.NextEntry - lab.Bend.Entry;
        double nextExit = Map199.At(2, 1).Exit - lab.Bend.Entry;
        int id = 100 * (stage - 'A' + 1) + i;
        int section = i / 8, variant = i % 8;
        double scale = variant is 0 or 1 ? .25 : variant is 2 or 3 ? .55 : 1;
        double sign = variant % 2 == 0 ? 1 : -1;
        double bias(int dimension) => 2 * Halton43(id + 1, dimension) - 1;
        Vec215 Rider(double size) => new(size * bias(5), size * bias(7), size * bias(11));
        Knot43 Mod(double station, double radius, double drive, double steer, double body, double foot) =>
            new(station, radius, drive, steer, Rider(body), foot);
        if (stage == 'A')
        {
            double s = new[] { -9d, 12, 31, 50, 69, 86, bendEnd - 1, bendEnd + 9 }[section];
            var k = Mod(s, variant >= 6 ? 12 : 6 + 2 * (section % 3), sign * scale * .14,
                variant < 2 ? 0 : bias(3) * scale * .013, .016 * scale, bias(13) * .08 * scale);
            if (variant == 6 && section is >= 1 and <= 6) k = k with { AbsoluteDrive = true, Drive = 1, Radius = 5.5 };
            return new(id, timeParent, null, [k]);
        }
        if (stage == 'B')
        {
            double band = new[] { 95d, 100, 105, 110, 115, 120 }[i % 6];
            int group = i / 6;
            if (group < 4)
            {
                double station = bendEnd - 7 + group * 3.5;
                return new(id, band > 95 ? speedParent : timeParent, band,
                    [Mod(station, 8, .045 * bias(2), .007 * bias(3), .011, .035 * bias(13)),
                     Mod(bendEnd + 23 + 2 * group, 13 + (band - 95) * .80,
                         Math.Clamp((band - 91) / 40, .07, .86), .012 * bias(3), .009, .04 * bias(13)),
                     Mod(next - 21 + group * 4, 15, -.14 * (group % 2), .018 * bias(17), .018, .08 * bias(19))]);
            }
            if (group < 8)
            {
                double drive = band >= 110 ? 1 : .50 + (band - 95) * .027;
                return new(id, speedParent, band,
                    [Mod(bendEnd + 13, 18, .09, .009 * bias(3), .013, .04 * bias(13)),
                     Mod(bendEnd + 27 + (group - 4) * 5, 17 + (band - 95) * .50, drive, .004 * bias(17),
                         .012, .05 * bias(19)) with { AbsoluteDrive = true },
                     Mod(next - 21 + (group - 4) * 6, 14, -.28 * (group % 2), .021 * bias(23), .023, .09 * bias(13))]);
            }
            return new(id, section % 2 == 0 ? timeParent : speedParent, band,
                [Mod(bendEnd + 12 + 5 * (i % 8), 5 + group, .08 * bias(2), .006 * bias(3), .007, .025 * bias(13))]);
        }
        if (stage == 'C')
        {
            double s = next + new[] { -18d, -4, 13, 30, 49, 67, 86, nextExit - next + 8 }[section];
            var k = Mod(s, variant < 4 ? 5 : 11, sign * scale * .12, bias(3) * scale * .009,
                .012 * scale, bias(13) * .07 * scale);
            if (variant == 6 && section is >= 2 and <= 6) k = k with { AbsoluteDrive = true, Drive = 1, Radius = 4.5 };
            return new(id, variant == 7 ? speedParent : timeParent, null, [k]);
        }
        double a = new[] { 23d, 56, 82, bendEnd + 11, next - 31, next - 8, next + 22, next + 65 }[section];
        double b = new[] { next - 28, next + 12, next + 61, nextExit + 9, next + 30, next + 72, nextExit + 12, nextExit + 14 }[section];
        return new(id, timeParent, null,
            [Mod(a, 8, sign * .075 * scale, .0035 * bias(3) * scale, .006 * scale, .04 * bias(13) * scale),
             Mod(b, 8 + variant % 3, .13 * bias(2) * scale, .005 * bias(17) * scale, .011 * scale, .07 * bias(19) * scale)]);
    }

    private static double Halton43(int i, int b)
    {
        double value = 0, scale = 1;
        for (; i > 0; i /= b) { scale /= b; value += (i % b) * scale; }
        return value;
    }

    private static double Weight43(Knot43 knot, double phase)
    {
        double s = Math.Abs(phase - knot.Station) / knot.Radius;
        return s >= 1 ? 0 : s <= knot.Flat ? 1 : (1 + Math.Cos(Math.PI * (s - knot.Flat) / (1 - knot.Flat))) / 2;
    }

    private static Command215 Bounded43(double drive, double steer, Vec215 rider, double foot) =>
        new(new(Steering39(steer), (float)Math.Clamp(drive, 0, 1)),
            new(Math.Clamp(rider.X, -Command215.MaximumForwardTarget, Command215.MaximumForwardTarget),
                Math.Clamp(rider.Y, -Command215.MaximumLateralTarget, Command215.MaximumLateralTarget),
                Math.Clamp(rider.Z, -Command215.MaximumVerticalTarget, Command215.MaximumVerticalTarget)))
                { LeftFoot = Math.Clamp(foot, 0, 1) };

    private static Command215 KnotDrive43(Command215 center, Knot43[] knots, double phase)
    {
        double drive = center.Bike.Drive, angle = center.Bike.SteeringRadians, foot = center.LeftFoot;
        var rider = center.Target;
        foreach (var knot in knots)
        {
            double w = Weight43(knot, phase);
            drive += (knot.AbsoluteDrive ? knot.Drive - center.Bike.Drive : knot.Drive) * w;
            angle += knot.Steering * w;
            rider += knot.Rider * w;
            foot += knot.Foot * w;
        }
        return Bounded43(drive, angle, rider, foot);
    }

    private static int CommandState43(Fork40 source, double time)
    {
        int low = 0, high = source.Flight.Steps.Length;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (source.Flight.Steps[mid].After.Time < time - 1e-9) low = mid + 1;
            else high = mid;
        }
        return Math.Min(low, source.Flight.Steps.Length - 1);
    }

    private static (Activation215[] Tape, int First) Commands43(Fork40 source, Knot43[] knots)
    {
        var tape = (Activation215[])source.Tape.Clone();
        int first = tape.Length;
        for (int j = 0; j < tape.Length; j++)
        {
            var a = tape[j];
            int row = CommandState43(source, a.Requested);
            double station = a.Requested < source.Flight.Steps[0].After.Time - 1e-9
                ? source.Seed.Distance : source.Flight.Distances[row];
            var cmd = KnotDrive43(a.Command, knots, station);
            if (cmd == a.Command) continue;
            tape[j] = a with { Command = cmd };
            first = Math.Min(first, j);
        }
        return (tape, first);
    }

    private static SearchResult43 Shoot43(Lab38 lab, Geometry215 g, Fork40 parent, Proposal43 p, double end, bool exact) =>
        Prove43(lab, g, parent, p, end, exact).Result;

    private static Shooting43 Prove43(Lab38 lab, Geometry215 g, Fork40 parent, Proposal43 p, double end, bool exact = true)
    {
        var (tape, first) = Commands43(parent, p.Knots);
        return Integrate43(lab, g, parent, p, tape, first, end, exact);
    }

    private static Shooting43 Integrate43(Lab38 lab, Geometry215 g, Fork40 parent, Proposal43 p, Activation215[] tape,
        int first, double end, bool exact, bool periodic = false)
    {
        if (first >= tape.Length)
            return new(new(p, Score43(lab, g, parent, p.Id), parent.Flight.Final.Distance, 0, PathLength43(parent),
                "unchanged executable controls", parent.Flight.Steps.Length, 0, exact), parent);
        double forkTime = tape[first].Requested;
        int i = CommandState43(parent, forkTime);
        while (i >= 0 && parent.Flight.Steps[i].After.Time > forkTime + 1e-9) i--;
        var checkpoint = i < 0 ? parent.Seed : new(parent.Flight.Steps[i].After, parent.Flight.Distances[i]);
        if (checkpoint.State.Next > first)
            throw new InvalidOperationException("Shooting fork modified already activated commands.");
        double finalTime = Math.Max(parent.Flight.Final.State.Time, tape[^1].Requested) + (periodic ? 5 : 1);
        var suffix = ReplayTape38(lab, g, checkpoint, tape, finalTime, exact: exact, endDistance: end);
        var trace = suffix with
        {
            Steps = [.. parent.Flight.Steps[..(i + 1)], .. suffix.Steps],
            Distances = [.. parent.Flight.Distances[..(i + 1)], .. suffix.Distances],
            Boundary = Math.Min(suffix.Boundary, parent.Flight.Boundary),
            Ground = Math.Min(suffix.Ground, parent.Flight.Ground),
            Stop = suffix.Final.Distance >= end ? "completed linked horizon" : suffix.Stop == "verified interval"
                ? "command horizon ended before the checkpoint" : suffix.Stop
        };
        var motion = new Fork40(p.Id.ToString(), parent.Seed, trace, tape);
        double phase = periodic && trace.Final.Distance >= Frame.LapMeters ? SeamError43(AtPhase43(lab, g, motion, 0),
            AtPhase43(lab, g, motion, Frame.LapMeters)) : 0;
        return new(new(p, Score43(lab, g, motion, p.Id), trace.Final.Distance, phase, PathLength43(motion), trace.Stop,
            i + 1, suffix.Steps.Length, exact), motion);
    }
}
