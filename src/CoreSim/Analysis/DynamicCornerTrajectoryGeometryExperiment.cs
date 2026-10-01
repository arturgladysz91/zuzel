using System.Collections.ObjectModel;
using System.Globalization;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record TrajectoryPlan(int EntryTarget, int ApexTarget, int ExitTarget)
{
    public string Id => $"E{EntryTarget}-A{ApexTarget}-X{ExitTarget}";
    public bool IsConstant => EntryTarget == ApexTarget && ApexTarget == ExitTarget;
}

public sealed record TrajectoryCornerObservation(
    int CornerId,
    float EntryLateralPosition,
    float OneThirdLateralPosition,
    float ApexLateralPosition,
    float TwoThirdsLateralPosition,
    float ExitLateralPosition,
    float EntryRadiusMeters,
    float MinimumRadiusMeters,
    float MeanTimeWeightedRadiusMeters,
    float ApexRadiusMeters,
    float ExitRadiusMeters,
    float EntrySpeedMetersPerSecond,
    float ApexSpeedMetersPerSecond,
    float MinimumSpeedMetersPerSecond,
    float MinimumSpeedCornerProgress,
    float ExitSpeedMetersPerSecond,
    float PeakLateralAccelerationProxyMetersPerSecondSquared,
    float MeanTimeWeightedLateralAccelerationProxyMetersPerSecondSquared,
    float CorrectionDistanceMeters,
    float CorrectionTimeSeconds,
    float PreApexCorrectionDistanceMeters,
    float PostApexCorrectionDistanceMeters,
    float ApexToExitTimeSeconds,
    float SpeedTenMetersAfterCornerMetersPerSecond,
    float FirstTwentyMetersStraightTimeSeconds);

public sealed record TrajectoryTurnSegmentObservation(
    int CornerId,
    int SegmentIndex,
    SegmentType SegmentType,
    float EntryLateralPosition,
    float ExitLateralPosition,
    float LateralChange,
    float PhysicalLateralChangeMeters,
    float EntryRadiusMeters,
    float ObservedExitRadiusMeters,
    float EntrySurfaceGrip,
    float EntrySurfaceRuts,
    float EntrySurfaceMoisture,
    float EntrySurfaceEffectiveGrip)
{
    public bool HasWithinSegmentLateralChange => LateralChange > 1e-6f;
}

public sealed record TrajectoryBenefitCostComparison(
    float ExitSpeedGainMetersPerSecond,
    float TenMeterSpeedGainMetersPerSecond,
    float FirstTwentyMetersStraightTimeGainSeconds,
    double FlyingLossSeconds,
    float ProductionDistanceDifferenceMeters);

public sealed record TrajectoryObservation(
    TrajectoryPlan Plan,
    string ActualTrajectoryFingerprint,
    IReadOnlyList<TrajectoryCornerObservation> Corners,
    IReadOnlyList<TrajectoryTurnSegmentObservation> TurnSegments,
    float FourLapDistanceMeters,
    double FlyingLapMedianSeconds,
    IReadOnlyList<float> FlyingLapTimesSeconds,
    float HeatTimeSeconds,
    float MaximumSpeedMetersPerSecond,
    float EntryLateralPosition,
    float ApexLateralPosition,
    float ExitLateralPosition,
    float EntryRadiusMeters,
    float MinimumRadiusMeters,
    float MeanTimeWeightedRadiusMeters,
    float ApexRadiusMeters,
    float ExitRadiusMeters,
    float CornerEntrySpeedMetersPerSecond,
    float TrueApexSpeedMetersPerSecond,
    float MinimumSpeedMetersPerSecond,
    float MinimumSpeedCornerProgress,
    float CornerExitSpeedMetersPerSecond,
    float PeakLateralAccelerationProxyMetersPerSecondSquared,
    float MeanTimeWeightedLateralAccelerationProxyMetersPerSecondSquared,
    float CorrectionDistanceMeters,
    float CorrectionTimeSeconds,
    float PreApexCorrectionDistanceMeters,
    float PostApexCorrectionDistanceMeters,
    float SpeedTenMetersAfterCornerMetersPerSecond,
    float ApexToExitTimeSeconds,
    float FirstTwentyMetersStraightTimeSeconds,
    float EstimatedExtraDiagonalDistancePerLapMeters,
    float EstimatedExtraTimeEquivalentSeconds,
    int BrakeCount,
    int RunWideCount,
    int CrashCount,
    int TargetTransitions,
    int PlannedLaneTransitions,
    int ActualWaypointArrivals,
    int PlannerCapBindingCount,
    float PlannerCapBindingFraction)
{
    public float ExitRelease => ExitLateralPosition - EntryLateralPosition;
    public bool IsWideExit => ExitRelease > DynamicCornerTrajectoryGeometryExperiment.WideExitThreshold;
}

public sealed record TrajectorySurfaceResult(
    string ProfileId,
    float Severity,
    TrajectoryObservation BestTrajectory,
    TrajectoryObservation BestConstantTrajectory,
    TrajectoryObservation? BestWideExitTrajectory,
    double BestVsConstantDeltaSeconds,
    bool DidOptimalTrajectoryChange);

public sealed record TrajectoryArchetypeResult(
    string Archetype,
    IReadOnlyList<TrajectoryObservation> TopThree,
    double TopThreeSpreadSeconds);

public sealed record TrajectorySetupResult(
    float TractionBias,
    TrajectoryObservation BestTrajectory);

