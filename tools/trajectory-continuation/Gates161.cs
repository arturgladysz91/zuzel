using System.Security.Cryptography;
using System.Text.Json;
using LineThrottle156;
namespace Continuation161;
public static class Gates161 {
 public static void RequireNative(bool validation){
  if(File.Exists(Path.Combine(Program161.Doc,"series-closure.json")))throw new InvalidOperationException("First native DNF closes study161 native work");
  var diagnostics=Directory.GetFiles(Path.Combine(Program161.Doc,"diagnostics"),"*.json.gz").Select(Study156.Read<JsonElement>).Select(x=>x.GetProperty("Summary")).Where(q=>q.GetProperty("PolicyHash").GetString()==Program161.PolicyHash()&&q.GetProperty("Legal").GetBoolean()&&q.GetProperty("Error").ValueKind==JsonValueKind.Null).ToArray();
  if(!diagnostics.Any(q=>q.GetProperty("InitialTime").GetDouble()>0)||!diagnostics.Any(q=>q.GetProperty("InitialTime").GetDouble()==0))throw new InvalidOperationException("G2/G3 need current-policy recovered authentic state and full stopped-start forecast");
  var certificates=Directory.Exists(Path.Combine(Program161.Doc,"certificates"))?Directory.GetFiles(Path.Combine(Program161.Doc,"certificates"),"*.json.gz").Select(Study156.Read<JsonElement>).ToArray():Array.Empty<JsonElement>();
  if(!certificates.Any(q=>q.GetProperty("Fixture").GetString()=="rest"&&q.GetProperty("PolicyHash").GetString()==Program161.PolicyHash()&&q.GetProperty("Error").ValueKind==JsonValueKind.Null&&q.GetProperty("Review").GetProperty("ConditionalCertificate").GetBoolean()&&q.GetProperty("Review").GetProperty("Independent").GetBoolean()&&q.GetProperty("Review").GetProperty("Tree").GetArrayLength()==13&&q.GetProperty("Review").GetProperty("Terminals").GetArrayLength()==9))throw new InvalidOperationException("G4 requires fresh current-policy independent13/9 from rest");
  var results=Directory.Exists(Path.Combine(Program161.Doc,"native"))?Directory.GetFiles(Path.Combine(Program161.Doc,"native"),"*-result.json"):Array.Empty<string>();
  if(!validation&&results.Length>0)throw new InvalidOperationException("One development native world only");
  if(validation&&!results.Any(p=>Study156.Read<JsonElement>(p).GetProperty("Pass").GetBoolean()))throw new InvalidOperationException("Held cases require first full native S3");
 }
}
public static class Freeze161 {
 public static Dictionary<string,string> Sources()=>Directory.GetFiles(Path.Combine(Study156.Root,"tools/trajectory-continuation")).Where(p=>p.EndsWith(".cs",StringComparison.Ordinal)||p.EndsWith(".csproj",StringComparison.Ordinal)||p.EndsWith(".mjs",StringComparison.Ordinal)||p.EndsWith(".json",StringComparison.Ordinal)).OrderBy(p=>p,StringComparer.Ordinal).ToDictionary(p=>Path.GetFileName(p),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
 public static void Create(){var result=Study156.Read<JsonElement>(Path.Combine(Program161.Doc,"native/native-dev-result.json"));if(!result.GetProperty("Pass").GetBoolean())throw new InvalidOperationException("No freeze before native S3");Program161.Save("controller-freeze.json",new{Program161.Base,PolicyHash=Program161.PolicyHash(),Sources=Sources(),HeldCount=5,Conditions="docs/experiments/robust-three-lap/final-v6/scenarios-frozen.json",BeforeHeldResults=true});}
 public static void Check(){var frozen=Study156.Read<JsonElement>(Path.Combine(Program161.Doc,"controller-freeze.json"));foreach(var row in frozen.GetProperty("Sources").EnumerateObject())if(Sources().GetValueOrDefault(row.Name)!=row.Value.GetString())throw new InvalidOperationException("Frozen161 source changed");}
}
