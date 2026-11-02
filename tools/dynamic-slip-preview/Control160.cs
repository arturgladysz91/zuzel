using System.Numerics;
using Autonomous146;
using CoreSim.PhysicalSpace;
using Efficient152;
using Safety153;
using SharedWorldSpeedway;
using ThreeLap158;
namespace Dynamic160;
public sealed record Settings160(bool Enabled=false,double DiagnosticSpeed=12,bool Actuator=true,bool Beta=true,bool Preview=true,bool Adaptive=false,double CourseSeconds=1.2,double BetaSeconds=1.5,double ForceFraction=.5,double OffsetMeters=3.5);
public sealed record Equilibrium160(double Beta,double Steering,double WheelSlip,double Gas,double Residual);
/// <summary>Proposal only: every dynamic response uses the original predictor, full queue,
/// wheel and crank. Acceptance remains the frozen Runtime158 finite certificate.</summary>
public sealed class Control160 {
 readonly Runtime158 rt;readonly Control158 prefix;readonly CoreSim.TrackStateSnapshot surface;readonly WorldParameters parameters;readonly Boundary152 boundary;
 public Settings160 Settings{get;}public object? Last{get;private set;}public object? LastProbe{get;private set;}public long Predictions,ForceQueries;
 readonly Dictionary<(int,int),Equilibrium160> equilibria=new();
 public Control160(Runtime158 runtime,Settings160? settings=null){rt=runtime;Settings=settings??new();prefix=new(rt,new(Settings.DiagnosticSpeed,OffsetMeters:Settings.OffsetMeters,CourseSeconds:1));surface=MultiActionExperiment135.Surface(rt.Frame);parameters=FreeLearningExperiment.Parameters();boundary=new(rt.Frame);}
 public void Begin(){prefix.Begin();if(equilibria.Count>8192)equilibria.Clear();}
 public State146 ActivationState(State146 s)=>prefix.ActivationState(s);
 public static float SteeringDifferenceProbe(float steer){
  const float DifferenceRadians=.015f;var max=TwoContactBikeParameters.Default.MaximumSteeringRadians;
  if(!float.IsFinite(steer)||Math.Abs(steer)>max)throw new ArgumentException("Valid steering iterate required");
  return steer+DifferenceRadians<=max?steer+DifferenceRadians:steer-DifferenceRadians;
 }
 public double ActualAlpha(State146 state,float steer,float drive){
  var q=Contact144.Forces(state.Input.Rider with{Drive=drive},rt.Spec,rt.Frame,surface,parameters,state.Input.Time,147,Speed148.ForecastFork148.Wheel(state.Input.Omega),new(state.Input.Steering),.02f,steeringOverride:steer,inertia:52.72097f,powertrain:new(omega:state.CrankOmega),releaseAt:.24);ForceQueries++;return q.NetTorque/52.72097;
 }
 public double TargetOffset(double s)=>prefix.TargetOffset(s);
 public Equilibrium160 Equilibrium(double speed,double curvature){
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
   var a=new double[3,4];var eps=new[]{.0002,.0002,.0002};for(int j=0;j<3;j++){var z=(double[])x.Clone();z[j]+=eps[j];var r=Eval(z).R;for(int i=0;i<3;i++)a[i,j]=(r[i]-q.R[i])/eps[j];}for(int i=0;i<3;i++)a[i,3]=-q.R[i];
   if(!Solve(a))break;bool improved=false;for(double scale=1;scale>=1d/64;scale/=2){var z=Enumerable.Range(0,3).Select(i=>x[i]+scale*a[i,3]).ToArray();if(Math.Abs(z[0])>.7||Math.Abs(z[1])>.6||z[2] is < -1 or >3)continue;var trial=Eval(z);if(trial.R.Sum(t=>t*t)>=q.R.Sum(t=>t*t))continue;x=z;q=trial;improved=true;break;}if(!improved||q.R.Sum(t=>t*t)<1e-7)break;
  }
  return equilibria[key]=new(x[0],x[1],x[2],q.Gas,Math.Sqrt(q.R.Sum(t=>t*t)));
 }
 public static bool Solve(double[,] a){for(int col=0;col<3;col++){int pivot=col;for(int i=col+1;i<3;i++)if(Math.Abs(a[i,col])>Math.Abs(a[pivot,col]))pivot=i;if(Math.Abs(a[pivot,col])<1e-8)return false;for(int j=col;j<4;j++)(a[col,j],a[pivot,j])=(a[pivot,j],a[col,j]);double d=a[col,col];for(int j=col;j<4;j++)a[col,j]/=d;for(int i=0;i<3;i++)if(i!=col){double f=a[i,col];for(int j=col;j<4;j++)a[i,j]-=f*a[col,j];}}return true;}
 public FreeRacingPrediction Predict(State146 s,FreeRacingAction action,double horizon){LastProbe=new{Observed=s,Action=action,Horizon=horizon};Predictions++;return PredictorA153.Predict(s.Input,rt.Spec,rt.Frame,surface,parameters,147,52.72097f,horizon,action,powertrain:new(omega:s.CrankOmega),fastBoundary:boundary,exactInitialWheel:false);}
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
   double first=Autonomous146.Planner146.Goal(rt.Frame,v.TotalReferenceMeters+.005,3);double end=Autonomous146.Planner146.Goal(rt.Frame,first+.005,3);end=Math.Max(end,v.TotalReferenceMeters+rt.Frame.SegmentStartMeters(1));double kmax=0;
   for(double reference=v.TotalReferenceMeters;reference<=end+1;reference+=1){var road=rt.Frame.At(reference,TargetOffset(reference)).Road;if(road.IsTurn)kmax=Math.Max(kmax,1/road.RadiusMeters);}
   var loads=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(v.Road.Segment,Math.Clamp((float)v.Road.OffsetMeters/CoreSim.LaneModel.UsableRacingWidthMeters(rt.Frame.Track.Segments[v.Road.Segment].Type,rt.Frame.Track.Geometry)*4,0,4)),TwoContactBikeParameters.Default,parameters.LoadBasedTyres);
   // Capacity fraction is a declared proposal reserve, not a change to tyre forces.
   speedTarget=kmax>1e-5?Math.Sqrt(o.ForceFraction*loads.TotalCapacityNewtons/147/kmax):v.Speed;
   while(speedTarget>2){var feasible=Equilibrium(speedTarget,kmax);if(feasible.Residual<.05&&feasible.Gas is >=0 and <=1)break;speedTarget*=.95;}

  }
  if(!o.Actuator){
   // Explicit diagnostic ablation of the old instantaneous inversion only.
   var p=Predict(observed,future.Input.Active,horizon).Points[^1].Rider;
   var road=rt.Frame.At(p.TotalReferenceMeters,TargetOffset(p.TotalReferenceMeters)).Road;double k=road.IsTurn?1/road.RadiusMeters:0;
   var nominal=rt.Frame.At(v.TotalReferenceMeters,TargetOffset(v.TotalReferenceMeters));double e=Vector2.Dot(v.Position-nominal.Position,nominal.Road.Outward),vy=Vector2.Dot(v.Velocity,nominal.Road.Outward);
   var eq=Equilibrium(v.Speed,k);double beta=o.Beta&&eq.Residual<.05?eq.Beta:0;
   double alpha=(v.Speed*k+(e/o.CourseSeconds+2*vy)/(Math.Max(2,v.Speed)*o.CourseSeconds)-(v.SlideRadians-beta)/o.BetaSeconds-v.AngularVelocity)/.6;
   double acceleration=Math.Clamp((speedTarget-v.Speed)/1.2,-2,3);float st=future.Input.Steering,dr=future.Input.Active.Drive;float max=TwoContactBikeParameters.Default.MaximumSteeringRadians;
   for(int repeat=0;repeat<3;repeat++){
    float lo=0,hi=1;for(int j=0;j<10;j++){float mid=(lo+hi)/2;if(prefix.Response(future,st,mid).LongitudinalAcceleration<acceleration)lo=mid;else hi=mid;}dr=(lo+hi)/2;
    lo=-max;hi=max;if(alpha<=prefix.Response(future,lo,dr).YawAcceleration)st=lo;else if(alpha>=prefix.Response(future,hi,dr).YawAcceleration)st=hi;else{for(int j=0;j<12;j++){float mid=(lo+hi)/2;if(prefix.Response(future,mid,dr).YawAcceleration<alpha)lo=mid;else hi=mid;}st=(lo+hi)/2;}
   }
   Last=new{Ablation="instantaneous inversion",SpeedTarget=speedTarget,DesiredAlpha=alpha,BetaTarget=beta};return new(st,dr);
  }
  (double A,double B,FreeRacingPrediction P,object Detail) Evaluate(float steer,float gas){
   var prediction=Predict(observed,new(steer,gas),horizon);var p=prediction.Points[^1];var w=p.Rider;var nominal=rt.Frame.At(w.TotalReferenceMeters,TargetOffset(w.TotalReferenceMeters));var road=nominal.Road;double k=road.IsTurn?1/road.RadiusMeters:0;
   double e=Vector2.Dot(w.Position-nominal.Position,road.Outward),vy=Vector2.Dot(w.Velocity,road.Outward);var eq=Equilibrium(w.Speed,k);double beta=o.Beta&&eq.Residual<.05?eq.Beta:0;
   double desired=w.Speed*k+(e/o.CourseSeconds+2*vy)/(Math.Max(2,w.Speed)*o.CourseSeconds)-(w.SlideRadians-beta)/o.BetaSeconds;
   return (w.AngularVelocity-desired,(w.Speed-speedTarget)/2,prediction,new{SpeedTarget=speedTarget,Curvature=k,DesiredYaw=desired,BetaTarget=beta,Equilibrium=eq,LateralError=e,CourseVelocity=vy});
  }
  float steer=future.Input.Steering,gas=future.Input.Active.Drive;var best=Evaluate(steer,gas);
  for(int j=0;j<3;j++){
   const float dg=.08f;float probe=SteeringDifferenceProbe(steer);double ds=probe-steer;var s=Evaluate(probe,gas);var g=Evaluate(steer,Math.Min(1,gas+dg));double actualDg=Math.Min(1,gas+dg)-gas;if(actualDg<.001){g=Evaluate(steer,gas-dg);actualDg=-dg;}
   double a=(s.A-best.A)/ds,b=(g.A-best.A)/actualDg,c=(s.B-best.B)/ds,d=(g.B-best.B)/actualDg,det=a*d-b*c;
   if(Math.Abs(det)<1e-6)break;double delta=(-best.A*d+b*best.B)/det,drive=(c*best.A-a*best.B)/det;
   float proposed=(float)Math.Clamp(steer+Math.Clamp(delta,-.2,.2),-.610865238,.610865238),throttle=(float)Math.Clamp(gas+Math.Clamp(drive,-.5,.5),0,1);
   var trial=Evaluate(proposed,throttle);if(trial.A*trial.A+trial.B*trial.B>=best.A*best.A+best.B*best.B)break;steer=proposed;gas=throttle;best=trial;
  }
  Last=new{Observed=observed,Activation=future,Steering=steer,Gas=gas,Horizon=horizon,ResidualYaw=best.A,ResidualSpeed=best.B,Target=best.Detail};return new(steer,gas);
 }
}
