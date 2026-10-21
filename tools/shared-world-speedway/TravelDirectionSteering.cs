using System.Numerics;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Positive angles/rates are counterclockwise. Beta is body minus velocity heading.</summary>
public readonly record struct TravelDirectionState(double Psi,double Chi,double Beta,double PsiDot,double ChiDot,double BetaDot,bool DirectionDefined)
{
    public const float MinimumDirectionSpeedMetersPerSecond=.5f;
    public static TravelDirectionState Observe(RiderView v,Vector2 force,float mass)
    {
        if(!float.IsFinite(mass)||mass<=0)throw new ArgumentOutOfRangeException(nameof(mass));
        var defined=v.Speed>=MinimumDirectionSpeedMetersPerSecond;
        var chi=defined?Math.Atan2(v.Velocity.Y,v.Velocity.X):v.Heading;
        var rate=defined?TwoContactForces.Cross(v.Velocity,force)/(mass*v.Speed*v.Speed):0;
        return new(v.Heading,chi,BikeAngles.Wrap(v.Heading-chi),v.AngularVelocity,rate,(double)v.AngularVelocity-rate,defined);
    }
}
public sealed record TravelDirectionDecision(float AngleRequestRadians,TravelDirectionState Motion,double DesiredChiDot,
    double PredictedChiDot,double PredictedBetaDot,double MinimumChiDot,double MaximumChiDot,
    double MinimumProjectedBetaDot,double MaximumProjectedBetaDot,bool DirectionOutsideSampledResponse,
    bool GrowingSlide,bool NoSampledSlideRecovery,bool RateLimited,int ContactQueries,
    double AbsoluteCapacityChiDotBound,bool DirectionExceedsTotalCapacity);

/// <summary>PROVISIONAL, stateless local contact response search. No world rollout or beta=0 target.</summary>
public static class TravelDirectionSteering
{
    public const int ResponseIntervals=32,RefinementIterations=8;
    // Operating envelope, not a tyre coefficient or a force clamp. Beyond it,
    // steering prioritizes arresting outward slip growth. Recovery is not guaranteed.
    public const double ControlledSlideLimitRadians=Math.PI/4;
    public static TravelDirectionDecision Decide(RiderView v,float acceleration,float mass,float inertia,float responseSeconds,
        TwoContactBikeParameters geometry,float before,float seconds,Func<float,TwoContactForces> response)
    {
        if(!float.IsFinite(acceleration)||!float.IsFinite(inertia)||inertia<=0||!float.IsFinite(responseSeconds)||responseSeconds<=0
            ||!float.IsFinite(before)||!float.IsFinite(seconds)||seconds<0||seconds>.020001f)throw new ArgumentException("Finite local steering inputs required.");
        var queries=0;var current=response(before);queries++;
        var motion=TravelDirectionState.Observe(v,current.NetForce,mass);
        var desired=acceleration/Math.Max(v.Speed,TravelDirectionState.MinimumDirectionSpeedMetersPerSecond);
        var minimum=double.PositiveInfinity;var maximum=double.NegativeInfinity;
        var minBeta=double.PositiveInfinity;var maxBeta=double.NegativeInfinity;
        var max=geometry.MaximumSteeringRadians;var step=2*max/ResponseIntervals;
        double Score(TwoContactForces f,out double chi,out double betaRate){
            chi=TravelDirectionState.Observe(v,f.NetForce,mass).ChiDot;
            // Local angular-response forecast only: the original contacts supply ALL torque.
            // Unlike #113, its reference is the direction rate actually available at this angle.
            betaRate=v.AngularVelocity+responseSeconds*f.NetTorque/inertia-chi;
            var limit=(ControlledSlideLimitRadians-motion.Beta)/responseSeconds;
            var lower=(-ControlledSlideLimitRadians-motion.Beta)/responseSeconds;
            var excess=betaRate-Math.Clamp(betaRate,lower,limit);
            return (chi-desired)*(chi-desired)+betaRate*betaRate+excess*excess;
        }
        var best=before;var bestScore=Score(current,out _,out _);
        void Consider(float angle){var f=response(angle);queries++;var score=Score(f,out var chi,out var beta);
            minimum=Math.Min(minimum,chi);maximum=Math.Max(maximum,chi);minBeta=Math.Min(minBeta,beta);maxBeta=Math.Max(maxBeta,beta);
            if(score<bestScore-1e-12||Math.Abs(score-bestScore)<=1e-12&&Math.Abs(angle-before)<Math.Abs(best-before)){best=angle;bestScore=score;}}
        for(var i=0;i<=ResponseIntervals;i++)Consider(-max+i*step);
        // Fixed bounded local refinement; no roots to nanosecond precision and no cached world.
        var radius=step;
        for(var i=0;i<RefinementIterations;i++){var center=best;radius/=2;Consider(Math.Max(-max,center-radius));Consider(Math.Min(max,center+radius));}
        var after=before+Math.Clamp(best-before,-geometry.SteeringRateRadiansPerSecond*seconds,geometry.SteeringRateRadiansPerSecond*seconds);
        var used=response((before+after)/2);queries++;Score(used,out var usedChi,out var usedBeta);
        var capacity=current.Budget/(mass*Math.Max(v.Speed,TravelDirectionState.MinimumDirectionSpeedMetersPerSecond));
        return new(best,motion,desired,usedChi,usedBeta,minimum,maximum,minBeta,maxBeta,
            !motion.DirectionDefined||desired<minimum||desired>maximum,
            motion.Beta*motion.BetaDot>0&&Math.Abs(motion.Beta)+responseSeconds*Math.Abs(motion.BetaDot)>ControlledSlideLimitRadians,
            motion.Beta>0?minBeta>0:motion.Beta<0&&maxBeta<0,Math.Abs(after-best)>1e-6,queries,capacity,Math.Abs(desired)>capacity);
    }
}
