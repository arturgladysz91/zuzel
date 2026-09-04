// Pojedynczy segment toru (wejście łuku, środek, wyjście, prosta) z lokalnymi liniami jazdy.
// Pojedynczy segment toru będący miejscem decyzji zawodnika.
namespace CoreSim;

public sealed class TrackSegment
{
    public int Id { get; }
    public SegmentType Type { get; }
    public float? StraightLengthMetersOverride { get; }
    public bool IsStandingStartSegment { get; }

    public const int LanesCount = LaneModel.LanesCount;

    public TrackSegment(
        int id,
        SegmentType type,
        float? straightLengthMetersOverride = null,
        bool isStandingStartSegment = false)
    {
        if (straightLengthMetersOverride is { } length)
        {
            if (type != SegmentType.Straight)
                throw new ArgumentException("Only a Straight may override its length.", nameof(straightLengthMetersOverride));
            if (!float.IsFinite(length) || length <= 0f)
                throw new ArgumentOutOfRangeException(nameof(straightLengthMetersOverride));
        }
        if (isStandingStartSegment && type != SegmentType.Straight)
            throw new ArgumentException("A standing-start segment must be a Straight.", nameof(isStandingStartSegment));

        Id = id;
        Type = type;
        StraightLengthMetersOverride = straightLengthMetersOverride;
        IsStandingStartSegment = isStandingStartSegment;
    }
}
