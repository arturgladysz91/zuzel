using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>One opt-in native trial after frozen saved-state gates, plus one exact OFF verification.</summary>
public static class PredictiveRiderAudit
{
    public static void Run(string dir,bool candidate)
    {
        var name=candidate?"candidate":"baseline";Directory.CreateDirectory(dir);
        var controlled=File.Exists(Path.Combine(dir,"controlled-comparison-protocol.json"));
        var evidence=controlled?Path.GetFullPath(Path.Combine(dir,"..")):dir;
        foreach(var file in new[]{"prediction-validation.json","decision-validation.json"}){
            using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence,file)));
            if(!gate.RootElement.GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Saved-state prediction/decision gate failed; no native trial.");
        }
        if(candidate){using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence,"baseline-verification.json")));
            if(!baseline.RootElement.GetProperty("ExactFingerprint").GetBoolean())throw new InvalidOperationException("OFF must match #123 first.");}
        // A persisted attempt cannot be silently overwritten, even after a failed trial.
        InverseFeasibilityAudit.Save(dir,name+"-attempt.json",new{Worlds=1,Candidate=candidate,RetuningAllowed=false});
        var p=ContinuousCornerAudit.Parameters() with{EnablePredictiveRiderControl=candidate};
        var frame=RacingLineRootCauseAudit.Frame();var boundary=new RacingLineGeometry(frame);
        PreparedCornerPlan? frozenNext=null;
        if(controlled){var saved=ContinuousCornerAudit.SavedEnd();var frozenSurface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
            frozenNext=new(saved.Rider,CornerEntryAudit.Rider(),frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,frozenSurface,new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));}
        var ticks=new List<TwoContactBikeAudit.Tick>();var plans=new List<AuthoredPathSample>();var derivatives=new List<double>();
        var reviews=new List<PreparedCornerReview>();var changes=new List<CornerPlanChange>();
        var before=default(RiderView);var forces=default(TwoContactForces);var wheel=default(RearWheelStep);var sample=default(AuthoredPathSample);
        double previous=0,derivative=0;string? stop=null;WorldRunner? active=null;
        using var world=new WorldRunner(new[]{CornerEntryAudit.Rider()},parameters:p,trackShape:RacingLineRootCauseAudit.Variant(),preparedOptions:new(),twoContactInitial:new(),diagnosticFrozenNextPlan:frozenNext,
            frozenSurface:TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),diagnostics:new(){Force=o=>{before=o.Rider;sample=active!.PreparedPath!.At(before.TotalReferenceMeters);derivative=active.PreparedPath.CurvatureDerivative(before.TotalReferenceMeters);},
                TwoContact=o=>forces=o.Forces,RearWheel=o=>wheel=o.Step,PreparedCorner=reviews.Add,CornerPlan=changes.Add});
        active=world;var initial=world.Snapshot;var mechanical=world.MechanicalProperties(1);
        var result=world.Run("continuous-two-bends",.01,40,stopOnCrash:true,acceptedObserver:s=>{
            var after=s.Riders[0];var gap=boundary.Clearance(after);ticks.Add(new(previous,(float)wheel.Seconds,before,after,forces,wheel,gap.Inner,gap.Outer));
            plans.Add(sample);derivatives.Add(derivative);previous=s.TimeSeconds;
        },diagnosticStop:s=>{
            var t=ticks[^1];var a=t.Before;var b=t.After;
            var envelope=(Math.Max(a.Speed,b.Speed)+1.05*Math.Max(Math.Abs(a.AngularVelocity),Math.Abs(b.AngularVelocity)))*t.Seconds/2+.012;
            var delta=TwoContactBikeAudit.BodyKinetic(b,mechanical.MassKg,mechanical.InertiaKgMetersSquared)-TwoContactBikeAudit.BodyKinetic(a,mechanical.MassKg,mechanical.InertiaKgMetersSquared)+.45*(wheel.AfterOmega*wheel.AfterOmega-wheel.BeforeOmega*wheel.BeforeOmega);
            if(delta-wheel.EngineWorkJoules>.05+1e-6*Math.Max(TwoContactBikeAudit.BodyKinetic(a,mechanical.MassKg,mechanical.InertiaKgMetersSquared),TwoContactBikeAudit.BodyKinetic(b,mechanical.MassKg,mechanical.InertiaKgMetersSquared)))stop="energy excess";
            if(t.Inner-envelope<=0||t.Outer-envelope<=0)stop=t.Inner-envelope<=0?"inner conservative clearance":"outer conservative clearance";
            if(Math.Abs(b.SlideRadians)>TravelDirectionSteering.ControlledSlideLimitRadians)stop="beta outside45deg";
            return stop is not null||ContinuousCornerPlanning.Cleared(b,frame,frame.SegmentStartMeters(8));
        });
        var capture=new TwoContactBikeAudit.Capture(initial,result,ticks.ToArray(),mechanical.MassKg,mechanical.InertiaKgMetersSquared,0,stop);
        TwoContactBikeAudit.Export(dir,name,capture);
        InverseFeasibilityAudit.Save(dir,name+"-reviews.json",reviews);InverseFeasibilityAudit.Save(dir,name+"-changes.json",changes);
        using(var writer=InverseFeasibilityAudit.Csv(dir,name+"-plan.csv","t,s,offset,target,path_x,path_y,path_heading,path_k,path_kprime,error,normal_error,lateral_speed,chi_dot,beta_dot,desired_chi_dot,min_chi_dot,max_chi_dot,unreachable,rate_limited,front_capacity,rear_capacity,required_path_normal,actual_normal,front_fy,rear_fy,rear_fx"))
        for(var i=0;i<ticks.Count;i++){var t=ticks[i];var v=t.Before;var q=plans[i];var f=t.Forces;var motion=TravelDirectionState.Observe(v,f.NetForce,147);
            var delta=new MeterPoint(v.Position.X-q.Position.X,v.Position.Y-q.Position.Y);var velocity=new MeterPoint(v.Velocity.X,v.Velocity.Y);
            InverseFeasibilityAudit.Row(writer,t.Time,v.TotalReferenceMeters,v.Road.OffsetMeters,v.TargetOffsetMeters,q.Position.X,q.Position.Y,q.Heading,q.Curvature,derivatives[i],v.Road.OffsetMeters-q.OffsetMeters,
                MeterPoint.Dot(delta,q.Outward),MeterPoint.Dot(velocity,q.Outward),motion.ChiDot,motion.BetaDot,f.TravelSteering?.DesiredChiDot??f.Demand/Math.Max(v.Speed,.5f),f.TravelSteering?.MinimumChiDot??double.NaN,
                f.TravelSteering?.MaximumChiDot??double.NaN,f.TravelSteering?.DirectionOutsideSampledResponse==true?1:0,f.TravelSteering?.RateLimited==true?1:0,f.TyreLoads!.FrontCapacityNewtons,f.TyreLoads.RearCapacityNewtons,
                147*v.Speed*v.Speed*Math.Abs(q.Curvature),motion.ChiDot*147*v.Speed,f.FrontLateral,f.RearLateral,f.RearLongitudinal);
        }
        if(candidate){
            InverseFeasibilityAudit.Save(dir,"candidate-steering-activations.json",world.PredictiveActivations);
            using var file=File.Create(Path.Combine(dir,"candidate-predictions.json.gz"));using var zip=new System.IO.Compression.GZipStream(file,System.IO.Compression.CompressionLevel.SmallestSize);
            JsonSerializer.Serialize(zip,world.PredictiveDecisions,InverseFeasibilityAudit.Json);
        }else{
            using var original=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../continuous-corner-planning/continuous.json")));
            var expected=original.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString();
            InverseFeasibilityAudit.Save(dir,"baseline-verification.json",new{ExactFingerprint=result.Fingerprint==expected,Expected=expected,Actual=result.Fingerprint});
            if(result.Fingerprint!=expected)throw new InvalidOperationException("OFF differs from original #123; candidate blocked.");
        }
        Console.WriteLine($"{name}: {result.TimeSeconds:F2}s {ticks.Count} native steps; stop={stop??"second full exit"}; fingerprint={result.Fingerprint}");
    }
}
