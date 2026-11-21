using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class ContactLoadSolver64Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContinuousRootIsAcceptedWithItsMeasuredResidual(bool bisect)
    {
        static ContactResidual64<double> Sample(double x) => new(x, Math.Exp(x)-1.7, x);
        var point=ContactLoadSolver64.Solve(-1,1,2e-5,Sample,_=>false,bisect);
        Assert.InRange(Math.Abs(point.Residual),0,2e-5);
        Assert.Equal(Sample(point.Ax),point);
    }

    [Fact]
    public void SameSignRequiresExclusionAndDoesNotHideTwoInteriorCrossings()
    {
        static ContactResidual64<int> Above(double x) => new(x,1+x*x,0);
        static ContactResidual64<int> Twice(double x) => new(x,(x-.2)*(x-.7),0);
        Assert.Equal(ContactRootStatus.NoPhysicallyAdmissibleRoot,
            Assert.Throws<ContactRootException>(()=>ContactLoadSolver64.Solve(-1,1,2e-5,Above,_=>true)).Status);
        Assert.Equal(ContactRootStatus.AmbiguousOrMultipleRoots,
            Assert.Throws<ContactRootException>(()=>ContactLoadSolver64.Solve(-1,1,2e-5,Above,_=>false)).Status);
        Assert.Equal(ContactRootStatus.AmbiguousOrMultipleRoots,
            Assert.Throws<ContactRootException>(()=>ContactLoadSolver64.Solve(0,1,2e-5,Twice,_=>false)).Status);
    }

    [Fact]
    public void JumpAcrossToleranceAndNonfiniteInputHaveDistinctStatuses()
    {
        static ContactResidual64<int> Jump(double x) => new(x,x<.31?-3e-5:3e-5,0);
        static ContactResidual64<int> Bad(double x) => new(x,double.NaN,0);
        Assert.Equal(ContactRootStatus.NumericalDiscontinuity,
            Assert.Throws<ContactRootException>(()=>ContactLoadSolver64.Solve(0,1,2e-5,Jump,_=>false)).Status);
        Assert.Equal(ContactRootStatus.InvalidPhysicalState,
            Assert.Throws<ContactRootException>(()=>ContactLoadSolver64.Solve(0,1,2e-5,Bad,_=>false)).Status);
    }

    [Fact]
    public void ActuatorCertificateValidatesDoubleBeforeItsSinglePrecisionBoundary()
    {
        const float previous=.014686691f, seconds=.01f;
        const double target=.000506209699;
        var step=new TwoContactForces
        {
            SteeringBefore=previous,SteeringRequest=(float)target,SteeringAfter=(float)target,
            SteeringUsed=(float)((previous+target)/2),SteeringTargetBeforeRounding=target
        };
        Assert.True(FreeLearningExperiment.SteeringStepIsPhysical(step,seconds));
        Assert.False(FreeLearningExperiment.SteeringStepIsPhysical(step with {SteeringTargetBeforeRounding=null},seconds));
        Assert.False(FreeLearningExperiment.SteeringStepIsPhysical(step with {SteeringAfter=MathF.BitIncrement(step.SteeringAfter)},seconds));
        Assert.False(FreeLearningExperiment.SteeringStepIsPhysical(step with {SteeringTargetBeforeRounding=1},seconds));
    }

    [Theory]
    [InlineData(-40)]
    [InlineData(40)]
    public void BoundaryWheelReactionAndWorkUseAppliedForce(double torque)
    {
        const double seconds=.01, mass=147, forward=16, external=-60, capacity=1000;
        var wheel=RearWheelParameters.Default;
        double omega=(forward+Math.CopySign(2,torque))/wheel.WheelRadiusMeters;
        var forecast=RearWheelDynamics.EvaluateLongitudinal64(forward,omega,mass,capacity,torque,external,
            seconds,wheel,wheel.MinimumContactRelaxationSeconds);
        double applied=(float)forecast.Force+1e-8;
        var accepted=RearWheelDynamics.AcceptLongitudinal64(forward,omega,mass,capacity,torque,external,applied,0,
            seconds,wheel,forecast);
        Assert.Equal(applied,accepted.AppliedLongitudinalNewtons);
        Assert.Equal(Math.Sign(torque),Math.Sign(accepted.AppliedLongitudinalNewtons!.Value));
        double impulse=wheel.WheelInertiaKgMetersSquared*(accepted.AfterOmega-omega);
        Assert.Equal(impulse,seconds*(torque-wheel.WheelRadiusMeters*applied-accepted.BearingTorqueNewtonMeters),12);
        double change=.5*wheel.WheelInertiaKgMetersSquared*(accepted.AfterOmega*accepted.AfterOmega-omega*omega);
        Assert.Equal(change,accepted.EngineWorkJoules-accepted.BearingLossJoules-
            seconds*wheel.WheelRadiusMeters*accepted.MidOmega*applied,10);
    }
}
