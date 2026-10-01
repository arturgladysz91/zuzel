using System.Globalization;
using CoreSim.Decisions;
using CoreSim.Race;

namespace CoreSim.Analysis;

public sealed record BehaviorRiderInput(int Id, int Lane, float SpeedMetersPerSecond,
    float SegmentProgress, float ArrivalOffsetSeconds, RiderSkills Skills, RiderStyle Style)
{
    public RiderState Create(Track track)
    {
        var rider = new RiderState(new RiderProfile(Id, $"Audit rider {Id.ToString(CultureInfo.InvariantCulture)}",
            Skills, Style), Lane) { Speed = SpeedMetersPerSecond, ElapsedTimeSeconds = ArrivalOffsetSeconds };
        rider.RestorePosition(RiderPosition.Create(1, 0, SegmentProgress, track.Segments.Count,
            LaneModel.SegmentLengthMeters(track.Segments[0], Lane, track.Geometry) * SegmentProgress));
        return rider;
    }
}

public sealed record BehaviorScenario(string Id, string Description, Track Track,
    IReadOnlyList<BehaviorRiderInput> Riders, IReadOnlyList<TrackSurfaceState> LaneSurfaces)
{
    public TrackState CreateSurface() => new(Track.Segments.Count, LaneModel.LanesCount,
        (_, lane) => LaneSurfaces[lane]);
}

public sealed record BehaviorChoice(int Step, int RiderId, int TargetLane, int NoTrafficTargetLane);
public sealed record BehaviorPass(int Step, int Lap, int SegmentIndex, int SegmentId,
    int RiderId, int PassedRiderId, double ObservedBoundaryProgress, bool PreviouslyTied);
public sealed record BehaviorContact(int Step, int Lap, int SegmentIndex, SegmentType SegmentType,
    int RiderId, int OtherRiderId, SimulationEventType Type);
public sealed record BehaviorStep(int Step, int Lap, int SegmentIndex, SegmentType SegmentType,
    int RiderId, double StartProgress, double EndProgress, float StartTimeSeconds, float EndTimeSeconds,
    float BoundaryArrivalGapSeconds, int BeforeLane, int TargetLane, int PlannedLane, int ResolvedLane,
    float EntryLateralPosition, float ExitLateralPosition, float EntrySpeedMetersPerSecond,
    float SegmentConstraintSpeedMetersPerSecond, float TraversalExitSpeedMetersPerSecond,
    float FinalSpeedMetersPerSecond, float PathDistanceMeters,
    TrackSurfaceState EntrySurface, RiderRaceStatus Status, SegmentOutcome Outcome,
    float? CornerStartProgress, float? CornerEndProgress, float? MinimumCornerSpeedMetersPerSecond,
    float? ActualSampledApexSpeedMetersPerSecond, float? CornerEnvelopeApexSpeedMetersPerSecond,
    float? StartReactionSeconds);
public sealed record BehaviorRun(string ScenarioId, int Seed, IReadOnlyList<int> InitialOrder,
    IReadOnlyList<RiderHeatResult> Classification, IReadOnlyList<BehaviorPass> BoundaryPasses,
    IReadOnlyList<CoreSim.Logging.OvertakeEvent> ProductionOvertakes,
    IReadOnlyList<BehaviorContact> Contacts, IReadOnlyList<BehaviorChoice> Choices,
    IReadOnlyList<BehaviorStep> Steps, int FirstBoundaryEventMismatchCount)
{
    public IReadOnlyList<int> FinalOrder => Classification.OrderBy(item => item.Position).Select(item => item.RiderId).ToArray();
    public int Crashes => Classification.Count(item => item.Crashed);
    public int LostRhythm => Contacts.Count(item => item.Type == SimulationEventType.ContactLostRhythm);
}
public sealed record BehaviorSkillPair(string Skill, int Seed, BehaviorScenario LowInput, BehaviorScenario HighInput,
    BehaviorRun Low, BehaviorRun High);
public sealed record BehaviorRouteProbe(string ScenarioId, int Lane, float ProjectedRouteTimeSeconds,
    float ActualBendAndStraightTimeSeconds, float BendDistanceMeters, float BendEntrySpeedMetersPerSecond,
    float? ActualApexSpeedMetersPerSecond, float BendExitSpeedMetersPerSecond, float StraightExitSpeedMetersPerSecond);
