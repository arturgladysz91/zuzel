// Prosty log symulacji: zbiera linie tekstu, bez zależności od Console.
namespace CoreSim.Logging;

public sealed class SimLog
{
    private readonly List<string> _lines = new();
    private readonly List<TrackSurfaceChange> _surfaceChanges = new();
    public IReadOnlyList<string> Lines => _lines;
    public IReadOnlyList<TrackSurfaceChange> SurfaceChanges => _surfaceChanges;
    public void Add(string line) => _lines.Add(line);
    public void Add(TrackSurfaceChange change) => _surfaceChanges.Add(change);
}
