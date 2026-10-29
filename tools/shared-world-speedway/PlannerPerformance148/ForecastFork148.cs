namespace Speed148;
using SharedWorldSpeedway;
/// <summary>Exact value copy for an internal prediction fork. Never touches a native body.</summary>
public static class ForecastFork148 {
 public static RearWheelState Wheel(double omega,RearWheelParameters? parameters=null){
  if(!double.IsFinite(omega)||omega<0)throw new ArgumentException("Finite accepted predicted omega required.");
  var wheel=new RearWheelState(0,parameters);wheel.Prepared=default(RearWheelStep) with{AfterOmega=omega};wheel.Commit();return wheel;
 }
}
