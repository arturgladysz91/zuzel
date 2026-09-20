using CoreSim.Decisions;
using CoreSim.Race;

namespace CoreSim.Analysis;

/// <summary>Measurement orchestration only. Every traversal belongs to production physics.</summary>
public static class CalibrationScenarioSuite
{
    public static IReadOnlyList<CalibrationScenarioResult> RunRequired()
        => Run(CalibrationScenarioCatalog.RequiredScenarios());

    /// <summary>
    /// Calibration-only A0 equivalence probe. The experiment selector itself remains internal.
    /// </summary>
    public static IReadOnlyList<CalibrationScenarioResult> RunRequiredWithProductionBaselineExperiment()
        => RunCore(
            CalibrationScenarioCatalog.RequiredScenarios(),
            new StraightDriveEnvelopeAdjustment(0f, 0f),
            null);

    /// <summary>Calibration-only #42 R0 equivalence probe.</summary>
    internal static IReadOnlyList<CalibrationScenarioResult> RunRequiredWithZeroCornerResistanceExperiment()
        => RunCore(
            CalibrationScenarioCatalog.RequiredScenarios(),
            null,
            new CornerReducedDriveResistanceAdjustment(0f));

    public static IReadOnlyList<CalibrationScenarioResult> Run(IEnumerable<CalibrationScenario> scenarios)
        => RunCore(scenarios, null, null);

    private static IReadOnlyList<CalibrationScenarioResult> RunCore(
        IEnumerable<CalibrationScenario> scenarios,
        StraightDriveEnvelopeAdjustment? adjustment,
        CornerReducedDriveResistanceAdjustment? cornerResistanceAdjustment)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        var ordered = scenarios.OrderBy(item => item.Metadata.ScenarioId, StringComparer.Ordinal).ToArray();
        if (ordered.Select(item => item.Metadata.ScenarioId).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new ArgumentException("Scenario ids must be unique.", nameof(scenarios));
        return Array.AsReadOnly(ordered.Select(item => RunScenarioCore(
            item, adjustment, cornerResistanceAdjustment)).ToArray());
    }

    public static CalibrationScenarioResult RunScenario(CalibrationScenario scenario)
        => RunScenarioCore(scenario, null, null);

