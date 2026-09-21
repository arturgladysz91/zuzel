using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record PreApexScrubCandidate
{
    public string Id { get; }
    public float PeakScrubDecelerationMetersPerSecondSquared { get; }

    public PreApexScrubCandidate(string id, float peakScrubDecelerationMetersPerSecondSquared)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _ = new PreApexScrubLossAdjustment(peakScrubDecelerationMetersPerSecondSquared);
        Id = id;
        PeakScrubDecelerationMetersPerSecondSquared = peakScrubDecelerationMetersPerSecondSquared;
    }
}

public sealed record PreApexScrubTimeBudget(
    double HomeStartStraightSeconds,
    double BackStraightSeconds,
    double HomeFinishStraightSeconds,
    double TotalStraightTimeSeconds,
    double FirstCornerEntryToApexSeconds,
    double FirstCornerApexToExitSeconds,
    double FirstCornerTotalSeconds,
    double SecondCornerEntryToApexSeconds,
    double SecondCornerApexToExitSeconds,
    double SecondCornerTotalSeconds,
    double TotalCornerTimeSeconds,
    double FlyingLapTimeSeconds)
{
    public double TotalPreApexTimeSeconds =>
        FirstCornerEntryToApexSeconds + SecondCornerEntryToApexSeconds;
    public double TotalPostApexTimeSeconds =>
        FirstCornerApexToExitSeconds + SecondCornerApexToExitSeconds;
}

public sealed record PreApexScrubDistanceBuckets(
    double TravelledDistanceMeters,
    double CorrectionDistanceMeters,
    double ScrubDistanceMeters,
    double PositiveDriveDistanceMeters,
    double NegativeSignedDriveDistanceMeters,
    double NeutralCarryDistanceMeters,
    double ScrubWhileDriveZeroDistanceMeters,
    double ScrubWhileDrivePositiveDistanceMeters,
    bool Conserved);

public sealed record PreApexScrubEnergyDiagnostic(
    double ScrubWorkJoules,
    double EntryKineticEnergyJoules,
    double TrueApexKineticEnergyJoules,
    double MinimumKineticEnergyJoules,
    double ExitKineticEnergyJoules);

public sealed record PreApexScrubProfilePoint(
    string CandidateId,
    int CornerNumber,
    float CornerProgress,
    float ActualSpeedMetersPerSecond,
    float EnvelopeSpeedMetersPerSecond,
    float DriveAvailability,
    float ScrubWindow,
    bool ScrubApplied,
    float ScrubAccelerationMetersPerSecondSquared,
    float ScrubForceNewtons,
    float ProductionSignedDriveAccelerationMetersPerSecondSquared,
    float FinalAccelerationMetersPerSecondSquared,
    ContinuousCornerPhaseClassification PhaseClassification,
    float ElapsedCornerTimeSeconds);

public sealed record PreApexScrubPartitionControl(
    int CornerNumber,
    float LastRawNodeAtOrBeforeWindowStartProgress,
    float LastRawNodeAtOrBeforeWindowStartSpeedMetersPerSecond,
    float FirstRawNodeWithPositiveWindowProgress,
    float FirstRawNodeWithPositiveWindowSpeedMetersPerSecond,
    float? FirstActualScrubProgress,
    float? FirstActualScrubDistanceMeters,
    float CorrectionDistanceBeforeFirstScrubMeters);

public sealed record PreApexScrubHeatObservation(
    string CandidateId,
    string ScenarioId,
    float SkillSpeed,
    float SkillSlideControl,
    float Gearing,
    float LateralPosition,
    double ModeledFourLapDistanceMeters,
    double VmaxKilometersPerHour,
    double FlyingLapMedianSeconds,
    double HeatTimeSeconds,
    double AverageSpeedMetersPerSecond,
    double StraightPeakSpeedMetersPerSecond,
    double CornerEntrySpeedMetersPerSecond,
    double TrueApexSpeedMetersPerSecond,
    double MinimumSpeedMetersPerSecond,
    double MinimumSpeedCornerProgress,
    int MinimumSpeedCornerNumber,
    double CornerExitSpeedMetersPerSecond,
    double PeakToTrueApexAmplitudeMetersPerSecond,
    double PeakToMinimumAmplitudeMetersPerSecond,
    double EntryToTrueApexDeltaSpeedMetersPerSecond,
    double TrueApexToExitDeltaSpeedMetersPerSecond,
    int BrakeCount,
    int RunWideCount,
    int CrashCount,
    bool AnyZeroSpeedEvent,
    PreApexScrubTimeBudget TimeBudget,
    PreApexScrubDistanceBuckets DistanceBuckets,
    PreApexScrubEnergyDiagnostic Energy,
    float? FirstCorrectionProgress,
    float? LastCorrectionProgress,
    float? FirstScrubProgress,
    float? LastScrubProgress,
    IReadOnlyList<PreApexScrubPartitionControl> PartitionControl,
    IReadOnlyList<PreApexScrubProfilePoint> ProfilePoints,
    CalibrationTrace Trace);

public sealed record IsolatedPreApexScrubObservation(
    string CandidateId,
    float EntrySpeedMetersPerSecond,
    float TrueApexSpeedMetersPerSecond,
    float MinimumSpeedMetersPerSecond,
    float MinimumSpeedCornerProgress,
    float ExitSpeedMetersPerSecond,
    float EntryToApexTimeSeconds,
    float ApexToExitTimeSeconds,
    float TotalTimeSeconds,
    PreApexScrubDistanceBuckets DistanceBuckets,
    PreApexScrubEnergyDiagnostic Energy,
    float? FirstCorrectionProgress,
    float? LastCorrectionProgress,
    float? FirstScrubProgress,
    float? LastScrubProgress,
    IReadOnlyList<PreApexScrubProfilePoint> ProfilePoints);

public sealed record PreApexScrubSurfaceObservation(
    string CandidateId,
    string SurfaceId,
    PreApexScrubHeatObservation Observation);

