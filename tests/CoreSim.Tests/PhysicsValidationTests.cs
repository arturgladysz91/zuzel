using System.Numerics;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class PhysicsValidationTests
{
    [Fact] public void FrontIsUndrivenAndItsContactPowerNeverPropelsTheWheel(){
        var state=PhysicsAudit139.States()[0];
        foreach(var beta in new[]{-40d,0,40})foreach(var steering in new[]{-35d,0,35}){
            var q=PhysicsAudit139.Query(state,steering,beta,speed:20,yaw:.7,gas:1);var f=q.Force;
            var forward=new Vector2(f.FrontLeft.Y,-f.FrontLeft.X);
            Assert.InRange(Math.Abs(Vector2.Dot(f.FrontForce,forward)),0,.001);
            Assert.True(Vector2.Dot(f.FrontForce,f.FrontVelocity)<=.001);
            Assert.True(f.FrontLateral*Vector2.Dot(f.FrontVelocity,f.FrontLeft)<=.001);
        }
    }
    [Fact] public void GasChangesWheelAccelerationButCannotJumpForcesAtFixedInstantaneousSlip(){
        var state=PhysicsAudit139.States()[1];var zero=PhysicsAudit139.Query(state,0,gas:0);var full=PhysicsAudit139.Query(state,0,gas:1);
        Assert.Equal(zero.Force.FrontForce,full.Force.FrontForce);Assert.Equal(zero.Force.RearForce,full.Force.RearForce);
        Assert.Equal(0,zero.Wheel.DriveTorqueNewtonMeters);Assert.True(full.Wheel.DriveTorqueNewtonMeters>0);
        var after=PhysicsAudit139.Query(state,0,gas:1,seconds:.01);
        Assert.True(after.Wheel.AfterOmega>after.Wheel.BeforeOmega);
    }
    [Fact] public void CombinedRearForceStaysInsideOneBudgetAndBothSlipWorksAreDissipative(){
        var state=PhysicsAudit139.States()[0];
        foreach(var beta in new[]{-40d,-10,0,10,40})foreach(var kappa in new[]{-.5,0,.2,2.56,5}){
            var q=PhysicsAudit139.Query(state,0,beta,speed:20,yaw:0,kappa:kappa,gas:1);var f=q.Force;
            Assert.True(f.RearForce.Length()<=f.TyreLoads!.RearCapacityNewtons+.001);
            Assert.True(q.Wheel.LongitudinalNewtons*q.Wheel.MidSlipVelocityMetersPerSecond>=-.001);
            Assert.True(f.RearLateral*Vector2.Dot(f.RearVelocity,f.BodyLeft)<=.001);
        }
    }
    [Fact] public void ExactSavedNativeForceBenchmarkAndPassivePreparationUseOriginalAxes(){
        PhysicsAudit139.ValidateNativeBenchmark();var state=PhysicsAudit139.States()[0];var angle=PhysicsExperiment139.PreparationAngle(state.Input);
        Assert.InRange(Math.Abs(angle),0,15*Math.PI/180+.000001);Assert.False(new WorldParameters().EnableFreeRacingLearning);
        Assert.All(PhysicsVariant139.NativeSensitivity,x=>x.Validate());
    }
}
