using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using System.Globalization;
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
        var rider = new RiderState(
            new RiderProfile(
                7,
                "Fast exit rider",
                new RiderSkills(50f, 100f, 50f, 50f, 50f, 50f),
                RiderStyle.Balanced),
            lane: 1);
        var maxSafeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            1,
            track.Geometry,
            TrackSurfaceState.Default,
            rider.Profile.Skills,
            rider.ActiveSetup);
        var entrySpeed = maxSafeSpeed * 1.05f;
        rider.Speed = entrySpeed;
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

        var segmentLog = Assert.Single(result.Log.Lines, line => line.Contains("outcome=Brake"));
        Assert.Contains($"v_in={entrySpeed.ToString("F2", CultureInfo.InvariantCulture)}", segmentLog);
        Assert.Contains($"v_physics={maxSafeSpeed.ToString("F2", CultureInfo.InvariantCulture)}", segmentLog);
        Assert.Contains($"v_out={rider.Speed.ToString("F2", CultureInfo.InvariantCulture)}", segmentLog);
        Assert.True(rider.Speed > maxSafeSpeed);
    }
}
