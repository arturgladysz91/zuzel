using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using CoreSim.Setup;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using RacingTrajectoryIntent = CoreSim.Decisions.TrajectoryIntent;

namespace CoreSim.Analysis;

public sealed record ContestedRiderInput(int Id, float Lateral, float Progress, float Speed,
    RacingTrajectoryIntent Intent, int Attack = 70, int Defense = 70, int Technique = 70,
    float Combativeness = .5f, float Condition = 1f, int Strength = 50, float MassKg = 70f, int PairRiding = 50);
public sealed record ContestedScenario(string Name, int SegmentIndex, IReadOnlyList<ContestedRiderInput> Riders,
    float OuterGrip = 1f, bool Standing = false);
public sealed record ContestedScenarioEvidence(string Name, IReadOnlyList<ContestedRiderInput> Inputs, IReadOnlyList<RiderStateChange> Changes,
    IReadOnlyList<RiderStepDiagnostics> Physics, InteractionResolution Interaction);
public sealed record ContestedHeatEvidence(string Scenario, int Seed, string Weather, string Archetype,
    IReadOnlyList<RiderHeatResult> Classification, int Episodes, int FirstBendEpisodes, int OrdinaryEpisodes,
    double AverageClusterSize, int PreContactResolved, int Unresolved, int LegacyFallbackInvocations,
    int ResponseChanges, double MaximumActiveDurationSeconds, int MaximumEpisodesPerPair,
    int Overtakes, int OrderSnapshots, InteractionWork Work, IReadOnlyDictionary<string, int> Contexts,
    IReadOnlyDictionary<string, int> Responses, int MaximumResponseChangesPerEpisode, int MaximumFallbacksPerEpisode,
    int ResponseOscillations, int UnexplainedCommitmentChanges, ContestedPhaseStatistics FirstBend,
    ContestedPhaseStatistics OrdinaryRacing, IReadOnlyList<string> StormWarnings)
{
    public IReadOnlyList<ContestedEpisodeHistory> LongestEpisodes { get; init; } = Array.Empty<ContestedEpisodeHistory>();
}
public sealed record ContestedEpisodeHistory(long EpisodeId, IReadOnlyList<int> RiderSet, double StartTimeSeconds,
    double EndTimeSeconds, IReadOnlyList<InteractionContext> ContextsSeen, double MinimumObservedSeparationMeters,
    double MaximumObservedSeparationMeters, int SeparationObservations, int ResponseChanges,
    bool ContinuouslyWithinCompetitiveReach, bool CompetitiveReachCoverageCertified, bool MergedOtherRiders);
public sealed record ContestedPhaseStatistics(int Episodes, int ResolvedObservations, int UnresolvedObservations,
    int LegacyFallbackInvocations, int ResponseSelections, int JointCombinations, int ResponsePasses);

