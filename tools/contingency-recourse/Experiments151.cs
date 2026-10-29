using System.Diagnostics;
using System.Text.Json;
using Autonomous146;
using Efficient147;
using Robust150;
using SharedWorldSpeedway;
namespace Contingency151;
public static class Experiments151 {
    public static void Gate1(string label = "v1") {
        using var budget = new Budget151("gate1-saved-0.4-" + label, 0, 120);
        Program151.Snapshot("gate1-" + label);
        var (run, state, anchor) = Audit151.FailedStart();
        var planner = new Planner151(run.Riders[0].Spec, false, new(Enabled: true), budget.Guard);
        planner.InitializeDiagnosticTape(anchor); string? failure = null;
        try { planner.Choose(state.Input, new(omega: state.CrankOmega)); } catch (InvalidOperationException error) { failure = error.Message; }
        Program151.Save("gate1-" + label + ".json.gz", new { Failure = failure, Review = planner.Last, Scope = "Detached original0.4s accepted observation; no native-world completion claim.", planner.Envelope });
        budget.Finish(failure ?? "Gate1 finite scenario certificate; native gates remain untested");
        Console.WriteLine("gate1 " + (failure ?? "certified") + " CPU=" + budget.Elapsed);
    }
    public static void Native(string label, int gate, bool model = false) {
        if (gate is < 2 or > 5) throw new ArgumentException("Explicit native gate required");
        var prerequisite = gate == 2 ? Directory.GetFiles(Program151.Doc, "gate1-*.json.gz").Any(p => Program151.Read<JsonElement>(p).TryGetProperty("Review", out var r) && r.ValueKind != JsonValueKind.Null && r.GetProperty("ConditionalCertificate").GetBoolean())
            : Directory.Exists(Program151.Doc + "/native") && Directory.GetFiles(Program151.Doc + "/native", "*-result.json").Any(p => { var r = Program151.Read<JsonElement>(p); return r.GetProperty("Gate").GetInt32() == gate - 1 && r.GetProperty("Model").GetString() == "A" && r.GetProperty("GateReached").GetBoolean(); });
        if (!prerequisite) throw new InvalidOperationException("Prior experiment gate is not passed; no native world may be reserved.");
        if (model && (!File.Exists(Program151.Doc + "/probes.json") || Program151.Read<JsonElement[]>(Program151.Doc + "/probes.json").Where(x => x.GetProperty("Role").GetString()!.StartsWith("held", StringComparison.Ordinal)).Any(x => x.GetProperty("Certified").ValueKind != JsonValueKind.True))) throw new InvalidOperationException("Held-out gate required before full B.");
        using var budget = new Budget151(label, 1, gate == 2 ? 240 : gate == 3 ? 300 : 420);
        Program151.Snapshot(label);
        Directory.CreateDirectory(Program151.Doc + "/native");
        using var journal = new Journal147(Program151.Doc + "/native/" + label + "-decisions.jsonl");
        Planner151? planner = null; double attemptedCpu = 0; int count = 0; bool reached = false;
        var process = Process.GetCurrentProcess(); var profiles = new List<object>();
        Decision144 Choose(FreeRacingInput input, CrankClutch144 crank, RiderSpec spec, TrackFrame frame, CoreSim.TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, string variant) {
            if (gate == 2 && input.Rider.TotalReferenceMeters >= frame.SegmentStartMeters(4)) { reached = true; throw new InvalidOperationException("STUDY151_GATE2_TARGET_REACHED"); }
            planner ??= new(spec, model, new(Enabled: true, Laps: gate <= 3 ? 1 : 3), budget.Guard);
            var began = process.TotalProcessorTime;
            try {
                budget.Guard();
                var choice = planner.Choose(input, crank);
                var id = "decision-" + count++.ToString("D4");
                Program151.Save("trees/" + label + "/" + id + ".json.gz", planner.Last!);
                var review = planner.Last!;
                journal.Append(new { Event = "decision", input.Time, Selected = choice.Choice.Action, Tree = id, review.Reason, review.Independent, review.ConditionalCertificate, review.IrrevocableUntil, review.CpuSeconds, Observed = input, CrankOmega = crank.Omega,
                    Proof = new { Nodes = review.Tree.Length, Leaves = review.Terminals.Length, Attempts = review.Attempts.Length, Legal = review.Attempts.Count(a => a.Legal), FirstActions = review.Nominal!.Actions.Take(3).ToArray(), review.Nominal.Arrival, review.Nominal.Safety, ExitKph = review.Nominal.Points.MinBy(p => Math.Abs(p.Time - review.Nominal.Arrival!.Value))!.Rider.Speed * 3.6, Trace = review.Nominal.Points.Where((p, k) => k % 10 == 0).Select(p => new[] { p.Time, p.Rider.Position.X, p.Rider.Position.Y }).ToArray() } });
                profiles.Add(new { input.Time, review.Profile });
                return choice;
            } catch (InvalidOperationException error) {
                Program151.Save("trees/" + label + "/abstain.json.gz", new { Error = error.Message, Review = planner.Last, Observed = input, CrankOmega = crank.Omega });
                journal.Append(new { Event = "abstain", input.Time, Error = error.Message }); throw;
            } finally { attemptedCpu += (process.TotalProcessorTime - began).TotalSeconds; }
        }
        Run144 run; using (var world = new DiagnosticWorld149(new(Enabled: true, RequiredLaps: gate <= 3 ? 1 : 3, MaximumSeconds: gate <= 3 ? 35 : 100), separated: model)) run = world.Run(label, "K1", Choose);
        var samples = run.Riders[0].Samples;
        reached |= gate != 2 && run.Outcome == "completed";
        Program151.Save("native/" + label + ".json.gz", run);
        Program151.Save("native/" + label + "-result.json", new { Gate = gate, Model = model ? "B" : "A", GateReached = reached, run.Outcome, run.EndSeconds, run.Fingerprint, run.Riders[0].FinishSeconds, run.Riders[0].CompletedLaps, ControllerAttemptCpuSeconds = attemptedCpu, CpuSeconds = budget.Elapsed, Decisions = count, MinimumGapMeters = samples.Min(s => Math.Min(s.Inner, s.Outer)), MinimumSweepMeters = samples.Where(s => s.Certificate is not null).Min(s => s.Certificate), MaximumBetaDegrees = samples.Max(s => Math.Abs(s.Input.Rider.SlideRadians) * 180 / Math.PI), Contacts = run.Contacts.Length, Scope = "Continuous native world from rest, autonomous actual observations and certified commands; no historical prefix.", SourceHashes = Directory.GetFiles("tools/contingency-recourse", "*.cs").Order().ToDictionary(p => Path.GetFileName(p), p => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p)))) });
        Program151.Save("native/" + label + "-cpu.json", profiles); journal.Append(new { Event = "summary", reached, run.Outcome, run.Fingerprint });
        budget.Finish(reached ? "Gate" + gate + " reached; finite certificates only, not global guarantee" : run.Outcome);
        Console.WriteLine(label + " gate=" + reached + " outcome=" + run.Outcome + " t=" + run.EndSeconds + " CPU=" + budget.Elapsed);
    }
}
