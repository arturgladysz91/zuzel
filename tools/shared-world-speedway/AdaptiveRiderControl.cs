using System.Numerics;
using CoreSim;

namespace SharedWorldSpeedway;

public sealed record AdaptiveRiderAction(string Name,float Drive,float OffsetMeters);
public sealed record AdaptiveRiderCandidate(AdaptiveRiderAction Action,double ProgressMeters,double MinimumInner,double MinimumOuter,
    double MaximumDemandRatio,double Score,bool PredictedSafe,int ContactQueries,int ForceEvaluations);
public sealed record AdaptiveRiderDecision(double TimeSeconds,AdaptiveRiderAction Chosen,bool Changed,bool Emergency,
    IReadOnlyList<AdaptiveRiderCandidate> Candidates);

/// <summary>PROVISIONAL single-rider receding horizon. Predictions never enter the native world.</summary>
public sealed class AdaptiveRiderControl
{
    public const double ReviewSeconds=.2,HorizonSeconds=.6;
    public const float PredictionStepSeconds=.02f;
    public const int SteeringReviewSteps=5;
    public const double InnerMarginMeters=.5,OuterMarginMeters=1,SwitchGainMeters=.15;
    public const float OffsetChoiceMeters=1.5f,PartialDrive=.55f;
    public const float MinimumGoalOffsetMeters=1,GoalOuterInsetMeters=2;
    // Score is progress-equivalent metres, not a force or velocity adjustment.
    public const double SaturationCostMeters=2,ClearanceCostSquareMeters=.1,ClearanceSofteningMeters=.1;
    public AdaptiveRiderAction? Action {get;private set;}
    public double NextReviewSeconds {get;private set;}
    private readonly RacingLineGeometry geometry;
    public AdaptiveRiderControl(TrackFrame frame)=>geometry=new(frame);

    public AdaptiveRiderDecision Decide(RiderView accepted,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,
        WorldParameters p,double time,float mass,float inertia,RearWheelState acceptedWheel,TwoContactBikeState acceptedSteering)
    {
        if(!p.EnableAdaptiveRiderControl||!p.EnablePhysicalSteeringControl||p.EnableDynamicAxleLoad)
            throw new ArgumentException("Adaptive rider requires physical steering and static axle loads.");
        // These are temporary controls, not an authored racing line. The rider can keep,
        // open or tighten the current radius at every review, including inside a bend.
        var offset=(float)accepted.Road.OffsetMeters;
        float Target(float value)=>Math.Clamp(value,MinimumGoalOffsetMeters,(float)frame.PhysicalWidthMeters(accepted.Road)-GoalOuterInsetMeters);
        var incumbent=Action??new("hold-full",1,Target(offset));
        var choices=new[]{incumbent,new("hold-full",1,Target(offset)),new("open-full",1,Target(offset+OffsetChoiceMeters)),
            new("tighten-partial",PartialDrive,Target(offset-OffsetChoiceMeters)),new("hold-partial",PartialDrive,Target(offset))}
            .DistinctBy(a=>(a.Drive,a.OffsetMeters)).ToArray();
        var candidates=choices.Select(a=>Predict(accepted,spec,frame,surface,p,time,mass,inertia,acceptedWheel,acceptedSteering,a)).ToArray();
        var safe=candidates.Where(c=>c.PredictedSafe).ToArray();
        // A failed prediction is never called safe. Best clearance is a best-effort
        // fallback only; the independent executed-footprint audit can still halt it.
        var emergency=safe.Length==0;
        var best=emergency?candidates.OrderByDescending(c=>Math.Min(c.MinimumInner-InnerMarginMeters,c.MinimumOuter-OuterMarginMeters)).First()
            :safe.OrderByDescending(c=>c.Score).First();
        if(!emergency&&candidates[0].PredictedSafe&&best.Score-candidates[0].Score<SwitchGainMeters)best=candidates[0];
        var changed=Action is null||Action.Drive!=best.Action.Drive||Action.OffsetMeters!=best.Action.OffsetMeters;
        Action=best.Action;NextReviewSeconds=time+ReviewSeconds;
        return new(time,Action,changed,emergency,candidates);
    }
    private AdaptiveRiderCandidate Predict(RiderView accepted,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,
        WorldParameters p,double time,float mass,float inertia,RearWheelState acceptedWheel,TwoContactBikeState acceptedSteering,AdaptiveRiderAction action)
    {
        var v=accepted with{Drive=action.Drive,TargetOffsetMeters=action.OffsetMeters};
        var wheel=new RearWheelState(acceptedWheel.AngularVelocityRadPerSecond*acceptedWheel.Parameters.WheelRadiusMeters,acceptedWheel.Parameters);
        var steering=new TwoContactBikeState(acceptedSteering.SteeringRadians);
        var gap=geometry.Clearance(v);var inner=gap.Inner;var outer=gap.Outer;double demandRatio=0;int queries=0,evaluations=0;float? heldRequest=null;
        var steps=(int)Math.Round(HorizonSeconds/PredictionStepSeconds);
        for(var i=0;i<steps;i++){
            var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,time+i*PredictionStepSeconds,mass,wheel,steering,PredictionStepSeconds,
                steeringOverride:i%SteeringReviewSteps==0?null:heldRequest,inertia:inertia);
            heldRequest=f.SteeringRequest;queries+=f.SteeringControl?.ContactQueries??0;evaluations++;
            demandRatio=Math.Max(demandRatio,Math.Abs(f.Demand)*mass/f.Budget);
            // Small semi-implicit rigid-body prediction, using ONLY original point
            // forces/r×F and the original scalar wheel solve. No native steps, RNG,
            // surface writes or real wheel/steering commits occur in this branch.
            var velocity=v.Velocity+f.NetForce*(PredictionStepSeconds/mass);
            var spin=v.AngularVelocity+f.NetTorque*(PredictionStepSeconds/inertia);
            var position=v.Position+velocity*PredictionStepSeconds;var road=frame.Project(position);
            v=v with{Position=position,Velocity=velocity,AngularVelocity=spin,Heading=v.Heading+spin*PredictionStepSeconds,Road=road,
                TotalReferenceMeters=v.TotalReferenceMeters+frame.UnwrapDelta(v.Road.ReferenceMeters,road.ReferenceMeters)};
            wheel.Commit();steering.Commit();gap=geometry.Clearance(v);inner=Math.Min(inner,gap.Inner);outer=Math.Min(outer,gap.Outer);
        }
        var progress=v.TotalReferenceMeters-accepted.TotalReferenceMeters;
        var safe=inner>=InnerMarginMeters&&outer>=OuterMarginMeters&&Vector2.Dot(v.Velocity,v.Road.Tangent)>0;
        // Reward actual predicted progress, not a short geometric line or Drive=1.
        // Saturated tight requests and vanishing clearance carry explicit costs.
        var score=progress-SaturationCostMeters*Math.Max(0,demandRatio-1)
            -ClearanceCostSquareMeters/(Math.Max(0,inner)+ClearanceSofteningMeters)-ClearanceCostSquareMeters/(Math.Max(0,outer)+ClearanceSofteningMeters);
        return new(action,progress,inner,outer,demandRatio,score,safe,queries,evaluations);
    }
}
