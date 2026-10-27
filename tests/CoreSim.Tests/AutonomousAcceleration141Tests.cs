using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;

[Trait("Shard","core")]
public class AutonomousAcceleration141Tests
{
    [Fact] public void RecoveryStartsFromForecastEndpointWithoutResetOrNativeOracle(){
        // Reading an archived observation creates neither a forecast nor a world.
        var state=ThrottleSteering138.State(ThrottleSteering138.Cases()[0]);var input=state.Input;
        var action=new FreeRacingAction(.03f,1);var time=input.Time+.9;
        var endpoint=new FreeRacingPoint(time,input.Rider with{AngularVelocity=.7f},321,.04f,1,2);
        var forecast=new FreeRacingPrediction(new[]{endpoint},0,0);
        var candidate=new FreeLearningCandidate(action,FreeActionSafety.SafeInHorizon,0,0,1,1,0,0,0);
        var leaf=new AccelerationLeaf141(candidate,forecast,0,0,0,0);
        var spec=FreeLearningExperiment.Rider(PhysicsExperiment139.Cases()[0].Case.Scenario);
        var after=Controller141.AfterForecast(input,leaf,spec);
        Assert.Equal(endpoint.Rider,after.Rider);Assert.Equal(endpoint.Omega,after.Omega);Assert.Equal(endpoint.Steering,after.Steering);
        Assert.Equal(time,after.Time);Assert.Equal(action,after.Active);Assert.Empty(after.Pending);
        Assert.Equal(input.Pending,state.Input.Pending);Assert.NotEqual(input.Rider.AngularVelocity,after.Rider.AngularVelocity);
    }
    [Fact] public void OriginalActionsAndDefaultRemainBoundedAndOff(){
        var state=ThrottleSteering138.State(ThrottleSteering138.Cases()[0]);
        var actions=FreeLearningController.Actions(state.Input,PhysicsExperiment139.Policy,true);
        Assert.All(actions,a=>a.Validate());Assert.InRange(actions.Length,27,34);
        Assert.All(actions,a=>Assert.Contains(a.Drive,new[]{0f,(float)PhysicsExperiment139.Policy.Drive,1f}));
        Assert.False(new WorldParameters().EnableFreeRacingLearning);
        Assert.Equal(.9,Controller141.HorizonSeconds);Assert.Equal(.24,Controller141.ReactionSeconds);Assert.Equal(.2,Controller141.ReviewSeconds);
    }
    [Fact] public void PreviouslyFrozenRuntimeSourcesAreUnchanged(){
        var root=Experiment140.RepoRoot();
        var old=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"docs/experiments/tyre-law-regularization/results.json")))!;
        foreach(var pair in old["Protocol"]!["Sources"]!.AsObject()){
            var source=File.ReadAllText(Path.Combine(root,pair.Key)).Replace("\r\n","\n");
            Assert.Equal(pair.Value!.GetValue<string>(),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))) ;
        }
    }
}
