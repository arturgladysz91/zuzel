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
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EngineLedgerH26? H26 {get;init;}
    public double MidOmega=>(BeforeOmega+AfterOmega)/2;
}
public readonly record struct ClutchTrial64(double Torque, double EngineMid, double Engagement,
    double Residual, int WheelQueries, RearWheelTrial64 Wheel);

public sealed class CrankClutch144
{
    public LaunchParameters144 Parameters {get;}
    public EngineLossH26? LossH26 {get;}
    public double Omega {get;private set;}
    public ClutchStep144 Prepared {get;private set;}=null!;
    public CrankClutch144(LaunchParameters144? parameters,double? omega):this(parameters,omega,null) { }
    public CrankClutch144(LaunchParameters144? parameters=null,double? omega=null,EngineLossH26? lossH26=null){
        lossH26?.Validate();LossH26=lossH26;
        Parameters=parameters??LaunchParameters144.Default;Parameters.Validate();
        if(lossH26 is not null && Parameters.CrankDragNewtonMeterSeconds!=0)throw new ArgumentException("H26 replaces legacy crank drag; double counting forbidden.");
        Omega=omega??Parameters.InitialCrankRpm*Math.PI/30;
        if(!double.IsFinite(Omega)||Omega<0)throw new ArgumentOutOfRangeException(nameof(omega));
    }
    public CrankClutch144 Copy()=>new(Parameters,Omega,LossH26);
    public void Commit(){if(Prepared is null||!double.IsFinite(Prepared.AfterOmega)||Prepared.AfterOmega<0)throw new InvalidOperationException("Invalid crank commit.");Omega=Prepared.AfterOmega;}
    // Engine midpoint is solved analytically on the unchanged piecewise-linear torque curve.
    double EngineMid(double torque,double throttle,double seconds){
        if(seconds==0)return Omega;
        var c=2*Parameters.CrankInertiaKgMetersSquared/seconds;
        for(int i=1;i<=Engine500.Curve.Count;i++){
            var a=Engine500.Curve[i-1];var b=i<Engine500.Curve.Count?Engine500.Curve[i]:new EngineTorquePoint(double.PositiveInfinity,0);
            var lo=a.Rpm*Math.PI/30;var hi=b.Rpm*Math.PI/30;
            var slope=double.IsFinite(hi)?(b.NewtonMeters-a.NewtonMeters)/(hi-lo):0;var intercept=a.NewtonMeters-slope*lo;
            var value=LossH26 is null
                ?(c*Omega+throttle*intercept-torque)/(c+Parameters.CrankDragNewtonMeterSeconds-throttle*slope)
                :(c*Omega+throttle*intercept-torque)/(c+LossH26.NetViscosity(throttle)-throttle*slope);
            if(value>=lo-1e-9&&value<=hi+1e-9)return Math.Max(0,value);
        }
        throw new InvalidOperationException("No nonnegative crank midpoint; no hidden stall reset.");
    }
    // Every trial in one contact query reads the same accepted engine state.
    // Precompute the affine segments with the original floating-point grouping.
    public Func<double,double> EngineMidpointEvaluator64(double throttle,double seconds)
    {
        double omega=Omega;
        if(seconds==0) return _=>omega;
        double c=2*Parameters.CrankInertiaKgMetersSquared/seconds;
        var lines=new (double Lo,double Hi,double Numerator,double Denominator)[Engine500.Curve.Count];
        for(int i=0;i<lines.Length;i++)
        {
            var a=Engine500.Curve[i];
            var b=i+1<lines.Length?Engine500.Curve[i+1]:new EngineTorquePoint(double.PositiveInfinity,0);
            double lo=a.Rpm*Math.PI/30,hi=b.Rpm*Math.PI/30;
            double slope=double.IsFinite(hi)?(b.NewtonMeters-a.NewtonMeters)/(hi-lo):0;
            double intercept=a.NewtonMeters-slope*lo;
            double denominator=LossH26 is null?c+Parameters.CrankDragNewtonMeterSeconds-throttle*slope:
                c+LossH26.NetViscosity(throttle)-throttle*slope;
            lines[i]=(lo,hi,c*omega+throttle*intercept,denominator);
        }
        return torque=>
        {
            foreach(var line in lines)
            {
                double value=(line.Numerator-torque)/line.Denominator;
                if(value>=line.Lo-1e-9&&value<=line.Hi+1e-9) return Math.Max(0,value);
            }
            throw new InvalidOperationException("No nonnegative crank midpoint; no hidden stall reset.");
        };
    }

    public Engine500Command Prepare(double throttle,double ratio,double efficiency,double seconds,bool released,Func<double,RearWheelStep> wheelAtTorque)
        =>Prepare(throttle,ratio,efficiency,seconds,released,wheelAtTorque,null);
    public Engine500Command Prepare(double throttle,double ratio,double efficiency,double seconds,bool released,Func<double,RearWheelStep> wheelAtTorque,
        RearWheelParameters? wheelParameters){
        var trial=Solve(throttle,ratio,efficiency,seconds,released,wheelAtTorque,static wheel=>wheel.MidOmega);
        return Store(throttle,ratio,efficiency,seconds,trial.Torque,trial.EngineMid,trial.Engagement,trial.Residual,
            trial.Queries,trial.Wheel,wheelParameters);
    }

