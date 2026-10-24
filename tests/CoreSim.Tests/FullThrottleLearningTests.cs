using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class FullThrottleLearningTests
{
    private static FreeRacingInput Input(){
        var run=FreeLearningExperiment.ReadEpisode(PhysicalSteeringAudit.PreviousPath("../free-racing-learning"),"final-holdout-14-5");
        return run.Samples[0].Input with{Active=new(0,0),Pending=Array.Empty<FreeRacingCommand>()};
    }
    [Fact] public void DefaultActionsAreExactAndFullThrottleAddsOnlyOneDrivePerExistingAngle(){
        var input=Input();var p=FullThrottleLearningExperiment.StartPolicy();
        var historical=FreeLearningExperiment.ReadEpisode(PhysicalSteeringAudit.PreviousPath("../free-racing-learning"),"final-holdout-14-5");
        Assert.Equal(historical.Decisions[0].Candidates.Select(c=>c.Action),FreeLearningController.Actions(historical.Samples[0].Input,historical.Policy));
        var original=FreeLearningController.Actions(input,p);var angles=original.Select(a=>a.SteeringRadians).Distinct().ToArray();
        Assert.Equal(original,FreeLearningController.Actions(input,p,false));Assert.Equal(9,angles.Length);
        Assert.All(original,a=>Assert.True(a.Drive==0||a.Drive==(float)p.Drive));
        var expanded=FreeLearningController.Actions(input,p,true);Assert.Equal(angles,expanded.Select(a=>a.SteeringRadians).Distinct());
        Assert.All(angles,a=>Assert.Contains(new FreeRacingAction(a,1),expanded));Assert.All(original,a=>Assert.Contains(a,expanded));
        Assert.Equal(expanded.Length,expanded.Distinct().Count());Assert.Equal(expanded,FreeLearningController.Actions(input,p,true));
        Assert.DoesNotContain(FreeLearningController.Actions(input,p),a=>a.Drive==1);Assert.Contains(expanded,a=>a.Drive==1);
        Assert.All(expanded,a=>a.Validate());
    }
    [Fact] public void DuplicateCurrentAngleAndActiveFullThrottleAreRemovedDeterministically(){
        var p=FullThrottleLearningExperiment.StartPolicy();var input=Input() with{Steering=0,Active=new(0,1)};
        var expanded=FreeLearningController.Actions(input,p,true);Assert.Equal(27,expanded.Length);Assert.Equal(1,expanded.Count(a=>a==input.Active));
        Assert.Equal(expanded,FreeLearningController.Actions(input,p,true));
    }
    [Fact] public void BothVariantsUseIdenticalPhysicalForecastAndObjectiveForSharedCandidates(){
        var input=Input();var p=FullThrottleLearningExperiment.StartPolicy();var frame=RacingLineRootCauseAudit.Frame();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var spec=FreeLearningExperiment.Rider(FullThrottleLearningExperiment.Diagnostic[0]);
        FreeLearningDecision Decide(bool full)=>FreeLearningController.Choose(input,p,spec,frame,surface,FreeLearningExperiment.Parameters(),147,52.72097f,.9,true,full);
        var a=Decide(false);var b=Decide(true);Assert.All(a.Candidates,c=>Assert.Equal(c,b.Candidates.Single(x=>x.Action==c.Action)));
        var expected=b.Candidates.OrderBy(c=>c.Safety).ThenBy(c=>c.Score).ThenBy(c=>Math.Abs(c.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(c=>c.Action.SteeringRadians).First();
        Assert.Equal(expected.Action,b.Action);Assert.Equal(expected.Safety,b.Safety);Assert.Equal(b.Action,Decide(true).Action);
        Assert.False(new WorldParameters().EnableFreeRacing);Assert.Equal(.9,FreeRacingModel.MaximumHorizonSeconds);
    }
    [Fact] public void RequestedFullThrottleReopensOnlyAfterFullReactionAndPreservesPending(){
        var scenario=FullThrottleLearningExperiment.Diagnostic[0];var spec=FreeLearningExperiment.Rider(scenario);var frame=RacingLineRootCauseAudit.Frame();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        using var world=new WorldRunner(new[]{spec},parameters:FreeLearningExperiment.Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),frozenSurface:surface,freeLearningPolicy:FullThrottleLearningExperiment.StartPolicy());
        var controller=world.FreeLearning!;
        Func<FreeRacingInput,FreeLearningDecision> chooser=i=>new(i.Time,new(0,i.Time<.4-1e-8?0:1),FreeActionSafety.SafeInHorizon,false,Array.Empty<FreeLearningCandidate>(),0,0);
        typeof(FreeLearningController).GetProperty("DiagnosticChooser",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(controller,chooser);
        var observed=new List<(double Time,float Drive)>();world.Run("queue-only-test",.01,.85,acceptedObserver:s=>{
            observed.Add((s.TimeSeconds,s.Riders[0].Drive));
            Assert.All(controller.Input(s.Riders[0],s.TimeSeconds,world.RearWheel(1).RearWheelAngularVelocityRadPerSecond,s.Steering![0].AngleRadians).Pending,c=>Assert.Equal(.24,c.ActivationAt-c.ObservedAt,10));
            Assert.InRange(Math.Abs(s.Steering[0].AngleRadians),0,35*Math.PI/180+1e-6);
        });
        Assert.All(observed.Where(s=>s.Time<=.24+1e-9),s=>Assert.Equal(1,s.Drive));
        Assert.All(observed.Where(s=>s.Time>.24+1e-9&&s.Time<=.64+1e-9),s=>Assert.Equal(0,s.Drive));
        Assert.All(observed.Where(s=>s.Time>.64+1e-9),s=>Assert.Equal(1,s.Drive));
        Assert.Contains(controller.Activations,c=>Math.Abs(c.ObservedAt-.4)<1e-9&&Math.Abs(c.ActivationAt-.64)<1e-9&&c.Action.Drive==1);
    }
    [Fact] public void NewFinalStatesAreLegalAndDisjointFromAllPreviousExperiments(){
        static string Key(FreeLearningScenario s)=>$"{s.Speed:R}|{s.Offset:R}|{s.Start:R}|{s.OutwardSpeed:R}|{s.HeadingError:R}|{s.YawRate:R}";
        var final=FullThrottleLearningExperiment.FinalSingle.Concat(FullThrottleLearningExperiment.FinalTwo).ToArray();
        var others=FullThrottleLearningExperiment.Training.Concat(FullThrottleLearningExperiment.Selection).Concat(FullThrottleLearningExperiment.Diagnostic)
            .Concat(ExtendedLearningExperiment.FinalSingle).Concat(ExtendedLearningExperiment.FinalTwo).Concat(ExtendedLearningExperiment.Training).Concat(ExtendedLearningExperiment.Selection)
            .Concat(RobustLearningExperiment.SealedTest).Concat(RobustLearningExperiment.Selection).Concat(RobustLearningExperiment.High).Concat(RobustLearningExperiment.Medium).Concat(RobustLearningExperiment.Retention)
            .Concat(FreeLearningExperiment.Validation).Concat(Enumerable.Range(1,4).SelectMany(FreeLearningExperiment.Scenarios));
        Assert.Equal(36,final.Length);Assert.Equal(36,final.Select(Key).Distinct().Count());Assert.Empty(final.Select(Key).Intersect(others.Select(Key)));
        var frame=RacingLineRootCauseAudit.Frame();var boundary=new RacingLineGeometry(frame);
        foreach(var s in final.Concat(FullThrottleLearningExperiment.Training).Concat(FullThrottleLearningExperiment.Selection).Concat(FullThrottleLearningExperiment.Diagnostic)){
            var at=frame.At(s.Start,s.Offset);var rider=new RiderView(1,at.Position,at.Road.Tangent*s.Speed+at.Road.Outward*s.OutwardSpeed,(float)at.Road.Heading+s.HeadingError,s.YawRate,at.Road,s.Start,0,RiderActivity.Racing,1,s.Offset,0);
            var gap=boundary.Clearance(rider);Assert.True(gap.Inner>0&&gap.Outer>0,s.Name);
        }
    }
    [Fact] public void ArchivedCemDrawsUpdatesAndRankingReproduceForBothSeeds(){
        var directory=PhysicalSteeringAudit.PreviousPath("../full-throttle-learning");
        var labels=FullThrottleLearningExperiment.Seeds.SelectMany(s=>new[]{false,true}.Select(b=>(Label:$"{(b?"B":"A")}-s{s}",Seed:s,Initial:FullThrottleLearningExperiment.StartPolicy()))).ToList();
        if(File.Exists(Path.Combine(directory,"generations-B-continued.json")))labels.Add(("B-continued",3322026,RobustLearningExperiment.Read<ThrottleCandidate>(directory,"continuation-start.json").Policy));
        foreach(var item in labels){
            var generations=RobustLearningExperiment.Read<RobustGeneration[]>(directory,"generations-"+item.Label+".json");
            var incumbent=item.Initial;var mean=incumbent.Values;var std=mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
            var random=new FreeRacingExperiment.RandomStream(item.Seed);var again=new FreeRacingExperiment.RandomStream(item.Seed);
            foreach(var g in generations){
                for(int i=0;i<5;i++){
                    var policy=RobustLearningExperiment.DrawPolicy(random,incumbent,mean,std,i);
                    Assert.Equal(policy,RobustLearningExperiment.DrawPolicy(again,incumbent,mean,std,i));
                    Assert.All(policy.Values.Zip(g.Candidates[i].Policy.Values),x=>Assert.InRange(Math.Abs(x.First-x.Second),0,1e-12));
                }
                var ranked=FullThrottleLearningExperiment.Rank(g.Candidates).ToArray();Assert.Equal(g.BestId,ranked[0].Id);incumbent=ranked[0].Policy;
                Assert.Equal(incumbent,g.BestPolicy);
                for(int i=0;i<8;i++){
                    var m=ranked.Take(2).Average(c=>c.Policy.Values[i]);mean[i]=.4*mean[i]+.6*m;std[i]=Math.Max(.15*(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i]),Math.Sqrt(ranked.Take(2).Average(c=>Math.Pow(c.Policy.Values[i]-m,2))));
                    Assert.InRange(Math.Abs(g.Mean[i]-mean[i]),0,1e-12);Assert.InRange(Math.Abs(g.Std[i]-std[i]),0,1e-12);
                }
            }
        }
    }
    [Fact] public void LegalTwoBendCompletionAlwaysOutranksFastDnf(){
        var d=PhysicalSteeringAudit.PreviousPath("../full-throttle-learning");var original=RobustLearningExperiment.Read<RobustReservation[]>(d,"attempts.json").First(e=>e.Metrics?.Native.Completed==true).Metrics!;
        var complete=new RobustEvaluation("complete",0,1,FullThrottleLearningExperiment.StartPolicy(),new[]{"x-single","y-two"},new[]{original,original});
        var failure=original with{Native=original.Native with{Completed=false,GateSeconds=null,Outcome="control envelope",DurationSeconds=1}};
        var dnf=complete with{Id=0,Results=new[]{original,failure}};
        Assert.Equal(complete,FullThrottleLearningExperiment.Rank(new[]{dnf,complete}).First());
    }
}
