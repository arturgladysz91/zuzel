using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;
namespace Robust150;
public static class Program150{
 public const string Doc="docs/experiments/robust-autonomous-racing";
 public static JsonSerializerOptions Json=>FreeLearningExperiment.Json;
 public static T Read<T>(string path){using var f=File.OpenRead(path);using Stream s=path.EndsWith(".gz",StringComparison.Ordinal)?new GZipStream(f,CompressionMode.Decompress):f;return JsonSerializer.Deserialize<T>(s,Json)!;}
 public static void Save(string name,object data){var p=Path.Combine(Doc,name);Directory.CreateDirectory(Path.GetDirectoryName(p)!);if(File.Exists(p))throw new IOException("Preserve attempt "+p);if(p.EndsWith(".gz",StringComparison.Ordinal)){using var z=new GZipStream(File.Create(p),CompressionLevel.Fastest);JsonSerializer.Serialize(z,data,Json);}else File.WriteAllText(p,JsonSerializer.Serialize(data,Json));}
 public static void Main(string[] args){if(args.Length!=1)throw new ArgumentException("Study150 is OFF; explicit flag required.");switch(args[0]){case "--diagnosis150":Diagnosis150.Run();break;case "--state-probes150":StateProbes150.Run();break;case "--baseline-audit150":StateProbes150.BaselineAudit();break;case "--critical-tube150":Experiments150.Critical(true);break;case "--rolling-a-tube150":Experiments150.Native("robust150-tube-rolling-A",false,true,true);break;case "--full-a-tube150":Experiments150.Native("robust150-tube-full-A",false,false,true);break;case "--full-b-tube150":Experiments150.Native("robust150-tube-full-B",true,false,true);break;case "--critical150":Experiments150.Critical();break;case "--rolling-a150":Experiments150.Native("robust150-rolling-A",false,true);break;case "--full-a150":Experiments150.Native("robust150-full-A",false);break;case "--full-b150":Experiments150.Native("robust150-full-B",true);break;case "--native-diagnosis150":NativeDiagnosis150.Run();break;default:throw new ArgumentException("Unknown explicit flag.");}}
}
