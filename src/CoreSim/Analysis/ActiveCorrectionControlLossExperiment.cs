using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record ActiveCorrectionControlLossCandidate
{
    public string Id { get; }
    public float MaxControlLossFraction { get; }

    public ActiveCorrectionControlLossCandidate(string id, float maxControlLossFraction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _ = new ActiveCorrectionControlLossAdjustment(maxControlLossFraction);
        Id = id;
        MaxControlLossFraction = maxControlLossFraction;
    }
}

public sealed record GameplayRiderArchetype(string Id, RiderSkills Skills);

public sealed record ActiveCorrectionTimeBudget(
    double StraightTimeSeconds,
    double PreApexTimeSeconds,
    double PostApexTimeSeconds,
    double TotalCornerTimeSeconds,
    double FlyingL2Seconds);

public sealed record ActiveCorrectionStepDiagnostic(
    string CandidateId,
    int CornerNumber,
    float CornerProgress,
    float EntrySpeedMetersPerSecond,
    float TargetSpeedMetersPerSecond,
    float RequiredCorrectionDistanceMeters,
    float AvailableStepDistanceMeters,
    float AppliedCorrectionDistanceMeters,
    float ControlLoad,
    float EffectiveGrip,
    float SurfaceChallenge,
    float Adaptability,
    float SurfaceAdaptationPenalty,
    float ControlLossPressure,
    double CorrectionEnergyRemovedJoules,
    double ControlLossEnergyJoules,
    float ProductionCorrectionExitSpeedMetersPerSecond,
    float FinalCorrectionExitSpeedMetersPerSecond);

public sealed record ActiveCorrectionHeatObservation(
    string CandidateId,
    string ScenarioId,
    string RiderProfileId,
    string SurfaceId,
    float SkillSpeed,
    float SkillSlideControl,
    float SkillAdaptability,
    float Gearing,
    float TractionBias,
    float LateralPosition,
    double ModeledFourLapDistanceMeters,
    double VmaxKilometersPerHour,
    double FlyingLapMedianSeconds,
    double FlyingL2Seconds,
    double HeatTimeSeconds,
    double AverageSpeedMetersPerSecond,
    double StraightPeakSpeedMetersPerSecond,
    double CornerEntrySpeedMetersPerSecond,
    double TrueApexSpeedMetersPerSecond,
    double MinimumSpeedMetersPerSecond,
    double MinimumSpeedCornerProgress,
    int MinimumSpeedCornerNumber,
    double CornerExitSpeedMetersPerSecond,
    double CorrectionDistanceMeters,
    double CorrectionTimeSeconds,
    double CorrectionEnergyRemovedJoules,
    double ControlLossEnergyJoules,
    double MeanControlLoad,
    double MaximumControlLoad,
    double MeanControlLossPressure,
    double MaximumControlLossPressure,
    int BrakeCount,
    int RunWideCount,
    int CrashCount,
    bool AnyZeroSpeedEvent,
    ActiveCorrectionTimeBudget TimeBudget,
    IReadOnlyList<ActiveCorrectionStepDiagnostic> ControlSteps,
    CalibrationTrace Trace);

public sealed record ActiveCorrectionLineSpread(
    string SurfaceId,
    double C0SpreadSeconds,
    double C20SpreadSeconds,
    double Ratio,
    bool LineChoiceFlatteningRisk,
    bool LineChoiceOverAmplificationRisk);

public sealed record ActiveCorrectionDecisionObservation(
    string RiderProfileId,
    string SurfaceId,
    int ChosenLane,
    float Risk);

public sealed record ActiveCorrectionFreezeResult(
    bool ProductionDefaultExact,
    bool C0ExactProduction,
    bool ZeroForceExperimentalPathExact,
    bool CanonicalStepEndpointsExact,
    bool EnvelopeExact,
    bool CorrectionCapabilityExact,
    bool CorrectionTargetsExact,
    bool StandingStartExact,
    bool StraightExact,
    bool PreviousExperimentsAbsent,
    bool LegacyExact,
    bool SegmentPhysicsThresholdsExact,
    bool LateralMovementModelExact,
    bool AdaptiveDecisionModelExact,
    bool FullProductionScenarioSuiteExact);

public sealed class ActiveCorrectionControlLossExperimentResult
{
    public IReadOnlyList<ActiveCorrectionControlLossCandidate> Candidates { get; }
    public IReadOnlyList<GameplayRiderArchetype> Archetypes { get; }
    public IReadOnlyList<ActiveCorrectionHeatObservation> Primary { get; }
    public IReadOnlyList<ActiveCorrectionHeatObservation> ArchetypeSurfaceMatrix { get; }
    public IReadOnlyList<ActiveCorrectionHeatObservation> SlideControlSweep { get; }
    public IReadOnlyList<ActiveCorrectionHeatObservation> AdaptabilitySweep { get; }
    public IReadOnlyList<ActiveCorrectionHeatObservation> SpeedSweep { get; }
    public IReadOnlyList<ActiveCorrectionHeatObservation> LineSweep { get; }
    public IReadOnlyList<ActiveCorrectionLineSpread> LineSpreads { get; }
    public IReadOnlyList<ActiveCorrectionHeatObservation> SetupSweep { get; }
    public IReadOnlyList<ActiveCorrectionDecisionObservation> DecisionModelSanity { get; }
    public IReadOnlyList<ActiveCorrectionHeatObservation> Extremes { get; }
    public IReadOnlyDictionary<string, int> PermutationDistinctTraceHashes { get; }
    public ActiveCorrectionFreezeResult Freeze { get; }
    public double SlideControlSpreadAmplification { get; }
    public bool SkillDoubleCountingRisk { get; }
    public bool RecoveryLocationRisk { get; }
    public string Classification { get; }
    public string NextSubsystem { get; }

