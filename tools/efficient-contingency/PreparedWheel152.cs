using SharedWorldSpeedway;
namespace Efficient152;
/// <summary>One unchanged A contact equation with torque-independent terms prepared once per accepted frozen contact, never per world or review.</summary>
public sealed class PreparedWheelA152 {
 readonly double forward,side,omega,lateralScaleSquared,resistanceForward,rear,front,radius,inertia,deltaX,b,bearing,d,coefficient,slipRatio;
 readonly float mass,seconds,fyFront; readonly RearWheelParameters p;
 public PreparedWheelA152(double forward,double side,double omega,float budget,float mass,double lateralScaleSquared,double resistanceForward,float seconds,RearWheelParameters p,double? rearBudgetNewtons=null) {
  this.forward=forward;this.side=side;this.omega=omega;this.mass=mass;this.lateralScaleSquared=lateralScaleSquared;this.resistanceForward=resistanceForward;this.seconds=seconds;this.p=p;
  if(rearBudgetNewtons is {} input&&(!double.IsFinite(input)||input<0||input>budget))throw new ArgumentException("Rear contact budget must be within the unchanged total budget.");
  rear=rearBudgetNewtons??p.RearBudgetFraction*budget;front=budget-rear;radius=p.WheelRadiusMeters;inertia=p.WheelInertiaKgMetersSquared;
  var mobility=radius*radius/inertia+1d/mass;
  var speedScaleSquared=forward*forward+p.LowSpeedMetersPerSecond*p.LowSpeedMetersPerSecond;
  var relaxation=rear*mobility*p.MinimumContactRelaxationSeconds;
  deltaX=Math.Sqrt(p.LongitudinalSlipStiffnessRatio*p.LongitudinalSlipStiffnessRatio*speedScaleSquared+relaxation*relaxation);
  var deltaY=Math.Sqrt(lateralScaleSquared);b=side/deltaY;
  bearing=1+seconds*p.BearingResistanceNewtonMeterSeconds/(2*inertia);
  var halfMobility=seconds/2*(radius*radius/(inertia*bearing)+1d/mass);
  d=deltaX*Math.Sqrt(1+b*b);coefficient=halfMobility*rear;
  fyFront=(float)(-front*side/Math.Sqrt(side*side+lateralScaleSquared));slipRatio=RearWheelDynamics.SlipRatio(forward,omega,p);
 }
 public RearWheelStep Evaluate(double requestedTorque,double availableTorque,double driveTorque) {
  var freeMidOmega=(omega+seconds*driveTorque/(2*inertia))/bearing;
  var freeMidSlip=radius*freeMidOmega-forward-seconds*resistanceForward/(2*mass);
  var magnitude=Math.Abs(freeMidSlip);var z=magnitude/(1+coefficient/d);var iterations=0;double residual=0;
  for(;iterations<RearWheelDynamics.MaximumRootIterations;iterations++){
   var norm=Math.Sqrt(d*d+z*z);residual=z+coefficient*z/norm-magnitude;
   if(Math.Abs(residual)<=RearWheelDynamics.RootToleranceMetersPerSecond)break;
   z=Math.Clamp(z-residual/(1+coefficient*d*d/(norm*norm*norm)),0,magnitude);
  }
  var midSlip=Math.CopySign(z,freeMidSlip);var a=midSlip/deltaX;var denominator=Math.Sqrt(1+a*a+b*b);
  var fx=(float)(rear*a/denominator);var fyRear=(float)(-rear*b/denominator);
  var midOmega=(omega+seconds*(driveTorque-radius*fx)/(2*inertia))/bearing;var after=2*midOmega-omega;
  var actualMidSlip=radius*midOmega-forward-seconds*(fx+resistanceForward)/(2*mass);var rootResidual=actualMidSlip-midSlip;
  return new(seconds,omega,after,requestedTorque,availableTorque,driveTorque,p.BearingResistanceNewtonMeterSeconds*midOmega,
   fx,fyFront,fyRear,slipRatio,actualMidSlip,fx*actualMidSlip*seconds,seconds*p.BearingResistanceNewtonMeterSeconds*midOmega*midOmega,
   driveTorque*midOmega*seconds,rear==0?0:Math.Sqrt((double)fx*fx+(double)fyRear*fyRear)/rear,rootResidual,iterations);
 }
}
/// <summary>The frozen B midpoint equation and scaled material arithmetic. Only immutable contact coefficients/validation are hoisted.</summary>
public sealed class PreparedWheelB152 {
 readonly double forward,side,omega,resistanceForward,capacity,radius,inertia,h,bearing,coefficient,d,scaleX,forceB,slipRatio;
 readonly float mass; readonly RearWheelParameters p;
 public PreparedWheelB152(double forward,double side,double omega,float totalBudget,float mass,double resistanceForward,float seconds,RearWheelParameters p,double normal,double mu) {
  p.Validate();var material=TyreMaterial140.Reference;material.Validate();
  if(new[]{forward,side,omega,resistanceForward,(double)seconds,mass}.Any(x=>!double.IsFinite(x))||seconds is <0 or >.020001f||mass<=0||!float.IsFinite(totalBudget)||totalBudget<0)throw new ArgumentException("Finite bounded midpoint dynamics required.");
  this.forward=forward;this.side=side;this.omega=omega;this.resistanceForward=resistanceForward;this.mass=mass;this.p=p;
  var initial=TyreLaw140.Evaluate(0,side,forward,normal,mu,material);capacity=initial.CapacityNewtons;
  if(capacity>totalBudget+.001)throw new ArgumentException("Rear capacity exceeds shared total.");
  radius=p.WheelRadiusMeters;inertia=p.WheelInertiaKgMetersSquared;h=seconds;
  bearing=1+h*p.BearingResistanceNewtonMeterSeconds/(2*inertia);
  var mobility=h/2*(radius*radius/(inertia*bearing)+1d/mass);var scales=TyreLaw140.Scales(forward,material);var b=side/scales.Y;
  d=scales.X*Math.Sqrt(1+b*b);coefficient=mobility*capacity;scaleX=scales.X;forceB=-side/scales.Y;slipRatio=RearWheelDynamics.SlipRatio(forward,omega,p);
 }
 public RearWheelStep Evaluate(double requestedTorque,double availableTorque,double driveTorque) {
  if(!double.IsFinite(requestedTorque)||!double.IsFinite(availableTorque)||!double.IsFinite(driveTorque))throw new ArgumentException("Finite bounded midpoint dynamics required.");
  var freeMidOmega=(omega+h*driveTorque/(2*inertia))/bearing;var freeSlip=radius*freeMidOmega-forward-h*resistanceForward/(2*mass);
  var magnitude=Math.Abs(freeSlip);var lower=0d;var upper=magnitude;var z=magnitude/(1+coefficient/d);var iterations=0;var residual=0d;
  for(;iterations<WheelSolver140.MaximumIterations;iterations++){
   var norm=Math.Sqrt(d*d+z*z);residual=z+coefficient*z/norm-magnitude;
   if(Math.Abs(residual)<=WheelSolver140.RootToleranceMetersPerSecond)break;
   if(residual>0)upper=z;else lower=z;var next=z-residual/(1+coefficient*d*d/(norm*norm*norm));z=next>lower&&next<upper?next:(lower+upper)/2;
  }
  if(iterations==WheelSolver140.MaximumIterations)throw new InvalidOperationException("Contact root did not converge; no hidden fallback force.");
  var slip=Math.CopySign(z,freeSlip);var a=slip/scaleX;var b=forceB;var magnitudeForce=Math.Max(1,Math.Max(Math.Abs(a),Math.Abs(b)));
  var x=a/magnitudeForce;var y=b/magnitudeForce;var normForce=Math.Sqrt(1/(magnitudeForce*magnitudeForce)+x*x+y*y);
  var fx=(float)(capacity*x/normForce);var fy=(float)(capacity*y/normForce);
  var middle=(omega+h*(driveTorque-radius*fx)/(2*inertia))/bearing;var after=2*middle-omega;var actualSlip=radius*middle-forward-h*(fx+resistanceForward)/(2*mass);
  var result=new RearWheelStep(h,omega,after,requestedTorque,availableTorque,driveTorque,p.BearingResistanceNewtonMeterSeconds*middle,
   fx,0,fy,slipRatio,actualSlip,fx*actualSlip*h,h*p.BearingResistanceNewtonMeterSeconds*middle*middle,driveTorque*middle*h,
   capacity==0?0:Math.Sqrt((double)fx*fx+(double)fy*fy)/capacity,actualSlip-slip,iterations);
  if(!double.IsFinite(result.AfterOmega)||!double.IsFinite(result.RootResidualMetersPerSecond))throw new InvalidOperationException("Non-finite contact solution.");return result;
 }
}
