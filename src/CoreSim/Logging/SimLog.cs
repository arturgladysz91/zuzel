namespace CoreSim.Logging;

/// <summary>
/// Deterministic simulation log. Detailed capture can be disabled for large
/// balance batches without changing the simulated track or race outcome.
/// </summary>
public sealed class SimLog
{
    private readonly List<string> _lines = new();
    private readonly List<TrackSurfaceChange> _surfaceChanges = new();
    private readonly List<OvertakeEvent> _overtakes = new();
    private readonly List<RaceOrderSnapshot> _orderSnapshots = new();
    private readonly List<SegmentOrderSnapshot> _segmentOrderSnapshots = new();
    private readonly List<RaceEvent> _raceEvents = new();

    public SimLog(bool enabled = true) => Enabled = enabled;

    public bool Enabled { get; }
    public IReadOnlyList<string> Lines => _lines;
    public IReadOnlyList<TrackSurfaceChange> SurfaceChanges => _surfaceChanges;
    public IReadOnlyList<OvertakeEvent> Overtakes => _overtakes;
    public IReadOnlyList<RaceOrderSnapshot> OrderSnapshots => _orderSnapshots;
    public IReadOnlyList<SegmentOrderSnapshot> SegmentOrderSnapshots => _segmentOrderSnapshots;
    public IReadOnlyList<RaceEvent> RaceEvents => _raceEvents;

    public void Add(string line)
    {
        if (Enabled)
            _lines.Add(line);
    }

    public void Add(TrackSurfaceChange change)
    {
        if (Enabled)
            _surfaceChanges.Add(change);
    }

    public void Add(OvertakeEvent overtake)
    {
        if (Enabled)
            _overtakes.Add(overtake);
    }

    public void Add(RaceOrderSnapshot snapshot)
    {
        if (Enabled)
            _orderSnapshots.Add(snapshot);
    }

    public void Add(SegmentOrderSnapshot snapshot)
    {
        if (Enabled)
            _segmentOrderSnapshots.Add(snapshot);
    }

    public void Add(RaceEvent raceEvent)
    {
        if (Enabled)
            _raceEvents.Add(raceEvent);
    }
}
