using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class AdaptiveRiderControlTests
{
    [Fact]
    public void DefaultIsOffWithoutChangingHistoricalParameterBytes()
    {
        Assert.False(new WorldParameters().EnableAdaptiveRiderControl);
        Assert.DoesNotContain("EnableAdaptiveRiderControl",JsonSerializer.Serialize(PhysicalSteeringAudit.Parameters(),InverseFeasibilityAudit.Json));
        Assert.False(AdaptiveRiderAudit.Parameters().EnableDynamicAxleLoad);
    }
    [Fact]
    public void InvalidPhysicsAndAuthoredControlsAreRejected()
    {
        var path=AuthoredRacingPath.Crossover(false);var rider=RacingLineRootCauseAudit.Rider(path);
        foreach(var p in new[]{new WorldParameters{EnableAdaptiveRiderControl=true},AdaptiveRiderAudit.Parameters() with{EnablePhysicalSteeringControl=false},
            AdaptiveRiderAudit.Parameters() with{EnableDynamicAxleLoad=true},AdaptiveRiderAudit.Parameters() with{EnableLocalOffsetLimit=true}})
            Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{rider},parameters:p));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{rider},parameters:AdaptiveRiderAudit.Parameters(),
            controls:new[]{new ControlCommand(1,0,1,1)}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{rider},parameters:AdaptiveRiderAudit.Parameters(),authoredPath:path,diagnosticPathGuidance:true));
    }
    [Fact]
    public void PredictionIsDeterministicAndDoesNotCommitAcceptedWheelOrSteering()
    {
        var row=DynamicAxleLoadAudit.ReadSaved().Single(r=>Math.Abs(r["t"]-8.97)<1e-7);
        var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var view=PhysicalSteeringAudit.View(row,frame);
        var wheel=new RearWheelState(row["omega_before"]*.33);var steering=new TwoContactBikeState((float)row["steer_before"]);
        var before=wheel.AngularVelocityRadPerSecond;var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        AdaptiveRiderDecision Decide()=>new AdaptiveRiderControl(frame).Decide(view,RacingLineRootCauseAudit.Rider(path),frame,surface,
            AdaptiveRiderAudit.Parameters(),row["t"],147,52.72097f,wheel,steering);
        var a=Decide();var b=Decide();
        Assert.Equal(JsonSerializer.Serialize(a),JsonSerializer.Serialize(b));
        Assert.Equal(before,wheel.AngularVelocityRadPerSecond);Assert.Equal(default,wheel.Prepared);
        Assert.Equal((float)row["steer_before"],steering.SteeringRadians);Assert.Equal((float)row["steer_before"],steering.PreparedSteeringRadians);
        Assert.InRange(a.Candidates.Count,1,5);Assert.All(a.Candidates,c=>Assert.Equal(30,c.ForceEvaluations));
        Assert.Contains(a.Candidates,c=>c.MaximumDemandRatio>1);
        Assert.True(a.Chosen.Drive<1||a.Chosen.OffsetMeters>view.Road.OffsetMeters);
    }
    [Fact]
    public void NativeControlUsesOriginalContactForcesAndSingleWheelCommit()
    {
        var decisions=new List<AdaptiveRiderDecision>();
        var c=TwoContactBikeAudit.Execute(new("adaptive-invariant",Seconds:1.2,Drive:true),track:true,stopUnsafe:true,
            parameters:AdaptiveRiderAudit.Parameters(),adaptiveObserver:decisions.Add);
        Assert.Null(c.StopReason);Assert.Equal(0,c.LegacyYawCalls);Assert.Equal(c.Result.Work.SolverCalls,c.Result.Work.RearWheelCommits);
        Assert.Equal(c.Ticks.Length,c.Result.Work.SolverCalls);Assert.Equal(decisions.Count,c.Result.Work.AdaptiveReviews);
        var saved=DynamicAxleLoadAudit.ReadSaved("../adaptive-rider-decisions/adaptive-first-bend");
        foreach(var pair in c.Ticks.Zip(saved)){
            Assert.Equal(pair.Second["t"],pair.First.Time);Assert.Equal((float)pair.Second["x"],pair.First.Before.Position.X);
            Assert.Equal((float)pair.Second["y"],pair.First.Before.Position.Y);
            Assert.Equal((float)pair.Second["front_fx"],pair.First.Forces.FrontForce.X);
            Assert.Equal((float)pair.Second["rear_fy"],pair.First.Forces.RearForce.Y);
            Assert.Equal(pair.Second["omega_after"],pair.First.Wheel.AfterOmega);
        }
        Assert.All(c.Ticks,t=>{
            Assert.Null(t.Forces.AxleLoad);
            Assert.InRange(((t.After.Velocity-t.Before.Velocity)*c.Mass-t.Forces.NetForce*t.Seconds).Length(),0,.001);
            Assert.InRange(Math.Abs(c.Inertia*(t.After.AngularVelocity-t.Before.AngularVelocity)-t.Forces.NetTorque*t.Seconds),0,.00003);
            Assert.InRange(Math.Abs(t.Forces.FrontLateral),0,t.Forces.Budget*.400001);
            Assert.InRange(Math.Sqrt(t.Forces.RearLateral*t.Forces.RearLateral+t.Forces.RearLongitudinal*t.Forces.RearLongitudinal),0,t.Forces.Budget*.600001);
            Assert.InRange(Math.Abs(t.Forces.SteeringAfter),0,TwoContactBikeParameters.Default.MaximumSteeringRadians+1e-6);
            Assert.InRange(Math.Abs(t.Forces.SteeringAfter-t.Forces.SteeringBefore),0,TwoContactBikeParameters.Default.SteeringRateRadiansPerSecond*t.Seconds+1e-6);
        });
        Assert.All(decisions.Zip(decisions.Skip(1)),pair=>Assert.InRange(pair.Second.TimeSeconds-pair.First.TimeSeconds,.19999,.21001));
        Assert.Equal(0,JsonSerializer.SerializeToElement(TwoContactBikeAudit.Energy(c)).GetProperty("MaximumExcessBeyondEngineJoules").GetDouble());
    }
    [Fact]
    public void NativeReplayAndObservationalDiagnosticsDoNotChangeFingerprint()
    {
        var item=new TwoContactBikeAudit.Case("adaptive-repeat",Seconds:.6,Drive:true);
        var a=TwoContactBikeAudit.Execute(item,track:true,parameters:AdaptiveRiderAudit.Parameters());
        var b=TwoContactBikeAudit.Execute(item,track:true,parameters:AdaptiveRiderAudit.Parameters(),adaptiveObserver:_=>{});
        Assert.Equal(a.Result.Fingerprint,b.Result.Fingerprint);
    }
}
