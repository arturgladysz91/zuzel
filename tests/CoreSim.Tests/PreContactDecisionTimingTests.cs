using CoreSim;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using PreContactDecisions;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class PreContactDecisionTimingTests
{
    [Theory]
    [InlineData("A-yield")]
    [InlineData("B-no-room")]
    [InlineData("C-aggressive")]
    [InlineData("D-close-parallel")]
    public void ExistingResponsesAndBoundaryDelayHaveExplicitDistinctOutcomesAndStableOrder(string name)
    {
        var input = DecisionTimingProbe.Scenarios().Single(s => s.Name == name);
        var main = DecisionTimingProbe.Run(input, false); var waiting = DecisionTimingProbe.Run(input, true);
        foreach (var policy in new[] { false, true })
        {
            var forward = policy ? waiting : main; var reverse = DecisionTimingProbe.Run(input, policy, true);
            Assert.Equal(forward.Step.Changes, reverse.Step.Changes); Assert.Equal(forward.Step.Motions, reverse.Step.Motions);
            Assert.Equal(forward.Step.Diagnostics, reverse.Step.Diagnostics); Assert.Equal(forward.Step.Events, reverse.Step.Events);
            Assert.Equal(forward.Cue, reverse.Cue);
            Assert.Equal(forward.Step.Interaction?.Episodes.SelectMany(e => e.SelectedResponses).ToArray() ?? Array.Empty<InteractionAlternative>(),
                reverse.Step.Interaction?.Episodes.SelectMany(e => e.SelectedResponses).ToArray() ?? Array.Empty<InteractionAlternative>());
        }
        var safe = DecisionTimingProbe.Audit(main.Step);
        Assert.Empty(safe.FrameCoverageGaps); Assert.Equal(0, safe.Work.UnresolvedIntervals);
        Assert.DoesNotContain(safe.Intervals, r => r.HasConflict);
        Assert.All(main.Step.Motions, m =>
        {
            var initial = input.Riders.Single(r => r.Id == m.RiderId);
            Assert.Equal(initial.Speed, m.Initial.SpeedMetersPerSecond); Assert.Equal(initial.Lateral, m.Initial.LateralPosition);
        });
        if (name == "D-close-parallel")
        {
            Assert.False(waiting.Cue!.PossibleConflict); Assert.False(main.ResponseApplied);
            Assert.Equal(main.Step.Changes, waiting.Step.Changes); Assert.Equal(main.Step.Motions, waiting.Step.Motions);
            Assert.Empty(main.Step.Interaction!.Episodes); Assert.Equal(0, main.Step.Interaction.Work.ResponsePasses);
            Assert.DoesNotContain(waiting.DirectAudit!.Intervals, r => r.HasConflict);
        }
        else
        {
            Assert.True(waiting.Cue!.PossibleConflict); Assert.True(main.ResponseApplied); Assert.False(waiting.ResponseApplied);
            var report = waiting.DirectAudit!;
            var collisionWitness = report.Intervals.First(r => r.MinimumSeparationMeters < -GeometryNumerics.ContactDistanceMeters);
            var possible = report.Intervals.First(r => r.HasConflict || !r.NumericallyResolved);
            var nextDecision = waiting.Step.Motions.Min(m => m.TotalTimeSeconds);
            Assert.True(collisionWitness.MinimumSeparationCommonTimeSeconds < nextDecision);
            if (name is "A-yield" or "B-no-room")
                Assert.True(possible.IntervalStartSeconds > DecisionTimingProbe.ReactionDelaySeconds);
            else Assert.True(collisionWitness.MinimumSeparationCommonTimeSeconds < DecisionTimingProbe.ReactionDelaySeconds);
            // The candidate is NOT committed and no post-contact state/effect is accepted.
            Assert.All(waiting.Step.Snapshot.Riders, r => Assert.Equal(0, r.ElapsedTimeSeconds));
            Assert.DoesNotContain(waiting.Step.Events, e => e.Type != SimulationEventType.SegmentResolved);
        }
        if (name == "A-yield")
        {
            var choice = main.Step.Interaction!.Episodes.SelectMany(e => e.SelectedResponses).Single(a => a.RiderId == 2);
            Assert.Equal(InteractionResponse.YieldOutward, choice.Response); Assert.NotNull(choice.PhysicalTarget);
            Assert.Equal(input.Riders[1].Lateral, main.Step.Changes.Single(c => c.RiderId == 2).LateralPosition);
            Assert.True(waiting.Step.Changes.Single(c => c.RiderId == 2).LateralPosition < input.Riders[1].Lateral);
        }
        if (name == "B-no-room")
            Assert.Equal(4, main.Step.Changes.Single(c => c.RiderId == 2).LateralPosition);
        if (name == "C-aggressive")
        {
            Assert.All(main.Step.Interaction!.Episodes.SelectMany(e => e.SelectedResponses), a =>
                Assert.Equal(CoreSim.Decisions.RiderDriveControl.LiftThrottle, a.DriveControl));
            Assert.True(main.Step.Changes[0].Speed < waiting.Step.Changes[0].Speed);
            Assert.Equal(21, main.Step.Motions[0].Initial.SpeedMetersPerSecond); // lift does not assign entry speed
        }
    }

    [Fact]
    public void CheapCueDoesNotReadOpponentFutureAnchorsOrTreatAsynchronousPositionsAsOneObservation()
    {
        var input = DecisionTimingProbe.Scenarios().Single(s => s.Name == "D-close-parallel");
        var context = DecisionTimingProbe.Prepare(input);
        var asynchronous = new SimulationSnapshot(context.Snapshot.Step, context.Snapshot.Track, context.Snapshot.TrackState,
            context.Snapshot.Riders.Select(r => r.RiderId == 1 ? r with { ElapsedTimeSeconds = .1f } : r));
        Assert.Throws<ArgumentException>(() => DecisionTimingProbe.Observe(asynchronous, context.Intents));
        Assert.Null(DecisionTimingProbe.Observe(context.Snapshot, context.Intents).BoundingDiscEntrySeconds);
        var changedFuture = context.Intents.Select(i => i with { Decision = i.Decision with
            { Trajectory = new CoreSim.Decisions.TrajectoryIntent(4, 0, 4) } }).ToArray();
        Assert.Equal(DecisionTimingProbe.Observe(context.Snapshot, context.Intents),
            DecisionTimingProbe.Observe(context.Snapshot, changedFuture));
    }

    [Fact]
    public void CanonicalTacticalInputsChangeMarginsWithoutCreatingPhysicalExecutionCapability()
    {
        var input = DecisionTimingProbe.Scenarios().Single(s => s.Name == "A-yield");
        var altered = input with { Riders = input.Riders.Select(r => r with
            { Attack = 20, Defense = 99, Combativeness = 1, Technique = 20, Condition = .4f }).ToArray() };
        var a = DecisionTimingProbe.Run(input, true); var b = DecisionTimingProbe.Run(altered, true);
        Assert.Equal(a.Step.Changes, b.Step.Changes); Assert.Equal(a.Step.Motions, b.Step.Motions);
        Assert.True(InteractionGeometryModel.ExecutionMarginMeters(b.Step.Snapshot.Rider(1), new())
            > InteractionGeometryModel.ExecutionMarginMeters(a.Step.Snapshot.Rider(1), new()));
    }

    [Fact]
    public void ExistingMetreYieldCanWidenThroughTheUnchangedMovementBudgetWhenHoldingIsInsufficient()
    {
        var input = CoreSim.Analysis.ContestedSpaceResponseEvidence.Scenarios().Single(s => s.Name == "D-inside-overlap");
        var result = CoreSim.Analysis.ContestedSpaceResponseEvidence.Resolve(input);
        var motion = result.Motions.Single(m => m.RiderId == 2);
        Assert.InRange(motion.Final.PhysicalOffsetMeters - motion.Initial.PhysicalOffsetMeters, .01f, .25f);
        for (var i = 1; i < motion.Nodes.Count; i++)
        {
            var a = motion.Nodes[i - 1]; var b = motion.Nodes[i];
            var limit = LateralMovementModel.CalculateMaxLateralDistanceMeters(b.LocalTimeSeconds - a.LocalTimeSeconds,
                result.Snapshot.Segment.Type, result.Snapshot.Track.Geometry,
                result.Snapshot.TrackState.SampleSurface(motion.SegmentIndex, a.LateralPosition), result.Snapshot.Rider(2).Profile.Skills);
            Assert.True(Math.Abs(b.PhysicalOffsetMeters - a.PhysicalOffsetMeters) <= limit + .00002);
        }
    }
}
