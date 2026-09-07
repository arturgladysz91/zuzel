using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class ContinuousCornerCorrectionSimulationTests
{
    private static readonly TrackSurfaceState PerfectSurface = new(1f, 0f, 0.35f);

    [Fact]
    public void ExistingFirstHalfTurnEntryScrubRemainsNumericallyUnchanged()
    {
        var setup = TurnEntrySetup(MathF.PI / 3f, residualSpeedFactor: 1.05f);
        var diagnostics = Assert.Single(Resolve(setup.Track, setup.Rider).Diagnostics);
        var scrub = diagnostics.TurnEntryScrubProfile!.Value;
        var expected = LongitudinalDynamics.CalculateTurnEntryScrubProfile(
            setup.Rider.Speed,
            SegmentPhysics.MaxSafeTurnSpeed(
                setup.Rider.LateralPosition,
                setup.Track.Geometry,
                PerfectSurface,
                setup.Rider.Profile.Skills,
                setup.Rider.ActiveSetup),
            LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
                setup.Rider.Profile.Skills,
                PerfectSurface),
            diagnostics.TravelledMeters);

        Assert.Equal(expected, scrub);
        Assert.Equal(
            diagnostics.TravelledMeters
            * LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction,
            scrub.DecelerationDistanceMeters + scrub.CarryDistanceMeters,
            5);
    }

    [Fact]
    public void ResidualCorrectionUsesOnlyPostScrubDistance()
    {
        var setup = TurnEntrySetup(MathF.PI / 3f, residualSpeedFactor: 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var diagnostics = Assert.Single(step.Diagnostics);
        var scrub = Assert.IsType<TurnEntryScrubProfile>(diagnostics.TurnEntryScrubProfile);
        var correction = Assert.IsType<CornerSpeedCorrectionProfile>(
            diagnostics.CornerSpeedCorrectionProfile);

        Assert.Equal(
            diagnostics.TravelledMeters - scrub.DecelerationDistanceMeters - scrub.CarryDistanceMeters,
            correction.CorrectionDistanceMeters + correction.RemainingDistanceMeters,
            5);
    }

    [Fact]
    public void TurnEntryNoDistanceIsDoubleCounted()
    {
        var setup = TurnEntrySetup(MathF.PI / 3f, residualSpeedFactor: 1.05f);
        var diagnostics = Assert.Single(Resolve(setup.Track, setup.Rider).Diagnostics);
        var scrub = diagnostics.TurnEntryScrubProfile!.Value;
        var correction = diagnostics.CornerSpeedCorrectionProfile!.Value;

        Assert.Equal(
            diagnostics.TravelledMeters,
            scrub.DecelerationDistanceMeters + scrub.CarryDistanceMeters
            + correction.CorrectionDistanceMeters + correction.RemainingDistanceMeters,
            5);
    }

    [Fact]
    public void TurnEntryResidualInsufficientDistanceLeavesOverspeed()
    {
        var setup = TurnEntrySetup(0.10f, residualSpeedFactor: 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var change = Assert.Single(step.Changes);
        var correction = Assert.Single(step.Diagnostics).CornerSpeedCorrectionProfile!.Value;

        Assert.False(correction.TargetReached);
        Assert.Equal(0f, correction.RemainingDistanceMeters);
        Assert.True(change.Speed > correction.TargetSpeedMetersPerSecond);
        Assert.Equal(correction.ExitSpeedMetersPerSecond, change.Speed);
    }

    [Fact]
    public void TurnEntryResidualTargetReachedGivesCarryOnRemainingDistance()
    {
        var setup = TurnEntrySetup(MathF.PI / 3f, residualSpeedFactor: 1.03f);
        var step = Resolve(setup.Track, setup.Rider);
        var correction = Assert.Single(step.Diagnostics).CornerSpeedCorrectionProfile!.Value;

        Assert.True(correction.TargetReached);
        Assert.True(correction.RemainingDistanceMeters > 0f);
        Assert.Equal(correction.TargetSpeedMetersPerSecond, Assert.Single(step.Changes).Speed);
    }

    [Fact]
    public void FullTurnEntryTotalTimeEqualsPhaseSum()
    {
        var setup = TurnEntrySetup(MathF.PI / 3f, residualSpeedFactor: 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var diagnostics = Assert.Single(step.Diagnostics);
        var scrub = diagnostics.TurnEntryScrubProfile!.Value;
        var correction = diagnostics.CornerSpeedCorrectionProfile!.Value;
        var expected = scrub.TravelTimeSeconds + correction.TravelTimeSeconds
            + correction.RemainingDistanceMeters / correction.ExitSpeedMetersPerSecond;

        Assert.Equal(expected, diagnostics.TravelTimeSeconds, 5);
        Assert.Equal(expected, Assert.Single(step.Changes).ElapsedTimeSeconds, 5);
    }

    [Fact]
    public void PartialTurnEntryUsesOnlyActualRemainingDistance()
    {
        const float entryProgress = 0.5f;
        var setup = TurnEntrySetup(
            MathF.PI / 3f,
            residualSpeedFactor: 1.05f,
            entryProgress);
        var step = Resolve(setup.Track, setup.Rider);
        var diagnostics = Assert.Single(step.Diagnostics);
        var scrub = diagnostics.TurnEntryScrubProfile!.Value;
        var correction = diagnostics.CornerSpeedCorrectionProfile!.Value;
        var nominalDistance = LaneModel.SegmentLengthMeters(
            setup.Track.Segments[0],
            setup.Rider.LateralPosition,
            setup.Track.Geometry);

        Assert.Equal(nominalDistance * (1f - entryProgress), diagnostics.TravelledMeters, 5);
        Assert.Equal(
            diagnostics.TravelledMeters,
            scrub.DecelerationDistanceMeters + scrub.CarryDistanceMeters
            + correction.CorrectionDistanceMeters + correction.RemainingDistanceMeters,
            5);
    }

    [Fact]
    public void BrakeBandTurnMiddleDoesNotInstantlyBecomeMax()
    {
        var setup = OverspeedSetup(SegmentType.TurnMiddle, MathF.PI / 3f, 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var change = Assert.Single(step.Changes);
        var correction = Assert.Single(step.Diagnostics).CornerSpeedCorrectionProfile!.Value;

        Assert.Equal(SegmentOutcome.Brake, change.Outcome);
        Assert.Equal(setup.Rider.Speed, change.PhysicsSpeed);
        Assert.True(change.PhysicsSpeed > correction.TargetSpeedMetersPerSecond);
    }

    [Fact]
    public void TurnMiddleCorrectionConsumesPhysicalDistance()
    {
        var setup = OverspeedSetup(SegmentType.TurnMiddle, MathF.PI / 3f, 1.05f);
        var diagnostics = Assert.Single(Resolve(setup.Track, setup.Rider).Diagnostics);
        var correction = diagnostics.CornerSpeedCorrectionProfile!.Value;

        Assert.Equal(
            diagnostics.TravelledMeters,
            correction.CorrectionDistanceMeters + correction.RemainingDistanceMeters,
            5);
    }

    [Fact]
    public void TurnMiddleEnoughDistanceReachesTargetAndCarries()
    {
        var setup = OverspeedSetup(SegmentType.TurnMiddle, MathF.PI / 3f, 1.03f);
        var step = Resolve(setup.Track, setup.Rider);
        var correction = Assert.Single(step.Diagnostics).CornerSpeedCorrectionProfile!.Value;

        Assert.True(correction.TargetReached);
        Assert.True(correction.RemainingDistanceMeters > 0f);
        Assert.Equal(correction.TargetSpeedMetersPerSecond, Assert.Single(step.Changes).Speed);
    }

    [Fact]
    public void ShortTurnMiddlePreservesResidualOverspeed()
    {
        var setup = OverspeedSetup(SegmentType.TurnMiddle, 0.05f, 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var correction = Assert.Single(step.Diagnostics).CornerSpeedCorrectionProfile!.Value;

        Assert.False(correction.TargetReached);
        Assert.Equal(0f, correction.RemainingDistanceMeters);
        Assert.True(Assert.Single(step.Changes).Speed > correction.TargetSpeedMetersPerSecond);
    }

    [Fact]
    public void TurnMiddleFinalSpeedEqualsPhysicalProfileExit()
    {
        var setup = OverspeedSetup(SegmentType.TurnMiddle, MathF.PI / 3f, 1.05f);
        var step = Resolve(setup.Track, setup.Rider);

        Assert.Equal(
            Assert.Single(step.Diagnostics).CornerSpeedCorrectionProfile!.Value.ExitSpeedMetersPerSecond,
            Assert.Single(step.Changes).Speed);
    }

    [Fact]
    public void TurnExitCorrectionHappensBeforeDrive()
    {
        var setup = OverspeedSetup(SegmentType.TurnExit, MathF.PI / 3f, 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var diagnostics = Assert.Single(step.Diagnostics);
        var correction = diagnostics.CornerSpeedCorrectionProfile!.Value;
        var drive = diagnostics.TurnExitDriveProfile!.Value;
        var expected = LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
            correction.ExitSpeedMetersPerSecond,
            setup.Rider.Profile.Skills,
            setup.Rider.ActiveSetup,
            diagnostics.EntrySurface,
            correction.RemainingDistanceMeters);

        Assert.True(correction.TargetReached);
        Assert.Equal(expected, drive);
    }

    [Fact]
    public void TurnExitDriveUsesOnlyRemainingDistance()
    {
        var setup = OverspeedSetup(SegmentType.TurnExit, MathF.PI / 3f, 1.05f);
        var diagnostics = Assert.Single(Resolve(setup.Track, setup.Rider).Diagnostics);
        var correction = diagnostics.CornerSpeedCorrectionProfile!.Value;
        var drive = diagnostics.TurnExitDriveProfile!.Value;

        Assert.Equal(
            correction.RemainingDistanceMeters,
            drive.AccelerationDistanceMeters + drive.CruiseDistanceMeters
            + drive.DecelerationDistanceMeters,
            5);
        Assert.Equal(
            diagnostics.TravelledMeters,
            correction.CorrectionDistanceMeters + drive.AccelerationDistanceMeters
            + drive.CruiseDistanceMeters + drive.DecelerationDistanceMeters,
            5);
    }

    [Fact]
    public void TurnExitWithoutEnoughCorrectionDistanceDoesNotDrive()
    {
        var setup = OverspeedSetup(SegmentType.TurnExit, 0.05f, 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var diagnostics = Assert.Single(step.Diagnostics);

        Assert.False(diagnostics.CornerSpeedCorrectionProfile!.Value.TargetReached);
        Assert.Null(diagnostics.TurnExitDriveProfile);
        Assert.Equal(
            diagnostics.CornerSpeedCorrectionProfile.Value.ExitSpeedMetersPerSecond,
            Assert.Single(step.Changes).Speed);
    }

    [Fact]
    public void CleanOkTurnExitStillDrivesAcrossFullDistance()
    {
        var setup = OverspeedSetup(SegmentType.TurnExit, MathF.PI / 3f, 0.90f);
        var step = Resolve(setup.Track, setup.Rider);
        var diagnostics = Assert.Single(step.Diagnostics);
        var drive = diagnostics.TurnExitDriveProfile!.Value;

        Assert.Null(diagnostics.CornerSpeedCorrectionProfile);
        Assert.Equal(
            diagnostics.TravelledMeters,
            drive.AccelerationDistanceMeters + drive.CruiseDistanceMeters
            + drive.DecelerationDistanceMeters,
            5);
    }

    [Fact]
    public void RunWideTurnExitUsesContinuousCorrectionAndNeverDrive()
    {
        var setup = OverspeedSetup(SegmentType.TurnExit, MathF.PI / 3f, 1.15f);
        var step = Resolve(setup.Track, setup.Rider);
        var change = Assert.Single(step.Changes);
        var diagnostics = Assert.Single(step.Diagnostics);
        var correction = diagnostics.CornerSpeedCorrectionProfile!.Value;

        Assert.Equal(SegmentOutcome.RunWide, change.Outcome);
        Assert.Equal(setup.Rider.Speed, change.PhysicsSpeed);
        Assert.True(correction.CorrectionDistanceMeters > 0f);
        Assert.Equal(correction.ExitSpeedMetersPerSecond, change.Speed);
        Assert.Null(diagnostics.TurnExitDriveProfile);
        Assert.Equal(
            diagnostics.TravelledMeters,
            correction.CorrectionDistanceMeters + correction.RemainingDistanceMeters,
            5);
    }

    [Fact]
    public void TurnExitTotalTimeEqualsCorrectionPlusDrive()
    {
        var setup = OverspeedSetup(SegmentType.TurnExit, MathF.PI / 3f, 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var diagnostics = Assert.Single(step.Diagnostics);
        var expected = diagnostics.CornerSpeedCorrectionProfile!.Value.TravelTimeSeconds
            + diagnostics.TurnExitDriveProfile!.Value.TravelTimeSeconds;

        Assert.Equal(expected, diagnostics.TravelTimeSeconds, 5);
        Assert.Equal(expected, Assert.Single(step.Changes).ElapsedTimeSeconds, 5);
    }

    [Fact]
    public void TurnExitPeakIncludesPreCorrectionSpeedWhenItExceedsDrivePeak()
    {
        var setup = OverspeedSetup(SegmentType.TurnExit, 0.50f, 1.09f);
        var step = Resolve(setup.Track, setup.Rider);
        var change = Assert.Single(step.Changes);
        var diagnostics = Assert.Single(step.Diagnostics);
        var correction = diagnostics.CornerSpeedCorrectionProfile!.Value;
        var drive = diagnostics.TurnExitDriveProfile!.Value;

        Assert.Equal(SegmentOutcome.Brake, change.Outcome);
        Assert.True(correction.TargetReached);
        Assert.True(correction.RemainingDistanceMeters > 0f);
        Assert.True(correction.EntrySpeedMetersPerSecond > drive.PeakSpeedMetersPerSecond);
        Assert.Equal(
            correction.EntrySpeedMetersPerSecond,
            diagnostics.PeakSpeedMetersPerSecond,
            5);
    }

    [Fact]
    public void CalibrationPeakPropagatesFromProductionDiagnosticsToRiderVmax()
    {
        var setup = OverspeedSetup(SegmentType.TurnExit, 0.50f, 1.09f);
        var resolved = Resolve(setup.Track, setup.Rider);
        var productionPeak = Assert.Single(resolved.Diagnostics).PeakSpeedMetersPerSecond;
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 123,
            Weather = WeatherState.Dry,
            IncidentFrequency = 0f,
            EnableLogging = false,
        };
        var trace = CalibrationRunner.RunHeat(
            setup.Track,
            TrackState.CreateDefault(setup.Track, PerfectSurface),
            new List<RiderState> { Clone(setup.Rider) },
            new FixedDecisionModel(1, 0f),
            options,
            heatId: 34);
        var sample = Assert.Single(trace.StepSamples);
        var summary = Assert.Single(trace.RiderSummaries);

        Assert.Equal(productionPeak, sample.PeakSpeedMetersPerSecond, 5);
        Assert.Equal(sample.PeakSpeedMetersPerSecond, summary.MaxSpeedMetersPerSecond, 5);
    }

    [Fact]
    public void TurnEntryPeakIncludesActualSegmentEntrySpeed()
    {
        var setup = TurnEntrySetup(MathF.PI / 3f, residualSpeedFactor: 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var change = Assert.Single(step.Changes);
        var diagnostics = Assert.Single(step.Diagnostics);

        Assert.NotNull(diagnostics.CornerSpeedCorrectionProfile);
        Assert.True(diagnostics.PeakSpeedMetersPerSecond >= change.EntrySpeed);
        Assert.Equal(change.EntrySpeed, diagnostics.PeakSpeedMetersPerSecond, 5);
    }

    [Fact]
    public void TurnMiddlePeakIncludesCorrectionEntrySpeed()
    {
        var setup = OverspeedSetup(SegmentType.TurnMiddle, MathF.PI / 3f, 1.05f);
        var step = Resolve(setup.Track, setup.Rider);
        var diagnostics = Assert.Single(step.Diagnostics);
        var correction = diagnostics.CornerSpeedCorrectionProfile!.Value;

        Assert.Equal(
            correction.EntrySpeedMetersPerSecond,
            diagnostics.PeakSpeedMetersPerSecond,
            5);
    }

    [Fact]
    public void IncidentRunWideClearsStaleConstraintTargetAndPreservesImmediateLoss()
    {
        var setup = OverspeedSetup(SegmentType.TurnMiddle, MathF.PI / 3f, 1.05f);
        ResolvedSimulationStep? incidentStep = null;
        for (var seed = 0; seed < 10_000; seed++)
        {
            var candidate = Resolve(
                setup.Track,
                Clone(setup.Rider),
                incidentFrequency: 2f,
                decisionRisk: 1f,
                seed: seed);
            if (Assert.Single(candidate.Changes).Outcome == SegmentOutcome.RunWide
                && Assert.Single(candidate.Diagnostics).CornerSpeedCorrectionProfile is null)
            {
                incidentStep = candidate;
                break;
            }
        }

        Assert.NotNull(incidentStep);
        var change = Assert.Single(incidentStep!.Changes);
        Assert.Equal(setup.Rider.Speed * 0.88f, change.PhysicsSpeed, 5);
        Assert.Equal(change.PhysicsSpeed, change.Speed);
        Assert.Null(Assert.Single(incidentStep.Diagnostics).CornerSpeedCorrectionProfile);
    }

    [Fact]
    public void IncidentCrashClearsStaleConstraintTarget()
    {
        var setup = OverspeedSetup(SegmentType.TurnMiddle, MathF.PI / 3f, 1.05f);
        ResolvedSimulationStep? incidentStep = null;
        for (var seed = 0; seed < 10_000; seed++)
        {
            var candidate = Resolve(
                setup.Track,
                Clone(setup.Rider),
                incidentFrequency: 2f,
                decisionRisk: 1f,
                seed: seed);
            if (Assert.Single(candidate.Changes).Outcome == SegmentOutcome.Crash)
            {
                incidentStep = candidate;
                break;
            }
        }

        Assert.NotNull(incidentStep);
        var change = Assert.Single(incidentStep!.Changes);
        Assert.Equal(0f, change.PhysicsSpeed);
        Assert.Equal(0f, change.Speed);
        Assert.Null(Assert.Single(incidentStep.Diagnostics).CornerSpeedCorrectionProfile);
    }

    [Fact]
    public void CalibrationSchemaAppendsNullableCornerCorrectionColumns()
    {
        var setup = OverspeedSetup(SegmentType.TurnMiddle, MathF.PI / 3f, 1.05f);
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 123,
            Weather = WeatherState.Dry,
            IncidentFrequency = 0f,
            EnableLogging = false,
        };
        var trace = CalibrationRunner.RunHeat(
            setup.Track,
            TrackState.CreateDefault(setup.Track, PerfectSurface),
            new List<RiderState> { setup.Rider },
            new FixedDecisionModel(1, 0f),
            options,
            heatId: 34);
        var sample = Assert.Single(trace.StepSamples);
        var header = CalibrationCsvExporter.ExportSteps(trace)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)[0]
            .Split(',');
        var expectedTail = new[]
        {
            "CornerCorrectionEntrySpeedMetersPerSecond",
            "CornerCorrectionTargetSpeedMetersPerSecond",
            "CornerCorrectionExitSpeedMetersPerSecond",
            "CornerCorrectionTravelTimeSeconds",
            "CornerCorrectionRequiredDistanceMeters",
            "CornerCorrectionDistanceMeters",
            "CornerCorrectionRemainingDistanceMeters",
            "CornerCorrectionDecelerationMetersPerSecondSquared",
            "CornerCorrectionTargetReached",
        };

        Assert.Equal(expectedTail, header[^expectedTail.Length..]);
        Assert.NotNull(sample.CornerCorrectionEntrySpeedMetersPerSecond);
        Assert.NotNull(sample.CornerCorrectionTargetSpeedMetersPerSecond);
        Assert.NotNull(sample.CornerCorrectionExitSpeedMetersPerSecond);
        Assert.NotNull(sample.CornerCorrectionTravelTimeSeconds);
        Assert.NotNull(sample.CornerCorrectionDistanceMeters);
        Assert.NotNull(sample.CornerCorrectionRemainingDistanceMeters);
        Assert.NotNull(sample.CornerCorrectionDecelerationMetersPerSecondSquared);
        Assert.True(sample.CornerCorrectionTargetReached);
    }

    private static (Track Track, RiderState Rider) TurnEntrySetup(
        float turnAngleRadians,
        float residualSpeedFactor,
        float entryProgress = 0f)
    {
        var setup = OverspeedSetup(SegmentType.TurnEntry, turnAngleRadians, residualSpeedFactor);
        var distance = LaneModel.SegmentLengthMeters(
            setup.Track.Segments[0],
            setup.Rider.LateralPosition,
            setup.Track.Geometry) * (1f - entryProgress);
        var deceleration = LongitudinalDynamics
            .CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
                setup.Rider.Profile.Skills,
                PerfectSurface);
        var scrubDistance = distance
            * LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction;
        setup.Rider.Speed = MathF.Sqrt(
            setup.Rider.Speed * setup.Rider.Speed
            + 2f * deceleration * scrubDistance);
        if (entryProgress > 0f)
        {
            setup.Rider.RestorePosition(RiderPosition.Create(
                lapNumber: 1,
                segmentIndex: 0,
                segmentProgress: entryProgress,
                segmentCount: setup.Track.Segments.Count));
        }

        return setup;
    }

    private static (Track Track, RiderState Rider) OverspeedSetup(
        SegmentType segmentType,
        float turnAngleRadians,
        float speedFactor)
    {
        var geometry = new TrackGeometry(
            60f,
            24f,
            TrackGeometry.Default.StraightWidthMeters,
            TrackGeometry.Default.TurnWidthMeters,
            turnAngleRadians);
        var track = new Track(new[] { new TrackSegment(0, segmentType) }, geometry);
        var rider = new RiderState(
            new RiderProfile(1, "Correction rider", RiderSkills.Balanced, RiderStyle.Balanced),
            lane: 1)
        {
            LateralPosition = 1f,
            ActiveSetup = BikeSetup.Neutral,
        };
        rider.Speed = SegmentPhysics.MaxSafeTurnSpeed(
            rider.LateralPosition,
            geometry,
            PerfectSurface,
            rider.Profile.Skills,
            rider.ActiveSetup) * speedFactor;
        return (track, rider);
    }

    private static ResolvedSimulationStep Resolve(
        Track track,
        RiderState rider,
        float incidentFrequency = 0f,
        float decisionRisk = 0f,
        int seed = 123)
    {
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = seed,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = incidentFrequency,
        };
        var engine = new SimulationEngine(new FixedDecisionModel(rider.Lane, decisionRisk));
        var snapshot = engine.CaptureSnapshot(
            track,
            new TrackState(track.Segments.Count, LaneModel.LanesCount, (_, _) => PerfectSurface),
            new[] { rider },
            new SimulationStepContext(1, 0, 0, 0, seed, 1));
        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    private static RiderState Clone(RiderState rider)
        => new(rider.Profile, rider.Lane)
        {
            LateralPosition = rider.LateralPosition,
            Speed = rider.Speed,
            Morale = rider.Morale,
            ActiveSetup = rider.ActiveSetup,
        };

    private sealed class FixedDecisionModel(int lane, float risk) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(lane, risk);
    }
}
