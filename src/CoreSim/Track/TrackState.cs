using CoreSim.Logging;

namespace CoreSim;

public sealed class TrackState
{
    private readonly TrackSurfaceState[,] _surface;

    public int SegmentCount { get; }
    public int LinesCount { get; }

    public TrackState(int segmentCount, int linesCount, TrackSurfaceState? defaultSurface = null)
    {
        if (segmentCount <= 0) throw new ArgumentOutOfRangeException(nameof(segmentCount));
        if (linesCount <= 0) throw new ArgumentOutOfRangeException(nameof(linesCount));

        SegmentCount = segmentCount;
        LinesCount = linesCount;

        var surface = defaultSurface ?? TrackSurfaceState.Default;
        _surface = new TrackSurfaceState[segmentCount, linesCount];
        for (var segment = 0; segment < segmentCount; segment++)
        for (var lane = 0; lane < linesCount; lane++)
            _surface[segment, lane] = surface;
    }

    public TrackState(int segmentCount, int linesCount, Func<int, int, TrackSurfaceState> surfaceProfile)
    {
        if (segmentCount <= 0) throw new ArgumentOutOfRangeException(nameof(segmentCount));
        if (linesCount <= 0) throw new ArgumentOutOfRangeException(nameof(linesCount));
        ArgumentNullException.ThrowIfNull(surfaceProfile);

        SegmentCount = segmentCount;
        LinesCount = linesCount;
        _surface = new TrackSurfaceState[segmentCount, linesCount];
        for (var segment = 0; segment < segmentCount; segment++)
        for (var lane = 0; lane < linesCount; lane++)
            _surface[segment, lane] = surfaceProfile(segment, lane);
    }

    public static TrackState CreateDefault(Track track, TrackSurfaceState? defaultSurface = null)
        => new(track.Segments.Count, TrackSegment.LanesCount, defaultSurface ?? TrackSurfaceState.Default);

    public TrackSurfaceState GetSurface(int segmentIndex, int lineIndex)
    {
        ValidateIndices(segmentIndex, lineIndex);
        return _surface[segmentIndex, lineIndex];
    }

    public TrackState Snapshot()
        => new(SegmentCount, LinesCount, (segment, lane) => _surface[segment, lane]);

    public void ApplySurfaceDelta(
        int segmentIndex,
        int lineIndex,
        float deltaGrip,
        float deltaRuts,
        float deltaMoisture,
        string reason,
        int heatId,
        int tick,
        SimLog log)
    {
        ValidateIndices(segmentIndex, lineIndex);
        ArgumentNullException.ThrowIfNull(log);

        var current = _surface[segmentIndex, lineIndex];
        _surface[segmentIndex, lineIndex] = current.WithDelta(deltaGrip, deltaRuts, deltaMoisture);
        if (log.Enabled)
        {
            log.Add(new TrackSurfaceChange(
                segmentIndex,
                lineIndex,
                deltaGrip,
                deltaRuts,
                deltaMoisture,
                reason,
                heatId,
                tick));
        }
    }

    private void ValidateIndices(int segmentIndex, int lineIndex)
    {
        if (segmentIndex < 0 || segmentIndex >= SegmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        if (lineIndex < 0 || lineIndex >= LinesCount)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
    }
}
