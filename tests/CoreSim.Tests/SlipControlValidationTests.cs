using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class SlipControlValidationTests
{
    [Fact]
    public void ReadOnlyStagesPreserve97ForcesAndExactlyAccountForBox2DTorque()
    {
        var spec=SlipDynamicsAudit.Rider(reference:65);var p=SlipControlValidation.Parameters();
        var forces=new List<ForceObservation>();var yaws=new List<YawLimitObservation>();var applied=new List<AppliedForceObservation>();
        using var observed=new WorldRunner(new[]{spec},parameters:p,diagnostics:new(){Force=forces.Add,YawLimit=yaws.Add,AppliedForce=applied.Add});
        var r=observed.Run("A-arc",.01,1);
        using var plain=new WorldRunner(new[]{spec},parameters:p);var other=plain.Run("A-arc",.01,1);
        Assert.Equal(other.Fingerprint,r.Fingerprint);Assert.Equal(other.Work.SolverCalls,r.Work.SolverCalls);
        Assert.Equal(forces.Count,yaws.Count);Assert.Equal(forces.Count,applied.Count);
        for(var i=0;i<yaws.Count;i++) {
            var y=yaws[i];Assert.Equal(forces[i].TimeSeconds,y.TimeSeconds);Assert.Equal(y.TimeSeconds,applied[i].TimeSeconds);
            Assert.Equal(forces[i].TorqueNewtonMeters,y.AfterEnergyNewtonMeters);
            Assert.Equal(y.AfterEnergyNewtonMeters,applied[i].TorqueNewtonMeters);
            Assert.Equal(forces[i].ForceNewtons,applied[i].ForceNewtons);
            Assert.True(Math.Abs(y.AfterAuthorityNewtonMeters)<=Math.Abs(y.RequestedNewtonMeters)+1e-4);
            Assert.True(Math.Abs(y.AfterEnergyNewtonMeters)<=Math.Abs(y.AfterAuthorityNewtonMeters)+1e-4);
            var workRate=y.AfterEnergyNewtonMeters*(double)y.AngularVelocity+.02*y.AfterEnergyNewtonMeters*y.AfterEnergyNewtonMeters/(2*y.InertiaKgMetersSquared);
            Assert.True(workRate<=y.DissipationCreditWatts+.01);
        }
        // Exact ON parity with the frozen #97 evidence, independently of observer parity.
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"docs/experiments/slip-coupled-dynamics/A-arc-ON.json")))root=root.Parent;
        Assert.NotNull(root);
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName,"docs/experiments/slip-coupled-dynamics/A-arc-ON.json")));
        Assert.Equal(frozen.RootElement.GetProperty("Run").GetProperty("Result").GetProperty("Fingerprint").GetString(),r.Fingerprint);
    }
    [Fact]
    public void DiagnosticStagesExerciseBothLimitsWithoutChangingTheReturnedTorqueOrAllocating()
    {
        var spec=SlipDynamicsAudit.Rider();using var world=new WorldRunner(new[]{spec});
        var p=SlipControlValidation.Parameters();var surface=TrackState.CreateDefault(world.Frame.Track).Snapshot();
        var state=world.Snapshot.Riders[0];var mechanical=world.MechanicalProperties(1);
        var authorityLimited=false;var energyLimited=false;YawLimitObservation sample=default;
        Action<YawLimitObservation> observe=y=>sample=y;
        foreach(var speed in new[]{.01f,.2f,18f})foreach(var heading in new[]{-.2f,0,.2f})foreach(var spin in new[]{-3f,-.001f,0,.001f,3f}) {
            var v=state with{Heading=heading,Velocity=new(speed,0),AngularVelocity=spin,Drive=0};
            var f=RiderController.Forces(v,spec,world.Frame,surface,p,1,mechanical.MassKg,mechanical.InertiaKgMetersSquared,observeYaw:observe);
            var plain=RiderController.Forces(v,spec,world.Frame,surface,p,1,mechanical.MassKg,mechanical.InertiaKgMetersSquared);
            Assert.Equal(plain,f);Assert.Equal(f.Torque,sample.AfterEnergyNewtonMeters);
            authorityLimited|=Math.Abs(sample.RequestedNewtonMeters-sample.AfterAuthorityNewtonMeters)>1e-5;
            energyLimited|=Math.Abs(sample.AfterEnergyNewtonMeters-sample.AfterAuthorityNewtonMeters)>1e-5;
        }
        Assert.True(authorityLimited);Assert.True(energyLimited);
        for(var i=0;i<10000;i++)RiderController.Forces(state,spec,world.Frame,surface,p,1,147,50,observeYaw:observe);
        var bytes=GC.GetAllocatedBytesForCurrentThread();
        for(var i=0;i<10000;i++)RiderController.Forces(state,spec,world.Frame,surface,p,1,147,50,observeYaw:observe);
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-bytes);
    }
    [Fact]
    public void ThrottleTrialsReplayExactlyTheSameAcceptedPrefixAndActivateCausally()
    {
        const double activation=2.82;var runs=new List<SlipControlValidation.Capture>();
        foreach(var drive in new[]{.4f,.7f,1f}) {
            var c=SlipControlValidation.Execute(new("gas",new[]{SlipDynamicsAudit.Rider()},activation+1.1),
                controls:new[]{new ControlCommand(1,activation,5,drive)});runs.Add(c);
            var command=Assert.Single(c.Result.Decisions);Assert.Equal(activation,command.ActualActivation);
            Assert.All(c.Ticks.Where(t=>t.TimeSeconds<activation-1e-9),t=>Assert.Equal(1,t.Before.Drive));
            var first=c.Ticks.First(t=>t.TimeSeconds>=activation-1e-9);
            Assert.True(first.Before.Road.IsTurn);Assert.Equal(drive,first.Before.Drive);
            Assert.All(c.Ticks.Where(t=>t.TimeSeconds>=activation-1e-9),t=>Assert.Equal(drive,t.Before.Drive));
        }
        Assert.Single(runs.Select(c=>SlipControlValidation.PrefixFingerprint(c,activation)).Distinct());
        var initial=runs.Select(c=>c.Ticks.First(t=>t.TimeSeconds>=activation-1e-9).Slip).ToArray();
        Assert.Single(initial.Select(s=>s.LateralForceNewtons).Distinct());Assert.Single(initial.Select(s=>s.TorqueNewtonMeters).Distinct());
        Assert.Equal(3,initial.Select(s=>s.DriveForceNewtons).Distinct().Count());
        Assert.Equal(3,runs.Select(c=>c.Result.Fingerprint).Distinct().Count());
    }
    [Fact]
    public void AuthoredLineCommandChangesIntentAtItsDeadlineWithoutTeleporting()
    {
        const double activation=2.91;
        var c=SlipControlValidation.Execute(new("line",new[]{SlipDynamicsAudit.Rider(8)},4),controls:new[]{new ControlCommand(1,activation,5,1)});
        var plain=SlipControlValidation.Execute(new("line",new[]{SlipDynamicsAudit.Rider(8)},4));
        Assert.Equal(SlipControlValidation.PrefixFingerprint(plain,activation),SlipControlValidation.PrefixFingerprint(c,activation));
        var first=c.Ticks.First(t=>t.TimeSeconds>=activation-1e-9);Assert.Equal(5,first.Before.TargetOffsetMeters);
        Assert.Equal(plain.Ticks.First(t=>t.TimeSeconds>=activation-1e-9).Before.Position,first.Before.Position);
        Assert.True(first.Before.Road.OffsetMeters>7); // intent is not a body transform
        Assert.True(c.Result.Riders[0].Road.OffsetMeters<first.Before.Road.OffsetMeters);
    }
    [Fact]
    public void FourIndividualLapsAreDeterministicAndRealActiveContactIsResolved()
    {
        var pack=SlipControlValidation.Pack();var forward=SlipControlValidation.Execute(pack);
        var reverse=SlipControlValidation.Execute(pack,reverse:true);
        Assert.True(forward.Result.Completed);Assert.Equal(4,forward.Result.FinishTimes.Count);
        Assert.Equal(forward.Result.Fingerprint,reverse.Result.Fingerprint);
        Assert.Equal(forward.Result.Work.SolverCalls,reverse.Result.Work.SolverCalls);
        Assert.All(forward.Ticks,t=>{Assert.True(t.ClearanceMeters>0);Assert.True(t.PairSeparationMeters>0);});
        var contact=SlipControlValidation.Execute(SlipControlValidation.Contact(),controls:SlipControlValidation.ContactControls());
        var contactReverse=SlipControlValidation.Execute(SlipControlValidation.Contact(),controls:SlipControlValidation.ContactControls(),reverse:true);
        Assert.True(CloseRacingAudit.Separation(contact.Initial.Riders[0],contact.Initial.Riders[1],0)>0);
        Assert.Contains(contact.ContactPoints,p=>p.TotalNormalImpulseNewtonSeconds>0);
        Assert.True(contact.Result.Work.Episodes>0);Assert.Equal(contact.Result.Contacts,contactReverse.Result.Contacts);
        Assert.Equal(contact.Result.Fingerprint,contactReverse.Result.Fingerprint);
        var last=contact.Result.Riders;Assert.True(CloseRacingAudit.Separation(last[0],last[1],contact.Result.TimeSeconds)>-.001);
        Assert.All(contact.Ticks,t=>Assert.True(float.IsFinite(t.After.Speed)&&float.IsFinite(t.After.AngularVelocity)));
    }
}
