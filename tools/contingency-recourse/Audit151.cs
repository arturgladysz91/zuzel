using System.Numerics;
using System.Text.Json;
using Autonomous146;
using Efficient147;
using Robust150;
using SharedWorldSpeedway;
namespace Contingency151;
public static class Audit151 {
    public static object? FirstFailure(Tape150 tape) {
        for (int k = 0; k < tape.Points.Length; k++) {
            var point = tape.Points[k];
            var check = Planner146.Check(k == 0 ? new[] { point } : new[] { tape.Points[k - 1], point }, .1);
            if (!check.Legal) return new { point.Time, check.Reason, point.Inner, point.Outer, check.SweepLowerBoundMeters, BetaDegrees = Math.Abs(point.Rider.SlideRadians) * 180 / Math.PI, Reference = point.Rider.TotalReferenceMeters };
        }
        return null;
    }
    public static (Run144 Run, State146 State, Tape150 Anchor) FailedStart() {
        var name = "robust150-tube-full-A";
        var run = Program151.Read<Run144>(Program150.Doc + "/native/" + name + ".json.gz");
        var rows = Journal147.ReadComplete(Program150.Doc + "/native/" + name + "-decisions.jsonl");
        var last = rows.Last(x => x.GetProperty("Event").GetString() == "decision");
        var anchor = Program151.Read<Tape150>(Program150.Doc + "/plans/" + name + "/" + last.GetProperty("Anchor").GetString() + ".json.gz");
        var sample = run.Riders[0].Samples[^1];
        return (run, new(sample.Input, sample.CrankOmega), anchor);
    }
    public static object Delta(State146 predicted, State146 observed) {
        var a = predicted.Input; var b = observed.Input;
        var queue = JsonSerializer.Serialize(a.Pending, Program151.Json) == JsonSerializer.Serialize(b.Pending, Program151.Json);
        var actions = a.Pending.Length == b.Pending.Length && a.Pending.Zip(b.Pending).All(p => p.First.Action == p.Second.Action);
        var timestampError = a.Pending.Length == b.Pending.Length ? a.Pending.Zip(b.Pending).Select(p => Math.Max(Math.Abs(p.First.ActivationAt - p.Second.ActivationAt), Math.Abs(p.First.ObservedAt - p.Second.ObservedAt))).DefaultIfEmpty(0).Max() : (double?)null;
        return new { PositionMeters = Vector2.Distance(a.Rider.Position, b.Rider.Position), VelocityMetersPerSecond = Vector2.Distance(a.Rider.Velocity, b.Rider.Velocity), HeadingRadians = CoreSim.PhysicalSpace.BikeAngles.Wrap(a.Rider.Heading - b.Rider.Heading), YawRadiansPerSecond = a.Rider.AngularVelocity - b.Rider.AngularVelocity, WheelRadiansPerSecond = a.Omega - b.Omega, CrankRadiansPerSecond = predicted.CrankOmega - observed.CrankOmega, SteeringRadians = a.Steering - b.Steering, QueueExact = queue, QueueActionsExact = actions, QueueTimestampErrorSeconds = timestampError, QueueCausallyEquivalent = actions && timestampError <= 1e-8, ActiveExact = a.Active == b.Active };
    }
    public static void Run(string label = "v1") {
        using var budget = new Budget151("saved-causal-audit-" + label, 0, label == "v2" ? 8 : 120, savedExport: label == "v2"); Program151.Snapshot("causal-audit-" + label);
        var (run, start, anchor) = FailedStart(); var spec = run.Riders[0].Spec;
        var planner = new Planner150(spec, false, new(Enabled: true, Feedback: true), budget.Guard);
        var rt = planner.Runtime; rt.Begin(start.Input.Time);
        var retained = Feedback150.Evaluate(rt, start, anchor, anchor.Goal, anchor.SafetyGoal, "retained", budget.Guard);
        var fresh = Rollout150.Evaluate(rt, start, Array.Empty<FreeRacingAction>(), anchor.Goal, anchor.SafetyGoal, "fresh", generateTail: true, guard: budget.Guard);
        var failures = new List<object>();
        foreach (var (state, epoch) in new[] { (start, "current-0.4"), (rt.Step(start, retained.Actions[0]).End, "next-0.6") })
        foreach (var sign in new[] { 0, 1, -1 })
        foreach (var (reference, policyLabel) in new[] { (anchor, "retained"), (fresh, "fresh") }) {
            var challenged = sign == 0 ? state : planner.ChallengeForDiagnostics(state, sign);
            var tape = Feedback150.Evaluate(rt, challenged, reference, anchor.Goal, anchor.SafetyGoal, policyLabel, budget.Guard);
            failures.Add(new { Epoch = epoch, Sign = sign, Policy = policyLabel, tape.Safety, FirstFailure = FirstFailure(tape), challenged.Input.Pending, InitialAction = tape.Actions[0] });
            Program151.Save($"audit/{(label == "v1" ? "" : label + "/")}{epoch}-{sign}-{policyLabel}.json.gz", tape);
        }
        Program151.Save("rejection-audit" + (label == "v1" ? "" : "-" + label) + ".json", failures);
        var errors = new List<object>();
        var roots = new[] { Program150.Doc + "/native/robust150-exact-recovery-A", Program150.Doc + "/native/robust150-exact-recovery-B", Program150.Doc + "/native/robust150-prefix-B-45", Program150.Doc + "/native/robust150-tube-full-A", Diagnosis150.Previous + "/native/baseline149-final148", Diagnosis150.Previous + "/native/separated149-autonomous" };
        foreach (var root in roots) {
            budget.Guard(); var native = Program151.Read<Run144>(root + ".json.gz"); var model = root.EndsWith("-B", StringComparison.Ordinal) || root.Contains("prefix-B") || root.Contains("separated149");
            var rows = Journal147.ReadComplete(root + "-decisions.jsonl").Where(x => x.GetProperty("Event").GetString() == "decision").ToArray();
            State146 State(JsonElement row) => row.TryGetProperty("Review", out var q) ? q.GetProperty("Observed").Deserialize<State146>(Program151.Json)! : StateProbes150.State(row);
            FreeRacingAction Action(JsonElement row) => row.GetProperty("Selected").Deserialize<FreeRacingAction>(Program151.Json);
            var runtime = new Runtime150(native.Riders[0].Spec, model, false); var perRun = new List<object>();
            for (int k = 0; k + 1 < rows.Length; k++) {
                var state = State(rows[k]); runtime.Begin(state.Input.Time);
                var prediction = runtime.Step(state, Action(rows[k])).End; var next = State(rows[k + 1]);
                if (Math.Abs(next.Input.Time - state.Input.Time - .2) > 1e-7) throw new InvalidOperationException("Nonconsecutive observation");
                var traceDt = 0d; double? traceXy = null;
                if (rows[k].TryGetProperty("Review", out var review)) {
                    var trace = review.GetProperty("ForecastTrace").Deserialize<FreeRacingPoint[]>(Program151.Json)!;
                    var nearest = trace.MinBy(x => Math.Abs(x.Time - next.Input.Time))!;
                    traceDt = nearest.Time - next.Input.Time; traceXy = Vector2.Distance(nearest.Rider.Position, next.Input.Rider.Position);
                }
                perRun.Add(new { Time = state.Input.Time, HorizonSeconds = .2, SameExecutedCommands = true, Delta = Delta(prediction, next), HistoricalDecimatedTraceTimeErrorSeconds = traceDt, HistoricalNearestTracePositionMeters = traceXy });
                // Frozen, regularly spaced audit states; longer forecasts use exactly the actual submitted commands, never regenerated feedback.
                if (k % 40 == 0) {
                    var open = state;
                    for (int j = 0; j < 50 && k + j + 1 < rows.Length; j++) {
                        budget.Guard(); open = runtime.Step(open, Action(rows[k + j])).End;
                        if (new[] { 4, 9, 19, 49 }.Contains(j)) perRun.Add(new { Time = state.Input.Time, HorizonSeconds = (j + 1) * .2, SameExecutedCommands = true, Delta = Delta(open, State(rows[k + j + 1])) });
                    }
                }
            }
            errors.Add(new { Run = native.Name, Model = model ? "B" : "A", Rows = perRun }); Console.WriteLine(native.Name + " identical-command pairs=" + (rows.Length - 1));
        }
        Program151.Save("identical-command-errors" + (label == "v1" ? "" : "-" + label) + ".json", errors);
        budget.Finish("saved-state rejection and identical-command predictor/native audit; zero new worlds");
    }
}
