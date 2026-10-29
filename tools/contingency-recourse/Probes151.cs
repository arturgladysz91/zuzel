using Autonomous146;
using System.Text.Json;
using Robust150;
using SharedWorldSpeedway;
namespace Contingency151;
public static class Probes151 {
    public static void Run(bool remaining = false) {
        var label = remaining ? "held-v2" : "v1"; var cap = remaining ? 35 : 80;
        using var budget = new Budget151("frozen-critical-and-held-out-probes-" + label, 0, cap); Program151.Snapshot("probes-" + label);
        var output = new List<object>();
        foreach (var (model, time, role) in new[] { (false, 1.0, "historical exact1s"), (true, 1.0, "historical exact1s"), (false, 22.6, "historical22.6"), (true, 22.6, "historical22.6"), (false, 28.4, "corner2.2"), (true, 28.4, "corner2.2"), (true, 45.8, "last B corner"), (false, 12.8, "held for151; no tuning"), (true, 39.4, "held for151; no tuning") }) {
            if (remaining && time is not (45.8 or 12.8 or 39.4)) continue;
            if (budget.Elapsed >= cap - 5) { output.Add(new { Model = model ? "B" : "A", Time = time, Role = role, Failure = "not run: reserved native-race CPU budget", Certified = (bool?)null, CpuSeconds = (double?)null }); continue; }
            budget.Guard(); var key = model ? "B" : "A"; State146 state; Tape150? anchor = null; RiderSpec spec;
            if (time == 1) {
                var run = Program151.Read<Run144>(Diagnosis150.Previous + "/fixtures/planned-entry-90-outer-" + key + ".json.gz");
                var sample = run.Riders[0].Samples[^1]; state = new(sample.Input, sample.CrankOmega); spec = run.Riders[0].Spec;
                anchor = Program151.Read<Tape150>(Program150.Doc + "/diagnosis-safe/" + key + "-A-original-fixed.json.gz");
            } else {
                var run = Program151.Read<Run144>(Diagnosis150.Previous + "/native/" + (model ? "separated149-autonomous" : "baseline149-final148") + ".json.gz"); spec = run.Riders[0].Spec;
                state = StateProbes150.State(StateProbes150.Journal(model).Single(x => Math.Abs(x.GetProperty("Time").GetDouble() - time) < 1e-7));
                // Reconstruct the previous controller's own nominal continuation as diagnostic memory, never as a native command script.
                Review150? prior = time == 22.6 ? Program151.Read<JsonElement[]>(Program150.Doc + "/critical22-new-planner.json.gz").Single(x => x.GetProperty("Model").GetString() == key).GetProperty("Review").Deserialize<Review150>(Program151.Json)
                    : Program151.Read<JsonElement>(Program150.Doc + "/probe-midpoint-" + key + "-" + time.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ".json").GetProperty("Review").Deserialize<Review150>(Program151.Json);
                var sequence = prior?.Selected ?? prior?.Alternatives.Where(x => x.Legal).OrderBy(x => x.Seconds).FirstOrDefault()?.Sequence;
                if (sequence is not null) { var rt = new Runtime150(spec, model, workBudget: false); rt.Begin(state.Input.Time); var tape = Rollout150.Evaluate(rt, state, sequence, prior!.Goal, prior.SafetyGoal, "own-history-diagnostic-memory", guard: budget.Guard); if (tape.Safety.Legal) anchor = tape; }
            }
            var planner = new Planner151(spec, model, new(Enabled: true, Laps: time == 1 ? 1 : 3), budget.Guard);
            if (anchor is not null) planner.InitializeDiagnosticTape(anchor); string? failure = null;
            try { planner.Choose(state.Input, new(omega: state.CrankOmega)); } catch (InvalidOperationException error) { failure = error.Message; }
            var name = "probe-" + key + "-" + time.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            Program151.Save("probes/" + name + ".json.gz", new { Model = key, Time = time, Role = role, Failure = failure, Review = planner.Last, Scope = "Saved accepted state, finite policy certificate only; no native recovery or full race claim" });
            output.Add(new { Model = key, Time = time, Role = role, Failure = failure, Certified = planner.Last?.ConditionalCertificate, CpuSeconds = planner.Last?.CpuSeconds });
            Console.WriteLine(name + " " + (failure ?? "certificate"));
        }
        Program151.Save(remaining ? "probes-held.json" : "probes.json", output); budget.Finish("frozen historical and held-out observation probes complete; no new worlds");
    }
}
