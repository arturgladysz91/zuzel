using System.Text.Json;
using Autonomous146;
using SharedWorldSpeedway;
namespace Contingency151;
public static class FaultAudit151 {
    public static void Run() {
        using var budget = new Budget151("saved29.8-physical-proposal-failure-v1", 0, 40); Program151.Snapshot("fault29.8-v1");
        var raw = Program151.Read<JsonElement>(Program151.Doc + "/trees/contingency151-gate4-A-v1/abstain.json.gz");
        var run = Program151.Read<Run144>(Program151.Doc + "/native/contingency151-gate4-A-v1.json.gz");
        var old = raw.GetProperty("Review").Deserialize<Review151>(Program151.Json)!;
        var state = new State146(raw.GetProperty("Observed").Deserialize<FreeRacingInput>(Program151.Json)!, raw.GetProperty("CrankOmega").GetDouble());
        var planner = new Planner151(run.Riders[0].Spec, false, new(Enabled: true), budget.Guard); planner.InitializeDiagnosticReview(old); string? failure = null;
        try { planner.Choose(state.Input, new(omega: state.CrankOmega)); } catch (InvalidOperationException error) { failure = error.Message; }
        Program151.Save("fault-repair-29.8.json.gz", new { Failure = failure, Review = planner.Last, PriorAbstainReviewTime = old.Time, ActualFailedObservationTime = state.Input.Time, Scope = "Detached actual29.8s state plus own prior causal policy memory. Invalid physical proposals are excluded, never executed or declared legal." });
        budget.Finish(failure ?? "29.8s finite recourse certificate after rejecting invalid physical proposals"); Console.WriteLine("29.8 " + (failure ?? "certificate") + " CPU=" + budget.Elapsed);
    }
    public static void RepairedFull() {
        var audit = Program151.Read<JsonElement>(Program151.Doc + "/fault-repair-29.8.json.gz");
        if (audit.GetProperty("Failure").ValueKind != JsonValueKind.Null || !audit.GetProperty("Review").GetProperty("ConditionalCertificate").GetBoolean()) throw new InvalidOperationException("Diagnosed fault repair gate is not passed");
        Experiments151.Native("contingency151-gate4-A-v2", 4);
    }
}