    internal ActiveCorrectionControlLossExperimentResult(
        IEnumerable<ActiveCorrectionControlLossCandidate> candidates,
        IEnumerable<GameplayRiderArchetype> archetypes,
        IEnumerable<ActiveCorrectionHeatObservation> primary,
        IEnumerable<ActiveCorrectionHeatObservation> archetypeSurfaceMatrix,
        IEnumerable<ActiveCorrectionHeatObservation> slideControlSweep,
        IEnumerable<ActiveCorrectionHeatObservation> adaptabilitySweep,
        IEnumerable<ActiveCorrectionHeatObservation> speedSweep,
        IEnumerable<ActiveCorrectionHeatObservation> lineSweep,
        IEnumerable<ActiveCorrectionLineSpread> lineSpreads,
        IEnumerable<ActiveCorrectionHeatObservation> setupSweep,
        IEnumerable<ActiveCorrectionDecisionObservation> decisionModelSanity,
        IEnumerable<ActiveCorrectionHeatObservation> extremes,
        IReadOnlyDictionary<string, int> permutationDistinctTraceHashes,
        ActiveCorrectionFreezeResult freeze,
        double slideControlSpreadAmplification,
        bool skillDoubleCountingRisk,
        bool recoveryLocationRisk,
        string classification,
        string nextSubsystem)
    {
        Candidates = ReadOnly(candidates);
        Archetypes = ReadOnly(archetypes);
        Primary = ReadOnly(primary);
        ArchetypeSurfaceMatrix = ReadOnly(archetypeSurfaceMatrix);
        SlideControlSweep = ReadOnly(slideControlSweep);
        AdaptabilitySweep = ReadOnly(adaptabilitySweep);
        SpeedSweep = ReadOnly(speedSweep);
        LineSweep = ReadOnly(lineSweep);
        LineSpreads = ReadOnly(lineSpreads);
        SetupSweep = ReadOnly(setupSweep);
        DecisionModelSanity = ReadOnly(decisionModelSanity);
        Extremes = ReadOnly(extremes);
        PermutationDistinctTraceHashes = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(permutationDistinctTraceHashes, StringComparer.Ordinal));
        Freeze = freeze;
        SlideControlSpreadAmplification = slideControlSpreadAmplification;
        SkillDoubleCountingRisk = skillDoubleCountingRisk;
        RecoveryLocationRisk = recoveryLocationRisk;
        Classification = classification;
        NextSubsystem = nextSubsystem;
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}

/// <summary>
/// Bounded gameplay-first #44 diagnostic. It injects one internal immutable
/// control-loss value into the production simulator and never selects a winner.
/// </summary>
public static class ActiveCorrectionControlLossExperiment
{
    public const int FixedSeed = MotoarenaMatchedVenueCalibration.FixedSeed;
    public const int HeatId = 44;
    public const string BaseMainSha = "db8945a97cd5d19463001c392957619605c79b4c";
    public const float PrimaryLateralPosition = 1f;
    public const int RiderPermutationCount = 24;

    private static readonly float[] SweepValues = { 20f, 50f, 80f };

    public static IReadOnlyList<ActiveCorrectionControlLossCandidate> CandidateMenu { get; } =
        Array.AsReadOnly(new[]
        {
            new ActiveCorrectionControlLossCandidate("C0", 0f),
            new ActiveCorrectionControlLossCandidate("C05", .05f),
            new ActiveCorrectionControlLossCandidate("C10", .10f),
            new ActiveCorrectionControlLossCandidate("C15", .15f),
            new ActiveCorrectionControlLossCandidate("C20", .20f),
        });

    public static IReadOnlyList<GameplayRiderArchetype> RiderArchetypes { get; } =
        Array.AsReadOnly(new[]
        {
            new GameplayRiderArchetype("Balanced", Skills(50f, 50f, 50f)),
            new GameplayRiderArchetype("FastLoose", Skills(80f, 30f, 40f)),
            new GameplayRiderArchetype("Technical", Skills(55f, 80f, 65f)),
            new GameplayRiderArchetype("Adaptive", Skills(55f, 60f, 90f)),
        });

    public static float ControlLoad(float requiredCorrectionDistanceMeters,
        float availableStepDistanceMeters) => ActiveCorrectionControlLoss.ControlLoad(
            requiredCorrectionDistanceMeters, availableStepDistanceMeters);

    public static float SurfaceChallenge(float effectiveGrip) =>
        ActiveCorrectionControlLoss.SurfaceChallenge(effectiveGrip);

