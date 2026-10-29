using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
using Speed148;
namespace Limits149;
/// <summary>Re-evaluate actual accepted observations. Forecast CPU is charged; no native world or state writes.</summary>
public static class LocalAudit149{
 public static void Run(){using var b=new Budget149("planned-entry-replanning-forensics",0,90);var saved=new List<object>();
  foreach(var separated in new[]{false,true}){
   var name="planned-entry-90-outer-"+(separated?"B":"A");var run=Program149.Read<Run144>(Program149.Doc+"/fixtures/"+name+".json.gz");var records=Journal147.ReadComplete(Program149.Doc+"/fixtures/"+name+"-decisions.jsonl");var spec=run.Riders[0].Spec;var f=StandingWorld144.Frame();var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(f);var options=new RuntimeOptions147(EventSearch:true,WorkBudget:true,ProposalsPerThrottle:2,MaximumActualForecasts:8192,MaximumContacts:700000,OptionalSearchContacts:500000);var a=separated?null:new ExactRuntime148(spec,f,surface,p,options);var rt=separated?new ExactRuntime149(spec,f,surface,p,options):null;
   var planner=new Planner146(spec,f,surface,p,new(Enabled:true,RequiredLaps:1),budgetGuard:b.Guard,hooks:separated?rt!.Hooks():a!.Hooks());var reviews=new List<Review146>();bool same=true;string? failure=null;
   var decisions=records.Where(r=>r.GetProperty("Event").GetString()=="decision").ToArray();
   foreach(var row in decisions){var input=row.GetProperty("Observed").Deserialize<FreeRacingInput>(Program149.Json)!;var crank=row.GetProperty("CrankOmega").GetDouble();var d=planner.Choose(input,new(omega:crank));same&=d.Choice.Action==row.GetProperty("Selected").Deserialize<FreeRacingAction>(Program149.Json);var review=planner.Reviews[^1];reviews.Add(review);if(separated)rt!.Completed(input,review.Selected!);else a!.Completed(input,review.Selected!);planner.Reviews.Clear();}
   var final=run.Riders[0].Samples[^1];try{planner.Choose(final.Input with{NextReview=final.Input.Time},new(omega:final.CrankOmega));}catch(InvalidOperationException error){failure=error.Message;}
   reviews.AddRange(planner.Reviews);if(!same||failure is null)throw new InvalidOperationException("Saved local planner decisions did not reproduce.");var last=reviews[^1];
   saved.Add(new{Name=name,NativeWorlds=0,PrefixChoicesExact=same,Failure=failure,Reviews=reviews,FailedReasonCounts=last.Alternatives.GroupBy(c=>c.Reason).ToDictionary(g=>g.Key,g=>g.Count()),CandidateCount=last.Alternatives.Length,Observed=last.Input,ActualOutcome=run.Outcome,Scope="Diagnostic re-evaluation from detached accepted observations; no future observation provided to any decision"});Console.WriteLine(name+": "+string.Join(", ",last.Alternatives.GroupBy(c=>c.Reason).Select(g=>g.Key+"="+g.Count())));
  }
  Program149.Save("local-replanning-audit.json.gz",saved);b.Finish("all returned native choices and final abstention reproduced; rejection evidence saved");
 }
}