public sealed record PreApexScrubFreezeResult(
    bool ProductionDefaultExact,
    bool S0ExactProduction,
    bool ZeroForceExperimentalPathExact,
    bool EnvelopeExact,
    bool CorrectionCapabilityExact,
    bool CorrectionTargetExact,
    bool StandingStartExact,
    bool StraightExact,
    bool CornerResistanceExperimentAbsent,
    bool LegacyExact,
    bool SegmentPhysicsThresholdsExact,
    bool FullProductionScenarioSuiteExact,
    double ReactionTimeSeconds,
    double TimeTo70KphSeconds,
    double SpeedAtTwoSecondsKilometersPerHour);

public sealed class PreApexScrubLossExperimentResult
{
    public IReadOnlyList<PreApexScrubCandidate> Candidates { get; }
    public IReadOnlyList<PreApexScrubHeatObservation> Primary { get; }
    public IReadOnlyList<IsolatedPreApexScrubObservation> Isolated { get; }
    public IReadOnlyList<PreApexScrubHeatObservation> DistanceMatched { get; }
    public IReadOnlyList<PreApexScrubHeatObservation> LineSweep { get; }
    public IReadOnlyList<PreApexScrubHeatObservation> SpeedSweep { get; }
    public IReadOnlyList<PreApexScrubHeatObservation> SlideControlSweep { get; }
    public IReadOnlyList<PreApexScrubSurfaceObservation> SurfaceSensitivity { get; }
    public IReadOnlyList<PreApexScrubHeatObservation> Extreme { get; }
    public IReadOnlyDictionary<string, int> PermutationDistinctTraceHashes { get; }
    public PreApexScrubFreezeResult Freeze { get; }
    public string Classification { get; }
    public string NextSubsystem { get; }

    internal PreApexScrubLossExperimentResult(
        IEnumerable<PreApexScrubCandidate> candidates,
        IEnumerable<PreApexScrubHeatObservation> primary,
        IEnumerable<IsolatedPreApexScrubObservation> isolated,
        IEnumerable<PreApexScrubHeatObservation> distanceMatched,
        IEnumerable<PreApexScrubHeatObservation> lineSweep,
        IEnumerable<PreApexScrubHeatObservation> speedSweep,
        IEnumerable<PreApexScrubHeatObservation> slideControlSweep,
        IEnumerable<PreApexScrubSurfaceObservation> surfaceSensitivity,
        IEnumerable<PreApexScrubHeatObservation> extreme,
        IReadOnlyDictionary<string, int> permutationDistinctTraceHashes,
        PreApexScrubFreezeResult freeze,
        string classification,
        string nextSubsystem)
    {
        Candidates = ReadOnly(candidates);
        Primary = ReadOnly(primary);
        Isolated = ReadOnly(isolated);
        DistanceMatched = ReadOnly(distanceMatched);
        LineSweep = ReadOnly(lineSweep);
        SpeedSweep = ReadOnly(speedSweep);
        SlideControlSweep = ReadOnly(slideControlSweep);
        SurfaceSensitivity = ReadOnly(surfaceSensitivity);
        Extreme = ReadOnly(extreme);
        PermutationDistinctTraceHashes = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(permutationDistinctTraceHashes, StringComparer.Ordinal));
        Freeze = freeze;
        Classification = classification;
        NextSubsystem = nextSubsystem;
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}

/// <summary>
/// Bounded #43 diagnostic. Complete heats retain the production simulator and
/// inject exactly one immutable calibration-only pre-apex loss value.
/// </summary>
public static class PreApexScrubLossExperiment
{
    public const int FixedSeed = MotoarenaMatchedVenueCalibration.FixedSeed;
    public const int HeatId = 43;
    public const string BaseMainSha = "93ea9a1f21b1d7f92267abdb36c2f9085c5eeac2";
    public const float PrimaryLateralPosition = 1f;
    public const float DistanceMatchedLateralPosition = 1.11065f;
    public const float IsolatedEntrySpeedMetersPerSecond = 27.098435f;
    public const double CornerResistanceR100ExitPenaltyMetersPerSecond = .583672d;
    public const int RiderPermutationCount = 24;

    private static readonly float[] ProfileProgress =
        { 0f, .10f, .20f, .30f, .40f, .50f, .625f, .75f, .875f, 1f };
    private static readonly float[] SpeedSkills = { 0f, 25f, 50f, 75f, 100f };
    private static readonly float[] SlideSkills = { 0f, 50f, 100f };

    public static IReadOnlyList<PreApexScrubCandidate> CandidateMenu { get; } =
        Array.AsReadOnly(new[]
        {
            new PreApexScrubCandidate("S0", 0f),
            new PreApexScrubCandidate("S025", .25f),
            new PreApexScrubCandidate("S050", .50f),
            new PreApexScrubCandidate("S075", .75f),
            new PreApexScrubCandidate("S100", 1f),
        });

    public static float ScrubWindow(float progress) => PreApexScrubLossAdjustment.Window(progress);

