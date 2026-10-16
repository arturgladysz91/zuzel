using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;
using MidSegmentControl;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class MidSegmentControlTests
{
    private static ControlCases.Context Schedule(ControlCases.Context context, double at = .2,
        bool? hold = null, float? targetMeters = null, bool lift = true, bool enabled = true, int? onlyRiderId = null)
        => context with { Intents = context.Intents.Select(i => onlyRiderId.HasValue && i.RiderId != onlyRiderId ? i : i with { Decision = i.Decision with
            { ScheduledControl = new(i.RiderId, at, lift ? RiderDriveControl.LiftThrottle : new(1), hold,
                targetMeters.HasValue ? new InteractionLateralTarget(targetMeters.Value) : null, enabled) } }).ToArray() };
    private static ExecutedSegmentPath Path(ResolvedSimulationStep step) => Assert.IsType<ExecutedSegmentPath>(step.Diagnostics[0].ExecutedPath);
    private static ScheduledControlActivation Activation(ResolvedSimulationStep step) => Assert.IsType<ScheduledControlActivation>(step.Diagnostics[0].ControlActivation);
    private static void Prefix(ExecutedSegmentPath before, ExecutedSegmentPath after, int index)
    {
        Assert.Equal(before.Nodes.Take(index + 1), after.Nodes.Take(index + 1));
        Assert.Equal(before.Steps.Take(index), after.Steps.Take(index));
    }

    [Theory]
    [InlineData("straight", "2DE9CF4AC4A1776B9C19A6663586AD6FAC81AC8DD4417B136EF40FDAEBAF5BA8")]
    [InlineData("corner", "50CFA3AC2576408858A13891635FA2F7BBA5DCE52B246B941F916180B601FFDF")]
    [InlineData("moving", "AE171D23D42B3DB1DC5D93A1EB82165E64382D5C5E3AF158DFEBAA1949A4B7BE")]
    [InlineData("fixed-straight", "F5C45E6E7415EF5F977B59B4DB6E80EDB2838DF178E0D68EABCACBD50FBABE91")]
    [InlineData("fixed-corner", "4A76CBD807A2BF040FBF11DE5E23F07039E33A3027C1636FBE8B4D5E73C4D5D0")]
    public void EmptyAndDisabledCommandsPreserveMainPhysicalFingerprints(string name, string mainFingerprint)
    {
        // Captured from unmodified main DLL before implementing the command.
        var context = ControlCases.Prepare(name);
        Assert.Equal(mainFingerprint, ControlCases.Fingerprint(ControlCases.Run(context)));
        Assert.Equal(mainFingerprint, ControlCases.Fingerprint(ControlCases.Run(Schedule(context, enabled: false))));
        if (!name.StartsWith("fixed"))
            Assert.Equal(mainFingerprint, ControlCases.Fingerprint(ControlCases.Run(Schedule(context, at: 1000))));
    }

    [Fact]
    public void LiftStartsAfterTheDeadlineWithoutChangingAcceptedHistoryAndCommitsOneWearBudget()
    {
        var context = ControlCases.Prepare("straight"); var before = ControlCases.Run(context);
        var scheduled = Schedule(context); var step = ControlCases.Run(scheduled); var activation = Activation(step);
        var path = Path(step); Prefix(Path(before), path, activation.AcceptedStepIndex);
        Assert.True(activation.ActualElapsedTimeSeconds >= .2);
        Assert.True(path.Nodes[activation.AcceptedStepIndex - 1].ElapsedTimeSeconds < .2);
        var index = activation.AcceptedStepIndex;
        Assert.True(path.Nodes[index + 1].SpeedMetersPerSecond < path.Nodes[index].SpeedMetersPerSecond);
        Assert.True(step.Changes[0].Speed < before.Changes[0].Speed);
        var first = path.Steps[index]; var rider = context.Snapshot.Riders.Single();
        Assert.Equal(LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(path.Nodes[index].SpeedMetersPerSecond,
            first.DistanceMeters, 0, rider.ActiveSetup), path.Nodes[index + 1].SpeedMetersPerSecond);
        Assert.Equal(path.DistanceMeters, step.Motions[0].TotalDistanceMeters);
        var state = new TrackState(context.Snapshot.TrackState.SegmentCount, 5, context.Snapshot.TrackState.GetSurface);
        var mutable = rider.ToMutableCopy();
        context.Engine.Commit(step, new[] { mutable }, state, new SimLog(false));
        Assert.Equal(.004f * 1.4f, Enumerable.Range(0, 5).Sum(l => state.GetSurface(context.Snapshot.Step.SegmentIndex, l).Ruts), 6);
        Assert.Equal(0, context.Snapshot.TrackState.GetSurface(context.Snapshot.Step.SegmentIndex, 2).Ruts);
        Assert.Equal(step.Changes[0].Position.DistanceMeters, mutable.Position.DistanceMeters);
        var lean = SimulationEngine.ResolveSoloProjection(scheduled.Snapshot, rider, scheduled.Intents[0].Decision, ControlCases.Options);
        Assert.Equal(step.Changes[0], lean.Change);
        var leanState = new TrackState(state.SegmentCount, 5, context.Snapshot.TrackState.GetSurface);
        SimulationEngine.CommitProjectionWear(lean, leanState);
        for (var segment = 0; segment < state.SegmentCount; segment++)
        for (var lane = 0; lane < 5; lane++) Assert.Equal(state.GetSurface(segment, lane), leanState.GetSurface(segment, lane));
    }

    [Fact]
    public void CornerTargetIsGradualBoundedAndNeedNotBeReached()
    {
        var context = ControlCases.Prepare("corner"); var before = ControlCases.Run(context);
        var step = ControlCases.Run(Schedule(context, hold: false, targetMeters: 12, lift: false));
        var path = Path(step); var activation = Activation(step); Prefix(Path(before), path, activation.AcceptedStepIndex);
        Assert.True(path.Nodes[^1].PhysicalOffsetMeters > path.Nodes[activation.AcceptedStepIndex].PhysicalOffsetMeters);
        Assert.True(path.Nodes[^1].PhysicalOffsetMeters < 12);
        for (var i = activation.AcceptedStepIndex; i < path.Steps.Count; i++)
        {
            var a = path.Nodes[i]; var b = path.Nodes[i + 1];
            var budget = LateralMovementModel.CalculateMaxLateralDistanceMeters(path.Steps[i].TimeSeconds,
                context.Snapshot.Segment.Type, context.Snapshot.Track.Geometry, a.Surface, context.Snapshot.Riders[0].Profile.Skills);
            Assert.True(b.PhysicalOffsetMeters - a.PhysicalOffsetMeters <= budget + .00002);
            Assert.Equal(SegmentPhysics.MaxSafeTurnSpeed(b.LateralPosition, context.Snapshot.Track.Geometry,
                b.Surface, context.Snapshot.Riders[0].Profile.Skills, context.Snapshot.Riders[0].ActiveSetup), b.LocalSafeSpeedMetersPerSecond);
        }
        Assert.Equal(before.Changes[0].Outcome, step.Changes[0].Outcome);
    }

    [Theory]
    [InlineData("start")] [InlineData("node")] [InlineData("between")] [InlineData("end")] [InlineData("after")]
    public void DeadlinesUseAcceptedPhysicalTimesAndNeverCarryAnExpiredCommand(string boundary)
    {
        var context = ControlCases.Prepare("straight"); var original = Path(ControlCases.Run(context));
        var deadline = boundary switch { "start" => 0d, "node" => original.Nodes[4].ElapsedTimeSeconds,
            "between" => ((double)original.Nodes[4].ElapsedTimeSeconds + original.Nodes[5].ElapsedTimeSeconds) / 2,
            "end" => original.TravelTimeSeconds, _ => original.TravelTimeSeconds + 1d };
        var step = ControlCases.Run(Schedule(context, at: deadline));
        if (boundary == "after") { Assert.Null(step.Diagnostics[0].ControlActivation); Assert.Equal(original, Path(step)); return; }
        var activation = Activation(step); Assert.True(activation.ActualElapsedTimeSeconds >= deadline);
        var expected = boundary == "start" ? 0 : boundary == "node" ? 4 : boundary == "between" ? 5 : original.Steps.Count;
        Assert.Equal(expected, activation.AcceptedStepIndex);
        if (expected > 0) Prefix(original, Path(step), expected);
        if (boundary == "end") Assert.Equal(original, Path(step));
    }

    [Fact]
    public void ProductionAndForecastUseOneScheduleAndExpireItAtTheNextSegment()
    {
        var context = ControlCases.Prepare("corner"); var rider = context.Snapshot.Riders[0];
        context = context with { Snapshot = new(context.Snapshot.Step, context.Snapshot.Track, context.Snapshot.TrackState,
            new[] { rider with { ElapsedTimeSeconds = 100 } }) };
        context = Schedule(context, at: 100.2, hold: false, targetMeters: 9, lift: false);
        var step = ControlCases.Run(context); var decision = context.Intents[0].Decision;
        var control = new InteractionProjectionControl(null, true) { ScheduledControl = decision.ScheduledControl };
        var evaluator = TrajectoryEvaluator.ForScheduledControl(new RiderDecisionContext(context.Snapshot, context.Snapshot.Riders[0]), control);
        var forecast = evaluator.Evaluate(new(1, 1, 1), retainResolvedMotions: true);
        Assert.Equal(step.Motions[0], forecast.ResolvedMotions[0]); Assert.Equal(Activation(step), forecast.ControlActivation);
        Assert.True(forecast.ControlActivation!.ActualElapsedTimeSeconds >= 100.2);
        var state = new TrackState(context.Snapshot.TrackState.SegmentCount, 5, context.Snapshot.TrackState.GetSurface);
        var mutable = context.Snapshot.Riders[0].ToMutableCopy();
        context.Engine.Commit(step, new[] { mutable }, state, new SimLog(false));
        var next = context.Engine.CaptureSnapshot(context.Snapshot.Track, state, new[] { mutable },
            context.Snapshot.Step with { SegmentIndex = 3, StepNumber = context.Snapshot.Step.StepNumber + 1 });
        var following = context.Engine.ResolveProduction(next, new[] { new RiderIntent(mutable.RiderId, new(1)) }, ControlCases.Options, legacyContacts: false);
        Assert.Null(following.Diagnostics[0].ControlActivation); Assert.Equal(following.Motions[0], forecast.ResolvedMotions[1]);
        Assert.Equal(forecast, evaluator.Evaluate(new(1, 1, 1), retainResolvedMotions: true));
        var noPending = context with { Intents = new[] { context.Intents[0] with { Decision = decision with { ScheduledControl = null } } } };
        var laterThanThisSegment = 100 + Path(ControlCases.Run(noPending)).TravelTimeSeconds + .1;
        var expires = control with { ScheduledControl = new(rider.RiderId, laterThanThisSegment, RiderDriveControl.LiftThrottle) };
        var expiredForecast = TrajectoryEvaluator.ForScheduledControl(new RiderDecisionContext(context.Snapshot, context.Snapshot.Riders[0]), expires)
            .Evaluate(new(1, 1, 1), retainResolvedMotions: true);
        Assert.Null(expiredForecast.ControlActivation);
    }

    [Fact]
    public void ACommandAfterTheGeometryWitnessCannotEraseAnEarlierContactAndOrderIsStable()
    {
        ResolvedSimulationStep Run(bool reverse, bool schedule)
        {
            var context = ControlCases.Pair(late: true, reverse: reverse);
            return ControlCases.Run(schedule ? Schedule(context, hold: true, onlyRiderId: 2) : context);
        }
        var original = Run(false, false); var changed = Run(false, true);
        Assert.Equal(ControlCases.Fingerprint(changed), ControlCases.Fingerprint(Run(true, true)));
        var diagnostic = changed.Diagnostics.Single(d => d.RiderId == 2);
        var activation = Assert.IsType<ScheduledControlActivation>(diagnostic.ControlActivation);
        Prefix(original.Diagnostics.Single(d => d.RiderId == 2).ExecutedPath!, diagnostic.ExecutedPath!, activation.AcceptedStepIndex);
        Assert.Equal(original.Motions[0], changed.Motions[0]);
        Assert.Contains(ControlCases.Audit(changed).Intervals, r => r.MinimumSeparationMeters < -GeometryNumerics.ContactDistanceMeters
            && r.MinimumSeparationCommonTimeSeconds < .2);
        Assert.All(changed.Snapshot.Riders, r => Assert.Equal(0, r.ElapsedTimeSeconds)); // no colliding tail committed
    }

    [Fact]
    public void FullDriveCommandCannotEraseIncomingContactRecovery()
    {
        var context = ControlCases.Prepare("straight"); var rider = context.Snapshot.Riders[0] with
        { ContactRecovery = new(1, 0, 1, PhysicalContactSeverity.Disturbed, .5) };
        context = context with { Snapshot = new(context.Snapshot.Step, context.Snapshot.Track, context.Snapshot.TrackState, new[] { rider }) };
        ResolvedSimulationStep Run(ControlCases.Context c) => c.Engine.ResolveProduction(c.Snapshot, c.Intents,
            ControlCases.Options with { EnablePhysicalContactConsequences = true }, legacyContacts: false);
        var original = Run(context); var changed = Run(Schedule(context, lift: false));
        Assert.Equal(original.Changes, changed.Changes); Assert.Equal(Path(original), Path(changed)); Assert.NotNull(Activation(changed));
        Assert.Equal(.5, rider.ContactRecovery!.ControlLoss01);
    }

    [Fact]
    public void PairAExecutesTheKnown56BCommandBeforeTheOriginalContactWithoutDemandingAvoidance()
    {
        var context = ControlCases.Pair(); var free = ControlCases.Run(context);
        var immediate = context.Engine.Resolve(context.Snapshot, context.Intents, ControlCases.Options);
        var response = immediate.Interaction!.Episodes.SelectMany(e => e.SelectedResponses).Single(a => a.RiderId == 2);
        Assert.Equal(InteractionResponse.YieldOutward, response.Response);
        var delayed = ControlCases.Run(context with { Intents = context.Intents.Select(i => i.RiderId != 2 ? i : i with
            { Decision = i.Decision with { ScheduledControl = new(2, .2, response.DriveControl ?? new(1),
                response.HoldLateralPosition, response.PhysicalTarget) } }).ToArray() });
        var activation = Assert.IsType<ScheduledControlActivation>(delayed.Diagnostics.Single(d => d.RiderId == 2).ControlActivation);
        var witness = ControlCases.Audit(free).Intervals.First(r => r.MinimumSeparationMeters < -GeometryNumerics.ContactDistanceMeters);
        Assert.True(activation.ActualElapsedTimeSeconds < witness.MinimumSeparationCommonTimeSeconds);
        var path = delayed.Diagnostics.Single(d => d.RiderId == 2).ExecutedPath!;
        Prefix(free.Diagnostics.Single(d => d.RiderId == 2).ExecutedPath!, path, activation.AcceptedStepIndex);
        Assert.True(path.Nodes[^1].PhysicalOffsetMeters > path.Nodes[activation.AcceptedStepIndex].PhysicalOffsetMeters);
        Assert.DoesNotContain(ControlCases.Audit(immediate).Intervals, r => r.HasConflict || !r.NumericallyResolved);
        // Audit the delayed candidate too; a remaining contact is a valid physical result.
        Assert.Empty(ControlCases.Audit(delayed).FrameCoverageGaps);
        Assert.All(delayed.Snapshot.Riders, r => Assert.Equal(0, r.ElapsedTimeSeconds));
    }

    [Theory]
    [InlineData(1.05f, SegmentOutcome.Brake)] [InlineData(1.2f, SegmentOutcome.RunWide)] [InlineData(1.5f, SegmentOutcome.Crash)]
    public void ACommandCannotCancelEntryCorrectionForcedRunWideOrCrash(float factor, SegmentOutcome expected)
    {
        var context = ControlCases.Prepare("corner"); var rider = context.Snapshot.Riders[0];
        var phase = context.Snapshot.Track.CornerTopology.Resolve(2, rider.SegmentProgress, rider.LateralPosition, context.Snapshot.Track.Geometry)!.Value;
        var target = ContinuousCornerEnvelope.Create(phase, rider.LateralPosition, context.Snapshot.Track.Geometry,
            context.Snapshot.TrackState.SampleSurface(2, rider.LateralPosition), rider.Profile.Skills, rider.ActiveSetup).SpeedMetersPerSecond(phase.CornerProgress);
        context = context with { Snapshot = new(context.Snapshot.Step, context.Snapshot.Track, context.Snapshot.TrackState,
            new[] { rider with { Speed = target * factor } }) };
        var before = ControlCases.Run(context); var changed = ControlCases.Run(Schedule(context, hold: true, targetMeters: 0, lift: false));
        Assert.Equal(expected, before.Changes[0].Outcome); Assert.Equal(before.Changes, changed.Changes);
        Assert.Equal(Path(before), Path(changed));
        if (expected != SegmentOutcome.Crash) Assert.True(Path(changed).Steps.Sum(s => s.CorrectionDistanceMeters) > 0);
        else Assert.False(changed.Changes[0].ApplySurfaceWear);
    }

    [Fact]
    public void UnsupportedIntegratorsAndInvalidCommandsFailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScheduledControlChange(1, double.NaN, new(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScheduledControlChange(1, -.1, new(1)));
        Assert.Throws<NotSupportedException>(() => ControlCases.Run(Schedule(ControlCases.Prepare("fixed-straight"))));
        Assert.Throws<NotSupportedException>(() => ControlCases.Run(Schedule(ControlCases.Prepare("fixed-corner"))));
        var context = Schedule(ControlCases.Prepare("straight"));
        Assert.Throws<NotSupportedException>(() => context.Engine.Resolve(context.Snapshot, context.Intents, ControlCases.Options));
        var wrong = context with { Intents = new[] { context.Intents[0] with { Decision = context.Intents[0].Decision with
            { ScheduledControl = new(999, .2, new(1)) } } } };
        Assert.Throws<ArgumentException>(() => ControlCases.Run(wrong));
        var outside = Schedule(ControlCases.Prepare("corner"), targetMeters: 999);
        Assert.Throws<ArgumentOutOfRangeException>(() => ControlCases.Run(outside));
    }
}