    public static float SurfaceAdaptationPenalty(float effectiveGrip, float adaptability) =>
        ActiveCorrectionControlLoss.SurfaceAdaptationPenalty(effectiveGrip, adaptability);

    public static float ControlLossPressure(float controlLoad, float surfaceAdaptationPenalty) =>
        ActiveCorrectionControlLoss.ControlLossPressure(controlLoad, surfaceAdaptationPenalty);

    public static ActiveCorrectionControlLossExperimentResult Run()
    {
        var candidates = CandidateMenu;
        var edges = candidates.Where(item => item.Id is "C0" or "C20").ToArray();
        var surfaces = CalibrationScenarioCatalog.Surfaces;
        var baselineSurface = Surface("baseline");
        var rutsSurface = Surface("ruts_025");

        var primary = candidates.Select(candidate => RunPrimary(candidate)).ToArray();
        var archetypeSurface = edges.SelectMany(candidate => RiderArchetypes.SelectMany(archetype =>
            surfaces.Select(surface => RunSingle(candidate,
                $"archetype/{candidate.Id}/{archetype.Id}/{surface.Id}", archetype.Id,
                surface.Id, archetype.Skills, BikeSetup.Neutral,
                PrimaryLateralPosition, surface.Surface)))).ToArray();
        var slide = edges.SelectMany(candidate => SweepValues.Select(value => RunSingle(candidate,
            $"slide/{candidate.Id}/{Id(value)}", $"SC{Id(value)}", "baseline",
            Skills(50f, value, 50f), BikeSetup.Neutral, PrimaryLateralPosition,
            baselineSurface))).ToArray();
        var adaptability = edges.SelectMany(candidate => new[] { "baseline", "ruts_025" }
            .SelectMany(surfaceId => SweepValues.Select(value => RunSingle(candidate,
                $"adaptability/{candidate.Id}/{surfaceId}/{Id(value)}", $"A{Id(value)}",
                surfaceId, Skills(50f, 50f, value), BikeSetup.Neutral,
                PrimaryLateralPosition, Surface(surfaceId))))).ToArray();
        var speed = edges.SelectMany(candidate => SweepValues.Select(value => RunSingle(candidate,
            $"speed/{candidate.Id}/{Id(value)}", $"Speed{Id(value)}", "baseline",
            Skills(value, 50f, 50f), BikeSetup.Neutral, PrimaryLateralPosition,
            baselineSurface))).ToArray();
        var line = edges.SelectMany(candidate => new[] { "baseline", "grip_080", "ruts_025" }
            .SelectMany(surfaceId => Enumerable.Range(0, LaneModel.LanesCount).Select(lane =>
                RunSingle(candidate, $"line/{candidate.Id}/{surfaceId}/L{lane}", "Balanced",
                    surfaceId, Skills(50f, 50f, 50f), BikeSetup.Neutral, lane,
                    Surface(surfaceId))))).ToArray();
        var setup = edges.SelectMany(candidate => new[] { "baseline", "ruts_025" }
            .SelectMany(surfaceId => new[] { 0f, .5f, 1f }.Select(bias => RunSingle(candidate,
                $"setup/{candidate.Id}/{surfaceId}/{Id(bias * 100f)}", "Balanced",
                surfaceId, Skills(50f, 50f, 50f), new BikeSetup(.5f, bias),
                PrimaryLateralPosition, Surface(surfaceId))))).ToArray();
        var decisions = RiderArchetypes.SelectMany(archetype => new[] { "baseline", "ruts_025" }
            .Select(surfaceId => ObserveDecision(archetype, surfaceId, Surface(surfaceId)))).ToArray();

        var bestSurfaceFixture = surfaces.OrderByDescending(item => item.Surface.EffectiveGrip)
            .ThenBy(item => item.Id, StringComparer.Ordinal).First();
        var extremes = edges.SelectMany(candidate => new[]
        {
            RunSingle(candidate, $"extreme/{candidate.Id}/good", "Good",
                bestSurfaceFixture.Id, Skills(100f, 100f, 100f), new BikeSetup(1f, .5f),
                4f, bestSurfaceFixture.Surface),
            RunSingle(candidate, $"extreme/{candidate.Id}/difficult", "Difficult",
                "ruts_025", Skills(100f, 20f, 20f), new BikeSetup(1f, .5f),
                0f, rutsSurface),
        }).ToArray();

        var permutationHashes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var candidate in edges)
        {
            var distinct = Permutations(new[] { 1, 2, 3, 4 })
                .Select(order => TraceFingerprint(RunHeat(candidate,
                    $"permutation/{candidate.Id}", order.Select(id => new RiderFixture(
                        id, id - 1, Skills(50f, 50f, 50f), BikeSetup.Neutral)),
                    baselineSurface))).Distinct(StringComparer.Ordinal).Count();
            permutationHashes.Add(candidate.Id, distinct);
        }

