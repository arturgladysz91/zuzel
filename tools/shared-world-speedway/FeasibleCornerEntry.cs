using System.Numerics;
using CoreSim;

namespace SharedWorldSpeedway;

public sealed record CornerEntryDecision(double TimeSeconds,double NextReviewSeconds,bool Preparing,float TargetOffsetMeters,float Drive,
    double LookaheadMeters,double MaximumCurvature,double RequiredNormal,double CapacityCeiling,double RearFx,double RearFy,
    double Wheelspin,double BetaRadians,string Reason);

/// <summary>Conservative experimental entry preparation. One frozen contact query, no rollout or path fitting.</summary>
public sealed class FeasibleCornerEntry
{
    public const double ReviewSeconds=.1,WheelSettlingAllowanceSeconds=.55,ReleaseCurvaturePerMeter=.001;
    public const double ReleaseBetaRadians=3*Math.PI/180,ReleaseWheelspin=.1;
    public const int GeometryIntervals=32;
    public const float PreparationDrive=.15f;
    public bool Preparing {get;private set;}
    public double NextReviewSeconds {get;private set;}

    public CornerEntryDecision Observe(RiderView v,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,
        WorldParameters p,double time,float mass,RearWheelState wheel,TwoContactBikeState steering)
    {
        if(!p.EnableFeasibleCornerEntry||!p.EnableTravelDirectionSteering||!p.Enable500CcEngine||!p.EnableLoadBasedTyreGrip||p.EnableAdaptiveRiderControl||p.EnableDynamicAxleLoad)
            throw new ArgumentException("Entry preparation requires the original travel-direction steering and static contacts.");
        var bike=p.TwoContactBike??TwoContactBikeParameters.Default;
        // Reaction + existing lateral response + worst one-sided steering travel +
        // three local wheel time constants from #118, rounded upward. This is a
        // conservative lookahead hypothesis, NOT a universal braking guarantee.
        var horizon=RiderController.ReactionSeconds(spec)+p.LateralResponseSeconds+4*p.SlipYawResponseSeconds
            +bike.MaximumSteeringRadians/bike.SteeringRateRadiansPerSecond+WheelSettlingAllowanceSeconds;
        var lookahead=Math.Max(1,v.Speed*horizon);
        double MaximumCurvature(float offset){double maximum=0;
            for(var i=0;i<=GeometryIntervals;i++){
                var road=frame.RoadAt(v.TotalReferenceMeters+lookahead*i/GeometryIntervals,offset);
                var curvature=road.IsTurn&&double.IsFinite(road.RadiusMeters)?1/road.RadiusMeters:0;
                maximum=Math.Max(maximum,Math.Abs(curvature));
            }return maximum;}
        var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,time,mass,
            new(wheel.AngularVelocityRadPerSecond*wheel.Parameters.WheelRadiusMeters,wheel.Parameters),new(steering.SteeringRadians),0,steeringOverride:steering.SteeringRadians);
        var rear=f.TyreLoads!.RearCapacityNewtons;
        // Upper bound only: front capacity plus rear circle remainder at ACTUAL
        // wheelspin. It is never applied as a force or labelled guaranteed grip.
        var beta=v.SlideRadians;
        var ceiling=f.TyreLoads.FrontCapacityNewtons+Math.Sqrt(Math.Max(0,rear*rear-f.RearLongitudinal*f.RearLongitudinal))*Math.Abs(Math.Cos(beta))
            +Math.Max(0,f.RearLongitudinal*Math.Sin(beta));
        var curvature=MaximumCurvature(v.TargetOffsetMeters);var required=mass*v.Speed*v.Speed*curvature;
        var target=v.TargetOffsetMeters;var drive=v.Drive;var reason="hold";
        if(!Preparing&&required>SlipCoupledDynamics.MaximumSteeringDemandRatio*ceiling){
            Preparing=true;drive=PreparationDrive;
            var middle=Math.Min(frame.MinimumUsableWidthMeters/2,frame.MaximumSafeTargetOffsetMeters(v.TotalReferenceMeters));
            if(middle>target&&MaximumCurvature(middle)<curvature)target=middle;
            reason="upcoming force demand exceeds reserved contact ceiling";
        }else if(Preparing){
            drive=PreparationDrive;
            var ur=Vector2.Dot(f.RearVelocity,f.BodyForward);
            var kappa=RearWheelDynamics.SlipRatio(ur,wheel.AngularVelocityRadPerSecond,wheel.Parameters);
            if(curvature<=ReleaseCurvaturePerMeter&&Math.Abs(v.SlideRadians)<=ReleaseBetaRadians&&Math.Abs(kappa)<=ReleaseWheelspin){
                Preparing=false;drive=1;reason="curvature and accepted slip have cleared";
            }
        }
        var rearU=Vector2.Dot(f.RearVelocity,f.BodyForward);
        NextReviewSeconds=time+ReviewSeconds;
        return new(time,NextReviewSeconds,Preparing,target,drive,lookahead,curvature,required,ceiling,
            f.RearLongitudinal,f.RearLateral,RearWheelDynamics.SlipRatio(rearU,wheel.AngularVelocityRadPerSecond,wheel.Parameters),v.SlideRadians,reason);
    }
}