public sealed class DynamicCornerTrajectoryGeometryExperimentResult
{
    public IReadOnlyList<TrajectoryPlan> TargetPlans { get; }
    public IReadOnlyList<TrajectoryObservation> UniformPlans { get; }
    public IReadOnlyList<TrajectoryObservation> UniqueUniformTrajectories { get; }
    public IReadOnlyList<TrajectoryObservation> PlannerIndependentTrajectories { get; }
    public IReadOnlyList<TrajectoryObservation> TopTenPlannerIndependent { get; }
    public IReadOnlyList<TrajectoryObservation> TopFifteen { get; }
    public IReadOnlyList<TrajectoryObservation> Shortlist { get; }
    public IReadOnlyList<TrajectorySurfaceResult> SurfaceResults { get; }
    public IReadOnlyList<TrajectoryArchetypeResult> SkillResults { get; }
    public IReadOnlyList<TrajectorySetupResult> SetupResults { get; }
    public TrajectoryObservation OverallBest { get; }
    public TrajectoryObservation BestDynamic { get; }
    public TrajectoryObservation ConstantInner { get; }
    public TrajectoryObservation? BestWideExit { get; }
    public TrajectoryObservation BestPlannerIndependent { get; }
    public TrajectoryObservation? BestPlannerIndependentDynamic { get; }
    public TrajectoryObservation? BestPlannerIndependentWideExit { get; }
    public int DuplicateTargetPlanCount { get; }
    public int PlannerBoundPlanCount { get; }
    public bool ConstantReferencesReproduce45 { get; }
    public bool DynamicTrajectoryAdvantageObserved { get; }
    public bool ConstantInnerDominates { get; }
    public bool TrajectoryGeometrySignalWeak { get; }
    public bool WideExitBenefitObserved { get; }
    public bool TrajectoryPlannerResolutionTooCoarse { get; }
    public bool DiagonalDistanceOmissionCouldExplainWinner { get; }
    public bool SurfaceDependentTrajectoryOptimum { get; }
    public bool SkillDependentTrajectoryOptimum { get; }
    public bool CornerAsymmetryUnexpected { get; }
    public bool PlannerIndependentDynamicTrajectoryExists { get; }
    public bool PlannerIndependentWideExitExists { get; }
    public bool PlannerCanExplainConstantInnerDominance { get; }
    public bool WithinSegmentTrajectorySamplingLimitation { get; }
    public bool DynamicPathUsesWithinSegmentLateralChange { get; }
    public bool WithinSegmentChangingRadiusNotFullyIntegrated { get; }
    public bool CornerTrajectoryEconomicsMismatch { get; }
    public TrajectoryBenefitCostComparison? WideExitBenefitCost { get; }
    public bool Deterministic { get; }
    public string Classification { get; }
    public string FirstActualBottleneck { get; }
    public string RecommendedSubsystem { get; }

    internal DynamicCornerTrajectoryGeometryExperimentResult(
        IEnumerable<TrajectoryPlan> targetPlans,
        IEnumerable<TrajectoryObservation> uniformPlans,
        IEnumerable<TrajectoryObservation> uniqueUniformTrajectories,
        IEnumerable<TrajectoryObservation> plannerIndependentTrajectories,
        IEnumerable<TrajectoryObservation> topTenPlannerIndependent,
        IEnumerable<TrajectoryObservation> topFifteen,
        IEnumerable<TrajectoryObservation> shortlist,
        IEnumerable<TrajectorySurfaceResult> surfaceResults,
        IEnumerable<TrajectoryArchetypeResult> skillResults,
        IEnumerable<TrajectorySetupResult> setupResults,
        TrajectoryObservation overallBest,
        TrajectoryObservation bestDynamic,
        TrajectoryObservation constantInner,
        TrajectoryObservation? bestWideExit,
        TrajectoryObservation bestPlannerIndependent,
        TrajectoryObservation? bestPlannerIndependentDynamic,
        TrajectoryObservation? bestPlannerIndependentWideExit,
        bool constantReferencesReproduce45,
        bool dynamicTrajectoryAdvantageObserved,
        bool constantInnerDominates,
        bool trajectoryGeometrySignalWeak,
        bool wideExitBenefitObserved,
        bool trajectoryPlannerResolutionTooCoarse,
        bool diagonalDistanceOmissionCouldExplainWinner,
        bool surfaceDependentTrajectoryOptimum,
        bool skillDependentTrajectoryOptimum,
        bool cornerAsymmetryUnexpected,
        bool plannerIndependentDynamicTrajectoryExists,
        bool plannerIndependentWideExitExists,
        bool plannerCanExplainConstantInnerDominance,
        bool withinSegmentTrajectorySamplingLimitation,
        bool dynamicPathUsesWithinSegmentLateralChange,
        bool withinSegmentChangingRadiusNotFullyIntegrated,
        bool cornerTrajectoryEconomicsMismatch,
        TrajectoryBenefitCostComparison? wideExitBenefitCost,
        bool deterministic,
        string classification,
        string firstActualBottleneck,
        string recommendedSubsystem)
    {
        TargetPlans = ReadOnly(targetPlans);
        UniformPlans = ReadOnly(uniformPlans);
        UniqueUniformTrajectories = ReadOnly(uniqueUniformTrajectories);
        PlannerIndependentTrajectories = ReadOnly(plannerIndependentTrajectories);
        TopTenPlannerIndependent = ReadOnly(topTenPlannerIndependent);
        TopFifteen = ReadOnly(topFifteen);
        Shortlist = ReadOnly(shortlist);
        SurfaceResults = ReadOnly(surfaceResults);
        SkillResults = ReadOnly(skillResults);
        SetupResults = ReadOnly(setupResults);
        OverallBest = overallBest;
        BestDynamic = bestDynamic;
        ConstantInner = constantInner;
        BestWideExit = bestWideExit;
        BestPlannerIndependent = bestPlannerIndependent;
        BestPlannerIndependentDynamic = bestPlannerIndependentDynamic;
        BestPlannerIndependentWideExit = bestPlannerIndependentWideExit;
        DuplicateTargetPlanCount = UniformPlans.Count - UniqueUniformTrajectories.Count;
        PlannerBoundPlanCount = UniformPlans.Count(item => item.PlannerCapBindingCount > 0);
        ConstantReferencesReproduce45 = constantReferencesReproduce45;
        DynamicTrajectoryAdvantageObserved = dynamicTrajectoryAdvantageObserved;
        ConstantInnerDominates = constantInnerDominates;
        TrajectoryGeometrySignalWeak = trajectoryGeometrySignalWeak;
        WideExitBenefitObserved = wideExitBenefitObserved;
        TrajectoryPlannerResolutionTooCoarse = trajectoryPlannerResolutionTooCoarse;
        DiagonalDistanceOmissionCouldExplainWinner = diagonalDistanceOmissionCouldExplainWinner;
        SurfaceDependentTrajectoryOptimum = surfaceDependentTrajectoryOptimum;
        SkillDependentTrajectoryOptimum = skillDependentTrajectoryOptimum;
        CornerAsymmetryUnexpected = cornerAsymmetryUnexpected;
        PlannerIndependentDynamicTrajectoryExists = plannerIndependentDynamicTrajectoryExists;
        PlannerIndependentWideExitExists = plannerIndependentWideExitExists;
        PlannerCanExplainConstantInnerDominance = plannerCanExplainConstantInnerDominance;
        WithinSegmentTrajectorySamplingLimitation = withinSegmentTrajectorySamplingLimitation;
        DynamicPathUsesWithinSegmentLateralChange = dynamicPathUsesWithinSegmentLateralChange;
        WithinSegmentChangingRadiusNotFullyIntegrated = withinSegmentChangingRadiusNotFullyIntegrated;
        CornerTrajectoryEconomicsMismatch = cornerTrajectoryEconomicsMismatch;
        WideExitBenefitCost = wideExitBenefitCost;
        Deterministic = deterministic;
        Classification = classification;
        FirstActualBottleneck = firstActualBottleneck;
        RecommendedSubsystem = recommendedSubsystem;
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}

/// <summary>Analysis-only waypoint adapter. It owns no state and does not alter production planning.</summary>
internal sealed class TrajectoryPlanDecisionModel(TrajectoryPlan plan) : IRiderDecisionModel
{
    public RiderDecision Decide(TrackSegment segment, RiderState rider) => Decide(segment.Type);
    public RiderDecision Decide(RiderDecisionContext context) => Decide(context.Segment.Type);

