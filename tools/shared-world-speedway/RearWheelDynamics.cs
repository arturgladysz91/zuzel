namespace SharedWorldSpeedway;

/// <summary>PROVISIONAL planar effective wheel; no engine, clutch or load transfer.</summary>
public sealed record RearWheelParameters
{
    public static RearWheelParameters Default { get; }=new();
    public double WheelRadiusMeters { get; init; }=.33;
    public double WheelInertiaKgMetersSquared { get; init; }=.9;
    public double RearBudgetFraction { get; init; }=.6;
    public double LongitudinalSlipStiffnessRatio { get; init; }=.1;
    public double MinimumContactRelaxationSeconds { get; init; }=.03;
    public double LowSpeedMetersPerSecond { get; init; }=.5;
    public double BearingResistanceNewtonMeterSeconds { get; init; }=.02;
    public void Validate()
    {
        if(new[]{WheelRadiusMeters,WheelInertiaKgMetersSquared,RearBudgetFraction,LongitudinalSlipStiffnessRatio,
            MinimumContactRelaxationSeconds,LowSpeedMetersPerSecond,BearingResistanceNewtonMeterSeconds}.Any(x=>!double.IsFinite(x))
            ||WheelRadiusMeters<=0||WheelInertiaKgMetersSquared<=0||RearBudgetFraction is <=0 or >=1
            ||LongitudinalSlipStiffnessRatio<=0||MinimumContactRelaxationSeconds<.02||LowSpeedMetersPerSecond<=0
            ||BearingResistanceNewtonMeterSeconds<0)throw new ArgumentException("Invalid experimental wheel parameters.");
    }
}
public readonly record struct RearWheelView(int RiderId,double RearWheelAngularVelocityRadPerSecond,double WheelRadiusMeters,double WheelInertiaKgMetersSquared)
{
    public double CircumferentialSpeedMetersPerSecond=>RearWheelAngularVelocityRadPerSecond*WheelRadiusMeters;
    public double Rpm=>RearWheelAngularVelocityRadPerSecond*30/Math.PI;
    public double RotationalEnergyJoules=>.5*WheelInertiaKgMetersSquared*RearWheelAngularVelocityRadPerSecond*RearWheelAngularVelocityRadPerSecond;
}
public readonly record struct RearWheelStep(double Seconds,double BeforeOmega,double AfterOmega,
    double RequestedTorqueNewtonMeters,double AvailableTorqueNewtonMeters,double DriveTorqueNewtonMeters,
    double BearingTorqueNewtonMeters,float LongitudinalNewtons,float FrontLateralNewtons,float RearLateralNewtons,
    double InstantSlipRatio,double MidSlipVelocityMetersPerSecond,double LongitudinalLossJoules,double BearingLossJoules,
    double EngineWorkJoules,double RearUtilization,double RootResidualMetersPerSecond,int RootIterations)
{
    public float LateralNewtons=>FrontLateralNewtons+RearLateralNewtons;
    public double MidOmega=>(BeforeOmega+AfterOmega)/2;
}
/// <summary>Accepted omega changes only in Commit, after the one Box2D step.</summary>
public sealed class RearWheelState
{
    public RearWheelParameters Parameters { get; }
    public double AngularVelocityRadPerSecond { get; private set; }
    public RearWheelStep Prepared { get; internal set; }
    public RearWheelState(double initialForwardSpeed,RearWheelParameters? parameters=null)
    {
        Parameters=parameters??RearWheelParameters.Default;Parameters.Validate();
        if(!double.IsFinite(initialForwardSpeed))throw new ArgumentOutOfRangeException(nameof(initialForwardSpeed));
        AngularVelocityRadPerSecond=initialForwardSpeed/Parameters.WheelRadiusMeters;
    }
    public RearWheelView View(int riderId)=>new(riderId,AngularVelocityRadPerSecond,Parameters.WheelRadiusMeters,Parameters.WheelInertiaKgMetersSquared);
    public void Commit()
    {
        if(!double.IsFinite(Prepared.AfterOmega))throw new InvalidOperationException("Non-finite wheel state.");
        AngularVelocityRadPerSecond=Prepared.AfterOmega;
    }
    internal void PrepareFree(float seconds)
    {
        var p=Parameters;var omega=AngularVelocityRadPerSecond;
        var middle=omega/(1+seconds*p.BearingResistanceNewtonMeterSeconds/(2*p.WheelInertiaKgMetersSquared));
        Prepared=new(seconds,omega,2*middle-omega,0,0,0,p.BearingResistanceNewtonMeterSeconds*middle,0,0,0,0,0,0,
            seconds*p.BearingResistanceNewtonMeterSeconds*middle*middle,0,0,0,0);
    }
}
public static class RearWheelDynamics
{
    // The unchanged front constitutive law, also usable at a separate steered contact.
    public static float FrontLateralForce(double side,double frontBudget,double lateralScaleSquared)
        =>(float)(-frontBudget*side/Math.Sqrt(side*side+lateralScaleSquared));
    public const int MaximumRootIterations=8;
    public const double RootToleranceMetersPerSecond=1e-10;
    public static double SlipRatio(double forward,double omega,RearWheelParameters p)
        =>(p.WheelRadiusMeters*omega-forward)/Math.Sqrt(forward*forward+p.LowSpeedMetersPerSecond*p.LowSpeedMetersPerSecond);
    public static RearWheelStep Evaluate(double forward,double side,double omega,float budget,float mass,double lateralScaleSquared,
        double requestedTorque,double availableTorque,double driveTorque,double resistanceForward,float seconds,RearWheelParameters p,double? rearBudgetNewtons=null)
    {
        // The body is NOT integrated here. Freeze its accepted axes and use its
        // effective longitudinal inverse mass in this local contact equation.
        // Only Box2D applies the resulting force and advances its body.
        if(rearBudgetNewtons is { } input&&(!double.IsFinite(input)||input<0||input>budget))throw new ArgumentException("Rear contact budget must be within the unchanged total budget.");
        var rear=rearBudgetNewtons??p.RearBudgetFraction*budget;var front=budget-rear;var radius=p.WheelRadiusMeters;var inertia=p.WheelInertiaKgMetersSquared;
        var mobility=radius*radius/inertia+1d/mass;
        var speedScaleSquared=forward*forward+p.LowSpeedMetersPerSecond*p.LowSpeedMetersPerSecond;
        var relaxation=rear*mobility*p.MinimumContactRelaxationSeconds;
        var deltaX=Math.Sqrt(p.LongitudinalSlipStiffnessRatio*p.LongitudinalSlipStiffnessRatio*speedScaleSquared+relaxation*relaxation);
        var deltaY=Math.Sqrt(lateralScaleSquared);var b=side/deltaY;
        var bearing=1+seconds*p.BearingResistanceNewtonMeterSeconds/(2*inertia);
        var freeMidOmega=(omega+seconds*driveTorque/(2*inertia))/bearing;
        var freeMidSlip=radius*freeMidOmega-forward-seconds*resistanceForward/(2*mass);
        var halfMobility=seconds/2*(radius*radius/(inertia*bearing)+1d/mass);
        var d=deltaX*Math.Sqrt(1+b*b);var magnitude=Math.Abs(freeMidSlip);var coefficient=halfMobility*rear;
        // Unique monotone scalar root: z + h*A*Br*z/sqrt(d²+z²) = |S|.
        // Starting at the origin tangent approaches the root from below. This
        // is one local constitutive solve, never another body/world rollout.
        var z=magnitude/(1+coefficient/d);var iterations=0;double residual=0;
        for(;iterations<MaximumRootIterations;iterations++){
            var norm=Math.Sqrt(d*d+z*z);residual=z+coefficient*z/norm-magnitude;
            if(Math.Abs(residual)<=RootToleranceMetersPerSecond)break;
            z=Math.Clamp(z-residual/(1+coefficient*d*d/(norm*norm*norm)),0,magnitude);
        }
        var midSlip=Math.CopySign(z,freeMidSlip);
        var a=midSlip/deltaX;var denominator=Math.Sqrt(1+a*a+b*b);
        var fx=(float)(rear*a/denominator);var fyRear=(float)(-rear*b/denominator);
        var fyFront=(float)(-front*side/Math.Sqrt(side*side+lateralScaleSquared));
        // Use the EXACT float force submitted to Box2D also in wheel reaction.
        var midOmega=(omega+seconds*(driveTorque-radius*fx)/(2*inertia))/bearing;
        var after=2*midOmega-omega;
        var actualMidSlip=radius*midOmega-forward-seconds*(fx+resistanceForward)/(2*mass);
        var rootResidual=actualMidSlip-midSlip;
        return new(seconds,omega,after,requestedTorque,availableTorque,driveTorque,p.BearingResistanceNewtonMeterSeconds*midOmega,
            fx,fyFront,fyRear,SlipRatio(forward,omega,p),actualMidSlip,fx*actualMidSlip*seconds,
            seconds*p.BearingResistanceNewtonMeterSeconds*midOmega*midOmega,driveTorque*midOmega*seconds,
            rear==0?0:Math.Sqrt((double)fx*fx+(double)fyRear*fyRear)/rear,rootResidual,iterations);
    }
}
