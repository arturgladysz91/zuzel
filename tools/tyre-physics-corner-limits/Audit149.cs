using SharedWorldSpeedway;
namespace Limits149;
public static class Audit149{
 public const double Mu=1.2845000267028808;public const float Mass=147;
 public static double RearNormal=>Mass*9.81*(double).70f/((double).70f+.60f);
 public static RearWheelStep Contact(bool separated,double u,double side,double omega,double torque,float h,double iw,double load=1){
  var p=RearWheelParameters.Default with{WheelInertiaKgMetersSquared=iw};var total=(float)(Mu*Mass*9.81*load);var normal=RearNormal*load;
  var uf=(float)u;var acceleration=total/Mass;var dy=SlipCoupledDynamics.SlipStiffnessRatio*SlipCoupledDynamics.SlipStiffnessRatio*(uf*uf+.25f)+acceleration*acceleration*SlipCoupledDynamics.MinimumLateralRelaxationSeconds*SlipCoupledDynamics.MinimumLateralRelaxationSeconds;
  return separated?WheelSolver140.Evaluate(u,side,omega,total,Mass,torque,Math.Abs(torque),torque,0,h,p,normal,Mu):RearWheelDynamics.Evaluate(u,side,omega,total,Mass,dy,torque,Math.Abs(torque),torque,0,h,p,normal*Mu);
 }
 public static void Run(){
  using var budget=new Budget149("constitutive-grid",0,30);var rows=new List<object>();double maxEnergy=0,maxUtil=0,maxRoot=0,maxNominalFx=0,maxNominalFy=0;
  foreach(var u in new[]{.1,5,70/3.6,80/3.6,90/3.6,100/3.6})foreach(var slip in new[]{-.5,0,.02,.1,.5,1,3})foreach(var angle in new[]{-60d,-45,-20,-5,0,5,20,45,60})foreach(var iw in new[]{.45,.9,1.8})foreach(var load in new[]{.5,1,1.5}){
   budget.Guard();var omega=Math.Max(0,(u+slip*Math.Sqrt(u*u+.25))/.33);var side=u*Math.Tan(angle*Math.PI/180);
   foreach(var separated in new[]{false,true}){var f=Contact(separated,u,side,omega,0,0,iw,load);maxUtil=Math.Max(maxUtil,f.RearUtilization);rows.Add(new{Model=separated?"B":"A",U=u,SlipRatio=slip,AngleDegrees=angle,Iw=iw,LoadFraction=load,Omega=omega,Rpm=omega*30/Math.PI,Fx=f.LongitudinalNewtons,Fy=f.RearLateralNewtons,f.RearUtilization});}
   if(iw==.9&&load==1){var a=Contact(false,u,side,omega,0,0,iw);var b=Contact(true,u,side,omega,0,0,iw);maxNominalFx=Math.Max(maxNominalFx,Math.Abs(a.LongitudinalNewtons-b.LongitudinalNewtons));maxNominalFy=Math.Max(maxNominalFy,Math.Abs(a.RearLateralNewtons-b.RearLateralNewtons));}
  }
  var dynamic=new List<object>();foreach(var model in new[]{false,true})foreach(var u in new[]{.1,20d,100/3.6})foreach(var angle in new[]{0d,5,20,45,60})foreach(var torque in new[]{0d,120,360})foreach(var iw in new[]{.45,.9,1.8})foreach(var h in new[]{.02f,.01f,.005f,.0025f}){
   var omega=u*1.5/.33;var f=Contact(model,u,u*Math.Tan(angle*Math.PI/180),omega,torque,h,iw);
   var wheelDelta=.5*iw*(f.AfterOmega*f.AfterOmega-omega*omega);var bodyWork=f.LongitudinalNewtons*(u+h*f.LongitudinalNewtons/(2*Mass))*h;
   var residual=wheelDelta+bodyWork+f.LongitudinalLossJoules+f.BearingLossJoules-f.EngineWorkJoules;
   maxEnergy=Math.Max(maxEnergy,Math.Abs(residual));maxRoot=Math.Max(maxRoot,Math.Abs(f.RootResidualMetersPerSecond));maxUtil=Math.Max(maxUtil,f.RearUtilization);
   dynamic.Add(new{Model=model?"B":"A",U=u,AngleDegrees=angle,Torque=torque,Iw=iw,H=h,Wheel=f,EnergyResidual=residual});
  }
  Program149.Save("contact-grid.json.gz",rows);Program149.Save("contact-dynamics.json.gz",dynamic);
  Program149.Save("contact-summary.json",new{StaticRows=rows.Count,DynamicRows=dynamic.Count,MaximumEnergyIdentityResidualJoules=maxEnergy,MaximumRootResidual=maxRoot,MaximumRearUtilization=maxUtil,NominalMaximumFxDifference=maxNominalFx,NominalMaximumFyDifference=maxNominalFy,Material=TyreMaterial140.Reference,Conclusion="Original dx contains Br*(R^2/Iw+1/m)*0.03 even at h=0. This is constitutive inertia coupling, separate from correct finite-step midpoint inertia. B freezes scales at nominal #139 reference; no measured tyre calibration and no claim of improved realism at nominal conditions.",CpuSeconds=budget.Elapsed});budget.Finish("constitutive audit complete");
 }
}
