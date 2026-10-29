using Autonomous146;
using SharedWorldSpeedway;
namespace Robust150;
public sealed record Tape150(double CreatedAt,double Goal,double SafetyGoal,FreeRacingAction[] Actions,State146[] States,FreeRacingPoint[] Points,double? Arrival,Safety146 Safety,string Origin){
 public double Seconds=>Arrival is double t?t-CreatedAt:double.PositiveInfinity;
 public State146 End=>States[^1];
}
public static class Rollout150{
 public static Tape150 Evaluate(Runtime150 rt,State146 start,FreeRacingAction[] tape,double goal,double safetyGoal,string origin,bool generateTail=false,bool regenerateAll=false,bool early=true,Action? guard=null,Func<State146,int,FreeRacingAction>? feedback=null){
  var states=new List<State146>{start};var points=new List<FreeRacingPoint>();var actions=new List<FreeRacingAction>();double? arrival=null;var state=start;
  for(int k=0;k<130;k++){
   guard?.Invoke();if(k>=tape.Length&&!generateTail&&!regenerateAll)break;var action=regenerateAll||k>=tape.Length?rt.Seed(state).Choice.Action:feedback is null?tape[k]:feedback(state,k);var t=rt.Step(state,action);actions.Add(action);states.Add(t.End);
   foreach(var point in t.Prediction.Points.Skip(k==0?0:1)){if(arrival is null&&point.Rider.TotalReferenceMeters>=goal&&points.Count>0){var prev=points[^1];var delta=point.Rider.TotalReferenceMeters-prev.Rider.TotalReferenceMeters;arrival=prev.Time+(point.Time-prev.Time)*Math.Clamp((goal-prev.Rider.TotalReferenceMeters)/delta,0,1);}points.Add(point);}
   state=t.End;if(state.Input.Rider.TotalReferenceMeters>=safetyGoal)break;if(early&&!Planner146.Check(t.Prediction.Points,.1).Legal)break;
  }
  var safety=Planner146.Check(points.ToArray(),.1);if(arrival is null||state.Input.Rider.TotalReferenceMeters<safetyGoal)safety=safety with{Legal=false,Reason=safety.Legal?"terminal continuation not completed":safety.Reason};return new(start.Input.Time,goal,safetyGoal,actions.ToArray(),states.ToArray(),points.ToArray(),arrival,safety,origin);
 }
}
