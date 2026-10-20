using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class CoupledSlipInverseTests
{
    private static CoupledSlipInverse.Contact Contact(double beta=0,float step=.01f)
    {
        var sampler=new FullThrottleSlideAudit.Sampler(new(1,0,.5f));
        var q=sampler.At(18,beta,.15);q=sampler.Evaluate(q.View,q.Wheel.BeforeOmega,step);
        return CoupledSlipInverseAudit.Contact(q.View,q.Force,q.Wheel,q.Slip.GripBudgetNewtons,147);
    }
    [Fact]
    public void OriginalContactProjectionIncludesFxAndUsesTheFiniteInterval()
    {
        var sampler=new FullThrottleSlideAudit.Sampler(new(1,0,.5f));var q=sampler.At(18,15,.15);
        q=sampler.Evaluate(q.View,q.Wheel.BeforeOmega,.01f);
        var context=CoupledSlipInverseAudit.Contact(q.View,q.Force,q.Wheel,q.Slip.GripBudgetNewtons,147);
        var normal=new Vector2(-q.View.Velocity.Y,q.View.Velocity.X)/q.View.Speed;
        Assert.InRange(Math.Abs(context.NormalForce(q.View.SlideRadians)-Vector2.Dot(q.Force,normal)),0,.003);
        var fyOnly=q.Wheel.LateralNewtons*Math.Cos(q.View.SlideRadians);
        Assert.True(Math.Abs(context.NormalForce(q.View.SlideRadians)-fyOnly)>20);
        Assert.NotEqual(context.NormalForce(.2),(context with{Seconds=0}).NormalForce(.2));
    }
    [Theory]
    [InlineData(-10)] [InlineData(0)] [InlineData(10)]
    public void BracketedRootsHaveTheCorrectSignAndSmallResidual(double degrees)
    {
        var c=Contact();var beta=degrees*Math.PI/180;var force=c.NormalForce(beta);var r=CoupledSlipInverse.Solve(c,force);
        Assert.Equal(SlipInverseStatus.Solved,r.Status);
        Assert.InRange(Math.Abs(c.NormalForce(r.BetaRadians)-force),0,.052);
        Assert.InRange(Math.Abs(r.BetaRadians-beta),0,.0001);
        Assert.InRange(r.ContactEvaluations,25,250);
    }
    [Fact]
    public void NonmonotoneEnvelopeRejectsBelowScalarBudgetAndChoosesTheLowSlideBranch()
    {
        var c=Contact();var maximum=Enumerable.Range(0,901).Max(i=>c.NormalForce(i*Math.PI/3600));
        Assert.True(maximum<c.Budget-1);
        Assert.Equal(SlipInverseStatus.Unreachable,CoupledSlipInverse.Solve(c,(maximum+c.Budget)/2).Status);
        var r=CoupledSlipInverse.Solve(c,.98*maximum);
        Assert.Equal(SlipInverseStatus.Solved,r.Status);Assert.True(r.Roots>=2);
        Assert.True(r.BetaRadians<Math.PI/6);
        Assert.Equal(SlipInverseStatus.Ambiguous,CoupledSlipInverse.Solve(c,maximum+.1).Status);
    }
    [Fact]
    public void DisabledParametersSerializeExactlyAsBeforeAndUnsupportedWorldsAreRejected()
    {
        var p=LocalOffsetAudit.Parameters();Assert.False(p.EnableCoupledSlipInverse);
        Assert.Equal(JsonSerializer.Serialize(p,Audit.Json),JsonSerializer.Serialize(p with{EnableCoupledSlipInverse=false},Audit.Json));
        Assert.DoesNotContain("EnableCoupledSlipInverse",JsonSerializer.Serialize(p,Audit.Json));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{SlipDynamicsAudit.Rider()},parameters:p with{EnableCoupledSlipInverse=true}));
    }
    [Fact]
    public void SolveDoesNotAllocateOrChangeItsInputAndRepeatsExactly()
    {
        var c=Contact();var expected=CoupledSlipInverse.Solve(c,1400);Exception? failure=null;
        var worker=new Thread(()=>{try{
            for(var i=0;i<40;i++)_=CoupledSlipInverse.Solve(c,1400);
            var before=GC.GetAllocatedBytesForCurrentThread();var result=CoupledSlipInverse.Solve(c,1400);
            var bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Equal(0,bytes);Assert.Equal(expected,result);
        }catch(Exception ex){failure=ex;}});
        worker.Start();worker.Join();if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    [Fact]
    public void ShortNativeWorldRepeatsWithOneWheelCommitPerStepAndNoExtraActuator()
    {
        var observations=new List<CoupledSlipInverseObservation>();
        var a=SpeedGripCrossoverAudit.Execute(false,duration:1,laps:8,coupled:true,inverseObserver:observations.Add);
        var b=SpeedGripCrossoverAudit.Execute(false,duration:1,laps:8,coupled:true);
        Assert.Equal(a.Body.Result.Fingerprint,b.Body.Result.Fingerprint);
        Assert.Equal(a.Body.Result.Work.SolverCalls,a.Body.Result.Work.RearWheelCommits);
        Assert.Equal(a.Body.Ticks.Count,observations.Count);Assert.Empty(a.Body.ContactPoints);
        Assert.All(a.Wheels,w=>Assert.True(w.Step.DriveTorqueNewtonMeters>=0));
        Assert.All(observations.Where(o=>o.Result.Status!=SlipInverseStatus.Solved),o=>Assert.Equal(o.LegacyBetaRadians,o.AppliedBetaRadians));
    }
}
