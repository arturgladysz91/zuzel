using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

/// <summary>One frozen 2x2 attribution; no search, no artificial speed or position constraints.</summary>
public static class MotorcyclePerformanceAudit
{
    public const string BaseHead="d1325442e08233a9f44279c27410321f62a4a6e0";
    public static readonly string[] Variants={"baseline","A-engine","B-tyres","C-both"};
    public static WorldParameters Parameters(string variant)=>Variants.Contains(variant)?PhysicalSteeringAudit.Parameters() with{
        Enable500CcEngine=variant is "A-engine" or "C-both",EnableLoadBasedTyreGrip=variant is "B-tyres" or "C-both"}:throw new ArgumentException("Unknown performance variant.",nameof(variant));
    public static Track Fixture(float radius,bool longStraight)=>new(new TrackSegment[]{
        new(0,SegmentType.Straight,longStraight?5000:35),new(1,SegmentType.TurnEntry),new(2,SegmentType.TurnMiddle),new(3,SegmentType.TurnExit),
        new(4,SegmentType.Straight,longStraight?10000:62),new(5,SegmentType.TurnEntry),new(6,SegmentType.TurnMiddle),new(7,SegmentType.TurnExit),
        new(8,SegmentType.Straight,longStraight?5000:27)},new TrackGeometry(longStraight?10000:62,radius,12,16.6f,MathF.PI/3));
    public static TwoContactBikeAudit.Capture Isolated(string name,WorldParameters p,bool straight,float radius=31,double maximumSeconds=30)
    {
        var track=Fixture(radius,straight);var frame=new TrackFrame(track);var start=straight?4:35.05;
        var spec=RacingLineRootCauseAudit.Rider(AuthoredRacingPath.Crossover(false)) with{
            ReferenceMeters=start,OffsetMeters=5,TargetOffsetMeters=5,SpeedMetersPerSecond=18,TrafficEnabled=false};
        var ticks=new List<TwoContactBikeAudit.Tick>();TwoContactForces forces=default;RearWheelStep wheel=default;RiderView before=default;string? stop=null;
        using var world=new WorldRunner(new[]{spec},track:track,parameters:p with{EnableLocalOffsetLimit=false},
            frozenSurface:TrackState.CreateDefault(track,new(1,0,.5f)).Snapshot(),
            controls:new[]{new ControlCommand(spec.Profile.Id,0,5,1)},
            twoContactInitial:new(BodyAngularVelocity:straight?0:18/(radius+5)),
            steeringCommands:straight?new[]{new TwoContactSteeringCommand(spec.Profile.Id,0,0)}:null,
            diagnostics:new(){Force=f=>before=f.Rider,TwoContact=f=>forces=f.Forces,RearWheel=w=>wheel=w.Step});
        var initial=world.Snapshot;var mechanical=world.MechanicalProperties(spec.Profile.Id);var geometry=new RacingLineGeometry(frame);double previousTime=0;
        var result=world.Run(name,.01,maximumSeconds,acceptedObserver:s=>{
            var after=s.Riders[0];var gap=geometry.Clearance(after);ticks.Add(new(previousTime,(float)wheel.Seconds,before,after,forces,wheel,gap.Inner,gap.Outer));previousTime=s.TimeSeconds;
        },diagnosticStop:s=>{
            var t=ticks[^1];var delta=TwoContactBikeAudit.BodyKinetic(t.After,mechanical.MassKg,mechanical.InertiaKgMetersSquared)-TwoContactBikeAudit.BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared)
                +.45*(t.Wheel.AfterOmega*t.Wheel.AfterOmega-t.Wheel.BeforeOmega*t.Wheel.BeforeOmega);
            var tolerance=.05+1e-6*Math.Max(TwoContactBikeAudit.BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared),TwoContactBikeAudit.BodyKinetic(t.After,mechanical.MassKg,mechanical.InertiaKgMetersSquared));
            if(delta-t.Wheel.EngineWorkJoules>tolerance)stop="energy increase beyond engine work/float tolerance";
            if(t.Inner<0||t.Outer<0)stop=t.Inner<0?"observed inner footprint crossing":"observed outer footprint crossing";
            return stop is not null||!straight&&s.Riders[0].TotalReferenceMeters>=frame.SegmentStartMeters(4);
        });
        return new(initial,result,ticks.ToArray(),mechanical.MassKg,mechanical.InertiaKgMetersSquared,0,stop);
    }
    public static object Protocol()=>new{BaseHead,Engine=Engine500Parameters.Default,Curve=Engine500.Curve,Engine500.CutoffRpm,
        Tyres=LoadBasedTyreParameters.Default,Bike=TwoContactBikeParameters.Default,Wheel=RearWheelParameters.Default,
        Variants=Variants.Select(v=>new{Name=v,Parameters=Parameters(v)}),
        Rule="One 2x2 attribution, original mass/geometry/drag/gains/rider. All rolling starts18m/s. Four straight worlds30s, eight native constant-radius half-bends (R36/46m at offset5, full throttle), A/B/C first Egg bend. C alone advances first->second->lap->eight only after executed safety/energy gate. At most one adaptive C diagnostic. No fitting or extra solver in Forces. Zero dynamic axle transfer."};
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="freeze"){InverseFeasibilityAudit.Save(dir,"protocol.json",Protocol());return;}
        using var protocol=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"protocol.json")));
        if(protocol.RootElement.GetRawText()!=JsonSerializer.Serialize(Protocol(),InverseFeasibilityAudit.Json))throw new InvalidOperationException("Frozen performance inputs changed.");
        if(stage=="diagnose"){Diagnose(dir);return;}
        if(stage=="cost"){Cost(dir);return;}
        if(stage=="loss-of-control"){LossOfControl(dir);return;}
        if(stage=="straight"){
            foreach(var variant in Variants){var watch=Stopwatch.GetTimestamp();var c=Isolated(variant+"-straight",Parameters(variant),true);Export(dir,variant+"-straight",c,Stopwatch.GetElapsedTime(watch).TotalMilliseconds);}
            return;
        }
        if(stage=="arcs"){
            foreach(var radius in new[]{31f,41f})foreach(var variant in Variants){var name=variant+"-arc-"+(radius+5);var watch=Stopwatch.GetTimestamp();
                var c=Isolated(name,Parameters(variant),false,radius,12);Export(dir,name,c,Stopwatch.GetElapsedTime(watch).TotalMilliseconds);}
            return;
        }
        if(stage=="egg-first"){
            foreach(var variant in Variants.Skip(1))Egg(dir,variant,"first",12,0,AuthoredRacingPath.Crossover(false).Frame.SegmentStartMeters(4)+1.05);
            return;
        }
        if(stage=="adaptive"){
            var p=Parameters("C-both") with{EnableAdaptiveRiderControl=true,EnableLocalOffsetLimit=false};var watch=Stopwatch.GetTimestamp();
            var c=TwoContactBikeAudit.Execute(new("C-adaptive-diagnostic",Seconds:12,Drive:true),track:true,stopUnsafe:true,parameters:p,
                stopReference:AuthoredRacingPath.Crossover(false).Frame.SegmentStartMeters(4)+1.05);
            Export(dir,"C-adaptive-diagnostic",c,Stopwatch.GetElapsedTime(watch).TotalMilliseconds);return;
        }
        var frame=AuthoredRacingPath.Crossover(false).Frame;
        if(stage=="egg-second"){RequireGate(dir,"C-both-egg-first");Egg(dir,"C-both","second",25,0,frame.SegmentStartMeters(8)+1.05);return;}
        if(stage=="egg-lap"){RequireGate(dir,"C-both-egg-second");Egg(dir,"C-both","lap",40,1,32+frame.LapMeters);return;}
        if(stage=="egg-eight"){RequireGate(dir,"C-both-egg-lap");Egg(dir,"C-both","eight",200,8,32+8*frame.LapMeters);return;}
        throw new ArgumentException("Use freeze, straight, arcs, egg-first, egg-second, egg-lap, egg-eight, adaptive.");
    }
    private static void Diagnose(string dir)
    {
        var frame=new TrackFrame(Fixture(31,true));var spec=RacingLineRootCauseAudit.Rider(AuthoredRacingPath.Crossover(false)) with{ReferenceMeters=4,OffsetMeters=5,TargetOffsetMeters=5,SpeedMetersPerSecond=18};
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var at=frame.At(4,5);var results=new List<object>();
        foreach(var variant in Variants){var p=Parameters(variant);long calls=0;
            (TwoContactForces Forces,RearWheelStep Wheel) Query(double speed,double omega){calls++;
                var v=new RiderView(1,at.Position,new((float)speed,0),0,0,at.Road,4,0,RiderActivity.Racing,1,5,0);var wheel=new RearWheelState(omega*.33);
                var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,0,147,wheel,new(),0,steeringOverride:0,inertia:52.72097f);return(f,wheel.Prepared);
            }
            double Balance(double speed,double omega){var q=Query(speed,omega);return q.Wheel.DriveTorqueNewtonMeters-.33*q.Wheel.LongitudinalNewtons-q.Wheel.BearingTorqueNewtonMeters;}
            double WheelEquilibrium(double speed){
                // Last positive->negative torque root follows the running high-rpm
                // branch, rather than silently selecting a stalled low-rpm root.
                double low=0,high=500,previousOmega=0,previousValue=Balance(speed,0);var found=false;
                for(var i=1;i<=32;i++){var omega=500d*i/32;var value=Balance(speed,omega);
                    if(previousValue>=0&&value<=0){low=previousOmega;high=omega;found=true;}previousOmega=omega;previousValue=value;}
                if(!found)throw new InvalidOperationException("No running-branch wheel equilibrium bracket.");
                for(var i=0;i<22;i++){var mid=(low+high)/2;if(Balance(speed,mid)>0)low=mid;else high=mid;}return(low+high)/2;
            }
            double Net(double speed){var q=Query(speed,WheelEquilibrium(speed));return q.Forces.NetForce.X;}
            double vLow=5,vHigh=70;if(Net(vLow)<=0||Net(vHigh)>=0)throw new InvalidOperationException("No long-straight equilibrium bracket.");
            for(var i=0;i<18;i++){var mid=(vLow+vHigh)/2;if(Net(mid)>0)vLow=mid;else vHigh=mid;}
            var equilibrium=(vLow+vHigh)/2;var wheelOmega=WheelEquilibrium(equilibrium);var final=Query(equilibrium,wheelOmega);
            var samples=new List<object>();foreach(var speed in new[]{18d,22,26,30,34,38}){
                var q=Query(speed,speed/.33);var steady=Query(speed,WheelEquilibrium(speed));
                samples.Add(new{Speed=speed,RollingShaftWheelTorque=q.Wheel.DriveTorqueNewtonMeters,RollingShaftPowerWatts=q.Wheel.DriveTorqueNewtonMeters*speed/.33,
                    SteadyWheelOmega=steady.Wheel.BeforeOmega,SteadyFx=steady.Wheel.LongitudinalNewtons,Resistance=-steady.Forces.Resistance.X,
                    NetAcceleration=steady.Forces.NetForce.X/147,FrontCapacity=steady.Forces.TyreLoads?.FrontCapacityNewtons??.4*steady.Forces.Budget,
                    RearCapacity=steady.Forces.TyreLoads?.RearCapacityNewtons??.6*steady.Forces.Budget,OriginalBudget=steady.Forces.Budget});
            }
            results.Add(new{Variant=variant,EquilibriumSpeed=equilibrium,WheelOmega=wheelOmega,EngineRpm=final.Forces.Engine?.EngineRpm,
                Fx=final.Wheel.LongitudinalNewtons,Resistance=-final.Forces.Resistance.X,NetForceResidual=final.Forces.NetForce.X,
                TorqueResidual=Balance(equilibrium,wheelOmega),ForceQueries=calls,Samples=samples});
        }
        InverseFeasibilityAudit.Save(dir,"steady-straight.json",new{Worlds=0,Commits=0,Rule="Stationary wheel torque balance plus body force balance, running high-rpm root. 18 speed bisections (~0.00025m/s), 22 wheel bisections, no trajectory fitting or speed clamp. Compare separately with 30s native run and 62m crossing.",Results=results});
    }
    private static void RequireGate(string dir,string name)
    {
        using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,name+"-gate.json")));
        if(!gate.RootElement.GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Previous Egg safety/energy gate failed: longer run prohibited.");
    }
    private static void Cost(string dir)
    {
        var rows=DynamicAxleLoadAudit.ReadSaved("first-bend").Where((_,i)=>i%3==0).ToArray();var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;
        var spec=RacingLineRootCauseAudit.Rider(path);var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var views=rows.Select(r=>PhysicalSteeringAudit.View(r,frame)).ToArray();var wheels=rows.Select(r=>new RearWheelState(r["omega_before"]*.33)).ToArray();
        var guidance=views.Select(v=>path.Guidance(v,Parameters("baseline"),true)).ToArray();const int repetitions=8;
        var steering=rows.Select(r=>new TwoContactBikeState((float)r["steer_used"])).ToArray();double checksum=0;
        var times=Variants.ToDictionary(v=>v,_=>new List<double>());var iterations=new Dictionary<string,long>();
        double Batch(string variant){var p=Parameters(variant);long roots=0;var started=Stopwatch.GetTimestamp();
            for(var repeat=0;repeat<repetitions;repeat++)for(var i=0;i<rows.Length;i++){var f=TwoContactBikeDynamics.Forces(views[i],spec,frame,surface,p,rows[i]["t"],147,wheels[i],steering[i],(float)rows[i]["dt"],
                guidance[i],(float)rows[i]["steer_used"],inertia:52.72097f);checksum+=f.NetForce.X;roots+=wheels[i].Prepared.RootIterations;}
            iterations[variant]=roots;return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
        foreach(var v in Variants)Batch(v);
        for(var round=0;round<3;round++)foreach(var v in round%2==0?Variants:Variants.Reverse())times[v].Add(Batch(v));
        InverseFeasibilityAudit.Save(dir,"matched-force-cost.json",new{Worlds=0,Commits=0,StatesPerBatch=rows.Length,Repetitions=repetitions,BatchesPerVariant=4,TimesMilliseconds=times,
            MedianMilliseconds=times.ToDictionary(x=>x.Key,x=>x.Value.Order().ElementAt(1)),WheelRootIterations=iterations,Checksum=checksum,
            Rule="Identical decoded saved #113 first-bend states, guidance and steering, one warm+three alternating batches. Guidance decoded BEFORE timing. Eight fixed repetitions avoid a sub-millisecond sample. Same fixed inputs isolate force-evaluation overhead, not trajectory-dependent inverse root branches. No timing threshold or parameter fitting."});
    }
    private static void LossOfControl(string dir)
    {
        var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var spec=RacingLineRootCauseAudit.Rider(path);
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var samples=new List<object>();double maxDifference=0;
        foreach(var variant in Variants.Skip(1)){
            var rows=ReadRows(dir,variant+"-egg-first");var p=Parameters(variant);
            foreach(var r in rows.Where(r=>new[]{.5,1,1.5,2,2.5,3,4}.Any(t=>Math.Abs(r["t"]-t)<1e-7))){
                var v=PhysicalSteeringAudit.View(r,frame);var wheel=new RearWheelState(r["omega_before"]*.33);var steer=new TwoContactBikeState((float)r["steer_before"]);
                var acceptedOmega=wheel.AngularVelocityRadPerSecond;
                var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,r["t"],147,wheel,steer,(float)r["dt"],path.Guidance(v,p,true),inertia:52.72097f);
                maxDifference=Math.Max(maxDifference,new[]{Math.Abs(f.FrontForce.X-r["front_fx"]),Math.Abs(f.FrontForce.Y-r["front_fy"]),Math.Abs(f.RearForce.X-r["rear_fx"]),Math.Abs(f.RearForce.Y-r["rear_fy"])}.Max());
                var br=f.TyreLoads?.RearCapacityNewtons??.6*f.Budget;var bf=f.TyreLoads?.FrontCapacityNewtons??.4*f.Budget;
                samples.Add(new{Variant=variant,Time=r["t"],SpeedKph=v.Speed*3.6,BetaDegrees=v.SlideRadians*180/Math.PI,BodyYaw=v.AngularVelocity,
                    MotionYaw=TwoContactForces.Cross(v.Velocity,f.NetForce)/(147*v.Speed*v.Speed),RearLongitudinalFraction=Math.Abs(f.RearLongitudinal)/br,
                    RearLateralFraction=Math.Abs(f.RearLateral)/br,FrontLateralFraction=Math.Abs(f.FrontLateral)/bf,
                    Wheelspin=wheel.Prepared.InstantSlipRatio,Engine=f.Engine,Steering=f.SteeringControl,f.NetTorque,WheelRootResidual=wheel.Prepared.RootResidualMetersPerSecond});
                if(wheel.AngularVelocityRadPerSecond!=acceptedOmega||steer.SteeringRadians!=(float)r["steer_before"])throw new InvalidOperationException("Saved query committed state.");
            }
        }
        if(maxDifference!=0)throw new InvalidOperationException("Saved force replay differs.");
        InverseFeasibilityAudit.Save(dir,"loss-of-control.json",new{Worlds=0,Commits=0,MaximumForceReplayDifferenceNewtons=maxDifference,Samples=samples,
            Rule="Original saved actual Egg states, identical controls and original #113 inverse. Query only, exact replay, no changed trajectory or parameter. Direction/body rate divergence is observed, not forced away by a beta actuator."});
    }
    private static Dictionary<string,double>[] ReadRows(string dir,string name)
    {
        var file=Path.Combine(dir,name+".csv");var compressed=!File.Exists(file);using var stream=File.OpenRead(compressed?file+".gz":file);
        using var reader=new StreamReader(compressed?new GZipStream(stream,CompressionMode.Decompress):stream);
        var keys=reader.ReadLine()!.Split(',');var rows=new List<Dictionary<string,double>>();
        while(reader.ReadLine() is { } line)rows.Add(line.Split(',').Select((v,i)=>(keys[i],double.Parse(v,CultureInfo.InvariantCulture))).ToDictionary(x=>x.Item1,x=>x.Item2));
        return rows.ToArray();
    }
    private static void Egg(string dir,string variant,string stage,double seconds,int laps,double reference)
    {
        var name=variant+"-egg-"+stage;var watch=Stopwatch.GetTimestamp();
        var c=TwoContactBikeAudit.Execute(new(name,Seconds:seconds,Drive:true),track:true,laps:laps,stopUnsafe:true,parameters:Parameters(variant),stopReference:reference);
        Export(dir,name,c,Stopwatch.GetElapsedTime(watch).TotalMilliseconds);
        InverseFeasibilityAudit.Save(dir,name+"-gate.json",new{Passed=AdaptiveRiderAudit.Safe(c,reference),Reference=reference,c.StopReason,
            Rule="Existing #113 executed footprint, conservative between-step margin and native impulse/energy bounds. Reaching a boundary halts, never changes throttle or position."});
    }
    private static void Export(string dir,string name,TwoContactBikeAudit.Capture c,double elapsed)
    {
        TwoContactBikeAudit.Export(dir,name,c);
        using var writer=InverseFeasibilityAudit.Csv(dir,name+"-performance.csv","t,dt,distance,offset,ax,engine_rpm,engine_torque,full_crank_power,wheel_torque,wheel_power,crank_work,transmission_loss,mu,front_normal,rear_normal,front_capacity,rear_capacity,front_alpha,rear_alpha,motion_yaw,motion_curvature,queries");
        foreach(var t in c.Ticks){var f=t.Forces;var e=f.Engine;var w=t.Wheel;var loads=f.TyreLoads;
            var theta=Math.Atan2(t.After.Velocity.Y,t.After.Velocity.X)-Math.Atan2(t.Before.Velocity.Y,t.Before.Velocity.X);theta=Math.Atan2(Math.Sin(theta),Math.Cos(theta));
            var distance=Vector2.Distance(t.After.Position,t.Before.Position);
            InverseFeasibilityAudit.Row(writer,t.Time,t.Seconds,t.Before.DistanceMeters,t.Before.Road.OffsetMeters,(t.After.Speed-t.Before.Speed)/t.Seconds,
                e?.EngineRpm??0,e?.CrankTorqueNewtonMeters??0,e?.CrankPowerWatts??0,w.DriveTorqueNewtonMeters,w.EngineWorkJoules/t.Seconds,
                e?.CrankWorkJoules(w)??w.EngineWorkJoules,e?.TransmissionLossJoules(w)??0,loads?.EffectiveFrictionCoefficient??0,
                loads?.FrontNormalNewtons??0,loads?.RearNormalNewtons??0,loads?.FrontCapacityNewtons??.4*f.Budget,loads?.RearCapacityNewtons??.6*f.Budget,
                Math.Atan2(Vector2.Dot(f.FrontVelocity,f.FrontLeft),Vector2.Dot(f.FrontVelocity,new(f.FrontLeft.Y,-f.FrontLeft.X))),
                Math.Atan2(Vector2.Dot(f.RearVelocity,f.BodyLeft),Vector2.Dot(f.RearVelocity,f.BodyForward)),theta/t.Seconds,theta/Math.Max(distance,1e-12),f.SteeringControl?.ContactQueries??0);
        }
        InverseFeasibilityAudit.Save(dir,name+"-work.json",new{ElapsedMilliseconds=elapsed,SteeringQueries=c.Ticks.Sum(t=>(long)(t.Forces.SteeringControl?.ContactQueries??0)),
            BoundaryForceQueries=c.Result.Work.RearWheelBoundaryEvaluations,PhysicalSteps=c.Ticks.Length,WheelCommits=c.Result.Work.RearWheelCommits,
            CrankWorkJoules=c.Ticks.Sum(t=>t.Forces.Engine?.CrankWorkJoules(t.Wheel)??t.Wheel.EngineWorkJoules),
            TransmissionLossJoules=c.Ticks.Sum(t=>t.Forces.Engine?.TransmissionLossJoules(t.Wheel)??0),
            Rule="CPU with diagnostics; timing not a CI threshold. Legacy engine has no engine RPM/gear physical state: zeros in engine-specific columns are N/A, not a stopped shaft. Its actual wheel torque/power remain observed."});
    }
}
