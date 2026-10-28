using System.IO.Compression;
using System.Text.Json;
using Autonomous146;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class AutonomousPlanning146Tests{
    static Run144 Saved(){using var z=new GZipStream(File.OpenRead(Path.Combine(PhysicalSteeringAudit.PreviousPath("../standing-start-three-laps"),"native/K1.json.gz")),CompressionMode.Decompress);return JsonSerializer.Deserialize<Run144>(z,FreeLearningExperiment.Json)!;}
    [Fact] public void ExperimentalPlannerIsOffWithoutExplicitOptIn(){
        var f=StandingWorld144.Frame();
        Assert.Throws<ArgumentException>(()=>new Planner146(StandingWorld144.Rider(CoreSim.StartingGate.B),f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters(),new(Enabled:true,SearchIntervalSeconds:double.NaN)));
        Assert.Throws<InvalidOperationException>(()=>new Planner146(StandingWorld144.Rider(CoreSim.StartingGate.B),f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters()));
    }
    [Fact] public void ManeuverGoalIncludesNextBendAndItsFollowingStraight(){
        var f=StandingWorld144.Frame();Assert.Equal(f.SegmentStartMeters(5)-3,Planner146.Goal(f,0,3),8);
        Assert.Equal(f.LapMeters+f.SegmentStartMeters(1)-3,Planner146.Goal(f,f.SegmentStartMeters(5)-2,3),8);
        Assert.Equal(3*f.LapMeters,Planner146.Goal(f,2*f.LapMeters+f.SegmentStartMeters(5),3),8);
    }
    [Fact] public void ComposedForecastKeepsIrrevocableQueueCrankAndActuator(){
        var run=Saved();var s=run.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-29))!;
        var input=s.Input with{Time=29,NextReview=29};var before=JsonSerializer.Serialize(input,FreeLearningExperiment.Json);
        var f=StandingWorld144.Frame();var m=new Model146(run.Riders[0].Spec,f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters());
        var a=new FreeRacingAction(.1f,0);var b=new FreeRacingAction(-.1f,1);
        var first=m.Step(new(input,s.CrankOmega),a);Assert.Contains(first.End.Input.Pending,c=>c.Action==a&&Math.Abs(c.ActivationAt-29.24)<1e-8);
        var second=m.Step(first.End,b);Assert.Equal(a,second.End.Input.Active);
        Assert.Contains(second.End.Input.Pending,c=>c.Action==b&&Math.Abs(c.ActivationAt-29.44)<1e-8);
        Assert.Equal(before,JsonSerializer.Serialize(input,FreeLearningExperiment.Json));
        Assert.InRange(Math.Abs(first.End.Input.Steering-input.Steering),0,(float)(Math.PI/2*.2)+1e-5f);
        Assert.True(double.IsFinite(second.End.CrankOmega));Assert.Equal(2,m.Forecasts);
    }
    [Fact] public void IndependentScreenRejectsCrossingBetaAndUncertifiedSweep(){
        var r=Saved().Riders[0].Samples[400].Input.Rider;
        var a=new FreeRacingPoint(0,r,1,0,.3,1);var b=a with{Time=.01};
        Assert.True(Planner146.Check(new[]{a,b},.1).Legal);
        Assert.False(Planner146.Check(new[]{a,b with{Inner=-.01}},.1).Legal);
        Assert.False(Planner146.Check(new[]{a,b with{Rider=r with{Heading=(float)(r.Heading+Math.PI)}}},.1).Legal);
        Assert.False(Planner146.Check(new[]{a with{Inner=.11},b with{Time=.02,Inner=.11}},.1).Legal);
    }
    [Fact] public void NativeDiagnosisMustNotClaimPairConsistencyAloneSolvesFailure(){
        var path=Path.Combine(PhysicalSteeringAudit.PreviousPath("../autonomous-corner-planning"),"stage-A/sequence-checks.json");
        using var d=JsonDocument.Parse(File.ReadAllText(path));var rows=d.RootElement.GetProperty("Checks").EnumerateArray().ToArray();
        Assert.Equal(3,rows.Length);Assert.All(rows,x=>Assert.True(x.GetProperty("PrefixExact").GetBoolean()));
        Assert.Equal("confirmed footprint crossing",rows[1].GetProperty("Outcome").GetString());
        Assert.Equal("experimental beta45 envelope",rows[2].GetProperty("Outcome").GetString());
        Assert.All(rows.Skip(1),x=>Assert.True(x.GetProperty("Agreement").GetProperty("Passed").GetBoolean()));
    }
    [Fact] public void ForecastBudgetStopsBeforeMutatingAnAcceptedObservation(){
        var run=Saved();var s=run.Riders[0].Samples[400];var before=JsonSerializer.Serialize(s.Input,FreeLearningExperiment.Json);
        var f=StandingWorld144.Frame();var model=new Model146(run.Riders[0].Spec,f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters(),budgetGuard:()=>throw new InvalidOperationException("forecast budget"));
        Assert.Throws<InvalidOperationException>(()=>model.Step(new(s.Input,s.CrankOmega),new(0,1)));
        Assert.Equal(0,model.Forecasts);Assert.Equal(before,JsonSerializer.Serialize(s.Input,FreeLearningExperiment.Json));
    }

    [Fact] public void DeterministicForecastCapRejectsUnfinishedSearchWithoutSubmittingAnAction(){
        var run=Saved();var s=run.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-29))!;var shaft=new CrankClutch144(omega:s.CrankOmega);
        var f=StandingWorld144.Frame();var planner=new Planner146(run.Riders[0].Spec,f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters(),new(Enabled:true,MaximumForecastsPerReview:32));
        var error=Assert.Throws<InvalidOperationException>(()=>planner.Choose(s.Input with{Time=29,NextReview=29},shaft));
        Assert.Contains("STUDY146_BUDGET",error.Message);Assert.Equal(s.CrankOmega,shaft.Omega);Assert.Null(shaft.Prepared);Assert.Empty(planner.Reviews);
    }

}