    private static CalibrationScenarioResult RunScenarioCore(
        CalibrationScenario scenario,
        StraightDriveEnvelopeAdjustment? adjustment,
        CornerReducedDriveResistanceAdjustment? cornerResistanceAdjustment)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(scenario.Metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario.Metadata.ScenarioId);
        ValidateFixture(scenario.Fixture);
        var expectedKind = scenario switch
        {
            CalibrationStartScenario => CalibrationScenarioKind.Start,
            CalibrationStraightScenario => CalibrationScenarioKind.Straight,
            CalibrationTurnScenario { SegmentType: SegmentType.TurnEntry } => CalibrationScenarioKind.TurnEntry,
            CalibrationTurnScenario { SegmentType: SegmentType.TurnMiddle } => CalibrationScenarioKind.TurnMiddle,
            CalibrationTurnScenario { SegmentType: SegmentType.TurnExit } => CalibrationScenarioKind.TurnExit,
            CalibrationLineScenario => CalibrationScenarioKind.LineGeometry,
            CalibrationHeatScenario => CalibrationScenarioKind.FullHeat,
            _ => throw new ArgumentException("Unknown scenario type/segment.", nameof(scenario)),
        };
        if (scenario.Metadata.Kind != expectedKind)
            throw new ArgumentException("Scenario kind must match the typed input.", nameof(scenario));
        return scenario switch
        {
            CalibrationStartScenario start => RunStart(start, adjustment, cornerResistanceAdjustment),
            CalibrationStraightScenario straight => RunStraight(straight, adjustment),
            CalibrationTurnScenario turn => RunTurn(turn, adjustment, cornerResistanceAdjustment),
            CalibrationLineScenario line => RunLine(line, adjustment, cornerResistanceAdjustment),
            CalibrationHeatScenario heat => RunHeatCore(
                heat, null, adjustment, cornerResistanceAdjustment),
            _ => throw new ArgumentException("Unknown scenario type.", nameof(scenario)),
        };
    }

    private static CalibrationStartResult RunStart(
        CalibrationStartScenario scenario,
        StraightDriveEnvelopeAdjustment? adjustment,
        CornerReducedDriveResistanceAdjustment? cornerResistanceAdjustment)
    {
        var fixture = scenario.Fixture;
        var track = Track.CreateStandingStartExample();
        var distance = LaneModel.SegmentLengthMeters(track.Segments[0], 1, track.Geometry);
        var profile = scenario.Mode switch
        {
            CalibrationStartMode.PureLaunch => LongitudinalDynamics.CalculateStandingStartLaunchProfile(
                fixture.Skills, fixture.Setup, fixture.Surface, distance),
            CalibrationStartMode.FirstCornerPreparation => Resolve(
                    track, CreateRider(1, 1, fixture), fixture.Surface,
                    adjustment, cornerResistanceAdjustment)
                .Diagnostics.Single().StandingStartLaunchProfile
                ?? throw new InvalidOperationException("Production standing start did not produce a launch profile."),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        return new(scenario, distance, profile,
            LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(fixture.Skills, fixture.Setup, fixture.Surface),
            LongitudinalDynamics.CalculateStandingStartReferenceDriveAccelerationMetersPerSecondSquared(
                fixture.Skills, fixture.Setup, fixture.Surface));
    }

    private static CalibrationStraightResult RunStraight(
        CalibrationStraightScenario scenario,
        StraightDriveEnvelopeAdjustment? adjustment)
    {
        var f = scenario.Fixture;
        var cornerEntryDeceleration =
            LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(f.Skills, f.Surface);
        var profile = adjustment is { } activeAdjustment
            ? LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
                scenario.EntrySpeedMetersPerSecond, f.Skills, f.Setup, f.Surface,
                cornerEntryDeceleration, scenario.DistanceMeters, null, activeAdjustment)
            : LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
                scenario.EntrySpeedMetersPerSecond, f.Skills, f.Setup, f.Surface,
                cornerEntryDeceleration, scenario.DistanceMeters);
        return new(scenario, profile,
            LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(f.Skills, f.Setup, f.Surface),
            LongitudinalDynamics.CalculateStraightNetAccelerationMetersPerSecondSquared(
                scenario.EntrySpeedMetersPerSecond, f.Skills, f.Setup, f.Surface));
    }

    private static CalibrationTurnResult RunTurn(
        CalibrationTurnScenario scenario,
        StraightDriveEnvelopeAdjustment? adjustment,
        CornerReducedDriveResistanceAdjustment? cornerResistanceAdjustment)
    {
        if (scenario.SegmentType is not (SegmentType.TurnEntry or SegmentType.TurnMiddle or SegmentType.TurnExit))
            throw new ArgumentException("A turn scenario requires a corner segment.", nameof(scenario));
        if (!float.IsFinite(scenario.EntrySpeedMetersPerSecond) || scenario.EntrySpeedMetersPerSecond <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scenario), "Corner entry speed must be positive and finite.");
        if (!float.IsFinite(scenario.SegmentProgress) || scenario.SegmentProgress < 0f || scenario.SegmentProgress >= 1f)
            throw new ArgumentOutOfRangeException(nameof(scenario), "Progress must be in [0, 1).");
        var geometry = Track.CreateStandingStartExample().Geometry;
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnEntry),
            new TrackSegment(1, SegmentType.TurnMiddle), new TrackSegment(2, SegmentType.TurnExit) }, geometry);
        var segmentIndex = Array.FindIndex(track.Segments.ToArray(), segment => segment.Type == scenario.SegmentType);
        var rider = CreateRider(1, (int)scenario.LateralPosition, scenario.Fixture);
        rider.LateralPosition = scenario.LateralPosition;
        rider.Speed = scenario.EntrySpeedMetersPerSecond;
        rider.RestorePosition(RiderPosition.Create(1, segmentIndex, scenario.SegmentProgress, track.Segments.Count));
        var step = Resolve(track, rider, scenario.Fixture.Surface,
            adjustment, cornerResistanceAdjustment);
        return new(scenario, LaneModel.TurnArcRadiusMeters(scenario.LateralPosition, geometry),
            LaneModel.SegmentLengthMeters(track.Segments[segmentIndex], scenario.LateralPosition, geometry) * (1f - scenario.SegmentProgress),
            ObserveCornerCapability(scenario.Fixture, scenario.LateralPosition), step.Diagnostics.Single(), step.Changes.Single());
    }

    /// <summary>Black-box observation of current production transition speeds; no threshold formula is copied.</summary>
    public static CalibrationCornerCapability ObserveCornerCapability(CalibrationScenarioFixture fixture, float lateralPosition)
    {
        ValidateFixture(fixture);
        var geometry = Track.CreateStandingStartExample().Geometry;
        var segment = new TrackSegment(0, SegmentType.TurnMiddle);
        var max = SegmentPhysics.MaxSafeTurnSpeed(lateralPosition, geometry, fixture.Surface, fixture.Skills, fixture.Setup);
        SegmentResolution Apply(float speed) => SegmentPhysics.Apply(new SegmentPhysicsContext(
            segment, (int)lateralPosition, speed, geometry, fixture.Surface, fixture.Skills, 0.5f, fixture.Setup,
            LateralPosition: lateralPosition));
        float Transition(Func<SegmentOutcome, bool> predicate)
        {
            var lower = max;
            var upper = MathF.BitIncrement(max * SegmentPhysics.MaxAdvancedRunWideSpeedFactor);
            if (!predicate(Apply(upper).Outcome))
                throw new InvalidOperationException("Production transition is outside its published upper bracket.");
            // Diagnostic search to adjacent representable floats, not a traversal or calibration optimizer.
            for (var iteration = 0; iteration < 64 && MathF.BitIncrement(lower) < upper; iteration++)
            {
                var middle = lower + (upper - lower) * 0.5f;
                if (predicate(Apply(middle).Outcome)) upper = middle;
                else lower = middle;
            }
            return upper;
        }
        var brake = Transition(outcome => outcome != SegmentOutcome.Ok);
        var runWide = Transition(outcome => outcome is SegmentOutcome.RunWide or SegmentOutcome.Crash);
        var crash = Transition(outcome => outcome == SegmentOutcome.Crash);
        var sampleSpeed = (runWide + crash) * 0.5f;
        var resolution = Apply(sampleSpeed);
        // At the physical outer lane the production model crashes instead of running wider.
        // There is no recoverable band or observed retention to invent at that position.
        var hasRunWide = resolution.Outcome == SegmentOutcome.RunWide;
        var retention = hasRunWide && resolution.ContinuousCorrectionTargetSpeedMetersPerSecond is { } target
            ? (target - max) / (sampleSpeed - max) : (float?)null;
        return new(max, LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(fixture.Skills, fixture.Surface),
            brake, hasRunWide ? runWide : null, crash, retention);
    }

    public static CalibrationHeatResult RunHeat(CalibrationHeatScenario scenario, IEnumerable<int>? riderOrder = null)
        => RunHeatCore(scenario, riderOrder, null, null);

    private static CalibrationHeatResult RunHeatCore(
        CalibrationHeatScenario scenario,
        IEnumerable<int>? riderOrder,
        StraightDriveEnvelopeAdjustment? adjustment,
        CornerReducedDriveResistanceAdjustment? cornerResistanceAdjustment)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ValidateFixture(scenario.Fixture);
        foreach (var rider in scenario.Riders) ValidateFixture(scenario.Fixture with { Skills = rider.Skills });
        var order = (riderOrder ?? scenario.Riders.Select(rider => rider.RiderId)).ToArray();
        if (!order.Order().SequenceEqual(scenario.Riders.Select(rider => rider.RiderId).Order()))
            throw new ArgumentException("Input order must contain each scenario rider id exactly once.", nameof(riderOrder));
        var riders = order.Select(id => scenario.Riders.Single(rider => rider.RiderId == id))
            .Select(rider => CreateRider(rider.RiderId, rider.Lane, scenario.Fixture with { Skills = rider.Skills })).ToList();
        var trace = Trace(scenario.Fixture, riders, adjustment, cornerResistanceAdjustment);
        var observations = ObserveHeat(trace);
        var performance = observations.Select(rider => rider.Performance).ToArray();
        double Spread(Func<CalibrationSkillRiderObservation, double> select)
            => performance.Max(select) - performance.Min(select);
        return new(scenario, observations, new(
            Spread(rider => rider.TotalTimeSeconds),
            Spread(rider => CalibrationUnits.MetersPerSecondToKph(rider.MaximumSpeedMetersPerSecond)),
            Spread(rider => rider.L1Seconds ?? throw new InvalidOperationException("Fixture did not finish L1.")),
            Spread(rider => rider.AverageSpeedMetersPerSecond ?? throw new InvalidOperationException("Fixture has no average speed."))), trace);
    }

    private static CalibrationLineResult RunLine(
        CalibrationLineScenario scenario,
        StraightDriveEnvelopeAdjustment? adjustment,
        CornerReducedDriveResistanceAdjustment? cornerResistanceAdjustment)
    {
        LaneModel.ValidateLane(scenario.LateralPosition);
        var track = Track.CreateStandingStartExample();
        var geometry = track.Geometry;
        var lateral = scenario.LateralPosition;
        var trace = Trace(
            scenario.Fixture,
            new List<RiderState> { CreateRider(1, lateral, scenario.Fixture) },
            adjustment,
            cornerResistanceAdjustment);
        return new(scenario, LaneModel.NormalizedLateralFraction(lateral),
            LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(lateral, SegmentType.Straight, geometry),
            LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(lateral, SegmentType.TurnMiddle, geometry),
            LaneModel.TurnArcRadiusMeters(lateral, geometry), LaneModel.TurnArcLengthMeters(lateral, geometry),
            track.Segments.Sum(segment => LaneModel.SegmentLengthMeters(segment, lateral, geometry)),
            SegmentPhysics.MaxSafeTurnSpeed(lateral, geometry, scenario.Fixture.Surface, scenario.Fixture.Skills, scenario.Fixture.Setup),
            ObserveHeat(trace).Single(), trace);
    }

    private static IReadOnlyList<CalibrationHeatRiderObservation> ObserveHeat(CalibrationTrace trace)
        => Array.AsReadOnly(CalibrationSkillSweep.ObserveRiders(trace).Select(performance =>
        {
            var samples = trace.StepSamples.Where(sample => sample.RiderId == performance.RiderId).ToArray();
            var residuals = samples.Where(sample => sample.CornerCorrectionTargetReached == false).ToArray();
            return new CalibrationHeatRiderObservation(performance,
                samples.Count(sample => sample.CornerCorrectionTargetReached.HasValue), residuals.Length,
                residuals.Select(sample => sample.CornerCorrectionExitSpeedMetersPerSecond!.Value
                    - sample.CornerCorrectionTargetSpeedMetersPerSecond!.Value).DefaultIfEmpty(0f).Max(),
                samples.Min(sample => MathF.Min(sample.EntryLateralPosition, sample.ExitLateralPosition)),
                samples.Max(sample => MathF.Max(sample.EntryLateralPosition, sample.ExitLateralPosition)));
        }).ToArray());

    private static CalibrationTrace Trace(
        CalibrationScenarioFixture fixture,
        List<RiderState> riders,
        StraightDriveEnvelopeAdjustment? adjustment,
        CornerReducedDriveResistanceAdjustment? cornerResistanceAdjustment)
    {
        var track = Track.CreateStandingStartExample();
        return CalibrationRunner.RunHeat(track, TrackState.CreateDefault(track, fixture.Surface), riders,
            new HoldLane(), Options(adjustment, cornerResistanceAdjustment), heatId: 35);
    }

    private static ResolvedSimulationStep Resolve(
        Track track,
        RiderState rider,
        TrackSurfaceState surface,
        StraightDriveEnvelopeAdjustment? adjustment,
        CornerReducedDriveResistanceAdjustment? cornerResistanceAdjustment)
    {
        var engine = new SimulationEngine(new HoldLane());
        var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track, surface), new[] { rider },
            new SimulationStepContext(35, 0, rider.LapsCompleted, rider.SegmentIndex, CalibrationSkillSweep.FixedSeed, 4));
        return engine.Resolve(snapshot, engine.Decide(snapshot),
            Options(adjustment, cornerResistanceAdjustment));
    }

    private static RiderState CreateRider(int id, int lane, CalibrationScenarioFixture fixture)
        => new(new RiderProfile(id, $"Calibration rider {id}", fixture.Skills, RiderStyle.Balanced), lane)
        { ActiveSetup = fixture.Setup };

    private static HeatSimulationOptions Options(
        StraightDriveEnvelopeAdjustment? adjustment = null,
        CornerReducedDriveResistanceAdjustment? cornerResistanceAdjustment = null) => new()
    {
        Laps = 4, Seed = CalibrationSkillSweep.FixedSeed, Weather = WeatherState.Dry,
        IncidentFrequency = 0f, EnableLogging = false,
        StraightDriveEnvelopeAdjustment = adjustment,
        CornerReducedDriveResistanceAdjustment = cornerResistanceAdjustment,
    };

    private static void ValidateFixture(CalibrationScenarioFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(fixture.Skills);
        ArgumentNullException.ThrowIfNull(fixture.Setup);
        var s = fixture.Skills;
        if (new[] { s.Start, s.Speed, s.SlideControl, s.TrackReading, s.PairRiding, s.Adaptability,
            fixture.Setup.Gearing, fixture.Setup.TractionBias, fixture.Surface.Grip, fixture.Surface.Ruts,
            fixture.Surface.Moisture }.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Measurement fixture values must be finite.", nameof(fixture));
    }

    private sealed class HoldLane : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane, 0f);
        public RiderDecision Decide(RiderDecisionContext context) => new(context.Rider.Lane, 0f);
    }
}
