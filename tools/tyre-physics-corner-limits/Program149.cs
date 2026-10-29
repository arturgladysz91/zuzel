using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;
namespace Limits149;
public static class Program149{
 public const string Doc="docs/experiments/tyre-physics-corner-limits";
 public static JsonSerializerOptions Json=>FreeLearningExperiment.Json;
 public static T Read<T>(string path){using var f=File.OpenRead(path);using Stream input=path.EndsWith(".gz",StringComparison.Ordinal)?new GZipStream(f,CompressionMode.Decompress):f;return JsonSerializer.Deserialize<T>(input,Json)!;}
 public static void Save(string name,object value){var path=Path.Combine(Doc,name);if(File.Exists(path))throw new IOException("Preserve existing evidence "+path);if(path.EndsWith(".gz",StringComparison.Ordinal)){using var z=new GZipStream(File.Create(path),CompressionLevel.Fastest);JsonSerializer.Serialize(z,value,Json);}else File.WriteAllText(path,JsonSerializer.Serialize(value,Json));}
 public static void Main(string[] args){if(args.Length!=1)throw new ArgumentException("Study149 is OFF by default; explicit flag required.");switch(args[0]){case "--baseline149":Baseline149.Run("baseline149-final148",false);break;case "--audit149":Audit149.Run();break;case "--parity149":Fixtures149.Parity();break;case "--capabilities149":Fixtures149.Capabilities();break;case "--native-b149":Native149.Run("separated149-autonomous",false);break;default:throw new ArgumentException("Unknown explicit flag.");}}
}
