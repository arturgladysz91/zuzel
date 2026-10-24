using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class RiderDecisionTests
{
    [Fact] public void RobustDirectionKeepsLocalCurvatureAndOriginalThreatCost(){
        var scale=RiderDecisionObjective133.DirectionTransitionRadians;
        Assert.Equal(10*Math.PI/180,scale);Assert.Equal(0,RiderDecisionObjective133.DirectionShape(0,FreeActionSafety.SafeInHorizon,1,1));
        var tiny=1e-5;Assert.InRange(Math.Abs(RiderDecisionObjective133.DirectionShape(tiny,FreeActionSafety.SafeInHorizon,1,1)/(10*tiny*tiny)-1),0,1e-8);
        foreach(var error in new[]{.1,.3,.6,1.0}){
            var value=RiderDecisionObjective133.DirectionShape(error,FreeActionSafety.SafeInHorizon,1,1);
            Assert.True(value>0&&value<10*error*error);Assert.Equal(value,RiderDecisionObjective133.DirectionShape(-error,FreeActionSafety.SafeInHorizon,1,1));
            Assert.Equal(10*error*error,RiderDecisionObjective133.DirectionShape(error,FreeActionSafety.Unsafe,1,1),12);
            Assert.Equal(10*error*error,RiderDecisionObjective133.DirectionShape(error,FreeActionSafety.Uncertain,1,1),12);
            Assert.Equal(10*error*error,RiderDecisionObjective133.DirectionShape(error,FreeActionSafety.SafeInHorizon,.199,1),12);
            Assert.Equal(10*error*error,RiderDecisionObjective133.DirectionShape(error,FreeActionSafety.SafeInHorizon,1,0),12);
        }
        Assert.True(RiderDecisionObjective133.DirectionShape(.6,FreeActionSafety.SafeInHorizon,1,1)>RiderDecisionObjective133.DirectionShape(.3,FreeActionSafety.SafeInHorizon,1,1));
    }
    [Fact] public void OffMatchesRecorded132AndBChangesOnlyDirectionCost(){
        var run=RiderDecisionExperiment133.Historical(PhysicalSteeringAudit.PreviousPath("../full-throttle-learning"),"final-132-final-Z01");
        var sample=run.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-10.4)<1e-8);var input=RiderDecisionAudit133.BeforeDecision(sample);var frame=RacingLineRootCauseAudit.Frame();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var spec=FreeLearningExperiment.Rider(run.Replay.Scenario);var p=FreeLearningExperiment.Parameters();
        FreeLearningDecision Choose(bool newer)=>FreeLearningController.Choose(input,run.Replay.Policy,spec,frame,surface,p,147,52.72097f,.9,true,true,newer);
        var original=run.Replay.Decisions.Single(x=>Math.Abs(x.Time-input.Time)<1e-8);var a=Choose(false);var b=Choose(true);
        Assert.Equal(original.Action,a.Action);Assert.Equal(original.Candidates,a.Candidates);Assert.Equal(a.Candidates.Select(c=>c.Action),b.Candidates.Select(c=>c.Action));
        Assert.Equal(a.Forecasts,b.Forecasts);Assert.Equal(a.ForceEvaluations,b.ForceEvaluations);
        foreach(var (old,current) in a.Candidates.Zip(b.Candidates)){
            Assert.Equal(old.Safety,current.Safety);Assert.Equal(old.Progress,current.Progress);Assert.Equal(old.Clearance,current.Clearance);
            var delta=run.Replay.Policy.DirectionWeight*(RiderDecisionObjective133.DirectionShape(old.DirectionError,old.Safety,old.FutureClearance,old.Progress)-old.DirectionError*old.DirectionError*10);
            Assert.Equal(old.Score+delta,current.Score,12);
            var forecast=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,.9,old.Action);
            Assert.Equal(old.Score,RiderDecisionObjective133.Costs(input,run.Replay.Policy,old.Action,forecast,frame,surface,147,.9,true).Total,10);
        }
        Assert.Equal(b.Action,Choose(true).Action);Assert.False(new WorldParameters().EnableFreeRacing);Assert.Equal(.9,FreeRacingModel.MaximumHorizonSeconds);
    }
    [Fact] public void NewFinalInputsAreLegalAndDisjointFrom129Through132(){
        static string Key(FreeLearningScenario s)=>$"{s.Speed:R}|{s.Offset:R}|{s.Start:R}|{s.OutwardSpeed:R}|{s.HeadingError:R}|{s.YawRate:R}";
        var final=RiderDecisionExperiment133.FinalSingle.Concat(RiderDecisionExperiment133.FinalTwo).ToArray();
        var others=RiderDecisionExperiment133.Training.Concat(RiderDecisionExperiment133.Diagnostic).Concat(RiderDecisionExperiment133.Selection)
            .Concat(FullThrottleLearningExperiment.FinalSingle).Concat(FullThrottleLearningExperiment.FinalTwo).Concat(FullThrottleLearningExperiment.Training).Concat(FullThrottleLearningExperiment.Diagnostic).Concat(FullThrottleLearningExperiment.Selection)
            .Concat(ExtendedLearningExperiment.FinalSingle).Concat(ExtendedLearningExperiment.FinalTwo).Concat(ExtendedLearningExperiment.Training).Concat(ExtendedLearningExperiment.Selection)
            .Concat(RobustLearningExperiment.SealedTest).Concat(RobustLearningExperiment.Selection).Concat(RobustLearningExperiment.High).Concat(RobustLearningExperiment.Medium).Concat(RobustLearningExperiment.Retention)
            .Concat(FreeLearningExperiment.Validation).Concat(Enumerable.Range(1,4).SelectMany(FreeLearningExperiment.Scenarios));
        Assert.Equal(36,final.Select(Key).Distinct().Count());Assert.Empty(final.Select(Key).Intersect(others.Select(Key)));
        var frame=RacingLineRootCauseAudit.Frame();var boundary=new RacingLineGeometry(frame);
        foreach(var s in final.Concat(RiderDecisionExperiment133.Training).Concat(RiderDecisionExperiment133.Diagnostic).Concat(RiderDecisionExperiment133.Selection)){
            var at=frame.At(s.Start,s.Offset);var rider=new RiderView(1,at.Position,at.Road.Tangent*s.Speed+at.Road.Outward*s.OutwardSpeed,(float)at.Road.Heading+s.HeadingError,s.YawRate,at.Road,s.Start,0,RiderActivity.Racing,1,s.Offset,0);
            var gap=boundary.Clearance(rider);Assert.True(gap.Inner>0&&gap.Outer>0,s.Name);
        }
    }
    [Fact] public void R1PairsIdenticalStatesAndDoesNotLetOneFastCaseHideRegression(){
        var original=RiderDecisionExperiment133.Historical(PhysicalSteeringAudit.PreviousPath("../full-throttle-learning"),"final-132-final-Z01").Metrics;
        RobustMetrics At(double time)=>original with{Native=original.Native with{Completed=true,GateSeconds=time}};
        var baseline=new RobustEvaluation("baseline",0,0,RiderDecisionExperiment133.StartPolicy(),new[]{"a-single","b-two","c-two","d-two"},new[]{At(10),At(20),At(20),At(20)});
        var balanced=baseline with{Variant="balanced",Id=2,Results=new[]{At(10),At(19),At(19),At(19)}};
        var outlier=baseline with{Variant="outlier",Id=1,Results=new[]{At(10),At(5),At(18),At(24)}};
        Assert.Equal(balanced,RiderDecisionExperiment133.RankR1(new[]{outlier,balanced},baseline).First());
        Assert.Equal(-.05,RiderDecisionExperiment133.TimeScore(balanced,baseline).MedianTwo!.Value,10);
        Assert.False(RiderDecisionExperiment133.TimeScore(outlier,baseline).WithinWorstBound);
        Assert.Throws<ArgumentException>(()=>RiderDecisionExperiment133.TimeScore(balanced with{Scenarios=new[]{"changed"}},baseline));
        var dnf=balanced with{Id=0,Results=new[]{At(1),At(1),At(1),At(1) with{Native=original.Native with{Completed=false,GateSeconds=null,Outcome="control envelope"}}}};
        Assert.Equal(balanced,RiderDecisionExperiment133.RankR1(new[]{dnf,balanced},baseline).First());
    }
    [Fact] public void BothArmsKeepTheIdenticalInheritedCemSamplingAndSchedule(){
        FreeLearningPolicy[] Draw(uint seed){var p=RiderDecisionExperiment133.StartPolicy();var mean=p.Values;var std=mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();var random=new FreeRacingExperiment.RandomStream(seed);
            return Enumerable.Range(0,5).Select(i=>RobustLearningExperiment.DrawPolicy(random,p,mean,std,i)).ToArray();}
        Assert.Equal(Draw(1332026),Draw(1332026));Assert.NotEqual(Draw(1332026)[1],Draw(2332026)[1]);
        Assert.Equal(RiderDecisionExperiment133.Schedule(0),RiderDecisionExperiment133.Schedule(2));Assert.Equal(8,RiderDecisionExperiment133.StartPolicy().Values.Length);
    }
    [Fact] public void ArchivedCemReproducesEveryDrawAndR0EliteUpdateForBothArms(){
        var d=PhysicalSteeringAudit.PreviousPath("../rider-decision-optimization");
        var metadataErrors=new List<string>();
        foreach(var seed in RiderDecisionExperiment133.Seeds)foreach(var arm in new[]{"A","B"}){
            var gs=RiderDecisionExperiment133.Read<RobustGeneration[]>(d,$"generations-{arm}-s{seed}.json");
            Assert.Equal(5,gs.Length);var incumbent=RiderDecisionExperiment133.StartPolicy();var mean=incumbent.Values;
            var std=mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
            var random=new FreeRacingExperiment.RandomStream(seed);var repeat=new FreeRacingExperiment.RandomStream(seed);
            foreach(var g in gs){
                Assert.Equal(5,g.Candidates.Length);
                for(int c=0;c<5;c++){
                    var policy=RobustLearningExperiment.DrawPolicy(random,incumbent,mean,std,c);
                    Assert.Equal(policy,RobustLearningExperiment.DrawPolicy(repeat,incumbent,mean,std,c));
                    Assert.All(policy.Values.Zip(g.Candidates[c].Policy.Values),x=>Assert.InRange(Math.Abs(x.First-x.Second),0,1e-12));
                    Assert.Equal(RiderDecisionExperiment133.Schedule(g.Generation).Select((s,i)=>s.Name+(i>0?"-two":"-single")),g.Candidates[c].Scenarios);
                }
                var ranked=FullThrottleLearningExperiment.Rank(g.Candidates).ToArray();Assert.Equal(g.BestId,ranked[0].Id);incumbent=ranked[0].Policy;
                Assert.Equal(incumbent,g.BestPolicy);
                for(int i=0;i<8;i++){
                    var m=ranked.Take(2).Average(c=>c.Policy.Values[i]);mean[i]=.4*mean[i]+.6*m;
                    std[i]=Math.Max(.15*(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i]),Math.Sqrt(ranked.Take(2).Average(c=>Math.Pow(c.Policy.Values[i]-m,2))));
                    if(Math.Abs(mean[i]-g.Mean[i])>1e-12)metadataErrors.Add($"{arm}-{seed} g{g.Generation} mean[{i}]: expected {mean[i]:R}, stored {g.Mean[i]:R}");
                    if(Math.Abs(std[i]-g.Std[i])>1e-12)metadataErrors.Add($"{arm}-{seed} g{g.Generation} std[{i}]: expected {std[i]:R}, stored {g.Std[i]:R}");
                }
            }
        }
        Assert.True(metadataErrors.Count==0,string.Join(Environment.NewLine,metadataErrors));
    }
    [Fact] public void FrozenSelectionMatrixReproducesBothRankingsOnTheSamePool(){
        var d=PhysicalSteeringAudit.PreviousPath("../rider-decision-optimization");
        var scores=RiderDecisionExperiment133.Read<RobustEvaluation[]>(d,"selection.json");
        var choices=RiderDecisionExperiment133.Read<RiderDecisionCandidate[]>(d,"ranked-choices.json");
        var baseline=scores.Single(s=>s.Variant=="baseline4061-full");
        foreach(var arm in new[]{"A","B"}){
            var pool=scores.Where(s=>s.Variant.StartsWith(arm+"-",StringComparison.Ordinal)).ToArray();Assert.NotEmpty(pool);
            Assert.All(pool,s=>Assert.Equal(baseline.Scenarios,s.Scenarios));
            foreach(var ranking in new[]{0,1}){
                var expected=(ranking==0?FullThrottleLearningExperiment.Rank(pool):RiderDecisionExperiment133.RankR1(pool,baseline)).First();
                var selected=choices.Single(c=>c.Label==arm+ranking);Assert.Equal(expected.Id,selected.Id);Assert.Equal(expected.Policy,selected.Policy);
                Assert.Equal(arm=="B",selected.NewObjective);Assert.True(selected.FullThrottle);Assert.Equal(.9,selected.Horizon);
            }
        }
        var main=RiderDecisionExperiment133.Read<RiderDecisionCandidate>(d,"winner.json");Assert.Equal(choices.Single(c=>c.Label=="B1"),main);
        var seal=RiderDecisionExperiment133.Read<System.Text.Json.JsonElement>(d,"winner-lock.json");Assert.Equal(0,seal.GetProperty("FinalWorlds").GetInt32());
        Assert.Equal(RiderDecisionExperiment133.Hash(main),seal.GetProperty("PolicyHash").GetString());
        Assert.Equal(RiderDecisionExperiment133.Hash(RiderDecisionExperiment133.Read<RiderDecisionCandidate[]>(d,"final-plan.json")),seal.GetProperty("PlanHash").GetString());
    }
    [Fact] public void ResumingCannotMutateTheArchivedDistribution(){
        var d=PhysicalSteeringAudit.PreviousPath("../rider-decision-optimization");
        var saved=RiderDecisionExperiment133.Read<RobustGeneration[]>(d,"generations-A-s1332026.json")[0];
        var originalMean=(double[])saved.Mean.Clone();var originalStd=(double[])saved.Std.Clone();
        var state=RiderDecisionExperiment133.ResumeDistribution(saved);
        state.Mean[0]+=1;state.Std[0]+=.1;
        Assert.Equal(originalMean,saved.Mean);Assert.Equal(originalStd,saved.Std);
        Assert.NotSame(state.Mean,saved.Mean);Assert.NotSame(state.Std,saved.Std);
    }
}