    private RiderDecision Decide(SegmentType type) => new(type switch
    {
        SegmentType.Straight => plan.EntryTarget,
        SegmentType.TurnEntry => plan.ApexTarget,
        SegmentType.TurnMiddle or SegmentType.TurnExit => plan.ExitTarget,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    }, 0f);
}

/// <summary>
/// Production-observation benchmark for #46. Candidate timing is always produced by
/// CalibrationRunner -> HeatSimulator; derived geometry never feeds simulation.
/// </summary>
public static class DynamicCornerTrajectoryGeometryExperiment
{
    public const int HeatId = 46;
    public const int FixedSeed = 46000;
    public const string BaseMainSha = "4c54c7d2cc87c79ad7c28fd2b86080fb68319a99";
    public const float WideExitThreshold = .25f;
    public const double GameplayTieToleranceSeconds = .02d;
    public const bool ProductionUsesSegmentEntryTrajectorySampling = true;

    private static readonly (string Id, float Severity)[] SurfaceProfiles =
    {
        (DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile, .50f),
        (DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile, 1f),
        (DynamicLineChoiceTrackEvolutionExperiment.MiddleProfile, .50f),
        (DynamicLineChoiceTrackEvolutionExperiment.MiddleProfile, 1f),
    };

    public static IReadOnlyList<TrajectoryPlan> Plans { get; } = Array.AsReadOnly(
        (from entry in Enumerable.Range(0, LaneModel.LanesCount)
         from apex in Enumerable.Range(0, LaneModel.LanesCount)
         from exit in Enumerable.Range(0, LaneModel.LanesCount)
         select new TrajectoryPlan(entry, apex, exit)).ToArray());

    public static IReadOnlyDictionary<string, TrajectoryPlan> NamedReferences { get; } =
        new ReadOnlyDictionary<string, TrajectoryPlan>(new Dictionary<string, TrajectoryPlan>(StringComparer.Ordinal)
        {
            ["ConstantInner"] = new(0, 0, 0),
            ["ConstantL1"] = new(1, 1, 1),
            ["ConstantL2"] = new(2, 2, 2),
            ["ConstantL3"] = new(3, 3, 3),
            ["ConstantOuter"] = new(4, 4, 4),
            ["TightEntryWideExit"] = new(0, 1, 2),
            ["BalancedRelease"] = new(1, 1, 2),
            ["WideDrive"] = new(2, 2, 3),
            ["WideEntryCutBack"] = new(2, 1, 2),
        });

