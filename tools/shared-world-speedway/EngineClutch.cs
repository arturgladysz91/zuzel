using CoreSim.Setup;

namespace SharedWorldSpeedway;

/// <summary>PROVISIONAL effective crank inertia, dry clutch capacity and rider release; no measured calibration.</summary>
public sealed record EngineClutchParameters
{
    public double CrankInertiaKgMetersSquared {get;init;}=.02;
    public double CapacityNewtonMeters {get;init;}=90;
    public double InitialEngineRpm {get;init;}=6000;
    public double ReleaseSeconds {get;init;}=.4;
    public double TapeReleaseSeconds {get;init;}=0;
    public void Validate()
    {
        if(new[]{CrankInertiaKgMetersSquared,CapacityNewtonMeters,InitialEngineRpm,ReleaseSeconds,TapeReleaseSeconds}.Any(x=>!double.IsFinite(x))
            ||CrankInertiaKgMetersSquared<.005||CapacityNewtonMeters<=0||InitialEngineRpm<=0||InitialEngineRpm>Engine500.CutoffRpm
            ||ReleaseSeconds<=0||TapeReleaseSeconds<0)throw new ArgumentException("Finite positive provisional clutch/crank parameters required; inertia>=0.005 ensures a unique local engine solve at20ms.");
    }
}
public readonly record struct EngineClutchView(int RiderId,double AngularVelocityRadPerSecond,double Rpm,double RotationalEnergyJoules);
public sealed record EngineClutchStep(double Seconds,double BeforeOmega,double AfterOmega,double Engagement,double Throttle,
    double OverallRatio,double EngineTorqueNewtonMeters,double ClutchTorqueNewtonMeters,double WheelTorqueNewtonMeters,
    double CapacityNewtonMeters,double EndSlipRadPerSecond,double CombustionWorkJoules,double ClutchLossJoules,
    double TransmissionLossJoules,double EngineEnergyChangeJoules,bool Locked,int ContactQueries,RearWheelStep Wheel);
public sealed class EngineClutchState
{
    public EngineClutchParameters Parameters {get;}
    public double AngularVelocityRadPerSecond {get;private set;}
    public EngineClutchStep? Prepared {get;internal set;}
    public EngineClutchState(EngineClutchParameters parameters,double? omega=null)
    {parameters.Validate();Parameters=parameters;AngularVelocityRadPerSecond=omega??parameters.InitialEngineRpm*Math.PI/30;
        if(!double.IsFinite(AngularVelocityRadPerSecond)||AngularVelocityRadPerSecond<0)throw new ArgumentException("Finite forward crank state required.");}
    public EngineClutchView View(int id)=>new(id,AngularVelocityRadPerSecond,AngularVelocityRadPerSecond*30/Math.PI,
        .5*Parameters.CrankInertiaKgMetersSquared*AngularVelocityRadPerSecond*AngularVelocityRadPerSecond);
    public void Commit(){var step=Prepared??throw new InvalidOperationException("Prepare crank before commit.");
        if(!double.IsFinite(step.AfterOmega)||step.AfterOmega<0)throw new InvalidOperationException("Unsupported reverse/non-finite engine state.");AngularVelocityRadPerSecond=step.AfterOmega;}
}

