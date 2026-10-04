using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "historical-analysis")]
public sealed class CombinedGripExecutionReserveExperimentTests
{
    private static readonly Lazy<CombinedGripReserveResult> Result =
        new(() => FreeContinuousRacingTrajectoryGeometryExperiment.RunCombinedGripReserveExperiment());

    [Theory]
    [InlineData(-.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ReserveRejectsInvalidInputs(float reserve) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(new float[11], .5f,
            lateralExecutionReserveMeters: reserve));

    [Fact]
    public void ReserveIsAValidityGateAndDoesNotAlterPhysicalReplayOrActualMargin()
    {
        var baseline = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(new float[11], .75f);
        var reserved = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(new float[11], .75f,
            lateralExecutionReserveMeters: .10f);
        Assert.True(baseline.IsValid && reserved.IsValid);
        Assert.Equal(baseline.Profile, reserved.Profile);
        Assert.Equal(baseline.CombinedGripIntervals, reserved.CombinedGripIntervals);
        Assert.Equal(baseline.SectorTimeSeconds, reserved.SectorTimeSeconds);
        Assert.Equal(baseline.MinimumLateralExecutionHeadroomMeters,
            reserved.MinimumUnreservedLateralExecutionHeadroomMeters);
        Assert.Equal(0f, reserved.MinimumLateralExecutionHeadroomMeters);
        // Existing clamp keeps stationary cross-track motion feasible. Report raw
        // margin honestly instead of pretending this guarantees 10 cm of headroom.
        Assert.InRange(reserved.MinimumUnreservedLateralExecutionHeadroomMeters, 0f, .099999f);
    }

    [Fact]
    public void ReserveCanRejectAFormerlyValidTrajectoryWithoutChangingTheModel()
    {
        var controls = new[] { .52211154f, .61745787f, .60453284f, .4882735f, .31308693f,
            .14588839f, .050542116f, .063467115f, .17972651f, .354913f, .58461154f };
        var original = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(controls, .5f);
        Assert.True(original.IsValid);
        var reserved = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(controls, .5f,
            lateralExecutionReserveMeters: .02f);
        Assert.Equal(FreeTrajectoryValidity.LateralExecutionConstraint, reserved.Validity);
        Assert.Equal(original.Candidate.ControlOffsetsMeters, reserved.Candidate.ControlOffsetsMeters);
    }

    [Fact]
    public void AllEightCasesUseTheUnchangedSearcherAndReserveOnlyChangesEligibility()
    {
        var result = Result.Value;
        Assert.Equal(8, result.Cases.Count);
        Assert.Equal(8, result.Cases.Select(c => (c.Coupling, c.RequiredReserveMeters)).Distinct().Count());
        foreach (var c in result.Cases)
        {
            Assert.True(c.Inner.IsValid && c.Winner.IsValid);
            Assert.Equal(106, c.Search.StartsGenerated);
            Assert.InRange(c.Search.RefinedStartCount, 1, 48);
            var replay = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateCombinedGrip(
                c.Winner.Candidate.ControlOffsetsMeters, c.Coupling);
            Assert.True(replay.IsValid);
            Assert.Equal(c.Winner.SectorTimeSeconds, replay.SectorTimeSeconds);
            Assert.Equal(c.Winner.Profile, replay.Profile);
            Assert.Equal(c.Winner.MinimumUnreservedLateralExecutionHeadroomMeters,
                replay.MinimumLateralExecutionHeadroomMeters);
            Assert.Equal(c.Winner.MinimumUnreservedLateralExecutionHeadroomMeters,
                MathF.Min(c.Winner.CombinedGripIntervals.Min(x => x.UnreservedLateralHeadroomMeters),
                    replay.StraightRepositionLateralHeadroomMeters));
            Assert.Equal(c.Search.CandidatesEvaluated, c.Search.ValidCandidates
                + c.Search.InvalidTrackBoundaryCandidates + c.Search.InvalidSelfIntersectionCandidates
                + c.Search.InvalidNonSmoothGeometryCandidates + c.Search.InvalidLateralExecutionCandidates
                + c.Search.InvalidCornerControlDepartureCandidates + c.Search.InvalidCrashCandidates
                + c.Search.InvalidStraightRepositionCandidates + c.Search.InvalidNonTraversableCandidates);
            Assert.Equal(0, CombinedGripDiagnostics.Check(c.Winner, c.Coupling,
                result.LateralCapacityMetersPerSecondSquared).MissingCapacityIntervals);
        }
        Assert.Equal(4, result.Perturbations.Count);
        foreach (var p in result.Perturbations)
        for (var i = 0; i < 11; i++)
            Assert.Equal(result.PerturbationCase.Winner.Candidate.ControlOffsetsMeters[i] + p.ShiftMeters
                * (p.Kind == "Smooth exit fan" ? FreeContinuousRacingTrajectoryGeometryExperiment.ExitFanWeight(i) : 1f),
                p.Evaluation.Candidate.ControlOffsetsMeters[i]);
        var report = CombinedGripExecutionReserveReport.Render(result);
        Assert.Equal(report, CombinedGripExecutionReserveReport.Render(result));
        Assert.DoesNotContain("NaN", report);
        Assert.DoesNotContain("Infinity", report);
        Assert.Contains("not a guarantee", report);
    }
}
