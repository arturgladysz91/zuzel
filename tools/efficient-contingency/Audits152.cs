using System.Diagnostics;
using System.Text.Json;
using Autonomous146;
using Contingency151;
using Efficient147;
using Robust150;
using SharedWorldSpeedway;
namespace Efficient152;
public sealed record Reference152(double Time,string InputKey,string PhysicalHash,FreeRacingAction Action,object Counts,double CpuSeconds,int Nodes,int Leaves,int RejectedTrees);
public static class Audits152 {
 public const string ReferenceName="contingency151-gate4-A-v2";
 public static JsonElement[] ReferenceJournal()=>Journal147.ReadComplete(Program151.Doc+"/native/"+ReferenceName+"-decisions.jsonl").Where(x=>x.GetProperty("Event").GetString()=="decision").ToArray();
 public static Review151 ReferenceReview(int index)=>Program152.Read<Review151>(Program151.Doc+"/trees/"+ReferenceName+"/decision-"+index.ToString("D4")+".json.gz");
 public static void Baseline(){using var budget=new Budget152("saved151-baseline-digests-and-costs",0,30);var rows=new List<Reference152>();var journal=ReferenceJournal();
  for(int k=0;k<journal.Length;k++){budget.Guard();var r=ReferenceReview(k);var row=new Reference152(r.Time,ExactKey147.State(r.Observed),Bits152.Review(r),journal[k].GetProperty("Selected").Deserialize<FreeRacingAction>(Program152.Json),r.Counts,r.CpuSeconds,r.Tree.Length,r.Terminals.Length,r.RejectedTrees.Length);rows.Add(row);}
  Program152.Save("baseline151.json",rows);Program152.Save("baseline151-source.json",new{Base=Program152.Base,NativeFile=Program151.Doc+"/native/"+ReferenceName+".json.gz",NativeSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program151.Doc+"/native/"+ReferenceName+".json.gz"))),AcceptedDecisions=rows.Count,AcceptedDecisionCpuSeconds=rows.Sum(x=>x.CpuSeconds),Scope="Saved native states, full physical forecast/certificate digest, counters; no old simulation or controller rerun, no new world"});budget.Finish("immutable151 native prefix and complete physical certificate digests");Console.WriteLine("baseline "+rows.Count+" decisions CPU="+budget.Elapsed);
 }
 public static void Bench(string label,bool validated=false){using var budget=new Budget152("single-decision-exact-"+label,0,100);var native=Program152.Read<Run144>(Program151.Doc+"/native/"+ReferenceName+".json.gz");var rows=new List<object>();
  foreach(var index in new[]{0,2,13,50,149}){
   budget.Guard();var old=ReferenceReview(index);var planner=new Planner152(native.Riders[0].Spec,false,new(Enabled:true),budget.Guard,performance:new(ValidatedIndependent:validated));if(index>0)planner.InitializeDiagnosticReview(ReferenceReview(index-1));
   string? failure=null;try{planner.Choose(old.Observed.Input,new(omega:old.Observed.CrankOmega));}catch(InvalidOperationException error){failure=error.Message;}
   var equal=failure is null&&Bits152.Review(old)==Bits152.Review(planner.Last!);rows.Add(new{Time=old.Time,Failure=failure,AllPhysicalBitsEqual=equal,OldCpu=old.CpuSeconds,NewCpu=planner.Last?.CpuSeconds,Profile=planner.Last?.Profile,Counts=planner.Runtime.Counters(),RejectedTrees=planner.Last?.RejectedTrees.Length,Leaves=planner.Last?.Terminals.Length});
   Console.WriteLine("t="+old.Time+" exact="+equal+" old="+old.CpuSeconds+" new="+planner.Last?.CpuSeconds+" "+failure);
   if(!equal){Program152.Save("bench-"+label+"-mismatch.json.gz",new{Old=old,New=planner.Last,Failure=failure});break;}
  }
  Program152.Save("bench-"+label+".json",rows);budget.Finish("bounded exact physical/certificate comparison; inspect equality rows");
 }
}
