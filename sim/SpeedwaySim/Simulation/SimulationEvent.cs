using SpeedwaySim.Models;

namespace SpeedwaySim.Simulation;

public sealed class SimulationEvent
{
    public SimulationEvent(SimulationEventType type, string segmentId, int initialLine, int resultingLine, double magnitude)
    {
        Type = type;
        SegmentId = segmentId;
        InitialLine = initialLine;
        ResultingLine = resultingLine;
        Magnitude = magnitude;
    }

    public SimulationEventType Type { get; }

    public string SegmentId { get; }

    public int InitialLine { get; }

    public int ResultingLine { get; }

    /// <summary>
    /// Intensywność zdarzenia (np. wartość dryfu w m/s).
    /// </summary>
    public double Magnitude { get; }
}
