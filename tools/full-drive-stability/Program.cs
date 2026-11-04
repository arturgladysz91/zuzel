using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using LineThrottle156;
using SharedWorldSpeedway;

namespace FullDrive169;

internal sealed record NativeResult(string Id, string Stage, string Cohort, string Variant, string Control, float Drive,
    double StepSeconds, double BranchSeconds, double ActivationSeconds, double StartKph, double EndSeconds,
    double EndReference, double PeakBetaDegrees, double PeakYaw, double MinimumSwept, double MinimumFrontNormal,
    int UnloadedSteps, string PreActivationSha256, bool? ReproducesArchive, string Outcome,
    double CpuSeconds, int NativeSteps, long Forecasts, long ForecastContactQueries)
{
    public bool Legal => Outcome == "experiment timeout";
}

internal sealed partial class Study
{
    private const string Archive168 = "docs/experiments/throttle-contact-audit/168-dowody.zip";
    private const string Base = "84f193f9a3538a88841dad6dbfd9b8f4345c898c";
    private const double BudgetCpuSeconds = 300;
    private const double AdmissionCpuSeconds = 270;
    private const double ReactionSeconds = .24;
    private const double StraightBranchSeconds = .8;
    private const double StraightEndSeconds = 2.8;
    private const double ExitWindowSeconds = 1.4;
    private const double Epsilon = 1e-8;
    private const float Full = 1;
    private static readonly int[] Speeds = [60, 70, 80];
    private static readonly double[] Pulses = [.25, .5];
    private static readonly PhysicsVariant Original = PhysicsVariant.Original;
    private static readonly PhysicsVariant[] Transfers = [new("h035", .35), new("h055", .55), new("h075", .75)];
    private static readonly PhysicsVariant[] Tyres =
        [new("separated", Tyre: TyreVariant.Separated), new("lateral", Tyre: TyreVariant.LateralAllocation)];
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    private static readonly JsonSerializerOptions Pretty = new(Json) { WriteIndented = true };

    private readonly TrackFrame frame = NativeWorld169.Frame();
    private readonly Process clock = Process.GetCurrentProcess();
    private readonly List<NativeResult> results = [];
    private readonly string directory;
    private double Cpu => clock.TotalProcessorTime.TotalSeconds;
    private string At(string path) => Path.Combine(directory, path);

