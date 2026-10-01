using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record StraightDriveEnvelopeCandidate(
    string Id,
    string Name,
    float LowSpeedSuppression,
    float HighSpeedRetention,
    string Purpose)
{
    internal StraightDriveEnvelopeAdjustment Adjustment =>
        new(LowSpeedSuppression, HighSpeedRetention);
}

public sealed record StraightDriveForcePoint(
    string CandidateId,
    float SpeedMetersPerSecond,
    float Gearing,
    float BaselineMultiplier,
    float Delta,
    float ExperimentalMultiplier,
    float SignedNetAccelerationMetersPerSecondSquared,
    bool LowerClampActivated,
    bool UpperClampActivated);

public sealed record StraightDriveHeatObservation(
    string CandidateId,
    string ScenarioId,
    float SkillSpeed,
    float SkillSlideControl,
    float Gearing,
    float LateralPosition,
    double VmaxKilometersPerHour,
    string VmaxLocation,
    float? VmaxStraightProgress,
    float? VmaxDistanceBeforeCornerMeters,
    double StraightEntrySpeedMetersPerSecond,
    double StraightPeakSpeedMetersPerSecond,
    double CornerEntrySpeedMetersPerSecond,
    double ApexMinimumSpeedMetersPerSecond,
    double CornerExitSpeedMetersPerSecond,
    double StraightToApexAmplitudeMetersPerSecond,
    double? L2Seconds,
    double? L3Seconds,
    double? L4Seconds,
    double? FlyingLapMedianSeconds,
    double HeatTimeSeconds,
    double TotalDistanceMeters,
    double AverageSpeedMetersPerSecond,
    int BrakeCount,
    int RunWideCount,
    int CrashCount,
    double StraightAccelerationDistanceMeters,
    double StraightCruiseDistanceMeters,
    double StraightPreparationDistanceMeters,
    double FullDriveEquilibriumSpeedMetersPerSecond,
    double EnvelopeMultiplierAtVmax,
    double ReactionTimeSeconds,
    double? TimeTo70KphSeconds,
    double? SpeedAtTwoSecondsKilometersPerHour,
    CalibrationTrace Trace);

public sealed record StraightDriveBalancedObservation(
    string CandidateId,
    double VmaxP50KilometersPerHour,
    double AverageSpeedP50MetersPerSecond,
    double FlyingLapP50Seconds,
    double HeatTimeP50Seconds,
    double TotalDistanceP50Meters);

public sealed record StraightDriveTracePoint(
    string CandidateId,
    int LapNumber,
    float CumulativeLapDistanceMeters,
    string Segment,
    float? DistanceIntoStraightMeters,
    float? DistanceRemainingToCornerMeters,
    float SpeedMetersPerSecond,
    float? BaselineMultiplier,
    float? Delta,
    float? ExperimentalMultiplier,
    float? NetAccelerationMetersPerSecondSquared,
    float? BackwardAllowedSpeedMetersPerSecond,
    bool? PreparationBoundaryActive,
    float? CornerProgress,
    float? CornerEnvelopeSpeedMetersPerSecond,
    float? CornerDriveAvailability);

public sealed record StraightDriveFreezeResult(
    bool StandingStartExact,
    bool CornerLawExact,
    bool LegacyExact,
    double ReactionTimeSeconds,
    double TimeTo70KphSeconds,
    double SpeedAtTwoSecondsKilometersPerHour);

public sealed class StraightDriveEnvelopeExperimentResult
{
    public IReadOnlyList<StraightDriveEnvelopeCandidate> Candidates { get; }
    public IReadOnlyList<StraightDriveForcePoint> ForcePoints { get; }
    public IReadOnlyList<StraightDriveHeatObservation> Primary { get; }
    public IReadOnlyList<StraightDriveHeatObservation> SpeedSweep { get; }
    public IReadOnlyList<StraightDriveHeatObservation> SlideControlSweep { get; }
    public IReadOnlyList<StraightDriveHeatObservation> GearingSweep { get; }
    public IReadOnlyList<StraightDriveHeatObservation> Extreme { get; }
    public IReadOnlyList<StraightDriveBalancedObservation> Balanced { get; }
    public IReadOnlyList<StraightDriveTracePoint> FlyingLapTrace { get; }
    public IReadOnlyDictionary<string, int> PermutationDistinctTraceHashes { get; }
    public string DiagnosticBestCandidateId { get; }
    public string Classification { get; }
    public StraightDriveFreezeResult Freeze { get; }

