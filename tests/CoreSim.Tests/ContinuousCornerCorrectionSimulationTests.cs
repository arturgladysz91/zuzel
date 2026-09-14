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
    public void AdvancedEntryUsesContinuousEnvelopeInsteadOfSeparateScrub()
    {
        var r = CornerTestSupport.Probe();
        Assert.Null(r.Diagnostics.TurnEntryScrubProfile);
        Assert.NotNull(r.Diagnostics.ContinuousCornerProfile);
        Assert.True(r.Diagnostics.ContinuousCornerProfile!.CorrectionDistanceMeters > 0f);
    }

    [Fact]
    public void EntryCorrectionUsesWholeAvailableCornerFragment()
    {
        var r = CornerTestSupport.Probe(factor: 1.05f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.Null(r.Diagnostics.TurnEntryScrubProfile);
        Assert.Equal(r.Diagnostics.TravelledMeters, p.CorrectionDistanceMeters + p.CarryDistanceMeters + p.DriveDistanceMeters, 4);
    }

    [Fact]
    public void TurnEntryNoDistanceIsDoubleCounted()
    {
        var r = CornerTestSupport.Probe();
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.Equal(r.Diagnostics.TravelledMeters, p.CorrectionDistanceMeters + p.CarryDistanceMeters + p.DriveDistanceMeters, 4);
        Assert.Equal(0f, p.DriveDistanceMeters);
    }

    [Fact]
    public void TurnEntryResidualInsufficientDistanceLeavesOverspeed()
    {
        var r = CornerTestSupport.Probe(.333f, 1.05f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.False(p.TargetReached);
        Assert.True(p.ResidualOverspeedMetersPerSecond > 0f);
        Assert.Equal(p.ExitSpeedMetersPerSecond, r.Change.Speed);
    }

    [Fact]
    public void EntryBelowEnvelopeCarriesWithoutResistanceOnlyBraking()
    {
        var r = CornerTestSupport.Probe(factor: .80f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.True(p.TargetReached);
        Assert.True(p.CarryDistanceMeters > 0f);
        Assert.Equal(p.EntrySpeedMetersPerSecond, r.Change.Speed);
    }

    [Fact]
    public void FullTurnEntryTotalTimeEqualsPhaseSum()
    {
        var r = CornerTestSupport.Probe(factor: 1.05f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.Equal(p.CorrectionTimeSeconds + p.CarryTimeSeconds + p.DriveTimeSeconds, r.Diagnostics.TravelTimeSeconds, 5);
        Assert.Equal(p.TravelTimeSeconds, r.Change.ElapsedTimeSeconds, 5);
    }

    [Fact]
    public void PartialTurnEntryUsesOnlyActualRemainingDistance()
    {
        var r = CornerTestSupport.Probe(.125f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.Equal(r.AvailableDistanceMeters, p.CorrectionDistanceMeters + p.CarryDistanceMeters + p.DriveDistanceMeters, 4);
        Assert.Equal(.125f, p.Nodes[0].CornerProgress, 6);
        Assert.Equal(1f / 3f, p.Nodes[^1].CornerProgress, 6);
    }

    [Fact]
    public void BrakeBandTurnMiddleDoesNotInstantlyBecomeMax()
    {
        var r = CornerTestSupport.Probe(.375f, 1.05f);
        Assert.Equal(SegmentOutcome.Brake, r.Change.Outcome);
        Assert.Equal(r.Scenario.EntrySpeedMetersPerSecond, r.Change.PhysicsSpeed);
        Assert.True(r.Change.PhysicsSpeed > r.Diagnostics.ContinuousCornerProfile!.EnvelopeAtEntryMetersPerSecond);
    }

    [Fact]
    public void TurnMiddleCorrectionConsumesPhysicalDistance()
    {
        var r = CornerTestSupport.Probe(.375f, 1.05f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.True(p.CorrectionDistanceMeters > 0f);
        Assert.Equal(r.AvailableDistanceMeters, p.CorrectionDistanceMeters + p.CarryDistanceMeters + p.DriveDistanceMeters, 4);
    }

    [Fact]
    public void TurnMiddleCanCarryCorrectAndDriveAcrossApex()
    {
        var r = CornerTestSupport.Probe(.375f, .95f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.True(p.TargetReached);
        Assert.True(p.CarryDistanceMeters > 0f);
        Assert.True(p.DriveDistanceMeters > 0f);
    }

    [Fact]
    public void ShortTurnMiddlePreservesResidualOverspeed()
    {
        var r = CornerTestSupport.Probe(.6666f, 1.05f);
        Assert.False(r.Diagnostics.ContinuousCornerProfile!.TargetReached);
        Assert.True(r.Diagnostics.ContinuousCornerProfile!.ResidualOverspeedMetersPerSecond > 0f);
    }

    [Fact]
    public void TurnMiddleFinalSpeedEqualsPhysicalProfileExit()
    {
        var r = CornerTestSupport.Probe(.375f, 1.05f);
        Assert.Equal(r.Diagnostics.ContinuousCornerProfile!.ExitSpeedMetersPerSecond, r.Change.Speed);
    }

    [Fact]
    public void TurnExitCorrectionHappensBeforeDrive()
    {
        var r = CornerTestSupport.Probe(.75f, 1.03f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.True(p.CorrectionDistanceMeters > 0f);
        Assert.True(p.DriveDistanceMeters > 0f);
        Assert.Null(r.Diagnostics.TurnExitDriveProfile);
        Assert.Equal(p.ExitSpeedMetersPerSecond, r.Change.Speed);
    }

    [Fact]
    public void TurnExitDriveUsesOnlyRemainingDistance()
    {
        var r = CornerTestSupport.Probe(.75f, 1.03f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.Equal(r.AvailableDistanceMeters, p.CorrectionDistanceMeters + p.CarryDistanceMeters + p.DriveDistanceMeters, 4);
    }

    [Fact]
    public void TurnExitWithoutEnoughCorrectionDistanceDoesNotDrive()
    {
        var r = CornerTestSupport.Probe(.999f, 1.05f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.False(p.TargetReached);
        Assert.Equal(0f, p.DriveDistanceMeters);
        Assert.Equal(p.ExitSpeedMetersPerSecond, r.Change.Speed);
    }

    [Fact]
    public void CleanOkTurnExitStillDrivesAcrossFullDistance()
    {
        var r = CornerTestSupport.Probe(.75f, .90f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.Equal(SegmentOutcome.Ok, r.Change.Outcome);
        Assert.Equal(r.AvailableDistanceMeters, p.DriveDistanceMeters, 5);
    }

    [Fact]
    public void RunWideTurnExitUsesContinuousCorrectionAndNeverDrive()
    {
        var r = CornerTestSupport.Probe(.75f, 1.15f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.Equal(SegmentOutcome.RunWide, r.Change.Outcome);
        Assert.Equal(r.Scenario.EntrySpeedMetersPerSecond, r.Change.PhysicsSpeed);
        Assert.True(p.CorrectionDistanceMeters > 0f);
        Assert.Equal(0f, p.DriveDistanceMeters);
        Assert.Equal(p.ExitSpeedMetersPerSecond, r.Change.Speed);
    }

    [Fact]
    public void TurnExitTotalTimeEqualsCorrectionPlusDrive()
    {
        var r = CornerTestSupport.Probe(.75f, 1.03f);
        var p = r.Diagnostics.ContinuousCornerProfile!;
        Assert.Equal(p.CorrectionTimeSeconds + p.CarryTimeSeconds + p.DriveTimeSeconds, r.Diagnostics.TravelTimeSeconds, 5);
    }

    [Fact]
    public void TurnExitPeakIncludesPreCorrectionSpeedWhenItExceedsDrivePeak()
    {
        var r = CornerTestSupport.Probe(.999f, 1.09f);
        Assert.Equal(SegmentOutcome.Brake, r.Change.Outcome);
        Assert.Equal(r.Scenario.EntrySpeedMetersPerSecond, r.Diagnostics.PeakSpeedMetersPerSecond, 5);
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
        var r = CornerTestSupport.Probe(factor: 1.05f);
        Assert.Equal(r.Change.EntrySpeed, r.Diagnostics.PeakSpeedMetersPerSecond, 5);
        Assert.Equal(0f, r.Diagnostics.ContinuousCornerProfile!.PeakCornerProgress);
    }

    [Fact]
    public void TurnMiddlePeakIncludesCorrectionEntrySpeed()
    {
        var r = CornerTestSupport.Probe(.375f, 1.05f);
        Assert.Equal(r.Change.EntrySpeed, r.Diagnostics.PeakSpeedMetersPerSecond, 5);
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

        Assert.All(expectedTail, name => Assert.Contains(name, header));
        Assert.Contains("ContinuousCornerMinimumProgress", header);
        Assert.NotNull(sample.ContinuousCornerProfile);
        Assert.Null(sample.CornerCorrectionTargetSpeedMetersPerSecond);
        Assert.Null(sample.CornerCorrectionExitSpeedMetersPerSecond);
        Assert.Null(sample.CornerCorrectionTravelTimeSeconds);
        Assert.Null(sample.CornerCorrectionDistanceMeters);
        Assert.Null(sample.CornerCorrectionRemainingDistanceMeters);
        Assert.Null(sample.CornerCorrectionDecelerationMetersPerSecondSquared);
        Assert.True(sample.ContinuousCornerProfile!.CorrectionDistanceMeters > 0f);
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
        rider.Speed = CornerTestSupport.Envelope(track, rider).SpeedMetersPerSecond(0f) * speedFactor;
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
