using Autonomous146;
using LineThrottle156;
using Safety153;
using SharedWorldSpeedway;
namespace Transfer157;
/// <summary>Forensics only. Saved teacher states/actions never enter Policy157 or
/// Planner157. Counterfactual queue swaps are forecasts, never native initialisation.</summary>
public static class Diagnostics157 {
 static bool SameBody(State146 a,State146 b){var x=a.Input.Rider;var y=b.Input.Rider;return x.Position==y.Position&&x.Velocity==y.Velocity&&x.Heading==y.Heading&&x.AngularVelocity==y.AngularVelocity&&a.Input.Omega==b.Input.Omega&&a.Input.Steering==b.Input.Steering&&a.CrankOmega==b.CrankOmega&&a.Input.Active==b.Input.Active;}
 public static void Run(string label){using var budget=new Budget157(label,0,100);var rows=new List<object>();foreach(var pair in new[]{("held-first","first-A-T1-final"),("held-first","first-B-T1-refined"),("held-second","second-B-T1-refined"),("held-second","second-C-T1-refined")}){
  var f=Study156.Fixtures().Single(f=>f.Name==pair.Item1);var run=Study156.Read<Run144>(Study156.Doc+"/native/"+pair.Item2+".json.gz").Riders[0];var sample=run.Samples.Single(s=>Math.Abs(s.Input.Time-f.Time)<1e-7);var teacher=new State146(sample.Input,sample.CrankOmega);var rt=new Runtime153(f.Spec,false,Hierarchical155.Program155.Options());rt.Begin(f.Time);
  var low=rt.Step(f.State,new(0,0));var high=rt.Step(f.State,new(.1f,1));var queued=rt.Step(teacher,new(0,0));var actions=run.Decisions.Where(d=>d.Choice.Time>=f.Time-1e-8).Select(d=>d.Choice.Action).ToArray();
  object Roll(State146 start){var s=start;var points=new List<FreeRacingPoint>();string reason="complete common156 section";foreach(var action in actions){budget.Guard();var q=rt.Step(s,action);points.AddRange(q.Prediction.Points.Skip(points.Count==0?0:1));s=q.End;var c=Clearance153.Check(q.Prediction.Points,Regime153.SafetyHalfMeter);if(!c.Legal){reason=c.Reason;break;}if(s.Input.Rider.TotalReferenceMeters>=f.Goal)break;}var safety=Clearance153.Check(points.ToArray(),Regime153.SafetyHalfMeter);return new{End=s,Reason=reason,Safety=safety,Complete=s.Input.Rider.TotalReferenceMeters>=f.Goal,Points=points};}
  rows.Add(new{Fixture=f.Name,Teacher=pair.Item2,StartBodiesIdentical=SameBody(f.State,teacher),ObservedPending=f.State.Input.Pending,TeacherPending=teacher.Input.Pending,NewActionsCannotChangeNextObservation=SameBody(low.End,high.End),QueueOnlyEffectAtNextObservation=new{Position=System.Numerics.Vector2.Distance(low.End.Input.Rider.Position,queued.End.Input.Rider.Position),Velocity=System.Numerics.Vector2.Distance(low.End.Input.Rider.Velocity,queued.End.Input.Rider.Velocity),Yaw=low.End.Input.Rider.AngularVelocity-queued.End.Input.Rider.AngularVelocity,Beta=low.End.Input.Rider.SlideRadians-queued.End.Input.Rider.SlideRadians,Wheel=low.End.Input.Omega-queued.End.Input.Omega},ObservedQueueRollout=Roll(f.State),TeacherQueueCounterfactual=Roll(teacher),Scope="Saved-state causal forecast; teacher counterfactual not a native run and not a certified157 policy"});
 }
 Program157.Save(label+".json.gz",rows);budget.Finish("pending causality diagnostic only; zero native worlds");}
}
