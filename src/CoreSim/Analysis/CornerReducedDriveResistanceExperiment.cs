using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record CornerResistanceCandidate
{
    public string Id { get; }
    public float ReducedDriveResistanceExposure { get; }

    public CornerResistanceCandidate(string id, float reducedDriveResistanceExposure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!float.IsFinite(reducedDriveResistanceExposure)
            || reducedDriveResistanceExposure < 0f
            || reducedDriveResistanceExposure > 1f)
            throw new ArgumentOutOfRangeException(nameof(reducedDriveResistanceExposure),
                "Reduced-drive resistance exposure must be finite and in [0,1].");
        Id = id;
        ReducedDriveResistanceExposure = reducedDriveResistanceExposure;
    }
}

public sealed record CornerResistanceTimeBudget(
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
    double FlyingLapTimeSeconds);

public sealed record CornerResistanceDistanceBuckets(
    double TravelledDistanceMeters,
    double CorrectionDistanceMeters,
    double PassiveResistanceDistanceMeters,
    double PositiveDriveDistanceMeters,
    double NegativeSignedDriveDistanceMeters,
    double NeutralCarryDistanceMeters,
    double ZeroAvailabilityUncorrectedDistanceMeters,
    bool Conserved);

public sealed record CornerResistanceEnergyDiagnostic(
    double EntryKineticEnergyJoules,
    double ApexKineticEnergyJoules,
    double ExitKineticEnergyJoules,
    double EntryToApexDeltaJoules,
    double ApexToExitDeltaJoules);

public sealed record CornerResistanceProfilePoint(
    string CandidateId,
    int CornerNumber,
    float CornerProgress,
    float ActualSpeedMetersPerSecond,
    double ActualSpeedKilometersPerHour,
    float EnvelopeSpeedMetersPerSecond,
    float DriveAvailability,
    float AvailableDriveForceNewtons,
    float ResistanceForceNewtons,
    float ExposedResistanceForceNewtons,
    float NetForceNewtons,
    float NetAccelerationMetersPerSecondSquared,
    ContinuousCornerPhaseClassification PhaseClassification,
    float ElapsedCornerTimeSeconds);

public sealed record CornerResistanceHeatObservation(
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
    double CornerEntrySpeedMetersPerSecond,
    double ApexSpeedMetersPerSecond,
    double CornerExitSpeedMetersPerSecond,
    double StraightPeakSpeedMetersPerSecond,
    double PeakToApexAmplitudeMetersPerSecond,
    double PeakToMinimumAmplitudeMetersPerSecond,
    double EntryToApexDeltaSpeedMetersPerSecond,
    double ApexToExitDeltaSpeedMetersPerSecond,
    double MinimumSpeedMetersPerSecond,
    double MinimumSpeedCornerProgress,
    int MinimumSpeedCornerNumber,
    double FirstCornerMinimumSpeedMetersPerSecond,
    double FirstCornerMinimumSpeedCornerProgress,
    double SecondCornerMinimumSpeedMetersPerSecond,
    double SecondCornerMinimumSpeedCornerProgress,
    int BrakeCount,
    int RunWideCount,
    int CrashCount,
    bool AnyZeroSpeedEvent,
    CornerResistanceTimeBudget TimeBudget,
    CornerResistanceDistanceBuckets DistanceBuckets,
    IReadOnlyList<CornerResistanceProfilePoint> ProfilePoints,
    CalibrationTrace Trace);

public sealed record IsolatedCornerResistanceObservation(
    string CandidateId,
    float EntrySpeedMetersPerSecond,
    float ApexSpeedMetersPerSecond,
    float ExitSpeedMetersPerSecond,
    float PeakToApexAmplitudeMetersPerSecond,
    float EntryToApexDeltaSpeedMetersPerSecond,
    float ApexToExitDeltaSpeedMetersPerSecond,
    float EntryToApexTimeSeconds,
    float ApexToExitTimeSeconds,
    float TotalTimeSeconds,
    float? FirstCorrectionProgress,
    float? LastCorrectionProgress,
    float CorrectionDistanceMeters,
    float? TargetReachedProgress,
    CornerResistanceDistanceBuckets DistanceBuckets,
    CornerResistanceEnergyDiagnostic Energy,
    IReadOnlyList<CornerResistanceProfilePoint> ProfilePoints);

