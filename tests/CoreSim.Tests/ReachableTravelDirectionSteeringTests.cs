using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class ReachableTravelDirectionSteeringTests
{
    private static Dictionary<string,double>[] Rows(string name)=>DynamicAxleLoadAudit.ReadSaved("../reachable-steering-line-tracking/"+name);
    [Fact]
    public void DefaultOffIsOmittedAndRequiresOriginalTravelSteering()
    {
        Assert.False(new WorldParameters().EnableReachableTravelDirectionSteering);
        Assert.DoesNotContain("ReachableTravelDirectionSteering",JsonSerializer.Serialize(CornerEntryAudit.Parameters(),InverseFeasibilityAudit.Json));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{CornerEntryAudit.Rider()},parameters:CornerEntryAudit.Parameters() with{
            EnableReachableTravelDirectionSteering=true,EnableTravelDirectionSteering=false}));
    }
    [Fact]
    public void QueriesUseReachableMeanAnglesAndPreserveTheScoreAndPrediction()
    {
        var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var p=CornerEntryAudit.Parameters();var geometry=TwoContactBikeParameters.Default;
        foreach(var r in DynamicAxleLoadAudit.ReadSaved("../feasible-500cc-corner-entry/combined").Where(r=>new[]{.24,2,4.5,5.4,7}.Any(t=>Math.Abs(t-r["t"])<1e-8))){
            var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=5};var before=(float)r["steer_before"];var dt=(float)r["dt"];
            var wheel=new RearWheelState(r["omega_before"]*.33);var state=new TwoContactBikeState(before);
            var original=TwoContactBikeDynamics.Forces(v,CornerEntryAudit.Rider(),frame,surface,p,r["t"],147,wheel,state,dt,inertia:52.72097f);
            var samples=new List<float>();
            TwoContactForces Held(float angle){samples.Add(angle);return TwoContactBikeDynamics.Forces(v,CornerEntryAudit.Rider(),frame,surface,p,r["t"],147,
                new(r["omega_before"]*.33),new(angle),dt,steeringOverride:angle);}
            var d=ReachableTravelDirectionSteering.Decide(v,original.Demand,147,52.72097f,p.SlipYawResponseSeconds,geometry,before,dt,Held);
            Assert.Equal(51,d.ContactQueries);Assert.Equal(51,samples.Count);Assert.False(d.RateLimited);
            var low=Math.Max(-geometry.MaximumSteeringRadians,before-geometry.SteeringRateRadiansPerSecond*dt);
            var high=Math.Min(geometry.MaximumSteeringRadians,before+geometry.SteeringRateRadiansPerSecond*dt);
            Assert.InRange(d.AngleRequestRadians,low,high);
            Assert.All(samples,angle=>Assert.InRange(angle,(before+low)/2-1e-7,(before+high)/2+1e-7));
            var used=Held((before+d.AngleRequestRadians)/2);var m=TravelDirectionState.Observe(v,used.NetForce,147);
            Assert.Equal(m.ChiDot,d.PredictedChiDot);Assert.Equal(v.AngularVelocity+p.SlipYawResponseSeconds*used.NetTorque/52.72097f-m.ChiDot,d.PredictedBetaDot);
            Assert.Equal(before,state.SteeringRadians);Assert.Equal(r["omega_before"],wheel.AngularVelocityRadPerSecond,11);
        }
    }
    [Fact]
    public void ZeroIntervalCannotMoveSteering()
    {
        var frame=RacingLineRootCauseAudit.Frame();var r=Rows("reachable")[300];var p=CornerEntryAudit.Parameters();
        var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=5};var before=(float)r["steer_before"];
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        TwoContactForces Query(float angle)=>TwoContactBikeDynamics.Forces(v,CornerEntryAudit.Rider(),frame,surface,p,r["t"],147,new(r["omega_before"]*.33),new(angle),0,steeringOverride:angle);
        var d=ReachableTravelDirectionSteering.Decide(v,10,147,52.72097f,.1f,TwoContactBikeParameters.Default,before,0,Query);
        Assert.Equal(before,d.AngleRequestRadians);Assert.Equal(51,d.ContactQueries);
    }
    [Fact]
    public void EveryRecordedOnForceReplaysExactlyWithoutIntegratingAnotherWorld()
    {
        var frame=RacingLineRootCauseAudit.Frame();var p=CornerEntryAudit.Parameters() with{EnableReachableTravelDirectionSteering=true};
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        foreach(var r in Rows("reachable")){
            var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=r["t"]<.24-1e-8?1:5};
            var wheel=new RearWheelState(r["omega_before"]*.33);var state=new TwoContactBikeState((float)r["steer_before"]);
            var f=TwoContactBikeDynamics.Forces(v,CornerEntryAudit.Rider(),frame,surface,p,r["t"],147,wheel,state,(float)r["dt"],inertia:52.72097f);
            Assert.Equal(r["front_fx"],f.FrontForce.X);Assert.Equal(r["front_fy"],f.FrontForce.Y);Assert.Equal(r["rear_fx"],f.RearForce.X);Assert.Equal(r["rear_fy"],f.RearForce.Y);
            Assert.Equal(r["steer_after"],f.SteeringAfter);Assert.Equal(51,f.TravelSteering!.ContactQueries);
            Assert.Equal(r["steer_before"],state.SteeringRadians);Assert.Equal(r["omega_before"],wheel.AngularVelocityRadPerSecond,11);
        }
    }
}
