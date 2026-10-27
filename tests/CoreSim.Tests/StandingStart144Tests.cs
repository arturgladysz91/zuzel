using System.Numerics;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class StandingStart144Tests
{
    [Fact]
    public void ExperimentalLaunchIsOffByDefaultAndOriginalRollingGuardRemains(){
        Assert.Throws<InvalidOperationException>(()=>new StandingWorld144());
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{StandingWorld144.Rider(CoreSim.StartingGate.B)},parameters:FreeLearningExperiment.Parameters()));
    }
    [Fact]
    public void StationaryBodyAndWheelReceiveNoForceBeforeReactionAndCrankHasAccountedEnergy(){
        var frame=StandingWorld144.Frame();var spec=StandingWorld144.Rider(CoreSim.StartingGate.B);var at=frame.At(0,spec.OffsetMeters);
        var view=new RiderView(1,at.Position,Vector2.Zero,(float)at.Road.Heading,0,at.Road,0,0,RiderActivity.Racing,0,spec.OffsetMeters,0);
        var engine=new CrankClutch144();var wheel=new RearWheelState(0);var steering=new TwoContactBikeState();var p=FreeLearningExperiment.Parameters();
        var initial=engine.Omega;var losses=0d;
        for(int i=0;i<24;i++){
            var f=Contact144.Forces(view,spec,frame,MultiActionExperiment135.Surface(frame),p,i*.01,147,wheel,steering,.01f,steeringOverride:0,inertia:52.72097f,powertrain:engine,releaseAt:.24);
            Assert.Equal(Vector2.Zero,f.NetForce);Assert.Equal(0,f.NetTorque);Assert.Equal(0,engine.Prepared.ClutchTorque);Assert.Equal(0,wheel.Prepared.AfterOmega);
            Assert.InRange(Math.Abs(engine.Prepared.EnergyResidualJoules),0,1e-8);losses+=engine.Prepared.CrankDragWorkJoules;engine.Commit();wheel.Commit();steering.Commit();
        }
        Assert.Equal(.5*engine.Parameters.CrankInertiaKgMetersSquared*(initial*initial-engine.Omega*engine.Omega),losses,7);
        var force=Contact144.Forces(view with{Drive=1},spec,frame,MultiActionExperiment135.Surface(frame),p,.24,147,wheel,steering,.01f,steeringOverride:0,inertia:52.72097f,powertrain:engine,releaseAt:.24);
        Assert.True(force.RearLongitudinal>0);Assert.True(wheel.Prepared.AfterOmega>0);Assert.True(engine.Prepared.ClutchHeatJoules>=0);Assert.True(engine.Prepared.TransmissionLossJoules>=0);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DisabledLaunchPredictorIsExactlyOriginalAndLaunchPredictionsDoNotMutateAcceptedCrank(int index){
        var c=CausalYawExperiment137.Diagnostic[index];var state=CausalYawExperiment137.State(c,"exit1-10");var frame=TrackAwarenessExperiment134.Frame(c.Geometry);
        var spec=FreeLearningExperiment.Rider(c.Scenario);var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(frame);
        var action=new FreeRacingAction(0,1);
        var old=FreeRacingModel.Predict(state.Input,spec,frame,surface,p,147,52.72097f,.4,action);
        var same=Predictor144.Predict(state.Input,spec,frame,surface,p,147,52.72097f,.4,action);
        Assert.Equal(old.Points,same.Points);Assert.Equal(old.ForceEvaluations,same.ForceEvaluations);
        var engine=new CrankClutch144(omega:state.Input.Omega*9);var omega=engine.Omega;
        var launch=Predictor144.Predict(state.Input,spec,frame,surface,p,147,52.72097f,.4,action,powertrain:engine);
        Assert.Equal(omega,engine.Omega);Assert.Null(engine.Prepared);Assert.Equal(old.Points.Length,launch.Points.Length);
    }
    [Theory]
    [InlineData(0,0)]
    [InlineData(0,1)]
    [InlineData(65,0)]
    [InlineData(65,1)]
    public void PassiveCouplingAndEngineMapRetainEnergyIdentity(double wheelOmega,double gas){
        var shaft=new CrankClutch144();var p=RearWheelParameters.Default;
        shaft.Prepare(gas,9,.94,.01,true,torque=>RearWheelDynamics.Evaluate(15,0,wheelOmega,1850,147,2,torque,Math.Abs(torque),torque,-70,.01f,p,1110));
        Assert.InRange(Math.Abs(shaft.Prepared.EnergyResidualJoules),0,1e-7);Assert.True(shaft.Prepared.ClutchHeatJoules>=0);Assert.True(shaft.Prepared.TransmissionLossJoules>=0);
        Assert.Equal(Engine500.Torque(shaft.Prepared.MidOmega*30/Math.PI)*gas,shaft.Prepared.CrankTorque,12);
    }
    [Fact]
    public void MainCouplingHasNoExtraEngineDragAndSmallSteadySlipAtOriginalPeakTorque(){
        var p=LaunchParameters144.Default;
        Assert.Equal(0,p.CrankDragNewtonMeterSeconds);
        var peak=Engine500.Curve.Max(q=>q.NewtonMeters);
        Assert.True(p.ClutchCapacityNewtonMeters>peak);
        Assert.True(peak/p.ClutchViscosityNewtonMeterSeconds<.005*8500*Math.PI/30);
    }
}
