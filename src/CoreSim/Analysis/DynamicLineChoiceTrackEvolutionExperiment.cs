using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record DynamicLineSurfaceObservation(
    string ProfileId,
    float Severity,
    int Lane,
    float Grip,
    float Ruts,
    float Moisture,
    float EffectiveGrip);

public sealed record FixedLineBenchmarkObservation(
    string ProfileId,
    float Severity,
    int Lane,
    float FourLapDistanceMeters,
    double FlyingLapMedianSeconds,
    double HeatTimeSeconds,
    double MaximumSpeedMetersPerSecond,
    double CornerEntrySpeedMetersPerSecond,
    double TrueApexSpeedMetersPerSecond,
    double CornerExitSpeedMetersPerSecond,
    DynamicLineSurfaceObservation Surface,
    int Rank,
    double DeltaToBestSeconds);

public sealed record LineSweepSummary(
    string ProfileId,
    float Severity,
    int BestLane,
    int SecondBestLane,
    double BestVsSecondGapSeconds,
    IReadOnlyList<FixedLineBenchmarkObservation> Lines);

public sealed record StaticDecisionObservation(
    string ProfileId,
    float Severity,
    float TrackReading,
    int Seed,
    int ActualBestLane,
    int ChosenLane,
    double RegretSeconds);

public sealed record DecisionOracleObservation(
    string ProfileId,
    float Severity,
    int PhysicalBestLane,
    int OracleLane,
    bool SeedInvariant,
    double OraclePhysicalRegretSeconds,
    IReadOnlyList<int> ChosenLaneHistogram);

public sealed record TrackReadingDecisionMetrics(
    string ProfileId,
    float Severity,
    float TrackReading,
    int SeedCount,
    double PerfectChoicePercent,
    double WithinPointZeroFiveSecondsPercent,
    double MeanRegretSeconds,
    double MedianRegretSeconds,
    double P90RegretSeconds,
    IReadOnlyList<int> ChosenLaneHistogram,
    int OracleLane,
    double OracleAgreementPercent,
    IReadOnlyList<int> OracleDifferenceHistogram);

public sealed record LateralExecutionObservation(
    int SlideControl,
    int Adaptability,
    string ProfileId,
    float Severity,
    int TargetLane,
    int PlannedLaneOnPreparationSegment,
    float StartingLateralPosition,
    float CornerEntryLateralPosition,
    float DistanceToPlannedLaneMeters,
    float DistanceToTargetLaneMeters,
    float PhysicalLateralMetersMoved,
    float ArrivalErrorMeters,
    bool Arrived,
    float SegmentTravelTimeSeconds,
    double FlyingLapMedianSeconds,
    double LapTimeConsequenceSeconds,
    float MaximumPossibleLateralMeters,
    bool PlannerCapBinding);

public sealed record EvolutionCornerSurfaceObservation(
    int Heat,
    int CornerId,
    int Lane,
    float Grip,
    float Ruts,
    float Moisture,
    float EffectiveGrip);

public sealed record EvolutionHeatObservation(
    int Heat,
    int BestPhysicalLane,
    double BestFlyingSeconds,
    double SecondBestGapSeconds,
    int? MostUsedLane,
    IReadOnlyList<int> LaneUsageCounts,
    IReadOnlyList<int> TargetLaneCounts,
    IReadOnlyList<int> CornerEntryLaneHistogram,
    float MeanCornerEntryLateralPosition,
    IReadOnlyList<FixedLineBenchmarkObservation> FixedLines,
    IReadOnlyList<EvolutionCornerSurfaceObservation> CornerSurfaces);

public sealed record TrackWorkLaneDelta(
    int Lane,
    float GripDelta,
    float RutsDelta,
    float MoistureDelta,
    float EffectiveGripDelta);

public sealed record TrackWorkProbeResult(
    IReadOnlyList<int> SelectedLanes,
    IReadOnlyList<FixedLineBenchmarkObservation> BeforeLines,
    IReadOnlyList<FixedLineBenchmarkObservation> AfterLines,
    IReadOnlyList<TrackWorkLaneDelta> SurfaceDeltas,
    int AiLaneBefore,
    int AiLaneAfter);

public sealed record WeatherLaneObservation(
    int Lane,
    float BeforeMoisture,
    float AfterMoisture,
    float MoistureDelta);

public sealed record WeatherSanityResult(
    WeatherCondition Condition,
    float RainIntensity,
    IReadOnlyList<WeatherLaneObservation> TurnLanes,
    bool ExposureCreatesDifferentSurfaces);

public sealed class DynamicLineChoiceTrackEvolutionExperimentResult
{
    public IReadOnlyList<float> Severities { get; }
    public IReadOnlyList<int> DecisionSeeds { get; }
    public IReadOnlyList<LineSweepSummary> StaticSweeps { get; }
    public IReadOnlyList<DynamicLineSurfaceObservation> StaticSurfaceMatrix { get; }
    public IReadOnlyList<StaticDecisionObservation> StaticDecisions { get; }
    public IReadOnlyList<DecisionOracleObservation> DecisionOracles { get; }
    public IReadOnlyList<TrackReadingDecisionMetrics> TrackReadingMetrics { get; }
    public IReadOnlyList<LateralExecutionObservation> ExecutionMatrix { get; }
    public IReadOnlyList<LateralExecutionObservation> AdjacentExecutionControl { get; }
    public IReadOnlyList<EvolutionHeatObservation> Evolution { get; }
    public TrackWorkProbeResult TrackWork { get; }
    public WeatherSanityResult Weather { get; }
    public float? OutsideFirstNonInnerBestSeverity { get; }
    public float? OutsideFirstOuterHalfBestSeverity { get; }
    public float? MiddleFirstNonInnerBestSeverity { get; }
    public float? MiddleFirstOuterHalfBestSeverity { get; }
    public int? FirstEvolutionLineSwitchHeat { get; }
    public double InitialLineSpreadSeconds { get; }
    public double FinalLineSpreadSeconds { get; }
    public IReadOnlyList<float> InitialToFinalGripChanges { get; }
    public IReadOnlyList<float> InitialToFinalRutsChanges { get; }
    public bool TrackReadingPerceptionSignalHealthy { get; }
    public bool TrackReadingPerceptionSignalWeak { get; }
    public bool TrackReadingPerceptionReversal { get; }
    public bool DecisionObjectiveVsFastestLineMismatch { get; }
    public bool ExecutionPlannerBound { get; }
    public bool PlanningHorizonLimitsExecutionExpression { get; }
    public bool ExecutionSkillCapacityWeak { get; }
    public bool ExecutionSkillCapacityExists { get; }
    public bool AdjacentExecutionFixtureNonDiscriminating { get; }
    public float ExecutionCapacitySpreadMeters { get; }
    public float ExecutionCapacityRatio { get; }
    public bool LineSwitchRequiresStrongSurfaceContrast { get; }
    public bool TrackEvolutionTooWeak { get; }
    public bool TrackEvolutionTooStrong { get; }
    public bool TrackEvolutionOscillationRisk { get; }
    public bool HealthyFeedbackLoop { get; }
    public bool BenchmarkCopiesPreservedPersistentState { get; }
    public bool RiderOrderInvariant { get; }
    public bool Deterministic { get; }
    public string Classification { get; }
    public string FirstActualBottleneck { get; }
    public string RecommendedSubsystem { get; }