/// <summary>Offline evidence through the production engine. No alternate motion or collision solver.</summary>
public static class ContestedSpaceResponseEvidence
{
    public static IReadOnlyList<ContestedScenario> Scenarios() => new ContestedScenario[]
    {
        new("A-entry-close", 1, new[] { R(1, 2, .125f, 22, new(2,2,2)), R(2, 1.1f, 0, 28, new(1,1,1)) }),
        new("B-close-too-late", 1, new[] { R(1, 2, .125f, 21, new(0,0,0)), R(2, 1.6f, .125f, 21, new(1,1,1)) }),
        new("C-cutback-clean", 3, new[] { R(1, 1, .0625f, 22, new(1,1,3)), R(2, 2, 0, 23, new(2,2,1)) }),
        new("C-cutback-poor", 3, new[] { R(1, 1, .0625f, 22, new(1,1,3)), R(2, 2, 0, 23, new(2,2,1)) }, .55f),
        new("D-inside-overlap", 2, new[] { R(1, 1, .125f, 21, new(1,1,1)), R(2, 1.4f, .125f, 21, new(1,1,1)) }),
        new("E-outside-useful", 2, new[] { R(1, 1, .125f, 20, new(1,1,1)), R(2, 2, .125f, 22, new(2,2,2)) }),
        new("E-outside-poor", 2, new[] { R(1, 1, .125f, 20, new(1,1,1)), R(2, 2, .125f, 22, new(2,2,2)) }, .45f),
        new("F-exit-cross", 3, new[] { R(1, 1, .0625f, 22, new(1,1,3)), R(2, 2, .0625f, 22, new(2,2,0)) }),
        new("G-four-first-bend", 1, new[] { R(1, .8f, 0, 21, new(2,2,2)), R(2, 1.1f, .0625f, 21, new(2,2,2)),
            R(3, 1.4f, .125f, 21, new(2,2,2)), R(4, 1.7f, .1875f, 21, new(2,2,2)) }, Standing:true),
        new("H-three-squeeze", 4, new[] { R(1, 1.75f, 0, 22, new(2,2,2)), R(2, 2, 0, 22, new(2,2,2)),
            R(3, 2.25f, 0, 22, new(2,2,2)) }),
        new("K-far-apart", 4, new[] { R(1, .8f, .75f, 22, new(1,1,1)), R(2, 3.2f, 0, 22, new(3,3,3)) }),
        new("edge-trapped", 4, new[] { R(1, 3.2f, 0, 22, new(4,4,4)), R(2, 4, 0, 22, new(4,4,4)) }),
        new("imminent-overlap", 2, new[] { R(1, 2, 0, 21, new(2,2,2)), R(2, 2.125f, 0, 21, new(2,2,2), combat:1) }),
    };
    private static ContestedRiderInput R(int id, float lateral, float progress, float speed, RacingTrajectoryIntent intent,
        float combat = .5f) => new(id, lateral, progress, speed, intent, Combativeness:combat);
    public static Track CreateTrack(bool standing) => new(Track.CreateStandingStartExample().Segments.Select((s,i)
        => new TrackSegment(s.Id,s.Type,i is 0 or 8 ? 30 : s.StraightLengthMetersOverride,standing && i == 0)).ToArray(),
        new TrackGeometry(60, 24, 14, 14, MathF.PI / 3));
    public static RiderProfile Profile(ContestedRiderInput input) => RiderProfile.CreateCanonical(input.Id, $"Traffic {input.Id}",
        new(new(50,50,input.Technique,50,input.Attack,input.Defense,input.PairRiding,input.Strength), new(input.MassKg),
            new(input.Combativeness, PreferredLine.Neutral, .5f)),
        new RiderSkills(50, 70, 70, 50, 50, 70), RiderStyle.Balanced);
    public static SimulationSnapshot Snapshot(ContestedScenario scenario, bool reverse = false)
    {
        var track = CreateTrack(scenario.Standing);
        var surface = new TrackState(track.Segments.Count, 5, (_, lane) => new(1f + (scenario.OuterGrip - 1f) * lane / 4, 0, 0));
        var riders = scenario.Riders.Select(i => new RiderSnapshot(i.Id, Profile(i),
            RiderPosition.Create(1, scenario.SegmentIndex, i.Progress, track.Segments.Count), -1,
            (int)MathF.Round(i.Lateral), i.Lateral, i.Speed, 0, RiderRaceStatus.Racing, 0,
            BikeSetup.Neutral, .5f, .5f) { Condition = i.Condition });
        return new(new(57,scenario.SegmentIndex,0,scenario.SegmentIndex,7,4), track, surface.Snapshot(),
            reverse ? riders.Reverse() : riders);
    }
    public static ResolvedSimulationStep Resolve(ContestedScenario scenario, bool enabled = true, bool reverse = false,
        ContestedSpaceParameters? parameters = null, InteractionEpisodeTracker? tracker = null,
        InteractionDiagnosticsLevel diagnostics = InteractionDiagnosticsLevel.FullAudit)
    {
        var snapshot = Snapshot(scenario, reverse);
        var intents = scenario.Riders.Select(i => new RiderIntent(i.Id,
            new RiderDecision(i.Intent.TargetFor(snapshot.Segment.Type)) { Trajectory = i.Intent })).ToArray();
        var engine = new SimulationEngine(new FixedDecision());
        return engine.Resolve(snapshot, reverse ? intents.Reverse().ToArray() : intents,
            new() { EnableContestedSpaceResponses = enabled, IncidentFrequency = 0,
                InteractionDiagnostics = diagnostics, ContestedSpaceParameters = parameters ?? new() }, tracker);
    }
    public static string DeterministicJson(bool includeHeats = true, Action<string>? progress = null)
    {
        ContestedHeatEvidence Run(BehaviorScenario scenario, int seed, WeatherState weather, string archetype = "legacy-fallback")
        {
            var result = Heat(scenario,seed,weather,archetype);
            progress?.Invoke($"{scenario.Id} seed={seed} weather={weather.Condition} archetype={archetype} episodes={result.Episodes}");
            return result;
        }
        var scenarios = Scenarios().Select(s => Capture(s)).ToArray();
        var sensitivity = new[] { .15, .25, .35 }.SelectMany(clearance => new[] { .15, .30, .45 }
            .Select(window => new { ClearanceMeters = clearance, ClusterWindowSeconds = window,
                Evidence = Capture(Scenarios().Single(s => s.Name == "G-four-first-bend"),
                    new() { PressureClearanceMeters = clearance, ClusterTimeWindowSeconds = window }) })).ToArray();
        var heats = includeHeats ? FourRiderBehaviorSuite.CreateScenarios().SelectMany(s => new[] { 7, 19 }
            .SelectMany(seed => new[] { WeatherState.Dry, WeatherState.LightRain }.Select(weather => Run(s, seed, weather)))).ToArray()
            : Array.Empty<ContestedHeatEvidence>();
        var archetypes = includeHeats ? FourRiderBehaviorSuite.CreateScenarios().Where(s => s.Id is "B" or "I")
            .SelectMany(s => new[] { "technical-attacker", "aggressive-mediocre", "strong-defender", "cautious-defender" }
                .SelectMany(archetype => new[] { 7,19 }.SelectMany(seed => new[] { WeatherState.Dry, WeatherState.LightRain }
                    .Select(weather => Run(s,seed,weather,archetype))))).ToArray() : Array.Empty<ContestedHeatEvidence>();
        var sweepScenario = Scenarios().Single(s => s.Name == "G-four-first-bend");
        var margins = new[] {20,50,80}.SelectMany(technique => new[] {1f,.7f,.4f}.Select(condition =>
            new { Technique = technique, Condition = condition,
                MarginMeters = InteractionGeometryModel.ExecutionMarginMeters(Snapshot(sweepScenario with
                { Riders = sweepScenario.Riders.Select(r => r with { Technique = technique, Condition = condition }).ToArray() }).Riders[0], new()),
                Evidence = Capture(sweepScenario with { Riders = sweepScenario.Riders.Select(r => r with
                    { Technique = technique, Condition = condition }).ToArray() }) })).ToArray();
        return JsonSerializer.Serialize(new { Schema = "56B-v1", Base = "a1609e485131f553619c386a051a158796c9e2dd",
            Status = "PROVISIONAL synthetic behavior; not real-world calibrated", Scenarios = scenarios,
            Sensitivity = sensitivity, TechniqueCondition = margins, Heats = heats, Archetypes = archetypes }, new JsonSerializerOptions
            { WriteIndented = true, Converters = { new JsonStringEnumConverter(), new PresentationDouble() } }) + "\n";
    }
    public static ContestedScenarioEvidence Capture(ContestedScenario scenario, ContestedSpaceParameters? parameters = null)
    {
        var resolved = Resolve(scenario, parameters:parameters);
        return new(scenario.Name, scenario.Riders, resolved.Changes, resolved.Diagnostics, resolved.Interaction!);
    }
    public static ContestedHeatEvidence Heat(BehaviorScenario scenario, int seed, WeatherState weather, string archetype = "legacy-fallback")
    {
        var observer = new Collector();
        var result = new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track, scenario.CreateSurface(),
            scenario.Riders.Select(r => CanonicalState(r, scenario.Track, archetype)).ToList(),
            new() { Seed = seed, Weather = weather, EnableContestedSpaceResponses = true, InteractionDiagnostics = InteractionDiagnosticsLevel.FullAudit }, 57, observer);
        var episodes = observer.Episodes.GroupBy(e => e.EpisodeId).Select(g => g.Last()).ToArray();
        var reachedMechanical = observer.Episodes.Where(e => e.LegacyFallbackUsed).Select(e => e.EpisodeId).ToHashSet();
        var pairCounts = new Dictionary<string, int>();
        foreach (var episode in episodes)
        for (var a = 0; a < episode.RiderIds.Count; a++) for (var b = a + 1; b < episode.RiderIds.Count; b++)
        {
            var key = $"{episode.RiderIds[a]}/{episode.RiderIds[b]}";
            pairCounts[key] = pairCounts.GetValueOrDefault(key) + 1;
        }
        var maximumChanges = episodes.Select(e => e.ResponseChanges).DefaultIfEmpty(0).Max();
        var maximumFallbacks = observer.Episodes.GroupBy(e => e.EpisodeId).Select(g => g.Count(e => e.LegacyFallbackUsed)).DefaultIfEmpty(0).Max();
        var oscillations = 0; var unexplained = 0;
        foreach (var group in observer.Episodes.GroupBy(e => e.EpisodeId))
        foreach (var id in group.SelectMany(e => e.RiderIds).Distinct())
        {
            InteractionEpisodeDiagnostic? previous = null;
            InteractionAlternative? priorChoice = null, beforePrior = null;
            foreach (var row in group)
            {
                var choice = row.SelectedResponses.SingleOrDefault(a => a.RiderId == id);
                if (choice is null) continue;
                if (previous?.Context == row.Context && priorChoice is not null && choice.Response != priorChoice.Response)
                {
                    if (beforePrior?.Response == choice.Response) oscillations++;
                    if (!row.Pass1ActualMechanicalContact && !row.Geometry.Any(g => g.Space.HasConflict && g.TimeToConflictSeconds <= new ContestedSpaceParameters().EmergencyTimeSeconds)
                        && row.Candidates.Any(c => c.Feasible && c.Responses.All(a => previous.SelectedResponses.Any(old => old.RiderId == a.RiderId
                            && a.Response == old.Response && a.Intent == old.Intent
                            && a.DriveControl == old.DriveControl && a.HoldLateralPosition == old.HoldLateralPosition)))) unexplained++;
                }
                beforePrior = priorChoice; priorChoice = choice; previous = row;
            }
        }
        ContestedPhaseStatistics Phase(bool first)
        {
            var rows = observer.Episodes.Where(e => (e.Context == InteractionContext.FirstBendCluster) == first).ToArray();
            return new(rows.Select(e => e.EpisodeId).Distinct().Count(),rows.Count(e => e.ResolvedWithoutMechanicalContact),
                rows.Count(e => !e.ResolvedWithoutMechanicalContact),rows.Count(e => e.LegacyFallbackUsed),
                rows.Sum(e => e.SelectedResponses.Count),rows.Sum(e => e.Candidates.Count),rows.Sum(e => e.PassCount));
        }
        var warnings = new List<string>();
        if (pairCounts.Values.DefaultIfEmpty(0).Max() >= 12) warnings.Add("Repeated episodes for the same pair");
        if (episodes.Any(e => e.ActiveDurationSeconds >= 20)) warnings.Add("Prolonged active episode: inspect sustained proximity");
        if (maximumChanges >= 12) warnings.Add("Many response changes inside one episode");
        if (maximumFallbacks > 1) warnings.Add("Repeated legacy fallback inside one episode");
        if (unexplained > 0) warnings.Add("Feasible commitment changed without context/emergency justification");
        return new(scenario.Id, seed, weather.Condition.ToString(), archetype, result.Classification, episodes.Length,
            observer.Episodes.Where(e => e.Context == InteractionContext.FirstBendCluster).Select(e => e.EpisodeId).Distinct().Count(),
            episodes.Count(e => e.Context != InteractionContext.FirstBendCluster),
            episodes.Select(e => (double)e.RiderIds.Count).DefaultIfEmpty(0).Average(),
            episodes.Count(e => e.ResolvedWithoutMechanicalContact && !reachedMechanical.Contains(e.EpisodeId)),
            episodes.Count(e => !e.ResolvedWithoutMechanicalContact || reachedMechanical.Contains(e.EpisodeId)),
            observer.Episodes.Count(e => e.LegacyFallbackUsed), episodes.Sum(e => e.ResponseChanges),
            episodes.Select(e => e.ActiveDurationSeconds).DefaultIfEmpty(0).Max(), pairCounts.Values.DefaultIfEmpty(0).Max(),
            result.Log.Overtakes.Count, result.Log.OrderSnapshots.Count,
            SumWork(observer.Work),
            episodes.GroupBy(e => e.Context.ToString()).OrderBy(g => g.Key).ToDictionary(g => g.Key,g => g.Count()),
            observer.Episodes.SelectMany(e => e.SelectedResponses).GroupBy(r => r.Response.ToString()).OrderBy(g => g.Key)
                .ToDictionary(g => g.Key,g => g.Count()), maximumChanges, maximumFallbacks, oscillations, unexplained,
            Phase(true),Phase(false),warnings)
        {
            LongestEpisodes = observer.Episodes.GroupBy(e => e.EpisodeId).Select(g =>
            {
                var last = g.Last();
                return new ContestedEpisodeHistory(g.Key, last.RiderIds, last.StartTimeSeconds,
                    last.EndTimeSeconds ?? last.StartTimeSeconds + last.ActiveDurationSeconds,
                    g.Select(e => e.Context).Distinct().ToArray(), g.Min(e => e.FinalMinimumSeparationMeters),
                    g.Max(e => e.FinalMinimumSeparationMeters), g.Count(), last.ResponseChanges,
                    g.All(e => e.WithinCompetitiveReach), g.All(e => e.CompetitiveReachCoverageCertified), g.Any(e => e.MergedOtherRiders));
            }).OrderByDescending(e => e.EndTimeSeconds-e.StartTimeSeconds).ThenBy(e => e.EpisodeId).Take(10).ToArray(),
        };
    }
    public static string BenchmarkJson()
    {
        var samples = new List<object>();
        foreach (var scenario in new[] { Scenarios().Single(s => s.Name == "K-far-apart"), Scenarios().Single(s => s.Name == "H-three-squeeze") })
        foreach (var enabled in new[] { false, true })
        {
            Resolve(scenario, enabled, diagnostics:InteractionDiagnosticsLevel.Summary);
            var times = new List<double>(); var allocated = new List<long>();
            for (var repeat = 0; repeat < 5; repeat++)
            {
                var before = GC.GetTotalAllocatedBytes(true); var watch = Stopwatch.StartNew();
                Resolve(scenario, enabled, diagnostics:InteractionDiagnosticsLevel.Summary); watch.Stop(); times.Add(watch.Elapsed.TotalMilliseconds);
                allocated.Add(GC.GetTotalAllocatedBytes(true) - before);
            }
            samples.Add(new { scenario.Name, Enabled = enabled, MedianMilliseconds = times.Order().ElementAt(2),
                MedianAllocatedBytes = allocated.Order().ElementAt(2) });
        }
        var fullHeats = new List<object>();
        var fixture = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "I");
        foreach (var enabled in new[] { false, true })
        {
            (HeatResult Result, Collector Observer) Run()
            {
                var observer = new Collector();
                var result = new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(fixture.Track, fixture.CreateSurface(),
                    fixture.Riders.Select(r => r.Create(fixture.Track)).ToList(),
                    new() { Seed = 7, EnableContestedSpaceResponses = enabled, InteractionDiagnostics = InteractionDiagnosticsLevel.Summary },57,observer);
                return (result,observer);
            }
            Run();
            var measurements = new List<(double Wall, double Cpu, long Bytes, Collector Observer)>();
            using var process = Process.GetCurrentProcess();
            for (var repeat = 0; repeat < 3; repeat++)
            {
                var before = GC.GetTotalAllocatedBytes(true); var cpu = process.TotalProcessorTime;
                var watch = Stopwatch.StartNew(); var result = Run(); watch.Stop();
                measurements.Add((watch.Elapsed.TotalMilliseconds,(process.TotalProcessorTime-cpu).TotalMilliseconds,
                    GC.GetTotalAllocatedBytes(true)-before,result.Observer));
            }
            var median = measurements.OrderBy(m => m.Wall).ElementAt(1);
            fullHeats.Add(new { Scenario = fixture.Id, Enabled = enabled, Riders = 4, Laps = 4,
                TotalWallMilliseconds = median.Wall, TotalCpuMilliseconds = measurements.Select(m=>m.Cpu).Order().ElementAt(1),
                TotalAllocatedBytes = measurements.Select(m=>m.Bytes).Order().ElementAt(1),
                InteractionEpisodes = median.Observer.Episodes.Select(e=>e.EpisodeId).Distinct().Count(),
                Work = enabled ? SumWork(median.Observer.Work) : new InteractionWork(0,median.Observer.ProductionSteps,0,0,0),
                DescriptiveCpuProjection100HeatsSeconds = measurements.Select(m=>m.Cpu).Order().ElementAt(1)*100/1000,
                DescriptiveCpuProjection1000HeatsSeconds = measurements.Select(m=>m.Cpu).Order().ElementAt(1)*1000/1000 });
        }
        return JsonSerializer.Serialize(new { Schema = "56B-performance-v2", ReviewedHead = "3684efd743421812b75794c730f1db1b90f58b2d",
            BeforeReviewFixes = new { DenseThreeRiderMedianMilliseconds = 244.5898, DenseThreeRiderMedianAllocatedBytes = 82216464 },
            Protocol = "Release; current process; one warmup, five resolutions or three full heats; medians; Summary diagnostics; CPU projections are descriptive, not CI guarantees",
            Machine = new { Environment.OSVersion, Environment.ProcessorCount, Runtime = Environment.Version.ToString() },
            Resolutions = samples, FullHeats = fullHeats }, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }
    private static InteractionWork SumWork(IEnumerable<InteractionWork> work)
    {
        var rows = work.ToArray();
        return new(rows.Sum(w=>w.JointCombinations), rows.Sum(w=>w.ProductionResolutions), rows.Sum(w=>w.NarrowPhaseEvaluations),
            rows.Sum(w=>w.ResponsePasses), rows.Sum(w=>w.Clusters))
        {
            UniqueRiderAlternativeProjections = rows.Sum(w=>w.UniqueRiderAlternativeProjections),
            PairAlternativeChecks = rows.Sum(w=>w.PairAlternativeChecks), ActualProductionVerifications = rows.Sum(w=>w.ActualProductionVerifications),
            SafetyPasses = rows.Sum(w=>w.SafetyPasses), LegacyFallbackAttempts = rows.Sum(w=>w.LegacyFallbackAttempts),
        };
    }
    private sealed class FixedDecision : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }
    private sealed class PresentationDouble : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options) => reader.GetDouble();
        public override void Write(Utf8JsonWriter writer,double value,JsonSerializerOptions options)
            => writer.WriteNumberValue(Math.Round(value,10,MidpointRounding.ToEven));
    }
    private static RiderState CanonicalState(BehaviorRiderInput input, Track track, string archetype)
    {
        var original = input.Create(track);
        if (archetype == "legacy-fallback") return original;
        var (attack, defense, technique, combat) = archetype switch
        {
            "technical-attacker" => (90,60,90,.5f),
            "aggressive-mediocre" => (50,50,50,1f),
            "strong-defender" => (50,90,90,.5f),
            "cautious-defender" => (50,90,70,.1f),
            _ => throw new ArgumentException("Unknown archetype", nameof(archetype)),
        };
        var profile = RiderProfile.CreateCanonical(original.RiderId, original.Profile.Name,
            new(new(50,50,technique,50,attack,defense,50,50), new(70), new(combat,PreferredLine.Neutral,.5f)),
            original.Profile.Skills, original.Profile.Style);
        var rider = new RiderState(profile, original.Lane, original.Morale, original.ManagerTrust)
            { Speed = original.Speed, ElapsedTimeSeconds = original.ElapsedTimeSeconds, LateralPosition = original.LateralPosition };
        rider.RestorePosition(original.PositionForTrack(track.Segments.Count));
        return rider;
    }
    private sealed class Collector : ISimulationStepObserver
    {
        internal readonly List<InteractionEpisodeDiagnostic> Episodes = new();
        internal readonly List<InteractionWork> Work = new();
        internal int ProductionSteps;
        public void OnStepResolved(ResolvedSimulationStep step)
        {
            ProductionSteps++;
            if (step.Interaction is { } interaction) { Episodes.AddRange(interaction.Episodes); Work.Add(interaction.Work); }
        }
    }
}