    public static DynamicCornerTrajectoryGeometryExperimentResult Run()
    {
        var track = CreateTrack();
        var uniform = Plans.Select(plan => RunPlan(track, plan,
            FreshSurface(track, DynamicLineChoiceTrackEvolutionExperiment.UniformProfile, 0f),
            RiderSkills.Balanced, BikeSetup.Neutral)).ToArray();
        var unique = Deduplicate(uniform);
        var constantActualFingerprints = uniform.Where(item => item.Plan.IsConstant)
            .Select(item => item.ActualTrajectoryFingerprint)
            .ToHashSet(StringComparer.Ordinal);
        var plannerIndependent = unique.Where(item => item.PlannerCapBindingCount == 0).ToArray();
        var topTenPlannerIndependent = plannerIndependent.Take(10).ToArray();
        var top15 = unique.Take(15).ToArray();
        var constantInner = uniform.Single(item => item.Plan == NamedReferences["ConstantInner"]);
        var overall = unique[0];
        var dynamicBest = unique.First(item =>
            !constantActualFingerprints.Contains(item.ActualTrajectoryFingerprint));
        var wide = unique.FirstOrDefault(item => item.IsWideExit);
        var bestPlannerIndependent = plannerIndependent[0];
        var bestPlannerIndependentDynamic = plannerIndependent.FirstOrDefault(item =>
            !constantActualFingerprints.Contains(item.ActualTrajectoryFingerprint));
        var bestPlannerIndependentWideExit = plannerIndependent.FirstOrDefault(item => item.IsWideExit);

        var shortlistPlans = top15.Take(10).Select(item => item.Plan)
            .Concat(NamedReferences.Values)
            .Distinct()
            .Select(plan => uniform.Single(item => item.Plan == plan))
            .GroupBy(item => item.ActualTrajectoryFingerprint, StringComparer.Ordinal)
            .Select(group => group.OrderBy(item => item.FlyingLapMedianSeconds)
                .ThenBy(item => item.Plan.Id, StringComparer.Ordinal).First())
            .OrderBy(item => item.FlyingLapMedianSeconds).ThenBy(item => item.Plan.Id, StringComparer.Ordinal)
            .ToArray();

        var surfaceResults = SurfaceProfiles.Select(profile =>
        {
            var observations = shortlistPlans.Select(reference => RunPlan(track, reference.Plan,
                FreshSurface(track, profile.Id, profile.Severity), RiderSkills.Balanced, BikeSetup.Neutral))
                .OrderBy(item => item.FlyingLapMedianSeconds).ThenBy(item => item.Plan.Id, StringComparer.Ordinal)
                .ToArray();
            var best = observations[0];
            var bestConstant = observations.Where(item => item.Plan.IsConstant)
                .OrderBy(item => item.FlyingLapMedianSeconds).ThenBy(item => item.Plan.Id, StringComparer.Ordinal).First();
            var bestWide = observations.Where(item => item.IsWideExit)
                .OrderBy(item => item.FlyingLapMedianSeconds).ThenBy(item => item.Plan.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            return new TrajectorySurfaceResult(profile.Id, profile.Severity, best, bestConstant, bestWide,
                best.FlyingLapMedianSeconds - bestConstant.FlyingLapMedianSeconds,
                !StringComparer.Ordinal.Equals(best.ActualTrajectoryFingerprint, overall.ActualTrajectoryFingerprint));
        }).ToArray();

        var skillProfiles = new[]
        {
            ("Balanced", RiderSkills.Balanced),
            ("Technical", new RiderSkills(50, 50, 80, 50, 50, 60)),
            ("Low-control", new RiderSkills(50, 50, 20, 50, 50, 40)),
            ("Fast/Loose", new RiderSkills(50, 80, 30, 50, 50, 40)),
        };
        var skillResults = skillProfiles.Select(profile =>
        {
            var top = shortlistPlans.Select(reference => RunPlan(track, reference.Plan,
                    FreshSurface(track, DynamicLineChoiceTrackEvolutionExperiment.UniformProfile, 0f),
                    profile.Item2, BikeSetup.Neutral))
                .OrderBy(item => item.FlyingLapMedianSeconds).ThenBy(item => item.Plan.Id, StringComparer.Ordinal)
                .Take(3).ToArray();
            return new TrajectoryArchetypeResult(profile.Item1, Array.AsReadOnly(top),
                top[^1].FlyingLapMedianSeconds - top[0].FlyingLapMedianSeconds);
        }).ToArray();

        var setupPlans = unique.Take(5).Select(item => item.Plan).ToArray();
        var setupResults = new[] { 0f, .5f, 1f }.Select(traction =>
        {
            var best = setupPlans.Select(plan => RunPlan(track, plan,
                    FreshSurface(track, DynamicLineChoiceTrackEvolutionExperiment.UniformProfile, 0f),
                    RiderSkills.Balanced, new BikeSetup(.5f, traction)))
                .OrderBy(item => item.FlyingLapMedianSeconds).ThenBy(item => item.Plan.Id, StringComparer.Ordinal).First();
            return new TrajectorySetupResult(traction, best);
        }).ToArray();

        var bestConstant = unique.Where(item => item.Plan.IsConstant).Min(item => item.FlyingLapMedianSeconds);
        var dynamicAdvantage = bestConstant - dynamicBest.FlyingLapMedianSeconds > GameplayTieToleranceSeconds;
        var constantInnerDominates = constantInner.FlyingLapMedianSeconds <= unique.Min(item => item.FlyingLapMedianSeconds);
        var weak = unique.Length < 2 || unique[1].FlyingLapMedianSeconds - unique[0].FlyingLapMedianSeconds
            <= GameplayTieToleranceSeconds;
        var plannerTooCoarse = unique.Length < Plans.Count / 2
            || uniform.Count(item => item.PlannerCapBindingCount > 0) > Plans.Count / 2;
        var wideBenefit = wide is not null
            && constantInner.FlyingLapMedianSeconds - wide.FlyingLapMedianSeconds > GameplayTieToleranceSeconds;
        var winningMargin = Math.Abs(overall.FlyingLapMedianSeconds - constantInner.FlyingLapMedianSeconds);
        var diagonalRisk = !overall.Plan.IsConstant && overall.EstimatedExtraTimeEquivalentSeconds
            >= .5d * winningMargin;
        var surfaceDependent = surfaceResults.Any(item => item.DidOptimalTrajectoryChange);
        var strongSurfaceContrast = surfaceResults.Where(item => item.Severity == .5f)
            .All(item => !item.DidOptimalTrajectoryChange)
            && surfaceResults.Where(item => item.Severity == 1f)
                .Any(item => item.DidOptimalTrajectoryChange);
        var balancedBest = skillResults.Single(item => item.Archetype == "Balanced").TopThree[0]
            .ActualTrajectoryFingerprint;
        var skillDependent = skillResults.Any(item => !StringComparer.Ordinal.Equals(
            item.TopThree[0].ActualTrajectoryFingerprint, balancedBest));
        var asymmetry = uniform.Any(item => CornerAsymmetry(item.Corners));
        var reproduction = ConstantReferencesReproduce45(uniform);
        var plannerIndependentDynamicExists = bestPlannerIndependentDynamic is not null;
        var plannerIndependentWideExitExists = bestPlannerIndependentWideExit is not null;
        var plannerCanExplainConstantInnerDominance = constantInnerDominates
            && (!plannerIndependentDynamicExists || !plannerIndependentWideExitExists);
        var withinSegmentSamplingLimitation = ProductionUsesSegmentEntryTrajectorySampling;
        var comparedDynamicPaths = new[]
            {
                bestPlannerIndependentDynamic,
                bestPlannerIndependentWideExit,
            }
            .Where(item => item is not null)
            .Cast<TrajectoryObservation>()
            .DistinctBy(item => item.ActualTrajectoryFingerprint, StringComparer.Ordinal)
            .ToArray();
        var dynamicPathUsesWithinSegmentLateralChange = comparedDynamicPaths
            .Any(item => item.TurnSegments.Any(segment => segment.HasWithinSegmentLateralChange));
        var withinSegmentChangingRadiusNotFullyIntegrated = withinSegmentSamplingLimitation
            && dynamicPathUsesWithinSegmentLateralChange;
        var plannerIndependentRadiusOrSpeedSignal = bestPlannerIndependentWideExit is not null
            && (bestPlannerIndependentWideExit.ApexRadiusMeters > constantInner.ApexRadiusMeters + 1e-6f
                || bestPlannerIndependentWideExit.ExitRadiusMeters > constantInner.ExitRadiusMeters + 1e-6f
                || bestPlannerIndependentWideExit.TrueApexSpeedMetersPerSecond
                    > constantInner.TrueApexSpeedMetersPerSecond + 1e-6f
                || bestPlannerIndependentWideExit.CornerExitSpeedMetersPerSecond
                    > constantInner.CornerExitSpeedMetersPerSecond + 1e-6f);
        var cornerTrajectoryEconomicsMismatch = constantInnerDominates
            && bestPlannerIndependentWideExit is not null
            && (bestPlannerIndependentWideExit.ApexRadiusMeters > constantInner.ApexRadiusMeters + 1e-6f
                || bestPlannerIndependentWideExit.ExitRadiusMeters > constantInner.ExitRadiusMeters + 1e-6f)
            && bestPlannerIndependentWideExit.CornerExitSpeedMetersPerSecond
                > constantInner.CornerExitSpeedMetersPerSecond + 1e-6f
            && bestPlannerIndependentWideExit.FlyingLapMedianSeconds
                > constantInner.FlyingLapMedianSeconds
            && !diagonalRisk;
        var wideExitBenefitCost = bestPlannerIndependentWideExit is null
            ? null
            : new TrajectoryBenefitCostComparison(
                bestPlannerIndependentWideExit.CornerExitSpeedMetersPerSecond
                    - constantInner.CornerExitSpeedMetersPerSecond,
                bestPlannerIndependentWideExit.SpeedTenMetersAfterCornerMetersPerSecond
                    - constantInner.SpeedTenMetersAfterCornerMetersPerSecond,
                constantInner.FirstTwentyMetersStraightTimeSeconds
                    - bestPlannerIndependentWideExit.FirstTwentyMetersStraightTimeSeconds,
                bestPlannerIndependentWideExit.FlyingLapMedianSeconds
                    - constantInner.FlyingLapMedianSeconds,
                bestPlannerIndependentWideExit.FourLapDistanceMeters
                    - constantInner.FourLapDistanceMeters);

        var flags = new List<string>();
        if (dynamicAdvantage) flags.Add("DynamicTrajectoryAdvantageObserved");
        if (constantInnerDominates) flags.Add("ConstantInnerDominates");
        if (weak) flags.Add("TrajectoryGeometrySignalWeak");
        flags.Add(wideBenefit ? "WideExitBenefitObserved" : "NoWideExitBenefitObserved");
        if (plannerTooCoarse) flags.Add("TrajectoryPlannerResolutionTooCoarse");
        if (diagonalRisk) flags.Add("DiagonalDistanceOmissionCouldExplainWinner");
        if (surfaceDependent) flags.Add("SurfaceDependentTrajectoryOptimum");
        if (strongSurfaceContrast) flags.Add("LineSwitchRequiresStrongSurfaceContrast");
        if (skillDependent) flags.Add("SkillDependentTrajectoryOptimum");
        if (asymmetry) flags.Add("CornerAsymmetryUnexpected");
        if (plannerIndependentDynamicExists) flags.Add("PlannerIndependentDynamicTrajectoryExists");
        if (plannerIndependentWideExitExists) flags.Add("PlannerIndependentWideExitExists");
        if (plannerCanExplainConstantInnerDominance) flags.Add("PlannerCanExplainConstantInnerDominance");
        if (withinSegmentSamplingLimitation) flags.Add("WithinSegmentTrajectorySamplingLimitation");
        if (dynamicPathUsesWithinSegmentLateralChange) flags.Add("DynamicPathUsesWithinSegmentLateralChange");
        if (withinSegmentChangingRadiusNotFullyIntegrated)
            flags.Add("WithinSegmentChangingRadiusNotFullyIntegrated");
        if (cornerTrajectoryEconomicsMismatch) flags.Add("CornerTrajectoryEconomicsMismatch");
        var dynamicActualTrajectoryExists = unique.Any(item =>
            !constantActualFingerprints.Contains(item.ActualTrajectoryFingerprint));
        var firstBottleneck = !constantInnerDominates ? "no ConstantInner dominance"
            : !dynamicActualTrajectoryExists
                || !plannerIndependentDynamicExists
                || !plannerIndependentWideExitExists
                ? "planner resolution / planning horizon"
            : !plannerIndependentRadiusOrSpeedSignal ? "trajectory geometry / corner physics"
            : diagonalRisk ? "distance accounting"
            : cornerTrajectoryEconomicsMismatch ? "corner trajectory physics economics"
            : "trajectory selection";
        var recommendation = cornerTrajectoryEconomicsMismatch
            && plannerIndependentWideExitExists
            && !plannerCanExplainConstantInnerDominance
            && !diagonalRisk
            && withinSegmentChangingRadiusNotFullyIntegrated
                ? "within-corner continuous trajectory/radius sampling experiment"
            : plannerCanExplainConstantInnerDominance
                ? "multi-segment continuous trajectory planning"
            : diagonalRisk ? "diagonal/spiral path-distance accounting"
            : weak ? "within-corner continuous radius/surface sampling"
            : "AdaptiveDecisionModel trajectory selection";

        var deterministicProbe = RunPlan(track, overall.Plan,
            FreshSurface(track, DynamicLineChoiceTrackEvolutionExperiment.UniformProfile, 0f),
            RiderSkills.Balanced, BikeSetup.Neutral);
        var deterministic = Equivalent(overall, deterministicProbe);

        return new DynamicCornerTrajectoryGeometryExperimentResult(
            Plans, uniform, unique, plannerIndependent, topTenPlannerIndependent,
            top15, shortlistPlans, surfaceResults, skillResults, setupResults,
            overall, dynamicBest, constantInner, wide, bestPlannerIndependent,
            bestPlannerIndependentDynamic, bestPlannerIndependentWideExit,
            reproduction, dynamicAdvantage,
            constantInnerDominates, weak, wideBenefit, plannerTooCoarse, diagonalRisk,
            surfaceDependent, skillDependent, asymmetry, plannerIndependentDynamicExists,
            plannerIndependentWideExitExists, plannerCanExplainConstantInnerDominance,
            withinSegmentSamplingLimitation, dynamicPathUsesWithinSegmentLateralChange,
            withinSegmentChangingRadiusNotFullyIntegrated,
            cornerTrajectoryEconomicsMismatch, wideExitBenefitCost, deterministic,
            string.Join(", ", flags), firstBottleneck, recommendation);
    }

    public static TrajectoryObservation RunUniform(TrajectoryPlan plan)
    {
        var track = CreateTrack();
        return RunPlan(track, plan,
            FreshSurface(track, DynamicLineChoiceTrackEvolutionExperiment.UniformProfile, 0f),
            RiderSkills.Balanced, BikeSetup.Neutral);
    }

    private static TrajectoryObservation RunPlan(
        Track track, TrajectoryPlan plan, TrackState state, RiderSkills skills, BikeSetup setup)
    {
        ValidatePlan(plan);
        var rider = new RiderState(new RiderProfile(1, "Trajectory rider", skills, RiderStyle.Balanced),
            plan.EntryTarget) { ActiveSetup = setup, LateralPosition = plan.EntryTarget };
        var trace = CalibrationRunner.RunHeat(track, state, new List<RiderState> { rider },
            new TrajectoryPlanDecisionModel(plan), Options(), HeatId);
        var flyingLaps = trace.LapSummaries.Where(item => item.LapNumber > 1)
            .OrderBy(item => item.LapNumber).ToArray();
        var flyingSamples = trace.StepSamples.Where(item => item.LapIndex > 0)
            .OrderBy(item => item.LapIndex).ThenBy(item => item.SegmentIndex).ToArray();
        var passes = flyingSamples.Where(item => item.SegmentType != SegmentType.Straight)
            .GroupBy(item => (item.LapIndex, CornerId: CornerId(item.SegmentIndex)))
            .OrderBy(group => group.Key.LapIndex).ThenBy(group => group.Key.CornerId)
            .Select(group => ObserveCorner(track, group.Key.CornerId,
                group.OrderBy(item => item.SegmentIndex).ToArray(), trace.StepSamples))
            .ToArray();
        var corners = passes.GroupBy(item => item.CornerId).OrderBy(group => group.Key)
            .Select(group => AggregateCorner(group.Key, group.ToArray())).ToArray();
        var turnSegments = flyingSamples.Where(item => item.SegmentType != SegmentType.Straight)
            .GroupBy(item => (CornerId: CornerId(item.SegmentIndex), item.SegmentIndex, item.SegmentType))
            .OrderBy(group => group.Key.CornerId).ThenBy(group => group.Key.SegmentIndex)
            .Select(group => ObserveTurnSegment(track, group.Key.CornerId, group.Key.SegmentIndex,
                group.Key.SegmentType, group.ToArray()))
            .ToArray();
        var summary = trace.RiderSummaries.Single();
        var extraByLap = flyingSamples.GroupBy(item => item.LapIndex).Select(group =>
            group.Sum(item => (double)ExtraDiagonalDistance(track, item))).ToArray();
        var extra = (float)Median(extraByLap);
        var lapDistance = flyingLaps.Average(item => item.LapDistanceMeters);
        var flying = Median(flyingLaps.Select(item => (double)item.LapTimeSeconds));
        var averageSpeed = lapDistance / (float)flying;
        var targetTransitions = CountTransitions(trace.StepSamples.Select(item => item.TargetLane));
        var plannedTransitions = CountTransitions(trace.StepSamples.Select(item => item.PlannedLane));
        var arrivals = trace.StepSamples.Count(item => Arrived(track, item, item.TargetLane));
        var bindings = trace.StepSamples.Count(item => item.TargetLane != item.PlannedLane);
        var fingerprint = string.Join("|", corners.Select(corner => FormattableString.Invariant(
            $"C{corner.CornerId}:{corner.EntryLateralPosition:0.######},{corner.OneThirdLateralPosition:0.######},{corner.ApexLateralPosition:0.######},{corner.TwoThirdsLateralPosition:0.######},{corner.ExitLateralPosition:0.######}")));

        return new TrajectoryObservation(
            plan, fingerprint, Array.AsReadOnly(corners), Array.AsReadOnly(turnSegments),
            summary.TotalDistanceMeters, flying,
            Array.AsReadOnly(flyingLaps.Select(item => item.LapTimeSeconds).ToArray()),
            summary.TotalTimeSeconds, summary.MaxSpeedMetersPerSecond,
            Mean(corners, item => item.EntryLateralPosition),
            Mean(corners, item => item.ApexLateralPosition),
            Mean(corners, item => item.ExitLateralPosition),
            Mean(corners, item => item.EntryRadiusMeters),
            corners.Min(item => item.MinimumRadiusMeters),
            Mean(corners, item => item.MeanTimeWeightedRadiusMeters),
            Mean(corners, item => item.ApexRadiusMeters),
            Mean(corners, item => item.ExitRadiusMeters),
            Mean(corners, item => item.EntrySpeedMetersPerSecond),
            Mean(corners, item => item.ApexSpeedMetersPerSecond),
            corners.Min(item => item.MinimumSpeedMetersPerSecond),
            corners.OrderBy(item => item.MinimumSpeedMetersPerSecond).First().MinimumSpeedCornerProgress,
            Mean(corners, item => item.ExitSpeedMetersPerSecond),
            corners.Max(item => item.PeakLateralAccelerationProxyMetersPerSecondSquared),
            Mean(corners, item => item.MeanTimeWeightedLateralAccelerationProxyMetersPerSecondSquared),
            Mean(corners, item => item.CorrectionDistanceMeters),
            Mean(corners, item => item.CorrectionTimeSeconds),
            Mean(corners, item => item.PreApexCorrectionDistanceMeters),
            Mean(corners, item => item.PostApexCorrectionDistanceMeters),
            Mean(corners, item => item.SpeedTenMetersAfterCornerMetersPerSecond),
            Mean(corners, item => item.ApexToExitTimeSeconds),
            Mean(corners, item => item.FirstTwentyMetersStraightTimeSeconds),
            extra, averageSpeed <= 0f ? 0f : extra / averageSpeed,
            flyingSamples.Count(item => item.Outcome == SegmentOutcome.Brake),
            flyingSamples.Count(item => item.Outcome == SegmentOutcome.RunWide),
            flyingSamples.Count(item => item.Outcome == SegmentOutcome.Crash),
            targetTransitions, plannedTransitions, arrivals, bindings,
            trace.StepSamples.Count == 0 ? 0f : (float)bindings / trace.StepSamples.Count);
    }

    private static TrajectoryTurnSegmentObservation ObserveTurnSegment(
        Track track, int cornerId, int segmentIndex, SegmentType segmentType,
        IReadOnlyList<CalibrationStepSample> samples)
    {
        var entry = M(samples, item => item.EntryLateralPosition);
        var exit = M(samples, item => item.ExitLateralPosition);
        var lateralChange = M(samples,
            item => MathF.Abs(item.ExitLateralPosition - item.EntryLateralPosition));
        var physicalChange = M(samples, item => MathF.Abs(
            LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
                item.ExitLateralPosition, item.SegmentType, track.Geometry)
            - LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
                item.EntryLateralPosition, item.SegmentType, track.Geometry)));
        return new TrajectoryTurnSegmentObservation(
            cornerId, segmentIndex, segmentType, entry, exit, lateralChange, physicalChange,
            LaneModel.TurnArcRadiusMeters(entry, track.Geometry),
            LaneModel.TurnArcRadiusMeters(exit, track.Geometry),
            M(samples, item => item.EntrySurfaceGrip),
            M(samples, item => item.EntrySurfaceRuts),
            M(samples, item => item.EntrySurfaceMoisture),
            M(samples, item => item.EntrySurfaceEffectiveGrip));
    }

