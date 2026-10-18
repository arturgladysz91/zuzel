using System.Numerics;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
[Collection("Box2D worlds")]
public sealed class SideBySideRacingTests
{
    [Theory]
    [InlineData(.005)] [InlineData(.01)] [InlineData(.02)]
    public void ActualParallelEntryHasCausalControlAndMeasurableMotionBeyondNaturalStartingOrder(double step)
    {
        var scenario = SideBySideScenarios.A();
        var off = TacticalAudit.Execute(scenario, step, true);
        var on = TacticalAudit.Execute(scenario, step, true, side: true);
        var first = on.Result.Decisions.First(d => d.Reason.StartsWith("Contest"));
        Assert.Equal(.1, first.ObservedAt); Assert.Equal(.34, first.ActualActivation!.Value, 10);
        Assert.True(Math.Abs(on.Samples[0].Riders[0].TotalReferenceMeters - on.Samples[0].Riders[1].TotalReferenceMeters) < .01);
        var byTime = off.Samples.ToDictionary(s => Math.Round(s.TimeSeconds, 9));
        double maximumDisplacement = 0, maximumProgress = 0;
        foreach (var s in on.Samples)
        {
            if (!byTime.TryGetValue(Math.Round(s.TimeSeconds, 9), out var baseline)) continue;
            if (s.TimeSeconds <= first.ActualActivation) Assert.Equal(baseline.Riders, s.Riders);
            else foreach (var (v, i) in s.Riders.Select((v, i) => (v, i))) {
                maximumDisplacement = Math.Max(maximumDisplacement, Vector2.Distance(v.Position, baseline.Riders[i].Position));
                maximumProgress = Math.Max(maximumProgress, Math.Abs(v.TotalReferenceMeters - baseline.Riders[i].TotalReferenceMeters));
            }
        }
        Assert.True(maximumDisplacement > .5); Assert.True(maximumProgress > .1);
        Assert.True(on.Result.Work.ControlChanges >= 1);
        Assert.InRange(on.MaximumGripRatio, 0, 1.000001);
        Assert.All(on.Result.Decisions.Where(d => d.Intent is RacingIntent.ContestInside or RacingIntent.ContestOutside),
            d => Assert.Equal(RacingIntent.ContestInside, d.Intent));
    }

