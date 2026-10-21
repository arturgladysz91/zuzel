using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.Setup;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class MotorcyclePerformanceTests
{
    [Fact]
    public void FlagsAreOffWithoutChangingHistoricalParameterBytesAndInvalidDependenciesFail()
    {
        var p=PhysicalSteeringAudit.Parameters();Assert.False(p.Enable500CcEngine);Assert.False(p.EnableLoadBasedTyreGrip);
        var json=JsonSerializer.Serialize(p,InverseFeasibilityAudit.Json);Assert.DoesNotContain("Engine500",json);Assert.DoesNotContain("LoadBased",json);Assert.DoesNotContain("Enable500CcEngine",json);
        var spec=RacingLineRootCauseAudit.Rider(AuthoredRacingPath.Crossover(false));
        foreach(var invalid in new[]{new WorldParameters{Enable500CcEngine=true},MotorcyclePerformanceAudit.Parameters("C-both") with{EnableDynamicAxleLoad=true},
            p with{Engine500=new()},p with{LoadBasedTyres=new()},MotorcyclePerformanceAudit.Parameters("A-engine") with{EnablePhysicalSteeringControl=false}})
            Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:invalid));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec with{SpeedMetersPerSecond=0}},parameters:MotorcyclePerformanceAudit.Parameters("A-engine")));
    }
    [Fact]
    public void TorqueCurveProducesDocumentedPowerAndCutsEngineTorqueRatherThanVehicleSpeed()
    {
        Assert.InRange(Engine500.Torque(8500)*8500*Math.PI/30,48000,50000);
        Assert.Equal(0,Engine500.Torque(Engine500.CutoffRpm));Assert.Equal(0,Engine500.Torque(14000));
        Assert.All(Enumerable.Range(0,141),i=>Assert.InRange(Engine500.Torque(i*100)*i*100*Math.PI/30,0,50000));
        var over=Engine500.Evaluate(200,new(.5f,.5f),1);Assert.True(over.EngineRpm>Engine500.CutoffRpm);Assert.Equal(0,over.DriveWheelTorqueNewtonMeters);
    }
    [Fact]
    public void OneRatioCreatesLowSpeedTorqueAndHighSpeedCrossoverWithExplicitEfficiencyLoss()
    {
        var shortGear=new BikeSetup(0,.5f);var longGear=new BikeSetup(1,.5f);
        Assert.True(Engine500.Evaluate(60,shortGear,1).DriveWheelTorqueNewtonMeters>Engine500.Evaluate(60,longGear,1).DriveWheelTorqueNewtonMeters);
        Assert.True(Engine500.Evaluate(130,shortGear,1).DriveWheelTorqueNewtonMeters<Engine500.Evaluate(130,longGear,1).DriveWheelTorqueNewtonMeters);
        var command=Engine500.Evaluate(60,shortGear,.55);
        Assert.Equal(command.CrankTorqueNewtonMeters*command.OverallRatio*command.Efficiency,command.AvailableWheelTorqueNewtonMeters);
        Assert.Equal(.55*command.AvailableWheelTorqueNewtonMeters,command.DriveWheelTorqueNewtonMeters);
    }
    [Fact]
    public void TyreBudgetBalancesStaticNormalLoadAndRespondsToSurfaceWithoutReferenceSpeed()
    {
        var bike=TwoContactBikeParameters.Default;var a=LoadBasedTyreGrip.Budget(147,new(1,0,.35f),bike);var b=LoadBasedTyreGrip.Budget(147,new(.6f,.2f,.7f),bike);
        Assert.Equal(147*9.81,a.FrontNormalNewtons+a.RearNormalNewtons,9);
        Assert.InRange(Math.Abs(a.FrontNormalNewtons*bike.FrontAxleFromComMeters-a.RearNormalNewtons*bike.RearAxleFromComMeters),0,1e-10);
        Assert.Equal(a.EffectiveFrictionCoefficient*(a.FrontNormalNewtons+a.RearNormalNewtons),a.TotalCapacityNewtons,9);
        Assert.True(b.TotalCapacityNewtons<a.TotalCapacityNewtons);Assert.Equal(0,LoadBasedTyreGrip.Budget(147,new(0,0,.35f),bike).TotalCapacityNewtons);
    }
    [Fact]
    public void ZeroGripAndCombinedSlipRespectCapacitiesAndDissipation()
    {
        var wp=RearWheelParameters.Default;
        foreach(var budget in new[]{0f,1800f})foreach(var side in new[]{-8d,0,8})foreach(var spin in new[]{-10d,10}){
            var rear=budget*.5384615;var s=RearWheelDynamics.Evaluate(18,side,(18+spin)/.33,budget,147,4,350,350,350,-100,.01f,wp,rear);
            Assert.InRange(Math.Sqrt(s.LongitudinalNewtons*s.LongitudinalNewtons+s.RearLateralNewtons*s.RearLateralNewtons),0,rear+1e-3);
            Assert.True(s.LongitudinalLossJoules>=-1e-8);Assert.True(s.RearLateralNewtons*side<=0);Assert.True(s.FrontLateralNewtons*side<=0);
        }
    }
    [Fact]
    public void NewEngineCommandDoesNotDependOnSegmentAndQueriesDoNotCommitAcceptedState()
    {
        var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var spec=RacingLineRootCauseAudit.Rider(path);var p=MotorcyclePerformanceAudit.Parameters("C-both");
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var commands=new List<Engine500Command>();
        foreach(var reference in new[]{4d,70}){
            var at=frame.At(reference,5);var view=new RiderView(1,at.Position,at.Road.Tangent*18,(float)at.Road.Heading,0,at.Road,reference,0,RiderActivity.Racing,1,5,0);
            var wheel=new RearWheelState(18);var steering=new TwoContactBikeState();
            var f=TwoContactBikeDynamics.Forces(view,spec,frame,surface,p,0,147,wheel,steering,.01f,steeringOverride:0,inertia:52.72097f);
            Assert.Equal(18/.33,wheel.AngularVelocityRadPerSecond);Assert.Equal(0,steering.SteeringRadians);Assert.Null(f.AxleLoad);Assert.NotNull(f.TyreLoads);commands.Add(f.Engine!);
        }
        Assert.Equal(commands[0],commands[1]);
    }
    [Theory]
    [InlineData("baseline")][InlineData("A-engine")][InlineData("B-tyres")][InlineData("C-both")]
    public void NativeEnergyAndSingleCommitRemainCorrectAcrossTheAttribution(string variant)
    {
        var p=MotorcyclePerformanceAudit.Parameters(variant);var c=MotorcyclePerformanceAudit.Isolated("test-"+variant,p,true,maximumSeconds:.3);
        Assert.Null(c.StopReason);Assert.Equal(c.Ticks.Length,c.Result.Work.SolverCalls);Assert.Equal(c.Result.Work.SolverCalls,c.Result.Work.RearWheelCommits);
        foreach(var t in c.Ticks){
            Assert.InRange(((t.After.Velocity-t.Before.Velocity)*c.Mass-t.Forces.NetForce*t.Seconds).Length(),0,.001);
            Assert.InRange(Math.Abs(c.Inertia*(t.After.AngularVelocity-t.Before.AngularVelocity)-t.Forces.NetTorque*t.Seconds),0,.00003);
            if(t.Forces.Engine is { } e){Assert.True(e.TransmissionLossJoules(t.Wheel)>=0);Assert.Equal(e.CrankWorkJoules(t.Wheel),t.Wheel.EngineWorkJoules+e.TransmissionLossJoules(t.Wheel),9);}
        }
        Assert.Equal(0,JsonSerializer.SerializeToElement(TwoContactBikeAudit.Energy(c)).GetProperty("MaximumExcessBeyondEngineJoules").GetDouble());
        var repeat=MotorcyclePerformanceAudit.Isolated("test-"+variant,p,true,maximumSeconds:.3);Assert.Equal(c.Result.Fingerprint,repeat.Result.Fingerprint);
    }
}
