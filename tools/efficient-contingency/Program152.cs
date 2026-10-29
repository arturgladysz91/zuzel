using System.IO.Compression;
using System.Text.Json;
using Contingency151;
namespace Efficient152;
public static class Program152 {
 public const string Doc="docs/experiments/efficient-contingency";
 public const string Base="5558a30c8864199fb1c7abc18852ca27f4d29d9b";
 public static JsonSerializerOptions Json=>Program151.Json;
 public static T Read<T>(string path)=>Program151.Read<T>(path);
 public static void Save(string name,object value){var path=Path.Combine(Doc,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var f=new FileStream(path,FileMode.CreateNew);if(path.EndsWith(".gz",StringComparison.Ordinal)){using var z=new GZipStream(f,CompressionLevel.Optimal);JsonSerializer.Serialize(z,value,Json);}else JsonSerializer.Serialize(f,value,Json);}
 public static object Sources()=>Directory.GetFiles("tools/efficient-contingency").Where(p=>p.EndsWith(".cs")||p.EndsWith(".py")).Order().ToDictionary(p=>Path.GetFileName(p)!,p=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p))));
 public static void Main(string[] args){if(args.Length!=1)throw new ArgumentException("Study152 is OFF; explicit flag required");switch(args[0]){case "--gate1-b-v2-152":Experiments152.Critical(true);break;case "--gate1-152":Experiments152.Critical();break;case "--native-a152":Experiments152.Native();break;case "--held-b152":Held152.Run();break;case "--native-b152":Experiments152.Native(true);break;case "--repair38-v7-152":RepairProbe152.Run("v7");break;case "--repair38-v5-152":RepairProbe152.Run("v5");break;case "--repair38-v6-152":RepairProbe152.Run("v6");break;case "--repair38-v4-152":RepairProbe152.Run("v4");break;case "--repair38-v3-152":RepairProbe152.Run("v3");break;case "--repair38-v2-152":RepairProbe152.Run("v2");break;case "--repair38-152":RepairProbe152.Run();break;case "--recovery-portfolio152":Recovery152.Run();break;case "--native-portfolio-a152":Experiments152.Native(false,"portfolio-v1",true);break;case "--native-portfolio-b152":Experiments152.Native(true,"portfolio-v1",true);break;case "--micro152":Micro152.Run();break;case "--baseline152":Audits152.Baseline();break;case "--bench-cold152":Audits152.Bench("cold");break;case "--bench-boundary152":Audits152.Bench("boundary",true);break;case "--bench-project152":Audits152.Bench("project",true);break;case "--bench-memo152":Audits152.Bench("memo");break;case "--bench-verified152":Audits152.Bench("verified",true);break;default:throw new ArgumentException("Unknown explicit152 experiment");}}
}