    internal DynamicLineChoiceTrackEvolutionExperimentResult(
        IEnumerable<float> severities,
        IEnumerable<int> decisionSeeds,
        IEnumerable<LineSweepSummary> staticSweeps,
        IEnumerable<DynamicLineSurfaceObservation> staticSurfaceMatrix,
        IEnumerable<StaticDecisionObservation> staticDecisions,
        IEnumerable<DecisionOracleObservation> decisionOracles,
        IEnumerable<TrackReadingDecisionMetrics> trackReadingMetrics,
        IEnumerable<LateralExecutionObservation> executionMatrix,
        IEnumerable<LateralExecutionObservation> adjacentExecutionControl,
        IEnumerable<EvolutionHeatObservation> evolution,
        TrackWorkProbeResult trackWork,
        WeatherSanityResult weather,
        float? outsideFirstNonInnerBestSeverity,
        float? outsideFirstOuterHalfBestSeverity,
        float? middleFirstNonInnerBestSeverity,
        float? middleFirstOuterHalfBestSeverity,
        int? firstEvolutionLineSwitchHeat,
        double initialLineSpreadSeconds,
        double finalLineSpreadSeconds,
        IEnumerable<float> initialToFinalGripChanges,
        IEnumerable<float> initialToFinalRutsChanges,
        bool trackReadingPerceptionSignalHealthy,
        bool trackReadingPerceptionSignalWeak,
        bool trackReadingPerceptionReversal,
        bool decisionObjectiveVsFastestLineMismatch,
        bool executionPlannerBound,
        bool planningHorizonLimitsExecutionExpression,
        bool executionSkillCapacityWeak,
        bool executionSkillCapacityExists,
        bool adjacentExecutionFixtureNonDiscriminating,
        float executionCapacitySpreadMeters,
        float executionCapacityRatio,
        bool lineSwitchRequiresStrongSurfaceContrast,
        bool trackEvolutionTooWeak,
        bool trackEvolutionTooStrong,
        bool trackEvolutionOscillationRisk,
        bool healthyFeedbackLoop,
        bool benchmarkCopiesPreservedPersistentState,
        bool riderOrderInvariant,
        bool deterministic,
        string classification,
        string firstActualBottleneck,
        string recommendedSubsystem)
    {
        Severities = ReadOnly(severities);
        DecisionSeeds = ReadOnly(decisionSeeds);
        StaticSweeps = ReadOnly(staticSweeps);
        StaticSurfaceMatrix = ReadOnly(staticSurfaceMatrix);
        StaticDecisions = ReadOnly(staticDecisions);
        DecisionOracles = ReadOnly(decisionOracles);
        TrackReadingMetrics = ReadOnly(trackReadingMetrics);
        ExecutionMatrix = ReadOnly(executionMatrix);
        AdjacentExecutionControl = ReadOnly(adjacentExecutionControl);
        Evolution = ReadOnly(evolution);
        TrackWork = trackWork;
        Weather = weather;
        OutsideFirstNonInnerBestSeverity = outsideFirstNonInnerBestSeverity;
        OutsideFirstOuterHalfBestSeverity = outsideFirstOuterHalfBestSeverity;
        MiddleFirstNonInnerBestSeverity = middleFirstNonInnerBestSeverity;
        MiddleFirstOuterHalfBestSeverity = middleFirstOuterHalfBestSeverity;
        FirstEvolutionLineSwitchHeat = firstEvolutionLineSwitchHeat;
        InitialLineSpreadSeconds = initialLineSpreadSeconds;
        FinalLineSpreadSeconds = finalLineSpreadSeconds;
        InitialToFinalGripChanges = ReadOnly(initialToFinalGripChanges);
        InitialToFinalRutsChanges = ReadOnly(initialToFinalRutsChanges);
        TrackReadingPerceptionSignalHealthy = trackReadingPerceptionSignalHealthy;
        TrackReadingPerceptionSignalWeak = trackReadingPerceptionSignalWeak;
        TrackReadingPerceptionReversal = trackReadingPerceptionReversal;
        DecisionObjectiveVsFastestLineMismatch = decisionObjectiveVsFastestLineMismatch;
        ExecutionPlannerBound = executionPlannerBound;
        PlanningHorizonLimitsExecutionExpression = planningHorizonLimitsExecutionExpression;
        ExecutionSkillCapacityWeak = executionSkillCapacityWeak;
        ExecutionSkillCapacityExists = executionSkillCapacityExists;
        AdjacentExecutionFixtureNonDiscriminating = adjacentExecutionFixtureNonDiscriminating;
        ExecutionCapacitySpreadMeters = executionCapacitySpreadMeters;
        ExecutionCapacityRatio = executionCapacityRatio;
        LineSwitchRequiresStrongSurfaceContrast = lineSwitchRequiresStrongSurfaceContrast;
        TrackEvolutionTooWeak = trackEvolutionTooWeak;
        TrackEvolutionTooStrong = trackEvolutionTooStrong;
        TrackEvolutionOscillationRisk = trackEvolutionOscillationRisk;
        HealthyFeedbackLoop = healthyFeedbackLoop;
        BenchmarkCopiesPreservedPersistentState = benchmarkCopiesPreservedPersistentState;
        RiderOrderInvariant = riderOrderInvariant;
        Deterministic = deterministic;
        Classification = classification;
        FirstActualBottleneck = firstActualBottleneck;
        RecommendedSubsystem = recommendedSubsystem;
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}

/// <summary>
/// Production-observation experiment for #45. It uses the production heat,
/// decision, lateral movement, surface wear, weather and track-work paths and
/// never supplies a calibration physics adjustment.
/// </summary>
public static class DynamicLineChoiceTrackEvolutionExperiment
{
    public const int HeatId = 45;
    public const int StaticSeed = 45000;
    public const int DecisionSeedStart = 45101;
    public const int DecisionSeedCount = 64;
    public const int EvolutionHeatCount = 12;
    public const string BaseMainSha = "e6d519a1d00d82dc75570db8b596304d727b923a";
    public const string UniformProfile = "Uniform";
    public const string OutsideProfile = "OutsideCushion";
    public const string OutsideStrongProfile = "OutsideCushionStrong";
    public const string MiddleProfile = "MiddleCushion";

