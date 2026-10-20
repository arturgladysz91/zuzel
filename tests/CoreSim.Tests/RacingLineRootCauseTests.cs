using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class RacingLineRootCauseTests
{
    [Fact]
    public void AuthoredPathsAreClosedC2AndTheirFullXYCurvatureMatchesIndependentDifferences()
    {
        foreach(var name in AuthoredRacingPath.Names()){
            var path=new AuthoredRacingPath(name);var lap=path.Frame.LapMeters;
            for(var i=0;i<1600;i++){
                var s=(i+.371)*lap/1600;const double h=.005;var a=path.At(s-h).Position;var b=path.At(s).Position;var c=path.At(s+h).Position;
                var d=(c-a)*(1/(2*h));var dd=(a+c-b*2)*(1/(h*h));var k=MeterPoint.Cross(d,dd)/Math.Pow(d.Length,3);
                Assert.InRange(Math.Abs(k-path.At(s).Curvature),0,.00002);Assert.InRange(Math.Abs(d.Length-path.At(s).Metric),0,.00001);
                Assert.InRange((path.At(s+lap).Position-b).Length,0,1e-10);
            }
            var start=path.Frame.SegmentStartMeters(1);var end=path.Frame.SegmentStartMeters(4);var apex=start+(end-start)*path.ApexFraction;
            foreach(var s in new[]{start,apex,end,end+31,0}){
                var l=path.Offset(s-1e-5);var r=path.Offset(s+1e-5);var centre=path.Offset(s);
                Assert.InRange(Math.Abs(r.D-l.D-2e-5*centre.First),0,1e-8);Assert.InRange(Math.Abs(r.First-l.First-2e-5*centre.Second),0,1e-8);Assert.InRange(Math.Abs(l.Second-r.Second),0,1e-6);
                Assert.InRange(Math.Abs(BikeAngles.Wrap(path.At(s-1e-5).Heading-path.At(s+1e-5).Heading)),0,2e-6);
                Assert.InRange(Math.Abs(path.At(s-1e-5).Curvature-path.At(s+1e-5).Curvature),0,1e-6);
            }
        }
    }
    [Fact]
    public void CompleteFootprintFitsAllAuthoredPathsAtEveryYawAndProjectsBackToTheSameReference()
    {
        foreach(var name in AuthoredRacingPath.Names()){
            var p=new AuthoredRacingPath(name);var f=p.Frame;var g=new RacingLineGeometry(f);
            for(var i=0;i<800;i++){
                var q=p.At((i+.49)*f.LapMeters/800);var position=new Vector2((float)q.Position.X,(float)q.Position.Y);var road=f.Project(position);
                Assert.InRange(Math.Abs(f.UnwrapDelta(q.ReferenceMeters,road.ReferenceMeters)),0,.00004);
                for(var yaw=0;yaw<12;yaw++){
                    var v=new RiderView(1,position,Vector2.Zero,(float)(q.Heading+yaw*Math.PI/6),0,road,q.ReferenceMeters,0,RiderActivity.Racing,1,(float)q.OffsetMeters,0);
                    var gap=g.Clearance(v);Assert.True(gap.Inner>=.05);Assert.True(gap.Outer>=.5);
                }
            }
        }
    }
    [Fact]
    public void PerfectTrackingStillProducesTheWrongExistingDemandBeforeForceSaturation()
    {
        var path=new AuthoredRacingPath("outside-inside-outside");var q=path.At(65);var f=path.Frame;var rider=RacingLineRootCauseAudit.Rider(path);
        var velocity=new Vector2((float)(18*q.Tangent.X),(float)(18*q.Tangent.Y));
        var v=new RiderView(1,new((float)q.Position.X,(float)q.Position.Y),velocity,(float)q.Heading,0,f.RoadAt(65,q.OffsetMeters),65,0,RiderActivity.Racing,1,(float)q.OffsetMeters,0);
        var old=RacingLineRootCauseAudit.ExistingDemand(v,f,LocalOffsetAudit.Parameters());var needed=324*q.Curvature;
        Assert.True(needed<RacingLineRootCauseAudit.AccelerationBudget()*.95);Assert.True(old.Damping< -2);
        Assert.True(Math.Abs(old.Geometry+old.Offset+old.Damping-needed)>3);
        ForceObservation observation=default;
        _=SlipCoupledDynamics.Forces(v,rider,f,TrackState.CreateDefault(f.Track,new(1,0,.5f)).Snapshot(),LocalOffsetAudit.Parameters(),0,
            RacingLineRootCauseAudit.MassKg,50,o=>observation=o,rearWheel:new RearWheelState(v.ForwardSpeed),stepSeconds:.01f);
        Assert.InRange(Math.Abs(observation.DesiredInwardNewtons/RacingLineRootCauseAudit.MassKg-(old.Geometry+old.Offset+old.Damping)),0,.00001);
    }
    [Fact]
    public void DiagnosticPreviewImprovesTheControlledFixtureAndPreservesBudgetsAndDeterminism()
    {
        var old=RacingLineRootCauseAudit.Execute("outside-inside-outside",false).Capture;
        var changed=RacingLineRootCauseAudit.Execute("outside-inside-outside",true,preview:true).Capture;
        static double Rms(SlipControlValidation.Capture c){var p=new AuthoredRacingPath("outside-inside-outside");var a=c.Ticks.Where(t=>t.Before.TotalReferenceMeters>4+p.Frame.LapMeters).ToArray();
            return Math.Sqrt(a.Sum(t=>{var v=t.Before;var q=p.At(v.TotalReferenceMeters);var delta=new MeterPoint(v.Position.X,v.Position.Y)-q.Position;
                var e=MeterPoint.Dot(delta,q.Outward);return e*e*t.DurationSeconds;})/a.Sum(t=>t.DurationSeconds));}
        Assert.True(Rms(changed)<Rms(old));
        Assert.True(changed.Result.Completed);Assert.True(changed.Ticks.All(t=>t.Slip.GripUtilization<=1.00001));
        var path=new AuthoredRacingPath("outside-inside-outside");using var plain=new WorldRunner(new[]{RacingLineRootCauseAudit.Rider(path)},parameters:LocalOffsetAudit.Parameters(),
            trackShape:RacingLineRootCauseAudit.Variant(),authoredPath:path,diagnosticPathGuidance:true,diagnosticPathPreview:true,
            frozenSurface:TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot());
        Assert.Equal(changed.Result.Fingerprint,plain.Run("plain",.01,90,3,lapsFromInitialPosition:true).Fingerprint);
    }
    [Fact]
    public void AuthoredPathBoundaryRejectsUnsupportedWorldsAndRetainsTheHistoricalDefault()
    {
        var path=new AuthoredRacingPath("inner");var r=RacingLineRootCauseAudit.Rider(path);
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{r},authoredPath:path));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{r},parameters:LocalOffsetAudit.Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),diagnosticPathPreview:true));
        var old=LocalOffsetAudit.Plain(RacingLineRootCauseAudit.Variant(),LocalOffsetAudit.Plans().Single(p=>p.Name=="wide-tighten"));
        Assert.Equal("A9D859C3827210005DF91795450BDB91CECA34FC657972548B21E3C5113E5FFD",old.Fingerprint);
        var directory=Path.Combine(Path.GetTempPath(),"root-audit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var file=Path.Combine(directory,"inner-existing-accepted.csv");
        try{
            File.WriteAllText(file,"frozen evidence");
            Assert.Throws<IOException>(()=>RacingLineRootCauseAudit.ExportRun(directory,"inner",false));
            Assert.Equal("frozen evidence",File.ReadAllText(file));
        }finally{File.Delete(file);Directory.Delete(directory);}
    }
}
