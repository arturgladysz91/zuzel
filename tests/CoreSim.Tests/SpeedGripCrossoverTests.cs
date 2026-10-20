using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class SpeedGripCrossoverTests
{
    [Fact]
    public void FullXyAndCurvatureDerivativeAreContinuousThroughEveryC3Knot()
    {
        foreach(var wide in new[]{false,true}){
            var p=AuthoredRacingPath.Crossover(wide);var lap=p.Frame.LapMeters;var spacing=p.C3KnotSpacingMeters;
            for(var i=0;i<Math.Round(lap/spacing);i++){
                var s=i*spacing;const double e=1e-7;var l=p.C3Jet(s-e);var r=p.C3Jet(s+e);
                Assert.InRange((l.Position-r.Position).Length,0,1e-6);Assert.InRange((l.First-r.First).Length,0,1e-6);
                Assert.InRange((l.Second-r.Second).Length,0,1e-5);Assert.InRange((l.Third-r.Third).Length,0,1e-4);
                Assert.InRange(Math.Abs(p.CurvatureDerivative(s-e)-p.CurvatureDerivative(s+e)),0,1e-4);
            }
            for(var i=0;i<600;i++){
                var s=(i+.39)*lap/600;const double e=.002;var a=p.At(s-e).Position;var b=p.At(s).Position;var c=p.At(s+e).Position;
                var d=(c-a)*(1/(2*e));var dd=(c-b*2+a)*(1/(e*e));var k=MeterPoint.Cross(d,dd)/Math.Pow(d.Length,3);
                Assert.InRange(Math.Abs(k-p.At(s).Curvature),0,.000005);
                var kp=(p.At(s+e).Curvature-p.At(s-e).Curvature)/(2*e*p.At(s).Metric);
                Assert.InRange(Math.Abs(kp-p.CurvatureDerivative(s)),0,.0001);
                Assert.InRange((p.At(s+lap).Position-b).Length,0,1e-9);
            }
        }
    }
    [Fact]
    public void WideLineOpensAfterTheApexAndBothPathsFitTheCompleteMotorcycle()
    {
        var p=AuthoredRacingPath.Crossover(true);var apex=(p.Frame.SegmentStartMeters(1)+p.Frame.SegmentStartMeters(4))/2;
        for(var s=35d;s<apex-.5;s+=.5)Assert.InRange(p.At(s).OffsetMeters,.999,1.001);
        Assert.InRange(p.At(p.Frame.SegmentStartMeters(4)+15).OffsetMeters,4.999,5.001);
        foreach(var wide in new[]{false,true}){
            var path=AuthoredRacingPath.Crossover(wide);var f=path.Frame;var g=new RacingLineGeometry(f);
            for(var i=0;i<400;i++){
                var s=(i+.71)*f.LapMeters/400;var q=path.At(s);
                for(var yaw=0;yaw<8;yaw++){
                    var v=new RiderView(1,new((float)q.Position.X,(float)q.Position.Y),Vector2.Zero,(float)(yaw*Math.PI/4),0,f.RoadAt(s,q.OffsetMeters),s,0,RiderActivity.Racing,1,(float)q.OffsetMeters,0);
                    var gap=g.Clearance(v);Assert.True(gap.Inner>=.05);Assert.True(gap.Outer>=.5);
                }
            }
        }
    }
    [Fact]
    public void EngineRatioTradesTorqueForShaftSpeedAndRemainsOptIn()
    {
        var path=AuthoredRacingPath.Crossover(false);var setup=RacingLineRootCauseAudit.Rider(path).Setup;
        foreach(var engine in SpeedGripCrossoverAudit.Engines().Where(e=>e is not null).Select(e=>e!))for(var speed=1;speed<70;speed+=3){
            var output=engine.AvailableForce(400,speed,setup)*speed;
            var input=engine.TorqueScale*LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(400,speed*engine.FinalRatio,setup)*(speed*engine.FinalRatio);
            Assert.InRange(Math.Abs(output-input),0,.01);Assert.True(output>=0);
        }
        var json=JsonSerializer.SerializeToElement(LocalOffsetAudit.Parameters(),InverseFeasibilityAudit.Json);
        Assert.False(json.TryGetProperty("ExperimentalDrive",out _));
        Assert.Throws<ArgumentException>(()=>new ExperimentalDriveEnvelope("bad",0,1).Validate());
        var oldPath=new AuthoredRacingPath("inner");
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{RacingLineRootCauseAudit.Rider(oldPath)},parameters:SpeedGripCrossoverAudit.Parameters(new("test",2,.9f)),
            trackShape:RacingLineRootCauseAudit.Variant(),authoredPath:oldPath));
    }
    [Fact]
    public void ExtremeCoastRunsWideAndEveryVelocityIncrementComesFromTheRecordedForce()
    {
        var(c,wheels)=SpeedGripCrossoverAudit.Execute(false,initialSpeed:200/3.6f,drive:0,initialReference:33.75,laps:1,duration:2);
        Assert.Single(c.Initial.Riders);Assert.InRange(Math.Abs(c.Initial.Riders[0].Speed*3.6-200),0,.0001);
        Assert.Empty(c.ContactPoints);Assert.Empty(c.Result.Decisions);Assert.Equal(0,c.Result.Work.TacticalDecisions);
        var g=new RacingLineGeometry(AuthoredRacingPath.Crossover(false).Frame);var first=c.Ticks.First(t=>g.Clearance(t.After).Outer<0);
        Assert.True(first.After.Speed*3.6>180);Assert.True(first.After.Road.OffsetMeters>10);
        foreach(var t in c.Ticks){var m=c.Mechanical[1].MassKg;var acceleration=(t.After.Velocity-t.Before.Velocity)/(float)t.DurationSeconds;
            Assert.InRange((m*acceleration-t.Applied.ForceNewtons).Length(),0,.6);Assert.InRange(t.Slip.GripUtilization,0,1.000001);
        }
        Assert.All(wheels,w=>Assert.Equal(0,w.Step.DriveTorqueNewtonMeters));
        var energy=JsonSerializer.SerializeToElement(RearWheelAudit.Energy(new(c,wheels)),Audit.Json);
        Assert.InRange(Math.Abs(energy.GetProperty("BalanceResidualJoules").GetDouble()),0,5);
        Assert.Equal(0,energy.GetProperty("MaximumYawGuardExcessWatts").GetDouble());
    }
    [Fact]
    public void SlowerConstructivePairIsSafeTracksAndRepeatsWithoutObservers()
    {
        foreach(var wide in new[]{false,true}){
            var(c,w)=SpeedGripCrossoverAudit.Execute(wide,equilibrium:true);var path=AuthoredRacingPath.Crossover(wide);var lap=path.Frame.LapMeters;
            Assert.True(c.Result.Completed);Assert.Empty(c.Result.Decisions);Assert.Empty(c.ContactPoints);
            var g=new RacingLineGeometry(path.Frame);Assert.True(c.Ticks.All(t=>g.Clearance(t.After).Inner>=.05&&g.Clearance(t.After).Outer>=.5));
            var ticks=c.Ticks.Where(t=>t.Before.TotalReferenceMeters>=32+6*lap).ToArray();var duration=ticks.Sum(t=>t.DurationSeconds);
            double Error(SlipControlValidation.Tick t){var q=path.At(t.Before.TotalReferenceMeters);var delta=new MeterPoint(t.Before.Position.X,t.Before.Position.Y)-q.Position;return MeterPoint.Dot(delta,q.Outward);}
            Assert.True(Math.Sqrt(ticks.Sum(t=>Math.Pow(Error(t),2)*t.DurationSeconds)/duration)<=.5);Assert.True(ticks.Max(t=>Math.Abs(Error(t)))<=1);
            if(wide){
                var rider=RacingLineRootCauseAudit.Rider(path) with{ReferenceMeters=32,OffsetMeters=1,TargetOffsetMeters=1};
                using var plain=new WorldRunner(new[]{rider},parameters:SpeedGripCrossoverAudit.Parameters(null),trackShape:RacingLineRootCauseAudit.Variant(),authoredPath:path,
                    diagnosticPathGuidance:true,diagnosticPathPreview:true,diagnosticSteadyStart:InverseFeasibilityAudit.SteadyStart(18),
                    frozenSurface:TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot());
                Assert.Equal(c.Result.Fingerprint,plain.Run("plain",.01,240,8,lapsFromInitialPosition:true,stopOnCrash:true).Fingerprint);
            }
        }
    }
    [Fact]
    public void EvidenceRefusesOverwriteBeforeStartingAWorld()
    {
        var dir=Path.Combine(Path.GetTempPath(),"crossover-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var file=Path.Combine(dir,"guard-accepted.csv");
        try{File.WriteAllText(file,"preserved");Assert.Throws<IOException>(()=>SpeedGripCrossoverAudit.Capture(dir,"guard",false));Assert.Equal("preserved",File.ReadAllText(file));}
        finally{File.Delete(file);Directory.Delete(dir);}
    }
}
