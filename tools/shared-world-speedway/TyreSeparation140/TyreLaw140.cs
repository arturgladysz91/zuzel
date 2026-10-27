namespace SharedWorldSpeedway;

/// <summary>UNCALIBRATED phenomenological velocity scales. A frozen #139 reference curve,
/// not measured speedway tyre data. No dynamic inertia, time step or solver settings.</summary>
public sealed record TyreMaterial140(double LongitudinalFloorMetersPerSecond,double LateralFloorMetersPerSecond,
    double LongitudinalRatio=.1,double LateralRatio=.12,double LowSpeedMetersPerSecond=.5)
{
    // Derive ONCE from the declared reference, never from a run's inertia or dt.
    // The original lateral float arithmetic is retained in the reference floor.
    public static TyreMaterial140 Reference {get;}=CreateReference();
    private static TyreMaterial140 CreateReference(){
        const double mu=1.2845000267028808, mass=147, radius=.33, referenceIw=.9;
        var total=mu*mass*9.81;var a=(double).70f;var b=(double).60f;
        var rear=total*a/(a+b);var acceleration=(float)total/(float)mass;
        var lateralSquared=acceleration*acceleration*SlipCoupledDynamics.MinimumLateralRelaxationSeconds*SlipCoupledDynamics.MinimumLateralRelaxationSeconds;
        return new(LongitudinalFloorMetersPerSecond:rear*(radius*radius/referenceIw+1/mass)*.03,
            LateralRatio:SlipCoupledDynamics.SlipStiffnessRatio,LateralFloorMetersPerSecond:Math.Sqrt(lateralSquared));
    }
    public void Validate(){
        if(new[]{LongitudinalRatio,LateralRatio,LowSpeedMetersPerSecond,LongitudinalFloorMetersPerSecond,LateralFloorMetersPerSecond}
            .Any(x=>!double.IsFinite(x)||x<=0))throw new ArgumentException("Finite positive constitutive velocity scales required.");
    }
}

public readonly record struct TyreForce140(double FxNewtons,double FyNewtons,double CapacityNewtons,
    double LongitudinalLossWatts,double LateralLossWatts);

/// <summary>Pure quasi-static combined-slip material response: no Iw, body mass, dt,
/// iteration tolerance or relaxation state. Both forces share ONE mu*Fz budget.</summary>
public static class TyreLaw140
{
    public static (double X,double Y) Scales(double forwardMetersPerSecond,TyreMaterial140 material){
        material.Validate();if(!double.IsFinite(forwardMetersPerSecond))throw new ArgumentException("Finite contact velocity required.");
        var speedSquared=forwardMetersPerSecond*forwardMetersPerSecond+material.LowSpeedMetersPerSecond*material.LowSpeedMetersPerSecond;
        return(Math.Sqrt(material.LongitudinalRatio*material.LongitudinalRatio*speedSquared+Math.Pow(material.LongitudinalFloorMetersPerSecond,2)),
            Math.Sqrt(material.LateralRatio*material.LateralRatio*speedSquared+Math.Pow(material.LateralFloorMetersPerSecond,2)));
    }
    public static TyreForce140 Evaluate(double longitudinalSlipMetersPerSecond,double lateralSlipMetersPerSecond,
        double forwardMetersPerSecond,double normalNewtons,double effectiveFriction,TyreMaterial140? material=null){
        if(new[]{longitudinalSlipMetersPerSecond,lateralSlipMetersPerSecond,forwardMetersPerSecond,normalNewtons,effectiveFriction}.Any(x=>!double.IsFinite(x))
            ||normalNewtons<0||effectiveFriction<0||!double.IsFinite(normalNewtons*effectiveFriction))throw new ArgumentException("Finite slip and nonnegative contact load required.");
        var scale=Scales(forwardMetersPerSecond,material??TyreMaterial140.Reference);var a=longitudinalSlipMetersPerSecond/scale.X;var b=-lateralSlipMetersPerSecond/scale.Y;
        var magnitude=Math.Max(1,Math.Max(Math.Abs(a),Math.Abs(b)));var x=a/magnitude;var y=b/magnitude;
        var norm=Math.Sqrt(1/(magnitude*magnitude)+x*x+y*y);var capacity=normalNewtons*effectiveFriction;
        var fx=capacity*x/norm;var fy=capacity*y/norm;
        return new(fx,fy,capacity,fx*longitudinalSlipMetersPerSecond,-fy*lateralSlipMetersPerSecond);
    }
}
