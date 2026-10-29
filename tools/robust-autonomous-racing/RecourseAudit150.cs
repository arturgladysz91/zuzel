using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
namespace Robust150;
/// <summary>Measured errors belong to the next prediction, not to an already accepted observation.
/// Test whether a finite portfolio covers those next observations with the irrevocable root queue.</summary>
public static class RecourseAudit150{
 public static void Run(){using var budget=new Budget150("next-observation-portfolio-recourse",0,60);var name="robust150-tube-full-A";var run=Program150.Read<Run144>(Program150.Doc+"/native/"+name+".json.gz");var rows=Journal147.ReadComplete(Program150.Doc+"/native/"+name+"-decisions.jsonl");var previous=rows.Last(x=>x.GetProperty("Event").GetString()=="decision");var old=Program150.Read<Tape150>(Program150.Doc+"/plans/"+name+"/"+previous.GetProperty("Anchor").GetString()+".json.gz");var sample=run.Riders[0].Samples[^1];var state=new State146(sample.Input,sample.CrankOmega);var spec=run.Riders[0].Spec;var planner=new Planner150(spec,false,new(Enabled:true,Feedback:true),budget.Guard);var rt=planner.Runtime;rt.Begin(state.Input.Time);var retained=Feedback150.Evaluate(rt,state,old,old.Goal,old.SafetyGoal,"retained",budget.Guard);var fresh=Rollout150.Evaluate(rt,state,Array.Empty<FreeRacingAction>(),old.Goal,old.SafetyGoal,"fresh",generateTail:true,guard:budget.Guard);var anchors=new[]{old,fresh};var output=new List<object>();
  foreach(var root in new[]{retained,fresh}.Where(p=>p.Safety.Legal)){
   var next=rt.Step(state,root.Actions[0]).End;var cases=new List<object>();bool covered=true;
   foreach(var sign in new[]{0,1,-1}){var child=sign==0?next:planner.ChallengeForDiagnostics(next,sign);var alternatives=anchors.Select((anchor,index)=>{var tape=Feedback150.Evaluate(rt,child,anchor,old.Goal,old.SafetyGoal,"recourse-"+index,budget.Guard);return new{Index=index,tape.Safety,tape.Arrival,EndReference=tape.End.Input.Rider.TotalReferenceMeters};}).ToArray();var exists=alternatives.Any(x=>x.Safety.Legal);covered&=exists;cases.Add(new{Sign=sign,Queue=child.Input.Pending,Covered=exists,Alternatives=alternatives});}
   output.Add(new{Root=root.Origin,RootAction=root.Actions[0],AllNextScenariosCovered=covered,Cases=cases});Console.WriteLine(root.Origin+" covered="+covered);
  }
  Program150.Save("next-observation-recourse-audit.json",output);budget.Finish("finite next-step recourse audit complete");
 }
}
