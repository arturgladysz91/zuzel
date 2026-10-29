using System.Text.Json;
using Autonomous146;
using Efficient147;
using Robust150;
using SharedWorldSpeedway;
namespace Contingency151;
public static class NativeError151 {
    public static void Run() {
        using var budget = new Budget151("new-native-identical-command-error-audit-v1", 0, 8, savedExport: true); Program151.Snapshot("new-native-error-audit-v1"); var results = new List<object>();
        foreach (var path in Directory.GetFiles(Program151.Doc + "/native", "*-result.json").Order()) {
            budget.Guard(); var metadata = Program151.Read<JsonElement>(path); var name = Path.GetFileName(path).Replace("-result.json", "");
            var run = Program151.Read<Run144>(Program151.Doc + "/native/" + name + ".json.gz");
            var rows = Journal147.ReadComplete(Program151.Doc + "/native/" + name + "-decisions.jsonl").Where(x => x.GetProperty("Event").GetString() == "decision").ToArray();
            State146 State(JsonElement row) => StateProbes150.State(row);
            FreeRacingAction Action(JsonElement row) => row.GetProperty("Selected").Deserialize<FreeRacingAction>(Program151.Json);
            var rt = new Runtime150(run.Riders[0].Spec, metadata.GetProperty("Model").GetString() == "B", workBudget: false); var errors = new List<object>();
            for (int k = 0; k + 1 < rows.Length; k++) {
                budget.Guard(); var state = State(rows[k]); rt.Begin(state.Input.Time);
                var predicted = rt.Step(state, Action(rows[k])).End;
                errors.Add(new { Time = state.Input.Time, Horizon = .2, Delta = Audit151.Delta(predicted, State(rows[k + 1])), SameCommands = true });
                if (k % 5 == 0 && k + 3 < rows.Length && rows[k].TryGetProperty("Proof", out var proof)) {
                    var plannedActions = proof.GetProperty("FirstActions").Deserialize<FreeRacingAction[]>(Program151.Json)!;
                    if (plannedActions.Length < 3) continue;
                    var actual = state; var planned = state;
                    for (int j = 0; j < 3; j++) { actual = rt.Step(actual, Action(rows[k + j])).End; planned = rt.Step(planned, plannedActions[j]).End; }
                    errors.Add(new { Time = state.Input.Time, Horizon = .6, IdenticalActualCommandPrediction = Audit151.Delta(actual, State(rows[k + 3])), EarlierPlannedCommandPrediction = Audit151.Delta(planned, State(rows[k + 3])), NewDecisionContribution = Audit151.Delta(planned, actual), SameCommands = plannedActions.SequenceEqual(rows.Skip(k).Take(3).Select(Action)) });
                }
            }
            results.Add(new { Run = name, Errors = errors });
        }
        Program151.Save("new-native-errors.json", results); budget.Finish("same-time actual-command native comparisons and separate0.6s decision contribution; no new worlds");
    }
}
