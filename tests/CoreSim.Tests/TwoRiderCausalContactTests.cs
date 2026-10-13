using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using CoreSim.Setup;
using RaceReadiness;
using Xunit;
using Xunit.Abstractions;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class TwoRiderCausalContactTests(ITestOutputHelper output)
{
    private static HeatSimulationOptions Options => new() { Seed = 19, IncidentFrequency = 0,
        EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = true };
    private static SimulationEngine Engine() => new(new AdaptiveDecisionModel());
    private static SimulationSnapshot Origin()
    {
        var track = Track.CreateExample(); var surface = TrackState.CreateDefault(track).Snapshot();
        var a = new RiderSnapshot(1, RiderProfile.CreateDefault(1), RiderPosition.Create(1, 7, .05f, 8),
            5, 1, .98f, 18, 0, RiderRaceStatus.Racing, 0, BikeSetup.Neutral, .5f, .5f);
        var b = a with { RiderId = 2, Profile = RiderProfile.CreateDefault(2), Speed = 17,
            Position = RiderPosition.Create(1, 7, .09f, 8) };
        return new(new(91, 7, 0, 7, 19, 4), track, surface, new[] { a, b });
    }
    private static RiderIntent[] Intents => new[] { new RiderIntent(1, new RiderDecision(1)), new RiderIntent(2, new RiderDecision(1)) };
    [Fact]
    public void ProductionPairContactAndContinuation()
    {
        var snapshot = Origin(); var unchanged = Exact.Hash(snapshot); var counts = new Dictionary<ProjectionMaterialization, int>();
        TwoRiderContactResult result;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, count) => counts[kind] = counts.GetValueOrDefault(kind) + count;
            result = TwoRiderContactExecution.Execute(Engine(), snapshot, Intents, Options);
        }
        finally { ProjectionCaptureAudit.Observer = null; }
        Assert.True(result.ContinuationCertified);
        Assert.Single(result.Plan.AppliedPairs); Assert.Equal(2, result.Plan.Riders.Count);
        Assert.Equal(result.AfterEvent[0].ElapsedTimeSeconds, result.AfterEvent[1].ElapsedTimeSeconds);
        Assert.True(result.Ownership.Consumed); Assert.Equal(0, result.Ownership.Generation);
        Assert.False(result.Ownership.CanApply(result.CertifiedThroughSeconds, result.CertifiedThroughSeconds));
        Assert.All(result.ContactGeometry.Intervals, row => Assert.True(row.NumericallyResolved));
        Assert.All(result.ContinuationGeometry.Intervals, row => Assert.True(row.NumericallyResolved));
        Assert.All(result.ContinuationGeometry.Intervals, row => Assert.True(row.MinimumSeparationMeters < Options.ContestedSpaceParameters.ReleaseClearanceMeters));
        Assert.All(result.ContinuationGeometry.Intervals, row => Assert.True(row.MinimumSeparationMeters >= -GeometryNumerics.ContactDistanceMeters));
        Assert.All(result.CertifiedHistory, pose => Assert.True(pose.EndTimeSeconds <= result.CertifiedThroughSeconds));
        Assert.Equal(unchanged, Exact.Hash(snapshot)); Assert.Equal(0, counts.GetValueOrDefault(ProjectionMaterialization.PrivateCommit));
        foreach (var i in Enumerable.Range(0, 2))
        {
            var before = result.Before[i]; var after = result.After[i]; var effect = result.Plan.Riders[i];
            Assert.InRange(before.Boundary.TimeSeconds - result.ContactTimeSeconds, 0, ExecutedPathTraversal.TimeToleranceSeconds);
            Assert.Equal(before.Boundary.Speed, effect.PreContactSpeed);
            Assert.Equal(effect.PostContactSpeed, result.AfterEvent[i].Speed);
            Assert.Equal(RiderRaceStatus.Racing, result.AfterEvent[i].Status);
            Assert.Equal(PhysicalContactSeverity.Disturbed, effect.Severity); Assert.NotNull(effect.Recovery);
            Assert.Equal(before.Boundary.Steps, after.Boundary.Steps.Take(before.Boundary.CompletedSteps));
            var suffix = after.Boundary.Steps.Skip(before.Boundary.CompletedSteps).ToArray();
            Assert.Equal(6, suffix.Length); Assert.All(suffix, s => Assert.True(s.DistanceMeters > 0 && s.TimeSeconds > 0));
            Assert.InRange(after.Boundary.DistanceMeters - before.Boundary.DistanceMeters, 5.99, 6.01);
            Assert.Equal(before.Boundary.DistanceMeters + suffix.Sum(s => (double)s.DistanceMeters), after.Boundary.DistanceMeters);
            Assert.Equal(effect.PostContactSpeed, after.Boundary.ExecutionEndpoints![before.Boundary.CompletedSteps].Speed);
            Assert.True(after.Boundary.TimeSeconds > before.Boundary.TimeSeconds);
            Assert.Throws<RiderIntegrationRefusalException>(() => before.Resume(snapshot.TrackState));
            Assert.Throws<RiderIntegrationRefusalException>(() => after.Resume(snapshot.TrackState));
            var rider = snapshot.Riders[i]; var one = new SimulationSnapshot(snapshot.Step, snapshot.Track, snapshot.TrackState, new[] { rider });
            var parent = Engine().BeginRiderIntegration(one, Intents[i], Options, 1).Cursor!;
            var trial = parent.PreviewToTime(result.RequestedEventTimeSeconds);
            Assert.Throws<RiderIntegrationRefusalException>(() => parent.AcceptTimeTrial(TrackState.CreateDefault(snapshot.Track).Snapshot(), trial));
            var otherOwner = Engine().BeginRiderIntegration(one, Intents[i], Options, 1).Cursor!;
            Assert.Throws<RiderIntegrationRefusalException>(() => otherOwner.AcceptTimeTrial(snapshot.TrackState, trial));
            var baseline = parent.AcceptTimeTrial(snapshot.TrackState, trial);
            Assert.Equal(Exact.Hash(before.Boundary), Exact.Hash(baseline.Boundary));
            var control = baseline.ResumeEventWithoutConsequence(6).Cursor!;
            Assert.NotEqual(control.Boundary.Speed, after.Boundary.Speed);
            Assert.NotEqual(control.Boundary.TimeSeconds, after.Boundary.TimeSeconds);
            Assert.NotEqual(control.Boundary.Steps.Skip(before.Boundary.CompletedSteps), suffix);
            output.WriteLine(FormattableString.Invariant($"rider={rider.RiderId}; eventTime={before.Boundary.TimeSeconds:R}; progress={before.Boundary.CanonicalProgress:R}; distance={before.Boundary.DistanceMeters:R}; lateral={before.Boundary.LateralPosition:R}; pre={effect.PreContactSpeed:R}; post={effect.PostContactSpeed:R}; endSpeed={after.Boundary.Speed:R}; endTime={after.Boundary.TimeSeconds:R}; controlSpeed={control.Boundary.Speed:R}; controlTime={control.Boundary.TimeSeconds:R}"));
        }
        output.WriteLine(FormattableString.Invariant($"touch={result.ContactTimeSeconds:R}; timeTrials={result.PairTimeTrials}; coupledEvaluations={counts.GetValueOrDefault(ProjectionMaterialization.CoupledEvaluation)}; min={result.ContinuationGeometry.Intervals.Min(r=>r.MinimumSeparationMeters):R}; lower={result.ContinuationGeometry.Intervals.Min(r=>r.MinimumSeparationLowerBoundMeters):R}"));
    }

    [Fact]
    public void LateralEncroachmentCannotPublishBySuppressingRepeatedImpulse()
    {
        var track = Track.CreateExample(); var surface = TrackState.CreateDefault(track).Snapshot();
        var a = new RiderSnapshot(1, RiderProfile.CreateDefault(1), RiderPosition.Create(1, 7, .05f, 8),
            5, 1, .8f, 18, 0, RiderRaceStatus.Racing, 0, BikeSetup.Neutral, .5f, .5f);
        var b = a with { RiderId = 2, Profile = RiderProfile.CreateDefault(2), Speed = 17, LateralPosition = .98f,
            Position = RiderPosition.Create(1, 7, .09f, 8) };
        var snapshot = new SimulationSnapshot(new(91, 7, 0, 7, 19, 4), track, surface, new[] { a, b });
        var options = new HeatSimulationOptions { Seed = 19, IncidentFrequency = 0,
            EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = true };
        var result = TwoRiderContactExecution.Execute(new(new AdaptiveDecisionModel()), snapshot,
            new[] { new RiderIntent(1, new RiderDecision(2)), new RiderIntent(2, new RiderDecision(1)) }, options);
        output.WriteLine($"unsafe time={result.ContactTimeSeconds:R}; safe={result.ContinuationCertified}; min={result.ContinuationGeometry.Intervals.Min(r=>r.MinimumSeparationMeters):R}");
        Assert.False(result.ContinuationCertified);
        Assert.Equal(result.ContactTimeSeconds, result.CertifiedThroughSeconds);
        Assert.All(result.CertifiedHistory, p => Assert.True(p.EndTimeSeconds <= result.ContactTimeSeconds));
        Assert.True(result.Ownership.Consumed);
        Assert.All(result.After, c => Assert.Throws<RiderIntegrationRefusalException>(() => c.Resume(surface)));
    }

    [Fact]
    public void FrozenPairExecutionIsDeterministicUnderReversedInputOrder()
    {
        var origin = Origin(); var first = TwoRiderContactExecution.Execute(Engine(), origin, Intents, Options);
        var reversed = new SimulationSnapshot(origin.Step, origin.Track, origin.TrackState, origin.Riders.Reverse().ToArray());
        var second = TwoRiderContactExecution.Execute(Engine(), reversed, Intents.Reverse().ToArray(), Options);
        object Evidence(TwoRiderContactResult r) => new { r.ContactTimeSeconds, Before = r.Before.Select(c => c.Boundary),
            After = r.After.Select(c => c.Boundary), r.AfterEvent, r.Plan, r.ContactGeometry, r.ContinuationGeometry, r.Ownership,
            r.CertifiedThroughSeconds, r.PairTimeTrials };
        Assert.Equal(Exact.Hash(Evidence(first)), Exact.Hash(Evidence(second)));
    }

    [Fact]
    public void SeparateCapturedContextsPreserveAddressesAndRefuseMissingCoverageOrWorldVersion()
    {
        var snapshot = Origin();
        var origins = snapshot.Riders.Select((r, i) => new SimulationSnapshot(snapshot.Step with { StepNumber = 37 + i },
            snapshot.Track, snapshot.TrackState, new[] { r })).ToArray();
        var result = TwoRiderContactExecution.Execute(Engine(), origins.Reverse().ToArray(), Intents.Reverse().ToArray(), Options);
        Assert.Equal(new[] { 37, 38 }, result.Before.Select(c => c.Snapshot.Step.StepNumber));
        Assert.Equal(origins, result.Before.Select(c => c.Snapshot));
        var later = new SimulationSnapshot(origins[1].Step, snapshot.Track, snapshot.TrackState,
            new[] { origins[1].Riders[0] with { ElapsedTimeSeconds = .01f } });
        Assert.Contains("missing time coverage", Assert.Throws<RiderIntegrationRefusalException>(() =>
            TwoRiderContactExecution.Execute(Engine(), new[] { origins[0], later }, Intents, Options)).Message);
        var detached = new SimulationSnapshot(origins[1].Step, snapshot.Track, TrackState.CreateDefault(snapshot.Track).Snapshot(),
            origins[1].Riders);
        Assert.Contains("surface versions", Assert.Throws<RiderIntegrationRefusalException>(() =>
            TwoRiderContactExecution.Execute(Engine(), new[] { origins[0], detached }, Intents, Options)).Message);
    }

    [Fact]
    public void RealControl19CAcceptsStraightCursorWithOriginalIncidentOptions()
    {
        var (track, surface, riders) = Catalog.All().Single(s => s.Id == "control").Create(); var capture = new Capture();
        var options = Options with { IncidentFrequency = 1, PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit };
        Assert.Throws<Done>(() => new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(track, surface, riders, options, 91, capture));
        var peer = capture.Step!.Snapshot.Rider(2);
        Assert.Equal(SegmentType.Straight, track.Segments[peer.SegmentIndex].Type); Assert.Equal(8, peer.SegmentIndex);
        Assert.Null(peer.ContactRecovery);
        var one = new SimulationSnapshot(capture.Step.Snapshot.Step, track, capture.Step.Snapshot.TrackState, new[] { peer });
        Assert.NotNull(Engine().BeginRiderIntegration(one, new(2, new RiderDecision(0)), options, 1).Cursor);
    }
    private sealed class Done : Exception;
    private sealed class Capture : ISimulationStepObserver
    {
        internal ResolvedSimulationStep? Step;
        public void OnStepResolved(ResolvedSimulationStep step) { if (step.Snapshot.Step.StepNumber == 8) { Step = step; throw new Done(); } }
    }
}
