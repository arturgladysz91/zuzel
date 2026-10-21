namespace SharedWorldSpeedway;

public sealed record PhysicalSteeringDecision(float AngleRequestRadians,double DesiredDirectionRate,double ActualDirectionRate,double BetaRadians,
    double RequestedTorque,double PredictedTorque,double SampledMinimumTorque,double SampledMaximumTorque,bool OutsideSampledResponse,bool RateLimited,int ContactQueries);

/// <summary>PROVISIONAL local yaw response inversion. Only commands the massless front steering.</summary>
public static class PhysicalSteeringControl
{
    public const int ResponseIntervals=32,RootIterations=14;
    public const double TorqueRootToleranceNewtonMeters=.02;
    public static PhysicalSteeringDecision Decide(RiderView v,float normalAcceleration,float mass,float inertia,float yawResponseSeconds,
        TwoContactBikeParameters geometry,float before,float seconds,Func<float,TwoContactForces> response)
    {
        if(!float.IsFinite(normalAcceleration)||!float.IsFinite(mass)||mass<=0||!float.IsFinite(inertia)||inertia<=0||!float.IsFinite(yawResponseSeconds)||yawResponseSeconds<=0)
            throw new ArgumentException("Finite acceleration and positive inertia/yaw response required.");
        // The existing path preview/cross-track PD uses velocity, not body heading.
        // Equal direction/body rates describe ANY constant beta, not beta=0.
        var desired=normalAcceleration/Math.Max(v.Speed,.5f);
        var torque=inertia*(desired-v.AngularVelocity)/yawResponseSeconds;
        var max=geometry.MaximumSteeringRadians;var count=0;
        TwoContactForces Query(float angle){count++;return response(angle);}
        var current=Query(before);
        var actual=v.Speed>.01f?TwoContactForces.Cross(v.Velocity,current.NetForce)/(mass*v.Speed*v.Speed):0;
        var best=before;double bestError=Math.Abs(current.NetTorque-torque),minimum=current.NetTorque,maximum=minimum;
        var lastAngle=-max;var last=Query(lastAngle).NetTorque;
        void Consider(float angle,double value){minimum=Math.Min(minimum,value);maximum=Math.Max(maximum,value);var error=Math.Abs(value-torque);
            var bothRoots=error<TorqueRootToleranceNewtonMeters&&bestError<TorqueRootToleranceNewtonMeters;
            if(bothRoots?Math.Abs(angle-before)<Math.Abs(best-before):error<bestError){best=angle;bestError=error;}}
        Consider(lastAngle,last);
        for(var i=1;i<=ResponseIntervals;i++){
            var angle=-max+2*max*i/ResponseIntervals;var value=Query(angle).NetTorque;Consider(angle,value);
            if((last-torque)*(value-torque)<0){
                var low=lastAngle;var high=angle;var lowValue=last;
                for(var j=0;j<RootIterations;j++){
                    var mid=(low+high)/2;var middle=Query(mid).NetTorque;Consider(mid,middle);
                    if((lowValue-torque)*(middle-torque)<=0)high=mid;else{low=mid;lowValue=middle;}
                }
            }
            lastAngle=angle;last=value;
        }
        // No integrator, remembered torque error or hidden body state. An unreachable
        // request selects the best sampled contact response without accumulating it.
        var after=before+Math.Clamp(best-before,-geometry.SteeringRateRadiansPerSecond*seconds,geometry.SteeringRateRadiansPerSecond*seconds);
        var predicted=Query((before+after)/2).NetTorque;
        return new(best,desired,actual,v.SlideRadians,torque,predicted,minimum,maximum,torque<minimum||torque>maximum,Math.Abs(after-best)>1e-6,count);
    }
}
