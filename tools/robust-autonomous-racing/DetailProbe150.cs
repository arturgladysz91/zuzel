using System.Text.Json;
using Autonomous146;
using SharedWorldSpeedway;
namespace Robust150;
public static class DetailProbe150{
 public static void Run(){using var budget=new Budget150("critical22-new-planner-complete-alternatives",0,100);var output=new List<object>();foreach(var model in new[]{false,true}){
  var rows=StateProbes150.Journal(model);var old=rows.Single(x=>Math.Abs(x.GetProperty("Time").GetDouble()-22.4)<1e-7);var row=rows.Single(x=>Math.Abs(x.GetProperty("Time").GetDouble()-22.6)<1e-7);var state=StateProbes150.State(row);var before=StateProbes150.State(old);var run=Program150.Read<Run144>(Diagnosis150.Previous+"/native/"+(model?"separated149-autonomous":"baseline149-final148")+".json.gz");var rt=new Runtime150(run.Riders[0].Spec,model,false);rt.Begin(before.Input.Time);var tape=Rollout150.Evaluate(rt,before,old.GetProperty("PlannedActions").Deserialize<FreeRacingAction[]>(Program150.Json)!,old.GetProperty("GoalMeters").GetDouble(),old.GetProperty("SafetyGoalMeters").GetDouble(),"own-prior-certification",guard:budget.Guard);var planner=new Planner150(run.Riders[0].Spec,model,new(Enabled:true,Feedback:true),budget.Guard);planner.InitializeDiagnosticTape(tape);string? failure=null;try{planner.Choose(state.Input,new(omega:state.CrankOmega));}catch(InvalidOperationException error){failure=error.Message;}output.Add(new{Model=model?"B":"A",Failure=failure,Review=planner.Last,Scope="New planner supplied only the accepted observation/queue and reconstructed own previous certified maneuver; no future native states."});Console.WriteLine((model?"B":"A")+" "+(failure??"certified"));
  }Program150.Save("critical22-new-planner.json.gz",output);budget.Finish("all new alternative command sequences saved at critical22.6");
 }
}
