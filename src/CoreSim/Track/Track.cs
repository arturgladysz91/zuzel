// Model toru jako uporządkowana lista segmentów decyzyjnych używana przez symulację.
namespace CoreSim;

public sealed class Track
{
    public IReadOnlyList<TrackSegment> Segments { get; }
    public TrackGeometry Geometry { get; }
    public CornerTopology CornerTopology { get; }

    public Track(IReadOnlyList<TrackSegment> segments)
        : this(segments, TrackGeometry.Default)
    {
    }

    public Track(IReadOnlyList<TrackSegment> segments, TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(geometry);
        if (segments.Count == 0) throw new ArgumentException("Track must have at least one segment.", nameof(segments));
        Segments = Array.AsReadOnly(segments
            .Select(segment => new TrackSegment(segment.Id, segment.Type,
                segment.StraightLengthMetersOverride, segment.IsStandingStartSegment))
            .ToArray());
        if (Segments.Count(segment => segment.IsStandingStartSegment) > 1)
            throw new ArgumentException("Track may contain at most one standing-start segment.", nameof(segments));
        if (Segments.Any(segment => segment.IsStandingStartSegment)
            && (!Segments[0].IsStandingStartSegment || Segments[^1].Type != SegmentType.Straight))
            throw new ArgumentException("Standing start must be at index zero with a final Straight across the start/finish boundary.", nameof(segments));
        Geometry = geometry;
        CornerTopology = new CornerTopology(Segments);
    }

    // Compatibility layout: 2 łuki (wejście/środek/wyjście) + 2 proste.
    /// <summary>Synthetic 6 m / 6 m compatibility fixture, not regulatory-size geometry.</summary>
    public static Track CreateExample()
    {
        var geometry = new TrackGeometry(
            straightLengthMeters: 60.0f,
            innerRadiusMeters: 24.0f,
            straightWidthMeters: 6.0f,
            turnWidthMeters: 6.0f,
            turnSegmentAngleRadians: MathF.PI / 3.0f);

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

        return new Track(segs, geometry);
    }

    // A 70 m home straight crosses the canonical start/finish boundary.
    // Unlike the compatibility example, each half is 35 m; no virtual distance.
    public static Track CreateStandingStartExample()
        => new(new TrackSegment[]
        {
            new(0, SegmentType.Straight, 35f, isStandingStartSegment: true),
            new(1, SegmentType.TurnEntry),
            new(2, SegmentType.TurnMiddle),
            new(3, SegmentType.TurnExit),
            new(4, SegmentType.Straight),
            new(5, SegmentType.TurnEntry),
            new(6, SegmentType.TurnMiddle),
            new(7, SegmentType.TurnExit),
            new(8, SegmentType.Straight, 35f),
        }, new TrackGeometry(60f, 24f, 10f, 14f, MathF.PI / 3f));
}
