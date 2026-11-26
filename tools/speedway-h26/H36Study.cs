
using System.Diagnostics;
using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double H36ControlPeriod = .060;
    private const double H36StartBeforeEntry = 51;
    private const double H36CheckpointBeforeEntry = 33;
    private static readonly SpatialBike36 H36Footprint = new();
    private sealed record Tune36(double Lane, double Lookahead, double RollGain, double Damping,
        double Fore, double Side, double Vertical, double Apex, double Boot, bool Leg, double FullDriveExitMeters = 0,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        double ApexPaceDropKph = 0,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        double ApproachSpeedCorrectionKph = 0);
    private sealed record Sample36(double Time, double Reference, double Kph, double Roll, double YawRate, double Offset);
    private sealed record Corner36(string Case, int Id, double NominalEntryKph, Tune36 Tune, string Stop, bool Balanced,
        bool SustainedSlideSample, Sample36 Start, Sample36? Approach, Sample36? Entry, Sample36? Apex,
        Sample36? Exit, Sample36 Finish, double MinKph, double MinimumSweep, double GroundMargin,
        double PeakUtilization, double PeakNormalizedWheelspin, double MinFrontLoad, double MinRearLoad,
        double PeakMuscleForce, double PeakMusclePower, double PeakBootForce, double FullDriveInBend,
        double SlidingSeconds, double LongestSlide, double MaxEnergyError, int Steps, int ForecastSteps);
    private sealed record Trace36(Corner36 Summary, State215 Seed, Activation215[] Commands, Step215[] Trace)
    {
        [System.Text.Json.Serialization.JsonIgnore]
        internal double[]? ReferenceSamples { get; init; }
    }
    private sealed record Equilibrium36(double Speed, double Radius, double Residual, double LateralAcceleration,
        double Roll, double TyreUtilization, double FrontRollingForce, double Steering, double FrontYawBand,
        double FrontRollBand, double Drift, double Rpm, double Drive, double Seconds);
    private sealed record Forecast36(State215 State, Forces215? Forces, int Steps, string? Error);
    private readonly record struct RollReference36(double Curvature, double Roll, double PreviewMeters,
        double SpeedTargetKph, float Drive, Vec215 Rider, double Boot);
    private sealed record Stage36(double RequestedKph, int Proposals, int Balanced, int Sliding, string Outcome,
        int ActualSteps, int ForecastSteps, Corner36? Best, Corner36? HighestEntry, Corner36? FullDriveWitness,
        double CpuSeconds, double WallSeconds);

    private static void H36Study(string root, string output, string mode)
    {
        if (mode is not ("all" or "geometry" or "pilot" or "diagnose" or "continue" or "pace" or "trim" or "prepare"))
            throw new ArgumentException("Use all, geometry, pilot, diagnose, continue, pace, trim or prepare.");
        bool trim = mode == "trim";
        bool prepare = mode == "prepare";
        bool paceRecovery = mode is "pace" or "trim" || prepare;
        bool resumes = mode == "continue" || paceRecovery;
        string prefixName = prepare ? "prepare-" : trim ? "trim-" : paceRecovery ? "pace-" : "";
        var (_, spec) = CourseFixture208(root, "C");
        var fixture = new Fixture216(spec);
        var visit = Map199.At(2, 0);
        var track = new RacingLineGeometry(Frame);
        var course = new CornerCourse216(track, RootFor210(root, "C").State.State.Rider);
        using var process = Process.GetCurrentProcess();
        if (mode == "diagnose")
        {
            H36Diagnose(root, output, fixture, course, track);
            return;
        }
        if (mode is "all" or "geometry")
        {
            H36Geometry(root, output, fixture, course, track);
            if (mode == "geometry") return;
        }
        string protocolPath = Path.Combine(output, "protocol-h36.json");
        if (!resumes) Save(protocolPath, new
        {
            AuditBase = "7551d6ce7948b6c53d45091409bb39a30cd51181", visit,
            H36StartBeforeEntry, H36CheckpointBeforeEntry, H36ControlPeriod, ReactionSeconds216,
            StepSeconds216, SpatialBike36.RequiredSweptMeters, Footprint = H36Footprint, Actuator = Actuator215.Limited,
            Steering = Steering217, fixture.Base, Gravity216, NativeWorlds = 0,
            Qualification = "MODEL balanced continuous bend + 8 m. Official strict and sporting acceptance are separate."
        });

        string curriculumPath = Path.Combine(output, "curriculum-h36.json");
        var recorded = resumes ? ReadPlain214<Stage36[]>(curriculumPath).ToList() : new List<Stage36>();
        string priorName = "";
        string pacePath = Path.Combine(output, "pace-h36.json");
        string trimPath = Path.Combine(output, "trim-h36.json");
        string preparationPath = Path.Combine(output, "prepare-h36.json");
        var recourses = new[] { (trimPath, "trim-"), (preparationPath, "prepare-"), (pacePath, "pace-") };
        if (mode == "continue" && recorded.LastOrDefault()?.Outcome != "balanced at requested band")
        {
            foreach (var (path, stem) in recourses)
            {
                if (!File.Exists(path)) continue;
                var recourse = ReadPlain214<Stage36[]>(path).Single();
                if (recourse.RequestedKph != recorded[^1].RequestedKph || recourse.Outcome != "balanced at requested band") continue;
                recorded[^1] = recourse;
                priorName = stem;
                break;
            }
        }
        if (paceRecovery && (recorded.Count < 2 || recorded[^1].Outcome == "balanced at requested band"))
            throw new InvalidOperationException("pace recovery requires a previous balanced stage and a failed speed band");
        if (mode == "continue" && recorded.Count > 0 && recorded[^1].Outcome != "balanced at requested band")
            throw new InvalidOperationException("the preceding speed band has no qualified continuation");
        var stages = paceRecovery ? new List<Stage36>() : recorded;
        if (paceRecovery) curriculumPath = prepare ? preparationPath : trim ? trimPath : pacePath;
        var equilibria = new List<Equilibrium36>();
        var baseline = new Tune36(6.5, 10, 24, 8, 0, 0, 0, 0, 0, false);
        var prior = recorded.LastOrDefault(row => row.Outcome == "balanced at requested band");
        if (prior is not null)
            foreach (var (path, stem) in recourses)
            {
                if (!File.Exists(path)) continue;
                var recourse = ReadPlain214<Stage36[]>(path).Single();
                if (recourse != prior) continue;
                priorName = stem;
                break;
            }
        Trace36? warm = prior is null ? null :
            Read199<Trace36>(Path.Combine(output, $"{priorName}trajectory-{prior.RequestedKph:F0}.json.gz"));
        if (stages.Count > 0 && !warm!.Summary.Balanced)
            throw new InvalidOperationException("continue requires an earlier qualified approach");
        var nominal = fixture.Geometry();
        var nominalEngine = fixture.Engine(nominal, NominalSupport216, Actuator215.Limited, true);
        var (straight, curved) = Equilibria(40, true);
        if (straight.Residual > .005 || curved.Residual > .005)
        {
            Save(Path.Combine(output, "equilibria.json"), equilibria);
            return;
        }
        var targetSpeeds = paceRecovery ? new[] { recorded[^1].RequestedKph } :
            mode == "pilot" ? new[] { 40d } : new double[] { 40, 50, 60, 70, 80, 90, 100, 110, 120 };
        double completedTarget = stages.Count > 0 ? stages[^1].RequestedKph : 0;
        foreach (double target in targetSpeeds.Where(t => t > completedTarget))
        {
            var watch = Stopwatch.StartNew();
            var cpu = process.TotalProcessorTime;
            var center = warm?.Summary.Tune ?? baseline;
            var settings = prepare ? PreparationSettings(center) : trim ? TrimSettings(target) :
                paceRecovery ? Proposals36(center with { Lane = 7.85, Lookahead = 12.5 })
                .Select((p, i) => p with { Lane = Math.Clamp(p.Lane, 6.4, 8.6), FullDriveExitMeters = 0,
                    ApexPaceDropKph = 9 + (i * 7 % 23) }).ToArray() :
                mode == "pilot" ? new[]
            {
                baseline, baseline with { Lookahead = 8 }, baseline with { Lookahead = 13 },
                baseline with { RollGain = 17, Damping = 6 }, baseline with { RollGain = 30, Damping = 10 }
            } : Proposals36(center);
            var roots = new Dictionary<bool, (Geometry215 Geometry, State215 Root, float Drive)>();
            foreach (bool useLeg in settings.Select(x => x.Leg).Distinct())
            {
                var geometry = new Geometry215(fixture.Base, Axes215.All, useLeg ? new Leg216() : null, H36Footprint);
                // Each stage replays continuously from the 40 km/h approach root. The
                // prior winning commands, full checkpoints and line seed the next search.
                var plan = useLeg
                    ? SteadyCircle215(fixture.Engine(geometry, NominalSupport216, Actuator215.Limited, true), geometry,
                        fixture.Rest(geometry, NominalSupport216), 40 / 3.6, 0, fixture.Release + 1, fixture.Ratio, balanceRider: true)
                    : straight;
                roots[useLeg] = (geometry, plan.State, plan.State.Command.Bike.Drive);
            }
            var outcomes = new Corner36[settings.Length];
            var paired = new Trace36?[2];
            Trace36? best = null;
            Trace36? fastest = null;
            Trace36? fullDrive = null;
            var sync = new object();
            Parallel.For(0, settings.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, i =>
            {
                var tuning = settings[i];
                var pair = roots[tuning.Leg];
                var previous = warm;
                bool matching = previous is not null && previous.Summary.Tune.Leg == tuning.Leg;
                var seed = matching ? previous!.Seed : Place36(pair.Root, visit.Entry - H36StartBeforeEntry, tuning.Lane);
                bool early = trim ? i < 2 : i is 1 or 2 || i % 2 == 0;
                double retainedApproach = paceRecovery ? early ? .12 : .35 : early ? .40 : .73;
                double rewind = matching ? previous!.Summary.Start.Time +
                    (previous.Summary.Approach!.Time - previous.Summary.Start.Time) * retainedApproach : -1;
                var prefix = matching ? previous!.Commands.Where(c => c.Requested < rewind).ToArray() : [];
                var engine = fixture.Engine(pair.Geometry, NominalSupport216, Actuator215.Limited, true);
                var run = RunH36(i + 1, "search", target, tuning, pair.Geometry, engine, seed, visit,
                    track, course, pair.Drive, null, false, prefix);
                outcomes[i] = run.Summary;
                if (mode != "pilot" && !trim && !prepare && i is 1 or 2) paired[i - 1] = run;
                lock (sync)
                {
                    if (best is null || Better(run.Summary, best.Summary)) best = run;
                    if (run.Summary.Balanced && (fastest is null || Faster(run.Summary, fastest.Summary))) fastest = run;
                    if (run.Summary.Balanced && run.Summary.FullDriveInBend > 0 && (fullDrive is null ||
                        run.Summary.FullDriveInBend > fullDrive.Summary.FullDriveInBend ||
                        run.Summary.FullDriveInBend == fullDrive.Summary.FullDriveInBend && run.Summary.Id < fullDrive.Summary.Id))
                        fullDrive = run;
                }
                if (mode == "pilot")
                    Console.WriteLine($"{target:F0}/{i + 1}: {run.Summary.Stop}, s={run.Summary.Finish.Reference - visit.Entry:F1} m," +
                        $" v={run.Summary.Finish.Kph:F1}, roll={run.Summary.Finish.Roll:F3}");
            });
            if (best is null) throw new InvalidOperationException("no corner proposals");
            var final = best;
            var chosen = roots[final.Summary.Tune.Leg];
            var replay = RunH36(final.Summary.Id, "replay", target, final.Summary.Tune, chosen.Geometry,
                fixture.Engine(chosen.Geometry, NominalSupport216, Actuator215.Limited, true), final.Seed, visit,
                track, course, chosen.Drive, final.Commands, true);
            var deterministic = RunH36(final.Summary.Id, "repeat", target, final.Summary.Tune, chosen.Geometry,
                fixture.Engine(chosen.Geometry, NominalSupport216, Actuator215.Limited, true), final.Seed, visit,
                track, course, chosen.Drive, final.Commands, true);
            if (!replay.Trace.Select(r => r.After).SequenceEqual(deterministic.Trace.Select(r => r.After)))
                throw new InvalidOperationException("non-deterministic continuous qualification replay");
            Save(Path.Combine(output, $"{prefixName}stage-{target:F0}.json"), outcomes);
            Save(Path.Combine(output, $"{prefixName}trajectory-{target:F0}.json.gz"), replay, true);
            int extraSteps = 0;
            Corner36? highest = replay.Summary.Balanced ? replay.Summary : null;
            Corner36? driveWitness = replay.Summary.Balanced && replay.Summary.FullDriveInBend > 0 ? replay.Summary : null;
            foreach (var boot in paired.OfType<Trace36>())
            {
                var pairedTrace = Recheck(boot, "paired-replay");
                Save(Path.Combine(output, $"{prefixName}foot-pair-{target:F0}-{(boot.Summary.Tune.Boot > 0 ? "active" : "parked")}.json.gz"),
                    pairedTrace, true);
            }
            if (fastest is { } high && high.Summary.Id != final.Summary.Id)
            {
                var receipt = Recheck(high, "highest-entry-replay");
                Save(Path.Combine(output, $"{prefixName}highest-entry-{target:F0}.json.gz"), receipt, true);
                if (receipt.Summary.Balanced) highest = receipt.Summary;
            }
            if (fullDrive is { } driven && driven.Summary.Id != final.Summary.Id)
            {
                var receipt = Recheck(driven, "full-drive-replay");
                Save(Path.Combine(output, $"{prefixName}full-drive-{target:F0}.json.gz"), receipt, true);
                if (receipt.Summary.Balanced) driveWitness = receipt.Summary;
            }
            var status = replay.Summary.Balanced
                ? replay.Summary.Entry is { } e && Math.Abs(e.Kph - target) <= 2 ? "balanced at requested band" : "balanced outside requested band"
                : replay.Summary.Stop;
            stages.Add(new(target, settings.Length, outcomes.Count(x => x.Balanced), outcomes.Count(x => x.SustainedSlideSample),
                status, outcomes.Sum(x => x.Steps) + replay.Summary.Steps + deterministic.Summary.Steps + extraSteps,
                outcomes.Sum(x => x.ForecastSteps), replay.Summary, highest, driveWitness,
                (process.TotalProcessorTime - cpu).TotalSeconds, watch.Elapsed.TotalSeconds));
            Console.WriteLine($"{target:F0} km/h: {status}, {replay.Summary.Finish.Reference - visit.Entry:F2} m, " +
                $"entry {replay.Summary.Entry?.Kph:F2}, exit {replay.Summary.Exit?.Kph:F2}, {settings.Length} proposals");
            string journal = curriculumPath + ".pending";
            File.WriteAllText(journal, System.Text.Json.JsonSerializer.Serialize(stages, Json));
            File.Move(journal, curriculumPath, overwrite: true);
            if (!replay.Summary.Balanced || status != "balanced at requested band") break;
            warm = replay;

            Trace36 Recheck(Trace36 original, string label)
            {
                var pair = roots[original.Summary.Tune.Leg];
                var result = RunH36(original.Summary.Id, label, target, original.Summary.Tune, pair.Geometry,
                    fixture.Engine(pair.Geometry, NominalSupport216, Actuator215.Limited, true), original.Seed,
                    visit, track, course, pair.Drive, original.Commands, true);
                extraSteps += result.Summary.Steps;
                return result;
            }
        }
        if (!File.Exists(Path.Combine(output, "equilibria.json"))) Save(Path.Combine(output, "equilibria.json"), equilibria);

        (Balance215 Straight, Balance215 Curved) Equilibria(double kph, bool retain)
        {
            double speed = kph / 3.6;
            double radius = Frame.RoadAt((visit.Entry + visit.Exit) / 2, baseline.Lane).RadiusMeters;
            var rest = fixture.Rest(nominal, NominalSupport216);
            var line = SteadyCircle215(nominalEngine, nominal, rest, speed, 0, fixture.Release + 1, fixture.Ratio, balanceRider: true);
            var circle = SteadyCircle215(nominalEngine, nominal, rest, speed, speed / radius,
                fixture.Release + 1, fixture.Ratio, balanceRider: true);
            if (retain)
            {
                Measure("straight", line, 0);
                Measure("circle", circle, radius);
            }
            return (line, circle);

            void Measure(string name, Balance215 b, double r)
            {
                var result = MechanicalTrial216("h36-" + name, "balanced", nominal, nominalEngine, b.State, 2,
                    StepSeconds216, [], null, Gravity216);
                string path = Path.Combine(output, $"equilibrium-{name}.json.gz");
                if (!resumes) Save(path, result, true);
                var mid = result.Steps[result.Steps.Length / 2];
                var velocity = mid.Midpoint.Momentum.Plane / fixture.Base.Mass;
                var accel = (mid.After.Momentum - mid.Before.Momentum).Plane / (fixture.Base.Mass * Duration215(mid));
                var f = mid.Forces;
                equilibria.Add(new(speed, r, b.Residual, velocity.Cross(accel) / velocity.Norm, mid.Midpoint.Pose.Roll,
                    Math.Max(f.Front.Utilization, f.Rear.Utilization), f.Front.Lateral, mid.Midpoint.Steer,
                    f.FrontYawBand, f.FrontRollBand, Math.Abs(result.Summary.Final.Pose.Roll - b.State.Pose.Roll),
                    f.EngineRpm, b.State.Command.Bike.Drive, result.Summary.Seconds));
            }
        }

        bool Better(Corner36 a, Corner36 b)
        {
            if (a.Balanced != b.Balanced) return a.Balanced;
            if (!a.Balanced && a.Finish.Reference != b.Finish.Reference) return a.Finish.Reference > b.Finish.Reference;
            if (a.Balanced)
            {
                double aError = Math.Abs(a.NominalEntryKph - (a.Entry?.Kph ?? 0));
                double bError = Math.Abs(b.NominalEntryKph - (b.Entry?.Kph ?? 0));
                if (Math.Abs(aError - bError) > .05) return aError < bError;
                if (Math.Abs(a.MinimumSweep - b.MinimumSweep) > .02) return a.MinimumSweep > b.MinimumSweep;
            }
            return a.Id < b.Id;
        }

        static bool Faster(Corner36 a, Corner36 b) => a.Entry!.Kph == b.Entry!.Kph ? a.Id < b.Id : a.Entry.Kph > b.Entry.Kph;

        Tune36[] TrimSettings(double target)
        {
            var last = ReadPlain214<Stage36[]>(pacePath).Single();
            if (last.RequestedKph != target || last.Best is not { Balanced: true, Entry: not null } best ||
                last.HighestEntry is not { Balanced: true, Entry: not null } fastest)
                throw new InvalidOperationException("trim needs a balanced pace replay with an observed entry speed");
            return new[] { best, fastest }.SelectMany(result => new[] { 1d, 1.5 }.Select(factor =>
                result.Tune with { ApproachSpeedCorrectionKph = Math.Max(0, target - result.Entry!.Kph) * factor })).ToArray();
        }

        static Tune36[] PreparationSettings(Tune36 center)
        {
            // Keep the wide approach that reached the preceding band. Change the
            // entry speed target and the pace shed before maximum bend curvature.
            return Enumerable.Range(0, 48).Select(i => center with
            {
                Lane = center.Lane - .45 * Radical(i + 1, 2),
                Lookahead = 12 + 5 * Radical(i + 1, 3),
                RollGain = 20 + 16 * Radical(i + 1, 5),
                Damping = 6 + 7 * Radical(i + 1, 7),
                Leg = false,
                Boot = 0,
                FullDriveExitMeters = 0,
                ApexPaceDropKph = 24 + 17 * Radical(i + 1, 11),
                ApproachSpeedCorrectionKph = 4 + 7 * Radical(i + 1, 13)
            }).ToArray();
        }
    }

    private static State215 Place36(State215 local, double reference, double lane)
    {
        var (position, road) = Frame.At(reference, lane);
        var rotation = Attitude215.Pose(road.Heading, 0, 0);
        return local with
        {
            Center = new(position.X, position.Y, local.Center.Z),
            Pose = local.Pose.Advance(Vec215.Up, road.Heading),
            Momentum = rotation.Apply(local.Momentum), Angular = rotation.Apply(local.Angular)
        };
    }

    private static Tune36[] Proposals36(Tune36 anchor)
    {
        var settings = new List<Tune36> { anchor, anchor with { Leg = true, Boot = 0 }, anchor with { Leg = true, Boot = .83 } };
        for (int i = 1; settings.Count < 52; i++)
        {
            double P(int prime) => 2 * Radical(i, prime) - 1;
            bool foot = i % 7 == 0;
            settings.Add(new(Math.Clamp(anchor.Lane + 2.25 * P(2), 3.4, 8.6),
                Math.Clamp(anchor.Lookahead + 4 * P(3), 5, 17),
                Math.Clamp(anchor.RollGain + 13 * P(5), 10, 44), Math.Clamp(anchor.Damping + 4 * P(7), 4, 14),
                .05 * P(11), .045 * P(13), .032 * P(17), .90 * P(19), foot ? .44 + .18 * Radical(i, 23) : 0, foot,
                i % 9 == 0 ? 4 + 8 * Radical(i, 29) : 0));
        }
        return settings.ToArray();
    }

    private static double Radical(int index, int prime)
    {
        double place = 1d / prime, value = 0;
        while (index > 0)
        {
            value += (index % prime) * place;
            index /= prime;
            place /= prime;
        }
        return value;
    }

    private static Trace36 RunH36(int id, string name, double requested, Tune36 tune, Geometry215 g, UnifiedModel215 model,
        State215 seed, BendVisit visit, RacingLineGeometry track, CornerCourse216 course, float equilibriumDrive,
        Activation215[]? replay, bool exact, Activation215[]? prefix = null, Preparation37? preparation = null,
        double startingMeters = H36StartBeforeEntry)
    {
        var state = seed;
        Activation215[] queue = replay ?? prefix ?? [];
        double lastPrefixRequest = prefix is { Length: > 0 } ? prefix[^1].Requested : double.NegativeInfinity;
        var trace = new List<Step215>();
        var referenceSamples = preparation is null ? null : new List<double>();
        var mark = course.Mark(state, g);
        double s = visit.Entry - startingMeters;
        var origin = Sample(state, mark, s);
        double next = state.Time, end = state.Time + 24;
        double gap = H36Footprint.Clearance(track, g, state, exact), min = gap, ground = H36Footprint.GroundMargin(g, state);
        double wot = 0, slip = 0, continuous = 0, bestSlip = 0, energy = 0, peak = 0, minSpeed = double.PositiveInfinity;
        int forecastSteps = 0;
        Sample36? approach = null, entry = null, apex = null, exit = null;
        string stop = "time horizon";
        while (state.Time < end - 1e-9 && trace.Count < 18_000)
        {
            if (gap < SpatialBike36.RequiredSweptMeters) { stop = "spatial swept clearance"; break; }
            if (state.Time >= next - 1e-10)
            {
                if (replay is null && state.Time > lastPrefixRequest + 1e-10)
                {
                    var control = H36Control(state, g, model, mark, s, visit, tune, requested, equilibriumDrive, queue, course, preparation);
                    forecastSteps += control.Steps;
                    if (control.Command is not { } command) { stop = control.Stop!; break; }
                    queue = [.. queue, new(state.Time, state.Time + ReactionSeconds216, command)];
                }
                next += H36ControlPeriod;
            }
            try
            {
                var row = model.Step(state, queue, Math.Min(StepSeconds216, Math.Min(end - state.Time, next - state.Time)));
                var tail = course.Mark(row.After, g);
                double refAfter = s + Frame.UnwrapDelta(mark.Road.ReferenceMeters, tail.Road.ReferenceMeters);
                double tailGap = H36Footprint.Clearance(track, g, row.After, exact);
                double motionBound = H36Footprint.TransportBound(g, row) / 2;
                double swept = Math.Min(gap, tailGap) - motionBound;
                double groundSweep = Math.Min(H36Footprint.GroundMargin(g, row.Before), H36Footprint.GroundMargin(g, row.After)) -
                    H36Footprint.TransportBound(g, row, groundOnly: true) / 2;
                if (swept < SpatialBike36.RequiredSweptMeters) { stop = "spatial swept clearance"; break; }
                if (groundSweep <= 0) { stop = "chassis swept ground margin"; break; }
                if (CornerCourse216.Violation(tail, swept) is { } gate) { stop = gate; break; }
                min = Math.Min(min, swept);
                ground = Math.Min(ground, groundSweep);
                trace.Add(row);
                referenceSamples?.Add(refAfter);
                double dt = Duration215(row);
                var sample = Sample(row.After, tail, refAfter);
                if (approach is null && refAfter >= visit.Entry - H36CheckpointBeforeEntry) approach = sample;
                if (entry is null && refAfter >= visit.Entry) entry = sample;
                if (apex is null && refAfter >= (visit.Entry + visit.Exit) / 2) apex = sample;
                if (exit is null && refAfter >= visit.Exit) exit = sample;
                if ((s + refAfter) / 2 >= visit.Entry && (s + refAfter) / 2 < visit.Exit)
                {
                    minSpeed = Math.Min(minSpeed, sample.Kph);
                    if (row.Before.Command.Bike.Drive >= .9999) wot += dt;
                    if (Math.Abs(tail.SlideRadians) >= Math.PI / 30 && row.Forces.Rear.Normal > 20)
                    {
                        slip += dt;
                        continuous += dt;
                        bestSlip = Math.Max(bestSlip, continuous);
                    }
                    else continuous = 0;
                }
                energy += row.EnergyResidual;
                peak = Math.Max(peak, Math.Abs(energy));
                state = row.After;
                mark = tail;
                s = refAfter;
                gap = tailGap;
                if (s >= visit.Exit + ExitRunout216)
                {
                    stop = Math.Abs(state.Pose.Roll) <= .22 && Math.Abs(mark.AngularVelocity) <= .28
                        ? "balanced corner" : "exit balance gate";
                    break;
                }
            }
            catch (InvalidOperationException error) { stop = error.Message; break; }
        }
        double Max(Func<Step215, double> f) => trace.Select(f).DefaultIfEmpty(0).Max();
        double Min(Func<Step215, double> f) => trace.Select(f).DefaultIfEmpty(0).Min();
        var finish = Sample(state, mark, s);
        bool balanced = stop == "balanced corner" && approach is not null && entry is not null && exit is not null;
        var summary = new Corner36(name, id, requested, tune, stop, balanced, balanced && bestSlip >= .12,
            origin, approach, entry, apex, exit, finish, double.IsFinite(minSpeed) ? minSpeed : 0, min, ground,
            Max(r => Math.Max(r.Forces.Front.Utilization, r.Forces.Rear.Utilization)), Max(r => Math.Abs(r.Forces.RearSlip)),
            Min(r => r.Forces.Front.Normal), Min(r => r.Forces.Rear.Normal),
            Max(r => r.Forces.Rider.Norm + Math.Abs(r.Forces.LeftFoot?.MuscleForce ?? 0)),
            Max(r => r.Forces.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(r.ActuatorPower)),
            Max(r => r.Forces.LeftFoot?.Contact.NormalForce ?? 0), wot, slip, bestSlip, peak, trace.Count, forecastSteps);
        return new(summary, seed, queue, trace.ToArray()) { ReferenceSamples = referenceSamples?.ToArray() };

        static Sample36 Sample(State215 position, RiderView body, double reference) =>
            new(position.Time, reference, body.Speed * 3.6, position.Pose.Roll, body.AngularVelocity, body.Road.OffsetMeters);
    }

    private static (Command215? Command, int Steps, string? Stop) H36Control(State215 state, Geometry215 g,
        UnifiedModel215 model, RiderView mark, double reference, BendVisit bend, Tune36 tune, double requested,
        float equilibriumDrive, Activation215[] pending, CornerCourse216 course, Preparation37? preparation = null,
        Action<RollReference36>? observe = null, double rollReferenceScale = 1, Command215? commandBasis = null,
        Func<RiderView, double, RollReference36>? referenceBasis = null)
    {
        double activation = state.Time + ReactionSeconds216;
        var lead = Simulate(state, pending, activation);
        int count = lead.Steps;
        if (lead.Error is not null) return (null, count, "pending continuation: " + lead.Error);
        var x = lead.State;
        var body = course.Mark(x, g);
        double s = reference + Frame.UnwrapDelta(mark.Road.ReferenceMeters, body.Road.ReferenceMeters);
        var guide = referenceBasis?.Invoke(body, s) ?? (preparation is null
            ? Reference36(body, s, bend, tune, requested, equilibriumDrive)
            : Reference37(body, s, bend, tune, requested, equilibriumDrive, preparation));
        if (rollReferenceScale != 1) guide = guide with { Roll = guide.Roll * rollReferenceScale };
        if (preparation is { GroundReserveMeters: > 0 })
            guide = guide with { Roll = ClearanceRoll37(g, x, guide.Roll, preparation.GroundReserveMeters) };
        observe?.Invoke(guide);
        var m = g.Motion(x);
        double rate = RollRate36(x, m.BodyRate);
        const double response = .065;
        double demand = rate + response * (tune.RollGain * (guide.Roll - x.Pose.Roll) - tune.Damping * rate);
        double steerTravel = response * Steering217.SteeringRateRadiansPerSecond;
        double lo = Math.Max(-Steering217.MaximumSteeringRadians, x.Steer - steerTravel);
        double hi = Math.Min(Steering217.MaximumSteeringRadians, x.Steer + steerTravel);
        var low = Test(lo);
        var high = Test(hi);
        if (low.Error is not null && high.Error is not null)
        {
            var held = Test(x.Steer);
            return held.Error is null ? (Command(x.Steer), count, null) : (null, count, "roll forecast: " + held.Error);
        }
        if (low.Error is not null) return (Command(hi), count, null);
        if (high.Error is not null) return (Command(lo), count, null);
        double left = RollRate36(low.State, g.Motion(low.State).BodyRate);
        double right = RollRate36(high.State, g.Motion(high.State).BodyRate);
        double steer = Math.Abs(right - left) < 1e-7 ? x.Steer :
            lo + Math.Clamp((demand - left) / (right - left), 0, 1) * (hi - lo);
        return (Command(steer), count, null);

        Command215 Command(double angle) => commandBasis is { } input
            ? input with { Bike = input.Bike with { SteeringRadians = (float)angle } }
            : new(new((float)angle, guide.Drive), guide.Rider) { LeftFoot = guide.Boot };

        Forecast36 Test(double angle)
        {
            Activation215[] command = [new(state.Time, activation, Command(angle))];
            var result = Simulate(x with { Next = 0 }, command, activation + response);
            count += result.Steps;
            return result;
        }

        Forecast36 Simulate(State215 s, Activation215[] plan, double until)
        {
            int steps = 0;
            Forces215? f = null;
            try
            {
                while (s.Time < until - 1e-9 && steps < 160)
                {
                    var row = model.Step(s, plan, Math.Min(.005, until - s.Time));
                    s = row.After;
                    f = row.Forces;
                    steps++;
                }
                return new(s, f, steps, s.Time < until - 1e-9 ? "forecast integration budget" : null);
            }
            catch (InvalidOperationException e) { return new(s, f, steps, e.Message); }
        }

    }

    private static RollReference36 Reference36(RiderView body, double reference, BendVisit bend, Tune36 tune,
        double requested, float equilibriumDrive)
    {
        double fraction = Math.Clamp((reference - bend.Entry) / bend.Length, 0, 1);
        double ahead = reference + tune.Lookahead;
        double fAhead = Math.Clamp((ahead - bend.Entry) / bend.Length, 0, 1);
        double lane = tune.Lane - tune.Apex * Math.Sin(Math.PI * fAhead) * Math.Sin(Math.PI * fAhead);
        double curvature = PreviewCurvature36(body, ahead, lane);
        double speed = body.Speed;
        double desiredRoll = -Math.Atan(speed * speed * curvature / Gravity216);
        double paceRamp = Math.Clamp((reference - bend.Entry + 1) / 9, 0, 1);
        double desiredSpeed = (requested - tune.ApexPaceDropKph * paceRamp +
            tune.ApproachSpeedCorrectionKph * (1 - paceRamp)) / 3.6;
        float drive = (float)Math.Clamp(equilibriumDrive + .16 * (desiredSpeed - speed), 0, 1);
        if (reference < bend.Exit && bend.Exit - reference <= tune.FullDriveExitMeters) drive = 1;
        double phase = Math.Sin(Math.PI * fraction);
        return new(curvature, desiredRoll, tune.Lookahead, desiredSpeed * 3.6, drive,
            new(tune.Fore * phase, tune.Side * phase, tune.Vertical * phase), tune.Boot * phase * phase);
    }

    private static double PreviewCurvature36(RiderView body, double ahead, double lane)
    {
        var aim = Frame.At(ahead, lane).Position - body.Position;
        return 2 * ((double)body.Velocity.X * aim.Y - (double)body.Velocity.Y * aim.X) /
            Math.Max(1e-9, (double)body.Speed * aim.LengthSquared());
    }

    private static double RollRate36(State215 s, Vec215 w) =>
        w.X - Math.Tan(s.Pose.Pitch) * (Math.Sin(s.Pose.Roll) * w.Y + Math.Cos(s.Pose.Roll) * w.Z);
}
