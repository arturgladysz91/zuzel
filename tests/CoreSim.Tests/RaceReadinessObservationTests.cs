using RaceReadiness;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class RaceReadinessObservationTests
{
    private static OrderAudit Observe(params ProgressPiece[] pieces) => OrderEvidence.Analyze(pieces, 0);

    [Fact]
    public void StrictLeadReversalHasBracketAndCorrectRider()
    {
        var result = Observe(new(1, 0, 2, 1, 2, 0), new(2, 0, 2, 0, 3, 0));
        var pass = Assert.Single(result.Passes);
        Assert.Equal(2, pass.Ahead); Assert.Equal(1, pass.Behind);
        Assert.Equal(0, pass.BracketStart); Assert.Equal(2, pass.BracketEnd);
        var boundary = OrderEvidence.Analyze([new(1, 0, 2, 1, 2, 0), new(2, 0, 2, 0, 3, 0)], 2);
        Assert.Equal("first-bend boundary", Assert.Single(boundary.Passes).Phase);
    }

    [Fact]
    public void EqualSegmentStepsAtDifferentClocksDoNotManufacturePass()
    {
        // At step end rider 2 is farther, but its clock is five seconds later.
        var result = Observe(new(1, 0, 10, 0, 10, 0), new(2, 5, 15, 0, 11, 0));
        Assert.Empty(result.Passes);
    }

    [Fact]
    public void ContinuousTieCanBridgeStrictLeadSigns()
    {
        var result = Observe(new(1, 0, 1, 2, 3, 0), new(1, 1, 2, 3, 4, 0),
            new(2, 0, 1, 1, 3, 0), new(2, 1, 2, 3, 5, 0));
        Assert.Equal(2, Assert.Single(result.Passes).Ahead);
    }

    [Fact]
    public void ExactTiesNeverReceiveRiderIdOrder()
    {
        var result = Observe(new(12, 0, 1, 2, 3, 0), new(4, 0, 1, 2, 3, 0));
        Assert.Empty(result.Passes); Assert.Equal(1, result.TiedIntervals);
    }

    [Fact]
    public void MissingCoverageCannotBridgeLeadChange()
    {
        var result = Observe(new(1, 0, 1, 2, 3, 0), new(1, 2, 3, 4, 5, 0),
            new(2, 0, 1, 1, 2, 0), new(2, 2, 3, 6, 7, 0));
        Assert.Empty(result.Passes); Assert.Equal(1, result.CoverageBreaks);
    }

    [Fact]
    public void ZeroTimeProgressJumpCannotBecomePass()
    {
        var result = Observe(new(1, 0, 1, 2, 3, 0), new(1, 1, 2, 3, 4, 0),
            new(2, 0, 1, 1, 2, 0), new(2, 1, 2, 4, 5, 0));
        Assert.Empty(result.Passes); Assert.Equal(1, result.CoverageBreaks);
    }

    [Fact]
    public void FinishedOrRetiredRiderHasNoInventedContinuation()
    {
        var result = Observe(new(1, 0, 1, 2, 3, 0), new(2, 0, 3, 1, 4, 0));
        Assert.Empty(result.Passes);
    }

    [Fact]
    public void UnwrappedLapProgressAndInputOrderHaveSameEvidence()
    {
        ProgressPiece[] pieces = [new(1, 0, 2, 7.9, 8.5, 7), new(2, 0, 2, 7.8, 8.6, 7)];
        Assert.Equal(Observe(pieces).Passes, Observe(pieces.Reverse().ToArray()).Passes);
        Assert.Single(Observe(pieces).Passes);
    }

    [Fact]
    public void TypedHashPreservesIEEEBitsAndNumericType()
    {
        Assert.NotEqual(Exact.Hash(new { Value = 0f }), Exact.Hash(new { Value = -0f }));
        Assert.NotEqual(Exact.Hash(new { Value = 1f }), Exact.Hash(new { Value = 1d }));
        Assert.NotEqual(Exact.Hash(new { Value = 1f }), Exact.Hash(new { Value = MathF.BitIncrement(1f) }));
    }

    [Fact]
    public void ReusingHeatSimulatorAfterContactStressDoesNotLeakOwnershipOrRecovery()
    {
        var options = new HeatSimulationOptions { Seed = 19, EnableContestedSpaceResponses = true,
            EnablePhysicalContactConsequences = true };
        var simulator = new HeatSimulator(new AdaptiveDecisionModel());
        var stress = PhysicalContactConsequenceEvidence.HeatScenarios().Single(s => s.Id == "contact-heavy");
        Capture(simulator, stress, 90);
        var ordinary = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "A");
        var reused = Capture(simulator, ordinary, 91);
        var fresh = Capture(new HeatSimulator(new AdaptiveDecisionModel()), ordinary, 91);
        Assert.Equal(fresh, reused);

        string Capture(HeatSimulator heatSimulator, BehaviorScenario scenario, int heatId)
        {
            var surface = scenario.CreateSurface();
            var riders = scenario.Riders.Select(r => r.Create(scenario.Track)).ToList();
            var result = heatSimulator.SimulateHeat(scenario.Track, surface, riders, options, heatId);
            return Exact.Hash(new { result.Classification, result.Log, Riders = riders.ToArray(), Surface = surface.Snapshot() });
        }
    }
}