    private static readonly float[] SeverityValues = { 0f, .25f, .50f, .75f, 1f };
    private static readonly int[] TrackReadingValues = { 20, 50, 80 };
    private static readonly (int SlideControl, int Adaptability)[] ExecutionSkills =
    {
        (20, 20), (20, 80), (50, 50), (80, 20), (80, 80),
    };
    private static readonly float[] OutsideGripDeltas = { -.30f, -.20f, -.10f, -.03f, 0f };
    private static readonly float[] OutsideRutsDeltas = { .45f, .30f, .15f, .05f, 0f };
    private static readonly float[] MiddleGripDeltas = { -.25f, -.12f, 0f, -.08f, -.18f };
    private static readonly float[] MiddleRutsDeltas = { .35f, .18f, 0f, .12f, .25f };
    private static readonly WeatherState NeutralWeather =
        new(WeatherCondition.Dry, rainIntensity: 0f, dryingRate: 0f);

    public static IReadOnlyList<float> Severities => Array.AsReadOnly(SeverityValues);
    public static IReadOnlyList<int> DecisionSeeds => Array.AsReadOnly(
        Enumerable.Range(DecisionSeedStart, DecisionSeedCount).ToArray());
    public static IReadOnlyList<(int SlideControl, int Adaptability)> ExecutionMatrixSkills =>
        Array.AsReadOnly(ExecutionSkills);

