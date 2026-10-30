using System.Text.Json;
using Autonomous146;
using LineThrottle156;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;
[Trait("Shard","core")]
public sealed class LineThrottlePhysics156Tests {
 [Fact] public void DiagnosticIsOffWithoutAnExplicitResearchCommand(){
  Assert.Throws<ArgumentException>(()=>Study156.Main(Array.Empty<string>()));
 }
 [Fact] public void AllFinalFixturesAreLegalAndBeginBeforeTheirCorner(){
  var frame=StandingWorld144.Frame();var boundary=new RacingLineGeometry(frame);
  foreach(var f in Study156.Fixtures()){
   var gap=boundary.Clearance(f.State.Input.Rider);
   Assert.True(Math.Min(gap.Inner,gap.Outer)>=.5,f.Name);
   Assert.True(f.State.Input.Rider.TotalReferenceMeters<f.Entry-15,f.Name);
   Assert.Equal(.24,RiderController.ReactionSeconds(f.Spec),10);
   Assert.True(f.Goal>f.Exit+62);
  }
 }
 [Fact] public void IrrevocableQueueAndCrankStayPhysicalDuringThePointTwoSecondPrefix(){
  var f=Study156.Fixtures().Single(x=>x.Name=="first");
  var a=Study156.DiagnosticStep(f.State,new(0,0));
  var b=Study156.DiagnosticStep(f.State,new(.03f,1));
  Assert.Equal(a.End.Input.Rider.Position,b.End.Input.Rider.Position);
  Assert.Equal(a.End.Input.Rider.Velocity,b.End.Input.Rider.Velocity);
  Assert.Equal(a.End.CrankOmega,b.End.CrankOmega);
  Assert.Equal(a.End.Input.Omega,b.End.Input.Omega);
  Assert.NotEqual(a.End.Input.Pending.Last().Action,b.End.Input.Pending.Last().Action);
  Assert.Equal(f.State.Input.Time+.24,a.End.Input.Pending.Last().ActivationAt,10);
  Assert.All(a.Prediction.Points,p=>Assert.Equal(1,p.Rider.Drive));
  Assert.InRange(a.End.Input.Time-f.State.Input.Time,.199999,.200001);
 }
 [Fact] public void NativePassRequiresExactFullInitialStateAndContinuousHalfMeterReserve(){
  var root=Study156.Doc+"/native";var paths=Directory.GetFiles(root,"*-result.json");
  Assert.True(paths.Length>=18);
  foreach(var path in paths){var x=Study156.Read<JsonElement>(path);if(!x.GetProperty("Pass").GetBoolean())continue;
   Assert.True(x.GetProperty("Complete").GetBoolean());Assert.True(x.GetProperty("ExactInitialKey").GetBoolean());
   var s=x.GetProperty("Safety");Assert.True(s.GetProperty("Legal").GetBoolean());
   Assert.True(s.GetProperty("MinimumGapMeters").GetDouble()>=.5);
   Assert.True(s.GetProperty("SweepLowerBoundMeters").GetDouble()>=.5);
   Assert.True(s.GetProperty("MaximumBetaDegrees").GetDouble()<=45);
  }
 }
}
