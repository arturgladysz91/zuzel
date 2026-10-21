using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class PhysicalSteeringControlTests
{
    private static TwoContactForces Query(Dictionary<string,double> row,bool physical=true,float? heading=null)
    {
        var path=AuthoredRacingPath.Crossover(false);var v=PhysicalSteeringAudit.View(row,path.Frame);if(heading is { } angle)v=v with{Heading=angle};
        var p=TwoContactBikeAudit.Parameters() with{EnablePhysicalSteeringControl=physical};
        return TwoContactBikeDynamics.Forces(v,RacingLineRootCauseAudit.Rider(path),path.Frame,TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot(),p,row["t"],147,
            new(row["omega_before"]*.33),new((float)row["steer_before"]),(float)row["dt"],path.Guidance(v,p,true),inertia:52.72097f);
    }
    [Fact]
    public void DefaultsAndHistoricalParameterBytesStayOffAndInvalidCombinationIsRejected()
    {
        var p=SpeedGripCrossoverAudit.Parameters(null);Assert.False(p.EnablePhysicalSteeringControl);
        Assert.DoesNotContain("EnablePhysicalSteeringControl",JsonSerializer.Serialize(p,InverseFeasibilityAudit.Json));
        var path=AuthoredRacingPath.Crossover(false);
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{RacingLineRootCauseAudit.Rider(path)},parameters:p with{EnablePhysicalSteeringControl=true}));
    }
    [Fact]
    public void HistoricalTwoContactNativeFingerprintIsUnchangedWhenNewControlIsOff()
    {
        using var old=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("B-left.json")));
        var repeat=TwoContactBikeAudit.Execute(new("B-left",Steer:.04f));
        Assert.Equal(old.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString(),repeat.Result.Fingerprint);
    }
    [Fact]
    public void OriginalRecordedContactsStillReplayExactly()
    {
        foreach(var row in PhysicalSteeringAudit.ReadPrevious().Where((_,i)=>i%80==0||i==564)){
            var f=Query(row,false);Assert.Equal((float)row["steer_used"],f.SteeringUsed);
            Assert.Equal((float)row["front_fx"],f.FrontForce.X);Assert.Equal((float)row["front_fy"],f.FrontForce.Y);
            Assert.Equal((float)row["rear_fx"],f.RearForce.X);Assert.Equal((float)row["rear_fy"],f.RearForce.Y);Assert.Null(f.SteeringControl);
        }
    }
    [Fact]
    public void SteeringFixesEarlyCancellationWithoutRequiringBodyHeadingToEqualPathHeading()
    {
        var row=PhysicalSteeringAudit.ReadPrevious().Single(r=>Math.Abs(r["t"]-.2)<1e-7);var old=Query(row,false);var corrected=Query(row);
        Assert.True(corrected.SteeringRequest>old.SteeringRequest);Assert.True(corrected.NetTorque>old.NetTorque);
        var rotated=Query(row,heading:(float)row["heading"]+.1f);
        Assert.Equal(corrected.SteeringControl!.DesiredDirectionRate,rotated.SteeringControl!.DesiredDirectionRate);
        Assert.Equal(corrected.SteeringControl.RequestedTorque,rotated.SteeringControl.RequestedTorque);
        Assert.NotEqual(corrected.SteeringControl.AngleRequestRadians,rotated.SteeringControl.AngleRequestRadians);
    }
    [Fact]
    public void UnreachableTorqueSelectsContactResponsePeakAndNeverAccumulatesError()
    {
        var row=PhysicalSteeringAudit.ReadPrevious().Single(r=>Math.Abs(r["t"]-.94)<1e-7);var f=Query(row);var c=f.SteeringControl!;
        Assert.True(c.OutsideSampledResponse);Assert.True(c.RequestedTorque>c.SampledMaximumTorque);
        Assert.InRange(c.AngleRequestRadians*180/Math.PI,14,20); // full-lock has LESS moment here.
        Assert.True(c.RateLimited);Assert.InRange(Math.Abs(f.SteeringAfter-f.SteeringBefore),0,Math.PI/2*row["dt"]+1e-6);
        for(var i=0;i<3;i++)Assert.Equal(c,Query(row).SteeringControl);
    }
    [Fact]
    public void LocalQueriesLeaveAcceptedStatesUntouchedAndFinalForcesUseOriginalTyres()
    {
        var path=AuthoredRacingPath.Crossover(false);var r=PhysicalSteeringAudit.ReadPrevious()[20];var v=PhysicalSteeringAudit.View(r,path.Frame);
        var surface=TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot();var wheel=new RearWheelState(r["omega_before"]*.33);var before=wheel.AngularVelocityRadPerSecond;var state=new TwoContactBikeState((float)r["steer_before"]);
        var f=TwoContactBikeDynamics.Forces(v,RacingLineRootCauseAudit.Rider(path),path.Frame,surface,PhysicalSteeringAudit.Parameters(),r["t"],147,wheel,state,.01f,path.Guidance(v,PhysicalSteeringAudit.Parameters(),true),inertia:52.72097f);
        Assert.Equal(before,wheel.AngularVelocityRadPerSecond);Assert.Equal((float)r["steer_before"],state.SteeringRadians);
        var held=new RearWheelState(before*.33);var original=TwoContactBikeDynamics.Forces(v,RacingLineRootCauseAudit.Rider(path),path.Frame,surface,TwoContactBikeAudit.Parameters(),r["t"],147,held,new(f.SteeringUsed),.01f,steeringOverride:f.SteeringUsed);
        Assert.Equal(original.FrontForce,f.FrontForce);Assert.Equal(original.RearForce,f.RearForce);Assert.Equal(held.Prepared,wheel.Prepared);
        state.Commit();Assert.Equal(f.SteeringAfter,state.SteeringRadians);
    }
    [Fact]
    public void NativeSteeringGeneratesOnlyContactMomentsWithinUnchangedBudgetsAndEnergy()
    {
        var c=TwoContactBikeAudit.Execute(new("physical-native",Seconds:1.2,Drive:true),track:true,parameters:PhysicalSteeringAudit.Parameters());
        Assert.Null(c.StopReason);Assert.Equal(0,c.LegacyYawCalls);Assert.All(c.Ticks,t=>{
            Assert.InRange(Math.Abs(c.Inertia*(t.After.AngularVelocity-t.Before.AngularVelocity)-t.Forces.NetTorque*t.Seconds),0,.00003);
            Assert.InRange(((t.After.Velocity-t.Before.Velocity)*c.Mass-t.Forces.NetForce*t.Seconds).Length(),0,.001);
            Assert.InRange(Math.Abs(t.Forces.FrontLateral),0,t.Forces.Budget*.400001);
            Assert.InRange(Math.Sqrt(t.Forces.RearLateral*t.Forces.RearLateral+t.Forces.RearLongitudinal*t.Forces.RearLongitudinal),0,t.Forces.Budget*.600001);
            Assert.InRange(Math.Abs(t.Forces.SteeringAfter),0,TwoContactBikeParameters.Default.MaximumSteeringRadians+1e-6);
        });
        var e=JsonSerializer.SerializeToElement(TwoContactBikeAudit.Energy(c));Assert.Equal(0,e.GetProperty("MaximumExcessBeyondEngineJoules").GetDouble());
        Assert.InRange(Math.Abs(e.GetProperty("BalanceResidualJoules").GetDouble()),0,.15);
    }
}
