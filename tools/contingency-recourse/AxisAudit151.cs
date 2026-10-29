using System.Text.Json;
using Autonomous146;
using Efficient147;
using Robust150;
namespace Contingency151;
public static class AxisAudit151 {
    public static void Run() {
        using var budget = new Budget151("axis-invariant-native-history-v1", 0, 20); Program151.Snapshot("axis-audit-v1"); var output = new List<object>();
        foreach (var path in new[] { Diagnosis150.Previous + "/native/baseline149-final148", Diagnosis150.Previous + "/native/separated149-autonomous", Program151.Doc + "/native/contingency151-gate2-A-v2" }) {
            var rows = Journal147.ReadComplete(path + "-decisions.jsonl").Where(x => x.GetProperty("Event").GetString() == "decision").ToArray();
            State146 State(JsonElement row) => row.TryGetProperty("Review", out var review) ? review.GetProperty("Observed").Deserialize<State146>(Program151.Json)! : StateProbes150.State(row);
            int eligible = 0; var violations = new List<double>();
            for (int k = 0; k + 1 < rows.Length; k++) {
                var a = State(rows[k]); if (!Planner151.AxisAlignedInvariant(a)) continue; eligible++;
                var b = State(rows[k + 1]); var x = a.Input.Rider; var y = b.Input.Rider;
                if (x.Position.Y != y.Position.Y || y.Velocity.Y != 0 || y.AngularVelocity != 0 || y.Heading != 0) violations.Add(b.Input.Time);
            }
            output.Add(new { Run = path, EligibleAcceptedPairs = eligible, Violations = violations });
            if (violations.Count != 0) throw new InvalidOperationException("Axis invariant violated in native history");
        }
        Program151.Save("axis-invariant-audit.json", output); budget.Finish("equation invariant agrees with all eligible saved native pairs; no stochastic or real-world disturbance claim");
    }
}
