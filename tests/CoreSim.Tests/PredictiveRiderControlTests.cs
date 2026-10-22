using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class PredictiveRiderControlTests
{
    private static (PredictiveRiderInput Input,PreparedCornerPlan Plan,TrackStateSnapshot Surface,WorldParameters Parameters) Fixture(double time=10.4)
    {
        var rows=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous");var paths=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous-plan");
        var initial=PreparedCornerAudit.InitialPlan();var frame=initial.Path.Frame;var p=ContinuousCornerAudit.Parameters();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var spec=CornerEntryAudit.Rider();
        var saved=ContinuousCornerAudit.SavedEnd();var plan=time<10.15?initial:new PreparedCornerPlan(saved.Rider,spec,frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,surface,new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));
        var reviews=JsonSerializer.Deserialize<PreparedCornerReview[]>(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../continuous-corner-planning/reviews.json")))!;
        using var run=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../continuous-corner-planning/continuous.json")));
        var commands=run.RootElement.GetProperty("Result").GetProperty("Decisions").Deserialize<DecisionEvent[]>(InverseFeasibilityAudit.Json)!;
        var i=Array.FindIndex(rows,r=>Math.Abs(r["t"]-time)<1e-8);var row=rows[i];var review=reviews.Last(r=>r.Time<=time+1e-8);
        var pending=commands.FirstOrDefault(d=>d.ObservedAt<=time+1e-8&&d.RequestedActivation>time+1e-8&&d.CancelledAt is null);
        return(new(PhysicalSteeringAudit.View(row,frame) with{TargetOffsetMeters=(float)paths[i]["target"]},row["t"],row["omega_before"],(float)row["steer_before"],
            new((float)review.Drive,review.Time+.1),pending is null?null:pending with{ActualActivation=null}),plan,surface,p);
    }
    [Fact]
    public void ModelAndRuntimeAreOffByDefaultAndAbsentFromLegacySerialization()
    {
        Assert.False(new WorldParameters().EnablePredictiveRiderControl);Assert.False(new PredictiveRiderOptions().Enabled);
        Assert.DoesNotContain("EnablePredictiveRiderControl",JsonSerializer.Serialize(ContinuousCornerAudit.Parameters(),InverseFeasibilityAudit.Json));
        var f=Fixture();Assert.Throws<InvalidOperationException>(()=>PredictiveRiderModel.Predict(f.Input,CornerEntryAudit.Rider(),f.Plan.ForkForPrediction(),f.Surface,f.Parameters,147,52.72097f,.3));
    }
    [Fact]
    public void CandidateEvaluationNeverChangesAcceptedBodyWheelSteeringPendingOrPlanMemory()
    {
        var f=Fixture(5.3);var accepted=f.Input;var memory=f.Plan.ControlMemory;var path=f.Plan.Path;
        var controller=new PredictiveRiderControl();var decision=controller.Decide(f.Input,CornerEntryAudit.Rider(),f.Plan,f.Surface,f.Parameters with{EnablePredictiveRiderControl=true},147,52.72097f,false);
        Assert.Equal(accepted,f.Input);Assert.Equal(memory,f.Plan.ControlMemory);Assert.Same(path,f.Plan.Path);Assert.Equal(3,decision.Candidates.Length);
        Assert.All(decision.Candidates,c=>Assert.InRange(c.Prediction.MaximumSteeringRate,0,Math.PI/2+1e-4));
        Assert.All(decision.Candidates,c=>Assert.InRange(c.Prediction.MaximumFrontUtilization,0,1.00001));
        Assert.All(decision.Candidates,c=>Assert.InRange(c.Prediction.MaximumRearUtilization,0,1.00001));
    }
    [Fact]
    public void DelayedIntentionPreservesAllMotionAndThrottleBeforeFullReaction()
    {
        var f=Fixture();
        var baseline=PredictiveRiderModel.Predict(f.Input,CornerEntryAudit.Rider(),f.Plan.ForkForPrediction(),f.Surface,f.Parameters,147,52.72097f,.6,new(true));
        var changed=PredictiveRiderModel.Predict(f.Input,CornerEntryAudit.Rider(),f.Plan.ForkForPrediction(),f.Surface,f.Parameters,147,52.72097f,.6,new(true),PredictiveRiderControl.CorrectionRadians);
        foreach(var point in changed.Points.Where(p=>p.Time<=f.Input.Time+.24+1e-8)){
            var other=baseline.Points.Single(p=>Math.Abs(p.Time-point.Time)<1e-8);
            Assert.Equal(other.Rider,point.Rider);Assert.Equal(other.Omega,point.Omega);Assert.Equal(other.Steering,point.Steering);
        }
        Assert.NotEqual(baseline.Points[^1].Rider.Position,changed.Points[^1].Rider.Position);
        var pending=Assert.Single(changed.Activations.Where(a=>a.PreviouslyPending));
        Assert.Equal(10.64,pending.Time,8);Assert.Equal(f.Input.Pending!.Drive,pending.Drive);
    }
    [Fact]
    public void CausalPendingValidationRejectsCancelledOrNotYetObservedCommands()
    {
        var f=Fixture();
        foreach(var command in new[]{f.Input.Pending! with{CancelledAt=f.Input.Time},f.Input.Pending! with{ObservedAt=f.Input.Time+.01}})
            Assert.Throws<ArgumentException>(()=>PredictiveRiderModel.Predict(f.Input with{Pending=command},CornerEntryAudit.Rider(),f.Plan.ForkForPrediction(),f.Surface,f.Parameters,147,52.72097f,.3,new(true)));
    }
    [Theory]
    [InlineData(.3)] [InlineData(.6)] [InlineData(.9)]
    public void SavedNativeContinuationPassesFrozenPositionSpeedDirectionSlipAndSteeringTolerances(double horizon)
    {
        using var result=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../predictive-rider-control/prediction-validation.json")));
        var records=result.RootElement.GetProperty("Records").EnumerateArray().Where(r=>Math.Abs(r.GetProperty("Horizon").GetDouble()-horizon)<1e-8).ToArray();
        Assert.Equal(21,records.Length);Assert.All(records,r=>Assert.True(r.GetProperty("Passed").GetBoolean()));
    }
    [Fact]
    public void IndependentPredictionsAreDeterministicWithoutAdvancingAnyRealWorld()
    {
        var f=Fixture(6.3);
        PredictiveRiderResult Predict()=>PredictiveRiderModel.Predict(f.Input,CornerEntryAudit.Rider(),f.Plan.ForkForPrediction(),f.Surface,f.Parameters,147,52.72097f,.3,new(true),-PredictiveRiderControl.CorrectionRadians);
        Assert.Equal(JsonSerializer.Serialize(Predict(),InverseFeasibilityAudit.Json),JsonSerializer.Serialize(Predict(),InverseFeasibilityAudit.Json));
    }
    [Fact]
    public void RealOffVerificationRetainsExactOriginalTwoBendFingerprint()
    {
        using var verification=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../predictive-rider-control/baseline-verification.json")));
        Assert.True(verification.RootElement.GetProperty("ExactFingerprint").GetBoolean());
        Assert.Equal("33741F49880FADBCA5C7ECE240B32EF0017463A30B1BF85B32E06131D5CD4A03",verification.RootElement.GetProperty("Actual").GetString());
    }
    [Fact]
    public void FrozenComparisonLineRetainsEveryOriginalBaselinePathAndAppliedContact()
    {
        var rows=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous");var paths=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous-plan");
        var initial=PreparedCornerAudit.InitialPlan();var frame=initial.Path.Frame;var p=ContinuousCornerAudit.Parameters();var spec=CornerEntryAudit.Rider();
        var saved=ContinuousCornerAudit.SavedEnd();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var frozen=new PreparedCornerPlan(saved.Rider,spec,frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,surface,new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));
        var sequence=new ContinuousCornerPlanning(initial,frozen);var events=new List<CornerPlanChange>();
        foreach(var (row,i) in rows.Select((r,i)=>(r,i))){
            var v=PhysicalSteeringAudit.View(row,frame) with{TargetOffsetMeters=(float)paths[i]["target"]};
            var wheel=new RearWheelState(row["omega_before"]*.33);var steering=new TwoContactBikeState((float)row["steer_before"]);
            var change=sequence.Advance(v,spec,frame,surface,p,row["t"],147,wheel,steering,null);if(change is not null)events.Add(change);
            if(row["t"]+1e-10>=sequence.Active.NextReviewSeconds)sequence.Active.Observe(v,spec,frame,surface,p,row["t"],147,52.72097f,wheel,steering);
            var q=sequence.Active.Path.At(v.TotalReferenceMeters);
            Assert.Equal(paths[i]["path_x"],q.Position.X,8);Assert.Equal(paths[i]["path_y"],q.Position.Y,8);
            var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,row["t"],147,wheel,steering,(float)row["dt"],sequence.Active.Guidance(v,p,row["t"]),inertia:52.72097f);
            Assert.Equal(row["front_fx"],f.FrontForce.X);Assert.Equal(row["front_fy"],f.FrontForce.Y);
            Assert.Equal(row["rear_fx"],f.RearForce.X);Assert.Equal(row["rear_fy"],f.RearForce.Y);Assert.Equal(row["steer_after"],f.SteeringAfter);
        }
        Assert.Equal(2,events.Count);Assert.Equal(9.91,events[0].Time,8);Assert.Equal(10.15,events[1].Time,8);
    }
    [Fact]
    public void RecordedCandidateReplaysEveryActualForceWithRecordedSteeringPulseWithoutAnotherWorld()
    {
        var rows=DynamicAxleLoadAudit.ReadSaved("../predictive-rider-control/controlled/candidate");var paths=DynamicAxleLoadAudit.ReadSaved("../predictive-rider-control/controlled/candidate-plan");
        var initial=PreparedCornerAudit.InitialPlan();var frame=initial.Path.Frame;var p=ContinuousCornerAudit.Parameters();var spec=CornerEntryAudit.Rider();
        var saved=ContinuousCornerAudit.SavedEnd();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var next=new PreparedCornerPlan(saved.Rider,spec,frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,surface,new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));
        for(var i=0;i<rows.Length;i++){
            var r=rows[i];var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=(float)paths[i]["target"]};var plan=r["t"]<10.15?initial:next;
            float? pulse=double.IsNaN(paths[i]["min_chi_dot"])?(float)r["steer_request"]:null;
            var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,r["t"],147,new(r["omega_before"]*.33),new((float)r["steer_before"]),(float)r["dt"],plan.Guidance(v,p,r["t"]),pulse,inertia:52.72097f);
            Assert.Equal(r["front_fx"],f.FrontForce.X);Assert.Equal(r["front_fy"],f.FrontForce.Y);Assert.Equal(r["rear_fx"],f.RearForce.X);Assert.Equal(r["rear_fy"],f.RearForce.Y);
            Assert.Equal(r["steer_used"],f.SteeringUsed);Assert.Equal(r["steer_after"],f.SteeringAfter);
        }
    }
}
