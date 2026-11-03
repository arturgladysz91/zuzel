using System.Text.Json;
using Continuation161;
using LineThrottle156;
namespace S3Performance162;
public static class Gates162 {
 public static void Native(bool held){
  if(File.Exists(Path.Combine(Program162.Doc,"series-closure.json")))throw new InvalidOperationException("First162 native DNF closes series");
  var d=Study156.Read<JsonElement>(Path.Combine(Program162.Doc,"diagnostic.json.gz"));if(d.GetProperty("PolicyHash").GetString()!=Program162.PolicyHash()||!d.GetProperty("Legal").GetBoolean()||!d.GetProperty("Exact161").GetBoolean()||d.GetProperty("Error").ValueKind!=JsonValueKind.Null)throw new InvalidOperationException("Current exact three-maneuver full forecast required");
  var c=Directory.GetFiles(Path.Combine(Program162.Doc,"certificates"),"*.json.gz").Select(Study156.Read<JsonElement>).ToArray();
  if(c.Length!=2||c.Any(x=>x.GetProperty("PolicyHash").GetString()!=Program162.PolicyHash()||x.GetProperty("Error").ValueKind!=JsonValueKind.Null||!x.GetProperty("Review").GetProperty("Independent").GetBoolean()||x.GetProperty("Review").GetProperty("Tree").GetArrayLength()!=13||x.GetProperty("Review").GetProperty("Terminals").GetArrayLength()!=9)||c.Select(x=>x.GetProperty("Review").GetProperty("PhysicalHash").GetString()).Distinct().Count()!=1)throw new InvalidOperationException("Exact original/optimized full13/9 pair required");
  var results=Directory.Exists(Path.Combine(Program162.Doc,"native"))?Directory.GetFiles(Path.Combine(Program162.Doc,"native"),"*-result.json"):Array.Empty<string>();
  if(!held&&(results.Length!=0||File.Exists(Path.Combine(Program162.Doc,"budget-journal.json"))&&Study156.Read<JsonElement>(Path.Combine(Program162.Doc,"budget-journal.json")).EnumerateArray().Any(x=>x.GetProperty("Kind").GetString()=="native")))throw new InvalidOperationException("One development world only");
  if(held){var freeze=Study156.Read<JsonElement>(Path.Combine(Program162.Doc,"controller-freeze.json"));if(!results.Any(p=>{var r=Study156.Read<JsonElement>(p);return r.GetProperty("Pass").GetBoolean()&&r.GetProperty("ChargedCpuSeconds").GetDouble()<r.GetProperty("BaselineCpuSeconds").GetDouble();}))throw new InvalidOperationException("First full S3 required");foreach(var s in freeze.GetProperty("Sources").EnumerateObject())if(Program162.Sources().GetValueOrDefault(s.Name)!=s.Value.GetString())throw new InvalidOperationException("Frozen source changed");}
 }
 public static void Freeze(){var n=Study156.Read<JsonElement>(Path.Combine(Program162.Doc,"native/native-dev-result.json"));if(!n.GetProperty("Pass").GetBoolean())throw new InvalidOperationException("S3 required before held results");Program162.Save("controller-freeze.json",new{Program162.Base,Sources=Program162.Sources(),PolicyHash=Program162.PolicyHash(),HeldCount=5,BeforeHeldResults=true,Conditions="First five existing161 cases; no tuning after new results"});}
}
