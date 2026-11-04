using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using FastTwoCorners167;
using LearnedCorner166;
using LineThrottle156;
using SharedWorldSpeedway;

namespace ThrottleContact168;

internal sealed record InputSeal(FreeRacingInput Input, double CrankOmega, double InnerClearance, double OuterClearance);
internal sealed record Trial(string Id, string Experiment, string Cohort, string Steering, float? Drive,
    double BranchSeconds, double ActivationSeconds, double EndSeconds, double StartReferenceMeters,
    double StartKph, string BranchSha256, string PreActivationSha256, bool OriginalPrefixMatches,
    bool? OriginalTraceMatches, double CpuSeconds, int NativeSteps, string Outcome);
internal sealed record MatchedSource(Run144 Run, double BranchSeconds, string PrefixSha256, string ArchiveSha256);

internal sealed class Program
{
    private const string BaseStudy = "docs/experiments/fast-two-corners";
    private const string OriginalStudy = "docs/experiments/autonomous-fast-corner";
    private const double CpuLimit = 250;
    private const double Admission = 225;
    private const int StraightLimit = 18;
    private const int ExitLimit = 8;
    private const double StraightWindowSeconds = 2;
    private const double StraightPreparationSeconds = .8;
    private const double ExitWindowSeconds = 1.4;
    private const double StraightStartMeters = -25;
    private const double StraightOffsetMeters = 5;
    private const double PreparationThrottle = .5;
    private const double ClockTolerance = 1e-8;
    private static readonly float[] Throttles = [.5f, 1f];
    private static readonly int[] SpeedsKph = [60, 70, 80];
    private static readonly string[] SteeringModes = ["zero", "one-degree", "counter"];
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    private static readonly JsonSerializerOptions Pretty = new(Json) { WriteIndented = true };
    private readonly Process clock = Process.GetCurrentProcess();
    private readonly TrackFrame frame = NativeWorld166.Frame();
    private readonly string directory;
    private readonly List<Trial> trials = [];
    private double Cpu => clock.TotalProcessorTime.TotalSeconds;
    private string PathTo(string name) => Path.Combine(directory, name);

    private Program(string directory) => this.directory = directory;

    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--run")
        {
            Console.Error.WriteLine("Study #168 is OFF. Use --run with a fresh evidence directory.");
            return 2;
        }

