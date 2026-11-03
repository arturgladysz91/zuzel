using Autonomous146;
using Robust150;
using Safety153;
using SharedWorldSpeedway;
using ThreeLap158;
namespace Continuation161;
public sealed record Generation161(string Maneuver,bool Legal,double? Arrival,int Commands,double EndReference,double MinimumGap,double Sweep,string Reason,FreeRacingAction First,double CpuSeconds);
public sealed class Generator161 {
 readonly Runtime158 rt;readonly Func<State146,FreeRacingAction,string,Transition146> step;readonly Action guard;
 public Control161 Control{get;private set;}public const int MaximumReviews=450;
 public long PrimitiveCandidates,PhysicalSteps,PrefixRejections,DiversityPruned,LocalForceProbes,ForwardStageChecks,ForwardStageDeadEnds,ForwardStageWitnesses;public int FullPlans;public int DistinctLegalStates=>states.Count;readonly HashSet<string> states=new();
 public List<Generation161> Outcomes{get;}=new();public List<object> Rejections{get;}=new();public List<Tape150> Evaluated{get;}=new();public Tape150? FurthestPartial{get;private set;}
 public Generator161(Runtime158 rt,Func<State146,FreeRacingAction,string,Transition146> step,Action guard,Settings161? settings=null){this.rt=rt;this.step=step;this.guard=guard;Control=new(rt,settings??new(Enabled:true,Adaptive:true));}
 public void Begin(){Control.Begin();Outcomes.Clear();Rejections.Clear();Evaluated.Clear();states.Clear();FurthestPartial=null;PrimitiveCandidates=PhysicalSteps=PrefixRejections=0;FullPlans=0;}
 public Tape150[] Generate(State146 start,double goal,double safetyGoal,string origin,int maximumPlans=1){
  var spatial=Space161.Observe(start,rt.Frame);double width=rt.Frame.PhysicalWidthMeters(start.Input.Rider.Road);
  double prepareLine=Math.Clamp(spatial.ProjectedOffset,2.5,width-3);
  double recoveryLine=spatial.Inner<spatial.Outer?2.5:width-3;
  var policies=new[]{new Settings161(Enabled:true,Adaptive:true),new Settings161(Enabled:true,Adaptive:true,ForceFraction:.42,OffsetMeters:prepareLine,Maneuver:Maneuver161.Prepare),new Settings161(Enabled:true,Adaptive:true,ForceFraction:.32,OffsetMeters:recoveryLine,CourseSeconds:1.8,Maneuver:Maneuver161.Recover)};
  var legal=new List<(Tape150 Tape,Control161 Control)>();
  foreach(var settings in policies){guard();PrimitiveCandidates++;var control=new Control161(rt,settings);var s=start;var observations=new List<State146>{s};var actions=new List<FreeRacingAction>();var points=new List<FreeRacingPoint>();double? arrival=null;string reason="complete required maneuver";double began=System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
   for(int k=0;k<MaximumReviews;k++){
    guard();var action=control.Action(s);action.Validate();var q=step(s,action,"causal_reserve_generation");PhysicalSteps++;observations.Add(q.End);actions.Add(action);
    foreach(var p in q.Prediction.Points.Skip(points.Count==0?0:1)){if(arrival is null&&p.Rider.TotalReferenceMeters>=goal&&points.Count>0){var a=points[^1];arrival=a.Time+(p.Time-a.Time)*Math.Clamp((goal-a.Rider.TotalReferenceMeters)/(p.Rider.TotalReferenceMeters-a.Rider.TotalReferenceMeters),0,1);}points.Add(p);}
    s=q.End;var safety=Clearance153.Check(q.Prediction.Points,Regime153.SafetyHalfMeter);if(!safety.Legal){reason=safety.Reason;Rejections.Add(new{Maneuver=settings.Maneuver,Observed=s,Action=action,Reason=reason});PrefixRejections++;break;}
    states.Add(Efficient147.ExactKey147.State(s));if(s.Input.Rider.TotalReferenceMeters>=safetyGoal)break;
   }
   var check=Clearance153.Check(points.ToArray(),Regime153.SafetyHalfMeter);if(s.Input.Rider.TotalReferenceMeters<safetyGoal||arrival is null)check=check with{Legal=false,Reason=reason=="complete required maneuver"?"bounded terminal maneuver incomplete":reason};
   var tape=new Tape150(start.Input.Time,goal,safetyGoal,actions.ToArray(),observations.ToArray(),points.ToArray(),arrival,check,origin+"/"+settings.Maneuver);Evaluated.Add(tape);FurthestPartial=tape;
   Outcomes.Add(new(settings.Maneuver.ToString(),check.Legal,arrival,actions.Count,s.Input.Rider.TotalReferenceMeters,check.MinimumGapMeters,check.SweepLowerBoundMeters,check.Reason,actions.FirstOrDefault(),System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds-began));
   if(check.Legal){FullPlans++;legal.Add((tape,control));}
  }
  var ranked=legal.OrderBy(q=>q.Tape.Arrival).ThenBy(q=>q.Tape.Origin,StringComparer.Ordinal).ToArray();if(ranked.Length>0)Control=ranked[0].Control;return ranked.Take(maximumPlans).Select(q=>q.Tape).ToArray();
 }
}