    public static PreApexScrubLossExperimentResult Run()
    {
        var candidates = CandidateMenu;
        var primary = candidates.Select(candidate => RunFixedLineOneFourRider(
            candidate, $"primary/{candidate.Id}", Skills(), BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface)).ToArray();
        var isolated = candidates.Select(RunIsolatedCorner).ToArray();
        var edges = candidates.Where(item => item.Id is "S0" or "S100").ToArray();

        var distanceMatched = edges.Select(candidate => RunSingle(
            candidate, $"distance-matched/{candidate.Id}", Skills(), BikeSetup.Neutral,
            DistanceMatchedLateralPosition, CalibrationScenarioCatalog.Baseline.Surface)).ToArray();
        var lines = edges.SelectMany(candidate => Enumerable.Range(0, LaneModel.LanesCount)
            .Select(line => RunSingle(candidate, $"line/{candidate.Id}/L{line}", Skills(),
                BikeSetup.Neutral, line, CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var speed = edges.SelectMany(candidate => SpeedSkills.Select(value => RunFixedLineOneFourRider(
            candidate, $"speed/{candidate.Id}/{Id(value)}", Skills(speed: value), BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var slide = edges.SelectMany(candidate => SlideSkills.Select(value => RunFixedLineOneFourRider(
            candidate, $"slide/{candidate.Id}/{Id(value)}", Skills(slideControl: value), BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var surfaces = edges.SelectMany(candidate => CalibrationScenarioCatalog.Surfaces.Select(surface =>
            new PreApexScrubSurfaceObservation(candidate.Id, surface.Id, RunFixedLineOneFourRider(
                candidate, $"surface/{candidate.Id}/{surface.Id}", Skills(), BikeSetup.Neutral,
                surface.Surface)))).ToArray();
        var bestSurface = CalibrationScenarioCatalog.Surfaces
            .OrderByDescending(item => item.Surface.EffectiveGrip)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .First().Surface;
        var extreme = edges.Select(candidate => RunSingle(candidate, $"extreme/{candidate.Id}",
            Skills(speed: 100f, slideControl: 100f), new BikeSetup(1f, .5f), 4f, bestSurface)).ToArray();

        var permutationHashes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var candidate in edges)
        {
            var permutations = Permutations(new[] { 1, 2, 3, 4 }).ToArray();
            if (permutations.Length != RiderPermutationCount)
                throw new InvalidOperationException("The rider-order probe must contain all 24 permutations.");
            var distinct = permutations.Select(order => TraceHash(RunHeat(
                candidate,
                $"permutation/{candidate.Id}",
                order.Select(id => new RiderFixture(id, id - 1, Skills(), BikeSetup.Neutral)),
                CalibrationScenarioCatalog.Baseline.Surface))).Distinct(StringComparer.Ordinal).Count();
            permutationHashes.Add(candidate.Id, distinct);
        }

        var baseline = primary.Single(item => item.CandidateId == "S0");
        var full = primary.Single(item => item.CandidateId == "S100");
        var productionTrace = RunHeat(null, "production/default",
            Enumerable.Range(1, 4).Select(id => new RiderFixture(id, id - 1, Skills(), BikeSetup.Neutral)),
            CalibrationScenarioCatalog.Baseline.Surface);
        var s0Exact = TraceFingerprint(productionTrace) == TraceFingerprint(baseline.Trace);
        var envelopeExact = isolated.Select(item => string.Join(";", item.ProfilePoints.Select(point =>
                string.Join("|", F(point.CornerProgress), F(point.EnvelopeSpeedMetersPerSecond)))))
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var primaryEnvelope = CreatePrimaryEnvelope();
        var productionTraversal = primaryEnvelope.Traverse(
            IsolatedEntrySpeedMetersPerSecond, 0f, primaryEnvelope.TotalLengthMeters);
        var zeroForceExperimentalTraversal =
            primaryEnvelope.TraverseWithZeroForcePreApexScrubLossExperimentPath(
                IsolatedEntrySpeedMetersPerSecond, 0f, primaryEnvelope.TotalLengthMeters);
        var zeroForceExperimentalPathExact = TraversalKinematicFingerprint(productionTraversal)
            == TraversalKinematicFingerprint(zeroForceExperimentalTraversal);
        var correctionCapabilityExact = candidates.Select(_ => F(primaryEnvelope.CorrectionCapabilityMetersPerSecondSquared))
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var correctionTargetExact = candidates.Select(_ => string.Join(";", ProfileProgress.Select(progress =>
                F(primaryEnvelope.SpeedMetersPerSecond(progress)))))
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var launch = primary.Select(item => item.Trace.StepSamples.Single(sample =>
            sample.RiderId == 2 && sample.LapIndex == 0 && sample.SegmentIndex == 0)).ToArray();
        var standingExact = launch.Select(item => string.Join("|",
                F(item.StandingStartReactionTimeSeconds), F(item.StandingStartTimeTo70KphSeconds),
                F(item.StandingStartSpeedAtTwoSecondsMetersPerSecond)))
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var straightExact = candidates.Select(StraightFingerprint).Distinct(StringComparer.Ordinal).Count() == 1;
        var legacyExact = candidates.Select(LegacyFingerprint).Distinct(StringComparer.Ordinal).Count() == 1;
        var thresholdExact = candidates.Select(SegmentPhysicsFingerprint).Distinct(StringComparer.Ordinal).Count() == 1;
        var suiteExact = ScenarioSuiteFingerprint(CalibrationScenarioSuite.RunRequired())
            == ScenarioSuiteFingerprint(CalibrationScenarioSuite.RunRequiredWithZeroPreApexScrubExperiment());
        var cornerResistanceAbsent = AllProfiles(primary).All(profile => profile.ReducedDriveResistanceExposure == 0f);
        var freeze = new PreApexScrubFreezeResult(
            s0Exact, s0Exact, zeroForceExperimentalPathExact, envelopeExact,
            correctionCapabilityExact, correctionTargetExact,
            standingExact, straightExact, cornerResistanceAbsent, legacyExact, thresholdExact, suiteExact,
            launch[0].StandingStartReactionTimeSeconds!.Value,
            launch[0].StandingStartTimeTo70KphSeconds!.Value,
            CalibrationUnits.MetersPerSecondToKph(
                launch[0].StandingStartSpeedAtTwoSecondsMetersPerSecond!.Value));

        var flyingDelta = full.FlyingLapMedianSeconds - baseline.FlyingLapMedianSeconds;
        var trueApexDelta = full.TrueApexSpeedMetersPerSecond - baseline.TrueApexSpeedMetersPerSecond;
        var exitPenalty = baseline.CornerExitSpeedMetersPerSecond - full.CornerExitSpeedMetersPerSecond;
        var preDelta = full.TimeBudget.TotalPreApexTimeSeconds - baseline.TimeBudget.TotalPreApexTimeSeconds;
        var postDelta = full.TimeBudget.TotalPostApexTimeSeconds - baseline.TimeBudget.TotalPostApexTimeSeconds;
        var minimumNearApex = Math.Abs(full.MinimumSpeedCornerProgress - .5d) <= .10d;
        var pathological = full.AnyZeroSpeedEvent || !double.IsFinite(full.FlyingLapMedianSeconds)
            || speed.GroupBy(item => item.CandidateId).Any(group => !StrictlyOrderedBySpeedSkill(group))
            || slide.GroupBy(item => item.CandidateId).Any(group => !StrictlyOrderedBySlideSkill(group));
        var conservativeDistanceSmall = full.DistanceBuckets.ScrubDistanceMeters
            < full.DistanceBuckets.TravelledDistanceMeters * .01d;
        var classification = pathological || exitPenalty >= 2d
            ? "ScrubLossOverstatesDissipation"
            : conservativeDistanceSmall || flyingDelta <= .01d
                ? "ConservativeScrubDistanceInsufficient"
                : !minimumNearApex
                    ? "RecoveryProfileDominatesResidual"
                    : trueApexDelta < 0d && preDelta > postDelta
                        && exitPenalty < CornerResistanceR100ExitPenaltyMetersPerSecond
                        ? "PreApexScrubLossPlausiblyRelevant"
                        : "RecoveryProfileDominatesResidual";
        var nextSubsystem = classification switch
        {
            "PreApexScrubLossPlausiblyRelevant" =>
                "combined straight-peak and pre-apex-loss validation",
            "ConservativeScrubDistanceInsufficient" =>
                "slip-loss integration with active correction",
            "RecoveryProfileDominatesResidual" =>
                "post-apex drive recovery shape",
            _ => "lower bounded pre-apex dissipation",
        };

        return new PreApexScrubLossExperimentResult(
            candidates, primary, isolated, distanceMatched, lines, speed, slide, surfaces,
            extreme, permutationHashes, freeze, classification, nextSubsystem);
    }

    private static PreApexScrubHeatObservation RunFixedLineOneFourRider(
        PreApexScrubCandidate candidate,
        string scenarioId,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        var trace = RunHeat(candidate, scenarioId,
            Enumerable.Range(1, 4).Select(id => new RiderFixture(id, id - 1, skills, setup)), surface);
        return ObserveRider(candidate, scenarioId, trace, 2, PrimaryLateralPosition);
    }

    private static PreApexScrubHeatObservation RunSingle(
        PreApexScrubCandidate candidate,
        string scenarioId,
        RiderSkills skills,
        BikeSetup setup,
        float lateralPosition,
        TrackSurfaceState surface)
    {
        var trace = RunHeat(candidate, scenarioId,
            new[] { new RiderFixture(1, lateralPosition, skills, setup) }, surface);
        return ObserveRider(candidate, scenarioId, trace, 1, lateralPosition);
    }

    private static CalibrationTrace RunHeat(
        PreApexScrubCandidate? candidate,
        string scenarioId,
        IEnumerable<RiderFixture> fixtures,
        TrackSurfaceState surface)
    {
        var fixtureArray = fixtures.ToArray();
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaPrimaryStartLineToFirstCornerMeters);
        var riders = fixtureArray.Select(item => new RiderState(
            new RiderProfile(item.RiderId, $"#43 {scenarioId} rider {item.RiderId}", item.Skills,
                RiderStyle.Balanced), (int)MathF.Round(item.LateralPosition))
        {
            LateralPosition = item.LateralPosition,
            ActiveSetup = item.Setup,
        }).ToList();
        var options = new HeatSimulationOptions
        {
            Laps = 4,
            Seed = FixedSeed,
            Weather = WeatherState.Dry,
            IncidentFrequency = 0f,
            EnableLogging = false,
            StraightDriveEnvelopeAdjustment = null,
            CornerReducedDriveResistanceAdjustment = null,
            PreApexScrubLossAdjustment = candidate is null
                ? null
                : new PreApexScrubLossAdjustment(
                    candidate.PeakScrubDecelerationMetersPerSecondSquared),
        };
        return CalibrationRunner.RunHeat(track, TrackState.CreateDefault(track, surface), riders,
            new HoldLaneDecisionModel(), options, HeatId);
    }

    private static PreApexScrubHeatObservation ObserveRider(
        PreApexScrubCandidate candidate,
        string scenarioId,
        CalibrationTrace trace,
        int riderId,
        float modeledLateralPosition)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaPrimaryStartLineToFirstCornerMeters);
        var rider = CalibrationSkillSweep.ObserveRiders(trace).Single(item => item.RiderId == riderId);
        var samples = trace.StepSamples.Where(item => item.RiderId == riderId).ToArray();
        var flying = samples.Where(item => item.LapIndex == 1).OrderBy(item => item.SegmentIndex).ToArray();
        var cornerSeries = BuildCornerSeries(candidate.Id, flying);
        var partitionControl = BuildPartitionControl(flying);
        var profilePoints = cornerSeries.SelectMany(item => item.Points).ToArray();
        var profiles = flying.Where(item => item.ContinuousCornerProfile is not null)
            .Select(item => item.ContinuousCornerProfile!).ToArray();
        var rawNodes = profiles.SelectMany(item => item.Nodes).ToArray();
        var trueApex = profilePoints.Where(item => item.CornerProgress == .5f)
            .Average(item => item.ActualSpeedMetersPerSecond);
        var entry = profilePoints.Where(item => item.CornerProgress == 0f)
            .Average(item => item.ActualSpeedMetersPerSecond);
        var exit = profilePoints.Where(item => item.CornerProgress == 1f)
            .Average(item => item.ActualSpeedMetersPerSecond);
        var straightPeak = flying.Where(item => item.SegmentType == SegmentType.Straight)
            .Max(item => item.PeakSpeedMetersPerSecond);
        var minimumCorner = cornerSeries.Select((series, index) => new
            {
                CornerNumber = index + 1,
                series.MinimumSpeedMetersPerSecond,
                series.MinimumSpeedCornerProgress,
            })
            .OrderBy(item => item.MinimumSpeedMetersPerSecond)
            .ThenBy(item => item.CornerNumber)
            .First();
        var correction = profiles.Sum(item => (double)item.CorrectionDistanceMeters);
        var scrub = profiles.Sum(item => (double)item.ScrubDistanceMeters);
        var positive = profiles.Sum(item => (double)item.PositiveDriveDistanceMeters);
        var negative = profiles.Sum(item => (double)item.NegativeSignedDriveDistanceMeters);
        var neutral = profiles.Sum(item => (double)item.NeutralCarryDistanceMeters);
        var travelled = profiles.Sum(item => (double)item.CorrectionDistanceMeters
            + item.CarryDistanceMeters + item.DriveDistanceMeters);
        var scrubZero = profiles.Sum(item => (double)item.ScrubWhileDriveZeroDistanceMeters);
        var scrubPositive = profiles.Sum(item => (double)item.ScrubWhileDrivePositiveDistanceMeters);
        var bucketSum = correction + scrub + positive + negative + neutral;
        var buckets = new PreApexScrubDistanceBuckets(
            travelled, correction, scrub, positive, negative, neutral, scrubZero, scrubPositive,
            Math.Abs(bucketSum - travelled) <= Math.Max(1e-5d, travelled * 1e-5d));
        double Ke(double speed) => .5d * LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
            * speed * speed;
        var energy = new PreApexScrubEnergyDiagnostic(
            profiles.Sum(item => item.ScrubWorkJoules), Ke(entry), Ke(trueApex),
            Ke(minimumCorner.MinimumSpeedMetersPerSecond), Ke(exit));
        return new PreApexScrubHeatObservation(
            candidate.Id,
            scenarioId,
            samples[0].RiderSpeedSkill,
            samples[0].RiderSlideControlSkill,
            samples[0].Gearing,
            modeledLateralPosition,
            MathF.Abs(modeledLateralPosition - MathF.Round(modeledLateralPosition)) <= 1e-6f
                ? rider.TotalDistanceMeters
                : 4d * MotoarenaMatchedVenueCalibration.LapDistance(track, modeledLateralPosition),
            CalibrationUnits.MetersPerSecondToKph(rider.MaximumSpeedMetersPerSecond),
            rider.FlyingLapMedianSeconds ?? throw new InvalidOperationException("Completed heat has no flying lap."),
            rider.TotalTimeSeconds,
            rider.AverageSpeedMetersPerSecond ?? throw new InvalidOperationException("Completed heat has no average speed."),
            straightPeak,
            entry,
            trueApex,
            minimumCorner.MinimumSpeedMetersPerSecond,
            minimumCorner.MinimumSpeedCornerProgress,
            minimumCorner.CornerNumber,
            exit,
            straightPeak - trueApex,
            straightPeak - minimumCorner.MinimumSpeedMetersPerSecond,
            trueApex - entry,
            exit - trueApex,
            rider.BrakeCount,
            rider.RunWideCount,
            rider.CrashCount,
            rawNodes.Any(item => item.SpeedMetersPerSecond <= 0f),
            BuildTimeBudget(flying, cornerSeries),
            buckets,
            energy,
            MinNullable(profiles.Select(item => item.FirstCorrectionProgress)),
            MaxNullable(profiles.Select(item => item.LastCorrectionProgress)),
            MinNullable(profiles.Select(item => item.FirstScrubProgress)),
            MaxNullable(profiles.Select(item => item.LastScrubProgress)),
            partitionControl,
            Array.AsReadOnly(profilePoints),
            trace);
    }

    private static PreApexScrubTimeBudget BuildTimeBudget(
        IReadOnlyList<CalibrationStepSample> flying,
        IReadOnlyList<CornerSeries> corners)
    {
        double StraightAt(int index) => flying.Single(item => item.SegmentIndex == index).DurationSeconds;
        var first = corners[0];
        var second = corners[1];
        var homeStart = StraightAt(0);
        var back = StraightAt(4);
        var homeFinish = StraightAt(8);
        var totalStraight = homeStart + back + homeFinish;
        var totalCorner = first.TotalTimeSeconds + second.TotalTimeSeconds;
        return new PreApexScrubTimeBudget(
            homeStart, back, homeFinish, totalStraight,
            first.EntryToApexTimeSeconds, first.ApexToExitTimeSeconds, first.TotalTimeSeconds,
            second.EntryToApexTimeSeconds, second.ApexToExitTimeSeconds, second.TotalTimeSeconds,
            totalCorner, flying.Sum(item => (double)item.DurationSeconds));
    }

    private static IReadOnlyList<CornerSeries> BuildCornerSeries(
        string candidateId,
        IReadOnlyList<CalibrationStepSample> flying)
    {
        var groups = flying.Where(item => item.CornerPhase.HasValue)
            .GroupBy(item => item.CornerPhase!.Value.CornerId)
            .OrderBy(item => item.Key)
            .ToArray();
        var result = new List<CornerSeries>();
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var elapsedOffset = 0f;
            var nodes = new List<ContinuousCornerNode>();
            foreach (var sample in groups[groupIndex].OrderBy(item => item.SegmentIndex))
            {
                var profile = sample.ContinuousCornerProfile
                    ?? throw new InvalidOperationException("Corner sample has no traversal profile.");
                foreach (var node in profile.Nodes)
                    nodes.Add(node with { ElapsedTimeSeconds = elapsedOffset + node.ElapsedTimeSeconds });
                elapsedOffset += profile.TravelTimeSeconds;
            }
            var points = ProfileProgress.Select(progress => Interpolate(
                candidateId, groupIndex + 1, nodes, progress)).ToArray();
            var apexTime = points.Single(item => item.CornerProgress == .5f).ElapsedCornerTimeSeconds;
            var minimum = nodes.OrderBy(item => item.SpeedMetersPerSecond)
                .ThenBy(item => item.CornerProgress).First();
            result.Add(new CornerSeries(Array.AsReadOnly(points), apexTime,
                elapsedOffset - apexTime, elapsedOffset,
                minimum.SpeedMetersPerSecond, minimum.CornerProgress));
        }
        if (result.Count != 2)
            throw new InvalidOperationException("Motoarena fixture must contain two logical corners.");
        return Array.AsReadOnly(result.ToArray());
    }

    private static IReadOnlyList<PreApexScrubPartitionControl> BuildPartitionControl(
        IReadOnlyList<CalibrationStepSample> flying)
    {
        var groups = flying.Where(item => item.CornerPhase.HasValue)
            .GroupBy(item => item.CornerPhase!.Value.CornerId)
            .OrderBy(item => item.Key)
            .ToArray();
        var result = new List<PreApexScrubPartitionControl>();
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var profiles = groups[groupIndex].OrderBy(item => item.SegmentIndex)
                .Select(item => item.ContinuousCornerProfile
                    ?? throw new InvalidOperationException("Corner sample has no traversal profile."))
                .ToArray();
            var nodes = profiles.SelectMany(item => item.Nodes)
                .OrderBy(item => item.CornerProgress)
                .ThenBy(item => item.ElapsedTimeSeconds)
                .ToArray();
            var lastPreWindow = nodes.Where(item => item.CornerProgress <= .10f)
                .OrderByDescending(item => item.CornerProgress)
                .ThenByDescending(item => item.ElapsedTimeSeconds)
                .First();
            var firstPositiveWindow = nodes.First(item =>
                PreApexScrubLossAdjustment.Window(item.CornerProgress) > 0f);
            float? firstScrubProgress = null;
            float? firstScrubDistance = null;
            var correctionBeforeFirstScrub = 0f;
            var accumulatedCorrection = 0f;
            foreach (var profile in profiles)
            {
                if (profile.FirstScrubProgress is not null)
                {
                    firstScrubProgress = profile.FirstScrubProgress;
                    firstScrubDistance = profile.FirstScrubDistanceMeters;
                    correctionBeforeFirstScrub = accumulatedCorrection
                        + profile.CorrectionDistanceBeforeFirstScrubMeters;
                    break;
                }
                accumulatedCorrection += profile.CorrectionDistanceMeters;
            }
            result.Add(new PreApexScrubPartitionControl(
                groupIndex + 1,
                lastPreWindow.CornerProgress,
                lastPreWindow.SpeedMetersPerSecond,
                firstPositiveWindow.CornerProgress,
                firstPositiveWindow.SpeedMetersPerSecond,
                firstScrubProgress,
                firstScrubDistance,
                correctionBeforeFirstScrub));
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static PreApexScrubProfilePoint Interpolate(
        string candidateId,
        int cornerNumber,
        IReadOnlyList<ContinuousCornerNode> nodes,
        float progress)
    {
        var ordered = nodes.OrderBy(item => item.CornerProgress)
            .ThenBy(item => item.ElapsedTimeSeconds).ToArray();
        var upper = Array.FindIndex(ordered, item => item.CornerProgress >= progress);
        if (upper < 0) upper = ordered.Length - 1;
        var a = upper == 0 ? ordered[0] : ordered[upper - 1];
        var b = ordered[upper];
        var span = b.CornerProgress - a.CornerProgress;
        var t = span <= 1e-7f ? 0f : (progress - a.CornerProgress) / span;
        float L(float x, float y) => x + (y - x) * t;
        var diagnosticNode = progress == 0f && ordered.Length > 1 ? ordered[1] : b;
        var phase = diagnosticNode.PhaseClassification;
        var envelope = L(a.EnvelopeSpeedMetersPerSecond, b.EnvelopeSpeedMetersPerSecond);
        if (progress <= ContinuousCornerEnvelope.ApexProgress)
        {
            envelope = (float)Math.Sqrt(
                (double)b.EnvelopeApexSpeedMetersPerSecond * b.EnvelopeApexSpeedMetersPerSecond
                + 2d * b.EnvelopeCorrectionCapabilityMetersPerSecondSquared
                * (ContinuousCornerEnvelope.ApexProgress - progress)
                * b.EnvelopeTotalLengthMeters);
        }
        return new PreApexScrubProfilePoint(
            candidateId, cornerNumber, progress,
            L(a.SpeedMetersPerSecond, b.SpeedMetersPerSecond),
            envelope,
            L(a.DriveAvailability, b.DriveAvailability),
            PreApexScrubLossAdjustment.Window(progress),
            diagnosticNode.ScrubApplied,
            diagnosticNode.ScrubAccelerationMetersPerSecondSquared,
            diagnosticNode.ScrubForceNewtons,
            L(a.ProductionSignedDriveAccelerationMetersPerSecondSquared,
                b.ProductionSignedDriveAccelerationMetersPerSecondSquared),
            L(a.FinalAccelerationMetersPerSecondSquared, b.FinalAccelerationMetersPerSecondSquared),
            phase,
            L(a.ElapsedTimeSeconds, b.ElapsedTimeSeconds));
    }

    private static IsolatedPreApexScrubObservation RunIsolatedCorner(PreApexScrubCandidate candidate)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaPrimaryStartLineToFirstCornerMeters);
        var phase = track.CornerTopology.Resolve(1, 0f, PrimaryLateralPosition, track.Geometry)
            ?? throw new InvalidOperationException("Motoarena first corner has no phase context.");
        var envelope = ContinuousCornerEnvelope.Create(
            phase, PrimaryLateralPosition, track.Geometry,
            CalibrationScenarioCatalog.Baseline.Surface, Skills(), BikeSetup.Neutral);
        var profile = envelope.TraverseWithPreApexScrubLoss(
            IsolatedEntrySpeedMetersPerSecond,
            0f,
            phase.TotalCornerLengthMeters,
            new PreApexScrubLossAdjustment(candidate.PeakScrubDecelerationMetersPerSecondSquared));
        var points = ProfileProgress.Select(progress => Interpolate(
            candidate.Id, 1, profile.Nodes, progress)).ToArray();
        var entry = points[0].ActualSpeedMetersPerSecond;
        var apexPoint = points.Single(item => item.CornerProgress == .5f);
        var apex = apexPoint.ActualSpeedMetersPerSecond;
        var exit = points[^1].ActualSpeedMetersPerSecond;
        var minimum = profile.Nodes.OrderBy(item => item.SpeedMetersPerSecond)
            .ThenBy(item => item.CornerProgress).First();
        var correction = profile.CorrectionDistanceMeters;
        var scrub = profile.ScrubDistanceMeters;
        var positive = profile.PositiveDriveDistanceMeters;
        var negative = profile.NegativeSignedDriveDistanceMeters;
        var neutral = profile.NeutralCarryDistanceMeters;
        var exclusiveSum = correction + scrub + positive + negative + neutral;
        var buckets = new PreApexScrubDistanceBuckets(
            phase.TotalCornerLengthMeters, correction, scrub, positive, negative, neutral,
            profile.ScrubWhileDriveZeroDistanceMeters,
            profile.ScrubWhileDrivePositiveDistanceMeters,
            MathF.Abs(exclusiveSum - phase.TotalCornerLengthMeters)
                <= MathF.Max(1e-5f, phase.TotalCornerLengthMeters * 1e-5f));
        double Ke(float speed) => .5d * LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
            * speed * speed;
        return new IsolatedPreApexScrubObservation(
            candidate.Id, entry, apex, minimum.SpeedMetersPerSecond, minimum.CornerProgress, exit,
            apexPoint.ElapsedCornerTimeSeconds,
            profile.TravelTimeSeconds - apexPoint.ElapsedCornerTimeSeconds,
            profile.TravelTimeSeconds,
            buckets,
            new PreApexScrubEnergyDiagnostic(profile.ScrubWorkJoules, Ke(entry), Ke(apex),
                Ke(minimum.SpeedMetersPerSecond), Ke(exit)),
            profile.FirstCorrectionProgress,
            profile.LastCorrectionProgress,
            profile.FirstScrubProgress,
            profile.LastScrubProgress,
            Array.AsReadOnly(points));
    }

    private static ContinuousCornerEnvelope CreatePrimaryEnvelope()
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaPrimaryStartLineToFirstCornerMeters);
        var phase = track.CornerTopology.Resolve(1, 0f, PrimaryLateralPosition, track.Geometry)
            ?? throw new InvalidOperationException("Motoarena first corner has no phase context.");
        return ContinuousCornerEnvelope.Create(phase, PrimaryLateralPosition, track.Geometry,
            CalibrationScenarioCatalog.Baseline.Surface, Skills(), BikeSetup.Neutral);
    }

    private static string StraightFingerprint(PreApexScrubCandidate candidate)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var rider = new RiderState(new RiderProfile(1, "straight freeze", Skills(), RiderStyle.Balanced), 1)
        {
            LateralPosition = 1f,
            Speed = 24f,
            ActiveSetup = BikeSetup.Neutral,
        };
        rider.RestorePosition(RiderPosition.Create(1, 4, 0f, track.Segments.Count));
        var profile = ResolveOne(track, rider, 4, candidate, useLegacy: false)
            .Diagnostics.Single().StraightProfile!.Value;
        return string.Join("|", F(profile.ExitSpeedMetersPerSecond), F(profile.PeakSpeedMetersPerSecond),
            F(profile.TravelTimeSeconds), F(profile.AccelerationDistanceMeters),
            F(profile.CruiseDistanceMeters), F(profile.DecelerationDistanceMeters));
    }

