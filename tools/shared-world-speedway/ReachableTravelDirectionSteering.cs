namespace SharedWorldSpeedway;

/// <summary>PROVISIONAL #120: same #117 score, restricted to physically reachable steering endpoints.</summary>
public static class ReachableTravelDirectionSteering
{
    public static TravelDirectionDecision Decide(RiderView v,float acceleration,float mass,float inertia,float responseSeconds,
        TwoContactBikeParameters geometry,float before,float seconds,Func<float,TwoContactForces> response)
    {
        if(!float.IsFinite(acceleration)||!float.IsFinite(inertia)||inertia<=0||!float.IsFinite(responseSeconds)||responseSeconds<=0
            ||!float.IsFinite(before)||!float.IsFinite(seconds)||seconds<0||seconds>.020001f
            ||Math.Abs(before)>geometry.MaximumSteeringRadians)throw new ArgumentException("Finite reachable steering inputs required.");
        var queries=0;var current=response(before);queries++;
        var motion=TravelDirectionState.Observe(v,current.NetForce,mass);
        var desired=acceleration/Math.Max(v.Speed,TravelDirectionState.MinimumDirectionSpeedMetersPerSecond);
        var low=Math.Max(-geometry.MaximumSteeringRadians,before-geometry.SteeringRateRadiansPerSecond*seconds);
        var high=Math.Min(geometry.MaximumSteeringRadians,before+geometry.SteeringRateRadiansPerSecond*seconds);
        var minimum=double.PositiveInfinity;var maximum=double.NegativeInfinity;
        var minBeta=double.PositiveInfinity;var maxBeta=double.NegativeInfinity;
        double Score(TwoContactForces f,out double chi,out double betaRate){
            chi=TravelDirectionState.Observe(v,f.NetForce,mass).ChiDot;
            betaRate=v.AngularVelocity+responseSeconds*f.NetTorque/inertia-chi;
            var limit=(TravelDirectionSteering.ControlledSlideLimitRadians-motion.Beta)/responseSeconds;
            var lower=(-TravelDirectionSteering.ControlledSlideLimitRadians-motion.Beta)/responseSeconds;
            var excess=betaRate-Math.Clamp(betaRate,lower,limit);
            return (chi-desired)*(chi-desired)+betaRate*betaRate+excess*excess;
        }
        var best=before;var bestScore=Score(current,out _,out _);
        void Consider(float endpoint){
            endpoint=Math.Clamp(endpoint,low,high);
            // The original actuator applies the mean of accepted and prepared angles.
            var f=response((before+endpoint)/2);queries++;var score=Score(f,out var chi,out var beta);
            minimum=Math.Min(minimum,chi);maximum=Math.Max(maximum,chi);minBeta=Math.Min(minBeta,beta);maxBeta=Math.Max(maxBeta,beta);
            if(score<bestScore-1e-12||Math.Abs(score-bestScore)<=1e-12&&Math.Abs(endpoint-before)<Math.Abs(best-before)){best=endpoint;bestScore=score;}
        }
        var step=(high-low)/TravelDirectionSteering.ResponseIntervals;
        for(var i=0;i<=TravelDirectionSteering.ResponseIntervals;i++)Consider(low+i*step);
        var radius=step;
        for(var i=0;i<TravelDirectionSteering.RefinementIterations;i++){var center=best;radius/=2;Consider(Math.Max(low,center-radius));Consider(Math.Min(high,center+radius));}
        var used=response((before+best)/2);queries++;Score(used,out var usedChi,out var usedBeta);
        var capacity=current.Budget/(mass*Math.Max(v.Speed,TravelDirectionState.MinimumDirectionSpeedMetersPerSecond));
        return new(best,motion,desired,usedChi,usedBeta,minimum,maximum,minBeta,maxBeta,
            !motion.DirectionDefined||desired<minimum||desired>maximum,
            motion.Beta*motion.BetaDot>0&&Math.Abs(motion.Beta)+responseSeconds*Math.Abs(motion.BetaDot)>TravelDirectionSteering.ControlledSlideLimitRadians,
            motion.Beta>0?minBeta>0:motion.Beta<0&&maxBeta<0,false,queries,capacity,Math.Abs(desired)>capacity);
    }
}
