using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LearnedCorner166;
using LineThrottle156;
using SharedWorldSpeedway;
using Scenario = LearnedCorner166.Scenario;

namespace FastTwoCorners167;

internal sealed record Fit(Teacher A, Teacher B);

internal sealed partial class Study
{
    private const string Historical = "docs/experiments/autonomous-fast-corner";
    private static readonly JsonSerializerOptions Json = new(Study156.Json) { WriteIndented = false };
    private static readonly JsonSerializerOptions Pretty = new(Json) { WriteIndented = true };
    private readonly TrackFrame frame = NativeWorld166.Frame();
    private readonly string directory;
    private readonly Budget budget;
    private readonly Fit fit;
    private readonly Weights weights;
    private readonly Scenario[] held;
    private readonly Course course;
    private string PathTo(string name) => Path.Combine(directory, name);

    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("--diagnose" or "--study"))
        {
            Console.Error.WriteLine("Study #167 is OFF. Use --diagnose or --study with an evidence directory.");
            return 2;
        }

        bool diagnosis = args[0] == "--diagnose";
        string directory = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(directory);
        using var phaseLock = new FileStream(Path.Combine(directory, ".phase.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        double prior = diagnosis ? 0 : Read<JsonElement>(Path.Combine(directory, "diagnosis.json"))
            .GetProperty("TotalCpuSeconds").GetDouble();
        var preflightPath = Path.Combine(directory, "preflight.json");
        if (!diagnosis && File.Exists(preflightPath))
            prior += Read<JsonElement>(preflightPath).GetProperty("CpuSeconds").GetDouble();
        var budget = new Budget(directory, prior);
        string phase = diagnosis ? "diagnosis" : "study";
        Save(Path.Combine(directory, phase + ".started.json"), new { Phase = phase, PriorCpuSeconds = prior, Budget.Plan });
        budget.Log(new { Event = "phase-reserved", Phase = phase, PriorCpuSeconds = prior, Budget.Plan });
        try
        {
            var study = new Study(directory, budget);
            if (diagnosis) study.Diagnose();
            else study.TrainAndValidate();
            Save(study.PathTo(phase + ".json"), new { CpuSeconds = budget.Cpu, TotalCpuSeconds = budget.Used });
            return 0;
        }
        catch (Exception error)
        {
            budget.Log(new { Event = "phase-aborted", Phase = phase, TotalCpuSeconds = budget.Used, Error = error.ToString() });
            throw;
        }
    }

    private Study(string directory, Budget budget)
    {
        this.directory = directory;
        this.budget = budget;
        course = new(frame);
        var freeze = Read<JsonElement>($"{Historical}/evidence/freeze.json");
        string fitPath = $"{Historical}/evidence/fit.json";
        string heldPath = $"{Historical}/held.json";
        if (Hash(fitPath) != freeze.GetProperty("FitSha256").GetString() ||
            Hash(heldPath) != freeze.GetProperty("HeldSha256").GetString())
            throw new InvalidOperationException("Frozen #166 input SHA256 mismatch");
        foreach (var source in freeze.GetProperty("Sources").EnumerateObject().Where(x => x.Name != "Program.cs"))
            if (Hash($"tools/autonomous-fast-corner/{source.Name}") != source.Value.GetString())
                throw new InvalidOperationException("Frozen #166 source SHA256 mismatch: " + source.Name);
        fit = Read<Fit>(fitPath);
        held = Read<Scenario[]>(heldPath);
        weights = freeze.GetProperty("Selected").GetProperty("Weights").Deserialize<Weights>(Json)!;
        budget.Log(new { Event = "inputs", course.First, course.Second, course.Goal, frame.LapMeters,
            FitSha256 = Hash(fitPath), HeldSha256 = Hash(heldPath), FreezeSha256 = Hash($"{Historical}/evidence/freeze.json") });
    }

    private void Diagnose()
    {
        foreach (var id in new[] { "H3", "H4" })
        {
            var scenario = held.Single(x => x.Id == id);
            Execute("166-" + id, "diagnosis", scenario, Budget.DiagnosticLimit, archive: true);
        }
    }

    private Result Execute(string id, string stage, Scenario scenario, double limit, bool archive,
        Tuning? tuning = null, bool perturb = false)
    {
        budget.Guard(limit);
        double start = budget.Cpu;
        budget.Log(new { Event = "reserved", id, stage, scenario, tuning, perturb, CpuAtReservation = budget.Used, Limit = limit });
        Policy? original = null;
        TwoCornerPolicy? updated = null;
        bool disturbed = false;
        double decisionWallSeconds = 0;
        Decision144 Decide(FreeRacingInput input, CrankClutch144 crank, RiderSpec spec, TrackFrame track,
            CoreSim.TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, string variant)
        {
            budget.Guard(limit);
            double entered = budget.Cpu;
            long wallStart = Stopwatch.GetTimestamp();
            var firstWeights = tuning is null ? weights : weights with
            {
                SpeedScale = weights.SpeedScale * tuning.FirstSpeedGain,
                Margin = tuning.FirstMarginMeters
            };
            original ??= new(fit.A, fit.B, firstWeights, spec, track, surface, parameters, mass, inertia);
            long contactsBefore = original.ForecastContactQueries + (updated?.ForecastContactQueries ?? 0);
            long forecastsBefore = updated?.Forecasts ?? 0;
            long originalBefore = original.ForecastContactQueries;
            FreeRacingAction action;
            if (tuning is null) action = original.Choose(input, crank);
            else
            {
                updated ??= new(original, fit, weights, tuning, course, spec, track, surface, parameters, mass, inertia);
                action = updated.Choose(input, crank);
            }
            if (perturb && !disturbed && input.Rider.TotalReferenceMeters >= course.First.Entry + 44)
            {
                var steeringLimit = TwoContactBikeParameters.Default.MaximumSteeringRadians;
                action = action with { SteeringRadians = Math.Clamp(action.SteeringRadians + .014f, -steeringLimit, steeringLimit) };
                disturbed = true;
            }
            action.Validate();
            int queries = (int)(original.ForecastContactQueries + (updated?.ForecastContactQueries ?? 0) - contactsBefore);
            int forecasts = (int)((updated?.Forecasts ?? 0) - forecastsBefore)
                + (original.ForecastContactQueries > originalBefore ? 1 : 0);
            decisionWallSeconds += Stopwatch.GetElapsedTime(wallStart).TotalSeconds;
            return new(new(input.Time, action, FreeActionSafety.SafeInHorizon, false, [], forecasts, queries), budget.Cpu - entered);
        }

        using var world = new NativeWorld166(new(Enabled: true, RequiredLaps: 2,
            MaximumSeconds: Course.MaximumSeconds, SoloGate: scenario.Field), diagnosticFrame: frame,
            diagnosticRider: scenario.Spec, safetyHalfMeter: true, stopReference: course.Goal, postSeconds: Course.Reserve);
        var run = world.Run(id, "K1", Decide);
        long forecasts = (original?.ForecastContactQueries ?? 0) + (updated?.ForecastContactQueries ?? 0);
        var result = course.Inspect(run, scenario, archive, forecasts, budget.Cpu - start) with { DecisionWallSeconds = decisionWallSeconds };
        if (archive)
        {
            string path = PathTo(id + ".zip");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using (var payload = zip.CreateEntry("native.json").Open())
                    JsonSerializer.Serialize(payload, run, Json);
                using (var metadata = zip.CreateEntry("result.json").Open())
                    JsonSerializer.Serialize(metadata, result, Json);
            }
            budget.Log(new { Event = "archive", id, File = Path.GetFileName(path), Sha256 = Hash(path) });
        }
        result = result with { CpuSeconds = budget.Cpu - start };
        budget.Log(new { Event = "result", id, stage, result, run.NativeSteps, run.NativeContactQueries, run.NativeClutchWheelQueries });
        Console.WriteLine($"{id}: pass={result.Pass} time={result.Goal?.Seconds:F3} ref={result.EndReference:F2} "
            + $"swept={result.MinimumSwept:F3} CPU={budget.Used:F2} {result.Reason}");
        return result;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json)!;

    private static void Save(string path, object data)
    {
        using var stream = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(stream, data, Pretty);
    }
}
