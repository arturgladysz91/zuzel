using System.Numerics;
using Autonomous146;
using CoreSim;
using Hierarchical155;
using Safety153;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public sealed class HierarchicalManeuvers155Tests {
 static RiderSpec Spec()=>NativeWorld153.Rider(StartingGate.B);
 static State146 State()=>Program153.Read<Run144>(PhysicalSteeringAudit.PreviousPath("../viable-safe-contingency/native/S0-full-baseline-v1.json.gz")).Riders[0].Samples[0] is var p?new(p.Input,p.CrankOmega):throw new InvalidOperationException();
 static Generator155 Generator()=>new(new Runtime153(Spec(),false,Program155.Options()),(_,_,_)=>throw new InvalidOperationException("This test does not propagate"),()=>{},true);
 [Fact] public void ResearchEntryPointAndPlannerAreOffByDefault(){Assert.Throws<ArgumentException>(()=>Program155.Main(Array.Empty<string>()));Assert.Throws<InvalidOperationException>(()=>new Planner155(Spec(),false));Assert.Equal(130,Generator155.MaximumReviews);Assert.False(Program155.Options().RankPolicies);Assert.True(Program155.Options().ValidatedIndependent);Assert.Throws<ArgumentException>(()=>new Planner155(Spec(),false,new(Enabled:true),performance:new()));var enabled=new Planner155(Spec(),false,new(Enabled:true));var counters=System.Text.Json.JsonSerializer.SerializeToElement(enabled.Runtime.Counters(),Program155.Json);Assert.Equal(1,counters.GetProperty("options").GetProperty("Regime").GetInt32());Assert.True(counters.GetProperty("options").GetProperty("ValidatedIndependent").GetBoolean());Assert.Equal(Regime153.SafetyHalfMeter,Program155.Options().Regime);}
 [Fact] public void PhaseUsesActualSpeedAndGeometry(){var g=Generator();var s=State();Assert.Equal(ManeuverPhase155.Launch,g.Phase(s));var frame=NativeWorld153.Frame();var p=frame.At(90,5);var v=s.Input.Rider with{Position=p.Position,Road=p.Road,TotalReferenceMeters=90,Velocity=p.Road.Tangent*15};var bend=s with{Input=s.Input with{Rider=v}};Assert.NotEqual(ManeuverPhase155.Launch,g.Phase(bend));Assert.NotEqual(g.Primitives(s).Length,g.Primitives(bend).Length);}
 [Fact] public void BendVocabularyHasCoupledSteeringAndGasKnots(){var g=Generator();var s=State();var p=NativeWorld153.Frame().At(90,5);var v=s.Input.Rider with{Position=p.Position,Road=p.Road,TotalReferenceMeters=90,Velocity=p.Road.Tangent*15};var a=g.Primitives(s with{Input=s.Input with{Rider=v}});Assert.Contains(a,x=>x.SecondSteeringDegrees is not null&&x.SecondSteeringDegrees!=x.SteeringBiasDegrees);Assert.Contains(a,x=>x.FirstDrive!=x.SecondDrive);Assert.All(a,x=>Assert.Equal(1,x.SwitchReview));}
 [Fact] public void PrimitivePropagationPreservesPendingLatencyAndNeverRunsK1(){var s=State();var rt=new Runtime153(Spec(),false,Program155.Options());rt.Begin(0);var a=rt.Step(s,new(0,0));var b=rt.Step(s,new(.01f,1));Assert.Equal(0,rt.K1Calls);Assert.Equal(a.End.Input.Rider.Position,b.End.Input.Rider.Position);Assert.Equal(a.End.Input.Rider.Velocity,b.End.Input.Rider.Velocity);Assert.NotEqual(a.End.Input.Pending.Last().Action,b.End.Input.Pending.Last().Action);Assert.InRange(a.End.Input.Time,.199999,.200001);Assert.Equal(s.Input.Pending[0],a.End.Input.Pending[0]);}
}