    private static string LegacyFingerprint(PreApexScrubCandidate candidate)
    {
        var track = Track.CreateExample();
        var rider = new RiderState(new RiderProfile(1, "legacy freeze", Skills(), RiderStyle.Balanced), 1)
        {
            Speed = 19f,
            ActiveSetup = BikeSetup.Neutral,
        };
        var change = ResolveOne(track, rider, 0, candidate, useLegacy: true).Changes.Single();
        return string.Join("|", change.Outcome, change.Lane, F(change.Speed), F(change.ElapsedTimeSeconds));
    }

    private static string SegmentPhysicsFingerprint(PreApexScrubCandidate candidate)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var phase = track.CornerTopology.Resolve(1, 0f, 1f, track.Geometry)!.Value;
        var envelope = ContinuousCornerEnvelope.Create(phase, 1f, track.Geometry,
            CalibrationScenarioCatalog.Baseline.Surface, Skills(), BikeSetup.Neutral);
        return string.Join(";", new[] { .99f, 1.02f, 1.08f, 1.22f }.Select(multiplier =>
        {
            var rider = new RiderState(new RiderProfile(1, "threshold freeze", Skills(), RiderStyle.Balanced), 1)
            {
                LateralPosition = 1f,
                Speed = envelope.SpeedMetersPerSecond(0f) * multiplier,
                ActiveSetup = BikeSetup.Neutral,
            };
            rider.RestorePosition(RiderPosition.Create(1, 1, 0f, track.Segments.Count));
            var change = ResolveOne(track, rider, 1, candidate, useLegacy: false).Changes.Single();
            return string.Join(",", change.Outcome, change.Lane);
        }));
    }

    private static ResolvedSimulationStep ResolveOne(
        Track track,
        RiderState rider,
        int segmentIndex,
        PreApexScrubCandidate candidate,
        bool useLegacy)
    {
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var snapshot = engine.CaptureSnapshot(track,
            TrackState.CreateDefault(track, CalibrationScenarioCatalog.Baseline.Surface),
            new[] { rider },
            new SimulationStepContext(HeatId, 0, rider.LapsCompleted, segmentIndex, FixedSeed, 4,
                UseLegacyPhysics: useLegacy));
        return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions
        {
            Laps = 4,
            Seed = FixedSeed,
            IncidentFrequency = 0f,
            EnableLogging = false,
            StraightDriveEnvelopeAdjustment = null,
            CornerReducedDriveResistanceAdjustment = null,
            PreApexScrubLossAdjustment = new PreApexScrubLossAdjustment(
                candidate.PeakScrubDecelerationMetersPerSecondSquared),
        });
    }

    private static IEnumerable<ContinuousCornerTraversalProfile> AllProfiles(
        IEnumerable<PreApexScrubHeatObservation> observations) => observations
        .SelectMany(observation => observation.Trace.StepSamples)
        .Where(sample => sample.ContinuousCornerProfile is not null)
        .Select(sample => sample.ContinuousCornerProfile!);

    private static string ScenarioSuiteFingerprint(IEnumerable<CalibrationScenarioResult> values)
    {
        var rows = values.OrderBy(item => item.Metadata.ScenarioId, StringComparer.Ordinal).Select(item => item switch
        {
            CalibrationStartResult start => string.Join("|", item.Metadata.ScenarioId, start.Profile),
            CalibrationStraightResult straight => string.Join("|", item.Metadata.ScenarioId, straight.Profile),
            CalibrationTurnResult turn => string.Join("|", item.Metadata.ScenarioId, turn.Change,
                CornerProfileFingerprint(turn.Diagnostics.ContinuousCornerProfile)),
            CalibrationHeatResult heat => string.Join("|", item.Metadata.ScenarioId,
                CalibrationCsvExporter.ExportSteps(heat.Trace), ClassificationFingerprint(heat.Trace)),
            CalibrationLineResult line => string.Join("|", item.Metadata.ScenarioId,
                CalibrationCsvExporter.ExportSteps(line.Trace), ClassificationFingerprint(line.Trace)),
            _ => throw new InvalidOperationException("Unknown calibration scenario result."),
        });
        return Hash(string.Join("\n", rows));
    }

    private static string CornerProfileFingerprint(ContinuousCornerTraversalProfile? profile)
        => profile is null ? "null" : string.Join("|", profile,
            string.Join(";", profile.Nodes.Select(node => string.Join(",",
                F(node.CornerProgress), F(node.SpeedMetersPerSecond), F(node.EnvelopeSpeedMetersPerSecond),
                F(node.DriveAvailability)))));

    private static string TraversalKinematicFingerprint(ContinuousCornerTraversalProfile profile) =>
        string.Join("|",
            F(profile.EntrySpeedMetersPerSecond),
            F(profile.ExitSpeedMetersPerSecond),
            F(profile.TravelTimeSeconds),
            F(profile.CorrectionDistanceMeters),
            F(profile.CarryDistanceMeters),
            F(profile.DriveDistanceMeters),
            string.Join(";", profile.Nodes.Select(node => string.Join(",",
                F(node.CornerProgress), F(node.SpeedMetersPerSecond),
                F(node.EnvelopeSpeedMetersPerSecond), F(node.ElapsedTimeSeconds)))));

    private static string ClassificationFingerprint(CalibrationTrace trace)
        => string.Join(";", trace.Classification.Select(item => string.Join(",", item.RiderId,
            item.Position, item.Status, F(item.TimeSeconds), F(item.DistanceMeters), item.LapsCompleted)));

    private static string TraceFingerprint(CalibrationTrace trace)
        => Hash(CalibrationCsvExporter.ExportSteps(trace) + "\n" + ClassificationFingerprint(trace));

    private static string TraceHash(CalibrationTrace trace) => TraceFingerprint(trace);

    private static string Hash(string payload) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    private static IEnumerable<int[]> Permutations(int[] values)
    {
        var copy = values.ToArray();
        return Generate(0);

        IEnumerable<int[]> Generate(int index)
        {
            if (index == copy.Length)
            {
                yield return copy.ToArray();
                yield break;
            }
            for (var swap = index; swap < copy.Length; swap++)
            {
                (copy[index], copy[swap]) = (copy[swap], copy[index]);
                foreach (var permutation in Generate(index + 1)) yield return permutation;
                (copy[index], copy[swap]) = (copy[swap], copy[index]);
            }
        }
    }

    private static bool StrictlyOrderedBySpeedSkill(IEnumerable<PreApexScrubHeatObservation> group)
    {
        var ordered = group.OrderBy(item => item.SkillSpeed).ToArray();
        return ordered.Zip(ordered.Skip(1)).All(pair =>
            pair.Second.VmaxKilometersPerHour > pair.First.VmaxKilometersPerHour
            && pair.Second.FlyingLapMedianSeconds < pair.First.FlyingLapMedianSeconds);
    }

    private static bool StrictlyOrderedBySlideSkill(IEnumerable<PreApexScrubHeatObservation> group)
    {
        var ordered = group.OrderBy(item => item.SkillSlideControl).ToArray();
        return ordered.Zip(ordered.Skip(1)).All(pair =>
            pair.Second.FlyingLapMedianSeconds < pair.First.FlyingLapMedianSeconds);
    }

    private static float? MinNullable(IEnumerable<float?> values)
    {
        var present = values.Where(item => item.HasValue).Select(item => item!.Value).ToArray();
        return present.Length == 0 ? null : present.Min();
    }

    private static float? MaxNullable(IEnumerable<float?> values)
    {
        var present = values.Where(item => item.HasValue).Select(item => item!.Value).ToArray();
        return present.Length == 0 ? null : present.Max();
    }

    private static RiderSkills Skills(float speed = 50f, float slideControl = 50f) =>
        new(50f, speed, slideControl, 50f, 50f, 50f);

    private static string Id(float value) => value.ToString("000", CultureInfo.InvariantCulture);
    private static string F(float? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "null";

    private sealed record RiderFixture(int RiderId, float LateralPosition, RiderSkills Skills, BikeSetup Setup);

    private sealed record CornerSeries(
        IReadOnlyList<PreApexScrubProfilePoint> Points,
        float EntryToApexTimeSeconds,
        float ApexToExitTimeSeconds,
        float TotalTimeSeconds,
        float MinimumSpeedMetersPerSecond,
        float MinimumSpeedCornerProgress);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane, 0f);
        public RiderDecision Decide(RiderDecisionContext context) => new(context.Rider.Lane, 0f);
    }
}
