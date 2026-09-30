using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class CornerTurningSlipCostExperimentTests
{
    private static readonly Lazy<CornerTurningSlipCostExperimentResult> Result =
        new(FreeContinuousRacingTrajectoryGeometryExperiment.RunTurningCostExperiment);

    [Fact]
    public void ForceHasPhysicalLimitsAndMonotonicity()
    {
        Assert.Equal(0f, TurningCostForce.RequestedLossNewtons(.1f, 20f, 0f));
        Assert.Equal(0f, TurningCostForce.RequestedLossNewtons(.1f, 0f, .03f));
        Assert.Equal(0f, TurningCostForce.RequestedLossNewtons(0f, 20f, .03f));
        Assert.True(TurningCostForce.RequestedLossNewtons(.1f, 20f, .04f)
            > TurningCostForce.RequestedLossNewtons(.1f, 20f, .03f));
        Assert.True(TurningCostForce.RequestedLossNewtons(.1f, 21f, .03f)
            > TurningCostForce.RequestedLossNewtons(.1f, 20f, .03f));
        Assert.Equal(TurningCostForce.RequestedLossNewtons(.1f, 20f, -.03f),
            TurningCostForce.RequestedLossNewtons(.1f, 20f, .03f));
    }

    [Fact]
    public void DriveClampIsContinuousAndCannotIncreaseForwardForce()
    {
        const float speed = 20f;
        const float curvature = .03f;
        const float availability = .6f;
        const float referenceForce = 700f;
        var drive = availability * LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(
            referenceForce, speed, BikeSetup.Neutral);
        var saturation = drive / (LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
            * speed * speed * curvature);
        var below = TurningCostForce.LossNewtons(saturation - .00001f, speed, curvature,
            availability, referenceForce, BikeSetup.Neutral);
        var at = TurningCostForce.LossNewtons(saturation, speed, curvature,
            availability, referenceForce, BikeSetup.Neutral);
        var above = TurningCostForce.LossNewtons(saturation + .00001f, speed, curvature,
            availability, referenceForce, BikeSetup.Neutral);
        Assert.InRange(at - below, 0f, .05f);
        Assert.InRange(above - at, 0f, .05f);
        Assert.True(above > drive); // Remaining loss is passive drag, not negative engine propulsion.
        Assert.Equal(0f, MathF.Max(0f, drive - above));
        Assert.Equal(TurningCostForce.RequestedLossNewtons(.1f, speed, curvature),
            TurningCostForce.LossNewtons(.1f, speed, curvature, 0f, referenceForce, BikeSetup.Neutral));
    }

    [Fact]
    public void ReviewedWinnerPaysTurningLossDuringEveryCorrectionInterval()
    {
        var probe = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(
            FreeContinuousRacingTrajectoryGeometryExperiment.ReviewedTurningCostWinnerControls,
            .005f, captureIntervals: true);
        Assert.True(probe.IsValid);
        Assert.Contains(probe.TurningCostIntervals, interval => interval.CorrectionActive);
        Assert.All(probe.TurningCostIntervals, interval =>
        {
            Assert.True(interval.EntrySpeedMetersPerSecond > 0f);
            Assert.NotEqual(0f, interval.CurvaturePerMeter);
            Assert.True(interval.LossForceNewtons > 0f);
            Assert.True(interval.LossPowerWatts > 0f);
            Assert.True(interval.LossEnergyJoules > 0d);
        });
        Assert.True(probe.TurningLossEnergyDuringCorrectionJoules > 0d);
        Assert.True(probe.TurningLossEnergyDuringDriveJoules > 0d);
        Assert.InRange(Math.Abs(probe.TotalTurningLossEnergyJoules
            - probe.TurningLossEnergyDuringCorrectionJoules - probe.TurningLossEnergyDuringDriveJoules), 0d, 1e-8d);
        Assert.Equal(probe.TurningCostIntervals.Sum(x => x.LossEnergyJoules), probe.TotalTurningLossEnergyJoules);
        Assert.InRange(Math.Abs(probe.CorrectionModeDistanceMeters + probe.DriveModeDistanceMeters
            - probe.Path!.TotalLengthMeters), 0d, 1e-6d);
    }

    [Fact]
    public void PhaseBoundaryDoesNotRemoveLossOrDiscontinueWholeSector()
    {
        var probe = FreeContinuousRacingTrajectoryGeometryExperiment.RunTurningCostPhaseBoundaryProbe();
        Assert.True(probe.Below.IsValid && probe.Above.IsValid);
        var below = probe.Below.TurningCostIntervals[0];
        var above = probe.Above.TurningCostIntervals[0];
        Assert.False(below.CorrectionActive);
        Assert.True(above.CorrectionActive);
        Assert.Equal(below.CurvaturePerMeter, above.CurvaturePerMeter);
        Assert.InRange(above.EntrySpeedMetersPerSecond - below.EntrySpeedMetersPerSecond, 0f, .00011f);
        Assert.True(below.LossForceNewtons > 0f && above.LossForceNewtons > 0f);
        Assert.True(below.LossPowerWatts > 0f && above.LossPowerWatts > 0f);
        Assert.InRange(MathF.Abs(above.LossForceNewtons - below.LossForceNewtons), 0f, .001f);
        Assert.InRange(MathF.Abs(above.LossPowerWatts - below.LossPowerWatts), 0f, .05f);
        Assert.InRange(MathF.Abs(probe.Above.SectorTimeSeconds - probe.Below.SectorTimeSeconds), 0f, .0001f);
        Assert.InRange(Math.Abs(probe.Above.TotalTurningLossEnergyJoules
            - probe.Below.TotalTurningLossEnergyJoules), 0d, .1d);
    }

    [Fact]
    public void PassiveLossIsNotLiftedToTargetOrDoubleDebitedByCorrection()
    {
        TurningCostInterval Step(float? target, float coefficient = .005f) => TurningCostIntegrator.Step(
            20f, 1f, 1f / 31f, coefficient, 0f, 700f, BikeSetup.Neutral, target, 2.6f);
        var passive = Step(null);
        var unnecessary = Step(20f);
        Assert.False(unnecessary.CorrectionActive);
        Assert.Equal(passive.ExitSpeedMetersPerSecond, unnecessary.ExitSpeedMetersPerSecond);
        var correction = Step(passive.ExitSpeedMetersPerSecond - .01f);
        Assert.True(correction.CorrectionActive);
        Assert.InRange(correction.AdditionalCorrectionMetersPerSecondSquared, 0f, 2.6f);
        Assert.InRange(MathF.Abs(correction.ExitSpeedMetersPerSecond
            - (passive.ExitSpeedMetersPerSecond - .01f)), 0f, 2e-6f);
        Assert.Equal(passive.LossEnergyJoules, correction.LossEnergyJoules);
        var unreachable = Step(1f);
        Assert.Equal(2.6f, unreachable.AdditionalCorrectionMetersPerSecondSquared);
        Assert.True(unreachable.ExitSpeedMetersPerSecond > 1f);
    }

    [Fact]
    public void FixedTrajectoryRespondsContinuouslyNearZeroAndSaturation()
    {
        var controls = Enumerable.Repeat(0f,
            FreeContinuousRacingTrajectoryGeometryExperiment.ControlStationCount).ToArray();
        var zero = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(controls, 0f);
        var tiny = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(controls, .00001f);
        Assert.True(zero.IsValid && tiny.IsValid);
        Assert.InRange(tiny.SectorTimeSeconds - zero.SectorTimeSeconds, 0f, .01f);
        Assert.True(tiny.TurningLossEnergyJoules >= 0d);
        var low = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(controls, .3199f);
        var high = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(controls, .3201f);
        Assert.Equal(low.Validity, high.Validity);
        if (low.IsValid)
            Assert.InRange(MathF.Abs(high.SectorTimeSeconds - low.SectorTimeSeconds), 0f, .01f);
    }

    [Fact]
    public void ZeroCoefficientDirectReplayMatchesFortySevenValidityAndMetrics()
    {
        foreach (var offset in new[] { 0f, 2f, 4f, 6f, 8f, -1f })
        {
            var controls = Enumerable.Repeat(offset,
                FreeContinuousRacingTrajectoryGeometryExperiment.ControlStationCount).ToArray();
            var baseline = FreeContinuousRacingTrajectoryGeometryExperiment.Evaluate(controls);
            var additive = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(controls, 0f);
            Assert.Equal(baseline.Validity, additive.Validity);
            Assert.Equal(baseline.InvalidReason, additive.InvalidReason);
            Assert.Equal(baseline.CornerTimeSeconds, additive.CornerTimeSeconds);
            Assert.Equal(baseline.SectorTimeSeconds, additive.SectorTimeSeconds);
            Assert.Equal(baseline.ExitSpeedMetersPerSecond, additive.ExitSpeedMetersPerSecond);
            Assert.Equal(baseline.BrakeStepCount, additive.BrakeStepCount);
            Assert.Equal(baseline.CorrectionDistanceMeters, additive.CorrectionDistanceMeters);
            Assert.Equal(baseline.CorrectionTimeSeconds, additive.CorrectionTimeSeconds);
            Assert.Equal(baseline.Profile, additive.Profile);
            Assert.Equal(baseline.PeakEnvelopeOverspeedRatio, additive.PeakEnvelopeOverspeedRatio);
            Assert.Equal(0d, additive.TurningLossEnergyJoules);
        }
    }

    [Fact]
    public void ZeroScenarioIsExactlyMergedFortySevenAndSweepIsDeterministic()
    {
        var result = Result.Value;
        var zero = result.Scenarios[0];
        Assert.Equal("99f3afd08c8e6892a8b24eb785b5be6cbf116d38", result.BaseMainSha);
        Assert.Equal(result.Baseline.BestFound.Candidate.Id, zero.Winner.Candidate.Id);
        Assert.Equal(result.Baseline.BestFound.SectorTimeSeconds, zero.Winner.SectorTimeSeconds);
        Assert.Equal(result.Baseline.ConstantInner.SectorTimeSeconds, zero.ConstantInner.SectorTimeSeconds);
        Assert.Equal(result.Baseline.Search.ObjectiveConvergence, zero.Search.ObjectiveConvergence);
        Assert.Equal(result.Baseline.Search.GeometryConvergence, zero.Search.GeometryConvergence);
        Assert.Same(result.Baseline.Search, zero.Search);
        Assert.Same(result.Baseline.RepeatedBestFound, zero.RepeatedLap);
        Assert.Equal("FCT-B843ADDB373E", zero.Winner.Candidate.Id);
        Assert.All(zero.Winner.Candidate.ControlOffsetsMeters, value => Assert.Equal(0f, value));
        Assert.InRange(zero.Winner.SectorTimeSeconds, 6.676828f, 6.676830f);
        Assert.Equal(0f, zero.Winner.SectorTimeSeconds - zero.ConstantInner.SectorTimeSeconds);
        Assert.True(zero.Search.ObjectiveConvergence);
        Assert.True(zero.Search.GeometryConvergence);
        Assert.Equal(FreeContinuousRacingTrajectoryGeometryExperiment.TurningCostSweep,
            result.Scenarios.Select(x => x.Coefficient));
        Assert.All(result.Scenarios, scenario =>
        {
            Assert.Equal(106, scenario.Search.StartsGenerated);
            Assert.Equal(48, scenario.Search.RefinedStartCount);
            Assert.InRange(Math.Abs(scenario.Winner.TotalTurningLossEnergyJoules
                - scenario.Winner.TurningLossEnergyDuringCorrectionJoules
                - scenario.Winner.TurningLossEnergyDuringDriveJoules), 0d, 1e-7d);
        });
        Assert.Equal(CornerTurningSlipCostReport.Render(result),
            CornerTurningSlipCostReport.Render(result));
    }
}
