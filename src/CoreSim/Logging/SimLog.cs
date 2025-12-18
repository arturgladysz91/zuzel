// Prosty log symulacji: zbiera linie tekstu, bez zależności od Console.
namespace CoreSim.Logging;

public sealed class SimLog
{
    private readonly List<string> _lines = new();
    public IReadOnlyList<string> Lines => _lines;
    public void Add(string line) => _lines.Add(line);
}
