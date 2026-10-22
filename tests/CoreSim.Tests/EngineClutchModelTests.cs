using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class EngineClutchModelTests
{
    private static EngineClutchStep Query(EngineClutchState engine,double wheelOmega=0,double time=1,float dt=.01f,double drive=1)
    {
        var p=RearWheelParameters.Default;var setup=CornerEntryAudit.Rider().Setup;
        return EngineClutch.Evaluate(engine,setup,null,time,.24,drive,dt,torque=>RearWheelDynamics.Evaluate(0,0,wheelOmega,1852.339f,147,1,
            torque,torque,torque,0,dt,p,rearBudgetNewtons:997.413));
    }
    private static void Energy(EngineClutchStep s)
    {
        Assert.InRange(Math.Abs(s.EngineEnergyChangeJoules+s.Wheel.EngineWorkJoules+s.ClutchLossJoules+s.TransmissionLossJoules-s.CombustionWorkJoules),0,1e-6);
        Assert.True(s.ClutchLossJoules>=-1e-5);Assert.True(s.TransmissionLossJoules>=-1e-5);
        Assert.InRange(Math.Abs(s.ClutchTorqueNewtonMeters),0,s.CapacityNewtonMeters+1e-10);
    }
    [Fact]
    public void DisconnectedRunningCrankDoesNotDriveTheWheelBeforeReaction()
    {
        var state=new EngineClutchState(new());var s=Query(state,time:0);Energy(s);
        Assert.Equal(state.AngularVelocityRadPerSecond,s.AfterOmega);Assert.Equal(0,s.Engagement);Assert.Equal(0,s.WheelTorqueNewtonMeters);
        Assert.Equal(0,s.Wheel.AfterOmega);Assert.Equal(0,s.Wheel.LongitudinalNewtons);Assert.Null(state.Prepared);
        Assert.Throws<ArgumentException>(()=>Query(state,time:.235));
    }
    [Theory]
    [InlineData(.005f)] [InlineData(.01f)] [InlineData(.02f)]
    public void SlidingClutchTransfersCrankEnergyThroughOriginalWheelSolver(float dt)
    {
        var state=new EngineClutchState(new());var before=state.AngularVelocityRadPerSecond;
        var s=Query(state,time:.24,dt:dt);Energy(s);
        Assert.True(s.ClutchTorqueNewtonMeters>0);Assert.True(s.Wheel.LongitudinalNewtons>0);Assert.True(s.Wheel.AfterOmega>0);
        Assert.True(s.ClutchLossJoules>0);Assert.False(s.Locked);Assert.Equal(before,state.AngularVelocityRadPerSecond);
        Assert.Equal(s,Query(state,time:.24,dt:dt));
    }
    [Fact]
    public void FullyCoupledStateKeepsOriginalRatioAndTorqueCurveWithCrankInertia()
    {
        var setup=CornerEntryAudit.Rider().Setup;var ratio=Engine500.Evaluate(0,setup,1).OverallRatio;
        var state=new EngineClutchState(new(),ratio*80);var s=Query(state,80,time:10,drive:.5);Energy(s);
        Assert.True(s.Locked);Assert.InRange(Math.Abs(s.AfterOmega-ratio*s.Wheel.AfterOmega),0,1e-6);
        Assert.Equal(Engine500.Torque((s.BeforeOmega+s.AfterOmega)/2*30/Math.PI)*.5,s.EngineTorqueNewtonMeters);
        Assert.InRange(Math.Abs(s.EngineTorqueNewtonMeters-s.ClutchTorqueNewtonMeters-state.Parameters.CrankInertiaKgMetersSquared*(s.AfterOmega-s.BeforeOmega)/s.Seconds),0,1e-7);
        Assert.Equal(s.ClutchTorqueNewtonMeters*ratio*.94,s.WheelTorqueNewtonMeters);
        Assert.InRange(Engine500.Torque(8500)*8500*Math.PI/30,48000,50000);
    }
    [Fact]
    public void BackdriveAlsoDissipatesAndNearZeroRemainsFinite()
    {
        var s=Query(new(new(),50),80,time:10,drive:0);Energy(s);Assert.True(s.ClutchTorqueNewtonMeters<0);
        foreach(var dt in new[]{.005f,.01f,.02f}){var low=Query(new(new(),.001),time:10,dt:dt);Energy(low);
            Assert.True(low.AfterOmega>=0);Assert.True(double.IsFinite(low.Wheel.InstantSlipRatio));Assert.True(low.Wheel.AfterOmega>=0);}
    }
}
