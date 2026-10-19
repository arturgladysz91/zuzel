using System.Numerics;
using System.Text.Json;
using CoreSim.Analysis;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
[Collection("Box2D worlds")]
public sealed class ProportionalThrottleTests
{
    private static string Evidence(string name)
    {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent) {
            var path=Path.Combine(dir.FullName,"docs/experiments/full-heat-and-solo-validation",name+".json");
            if(File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Missing frozen #92 evidence: "+name);
    }
    [Fact]
    public void FlagIsOffByDefaultAndRequiresExperimentalTactics()
    {
        Assert.False(new WorldParameters().EnableProportionalTrafficThrottle);
        Assert.Throws<ArgumentException>(() => new WorldRunner(ThrottleAudit.Cases()[0].Riders,
            parameters:new() {EnableProportionalTrafficThrottle=true}));
    }
    [Fact]
    public void BlockedFollowingUsesPartialDriveAndRetainsWholeBikeSeparation()
    {
        var s = ThrottleAudit.Cases()[0]; var r = ThrottleAudit.Execute(s,true);
        Assert.True(r.Riders[0].PartialSeconds > .3); Assert.Empty(r.Result.Contacts);
        Assert.True(r.Riders[0].Measurement.MinimumPeerGapMeters > 0);
        Assert.Contains(r.Result.Decisions,d=>d.Channels==ControlChannels.Throttle && d.Drive is > 0 and < 1);
        Assert.All(r.Result.Decisions,d=>Assert.True(float.IsFinite(d.Drive) && d.Drive is >=0 and <=1));
    }
    [Fact]
    public void StableClosePairDoesNotLoseThrottleBeforeGeometryChanges()
    {
        var r=ThrottleAudit.Execute(ThrottleAudit.Cases()[1],true);
        Assert.All(r.Samples.Where(s=>s.RiderId==1 && s.TimeSeconds<2),s=>Assert.Equal(1,s.Drive));
        Assert.DoesNotContain(r.Result.Decisions,d=>d.RiderId==1 && d.ObservedAt<2 && d.Channels==ControlChannels.Line);
    }
    [Fact]
    public void FollowingDoesNotBlockLaterOffensiveCommands()
    {
        var r=ThrottleAudit.Execute(ThrottleAudit.Cases()[2],true);
        Assert.True(r.Riders[0].PartialSeconds>.3); Assert.True(r.Riders[0].Reattacks>0);
        Assert.Contains(r.Result.Decisions,d=>d.RiderId==1 && d.Channels==ControlChannels.Line
            && d.ActualActivation is not null && d.Action is TrafficAction.AttackInside or TrafficAction.AttackOutside);
        Assert.Empty(r.Result.Contacts);
    }
    [Fact]
    public void EmergencyUsesFullReactionAndDoesNotHideTheUnavoidableCrash()
    {
        var s=ThrottleAudit.Cases()[3]; var r=ThrottleAudit.Execute(s,true);
        var command=r.Result.Decisions.First(d=>d.Channels==ControlChannels.Throttle && d.SafetyReaction);
        Assert.Equal(0,command.Drive); Assert.True(command.ActualActivation>=command.RequestedActivation);
        Assert.Equal(RiderController.ReactionSeconds(s.Riders[0]),command.RequestedActivation-command.ObservedAt,9);
        Assert.Equal("Interrupted-Crash",r.Status); Assert.NotEmpty(r.Result.Contacts);
    }
    [Fact]
    public void SafeParallelLinesRetainDriveAndSideContestCandidates()
    {
        var r=ThrottleAudit.Execute(ThrottleAudit.Cases()[4],true);
        Assert.All(r.Riders,v=>{Assert.Equal(0,v.ZeroSeconds);Assert.Equal(0,v.PartialSeconds);});
        Assert.True(r.Result.Work.SideCandidates>0); Assert.Empty(r.Result.Contacts);
    }
    [Fact]
    public void StraightBendBoundaryHasFiniteThrottleAndGapMeasures()
    {
        var r=ThrottleAudit.Execute(ThrottleAudit.Cases()[7],true);
        Assert.All(r.Observations,o=>{
            Assert.True(float.IsFinite(o.DesiredDrive));
            Assert.True(double.IsFinite(o.DesiredAcceleration));
            if(o.Gap is {} g) Assert.True(double.IsFinite(g.GapMeters) && double.IsFinite(g.ClosingMetersPerSecond));
        });
        Assert.Empty(r.Result.Contacts);
    }
    [Fact]
    public void HalfDriveIsNotHalfSpeedAndItsForceDependsOnExistingCapabilitiesAndCornerPhase()
    {
        var frame=new TrackFrame(MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var surface=TrackState.CreateDefault(frame.Track).Snapshot(); var values=new List<double>();
        foreach(var reference in new[]{10d,50d,115d}) foreach(var level in new[]{20,80}) {
            var spec=ThrottleAudit.Cases()[5].Riders[0];
            spec=spec with {Profile=RiderProfile.CreateCanonical(1,"R1",spec.Profile.Gameplay,new(50,level,level,50,50,50),spec.Profile.Style)};
            var (position,road)=frame.At(reference,4);
            var view=new RiderView(1,position,road.Tangent*18,(float)road.Heading,0,road,reference,0,RiderActivity.Racing,.5f,4,0);
            var mass=spec.Profile.Gameplay.Physical.MassKg+77;
            var f=RiderController.Forces(view,spec,frame,surface,ThrottleAudit.Parameters(true),1,mass,1).Force;
            values.Add(Vector2.Dot(f,road.Tangent)/mass);
            Assert.Equal(18,view.Speed,4);
        }
        Assert.True(values.Max()-values.Min()>.2); Assert.All(values,v=>Assert.True(double.IsFinite(v)));
    }
    [Fact]
    public void ThrottleUpdatesAreIndependentOfLateralHysteresisAndNeverPrecedeTheirDeadline()
    {
        var r=ThrottleAudit.Execute(ThrottleAudit.Cases()[0],true);
        var commands=r.Result.Decisions.Where(d=>d.Channels==ControlChannels.Throttle && d.ActualActivation is not null).ToArray();
        Assert.True(commands.Length>2);
        Assert.All(commands,d=>{
            Assert.InRange(d.ActualActivation!.Value-d.RequestedActivation,-1e-10,.01000001);
            Assert.Equal(.24,d.RequestedActivation-d.ObservedAt,6);
        });
        Assert.Contains(commands.Zip(commands.Skip(1)),p=>p.Second.ActualActivation-p.First.ActualActivation<.8);
    }
    [Fact]
    public void NewEmergencyCancelsQueuedOrdinaryDriveWithoutPostponingTheSafetyCommand()
    {
        var seed=ThrottleAudit.Cases()[0].Riders.Take(2).ToArray();
        var riders=seed.Select((r,i)=>r with {ReferenceMeters=seed[0].ReferenceMeters+i*6.5,SpeedMetersPerSecond=i==0?18:16,
            Profile=RiderProfile.CreateCanonical(r.Profile.Id,r.Profile.Name,r.Profile.Gameplay,new(50,i==0?80:20,50,50,50,50),r.Profile.Style)}).ToArray();
        var r=ThrottleAudit.Execute(new("queue-escalation",riders,2),true);
        var cancelled=r.Result.Decisions.Where(d=>d.Channels==ControlChannels.Throttle && !d.SafetyReaction && d.CancelledAt is not null).ToArray();
        Assert.NotEmpty(cancelled); Assert.All(cancelled,d=>Assert.Null(d.ActualActivation));
        Assert.Contains(r.Result.Decisions,d=>d.Channels==ControlChannels.Throttle && d.SafetyReaction && d.ActualActivation is not null);
    }
    [Fact]
    public void RepeatAndInputReversalPreserveCommandsContactsAndExactAcceptedHash()
    {
        var s=ThrottleAudit.Cases()[2];var a=ThrottleAudit.Execute(s,true);var b=ThrottleAudit.Execute(s,true,reverse:true);
        Assert.Equal(a.Result.Fingerprint,b.Result.Fingerprint);Assert.Equal(a.Result.Decisions,b.Result.Decisions);
        Assert.Equal(a.Result.Contacts,b.Result.Contacts);
    }
    [Fact]
    public void AllEightOffHeatsPreserveFrozen92BodiesCommandsContactsAndFinishTimes()
    {
        foreach(var varied in new[]{false,true}) for(var rotation=0;rotation<4;rotation++) {
            var s=FullHeatSoloAudit.Heat(varied,rotation);
            var original=JsonSerializer.Deserialize<AuditRun>(File.ReadAllText(Evidence(s.Name)),Audit.Json)!;
            using var w=new WorldRunner(s.Riders,parameters:ThrottleAudit.Parameters(false));
            var r=w.Run(s.Name,.01,90,4,stopOnCrash:true);
            Assert.Equal(original.Result.Fingerprint,r.Fingerprint);
            Assert.Equal(original.Result.Decisions,r.Decisions);Assert.Equal(original.Result.Contacts,r.Contacts);
            Assert.Equal(original.Result.Riders,r.Riders);
            Assert.All(r.FinishTimes,p=>Assert.Equal(original.Result.FinishTimes[p.Key],p.Value));
        }
    }
    [Fact]
    public void NoOpponentLeavesAnExactSoloTrajectoryAndNoThrottleCommands()
    {
        var s=FullHeatSoloAudit.Solo("canonical","Reaction",50); var a=ThrottleAudit.Execute(s,false);var b=ThrottleAudit.Execute(s,true);
        Assert.Equal(a.Result.Fingerprint,b.Result.Fingerprint);Assert.Empty(b.Result.Decisions);
        Assert.Equal(0,b.Result.Work.ThrottleForceEvaluations);
    }
    [Fact]
    public void AQueuedLineOpeningCannotOverwriteNewerPartialThrottle()
    {
        var s=ThrottleAudit.Cases()[2]; var trace=new List<WorldSnapshot>();
        using var w=new WorldRunner(s.Riders,parameters:ThrottleAudit.Parameters(true));trace.Add(w.Snapshot);
        var result=w.Run(s.Name,.01,s.DurationSeconds,acceptedObserver:trace.Add,stopOnCrash:true);
        var command=result.Decisions.First(d=>d.RiderId==1 && d.Channels==ControlChannels.Line && d.Drive==1 && d.ActualActivation is not null);
        var before=trace.Last(x=>x.TimeSeconds<=command.ActualActivation+1e-9).Riders.Single(v=>v.Id==1);
        var after=trace.First(x=>x.TimeSeconds>command.ActualActivation+1e-9).Riders.Single(v=>v.Id==1);
        Assert.InRange(before.Drive,.001f,.999f);Assert.Equal(before.Drive,after.Drive);
        Assert.Equal(command.TargetOffsetMeters,after.TargetOffsetMeters);
    }
    [Fact]
    public void ReobservingDangerDoesNotCancelAnAlreadyQueuedSafetyLine()
    {
        var s=ThrottleAudit.Cases()[3];using var w=new WorldRunner(s.Riders,parameters:ThrottleAudit.Parameters(true));
        var snapshot=w.Snapshot with{TimeSeconds=.2};var own=snapshot.Riders[0];var spec=s.Riders[0];
        var waiting=new DecisionEvent(1,2,TrafficAction.HoldLine,.1,.1,.34,null,own.TargetOffsetMeters,1,
            RacingIntent.None,"CorridorOccupied",true,Channels:ControlChannels.Line);
        var work=new WorldWork();var plan=TrafficDecision.ObserveThrottle(snapshot,own,spec,w.Parameters,work,new(),
            TrafficDecision.ObservePairs(snapshot,work),w.Frame,TrackState.CreateDefault(w.Frame.Track).Snapshot(),waiting);
        Assert.True(plan.Emergency);Assert.False(plan.CancelLine);
    }
}
