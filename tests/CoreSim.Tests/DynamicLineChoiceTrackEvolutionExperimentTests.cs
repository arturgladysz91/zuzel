using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Analysis;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class DynamicLineChoiceTrackEvolutionExperimentTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly Lazy<DynamicLineChoiceTrackEvolutionExperimentResult> Result =
        new(DynamicLineChoiceTrackEvolutionExperiment.Run);

    [Fact]
    public void UniformLineDistancesAreStrictlyIncreasing()
    {
        var lines = Sweep(DynamicLineChoiceTrackEvolutionExperiment.UniformProfile, 0f).Lines;
        Assert.True(lines.Zip(lines.Skip(1)).All(pair =>
            pair.Second.FourLapDistanceMeters > pair.First.FourLapDistanceMeters));
    }

    [Fact]
    public void FixedLineRunsUseProductionCalibrationRunnerAndHeatSimulator()
    {
        var source = CanonicalText("src/CoreSim/Analysis/DynamicLineChoiceTrackEvolutionExperiment.cs");
        Assert.Contains("CalibrationRunner.RunHeat", source, StringComparison.Ordinal);
        Assert.Contains("HeatSimulator", CanonicalText("src/CoreSim/Analysis/CalibrationRunner.cs"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("surrogate", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoExperimentPhysicsAdjustmentIsEnabled()
    {
        var options = new HeatSimulationOptions();
        Assert.Null(options.StraightDriveEnvelopeAdjustment);
        Assert.Null(options.CornerReducedDriveResistanceAdjustment);
        Assert.Null(options.PreApexScrubLossAdjustment);
        Assert.Null(options.ActiveCorrectionControlLossAdjustment);
        Assert.Contains("every calibration-only physics adjustment null",
            DynamicLineChoiceTrackEvolutionReport.Render(Result.Value), StringComparison.Ordinal);
    }

    [Fact]
    public void SurfaceSeverityInterpolationIsExact()
    {
        var track = TrackFixture();
        var half = DynamicLineChoiceTrackEvolutionExperiment.CreateProfileState(
            track, DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile, .5f);
        var segment = track.CornerTopology.Corners[0].StartSegmentIndex;
        var lane0 = half.GetSurface(segment, 0);
        var lane3 = half.GetSurface(segment, 3);
        Assert.Equal(.85f, lane0.Grip);
        Assert.Equal(.225f, lane0.Ruts);
        Assert.Equal(.985f, lane3.Grip);
        Assert.Equal(.025f, lane3.Ruts);
        Assert.Equal(.35f, lane0.Moisture);
    }

    [Fact]
    public void SeverityZeroIsBitExactUniform()
    {
        var track = TrackFixture();
        var uniform = DynamicLineChoiceTrackEvolutionExperiment.CreateProfileState(
            track, DynamicLineChoiceTrackEvolutionExperiment.UniformProfile, 0f);
        foreach (var profile in new[]
                 {
                     DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile,
                     DynamicLineChoiceTrackEvolutionExperiment.MiddleProfile,
                 })
        {
            var zero = DynamicLineChoiceTrackEvolutionExperiment.CreateProfileState(track, profile, 0f);
            for (var segment = 0; segment < track.Segments.Count; segment++)
            for (var lane = 0; lane < LaneModel.LanesCount; lane++)
                Assert.Equal(uniform.GetSurface(segment, lane), zero.GetSurface(segment, lane));
        }
    }

    [Fact]
    public void SurfaceRawValuesStayBounded()
    {
        Assert.All(Result.Value.StaticSurfaceMatrix, item =>
        {
            Assert.InRange(item.Grip, 0f, 1f);
            Assert.InRange(item.Ruts, 0f, 1f);
            Assert.InRange(item.Moisture, 0f, 1f);
            Assert.InRange(item.EffectiveGrip, 0f, 1f);
        });
    }

    [Fact]
    public void IntegerContinuousSamplesEqualStoredCells()
    {
        var track = TrackFixture();
        var state = DynamicLineChoiceTrackEvolutionExperiment.CreateProfileState(
            track, DynamicLineChoiceTrackEvolutionExperiment.MiddleProfile, .75f);
        var snapshot = state.Snapshot();
        for (var segment = 0; segment < track.Segments.Count; segment++)
        for (var lane = 0; lane < LaneModel.LanesCount; lane++)
            Assert.Equal(state.GetSurface(segment, lane), snapshot.SampleSurface(segment, lane));
    }

    [Fact]
    public void FixedLineResultIsDeterministic()
    {
        Assert.True(Result.Value.Deterministic);
    }

    [Fact]
    public void LineRankingIsDeterministicAndComplete()
    {
        Assert.All(Result.Value.StaticSweeps, sweep =>
        {
            Assert.Equal(5, sweep.Lines.Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 },
                sweep.Lines.Select(item => item.Rank).Order());
            Assert.Equal(sweep.BestLane,
                sweep.Lines.OrderBy(item => item.FlyingLapMedianSeconds)
                    .ThenBy(item => item.Lane).First().Lane);
        });
    }

    [Fact]
    public void EveryNumericObservationIsFinite()
    {
        Assert.All(Result.Value.StaticSweeps.SelectMany(item => item.Lines), item =>
        {
            AssertFinite(item.FourLapDistanceMeters, item.FlyingLapMedianSeconds,
                item.HeatTimeSeconds, item.MaximumSpeedMetersPerSecond,
                item.CornerEntrySpeedMetersPerSecond, item.TrueApexSpeedMetersPerSecond,
                item.CornerExitSpeedMetersPerSecond, item.DeltaToBestSeconds);
        });
        Assert.All(Result.Value.DecisionOracles, item =>
            AssertFinite(item.OraclePhysicalRegretSeconds));
        Assert.All(Result.Value.TrackReadingMetrics, item => AssertFinite(
            item.PerfectChoicePercent, item.WithinPointZeroFiveSecondsPercent,
            item.MeanRegretSeconds, item.MedianRegretSeconds, item.P90RegretSeconds,
            item.OracleAgreementPercent));
        Assert.All(Result.Value.ExecutionMatrix.Concat(Result.Value.AdjacentExecutionControl),
            item => AssertFinite(item.CornerEntryLateralPosition,
                item.DistanceToPlannedLaneMeters, item.DistanceToTargetLaneMeters,
                item.PhysicalLateralMetersMoved, item.ArrivalErrorMeters,
                item.SegmentTravelTimeSeconds, item.FlyingLapMedianSeconds,
                item.LapTimeConsequenceSeconds, item.MaximumPossibleLateralMeters));
    }

    [Fact]
    public void SameSeedProducesSameDecision()
    {
        var track = TrackFixture();
        var first = DynamicLineChoiceTrackEvolutionExperiment.Decide(track,
            DynamicLineChoiceTrackEvolutionExperiment.CreateProfileState(track,
                DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile, 1f), 20, 45117);
        var second = DynamicLineChoiceTrackEvolutionExperiment.Decide(track,
            DynamicLineChoiceTrackEvolutionExperiment.CreateProfileState(track,
                DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile, 1f), 20, 45117);
        Assert.Equal(first, second);
    }

    [Fact]
    public void DecisionSeedSetIsExactlySixtyFourStableValues()
    {
        Assert.Equal(64, DynamicLineChoiceTrackEvolutionExperiment.DecisionSeeds.Count);
        Assert.Equal(Enumerable.Range(45101, 64),
            DynamicLineChoiceTrackEvolutionExperiment.DecisionSeeds);
        Assert.All(Result.Value.TrackReadingMetrics, item => Assert.Equal(64, item.SeedCount));
    }

    [Fact]
    public void TrackReadingOneHundredOracleIsSeedInvariant()
    {
        Assert.Equal(5, Result.Value.DecisionOracles.Count);
        Assert.All(Result.Value.DecisionOracles, oracle =>
        {
            Assert.True(oracle.SeedInvariant);
            Assert.Equal(64, oracle.ChosenLaneHistogram.Sum());
            Assert.Equal(64, oracle.ChosenLaneHistogram[oracle.OracleLane]);
            Assert.Single(oracle.ChosenLaneHistogram.Where(count => count != 0));
        });
    }

    [Fact]
    public void OracleLaneIsReportedForEveryStaticDecisionProfile()
    {
        var profiles = Result.Value.TrackReadingMetrics
            .Select(item => (item.ProfileId, item.Severity)).Distinct().ToArray();
        Assert.Equal(profiles.Length, Result.Value.DecisionOracles.Count);
        Assert.All(profiles, profile => Assert.Single(Result.Value.DecisionOracles.Where(item =>
            item.ProfileId == profile.ProfileId
            && BitConverter.SingleToInt32Bits(item.Severity)
            == BitConverter.SingleToInt32Bits(profile.Severity))));
    }

    [Fact]
    public void OraclePhysicalRegretIsFiniteAndNonNegative()
    {
        Assert.All(Result.Value.DecisionOracles, item =>
        {
            Assert.True(double.IsFinite(item.OraclePhysicalRegretSeconds));
            Assert.True(item.OraclePhysicalRegretSeconds >= 0d);
        });
    }

    [Fact]
    public void OracleAgreementHistogramAndAccountingAreExact()
    {
        Assert.All(Result.Value.TrackReadingMetrics, item =>
        {
            Assert.Equal(9, item.OracleDifferenceHistogram.Count);
            Assert.Equal(64, item.OracleDifferenceHistogram.Sum());
            Assert.Equal(item.OracleAgreementPercent,
                100d * item.OracleDifferenceHistogram[LaneModel.MaxLane] / item.SeedCount,
                precision: 10);
            Assert.InRange(item.OracleAgreementPercent, 0d, 100d);
        });
    }

    [Fact]
    public void TrackReadingClassificationUsesOracleAgreement()
    {
        var agreement = new[] { 20f, 50f, 80f }.Select(trackReading =>
            Result.Value.TrackReadingMetrics.Where(item => item.TrackReading == trackReading)
                .Average(item => item.OracleAgreementPercent)).ToArray();
        Assert.True(agreement[0] < agreement[1]);
        Assert.True(agreement[1] < agreement[2]);
        Assert.True(Result.Value.TrackReadingPerceptionSignalHealthy);
        Assert.False(Result.Value.TrackReadingPerceptionSignalWeak);
        Assert.False(Result.Value.TrackReadingPerceptionReversal);
    }

    [Fact]
    public void WorseningPhysicalRegretIsNotMisclassifiedAsWeakPerception()
    {
        var regret20 = Result.Value.TrackReadingMetrics.Where(item => item.TrackReading == 20f)
            .Average(item => item.MeanRegretSeconds);
        var regret80 = Result.Value.TrackReadingMetrics.Where(item => item.TrackReading == 80f)
            .Average(item => item.MeanRegretSeconds);
        Assert.True(regret20 - regret80 < 0d);
        Assert.False(Result.Value.TrackReadingPerceptionSignalWeak);
        Assert.True(Result.Value.TrackReadingPerceptionSignalHealthy);
    }

    [Fact]
    public void PhysicalRegretRemainsSeparateFromOracleAgreement()
    {
        var metric = Result.Value.TrackReadingMetrics.Single(item =>
            item.ProfileId == DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile
            && item.Severity == .5f && item.TrackReading == 80f);
        Assert.Equal(90.625d, metric.OracleAgreementPercent);
        Assert.Equal(.321890d, metric.MeanRegretSeconds, precision: 6);
        Assert.NotEqual(metric.OracleAgreementPercent, metric.MeanRegretSeconds);
    }

    [Fact]
    public void BalancedStyleDecisionObjectiveConfoundIsDocumented()
    {
        var report = DynamicLineChoiceTrackEvolutionReport.Render(Result.Value);
        Assert.Contains("OutsidePreference = 0.5", report, StringComparison.Ordinal);
        Assert.Contains("movement cost, occupancy, surface risk, and projected route time",
            report, StringComparison.Ordinal);
    }

    [Fact]
    public void TrackReadingChangesPerceptionOnlyNotPhysicalCapability()
    {
        var geometry = TrackFixture().Geometry;
        var surface = CalibrationScenarioCatalog.Baseline.Surface;
        var low = new RiderSkills(50, 50, 50, 20, 50, 50);
        var high = new RiderSkills(50, 50, 50, 80, 50, 50);
        Assert.Equal(
            SegmentPhysics.MaxSafeTurnSpeed(2f, geometry, surface, low, BikeSetup.Neutral),
            SegmentPhysics.MaxSafeTurnSpeed(2f, geometry, surface, high, BikeSetup.Neutral));
    }

    [Fact]
    public void TrackReadingIsAbsentFromPhysicalFormulaSignatures()
    {
        var types = new[]
        {
            typeof(SegmentPhysics), typeof(ContinuousCornerEnvelope),
            typeof(LongitudinalDynamics), typeof(LateralMovementModel),
        };
        Assert.DoesNotContain(types.SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SelectMany(method => method.GetParameters()), parameter =>
            parameter.Name?.Contains("trackReading", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void DecisionRegretIsNeverNegative()
    {
        Assert.All(Result.Value.TrackReadingMetrics, item =>
        {
            Assert.True(item.MeanRegretSeconds >= 0d);
            Assert.True(item.MedianRegretSeconds >= 0d);
            Assert.True(item.P90RegretSeconds >= 0d);
        });
        Assert.All(Result.Value.StaticDecisions, item => Assert.True(item.RegretSeconds >= 0d));
    }

    [Fact]
    public void EveryChosenLaneHistogramSumsToSixtyFour()
    {
        Assert.All(Result.Value.TrackReadingMetrics, item =>
            Assert.Equal(64, item.ChosenLaneHistogram.Sum()));
    }

    [Fact]
    public void PerfectAndNonPerfectDecisionAccountingIsValid()
    {
        Assert.All(Result.Value.TrackReadingMetrics, item =>
        {
            Assert.InRange(item.PerfectChoicePercent, 0d, 100d);
            Assert.InRange(item.WithinPointZeroFiveSecondsPercent, 0d, 100d);
            Assert.True(item.WithinPointZeroFiveSecondsPercent + 1e-9d
                >= item.PerfectChoicePercent);
        });
    }

    [Fact]
    public void LateralMovementModelSourceIsUnchanged()
    {
        AssertHashes(new Dictionary<string, string>
        {
            ["src/CoreSim/Track/LateralMovementModel.cs"] =
                "4A5A71D726B10D1D52C84BCFC053E63E11A4F4D21A381225DC23821D3BA9AC44",
        });
    }

    [Fact]
    public void HigherExecutionSkillDoesNotReduceMaximumLateralMovement()
    {
        var ordered = Result.Value.ExecutionMatrix
            .OrderBy(item => item.SlideControl + item.Adaptability).ToArray();
        Assert.True(ordered[^1].MaximumPossibleLateralMeters
            >= ordered[0].MaximumPossibleLateralMeters);
        Assert.True(ordered[^1].MaximumPossibleLateralMeters
            > ordered[0].MaximumPossibleLateralMeters);
    }

    [Fact]
    public void ExecutionReportsTargetAndProductionPlannedLane()
    {
        Assert.All(Result.Value.ExecutionMatrix, item =>
        {
            Assert.Equal(2, item.TargetLane);
            Assert.Equal(1, item.PlannedLaneOnPreparationSegment);
            Assert.True(item.PlannedLaneOnPreparationSegment < item.TargetLane);
        });
    }

    [Fact]
    public void PlannerCapDetectionMatchesProductionTraceGeometry()
    {
        var geometry = TrackFixture().Geometry;
        foreach (var item in Result.Value.ExecutionMatrix)
        {
            var planned = LateralSpaceModel.LateralDistanceMeters(
                item.StartingLateralPosition, item.PlannedLaneOnPreparationSegment,
                SegmentType.Straight, geometry);
            var target = LateralSpaceModel.LateralDistanceMeters(
                item.StartingLateralPosition, item.TargetLane,
                SegmentType.Straight, geometry);
            Assert.Equal(planned, item.DistanceToPlannedLaneMeters);
            Assert.Equal(target, item.DistanceToTargetLaneMeters);
            Assert.Equal(planned, item.PhysicalLateralMetersMoved);
            Assert.True(item.PlannerCapBinding);
        }
    }

    [Fact]
    public void ExecutionCapacitySpreadAndRatioAreReportedExactly()
    {
        var minimum = Result.Value.ExecutionMatrix.Min(item => item.MaximumPossibleLateralMeters);
        var maximum = Result.Value.ExecutionMatrix.Max(item => item.MaximumPossibleLateralMeters);
        Assert.Equal(maximum - minimum, Result.Value.ExecutionCapacitySpreadMeters);
        Assert.Equal(maximum / minimum, Result.Value.ExecutionCapacityRatio);
        Assert.True(Result.Value.ExecutionSkillCapacityExists);
        Assert.False(Result.Value.ExecutionSkillCapacityWeak);
    }

    [Fact]
    public void PlannerBoundMovementIsNotClassifiedAsWeakExecutionSkill()
    {
        Assert.True(Result.Value.ExecutionPlannerBound);
        Assert.True(Result.Value.PlanningHorizonLimitsExecutionExpression);
        Assert.True(Result.Value.ExecutionSkillCapacityExists);
        Assert.DoesNotContain("ExecutionSkillSignalTooWeak", Result.Value.Classification,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AdjacentLaneControlIsReportedWithoutManufacturedSeparation()
    {
        Assert.Equal(5, Result.Value.AdjacentExecutionControl.Count);
        Assert.All(Result.Value.AdjacentExecutionControl, item =>
        {
            Assert.Equal(1, item.TargetLane);
            Assert.Equal(1, item.PlannedLaneOnPreparationSegment);
            Assert.False(item.PlannerCapBinding);
            Assert.True(item.Arrived);
        });
        Assert.True(Result.Value.AdjacentExecutionFixtureNonDiscriminating);
    }

    [Fact]
    public void LineSwitchStrongContrastFlagUsesObservedThresholds()
    {
        Assert.Equal(1f, Result.Value.OutsideFirstNonInnerBestSeverity);
        Assert.Equal(1f, Result.Value.MiddleFirstNonInnerBestSeverity);
        Assert.True(Result.Value.LineSwitchRequiresStrongSurfaceContrast);
    }

    [Fact]
    public void ArrivalErrorUsesPhysicalTurnMeters()
    {
        var geometry = TrackFixture().Geometry;
        foreach (var item in Result.Value.ExecutionMatrix)
        {
            var target = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
                item.TargetLane, SegmentType.TurnEntry, geometry);
            var actual = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
                item.CornerEntryLateralPosition, SegmentType.TurnEntry, geometry);
            Assert.Equal(MathF.Abs(target - actual), item.ArrivalErrorMeters);
        }
    }

    [Fact]
    public void ArrivalToleranceIsExistingExactPointZeroFiveMeters()
    {
        Assert.Equal(.05f, LateralMovementModel.LaneArrivalToleranceMeters);
        Assert.All(Result.Value.ExecutionMatrix.Concat(Result.Value.AdjacentExecutionControl),
            item => Assert.Equal(item.ArrivalErrorMeters <= .05f, item.Arrived));
    }

    [Fact]
    public void LateralExecutionNeverTeleports()
    {
        Assert.All(Result.Value.ExecutionMatrix.Concat(Result.Value.AdjacentExecutionControl), item =>
        {
            Assert.True(item.PhysicalLateralMetersMoved
                <= item.MaximumPossibleLateralMeters + 1e-5f);
            Assert.True(item.PhysicalLateralMetersMoved >= 0f);
        });
    }

    [Fact]
    public void LateralPositionAlwaysRemainsInsideNormalizedDomain()
    {
        Assert.All(Result.Value.ExecutionMatrix, item =>
            Assert.InRange(item.CornerEntryLateralPosition, 0f, 4f));
        Assert.All(Result.Value.Evolution.SelectMany(item => item.CornerSurfaces), item =>
            Assert.InRange(item.Lane, 0, 4));
    }

    [Fact]
    public void PersistentTrackStateChangesAcrossHeats()
    {
        var initial = Result.Value.Evolution[0].CornerSurfaces;
        var first = Result.Value.Evolution[1].CornerSurfaces;
        Assert.Contains(first.Zip(initial), pair =>
            pair.First.Ruts > pair.Second.Ruts || pair.First.Grip < pair.Second.Grip);
        Assert.Contains(Result.Value.Evolution[^1].CornerSurfaces.Zip(first), pair =>
            pair.First.Ruts > pair.Second.Ruts || pair.First.Grip < pair.Second.Grip);
    }

    [Fact]
    public void FixedLineBenchmarksDoNotMutatePersistentState()
    {
        Assert.True(Result.Value.BenchmarkCopiesPreservedPersistentState);
    }

    [Fact]
    public void WearRunIsDeterministic()
    {
        var second = DynamicLineChoiceTrackEvolutionExperiment.Run();
        Assert.Equal(DynamicLineChoiceTrackEvolutionReport.Render(Result.Value),
            DynamicLineChoiceTrackEvolutionReport.Render(second));
    }

    [Fact]
    public void WearIsRiderOrderInvariant()
    {
        Assert.True(Result.Value.RiderOrderInvariant);
    }

    [Fact]
    public void SurfaceChangesAreRecordedAfterEveryHeat()
    {
        Assert.Equal(13, Result.Value.Evolution.Count);
        Assert.All(Result.Value.Evolution, item => Assert.Equal(10, item.CornerSurfaces.Count));
        Assert.All(Result.Value.Evolution.Skip(1), item =>
            Assert.Contains(item.CornerSurfaces, surface => surface.Ruts > 0f));
    }

    [Fact]
    public void LineUsageAccountingIsExact()
    {
        Assert.All(Result.Value.Evolution.Skip(1), item =>
        {
            Assert.Equal(item.ObservedTurnSegments, item.LaneUsageCounts.Sum());
            Assert.Equal(item.ObservedSegments, item.TargetLaneCounts.Sum());
            Assert.Equal(item.ObservedCornerEntries, item.CornerEntryLaneHistogram.Sum());
        });
    }

    [Fact]
    public void SnapshotAfterEveryHeatIsDeterministicallyOrdered()
    {
        foreach (var item in Result.Value.Evolution)
            Assert.Equal(item.CornerSurfaces.OrderBy(surface => surface.CornerId)
                .ThenBy(surface => surface.Lane), item.CornerSurfaces);
    }

    [Fact]
    public void TrackWorkUsesExistingProductionFunction()
    {
        Assert.Equal(2, Result.Value.TrackWork.SelectedLanes.Count);
        var final = Result.Value.Evolution.Single(item => item.Heat == 12);
        var expected = Enumerable.Range(0, LaneModel.LanesCount)
            .OrderByDescending(lane => final.CornerSurfaces.Where(item => item.Lane == lane)
                .Average(item => item.Ruts))
            .ThenBy(lane => lane).Take(2);
        Assert.Equal(expected, Result.Value.TrackWork.SelectedLanes);
        Assert.Contains("TrackEvolution.ApplyTrackWork",
            CanonicalText("src/CoreSim/Analysis/DynamicLineChoiceTrackEvolutionExperiment.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TrackWorkChangesOnlyRequestedTurnsAndLanes()
    {
        var track = TrackFixture();
        var state = TrackState.CreateDefault(track, CalibrationScenarioCatalog.Baseline.Surface);
        var before = state.Snapshot();
        TrackEvolution.ApplyTrackWork(track, state,
            new TrackWorkAction(TrackWorkType.Pack, .5f, new[] { 1 }, new[] { 2 }),
            45, 0, new SimLog(enabled: false));
        for (var segment = 0; segment < track.Segments.Count; segment++)
        for (var lane = 0; lane < LaneModel.LanesCount; lane++)
        {
            if (segment == 1 && lane == 2)
                Assert.NotEqual(before.GetSurface(segment, lane), state.GetSurface(segment, lane));
            else
                Assert.Equal(before.GetSurface(segment, lane), state.GetSurface(segment, lane));
        }
    }

    [Fact]
    public void WeatherSanityUsesExistingProductionFunction()
    {
        Assert.Equal(WeatherCondition.Rain, Result.Value.Weather.Condition);
        Assert.Equal(.5f, Result.Value.Weather.RainIntensity);
        Assert.True(Result.Value.Weather.ExposureCreatesDifferentSurfaces);
        Assert.Contains("TrackEvolution.ApplyWeather",
            CanonicalText("src/CoreSim/Analysis/DynamicLineChoiceTrackEvolutionExperiment.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void EverySeverityHasLineAdvantageCurveRelativeToZero()
    {
        Assert.All(Result.Value.StaticSweeps, sweep =>
        {
            Assert.Equal(0d, sweep.Lines.Min(item => item.DeltaToBestSeconds));
            Assert.All(sweep.Lines, item => Assert.True(item.DeltaToBestSeconds >= 0d));
        });
    }

    [Fact]
    public void SwitchThresholdsAreDerivedRatherThanHardcodedWinners()
    {
        Assert.Equal(1f, Result.Value.OutsideFirstNonInnerBestSeverity);
        Assert.Equal(1f, Result.Value.OutsideFirstOuterHalfBestSeverity);
        Assert.Equal(1f, Result.Value.MiddleFirstNonInnerBestSeverity);
        Assert.Equal(1f, Result.Value.MiddleFirstOuterHalfBestSeverity);
        Assert.Equal(2, Sweep(DynamicLineChoiceTrackEvolutionExperiment.OutsideProfile, 1f).BestLane);
        Assert.Equal(2, Sweep(DynamicLineChoiceTrackEvolutionExperiment.MiddleProfile, 1f).BestLane);
    }

    [Fact]
    public void EvolutionBenchmarkHasHeatZeroThroughTwelve()
    {
        Assert.Equal(Enumerable.Range(0, 13), Result.Value.Evolution.Select(item => item.Heat));
        Assert.All(Result.Value.Evolution, item => Assert.Equal(5, item.FixedLines.Count));
    }

    [Fact]
    public void EvolutionFlagsFollowDocumentedDiagnosticRules()
    {
        var sequence = Result.Value.Evolution.Select(item => item.BestPhysicalLane).ToArray();
        var switches = sequence.Zip(sequence.Skip(1)).Count(pair => pair.First != pair.Second);
        Assert.Equal(switches > 4, Result.Value.TrackEvolutionOscillationRisk);
        Assert.False(Result.Value.TrackEvolutionTooWeak);
        Assert.True(Result.Value.TrackEvolutionTooStrong);
    }

    [Fact]
    public void StaticPhysicalOptimumCanMoveWithoutAssertingSpecificWinnerInHarness()
    {
        Assert.Contains(Result.Value.StaticSweeps, item => item.BestLane != 0);
        var source = CanonicalText("src/CoreSim/Analysis/DynamicLineChoiceTrackEvolutionExperiment.cs");
        Assert.DoesNotContain("Assert", source, StringComparison.Ordinal);
        Assert.DoesNotContain("outer lane bonus", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExperimentTypesContainNoMutableStaticState()
    {
        foreach (var field in new[]
                 {
                     typeof(DynamicLineChoiceTrackEvolutionExperiment),
                     typeof(DynamicLineChoiceTrackEvolutionReport),
                 }.SelectMany(type => type.GetFields(
                     BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
            Assert.True(field.IsInitOnly || field.IsLiteral,
                $"Mutable static field: {field.DeclaringType}.{field.Name}");
    }

    [Fact]
    public void FrozenProductionSourceFilesRemainCanonicalByteIdentical()
    {
        AssertHashes(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/CoreSim/Track/SegmentPhysics.cs"] = "693D3C911F76CB83F60CD6DED850490F101B0FE0F6558D453DC35927DC123B16",
            ["src/CoreSim/Track/ContinuousCornerEnvelope.cs"] = "4C86C9B232D8112150AD8FB9D5CF1E2B65941A3D132C7D82E61379FE367F748B",
            ["src/CoreSim/Track/LongitudinalDynamics.cs"] = "33349B6719C69F4F4743D69200A68266D452A7EDD601E702E1E7F9233640EF31",
            ["src/CoreSim/Track/LateralMovementModel.cs"] = "4A5A71D726B10D1D52C84BCFC053E63E11A4F4D21A381225DC23821D3BA9AC44",
            ["src/CoreSim/Decisions/AdaptiveDecisionModel.cs"] = "A1FCBA7E068C09B69CF849492424C89083BAD69F965FC6D767415EA9F7478314",
            ["src/CoreSim/Track/TrackEvolution.cs"] = "DF64FB9A640601FB5A381E4307E6D605A848CB470D46B3C5616D505D7671E27B",
            ["src/CoreSim/Track/TrackState.cs"] = "20CA39B95F183F1F19C7ED8ABD5AB0C4F079FA06F58465876284F47BA3C3DBAF",
            ["src/CoreSim/SimulationEngine.cs"] = "62424F726A31BDB69900A4468D8F2EE190F6D7C7129AE3CF31F6053404416746",
        });
    }

    [Fact]
    public void HistoricalCalibrationEvidenceAndReportsRemainCanonicalByteIdentical()
    {
        AssertHashes(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["data/calibration/pge/v1/literature_targets.json"] = "0BA6F118F0FC3F8A6298302FAC59CF04E72563D8773D22D59E5E56F367FE7765",
            ["data/calibration/pge/v1/pge_matches.csv"] = "88F9450116CF1807B3884FB9A84A953763B919C0E8D03649516064391885F99B",
            ["data/calibration/pge/v1/pge_rider_heats.csv"] = "DCBFF29E5EEB11402271D083DC8AF23BCDC0D7A921001B2B8A53228690CC29DC",
            ["data/calibration/pge/v1/pge_split.csv"] = "609BC682360658F3B452E8BC71B92665CB50CAE5B084AC7B21403A886F715A2C",
            ["data/calibration/pge/v1/README.md"] = "85125FAF298604AF018491BB0A382594A053B3FB89DF80E21590E6570E601C0E",
            ["data/calibration/pge/v1/source_manifest.json"] = "F2186F38E165DB26B372623EABCA732AFC8E3070A838C56ADEEA4C8336E4B3FD",
            ["data/calibration/pge/v1/summary.json"] = "4015DF4FBE1D9A78EAD116AC2B725BE3C41721DD079558FEE27C8AB45E8431F8",
            ["docs/calibration/continuous-corner-envelope-impact.md"] = "9F97DE671EF6656E3054BC408C2B089CDC0D225208F7A6D0C5A2D36F3489CBB9",
            ["docs/calibration/motoarena-matched-venue.md"] = "E13FD2A9D3B21C7AF5F2FB8C9BBBE89EEB6A796A4AAE4299D0D46EC04807DAF9",
            ["docs/calibration/real-start-telemetry.md"] = "916B6DDB543A70D2DFCD5110F90DCDD1C345B58562BB9A3780E6CCBBF4039913",
            ["docs/calibration/straight-drive-envelope-experiment.md"] = "AC99CCC757584E9F1DB833C6F4EF7343BD109479EC964AAC013A73A50E016BA8",
            ["docs/calibration/corner-reduced-drive-resistance-experiment.md"] = "A3C75BBD06EA59BCEE97F1753EFCAACD80436676379E03D562FBE81408F11E72",
            ["docs/calibration/pre-apex-scrub-loss-experiment.md"] = "7ABD8AF54C9E8FB8B9E1D281F911D45F00505DE42FBF7C7363D077E2D02CB5B3",
            ["docs/calibration/gameplay-corner-control-loss-experiment.md"] = "2549714ABDD87FDAD6F9C092EF03837CC433EF4B412CC97F980E4356461963B3",
        });
    }

    [Fact]
    public void ReportIsCommittedCultureInvariantLfOnlyAndHasExactAToZSections()
    {
        var expected = File.ReadAllText(Path.Combine(Root, "docs", "calibration",
            "dynamic-line-choice-track-evolution.md"));
        Assert.DoesNotContain('\r', expected.Replace("\r\n", "\n", StringComparison.Ordinal));
        var en = RenderUnderCulture("en-US");
        var pl = RenderUnderCulture("pl-PL");
        HistoricalPhysicsSource.AssertArtifactUnchanged("docs/calibration/dynamic-line-choice-track-evolution.md");
        Assert.Equal(en, pl);
        Assert.DoesNotContain("NaN", expected, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", expected, StringComparison.Ordinal);
        foreach (var section in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            Assert.Equal(1, Count(en, $"## {section}."));
    }

    [Fact]
    public void ReportAnswersAllFifteenGameplayQuestionsDirectly()
    {
        var report = DynamicLineChoiceTrackEvolutionReport.Render(Result.Value);
        for (var index = 1; index <= 15; index++)
            Assert.Contains($"{index}.", report, StringComparison.Ordinal);
        Assert.Contains("First actual bottleneck", report, StringComparison.Ordinal);
        Assert.Contains("Recommended single subsystem", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportContainsRepairedDecisionAndExecutionDiagnostics()
    {
        var report = DynamicLineChoiceTrackEvolutionReport.Render(Result.Value);
        foreach (var heading in new[]
                 {
                     "Decision oracle at TrackReading 100",
                     "Perception agreement vs physical regret",
                     "Decision-objective mismatch",
                     "Planner waypoint binding",
                     "Execution capacity vs realized movement",
                     "Physical line sensitivity",
                 })
            Assert.Contains(heading, report, StringComparison.Ordinal);
        Assert.Contains("constant-reference-line benchmark does not model diagonal/spiral racing trajectory",
            report, StringComparison.Ordinal);
        Assert.DoesNotContain("TrackReadingSignalTooWeak", report, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionSkillSignalTooWeak", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassificationUsesMandatoryFailureLayerPriority()
    {
        Assert.Equal(
            "MixedDynamicLineGameplayDiagnostics: LineSwitchRequiresStrongSurfaceContrast, "
            + "DecisionObjectiveVsFastestLineMismatch, TrackReadingPerceptionSignalHealthy, "
            + "ExecutionPlannerBound, PlanningHorizonLimitsExecutionExpression, "
            + "TrackEvolutionTooStrong",
            Result.Value.Classification);
        Assert.True(Result.Value.DecisionObjectiveVsFastestLineMismatch);
        Assert.Equal("decision objective alignment", Result.Value.FirstActualBottleneck);
        Assert.Equal("AdaptiveDecisionModel route-cost alignment", Result.Value.RecommendedSubsystem);
    }

    private static LineSweepSummary Sweep(string profile, float severity) =>
        Result.Value.StaticSweeps.Single(item => item.ProfileId == profile
            && BitConverter.SingleToInt32Bits(item.Severity)
            == BitConverter.SingleToInt32Bits(severity));

    private static Track TrackFixture() => MatchedVenueProfiles.Motoarena2026.CreateTrack(
        MatchedVenueProfiles.MotoarenaHistorical39StartLineToFirstCornerMeters);

    private static string RenderUnderCulture(string culture)
    {
        var prior = CultureInfo.CurrentCulture;
        var priorUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return DynamicLineChoiceTrackEvolutionReport.Render(Result.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
            CultureInfo.CurrentUICulture = priorUi;
        }
    }

    private static void AssertFinite(params double[] values) =>
        Assert.All(values, value => Assert.True(double.IsFinite(value)));

    private static void AssertHashes(IReadOnlyDictionary<string, string> expected)
    {
        foreach (var (relative, sha) in expected)
        {
            if (relative == "src/CoreSim/SimulationEngine.cs") { HistoricalPhysicsSource.AssertRecordedEngineProvenance(sha); continue; }
            var text = HistoricalPhysicsSource.ForHash(relative, CanonicalText(relative));
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(text))));
        }
    }

    private static string CanonicalText(string relative) => File.ReadAllText(Path.Combine(
            Root, relative.Replace('/', Path.DirectorySeparatorChar)))
        .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static int Count(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SpeedwayManager.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
