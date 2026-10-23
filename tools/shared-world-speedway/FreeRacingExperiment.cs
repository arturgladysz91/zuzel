using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record FreeRacingScenario(string Name, float Speed, float Offset);
public sealed record FreeRacingSample(FreeRacingInput Input, double Inner, double Outer, float RequestedSteering, float RequestedDrive);
public sealed record FreeRacingMetrics(bool Completed, string Outcome, double? GateSeconds, double DurationSeconds,
    double EntrySpeed, double MinimumSpeed, double ExitSpeed, double MaximumBetaDegrees, double MeanDrive,
    double FullDriveFraction, int LargeTrajectoryOscillations, double DistanceMeters, double MinimumClearanceMeters,
    long NativeSteps, long PhysicsEvaluations, double CpuSeconds, string Fingerprint);
public sealed record FreeRacingRun(string Name, int PolicyId, FreeRacingPolicy Policy, FreeRacingScenario Scenario,
    FreeRacingMetrics Metrics, FreeRacingSample[] Samples, FreeRacingDecision[] Decisions, FreeRacingCommand[] Activations);
public sealed record FreeRacingEvaluation(int PolicyId, int Generation, FreeRacingPolicy Policy, FreeRacingMetrics[] Results);
public sealed record FreeRacingReservation(int Number, string Name, string Stage, bool Complete = false);
public sealed record FreeRacingTraining(FreeRacingEvaluation[] Evaluations, int IntermediatePolicyId, int BestPolicyId, FreeRacingPolicy Policy);

