using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class FeasibleCornerEntryTests
{
    private static WorldParameters Parameters()=>CornerEntryAudit.Parameters() with{EnableFeasibleCornerEntry=true};
    private static RiderView View(double s=4,float speed=18,float beta=0)
    {
        var frame=RacingLineRootCauseAudit.Frame();var at=frame.At(s,1);var heading=(float)at.Road.Heading;
        return new(1,at.Position,new(MathF.Cos(heading)*speed,MathF.Sin(heading)*speed),heading+beta,0,at.Road,s,0,RiderActivity.Racing,1,1,0);
    }
    private static CornerEntryDecision Observe(FeasibleCornerEntry control,RiderView v,double time=0,double kappa=0)
    {
        var frame=RacingLineRootCauseAudit.Frame();var wheel=new RearWheelState(v.ForwardSpeed*(1+kappa));var omega=wheel.AngularVelocityRadPerSecond;
        var steer=new TwoContactBikeState();
        var d=control.Observe(v,CornerEntryAudit.Rider(),frame,TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),Parameters(),time,147,wheel,steer);
        Assert.Equal(omega,wheel.AngularVelocityRadPerSecond);Assert.Equal(0,steer.SteeringRadians);return d;
    }
    [Fact]
    public void DefaultOffIsOmittedAndConflictingControlSourcesAreRejected()
    {
        Assert.False(new WorldParameters().EnableFeasibleCornerEntry);
        Assert.DoesNotContain("FeasibleCornerEntry",JsonSerializer.Serialize(CornerEntryAudit.Parameters(),InverseFeasibilityAudit.Json));
        var spec=CornerEntryAudit.Rider();
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:Parameters() with{EnableTravelDirectionSteering=false}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:Parameters() with{EnableLoadBasedTyreGrip=false}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:Parameters() with{Enable500CcEngine=false}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:Parameters(),controls:new[]{new ControlCommand(1,0,5,.15f)}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:Parameters() with{EnableAdaptiveRiderControl=true}));
    }
    [Fact]
    public void DecisionDependsOnPhysicalStateAndGeometryNotAnEggClock()
    {
        var incoming=Observe(new(),View(),123);
        Assert.True(incoming.Preparing);Assert.Equal(.15f,incoming.Drive);Assert.Equal(5,incoming.TargetOffsetMeters);
        Assert.True(incoming.RequiredNormal>SlipCoupledDynamics.MaximumSteeringDemandRatio*incoming.CapacityCeiling);
        Assert.InRange(incoming.LookaheadMeters,39,42);
        var slow=Observe(new(),View(speed:12),123);Assert.False(slow.Preparing);Assert.Equal(1,slow.Drive);
        var straight=Observe(new(),View(150),123);Assert.False(straight.Preparing);Assert.Equal(1,straight.Drive);
    }
    [Fact]
    public void RestoringGasRequiresCurvatureAndBothAcceptedSlipsToClear()
    {
        var control=new FeasibleCornerEntry();Assert.True(Observe(control,View()).Preparing);
        Assert.True(Observe(control,View(150,beta:.1f),1).Preparing);
        Assert.True(Observe(control,View(150),2,kappa:.5).Preparing);
        var release=Observe(control,View(150),3);Assert.False(release.Preparing);Assert.Equal(1,release.Drive);
        Assert.Equal("curvature and accepted slip have cleared",release.Reason);
    }
    [Fact]
    public void ExplicitCombinedTrialRemainsExactWithFlagOff()
    {
        var c=CornerEntryAudit.Execute("combined-off-check",5,.15f);
        using var saved=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../feasible-500cc-corner-entry/combined.json")));
        Assert.Equal(saved.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString(),c.Result.Fingerprint);
        Assert.Null(c.StopReason);Assert.Equal(0,c.Result.Work.CornerEntryReviews);
    }
    [Fact]
    public void NativeAiCompletesFirstBendWithDelayedControlsAndOriginalForces()
    {
        var reviews=new List<CornerEntryDecision>();var c=CornerEntryAudit.Execute("ai-check",1,1,true,reviews.Add);
        using var saved=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../feasible-500cc-corner-entry/ai.json")));
        Assert.Equal(saved.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString(),c.Result.Fingerprint);
        Assert.Null(c.StopReason);Assert.Equal(2,c.Result.Decisions.Count);
        var a=c.Result.Decisions[0];var b=c.Result.Decisions[1];
        Assert.Equal(.24,a.RequestedActivation,8);Assert.Equal(.24,a.ActualActivation!.Value,8);
        Assert.Equal(.24,b.ActualActivation!.Value-b.DecisionAt,8);Assert.Equal(1,b.Drive);Assert.True(b.DecisionAt>0);
        Assert.Equal(reviews.Count,c.Result.Work.CornerEntryReviews);
        Assert.Equal(c.Ticks.Length,c.Result.Work.SolverCalls);Assert.Equal(c.Ticks.Length,c.Result.Work.RearWheelCommits);
        Assert.True(c.Ticks[^1].After.TotalReferenceMeters>RacingLineRootCauseAudit.Frame().SegmentStartMeters(4)+6);
        Assert.True(c.Ticks[^1].After.TotalReferenceMeters<RacingLineRootCauseAudit.Frame().SegmentStartMeters(5));
        Assert.All(c.Ticks,t=>{
            Assert.NotNull(t.Forces.TravelSteering);Assert.Null(t.Forces.AxleLoad);
            Assert.InRange(Math.Abs(t.Forces.FrontLateral),0,t.Forces.TyreLoads!.FrontCapacityNewtons+1e-3);
            Assert.InRange(Math.Sqrt(t.Forces.RearLongitudinal*t.Forces.RearLongitudinal+t.Forces.RearLateral*t.Forces.RearLateral),0,t.Forces.TyreLoads.RearCapacityNewtons+1e-3);
            Assert.InRange(Math.Abs(t.Forces.SteeringAfter-t.Forces.SteeringBefore),0,TwoContactBikeParameters.Default.SteeringRateRadiansPerSecond*t.Seconds+1e-6);
            Assert.InRange(((t.After.Velocity-t.Before.Velocity)*c.Mass-t.Forces.NetForce*t.Seconds).Length(),0,.001);
            Assert.InRange(Math.Abs(c.Inertia*(t.After.AngularVelocity-t.Before.AngularVelocity)-t.Forces.NetTorque*t.Seconds),0,.00003);
            Assert.True(t.Inner>0&&t.Outer>0);Assert.True(Math.Abs(t.After.SlideRadians)<Math.PI/4);
        });
        var energy=JsonSerializer.SerializeToElement(TwoContactBikeAudit.Energy(c));Assert.Equal(0,energy.GetProperty("MaximumExcessBeyondEngineJoules").GetDouble());
        Assert.True(Math.Abs(c.Ticks[^1].After.SlideRadians)<Math.PI/60);
    }
    [Fact]
    public void AiObserverCannotChangeTheAcceptedFingerprint()
    {
        var a=CornerEntryAudit.Execute("observer",1,1,true,_=>{});var b=CornerEntryAudit.Execute("no-observer",1,1,true);
        Assert.Equal(a.Result.Fingerprint,b.Result.Fingerprint);
    }
}
