namespace SharedWorldSpeedway;

/// <summary>PROVISIONAL launch coupling, not a calibrated speedway clutch. Original engine map/ratio/efficiency and tyre laws are unchanged.</summary>
public sealed record LaunchParameters144(double CrankInertiaKgMetersSquared=.01,double InitialCrankRpm=4000,
    double CrankDragNewtonMeterSeconds=0,double ClutchCapacityNewtonMeters=80,double ClutchViscosityNewtonMeterSeconds=20,
    double DisengagedBelowRpm=1500,double FullyEngagedAboveRpm=4000)
{
    public static LaunchParameters144 Default {get;}=new();
    public void Validate(){
        if(new[]{CrankInertiaKgMetersSquared,InitialCrankRpm,CrankDragNewtonMeterSeconds,ClutchCapacityNewtonMeters,ClutchViscosityNewtonMeterSeconds,
            DisengagedBelowRpm,FullyEngagedAboveRpm}.Any(x=>!double.IsFinite(x))||CrankInertiaKgMetersSquared<.005||InitialCrankRpm<=0||InitialCrankRpm>=Engine500.CutoffRpm
            ||CrankDragNewtonMeterSeconds<0||ClutchCapacityNewtonMeters<=0||ClutchViscosityNewtonMeterSeconds<=0
            ||DisengagedBelowRpm<0||FullyEngagedAboveRpm<=DisengagedBelowRpm)throw new ArgumentException("Finite explicit launch parameters required.");
    }
}
public sealed record ClutchStep144(double Seconds,double BeforeOmega,double AfterOmega,double CrankTorque,double ClutchTorque,
    double Engagement,double SlipRadiansPerSecond,double CombustionWorkJoules,double CrankDragWorkJoules,double ClutchHeatJoules,
    double TransmissionLossJoules,double EnergyResidualJoules,double RootResidualNewtonMeters,int WheelQueries,RearWheelStep Wheel,Engine500Command Engine)
{
    public double MidOmega=>(BeforeOmega+AfterOmega)/2;
}
public sealed class CrankClutch144
{
    public LaunchParameters144 Parameters {get;}
    public double Omega {get;private set;}
    public ClutchStep144 Prepared {get;private set;}=null!;
    public CrankClutch144(LaunchParameters144? parameters=null,double? omega=null){
        Parameters=parameters??LaunchParameters144.Default;Parameters.Validate();Omega=omega??Parameters.InitialCrankRpm*Math.PI/30;
        if(!double.IsFinite(Omega)||Omega<0)throw new ArgumentOutOfRangeException(nameof(omega));
    }
    public CrankClutch144 Copy()=>new(Parameters,Omega);
    public void Commit(){if(Prepared is null||!double.IsFinite(Prepared.AfterOmega)||Prepared.AfterOmega<0)throw new InvalidOperationException("Invalid crank commit.");Omega=Prepared.AfterOmega;}
    // Engine midpoint is solved analytically on the unchanged piecewise-linear torque curve.
    double EngineMid(double torque,double throttle,double seconds){
        if(seconds==0)return Omega;
        var c=2*Parameters.CrankInertiaKgMetersSquared/seconds;
        for(int i=1;i<=Engine500.Curve.Count;i++){
            var a=Engine500.Curve[i-1];var b=i<Engine500.Curve.Count?Engine500.Curve[i]:new EngineTorquePoint(double.PositiveInfinity,0);
            var lo=a.Rpm*Math.PI/30;var hi=b.Rpm*Math.PI/30;
            var slope=double.IsFinite(hi)?(b.NewtonMeters-a.NewtonMeters)/(hi-lo):0;var intercept=a.NewtonMeters-slope*lo;
            var value=(c*Omega+throttle*intercept-torque)/(c+Parameters.CrankDragNewtonMeterSeconds-throttle*slope);
            if(value>=lo-1e-9&&value<=hi+1e-9)return Math.Max(0,value);
        }
        throw new InvalidOperationException("No nonnegative crank midpoint; no hidden stall reset.");
    }
    public Engine500Command Prepare(double throttle,double ratio,double efficiency,double seconds,bool released,Func<double,RearWheelStep> wheelAtTorque){
        if(!double.IsFinite(throttle)||throttle is <0 or >1||!double.IsFinite(seconds)||seconds is <0 or >.020001
            ||!double.IsFinite(ratio)||ratio<=0||efficiency is <=0 or >1)throw new ArgumentException("Physical midpoint interval required.");
        var p=Parameters;var fraction=Math.Clamp((Omega*30/Math.PI-p.DisengagedBelowRpm)/(p.FullyEngagedAboveRpm-p.DisengagedBelowRpm),0,1);
        var engagement=released?fraction*fraction*(3-2*fraction):0;var capacity=p.ClutchCapacityNewtonMeters*engagement;
        int queries=0;
        (double Residual,double EngineMid,RearWheelStep Wheel) At(double torque){
            var engine=EngineMid(torque,throttle,seconds);var wheel=wheelAtTorque(torque*ratio*(torque>=0?efficiency:1/efficiency));queries++;
            var slip=engine-ratio*wheel.MidOmega;
            return(torque-Math.Clamp(p.ClutchViscosityNewtonMeterSeconds*engagement*slip,-capacity,capacity),engine,wheel);
        }
        double chosen=0;var point=At(0);
        if(capacity>0){
            var lower=-capacity;var upper=capacity;var a=At(lower);var b=At(upper);
            if(a.Residual>1e-8||b.Residual< -1e-8)throw new InvalidOperationException("Unbracketed passive clutch solve.");
            int lastSide=0;
            for(int i=0;i<48;i++){
                chosen=(lower*b.Residual-upper*a.Residual)/(b.Residual-a.Residual);
                if(!double.IsFinite(chosen)||chosen<=lower||chosen>=upper)chosen=(lower+upper)/2;
                point=At(chosen);if(Math.Abs(point.Residual)<1e-8)break;
                // Illinois safeguard avoids false-position endpoint stagnation at a saturated clutch.
                if(point.Residual<0){lower=chosen;a=point;if(lastSide<0)b=(b.Residual/2,b.EngineMid,b.Wheel);lastSide=-1;}
                else{upper=chosen;b=point;if(lastSide>0)a=(a.Residual/2,a.EngineMid,a.Wheel);lastSide=1;}
                if(i==47)throw new InvalidOperationException("Clutch solve did not converge.");
            }
        }
        var mid=point.EngineMid;var after=2*mid-Omega;var crank=Engine500.Torque(mid*30/Math.PI);
        var combustion=throttle*crank*mid*seconds;var drag=p.CrankDragNewtonMeterSeconds*mid*mid*seconds;
        var slipRate=mid-ratio*point.Wheel.MidOmega;var heat=chosen*slipRate*seconds;
        var transmission=chosen*ratio*point.Wheel.MidOmega*seconds-point.Wheel.EngineWorkJoules;
        var change=.5*p.CrankInertiaKgMetersSquared*(after*after-Omega*Omega);
        var residual=combustion-drag-change-point.Wheel.EngineWorkJoules-heat-transmission;
        if(after<0||!double.IsFinite(after)||heat< -1e-7||transmission< -1e-7||Math.Abs(residual)>1e-6)
            throw new InvalidOperationException("Crank/clutch passivity or energy identity failed.");
        var command=new Engine500Command(mid*30/Math.PI,ratio,crank,crank*ratio*efficiency,point.Wheel.DriveTorqueNewtonMeters,efficiency);
        Prepared=new(seconds,Omega,after,throttle*crank,chosen,engagement,slipRate,combustion,drag,heat,transmission,residual,point.Residual,queries,point.Wheel,command);
        return command;
    }
}
