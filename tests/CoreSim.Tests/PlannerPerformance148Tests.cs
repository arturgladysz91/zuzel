using System.IO.Compression;
using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
using Speed148;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class PlannerPerformance148Tests{
 static Run144 Saved()=>Speed148.Program.ReadZip<Run144>(PhysicalSteeringAudit.PreviousPath("../efficient-autonomous-planner/native/autonomous147-shortlist.json.gz"));
 static State146 State(Run144 r){var s=r.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-22.6))!;return new(s.Input with{NextReview=s.Input.Time,Pending=s.Input.Pending.Where(c=>c.ObservedAt<s.Input.Time-1e-8).ToArray()},s.CrankOmega);}
 [Fact]public void EntrypointIsExplicitlyOffByDefault()=>Assert.Throws<ArgumentException>(()=>Speed148.Program.Main(Array.Empty<string>()));
 [Fact]public void PredictionForkRetainsEveryAcceptedOmegaBit(){foreach(var w in new[]{0d,Math.BitIncrement(77.3),Math.BitDecrement(124.6),199.45678})Assert.Equal(BitConverter.DoubleToInt64Bits(w),BitConverter.DoubleToInt64Bits(ForecastFork148.Wheel(w).AngularVelocityRadPerSecond));}
 [Fact]public void SharedDelayedPrefixMatchesOriginalFullPrediction(){var r=Saved();var s=State(r);var f=StandingWorld144.Frame();var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(f);var rt=new ExactRuntime148(r.Riders[0].Spec,f,surface,p);rt.Begin(s.Input.Time);
  foreach(var action in new[]{new FreeRacingAction(-.04f,0),new FreeRacingAction(.04f,1),new FreeRacingAction(0,.31820688f)}){var old=Predictor144.Predict(s.Input,r.Riders[0].Spec,f,surface,p,147,52.72097f,.9,action,powertrain:new CrankClutch144(omega:s.CrankOmega));var actual=rt.ForecastForDiagnostics(s,action,.9);Assert.Equal(JsonSerializer.Serialize(old,Speed148.Program.Json),JsonSerializer.Serialize(actual,Speed148.Program.Json));}Assert.Equal(1,rt.SeedSharedPrefixes);Assert.True(rt.SharedPrefixContactQueriesSaved>0);}
 [Fact]public void FreshPreflightBypassesBothPrefixSharingAndPredictionCache(){var r=Saved();var s=State(r);var f=StandingWorld144.Frame();var rt=new ExactRuntime148(r.Riders[0].Spec,f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters());rt.Begin(s.Input.Time);var a=new FreeRacingAction(0,0);
  using(rt.Operation("independent_preflight")){rt.ForecastForDiagnostics(s,a,.9);rt.ForecastForDiagnostics(s,a,.9);}Assert.Equal(2,rt.ActualForecasts);Assert.Equal(0,rt.SeedSharedPrefixes);Assert.Equal(0,rt.PredictionHits);}
 [Fact]public void PrefixAndTailEachConsumeTheirOwnForecastBudget(){var r=Saved();var s=State(r);var f=StandingWorld144.Frame();var rt=new ExactRuntime148(r.Riders[0].Spec,f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters(),new(WorkBudget:true,MaximumActualForecasts:1));rt.Begin(s.Input.Time);Assert.Throws<PlanningBudget146>(()=>rt.ForecastForDiagnostics(s,new(0,1),.9));Assert.Equal(1,rt.ActualForecasts);}
 [Fact]public void ExactBoundaryRetainsOutsideAndRotatedFootprints(){var r=Saved();var f=StandingWorld144.Frame();var old=new RacingLineGeometry(f);var fast=new FastBoundary148(f);var template=r.Riders[0].Samples[300].Input.Rider;
  foreach(var reference in new[]{10d,35,70,100,190,230,290})foreach(var offset in new[]{-3d,-.3,3,14.8,19})foreach(var yaw in new[]{-.7f,0f,.7f}){var at=f.At(reference,offset);var v=template with{Position=at.Position,Road=f.Project(at.Position),Heading=(float)at.Road.Heading+yaw};Assert.Equal(old.Clearance(v),fast.Clearance(v));}Assert.True(fast.FastQueries>0);Assert.True(fast.FallbackQueries>0);}
}
