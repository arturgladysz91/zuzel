using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public class MultiActionPlanningTests
{
    static (FreeRacingInput Input,TrackFrame Frame,CoreSim.TrackStateSnapshot Surface,RiderSpec Spec,WorldParameters Parameters) Fixture(){
        var c=MultiActionExperiment135.Cases[0];var f=TrackAwarenessExperiment134.Frame(c.Geometry);var pose=f.At(25,5);var v=new RiderView(1,pose.Position,pose.Road.Tangent*16,(float)pose.Road.Heading,0,pose.Road,25,0,RiderActivity.Racing,1,5,0);
        return(new(v,0,16/.35,0,new(0,1),Array.Empty<FreeRacingCommand>(),0),f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Rider(c.Scenario),FreeLearningExperiment.Parameters());
    }
    [Fact] public void HeldUtilityIsExactlyOriginal134ForEveryPhysicalAction(){
        var x=Fixture();var policy=MultiActionExperiment135.Policy();var old=MultiActionPlanner135.Baseline(x.Input,policy,x.Spec,x.Frame,x.Surface,x.Parameters);
        foreach(var c in old.Candidates){var prediction=FreeRacingModel.Predict(x.Input,x.Spec,x.Frame,x.Surface,x.Parameters,147,52.72097f,.9,c.Action);Assert.Equal(c,MultiActionPlanner135.Evaluate(x.Input,policy,x.Frame,x.Surface,prediction,c.Action));}
    }
    [Fact] public void FutureCommandsRetainReactionPendingAndRateLimit(){
        var x=Fixture();var pending=new FreeRacingCommand(-.2,.04,new(.1f,.25f));var input=x.Input with{Pending=new[]{pending}};
        var actions=new[]{new FreeRacingAction(-.1f,0),new FreeRacingAction(.2f,1),new FreeRacingAction(0,.3f)};
        var prediction=MultiActionPlanner135.Predict(input,actions,x.Spec,x.Frame,x.Surface,x.Parameters);
        foreach(var (at,drive) in new[]{(.04,.25f),(.24,0f),(.44,1f),(.64,.3f)})Assert.Equal(drive,prediction.Points.Single(p=>Math.Abs(p.Time-at)<1e-8).Rider.Drive);
        Assert.Equal(.24,RiderController.ReactionSeconds(x.Spec),8);Assert.Single(input.Pending);Assert.Equal(pending,input.Pending[0]);
        foreach(var (a,b) in prediction.Points.Zip(prediction.Points.Skip(1)))Assert.True(Math.Abs(b.Steering-a.Steering)<=Math.PI/2*(b.Time-a.Time)+1e-6);
        Assert.All(prediction.Points,p=>Assert.InRange(p.Steering,-35*Math.PI/180,35*Math.PI/180));
    }
    [Fact] public void SearchIsBoundedDeterministicAndKeepsFullGas(){
        var x=Fixture();var policy=MultiActionExperiment135.Policy();var a=MultiActionPlanner135.Search(x.Input,policy,x.Spec,x.Frame,x.Surface,x.Parameters);var b=MultiActionPlanner135.Search(x.Input,policy,x.Spec,x.Frame,x.Surface,x.Parameters);
        Assert.InRange(a.Two.Length,1,12);Assert.InRange(a.Three.Length,1,24);Assert.Equal(a.BestTwo.Actions,b.BestTwo.Actions);Assert.Equal(a.BestThree.Actions,b.BestThree.Actions);
        Assert.Equal(a.Two.Select(l=>l.Evaluation),b.Two.Select(l=>l.Evaluation));Assert.Equal(a.Contacts,b.Contacts);
        var roots=MultiActionPlanner135.Roots(x.Input,a.Baseline);Assert.Contains(roots,a=>a.Drive==1);Assert.Contains(roots,a=>a.Drive==0);Assert.Contains(x.Input.Active,roots);
        var allowed=FreeLearningController.Actions(x.Input,policy,true);Assert.All(a.Three.SelectMany(l=>l.Actions),action=>Assert.Contains(action,allowed));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)] public void ExistingMetricGeometryAndStartDiscrepancyRemainUnchanged(int index){
        var f=TrackAwarenessExperiment134.Frame(MultiActionExperiment135.Cases[index].Geometry);Assert.Equal(35,f.SegmentLengthMeters(0));Assert.Equal(62,f.SegmentLengthMeters(4));Assert.Equal(27,f.SegmentLengthMeters(8));Assert.InRange(f.LapMeters,318.7,318.9);
        Assert.InRange(f.At(0,0).Position.Length(),0,.00001);Assert.Equal(12,f.PhysicalWidthMeters(f.RoadAt(10,0)),5);
    }
    [Fact] public void IndependentPoolAndDefaultsRemainSeparate(){
        Assert.Equal(16,MultiActionExperiment135.Slots.Take(3).Sum(x=>x.Length));Assert.Equal(6,MultiActionExperiment135.Slots.Skip(3).Sum(x=>x.Length));Assert.Equal(6,MultiActionExperiment135.Cases.Select(c=>c.Scenario).Distinct().Count());
        Assert.False(new WorldParameters().EnableFreeRacingLearning);Assert.Equal(.9,FreeRacingModel.MaximumHorizonSeconds);Assert.Equal(16015,TrackAwarenessExperiment134.Read<AwarenessPolicy134[]>(MultiActionExperiment135.Historical,"choices.json").Single(c=>c.Policy==MultiActionExperiment135.Policy()).Id);
        Assert.Throws<ArgumentException>(()=>MultiActionPlanner135.Predict(Fixture().Input,Array.Empty<FreeRacingAction>(),Fixture().Spec,Fixture().Frame,Fixture().Surface,Fixture().Parameters));
    }
}
