using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record RiderDecisionCosts(double Progress,double Direction,double Yaw,double Throttle,double SteeringChange,double GeometrySpeed,double FutureClearance,double UnsafePenalty)
{
    public double Total=>Progress+Direction+Yaw+Throttle+SteeringChange+GeometrySpeed+FutureClearance+UnsafePenalty;
}

/// <summary>One optional score change. Forecast, legality, actions and native forces are untouched.</summary>
public static class RiderDecisionObjective133
{
    public const double DirectionTransitionRadians=10*Math.PI/180;
    public const double TerminalReserveMeters=.2;
    public static double DirectionShape(double error,FreeActionSafety safety,double futureGap,double progress){
        if(safety!=FreeActionSafety.SafeInHorizon||futureGap<TerminalReserveMeters||progress<=0)return error*error*10;
        // Pseudo-Huber: identical local quadratic curvature, linear rather than
        // quadratic tails, nonzero direction gradient. Stable near zero error.
        var q=error/DirectionTransitionRadians;
        return 20*error*error/(Math.Sqrt(1+q*q)+1);
    }
    public static RiderDecisionCosts Costs(FreeRacingInput input,FreeLearningPolicy policy,FreeRacingAction action,FreeRacingPrediction prediction,
        TrackFrame frame,TrackStateSnapshot surface,float mass,double horizon,bool normalized){
        var end=prediction.Points[^1];var v=end.Rider;var clearance=prediction.Points.Min(q=>Math.Min(q.Inner,q.Outer));
        var closing=Vector2.Dot(v.Velocity,v.Road.Outward);var gap=Math.Min(end.Inner-Math.Max(0,-closing)*.24,end.Outer-Math.Max(0,closing)*.24);
        var safe=clearance<0?FreeActionSafety.Unsafe:clearance<.2||gap<.1?FreeActionSafety.Uncertain:FreeActionSafety.SafeInHorizon;
        if(prediction.Points.Any(q=>Math.Abs(q.Rider.SlideRadians)>Math.PI/4))safe=FreeActionSafety.Unsafe;
        var ahead=Math.Max(.1,frame.ReferenceSpeed(v.Road,v.Speed)*policy.PreviewSeconds);var road=frame.RoadAt(v.TotalReferenceMeters+ahead,v.Road.OffsetMeters);
        var error=BikeAngles.Wrap(road.Heading-Math.Atan2(v.Velocity.Y,v.Velocity.X));
        var desiredYaw=v.Speed*(road.IsTurn&&double.IsFinite(road.RadiusMeters)?1/road.RadiusMeters:0);
        var progress=v.TotalReferenceMeters-input.Rider.TotalReferenceMeters;var scored=normalized?progress*FreeLearningController.HorizonSeconds/horizon:progress;
        var lane=Math.Clamp((float)v.Road.OffsetMeters/LaneModel.UsableRacingWidthMeters(frame.Track.Segments[v.Road.Segment].Type,frame.Track.Geometry)*4,0,4);
        var capacity=LoadBasedTyreGrip.Budget(mass,surface.SampleSurface(v.Road.Segment,lane),TwoContactBikeParameters.Default,null).TotalCapacityNewtons/mass;
        var speedEnvelope=desiredYaw>1e-8?Math.Sqrt(.8*capacity/(desiredYaw/Math.Max(v.Speed,.5))):100;
        return new(-policy.ProgressWeight*scored,policy.DirectionWeight*error*error*10,policy.YawWeight*Math.Pow(v.AngularVelocity-desiredYaw,2)*10,
            policy.ThrottleWeight*Math.Pow(action.Drive-policy.Drive,2),policy.SteeringChangeWeight*Math.Pow((action.SteeringRadians-input.Active.SteeringRadians)*180/Math.PI,2),
            policy.GeometrySpeedWeight*Math.Pow(Math.Max(0,v.Speed-speedEnvelope),2),10*Math.Pow(Math.Max(0,.2-gap),2),safe==FreeActionSafety.Unsafe?10000*Math.Pow(Math.Max(.001,-clearance),2):0);
    }
    // B2 is an OFFLINE alternative only: a reaction-distance terminal credit.
    // It is a heuristic, not a proof of feasible continuation, and is never combined with B1.
    public static double OfflineTerminalCredit(FreeRacingPoint end,double futureGap,double reactionSeconds)=>
        Math.Max(0,Vector2.Dot(end.Rider.Velocity,end.Rider.Road.Tangent))*reactionSeconds*Math.Clamp(futureGap/TerminalReserveMeters,0,1);
}