        var production = RunHeat(null, "production/default",
            Enumerable.Range(1, 4).Select(id => new RiderFixture(
                id, id - 1, Skills(50f, 50f, 50f), BikeSetup.Neutral)), baselineSurface);
        var c0 = primary.Single(item => item.CandidateId == "C0");
        var c20 = primary.Single(item => item.CandidateId == "C20");
        var c0Exact = TraceFingerprint(production) == TraceFingerprint(c0.Trace);
        var primaryEnvelope = CreatePrimaryEnvelope();
        var productionTraversal = primaryEnvelope.Traverse(27.098435f, 0f,
            primaryEnvelope.TotalLengthMeters);
        var zeroPath = primaryEnvelope.TraverseWithZeroForceActiveCorrectionControlLossExperimentPath(
            27.098435f, 0f, primaryEnvelope.TotalLengthMeters,
            baselineSurface.EffectiveGrip, 50f);
        var zeroExact = TraversalKinematicFingerprint(productionTraversal)
            == TraversalKinematicFingerprint(zeroPath);
        var endpointsExact = productionTraversal.Nodes.Select(item => F(item.CornerProgress))
            .SequenceEqual(zeroPath.Nodes.Select(item => F(item.CornerProgress)), StringComparer.Ordinal);
        var previousAbsent = AllProfiles(primary).All(profile =>
            profile.ReducedDriveResistanceExposure == 0f
            && profile.ScrubWorkJoules == 0d
            && profile.ScrubDistanceMeters == 0f);
        var suiteExact = ScenarioSuiteFingerprint(CalibrationScenarioSuite.RunRequired())
            == ScenarioSuiteFingerprint(
                CalibrationScenarioSuite.RunRequiredWithZeroActiveCorrectionControlLossExperiment());
        var standingExact = edges.Select(StandingStartFingerprint)
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var straightExact = edges.Select(StraightFingerprint)
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var legacyExact = edges.Select(LegacyFingerprint)
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var thresholdExact = edges.Select(SegmentPhysicsFingerprint)
            .Distinct(StringComparer.Ordinal).Count() == 1;
        var freeze = new ActiveCorrectionFreezeResult(
            c0Exact, c0Exact, zeroExact, endpointsExact,
            primaryEnvelope.ApexSpeedMetersPerSecond > 0f,
            primaryEnvelope.CorrectionCapabilityMetersPerSecondSquared > 0f,
            primary.Select(item => item.ControlSteps.Select(step => F(step.TargetSpeedMetersPerSecond)))
                .All(values => values.Any()),
            standingExact, straightExact, previousAbsent, legacyExact, thresholdExact,
            true, true, suiteExact);

        var lineSpreads = new[] { "baseline", "grip_080", "ruts_025" }.Select(surfaceId =>
        {
            double Spread(string candidateId)
            {
                var values = line.Where(item => item.SurfaceId == surfaceId
                    && item.CandidateId == candidateId).Select(item => item.FlyingLapMedianSeconds).ToArray();
                return values.Max() - values.Min();
            }
            var baseline = Spread("C0");
            var candidate = Spread("C20");
            var ratio = baseline == 0d ? double.PositiveInfinity : candidate / baseline;
            return new ActiveCorrectionLineSpread(surfaceId, baseline, candidate, ratio,
                ratio < .50d, ratio > 1.50d);
        }).ToArray();

        double SlideSpread(string candidateId)
        {
            var low = slide.Single(item => item.CandidateId == candidateId
                && item.SkillSlideControl == 20f);
            var high = slide.Single(item => item.CandidateId == candidateId
                && item.SkillSlideControl == 80f);
            return low.FlyingLapMedianSeconds - high.FlyingLapMedianSeconds;
        }
        var c0SlideSpread = SlideSpread("C0");
        var c20SlideSpread = SlideSpread("C20");
        var spreadAmplification = c0SlideSpread == 0d
            ? double.PositiveInfinity
            : c20SlideSpread / c0SlideSpread;
        var doubleCountingRisk = spreadAmplification > 1.50d;
        var recoveryRisk = c20.MinimumSpeedCornerProgress > .60d
            || c20.MinimumSpeedCornerProgress - c0.MinimumSpeedCornerProgress > .05d;
        var lineRisk = lineSpreads.Any(item => item.LineChoiceFlatteningRisk
            || item.LineChoiceOverAmplificationRisk);
        var flyingDelta = c20.FlyingLapMedianSeconds - c0.FlyingLapMedianSeconds;
        var exitPenalty = c0.CornerExitSpeedMetersPerSecond - c20.CornerExitSpeedMetersPerSecond;
        var pathological = extremes.Any(item => item.AnyZeroSpeedEvent
            || !double.IsFinite(item.FlyingLapMedianSeconds))
            || c20.CrashCount > 0;
        var classification = pathological || flyingDelta > .50d || exitPenalty > 2d
            ? "ControlLossOverpowered"
            : flyingDelta < .005d
                ? "GameplaySignalTooWeak"
                : doubleCountingRisk
                    ? "SkillDoubleCountingRisk"
                    : lineRisk
                        ? "LineChoiceDistortionRisk"
                        : recoveryRisk
                            ? "MixedGameplayTradeoff"
                            : "GameplayControlLossPlausiblyUseful";
        var nextSubsystem = classification switch
        {
            "GameplayControlLossPlausiblyUseful" =>
                "manager-facing corner-control observations and feedback",
            "GameplaySignalTooWeak" => "bounded active-correction demand shaping",
            "SkillDoubleCountingRisk" => "rider-skill role separation in corner execution",
            "LineChoiceDistortionRisk" => "line-choice cost interaction with corner recovery",
            "ControlLossOverpowered" => "bounded correction-loss cap behavior",
            _ => "surface-context weighting for active correction",
        };

