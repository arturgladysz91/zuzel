using System.Text.Json;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class RobustLearningTests
{
    [Fact]public void BothBranchesShareFourConditionsWithinGenerationAndOnlyBRevisitsSkills(){
        for(var generation=0;generation<15;generation++){
            var a=RobustLearningExperiment.Schedule("A",generation);var b=RobustLearningExperiment.Schedule("B",generation);
            Assert.Equal(4,a.Length);Assert.Equal(4,b.Length);Assert.Equal(4,a.Distinct().Count());Assert.Equal(4,b.Distinct().Count());
            Assert.All(a,s=>Assert.Equal(4,s.Level));Assert.Equal(a.Take(2),b.Take(2));Assert.Contains(b,s=>s.Level==3);Assert.Contains(b,s=>s.Level==2);
            Assert.Equal(b,RobustLearningExperiment.Schedule("B",generation+2));
        }
    }
    [Fact]public void TrainSelectionAndSealedTestAreDisjointIncludingOldHoldoutStates(){
        var training=RobustLearningExperiment.High.Concat(RobustLearningExperiment.Medium).Concat(RobustLearningExperiment.Retention).ToArray();
        var selection=RobustLearningExperiment.Selection;var test=RobustLearningExperiment.SealedTest;
        static (float,float,double) Key(FreeLearningScenario s)=>(s.Speed,s.Offset,s.Start);
        Assert.Equal(12,test.Length);Assert.Empty(training.Select(Key).Intersect(selection.Select(Key)));Assert.Empty(training.Select(Key).Intersect(test.Select(Key)));
        Assert.Empty(selection.Select(Key).Intersect(test.Select(Key)));Assert.Empty(training.Select(Key).Intersect(FreeLearningExperiment.Validation.Select(Key)));
        Assert.Contains(test,s=>s.HeadingError<0);Assert.Contains(test,s=>s.HeadingError>0);Assert.Contains(test,s=>s.OutwardSpeed<0);Assert.Contains(test,s=>s.YawRate>0);
    }
    [Fact]public void InitialFootprintsOfAllFrozenScenariosAreLegalWithoutRunningTheTestSet(){
        var frame=RacingLineRootCauseAudit.Frame();var geometry=new RacingLineGeometry(frame);
        foreach(var s in RobustLearningExperiment.High.Concat(RobustLearningExperiment.Medium).Concat(RobustLearningExperiment.Retention).Concat(RobustLearningExperiment.Selection).Concat(RobustLearningExperiment.SealedTest)){
            var at=frame.At(s.Start,s.Offset);var rider=new RiderView(1,at.Position,at.Road.Tangent*s.Speed+at.Road.Outward*s.OutwardSpeed,
                (float)at.Road.Heading+s.HeadingError,s.YawRate,at.Road,s.Start,0,RiderActivity.Racing,1,s.Offset,0);
            var gap=geometry.Clearance(rider);Assert.True(gap.Inner>0&&gap.Outer>0,s.Name);
        }
    }
    [Fact]public void SameSeedProducesTheSameInitialCemPopulationForAAndB(){
        var seed=new FreeLearningPolicy();var mean=seed.Values;var std=mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
        FreeLearningPolicy[] Draw(){var random=new FreeRacingExperiment.RandomStream(RobustLearningExperiment.Seed);return Enumerable.Range(0,5).Select(i=>RobustLearningExperiment.DrawPolicy(random,seed,mean,std,i)).ToArray();}
        var a=Draw();var b=Draw();Assert.Equal(a,b);Assert.Equal(seed,a[0]);Assert.True(a.Distinct().Count()>=4);Assert.Equal(seed.Values,mean);
    }
    private static RobustMetrics Metric(bool completed,double progress=100,double seconds=8,double clearance=.2,string boundary="none")=>new(new(completed,completed?"completed":"uncertified interval",completed?seconds:null,
        seconds+1,progress,seconds,clearance,12,10,completed?15:null,20,.3,0,2,120,1,0,100,1000,20,.1,"fixture")){BoundaryClass=boundary,ExitYawRate=.1};
    private static RobustEvaluation Evaluation(int id,params RobustMetrics[] scores)=>new("fixture",0,id,new(),Enumerable.Range(0,scores.Length).Select(i=>"case"+i).ToArray(),scores);
    [Fact]public void FourCompletionsBeatAThreeCompletionRecordTime(){
        var safe=Evaluation(1,Enumerable.Repeat(Metric(true,seconds:10),4).ToArray());
        var fast=Evaluation(2,Metric(true,seconds:5),Metric(true,seconds:5),Metric(true,seconds:5),Metric(false,progress:130));
        Assert.Equal(1,RobustRanking.Rank(new[]{fast,safe}).First().Id);Assert.True(double.IsPositiveInfinity(RobustRanking.WorstTime(fast)));
    }
    [Fact]public void WorstFailureProgressBeatsWaitingAndCannotBeHiddenByThreeFastCompletions(){
        var a=Evaluation(1,Metric(false,10,seconds:100),Metric(true),Metric(true),Metric(true));
        var b=Evaluation(2,Metric(false,40,seconds:3),Metric(true),Metric(true),Metric(true));
        Assert.Equal(2,RobustRanking.Rank(new[]{a,b}).First().Id);
    }
    [Fact]public void ComparableTimesCanPreferCertifiedReserveWithoutAnOffsetTarget(){
        var narrow=Evaluation(1,Enumerable.Repeat(Metric(true,seconds:8.011,clearance:.01),4).ToArray());
        var reserve=Evaluation(2,Enumerable.Repeat(Metric(true,seconds:8.019,clearance:.5),4).ToArray());
        Assert.Equal(2,RobustRanking.Rank(new[]{narrow,reserve}).First().Id);
    }
    [Fact]public void DefaultChooserStillReconstructs129ActualDecisions(){
        var run=FreeLearningExperiment.ReadEpisode(PhysicalSteeringAudit.PreviousPath("../free-racing-learning"),"final-holdout-14-5");
        var frame=RacingLineRootCauseAudit.Frame();var surface=CoreSim.TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        foreach(var time in new[]{0d,3.2,5.8}){
            var input=run.Samples.Single(s=>Math.Abs(s.Input.Time-time)<1e-8).Input;
            input=input with{Pending=input.Pending.Where(c=>c.ObservedAt<time-1e-8).ToArray(),NextReview=time};
            var d=FreeLearningController.Choose(input,run.Policy,FreeLearningExperiment.Rider(run.Scenario),frame,surface,FreeLearningExperiment.Parameters(),147,52.72097f);
            Assert.Equal(run.Decisions.Single(d=>Math.Abs(d.Time-time)<1e-8).Action,d.Action);
        }
    }
    [Fact]public void ExtendedAuditHorizonPreservesActionSetAndRejectsAnUnboundedHorizon(){
        var run=FreeLearningExperiment.ReadEpisode(PhysicalSteeringAudit.PreviousPath("../free-racing-learning"),"final-holdout-14-5");
        var input=run.Samples[0].Input;var frame=RacingLineRootCauseAudit.Frame();var surface=CoreSim.TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var spec=FreeLearningExperiment.Rider(run.Scenario);
        FreeLearningDecision Decide(double horizon)=>FreeLearningController.Choose(input,run.Policy,spec,frame,surface,FreeLearningExperiment.Parameters(),147,52.72097f,horizon);
        var a=Decide(.6);var b=Decide(.9);Assert.Equal(a.Candidates.Select(c=>c.Action),b.Candidates.Select(c=>c.Action));Assert.True(b.ForceEvaluations>a.ForceEvaluations);
        Assert.Throws<ArgumentException>(()=>Decide(1));
    }
    [Fact]public void DiagnosticCertificateRetainsAnExtraFloatTubeAndDoesNotChangeMainAcceptance(){
        Assert.Equal(.0005,RobustSweepCertificate.FloatTubeMeters);Assert.Equal(.6,FreeLearningController.HorizonSeconds);Assert.False(new WorldParameters().EnableFreeRacingLearning);
        Assert.DoesNotContain("EnableFreeRacingLearning",JsonSerializer.Serialize(new WorldParameters()));
    }
    [Fact]public void ReservationsEnforceNativeAndCpuBudgetsBeforeAnotherWorld(){
        var directory=Path.Combine(Path.GetTempPath(),"speedway-robust-budget-"+Guid.NewGuid());
        try{using var budget=new RobustBudget(directory);for(var i=0;i<100;i++)budget.Reserve("A"+i,"train-A");
            Assert.Throws<InvalidOperationException>(()=>budget.Reserve("A101","train-A"));
            var n=budget.Reserve("offline","offline",false);budget.Complete(n,RobustBudget.CpuLimitSeconds,0,0);
            Assert.Throws<InvalidOperationException>(()=>budget.Reserve("B0","train-B"));Assert.Equal(100,budget.Entries.Sum(e=>e.NativeWorlds));
        }finally{foreach(var file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}
    }
    [Fact]public void FrozenHistoricalReferencesRemainDistinctAndUnmodified(){
        var policies=new[]{"129-100","129-200","129-final"}.Select(RobustLearningExperiment.Reference).ToArray();Assert.Equal(3,policies.Distinct().Count());
        var values=policies[0].Values;values[0]=0;Assert.NotEqual(values,RobustLearningExperiment.Reference("129-100").Values);
    }
}
