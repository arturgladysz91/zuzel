using System.Numerics;
using CoreSim.Analysis;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
[Collection("Box2D worlds")]
public sealed class FullHeatSoloAuditTests
{
    [Fact]
    public void CrossingsInterpolateOnlyForwardIntervalsAndDoNotCountTheStartLine()
    {
        var laps = new LapCrossings();
        laps.Accept(0, 0, 1, 0, 100, 4); Assert.Empty(laps.Times);
        laps.Accept(1, 0, 2, 90, 100, 4); Assert.Empty(laps.Times);
        laps.Accept(2, 90, 3, 110, 100, 4); Assert.Equal(2.5, Assert.Single(laps.Times));
        laps.Accept(3, 110, 4, 99, 100, 4); Assert.Single(laps.Times);
        laps.Accept(4, 99, 5, 110, 100, 4); Assert.Single(laps.Times);
        laps.Accept(5, 110, 8, 410, 100, 4);
        Assert.Equal(new[] {2.5, 5.9, 6.9, 7.9}, laps.Times);
        laps.Accept(8, 410, 9, 510, 100, 4); Assert.Equal(4, laps.Times.Count);
    }

    private static readonly TrackFrame Frame = new(MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
    private static RiderView View(int id, double total, double distance)
    {
        var (position, road) = Frame.At(total, 4);
        return new(id, position, road.Tangent * 20, (float)road.Heading, 0, road, total, distance,
            RiderActivity.Racing, 1, 4, 0, RacingContext.NormalRacing);
    }
    private static WorldSnapshot Pair(double time, double first, double second) => new(time,
        new[] {View(1, first, time * 20), View(2, second, time * 20)});
    private static FullHeatPassObserver Established()
    {
        var observer = new FullHeatPassObserver();
        observer.Accept(Pair(0, 0, 0)); observer.Accept(Pair(.1, 5, 0)); observer.Accept(Pair(1.2, 27, 22));
        Assert.Empty(observer.Events); return observer;
    }
    [Fact]
    public void APassNeedsEstablishedRearPositionAlongsideAndOneSecondAndTenMetersOfWholeBikeLead()
    {
        var observer = Established();
        observer.Accept(Pair(1.3, 29, 29)); observer.Accept(Pair(1.4, 31, 36));
        observer.Accept(Pair(1.9, 41, 46)); Assert.Empty(observer.Events);
        observer.Accept(Pair(2.41, 51, 56));
        var pass = Assert.Single(observer.Events); Assert.Equal(2, pass.RiderId); Assert.Equal(1, pass.OpponentId);
        Assert.Equal(1.4, pass.ClearSinceSeconds); Assert.True(pass.HeldDistanceMeters >= 10);
    }
    [Fact]
    public void BriefReferenceFlipsAndLappedPhysicalNeighborsAreNotPositionPasses()
    {
        var observer = Established();
        observer.Accept(Pair(1.3, 29, 29.01)); observer.Accept(Pair(1.4, 31, 36));
        observer.Accept(Pair(1.6, 40, 35)); observer.Accept(Pair(3, 68, 63)); Assert.Empty(observer.Events);
        observer.Accept(Pair(4, Frame.LapMeters + 85, 80));
        observer.Accept(Pair(6, Frame.LapMeters + 125, 120)); Assert.Empty(observer.Events);
    }
    [Fact]
    public void SensitivityInputsChangeExactlyOneDeclaredAxisWithExplicitCanonicalData()
    {
        foreach (var family in new[] {"canonical", "legacy"})
        foreach (var attribute in family == "canonical" ? FullHeatSoloAudit.Canonical : FullHeatSoloAudit.Legacy)
        {
            var spec = Assert.Single(FullHeatSoloAudit.Solo(family, attribute, 80).Riders);
            Assert.True(spec.Profile.HasExplicitGameplay); Assert.False(spec.TrafficEnabled);
            var abilities = spec.Profile.Gameplay.Abilities;
            foreach (var name in FullHeatSoloAudit.Canonical)
                Assert.Equal(family == "canonical" && attribute == name ? 80 : 50,
                    typeof(RiderAbilities).GetProperty(name)!.GetValue(abilities));
            foreach (var name in FullHeatSoloAudit.Legacy)
                Assert.Equal(family == "legacy" && attribute == name ? 80f : 50f,
                    typeof(RiderSkills).GetProperty(name)!.GetValue(spec.Profile.Skills));
        }
    }
    [Fact]
    public void ObserverAgreesWithFinishTimingAndPreservesExactWorldHashOnAFullHeat()
    {
        var s = FullHeatSoloAudit.Heat(false, 0);
        var observed = FullHeatSoloAudit.Execute(s);
        using var world = new WorldRunner(s.Riders.Reverse(), parameters: FullHeatSoloAudit.Parameters());
        var bare = world.Run(s.Name, .01, 90, 4, stopOnCrash: true);
        Assert.Equal("Completed", observed.Status); Assert.Equal(bare.Fingerprint, observed.Result.Fingerprint);
        foreach (var rider in observed.Riders) {
            Assert.Equal(4, rider.LapSeconds.Length);
            Assert.Equal(bare.FinishTimes[rider.RiderId], rider.LapSeconds.Sum(), 8);
        }
        Assert.Equal(bare.Work.ControlChanges, observed.Riders.Sum(r => r.ActualControlChanges));
    }
    [Fact]
    public void CrashIsInterruptedWithoutInventingDnfClassification()
    {
        var s = Scenarios.E(); var run = FullHeatSoloAudit.Execute(s);
        Assert.Equal("Interrupted-Crash", run.Status); Assert.NotNull(run.CrashSnapshot);
        Assert.NotEmpty(run.Consequences); Assert.NotEmpty(run.Result.Contacts);
        Assert.True(run.Result.TimeSeconds < s.DurationSeconds); Assert.Empty(run.Result.FinishTimes);
        Assert.Contains(run.Result.Riders, r => r.Activity == RiderActivity.Crashed);
    }
}
