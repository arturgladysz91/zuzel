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
        Assert.True(above <= drive);
        Assert.True(drive - above >= 0f);
        Assert.Equal(0f, TurningCostForce.LossNewtons(.1f, speed, curvature,
            0f, referenceForce, BikeSetup.Neutral));
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
        Assert.All(zero.Winner.Candidate.ControlOffsetsMeters, value => Assert.Equal(0f, value));
        Assert.InRange(zero.Winner.SectorTimeSeconds, 6.676828f, 6.676830f);
        Assert.Equal(0f, zero.Winner.SectorTimeSeconds - zero.ConstantInner.SectorTimeSeconds);
        Assert.True(zero.Search.ObjectiveConvergence);
        Assert.True(zero.Search.GeometryConvergence);
        Assert.Equal(FreeContinuousRacingTrajectoryGeometryExperiment.TurningCostSweep,
            result.Scenarios.Select(x => x.Coefficient));
        Assert.Equal(CornerTurningSlipCostReport.Render(result),
            CornerTurningSlipCostReport.Render(result));
    }
}
