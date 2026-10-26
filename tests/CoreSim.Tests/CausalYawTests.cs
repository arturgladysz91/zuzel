using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;
[Trait("Shard","core")]
public class CausalYawTests
{
    [Fact] public void EveryPlannedCommandPaysAndIdenticalRequestsCoalesce(){
        var s=CausalYawExperiment137.State(CausalYawExperiment137.Diagnostic[0],"exit1-10");var policy=CausalYawExperiment137.Policy;
        var first=new FreeRacingAction(.02f,(float)policy.Drive);var single=CausalYawPlanner137.ActionCosts(s.Input,policy,new[]{first});
        Assert.Equal(single,CausalYawPlanner137.ActionCosts(s.Input,policy,new[]{first,first}));
        var full=CausalYawPlanner137.ActionCosts(s.Input,policy,new[]{first,first with{Drive=1}});
        Assert.True(full.Throttle>.9);Assert.Equal(single.Steering,full.Steering);
        var turn=CausalYawPlanner137.ActionCosts(s.Input,policy,new[]{first,first with{SteeringRadians=-.02f}});Assert.True(turn.Steering>single.Steering);
    }
    [Theory][InlineData(0)][InlineData(1)] public void SharedPrefixMatchesOriginalCausalCallbackAndKeepsPending(int index){
        var state=CausalYawExperiment137.State(CausalYawExperiment137.Diagnostic[index],"exit1-10");var search=CausalYawExperiment137.Search(state);
        var f=TrackAwarenessExperiment134.Frame(state.Case.Geometry);var spec=FreeLearningExperiment.Rider(state.Case.Scenario);var surface=MultiActionExperiment135.Surface(f);
        foreach(var arm in new[]{"B","D"}){
            var leaf=search.Best[arm];FreeRacingInput? observed=null;
            var reference=CausalYawPlanner137.ReferencePair(state.Input,leaf.Actions,spec,f,surface,FreeLearningExperiment.Parameters(),q=>observed=q);
            Assert.Equal(observed!.Pending,leaf.SecondObservation!.Pending);Assert.Equal(state.Time+.2,observed.Time,8);
            foreach(var q in leaf.Prediction.Points){var r=reference.Points.MinBy(x=>Math.Abs(x.Time-q.Time))!;
                Assert.InRange(System.Numerics.Vector2.Distance(q.Rider.Position,r.Rider.Position),0,.001);
                Assert.InRange(Math.Abs(q.Omega-r.Omega),0,.001);Assert.Equal(q.Rider.Drive,r.Rider.Drive);}
        }
        Assert.InRange(search.Roots,1,5);Assert.InRange(search.MaximumCorrections,1,7);Assert.InRange(search.Pairs.Length,1,35);
    }
    [Fact] public void BaselineUtilityIsExactly134AndNoNativeOutcomeEntersSearch(){
        var state=CausalYawExperiment137.State(CausalYawExperiment137.Diagnostic[0],"middle1");var search=CausalYawExperiment137.Search(state);
        var f=TrackAwarenessExperiment134.Frame(state.Case.Geometry);var old=MultiActionPlanner135.Baseline(state.Input,CausalYawExperiment137.Policy,FreeLearningExperiment.Rider(state.Case.Scenario),f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters());
        Assert.Equal(old.Action,search.Best["A"].Actions[0]);Assert.Equal(old.Candidates.Single(x=>x.Action==old.Action),search.Best["A"].Evaluation);
        Assert.All(search.Pairs,x=>Assert.Equal(state.Time+.2,x.SecondObservation!.Time,8));
        Assert.False(new WorldParameters().EnableFreeRacingLearning);
    }
    [Fact] public void MeanYawRetainsTheOriginalUnitsForConstantError(){
        var state=CausalYawExperiment137.State(CausalYawExperiment137.Diagnostic[0],"straight+10");var f=TrackAwarenessExperiment134.Frame(state.Case.Geometry);
        var policy=CausalYawExperiment137.Policy with{PreviewSeconds=0};var v=state.Input.Rider with{AngularVelocity=.7f};
        var points=new[]{0d,.2,.4,.6,.9}.Select(t=>new FreeRacingPoint(t,v,state.Input.Omega,0,2,2)).ToArray();var p=new FreeRacingPrediction(points,0,0);
        Assert.Equal(CausalYawPlanner137.YawCost(p,policy,f,false),CausalYawPlanner137.YawCost(p,policy,f,true),10);
    }
}
