using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class FullContactYawTests
{
    private static string Evidence(string file)
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"SpeedwayManager.sln")))directory=directory.Parent;
        return Path.Combine((directory??throw new DirectoryNotFoundException("Repository root")).FullName,
            "docs","experiments","full-contact-yaw-feedforward",file);
    }
    [Theory]
    [InlineData(-.2f)] [InlineData(0f)] [InlineData(.2f)]
    public void DriveNormalComponentHasTheCorrectSignAndRotationUnits(float beta)
    {
        const float speed=18,mass=147,fx=300;var v=new Vector2(speed,0);
        var forward=new Vector2(MathF.Cos(beta),MathF.Sin(beta));var side=new Vector2(-forward.Y,forward.X);
        var lateral=side*(MathF.Sign(beta)*1000);var full=forward*fx+lateral;
        var difference=FullContactYawAudit.YawRate(v,full,mass)-FullContactYawAudit.YawRate(v,lateral,mass);
        Assert.InRange(Math.Abs(difference-speed*fx*MathF.Sin(beta)/(mass*(speed*speed+.25f))),0,1e-7);
        Assert.Equal(MathF.Sign(beta),MathF.Sign(difference));
        Assert.Equal(MathF.Sign(beta),MathF.Sign(FullContactYawAudit.YawRate(v,full,mass)));
        Vector2 Rotate(Vector2 x)=>new(x.X*MathF.Cos(1.2f)-x.Y*MathF.Sin(1.2f),x.X*MathF.Sin(1.2f)+x.Y*MathF.Cos(1.2f));
        Assert.InRange(Math.Abs(FullContactYawAudit.YawRate(v,full,mass)-FullContactYawAudit.YawRate(Rotate(v),Rotate(full),mass)),0,1e-6);
        Assert.InRange(Math.Abs(FullContactYawAudit.YawRate(Rotate(v),-Rotate(v)*4,mass)),0,1e-7);
    }
    [Fact]
    public void NativeArithmeticReconstructsOriginalMomentsWithoutContactQueries()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Evidence("native-mathematics.json")));
        Assert.Equal(0,doc.RootElement.GetProperty("NewWorlds").GetInt32());
        foreach(var c in doc.RootElement.GetProperty("Cases").EnumerateArray()){
            Assert.Equal(0,c.GetProperty("MaximumRequestedReplayError").GetDouble());
            Assert.Equal(0,c.GetProperty("MaximumAuthorityReplayError").GetDouble());
            Assert.Equal(0,c.GetProperty("MaximumGuardReplayError").GetDouble());
            Assert.Equal(0,c.GetProperty("AdditionalContactEvaluations").GetInt32());
            Assert.Equal(0,c.GetProperty("SlipErrorDeltaOnFrozenState").GetInt32());
        }
    }
    [Fact]
    public void FlagChangesOnlyYawNotForceTargetWheelOrGuardCreditAtTheSameState()
    {
        var sampler=new FullThrottleSlideAudit.Sampler(new(1,0,.5f));var q=sampler.At(18,12,.15);var path=sampler.Path;
        var rider=RacingLineRootCauseAudit.Rider(path);var surface=TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot();
        var p=SpeedGripCrossoverAudit.Parameters(null) with{EnableCoupledSlipInverse=true};
        var wheel=new RearWheelState(q.Wheel.BeforeOmega*.33);SlipObservation a=default,b=default;YawLimitObservation ya=default,yb=default;
        CoupledSlipInverseObservation ia=default,ib=default;
        var old=SlipCoupledDynamics.Forces(q.View,rider,path.Frame,surface,p,0,147,52.72097f,observeSlip:x=>a=x,observeYaw:x=>ya=x,
            rearWheel:wheel,stepSeconds:.01f,diagnosticPath:path.Guidance(q.View,p,true),observeInverse:x=>ia=x);var wa=wheel.Prepared;
        var full=SlipCoupledDynamics.Forces(q.View,rider,path.Frame,surface,p with{EnableFullContactYawFeedForward=true},0,147,52.72097f,
            observeSlip:x=>b=x,observeYaw:x=>yb=x,rearWheel:wheel,stepSeconds:.01f,diagnosticPath:path.Guidance(q.View,p,true),observeInverse:x=>ib=x);
        Assert.Equal(old.Force,full.Force);Assert.Equal(a.TargetSlipDegrees,b.TargetSlipDegrees);Assert.Equal(ia.Result,ib.Result);
        Assert.Equal(wa,wheel.Prepared);Assert.Equal(ya.DissipationCreditWatts,yb.DissipationCreditWatts);Assert.NotEqual(old.Torque,full.Torque);
    }
    [Fact]
    public void DefaultOffKeepsTheControlConfigurationAndRequiresCoupledOnForTheExperiment()
    {
        var p=SpeedGripCrossoverAudit.Parameters(null) with{EnableCoupledSlipInverse=true};
        Assert.False(p.EnableFullContactYawFeedForward);
        Assert.DoesNotContain("EnableFullContactYawFeedForward",JsonSerializer.Serialize(p,InverseFeasibilityAudit.Json));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{SlipDynamicsAudit.Rider()},parameters:LocalOffsetAudit.Parameters() with{EnableFullContactYawFeedForward=true}));
    }
    [Fact]
    public void OffRetainsTheShortFingerprintCapturedBeforeChangingTravelYaw()
    {
        using var reference=JsonDocument.Parse(File.ReadAllText(Evidence("off-reference.json")));
        var actual=SpeedGripCrossoverAudit.Execute(false,coupled:true,duration:1).Body.Result;
        Assert.Equal(reference.RootElement.GetProperty("Fingerprint").GetString(),actual.Fingerprint);
    }
    [Fact]
    public void FullContactNativeRepeatKeepsOneBodyAndWheelCommitPerAcceptedStep()
    {
        var a=SpeedGripCrossoverAudit.Execute(false,coupled:true,fullContactYaw:true,duration:1);
        var b=SpeedGripCrossoverAudit.Execute(false,coupled:true,fullContactYaw:true,duration:1);
        Assert.Equal(a.Body.Result.Fingerprint,b.Body.Result.Fingerprint);Assert.Empty(a.Body.Result.Contacts);Assert.Empty(a.Body.Result.Decisions);
        Assert.Equal(a.Body.Result.Work.SolverCalls,a.Body.Result.Work.RearWheelCommits);
        Assert.Equal(a.Body.Ticks.Count,a.Wheels.Length);Assert.All(a.Body.Ticks,t=>Assert.Equal(1,t.Before.Drive));
    }
}
