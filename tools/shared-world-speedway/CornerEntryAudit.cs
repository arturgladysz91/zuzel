using System.Numerics;
using System.Text.Json;
using System.Globalization;
using System.IO.Compression;
using CoreSim;

namespace SharedWorldSpeedway;

/// <summary>#119 bounded preparation experiment; no force or steering-law changes.</summary>
public static class CornerEntryAudit
{
    public const string BaseHead="895cffe3df5a2486d6876c0798c494fc061f9308";
    public const float PartialDrive=.15f,WideOffset=5;
    public const double StartReference=4;
    public static WorldParameters Parameters()=>TravelDirectionAudit.Parameters() with{EnableLocalOffsetLimit=false};
    public static RiderSpec Rider()=>RacingLineRootCauseAudit.Rider(AuthoredRacingPath.Crossover(false)) with{
        ReferenceMeters=StartReference,OffsetMeters=1,TargetOffsetMeters=1,SpeedMetersPerSecond=18,TrafficEnabled=false};
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="freeze"){
            InverseFeasibilityAudit.Save(dir,"protocol.json",new{BaseHead,Parameters=Parameters(),Start=Rider(),PartialDrive,WideOffset,
                ReactionSeconds=RiderController.ReactionSeconds(Rider()),MaximumManualVariants=3,
                Rule="Same Egg Mild progressive-b and 18m/s rolling initial conditions, extend approach from s32 to s4. Ordinary TargetOffsetMeters controls, no authored C3 guidance. Commands activate after original rider reaction. Partial0.15 from #118 low-slip operating point; wide5m is an existing middle-line target. Combined only after the first two. Stop first bend, boundary, beta45 or energy violation; no laps or parameter tuning. AI gated on a successful explicit trial."});return;
        }
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"protocol.json")));
        if(frozen.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(Parameters(),InverseFeasibilityAudit.Json))throw new InvalidOperationException("Frozen parameters changed.");
        if(stage=="approach"){Approach(dir);return;}
        if(stage=="post"){Post(dir);return;}
        if(!File.Exists(Path.Combine(dir,"approach.json")))throw new InvalidOperationException("Saved-state diagnosis first.");
        if(stage is "ai" or "freeze-ai"){
            using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"combined-gate.json")));
            if(!gate.RootElement.GetProperty("Pass").GetBoolean())throw new InvalidOperationException("Explicit feasibility gate closed; no AI.");
            if(stage=="freeze-ai"){
                InverseFeasibilityAudit.Save(dir,"ai-protocol.json",new{FeasibleCornerEntry.ReviewSeconds,FeasibleCornerEntry.GeometryIntervals,
                    FeasibleCornerEntry.WheelSettlingAllowanceSeconds,FeasibleCornerEntry.PreparationDrive,FeasibleCornerEntry.ReleaseCurvaturePerMeter,
                    FeasibleCornerEntry.ReleaseBetaRadians,FeasibleCornerEntry.ReleaseWheelspin,
                    Rule="One policy, no tuning by replay time. Current-state V and actual wheel Fx bound plus geometry lookahead; middle line from physical usable width. Latched original reaction deadline, no AI rollout. Hold reduced gas until curvature and accepted beta/kappa clear; then restore full gas with same reaction. Stop first exit+6m only to observe activation; no second bend."});return;
            }
            if(!File.Exists(Path.Combine(dir,"ai-protocol.json")))throw new InvalidOperationException("Freeze AI hypothesis before native trial.");
            var observations=new List<CornerEntryDecision>();var ai=Execute("ai",1,1,true,observations.Add);
            TwoContactBikeAudit.Export(dir,"ai",ai);Export(dir,"ai",ai);InverseFeasibilityAudit.Save(dir,"ai-decisions.json",observations);return;
        }
        if(stage=="combined"&&(!File.Exists(Path.Combine(dir,"partial.json"))||!File.Exists(Path.Combine(dir,"wide.json"))))throw new InvalidOperationException("Both isolated actions first.");
        var offset=stage is "wide" or "combined"?WideOffset:1;var drive=stage is "partial" or "combined"?PartialDrive:1;
        if(stage is not ("partial" or "wide" or "combined"))throw new ArgumentException("freeze, approach, partial, wide, combined");
        var c=Execute(stage,offset,drive);TwoContactBikeAudit.Export(dir,stage,c);Export(dir,stage,c);
    }
    public static TwoContactBikeAudit.Capture Execute(string name,float offset,float drive,bool ai=false,Action<CornerEntryDecision>? observe=null,bool reachableSteering=false)
    {
        var spec=Rider();var frame=RacingLineRootCauseAudit.Frame();var p=Parameters() with{EnableFeasibleCornerEntry=ai,EnableReachableTravelDirectionSteering=reachableSteering};
        var ticks=new List<TwoContactBikeAudit.Tick>();TwoContactForces forces=default;RearWheelStep wheel=default;RiderView before=default;
        var reaction=RiderController.ReactionSeconds(spec);string? stop=null;double previous=0;
        using var world=new WorldRunner(new[]{spec},parameters:p,trackShape:RacingLineRootCauseAudit.Variant(),
            controls:ai?null:new[]{new ControlCommand(spec.Profile.Id,reaction,offset,drive)},
            frozenSurface:TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),twoContactInitial:new(),
            diagnostics:new(){Force=f=>before=f.Rider,TwoContact=o=>forces=o.Forces,RearWheel=o=>wheel=o.Step,CornerEntry=observe});
        var initial=world.Snapshot;var mechanical=world.MechanicalProperties(spec.Profile.Id);var geometry=new RacingLineGeometry(world.Frame);
        var end=frame.SegmentStartMeters(4)+(ai?6:1.05); // Short exit tail to observe delayed gas restoration; no second bend.
        var result=world.Run(name,.01,14,acceptedObserver:s=>{
            var after=s.Riders[0];var gap=geometry.Clearance(after);
            ticks.Add(new(previous,(float)wheel.Seconds,before,after,forces,wheel,gap.Inner,gap.Outer));previous=s.TimeSeconds;
        },stopOnCrash:true,diagnosticStop:s=>{
            var t=ticks[^1];var delta=TwoContactBikeAudit.BodyKinetic(t.After,mechanical.MassKg,mechanical.InertiaKgMetersSquared)
                -TwoContactBikeAudit.BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared)
                +.45*(t.Wheel.AfterOmega*t.Wheel.AfterOmega-t.Wheel.BeforeOmega*t.Wheel.BeforeOmega);
            if(delta-t.Wheel.EngineWorkJoules>.05+1e-6*Math.Max(TwoContactBikeAudit.BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared),TwoContactBikeAudit.BodyKinetic(t.After,mechanical.MassKg,mechanical.InertiaKgMetersSquared)))stop="energy excess";
            if(t.Inner<0||t.Outer<0)stop=t.Inner<0?"inner crossing":"outer crossing";
            if(Math.Abs(t.After.SlideRadians)>TravelDirectionSteering.ControlledSlideLimitRadians)stop="beta outside original 45 degree operating envelope";
            return stop is not null||t.After.TotalReferenceMeters>=end;
        });
        return new(initial,result,ticks.ToArray(),mechanical.MassKg,mechanical.InertiaKgMetersSquared,0,stop);
    }
    public static void Export(string dir,string name,TwoContactBikeAudit.Capture c)
    {
        var frame=RacingLineRootCauseAudit.Frame();var end=frame.SegmentStartMeters(4)+1.05;
        using var writer=InverseFeasibilityAudit.Csv(dir,name+"-control.csv","t,s,target,chi_dot,beta_dot,desired_chi_dot,min_chi_dot,max_chi_dot,unreachable,rate_limited,front_capacity,rear_capacity,path_curvature,required_normal,actual_normal,wheel_power,alpha_rear,queries");
        foreach(var t in c.Ticks){var f=t.Forces;var m=TravelDirectionState.Observe(t.Before,f.NetForce,c.Mass);var d=f.TravelSteering!;
            var road=frame.Shape!.At(t.Before.TotalReferenceMeters);var k=road.Curvature/(1+road.Curvature*t.Before.TargetOffsetMeters);
            var ur=Vector2.Dot(f.RearVelocity,f.BodyForward);var wr=Vector2.Dot(f.RearVelocity,f.BodyLeft);
            InverseFeasibilityAudit.Row(writer,t.Time,t.Before.TotalReferenceMeters,t.Before.TargetOffsetMeters,m.ChiDot,m.BetaDot,d.DesiredChiDot,d.MinimumChiDot,d.MaximumChiDot,
                d.DirectionOutsideSampledResponse?1:0,d.RateLimited?1:0,f.TyreLoads!.FrontCapacityNewtons,f.TyreLoads.RearCapacityNewtons,k,
                c.Mass*t.Before.Speed*t.Before.Speed*k,m.ChiDot*c.Mass*t.Before.Speed,t.Wheel.EngineWorkJoules/t.Seconds,Math.Atan2(wr,ur),d.ContactQueries);
        }
        var last=c.Ticks[^1];var final=TravelDirectionState.Observe(last.After,last.Forces.NetForce,c.Mass);
        var summary=JsonSerializer.SerializeToElement(TwoContactBikeAudit.Summary(c),InverseFeasibilityAudit.Json);
        var conservativeInner=summary.GetProperty("ConservativeInner").GetDouble();var conservativeOuter=summary.GetProperty("ConservativeOuter").GetDouble();
        var energy=summary.GetProperty("Energy");
        var completed=last.After.TotalReferenceMeters>=end;
        var slideBounded=Math.Abs(last.After.SlideRadians)<=TravelDirectionSteering.ControlledSlideLimitRadians
            &&(Math.Abs(last.After.SlideRadians)<3*Math.PI/180||final.Beta*final.BetaDot<=0);
        InverseFeasibilityAudit.Save(dir,name+"-gate.json",new{CompletedFirstBend=completed,ConservativeInner=conservativeInner,ConservativeOuter=conservativeOuter,
            FinalBetaDegrees=final.Beta*180/Math.PI,FinalBetaDot=final.BetaDot,SlideBounded=slideBounded,Energy=energy,
            Pass=completed&&c.StopReason is null&&conservativeInner>0&&conservativeOuter>0&&slideBounded&&energy.GetProperty("MaximumExcessBeyondEngineJoules").GetDouble()==0,
            Rule="Whole footprint through first exit+1.05m, conservative inter-step clearance, original energy gate and beta45 envelope, final small or recovering beta. No second bend."});
    }
    private static void Approach(string dir)
    {
        var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var spec=Rider();var p=TravelDirectionAudit.Parameters();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var points=new List<object>();double replay=0;
        foreach(var row in DynamicAxleLoadAudit.ReadSaved("../controlled-slip-travel-direction/egg").Where(r=>r["t"]<=.6)){
            var v=PhysicalSteeringAudit.View(row,frame);var w=new RearWheelState(row["omega_before"]*.33);var steering=new TwoContactBikeState((float)row["steer_before"]);
            var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,row["t"],147,w,steering,(float)row["dt"],path.Guidance(v,p,true),inertia:52.72097f);
            replay=Math.Max(replay,new[]{Math.Abs(f.FrontForce.X-row["front_fx"]),Math.Abs(f.FrontForce.Y-row["front_fy"]),Math.Abs(f.RearForce.X-row["rear_fx"]),Math.Abs(f.RearForce.Y-row["rear_fy"])}.Max());
            var d=f.TravelSteering!;var capacity=d.MaximumChiDot*147*v.Speed;var motion=TravelDirectionState.Observe(v,f.NetForce,147);
            var reachable=new[]{-1f,0,1}.Select(sign=>{
                var angle=(float)row["steer_before"]+sign*TwoContactBikeParameters.Default.SteeringRateRadiansPerSecond*(float)row["dt"];
                return TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,row["t"],147,new(row["omega_before"]*.33),new((float)row["steer_before"]),(float)row["dt"],path.Guidance(v,p,true),angle);
            }).Max(force=>TravelDirectionState.Observe(v,force.NetForce,147).ChiDot*147*v.Speed);
            var k=path.At(v.TotalReferenceMeters).Curvature;var required=147*v.Speed*v.Speed*k;
            points.Add(new{Time=row["t"],S=v.TotalReferenceMeters,Speed=v.Speed,Curvature=k,RequiredNormal=required,Budget=f.Budget,
                RearFx=f.RearLongitudinal,RearFy=f.RearLateral,RearCircleFy=Math.Sqrt(Math.Max(0,f.TyreLoads!.RearCapacityNewtons*f.TyreLoads.RearCapacityNewtons-f.RearLongitudinal*f.RearLongitudinal)),
                ActualNormal=motion.ChiDot*147*v.Speed,AllSteeringSampledNormal=capacity,NextIntervalSampledNormal=reachable,
                StaticHeadroom=capacity-required,ImmediateHeadroom=reachable-required,BetaDegrees=motion.Beta*180/Math.PI,Wheelspin=row["wheelspin"],
                d.RateLimited,d.PredictedBetaDot,DesiredNormal=f.Demand*147});
        }
        if(replay!=0)throw new InvalidOperationException("Saved #117 force replay changed.");
        var geometry=new List<object>();
        foreach(var offset in new[]{1d,5d})for(var s=4d;s<=frame.SegmentStartMeters(4);s+=1){var q=frame.Shape!.At(s);var k=q.Curvature/(1+offset*q.Curvature);
            geometry.Add(new{S=s,Offset=offset,Curvature=k,AbsoluteSpeedBound=k>0?Math.Sqrt(1852.3389892578125/(147*k)):(double?)null});}
        InverseFeasibilityAudit.Save(dir,"approach.json",new{BaseHead,Worlds=0,MaximumReplayError=replay,Points=points,Geometry=geometry,
            ReactionSeconds=RiderController.ReactionSeconds(spec),SteeringRateDegreesPerSecond=90,MaximumSteeringDegrees=35,YawResponseSeconds=p.SlipYawResponseSeconds,
            Rule="All-steering sampled capacity holds wheel/body fixed and ignores steering travel; next-interval samples obey original rate. Neither is a future recovery certificate. Historical capture starts s32, only3m before entry35; no defensible latest-action time from these samples. New trials extend real approach to s4 with same initial speed and rolling wheel."});
    }
    private static Dictionary<string,double>[] ReadLocal(string dir,string name)
    {
        var file=Path.Combine(dir,name+".csv");using var stream=File.OpenRead(File.Exists(file)?file:file+".gz");
        using var reader=new StreamReader(File.Exists(file)?stream:new GZipStream(stream,CompressionMode.Decompress));
        var keys=reader.ReadLine()!.Split(',');var rows=new List<Dictionary<string,double>>();
        while(reader.ReadLine() is { } line)rows.Add(line.Split(',').Select((v,i)=>(keys[i],double.Parse(v,CultureInfo.InvariantCulture))).ToDictionary(x=>x.Item1,x=>x.Item2));
        return rows.ToArray();
    }
    private static void Post(string dir)
    {
        var frame=RacingLineRootCauseAudit.Frame();
        foreach(var name in new[]{"partial","wide","combined","ai"}){
            using var writer=InverseFeasibilityAudit.Csv(dir,name+"-geometry.csv","t,offset,local_lane_curvature");
            foreach(var row in ReadLocal(dir,name)){
                var v=PhysicalSteeringAudit.View(row,frame);var k=frame.Shape!.At(v.TotalReferenceMeters).Curvature;
                InverseFeasibilityAudit.Row(writer,row["t"],v.Road.OffsetMeters,k/(1+k*v.Road.OffsetMeters));
            }
        }
        var oldPath=Path.Combine(dir,"ai-decisions.json");var old=JsonSerializer.Deserialize<CornerEntryDecision[]>(File.ReadAllText(oldPath))!;
        var rows=ReadLocal(dir,"ai");var controlRows=ReadLocal(dir,"ai-control");var policy=new FeasibleCornerEntry();var decisions=new List<CornerEntryDecision>();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var p=Parameters() with{EnableFeasibleCornerEntry=true};
        foreach(var previous in old){
            var index=Enumerable.Range(0,rows.Length).MinBy(i=>Math.Abs(rows[i]["t"]-previous.TimeSeconds));var row=rows[index];
            if(Math.Abs(row["t"]-previous.TimeSeconds)>1e-8)throw new InvalidOperationException("Missing accepted AI review.");
            var v=PhysicalSteeringAudit.View(row,frame) with{TargetOffsetMeters=(float)controlRows[index]["target"]};
            var d=policy.Observe(v,Rider(),frame,surface,p,previous.TimeSeconds,147,new(row["omega_before"]*.33),new((float)row["steer_before"]));
            if(d.Drive!=previous.Drive||d.TargetOffsetMeters!=previous.TargetOffsetMeters||d.Preparing!=previous.Preparing)throw new InvalidOperationException("AI choices changed.");
            decisions.Add(d);
        }
        // Retain the initial diagnostic body-lateral proxy. The final bound includes
        // projection into the velocity normal; all physical actions remain identical.
        File.Move(oldPath,Path.Combine(dir,"ai-decisions-body-proxy.json"));
        InverseFeasibilityAudit.Save(dir,"ai-decisions.json",decisions);
        InverseFeasibilityAudit.Save(dir,"ai-replay.json",new{Worlds=0,Commits=0,Reviews=decisions.Count,ChangedActions=0,
            Rule="Pure replay of accepted review states after correcting only capacity diagnostics to the velocity-normal bound. Native fingerprint verified unchanged by targeted tests. Initial diagnostic values retained separately."});
    }
}
