using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Analysis;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "historical-analysis")]
public sealed class DynamicCornerTrajectoryGeometryExperimentTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly Lazy<DynamicCornerTrajectoryGeometryExperimentResult> Result =
        new(DynamicCornerTrajectoryGeometryExperiment.Run);

    [Fact]
    public void SquashBaseIsExact() => Assert.Equal(
        "4c54c7d2cc87c79ad7c28fd2b86080fb68319a99",
        DynamicCornerTrajectoryGeometryExperiment.BaseMainSha);

    [Fact]
    public void ProductionSourceIsNotModifiedByExperiment() => Assert.Equal(
        new[]
        {
            "docs/calibration/dynamic-corner-trajectory-geometry.md",
            "src/CoreSim/Analysis/DynamicCornerTrajectoryGeometryExperiment.cs",
            "src/CoreSim/Analysis/DynamicCornerTrajectoryGeometryReport.cs",
            "src/Sandbox/DynamicCornerTrajectoryGeometryReportWriter.cs",
            "src/Sandbox/Program.cs",
            "tests/CoreSim.Tests/DynamicCornerTrajectoryGeometryExperimentTests.cs",
        }, ChangedPathsExpected());

    [Fact]
    public void AllCalibrationOnlyAdjustmentsDefaultNull()
    {
        var options = new HeatSimulationOptions();
        Assert.Null(options.StraightDriveEnvelopeAdjustment);
        Assert.Null(options.CornerReducedDriveResistanceAdjustment);
        Assert.Null(options.PreApexScrubLossAdjustment);
        Assert.Null(options.ActiveCorrectionControlLossAdjustment);
    }

    [Fact]
    public void TrajectoryDecisionModelIsInternalAndAnalysisOnly()
    {
        var type = typeof(DynamicCornerTrajectoryGeometryExperiment).Assembly
            .GetType("CoreSim.Analysis.TrajectoryPlanDecisionModel", throwOnError: true)!;
        Assert.False(type.IsPublic);
        Assert.Equal("CoreSim.Analysis", type.Namespace);
        // Preserve the historical #50 contract through the exact reviewed #56B
        // metadata extraction, independently pinned by its complete fixture hash.
        Assert.DoesNotContain("Trajectory", HistoricalPhysicsSource.ForHash("src/CoreSim/Decisions/RiderDecision.cs",
            CanonicalText("src/CoreSim/Decisions/RiderDecision.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Exactly125TargetPlansAreGenerated() => Assert.Equal(125, Result.Value.TargetPlans.Count);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryEntryTargetAppearsExactly25Times(int target) => Assert.Equal(25,
        Result.Value.TargetPlans.Count(item => item.EntryTarget == target));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryApexTargetAppearsExactly25Times(int target) => Assert.Equal(25,
        Result.Value.TargetPlans.Count(item => item.ApexTarget == target));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryExitTargetAppearsExactly25Times(int target) => Assert.Equal(25,
        Result.Value.TargetPlans.Count(item => item.ExitTarget == target));

    [Fact]
    public void CandidateOrderingIsDeterministicAndCanonical()
    {
        Assert.Equal("E0-A0-X0", Result.Value.TargetPlans[0].Id);
        Assert.Equal("E0-A0-X1", Result.Value.TargetPlans[1].Id);
        Assert.Equal("E4-A4-X4", Result.Value.TargetPlans[^1].Id);
        Assert.Equal(125, Result.Value.TargetPlans.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void SameSeedIsDeterministic() => Assert.True(Result.Value.Deterministic);

    [Fact]
    public void ReportIsInvariantCulture()
    {
        var en = RenderUnderCulture("en-US");
        var pl = RenderUnderCulture("pl-PL");
        Assert.Equal(en, pl);
    }

    [Fact]
    public void ReportIsLfOnly() => Assert.DoesNotContain('\r',
        DynamicCornerTrajectoryGeometryReport.Render(Result.Value));

    [Fact]
    public void AllActualLateralPositionsStayInDomain()
    {
        Assert.All(Result.Value.UniformPlans.SelectMany(item => item.Corners), corner =>
        {
            Assert.InRange(corner.EntryLateralPosition, 0f, 4f);
            Assert.InRange(corner.OneThirdLateralPosition, 0f, 4f);
            Assert.InRange(corner.ApexLateralPosition, 0f, 4f);
            Assert.InRange(corner.TwoThirdsLateralPosition, 0f, 4f);
            Assert.InRange(corner.ExitLateralPosition, 0f, 4f);
        });
    }

    [Fact]
    public void ActualPathComesFromProductionTrace()
    {
        var source = CanonicalText("src/CoreSim/Analysis/DynamicCornerTrajectoryGeometryExperiment.cs");
        Assert.Contains("CalibrationRunner.RunHeat", source, StringComparison.Ordinal);
        Assert.Contains("ContinuousCornerProfile", source, StringComparison.Ordinal);
        Assert.Contains("EntryLateralPosition", source, StringComparison.Ordinal);
        Assert.Contains("ExitLateralPosition", source, StringComparison.Ordinal);
        Assert.DoesNotContain("trajectory physics calculator", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TargetPlanIsNotAssumedToBeActualPath()
    {
        var planned = Result.Value.UniformPlans.Single(item => item.Plan ==
            DynamicCornerTrajectoryGeometryExperiment.NamedReferences["TightEntryWideExit"]);
        Assert.True(MathF.Abs(planned.Plan.EntryTarget - planned.EntryLateralPosition) > 1e-4f);
        Assert.True(MathF.Abs(planned.Plan.ApexTarget - planned.ApexLateralPosition) > 1e-4f);
        Assert.Contains(planned.Corners, corner =>
            MathF.Abs(planned.Plan.ApexTarget - corner.ApexLateralPosition) > 1e-4f);
    }

    [Fact]
    public void FingerprintsAreCanonicalAndDeterministic()
    {
        Assert.All(Result.Value.UniformPlans, item =>
        {
            Assert.StartsWith("C0:", item.ActualTrajectoryFingerprint, StringComparison.Ordinal);
            Assert.Contains("|C1:", item.ActualTrajectoryFingerprint, StringComparison.Ordinal);
            Assert.DoesNotContain(' ', item.ActualTrajectoryFingerprint);
        });
    }

    [Fact]
    public void DuplicatePlansGroupByActualFingerprint()
    {
        Assert.InRange(Result.Value.UniqueUniformTrajectories.Count, 1, 125);
        Assert.Equal(125 - Result.Value.UniqueUniformTrajectories.Count, Result.Value.DuplicateTargetPlanCount);
        Assert.Equal(Result.Value.UniqueUniformTrajectories.Count,
            Result.Value.UniformPlans.Select(item => item.ActualTrajectoryFingerprint).Distinct().Count());
    }

    [Fact]
    public void TrueApexRemainsPointFive()
    {
        Assert.All(Result.Value.UniformPlans, item => Assert.InRange(item.MinimumSpeedCornerProgress, 0f, 1f));
        var report = DynamicCornerTrajectoryGeometryReport.Render(Result.Value);
        Assert.Contains("p=.500", report, StringComparison.Ordinal);
    }

    [Fact]
    public void MinimumSpeedRemainsSeparateFromTrueApex()
    {
        Assert.Contains(Result.Value.UniformPlans,
            item => MathF.Abs(item.MinimumSpeedCornerProgress - .5f) > 1e-4f);
        Assert.All(Result.Value.UniformPlans, item =>
            Assert.True(item.MinimumSpeedMetersPerSecond <= item.TrueApexSpeedMetersPerSecond + 1e-5f));
    }

    [Fact]
    public void NoTrajectoryTeleportIsAssumed()
    {
        Assert.Contains(Result.Value.UniformPlans, item => item.PlannerCapBindingCount > 0);
        Assert.Contains(Result.Value.UniformPlans, item =>
            item.Plan.ExitTarget == 4 && item.ExitLateralPosition < 4f);
    }

    [Fact]
    public void PlannerCapRemainsObservableAndUnchanged()
    {
        Assert.Equal(96, Result.Value.PlannerBoundPlanCount);
        AssertCanonicalHash("src/CoreSim/SimulationEngine.cs",
            "62424F726A31BDB69900A4468D8F2EE190F6D7C7129AE3CF31F6053404416746");
    }

    [Fact]
    public void PlannerIndependentSubsetContainsOnlyZeroBindingTrajectories()
    {
        Assert.NotEmpty(Result.Value.PlannerIndependentTrajectories);
        Assert.All(Result.Value.PlannerIndependentTrajectories,
            item => Assert.Equal(0, item.PlannerCapBindingCount));
    }

    [Fact]
    public void ConstantInnerIsPlannerIndependent() => Assert.Contains(
        Result.Value.ConstantInner, Result.Value.PlannerIndependentTrajectories);

    [Fact]
    public void E0A1X1RemainsPlannerIndependent() => Assert.Equal(
        0, E0A1X1().PlannerCapBindingCount);

    [Fact]
    public void E0A1X1IsAnActualWideExit() => Assert.True(
        E0A1X1().ExitLateralPosition > E0A1X1().EntryLateralPosition
            + DynamicCornerTrajectoryGeometryExperiment.WideExitThreshold);

    [Fact]
    public void E0A1X1ExitRadiusExceedsConstantInner() => Assert.True(
        E0A1X1().ExitRadiusMeters > Result.Value.ConstantInner.ExitRadiusMeters);

    [Fact]
    public void E0A1X1ExitSpeedExceedsConstantInner() => Assert.True(
        E0A1X1().CornerExitSpeedMetersPerSecond
            > Result.Value.ConstantInner.CornerExitSpeedMetersPerSecond);

    [Fact]
    public void E0A1X1TenMeterSpeedExceedsConstantInner() => Assert.True(
        E0A1X1().SpeedTenMetersAfterCornerMetersPerSecond
            > Result.Value.ConstantInner.SpeedTenMetersAfterCornerMetersPerSecond);

    [Fact]
    public void E0A1X1FirstTwentyMetersIsQuickerThanConstantInner() => Assert.True(
        E0A1X1().FirstTwentyMetersStraightTimeSeconds
            < Result.Value.ConstantInner.FirstTwentyMetersStraightTimeSeconds);

    [Fact]
    public void E0A1X1StillLosesToConstantInner() => Assert.True(
        E0A1X1().FlyingLapMedianSeconds > Result.Value.ConstantInner.FlyingLapMedianSeconds);

    [Fact]
    public void PlannerIndependentDynamicUsesActualTrajectoryNotTargetShape()
    {
        var constantFingerprints = Result.Value.UniformPlans.Where(item => item.Plan.IsConstant)
            .Select(item => item.ActualTrajectoryFingerprint).ToHashSet(StringComparer.Ordinal);
        var dynamic = Assert.IsType<TrajectoryObservation>(Result.Value.BestPlannerIndependentDynamic);
        Assert.DoesNotContain(dynamic.ActualTrajectoryFingerprint, constantFingerprints);
    }

    [Fact]
    public void PlannerResolutionFlagRemainsSeparateAndTrue() => Assert.True(
        Result.Value.TrajectoryPlannerResolutionTooCoarse);

    [Fact]
    public void GlobalBoundPlanPercentageDoesNotDetermineDominanceCause()
    {
        Assert.True(Result.Value.PlannerBoundPlanCount > Result.Value.TargetPlans.Count / 2);
        Assert.True(Result.Value.PlannerIndependentDynamicTrajectoryExists);
        Assert.True(Result.Value.PlannerIndependentWideExitExists);
        Assert.False(Result.Value.PlannerCanExplainConstantInnerDominance);
    }

    [Fact]
    public void SegmentEntrySamplingLimitationMatchesFrozenProductionContract()
    {
        var source = CanonicalText("src/CoreSim/SimulationEngine.cs");
        Assert.Contains("SampleSurface(snapshot.Step.SegmentIndex, rider.LateralPosition)", source,
            StringComparison.Ordinal);
        Assert.Contains("snapshot.Segment,\n                rider.LateralPosition,\n                snapshot.Track.Geometry",
            source, StringComparison.Ordinal);
        Assert.Contains("ContinuousCornerEnvelope.Create(phase, rider.LateralPosition", source,
            StringComparison.Ordinal);
        Assert.True(Result.Value.WithinSegmentTrajectorySamplingLimitation);
    }

    [Fact]
    public void BestPlannerIndependentWideExitUsesWithinSegmentLateralChange()
    {
        var wide = Assert.IsType<TrajectoryObservation>(Result.Value.BestPlannerIndependentWideExit);
        Assert.Equal("E0-A1-X1", wide.Plan.Id);
        Assert.Contains(wide.TurnSegments, item => item.HasWithinSegmentLateralChange);
        Assert.True(Result.Value.DynamicPathUsesWithinSegmentLateralChange);
        Assert.True(Result.Value.WithinSegmentChangingRadiusNotFullyIntegrated);
    }

    [Fact]
    public void PlannerIndependentControlsExposeProductionEntrySurfacePerTurnSegment()
    {
        var controls = new[]
        {
            Assert.IsType<TrajectoryObservation>(Result.Value.BestPlannerIndependentDynamic),
            Assert.IsType<TrajectoryObservation>(Result.Value.BestPlannerIndependentWideExit),
        };
        Assert.All(controls, control =>
            Assert.Contains(control.TurnSegments, segment => segment.HasWithinSegmentLateralChange));
        Assert.All(controls.SelectMany(item => item.TurnSegments), segment =>
        {
            AssertFinitePositive(segment.EntrySurfaceGrip, segment.EntrySurfaceEffectiveGrip);
            AssertFiniteNonNegative(segment.EntrySurfaceRuts, segment.EntrySurfaceMoisture);
            AssertFinitePositive(segment.EntryRadiusMeters, segment.ObservedExitRadiusMeters);
        });
    }

    [Fact]
    public void WideExitBenefitCostUsesObservedProductionValues()
    {
        var inner = Result.Value.ConstantInner;
        var wide = Assert.IsType<TrajectoryObservation>(Result.Value.BestPlannerIndependentWideExit);
        var benefit = Assert.IsType<TrajectoryBenefitCostComparison>(Result.Value.WideExitBenefitCost);
        Assert.Equal(wide.CornerExitSpeedMetersPerSecond - inner.CornerExitSpeedMetersPerSecond,
            benefit.ExitSpeedGainMetersPerSecond);
        Assert.Equal(wide.SpeedTenMetersAfterCornerMetersPerSecond
            - inner.SpeedTenMetersAfterCornerMetersPerSecond, benefit.TenMeterSpeedGainMetersPerSecond);
        Assert.Equal(inner.FirstTwentyMetersStraightTimeSeconds
            - wide.FirstTwentyMetersStraightTimeSeconds, benefit.FirstTwentyMetersStraightTimeGainSeconds);
        Assert.Equal(wide.FlyingLapMedianSeconds - inner.FlyingLapMedianSeconds,
            benefit.FlyingLossSeconds);
        Assert.Equal(wide.FourLapDistanceMeters - inner.FourLapDistanceMeters,
            benefit.ProductionDistanceDifferenceMeters);
    }

    [Fact]
    public void ExistingUniformResultsRemainNumericallyUnchanged()
    {
        var snapshot = CanonicalText("docs/calibration/dynamic-corner-trajectory-geometry.md");
        Assert.Contains("UniqueActualTrajectoryCount: **60**", snapshot);
        HistoricalPhysicsSource.AssertArtifactUnchanged("docs/calibration/dynamic-corner-trajectory-geometry.md");
    }

    [Fact]
    public void LateralMovementSourceRemainsUnchanged() => AssertCanonicalHash(
        "src/CoreSim/Track/LateralMovementModel.cs",
        "4A5A71D726B10D1D52C84BCFC053E63E11A4F4D21A381225DC23821D3BA9AC44");

    [Theory]
    [InlineData(0, "ConstantInner", 13.397707)]
    [InlineData(1, "ConstantL1", 13.737913)]
    [InlineData(2, "ConstantL2", 14.092064)]
    [InlineData(3, "ConstantL3", 14.456406)]
    [InlineData(4, "ConstantOuter", 14.828415)]
    public void ConstantPlansReproduce45(int lane, string name, double expectedFlying)
    {
        var value = Result.Value.UniformPlans.Single(item => item.Plan ==
            DynamicCornerTrajectoryGeometryExperiment.NamedReferences[name]);
        Assert.Equal(lane, value.Plan.EntryTarget);
        Assert.InRange(value.FlyingLapMedianSeconds, expectedFlying - 1e-5d, expectedFlying + 1e-5d);
        Assert.All(value.Corners, corner =>
        {
            Assert.InRange(corner.EntryLateralPosition, lane - 1e-5f, lane + 1e-5f);
            Assert.InRange(corner.ApexLateralPosition, lane - 1e-5f, lane + 1e-5f);
            Assert.InRange(corner.ExitLateralPosition, lane - 1e-5f, lane + 1e-5f);
        });
    }

    [Fact]
    public void ConstantDistancesStrictlyIncreaseInnerToOuter()
    {
        var constants = Result.Value.UniformPlans.Where(item => item.Plan.IsConstant)
            .OrderBy(item => item.Plan.EntryTarget).ToArray();
        Assert.True(constants.Zip(constants.Skip(1)).All(pair =>
            pair.Second.FourLapDistanceMeters > pair.First.FourLapDistanceMeters));
        Assert.True(Result.Value.ConstantReferencesReproduce45);
    }

    [Fact]
    public void RadiusDiagnosticsAreFinitePositive() => Assert.All(
        Result.Value.UniformPlans, item => AssertFinitePositive(item.EntryRadiusMeters,
            item.MinimumRadiusMeters, item.MeanTimeWeightedRadiusMeters,
            item.ApexRadiusMeters, item.ExitRadiusMeters));

    [Fact]
    public void LateralAccelerationProxyIsFiniteNonNegative() => Assert.All(
        Result.Value.UniformPlans, item => AssertFiniteNonNegative(
            item.PeakLateralAccelerationProxyMetersPerSecondSquared,
            item.MeanTimeWeightedLateralAccelerationProxyMetersPerSecondSquared));

    [Fact]
    public void ExtraDiagonalEstimateIsFiniteNonNegative() => Assert.All(
        Result.Value.UniformPlans, item => AssertFiniteNonNegative(
            item.EstimatedExtraDiagonalDistancePerLapMeters,
            item.EstimatedExtraTimeEquivalentSeconds));

    [Fact]
    public void ExtraDiagonalEstimateDoesNotAffectProductionTiming()
    {
        var inner = Result.Value.ConstantInner;
        Assert.Equal(0f, inner.EstimatedExtraDiagonalDistancePerLapMeters);
        Assert.InRange(inner.FlyingLapMedianSeconds, 13.397697d, 13.397717d);
    }

    [Fact]
    public void CorrectionDiagnosticsAreFinite() => Assert.All(
        Result.Value.UniformPlans, item => AssertFiniteNonNegative(
            item.CorrectionDistanceMeters, item.CorrectionTimeSeconds,
            item.PreApexCorrectionDistanceMeters, item.PostApexCorrectionDistanceMeters));

    [Fact]
    public void ExitQualityDiagnosticsAreFinite() => Assert.All(
        Result.Value.UniformPlans, item => AssertFinitePositive(
            item.CornerExitSpeedMetersPerSecond,
            item.SpeedTenMetersAfterCornerMetersPerSecond,
            item.ApexToExitTimeSeconds,
            item.FirstTwentyMetersStraightTimeSeconds));

    [Fact]
    public void AllTrajectoryTimesAreFinitePositive() => Assert.All(
        Result.Value.UniformPlans, item => AssertFinitePositive(
            item.FlyingLapMedianSeconds, item.HeatTimeSeconds));

    [Fact]
    public void NoNumericReportValueIsNaN() => Assert.DoesNotContain("NaN",
        DynamicCornerTrajectoryGeometryReport.Render(Result.Value), StringComparison.Ordinal);

    [Fact]
    public void NoNumericReportValueIsInfinity() => Assert.DoesNotContain("Infinity",
        DynamicCornerTrajectoryGeometryReport.Render(Result.Value), StringComparison.Ordinal);

    [Theory]
    [InlineData("src/CoreSim/Decisions/AdaptiveDecisionModel.cs", "A1FCBA7E068C09B69CF849492424C89083BAD69F965FC6D767415EA9F7478314")]
    [InlineData("src/CoreSim/Decisions/RiderDecision.cs", "A38180EAF19EA50A7C0863A1B1C4FDD59776330ED6170ACF01F0AB77FA678052")]
    [InlineData("src/CoreSim/Track/LateralMovementModel.cs", "4A5A71D726B10D1D52C84BCFC053E63E11A4F4D21A381225DC23821D3BA9AC44")]
    [InlineData("src/CoreSim/Track/SegmentPhysics.cs", "693D3C911F76CB83F60CD6DED850490F101B0FE0F6558D453DC35927DC123B16")]
    [InlineData("src/CoreSim/Track/ContinuousCornerEnvelope.cs", "4C86C9B232D8112150AD8FB9D5CF1E2B65941A3D132C7D82E61379FE367F748B")]
    [InlineData("src/CoreSim/Track/LongitudinalDynamics.cs", "33349B6719C69F4F4743D69200A68266D452A7EDD601E702E1E7F9233640EF31")]
    [InlineData("src/CoreSim/Track/TrackEvolution.cs", "DF64FB9A640601FB5A381E4307E6D605A848CB470D46B3C5616D505D7671E27B")]
    [InlineData("src/CoreSim/Track/TrackState.cs", "20CA39B95F183F1F19C7ED8ABD5AB0C4F079FA06F58465876284F47BA3C3DBAF")]
    [InlineData("src/CoreSim/SimulationEngine.cs", "62424F726A31BDB69900A4468D8F2EE190F6D7C7129AE3CF31F6053404416746")]
    public void FrozenProductionFilesRemainCanonical(string path, string hash) =>
        AssertCanonicalHash(path, hash);

    [Theory]
    [InlineData("data/calibration/pge/v1/summary.json", "4015DF4FBE1D9A78EAD116AC2B725BE3C41721DD079558FEE27C8AB45E8431F8")]
    [InlineData("docs/calibration/continuous-corner-envelope-impact.md", "9F97DE671EF6656E3054BC408C2B089CDC0D225208F7A6D0C5A2D36F3489CBB9")]
    [InlineData("docs/calibration/motoarena-matched-venue.md", "E13FD2A9D3B21C7AF5F2FB8C9BBBE89EEB6A796A4AAE4299D0D46EC04807DAF9")]
    [InlineData("docs/calibration/real-start-telemetry.md", "916B6DDB543A70D2DFCD5110F90DCDD1C345B58562BB9A3780E6CCBBF4039913")]
    [InlineData("docs/calibration/straight-drive-envelope-experiment.md", "AC99CCC757584E9F1DB833C6F4EF7343BD109479EC964AAC013A73A50E016BA8")]
    [InlineData("docs/calibration/corner-reduced-drive-resistance-experiment.md", "A3C75BBD06EA59BCEE97F1753EFCAACD80436676379E03D562FBE81408F11E72")]
    [InlineData("docs/calibration/pre-apex-scrub-loss-experiment.md", "7ABD8AF54C9E8FB8B9E1D281F911D45F00505DE42FBF7C7363D077E2D02CB5B3")]
    [InlineData("docs/calibration/gameplay-corner-control-loss-experiment.md", "2549714ABDD87FDAD6F9C092EF03837CC433EF4B412CC97F980E4356461963B3")]
    [InlineData("docs/calibration/dynamic-line-choice-track-evolution.md", "20BF300FFAF17DA588AE5670F8C4991C9078810DEC8E3B1CF5AE039F45E330CF")]
    public void HistoricalArtifactsRemainCanonical(string path, string hash) =>
        AssertCanonicalHash(path, hash);

    [Fact]
    public void AnalysisTypesContainNoMutableStaticState()
    {
        foreach (var field in new[]
                 {
                     typeof(DynamicCornerTrajectoryGeometryExperiment),
                     typeof(DynamicCornerTrajectoryGeometryReport),
                 }.SelectMany(type => type.GetFields(BindingFlags.Static |
                     BindingFlags.Public | BindingFlags.NonPublic)))
            Assert.True(field.IsInitOnly || field.IsLiteral,
                $"Mutable static field: {field.DeclaringType}.{field.Name}");
    }

    [Fact]
    public void ReportHasExactAToZSections()
    {
        var report = DynamicCornerTrajectoryGeometryReport.Render(Result.Value);
        foreach (var section in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            Assert.Equal(1, Count(report, $"## {section}."));
    }

    [Fact]
    public void ReportContainsRevisedFailureLayerSections()
    {
        var report = DynamicCornerTrajectoryGeometryReport.Render(Result.Value);
        Assert.Contains("## Planner-independent trajectory subset", report, StringComparison.Ordinal);
        Assert.Contains("## Planner-independent ConstantInner comparison", report, StringComparison.Ordinal);
        Assert.Contains("## Exit benefit versus total-lap cost", report, StringComparison.Ordinal);
        Assert.Contains("## Can planner explain ConstantInner dominance?", report, StringComparison.Ordinal);
        Assert.Contains("## Segment-entry trajectory sampling limitation", report, StringComparison.Ordinal);
        Assert.Contains("## Within-segment radius limitation", report, StringComparison.Ordinal);
        Assert.Contains("## Revised failure-layer diagnosis", report, StringComparison.Ordinal);
        Assert.Contains("## Revised #47 recommendation", report, StringComparison.Ordinal);
        Assert.Contains("Production entry surface G/R/M/E", report, StringComparison.Ordinal);
        Assert.Contains("Minimum/mean radius", report, StringComparison.Ordinal);
        Assert.Contains("Future #47 question:", report, StringComparison.Ordinal);
        Assert.Contains("Fallback hypothesis:", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportSeparatesTargetPlannedAndActual()
    {
        var report = DynamicCornerTrajectoryGeometryReport.Render(Result.Value);
        Assert.Contains("TargetLane → PlannedLane → Lane → continuous LateralPosition", report,
            StringComparison.Ordinal);
        Assert.Contains("Actual positions", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportAvoidsFakeMotorcycleObservables()
    {
        var report = DynamicCornerTrajectoryGeometryReport.Render(Result.Value);
        Assert.DoesNotContain("MotorcycleBreakAngle", report, StringComparison.Ordinal);
        Assert.Contains("not tyre force, lean, yaw, slip angle or slip ratio", report,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReportMatchesCommittedArtifact()
    {
        HistoricalPhysicsSource.AssertArtifactUnchanged("docs/calibration/dynamic-corner-trajectory-geometry.md");
    }

    [Fact]
    public void ClassificationFollowsFailureLayerOrder()
    {
        Assert.True(Result.Value.ConstantInnerDominates);
        Assert.True(Result.Value.TrajectoryPlannerResolutionTooCoarse);
        Assert.False(Result.Value.PlannerCanExplainConstantInnerDominance);
        Assert.True(Result.Value.WithinSegmentChangingRadiusNotFullyIntegrated);
        Assert.True(Result.Value.CornerTrajectoryEconomicsMismatch);
        Assert.Contains("WithinSegmentChangingRadiusNotFullyIntegrated", Result.Value.Classification,
            StringComparison.Ordinal);
        Assert.Equal("corner trajectory physics economics", Result.Value.FirstActualBottleneck);
        Assert.Equal("within-corner continuous trajectory/radius sampling experiment",
            Result.Value.RecommendedSubsystem);
    }

    [Fact]
    public void SurfaceShortlistUsesRequiredProfiles()
    {
        Assert.Equal(4, Result.Value.SurfaceResults.Count);
        Assert.Contains(Result.Value.SurfaceResults, item => item.ProfileId == "OutsideCushion" && item.Severity == .5f);
        Assert.Contains(Result.Value.SurfaceResults, item => item.ProfileId == "OutsideCushion" && item.Severity == 1f);
        Assert.Contains(Result.Value.SurfaceResults, item => item.ProfileId == "MiddleCushion" && item.Severity == .5f);
        Assert.Contains(Result.Value.SurfaceResults, item => item.ProfileId == "MiddleCushion" && item.Severity == 1f);
    }

    [Fact]
    public void SkillShortlistUsesRequiredArchetypes() => Assert.Equal(
        new[] { "Balanced", "Technical", "Low-control", "Fast/Loose" },
        Result.Value.SkillResults.Select(item => item.Archetype));

    [Fact]
    public void SetupSanityUsesRequiredTractionBiases() => Assert.Equal(
        new[] { 0f, .5f, 1f }, Result.Value.SetupResults.Select(item => item.TractionBias));

    [Fact]
    public void OverallBestIsAUniqueActualTrajectory() => Assert.Contains(
        Result.Value.OverallBest, Result.Value.UniqueUniformTrajectories);

    [Fact]
    public void TopFifteenContainsUniqueActualFingerprints() => Assert.Equal(
        Result.Value.TopFifteen.Count,
        Result.Value.TopFifteen.Select(item => item.ActualTrajectoryFingerprint).Distinct().Count());

    [Fact]
    public void WideExitUsesActualReleaseThreshold() => Assert.All(
        Result.Value.UniqueUniformTrajectories.Where(item => item.IsWideExit),
        item => Assert.True(item.ExitLateralPosition - item.EntryLateralPosition > .25f));

    [Fact]
    public void FirstLapIsExcludedFromFlyingMedian() => Assert.All(
        Result.Value.UniformPlans, item => Assert.Equal(3, item.FlyingLapTimesSeconds.Count));

    [Fact]
    public void BothLogicalCornersAreObserved() => Assert.All(
        Result.Value.UniformPlans, item => Assert.Equal(new[] { 0, 1 },
            item.Corners.Select(corner => corner.CornerId)));

    private static string RenderUnderCulture(string culture)
    {
        var prior = CultureInfo.CurrentCulture;
        var priorUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return DynamicCornerTrajectoryGeometryReport.Render(Result.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
            CultureInfo.CurrentUICulture = priorUi;
        }
    }

    private static string[] ChangedPathsExpected() => new[]
    {
        "docs/calibration/dynamic-corner-trajectory-geometry.md",
        "src/CoreSim/Analysis/DynamicCornerTrajectoryGeometryExperiment.cs",
        "src/CoreSim/Analysis/DynamicCornerTrajectoryGeometryReport.cs",
        "src/Sandbox/DynamicCornerTrajectoryGeometryReportWriter.cs",
        "src/Sandbox/Program.cs",
        "tests/CoreSim.Tests/DynamicCornerTrajectoryGeometryExperimentTests.cs",
    };

    private static TrajectoryObservation E0A1X1() => Result.Value.UniformPlans.Single(item =>
        item.Plan == new TrajectoryPlan(0, 1, 1));

    private static void AssertFinitePositive(params double[] values) => Assert.All(values,
        value => Assert.True(double.IsFinite(value) && value > 0d, $"Expected finite positive, got {value}."));

    private static void AssertFiniteNonNegative(params double[] values) => Assert.All(values,
        value => Assert.True(double.IsFinite(value) && value >= 0d, $"Expected finite non-negative, got {value}."));

    private static void AssertCanonicalHash(string relative, string expected)
    {
        if (relative == "src/CoreSim/Decisions/AdaptiveDecisionModel.cs") { HistoricalPhysicsSource.AssertRecordedDecisionProvenance(expected); return; }
        if (relative == "src/CoreSim/SimulationEngine.cs") { HistoricalPhysicsSource.AssertRecordedEngineProvenance(expected); return; }
        if (relative == "src/CoreSim/Track/LongitudinalDynamics.cs") { HistoricalPhysicsSource.AssertRecordedLongitudinalProvenance(expected); return; }
        Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            HistoricalPhysicsSource.ForHash(relative, CanonicalText(relative))))));
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
