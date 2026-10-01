using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class CombinedGripAvailabilityExperimentTests
{
    private static readonly Lazy<CombinedGripExperimentResult> Result =
        new(FreeContinuousRacingTrajectoryGeometryExperiment.RunCombinedGripExperiment);

    [Fact]
    public void ExistingSettledSpeedDefinesOneRadiusIndependentAccelerationCapacity()
    {
        var surface = CalibrationScenarioCatalog.Baseline.Surface;
        var capacity = CombinedGripAvailability.DeriveLateralAccelerationCapacity(surface,
            RiderSkills.Balanced, BikeSetup.Neutral);
        foreach (var radius in new[] { 10f, 24f, 31f, 48f, 100f })
        {
            var speed = SegmentPhysics.MaxSafeTurnSpeedForRadius(radius, surface,
                RiderSkills.Balanced, BikeSetup.Neutral);
            Assert.InRange(MathF.Abs(speed * speed / radius - capacity), 0f, 4f * Ulp(capacity));
        }
    }

    [Theory]
    [InlineData(500f)]
    [InlineData(0f)]
    [InlineData(-300f)]
    public void IdenticalPhysicalRequestsAreInvariantUnderControllerLabels(float request)
    {
        // A caller cannot make the same physical action exempt by changing metadata.
        var labelledActions = new[] { ("Drive", request), ("Correction", request), ("Carry", request) };
        var states = labelledActions.Select(action => CombinedGripAvailability.Evaluate(
            20f, .03f, 15f, 500f, action.Item2, .75f)).ToArray();
        Assert.All(states, state => Assert.Equal(states[0], state));
        if (request > 0f) Assert.True(states[0].AvailableLongitudinalForceNewtons < request);
    }

    [Fact]
    public void ForceBudgetUsesDemandNotAnEnergyPenaltyAndPreservesUnits()
    {
        var state = CombinedGripAvailability.Evaluate(20f, .03f, 15f, 500f, 500f, 1f);
        Assert.Equal(12f, state.LateralAccelerationMetersPerSecondSquared);
        Assert.InRange(MathF.Abs(.8f - state.LateralUtilization), 0f, Ulp(.8f));
        Assert.InRange(state.LongitudinalAvailabilityFactor, .5999999f, .6000001f);
        Assert.InRange(state.LongitudinalCapacityNewtons, 299.99997f, 300.00003f);
        Assert.Equal(state.LongitudinalCapacityNewtons, state.AvailableLongitudinalForceNewtons);
        Assert.Equal(500f - state.AvailableLongitudinalForceNewtons, state.ClippedDriveForceNewtons);
        // Less than capacity needs no force reduction.
        Assert.Equal(100f, CombinedGripAvailability.Evaluate(20f, .03f, 15f, 500f, 100f, 1f)
            .AvailableLongitudinalForceNewtons);
    }

    [Fact]
    public void LateralDemandCannotIncreasePropulsionOrCreateEnergy()
    {
        var previous = 500f;
        foreach (var curvature in new[] { 0f, .005f, .01f, .02f, .03f, .04f, .10f })
        {
            var state = CombinedGripAvailability.Evaluate(20f, curvature, 15f, 500f, 500f, .75f);
            Assert.InRange(state.AvailableLongitudinalForceNewtons, 0f, previous);
            Assert.InRange(state.LongitudinalAvailabilityFactor, 0f, 1f);
            previous = state.AvailableLongitudinalForceNewtons;
            Assert.Equal(state, CombinedGripAvailability.Evaluate(20f, -curvature, 15f, 500f, 500f, .75f));
            var baseline = CombinedGripIntegrator.DriveEndSpeed(20f, 1f, curvature, 15f, 0f,
                500f, BikeSetup.Neutral, 1f);
            var coupled = CombinedGripIntegrator.DriveEndSpeed(20f, 1f, curvature, 15f, .75f,
                500f, BikeSetup.Neutral, 1f);
            Assert.True(coupled <= baseline);
        }
        // No requested drive: coupling cannot create thrust or lateral drag.
        Assert.Equal(20f, CombinedGripIntegrator.DriveEndSpeed(20f, 1f, .10f, 15f, 1f,
            500f, BikeSetup.Neutral, 0f));
        // Full saturation leaves resistance intact and naturally slows the bike.
        Assert.True(CombinedGripIntegrator.DriveEndSpeed(20f, 1f, .10f, 15f, 1f,
            500f, BikeSetup.Neutral, 1f) < 20f);
    }

    [Fact]
    public void CapacityIsContinuousThroughSaturationAndControllerTransition()
    {
        const float speed = 20f, capacity = 15f, engine = 500f;
        var saturationCurvature = capacity / (speed * speed);
        var at = CombinedGripAvailability.Evaluate(speed, saturationCurvature, capacity, engine, engine, 1f);
        var below = CombinedGripAvailability.Evaluate(speed, MathF.BitDecrement(saturationCurvature), capacity,
            engine, engine, 1f);
        var above = CombinedGripAvailability.Evaluate(speed, MathF.BitIncrement(saturationCurvature), capacity,
            engine, engine, 1f);
        Assert.InRange(below.LongitudinalCapacityNewtons - at.LongitudinalCapacityNewtons, 0f, .3f);
        Assert.InRange(at.LongitudinalCapacityNewtons - above.LongitudinalCapacityNewtons, 0f, .3f);
        var positive = CombinedGripAvailability.Evaluate(speed, .02f, capacity, engine, .0001f, .5f);
        var negative = CombinedGripAvailability.Evaluate(speed, .02f, capacity, engine, -.0001f, .5f);
        Assert.Equal(positive.LongitudinalCapacityNewtons, negative.LongitudinalCapacityNewtons);
        Assert.Equal(positive.LongitudinalAvailabilityFactor, negative.LongitudinalAvailabilityFactor);
        Assert.InRange(positive.AvailableLongitudinalForceNewtons - negative.AvailableLongitudinalForceNewtons,
            0f, .00021f);
    }

    [Fact]
    public void ActualDriveCorrectionBoundaryHasContinuousCapacityAndSectorTime()
    {
        var controls = new float[11];
        var original = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(controls, .5f);
        var constraint = original.TurningDemandConstraints.Single();
        var endDistance = original.Path!.DistanceAtProgress(original.CombinedGripIntervals[0].EndProgress);
        var deceleration = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
            RiderSkills.Balanced, CalibrationScenarioCatalog.Baseline.Surface);
        var target = (float)Math.Sqrt((double)constraint.SettledCapabilityMetersPerSecond
            * constraint.SettledCapabilityMetersPerSecond + 2d * deceleration * (constraint.DistanceMeters - endDistance));
        var below = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(controls, .5f,
            target - .00005f);
        var above = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(controls, .5f,
            target + .00005f);
        Assert.True(below.IsValid && above.IsValid);
        Assert.False(below.CombinedGripIntervals[0].ControllerLimited);
        Assert.True(above.CombinedGripIntervals[0].ControllerLimited);
        Assert.InRange(MathF.Abs(above.CombinedGripIntervals[0].Grip.LongitudinalCapacityNewtons
            - below.CombinedGripIntervals[0].Grip.LongitudinalCapacityNewtons), 0f, .05f);
        Assert.InRange(MathF.Abs(above.SectorTimeSeconds - below.SectorTimeSeconds), 0f, .0001f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(.25f)]
    [InlineData(.5f)]
    [InlineData(.75f)]
    [InlineData(1f)]
    public void AllWinnerIntervalsHaveStateDerivedCapacityIncludingCorrection(float coupling)
    {
        var result = Result.Value;
        var scenario = result.Scenarios.Single(s => s.Coupling == coupling);
        var e = scenario.Winner.Evaluation;
        Assert.True(e.IsValid);
        var check = CombinedGripDiagnostics.Check(e, coupling, result.LateralAccelerationCapacityMetersPerSecondSquared);
        Assert.Equal(0, check.MissingCapacityIntervals);
        Assert.Equal(0f, check.MaximumCapacityResidualNewtons);
        Assert.Equal(0f, check.MaximumTransitionCapacityResidualNewtons);
        Assert.Contains(e.CombinedGripIntervals, x => x.CorrectionDistanceMeters > 0f);
        Assert.Contains(e.CombinedGripIntervals, x => x.ControllerState == "Drive");
        Assert.All(e.CombinedGripIntervals, x =>
        {
            if (x.ControllerLimited)
            {
                Assert.True(x.Grip.RequestedLongitudinalForceNewtons <= 0f);
                Assert.Equal(x.Grip.RequestedLongitudinalForceNewtons, x.Grip.AvailableLongitudinalForceNewtons);
                Assert.Equal(0f, x.Grip.ClippedDriveForceNewtons);
            }
        });
        var search = scenario.Search;
        Assert.Equal(106, search.StartsGenerated);
        Assert.Equal(48, search.RefinedStartCount);
        Assert.Equal(search.CandidatesEvaluated, search.ValidCandidates + search.InvalidTrackBoundaryCandidates
            + search.InvalidSelfIntersectionCandidates + search.InvalidNonSmoothGeometryCandidates
            + search.InvalidLateralExecutionCandidates + search.InvalidCornerControlDepartureCandidates
            + search.InvalidCrashCandidates + search.InvalidStraightRepositionCandidates + search.InvalidNonTraversableCandidates);
        Assert.Equal(5, scenario.ConstantLines.Count);
    }

    [Fact]
    public void DisabledCouplingExactlyReplaysBaselineIncludingIndependentOptimizer()
    {
        Assert.True(Result.Value.ZeroBaselineExact);
        foreach (var controls in new[] { new float[11],
            FreeContinuousRacingTrajectoryGeometryExperiment.ReviewedTurningCostWinnerControls.ToArray() })
        {
            var old = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(controls, 0f);
            var zero = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(controls, 0f);
            Assert.Equal(old.Validity, zero.Validity);
            Assert.Equal(old.CornerTimeSeconds, zero.CornerTimeSeconds);
            Assert.Equal(old.SectorTimeSeconds, zero.SectorTimeSeconds);
            Assert.Equal(old.ExitSpeedMetersPerSecond, zero.ExitSpeedMetersPerSecond);
            Assert.Equal(old.CorrectionDistanceMeters, zero.CorrectionDistanceMeters);
            Assert.Equal(old.CorrectionTimeSeconds, zero.CorrectionTimeSeconds);
            Assert.Equal(old.Profile, zero.Profile);
        }
    }

    [Fact]
    public void SmallCouplingApproachesDirectBaselineWithoutThresholdOrCorrectionPlateau()
    {
        var controls = FreeContinuousRacingTrajectoryGeometryExperiment.ReviewedTurningCostWinnerControls;
        var baseline = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(controls, 0f);
        var tiny = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(controls, 1e-8f);
        Assert.True(tiny.IsValid);
        Assert.InRange(MathF.Abs(tiny.SectorTimeSeconds - baseline.SectorTimeSeconds), 0f, 2f * Ulp(baseline.SectorTimeSeconds));
        Assert.InRange(MathF.Abs(tiny.ExitSpeedMetersPerSecond - baseline.ExitSpeedMetersPerSecond),
            0f, 2f * Ulp(baseline.ExitSpeedMetersPerSecond));
        Assert.Equal(baseline.CorrectionDistanceMeters, tiny.CorrectionDistanceMeters);
    }

    [Fact]
    public void PerturbationsAreCoherentPhysicalShiftsWithoutSearchOrClipping()
    {
        var result = Result.Value;
        Assert.True(result.PerturbationReference.IsNonConstant);
        Assert.Equal(13, result.Perturbations.Count);
        foreach (var probe in result.Perturbations)
        for (var i = 0; i < 11; i++)
            Assert.Equal(result.PerturbationReference.Candidate.ControlOffsetsMeters[i] + probe.ShiftMeters
                    * (probe.Kind == "Smooth exit fan" ? FreeContinuousRacingTrajectoryGeometryExperiment.ExitFanWeight(i) : 1f),
                probe.Evaluation.Candidate.ControlOffsetsMeters[i]);
        Assert.Equal(result.PerturbationReference.SectorTimeSeconds,
            result.Perturbations.Single(p => p.ShiftMeters == 0f).Evaluation.SectorTimeSeconds);
        var report = CombinedGripAvailabilityReport.Render(result);
        Assert.Equal(report, CombinedGripAvailabilityReport.Render(result));
        Assert.Contains("Required answers", report);
        Assert.DoesNotContain("NaN", report);
        Assert.DoesNotContain("Infinity", report);
    }

    [Theory]
    [InlineData(-.01f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void CouplingIsValidated(float coupling) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        CombinedGripAvailability.Evaluate(20f, .03f, 15f, 500f, 300f, coupling));

    [Fact]
    public void PhysicalInputsAreValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CombinedGripAvailability.Evaluate(-1f, .03f, 15f, 500f, 300f, .5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombinedGripAvailability.Evaluate(20f, float.NaN, 15f, 500f, 300f, .5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombinedGripAvailability.Evaluate(20f, .03f, 0f, 500f, 300f, .5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombinedGripAvailability.Evaluate(20f, .03f, 15f, -1f, 300f, .5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombinedGripAvailability.Evaluate(20f, .03f, 15f, 500f, float.NaN, .5f));
    }

    private static float Ulp(float value) => MathF.BitIncrement(value) - value;
}
