using Continuation161;
using Instrumentation163;
using System.Text.Json;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public sealed class InstrumentationCost163Tests {
 [Fact]public void DefaultOffDelegates161(){var c=new Controller163(Program161.Spec);Assert.NotNull(c.Legacy);Assert.Null(c.Experimental);}
 [Fact]public void AggregateEqualNestedPhaseAvoidsClockReadsAndAllocation(){long t=0;var p=new CpuProfile163(ProfileMode163.Aggregate,()=>t);using(p.Continuation("work")){long reads=p.ClockReads,alloc=p.ScopeAllocations;using(p.Phase("work")){t+=10;}Assert.Equal(reads,p.ClockReads);Assert.Equal(alloc,p.ScopeAllocations);}Assert.Equal(1,p.SamePhaseNoops);}
 [Fact]public void FineRetainsPerStepMeasurements(){long t=0;var p=new CpuProfile163(ProfileMode163.Fine,()=>t);using(p.Continuation("work")){long reads=p.ClockReads;using(p.Phase("work")){t+=10;}Assert.Equal(reads+2,p.ClockReads);}Assert.Equal(0,p.SamePhaseNoops);}
 [Fact]public void DisabledOnlyDisablesMeasurementScopes(){var p=new CpuProfile163(ProfileMode163.Disabled,()=>0);long reads=p.ClockReads;using(p.Phase("work")){}Assert.Equal(reads,p.ClockReads);Assert.Equal(0,p.ScopeAllocations);}
 [Fact]public void AggregateNestedDifferentPhaseStillAccruesAndRestores(){long t=0;var p=new CpuProfile163(ProfileMode163.Aggregate,()=>t);using(p.Continuation("outer")){t=10;using(p.Phase("inner")){t=30;}t=40;}var q=JsonSerializer.SerializeToElement(p.Snapshot());var phases=q.GetProperty("Phases").EnumerateArray().ToDictionary(x=>x.GetProperty("Name").GetString()!,x=>x.GetProperty("CpuSeconds").GetDouble());Assert.Equal(20d/TimeSpan.TicksPerSecond,phases["inner"]);Assert.Equal(20d/TimeSpan.TicksPerSecond,phases["outer"]);}
 [Fact]public void ScopeDisposalIsIdempotent(){long t=0;var p=new CpuProfile163(ProfileMode163.Fine,()=>t);var lease=p.Phase("work");t=10;lease.Dispose();long reads=p.ClockReads;lease.Dispose();Assert.Equal(reads,p.ClockReads);}
 [Fact]public void AllOriginalBudgetChecksRemainByteIdentical(){var root=LineThrottle156.Study156.Root;string Block(string path){var s=File.ReadAllText(root+path);int i=s.IndexOf("    void Guard()",StringComparison.Ordinal),j=s.IndexOf("    public Planner",i,StringComparison.Ordinal);return s[i..j];}Assert.Equal(Block("/tools/trajectory-continuation/Planner161.Generated.cs"),Block("/tools/instrumentation-cost/Planner163.Generated.cs"));}
}
