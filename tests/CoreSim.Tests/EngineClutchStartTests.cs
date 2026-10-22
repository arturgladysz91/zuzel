using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class EngineClutchStartTests
{
    [Fact]
    public void FlagOffAndDependenciesRetainRollingStartContract()
    {
        Assert.False(new WorldParameters().EnableEngineClutchStart);
        Assert.DoesNotContain("Clutch",JsonSerializer.Serialize(CornerEntryAudit.Parameters(),InverseFeasibilityAudit.Json));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{EngineClutchStartAudit.Rider()},parameters:CornerEntryAudit.Parameters()));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{EngineClutchStartAudit.Rider()},parameters:EngineClutchStartAudit.Parameters() with{Enable500CcEngine=false}));
    }
    [Fact]
    public void TapeAndReactionPrecedeAllMotionAndStatesCommitOnce()
    {
        var c=EngineClutchStartAudit.Execute(maximumSeconds:1.2,tapeRelease:.1,requiredLaps:1);var reaction=.1+RiderController.TapeReactionSeconds(EngineClutchStartAudit.Rider());
        Assert.Empty(c.Result.FinishTimes);Assert.True(c.Ticks[^1].After.TotalReferenceMeters>0);
        Assert.Equal(0,c.Initial.Riders[0].Speed);Assert.Equal(0,c.Initial.RearWheels![0].RearWheelAngularVelocityRadPerSecond);Assert.True(c.Initial.Engines![0].Rpm>0);
        Assert.Equal(c.Ticks.Length,c.Result.Work.SolverCalls);Assert.Equal(c.Ticks.Length,c.Result.Work.RearWheelCommits);Assert.Equal(c.Ticks.Length,c.Result.Work.EngineClutchCommits);
        Assert.All(c.Ticks.Where(t=>t.Time<reaction-1e-8),t=>{Assert.Equal(c.Initial.Riders[0].Position,t.After.Position);Assert.Equal(0,t.After.Speed);
            Assert.Equal(0,t.Wheel.AfterOmega);Assert.Equal(0,t.Forces.Drivetrain!.ClutchTorqueNewtonMeters);});
        Assert.Contains(c.Ticks,t=>t.Time>=reaction&&t.After.Speed>0);
        var repeat=EngineClutchStartAudit.Execute(maximumSeconds:1.2,tapeRelease:.1,requiredLaps:1);Assert.Equal(c.Result.Fingerprint,repeat.Result.Fingerprint);
    }
    [Fact]
    public void ThreeSavedLaunchesReplayExactContactForcesAndCrankWithinNumericalToleranceWithoutNewWorlds()
    {
        var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        foreach(var c in EngineClutchStartAudit.Cases){
            var p=EngineClutchStartAudit.Parameters(c.ReleaseSeconds);var rows=DynamicAxleLoadAudit.ReadSaved("../physical-500cc-tape-start/"+c.Name);
            var engineRows=DynamicAxleLoadAudit.ReadSaved("../physical-500cc-tape-start/"+c.Name+"-drivetrain");
            foreach(var (r,index) in rows.Select((r,i)=>(r,i))){
                var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=EngineClutchStartAudit.Rider().TargetOffsetMeters};
                var engine=new EngineClutchState(p.EngineClutch!,engineRows[index]["engine_before"]);var wheel=new RearWheelState(r["omega_before"]*.33);
                var f=TwoContactBikeDynamics.Forces(v,EngineClutchStartAudit.Rider(),frame,surface,p,r["t"],147,wheel,new(),(float)r["dt"],
                    steeringOverride:0,inertia:52.72097f,drivetrain:engine);
                Assert.Equal(r["rear_fx"],f.RearForce.X);Assert.Equal(r["rear_fy"],f.RearForce.Y);Assert.Equal(r["front_fx"],f.FrontForce.X);Assert.Equal(r["front_fy"],f.FrontForce.Y);
                // Rebuilding wheel omega via radius can move a double by one ulp;
                // contact forces remain exact floats, crank replay tolerance is1e-9rad/s.
                Assert.InRange(Math.Abs(engineRows[index]["engine_after"]-f.Drivetrain!.AfterOmega),0,1e-9);Assert.Equal(engineRows[index]["engine_before"],engine.AngularVelocityRadPerSecond);
                Assert.InRange(Math.Sqrt(f.RearLateral*f.RearLateral+f.RearLongitudinal*f.RearLongitudinal),0,f.TyreLoads!.RearCapacityNewtons+1e-3);
                Assert.InRange(Math.Abs(f.Drivetrain.EngineEnergyChangeJoules+f.Drivetrain.Wheel.EngineWorkJoules+f.Drivetrain.ClutchLossJoules+f.Drivetrain.TransmissionLossJoules-f.Drivetrain.CombustionWorkJoules),0,1e-6);
            }
        }
    }
    [Fact]
    public void IntermediateNativeReplayMatchesTheSavedFullStraightFingerprint()
    {
        var c=EngineClutchStartAudit.Execute();
        using var saved=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../physical-500cc-tape-start/intermediate.json")));
        Assert.Equal(saved.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString(),c.Result.Fingerprint);
        Assert.Null(c.StopReason);Assert.True(c.Ticks[^1].After.Speed>70/3.6);
        Assert.Equal(c.Ticks.Length,c.Result.Work.EngineClutchCommits);
    }
}
