
using System.Diagnostics;
using System.Numerics;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double PreparationStart37 = 64;
    private const double ContinuationSeconds37 = 1.15;
    private enum Strategy37 { Balanced, ProgressivePower, EarlyLine, ActivePosture, LeftFoot }
    private sealed record Preparation37(Strategy37 Strategy, double OnsetMeters, double InwardShiftMeters,
        double EntryBrakeLeadMeters, double PoweredDrive, double RecoverMeters, double ExitBoostMeters,
        double PaceEndMeters = 18, double GroundReserveMeters = 0);
    private sealed record Candidate37(int Id, int TargetKph, Tune36 Tune, Preparation37 Plan);
    private sealed record Metric37(double? CornerSeconds, double? MinimumKph, double? ExitKph,
        double SlideSeconds, double LongestSlideSeconds, double PoweredSlideSeconds, double FullDriveSlideSeconds,
        double PeakRoll, double PeakEntryRollRate, double MaximumEnergyLedgerJoules);
    private sealed record StageResult37(Candidate37 Setup, Corner36 Corner, Metric37 Metrics);
    private sealed record StageTrials37(StageResult37[] Outcomes);
    private sealed record Tape37(Corner36 Summary, State215 Seed, Activation215[] Commands);
    private sealed record Runout37(double InitialTime, double FinalTime, double InitialKph, double FinalKph,
        double MinimumSwept, double MinimumHardGround, double MaxRoll, bool Stable, double EnergyLedger,
        string Stop, State215 Final, Activation215[] Commands, Step215[] Trace);

    private static void PreparationStudy218(string repository, string output, string mode)
    {
        string root = Path.GetFullPath(repository);
        var (_, spec) = CourseFixture208(root, "C");
        var fixture = new Fixture216(spec);
        var bend = Map199.At(2, 0);
        var track = new RacingLineGeometry(Frame);
        var course = new CornerCourse216(track, RootFor210(root, "C").State.State.Rider);
        var geometry = new Geometry215(fixture.Base, Axes215.All, spatial: H36Footprint);
        var engine = fixture.Engine(geometry, NominalSupport216, Actuator215.Limited, true);
        var straight = SteadyCircle215(engine, geometry, fixture.Rest(geometry, NominalSupport216),
            40 / 3.6, 0, fixture.Release + 1, fixture.Ratio, balanceRider: true);
        if (straight.Residual > .005) throw new InvalidOperationException("Preparation seed equilibrium failed.");
        float equilibriumDrive = straight.State.Command.Bike.Drive;
        switch (mode)
        {
            case "diagnose":
                Reconstruct37(root, output, fixture, track, course, bend, equilibriumDrive);
                return;
            case "all":
                Reconstruct37(root, output, fixture, track, course, bend, equilibriumDrive);
                break;
            case "search": case "70": case "80": case "90": case "100": case "boot": case "wide-90": case "trim-90":
            case "exit-80": case "settle-80": case "terminal-80":
                break;
            default:
                throw new ArgumentException("Use diagnose, search, all, boot, wide-90, trim-90, exit-80, settle-80, terminal-80, 70, 80, 90 or 100.");
        }

        var legGeometry = new Geometry215(fixture.Base, Axes215.All, new Leg216(), H36Footprint);
        var legStraight = SteadyCircle215(fixture.Engine(legGeometry, NominalSupport216, Actuator215.Limited, true),
            legGeometry, fixture.Rest(legGeometry, NominalSupport216), 40 / 3.6, 0,
            fixture.Release + 1, fixture.Ratio, balanceRider: true);
        if (legStraight.Residual > .005) throw new InvalidOperationException("Leg-equipped preparation seed equilibrium failed.");
        State215 wheels = Place36(straight.State, bend.Entry - PreparationStart37, 8.20);
        State215 boot = Place36(legStraight.State, bend.Entry - PreparationStart37, 8.20);
        using var process = Process.GetCurrentProcess();
        if (mode == "terminal-80")
        {
            const double terminalSeconds = 2.10;
            var cpu = process.TotalProcessorTime;
            var timer = Stopwatch.StartNew();
            var selection = ReadPlain214<StageTrials37>(Path.Combine(output, "stage-80.json")).Outcomes
                .Where(o => o.Corner.Balanced && o.Corner.Entry is { } e && Math.Abs(e.Kph - 80) <= 2)
                .OrderBy(o => o.Metrics.CornerSeconds).ThenBy(o => o.Setup.Id).First();
            var setup = selection.Setup;
            var (g, _) = Seed(setup);
            var replay = Execute(setup, "fixed bend before terminal search", exact: false, null);
            if (replay.Summary.Finish != selection.Corner.Finish) throw new InvalidOperationException("Terminal fork mismatch.");
            var settings = (from preview in new[] { 14d, 23d }
                            from scale in new[] { .25, .6, 1d }
                            from lane in new[] { -.5, .35 }
                            from slow in new[] { 0, 1 }
                            select new StraightTuning37(preview, 17 + 7 * slow, 8 + 9 * slow,
                                scale, lane, 6 + 3 * slow)).ToArray();
            var outcomes = new Runout37[settings.Length];
            Parallel.For(0, settings.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) },
                i => outcomes[i] = CheckRunout37(fixture, g, track, course, bend, replay, true, terminalSeconds, settings[i]));
            var ranked = Enumerable.Range(0, settings.Length)
                .OrderByDescending(i => outcomes[i].Stable)
                .ThenBy(i => Math.Abs(outcomes[i].Final.Pose.Roll) +
                    Math.Abs(course.Mark(outcomes[i].Final, g).AngularVelocity) * .20)
                .ThenByDescending(i => outcomes[i].FinalKph).ToArray();
            int best = ranked[0], bestRecoveryCount = -1;
            Perturbation37[] robustness = [];
            var robustnessTrials = new List<object>();
            foreach (var i in ranked.Where(i => outcomes[i].Stable).Take(6))
            {
                var trial = Perturbations37(fixture, g, track, course, bend, setup, replay, equilibriumDrive,
                    true, terminalSeconds, settings[i]);
                int recovered = trial.Count(x => x.Balanced && x.InBand && x.Recovered);
                robustnessTrials.Add(new { Id = i + 1, Recovered = recovered, Cases = trial });
                if (recovered > bestRecoveryCount)
                {
                    (best, bestRecoveryCount, robustness) = (i, recovered, trial);
                }
                if (recovered == trial.Length) break;
            }
            var runout = outcomes[best];
            Save(Path.Combine(output, "terminal-80-runout.json.gz"), runout, true);
            Save(Path.Combine(output, "terminal-80.json"), new
            {
                setup, Bend = selection.Corner, Selection = settings[best], Chosen = best + 1,
                Robustness = robustness, RobustnessTrials = robustnessTrials,
                Outcomes = outcomes.Select((r, i) => new
                {
                    Id = i + 1, Tuning = settings[i], r.Stop, r.Stable, r.InitialKph, r.FinalKph,
                    Roll = r.Final.Pose.Roll, YawRate = course.Mark(r.Final, g).AngularVelocity, r.MaxRoll,
                    r.MinimumSwept, r.MinimumHardGround, AcceptedSteps = r.Trace.Length
                }).ToArray(),
                CpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds, WallSeconds = timer.Elapsed.TotalSeconds,
                ContinuationSeconds = terminalSeconds
            });
            return;
        }
        if (mode is "exit-80" or "settle-80")
        {
            bool settle = mode == "settle-80";
            double continuationSeconds = settle ? 2.10 : ContinuationSeconds37;
            var startedCpu = process.TotalProcessorTime;
            var clock = Stopwatch.StartNew();
            var candidates = ReadPlain214<StageTrials37>(Path.Combine(output, "stage-80.json")).Outcomes
                .Where(o => o.Corner.Balanced && o.Corner.Entry is { } e && Math.Abs(e.Kph - 80) <= 2)
                .OrderBy(o => o.Metrics.CornerSeconds).ThenBy(o => o.Setup.Id).Take(3);
            var reviews = new List<object>();
            foreach (var candidate in candidates)
            {
                var setup = candidate.Setup;
                var (g, _) = Seed(setup);
                var replay = Execute(setup, "recorded bend before gradual acceleration", exact: true, null);
                if (!replay.Summary.Balanced || replay.Summary.Entry != candidate.Corner.Entry ||
                    replay.Summary.Finish != candidate.Corner.Finish)
                    throw new InvalidOperationException("Archived 80 km/h bend did not reproduce.");
                var runout = CheckRunout37(fixture, g, track, course, bend, replay, progressive: true, continuationSeconds);
                var tail = course.Mark(runout.Final, g);
                var robustness = runout.Stable ? Perturbations37(fixture, g, track, course, bend, setup,
                    replay, equilibriumDrive, progressive: true, continuationSeconds) : [];
                string label = $"{mode}-proposal-{setup.Id}";
                WriteComparison37(output, label, fixture, g, track, course, bend, replay, setup.Plan, equilibriumDrive);
                Save(Path.Combine(output, label + "-runout.json.gz"), runout, true);
                reviews.Add(new { setup, replay.Summary, candidate.Metrics, Runout = new
                    {
                        runout.Stable, runout.InitialKph, runout.FinalKph, runout.MinimumSwept, runout.MinimumHardGround,
                        runout.Stop, Roll = runout.Final.Pose.Roll, YawRate = tail.AngularVelocity,
                        AcceptedSteps = runout.Trace.Length
                    },
                    Robustness = robustness,
                    ReplaySteps = replay.Trace.Length + runout.Trace.Length + robustness.Sum(x => x.ActualSteps) });
                if (runout.Stable && robustness.All(x => x.Balanced && x.InBand && x.Recovered)) break;
            }
            Save(Path.Combine(output, $"{mode}.json"), new
            {
                Reviews = reviews, ContinuationSeconds = continuationSeconds,
                CpuSeconds = (process.TotalProcessorTime - startedCpu).TotalSeconds,
                WallSeconds = clock.Elapsed.TotalSeconds
            });
            return;
        }
        if (mode is "boot" or "all")
        {
            var matched = new List<object>();
            foreach (int target in new[] { 70, 80 })
            {
                var active = PreparationGrid37(target).First(x => x.Plan.Strategy == Strategy37.LeftFoot &&
                    x.Plan.OnsetMeters == 30);
                foreach (bool deploy in new[] { false, true })
                {
                    var settings = deploy ? active : active with { Tune = active.Tune with { Boot = 0 } };
                    var ride = Execute(settings, deploy ? "deploy boot" : "park boot", true, null);
                    string label = $"boot-{target}-{(deploy ? "deployed" : "parked")}";
                    var telemetry = WriteComparison37(output, label, fixture, legGeometry, track, course, bend, ride,
                        settings.Plan, equilibriumDrive);
                    matched.Add(new { target, deploy, ride.Summary, telemetry.Metrics,
                        FootNormalImpulse = telemetry.Steps.Select((s, i) => s.FootNormal *
                            (s.Time - (i == 0 ? ride.Seed.Time : telemetry.Steps[i - 1].Time))).Sum() });
                }
            }
            Save(Path.Combine(output, "matched-boot.json"), matched);
            if (mode == "boot") return;
        }
        bool trim = mode == "trim-90";
        bool widerLine = mode == "wide-90" || trim;
        string stem = trim ? "trim-90" : "wide-90";
        int[] speeds = widerLine ? [90] : int.TryParse(mode, out int single) ? [single] : [70, 80, 90, 100];
        foreach (int target in speeds)
        {
            var timer = Stopwatch.StartNew();
            var startedCpu = process.TotalProcessorTime;
            var settings = trim ? TrimGrid37(output) : widerLine ? WideGrid37() : PreparationGrid37(target);
            var outcomes = new StageResult37[settings.Length];
            int finished = 0;
            Parallel.For(0, settings.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(6, Environment.ProcessorCount) }, i =>
            {
                var setting = settings[i];
                var sample = Execute(setting, "search", exact: false, null);
                outcomes[i] = new(setting, sample.Summary, Metrics37(sample, fixture.Base.Mass, bend));
                int count = Interlocked.Increment(ref finished);
                if (count % 10 == 0) Console.WriteLine($"{target}: {count}/{settings.Length} proposals evaluated.");
            });
            var contenders = outcomes
                .Where(o => o.Corner.Balanced)
                .OrderBy(o => o.Corner.Entry!.Kph < target - 2 || o.Corner.Entry.Kph > target + 2)
                .ThenBy(o => o.Metrics.CornerSeconds)
                .ThenByDescending(o => o.Corner.Exit!.Kph)
                .ThenBy(o => o.Setup.Id)
                .Take(8)
                .ToArray();
            var retained = new List<object>();
            var recourse = new List<object>();
            int replaySteps = 0;
            foreach (var c in contenders)
            {
                var replay = Execute(c.Setup, "continuous exact replay", exact: true, null);
                var repeat = Execute(c.Setup, "recorded command replay", exact: true, replay.Commands);
                if (!replay.Trace.Select(r => r.After).SequenceEqual(repeat.Trace.Select(r => r.After)))
                    throw new InvalidOperationException("Independent exact replay did not reproduce the preparation.");
                replaySteps += replay.Trace.Length + repeat.Trace.Length;
                if (!replay.Summary.Balanced)
                {
                    recourse.Add(new { c.Setup.Id, replay.Summary.Stop, replay.Summary.Finish.Reference });
                    continue;
                }
                bool inBand = Math.Abs(replay.Summary.Entry!.Kph - target) <= 2;
                var (g, _) = Seed(c.Setup);
                var runout = CheckRunout37(fixture, g, track, course, bend, replay);
                replaySteps += runout.Trace.Length;
                if (!runout.Stable)
                {
                    recourse.Add(new { c.Setup.Id, runout.Stop, runout.FinalKph });
                    continue;
                }
                string label = $"{(widerLine ? stem : $"speed-{target}")}-proposal-{c.Setup.Id}";
                var robustness = Perturbations37(fixture, g, track, course, bend, c.Setup, replay, equilibriumDrive);
                replaySteps += robustness.Sum(r => r.ActualSteps);
                WriteComparison37(output, label, fixture, g, track, course, bend, replay, c.Setup.Plan, equilibriumDrive);
                Save(Path.Combine(output, label + "-runout.json.gz"), runout, true);
                retained.Add(new { c.Setup, InBand = inBand, replay.Summary, Metrics = Metrics37(replay, fixture.Base.Mass, bend),
                    Runout = new { runout.Stable, runout.InitialKph, runout.FinalKph, runout.MinimumSwept, runout.MinimumHardGround, runout.Stop },
                    Robustness = robustness });
                if (inBand && robustness.All(r => r.Balanced && r.Recovered && r.InBand)) break;
                if (retained.Count >= 3) break;
            }

            if (retained.Count == 0)
            {
                var deepest = outcomes.OrderByDescending(o => o.Corner.Finish.Reference).ThenBy(o => o.Setup.Id).First();
                var witness = Execute(deepest.Setup, "furthest exact prefix", exact: true, null);
                replaySteps += witness.Trace.Length;
                var (g, _) = Seed(deepest.Setup);
                string label = $"{(widerLine ? stem : $"speed-{target}")}-frontier";
                WriteComparison37(output, label, fixture, g, track, course, bend, witness,
                    deepest.Setup.Plan, equilibriumDrive);
            }
            Save(Path.Combine(output, trim ? "trim-90.json" : widerLine ? "recourse-90.json" : $"stage-{target}.json"), new
            {
                TargetKph = target, RequestedProposals = settings.Length, ApproximateCompletions = outcomes.Count(x => x.Corner.Balanced),
                Outcomes = outcomes, Rechecked = retained, StoppedDuringExactVerification = recourse, ReplaySteps = replaySteps,
                SearchActualSteps = outcomes.Sum(x => x.Corner.Steps), SearchForecastSteps = outcomes.Sum(x => x.Corner.ForecastSteps),
                CpuSeconds = (process.TotalProcessorTime - startedCpu).TotalSeconds, WallSeconds = timer.Elapsed.TotalSeconds
            });
            Console.WriteLine($"{target}: {outcomes.Count(x => x.Corner.Balanced)}/{outcomes.Length} complete, {retained.Count} exact exits with acceleration.");
        }

        (Geometry215 Geometry, State215 State) Seed(Candidate37 spec37) =>
            spec37.Tune.Leg ? (legGeometry, boot) : (geometry, wheels);

        Trace36 Execute(Candidate37 setup, string label, bool exact, Activation215[]? tape)
        {
            var (g, s) = Seed(setup);
            return RunH36(setup.Id, label, setup.TargetKph, setup.Tune, g,
                fixture.Engine(g, NominalSupport216, Actuator215.Limited, true), s, bend, track, course, equilibriumDrive,
                tape, exact, preparation: setup.Plan, startingMeters: PreparationStart37);
        }
    }

    private static Candidate37[] PreparationGrid37(int target)
    {
        int id = 0;
        return (from onset in new[] { 50, 40, 30, 20, 10 }
                from method in Enum.GetValues<Strategy37>()
                from variation in new[] { 0, 1 }
                let leftFoot = method == Strategy37.LeftFoot
                let posture = method is Strategy37.ActivePosture or Strategy37.LeftFoot
                let shift = method == Strategy37.EarlyLine ? .95 + .30 * variation : .14 + .16 * variation
                let powered = method == Strategy37.ProgressivePower ? .22 + .13 * variation : posture ? .10 : 0
                let slowdown = Math.Max(0, target - (variation == 0 ? 62 : 72))
                let lane = variation == 0 ? 8.20 : 7.45
                select new Candidate37(++id, target,
                    new Tune36(lane, 12 + 3 * variation, 22 + 5 * variation, 11 - 2 * variation,
                        posture ? -.045 : -.018, posture ? .045 : .013, posture ? -.024 : -.008,
                        variation == 0 ? .20 : .53, leftFoot ? .72 - .1 * variation : 0, leftFoot,
                        ApexPaceDropKph: slowdown, ApproachSpeedCorrectionKph: target >= 90 ? 3.4 + variation * 2 : .6),
                    new(method, onset, shift, method == Strategy37.Balanced ? 0 : Math.Min(8, onset * .14),
                        powered, 7 + 3 * variation, 3 + 4 * variation))).ToArray();
    }

    private static Candidate37[] WideGrid37()
    {
        int id = 0;
        return (from lane in new[] { 8.25, 8.85, 9.45 }
                from brake in new[] { 9d, 14d, 19d }
                from reserve in new[] { .018, .038 }
                from pace in new[] { 0, 1 }
                select new Candidate37(++id, 90,
                    new Tune36(lane, 15 + 3 * pace, 24, 10, .025, .035, -.018,
                        -.25, 0, false, ApexPaceDropKph: 30 + 8 * pace,
                        ApproachSpeedCorrectionKph: 3.2 + .55 * brake + 1.5 * pace),
                    new(Strategy37.EarlyLine, 40, .35, brake, 0, 8, 4, 9 + 3 * pace, reserve))).ToArray();
    }

    private static Candidate37[] TrimGrid37(string output)
    {
        var prior = ReadPlain214<StageTrials37>(Path.Combine(output, "recourse-90.json"));
        int id = 0;
        return (from trial in prior.Outcomes.Where(x => x.Corner.Entry is not null)
                    .OrderByDescending(x => x.Corner.Finish.Reference)
                    .DistinctBy(x => (x.Setup.Tune.Lane, x.Setup.Plan.GroundReserveMeters))
                    .Take(4)
                from factor in new[] { .95, 1.4, 1.8 }
                let correction = (trial.Corner.Entry!.Kph - 89.8) * factor
                select trial.Setup with
                {
                    Id = ++id,
                    Tune = trial.Setup.Tune with
                    {
                        ApproachSpeedCorrectionKph = trial.Setup.Tune.ApproachSpeedCorrectionKph - correction
                    }
                }).ToArray();
    }

    private static RollReference36 Reference37(RiderView body, double reference, BendVisit bend, Tune36 tune,
        double requested, float equilibriumDrive, Preparation37 plan)
    {
        double distance = reference - bend.Entry;
        double before = Smooth37((distance + plan.OnsetMeters) / plan.OnsetMeters);
        double approachWeight = 1 - Smooth37((distance - 3) / 30);
        double preview = tune.Lookahead + before * approachWeight *
            Math.Min(14, plan.OnsetMeters * .26);
        double ahead = distance < -plan.OnsetMeters ? Math.Min(bend.Entry, reference + preview) : reference + preview;
        double progress = Math.Clamp((ahead - bend.Entry) / bend.Length, 0, 1);
        double setup = Smooth37((ahead - bend.Entry + plan.OnsetMeters) / (plan.OnsetMeters + 14)) * approachWeight;
        double lane = 8.20 + (tune.Lane - 8.20) * before - plan.InwardShiftMeters * setup -
            tune.Apex * Math.Pow(Math.Sin(Math.PI * progress), 2);
        if (distance < -plan.OnsetMeters) lane = 8.20;
        double curvature = PreviewCurvature36(body, ahead, lane);
        double roll = -Math.Atan((double)body.Speed * body.Speed * curvature / Gravity216);
        double pace = Smooth37((distance + plan.EntryBrakeLeadMeters) / (plan.PaceEndMeters + plan.EntryBrakeLeadMeters));
        double recovery = Smooth37((reference - bend.Exit + plan.RecoverMeters) / plan.RecoverMeters);
        double speedTarget = requested + tune.ApproachSpeedCorrectionKph * (1 - pace) -
            tune.ApexPaceDropKph * pace + recovery * (tune.ApexPaceDropKph * .35 + 4);
        double drive = Math.Clamp(equilibriumDrive + .16 * (speedTarget / 3.6 - body.Speed), 0, 1);
        if (distance >= -3 && distance <= 34)
            drive = Math.Max(drive, plan.PoweredDrive * Smooth37((distance + 3) / 12) * (1 - Smooth37((distance - 21) / 13)));
        if (reference >= bend.Exit - plan.ExitBoostMeters) drive = Math.Max(drive, .68 * recovery);
        double posture = Math.Max(Smooth37((distance + plan.OnsetMeters) / (plan.OnsetMeters + 8)) * approachWeight,
            Math.Sin(Math.PI * Math.Clamp(distance / bend.Length, 0, 1)));
        var rider = new Vec215(tune.Fore * posture, tune.Side * posture, tune.Vertical * posture);
        double foot = tune.Boot * posture * posture * (1 - recovery);
        return new(curvature, roll, preview, speedTarget, (float)drive, rider, foot);
    }

    private static double Smooth37(double x)
    {
        double t = Math.Clamp(x, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double ClearanceRoll37(Geometry215 g, State215 s, double requested, double reserve)
    {
        if (reserve <= 0) return requested;
        var sign = Math.Sign(requested);
        double safe = 0, target = Math.Min(Math.Abs(requested), Geometry215.MaxRoll);
        if (Fits(target)) return sign * target;
        for (int n = 0; n < 9; n++)
        {
            double middle = (safe + target) / 2;
            if (Fits(middle)) safe = middle;
            else target = middle;
        }
        return sign * safe;

        bool Fits(double angle)
        {
            var candidate = s with { Pose = Attitude215.Pose(s.Pose.Yaw, s.Pose.Pitch, sign * angle) };
            return H36Footprint.GroundMargin(g, candidate) >= reserve;
        }
    }

    private static Metric37 Metrics37(Trace36 trajectory, double mass, BendVisit bend)
    {
        double prepRate = 0, peak = 0, fullSlide = 0, poweredSlide = 0, reference = trajectory.Summary.Start.Reference;
        for (int i = 0; i < trajectory.Trace.Length; i++)
        {
            var row = trajectory.Trace[i];
            double after;
            if (trajectory.ReferenceSamples is { } measured) after = measured[i];
            else
            {
                var x = Frame.Project(new Vector2((float)row.Before.Center.X, (float)row.Before.Center.Y));
                var y = Frame.Project(new Vector2((float)row.After.Center.X, (float)row.After.Center.Y));
                after = reference + Frame.UnwrapDelta(x.ReferenceMeters, y.ReferenceMeters);
            }
            double midpoint = (reference + after) / 2;
            double duration = Duration215(row);
            reference = after;
            if (midpoint >= bend.Entry - 15 && midpoint <= bend.Entry + 15)
                prepRate = Math.Max(prepRate, Math.Abs(row.After.Pose.Roll - row.Before.Pose.Roll) / duration);
            if (midpoint < bend.Entry || midpoint >= bend.Exit) continue;
            peak = Math.Max(peak, Math.Abs(row.After.Pose.Roll));
            double heading = Math.Atan2(row.After.Momentum.Y / mass, row.After.Momentum.X / mass);
            if (Math.Abs(Math.IEEERemainder(row.After.Pose.Yaw - heading, 2 * Math.PI)) < Math.PI / 30 ||
                row.Forces.Rear.Normal <= 20) continue;
            if (row.Before.Command.Bike.Drive >= .25f) poweredSlide += duration;
            if (row.Before.Command.Bike.Drive >= .9999f) fullSlide += duration;
        }
        var s = trajectory.Summary;
        return new(s.Entry is { } entry && s.Exit is { } exit ? exit.Time - entry.Time : null,
            s.Exit is null ? null : s.MinKph, s.Exit?.Kph, s.SlidingSeconds, s.LongestSlide, poweredSlide, fullSlide,
            peak, prepRate, s.MaxEnergyError);
    }
}
