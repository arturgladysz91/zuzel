using System.Text.Json;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class RacingConstraintsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AExactlyMatches142AndGlobalRemovalsUseTheSamePhysicalCandidates(int index){
        var c=CausalYawExperiment137.Diagnostic[index];var state=CausalYawExperiment137.State(c,"exit1-10");
        var sample=new NativeCase139(c.Name,"unit",c,1,"","");var controller=new Controller143(sample);var frame=TrackAwarenessExperiment134.Frame(c.Geometry);
        var original=Objective142.Choose(state.Input,"A",FreeLearningExperiment.Rider(c.Scenario),frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters());
        var actual=controller.Choose(state.Input,Experiment143.Isolated[0]);
        Assert.Equal(original.Decision,actual.Decision with{Candidates=original.Decision.Candidates});
        Assert.Equal(original.Decision.Candidates,actual.Decision.Candidates);
        var before=JsonSerializer.Serialize(state.Input);
        var rows=controller.Evaluate(state.Input,Experiment143.Isolated[0]);
        foreach(var row in rows){
            foreach(var (id,term) in new[]{("B",row.Original.Terms.Throttle),("C",row.Original.Terms.Direction),("D",row.Original.Terms.Yaw),("E",row.Original.Terms.Steering),("F",row.Original.Terms.Geometry)}){
                var score=Controller143.Score(row.Original,Experiment143.Isolated.Single(v=>v.Id==id));
                Assert.Equal(row.Original.Candidate.Score-term,score.Score);Assert.Equal(row.Original.Candidate.Safety,score.Safety);
            }
        }
        Assert.Equal(before,JsonSerializer.Serialize(state.Input));
    }
    [Fact]
    public void RecoveryIsBoundedCausalAndNeverAddsAFirstActionOrChangesInput(){
        var c=CausalYawExperiment137.Diagnostic[0];var state=CausalYawExperiment137.State(c,"exit1-20");var before=JsonSerializer.Serialize(state.Input);
        var result=new Controller143(new(c.Name,"unit",c,1,"","")).Choose(state.Input,Experiment143.Isolated.Single(v=>v.Id=="R"));
        Assert.Contains(result.Decision.Action,FreeLearningController.Actions(state.Input,PhysicsExperiment139.Policy,true));
        Assert.InRange(result.Forecasts,FreeLearningController.Actions(state.Input,PhysicsExperiment139.Policy,true).Length,FreeLearningController.Actions(state.Input,PhysicsExperiment139.Policy,true).Length+4);
        if(result.RecoveryObservedAt is{} time)Assert.Equal(state.Input.Time+.8,time,8);
        Assert.Equal(before,JsonSerializer.Serialize(state.Input));
    }
}
