using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Autonomous146;
using Contingency151;
using Efficient147;
using Efficient152;
using SharedWorldSpeedway;
namespace Safety153;
public static class Program153 {
 public const string Base="a9024b5bb141e8158fc7c441705768a7b7354d35";
 public static string Doc=>Environment.GetEnvironmentVariable("SPEEDWAY153_OUTPUT")??"docs/experiments/safe-fast-contingency";
 public static JsonSerializerOptions Json=>Program151.Json;
 public static T Read<T>(string p)=>Program151.Read<T>(p);
 public static void Save(string name,object value){var p=Path.Combine(Doc,name);Directory.CreateDirectory(Path.GetDirectoryName(p)!);using var f=new FileStream(p,FileMode.CreateNew);if(p.EndsWith(".gz",StringComparison.Ordinal)){using var z=new GZipStream(f,CompressionLevel.Optimal);JsonSerializer.Serialize(z,value,Json);}else JsonSerializer.Serialize(f,value,Json);}
 public static object Sources()=>Directory.GetFiles("tools/safe-fast-contingency").Where(p=>p.EndsWith(".cs")||p.EndsWith(".py")||p.EndsWith(".csproj")).Order().ToDictionary(p=>Path.GetFileName(p)!,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
 public static void SourceArchive(string label){var path=Doc+"/sources/"+label+".zip";Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var f=new FileStream(path,FileMode.CreateNew);using var zip=new ZipArchive(f,ZipArchiveMode.Create);foreach(var p in Directory.GetFiles("tools/safe-fast-contingency").Where(p=>p.EndsWith(".cs")||p.EndsWith(".py")||p.EndsWith(".csproj")||p.EndsWith(".lock.json")).Order())zip.CreateEntryFromFile(p,Path.GetFileName(p),CompressionLevel.Optimal);}
 public static RuntimeOptions153 Performance(Regime153 regime,bool rank=false,bool fast=true)=>new(ValidatedIndependent:true,ShareCurrentPolicies:true,FastIndependent:fast,Regime:regime,RankPolicies:rank,StrictSeeds:regime==Regime153.SafetyHalfMeter);
 public static void Main(string[] args){
  if(args.Length!=1)throw new ArgumentException("Study153 is OFF; an explicit bounded experiment flag is required");
  switch(args[0]){
   case "--geometry153":GeometryAudit153.Run();break;
   case "--bench153":Bench();break;
   case "--bench152-153":Bench("v2",true);break;
   case "--bench-exact153":Bench("v3",true);break;
   case "--native-exact-L153":Native("exact-L-v3",Regime153.Legacy,false);break;
   case "--native-ranked-L153":Native("ranked-L-v3",Regime153.Legacy,true);break;
   case "--native-ranked-S153":Native("ranked-S-v1",Regime153.SafetyHalfMeter,true);break;
   case "--native-cold-L153":Native("cold-L-v1",Regime153.Legacy,false,true);break;
   case "--native-cold-prefix-L153":Native("cold-L-prefix-v1",Regime153.Legacy,false,true,12,90);break;
   default:throw new ArgumentException("Unknown explicit153 experiment");
  }
 }
 public static void Bench(string version="v1",bool completed152=false){
  using var budget=new Budget153("paired-exact-five-decisions-"+version,0,140);SourceArchive("bench-"+version);var run=Read<Run144>(completed152?Program152.Doc+"/native/efficient152-A-portfolio-v1.json.gz":Program151.Doc+"/native/"+Audits152.ReferenceName+".json.gz");var spec=run.Riders[0].Spec;var rows=new List<object>();bool passed=true;
  foreach(var index in new[]{0,2,13,50,149}){
   budget.Guard();string PathFor(int k)=>Program152.Doc+"/policies/efficient152-A-portfolio-v1/decision-"+k.ToString("D4")+".json.gz";
   var original=completed152?RepairProbe152.Restore(PathFor(index),spec,false,budget.Guard):Audits152.ReferenceReview(index);var prior=index>0?(completed152?RepairProbe152.Restore(PathFor(index-1),spec,false,budget.Guard):Audits152.ReferenceReview(index-1)):null;
   var old=new Planner152(spec,false,new(Enabled:true),budget.Guard,performance:new(ValidatedIndependent:true,ShareCurrentPolicies:completed152));
   var current=new Planner153(spec,false,new(Enabled:true),budget.Guard,performance:new(ValidatedIndependent:true,FastIndependent:true,ShareCurrentPolicies:completed152));
   if(prior is not null){old.InitializeDiagnosticReview(prior);current.InitializeDiagnosticReview(prior);}
   string? failure=null;
   try{if(index%2==0){old.Choose(original.Observed.Input,new(omega:original.Observed.CrankOmega));current.Choose(original.Observed.Input,new(omega:original.Observed.CrankOmega));}else{current.Choose(original.Observed.Input,new(omega:original.Observed.CrankOmega));old.Choose(original.Observed.Input,new(omega:original.Observed.CrankOmega));}}
   catch(InvalidOperationException e){failure=e.Message;}
   var equal=failure is null&&Bits152.Review(original)==Bits152.Review(old.Last!)&&Bits152.Review(original)==Bits152.Review(current.Last!);passed&=equal;
   rows.Add(new{Index=index,Time=original.Time,Failure=failure,AllCommandsStatesQueuesAndCertificatesEqual=equal,ColdCpu=old.Last?.CpuSeconds,OptimizedCpu=current.Last?.CpuSeconds,ColdProfile=old.Last?.Profile,OptimizedProfile=current.Last?.Profile,Counts=current.Runtime.Counters()});
   Console.WriteLine($"paired153 t={original.Time:F1} equal={equal} CPU={old.Last?.CpuSeconds:F3}->{current.Last?.CpuSeconds:F3}");
   if(!equal){Save("bench-mismatch-"+version+".json.gz",new{Original=Evidence152.Compact(original),Cold=old.Last is {} o?Evidence152.Compact(o):null,New=current.Last is {} n?Evidence152.Compact(n):null,Failure=failure});break;}
  }
  Save("bench-"+version+".json",new{Passed=passed,Reference=completed152?"completed152":"partial151",Rows=rows,Scope="Paired fresh planners on identical saved inputs and own prior policies. No native world. Full physical digest includes every selected command, point, queue, wheel, crank, nominal and nine reserve certificates; CPU/profile excluded."});budget.Finish(passed?"all physical bits and certificates match":"exact comparison failed");
 }
 public static void Native(string label,Regime153 regime,bool ranked,bool coldControl=false,double maximumSeconds=100,double? maximumCpu=null){
  if(!Read<JsonElement>(Doc+"/ci-gate1.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Windows CI gate1 prerequisite");
  if(!Read<JsonElement>(Doc+"/geometry-audit.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Safety semantics gate2 prerequisite");
  if(!Read<JsonElement>(Doc+"/bench-v3.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Exact CPU parity gate3 prerequisite");
  if(ranked&&!Read<JsonElement>(Doc+"/native/exact-L-v3-result.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Exact native legacy prerequisite");
  if(regime==Regime153.SafetyHalfMeter&&!Read<JsonElement>(Doc+"/native/ranked-L-v3-result.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Complete ranked legacy gate4 prerequisite");
  using var budget=new Budget153(label,1,maximumCpu??(coldControl?300:ranked?330:260));SourceArchive(label);Save("sources/"+label+".json",Sources());
  var oldRun=Read<Run144>(Program152.Doc+"/native/efficient152-A-portfolio-v1.json.gz");var reference=Journal147.ReadComplete(Program152.Doc+"/native/efficient152-A-portfolio-v1-decisions.jsonl").Where(x=>x.GetProperty("Event").GetString()=="decision").ToArray();
  Directory.CreateDirectory(Doc+"/native");using var journal=new Journal147(Doc+"/native/"+label+"-decisions.jsonl");var process=Process.GetCurrentProcess();Planner153? planner=null;Planner152? cold=null;var timings=new List<object>();int count=0,equal=0;double attempted=0,archive=0,planning=0;double lastAttempt=0;
  Decision144 Choose(FreeRacingInput i,CrankClutch144 crank,RiderSpec spec,TrackFrame frame,CoreSim.TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,string variant){
   var began=process.TotalProcessorTime;lastAttempt=i.Time;bool returned=false;
   try{budget.Guard();Decision144 choice;Review151 review;
    if(coldControl){cold??=new(spec,false,new(Enabled:true),budget.Guard,performance:new(ValidatedIndependent:true,ShareCurrentPolicies:true));choice=cold.Choose(i,crank);review=cold.Last!;}
    else{planner??=new(spec,false,new(Enabled:true),budget.Guard,performance:Performance(regime,ranked));choice=planner.Choose(i,crank);review=planner.Last!;}
    planning+=review.CpuSeconds;var physical=Bits152.Review(review);
    if(!ranked){var prior=reference[count];if(prior.GetProperty("PhysicalHash").GetString()!=physical||prior.GetProperty("Selected").Deserialize<FreeRacingAction>(Json)!=choice.Choice.Action||ExactKey147.State(new(prior.GetProperty("Observed").Deserialize<FreeRacingInput>(Json)!,prior.GetProperty("CrankOmega").GetDouble()))!=ExactKey147.State(new(i,crank.Omega)))throw new InvalidOperationException("Exact152 full native parity differs at"+i.Time);equal++;}
    var before=process.TotalProcessorTime;var id="decision-"+count.ToString("D4");Save("policies/"+label+"/"+id+".json.gz",Evidence152.Compact(review));
    journal.Append(new{Event="decision",i.Time,Regime=regime.ToString(),Selected=choice.Choice.Action,Tree=id,PhysicalHash=physical,review.ConditionalCertificate,review.Independent,review.Reason,review.CpuSeconds,review.Profile,review.Counts,Observed=i,CrankOmega=crank.Omega,Ranked=planner?.Ranked??new(),SelectedRank=PolicyRank153.From(review.Nominal!,review.Terminals,frame),Proof=new{Nodes=review.Tree.Length,Leaves=review.Terminals.Length,RejectedTrees=review.RejectedTrees.Length,Attempts=review.Attempts.Length,review.Nominal!.Arrival,review.Nominal.Safety,Trace=review.Nominal.Points.Where((_,k)=>k%10==0).Select(q=>new[]{q.Time,(double)q.Rider.Position.X,q.Rider.Position.Y}).ToArray()}});
    archive+=(process.TotalProcessorTime-before).TotalSeconds;if(count++%25==0)Console.WriteLine($"{label} t={i.Time:F1} CPU={budget.Elapsed:F3} decisions={count} exact={equal}");returned=true;return choice;
   }catch(InvalidOperationException e){Save("policies/"+label+"/abstain.json.gz",new{e.Message,Observed=i,CrankOmega=crank.Omega,Review=(planner?.Last??cold?.Last) is {} r?Evidence152.Compact(r):null,Ranked=planner?.Ranked,Counts=planner?.Runtime.Counters()??cold?.Runtime.Counters()});journal.Append(new{Event="abstain",i.Time,e.Message});throw;}
   finally{var seconds=(process.TotalProcessorTime-began).TotalSeconds;attempted+=seconds;timings.Add(new{Time=i.Time,CpuSeconds=seconds,Returned=returned});}
  }
  Run144 run;using(var world=new NativeWorld153(new(Enabled:true,RequiredLaps:3,MaximumSeconds:maximumSeconds),safetyHalfMeter:regime==Regime153.SafetyHalfMeter))run=world.Run(label,"K1",Choose);
  Save("native/"+label+".json.gz",run);Save("timings/"+label+".json",timings);var samples=run.Riders[0].Samples;var min=samples.Min(s=>Math.Min(s.Inner,s.Outer));var sweep=samples.Where(s=>s.Certificate is not null).Min(s=>s.Certificate)??double.NegativeInfinity;
  var passed=run.Outcome=="completed"&&run.Riders[0].CompletedLaps==3&&run.Contacts.Length==0&&(regime==Regime153.Legacy?sweep>=0:sweep>=Clearance153.RequiredMeters);
  var exactNative=!ranked&&run.Fingerprint==oldRun.Fingerprint&&equal==reference.Length;if(!ranked)passed&=exactNative;
  var prefixEqual=!ranked&&JsonSerializer.SerializeToUtf8Bytes(samples,Json).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(oldRun.Riders[0].Samples.Take(samples.Length).ToArray(),Json));
  Save("native/"+label+"-result.json",new{Passed=passed,Regime=regime.ToString(),Ranked=ranked,ColdControl=coldControl,MaximumNativeSeconds=maximumSeconds,run.Outcome,run.EndSeconds,run.Fingerprint,run.Riders[0].FinishSeconds,run.Riders[0].CompletedLaps,Decisions=count,LastAttempt=lastAttempt,Exact152Decisions=equal,Exact152NativeFingerprint=exactNative,NativePrefixAllFieldsEqual=prefixEqual,NativePrefixSamples=samples.Length,ControllerAttemptCpu=attempted,PlanningCpu=planning,PolicyArchiveCpu=archive,CpuSeconds=budget.Elapsed,MinimumGap=min,MinimumSweep=sweep,MaximumBeta=samples.Max(s=>Math.Abs(s.Input.Rider.SlideRadians)*180/Math.PI),Contacts=run.Contacts.Length,Counts=planner?.Runtime.Counters()??cold?.Runtime.Counters(),Scope="One continuous native Box2D world from rest. No supplied controls, preserved0.24s latency,13 logical nodes/9 independently repropagated reserves each accepted decision. PASS requires the active swept margin over the whole completed distance; a12s timing control is explicitly partial."});
  journal.Append(new{Event="summary",run.Outcome,run.Fingerprint,Passed=passed});budget.Finish(run.Outcome);Console.WriteLine($"{label} PASS={passed} {run.Outcome} t={run.EndSeconds:F3} CPU={budget.Elapsed:F3}");
 }
}
