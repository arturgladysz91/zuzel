using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class FullHeatSimulationTests
{
    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane);
    }

    [Fact]
    public void FourLapHeatIsDeterministicForSameSeedAndInputs()
    {
        var track = Track.CreateExample();
        var options = new HeatSimulationOptions
        {
            Laps = 4,
            Seed = 77,
            Weather = WeatherState.LightRain,
            IncidentFrequency = 0f,
        };

        var ridersA = CreateRiders();
        var ridersB = CreateRiders();
        var stateA = TrackState.CreateDefault(track);
        var stateB = TrackState.CreateDefault(track);
        var simulator = new HeatSimulator(new HoldLaneDecisionModel());

        var resultA = simulator.SimulateHeat(track, stateA, ridersA, options, heatId: 5);
        var resultB = simulator.SimulateHeat(track, stateB, ridersB, options, heatId: 5);

        Assert.Equal(resultA.Classification, resultB.Classification);
        Assert.Equal(resultA.Log.Lines, resultB.Log.Lines);
        Assert.Equal(resultA.Log.SurfaceChanges, resultB.Log.SurfaceChanges);
        Assert.Equal(resultA.Log.Overtakes, resultB.Log.Overtakes);
        Assert.Equal(4, resultA.Log.OrderSnapshots.Count);
        Assert.All(resultA.Log.OrderSnapshots, snapshot => Assert.Equal(4, snapshot.Order.Count));
        Assert.All(resultA.Classification, result => Assert.Equal(4, result.LapsCompleted));
        Assert.Equal(new[] { 3, 2, 1, 0 }, resultA.Classification.Select(result => result.Points));
        Assert.Equal(1, resultA.Classification[0].RiderId);
    }

    private static List<RiderState> CreateRiders()
        => new()
        {
            new RiderState(1, 0),
            new RiderState(2, 1),
            new RiderState(3, 2),
            new RiderState(4, 3),
        };
}
