using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using System.Globalization;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class SegmentLoggingTests
{
    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane, 0f);
    }

    private sealed class TargetLaneDecisionModel(int targetLane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(targetLane, 0f);
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
        var maxSafeSpeed = CornerTestSupport.SingleEnvelopeSpeed(
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
        Assert.Contains($"v_physics={entrySpeed.ToString("F2", CultureInfo.InvariantCulture)}", segmentLog);
        Assert.Contains($"v_out={rider.Speed.ToString("F2", CultureInfo.InvariantCulture)}", segmentLog);
        Assert.True(rider.Speed < entrySpeed);
        Assert.True(rider.Speed > 0f);
    }

    [Fact]
    public void SegmentLogDeterministicallyIncludesInvariantLateralTransition()
    {
        var geometry = new TrackGeometry(60f, 24f, 1f, 0.20f);
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnMiddle) }, geometry);
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 19,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };

        (HeatResult Result, RiderState Rider) Run()
        {
            var rider = new RiderState(9, lane: 0) { Speed = 10f };
            var result = new HeatSimulator(new TargetLaneDecisionModel(1)).SimulateHeat(
                track,
                TrackState.CreateDefault(track, surface),
                new List<RiderState> { rider },
                options);
            return (result, rider);
        }

        var first = Run();
        var second = Run();
        var expectedTransition = $"lateral=0.000->{first.Rider.LateralPosition.ToString("F3", CultureInfo.InvariantCulture)}";

        Assert.Equal(first.Result.Log.Lines, second.Result.Log.Lines);
        Assert.Contains(expectedTransition, Assert.Single(first.Result.Log.Lines, line => line.Contains("lateral=")));
    }
}
