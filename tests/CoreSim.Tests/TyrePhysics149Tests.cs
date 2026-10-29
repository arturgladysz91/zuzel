using Limits149;
using SharedWorldSpeedway;
using Autonomous146;
using Speed148;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class TyrePhysics149Tests{
 [Fact]public void EntrypointIsOff()=>Assert.Throws<ArgumentException>(()=>Program149.Main(Array.Empty<string>()));
 [Fact]public void OriginalConstitutiveInertiaEffectPersistsAtZeroStep(){
  var a=Audit149.Contact(false,20,5,100,120,0,.45);var b=Audit149.Contact(false,20,5,100,120,0,1.8);
  Assert.True(Math.Abs(a.LongitudinalNewtons-b.LongitudinalNewtons)>100);Assert.Equal(100,a.AfterOmega);Assert.Equal(100,b.AfterOmega);
 }
 [Fact]public void SeparatedMaterialIgnoresInertiaAndTorqueAtZeroStep(){
  var a=Audit149.Contact(true,20,5,100,0,0,.45);var b=Audit149.Contact(true,20,5,100,360,0,1.8);
  Assert.Equal(a.LongitudinalNewtons,b.LongitudinalNewtons);Assert.Equal(a.RearLateralNewtons,b.RearLateralNewtons);
 }
 [Fact]public void MidpointDynamicsStillRespondToInertia(){var a=Audit149.Contact(true,20,5,100,360,.01f,.45);var b=Audit149.Contact(true,20,5,100,360,.01f,1.8);Assert.NotEqual(a.AfterOmega,b.AfterOmega);}
 [Fact]public void NativeAndForecastUseSameSeparatedClutchCallback(){
  var r=Speed148.Program.ReadZip<Run144>(PhysicalSteeringAudit.PreviousPath("../efficient-autonomous-planner/native/autonomous147-shortlist.json.gz"));var s=r.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-30))!;var f=StandingWorld144.Frame();var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(f);var spec=r.Riders[0].Spec;
  foreach(var drive in new[]{0f,.5f,1})foreach(var iw in new[]{.45,.9,1.8}){
   var v=s.Input.Rider with{Drive=drive};var wp=RearWheelParameters.Default with{WheelInertiaKgMetersSquared=iw};var w=ForecastFork148.Wheel(s.Input.Omega,wp);var z=ForecastFork148.Wheel(s.Input.Omega,wp);var c=new CrankClutch144(omega:s.CrankOmega);var fast=new Speed148.FastCrank148(omega:s.CrankOmega);
   var a=Contact149.Forces(v,spec,f,surface,p,30,147,w,new(s.Input.Steering),.01f,steeringOverride:.04f,inertia:52.72097f,powertrain:c);
   var b=ForecastContact149.Forces(v,spec,f,surface,p,30,147,z,new(s.Input.Steering),.01f,steeringOverride:.04f,inertia:52.72097f,powertrain:fast);
   Assert.Equal(a.NetForce,b.NetForce);Assert.Equal(a.NetTorque,b.NetTorque);Assert.Equal(c.Prepared,fast.Prepared);Assert.Equal(w.Prepared,z.Prepared);
   Assert.Equal(s.Forces!.Value.TyreLoads,a.TyreLoads);Assert.InRange(w.Prepared.RearUtilization,0,1.000001);
  }
 }
 [Fact]public void IndependentPreflightRemainsUncachedOnB(){var r=Speed148.Program.ReadZip<Run144>(PhysicalSteeringAudit.PreviousPath("../efficient-autonomous-planner/native/autonomous147-shortlist.json.gz"));var s=r.Riders[0].Samples[1000];var f=StandingWorld144.Frame();var rt=new ExactRuntime149(r.Riders[0].Spec,f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters());var i=s.Input with{NextReview=s.Input.Time,Pending=s.Input.Pending.Where(c=>c.ObservedAt<s.Input.Time-1e-8).ToArray()};rt.Begin(i.Time);using(rt.Operation("independent_preflight")){rt.ForecastForDiagnostics(new(i,s.CrankOmega),new(0,0),.2);rt.ForecastForDiagnostics(new(i,s.CrankOmega),new(0,0),.2);}Assert.Equal(2,rt.ActualForecasts);Assert.Equal(0,rt.SeedSharedPrefixes);Assert.Equal(0,rt.PredictionHits);}
}
