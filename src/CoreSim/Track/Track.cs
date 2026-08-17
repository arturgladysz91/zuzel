// Model toru jako uporządkowana lista segmentów decyzyjnych używana przez symulację.
namespace CoreSim;

public sealed class Track
{
    public IReadOnlyList<TrackSegment> Segments { get; }

    public Track(IReadOnlyList<TrackSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0) throw new ArgumentException("Track must have at least one segment.", nameof(segments));
        Segments = Array.AsReadOnly(segments
            .Select(segment => new TrackSegment(segment.Id, segment.Type))
            .ToArray());
    }

    // Przykładowy tor do uruchomienia Sandbox: 2 łuki (wejście/środek/wyjście) + 2 proste.
    public static Track CreateExample()
    {
        var segs = new List<TrackSegment>
        {
            new(0, SegmentType.TurnEntry),
            new(1, SegmentType.TurnMiddle),
            new(2, SegmentType.TurnExit),
            new(3, SegmentType.Straight),

            new(4, SegmentType.TurnEntry),
            new(5, SegmentType.TurnMiddle),
            new(6, SegmentType.TurnExit),
            new(7, SegmentType.Straight),
        };

        return new Track(segs);
    }
}
