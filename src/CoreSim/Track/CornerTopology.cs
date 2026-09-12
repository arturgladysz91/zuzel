using System.Collections.ObjectModel;

namespace CoreSim;

/// <summary>
/// One maximal, non-wrapping run of turn segments in track-topology order.
/// Segment labels remain compatibility metadata; membership is derived from
/// whether a segment is a turn.
/// </summary>
public sealed class LogicalCorner
{
    public int CornerId { get; }
    public int StartSegmentIndex { get; }
    public int EndSegmentIndex { get; }
    public int SegmentCount => EndSegmentIndex - StartSegmentIndex + 1;

    internal LogicalCorner(int cornerId, int startSegmentIndex, int endSegmentIndex)
    {
        if (cornerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(cornerId));
        if (startSegmentIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startSegmentIndex));
        if (endSegmentIndex < startSegmentIndex)
            throw new ArgumentOutOfRangeException(nameof(endSegmentIndex));

        CornerId = cornerId;
        StartSegmentIndex = startSegmentIndex;
        EndSegmentIndex = endSegmentIndex;
    }

    public bool ContainsSegment(int segmentIndex)
        => segmentIndex >= StartSegmentIndex && segmentIndex <= EndSegmentIndex;
}

/// <summary>
/// Immutable geometric projection of one rider's local segment progress onto
/// its complete logical corner. Every normalized value is in [0, 1].
/// </summary>
public readonly record struct CornerPhaseContext(
    int CornerId,
    int SegmentIndex,
    int SegmentOffset,
    int SegmentCount,
    SegmentType CompatibilitySegmentType,
    float SegmentStartCornerProgress,
    float SegmentEndCornerProgress,
    float CornerProgress,
    float TotalCornerLengthMeters,
    float RemainingCornerLengthMeters)
{
    public bool IsFirstSegment => SegmentOffset == 0;
    public bool IsLastSegment => SegmentOffset == SegmentCount - 1;
}

/// <summary>
/// Canonical, immutable and deterministic logical-corner topology for a track.
/// Corners never join across the lap boundary.
/// </summary>
public sealed class CornerTopology
{
    private readonly ReadOnlyCollection<LogicalCorner> _corners;
    private readonly LogicalCorner?[] _cornerBySegmentIndex;
    private readonly ReadOnlyCollection<TrackSegment> _segments;

    public IReadOnlyList<LogicalCorner> Corners => _corners;
    public int SegmentCount => _segments.Count;

    internal CornerTopology(IReadOnlyList<TrackSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0)
            throw new ArgumentException("Corner topology requires at least one track segment.", nameof(segments));

        _segments = Array.AsReadOnly(segments.ToArray());
        _cornerBySegmentIndex = new LogicalCorner?[segments.Count];
        var corners = new List<LogicalCorner>();
        var segmentIndex = 0;
        while (segmentIndex < segments.Count)
        {
            if (!IsTurn(segments[segmentIndex].Type))
            {
                segmentIndex++;
                continue;
            }

            var startSegmentIndex = segmentIndex;
            while (segmentIndex + 1 < segments.Count
                && IsTurn(segments[segmentIndex + 1].Type))
            {
                segmentIndex++;
            }

            var corner = new LogicalCorner(
                corners.Count + 1,
                startSegmentIndex,
                segmentIndex);
            corners.Add(corner);
            for (var member = startSegmentIndex; member <= segmentIndex; member++)
                _cornerBySegmentIndex[member] = corner;
            segmentIndex++;
        }

        _corners = Array.AsReadOnly(corners.ToArray());
    }

    public LogicalCorner? CornerForSegment(int segmentIndex)
    {
        ValidateSegmentIndex(segmentIndex);
        return _cornerBySegmentIndex[segmentIndex];
    }

    /// <summary>
    /// Resolves progress from accumulated physical arc distance at the supplied
    /// lateral reference. No equal-third assumption is used.
    /// </summary>
    public CornerPhaseContext? Resolve(
        int segmentIndex,
        float localSegmentProgress,
        float lateralPosition,
        TrackGeometry geometry)
    {
        ValidateSegmentIndex(segmentIndex);
        if (!float.IsFinite(localSegmentProgress)
            || localSegmentProgress < 0f
            || localSegmentProgress > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(localSegmentProgress),
                localSegmentProgress,
                "Local segment progress must be finite and in [0, 1].");
        }
        ArgumentNullException.ThrowIfNull(geometry);

        var corner = _cornerBySegmentIndex[segmentIndex];
        if (corner is null)
            return null;

        var accumulatedLengthMeters = 0f;
        var currentSegmentLengthMeters = 0f;
        var totalCornerLengthMeters = 0f;
        for (var member = corner.StartSegmentIndex; member <= corner.EndSegmentIndex; member++)
        {
            var lengthMeters = LaneModel.SegmentLengthMeters(
                _segments[member],
                lateralPosition,
                geometry);
            if (member < segmentIndex)
                accumulatedLengthMeters += lengthMeters;
            if (member == segmentIndex)
                currentSegmentLengthMeters = lengthMeters;
            totalCornerLengthMeters += lengthMeters;
        }

        if (!float.IsFinite(totalCornerLengthMeters) || totalCornerLengthMeters <= 0f)
            throw new InvalidOperationException("Logical corner length must be positive and finite.");

        var segmentEndDistanceMeters = accumulatedLengthMeters + currentSegmentLengthMeters;
        var travelledCornerDistanceMeters = accumulatedLengthMeters
            + currentSegmentLengthMeters * localSegmentProgress;
        var segmentStartProgress = accumulatedLengthMeters / totalCornerLengthMeters;
        var segmentEndProgress = segmentEndDistanceMeters / totalCornerLengthMeters;
        var cornerProgress = Math.Clamp(
            travelledCornerDistanceMeters / totalCornerLengthMeters,
            0f,
            1f);
        var remainingCornerLengthMeters = MathF.Max(
            0f,
            totalCornerLengthMeters - travelledCornerDistanceMeters);

        return new CornerPhaseContext(
            corner.CornerId,
            segmentIndex,
            segmentIndex - corner.StartSegmentIndex,
            corner.SegmentCount,
            _segments[segmentIndex].Type,
            segmentStartProgress,
            segmentEndProgress,
            cornerProgress,
            totalCornerLengthMeters,
            remainingCornerLengthMeters);
    }

    /// <summary>
    /// Returns a corner only when its first segment is immediately next in
    /// topology. Lap wrapping selects the first corner but never joins it with
    /// a corner ending at the previous lap boundary.
    /// </summary>
    public LogicalCorner? ImmediateNextCorner(int segmentIndex, bool allowLapWrap)
    {
        ValidateSegmentIndex(segmentIndex);
        if (segmentIndex == SegmentCount - 1 && !allowLapWrap)
            return null;

        var nextSegmentIndex = (segmentIndex + 1) % SegmentCount;
        var corner = _cornerBySegmentIndex[nextSegmentIndex];
        return corner is not null && corner.StartSegmentIndex == nextSegmentIndex
            ? corner
            : null;
    }

    private static bool IsTurn(SegmentType segmentType)
        => segmentType is SegmentType.TurnEntry
            or SegmentType.TurnMiddle
            or SegmentType.TurnExit;

    private void ValidateSegmentIndex(int segmentIndex)
    {
        if (segmentIndex < 0 || segmentIndex >= SegmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
    }
}
