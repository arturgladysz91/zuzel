using Autonomous146;
using Robust150;
using Safety153;
using SharedWorldSpeedway;
namespace ThreeLap158;
public sealed record Generation158(string Origin,bool Legal,double? Arrival,int Commands,double EndReference,double MinimumGap,double Sweep,string Reason);
/// <summary>State-feedback reserve generation. Controls are calculated online from
/// each propagated state and its actual pending queue, with no historical data.</summary>
public sealed class Generator158 {
 readonly Func<State146,FreeRacingAction,string,Transition146> step;readonly Action guard;
 public Control158 Control{get;}public const int MaximumReviews=450;
 public long PrimitiveCandidates,PhysicalSteps,PrefixRejections,DiversityPruned,LocalForceProbes,ForwardStageChecks,ForwardStageDeadEnds,ForwardStageWitnesses;public int FullPlans;public int DistinctLegalStates=>states.Count;readonly HashSet<string> states=new();
 public List<Generation158> Outcomes{get;}=new();public List<object> Rejections{get;}=new();public Tape150? FurthestPartial{get;private set;}
 public Generator158(Runtime158 rt,Func<State146,FreeRacingAction,string,Transition146> step,Action guard,Settings158? settings=null){Control=new(rt,settings??new(SpeedMetersPerSecond:10,CourseSeconds:1));this.step=step;this.guard=guard;}
 public void Begin(){Control.Begin();Outcomes.Clear();Rejections.Clear();states.Clear();FurthestPartial=null;PrimitiveCandidates=PhysicalSteps=PrefixRejections=0;FullPlans=0;}
 public Tape150[] Generate(State146 start,double goal,double safetyGoal,string origin,int maximumPlans=1){PrimitiveCandidates++;var s=start;var observations=new List<State146>{s};var actions=new List<FreeRacingAction>();var points=new List<FreeRacingPoint>();double? arrival=null;string reason="full original terminal section";
  for(int k=0;k<MaximumReviews;k++){guard();var action=Control.Action(s);var q=step(s,action,"causal_reserve_generation");PhysicalSteps++;observations.Add(q.End);actions.Add(action);foreach(var p in q.Prediction.Points.Skip(points.Count==0?0:1)){if(arrival is null&&p.Rider.TotalReferenceMeters>=goal&&points.Count>0){var a=points[^1];arrival=a.Time+(p.Time-a.Time)*Math.Clamp((goal-a.Rider.TotalReferenceMeters)/(p.Rider.TotalReferenceMeters-a.Rider.TotalReferenceMeters),0,1);}points.Add(p);}s=q.End;var safety=Clearance153.Check(q.Prediction.Points,Regime153.SafetyHalfMeter);if(!safety.Legal){reason=safety.Reason;Rejections.Add(new{Observed=s,Action=action,Reason=reason});PrefixRejections++;break;}states.Add(Efficient147.ExactKey147.State(s));if(s.Input.Rider.TotalReferenceMeters>=safetyGoal)break;}
  var check=Clearance153.Check(points.ToArray(),Regime153.SafetyHalfMeter);if(s.Input.Rider.TotalReferenceMeters<safetyGoal||arrival is null)check=check with{Legal=false,Reason=reason=="full original terminal section"?"90s full terminal incomplete":reason};var tape=new Tape150(start.Input.Time,goal,safetyGoal,actions.ToArray(),observations.ToArray(),points.ToArray(),arrival,check,origin);FurthestPartial=tape;Outcomes.Add(new(origin,check.Legal,arrival,actions.Count,s.Input.Rider.TotalReferenceMeters,check.MinimumGapMeters,check.SweepLowerBoundMeters,check.Reason));if(check.Legal){FullPlans++;return new[]{tape};}return Array.Empty<Tape150>();
 }
}
