using SharedWorldSpeedway;
using Speed148;
namespace Limits149;
public static class Coupling149{
 public static void Run(){
  using var b=new Budget149("clutch-constitutive-separation-and-convergence",0,20);var run=Program149.Read<Run144>(Program149.Doc+"/native/baseline149-final148.json.gz");var at=run.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-30))!;var f=StandingWorld144.Frame();var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(f);var rows=new List<object>();
  foreach(var model in new[]{false,true})foreach(var iw in new[]{.45,.9,1.8})foreach(var drive in new[]{0f,.5f,1})foreach(var h in new[]{0f,.02f,.01f,.005f}){
   b.Guard();var w=ForecastFork148.Wheel(at.Input.Omega,RearWheelParameters.Default with{WheelInertiaKgMetersSquared=iw});var c=new CrankClutch144(omega:at.CrankOmega);var s=new TwoContactBikeState(at.Input.Steering);var v=at.Input.Rider with{Drive=drive};var spec=run.Riders[0].Spec;
   var force=model?Contact149.Forces(v,spec,f,surface,p,30,147,w,s,h,steeringOverride:at.RequestedSteering,inertia:52.72097f,powertrain:c):Contact144.Forces(v,spec,f,surface,p,30,147,w,s,h,steeringOverride:at.RequestedSteering,inertia:52.72097f,powertrain:c);
   rows.Add(new{Model=model?"B":"A",Iw=iw,Throttle=drive,H=h,State=at.Input,CrankOmega=at.CrankOmega,force.RearLongitudinal,force.RearLateral,force.FrontLateral,force.NetTorque,force.TyreLoads,Power=c.Prepared,Wheel=w.Prepared});
  }
  var convergence=new List<object>();foreach(var model in new[]{false,true})foreach(var iw in new[]{.45,.9,1.8})foreach(var h in new[]{.02f,.01f,.005f,.0025f}){
   double u=20,omega=100,energy=0;var n=(int)Math.Round(1/h);for(int j=0;j<n;j++){var step=Audit149.Contact(model,u,0,omega,120,h,iw);var next=u+h*step.LongitudinalNewtons/147;energy+=.5*147*(next*next-u*u)+.5*iw*(step.AfterOmega*step.AfterOmega-omega*omega)+step.LongitudinalLossJoules+step.BearingLossJoules-step.EngineWorkJoules;u=next;omega=step.AfterOmega;}
   convergence.Add(new{Model=model?"B":"A",Iw=iw,H=h,Steps=n,Seconds=n*(double)h,FinalBodySpeed=u,FinalWheelOmega=omega,AccumulatedEnergyIdentityResidual=energy,Scope="1D midpoint effective mass/contact convergence, not a Box2D corner or measured calibration"});
  }
  Program149.Save("clutch-coupling.json.gz",rows);Program149.Save("contact-convergence.json",convergence);b.Finish("unchanged engine/clutch callback audit and dt convergence complete");
 }
}
