namespace CoreSim.Race;

public enum RiderRaceStatus
{
    NotStarted,
    Racing,
    Finished,
    Crashed,
    Retired,
}

/// <summary>
/// Canonical topological position. Lap, segment and in-segment progress are
/// derived from one monotonic value and therefore cannot drift apart.
/// Physical distance is accumulated atomically with canonical progress.
/// </summary>
public readonly record struct RiderPosition
{
    private const double BoundaryTolerance = 0.000000001d;

    public int SegmentCount { get; }
    public double TotalSegmentProgress { get; }
    public float DistanceMeters { get; }

    public int LapsCompleted => SegmentCount == 0
        ? 0
        : (int)Math.Floor((TotalSegmentProgress + BoundaryTolerance) / SegmentCount);

    public int SegmentIndex => SegmentCount == 0
        ? 0
        : (int)Math.Floor(TotalSegmentProgress + BoundaryTolerance) % SegmentCount;

    public float SegmentProgress
    {
        get
        {
            var progress = TotalSegmentProgress - Math.Floor(TotalSegmentProgress + BoundaryTolerance);
            return (float)Math.Clamp(progress, 0d, 1d);
        }
    }

    private RiderPosition(int segmentCount, double totalSegmentProgress, float distanceMeters)
    {
        if (segmentCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(segmentCount));
        if (totalSegmentProgress < 0d)
            throw new ArgumentOutOfRangeException(nameof(totalSegmentProgress));
        if (distanceMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));

        SegmentCount = segmentCount;
        TotalSegmentProgress = NormalizeBoundary(totalSegmentProgress);
        DistanceMeters = distanceMeters;
    }

    public static RiderPosition Start(int segmentCount) => new(segmentCount, 0d, 0f);

    public static RiderPosition Create(
        int lapNumber,
        int segmentIndex,
        float segmentProgress,
        int segmentCount,
        float distanceMeters = 0f)
    {
        if (lapNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(lapNumber));
        if (segmentIndex < 0 || segmentIndex >= segmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        if (segmentProgress is < 0f or >= 1f)
            throw new ArgumentOutOfRangeException(nameof(segmentProgress));

        var total = (lapNumber - 1d) * segmentCount + segmentIndex + segmentProgress;
        return new RiderPosition(segmentCount, total, distanceMeters);
    }

    internal RiderPosition Advance(float canonicalProgress, float physicalDistanceMeters)
    {
        if (canonicalProgress < 0f)
            throw new ArgumentOutOfRangeException(nameof(canonicalProgress));
        if (physicalDistanceMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(physicalDistanceMeters));

        return new RiderPosition(
            SegmentCount,
            TotalSegmentProgress + canonicalProgress,
            DistanceMeters + physicalDistanceMeters);
    }

    /// <summary>
    /// Completes the currently owned segment after production traversal succeeded.
    /// Canonical ownership advances discretely; distance is only the actual traversal result.
    /// Partial/terminal traversal must use Advance instead.
    /// </summary>
    internal RiderPosition AdvanceToSegmentEnd(float physicalDistanceMeters)
    {
        if (SegmentCount <= 0 || !double.IsFinite(TotalSegmentProgress)
            || !float.IsFinite(DistanceMeters))
            throw new InvalidOperationException("Segment completion requires an initialized finite position.");
        if (!float.IsFinite(physicalDistanceMeters) || physicalDistanceMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(physicalDistanceMeters));

        // Use the same discrete ownership as SegmentIndex, never the float remainder.
        var boundary = Math.Floor(TotalSegmentProgress + BoundaryTolerance) + 1d;
        if (!double.IsFinite(boundary) || boundary <= TotalSegmentProgress)
            throw new InvalidOperationException("The next segment boundary must be representable and forward.");
        var distance = DistanceMeters + physicalDistanceMeters;
        if (!float.IsFinite(distance))
            throw new ArgumentOutOfRangeException(nameof(physicalDistanceMeters));
        return new RiderPosition(SegmentCount, boundary, distance);
    }
    private static double NormalizeBoundary(double value)
    {
        var boundary = Math.Round(value);
        return Math.Abs(value - boundary) <= BoundaryTolerance ? boundary : value;
    }
}
