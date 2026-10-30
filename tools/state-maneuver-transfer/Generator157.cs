using System.Numerics;
using Autonomous146;
using Robust150;
using Safety153;
using SharedWorldSpeedway;
namespace Transfer157;
public sealed record Generation157(string Origin,string Family,bool Legal,double? Arrival,double LengthMeters,int Commands,double EndReference,double MinimumGap,double Sweep,string Reason,double MaximumModelDistance);
/// <summary>Three unordered demonstration families, re-evaluated from each predicted
/// observation, never a teacher tape. Bounded nine rollouts; no K1 or best-time search.</summary>
public sealed class Generator157 {
 readonly Runtime153 rt;readonly Func<State146,FreeRacingAction,string,Transition146> step;readonly Action guard;readonly Policy157 model=new();
 public long PrimitiveCandidates,PhysicalSteps,PrefixRejections,DiversityPruned,LocalForceProbes,ForwardStageChecks,ForwardStageDeadEnds,ForwardStageWitnesses;public int FullPlans;public Tape150? FurthestPartial{get;private set;}public int DistinctLegalStates=>states.Count;readonly HashSet<string> states=new();
 public List<Generation157> Outcomes{get;}=new();public List<object> Rejections{get;}=new();
 public Generator157(Runtime153 rt,Func<State146,FreeRacingAction,string,Transition146> step,Action guard,bool forwardCheck){this.rt=rt;this.step=step;this.guard=guard;}
 public void Begin(){Outcomes.Clear();Rejections.Clear();states.Clear();FurthestPartial=null;PrimitiveCandidates=PhysicalSteps=PrefixRejections=DiversityPruned=LocalForceProbes=ForwardStageChecks=ForwardStageDeadEnds=ForwardStageWitnesses=0;FullPlans=0;}
 public Tape150[] Generate(State146 start,double goal,double safetyGoal,string origin,int maximumPlans=3){var complete=new List<(Tape150 Tape,double Length)>();
  foreach(var family in new[]{"A","B","C"})foreach(var correction in new[]{0,-1,1}){guard();PrimitiveCandidates++;var s=start;var actions=new List<FreeRacingAction>();var observations=new List<State146>{s};var points=new List<FreeRacingPoint>();double? arrival=null;double length=0,distance=0;string reason="full original terminal section";bool legal=true;
   for(int k=0;k<130;k++){guard();var proposal=model.Suggest(s,family);distance=Math.Max(distance,proposal.Distance);var a=proposal.Action;
    // A coupled bounded local proposal, rather than a preference for more throttle.
    if(correction!=0){var slip=s.Input.Rider.SlideRadians;var yaw=s.Input.Rider.AngularVelocity;var steer=a.SteeringRadians+correction*.25*Math.PI/180;var drive=a.Drive-correction*Math.Clamp(slip*yaw,-.1,.1);a=new((float)Math.Clamp(steer,-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians),(float)Math.Clamp(drive,0,1));}
    try{var q=step(s,a,"learned_proposal_physics");PhysicalSteps++;actions.Add(a);observations.Add(q.End);foreach(var p in q.Prediction.Points.Skip(points.Count==0?0:1)){if(points.Count>0){var b=points[^1];length+=Vector2.Distance(b.Rider.Position,p.Rider.Position);if(arrival is null&&p.Rider.TotalReferenceMeters>=goal)arrival=b.Time+(p.Time-b.Time)*Math.Clamp((goal-b.Rider.TotalReferenceMeters)/(p.Rider.TotalReferenceMeters-b.Rider.TotalReferenceMeters),0,1);}points.Add(p);}s=q.End;var safety=Clearance153.Check(q.Prediction.Points,Regime153.SafetyHalfMeter);if(!safety.Legal){legal=false;reason=safety.Reason;PrefixRejections++;Rejections.Add(new{Family=family,Correction=correction,Observed=s,Action=a,Reason=reason,Steps=k+1});break;}states.Add(Efficient147.ExactKey147.State(s));if(s.Input.Rider.TotalReferenceMeters>=safetyGoal)break;
    }catch(InvalidOperationException e)when(Planner157.PhysicalForecastFailure(e)){legal=false;reason=e.Message;Rejections.Add(new{Family=family,Correction=correction,Observed=s,Action=a,Reason=reason,Steps=k+1});break;}
   }
   if(s.Input.Rider.TotalReferenceMeters<safetyGoal||arrival is null){legal=false;if(reason=="full original terminal section")reason="26s terminal section incomplete";}
   if(points.Count==0)continue;var check=Clearance153.Check(points.ToArray(),Regime153.SafetyHalfMeter);var tape=new Tape150(start.Input.Time,goal,safetyGoal,actions.ToArray(),observations.ToArray(),points.ToArray(),arrival,check with{Legal=legal&&check.Legal,Reason=reason},origin+"/"+family+"/"+correction);
   Outcomes.Add(new(tape.Origin,family,tape.Safety.Legal,arrival,length,actions.Count,s.Input.Rider.TotalReferenceMeters,check.MinimumGapMeters,check.SweepLowerBoundMeters,reason,distance));if(FurthestPartial is null||s.Input.Rider.TotalReferenceMeters>FurthestPartial.End.Input.Rider.TotalReferenceMeters)FurthestPartial=tape;
   if(tape.Safety.Legal){FullPlans++;complete.Add((tape,length));}
  }
  return complete.OrderBy(x=>x.Tape.Arrival).ThenBy(x=>x.Length).Take(maximumPlans).Select(x=>x.Tape).ToArray();
 }
}
