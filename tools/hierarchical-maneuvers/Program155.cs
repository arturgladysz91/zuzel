using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Security.Cryptography;
using Autonomous146;
using Contingency151;
using Efficient152;
using Safety153;
using SharedWorldSpeedway;
using Viable154;
namespace Hierarchical155;
public static class Program155 {
 public const string Base="405111a721d5bc5ba1477999f25b3837e8c62307";
 public static string Doc=>Environment.GetEnvironmentVariable("SPEEDWAY155_OUTPUT")??"docs/experiments/hierarchical-maneuvers";
 public static JsonSerializerOptions Json=>Program153.Json;
 public static T Read<T>(string p)=>Program153.Read<T>(p);
 public static void Save(string name,object value){var p=Path.Combine(Doc,name);Directory.CreateDirectory(Path.GetDirectoryName(p)!);using var f=new FileStream(p,FileMode.CreateNew);if(p.EndsWith(".gz")){using var z=new GZipStream(f,CompressionLevel.Optimal);JsonSerializer.Serialize(z,value,Json);}else JsonSerializer.Serialize(f,value,Json);}
 public static void Sources(string name){Directory.CreateDirectory(Doc+"/sources");using var f=new FileStream(Doc+"/sources/"+name+".zip",FileMode.CreateNew);using var z=new ZipArchive(f,ZipArchiveMode.Create);var hashes=new Dictionary<string,string>();foreach(var p in Directory.GetFiles("tools/hierarchical-maneuvers").Order()){hashes[Path.GetFileName(p)]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));z.CreateEntryFromFile(p,Path.GetFileName(p),CompressionLevel.Optimal);}Save("sources/"+name+".json",hashes);}
 public static RuntimeOptions153 Options()=>new(ValidatedIndependent:true,ShareCurrentPolicies:true,FastIndependent:true,Regime:Regime153.SafetyHalfMeter,RankPolicies:false,StrictSeeds:true);
 public static void Main(string[] args){if(args.Length==2&&args[0]=="--stability"){Replay155.Stability(args[1]);return;}if(args.Length==1&&args[0]=="--export-replays"){Replay155.Export();return;}if(args.Length!=4||args[0]!="--probe")throw new ArgumentException("Study155 OFF; --probe A|B|C 0|6.6|13.8 unique-label");Probe(args[1],double.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture),args[3]);}
 public static (State146 State,RiderSpec Spec,string? Prior) State(double time){var root=Program154.Doc;var run=Read<Run144>(root+"/native/S0-full-baseline-v1.json.gz");if(time==0){var s=run.Riders[0].Samples[0];return(new(s.Input,s.CrankOmega),run.Riders[0].Spec,null);}if(time==6.6){var s=Read<JsonElement>(root+"/policies/S0-full-baseline-v1/decision-0033.json.gz").GetProperty("Observed").Deserialize<State146>(Json)!;return(s,run.Riders[0].Spec,root+"/policies/S0-full-baseline-v1/decision-0032.json.gz");}if(time==13.8){var x=Read<JsonElement>(root+"/policies/S0-full-baseline-v1/abstain.json.gz");return(new(x.GetProperty("Observed").Deserialize<FreeRacingInput>(Json)!,x.GetProperty("CrankOmega").GetDouble()),run.Riders[0].Spec,root+"/policies/S0-full-baseline-v1/decision-0068.json.gz");}throw new ArgumentException("Three frozen comparison states only");}
 public static void Probe(string mode,double time,string version){if(mode is not ("A" or "B" or "C"))throw new ArgumentException("A=S0 B=beam C=forward checked beam");var label=mode+"-"+time.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+"-"+version;using var budget=new Budget155(label,0,50);Sources(label);var s=State(time);string? error=null;Review151? review=null;object? extra=null;
  if(mode=="A"){var p=new Planner154(s.Spec,false,new(Enabled:true),budget.Guard,performance:Program154.Performance(0));if(s.Prior is not null)p.InitializeDiagnosticReview(Program154.Restore(s.Prior,s.Spec,budget.Guard));try{p.Choose(s.State.Input,new(omega:s.State.CrankOmega));}catch(InvalidOperationException e){error=e.Message;}review=p.Last;extra=p.Runtime.Counters();}
  else{var p=new Planner155(s.Spec,false,new(Enabled:true),budget.Guard,performance:Options(),forwardCheck:mode=="C");try{p.Choose(s.State.Input,new(omega:s.State.CrankOmega));}catch(InvalidOperationException e){error=e.Message;}review=p.Last;extra=new{Counters=p.Runtime.Counters(),p.Generator.Outcomes,p.Generator.Rejections,p.Generator.FurthestPartial,p.Generator.DistinctLegalStates,NoHistoricalAnchor=true};}
  Save(label+".json.gz",new{Mode=mode,Time=time,Error=error,Passed=error is null&&review is {ConditionalCertificate:true,Independent:true},Review=review is null?null:Evidence152.Compact(review),Extra=extra,CpuSeconds=budget.Elapsed,Scope="Identical saved input/queue and original terminal sections/26s. A has its own certified prior policy, B/C start without historical tapes. No native world; local full13/9 is not a complete race."});budget.Finish(error??"independently certified original13/9 S continuation");Console.WriteLine(label+" "+(error??"CERTIFIED")+" CPU="+budget.Elapsed);
 }
}
