using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LearnedCorner166;
using SharedWorldSpeedway;
using Scenario = LearnedCorner166.Scenario;

namespace Cyclic172;

internal static class Native173
{
    public static void Run(Study173 study, string directory, string label)
    {
        var selected = Study173.Read<Selected173[]>(Path.Combine(directory, "selected173.json"));
        var state = selected.Single(x => x.Id == "P7");
        var forecast = Study173.Read<JsonElement>(Path.Combine(directory, "q1-counterfactual.json"));
        if (forecast.GetProperty("SelectedSha256").GetString() != Study173.Hash(Path.Combine(directory, "selected173.json")))
            throw new InvalidOperationException("Counterfactual state selection changed");
        var coordinated = forecast.GetProperty("Rows").EnumerateArray().Single(x => x.GetProperty("State").GetString() == "P7" &&
            x.GetProperty("Kind").GetString() == "pulse0.2-policy-release" && x.GetProperty("Gas").GetString() == "95percent" &&
            x.GetProperty("Steering").GetString() == "best-local-grid").GetProperty("Action").Deserialize<FreeRacingAction>(Study173.Json)!;
        using var outer = ZipFile.OpenRead(Study173.Docs172 + "/172-validation-K4.zip");
        using var from = outer.GetEntry(state.Trial + ".zip")!.Open(); using var memory = new MemoryStream(); from.CopyTo(memory); memory.Position = 0;
        using var zip = new ZipArchive(memory);
        var original = Study173.Read<Run144>(zip, "native.json");
        var archived = Study173.Read<CyclicDecision172[]>(zip, "decisions.json");
        var rider = original.Riders.Single();
        double stop = study.Frame.SegmentStartMeters(8) + 20;
        var arms = new (string Name, FreeRacingAction Action)[] {
            ("baseline", state.Action), ("plus20pp", state.Action with { Drive = state.Action.Drive + .2f }),
            ("95-fixed", state.Action with { Drive = .95f }), ("95-coordinated", coordinated) };
        Study173.Save(Path.Combine(directory, label + "-protocol.json"), new {
            Question = "Can a single extra 0.2s drive pulse rejected by CanPrepare at P7 preserve native legality through the next whole bend?",
            Evidence = "All local 0.84s forecasts pass, but remaining bend exceeds that horizon; 0.65 is not a measured physical bound.",
            State = state, MaximumWorlds = 4, StopReference = stop, MaximumSeconds = 40,
            Arms = arms.Select(a => new { a.Name, a.Action }),
            Continuation = "Original frozen cyclic K4; exactly one accepted command differs at the branch; no S3 performance claim" });
        string? commonBranch = null;
        foreach (var arm in arms)
        {
            study.AdmitWorld();
            Trace173.Enabled = false;
            CyclicController172? controller = null;
            var decisions = new List<CyclicDecision172>();
            string? branchHash = null; int prefixMatches = 0, injections = 0;
            Decision144 Choose(FreeRacingInput input, CrankClutch144 crank, RiderSpec spec, TrackFrame frame,
                CoreSim.TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, string variant)
            {
                Trace173.Begin(input.Time);
                controller ??= study.Controller(spec);
                var choice = controller.Choose(input, crank);
                if (input.Time <= state.Input.Time + 1e-8)
                {
                    var old = archived.Single(x => Math.Abs(x.Time - input.Time) < 1e-8);
                    if (choice.Action != old.Action) throw new InvalidOperationException("Prefix action changed");
                    var sample = rider.Samples.MinBy(x => Math.Abs(x.Input.Time - input.Time))!;
                    var expected = Study173.Observation(sample, input.Time);
                    var actualBytes = JsonSerializer.SerializeToUtf8Bytes(new { Input = input, CrankOmega = crank.Omega }, Study173.Json);
                    var expectedBytes = JsonSerializer.SerializeToUtf8Bytes(new { Input = expected, sample.CrankOmega }, Study173.Json);
                    if (!actualBytes.SequenceEqual(expectedBytes)) throw new InvalidOperationException("Native prefix state changed");
                    prefixMatches++;
                    if (Math.Abs(input.Time - state.Input.Time) < 1e-8)
                    {
                        branchHash = Convert.ToHexString(SHA256.HashData(actualBytes));
                        commonBranch ??= branchHash;
                        if (branchHash != commonBranch) throw new InvalidOperationException("Branches are not identical");
                        choice = choice with { Action = arm.Action, Transition = arm.Name == "baseline" ? choice.Transition : "diagnostic single pulse" };
                        controller.DiagnosticApplied173(choice.Action); injections++;
                    }
                }
                decisions.Add(choice);
                return new(new(input.Time, choice.Action, FreeActionSafety.Uncertain, false, [], choice.Queries, choice.Forecasts), choice.CpuSeconds);
            }
            double began = Study172.Cpu;
            using var world = new NativeWorld172(new(Enabled: true, RequiredLaps: 3, MaximumSeconds: 40, SoloGate: rider.Gate),
                diagnosticFrame: study.Frame, diagnosticRider: rider.Spec, safetyHalfMeter: true, stopReference: stop);
            var run = world.Run(label + "-" + arm.Name, "K1", Choose);
            var scenario = new Scenario("S1", "A", rider.Spec.ReferenceMeters, rider.Spec.OffsetMeters);
            var plans = controller?.Plans.ToArray() ?? [];
            var result = Inspect172.Analyze(run, "diagnostic-single-pulse", arm.Name, scenario, study.Frame, stop,
                decisions.ToArray(), plans, Study172.Cpu - began);
            string path = Path.Combine(directory, label + "-" + arm.Name + ".zip");
            using (var file = new FileStream(path, FileMode.CreateNew))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Add(archive, "native.json", run); Add(archive, "decisions.json", decisions); Add(archive, "plans.json", plans);
                Add(archive, "result.json", result); Add(archive, "branch.json", new { state.Id, state.Reason, arm.Name, arm.Action,
                    Time = state.Input.Time, BranchHash = branchHash, PrefixMatches = prefixMatches, SingleDecision = true });
            }
            Study173.Save(Path.Combine(directory, label + "-" + arm.Name + "-result.json"), result);
            if (branchHash is null || injections != 1) throw new InvalidOperationException("No verified intervention reached; failed native evidence retained");
            Console.WriteLine(arm.Name + $": reached={result.ReachedStop}, t={result.EndTime:F3}, swept={result.MinSweep:F3}, beta={result.MaxBetaDegrees:F2}, {result.Reason}");
        }
        Trace173.Enabled = true;
    }

    private static void Add(ZipArchive archive, string name, object value)
    { using var stream = archive.CreateEntry(name).Open(); JsonSerializer.Serialize(stream, value, Study173.Json); }
}
