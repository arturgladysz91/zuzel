namespace SpeedwaySim.Models;

public sealed class TrackSegment
{
    public TrackSegment(string id, TrackSegmentType type, double length)
    {
        Id = id;
        Type = type;
        Length = length;
    }

    public string Id { get; }

    public TrackSegmentType Type { get; }

    /// <summary>
    /// Długość segmentu w metrach.
    /// </summary>
    public double Length { get; }
}
