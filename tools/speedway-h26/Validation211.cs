using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record GuardReceipt211(string Fixture, int Reviews, DecisionTiming206 Timing, Guard210[] Failures);
    private sealed record Preflight211(string CandidateSha256, string ProtocolSha256, Run209[] Registered, Run209[] Fresh,
        Run209[] Disturbances, GuardReceipt211[] GuardChecks, Guard210[] NativeStress);

    private static Root210 Offset211(string root, string name, float offset)
    {
        var (seed, _, reference) = Fixture206(root, "C");
        var spec = reference with { OffsetMeters = reference.OffsetMeters + offset,
            TargetOffsetMeters = reference.TargetOffsetMeters + offset };
        var pose = Frame.At(spec.ReferenceMeters, spec.OffsetMeters);
        var rider = seed.Start.State.Rider with { Position = pose.Position, Road = Frame.Project(pose.Position),
            Heading = (float)pose.Road.Heading, TargetOffsetMeters = (float)spec.TargetOffsetMeters };
        return new(name, Model208(spec), seed.Start with { State = seed.Start.State with { Rider = rider } }, seed.Goal);
    }

    private static (Race210 Race, GuardReceipt211 Guard) GuardRace211(Source211 source, Policy211 plan,
        Root210 origin, Func<FreeRacingAction, Start197, FreeRacingAction>? disturbance = null)
    {
        var law = Law211(source, plan);
        if (disturbance is not null)
        {
            var nominalLaw = law;
            law = at => disturbance(nominalLaw(at), at);
        }
        var fallback = Law211(source, plan, windowRecovery: false);
        var failures = new List<Guard210>();
        var timings = new List<double>();
        var run = RaceFrom210(origin, state =>
        {
            long start = Stopwatch.GetTimestamp();
            var receipt = GuardFrom210(origin, state, law, fallback);
            timings.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            if (!receipt.Passed) failures.Add(receipt);
            // Keep a rejected simulation for diagnosis only. Native always stops before actuation.
            return receipt.Action;
        });
        return (run, new(origin.Fixture, timings.Count, DecisionTiming206.Measure(timings), failures.ToArray()));
    }

    private static void Select211(string root, string output, string folder)
    {
        var source = SourceFor211(root);
        var training = Development211(root, true);
        var states = FailedNativeStates211(root);
        var pool = new List<Policy211>();
        foreach (string arm in new[] { "A", "B", "C", "D1", "D3", "D4", "D6", "E", "E2", "R" })
        {
            string archivePath = Path.Combine(folder, arm, "search.json.gz");
            if (arm == "R" && !File.Exists(archivePath)) continue;
            using var zip = new GZipStream(File.OpenRead(archivePath), CompressionMode.Decompress);
            using var archive = JsonDocument.Parse(zip);
            pool.AddRange(archive.RootElement.GetProperty("Screened").Deserialize<Sample211[]>(Json)!.Where(Legal211)
                .Select(s => s.Policy));
        }
        foreach (string arm in new[] { "B", "E", "E2" })
        {
            var joint = Read211(Path.Combine(folder, arm, "candidate.json"));
            foreach (double r in new[] { .4, .5, .55, .60, .65, .70, .80, .90, 1d })
                pool.Add(joint with { FirstRecovery = r });
        }
        pool = pool.DistinctBy(p => JsonSerializer.Serialize(p, Json)).ToList();
        var ids = Enumerable.Range(0, pool.Count).ToArray();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        var tested = new Sample211[pool.Count];
        Parallel.ForEach(ids, new ParallelOptions { MaxDegreeOfParallelism = 4 }, id =>
        {
            var law = Law211(source, pool[id]);
            tested[id] = new(id + 1, 0, "selection time-zero development", pool[id],
                training.Select(r => Measure211(r, law, source, [0, 1, 2, 3, 4])).ToArray());
        });
        var eligible = Rank211(tested.Where(Legal211)).ToArray();
        var first = Choose211(eligible);
        var sequence = new[] { first }.Concat(eligible.Where(s => Reserve211(s) >= .55)).Concat(eligible)
            .DistinctBy(s => s.Id).Take(10);
        var audits = new List<object>();
        Sample211? winner = null;
        foreach (var trial in sequence)
        {
            var law = Law211(source, trial.Policy);
            var fallback = Law211(source, trial.Policy, windowRecovery: false);
            var stress = states.Select(x => GuardFrom210(training[0], x, law, fallback)).ToArray();
            var receipts = new List<GuardReceipt211>();
            if (stress.All(g => g.Passed))
                foreach (var origin in training)
                {
                    var replay = GuardRace211(source, trial.Policy, origin);
                    receipts.Add(replay.Guard);
                    if (!replay.Race.Complete || replay.Guard.Failures.Length != 0) break;
                }
            audits.Add(new { trial.Id, Stress = stress, MODEL = receipts });
            if (receipts.Count == training.Length && receipts.All(r => r.Failures.Length == 0))
            {
                winner = trial;
                break;
            }
        }
        var bands = new[] { .50, .52, .55, .59 }.Select(x => new
        {
            Target = x, Best = eligible.FirstOrDefault(t => Reserve211(t) >= x)?.Id,
            Count = eligible.Count(t => Reserve211(t) >= x)
        }).ToArray();
        var pareto = eligible.Where(x => !eligible.Any(y => y.Id != x.Id &&
            Worst211(y) <= Worst211(x) && Reserve211(y) >= Reserve211(x) &&
            y.Results.Min(r => r.Over105) >= x.Results.Min(r => r.Over105) &&
            (Worst211(y) < Worst211(x) || Reserve211(y) > Reserve211(x) ||
                y.Results.Min(r => r.Over105) > x.Results.Min(r => r.Over105)))).Select(x => x.Id).ToArray();
        Save(Path.Combine(output, "selection.json.gz"), new { Policy = winner?.Policy, Winner = winner?.Id,
            Trials = tested, Audits = audits, Bands = bands, Pareto = pareto, MaxFinalists = 10,
            Cpu = (process.TotalProcessorTime - cpu).TotalSeconds, Wall = wall.Elapsed.TotalSeconds, NativeWorlds = 0 }, true);
        if (winner is not null)
        {
            Save(Path.Combine(output, "candidate.json"), winner.Policy);
            Console.WriteLine($"Final development candidate {winner.Id}: worst {Worst211(winner):F6}, minimum swept {Reserve211(winner):F6}.");
        }
    }

    private static void Admit211(string root, string output, string candidate)
    {
        var source = SourceFor211(root);
        var plan = Read211(candidate);
        var feedback = Law211(source, plan);
        var cases = Official208.Select(f => RootFor210(root, f)).ToArray();
        var fresh = new[] { -.035f, .035f, -.145f, .145f }.Select((offset, i) => Offset211(root,
            new[] { "fresh-minus-3.5", "fresh-plus-3.5", "fresh-minus-14.5", "fresh-plus-14.5" }[i], offset)).ToArray();
        var disturbances = new[] { Offset211(root, "stress-launch", -.035f), Offset211(root, "stress-running", .145f) };
        Run209 Record(Root210 origin, Func<Start197, FreeRacingAction> law)
        {
            var r = Follow209(origin.Fixture, origin.Model, origin.State, origin.Goal, law, true);
            Save(Path.Combine(output, r.Fixture + ".json.gz"), r, true);
            Console.WriteLine($"{r.Fixture}: {r.Stop}; {r.Finish:F6} s; swept {r.Sweep:F6}; peak {r.PeakKph:F3}.");
            return r with { Trace = null };
        }
        var original = cases.Select(c => Record(c, feedback)).ToArray();
        var unseen = fresh.Select(c => Record(c, feedback)).ToArray();
        var stress = disturbances.Select((origin, i) => Record(origin, state =>
        {
            var action = feedback(state);
            double time = i == 0 ? 1.35 : 17.5;
            return Math.Abs(state.State.Time - time) <= 1e-8
                ? ExitPolicy208.Bound(action.SteeringRadians + (i == 0 ? .001 : -.001), action.Drive - .015) : action;
        })).ToArray();

        var guards = cases.Concat(fresh).Select(r => GuardRace211(source, plan, r).Guard).ToArray();
        var archived = FailedNativeStates211(root).Select(a => GuardFrom210(cases[0], a, feedback,
            Law211(source, plan, windowRecovery: false))).ToArray();
        var admission = new Preflight211(Hash210(candidate), Hash210(Path.Combine(root, Evidence211, "protocol.json")),
            original, unseen, stress, guards, archived);
        Save(Path.Combine(output, "admission.json"), admission);
    }

    private static void Native211(string root, string output, string candidate, string admission)
    {
        var locked = JsonSerializer.Deserialize<Preflight211>(File.ReadAllText(admission), Json)!;
        if (locked.CandidateSha256 != Hash210(candidate) || locked.ProtocolSha256 != Hash210(Path.Combine(root, Evidence211, "protocol.json")) ||
            !locked.Registered.Select(r => r.Fixture).SequenceEqual(Official208) || locked.Fresh.Length != 4 ||
            locked.Registered.Concat(locked.Fresh).Any(r => !r.Complete || r.Finish is null || r.Sweep < .50) ||
            locked.GuardChecks.Length != 9 || locked.GuardChecks.Any(r => r.Reviews == 0 || r.Failures.Length != 0) ||
            locked.NativeStress.Length != 9 || locked.NativeStress.Any(r => !r.Passed))
            throw new InvalidOperationException("Required native preflight evidence is incomplete.");
        var source = SourceFor211(root);
        var plan = Read211(candidate);
        var law = Law211(source, plan);
        var fallback = Law211(source, plan, windowRecovery: false);
        foreach (var row in locked.Registered)
        {
            var replay = RaceFrom210(RootFor210(root, row.Fixture), law);
            if (!replay.Complete || replay.Finish != row.Finish || Math.Abs(replay.Sweep - row.Sweep) > 1e-9)
                throw new InvalidDataException("Frozen admission changed.");
        }
        foreach (string fixture in Official208)
        {
            RunNativeFeedback210(root, output, fixture, "H30-C", locked.Registered, law, fallback);
            var result = JsonSerializer.Deserialize<NativeResult208>(
                File.ReadAllText(Path.Combine(output, "H30-C-" + fixture + ".result.json")), Json)!;
            Console.WriteLine($"{fixture}: {result.Outcome}; {result.Finish:F6} s; {result.Sweep:F6} m; {result.Decisions.Max:F4} ms maximum.");
            if (!result.Finished || result.Decisions.Over50 != 0) break;
        }
    }
}
