using System.Diagnostics;
using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
namespace Speed148;
public static class Compare148{
 public static readonly RuntimeOptions147 Options=new(EventSearch:true,WorkBudget:true,ProposalsPerThrottle:2,MaximumActualForecasts:8192,MaximumContacts:700000,OptionalSearchContacts:500000);
 public static JsonElement[] Journal()=>Journal147.ReadComplete("docs/experiments/efficient-autonomous-planner/native/autonomous147-shortlist-decisions.jsonl").Where(e=>e.GetProperty("Event").GetString()=="decision").ToArray();
 public static State146 State(JsonElement e)=>new(e.GetProperty("Observed").Deserialize<FreeRacingInput>(Program.Json)!,e.GetProperty("CrankOmega").GetDouble());
 public static Memory146 Before(JsonElement previous,RiderSpec spec,TrackFrame f,CoreSim.TrackStateSnapshot surface,WorldParameters p){
  var start=State(previous);var actions=previous.GetProperty("PlannedActions").Deserialize<FreeRacingAction[]>(Program.Json)!;
  var plan=new Plan146(start.Input.Time,previous.GetProperty("GoalMeters").GetDouble(),previous.GetProperty("SafetyGoalMeters").GetDouble(),actions,Array.Empty<FreeRacingPoint>(),previous.GetProperty("PredictedEnd").Deserialize<State146>(Program.Json)!,previous.GetProperty("GoalTime").GetDouble(),previous.GetProperty("CornerExitTime").ValueKind==JsonValueKind.Number?previous.GetProperty("CornerExitTime").GetDouble():null,previous.GetProperty("CornerExitSpeed").ValueKind==JsonValueKind.Number?previous.GetProperty("CornerExitSpeed").GetDouble():null,previous.GetProperty("Certificate").Deserialize<Safety146>(Program.Json)!,previous.GetProperty("Origin").GetString()!);
  var expected=new Model146(spec,f,surface,p).Step(start,actions[0]).End;return new(plan,expected,start.Input.Time+1.2);
 }
 public static void Run(){
  var saved=Journal();var run=Program.Baseline();var f=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(f);var p=FreeLearningExperiment.Parameters();var spec=run.Riders[0].Spec;
  foreach(var at in new[]{0d,22.6,30d})foreach(var mode in new[]{"A","B"}){
   var path=Program.Doc+$"/decision-{mode}-{at:0.0}.json";if(File.Exists(path))continue;using var budget=new Budget148($"decision-{mode}-{at:0.0}",0,100);
   var index=Array.FindIndex(saved,e=>Math.Abs(e.GetProperty("Time").GetDouble()-at)<1e-7);var state=State(saved[index]);var memory=index==0?null:Before(saved[index-1],spec,f,surface,p);
   var runtime=new ExactRuntime148(spec,f,surface,p,Options,cheapTail:mode=="B");var planner=new Planner146(spec,f,surface,p,new(Enabled:true),budgetGuard:budget.Guard,hooks:runtime.Hooks());
   if(memory is not null){planner.RestoreMemoryForDiagnostics(memory);runtime.InitializeHistory(State(saved[index-1]).Input,memory.Previous);}
   string? error=null;Decision144? decision=null;var alloc=GC.GetAllocatedBytesForCurrentThread();try{decision=planner.Choose(state.Input,new CrankClutch144(omega:state.CrankOmega));}catch(InvalidOperationException e){error=e.Message;}
   var selected=planner.Reviews.LastOrDefault()?.Selected;var exact=selected is not null&&decision!.Choice.Action==saved[index].GetProperty("Selected").Deserialize<FreeRacingAction>(Program.Json)&&ExactKey147.State(selected.End)==ExactKey147.State(saved[index].GetProperty("PredictedEnd").Deserialize<State146>(Program.Json)!);
   var result=new{Mode=mode,At=at,Outcome=decision is null?"abstain":"certified",Error=error,Command=decision?.Choice.Action,ExactSelectedEndAndCommand=exact,GoalTime=selected?.GoalTime,ReferenceGoalTime=saved[index].GetProperty("GoalTime").GetDouble(),CpuSeconds=budget.Elapsed,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-alloc,Counts=runtime.Counters(),Profile=runtime.Profile?.Snapshot(),Review=planner.Reviews.LastOrDefault()};
   Program.Save(path,result);budget.Finish(result.Outcome);Console.WriteLine(JsonSerializer.Serialize(new{mode,at,result.Outcome,exact,result.CpuSeconds},Program.Json));Console.Out.Flush();
   if(mode=="A"&&!exact)throw new InvalidOperationException("Exact A diverged; stop before native.");
  }
 }
}
