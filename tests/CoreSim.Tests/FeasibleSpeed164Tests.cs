using Continuation161;
using FeasibleSpeed164;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public sealed class FeasibleSpeed164Tests {
 [Fact]public void DefaultOffDelegatesToLegacy(){var c=new Controller164(Program161.Spec);Assert.NotNull(c.Legacy);Assert.Null(c.Experimental);}
 [Fact]public void ZeroDistanceCannotRaiseBendSpeed(){Assert.Equal(13,SpeedProfile164.Envelope(13,0,.25,1));}
 [Fact]public void MoreDistancePermitsOnlyStoppingDistanceEnvelope(){double v=SpeedProfile164.Envelope(13,40,.25,1);Assert.Equal(189,v*v,10);Assert.True(v>13);Assert.True(v<SpeedProfile164.Envelope(13,50,.25,1));}
 [Fact]public void NoStabilityReserveCannotRequestStraightBonus(){Assert.Equal(13,SpeedProfile164.Envelope(13,50,.25,0));}
 [Fact]public void RejectsNonfiniteProposalInsteadOfRepairing(){Assert.Throws<ArgumentException>(()=>SpeedProfile164.Envelope(double.NaN,50,.25,1));Assert.Throws<ArgumentException>(()=>SpeedProfile164.Envelope(13,-1,.25,1));}
 [Fact]public void OriginalCompleteBudgetAndRefusalChecksAreUnchanged(){string Block(string name){var s=File.ReadAllText(LineThrottle156.Study156.Root+name);int i=s.IndexOf("    void Guard()",StringComparison.Ordinal),j=s.IndexOf("    public Planner",i,StringComparison.Ordinal);return s[i..j];}Assert.Equal(Block("/tools/trajectory-continuation/Planner161.Generated.cs"),Block("/tools/feasible-speed-profile/Planner164.Generated.cs"));}
 [Fact]public void FutureEntryRemainsPeriodicBeyondFinish(){var rt=new ThreeLap158.Runtime158(Program161.Spec,false,Hierarchical155.Program155.Options());double s=3*rt.Frame.LapMeters;Assert.True(SpeedProfile164.NextEntry(rt.Frame,s)>s);}
}
