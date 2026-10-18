using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
[Collection("Box2D worlds")]
public sealed class TacticalQualityTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public void QualityRequiresBothExperimentalPolicies(bool tactics, bool side)
        => Assert.Throws<ArgumentException>(() => new WorldRunner(SideBySideScenarios.A().Riders,
            parameters: new() {EnableTacticalRacing = tactics, EnableSideBySideContests = side, EnableTacticalQualityScoring = true}));

    [Theory]
    [InlineData("C-loaded-tighten")] [InlineData("H-original-F")]
    public void LoadedTighteningHasTransientCostDespiteFasterLegacyTerminalRate(string name)
    {
        var c = TacticalQualityAudit.Cases().Single(c => c.Name == name);
        using var world = new WorldRunner(c.Scenario.Riders);
        world.Run(c.Name, .01, c.ObserveAt);
        var own = world.Snapshot.Riders.Single(r => r.Id == c.RiderId);
        var spec = c.Scenario.Riders.Single(r => r.Profile.Id == c.RiderId);
        var surface = TrackState.CreateDefault(world.Frame.Track).Snapshot(); var p = new WorldParameters();
        var inside = (float)own.Road.OffsetMeters - 1;
        var time = ContestScoring.HorizonSeconds(own, spec, p, world.Frame, c.Defending);
        var legacy = ContestScoring.LegacyRate(own, spec, p, world.Frame, surface, inside, time)
            - ContestScoring.LegacyRate(own, spec, p, world.Frame, surface, own.TargetOffsetMeters, time);
        var gain = ContestScoring.Progress(own, spec, p, world.Frame, surface, inside, time)
            - ContestScoring.Progress(own, spec, p, world.Frame, surface, own.TargetOffsetMeters, time);
        Assert.True(legacy > .2); Assert.True(gain < 0);
        Assert.Equal(0, ContestScoring.Progress(own, spec, p, world.Frame, surface, inside, 0));
    }

    [Fact]
    public void UsefulEntryManeuverSurvivesAndRealGatesPreservePrefixUntilChangedActivation()
    {
        var a = TacticalAudit.Execute(SideBySideScenarios.A(), .01, true, side: true, quality: true);
        Assert.Contains(a.Result.Decisions, d => d.Reason == "ContestInside" && d.ActualActivation == .34);
        var scenario = SideBySideScenarios.F();
        var old = TacticalAudit.Execute(scenario, .01, true, side: true);
        var quality = TacticalAudit.Execute(scenario, .01, true, side: true, quality: true);
        var prefix = TacticalQualityAudit.Prefix(scenario, old, quality);
        Assert.True(prefix.Exact); Assert.Equal(4.344, prefix.FirstDifferentActivation!.Value, 9);
        Assert.True(prefix.CommonSamples > 400);
        Assert.DoesNotContain(quality.Result.Decisions, d => d.RiderId == 1 && d.Reason == "ContestInside" && d.ObservedAt < 6);
        Assert.InRange(quality.MaximumGripRatio, 0, 1.000001);
    }

    [Theory]
    [InlineData("SA-entry-parallel")] [InlineData("SF-varied-Motoarena")] [InlineData("SC-late-reaction")] [InlineData("SG-F-close")]
    public void QualityIsDeterministicAndIndependentOfInputOrder(string name)
    {
        var scenario = SideBySideScenarios.Cases().Single(s => s.Name == name);
        var first = TacticalAudit.Execute(scenario, .01, true, side: true, quality: true);
        var repeat = TacticalAudit.Execute(scenario, .01, true, side: true, quality: true);
        var reverse = TacticalAudit.Execute(scenario, .01, true, reverse: true, side: true, quality: true);
        Assert.Equal(first.Result.Fingerprint, repeat.Result.Fingerprint);
        Assert.Equal(first.Result.Fingerprint, reverse.Result.Fingerprint);
        Assert.Equal(first.Result.Decisions, reverse.Result.Decisions); Assert.Equal(first.Result.Contacts, reverse.Result.Contacts);
        Assert.InRange(first.MaximumGripRatio, 0, 1.000001);
    }

    [Theory]
    [InlineData("Brush")] [InlineData("MajorSave")] [InlineData("Crash")]
    public void PhysicalConsequenceFixturesAreUnchangedWithQualityEnabled(string name)
    {
        var scenario = name switch {"Brush" => Scenarios.C(), "MajorSave" => Scenarios.D(), _ => Scenarios.E()};
        var off = TacticalAudit.Execute(scenario, .01, true, side: true);
        var on = TacticalAudit.Execute(scenario, .01, true, side: true, quality: true);
        Assert.Equal(off.Result.Fingerprint, on.Result.Fingerprint);
        Assert.Equal(off.Result.Contacts, on.Result.Contacts); Assert.NotEmpty(on.Result.Contacts);
    }

    [Theory]
    [InlineData("SA-entry-parallel", "2BFDD3671C4947F552AD1D301CDEE24B12824AF5BEE39AD1A6B5520CB9077AD2")]
    [InlineData("SF-varied-Motoarena", "5492318C86C479D17F6112B59004B697A0CCC8A14E968AAF168DB0085A55FF60")]
    [InlineData("SG-F-close", "CD70ED3D351B4347CC01EB95C858234327217A9EFFC0B392B6E9E44D8EB4F320")]
    public void FlagOffPreservesFrozen90TrajectoryHash(string name, string expected)
    {
        var evidence = TacticalAudit.Execute(SideBySideScenarios.Cases().Single(s => s.Name == name), .01, true, side: true);
        Assert.Equal(expected, evidence.Result.Fingerprint); Assert.Equal(0, evidence.Result.Work.QualitySamples);
    }
}
