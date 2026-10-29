using System.Text.Json;
using Efficient147;
using SharedWorldSpeedway;
namespace Robust150;
public static class PhaseAudit150{
 public static void Run(){using var budget=new Budget150("phase-indexed-versus-clock-feedback",0,100);var old=Program150.Read<Run144>(Program150.Doc+"/native/robust150-tube-full-A.json.gz");var records=Journal147.ReadComplete(Program150.Doc+"/native/robust150-tube-full-A-decisions.jsonl");var row=records.Last(x=>x.GetProperty("Event").GetString()=="decision");var anchor=Program150.Read<Tape150>(Program150.Doc+"/plans/robust150-tube-full-A/"+row.GetProperty("Anchor").GetString()+".json.gz");var sample=old.Riders[0].Samples[^1];var output=new List<object>();
  foreach(var spatial in new[]{false,true}){
   var planner=new Planner150(old.Riders[0].Spec,false,new(Enabled:true,Feedback:true,SpatialFeedback:spatial),budget.Guard);planner.InitializeDiagnosticTape(anchor);string? failure=null;try{planner.Choose(sample.Input,new(omega:sample.CrankOmega));}catch(InvalidOperationException error){failure=error.Message;}output.Add(new{Spatial=spatial,Failure=failure,Review=planner.Last});Console.WriteLine("spatial="+spatial+" "+(failure??"certified"));
  }
  Program150.Save("phase-feedback-audit.json.gz",output);budget.Finish("controlled same-state/reference comparison complete");
 }
}