    private static TrajectoryCornerObservation ObserveCorner(
        Track track, int cornerId, IReadOnlyList<CalibrationStepSample> samples,
        IReadOnlyList<CalibrationStepSample> allSamples)
    {
        var entry = PositionAt(samples, 0f);
        var third = PositionAt(samples, 1f / 3f);
        var apex = PositionAt(samples, .5f);
        var twoThirds = PositionAt(samples, 2f / 3f);
        var exit = PositionAt(samples, 1f);
        var entrySpeed = SpeedAt(samples, 0f);
        var apexSpeed = SpeedAt(samples, .5f);
        var exitSpeed = SpeedAt(samples, 1f);
        var nodes = samples.SelectMany(sample => sample.ContinuousCornerProfile!.Nodes
                .Select(node => (Sample: sample, Node: node)))
            .OrderBy(item => item.Node.CornerProgress).ToArray();
        var minimum = nodes.OrderBy(item => item.Node.SpeedMetersPerSecond)
            .ThenBy(item => item.Node.CornerProgress).First().Node;
        double weightedRadius = 0d, weightedProxy = 0d, totalTime = 0d;
        var peakProxy = 0f;
        foreach (var sample in samples)
        {
            var profileNodes = sample.ContinuousCornerProfile!.Nodes;
            for (var index = 1; index < profileNodes.Count; index++)
            {
                var previous = profileNodes[index - 1];
                var current = profileNodes[index];
                var dt = current.ElapsedTimeSeconds - previous.ElapsedTimeSeconds;
                if (dt <= 0f) continue;
                var progress = (previous.CornerProgress + current.CornerProgress) * .5f;
                var lateral = PositionAt(samples, progress);
                var radius = LaneModel.TurnArcRadiusMeters(lateral, track.Geometry);
                var speed = (previous.SpeedMetersPerSecond + current.SpeedMetersPerSecond) * .5f;
                var proxy = speed * speed / radius;
                weightedRadius += radius * dt;
                weightedProxy += proxy * dt;
                totalTime += dt;
                peakProxy = MathF.Max(peakProxy, proxy);
            }
        }
        var correctionDistance = samples.Sum(item => item.ContinuousCornerProfile!.CorrectionDistanceMeters);
        var correctionTime = samples.Sum(item => item.ContinuousCornerProfile!.CorrectionTimeSeconds);
        var pre = samples.Where(item => item.CornerPhase!.Value.CornerProgress < .5f)
            .Sum(item => item.ContinuousCornerProfile!.CorrectionDistanceMeters);
        var post = correctionDistance - pre;
        var exitSample = samples[^1];
        var nextStraightIndex = exitSample.SegmentIndex + 1;
        var straight = allSamples.Single(item => item.LapIndex == exitSample.LapIndex
            && item.SegmentIndex == nextStraightIndex && item.RiderId == exitSample.RiderId);
        var speed10 = SpeedAtDistance(straight, MathF.Min(10f, straight.TravelledMeters));
        var time20 = TimeAtDistance(straight, MathF.Min(20f, straight.TravelledMeters));
        var apexToExit = TimeAtCornerProgress(samples, 1f) - TimeAtCornerProgress(samples, .5f);
        var positions = new[] { entry, third, apex, twoThirds, exit };
        return new TrajectoryCornerObservation(
            cornerId, entry, third, apex, twoThirds, exit,
            LaneModel.TurnArcRadiusMeters(entry, track.Geometry),
            positions.Min(value => LaneModel.TurnArcRadiusMeters(value, track.Geometry)),
            totalTime <= 0d ? LaneModel.TurnArcRadiusMeters(apex, track.Geometry) : (float)(weightedRadius / totalTime),
            LaneModel.TurnArcRadiusMeters(apex, track.Geometry),
            LaneModel.TurnArcRadiusMeters(exit, track.Geometry),
            entrySpeed, apexSpeed, minimum.SpeedMetersPerSecond, minimum.CornerProgress, exitSpeed,
            peakProxy, totalTime <= 0d ? 0f : (float)(weightedProxy / totalTime),
            correctionDistance, correctionTime, pre, post, apexToExit, speed10, time20);
    }

