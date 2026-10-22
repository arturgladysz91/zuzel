using System.Numerics;
using System.Reflection;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

// Read-only JSON-line adapter. No WorldRunner, Box2D calls, Commit or copied tyre law.
var path=AuthoredRacingPath.Crossover(false);
var frame=path.Frame;
var spec=RacingLineRootCauseAudit.Rider(path);
var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
var original=MotorcyclePerformanceAudit.Parameters("C-both");
var parameters=original with{EnablePhysicalSteeringControl=false,EnableTravelDirectionSteering=false};
var rows=TravelDirectionAudit.Saved();
var wp=RearWheelParameters.Default;
var scale=typeof(SlipCoupledDynamics).GetMethod("VelocityScaleSquared",BindingFlags.Static|BindingFlags.NonPublic)!;
const float mass=147, inertia=52.72097f;
var count=0;var internalQueries=0;var directQueries=0;
string? line;
while((line=Console.ReadLine()) is not null){
    using var doc=JsonDocument.Parse(line);var request=doc.RootElement;
    double N(string key,double fallback=0)=>request.TryGetProperty(key,out var value)?value.GetDouble():fallback;
    var op=request.GetProperty("op").GetString();
    if(op=="count"){Console.WriteLine(JsonSerializer.Serialize(new{TwoContactEntryCalls=count,OriginalSteeringInternalQueries=internalQueries,
        DirectRearQueries=directQueries,TotalConstitutiveEvaluations=count+internalQueries+directQueries,Worlds=0,BodySteps=0,Commits=0}));continue;}
    var row=rows.MinBy(r=>Math.Abs(r["t"]-N("t",.5)))!;
    var saved=PhysicalSteeringAudit.View(row,frame);
    var v=saved;var omega=N("omega",row["omega_before"]);var steering=(float)N("steer",row["steer_used"]);
    var dt=(float)N("h");
    if(op=="state"){
        v=v with{Position=Vector2.Zero,Velocity=new((float)N("u"),(float)N("w")),Heading=0,AngularVelocity=(float)N("r"),Drive=(float)N("drive")};
    }else if(op=="grid"){
        var baseWheel=new RearWheelState(omega*wp.WheelRadiusMeters);
        var baseForce=TwoContactBikeDynamics.Forces(saved,spec,frame,surface,parameters,row["t"],mass,baseWheel,new(steering),0,steeringOverride:steering);count++;
        var u=Vector2.Dot(baseForce.RearVelocity,baseForce.BodyForward);
        var w=u*Math.Tan(N("alpha")*Math.PI/180);
        v=saved with{Position=Vector2.Zero,Heading=0,Velocity=new(u,(float)(w+TwoContactBikeParameters.Default.RearAxleFromComMeters*saved.AngularVelocity))};
        omega=(u+N("kappa")*Math.Sqrt(u*u+wp.LowSpeedMetersPerSecond*wp.LowSpeedMetersPerSecond))/wp.WheelRadiusMeters;
        steering=0;dt=0;
    }else if(op!="saved")throw new ArgumentException("saved, grid, state, count");
    if(request.TryGetProperty("drive",out var drive))v=v with{Drive=drive.GetSingle()};
    var wheel=new RearWheelState(omega*wp.WheelRadiusMeters);var acceptedOmega=wheel.AngularVelocityRadPerSecond;var steer=new TwoContactBikeState(steering);
    var replay=request.TryGetProperty("replay",out var flag)&&flag.GetBoolean();
    if(replay){dt=(float)row["dt"];steer=new((float)row["steer_before"]);}
    var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,replay?original:parameters,row["t"],mass,wheel,steer,dt,
        replay?path.Guidance(v,original,true):null,steeringOverride:replay?null:steering,inertia:inertia);count++;
    internalQueries+=f.SteeringControl?.ContactQueries??0;
    if(wheel.AngularVelocityRadPerSecond!=acceptedOmega||steer.SteeringRadians!=(replay?(float)row["steer_before"]:steering))throw new Exception("Query committed state.");
    var c=wheel.Prepared;
    var ur=Vector2.Dot(f.RearVelocity,f.BodyForward);var wr=Vector2.Dot(f.RearVelocity,f.BodyLeft);
    var uf=Vector2.Dot(f.FrontVelocity,new(f.FrontLeft.Y,-f.FrontLeft.X));var wf=Vector2.Dot(f.FrontVelocity,f.FrontLeft);
    var dy2=(float)scale.Invoke(null,new object[]{ur,f.Budget/mass})!;
    var direct=RearWheelDynamics.Evaluate(ur,wr,omega,f.Budget,mass,dy2,c.RequestedTorqueNewtonMeters,c.AvailableTorqueNewtonMeters,c.DriveTorqueNewtonMeters,
        Vector2.Dot(f.Resistance+f.FrontForce,f.BodyForward),dt,wp,f.TyreLoads!.RearCapacityNewtons);directQueries++;
    var speed=v.Speed;var tangent=v.Velocity/speed;var normal=new Vector2(-tangent.Y,tangent.X);
    var motion=TravelDirectionState.Observe(v,f.NetForce,mass);
    var replayError=replay?new[]{Math.Abs(f.FrontForce.X-row["front_fx"]),Math.Abs(f.FrontForce.Y-row["front_fy"]),Math.Abs(f.RearForce.X-row["rear_fx"]),Math.Abs(f.RearForce.Y-row["rear_fy"])}.Max():0;
    Console.WriteLine(JsonSerializer.Serialize(new{
        Time=row["t"],U=v.ForwardSpeed,W=v.SideSpeed,Speed=speed,R=v.AngularVelocity,Omega=omega,Drive=v.Drive,Steer=f.SteeringUsed,
        Ur=ur,Wr=wr,AlphaRearDegrees=Math.Atan2(wr,ur)*180/Math.PI,AlphaFrontDegrees=Math.Atan2(wf,uf)*180/Math.PI,
        BetaDegrees=v.SlideRadians*180/Math.PI,Kappa=RearWheelDynamics.SlipRatio(ur,omega,wp),
        Fx=f.RearLongitudinal,Fy=f.RearLateral,FyFront=f.FrontLateral,Br=f.TyreLoads.RearCapacityNewtons,Bf=f.TyreLoads.FrontCapacityNewtons,B=f.Budget,
        FxBody=Vector2.Dot(f.NetForce,f.BodyForward),FyBody=Vector2.Dot(f.NetForce,f.BodyLeft),
        Normal=Vector2.Dot(f.NetForce,normal),Tangential=Vector2.Dot(f.NetForce,tangent),Torque=f.NetTorque,
        OmegaDot=(c.DriveTorqueNewtonMeters-wp.WheelRadiusMeters*f.RearLongitudinal-wp.BearingResistanceNewtonMeterSeconds*omega)/wp.WheelInertiaKgMetersSquared,
        AvailableTorque=c.AvailableTorqueNewtonMeters,DriveTorque=c.DriveTorqueNewtonMeters,Power=c.DriveTorqueNewtonMeters*omega,
        motion.ChiDot,motion.BetaDot,Demand=f.Demand,PathCurvature=path.At(row["s"]).Curvature,
        CircleFy=Math.Sqrt(Math.Max(0,f.TyreLoads.RearCapacityNewtons*f.TyreLoads.RearCapacityNewtons-f.RearLongitudinal*f.RearLongitudinal)),
        DirectError=Math.Max(Math.Abs(direct.LongitudinalNewtons-f.RearLongitudinal),Math.Abs(direct.RearLateralNewtons-f.RearLateral)),ReplayError=replayError,
        WheelSeconds=c.Seconds,WheelBefore=c.BeforeOmega,WheelAfter=c.AfterOmega
    }));
}
