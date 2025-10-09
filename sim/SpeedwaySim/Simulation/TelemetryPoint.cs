using SpeedwaySim.Models;

namespace SpeedwaySim.Simulation;

public sealed class TelemetryPoint
{
    public TelemetryPoint(string segmentId, TrackSegmentType segmentType, double distanceStart, double distanceEnd, double speedIn, double speedOut, int lineIn, int lineOut)
    {
        SegmentId = segmentId;
        SegmentType = segmentType;
        DistanceStart = distanceStart;
        DistanceEnd = distanceEnd;
        SpeedIn = speedIn;
        SpeedOut = speedOut;
        LineIn = lineIn;
        LineOut = lineOut;
    }

    public string SegmentId { get; }

    public TrackSegmentType SegmentType { get; }

    public double DistanceStart { get; }

    public double DistanceEnd { get; }

    public double SpeedIn { get; }

    public double SpeedOut { get; }

    public int LineIn { get; }

    public int LineOut { get; }
}
