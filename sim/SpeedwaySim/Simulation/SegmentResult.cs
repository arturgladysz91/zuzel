using System.Collections.Generic;
using SpeedwaySim.Models;

namespace SpeedwaySim.Simulation;

public sealed class SegmentResult
{
    public SegmentResult(TrackSegment segment, double timeSeconds, RiderState resultingState, IReadOnlyList<SimulationEvent> events)
    {
        Segment = segment;
        TimeSeconds = timeSeconds;
        ResultingState = resultingState;
        Events = events;
    }

    public TrackSegment Segment { get; }

    public double TimeSeconds { get; }

    public RiderState ResultingState { get; }

    public IReadOnlyList<SimulationEvent> Events { get; }
}
