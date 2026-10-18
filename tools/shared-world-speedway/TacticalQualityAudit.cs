using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public sealed record QualityCase(string Name, Scenario Scenario, double ObserveAt, int RiderId, bool Defending = false);
public sealed record QualityTrial(string Candidate, float Target, bool Available, double[] ActualGain,
    double[] LegacyGain, string Hash, bool PrefixExact, double PeakLateralBudgetRatio,
    double MinimumSpeed, double TangentialImpulseNewtonSeconds, int Contacts);
public sealed record QualityEvidence(string Name, RiderSpec Spec, RiderView Observation, double Activation,
    double[] Horizons, double MovementHorizon, QualityTrial[] Trials);

/// <summary>Offline, authored commands from the same initial state. Never used by TrafficDecision.</summary>
public static class TacticalQualityAudit
{
    public static void ScoringCost(string frozenDirectory, string outputDirectory)
    {
        var stored = JsonSerializer.Deserialize<QualityEvidence[]>(File.ReadAllText(Path.Combine(frozenDirectory, "baseline.json")), Audit.Json)!;
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var surface = TrackState.CreateDefault(frame.Track).Snapshot(); var p = new WorldParameters();
        var inputs = stored.Select(c => {
            var definition = Cases().Single(x => x.Name == c.Name);
            return (c.Observation, Spec: definition.Scenario.Riders.Single(x => x.Profile.Id == definition.RiderId), definition.Defending);
        }).ToArray();
        var results = new List<object>(); double checksum = 0;
        foreach (var quality in new[] {false, true})
        {
            var elapsed = new List<double>(); long allocated = 0; const int samples = 5, passes = 20;
            double Score((RiderView Observation, RiderSpec Spec, bool Defending) c, float target)
            {
                var horizon = quality ? ContestScoring.HorizonSeconds(c.Observation, c.Spec, p, frame, c.Defending)
                    : ContestScoring.MovementSeconds(c.Spec, p, c.Defending);
                return quality ? ContestScoring.Progress(c.Observation, c.Spec, p, frame, surface, target, horizon) / horizon
                    : ContestScoring.LegacyRate(c.Observation, c.Spec, p, frame, surface, target, horizon);
            }
            foreach (var c in inputs) checksum += Score(c, c.Observation.TargetOffsetMeters);
            for (var i = 0; i < samples; i++)
            {
                var allocation = GC.GetAllocatedBytesForCurrentThread(); var start = System.Diagnostics.Stopwatch.GetTimestamp();
                for (var n = 0; n < passes; n++) foreach (var c in inputs)
                    for (var direction = -1; direction <= 1; direction++) {
                        var target = (float)c.Observation.Road.OffsetMeters + direction;
                        if (target is >= .5f and <= 9.5f) checksum += Score(c, target);
                    }
                elapsed.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                allocated += GC.GetAllocatedBytesForCurrentThread() - allocation;
            }
            elapsed.Sort(); var calls = passes * inputs.Sum(c => Enumerable.Range(-1, 3)
                .Count(d => c.Observation.Road.OffsetMeters + d is >= .5 and <= 9.5));
            results.Add(new {Quality = quality, Samples = samples, CallsPerSample = calls,
                MicrosecondsPerScore = elapsed[samples / 2] * 1000 / calls,
                AllocatedBytesPerScore = (double)allocated / samples / calls, Checksum = checksum});
        }
        File.WriteAllText(Path.Combine(outputDirectory, "scoring-cost.json"), JsonSerializer.Serialize(results, Audit.Json));
    }

