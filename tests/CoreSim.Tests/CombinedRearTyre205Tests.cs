using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class CombinedRearTyre205Tests
{
    [Theory]
    [InlineData(0,0)]
    [InlineData(0,20)]
    [InlineData(22,65)]
    [InlineData(22,110)]
    public void PureLongitudinalRootRetainsOldLaw(double forward,double omega)
    {
        const double mass=147, capacity=1200, dt=.01, torque=100, external=-77;
        var p=RearWheelParameters.Default;
        var legacy=RearWheelDynamics.EvaluateLongitudinal64(forward,omega,mass,capacity,torque,external,dt,p,
            p.MinimumContactRelaxationSeconds);
        double ax=(legacy.Force+external)/mass;
        var coupled=RearWheelDynamics.EvaluateCombined64(forward,0,omega,mass,capacity,torque,ax,dt,p);
        Assert.InRange(Math.Abs(legacy.Force-coupled.Force),0,1e-6);
        Assert.Equal(0,RearWheelDynamics.CoupledLateralForce(capacity,0,coupled));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(2)]
    public void AtZeroMidSlipLateralCurveIsUnchanged(double side)
    {
        const double dt=.01, forward=18, ax=2, capacity=1500;
        var p=RearWheelParameters.Default;
        double wheel=(forward+dt*ax/2)/p.WheelRadiusMeters*
            (1+dt*p.BearingResistanceNewtonMeterSeconds/(2*p.WheelInertiaKgMetersSquared));
        var coupled=RearWheelDynamics.EvaluateCombined64(forward,side,wheel,147,capacity,0,ax,dt,p);
        Assert.InRange(Math.Abs(coupled.Force),0,1e-7);
        Assert.InRange(Math.Abs(RearWheelDynamics.CoupledLateralForce(capacity,side,coupled)+
            capacity*side/Math.Sqrt(1+side*side)),0,1e-7);
    }

    [Fact]
    public void SignedCombinedSlipIsPassiveInsideCircleAtLowSpeedAndExtremeSpin()
    {
        const double capacity=1550, dt=.01, mass=147;
        var p=RearWheelParameters.Default;
        foreach (double forward in new[]{0d,1e-8,22d})
        foreach (double signedSlip in new[]{-13d,-1d,0,1e-8,1d,1e5})
        foreach (double side in new[]{-4d,-.25,0,.25,4d})
        {
            double torque=0, bearing=1+dt*p.BearingResistanceNewtonMeterSeconds/(2*p.WheelInertiaKgMetersSquared);
            double omega=(forward+signedSlip)/p.WheelRadiusMeters*bearing;
            var x=RearWheelDynamics.EvaluateCombined64(forward,side,omega,mass,capacity,torque,0,dt,p);
            var opposite=RearWheelDynamics.EvaluateCombined64(forward,-side,omega,mass,capacity,torque,0,dt,p);
            double y=RearWheelDynamics.CoupledLateralForce(capacity,side,x);
            Assert.Equal(x.Force,opposite.Force);
            Assert.Equal(-y,RearWheelDynamics.CoupledLateralForce(capacity,-side,opposite));
            Assert.InRange(x.Force*x.ConstitutiveSlip,0,double.MaxValue);
            Assert.InRange(-y*side,0,double.MaxValue);
            Assert.InRange(x.Force*x.Force+y*y,0,capacity*capacity+1e-8);
            if(Math.Abs(signedSlip)>1e-9) Assert.Equal(Math.Sign(signedSlip),Math.Sign(x.Force));
        }
    }

    [Fact]
    public void CertifierDistinguishesUniqueMultipleAbsentAndUnresolvedRoots()
    {
        static ContactResidual64<int> At(double x)=>new(x,x*x-.25,0);
        static ContactSlope64 Enclose(double lo,double hi,ContactResidual64<int> _)=>
            new(new ContactInterval64(lo,hi).Square()-.25,new ContactInterval64(2*lo,2*hi));
        var accepted=ContactLoadSolver64.SolveCertified(0,1,1e-8,At,Enclose);
        Assert.Equal(.5,accepted.Ax,7);
        var atRest=ContactLoadSolver64.SolveCertified(-1,3,1e-8,
            x=>new ContactResidual64<int>(x,Math.Exp(x)-1,0),
            (lo,hi,_)=>new(new(Math.Exp(lo)-1,Math.Exp(hi)-1),new(Math.Exp(lo),Math.Exp(hi))));
        Assert.Equal(0d,atRest.Ax);
        Assert.Equal(ContactRootStatus.AmbiguousOrMultipleRoots,Assert.Throws<ContactRootException>(
            ()=>ContactLoadSolver64.SolveCertified(-1,1,1e-8,At,Enclose)).Status);
        Assert.Equal(ContactRootStatus.NoPhysicallyAdmissibleRoot,Assert.Throws<ContactRootException>(
            ()=>ContactLoadSolver64.SolveCertified(-.25,.25,1e-8,At,Enclose)).Status);
        Assert.Equal(ContactRootStatus.AmbiguousOrMultipleRoots,Assert.Throws<ContactRootException>(
            ()=>ContactLoadSolver64.SolveCertified(-1,1,1e-8,At,(_,_,_)=>new(new(-1,1),new(-1,1)))).Status);
    }
}
