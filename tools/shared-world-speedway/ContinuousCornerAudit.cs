using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>#123: saved-state diagnosis, freeze, ONE continuous native capture. No parameter search.</summary>
public static class ContinuousCornerAudit
{
    public const string BaseHead="f9d4dd7d8d09a755830c716c3f34af9e7daecc58";
    public static WorldParameters Parameters()=>PreparedCornerAudit.Parameters() with{EnableContinuousCornerPlanning=true};
    public static (RiderView Rider,RearWheelState Wheel,TwoContactBikeState Steering,PreparedCornerPlan Old,DecisionEvent? Pending,double Time) SavedEnd()
    {
        using var file=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../feasible-path-speed-planning/C.json")));
        var result=file.RootElement.GetProperty("Result");var v=result.GetProperty("Riders")[0].Deserialize<RiderView>(InverseFeasibilityAudit.Json);
        var last=DynamicAxleLoadAudit.ReadSaved("../feasible-path-speed-planning/C")[^1];
        var reviews=JsonSerializer.Deserialize<PreparedCornerReview[]>(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../feasible-path-speed-planning/C-reviews.json")))!;
        var old=PreparedCornerAudit.InitialPlan();old.CarryControlMemory(new((float)reviews[^1].Drive,reviews[^1].Time+PreparedCornerPlan.ReviewSeconds));
        var pending=result.GetProperty("Decisions").Deserialize<DecisionEvent[]>(InverseFeasibilityAudit.Json)!.LastOrDefault(d=>d.ActualActivation is null);
        return(v,new(last["omega_after"]*.33),new((float)last["steer_after"]),old,pending,result.GetProperty("TimeSeconds").GetDouble());
    }
    public static SortedDictionary<string,string> Sources()
    {
        var repo=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(PhysicalSteeringAudit.PreviousPath("../feasible-path-speed-planning/unused"))!,"../../.."));
        var names=PreparedCornerAudit.SourceHashes().Keys.Concat(new[]{"ContinuousCornerPlanning.cs","ContinuousCornerAudit.cs","WorldDiagnostics.cs","Program.cs"});
        return new(names.ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(repo,"tools/shared-world-speedway",n)).Replace("\r\n","\n"))))),StringComparer.Ordinal);
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="diagnostic"){Diagnostic(dir);return;}
        if(stage=="freeze"){
            if(!File.Exists(Path.Combine(dir,"diagnostic.json")))throw new InvalidOperationException("Saved C geometry first; no world yet.");
            InverseFeasibilityAudit.Save(dir,"protocol.json",new{BaseHead,Parameters=Parameters(),Options=new PreparedCornerOptions(),Start=CornerEntryAudit.Rider(),Sources=Sources(),
                MaximumNewWorlds=1,StepSeconds=.01,MaximumSeconds=40,
                Rule="One continuous world from exact #122 C initial state. Replan only after accepted full footprint clears active bend exit, activate after original0.24s reaction, keep pending commands and latest gas/rate memory. Keep #122 parameters/#116/#117 unchanged, #120/#121/#114 OFF. Stop first nonpositive conservative gap, energy excess, beta45 or full second exit. No tuning or retries after failure."});return;
        }
        if(stage!="trial")throw new ArgumentException("diagnostic, freeze, trial");
        using var protocol=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"protocol.json")));
        if(!protocol.RootElement.GetProperty("Sources").Deserialize<SortedDictionary<string,string>>()!.SequenceEqual(Sources()))throw new InvalidOperationException("Frozen sources changed.");
        InverseFeasibilityAudit.Save(dir,"attempt.json",new{Worlds=1,Retries=0});
        Trial(dir);
    }
    private static void Diagnostic(string dir)
    {
        var s=SavedEnd();var frame=s.Old.Path.Frame;var p=Parameters();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var sequence=new ContinuousCornerPlanning(s.Old);var change=sequence.Advance(s.Rider,CornerEntryAudit.Rider(),frame,surface,p,s.Time,147,s.Wheel,s.Steering,s.Pending)
            ??throw new InvalidOperationException("Saved C does not clear first full footprint.");
        var next=sequence.Waiting!;var geometry=new RacingLineGeometry(frame);double minInner=double.PositiveInfinity,minOuter=minInner,maxNormal=0;
        using(var w=InverseFeasibilityAudit.Csv(dir,"diagnostic-geometry.csv","s,x,y,heading,offset,k,kprime,inner,outer,normal_at_saved_speed,inner_x,inner_y,outer_x,outer_y"))
        for(var x=s.Rider.TotalReferenceMeters;x<=next.BendExitMeters+2;x+=.25){var q=next.Path.At(x);var pose=PreparedCornerPlan.Pose(s.Rider,frame,q);
            var gaps=new[]{0d,-Math.PI/4,Math.PI/4}.Select(b=>geometry.Clearance(pose with{Heading=(float)(q.Heading+b)})).ToArray();
            var inner=gaps.Min(g=>g.Inner);var outer=gaps.Min(g=>g.Outer);minInner=Math.Min(inner,minInner);minOuter=Math.Min(outer,minOuter);
            var normal=147*s.Rider.Speed*s.Rider.Speed*Math.Abs(q.Curvature);maxNormal=Math.Max(normal,maxNormal);
            var edge=frame.Shape!.At(x);var width=frame.PhysicalWidthMeters(pose.Road);var n=new MeterPoint(Math.Sin(edge.Heading),-Math.Cos(edge.Heading));var inside=edge.Position-n;var outside=edge.Position+n*(width-1);
            InverseFeasibilityAudit.Row(w,x,q.Position.X,q.Position.Y,q.Heading,q.OffsetMeters,q.Curvature,next.Path.CurvatureDerivative(x),inner,outer,normal,inside.X,inside.Y,outside.X,outside.Y);
        }
        var splice=next.TransitionStartMeters;var old=s.Old.Path.At(splice);var fresh=next.Path.At(splice);
        var review=next.Observe(s.Rider,CornerEntryAudit.Rider(),frame,surface,p,s.Time,147,52.72097f,s.Wheel,s.Steering);
        InverseFeasibilityAudit.Save(dir,"diagnostic.json",new{BaseHead,Worlds=0,Commits=0,Change=change,MinimumInner=minInner,MinimumOuter=minOuter,MaximumRequiredAtSavedSpeed=maxNormal,
            Continuity=new{PositionJump=(old.Position-fresh.Position).Length,HeadingJump=BikeAngles.Wrap(old.Heading-fresh.Heading),CurvatureJump=old.Curvature-fresh.Curvature,
                TravelHeadingErrorAtObservation=BikeAngles.Wrap(Math.Atan2(s.Rider.Velocity.Y,s.Rider.Velocity.X)-next.Path.At(s.Rider.TotalReferenceMeters).Heading)},Review=review,
            Rule="Static geometry and frozen original contacts at accepted final C only. Same options and constants as #122; not evidence of dynamic feasibility. Old path is retained during reaction and before C2 splice; actual beta, yaw and lateral velocity persist."});
    }
    private static void Trial(string dir)
    {
        var frame=RacingLineRootCauseAudit.Frame();var geometry=new RacingLineGeometry(frame);var ticks=new List<TwoContactBikeAudit.Tick>();
        var plans=new List<AuthoredPathSample>();var derivatives=new List<double>();var changes=new List<CornerPlanChange>();var reviews=new List<PreparedCornerReview>();
        var forces=default(TwoContactForces);var wheel=default(RearWheelStep);var before=default(RiderView);var sample=default(AuthoredPathSample);
        string? stop=null,prefixHash=null;double previous=0,derivative=0;var firstExit=frame.SegmentStartMeters(4);var secondExit=frame.SegmentStartMeters(8);
        WorldRunner? activeWorld=null;
        using var world=new WorldRunner(new[]{CornerEntryAudit.Rider()},parameters:Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),preparedOptions:new(),twoContactInitial:new(),
            frozenSurface:TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),diagnostics:new(){Force=o=>{before=o.Rider;sample=activeWorld!.PreparedPath!.At(before.TotalReferenceMeters);derivative=activeWorld.PreparedPath.CurvatureDerivative(before.TotalReferenceMeters);},
                TwoContact=o=>forces=o.Forces,RearWheel=o=>wheel=o.Step,PreparedCorner=reviews.Add,CornerPlan=changes.Add});
        activeWorld=world;var initial=world.Snapshot;var mechanical=world.MechanicalProperties(1);
        using var old=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../feasible-path-speed-planning/C.json")));
        var oldTime=old.RootElement.GetProperty("Result").GetProperty("TimeSeconds").GetDouble();
        var result=world.Run("continuous-two-bends",.01,40,stopOnCrash:true,acceptedObserver:s=>{
            var after=s.Riders[0];var gap=geometry.Clearance(after);ticks.Add(new(previous,(float)wheel.Seconds,before,after,forces,wheel,gap.Inner,gap.Outer));plans.Add(sample);derivatives.Add(derivative);previous=s.TimeSeconds;
            if(Math.Abs(s.TimeSeconds-oldTime)<1e-8)prefixHash=world.CurrentFingerprint;
        },diagnosticStop:s=>{
            var t=ticks[^1];var a=t.Before;var b=t.After;var envelope=(Math.Max(a.Speed,b.Speed)+1.05*Math.Max(Math.Abs(a.AngularVelocity),Math.Abs(b.AngularVelocity)))*t.Seconds/2+.012;
            var delta=TwoContactBikeAudit.BodyKinetic(b,mechanical.MassKg,mechanical.InertiaKgMetersSquared)-TwoContactBikeAudit.BodyKinetic(a,mechanical.MassKg,mechanical.InertiaKgMetersSquared)+.45*(wheel.AfterOmega*wheel.AfterOmega-wheel.BeforeOmega*wheel.BeforeOmega);
            if(delta-wheel.EngineWorkJoules>.05+1e-6*Math.Max(TwoContactBikeAudit.BodyKinetic(a,mechanical.MassKg,mechanical.InertiaKgMetersSquared),TwoContactBikeAudit.BodyKinetic(b,mechanical.MassKg,mechanical.InertiaKgMetersSquared)))stop="energy excess";
            if(t.Inner-envelope<=0||t.Outer-envelope<=0)stop=t.Inner-envelope<=0?"inner conservative clearance":"outer conservative clearance";
            if(Math.Abs(b.SlideRadians)>TravelDirectionSteering.ControlledSlideLimitRadians)stop="beta outside45deg";
            return stop is not null||ContinuousCornerPlanning.Cleared(b,frame,secondExit);
        });
        var capture=new TwoContactBikeAudit.Capture(initial,result,ticks.ToArray(),mechanical.MassKg,mechanical.InertiaKgMetersSquared,0,stop);
        TwoContactBikeAudit.Export(dir,"continuous",capture);
        InverseFeasibilityAudit.Save(dir,"changes.json",changes);InverseFeasibilityAudit.Save(dir,"reviews.json",reviews);
        var expectedHash=old.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString();
        InverseFeasibilityAudit.Save(dir,"prefix.json",new{Time=oldTime,Expected=expectedHash,Actual=prefixHash,Equal=prefixHash==expectedHash});
        using(var w=InverseFeasibilityAudit.Csv(dir,"continuous-plan.csv","t,s,offset,target,path_x,path_y,path_heading,path_k,path_kprime,error,normal_error,lateral_speed,chi_dot,beta_dot,desired_chi_dot,min_chi_dot,max_chi_dot,unreachable,rate_limited,front_capacity,rear_capacity,required_path_normal,actual_normal,front_fy,rear_fy,rear_fx"))
        for(var i=0;i<ticks.Count;i++){var t=ticks[i];var q=plans[i];var f=t.Forces;var v=t.Before;var motion=TravelDirectionState.Observe(v,f.NetForce,147);var delta=new MeterPoint(v.Position.X-q.Position.X,v.Position.Y-q.Position.Y);var velocity=new MeterPoint(v.Velocity.X,v.Velocity.Y);
            InverseFeasibilityAudit.Row(w,t.Time,v.TotalReferenceMeters,v.Road.OffsetMeters,v.TargetOffsetMeters,q.Position.X,q.Position.Y,q.Heading,q.Curvature,derivatives[i],v.Road.OffsetMeters-q.OffsetMeters,
                MeterPoint.Dot(delta,q.Outward),MeterPoint.Dot(velocity,q.Outward),motion.ChiDot,motion.BetaDot,f.TravelSteering!.DesiredChiDot,f.TravelSteering.MinimumChiDot,f.TravelSteering.MaximumChiDot,
                f.TravelSteering.DirectionOutsideSampledResponse?1:0,f.TravelSteering.RateLimited?1:0,f.TyreLoads!.FrontCapacityNewtons,f.TyreLoads.RearCapacityNewtons,
                147*v.Speed*v.Speed*Math.Abs(q.Curvature),motion.ChiDot*147*v.Speed,f.FrontLateral,f.RearLateral,f.RearLongitudinal);
        }
        var last=ticks[^1];var endMotion=TravelDirectionState.Observe(last.After,last.Forces.NetForce,147);var summary=JsonSerializer.SerializeToElement(TwoContactBikeAudit.Summary(capture),InverseFeasibilityAudit.Json);
        var safeBeta=Math.Abs(endMotion.Beta)<Math.PI/60||endMotion.Beta*endMotion.BetaDot<=0;
        InverseFeasibilityAudit.Save(dir,"gate.json",new{FirstCompleted=ContinuousCornerPlanning.Cleared(last.After,frame,firstExit),SecondCompleted=ContinuousCornerPlanning.Cleared(last.After,frame,secondExit),
            FinalMotion=endMotion,SlideRecovering=safeBeta,ConservativeInner=summary.GetProperty("ConservativeInner").GetDouble(),ConservativeOuter=summary.GetProperty("ConservativeOuter").GetDouble(),
            Pass=stop is null&&ContinuousCornerPlanning.Cleared(last.After,frame,secondExit)&&safeBeta&&prefixHash==expectedHash&&summary.GetProperty("Energy").GetProperty("MaximumExcessBeyondEngineJoules").GetDouble()==0});
    }
}
