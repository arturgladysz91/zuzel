using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public static class TravelDirectionAudit
{
    public const string BaseHead="653c702bccefaa6bc83b20f1c646ddb869a58223";
    public static WorldParameters Parameters()=>MotorcyclePerformanceAudit.Parameters("C-both") with{EnableTravelDirectionSteering=true};
    public static Dictionary<string,double>[] Saved()=>DynamicAxleLoadAudit.ReadSaved("../motorcycle-performance-500cc/C-both-egg-first");
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="freeze"){
            InverseFeasibilityAudit.Save(dir,"protocol.json",new{BaseHead,Parameters=Parameters(),TravelDirectionSteering.ControlledSlideLimitRadians,
                TravelDirectionSteering.ResponseIntervals,TravelDirectionSteering.RefinementIterations,
                Rule="Saved C first, no Box2D. One achievable short arc, one Egg full-gas diagnostic, one explicit early throttle decision. No laps, no tyre/engine fitting, no #114, no hidden throttle. Original contact/energy limits."});return;
        }
        using var protocol=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"protocol.json")));
        if(protocol.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(Parameters(),InverseFeasibilityAudit.Json))throw new InvalidOperationException("Frozen inputs changed.");
        if(stage=="saved"){SavedStates(dir);return;}
        if(!File.Exists(Path.Combine(dir,"saved-states.json")))throw new InvalidOperationException("Saved state diagnosis must precede worlds.");
        if(stage=="cost"){Cost(dir);return;}
        var watch=Stopwatch.GetTimestamp();TwoContactBikeAudit.Capture c;
        if(stage=="arc")c=Arc("achievable-arc",14,.35f,null,3);
        else{
            using var arc=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"arc.json")));
            var a=arc.RootElement;var e=a.GetProperty("Energy");
            if(a.GetProperty("StopReason").ValueKind!=JsonValueKind.Null||a.GetProperty("ConservativeInner").GetDouble()<=.5||a.GetProperty("ConservativeOuter").GetDouble()<=.5
                ||a.GetProperty("MaximumSlideDegrees").GetDouble()>=45||e.GetProperty("MaximumExcessBeyondEngineJoules").GetDouble()!=0
                ||e.GetProperty("MaximumLinearImpulseResidualNs").GetDouble()>=.001||e.GetProperty("MaximumAngularImpulseResidualNms").GetDouble()>=.00003)
                throw new InvalidOperationException("Short arc safety/energy gate failed; no further worlds.");
            if(stage=="egg")c=TwoContactBikeAudit.Execute(new("egg-full-gas",Seconds:12,Drive:true),track:true,stopUnsafe:true,parameters:Parameters(),
                stopReference:AuthoredRacingPath.Crossover(false).Frame.SegmentStartMeters(4)+1.05);
            else if(stage=="rider")c=Arc("explicit-rider-action",18,1,.35f,3);
            else if(stage=="rider-wide")c=Arc("explicit-rider-wide-action",18,1,.35f,3,7);
            else if(stage=="rider-coast")c=Arc("explicit-rider-coast-action",18,1,0,3);
            else throw new ArgumentException("freeze, saved, cost, arc, egg, rider, rider-wide, rider-coast");
        }
        Export(dir,stage,c,Stopwatch.GetElapsedTime(watch).TotalMilliseconds);
    }
    public static TwoContactBikeAudit.Capture Arc(string name,float speed,float drive,float? earlyDrive,double seconds,float earlyTarget=5)
    {
        var track=MotorcyclePerformanceAudit.Fixture(41,false);var frame=new TrackFrame(track);const double start=35.05;
        var spec=RacingLineRootCauseAudit.Rider(AuthoredRacingPath.Crossover(false)) with{
            ReferenceMeters=start,OffsetMeters=5,TargetOffsetMeters=5,SpeedMetersPerSecond=speed,TrafficEnabled=false};
        var controls=new List<ControlCommand>{new(spec.Profile.Id,0,5,drive)};
        if(earlyDrive is { } d)controls.Add(new(spec.Profile.Id,.2,earlyTarget,d));
        var ticks=new List<TwoContactBikeAudit.Tick>();TwoContactForces forces=default;RearWheelStep wheel=default;RiderView before=default;string? stop=null;
        using var world=new WorldRunner(new[]{spec},track:track,parameters:Parameters() with{EnableLocalOffsetLimit=false},controls:controls,
            frozenSurface:TrackState.CreateDefault(track,new(1,0,.5f)).Snapshot(),twoContactInitial:new(BodyAngularVelocity:speed/46),
            diagnostics:new(){Force=f=>before=f.Rider,TwoContact=f=>forces=f.Forces,RearWheel=w=>wheel=w.Step});
        var initial=world.Snapshot;var mechanical=world.MechanicalProperties(spec.Profile.Id);var geometry=new RacingLineGeometry(frame);double previous=0;
        var result=world.Run(name,.01,seconds,acceptedObserver:s=>{var after=s.Riders[0];var gap=geometry.Clearance(after);
            ticks.Add(new(previous,(float)wheel.Seconds,before,after,forces,wheel,gap.Inner,gap.Outer));previous=s.TimeSeconds;
        },diagnosticStop:s=>{
            var t=ticks[^1];var delta=TwoContactBikeAudit.BodyKinetic(t.After,mechanical.MassKg,mechanical.InertiaKgMetersSquared)-TwoContactBikeAudit.BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared)
                +.45*(t.Wheel.AfterOmega*t.Wheel.AfterOmega-t.Wheel.BeforeOmega*t.Wheel.BeforeOmega);
            if(delta-t.Wheel.EngineWorkJoules>.05+1e-6*TwoContactBikeAudit.BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared))stop="energy increase beyond engine work/float tolerance";
            if(t.Inner<0||t.Outer<0)stop=t.Inner<0?"observed inner footprint crossing":"observed outer footprint crossing";
            return stop is not null;
        });
        return new(initial,result,ticks.ToArray(),mechanical.MassKg,mechanical.InertiaKgMetersSquared,0,stop);
    }
    private static void SavedStates(string dir)
    {
        var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var spec=RacingLineRootCauseAudit.Rider(path);
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var samples=new List<object>();double replay=0;long oldQueries=0,newQueries=0;
        foreach(var row in Saved()){
            var v=PhysicalSteeringAudit.View(row,frame);
            TwoContactForces Query(bool enabled){var p=Parameters() with{EnableTravelDirectionSteering=enabled};var wheel=new RearWheelState(row["omega_before"]*.33);
                var steer=new TwoContactBikeState((float)row["steer_before"]);var omega=wheel.AngularVelocityRadPerSecond;
                var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,row["t"],147,wheel,steer,(float)row["dt"],path.Guidance(v,p,true),inertia:52.72097f);
                if(wheel.AngularVelocityRadPerSecond!=omega||steer.SteeringRadians!=(float)row["steer_before"])throw new InvalidOperationException("Local query committed state.");return f;}
            var old=Query(false);var next=Query(true);oldQueries+=old.SteeringControl!.ContactQueries;newQueries+=next.TravelSteering!.ContactQueries;
            replay=Math.Max(replay,new[]{Math.Abs(old.FrontForce.X-row["front_fx"]),Math.Abs(old.FrontForce.Y-row["front_fy"]),Math.Abs(old.RearForce.X-row["rear_fx"]),Math.Abs(old.RearForce.Y-row["rear_fy"])}.Max());
            var rear=old.TyreLoads!.RearCapacityNewtons;var longitudinal=old.RearLongitudinal/rear;
            samples.Add(new{Time=row["t"],Speed=v.Speed,BetaDegrees=v.SlideRadians*180/Math.PI,
                Old=old.SteeringControl,New=next.TravelSteering,OldMotion=TravelDirectionState.Observe(v,old.NetForce,147),NewMotion=TravelDirectionState.Observe(v,next.NetForce,147),
                RearFx=old.RearLongitudinal,RearFy=old.RearLateral,RearCapacity=rear,RearLongitudinalFraction=longitudinal,
                RemainingLateralCircleNewtons=rear*Math.Sqrt(Math.Max(0,1-longitudinal*longitudinal)),
                AbsoluteAllContactChiDotBound=old.Budget/(147*v.Speed),
                OldTorque=old.NetTorque,NewTorque=next.NetTorque,OldSteer=old.SteeringAfter,NewSteer=next.SteeringAfter});
        }
        if(replay!=0)throw new InvalidOperationException("Original C replay changed.");
        InverseFeasibilityAudit.Save(dir,"saved-states.json",new{Worlds=0,Commits=0,States=samples.Count,MaximumForceReplayDifference=replay,OldQueries=oldQueries,NewQueries=newQueries,Samples=samples,
            Caveat="Sampled steering envelopes are local at frozen wheel/body states, not a proof of future recovery. Absolute total force bound can exclude curvature even if steering were instantaneous. No Box2D, RNG or wear."});
    }
    private static void Cost(string dir)
    {
        var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var spec=RacingLineRootCauseAudit.Rider(path);
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var rows=Saved();var views=rows.Select(r=>PhysicalSteeringAudit.View(r,frame)).ToArray();
        var guidance=views.Select(v=>path.Guidance(v,Parameters(),true)).ToArray();var times=new[]{new List<double>(),new List<double>()};var counts=new long[2];double checksum=0;
        for(var batch=0;batch<4;batch++)foreach(var enabled in batch%2==0?new[]{false,true}:new[]{true,false}){
            var p=Parameters() with{EnableTravelDirectionSteering=enabled};var index=enabled?1:0;long queries=0;var started=Stopwatch.GetTimestamp();
            for(var i=0;i<rows.Length;i++){var f=TwoContactBikeDynamics.Forces(views[i],spec,frame,surface,p,rows[i]["t"],147,
                new(rows[i]["omega_before"]*.33),new((float)rows[i]["steer_before"]),(float)rows[i]["dt"],guidance[i],inertia:52.72097f);
                checksum+=f.NetTorque;queries+=f.TravelSteering?.ContactQueries??f.SteeringControl!.ContactQueries;}
            var elapsed=Stopwatch.GetElapsedTime(started).TotalMilliseconds;if(batch>0)times[index].Add(elapsed);counts[index]=queries;
        }
        InverseFeasibilityAudit.Save(dir,"matched-cost.json",new{Worlds=0,Commits=0,States=rows.Length,WarmBatches=1,MeasuredBatches=3,
            OldMilliseconds=times[0],NewMilliseconds=times[1],OldMedianMilliseconds=times[0].Order().ElementAt(1),NewMedianMilliseconds=times[1].Order().ElementAt(1),
            OldQueries=counts[0],NewQueries=counts[1],Checksum=checksum,Rule="Identical decoded C accepted states/guidance, alternating old/new, no native world. Timing diagnostic, not CI gate."});
    }
    private static void Export(string dir,string name,TwoContactBikeAudit.Capture c,double elapsed)
    {
        TwoContactBikeAudit.Export(dir,name,c);
        using var writer=InverseFeasibilityAudit.Csv(dir,name+"-control.csv","t,psi,chi,beta,psi_dot,chi_dot,beta_dot,desired_chi_dot,predicted_beta_dot,min_chi_dot,max_chi_dot,unreachable,growing,no_recovery,rate_limited,queries,front_capacity,rear_capacity,wheel_power,crank_work,transmission_loss");
        foreach(var t in c.Ticks){var d=t.Forces.TravelSteering!;var m=TravelDirectionState.Observe(t.Before,t.Forces.NetForce,c.Mass);var f=t.Forces;
            InverseFeasibilityAudit.Row(writer,t.Time,m.Psi,m.Chi,m.Beta,m.PsiDot,m.ChiDot,m.BetaDot,d.DesiredChiDot,d.PredictedBetaDot,d.MinimumChiDot,d.MaximumChiDot,
                d.DirectionOutsideSampledResponse?1:0,d.GrowingSlide?1:0,d.NoSampledSlideRecovery?1:0,d.RateLimited?1:0,d.ContactQueries,f.TyreLoads!.FrontCapacityNewtons,f.TyreLoads.RearCapacityNewtons,
                t.Wheel.EngineWorkJoules/t.Seconds,f.Engine!.CrankWorkJoules(t.Wheel),f.Engine.TransmissionLossJoules(t.Wheel));}
        InverseFeasibilityAudit.Save(dir,name+"-work.json",new{ElapsedMilliseconds=elapsed,ContactQueries=c.Ticks.Sum(t=>t.Forces.TravelSteering!.ContactQueries),NativeSteps=c.Ticks.Length,c.Result.Work,
            Rule="Contact query count excludes the one applied evaluation and accepted curvature-boundary probes; no extra worlds or prefix replay."});
    }
}
