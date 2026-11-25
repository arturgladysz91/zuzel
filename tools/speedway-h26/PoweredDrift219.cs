
using System.Diagnostics;
using System.Numerics;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Base38 = "7385987c824be319e157ea836c8f045aa6a12258";
    private const double MeasuredSlide38 = Math.PI / 30;
    private const double PoweredDrive38 = .25;
    private const double PoweredSlip38 = .06;
    private const double Cadence38 = H36ControlPeriod;
    private static readonly float[] Drives38 = [0, .25f, .50f, .70f, .85f, 1];
    private static readonly SpatialBike36 Compact38 = new(.70, .095, -.12, .28, .90);
    private static readonly SpatialBike36 Large38 = new(.90, .135, -.18, .28, 1.10);

    private sealed class Lab38(string root, Fixture216 fixture, BendVisit bend, RacingLineGeometry track, CornerCourse216 course)
    {
        internal string Root => root;
        internal Fixture216 Fixture => fixture;
        internal BendVisit Bend => bend;
        internal RacingLineGeometry Track => track;
        internal CornerCourse216 Course => course;

        internal Geometry215 Geometry(bool leg, SpatialBike36? envelope = null) =>
            new(fixture.Base, Axes215.All, leg ? new Leg216() : null, envelope ?? H36Footprint);

        internal UnifiedModel215 Engine(Geometry215 g) => fixture.Engine(g, NominalSupport216, Actuator215.Limited, true);
    }

    private sealed record Archive38(string Id, Trace36 Run, double[] Distances, Geometry215 Geometry, Preparation37? Plan)
    {
        internal int At(double distance)
        {
            int i = Array.BinarySearch(Distances, distance);
            return Math.Clamp(i >= 0 ? i : ~i, 0, Distances.Length - 1);
        }
        internal Step215 Sample(double distance) => Run.Trace[At(distance)];
        internal double ActualDistance(int i) => Distances[i];
    }

    private sealed record Terminal218( StraightTuning37 Selection, Perturbation37[] Robustness);

    private static void PoweredStudy219(string workspace, string output, string mode)
    {
        if (mode is not ("all" or "audit" or "baseline" or "maps" or "matched" or "search" or "recourse"
            or "search-history" or "recourse-history" or "recover"))
            throw new ArgumentException("Use all, audit, baseline, maps, matched, search, recourse, recover, or a -history mode.");
        string root = Path.GetFullPath(workspace);
        var (_, spec) = CourseFixture208(root, "C");
        var fixture = new Fixture216(spec);
        var track = new RacingLineGeometry(Frame);
        var lab = new Lab38(root, fixture, Map199.At(2, 0), track,
            new CornerCourse216(track, RootFor210(root, "C").State.State.Rider));
        if (mode == "recover")
        {
            StraightEvidence38(lab, output);
            return;
        }
        if (mode is "recourse" or "recourse-history")
        {
            PoweredRecourse38(lab, output, mode == "recourse-history");
            return;
        }
        if (mode is "all" or "audit" or "baseline")
            ReplayBaselines38(lab, output);
        if (mode == "baseline") return;
        if (mode is "all" or "audit" or "maps")
            ForceInvestigation38(lab, output);
        if (mode is "audit" or "maps") return;
        var archives = new[] { ReadArchive38(lab, "70"), ReadArchive38(lab, "77"), ReadArchive38(lab, "78") };
        if (mode is "all" or "matched")
        {
            PostureStudy38(lab, output, archives);
            MatchedStudy38(lab, output, archives);
        }
        if (mode is "all" or "search" or "search-history")
            WholeStudy38(lab, output, archives, mode == "search-history");
    }

    private static Archive38 ReadArchive38(Lab38 lab, string id, bool exact = false)
    {
        string path = id switch
        {
            "70" => "speedway217/h36/trajectory-70.trajectory.json.gz",
            "77" => "speedway217/h36/pace-trajectory-80.trajectory.json.gz",
            "78" => "speedway218/exit-80-proposal-6.json.gz",
            "91" => "speedway218/archive-91.json.gz",
            "89" => "speedway218/speed-90-frontier.json.gz",
            "88" => "speedway218/trim-90-frontier.json.gz",
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
        var file = Path.Combine(lab.Root, "data/experiments", path);
        var tape = Read199<Tape37>(file);
        var g = lab.Geometry(tape.Summary.Tune.Leg);
        double start = lab.Bend.Entry - tape.Summary.Start.Reference;
        var run = RunH36(tape.Summary.Id, id, tape.Summary.NominalEntryKph, tape.Summary.Tune, g,
            lab.Engine(g), tape.Seed, lab.Bend, lab.Track, lab.Course, 0, tape.Commands, exact, startingMeters: start);
        if (!tape.Summary.Balanced && run.Trace.Length >= tape.Summary.Steps)
        {
            // A forecast abstention has no next command. A held command could
            // carry the solver past that point, so certify the published prefix.
            var prefix = run.Trace.Take(tape.Summary.Steps).ToArray();
            var last = prefix[^1].After;
            if (last.Time == tape.Summary.Finish.Time && last.Pose.Roll == tape.Summary.Finish.Roll)
                run = run with { Summary = tape.Summary, Trace = prefix };
        }
        if (run.Summary.Entry != tape.Summary.Entry || run.Summary.Finish != tape.Summary.Finish ||
            run.Trace.Length != tape.Summary.Steps)
        {
            // Near a conservative screening frontier the archive used full shape
            // projection. Replay it with the same detail before comparing states.
            if (!exact) return ReadArchive38(lab, id, true);
            throw new InvalidOperationException($"Archive {id} did not reproduce: {run.Summary.Stop}, {run.Trace.Length}.");
        }
        if (path.Contains(".trajectory."))
            CheckArchive37(file, run);
        var marks = run.Trace.Select(s => lab.Course.Mark(s.After, g).Road.ReferenceMeters).ToArray();
        double previous = lab.Course.Mark(run.Seed, g).Road.ReferenceMeters, distance = -start;
        for (int i = 0; i < marks.Length; i++)
        {
            double current = marks[i];
            distance += Frame.UnwrapDelta(previous, current);
            marks[i] = distance;
            previous = current;
        }
        Preparation37? plan = null;
        if (path.Contains("speedway218"))
        {
            var evidence = Read199<Evidence37>(file);
            plan = evidence.Strategy;
            foreach (var (step, accepted) in run.Trace.Zip(evidence.Steps))
                if (step.After.Time != accepted.Time || step.After.Pose.Roll != accepted.Roll ||
                    step.After.Pose.Yaw != accepted.Yaw || step.After.Steer != accepted.Steer ||
                    step.Before.Command.Bike.Drive != accepted.ActiveDrive ||
                    step.Forces.Front.Normal != accepted.FrontNormal || step.Forces.Rear.Normal != accepted.RearNormal)
                    throw new InvalidOperationException($"Archive {id} diverged at {accepted.Time:R}s.");
        }
        return new(id, run, marks, g, plan);
    }

    private static void ReplayBaselines38(Lab38 lab, string output)
    {
        var clock = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        TimeSpan cpu = process.TotalProcessorTime;
        string[] names = ["70", "77", "78", "91", "89", "88"];
        var archives = new Archive38[names.Length];
        Parallel.For(0, names.Length, new ParallelOptions { MaxDegreeOfParallelism = 3 }, i =>
            archives[i] = ReadArchive38(lab, names[i]));
        var a = archives[2];
        string data = Path.Combine(lab.Root, "data/experiments/speedway218");
        var selected = ReadPlain214<Terminal218>(Path.Combine(data, "terminal-80.json"));
        var continuation = CheckRunout37(lab.Fixture, a.Geometry, lab.Track, lab.Course, lab.Bend,
            a.Run, true, 2.10, selected.Selection);
        var archived = Read199<Runout37>(Path.Combine(data, "terminal-80-runout.json.gz"));
        if (!continuation.Stable || continuation.Final != archived.Final ||
            !continuation.Trace.Select(x => x.After).SequenceEqual(archived.Trace.Select(x => x.After)))
            throw new InvalidOperationException("Selected #218 straight continuation did not reproduce.");
        var results = ReadPlain214<StageTrials37>(Path.Combine(data, "stage-80.json"));
        var setup = results.Outcomes.Single(x => x.Setup.Id == a.Run.Summary.Id).Setup;
        var straight = SteadyCircle215(lab.Engine(a.Geometry), a.Geometry, lab.Fixture.Rest(a.Geometry, NominalSupport216),
            40 / 3.6, 0, lab.Fixture.Release + 1, lab.Fixture.Ratio, balanceRider: true);
        var held = Perturbations37(lab.Fixture, a.Geometry, lab.Track, lab.Course, lab.Bend, setup, a.Run,
            straight.State.Command.Bike.Drive, true, 2.10, selected.Selection);
        if (!held.SequenceEqual(selected.Robustness))
            throw new InvalidOperationException("The four reported #218 perturbations did not reproduce.");
        Save(Path.Combine(output, "baseline.json"), new
        {
            Parent = Base38, NativeWorlds = 0,
            Arcs = archives.Select(a => new
            {
                a.Id, a.Run.Summary, Metrics = Metrics37(a.Run, a.Geometry.Base.Mass, lab.Bend),
                Commands = a.Run.Commands.Length, States = a.Run.Trace.Length,
                FrontierMeters = a.Distances[^1]
            }),
            Straight = new { continuation.InitialKph, continuation.FinalKph, continuation.Stable,
                Steps = continuation.Trace.Length, State = continuation.Final },
            Held = held, ActualSteps = archives.Sum(a => a.Run.Trace.Length) + continuation.Trace.Length + held.Sum(r => r.ActualSteps),
            WallSeconds = clock.Elapsed.TotalSeconds, CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds
        });
        Console.WriteLine("H38 baseline replay and four published perturbations reproduced.");
    }

    private static double LateralAcceleration38(Step215 row, double mass)
    {
        var velocity = row.Midpoint.Momentum.Plane / mass;
        var acceleration = (row.After.Momentum - row.Before.Momentum).Plane / (mass * Duration215(row));
        return velocity.Norm > 1e-6 ? velocity.Cross(acceleration) / velocity.Norm : 0;
    }

    private static double SlipAngle38(State215 s)
    {
        var v = s.Momentum;
        return Math.IEEERemainder(s.Pose.Yaw - Math.Atan2(v.Y, v.X), 2 * Math.PI);
    }

    private static bool Slide38(Step215 row) =>
        row.Forces.Rear.Normal > 20 && Math.Abs(SlipAngle38(row.After)) >= MeasuredSlide38;

    private static bool Powered38(Step215 row) =>
        Slide38(row) && row.Forces.RearSlip >= PoweredSlip38 && row.Before.Command.Bike.Drive >= PoweredDrive38;
}
