using System.Numerics;
using System.Text.Json;
using Autonomous146;
using SharedWorldSpeedway;
namespace Robust150;
public static class Diagnosis150{
 public const string Previous="docs/experiments/tyre-physics-corner-limits";
 public static State146 Perturb(State146 predicted,State146 observed,TrackFrame frame,string group,double scale){
  var v=predicted.Input.Rider;var w=observed.Input.Rider;bool Has(string g)=>group=="all"||group==g;
  var pos=Has("position")?v.Position+(w.Position-v.Position)*(float)scale:v.Position;var road=frame.Project(pos);var reference=v.TotalReferenceMeters+frame.UnwrapDelta(v.Road.ReferenceMeters,road.ReferenceMeters);
  var rider=v with{Position=pos,Road=road,TotalReferenceMeters=reference,Context=frame.Context(road,reference),Velocity=Has("velocity")?v.Velocity+(w.Velocity-v.Velocity)*(float)scale:v.Velocity,Heading=Has("heading")?v.Heading+(w.Heading-v.Heading)*(float)scale:v.Heading,AngularVelocity=Has("yaw")?v.AngularVelocity+(w.AngularVelocity-v.AngularVelocity)*(float)scale:v.AngularVelocity};
  return new(predicted.Input with{Rider=rider,Omega=Has("wheel")?predicted.Input.Omega+(observed.Input.Omega-predicted.Input.Omega)*scale:predicted.Input.Omega},Has("crank")?predicted.CrankOmega+(observed.CrankOmega-predicted.CrankOmega)*scale:predicted.CrankOmega);
 }
 public static void Run(){using var budget=new Budget150("causal-plan-tail-diagnosis-safe-window",0,100);var archive=Program150.Read<JsonElement>(Previous+"/local-replanning-audit.json.gz");var summaries=new List<object>();
  foreach(var model in new[]{false,true}){
   var key=model?"B":"A";var item=archive.EnumerateArray().Single(q=>q.GetProperty("Name").GetString()!.EndsWith(key,StringComparison.Ordinal));var review=item.GetProperty("Reviews")[4];var plan=review.GetProperty("Selected").Deserialize<Plan146>(Program150.Json)!;var native=Program150.Read<Run144>(Previous+"/fixtures/planned-entry-90-outer-"+key+".json.gz");var spec=native.Riders[0].Spec;var rt=new Runtime150(spec,model,false);rt.Begin(.8);var start=new State146(review.GetProperty("Input").Deserialize<FreeRacingInput>(Program150.Json)!,review.GetProperty("CrankOmega").GetDouble());
   var expected=rt.Step(start,plan.Actions[0]).End;var sample=native.Riders[0].Samples[^1];var actual=new State146(sample.Input with{NextReview=sample.Input.Time},sample.CrankOmega);var suffix=plan.Actions.Skip(1).ToArray();var cases=new List<(string Name,State146 State,FreeRacingAction[] Tape,bool Tail,bool All)> {("A-original-fixed",start,plan.Actions,false,false),("B-predicted-shift-fixed",expected,suffix,false,false),("C-observed-shift-fixed",actual,suffix,false,false),("D-observed-shift-append-K1",actual,suffix,true,false),("D-fresh-K1",actual,Array.Empty<FreeRacingAction>(),true,true)};
   foreach(var group in new[]{"position","velocity","heading","yaw","wheel","crank","all"})foreach(var sign in new[]{-1d,1d})cases.Add(($"E-{group}-{sign}",Perturb(expected,actual,rt.Frame,group,sign),suffix,false,false));
   var results=new Dictionary<string,Tape150>();
   foreach(var c in cases){budget.Guard();rt.Begin(c.State.Input.Time);var t=Rollout150.Evaluate(rt,c.State,c.Tape,plan.GoalMeters,plan.SafetyGoalMeters,c.Name,c.Tail,c.All,early:true,guard:budget.Guard);results[c.Name]=t;Program150.Save("diagnosis-safe/"+key+"-"+c.Name+".json.gz",t);Console.WriteLine(key+" "+c.Name+" "+t.Safety.Reason+" beta="+t.Safety.MaximumBetaDegrees);}
   var baseline=results["B-predicted-shift-fixed"];var trace=new List<object>();
   foreach(var (name,t) in results){var firstAction=Enumerable.Range(0,Math.Min(t.Actions.Length,baseline.Actions.Length)).FirstOrDefault(i=>t.Actions[i]!=baseline.Actions[i],-1);var comparison=t.States.Skip(1).Zip(baseline.States.Skip(1)).Select((q,i)=>new{Step=i,Time=q.First.Input.Time,PositionError=Vector2.Distance(q.First.Input.Rider.Position,q.Second.Input.Rider.Position),BetaError=Math.Abs(q.First.Input.Rider.SlideRadians-q.Second.Input.Rider.SlideRadians),YawError=Math.Abs(q.First.Input.Rider.AngularVelocity-q.Second.Input.Rider.AngularVelocity)}).ToArray();var firstGrowing=comparison.FirstOrDefault(q=>q.PositionError>.001||q.BetaError>.001);var firstUnsafe=t.Points.FirstOrDefault(q=>!Planner146.Check(new[]{q},.1).Legal);trace.Add(new{Name=name,t.Safety,Steps=t.Actions.Length,t.Arrival,EndReference=t.End.Input.Rider.TotalReferenceMeters,FirstDifferentAction=firstAction<0?null:new{Step=firstAction,Time=t.CreatedAt+firstAction*.2,Action=t.Actions[firstAction],Baseline=baseline.Actions[firstAction]},FirstGrowingError=firstGrowing,FirstUnsafe=firstUnsafe,Errors=comparison});}
   var oldMatch=results["A-original-fixed"].Points.Zip(plan.Points).All(q=>JsonSerializer.Serialize(q.First,Program150.Json)==JsonSerializer.Serialize(q.Second,Program150.Json));summaries.Add(new{Model=key,OriginalForecastExactlyReproduced=oldMatch,QueueEqual=JsonSerializer.Serialize(expected.Input.Pending,Program150.Json)==JsonSerializer.Serialize(actual.Input.Pending,Program150.Json),Expected=expected,Observed=actual,Cases=trace,Counts=rt.Counters()});
  }
  Program150.Save("diagnosis-summary.json",summaries);budget.Finish("A-E counterfactuals complete; no native worlds");
 }
}
