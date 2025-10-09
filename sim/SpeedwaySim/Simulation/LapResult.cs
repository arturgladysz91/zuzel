using System.Collections.Generic;
using SpeedwaySim.Models;

namespace SpeedwaySim.Simulation;

public sealed class LapResult
{
    public LapResult(double lapTimeSeconds, RiderState finalState, IReadOnlyList<SegmentResult> segments, IReadOnlyList<TelemetryPoint> telemetry)
    {
        LapTimeSeconds = lapTimeSeconds;
        FinalState = finalState;
        Segments = segments;
        Telemetry = telemetry;
    }

    public double LapTimeSeconds { get; }

    public RiderState FinalState { get; }

    public IReadOnlyList<SegmentResult> Segments { get; }

    public IReadOnlyList<TelemetryPoint> Telemetry { get; }
}
