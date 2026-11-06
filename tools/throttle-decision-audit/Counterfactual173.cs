using System.Security.Cryptography;
using System.Text.Json;
using CoreSim.PhysicalSpace;
using Efficient147;
using Efficient152;
using LearnedCorner166;
using Safety153;
using SharedWorldSpeedway;

namespace Cyclic172;

internal sealed record Selected173(string Id, string Trial, string Reason, FreeRacingInput Input,
    double CrankOmega, FreeRacingAction Action, RiderSpec Spec, JsonElement Classification);
internal sealed record Forecast173(string State, string Kind, string Gas, string Steering, FreeRacingAction Action,
    string InitialSha256, bool LocallyFeasible, string? FirstGate, double? FirstGateTime,
    double EndSpeed, double EndReference, double MinimumSweep, double MaximumBeta,
    object EntryBudget, object? Settled, object Summary);

internal static class Counterfactual173
{
    public static void Run(Study173 study, string directory, string label)
    {
        var selected = Study173.Read<Selected173[]>(Path.Combine(directory, "selected173.json"));
        if (selected.Length is < 6 or > 12) throw new InvalidOperationException("6-12 selected authentic observations required");
        var all = new List<Forecast173>();
        var targets = new CyclicReference172(study.A, study.B, study.Weights, selected[0].Spec, study.Frame);
        foreach (var state in selected)
        {
            Trace173.Begin(state.Input.Time); Trace173.Enabled = false;
            var surface = MultiActionExperiment135.Surface(study.Frame);
            var parameters = FreeLearningExperiment.Parameters();
            var boundary = new Boundary152(study.Frame); var cache = new PredictionBoundary147(study.Frame);
            var policy = new Policy172(study.A, study.B, study.Weights, state.Spec, study.Frame, surface, parameters,
                147, 52.72097f, new(study.A, study.B, study.Weights, state.Spec, study.Frame));
            var input = state.Input;
            double entry = state.Classification.GetProperty("distance_to_entry").GetDouble() + input.Rider.TotalReferenceMeters;
            string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { input, state.CrankOmega, state.Spec }, Study173.Json)));
            var gases = new (string Name, float Drive)[] { ("historical", state.Action.Drive),
                ("plus20pp", Math.Min(.94f, state.Action.Drive + .2f)), ("95percent", .95f), ("100percent", 1f) };
            foreach (var gas in gases)
            {
                var action = state.Action with { Drive = gas.Drive };
                Hold(gas.Name, "historical", action);
                Pulse(gas.Name, "historical", action);
            }
            // Small predeclared steering factor. Every evaluated alternative remains in the evidence.
            foreach (var gas in gases.Where(x => x.Drive >= .95f))
            {
                var candidates = new List<Forecast173>();
                foreach (float steering in new[] { state.Action.SteeringRadians - .02f, state.Action.SteeringRadians - .01f,
                    state.Action.SteeringRadians + .01f, state.Action.SteeringRadians + .02f, 0f }.Distinct())
                    candidates.Add(Hold(gas.Name, "grid", new(steering, gas.Drive)));
                var best = candidates.Where(x => x.LocallyFeasible).OrderByDescending(x => x.MinimumSweep)
                    .ThenBy(x => x.MaximumBeta).FirstOrDefault();
                if (best is not null) Pulse(gas.Name, "best-local-grid", best.Action);
            }
            Console.WriteLine(state.Id + ": " + all.Count(x => x.State == state.Id) + " forecasts, " +
                all.Count(x => x.State == state.Id && x.LocallyFeasible) + " locally feasible; no native world");

            FreeRacingPrediction Predict(FreeRacingInput from, double crank, double horizon, FreeRacingAction action,
                Action<double, double>? observe = null)
            {
                Trace173.Guard();
                var result = PredictorA153.Predict(from, state.Spec, study.Frame, surface, parameters, 147, 52.72097f,
                    horizon, candidate: action, powertrain: new CrankClutch144(omega: crank), observeCrank: observe,
                    boundaryCache: cache, fastBoundary: boundary, exactInitialWheel: true);
                if (result.Points[0].Rider != from.Rider || result.Points[0].Omega != from.Omega ||
                    result.Points[0].Steering != from.Steering) throw new InvalidOperationException("Counterfactual initial state changed");
                return result;
            }

            Forecast173 Hold(string gas, string steering, FreeRacingAction action)
            {
                var path = Predict(input, state.CrankOmega, .84, action);
                var row = Result("held-0.84", gas, steering, action, path.Points, path);
                all.Add(row); return row;
            }

            void Pulse(string gas, string steering, FreeRacingAction action)
            {
                double crank = state.CrankOmega;
                var head = Predict(input, state.CrankOmega, .2, action, (_, omega) => crank = omega);
                var end = head.Points[^1];
                var pending = input.Pending.Where(c => c.ActivationAt > end.Time + 1e-9)
                    .Append(new FreeRacingCommand(input.Time, input.Time + .24, action)).ToArray();
                var active = input.Pending.Where(c => c.ActivationAt <= end.Time + 1e-9).Select(c => c.Action)
                    .Prepend(input.Active).Last();
                var next = new FreeRacingInput(end.Rider, end.Time, end.Omega, end.Steering, active, pending, end.Time);
                var release = policy.Choose(next, new CrankClutch144(omega: crank));
                var tail = Predict(next, crank, .64, release);
                var points = head.Points.Concat(tail.Points.Skip(1)).ToArray();
                var combined = new FreeRacingPrediction(points, head.ForceEvaluations + tail.ForceEvaluations, head.DistanceMeters + tail.DistanceMeters);
                all.Add(Result("pulse0.2-policy-release", gas, steering, action, points, combined));
            }

            Forecast173 Result(string kind, string gas, string steering, FreeRacingAction action,
                FreeRacingPoint[] points, FreeRacingPrediction path)
            {
                var safety = Safety(points);
                var end = points[^1];
                var target = targets.Stability(end.Rider.TotalReferenceMeters);
                var previous = points[^2];
                double beta = BikeAngles.Wrap(end.Rider.SlideRadians - target.Beta);
                double betaRate = BikeAngles.Wrap(beta - (previous.Rider.SlideRadians - targets.Stability(previous.Rider.TotalReferenceMeters).Beta)) / (end.Time - previous.Time);
                var settled = new { Beta = beta, BetaRate = betaRate,
                    BetaGate = Math.Abs(beta) + .2 * Math.Max(0, betaRate * Math.Sign(beta)),
                    YawGate = Math.Abs(end.Rider.AngularVelocity - target.Yaw),
                    CourseGate = Math.Abs(BikeAngles.Wrap(Teacher.CourseError(end.Rider) - target.Course)) };
                double remaining = Math.Max(0, entry - end.Rider.TotalReferenceMeters);
                double entrySpeed = targets.FlyingEntrySpeed + study.Tuning.SpeedHeadroom;
                return new(state.Id, kind, gas, steering, action, hash, safety.Gate is null, safety.Gate, safety.Time,
                    end.Rider.Speed, end.Rider.TotalReferenceMeters, safety.Sweep, points.Max(p => Math.Abs(p.Rider.SlideRadians)),
                    new { Entry = entry, Remaining = remaining, EntrySpeed = entrySpeed, AssumedDeceleration = .65,
                        RequiredDeceleration = remaining > 0 ? Math.Max(0, (end.Rider.Speed * end.Rider.Speed - entrySpeed * entrySpeed) / (2 * remaining)) : (double?)null,
                        Accepted = end.Rider.Speed * end.Rider.Speed <= entrySpeed * entrySpeed + 2 * .65 * remaining }, settled,
                    Trace173.Summary(path));
            }
        }
        Trace173.Enabled = true;
        Study173.Save(Path.Combine(directory, label + "-counterfactual.json"), new {
            NativeWorlds = 0, Horizon = .84, Status = "local predictions only; no claim about full bend or S3",
            SelectedSha256 = Study173.Hash(Path.Combine(directory, "selected173.json")), Rows = all });
    }

    internal static (string? Gate, double? Time, double Sweep) Safety(FreeRacingPoint[] points)
    {
        string? gate = null; double? time = null; double minimum = double.PositiveInfinity;
        for (int i = 1; i < points.Length; i++)
        {
            var a = points[i - 1]; var b = points[i];
            double sweep = Clearance153.SweepLowerBound(a.Rider, b.Rider, b.Time - a.Time, (a.Inner, a.Outer), (b.Inner, b.Outer));
            minimum = Math.Min(minimum, sweep);
            if (gate is null && (Math.Abs(b.Rider.SlideRadians) > Math.PI / 4 || sweep < .5))
            { gate = Math.Abs(b.Rider.SlideRadians) > Math.PI / 4 ? "BETA45" : "SWEPT050"; time = b.Time; }
        }
        return (gate, time, minimum);
    }
}
