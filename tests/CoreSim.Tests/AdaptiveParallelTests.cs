using System.Text.Json;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "trajectory")]
public sealed class AdaptiveParallelTests
{
    [Fact]
    public void FractionalProgressCompletesWithExactSerialParallelDecisions()
    {
        var source = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "B");
        var rider = (source.Riders[0] with { SegmentProgress = .05f }).Create(source.Track);
        var context = TrajectoryEvaluatorTests.Context(source.Track, rider, source.CreateSurface());
        var serial = new AdaptiveDecisionModel { MaxDegreeOfParallelism = 1 }.Evaluate(context);
        var parallel = new AdaptiveDecisionModel { MaxDegreeOfParallelism = 4 }.Evaluate(context);
        Assert.Equal(JsonSerializer.Serialize(serial), JsonSerializer.Serialize(parallel));
        Assert.True(serial.ProductionResolutions > 0);
    }
    [Fact]
    public void AllAuditedCandidatesAndUniquePrefixCountsMatchDegreeOneExactly()
    {
        foreach (var source in FourRiderBehaviorSuite.CreateScenarios().Where(s =>
            new[] { "B", "C", "H", "F", "G", "J-tight", "J-wide" }.Contains(s.Id)))
        foreach (var seed in new[] { 0, 17 })
        foreach (var modelSeed in new[] { 1234, 37 })
        {
            var rider = source.Riders.Single(r => r.Id == 2).Create(source.Track);
            var context = TrajectoryEvaluatorTests.Context(source.Track, rider, source.CreateSurface(), seed: seed);
            var serial = new AdaptiveDecisionModel(modelSeed) { MaxDegreeOfParallelism = 1 }.Evaluate(context);
            var parallelModel = new AdaptiveDecisionModel(modelSeed) { MaxDegreeOfParallelism = 4 };
            var parallel = parallelModel.Evaluate(context);
            Assert.Equal(JsonSerializer.Serialize(serial), JsonSerializer.Serialize(parallel));
            Assert.Equal(serial.Decision, parallelModel.Decide(context));
            Assert.Equal(serial.CandidateTraversals, parallel.CandidateTraversals);
            Assert.Equal(serial.ProductionResolutions, parallel.ProductionResolutions);
            Assert.True(parallel.ProductionResolutions > 0);
        }
    }

    [Theory]
    [InlineData(0, 0f)] [InlineData(17, 0f)] [InlineData(0, 1f)] [InlineData(17, 1f)]
    public void CompleteHeatClassificationFinalRidersAndRawSurfaceBitsMatch(int seed, float incidents)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        (HeatResult Heat, SimulationSnapshot Final) Run(int degree)
        {
            var riders = Enumerable.Range(1, 4).Select(id => new RiderState(RiderProfile.CreateDefault(id),
                (StartingGate)(id - 1), track)).ToList();
            var state = TrackState.CreateDefault(track);
            var model = new AdaptiveDecisionModel { MaxDegreeOfParallelism = degree };
            var heat = new HeatSimulator(model).SimulateHeat(track, state, riders,
                new HeatSimulationOptions { Seed = seed, Laps = 4, IncidentFrequency = incidents, EnableLogging = false });
            return (heat, new SimulationEngine(model).CaptureSnapshot(track, state, riders, new(1, 36, 4, 0, seed, 4)));
        }
        var serial = Run(1); var parallel = Run(4);
        Assert.Equal(serial.Heat.Classification, parallel.Heat.Classification);
        Assert.Equal(serial.Final.Riders, parallel.Final.Riders);
        for (var segment = 0; segment < track.Segments.Count; segment++)
        for (var lane = 0; lane < 5; lane++)
        {
            var a = serial.Final.TrackState.GetSurface(segment, lane);
            var b = parallel.Final.TrackState.GetSurface(segment, lane);
            Assert.Equal(BitConverter.SingleToInt32Bits(a.Grip), BitConverter.SingleToInt32Bits(b.Grip));
            Assert.Equal(BitConverter.SingleToInt32Bits(a.Ruts), BitConverter.SingleToInt32Bits(b.Ruts));
            Assert.Equal(BitConverter.SingleToInt32Bits(a.Moisture), BitConverter.SingleToInt32Bits(b.Moisture));
        }
    }
}
