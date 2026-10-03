using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using Xunit;
using TrajectoryIntent = CoreSim.Decisions.TrajectoryIntent;

namespace CoreSim.Tests;

public sealed class LeanProjectionTests
{
    public static IEnumerable<object[]> Matrix()
    {
        foreach (var segment in new[] { 0, 1, 2, 3, 8 })
        foreach (var moving in new[] { false, true })
        foreach (var poor in new[] { false, true })
            yield return new object[] { segment, moving, poor, 0f, 0, 22f };
        foreach (var segment in new[] { 0, 1, 2, 3, 5, 8 })
        foreach (var moving in new[] { false, true })
            yield return new object[] { segment, moving, true, .125f, 3, 31f };
        yield return new object[] { 1, false, true, 0f, 0, 80f };
        yield return new object[] { 2, true, true, .25f, 0, 35f };
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void FullAndLeanHaveBitExactStateWearAndAllPhaseObservations(int index,
        bool moving, bool poor, float progress, int lap, float speed)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = TrajectoryEvaluatorTests.At(track, index, poor ? 4 : 2, speed, progress, lap);
        if (moving) rider.LateralPosition = poor ? 3.37f : 2.21f;
        var state = new TrackState(track.Segments.Count, 5, (s, l) => poor
            ? new(.50f + l * .045f, .12f + s * .003f, .61f)
            : new(1f - l * .03f, l * .013f, .35f));
        var context = TrajectoryEvaluatorTests.Context(track, rider, state);
        foreach (var intent in TrajectoryCandidates.Generate(context.Segment.Type)) Verify(context, intent);
    }

    [Theory]
    [InlineData(StartingGate.A)] [InlineData(StartingGate.B)]
    [InlineData(StartingGate.C)] [InlineData(StartingGate.D)]
    public void EveryGateRetainsReactionAndExactOwnWear(StartingGate gate)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var context = TrajectoryEvaluatorTests.Context(track,
            new RiderState(RiderProfile.CreateDefault(17), gate, track));
        foreach (var intent in TrajectoryCandidates.Generate(SegmentType.Straight)) Verify(context, intent);
    }

    [Fact]
    public void DeterministicCrashHasTheSameTerminalStateAndNoWear()
    {
        var track = TrajectoryEvaluatorTests.CornerFirst(Track.CreateStandingStartExample().Geometry);
        var context = TrajectoryEvaluatorTests.Context(track, TrajectoryEvaluatorTests.At(track, 1, 4, 80f));
        Verify(context, new(4, 4, 4));
        Assert.Contains(new TrajectoryEvaluator(context).Evaluate(new(4, 4, 4)).PhaseEndpoints,
            p => p.Outcome == SegmentOutcome.Crash);
    }

    [Fact]
    public void PhysicalRunWideKeepsExactOutwardTraversalAndWear()
    {
        var track = new Track(new[] { new TrackSegment(0,SegmentType.TurnEntry) });
        var context = Enumerable.Range(22,22).Select(speed => TrajectoryEvaluatorTests.Context(track,
                TrajectoryEvaluatorTests.At(track,0,1,speed,id:1),
                TrackState.CreateDefault(track,new TrackSurfaceState(1f,0f,.35f)),laps:1))
            .First(c => new TrajectoryEvaluator(c).Evaluate(new(1,1,1)).PhaseEndpoints[0].Outcome == SegmentOutcome.RunWide);
        Verify(context,new(1,1,1));
    }

    internal static void Verify(RiderDecisionContext context, TrajectoryIntent intent)
    {
        var fullRider = context.Rider.ToMutableCopy(); var leanRider = context.Rider.ToMutableCopy();
        var fullState = new TrackState(context.TrackState.SegmentCount, 5, context.TrackState.GetSurface);
        var leanState = fullState.Clone(); var engine = new SimulationEngine(new Target(0));
        var options = new HeatSimulationOptions { Laps = context.Snapshot.Step.RequiredLaps, IncidentFrequency = 0, EnableLogging = false };
        var evaluator = new TrajectoryEvaluator(context);
        var rich = evaluator.Evaluate(intent, true); var lean = evaluator.Evaluate(intent);
        var richJson = System.Text.Json.JsonSerializer.Serialize(rich with { ResolvedMotions = Array.Empty<ResolvedRiderMotion>() });
        Assert.Equal(richJson, System.Text.Json.JsonSerializer.Serialize(lean));
        var offset = 0;
        foreach (var part in evaluator.Horizon)
        {
            if (fullRider.Status is not (RiderRaceStatus.NotStarted or RiderRaceStatus.Racing)) break;
            var step = context.Snapshot.Step with { StepNumber = context.StepNumber + offset++,
                SegmentIndex = part.SegmentIndex, LapIndex = part.LapIndex };
            var fullInput = engine.CaptureSnapshot(context.Snapshot.Track, fullState, new[] { fullRider }, step);
            var leanInput = engine.CaptureSnapshot(context.Snapshot.Track, leanState, new[] { leanRider }, step);
            Assert.Equal(fullInput.Riders[0], leanInput.Riders[0]);
            var target = TrajectoryEvaluator.Target(intent, part.Phase);
            var full = engine.Resolve(fullInput, new[] { new RiderIntent(fullRider.RiderId, new(target)) }, options);
            var projected = SimulationEngine.ResolveSoloProjection(leanInput, leanInput.Riders[0], new(target), options);
            Assert.Equal(full.Changes[0], projected.Change);
            Bits(full.Motions[0].TotalTimeSeconds, projected.TravelTimeSeconds);
            Bits(full.Motions[0].TotalDistanceMeters, projected.DistanceMeters);
            Bits(full.Changes[0].LateralPosition, projected.Change.LateralPosition);
            Bits(full.Changes[0].Speed, projected.Change.Speed);
            Assert.Equal(full.Diagnostics[0].ExecutedPath?.Steps.Sum(s => s.TimeSolveSubdivisions) ?? 0,
                projected.TimeSolveSubdivisions);
            engine.Commit(full, new[] { fullRider }, fullState, new SimLog(false));
            SimulationEngine.CommitSoloProjection(projected, leanRider, leanState);
            Assert.Equal(fullRider.Position, leanRider.Position);
            Bits(fullRider.ElapsedTimeSeconds, leanRider.ElapsedTimeSeconds);
            for (var s = 0; s < fullState.SegmentCount; s++)
            for (var l = 0; l < 5; l++)
            {
                var a = fullState.GetSurface(s,l); var b = leanState.GetSurface(s,l);
                Bits(a.Grip,b.Grip); Bits(a.Ruts,b.Ruts); Bits(a.Moisture,b.Moisture);
            }
        }
    }
    private static void Bits(float a,float b) => Assert.Equal(BitConverter.SingleToInt32Bits(a), BitConverter.SingleToInt32Bits(b));
    private sealed class Target(int target) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment,RiderState rider) => new(target);
    }
}
