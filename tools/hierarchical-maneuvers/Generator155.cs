using System.Numerics;
using Autonomous146;
using CoreSim.PhysicalSpace;
using Robust150;
using Safety153;
using SharedWorldSpeedway;
namespace Hierarchical155;
public enum ManeuverPhase155 { Launch,Prepare,Initiate,MidBend,Exit,NextPreparation }
public sealed record Primitive155(double SteeringBiasDegrees,float FirstDrive,float SecondDrive,int SwitchReview,bool AbsoluteSteering=false,double? SecondSteeringDegrees=null);
public sealed record Generation155(string Origin,bool Legal,double? Arrival,int Commands,double EndReference,double MinimumGap,double Sweep,string Reason,double Score,string Phase);
/// <summary>Bounded macro-action beam, three0.2s reviews per primitive. The parameters
/// define coupled steering/throttle knots; phases and physical feedback are recomputed
/// from each branch's own state. No K1, historical tape, authored line or body write.</summary>
public sealed class Generator155 {
 readonly RacingLineGeometry boundary;readonly CoreSim.TrackStateSnapshot surface;readonly Runtime153 rt;readonly Func<State146,FreeRacingAction,string,Transition146> step;readonly Action guard;readonly bool forwardCheck;
 public long PrimitiveCandidates,PhysicalSteps,PrefixRejections,DiversityPruned,LocalForceProbes,ForwardStageChecks,ForwardStageDeadEnds,ForwardStageWitnesses;public int FullPlans;public Tape150? FurthestPartial {get;private set;}public int DistinctLegalStates=>legalStates.Count;readonly HashSet<string> legalStates=new();
 public List<Generation155> Outcomes {get;}=new();
 public List<object> Rejections {get;}=new();
 public const int ReviewsPerPrimitive=3,BeamWidth=4,MaximumReviews=130;
 public Generator155(Runtime153 rt,Func<State146,FreeRacingAction,string,Transition146> step,Action guard,bool forwardCheck){this.rt=rt;this.step=step;this.guard=guard;this.forwardCheck=forwardCheck;boundary=new(rt.Frame);surface=MultiActionExperiment135.Surface(rt.Frame);}
 public void Begin(){Outcomes.Clear();Rejections.Clear();PrimitiveCandidates=PhysicalSteps=PrefixRejections=DiversityPruned=LocalForceProbes=ForwardStageChecks=ForwardStageDeadEnds=ForwardStageWitnesses=0;FullPlans=0;FurthestPartial=null;legalStates.Clear();}
 sealed record Node(State146 State,FreeRacingAction[] Actions,State146[] States,FreeRacingPoint[] Points,double? Arrival,double MinimumGap,double Sweep,double Score,string Family);
 public ManeuverPhase155 Phase(State146 s){var v=s.Input.Rider;if(v.Speed<3)return ManeuverPhase155.Launch;var distance=rt.Frame.DistanceToCurvatureChange(v.Road);var preview=rt.Frame.RoadAt(v.TotalReferenceMeters+Math.Max(3,rt.Frame.ReferenceSpeed(v)*1.2),v.Road.OffsetMeters);if(!v.Road.IsTurn)return preview.IsTurn?ManeuverPhase155.Prepare:ManeuverPhase155.NextPreparation;if(!preview.IsTurn||distance<Math.Max(5,v.Speed*.8))return ManeuverPhase155.Exit;if(Math.Abs(v.SlideRadians)>8*Math.PI/180||v.Road.CornerProgress>.25)return ManeuverPhase155.MidBend;return ManeuverPhase155.Initiate;}
 public Primitive155[] Primitives(State146 s){var phase=Phase(s);if(phase==ManeuverPhase155.Launch)return new[]{new Primitive155(0,1,1,1),new Primitive155(0,.31820688f,1,1)};
  var drive=(float)Controller143.Policy.Drive;var result=new List<Primitive155>();
  var drives=phase==ManeuverPhase155.Exit?new[]{drive,1f}:new[]{0f,drive};
  foreach(var d in drives)foreach(var bias in new[]{-1d,0d,1d})result.Add(new(bias,d,d,1));
  var angles=phase is ManeuverPhase155.Prepare or ManeuverPhase155.NextPreparation?new[]{-.5,0,.5}:new[]{-1.5,-.5,0,.5,1.5};
  foreach(var d in phase is ManeuverPhase155.Prepare or ManeuverPhase155.NextPreparation?new[]{0f,drive,1f}:drives)foreach(var angle in angles)result.Add(new(angle,phase==ManeuverPhase155.Exit?0:d,d,1,true));foreach(var pair in new[]{(1.5,-.5),(5d,1.5),(-1.5,.5),(-5d,-1.5)})foreach(var d in new[]{0f,drive})result.Add(new(pair.Item1,d,phase==ManeuverPhase155.Exit?drive:0,1,true,pair.Item2));return result.ToArray();}

