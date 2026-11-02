using System.Diagnostics;
using System.Numerics;
using Autonomous146;
using CoreSim;
using Efficient152;
using LineThrottle156;
using Safety153;
using SharedWorldSpeedway;
using ThreeLap158;
namespace Audit159;

/// <summary>Read-only original-model diagnostics. No production or native driving entrypoint.</summary>
public static class Program159 {
 public const string Base="9c94a21e37877fd82565fd309fd2850aeb23f341";
 public static string Doc=>Path.Combine(Study156.Root,"docs/experiments/geometry-speed-audit");
 static readonly Process Process=Process.GetCurrentProcess();
 static double priorCpu;
 static void Guard(){if(priorCpu+Process.TotalProcessorTime.TotalSeconds>=600)throw new InvalidOperationException("STUDY159_DIAGNOSTIC_CPU_STOP");}
 static void Save(string file,object value)=>Study156.Save(Path.Combine(Doc,file),value);
 public static int Main(string[] args){
  if(args.Length!=1||args[0] is not ("--geometry" or "--diagnostics" or "--certificates" or "--operating-points" or "--microprofile"))throw new ArgumentException("Study159 OFF: explicit research command required");
  if(!File.Exists(Path.Combine(Doc,"budget-plan.json")))throw new InvalidOperationException("Declare budget first");
  if(File.Exists(Path.Combine(Doc,"cost-"+args[0][2..]+".json")))throw new IOException("Preserve existing159 operation; evidence already exists");
  foreach(var file in Directory.GetFiles(Doc,"cost-*.json")){using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));var root=json.RootElement;priorCpu+=root.TryGetProperty("ChargedCpuSeconds",out var charged)?charged.GetDouble():root.GetProperty("CpuSeconds").GetDouble();}
  Guard();
  bool complete=false;string? error=null;
  try{if(args[0]=="--geometry")Geometry();else if(args[0]=="--certificates")Certificates();else if(args[0]=="--operating-points")OperatingPoints();else if(args[0]=="--microprofile")Microprofile();else Diagnostics();complete=true;return 0;}
  catch(Exception e){error=e.ToString();Console.Error.WriteLine(error);return 1;}
  finally{Save("cost-"+args[0][2..]+".json",new{Base,Complete=complete,Error=error,NativeWorlds=0,PriorChargedCpuSeconds=priorCpu,CpuSeconds=Process.TotalProcessorTime.TotalSeconds});}
 }
 public static TrackFrame Frame(string name)=>TrackAwarenessExperiment134.Frame(name);
 public static object GeometryRow(TrackFrame frame,double s){
  var shape=frame.Shape!;var q=shape.At(s);var width=shape.ProgressiveWidth(s);var outward=new CoreSim.PhysicalSpace.MeterPoint(Math.Sin(q.Heading),-Math.Cos(q.Heading));
  var inner=q.Position-outward;var outer=q.Position+outward*(width.Width-1);var line=q.Position+outward*3.5;
  var k=q.Curvature;var kp=(shape.At(s+.001).Curvature-shape.At(s-.001).Curvature)/.002;var d=width.Width-1;var a=1+k*d;
  var outerK=(k*a*a-a*width.Second+kp*d*width.First+2*k*width.First*width.First)/Math.Pow(a*a+width.First*width.First,1.5);
  var back=frame.Project(frame.At(s,3.5).Position);var err=frame.UnwrapDelta(s,back.ReferenceMeters);
  return new {Reference=s,Width=width.Width,WidthFirst=width.First,WidthSecond=width.Second,ReferenceXY=q.Position,Inner=inner,Outer=outer,Line=line,Heading=q.Heading,Curvature=k,LineCurvature=k/(1+3.5*k),InnerCurvature=k/(1-k),OuterCurvature=outerK,RoundTripReferenceError=err,RoundTripOffsetError=back.OffsetMeters-3.5};
 }
 static void Geometry(){
  var rows=new List<object>();foreach(var name in new[]{"progressive-b","progressive-c"}){var frame=Frame(name);var samples=new List<object>();for(double s=0;s<frame.LapMeters;s+=.1)samples.Add(GeometryRow(frame,s));
   var joins=Enumerable.Range(0,10).Select(i=>i==9?frame.LapMeters:frame.SegmentStartMeters(i)).ToArray();
   rows.Add(new{Name=name,frame.LapMeters,Starts=joins,Sections=samples,Boundaries=new{Inner=BoundaryPoints(frame,"inner"),Outer=BoundaryPoints(frame,"outer")},Joins=joins.Select(s=>new{Reference=s,Before=GeometryRow(frame,s-1e-5),After=GeometryRow(frame,s+1e-5)})});}
  var spec=StandingWorld144.Rider(StartingGate.B);var p=FreeLearningExperiment.Parameters();var f=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(f);var loads=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(0,2),TwoContactBikeParameters.Default,p.LoadBasedTyres);
  Save("geometry.json.gz",new{Base,Scope="Exact original ExperimentalTrackShape and physical boundaries, double precision,0.1m samples; reconstruction not surveyed",Tracks=rows,Loads=loads,EngineCurve=Engine500.Curve,EngineParameters=Engine500Parameters.Default,Wheel=RearWheelParameters.Default,Setup=spec.Setup,WorldParameters=p,CpuSeconds=Process.TotalProcessorTime.TotalSeconds});
 }
 static CoreSim.PhysicalSpace.MeterPoint[] BoundaryPoints(TrackFrame frame,string edge){
  const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
  var geometry=new RacingLineGeometry(frame);var curve=typeof(RacingLineGeometry).GetField("curve",flags)!.GetValue(geometry)!;var boundary=curve.GetType().GetField(edge,flags)!.GetValue(curve)!;
  return (CoreSim.PhysicalSpace.MeterPoint[])boundary.GetType().GetField("points",flags)!.GetValue(boundary)!;
 }
 static State146 State(StartSample144 sample)=>new(sample.Input,sample.CrankOmega);
 static TwoContactForces Force(State146 state,RiderSpec spec,TrackFrame frame,float drive,float steering,out ClutchStep144 power){
  var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(frame);var shaft=new CrankClutch144(omega:state.CrankOmega);var wheel=Speed148.ForecastFork148.Wheel(state.Input.Omega);
  var q=Contact144.Forces(state.Input.Rider with{Drive=drive},spec,frame,surface,p,state.Input.Time,147,wheel,new(state.Input.Steering),.01f,steeringOverride:steering,inertia:52.72097f,powertrain:shaft,releaseAt:.24);power=shaft.Prepared;return q;
 }
 static void Diagnostics(){
  if(!File.Exists(Path.Combine(Doc,"budget-plan.json")))throw new InvalidOperationException("Declare budget first");
  var source="docs/experiments/robust-three-lap/final-v6/native/validation-v6-new-0.json.gz";var run=Study156.Read<Run144>(source);var rider=run.Riders[0];var frame=StandingWorld144.Frame();var forceRows=new List<object>();
  // Fixed times span all six bends and straights. State/pending are never changed in these paired contact queries.
  foreach(var time in Enumerable.Range(1,50).Select(i=>2d*i)){
   Guard();var sample=rider.Samples.First(s=>s.Input.Time>=time-1e-8);var state=State(sample);var tangent=Vector2.Normalize(state.Input.Rider.Velocity);var rows=new List<object>();
   foreach(var gas in new[]{state.Input.Active.Drive,0f,1f}.Distinct()){
    var q=Force(state,rider.Spec,frame,gas,state.Input.Active.SteeringRadians,out var power);rows.Add(new{Gas=gas,AlongAcceleration=Vector2.Dot(q.NetForce,tangent)/147,NormalAcceleration=Vector2.Dot(q.NetForce,new(-tangent.Y,tangent.X))/147,YawAcceleration=q.NetTorque/52.72097f,Forces=q,Powertrain=power});}
   var settings=new Settings158(SpeedMetersPerSecond:10,CourseSeconds:1);var rt=new Runtime158(rider.Spec,false,Hierarchical155.Program155.Options());rt.Begin(time);var control=new Control158(rt,settings);var activation=control.ActivationState(state);var action=control.Action(state);
   forceRows.Add(new{Time=sample.Input.Time,State=state,Activation=activation,ControllerAction=action,DesiredAcceleration=Math.Clamp((10-activation.Input.Rider.Speed)/1.2,-2,3),Queries=rows});
  }
  Save("contact-pairs.json.gz",new{Base,Source=source,Scope="Paired original finite0.01s contact queries; same native state/queue, no integration or safety certificate",Rows=forceRows});
  var trials=new List<object>();var initial=Program158.Initial(rider.Spec);var straight=rider.Samples.First(s=>s.Input.Rider.TotalReferenceMeters>=frame.SegmentStartMeters(4)+.5);
  var fixtures=new[]{("rest",initial,3*frame.LapMeters,180d),("straight",State(straight),straight.Input.Rider.TotalReferenceMeters+58,12d)};
  // Single-factor changes against exactly10m/s,course1. Original policy class supplies every control.
  var variants=new[]{("base10",new Settings158(10,CourseSeconds:1),false),("speed12",new Settings158(12,CourseSeconds:1),false),("speed14",new Settings158(14,CourseSeconds:1),false),("full-gas",new Settings158(10,CourseSeconds:1),true),("beta-weak",new Settings158(10,CourseSeconds:1,BetaSeconds:8),false),("yaw-slow",new Settings158(10,CourseSeconds:1,YawSeconds:1.2),false),("offset6",new Settings158(10,OffsetMeters:6,CourseSeconds:1),false)};
  foreach(var fixture in fixtures)foreach(var variant in variants){Guard();var rt=new Runtime158(rider.Spec,false,Hierarchical155.Program155.Options());var control=new Control158(rt,variant.Item2);var state=fixture.Item2;var points=new List<FreeRacingPoint>();var actions=new List<object>();var began=Process.TotalProcessorTime.TotalSeconds;string reason="diagnostic horizon";bool reached=false;Safety146? safety=null;
   for(int k=0;k<900&&state.Input.Time-fixture.Item2.Input.Time<fixture.Item4;k++){
    try{Guard();rt.Begin(state.Input.Time);control.Begin();var action=control.Action(state);if(variant.Item3)action=action with{Drive=1};var step=rt.Step(state,action);actions.Add(new{State=state,Action=action});points.AddRange(step.Prediction.Points.Skip(points.Count==0?0:1));state=step.End;safety=Clearance153.Check(step.Prediction.Points,Regime153.SafetyHalfMeter);if(!safety.Legal){reason=safety.Reason;break;}if(state.Input.Rider.TotalReferenceMeters>=fixture.Item3){reached=true;reason="diagnostic geometric goal";break;}}
    catch(InvalidOperationException e){reason=e.Message;break;}
   }
   var all=Clearance153.Check(points.ToArray(),Regime153.SafetyHalfMeter);var label=fixture.Item1+"-"+variant.Item1;var summary=new{Label=label,Fixture=fixture.Item1,Variant=variant.Item1,Settings=variant.Item2,FullGasOverride=variant.Item3,Reached=reached,Legal=all.Legal,Reason=reason,EndTime=state.Input.Time,EndReference=state.Input.Rider.TotalReferenceMeters,Duration=state.Input.Time-fixture.Item2.Input.Time,MaximumSpeedKph=points.Select(p=>p.Rider.Speed*3.6).DefaultIfEmpty().Max(),Safety=all,CpuSeconds=Process.TotalProcessorTime.TotalSeconds-began,control.PrefixForecasts,control.ForceQueries};
   Save("forecasts/"+label+".json.gz",new{Summary=summary,Initial=fixture.Item2,Goal=fixture.Item3,End=state,Points=points,Actions=actions,Scope="DIAGNOSTIC FORECAST ONLY. No Box2D world, no independent13/9, not autonomous S3 or DNF race."});trials.Add(summary);Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(summary,Study156.Json));
  }
  Save("forecast-summary.json",new{Base,Trials=trials,NativeWorlds=0,CpuSeconds=Process.TotalProcessorTime.TotalSeconds,NativeGate="Review fastest full causal forecast before any certificate/native reservation"});
 }
 static void Certificates(){
  var spec=StandingWorld144.Rider(StartingGate.B);var initial=Program158.Initial(spec);var planner=new Planner158(spec,false,new(Enabled:true,MaximumRoots:3),Guard,settings:new Settings158(12,CourseSeconds:1));string? error=null;
  try{planner.Choose(initial.Input,new(omega:initial.CrankOmega));}catch(InvalidOperationException e){error=e.Message;}
  Save("speed12-certificate.json.gz",new{Base,Scope="Original Planner158 full13/9 from rest, diagnostic only; no native world",Error=error,Review=planner.Last is null?null:Evidence152.Compact(planner.Last),planner.Generator.Outcomes,planner.Generator.Rejections,planner.Generator.FurthestPartial});
  var alternate=new Boundary152(Frame("progressive-c"));var rows=new List<object>();
  for(int i=0;i<10;i++){
   Guard();var source=$"docs/experiments/robust-three-lap/final-v6/native/validation-v6-new-{i}.json.gz";var run=Study156.Read<Run144>(source);var samples=run.Riders[0].Samples;double pose=double.PositiveInfinity,sweep=double.PositiveInfinity;int badPose=0,badSweep=0;double? firstBad=null;
   var previous=alternate.Clearance(samples[0].Input.Rider);
   for(int j=0;j<samples.Length;j++){
    var sample=samples[j];var gap=alternate.Clearance(sample.Input.Rider);var minimum=Math.Min(gap.Inner,gap.Outer);pose=Math.Min(pose,minimum);if(minimum<.5){badPose++;firstBad??=sample.Input.Time;}
    if(j>0){var before=samples[j-1];var lower=Clearance153.SweepLowerBound(before.Input.Rider,sample.Input.Rider,sample.Input.Time-before.Input.Time,previous,gap);sweep=Math.Min(sweep,lower);if(lower<.5){badSweep++;firstBad??=sample.Input.Time;}}previous=gap;
   }
   rows.Add(new{Source=source,OriginalFingerprint=run.Fingerprint,Alternate="progressive-c",MinimumPose=pose,MinimumSweep=sweep,BadPoses=badPose,BadSweeps=badSweep,FirstBadTime=firstBad,Scope="Same actual native XY/heading; only boundary certificate recalculated. Not a new autonomous run."});
  }
  Save("geometry-recheck.json",rows);
 }
 sealed record Point159(double BetaDegrees,double SteeringDegrees,double Kappa,double Gas,double[] Residual,TwoContactForces Forces,double WheelOmega,double RequiredWheelTorque);
 static double Norm(double[] r)=>Math.Sqrt(r.Sum(v=>v*v));
 static void OperatingPoints(){
  var frame=StandingWorld144.Frame();var spec=StandingWorld144.Rider(StartingGate.B);var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(frame);var rows=new List<object>();
  // Stationary local circular-motion equations at the actual entry, middle and exit curvature.
  // Each candidate queries original contacts. This is not a forecast or a native completion.
  foreach(int bend in new[]{0,1})foreach(var fraction in new[]{.12,.5,.88})foreach(double offset in new[]{3.5,8d})foreach(double speed in new[]{10d,12,14,16,18,20,22}){
   Guard();double reference=frame.SegmentStartMeters(bend==0?1:5)+fraction*frame.SegmentLengthMeters(1)*3;var at=frame.At(reference,offset);var road=at.Road;double curvature=1/road.RadiusMeters;var yaw=(float)(speed*curvature);var tangent=road.Tangent;var normal=new Vector2(-tangent.Y,tangent.X);
   Point159 Evaluate(double beta,double steer,double kappa){
    var heading=(float)(road.Heading+beta*Math.PI/180);var v=new RiderView(spec.Profile.Id,at.Position,tangent*(float)speed,heading,yaw,road,reference,0,RiderActivity.Racing,0,(float)offset,0,frame.Context(road,reference));
    var forward=new Vector2(MathF.Cos(heading),MathF.Sin(heading));double ur=Vector2.Dot(v.Velocity,forward);double omega=(ur+kappa*Math.Sqrt(ur*ur+.25))/.33;var wheel=Speed148.ForecastFork148.Wheel(omega);float steering=(float)(steer*Math.PI/180);
    var q=Contact144.Forces(v,spec,frame,surface,p,1,147,wheel,new(steering),0,steeringOverride:steering,inertia:52.72097f);
    double required=q.RearLongitudinal*.33+.02*omega;var engine=Engine500.Evaluate(omega,spec.Setup,1);double gas=engine.AvailableWheelTorqueNewtonMeters>0?required/engine.AvailableWheelTorqueNewtonMeters:double.PositiveInfinity;
    return new(beta,steer,kappa,gas,new[]{(double)Vector2.Dot(q.NetForce,tangent),Vector2.Dot(q.NetForce,normal)-147*speed*speed*curvature,q.NetTorque/1.3},q,omega,required);
   }
   Point159? best=null;
   foreach(double beta in Enumerable.Range(0,36))foreach(double steer in Enumerable.Range(-25,51))foreach(double kappa in new[]{0,.01,.03,.06,.1,.15,.25,.4,.7,1,2}){
    var q=Evaluate(beta,steer,kappa);if(q.Gas is <0 or >1||!double.IsFinite(q.Gas))continue;if(best is null||Norm(q.Residual)<Norm(best.Residual))best=q;
   }
   // Refine a finite grid candidate through original function calls; residuals remain explicit.
   if(best is not null)for(int iteration=0;iteration<18;iteration++){
    var q=best;var x=new[]{q.BetaDegrees,q.SteeringDegrees,q.Kappa};var eps=new[]{.001,.001,.00001};var matrix=new double[3,4];
    for(int j=0;j<3;j++){var z=(double[])x.Clone();z[j]+=eps[j];var r=Evaluate(z[0],z[1],z[2]).Residual;for(int i=0;i<3;i++)matrix[i,j]=(r[i]-q.Residual[i])/eps[j];}
    for(int i=0;i<3;i++)matrix[i,3]=-q.Residual[i];bool singular=false;
    for(int col=0;col<3;col++){int pivot=col;for(int i=col+1;i<3;i++)if(Math.Abs(matrix[i,col])>Math.Abs(matrix[pivot,col]))pivot=i;if(Math.Abs(matrix[pivot,col])<1e-9){singular=true;break;}for(int j=col;j<4;j++)(matrix[col,j],matrix[pivot,j])=(matrix[pivot,j],matrix[col,j]);double divisor=matrix[col,col];for(int j=col;j<4;j++)matrix[col,j]/=divisor;for(int i=0;i<3;i++)if(i!=col){double factor=matrix[i,col];for(int j=col;j<4;j++)matrix[i,j]-=factor*matrix[col,j];}}
    if(singular)break;bool improved=false;
    for(double scale=1;scale>=1d/128;scale/=2){var z=Enumerable.Range(0,3).Select(i=>x[i]+scale*matrix[i,3]).ToArray();if(z[0] is < -5 or >45||Math.Abs(z[1])>25||z[2] is <0 or >3)continue;var r=Evaluate(z[0],z[1],z[2]);if(r.Gas is <0 or >1||Norm(r.Residual)>=Norm(q.Residual))continue;best=r;improved=true;break;}if(!improved||Norm(best.Residual)<.001)break;
   }
   object? clutchCheck=null;
   if(best is not null&&Norm(best.Residual)<1){
    double ratio=Engine500Parameters.Default.ShortOverallRatio+(Engine500Parameters.Default.LongOverallRatio-Engine500Parameters.Default.ShortOverallRatio)*spec.Setup.Gearing;double shaftTorque=best.RequiredWheelTorque/(ratio*.94);double lo=ratio*best.WheelOmega,hi=lo+1000;
    double TorqueAt(double omega){var l=LaunchParameters144.Default;var u=Math.Clamp((omega*30/Math.PI-l.DisengagedBelowRpm)/(l.FullyEngagedAboveRpm-l.DisengagedBelowRpm),0,1);var engaged=u*u*(3-2*u);return Math.Clamp(l.ClutchViscosityNewtonMeterSeconds*engaged*(omega-ratio*best.WheelOmega),-l.ClutchCapacityNewtonMeters*engaged,l.ClutchCapacityNewtonMeters*engaged);}
    for(int j=0;j<60;j++){double mid=(lo+hi)/2;if(TorqueAt(mid)<shaftTorque)lo=mid;else hi=mid;}double crank=(lo+hi)/2;double available=Engine500.Torque(crank*30/Math.PI);double gas=available>0?shaftTorque/available:double.PositiveInfinity;
    var heading=(float)(road.Heading+best.BetaDegrees*Math.PI/180);var v=new RiderView(spec.Profile.Id,at.Position,tangent*(float)speed,heading,yaw,road,reference,0,RiderActivity.Racing,(float)Math.Clamp(gas,0,1),(float)offset,0,frame.Context(road,reference));var steering=(float)(best.SteeringDegrees*Math.PI/180);var shaft=new CrankClutch144(omega:crank);var wheel=Speed148.ForecastFork148.Wheel(best.WheelOmega);
    var q=Contact144.Forces(v,spec,frame,surface,p,1,147,wheel,new(steering),0,steeringOverride:steering,inertia:52.72097f,powertrain:shaft,releaseAt:.24);
    clutchCheck=new{Gas=gas,CrankRpm=crank*30/Math.PI,CrankTorqueResidual=shaft.Prepared.CrankTorque-shaft.Prepared.ClutchTorque,WheelTorqueResidual=shaft.Prepared.Wheel.DriveTorqueNewtonMeters-.33*q.RearLongitudinal-.02*best.WheelOmega,ForceDifference=Vector2.Distance(q.NetForce,best.Forces.NetForce),YawTorque=q.NetTorque,ClutchCapacitySatisfied=Math.Abs(TorqueAt(crank)-shaftTorque)<.001,WithinThrottle=gas is >=0 and <=1};
   }
   rows.Add(new{Bend=bend+1,Fraction=fraction,Reference=reference,Offset=offset,SpeedKph=speed*3.6,Curvature=curvature,RequiredLateralAcceleration=speed*speed*curvature,ScalarForceBoundKph=3.6*Math.Sqrt(1852.3389535074234/147/curvature),Best=best,ClutchCheck=clutchCheck});
  }
  Save("operating-points.json",new{Base,Scope="Original tyre/engine/clutch instantaneous local equilibrium search. Zero-dt synthetic states, no stability/reachability/whole-bend or autonomous claim. Residual=[along N,normal error N,yaw torque/1.3 N]. Grid and damped Newton are diagnostic only.",Rows=rows,CpuSeconds=Process.TotalProcessorTime.TotalSeconds});
 }
 static void Microprofile(){
  var run=Study156.Read<Run144>("docs/experiments/robust-three-lap/final-v6/native/validation-v6-new-0.json.gz");var spec=run.Riders[0].Spec;
  var states=run.Riders[0].Decisions.Where(d=>d.Choice.Time>=2&&d.Choice.Time<=100).Select(d=>State(run.Riders[0].Samples.First(s=>Math.Abs(s.Input.Time-d.Choice.Time)<1e-8))).Select(s=>s with{Input=s.Input with{NextReview=s.Input.Time}}).ToArray();
  var rt=new Runtime158(spec,false,Hierarchical155.Program155.Options());var control=new Control158(rt,new(10,CourseSeconds:1));var phases=new List<object>();
  void Measure(string label,Action<State146> action){var start=Process.TotalProcessorTime.TotalSeconds;var watch=Stopwatch.StartNew();foreach(var state in states){Guard();action(state);}phases.Add(new{Name=label,Calls=states.Length,CpuSeconds=Process.TotalProcessorTime.TotalSeconds-start,WallSeconds=watch.Elapsed.TotalSeconds});}
  Measure("pending0.24-cold",s=>{rt.Begin(s.Input.Time);control.ActivationState(s);});
  Measure("Control158.Action-warm-pending",s=>{rt.Begin(s.Input.Time);control.Action(s);});
  // Original coupled yaw/beta/speed correction, distinct real native states and nominal same-state anchor.
  Measure("Joint158.Correct-distinct-native",s=>{rt.Begin(s.Input.Time);var action=s.Input.Active;Joint158.Correct(rt.Alpha,s,s,action,.1,.88,control);});
  Save("microprofile.json",new{Base,Scope="Batched read-only original functions, one process, no world; not additive to archived full-race phase times. CPU resolution retained.",Phases=phases,control.PrefixForecasts,control.PrefixContactQueries,control.ForceQueries,control.JointMemoHits,control.JointMemoMisses});
 }
}