    private static TrajectoryCornerObservation AggregateCorner(
        int cornerId, IReadOnlyList<TrajectoryCornerObservation> values) => new(
        cornerId,
        M(values, item => item.EntryLateralPosition), M(values, item => item.OneThirdLateralPosition),
        M(values, item => item.ApexLateralPosition), M(values, item => item.TwoThirdsLateralPosition),
        M(values, item => item.ExitLateralPosition), M(values, item => item.EntryRadiusMeters),
        values.Min(item => item.MinimumRadiusMeters), M(values, item => item.MeanTimeWeightedRadiusMeters),
        M(values, item => item.ApexRadiusMeters), M(values, item => item.ExitRadiusMeters),
        M(values, item => item.EntrySpeedMetersPerSecond), M(values, item => item.ApexSpeedMetersPerSecond),
        values.Min(item => item.MinimumSpeedMetersPerSecond),
        values.OrderBy(item => item.MinimumSpeedMetersPerSecond).First().MinimumSpeedCornerProgress,
        M(values, item => item.ExitSpeedMetersPerSecond),
        values.Max(item => item.PeakLateralAccelerationProxyMetersPerSecondSquared),
        M(values, item => item.MeanTimeWeightedLateralAccelerationProxyMetersPerSecondSquared),
        M(values, item => item.CorrectionDistanceMeters), M(values, item => item.CorrectionTimeSeconds),
        M(values, item => item.PreApexCorrectionDistanceMeters), M(values, item => item.PostApexCorrectionDistanceMeters),
        M(values, item => item.ApexToExitTimeSeconds),
        M(values, item => item.SpeedTenMetersAfterCornerMetersPerSecond),
        M(values, item => item.FirstTwentyMetersStraightTimeSeconds));

