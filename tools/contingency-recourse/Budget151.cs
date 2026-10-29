using System.Diagnostics;
using System.Text.Json.Nodes;
namespace Contingency151;
/// <summary>Write-ahead, exclusive, cumulative budget. Failures and interrupted reservations remain visible.</summary>
public sealed class Budget151 : IDisposable {
    readonly Process process = Process.GetCurrentProcess();
    readonly TimeSpan start;
    readonly JsonArray ledger;
    readonly JsonObject row;
    readonly FileStream lease;
    readonly string path = Program151.Doc + "/budget.json";
    readonly double cap;
    bool complete;
    public double Elapsed => (process.TotalProcessorTime - start).TotalSeconds;
    public Budget151(string name, int worlds, double maximumCpu, bool savedExport = false) {
        Directory.CreateDirectory(Program151.Doc);
        lease = new(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        ledger = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))!.AsArray() : new();
        if (ledger.Any(x => !x!["Complete"]!.GetValue<bool>())) throw new IOException("Inspect interrupted Study151 reservation.");
        if (ledger.Any(x => x!["Name"]!.GetValue<string>() == name)) throw new IOException("Preserve attempt " + name);
        if (worlds < 0 || ledger.Sum(x => x!["NativeWorlds"]!.GetValue<int>()) + worlds > 25) throw new IOException("Study151 world budget exhausted.");
        if (savedExport && worlds != 0) throw new ArgumentException("Export reserve cannot create native worlds");
        cap = Math.Min(maximumCpu, (savedExport ? 1197 : 1180) - ledger.Sum(x => x!["CpuSeconds"]!.GetValue<double>()));
        if (cap <= 0) throw new IOException("Study151 CPU reserve exhausted.");
        start = process.TotalProcessorTime;
        row = new() { ["Name"] = name, ["Complete"] = false, ["NativeWorlds"] = worlds, ["CpuSeconds"] = 0, ["Base"] = Program151.Base };
        ledger.Add(row); Save();
    }
    void Save() => File.WriteAllText(path, ledger.ToJsonString(Program151.Json));
    public void Guard() { if (Elapsed >= cap) throw new InvalidOperationException("STUDY151_CPU_STOP"); }
    public void Finish(string outcome) { if (complete) return; row["Complete"] = true; row["CpuSeconds"] = Elapsed; row["Outcome"] = outcome; Save(); complete = true; }
    public void Dispose() { if (!complete) Finish("exception; preserve failure and inspect output"); lease.Dispose(); }
}
