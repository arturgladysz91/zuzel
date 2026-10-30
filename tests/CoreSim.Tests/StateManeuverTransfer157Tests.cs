using System.Text.Json;
using Autonomous146;
using LineThrottle156;
using Transfer157;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public sealed class StateManeuverTransfer157Tests {
 [Fact]public void PlannerRequiresExplicitOptIn(){var f=Study156.Fixtures()[0];Assert.Throws<InvalidOperationException>(()=>new Planner157(f.Spec,false));}
 [Fact]public void CurrentActionCannotLeakThroughPostDecisionPending(){var f=Study156.Fixtures()[0];var x=f.State;var y=x with{Input=x.Input with{Pending=x.Input.Pending.Append(new(x.Input.Time,x.Input.Time+.24,new(.1f,.123f))).ToArray()}};Assert.Equal(Policy157.Features(x),Policy157.Features(y));}
 [Fact]public void RealPriorPendingRemainsAnObservationFeature(){var x=Study156.Fixtures()[0].State;var y=x with{Input=x.Input with{Pending=x.Input.Pending.Select(c=>c with{Action=new(.09f,.1f)}).ToArray()}};Assert.NotEqual(Policy157.Features(x),Policy157.Features(y));}
 [Fact]public void PolicyIsInvariantToAnAbsoluteClockShift(){var x=Study156.Fixtures()[0].State;var y=x with{Input=x.Input with{Time=x.Input.Time+100,NextReview=x.Input.NextReview+100,Pending=x.Input.Pending.Select(c=>c with{ObservedAt=c.ObservedAt+100,ActivationAt=c.ActivationAt+100}).ToArray()}};var a=Policy157.Features(x);var b=Policy157.Features(y);for(int i=0;i<a.Length;i++)Assert.Equal(a[i],b[i],10);var p=new Policy157();foreach(var family in new[]{"A","B","C"})Assert.Equal(p.Suggest(x,family).Action,p.Suggest(y,family).Action);}
 [Fact]public void SavedPassRequiresNewIndependentFullReserves(){int count=0;foreach(var file in Directory.GetFiles(Program157.Doc,"*.json.gz",SearchOption.AllDirectories)){var x=Study156.Read<JsonElement>(file);if(x.ValueKind!=JsonValueKind.Object||!x.TryGetProperty("Review",out var r))continue;Assert.Equal(0,x.GetProperty("K1Calls").GetInt32());if(!x.GetProperty("Pass").GetBoolean())continue;count++;Assert.True(r.GetProperty("Independent").GetBoolean());Assert.Equal(13,r.GetProperty("Tree").GetArrayLength());Assert.Equal(9,r.GetProperty("Terminals").GetArrayLength());Assert.True(x.GetProperty("Counters").GetProperty("IndependentComparisons").GetInt32()>0);}Assert.True(count>=7);}
 [Fact]public void CausalEvidenceSeparatesPendingFromPhysicalInitialState(){var rows=Study156.Read<JsonElement[]>(Program157.Doc+"/causal-v1.json.gz");Assert.Equal(4,rows.Length);foreach(var r in rows){Assert.True(r.GetProperty("StartBodiesIdentical").GetBoolean());Assert.True(r.GetProperty("NewActionsCannotChangeNextObservation").GetBoolean());Assert.True(r.GetProperty("TeacherQueueCounterfactual").GetProperty("Complete").GetBoolean());Assert.True(r.GetProperty("TeacherQueueCounterfactual").GetProperty("Safety").GetProperty("Legal").GetBoolean());}}
}
