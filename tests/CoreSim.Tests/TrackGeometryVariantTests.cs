using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class TrackGeometryVariantTests
{
    [Fact]
    public void FrozenShapesCloseAndHaveConsistentArcLengthProjectionAndNormals()
    {
        foreach(var variant in TrackShapeVariant.All()){
            var frame=TrackGeometryAudit.Frame(variant);var shape=frame.Shape!;
            Assert.InRange(frame.LapMeters,318.77874,318.77876);
            double distance=0;var last=frame.At(0,0).Position;
            for(var i=1;i<=4000;i++){
                var s=i*frame.LapMeters/4000;var p=frame.At(s,0);distance+=Vector2.Distance(last,p.Position);last=p.Position;
                Assert.True(float.IsFinite(p.Position.X)&&float.IsFinite(p.Position.Y));
                Assert.InRange(p.Road.Tangent.LengthSquared(),.999999,1.000001);Assert.InRange(Vector2.Dot(p.Road.Tangent,p.Road.Outward),-1e-7,1e-7);
                Assert.InRange(shape.Width(s),12,16.600001);
                if(i%4!=0)continue;
                foreach(var offset in new[]{-1d,1,5,9.5,15.6}){
                    var q=frame.At(s,offset);var road=frame.Project(q.Position);
                    Assert.InRange(Math.Abs(frame.UnwrapDelta(s%frame.LapMeters,road.ReferenceMeters)),0,.00004);
                    Assert.InRange(Math.Abs(road.OffsetMeters-offset),0,.00002);
                    Assert.InRange(Math.Abs(BikeAngles.Wrap(road.Heading-q.Road.Heading)),0,.000004);
                    Assert.InRange(frame.ReferenceSpeed(road,20),10,21.1);
                }
            }
            Assert.InRange(Math.Abs(distance-frame.LapMeters),0,.001);
            Assert.InRange(Vector2.Distance(frame.At(-.00001,5).Position,frame.At(.00001,5).Position),0,.00003);
        }
    }
    [Fact]
    public void CurveJoinsAreC2AndPhysicalEdgeSagBoundIsConservative()
    {
        foreach(var variant in TrackShapeVariant.All()){
            var f=TrackGeometryAudit.Frame(variant);var shape=f.Shape!;
            if(!variant.Circular)foreach(var i in new[]{1,4,5,8}){
                var s=f.SegmentStartMeters(i);var a=shape.At(s-1e-5);var b=shape.At(s+1e-5);
                Assert.InRange(Math.Abs(BikeAngles.Wrap(a.Heading-b.Heading)),0,1e-8);
                Assert.InRange(Math.Abs(a.Curvature-b.Curvature),0,1e-8);
            }
            // Independent finite differences of the exported physical curves, including tapers.
            MeterPoint Edge(double s,bool outer){var q=shape.At(s);var n=new MeterPoint(Math.Sin(q.Heading),-Math.Cos(q.Heading));return q.Position+n*(outer?shape.Width(s)-1:-1);}
            for(var i=0;i<16000;i++){
                var s=i*f.LapMeters/16000;var q=shape.At(s);
                Assert.InRange(q.Curvature,-1e-8,.1);
                foreach(var outer in new[]{false,true}){
                    var a=Edge(s-.001,outer);var b=Edge(s,outer);var c=Edge(s+.001,outer);
                    var second=(a+c-b*2)*(1e6);
                    if(!variant.Circular||!new[]{1,4,5,8}.Any(j=>Math.Abs(s-f.SegmentStartMeters(j))<.002))Assert.InRange(second.Length,0,2);
                    var left=Edge(s-.025,outer);var right=Edge(s+.025,outer);var delta=right-left;
                    var chord=left+delta*Math.Clamp(MeterPoint.Dot(b-left,delta)/MeterPoint.Dot(delta,delta),0,1);
                    Assert.True((chord-b).Length<=.0007,$"{variant.Name} s={s:R} outer={outer} sag={(chord-b).Length:R}");
                }
            }
        }
    }
    [Fact]
    public void ProjectionIsGlobalAndStableForPointsAwayFromTheRacingTube()
    {
        foreach(var variant in TrackShapeVariant.All()){
            var f=TrackGeometryAudit.Frame(variant);var shape=f.Shape!;
            for(var x=-80;x<=80;x+=8)for(var y=-20;y<=90;y+=10){
                var p=new Vector2(x,y);var road=f.Project(p);var closest=shape.At(road.ReferenceMeters).Position;var dx=closest.X-x;var dy=closest.Y-y;
                var score=dx*dx+dy*dy;var brute=double.PositiveInfinity;
                for(var i=0;i<1500;i++){var q=shape.At(i*f.LapMeters/1500).Position-new MeterPoint(x,y);brute=Math.Min(brute,MeterPoint.Dot(q,q));}
                Assert.True(score<=brute+.00001,$"{variant.Name} ({x},{y}) distance squared {score} > sampled {brute}");
            }
        }
    }
    [Fact]
    public void GeometryHotPathDoesNotAllocateAndLookupDoesNotCreateSolverBoundaries()
    {
        // Measure the complete warmed path on a thread that never runs xUnit work.
        // Keep the zero-byte contract: no tolerance or retry of the measured loop.
        static double Sample(TrackFrame f,Vector2[] points){
            double sum=0;
            for(var i=0;i<5000;i++){var road=f.Project(points[i%100]);sum+=f.RoadAt(road.ReferenceMeters+10,5).Heading;sum+=f.ReferenceSpeed(road,20);}
            return sum;
        }
        Exception? failure=null;
        var worker=new Thread(()=>{
            try{
                foreach(var variant in TrackShapeVariant.All()){
                    var f=TrackGeometryAudit.Frame(variant);var points=Enumerable.Range(0,100).Select(i=>f.At(i*3,5).Position).ToArray();
                    Assert.True(double.IsFinite(Sample(f,points)));
                    var bytes=GC.GetAllocatedBytesForCurrentThread();var sum=Sample(f,points);
                    var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
                    Assert.Equal(0,allocated);Assert.True(double.IsFinite(sum));
                    var start=f.SegmentStartMeters(1);var roadAt=f.RoadAt(start+5,0);
                    Assert.InRange(f.DistanceToCurvatureChange(roadAt),f.SegmentStartMeters(4)-start-5-1e-8,f.SegmentStartMeters(4)-start-5+1e-8);
                }
            }catch(Exception error){failure=error;}
        });
        worker.Start();worker.Join();
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    [Fact]
    public void BaselineGeometryAndWheelScenarioStayExactlyAsIn100()
    {
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ReplayGeometry.Serialize())));
        Assert.Equal("8D3984230CFF4340D0D282BBFE416C115CB10E4B8A05B6FE9C16C627B51617B3",hash);
        var result=TrackGeometryAudit.Plain(null,TrackGeometryAudit.Plans()[0]);
        Assert.Equal("D233EC7F54B6FCB8070819A9E941E9BB68B0AA806DBDF712814D65E03B37A9CE",result.Fingerprint);
        Assert.Equal(4815,result.Work.SolverCalls);Assert.Null(TrackGeometryAudit.Frame(null).Shape);
    }
    [Fact]
    public void NewWorldRepeatsAndReplayUsesItsOwnGeometry()
    {
        var variant=TrackShapeVariant.All().Single(v=>v.Name=="egg-mild");var plan=TrackGeometryAudit.Plans()[0];
        var a=TrackGeometryAudit.Plain(variant,plan);var b=TrackGeometryAudit.Plain(variant,plan);
        Assert.True(a.Completed);Assert.Equal(a.Fingerprint,b.Fingerprint);Assert.Equal(a.Work.SolverCalls,a.Work.RearWheelCommits);
        var frame=TrackGeometryAudit.Frame(variant);var data=ReplayGeometry.Create(frame);Assert.Equal(frame.LapMeters,data.LapMeters);
        Assert.Contains(variant.Name,data.Source);Assert.Equal(9,data.Segments.Length);
        foreach(var segment in data.Segments)foreach(var section in segment.Sections){var q=frame.Shape!.At(section.ReferenceMeters);
            Assert.InRange((q.Position-new MeterPoint(section.Reference.X,section.Reference.Y)).Length,0,1e-10);}
    }
    [Fact]
    public void WholeCapsuleAuditRejectsKerbCrossingAndRetainsSafePose()
    {
        foreach(var variant in TrackShapeVariant.All()){
            var f=TrackGeometryAudit.Frame(variant);var g=new RacingLineGeometry(f);
            RiderView Pose(double offset){var q=f.At(80,offset);return new(1,q.Position,q.Road.Tangent*18,(float)q.Road.Heading,0,q.Road,80,0,RiderActivity.Racing,1,5,0);}
            Assert.True(g.Clearance(Pose(5)).Inner>3);Assert.True(g.Clearance(Pose(-1)).Inner<0);
            Assert.True(g.Clearance(Pose(17)).Outer<0);
        }
    }
    [Fact]
    public void UnsupportedInputsAreRejectedAtTheGeometryBoundary()
    {
        Assert.Throws<ArgumentException>(()=>new ExperimentalTrackShape(new("arbitrary",.9,.9),318.77875));
        var v=TrackShapeVariant.All()[2];
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{SlipDynamicsAudit.Rider()},trackShape:v));
        var f=TrackGeometryAudit.Frame(v);Assert.Throws<ArgumentOutOfRangeException>(()=>f.Project(new(float.NaN,0)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>f.RoadAt(double.PositiveInfinity,5));
    }
}
