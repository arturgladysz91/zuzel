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
    [Fact] public void ComparingClockRoundoffDoesNotIgnorePhysicalOrPendingChanges(){
        var input=Input() with{Time=10.799999999999994,NextReview=10.799999999999994};var rounded=input with{Time=10.8,NextReview=10.8};
        Assert.Equal(Clock142.StateHash(input),Clock142.StateHash(rounded));
        Assert.NotEqual(Clock142.StateHash(input),Clock142.StateHash(input with{Omega=1}));
        Assert.NotEqual(Clock142.StateHash(input),Clock142.StateHash(input with{Pending=new[]{new FreeRacingCommand(10.6,10.84,new(0,1))}}));
    }
    [Fact] public void NominalConstraintCheckKeepsActualBoundsAndExperimentalBetaSeparateFromMargins(){
        var archived=ThrottleSteering138.State(ThrottleSteering138.Cases()[0]).Input;
        var rider=archived.Rider with{Heading=(float)Math.Atan2(archived.Rider.Velocity.Y,archived.Rider.Velocity.X)};
        var point=new FreeRacingPoint(archived.Time,rider,archived.Omega,0,.15,.05);
        string? Check(FreeRacingPoint p)=>Objective142.HardViolation(new(new[]{p},0,0));
        Assert.Null(Check(point)); // inside true bounds, despite being Uncertain
        Assert.Equal("predicted footprint boundary crossing",Check(point with{Inner=-.01}));
        Assert.Equal("predicted experimental beta45 envelope",Check(point with{Rider=rider with{Heading=rider.Heading+(float)Math.PI/3}}));
        Assert.Equal("invalid physical state",Check(point with{Omega=double.NaN}));
    }
    [Fact] public void EveryPreviouslyFrozen141RuntimeSourceIsUnchanged(){
        var root=Experiment140.RepoRoot();var previous=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(root,"docs/experiments/autonomous-acceleration/results.json")))!;
        foreach(var item in previous["Protocol"]!["Sources"]!.AsObject())
            Assert.Equal(item.Value!.GetValue<string>(),Offline142.Hash(File.ReadAllText(Path.Combine(root,item.Key))));
    }
}