    private static TrajectoryObservation[] Deduplicate(IEnumerable<TrajectoryObservation> values) => values
        .GroupBy(item => item.ActualTrajectoryFingerprint, StringComparer.Ordinal)
        .Select(group => group.OrderBy(item => item.FlyingLapMedianSeconds)
            .ThenBy(item => item.Plan.Id, StringComparer.Ordinal).First())
        .OrderBy(item => item.FlyingLapMedianSeconds).ThenBy(item => item.Plan.Id, StringComparer.Ordinal)
        .ToArray();

    private static float PositionAt(IReadOnlyList<CalibrationStepSample> samples, float progress)
    {
        var sample = samples.FirstOrDefault(item => progress <= item.CornerPhase!.Value.SegmentEndCornerProgress + 1e-6f)
            ?? samples[^1];
        var phase = sample.CornerPhase!.Value;
        var span = phase.SegmentEndCornerProgress - phase.CornerProgress;
        var local = span <= 0f ? 0f : Math.Clamp((progress - phase.CornerProgress) / span, 0f, 1f);
        return sample.EntryLateralPosition + (sample.ExitLateralPosition - sample.EntryLateralPosition) * local;
    }

    private static float SpeedAt(IReadOnlyList<CalibrationStepSample> samples, float progress)
    {
        var nodes = samples.SelectMany(item => item.ContinuousCornerProfile!.Nodes)
            .OrderBy(item => item.CornerProgress).ToArray();
        if (progress <= nodes[0].CornerProgress) return nodes[0].SpeedMetersPerSecond;
        for (var index = 1; index < nodes.Length; index++)
        {
            if (progress > nodes[index].CornerProgress + 1e-6f) continue;
            var prior = nodes[index - 1];
            var span = nodes[index].CornerProgress - prior.CornerProgress;
            var t = span <= 0f ? 0f : (progress - prior.CornerProgress) / span;
            return prior.SpeedMetersPerSecond + (nodes[index].SpeedMetersPerSecond - prior.SpeedMetersPerSecond) * t;
        }
        return nodes[^1].SpeedMetersPerSecond;
    }

