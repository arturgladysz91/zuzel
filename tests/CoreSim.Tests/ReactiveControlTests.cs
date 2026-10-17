using CoreSim.Decisions;
using MidSegmentControl;
using ReactiveControl;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class ReactiveControlTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NewCommandIsIssuedFromAnAcceptedOwnPointAndProductionForecastKeepThePrefix(bool lateral)
    {
        var context = ControlCases.Prepare(lateral ? "moving" : "straight");
        var original = ControlCases.Run(context); var own = context.Snapshot.Riders.Single();
        double? observed = null, requested = null; var issued = 0;
        var source = ReactiveEvidence.DeliverySource(context, lateral, (a, b) => { observed = a; requested = b; issued++; });
        var decision = context.Intents[0].Decision with { ControlSource = source };
        Assert.Null(decision.ScheduledControl);
        var changed = ControlCases.Run(context with { Intents = new[] { context.Intents[0] with { Decision = decision } } });
        var activation = Assert.IsType<ScheduledControlActivation>(changed.Diagnostics[0].ControlActivation);
        Assert.Equal(1, issued); Assert.True(observed > 0); Assert.True(requested > observed);
        Assert.Equal(requested, activation.RequestedElapsedTimeSeconds); Assert.True(activation.ActualElapsedTimeSeconds >= requested);
        var before = original.Diagnostics[0].ExecutedPath!; var after = changed.Diagnostics[0].ExecutedPath!;
        Assert.Equal(before.Nodes.Take(activation.AcceptedStepIndex + 1), after.Nodes.Take(activation.AcceptedStepIndex + 1));
        Assert.Equal(before.Steps.Take(activation.AcceptedStepIndex), after.Steps.Take(activation.AcceptedStepIndex));
        if (lateral) Assert.True(after.Nodes[^1].PhysicalOffsetMeters > after.Nodes[activation.AcceptedStepIndex].PhysicalOffsetMeters);
        else Assert.True(changed.Changes[0].Speed < original.Changes[0].Speed);
        var control = new InteractionProjectionControl(null, decision.HoldLateralPosition)
            { ControlSource = ReactiveEvidence.DeliverySource(context, lateral) };
        var forecast = TrajectoryEvaluator.ForScheduledControl(new RiderDecisionContext(context.Snapshot, own), control)
            .Evaluate(new(decision.TargetLane, decision.TargetLane, decision.TargetLane), retainResolvedMotions: true);
        Assert.Equal(changed.Motions[0], forecast.ResolvedMotions[0]); Assert.Equal(activation, forecast.ControlActivation);
        Assert.Equal(0, context.Snapshot.Riders[0].ElapsedTimeSeconds);
    }

    [Theory]
    [InlineData("A")] [InlineData("B")] [InlineData("C")] [InlineData("D")]
    public void PairCannotUseAFutureSegmentOrAnAsynchronousSnapshotAsACurrentObservation(string scenario)
    {
        var forward = ReactiveEvidence.AttemptPair(scenario); var reverse = ReactiveEvidence.AttemptPair(scenario, true);
        Assert.Equal(forward, reverse); Assert.Equal(0, forward.AcceptedCommonTime);
        Assert.True(forward.AttemptedAt > forward.PeerAvailableAt); Assert.Equal(1, forward.SourceCalls);
        // In C the trial stops before the known ~0.143 s witness, not through an overlap.
        if (scenario == "C") Assert.True(forward.AttemptedAt < .143);
    }

    [Fact]
    public void QuietOwnPointNotificationsDoNotChangePhysicsOrCreateACommand()
    {
        var context = ControlCases.Prepare("straight"); var count = 0;
        var original = ControlCases.Run(context);
        var quiet = context with { Intents = new[] { context.Intents[0] with { Decision = context.Intents[0].Decision with
            { ControlSource = _ => { count++; return null; } } } } };
        var changed = ControlCases.Run(quiet);
        Assert.Equal(ControlCases.Fingerprint(original), ControlCases.Fingerprint(changed));
        Assert.Equal(original.Diagnostics[0].ExecutedPath!.Steps.Count, count);
        Assert.Null(changed.Diagnostics[0].ControlActivation);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SourceCannotBackdateOrMisaddressItsNewCommand(bool wrongRider)
    {
        var context = ControlCases.Prepare("straight"); var id = context.Snapshot.Riders[0].RiderId;
        var bad = context with { Intents = new[] { context.Intents[0] with { Decision = context.Intents[0].Decision with
            { ControlSource = node => new(wrongRider ? 999 : id, wrongRider ? 1 : node.ElapsedTimeSeconds - .001, new(1)) } } } };
        Assert.Throws<ArgumentException>(() => ControlCases.Run(bad));
    }

    [Fact]
    public void SourceDoesNotSilentlySwitchAFixedLineOrCompeteWithAPredeclaredCommand()
    {
        var fixedLine = ControlCases.Prepare("fixed-straight");
        var decision = fixedLine.Intents[0].Decision with { ControlSource = _ => null };
        Assert.Throws<NotSupportedException>(() => ControlCases.Run(fixedLine with { Intents = new[] { fixedLine.Intents[0] with { Decision = decision } } }));
        var context = ControlCases.Prepare("straight");
        decision = context.Intents[0].Decision with { ControlSource = _ => null, ScheduledControl = new(2, .2, new(1)) };
        Assert.Throws<ArgumentException>(() => ControlCases.Run(context with { Intents = new[] { context.Intents[0] with { Decision = decision } } }));
        decision = decision with { ScheduledControl = null };
        Assert.Throws<NotSupportedException>(() => SimulationEngine.ResolveSoloProjection(context.Snapshot,
            context.Snapshot.Riders[0], decision, ControlCases.Options));
    }
}
