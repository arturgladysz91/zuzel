using System.Diagnostics;
using System.Numerics;
using CoreSim;
using SharedWorldSpeedway;

// Saved-state diagnostics only: never constructs WorldRunner/ContactAdapter or a native world.
var dir = args[0]; var run = FreeRacingExperiment.ReadRun(dir, "preflight-train-16-3");
var frame = RacingLineRootCauseAudit.Frame(); var spec = FreeRacingExperiment.Rider(run.Scenario);
var surface = TrackState.CreateDefault(frame.Track, new(1, 0, .5f)).Snapshot(); var p = FreeRacingExperiment.Parameters();
var process = Process.GetCurrentProcess(); var start = process.TotalProcessorTime; long queries = 0;
var records = new List<object>(); var errors = new List<double>(); double maxRate = 0, maxFront = 0, maxRear = 0;
for (var i = 0; i < run.Samples.Length - 1; i++) {
    var s = run.Samples[i]; var next = run.Samples[i + 1]; var input = s.Input; var dt = (float)(next.Input.Time - input.Time);
    var wheel = new RearWheelState(input.Omega * .33); var steering = new TwoContactBikeState(input.Steering);
    var f = TwoContactBikeDynamics.Forces(input.Rider, spec, frame, surface, p, input.Time, 147, wheel, steering, dt, steeringOverride: input.Active.SteeringRadians, inertia: 52.72097f); queries++;
    maxRate = Math.Max(maxRate, Math.Abs(f.SteeringAfter - input.Steering) / dt);
    maxFront = Math.Max(maxFront, Math.Abs(f.FrontLateral) / f.TyreLoads!.FrontCapacityNewtons);
    maxRear = Math.Max(maxRear, Math.Sqrt((double)f.RearLongitudinal * f.RearLongitudinal + (double)f.RearLateral * f.RearLateral) / f.TyreLoads.RearCapacityNewtons);
    errors.Add(Math.Abs(f.SteeringAfter - next.Input.Steering));
    errors.Add(Math.Abs(wheel.Prepared.AfterOmega - next.Input.Omega));
}
foreach (var origin in new[] { .1, .6, 1.5 }) foreach (var horizon in new[] { .3, .6, .9 }) {
    var s = run.Samples.SingleOrDefault(s => Math.Abs(s.Input.Time - origin) < 1e-8);
    var end = run.Samples.SingleOrDefault(s => Math.Abs(s.Input.Time - origin - horizon) < 1e-8);
    if (s is null || end is null) continue;
    var prediction = FreeRacingModel.Predict(s.Input, spec, frame, surface, p, 147, 52.72097f, horizon, closedLoop: run.Policy);
    queries += prediction.ForceEvaluations;
    records.Add(new { Origin = origin, Horizon = horizon, prediction.ForceEvaluations, MaximumPositionErrorMeters = prediction.Points.Max(point => {
        var native = run.Samples.FirstOrDefault(s => Math.Abs(s.Input.Time - point.Time) < 1e-8);
        return native is null ? 0 : Vector2.Distance(point.Rider.Position, native.Input.Rider.Position);
    }) });
}
// Recompute each observed choice, using only the then-accepted state/pending queue.
// Already requested action at this exact observation must be removed before recomputing it.
foreach (var decision in run.Decisions) {
    var input = run.Samples.Single(s => Math.Abs(s.Input.Time - decision.Time) < 1e-8).Input;
    input = input with { Pending = input.Pending.Where(c => c.ObservedAt < input.Time - 1e-8).ToArray(), NextReview = input.Time };
    var recomputed = FreeRacingController.Choose(input, run.Policy, spec, frame, surface, p, 147, 52.72097f);
    queries += recomputed.ForceEvaluations;
    if (recomputed.Action != decision.Action) throw new InvalidOperationException("Saved policy decision differs at " + input.Time);
}
var result = new { NativeWorlds = 0, PhysicsEvaluations = queries, CpuSeconds = (process.TotalProcessorTime - start).TotalSeconds,
    NativeDecisionsRecomputedExactly = run.Decisions.Length, MaximumWheelOrSteeringReplayError = errors.Max(),
    MaximumSteeringRateRadiansPerSecond = maxRate, MaximumFrontUtilization = maxFront, MaximumRearUtilization = maxRear,
    Records = records };
FreeRacingExperiment.Save(dir, "offline-validation.json", result);
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, FreeRacingExperiment.Json));