    private static float TimeAtCornerProgress(IReadOnlyList<CalibrationStepSample> samples, float progress)
    {
        var elapsed = 0f;
        foreach (var sample in samples)
        {
            var phase = sample.CornerPhase!.Value;
            if (progress > phase.SegmentEndCornerProgress + 1e-6f)
            {
                elapsed += sample.ContinuousCornerProfile!.TravelTimeSeconds;
                continue;
            }
            var nodes = sample.ContinuousCornerProfile!.Nodes;
            if (progress <= nodes[0].CornerProgress) return elapsed + nodes[0].ElapsedTimeSeconds;
            for (var index = 1; index < nodes.Count; index++)
            {
                if (progress > nodes[index].CornerProgress + 1e-6f) continue;
                var prior = nodes[index - 1];
                var span = nodes[index].CornerProgress - prior.CornerProgress;
                var t = span <= 0f ? 0f : (progress - prior.CornerProgress) / span;
                return elapsed + prior.ElapsedTimeSeconds
                    + (nodes[index].ElapsedTimeSeconds - prior.ElapsedTimeSeconds) * t;
            }
            return elapsed + sample.ContinuousCornerProfile.TravelTimeSeconds;
        }
        return elapsed;
    }

    private static float SpeedAtDistance(CalibrationStepSample sample, float distance)
    {
        if (sample.TravelledMeters <= 0f) return sample.ExitSpeedMetersPerSecond;
        var fraction = Math.Clamp(distance / sample.TravelledMeters, 0f, 1f);
        var squared = sample.EntrySpeedMetersPerSecond * sample.EntrySpeedMetersPerSecond
            + (sample.ExitSpeedMetersPerSecond * sample.ExitSpeedMetersPerSecond
                - sample.EntrySpeedMetersPerSecond * sample.EntrySpeedMetersPerSecond) * fraction;
        return MathF.Sqrt(MathF.Max(0f, squared));
    }

    private static float TimeAtDistance(CalibrationStepSample sample, float distance)
    {
        var speed = SpeedAtDistance(sample, distance);
        var denominator = sample.EntrySpeedMetersPerSecond + speed;
        return denominator <= 0f ? 0f : 2f * distance / denominator;
    }

    private static float ExtraDiagonalDistance(Track track, CalibrationStepSample sample)
    {
        var ds = sample.TravelledMeters;
        var start = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            sample.EntryLateralPosition, sample.SegmentType, track.Geometry);
        var end = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            sample.ExitLateralPosition, sample.SegmentType, track.Geometry);
        var dy = end - start;
        return MathF.Sqrt(ds * ds + dy * dy) - ds;
    }

    private static bool Arrived(Track track, CalibrationStepSample sample, int target)
    {
        var actual = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            sample.ExitLateralPosition, sample.SegmentType, track.Geometry);
        var desired = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            target, sample.SegmentType, track.Geometry);
        return MathF.Abs(actual - desired) <= LateralMovementModel.LaneArrivalToleranceMeters;
    }

    private static int CountTransitions(IEnumerable<int> values)
    {
        var array = values.ToArray();
        return array.Zip(array.Skip(1)).Count(pair => pair.First != pair.Second);
    }

    private static bool ConstantReferencesReproduce45(IEnumerable<TrajectoryObservation> values)
    {
        var expected = new[] { 13.397707d, 13.737913d, 14.092064d, 14.456406d, 14.828415d };
        var constants = values.Where(item => item.Plan.IsConstant).OrderBy(item => item.Plan.EntryTarget).ToArray();
        return constants.Length == expected.Length && constants.Select(item => item.FlyingLapMedianSeconds)
            .Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) <= 1e-5d)
            && constants.Zip(constants.Skip(1)).All(pair => pair.Second.FourLapDistanceMeters > pair.First.FourLapDistanceMeters);
    }

    private static bool CornerAsymmetry(IReadOnlyList<TrajectoryCornerObservation> corners)
    {
        if (corners.Count != 2) return true;
        return MathF.Abs(corners[0].ApexLateralPosition - corners[1].ApexLateralPosition) > .10f
            || MathF.Abs(corners[0].ExitSpeedMetersPerSecond - corners[1].ExitSpeedMetersPerSecond) > .20f;
    }

    private static bool Equivalent(TrajectoryObservation left, TrajectoryObservation right) =>
        StringComparer.Ordinal.Equals(left.ActualTrajectoryFingerprint, right.ActualTrajectoryFingerprint)
        && BitConverter.DoubleToInt64Bits(left.FlyingLapMedianSeconds)
            == BitConverter.DoubleToInt64Bits(right.FlyingLapMedianSeconds)
        && BitConverter.SingleToInt32Bits(left.HeatTimeSeconds)
            == BitConverter.SingleToInt32Bits(right.HeatTimeSeconds)
        && BitConverter.SingleToInt32Bits(left.FourLapDistanceMeters)
            == BitConverter.SingleToInt32Bits(right.FourLapDistanceMeters);

    private static Track CreateTrack() => MatchedVenueProfiles.Motoarena2026.CreateTrack(
        MatchedVenueProfiles.MotoarenaHistorical39StartLineToFirstCornerMeters);

    private static TrackState FreshSurface(Track track, string profile, float severity) =>
        DynamicLineChoiceTrackEvolutionExperiment.CreateProfileState(track, profile, severity);

    private static HeatSimulationOptions Options() => new()
    {
        Laps = 4,
        Seed = FixedSeed,
        Weather = new WeatherState(WeatherCondition.Dry, 0f, 0f),
        IncidentFrequency = 0f,
        EnableLogging = false,
    };

    private static void ValidatePlan(TrajectoryPlan plan)
    {
        LaneModel.ValidateLane(plan.EntryTarget);
        LaneModel.ValidateLane(plan.ApexTarget);
        LaneModel.ValidateLane(plan.ExitTarget);
    }

    private static int CornerId(int segmentIndex) => segmentIndex switch
    {
        1 or 2 or 3 => 0,
        5 or 6 or 7 => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(segmentIndex)),
    };

    private static float Mean<T>(IEnumerable<T> values, Func<T, float> selector) => values.Average(selector);
    private static float M<T>(IEnumerable<T> values, Func<T, float> selector) => (float)Median(values.Select(x => (double)selector(x)));

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) throw new InvalidOperationException("Median requires data.");
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) * .5d
            : ordered[middle];
    }
}