public sealed record CornerResistanceSurfaceObservation(
    string CandidateId,
    string SurfaceId,
    CornerResistanceHeatObservation Observation);

public sealed record CornerResistanceFreezeResult(
    bool ProductionDefaultExact,
    bool R0ExactProduction,
    bool R0ForceIdentity,
    bool AvailabilityOneExact,
    bool EnvelopeExact,
    bool CorrectionExact,
    bool StandingStartExact,
    bool StraightExact,
    bool LegacyExact,
    bool SegmentPhysicsThresholdsExact,
    bool FullProductionScenarioSuiteExact,
    double ReactionTimeSeconds,
    double TimeTo70KphSeconds,
    double SpeedAtTwoSecondsKilometersPerHour);

public sealed class CornerReducedDriveResistanceExperimentResult
{
    public IReadOnlyList<CornerResistanceCandidate> Candidates { get; }
    public IReadOnlyList<CornerResistanceHeatObservation> Primary { get; }
    public IReadOnlyList<IsolatedCornerResistanceObservation> Isolated { get; }
    public IReadOnlyList<CornerResistanceHeatObservation> DistanceMatched { get; }
    public IReadOnlyList<CornerResistanceHeatObservation> LineSweep { get; }
    public IReadOnlyList<CornerResistanceHeatObservation> SpeedSweep { get; }
    public IReadOnlyList<CornerResistanceHeatObservation> SlideControlSweep { get; }
    public IReadOnlyList<CornerResistanceSurfaceObservation> SurfaceSensitivity { get; }
    public IReadOnlyList<CornerResistanceHeatObservation> Extreme { get; }
    public IReadOnlyDictionary<string, int> PermutationDistinctTraceHashes { get; }
    public CornerResistanceFreezeResult Freeze { get; }
    public string Classification { get; }
    public string NextSubsystem { get; }