    internal StraightDriveEnvelopeExperimentResult(
        IEnumerable<StraightDriveEnvelopeCandidate> candidates,
        IEnumerable<StraightDriveForcePoint> forcePoints,
        IEnumerable<StraightDriveHeatObservation> primary,
        IEnumerable<StraightDriveHeatObservation> speedSweep,
        IEnumerable<StraightDriveHeatObservation> slideControlSweep,
        IEnumerable<StraightDriveHeatObservation> gearingSweep,
        IEnumerable<StraightDriveHeatObservation> extreme,
        IEnumerable<StraightDriveBalancedObservation> balanced,
        IEnumerable<StraightDriveTracePoint> flyingLapTrace,
        IReadOnlyDictionary<string, int> permutationDistinctTraceHashes,
        string diagnosticBestCandidateId,
        string classification,
        StraightDriveFreezeResult freeze)
    {
        Candidates = ReadOnly(candidates);
        ForcePoints = ReadOnly(forcePoints);
        Primary = ReadOnly(primary);
        SpeedSweep = ReadOnly(speedSweep);
        SlideControlSweep = ReadOnly(slideControlSweep);
        GearingSweep = ReadOnly(gearingSweep);
        Extreme = ReadOnly(extreme);
        Balanced = ReadOnly(balanced);
        FlyingLapTrace = ReadOnly(flyingLapTrace);
        PermutationDistinctTraceHashes = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(permutationDistinctTraceHashes, StringComparer.Ordinal));
        DiagnosticBestCandidateId = diagnosticBestCandidateId;
        Classification = classification;
        Freeze = freeze;
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}

/// <summary>
/// Bounded #41 causal experiment. Candidate heats use the production
/// CalibrationRunner -> HeatSimulator -> SimulationEngine path. Only the
/// ordinary advanced Straight envelope receives the internal adjustment.
/// </summary>
public static class StraightDriveEnvelopeExperiment
{
    public const int FixedSeed = MotoarenaMatchedVenueCalibration.FixedSeed;
    // Preserve the exact #39 matched-fixture identity for A0 comparisons.
    public const int HeatId = MotoarenaMatchedVenueCalibration.HeatId;
    public const string BaseMainSha = "01af1a8e605e1c998c6148e3abe1a7aa17cf0e17";

    private static readonly float[] CurveSpeeds =
        { 16f, 18f, 20f, 22f, 24f, 26f, 28f, 30f, 32f, 34f, 36f, 38f, 40f };
    private static readonly float[] Gearings = { 0f, .5f, 1f };
    private static readonly float[] SpeedSkills = { 0f, 25f, 50f, 75f, 100f };
    private static readonly float[] SlideSkills = { 0f, 50f, 100f };

    public static IReadOnlyList<StraightDriveEnvelopeCandidate> CandidateMenu { get; } =
        Array.AsReadOnly(new[]
        {
            new StraightDriveEnvelopeCandidate("A0", "ProductionBaseline", 0f, 0f,
                "Reviewed post-#40 production envelope"),
            new StraightDriveEnvelopeCandidate("R08", "AreaNeutralProxy08", .08f, .04f,
                "EnvelopeAreaControlProxy"),
            new StraightDriveEnvelopeCandidate("R12", "AreaNeutralProxy12", .12f, .06f,
                "EnvelopeAreaControlProxy"),
            new StraightDriveEnvelopeCandidate("H08", "HighSpeedRetention08", .08f, .08f,
                "DiagnosticDriveEnvelopeRedistribution"),
            new StraightDriveEnvelopeCandidate("H12", "HighSpeedRetention12", .12f, .12f,
                "DiagnosticDriveEnvelopeRedistribution"),
        });

