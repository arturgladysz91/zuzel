using System.Numerics;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class FullThrottleSlideAuditTests
{
    [Fact]
    public void OriginalBodyAndWheelPowerCloseWithoutAddingADriftBrake()
    {
        foreach(var grip in new[]{1f,.7f})foreach(var beta in new[]{0d,5,15,30})foreach(var kappa in new[]{0d,.2,1}){
            var q=new FullThrottleSlideAudit.Sampler(new(grip,0,.5f)).At(18,beta,kappa);
            var p=RearWheelParameters.Default;
            var engine=q.Wheel.DriveTorqueNewtonMeters*q.Wheel.BeforeOmega;
            var bearing=q.Wheel.BearingTorqueNewtonMeters*q.Wheel.BeforeOmega;
            var residual=Vector2.Dot(q.Force,q.View.Velocity)+q.WheelPowerWatts+q.LateralLossWatts+q.LongitudinalLossWatts+q.Slip.ResistanceLossWatts+bearing-engine;
            Assert.InRange(Math.Abs(residual),0,.02);Assert.True(q.LateralLossWatts>=-.00001);Assert.True(q.LongitudinalLossWatts>=-.00001);
            Assert.InRange(q.Wheel.RearUtilization,0,1.000001);Assert.InRange(q.Slip.GripUtilization,0,1.000001);
            Assert.Equal(0,q.View.AngularVelocity);Assert.Equal(.33,p.WheelRadiusMeters);
        }
    }
    [Fact]
    public void BodySlideAndDrivenWheelSlipAreDistinctAndCombinedRearBudgetIsShared()
    {
        var sampler=new FullThrottleSlideAudit.Sampler(new(1,0,.5f));
        var rolling=sampler.At(18,15,0);var spinning=sampler.At(18,15,.5);
        Assert.InRange(Math.Abs(rolling.View.SlideRadians*180/Math.PI-15),0,.00001);
        Assert.InRange(Math.Abs(rolling.Wheel.InstantSlipRatio),0,1e-6);Assert.True(spinning.Wheel.InstantSlipRatio>.49);
        Assert.Equal(rolling.Wheel.FrontLateralNewtons,spinning.Wheel.FrontLateralNewtons);
        Assert.True(spinning.Wheel.LongitudinalNewtons>rolling.Wheel.LongitudinalNewtons);
        Assert.True(Math.Abs(spinning.Wheel.RearLateralNewtons)<Math.Abs(rolling.Wheel.RearLateralNewtons));
        Assert.True(spinning.LongitudinalLossWatts>rolling.LongitudinalLossWatts);
    }
    [Fact]
    public void FullDriveCanDecelerateAtModerateSlideWithSteadyWheelButDoesNotProveStableBodyMotion()
    {
        var sampler=new FullThrottleSlideAudit.Sampler(new(1,0,.5f));var aligned=sampler.SteadyWheel(18,0);var sliding=sampler.SteadyWheel(18,15);
        Assert.True(aligned.TangentialAcceleration>1);Assert.True(sliding.TangentialAcceleration< -1);
        Assert.True(sliding.LateralLossWatts>8000);Assert.Equal(1,sliding.View.Drive);
        Assert.InRange(Math.Abs(sliding.WheelPowerWatts),0,.001);
        Assert.True(Vector2.Dot(sliding.Force,sliding.View.Velocity)<0);
    }
    [Fact]
    public void RawSurfaceInputsCollapseToEffectiveGripButLowerGripAlsoChangesRegularization()
    {
        var one=new TrackSurfaceState(1,0,.5f);var two=new TrackSurfaceState(one.EffectiveGrip,0,.35f);
        Assert.Equal(one.EffectiveGrip,two.EffectiveGrip);
        var a=new FullThrottleSlideAudit.Sampler(one).At(18,15,.2);var b=new FullThrottleSlideAudit.Sampler(two).At(18,15,.2);
        Assert.Equal(a.Force,b.Force);Assert.Equal(a.Wheel,b.Wheel);
        var low=new FullThrottleSlideAudit.Sampler(new(.55f,0,.5f)).At(18,15,.2);
        Assert.True(low.Slip.GripBudgetNewtons<a.Slip.GripBudgetNewtons);
        var normalizedHigh=a.Wheel.LongitudinalNewtons/a.Slip.GripBudgetNewtons;
        var normalizedLow=low.Wheel.LongitudinalNewtons/low.Slip.GripBudgetNewtons;
        Assert.True(Math.Abs(normalizedHigh-normalizedLow)>.001);
    }
}
