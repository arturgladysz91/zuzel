using System.Text.Json;
using Autonomous146;
using CoreSim;
using Robust150;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class RobustPlanning150Tests{
 static Tape150 Tape()=>Program150.Read<Tape150>(PhysicalSteeringAudit.PreviousPath("../robust-autonomous-racing/diagnosis-safe/A-A-original-fixed.json.gz"));
 static RiderSpec Spec()=>StandingWorld144.Rider(StartingGate.B) with{SpeedMetersPerSecond=25,ReferenceMeters=23,OffsetMeters=10};
 [Fact]public void DefaultsAreOff(){Assert.Throws<ArgumentException>(()=>Program150.Main(Array.Empty<string>()));Assert.Throws<InvalidOperationException>(()=>new Planner150(Spec(),false));}
 [Fact]public void ExactReferenceReturnsItsOwnJointFeedforward(){var tape=Tape();var rt=new Runtime150(Spec(),false,false);rt.Begin(tape.States[2].Input.Time);Assert.Equal(tape.Actions[2],Feedback150.Action(rt,tape.States[2],tape));Assert.Equal(0,rt.FeedbackContactQueries);}
 [Fact]public void FeedbackQueriesNeverCommitAcceptedWheelOrCrank(){var tape=Tape();var state=tape.States[2];var before=JsonSerializer.Serialize(state,Program150.Json);var rt=new Runtime150(Spec(),false,false);rt.Begin(state.Input.Time);var perturbed=state with{Input=state.Input with{Rider=state.Input.Rider with{Heading=state.Input.Rider.Heading+.00002f}}};var action=Feedback150.Action(rt,perturbed,tape);action.Validate();Assert.Equal(before,JsonSerializer.Serialize(state,Program150.Json));Assert.InRange(action.Drive,0,1);Assert.InRange(Math.Abs(action.SteeringRadians),0,TwoContactBikeParameters.Default.MaximumSteeringRadians);}
 [Fact]public void ExactLocalCacheAndIndependentCertificateHaveSeparateWork(){var state=Tape().States[2];var rt=new Runtime150(Spec(),false,false);rt.Begin(state.Input.Time);var a=new FreeRacingAction(0,.31820688f);var x=rt.LocalAlpha(state,a,0);Assert.Equal(x,rt.LocalAlpha(state,a,0));Assert.Equal(1,rt.FeedbackHits);var before=rt.FeedbackContactQueries;using(rt.Operation("independent_preflight")){Assert.Equal(x,rt.LocalAlpha(state,a,0));Assert.Equal(x,rt.LocalAlpha(state,a,0));}Assert.Equal(before+2,rt.FeedbackContactQueries);}
 [Fact]public void NewFeedbackCommandKeepsIrrevocablePendingControls(){var tape=Tape();var state=tape.States[1];var rt=new Runtime150(Spec(),false,false);rt.Begin(state.Input.Time);var action=Feedback150.Action(rt,state,tape);var next=rt.Step(state,action).End.Input;Assert.Contains(next.Pending,c=>c.ObservedAt==state.Input.Time&&Math.Abs(c.ActivationAt-state.Input.Time-.24)<1e-8&&c.Action==action);var active=state.Input.Pending.Where(c=>c.ActivationAt<=next.Time+1e-10).Select(c=>c.Action).LastOrDefault(state.Input.Active);Assert.Equal(active,next.Active);}
 [Fact]public void CalibrationContainsOnlyPriorErrorDeltas(){var names=typeof(ErrorVector150).GetProperties().Select(x=>x.PropertyType).ToArray();Assert.DoesNotContain(typeof(RiderView),names);Assert.DoesNotContain(typeof(State146),names);Assert.DoesNotContain(typeof(FreeRacingAction),names);}
 [Fact]public void ExpandedAnglesCoverOneDegreeWithoutReadingBenchmark(){var tape=Tape();var candidates=Planner150.DiverseActions(tape.States[0],new(.02617994f,.31820688f));Assert.Contains(candidates,c=>Math.Abs(c.SteeringRadians-MathF.PI/180)<1e-7&&c.Drive==.31820688f);}
}
