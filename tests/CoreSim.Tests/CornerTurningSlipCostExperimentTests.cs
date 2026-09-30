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
        Assert.Equal(passive.ExitSpeedMetersPerSecond - .01f, correction.CorrectionExitSpeedMetersPerSecond);
        Assert.True(correction.ExitSpeedMetersPerSecond < correction.CorrectionExitSpeedMetersPerSecond);
        Assert.InRange(correction.CorrectionDistanceMeters, 0f, 1f);
        Assert.True(correction.CorrectionLossEnergyJoules > 0d);
        Assert.True(correction.OutsideCorrectionLossEnergyJoules > 0d);
        Assert.Equal(correction.CorrectionLossEnergyJoules + correction.OutsideCorrectionLossEnergyJoules,
            correction.LossEnergyJoules);
        var unreachable = Step(1f);
        Assert.Equal(2.6f, unreachable.AdditionalCorrectionMetersPerSecondSquared);
        Assert.True(unreachable.ExitSpeedMetersPerSecond > 1f);
    }

    [Theory]
    [InlineData(1e-8f)]
    [InlineData(.001f)]
    [InlineData(.005f)]
    [InlineData(.32f)]
    public void PartialCorrectionCrossingAndRemainingCarryPartitionDistanceAndEnergy(float coefficient)
    {
        const float entry = 26f, target = 24f, distance = 30f, capability = 2.6f;
        const float curvature = 1f / 31f;
        var step = TurningCostIntegrator.Step(entry, distance, curvature, coefficient,
            0f, 700f, BikeSetup.Neutral, target, capability);
        var legacy = LongitudinalDynamics.CalculateCornerSpeedCorrectionProfile(entry, target, capability, distance);
        var midpoint = ((double)entry + target) * .5d;
        var crossingDistance = ((double)entry * entry - (double)target * target)
            / (2d * (capability + coefficient * Math.Abs((double)curvature) * midpoint * midpoint));
        Assert.Equal((float)crossingDistance, step.CorrectionDistanceMeters);
        Assert.InRange(step.CorrectionDistanceMeters, 0f, legacy.CorrectionDistanceMeters);
        Assert.True(step.CorrectionDistanceMeters < distance);
        Assert.Equal(target, step.CorrectionExitSpeedMetersPerSecond);
        Assert.InRange(step.ExitSpeedMetersPerSecond, 0f, target);
        Assert.Equal(capability, step.AdditionalCorrectionMetersPerSecondSquared);
        Assert.True(step.CorrectionLossEnergyJoules > 0d && step.OutsideCorrectionLossEnergyJoules > 0d);
        var carryDistance = distance - step.CorrectionDistanceMeters;
        var carryMidpoint = (float)(((double)target + step.ExitSpeedMetersPerSecond) * .5d);
        Assert.Equal((double)TurningCostForce.RequestedLossNewtons(coefficient, (float)midpoint, curvature)
            * step.CorrectionDistanceMeters, step.CorrectionLossEnergyJoules);
        Assert.Equal((double)TurningCostForce.RequestedLossNewtons(coefficient, carryMidpoint, curvature)
            * carryDistance, step.OutsideCorrectionLossEnergyJoules);
        Assert.Equal(step.CorrectionLossEnergyJoules + step.OutsideCorrectionLossEnergyJoules, step.LossEnergyJoules);
        var mass = LongitudinalDynamics.ProvisionalNominalSystemMassKilograms;
        var kineticDebit = .5d * mass * ((double)entry * entry
            - (double)step.ExitSpeedMetersPerSecond * step.ExitSpeedMetersPerSecond);
        var controlWork = (double)mass * capability * step.CorrectionDistanceMeters;
        var roundingBound = mass * (entry + step.ExitSpeedMetersPerSecond)
            * 2d * TurningCostZeroLimitDiagnostic.OutputUlp(entry);
        Assert.InRange(Math.Abs(kineticDebit - controlWork - step.LossEnergyJoules), 0d, roundingBound);
        // Roll-off/carry must not acquire propulsion from the availability argument.
        Assert.Equal(step, TurningCostIntegrator.Step(entry, distance, curvature, coefficient,
            1f, 700f, BikeSetup.Neutral, target, capability) with
            { PassiveEndSpeedMetersPerSecond = step.PassiveEndSpeedMetersPerSecond });
        if (coefficient == 1e-8f)
        {
            Assert.InRange(Math.Abs((double)step.CorrectionDistanceMeters - legacy.CorrectionDistanceMeters),
                0d, 2d * TurningCostZeroLimitDiagnostic.OutputUlp(legacy.CorrectionDistanceMeters));
            Assert.InRange(Math.Abs((double)step.CorrectionTimeSeconds - legacy.TravelTimeSeconds),
                0d, 2d * TurningCostZeroLimitDiagnostic.OutputUlp(legacy.TravelTimeSeconds));
        }
    }

    [Fact]
    public void ControllerRequestedCarryBelowTargetHasLossWithoutInventingCorrectionDistance()
    {
        var step = TurningCostIntegrator.Step(20f, 1f, 1f / 31f, .005f,
            1f, 700f, BikeSetup.Neutral, 21f, 2.6f, correctionRequested: true);
        Assert.True(step.ControllerLimited);
        Assert.False(step.CorrectionActive);
        Assert.Equal(0f, step.CorrectionDistanceMeters);
        Assert.Equal(0f, step.CorrectionTimeSeconds);
        Assert.Equal(0d, step.CorrectionLossEnergyJoules);
        Assert.True(step.ExitSpeedMetersPerSecond < step.EntrySpeedMetersPerSecond);
        Assert.True(step.OutsideCorrectionLossEnergyJoules > 0d);
    }

    [Fact]
    public void NonTraversableZeroSpeedCarryIsInvalidInsteadOfAbortingSearchOrWinningWithZeroTime()
    {
        var step = TurningCostIntegrator.Step(20f, 100f, 1f / 31f, .005f,
            0f, 700f, BikeSetup.Neutral, 0f, 2.6f);
        Assert.False(step.IsTraversable);
        Assert.True(double.IsFinite(step.TimeSeconds));
        Assert.True(step.TimeSeconds > 0d);
        Assert.Equal(0f, step.CorrectionExitSpeedMetersPerSecond);
        Assert.True(step.CorrectionDistanceMeters < step.DistanceMeters);
        Assert.True(step.RemainingDistanceMeters > 0f);
        Assert.Equal(step.CorrectionDistanceMeters, step.TraversedDistanceMeters);
        var stoppedCarry = TurningCostIntegrator.Step(20f, 10f, .05f, 4.04f,
            0f, 700f, BikeSetup.Neutral, 20f, 2.6f, correctionRequested: true);
        var repeatedCarry = TurningCostIntegrator.Step(20f, 10f, .05f, 4.04f,
            0f, 700f, BikeSetup.Neutral, 20f, 2.6f, correctionRequested: true);
        Assert.Equal(stoppedCarry, repeatedCarry);
        Assert.False(stoppedCarry.IsTraversable);
        Assert.Equal(0f, stoppedCarry.ExitSpeedMetersPerSecond);
        Assert.True(stoppedCarry.RemainingDistanceMeters > 0f);
        Assert.InRange(stoppedCarry.TraversedDistanceMeters, 9.89f, 9.91f);
        Assert.True(double.IsFinite(stoppedCarry.TimeSeconds) && stoppedCarry.TimeSeconds > 0d);
        Assert.True(double.IsFinite(stoppedCarry.LossEnergyJoules) && stoppedCarry.LossEnergyJoules > 0d);
        Assert.True(float.IsFinite(stoppedCarry.LossForceNewtons) && stoppedCarry.LossForceNewtons >= 0f);
        Assert.True(float.IsFinite(stoppedCarry.LossPowerWatts) && stoppedCarry.LossPowerWatts >= 0f);
        var traversableCarry = TurningCostIntegrator.Step(20f, 10f, .05f, 3.96f,
            0f, 700f, BikeSetup.Neutral, 20f, 2.6f, correctionRequested: true);
        Assert.True(traversableCarry.IsTraversable);
        Assert.True(traversableCarry.ExitSpeedMetersPerSecond > 0f);
        Assert.Equal(10f, traversableCarry.TraversedDistanceMeters);
        Assert.Equal(0f, traversableCarry.RemainingDistanceMeters);
        var controls = new float[FreeContinuousRacingTrajectoryGeometryExperiment.ControlStationCount];
        var stalled = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(
            controls, 128f);
        Assert.False(stalled.IsValid);
        Assert.Equal(FreeTrajectoryValidity.NonTraversable, stalled.Validity);
        Assert.Equal("NonTraversable", stalled.InvalidReason);
        Assert.True(stalled.RemainingCornerDistanceMeters > 0f);
        Assert.InRange(stalled.NonTraversableProgress!.Value, 0f, 1f);
        Assert.True(float.IsFinite(stalled.NonTraversableDistanceMeters!.Value));
        Assert.Equal(stalled.Path!.TotalLengthMeters,
            stalled.NonTraversableDistanceMeters + stalled.RemainingCornerDistanceMeters);
        var repeated = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(controls, 128f);
        Assert.Equal(stalled.Candidate.Id, repeated.Candidate.Id);
        Assert.Equal(stalled.Validity, repeated.Validity);
        Assert.Equal(stalled.InvalidReason, repeated.InvalidReason);
        Assert.Equal(stalled.NonTraversableProgress, repeated.NonTraversableProgress);
        Assert.Equal(stalled.NonTraversableDistanceMeters, repeated.NonTraversableDistanceMeters);
        Assert.Equal(stalled.RemainingCornerDistanceMeters, repeated.RemainingCornerDistanceMeters);
        Assert.All(new[] { stalled.SectorTimeSeconds, stalled.CornerTimeSeconds, stalled.ExitSpeedMetersPerSecond },
            value => Assert.True(float.IsFinite(value) && value >= 0f));
        Assert.Empty(stalled.Profile);
    }

    [Fact]
    public void FixedTrajectoryHasSmallFiniteResponseNearZeroAndSaturationButThisIsNotAZeroLimitProof()
    {
        var controls = Enumerable.Repeat(0f,
            FreeContinuousRacingTrajectoryGeometryExperiment.ControlStationCount).ToArray();
        var zero = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(controls, 0f);
        var tiny = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(controls, .00001f);
        Assert.True(zero.IsValid && tiny.IsValid);
        Assert.InRange(tiny.SectorTimeSeconds - zero.SectorTimeSeconds, 0f, .0001f);
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
            Assert.Equal(scenario.Search.CandidatesEvaluated, scenario.Search.ValidCandidates
                + scenario.Search.InvalidGeometryCandidates + scenario.Search.InvalidLateralExecutionCandidates
                + scenario.Search.InvalidCornerControlDepartureCandidates + scenario.Search.InvalidCrashCandidates
                + scenario.Search.InvalidStraightRepositionCandidates + scenario.Search.InvalidNonTraversableCandidates);
            Assert.Equal(scenario.Search.InvalidGeometryCandidates, scenario.Search.InvalidTrackBoundaryCandidates
                + scenario.Search.InvalidSelfIntersectionCandidates + scenario.Search.InvalidNonSmoothGeometryCandidates);
            Assert.InRange(Math.Abs(scenario.Winner.TotalTurningLossEnergyJoules
                - scenario.Winner.TurningLossEnergyDuringCorrectionJoules
                - scenario.Winner.TurningLossEnergyDuringDriveJoules), 0d, 1e-7d);
        });
        var atFive = result.Scenarios.Single(x => x.Coefficient == .005f);
        Assert.Equal(atFive.Winner.Candidate.ControlOffsetsMeters,
            result.Robustness.Reference.Candidate.ControlOffsetsMeters);
        Assert.Equal(result.Robustness.Reference.SectorTimeSeconds, atFive.Winner.SectorTimeSeconds);
        Assert.Equal(CornerTurningSlipCostReport.Render(result),
            CornerTurningSlipCostReport.Render(result));
    }
}
