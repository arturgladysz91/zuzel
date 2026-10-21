using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class NaturalRacingLineTests
{
    [Fact]
    public void FrozenNaturalLineIsPeriodicC3InXyAndHasTheAuthoredApex()
    {
        var p=NaturalRacingLineAudit.Path();var lap=p.Frame.LapMeters;var spacing=p.C3KnotSpacingMeters;
        for(var i=0;i<Math.Round(lap/spacing);i++){
            var s=i*spacing;var a=p.C3Jet(s-1e-7);var b=p.C3Jet(s+1e-7);
            Assert.InRange((a.Position-b.Position).Length,0,1e-6);Assert.InRange((a.First-b.First).Length,0,1e-6);
            Assert.InRange((a.Second-b.Second).Length,0,1e-5);Assert.InRange((a.Third-b.Third).Length,0,1e-4);
            Assert.InRange(Math.Abs(p.CurvatureDerivative(s-1e-7)-p.CurvatureDerivative(s+1e-7)),0,1e-4);
        }
        for(var i=0;i<600;i++){
            var s=(i+.39)*lap/600;const double e=.002;var a=p.At(s-e).Position;var b=p.At(s).Position;var c=p.At(s+e).Position;
            var d=(c-a)*(1/(2*e));var dd=(c-b*2+a)*(1/(e*e));
            Assert.InRange(Math.Abs(MeterPoint.Cross(d,dd)/Math.Pow(d.Length,3)-p.At(s).Curvature),0,.000005);
            Assert.InRange((p.At(s+lap).Position-b).Length,0,1e-9);
        }
        var entry=p.Frame.SegmentStartMeters(1);var exit=p.Frame.SegmentStartMeters(4);
        Assert.InRange(p.At(entry-25).OffsetMeters,7.999,8.001);
        Assert.InRange(p.At(entry+.58*(exit-entry)).OffsetMeters,1.499,1.501);
        Assert.InRange(p.At(exit+20).OffsetMeters,7.999,8.001);
        Assert.True(p.At(entry-3).OffsetFirst<0);Assert.True(p.At(exit).OffsetFirst>0);
    }
    [Fact]
    public void CompleteFootprintFitsBothBoundariesAtEveryYaw()
    {
        var p=NaturalRacingLineAudit.Path();var g=new RacingLineGeometry(p.Frame);
        for(var i=0;i<800;i++){
            var s=(i+.71)*p.Frame.LapMeters/800;var q=p.At(s);
            for(var yaw=0;yaw<8;yaw++){
                var v=new RiderView(1,new((float)q.Position.X,(float)q.Position.Y),Vector2.Zero,(float)(yaw*Math.PI/4),0,p.Frame.RoadAt(s,q.OffsetMeters),s,0,RiderActivity.Racing,1,(float)q.OffsetMeters,0);
                var gap=g.Clearance(v);Assert.True(gap.Inner>=.05);Assert.True(gap.Outer>=.5);
            }
        }
    }
    [Fact]
    public void OrdinaryRollingInitialStateAndNativePrefixMatchBothHistoricalInnerRuns()=>NaturalRacingLineAudit.VerifyReuse();
    [Fact]
    public void NewPathUsesDriveOneAndOriginalControllerWithoutPoseCorrections()
    {
        var p=NaturalRacingLineAudit.Path();var parameters=SpeedGripCrossoverAudit.Parameters(NaturalRacingLineAudit.Engine("power4"));
        Assert.False(parameters.EnableCoupledSlipInverse);Assert.False(parameters.EnableFullContactYawFeedForward);
        var(c,w)=SpeedGripCrossoverAudit.Execute(false,NaturalRacingLineAudit.Engine("power4"),duration:.5,pathOverride:p);
        Assert.Equal(18,c.Initial.Riders.Single().Speed);Assert.Equal(0,c.Initial.Riders.Single().Heading);
        Assert.Equal(18/.33,w[0].Step.BeforeOmega);Assert.Empty(c.ContactPoints);Assert.Empty(c.Result.Decisions);
        Assert.All(c.Ticks,t=>{
            Assert.Equal(1,t.Before.Drive);Assert.InRange(t.Slip.GripUtilization,0,1.000001);
            var dv=t.After.Velocity-t.Before.Velocity;
            Assert.InRange((dv*c.Mechanical[1].MassKg-t.Applied.ForceNewtons*(float)t.DurationSeconds).Length(),0,.006);
        });
        Assert.Throws<ArgumentException>(()=>SpeedGripCrossoverAudit.Execute(false,drive:0,pathOverride:p));
        Assert.Throws<ArgumentException>(()=>SpeedGripCrossoverAudit.Execute(false,coupled:true,pathOverride:p));
    }
    [Theory]
    [InlineData(1,2,25,.58,20)]
    [InlineData(8,1.5,25,1,20)]
    [InlineData(8,1.5,55,.58,20)]
    [InlineData(double.NaN,1.5,25,.58,20)]
    public void InvalidOrOverlappingDefinitionIsRejected(double outside,double apex,double before,double fraction,double after)=>
        Assert.Throws<ArgumentException>(()=>AuthoredRacingPath.Natural(new(outside,apex,before,fraction,after)));
}
