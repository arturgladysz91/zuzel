using System.Numerics;
using Autonomous146;
using CoreSim.PhysicalSpace;
using Safety153;
using SharedWorldSpeedway;
using Viable154;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public sealed class ViableSafeContingency154Tests {
 static FreeRacingPoint Point(double t,double gap,float speed=20,float yaw=0){var frame=NativeWorld153.Frame();var p=frame.At(5,5);var source=Program153.Read<Run144>(PhysicalSteeringAudit.PreviousPath("../safe-fast-contingency/native/ranked-S-v1.json.gz")).Riders[0].Samples[0].Input.Rider;return new(t,source with{Position=p.Position,Road=p.Road,Velocity=new Vector2(speed,0),AngularVelocity=yaw},0,0,gap,gap);}
 [Fact] public void DefaultOffAndRankingDisabled(){Assert.Throws<ArgumentException>(()=>Program154.Main(Array.Empty<string>()));Assert.Throws<InvalidOperationException>(()=>new Planner154(NativeWorld153.Rider(CoreSim.StartingGate.B),false));Assert.False(Program154.Performance(1).RankPolicies);Assert.True(Program154.Performance(2).StrictSeeds);}
 [Fact] public void BothEndpointConesAreTighterWithoutChangingMotionConstant(){var a=Point(0,.60);var b=Point(.01,.70);var old=Clearance153.SweepLowerBound(a.Rider,b.Rider,.01,(a.Inner,a.Outer),(b.Inner,b.Outer));var tight=Sweep154.Interval(a,b);Assert.Equal(.4995,old,9);Assert.Equal(.5495,tight,9);}
 [Theory][InlineData(0)][InlineData(1)][InlineData(30)]public void NewBoundCannotExceedKnownContinuousLinearClearanceOrLoseYaw(float yaw){var a=Point(0,.61,20,yaw);var b=Point(.01,.67,20,yaw);var lower=Sweep154.Interval(a,b);for(int j=0;j<=100;j++){var t=.01*j/100;var known=.61+6*t;Assert.True(lower<=known);}var old=Clearance153.SweepLowerBound(a.Rider,b.Rider,.01,(a.Inner,a.Outer),(b.Inner,b.Outer));Assert.True(lower>=old-1e-12);}
 [Fact] public void InvalidIntervalsFailClosed(){Assert.Equal(double.NegativeInfinity,Sweep154.Interval(Point(1,1),Point(0,1)));Assert.Equal(double.NegativeInfinity,Sweep154.Interval(Point(0,double.NaN),Point(.01,1)));}
 [Fact] public void HighYawBoundCoversActualRotatingWholeCapsuleGeometry(){var frame=NativeWorld153.Frame();var boundary=new RacingLineGeometry(frame);var pose=frame.At(75,1.8);var source=Point(0,1,0,30).Rider with{Position=pose.Position,Road=pose.Road,Heading=(float)(pose.Road.Heading+1.3)};FreeRacingPoint At(double t){var v=source with{Heading=(float)(source.Heading+30*t)};var g=boundary.Clearance(v);return new(t,v,0,0,g.Inner,g.Outer);}var a=At(0);var b=At(.01);var lower=Sweep154.Interval(a,b);var sampled=Enumerable.Range(0,101).Select(j=>At(.01*j/100)).Min(p=>Math.Min(p.Inner,p.Outer));Assert.True(lower<=sampled);Assert.True(lower>=Clearance153.SweepLowerBound(a.Rider,b.Rider,.01,(a.Inner,a.Outer),(b.Inner,b.Outer))-1e-12);}
 [Fact] public void AGeometryProposalIsNeverASafetyCertificate(){var p=Point(0,.6);var q=Point(.01,.6,20,30);Assert.False(Clearance153.Check(new[]{p,q},Regime153.SafetyHalfMeter).Legal);}
 [Theory][InlineData(false)][InlineData(true)] public void ExactCandidateViewReusesTheOriginalCompleteH09Predictions(bool separated){var run=Program153.Read<Run144>(PhysicalSteeringAudit.PreviousPath("../safe-fast-contingency/native/ranked-S-v1.json.gz"));var s=run.Riders[0].Samples[10];var state=new State146(s.Input,s.CrankOmega);var rt=new Runtime154(run.Riders[0].Spec,separated,Program154.Performance(1));rt.Begin(state.Input.Time);var before=rt.Forecasts;var decision=rt.OriginalSeed(state);var count=rt.Forecasts;foreach(var candidate in decision.Choice.Candidates){var p=rt.CandidateForecast(state,candidate.Action);Assert.True(p.Points.Length>0);}Assert.True(count>before);Assert.Equal(count,rt.Forecasts);}
 [Fact] public void AllModesUseSameSafetyAndFreshIndependentProvider(){foreach(var mode in new[]{0,1,2}){var o=Program154.Performance(mode);Assert.Equal(Regime153.SafetyHalfMeter,o.Regime);Assert.True(o.ValidatedIndependent);Assert.True(o.FastIndependent);Assert.False(o.RankPolicies);}}
}