/// <summary>Pure coupled crank/clutch solve around ORIGINAL rear-wheel contact queries. No body integration.</summary>
public static class EngineClutch
{
    public const int RootIterations=48;
    public static EngineClutchStep Evaluate(EngineClutchState state,BikeSetup setup,Engine500Parameters? engine,
        double time,double reaction,double requestedThrottle,float seconds,Func<double,RearWheelStep> wheelAtTorque)
    {
        var p=state.Parameters;var start=p.TapeReleaseSeconds+reaction;
        if(!double.IsFinite(time)||!double.IsFinite(reaction)||reaction<0||!double.IsFinite(requestedThrottle)||requestedThrottle is <0 or >1
            ||!float.IsFinite(seconds)||seconds is <0 or >.020001f)throw new ArgumentException("Finite local start interval required.");
        if(time<start-1e-10&&time+seconds>start+1e-8)throw new ArgumentException("Partition the accepted interval at the existing tape-reaction deadline.");
        var active=time+1e-10>=start;var throttle=active?requestedThrottle:0;
        var engagement=active?Math.Clamp((time+seconds/2d-start)/p.ReleaseSeconds,0,1):0;
        var capacity=engagement*p.CapacityNewtonMeters;
        var map=Engine500.Evaluate(0,setup,throttle,engine);var ratio=map.OverallRatio;var efficiency=map.Efficiency;
        var before=state.AngularVelocityRadPerSecond;var inertia=p.CrankInertiaKgMetersSquared;var queries=0;
        EngineClutchStep At(double clutch){
            // Brake/net crank torque: unchanged #116 map. Zero net prelaunch torque
            // maintains the prescribed running RPM; internal combustion/friction cancel.
            var low=before-seconds*clutch/(2*inertia);var high=low+seconds*Engine500.Curve.Max(x=>x.NewtonMeters)*throttle/(2*inertia);
            for(var i=0;i<RootIterations;i++){
                var middle=(low+high)/2;var torque=Engine500.Torque(Math.Max(0,middle)*30/Math.PI)*throttle;
                if(middle-before-seconds*(torque-clutch)/(2*inertia)>0)high=middle;else low=middle;
            }
            var mid=(low+high)/2;var after=2*mid-before;var crank=Engine500.Torque(Math.Max(0,mid)*30/Math.PI)*throttle;
            // Same #116 forward efficiency. Backdrive loses power in the reverse direction too.
            var wheelTorque=clutch*ratio*(clutch>=0?efficiency:1/efficiency);
            var wheel=wheelAtTorque(wheelTorque);queries++;
            var slip=after-ratio*wheel.AfterOmega;var midSlip=mid-ratio*wheel.MidOmega;
            return new(seconds,before,after,engagement,throttle,ratio,crank,clutch,wheelTorque,capacity,slip,
                crank*mid*seconds,clutch*midSlip*seconds,(clutch*ratio-wheelTorque)*wheel.MidOmega*seconds,
                .5*inertia*(after*after-before*before),false,0,wheel);
        }
        EngineClutchStep best;
        if(capacity==0||seconds==0)best=At(0);
        else{
            var a=At(-capacity);var b=At(capacity);
            if(b.EndSlipRadPerSecond>=0)best=b;
            else if(a.EndSlipRadPerSecond<=0)best=a;
            else{
                // Stick if endpoint synchronization is reachable; otherwise bounded sliding.
                // Endpoint (not midpoint) synchronization avoids alternating-slip chatter.
                var low=-capacity;var high=capacity;best=Math.Abs(a.EndSlipRadPerSecond)<Math.Abs(b.EndSlipRadPerSecond)?a:b;
                for(var i=0;i<RootIterations;i++){var c=(low+high)/2;var value=At(c);
                    if(Math.Abs(value.EndSlipRadPerSecond)<Math.Abs(best.EndSlipRadPerSecond))best=value;
                    if(value.EndSlipRadPerSecond>0)low=c;else high=c;}
            }
        }
        if(new[]{best.AfterOmega,best.Wheel.AfterOmega,best.CombustionWorkJoules,best.ClutchLossJoules,best.TransmissionLossJoules}.Any(x=>!double.IsFinite(x))
            ||best.AfterOmega<0||best.Wheel.AfterOmega<0||best.ClutchLossJoules<-.00001||best.TransmissionLossJoules<-.00001)
            throw new InvalidOperationException("Forward dry-clutch assumptions or dissipativity violated; no state/energy clamp.");
        return best with{Locked=capacity>0&&Math.Abs(best.EndSlipRadPerSecond)<1e-6,ContactQueries=queries};
    }
}
