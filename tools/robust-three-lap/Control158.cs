using System.Numerics;
using Autonomous146;
using CoreSim.PhysicalSpace;
using Efficient152;
using Safety153;
using SharedWorldSpeedway;
namespace ThreeLap158;

public sealed record Settings158(double SpeedMetersPerSecond=12,double OffsetMeters=3.5,
 double CourseSeconds=.8,double YawSeconds=.6,double BetaSeconds=.8,double PreviewSeconds=.8,bool PredictPending=true);
public sealed record Response158(double YawAcceleration,double LongitudinalAcceleration,double LateralAcceleration);

/// <summary>Only current observations, immutable geometry and original contact/predictor
/// calls. No demonstrations, saved commands, body writes or parameter substitution.</summary>
public sealed class Control158 {
 readonly Runtime158 rt;readonly CoreSim.TrackStateSnapshot surface;readonly WorldParameters parameters;
 readonly Boundary152 boundary;sealed record Prefix158(State146 Future,FreeRacingPrediction Short,double Crank);readonly Dictionary<string,Prefix158> prefixes=new();readonly Dictionary<(string,int,int),Robust150.Response150> jointMemo=new();public long JointMemoHits,JointMemoMisses;
 public Settings158 Settings{get;}public long PrefixForecasts,PrefixContactQueries,PrefixCacheHits,ForceQueries,JointCorrections;
 public Control158(Runtime158 runtime,Settings158? settings=null){rt=runtime;Settings=settings??new();surface=MultiActionExperiment135.Surface(rt.Frame);parameters=FreeLearningExperiment.Parameters();boundary=new(rt.Frame);}
 public void Begin(){if(prefixes.Count>=4096)prefixes.Clear();if(jointMemo.Count>=8192)jointMemo.Clear();}
 public State146 ActivationState(State146 state){if(!Settings.PredictPending)return state;var key=Efficient147.ExactKey147.State(state);if(prefixes.TryGetValue(key,out var hit)){PrefixCacheHits++;rt.PrimeUnavoidablePrefix(state,hit.Short,hit.Crank);return hit.Future;}double crank=state.CrankOmega,shortCrank=state.CrankOmega;PrefixForecasts++;
  var p=PredictorA153.Predict(state.Input,rt.Spec,rt.Frame,surface,parameters,147,52.72097f,.24,null,powertrain:new(omega:state.CrankOmega),observeCrank:(t,w)=>{crank=w;if(t<=state.Input.Time+.2+1e-10)shortCrank=w;},fastBoundary:boundary,exactInitialWheel:false);PrefixContactQueries+=p.ForceEvaluations;
  var pp=p.Points.TakeWhile(q=>q.Time<=state.Input.Time+.2+1e-10).ToArray();pp[^1]=pp[^1] with{Time=Math.Min(pp[^1].Time,state.Input.Time+.2)};double length=0;for(int j=1;j<pp.Length;j++)length+=Vector2.Distance(pp[j-1].Rider.Position,pp[j].Rider.Position);var shortPrediction=new FreeRacingPrediction(pp,0,length);
  var end=p.Points[^1];var queue=state.Input.Pending.OrderBy(c=>c.ActivationAt).ToArray();var active=queue.Where(c=>c.ActivationAt<=end.Time+1e-10).Select(c=>c.Action).LastOrDefault(state.Input.Active);var future=new State146(new(end.Rider with{Drive=active.Drive},end.Time,end.Omega,end.Steering,active,queue.Where(c=>c.ActivationAt>end.Time+1e-10).ToArray(),end.Time),crank);
  prefixes[key]=new(future,shortPrediction,shortCrank);rt.PrimeUnavoidablePrefix(state,shortPrediction,shortCrank);return future;
 }
 public double TargetOffset(double reference){var finishMerge=rt.Frame.SegmentStartMeters(1);var u=Math.Clamp(reference/finishMerge,0,1);var smooth=u*u*(3-2*u);return Settings.OffsetMeters+(rt.Spec.OffsetMeters-Settings.OffsetMeters)*(1-smooth);}
 public Robust150.Response150 JointResponse(Robust150.Runtime150 runtime,State146 state,float drive,float steer){var key=(Efficient147.ExactKey147.State(state),BitConverter.SingleToInt32Bits(drive),BitConverter.SingleToInt32Bits(steer));if(jointMemo.TryGetValue(key,out var hit)){JointMemoHits++;return hit;}JointMemoMisses++;return jointMemo[key]=runtime.Response(state,drive,steer);}
 public Response158 Response(State146 state,float steer,float drive){ForceQueries++;var v=state.Input.Rider with{Drive=drive};var wheel=Speed148.ForecastFork148.Wheel(state.Input.Omega);var shaft=new CrankClutch144(omega:state.CrankOmega);
  var q=Contact144.Forces(v,rt.Spec,rt.Frame,surface,parameters,state.Input.Time,147,wheel,new(steer),.01f,steeringOverride:steer,inertia:52.72097f,powertrain:shaft,releaseAt:.24);
  var tangent=v.Speed>.1?Vector2.Normalize(v.Velocity):v.Road.Tangent;var left=new Vector2(-tangent.Y,tangent.X);return new(q.NetTorque/52.72097f,Vector2.Dot(q.NetForce,tangent)/147,Vector2.Dot(q.NetForce,left)/147);
 }
 public FreeRacingAction Action(State146 observed){var state=ActivationState(observed);var v=state.Input.Rider;var o=Settings;
  if(v.Speed<2)return new(0,1);
  var nominal=rt.Frame.At(v.TotalReferenceMeters,TargetOffset(v.TotalReferenceMeters));var road=nominal.Road;
  var curvature=road.IsTurn&&double.IsFinite(road.RadiusMeters)?1/road.RadiusMeters:0;
  var lateralError=Vector2.Dot(v.Position-nominal.Position,road.Outward);var lateralVelocity=Vector2.Dot(v.Velocity,road.Outward);
  // beta is body heading MINUS velocity heading in this repository.
  var desiredYaw=v.Speed*curvature+(lateralError/o.CourseSeconds+2*lateralVelocity)/(Math.Max(2,v.Speed)*o.CourseSeconds)-v.SlideRadians/o.BetaSeconds;
  var desiredAlpha=(desiredYaw-v.AngularVelocity)/o.YawSeconds;var desiredAcceleration=Math.Clamp((o.SpeedMetersPerSecond-v.Speed)/1.2,-2,3);
  float steer=state.Input.Steering,drive=state.Input.Active.Drive;var max=TwoContactBikeParameters.Default.MaximumSteeringRadians;
  // Simultaneously invert yaw and along-velocity acceleration using the ORIGINAL
  // contact equations. These are proposals; full physical certification owns safety.
  for(int k=0;k<3;k++){
   float dl=0,dh=1;var low=Response(state,steer,dl);var high=Response(state,steer,dh);
   if(desiredAcceleration<=low.LongitudinalAcceleration)drive=0;
   else if(desiredAcceleration>=high.LongitudinalAcceleration)drive=1;
   else {for(int j=0;j<10;j++){var mid=(dl+dh)/2;var r=Response(state,steer,mid);if(r.LongitudinalAcceleration<desiredAcceleration)dl=mid;else dh=mid;}drive=(dl+dh)/2;}
   float sl=-max,sh=max;var left=Response(state,sl,drive);var right=Response(state,sh,drive);
   if(desiredAlpha<=left.YawAcceleration)steer=sl;else if(desiredAlpha>=right.YawAcceleration)steer=sh;
   else {for(int j=0;j<12;j++){var mid=(sl+sh)/2;var r=Response(state,mid,drive);if(r.YawAcceleration<desiredAlpha)sl=mid;else sh=mid;}steer=(sl+sh)/2;}JointCorrections++;
  }
  return new(steer,drive);
 }
}
