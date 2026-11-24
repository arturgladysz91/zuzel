using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Admission210(string CandidateSha256, string[] Order, Run209[] Races);

    private static void FreezeReference210(string root, string output)
    {
        var context = ContextFor210(root);
        var law = context.FrozenLaw;
        var strict = new RacingProfile206(Map199);
        var checks = new List<object>();
        var population = new List<double>();
        foreach (string fixture in Official208)
        {
            string archivePath = Path.Combine(root, Evidence209, "native", $"H29-S-{fixture}.json.gz");
            using var stream = new GZipStream(File.OpenRead(archivePath), CompressionMode.Decompress);
            using var archive = JsonDocument.Parse(stream);
            var document = archive.RootElement;
            var run = document.GetProperty("Run").Deserialize<Run144>(Json)!;
            var result = document.GetProperty("Result").Deserialize<NativeResult208>(Json)!;
            var rider = run.Riders.Single();
            var decisions = document.GetProperty("Decisions").EnumerateArray().ToArray();
            var observer = strict.Begin();
            observer.ObserveInitial(rider.Samples[0].Input.Rider, rider.Samples[0].Input.Time);
            int sampleIndex = 1, actionMatches = 0, historyMatches = 0, activationMatches = 0;
            int observed = 0;
            double goal = 3 * run.LapMeters;
            var timing = new List<double>();
            var originalRequests = new List<(double Time, FreeRacingAction Action)>();
            foreach (var decision in decisions)
            {
                var state = decision.GetProperty("State").Deserialize<Start197>(Json)!;
                while (sampleIndex < rider.Samples.Length && rider.Samples[sampleIndex].Input.Time <= state.State.Time + 1e-9)
                    CheckSample(rider.Samples[sampleIndex++]);
                var expected = decision.GetProperty("Action").Deserialize<FreeRacingAction>(Json);
                if (expected == law(state)) actionMatches++;
                if (observer.Capture() == state.TechniqueHistory) historyMatches++;
                timing.Add(decision.GetProperty("Milliseconds").GetDouble());
                originalRequests.Add((state.State.Time, expected));
            }
            while (sampleIndex < rider.Samples.Length) CheckSample(rider.Samples[sampleIndex++]);
            foreach (var activation in rider.Activations)
                if (originalRequests.Any(d => Math.Abs(d.Time - activation.ObservedAt) < 1e-8 && d.Action == activation.Action))
                    activationMatches++;

            double sweep = rider.Samples.Where(s => s.Certificate.HasValue).Min(s => s.Certificate!.Value);
            var crossings = new List<double>();
            for (int lap = 1; lap <= 3; lap++)
            {
                double mark = lap * run.LapMeters;
                int i = Array.FindIndex(rider.Samples, s => s.Input.Rider.TotalReferenceMeters >= mark);
                if (i <= 0) throw new InvalidDataException($"Missing lap {lap} crossing: {fixture}.");
                var before = rider.Samples[i - 1].Input;
                var after = rider.Samples[i].Input;
                double fraction = (mark - before.Rider.TotalReferenceMeters) /
                    (after.Rider.TotalReferenceMeters - before.Rider.TotalReferenceMeters);
                crossings.Add(after.Time - (after.Time - before.Time) * (1 - fraction));
            }
            var measured = DecisionTiming206.Measure(timing);
            if (actionMatches != decisions.Length || historyMatches != decisions.Length || activationMatches != rider.Activations.Length ||
                crossings.Zip(run.Crossings).Any(p => Math.Abs(p.First - p.Second.Time) > 1e-9) ||
                Math.Abs(crossings[^1] - result.Finish!.Value) > 1e-9 || result.Sweep != sweep ||
                result.Decisions != measured || run.Contacts.Length != 0 || rider.CompletedLaps != 3 || !result.Finished)
                throw new InvalidDataException($"#209 reconstruction failed: {fixture}; actions {actionMatches}, observer {historyMatches}, activations {activationMatches}.");
            var model = RootFor210(root, fixture);
            var modelReplay = RaceFrom210(model, law);
            if (!modelReplay.Complete) throw new InvalidDataException($"Frozen MODEL race {fixture} failed.");
            checks.Add(new { Fixture = fixture, Archive = $"{Evidence209}/native/H29-S-{fixture}.json.gz",
                ArchiveSha256 = Hash210(archivePath), NativeFinish = crossings[^1], LapCrossings = crossings,
                NativeSteps = observed, Decisions = measured, ActionsExact = actionMatches, HistoriesExact = historyMatches,
                ActivationsExact = activationMatches, Sweep = sweep, Contacts = run.Contacts.Length,
                FinalTechniqueHistory = observer.Capture(), Model = modelReplay });
            population.AddRange(timing);
            Console.WriteLine($"#209 {fixture}: {actionMatches} actions, {historyMatches} checkpoints, {observed} strict steps; {crossings[^1]:F6} s");

            void CheckSample(StartSample144 sample)
            {
                var failure = observer.Check(sample.Input.Rider, sample.Input.Time, sample.Outer, sample.Certificate!.Value,
                    .5, RacingProfile206.Physical(sample.Input.Rider, sample.Input.Time,
                        (float)sample.Powertrain!.Seconds, sample.Forces!.Value, sample.Powertrain),
                    sample.Input.Rider.TotalReferenceMeters >= goal);
                if (failure is not null) throw new InvalidDataException($"Frozen native sample failed: {failure}.");
                observed++;
            }
        }
        if (population.Count != 4985) throw new InvalidDataException("Wrong frozen native decision count.");
        var bends = Enumerable.Range(0, 6).Select(i => sourceBend(i)).ToArray();
        Save(Path.Combine(output, "frozen-reference.json"), new { Base = BaseCommit210, CandidateSha256 = FrozenSha209,
            NativeWorlds = 0, Fixtures = checks, Pooled = DecisionTiming206.Measure(population),
            Track = new { Frame.LapMeters, Bends = bends } });
        var nominal = RootFor210(root, "C");
        Save(Path.Combine(output, "nominal-trace.json.gz"), Follow209("C", nominal.Model, nominal.State, nominal.Goal, law, true), true);

        object sourceBend(int visit)
        {
            var b = context.Source.Policy.Bend(visit);
            return new { Visit = visit + 1, b.Entry, Apex = b.Entry + b.Length * .5, b.Exit };
        }
    }

    private static void ArmTraces210(string root, string output, string folder)
    {
        var source = ContextFor210(root);
        var nominal = RootFor210(root, "C");
        foreach (string arm in new[] { "B", "C", "D", "E", "F" })
        {
            string path = Path.Combine(folder, arm, "candidate.json");
            if (!File.Exists(path)) continue;
            if (arm == "F" && File.Exists(Path.Combine(folder, "E", "candidate.json")) &&
                Hash210(path) == Hash210(Path.Combine(folder, "E", "candidate.json"))) continue;
            var law = Law210(source, Read210(path));
            Save(Path.Combine(output, arm + ".json.gz"), Follow209("C", nominal.Model, nominal.State, nominal.Goal, law, true), true);
        }
    }

    private static void Admit210(string root, string output, string candidate)
    {
        var source = ContextFor210(root);
        var law = Law210(source, Read210(candidate));
        string hash = Hash210(candidate);
        var runs = new List<Run209>();
        foreach (string fixture in Official208)
        {
            var input = RootFor210(root, fixture);
            var run = Follow209(fixture, input.Model, input.State, input.Goal, law, true);
            Save(Path.Combine(output, fixture + ".json.gz"), run, true);
            runs.Add(run with { Trace = null });
            Console.WriteLine($"{fixture}: {run.Stop}; {run.Finish:F6} s; swept {run.Sweep:F6}; peak {run.PeakKph:F3} km/h");
        }
        Save(Path.Combine(output, "admission.json"), new Admission210(hash, Official208, runs.ToArray()));
    }

    private static void TracePolicy210(string root, string output, string candidate, string fixture)
    {
        var context = ContextFor210(root);
        var origin = RootFor210(root, fixture);
        var law = Law210(context, Read210(candidate));
        var run = Follow209(fixture, origin.Model, origin.State, origin.Goal, law, true);
        Save(Path.Combine(output, fixture + ".json.gz"), run, true);
        Console.WriteLine($"{fixture}: {run.Stop}, last legal S={run.LastLegalS:F6} m.");
    }

    private static void Native210(string root, string output, string candidate, string admission)
    {
        var source = ContextFor210(root);
        var policy = Read210(candidate);
        var checkedPlan = JsonSerializer.Deserialize<Admission210>(File.ReadAllText(admission), Json)!;
        if (checkedPlan.CandidateSha256 != Hash210(candidate) || !checkedPlan.Order.SequenceEqual(Official208) ||
            !checkedPlan.Races.Select(r => r.Fixture).SequenceEqual(Official208) ||
            checkedPlan.Races.Any(r => !r.Complete || r.Finish is null || r.Sweep < .5))
            throw new InvalidOperationException("The frozen candidate requires five original-start MODEL passes.");
        var law = Law210(source, policy);
        foreach (var recorded in checkedPlan.Races)
        {
            var fresh = RaceFrom210(RootFor210(root, recorded.Fixture), law);
            if (!fresh.Complete || fresh.Finish != recorded.Finish || Math.Abs(fresh.Sweep - recorded.Sweep) > 1e-9)
                throw new InvalidOperationException($"Independent preflight differs on {recorded.Fixture}.");
        }
        var fallback = Law210(source, policy, optionalRecovery: false, inheritedRecovery: false);
        foreach (string fixture in Official208)
        {
            RunNativeFeedback210(root, output, fixture, "H29-P", checkedPlan.Races, law, fallback);
            string path = Path.Combine(output, $"H29-P-{fixture}.result.json");
            var result = JsonSerializer.Deserialize<NativeResult208>(File.ReadAllText(path), Json)!;
            Console.WriteLine($"{fixture}: {result.Outcome} {result.Finish:F6} s, sweep {result.Sweep:F6}; decision max {result.Decisions.Max:F4} ms");
            if (!result.Finished || result.Decisions.Over50 != 0) break;
        }
    }
}
