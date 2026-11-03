#nullable enable
using System.Numerics;
using Autonomous146;
using CoreSim.PhysicalSpace;
using Efficient152;
using Safety153;
using SharedWorldSpeedway;
using ThreeLap158;
namespace S3Performance162;
using Continuation161;
/// <summary>Proposal only: every dynamic response uses the original predictor, full queue,
/// wheel and crank. Acceptance remains the frozen Runtime158 finite certificate.</summary>
public sealed class Control162 {
 readonly ResponseCache162 responses;
 public ResponseStatistics162 ResponseStatistics=>responses.Statistics;
 readonly Runtime158 rt;readonly Control158 prefix;readonly CoreSim.TrackStateSnapshot surface;readonly WorldParameters parameters;readonly Boundary152 boundary;
 public Settings161 Settings{get;}public object? Last{get;private set;}public object? LastProbe{get;private set;}public long ContactPredictions;public List<object> ProposalEvents{get;}=new();public long Predictions,ForceQueries;
 readonly Dictionary<(int,int),Equilibrium161> equilibria=new();
 public Control162(Runtime158 runtime,Settings161? settings=null,bool memoizeResponses=true,ResponseStatistics162? responseStatistics=null){responses=new(memoizeResponses,responseStatistics);rt=runtime;Settings=settings??new();prefix=new(rt,new(Settings.DiagnosticSpeed,OffsetMeters:Settings.OffsetMeters,CourseSeconds:1));surface=MultiActionExperiment135.Surface(rt.Frame);parameters=FreeLearningExperiment.Parameters();boundary=new(rt.Frame);}
 public void Begin(){responses.Clear();prefix.Begin();if(equilibria.Count>8192)equilibria.Clear();}
 public State146 ActivationState(State146 s)=>prefix.ActivationState(s);
 public static float SteeringDifferenceProbe(float steer){
  const float DifferenceRadians=.015f;var max=TwoContactBikeParameters.Default.MaximumSteeringRadians;
  if(!float.IsFinite(steer)||Math.Abs(steer)>max)throw new ArgumentException("Valid steering iterate required");
  return steer+DifferenceRadians<=max?steer+DifferenceRadians:steer-DifferenceRadians;
 }
 public Robust150.Response150 Response(State146 state,float steer,float drive){
  new FreeRacingAction(steer,drive).Validate();return responses.Get(state,steer,drive,()=>ComputeResponse(state,steer,drive));
 }
 Robust150.Response150 ComputeResponse(State146 state,float steer,float drive){
  var v=state.Input.Rider with{Drive=drive};var wheel=Speed148.ForecastFork148.Wheel(state.Input.Omega);const float StepSeconds=.02f;
  var q=Contact144.Forces(v,rt.Spec,rt.Frame,surface,parameters,state.Input.Time,147,wheel,new(state.Input.Steering),StepSeconds,steeringOverride:steer,inertia:52.72097f,powertrain:new(omega:state.CrankOmega),releaseAt:.24);ForceQueries++;
  double alpha=q.NetTorque/52.72097;var arm=q.RearArm;var acceleration=q.NetForce/147+new Vector2(-arm.Y,arm.X)*(float)alpha-arm*(v.AngularVelocity*v.AngularVelocity);
  double uDot=Vector2.Dot(acceleration,q.BodyForward)+v.AngularVelocity*Vector2.Dot(q.RearVelocity,q.BodyLeft),u=Vector2.Dot(q.RearVelocity,q.BodyForward);
  return new(alpha,.33*(wheel.Prepared.AfterOmega-state.Input.Omega)/StepSeconds-uDot,.33*state.Input.Omega-u);
 }
 public double ActualAlpha(State146 state,float steer,float drive)=>Response(state,steer,drive).YawAcceleration;
 public double TargetOffset(double s)=>prefix.TargetOffset(s);
 public static double ContinuationPreviewEnd(TrackFrame frame,double reference){
  // Preview physical bends beyond the race finish as well. Race classification
  // never truncates the force/actuator horizon used by a terminal reserve.
  int lap=(int)Math.Floor(reference/frame.LapMeters);var exits=new List<double>();
  for(int k=lap;k<=lap+2;k++)foreach(int i in new[]{4,8}){double q=k*frame.LapMeters+frame.SegmentStartMeters(i);if(q>reference+.005)exits.Add(q);}
  return exits[1];
 }
 public Equilibrium161 Equilibrium(double speed,double curvature){
  var key=((int)Math.Round(speed*10),(int)Math.Round(curvature*2000));if(equilibria.TryGetValue(key,out var hit))return hit;
  speed=Math.Max(2,key.Item1/10d);curvature=key.Item2/2000d;
  var at=rt.Frame.At(0,3.5);var road=at.Road;var tangent=road.Tangent;var normal=new Vector2(-tangent.Y,tangent.X);
  (double[] R,double Gas) Eval(double[] x){
   var h=(float)(road.Heading+x[0]);var v=new RiderView(rt.Spec.Profile.Id,at.Position,tangent*(float)speed,h,(float)(speed*curvature),road,0,0,RiderActivity.Racing,0,3.5f,0,rt.Frame.Context(road,0));
   double u=Vector2.Dot(v.Velocity,new(MathF.Cos(h),MathF.Sin(h)));double omega=(u+x[2]*Math.Sqrt(u*u+.25))/.33;
   var q=Contact144.Forces(v,rt.Spec,rt.Frame,surface,parameters,1,147,Speed148.ForecastFork148.Wheel(omega),new((float)x[1]),0,steeringOverride:(float)x[1],inertia:52.72097f);ForceQueries++;
   double required=q.RearLongitudinal*.33+.02*omega;var engine=Engine500.Evaluate(omega,rt.Spec.Setup,1);
   return (new[]{Vector2.Dot(q.NetForce,tangent)/147d,(Vector2.Dot(q.NetForce,normal)-147*speed*speed*curvature)/147d,q.NetTorque/52.72097d},required/engine.AvailableWheelTorqueNewtonMeters);
  }
  var x=new[]{speed*speed*curvature*.004,curvature*1.3,.04};var q=Eval(x);
  for(int k=0;k<14;k++){
   var a=new double[3,4];var bounds=new[]{(-.7,.7),(-.6,.6),(0d,3d)};for(int j=0;j<3;j++){var z=(double[])x.Clone();var pair=Difference161.OneSided(x[j],.0002,bounds[j].Item1,bounds[j].Item2);z[j]=pair.Probe;var r=Eval(z).R;for(int i=0;i<3;i++)a[i,j]=(r[i]-q.R[i])/pair.Step;}for(int i=0;i<3;i++)a[i,3]=-q.R[i];
   if(!Solve(a))break;bool improved=false;for(double scale=1;scale>=1d/64;scale/=2){var z=Enumerable.Range(0,3).Select(i=>x[i]+scale*a[i,3]).ToArray();if(Math.Abs(z[0])>.7||Math.Abs(z[1])>.6||z[2] is <0 or >3)continue;var trial=Eval(z);if(trial.R.Sum(t=>t*t)>=q.R.Sum(t=>t*t))continue;x=z;q=trial;improved=true;break;}if(!improved||q.R.Sum(t=>t*t)<1e-7)break;
  }
  return equilibria[key]=new(x[0],x[1],x[2],q.Gas,Math.Sqrt(q.R.Sum(t=>t*t)));
 }
 public static bool Solve(double[,] a){for(int col=0;col<3;col++){int pivot=col;for(int i=col+1;i<3;i++)if(Math.Abs(a[i,col])>Math.Abs(a[pivot,col]))pivot=i;if(Math.Abs(a[pivot,col])<1e-8)return false;for(int j=col;j<4;j++)(a[col,j],a[pivot,j])=(a[pivot,j],a[col,j]);double d=a[col,col];for(int j=col;j<4;j++)a[col,j]/=d;for(int i=0;i<3;i++)if(i!=col){double f=a[i,col];for(int j=col;j<4;j++)a[i,j]-=f*a[col,j];}}return true;}
 public FreeRacingPrediction Predict(State146 s,FreeRacingAction action,double horizon){action.Validate();LastProbe=new{Observed=s,Action=action,Horizon=horizon};Predictions++;var result=PredictorA153.Predict(s.Input,rt.Spec,rt.Frame,surface,parameters,147,52.72097f,horizon,action,powertrain:new(omega:s.CrankOmega),fastBoundary:boundary,exactInitialWheel:false);ContactPredictions+=result.ForceEvaluations;return result;}
 public FreeRacingAction Action(State146 observed){
  if(!Settings.Enabled)return prefix.Action(observed);
  var future=ActivationState(observed);var v=future.Input.Rider;if(v.Speed<2)return new(0,1);
  var o=Settings;var currentRoad=rt.Frame.At(v.TotalReferenceMeters,TargetOffset(v.TotalReferenceMeters)).Road;
  var feed=Equilibrium(v.Speed,currentRoad.IsTurn?1/currentRoad.RadiusMeters:0);
  double steeringPreparation=Math.Abs(feed.Steering-future.Input.Steering)/TwoContactBikeParameters.Default.SteeringRateRadiansPerSecond;
  double horizon=o.Preview?Math.Min(.9,RiderController.ReactionSeconds(rt.Spec)+2*FreeLearningController.ReviewSeconds+steeringPreparation):RiderController.ReactionSeconds(rt.Spec)+FreeLearningController.ReviewSeconds;
  double speedTarget=o.DiagnosticSpeed;
  if(o.Adaptive){
   // Bound the complete next maneuver, including both curvature ramps. Coast
   // braking is weak: a short straight forecast alone cannot justify acceleration.
   // A line is a proposal; original continuation certification owns feasibility.
   double end=ContinuationPreviewEnd(rt.Frame,v.TotalReferenceMeters);double kmax=0;
   for(double reference=v.TotalReferenceMeters;reference<=end+1;reference+=1){var road=rt.Frame.At(reference,TargetOffset(reference)).Road;if(road.IsTurn)kmax=Math.Max(kmax,1/road.RadiusMeters);}
   var loads=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(v.Road.Segment,Math.Clamp((float)v.Road.OffsetMeters/CoreSim.LaneModel.UsableRacingWidthMeters(rt.Frame.Track.Segments[v.Road.Segment].Type,rt.Frame.Track.Geometry)*4,0,4)),TwoContactBikeParameters.Default,parameters.LoadBasedTyres);
   // Capacity fraction is a declared proposal reserve, not a change to tyre forces.
   speedTarget=kmax>1e-5?Math.Sqrt(o.ForceFraction*loads.TotalCapacityNewtons/147/kmax):v.Speed;
   while(speedTarget>2){var feasible=Equilibrium(speedTarget,kmax);if(feasible.Residual<.05&&feasible.Gas is >=0 and <=1)break;speedTarget*=.95;}

  }
  (double A,double B,FreeRacingPrediction P,object Detail) Evaluate(float steer,float gas){
   var prediction=Predict(observed,new(steer,gas),horizon);var p=prediction.Points[^1];var w=p.Rider;var nominal=rt.Frame.At(w.TotalReferenceMeters,TargetOffset(w.TotalReferenceMeters));var road=nominal.Road;double k=road.IsTurn?1/road.RadiusMeters:0;
   double e=Vector2.Dot(w.Position-nominal.Position,road.Outward),vy=Vector2.Dot(w.Velocity,road.Outward);var eq=Equilibrium(w.Speed,k);double beta=o.Beta&&eq.Residual<.05?eq.Beta:0;
   double desired=w.Speed*k+(e/o.CourseSeconds+2*vy)/(Math.Max(2,w.Speed)*o.CourseSeconds)-(w.SlideRadians-beta)/o.BetaSeconds;
   return (w.AngularVelocity-desired,(w.Speed-speedTarget)/2,prediction,new{SpeedTarget=speedTarget,Curvature=k,DesiredYaw=desired,BetaTarget=beta,Equilibrium=eq,LateralError=e,CourseVelocity=vy});
  }
  var spatial=Space161.Observe(future,rt.Frame);double courseError=Math.Abs(BikeAngles.Wrap((float)(spatial.Chi-v.Road.Heading)));
  bool prepare=o.Maneuver==Maneuver161.Prepare&&(Math.Abs(spatial.LateralVelocity)>.5||spatial.ProjectedOffset<2.5);
  bool recover=o.Maneuver==Maneuver161.Recover&&(courseError>.08||spatial.ProjectedOffset<2.5||Math.Abs(spatial.LateralVelocity)>.5);
  float maximumGas=recover?0:prepare?.15f:1;
  float steer=future.Input.Steering,gas=Math.Min(maximumGas,future.Input.Active.Drive);var best=Evaluate(steer,gas);
  // A legal coasting proposal is always considered when the delayed state is
  // overspeed or approaching a boundary. It prevents saturation becoming a
  // stationary Newton seed despite an available throttle reduction.
  if(best.B>0||spatial.ProjectedOffset<2||o.Maneuver!=Maneuver161.Track){var coast=Evaluate(steer,0);if(coast.A*coast.A+coast.B*coast.B<best.A*best.A+best.B*best.B){gas=0;best=coast;}}