    public static void Races(string directory)
    {
        Directory.CreateDirectory(directory); var results = new List<object>();
        using var candidates = File.CreateText(Path.Combine(directory, "race-candidates.csv"));
        candidates.WriteLine("case,quality,time_s,rider,target_m,available,baseline_score_mps,candidate_score_mps,margin_mps");
        foreach (var scenario in new[] {SideBySideScenarios.A(), SideBySideScenarios.E(), SideBySideScenarios.F()})
        {
            var old = TacticalAudit.Execute(scenario, .01, true, side: true, contestObserver: c => Row(false, c));
            var quality = TacticalAudit.Execute(scenario, .01, true, side: true, quality: true, contestObserver: c => Row(true, c));
            var comparison = SideBySideAudit.Compare(scenario, old, quality);
            var prefix = Prefix(scenario, old, quality);
            var ids = scenario.Riders.Select(r => r.Profile.Id).ToArray();
            var checkpoints = new[] {4.344, 4.805, 6, 7.344, 12}.Where(t => t <= scenario.DurationSeconds)
                .Select(t => new {Time = t, DeltaReference = ids.Select(id => Progress(quality.Samples, t, id) - Progress(old.Samples, t, id)).ToArray()});
            results.Add(new {scenario.Name, Prefix = prefix,
                comparison.FirstPositionDifferenceAt, comparison.FirstLateralDifferenceAt,
                comparison.MaximumPositionDifferenceMeters, comparison.MaximumReferenceDifferenceMeters,
                comparison.MaximumFirstBendPositionDifferenceMeters, comparison.MaximumFirstBendReferenceDifferenceMeters,
                comparison.FinalReferenceLeadDifferenceMeters, Checkpoints = checkpoints,
                Old = old.Result, Quality = quality.Result, OldResolutions = SideBySideAudit.Resolutions(old),
                QualityResolutions = SideBySideAudit.Resolutions(quality), quality.MaximumGripRatio});
            void Row(bool enabled, ContestObservation c) => candidates.WriteLine(FormattableString.Invariant(
                $"{scenario.Name},{enabled},{c.TimeSeconds:R},{c.RiderId},{c.TargetMeters:R},{c.Available},{c.BaselineReferenceSpeed:R},{c.CandidateReferenceSpeed:R},{c.RequiredGain:R}"));
        }
        File.WriteAllText(Path.Combine(directory, "races.json"), JsonSerializer.Serialize(results, Audit.Json));
    }

