using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;

[Trait("Shard","core")]
public class RacingObjective142Tests
{
    static FreeRacingInput Input()=>new(default,0,0,0,new(0,.318f),Array.Empty<FreeRacingCommand>(),0);
    static Leaf142 Leaf(float gas,FreeActionSafety safety,double progress,double score,string? hard=null)=>new(
        new(new(0,gas),safety,score,progress,.15,.05,20,0,0),new(-progress,0,0,score+progress,0,0,0,0),hard,20,.5,.2);
    [Fact] public void UncertainIsAMarginNotADnfOrAHardConstraintForC(){
        var safe=Leaf(0,FreeActionSafety.SafeInHorizon,10,0);var margin=Leaf(1,FreeActionSafety.Uncertain,20,-10);
        Assert.Same(safe,Objective142.Select(new[]{safe,margin},Input(),"B"));
        Assert.Same(margin,Objective142.Select(new[]{safe,margin},Input(),"C"));
    }
    [Theory][InlineData("predicted footprint boundary crossing")][InlineData("predicted experimental beta45 envelope")][InlineData("invalid physical state")]
    public void CRejectsHardViolationsAndAbstainsIfEveryForecastIsRejected(string reason){
        var legal=Leaf(.318f,FreeActionSafety.Uncertain,10,0);var bad=Leaf(1,FreeActionSafety.Unsafe,20,-10,reason);
        Assert.Same(legal,Objective142.Select(new[]{bad,legal},Input(),"C"));Assert.Null(Objective142.Select(new[]{bad},Input(),"C"));
    }
    [Fact] public void ProgressRankingRemovesPreferenceWithoutChangingCachedPhysics(){
        var partial=Leaf(.318f,FreeActionSafety.SafeInHorizon,10,0);var full=Leaf(1,FreeActionSafety.SafeInHorizon,12,2);
        var original=new[]{partial,full};Assert.Same(partial,Objective142.Select(original,Input(),"A"));
        Assert.Same(full,Objective142.Select(original,Input(),"B"));Assert.Equal(2,full.Candidate.Score);
        Assert.Same(full,Objective142.Select(original,Input(),"no-throttle"));
    }
    [Fact] public void NewPolicyDoesNotChangeOriginalDefaultsOrActuator(){
        Assert.False(new WorldParameters().EnableFreeRacingLearning);Assert.Equal(.2,FreeLearningController.ReviewSeconds);
        Assert.Equal(.9,MultiActionPlanner135.HorizonSeconds);Assert.Contains("no-yaw",Objective142.Single);
        var p=TwoContactBikeParameters.Default;Assert.InRange(p.MaximumSteeringRadians*180/Math.PI,34.999,35.001);
        Assert.InRange(p.SteeringRateRadiansPerSecond*180/Math.PI,89.999,90.001);
    }
}
