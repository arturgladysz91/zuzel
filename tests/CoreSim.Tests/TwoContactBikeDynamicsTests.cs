using System.Numerics;
using System.Text.Json;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class TwoContactBikeDynamicsTests
{
    private static JsonElement Energy(TwoContactBikeAudit.Capture c)=>JsonSerializer.SerializeToElement(TwoContactBikeAudit.Energy(c));
    [Fact]
    public void OffRetainsHistoricalInitialStatesAndNativeForcePrefixes()
    {
        Assert.False(new WorldParameters().EnableTwoContactBikeDynamics);
        var json=JsonSerializer.SerializeToElement(SpeedGripCrossoverAudit.Parameters(null),InverseFeasibilityAudit.Json);
        Assert.False(json.TryGetProperty("EnableTwoContactBikeDynamics",out _));Assert.False(json.TryGetProperty("TwoContactBike",out _));
        _=NaturalRacingLineAudit.VerifyReuse();
    }
    [Fact]
    public void FeatureRequiresOriginalWheelAndExcludesLegacyYawInverseVariants()
    {
        var p=AuthoredRacingPath.Crossover(false);var rider=RacingLineRootCauseAudit.Rider(p);
        foreach(var settings in new[]{TwoContactBikeAudit.Parameters() with{EnableRearWheelDynamics=false},
            TwoContactBikeAudit.Parameters() with{EnableCoupledSlipInverse=true},TwoContactBikeAudit.Parameters() with{EnableFullContactYawFeedForward=true},
            SpeedGripCrossoverAudit.Parameters(null) with{TwoContactBike=new()}})
            Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{rider},parameters:settings,trackShape:RacingLineRootCauseAudit.Variant()));
        Assert.Throws<ArgumentException>(()=>(new TwoContactBikeParameters{FrontAxleFromComMeters=1}).Validate(RearWheelParameters.Default));
        Assert.Throws<ArgumentException>(()=>(new TwoContactBikeParameters{SteeringRateRadiansPerSecond=float.NaN}).Validate(RearWheelParameters.Default));
        using var old=new WorldRunner(new[]{rider},parameters:SpeedGripCrossoverAudit.Parameters(null),trackShape:RacingLineRootCauseAudit.Variant());
        using var current=new WorldRunner(new[]{rider},parameters:TwoContactBikeAudit.Parameters(),trackShape:RacingLineRootCauseAudit.Variant());
        Assert.Equal(old.MechanicalProperties(rider.Profile.Id),current.MechanicalProperties(rider.Profile.Id));
        Assert.Throws<ArgumentException>(()=>SlipCoupledDynamics.Forces(current.Snapshot.Riders.Single(),rider,current.Frame,
            CoreSim.TrackState.CreateDefault(current.Frame.Track).Snapshot(),TwoContactBikeAudit.Parameters(),0,147,52.72097f,rearWheel:new(18),stepSeconds:.01f));
    }
    [Fact]
    public void StraightWithNoSteeringDoesNotTurnWithOrWithoutPower()
    {
        foreach(var drive in new[]{false,true}){
            var c=TwoContactBikeAudit.Execute(new("straight",Drive:drive));Assert.Null(c.StopReason);Assert.Equal(0,c.LegacyYawCalls);
            Assert.All(c.Ticks,t=>{Assert.Equal(0,t.After.AngularVelocity);Assert.Equal(0,t.After.Heading);Assert.Equal(0,t.Forces.NetTorque);});
            Assert.Equal(0,Energy(c).GetProperty("MaximumExcessBeyondEngineJoules").GetDouble());
        }
    }
    [Fact]
    public void AxleVelocitiesIncludeBodySpinAndSmallSteeringInitiatesBothTurnSigns()
    {
        Assert.Equal(new Vector2(18,1.4f),TwoContactForces.PointVelocity(new(18,0),2,new(.7f,0)));
        Assert.Equal(new Vector2(18,-1.2f),TwoContactForces.PointVelocity(new(18,0),2,new(-.6f,0)));
        foreach(var sign in new[]{1,-1}){
            var c=TwoContactBikeAudit.Execute(new("turn",Steer:sign*.04f));Assert.Null(c.StopReason);Assert.Equal(0,c.LegacyYawCalls);
            Assert.True(c.Ticks[0].Forces.FrontTorque*sign>0);Assert.True(c.Ticks[^1].After.AngularVelocity*sign>0);
            foreach(var t in c.Ticks){
                Assert.InRange(Math.Abs(t.Forces.FrontLateral),0,t.Forces.Budget*.400001);
                Assert.InRange(Math.Sqrt(t.Forces.RearLateral*t.Forces.RearLateral+t.Forces.RearLongitudinal*t.Forces.RearLongitudinal),0,t.Forces.Budget*.600001);
                Assert.InRange(Math.Abs(c.Inertia*(t.After.AngularVelocity-t.Before.AngularVelocity)-t.Forces.NetTorque*t.Seconds),0,.00003);
                Assert.InRange(((t.After.Velocity-t.Before.Velocity)*c.Mass-t.Forces.NetForce*t.Seconds).Length(),0,.001);
            }
        }
    }
    [Fact]
    public void SteeringIsLimitedInAngleAndRateWithoutABodyAngleLimit()
    {
        var c=TwoContactBikeAudit.Execute(new("steer-limit",Steer:2,Seconds:.5));var p=TwoContactBikeParameters.Default;
        foreach(var t in c.Ticks){
            Assert.InRange(Math.Abs(t.Forces.SteeringAfter),0,p.MaximumSteeringRadians+.000001);
            Assert.InRange(Math.Abs(t.Forces.SteeringAfter-t.Forces.SteeringBefore),0,p.SteeringRateRadiansPerSecond*t.Seconds+.000001);
        }
        var high=TwoContactBikeAudit.Execute(new("large",12,50*MathF.PI/180,Seconds:1.2));
        Assert.True(Math.Abs(high.Initial.Riders.Single().SlideRadians)*180/Math.PI>49);
        Assert.True(Math.Abs(high.Ticks[^1].After.SlideRadians)*180/Math.PI<1);Assert.Null(high.StopReason);
    }
    [Fact]
    public void WheelspinChangesRearSideForceAndItsPhysicalMomentAtAnIdenticalBodyState()
    {
        var probes=JsonSerializer.SerializeToElement(TwoContactBikeAudit.ContactProbes(),InverseFeasibilityAudit.Json).GetProperty("Rows").EnumerateArray().ToArray();
        foreach(var sign in new[]{1,-1}){
            var group=probes.Where(r=>r.GetProperty("Sign").GetInt32()==sign).ToArray();
            var first=group[0].GetProperty("Forces");var last=group[^1].GetProperty("Forces");
            Assert.Equal(first.GetProperty("FrontLateral").GetDouble(),last.GetProperty("FrontLateral").GetDouble());
            Assert.True(Math.Abs(last.GetProperty("RearLateral").GetDouble())<Math.Abs(first.GetProperty("RearLateral").GetDouble()));
            Assert.NotEqual(first.GetProperty("RearTorque").GetDouble(),last.GetProperty("RearTorque").GetDouble());
            Assert.True(last.GetProperty("RearLongitudinal").GetDouble()>first.GetProperty("RearLongitudinal").GetDouble());
        }
        var native=TwoContactBikeAudit.Execute(new("ramp",Beta:.15f,Seconds:1.2,Ramp:true));
        Assert.Equal(0,native.Ticks[0].Before.Drive);Assert.Equal(1,native.Ticks[^1].Before.Drive);
        Assert.True(native.Ticks[^1].Wheel.InstantSlipRatio>.05);
    }
    [Theory]
    [InlineData(0,0,0)]
    [InlineData(0,0,2)]
    [InlineData(.05,50,.2)]
    [InlineData(12,35,0)]
    public void LocalContactWorkIncludesRotationExactlyOnceAndNeverAddsFreeEnergy(float speed,float betaDegrees,float spin)
    {
        var c=TwoContactBikeAudit.Execute(new("energy",speed,betaDegrees*MathF.PI/180,spin,Seconds:1.2));var energy=Energy(c);
        Assert.Equal(speed,c.Initial.Riders.Single().Velocity.X);Assert.Equal(0,c.Initial.Riders.Single().Velocity.Y);
        Assert.Equal(spin,c.Initial.Riders.Single().AngularVelocity);
        Assert.Null(c.StopReason);Assert.Equal(0,c.LegacyYawCalls);
        Assert.Equal(0,energy.GetProperty("MaximumExcessBeyondEngineJoules").GetDouble());
        Assert.InRange(Math.Abs(energy.GetProperty("BalanceResidualJoules").GetDouble()),0,.15);
        Assert.InRange(energy.GetProperty("MaximumRootResidual").GetDouble(),0,.000001);
        Assert.All(c.Ticks,t=>Assert.True(float.IsFinite(t.After.Speed)&&float.IsFinite(t.After.Heading)&&float.IsFinite(t.After.AngularVelocity)));
    }
    [Fact]
    public void NativeResultsRepeatAndConvergeWithSmallerHeldIntervals()
    {
        TwoContactBikeAudit.Capture Run(double h)=>TwoContactBikeAudit.Execute(new("convergence",Steer:.04f,Step:h));
        var a=Run(.02);var b=Run(.01);var c=Run(.005);Assert.Equal(b.Result.Fingerprint,Run(.01).Result.Fingerprint);
        var reference=c.Ticks[^1].After.Position;
        Assert.True(Vector2.Distance(b.Ticks[^1].After.Position,reference)<Vector2.Distance(a.Ticks[^1].After.Position,reference));
    }
    [Theory]
    [InlineData(.02)]
    [InlineData(.005)]
    public void LowSpeedAndPureBodySpinRemainPassiveAtTheResolutionEndpoints(double h)
    {
        foreach(var c in new[]{new TwoContactBikeAudit.Case("low-step",.05f,50*MathF.PI/180,Spin:.2f,Step:h),new("spin-step",0,Spin:2,Step:h)}){
            var run=TwoContactBikeAudit.Execute(c);var energy=Energy(run);
            Assert.Null(run.StopReason);Assert.Equal(0,energy.GetProperty("MaximumExcessBeyondEngineJoules").GetDouble());
            Assert.InRange(Math.Abs(energy.GetProperty("BalanceResidualJoules").GetDouble()),0,.001);
        }
    }
}
