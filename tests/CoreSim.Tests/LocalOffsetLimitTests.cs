using System.Numerics;
using System.Text.Json;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class LocalOffsetLimitTests
{
    [Fact]
    public void LocalEnvelopeUsesTheCompleteFootprintAndAllFrozenGeometries()
    {
        foreach(var variant in TrackGeometryAudit.Shapes()){
            var frame=TrackGeometryAudit.Frame(variant);var audit=new RacingLineGeometry(frame);
            var table=frame.LocalOffsets;var minimum=double.PositiveInfinity;var maximum=0f;
            // Offset cells, joins, width tapers, opposite directions and arbitrary yaw.
            var positions=Enumerable.Range(0,800).Select(i=>(i+.371)*frame.LapMeters/800)
                .Concat(new[]{1,4,5,8}.SelectMany(i=>new[]{-.051,-.001,0,.001,.051}.Select(d=>frame.SegmentStartMeters(i)+d)));
            foreach(var s in positions){
                var cap=frame.MaximumSafeTargetOffsetMeters(s);maximum=Math.Max(maximum,cap);
                var at=frame.At(s,cap);
                foreach(var yaw in new[]{0,.3,-.3,Math.PI/2,-Math.PI/2,Math.PI}){
                    var v=new RiderView(1,at.Position,Vector2.Zero,(float)(at.Road.Heading+yaw),0,at.Road,s,0,RiderActivity.Racing,1,cap,0);
                    var gap=audit.Clearance(v).Outer;minimum=Math.Min(minimum,gap);
                    Assert.True(gap>=.5,$"{variant?.Name??"baseline"} ref={s:R}, yaw={yaw}, cap={cap}, outer={gap:R}");
                }
                Assert.Equal(cap,frame.MaximumSafeTargetOffsetMeters(s+frame.LapMeters));
            }
            Assert.True(maximum>13.8);Assert.True(frame.MaximumSafeTargetOffsetMeters(5)<10);
            Assert.True(minimum>=.5);
        }
    }
    [Fact]
    public void ExperimentalCommandsCanExceed95ButUnsafeTargetsAreRejected()
    {
        var variant=LocalOffsetAudit.Mild();var frame=TrackGeometryAudit.Frame(variant);var rider=SlipDynamicsAudit.Rider(reference:80);
        using(var w=new WorldRunner(new[]{rider with{TargetOffsetMeters=12}},parameters:LocalOffsetAudit.Parameters(),trackShape:variant)){
            var result=w.Run("wide-valid",.01,.1);Assert.Equal(12,result.Riders[0].TargetOffsetMeters);
        }
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{rider with{TargetOffsetMeters=15}},parameters:LocalOffsetAudit.Parameters(),trackShape:variant));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{SlipDynamicsAudit.Rider() with{TargetOffsetMeters=12}},parameters:LocalOffsetAudit.Parameters(),trackShape:variant));
        var plan=new RacingLinePlan("wide-apex",5,5,12,5);
        Assert.Contains(PhysicsDrivenRacingLine.Compile(plan,frame,localOuterLimit:true),c=>c.TargetOffsetMeters==12);
        Assert.Throws<ArgumentException>(()=>PhysicsDrivenRacingLine.Compile(plan,frame));
        using var timed=new WorldRunner(new[]{SlipDynamicsAudit.Rider()},parameters:LocalOffsetAudit.Parameters(),trackShape:variant,
            controls:new[]{new ControlCommand(1,.01,12,1)});
        Assert.Throws<ArgumentException>(()=>timed.Run("invalid-timed-target",.01,.1));
        using var held=new WorldRunner(new[]{SlipDynamicsAudit.Rider(reference:120) with{TargetOffsetMeters=12}},
            parameters:LocalOffsetAudit.Parameters(),trackShape:variant);
        Assert.Throws<ArgumentException>(()=>held.Run("invalid-after-taper",.01,2));
    }
    [Fact]
    public void FullRunSafetyRejectsTheUnsafeOuterPlanWithoutRepairingItsPath()
    {
        var result=LocalOffsetAudit.Measure(LocalOffsetAudit.Mild(),LocalOffsetAudit.Plans().Single(p=>p.Name=="outer-max"));
        Assert.False(result.Evaluation.Feasible);Assert.Contains("OuterClearanceBelow0.5m",result.Evaluation.Rejections);
        Assert.True(result.MaximumActualOffsetMeters>9.5);Assert.True(result.Evaluation.MinimumOuterClearanceMeters<.5);
        Assert.NotEmpty(result.WorstStates);
        var safe=LocalOffsetAudit.Measure(LocalOffsetAudit.Mild(),LocalOffsetAudit.Plans().Single(p=>p.Name=="middle-to-outer"));
        Assert.True(safe.Evaluation.Feasible);Assert.True(safe.ConservativeMinimumOuterClearanceMeters>=.5);
    }
    [Fact]
    public void LocalRulesAreDeterministicAndLegacyBaselineIsUnchanged()
    {
        Assert.False(new WorldParameters().EnableLocalOffsetLimit);
        var baseline=TrackGeometryAudit.Plain(null,TrackGeometryAudit.Plans()[0]);
        Assert.Equal("D233EC7F54B6FCB8070819A9E941E9BB68B0AA806DBDF712814D65E03B37A9CE",baseline.Fingerprint);
        var p=LocalOffsetAudit.Plans().Single(p=>p.Name=="middle-to-outer");
        var a=LocalOffsetAudit.Plain(LocalOffsetAudit.Mild(),p);var b=LocalOffsetAudit.Plain(LocalOffsetAudit.Mild(),p);
        Assert.Equal(a.Fingerprint,b.Fingerprint);Assert.Equal(a.Work.SolverCalls,b.Work.SolverCalls);
        var riders=new[]{SlipDynamicsAudit.Rider(5,0,18,1),SlipDynamicsAudit.Rider(7,10,18,2)};
        RunResult Run(bool reverse){using var w=new WorldRunner(reverse?riders.Reverse():riders,parameters:LocalOffsetAudit.Parameters(),trackShape:LocalOffsetAudit.Mild(),
            spatialControls:new[]{new SpatialControlCommand(1,0,.5f,1,.6f),new(2,10,.5f,1,.6f)});return w.Run("order",.01,2);}
        Assert.Equal(Run(false).Fingerprint,Run(true).Fingerprint);
    }
    [Fact]
    public void CachedLimitDoesNotAllocateAndReplayExportsTheSameLocalGeometry()
    {
        var frame=TrackGeometryAudit.Frame(LocalOffsetAudit.Mild());var table=frame.LocalOffsets;double sum=0;
        for(var i=0;i<10000;i++)sum+=frame.MaximumSafeTargetOffsetMeters(i*.031);
        var bytes=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<10000;i++)sum+=frame.MaximumSafeTargetOffsetMeters(i*.031);
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-bytes);Assert.True(double.IsFinite(sum));
        var geometry=ReplayGeometry.CreateWithLocalLimit(frame);var exported=Assert.IsType<ReplayTargetLimit>(geometry.LocalOuterLimit);
        Assert.Equal(.5,exported.RequiredOuterClearanceMeters);
        foreach(var section in exported.Sections){var p=frame.At(section.ReferenceMeters,section.MaximumSafeTargetOffsetMeters).Position;
            Assert.Equal(p.X,section.Point.X);Assert.Equal(p.Y,section.Point.Y);
            Assert.Equal(table.At(section.ReferenceMeters),section.MaximumSafeTargetOffsetMeters);}
        using var doc=JsonDocument.Parse(ReplayGeometry.Serialize());Assert.False(doc.RootElement.TryGetProperty("localOuterLimit",out _));
    }
}
