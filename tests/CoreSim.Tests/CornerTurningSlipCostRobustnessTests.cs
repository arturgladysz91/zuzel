using System.Globalization;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

public sealed class CornerTurningSlipCostRobustnessTests
{
    private static readonly Lazy<TurningCostRobustnessResult> Result =
        new(FreeContinuousRacingTrajectoryGeometryExperiment.RunTurningCostRobustness);

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
        Assert.True(Math.Abs(jumped) > TurningCostZeroLimitDiagnostic.SectorResolutionSeconds);
        Assert.InRange(Math.Abs(jumped - .000020d), 0d, 1e-15d);
        Assert.True(TurningCostZeroLimitDiagnostic.SectorResolutionSeconds < .006022d / 500d);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TurningCostZeroLimitDiagnostic.EstimateIntercept(0d, 0d, upper, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TurningCostZeroLimitDiagnostic.EstimateIntercept(upper, 0d, lower, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TurningCostZeroLimitDiagnostic.EstimateIntercept(lower, double.NaN, upper, 0d));
    }

    [Fact]
    public void FrozenControllerSplitIsReportedAsFailedZeroLimitValidationNotAsPhysicsPass()
    {
        var probes = Result.Value.ZeroLimitProbes;
        Assert.Equal(2, probes.Count);
        // Known negative validation outcome: preserve the reviewed integrator,
        // but require the stricter diagnostic to flag its finite intercept.
        // Green tests certify detection, NOT the requested physics invariant.
        Assert.All(probes, probe => Assert.False(probe.SectorZeroLimitPass));
        Assert.InRange(probes[0].EstimatedSectorInterceptSeconds, -.000021d, -.000013d);
        Assert.InRange(probes[1].EstimatedSectorInterceptSeconds, -.000107d, -.000094d);
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
            Assert.True(probe.SectorDeltaSeconds(probe.Samples[1]) < 0d);
            Assert.InRange(Math.Abs(probe.SectorDeltaSeconds(probe.Samples[1])
                - probe.SectorDeltaSeconds(probe.Samples[2])), 0d,
                TurningCostZeroLimitDiagnostic.SectorResolutionSeconds);
            Assert.True(Math.Abs(probe.Samples[1].Evaluation.CorrectionDistanceMeters
                - zero.CorrectionDistanceMeters) > .1f);
            foreach (var sample in probe.Samples.Skip(1)) Assert.True(sample.Evaluation.TotalTurningLossEnergyJoules > 0d);
            Assert.InRange(probe.Samples[2].Evaluation.TotalTurningLossEnergyJoules
                / probe.Samples[1].Evaluation.TotalTurningLossEnergyJoules, 9.99d, 10.01d);
        });
        var report = TurningCostRobustnessReport.Render(Result.Value);
        Assert.Contains("clean zero-limit validation FAILS", report);
        Assert.Contains("green software tests do NOT certify zero-limit continuity", report);
    }

    [Fact]
    public void RobustnessUsesUnclippedWholeLineAndAmplitudeFamiliesAndReportsInvalidGeometry()
    {
        var result = Result.Value;
        Assert.Equal(14, result.Variants.Count);
        Assert.Equal(new[] { -1f, -.5f, -.25f, -.1f, 0f, .1f, .25f, .5f, 1f },
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
            if (probe.Variation == TurningCostTrajectoryVariation.WholeLineShift && probe.Parameter < 0f)
            {
                Assert.False(probe.Evaluation.IsValid);
                Assert.Equal("TrackBoundary", probe.Evaluation.InvalidReason);
                Assert.Contains(probe.Evaluation.Candidate.ControlOffsetsMeters, x => x < 0f);
                Assert.Null(probe.MaximumLateralDisplacementMeters);
            }
            else
            {
                Assert.True(probe.Evaluation.IsValid);
                Assert.NotNull(probe.MaximumLateralDisplacementMeters);
                Assert.InRange(Math.Abs(probe.Evaluation.TotalTurningLossEnergyJoules
                    - probe.Evaluation.TurningLossEnergyDuringCorrectionJoules
                    - probe.Evaluation.TurningLossEnergyDuringDriveJoules), 0d, 1e-7d);
            }
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
    public void RobustnessReportIsInvariantCultureAndPreservesNegativeValidationOutcome()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = TurningCostRobustnessReport.Render(Result.Value);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal(english, TurningCostRobustnessReport.Render(Result.Value));
            Assert.Contains("zero-limit diagnostic FAIL", english);
            Assert.Contains("TrackBoundary", english);
            Assert.DoesNotContain('\r', english);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
