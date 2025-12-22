using CoreSim;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class TrackSurfaceStateTests
{
    private sealed class FixedDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane, 0f);
    }

    [Fact]
    public void TrackState_DefaultGridHasExpectedSizeAndValues()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.Straight), new(1, SegmentType.TurnEntry) });
        var trackState = TrackState.CreateDefault(track);

        Assert.Equal(2, trackState.SegmentCount);
        Assert.Equal(TrackSegment.LanesCount, trackState.LinesCount);

        var surface = trackState.GetSurface(0, 0);
        Assert.Equal(1.0f, surface.Grip);
        Assert.Equal(0.0f, surface.Ruts);
        Assert.Equal(0.5f, surface.Moisture);
    }

    [Fact]
    public void ApplySurfaceDelta_ClampsValuesToZeroOneRange()
    {
        var trackState = new TrackState(1, 1, new TrackSurfaceState(0.9f, 0.1f, 0.5f));
        var log = new SimLog();

        trackState.ApplySurfaceDelta(0, 0, 1.0f, -2.0f, 0.8f, "test", 1, 0, log);
        var surface = trackState.GetSurface(0, 0);

        Assert.Equal(1.0f, surface.Grip);
        Assert.Equal(0.0f, surface.Ruts);
        Assert.Equal(1.0f, surface.Moisture);
    }

    [Fact]
    public void PassingRiderUpdatesSurfaceDeterministically()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.TurnEntry) });
        var trackState = TrackState.CreateDefault(track);
        var riders = new List<RiderState> { RiderState.CreateDefault(1, 0) };

        var sim = new HeatSimulator(new FixedDecisionModel());
        var log = sim.Simulate(track, trackState, riders, heatId: 10);

        var surface = trackState.GetSurface(0, 0);
        Assert.Equal(0.02f, surface.Ruts, 3);
        Assert.Equal(0.995f, surface.Grip, 3);
        Assert.Single(log.SurfaceChanges);
        Assert.Equal("pass", log.SurfaceChanges[0].Reason);
    }

    [Fact]
    public void WorseGripOrRutsIncreaseRiskInTurns()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.TurnEntry) });
        var riders = new List<RiderState> { RiderState.CreateDefault(1, 0) };
        var sim = new HeatSimulator(new FixedDecisionModel());

        var goodState = new TrackState(1, TrackSegment.LanesCount, new TrackSurfaceState(1.0f, 0.0f, 0.5f));
        sim.Simulate(track, goodState, riders);
        var goodRisk = riders[0].Risk;

        riders[0] = RiderState.CreateDefault(1, 0);
        var badState = new TrackState(1, TrackSegment.LanesCount, new TrackSurfaceState(0.5f, 0.8f, 0.5f));
        sim.Simulate(track, badState, riders);
        var badRisk = riders[0].Risk;

        Assert.True(badRisk > goodRisk);
    }

    [Fact]
    public void StraightSurfaceDoesNotChangeRisk()
    {
        var track = new Track(new List<TrackSegment> { new(0, SegmentType.Straight) });
        var riders = new List<RiderState> { RiderState.CreateDefault(1, 0) };
        var sim = new HeatSimulator(new FixedDecisionModel());

        var cleanState = new TrackState(1, TrackSegment.LanesCount, new TrackSurfaceState(1.0f, 0.0f, 0.5f));
        sim.Simulate(track, cleanState, riders);
        var cleanRisk = riders[0].Risk;

        riders[0] = RiderState.CreateDefault(1, 0);
        var badState = new TrackState(1, TrackSegment.LanesCount, new TrackSurfaceState(0.2f, 1.0f, 1.0f));
        sim.Simulate(track, badState, riders);
        var badRisk = riders[0].Risk;

        Assert.Equal(cleanRisk, badRisk);
    }

    [Fact]
    public void SimulationIsDeterministicWithSameInputs()
    {
        var track = new Track(new List<TrackSegment>
        {
            new(0, SegmentType.TurnEntry),
            new(1, SegmentType.Straight)
        });
        var ridersA = new List<RiderState> { RiderState.CreateDefault(1, 0) };
        var ridersB = new List<RiderState> { RiderState.CreateDefault(1, 0) };
        var sim = new HeatSimulator(new FixedDecisionModel());

        var stateA = TrackState.CreateDefault(track);
        var stateB = TrackState.CreateDefault(track);

        var logA = sim.Simulate(track, stateA, ridersA, heatId: 5);
        var logB = sim.Simulate(track, stateB, ridersB, heatId: 5);

        Assert.Equal(logA.Lines, logB.Lines);
        Assert.Equal(logA.SurfaceChanges, logB.SurfaceChanges);

        for (var s = 0; s < track.Segments.Count; s++)
        for (var l = 0; l < TrackSegment.LanesCount; l++)
        {
            var a = stateA.GetSurface(s, l);
            var b = stateB.GetSurface(s, l);
            Assert.Equal(a, b);
        }
    }
}
