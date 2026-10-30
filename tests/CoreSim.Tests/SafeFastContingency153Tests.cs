using System.Numerics;
using Autonomous146;
using CoreSim;
using Efficient152;
using Robust150;
using Safety153;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class SafeFastContingency153Tests {
 static Tape150 Tape()=>Program150.Read<Tape150>(PhysicalSteeringAudit.PreviousPath("../robust-autonomous-racing/diagnosis-safe/A-A-original-fixed.json.gz"));
 static RiderSpec Spec()=>StandingWorld144.Rider(StartingGate.B) with{SpeedMetersPerSecond=25,ReferenceMeters=23,OffsetMeters=10};
 static FreeRacingPoint Point(double t,double clearance,float speed=0,float yaw=0){var old=Tape().Points[0];return old with{Time=t,Inner=clearance,Outer=clearance,Rider=old.Rider with{Velocity=new Vector2(speed,0),AngularVelocity=yaw,Heading=0}};}
 [Fact] public void DefaultIsOff(){Assert.Throws<InvalidOperationException>(()=>new Planner153(Spec(),false));Assert.Throws<ArgumentException>(()=>Program153.Main(Array.Empty<string>()));Assert.Throws<InvalidOperationException>(()=>new NativeWorld153());}
 [Fact] public void EndpointHalfMeterDoesNotCertifyHalfMeterDuringMotion(){var p=new[]{Point(0,.55,20),Point(.01,.55,20)};Assert.True(Planner146.Check(p,.5).Legal);var s=Clearance153.Check(p,Regime153.SafetyHalfMeter);Assert.False(s.Legal);Assert.InRange(s.SweepLowerBoundMeters,.449,.451);Assert.Contains("swept",s.Reason);}
 [Fact] public void YawedFootprintSweepCountsEvenWithoutCenterTranslation(){var p=new[]{Point(0,.6,0,30),Point(.01,.6,0,30)};var s=Clearance153.Check(p,Regime153.SafetyHalfMeter);Assert.False(s.Legal);Assert.Equal(.6,s.MinimumGapMeters);Assert.True(s.SweepLowerBoundMeters<.5);}
 [Fact] public void ExactHalfMeterPoseIsAllowedButNumericalTubeRemainsRequiredForIntervals(){Assert.True(Clearance153.Check(new[]{Point(0,.5)},Regime153.SafetyHalfMeter).Legal);Assert.False(Clearance153.Check(new[]{Point(0,.5),Point(.01,.5)},Regime153.SafetyHalfMeter).Legal);Assert.True(Clearance153.Check(new[]{Point(0,.501),Point(.01,.501)},Regime153.SafetyHalfMeter).Legal);}
 [Fact] public void MissingNonfiniteAndReversedIntervalsFailClosed(){Assert.False(Clearance153.Check(Array.Empty<FreeRacingPoint>(),Regime153.SafetyHalfMeter).Legal);Assert.False(Clearance153.Check(new[]{Point(0,double.NaN)},Regime153.SafetyHalfMeter).Legal);Assert.False(Clearance153.Check(new[]{Point(1,1),Point(0,1)},Regime153.SafetyHalfMeter).Legal);}
 [Fact] public void LegacyCertificateRemainsExactlyUnchanged(){var p=Tape().Points;Assert.Equal(Planner146.Check(p,.1),Clearance153.Check(p,Regime153.Legacy));}
 [Fact] public void CenterDistanceCannotReplaceRotatedWholeMechanicalFootprint(){var frame=DiagnosticWorld149.Frame();var p=frame.At(10,.1);var v=Tape().Points[0].Rider with{Position=p.Position,Road=p.Road,Heading=(float)(p.Road.Heading+Math.PI/2)};var gap=new RacingLineGeometry(frame).Clearance(v);Assert.True(v.Road.OffsetMeters+1>.5);Assert.True(gap.Inner<.5);}
 [Theory][InlineData(false)][InlineData(true)] public void IndependentPreparedDagRetainsFrozenBitsAndVetoesCorruptedSearch(bool model){var s=Tape().States[3];var action=new FreeRacingAction(.02f,1);var rt=new Runtime153(Spec(),model,new(ValidatedIndependent:true,FastIndependent:true));rt.Begin(s.Input.Time);var a=rt.Step(s,action);using(rt.Operation("independent_preflight")){var b=rt.Step(s,action);Assert.True(Bits152.Transition(a,b));Assert.NotSame(a.Prediction,b.Prediction);}rt.CorruptSearchForDiagnostics();using(rt.Operation("independent_preflight"))Assert.Throws<InvalidOperationException>(()=>rt.Step(s,action));}
 [Fact] public void PolicyRankingUsesRealCommonGoalTimeBeforeSpeedOrLine(){var fast=new PolicyRank153("fast",100,5,6,15,100,2,.6);var highPeak=new PolicyRank153("long",100,5.1,6,25,110,1,2);Assert.True(fast.CompareTo(highPeak)<0);Assert.Throws<InvalidOperationException>(()=>fast.CompareTo(highPeak with{CommonGoalMeters=101}));}
 [Fact] public void NominalGainCannotHideASlowerWorstReserve(){var incumbent=new PolicyRank153("retained",100,5,5.01,20,100,4,.6);var trap=new PolicyRank153("trap",100,4.66,5.30,22,95,3,.7);Assert.True(trap.CompareTo(incumbent)>0);}
 [Theory][InlineData(false)][InlineData(true)] public void CompleteK1GridScoresAndCommandsMatchOriginal152(bool separated){var state=Tape().States[3];var old=new Runtime152(Spec(),separated);var current=new Runtime153(Spec(),separated);old.Begin(state.Input.Time);current.Begin(state.Input.Time);var a=old.Seed(state).Choice;var b=current.Seed(state).Choice;Assert.Equal(a.Action,b.Action);Assert.Equal(a.Candidates,b.Candidates);}
 [Fact] public void NativeIntervalUsesTheSameVelocityAndYawBoundAsTheIndependentForecast(){var a=Point(0,.7,20,1);var b=Point(.01,.8,21,2);Assert.Equal(Planner146.Check(new[]{a,b},.5).SweepLowerBoundMeters,Clearance153.SweepLowerBound(a.Rider,b.Rider,.01,(a.Inner,a.Outer),(b.Inner,b.Outer)));}
}
