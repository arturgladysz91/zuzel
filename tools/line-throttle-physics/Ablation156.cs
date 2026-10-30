using SharedWorldSpeedway;
namespace LineThrottle156;

public static partial class Study156
{
    public static void Ablate(string baselinePath, string optimizedPath, string label)
    {
        var baseline = Read<Attempt156>(baselinePath); var optimized = Read<Attempt156>(optimizedPath);
        if (baseline.Fixture != optimized.Fixture || baseline.Family != optimized.Family) throw new ArgumentException("Matched initial state and family required");
        var f = Fixtures().Single(x => x.Name == baseline.Fixture); Begin("ablation-" + label, 10); Snapshot("ablation-" + label);
        try
        {
            var e = new Experiment(f); foreach (var component in new[] { "gas-only", "steer-only" })
            {
                var tape = Enumerable.Range(0, Math.Max(baseline.Actions.Length, optimized.Actions.Length) + 12).Select(j => { var a = baseline.Actions[Math.Min(j, baseline.Actions.Length - 1)]; var b = optimized.Actions[Math.Min(j, optimized.Actions.Length - 1)]; return component == "gas-only" ? new FreeRacingAction(a.SteeringRadians, b.Drive) : new FreeRacingAction(b.SteeringRadians, a.Drive); }).ToArray();
                var a = Rollout(e, f, baseline.Family, "T1-" + component, label + "-" + component, null, tape); Save(Doc + "/ablation/" + label + "-" + component + ".json.gz", a); Console.WriteLine($"ablation {label} {component} complete={a.Complete} S={a.Legal} family={a.FamilyAchieved} time={a.Arrival - f.Time:F3}");
            }
            Save(Doc + "/ablation/" + label + "-sources.json", new { Baseline = baselinePath, Optimized = optimizedPath, Scope = "Factorial command-component intervention from identical full initial state. Same0.2s observation schedule and0.24s irrevocable delay." }); End("matched command-component interventions");
        }
        catch (Exception ex) { End("failed: " + ex.Message); throw; }
    }
    public static Autonomous146.Transition146 DiagnosticStep(Autonomous146.State146 state, SharedWorldSpeedway.FreeRacingAction action, string fixture = "first") => new Experiment(Fixtures().Single(x => x.Name == fixture)).Step(state, action);
}
