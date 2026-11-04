using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using SharedWorldSpeedway;

namespace FullDrive169;

internal sealed partial class Study
{
    private void VerifyLocal()
    {
        double prior = ReadReceipt().GetProperty("CpuSeconds").GetDouble();
        if (prior + Cpu >= AdmissionCpuSeconds)
            throw new InvalidOperationException("Insufficient diagnostic verification budget.");
        Save("verification.started.json", new { PriorCpuSeconds = prior, NativeWorlds = 0 });
        Log(new { Event = "verification-reserved", PriorCpuSeconds = prior });
        using var archive = ZipFile.OpenRead(At("native.zip"));
        var frames = new (string Id, PhysicsVariant Variant)[]
        {
            ("A-original-70-0.50-feedback-10", Original),
            ("B-h035-70-0.50-pulse-10", Transfers[0]), ("B-h055-70-0.50-pulse-10", Transfers[1]),
            ("B-h075-70-0.50-pulse-10", Transfers[2]),
            ("C-separated-70-0.50-pulse-10", Tyres[0]), ("C-lateral-70-0.50-pulse-10", Tyres[1]),
            ("D-167-combined-fixed-100", new("combined", .55, TyreVariant.LateralAllocation))
        };
        var parameters = FreeLearningExperiment.Parameters();
        var surface = MultiActionExperiment135.Surface(frame);
        var trials = JsonSerializer.Deserialize<NativeResult[]>(File.ReadAllBytes(At("trials.json")), Json)!;
        var rows = new List<object>();
        foreach (var (id, variant) in frames)
        {
            var trial = trials.SingleOrDefault(x => x.Id == id);
            if (trial is null) continue;
            var run = Read<Run144>(archive, id + ".json").Riders.Single();
            var sample = SampleAt(run.Samples, trial.ActivationSeconds + .05);
            var priorSample = run.Samples[Array.IndexOf(run.Samples, sample) - 1];
            double ax = Contact169.Acceleration(priorSample.Input.Rider, sample.Input.Rider,
                (float)(sample.Input.Time - priorSample.Input.Time));
            foreach (var step in new[] { .02, .01, .005, .0025 })
            {
                if (prior + Cpu >= AdmissionCpuSeconds)
                    throw new InvalidOperationException("Verification CPU admission exhausted.");
                double omega = sample.CrankOmega;
                var prediction = Predictor169.Predict(sample.Input, run.Spec, frame, surface, parameters,
                    147, 52.72097f, .4, powertrain: new(omega: sample.CrankOmega),
                    observeCrank: (_, w) => omega = w, experiment: variant, acceptedAx: ax, stepSeconds: step);
                var end = prediction.Points[^1];
                var start = prediction.Points[0];
                rows.Add(new { id, StepSeconds = step, InitialAx = ax, InitialTime = start.Time,
                    EndKph = end.Rider.Speed * 3.6, EndBetaDegrees = end.Rider.SlideRadians * 180 / Math.PI,
                    EndYaw = end.Rider.AngularVelocity, EndOmega = end.Omega, EndCrankOmega = omega,
                    Distance = Vector2.Distance(start.Rider.Position, end.Rider.Position), prediction.ForceEvaluations });
            }
        }
        Save("local-convergence.json", rows);
        Save("verification.json", new { CpuSeconds = Cpu, NativeWorlds = 0, Rows = rows.Count,
            Scope = "Short predictor from the same accepted state, pending and shaft. Real Box2D refinement is reported separately." });
        Log(new { Event = "verification-complete", CpuSeconds = Cpu, Rows = rows.Count });
    }

    private JsonElement ReadReceipt() => JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(At("receipt.json")), Json);
}