    public static DynamicLineChoiceTrackEvolutionExperimentResult Run()
    {
        var track = CreateTrack();
        var staticSweeps = new List<LineSweepSummary>
        {
            BenchmarkProfile(track, UniformProfile, 0f, CreateProfileState(track, UniformProfile, 0f)),
        };
        foreach (var profile in new[] { OutsideProfile, MiddleProfile })
        foreach (var severity in SeverityValues)
            staticSweeps.Add(BenchmarkProfile(track, profile, severity,
                CreateProfileState(track, profile, severity)));

        var surfaceMatrix = staticSweeps
            .SelectMany(summary => summary.Lines.Select(line => line.Surface))
            .ToArray();
        var decisionProfiles = new[]
        {
            (UniformProfile, 0f),
            (OutsideProfile, .50f),
            (OutsideProfile, 1f),
            (MiddleProfile, .50f),
            (MiddleProfile, 1f),
        };
        var staticDecisions = decisionProfiles.Select(profile =>
        {
            var sweep = FindSweep(staticSweeps, profile.Item1, profile.Item2);
            var chosen = Decide(track, CreateProfileState(track, profile.Item1, profile.Item2),
                trackReading: 50, seed: StaticSeed);
            return new StaticDecisionObservation(profile.Item1, profile.Item2, 50f, StaticSeed,
                 sweep.BestLane, chosen, Regret(sweep, chosen));
        }).ToArray();

        var decisionOracles = decisionProfiles.Select(profile =>
        {
            var sweep = FindSweep(staticSweeps, profile.Item1, profile.Item2);
            var choices = DecisionSeeds.Select(seed => Decide(track,
                CreateProfileState(track, profile.Item1, profile.Item2), trackReading: 100, seed))
                .ToArray();
            var distinct = choices.Distinct().ToArray();
            if (distinct.Length != 1)
            {
                throw new InvalidOperationException(
                    $"TR100 oracle is seed-dependent for {profile.Item1}/{profile.Item2}.");
            }

            var histogram = Enumerable.Range(0, LaneModel.LanesCount)
                .Select(lane => choices.Count(choice => choice == lane)).ToArray();
            return new DecisionOracleObservation(
                profile.Item1,
                profile.Item2,
                sweep.BestLane,
                distinct[0],
                SeedInvariant: true,
                Regret(sweep, distinct[0]),
                Array.AsReadOnly(histogram));
        }).ToArray();

        var readingMetrics = decisionProfiles.SelectMany(profile => TrackReadingValues.Select(trackReading =>
        {
            var sweep = FindSweep(staticSweeps, profile.Item1, profile.Item2);
            var oracle = decisionOracles.Single(item => item.ProfileId == profile.Item1
                && BitConverter.SingleToInt32Bits(item.Severity)
                == BitConverter.SingleToInt32Bits(profile.Item2));
            var choices = DecisionSeeds.Select(seed => Decide(track,
                CreateProfileState(track, profile.Item1, profile.Item2), trackReading, seed)).ToArray();
            var regrets = choices.Select(choice => Regret(sweep, choice)).ToArray();
            var histogram = Enumerable.Range(0, LaneModel.LanesCount)
                .Select(lane => choices.Count(choice => choice == lane)).ToArray();
            var differenceHistogram = Enumerable.Range(-LaneModel.MaxLane, 2 * LaneModel.MaxLane + 1)
                .Select(delta => choices.Count(choice => choice - oracle.OracleLane == delta)).ToArray();
            return new TrackReadingDecisionMetrics(
                profile.Item1,
                profile.Item2,
                trackReading,
                choices.Length,
                100d * choices.Count(choice => choice == sweep.BestLane) / choices.Length,
                100d * regrets.Count(regret => regret <= .0500001d) / regrets.Length,
                regrets.Average(),
                Median(regrets),
                Percentile(regrets, .90d),
                Array.AsReadOnly(histogram),
                oracle.OracleLane,
                100d * choices.Count(choice => choice == oracle.OracleLane) / choices.Length,
                Array.AsReadOnly(differenceHistogram));
        })).ToArray();

        var executionTarget = staticSweeps
            .Where(item => item.ProfileId != UniformProfile && item.Severity > 0f)
            .OrderByDescending(item => item.BestLane)
            .ThenByDescending(item => item.Severity)
            .ThenBy(item => item.ProfileId, StringComparer.Ordinal)
            .First();
        var execution = ExecutionSkills.Select(skills => RunExecution(
            track, executionTarget, executionTarget.BestLane,
            skills.SlideControl, skills.Adaptability)).ToArray();
        var adjacentExecution = ExecutionSkills.Select(skills => RunExecution(
            track, executionTarget, LaneModel.MinLane + 1,
            skills.SlideControl, skills.Adaptability)).ToArray();

        var evolution = RunEvolution(track, new[] { 1, 2, 3, 4 });
        var evolutionReordered = RunEvolution(track, new[] { 4, 3, 2, 1 });
        var riderOrderInvariant = EvolutionSignature(evolution.Observations)
            == EvolutionSignature(evolutionReordered.Observations);

        var initial = evolution.Observations[0];
        var final = evolution.Observations[^1];
        var firstSwitch = evolution.Observations.Skip(1)
            .FirstOrDefault(item => item.BestPhysicalLane != initial.BestPhysicalLane)?.Heat;
        var bestSequence = evolution.Observations.Select(item => item.BestPhysicalLane).ToArray();
        var switchCount = bestSequence.Zip(bestSequence.Skip(1)).Count(pair => pair.First != pair.Second);
        var gripChanges = Enumerable.Range(0, LaneModel.LanesCount).Select(lane =>
            MeanCornerValue(final.CornerSurfaces, lane, item => item.Grip)
            - MeanCornerValue(initial.CornerSurfaces, lane, item => item.Grip)).ToArray();
        var rutsChanges = Enumerable.Range(0, LaneModel.LanesCount).Select(lane =>
            MeanCornerValue(final.CornerSurfaces, lane, item => item.Ruts)
            - MeanCornerValue(initial.CornerSurfaces, lane, item => item.Ruts)).ToArray();
        var initialSpread = Spread(initial.FixedLines.Select(item => item.FlyingLapMedianSeconds));
        var finalSpread = Spread(final.FixedLines.Select(item => item.FlyingLapMedianSeconds));
        var surfaceChanged = gripChanges.Any(value => MathF.Abs(value) > 1e-6f)
            || rutsChanges.Any(value => MathF.Abs(value) > 1e-6f);
        var evolutionTooWeak = surfaceChanged && firstSwitch is null
            && Math.Abs(finalSpread - initialSpread) < .01d;
        var initialByLane = initial.FixedLines.ToDictionary(item => item.Lane);
        var evolutionTooStrong = final.FixedLines.Any(item =>
                item.FlyingLapMedianSeconds - initialByLane[item.Lane].FlyingLapMedianSeconds > 1d)
            || final.CornerSurfaces.Any(item => item.EffectiveGrip < .25f);
        var healthyFeedback = firstSwitch is not null
            && evolution.Observations.Skip(1).Where(item => item.MostUsedLane.HasValue)
                .Select(item => item.MostUsedLane!.Value).Distinct().Count() > 1;

        var work = RunTrackWork(track, evolution.PersistentState, final.FixedLines);
        var weather = RunWeatherSanity(track);
        var metricGroups = readingMetrics
            .GroupBy(item => (item.ProfileId, item.Severity)).ToArray();
        var agreementByTrackReading = TrackReadingValues.ToDictionary(
            trackReading => trackReading,
            trackReading => readingMetrics.Where(item => item.TrackReading == trackReading)
                .Average(item => item.OracleAgreementPercent));
        var agreementMonotonic = metricGroups.All(group =>
            group.Single(item => item.TrackReading == 20f).OracleAgreementPercent
                <= group.Single(item => item.TrackReading == 50f).OracleAgreementPercent + 1e-9d
            && group.Single(item => item.TrackReading == 50f).OracleAgreementPercent
                <= group.Single(item => item.TrackReading == 80f).OracleAgreementPercent + 1e-9d);
        var readingHealthy = agreementMonotonic
            && agreementByTrackReading[80] - agreementByTrackReading[20] >= 5d;
        var agreementSpread = agreementByTrackReading.Values.Max()
            - agreementByTrackReading.Values.Min();
        var readingWeak = agreementSpread < 1d;
        var readingReversal = agreementByTrackReading[80]
            < agreementByTrackReading[20] - 1d;
        var objectiveMismatch = decisionOracles.Any(item =>
            item.OracleLane != item.PhysicalBestLane
            && item.OraclePhysicalRegretSeconds > .0500001d);

        var minimumCapacity = execution.Min(item => item.MaximumPossibleLateralMeters);
        var maximumCapacity = execution.Max(item => item.MaximumPossibleLateralMeters);
        var capacitySpread = maximumCapacity - minimumCapacity;
        var capacityRatio = minimumCapacity <= 0f ? float.PositiveInfinity
            : maximumCapacity / minimumCapacity;
        var capacityExists = capacitySpread >= .10f && capacityRatio >= 1.05f;
        var capacityWeak = !capacityExists;
        var plannerBound = execution.All(item => item.PlannerCapBinding);
        var actualMovementSpread = execution.Max(item => item.PhysicalLateralMetersMoved)
            - execution.Min(item => item.PhysicalLateralMetersMoved);
        var planningHorizonLimitsExecution = plannerBound && capacityExists
            && actualMovementSpread <= LateralMovementModel.LaneArrivalToleranceMeters;
        var adjacentNonDiscriminating = adjacentExecution.All(item => item.Arrived)
            && adjacentExecution.Max(item => item.PhysicalLateralMetersMoved)
            - adjacentExecution.Min(item => item.PhysicalLateralMetersMoved)
            <= LateralMovementModel.LaneArrivalToleranceMeters;

        var outsideFirstNonInner = Threshold(staticSweeps, OutsideProfile,
            lane => lane != LaneModel.MinLane);
        var outsideFirstOuterHalf = Threshold(staticSweeps, OutsideProfile, lane => lane >= 2);
        var middleFirstNonInner = Threshold(staticSweeps, MiddleProfile,
            lane => lane != LaneModel.MinLane);
        var middleFirstOuterHalf = Threshold(staticSweeps, MiddleProfile, lane => lane >= 2);
        var lineSwitchRequiresStrongContrast = outsideFirstNonInner == 1f
            && middleFirstNonInner == 1f;

        var physicalVariation = staticSweeps.Any(item => item.BestLane != LaneModel.MinLane);
        var diagnostics = new List<string>();
        if (!physicalVariation) diagnostics.Add("PhysicalLineVariationInsufficient");
        if (lineSwitchRequiresStrongContrast) diagnostics.Add("LineSwitchRequiresStrongSurfaceContrast");
        if (objectiveMismatch) diagnostics.Add("DecisionObjectiveVsFastestLineMismatch");
        if (readingHealthy) diagnostics.Add("TrackReadingPerceptionSignalHealthy");
        else if (readingWeak) diagnostics.Add("TrackReadingPerceptionSignalWeak");
        else if (readingReversal) diagnostics.Add("TrackReadingPerceptionReversal");
        if (plannerBound) diagnostics.Add("ExecutionPlannerBound");
        if (planningHorizonLimitsExecution) diagnostics.Add("PlanningHorizonLimitsExecutionExpression");
        if (capacityWeak) diagnostics.Add("ExecutionSkillCapacityWeak");
        if (evolutionTooWeak) diagnostics.Add("TrackEvolutionTooWeak");
        if (evolutionTooStrong) diagnostics.Add("TrackEvolutionTooStrong");
        var classification = diagnostics.Count switch
        {
            0 => "DynamicLineGameplayAlreadyViable",
            1 => diagnostics[0],
            _ => "MixedDynamicLineGameplayDiagnostics: " + string.Join(", ", diagnostics),
        };
        var firstBottleneck = !physicalVariation
            ? "physical line economy"
            : objectiveMismatch
                ? "decision objective alignment"
                : readingWeak || readingReversal
                    ? "TrackReading perception signal"
                    : planningHorizonLimitsExecution
                        ? "lateral planning horizon"
                        : capacityWeak
                            ? "execution capacity"
                            : evolutionTooWeak || evolutionTooStrong
                                ? "track evolution"
                                : "none observed in the bounded experiment";
        var recommendation = firstBottleneck switch
        {
            "physical line economy" => "continuous within-corner surface traversal sampling",
            "decision objective alignment" => "AdaptiveDecisionModel route-cost alignment",
            "TrackReading perception signal" => "TrackReading perception calibration",
            "lateral planning horizon" => "multi-segment lateral preparation / planning horizon",
            "execution capacity" => "lateral execution capacity calibration",
            "track evolution" => "track-wear feedback calibration",
            _ => "traffic-aware dynamic trajectory planning",
        };

        var deterministic = EquivalentBenchmark(
            FindSweep(staticSweeps, UniformProfile, 0f).Lines[0],
            RunFixedLine(track, UniformProfile, 0f, 0,
                CreateProfileState(track, UniformProfile, 0f)));

        return new DynamicLineChoiceTrackEvolutionExperimentResult(
            SeverityValues,
            DecisionSeeds,
            staticSweeps,
            surfaceMatrix,
            staticDecisions,
            decisionOracles,
            readingMetrics,
            execution,
            adjacentExecution,
            evolution.Observations,
            work,
            weather,
            outsideFirstNonInner,
            outsideFirstOuterHalf,
            middleFirstNonInner,
            middleFirstOuterHalf,
            firstSwitch,
            initialSpread,
            finalSpread,
            gripChanges,
            rutsChanges,
            readingHealthy,
            readingWeak,
            readingReversal,
            objectiveMismatch,
            plannerBound,
            planningHorizonLimitsExecution,
            capacityWeak,
            capacityExists,
            adjacentNonDiscriminating,
            capacitySpread,
            capacityRatio,
            lineSwitchRequiresStrongContrast,
            evolutionTooWeak,
            evolutionTooStrong,
            switchCount > 4,
            healthyFeedback,
            evolution.BenchmarkCopiesPreservedPersistentState,
            riderOrderInvariant,
            deterministic,
            classification,
            firstBottleneck,
            recommendation);
    }

