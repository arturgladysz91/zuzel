using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class SlipCoupledDynamicsTests
{
    private static string Root() {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
            if(File.Exists(Path.Combine(d.FullName,"docs/experiments/slip-coupled-dynamics/baseline.json")))return d.FullName;
        throw new FileNotFoundException("Frozen slip baseline missing.");
    }
    [Fact]
    public void OffPreservesEveryFrozenAcceptedStateAndForce() {
        Assert.False(new WorldParameters().EnableSlipCoupledDynamics);
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(),"docs/experiments/slip-coupled-dynamics/baseline.json")));
        foreach(var row in doc.RootElement.GetProperty("Rows").EnumerateArray()) {
            var s=SlipDynamicsAudit.Cases().Single(x=>x.Name==row.GetProperty("Name").GetString());
            var observed=new List<ForceObservation>();
            using var w=new WorldRunner(s.Riders,parameters:new(){EnableSlipCoupledDynamics=false},
                diagnostics:new(){Force=observed.Add,Slip=_=>throw new InvalidOperationException("OFF slip instrumentation executed")});
            var r=w.Run(s.Name,.01,s.DurationSeconds);
            Assert.Equal(row.GetProperty("Result").GetProperty("Fingerprint").GetString(),r.Fingerprint);
            Assert.Equal(row.GetProperty("Forces").Deserialize<ForceObservation[]>(Audit.Json)!,observed.ToArray());
        }
    }
    [Theory]
    [InlineData(.1f)] [InlineData(-.1f)]
    public void ActualSlipGivesTheCorrectTurnSignAndOpposesSideVelocity(float heading) {
        using var w=new WorldRunner(new[]{SlipDynamicsAudit.Rider()});
        var v=w.Snapshot.Riders[0] with{Velocity=new(20,0),Heading=heading};var tyre=SlipCoupledDynamics.Tyre(v,0,1000,147);
        var f=new Vector2(-MathF.Sin(heading),MathF.Cos(heading))*tyre.LateralNewtons;
        Assert.Equal(Math.Sign(heading),Math.Sign(f.Y)); // CCW nose-in => left, mirrored CW => right
        Assert.True(Vector2.Dot(f,v.Velocity)<0);Assert.True(tyre.LateralNewtons*v.SideSpeed<0);
    }
    [Fact]
    public void OffAlsoMatchesTheUnmodifiedHistorical95AutonomousReplay()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(),"docs/experiments/autonomous-racing-line/rolling-S3-entry-ON.json")));
        var expected=doc.RootElement.GetProperty("Run").GetProperty("Result");
        var actual=AutonomousLineAudit.Execute(AutonomousLineAudit.Solos()[2],true).Run.Result;
        Assert.Equal(expected.GetProperty("Fingerprint").GetString(),actual.Fingerprint);
        Assert.Equal(expected.GetProperty("Decisions").Deserialize<DecisionEvent[]>(Audit.Json)!,actual.Decisions.ToArray());
    }
    [Fact]
    public void TyreIsContinuousAtZeroSlipAndSharesOneSaturatingCircleWithDrive() {
        using var w=new WorldRunner(new[]{SlipDynamicsAudit.Rider()});var state=w.Snapshot.Riders[0] with{Velocity=new(20,0),Heading=0};
        Assert.Equal(0,SlipCoupledDynamics.Tyre(state,500,1000,147).LateralNewtons);
        float previous=0;foreach(var angle in new[]{0f,.00001f,.03f,.1f,.3f,1f,1.5f}) {
            var v=state with{Heading=angle};var tyre=SlipCoupledDynamics.Tyre(v,1000,1000,147);
            Assert.True(Math.Abs(tyre.LateralNewtons)>=previous);previous=Math.Abs(tyre.LateralNewtons);
            Assert.InRange(tyre.Utilization,0,1.000001f);Assert.InRange(tyre.LateralLossWatts,0,float.MaxValue);
            Assert.True(tyre.DriveNewtons*tyre.DriveNewtons+tyre.LateralNewtons*tyre.LateralNewtons<=1000001);
        }
        var wrapped=state with{Velocity=new(-20,-.2f),Heading=MathF.PI-.01f};
        Assert.InRange(wrapped.SlideRadians,-.03f,-.01f);Assert.True(SlipCoupledDynamics.Tyre(wrapped,0,1000,147).LateralNewtons<0);
    }
    [Fact]
    public void ZeroDriveCannotGenerateMechanicalPowerAndForwardThrottlePowerIsMonotone() {
        var spec=SlipDynamicsAudit.Rider();var frame=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var surface=TrackState.CreateDefault(frame.Track).Snapshot();using var w=new WorldRunner(new[]{spec});
        var start=w.Snapshot.Riders[0];var properties=w.MechanicalProperties(1);var p=new WorldParameters{EnableSlipCoupledDynamics=true};
        foreach(var speed in new[]{0f,.01f,.2f,18f,35f})foreach(var heading in new[]{-3f,-.2f,0,.2f,3f})foreach(var spin in new[]{-3f,0,3f}) {
            var v=start with{Velocity=new(speed,0),Heading=heading,AngularVelocity=spin,Drive=0};
            var f=RiderController.Forces(v,spec,frame,surface,p,1,properties.MassKg,properties.InertiaKgMetersSquared);
            var power=Vector2.Dot(f.Force,v.Velocity)+f.Torque*v.AngularVelocity;
            Assert.True(power<=.05f,$"Generated zero-drive power {power}");
            if(v.ForwardSpeed>=0) {
                var full=RiderController.Forces(v with{Drive=1},spec,frame,surface,p,1,properties.MassKg,properties.InertiaKgMetersSquared);
                Assert.Equal(f.Torque,full.Torque);
                Assert.True(Vector2.Dot(full.Force,v.Velocity)+full.Torque*v.AngularVelocity>=power-.05f);
            }
        }
    }
    [Fact]
    public void ZeroDriveBox2DEnergyDoesNotIncreaseIncludingRotation() {
        var spec=SlipDynamicsAudit.Rider(reference:20,speed:22);
        using var w=new WorldRunner(new[]{spec},parameters:new(){EnableSlipCoupledDynamics=true},controls:new[]{new ControlCommand(1,0,5,0)});
        var p=w.MechanicalProperties(1);
        double Energy(RiderView v)=>.5*p.MassKg*v.Velocity.LengthSquared()+.5*p.InertiaKgMetersSquared*v.AngularVelocity*v.AngularVelocity;
        var previous=Energy(w.Snapshot.Riders[0]);
        w.Run("zero-drive",.01,8,acceptedObserver:s=>{var e=Energy(s.Riders[0]);Assert.True(e<=previous+.02,$"Energy increased {e-previous}");previous=e;});
    }
    [Theory]
    [InlineData(0f,.01)] [InlineData(.1f,.01)] [InlineData(18f,.005)] [InlineData(18f,.01)] [InlineData(18f,.02)]
    public void LowSpeedAndStraightCornerStraightTransitionsRemainFiniteAndOnTrack(float speed,double step) {
        using var w=new WorldRunner(new[]{SlipDynamicsAudit.Rider(speed:speed)},parameters:new(){EnableSlipCoupledDynamics=true});
        var r=w.Run("transition",step,30,1,s=>Assert.True(HeatMeasurements.Clearance(s.Riders[0],w.Frame)>0));
        Assert.True(r.Completed);Assert.Empty(r.Contacts);Assert.True(float.IsFinite(r.Riders[0].Speed));
    }
    [Theory]
    [InlineData(.005)] [InlineData(.01)] [InlineData(.02)]
    public void LowSpeedSideSlipCannotNumericallyCreateEnergyWithZeroDrive(double step)
    {
        var spec=SlipDynamicsAudit.Rider(speed:.01f) with{InitialOutwardSpeed=.05f};
        using var w=new WorldRunner(new[]{spec},parameters:new(){EnableSlipCoupledDynamics=true},controls:new[]{new ControlCommand(1,0,5,0)});
        var p=w.MechanicalProperties(1);
        double Energy(RiderView v)=>.5*p.MassKg*v.Velocity.LengthSquared()+.5*p.InertiaKgMetersSquared*v.AngularVelocity*v.AngularVelocity;
        var previous=Energy(w.Snapshot.Riders[0]);
        var r=w.Run("low-side-slip",step,.2,acceptedObserver:s=>{var e=Energy(s.Riders[0]);Assert.True(e<=previous+.00001,$"Low-speed energy increased {e-previous}");previous=e;});
        Assert.Equal((long)Math.Round(.2/step),r.Work.SolverCalls);
    }
    [Fact]
    public void DiagnosticsAndInputOrderCannotChangeTheWorldAndContactsRemainPhysical() {
        var s=SlipDynamicsAudit.Cases().Single(x=>x.Name=="D-pack");var p=new WorldParameters{EnableSlipCoupledDynamics=true};
        RunResult Run(bool reverse,bool diagnostics) {
            using var w=new WorldRunner(reverse?s.Riders.Reverse():s.Riders,parameters:p,diagnostics:diagnostics?new(){Slip=_=>{},Force=_=>{}}:null);
            return w.Run("pack",.01,10);
        }
        var a=Run(false,false);var b=Run(true,true);
        Assert.Equal(a.Fingerprint,b.Fingerprint);Assert.Equal(a.Contacts,b.Contacts);Assert.Equal(a.Work.SolverCalls,b.Work.SolverCalls);
        using var contact=new WorldRunner(Scenarios.C().Riders.Select(x=>x with{Passive=true}),parameters:p);var c=contact.Run("physical-contact",.01,1);
        Assert.Contains(c.Contacts,x=>x.Kind=="begin");Assert.True(c.Work.SolvedPoints>0);
    }
    [Fact]
    public void AnalyticalScoringIsRejectedAndAllocationFreePreviewMatchesNativeFrame() {
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{SlipDynamicsAudit.Rider()},parameters:new(){EnableSlipCoupledDynamics=true,EnableTacticalRacing=true}));
        var f=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        for(double s=-1;s<2*f.LapMeters;s+=.13)Assert.Equal(f.At(s,5).Road,f.RoadAt(s,5));
    }
    [Fact]
    public void InfeasibleSpeedCannotBeHeldOnTheRequestedRadius()
    {
        var samples=new List<SlipObservation>();
        using var w=new WorldRunner(new[]{SlipDynamicsAudit.Rider(reference:65,speed:35)},
            parameters:new(){EnableSlipCoupledDynamics=true},diagnostics:new(){Slip=samples.Add});
        var initial=w.Snapshot.Riders[0];var r=w.Run("infeasible",.01,.5);
        Assert.True(r.Riders[0].Road.OffsetMeters>initial.Road.OffsetMeters+.3);
        Assert.All(samples,s=>Assert.InRange(s.GripUtilization,0,1.000001f));
    }
    [Fact]
    public void HeadingControlIsContinuousAtTheLowSpeedTravelDirectionThreshold()
    {
        var spec=SlipDynamicsAudit.Rider();var frame=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var surface=TrackState.CreateDefault(frame.Track).Snapshot();using var w=new WorldRunner(new[]{spec});
        var p=new WorldParameters{EnableSlipCoupledDynamics=true};var start=w.Snapshot.Riders[0] with{Heading=0,AngularVelocity=.1f,Drive=0};
        var a=RiderController.Forces(start with{Velocity=new(0,.0099f)},spec,frame,surface,p,1,147,50);
        var b=RiderController.Forces(start with{Velocity=new(0,.0101f)},spec,frame,surface,p,1,147,50);
        Assert.InRange(Math.Abs(a.Torque-b.Torque),0,1);
    }
    [Fact]
    public void ForcesDoNotAllocateAndTargetOrientationDoesNotReplaceAcceptedSlip() {
        var spec=SlipDynamicsAudit.Rider(reference:65);var p=new WorldParameters{EnableSlipCoupledDynamics=true};
        var frame=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());var surface=TrackState.CreateDefault(frame.Track).Snapshot();
        using var w=new WorldRunner(new[]{spec});var v=w.Snapshot.Riders[0] with{Heading=0,Velocity=new(18,0)};
        for(var i=0;i<10000;i++)RiderController.Forces(v,spec,frame,surface,p,1,147,50);
        var bytes=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<10000;i++)RiderController.Forces(v,spec,frame,surface,p,1,147,50);
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-bytes);
        SlipObservation row=default;RiderController.Forces(v,spec,frame,surface,p,1,147,50,observeSlip:o=>row=o);
        Assert.InRange(Math.Abs(row.ActualSlipDegrees),0,.01);Assert.True(row.TargetSlipDegrees>0);
        Assert.InRange(Math.Abs(row.LateralForceNewtons),0,.1);
    }
}
