using System.Globalization;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "historical-analysis")]
public sealed class CornerTurningSlipCostRobustnessTests
{
    private static readonly Lazy<TurningCostRobustnessResult> Result =
        new(() => FreeContinuousRacingTrajectoryGeometryExperiment.RunTurningCostRobustness());

    [Fact]
    public void ZeroLimitDiagnosticDetectsTwentyMicrosecondJumpInsteadOfAcceptingOldTenMillisecondBound()
    {
        const double lower = 1e-8d;
        const double upper = 1e-7d;
        const double slope = 4d;
        var continuous = TurningCostZeroLimitDiagnostic.EstimateIntercept(lower, slope * lower, upper, slope * upper);
        var jumped = TurningCostZeroLimitDiagnostic.EstimateIntercept(lower, slope * lower + .000020d,
            upper, slope * upper + .000020d);
        Assert.InRange(Math.Abs(continuous), 0d, 1e-15d);
        var ulp = TurningCostZeroLimitDiagnostic.OutputUlp(6.7f);
        Assert.True(Math.Abs(jumped) / ulp > TurningCostZeroLimitDiagnostic.MaximumInterceptUlps);
        Assert.InRange(Math.Abs(jumped - .000020d), 0d, 1e-15d);
        Assert.True(ulp * TurningCostZeroLimitDiagnostic.MaximumInterceptUlps < .006022d / 5000d);
        // The previous measured plateaus must be rejected, not fitted away.
        Assert.True(Math.Abs(TurningCostZeroLimitDiagnostic.EstimateIntercept(lower, -.000017166d,
            upper, -.000016212d)) / ulp > TurningCostZeroLimitDiagnostic.MaximumInterceptUlps);
        Assert.True(Math.Abs(TurningCostZeroLimitDiagnostic.EstimateIntercept(lower, -.000100613d,
            upper, -.000100613d)) / ulp > TurningCostZeroLimitDiagnostic.MaximumInterceptUlps);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TurningCostZeroLimitDiagnostic.EstimateIntercept(0d, 0d, upper, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TurningCostZeroLimitDiagnostic.EstimateIntercept(upper, 0d, lower, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TurningCostZeroLimitDiagnostic.EstimateIntercept(lower, double.NaN, upper, 0d));
    }

    [Fact]
    public void RepairedControllerConvergesToFortySevenWithinOutputUlpsWithoutCorrectionPlateaus()
    {
        var probes = Result.Value.ZeroLimitProbes;
        Assert.Equal(2, probes.Count);
        Assert.All(probes, probe => Assert.True(probe.SectorZeroLimitPass));
        Assert.All(probes, probe =>
        {
            Assert.Equal(new[] { 0f, 1e-8f, 1e-7f, 1e-6f, 1e-5f, 1e-4f, .001f },
                probe.Samples.Select(x => x.Coefficient));
            Assert.All(probe.Samples, sample => Assert.True(sample.Evaluation.IsValid));
            var zero = probe.Samples[0].Evaluation;
            var baseline = FreeContinuousRacingTrajectoryGeometryExperiment.Evaluate(zero.Candidate.ControlOffsetsMeters);
            Assert.Equal(baseline.SectorTimeSeconds, zero.SectorTimeSeconds);
            Assert.Equal(baseline.Profile, zero.Profile);
            Assert.Equal(0d, zero.TotalTurningLossEnergyJoules);
            var tiny = probe.Samples[1].Evaluation;
            void WithinUlps(float baseline, float value) => Assert.InRange(Math.Abs((double)value - baseline),
                0d, TurningCostZeroLimitDiagnostic.MaximumInterceptUlps * TurningCostZeroLimitDiagnostic.OutputUlp(baseline));
            WithinUlps(zero.SectorTimeSeconds, tiny.SectorTimeSeconds);
            WithinUlps(zero.CornerTimeSeconds, tiny.CornerTimeSeconds);
            WithinUlps(zero.ExitSpeedMetersPerSecond, tiny.ExitSpeedMetersPerSecond);
            WithinUlps(zero.CorrectionDistanceMeters, tiny.CorrectionDistanceMeters);
            WithinUlps(zero.CorrectionTimeSeconds, tiny.CorrectionTimeSeconds);
            Assert.InRange(Math.Abs(probe.SectorInterceptInUlps), 0d, TurningCostZeroLimitDiagnostic.MaximumInterceptUlps);
            Assert.Equal(zero.Profile.Count, tiny.Profile.Count);
            for (var index = 0; index < zero.Profile.Count; index++)
            {
                WithinUlps(zero.Profile[index].SpeedMetersPerSecond, tiny.Profile[index].SpeedMetersPerSecond);
                Assert.Equal(zero.Profile[index].CorrectionActive, tiny.Profile[index].CorrectionActive);
                Assert.Equal(zero.Profile[index].CornerControlOutcome, tiny.Profile[index].CornerControlOutcome);
            }
            foreach (var sample in probe.Samples.Skip(1)) Assert.True(sample.Evaluation.TotalTurningLossEnergyJoules > 0d);
            for (var index = 2; index < probe.Samples.Count; index++)
                Assert.True(probe.Samples[index].Evaluation.TotalTurningLossEnergyJoules
                    > probe.Samples[index - 1].Evaluation.TotalTurningLossEnergyJoules);
            Assert.True(Math.Abs((double)tiny.CorrectionDistanceMeters - zero.CorrectionDistanceMeters)
                < Math.Abs((double)probe.Samples[^1].Evaluation.CorrectionDistanceMeters - zero.CorrectionDistanceMeters));
            Assert.True(Math.Abs((double)tiny.CorrectionTimeSeconds - zero.CorrectionTimeSeconds)
                < Math.Abs((double)probe.Samples[^1].Evaluation.CorrectionTimeSeconds - zero.CorrectionTimeSeconds));
            Assert.InRange(probe.Samples[2].Evaluation.TotalTurningLossEnergyJoules
                / probe.Samples[1].Evaluation.TotalTurningLossEnergyJoules, 9.99d, 10.01d);
        });
        var report = TurningCostRobustnessReport.Render(Result.Value);
        Assert.Contains("zero-limit diagnostic PASS", report);
        Assert.DoesNotContain("zero-limit diagnostic FAIL", report);
    }

    [Fact]
    public void RobustnessUsesUnclippedOutwardAndAmplitudeFamilies()
    {
        var result = Result.Value;
        Assert.Equal(10, result.Variants.Count);
        Assert.Equal(new[] { 0f, .1f, .25f, .5f, 1f },
            result.Variants.Where(x => x.Variation == TurningCostTrajectoryVariation.WholeLineShift)
                .Select(x => x.Parameter));
        Assert.Equal(new[] { .75f, .9f, 1f, 1.1f, 1.25f },
            result.Variants.Where(x => x.Variation == TurningCostTrajectoryVariation.ShapeAmplitude)
                .Select(x => x.Parameter));
        foreach (var probe in result.Variants)
        {
            Assert.Equal(result.Reference.Candidate.ControlOffsetsMeters.Select(x =>
                probe.Variation == TurningCostTrajectoryVariation.WholeLineShift
                    ? x + probe.Parameter : x * probe.Parameter), probe.Evaluation.Candidate.ControlOffsetsMeters);
            Assert.True(probe.Evaluation.IsValid);
            Assert.NotNull(probe.MaximumLateralDisplacementMeters);
            Assert.InRange(Math.Abs(probe.Evaluation.TotalTurningLossEnergyJoules
                - probe.Evaluation.TurningLossEnergyDuringCorrectionJoules
                - probe.Evaluation.TurningLossEnergyDuringDriveJoules), 0d, 1e-7d);
        }
    }

    [Fact]
    public void PhysicalDisplacementSamplesSplineOvershootNotJustLargestControlDifference()
    {
        var result = Result.Value;
        foreach (var probe in result.Variants.Where(x => x.Evaluation.IsValid))
        {
            var expected = probe.Variation == TurningCostTrajectoryVariation.WholeLineShift
                ? MathF.Abs(probe.Parameter)
                : MathF.Abs(probe.Parameter - 1f) * result.MaximumReferenceOffsetMeters;
            Assert.InRange(MathF.Abs(probe.MaximumLateralDisplacementMeters!.Value - expected), 0f, 1e-6f);
        }
        var amplitude = result.Variants.Single(x => x.Variation == TurningCostTrajectoryVariation.ShapeAmplitude
            && x.Parameter == .75f);
        var controlOnlyMaximum = result.Reference.Candidate.ControlOffsetsMeters.Max() * .25f;
        Assert.True(amplitude.MaximumLateralDisplacementMeters > controlOnlyMaximum + .002f);
        Assert.InRange(amplitude.MaximumLateralDisplacementMeters!.Value, .1567f, .1568f);
    }

    [Fact]
    public void BetterNearbyShapeReceivesOnlySeparateDeterministicOneParameterRefinement()
    {
        var result = Result.Value;
        Assert.Equal(17, result.LocalAmplitudeRefinement.Count);
        Assert.Equal(.9f, result.LocalAmplitudeRefinement[0].Parameter);
        Assert.True(result.BestLocalAmplitude!.Evaluation.SectorTimeSeconds < result.Reference.SectorTimeSeconds);
        Assert.All(result.LocalAmplitudeRefinement, probe =>
        {
            Assert.Equal(TurningCostTrajectoryVariation.ShapeAmplitude, probe.Variation);
            Assert.Equal(result.Reference.Candidate.ControlOffsetsMeters.Select(x => x * probe.Parameter),
                probe.Evaluation.Candidate.ControlOffsetsMeters);
        });
        var repeated = FreeContinuousRacingTrajectoryGeometryExperiment.RunTurningCostRobustness();
        Assert.Equal(TurningCostRobustnessReport.Render(result), TurningCostRobustnessReport.Render(repeated));
    }

    [Fact]
    public void RobustnessReportIsInvariantCultureAndReportsUlpAwareValidationOutcome()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = TurningCostRobustnessReport.Render(Result.Value);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal(english, TurningCostRobustnessReport.Render(Result.Value));
            Assert.Contains("zero-limit diagnostic PASS", english);
            Assert.Contains("ULPs", english);
            Assert.DoesNotContain('\r', english);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void ConstantInnerReferenceDoesNotRequireNonInnerWidthOrAmplitudeProbes()
    {
        var result = FreeContinuousRacingTrajectoryGeometryExperiment.RunTurningCostRobustness(
            new float[FreeContinuousRacingTrajectoryGeometryExperiment.ControlStationCount]);
        Assert.Empty(result.Variants);
        Assert.Empty(result.LocalAmplitudeRefinement);
        Assert.Contains("no non-inner physical-width or amplitude probes are required",
            TurningCostRobustnessReport.Render(result));
        Assert.All(result.ZeroLimitProbes, probe => Assert.True(probe.SectorZeroLimitPass));
    }
}