        string path = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(path);
        using var mutex = new FileStream(Path.Combine(path, ".study.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var study = new Program(path);
        study.Run();
        return 0;
    }

    private void Run()
    {
        Save("budget.json", new
        {
            Base = "c66024dfd5905f6120c224ebeeee60e8a866ba20", CpuLimit, Admission, StraightLimit, ExitLimit,
            AdditionalRollouts = 0, ReservedRollouts = StraightLimit + ExitLimit, UserRolloutLimit = 34,
            StraightWindowSeconds, StraightPreparationSeconds, ExitWindowSeconds, StraightStartMeters, StraightOffsetMeters,
            PreparationThrottle, SpeedsKph, Throttles, SteeringModes, ExitBranchBeforeMeters = 4,
            Purpose = "Includes replay prefixes, native motion, local force grid, hashing and writing. CPU guard at each review."
        });
        Log(new { Event = "phase-reserved", CpuSeconds = Cpu, StraightLimit, ExitLimit });
        try
        {
            using var native = new ZipArchive(new FileStream(PathTo("native.zip"), FileMode.CreateNew), ZipArchiveMode.Create);
            Save("geometry.json", ReplayGeometry.Create(frame));
            Save("model.json", new { Parameters = FreeLearningExperiment.Parameters(), RearWheelParameters.Default,
                Bike = TwoContactBikeParameters.Default, Launch = LaunchParameters144.Default,
                BodyMassKg = 147, BodyInertiaKgMetersSquared = 52.72097f,
                StraightExitMeters = frame.SegmentStartMeters(1), FirstExitMeters = frame.SegmentStartMeters(4),
                ReactionSeconds = RiderController.ReactionSeconds(NativeWorld166.Rider(StartingGate.C)),
                ReviewSeconds = FreeLearningController.ReviewSeconds,
                NativeContact = "Contact144.Forces -> CrankClutch144.Prepare -> RearWheelDynamics.Evaluate" });
            foreach (int kph in SpeedsKph)
            foreach (string mode in SteeringModes)
            foreach (float throttle in Throttles)
                Straight(native, kph, mode, throttle);

            using var history = ZipFile.OpenRead($"{BaseStudy}/evidence.zip");
            var freeze = Read<JsonElement>(history, "freeze.json");
            var tuning = freeze.GetProperty("Selected").GetProperty("Tuning").Deserialize<Tuning>(Json)!;
            var oldFreeze = Study156.Read<JsonElement>($"{OriginalStudy}/evidence/freeze.json");
            var weights = oldFreeze.GetProperty("Selected").GetProperty("Weights").Deserialize<Weights>(Json)!;
            var fit = Study156.Read<Fit>($"{OriginalStudy}/evidence/fit.json");
            foreach (var name in new[] { "166-H3", "167-H3" })
            {
                var source = ReadSource(history, name);
                Exit(native, name, source, fit, weights, tuning, "original", null);
                Exit(native, name, source, fit, weights, tuning, "fixed", .5f);
                Exit(native, name, source, fit, weights, tuning, "fixed", 1);
                Exit(native, name, source, fit, weights, tuning, "adaptive166", 1);
            }
            ForceGrid();
            native.Dispose();
            Save("runs.json", trials);
            Save("run-receipt.json", new { CpuSeconds = Cpu, Rollouts = trials.Count, Budget = CpuLimit,
                ArchiveSha256 = Digest(File.ReadAllBytes(PathTo("native.zip"))) });
        }
        catch (Exception error)
        {
            Log(new { Event = "phase-aborted", CpuSeconds = Cpu, Error = error.ToString() });
            throw;
        }
    }

    private void Straight(ZipArchive native, int kph, string mode, float throttle)
    {
        var rider = NativeWorld166.Rider(StartingGate.C) with
        {
            ReferenceMeters = StraightStartMeters, OffsetMeters = (float)StraightOffsetMeters,
            TargetOffsetMeters = (float)StraightOffsetMeters, SpeedMetersPerSecond = (float)((kph - 5) / 3.6)
        };
        string id = $"AB-{kph}-{mode}-{100 * throttle:00}";
        var start = Reserve(id, "AB", mode, throttle);
        InputSeal? accepted = null;
        double? branch = null;
        double? activation = null;
        using var world = new NativeWorld166(new(Enabled: true, RequiredLaps: 2, MaximumSeconds: StraightPreparationSeconds + StraightWindowSeconds, SoloGate: StartingGate.C),
            diagnosticFrame: frame, diagnosticRider: rider, safetyHalfMeter: true,
            stopReference: frame.SegmentStartMeters(1));
        var run = world.Run(id, "K1", (input, crank, spec, _, _, _, _, _, _) =>
        {
            Guard();
            if (branch is null && input.Time >= StraightPreparationSeconds - ClockTolerance)
            {
                branch = input.Time;
                activation = branch + RiderController.ReactionSeconds(spec);
                accepted = Seal(input, crank);
            }
            if (branch is null) return Decision(input, new(0, (float)PreparationThrottle));
            double elapsed = input.Time - branch.Value;
            double direction = mode switch
            {
                "zero" => 0,
                "one-degree" => 1,
                "counter" => elapsed < .4 - ClockTolerance ? 1 : elapsed < .8 - ClockTolerance ? -1 : 0,
                _ => throw new ArgumentException("Unknown steering scenario")
            };
            return Decision(input, new((float)(direction * Math.PI / 180), throttle));
        });
        if (branch is null || activation is null || accepted is null)
            throw new InvalidOperationException(id + " did not reach an accepted target speed.");
        if (Math.Abs(accepted.Input.Rider.Speed * 3.6 - kph) > 4)
            throw new InvalidOperationException(id + " target is outside the predeclared speed band.");
        Finish(native, run, start, "AB", kph.ToString(), mode, throttle, accepted, branch.Value,
            activation.Value, originalPrefixMatches: true, originalTraceMatches: null);
    }

    private MatchedSource ReadSource(ZipArchive history, string name)
    {
        using var copy = new MemoryStream();
        using (var source = history.GetEntry(name + ".zip")!.Open()) source.CopyTo(copy);
        var hash = Digest(copy.ToArray());
        copy.Position = 0;
        using var archive = new ZipArchive(copy);
        var run = Read<Run144>(archive, "native.json");
        var rider = run.Riders.Single();
        var threshold = frame.SegmentStartMeters(4) - 4;
        var branch = rider.Decisions.Select(d => d.Choice.Time)
            .First(t => rider.Samples.First(s => s.Input.Time >= t - ClockTolerance).Input.Rider.TotalReferenceMeters >= threshold);
        var sample = rider.Samples.First(s => s.Input.Time >= branch - ClockTolerance);
        if (sample.Certificate is null or < .5 || Math.Abs(sample.Input.Rider.SlideRadians) >= Math.PI / 4)
            throw new InvalidOperationException("Historical branch has to be legal.");
        var prefix = Prefix(rider.Samples, branch);
        Log(new { Event = "history", name, ArchiveSha256 = hash, run.Fingerprint, BranchSeconds = branch,
            PrefixSha256 = prefix, Reference = sample.Input.Rider.TotalReferenceMeters });
        return new(run, branch, prefix, hash);
    }

    private void Exit(ZipArchive native, string cohort, MatchedSource source, Fit fit, Weights weights,
        Tuning tuning, string mode, float? throttle)
    {
        string id = $"C-{cohort[..3]}-{mode}" + (throttle is float drive ? $"-{100 * drive:00}" : "");
        double start = Reserve(id, "C", mode, throttle);
        var archived = source.Run.Riders.Single();
        double branch = source.BranchSeconds;
        double activation = branch + RiderController.ReactionSeconds(archived.Spec);
        double stop = branch + ExitWindowSeconds;
        Policy? original = null;
        Policy? corrective = null;
        TwoCornerPolicy? newer = null;
        InputSeal? accepted = null;
        int index = 0;
        var course = new Course(frame);
        var firstWeights = cohort.StartsWith("167", StringComparison.Ordinal) ? weights with
        {
            SpeedScale = weights.SpeedScale * tuning.FirstSpeedGain, Margin = tuning.FirstMarginMeters
        } : weights;

        using var world = new NativeWorld166(source.Run.Options with { MaximumSeconds = stop },
            diagnosticFrame: frame, diagnosticRider: archived.Spec, safetyHalfMeter: true);
        var run = world.Run(id, "K1", (input, crank, spec, track, surface, parameters, mass, inertia, _) =>
        {
            Guard();
            bool before = input.Time < branch - ClockTolerance;
            if (!before) accepted ??= Seal(input, crank);
            if (before || mode == "original")
            {
                original ??= new(fit.A, fit.B, firstWeights, spec, track, surface, parameters, mass, inertia);
                if (cohort.StartsWith("167", StringComparison.Ordinal))
                    newer ??= new(original, fit, weights, tuning, course, spec, track, surface, parameters, mass, inertia);
                var action = newer?.Choose(input, crank) ?? original.Choose(input, crank);
                var expected = archived.Decisions[index++].Choice;
                if (Math.Abs(expected.Time - input.Time) > ClockTolerance || action != expected.Action)
                    throw new InvalidOperationException(id + " did not reproduce the archived policy decision.");
                return Decision(input, action);
            }
            if (mode == "adaptive166")
            {
                corrective ??= new(fit.A, fit.B, weights, spec, track, surface, parameters, mass, inertia);
                return Decision(input, corrective.Choose(input, crank) with { Drive = throttle!.Value });
            }
            var steer = archived.Decisions[index++].Choice.Action.SteeringRadians;
            return Decision(input, new(steer, throttle!.Value));
        });

        var samples = run.Riders.Single().Samples;
        bool prefixMatches = Prefix(samples, branch) == source.PrefixSha256;
        if (!prefixMatches || accepted is null)
            throw new InvalidOperationException(id + " failed full native prefix parity.");
        bool? originalTrace = null;
        if (mode == "original")
        {
            double until = Math.Min(run.EndSeconds, source.Run.EndSeconds) - .011;
            originalTrace = Prefix(samples, until) == Prefix(archived.Samples, until);
            if (originalTrace != true)
                throw new InvalidOperationException(id + " failed original post-branch trace parity.");
        }
        Finish(native, run, start, "C", cohort, mode, throttle, accepted, branch, activation, prefixMatches, originalTrace);
    }

    private InputSeal Seal(FreeRacingInput input, CrankClutch144 crank)
    {
        var gap = new RacingLineGeometry(frame).Clearance(input.Rider);
        return new(input, crank.Omega, gap.Inner, gap.Outer);
    }

    private static string Prefix(StartSample144[] samples, double cutoff) => Digest(
        JsonSerializer.SerializeToUtf8Bytes(samples.TakeWhile(s => s.Input.Time <= cutoff + ClockTolerance), Json));

    private void Finish(ZipArchive native, Run144 run, double entered, string experiment, string cohort, string mode,
        float? drive, InputSeal accepted, double branch, double activation, bool originalPrefixMatches, bool? originalTraceMatches)
    {
        using (var payload = native.CreateEntry(run.Name + ".json", CompressionLevel.Fastest).Open())
            JsonSerializer.Serialize(payload, run, Json);
        var result = new Trial(run.Name, experiment, cohort, mode, drive, branch, activation,
            run.EndSeconds, accepted.Input.Rider.TotalReferenceMeters, accepted.Input.Rider.Speed * 3.6,
            Digest(JsonSerializer.SerializeToUtf8Bytes(accepted, Json)),
            Digest(JsonSerializer.SerializeToUtf8Bytes(run.Riders.Single().Samples
                .TakeWhile(s => s.Input.Time <= activation + ClockTolerance)
                .Select(s => new { s.Input.Rider, s.Input.Time, s.Input.Omega, s.Input.Steering,
                    s.Input.Active, s.CrankOmega, s.Powertrain, s.Forces, s.Certificate }), Json)),
            originalPrefixMatches, originalTraceMatches,
            Cpu - entered, run.NativeSteps, run.Outcome);
        trials.Add(result);
        Log(new { Event = "result", Result = result });
        Console.WriteLine($"{run.Name}  start={result.StartKph:F2}km/h  end={result.EndSeconds:F3}s  {result.Outcome}");
    }

    private double Reserve(string id, string experiment, string mode, float? throttle)
    {
        Guard();
        double start = Cpu;
        Log(new { Event = "rollout-reserved", id, experiment, mode, throttle, CpuSeconds = start });
        return start;
    }

    private void Guard()
    {
        if (Cpu >= Admission) throw new InvalidOperationException("Diagnostic CPU admission exhausted");
    }

    private static Decision144 Decision(FreeRacingInput input, FreeRacingAction action) =>
        new(new(input.Time, action, FreeActionSafety.Uncertain, false, [], 0, 0), 0);

    private void Save(string name, object data)
    {
        using var file = new FileStream(PathTo(name), FileMode.CreateNew);
        JsonSerializer.Serialize(file, data, Pretty);
    }

    private static T Read<T>(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        return JsonSerializer.Deserialize<T>(stream, Json)!;
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private void Log(object data) => File.AppendAllText(PathTo("ledger.jsonl"), JsonSerializer.Serialize(data, Json) + "\n");

    private void ForceGrid()
    {
        const float mass = 147;
        var bike = TwoContactBikeParameters.Default;
        var wheelbase = (double)bike.FrontAxleFromComMeters + bike.RearAxleFromComMeters;
        var rear = mass * 9.81 * bike.FrontAxleFromComMeters / wheelbase;
        var sample = MultiActionExperiment135.Surface(frame).SampleSurface(0, 2);
        double mu = LoadBasedTyreGrip.Budget(mass, sample, bike).EffectiveFrictionCoefficient;
        float total = (float)(mu * mass * 9.81);
        var wheel = RearWheelParameters.Default;
        var rows = new List<object>();
        foreach (var kph in SpeedsKph)
        foreach (double alpha in new[] { 0d, 1, 2, 4, 6, 10, 15, 20 })
        foreach (double kappa in new[] { 0d, .05, .10, .20, .30, .40, .50, .60, .75, 1, 1.5, 2, 3 })
        foreach (double ax in new[] { 0d, 3, 6 })
        foreach (double h in new[] { .35, .55, .75 })
        {
            Guard();
            var forward = (float)(kph / 3.6);
            var side = forward * Math.Tan(alpha * Math.PI / 180);
            var slip = kappa * Math.Sqrt(forward * forward + .25);
            double loadTransfer = mass * ax * h / wheelbase;
            double rearBudget = mu * (rear + loadTransfer);
            // Same lateral regularization as the active contact. u is local to the rear patch.
            float scale = SlipCoupledDynamics.SlipStiffnessRatio * SlipCoupledDynamics.SlipStiffnessRatio *
                (forward * forward + .25f) + (total / mass) * (total / mass) *
                SlipCoupledDynamics.MinimumLateralRelaxationSeconds * SlipCoupledDynamics.MinimumLateralRelaxationSeconds;
            var point = RearWheelDynamics.Evaluate(forward, side, (forward + slip) / wheel.WheelRadiusMeters,
                total, mass, scale, 0, 0, 0, 0, 0, wheel, rearBudget);
            var mobility = wheel.WheelRadiusMeters * wheel.WheelRadiusMeters / wheel.WheelInertiaKgMetersSquared + 1d / mass;
            double relaxation = rearBudget * mobility * wheel.MinimumContactRelaxationSeconds;
            double speedTerm = wheel.LongitudinalSlipStiffnessRatio * Math.Sqrt(forward * forward + .25);
            rows.Add(new { Kph = kph, AlphaDegrees = alpha, Kappa = kappa, Acceleration = ax, Height = h,
                LoadTransferNewtons = loadTransfer, FrontNormal = mass * 9.81 - rear - loadTransfer,
                RearNormal = rear + loadTransfer, RearBudget = rearBudget, Mu = mu,
                Fx = point.LongitudinalNewtons, Fy = point.RearLateralNewtons, point.RearUtilization,
                LateralCapacityAtFx = Math.Sqrt(Math.Max(0, rearBudget * rearBudget - Math.Pow(point.LongitudinalNewtons, 2))),
                LongitudinalFloor = relaxation, LongitudinalSpeedTerm = speedTerm });
        }
        Save("contact-grid.json", rows);
    }
}
