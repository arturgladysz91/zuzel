using System.Numerics;
using Autonomous146;
using SharedWorldSpeedway;
namespace Speed148;
public static class Maps148{
 public static void Run(){
  using var b=new Budget148("accepted-state-tyre-maps",0,40);var run=Program.Baseline();var frame=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(frame);var p=FreeLearningExperiment.Parameters();var spec=run.Riders[0].Spec;var rows=new List<object>();
  foreach(var at in new[]{28.4,29.4,30,32.8})foreach(var iw in new[]{.45,.9,1.8})foreach(var gas in new[]{0f,.31820688f,1f})foreach(var degrees in new[]{-5d,-2.5,0,2.5,5}){
   b.Guard();var s=run.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-at))!;var v=s.Input.Rider with{Drive=gas};var wheel=new RearWheelState(s.Input.Omega*.33,RearWheelParameters.Default with{WheelInertiaKgMetersSquared=iw});var steer=(float)(degrees*Math.PI/180);var shaft=new CrankClutch144(omega:s.CrankOmega);var forces=Contact144.Forces(v,spec,frame,surface,p,s.Input.Time,147,wheel,new TwoContactBikeState(steer),0,steeringOverride:steer,inertia:52.72097f,powertrain:shaft,releaseAt:.24);
   var speed2=v.Velocity.LengthSquared();var chiDot=(v.Velocity.X*forces.NetForce.Y-v.Velocity.Y*forces.NetForce.X)/(147*speed2);
   rows.Add(new{At=at,WheelInertia=iw,Seconds=0,Throttle=gas,HeldSteeringDegrees=degrees,forces.RearLongitudinal,forces.RearLateral,forces.FrontLateral,forces.NetTorque,YawAcceleration=forces.NetTorque/52.72097f,BetaDot=v.AngularVelocity-chiDot,forces.TyreLoads,Wheel=shaft.Prepared.Wheel,Clutch=shaft.Prepared.ClutchTorque});
  }
  Program.Save(Program.Doc+"/tyre-maps.json",new{Scope="UNCALIBRATED static contact sensitivity on #147 accepted states; held actuator angle is a fixture, not a causal instantaneous command. Zero native worlds; no sensitivity parameter enters autonomous trials.",Rows=rows,CpuSeconds=b.Elapsed});b.Finish("static tyre and yaw maps saved");
 }
}
