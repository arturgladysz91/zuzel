using Autonomous146;
using Continuation161;
using CoreSim;
using LineThrottle156;
using Robust150;
using S3Performance162;
using SharedWorldSpeedway;
using ThreeLap158;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public sealed class S3Performance162Tests {
 static State146 State()=>Program158.Initial(Program161.Spec);
 static Response150 Value()=>new(1,2,3);
 [Fact]public void OffDelegatesUnchanged161Controller(){var c=new Controller162(Program161.Spec);Assert.NotNull(c.Legacy);Assert.Null(c.Optimized);Assert.NotNull(c.Legacy!.Legacy);}
 [Fact]public void IdenticalCopiedStatesUseExactResponseOnce(){var c=new ResponseCache162();int calls=0;var a=State();var b=a with{Input=a.Input with{Pending=a.Input.Pending.ToArray()}};var x=c.Get(a,.1f,.2f,()=>{calls++;return Value();});Assert.Same(x,c.Get(b,.1f,.2f,()=>{calls++;return Value();}));Assert.Equal(1,calls);Assert.Equal(1,c.Statistics.Hits);}
 [Fact]public void WheelCrankSteeringAndPendingTimesCannotAlias(){var c=new ResponseCache162();var a=State();c.Get(a,0,0,Value);var variants=new[]{a with{CrankOmega=1},a with{Input=a.Input with{Omega=1}},a with{Input=a.Input with{Steering=.001f}},a with{Input=a.Input with{Pending=new[]{new FreeRacingCommand(0,.24,new(0,0))}}},a with{Input=a.Input with{Pending=new[]{new FreeRacingCommand(0,.25,new(0,0))}}}};foreach(var v in variants)c.Get(v,0,0,Value);Assert.Equal(6,c.Statistics.Evaluations);Assert.Equal(0,c.Statistics.Hits);}
 [Fact]public void ReviewClearForcesFreshProposalEvaluation(){var c=new ResponseCache162();c.Get(State(),0,0,Value);c.Clear();c.Get(State(),0,0,Value);Assert.Equal(2,c.Statistics.Evaluations);Assert.Equal(0,c.Statistics.Hits);}
 [Fact]public void DisabledMemoAlwaysCallsOriginalResponse(){var c=new ResponseCache162(false);c.Get(State(),0,0,Value);c.Get(State(),0,0,Value);Assert.Equal(2,c.Statistics.Evaluations);Assert.Equal(0,c.Count);}
 [Fact]public void IllegalOrFailedCalculationsAreNeverCached(){var c=new ResponseCache162();Assert.Throws<ArgumentException>(()=>c.Get(State(),0,1.01f,Value));Assert.Throws<InvalidOperationException>(()=>c.Get(State(),0,0,()=>throw new InvalidOperationException("physical failure")));Assert.Equal(0,c.Count);Assert.Equal(Value(),c.Get(State(),0,0,Value));}
 [Theory][InlineData(-1,0)][InlineData(-1,1)][InlineData(1,0)][InlineData(1,1)]
 public void ExactOriginalContactsRemainEqualAtAllCommandCorners(int sign,int gas){var rt=new Runtime158(Program161.Spec,false,Hierarchical155.Program155.Options());var original=new Control161(rt,new(Enabled:true));var optimized=new Control162(rt,new(Enabled:true));float steer=sign*TwoContactBikeParameters.Default.MaximumSteeringRadians;var a=Program161.Fixture("native-end");var x=original.Response(a,steer,gas);Assert.Equal(x,optimized.Response(a,steer,gas));Assert.Equal(x,optimized.Response(a,steer,gas));Assert.Equal(1,optimized.ResponseStatistics.Hits);}
 [Fact]public void OneBitInputChangeCannotUsePreviousProposal(){var c=new ResponseCache162();var a=State();c.Get(a,0,0,Value);c.Get(a,MathF.BitIncrement(0),0,Value);Assert.Equal(2,c.Statistics.Evaluations);}
 [Fact]public void ConsolidatedNativeStatesReproduceOriginalMotionFingerprints(){var folder=Path.Combine(Study156.Root,"docs/experiments/s3-performance/native");var method=typeof(NativeWorld156).GetMethod("Fingerprint",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;foreach(var file in Directory.GetFiles(folder,"*-result.json")){var meta=Study156.Read<System.Text.Json.JsonElement>(file);var label=meta.GetProperty("Label").GetString()!;using var zip=System.IO.Compression.ZipFile.OpenRead(Path.Combine(folder,label+".zip"));using var stream=zip.GetEntry("native.json")!.Open();var run=System.Text.Json.JsonSerializer.Deserialize<Run144>(stream,Study156.Json)!;var recomputed=(string)method.Invoke(null,new object[]{run.Riders})!;Assert.Equal(run.Fingerprint,recomputed);Assert.Equal(meta.GetProperty("Fingerprint").GetString(),recomputed);}}
}
