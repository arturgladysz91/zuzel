using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class FreeContinuousRacingTrajectoryGeometryExperimentTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly Lazy<FreeContinuousRacingTrajectoryGeometryExperimentResult> Result =
        new(FreeContinuousRacingTrajectoryGeometryExperiment.Run);
    private static readonly Lazy<string> Report = new(() =>
        FreeContinuousRacingTrajectoryGeometryReport.Render(Result.Value));

    [Fact]
    public void SquashBaseIsExact() => Assert.Equal(
        "4abc9700dbc794802e5afb9364a718bb7ec57bd6",
        FreeContinuousRacingTrajectoryGeometryExperiment.BaseMainSha);

    [Fact]
    public void ElevenPhysicalControlStationsAreRequired() => Assert.Equal(
        11, FreeContinuousRacingTrajectoryGeometryExperiment.ControlStationCount);

    [Fact]
    public void ProductionAdvancedReferenceRemainsExactlyNineteen() => Assert.Equal(
        19f,
        SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond);

    [Theory]
    [InlineData(24f)]
    [InlineData(31f)]
    [InlineData(45.6f)]
    public void ProductionRadiusOutputIsBitExactThroughDefaultReferenceWrapper(float radius)
    {
        var surface = CalibrationScenarioCatalog.Baseline.Surface;
        var production = SegmentPhysics.MaxSafeTurnSpeedForRadius(
            radius, surface, RiderSkills.Balanced, BikeSetup.Neutral);
        var explicitNineteen = SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
            radius, 19f, surface, RiderSkills.Balanced, BikeSetup.Neutral);
        Assert.Equal(BitConverter.SingleToInt32Bits(production),
            BitConverter.SingleToInt32Bits(explicitNineteen));
    }

    [Fact]
    public void SensitivityHelperAtNineteenIsBitExactWithProductionCapability()
    {
        var surface = CalibrationScenarioCatalog.Baseline.Surface;
        var production = SegmentPhysics.MaxSafeTurnSpeedForRadius(
            31f, surface, RiderSkills.Balanced, BikeSetup.Neutral);
        var sensitivity = SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
            31f,
            SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond,
            surface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        Assert.Equal(BitConverter.SingleToInt32Bits(production),
            BitConverter.SingleToInt32Bits(sensitivity));
    }

    [Fact]
    public void HypotheticalReferenceCapabilityIsStrictlyMonotonic()
    {
        var surface = CalibrationScenarioCatalog.Baseline.Surface;
        var values = FreeContinuousRacingTrajectoryGeometryExperiment
            .CornerSpeedSensitivityReferenceSweepMetersPerSecond
            .Select(reference => SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
                31f, reference, surface, RiderSkills.Balanced, BikeSetup.Neutral))
            .ToArray();
        Assert.True(values.Zip(values.Skip(1)).All(pair => pair.First < pair.Second));
    }

    [Fact]
    public void HypotheticalCapabilityDoesNotMutateProductionBehavior()
    {
        var surface = CalibrationScenarioCatalog.Baseline.Surface;
        var before = SegmentPhysics.MaxSafeTurnSpeedForRadius(
            31f, surface, RiderSkills.Balanced, BikeSetup.Neutral);
        _ = SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
            31f, 23f, surface, RiderSkills.Balanced, BikeSetup.Neutral);
        var after = SegmentPhysics.MaxSafeTurnSpeedForRadius(
            31f, surface, RiderSkills.Balanced, BikeSetup.Neutral);
        Assert.Equal(BitConverter.SingleToInt32Bits(before), BitConverter.SingleToInt32Bits(after));
        Assert.Equal(19f, SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond);
    }

    [Fact]
    public void SensitivitySweepPointsAreExact() => Assert.Equal(
        new[] { 19f, 20f, 21f, 22f, 23f },
        Result.Value.CornerSpeedSensitivity.ReferenceSweepMetersPerSecond);

    [Fact]
    public void ControlledSensitivityCasesAreFixedValidAndDeterministic()
    {
        var sensitivity = Result.Value.CornerSpeedSensitivity;
        Assert.Equal(10, sensitivity.Controls.Count);
        Assert.Equal(new[]
        {
            "ConstantInner", "ConstantL1", "ConstantL2", "ConstantL3", "ConstantOuter",
            "#46 E0-A1-X0", "#46 E0-A1-X1", "MildDynamic", "ModerateDynamic",
            "WideEntryEarlyTightRelease",
        }, sensitivity.Controls.Select(item => item.Name));
        Assert.All(sensitivity.Points.SelectMany(point => point.Observations), observation =>
        {
            Assert.True(observation.Evaluation.IsValid);
            Assert.Single(observation.Evaluation.TurningDemandConstraints);
            Assert.Equal(.5f, observation.Evaluation.TurningDemandConstraints[0].Progress);
            Assert.True(observation.Evaluation.TurningDemandConstraints[0].IsCanonicalBaseline);
        });
        var another = FreeContinuousRacingTrajectoryGeometryExperiment.Run().CornerSpeedSensitivity;
        Assert.Equal(
            sensitivity.Points.SelectMany(point => point.Observations)
                .Select(item => (item.Name, item.Evaluation.SectorTimeSeconds)),
            another.Points.SelectMany(point => point.Observations)
                .Select(item => (item.Name, item.Evaluation.SectorTimeSeconds)));
    }

    [Fact]
    public void SensitivityDiagnosticDoesNotUseFreeOptimizer()
    {
        var source = CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs");
        var start = source.IndexOf("private static CornerSpeedCapabilitySensitivityResult RunCornerSpeedSensitivity",
            StringComparison.Ordinal);
        var end = source.IndexOf("private static IReadOnlyList<CornerSpeedSensitivityControl>",
            start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        Assert.DoesNotContain("RunSearch", source[start..end], StringComparison.Ordinal);
        Assert.Contains("useCanonicalSensitivityConstraint: true", source[start..end], StringComparison.Ordinal);
    }

    [Fact]
    public void SensitivityReportIsDeterministic()
    {
        var first = FreeContinuousRacingTrajectoryGeometryReport.Render(Result.Value);
        var second = FreeContinuousRacingTrajectoryGeometryReport.Render(Result.Value);
        Assert.Equal(first, second);
        Assert.Contains("## AA. Corner-speed capability sensitivity diagnostic", first,
            StringComparison.Ordinal);
        Assert.Contains("## AB. Near-constant turning-demand continuity diagnostic", first,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SensitivityCrossoverAndGlobalGuardrailClassificationAreExplicit()
    {
        var sensitivity = Result.Value.CornerSpeedSensitivity;
        Assert.Equal(5, sensitivity.Points.Count);
        Assert.InRange(sensitivity.ApproximateCrossoverReferenceSpeedMetersPerSecond!.Value, 22f, 23f);
        Assert.True(sensitivity.GlobalSpeedGuardrailBreaksAtCrossover);
        Assert.NotNull(sensitivity.FirstGlobalSpeedGuardrailBreakReferenceMetersPerSecond);
        Assert.True(sensitivity.GlobalSpeedGuardrailBreaksBeforeCrossover);
        Assert.True(sensitivity.FirstGlobalSpeedGuardrailBreakReferenceMetersPerSecond
            < sensitivity.ApproximateCrossoverReferenceSpeedMetersPerSecond);
        var firstDelta = sensitivity.Points[0].BestControlledDynamic.Evaluation.SectorTimeSeconds
            - sensitivity.Points[0].BestConstant.Evaluation.SectorTimeSeconds;
        var lastDelta = sensitivity.Points[^1].BestControlledDynamic.Evaluation.SectorTimeSeconds
            - sensitivity.Points[^1].BestConstant.Evaluation.SectorTimeSeconds;
        Assert.True(lastDelta > firstDelta + .02f);
        Assert.Equal("S4", sensitivity.Classification);
        Assert.StartsWith("Global speed breaks first", sensitivity.Interpretation,
            StringComparison.Ordinal);
        Assert.True(sensitivity.MateriallyControlsLineEconomics);
        Assert.Equal("NO", sensitivity.RaisingCapabilityAloneLooksLikeCorrectFix);
        Assert.All(sensitivity.Points, point =>
        {
            Assert.True(point.ConstantInnerRepeatedLap.Converged);
            AssertFinitePositive(
                point.SettledCapabilityAt31MetersPerSecond,
                point.ConstantInnerRepeatedLap.FlyingLapTimeSeconds,
                point.ConstantInnerRepeatedLap.MaximumSpeedMetersPerSecond,
                point.ConstantInnerRepeatedLap.AverageSpeedMetersPerSecond,
                point.E0A1X1.Evaluation.SectorTimeSeconds,
                point.BestControlledDynamic.Evaluation.SectorTimeSeconds);
        });
    }

    [Fact]
    public void RepresentationDoesNotHaveAnApexInput()
    {
        var properties = typeof(FreeTrajectoryCandidate).GetProperties().Select(item => item.Name).ToArray();
        Assert.Equal(new[] { "SeedFamily", "ControlOffsetsMeters", "Id" }, properties);
        Assert.DoesNotContain(properties, name => name.Contains("Apex", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(3f)]
    [InlineData(4f)]
    public void ExplicitRadiusHelperIsBitExactWithProductionWrapper(float lateral)
    {
        var geometry = MatchedVenueProfiles.Motoarena2026.CreateGeometry();
        var surface = CalibrationScenarioCatalog.Baseline.Surface;
        var wrapper = SegmentPhysics.MaxSafeTurnSpeed(
            lateral, geometry, surface, RiderSkills.Balanced, BikeSetup.Neutral);
        var extracted = SegmentPhysics.MaxSafeTurnSpeedForRadius(
            LaneModel.TurnArcRadiusMeters(lateral, geometry),
            surface, RiderSkills.Balanced, BikeSetup.Neutral);
        Assert.Equal(BitConverter.SingleToInt32Bits(wrapper), BitConverter.SingleToInt32Bits(extracted));
    }

    [Fact]
    public void ExplicitRadiusHelperRejectsNonPositiveRadius() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        SegmentPhysics.MaxSafeTurnSpeedForRadius(
            0f, CalibrationScenarioCatalog.Baseline.Surface, RiderSkills.Balanced, BikeSetup.Neutral));

    [Theory]
    [InlineData(.90f, SegmentOutcome.Ok)]
    [InlineData(1.01f, SegmentOutcome.Ok)]
    [InlineData(1.05f, SegmentOutcome.Brake)]
    [InlineData(1.15f, SegmentOutcome.RunWide)]
    [InlineData(1.30f, SegmentOutcome.Crash)]
    public void ExplicitAdvancedTargetHelperIsBitExactWithProductionWrapper(
        float speedFactor,
        SegmentOutcome expectedOutcome)
    {
        var segment = new TrackSegment(47, SegmentType.TurnMiddle);
        var geometry = MatchedVenueProfiles.Motoarena2026.CreateGeometry();
        var surface = CalibrationScenarioCatalog.Baseline.Surface;
        const int lane = 1;
        const float lateral = 1f;
        var target = SegmentPhysics.MaxSafeTurnSpeed(
            lateral, geometry, surface, RiderSkills.Balanced, BikeSetup.Neutral);
        var speed = target * speedFactor;
        var production = SegmentPhysics.Apply(new SegmentPhysicsContext(
            segment, lane, speed, geometry, surface, RiderSkills.Balanced,
            Morale: .5f, Setup: BikeSetup.Neutral, DecisionRisk: .125f,
            LateralPosition: lateral));
        var explicitTarget = SegmentPhysics.ResolveAdvancedForExplicitTarget(
            lane, speed, target, surface, RiderSkills.Balanced, .5f, .125f);

        Assert.Equal(expectedOutcome, production.Outcome);
        Assert.Equal(production, explicitTarget);
        Assert.Equal(BitConverter.SingleToInt32Bits(production.Speed),
            BitConverter.SingleToInt32Bits(explicitTarget.Speed));
        Assert.Equal(production.ContinuousCorrectionTargetSpeedMetersPerSecond,
            explicitTarget.ContinuousCorrectionTargetSpeedMetersPerSecond);
    }

    [Fact]
    public void StraightCurvatureIsZero()
    {
        var curvature = FreeContinuousTrajectoryGeometry.CurvatureFromThreePoints(
            (0f, 0f), (1f, 0f), (2f, 0f));
        Assert.InRange(curvature, 0f, 1e-7f);
    }

    [Theory]
    [InlineData(31f)]
    [InlineData(40f)]
    public void IdealCircleRadiusIsRecovered(float radius)
    {
        const float angle = .01f;
        var curvature = FreeContinuousTrajectoryGeometry.CurvatureFromThreePoints(
            (radius * MathF.Cos(-angle), radius * MathF.Sin(-angle)),
            (radius, 0f),
            (radius * MathF.Cos(angle), radius * MathF.Sin(angle)));
        Assert.InRange(1f / curvature, radius - .03f, radius + .03f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ConstantPathsRecoverProductionRadius(int lane)
    {
        var geometry = MatchedVenueProfiles.Motoarena2026.CreateGeometry();
        var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            lane, SegmentType.TurnMiddle, geometry);
        var path = FreeContinuousTrajectoryGeometry.Build(Enumerable.Repeat(offset, 11).ToArray(), geometry);
        var expected = LaneModel.TurnArcRadiusMeters(lane, geometry);
        Assert.All(path.Samples.Where((_, index) => index % 128 == 0),
            point => Assert.InRange(point.LocalRadiusMeters, expected - .001f, expected + .001f));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ConstantPathLengthsRecoverSemicircle(int lane)
    {
        var geometry = MatchedVenueProfiles.Motoarena2026.CreateGeometry();
        var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            lane, SegmentType.TurnMiddle, geometry);
        var path = FreeContinuousTrajectoryGeometry.Build(Enumerable.Repeat(offset, 11).ToArray(), geometry);
        Assert.InRange(path.TotalLengthMeters,
            MathF.PI * LaneModel.TurnArcRadiusMeters(lane, geometry) - .003f,
            MathF.PI * LaneModel.TurnArcRadiusMeters(lane, geometry) + .003f);
    }

    [Fact]
    public void PathLengthIsFiniteAndPositive() => Assert.All(Result.Value.Search.TopTwenty,
        item => Assert.True(float.IsFinite(item.Path!.TotalLengthMeters) && item.Path.TotalLengthMeters > 0f));

    [Fact]
    public void CartesianResolutionStaysBelowHalfMeter() => Assert.All(Result.Value.Search.TopTwenty,
        item => Assert.InRange(item.Path!.MaximumSampleSpacingMeters, 0f, .5f));

    [Fact]
    public void DynamicPathUsesActualTwoDimensionalLength()
    {
        var dynamic = Result.Value.Search.TopTwenty.First(item => item.IsNonConstant);
        var entryCircle = MathF.PI * (Result.Value.Geometry.InnerRadiusMeters
            + dynamic.Profile[0].LateralOffsetMeters);
        Assert.True(MathF.Abs(dynamic.Path!.TotalLengthMeters - entryCircle) > .01f);
    }

    [Fact]
    public void OutOfRangeControlPointIsRejected()
    {
        var width = LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, Result.Value.Geometry);
        var controls = Enumerable.Repeat(width * .5f, 11).ToArray();
        controls[4] = width + .01f;
        var value = FreeContinuousRacingTrajectoryGeometryExperiment.Evaluate(controls);
        Assert.Equal(FreeTrajectoryValidity.TrackBoundary, value.Validity);
    }

    [Fact]
    public void InterpolatedBoundaryBreachIsRejectedWithoutClamping()
    {
        var width = LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, Result.Value.Geometry);
        var controls = new[] { 0f, 0f, 0f, width, width, 0f, 0f, 0f, 0f, 0f, 0f };
        var value = FreeContinuousRacingTrajectoryGeometryExperiment.Evaluate(controls);
        Assert.Equal(FreeTrajectoryValidity.TrackBoundary, value.Validity);
    }

    [Fact]
    public void BowTiePolylineIsDetectedAsSelfIntersecting() => Assert.True(
        FreeContinuousTrajectoryGeometry.HasSelfIntersection(new[]
        {
            (0f, 0f), (1f, 1f), (0f, 1f), (1f, 0f),
        }));

    [Fact]
    public void OrdinaryPolylineDoesNotSelfIntersect() => Assert.False(
        FreeContinuousTrajectoryGeometry.HasSelfIntersection(new[]
        {
            (0f, 0f), (1f, 0f), (2f, 1f), (3f, 1f),
        }));

    [Fact]
    public void NaturalSplineIsC2AcrossEveryInteriorKnot()
    {
        var spline = new NaturalCubicSpline(new[] { 2f, 3f, 4f, 3f, 2f, 2.5f, 3f, 3.5f, 3f, 2.5f, 2f });
        const float epsilon = 1e-5f;
        for (var knot = 1; knot < 10; knot++)
        {
            var progress = knot / 10f;
            var left = spline.Evaluate(progress - epsilon);
            var right = spline.Evaluate(progress + epsilon);
            Assert.InRange(MathF.Abs(left.Value - right.Value), 0f, .01f);
            Assert.InRange(MathF.Abs(left.FirstDerivative - right.FirstDerivative), 0f, .02f);
            Assert.InRange(MathF.Abs(left.SecondDerivative - right.SecondDerivative), 0f, .2f);
        }
    }

    [Fact]
    public void LocalSafeSpeedsAreFinitePositive() => Assert.All(
        Result.Value.Search.TopTwenty.SelectMany(item => item.Profile),
        point => Assert.True(float.IsFinite(point.LocalSafeSpeedMetersPerSecond)
            && point.LocalSafeSpeedMetersPerSecond > 0f));

    [Fact]
    public void DynamicLocalRadiusIsNotDerivedOnlyFromLateralPosition()
    {
        var dynamic = Result.Value.Search.TopTwenty.First(item => item.IsNonConstant);
        Assert.Contains(dynamic.Profile, point => MathF.Abs(point.LocalRadiusMeters
            - LaneModel.TurnArcRadiusMeters(point.LateralPosition, Result.Value.Geometry)) > .10f);
    }

    [Fact]
    public void ProductionStraightProfileIsReusedExactly()
    {
        var best = Result.Value.BestFound;
        var expected = LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            best.ExitSpeedMetersPerSecond, RiderSkills.Balanced, BikeSetup.Neutral,
            CalibrationScenarioCatalog.Baseline.Surface,
            LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
                RiderSkills.Balanced, CalibrationScenarioCatalog.Baseline.Surface),
            best.StraightRepositionDistanceMeters);
        Assert.Equal(expected.TravelTimeSeconds, best.FollowingStraightTimeSeconds);
        Assert.Equal(expected.ExitSpeedMetersPerSecond, best.EndStraightSpeedMetersPerSecond);
    }

    [Fact]
    public void ProductionCorrectionCapabilityIsReused()
    {
        Assert.True(Result.Value.ConstantInner.CorrectionDistanceMeters > 0f);
        Assert.Contains("CalculateCornerCorrectionDecelerationMetersPerSecondSquared",
            CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionDriveAvailabilityIsReused() => Assert.Contains(
        "ContinuousCornerEnvelope.DriveAvailability",
        CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs"),
        StringComparison.Ordinal);

    [Fact]
    public void ProductionMidpointDriveIsReused() => Assert.Contains(
        "CalculateMidpointDriveEndSpeedMetersPerSecond",
        CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs"),
        StringComparison.Ordinal);

    [Fact]
    public void NoArtificialDynamicLineBonusExists()
    {
        var source = CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs");
        Assert.DoesNotContain("DynamicLineBonus", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LanePreference", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NoTurningOrSlipPenaltyWasIntroduced()
    {
        var source = CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs");
        Assert.DoesNotContain("SlipPenalty", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TurningPenalty", source, StringComparison.Ordinal);
        Assert.DoesNotContain("YawPenalty", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AllFiveConstantReplaysPassStrictGate()
    {
        Assert.True(Result.Value.ConstantReplayValidationPassed);
        Assert.Equal(5, Result.Value.ConstantReplay.Count);
        Assert.All(Result.Value.ConstantReplay, item => Assert.True(item.Passed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ConstantReplayTimeAndSpeedDeltasStayBelowBlocker(int lane)
    {
        var replay = Result.Value.ConstantReplay.Single(item => item.Lane == lane);
        Assert.InRange(MathF.Abs(replay.SectorDeltaSeconds), 0f, .005f);
        Assert.InRange(MathF.Abs(replay.ExitSpeedDeltaMetersPerSecond), 0f, .005f);
        Assert.InRange(replay.MaximumCornerSpeedProfileDeltaMetersPerSecond, 0f, .005f);
    }

    [Fact]
    public void VariableReplayNeverBuildsFreshCanonicalEnvelopeFromLocalSafeSpeed()
    {
        var source = CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs");
        Assert.DoesNotContain("ContinuousCornerEnvelope.Create", source, StringComparison.Ordinal);
        Assert.Contains("BuildTurningDemandConstraints", source, StringComparison.Ordinal);
        Assert.Contains("LookaheadTarget", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstantCurvatureUsesCanonicalProductionTurningPoint()
    {
        var constraints = FreeContinuousRacingTrajectoryGeometryExperiment.ExtractTurningDemandConstraints(
            Enumerable.Repeat(0f, 11).ToArray());
        var only = Assert.Single(constraints);
        Assert.Equal(.5f, only.Progress);
        Assert.True(only.IsCanonicalBaseline);
        Assert.True(only.IsBindingEnvelopePoint);
        Assert.True(only.BackwardReachableEntryTargetMetersPerSecond > 0f);
    }

    [Fact]
    public void ConstantPlateauRepresentativeIsDeterministicArcMidpoint()
    {
        var controls = Enumerable.Repeat(2f, 11).ToArray();
        var first = FreeContinuousRacingTrajectoryGeometryExperiment.ExtractTurningDemandConstraints(controls);
        var second = FreeContinuousRacingTrajectoryGeometryExperiment.ExtractTurningDemandConstraints(controls);
        Assert.Equal(first, second);
        Assert.Equal(.5f, Assert.Single(first).Progress);
    }

    [Fact]
    public void ConstraintSetIsOrderedDeterministicAndContainsOnlyBindingSamplesPlusCanonicalAnchor()
    {
        var controls = new[] { .65f, .15f, .15f, .65f, .15f, .15f, .65f, .40f, .15f, .15f, .40f };
        var first = FreeContinuousRacingTrajectoryGeometryExperiment.ExtractTurningDemandConstraints(controls);
        var second = FreeContinuousRacingTrajectoryGeometryExperiment.ExtractTurningDemandConstraints(controls);
        Assert.Equal(first, second);
        Assert.True(first.Zip(first.Skip(1)).All(pair => pair.First.Progress < pair.Second.Progress));

        Assert.Contains(first, item => item.IsCanonicalBaseline && item.Progress == .5f);
        Assert.All(first.Where(item => !item.IsCanonicalBaseline),
            item => Assert.True(item.IsBindingEnvelopePoint));
        Assert.All(first.Where(item => item.IsBindingEnvelopePoint),
            item => Assert.True(item.BackwardReachableEntryTargetMetersPerSecond > 0f));
    }

    [Fact]
    public void ReducedSuffixEnvelopeMatchesBruteForceForSharpBroadAndShallowShapes()
    {
        var shapes = new Dictionary<string, float[]>
        {
            ["sharp early"] = new[] { 1f, 1f, 2.75f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f },
            ["broad early"] = new[] { 1f, 1.5f, 2f, 2.25f, 2f, 1.5f, 1f, 1f, 1f, 1f, 1f },
            ["sharp late"] = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 2.75f, 1f, 1f },
            ["broad late"] = new[] { 1f, 1f, 1f, 1f, 1f, 1.5f, 2f, 2.25f, 2f, 1.5f, 1f },
            ["unequal separated peaks"] = new[] { 1f, 2.25f, 1f, 1f, 1f, 1f, 1f, 2.75f, 1f, 1f, 1f },
            ["shallow near constant"] = new[]
                { 1f, 1.0001f, 1.0002f, 1.0001f, 1f, .9999f, .9998f, .9999f, 1f, 1.0001f, 1f },
        };
        var progress = new[] { 0f, .071f, .2f, .37f, .49f, .5f, .61f, .75f, .93f, 1f };

        foreach (var shape in shapes)
        {
            var comparisons = FreeContinuousRacingTrajectoryGeometryExperiment
                .CompareTurningDemandEnvelope(shape.Value, progress);
            Assert.Equal(progress, comparisons.Select(item => item.Progress));
            Assert.All(comparisons, item => Assert.InRange(
                MathF.Abs(item.ReducedLookaheadTargetMetersPerSecond
                    - item.BruteForceLookaheadTargetMetersPerSecond),
                0f,
                1e-5f));
        }
    }

    [Fact]
    public void ReviewedHeadBroadLateWinnerHasPostApexBindingDemand()
    {
        var value = Result.Value.PriorReviewedBroadLateWinnerAfterEnvelopeRepair;
        Assert.Equal("FCT-49FCA338392C", value.Candidate.Id);
        var canonical = Assert.Single(value.TurningDemandConstraints
            .Where(item => item.IsCanonicalBaseline));
        var broadLateBindings = value.TurningDemandConstraints.Where(item =>
            item.IsBindingEnvelopePoint
            && !item.IsCanonicalBaseline
            && item.Progress is >= .55f and <= .80f
            && item.LocalRadiusMeters < canonical.LocalRadiusMeters - 1f).ToArray();

        Assert.NotEmpty(broadLateBindings);
        Assert.All(broadLateBindings, item =>
        {
            Assert.True(item.BackwardReachableEntryTargetMetersPerSecond > 0f);
            Assert.True(item.SettledCapabilitySquaredDeltaMetersSquaredPerSecondSquared < 0f);
        });

        var lookahead = FreeContinuousRacingTrajectoryGeometryExperiment.CompareTurningDemandEnvelope(
            value.Candidate.ControlOffsetsMeters,
            new[] { .55f, .60f, .65f, .70f, .75f });
        Assert.All(lookahead, item =>
        {
            Assert.True(float.IsFinite(item.ReducedLookaheadTargetMetersPerSecond));
            Assert.True(item.ReducedLookaheadTargetMetersPerSecond > 0f);
        });
    }

    [Fact]
    public void NearConstantFamilyRetainsCanonicalBaselineAcrossOldTolerance()
    {
        var diagnostic = Result.Value.TurningDemandContinuity;
        Assert.Equal(new[]
        {
            "Constant", "Approach-0.001x", "Approach-0.01x", "Approach-0.10x",
            "HalfTolerance", "NineTenthsTolerance", "JustBelowTolerance",
            "JustAboveTolerance", "ElevenTenthsTolerance", "TwiceTolerance",
        }, diagnostic.Observations.Select(item => item.Name));
        Assert.True(diagnostic.CanonicalBaselineAlwaysPresent);
        Assert.All(diagnostic.Observations, observation =>
        {
            Assert.True(observation.Evaluation.IsValid);
            var canonical = Assert.Single(observation.Evaluation.TurningDemandConstraints
                .Where(item => item.IsCanonicalBaseline));
            Assert.Equal(.5f, canonical.Progress);
        });
        Assert.All(diagnostic.Observations.Skip(1), observation =>
            Assert.Contains(observation.Evaluation.TurningDemandConstraints,
                item => !item.IsCanonicalBaseline));
        Assert.True(diagnostic.Observations.Single(item => item.Name == "JustBelowTolerance")
            .ActualCurvatureRangePerMeter < diagnostic.OldTolerancePerMeter);
        Assert.True(diagnostic.Observations.Single(item => item.Name == "JustAboveTolerance")
            .ActualCurvatureRangePerMeter > diagnostic.OldTolerancePerMeter);
        Assert.All(Result.Value.SanityControls, control =>
            Assert.Contains(control.Evaluation.TurningDemandConstraints,
                item => item.IsCanonicalBaseline && item.Progress == .5f));
        Assert.All(Result.Value.Search.TopTwenty.Where(item => item.IsNonConstant), evaluation =>
            Assert.Contains(evaluation.TurningDemandConstraints,
                item => item.IsCanonicalBaseline && item.Progress == .5f));
    }

    [Fact]
    public void SupplementalTurningDemandConvergesAndHasNoOldThresholdJump()
    {
        var diagnostic = Result.Value.TurningDemandContinuity;
        Assert.True(diagnostic.NearConstantPenaltyGatePassed,
            $"max sector delta / twice restriction: {diagnostic.MaximumNearConstantSectorDeltaFromConstantSeconds:R} / {diagnostic.TwiceToleranceSupplementalRestrictionMetersPerSecond:R}");
        Assert.True(diagnostic.DifferentialRestrictionConverges);
        Assert.True(diagnostic.ConvergesToConstant,
            $"smallest sector/profile deltas: {diagnostic.SmallestPerturbationSectorDeltaFromConstantSeconds:R} / {diagnostic.SmallestPerturbationMaximumSpeedProfileDeltaFromConstantMetersPerSecond:R}");
        Assert.True(diagnostic.OldThresholdContinuityPassed,
            $"below/above sector/profile deltas: {diagnostic.BelowAboveSectorDeltaSeconds:R} / {diagnostic.BelowAboveMaximumSpeedProfileDeltaMetersPerSecond:R}");
        Assert.InRange(diagnostic.BelowAboveSectorDeltaSeconds, 0f, .005f);
        Assert.InRange(diagnostic.BelowAboveMaximumSpeedProfileDeltaMetersPerSecond, 0f, .005f);
        Assert.InRange(diagnostic.SmallestPerturbationSectorDeltaFromConstantSeconds, 0f, .005f);
        Assert.InRange(diagnostic.SmallestPerturbationCornerDeltaFromConstantSeconds, 0f, .005f);
        Assert.InRange(
            diagnostic.SmallestPerturbationExitSpeedDeltaFromConstantMetersPerSecond,
            0f,
            .005f);
        Assert.InRange(
            diagnostic.SmallestPerturbationMaximumSpeedProfileDeltaFromConstantMetersPerSecond,
            0f,
            .005f);
        Assert.InRange(diagnostic.MaximumNearConstantSectorDeltaFromConstantSeconds, 0f, .005f);
        Assert.InRange(diagnostic.TwiceToleranceSupplementalRestrictionMetersPerSecond, 0.0000001f, .01f);

        var ordered = diagnostic.Observations.Take(4).Reverse().ToArray();
        var constant = diagnostic.Observations[0].Evaluation;
        var deltas = ordered.Select(item => MathF.Abs(
            item.Evaluation.SectorTimeSeconds - constant.SectorTimeSeconds)).ToArray();
        Assert.True(deltas.Zip(deltas.Skip(1)).All(pair => pair.Second <= pair.First + 1e-5f));
    }

    [Fact]
    public void EqualOrEasierCurvaturePostApexKeepsCanonicalReplayBaseline()
    {
        const float canonicalBaseline = 23.40593f;
        const float canonicalSettled = 21.649185f;
        var equal = FreeContinuousRacingTrajectoryGeometryExperiment
            .DifferentialSupplementalCapabilityMetersPerSecond(
                canonicalBaseline,
                canonicalSettled,
                canonicalSettled);
        var easier = FreeContinuousRacingTrajectoryGeometryExperiment
            .DifferentialSupplementalCapabilityMetersPerSecond(
                canonicalBaseline,
                canonicalSettled,
                canonicalSettled + .5f);

        Assert.Equal(canonicalBaseline, equal);
        Assert.Equal(canonicalBaseline, easier);
        Assert.NotEqual(canonicalSettled, equal);
    }

    [Fact]
    public void TinyDifferentialLatePeakAppliesSmallNonzeroRestrictionWithoutBaselineCollapse()
    {
        var twice = Result.Value.TurningDemandContinuity.Observations
            .Single(item => item.Name == "TwiceTolerance").Evaluation;
        var supplemental = twice.TurningDemandConstraints
            .Where(item => !item.IsCanonicalBaseline)
            .OrderByDescending(item => item.CanonicalBaselineSpeedMetersPerSecond
                - item.SettledCapabilityMetersPerSecond)
            .First();
        var restriction = supplemental.CanonicalBaselineSpeedMetersPerSecond
            - supplemental.SettledCapabilityMetersPerSecond;

        Assert.True(supplemental.SettledCapabilitySquaredDeltaMetersSquaredPerSecondSquared < 0f);
        Assert.InRange(restriction, 0.0000001f, .01f);
        Assert.True(supplemental.SettledCapabilityMetersPerSecond
            > supplemental.LocalSettledCapabilityMetersPerSecond + 1f);
    }

    [Fact]
    public void MaterialCurvaturePeakStillMeaningfullyRestrictsCanonicalBaseline()
    {
        var value = Result.Value.SanityControls
            .Single(item => item.Name == "EarlyTightThenOpen").Evaluation;
        var peak = value.TurningDemandConstraints
            .Where(item => !item.IsCanonicalBaseline && item.Progress < .5f)
            .OrderByDescending(item => item.CanonicalBaselineSpeedMetersPerSecond
                - item.SettledCapabilityMetersPerSecond)
            .First();
        var restriction = peak.CanonicalBaselineSpeedMetersPerSecond
            - peak.SettledCapabilityMetersPerSecond;

        Assert.True(peak.SettledCapabilitySquaredDeltaMetersSquaredPerSecondSquared < 0f);
        Assert.True(restriction > .25f);
        Assert.True(peak.SettledCapabilityMetersPerSecond
            > peak.LocalSettledCapabilityMetersPerSecond + 1f);
    }

    [Fact]
    public void VariableCurvatureSanityFixturesPassTheirSemanticGates()
    {
        Assert.True(Result.Value.VariableCurvatureEnvelopeConsistent);
        Assert.Equal(new[] { "SmoothSinglePeak", "SmoothTwoPeak", "EarlyTightThenOpen", "LateTight" },
            Result.Value.SanityControls.Select(item => item.Name));
        Assert.All(Result.Value.SanityControls, item =>
        {
            Assert.True(item.SemanticsPassed);
            Assert.True(item.Evaluation.IsValid);
            Assert.DoesNotContain(item.Evaluation.TurningConstraintTrace,
                trace => trace.Outcome is SegmentOutcome.RunWide or SegmentOutcome.Crash);
        });
        Assert.Contains(Result.Value.SanityControls.Single(item => item.Name == "SmoothSinglePeak")
            .Evaluation.TurningDemandConstraints,
            item => !item.IsCanonicalBaseline && item.IsBindingEnvelopePoint);
    }

    [Fact]
    public void EarlyTightBeginsCorrectionBeforeItsTurningConstraint()
    {
        var value = Result.Value.SanityControls.Single(item => item.Name == "EarlyTightThenOpen").Evaluation;
        var early = value.TurningDemandConstraints
            .Where(item => !item.IsCanonicalBaseline && item.Progress is > 0f and < .5f)
            .OrderByDescending(item => item.CanonicalBaselineSpeedMetersPerSecond
                - item.SettledCapabilityMetersPerSecond)
            .First();
        Assert.True(value.FirstCorrectionProgress < early.Progress);
        Assert.True(early.Progress < .5f);
    }

    [Fact]
    public void LateTightRemainsConstrainedBeforeItsLateTurningPoint()
    {
        var value = Result.Value.SanityControls.Single(item => item.Name == "LateTight").Evaluation;
        var late = value.TurningDemandConstraints
            .Where(item => !item.IsCanonicalBaseline && item.Progress > .9f)
            .OrderByDescending(item => item.CanonicalBaselineSpeedMetersPerSecond
                - item.SettledCapabilityMetersPerSecond)
            .First();
        Assert.True(late.Progress > .9f);
        Assert.True(value.FirstCorrectionProgress < late.Progress);
        Assert.Contains(value.Profile,
            item => item.Progress < late.Progress && item.CorrectionActive);
    }

    [Fact]
    public void SmoothTwoPeakRespectsBothFutureConstraints()
    {
        var value = Result.Value.SanityControls.Single(item => item.Name == "SmoothTwoPeak").Evaluation;
        Assert.Contains(value.TurningDemandConstraints,
            item => !item.IsCanonicalBaseline && item.IsBindingEnvelopePoint && item.Progress < .5f);
        Assert.Contains(value.TurningDemandConstraints,
            item => !item.IsCanonicalBaseline && item.IsBindingEnvelopePoint && item.Progress > .5f);
        Assert.Equal(value.TurningDemandConstraints.Count, value.TurningConstraintTrace.Count);
        Assert.All(value.TurningConstraintTrace, item =>
            Assert.Contains(item.Outcome, new[] { SegmentOutcome.Ok, SegmentOutcome.Brake }));
    }

    [Fact]
    public void OldWinnerIsReevaluatedDeterministicallyAndRejectedByProductionSemantics()
    {
        var old = Result.Value.OldWinnerAfterRepair;
        Assert.Equal("FCT-CEAACA7835FE", old.Candidate.Id);
        Assert.False(old.IsValid);
        Assert.Equal(FreeTrajectoryValidity.CornerControlPathDeparture, old.Validity);
        Assert.Equal("CornerControlPathDeparture", old.InvalidReason);
        Assert.Contains(old.TurningDemandConstraints,
            item => item.IsCanonicalBaseline && item.Progress == .5f);
        Assert.All(old.TurningDemandConstraints.Where(item => !item.IsCanonicalBaseline),
            item => Assert.True(item.IsBindingEnvelopePoint));

        var replay = FreeContinuousRacingTrajectoryGeometryExperiment.Evaluate(
            old.Candidate.ControlOffsetsMeters, seedFamily: old.Candidate.SeedFamily);
        Assert.Equal(old.Candidate.Id, replay.Candidate.Id);
        Assert.Equal(old.Validity, replay.Validity);
        Assert.Equal(old.InvalidReason, replay.InvalidReason);
        Assert.Equal(old.PeakSettledOverspeedRatio, replay.PeakSettledOverspeedRatio);
        Assert.Equal(old.TurningDemandConstraints, replay.TurningDemandConstraints);
    }

    [Fact]
    public void ProductionCornerControlFailuresStillInvalidatePlannedPaths()
    {
        Assert.Equal(FreeTrajectoryValidity.CornerControlPathDeparture,
            Result.Value.OldWinnerAfterRepair.Validity);
        Assert.Equal(1, Result.Value.OldWinnerAfterRepair.RunWideInvalidationCount);
        Assert.True(Result.Value.Search.InvalidCornerControlDepartureCandidates > 0);
        Assert.True(Result.Value.Search.InvalidCrashCandidates > 0);
    }

    [Fact]
    public void AnalysisCopiesNoAdvancedOutcomeThresholds()
    {
        var source = CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs");
        Assert.DoesNotContain("MinAdvancedBrakeSpeedFactor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxAdvancedBrakeSpeedFactor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MinAdvancedRunWideSpeedFactor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxAdvancedRunWideSpeedFactor", source, StringComparison.Ordinal);
        Assert.Contains("ResolveAdvancedForExplicitTarget", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchBudgetIsBoundedAndInTargetRange() => Assert.InRange(
        Result.Value.Search.CandidatesEvaluated, 8_000, 20_000);

    [Fact]
    public void AllSeedFamiliesWereExecuted()
    {
        Assert.Equal(10, Result.Value.Search.StructuredStartCount);
        Assert.Equal(96, Result.Value.Search.LowDiscrepancyStartCount);
        Assert.Equal(106, Result.Value.Search.StartsGenerated);
        Assert.True(Result.Value.Search.ValidInitialStarts >= 12);
        Assert.Equal(48, Result.Value.Search.RefinedStartCount);
        Assert.Equal(6, Result.Value.Search.RefinementRounds);
        Assert.Contains(Result.Value.Search.FamilyResults, item => item.Family == "Flat-0");
        var source = CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs");
        Assert.Contains("Linear-inward", source, StringComparison.Ordinal);
        Assert.Contains("Linear-outward", source, StringComparison.Ordinal);
        Assert.Contains("Halton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Random", source, StringComparison.Ordinal);
        Assert.Equal(new[] { 2f, 1f, .5f, .25f, .125f, .0625f },
            Result.Value.Search.FinalRefinementStepsMeters);
    }

    [Fact]
    public void SearchProducesTwentyUniqueValidTrajectories()
    {
        Assert.Equal(20, Result.Value.Search.TopTwenty.Count);
        Assert.All(Result.Value.Search.TopTwenty, item => Assert.True(item.IsValid));
        Assert.Equal(20, Result.Value.Search.TopTwenty.Select(item => item.PathFingerprint).Distinct().Count());
        Assert.Equal(20, Result.Value.Search.TopTwenty.Select(item => item.Candidate.Id).Distinct().Count());
    }

    [Fact]
    public void RefinedStartsContainAtLeastTwelveGeometryDiversePaths()
    {
        var representatives = new List<IReadOnlyList<float>>();
        foreach (var item in Result.Value.Search.FamilyResults)
        {
            var controls = item.Best.Candidate.ControlOffsetsMeters;
            if (representatives.All(existing => RootMeanSquareDifference(existing, controls) > .25f))
                representatives.Add(controls);
        }
        Assert.True(representatives.Count >= 12, $"Only {representatives.Count} diverse refined paths.");
    }

    [Fact]
    public void TopRankingIsCanonical() => Assert.True(Result.Value.Search.TopTwenty
        .Zip(Result.Value.Search.TopTwenty.Skip(1))
        .All(pair => pair.First.SectorTimeSeconds <= pair.Second.SectorTimeSeconds));

    [Fact]
    public void RepeatedRunFindsIdenticalRanking()
    {
        var another = FreeContinuousRacingTrajectoryGeometryExperiment.Run();
        Assert.Equal(Result.Value.BestFound.Candidate.Id, another.BestFound.Candidate.Id);
        Assert.Equal(Result.Value.Search.TopTwenty.Select(item => item.Candidate.Id),
            another.Search.TopTwenty.Select(item => item.Candidate.Id));
        Assert.Equal(Result.Value.Search.TopTwenty.Select(item => item.SectorTimeSeconds),
            another.Search.TopTwenty.Select(item => item.SectorTimeSeconds));
    }

    [Fact]
    public void CandidateIdsAreCultureInvariant()
    {
        var controls = Enumerable.Range(0, 11).Select(index => index * .125f).ToArray();
        Assert.Equal(UnderCulture("en-US", () => FreeContinuousRacingTrajectoryGeometryExperiment.CandidateId(controls)),
            UnderCulture("pl-PL", () => FreeContinuousRacingTrajectoryGeometryExperiment.CandidateId(controls)));
    }

    [Fact]
    public void LateralExecutionConstraintRejectsImpossibleSymmetricCrossing()
    {
        var width = LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, Result.Value.Geometry);
        var value = FreeContinuousRacingTrajectoryGeometryExperiment.Evaluate(
            Enumerable.Range(0, 11).Select(index =>
                width * (.25f + .50f * MathF.Abs(2f * index / 10f - 1f))).ToArray());
        Assert.Equal(FreeTrajectoryValidity.LateralExecutionConstraint, value.Validity);
    }

    [Fact]
    public void FeasibleConstantPathIsAccepted() => Assert.True(Result.Value.ConstantInner.IsValid);

    [Fact]
    public void NoLateralTeleportIsNeededByAcceptedPaths() => Assert.All(
        Result.Value.Search.TopTwenty,
        item => Assert.True(item.MinimumLateralExecutionHeadroomMeters >= -1e-4f));

    [Fact]
    public void ImpossiblePeriodicStraightRepositionIsRejected()
    {
        var width = LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, Result.Value.Geometry);
        var controls = Enumerable.Range(0, 11)
            .Select(index => width * (.75f - .50f * index / 10f)).ToArray();
        var value = FreeContinuousRacingTrajectoryGeometryExperiment.Evaluate(controls);
        Assert.Equal(FreeTrajectoryValidity.StraightRepositionConstraint, value.Validity);
        Assert.True(value.StraightRepositionLateralHeadroomMeters < 0f);
    }

    [Fact]
    public void ConstantPathClosesWithExactGeometricStraightLength()
    {
        var value = Result.Value.ConstantInner;
        Assert.True(value.IsValid);
        Assert.Equal(value.Profile[0].LateralOffsetMeters, value.Profile[^1].LateralOffsetMeters);
        Assert.Equal(Result.Value.Geometry.StraightLengthMeters, value.StraightRepositionDistanceMeters);
        Assert.Equal(value.Profile[0].LateralOffsetMeters, value.NextEntryLateralMeters);
    }

    [Fact]
    public void NonConstantPeriodicStraightUsesDiagonalDistance()
    {
        var value = Result.Value.BestFound;
        var lateral = value.NextEntryLateralMeters - value.Profile[^1].LateralOffsetMeters;
        var expected = MathF.Sqrt(Result.Value.Geometry.StraightLengthMeters
            * Result.Value.Geometry.StraightLengthMeters + lateral * lateral);
        Assert.InRange(MathF.Abs(expected - value.StraightRepositionDistanceMeters), 0f, 1e-5f);
        Assert.True(value.StraightRepositionDistanceMeters >= Result.Value.Geometry.StraightLengthMeters);
        Assert.True(value.StraightRepositionLateralHeadroomMeters >= -1e-4f);
    }

    [Fact]
    public void FalseEntryApproachGuardrailIsRemovedAndPeriodicClosureIsPresent()
    {
        var source = CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs");
        Assert.DoesNotContain("ApproachWindowMeters", source, StringComparison.Ordinal);
        Assert.Contains("StraightRepositionConstraint", source, StringComparison.Ordinal);
        Assert.Contains("SegmentType.Straight", source, StringComparison.Ordinal);
        Assert.Contains("LateralMovementModel.CalculateMaxLateralDistanceMeters", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchConvergenceDimensionsAreComputedIndependently()
    {
        var search = Result.Value.Search;
        Assert.True(search.ObjectiveConvergence);
        Assert.True(search.TopThreeStable);
        Assert.True(search.GeometryConvergence);
        Assert.Equal(search.ObjectiveConvergence && !search.GeometryConvergence,
            search.MultipleNearOptimalBasins);
        Assert.Equal(!search.ObjectiveConvergence, search.SearchConvergenceUncertain);
        var source = CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs");
        Assert.Contains("objectiveConvergence && !geometryConvergence", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalPerturbationGatesWereExecuted()
    {
        Assert.True(Result.Value.Search.LocalPerturbationImprovementSeconds >= 0f);
        Assert.True(Result.Value.Search.PairPerturbationImprovementSeconds >= 0f);
        Assert.True(Result.Value.Search.InitialLocalPerturbationImprovementSeconds >= 0f);
        Assert.True(Result.Value.Search.InitialPairPerturbationImprovementSeconds >= 0f);
        Assert.True(Result.Value.Search.AdditionalAcceptedLocalMoves >= 0);
        Assert.Equal(Result.Value.Search.LocalPerturbationImprovementSeconds <=
                     FreeContinuousRacingTrajectoryGeometryExperiment.LocalSearchImprovementToleranceSeconds
                     && Result.Value.Search.PairPerturbationImprovementSeconds <=
                     FreeContinuousRacingTrajectoryGeometryExperiment.LocalSearchImprovementToleranceSeconds,
            Result.Value.Search.LocalSearchConverged);
    }

    [Fact]
    public void MaterialFinalScheduledRoundReceivesAnIndependentClosurePass()
    {
        var tolerance = FreeContinuousRacingTrajectoryGeometryExperiment
            .LocalSearchImprovementToleranceSeconds;
        var material = Result.Value.Search.TopThreeRefinementDiagnostics
            .Where(item => item.LastRoundImprovementSeconds > tolerance).ToArray();
        Assert.NotEmpty(material);
        Assert.All(material, item =>
        {
            Assert.Equal(6, item.RefinementStepImprovementsSeconds.Count);
            Assert.Equal(item.RefinementStepImprovementsSeconds[^1], item.LastRoundImprovementSeconds);
            Assert.True(item.ChangedInLastRound);
            Assert.True(item.ClosurePasses >= 1);
            Assert.True(item.Stable);
        });
    }

    [Fact]
    public void TopThreeReportedStabilityMatchesIndependentlyEvaluatedFinalNeighborhoods()
    {
        var search = Result.Value.Search;
        var diagnostics = search.TopThreeRefinementDiagnostics;
        var tolerance = FreeContinuousRacingTrajectoryGeometryExperiment
            .LocalSearchImprovementToleranceSeconds;
        Assert.Equal(3, diagnostics.Count);
        Assert.Equal(3, diagnostics.Select(item => item.Family).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(search.TopThreeStable, diagnostics.All(item => item.Stable));
        foreach (var item in diagnostics)
        {
            Assert.Equal(search.FamilyResults.Single(family => family.Family == item.Family)
                .Best.Candidate.Id, item.Final.Candidate.Id);
            var origin = item.Final;
            var entrySpeed = Result.Value.EntrySpeedMetersPerSecond;
            var singleAtFinalStep = BestSingleResidual(origin, entrySpeed, .0625f);
            var expandedSingle = BestSingleResidual(origin, entrySpeed, .125f, .0625f);
            var pair = BestAdjacentPairResidual(origin, entrySpeed);
            Assert.InRange(MathF.Abs(singleAtFinalStep - item.ResidualSingleCoordinateImprovementSeconds),
                0f, 1e-5f);
            Assert.InRange(MathF.Abs(expandedSingle
                - item.ResidualExpandedSingleCoordinateImprovementSeconds), 0f, 1e-5f);
            Assert.InRange(MathF.Abs(pair - item.ResidualAdjacentPairImprovementSeconds), 0f, 1e-5f);
            Assert.True(singleAtFinalStep <= tolerance);
            Assert.True(expandedSingle <= tolerance);
            Assert.True(pair <= tolerance);
            Assert.Equal(item.ClosurePasses > 0 && expandedSingle <= tolerance && pair <= tolerance,
                item.Stable);
        }
    }

    private static float BestSingleResidual(
        FreeTrajectoryEvaluation origin, float entrySpeedMetersPerSecond, params float[] stepsMeters)
    {
        var best = origin.SectorTimeSeconds;
        foreach (var step in stepsMeters)
        for (var station = 0; station < FreeContinuousRacingTrajectoryGeometryExperiment.ControlStationCount;
             station++)
        foreach (var direction in new[] { -1f, 1f })
        {
            var controls = origin.Candidate.ControlOffsetsMeters.ToArray();
            controls[station] += direction * step;
            var trial = FreeContinuousRacingTrajectoryGeometryExperiment.Evaluate(
                controls, entrySpeedMetersPerSecond);
            if (trial.IsValid) best = MathF.Min(best, trial.SectorTimeSeconds);
        }
        return MathF.Max(0f, origin.SectorTimeSeconds - best);
    }

    private static float BestAdjacentPairResidual(
        FreeTrajectoryEvaluation origin, float entrySpeedMetersPerSecond)
    {
        var best = origin.SectorTimeSeconds;
        for (var station = 0; station
             < FreeContinuousRacingTrajectoryGeometryExperiment.ControlStationCount - 1; station++)
        foreach (var firstDirection in new[] { -1f, 1f })
        foreach (var secondDirection in new[] { -1f, 1f })
        {
            var controls = origin.Candidate.ControlOffsetsMeters.ToArray();
            controls[station] += firstDirection * .0625f;
            controls[station + 1] += secondDirection * .0625f;
            var trial = FreeContinuousRacingTrajectoryGeometryExperiment.Evaluate(
                controls, entrySpeedMetersPerSecond);
            if (trial.IsValid) best = MathF.Min(best, trial.SectorTimeSeconds);
        }
        return MathF.Max(0f, origin.SectorTimeSeconds - best);
    }

    [Fact]
    public void ConstantInnerProductionBaselineReproduces46() => Assert.InRange(
        Result.Value.ConstantInnerProductionFlyingLapSeconds, 13.397697d, 13.397717d);

    [Fact]
    public void E0A1X0ProductionControlReproduces46() => Assert.InRange(
        Result.Value.E0A1X0ProductionFlyingLapSeconds, 13.507455d, 13.507475d);

    [Fact]
    public void E0A1X1ProductionControlReproduces46() => Assert.InRange(
        Result.Value.E0A1X1ProductionFlyingLapSeconds, 13.581979d, 13.581999d);

    [Fact]
    public void EveryCandidateStartsAtSameObservedEntrySpeed() => Assert.All(
        Result.Value.Search.TopTwenty,
        item => Assert.Equal(Result.Value.EntrySpeedMetersPerSecond, item.Profile[0].SpeedMetersPerSecond));

    [Fact]
    public void EveryTopTimeIsFinitePositive() => Assert.All(Result.Value.Search.TopTwenty, item =>
    {
        AssertFinitePositive(item.CornerTimeSeconds, item.FollowingStraightTimeSeconds, item.SectorTimeSeconds);
        AssertFinitePositive(item.ExitSpeedMetersPerSecond, item.EndStraightSpeedMetersPerSecond);
    });

    [Fact]
    public void EveryTopGeometryValueIsFinite() => Assert.All(
        Result.Value.Search.TopTwenty.SelectMany(item => item.Profile), point =>
        {
            Assert.True(float.IsFinite(point.LateralOffsetMeters));
            Assert.True(float.IsFinite(point.CurvaturePerMeter));
            AssertFinitePositive(point.LocalRadiusMeters, point.LocalSafeSpeedMetersPerSecond,
                point.SpeedMetersPerSecond);
        });

    [Fact]
    public void EveryTopPathStaysInsideUsableWidth()
    {
        var width = LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, Result.Value.Geometry);
        Assert.All(Result.Value.Search.TopTwenty.SelectMany(item => item.Path!.Samples),
            point => Assert.InRange(point.LateralOffsetMeters, 0f, width));
    }

    [Fact]
    public void FivePercentProfileContainsTwentyOnePoints() => Assert.All(
        Result.Value.Search.TopTwenty, item =>
        {
            Assert.Equal(21, item.Profile.Count);
            Assert.Equal(0f, item.Profile[0].Progress);
            Assert.Equal(1f, item.Profile[^1].Progress);
        });

    [Fact]
    public void BestResultClassificationDoesNotAssumeExpectedShape()
    {
        Assert.Equal("FCT-0B03DE6AD605",
            Result.Value.PriorReviewedWinnerAfterContinuityRepair.Candidate.Id);
        Assert.Equal("FCT-49FCA338392C",
            Result.Value.PriorReviewedBroadLateWinnerAfterEnvelopeRepair.Candidate.Id);
        Assert.True(Result.Value.VariableCurvatureEnvelopeConsistent);
        Assert.True(Result.Value.LocalCornerControlConstraintActive);
        Assert.True(Result.Value.OldWinnerRejectedAfterRepair);
        Assert.True(Result.Value.StraightRepositionConstraintActive);
        Assert.True(Result.Value.VariableCurvatureReplayHealthy);
        Assert.True(Result.Value.FreeTrajectoryGeometrySignalHealthy);
    }

    [Fact]
    public void ReviewedHeadFct0BUsesOnlyDifferentialLateTurningDemand()
    {
        var value = Result.Value.PriorReviewedWinnerAfterContinuityRepair;
        Assert.True(value.IsValid);
        var canonical = Assert.Single(value.TurningDemandConstraints
            .Where(item => item.IsCanonicalBaseline));
        var supplements = value.TurningDemandConstraints
            .Where(item => !item.IsCanonicalBaseline
                && item.SettledCapabilitySquaredDeltaMetersSquaredPerSecondSquared < 0f).ToArray();
        Assert.NotEmpty(supplements);
        Assert.Equal(.5f, canonical.Progress);
        Assert.All(supplements, peak =>
        {
            Assert.True(peak.CanonicalBaselineSpeedMetersPerSecond
                >= peak.SettledCapabilityMetersPerSecond);
            var expected = FreeContinuousRacingTrajectoryGeometryExperiment
                .DifferentialSupplementalCapabilityMetersPerSecond(
                    peak.CanonicalBaselineSpeedMetersPerSecond,
                    canonical.LocalSettledCapabilityMetersPerSecond,
                    peak.LocalSettledCapabilityMetersPerSecond);
            Assert.InRange(MathF.Abs(expected - peak.SettledCapabilityMetersPerSecond), 0f, 1e-5f);
        });
        Assert.Contains(supplements, peak => peak.Progress > .5f);
    }

    [Fact]
    public void DecisionTreeSelectsOneNextSubsystem()
    {
        Assert.False(Result.Value.Search.SearchConvergenceUncertain);
        Assert.NotEqual("D", Result.Value.DecisionCase);
        Assert.NotEqual("search convergence", Result.Value.FirstActualBottleneck);
    }

    [Fact]
    public void RepeatedLapDiagnosticCompletesForBothComparedPaths()
    {
        Assert.True(Result.Value.RepeatedBestConstant.Converged);
        Assert.InRange(Result.Value.RepeatedBestFound.Iterations, 1, 20);
        Assert.InRange(Result.Value.RepeatedBestConstant.Iterations, 1, 20);
        AssertFinitePositive(Result.Value.RepeatedBestFound.StableCornerEntrySpeedMetersPerSecond);
    }

    [Fact]
    public void SearchObjectiveIsSectorNotRepeatedLap()
    {
        var source = CanonicalText("src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs");
        var searchStart = source.IndexOf("private static FreeTrajectorySearchResult RunSearch", StringComparison.Ordinal);
        var searchEnd = source.IndexOf("private static IReadOnlyList<FreeTrajectoryCandidate> SelectGeometryDiverse",
            searchStart, StringComparison.Ordinal);
        Assert.True(searchStart >= 0 && searchEnd > searchStart);
        Assert.DoesNotContain("RunRepeatedLap", source[searchStart..searchEnd], StringComparison.Ordinal);
    }

    [Fact]
    public void ReportIsInvariantCulture() => Assert.Equal(
        UnderCulture("en-US", () => FreeContinuousRacingTrajectoryGeometryReport.Render(Result.Value)),
        UnderCulture("pl-PL", () => FreeContinuousRacingTrajectoryGeometryReport.Render(Result.Value)));

    [Fact]
    public void ReportIsLfOnly() => Assert.DoesNotContain('\r', Report.Value);

    [Fact]
    public void ReportHasNoNaNOrInfinity()
    {
        Assert.DoesNotContain("NaN", Report.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", Report.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportContainsAllSectionsAThroughZ()
    {
        foreach (var letter in Enumerable.Range('A', 26).Select(value => (char)value))
            Assert.Contains($"## {letter}.", Report.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportMatchesCommittedArtifact() => Assert.Equal(
        CanonicalText("docs/calibration/free-continuous-racing-trajectory-geometry.md"), Report.Value);

    [Theory]
    [InlineData("src/CoreSim/Decisions/AdaptiveDecisionModel.cs", "A1FCBA7E068C09B69CF849492424C89083BAD69F965FC6D767415EA9F7478314")]
    [InlineData("src/CoreSim/Decisions/RiderDecision.cs", "A38180EAF19EA50A7C0863A1B1C4FDD59776330ED6170ACF01F0AB77FA678052")]
    [InlineData("src/CoreSim/Track/LateralMovementModel.cs", "4A5A71D726B10D1D52C84BCFC053E63E11A4F4D21A381225DC23821D3BA9AC44")]
    [InlineData("src/CoreSim/Track/ContinuousCornerEnvelope.cs", "4C86C9B232D8112150AD8FB9D5CF1E2B65941A3D132C7D82E61379FE367F748B")]
    [InlineData("src/CoreSim/Track/LongitudinalDynamics.cs", "33349B6719C69F4F4743D69200A68266D452A7EDD601E702E1E7F9233640EF31")]
    [InlineData("src/CoreSim/Track/TrackEvolution.cs", "DF64FB9A640601FB5A381E4307E6D605A848CB470D46B3C5616D505D7671E27B")]
    [InlineData("src/CoreSim/Track/TrackState.cs", "20CA39B95F183F1F19C7ED8ABD5AB0C4F079FA06F58465876284F47BA3C3DBAF")]
    [InlineData("src/CoreSim/SimulationEngine.cs", "62424F726A31BDB69900A4468D8F2EE190F6D7C7129AE3CF31F6053404416746")]
    public void FrozenProductionFilesRemainCanonical(string path, string hash) => AssertCanonicalHash(path, hash);

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
    [InlineData("docs/calibration/dynamic-corner-trajectory-geometry.md", "0CFEA508D0CDC23B980022CFCBEBED7DA52B97403F39298751D367715295AA53")]
    public void HistoricalCalibrationArtifactsRemainCanonical(string path, string hash) =>
        AssertCanonicalHash(path, hash);

    private static string UnderCulture(string culture, Func<string> action)
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    private static void AssertFinitePositive(params double[] values) => Assert.All(values,
        value => Assert.True(double.IsFinite(value) && value > 0d, $"Expected finite positive, got {value}."));

    private static float RootMeanSquareDifference(IReadOnlyList<float> first, IReadOnlyList<float> second)
    {
        var sum = 0d;
        for (var index = 0; index < first.Count; index++)
        {
            var difference = first[index] - second[index];
            sum += difference * difference;
        }
        return (float)Math.Sqrt(sum / first.Count);
    }

    private static void AssertCanonicalHash(string relative, string expected) => Assert.Equal(expected,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText(relative)))));

    private static string CanonicalText(string relative) => File.ReadAllText(Path.Combine(
            Root, relative.Replace('/', Path.DirectorySeparatorChar)))
        .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

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
