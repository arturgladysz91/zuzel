using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using SpeedwayHandling;
using WholeCorner171;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class ThrottleRecovery209Tests
{
    [Theory]
    [InlineData(.25,.25)]
    [InlineData(0,0)]
    public void GeometryReportsTheEffectiveCeilingAfterBiasAndSpeedFeedback(double ceiling,double expected)
    {
        var frame=Cyclic172.TrafficWorld191.Frame();var map=new BendMap(frame);
        var root=Root(10,22);
        Knot199[] knots=[new(Root(0,22),new(0,.8f),5,5),new(Root(300,22),new(0,.8f),5,5),
            new(Root(frame.LapMeters,22),new(0,.8f),5,5),new(Root(frame.LapMeters+300,22),new(0,.8f),5,5)];
        var policy=new GeometryPolicy207(map,frame.LapMeters,knots);
        var move=new Move207(-.2,0,0,0,0,0,ThrottleCeiling:ceiling);
        var plain=policy.At(root,move);
        var observed=ThrottleTrace209.Capture(()=>policy.At(root,move));
        Assert.Equal(plain,observed.Action);
        Assert.Equal(expected,observed.Action.Drive,6);
        Assert.InRange(observed.Trace.RawRequested,.599999,.600001);
        Assert.Equal("207 ceiling",observed.Trace.Dominant);
        Assert.Equal(expected,observed.Trace.Stages.Single(s=>s.Name=="207 ceiling").After,6);
    }

    [Fact]
    public void SimultaneousPaceRampAndActuatorClampsPreserveTheirOrderedContributions()
    {
        var frame=Cyclic172.TrafficWorld191.Frame();var map=new BendMap(frame);var bend=map.At(2,0);
        RoutePoint208[] route=[new(frame.LapMeters,8,0,22,0,0),new(2*frame.LapMeters,8,0,22,0,0)];
        var bank=new ExitPolicy208(map,frame.LapMeters,_=>new(0,.8f),route,route);
        var shape=new Shape208(35,0,0,0,0,0,0,0,0,15,0,0,0,5,8,-.65,0,0,0,0,0);
        var maneuver=new Maneuver208(4,shape,new(0,-.08,0));
        var state=Root(bend.Exit-10,22);
        var trace=ThrottleTrace209.Capture(()=>bank.At(state,bend,maneuver));
        Assert.Equal(0,trace.Action.Drive);
        Assert.True(trace.Trace.Stages.Single(s=>s.Name=="208 pace cap").Reduction>0);
        Assert.True(trace.Trace.Stages.Single(s=>s.Name=="208 ramp and recovery").After<0);
        Assert.Equal(0,trace.Trace.Stages.Single(s=>s.Name=="208 actuator bound").After);
        Assert.Equal(trace.Action,trace.Trace.Selected);
        Assert.Equal(0,trace.Trace.EffectiveRemovalDeltaAt208Output["ramp"]);
        Assert.Equal(0,trace.Trace.Reconstructed208Requested);
    }

    [Fact]
    public void CapturesAreCommandLocalAndRestoreTheOuterObserverAfterExceptions()
    {
        var outer=new ThrottleTrace209();ThrottleTrace209.Current=outer;
        try
        {
            Assert.Throws<InvalidOperationException>(()=>ThrottleTrace209.Capture(()=>throw new InvalidOperationException()));
            Assert.Same(outer,ThrottleTrace209.Current);
        }
        finally {ThrottleTrace209.Current=null;}
    }

    [Fact]
    public void RequestedThrottleCannotOvertakeThePendingActivationQueue()
    {
        var start=Root(0,0);var pending=new FreeRacingCommand(-.01,.09,new(0,.25f));
        start=start with {Pending=[pending]};var model=Model();
        var fork=model.BeginFeedback207(start,_=>new(0,.6f),100,.5,true);
        fork.Advance(10,.04);
        var before=fork.Checkpoint();
        Assert.Equal(0,before.Active.Drive);
        Assert.Equal(pending,before.Pending[0]);
        Assert.Contains(before.Pending,p=>p.Action.Drive==.6f&&p.ActivationAt>=.09);
        fork.Advance(30,.12);
        Assert.Equal(.6f,fork.Checkpoint().Active.Drive);
        Assert.Single(start.Pending);
        Assert.Equal(0,start.Active.Drive);
    }

    [Fact]
    public void PredictiveThrottlePreservesAnExactFeasibleSuccessorAndDoesNotMutateTheRoot()
    {
        var start=Root(0,0);var copy=start with {Pending=[..start.Pending]};var model=Model();
        var baseline=new FreeRacingAction(0,.3f);
        var selected=Recovery209.Select(start,baseline,new(0,1),model,_=>baseline,100);
        Assert.True(Beam207.SameCheckpoint(start,copy));
        Assert.InRange(selected.Drive,baseline.Drive,1);
        var continuation=model.BeginFeedback207(start,at=>at.State.Time<.025?selected:baseline,100,.5,true);
        continuation.Advance(150,1.2);
        Assert.Null(continuation.Outcome);
        Assert.InRange(continuation.Sweep,.5,100);
        Assert.True(continuation.LastPower!.Wheel.RearUtilization<=1.000001);
        Assert.True(continuation.LastContact!.Forces.TyreLoads!.FrontNormalNewtons>0);
    }

    [Fact]
    public void ForceAccountingIncludesTyreScrubAndResistanceInsteadOfLabellingRollOffAsABrake()
    {
        using var zip=new GZipStream(File.OpenRead(Path.Combine(Repository(),"data/experiments/speedway209/baseline/C.json.gz")),CompressionMode.Decompress);
        using var document=JsonDocument.Parse(zip);
        var rows=document.RootElement.GetProperty("Trace").EnumerateArray().ToArray();
        Assert.All(rows,r=>Assert.InRange(Math.Abs(r.GetProperty("ForceAcceleration").GetDouble()-
            r.GetProperty("ResistanceAcceleration").GetDouble()-r.GetProperty("FrontAcceleration").GetDouble()-
            r.GetProperty("RearAcceleration").GetDouble()),0,3e-5));
        Assert.Contains(rows,r=>r.GetProperty("Acceleration").GetDouble()<-.1&&
            r.GetProperty("Before").GetProperty("Active").GetProperty("Drive").GetDouble()>.5);
        Assert.Contains(rows,r=>r.GetProperty("Command").GetProperty("Selected").GetProperty("Drive").GetDouble()!=
            r.GetProperty("Before").GetProperty("Active").GetProperty("Drive").GetDouble());
    }

    [Fact]
    public void Frozen208StillCompletesAllFiveStartsWithTheExactPublishedModelTimes()
    {
        string root=Repository();var runs=Study191.VerifyPolicy209(root,Path.Combine(root,"data/experiments/speedway208/frozen-candidate.json"));
        double[] times=[51.85324838518148,51.882078,51.865243,51.870619,51.868291];
        Assert.Equal(5,runs.Length);
        for(int i=0;i<runs.Length;i++)
        {
            Assert.True(runs[i].Complete);Assert.InRange(runs[i].Sweep,.5,100);
            Assert.InRange(Math.Abs(runs[i].Finish!.Value-times[i]),0,1e-6);
        }
    }

    [Fact]
    public void Frozen209PreservesFiveStrictFullRacesWithoutChangingPhysics()
    {
        string root=Repository();var runs=Study191.VerifyPolicy209(root,Path.Combine(root,"data/experiments/speedway209/frozen-candidate.json"));
        Assert.Equal(5,runs.Length);Assert.All(runs,r=>{Assert.True(r.Complete);Assert.True(r.Finish<50);Assert.True(r.Sweep>=.5);});
    }

    [Fact]
    public void OriginalStraightSupportsMoreThan100KphWithoutAHiddenVehicleSpeedLimit()
    {
        using var zip=new GZipStream(File.OpenRead(Path.Combine(Repository(),"data/experiments/speedway209/diagnostics/capability.json.gz")),CompressionMode.Decompress);
        using var document=JsonDocument.Parse(zip);
        Assert.Contains(document.RootElement.GetProperty("Straights").EnumerateArray(),x=>
            x.GetProperty("Gas").GetDouble()==1&&x.GetProperty("Run").GetProperty("Complete").GetBoolean()&&
            x.GetProperty("Run").GetProperty("PeakKph").GetDouble()>120);
        Assert.Equal(0,Engine500.Torque(Engine500.CutoffRpm));
    }

    private static string Repository()
    {
        var folder=new DirectoryInfo(AppContext.BaseDirectory);
        while(folder is not null&&!Directory.Exists(Path.Combine(folder.FullName,"tools/speedway-h26")))folder=folder.Parent;
        return folder?.FullName??throw new DirectoryNotFoundException();
    }
    private static Start197 Root(double s,double speed)
    {
        var frame=Cyclic172.TrafficWorld191.Frame();var pose=frame.At(s,9.5);
        var rider=new RiderView(1,pose.Position,pose.Road.Tangent*(float)speed,(float)pose.Road.Heading,0,
            pose.Road,s,0,RiderActivity.Racing,0,9.5f,0);
        return new(new(rider,0,speed/.33,0,0,4000*Math.PI/30),default,[],0);
    }
    private static Predictor198 Model()
    {
        var frame=Cyclic172.TrafficWorld191.Frame();var spec=Cyclic172.TrafficWorld191.Rider(StartingGate.D);
        spec=spec with {Setup=new(1,spec.Setup.TractionBias)};
        return new(frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters(),
            new HandlingSettings {H26=H26Configuration.For("H26-LE"),PostureH27=new(),DoubleContactSolve=true,
                CoupledRearTyre=true,FastCoupledCertificate=true},new(0,.05,.05),spec,147,52.72097f,new(new BendMap(frame)));
    }
}
