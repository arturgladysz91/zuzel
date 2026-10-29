using System.Text.Json;
using Contingency151;
using SharedWorldSpeedway;
namespace Efficient152;
public static class Held152 {
 public static void Run(){using var budget=new Budget152("frozen-B-held-states-before-full-run",0,45);var rows=new List<object>();foreach(var time in new[]{39.4,45.8}){
  budget.Guard();var input=Program152.Read<JsonElement>(Program151.Doc+"/probes/probe-B-"+time.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".json.gz");var old=input.GetProperty("Review").Deserialize<Review151>(Program152.Json)!;var run=Program152.Read<Run144>(Robust150.Diagnosis150.Previous+"/native/separated149-autonomous.json.gz");var planner=new Planner152(run.Riders[0].Spec,true,new(Enabled:true),budget.Guard,performance:new(ValidatedIndependent:true));planner.InitializeDiagnosticReview(old);string? failure=null;
  try{planner.Choose(old.Observed.Input,new(omega:old.Observed.CrankOmega));}catch(InvalidOperationException error){failure=error.Message;}Program152.Save("held/B-"+time.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".json.gz",new{Failure=failure,Policy=planner.Last is {} last?Evidence152.Compact(last):null});rows.Add(new{Time=time,Certified=planner.Last?.ConditionalCertificate==true,Failure=failure,CpuSeconds=planner.Last?.CpuSeconds,Scope="Frozen previously held-out B state. Old controls supplied as a proposal only; all scenarios and reserves regenerated and independently certified. No native completion claim."});Console.WriteLine("held B "+time+" "+(failure??"certified"));if(failure is not null)break;
 }Program152.Save("held-B.json",rows);budget.Finish("bounded frozen B held-state certification; inspect individual results");}
}