  for(int j=0;j<3;j++){
   var sp=Difference161.OneSided(steer,.015,-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians);var s=Evaluate((float)sp.Probe,gas);
   double a=(s.A-best.A)/sp.Step,c=(s.B-best.B)/sp.Step,delta,drive=0;
   if(maximumGas>0){
    var gp=Difference161.OneSided(gas,.08,0,maximumGas);var g=Evaluate(steer,(float)gp.Probe);double b=(g.A-best.A)/gp.Step,d=(g.B-best.B)/gp.Step,det=a*d-b*c;
    if(!double.IsFinite(det)||Math.Abs(det)<1e-6){ProposalEvents.Add(new{Time=observed.Input.Time,Reason="unreliable coupled derivative",Determinant=det});break;}
    delta=(-best.A*d+b*best.B)/det;drive=(c*best.A-a*best.B)/det;
   }else{if(!double.IsFinite(a)||Math.Abs(a)<1e-6){ProposalEvents.Add(new{Time=observed.Input.Time,Reason="unreliable coast steering derivative"});break;}delta=-best.A/a;}
   float proposed=(float)Math.Clamp(steer+Math.Clamp(delta,-.2,.2),-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians),throttle=(float)Math.Clamp(gas+Math.Clamp(drive,-.5,.5),0,maximumGas);
   var trial=Evaluate(proposed,throttle);if(trial.A*trial.A+trial.B*trial.B>=best.A*best.A+best.B*best.B)break;steer=proposed;gas=throttle;best=trial;
  }
  Last=new{Observed=observed,Activation=future,Steering=steer,Gas=gas,Horizon=horizon,ResidualYaw=best.A,ResidualSpeed=best.B,Target=best.Detail};return new(steer,gas);
 }
}
