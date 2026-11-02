using Audit159;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public sealed class GeometryAudit159Tests {
 [Fact] public void ResearchHasNoImplicitEntryPoint(){Assert.Throws<ArgumentException>(()=>Program159.Main(Array.Empty<string>()));Assert.Throws<ArgumentException>(()=>Program159.Main(new[]{"--native"}));}
 [Fact] public void WidthCDoesNotReconstructADifferentReferenceCurve(){var b=Program159.Frame("progressive-b");var c=Program159.Frame("progressive-c");Assert.Equal(b.LapMeters,c.LapMeters);for(int i=0;i<638;i++){double s=i*.5;Assert.Equal(b.Shape!.At(s),c.Shape!.At(s));}Assert.Equal(16.6,b.Shape!.ProgressiveWidth((b.SegmentStartMeters(1)+b.SegmentStartMeters(4))/2).Width,9);Assert.Equal(17,c.Shape!.ProgressiveWidth((c.SegmentStartMeters(1)+c.SegmentStartMeters(4))/2).Width,9);Assert.Equal(16.2,c.Shape!.ProgressiveWidth((c.SegmentStartMeters(5)+c.SegmentStartMeters(8))/2).Width,9);}
 [Fact] public void WholeStraightAndBothBendsRetainHistoricalLength(){var f=Program159.Frame("progressive-b");Assert.Equal(318.77874994277954,f.LapMeters,8);Assert.Equal(62,f.SegmentStartMeters(1)+f.LapMeters-f.SegmentStartMeters(8),8);Assert.Equal(62,f.SegmentLengthMeters(4),8);Assert.Equal(f.SegmentStartMeters(4)-f.SegmentStartMeters(1),f.SegmentStartMeters(8)-f.SegmentStartMeters(5),8);Assert.NotEqual(325,f.LapMeters);Assert.NotEqual(318,f.LapMeters);}
 [Theory][InlineData("progressive-b")][InlineData("progressive-c")]
 public void BoundariesAndProjectionStayContinuousAcrossAllJoins(string name){var f=Program159.Frame(name);for(int i=0;i<9;i++){double s=f.SegmentStartMeters(i);var a=f.At(s-.0001,3.5);var b=f.At(s+.0001,3.5);Assert.True(System.Numerics.Vector2.Distance(a.Position,b.Position)<.0003);var road=f.Project(f.At(s+.01,8).Position);Assert.InRange(Math.Abs(f.UnwrapDelta(s+.01,road.ReferenceMeters)),0,.00002);Assert.InRange(Math.Abs(road.OffsetMeters-8),0,.00002);}}
 [Fact] public void CurvatureBoundIsNecessaryAndDoesNotCertifyAController(){var f=Program159.Frame("progressive-b");double k=Enumerable.Range(0,3200).Max(i=>f.Shape!.At(i*.1).Curvature);double r=1/k+3.5;double budget=1852.3389535074234;Assert.InRange(r,27.9,28.1);Assert.True(147*Math.Pow(90/3.6,2)/r>budget);Assert.True(147*Math.Pow(57.6/3.6,2)/r<budget);}
}
