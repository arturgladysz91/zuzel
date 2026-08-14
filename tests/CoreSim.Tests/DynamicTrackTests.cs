using CoreSim;
using CoreSim.Logging;
using Xunit;

namespace CoreSim.Tests;

public sealed class DynamicTrackTests
{
    [Fact]
    public void RainChangesLanesNonUniformly()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var state = TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, 0.3f));
        var log = new SimLog();
        var weather = new WeatherState(WeatherCondition.LightRain, 0.4f, 0f);

        TrackEvolution.ApplyWeather(track, state, weather, heatId: 1, tick: 0, log);

        Assert.True(state.GetSurface(0, LaneModel.MaxLane).Moisture
                    > state.GetSurface(0, LaneModel.MinLane).Moisture);
        Assert.Equal(LaneModel.LanesCount, log.SurfaceChanges.Count);
    }

    [Fact]
    public void GradingRemovesRutsOnlyFromSelectedArea()
    {
        var track = new Track(new[]
        {
            new TrackSegment(0, SegmentType.TurnEntry),
            new TrackSegment(1, SegmentType.TurnMiddle),
        });
        var state = TrackState.CreateDefault(track, new TrackSurfaceState(0.9f, 0.6f, 0.4f));
        var log = new SimLog();

        TrackEvolution.ApplyTrackWork(
            track,
            state,
            new TrackWorkAction(TrackWorkType.Grade, 1f, new[] { 1 }, new[] { 0, 1 }),
            heatId: 2,
            tick: 10,
            log);

        Assert.Equal(0.6f, state.GetSurface(0, 0).Ruts, 3);
        Assert.Equal(0.3f, state.GetSurface(1, 0).Ruts, 3);
        Assert.Equal(0.6f, state.GetSurface(1, 4).Ruts, 3);
    }

    [Fact]
    public void WetRuttedSurfaceLowersSafeCornerSpeed()
    {
        var good = new TrackSurfaceState(1f, 0f, 0.35f);
        var bad = new TrackSurfaceState(0.65f, 0.7f, 0.9f);
        var skills = RiderSkills.Balanced;

        var goodSpeed = SegmentPhysics.MaxSafeTurnSpeed(2, good, skills, 0.5f, CoreSim.Setup.BikeSetup.Neutral);
        var badSpeed = SegmentPhysics.MaxSafeTurnSpeed(2, bad, skills, 0.5f, CoreSim.Setup.BikeSetup.Neutral);

        Assert.True(badSpeed < goodSpeed);
    }
}
