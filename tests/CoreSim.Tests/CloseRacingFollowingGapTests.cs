using System.Text.Json;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class CloseRacingFollowingGapTests
{
    private static string Evidence(string folder,string file) {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent) {
            var path=Path.Combine(dir.FullName,"docs/experiments",folder,file+".json");if(File.Exists(path))return path;
        }
        throw new FileNotFoundException("Missing frozen evidence: "+file);
    }
    [Fact]
    public void DefaultIsOffAndAdaptiveGapRequiresProportionalThrottle() {
        Assert.False(new WorldParameters().EnableClosureAdaptiveFollowingGap);
        Assert.Throws<ArgumentException>(()=>new WorldRunner(CloseRacingAudit.Rolling(false).Riders,
            parameters:new() {EnableTacticalRacing=true,EnableClosureAdaptiveFollowingGap=true}));
    }
    [Fact]
    public void PreferredSpacingUsesPositiveClosureWhileLegacyStillUsesAbsoluteSpeed() {
        var legacy=CloseRacingAudit.Variant(1);var adaptive=CloseRacingAudit.Variant(3);
        Assert.Equal(2.6,TrafficThrottle.PreferredGap(20,0,.24,-2,legacy),12);
        Assert.Equal(.6,TrafficThrottle.PreferredGap(20,0,.24,-2,adaptive),12);
        Assert.Equal(.6,TrafficThrottle.PreferredGap(30,-3,.24,-2,adaptive),12);
        Assert.True(TrafficThrottle.PreferredGap(20,3,.24,-2,adaptive)>TrafficThrottle.PreferredGap(20,1,.24,-2,adaptive));
        Assert.True(TrafficThrottle.PreferredGap(20,3,.4,-1,adaptive)>TrafficThrottle.PreferredGap(20,3,.2,-2,adaptive));
    }
    [Fact]
    public void NewFlagOffPreservesAllEightFrozen93OnHeatsExactly() {
        foreach(var varied in new[]{false,true})for(var rotation=0;rotation<4;rotation++) {
            var s=FullHeatSoloAudit.Heat(varied,rotation);
            var old=JsonSerializer.Deserialize<ThrottleRun>(File.ReadAllText(Evidence("proportional-throttle-control",s.Name+"-ON")),Audit.Json)!;
            using var w=new WorldRunner(s.Riders,parameters:CloseRacingAudit.Variant(1));
            var r=w.Run(s.Name,.01,90,4,stopOnCrash:true);
            Assert.Equal(old.Result.Fingerprint,r.Fingerprint);Assert.Equal(old.Result.Riders,r.Riders);
            Assert.Equal(old.Result.Decisions,r.Decisions);Assert.Equal(old.Result.Contacts,r.Contacts);
            Assert.All(r.FinishTimes,p=>Assert.Equal(old.Result.FinishTimes[p.Key],p.Value));
        }
    }
    [Fact]
    public void RollingPackHasValidWholeCapsulesAndDoesNotCountItsInitialOrderAsAPass() {
        var s=CloseRacingAudit.Rolling(true);using var w=new WorldRunner(s.Riders,parameters:CloseRacingAudit.Variant(3));
        CloseRacingAudit.ValidateInitial(w);
        var passes=new FullHeatPassObserver();passes.Accept(w.Snapshot);
        w.Run(s.Name,.01,1,acceptedObserver:passes.Accept);
        Assert.Empty(passes.Events);
    }
    [Fact]
    public void RollingFinishMeasuresFourCompleteReferenceCircuitsAndStopsOwnTelemetryAtFinish() {
        var r=JsonSerializer.Deserialize<GapRun>(File.ReadAllText(Evidence("close-racing-following-gap","rolling-straight-candidate")),Audit.Json)!;
        Assert.True(r.Result.Completed);Assert.Equal(4,r.Result.FinishTimes.Count);
        foreach(var v in r.Result.Riders) {
            var start=r.Initial.Riders.Single(x=>x.Id==v.Id).TotalReferenceMeters;
            Assert.InRange(v.TotalReferenceMeters-start,4*318.77874994277954,4*318.77874994277954+35);
            var m=r.Riders.Single(x=>x.Measurement.RiderId==v.Id);
            Assert.Equal(4,m.Measurement.LapSeconds.Length);
            Assert.Equal(r.Result.FinishTimes[v.Id],m.Measurement.LapSeconds.Sum(),8);
            Assert.Equal(r.Result.FinishTimes[v.Id],m.ZeroSeconds+m.PartialSeconds+m.FullSeconds,7);
            Assert.DoesNotContain(r.Result.Decisions,d=>d.RiderId==v.Id&&d.ActualActivation>r.Result.FinishTimes[v.Id]+1e-9);
        }
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void SevenFrozenSituationsKeepFiniteCommandsAndTheCompleteReactionDelay(int index) {
        var s=CloseRacingAudit.ShortCases()[index];var r=CloseRacingAudit.Execute(s,CloseRacingAudit.Variant(3));
        Assert.Equal("Completed",r.Status);Assert.Empty(r.Result.Contacts);Assert.Empty(r.Anomalies);
        Assert.All(r.Result.Decisions,d=>{
            Assert.True(float.IsFinite(d.Drive));Assert.InRange(d.Drive,0,1);
            var delay=RiderController.ReactionSeconds(s.Riders.Single(x=>x.Profile.Id==d.RiderId));
            Assert.Equal(delay,d.RequestedActivation-d.ObservedAt,9);
            if(d.ActualActivation is {} t)Assert.True(t+1e-9>=d.RequestedActivation);
        });
        if(index==3)Assert.True(r.Riders[0].ClosingSignChanges>0);
        if(index==4) {
            Assert.True(r.Riders[0].PartialFollowingSeconds>.3);
            Assert.Contains(r.Result.Decisions,d=>d.Channels==ControlChannels.Line&&d.Action is TrafficAction.AttackInside or TrafficAction.AttackOutside&&d.ActualActivation>1);
        }
        if(index==5)Assert.All(r.Riders,x=>{Assert.Equal(0,x.ZeroSeconds);Assert.Equal(0,x.PartialSeconds);});
    }
    [Fact]
    public void ImmediateThreatStillCrashesAfterTheSamePhysicalReaction() {
        var s=ThrottleAudit.Cases()[3];var a=CloseRacingAudit.Execute(s,CloseRacingAudit.Variant(1));var b=CloseRacingAudit.Execute(s,CloseRacingAudit.Variant(3));
        Assert.Equal("Interrupted-Crash",b.Status);Assert.NotEmpty(b.Result.Contacts);
        Assert.Equal(a.Result.Fingerprint,b.Result.Fingerprint);Assert.Equal(a.Result.Contacts,b.Result.Contacts);
        Assert.Contains(b.Result.Decisions,d=>d.Channels==ControlChannels.Throttle&&d.SafetyReaction&&d.Drive==0);
    }
    [Fact]
    public void SoloHasNoGapForceProbesOrNewCommands() {
        var s=FullHeatSoloAudit.Solo("canonical","Reaction",50) with {DurationSeconds=3,Laps=0};
        var a=CloseRacingAudit.Execute(s,CloseRacingAudit.Variant(1));var b=CloseRacingAudit.Execute(s,CloseRacingAudit.Variant(3));
        Assert.Equal(a.Result.Fingerprint,b.Result.Fingerprint);Assert.Empty(b.Result.Decisions);Assert.Equal(0,b.Result.Work.ThrottleForceEvaluations);
    }
    [Fact]
    public void ReversedRollingInputPreservesAcceptedStatesCommandsAndContacts() {
        var s=CloseRacingAudit.Rolling(false) with {DurationSeconds=8,Laps=0};
        var a=CloseRacingAudit.Execute(s,CloseRacingAudit.Variant(3));var b=CloseRacingAudit.Execute(s,CloseRacingAudit.Variant(3),reverse:true);
        Assert.Equal(a.Result.Fingerprint,b.Result.Fingerprint);Assert.Equal(a.Result.Decisions,b.Result.Decisions);Assert.Equal(a.Result.Contacts,b.Result.Contacts);
    }
}
