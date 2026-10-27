using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
namespace SharedWorldSpeedway;

public sealed record AccelerationLeaf141(FreeLearningCandidate Original,FreeRacingPrediction Forecast,double Utility,double MeanYaw,double ThrottlePrior,double TerminalYaw);
public sealed record Preparation141(FreeRacingAction Recovery,FreeActionSafety TailSafety,double ResidualSpeedMetersPerSecond,
    double CoastDecelerationMetersPerSecondSquared,double ReactionSeconds,double SteeringTravelSeconds,double ResponseSeconds,
    double CriticalDistanceMeters,double SpeedHintMetersPerSecond,double MaximumTailBetaRadians,int ContactQueries,
    string Classification="uncertified recovery forecast and geometry estimate; not a native certificate");
public sealed record AccelerationChoice141(FreeLearningDecision Decision,FreeLearningCandidate OriginalChoice,double Utility,
    Preparation141 Preparation,Preparation141 OriginalPreparation,object[] GasRoots,bool Changed,string Reason);

/// <summary>One bounded prototype. Frozen #16015 weights; no throttle prior,
/// mean rather than terminal yaw cost, and a relative next-corner preparation gate.
/// All point forces, prediction integration, queues and actuators are ORIGINAL.</summary>
public static class Controller141
{
    public const double HorizonSeconds=.9,ReactionSeconds=.24,ReviewSeconds=.2;
    public const double PreparationComparisonToleranceMetersPerSecond=.001;
    public static AccelerationLeaf141[] Evaluate(FreeRacingInput input,FreeLearningPolicy policy,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p){
        return FreeLearningController.Actions(input,policy,true).Select(action=>{
            var forecast=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,HorizonSeconds,action);
            var original=MultiActionPlanner135.Evaluate(input,policy,frame,surface,forecast,action);
            var prior=policy.ThrottleWeight*Math.Pow(action.Drive-policy.Drive,2);
            var terminal=CausalYawPlanner137.YawCost(forecast,policy,frame,false);
            var mean=CausalYawPlanner137.YawCost(forecast,policy,frame,true);
            return new AccelerationLeaf141(original,forecast,original.Score-prior-terminal+mean,mean,prior,terminal);
        }).ToArray();
    }
    public static FreeRacingInput AfterForecast(FreeRacingInput input,AccelerationLeaf141 leaf,RiderSpec spec){
        var end=leaf.Forecast.Points[^1];var pending=input.Pending.Append(new(input.Time,input.Time+RiderController.ReactionSeconds(spec),leaf.Original.Action))
            .Where(x=>x.ActivationAt>end.Time+1e-10).OrderBy(x=>x.ActivationAt).ToArray();
        return new(end.Rider,end.Time,end.Omega,end.Steering,leaf.Original.Action,pending,end.Time);
    }
    public static Preparation141 Preparation(FreeRacingInput input,AccelerationLeaf141 leaf,FreeLearningPolicy policy,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p){
        var future=AfterForecast(input,leaf,spec);var v=future.Rider;
        var ahead=Math.Max(.1,frame.ReferenceSpeed(v.Road,v.Speed)*policy.PreviewSeconds);
        var preview=frame.RoadAt(v.TotalReferenceMeters+ahead,v.Road.OffsetMeters);
        var chi=Math.Atan2(v.Velocity.Y,v.Velocity.X);var curvature=preview.IsTurn&&double.IsFinite(preview.RadiusMeters)?1/preview.RadiusMeters:0;
        var desired=v.Speed*curvature+BikeAngles.Wrap(preview.Heading-chi)/policy.PreviewSeconds;
        var parameters=p with{EnablePhysicalSteeringControl=false,EnableTravelDirectionSteering=false,EnableReachableTravelDirectionSteering=false};
        TwoContactForces Query(float angle)=>TwoContactBikeDynamics.Forces(v with{Drive=0},spec,frame,surface,parameters,future.Time,147,
            new RearWheelState(future.Omega*.33),new(angle),0,steeringOverride:angle,inertia:52.72097f);
        var inverse=PhysicalSteeringControl.Decide(v,(float)(v.Speed*desired),147,52.72097f,p.SlipYawResponseSeconds,
            TwoContactBikeParameters.Default,future.Steering,.01f,Query);
        var recovery=new FreeRacingAction(inverse.AngleRequestRadians,0);
        // This additional .9s coast probe retains the actual pending queue and
        // full .24s reaction. It is a possible recovery, never a promised suffix.
        var tail=FreeRacingModel.Predict(future,spec,frame,surface,p,147,52.72097f,HorizonSeconds,recovery);
        var assessment=MultiActionPlanner135.Evaluate(future,policy,frame,surface,tail,recovery);
        var at=tail.Points.First(x=>x.Time>=future.Time+ReactionSeconds-1e-8);var end=tail.Points[^1];
        var deceleration=Math.Max(0,(at.Rider.Speed-end.Rider.Speed)/(HorizonSeconds-ReactionSeconds));
        var endInput=new FreeRacingInput(end.Rider,end.Time,end.Omega,end.Steering,recovery,Array.Empty<FreeRacingCommand>(),end.Time);
        var knowledge=TrackAwareness134.Observe(endInput,frame,p);
        var lane=Math.Clamp((float)end.Rider.Road.OffsetMeters/LaneModel.UsableRacingWidthMeters(frame.Track.Segments[end.Rider.Road.Segment].Type,frame.Track.Geometry)*4,0,4);
        var capacity=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(end.Rider.Road.Segment,lane),TwoContactBikeParameters.Default).TotalCapacityNewtons/147;
        var travel=Math.Abs(recovery.SteeringRadians-future.Steering)/TwoContactBikeParameters.Default.SteeringRateRadiansPerSecond;
        var response=p.LateralResponseSeconds+p.SlipYawResponseSeconds;double residual=0,critical=0,hint=100;
        foreach(var point in knowledge.Profile.Where(x=>x.Curvature>1e-7&&x.ArrivalSecondsAtCurrentVelocity is not null)){
            // Only the offset actually attained in the physical coast forecast.
            // No hypothetical outward teleport or fixed distance-to-turn rule.
            var radius=1/point.Curvature+Math.Max(0,end.Rider.Road.OffsetMeters);
            var limit=Math.Sqrt(.8*capacity*radius);
            var available=Math.Max(0,point.ArrivalSecondsAtCurrentVelocity!.Value-ReactionSeconds-travel-response);
            var deficit=Math.Max(0,end.Rider.Speed-deceleration*available-limit);
            if(deficit>residual){residual=deficit;critical=point.MetricDistanceAtCurrentOffsetMeters;hint=limit;}
        }
        return new(recovery,assessment.Safety,residual,deceleration,ReactionSeconds,travel,response,critical,hint,
            tail.Points.Max(x=>Math.Abs(x.Rider.SlideRadians)),tail.ForceEvaluations+inverse.ContactQueries);
    }
    public static AccelerationChoice141 Choose(FreeRacingInput input,FreeLearningPolicy policy,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p){
        var leaves=Evaluate(input,policy,spec,frame,surface,p);
        IOrderedEnumerable<AccelerationLeaf141> Rank(IEnumerable<AccelerationLeaf141> x,bool original)=>x.OrderBy(x=>x.Original.Safety)
            .ThenBy(x=>original?x.Original.Score:x.Utility).ThenBy(x=>Math.Abs(x.Original.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(x=>x.Original.Action.SteeringRadians);
        var old=Rank(leaves,true).First();var roots=leaves.GroupBy(x=>x.Original.Action.Drive).Select(g=>Rank(g,false).First()).ToArray();
        var probes=new Dictionary<FreeRacingAction,Preparation141>();int forecasts=leaves.Length;long contacts=leaves.Sum(x=>(long)x.Forecast.ForceEvaluations);
        Preparation141 Probe(AccelerationLeaf141 leaf){if(!probes.TryGetValue(leaf.Original.Action,out var q)){q=Preparation(input,leaf,policy,spec,frame,surface,p);probes.Add(leaf.Original.Action,q);contacts+=q.ContactQueries;forecasts++;}return q;}
        var oldPreparation=Probe(old);var eligible=new List<AccelerationLeaf141>{old};var rows=new List<object>();
        foreach(var root in roots){var estimate=Probe(root);
            var allowed=root.Original.Safety==FreeActionSafety.SafeInHorizon&&estimate.TailSafety==FreeActionSafety.SafeInHorizon
                &&estimate.ResidualSpeedMetersPerSecond<=oldPreparation.ResidualSpeedMetersPerSecond+PreparationComparisonToleranceMetersPerSecond;
            if(allowed&&!eligible.Contains(root))eligible.Add(root);
            rows.Add(new{root.Original.Action,H09Safety=root.Original.Safety,root.Original.Score,root.Utility,root.MeanYaw,root.TerminalYaw,root.ThrottlePrior,Estimate=estimate,Eligible=allowed});
        }
        var chosen=eligible.OrderBy(x=>x.Original.Safety).ThenBy(x=>x.Utility+policy.GeometrySpeedWeight*Math.Pow(Probe(x).ResidualSpeedMetersPerSecond,2))
            .ThenBy(x=>Math.Abs(x.Original.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(x=>x.Original.Action.SteeringRadians).First();
        var changed=chosen.Original.Action!=old.Original.Action;
        var decision=new FreeLearningDecision(input.Time,chosen.Original.Action,chosen.Original.Safety,
            chosen.Original.Safety!=FreeActionSafety.SafeInHorizon,eligible.Select(x=>x.Original with{Score=x.Utility}).ToArray(),checked((int)contacts),forecasts);
        return new(decision,old.Original,chosen.Utility,Probe(chosen),oldPreparation,rows.ToArray(),changed,
            changed?"mean-yaw/no-throttle-prior candidate passes relative preparation and physical recovery probes":"frozen134A action retained; no admissible candidate has better utility");
    }
}