    internal CornerReducedDriveResistanceExperimentResult(
        IEnumerable<CornerResistanceCandidate> candidates,
        IEnumerable<CornerResistanceHeatObservation> primary,
        IEnumerable<IsolatedCornerResistanceObservation> isolated,
        IEnumerable<CornerResistanceHeatObservation> distanceMatched,
        IEnumerable<CornerResistanceHeatObservation> lineSweep,
        IEnumerable<CornerResistanceHeatObservation> speedSweep,
        IEnumerable<CornerResistanceHeatObservation> slideControlSweep,
        IEnumerable<CornerResistanceSurfaceObservation> surfaceSensitivity,
        IEnumerable<CornerResistanceHeatObservation> extreme,
        IReadOnlyDictionary<string, int> permutationDistinctTraceHashes,
        CornerResistanceFreezeResult freeze,
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
/// Bounded #42 diagnostic experiment. Every complete heat uses the production
/// CalibrationRunner -> HeatSimulator -> SimulationEngine path. Only advanced
/// continuous-corner traversal receives the internal immutable exposure value.
/// </summary>
public static class CornerReducedDriveResistanceExperiment
{
    public const int FixedSeed = MotoarenaMatchedVenueCalibration.FixedSeed;
    public const int HeatId = 42;
    public const string BaseMainSha = "c1aae7e54e5e70a037b6202787246d3348afbe2b";
    public const float PrimaryLateralPosition = 1f;
    public const float DistanceMatchedLateralPosition = 1.11065f;
    public const float IsolatedEntrySpeedMetersPerSecond = 27.098435f;

    private static readonly float[] ProfileProgress =
        { 0f, .125f, .25f, .375f, .5f, .625f, .75f, .875f, 1f };
    private static readonly float[] SpeedSkills = { 0f, 25f, 50f, 75f, 100f };
    private static readonly float[] SlideSkills = { 0f, 50f, 100f };

    public static IReadOnlyList<CornerResistanceCandidate> CandidateMenu { get; } =
        Array.AsReadOnly(new[]
        {
            new CornerResistanceCandidate("R0", 0f),
            new CornerResistanceCandidate("R25", .25f),
            new CornerResistanceCandidate("R50", .50f),
            new CornerResistanceCandidate("R75", .75f),
            new CornerResistanceCandidate("R100", 1f),
        });

    public static CornerReducedDriveResistanceExperimentResult Run()
    {
        var candidates = CandidateMenu;
        var primary = candidates.Select(candidate => RunFixedLineOneFourRider(
            candidate, $"primary/{candidate.Id}", Skills(), BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface)).ToArray();
        var isolated = candidates.Select(RunIsolatedCorner).ToArray();
        var edgeCandidates = candidates.Where(item => item.Id is "R0" or "R100").ToArray();

        var distanceMatched = edgeCandidates.Select(candidate => RunSingle(
            candidate, $"distance-matched/{candidate.Id}", Skills(), BikeSetup.Neutral,
            DistanceMatchedLateralPosition, CalibrationScenarioCatalog.Baseline.Surface)).ToArray();
        var lines = edgeCandidates.SelectMany(candidate => Enumerable.Range(0, LaneModel.LanesCount)
            .Select(line => RunSingle(candidate, $"line/{candidate.Id}/L{line}", Skills(),
                BikeSetup.Neutral, line, CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var speed = edgeCandidates.SelectMany(candidate => SpeedSkills.Select(value => RunFixedLineOneFourRider(
            candidate, $"speed/{candidate.Id}/{Id(value)}", Skills(speed: value), BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var slide = edgeCandidates.SelectMany(candidate => SlideSkills.Select(value => RunFixedLineOneFourRider(
            candidate, $"slide/{candidate.Id}/{Id(value)}", Skills(slideControl: value), BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var surfaces = edgeCandidates.SelectMany(candidate => CalibrationScenarioCatalog.Surfaces.Select(surface =>
            new CornerResistanceSurfaceObservation(candidate.Id, surface.Id, RunFixedLineOneFourRider(
                candidate, $"surface/{candidate.Id}/{surface.Id}", Skills(), BikeSetup.Neutral,
                surface.Surface)))).ToArray();
        var bestSurface = CalibrationScenarioCatalog.Surfaces
            .OrderByDescending(item => item.Surface.EffectiveGrip)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .First().Surface;
        var extreme = edgeCandidates.Select(candidate => RunSingle(candidate, $"extreme/{candidate.Id}",
            Skills(speed: 100f, slideControl: 100f), new BikeSetup(1f, .5f), 4f, bestSurface)).ToArray();

        var permutationHashes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var candidate in edgeCandidates)
        {
            var hashes = Permutations(new[] { 1, 2, 3, 4 }).Select(order => TraceHash(RunHeat(
                candidate,
                $"permutation/{candidate.Id}",
                order.Select(id => new RiderFixture(id, id - 1, Skills(), BikeSetup.Neutral)),
                CalibrationScenarioCatalog.Baseline.Surface))).Distinct(StringComparer.Ordinal).Count();
            permutationHashes.Add(candidate.Id, hashes);
        }

        var baseline = primary.Single(item => item.CandidateId == "R0");
        var full = primary.Single(item => item.CandidateId == "R100");
        var productionTrace = RunHeat(null, "production/default",
            Enumerable.Range(1, 4).Select(id => new RiderFixture(id, id - 1, Skills(), BikeSetup.Neutral)),
            CalibrationScenarioCatalog.Baseline.Surface);
        var r0TraceExact = TraceFingerprint(productionTrace) == TraceFingerprint(baseline.Trace);
        var envelopeExact = isolated.SelectMany(item => item.ProfilePoints.Select(point =>
                string.Join("|", F(point.CornerProgress), F(point.EnvelopeSpeedMetersPerSecond))))
            .Chunk(ProfileProgress.Length)
            .Select(chunk => string.Join(";", chunk))
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var forceProbeEnvelope = CreatePrimaryEnvelope();
        var availabilityOneExact = candidates.Select(item => forceProbeEnvelope.ObserveReducedDriveForce(
                23f, 1f, item.ReducedDriveResistanceExposure))
            .Select(item => string.Join("|", BitConverter.SingleToInt32Bits(item.AvailableDriveForceNewtons),
                BitConverter.SingleToInt32Bits(item.ResistanceForceNewtons),
                BitConverter.SingleToInt32Bits(item.ExposedResistanceForceNewtons),
                BitConverter.SingleToInt32Bits(item.NetForceNewtons),
                BitConverter.SingleToInt32Bits(item.NetAccelerationMetersPerSecondSquared)))
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var correctionExact = candidates.Select(_ =>
                LongitudinalDynamics.CalculateCornerSpeedCorrectionProfile(27f, 23f, 2.6f, 50f))
            .Select(item => string.Join("|", BitConverter.SingleToInt32Bits(item.ExitSpeedMetersPerSecond),
                BitConverter.SingleToInt32Bits(item.CorrectionDistanceMeters),
                BitConverter.SingleToInt32Bits(item.TravelTimeSeconds), item.TargetReached))
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
            == ScenarioSuiteFingerprint(CalibrationScenarioSuite.RunRequiredWithZeroCornerResistanceExperiment());
        var r0ForceIdentity = R0ForceIdentity(forceProbeEnvelope);
        var freeze = new CornerResistanceFreezeResult(
            r0TraceExact,
            r0TraceExact,
            r0ForceIdentity,
            availabilityOneExact,
            envelopeExact,
            correctionExact,
            standingExact,
            straightExact,
            legacyExact,
            thresholdExact,
            suiteExact,
            launch[0].StandingStartReactionTimeSeconds!.Value,
            launch[0].StandingStartTimeTo70KphSeconds!.Value,
            CalibrationUnits.MetersPerSecondToKph(
                launch[0].StandingStartSpeedAtTwoSecondsMetersPerSecond!.Value));

        var flyingDelta = full.FlyingLapMedianSeconds - baseline.FlyingLapMedianSeconds;
        var amplitudeDelta = full.PeakToApexAmplitudeMetersPerSecond - baseline.PeakToApexAmplitudeMetersPerSecond;
        var preApexDelta = full.TimeBudget.FirstCornerEntryToApexSeconds
            + full.TimeBudget.SecondCornerEntryToApexSeconds
            - baseline.TimeBudget.FirstCornerEntryToApexSeconds
            - baseline.TimeBudget.SecondCornerEntryToApexSeconds;
        var postApexDelta = full.TimeBudget.FirstCornerApexToExitSeconds
            + full.TimeBudget.SecondCornerApexToExitSeconds
            - baseline.TimeBudget.FirstCornerApexToExitSeconds
            - baseline.TimeBudget.SecondCornerApexToExitSeconds;
        var pathological = full.AnyZeroSpeedEvent || !double.IsFinite(full.FlyingLapMedianSeconds)
            || speed.GroupBy(item => item.CandidateId).Any(group => !StrictlyDecreasing(
                group.OrderBy(item => item.SkillSpeed).Select(item => item.FlyingLapMedianSeconds).ToArray()));
        var classification = pathological
            ? "FullPassiveResistanceExposureOverstatesLoss"
            : flyingDelta <= .01d
                ? "ExistingResistanceExposureInsufficient"
                : postApexDelta > preApexDelta * 2d && amplitudeDelta < .05d
                    ? "LossLocationMismatch"
                    : "PassiveResistanceSemanticsPlausiblyRelevant";
        var nextSubsystem = classification is "ExistingResistanceExposureInsufficient" or "LossLocationMismatch"
            ? "corner-specific slip-loss / scrub-drag"
            : "independent continuous-corner resistance validation";

        return new CornerReducedDriveResistanceExperimentResult(
            candidates, primary, isolated, distanceMatched, lines, speed, slide, surfaces,
            extreme, permutationHashes, freeze, classification, nextSubsystem);
    }

    private static CornerResistanceHeatObservation RunFixedLineOneFourRider(
        CornerResistanceCandidate candidate,
        string scenarioId,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        var trace = RunHeat(candidate, scenarioId,
            Enumerable.Range(1, 4).Select(id => new RiderFixture(id, id - 1, skills, setup)), surface);
        return ObserveRider(candidate, scenarioId, trace, 2, PrimaryLateralPosition);
    }

    private static CornerResistanceHeatObservation RunSingle(
        CornerResistanceCandidate candidate,
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
        CornerResistanceCandidate? candidate,
        string scenarioId,
        IEnumerable<RiderFixture> fixtures,
        TrackSurfaceState surface)
    {
        var fixtureArray = fixtures.ToArray();
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaPrimaryStartLineToFirstCornerMeters);
        var riders = fixtureArray.Select(item => new RiderState(
            new RiderProfile(item.RiderId, $"#42 {scenarioId} rider {item.RiderId}", item.Skills,
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
            CornerReducedDriveResistanceAdjustment = candidate is null
                ? null
                : new CornerReducedDriveResistanceAdjustment(candidate.ReducedDriveResistanceExposure),
        };
        return CalibrationRunner.RunHeat(track, TrackState.CreateDefault(track, surface), riders,
            new HoldLaneDecisionModel(), options, HeatId);
    }

    private static CornerResistanceHeatObservation ObserveRider(
        CornerResistanceCandidate candidate,
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
        var profilePoints = cornerSeries.SelectMany(item => item.Points).ToArray();
        var rawCornerNodes = flying.Where(item => item.ContinuousCornerProfile is not null)
            .SelectMany(item => item.ContinuousCornerProfile!.Nodes).ToArray();
        var apex = profilePoints.Where(item => item.CornerProgress == ContinuousCornerEnvelope.ApexProgress)
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
        var timeBudget = BuildTimeBudget(flying, cornerSeries);
        var cornerProfiles = flying.Where(item => item.ContinuousCornerProfile is not null)
            .Select(item => item.ContinuousCornerProfile!).ToArray();
        var travelled = cornerProfiles.Sum(item => (double)item.CorrectionDistanceMeters
            + item.CarryDistanceMeters + item.DriveDistanceMeters);
        var correction = cornerProfiles.Sum(item => (double)item.CorrectionDistanceMeters);
        var passive = cornerProfiles.Sum(item => (double)item.PassiveResistanceDistanceMeters);
        var positive = cornerProfiles.Sum(item => (double)item.PositiveDriveDistanceMeters);
        var negative = cornerProfiles.Sum(item => (double)item.NegativeSignedDriveDistanceMeters);
        var neutral = cornerProfiles.Sum(item => (double)item.NeutralCarryDistanceMeters);
        var zeroAvailability = cornerProfiles.Sum(item => (double)item.ZeroAvailabilityUncorrectedDistanceMeters);
        var bucketSum = correction + passive + positive + negative + neutral;
        var buckets = new CornerResistanceDistanceBuckets(
            travelled, correction, passive, positive, negative, neutral, zeroAvailability,
            Math.Abs(bucketSum - travelled) <= Math.Max(1e-5d, travelled * 1e-5d));
        var anyZero = rawCornerNodes.Any(item => item.SpeedMetersPerSecond <= 0f);
        return new CornerResistanceHeatObservation(
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
            entry,
            apex,
            exit,
            straightPeak,
            straightPeak - apex,
            straightPeak - minimumCorner.MinimumSpeedMetersPerSecond,
            apex - entry,
            exit - apex,
            minimumCorner.MinimumSpeedMetersPerSecond,
            minimumCorner.MinimumSpeedCornerProgress,
            minimumCorner.CornerNumber,
            cornerSeries[0].MinimumSpeedMetersPerSecond,
            cornerSeries[0].MinimumSpeedCornerProgress,
            cornerSeries[1].MinimumSpeedMetersPerSecond,
            cornerSeries[1].MinimumSpeedCornerProgress,
            rider.BrakeCount,
            rider.RunWideCount,
            rider.CrashCount,
            anyZero,
            timeBudget,
            buckets,
            Array.AsReadOnly(profilePoints),
            trace);
    }

    private static CornerResistanceTimeBudget BuildTimeBudget(
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
        var flyingTime = flying.Sum(item => (double)item.DurationSeconds);
        return new CornerResistanceTimeBudget(
            homeStart, back, homeFinish, totalStraight,
            first.EntryToApexTimeSeconds, first.ApexToExitTimeSeconds, first.TotalTimeSeconds,
            second.EntryToApexTimeSeconds, second.ApexToExitTimeSeconds, second.TotalTimeSeconds,
            totalCorner, flyingTime);
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
                    ?? throw new InvalidOperationException("Corner sample has no production traversal profile.");
                foreach (var node in profile.Nodes)
                    nodes.Add(node with { ElapsedTimeSeconds = elapsedOffset + node.ElapsedTimeSeconds });
                elapsedOffset += profile.TravelTimeSeconds;
            }
            var points = ProfileProgress.Select(progress => Interpolate(
                candidateId, groupIndex + 1, nodes, progress)).ToArray();
            var apexTime = points.Single(item => item.CornerProgress == .5f).ElapsedCornerTimeSeconds;
            var minimum = nodes.OrderBy(item => item.SpeedMetersPerSecond)
                .ThenBy(item => item.CornerProgress)
                .First();
            result.Add(new CornerSeries(Array.AsReadOnly(points), apexTime,
                elapsedOffset - apexTime, elapsedOffset,
                minimum.SpeedMetersPerSecond, minimum.CornerProgress));
        }
        if (result.Count != 2)
            throw new InvalidOperationException("Motoarena fixture must contain two logical corners.");
        return Array.AsReadOnly(result.ToArray());
    }

    private static CornerResistanceProfilePoint Interpolate(
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
        var phase = progress == 0f && ordered.Length > 1
            ? ordered[1].PhaseClassification
            : b.PhaseClassification;
        var actual = L(a.SpeedMetersPerSecond, b.SpeedMetersPerSecond);
        return new CornerResistanceProfilePoint(
            candidateId,
            cornerNumber,
            progress,
            actual,
            CalibrationUnits.MetersPerSecondToKph(actual),
            L(a.EnvelopeSpeedMetersPerSecond, b.EnvelopeSpeedMetersPerSecond),
            L(a.DriveAvailability, b.DriveAvailability),
            L(a.AvailableDriveForceNewtons, b.AvailableDriveForceNewtons),
            L(a.ResistanceForceNewtons, b.ResistanceForceNewtons),
            L(a.ExposedResistanceForceNewtons, b.ExposedResistanceForceNewtons),
            L(a.NetForceNewtons, b.NetForceNewtons),
            L(a.NetDriveAccelerationMetersPerSecondSquared, b.NetDriveAccelerationMetersPerSecondSquared),
            phase,
            L(a.ElapsedTimeSeconds, b.ElapsedTimeSeconds));
    }

    private static IsolatedCornerResistanceObservation RunIsolatedCorner(CornerResistanceCandidate candidate)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaPrimaryStartLineToFirstCornerMeters);
        var phase = track.CornerTopology.Resolve(1, 0f, PrimaryLateralPosition, track.Geometry)
            ?? throw new InvalidOperationException("Motoarena first corner has no phase context.");
        var envelope = ContinuousCornerEnvelope.Create(
            phase,
            PrimaryLateralPosition,
            track.Geometry,
            CalibrationScenarioCatalog.Baseline.Surface,
            Skills(),
            BikeSetup.Neutral);
        var profile = envelope.TraverseWithReducedDriveResistanceExposure(
            IsolatedEntrySpeedMetersPerSecond,
            0f,
            phase.TotalCornerLengthMeters,
            candidate.ReducedDriveResistanceExposure);
        var points = ProfileProgress.Select(progress => Interpolate(
            candidate.Id, 1, profile.Nodes, progress)).ToArray();
        var entry = points[0].ActualSpeedMetersPerSecond;
        var apex = points.Single(item => item.CornerProgress == .5f).ActualSpeedMetersPerSecond;
        var exit = points[^1].ActualSpeedMetersPerSecond;
        var apexTime = points.Single(item => item.CornerProgress == .5f).ElapsedCornerTimeSeconds;
        var correctionNodes = profile.Nodes.Where(item =>
            item.PhaseClassification == ContinuousCornerPhaseClassification.Correction).ToArray();
        var targetReached = profile.Nodes.FirstOrDefault(item =>
            item.CornerProgress > 0f
            && item.SpeedMetersPerSecond <= item.EnvelopeSpeedMetersPerSecond + 1e-5f);
        var correction = profile.CorrectionDistanceMeters;
        var passive = profile.PassiveResistanceDistanceMeters;
        var positive = profile.PositiveDriveDistanceMeters;
        var negative = profile.NegativeSignedDriveDistanceMeters;
        var neutral = profile.NeutralCarryDistanceMeters;
        var bucketSum = correction + passive + positive + negative + neutral;
        var buckets = new CornerResistanceDistanceBuckets(
            phase.TotalCornerLengthMeters,
            correction,
            passive,
            positive,
            negative,
            neutral,
            profile.ZeroAvailabilityUncorrectedDistanceMeters,
            MathF.Abs(bucketSum - phase.TotalCornerLengthMeters)
                <= MathF.Max(1e-5f, phase.TotalCornerLengthMeters * 1e-5f));
        double Ke(float speed) => .5d * LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
            * speed * speed;
        var entryKe = Ke(entry);
        var apexKe = Ke(apex);
        var exitKe = Ke(exit);
        return new IsolatedCornerResistanceObservation(
            candidate.Id,
            entry,
            apex,
            exit,
            profile.PeakSpeedMetersPerSecond - apex,
            apex - entry,
            exit - apex,
            apexTime,
            profile.TravelTimeSeconds - apexTime,
            profile.TravelTimeSeconds,
            correctionNodes.Length == 0 ? null : correctionNodes.Min(item => item.CornerProgress),
            correctionNodes.Length == 0 ? null : correctionNodes.Max(item => item.CornerProgress),
            profile.CorrectionDistanceMeters,
            targetReached?.CornerProgress,
            buckets,
            new CornerResistanceEnergyDiagnostic(entryKe, apexKe, exitKe,
                apexKe - entryKe, exitKe - apexKe),
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

    private static bool R0ForceIdentity(ContinuousCornerEnvelope envelope)
        => new[] { 0f, .125f, .5f, .875f, 1f }.All(availability =>
        {
            var observation = envelope.ObserveReducedDriveForce(23f, availability, 0f);
            var expectedExposed = (float)((double)availability * observation.ResistanceForceNewtons);
            var expectedNet = observation.AvailableDriveForceNewtons - expectedExposed;
            return BitConverter.SingleToInt32Bits(observation.ExposedResistanceForceNewtons)
                    == BitConverter.SingleToInt32Bits(expectedExposed)
                && BitConverter.SingleToInt32Bits(observation.NetForceNewtons)
                    == BitConverter.SingleToInt32Bits(expectedNet);
        });

    private static string StraightFingerprint(CornerResistanceCandidate candidate)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var rider = new RiderState(new RiderProfile(1, "straight freeze", Skills(), RiderStyle.Balanced), 1)
        {
            LateralPosition = 1f,
            Speed = 24f,
            ActiveSetup = BikeSetup.Neutral,
        };
        rider.RestorePosition(RiderPosition.Create(1, 4, 0f, track.Segments.Count));
        var resolved = ResolveOne(track, rider, 4, candidate, useLegacy: false);
        var profile = resolved.Diagnostics.Single().StraightProfile!.Value;
        return string.Join("|", F(profile.ExitSpeedMetersPerSecond), F(profile.PeakSpeedMetersPerSecond),
            F(profile.TravelTimeSeconds), F(profile.AccelerationDistanceMeters),
            F(profile.CruiseDistanceMeters), F(profile.DecelerationDistanceMeters));
    }

    private static string LegacyFingerprint(CornerResistanceCandidate candidate)
    {
        var track = Track.CreateExample();
        var rider = new RiderState(new RiderProfile(1, "legacy freeze", Skills(), RiderStyle.Balanced), 1)
        {
            Speed = 19f,
            ActiveSetup = BikeSetup.Neutral,
        };
        var resolved = ResolveOne(track, rider, 0, candidate, useLegacy: true);
        var change = resolved.Changes.Single();
        return string.Join("|", change.Outcome, change.Lane, F(change.Speed), F(change.ElapsedTimeSeconds));
    }

    private static string SegmentPhysicsFingerprint(CornerResistanceCandidate candidate)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var phase = track.CornerTopology.Resolve(1, 0f, 1f, track.Geometry)!.Value;
        var envelope = ContinuousCornerEnvelope.Create(phase, 1f, track.Geometry,
            CalibrationScenarioCatalog.Baseline.Surface, Skills(), BikeSetup.Neutral);
        var speeds = new[] { .99f, 1.02f, 1.08f, 1.22f }.Select(multiplier =>
        {
            var rider = new RiderState(new RiderProfile(1, "threshold freeze", Skills(), RiderStyle.Balanced), 1)
            {
                LateralPosition = 1f,
                Speed = envelope.SpeedMetersPerSecond(0f) * multiplier,
                ActiveSetup = BikeSetup.Neutral,
            };
            rider.RestorePosition(RiderPosition.Create(1, 1, 0f, track.Segments.Count));
            var resolved = ResolveOne(track, rider, 1, candidate, useLegacy: false);
            var change = resolved.Changes.Single();
            return string.Join(",", change.Outcome, change.Lane);
        });
        return string.Join(";", speeds);
    }

    private static ResolvedSimulationStep ResolveOne(
        Track track,
        RiderState rider,
        int segmentIndex,
        CornerResistanceCandidate candidate,
        bool useLegacy)
    {
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track,
                CalibrationScenarioCatalog.Baseline.Surface), new[] { rider },
            new SimulationStepContext(HeatId, 0, rider.LapsCompleted, segmentIndex, FixedSeed, 4,
                UseLegacyPhysics: useLegacy));
        return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions
        {
            Laps = 4,
            Seed = FixedSeed,
            IncidentFrequency = 0f,
            EnableLogging = false,
            CornerReducedDriveResistanceAdjustment =
                new CornerReducedDriveResistanceAdjustment(candidate.ReducedDriveResistanceExposure),
        });
    }

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

    private static bool StrictlyDecreasing(IReadOnlyList<double> values)
    {
        for (var index = 1; index < values.Count; index++)
            if (values[index] >= values[index - 1]) return false;
        return true;
    }

    private static RiderSkills Skills(float speed = 50f, float slideControl = 50f) =>
        new(50f, speed, slideControl, 50f, 50f, 50f);
    private static string Id(float value) => value.ToString("000", CultureInfo.InvariantCulture);
    private static string F(float? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "null";

    private sealed record RiderFixture(int RiderId, float LateralPosition, RiderSkills Skills, BikeSetup Setup);
    private sealed record CornerSeries(
        IReadOnlyList<CornerResistanceProfilePoint> Points,
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
