using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class TravelDirectionSteeringTests
{
    private static string Evidence(string name)=>PhysicalSteeringAudit.PreviousPath("../controlled-slip-travel-direction/"+name);
    private static Dictionary<string,double> Row(double time)=>TravelDirectionAudit.Saved().Single(r=>Math.Abs(r["t"]-time)<1e-7);
    private static TwoContactForces Query(Dictionary<string,double> row,bool enabled=true,float? steeringOverride=null)
    {
        var path=AuthoredRacingPath.Crossover(false);var p=TravelDirectionAudit.Parameters() with{EnableTravelDirectionSteering=enabled};
        var v=PhysicalSteeringAudit.View(row,path.Frame);var wheel=new RearWheelState(row["omega_before"]*.33);var before=wheel.AngularVelocityRadPerSecond;
        var state=new TwoContactBikeState((float)row["steer_before"]);
        var f=TwoContactBikeDynamics.Forces(v,RacingLineRootCauseAudit.Rider(path),path.Frame,TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot(),p,
            row["t"],147,wheel,state,(float)row["dt"],path.Guidance(v,p,true),steeringOverride,inertia:52.72097f);
        Assert.Equal(before,wheel.AngularVelocityRadPerSecond);Assert.Equal((float)row["steer_before"],state.SteeringRadians);return f;
    }
    [Fact]
    public void DefaultOffOmitsNewJsonAndInvalidDependenciesAreRejected()
    {
        Assert.False(new WorldParameters().EnableTravelDirectionSteering);
        Assert.DoesNotContain("TravelDirection",JsonSerializer.Serialize(MotorcyclePerformanceAudit.Parameters("C-both"),InverseFeasibilityAudit.Json));
        var spec=RacingLineRootCauseAudit.Rider(AuthoredRacingPath.Crossover(false));
        foreach(var p in new[]{new WorldParameters{EnableTravelDirectionSteering=true},TravelDirectionAudit.Parameters() with{EnablePhysicalSteeringControl=false},
            TravelDirectionAudit.Parameters() with{EnableDynamicAxleLoad=true}})
            Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:p));
    }
    [Theory]
    [InlineData(1)][InlineData(-1)]
    public void KinematicsUseForceDirectionWithSignedBetaDerivative(int sign)
    {
        var v=PhysicalSteeringAudit.View(Row(.5),AuthoredRacingPath.Crossover(false).Frame) with{Heading=sign*.4f,Velocity=new(18,0),AngularVelocity=sign*.7f};
        var m=TravelDirectionState.Observe(v,new(1000,sign*800),147);
        Assert.Equal(sign*.4f,m.Beta,6);Assert.Equal(sign*800d/(147*18),m.ChiDot,7);Assert.Equal(m.PsiDot-m.ChiDot,m.BetaDot);
        Assert.True(Math.Abs(m.PsiDot-m.ChiDot)>.3);Assert.True(m.DirectionDefined);
    }
    [Fact]
    public void LowSpeedHasNoClaimedVelocityDirection()
    {
        var v=PhysicalSteeringAudit.View(Row(0),AuthoredRacingPath.Crossover(false).Frame) with{Velocity=Vector2.Zero};
        var m=TravelDirectionState.Observe(v,new(100,100),147);Assert.False(m.DirectionDefined);Assert.Equal(0,m.ChiDot);Assert.True(double.IsFinite(m.BetaDot));
    }
    [Fact]
    public void ConstantNonzeroSlideHasNoZeroBetaRestoringTarget()
    {
        var v=PhysicalSteeringAudit.View(Row(0),AuthoredRacingPath.Crossover(false).Frame) with{Heading=.35f,Velocity=new(18,0),AngularVelocity=.3f};
        var response=default(TwoContactForces) with{FrontForce=new(0,147*18*.3f),Budget=1852};
        var a=TravelDirectionSteering.Decide(v,18*.3f,147,52.72097f,.1f,TwoContactBikeParameters.Default,.07f,.01f,_=>response);
        Assert.Equal(.35,a.Motion.Beta,6);Assert.InRange(Math.Abs(a.PredictedBetaDot),0,1e-6);Assert.Equal(.07f,a.AngleRequestRadians);
        Assert.Equal(51,a.ContactQueries);
    }
    [Theory]
    [InlineData(.5)][InlineData(1)][InlineData(1.5)][InlineData(2)]
    public void SavedCReplaysOldForcesExactlyAndNewQueriesArePureAndDeterministic(double time)
    {
        var row=Row(time);var old=Query(row,false);Assert.Equal((float)row["front_fx"],old.FrontForce.X);Assert.Equal((float)row["rear_fy"],old.RearForce.Y);
        Assert.Null(old.TravelSteering);var a=Query(row);var b=Query(row);Assert.Equal(a,b);Assert.Null(a.SteeringControl);Assert.Null(a.AxleLoad);
        var motion=TravelDirectionState.Observe(PhysicalSteeringAudit.View(row,AuthoredRacingPath.Crossover(false).Frame),a.NetForce,147);
        Assert.Equal(motion.PsiDot-motion.ChiDot,motion.BetaDot);
        Assert.Equal(51,a.TravelSteering!.ContactQueries);
        // Same requested steering through the original contact law reproduces ALL forces.
        var applied=Query(row,false,a.SteeringRequest);Assert.Equal(applied.FrontForce,a.FrontForce);Assert.Equal(applied.RearForce,a.RearForce);Assert.Equal(applied.NetTorque,a.NetTorque);
        Assert.Equal(old.Engine,a.Engine);Assert.Equal(old.TyreLoads,a.TyreLoads);
    }
    [Fact]
    public void FullThrottleSavedStateReportsImpossibleCurvatureSeparatelyFromLateSlideRecovery()
    {
        var early=Query(Row(.5)).TravelSteering!;Assert.True(early.DirectionOutsideSampledResponse);Assert.True(early.DirectionExceedsTotalCapacity);
        Assert.True(early.DesiredChiDot>early.AbsoluteCapacityChiDotBound);Assert.False(early.NoSampledSlideRecovery);
        var late=Query(Row(1.5)).TravelSteering!;Assert.True(late.NoSampledSlideRecovery);Assert.True(late.GrowingSlide);
    }
    [Fact]
    public void AchievableNativeArcPreservesContactEnergySteeringAndSingleCommitInvariants()
    {
        var c=TravelDirectionAudit.Arc("achievable-arc",14,.35f,null,3);Assert.Null(c.StopReason);Assert.Equal(0,c.LegacyYawCalls);
        Assert.Equal(c.Ticks.Length,c.Result.Work.SolverCalls);Assert.Equal(c.Result.Work.SolverCalls,c.Result.Work.RearWheelCommits);
        using var saved=JsonDocument.Parse(File.ReadAllText(Evidence("arc.json")));
        Assert.Equal(saved.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString(),c.Result.Fingerprint);
        Assert.All(c.Ticks,t=>{
            var f=t.Forces;Assert.Equal(.35f,t.Before.Drive);Assert.Null(f.AxleLoad);Assert.NotNull(f.TravelSteering);
            Assert.InRange(Math.Abs(f.FrontLateral),0,f.TyreLoads!.FrontCapacityNewtons+1e-3);
            Assert.InRange(Math.Sqrt(f.RearLongitudinal*f.RearLongitudinal+f.RearLateral*f.RearLateral),0,f.TyreLoads.RearCapacityNewtons+1e-3);
            Assert.InRange(Math.Abs(f.SteeringAfter-f.SteeringBefore),0,TwoContactBikeParameters.Default.SteeringRateRadiansPerSecond*t.Seconds+1e-6);
            Assert.InRange(((t.After.Velocity-t.Before.Velocity)*c.Mass-f.NetForce*t.Seconds).Length(),0,.001);
            Assert.InRange(Math.Abs(c.Inertia*(t.After.AngularVelocity-t.Before.AngularVelocity)-f.NetTorque*t.Seconds),0,.00003);
            Assert.True(t.Inner>5&&t.Outer>5);
        });
        Assert.Equal(0,JsonSerializer.SerializeToElement(TwoContactBikeAudit.Energy(c)).GetProperty("MaximumExcessBeyondEngineJoules").GetDouble());
        Assert.InRange(c.Ticks[^1].After.SlideRadians,.07,.12);
    }
    [Fact]
    public void ExplicitCoastActionRecoversWhileSteeringNeverChangesThrottle()
    {
        var c=TravelDirectionAudit.Arc("explicit-rider-coast-action",18,1,0,3);Assert.Null(c.StopReason);
        using var saved=JsonDocument.Parse(File.ReadAllText(Evidence("rider-coast.json")));
        Assert.Equal(saved.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString(),c.Result.Fingerprint);
        Assert.All(c.Ticks,t=>Assert.Equal(t.Time<.2-1e-9?1:0,t.Before.Drive));
        var peak=c.Ticks.Max(t=>t.Before.SlideRadians);Assert.True(c.Ticks[^1].After.SlideRadians<peak/2);
        Assert.All(c.Ticks.Where(t=>t.Time>2),t=>Assert.True(TravelDirectionState.Observe(t.Before,t.Forces.NetForce,c.Mass).BetaDot<0));
    }
    [Fact]
    public void PerformanceCOffAcceptedPrefixRemainsExact()
    {
        var c=TwoContactBikeAudit.Execute(new("off-C-prefix",Seconds:.4,Drive:true),track:true,parameters:MotorcyclePerformanceAudit.Parameters("C-both"));
        foreach(var (t,row) in c.Ticks.Zip(TravelDirectionAudit.Saved())){
            Assert.Equal(row["t"],t.Time);Assert.Equal((float)row["x"],t.Before.Position.X);Assert.Equal((float)row["y"],t.Before.Position.Y);
            Assert.Equal(row["omega_after"],t.Wheel.AfterOmega);Assert.Equal((float)row["front_fx"],t.Forces.FrontForce.X);Assert.Null(t.Forces.TravelSteering);
        }
    }
}
