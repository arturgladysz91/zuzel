using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>At most three short native trials; geometry/saved-state work precedes the frozen protocol.</summary>
public static class PreparedCornerAudit
{
    public const string BaseHead="46129d74cf725c33d67040663262062ac9067281";
    public static WorldParameters Parameters()=>CornerEntryAudit.Parameters() with{EnablePreparedCornerPlan=true};
    public static RiderView InitialView(){var f=RacingLineRootCauseAudit.Frame();var a=f.At(4,1);
        return new(1,a.Position,a.Road.Tangent*18,(float)a.Road.Heading,0,a.Road,4,0,RiderActivity.Racing,1,1,0);}
    public static PreparedCornerPlan InitialPlan(PreparedCornerOptions? options=null)=>new(InitialView(),CornerEntryAudit.Rider(),RacingLineRootCauseAudit.Frame(),Parameters(),0,147,new(18),new(),options);
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="geometry"){Geometry(dir);return;}
        if(stage=="freeze"){
            if(!File.Exists(Path.Combine(dir,"geometry.json")))throw new InvalidOperationException("Read-only geometry first.");
            InverseFeasibilityAudit.Save(dir,"protocol.json",new{BaseHead,Parameters=Parameters(),Start=CornerEntryAudit.Rider(),Options=new PreparedCornerOptions(),
                PreparedCornerPlan.ReviewSeconds,PreparedCornerPlan.ForceReserve,PreparedCornerPlan.WheelAllowanceSeconds,PreparedCornerPlan.ThrottleRisePerSecond,
                PreparedCornerPlan.SpeedFeedbackPerMeterPerSecond,PreparedCornerPlan.GeometryStepMeters,PreparedCornerPlan.FootprintReserveMeters,
                SourceHashes=SourceHashes(),MaximumNativeTrials=3,
                Rule="A path +0.15 gas; B ordinary5m +new gas; C both. Same #119 rolling start. Original0.24s reaction and #117, #120 OFF. No tuning after native results. Stop after first real exit+1.05m, any crossing/beta45/energy excess, or14s. No second bend/laps. Coast is a work estimate/bound, not a brake. Reserved/response profile is necessary, not a tracking certificate."});return;
        }
        if(stage is not ("A" or "B" or "C"))throw new ArgumentException("geometry, freeze, A, B, C");
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"protocol.json")));
        if(!frozen.RootElement.GetProperty("SourceHashes").Deserialize<SortedDictionary<string,string>>()!.SequenceEqual(SourceHashes()))throw new InvalidOperationException("Frozen execution sources changed.");
        if(stage=="B"&&!File.Exists(Path.Combine(dir,"A.json"))||stage=="C"&&!File.Exists(Path.Combine(dir,"B.json")))throw new InvalidOperationException("A, B, C in order.");
        // Persist before constructing a world. Failed attempts cannot silently be retried/overwritten.
        InverseFeasibilityAudit.Save(dir,stage+"-attempt.json",new{Stage=stage,Worlds=1,NoRetuning=true});
        var options=new PreparedCornerOptions(Path:stage!="B",Throttle:stage!="A");
        var reviews=new List<PreparedCornerReview>();var c=Execute(stage,options,reviews.Add);
        TwoContactBikeAudit.Export(dir,stage,c);CornerEntryAudit.Export(dir,stage,c);
        InverseFeasibilityAudit.Save(dir,stage+"-reviews.json",reviews);
        ExportPlan(dir,stage,c,InitialPlan(options));
    }
    public static SortedDictionary<string,string> SourceHashes(){var root=Path.GetDirectoryName(PhysicalSteeringAudit.PreviousPath("../feasible-path-speed-planning/unused"))!;
        var repo=Path.GetFullPath(Path.Combine(root,"../../.."));
        return new(new[]{"AuthoredRacingPath.cs","PreparedCornerPlan.cs","PreparedCornerAudit.cs","WorldRunner.cs","WorldModel.cs","TwoContactBikeDynamics.cs","RearWheelDynamics.cs","TravelDirectionSteering.cs","Engine500.cs","LoadBasedTyreGrip.cs"}
            .ToDictionary(name=>name,name=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo,"tools/shared-world-speedway",name))))),StringComparer.Ordinal);}
    private static void Geometry(string dir)
    {
        var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var plan=InitialPlan();var points=new List<object>();var boundary=new RacingLineGeometry(frame);
        using(var writer=InverseFeasibilityAudit.Csv(dir,"planned-geometry.csv","s,x,y,heading,offset,offset_first,offset_second,curvature,curvature_derivative,metric,inner,outer,edge_inner_x,edge_inner_y,edge_outer_x,edge_outer_y"))
        for(var s=4d;s<=plan.BendExitMeters+2;s+=.25){var q=plan.Path.At(s);var v=PreparedCornerPlan.Pose(InitialView(),frame,q);var gap=boundary.Clearance(v);
            var edge=frame.Shape!.At(s);var nx=Math.Sin(edge.Heading);var ny=-Math.Cos(edge.Heading);var width=frame.PhysicalWidthMeters(v.Road);
            InverseFeasibilityAudit.Row(writer,s,q.Position.X,q.Position.Y,q.Heading,q.OffsetMeters,q.OffsetFirst,q.OffsetSecond,q.Curvature,plan.Path.CurvatureDerivative(s),q.Metric,gap.Inner,gap.Outer,
                edge.Position.X-nx,edge.Position.Y-ny,edge.Position.X+nx*(width-1),edge.Position.Y+ny*(width-1));}
        var initial=plan.Observe(InitialView(),CornerEntryAudit.Rider(),frame,surface,Parameters(),0,147,52.72097f,new(18),new());
        using(var writer=InverseFeasibilityAudit.Csv(dir,"saved119-plan.csv","t,s,speed,offset,planned_offset,heading_error,path_error,planned_curvature,required_normal,budget,fx,fy,profile_speed,coast_residual"))
        foreach(var row in DynamicAxleLoadAudit.ReadSaved("../feasible-500cc-corner-entry/combined")){
            var v=PhysicalSteeringAudit.View(row,frame) with{TargetOffsetMeters=row["t"]<.24?1:5,Drive=(float)row["drive"]};
            var q=plan.Path.At(v.TotalReferenceMeters);var delta=new CoreSim.PhysicalSpace.MeterPoint(v.Position.X-q.Position.X,v.Position.Y-q.Position.Y);
            var f=TwoContactBikeDynamics.Forces(v,CornerEntryAudit.Rider(),frame,surface,Parameters(),row["t"],147,new(row["omega_before"]*.33),new((float)row["steer_before"]),0,steeringOverride:(float)row["steer_before"]);
            InverseFeasibilityAudit.Row(writer,row["t"],v.TotalReferenceMeters,v.Speed,v.Road.OffsetMeters,q.OffsetMeters,BikeAngles.Wrap(Math.Atan2(v.Velocity.Y,v.Velocity.X)-q.Heading),
                CoreSim.PhysicalSpace.MeterPoint.Dot(delta,q.Outward),q.Curvature,147*v.Speed*v.Speed*Math.Abs(q.Curvature),f.Budget,f.RearLongitudinal,f.RearLateral,
                initial.Profile.MinBy(p=>Math.Abs(p.S-v.TotalReferenceMeters))!.SpeedLimit,initial.Profile.Any(p=>p.ResidualOverspeed)?1:0);
        }
        InverseFeasibilityAudit.Save(dir,"geometry.json",new{BaseHead,Worlds=0,SavedStates=1017,plan.ActivationSeconds,plan.TransitionStartMeters,plan.TransitionEndMeters,plan.SelectedOffsetMeters,
            plan.BendExitMeters,InitialReview=initial,Rule="Two geometry-only intents5/8.5m; quintic zero first/second offset derivatives at both ends. Exact offset derivatives in XY curvature. Entire footprint at0,+/-45deg sampled every0.25m with0.5m reserve plus interpolation allowance. No inference of stable driving from these necessary bounds."});
    }
    public static TwoContactBikeAudit.Capture Execute(string name,PreparedCornerOptions options,Action<PreparedCornerReview>? observe=null)
    {
        var spec=CornerEntryAudit.Rider();var frame=RacingLineRootCauseAudit.Frame();var ticks=new List<TwoContactBikeAudit.Tick>();
        TwoContactForces forces=default;RearWheelStep wheel=default;RiderView before=default;string? stop=null;double previous=0;
        using var world=new WorldRunner(new[]{spec},parameters:Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),preparedOptions:options,
            frozenSurface:TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),twoContactInitial:new(),
            diagnostics:new(){Force=f=>before=f.Rider,TwoContact=o=>forces=o.Forces,RearWheel=o=>wheel=o.Step,PreparedCorner=observe});
        var initial=world.Snapshot;var mechanical=world.MechanicalProperties(spec.Profile.Id);var geometry=new RacingLineGeometry(frame);
        var end=frame.SegmentStartMeters(4)+1.05;
        var result=world.Run(name,.01,14,acceptedObserver:s=>{
            var after=s.Riders[0];var gap=geometry.Clearance(after);ticks.Add(new(previous,(float)wheel.Seconds,before,after,forces,wheel,gap.Inner,gap.Outer));previous=s.TimeSeconds;
        },stopOnCrash:true,diagnosticStop:s=>{
            var t=ticks[^1];var delta=TwoContactBikeAudit.BodyKinetic(t.After,mechanical.MassKg,mechanical.InertiaKgMetersSquared)-TwoContactBikeAudit.BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared)
                +.45*(t.Wheel.AfterOmega*t.Wheel.AfterOmega-t.Wheel.BeforeOmega*t.Wheel.BeforeOmega);
            if(delta-t.Wheel.EngineWorkJoules>.05+1e-6*Math.Max(TwoContactBikeAudit.BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared),TwoContactBikeAudit.BodyKinetic(t.After,mechanical.MassKg,mechanical.InertiaKgMetersSquared)))stop="energy excess";
            if(t.Inner<0||t.Outer<0)stop=t.Inner<0?"inner crossing":"outer crossing";
            if(Math.Abs(t.After.SlideRadians)>TravelDirectionSteering.ControlledSlideLimitRadians)stop="beta outside45deg";
            return stop is not null||t.After.TotalReferenceMeters>=end;
        });
        return new(initial,result,ticks.ToArray(),mechanical.MassKg,mechanical.InertiaKgMetersSquared,0,stop);
    }
    private static void ExportPlan(string dir,string name,TwoContactBikeAudit.Capture c,PreparedCornerPlan plan)
    {
        var path=plan.Options.Path?plan.Path:AuthoredRacingPath.Prepared(plan.Path.Frame,new(0,plan.Options.PreferredOffsetMeters),new(plan.BendExitMeters+1,plan.Options.PreferredOffsetMeters));
        using var writer=InverseFeasibilityAudit.Csv(dir,name+"-plan.csv","t,s,path_x,path_y,path_heading,path_curvature,guidance_curvature,path_offset,path_error,lateral_speed,actual_curvature,chi_dot,beta_dot,inner,outer");
        foreach(var t in c.Ticks){var v=t.Before;var q=path.At(v.TotalReferenceMeters);var m=TravelDirectionState.Observe(v,t.Forces.NetForce,c.Mass);
            var delta=new CoreSim.PhysicalSpace.MeterPoint(v.Position.X-q.Position.X,v.Position.Y-q.Position.Y);var vel=new CoreSim.PhysicalSpace.MeterPoint(v.Velocity.X,v.Velocity.Y);
            InverseFeasibilityAudit.Row(writer,t.Time,v.TotalReferenceMeters,q.Position.X,q.Position.Y,q.Heading,q.Curvature,plan.Guidance(v,Parameters(),t.Time)?.FeedForwardCurvature??0,q.OffsetMeters,
                CoreSim.PhysicalSpace.MeterPoint.Dot(delta,q.Outward),CoreSim.PhysicalSpace.MeterPoint.Dot(vel,q.Outward),m.ChiDot/Math.Max(.5,v.Speed),m.ChiDot,m.BetaDot,t.Inner,t.Outer);}
    }
}
