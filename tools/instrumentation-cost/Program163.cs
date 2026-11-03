using System.Diagnostics;
using System.Text.Json;
using Continuation161;
using Efficient152;
using LineThrottle156;
using SharedWorldSpeedway;
namespace Instrumentation163;
public static class Program163 {
 public static string Doc=>Path.Combine(Study156.Root,"docs/experiments/instrumentation-cost");
 public static void Save(string name,object data)=>Study156.Save(Path.Combine(Doc,name),data);
 public static int Main(string[] args){try{if(args.Length==2&&args[0]=="--micro"){Micro(args[1]);return 0;}throw new ArgumentException("Study163 OFF; --micro declared-label only. Native CLI absent until >=10% gate.");}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 public static void Micro(string label){using var budget=new Budget163(label);var row=Study156.Read<JsonElement>(Path.Combine(Doc,"budget-plan.json")).GetProperty("Cases").EnumerateArray().Single(r=>r.GetProperty("Label").GetString()==label);var order=row.GetProperty("Order").EnumerateArray().Select(v=>Enum.Parse<ProfileMode163>(v.GetString()!)).ToArray();var process=Process.GetCurrentProcess();double prepareAt=budget.Elapsed;ArchivedContext163? context=null;string? error=null;var trials=new List<object>();var clocks=new List<object>();
  try{context=ArchivedContext163.Load(row.GetProperty("Native").GetString()!,row.GetProperty("Decision").GetInt32(),budget.Guard);double preparation=budget.Elapsed-prepareAt;
   // Reconstruction precedes the trials; clock/scope calibration follows them.
   // The first real trial remains visible, counterbalanced by reversed order.
   foreach(var mode in order){budget.Guard();long guardCalls=0;void Guard(){guardCalls++;budget.Guard();}var planner=context.Planner(mode,Guard);var began=process.TotalProcessorTime;long allocated=GC.GetAllocatedBytesForCurrentThread();var wall=Stopwatch.StartNew();var result=planner.Choose(context.Observed.Input,new(omega:context.Observed.CrankOmega));wall.Stop();double cpu=(process.TotalProcessorTime-began).TotalSeconds;var review=planner.Last!;string hash=Bits152.Review(review);bool exact=hash==context.ArchivedHash;
    trials.Add(new{Mode=mode.ToString(),CpuSeconds=cpu,WallSeconds=wall.Elapsed.TotalSeconds,AllocatedBytesOnCurrentThread=GC.GetAllocatedBytesForCurrentThread()-allocated,GuardCalls=guardCalls,PhysicalHash=hash,ExactArchived161=exact,Nodes=review.Tree.Length,Reserves=review.Terminals.Length,review.ConditionalCertificate,review.Independent,review.Goal,review.SafetyGoal,Profiler=new{planner.Measurements.ClockReads,planner.Measurements.PhaseRequests,planner.Measurements.ScopeAllocations,planner.Measurements.SamePhaseNoops},Ranges=planner.Measurements.Ranges(),PhysicalCounters=planner.Runtime.Counters(),Review=Evidence152.Compact(review)});Console.WriteLine(label+" "+mode+" CPU="+cpu+" hash="+hash[..12]);if(!exact)throw new InvalidOperationException("Current physical state/action/certificate differs from archived161: actual="+hash+" expected="+context.ArchivedHash);
   }
   var kinds=label=="m2-tight"?new[]{"empty-loop","clock","empty-lease","scope"}:new[]{"clock","empty-loop","scope","empty-lease"};
   foreach(var kind in kinds){budget.Guard();int count=kind is "scope" or "empty-lease"?20000:100000;var p=new CpuProfile163();long before=process.TotalProcessorTime.Ticks,bytes=GC.GetAllocatedBytesForCurrentThread(),checksum=0;var wall=Stopwatch.StartNew();for(int i=0;i<count;i++){if((i&1023)==0)budget.Guard();switch(kind){case "clock":checksum^=process.TotalProcessorTime.Ticks;break;case "empty-loop":checksum^=i;break;case "scope":using(p.Phase("conditional_continuations")){checksum^=i;}break;default:using(CpuProfile163.EmptyScope){checksum^=i;}break;}}wall.Stop();clocks.Add(new{Kind=kind,Iterations=count,CpuSeconds=(process.TotalProcessorTime.Ticks-before)/(double)TimeSpan.TicksPerSecond,WallSeconds=wall.Elapsed.TotalSeconds,AllocatedBytesOnCurrentThread=GC.GetAllocatedBytesForCurrentThread()-bytes,Checksum=checksum,ProfilerReads=p.ClockReads});}
   Save("micro/"+label+".json.gz",new{Label=label,PreparationCpuSeconds=preparation,Context=new{Native=row.GetProperty("Native").GetString(),Decision=row.GetProperty("Decision").GetInt32(),context.Observed,context.PreviousReconstructedHash,context.PreviousArchivedHash,context.ArchivedHash},Trials=trials,Calibration=clocks,Error=error,Scope="Microbenchmark actual full retained-policy review; not a native world. Exact full13/9 + independent physics, guards unchanged in all modes."});
  }catch(Exception e){error=e.ToString();Save("micro/"+label+".json.gz",new{Label=label,Trials=trials,Calibration=clocks,Error=error,Scope="Failed microbenchmark retained; no native admission"});Console.Error.WriteLine(error);}
  budget.Finish(error??"completed controlled counterbalanced microbenchmark");
 }
}
