using Autonomous146;
using Continuation161;
using CoreSim;
using LineThrottle156;
using Robust150;
using SharedWorldSpeedway;
using ThreeLap158;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public sealed class TrajectoryContinuation161Tests {
 static RiderSpec Spec=>StandingWorld144.Rider(StartingGate.B);
 static Runtime158 Runtime()=>new(Spec,false,Hierarchical155.Program155.Options());
 [Fact]public void OffDelegatesOriginalControllerWithoutNewPolicy(){var c=new Controller161(Spec);Assert.NotNull(c.Legacy);Assert.Null(c.Dynamic);Assert.NotNull(c.Legacy!.Legacy);Assert.False(new Settings161().Enabled);Assert.Throws<InvalidOperationException>(()=>new Planner161(Spec,false));}
 [Theory][InlineData(0)][InlineData(.00001)][InlineData(.5)][InlineData(.99999)][InlineData(1)]
 public void GasDifferencesRemainLegalAndUseRealDenominator(double value){var p=Difference161.OneSided(value,.08,0,1);Assert.InRange(p.Probe,0,1);Assert.Equal(p.Probe-value,p.Step);Assert.True(Math.Abs(p.Step)>1e-9);new FreeRacingAction(0,(float)p.Probe).Validate();}
 [Theory][InlineData(-1)][InlineData(0)][InlineData(1)]
 public void SteeringBracketsAreLegalIncludingBothSaturatedEdges(int sign){float max=TwoContactBikeParameters.Default.MaximumSteeringRadians,value=sign*max;var p=Difference161.Bracket(value,.005f,-max,max);new FreeRacingAction(p.Low,0).Validate();new FreeRacingAction(p.High,1).Validate();Assert.Equal((double)p.High-p.Low,p.Span);Assert.True(p.Span>0);var q=Difference161.OneSided(value,.015,-max,max);new FreeRacingAction((float)q.Probe,1).Validate();}
 [Fact]public void NarrowThrottleIntervalUsesAvailableEndpoint(){var p=Difference161.OneSided(0,.08,0,.01);Assert.True(p.Probe>0);Assert.InRange(p.Probe,0,.011);}
 [Fact]public void InvalidDerivativeInputsAreNotSilentlyClamped(){Assert.Throws<ArgumentException>(()=>Difference161.OneSided(double.NaN,.01,0,1));Assert.Throws<ArgumentException>(()=>Difference161.OneSided(1.1,.01,0,1));Assert.Throws<ArgumentException>(()=>Difference161.OneSided(.5,.01,1,1));}
 [Fact]public void GeometryPreviewContinuesAcrossThirdFinish(){var f=StandingWorld144.Frame();foreach(double s in new[]{0d,925,953,956,976,988}){double end=Control161.ContinuationPreviewEnd(f,s);Assert.True(end>s+35);Assert.True(end>f.SegmentStartMeters(4));}Assert.True(Control161.ContinuationPreviewEnd(f,976)>3*f.LapMeters+f.SegmentStartMeters(4));}
 [Fact]public void ActualForceAdapterRejectsIllegalCommandsAndPreservesObservation(){var s=Program158.Initial(Spec);var c=new Control161(Runtime(),new(Enabled:true,Adaptive:true));float max=TwoContactBikeParameters.Default.MaximumSteeringRadians;Assert.Throws<ArgumentException>(()=>c.Response(s,max+.005f,0));Assert.Throws<ArgumentException>(()=>c.Response(s,0,1.01f));Assert.Throws<ArgumentException>(()=>c.Predict(s,new(0,-.01f),.64));Assert.Equal(0,s.Input.Steering);Assert.Empty(s.Input.Pending);}
 [Fact]public void BetaNearZeroDoesNotHideInwardCourseVelocity(){var s=Program161.Fixture("native-end");var q=Space161.Observe(s,StandingWorld144.Frame());Assert.InRange(Math.Abs(q.Beta),0,.001);Assert.True(q.LateralVelocity< -1);Assert.True(q.ProjectedOffset<q.Offset);Assert.Equal(1,q.PendingCount);Assert.Equal(67,s.Input.Time);}
 [Fact]public void EveryPendingActivationIsRetained(){var s=Program161.Fixture("native-end");var c=new Control161(Runtime(),new(Enabled:true,Adaptive:true));var q=c.ActivationState(s);Assert.Single(s.Input.Pending);Assert.Equal(67.04,s.Input.Pending[0].ActivationAt,8);Assert.Equal(67.24,q.Input.Time,8);Assert.Equal(s.Input.Pending[0].Action,q.Input.Active);}
 [Fact]public void FeedbackAtActuatorLimitsUsesLegalOriginalProbes(){float max=TwoContactBikeParameters.Default.MaximumSteeringRadians;foreach(float steering in new[]{-max,max}){var z=Program158.Initial(Spec);var s=z with{Input=z.Input with{Time=1,NextReview=1,Omega=10/.33,Steering=steering,Active=new(steering,.5f),Rider=z.Input.Rider with{Velocity=new(10,0),Drive=.5f}}};var changed=s with{Input=s.Input with{Rider=s.Input.Rider with{Heading=.001f}}};var tape=new Tape150(1,100,200,new[]{s.Input.Active},new[]{s},Array.Empty<FreeRacingPoint>(),null,new(false,0,0,0,0,"proposal"),"unit");var rt=Runtime();var c=new Control161(rt,new(Enabled:true,Adaptive:true));Feedback161.Action(rt,changed,tape,c).Validate();}}
 [Fact]public void RegressedV5ProbeIsOutsideOriginalRangeButNewDifferenceIsInside(){var p=Study156.Read<System.Text.Json.JsonElement>(Program161.Doc+"/../dynamic-slip-preview/certificates/terminal-cause-v5.json.gz").GetProperty("LastProbe").GetProperty("Action");Assert.True(p.GetProperty("SteeringRadians").GetDouble()>TwoContactBikeParameters.Default.MaximumSteeringRadians);var d=Difference161.OneSided(TwoContactBikeParameters.Default.MaximumSteeringRadians,.015,-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians);new FreeRacingAction((float)d.Probe,1).Validate();}
 [Fact]public void CertificateSourceChangesOnlyProposalHooks(){var s=File.ReadAllText(Study156.Root+"/tools/dynamic-slip-preview/Planner160.Generated.cs").Replace("namespace Dynamic160;","namespace Continuation161;");foreach(string n in new[]{"Planner","Generator","Settings","Feedback"})s=s.Replace(n+"160",n+"161");s=s.Replace("STUDY160","STUDY161");Assert.Equal(s,File.ReadAllText(Study156.Root+"/tools/trajectory-continuation/Planner161.Generated.cs"));}
 [Fact]public void ActualContactCornersRemainFiniteWithoutChangingObservedActuator(){var state=Program161.Fixture("native-end");var c=new Control161(Runtime(),new(Enabled:true,Adaptive:true));float max=TwoContactBikeParameters.Default.MaximumSteeringRadians;foreach(float steer in new[]{-max,max})foreach(float gas in new[]{0f,1f}){var q=c.Response(state,steer,gas);Assert.True(double.IsFinite(q.YawAcceleration)&&double.IsFinite(q.SlipRate)&&double.IsFinite(q.Slip));}Assert.Equal(-.00013205086f,state.Input.Steering);}

}
