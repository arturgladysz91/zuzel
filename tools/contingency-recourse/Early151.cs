using System.Text.Json;
using Autonomous146;
using Efficient147;
using Robust150;
using SharedWorldSpeedway;
namespace Contingency151;
public static class Early151 {
    public static void Run() {
        using var budget = new Budget151("earlier-actions-0.0-0.2-v1", 0, 220); Program151.Snapshot("early-v1");
        var (run, _, anchor) = Audit151.FailedStart();
        var rows = Journal147.ReadComplete(Program150.Doc + "/native/robust150-tube-full-A-decisions.jsonl").Where(x => x.GetProperty("Event").GetString() == "decision").ToArray();
        var output = new List<object>();
        foreach (var row in rows) {
            var state = row.GetProperty("Review").GetProperty("Observed").Deserialize<State146>(Program151.Json)!;
            // Symmetric root changes plus all throttle channels; no historical command tape is executed.
            foreach (var action in new[] { new FreeRacingAction(0, 0), new FreeRacingAction(0, (float)Controller143.Policy.Drive), new FreeRacingAction(-.5f * MathF.PI / 180, 1), new FreeRacingAction(.5f * MathF.PI / 180, 1) }) {
                var planner = new Planner151(run.Riders[0].Spec, false, new(Enabled: true, MaximumRepairs: 0), budget.Guard);
                planner.InitializeDiagnosticTape(anchor); string? failure = null;
                try { planner.Choose(state.Input, new(omega: state.CrankOmega), action); } catch (InvalidOperationException error) { failure = error.Message; }
                var name = "early-" + state.Input.Time.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "-" + output.Count;
                Program151.Save("early/" + name + ".json.gz", new { Time = state.Input.Time, Action = action, Failure = failure, Review = planner.Last });
                output.Add(new { Time = state.Input.Time, Action = action, Failure = failure, Certified = planner.Last?.ConditionalCertificate, CpuSeconds = planner.Last?.CpuSeconds });
                Console.WriteLine(name + " " + (failure ?? "finite certificate"));
            }
        }
        Program151.Save("earlier-actions.json", output); budget.Finish("bounded earlier joint-action alternatives; finite model certificates, no native prevention claim");
    }
}
