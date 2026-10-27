using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;

[Trait("Shard","core")]
public class TyreSeparation140Tests
{
    [Fact] public void MaterialHasNoInertiaTimeStepOrNumericalSettingsAndSharesOneBudget(){
        var names=typeof(TyreLaw140).GetMethod(nameof(TyreLaw140.Evaluate))!.GetParameters().Select(x=>x.Name).ToArray();
        Assert.DoesNotContain(names,x=>x!.Contains("inertia",StringComparison.OrdinalIgnoreCase)||x!.Contains("seconds",StringComparison.OrdinalIgnoreCase)&&x!="forwardMetersPerSecond"&&x!="longitudinalSlipMetersPerSecond"&&x!="lateralSlipMetersPerSecond");
        foreach(var slip in new[]{-1000d,-1,0,1,1000})foreach(var side in new[]{-100d,0,100}){
            var f=TyreLaw140.Evaluate(slip,side,0,700,1.2845);
            Assert.True(double.IsFinite(f.FxNewtons)&&double.IsFinite(f.FyNewtons));
            Assert.True(f.FxNewtons*f.FxNewtons+f.FyNewtons*f.FyNewtons<=f.CapacityNewtons*f.CapacityNewtons*(1+1e-14));
            Assert.True(f.LongitudinalLossWatts>=0&&f.LateralLossWatts>=0);
        }
    }
    [Fact] public void DynamicsRespondToInertiaWhileMaterialResponseIsTheSame(){
        var after=new List<double>();
        foreach(var iw in new[]{.45,.9,1.8}){
            var p=RearWheelParameters.Default with{WheelInertiaKgMetersSquared=iw};
            var s=WheelSolver140.Evaluate(16,3,90,1852,147,300,300,300,-100,.01f,p,776.5,1.2845);
            Assert.InRange(Math.Abs(s.RootResidualMetersPerSecond),0,2e-5);after.Add(s.AfterOmega);
        }
        Assert.Equal(3,after.Distinct().Count());
    }
    [Fact] public void ZeroStepQueriesAreMaterialQueriesAndDoNotCommitWheelMotion(){
        var p=RearWheelParameters.Default;var s=WheelSolver140.Evaluate(16,3,90,1852,147,300,300,300,-100,0,p,776.5,1.2845);
        var f=TyreLaw140.Evaluate(.33*90-16,3,16,776.5,1.2845);
        Assert.Equal(90,s.AfterOmega);Assert.Equal((float)f.FxNewtons,s.LongitudinalNewtons);Assert.Equal((float)f.FyNewtons,s.RearLateralNewtons);
        Assert.Equal(0,s.EngineWorkJoules);Assert.Equal(0,s.FrontLateralNewtons);
    }
    [Fact] public void WheelAndBodyMidpointEnergyLedgerIsConsistent(){
        var grid=System.Text.Json.JsonSerializer.SerializeToElement(Mathematics140.PhysicsGrid(),FreeLearningExperiment.Json);
        Assert.True(grid.GetProperty("Passed").GetBoolean());
    }
    [Fact] public void InvalidMaterialAndInvalidTimeStepsAreRejected(){
        Assert.Throws<ArgumentException>(()=>TyreLaw140.Evaluate(double.NaN,0,0,1,1));
        Assert.Throws<ArgumentException>(()=>TyreLaw140.Evaluate(0,0,0,-1,1));
        Assert.Throws<ArgumentException>(()=>(TyreMaterial140.Reference with{LongitudinalFloorMetersPerSecond=0}).Validate());
        Assert.Throws<ArgumentException>(()=>WheelSolver140.Evaluate(0,0,0,1000,147,0,0,0,0,.03f,RearWheelParameters.Default,700,1));
    }
}
