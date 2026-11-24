using SharedWorldSpeedway;
using SpeedwayHandling;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class H26PhysicsTests
{
    [Fact]
    public void SagittalPostureTransfersStaticLoadThroughFixedWheelbase()
    {
        var neutral = TwoContactBikeParameters.Default;
        const double massFraction = .46;
        const double displacement = .15;
        var front = HandlingContact.AxlesAtForeAft(neutral, massFraction, displacement);
        var rear = HandlingContact.AxlesAtForeAft(neutral, massFraction, -displacement);
        double wheelbase = (double)neutral.FrontAxleFromComMeters + neutral.RearAxleFromComMeters;
        var forwardLoad = DynamicAxleLoad.AtAcceleration(0, front);
        var neutralLoad = DynamicAxleLoad.AtAcceleration(0, neutral);
        var rearLoad = DynamicAxleLoad.AtAcceleration(0, rear);
        Assert.Equal(wheelbase, (double)front.FrontAxleFromComMeters + front.RearAxleFromComMeters, 6);
        Assert.Equal(wheelbase, (double)rear.FrontAxleFromComMeters + rear.RearAxleFromComMeters, 6);
        Assert.Equal(1, forwardLoad.FrontFraction + forwardLoad.RearFraction, 14);
        Assert.Equal(1, rearLoad.FrontFraction + rearLoad.RearFraction, 14);
        Assert.Equal(massFraction * displacement / wheelbase, forwardLoad.FrontFraction - neutralLoad.FrontFraction, 6);
        Assert.Equal(massFraction * displacement / wheelbase, neutralLoad.FrontFraction - rearLoad.FrontFraction, 6);
        Assert.Throws<ArgumentException>(() => HandlingContact.AxlesAtForeAft(neutral, massFraction, .19));
    }

    [Fact]
    public void LoadTransferConservesWeightAndRejectsWheelLiftAsOutsideAssumptions()
    {
        var geometry=TwoContactBikeParameters.Default;
        var random=new Random(202);
        double neutral=DynamicAxleLoad.AtAcceleration(0,geometry).RearFraction;
        for(int i=0;i<3000;i++){
            double ax=random.NextDouble()*16-8,h=.45+random.NextDouble()*.2;
            var split=DynamicAxleLoad.AtAcceleration(ax,geometry,h);
            Assert.True(split.WithinAssumptions);
            Assert.Equal(1,split.FrontFraction+split.RearFraction,14);
            Assert.Equal(Math.Sign(ax),Math.Sign(split.RearFraction-neutral));
        }
        Assert.False(DynamicAxleLoad.AtAcceleration(25,geometry).WithinAssumptions);
        Assert.False(DynamicAxleLoad.AtAcceleration(-25,geometry).WithinAssumptions);
    }

    [Fact]
    public void H26EngineIsPassiveBidirectionalAndRetainsFullThrottleBrakeMap()
    {
        var random=new Random(2026);
        int braking=0,drive=0;
        for(int i=0;i<1800;i++){
            var loss=new EngineLossH26(Scale:.5+random.NextDouble());
            double omega=60+random.NextDouble()*1350;
            double gas=i%5==0?0:i%5==1?1:random.NextDouble();
            float dt=i%3==0?.005f:i%3==1?.01f:.02f;
            double ratio=8+random.NextDouble()*2;
            double wheelOmega=omega/ratio*(.6+random.NextDouble()*.8);
            double speed=wheelOmega*.33;
            var wheel=RearWheelParameters.Default;
            var shaft=new CrankClutch144(omega:omega,lossH26:loss);
            shaft.Prepare(gas,ratio,.94,dt,true,torque=>RearWheelDynamics.Evaluate(speed,0,wheelOmega,
                2300,147,1,torque,Math.Abs(torque),torque,-100,dt,wheel,rearBudgetNewtons:1200),wheel);
            var p=shaft.Prepared;
            Assert.InRange(Math.Abs(p.EnergyResidualJoules),0,1e-6);
            Assert.True(p.ClutchHeatJoules>=-1e-7&&p.TransmissionLossJoules>=-1e-7);
            Assert.True(p.H26!.MechanicalWorkJoules>=0&&p.H26.PumpingWorkJoules>=0);
            Assert.Equal(loss,shaft.Copy().LossH26);
            Assert.Equal(Engine500.Torque(omega*30/Math.PI),
                Engine500.Torque(omega*30/Math.PI)-loss.NetViscosity(1)*omega);
            if(p.ClutchTorque<0)braking++;else drive++;
            if(gas==0)Assert.Equal(0,p.H26.GrossCombustionWorkJoules);
            shaft.Commit();Assert.True(shaft.Omega>=0);
        }
        Assert.True(braking>0&&drive>0);
    }

    [Fact]
    public void H26ConfigurationDoesNotSilentlyDoubleCountLegacyDrag()
    {
        Assert.Throws<ArgumentException>(()=>new CrankClutch144(new(CrankDragNewtonMeterSeconds:.008),lossH26:new()));
        Assert.Null(new CrankClutch144().LossH26);
        Assert.Throws<ArgumentException>(()=>new EngineLossH26(Scale:double.NaN).Validate());
        Assert.Throws<ArgumentException>(()=>new H26Configuration(true,ComHeightMeters:0).Validate());
    }
}