 FreeRacingAction Action(State146 s,Primitive155 p,int review){var v=s.Input.Rider;var drive=review<p.SwitchReview?p.FirstDrive:p.SecondDrive;if(v.Speed<2)return new(0,drive);
  var speed=Math.Max(v.Speed,2);var future=rt.Frame.RoadAt(v.TotalReferenceMeters+Math.Max(1,rt.Frame.ReferenceSpeed(v)*.44),v.Road.OffsetMeters);var curvature=future.IsTurn&&double.IsFinite(future.RadiusMeters)?1/future.RadiusMeters:0;
  var course=Math.Atan2(v.Velocity.Y,v.Velocity.X);var error=BikeAngles.Wrap(course+v.AngularVelocity*.44-future.Heading);var gap=boundary.Clearance(v);var barrier=.08*(1/Math.Max(.1,gap.Inner-.3)-1/Math.Max(.1,gap.Outer-.3));
  var yawRate=speed*curvature-error/.8-barrier;var desiredAlpha=(yawRate-v.AngularVelocity)/.65;
  var max=TwoContactBikeParameters.Default.MaximumSteeringRadians;if(p.AbsoluteSteering)return new((float)Math.Clamp((review>=p.SwitchReview?p.SecondSteeringDegrees??p.SteeringBiasDegrees:p.SteeringBiasDegrees)*Math.PI/180,-max,max),drive);var centre=s.Input.Steering;var eps=.5f*MathF.PI/180;var lo=Math.Clamp(centre-eps,-max,max);var hi=Math.Clamp(centre+eps,-max,max);var action=new FreeRacingAction(centre,drive);LocalForceProbes+=3;
  var alpha=rt.LocalAlpha(s,action,centre);var derivative=(rt.LocalAlpha(s,action,hi)-rt.LocalAlpha(s,action,lo))/(hi-lo);
  var nominal=double.IsFinite(derivative)&&Math.Abs(derivative)>1e-8?Math.Clamp(centre+(desiredAlpha-alpha)/derivative,lo,hi):centre;
  var taper=review==0?1:review==1?.65:.35;return new((float)Math.Clamp(nominal+p.SteeringBiasDegrees*Math.PI/180*taper,-max,max),drive);
 }
 double Score(State146 state,double elapsed,double goal,double safetyGoal,double gap){var v=state.Input.Rider;var rate=Math.Max(1,rt.Frame.ReferenceSpeed(v));var course=Math.Atan2(v.Velocity.Y,v.Velocity.X);var error=BikeAngles.Wrap(course-v.Road.Heading);var score=elapsed+Math.Max(0,goal-v.TotalReferenceMeters)/rate+Math.Abs(error)*.6+.08/Math.Max(.02,gap-.49);
  if(forwardCheck){double maximumK=0;for(int j=0;j<8;j++){var q=rt.Frame.RoadAt(v.TotalReferenceMeters+j*5,v.Road.OffsetMeters);if(q.IsTurn&&double.IsFinite(q.RadiusMeters))maximumK=Math.Max(maximumK,1/q.RadiusMeters);}var localSurface=surface.SampleSurface(v.Road.Segment,2);var capacity=LoadBasedTyreGrip.Budget(147,localSurface,TwoContactBikeParameters.Default).TotalCapacityNewtons/147;var curveSpeed=maximumK>0?Math.Sqrt(.8*capacity/maximumK):40;
   // Reserve/capacity and ETA are ranking heuristics, never a safety or
   // impossibility proof. Only the exact irrevocable prefix below can prune.
   score+=Math.Max(0,v.Speed-curveSpeed)*.8;var remaining=Math.Max(.2,26-elapsed);score+=Math.Max(0,(safetyGoal-v.TotalReferenceMeters)/remaining-rate)*.4;
  }return score;
 }
 string Cell(Node n){var v=n.State.Input.Rider;return $"{n.Family}/{Math.Floor(n.State.Input.Steering*180/Math.PI)}/{n.State.Input.Active.Drive:F2}/{string.Join(",",n.State.Input.Pending.Select(c=>$"{c.Action.SteeringRadians:F3}:{c.Action.Drive:F2}"))}/{Math.Floor(v.TotalReferenceMeters/4)}/{Math.Floor(v.Road.OffsetMeters/.75)}/{Math.Floor(v.Speed/1.5)}/{Math.Floor(v.SlideRadians/.08)}/{Math.Floor(v.AngularVelocity/.15)}";}
 bool HasNextPrimitive(Node node,double safetyGoal){
  if(!forwardCheck||node.State.Input.Rider.TotalReferenceMeters>=safetyGoal)return true;
  var remaining=MaximumReviews-node.Actions.Length;if(remaining<=0)return false;ForwardStageChecks++;
  foreach(var primitive in Primitives(node.State)){var state=node.State;bool legal=true;
   for(int j=0;j<Math.Min(ReviewsPerPrimitive,remaining);j++){guard();try{var action=Action(state,primitive,j);var t=step(state,action,"physical_stage_reachability");PhysicalSteps++;if(!Clearance153.Check(t.Prediction.Points,Regime153.SafetyHalfMeter).Legal){legal=false;break;}state=t.End;if(state.Input.Rider.TotalReferenceMeters>=safetyGoal)break;}catch(InvalidOperationException e)when(Planner155.PhysicalForecastFailure(e)){legal=false;break;}}
   if(legal){ForwardStageWitnesses++;return true;}
  }ForwardStageDeadEnds++;return false;
 }
 public Tape150[] Generate(State146 start,double goal,double safetyGoal,string origin,int maximumPlans=3){var root=new Node(start,Array.Empty<FreeRacingAction>(),new[]{start},Array.Empty<FreeRacingPoint>(),null,double.PositiveInfinity,double.PositiveInfinity,0,"initial");var beam=new List<Node>{root};var complete=new List<Node>();
  for(int depth=0;depth<MaximumReviews&&beam.Count>0;depth+=ReviewsPerPrimitive){guard();var next=new List<Node>();foreach(var parent in beam)foreach(var primitive in Primitives(parent.State)){
    guard();PrimitiveCandidates++;var state=parent.State;var actions=new List<FreeRacingAction>(parent.Actions);var states=new List<State146>(parent.States);var points=new List<FreeRacingPoint>(parent.Points);var arrival=parent.Arrival;var gap=parent.MinimumGap;var sweep=parent.Sweep;string? rejection=null;var phase=Phase(state);
    for(int j=0;j<Math.Min(ReviewsPerPrimitive,MaximumReviews-depth);j++){
     guard();try{var action=Action(state,primitive,j);var transition=step(state,action,"primitive_physics");PhysicalSteps++;var safety=Clearance153.Check(transition.Prediction.Points,Regime153.SafetyHalfMeter);actions.Add(action);states.Add(transition.End);foreach(var p in transition.Prediction.Points.Skip(points.Count==0?0:1)){if(arrival is null&&p.Rider.TotalReferenceMeters>=goal&&points.Count>0){var before=points[^1];arrival=before.Time+(p.Time-before.Time)*Math.Clamp((goal-before.Rider.TotalReferenceMeters)/(p.Rider.TotalReferenceMeters-before.Rider.TotalReferenceMeters),0,1);}points.Add(p);}state=transition.End;gap=Math.Min(gap,safety.MinimumGapMeters);sweep=Math.Min(sweep,safety.SweepLowerBoundMeters);if(!safety.Legal){rejection=safety.Reason;break;}if(state.Input.Rider.TotalReferenceMeters>=safetyGoal)break;
     }catch(InvalidOperationException e) when(Planner155.PhysicalForecastFailure(e)){rejection=e.Message;break;}
    }
    if(rejection is null&&forwardCheck&&state.Input.Rider.TotalReferenceMeters<safetyGoal){
     // A newly issued command cannot influence the next0.2s because latency
     // is0.24s. Failure of this exact physical prefix is therefore necessary.
     var prefix=step(state,new(0,0),"necessary_irrevocable_prefix");PhysicalSteps++;if(!Clearance153.Check(prefix.Prediction.Points,Regime153.SafetyHalfMeter).Legal){PrefixRejections++;rejection="unavoidable next0.2s prefix lacks S certificate";}
    }
    var elapsed=state.Input.Time-start.Input.Time;var score=Score(state,elapsed,goal,safetyGoal,gap);var legal=rejection is null&&state.Input.Rider.TotalReferenceMeters>=safetyGoal&&arrival is not null;
    if(rejection is not null){if(Rejections.Count<300)Rejections.Add(new{Start=parent.State,Actions=actions.Skip(parent.Actions.Length).ToArray(),Primitive=primitive,Phase=phase.ToString(),Reason=rejection,End=state,MinimumGap=gap,Sweep=sweep});continue;}
    var node=new Node(state,actions.ToArray(),states.ToArray(),points.ToArray(),arrival,gap,sweep,score,primitive.SecondSteeringDegrees is not null?"coupled steering pulse":primitive.AbsoluteSteering?"absolute knot":"local-force secant knot");
    legalStates.Add(Efficient147.ExactKey147.State(state));if(FurthestPartial is null||state.Input.Rider.TotalReferenceMeters>FurthestPartial.End.Input.Rider.TotalReferenceMeters)FurthestPartial=new(start.Input.Time,goal,safetyGoal,node.Actions,node.States,node.Points,arrival,Clearance153.Check(node.Points,Regime153.SafetyHalfMeter) with{Legal=false,Reason="diagnostic partial path; not a full terminal certificate"},origin);
    if(legal){complete.Add(node);FullPlans++;Outcomes.Add(new(origin,true,arrival,actions.Count,state.Input.Rider.TotalReferenceMeters,gap,sweep,"complete original terminal section",score,phase.ToString()));}else next.Add(node);
   }
   // Coarse cells maintain search diversity only. They are never cache keys or
   // certificate equivalence classes. Every selected state retains its own tape.
   var ranked=next.OrderBy(n=>n.Score).ThenByDescending(n=>n.State.Input.Rider.TotalReferenceMeters).ToArray();var cells=new HashSet<string>();beam=new();var checkedNodes=new Dictionary<Node,bool>();int checkedCount=0;
   bool Viable(Node node){if(checkedNodes.TryGetValue(node,out var known))return known;if(checkedCount++>=BeamWidth*3)return false;return checkedNodes[node]=HasNextPrimitive(node,safetyGoal);}
   foreach(var family in ranked.GroupBy(n=>n.Family))foreach(var one in family.Take(3)){if(Viable(one)&&cells.Add(Cell(one))){beam.Add(one);break;}}
   foreach(var n in ranked){if(beam.Count>=BeamWidth)break;if(cells.Contains(Cell(n))){DiversityPruned++;continue;}if(Viable(n)){cells.Add(Cell(n));beam.Add(n);}}

   if(complete.Count>0)break;
  }
  if(complete.Count==0)Outcomes.Add(new(origin,false,null,FurthestPartial?.Actions.Length??0,FurthestPartial?.End.Input.Rider.TotalReferenceMeters??start.Input.Rider.TotalReferenceMeters,FurthestPartial?.Safety.MinimumGapMeters??0,FurthestPartial?.Safety.SweepLowerBoundMeters??0,"bounded primitive beam has no complete maneuver",0,Phase(start).ToString()));
  return complete.OrderBy(n=>n.Arrival).ThenBy(n=>n.Actions.Length).Take(maximumPlans).Select(n=>new Tape150(start.Input.Time,goal,safetyGoal,n.Actions,n.States,n.Points,n.Arrival,Clearance153.Check(n.Points,Regime153.SafetyHalfMeter),origin)).ToArray();
 }
}
