using System.Numerics;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;
[Trait("Shard","core")]
public class ThrottleSteeringTests
{
    [Fact] public void FactorialArmsChangeOnlyTheirAssignedChannel(){
        var state=ThrottleSteering138.State(ThrottleSteering138.Cases()[0]);var plan=ThrottleSteering138.Plans(state.Input).Last();
        for(int j=0;j<8;j++){
            var a=ThrottleSteering138.Action(state.Input,state.Hold,1,j);var b=ThrottleSteering138.Action(state.Input,plan,1,j);
            var c=ThrottleSteering138.Action(state.Input,state.Hold,(float)ThrottleSteering138.Policy.Drive,j);var d=ThrottleSteering138.Action(state.Input,plan,(float)ThrottleSteering138.Policy.Drive,j);
            Assert.Equal(a.SteeringRadians,c.SteeringRadians);Assert.Equal(b.SteeringRadians,d.SteeringRadians);
            Assert.Equal(a.Drive,b.Drive);Assert.Equal(c.Drive,d.Drive);
            Assert.Equal(j==0?state.Input.Active.Drive:1,a.Drive);
        }
    }
    [Fact] public void PreparationAndFullGasTraverseOriginalReactionAndRateLimitedActuator(){
        var state=ThrottleSteering138.State(ThrottleSteering138.Cases()[0]);var plan=ThrottleSteering138.Plans(state.Input).Last();
        var original=state.Input.Pending.ToArray();var b=ThrottleSteering138.Forecast(state,plan,1);var d=ThrottleSteering138.Forecast(state,plan,(float)ThrottleSteering138.Policy.Drive);
        foreach(var q in b.Prediction.Points.Where(q=>q.Time<state.Input.Time+.44-1e-8)){
            var r=d.Prediction.Points.MinBy(x=>Math.Abs(x.Time-q.Time))!;
            Assert.InRange(Vector2.Distance(q.Rider.Position,r.Rider.Position),0,1e-6);Assert.Equal(q.Omega,r.Omega);Assert.Equal(q.Steering,r.Steering);
        }
        Assert.Equal(original,state.Input.Pending);
        foreach(var (a,z) in b.Prediction.Points.Zip(b.Prediction.Points.Skip(1))){
            Assert.InRange(Math.Abs(z.Steering-a.Steering),0,TwoContactBikeParameters.Default.SteeringRateRadiansPerSecond*(z.Time-a.Time)+1e-6);
            Assert.InRange(Math.Abs(z.Steering),0,TwoContactBikeParameters.Default.MaximumSteeringRadians+1e-6);
        }
        Assert.Equal(1,b.Prediction.Points.First(x=>x.Time>=state.Input.Time+.44-1e-8).Rider.Drive);
    }
    [Fact] public void CandidateSetIsBoundedDeterministicAndIncludesPreparationBeforeGas(){
        var state=ThrottleSteering138.State(ThrottleSteering138.Cases()[1]);var first=ThrottleSteering138.Plans(state.Input);
        Assert.Equal(first,ThrottleSteering138.Plans(state.Input));Assert.InRange(first.Length,1,7);
        Assert.Contains(first,x=>x.Name=="early-prepare");Assert.All(first,x=>{
            ThrottleSteering138.Action(state.Input,x,1,0).Validate();ThrottleSteering138.Action(state.Input,x,1,1).Validate();});
        Assert.False(new WorldParameters().EnableFreeRacingLearning);
    }
}
