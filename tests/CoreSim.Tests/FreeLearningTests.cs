using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class FreeLearningTests
{
    private static (FreeRacingInput Input,RiderSpec Spec,TrackFrame Frame,TrackStateSnapshot Surface) Fixture(){
        var scenario=FreeLearningExperiment.Scenarios(1)[0];var spec=FreeLearningExperiment.Rider(scenario);var frame=RacingLineRootCauseAudit.Frame();var at=frame.At(spec.ReferenceMeters,spec.OffsetMeters);
        var v=new RiderView(1,at.Position,at.Road.Tangent*8,(float)at.Road.Heading,0,at.Road,spec.ReferenceMeters,0,RiderActivity.Racing,1,spec.TargetOffsetMeters,0);
        return(new(v,0,8/.33,0,new(0,1),Array.Empty<FreeRacingCommand>(),.2),spec,frame,TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot());
    }
    [Fact]public void LearningIsOffByDefaultAndDoesNotReplaceOldFreeRacing(){Assert.False(new WorldParameters().EnableFreeRacingLearning);Assert.DoesNotContain("EnableFreeRacingLearning",JsonSerializer.Serialize(new WorldParameters()));}
    [Fact]public void CandidateSetHasZeroCurrentBothSignsAndBothThrottleOptions(){
        var f=Fixture();var actions=FreeLearningController.Actions(f.Input with{Steering=.12f},new());
        Assert.Contains(actions,a=>a.SteeringRadians==0);Assert.Contains(actions,a=>a.SteeringRadians==.12f);
        Assert.Contains(actions,a=>a.SteeringRadians>14*Math.PI/180);Assert.Contains(actions,a=>a.SteeringRadians< -14*Math.PI/180);
        Assert.Contains(actions,a=>a.Drive==0);Assert.Contains(actions,a=>a.Drive==1);
        Assert.All(actions,a=>a.Validate());Assert.InRange(actions.Length,19,25);
    }
    [Fact]public void SteeringSignEvidenceUsesActualBodyTravelAndXYNotOnlyHandlebar(){
        using var d=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../free-racing-learning/physics.json")));
        Assert.True(d.RootElement.GetProperty("Passed").GetBoolean());var rows=d.RootElement.GetProperty("Records").EnumerateArray().ToArray();Assert.Equal(5,rows.Length);
        foreach(var row in rows){var angle=row.GetProperty("AngleDegrees").GetInt32();if(angle==0)continue;
            foreach(var key in new[]{"DeltaPsi","DeltaChi","DeltaY"})Assert.Equal(Math.Sign(angle),Math.Sign(row.GetProperty(key).GetDouble()));}
    }
    [Fact]public void GeometryTargetOffsetDoesNotInfluenceActionsOrScores(){
        var f=Fixture();FreeLearningDecision Decide(FreeRacingInput x)=>FreeLearningController.Choose(x,new(),f.Spec,f.Frame,f.Surface,FreeLearningExperiment.Parameters(),147,52.72097f);
        var a=Decide(f.Input);var b=Decide(f.Input with{Rider=f.Input.Rider with{TargetOffsetMeters=9}});
        Assert.Equal(a.Action,b.Action);Assert.Equal(a.Candidates,b.Candidates);Assert.DoesNotContain(typeof(FreeLearningPolicy).GetProperties(),p=>p.Name.Contains("Offset")||p.Name.Contains("Path"));
    }
    [Fact]public void FeasibleCandidateCannotLoseToUnsafeFractionalProgress(){
        var f=Fixture();var decision=FreeLearningController.Choose(f.Input,new(),f.Spec,f.Frame,f.Surface,FreeLearningExperiment.Parameters(),147,52.72097f);
        Assert.Equal(decision.Candidates.Min(c=>c.Safety),decision.Safety);
        Assert.Equal(decision.Candidates.All(c=>c.Safety!=FreeActionSafety.SafeInHorizon),decision.NoSafeContinuation);
    }
    private static FreeLearningMetrics Metric(double progress,bool complete=false,double duration=3)=>new(complete,complete?"completed":"boundary",complete?duration:null,duration,progress,duration,complete?1:-.1,null,8,null,10,.1,0,0,progress,1,0,30,100,10,.01,"test");
    [Fact]public void FailedEpisodesTeachProgressWithoutRewardForWaitingOrIllegalDistance(){
        var a=new FreeLearningEvaluation(1,0,1,new(),new[]{Metric(5,duration:100),Metric(5,duration:100)});
        var b=new FreeLearningEvaluation(2,0,1,new(),new[]{Metric(15),Metric(15)});
        Assert.Equal(2,FreeLearningExperiment.Rank(new[]{a,b}).First().Id);
        var c=new FreeLearningEvaluation(3,0,1,new(),new[]{Metric(1,true),Metric(1,true)});Assert.Equal(3,FreeLearningExperiment.Rank(new[]{a,b,c}).First().Id);
    }
    [Fact]public void MutatedPolicyIsBoundedAndCanDifferSubstantially(){
        var x=new FreeLearningPolicy().Values;x[1]=FreeLearningPolicy.Upper[1];Assert.NotEqual(new FreeLearningPolicy(),FreeLearningPolicy.From(x));
        x[0]=double.NaN;Assert.Throws<ArgumentException>(()=>FreeLearningPolicy.From(x));
    }
    [Fact]public void CausalPolicyDoesNotExecuteBeforeFullReaction(){
        var f=Fixture();var p=FreeLearningExperiment.Parameters();
        var a=FreeRacingModel.Predict(f.Input,f.Spec,f.Frame,f.Surface,p,147,52.72097f,.3,new(.1f,0));
        var b=FreeRacingModel.Predict(f.Input,f.Spec,f.Frame,f.Surface,p,147,52.72097f,.3,new(-.1f,.8f));
        foreach(var x in a.Points.Where(x=>x.Time<.24-1e-8))Assert.Equal(x,b.Points.Single(y=>Math.Abs(y.Time-x.Time)<1e-8));
        Assert.Equal(0,a.Points.Single(x=>Math.Abs(x.Time-.24)<1e-8).Steering);
    }
    [Fact]public void HeldOutScenariosAreNotTrainingScenarios(){
        var training=Enumerable.Range(1,4).SelectMany(FreeLearningExperiment.Scenarios).Select(s=>(s.Speed,s.Offset,s.Start)).ToHashSet();
        Assert.All(FreeLearningExperiment.Validation,s=>Assert.DoesNotContain((s.Speed,s.Offset,s.Start),training));
    }
    [Fact]public void SavedPolicyDecisionIsDeterministic(){
        var f=Fixture();var policy=JsonSerializer.Deserialize<FreeLearningPolicy>(JsonSerializer.Serialize(new FreeLearningPolicy()))!;
        FreeLearningDecision Decide()=>FreeLearningController.Choose(f.Input,policy,f.Spec,f.Frame,f.Surface,FreeLearningExperiment.Parameters(),147,52.72097f);
        Assert.Equal(JsonSerializer.Serialize(Decide()),JsonSerializer.Serialize(Decide()));
    }
    [Fact]public void ZeroCurvatureInfiniteRadiusMetadataRoundTripsWithoutChangingFiniteBody(){
        var f=Fixture();var v=f.Input.Rider with{Road=f.Input.Rider.Road with{RadiusMeters=double.PositiveInfinity}};
        var text=JsonSerializer.Serialize(v,FreeLearningExperiment.Json);var restored=JsonSerializer.Deserialize<RiderView>(text,FreeLearningExperiment.Json);
        Assert.Equal(v,restored);Assert.True(float.IsFinite(restored.Position.X));Assert.True(double.IsPositiveInfinity(restored.Road.RadiusMeters));
    }
    [Fact]public void MicrosecondSteeringChecksOriginalFloatClampWithoutFalseRateFailure(){
        const float before=.03700901f,h=.000001f;var p=TwoContactBikeParameters.Default;
        var after=before+p.SteeringRateRadiansPerSecond*h;
        var f=default(TwoContactForces) with{SteeringBefore=before,SteeringAfter=after,SteeringRequest=.1f};
        Assert.True(Math.Abs(after-before)/h>p.SteeringRateRadiansPerSecond);
        Assert.True(FreeLearningExperiment.SteeringStepIsPhysical(f,h));
        Assert.False(FreeLearningExperiment.SteeringStepIsPhysical(f with{SteeringAfter=after+.0001f},h));
    }
}