    private Study(string directory) => this.directory = directory;

    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("--run" or "--verify-local"))
        {
            Console.Error.WriteLine("Study #169 is OFF. Use --run with a fresh evidence directory.");
            return 2;
        }
        var directory = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(directory);
        using var mutex = new FileStream(Path.Combine(directory, ".study.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        var study = new Study(directory);
        if (args[0] == "--run") study.Run();
        else study.VerifyLocal();
        return 0;
    }

    private void Run()
    {
        Save("budget.json", new { Base, BudgetCpuSeconds, AdmissionCpuSeconds, MaxA = 15, MaxB = 11, MaxC = 10,
            MaxD = 16, MaxWorlds = 52, UserMaxWorlds = 58, Speeds, Pulses, Transfers, Tyres,
            ReactionSeconds, ReviewSeconds = FreeLearningController.ReviewSeconds, StraightBranchSeconds,
            StraightEndSeconds, ExitWindowSeconds,
            BGate = "At least one probe changes Fx, Fy or front capacity by >=50 N.",
            D3Gate = "D1 and D2 each restore legality in at least one fixed D arm.",
            BSelection = "Max legal count without complete unloading, then max legal count, then lowest worst beta.",
            CSelection = "Lateral allocation if numerically admissible. Separated is the structural control.",
            DTail = "50% measured on most informative adaptive physics, ranked by completions then worst beta." });
        Log(new { Event = "phase-reserved", CpuSeconds = Cpu });
        try
        {
            using var history = ZipFile.OpenRead(Archive168);
            using var copy = new MemoryStream();
            using (var source = history.GetEntry("native.zip")!.Open()) source.CopyTo(copy);
            copy.Position = 0;
            using var archive = new ZipArchive(copy);
            Save("sources.json", new { Base, History = Archive168, HistorySha256 = Hash(File.ReadAllBytes(Archive168)),
                Proof = "D0 and A reproduction compare the full trajectory fingerprint. D physics branches at first new gas activation." });
            Save("model.json", new { BodyMassKg = 147, BodyInertiaKgMetersSquared = 52.72097f,
                RearWheelParameters.Default, Bike = TwoContactBikeParameters.Default, Launch = LaunchParameters144.Default,
                Material = TyreMaterial140.Reference, Parameters = FreeLearningExperiment.Parameters(),
                StraightExit = frame.SegmentStartMeters(1), FirstExit = frame.SegmentStartMeters(4) });
            Save("geometry.json", ReplayGeometry.Create(frame));

            using var native = new ZipArchive(new FileStream(At("native.zip"), FileMode.CreateNew), ZipArchiveMode.Create);
            foreach (int speed in Speeds)
            foreach (double pulse in Pulses)
            {
                Straight(native, "A", Original, speed, pulse, feedback: false);
                Straight(native, "A", Original, speed, pulse, feedback: true);
            }
            Straight(native, "A", Original, 70, 1, feedback: false, replay: Read<Run144>(archive, "AB-70-counter-100.json"));
            foreach (double dt in new[] { .005, .02 }) Straight(native, "A", Original, 70, .5, feedback: true, dt);

            var probes = LoadProbes(archive);
            if (!probes) throw new InvalidOperationException("B0Ă˘â‚¬â€śB3 did not clear the preregistered load probe.");
            foreach (var variant in Transfers)
            foreach (int speed in Speeds)
                Straight(native, "B", variant, speed, .5, feedback: false);
            foreach (double dt in new[] { .005, .02 }) Straight(native, "B", Transfers[1], 70, .5, feedback: false, dt);

            ContactGrid();
            foreach (var variant in Tyres)
            {
                foreach (int speed in Speeds) Straight(native, "C", variant, speed, .5, feedback: false);
                foreach (double dt in new[] { .005, .02 }) Straight(native, "C", variant, 70, .5, feedback: false, dt);
            }
            Exits(native, archive);
            native.Dispose();
            Save("trials.json", results);
            Save("receipt.json", new { CpuSeconds = Cpu, Rollouts = results.Count, BudgetCpuSeconds,
                ArchiveSha256 = Hash(File.ReadAllBytes(At("native.zip"))) });
        }
        catch (Exception error)
        {
            Log(new { Event = "phase-aborted", CpuSeconds = Cpu, Error = error.ToString() });
            throw;
        }
    }

    private void Straight(ZipArchive archive, string stage, PhysicsVariant variant, int kph, double pulse,
        bool feedback, double step = .01, Run144? replay = null)
    {
        string mode = replay is not null ? "replay168" : feedback ? "feedback" : "pulse";
        string id = FormattableString.Invariant($"{stage}-{variant.Name}-{kph}-{pulse:0.00}-{mode}-{1000*step:0}");
        var entered = Reserve(stage, id, variant);
        var spec = NativeWorld169.Rider(StartingGate.C) with
        {
            ReferenceMeters = -25, OffsetMeters = 5, TargetOffsetMeters = 5, SpeedMetersPerSecond = (float)((kph - 5) / 3.6)
        };
        double branch = StraightBranchSeconds;
        double activation = branch + ReactionSeconds;
        var regulator = new Stabilizer169(5, variant, activation);
        var prefix = replay?.Riders.Single().Decisions;
        int decision = 0;
        using var world = new NativeWorld169(new(Enabled: true, RequiredLaps: 2, MaximumSeconds: StraightEndSeconds,
            StepSeconds: step, SoloGate: StartingGate.C), diagnosticFrame: frame, diagnosticRider: spec,
            safetyHalfMeter: true, stopReference: frame.SegmentStartMeters(1), experiment: variant, experimentFrom: activation);
        var run = world.Run(id, "K1", (input, crank, rider, track, surface, parameters, mass, inertia, _) =>
        {
            Guard();
            if (prefix is not null) return Replay(input, prefix[decision++]);
            if (input.Time < branch - Epsilon) return Command(input, 0, .5f);
            if (input.Time < branch + FreeLearningController.ReviewSeconds - Epsilon)
            {
                regulator.Observe(input);
                return Command(input, (float)(pulse * Math.PI / 180), Full);
            }
            return feedback
                ? regulator.Choose(input, crank, rider, track, surface, parameters, mass, inertia, Full, world.AcceptedAx)
                : Command(input, 0, Full);
        });
        SaveTrial(archive, run, stage, kph.ToString(), variant, mode, Full, branch, activation, entered,
            replay is null ? null : run.Fingerprint == replay.Fingerprint);
    }

    private void Exits(ZipArchive native, ZipArchive history)
    {
        bool Usable(PhysicsVariant v) => results.Where(q => q.Variant == v.Name)
            .All(q => !q.Outcome.StartsWith("technical invariant", StringComparison.Ordinal));
        var b = Transfers.Where(Usable)
            .OrderByDescending(v => Results(v).Count(q => q.Legal && q.UnloadedSteps == 0))
            .ThenByDescending(v => Results(v).Count(q => q.Legal))
            .ThenBy(v => Results(v).Max(q => q.PeakBetaDegrees)).FirstOrDefault();
        var c = Tyres.Reverse().FirstOrDefault(Usable);
        if (b is null || c is null) throw new InvalidOperationException("No numerically admissible B or C arm.");

        IEnumerable<NativeResult> Results(PhysicsVariant v) =>
            results.Where(q => q.Variant == v.Name && q.StepSeconds == .01 && q.Stage == "B");

        var variants = new List<PhysicsVariant> { Original, b, c };
        var cohorts = new[] { "166", "167" };
        foreach (var variant in variants)
        foreach (var cohort in cohorts)
            Exit(native, history, cohort, variant, feedback: false, Full);

        bool restores(PhysicsVariant v) =>
            results.Any(q => q.Stage == "D" && q.Variant == v.Name && q.Legal && q.UnloadedSteps == 0 &&
                results.Any(o => o.Stage == "D" && o.Cohort == q.Cohort && o.Variant == Original.Name && !o.Legal));
        bool interaction = restores(b) && restores(c);
        if (interaction)
        {
            var combined = new PhysicsVariant("combined", b.HeightMeters, c.Tyre);
            foreach (var cohort in cohorts) Exit(native, history, cohort, combined, feedback: false, Full);
        }

        foreach (var variant in variants)
        foreach (var cohort in cohorts)
            Exit(native, history, cohort, variant, feedback: true, Full);

        var final = variants.OrderByDescending(v => Adaptive(v).Count(q => q.Legal && q.UnloadedSteps == 0))
            .ThenBy(v => Adaptive(v).Max(q => q.PeakBetaDegrees)).First();
        IEnumerable<NativeResult> Adaptive(PhysicsVariant v) =>
            results.Where(q => q.Stage == "D" && q.Control == "feedback" && q.Variant == v.Name);
        foreach (var cohort in cohorts) Exit(native, history, cohort, final, feedback: true, .5f);
        Save("selection.json", new { B = b, C = c, Interaction = interaction, SamePhysicsThrottlePair = final });
    }

    private void Exit(ZipArchive native, ZipArchive history, string cohort, PhysicsVariant variant, bool feedback, float throttle)
    {
        string mode = feedback ? "feedback" : "fixed";
        string id = FormattableString.Invariant($"D-{cohort}-{variant.Name}-{mode}-{100*throttle:0}");
        double entered = Reserve("D", id, variant);
        var baseline = Read<Run144>(history, $"C-{cohort}-fixed-100.json");
        var original = Read<Run144>(history, $"C-{cohort}-original.json");
        var source = original.Riders.Single();
        double threshold = frame.SegmentStartMeters(4) - 4;
        double branch = source.Decisions.Select(d => d.Choice.Time)
            .First(t => SampleAt(source.Samples, t).Input.Rider.TotalReferenceMeters >= threshold);
        double activation = branch + ReactionSeconds;
        double end = branch + ExitWindowSeconds;
        int index = 0;
        var regulator = new Stabilizer169(3.2, variant, activation);
        using var world = new NativeWorld169(original.Options with { MaximumSeconds = end },
            diagnosticFrame: frame, diagnosticRider: source.Spec, safetyHalfMeter: true,
            experiment: variant, experimentFrom: activation);
        var run = world.Run(id, "K1", (input, crank, spec, track, surface, parameters, mass, inertia, _) =>
        {
            Guard();
            if (input.Time < branch - Epsilon)
            {
                regulator.Observe(input);
                return Replay(input, source.Decisions[index++]);
            }
            if (feedback) return regulator.Choose(input, crank, spec, track, surface, parameters, mass, inertia,
                throttle, world.AcceptedAx);
            var d = Replay(input, source.Decisions[index++]);
            return Command(input, d.Choice.Action.SteeringRadians, throttle);
        });
        string prefix = PhysicalPrefix(run.Riders.Single().Samples, activation);
        if (prefix != PhysicalPrefix(baseline.Riders.Single().Samples, activation))
            throw new InvalidOperationException(id + " did not reproduce the complete physical prefix.");
        bool? reproduced = !variant.Active && !feedback && throttle == Full ? run.Fingerprint == baseline.Fingerprint : null;
        SaveTrial(native, run, "D", cohort, variant, mode, throttle, branch, activation, entered, reproduced);
    }

    private double Reserve(string stage, string id, PhysicsVariant variant)
    {
        Guard();
        int count = results.Count(q => q.Stage == stage);
        int limit = stage switch { "A" => 15, "B" => 11, "C" => 10, "D" => 16, _ => 0 };
        if (count >= limit || results.Count >= 52) throw new InvalidOperationException("Native count admission exhausted.");
        double entered = Cpu;
        Log(new { Event = "reserved", stage, id, variant, CpuSeconds = entered });
        return entered;
    }

    private void SaveTrial(ZipArchive archive, Run144 run, string stage, string cohort, PhysicsVariant variant, string mode,
        float throttle, double branch, double activation, double entered, bool? reproduced)
    {
        using (var payload = archive.CreateEntry(run.Name + ".json", CompressionLevel.Fastest).Open())
            JsonSerializer.Serialize(payload, run, Json);
        var samples = run.Riders.Single().Samples;
        var post = samples.Where(q => q.Input.Time > activation + Epsilon).ToArray();
        if (post.Length == 0) throw new InvalidOperationException(run.Outcome);
        var result = new NativeResult(run.Name, stage, cohort, variant.Name, mode, throttle, run.Options.StepSeconds,
            branch, activation, SampleAt(samples, branch).Input.Rider.Speed * 3.6, run.EndSeconds,
            samples[^1].Input.Rider.TotalReferenceMeters,
            post.Max(q => Math.Abs(q.Input.Rider.SlideRadians)) * 180 / Math.PI,
            post.Max(q => Math.Abs(q.Input.Rider.AngularVelocity)), post.Min(q => q.Certificate ?? 0),
            post.Min(q => q.Forces!.Value.TyreLoads!.FrontNormalNewtons),
            post.Count(q => q.Forces?.AxleLoad is { RearFraction: 0 or 1 }),
            PhysicalPrefix(samples, activation), reproduced, run.Outcome, Cpu - entered, run.NativeSteps,
            run.Forecasts, run.ForecastContactQueries);
        results.Add(result);
        Log(new { Event = "result", Result = result });
        Console.WriteLine($"{result.Id}  ĂŽË›={result.PeakBetaDegrees:F2}  s={result.EndReference:F2}  {result.Outcome}");
        if (reproduced == false) throw new InvalidOperationException("Exact #168 reproduction failed.");
    }

    private static StartSample144 SampleAt(StartSample144[] samples, double t) =>
        samples.First(q => q.Input.Time >= t - Epsilon);

    private static string PhysicalPrefix(StartSample144[] samples, double cutoff) => Hash(JsonSerializer.SerializeToUtf8Bytes(
        samples.TakeWhile(q => q.Input.Time <= cutoff + Epsilon).Select(q => new { q.Input.Rider, q.Input.Time,
            q.Input.Omega, q.Input.Steering, q.Input.Active, q.CrankOmega, q.Powertrain, q.Forces, q.Certificate }), Json));

    private static Decision144 Replay(FreeRacingInput input, Decision144 record)
    {
        if (Math.Abs(input.Time - record.Choice.Time) > Epsilon)
            throw new InvalidOperationException("Archived review is not on the native clock.");
        return Command(input, record.Choice.Action.SteeringRadians, record.Choice.Action.Drive);
    }

    private static Decision144 Command(FreeRacingInput input, float steering, float throttle) =>
        new(new(input.Time, new(steering, throttle), FreeActionSafety.Uncertain, false, [], 0, 0), 0);

    private void Guard()
    {
        if (Cpu >= AdmissionCpuSeconds) throw new InvalidOperationException("CPU admission exhausted.");
    }

    private static T Read<T>(ZipArchive archive, string path)
    {
        using var stream = archive.GetEntry(path)!.Open();
        return JsonSerializer.Deserialize<T>(stream, Json)!;
    }

    private void Save(string name, object data)
    {
        using var stream = new FileStream(At(name), FileMode.CreateNew);
        JsonSerializer.Serialize(stream, data, Pretty);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private void Log(object data) => File.AppendAllText(At("ledger.jsonl"), JsonSerializer.Serialize(data, Json) + "\n");
}