    [Theory]
    [InlineData(20, 25, 20, 25)]
    [InlineData(45, 50, 20, 21)]
    [InlineData(34.9, 35.1, 20, 21)]
    [InlineData(35.1, 34.9, 20, 21)]
    public void InstantaneousProgressUsesCommonReferenceMetresOnBothSidesOfBoundary(double a, double b, double va, double vb)
    {
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var x = frame.At(a, 3).Road; var y = frame.At(b, 6).Road;
        var ra = frame.ReferenceSpeed(x, va); var rb = frame.ReferenceSpeed(y, vb);
        Assert.True(double.IsFinite(ra - rb));
        Assert.Equal(x.IsTurn ? va * 31 / 34 : va, ra, 10);
        Assert.Equal(y.IsTurn ? vb * 31 / 37 : vb, rb, 10);
        if (x.IsTurn && y.IsTurn) Assert.True(va < vb && ra > rb);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ActualDecisionSnapshotCanStraddleStraightAndArcWithoutZeroRadiusOrIntentChatter(bool innerFirst)
    {
        var e = TacticalAudit.Execute(SideBySideScenarios.D(innerFirst), .01, true, side: true);
        var observed = e.Samples.Single(s => Math.Abs(s.TimeSeconds - .1) < 1e-9);
        Assert.NotEqual(observed.Riders[0].Road.IsTurn, observed.Riders[1].Road.IsTurn);
        Assert.Contains(e.Result.Decisions, d => d.Reason is "ContestInside" or "ContestHoldAdvantage");
        Assert.True(e.Result.Work.SideObservations > 0); Assert.True(e.Result.Work.MaximumTacticalPairs <= 1);
        Assert.InRange(e.MaximumGripRatio, 0, 1.000001);
        foreach (var group in e.Result.Decisions.Where(d => d.Reason is "ContestInside" or "ContestOutside").GroupBy(d => d.RiderId))
            Assert.Single(group);
    }

    [Theory]
    [InlineData(.005)] [InlineData(.01)] [InlineData(.02)]
    public void VariedRealGatesHaveAnAutonomousFirstBendManeuverAndExactUnchangedPrefix(double step)
    {
        var scenario = SideBySideScenarios.F(); var original = Scenarios.G();
        for (var i = 0; i < 4; i++) {
            Assert.Equal(original.Riders[i] with {Profile = scenario.Riders[i].Profile}, scenario.Riders[i]);
            Assert.Equal(0, scenario.Riders[i].SpeedMetersPerSecond);
        }
        var off = TacticalAudit.Execute(scenario, step, true);
        var on = TacticalAudit.Execute(scenario, step, true, side: true);
        var comparison = SideBySideAudit.Compare(scenario, off, on);
        Assert.True(comparison.PrefixExact); Assert.Equal("ContestInside", comparison.FirstControlReason);
        Assert.True(comparison.FirstControlAt > 3 && comparison.FirstControlAt < 6);
        Assert.True(comparison.FirstPositionDifferenceAt > comparison.FirstControlAt);
        Assert.True(comparison.MaximumFirstBendPositionDifferenceMeters > .1);
        Assert.True(comparison.MaximumFirstBendReferenceDifferenceMeters > .1);
        Assert.Equal(6, on.Result.Work.MaximumTacticalPairs);
        Assert.Equal(714, on.Result.Work.TacticalPairObservations);
        var command = on.Result.Decisions.First(d => d.Reason == "ContestInside");
        var actual = on.Samples.Last(s => s.TimeSeconds <= command.ObservedAt + 1e-9);
        Assert.Equal(RacingContext.FirstBend, actual.Riders.Single(v => v.Id == command.RiderId).Context);
        Assert.True(Math.Abs(actual.Riders.Single(v => v.Id == command.RiderId).TotalReferenceMeters
            - actual.Riders.Single(v => v.Id == command.OpponentId).TotalReferenceMeters) < 2.1);
        Assert.Equal(command.RequestedActivation, command.ActualActivation!.Value, 10);
        Assert.InRange(on.MaximumGripRatio, 0, 1.000001);
    }

    [Fact]
    public void InnerAndOuterHoldChoicesUseActualReferencePaceAndDoNotForceYield()
    {
        var inner = TacticalAudit.Execute(SideBySideScenarios.BHoldInside(), .01, true, side: true);
        var outer = TacticalAudit.Execute(SideBySideScenarios.BOutside(), .01, true, side: true);
        var i = inner.Result.Decisions.First(d => d.RiderId == 1);
        var o = outer.Result.Decisions.First(d => d.RiderId == 2);
        Assert.Equal("ContestHoldAdvantage", i.Reason); Assert.Equal("ContestHoldAdvantage", o.Reason);
        Assert.Equal(.5f, i.TargetOffsetMeters); Assert.Equal(6.2f, o.TargetOffsetMeters);
        Assert.Equal(TrafficAction.HoldLine, o.Action);
        Assert.All(outer.Samples, s => Assert.Equal(1, s.Riders[1].Drive));
        Assert.True(outer.Result.Riders[1].TotalReferenceMeters > outer.Result.Riders[0].TotalReferenceMeters + 5);
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var start = inner.Samples[0].Riders;
        Assert.True(start[0].Speed < start[1].Speed && frame.ReferenceSpeed(start[0]) > frame.ReferenceSpeed(start[1]));
    }

    [Fact]
    public void NoGainAndExistingCloseParallelRidingKeepExactMotionWithoutUnneededCommands()
    {
        foreach (var scenario in new[] {SideBySideScenarios.BNoGain(), SideBySideScenarios.G()}) {
            var off = TacticalAudit.Execute(scenario, .01, true);
            var on = TacticalAudit.Execute(scenario, .01, true, side: true);
            Assert.Empty(on.Result.Decisions); Assert.Equal(off.Result.Fingerprint, on.Result.Fingerprint);
            Assert.Equal(0, on.Result.Work.ControlChanges);
        }
    }

    [Fact]
    public void OccupiedAlongsideCutHoldsOwnSpaceAfterReactionWithoutAutomaticThrottleLift()
    {
        var e = TacticalAudit.Execute(SideBySideScenarios.C(), .01, true, side: true);
        var hold = e.Result.Decisions.First(d => d.RiderId == 2);
        Assert.Equal("OccupiedAlongsideCut", hold.Reason); Assert.True(hold.SafetyReaction);
        Assert.Equal(1, hold.Drive); Assert.Equal(.34, hold.ActualActivation!.Value, 10);
        Assert.True(hold.TargetOffsetMeters > 4);
        Assert.DoesNotContain(e.Result.Decisions, d => d.RiderId == 2 && d.Intent == RacingIntent.ContestInside);
        Assert.True(e.Result.Work.MinimumGapMeters > 0);
    }

    [Fact]
    public void AReactionCanStillBeTooLateAndPhysicalContactHasOneConsequence()
    {
        var e = TacticalAudit.Execute(SideBySideScenarios.CRisk(), .01, true, side: true);
        Assert.Contains(e.Result.Decisions, d => d.Reason == "OccupiedAlongsideCut");
        Assert.Contains(e.Result.Contacts, c => c.Kind == "begin");
        Assert.Equal(e.Result.Work.Episodes, e.Result.Work.Consequences);
        Assert.InRange(e.Result.Work.MinimumGapMeters, -.005, .01);
    }

    [Fact]
    public void HoldWithNoActuatorChangeNeverRepartitionsPhysicsBeforeRealManeuver()
    {
        var scenario = SideBySideScenarios.F(); var off = TacticalAudit.Execute(scenario, .01, true);
        var on = TacticalAudit.Execute(scenario, .01, true, side: true);
        Assert.True(SideBySideAudit.Compare(scenario, off, on).PrefixExact);
        var holds = on.Result.Decisions.Where(d => d.Reason == "ContestHoldAdvantage" && d.ObservedAt == 3).ToArray();
        Assert.NotEmpty(holds);
        Assert.All(holds, d => Assert.True(d.ActualActivation >= d.RequestedActivation));
        Assert.Contains(holds, d => d.ActualActivation > d.RequestedActivation + 1e-6);
    }

    [Fact]
    public void SideBySideDecisionsRepeatAndIgnoreInitialInputOrder()
    {
        foreach (var scenario in new[] {SideBySideScenarios.C(), SideBySideScenarios.E(), SideBySideScenarios.F()}) {
            var a = TacticalAudit.Execute(scenario, .01, true, side: true);
            var b = TacticalAudit.Execute(scenario, .01, true, reverse: true, side: true);
            var repeat = TacticalAudit.Execute(scenario, .01, true, side: true);
            Assert.Equal(a.Result.Fingerprint, b.Result.Fingerprint); Assert.Equal(a.Result.Fingerprint, repeat.Result.Fingerprint);
            Assert.Equal(a.Result.Decisions, b.Result.Decisions); Assert.Equal(a.Result.Decisions, repeat.Result.Decisions);
            Assert.Equal(a.Result.Contacts, b.Result.Contacts);
            foreach (var group in a.Result.Decisions.Where(d => d.Reason is "ContestInside" or "ContestOutside").GroupBy(d => d.RiderId)) {
                var commands = group.ToArray();
                for (var i = 1; i < commands.Length; i++) Assert.True(commands[i].ObservedAt - commands[i - 1].ActualActivation >= .8);
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ExistingBrushMajorSaveAndCrashRemainPhysicalWithFeatureEnabled(int index)
    {
        var scenario = new[] {Scenarios.C(), Scenarios.D(), Scenarios.E()}[index];
        var off = TacticalAudit.Execute(scenario, .01, true);
        var on = TacticalAudit.Execute(scenario, .01, true, side: true);
        Assert.Equal(off.Result.Fingerprint, on.Result.Fingerprint);
        Assert.Equal(off.Result.Contacts, on.Result.Contacts);
        Assert.True(on.Result.Work.Consequences > 0);
        Assert.Equal(on.Result.Work.Episodes, on.Result.Work.Consequences);
        if (index == 2) Assert.Equal(on.Result.Contacts.First(c => c.Kind == "begin").TimeSeconds, on.Result.TimeSeconds);
    }

    [Fact]
    public void OpponentFutureCommandsCannotFreeSpaceOccupiedNow()
    {
        var scenario = SideBySideScenarios.C(); using var world = new WorldRunner(scenario.Riders);
        var snapshot = world.Snapshot with {TimeSeconds = .1};
        var surface = CoreSim.TrackState.CreateDefault(world.Frame.Track).Snapshot();
        DecisionEvent? Observe(WorldSnapshot s) {
            var work = new WorldWork(); return TrafficDecision.Observe(s, s.Riders[1], scenario.Riders[1],
                new() {EnableTacticalRacing = true, EnableSideBySideContests = true}, work,
                new() {HomeOffsetMeters = 4.05f}, TrafficDecision.ObservePairs(s, work), world.Frame, surface);
        }
        var original = Observe(snapshot);
        var future = snapshot with {Riders = new[] {snapshot.Riders[0] with {TargetOffsetMeters = 9, Drive = 0}, snapshot.Riders[1]}};
        Assert.Equal(original, Observe(future));
        var leaving = snapshot with {Riders = new[] {snapshot.Riders[0] with {Velocity = snapshot.Riders[0].Velocity + snapshot.Riders[0].Road.Outward * 10}, snapshot.Riders[1]}};
        Assert.Equal("OccupiedAlongsideCut", Observe(leaving)!.Reason);
    }

    [Fact]
    public void PendingAttackCancellationStillStartsANewReactionWithSideFeatureEnabled()
    {
        var a = TacticalScenarios.A(); var start = a.Riders[0].ReferenceMeters;
        var scenario = a with {DurationSeconds = .65, Riders = new[] {
            a.Riders[0] with {OffsetMeters = 5, TargetOffsetMeters = 5},
            a.Riders[1] with {ReferenceMeters = start + 8, OffsetMeters = 5, TargetOffsetMeters = 5}}};
        var e = TacticalAudit.Execute(scenario, .01, true, side: true);
        var cancelled = Assert.Single(e.Result.Decisions.Where(d => d.CancelledAt is not null));
        var safety = e.Result.Decisions.First(d => d.SafetyReaction);
        Assert.Null(cancelled.ActualActivation); Assert.Equal(cancelled.CancelledAt, safety.ObservedAt);
        Assert.Equal(RiderController.ReactionSeconds(scenario.Riders[0]), safety.RequestedActivation - safety.ObservedAt, 10);
        Assert.True(safety.RequestedActivation > cancelled.RequestedActivation);
    }
}
