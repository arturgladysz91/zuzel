using System.Text.Json;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class AutonomousRacingLineTests
{
    private static string Evidence(string folder,string file) {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent) {
            var p=Path.Combine(dir.FullName,"docs/experiments",folder,file+".json");if(File.Exists(p))return p;
        }throw new FileNotFoundException(file);
    }
    [Fact]
    public void FlagIsOffAndOnlyRequiresExperimentalTactics() {
        Assert.False(new WorldParameters().EnableAutonomousRacingLine);
        var s=AutonomousLineAudit.Solos()[2];
        Assert.Throws<ArgumentException>(()=>new WorldRunner(s.Riders,parameters:new(){EnableAutonomousRacingLine=true}));
        using var w=new WorldRunner(s.Riders,parameters:new(){EnableTacticalRacing=true,EnableAutonomousRacingLine=true});
        var r=w.Run(s.Name,.01,1);
        Assert.Contains(r.Decisions,d=>d.Reason.StartsWith("Autonomous",StringComparison.Ordinal)&&d.ActualActivation is not null);
    }
    [Fact]
    public void BendHorizonReachesTheExitAndTwentyReferenceMetersOfStraight() {
        var spec=AutonomousLineAudit.Solos()[2].Riders[0] with{ReferenceMeters=45,SpeedMetersPerSecond=19};
        using var w=new WorldRunner(new[]{spec},parameters:AutonomousLineAudit.Parameters(true));
        var own=w.Snapshot.Riders[0];var h=AutonomousRacingLine.HorizonSeconds(own,spec,w.Frame);
        var end=own.Road.ReferenceMeters+w.Frame.ReferenceSpeed(own)*(RiderController.ReactionSeconds(spec)+h);
        var corner=w.Frame.Track.CornerTopology.CornerForSegment(own.Road.Segment)!;
        var exit=w.Frame.Embedding.Segments.Take(corner.StartSegmentIndex+corner.SegmentCount).Sum(s=>s.SegmentType==CoreSim.SegmentType.Straight?s.StraightLengthMeters:s.InnerRadiusMeters*s.TurnAngleRadians);
        Assert.Equal(exit+20,end,7);Assert.False(w.Frame.At(end,own.Road.OffsetMeters).Road.IsTurn);
    }
    [Fact]
    public void FlagOffPreservesFiveFrozen94HeatsExactly() {
        foreach(var s in AutonomousLineAudit.Heats()) {
            var old=JsonSerializer.Deserialize<GapRun>(File.ReadAllText(Evidence("close-racing-following-gap",s.Name+"-baseline")),Audit.Json)!;
            using var w=new WorldRunner(s.Riders,parameters:AutonomousLineAudit.Parameters());
            var r=w.Run(s.Name,.01,90,4,stopOnCrash:true,lapsFromInitialPosition:CloseRacingAudit.IsRolling(s));
            Assert.Equal(old.Result.Fingerprint,r.Fingerprint);Assert.Equal(old.Result.Decisions,r.Decisions);Assert.Equal(old.Result.Contacts,r.Contacts);
            Assert.Equal(old.Result.Riders,r.Riders);Assert.All(r.FinishTimes,p=>Assert.Equal(old.Result.FinishTimes[p.Key],p.Value));
        }
    }
    [Fact]
    public void AdaptiveFollowing94AlsoRetainsItsExactTrajectoryWithAutonomyOff() {
        var s=CloseRacingAudit.Rolling(false);
        var old=JsonSerializer.Deserialize<GapRun>(File.ReadAllText(Evidence("close-racing-following-gap",s.Name+"-candidate")),Audit.Json)!;
        using var w=new WorldRunner(s.Riders,parameters:CloseRacingAudit.Variant(3));
        var r=w.Run(s.Name,.01,90,4,stopOnCrash:true,lapsFromInitialPosition:true);
        Assert.Equal(old.Result.Fingerprint,r.Fingerprint);Assert.Equal(old.Result.Decisions,r.Decisions);
    }
    [Fact]
    public void HoldingTheInnerSoloLineDoesNotInventACommandOrSpeedBonus() {
        var s=AutonomousLineAudit.Solos()[0];var a=AutonomousLineAudit.Execute(s);var b=AutonomousLineAudit.Execute(s,true);
        Assert.True(b.Run.Result.Completed);Assert.Equal(a.Run.Result.Fingerprint,b.Run.Result.Fingerprint);Assert.Empty(b.Run.Result.Decisions);
    }
    [Fact]
    public void SoloPreparesBeforeTheBendAndExecutesFiniteDelayedLateralMovement() {
        var s=AutonomousLineAudit.Solos()[2];var r=AutonomousLineAudit.Execute(s,true);
        var entry=r.Phases.First(x=>x.Phase=="Entry");var command=r.Run.Result.Decisions.First(d=>d.Reason=="AutonomousApproach");
        Assert.True(command.ActualActivation<entry.TimeSeconds);Assert.InRange(entry.OffsetMeters,command.TargetOffsetMeters+.1,6.99);
        Assert.Equal(4,r.Run.Riders[0].Measurement.LapSeconds.Length);Assert.True(r.Run.Result.Completed);Assert.Empty(r.Run.Anomalies);
        Assert.All(r.Run.Result.Decisions,d=>{
            Assert.Equal(ControlChannels.Line,d.Channels);Assert.Equal(1,d.Drive);Assert.InRange(d.TargetOffsetMeters,.5f,9.5f);
            Assert.Equal(RiderController.ReactionSeconds(s.Riders[0]),d.RequestedActivation-d.ObservedAt,9);
            Assert.True(d.ActualActivation+1e-9>=d.RequestedActivation);
        });
    }
    [Fact]
    public void ReturnsUseSeparateAutonomousGoalAndPreserveHistoricalHomeOff() {
        var s=AutonomousLineAudit.Solos()[1];var spec=s.Riders[0] with{OffsetMeters=5.5f,TargetOffsetMeters=5.5f};
        foreach(var enabled in new[]{false,true}) {
            using var w=new WorldRunner(new[]{spec},parameters:AutonomousLineAudit.Parameters(enabled));
            var snapshot=w.Snapshot with{TimeSeconds=2};
            var memory=new TacticalMemory{HomeOffsetMeters=9.5f,AutonomousOffsetMeters=5.5f,Intent=RacingIntent.ReturnLine,ClearSince=.1};
            var decision=TrafficDecision.Observe(snapshot,snapshot.Riders[0],spec,w.Parameters,new(),memory,Array.Empty<RacingPair>(),w.Frame,TrackState.CreateDefault(w.Frame.Track).Snapshot());
            Assert.NotNull(decision);Assert.Equal(enabled?5.5f:9.5f,decision.TargetOffsetMeters);Assert.Equal(9.5f,memory.HomeOffsetMeters);
        }
    }
    [Fact]
    public void OccupiedNeighborBlocksUsefulCandidateAndActiveOppositeAttackWins() {
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(AutonomousLineAudit.Conflicts(),Audit.Json));
        var blocked=doc.RootElement.GetProperty("BlockedNeighbor");
        Assert.Contains(blocked.GetProperty("Evaluations").EnumerateArray(),e=>e.GetProperty("TimeSeconds").GetDouble()==.1
            &&e.GetProperty("TargetMeters").GetDouble()==5.5&&!e.GetProperty("Available").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("ActiveAttackSnapshot").GetProperty("PreservedActiveAttack").GetBoolean());
    }
    [Fact]
    public void AutonomousSuggestionsNeverReopenTheIndependentThrottleOrReplaceEmergency() {
        var s=ThrottleAudit.Cases()[3];var r=AutonomousLineAudit.Execute(s,true).Run;
        Assert.Equal("Interrupted-Crash",r.Status);Assert.NotEmpty(r.Result.Contacts);
        Assert.DoesNotContain(r.Result.Decisions,d=>d.Reason.StartsWith("Autonomous",StringComparison.Ordinal));
        Assert.Contains(r.Result.Decisions,d=>d.SafetyReaction&&d.Channels==ControlChannels.Throttle&&d.Drive==0&&d.ActualActivation>=d.RequestedActivation);
    }
    [Fact]
    public void ReversedInputKeepsExactStatesCommandsContactsAndNoPostFinishActivation() {
        var s=AutonomousLineAudit.Heats().Single(s=>s.Name=="rolling-straight");
        var a=AutonomousLineAudit.Execute(s,true);var b=AutonomousLineAudit.Execute(s,true,reverse:true);
        Assert.Equal(a.Run.Result.Fingerprint,b.Run.Result.Fingerprint);Assert.Equal(a.Run.Result.Decisions,b.Run.Result.Decisions);Assert.Equal(a.Run.Result.Contacts,b.Run.Result.Contacts);
        Assert.All(a.Run.Result.Decisions.Where(d=>d.ActualActivation is not null),d=>Assert.True(d.ActualActivation<=a.Run.Result.FinishTimes[d.RiderId]));
        Assert.All(a.Run.Result.Decisions.Where(d=>d.Reason.StartsWith("Autonomous",StringComparison.Ordinal)),d=>Assert.Equal(ControlChannels.Line,d.Channels));
    }
}
