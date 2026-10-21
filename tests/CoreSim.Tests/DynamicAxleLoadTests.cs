using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class DynamicAxleLoadTests
{
    [Fact]
    public void StaticGeometryAccelerationAndBrakingHaveCorrectSignsAndOutOfRangeIsNotClamped()
    {
        var p=TwoContactBikeParameters.Default;var zero=DynamicAxleLoad.AtAcceleration(0,p);
        Assert.InRange(zero.RearFraction,.53846,.53847);Assert.Equal(1,zero.FrontFraction+zero.RearFraction);
        Assert.True(DynamicAxleLoad.AtAcceleration(2,p).RearFraction>zero.RearFraction);
        Assert.True(DynamicAxleLoad.AtAcceleration(-2,p).RearFraction<zero.RearFraction);
        var high=DynamicAxleLoad.AtAcceleration(20,p);var low=DynamicAxleLoad.AtAcceleration(-20,p);
        Assert.False(high.WithinAssumptions);Assert.True(high.FrontFraction<0);Assert.False(low.WithinAssumptions);Assert.True(low.RearFraction<0);
        Assert.Throws<ArgumentException>(()=>DynamicAxleLoad.AtAcceleration(0,p,double.NaN));
    }
    [Fact]
    public void AccelerationUsesOnlyAcceptedDeltaVelocityAndCurrentBodyAxis()
    {
        var path=AuthoredRacingPath.Crossover(false);var position=path.Frame.At(32,1);var before=new RiderView(1,position.Position,new(1,0),0,0,position.Road,32,0,RiderActivity.Racing,1,1,0);
        var after=before with{Velocity=new(1,1),Heading=MathF.PI/2};
        Assert.InRange(DynamicAxleLoad.Accepted(before,after,.5f,TwoContactBikeParameters.Default).LongitudinalAcceleration,1.999999,2.000001);
        Assert.Throws<ArgumentException>(()=>DynamicAxleLoad.Accepted(before,after,0,TwoContactBikeParameters.Default));
    }
    [Fact]
    public void DependenciesAndHeightAreValidatedWhileOffPreservesParameterBytes()
    {
        var p=PhysicalSteeringAudit.Parameters();var json=JsonSerializer.Serialize(p,InverseFeasibilityAudit.Json);
        Assert.DoesNotContain("EnableDynamicAxleLoad",json);Assert.DoesNotContain("AxleLoadHeightMeters",json);
        var path=AuthoredRacingPath.Crossover(false);var rider=RacingLineRootCauseAudit.Rider(path);
        foreach(var invalid in new[]{p with{EnableDynamicAxleLoad=true,EnablePhysicalSteeringControl=false},p with{EnableDynamicAxleLoad=true,EnableTwoContactBikeDynamics=false},
            p with{EnableDynamicAxleLoad=true,AxleLoadHeightMeters=0},p with{AxleLoadHeightMeters=.55}})
            Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{rider},parameters:invalid));
    }
    [Fact]
    public void OneCacheUpdatePerAcceptedStateIsReusedByEveryInverseQueryAndPreservesEnergy()
    {
        var capture=TwoContactBikeAudit.Execute(new("load-cache",Seconds:.05,Drive:true),track:true,parameters:DynamicAxleLoadAudit.Parameters());
        Assert.Null(capture.StopReason);Assert.Equal(capture.Ticks.Length+1,capture.Result.Work.AxleLoadUpdates);
        for(var i=0;i<capture.Ticks.Length;i++){
            var t=capture.Ticks[i];var expected=i==0?DynamicAxleLoad.AtAcceleration(0,TwoContactBikeParameters.Default):DynamicAxleLoad.Accepted(capture.Ticks[i-1].Before,t.Before,capture.Ticks[i-1].Seconds,TwoContactBikeParameters.Default);
            Assert.Equal(expected,t.Forces.AxleLoad);Assert.True(t.Forces.SteeringControl!.ContactQueries>32);
            var rear=t.Forces.Budget*expected.RearFraction;var front=t.Forces.Budget-rear;
            Assert.Equal(t.Forces.Budget,front+rear);Assert.InRange(Math.Abs(t.Forces.FrontLateral),0,front+.001);
            Assert.InRange(Math.Sqrt(t.Forces.RearLateral*t.Forces.RearLateral+t.Forces.RearLongitudinal*t.Forces.RearLongitudinal),0,rear+.001);
        }
        var energy=JsonSerializer.SerializeToElement(TwoContactBikeAudit.Energy(capture));Assert.Equal(0,energy.GetProperty("MaximumExcessBeyondEngineJoules").GetDouble());
        Assert.InRange(Math.Abs(energy.GetProperty("BalanceResidualJoules").GetDouble()),0,.15);
    }
    [Fact]
    public void ExplicitRearBudgetReusesOriginalRootWithoutChangingSharedWheelParameters()
    {
        var p=RearWheelParameters.Default;
        RearWheelStep Solve(double? rear=null)=>RearWheelDynamics.Evaluate(18,-2,60,2138,147,5,100,100,100,-100,.01f,p,rear);
        Assert.Equal(Solve(),Solve(.6*2138));var unloaded=Solve(0);
        Assert.Equal(0,unloaded.LongitudinalNewtons);Assert.Equal(0,unloaded.RearLateralNewtons);Assert.Equal(0,unloaded.RearUtilization);
        Assert.True(double.IsFinite(unloaded.AfterOmega));Assert.Equal(.6,p.RearBudgetFraction);
        Assert.Throws<ArgumentException>(()=>Solve(-1));Assert.Throws<ArgumentException>(()=>Solve(2139));
    }
    [Fact]
    public void OffReplaysSavedPhysicalControllerForcesAndQueryCountsExactly()
    {
        var data=DynamicAxleLoadAudit.ReadSaved("first-bend");var control=DynamicAxleLoadAudit.ReadSaved("first-bend-control");var path=AuthoredRacingPath.Crossover(false);var p=PhysicalSteeringAudit.Parameters();
        var surface=TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot();var spec=RacingLineRootCauseAudit.Rider(path);
        foreach(var i in new[]{0,20,94,200,400,580}){
            var r=data[i];var v=PhysicalSteeringAudit.View(r,path.Frame);
            var f=TwoContactBikeDynamics.Forces(v,spec,path.Frame,surface,p,r["t"],147,new(r["omega_before"]*.33),new((float)r["steer_before"]),(float)r["dt"],path.Guidance(v,p,true),inertia:52.72097f);
            Assert.Null(f.AxleLoad);Assert.Equal((float)r["steer_used"],f.SteeringUsed);Assert.Equal((float)r["front_fx"],f.FrontForce.X);Assert.Equal((float)r["rear_fy"],f.RearForce.Y);
            Assert.Equal((int)control[i]["queries"],f.SteeringControl!.ContactQueries);
        }
    }
}
