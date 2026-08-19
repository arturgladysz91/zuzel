using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class RaceBalanceTests
{
    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane);
    }

    [Fact]
    public void ExtremeLaneTimeComparisonIsAnExampleTrackDiagnostic()
    {
        var track = Track.CreateExample();
        var innerDistance = track.Segments.Sum(segment => LaneModel.SegmentLengthMeters(
            segment,
            LaneModel.MinLane,
            track.Geometry));
        var outerDistance = track.Segments.Sum(segment => LaneModel.SegmentLengthMeters(
            segment,
            LaneModel.MaxLane,
            track.Geometry));
        var innerTime = innerDistance / SegmentPhysics.MaxSafeTurnSpeed(LaneModel.MinLane);
        var outerTime = outerDistance / SegmentPhysics.MaxSafeTurnSpeed(LaneModel.MaxLane);

        Assert.True(outerDistance > innerDistance);
        Assert.True(float.IsFinite(innerTime));
        Assert.True(float.IsFinite(outerTime));
        Assert.True(outerTime / innerTime > 0f);
    }

    [Fact]
    public void StrongRiderCanOvercomeOneLaneOfGeometricSpeedDifference()
    {
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var strong = new RiderSkills(70f, 90f, 90f, 70f, 70f, 70f);
        var weak = new RiderSkills(70f, 30f, 30f, 70f, 70f, 70f);

        var strongSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            1, surface, strong, 0.5f, CoreSim.Setup.BikeSetup.Neutral);
        var weakSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            2, surface, weak, 0.5f, CoreSim.Setup.BikeSetup.Neutral);

        Assert.True(strongSpeed > weakSpeed);
    }

    [Fact]
    public void RiderSkillProducesLowerHeatTimeFromTheSameLane()
    {
        var track = Track.CreateExample();
        var strong = CreateRider(1, speed: 90f, control: 85f);
        var weak = CreateRider(2, speed: 35f, control: 40f);
        var options = new HeatSimulationOptions
        {
            Laps = 4,
            Seed = 50,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };

        var simulator = new HeatSimulator(new HoldLaneDecisionModel());
        var strongResult = simulator.SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { strong },
            options);
        var weakResult = simulator.SimulateHeat(
            track,
            TrackState.CreateDefault(track),
            new List<RiderState> { weak },
            options);

        Assert.True(strongResult.Classification[0].TimeSeconds
                    < weakResult.Classification[0].TimeSeconds * 0.94f);
    }

    private static RiderState CreateRider(int id, float speed, float control)
        => new(
            new RiderProfile(
                id,
                $"Rider {id}",
                new RiderSkills(70f, speed, control, 70f, 70f, 70f),
                RiderStyle.Balanced),
            lane: 1);
}