public sealed record BehaviorWearHeat(int Heat, float MeanGripBefore, float MeanRutsBefore,
    float MeanGripAfter, float MeanRutsAfter, int SurfaceCellChanges,
    int DecisionsDifferentFromFresh, IReadOnlyList<int> TargetHistogram, IReadOnlyList<int> FreshTargetHistogram);
public sealed record BehaviorDiagnosticControl(string Id, string Intent, BehaviorScenario Input, BehaviorRun Run);
public sealed record BehaviorAuditResult(IReadOnlyList<BehaviorScenario> Scenarios,
    IReadOnlyList<BehaviorRun> Runs, BehaviorRun NonStochasticControl,
    IReadOnlyList<BehaviorSkillPair> SkillPairs, IReadOnlyList<BehaviorRouteProbe> RouteProbes,
    IReadOnlyList<BehaviorWearHeat> WearHeats, string FractionalProgressProbe,
    IReadOnlyList<BehaviorDiagnosticControl> DiagnosticControls);

/// <summary>Analysis-only observations of HeatSimulator. Never supplies alternate physics.</summary>
public static class FourRiderBehaviorSuite
{
    public const string BaselineSha = "a3165260183067bff9b6a0e9e94034fa1f361dcf";
    public const int HeatId = 6101;
    public const int SeedCount = 32;

    public static HeatSimulationOptions ProductionOptions(int seed) => new()
    {
        Seed = seed, Laps = 4, IncidentFrequency = 1f, EnableLogging = true,
        Weather = WeatherState.Dry,
        // All calibration-only adjustments intentionally retain their null defaults.
    };

    public static IReadOnlyList<BehaviorScenario> CreateScenarios()
    {
        var normal = new TrackGeometry(60f, 24f, 10f, 14f, MathF.PI / 3f);
        var rolling = StandardTrack(normal, false);
        var corner = StandardTrack(normal, true);
        var uniform = Gradient(1f, 1f, 0f, 0f);
        var neutral = new[] { Input(3, 3, 18f, 0f, 3f), Input(4, 4, 18f, 0f, 5f) };
        BehaviorRiderInput[] Pair(BehaviorRiderInput first, BehaviorRiderInput second)
            => new[] { first, second }.Concat(neutral).ToArray();
        var battle = Pair(Input(1, 3, 18f, .0625f), Input(2, 2, 19f));
        var varied = new[]
        {
            Input(1, 0, 18f, reading: 20f, control: 20f, outside: 0f),
            Input(2, 1, 18f, offset: .1f, reading: 80f, control: 80f, outside: 0f),
            Input(3, 3, 18f, offset: .2f, reading: 20f, control: 80f, outside: 1f),
            Input(4, 4, 18f, offset: .3f, reading: 80f, control: 20f, outside: 1f),
        };
        return new[]
        {
            new BehaviorScenario("A", "Faster catches slower; initial physical gap 15 m", rolling,
                Pair(Input(1, 1, 14f, .25f, speedSkill: 20f), Input(2, 1, 20f, speedSkill: 80f)), uniform),
            new BehaviorScenario("B", "Inside attack; inner space free", rolling, battle, uniform),
            new BehaviorScenario("C", "Outside pass; outer grip and stronger follower", rolling,
                Pair(Input(1, 0, 18f, .0625f), Input(2, 3, 22f, speedSkill: 80f, control: 80f, outside: 1f)),
                Gradient(.72f, 1f, .30f, 0f)),
            new BehaviorScenario("D", "Side-by-side corner entry", corner,
                Pair(Input(1, 1, 19f), Input(2, 3, 21f)), uniform),
            new BehaviorScenario("E", "Blocked preferred inner line", rolling,
                Pair(Input(1, 0, 18f, outside: 0f), Input(2, 1, 18f, offset: .10f, outside: 0f)),
                Gradient(1f, .65f, 0f, .4f)),
            new BehaviorScenario("F", "Worn inside / clean outside", rolling, varied, Gradient(.55f, 1f, .75f, 0f)),
            new BehaviorScenario("G", "Clean inside / poor outside", rolling, varied, Gradient(1f, .55f, 0f, .75f)),
            new BehaviorScenario("H", "Exit battle at logical corner progress 0.60", WholeBendTrack(),
                Pair(Input(1, 0, 18f, .60f), Input(2, 4, 22f, .60f)), uniform),
            new BehaviorScenario("I", "Four-rider standing start / first corner", Track.CreateStandingStartExample(),
                new[] { Input(1, 0, 0f, start: 35f), Input(2, 1, 0f, start: 50f),
                    Input(3, 3, 0f, start: 65f), Input(4, 4, 0f, start: 80f) }, uniform),
            new BehaviorScenario("J-tight", "Same B battle; tighter/narrower geometry",
                StandardTrack(new TrackGeometry(45f, 18f, 8f, 10f, MathF.PI / 3f), false), battle, uniform),
            new BehaviorScenario("J-wide", "Same B battle; faster/wider geometry",
                StandardTrack(new TrackGeometry(80f, 34f, 12f, 18f, MathF.PI / 3f), false), battle, uniform),
        };
    }

