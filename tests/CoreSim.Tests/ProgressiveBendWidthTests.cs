using System.Numerics;
using System.Text.Json;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class ProgressiveBendWidthTests
{
    [Fact]
    public void WidthOnlyVariantsKeepEveryReferenceSampleAndHaveC2WidthJoins()
    {
        var original=TrackGeometryAudit.Frame(LocalOffsetAudit.Mild());
        foreach(var v in TrackShapeVariant.ProgressiveWidths()){
            var f=TrackGeometryAudit.Frame(v);var shape=f.Shape!;Assert.Equal(original.LapMeters,f.LapMeters);
            for(var i=0;i<12000;i++){
                var s=i*f.LapMeters/12000;Assert.Equal(original.Shape!.At(s),shape.At(s));
                Assert.InRange(shape.Width(s),12,shape.MaximumPhysicalWidthMeters+1e-10);
                Assert.InRange(Math.Abs((shape.At(s+.001).Curvature-shape.At(s-.001).Curvature)/.002),0,.1);
            }
            foreach(var (start,end,peak) in new[]{(1,4,v.WidthProfile==BendWidthProfile.ProgressiveMotoarena?17:16.6),(5,8,v.WidthProfile==BendWidthProfile.ProgressiveMotoarena?16.2:16.6)}){
                var a=f.SegmentStartMeters(start);var b=f.SegmentStartMeters(end);var m=(a+b)/2;
                Assert.Equal(peak,shape.Width(m),10);Assert.Equal(12,shape.Width(a),10);Assert.Equal(12,shape.Width(b),10);
                foreach(var s in new[]{a,m,b}){
                    var q=shape.ProgressiveWidth(s);Assert.InRange(Math.Abs(q.First),0,1e-14);Assert.InRange(Math.Abs(q.Second),0,1e-14);
                    foreach(var sign in new[]{-1,1}){var near=shape.ProgressiveWidth(s+sign*1e-4);
                        Assert.InRange(Math.Abs(near.Width-q.Width),0,1e-10);Assert.InRange(Math.Abs(near.First),0,1e-9);Assert.InRange(Math.Abs(near.Second),0,1e-6);}
                }
                for(var i=1;i<500;i++)Assert.True(shape.Width(a+i*(m-a)/500)>=shape.Width(a+(i-1)*(m-a)/500));
            }
        }
    }
    [Fact]
    public void WholeXYBoundaryIsRegularContinuousAndDoesNotCrossItself()
    {
        foreach(var v in TrackShapeVariant.ProgressiveWidths()){
            var check=ProgressiveWidthAudit.CheckBoundary(v);
            Assert.Equal(0,check.NonAdjacentIntersections);Assert.Equal(0,check.MaximumReferenceDifference);
            Assert.InRange(check.MinimumDerivativeLength,.99999,1.00001);
            Assert.InRange(check.MaximumSecondDerivative,0,.18); // far below the frozen 2/m chord-error bound
            Assert.InRange(check.MaximumChordSagMeters,0,.0007);
            Assert.InRange(check.MaximumAbsoluteCurvature,0,.026);
            // These are differences across a 4mm neighbourhood, not an analytic discontinuity.
            Assert.InRange(check.MaximumJoinTangentError,0,.00012);Assert.InRange(check.MaximumJoinCurvatureJump,0,.000006);
        }
    }
    [Fact]
    public void LocalEnvelopeCertifiesAllYawAndProjectionAcrossCellEdgesAndBends()
    {
        foreach(var v in TrackShapeVariant.ProgressiveWidths()){
            var f=TrackGeometryAudit.Frame(v);var audit=new RacingLineGeometry(f);var table=f.LocalOffsets;
            for(var i=0;i<1800;i++){
                var s=i*f.LapMeters/1800;var cap=table.At(s);
                foreach(var d in new[]{-.0249,0,.0249}){
                    // Test each offset inside the cap's own cell, including the worst edges.
                    var centre=(Math.Floor(s/table.ReferenceSpacingMeters)+.5)*table.ReferenceSpacingMeters;
                    var at=f.At(centre+d,cap);var road=f.Project(at.Position);
                    Assert.InRange(Math.Abs(f.UnwrapDelta(at.Road.ReferenceMeters,road.ReferenceMeters)),0,.00004);
                    Assert.InRange(Math.Abs(road.OffsetMeters-cap),0,.00003);
                    for(var yaw=0;yaw<12;yaw++){
                        var pose=new RiderView(1,at.Position,Vector2.Zero,(float)(at.Road.Heading+yaw*Math.PI/6),0,at.Road,s,0,RiderActivity.Racing,1,cap,0);
                        Assert.True(audit.Clearance(pose).Outer>=.5,$"{v.Name} s={s} yaw={yaw}");
                    }
                }
            }
        }
    }
    [Fact]
    public void RealDynamicsKeepRejectedMaximalTrajectoriesAndCertifyTightening()
    {
        foreach(var v in TrackShapeVariant.ProgressiveWidths()){
            var maximum=ProgressiveWidthAudit.Measure(v,LocalOffsetAudit.Plans().Single(p=>p.Name=="outer-max"));
            Assert.False(maximum.Measurement.Evaluation.Feasible);Assert.NotNull(maximum.FirstOuterViolation);
            Assert.True(maximum.WorstOuterState.Outer<0);Assert.True(maximum.Measurement.Evaluation.LapSeconds.Length==3);
            var tighten=ProgressiveWidthAudit.Measure(v,LocalOffsetAudit.Plans().Single(p=>p.Name=="outer-tighten"));
            Assert.True(tighten.Measurement.Evaluation.Feasible);Assert.Null(tighten.FirstOuterViolation);
            Assert.True(tighten.Measurement.ConservativeMinimumOuterClearanceMeters>=.5);
            Assert.True(tighten.ConservativeMinimumInnerClearanceMeters>=0);
            Assert.Equal(tighten.Measurement.Evaluation.Fingerprint,LocalOffsetAudit.Plain(v,tighten.Measurement.Plan).Fingerprint);
        }
    }
    [Fact]
    public void ReplayOwnEdgesCapsAndHotPathRetainTheGeometryContract()
    {
        foreach(var v in TrackShapeVariant.ProgressiveWidths()){
            var f=TrackGeometryAudit.Frame(v);var geometry=ReplayGeometry.CreateWithLocalLimit(f);
            foreach(var segment in geometry.Segments)foreach(var section in segment.Sections){
                var expected=ProgressiveWidthAudit.Edge(f.Shape!,section.ReferenceMeters);
                Assert.InRange((new MeterPoint(section.Outer.X,section.Outer.Y)-expected).Length,0,1e-10);
            }
            var table=f.LocalOffsets;double sink=0;for(var i=0;i<10000;i++)sink+=table.At(i*.031);
            var bytes=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<10000;i++)sink+=table.At(i*.031);
            Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-bytes);Assert.True(double.IsFinite(sink));
        }
        // All historical #101 variants still serialize without a new default field.
        Assert.DoesNotContain("WidthProfile",JsonSerializer.Serialize(LocalOffsetAudit.Mild()));
    }
}
