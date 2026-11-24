using System.Diagnostics;
using System.Numerics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record ProbePoint210(HeadingSample198 Sample, double? RearUse, double? FrontLoad,
        double? YawMoment, double OutwardSpeed, double Course, bool RateLimited, bool AngleLimited);
    private sealed record Probe210(double Horizon, string? Stop, bool Complete, double? Finish,
        double LastLegalS, double Sweep, double PeakKph, double? First110S, int Steps,
        RacingFailure206? Failure, ContactFailure203? Contact, ProbePoint210[]? Points);

    private static void Probes210(string root, string output, string candidate)
    {
        var source = ContextFor210(root);
        var nominal = RootFor210(root, "C");
        var cases = new[] { ("A", Baseline210), ("selected", Read210(candidate)) };
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        var trials = new List<object>();
        const double cpuCap = 180;
        foreach (var (name, plan) in cases)
        {
            var law = Law210(source, plan);
            var physicalSteering = Law210(source, plan, optionalRecovery: false, inheritedRecovery: false);
            var physical = new Predictor198(Frame, Surface, Physics, Settings202("H28-F"), Timing,
                CourseFixture208(root, "C").Spec, Mass, Inertia);
            var trace = Follow209("C", nominal.Model, nominal.State, nominal.Goal, law, true).Trace!;
            for (int visit = 0; visit < 6; visit++)
            {
                var bend = source.Source.Policy.Bend(visit);
                double entry = visit < 5 ? source.Source.Policy.Bend(visit + 1).Entry : nominal.Goal;
                double next = visit < 5 ? source.Source.Policy.Bend(visit + 1).Exit + 16 : nominal.Goal;
                foreach (int offset in new[] { -12, 0, 12, 22, 34 })
                {
                    var at = trace.FirstOrDefault(row => row.Before.State.Rider.TotalReferenceMeters >= bend.Exit + offset &&
                        Math.Abs(row.Before.State.Time - row.Before.Review) < 1e-8)?.Before;
                    if (at is null || at.State.Rider.TotalReferenceMeters > entry) continue;
                    foreach (float gas in new[] { .85f, 1f })
                    {
                        // Change one queued request only. Every later review uses the same
                        // underlying policy, even when the trajectory has since diverged.
                        FreeRacingAction Pulse(Start197 state) => state.State.Time < at.State.Time + .025
                            ? law(state) with { Drive = gas } : law(state);
                        var guard = ProbeRun210(nominal.Model, at, nominal.Goal, Pulse, nominal.Goal, .25);
                        var supervisor = ProbeRun210(nominal.Model, at, nominal.Goal, Pulse, nominal.Goal, 1.2);
                        var nextBend = ProbeRun210(nominal.Model, at, nominal.Goal, Pulse, next, 14);
                        var finish = RaceFrom210(nominal with { State = at }, Pulse);
                        trials.Add(new { Case = name, Visit = visit + 1, Offset = offset, Gas = gas,
                            Mode = "one request", Initial = at, Original = law(at), Guard = guard,
                            Supervisor = supervisor, NextBend = nextBend, Race = finish });
                    }
                    if (offset != 22) continue;
                    foreach (int lead in new[] { 0, 7, 14, 22, 30, 32, 34, 36, 38, 40 })
                    {
                        double release = Math.Max(at.State.Rider.TotalReferenceMeters, entry - lead);
                        FreeRacingAction Held(Start197 state) => state.State.Rider.TotalReferenceMeters < release
                            ? law(state) with { Drive = 1 } : law(state);
                        var nextBend = ProbeRun210(nominal.Model, at, nominal.Goal, Held, next, 14, trace: lead is 0 or 22);
                        var finish = RaceFrom210(nominal with { State = at }, Held);
                        trials.Add(new { Case = name, Visit = visit + 1, Offset = offset, Gas = 1,
                            Mode = "hold until next entry minus lead", Lead = lead, ReleaseS = release,
                            Initial = at, Original = law(at), NextBend = nextBend, Race = finish });
                    }
                    FreeRacingAction Full(Start197 state) => physicalSteering(state) with { Drive = 1 };
                    var freeStraight = ProbeRun210(physical, at, nominal.Goal, Full, entry, 8, trace: true);
                    trials.Add(new { Case = name, Visit = visit + 1, Offset = offset, Gas = 1,
                        Mode = "physical straight", Initial = at, Original = law(at), Straight = freeStraight });
                }
                if ((process.TotalProcessorTime - cpu).TotalSeconds >= cpuCap)
                    throw new InvalidOperationException("Probe CPU limit reached before finishing registered bends.");
            }
        }
        Save(Path.Combine(output, "probes.json.gz"), new { Cases = trials, Cpu = (process.TotalProcessorTime - cpu).TotalSeconds,
            Wall = wall.Elapsed.TotalSeconds, CpuCap = cpuCap, NativeWorlds = 0 }, true);
        Console.WriteLine($"{trials.Count} matched-state acceleration and continuation probes.");

        var chosen = cases[1].Item2;
        var comparisons = new List<(string Name, Policy210 Policy)> { ("selected", chosen) };
        for (int visit = 0; visit < 6; visit++)
        {
            comparisons.Add(($"remove bend {visit + 1}", chosen with { Bends = chosen.Bends.Where(b => b.Visit != visit).ToArray(),
                Recovery = chosen.Recovery.Where(w => w.Visit != visit).ToArray() }));
        }
        comparisons.Add(("throttle pulses only", chosen with { Bends = chosen.Bends.Select(b => b with
            { Pulses = b.Pulses.Select(p => new PacePulse210(p.Drive, 0, 0, 0)).ToArray() }).ToArray() }));
        comparisons.Add(("steering pulses only", chosen with { Recovery = [], Bends = chosen.Bends.Select(b => b with
            { Pulses = b.Pulses.Select(p => p with { Drive = 0 }).ToArray() }).ToArray() }));
        var development = TrainingStarts207.Select(f => RootFor210(root, f)).ToArray();
        var started = process.TotalProcessorTime;
        var wallAblation = Stopwatch.StartNew();
        var ablations = comparisons.Select(x =>
        {
            var law = Law210(source, x.Policy);
            return new { x.Name, x.Policy, Races = development.Select(r => RaceFrom210(r, law)).ToArray() };
        }).ToArray();
        Save(Path.Combine(output, "ablations.json"), new { Ablations = ablations, Training = TrainingStarts207,
            Cpu = (process.TotalProcessorTime - started).TotalSeconds, Wall = wallAblation.Elapsed.TotalSeconds,
            Proposals = comparisons.Count, NativeWorlds = 0 });
    }

    private static Probe210 ProbeRun210(Predictor198 model, Start197 at, double goal,
        Func<Start197, FreeRacingAction> law, double end, double horizon, bool trace = false)
    {
        var cursor = model.BeginFeedback207(at, law, goal, .50);
        double peak = cursor.Snapshot.Speed;
        double legalS = cursor.Snapshot.S;
        double sweep = 100;
        double? first110 = peak * 3.6 > 110 ? cursor.Snapshot.S : null;
        List<ProbePoint210>? points = trace ? [] : null;
        while (cursor.Snapshot.S < end && cursor.Outcome is null && cursor.Snapshot.Time < at.State.Time + horizon - 1e-9)
        {
            cursor.Advance(1, at.State.Time + horizon);
            if (cursor.Outcome is not (null or "goal")) break;
            var sample = cursor.Snapshot;
            legalS = sample.S;
            sweep = Math.Min(sweep, cursor.LastSweep);
            peak = Math.Max(peak, sample.Speed);
            if (sample.Speed * 3.6 > 110) first110 ??= sample.S;
            if (!trace || cursor.LastContact is not { } contact) continue;
            var state = cursor.Checkpoint().State.Rider;
            var forces = contact.Forces;
            points!.Add(new(sample, cursor.LastPower?.Wheel.RearUtilization, forces.TyreLoads?.FrontNormalNewtons,
                forces.NetTorque, Vector2.Dot(state.Velocity, state.Road.Outward), Corridor199.CourseError(state),
                Math.Abs(forces.SteeringAfter - forces.SteeringRequest) > 1e-5,
                Math.Abs(forces.SteeringAfter) >= TwoContactBikeParameters.Default.MaximumSteeringRadians - 1e-5));
        }
        return new(horizon, cursor.Outcome, cursor.Reached, cursor.Reached ? cursor.GoalTime : null, legalS,
            sweep, peak * 3.6, first110, cursor.Steps, cursor.StrictFailure206, cursor.ContactFailure,
            points?.ToArray());
    }

    private static void RecoveryTrial210(string root, string output, string candidate)
    {
        var source = ContextFor210(root);
        var basis = Read210(candidate);
        var development = TrainingStarts207.Select(f => RootFor210(root, f)).ToArray();
        var screening = development.Concat(Screening208.Keys.Select(f => RootFor210(root, f))).ToArray();
        var menu = new List<Policy210> { basis };
        foreach (int visit in Enumerable.Range(0, 5))
        foreach (double gas in new[] { .35, .70, 1 })
        foreach (double end in new[] { 19d, 34 })
            menu.Add(basis with { Recovery = [new(visit, -5, end, gas, 0, 0, 0, true)] });
        foreach (double gas in new[] { .25, .55 })
            menu.Add(basis with { Recovery = Enumerable.Range(0, 5)
                .Select(v => new RecoveryWindow209(v, -5, 22, gas, 0, 0, 0, true)).ToArray() });
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var watch = Stopwatch.StartNew();
        var trials = menu.Select((plan, i) => Evaluate(i, plan, development)).ToArray();
        var finalists = Rank210(trials.Where(t => t.Races.All(r => r.Complete))).Take(8)
            .Select(t => Evaluate(t.Id, t.Policy, screening)).ToArray();
        if (finalists.All(f => f.Id != 0)) finalists = finalists.Append(Evaluate(0, basis, screening)).ToArray();
        var reference = finalists.Single(f => f.Id == 0);
        var best = Rank210(finalists).First();
        var timings = new List<double>();
        int guardFailures = 0;
        var feedback = Law210(source, best.Policy);
        var fallback = Law210(source, best.Policy, optionalRecovery: false, inheritedRecovery: false);
        var nominal = development[0];
        var replay = RaceFrom210(nominal, state =>
        {
            long start = Stopwatch.GetTimestamp();
            var action = feedback(state);
            var guard = nominal.Model.BeginFeedback207(state, at => at.State.Time < state.State.Time + .025
                ? action : fallback(at), nominal.Goal, .5, profile: true);
            guard.Advance(40, state.State.Time + .25);
            timings.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            if (!guard.Reached && (guard.Outcome is not null || guard.Snapshot.Time < state.State.Time + .25 - 1e-8))
                guardFailures++;
            return action;
        });
        var profile = DecisionTiming206.Measure(timings);
        const double maximumModelMilliseconds = 35;
        bool promote = best.Id != 0 && best.Races.All(r => r.Complete) && replay.Complete && guardFailures == 0 &&
            reference.Races.Max(r => r.Finish) - best.Races.Max(r => r.Finish)!.Value > .005 && profile.Max <= maximumModelMilliseconds;
        Save(Path.Combine(output, "candidate.json"), promote ? best.Policy : basis);
        Save(Path.Combine(output, "recovery.json.gz"), new { Trials = trials, Screening = finalists, Best = best.Id,
            Selected = promote ? best.Id : 0, GuardFailures = guardFailures, ModelDecisionTiming = profile,
            MaximumModelMilliseconds = maximumModelMilliseconds, MinimumWorstGainSeconds = .005,
            Cpu = (process.TotalProcessorTime - cpu).TotalSeconds, Wall = watch.Elapsed.TotalSeconds,
            MaximumProposals = 33, MaximumScreened = 9, MaximumStepsPerRace = 10000, NativeWorlds = 0 }, true);
        Console.WriteLine($"F best {best.Id}, selected {(promote ? best.Id : 0)}; guard failures {guardFailures}, max {profile.Max:F3} ms.");

        Trial210 Evaluate(int id, Policy210 p, Root210[] roots)
        {
            var law = Law210(source, p);
            return new(id, 0, "predictive recovery", p, roots.Select(r => RaceFrom210(r, law)).ToArray());
        }
    }

    private static void ReserveTrial210(string root, string output, string candidate)
    {
        var source = ContextFor210(root);
        var basis = Read210(candidate);
        var roots = TrainingStarts207.Concat(Screening208.Keys).Select(f => RootFor210(root, f)).ToArray();
        // The largest observed #209 native sweep deficit was 1.915 mm. The study
        // uses approximately three times that as a development selection reserve.
        // The actual strict legality and native observer still use exactly 0.50 m.
        const double targetSweep = .506;
        var candidates = new List<Policy210> { basis };
        foreach (double end in new[] { 12d, 15, 17, 19 })
        foreach (double gas in new[] { .30, .45, .55, .65, .75, .9, 1 })
            candidates.Add(basis with { Recovery = [new(0, -5, end, gas, 0, 0, 0, true)] });
        var rows = new List<Trial210>();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var clock = Stopwatch.StartNew();
        Check(candidates, "early window", 0);
        var early = Select();
        candidates.Clear();
        foreach (double restore in new[] { 0d, .10, .25, .45, .70, 1 })
        foreach (int stage in new[] { 3, 4, 5 })
        {
            var edits = early.Policy.Bends.Select(b => b.Visit == 5 ? b with { Pulses = b.Pulses.Select((p, i) =>
                i == stage ? p with { Drive = ExitPolicy208.Lerp(p.Drive, 1, restore) } : p).ToArray() } : b).ToArray();
            candidates.Add(early.Policy with { Bends = edits });
        }
        Check(candidates, "sixth exit interaction", early.Id);
        var winner = Select();
        Save(Path.Combine(output, "candidate.json"), winner.Policy);
        Save(Path.Combine(output, "reserve.json.gz"), new { Trials = rows, Winner = winner.Id, TargetSweep = targetSweep,
            Development = roots.Select(r => r.Fixture), EarlyWindowWinner = early.Id, Cpu = (process.TotalProcessorTime - cpu).TotalSeconds,
            Wall = clock.Elapsed.TotalSeconds, StepCapPerRace = 10000, ProposalCap = 47, Workers = 4, NativeWorlds = 0 }, true);
        Console.WriteLine($"Reserve winner {winner.Id}, worst {winner.Races.Max(r => r.Finish):F6} s, sweep {winner.Races.Min(r => r.Sweep):F6} m.");

        Trial210 Select() => Rank210(rows.Where(t => t.Races.All(r => r.Complete && r.Sweep >= targetSweep))).First();

        void Check(List<Policy210> plans, string stage, int parent)
        {
            int begin = rows.Count;
            var results = new Trial210[plans.Count];
            Parallel.For(0, plans.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
            {
                var law = Law210(source, plans[i]);
                results[i] = new(begin + i + 1, parent, stage, plans[i], roots.Select(r => RaceFrom210(r, law)).ToArray());
            });
            rows.AddRange(results);
        }
    }
}