    public static BehaviorAuditResult RunSuite(Action<string>? progress = null)
    {
        var scenarios = CreateScenarios();
        var runs = new List<BehaviorRun>();
        foreach (var scenario in scenarios)
        {
            progress?.Invoke($"Production four-rider case {scenario.Id}: seeds 0..31");
            for (var seed = 0; seed < SeedCount; seed++) runs.Add(Run(scenario, seed));
        }
        var controls = new List<BehaviorSkillPair>();
        foreach (var skill in new[] { "Start", "Speed", "SlideControl", "TrackReading", "PairRiding", "Adaptability" })
        {
            progress?.Invoke($"Matched skill control: {skill}");
            var scenario = SkillScenario(skill, scenarios);
            var fixedIntent = skill is "SlideControl" or "PairRiding" or "Adaptability";
            for (var seed = 0; seed < SeedCount; seed++)
            {
                var low = WithSkill(scenario, skill, 20f);
                var high = WithSkill(scenario, skill, 80f);
                controls.Add(new BehaviorSkillPair(skill, seed, low, high,
                    Run(low, seed, fixedIntent ? new FixedDecision(skill == "Adaptability" ? 0 : 2) : null, incidentFrequency: 0f),
                    Run(high, seed, fixedIntent ? new FixedDecision(skill == "Adaptability" ? 0 : 2) : null, incidentFrequency: 0f)));
            }
        }
        progress?.Invoke("Fixed-route projection checks and repeated-heat wear controls");
        return new BehaviorAuditResult(scenarios, runs, RunNonStochasticControl(), controls,
            RouteProbes(scenarios), WearProbe(scenarios.Single(item => item.Id == "B")), FractionalProgressProbe(scenarios),
            DiagnosticControls(scenarios));
    }

