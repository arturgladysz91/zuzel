using SharedWorldSpeedway;

namespace SpeedwayHandling;

// The search changes controls only. Reference distance locates a maneuver; measured
// course/yaw close its steering loop. These are not clocked action tapes or speed caps.
internal sealed record RecoveryWindow209(int Visit,double From,double To,double Gas,double Steering,
    double CourseGain,double YawGain,bool Predictive=false);

internal static class Recovery209
{
    internal static FreeRacingAction Select(Start197 state,FreeRacingAction baseline,FreeRacingAction proposed,
        Predictor198 model,Func<Start197,FreeRacingAction> continuation,double goal)
    {
        if(proposed.Drive<=baseline.Drive+1e-7)return proposed;
        var audit=ThrottleTrace209.Current;ThrottleTrace209.Current=null;
        try
        {
            // The exact pending queue, posture, shaft, wheel and strict observer enter
            // every fork. One request is changed; the original feedback supplies the
            // demonstrated successor. Receding application is independently rechecked S3.
            foreach(double fraction in new[]{1d,.75,.5,.25})
            {
                var candidate=ExitPolicy208.Bound(baseline.SteeringRadians+
                    fraction*(proposed.SteeringRadians-baseline.SteeringRadians),
                    baseline.Drive+fraction*(proposed.Drive-baseline.Drive));
                var fork=model.BeginFeedback207(state,at=>at.State.Time<state.State.Time+.025?
                    candidate:continuation(at),goal,.5,profile:true);
                fork.Advance(150,state.State.Time+1.2);
                if(fork.Reached||fork.Outcome is null&&fork.Snapshot.Time>=state.State.Time+1.2-1e-8)
                {
                    audit?.Add("209 predictive selection",proposed.Drive,candidate.Drive);
                    return candidate;
                }
            }
            audit?.Add("209 predictive rejection",proposed.Drive,baseline.Drive);
            return baseline;
        }
        finally {ThrottleTrace209.Current=audit;}
    }

    internal static FreeRacingAction Apply(Start197 state,FreeRacingAction baseline,RecoveryWindow209 window,
        double exit)
    {
        double s=state.State.Rider.TotalReferenceMeters-exit;
        double blend=ExitPolicy208.Gate((s-window.From)/3)*ExitPolicy208.Gate((window.To-s)/3);
        double correction=window.Steering+window.CourseGain*Corridor199.CourseError(state.State.Rider)+
            window.YawGain*state.State.Rider.AngularVelocity;
        return ExitPolicy208.Bound(baseline.SteeringRadians+blend*correction,
            baseline.Drive+blend*window.Gas*(1-baseline.Drive));
    }
}
