namespace SharedWorldSpeedway;

/// <summary>One safeguarded Newton solver for the monotone implicit midpoint slip equation.
/// Dynamics contain Iw and h; the material function does not. Box2D still owns the body.</summary>
public static class WheelSolver140
{
    public const int MaximumIterations=48;
    public const double RootToleranceMetersPerSecond=1e-10;
    public static RearWheelStep Evaluate(double forward,double side,double omega,float totalBudget,float mass,
        double requestedTorque,double availableTorque,double driveTorque,double resistanceForward,float seconds,RearWheelParameters p,
        double normalNewtons,double effectiveFriction,TyreMaterial140? material=null){
        p.Validate();material??=TyreMaterial140.Reference;material.Validate();
        if(new[]{forward,side,omega,requestedTorque,availableTorque,driveTorque,resistanceForward,(double)seconds,mass}.Any(x=>!double.IsFinite(x))
            ||seconds is <0 or >.020001f||mass<=0||!float.IsFinite(totalBudget)||totalBudget<0)throw new ArgumentException("Finite bounded midpoint dynamics required.");
        var initial=TyreLaw140.Evaluate(0,side,forward,normalNewtons,effectiveFriction,material);var capacity=initial.CapacityNewtons;
        if(capacity>totalBudget+.001)throw new ArgumentException("Rear capacity exceeds shared total.");
        var radius=p.WheelRadiusMeters;var inertia=p.WheelInertiaKgMetersSquared;var h=(double)seconds;
        var bearing=1+h*p.BearingResistanceNewtonMeterSeconds/(2*inertia);
        var freeMidOmega=(omega+h*driveTorque/(2*inertia))/bearing;
        var freeSlip=radius*freeMidOmega-forward-h*resistanceForward/(2*mass);
        var mobility=h/2*(radius*radius/(inertia*bearing)+1d/mass);
        var scales=TyreLaw140.Scales(forward,material);var b=side/scales.Y;
        var d=scales.X*Math.Sqrt(1+b*b);var magnitude=Math.Abs(freeSlip);var coefficient=mobility*capacity;
        // g(z)=z+c*z/sqrt(d²+z²)-|S|, g'>=1, unique root in [0,|S|].
        var lower=0d;var upper=magnitude;var z=magnitude/(1+coefficient/d);var iterations=0;var residual=0d;
        for(;iterations<MaximumIterations;iterations++){
            var norm=Math.Sqrt(d*d+z*z);residual=z+coefficient*z/norm-magnitude;
            if(Math.Abs(residual)<=RootToleranceMetersPerSecond)break;
            if(residual>0)upper=z;else lower=z;
            var next=z-residual/(1+coefficient*d*d/(norm*norm*norm));
            z=next>lower&&next<upper?next:(lower+upper)/2;
        }
        if(iterations==MaximumIterations)throw new InvalidOperationException("Contact root did not converge; no hidden fallback force.");
        var slip=Math.CopySign(z,freeSlip);var force=TyreLaw140.Evaluate(slip,side,forward,normalNewtons,effectiveFriction,material);
        // Identical float reaction force in the wheel and the submitted Box2D force.
        var fx=(float)force.FxNewtons;var fy=(float)force.FyNewtons;
        var middle=(omega+h*(driveTorque-radius*fx)/(2*inertia))/bearing;
        var after=2*middle-omega;var actualSlip=radius*middle-forward-h*(fx+resistanceForward)/(2*mass);
        var result=new RearWheelStep(h,omega,after,requestedTorque,availableTorque,driveTorque,p.BearingResistanceNewtonMeterSeconds*middle,
            fx,0,fy,RearWheelDynamics.SlipRatio(forward,omega,p),actualSlip,fx*actualSlip*h,
            h*p.BearingResistanceNewtonMeterSeconds*middle*middle,driveTorque*middle*h,
            capacity==0?0:Math.Sqrt((double)fx*fx+(double)fy*fy)/capacity,actualSlip-slip,iterations);
        if(!double.IsFinite(result.AfterOmega)||!double.IsFinite(result.RootResidualMetersPerSecond))throw new InvalidOperationException("Non-finite contact solution.");
        return result;
    }
}
