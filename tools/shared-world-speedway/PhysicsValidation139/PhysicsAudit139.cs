using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
namespace SharedWorldSpeedway;

public sealed record ContactState139(string Name,AwarenessCase134 Case,FreeRacingInput Input,bool NativeReached,bool LegalAtCapture);
public static class PhysicsAudit139
{
    public static long Queries {get;private set;}
    public static readonly string Old="docs/experiments/throttle-steering-decoupling";
    public static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    public static void Save(string d,string n,object v)=>RobustLearningExperiment.Save(d,n,v);
    public static T Read<T>(string d,string n)=>RobustLearningExperiment.Read<T>(d,n);
    public static ContactState139[] States(){
        var db=CausalYawExperiment137.Diagnostic[0];var dc=CausalYawExperiment137.Diagnostic[1];
        var full=RobustLearningExperiment.ReadEpisode(Old+"/native","D0-B");var partial=RobustLearningExperiment.ReadEpisode(Old+"/native","D0-D");var second=RobustLearningExperiment.ReadEpisode(Old+"/native","D1-B");
        ContactState139 S(string n,AwarenessCase134 c,FreeRacingSample s,bool legal=true)=>new(n,c,s.Input,true,legal);
        return new[]{S("D0-before",db,full.Replay.Samples.MinBy(s=>Math.Abs(s.Input.Time-5.4))!),
            S("D0-developing",db,full.Replay.Samples.MinBy(s=>Math.Abs(s.Input.Time-6.1))!),S("D0-failed",db,full.Replay.Samples[^1],false),
            S("D0-partial",db,partial.Replay.Samples.MinBy(s=>Math.Abs(s.Input.Time-6.3))!),S("D1-failed",dc,second.Replay.Samples[^1],false)};
    }
    public static (TwoContactForces Force,RearWheelStep Wheel,RiderView Rider) Query(ContactState139 state,double steerDegrees,double? betaDegrees=null,double? speed=null,double? yaw=null,double? kappa=null,float? gas=null,
        PhysicsVariant139? variant=null,double wheelInertia=.9,double seconds=0){
        Queries++;var i=state.Input;var v=i.Rider;var beta=betaDegrees is{} b?b*Math.PI/180:v.SlideRadians;
        var magnitude=speed??v.Speed;var chi=v.Heading-beta;
        v=v with{Velocity=new((float)(magnitude*Math.Cos(chi)),(float)(magnitude*Math.Sin(chi))),AngularVelocity=(float)(yaw??v.AngularVelocity),Drive=gas??i.Active.Drive};
        var wp=RearWheelParameters.Default with{WheelInertiaKgMetersSquared=wheelInertia};
        var forward=new Vector2(MathF.Cos(v.Heading),MathF.Sin(v.Heading));var arm=-forward*TwoContactBikeParameters.Default.RearAxleFromComMeters;
        var ur=Vector2.Dot(TwoContactForces.PointVelocity(v.Velocity,v.AngularVelocity,arm),forward);
        var omega=kappa is{} slip?(ur+slip*Math.Sqrt(ur*ur+wp.LowSpeedMetersPerSecond*wp.LowSpeedMetersPerSecond))/wp.WheelRadiusMeters:i.Omega;
        if(omega<0)throw new ArgumentException("Map does not assume negative wheel speed.");
        var wheel=new RearWheelState(omega*wp.WheelRadiusMeters,wp);var angle=(float)(steerDegrees*Math.PI/180);
        var f=DiagnosticContact139.Forces(v,FreeLearningExperiment.Rider(state.Case.Scenario),Frame(state.Case.Geometry),
            Surface(state.Case.Geometry),FreeLearningExperiment.Parameters(),i.Time,147,wheel,new(angle),(float)seconds,steeringOverride:angle,inertia:52.72097f,variant:variant);
        return(f,wheel.Prepared,v);
    }
    static readonly Dictionary<string,TrackStateSnapshot> Surfaces=new();
    static readonly Dictionary<string,TrackFrame> Frames=new();
    static TrackFrame Frame(string geometry){if(!Frames.TryGetValue(geometry,out var f)){f=TrackAwarenessExperiment134.Frame(geometry);Frames[geometry]=f;}return f;}
    static TrackStateSnapshot Surface(string geometry){if(!Surfaces.TryGetValue(geometry,out var s)){s=MultiActionExperiment135.Surface(Frame(geometry));Surfaces[geometry]=s;}return s;}
    public static object Row(string kind,ContactState139 state,double steer,double? beta=null,double? speed=null,double? yaw=null,double? kappa=null,float? gas=null,PhysicsVariant139? variant=null,double wheelInertia=.9){
        var q=Query(state,steer,beta,speed,yaw,kappa,gas,variant,wheelInertia);var f=q.Force;var v=q.Rider;var chiRate=TwoContactForces.Cross(v.Velocity,f.NetForce)/(147*Math.Pow(Math.Max(.1,v.Speed),2));
        var frontForward=new Vector2(f.FrontLeft.Y,-f.FrontLeft.X);var frontSide=Vector2.Dot(f.FrontVelocity,f.FrontLeft);var frontAlong=Vector2.Dot(f.FrontVelocity,frontForward);
        var rearSide=Vector2.Dot(f.RearVelocity,f.BodyLeft);var rearAlong=Vector2.Dot(f.RearVelocity,f.BodyForward);
        var sf=f.TyreLoads!;var varp=variant??PhysicsVariant139.Original;var omegaDot=(q.Wheel.DriveTorqueNewtonMeters-.33*q.Wheel.LongitudinalNewtons-q.Wheel.BearingTorqueNewtonMeters)/wheelInertia;
        return new{Kind=kind,State=state.Name,SteerDegrees=steer,BetaDegrees=v.SlideRadians*180/Math.PI,SpeedMps=v.Speed,YawRate=v.AngularVelocity,Gas=v.Drive,
            Variant=varp.Name,WheelInertia=wheelInertia,FrontNormal=sf.FrontNormalNewtons,RearNormal=sf.RearNormalNewtons,FrontAlphaDegrees=Math.Atan2(frontSide,frontAlong)*180/Math.PI,
            RearAlphaDegrees=Math.Atan2(rearSide,rearAlong)*180/Math.PI,FrontLocalFx=Vector2.Dot(f.FrontForce,frontForward),FrontLocalFy=f.FrontLateral,
            FrontBodyFx=Vector2.Dot(f.FrontForce,f.BodyForward),FrontBodyFy=Vector2.Dot(f.FrontForce,f.BodyLeft),RearFx=f.RearLongitudinal,RearFy=f.RearLateral,
            FrontTorque=f.FrontTorque,RearTorque=f.RearTorque,NetTorque=f.NetTorque,YawAcceleration=f.NetTorque/(52.72097*varp.InertiaScale),ChiRate=chiRate,
            BetaRate=v.AngularVelocity-chiRate,OmegaDot=omegaDot,SpeedAcceleration=Vector2.Dot(v.Velocity,f.NetForce)/(147*Math.Max(.1,v.Speed)),
            FrontUse=f.FrontForce.Length()/sf.FrontCapacityNewtons,RearUse=f.RearForce.Length()/sf.RearCapacityNewtons,Kappa=q.Wheel.InstantSlipRatio,
            FrontLossWatts=-Vector2.Dot(f.FrontForce,f.FrontVelocity),RearLongLossWatts=q.Wheel.LongitudinalNewtons*q.Wheel.MidSlipVelocityMetersPerSecond,
            RearLatLossWatts=-f.RearLateral*rearSide,EngineRpm=f.Engine!.EngineRpm,DriveTorque=q.Wheel.DriveTorqueNewtonMeters,
            Basis="Instantaneous uncommitted contact query dt=0, NOT a native trajectory or reachability claim. Gas cannot instantly change Fx/Fy at fixed current wheel omega.",
            AnchorNativeReached=state.NativeReached,AnchorSnapshotLegal=state.LegalAtCapture,QueryReachability="Not established for the perturbed contact state"};
    }
    public static void Run(string d,RobustBudget budget){
        if(File.Exists(Path.Combine(d,"maps-complete.json")))return;
        var reservation=budget.Reserve("contact-maps","offline",false);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        Queries=0;var states=Read<ContactState139[]>(d,"contact-states.json");var rows=new List<object>();
        foreach(var state in states)for(var steer=-35;steer<=35;steer++)rows.Add(Row("steering",state,steer));
        foreach(var state in new[]{states[0],states[2]})for(var beta=-45;beta<=45;beta+=5)for(var steer=-35;steer<=35;steer+=5)rows.Add(Row("beta-steering",state,steer,beta,gas:1));
        foreach(var speed in new[]{10d,15,20,25,30})foreach(var beta in new[]{0d,10,20,30,40})foreach(var yaw in new[]{0d,.5,1,2.875})for(var steer=-35;steer<=35;steer+=5)
            rows.Add(Row("speed-authority",states[0],steer,beta,speed,yaw,gas:1));
        foreach(var speed in new[]{10d,20,30})foreach(var beta in new[]{-45d,-20,-5,0,5,20,45})foreach(var kappa in new[]{-.5,0,.1,.25,.5,1,2,2.56,3,5})foreach(var gas in new[]{0f,(float)CausalYawExperiment137.Policy.Drive,1f})
            rows.Add(Row("rear-slip",states[0],0,beta,speed,0,kappa,gas));
        foreach(var state in new[]{states[0],states[2]})foreach(var fraction in new[]{.3,.35,.4,6d/13,.5,.55,.6})for(var steer=-35;steer<=35;steer+=5)
            rows.Add(Row("load-split",state,steer,gas:1,variant:PhysicsVariant139.Original with{Name="load-"+fraction,FrontFraction=fraction}));
        foreach(var state in new[]{states[0],states[2]})foreach(var front in new[]{.55,.6,.65,.7,.75})for(var steer=-35;steer<=35;steer+=5)
            rows.Add(Row("geometry-com",state,steer,gas:1,variant:PhysicsVariant139.Original with{Name="front-arm-"+front,FrontArm=front,RearArm=1.3-front}));
        foreach(var state in states)foreach(var factor in new[]{1d,2,4})rows.Add(Row("front-slope",state,state.Input.Steering*180/Math.PI,variant:PhysicsVariant139.Original with{Name="front-scale-"+factor,FrontScaleSquared=factor}));
        foreach(var state in states)foreach(var factor in new[]{.5,1,2})rows.Add(Row("rear-stiffness",state,state.Input.Steering*180/Math.PI,variant:PhysicsVariant139.Original with{Name="rear-scale-"+factor,RearLongScale=factor}));
        foreach(var state in states)foreach(var factor in new[]{1d,2,3})rows.Add(Row("numerical-contact-regularization",state,state.Input.Steering*180/Math.PI,variant:PhysicsVariant139.Original with{Name="regularizer-"+factor,ContactRegularization=factor}));
        foreach(var state in states)foreach(var inertia in new[]{.45,.9,1.8})rows.Add(Row("wheel-inertia-constitutive",state,state.Input.Steering*180/Math.PI,wheelInertia:inertia));
        foreach(var state in states)foreach(var inertia in new[]{.75,1d,1.25})rows.Add(Row("yaw-inertia",state,state.Input.Steering*180/Math.PI,variant:PhysicsVariant139.Original with{Name="yaw-inertia-"+inertia,InertiaScale=inertia}));
        // Temporal relaxation is deliberately not invented: a first-order hypothetical
        // step response is shown analytically, separately from these memoryless contact laws.
        var relaxation=from tau in new[]{.01,.03,.06,.1} from time in Enumerable.Range(0,21).Select(j=>j*.01)
            select new{TauSeconds=tau,TimeSeconds=time,NormalizedResponse=1-Math.Exp(-time/tau),Calibrated=false,NativeWorlds=0};
        Save(d,"relaxation-hypothesis.json",relaxation.ToArray());ThrottleSteering138.Zip(Path.Combine(d,"contact-maps.json.gz"),rows);
        var roots=new List<object>();
        foreach(var state in states){
            var scan=Enumerable.Range(-35,71).Select(angle=>(Angle:(double)angle,Force:Query(state,angle,gas:1).Force)).ToArray();var rr=new List<object>();
            foreach(var(a,b)in scan.Zip(scan.Skip(1))){
                if(a.Force.NetTorque*b.Force.NetTorque>0)continue;double lo=a.Angle,hi=b.Angle;var sign=Math.Sign(a.Force.NetTorque);
                for(var j=0;j<20;j++){var mid=(lo+hi)/2;var torque=Query(state,mid,gas:1).Force.NetTorque;if(Math.Sign(torque)==sign)lo=mid;else hi=mid;}
                var angle=(lo+hi)/2;rr.Add(Row("yaw-balance-root",state,angle,gas:1));
            }
            roots.Add(new{State=state.Name,MinimumYawMoment=scan.Min(x=>x.Force.NetTorque),MaximumYawMoment=scan.Max(x=>x.Force.NetTorque),Roots=rr,
                Meaning="Tau=0 only. Inspect BetaRate, OmegaDot and SpeedAcceleration; this is not a dynamically stable trajectory."});
        }
        Save(d,"yaw-balance-roots.json",roots);
        var checks=ValidateNativeBenchmark();Save(d,"contact-invariants.json",checks);Queries++;
        budget.Complete(reservation,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,Queries,0);
        Save(d,"maps-complete.json",new{NativeWorlds=0,Queries,Rows=rows.Count,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,Passed=true});
    }
    public static object ValidateNativeBenchmark(){
        var run=RobustLearningExperiment.ReadEpisode(Old+"/native","D0-B");var sample=run.Replay.Samples[^2];var interval=run.Telemetry[^1];var i=sample.Input;
        var wheel=new RearWheelState(i.Omega*.33);var f=TwoContactBikeDynamics.Forces(i.Rider,FreeLearningExperiment.Rider(run.Replay.Scenario),TrackAwarenessExperiment134.Frame(run.GeometryName!),
            Surface(run.GeometryName!),FreeLearningExperiment.Parameters(),i.Time,147,wheel,new(i.Steering),(float)interval.Seconds,steeringOverride:i.Active.SteeringRadians,inertia:52.72097f);
        var front=Vector2.Distance(f.FrontForce,interval.Forces.FrontForce);var rear=Vector2.Distance(f.RearForce,interval.Forces.RearForce);
        if(front>.001||rear>.001||Math.Abs(wheel.Prepared.AfterOmega-interval.Wheel.AfterOmega)>.001)throw new InvalidOperationException("Saved native force reproduction failed.");
        var ff=new Vector2(f.FrontLeft.Y,-f.FrontLeft.X);var frontDrive=Vector2.Dot(f.FrontForce,ff);var power=Vector2.Dot(f.FrontForce,f.FrontVelocity);
        if(Math.Abs(frontDrive)>.001||power>.001)throw new InvalidOperationException("Non-driven front contact invariant failed.");
        return new{Passed=true,FrontForceErrorNewtons=front,RearForceErrorNewtons=rear,FrontLocalDriveNewtons=frontDrive,FrontContactPowerWatts=power,
            NativeFrontTorque=interval.Forces.FrontTorque,NativeRearTorque=interval.Forces.RearTorque,NativeNetTorque=interval.Forces.NetTorque,
            Note="Exact previous accepted state and actual interval; not dt0 force at the failed end state."};
    }
}
