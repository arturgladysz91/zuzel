using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class FreeRacingTests
{
    private static (FreeRacingInput Input, RiderSpec Spec, TrackFrame Frame, TrackStateSnapshot Surface) Fixture() {
        var spec = FreeRacingExperiment.Rider(FreeRacingExperiment.TrainingScenarios[0]); var frame = RacingLineRootCauseAudit.Frame();
        var at = frame.At(spec.ReferenceMeters, spec.OffsetMeters);
        var v = new RiderView(spec.Profile.Id, at.Position, at.Road.Tangent * spec.SpeedMetersPerSecond, (float)at.Road.Heading,
            0, at.Road, spec.ReferenceMeters, 0, RiderActivity.Racing, 1, spec.TargetOffsetMeters, 0);
        return (new(v, 0, spec.SpeedMetersPerSecond / .33, 0, new(0, 1), Array.Empty<FreeRacingCommand>(), .1), spec, frame, TrackState.CreateDefault(frame.Track, new(1, 0, .5f)).Snapshot());
    }
    [Fact] public void OffDoesNotChangeDefaultSerializationAndRequiresExplicitEnablement() {
        Assert.False(new WorldParameters().EnableFreeRacing);
        Assert.DoesNotContain("EnableFreeRacing", JsonSerializer.Serialize(new WorldParameters()));
        var f = Fixture(); Assert.Throws<InvalidOperationException>(() => FreeRacingModel.Predict(f.Input, f.Spec, f.Frame, f.Surface, new(), 147, 52.72097f, .3));
    }
    [Fact] public void PolicyAndObservationContainNoPathOrDestination() {
        Assert.Equal(7, new FreeRacingPolicy().Values.Length);
        Assert.DoesNotContain(typeof(FreeRacingPolicy).GetProperties(), p => p.Name.Contains("Offset") || p.Name.Contains("Path"));
        var f = Fixture(); var p = FreeRacingExperiment.Parameters();
        var a = FreeRacingController.Choose(f.Input, new(), f.Spec, f.Frame, f.Surface, p, 147, 52.72097f);
        var b = FreeRacingController.Choose(f.Input with { Rider = f.Input.Rider with { TargetOffsetMeters = 9 } }, new(), f.Spec, f.Frame, f.Surface, p, 147, 52.72097f);
        Assert.Equal(a.Action, b.Action); Assert.Equal(a.Candidates, b.Candidates);
        Assert.False(p.EnableTravelDirectionSteering); Assert.False(p.EnablePreparedCornerPlan);
    }
    [Fact] public void PendingAndBothActionsRetainCompleteReactionAndPhysicalSlew() {
        var f = Fixture(); var reaction = RiderController.ReactionSeconds(f.Spec); var p = FreeRacingExperiment.Parameters();
        var action = new FreeRacingAction(.3f, 0);
        var baseline = FreeRacingModel.Predict(f.Input, f.Spec, f.Frame, f.Surface, p, 147, 52.72097f, .6);
        var changed = FreeRacingModel.Predict(f.Input, f.Spec, f.Frame, f.Surface, p, 147, 52.72097f, .6, action);
        foreach (var point in changed.Points.Where(p => p.Time < reaction - 1e-8)) {
            var same = baseline.Points.Single(p => Math.Abs(p.Time - point.Time) < 1e-8); Assert.Equal(same, point);
        }
        var activation = changed.Points.Single(p => Math.Abs(p.Time - reaction) < 1e-8);
        Assert.Equal(0, activation.Rider.Drive); Assert.Equal(0, activation.Steering);
        Assert.NotEqual(baseline.Points[^1].Rider.Position, changed.Points[^1].Rider.Position);
        foreach (var pair in changed.Points.Zip(changed.Points.Skip(1)))
            Assert.InRange(Math.Abs(pair.Second.Steering - pair.First.Steering) / (pair.Second.Time - pair.First.Time), 0, Math.PI / 2 + 1e-4);
    }
    [Fact] public void IrrevocableQueueIsPreservedBeforeNewCandidate() {
        var f = Fixture(); var input = f.Input with { Time = .2, NextReview = .3, Pending = new[] { new FreeRacingCommand(0, .24, new(.1f, .2f)) } };
        var prediction = FreeRacingModel.Predict(input, f.Spec, f.Frame, f.Surface, FreeRacingExperiment.Parameters(), 147, 52.72097f, .3, new(-.1f, .7f));
        Assert.Equal(.2f, prediction.Points.Single(p => Math.Abs(p.Time - .24) < 1e-8).Rider.Drive);
        Assert.Equal(.7f, prediction.Points.Single(p => Math.Abs(p.Time - .44) < 1e-8).Rider.Drive);
        Assert.Equal(.24, input.Pending[0].ActivationAt);
    }
    [Fact] public void FutureCommandsAndUnvalidatedHorizonAreRejected() {
        var f = Fixture(); var p = FreeRacingExperiment.Parameters();
        Assert.Throws<ArgumentException>(() => FreeRacingModel.Predict(f.Input, f.Spec, f.Frame, f.Surface, p, 147, 52.72097f, .91));
        Assert.Throws<ArgumentException>(() => FreeRacingModel.Predict(f.Input with { Pending = new[] { new FreeRacingCommand(.1, .34, new(0, 0)) } }, f.Spec, f.Frame, f.Surface, p, 147, 52.72097f, .3));
    }
    [Fact] public void PredictionAndSerializedPolicyReplayAreDeterministicAndDetached() {
        var f = Fixture(); var policy = JsonSerializer.Deserialize<FreeRacingPolicy>(JsonSerializer.Serialize(new FreeRacingPolicy()))!;
        FreeRacingDecision Choose() => FreeRacingController.Choose(f.Input, policy, f.Spec, f.Frame, f.Surface, FreeRacingExperiment.Parameters(), 147, 52.72097f);
        Assert.Equal(JsonSerializer.Serialize(Choose()), JsonSerializer.Serialize(Choose()));
        Assert.Equal(0, f.Input.Time); Assert.Empty(f.Input.Pending); Assert.Equal(1, f.Input.Rider.Drive);
    }
    [Fact] public void BudgetReservesBeforeAttemptAndPersistsFailures() {
        var directory = Path.Combine(Path.GetTempPath(), "free-racing-budget-" + Guid.NewGuid());
        try {
            using (var b = new FreeRacingBudget(directory)) { for (var i = 0; i < 40; i++) b.Reserve("run-" + i, "test"); Assert.Throws<InvalidOperationException>(() => b.Reserve("41", "test")); }
            using (var b = new FreeRacingBudget(directory)) { Assert.Equal(40, b.Runs.Count); Assert.All(b.Runs, r => Assert.False(r.Complete)); Assert.Throws<InvalidOperationException>(() => b.Reserve("new", "test")); }
        } finally { Directory.Delete(directory, true); }
    }
    [Fact] public void SeedAndParameterBoundsAreExplicitAndReproducible() {
        var a = new FreeRacingExperiment.RandomStream(FreeRacingExperiment.Seed); var b = new FreeRacingExperiment.RandomStream(FreeRacingExperiment.Seed);
        for (var i = 0; i < 100; i++) Assert.Equal(a.Normal(), b.Normal());
        Assert.Throws<ArgumentException>(() => FreeRacingPolicy.From(new[] { double.NaN, 1, .2, 1, .5, .2, .7 }));
        Assert.Throws<ArgumentException>(() => FreeRacingPolicy.From(new double[6]));
    }
    [Fact] public void UnsafeApparentFinishNeverBeatsLegalCompletion() {
        FreeRacingMetrics Result(bool completed, double seconds) => new(completed, completed ? "completed" : "boundary", seconds, seconds, 18, 10, 15, 10, .2, 0, 0, 100, completed ? 1 : -1, 100, 100, 1, "test");
        var unsafeFast = new FreeRacingEvaluation(0, 0, new(), new[] { Result(false, 1), Result(false, 1) });
        var safe = new FreeRacingEvaluation(1, 0, new(), new[] { Result(true, 10), Result(true, 10) });
        Assert.Equal(1, FreeRacingExperiment.Rank(new[] { unsafeFast, safe }).First().PolicyId);
    }
}
