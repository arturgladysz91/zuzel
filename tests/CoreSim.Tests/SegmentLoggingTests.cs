using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class SegmentLoggingTests
{
    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane, 0f);
    }

    [Fact]
    public void SpeedLogSeparatesPhysicsResolutionFromFinalExitSpeed()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnExit) });
        var rider = RiderState.CreateDefault(7, 1);
        rider.Speed = SegmentPhysics.MaxSafeTurnSpeed(
            1,
            TrackSurfaceState.Default,
            rider.Profile.Skills,
            rider.Morale,
            rider.ActiveSetup) * 1.05f;
        var simulator = new HeatSimulator(new HoldLaneDecisionModel());

        var result = simulator.SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { rider },
            new HeatSimulationOptions
            {
                Laps = 1,
                Seed = 7,
                Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
                IncidentFrequency = 0f,
            });

        Assert.Contains("outcome=Brake", result.Log.Lines[0]);
        Assert.Contains("v_in=", result.Log.Lines[0]);
        Assert.Contains("v_physics=", result.Log.Lines[0]);
        Assert.Contains("v_out=", result.Log.Lines[0]);
    }
}