    public static TrackState CreateProfileState(Track track, string profileId, float severity)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (!float.IsFinite(severity) || severity < 0f || severity > 1f)
            throw new ArgumentOutOfRangeException(nameof(severity));
        if (profileId is not (UniformProfile or OutsideProfile or MiddleProfile))
            throw new ArgumentOutOfRangeException(nameof(profileId));
        var baseline = CalibrationScenarioCatalog.Baseline.Surface;
        return new TrackState(track.Segments.Count, LaneModel.LanesCount, (segment, lane) =>
        {
            if (track.Segments[segment].Type == SegmentType.Straight || profileId == UniformProfile)
                return baseline;
            var grip = profileId == OutsideProfile ? OutsideGripDeltas[lane] : MiddleGripDeltas[lane];
            var ruts = profileId == OutsideProfile ? OutsideRutsDeltas[lane] : MiddleRutsDeltas[lane];
            return baseline.WithDelta(severity * grip, severity * ruts, 0f);
        });
    }

    public static int Decide(Track track, TrackState state, int trackReading, int seed)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(state);
        var skills = new RiderSkills(50f, 50f, 50f, trackReading, 50f, 50f);
        var rider = new RiderState(new RiderProfile(1, "Decision probe", skills, RiderStyle.Balanced), 0)
        {
            ActiveSetup = BikeSetup.Neutral,
        };
        var engine = new SimulationEngine(new AdaptiveDecisionModel(HeatId));
        var snapshot = engine.CaptureSnapshot(track, state, new[] { rider },
            new SimulationStepContext(HeatId, 0, 0, 0, seed, 4));
        return engine.Decide(snapshot).Single().Decision.TargetLane;
    }

    private static Track CreateTrack() => MatchedVenueProfiles.Motoarena2026.CreateTrack(
        MatchedVenueProfiles.MotoarenaPrimaryStartLineToFirstCornerMeters);

    private static LineSweepSummary BenchmarkProfile(
        Track track,
        string profileId,
        float severity,
        TrackState source)
    {
        var raw = Enumerable.Range(LaneModel.MinLane, LaneModel.LanesCount)
            .Select(lane => RunFixedLine(track, profileId, severity, lane, CopyState(source)))
            .ToArray();
        var ordered = raw.OrderBy(item => item.FlyingLapMedianSeconds).ThenBy(item => item.Lane).ToArray();
        var best = ordered[0].FlyingLapMedianSeconds;
        var ranked = raw.Select(item => item with
        {
            Rank = Array.IndexOf(ordered, item) + 1,
            DeltaToBestSeconds = item.FlyingLapMedianSeconds - best,
        }).OrderBy(item => item.Lane).ToArray();
        return new LineSweepSummary(profileId, severity, ordered[0].Lane, ordered[1].Lane,
            ordered[1].FlyingLapMedianSeconds - best, Array.AsReadOnly(ranked));
    }

    private static FixedLineBenchmarkObservation RunFixedLine(
        Track track,
        string profileId,
        float severity,
        int lane,
        TrackState state)
    {
        var initial = state.Snapshot().SampleSurface(track.CornerTopology.Corners[0].StartSegmentIndex, lane);
        var rider = new RiderState(new RiderProfile(1, "Fixed-line rider", RiderSkills.Balanced,
            RiderStyle.Balanced), lane) { ActiveSetup = BikeSetup.Neutral };
        var trace = CalibrationRunner.RunHeat(track, state, new List<RiderState> { rider },
            new HoldLaneDecisionModel(lane), Options(StaticSeed), HeatId);
        var summary = trace.RiderSummaries.Single();
        var laps = trace.LapSummaries.OrderBy(item => item.LapNumber).ToArray();
        var cornerNodes = trace.StepSamples.Where(item => item.ContinuousCornerProfile is not null)
            .SelectMany(item => item.ContinuousCornerProfile!.Nodes).ToArray();
        var apex = cornerNodes.Where(item => MathF.Abs(item.CornerProgress - .5f) <= 1e-5f)
            .Select(item => (double)item.SpeedMetersPerSecond).ToArray();
        if (apex.Length == 0)
            apex = cornerNodes.OrderBy(item => MathF.Abs(item.CornerProgress - .5f)).Take(1)
                .Select(item => (double)item.SpeedMetersPerSecond).ToArray();
        return new FixedLineBenchmarkObservation(
            profileId,
            severity,
            lane,
            summary.TotalDistanceMeters,
            Median(laps.Skip(1).Select(item => (double)item.LapTimeSeconds)),
            summary.TotalTimeSeconds,
            summary.MaxSpeedMetersPerSecond,
            Median(trace.StepSamples.Where(item => item.SegmentType == SegmentType.TurnEntry)
                .Select(item => (double)item.EntrySpeedMetersPerSecond)),
            Median(apex),
            Median(trace.StepSamples.Where(item => item.SegmentType == SegmentType.TurnExit)
                .Select(item => (double)item.ExitSpeedMetersPerSecond)),
            new DynamicLineSurfaceObservation(profileId, severity, lane, initial.Grip,
                initial.Ruts, initial.Moisture, initial.EffectiveGrip),
            Rank: 0,
            DeltaToBestSeconds: 0d);
    }

    private static LateralExecutionObservation RunExecution(
        Track track,
        LineSweepSummary target,
        int targetLane,
        int slideControl,
        int adaptability)
    {
        var skills = new RiderSkills(50f, 50f, slideControl, 50f, 50f, adaptability);
        var rider = new RiderState(new RiderProfile(1, "Execution rider", skills, RiderStyle.Balanced), 0)
        {
            ActiveSetup = BikeSetup.Neutral,
        };
        var trace = CalibrationRunner.RunHeat(track,
            CreateProfileState(track, target.ProfileId, target.Severity),
            new List<RiderState> { rider },
            new TargetLaneDecisionModel(targetLane),
            Options(StaticSeed), HeatId);
        var firstStraight = trace.StepSamples.Single(item => item.LapIndex == 0 && item.SegmentIndex == 0);
        var cornerEntry = trace.StepSamples.Single(item => item.LapIndex == 0 && item.SegmentIndex == 1);
        var entry = cornerEntry.EntryLateralPosition;
        const float startingLateralPosition = 0f;
        var startOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            startingLateralPosition, SegmentType.Straight, track.Geometry);
        var plannedOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            firstStraight.PlannedLane, SegmentType.Straight, track.Geometry);
        var targetOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            targetLane, SegmentType.Straight, track.Geometry);
        var moved = MathF.Abs(LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            entry, SegmentType.Straight, track.Geometry) - startOffset);
        var distanceToPlanned = MathF.Abs(plannedOffset - startOffset);
        var distanceToTarget = MathF.Abs(targetOffset - startOffset);
        var error = MathF.Abs(
            LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(targetLane,
                SegmentType.TurnEntry, track.Geometry)
            - LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(entry,
                SegmentType.TurnEntry, track.Geometry));
        var movementTime = firstStraight.StandingStartMovementTimeSeconds
            ?? firstStraight.DurationSeconds;
        var maxMovement = LateralMovementModel.CalculateMaxLateralDistanceMeters(
            movementTime,
            SegmentType.Straight,
            track.Geometry,
            new TrackSurfaceState(firstStraight.EntrySurfaceGrip, firstStraight.EntrySurfaceRuts,
                firstStraight.EntrySurfaceMoisture),
            skills);
        var flying = Median(trace.LapSummaries.OrderBy(item => item.LapNumber).Skip(1)
            .Select(item => (double)item.LapTimeSeconds));
        var targetFixed = target.Lines.Single(item => item.Lane == targetLane);
        var plannerCapBinding = firstStraight.PlannedLane != targetLane
            && distanceToTarget > distanceToPlanned + LateralMovementModel.LaneArrivalToleranceMeters
            && maxMovement + LateralMovementModel.LaneArrivalToleranceMeters >= distanceToPlanned
            && MathF.Abs(moved - distanceToPlanned)
            <= LateralMovementModel.LaneArrivalToleranceMeters;
        return new LateralExecutionObservation(slideControl, adaptability, target.ProfileId,
            target.Severity, targetLane, firstStraight.PlannedLane, startingLateralPosition, entry,
            distanceToPlanned, distanceToTarget, moved, error,
            error <= LateralMovementModel.LaneArrivalToleranceMeters,
            movementTime, flying, flying - targetFixed.FlyingLapMedianSeconds, maxMovement,
            plannerCapBinding);
    }

    private static EvolutionRun RunEvolution(Track track, IReadOnlyList<int> riderOrder)
    {
        var state = CreateProfileState(track, UniformProfile, 0f);
        var observations = new List<EvolutionHeatObservation>();
        var copiesPreserved = true;
        AddObservation(0, null);
        for (var heat = 1; heat <= EvolutionHeatCount; heat++)
        {
            var riders = riderOrder.Select(id => new RiderState(
                new RiderProfile(id, $"Evolution rider {id}", RiderSkills.Balanced, RiderStyle.Balanced),
                id - 1) { ActiveSetup = BikeSetup.Neutral }).ToList();
            var seed = 45000 + heat;
            var trace = CalibrationRunner.RunHeat(track, state, riders,
                new AdaptiveDecisionModel(seed), Options(seed), heatId: heat);
            AddObservation(heat, trace);
        }
        return new EvolutionRun(Array.AsReadOnly(observations.ToArray()), state, copiesPreserved);

        void AddObservation(int heat, CalibrationTrace? trace)
        {
            var before = StateSignature(state);
            var sweep = BenchmarkProfile(track, $"EvolutionHeat{heat}", 0f, state);
            copiesPreserved &= StringComparer.Ordinal.Equals(before, StateSignature(state));
            var surfaces = ObserveCornerSurfaces(track, state, heat);
            var turns = trace?.StepSamples.Where(item => item.SegmentType != SegmentType.Straight)
                .ToArray() ?? Array.Empty<CalibrationStepSample>();
            var entries = trace?.StepSamples.Where(item => item.SegmentType == SegmentType.TurnEntry)
                .ToArray() ?? Array.Empty<CalibrationStepSample>();
            var usage = Histogram(turns.Select(item => NearestLane(item.EntryLateralPosition)));
            var targets = Histogram(trace?.StepSamples.Select(item => item.TargetLane)
                ?? Enumerable.Empty<int>());
            var entryHistogram = Histogram(entries.Select(item => NearestLane(item.EntryLateralPosition)));
            int? mostUsed = usage.Sum() == 0
                ? null
                : Enumerable.Range(0, LaneModel.LanesCount)
                    .OrderByDescending(lane => usage[lane]).ThenBy(lane => lane).First();
            observations.Add(new EvolutionHeatObservation(
                heat,
                sweep.BestLane,
                sweep.Lines.Min(item => item.FlyingLapMedianSeconds),
                sweep.BestVsSecondGapSeconds,
                mostUsed,
                Array.AsReadOnly(usage),
                Array.AsReadOnly(targets),
                Array.AsReadOnly(entryHistogram),
                entries.Length == 0 ? 0f : entries.Average(item => item.EntryLateralPosition),
                sweep.Lines,
                surfaces));
        }
    }

    private static TrackWorkProbeResult RunTrackWork(
        Track track,
        TrackState evolved,
        IReadOnlyList<FixedLineBenchmarkObservation> beforeLines)
    {
        var state = CopyState(evolved);
        var beforeSurface = ObserveCornerSurfaces(track, state, EvolutionHeatCount);
        var worn = Enumerable.Range(0, LaneModel.LanesCount)
            .OrderByDescending(lane => MeanCornerValue(beforeSurface, lane, item => item.Ruts))
            .ThenBy(lane => lane).Take(2).ToArray();
        var turnSegments = track.Segments.Select((segment, index) => (segment, index))
            .Where(item => item.segment.Type != SegmentType.Straight)
            .Select(item => item.index).ToArray();
        TrackEvolution.ApplyTrackWork(track, state,
            new TrackWorkAction(TrackWorkType.Pack, .50f, turnSegments, worn),
            HeatId, tick: 12, new SimLog(enabled: false));
        var afterSurface = ObserveCornerSurfaces(track, state, EvolutionHeatCount);
        var after = BenchmarkProfile(track, "AfterTrackWork", 0f, state).Lines;
        var deltas = Enumerable.Range(0, LaneModel.LanesCount).Select(lane =>
            new TrackWorkLaneDelta(
                lane,
                MeanCornerValue(afterSurface, lane, item => item.Grip)
                - MeanCornerValue(beforeSurface, lane, item => item.Grip),
                MeanCornerValue(afterSurface, lane, item => item.Ruts)
                - MeanCornerValue(beforeSurface, lane, item => item.Ruts),
                MeanCornerValue(afterSurface, lane, item => item.Moisture)
                - MeanCornerValue(beforeSurface, lane, item => item.Moisture),
                MeanCornerValue(afterSurface, lane, item => item.EffectiveGrip)
                - MeanCornerValue(beforeSurface, lane, item => item.EffectiveGrip)))
            .ToArray();
        return new TrackWorkProbeResult(
            Array.AsReadOnly(worn),
            beforeLines,
            after,
            Array.AsReadOnly(deltas),
            Decide(track, evolved, 50, 45012),
            Decide(track, state, 50, 45012));
    }

    private static WeatherSanityResult RunWeatherSanity(Track track)
    {
        var state = CreateProfileState(track, UniformProfile, 0f);
        var before = state.Snapshot();
        var weather = new WeatherState(WeatherCondition.Rain, .50f, 0f);
        TrackEvolution.ApplyWeather(track, state, weather, HeatId, tick: 0,
            new SimLog(enabled: false));
        var after = state.Snapshot();
        var segment = track.CornerTopology.Corners[0].StartSegmentIndex;
        var lanes = Enumerable.Range(0, LaneModel.LanesCount).Select(lane =>
        {
            var a = before.SampleSurface(segment, lane);
            var b = after.SampleSurface(segment, lane);
            return new WeatherLaneObservation(lane, a.Moisture, b.Moisture,
                b.Moisture - a.Moisture);
        }).ToArray();
        return new WeatherSanityResult(weather.Condition, weather.RainIntensity,
            Array.AsReadOnly(lanes), lanes.Select(item => item.AfterMoisture).Distinct().Count() > 1);
    }

    private static IReadOnlyList<EvolutionCornerSurfaceObservation> ObserveCornerSurfaces(
        Track track,
        TrackState state,
        int heat)
    {
        var snapshot = state.Snapshot();
        var values = new List<EvolutionCornerSurfaceObservation>();
        foreach (var corner in track.CornerTopology.Corners)
        foreach (var lane in Enumerable.Range(0, LaneModel.LanesCount))
        {
            var samples = Enumerable.Range(corner.StartSegmentIndex, corner.SegmentCount)
                .Select(segment => snapshot.SampleSurface(segment, lane)).ToArray();
            values.Add(new EvolutionCornerSurfaceObservation(heat, corner.CornerId, lane,
                samples.Average(item => item.Grip),
                samples.Average(item => item.Ruts),
                samples.Average(item => item.Moisture),
                samples.Average(item => item.EffectiveGrip)));
        }
        return Array.AsReadOnly(values.ToArray());
    }

    private static HeatSimulationOptions Options(int seed) => new()
    {
        Laps = 4,
        Seed = seed,
        Weather = NeutralWeather,
        IncidentFrequency = 0f,
        EnableLogging = false,
    };

    private static TrackState CopyState(TrackState source) => new(
        source.SegmentCount,
        source.LinesCount,
        (segment, lane) => source.GetSurface(segment, lane));

    private static LineSweepSummary FindSweep(
        IEnumerable<LineSweepSummary> sweeps,
        string profile,
        float severity) => sweeps.Single(item => item.ProfileId == profile
            && BitConverter.SingleToInt32Bits(item.Severity) == BitConverter.SingleToInt32Bits(severity));

    private static double Regret(LineSweepSummary sweep, int lane)
    {
        var chosen = sweep.Lines.Single(item => item.Lane == lane).FlyingLapMedianSeconds;
        var best = sweep.Lines.Min(item => item.FlyingLapMedianSeconds);
        return Math.Max(0d, chosen - best);
    }

    private static float? Threshold(
        IEnumerable<LineSweepSummary> sweeps,
        string profile,
        Func<int, bool> predicate) => sweeps.Where(item => item.ProfileId == profile)
        .OrderBy(item => item.Severity).FirstOrDefault(item => predicate(item.BestLane))?.Severity;

    private static int[] Histogram(IEnumerable<int> values)
    {
        var result = new int[LaneModel.LanesCount];
        foreach (var value in values)
        {
            LaneModel.ValidateLane(value);
            result[value]++;
        }
        return result;
    }

    private static int NearestLane(float lateralPosition) => LaneModel.ClampLane(
        (int)MathF.Round(lateralPosition, MidpointRounding.AwayFromZero));

    private static float MeanCornerValue(
        IEnumerable<EvolutionCornerSurfaceObservation> values,
        int lane,
        Func<EvolutionCornerSurfaceObservation, float> selector) =>
        values.Where(item => item.Lane == lane).Average(selector);

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) throw new InvalidOperationException("Median requires data.");
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) * .5d
            : ordered[middle];
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) throw new InvalidOperationException("Percentile requires data.");
        var index = Math.Clamp((int)Math.Ceiling(percentile * ordered.Length) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static double Spread(IEnumerable<double> values)
    {
        var actual = values.ToArray();
        return actual.Max() - actual.Min();
    }

    private static bool EquivalentBenchmark(
        FixedLineBenchmarkObservation left,
        FixedLineBenchmarkObservation right) =>
        BitConverter.DoubleToInt64Bits(left.FlyingLapMedianSeconds)
        == BitConverter.DoubleToInt64Bits(right.FlyingLapMedianSeconds)
        && BitConverter.DoubleToInt64Bits(left.HeatTimeSeconds)
        == BitConverter.DoubleToInt64Bits(right.HeatTimeSeconds)
        && BitConverter.SingleToInt32Bits(left.FourLapDistanceMeters)
        == BitConverter.SingleToInt32Bits(right.FourLapDistanceMeters);

    private static string StateSignature(TrackState state)
    {
        var builder = new StringBuilder();
        for (var segment = 0; segment < state.SegmentCount; segment++)
        for (var lane = 0; lane < state.LinesCount; lane++)
        {
            var surface = state.GetSurface(segment, lane);
            builder.Append(surface.Grip.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(surface.Ruts.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(surface.Moisture.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }
        return builder.ToString();
    }

    private static string EvolutionSignature(IEnumerable<EvolutionHeatObservation> observations)
    {
        var builder = new StringBuilder();
        foreach (var item in observations)
        {
            builder.Append(item.Heat).Append(':').Append(item.BestPhysicalLane).Append(':')
                .Append(item.BestFlyingSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .AppendJoin(',', item.LaneUsageCounts).Append(':')
                .AppendJoin(',', item.TargetLaneCounts).Append(';');
            foreach (var surface in item.CornerSurfaces)
                builder.Append(surface.Grip.ToString("R", CultureInfo.InvariantCulture)).Append('/')
                    .Append(surface.Ruts.ToString("R", CultureInfo.InvariantCulture)).Append('/');
        }
        return builder.ToString();
    }

    private sealed record EvolutionRun(
        IReadOnlyList<EvolutionHeatObservation> Observations,
        TrackState PersistentState,
        bool BenchmarkCopiesPreservedPersistentState);

    private sealed class HoldLaneDecisionModel(int lane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(lane, 0f);
        public RiderDecision Decide(RiderDecisionContext context) => new(lane, 0f);
    }

    private sealed class TargetLaneDecisionModel(int lane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(lane, 0f);
        public RiderDecision Decide(RiderDecisionContext context) => new(lane, 0f);
    }
}
