using System.Numerics;
using CoreSim;
namespace SharedWorldSpeedway;

public sealed record Terms142(double Progress,double Direction,double Yaw,double Throttle,double Steering,double Geometry,double Clearance,double Unsafe,double TrackAwareness=0){
    public double Sum=>Progress+Direction+Yaw+Throttle+Steering+Geometry+Clearance+Unsafe+TrackAwareness;
    public double Removed(string mode)=>mode switch{"no-throttle"=>Throttle,"no-yaw"=>Yaw,"no-direction"=>Direction,"no-steering"=>Steering,"no-geometry"=>Geometry,"no-clearance"=>Clearance,"no-unsafe-score"=>Unsafe,"no-track-awareness"=>TrackAwareness,_=>0};
}
public sealed record Leaf142(FreeLearningCandidate Candidate,Terms142 Terms,string? HardViolation,double MaximumBetaDegrees,double MaximumYawRate,double? EndYawRate);
public sealed record Decision142(FreeLearningDecision Decision,Leaf142? Selected,Leaf142 Original,int HardFeasibleCount,bool Abstained,Leaf142[] Leaves);

/// <summary>Only objective/ranking changes. Original H09 predictions and bounded actions.
/// C's admissibility is nominal prediction feasibility, NOT a native safety certificate.</summary>
public static class Objective142
{
    public static readonly string[] Single={"no-throttle","no-yaw","no-direction","no-steering","no-geometry","no-clearance","no-unsafe-score","no-track-awareness"};
    public static Terms142 Components(FreeRacingInput input,FreeLearningPolicy policy,TrackFrame frame,TrackStateSnapshot surface,FreeRacingPrediction prediction,FreeLearningCandidate c){
        var v=prediction.Points[^1].Rider;var ahead=Math.Max(.1,frame.ReferenceSpeed(v.Road,v.Speed)*policy.PreviewSeconds);
        var road=frame.RoadAt(v.TotalReferenceMeters+ahead,v.Road.OffsetMeters);
        var desired=v.Speed*(road.IsTurn&&double.IsFinite(road.RadiusMeters)?1/road.RadiusMeters:0);
        var lane=Math.Clamp((float)v.Road.OffsetMeters/LaneModel.UsableRacingWidthMeters(frame.Track.Segments[v.Road.Segment].Type,frame.Track.Geometry)*4,0,4);
        var capacity=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(v.Road.Segment,lane),TwoContactBikeParameters.Default).TotalCapacityNewtons/147;
        var envelope=desired>1e-8?Math.Sqrt(.8*capacity/(desired/Math.Max(v.Speed,.5))):100;
        return new(-policy.ProgressWeight*c.Progress*.6/.9,policy.DirectionWeight*c.DirectionError*c.DirectionError*10,
            policy.YawWeight*Math.Pow(v.AngularVelocity-desired,2)*10,policy.ThrottleWeight*Math.Pow(c.Action.Drive-policy.Drive,2),
            policy.SteeringChangeWeight*Math.Pow((c.Action.SteeringRadians-input.Active.SteeringRadians)*180/Math.PI,2),
            policy.GeometrySpeedWeight*Math.Pow(Math.Max(0,v.Speed-envelope),2),10*Math.Pow(Math.Max(0,.2-c.FutureClearance),2),
            c.Safety==FreeActionSafety.Unsafe?10000*Math.Pow(Math.Max(.001,-c.Clearance),2):0);
    }
    public static string? HardViolation(FreeRacingPrediction prediction){
        foreach(var q in prediction.Points){var v=q.Rider;
            if(!double.IsFinite(q.Time)||!double.IsFinite(v.TotalReferenceMeters)||!double.IsFinite(v.DistanceMeters)||!float.IsFinite(v.SlideRadians)
                ||!float.IsFinite(v.Position.X)||!float.IsFinite(v.Position.Y)||!float.IsFinite(v.Velocity.X)||!float.IsFinite(v.Velocity.Y)
                ||!float.IsFinite(v.Heading)||!float.IsFinite(v.AngularVelocity)||!double.IsFinite(q.Omega)||q.Omega<0
                ||!double.IsFinite(q.Inner)||!double.IsFinite(q.Outer)||!float.IsFinite(q.Steering)
                ||Math.Abs(q.Steering)>TwoContactBikeParameters.Default.MaximumSteeringRadians+1e-6)return "invalid physical state";
            if(q.Inner<0||q.Outer<0)return "predicted footprint boundary crossing";
            if(Math.Abs(v.SlideRadians)>Math.PI/4)return "predicted experimental beta45 envelope";
            if(Vector2.Dot(v.Velocity,v.Road.Tangent)<=0)return "predicted reverse";
        }
        return null;
    }
    public static Leaf142 FromPrediction(FreeRacingInput input,FreeLearningPolicy policy,TrackFrame frame,TrackStateSnapshot surface,FreeRacingPrediction prediction,FreeRacingAction action){
        var c=MultiActionPlanner135.Evaluate(input,policy,frame,surface,prediction,action);var terms=Components(input,policy,frame,surface,prediction,c);
        if(Math.Abs(terms.Sum-c.Score)>1e-8)throw new InvalidOperationException("Independent component reconstruction differs from original score.");
        return new(c,terms,HardViolation(prediction),prediction.Points.Max(x=>Math.Abs(x.Rider.SlideRadians))*180/Math.PI,
            prediction.Points.Max(x=>Math.Abs(x.Rider.AngularVelocity)),prediction.Points[^1].Rider.AngularVelocity);
    }
    public static Leaf142? Select(IEnumerable<Leaf142> leaves,FreeRacingInput input,string mode){
        var eligible=mode=="C"?leaves.Where(x=>x.HardViolation is null):leaves;
        // No extra steering preference in Progress Only. Original tie order is
        // retained purely for deterministic equality of exactly equal progress.
        return eligible.OrderBy(x=>mode=="C"?0:(int)x.Candidate.Safety)
            .ThenBy(x=>mode is "B" or "C"?-x.Candidate.Progress:x.Candidate.Score-x.Terms.Removed(mode))
            .ThenBy(x=>Math.Abs(x.Candidate.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(x=>x.Candidate.Action.SteeringRadians).FirstOrDefault();
    }
    public static Decision142 Choose(FreeRacingInput input,string mode,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p){
        var leaves=new List<Leaf142>();int contacts=0;
        foreach(var action in FreeLearningController.Actions(input,PhysicsExperiment139.Policy,true)){
            var pred=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,.9,action);contacts+=pred.ForceEvaluations;
            leaves.Add(FromPrediction(input,PhysicsExperiment139.Policy,frame,surface,pred,action));
        }
        var rows=leaves.ToArray();var original=Select(rows,input,"A")!;var chosen=Select(rows,input,mode);
        // An empty C set abstains. The collector stops before this diagnostic
        // placeholder request can activate; it is never called a certified action.
        var actionRequest=chosen?.Candidate.Action??input.Active;
        var decision=new FreeLearningDecision(input.Time,actionRequest,chosen?.Candidate.Safety??FreeActionSafety.Unsafe,
            chosen is null||rows.All(x=>x.Candidate.Safety!=FreeActionSafety.SafeInHorizon),rows.Select(x=>x.Candidate).ToArray(),contacts,rows.Length);
        return new(decision,chosen,original,rows.Count(x=>x.HardViolation is null),chosen is null,rows);
    }
}