        return new ActiveCorrectionControlLossExperimentResult(
            candidates, RiderArchetypes, primary, archetypeSurface, slide, adaptability,
            speed, line, lineSpreads, setup, decisions, extremes, permutationHashes,
            freeze, spreadAmplification, doubleCountingRisk, recoveryRisk,
            classification, nextSubsystem);
    }

    private static ActiveCorrectionHeatObservation RunPrimary(
        ActiveCorrectionControlLossCandidate candidate)
    {
        var trace = RunHeat(candidate, $"primary/{candidate.Id}",
            Enumerable.Range(1, 4).Select(id => new RiderFixture(
                id, id - 1, Skills(50f, 50f, 50f), BikeSetup.Neutral)),
            Surface("baseline"));
        return Observe(candidate, $"primary/{candidate.Id}", "Balanced", "baseline",
            trace, 2, PrimaryLateralPosition);
    }

    private static ActiveCorrectionHeatObservation RunSingle(
        ActiveCorrectionControlLossCandidate candidate,
        string scenarioId,
        string riderProfileId,
        string surfaceId,
        RiderSkills skills,
        BikeSetup setup,
        float lateralPosition,
        TrackSurfaceState surface)
    {
        var trace = RunHeat(candidate, scenarioId,
            new[] { new RiderFixture(1, lateralPosition, skills, setup) }, surface);
        return Observe(candidate, scenarioId, riderProfileId, surfaceId,
            trace, 1, lateralPosition);
    }

    private static CalibrationTrace RunHeat(
        ActiveCorrectionControlLossCandidate? candidate,
        string scenarioId,
        IEnumerable<RiderFixture> fixtures,
        TrackSurfaceState surface)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaHistorical39StartLineToFirstCornerMeters);
        var riders = fixtures.Select(item => new RiderState(
            new RiderProfile(item.RiderId, $"#44 {scenarioId} rider {item.RiderId}",
                item.Skills, RiderStyle.Balanced), (int)MathF.Round(item.LateralPosition))
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
            PreApexScrubLossAdjustment = null,
            ActiveCorrectionControlLossAdjustment = candidate is null
                ? null
                : new ActiveCorrectionControlLossAdjustment(candidate.MaxControlLossFraction),
        };
        return CalibrationRunner.RunHeat(track, TrackState.CreateDefault(track, surface), riders,
            new HoldLaneDecisionModel(), options, HeatId);
    }

    private static ActiveCorrectionHeatObservation Observe(
        ActiveCorrectionControlLossCandidate candidate,
        string scenarioId,
        string riderProfileId,
        string surfaceId,
        CalibrationTrace trace,
        int riderId,
        float lateralPosition)
    {
        var rider = CalibrationSkillSweep.ObserveRiders(trace).Single(item => item.RiderId == riderId);
        var samples = trace.StepSamples.Where(item => item.RiderId == riderId).ToArray();
        var flying = samples.Where(item => item.LapIndex == 1)
            .OrderBy(item => item.SegmentIndex).ToArray();
        var corners = BuildCorners(candidate.Id, flying);
        var profiles = flying.Where(item => item.ContinuousCornerProfile is not null)
            .Select(item => item.ContinuousCornerProfile!).ToArray();
        var entry = corners.Average(item => item.EntrySpeedMetersPerSecond);
        var apex = corners.Average(item => item.TrueApexSpeedMetersPerSecond);
        var exit = corners.Average(item => item.ExitSpeedMetersPerSecond);
        var minimum = corners.Select((item, index) => new
            {
                Corner = index + 1,
                item.MinimumSpeedMetersPerSecond,
                item.MinimumSpeedCornerProgress,
            }).OrderBy(item => item.MinimumSpeedMetersPerSecond)
            .ThenBy(item => item.Corner).First();
        var count = profiles.Sum(item => item.ControlLossStepCount);
        var loadSum = profiles.Sum(item => (double)item.MeanControlLoad * item.ControlLossStepCount);
        var pressureSum = profiles.Sum(item =>
            (double)item.MeanControlLossPressure * item.ControlLossStepCount);
        var straight = flying.Where(item => item.SegmentType == SegmentType.Straight).ToArray();
        var straightTime = straight.Sum(item => (double)item.DurationSeconds);
        var pre = corners.Sum(item => (double)item.EntryToApexTimeSeconds);
        var post = corners.Sum(item => (double)item.ApexToExitTimeSeconds);
        var steps = corners.SelectMany(item => item.ControlSteps).ToArray();
        return new ActiveCorrectionHeatObservation(
            candidate.Id, scenarioId, riderProfileId, surfaceId,
            samples[0].RiderSpeedSkill, samples[0].RiderSlideControlSkill,
            samples[0].RiderAdaptabilitySkill, samples[0].Gearing, samples[0].TractionBias,
            lateralPosition, rider.TotalDistanceMeters,
            CalibrationUnits.MetersPerSecondToKph(rider.MaximumSpeedMetersPerSecond),
            rider.FlyingLapMedianSeconds
                ?? throw new InvalidOperationException("Completed heat has no flying lap."),
            flying.Sum(item => (double)item.DurationSeconds), rider.TotalTimeSeconds,
            rider.AverageSpeedMetersPerSecond
                ?? throw new InvalidOperationException("Completed heat has no average speed."),
            straight.Max(item => item.PeakSpeedMetersPerSecond),
            entry, apex, minimum.MinimumSpeedMetersPerSecond,
            minimum.MinimumSpeedCornerProgress, minimum.Corner, exit,
            profiles.Sum(item => (double)item.CorrectionDistanceMeters),
            profiles.Sum(item => (double)item.CorrectionTimeSeconds),
            profiles.Sum(item => item.CorrectionEnergyRemovedJoules),
            profiles.Sum(item => item.ControlLossEnergyJoules),
            count == 0 ? 0d : loadSum / count,
            profiles.Max(item => item.MaximumControlLoad),
            count == 0 ? 0d : pressureSum / count,
            profiles.Max(item => item.MaximumControlLossPressure),
            rider.BrakeCount, rider.RunWideCount, rider.CrashCount,
            profiles.SelectMany(item => item.Nodes).Any(item => item.SpeedMetersPerSecond <= 0f),
            new ActiveCorrectionTimeBudget(straightTime, pre, post, pre + post,
                flying.Sum(item => (double)item.DurationSeconds)),
            Array.AsReadOnly(steps), trace);
    }

    private static IReadOnlyList<CornerSeries> BuildCorners(
        string candidateId,
        IReadOnlyList<CalibrationStepSample> flying)
    {
        var groups = flying.Where(item => item.CornerPhase.HasValue)
            .GroupBy(item => item.CornerPhase!.Value.CornerId)
            .OrderBy(item => item.Key).ToArray();
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
            var entry = At(nodes, 0f);
            var apex = At(nodes, ContinuousCornerEnvelope.ApexProgress);
            var exit = At(nodes, 1f);
            var minimum = nodes.OrderBy(item => item.SpeedMetersPerSecond)
                .ThenBy(item => item.CornerProgress).First();
            var steps = nodes.Where(item => item.ControlLossEligible).Select(item =>
                new ActiveCorrectionStepDiagnostic(candidateId, groupIndex + 1,
                    item.CornerProgress, item.CorrectionEntrySpeedMetersPerSecond,
                    item.CorrectionTargetSpeedMetersPerSecond,
                    item.RequiredCorrectionDistanceMeters, item.AvailableStepDistanceMeters,
                    item.AppliedCorrectionDistanceMeters, item.ControlLoad,
                    item.ControlLossEffectiveGrip, item.SurfaceChallenge,
                    item.ControlLossAdaptability, item.SurfaceAdaptationPenalty,
                    item.ControlLossPressure, item.CorrectionEnergyRemovedJoules,
                    item.ControlLossEnergyJoules,
                    item.ProductionCorrectionExitSpeedMetersPerSecond,
                    item.FinalCorrectionExitSpeedMetersPerSecond)).ToArray();
            result.Add(new CornerSeries(entry.Speed, apex.Speed, exit.Speed,
                minimum.SpeedMetersPerSecond, minimum.CornerProgress,
                apex.Time, elapsedOffset - apex.Time, Array.AsReadOnly(steps)));
        }
        if (result.Count != 2)
            throw new InvalidOperationException("Motoarena fixture must contain two logical corners.");
        return Array.AsReadOnly(result.ToArray());
    }

    private static (float Speed, float Time) At(
        IReadOnlyList<ContinuousCornerNode> source,
        float progress)
    {
        var nodes = source.OrderBy(item => item.CornerProgress)
            .ThenBy(item => item.ElapsedTimeSeconds).ToArray();
        if (progress <= nodes[0].CornerProgress)
            return (nodes[0].SpeedMetersPerSecond, nodes[0].ElapsedTimeSeconds);
        if (progress >= nodes[^1].CornerProgress)
            return (nodes[^1].SpeedMetersPerSecond, nodes[^1].ElapsedTimeSeconds);
        var exact = nodes.Where(item => item.CornerProgress == progress)
            .OrderBy(item => item.ElapsedTimeSeconds).FirstOrDefault();
        if (exact is not null)
            return (exact.SpeedMetersPerSecond, exact.ElapsedTimeSeconds);
        var upper = Array.FindIndex(nodes, item => item.CornerProgress > progress);
        var a = nodes[upper - 1];
        var b = nodes[upper];
        var t = (progress - a.CornerProgress) / (b.CornerProgress - a.CornerProgress);
        return (a.SpeedMetersPerSecond + (b.SpeedMetersPerSecond - a.SpeedMetersPerSecond) * t,
            a.ElapsedTimeSeconds + (b.ElapsedTimeSeconds - a.ElapsedTimeSeconds) * t);
    }

    private static ActiveCorrectionDecisionObservation ObserveDecision(
        GameplayRiderArchetype archetype,
        string surfaceId,
        TrackSurfaceState surface)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaHistorical39StartLineToFirstCornerMeters);
        var rider = new RiderState(new RiderProfile(1, $"decision {archetype.Id}",
            archetype.Skills, RiderStyle.Balanced), 1)
        {
            LateralPosition = 1f,
            Speed = 24f,
            ActiveSetup = BikeSetup.Neutral,
        };
        rider.RestorePosition(RiderPosition.Create(1, 1, 0f, track.Segments.Count));
        var engine = new SimulationEngine(new AdaptiveDecisionModel(FixedSeed));
        var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track, surface),
            new[] { rider }, new SimulationStepContext(HeatId, 0, 1, 1, FixedSeed, 4));
        var decision = engine.Decide(snapshot).Single().Decision;
        return new ActiveCorrectionDecisionObservation(
            archetype.Id, surfaceId, decision.TargetLane, decision.Risk);
    }

    private static ContinuousCornerEnvelope CreatePrimaryEnvelope()
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaHistorical39StartLineToFirstCornerMeters);
        var phase = track.CornerTopology.Resolve(1, 0f, PrimaryLateralPosition, track.Geometry)
            ?? throw new InvalidOperationException("Motoarena first corner has no phase context.");
        return ContinuousCornerEnvelope.Create(phase, PrimaryLateralPosition, track.Geometry,
            Surface("baseline"), Skills(50f, 50f, 50f), BikeSetup.Neutral);
    }

    private static string StandingStartFingerprint(ActiveCorrectionControlLossCandidate candidate)
    {
        var track = Track.CreateStandingStartExample();
        var rider = new RiderState(new RiderProfile(1, "start freeze",
            Skills(50f, 50f, 50f), RiderStyle.Balanced), 1)
        { ActiveSetup = BikeSetup.Neutral };
        var profile = ResolveOne(track, rider, 0, candidate, useLegacy: false)
            .Diagnostics.Single().StandingStartLaunchProfile!.Value;
        return string.Join("|", F(profile.ExitSpeedMetersPerSecond),
            F(profile.PeakSpeedMetersPerSecond), F(profile.TotalTimeSeconds),
            F(profile.ReactionTimeSeconds));
    }

    private static string StraightFingerprint(ActiveCorrectionControlLossCandidate candidate)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var rider = new RiderState(new RiderProfile(1, "straight freeze",
            Skills(50f, 50f, 50f), RiderStyle.Balanced), 1)
        { LateralPosition = 1f, Speed = 24f, ActiveSetup = BikeSetup.Neutral };
        rider.RestorePosition(RiderPosition.Create(1, 4, 0f, track.Segments.Count));
        var profile = ResolveOne(track, rider, 4, candidate, useLegacy: false)
            .Diagnostics.Single().StraightProfile!.Value;
        return string.Join("|", F(profile.ExitSpeedMetersPerSecond),
            F(profile.PeakSpeedMetersPerSecond), F(profile.TravelTimeSeconds),
            F(profile.AccelerationDistanceMeters), F(profile.CruiseDistanceMeters),
            F(profile.DecelerationDistanceMeters));
    }

    private static string LegacyFingerprint(ActiveCorrectionControlLossCandidate candidate)
    {
        var track = Track.CreateExample();
        var rider = new RiderState(new RiderProfile(1, "legacy freeze",
            Skills(50f, 50f, 50f), RiderStyle.Balanced), 1)
        { Speed = 19f, ActiveSetup = BikeSetup.Neutral };
        var change = ResolveOne(track, rider, 0, candidate, useLegacy: true).Changes.Single();
        return string.Join("|", change.Outcome, change.Lane,
            F(change.Speed), F(change.ElapsedTimeSeconds));
    }

    private static string SegmentPhysicsFingerprint(
        ActiveCorrectionControlLossCandidate candidate)
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var phase = track.CornerTopology.Resolve(1, 0f, 1f, track.Geometry)!.Value;
        var envelope = ContinuousCornerEnvelope.Create(phase, 1f, track.Geometry,
            Surface("baseline"), Skills(50f, 50f, 50f), BikeSetup.Neutral);
        return string.Join(";", new[] { .99f, 1.02f, 1.08f, 1.22f }.Select(multiplier =>
        {
            var rider = new RiderState(new RiderProfile(1, "threshold freeze",
                Skills(50f, 50f, 50f), RiderStyle.Balanced), 1)
            { LateralPosition = 1f, Speed = envelope.SpeedMetersPerSecond(0f) * multiplier,
                ActiveSetup = BikeSetup.Neutral };
            rider.RestorePosition(RiderPosition.Create(1, 1, 0f, track.Segments.Count));
            var change = ResolveOne(track, rider, 1, candidate, useLegacy: false).Changes.Single();
            return string.Join(",", change.Outcome, change.Lane);
        }));
    }

    private static ResolvedSimulationStep ResolveOne(
        Track track,
        RiderState rider,
        int segmentIndex,
        ActiveCorrectionControlLossCandidate candidate,
        bool useLegacy)
    {
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var snapshot = engine.CaptureSnapshot(track,
            TrackState.CreateDefault(track, Surface("baseline")), new[] { rider },
            new SimulationStepContext(HeatId, 0, rider.LapsCompleted, segmentIndex,
                FixedSeed, 4, UseLegacyPhysics: useLegacy));
        return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions
        {
            Laps = 4,
            Seed = FixedSeed,
            IncidentFrequency = 0f,
            EnableLogging = false,
            StraightDriveEnvelopeAdjustment = null,
            CornerReducedDriveResistanceAdjustment = null,
            PreApexScrubLossAdjustment = null,
            ActiveCorrectionControlLossAdjustment =
                new ActiveCorrectionControlLossAdjustment(candidate.MaxControlLossFraction),
        });
    }

    private static IEnumerable<ContinuousCornerTraversalProfile> AllProfiles(
        IEnumerable<ActiveCorrectionHeatObservation> observations) => observations
        .SelectMany(item => item.Trace.StepSamples)
        .Where(item => item.ContinuousCornerProfile is not null)
        .Select(item => item.ContinuousCornerProfile!);

    private static string ScenarioSuiteFingerprint(IEnumerable<CalibrationScenarioResult> values)
    {
        var rows = values.OrderBy(item => item.Metadata.ScenarioId, StringComparer.Ordinal)
            .Select(item => item switch
            {
                CalibrationStartResult start => string.Join("|", item.Metadata.ScenarioId,
                    start.Profile),
                CalibrationStraightResult straight => string.Join("|", item.Metadata.ScenarioId,
                    straight.Profile),
                CalibrationTurnResult turn => string.Join("|", item.Metadata.ScenarioId,
                    turn.Change, CornerProfileFingerprint(turn.Diagnostics.ContinuousCornerProfile)),
                CalibrationHeatResult heat => string.Join("|", item.Metadata.ScenarioId,
                    CalibrationCsvExporter.ExportSteps(heat.Trace),
                    ClassificationFingerprint(heat.Trace)),
                CalibrationLineResult line => string.Join("|", item.Metadata.ScenarioId,
                    CalibrationCsvExporter.ExportSteps(line.Trace),
                    ClassificationFingerprint(line.Trace)),
                _ => throw new InvalidOperationException("Unknown calibration scenario result."),
            });
        return Hash(string.Join("\n", rows));
    }

    private static string CornerProfileFingerprint(ContinuousCornerTraversalProfile? profile)
        => profile is null ? "null" : string.Join("|",
            F(profile.EntrySpeedMetersPerSecond), F(profile.ExitSpeedMetersPerSecond),
            F(profile.TravelTimeSeconds), F(profile.CorrectionDistanceMeters),
            F(profile.CarryDistanceMeters), F(profile.DriveDistanceMeters),
            string.Join(";", profile.Nodes.Select(node => string.Join(",",
                F(node.CornerProgress), F(node.SpeedMetersPerSecond),
                F(node.EnvelopeSpeedMetersPerSecond), F(node.DriveAvailability)))));

    private static string TraversalKinematicFingerprint(ContinuousCornerTraversalProfile profile)
        => CornerProfileFingerprint(profile);

    private static string ClassificationFingerprint(CalibrationTrace trace)
        => string.Join(";", trace.Classification.Select(item => string.Join(",",
            item.RiderId, item.Position, item.Status, F(item.TimeSeconds),
            F(item.DistanceMeters), item.LapsCompleted)));

    private static string TraceFingerprint(CalibrationTrace trace) => Hash(
        CalibrationCsvExporter.ExportSteps(trace) + "\n" + ClassificationFingerprint(trace));

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

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
                foreach (var permutation in Generate(index + 1))
                    yield return permutation;
                (copy[index], copy[swap]) = (copy[swap], copy[index]);
            }
        }
    }

    private static TrackSurfaceState Surface(string id) =>
        CalibrationScenarioCatalog.Surfaces.Single(item => item.Id == id).Surface;

    private static RiderSkills Skills(float speed, float slideControl, float adaptability) =>
        new(50f, speed, slideControl, 50f, 50f, adaptability);

    private static string Id(float value) => value.ToString("000", CultureInfo.InvariantCulture);
    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private sealed record RiderFixture(
        int RiderId,
        float LateralPosition,
        RiderSkills Skills,
        BikeSetup Setup);

    private sealed record CornerSeries(
        float EntrySpeedMetersPerSecond,
        float TrueApexSpeedMetersPerSecond,
        float ExitSpeedMetersPerSecond,
        float MinimumSpeedMetersPerSecond,
        float MinimumSpeedCornerProgress,
        float EntryToApexTimeSeconds,
        float ApexToExitTimeSeconds,
        IReadOnlyList<ActiveCorrectionStepDiagnostic> ControlSteps);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) =>
            new(rider.Lane, 0f);

        public RiderDecision Decide(RiderDecisionContext context) =>
            new(context.Rider.Lane, 0f);
    }
}
