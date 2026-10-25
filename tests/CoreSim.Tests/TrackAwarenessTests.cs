using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class TrackAwarenessTests
{
    private static FreeRacingInput Input(string geometry,double reference=20,float speed=15,float offset=5){
        var frame=TrackAwarenessExperiment134.Frame(geometry);var q=frame.At(reference,offset);
        var rider=new RiderView(1,q.Position,q.Road.Tangent*speed,(float)q.Road.Heading,0,q.Road,reference,0,RiderActivity.Racing,1,offset,0);
        return new(rider,0,speed/.33,0,new(0,1),Array.Empty<FreeRacingCommand>(),0);
    }
    [Fact] public void GeometryFactsAreMetricPeriodicAndReadOnlyAcrossExistingTracks(){
        foreach(var name in new[]{"progressive-b","progressive-c","egg-mild","egg-medium","egg-asymmetric"}){
            var frame=TrackAwarenessExperiment134.Frame(name);var input=Input(name);var p=FreeLearningExperiment.Parameters();var view=TrackAwareness134.Observe(input,frame,p);
            Assert.Equal(15,view.DistanceToNextCornerMeters,9);Assert.Equal(frame.SegmentStartMeters(1),view.NextCornerStartReferenceMeters);
            Assert.Equal(frame.SegmentStartMeters(4)-frame.SegmentStartMeters(1),view.NextCornerLengthMeters,9);Assert.Equal(62,view.FollowingStraightMeters,8);
            Assert.Equal(1,view.NextCornerDirection);Assert.True(view.KnowledgeRangeMeters>=20);Assert.NotEmpty(view.Profile);
            Assert.All(view.Profile,q=>{Assert.True(q.WidthMeters>=12);Assert.True(q.Curvature>=-1e-10);Assert.True(q.ReferenceDistanceMeters>=0);});
            var wrapped=TrackAwareness134.Observe(input with{Rider=input.Rider with{TotalReferenceMeters=frame.LapMeters+20}},frame,p);
            Assert.Equal(view.Profile.Count,wrapped.Profile.Count);
            foreach(var (a,b) in view.Profile.Zip(wrapped.Profile)){Assert.Equal(a.ReferenceMeters,b.ReferenceMeters,8);Assert.Equal(a.Curvature,b.Curvature,10);Assert.Equal(a.WidthMeters,b.WidthMeters,8);}
            Assert.Empty(input.Pending);
            var nearEnd=Input(name,frame.LapMeters-2);var after=TrackAwareness134.Observe(nearEnd,frame,p);Assert.Equal(37,after.DistanceToNextCornerMeters,8);
        }
    }
    [Fact] public void DifferentCurvatureAndWidthRemainFactsWithoutAnIdealLine(){
        var b=TrackAwarenessExperiment134.Frame("progressive-b");var c=TrackAwarenessExperiment134.Frame("progressive-c");var m=TrackAwarenessExperiment134.Frame("egg-medium");
        var s=(b.SegmentStartMeters(1)+b.SegmentStartMeters(4))/2;
        Assert.NotEqual(b.Shape!.At(s).Curvature,m.Shape!.At(s).Curvature);
        Assert.NotEqual(b.PhysicalWidthMeters(b.RoadAt(s,0)),c.PhysicalWidthMeters(c.RoadAt(s,0)));
        var view=TrackAwareness134.Observe(Input("progressive-b"),b,FreeLearningExperiment.Parameters());
        Assert.All(view.Profile,q=>Assert.Equal(b.At(q.ReferenceMeters,0).Position,new Vector2((float)q.X,(float)q.Y)));
        Assert.IsAssignableFrom<IReadOnlyList<TrackKnowledgePoint134>>(view.Profile);
    }
    [Fact] public void SteeringReachBoundIncludesReactionAndDoesNotClaimInstantYaw(){
        var frame=TrackAwarenessExperiment134.Frame("progressive-b");var speed=70/3.6f;var view=TrackAwareness134.Observe(Input("progressive-b",33,speed),frame,FreeLearningExperiment.Parameters());
        Assert.InRange(view.ReactionDistanceMeters,4.66666,4.66668);Assert.Equal(0,view.ReachableSteeringLowRadians);Assert.Equal(0,view.ReachableSteeringHighRadians);
        Assert.InRange(view.MaximumSteeringTravelSeconds,1d/6-1e-6,1d/6+1e-6);
        var farther=TrackAwareness134.Observe(Input("progressive-b",20,speed),frame,FreeLearningExperiment.Parameters());
        Assert.True(farther.ReachableSteeringLowRadians<0&&farther.ReachableSteeringHighRadians>0);
    }
    [Fact] public void OffExactlyMatchesFrozen133A0DecisionAndAllCandidates(){
        var run=RiderDecisionExperiment133.Historical(PhysicalSteeringAudit.PreviousPath("../rider-decision-optimization"),"final-133-A0-X00");
        var input=RiderDecisionAudit133.BeforeDecision(run.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-2)<1e-8));var frame=TrackAwarenessExperiment134.Frame("progressive-b");
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var actual=FreeLearningController.Choose(input,run.Replay.Policy,FreeLearningExperiment.Rider(run.Replay.Scenario),frame,surface,FreeLearningExperiment.Parameters(),147,52.72097f,.9,true,true);
        var saved=run.Replay.Decisions.Single(d=>Math.Abs(d.Time-2)<1e-8);Assert.Equal(saved.Action,actual.Action);Assert.Equal(saved.Candidates,actual.Candidates);Assert.Null(actual.TrackKnowledge);
    }
    [Fact] public void TreatmentChangesOnlyGeometryUtilityOnIdenticalH09Forecasts(){
        var c=TrackAwarenessExperiment134.Diagnostic[1];var input=Input(c.Geometry,c.Scenario.Start,c.Scenario.Speed,c.Scenario.Offset);var frame=TrackAwarenessExperiment134.Frame(c.Geometry);
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var policy=TrackAwarenessExperiment134.StartPolicy();var p=FreeLearningExperiment.Parameters();
        FreeLearningDecision Choose(bool aware)=>FreeLearningController.Choose(input,policy,FreeLearningExperiment.Rider(c.Scenario),frame,surface,p,147,52.72097f,.9,true,true,false,aware);
        var a=Choose(false);var b=Choose(true);Assert.NotNull(b.TrackKnowledge);Assert.Equal(a.Forecasts,b.Forecasts);Assert.Equal(a.ForceEvaluations,b.ForceEvaluations);
        Assert.Equal(a.Candidates.Select(x=>x.Action),b.Candidates.Select(x=>x.Action));Assert.Equal(a.NoSafeContinuation,b.NoSafeContinuation);
        foreach(var (old,current) in a.Candidates.Zip(b.Candidates)){
            Assert.Equal(old.Safety,current.Safety);Assert.Equal(old.Progress,current.Progress);Assert.Equal(old.Clearance,current.Clearance);Assert.Equal(old.FutureClearance,current.FutureClearance);
            var estimate=Assert.IsType<TrackReachability134>(current.TrackAwareness);Assert.Equal(old.Score+estimate.ScoreDelta,current.Score,12);Assert.Contains("heuristic",estimate.Classification);
            Assert.True(double.IsFinite(current.Score));
        }
        Assert.Equal(b.Action,Choose(true).Action);
    }
    [Fact] public void Separate133ObjectiveCannotBeSilentlyCombinedWithAwareness(){
        var frame=TrackAwarenessExperiment134.Frame("progressive-b");var p=FreeLearningExperiment.Parameters();var input=Input("progressive-b");var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        Assert.Throws<ArgumentException>(()=>FreeLearningController.Choose(input,TrackAwarenessExperiment134.StartPolicy(),FreeLearningExperiment.Rider(TrackAwarenessExperiment134.Diagnostic[0].Scenario),frame,surface,p,147,52.72097f,.9,true,true,true,true));
        Assert.False(new WorldParameters().EnableFreeRacing);Assert.Equal(.9,FreeRacingModel.MaximumHorizonSeconds);
    }
    [Fact] public void LegacyResponseSettingsAreOnlyHeuristicMarginsWithDirectSteering(){
        var frame=TrackAwarenessExperiment134.Frame("progressive-b");var p=FreeLearningExperiment.Parameters();var input=Input("progressive-b");
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var spec=FreeLearningExperiment.Rider(TrackAwarenessExperiment134.Diagnostic[0].Scenario);
        var changed=p with{LateralResponseSeconds=.4f,SlipYawResponseSeconds=.2f,YawResponseSeconds=.25f};
        var a=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,.9,new(.1f,0));
        var b=FreeRacingModel.Predict(input,spec,frame,surface,changed,147,52.72097f,.9,new(.1f,0));
        Assert.Equal(a.Points,b.Points);Assert.Equal(a.ForceEvaluations,b.ForceEvaluations);
        // These legacy controller constants therefore cannot certify a physical
        // tyre relaxation time in #134's direct steering mode. The frozen
        // TyreResponseSeconds field must be read as an uncertified soft margin.
    }
    [Fact] public void NewHoldoutsAndGeometriesAreSeparateFromTrainingAnd133(){
        static string Key(FreeLearningScenario s)=>$"{s.Speed:R}|{s.Offset:R}|{s.Start:R}|{s.OutwardSpeed:R}|{s.HeadingError:R}|{s.YawRate:R}";
        var main=TrackAwarenessExperiment134.FinalSingle.Concat(TrackAwarenessExperiment134.FinalTwo).ToArray();var others=TrackAwarenessExperiment134.Training.Concat(TrackAwarenessExperiment134.Selection).Concat(TrackAwarenessExperiment134.Diagnostic).ToArray();
        Assert.Equal(36,main.Length);Assert.Equal(36,main.Select(s=>Key(s.Scenario)).Distinct().Count());Assert.Empty(main.Select(s=>Key(s.Scenario)).Intersect(others.Select(s=>Key(s.Scenario))));
        Assert.Empty(main.Select(s=>Key(s.Scenario)).Intersect(RiderDecisionExperiment133.FinalSingle.Concat(RiderDecisionExperiment133.FinalTwo).Select(Key)));
        Assert.All(main,s=>Assert.Equal("progressive-c",s.Geometry));Assert.DoesNotContain(others,s=>s.Geometry is "progressive-c" or "egg-asymmetric");
        Assert.All(TrackAwarenessExperiment134.Transfer,s=>Assert.Equal("egg-asymmetric",s.Geometry));
    }
    [Fact] public void CEMStartsAtExactA0AndKeepsEightInheritedParameters(){
        var old=RiderDecisionExperiment133.Read<RiderDecisionCandidate[]>(PhysicalSteeringAudit.PreviousPath("../rider-decision-optimization"),"ranked-choices.json").Single(c=>c.Label=="A0");
        Assert.Equal(old.Policy,TrackAwarenessExperiment134.StartPolicy());Assert.Equal(8,old.Policy.Values.Length);Assert.False(old.NewObjective);
        Assert.Equal(TrackAwarenessExperiment134.Schedule(0),TrackAwarenessExperiment134.Schedule(2));Assert.Equal(2,TrackAwarenessExperiment134.Schedule(0).Count(c=>c.Two));
        var a=new FreeRacingExperiment.RandomStream(1342026);var b=new FreeRacingExperiment.RandomStream(1342026);var mean=old.Policy.Values;var std=mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
        for(int i=0;i<5;i++)Assert.Equal(RobustLearningExperiment.DrawPolicy(a,old.Policy,mean,std,i),RobustLearningExperiment.DrawPolicy(b,old.Policy,mean,std,i));
    }
    [Fact] public void ArchivedCemReproducesEvery134DrawAndEliteDistribution(){
        var d=PhysicalSteeringAudit.PreviousPath("../track-awareness-anticipatory-riding");
        foreach(var seed in TrackAwarenessExperiment134.Seeds)foreach(var arm in new[]{"A","B"}){
            var gs=TrackAwarenessExperiment134.Read<RobustGeneration[]>(d,$"generations-{arm}-s{seed}.json");
            Assert.Equal(4,gs.Length);var incumbent=TrackAwarenessExperiment134.StartPolicy();var mean=incumbent.Values;
            var std=mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
            var random=new FreeRacingExperiment.RandomStream(seed);
            foreach(var g in gs){
                Assert.Equal(5,g.Candidates.Length);
                for(int c=0;c<5;c++){
                    var expected=RobustLearningExperiment.DrawPolicy(random,incumbent,mean,std,c);
                    Assert.All(expected.Values.Zip(g.Candidates[c].Policy.Values),x=>Assert.InRange(Math.Abs(x.First-x.Second),0,1e-12));
                    Assert.Equal(TrackAwarenessExperiment134.Schedule(g.Generation).Select(s=>s.Geometry+"-"+s.Name+(s.Two?"-two":"-single")),g.Candidates[c].Scenarios);
                }
                var ranked=FullThrottleLearningExperiment.Rank(g.Candidates).ToArray();Assert.Equal(g.BestId,ranked[0].Id);incumbent=ranked[0].Policy;
                Assert.Equal(incumbent,g.BestPolicy);
                for(int i=0;i<8;i++){
                    var m=ranked.Take(2).Average(c=>c.Policy.Values[i]);mean[i]=.4*mean[i]+.6*m;
                    std[i]=Math.Max(.15*(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i]),Math.Sqrt(ranked.Take(2).Average(c=>Math.Pow(c.Policy.Values[i]-m,2))));
                    Assert.InRange(Math.Abs(mean[i]-g.Mean[i]),0,1e-12);Assert.InRange(Math.Abs(std[i]-g.Std[i]),0,1e-12);
                }
            }
        }
    }
    [Fact] public void FrozenSelectionUsesOnly134SelectionPoolAndSealsBeforeFinal(){
        var d=PhysicalSteeringAudit.PreviousPath("../track-awareness-anticipatory-riding");
        var scores=TrackAwarenessExperiment134.Read<RobustEvaluation[]>(d,"selection.json");var choices=TrackAwarenessExperiment134.Read<AwarenessPolicy134[]>(d,"choices.json");
        foreach(var arm in new[]{"A","B"}){
            var pool=scores.Where(s=>s.Variant.StartsWith(arm+"-",StringComparison.Ordinal)).ToArray();Assert.NotEmpty(pool);
            var expected=FullThrottleLearningExperiment.Rank(pool).First();var choice=choices.Single(c=>c.Label=="134-"+arm);
            Assert.Equal(expected.Id,choice.Id);Assert.Equal(expected.Policy,choice.Policy);Assert.Equal(arm=="B",choice.Awareness);Assert.True(choice.FullThrottle);
            Assert.All(pool,s=>Assert.Equal(TrackAwarenessExperiment134.Selection.Select(c=>c.Geometry+"-"+c.Name+(c.Two?"-two":"-single")),s.Scenarios));
        }
        var seal=TrackAwarenessExperiment134.Read<System.Text.Json.JsonElement>(d,"winner-lock.json");Assert.Equal(0,seal.GetProperty("FinalWorlds").GetInt32());
        Assert.Equal(TrackAwarenessExperiment134.Hash(TrackAwarenessExperiment134.Read<AwarenessPolicy134[]>(d,"final-plan.json")),seal.GetProperty("PlanHash").GetString());
        Assert.Equal(TrackAwarenessExperiment134.Hash(TrackAwarenessExperiment134.Read<AwarenessPolicy134>(d,"winner.json")),seal.GetProperty("WinnerHash").GetString());
    }
}
