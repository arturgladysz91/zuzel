// Pojedynczy segment toru (wejście łuku, środek, wyjście, prosta) z lokalnymi liniami jazdy.
// Pojedynczy segment toru będący miejscem decyzji zawodnika.
namespace CoreSim;

public sealed class TrackSegment
{
    public int Id { get; }
    public SegmentType Type { get; }

    public const int LanesCount = 5;

    public TrackSegment(int id, SegmentType type)
    {
        Id = id;
        Type = type;
    }
}