    public static (double? FirstDifferentActivation, bool Exact, int CommonSamples) Prefix(Scenario s, TacticalEvidence old, TacticalEvidence changed)
    {
        DecisionEvent[] Effective(TacticalEvidence e, RiderSpec r)
        {
            var target = r.TargetOffsetMeters; var drive = 1f; var events = new List<DecisionEvent>();
            foreach (var d in e.Result.Decisions.Where(d => d.RiderId == r.Profile.Id && d.ActualActivation is not null))
            {
                if (target != d.TargetOffsetMeters || drive != d.Drive) events.Add(d);
                target = d.TargetOffsetMeters; drive = d.Drive;
            }
            return events.ToArray();
        }
        double? first = null;
        foreach (var r in s.Riders)
        {
            var a = Effective(old, r); var b = Effective(changed, r);
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                if (i < a.Length && i < b.Length && a[i].ActualActivation == b[i].ActualActivation
                    && a[i].TargetOffsetMeters == b[i].TargetOffsetMeters && a[i].Drive == b[i].Drive) continue;
                var time = Math.Min(i < a.Length ? a[i].ActualActivation!.Value : double.PositiveInfinity,
                    i < b.Length ? b[i].ActualActivation!.Value : double.PositiveInfinity);
                first = Math.Min(first ?? double.PositiveInfinity, time); break;
            }
        }
        var times = old.Samples.ToDictionary(s => Math.Round(s.TimeSeconds, 9));
        var common = changed.Samples.Where(s => s.TimeSeconds <= (first ?? s.TimeSeconds) + 1e-10
            && times.ContainsKey(Math.Round(s.TimeSeconds, 9))).ToArray();
        return (first, common.All(s => s.Riders.SequenceEqual(times[Math.Round(s.TimeSeconds, 9)].Riders)), common.Length);
    }

    public static void Evaluate(string frozenDirectory, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var cases = JsonSerializer.Deserialize<QualityEvidence[]>(File.ReadAllText(Path.Combine(frozenDirectory, "baseline.json")), Audit.Json)!;
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var surface = TrackState.CreateDefault(frame.Track).Snapshot(); var p = new WorldParameters();
        using var csv = File.CreateText(Path.Combine(outputDirectory, "predictions.csv"));
        csv.WriteLine("case,candidate,available,horizon_s,checkpoint,old_gain_m,new_gain_m,actual_gain_m,target_m");
        var choices = new List<object>();
        foreach (var stored in cases)
        {
            // RiderProfile.Gameplay is deliberately JsonIgnore in the domain API.
            // Reuse the frozen authored definition, never its lossy JSON round trip.
            var definition = Cases().Single(x => x.Name == stored.Name);
            var c = stored with {Spec = definition.Scenario.Riders.Single(x => x.Profile.Id == definition.RiderId)};
            var defending = definition.Defending;
            var window = ContestScoring.HorizonSeconds(c.Observation, c.Spec, p, frame, defending);
            double Gain(float target, double t) => ContestScoring.Progress(c.Observation, c.Spec, p, frame, surface, target, t)
                - ContestScoring.Progress(c.Observation, c.Spec, p, frame, surface, c.Observation.TargetOffsetMeters, t);
            var valid = c.Trials.Where(t => t.Available).ToArray();
            var quality = ((defending ? c.Spec.Profile.Gameplay.Abilities.Defense : c.Spec.Profile.Gameplay.Abilities.Attack) - 1) / 98d;
            var margin = .12 + .15 * (1 - quality);
            var oldChoice = valid.Where(t => t.LegacyGain[1] / 2 > margin).OrderByDescending(t => t.LegacyGain[1]).FirstOrDefault() ?? valid[0];
            var newChoice = valid.Where(t => Gain(t.Target, window) / window > margin).OrderByDescending(t => Gain(t.Target, window)).FirstOrDefault() ?? valid[0];
            choices.Add(new {c.Name, WindowSeconds = window, OldChoice = oldChoice.Candidate, NewChoice = newChoice.Candidate,
                ActualBestAtTwoSeconds = valid.MaxBy(t => t.ActualGain[1])!.Candidate,
                OldActualGainAtTwoSeconds = oldChoice.ActualGain[1], NewActualGainAtTwoSeconds = newChoice.ActualGain[1],
                OldForecastAtWindow = oldChoice.LegacyGain[1] * window / 2, NewForecastAtWindow = Gain(newChoice.Target, window), MarginMps = margin});
            foreach (var t in c.Trials)
            {
                if (!t.Available) {csv.WriteLine($"{c.Name},{t.Candidate},false,,,,,,{t.Target.ToString(CultureInfo.InvariantCulture)}"); continue;}
                for (var i = 0; i < c.Horizons.Length; i++)
                    csv.WriteLine(FormattableString.Invariant($"{c.Name},{t.Candidate},true,{c.Horizons[i]:R},{(i == 3 ? "window-end" : "after-" + (i + 1))},{t.LegacyGain[i]:R},{Gain(t.Target, c.Horizons[i]):R},{t.ActualGain[i]:R},{t.Target:R}"));
            }
        }
        File.WriteAllText(Path.Combine(outputDirectory, "choices.json"), JsonSerializer.Serialize(choices, Audit.Json));
        File.WriteAllText(Path.Combine(outputDirectory, "inputs.json"), JsonSerializer.Serialize(Cases().Select(c => new {
            c.Name, c.ObserveAt, c.RiderId, c.Defending, c.Scenario,
            Gameplay = c.Scenario.Riders.Select(r => new {r.Profile.Id, r.Profile.Gameplay})}), Audit.Json));
        Console.WriteLine(JsonSerializer.Serialize(choices, Audit.Json));
    }
    public static void ReactionProbe(string frozenDirectory, string outputDirectory)
    {
        var c = Cases().Single(c => c.Name == "H-original-F");
        var stored = JsonSerializer.Deserialize<QualityEvidence[]>(File.ReadAllText(Path.Combine(frozenDirectory, "baseline.json")), Audit.Json)!
            .Single(x => x.Name == c.Name);
        var target = stored.Trials.Single(t => t.Candidate == "inside").Target;
        var hold = Execute(c.Scenario, new[] {new ControlCommand(c.RiderId, c.ObserveAt, stored.Observation.TargetOffsetMeters, 1)}, c.RiderId);
        var immediate = Execute(c.Scenario, new[] {new ControlCommand(c.RiderId, c.ObserveAt, target, 1)}, c.RiderId);
        var comparison = stored.Horizons.Take(3).Select((t, i) => new {SecondsAfterDelayedActivation = t,
            DelayedGain = stored.Trials.Single(t => t.Candidate == "inside").ActualGain[i],
            ImmediateGainAtSameWorldTime = Progress(immediate.Samples, stored.Activation + t, c.RiderId)
                - Progress(hold.Samples, stored.Activation + t, c.RiderId)});
        File.WriteAllText(Path.Combine(outputDirectory, "reaction.json"), JsonSerializer.Serialize(new {
            ReactionSeconds = stored.Activation - c.ObserveAt, DelayedGainBeforeActivation = 0,
            ImmediateGainAtDelayedActivation = Progress(immediate.Samples, stored.Activation, c.RiderId)
                - Progress(hold.Samples, stored.Activation, c.RiderId), Comparison = comparison}, Audit.Json));
    }
    public static IReadOnlyList<QualityCase> Cases()
    {
        var seed = SideBySideScenarios.BInside().Riders[0];
        QualityCase Pair(string name, float offset, float speed, float slide = 70, double reference = 45,
            float? target = null, float outward = 0, int technique = 70, int attack = 80, int defense = 75, bool defending = false)
        {
            var gameplay = new RiderGameplayProfile(new(50, 50, technique, 65, attack, defense, 50, 60),
                seed.Profile.Gameplay.Physical, seed.Profile.Gameplay.InteractionStyle);
            var own = seed with {ReferenceMeters = reference, OffsetMeters = offset, SpeedMetersPerSecond = speed,
                TargetOffsetMeters = target ?? offset, InitialOutwardSpeed = outward, TrafficEnabled = false,
                Profile = RiderProfile.CreateCanonical(1, "R1", gameplay, new(50, 50, slide, 50, 50, 50), seed.Profile.Style)};
            // Fixed distant peer isolates own cost; no reactive commands or collisions.
            var peer = SideBySideScenarios.BInside().Riders[1] with {ReferenceMeters = reference + (defending ? -35 : 35), OffsetMeters = 8,
                TargetOffsetMeters = 8, TrafficEnabled = false};
            return new(name, new(name, new[] {own, peer}, 4), .1, 1, defending);
        }
        // Freeze this explicit set before changing the scoring model. Names describe hypotheses.
        var f = SideBySideScenarios.F();
        return new[] {
            Pair("A-inner-hold", .6f, 20, 50), Pair("B-small-tighten", 3, 17),
            Pair("C-loaded-tighten", 3, 22), Pair("D-fast-outer", 6, 22, 50),
            Pair("E-slow-outer", 5, 17), Pair("F-entry-16", 3, 16, reference: 36),
            Pair("F-entry-19", 3, 19, reference: 36), Pair("F-entry-23", 3, 23, reference: 36),
            Pair("G-low-tech-attack", 3, 20, technique: 35, attack: 35, defense: 70),
            Pair("G-high-tech-attack", 3, 20, technique: 90, attack: 90, defense: 35),
            Pair("G-defense", 3, 20, technique: 60, attack: 40, defense: 90, defending: true),
            new QualityCase("H-original-F", f with {Riders = f.Riders.Select(r => r with {TrafficEnabled = false}).ToArray(),
                DurationSeconds = 7.6}, 4.1, 1),
            Pair("I-near-exit", 3, 20, reference: 115),
            Pair("J-existing-response", 4, 20, target: 2, outward: -1)
        };
    }
    public static void Freeze(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "baseline.json");
        if (File.Exists(path)) throw new InvalidOperationException("Baseline is immutable; use a fresh directory.");
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var surface = TrackState.CreateDefault(frame.Track).Snapshot(); var p = new WorldParameters();
        var rows = new List<QualityEvidence>();
        foreach (var c in Cases())
        {
            var spec = c.Scenario.Riders.Single(r => r.Profile.Id == c.RiderId);
            var first = Execute(c.Scenario, Array.Empty<ControlCommand>(), c.RiderId);
            var observation = At(first.Samples, c.ObserveAt).Riders.Single(r => r.Id == c.RiderId);
            var activation = c.ObserveAt + RiderController.ReactionSeconds(spec);
            var window = first.Samples.FirstOrDefault(s => s.TimeSeconds > activation
                && !s.Riders.Single(v => v.Id == c.RiderId).Road.IsTurn)?.TimeSeconds ?? activation + 3;
            var horizons = new[] {1d, 2d, 3d, Math.Min(3, window - activation)};
            var movement = ContestScoring.MovementSeconds(spec, p, c.Defending);
            var baselineRate = ContestScoring.LegacyRate(observation, spec, p, frame, surface, observation.TargetOffsetMeters, movement);
            var variants = new[] {("current", observation.TargetOffsetMeters), ("hold-position", (float)observation.Road.OffsetMeters),
                ("inside", (float)observation.Road.OffsetMeters - 1), ("outside", (float)observation.Road.OffsetMeters + 1)};
            var reference = Execute(c.Scenario, new[] {new ControlCommand(c.RiderId, activation, observation.TargetOffsetMeters, 1)}, c.RiderId);
            var trials = new List<QualityTrial>();
            foreach (var (name, target) in variants)
            {
                var available = target is >= .5f and <= 9.5f;
                if (!available) {trials.Add(new(name, target, false, Array.Empty<double>(), Array.Empty<double>(), "", true, 0, 0, 0, 0)); continue;}
                var run = name == "current" ? reference : Execute(c.Scenario,
                    new[] {new ControlCommand(c.RiderId, activation, target, 1)}, c.RiderId);
                var gains = horizons.Select(t => Progress(run.Samples, activation + t, c.RiderId)
                    - Progress(reference.Samples, activation + t, c.RiderId)).ToArray();
                var legacy = ContestScoring.LegacyRate(observation, spec, p, frame, surface, target, movement) - baselineRate;
                var byTime = reference.Samples.ToDictionary(s => Math.Round(s.TimeSeconds, 9));
                var prefix = run.Samples.Where(s => s.TimeSeconds <= activation + 1e-10)
                    .All(s => byTime.TryGetValue(Math.Round(s.TimeSeconds, 9), out var b) && s.Riders.SequenceEqual(b.Riders));
                trials.Add(new(name, target, true, gains, horizons.Select(t => legacy * t).ToArray(), run.Result.Fingerprint,
                    prefix, run.PeakLateral, run.MinimumSpeed, run.TangentialImpulse, run.Result.Contacts.Count(x => x.Kind == "begin")));
            }
            rows.Add(new(c.Name, spec, observation, activation, horizons, movement, trials.ToArray()));
        }
        File.WriteAllText(path, JsonSerializer.Serialize(rows, Audit.Json));
        File.WriteAllText(Path.Combine(directory, "cases.json"), JsonSerializer.Serialize(Cases(), Audit.Json));
        Console.WriteLine($"Frozen {rows.Count} cases in {Path.GetFullPath(path)}; prefix exact: {rows.All(r => r.Trials.All(t => t.PrefixExact))}");
    }
    private sealed record Execution(RunResult Result, List<WorldSnapshot> Samples, double PeakLateral, double MinimumSpeed, double TangentialImpulse);
    private static Execution Execute(Scenario scenario, ControlCommand[] controls, int rider)
    {
        var forces = new List<ForceObservation>();
        using var world = new WorldRunner(scenario.Riders, controls: controls,
            diagnostics: new() {Force = f => {if (f.Rider.Id == rider) forces.Add(f);}});
        var samples = new List<WorldSnapshot> {world.Snapshot};
        var result = world.Run(scenario.Name, .01, scenario.DurationSeconds, acceptedObserver: samples.Add, stopOnCrash: true);
        var from = controls.Length == 0 ? 0 : controls[0].AtSeconds;
        var relevant = forces.Where(f => f.TimeSeconds >= from && f.TimeSeconds <= from + 3).ToArray();
        double impulse = 0;
        for (var i = 1; i < relevant.Length; i++) impulse += relevant[i - 1].TangentialNewtons * (relevant[i].TimeSeconds - relevant[i - 1].TimeSeconds);
        return new(result, samples, relevant.Where(f => f.GripBudgetNewtons is > 0)
            .Select(f => f.DesiredInwardNewtons / f.GripBudgetNewtons!.Value).DefaultIfEmpty().Max(),
            relevant.Select(f => (double)f.Rider.Speed).DefaultIfEmpty().Min(), impulse);
    }
    public static WorldSnapshot At(IReadOnlyList<WorldSnapshot> samples, double time) => samples.Last(s => s.TimeSeconds <= time + 1e-9);
    private static double Progress(IReadOnlyList<WorldSnapshot> samples, double time, int rider)
    {
        if (time > samples[^1].TimeSeconds + 1e-9) throw new InvalidOperationException("Run stopped before diagnostic horizon.");
        var before = At(samples, time); var after = samples.First(s => s.TimeSeconds >= time - 1e-9);
        var a = before.Riders.Single(r => r.Id == rider).TotalReferenceMeters;
        var b = after.Riders.Single(r => r.Id == rider).TotalReferenceMeters;
        return after.TimeSeconds <= before.TimeSeconds + 1e-12 ? a : a + (b - a) * (time - before.TimeSeconds) / (after.TimeSeconds - before.TimeSeconds);
    }
}