    public ClutchTrial64 Evaluate64(double throttle,double ratio,double efficiency,double seconds,bool released,
        Func<double,RearWheelTrial64> wheelAtTorque) =>
        Evaluate64(throttle,ratio,efficiency,seconds,released,wheelAtTorque,null);

    public ClutchTrial64 Evaluate64(double throttle,double ratio,double efficiency,double seconds,bool released,
        Func<double,RearWheelTrial64> wheelAtTorque,Func<double,double>? engineMidpoint)
    {
        var trial=Solve(throttle,ratio,efficiency,seconds,released,wheelAtTorque,static wheel=>wheel.MidOmega,engineMidpoint);
        return new(trial.Torque,trial.EngineMid,trial.Engagement,trial.Residual,trial.Queries,trial.Wheel);
    }

    public Engine500Command Accept64(double throttle,double ratio,double efficiency,double seconds,
        ClutchTrial64 trial,RearWheelStep appliedWheel,RearWheelParameters wheelParameters)
    {
        double capacity=Parameters.ClutchCapacityNewtonMeters*trial.Engagement;
        double residual=trial.Torque-Math.Clamp(Parameters.ClutchViscosityNewtonMeterSeconds*trial.Engagement*
            (trial.EngineMid-ratio*appliedWheel.MidOmega),-capacity,capacity);
        return Store(throttle,ratio,efficiency,seconds,trial.Torque,trial.EngineMid,trial.Engagement,residual,
            trial.WheelQueries,appliedWheel,wheelParameters);
    }

    private (double Torque,double EngineMid,double Engagement,double Residual,int Queries,T Wheel) Solve<T>(
        double throttle,double ratio,double efficiency,double seconds,bool released,Func<double,T> wheelAtTorque,
        Func<T,double> midOmega,Func<double,double>? engineMidpoint=null)
    {
        if(!double.IsFinite(throttle)||throttle is <0 or >1||!double.IsFinite(seconds)||seconds is <0 or >.020001
            ||!double.IsFinite(ratio)||ratio<=0||efficiency is <=0 or >1)throw new ArgumentException("Physical midpoint interval required.");
        var p=Parameters;var fraction=Math.Clamp((Omega*30/Math.PI-p.DisengagedBelowRpm)/(p.FullyEngagedAboveRpm-p.DisengagedBelowRpm),0,1);
        var engagement=released?fraction*fraction*(3-2*fraction):0;var capacity=p.ClutchCapacityNewtonMeters*engagement;
        int queries=0;
        (double Residual,double EngineMid,T Wheel) At(double torque){
            var engine=engineMidpoint is null?EngineMid(torque,throttle,seconds):engineMidpoint(torque);var wheel=wheelAtTorque(torque*ratio*(torque>=0?efficiency:1/efficiency));queries++;
            var slip=engine-ratio*midOmega(wheel);
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
        return (chosen,point.EngineMid,engagement,point.Residual,queries,point.Wheel);
    }

    private Engine500Command Store(double throttle,double ratio,double efficiency,double seconds,double chosen,double mid,
        double engagement,double contactResidual,int queries,RearWheelStep wheel,RearWheelParameters? wheelParameters)
    {
        var p=Parameters;
        var after=2*mid-Omega;var crank=Engine500.Torque(mid*30/Math.PI);
        var combustion=throttle*crank*mid*seconds;var drag=p.CrankDragNewtonMeterSeconds*mid*mid*seconds;
        double mechanical=0,pumping=0,grossTorque=throttle*crank;
        if(LossH26 is {} losses){
            mechanical=losses.Mechanical(mid);pumping=losses.Pumping(mid,throttle);
            grossTorque=throttle*(crank+mechanical);
            combustion=grossTorque*mid*seconds;drag=(mechanical+pumping)*mid*seconds;
        }
        var slipRate=mid-ratio*wheel.MidOmega;var heat=chosen*slipRate*seconds;
        var transmission=chosen*ratio*wheel.MidOmega*seconds-wheel.EngineWorkJoules;
        var change=.5*p.CrankInertiaKgMetersSquared*(after*after-Omega*Omega);
        var residual=combustion-drag-change-wheel.EngineWorkJoules-heat-transmission;
        if(after<0||!double.IsFinite(after)||heat< -1e-7||transmission< -1e-7||Math.Abs(residual)>1e-6)
            throw new InvalidOperationException("Crank/clutch passivity or energy identity failed.");
        var command=new Engine500Command(mid*30/Math.PI,ratio,crank,crank*ratio*efficiency,wheel.DriveTorqueNewtonMeters,efficiency);
        Prepared=new(seconds,Omega,after,throttle*crank,chosen,engagement,slipRate,combustion,drag,heat,transmission,residual,contactResidual,queries,wheel,command);
        if(LossH26 is not null){
            var w=wheel;
            var wp=wheelParameters??RearWheelParameters.Default;
            double wheelChange=.5*wp.WheelInertiaKgMetersSquared*(w.AfterOmega*w.AfterOmega-w.BeforeOmega*w.BeforeOmega);
            double bodyWork=(w.AppliedLongitudinalNewtons??w.LongitudinalNewtons)*
                (wp.WheelRadiusMeters*w.MidOmega-w.MidSlipVelocityMetersPerSecond)*seconds;
            Prepared=Prepared with {H26=new(grossTorque,mechanical,pumping,combustion,
                mechanical*mid*seconds,pumping*mid*seconds,change,chosen*mid*seconds,wheelChange,bodyWork,residual)};
        }
        return command;
    }
}
