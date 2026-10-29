using System.IO.Compression;
using System.Text.Json;
using Robust150;
namespace Contingency151;
public static class Program151 {
    public const string Doc = "docs/experiments/contingency-recourse";
    public const string Base = "5bc8575f60d9fa1116e45dad85c08786158faccd";
    public static JsonSerializerOptions Json => Program150.Json;
    public static T Read<T>(string path) => Program150.Read<T>(path);
    public static void Snapshot(string label) {
        Save("sources/" + label + ".json.gz", Directory.GetFiles("tools/contingency-recourse", "*.cs").Order().ToDictionary(p => Path.GetFileName(p), File.ReadAllText));
    }
    public static void Save(string name, object data) {
        var path = Path.Combine(Doc, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = new FileStream(path, FileMode.CreateNew);
        if (path.EndsWith(".gz", StringComparison.Ordinal)) { using var gzip = new GZipStream(file, CompressionLevel.Fastest); JsonSerializer.Serialize(gzip, data, Json); }
        else JsonSerializer.Serialize(file, data, Json);
    }
    public static void Main(string[] args) {
        if (args.Length != 1) throw new ArgumentException("Study151 is OFF. Supply one explicit experiment flag.");
        switch (args[0]) {
            case "--audit151": Audit151.Run(); break;
            case "--audit-v2-151": Audit151.Run("v2"); break;
            case "--gate1-151": Experiments151.Gate1(); break;
            case "--gate1-v2-151": Experiments151.Gate1("v2"); break;
            case "--gate1-v3-151": Experiments151.Gate1("v3"); break;
            case "--gate1-v4-151": Experiments151.Gate1("v4"); break;
            case "--gate1-v5-151": Experiments151.Gate1("v5"); break;
            case "--gate1-v6-151": Experiments151.Gate1("v6"); break;
            case "--gate1-v7-151": Experiments151.Gate1("v7"); break;
            case "--gate2-v2-a151": Experiments151.Native("contingency151-gate2-A-v2", 2); break;
            case "--gate2-v3-a151": Experiments151.Native("contingency151-gate2-A-v3", 2); break;
            case "--axis-audit151": AxisAudit151.Run(); break;
            case "--gate2-a151": Experiments151.Native("contingency151-gate2-A-v1", 2); break;
            case "--gate3-a151": Experiments151.Native("contingency151-gate3-A-v1", 3); break;
            case "--gate4-a151": Experiments151.Native("contingency151-gate4-A-v1", 4); break;
            case "--gate5-b151": Experiments151.Native("contingency151-gate5-B-v1", 5, true); break;
            case "--probes151": Probes151.Run(); break;
            case "--held-probes151": Probes151.Run(true); break;
            case "--early151": Early151.Run(); break;
            case "--native-errors151": NativeError151.Run(); break;
            case "--fault29.8-151": FaultAudit151.Run(); break;
            case "--gate4-v2-a151": FaultAudit151.RepairedFull(); break;
            default: throw new ArgumentException("Unknown Study151 experiment.");
        }
    }
}