/// <summary>Persistent reservation before EVERY new native world, including failed/aborted attempts.</summary>
public sealed class FreeRacingBudget : IDisposable
{
    public const int MaximumRuns = 40;
    private readonly string path;
    private readonly FileStream lease;
    private readonly List<FreeRacingReservation> runs;
    public IReadOnlyList<FreeRacingReservation> Runs => runs.AsReadOnly();
    public FreeRacingBudget(string directory) {
        Directory.CreateDirectory(directory); path = Path.Combine(directory, "budget.json");
        lease = new FileStream(Path.Combine(directory, "budget.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        runs = File.Exists(path) ? JsonSerializer.Deserialize<List<FreeRacingReservation>>(File.ReadAllText(path))! : new();
    }
    public int Reserve(string name, string stage) {
        if (runs.Count >= MaximumRuns || runs.Any(r => r.Name == name)) throw new InvalidOperationException("Native run budget exhausted or run already reserved; never silently repeat.");
        var number = runs.Count + 1; runs.Add(new(number, name, stage)); Save(); return number;
    }
    public void Complete(int number) { runs[number - 1] = runs[number - 1] with { Complete = true }; Save(); }
    private void Save() {
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(runs, FreeRacingExperiment.Json));
        File.Move(path + ".tmp", path, true);
    }
    public void Dispose() => lease.Dispose();
}

public static class FreeRacingExperiment
{
    public const string BaseHead = "9a7ba4daef3e7702e1bac3e9bc9271eedeca64df";
    public const uint Seed = 1282026;
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, IncludeFields = true };
    public static readonly FreeRacingScenario[] TrainingScenarios = { new("train-16-3", 16, 3), new("train-18-6", 18, 6) };
    public static readonly FreeRacingScenario[] ValidationScenarios = { new("test-17-4", 17, 4), new("test-19-5", 19, 5), new("test-20-7", 20, 7) };
    public static WorldParameters Parameters() => CornerEntryAudit.Parameters() with { EnableTravelDirectionSteering = false, EnableFreeRacing = true };
    public static RiderSpec Rider(FreeRacingScenario scenario) => CornerEntryAudit.Rider() with { SpeedMetersPerSecond = scenario.Speed, OffsetMeters = scenario.Offset, TargetOffsetMeters = scenario.Offset };
    public static void Save(string dir, string file, object value) => File.WriteAllText(Path.Combine(dir, file), JsonSerializer.Serialize(value, Json));
    public static void SaveRun(string dir, FreeRacingRun run) {
        using var stream = new GZipStream(File.Create(Path.Combine(dir, run.Name + ".json.gz")), CompressionLevel.SmallestSize);
        JsonSerializer.Serialize(stream, run, Json);
        Save(dir, run.Name + "-metrics.json", run.Metrics);
    }
    public static FreeRacingRun ReadRun(string dir, string name) {
        using var stream = new GZipStream(File.OpenRead(Path.Combine(dir, name + ".json.gz")), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<FreeRacingRun>(stream, Json)!;
    }
    public static Dictionary<string, string> Sources() => new[] { "FreeRacingController.cs", "FreeRacingModel.cs", "FreeRacingExperiment.cs", "WorldRunner.cs", "WorldModel.cs",
        "TwoContactBikeDynamics.cs", "ContactAdapter.cs", "RearWheelDynamics.cs", "Engine500.cs", "LoadBasedTyreGrip.cs", "TrackFrame.cs" }
        .ToDictionary(n => n, n => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine("tools", "shared-world-speedway", n)))));
    public static void Run(string dir, string stage) {
        Directory.CreateDirectory(dir);
        if (stage == "freeze") {
            if (File.Exists(Path.Combine(dir, "protocol.json"))) throw new InvalidOperationException("Protocol already frozen.");
            Save(dir, "protocol.json", new { BaseHead, Seed, Parameters = Parameters(), InitialPolicy = new FreeRacingPolicy(),
                FreeRacingPolicy.Names, FreeRacingPolicy.Lower, FreeRacingPolicy.Upper, TrainingScenarios, ValidationScenarios,
                PreflightMaximumSeconds = 3.0, TrainingCandidates = 12, ScenariosPerCandidate = 2, MaximumNativeRuns = 40,
                PredictionHorizonSeconds = .9, PredictionTolerances = new { PositionMeters = .1, Speed = .2, ChiDegrees = 1, BetaDegrees = 1, SteeringDegrees = 1.5, WheelOmega = 2, YawRate = .05, ClearanceOptimismMeters = .1 },
                Gate = "Whole mechanical footprint across fixed exit plane +3m, then 1s legal continuation; 10ms upper-bound time. Boundary sweep bound, reverse, projection, beta45, energy failures precede finish.",
                Selection = "Completion count first, then worst actual gate time, sum of actual gate times; comparable 20ms times prefer higher minimum exit speed. Unsafe partial progress cannot beat a completion. Only 2/2 safe candidates update CEM; otherwise retain previous mean.",
                CEM = "Two generations of six: incumbent plus five independent normal samples. Top two 2/2-safe elites; mean and std from elites, std floor 5% of range. Initial std=18% of range. Fixed xorshift32 + Box-Muller seed; no retries or hidden evaluations.",
                PreflightGate = "Both 3s runs must remain legal, make >=10m progress, preserve physical limits and pass causal closed-loop .3/.6/.9s prediction checks before training.",
                Verdict = "PASS only if at least two of three untouched validation cases improve by >=20ms with 3/3 legal completions, no validation regression >20ms, and safe two-bend final. Otherwise PARTIAL; invalid physics/hidden path/numerical failure FAIL.",
                Sources = Sources() });
            return;
        }
        using var protocol = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "protocol.json")));
        foreach (var item in Sources()) if (protocol.RootElement.GetProperty("Sources").GetProperty(item.Key).GetString() != item.Value)
            throw new InvalidOperationException("Frozen implementation changed: " + item.Key);
        using var budget = new FreeRacingBudget(dir);
        if (stage == "preflight") { Preflight(dir, budget); return; }
        using var preflight = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "preflight.json")));
        if (!preflight.RootElement.GetProperty("Passed").GetBoolean()) throw new InvalidOperationException("Preflight gate closed. No training or validation worlds authorized.");
        if (stage == "train") { Train(dir, budget); return; }
        var training = JsonSerializer.Deserialize<FreeRacingTraining>(File.ReadAllText(Path.Combine(dir, "training.json")), Json)!;
        if (stage == "validate") {
            foreach (var scenario in ValidationScenarios) {
                Execute(dir, budget, "validation", "initial-" + scenario.Name, 0, new(), scenario);
                Execute(dir, budget, "validation", "learned-" + scenario.Name, training.BestPolicyId, training.Policy, scenario);
            }
            return;
        }
        if (stage == "final") {
            foreach (var (name, id) in new[] { ("early", 0), ("intermediate", training.IntermediatePolicyId), ("learned", training.BestPolicyId) }) {
                var policy = training.Evaluations.Single(e => e.PolicyId == id).Policy;
                Execute(dir, budget, "final", "final-" + name, id, policy, new("common-18-1", 18, 1), twoBends: true);
            }
            return;
        }
        throw new ArgumentException("free-racing directory freeze|preflight|train|validate|final");
    }

    public static FreeRacingRun Execute(string dir, FreeRacingBudget budget, string stage, string name, int id,
        FreeRacingPolicy policy, FreeRacingScenario scenario, double? shortSeconds = null, bool twoBends = false) {
        var number = budget.Reserve(name, stage); var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
        var spec = Rider(scenario); var frame = RacingLineRootCauseAudit.Frame(); var p = Parameters();
        var surface = TrackState.CreateDefault(frame.Track, new(1, 0, .5f)).Snapshot(); var boundary = new RacingLineGeometry(frame);
        var samples = new List<FreeRacingSample>(); TwoContactForces force = default; RearWheelStep wheel = default;
        RiderView before = default; string? failure = null; double? gateTime = null; double? firstEntry = null; double entrySpeed = scenario.Speed;
        double minimumSpeed = double.PositiveInfinity, maxBeta = 0, throttleIntegral = 0, fullGas = 0, minimumGap = double.PositiveInfinity;
        double lastOffset = scenario.Offset, lastExtremum = scenario.Offset; int direction = 0, oscillations = 0; double exitSpeed = 0;
        using var world = new WorldRunner(new[] { spec }, parameters: p, trackShape: RacingLineRootCauseAudit.Variant(), frozenSurface: surface,
            twoContactInitial: new(), freeRacingPolicy: policy, diagnostics: new() { Force = f => before = f.Rider, TwoContact = f => force = f.Forces, RearWheel = o => wheel = o.Step });
        var mechanical = world.MechanicalProperties(spec.Profile.Id);
        var gateReference = frame.SegmentStartMeters(twoBends ? 8 : 4) + 3;
        var gate = frame.At(gateReference, 0); var gatePoint = new MeterPoint(gate.Position.X, gate.Position.Y);
        var tangent = new MeterPoint(gate.Road.Tangent.X, gate.Road.Tangent.Y);
        double WholeGate(RiderView v) {
            var bike = BikeFootprint.Create(new(v.Position.X, v.Position.Y), v.Heading, SpeedwayBikeDimensions.Reference);
            return new[] { bike.Chassis, bike.Handlebar }.Min(c => Math.Min(MeterPoint.Dot(c.Start - gatePoint, tangent), MeterPoint.Dot(c.End - gatePoint, tangent)) - c.RadiusMeters);
        }
        void Sample(WorldSnapshot snapshot) {
            var v = snapshot.Riders[0]; var gap = boundary.Clearance(v);
            var controller = world.FreeRacing!;
            var input = controller.Input(v, snapshot.TimeSeconds, world.RearWheel(v.Id).RearWheelAngularVelocityRadPerSecond, snapshot.Steering![0].AngleRadians);
            var request = controller.Decisions.LastOrDefault()?.Action ?? controller.Active;
            samples.Add(new(input, gap.Inner, gap.Outer, request.SteeringRadians, request.Drive));
            minimumGap = Math.Min(minimumGap, Math.Min(gap.Inner, gap.Outer));
        }
        Sample(world.Snapshot);
        if (minimumGap <= 0) throw new ArgumentException("Illegal initial footprint.");
        var result = world.Run(name, .01, shortSeconds ?? (twoBends ? 40 : 20), acceptedObserver: snapshot => {
            // Capture post-activation controls used for the preceding native interval independently of the end pose.
            var previous = samples[^1];
            samples[^1] = previous with { Input = previous.Input with { Rider = before, Active = world.FreeRacing!.Active,
                Pending = world.FreeRacing.Pending.ToArray(), NextReview = world.FreeRacing.NextReview }, RequestedSteering = world.FreeRacing.Decisions[^1].Action.SteeringRadians, RequestedDrive = world.FreeRacing.Decisions[^1].Action.Drive };
            var v = snapshot.Riders[0]; var dt = wheel.Seconds; var gap = boundary.Clearance(v); var oldGap = boundary.Clearance(before);
            // Lipschitz swept lower bound for the complete capsule envelope between endpoints.
            var movement = Vector2.Distance(before.Position, v.Position);
            var sweep = .5 * (movement + SpeedwayBikeDimensions.Reference.BoundingRadiusMeters * Math.Abs(BikeAngles.Wrap(v.Heading - before.Heading)));
            var sweptGap = Math.Min(Math.Min(gap.Inner, gap.Outer), Math.Min(oldGap.Inner, oldGap.Outer)) - sweep;
            minimumGap = Math.Min(minimumGap, sweptGap);
            if (sweptGap < 0) failure = "boundary or uncertified boundary sweep";
            var progress = v.TotalReferenceMeters - before.TotalReferenceMeters;
            if (Math.Abs(progress) > movement * 1.5 + .1) failure = "projection discontinuity";
            if (Vector2.Dot(v.Velocity, v.Road.Tangent) <= 0 || progress < -.02) failure = "reverse progress";
            if (Math.Abs(v.SlideRadians) > Math.PI / 4) failure = "loss of control beta45";
            if (Math.Abs(force.SteeringAfter) > 35 * Math.PI / 180 + 1e-6 || Math.Abs(force.SteeringAfter - force.SteeringBefore) / dt > Math.PI / 2 + 1e-4
                || force.TravelSteering is not null || force.SteeringControl is not null) failure = "actuator/controller invariant";
            var energy = TwoContactBikeAudit.BodyKinetic(v, mechanical.MassKg, mechanical.InertiaKgMetersSquared)
                - TwoContactBikeAudit.BodyKinetic(before, mechanical.MassKg, mechanical.InertiaKgMetersSquared)
                + .45 * (wheel.AfterOmega * wheel.AfterOmega - wheel.BeforeOmega * wheel.BeforeOmega);
            if (energy - wheel.EngineWorkJoules > .05 + 1e-6 * Math.Max(TwoContactBikeAudit.BodyKinetic(before, mechanical.MassKg, mechanical.InertiaKgMetersSquared), TwoContactBikeAudit.BodyKinetic(v, mechanical.MassKg, mechanical.InertiaKgMetersSquared))) failure = "energy excess";
            if (firstEntry is null && v.TotalReferenceMeters >= frame.SegmentStartMeters(1)) { firstEntry = snapshot.TimeSeconds; entrySpeed = v.Speed; }
            if (firstEntry is not null) minimumSpeed = Math.Min(minimumSpeed, v.Speed);
            maxBeta = Math.Max(maxBeta, Math.Abs(v.SlideRadians) * 180 / Math.PI);
            throttleIntegral += before.Drive * dt; if (before.Drive >= .99) fullGas += dt;
            // Large lateral reversals require >=1m excursions, not steering sign changes or a target line.
            var newDirection = Math.Sign(v.Road.OffsetMeters - lastOffset);
            if (newDirection != 0 && newDirection != direction && Math.Abs(lastOffset - lastExtremum) >= 1) { if (direction != 0) oscillations++; direction = newDirection; lastExtremum = lastOffset; }
            if (direction == 0 && newDirection != 0) direction = newDirection; lastOffset = v.Road.OffsetMeters;
            if (failure is null && gateTime is null && v.TotalReferenceMeters >= gateReference && WholeGate(v) >= 0) { gateTime = snapshot.TimeSeconds; exitSpeed = v.Speed; }
            Sample(snapshot);
        }, stopOnCrash: true, diagnosticStop: snapshot => failure is not null || gateTime is { } t && snapshot.TimeSeconds >= t + 1 - 1e-10 || snapshot.Riders[0].Speed < .5);
        var completed = failure is null && gateTime is not null && result.TimeSeconds >= gateTime + 1 - 1e-8;
        var outcome = failure ?? (completed ? "completed with legal continuation" : shortSeconds is not null ? "preflight prefix" : "no progress or timeout");
        if (!double.IsFinite(minimumSpeed)) minimumSpeed = samples.Min(s => (double)s.Input.Rider.Speed);
        var metrics = new FreeRacingMetrics(completed, outcome, failure is null ? gateTime : null, result.TimeSeconds, entrySpeed, minimumSpeed,
            gateTime is null ? result.Riders[0].Speed : exitSpeed, maxBeta, throttleIntegral / result.TimeSeconds, fullGas / result.TimeSeconds, oscillations,
            result.Riders[0].DistanceMeters, minimumGap, result.Work.SolverCalls,
            result.Work.RearWheelForceEvaluations + result.Work.RearWheelBoundaryEvaluations + world.FreeRacing!.ForceEvaluations,
            (process.TotalProcessorTime - cpu).TotalSeconds, result.Fingerprint);
        var run = new FreeRacingRun(name, id, policy, scenario, metrics, samples.ToArray(), world.FreeRacing.Decisions.ToArray(), world.FreeRacing.Activations.ToArray());
        SaveRun(dir, run); budget.Complete(number);
        Console.WriteLine($"native {number}/40 {name}: {outcome}, gate={metrics.GateSeconds}, t={metrics.DurationSeconds:F2}, gap={minimumGap:F3}");
        return run;
    }

    private static void Preflight(string dir, FreeRacingBudget budget) {
        var records = new List<object>(); var passed = true;
        foreach (var scenario in TrainingScenarios) {
            var run = Execute(dir, budget, "preflight", "preflight-" + scenario.Name, 0, new(), scenario, shortSeconds: 3);
            var legal = run.Metrics.Outcome == "preflight prefix" && run.Metrics.DistanceMeters >= 10;
            var spec = Rider(scenario); var frame = RacingLineRootCauseAudit.Frame(); var surface = TrackState.CreateDefault(frame.Track, new(1, 0, .5f)).Snapshot();
            // Several true continuations of the NEW closed-loop policy, including pending actions.
            foreach (var origin in new[] { .1, .6, 1.5 }) foreach (var horizon in new[] { .3, .6, .9 }) {
                var start = run.Samples.SingleOrDefault(s => Math.Abs(s.Input.Time - origin) < 1e-8);
                var end = run.Samples.SingleOrDefault(s => Math.Abs(s.Input.Time - origin - horizon) < 1e-8);
                if (start is null || end is null) { passed = false; continue; }
                var prediction = FreeRacingModel.Predict(start.Input, spec, frame, surface, Parameters(), 147, 52.72097f, horizon, closedLoop: run.Policy);
                var point = prediction.Points[^1]; var actual = end.Input;
                var xy = Vector2.Distance(point.Rider.Position, actual.Rider.Position);
                var speed = Math.Abs(point.Rider.Speed - actual.Rider.Speed);
                var chi = Math.Abs(BikeAngles.Wrap(Math.Atan2(point.Rider.Velocity.Y, point.Rider.Velocity.X) - Math.Atan2(actual.Rider.Velocity.Y, actual.Rider.Velocity.X))) * 180 / Math.PI;
                var beta = Math.Abs(BikeAngles.Wrap(point.Rider.SlideRadians - actual.Rider.SlideRadians)) * 180 / Math.PI;
                var steer = Math.Abs(point.Steering - actual.Steering) * 180 / Math.PI; var omega = Math.Abs(point.Omega - actual.Omega);
                var yaw = Math.Abs(point.Rider.AngularVelocity - actual.Rider.AngularVelocity);
                var optimism = Math.Max(point.Inner - end.Inner, point.Outer - end.Outer);
                var ok = xy <= .1 && speed <= .2 && chi <= 1 && beta <= 1 && steer <= 1.5 && omega <= 2 && yaw <= .05 && optimism <= .1;
                records.Add(new { Scenario = scenario.Name, Origin = origin, Horizon = horizon, XY = xy, Speed = speed, ChiDegrees = chi, BetaDegrees = beta, SteeringDegrees = steer, Omega = omega, YawRate = yaw, ClearanceOptimism = optimism, Passed = ok });
                passed &= ok;
            }
            passed &= legal;
            if (!passed) break; // Hard gate: no spending the remaining native budget on a broken foundation.
        }
        Save(dir, "preflight.json", new { Passed = passed, Records = records, NativeRuns = budget.Runs.Count });
        Console.WriteLine("Preflight: " + (passed ? "PASS" : "STOP — gate closed"));
    }

    public static IOrderedEnumerable<FreeRacingEvaluation> Rank(IEnumerable<FreeRacingEvaluation> evaluations) => evaluations
        .OrderByDescending(e => e.Results.Count(r => r.Completed))
        .ThenBy(e => e.Results.All(r => r.Completed) ? Math.Round(e.Results.Max(r => r.GateSeconds!.Value) / .02) : double.PositiveInfinity)
        .ThenBy(e => e.Results.All(r => r.Completed) ? Math.Round(e.Results.Sum(r => r.GateSeconds!.Value) / .02) : double.PositiveInfinity)
        .ThenByDescending(e => e.Results.All(r => r.Completed) ? e.Results.Min(r => r.ExitSpeed) : 0).ThenBy(e => e.PolicyId);

    public sealed class RandomStream(uint seed)
    {
        private uint state = seed != 0 ? seed : throw new ArgumentException("Nonzero seed required.");
        private double Uniform() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (state + .5) / 4294967296d; }
        public double Normal() => Math.Sqrt(-2 * Math.Log(Uniform())) * Math.Cos(2 * Math.PI * Uniform());
    }
    private static void Train(string dir, FreeRacingBudget budget) {
        var random = new RandomStream(Seed); var all = new List<FreeRacingEvaluation>(); var mean = new FreeRacingPolicy().Values;
        var std = FreeRacingPolicy.Lower.Select((v, i) => .18 * (FreeRacingPolicy.Upper[i] - v)).ToArray();
        var incumbent = new FreeRacingPolicy(); var middle = 0;
        for (var generation = 0; generation < 2; generation++) {
            var population = new List<FreeRacingEvaluation>();
            for (var candidate = 0; candidate < 6; candidate++) {
                var id = generation * 6 + candidate;
                var policy = candidate == 0 ? incumbent : FreeRacingPolicy.From(mean.Select((m, i) => Math.Clamp(m + std[i] * random.Normal(), FreeRacingPolicy.Lower[i], FreeRacingPolicy.Upper[i])).ToArray());
                var results = TrainingScenarios.Select(s => Execute(dir, budget, "training", $"policy-{id:D2}-{s.Name}", id, policy, s).Metrics).ToArray();
                var evaluation = new FreeRacingEvaluation(id, generation, policy, results); all.Add(evaluation); population.Add(evaluation);
                Save(dir, "training-progress.json", all);
            }
            var elites = Rank(population.Where(e => e.Results.All(r => r.Completed))).Take(2).ToArray();
            if (elites.Length > 0) {
                for (var i = 0; i < mean.Length; i++) {
                    mean[i] = elites.Average(e => e.Policy.Values[i]);
                    std[i] = Math.Max(.05 * (FreeRacingPolicy.Upper[i] - FreeRacingPolicy.Lower[i]), Math.Sqrt(elites.Average(e => Math.Pow(e.Policy.Values[i] - mean[i], 2))));
                }
                incumbent = Rank(all.Where(e => e.Results.All(r => r.Completed))).First().Policy;
            }
            if (generation == 0) middle = Rank(all).First().PolicyId;
            Save(dir, $"generation-{generation}.json", new { Mean = mean, StandardDeviation = std, EliteIds = elites.Select(e => e.PolicyId).ToArray() });
        }
        var best = Rank(all).First();
        Save(dir, "training.json", new FreeRacingTraining(all.ToArray(), middle, best.PolicyId, best.Policy));
        Save(dir, "learned-policy.json", new { Frozen = true, BestPolicyId = best.PolicyId, Policy = best.Policy, TrainingSafeOnBoth = best.Results.All(r => r.Completed), Seed });
    }
}
