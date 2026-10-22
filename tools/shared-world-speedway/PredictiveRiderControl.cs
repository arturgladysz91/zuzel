using System.Diagnostics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record PredictiveSteeringCandidate(float CorrectionRadians,double Score,bool Safe,PredictiveRiderResult Prediction);
public sealed record PredictiveSteeringDecision(double ObservedAt,double Activation,float CorrectionRadians,bool Queued,string Reason,
    double Milliseconds,PredictiveSteeringCandidate[] Candidates);
public sealed record PredictiveSteeringActivation(double Time,float CorrectionRadians,float TargetRadians);

/// <summary>Default-OFF delayed steering intention. Original throttle/plan and physical #117 tracking are retained.</summary>
public sealed class PredictiveRiderControl
{
    public const double ReviewSeconds=.4,HorizonSeconds=.6,PulseSeconds=.1;
    public const float CorrectionRadians=(float)(Math.PI/200); // one nominal 10-ms endpoint move at the existing 90 deg/s
    public const double MinimumScoreGainSquareMeters=.01,ReversalCostSquareMeters=.01;
    public const double ProgressWeightMeters=.1,PredictionAllowanceMeters=.1;
    private PredictiveSteeringDecision? pending;
    private double activeUntil;
    private float? activeTarget;
    private float previousCorrection;
    public double NextReviewSeconds {get;private set;}
    public List<PredictiveSteeringDecision> Decisions {get;}=[];
    public List<PredictiveSteeringActivation> Activations {get;}=[];
    public double? NextControlDeadline(double time)=>pending is { } c&&c.Activation>time+1e-12?c.Activation:
        activeUntil>time+1e-12?activeUntil:null;

    public float? SteeringOverride(double time,float acceptedSteering,TwoContactBikeParameters geometry)
    {
        if(pending is { } ready&&time+1e-12>=ready.Activation){
            activeTarget=Math.Clamp(acceptedSteering+ready.CorrectionRadians,-geometry.MaximumSteeringRadians,geometry.MaximumSteeringRadians);
            activeUntil=ready.Activation+PulseSeconds;Activations.Add(new(time,ready.CorrectionRadians,activeTarget.Value));pending=null;
        }
        return time<activeUntil-1e-10?activeTarget:null;
    }
    public PredictiveSteeringDecision Decide(PredictiveRiderInput input,RiderSpec spec,PreparedCornerPlan activePlan,
        TrackStateSnapshot surface,WorldParameters parameters,float mass,float inertia,bool planWaiting)
    {
        if(!parameters.EnablePredictiveRiderControl||!parameters.EnableContinuousCornerPlanning)
            throw new ArgumentException("Predictive rider is explicitly opt-in and requires the prepared continuous plan.");
        NextReviewSeconds=input.Time+ReviewSeconds;
        PredictiveSteeringDecision Keep(string reason){var d=new PredictiveSteeringDecision(input.Time,input.Time+RiderController.ReactionSeconds(spec),0,false,reason,0,[]);Decisions.Add(d);return d;}
        if(planWaiting||pending is not null)return Keep("Unfinished delayed intention; preserve existing command/plan lifecycle.");
        // The validated model has one active immutable path. Do not claim prediction through an unmodelled path handover.
        if(activePlan.BendExitMeters-input.Rider.TotalReferenceMeters<(input.Rider.Speed+5)*.9+2.1)
            return Keep("Horizon could cross current plan lifecycle; original controller retained.");
        var watch=Stopwatch.StartNew();var candidates=new List<PredictiveSteeringCandidate>();
        foreach(var correction in new[]{0f,CorrectionRadians,-CorrectionRadians}){
            var forecast=PredictiveRiderModel.Predict(input,spec,activePlan.ForkForPrediction(),surface,parameters,mass,inertia,HorizonSeconds,
                new(true),correction==0?null:correction);
            var points=forecast.Points;double tracking=0,heading=0;
            var safe=true;
            for(var i=1;i<points.Length;i++){
                var point=points[i];var before=points[i-1];var dt=point.Time-before.Time;
                var q=activePlan.Path.At(point.Rider.TotalReferenceMeters);var delta=new MeterPoint(point.Rider.Position.X-q.Position.X,point.Rider.Position.Y-q.Position.Y);
                tracking+=dt*Math.Pow(MeterPoint.Dot(delta,q.Outward),2);
                var chi=Math.Atan2(point.Rider.Velocity.Y,point.Rider.Velocity.X);
                heading+=dt*Math.Pow(SpeedwayBikeDimensions.Reference.OverallMechanicalLengthMeters*BikeAngles.Wrap(chi-q.Heading),2);
                var uncertainty=(Math.Max(point.Rider.Speed,before.Rider.Speed)+1.05*Math.Max(Math.Abs(point.Rider.AngularVelocity),Math.Abs(before.Rider.AngularVelocity)))*dt/2+.012+PredictionAllowanceMeters;
                safe&=point.Inner-uncertainty>.05&&point.Outer-uncertainty>.5;
            }
            var final=points[^1].Rider;var growth=Math.Max(0,Math.Abs(final.SlideRadians)-Math.Abs(input.Rider.SlideRadians));
            var progress=final.TotalReferenceMeters-input.Rider.TotalReferenceMeters;
            var score=(tracking+heading)/HorizonSeconds+Math.Pow(SpeedwayBikeDimensions.Reference.OverallMechanicalLengthMeters*growth,2)
                -ProgressWeightMeters*progress+(previousCorrection*correction<0?ReversalCostSquareMeters:0);
            safe&=forecast.MaximumFrontUtilization<=1.00001&&forecast.MaximumRearUtilization<=1.00001
                &&forecast.MaximumSteeringRate<=(parameters.TwoContactBike??TwoContactBikeParameters.Default).SteeringRateRadiansPerSecond+1e-4
                &&Math.Abs(final.SlideRadians)<TravelDirectionSteering.ControlledSlideLimitRadians;
            candidates.Add(new(correction,score,safe,forecast));
        }
        var baseline=candidates[0];var best=candidates.Where(c=>c.Safe).OrderBy(c=>c.Score).FirstOrDefault();
        var changed=best is not null&&best.CorrectionRadians!=0&&baseline.Safe&&baseline.Score-best.Score>=MinimumScoreGainSquareMeters;
        var chosen=changed?best!:baseline;
        watch.Stop();
        var decision=new PredictiveSteeringDecision(input.Time,input.Time+RiderController.ReactionSeconds(spec),chosen.CorrectionRadians,changed,
            changed?"Delayed reachable steering pulse improves frozen future-motion score.":best is null?"No certified sampled option; original control retained, not a safe fallback claim.":"Insufficient forecast gain; original control retained.",
            watch.Elapsed.TotalMilliseconds,candidates.ToArray());
        if(changed){pending=decision;previousCorrection=chosen.CorrectionRadians;}
        Decisions.Add(decision);return decision;
    }
}
