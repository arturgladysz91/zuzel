using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class ExtendedLearningTests
{
    [Fact] public void NewFinalStatesAreDisjointAndHaveLegalWholeFootprints(){
        var final=ExtendedLearningExperiment.FinalSingle.Concat(ExtendedLearningExperiment.FinalTwo).ToArray();
        var others=ExtendedLearningExperiment.Training.Concat(ExtendedLearningExperiment.Selection).Concat(RobustLearningExperiment.SealedTest)
            .Concat(RobustLearningExperiment.Selection).Concat(RobustLearningExperiment.High).Concat(RobustLearningExperiment.Medium)
            .Concat(RobustLearningExperiment.Retention).Concat(FreeLearningExperiment.Validation).Concat(Enumerable.Range(1,4).SelectMany(FreeLearningExperiment.Scenarios));
        static string Key(FreeLearningScenario s)=>$"{s.Speed:R}|{s.Offset:R}|{s.Start:R}|{s.OutwardSpeed:R}|{s.HeadingError:R}|{s.YawRate:R}";
        Assert.Equal(24,ExtendedLearningExperiment.FinalSingle.Length);Assert.Equal(12,ExtendedLearningExperiment.FinalTwo.Length);
        Assert.Equal(36,final.Select(Key).Distinct().Count());Assert.Empty(final.Select(Key).Intersect(others.Select(Key)));
        var frame=RacingLineRootCauseAudit.Frame();var geometry=new RacingLineGeometry(frame);
        foreach(var s in final.Concat(ExtendedLearningExperiment.Training).Concat(ExtendedLearningExperiment.Selection)){
            var at=frame.At(s.Start,s.Offset);var rider=new RiderView(1,at.Position,at.Road.Tangent*s.Speed+at.Road.Outward*s.OutwardSpeed,
                (float)at.Road.Heading+s.HeadingError,s.YawRate,at.Road,s.Start,0,RiderActivity.Racing,1,s.Offset,0);
            var gap=geometry.Clearance(rider);Assert.True(gap.Inner>0&&gap.Outer>0,s.Name);
        }
    }
    [Fact] public void HorizonNormalizationChangesOnlyTheIntegratedProgressTerm(){
        var run=FreeLearningExperiment.ReadEpisode(PhysicalSteeringAudit.PreviousPath("../free-racing-learning"),"final-holdout-14-5");
        var input=run.Samples[0].Input;var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        FreeLearningDecision Decide(double h,bool normalized)=>FreeLearningController.Choose(input,run.Policy,FreeLearningExperiment.Rider(run.Scenario),frame,surface,FreeLearningExperiment.Parameters(),147,52.72097f,h,normalized);
        var old=Decide(.6,false);var same=Decide(.6,true);Assert.Equal(old.Action,same.Action);
        Assert.Equal(old.Candidates.Select(c=>c.Score),same.Candidates.Select(c=>c.Score));
        var raw=Decide(.9,false);var normalized=Decide(.9,true);Assert.Equal(raw.Candidates.Select(c=>c.Action),normalized.Candidates.Select(c=>c.Action));
        foreach(var (a,b) in raw.Candidates.Zip(normalized.Candidates)){
            Assert.Equal(a.Safety,b.Safety);Assert.Equal(run.Policy.ProgressWeight*a.Progress/3,b.Score-a.Score,8);
        }
    }
    [Fact] public void TrainingDrawsAreDeterministicAndSeedsAreDistinct(){
        var initial=ExtendedLearningExperiment.StartPolicy();var mean=initial.Values;var std=mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
        FreeLearningPolicy[] Draw(uint seed){var r=new FreeRacingExperiment.RandomStream(seed);return Enumerable.Range(0,5).Select(i=>RobustLearningExperiment.DrawPolicy(r,initial,mean,std,i)).ToArray();}
        Assert.Equal(Draw(1312026),Draw(1312026));Assert.NotEqual(Draw(1312026)[1],Draw(2312026)[1]);
        Assert.Equal(ExtendedLearningExperiment.Schedule(0),ExtendedLearningExperiment.Schedule(2));
    }
    [Fact] public void ContinuousRolloutPreservesQueueAndActuatorsAcrossFirstExit(){
        var scenario=RobustLearningExperiment.Selection[2];var spec=FreeLearningExperiment.Rider(scenario);var p=FreeLearningExperiment.Parameters();
        var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        using var world=new WorldRunner(new[]{spec},parameters:p,trackShape:RacingLineRootCauseAudit.Variant(),frozenSurface:surface,
            twoContactInitial:new(scenario.HeadingError,scenario.YawRate),freeLearningPolicy:ExtendedLearningExperiment.StartPolicy());
        double priorOmega=world.RearWheel(1).RearWheelAngularVelocityRadPerSecond;float priorSteering=0;bool exited=false;
        world.Run("continuous-invariant",.01,8,acceptedObserver:s=>{
            var steering=s.Steering![0].AngleRadians;Assert.InRange(Math.Abs(steering),0,35*Math.PI/180+1e-6);
            Assert.True(Math.Abs(steering-priorSteering)<=.01*90*Math.PI/180+1e-5);priorSteering=steering;
            var omega=world.RearWheel(1).RearWheelAngularVelocityRadPerSecond;Assert.True(omega>=0);priorOmega=omega;
            if(s.Riders[0].TotalReferenceMeters>frame.SegmentStartMeters(4))exited=true;
            Assert.All(world.FreeLearning!.Activations,a=>Assert.Equal(.24,a.ActivationAt-a.ObservedAt,9));
            Assert.All(world.FreeLearning.Input(s.Riders[0],s.TimeSeconds,omega,steering).Pending,a=>Assert.True(a.ActivationAt>=s.TimeSeconds-1e-8));
        });
        Assert.True(exited);Assert.True(priorOmega>0);Assert.True(world.FreeLearning!.Decisions.Count>35);
        Assert.All(world.FreeLearning.Decisions.Zip(world.FreeLearning.Decisions.Skip(1)),x=>Assert.Equal(.2,x.Second.Time-x.First.Time,9));
    }
    [Fact] public void EveryArchivedCemPopulationAndUpdateReconstructsFromTheFrozenSeed(){
        // Math.Cos/Math.Tanh in the inherited sampler are not a cross-runtime bitwise
        // contract. Compare frozen coefficients at1e-12; independently require exact
        // same-runtime replay of every draw. Native predictor tolerances stay frozen.
        static void ArchiveMatches(double[] expected,double[] actual){
            Assert.Equal(expected.Length,actual.Length);
            for(int i=0;i<expected.Length;i++)Assert.InRange(Math.Abs(expected[i]-actual[i]),0,1e-12);
        }
        var directory=PhysicalSteeringAudit.PreviousPath("../autonomous-speedway-extended-learning");
        var starts=new List<(string Label,uint Seed,FreeLearningPolicy Policy)>();
        foreach(var seed in ExtendedLearningExperiment.Seeds)foreach(var horizon in new[]{"06","09"})
            starts.Add(($"H{horizon}-s{seed}",seed,ExtendedLearningExperiment.StartPolicy()));
        starts.Add(("two",3312026,RobustLearningExperiment.Read<ExtendedCandidate>(directory,"ab-winner.json").Policy));
        foreach(var (label,seed,initial) in starts){
            var gs=RobustLearningExperiment.Read<RobustGeneration[]>(directory,"generations-"+label+".json");
            var incumbent=initial;var mean=initial.Values;var std=mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
            var random=new FreeRacingExperiment.RandomStream(seed);var repeatedRandom=new FreeRacingExperiment.RandomStream(seed);
            foreach(var generation in gs){
                for(int c=0;c<5;c++){
                    var policy=RobustLearningExperiment.DrawPolicy(random,incumbent,mean,std,c);
                    Assert.Equal(policy,RobustLearningExperiment.DrawPolicy(repeatedRandom,incumbent,mean,std,c));
                    ArchiveMatches(generation.Candidates[c].Policy.Values,policy.Values);
                }
                var ranked=RobustRanking.Rank(generation.Candidates).ToArray();Assert.Equal(generation.BestId,ranked[0].Id);incumbent=ranked[0].Policy;
                for(int i=0;i<8;i++){var m=ranked.Take(2).Average(c=>c.Policy.Values[i]);mean[i]=.4*mean[i]+.6*m;std[i]=Math.Max(.15*(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i]),Math.Sqrt(ranked.Take(2).Average(c=>Math.Pow(c.Policy.Values[i]-m,2))));}
                ArchiveMatches(generation.Mean,mean);ArchiveMatches(generation.Std,std);Assert.Equal(generation.BestPolicy,incumbent);
            }
        }
    }
}
