using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Guard210(double Time, double S, bool Passed, string? Stop, double Sweep,
        FreeRacingAction Action, RacingFailure206? Failure, ContactFailure203? Contact);
    private sealed record RepairRow210(Trial210 Trial, Guard210[] NativeStress);

    private static Guard210 GuardFrom210(Root210 nominal, Start197 state, Func<Start197, FreeRacingAction> law,
        Func<Start197, FreeRacingAction> fallback)
    {
        var action = law(state);
        var guard = nominal.Model.BeginFeedback207(state, at => at.State.Time < state.State.Time + .025
            ? action : fallback(at), nominal.Goal, .50, profile: true);
        guard.Advance(40, state.State.Time + .25);
        bool passed = guard.Reached || guard.Outcome is null && guard.Snapshot.Time >= state.State.Time + .25 - 1e-8;
        return new(state.State.Time, state.State.Rider.TotalReferenceMeters, passed, guard.Outcome, guard.Sweep,
            action, guard.StrictFailure206, guard.ContactFailure);
    }

    private static void Repair210(string root, string output, string failed, string reservePath)
    {
        using var nativeStream = new GZipStream(File.OpenRead(failed), CompressionMode.Decompress);
        using var native = JsonDocument.Parse(nativeStream);
        var result = native.RootElement.GetProperty("Result").Deserialize<NativeResult208>(Json)!;
        if (result.Finished || result.FirstFailure is null || result.Fixture != "C")
            throw new InvalidDataException("Repair uses the existing failed nominal recording only.");
        using var searchStream = new GZipStream(File.OpenRead(reservePath), CompressionMode.Decompress);
        using var search = JsonDocument.Parse(searchStream);
        var rows = search.RootElement.GetProperty("Trials").Deserialize<Trial210[]>(Json)!;
        var unchangedTail = rows.Single(r => r.Id == search.RootElement.GetProperty("EarlyWindowWinner").GetInt32()).Policy;
        var old = rows.Single(r => r.Id == search.RootElement.GetProperty("Winner").GetInt32()).Policy;
        var checkpoints = native.RootElement.GetProperty("Decisions").EnumerateArray().TakeLast(9)
            .Select(d => d.GetProperty("State").Deserialize<Start197>(Json)!).ToArray();

        var source = ContextFor210(root);
        var development = TrainingStarts207.Concat(Screening208.Keys).Select(f => RootFor210(root, f)).ToArray();
        var nominal = development[0];
        var oldLaw = Law210(source, old);
        var oldFallback = Law210(source, old, optionalRecovery: false, inheritedRecovery: false);
        var reproduced = GuardFrom210(nominal, checkpoints[^1], oldLaw, oldFallback);
        if (reproduced.Passed || reproduced.Stop is null || !reproduced.Stop.Contains("AmbiguousOrMultipleRoots", StringComparison.Ordinal))
            throw new InvalidDataException("The prior nominal native guard rejection did not reproduce.");

        var menu = new List<(string, Policy210)> { ("earlier sixth-bend reference from reserve screen", unchangedTail) };
        foreach (double scale in new[] { 0d, .2, .4, .6, .8, .9, 1 })
        foreach (double tail in new[] { -.055, 0, .055 })
        {
            var edits = unchangedTail.Bends.Select(b => b.Visit == 5 ? b with { Pulses = b.Pulses.Select((p, i) =>
                new PacePulse210(p.Drive * scale + (i == 4 ? tail : 0), p.Steer * scale, p.Line * scale,
                    p.Alignment * scale)).ToArray() } : b).ToArray();
            menu.Add(($"sixth scale {scale}, recovery drive delta {tail}", unchangedTail with { Bends = edits }));
        }
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var clock = Stopwatch.StartNew();
        var tested = new RepairRow210[menu.Count];
        Parallel.For(0, menu.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            var (label, policy) = menu[i];
            var law = Law210(source, policy);
            var fallback = Law210(source, policy, optionalRecovery: false, inheritedRecovery: false);
            var stress = checkpoints.Select(x => GuardFrom210(nominal, x, law, fallback)).ToArray();
            tested[i] = new(new Trial210(i + 1, 0, label, policy,
                development.Select(r => RaceFrom210(r, law)).ToArray()), stress);
        });

        var finalists = Rank210(tested.Where(r => r.NativeStress.All(g => g.Passed)).Select(r => r.Trial)
            .Where(t => t.Races.All(r => r.Complete && r.Sweep >= .506))).Take(5).ToArray();
        var guardChecks = new List<object>();
        Trial210? winner = null;
        foreach (var trial in finalists)
        {
            var policy = trial.Policy;
            var law = Law210(source, policy);
            var fallback = Law210(source, policy, optionalRecovery: false, inheritedRecovery: false);
            var failures = new List<Guard210>();
            var timings = new List<double>();
            var rollouts = development.Select(origin => RaceFrom210(origin, state =>
            {
                long start = Stopwatch.GetTimestamp();
                var guard = GuardFrom210(origin, state, law, fallback);
                timings.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                if (!guard.Passed) failures.Add(guard);
                return guard.Action;
            })).ToArray();
            var timing = DecisionTiming206.Measure(timings);
            guardChecks.Add(new { trial.Id, Races = rollouts, Timing = timing, Failures = failures });
            if (failures.Count == 0 && rollouts.All(r => r.Complete) && timing.Max <= 40)
            {
                winner = trial;
                break;
            }
        }
        Save(Path.Combine(output, "repair.json.gz"), new { Reproduced = reproduced,
            NativeFixtureUsed = "nominal C, already a development start", NativeGuardCheckpoints = checkpoints,
            Trials = tested, FinalistGuardChecks = guardChecks, Winner = winner?.Id,
            Proposals = menu.Count, Workers = 4, StepCapPerRace = 10000, FinalistCap = 5, Reserve = .506,
            Cpu = (process.TotalProcessorTime - cpu).TotalSeconds, Wall = clock.Elapsed.TotalSeconds, NativeWorlds = 0 }, true);
        if (winner is null) throw new InvalidOperationException("No development repair met all guard and strict requirements.");
        Save(Path.Combine(output, "candidate.json"), winner.Policy);
        Console.WriteLine($"Repair {winner.Id}: worst {winner.Races.Max(r => r.Finish):F6} s; sweep {winner.Races.Min(r => r.Sweep):F6} m.");
    }

    private static void Fresh210(string root, string output, string candidate)
    {
        var source = ContextFor210(root);
        var policy = Read210(candidate);
        var law = Law210(source, policy);
        var (seed, _, registered) = Fixture206(root, "C");
        var rows = new List<object>();
        foreach (float offset in new[] { -.09f, .09f, -.20f, .20f })
        {
            var spec = registered with { OffsetMeters = registered.OffsetMeters + offset,
                TargetOffsetMeters = registered.TargetOffsetMeters + offset };
            var pose = Frame.At(spec.ReferenceMeters, spec.OffsetMeters);
            var rider = seed.Start.State.Rider with { Position = pose.Position, Road = Frame.Project(pose.Position),
                Heading = (float)pose.Road.Heading, TargetOffsetMeters = (float)spec.TargetOffsetMeters };
            var start = seed.Start with { State = seed.Start.State with { Rider = rider } };
            string fixture = $"fresh-{(offset < 0 ? "minus" : "plus")}-{Math.Round(Math.Abs(offset) * 100):F0}cm";
            var run = Follow209(fixture, Model208(spec), start, seed.Goal, law, true);
            Save(Path.Combine(output, fixture + ".json.gz"), run, true);
            rows.Add(new { OffsetMeters = offset, Outcome = run with { Trace = null } });
            Console.WriteLine($"{fixture}: {run.Stop}, {run.Finish:F6} s.");
        }
        Save(Path.Combine(output, "holdouts.json"), new { CandidateSha256 = Hash210(candidate), Cases = rows, NativeWorlds = 0 });
    }
}
