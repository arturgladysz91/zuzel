namespace SharedWorldSpeedway;
/// <summary>Explicit diagnostic adapter. Capacity is the actual unchanged mu*Fz, not a fitted force.</summary>
public static class Wheel149{
 public static RearWheelStep Evaluate(double forward,double side,double omega,float budget,float mass,double unusedOriginalLateralScaleSquared,double requestedTorque,double availableTorque,double driveTorque,double resistanceForward,float seconds,RearWheelParameters p,double normal,double mu)
  =>WheelSolver140.Evaluate(forward,side,omega,budget,mass,requestedTorque,availableTorque,driveTorque,resistanceForward,seconds,p,normal,mu);
}