    private static IReadOnlyList<BehaviorDiagnosticControl> DiagnosticControls(IReadOnlyList<BehaviorScenario> scenarios)
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight), new TrackSegment(1, SegmentType.Straight) },
            new TrackGeometry(60f, 24f, 10f, 14f, MathF.PI / 3f));
        var crossing = new BehaviorScenario("K-crossing", "Identical longitudinal profiles, opposite lateral intents", track,
            new[] { Input(1, 0, 18f), Input(2, 1, 18f), Input(3, 3, 18f, offset: 3f), Input(4, 4, 18f, offset: 5f) }, Gradient(1f, 1f, 0f, 0f));
        var entry = scenarios.Single(item => item.Id == "D");
        var start = scenarios.Single(item => item.Id == "I") with
        { Riders = scenarios.Single(item => item.Id == "I").Riders.Select(item => item with { Skills = RiderSkills.Balanced }).ToArray() };
        return new[]
        {
            new BehaviorDiagnosticControl("crossing", "R1 target 1; R2 target 0; others hold", crossing,
                Run(crossing, 0, new CrossingDecision(), incidentFrequency: 0f)),
            new BehaviorDiagnosticControl("entry-hold", "Solo R2 holds lane 3", entry,
                Run(entry, 0, new FixedDecision(3), incidentFrequency: 0f, onlyRiderId: 2)),
            new BehaviorDiagnosticControl("entry-inward", "Solo R2 target 0", entry,
                Run(entry, 0, new FixedDecision(0), incidentFrequency: 0f, onlyRiderId: 2)),
            new BehaviorDiagnosticControl("equal-start", "Equal skills/setup; all hold starting reference lanes", start,
                Run(start, 0, new FixedDecision(null), incidentFrequency: 0f)),
        };
    }

    private static string FractionalProgressProbe(IReadOnlyList<BehaviorScenario> scenarios)
    {
        var source = scenarios.Single(item => item.Id == "B");
        var rejected = source with { Riders = source.Riders.Select(item => item.Id == 1 ? item with { SegmentProgress = .05f } : item).ToArray() };
        try { Run(rejected, 0); return "No exception: production fractional-progress behavior changed; re-review required."; }
        catch (InvalidOperationException exception) { return exception.Message; }
    }

    public static BehaviorRun Run(BehaviorScenario scenario, int seed, IRiderDecisionModel? decision = null,
        bool reverseRiders = false, TrackState? surface = null, WeatherState? weather = null,
        float incidentFrequency = 1f, int? onlyRiderId = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var riders = scenario.Riders.Where(item => onlyRiderId is null || item.Id == onlyRiderId)
            .Select(item => item.Create(scenario.Track)).ToList();
        if (reverseRiders) riders.Reverse();
        var initial = Order(riders.Select(item => (item.RiderId, item.CanonicalProgress, item.ElapsedTimeSeconds))).ToArray();
        var recorder = new ChoiceRecorder(decision ?? new AdaptiveDecisionModel());
        var observer = new Observer(riders, scenario.Track);
        var options = ProductionOptions(seed) with
        { Weather = weather ?? WeatherState.Dry, IncidentFrequency = incidentFrequency };
        var heat = new HeatSimulator(recorder).SimulateHeat(scenario.Track, surface ?? scenario.CreateSurface(),
            riders, options, HeatId, observer);
        var firstPasses = observer.Passes.Where(item => item.Step == 0).Select(item => (item.RiderId, item.PassedRiderId)).ToHashSet();
        var firstEvents = heat.Log.Overtakes.Where(item => item.Lap == 1 && item.SegmentId == scenario.Track.Segments[0].Id)
            .Select(item => (item.RiderId, item.PassedRiderId)).ToHashSet();
        var mismatch = firstPasses.Except(firstEvents).Count() + firstEvents.Except(firstPasses).Count();
        return new BehaviorRun(scenario.Id, seed, initial, heat.Classification, observer.Passes,
            heat.Log.Overtakes.ToArray(), observer.Contacts, recorder.Choices, observer.Steps, mismatch);
    }

    public static BehaviorRun RunNonStochasticControl(int seed = 0)
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight), new TrackSegment(1, SegmentType.Straight) });
        var scenario = new BehaviorScenario("K-fixed-separated", "No stochastic outcome branches; fixed separated straight paths",
            track, new[] { Input(1, 0, 18f), Input(2, 1, 18f, offset: 2f),
                Input(3, 3, 18f, offset: 4f), Input(4, 4, 18f, offset: 6f) }, Gradient(1f, 1f, 0f, 0f));
        return Run(scenario, seed, new FixedDecision(null), incidentFrequency: 0f);
    }

    private static IEnumerable<int> Order(IEnumerable<(int Id, double Progress, float Time)> riders)
        => riders.OrderByDescending(item => item.Progress).ThenBy(item => item.Time).ThenBy(item => item.Id).Select(item => item.Id);

    private static BehaviorRiderInput Input(int id, int lane, float speed, float progress = 0f,
        float offset = 0f, float speedSkill = 50f, float reading = 50f, float control = 50f,
        float outside = .5f, float start = 50f)
        => new(id, lane, speed, progress, offset, new RiderSkills(start, speedSkill, control, reading, 50f, 50f),
            new RiderStyle(.5f, .5f, outside, .5f));

    private static TrackSurfaceState[] Gradient(float innerGrip, float outerGrip, float innerRuts, float outerRuts)
        => Enumerable.Range(0, 5).Select(lane => new TrackSurfaceState(
            innerGrip + (outerGrip - innerGrip) * lane / 4f,
            innerRuts + (outerRuts - innerRuts) * lane / 4f, .35f)).ToArray();

    private static Track StandardTrack(TrackGeometry geometry, bool startsInCorner)
    {
        var cornerFirst = new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit, SegmentType.Straight,
            SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit, SegmentType.Straight };
        var types = startsInCorner ? cornerFirst : new[] { SegmentType.Straight }.Concat(cornerFirst.Take(7)).ToArray();
        return new Track(types.Select((type, index) => new TrackSegment(index, type)).ToArray(), geometry);
    }

    private static Track WholeBendTrack() => new(new[]
    { new TrackSegment(0, SegmentType.TurnEntry), new TrackSegment(1, SegmentType.Straight),
        new TrackSegment(2, SegmentType.TurnEntry), new TrackSegment(3, SegmentType.Straight) },
        new TrackGeometry(60f, 24f, 10f, 14f, MathF.PI));

    private static BehaviorScenario SkillScenario(string skill, IReadOnlyList<BehaviorScenario> scenarios)
    {
        if (skill is "Start" or "Speed" or "TrackReading")
            return scenarios.Single(item => item.Id == (skill == "Start" ? "I" : skill == "Speed" ? "A" : "F"));
        var track = StandardTrack(new TrackGeometry(60f, 24f, 10f, 14f, MathF.PI / 3f), true);
        return new BehaviorScenario("K-" + skill, "Fixed-intent one-skill control", track,
            skill == "Adaptability"
                ? new[] { Input(1, 0, 18f, offset: 2f), Input(2, 4, 18f), Input(3, 2, 18f, offset: 4f), Input(4, 3, 18f, offset: 6f) }
                : Enumerable.Range(1, 4).Select(id => Input(id, 2, 18f)).ToArray(), Gradient(1f, 1f, 0f, 0f));
    }

    private static BehaviorScenario WithSkill(BehaviorScenario scenario, string skill, float value)
        => scenario with { Riders = scenario.Riders.Select(input => input.Id != 2 ? input : input with
        {
            Skills = new RiderSkills(skill == "Start" ? value : input.Skills.Start,
                skill == "Speed" ? value : input.Skills.Speed,
                skill == "SlideControl" ? value : input.Skills.SlideControl,
                skill == "TrackReading" ? value : input.Skills.TrackReading,
                skill == "PairRiding" ? value : input.Skills.PairRiding,
                skill == "Adaptability" ? value : input.Skills.Adaptability),
        }).ToArray() };

    private static IReadOnlyList<BehaviorRouteProbe> RouteProbes(IReadOnlyList<BehaviorScenario> scenarios)
    {
        var probes = new List<BehaviorRouteProbe>();
        foreach (var id in new[] { "B", "C", "H" })
        {
            var source = scenarios.Single(item => item.Id == id);
            // Begin at the actual bend entrance, before any lateral manoeuvre.
            var track = id == "H" ? source.Track : StandardTrack(source.Track.Geometry, true);
            var focal = source.Riders.Single(item => item.Id == 2);
            for (var lane = 0; lane <= 4; lane++)
            {
                var scenario = source with { Track = track, Riders = new[] { focal with { Lane = lane, SegmentProgress = 0f, ArrivalOffsetSeconds = 0f } } };
                var run = Run(scenario, 0, new FixedDecision(lane), weather: new WeatherState(WeatherCondition.Dry, 0f, 0f), incidentFrequency: 0f);
                var straightIndex = track.Segments.Select((segment, index) => (segment, index)).First(item => item.segment.Type == SegmentType.Straight).index;
                var bend = run.Steps.Where(item => item.Lap == 1 && item.SegmentIndex < straightIndex).ToArray();
                var straight = run.Steps.Single(item => item.Lap == 1 && item.SegmentIndex == straightIndex);
                var safe = SegmentPhysics.MaxSafeTurnSpeed(lane, track.Geometry, source.LaneSurfaces[lane], focal.Skills, focal.Create(track).ActiveSetup);
                // This is a diagnostic transcription of the production projection, not a solver.
                var projected = (LaneModel.TurnArcLengthMeters(lane, track.Geometry) * 3f + track.Geometry.StraightLengthMeters) / MathF.Max(safe, 1f);
                probes.Add(new BehaviorRouteProbe(id, lane, projected, straight.EndTimeSeconds,
                    bend.Sum(item => item.PathDistanceMeters), bend[0].EntrySpeedMetersPerSecond,
                    bend.Select(item => item.ActualSampledApexSpeedMetersPerSecond).FirstOrDefault(item => item is not null),
                    bend[^1].TraversalExitSpeedMetersPerSecond, straight.TraversalExitSpeedMetersPerSecond));
            }
        }
        return probes;
    }

    private static IReadOnlyList<BehaviorWearHeat> WearProbe(BehaviorScenario scenario)
    {
        const int heats = 6;
        var accumulated = new List<(int Heat, float GripBefore, float RutsBefore, float GripAfter, float RutsAfter,
            int Changes, int Different, int[] Targets, int[] Fresh)>();
        for (var seed = 0; seed < SeedCount; seed++)
        {
            var state = scenario.CreateSurface();
            for (var heat = 1; heat <= heats; heat++)
            {
                var before = Cells(state).ToArray();
                var weather = new WeatherState(WeatherCondition.Dry, 0f, 0f);
                var reused = Run(scenario, seed, surface: state, weather: weather);
                var fresh = Run(scenario, seed, weather: weather);
                var after = Cells(state).ToArray();
                var freshChoices = fresh.Choices.ToDictionary(item => (item.Step, item.RiderId));
                var different = reused.Choices.Count(item => freshChoices.TryGetValue((item.Step, item.RiderId), out var other) && item.TargetLane != other.TargetLane);
                accumulated.Add((heat, before.Average(item => item.Grip), before.Average(item => item.Ruts),
                    after.Average(item => item.Grip), after.Average(item => item.Ruts),
                    before.Zip(after).Count(item => item.First != item.Second), different,
                    Histogram(reused.Choices), Histogram(fresh.Choices)));
            }
        }
        return accumulated.GroupBy(item => item.Heat).OrderBy(item => item.Key).Select(group => new BehaviorWearHeat(group.Key,
            group.Average(item => item.GripBefore), group.Average(item => item.RutsBefore), group.Average(item => item.GripAfter), group.Average(item => item.RutsAfter),
            group.Sum(item => item.Changes), group.Sum(item => item.Different),
            Enumerable.Range(0, 5).Select(lane => group.Sum(item => item.Targets[lane])).ToArray(),
            Enumerable.Range(0, 5).Select(lane => group.Sum(item => item.Fresh[lane])).ToArray())).ToArray();
    }

    private static IEnumerable<TrackSurfaceState> Cells(TrackState state)
    {
        for (var segment = 0; segment < state.SegmentCount; segment++)
        for (var lane = 0; lane < state.LinesCount; lane++) yield return state.GetSurface(segment, lane);
    }

    private static int[] Histogram(IReadOnlyList<BehaviorChoice> choices)
        => Enumerable.Range(0, 5).Select(lane => choices.Count(item => item.TargetLane == lane)).ToArray();

    private sealed class FixedDecision(int? lane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(lane ?? rider.Lane, 0f);
        public RiderDecision Decide(RiderDecisionContext context) => new(lane ?? context.Rider.Lane, 0f);
    }

    private sealed class CrossingDecision : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.RiderId == 1 ? 1 : rider.RiderId == 2 ? 0 : rider.Lane, 0f);
        public RiderDecision Decide(RiderDecisionContext context)
            => new(context.Rider.RiderId == 1 ? 1 : context.Rider.RiderId == 2 ? 0 : context.Rider.Lane, 0f);
    }

    private sealed class ChoiceRecorder(IRiderDecisionModel inner) : IRiderDecisionModel
    {
        public List<BehaviorChoice> Choices { get; } = new();
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => inner.Decide(segment, rider);
        public RiderDecision Decide(RiderDecisionContext context)
        {
            var decision = inner.Decide(context);
            var solo = new SimulationSnapshot(context.Snapshot.Step, context.Snapshot.Track,
                context.TrackState, new[] { context.Rider });
            var noTraffic = inner.Decide(new RiderDecisionContext(solo, solo.Rider(context.Rider.RiderId)));
            Choices.Add(new BehaviorChoice(context.StepNumber, context.Rider.RiderId, decision.TargetLane, noTraffic.TargetLane));
            return decision;
        }
    }

    private sealed class Observer : ISimulationStepObserver
    {
        private readonly Track _track;
        private Dictionary<int, (double Progress, float Time)> _previous;
        private int[] _previousOrder;
        public List<BehaviorStep> Steps { get; } = new();
        public List<BehaviorPass> Passes { get; } = new();
        public List<BehaviorContact> Contacts { get; } = new();

        public Observer(IReadOnlyList<RiderState> riders, Track track)
        {
            _track = track;
            _previous = riders.ToDictionary(item => item.RiderId, item => (item.CanonicalProgress, item.ElapsedTimeSeconds));
            _previousOrder = Order(riders.Select(item => (item.RiderId, item.CanonicalProgress, item.ElapsedTimeSeconds))).ToArray();
        }

        public void OnStepResolved(ResolvedSimulationStep resolved)
        {
            var step = resolved.Snapshot.Step;
            var diagnostics = resolved.Diagnostics.ToDictionary(item => item.RiderId);
            var active = resolved.Changes.Where(item => item.Status is RiderRaceStatus.Racing or RiderRaceStatus.Finished).ToArray();
            var minimumTime = active.Length == 0 ? 0f : active.Min(item => item.ElapsedTimeSeconds);
            foreach (var change in resolved.Changes)
            {
                var entry = resolved.Snapshot.Rider(change.RiderId);
                var diagnostic = diagnostics[change.RiderId];
                var corner = diagnostic.ContinuousCornerProfile;
                var apex = corner?.Nodes.FirstOrDefault(node => MathF.Abs(node.CornerProgress - .5f) < 1e-5f)?.SpeedMetersPerSecond;
                var traversalExit = corner?.ExitSpeedMetersPerSecond
                    ?? diagnostic.StandingStartLaunchProfile?.ExitSpeedMetersPerSecond
                    ?? diagnostic.StraightProfile?.ExitSpeedMetersPerSecond ?? change.PhysicsSpeed;
                float? cornerEnd = diagnostic.CornerPhaseContext is { } phase
                    ? phase.CornerProgress + (phase.SegmentEndCornerProgress - phase.SegmentStartCornerProgress)
                        * (float)(change.Position.TotalSegmentProgress - entry.CanonicalProgress)
                    : null;
                Steps.Add(new BehaviorStep(step.StepNumber, step.LapIndex + 1, step.SegmentIndex, resolved.Snapshot.Segment.Type,
                    change.RiderId, entry.CanonicalProgress, change.Position.TotalSegmentProgress,
                    entry.ElapsedTimeSeconds, change.ElapsedTimeSeconds, change.ElapsedTimeSeconds - minimumTime,
                    change.BeforeLane, change.TargetLane, change.PlannedLane, change.Lane, entry.LateralPosition, change.LateralPosition,
                    change.EntrySpeed, change.PhysicsSpeed, traversalExit, change.Speed, diagnostic.TravelledMeters, diagnostic.EntrySurface,
                    change.Status, change.Outcome, diagnostic.CornerPhaseContext?.CornerProgress,
                    cornerEnd, corner?.MinimumSpeedMetersPerSecond,
                    apex, corner?.ApexSpeedMetersPerSecond, diagnostic.StandingStartLaunchProfile?.ReactionTimeSeconds));
            }
            foreach (var item in resolved.Events.Where(item => item.Type is SimulationEventType.ContactCrash or SimulationEventType.ContactLostRhythm))
                Contacts.Add(new BehaviorContact(step.StepNumber, step.LapIndex + 1, step.SegmentIndex, resolved.Snapshot.Segment.Type,
                    item.RiderId, item.OtherRiderId!.Value, item.Type));
            var order = Order(active.Select(item => (item.RiderId, item.Position.TotalSegmentProgress, item.ElapsedTimeSeconds))).ToArray();
            var ranks = order.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
            for (var earlier = 0; earlier < _previousOrder.Length; earlier++)
            for (var later = earlier + 1; later < _previousOrder.Length; later++)
            {
                var leader = _previousOrder[earlier];
                var trailing = _previousOrder[later];
                if (!ranks.TryGetValue(leader, out var leaderRank) || !ranks.TryGetValue(trailing, out var trailingRank) || trailingRank >= leaderRank) continue;
                var tied = _previous[leader] == _previous[trailing];
                var passed = active.Single(item => item.RiderId == trailing);
                Passes.Add(new BehaviorPass(step.StepNumber, step.LapIndex + 1, step.SegmentIndex,
                    _track.Segments[step.SegmentIndex].Id, trailing, leader, passed.Position.TotalSegmentProgress, tied));
            }
            _previousOrder = order;
            _previous = active.ToDictionary(item => item.RiderId, item => (item.Position.TotalSegmentProgress, item.ElapsedTimeSeconds));
        }
    }
}