    public static StraightDriveEnvelopeExperimentResult Run()
    {
        var candidates = CandidateMenu;
        var primary = candidates.Select(candidate => RunFixedLineOneFourRider(
            candidate, $"primary/{candidate.Id}", Skills(), BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface)).ToArray();
        var baseline = primary.Single(item => item.CandidateId == "A0");
        var desired = primary.Where(item => item.CandidateId != "A0"
                && item.VmaxKilometersPerHour > baseline.VmaxKilometersPerHour
                && item.FlyingLapMedianSeconds > baseline.FlyingLapMedianSeconds
                && item.CrashCount == 0)
            .OrderByDescending(item => item.VmaxKilometersPerHour)
            .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var diagnosticBestId = desired.FirstOrDefault()?.CandidateId
            ?? primary.Where(item => item.CandidateId != "A0")
                .OrderByDescending(item => item.VmaxKilometersPerHour)
                .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
                .First().CandidateId;
        var classification = desired.Length > 0
            ? "PromisingForProductionFollowUp"
            : primary.Any(item => item.CandidateId != "A0"
                && item.VmaxKilometersPerHour > baseline.VmaxKilometersPerHour
                && item.FlyingLapMedianSeconds < baseline.FlyingLapMedianSeconds)
                ? "StraightEnvelopeShapeInsufficient"
                : "FiniteStraightDistanceOrCornerExitStateDominates";

        var force = candidates.SelectMany(candidate => CurveSpeeds.SelectMany(speed =>
            Gearings.Select(gearing => ObserveForce(candidate, speed, gearing)))).ToArray();
        var speedSweep = candidates.SelectMany(candidate => SpeedSkills.Select(speed => RunFixedLineOneFourRider(
            candidate, $"speed/{candidate.Id}/{Id(speed)}", Skills(speed: speed), BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var slideCandidates = candidates.Where(candidate => candidate.Id is "A0" || candidate.Id == diagnosticBestId);
        var slideSweep = slideCandidates.SelectMany(candidate => SlideSkills.Select(slide => RunFixedLineOneFourRider(
            candidate, $"slide/{candidate.Id}/{Id(slide)}", Skills(slideControl: slide), BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var gearingSweep = candidates.SelectMany(candidate => Gearings.Select(gearing => RunFixedLineOneFourRider(
            candidate, $"gearing/{candidate.Id}/{Id(gearing * 100f)}", Skills(), new BikeSetup(gearing, .5f),
            CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var bestSurface = CalibrationScenarioCatalog.Surfaces
            .OrderByDescending(item => item.Surface.EffectiveGrip)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .First().Surface;
        var extreme = candidates.Select(candidate => RunSingle(
            candidate, $"extreme/{candidate.Id}", Skills(speed: 100f), new BikeSetup(1f, .5f), 4f,
            bestSurface)).ToArray();
        var balanced = candidates.Select(candidate => ObserveBalanced(candidate,
            RunHeat(candidate, $"balanced/{candidate.Id}",
                Enumerable.Range(1, 4).Select(id => new RiderFixture(id, id - 1, Skills(), BikeSetup.Neutral)),
                CalibrationScenarioCatalog.Baseline.Surface))).ToArray();
        var trace = primary.SelectMany(item => BuildFlyingLapTrace(
            candidates.Single(candidate => candidate.Id == item.CandidateId), item.Trace,
            riderId: 2, lapIndex: 1)).ToArray();
        var permutationHashes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var candidateId in new[] { "A0", diagnosticBestId }.Distinct(StringComparer.Ordinal))
        {
            var candidate = candidates.Single(item => item.Id == candidateId);
            var hashes = Permutations(new[] { 1, 2, 3, 4 }).Select(order => TraceHash(RunHeat(
                candidate, $"permutation/{candidate.Id}",
                order.Select(id => new RiderFixture(id, id - 1, Skills(), BikeSetup.Neutral)),
                CalibrationScenarioCatalog.Baseline.Surface))).Distinct(StringComparer.Ordinal).Count();
            permutationHashes.Add(candidate.Id, hashes);
        }

        var standingExact = primary.All(item =>
            BitConverter.SingleToInt32Bits((float)item.ReactionTimeSeconds)
                == BitConverter.SingleToInt32Bits((float)baseline.ReactionTimeSeconds)
            && BitConverter.SingleToInt32Bits((float)item.TimeTo70KphSeconds!.Value)
                == BitConverter.SingleToInt32Bits((float)baseline.TimeTo70KphSeconds!.Value)
            && BitConverter.SingleToInt32Bits((float)item.SpeedAtTwoSecondsKilometersPerHour!.Value)
                == BitConverter.SingleToInt32Bits((float)baseline.SpeedAtTwoSecondsKilometersPerHour!.Value));
        var cornerExact = candidates.Select(CornerLawFingerprint).Distinct(StringComparer.Ordinal).Count() == 1;
        var legacyExact = candidates.Select(LegacyFingerprint).Distinct(StringComparer.Ordinal).Count() == 1;
        var freeze = new StraightDriveFreezeResult(
            standingExact,
            cornerExact,
            legacyExact,
            baseline.ReactionTimeSeconds,
            baseline.TimeTo70KphSeconds!.Value,
            baseline.SpeedAtTwoSecondsKilometersPerHour!.Value);

        return new StraightDriveEnvelopeExperimentResult(
            candidates, force, primary, speedSweep, slideSweep, gearingSweep, extreme,
            balanced, trace, permutationHashes, diagnosticBestId, classification, freeze);
    }

    public static float ExperimentalDelta(
        StraightDriveEnvelopeCandidate candidate,
        float speedMetersPerSecond) => candidate.Adjustment.Delta(speedMetersPerSecond);

    public static float ExperimentalMultiplier(
        StraightDriveEnvelopeCandidate candidate,
        float speedMetersPerSecond,
        float gearing) => LongitudinalDynamics.CalculateStraightDriveEnvelopeMultiplier(
            speedMetersPerSecond,
            new BikeSetup(gearing, .5f),
            candidate.Adjustment);

    private static StraightDriveForcePoint ObserveForce(
        StraightDriveEnvelopeCandidate candidate,
        float speed,
        float gearing)
    {
        var setup = new BikeSetup(gearing, .5f);
        var baseline = LongitudinalDynamics.CalculatePositiveDriveEnvelopeMultiplier(speed, setup);
        var delta = candidate.Adjustment.Delta(speed);
        var unclamped = (double)baseline + delta;
        var multiplier = LongitudinalDynamics.CalculateStraightDriveEnvelopeMultiplier(
            speed, setup, candidate.Adjustment);
        var force = LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(
            Skills(), setup, CalibrationScenarioCatalog.Baseline.Surface);
        var acceleration = LongitudinalDynamics.CalculateStraightNetDriveAccelerationMetersPerSecondSquared(
            speed, force, setup, candidate.Adjustment);
        return new StraightDriveForcePoint(candidate.Id, speed, gearing, baseline, delta, multiplier,
            acceleration, unclamped < 0d, unclamped > 1d);
    }

    private static StraightDriveHeatObservation RunSingle(
        StraightDriveEnvelopeCandidate candidate,
        string scenarioId,
        RiderSkills skills,
        BikeSetup setup,
        float lateralPosition,
        TrackSurfaceState surface)
    {
        var trace = RunHeat(candidate, scenarioId,
            new[] { new RiderFixture(1, lateralPosition, skills, setup) }, surface);
        return ObserveRider(candidate, scenarioId, trace, trace.RiderSummaries.Single().RiderId);
    }

    private static StraightDriveHeatObservation RunFixedLineOneFourRider(
        StraightDriveEnvelopeCandidate candidate,
        string scenarioId,
        RiderSkills skills,
        BikeSetup setup,
        TrackSurfaceState surface)
    {
        var trace = RunHeat(candidate, scenarioId,
            Enumerable.Range(1, 4).Select(id => new RiderFixture(id, id - 1, skills, setup)),
            surface);
        return ObserveRider(candidate, scenarioId, trace, riderId: 2);
    }

    private static CalibrationTrace RunHeat(
        StraightDriveEnvelopeCandidate candidate,
        string scenarioId,
        IEnumerable<RiderFixture> fixtures,
        TrackSurfaceState surface)
    {
        var fixtureArray = fixtures.ToArray();
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaHistorical39StartLineToFirstCornerMeters);
        var riders = fixtureArray.Select(item => new RiderState(
            new RiderProfile(item.RiderId, $"#41 {scenarioId} rider {item.RiderId}", item.Skills, RiderStyle.Balanced),
            (int)item.LateralPosition)
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
            StraightDriveEnvelopeAdjustment = candidate.Adjustment,
        };
        return CalibrationRunner.RunHeat(
            track,
            TrackState.CreateDefault(track, surface),
            riders,
            new HoldLaneDecisionModel(),
            options,
            HeatId);
    }

    private static StraightDriveHeatObservation ObserveRider(
        StraightDriveEnvelopeCandidate candidate,
        string scenarioId,
        CalibrationTrace trace,
        int riderId)
    {
        var samples = trace.StepSamples.Where(item => item.RiderId == riderId).ToArray();
        var rider = CalibrationSkillSweep.ObserveRiders(trace).Single(item => item.RiderId == riderId);
        var flying = samples.Where(item => item.LapIndex == 1).ToArray();
        var flyingStraights = flying.Where(IsOrdinaryStraight).ToArray();
        var flyingCorners = flying.Where(item => item.ContinuousCornerProfile is not null).ToArray();
        var apexMinimum = flyingCorners.SelectMany(item => item.ContinuousCornerProfile!.Nodes)
            .Min(item => item.SpeedMetersPerSecond);
        var straightPeak = flyingStraights.Max(item => item.PeakSpeedMetersPerSecond);
        var peakSample = samples.OrderByDescending(item => item.PeakSpeedMetersPerSecond)
            .ThenBy(item => item.StepNumber).First();
        var peakLocation = LocatePeak(trace, peakSample);
        var peakMultiplier = LongitudinalDynamics.CalculateStraightDriveEnvelopeMultiplier(
            rider.MaximumSpeedMetersPerSecond,
            new BikeSetup(peakSample.Gearing, peakSample.TractionBias),
            candidate.Adjustment);
        var launch = samples.Single(item => item.LapIndex == 0 && item.SegmentIndex == 0);
        var equilibrium = peakSample.FullDriveEquilibriumSpeedMetersPerSecond
            ?? samples.Where(item => item.FullDriveEquilibriumSpeedMetersPerSecond.HasValue)
                .Max(item => item.FullDriveEquilibriumSpeedMetersPerSecond!.Value);

        return new StraightDriveHeatObservation(
            candidate.Id,
            scenarioId,
            rider.RiderId == 0 ? 0f : samples[0].RiderSpeedSkill,
            samples[0].RiderSlideControlSkill,
            samples[0].Gearing,
            samples[0].EntryLateralPosition,
            CalibrationUnits.MetersPerSecondToKph(rider.MaximumSpeedMetersPerSecond),
            peakLocation.Label,
            peakLocation.StraightProgress,
            peakLocation.DistanceBeforeCornerMeters,
            Median(flyingStraights.Select(item => item.EntrySpeedMetersPerSecond)),
            straightPeak,
            Median(flying.Where(item => item.SegmentType == SegmentType.TurnEntry)
                .Select(item => item.EntrySpeedMetersPerSecond)),
            apexMinimum,
            Median(flying.Where(item => item.SegmentType == SegmentType.TurnExit)
                .Select(item => item.ExitSpeedMetersPerSecond)),
            straightPeak - apexMinimum,
            rider.L2Seconds,
            rider.L3Seconds,
            rider.L4Seconds,
            rider.FlyingLapMedianSeconds,
            rider.TotalTimeSeconds,
            rider.TotalDistanceMeters,
            rider.AverageSpeedMetersPerSecond ?? throw new InvalidOperationException("Completed heat has no average speed."),
            rider.BrakeCount,
            rider.RunWideCount,
            rider.CrashCount,
            samples.Where(IsOrdinaryStraight).Sum(item => item.StraightAccelerationDistanceMeters ?? 0f),
            samples.Where(IsOrdinaryStraight).Sum(item => item.StraightCruiseDistanceMeters ?? 0f),
            samples.Where(IsOrdinaryStraight).Sum(item => item.StraightDecelerationDistanceMeters ?? 0f),
            equilibrium,
            peakMultiplier,
            launch.StandingStartReactionTimeSeconds ?? throw new InvalidOperationException("Standing start missing."),
            launch.StandingStartTimeTo70KphSeconds,
            launch.StandingStartSpeedAtTwoSecondsMetersPerSecond is { } speedAtTwo
                ? CalibrationUnits.MetersPerSecondToKph(speedAtTwo)
                : null,
            trace);
    }

    private static StraightDriveBalancedObservation ObserveBalanced(
        StraightDriveEnvelopeCandidate candidate,
        CalibrationTrace trace)
    {
        var riders = CalibrationSkillSweep.ObserveRiders(trace);
        return new StraightDriveBalancedObservation(
            candidate.Id,
            Quantile(riders.Select(item => (double)CalibrationUnits.MetersPerSecondToKph(item.MaximumSpeedMetersPerSecond))),
            Quantile(riders.Select(item => (double)item.AverageSpeedMetersPerSecond!.Value)),
            Quantile(riders.Select(item => (double)item.FlyingLapMedianSeconds!.Value)),
            Quantile(riders.Select(item => (double)item.TotalTimeSeconds)),
            Quantile(riders.Select(item => (double)item.TotalDistanceMeters)));
    }

    private static IReadOnlyList<StraightDriveTracePoint> BuildFlyingLapTrace(
        StraightDriveEnvelopeCandidate candidate,
        CalibrationTrace trace,
        int riderId,
        int lapIndex)
    {
        var samples = trace.StepSamples.Where(item => item.RiderId == riderId && item.LapIndex == lapIndex)
            .OrderBy(item => item.SegmentIndex).ToArray();
        var points = new List<StraightDriveTracePoint>();
        var cumulative = 0f;
        foreach (var sample in samples)
        {
            if (sample.StraightDriveCalibrationSteps is { } straightSteps)
            {
                var selected = straightSteps.Where((item, index) =>
                        index == straightSteps.Count - 1
                        || MathF.Abs(item.EndDistanceMeters / 5f - MathF.Round(item.EndDistanceMeters / 5f)) < 1e-5f
                        || item.PreparationApplied != (index > 0 && straightSteps[index - 1].PreparationApplied))
                    .ToArray();
                foreach (var step in selected)
                {
                    points.Add(new StraightDriveTracePoint(
                        candidate.Id,
                        lapIndex + 1,
                        cumulative + step.EndDistanceMeters,
                        $"Straight {sample.SegmentId}",
                        step.EndDistanceMeters,
                        sample.TravelledMeters - step.EndDistanceMeters,
                        step.ExitSpeedMetersPerSecond,
                        step.BaselineEnvelopeMultiplier,
                        step.ExperimentalEnvelopeMultiplier - step.BaselineEnvelopeMultiplier,
                        step.ExperimentalEnvelopeMultiplier,
                        step.NetAccelerationMetersPerSecondSquared,
                        step.AllowedEndSpeedMetersPerSecond,
                        step.PreparationApplied,
                        null,
                        null,
                        null));
                }
            }
            else if (sample.ContinuousCornerProfile is { } corner)
            {
                var selected = corner.Nodes.Where((node, index) => index == 0 || index == corner.Nodes.Count - 1
                    || MathF.Abs(node.CornerProgress * 20f - MathF.Round(node.CornerProgress * 20f)) < .006f);
                foreach (var node in selected)
                {
                    var distanceWithinSample = sample.TravelledMeters
                        * Math.Clamp((node.CornerProgress - sample.CornerPhase!.Value.CornerProgress)
                            / MathF.Max(1e-6f, sample.CornerPhase.Value.SegmentEndCornerProgress
                                - sample.CornerPhase.Value.CornerProgress), 0f, 1f);
                    points.Add(new StraightDriveTracePoint(
                        candidate.Id,
                        lapIndex + 1,
                        cumulative + distanceWithinSample,
                        $"{sample.SegmentType} {sample.SegmentId}",
                        null, null,
                        node.SpeedMetersPerSecond,
                        null, null, null, null, null, null,
                        node.CornerProgress,
                        node.EnvelopeSpeedMetersPerSecond,
                        node.DriveAvailability));
                }
            }

            cumulative += sample.TravelledMeters;
        }

        return Array.AsReadOnly(points
            .OrderBy(item => item.CumulativeLapDistanceMeters)
            .ThenBy(item => item.Segment, StringComparer.Ordinal)
            .ToArray());
    }

    private static PeakLocation LocatePeak(CalibrationTrace trace, CalibrationStepSample sample)
    {
        if (sample.SegmentType != SegmentType.Straight)
            return new PeakLocation($"Corner {sample.SegmentId} @ {F(sample.PeakCornerProgress)}", null, null);
        if (sample.StraightDriveCalibrationSteps is not { Count: > 0 } steps)
            return new PeakLocation($"StandingStart Straight {sample.SegmentId}", null, null);

        var point = steps.SelectMany(step => new[]
            {
                (Speed: step.EntrySpeedMetersPerSecond, Distance: step.StartDistanceMeters),
                (Speed: step.ExitSpeedMetersPerSecond, Distance: step.EndDistanceMeters),
            }).OrderByDescending(item => item.Speed).ThenBy(item => item.Distance).First();
        var progress = point.Distance / sample.TravelledMeters;
        var finalRaceSegment = sample.LapIndex == trace.Options.Laps - 1 && sample.SegmentIndex == 8;
        float? distanceBeforeCorner = finalRaceSegment ? null : sample.SegmentIndex switch
        {
            0 or 4 => sample.TravelledMeters - point.Distance,
            8 => sample.TravelledMeters - point.Distance + 31f,
            _ => null,
        };
        return new PeakLocation($"Straight {sample.SegmentId} @ {progress.ToString("0.######", CultureInfo.InvariantCulture)}",
            progress, distanceBeforeCorner);
    }

    private static string CornerLawFingerprint(StraightDriveEnvelopeCandidate candidate)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var rider = new RiderState(new RiderProfile(1, "corner freeze", Skills(), RiderStyle.Balanced), 1)
        {
            LateralPosition = 1f,
            Speed = 25f,
            ActiveSetup = BikeSetup.Neutral,
        };
        rider.RestorePosition(RiderPosition.Create(1, 1, 0f, track.Segments.Count));
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var snapshot = engine.CaptureSnapshot(track,
            TrackState.CreateDefault(track, CalibrationScenarioCatalog.Baseline.Surface),
            new[] { rider }, new SimulationStepContext(HeatId, 0, 0, 1, FixedSeed, 4));
        var resolved = engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions
        {
            Laps = 4,
            Seed = FixedSeed,
            IncidentFrequency = 0f,
            EnableLogging = false,
            StraightDriveEnvelopeAdjustment = candidate.Adjustment,
        });
        var change = resolved.Changes.Single();
        var diagnostic = resolved.Diagnostics.Single();
        return string.Join("|", change.Outcome, R(change.Speed), R(change.ElapsedTimeSeconds),
            string.Join(";", diagnostic.ContinuousCornerProfile!.Nodes.Select(node =>
                string.Join(",", R(node.CornerProgress), R(node.SpeedMetersPerSecond),
                    R(node.EnvelopeSpeedMetersPerSecond), R(node.DriveAvailability)))));
    }

    private static string LegacyFingerprint(StraightDriveEnvelopeCandidate candidate)
    {
        var track = Track.CreateExample();
        var rider = new RiderState(new RiderProfile(1, "legacy freeze", Skills(), RiderStyle.Balanced), 1)
        {
            Speed = 19f,
            ActiveSetup = BikeSetup.Neutral,
        };
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track), new[] { rider },
            new SimulationStepContext(HeatId, 0, 0, 0, FixedSeed, 1, UseLegacyPhysics: true));
        var resolved = engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions
        {
            Laps = 1,
            Seed = FixedSeed,
            IncidentFrequency = 0f,
            StraightDriveEnvelopeAdjustment = candidate.Adjustment,
        });
        var change = resolved.Changes.Single();
        return string.Join("|", change.Outcome, change.Lane, R(change.Speed), R(change.ElapsedTimeSeconds));
    }

    private static string TraceHash(CalibrationTrace trace)
    {
        var payload = CalibrationCsvExporter.ExportSteps(trace) + "\n"
            + string.Join("\n", trace.RiderSummaries.Select(item => string.Join(",",
                item.RiderId, item.Status, item.LapsCompleted, R(item.TotalTimeSeconds),
                R(item.TotalDistanceMeters), R(item.MaxSpeedMetersPerSecond))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

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

    private static bool IsOrdinaryStraight(CalibrationStepSample sample) =>
        sample.SegmentType == SegmentType.Straight
        && sample.StandingStartProfileTotalTimeSeconds is null;

    private static RiderSkills Skills(float speed = 50f, float slideControl = 50f) =>
        new(50f, speed, slideControl, 50f, 50f, 50f);

    private static double Median(IEnumerable<float> values) => Quantile(values.Select(item => (double)item));
    private static double Quantile(IEnumerable<double> values) =>
        CalibrationDistribution.LinearQuantile(values.Order().ToArray(), .5d);
    private static string Id(float value) => value.ToString("000", CultureInfo.InvariantCulture);
    private static string F(float? value) => value?.ToString("0.######", CultureInfo.InvariantCulture) ?? "n/a";
    private static string R(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private sealed record RiderFixture(int RiderId, float LateralPosition, RiderSkills Skills, BikeSetup Setup);
    private sealed record PeakLocation(string Label, float? StraightProgress, float? DistanceBeforeCornerMeters);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane, 0f);
        public RiderDecision Decide(RiderDecisionContext context) => new(context.Rider.Lane, 0f);
    }
}
