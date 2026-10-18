using SharedWorldSpeedway;
using CoreSim;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
[Collection("Box2D worlds")]
public sealed class SharedWorldTacticalTests
{
    [Theory]
    [InlineData(.005)] [InlineData(.01)] [InlineData(.02)]
    public void FasterRiderActuallyPassesWholeBikeOnStraightAndHoldsLeadWithoutLeaderYield(double step)
    {
        var scenario = TacticalScenarios.A(); var on = TacticalAudit.Execute(scenario, step, true);
        var off = TacticalAudit.Execute(scenario, step, false);
        Assert.NotNull(on.ClearAt); Assert.NotNull(on.HeldAt);
        Assert.True(on.HeldAt - on.HeldSince >= 1); Assert.True(on.HeldDistanceMeters >= 10);
        Assert.True(on.LeaderKeptDrive); Assert.Empty(on.Result.Contacts);
        var clear = on.Samples.First(s => s.TimeSeconds >= on.ClearAt);
        Assert.False(clear.Riders[0].Road.IsTurn);
        Assert.True(on.FinalLeadMeters > 5);
        var attack = on.Result.Decisions.First(d => d.Action == TrafficAction.AttackInside);
        Assert.Equal(.1, attack.ObservedAt, 9); Assert.True(attack.ActualActivation >= attack.RequestedActivation);
        Assert.True(on.MovementAt > attack.ActualActivation);
        foreach (var s in on.Samples.Where(s => s.TimeSeconds <= attack.ActualActivation))
        {
            var baseline = off.Samples.SingleOrDefault(b => Math.Abs(b.TimeSeconds - s.TimeSeconds) < 1e-10);
            if (baseline is not null) Assert.Equal(baseline.Riders, s.Riders);
        }
        Assert.All(on.Result.Decisions.Where(d => d.RiderId == 1 && d.Action is TrafficAction.AttackInside or TrafficAction.AttackOutside),
            d => Assert.Equal(TrafficAction.AttackInside, d.Action));
        Assert.InRange(on.Result.Work.MaximumTacticalPairs, 0, 1);
        Assert.Contains(on.Result.Decisions, d => d.Reason == "ReturnCompleted");
        Assert.InRange(on.MaximumGripRatio, 0, 1.000001);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BothBendSidesProduceActualPassesWithExistingTyreBudget(bool outside)
    {
        var evidence = TacticalAudit.Execute(outside ? TacticalScenarios.C() : TacticalScenarios.B(), .01, true);
        var attack = Assert.Single(evidence.Result.Decisions.Where(d => d.RiderId == 1
            && d.Action is TrafficAction.AttackInside or TrafficAction.AttackOutside));
        Assert.Equal(outside ? TrafficAction.AttackOutside : TrafficAction.AttackInside, attack.Action);
        Assert.True(evidence.MovementAt > attack.ActualActivation);
        Assert.NotNull(evidence.HeldAt); Assert.Empty(evidence.Result.Contacts);
        Assert.True(evidence.Result.Work.MinimumTrackClearanceMeters > 0);
        Assert.InRange(evidence.MaximumGripRatio, 0, 1.000001);
    }

    [Fact]
    public void DefenseAloneChangesAnEarlyChoiceAndActualMotionWithoutGuaranteeingAWin()
    {
        var low = TacticalScenarios.D(false); var high = TacticalScenarios.D(true);
        Assert.Equal(low.Riders[0], high.Riders[0]);
        var a = low.Riders[1]; var b = high.Riders[1];
        Assert.Equal(a with {Profile = b.Profile}, b);
        Assert.Equal(a.Profile.Skills, b.Profile.Skills);
        Assert.Equal(a.Profile.Gameplay.Physical, b.Profile.Gameplay.Physical);
        Assert.Equal(a.Profile.Gameplay.InteractionStyle, b.Profile.Gameplay.InteractionStyle);
        var offLow = TacticalAudit.Execute(low, .01, false); var offHigh = TacticalAudit.Execute(high, .01, false);
        Assert.Equal(offLow.Result.Fingerprint, offHigh.Result.Fingerprint);
        var l = TacticalAudit.Execute(low, .01, true); var h = TacticalAudit.Execute(high, .01, true);
        Assert.DoesNotContain(l.Result.Decisions, d => d.Intent == RacingIntent.DefendLine);
        var defense = h.Result.Decisions.First(d => d.Reason == "CoverInsideEarly");
        Assert.Equal(.1, defense.ObservedAt); Assert.Equal(.34, defense.ActualActivation!.Value, 10);
        var lt = l.Samples.First(s => s.TimeSeconds >= .7).Riders[1];
        var ht = h.Samples.First(s => s.TimeSeconds >= .7).Riders[1];
        Assert.True(lt.Road.OffsetMeters - ht.Road.OffsetMeters > .05);
        Assert.Contains(h.Result.Decisions, d => d.Reason == "RearRiderOccupiesCrossing");
        Assert.NotNull(h.HeldAt); // better Defense is not a magic victory
    }

    [Fact]
    public void BothBlockedCorridorsCauseLiftAndNoInventedPass()
    {
        var e = TacticalAudit.Execute(TacticalScenarios.E(), .01, true);
        Assert.Contains(e.Result.Decisions, d => d.RiderId == 1 && d.Reason == "NoPassingCorridor" && d.Drive == 0);
        Assert.DoesNotContain(e.Result.Decisions, d => d.RiderId == 1 && d.Intent is RacingIntent.AttackInside or RacingIntent.AttackOutside);
        Assert.Null(e.ClearAt); Assert.Null(e.MovementAt); Assert.True(e.FinalLeadMeters < 0);
        Assert.Equal(6, e.Result.Work.MaximumTacticalPairs);
        Assert.True(e.Result.Work.MinimumGapMeters > .5);
    }

    [Fact]
    public void NewImmediateThreatCancelsPendingTacticAndStartsANewReactionDelay()
    {
        var a = TacticalScenarios.A(); var start = a.Riders[0].ReferenceMeters;
        var scenario = a with {DurationSeconds = .65, Riders = new[] {
            a.Riders[0] with {OffsetMeters = 5, TargetOffsetMeters = 5},
            a.Riders[1] with {ReferenceMeters = start + 8, OffsetMeters = 5, TargetOffsetMeters = 5}}};
        var e = TacticalAudit.Execute(scenario, .01, true);
        var cancelled = Assert.Single(e.Result.Decisions.Where(d => d.CancelledAt is not null));
        Assert.Equal(RacingIntent.AttackInside, cancelled.Intent);
        Assert.Null(cancelled.ActualActivation);
        var safety = e.Result.Decisions.First(d => d.SafetyReaction);
        Assert.Equal(cancelled.CancelledAt, safety.ObservedAt);
        Assert.True(safety.RequestedActivation > cancelled.RequestedActivation);
        Assert.Equal(RiderController.ReactionSeconds(scenario.Riders[0]), safety.RequestedActivation - safety.ObservedAt, 10);
        Assert.Equal(safety.RequestedActivation, safety.ActualActivation!.Value, 10);
        Assert.All(e.Samples.Where(s => s.TimeSeconds <= safety.ActualActivation), s => {
            Assert.Equal(5, s.Riders[0].TargetOffsetMeters); Assert.Equal(1, s.Riders[0].Drive);
        });
        Assert.Equal(1, e.Result.Work.CancelledCommands);
    }

    [Fact]
    public void OpponentFutureTargetsAreInvisibleAndStalledAttackCanBeAbandoned()
    {
        var scenario = TacticalScenarios.A(); using var world = new WorldRunner(scenario.Riders);
        var snapshot = world.Snapshot with {TimeSeconds = .1};
        var surface = CoreSim.TrackState.CreateDefault(world.Frame.Track).Snapshot();
        DecisionEvent? Observe(WorldSnapshot s, TacticalMemory memory) {
            var work = new WorldWork(); var pairs = TrafficDecision.ObservePairs(s, work);
            return TrafficDecision.Observe(s, s.Riders[0], scenario.Riders[0], new(), work, memory, pairs, world.Frame, surface);
        }
        var changed = snapshot with {Riders = new[] {snapshot.Riders[0], snapshot.Riders[1] with {TargetOffsetMeters = 9, Drive = 0}}};
        Assert.Equal(Observe(snapshot, new() {HomeOffsetMeters = 4}), Observe(changed, new() {HomeOffsetMeters = 4}));
        var abort = Observe(snapshot with {TimeSeconds = 5}, new() {
            HomeOffsetMeters = 4, Intent = RacingIntent.AttackInside, OpponentId = 2, ActivatedAt = 0});
        Assert.NotNull(abort); Assert.Equal("AttackNoLongerFeasible", abort.Reason); Assert.Equal(0, abort.Drive);
        Assert.Equal(RacingIntent.None, abort.Intent);
    }

    [Fact]
    public void AttackQualityAndRiskChangeChoicesWithoutAddingPhysicalForce()
    {
        var scenario = TacticalScenarios.A(); var original = scenario.Riders[0];
        RiderSpec Profile(int attack, float combativeness) {
            var old = original.Profile.Gameplay; var a = old.Abilities;
            var gameplay = new RiderGameplayProfile(new(a.Reaction, a.Start, a.Technique, a.TrackReading,
                attack, a.Defense, a.PairRiding, a.Strength), old.Physical,
                new(combativeness, old.InteractionStyle.PreferredLine, old.InteractionStyle.SetupIndependence));
            return original with {Profile = RiderProfile.CreateCanonical(original.Profile.Id, "R1", gameplay,
                original.Profile.Skills, original.Profile.Style)};
        }
        using var world = new WorldRunner(scenario.Riders); var snapshot = world.Snapshot with {TimeSeconds = .1};
        var surface = TrackState.CreateDefault(world.Frame.Track).Snapshot();
        DecisionEvent? Decide(RiderSpec spec) {
            var work = new WorldWork(); return TrafficDecision.Observe(snapshot, snapshot.Riders[0], spec, new(), work,
                new() {HomeOffsetMeters = 4}, TrafficDecision.ObservePairs(snapshot, work), world.Frame, surface);
        }
        var weak = Profile(10, .35f); var bold = Profile(85, .95f);
        Assert.Null(Decide(weak));
        var normal = Decide(original); var risk = Decide(bold);
        Assert.NotNull(normal); Assert.NotNull(risk);
        Assert.Equal(TrafficAction.AttackInside, normal.Action);
        Assert.True(risk.TargetOffsetMeters > normal.TargetOffsetMeters);
        var force = RiderController.Forces(snapshot.Riders[0], original, world.Frame, surface, new(), .1, 142, 50);
        Assert.Equal(force, RiderController.Forces(snapshot.Riders[0], weak, world.Frame, surface, new(), .1, 142, 50));
        Assert.Equal(force, RiderController.Forces(snapshot.Riders[0], bold, world.Frame, surface, new(), .1, 142, 50));
    }

    [Fact]
    public void DriveHysteresisPreventsImmediateLiftResumeLoopsDuringRealDuels()
    {
        foreach (var scenario in new[] {TacticalScenarios.A(), TacticalScenarios.D(true), TacticalScenarios.E()})
        {
            var result = TacticalAudit.Execute(scenario, .01, true).Result;
            foreach (var rider in scenario.Riders)
            {
                float drive = 1; var changes = new List<double>();
                foreach (var d in result.Decisions.Where(d => d.RiderId == rider.Profile.Id && d.ActualActivation is not null))
                    if (d.Drive != drive) {drive = d.Drive; changes.Add(d.ActualActivation!.Value);}
                Assert.True(changes.Count <= 4);
                for (var i = 1; i < changes.Count; i++) Assert.True(changes[i] - changes[i - 1] >= .8 - 1e-9);
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void SameSnapshotDecisionsRepeatAndIgnoreInputOrdering(int index)
    {
        var scenario = TacticalScenarios.Cases()[index];
        var a = TacticalAudit.Execute(scenario, .01, true);
        var b = TacticalAudit.Execute(scenario, .01, true);
        var reversed = TacticalAudit.Execute(scenario, .01, true, reverse: true);
        Assert.Equal(a.Result.Fingerprint, b.Result.Fingerprint); Assert.Equal(a.Result.Fingerprint, reversed.Result.Fingerprint);
        Assert.Equal(a.Result.Decisions, reversed.Result.Decisions); Assert.Equal(a.Result.Contacts, reversed.Result.Contacts);
        Assert.Equal(a.Result.Decisions, b.Result.Decisions);
        Assert.All(a.Result.Decisions, d => {
            Assert.Equal(d.ObservedAt, d.DecisionAt); Assert.Equal(d.ObservedAt / .1, Math.Round(d.ObservedAt / .1), 8);
            if (d.ActualActivation is { } t) Assert.Equal(d.RequestedActivation, t, 10);
            Assert.InRange(d.TargetOffsetMeters, .5, 9.5);
        });
        Assert.True(a.Result.Work.MaximumTacticalPairs <= 6);
        Assert.Equal((long)Math.Round((scenario.DurationSeconds - .1) / .1) * scenario.Riders.Length * (scenario.Riders.Length - 1) / 2,
            a.Result.Work.TacticalPairObservations);
    }

    [Fact]
    public void ParallelCloseRidingKeepsFrozenFingerprintAndNoNeedlessControls()
    {
        var scenario = TacticalScenarios.G(); var on = TacticalAudit.Execute(scenario, .01, true);
        var off = TacticalAudit.Execute(scenario, .01, false);
        Assert.Equal(off.Result.Fingerprint, on.Result.Fingerprint);
        Assert.Equal("CD70ED3D351B4347CC01EB95C858234327217A9EFFC0B392B6E9E44D8EB4F320", on.Result.Fingerprint);
        Assert.Empty(on.Result.Decisions); Assert.Equal(0, on.Result.Work.ControlChanges);
        Assert.Null(on.HeldAt); Assert.Empty(on.Result.Contacts);
    }

    [Fact]
    public void ActualMotoarenaGatesAreObservedTogetherWithoutInventedPassingFromTiedStart()
    {
        var scenario = TacticalScenarios.F(); Assert.Equal(Scenarios.G().Riders, scenario.Riders);
        var e = TacticalAudit.Execute(scenario, .01, true);
        Assert.All(e.Samples, s => Assert.Equal(4, s.Riders.Count));
        foreach (var id in new[] {1, 2, 3, 4}) Assert.Contains(e.Samples, s => s.Riders.Single(r => r.Id == id).Context == RacingContext.FirstBend);
        Assert.Equal(6, e.Result.Work.MaximumTacticalPairs);
        Assert.Equal(714, e.Result.Work.TacticalPairObservations);
        Assert.Equal(476, e.Result.Work.TacticalObservations);
        Assert.Null(e.ClearAt); Assert.Null(e.HeldAt);
        Assert.Equal("BB7DF2B12B88EDA37A58E542B2FF495582276B10F455F5467ECE437A097C49B6", e.Result.Fingerprint);
    }

    [Fact]
    public void FirstCrashEndsDiagnosticAtContactInsteadOfSimulatingFallenBikes()
    {
        var e = TacticalAudit.Execute(TacticalScenarios.A(), .01, false);
        var contact = Assert.Single(e.Result.Contacts.Where(c => c.Kind == "begin"));
        Assert.Equal(contact.TimeSeconds, e.Result.TimeSeconds);
        Assert.Contains(e.Result.Riders, r => r.Activity == RiderActivity.Crashed);
        Assert.Equal(e.Result.TimeSeconds, e.Samples[^1].TimeSeconds);
    }
}
