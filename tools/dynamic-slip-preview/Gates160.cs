using System.Security.Cryptography;
using System.Text.Json;
using LineThrottle156;
namespace Dynamic160;
public static class Gates160 {
 public static void RequireNative(bool validation){
  if(File.Exists(Path.Combine(Program160.Doc,"series-closure.json")))throw new InvalidOperationException("Study160 native series closed after first DNF; no automatic restart");
  var forecast=Study156.Read<JsonElement>(Path.Combine(Program160.Doc,"forecasts/adaptive-v3.json.gz")).GetProperty("Summary");
  if(!forecast.GetProperty("Reached").GetBoolean()||!forecast.GetProperty("Legal").GetBoolean()||forecast.GetProperty("EndTime").GetDouble()>=Study156.Read<JsonElement>(Path.Combine(Study156.Root,"docs/experiments/geometry-speed-audit/forecasts/rest-base10.json.gz")).GetProperty("Summary").GetProperty("Duration").GetDouble())throw new InvalidOperationException("Gate C closed");
  var certificate=Study156.Read<JsonElement>(Path.Combine(Program160.Doc,"certificates/adaptive-v5-initial.json.gz"));var r=certificate.GetProperty("Review");
  if(certificate.GetProperty("Error").ValueKind!=JsonValueKind.Null||!r.GetProperty("ConditionalCertificate").GetBoolean()||!r.GetProperty("Independent").GetBoolean()||r.GetProperty("Tree").GetArrayLength()!=13||r.GetProperty("Terminals").GetArrayLength()!=9)throw new InvalidOperationException("Gate D closed");
  var prior=Directory.Exists(Path.Combine(Program160.Doc,"native"))?Directory.GetFiles(Path.Combine(Program160.Doc,"native"),"*-result.json"):Array.Empty<string>();
  if(!validation&&prior.Length!=0)throw new InvalidOperationException("One development native only; diagnose before another series");
  if(validation&&!prior.Any(p=>Study156.Read<JsonElement>(p).GetProperty("Pass").GetBoolean()))throw new InvalidOperationException("Gate F needs first native S3");
 }
}
public static class Freeze160 {
 public static Dictionary<string,string> Sources(){var root=Study156.Root;return Directory.GetFiles(Path.Combine(root,"tools/dynamic-slip-preview")).Where(p=>!p.EndsWith(".py",StringComparison.Ordinal)).OrderBy(p=>p,StringComparer.Ordinal).ToDictionary(p=>Path.GetRelativePath(root,p).Replace('\\','/'),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant());}
 public static void Create(){var result=Study156.Read<JsonElement>(Path.Combine(Program160.Doc,"native/native-dev-v5-result.json"));if(!result.GetProperty("Pass").GetBoolean())throw new InvalidOperationException("Cannot freeze before first native S3");Program160.Save("controller-freeze.json",new{Program160.Base,Sources=Sources(),Settings=new Settings160(Enabled:true,Adaptive:true),HeldCount=result.GetProperty("ChargedCpuSeconds").GetDouble()<=900?10:5,Conditions="docs/experiments/robust-three-lap/final-v6/scenarios-frozen.json",AfterFirstNative=true});}
 public static void Check(){var freeze=Study156.Read<JsonElement>(Path.Combine(Program160.Doc,"controller-freeze.json"));foreach(var row in freeze.GetProperty("Sources").EnumerateObject())if(Sources().GetValueOrDefault(row.Name)!=row.Value.GetString())throw new InvalidOperationException("Frozen controller changed");}
}
