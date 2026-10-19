using System.Numerics;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;
[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class RearWheelDynamicsTests
{
    private static WorldParameters On()=>SlipControlValidation.Parameters() with{EnableRearWheelDynamics=true};
    [Fact]
    public void WheelRequiresSlipAndStartsAtTheActualLongitudinalRollingSpeed()
    {
        Assert.False(new WorldParameters().EnableRearWheelDynamics);
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{SlipDynamicsAudit.Rider()},parameters:new(){EnableRearWheelDynamics=true}));
        Assert.Throws<ArgumentException>(()=>new RearWheelState(1,new(){WheelInertiaKgMetersSquared=0}));
        Assert.Throws<ArgumentException>(()=>new RearWheelState(1,new(){MinimumContactRelaxationSeconds=.001}));
        using var w=new WorldRunner(new[]{SlipDynamicsAudit.Rider(speed:18) with{InitialOutwardSpeed=3}},parameters:On());
        var b=w.Snapshot.Riders.Single();var wheel=w.RearWheel(1);
        Assert.Equal(b.ForwardSpeed,wheel.CircumferentialSpeedMetersPerSecond,10);
        Assert.InRange(Math.Abs(RearWheelDynamics.SlipRatio(b.ForwardSpeed,wheel.RearWheelAngularVelocityRadPerSecond,RearWheelParameters.Default)),0,1e-12);
        Assert.Single(w.Snapshot.RearWheels!);
    }
    [Fact]
    public void CombinedContactIsDissipativeBoundedAndSeparatesFrontFromRear()
    {
        var p=RearWheelParameters.Default;const float budget=1800,mass=147;
        foreach(var u in new[]{0d,.0001,.1,20,50})foreach(var side in new[]{-12d,0,12})foreach(var circum in new[]{-20d,0,20,150})foreach(var h in new[]{.005f,.01f,.02f}){
            var scale=.12*.12*(u*u+.25)+Math.Pow(budget/mass*.03,2);
            var s=RearWheelDynamics.Evaluate(u,side,circum/p.WheelRadiusMeters,budget,mass,scale,150,150,150,0,h,p);
            Assert.True(double.IsFinite(s.AfterOmega));Assert.True(s.RearUtilization<=1.000001);
            Assert.True(Math.Sqrt(s.LongitudinalNewtons*s.LongitudinalNewtons+s.LateralNewtons*s.LateralNewtons)<=budget+.001);
            Assert.True(s.LongitudinalLossJoules>=-.00001);Assert.True(-s.LateralNewtons*side>=0);
            Assert.InRange(Math.Abs(s.RootResidualMetersPerSecond),0,.00001);
            var wheelDelta=.5*p.WheelInertiaKgMetersSquared*(s.AfterOmega*s.AfterOmega-s.BeforeOmega*s.BeforeOmega);
            Assert.InRange(Math.Abs(wheelDelta-s.EngineWorkJoules+s.LongitudinalNewtons*p.WheelRadiusMeters*s.MidOmega*h+s.BearingLossJoules),0,1e-8);
        }
        var rolling=RearWheelDynamics.Evaluate(20,-8,20/p.WheelRadiusMeters,budget,mass,6,0,0,0,0,0,p);
        var spinning=RearWheelDynamics.Evaluate(20,-8,80/p.WheelRadiusMeters,budget,mass,6,0,0,0,0,0,p);
        Assert.Equal(rolling.FrontLateralNewtons,spinning.FrontLateralNewtons);
        Assert.True(Math.Abs(spinning.RearLateralNewtons)<Math.Abs(rolling.RearLateralNewtons)/2);
        Assert.Equal(0,rolling.LongitudinalNewtons);Assert.True(spinning.LongitudinalNewtons>0);
    }
    [Theory]
    [InlineData(.005)] [InlineData(.01)] [InlineData(.02)]
    public void OneAcceptedClockAndZeroDriveEnergyRemainStable(double step)
    {
        foreach(var speed in new[]{0f,.01f,22f}){
            var spec=SlipDynamicsAudit.Rider(reference:30,speed:speed) with{InitialOutwardSpeed=speed==.01f?.1f:0};
            var rows=new List<RearWheelObservation>();
            using var w=new WorldRunner(new[]{spec},parameters:On(),controls:new[]{new ControlCommand(1,0,5,0)},diagnostics:new(){RearWheel=rows.Add});
            var m=w.MechanicalProperties(1);
            double E(WorldSnapshot s)=>.5*m.MassKg*s.Riders[0].Velocity.LengthSquared()+.5*m.InertiaKgMetersSquared*Math.Pow(s.Riders[0].AngularVelocity,2)+s.RearWheels![0].RotationalEnergyJoules;
            var previous=E(w.Snapshot);var beforeOmega=w.RearWheel(1).RearWheelAngularVelocityRadPerSecond;
            var result=w.Run("coast",step,3,acceptedObserver:s=>{var energy=E(s);Assert.True(energy<=previous+.03,$"energy gain {energy-previous}");previous=energy;});
            Assert.Equal(result.Work.SolverCalls,rows.Count);
            Assert.All(rows,r=>Assert.InRange(r.Step.Seconds,0,step+1e-8));
            for(var i=0;i<rows.Count;i++){
                var r=rows[i];Assert.Equal(i==0?beforeOmega:rows[i-1].Step.AfterOmega,r.Step.BeforeOmega);
                var p=RearWheelParameters.Default;
                Assert.InRange(Math.Abs(p.WheelInertiaKgMetersSquared*(r.Step.AfterOmega-r.Step.BeforeOmega)
                    -r.Step.Seconds*(r.Step.DriveTorqueNewtonMeters-r.Step.LongitudinalNewtons*p.WheelRadiusMeters-r.Step.BearingTorqueNewtonMeters)),0,1e-10);
            }
            if(speed==22)Assert.True(result.Work.CurvatureLimitedCalls>0);
        }
    }
    [Fact]
    public void AShortenedCurvatureIntervalCannotAdvanceTheWheelByTheNominalTick()
    {
        var rows=new List<RearWheelObservation>();using var w=new WorldRunner(new[]{SlipDynamicsAudit.Rider(reference:34.95)},parameters:On(),diagnostics:new(){RearWheel=rows.Add});
        var result=w.Run("boundary",.02,.2);
        Assert.True(result.Work.CurvatureLimitedCalls>0);
        Assert.Contains(rows,r=>r.Step.Seconds<.019);
        Assert.InRange(Math.Abs(rows.Sum(r=>r.Step.Seconds)-result.TimeSeconds),0,1e-8);
    }
    [Fact]
    public void ContactAndCrashNeverResetTheIndependentWheelOrAddASecondIntegration()
    {
        var rows=new List<RearWheelObservation>();var bodies=new List<WorldSnapshot>();
        var scenario=new Scenario("severe-contact",new[]{SlipDynamicsAudit.Rider(5,0,35,1),SlipDynamicsAudit.Rider(5,3,8,2)},2);
        using var w=new WorldRunner(scenario.Riders,parameters:On(),diagnostics:new(){RearWheel=rows.Add});
        var result=w.Run("contact",.01,2,acceptedObserver:bodies.Add);
        Assert.NotEmpty(result.Contacts);
        Assert.Contains(rows,r=>r.Before.Activity==RiderActivity.Crashed);
        foreach(var group in rows.GroupBy(r=>r.RiderId)){
            var a=group.ToArray();for(var i=1;i<a.Length;i++)Assert.Equal(a[i-1].Step.AfterOmega,a[i].Step.BeforeOmega);
            Assert.Equal(result.Work.SolverCalls,a.Length);
            foreach(var row in a.Where(r=>r.Before.Activity==RiderActivity.Crashed)){
                Assert.Equal(0,row.Step.DriveTorqueNewtonMeters);Assert.Equal(0,row.Step.LongitudinalNewtons);
                Assert.True(Math.Abs(row.Step.AfterOmega)<=Math.Abs(row.Step.BeforeOmega));
            }
        }
        using var reverse=new WorldRunner(scenario.Riders.Reverse(),parameters:On());
        var again=reverse.Run("contact",.01,2);Assert.Equal(result.Fingerprint,again.Fingerprint);
    }
    [Fact]
    public void RearWheelForcesAllocateNothingAndTheirGuardAccountsForWheelEnergy()
    {
        var spec=SlipDynamicsAudit.Rider(reference:65);using var w=new WorldRunner(new[]{spec},parameters:On());
        var p=On();var surface=TrackState.CreateDefault(w.Frame.Track).Snapshot();var m=w.MechanicalProperties(1);
        var state=w.Snapshot.Riders[0];var wheel=new RearWheelState(state.ForwardSpeed);
        for(var i=0;i<10000;i++)RiderController.Forces(state,spec,w.Frame,surface,p,1,m.MassKg,m.InertiaKgMetersSquared,rearWheel:wheel,stepSeconds:.01f);
        var bytes=GC.GetAllocatedBytesForCurrentThread();
        for(var i=0;i<10000;i++)RiderController.Forces(state,spec,w.Frame,surface,p,1,m.MassKg,m.InertiaKgMetersSquared,rearWheel:wheel,stepSeconds:.01f);
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-bytes);
        var limited=false;YawLimitObservation yaw=default;
        foreach(var speed in new[]{0f,.01f,18f})foreach(var heading in new[]{-.2f,0,.2f})foreach(var spin in new[]{-3f,-.001f,.001f,3f}){
            var v=state with{Velocity=new(speed,0),Heading=heading,AngularVelocity=spin,Drive=0};var spinWheel=new RearWheelState(speed);
            var f=RiderController.Forces(v,spec,w.Frame,surface,p,1,m.MassKg,m.InertiaKgMetersSquared,observeYaw:y=>yaw=y,rearWheel:spinWheel,stepSeconds:.02f);
            limited|=Math.Abs(yaw.AfterEnergyNewtonMeters-yaw.AfterAuthorityNewtonMeters)>1e-5;
            var step=spinWheel.Prepared;
            var bodyRate=Vector2.Dot(f.Force,v.Velocity)+.01*f.Force.LengthSquared()/m.MassKg
                +f.Torque*v.AngularVelocity+.01*f.Torque*f.Torque/m.InertiaKgMetersSquared;
            var wheelRate=.5*spinWheel.Parameters.WheelInertiaKgMetersSquared*(step.AfterOmega*step.AfterOmega-step.BeforeOmega*step.BeforeOmega)/.02;
            Assert.True(bodyRate+wheelRate<=.1,$"zero-drive rate {bodyRate+wheelRate}");
        }
        Assert.True(limited);
    }
    [Fact]
    public void ThrottleReplaysTheWholeBodyAndWheelPrefixAndActsThroughTorqueFirst()
    {
        var frame=RearWheelAudit.Frame();var plan=PhysicsDrivenRacingLine.Plans().Single(p=>p.Name=="inner");
        var original=RearWheelAudit.Execute(new("prefix",new[]{SlipDynamicsAudit.Rider()},20),true,spatial:PhysicsDrivenRacingLine.Compile(plan,frame));
        var anchor=original.Body.Ticks.First(t=>t.Before.Road.IsTurn&&t.Before.Road.CornerProgress>=.2&&Math.Abs(t.Slip.ActualSlipDegrees)>=15);
        var runs=new[]{.4f,.7f,1f}.Select(d=>RearWheelAudit.Probe(original,anchor,d,true)).ToArray();
        Assert.Single(runs.Select(c=>RearWheelAudit.Prefix(c,anchor.TimeSeconds)).Distinct());
        var at=runs.Select(c=>c.Wheels.First(w=>w.TimeSeconds>=anchor.TimeSeconds-1e-9)).ToArray();
        Assert.Single(at.Select(w=>w.Step.BeforeOmega).Distinct());
        Assert.Single(at.Select(w=>w.Before.Position).Distinct());
        Assert.Equal(3,at.Select(w=>w.Step.DriveTorqueNewtonMeters).Distinct().Count());
        Assert.Equal(3,at.Select(w=>w.Step.AfterOmega).Distinct().Count());
        Assert.Equal(3,runs.Select(c=>c.Body.Result.Fingerprint).Distinct().Count());
        Assert.All(runs,c=>Assert.True(c.Body.Result.Work.MaximumStepDisplacementMeters<.3));
        var p=RearWheelParameters.Default;var mass=original.Body.Mechanical[1].MassKg;
        var noTime=new[]{.4,.7,1}.Select(d=>RearWheelDynamics.Evaluate(anchor.Before.ForwardSpeed,anchor.Before.SideSpeed,at[0].Step.BeforeOmega,
            anchor.Slip.GripBudgetNewtons,mass,6,150*d,150,150*d,0,0,p)).ToArray();
        Assert.Single(noTime.Select(x=>x.LongitudinalNewtons).Distinct());Assert.Single(noTime.Select(x=>x.RearLateralNewtons).Distinct());
    }
    [Fact]
    public void TheNewWorldRepeatsFlyingLapsAndRetainsActualOffTrackTrajectories()
    {
        var condition=PhysicsDrivenRacingLineAudit.Conditions()[0];var plan=PhysicsDrivenRacingLine.Plans().Single(p=>p.Name=="inner");
        var a=PhysicsDrivenRacingLineAudit.Evaluate(condition,plan,parameters:On());
        var b=PhysicsDrivenRacingLineAudit.Evaluate(condition,plan,parameters:On());
        Assert.True(a.Evaluation.Feasible);Assert.Equal(a.Evaluation.Fingerprint,b.Evaluation.Fingerprint);
        Assert.InRange(Math.Abs(a.Evaluation.LapSeconds[1]-a.Evaluation.LapSeconds[2]),0,.05);
        var unsafeRun=PhysicsDrivenRacingLineAudit.Evaluate(condition,new("unsafe",.5f,.5f,.5f,.5f),parameters:On());
        Assert.False(unsafeRun.Evaluation.Feasible);Assert.Contains("CapsuleClearance",unsafeRun.Evaluation.Rejections);
        Assert.True(unsafeRun.Evaluation.OutsideSamples>0);Assert.True(unsafeRun.Evaluation.MinimumInnerClearanceMeters<0);
    }
    [Fact]
    public void LaunchBuildsActualSpinAndTheChassisReceivesOnlyTheContactForce()
    {
        var frame=RearWheelAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(.35f,0,.5f)).Snapshot();
        var c=RearWheelAudit.Execute(new("launch",new[]{SlipDynamicsAudit.Rider(speed:0)},2),true,
            new[]{new ControlCommand(1,0,5,1),new(1,1,5,.4f)},surface:surface);
        Assert.Contains(c.Wheels,w=>w.Step.InstantSlipRatio>.2&&w.Step.AfterOmega>w.Step.BeforeOmega);
        var opened=c.Wheels.First(w=>w.TimeSeconds>=1-1e-9);
        Assert.Equal(c.Wheels.Last(w=>w.TimeSeconds<1-1e-9).Step.AfterOmega,opened.Step.BeforeOmega);
        Assert.True(opened.Step.BeforeOmega*RearWheelParameters.Default.WheelRadiusMeters>opened.Before.ForwardSpeed);
        for(var i=0;i<c.Body.Ticks.Count;i++){
            var t=c.Body.Ticks[i];if(t.Slip.RiderId==0)continue;
            var f=new Vector2(MathF.Cos(t.Before.Heading),MathF.Sin(t.Before.Heading));
            var resistance=-t.Before.Velocity*(LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(t.Before.Speed)/MathF.Sqrt(t.Before.Speed*t.Before.Speed+.25f));
            Assert.InRange(Math.Abs(Vector2.Dot(t.Applied.ForceNewtons-resistance,f)-c.Wheels[i].Step.LongitudinalNewtons),0,.001);
        }
    }
}
