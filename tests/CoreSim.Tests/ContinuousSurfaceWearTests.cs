using CoreSim;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class ContinuousSurfaceWearTests
{
    private static readonly TrackSurfaceState NeutralSurface = new(0.80f, 0.20f, 0.35f);

    private sealed class FixedTargetDecisionModel(int targetLane) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(targetLane, 0f);
    }

    private sealed class PerRiderTargetDecisionModel(IReadOnlyDictionary<int, int> targetLanes)
        : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(targetLanes[rider.RiderId], 0f);
    }

    [Theory]
    [InlineData(0f, 1f, 0.20f, 0f, 0f, 0f)]
    [InlineData(1f, 0.20f, 1f, 0.20f, 0f, 0f)]
    [InlineData(2f, 0f, 0.20f, 1f, 0.20f, 0f)]
    [InlineData(3f, 0f, 0f, 0.20f, 1f, 0.20f)]
    [InlineData(4f, 0f, 0f, 0f, 0.20f, 1f)]
    public void IntegerEntryPositionsPreserveAdvancedWearKernel(
        float entryPosition,
        float lane0,
        float lane1,
        float lane2,
        float lane3,
        float lane4)
    {
        var lane = (int)entryPosition;

        var result = RunSingle(
            SegmentType.TurnMiddle,
            Rider(1, lane, entryPosition, speed: 8f),
            targetLane: lane);

        AssertAdvancedWear(result, SegmentType.TurnMiddle, lane0, lane1, lane2, lane3, lane4);
        Assert.All(result.Log.SurfaceChanges, change =>
            Assert.Contains(change.Reason, new[] { "pass", "pass-adjacent" }));
    }

    [Theory]
    [InlineData(1.50f, 0.10f, 0.60f, 0.60f, 0.10f, 0f)]
    [InlineData(1.25f, 0.15f, 0.80f, 0.40f, 0.05f, 0f)]
    [InlineData(0.50f, 0.60f, 0.60f, 0.10f, 0f, 0f)]
    [InlineData(3.50f, 0f, 0f, 0.10f, 0.60f, 0.60f)]
    public void FractionalTurnEntryBlendsAndAggregatesDiscreteWearKernels(
        float entryPosition,
        float lane0,
        float lane1,
        float lane2,
        float lane3,
        float lane4)
    {
        var lane = (int)MathF.Round(entryPosition);

        var result = RunSingle(
            SegmentType.TurnMiddle,
            Rider(1, lane, entryPosition, speed: 8f),
            targetLane: lane);

        AssertAdvancedWear(result, SegmentType.TurnMiddle, lane0, lane1, lane2, lane3, lane4);
        Assert.All(result.Log.SurfaceChanges, change => Assert.Equal("pass-continuous", change.Reason));
    }

    [Fact]
    public void StraightContinuousWearUsesEntryPositionAndStraightMagnitude()
    {
        var result = RunSingle(
            SegmentType.Straight,
            Rider(1, lane: 1, lateralPosition: 1.5f, speed: 8f),
            targetLane: 2);

        AssertAdvancedWear(result, SegmentType.Straight, 0.10f, 0.60f, 0.60f, 0.10f, 0f);
    }

    [Fact]
    public void AdvancedWearUsesEntryPositionInsteadOfPlannedOrPostMovementPosition()
    {
        const float entryPosition = 1.25f;
        var result = RunSingle(
            SegmentType.TurnMiddle,
            Rider(1, lane: 1, entryPosition, speed: 8f),
            targetLane: 2);

        Assert.Equal(2, result.Change.PlannedLane);
        Assert.True(result.Change.LateralPosition > entryPosition);
        AssertAdvancedWear(result, SegmentType.TurnMiddle, 0.15f, 0.80f, 0.40f, 0.05f, 0f);
    }

    [Fact]
    public void RunWideWearRemainsCenteredOnEntryPosition()
    {
        const float entryPosition = 1.40f;
        var maxSafeSpeed = CornerTestSupport.SingleEnvelopeSpeed(
            entryPosition,
            TrackGeometry.Default,
            NeutralSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        var result = RunSingle(
            SegmentType.TurnMiddle,
            Rider(1, lane: 1, entryPosition, speed: maxSafeSpeed * 1.20f),
            targetLane: 2);

        Assert.Equal(2, result.Change.PlannedLane);
        Assert.Equal(SegmentOutcome.RunWide, result.Change.Outcome);
        Assert.Equal(3, result.Change.Lane);
        AssertAdvancedWear(result, SegmentType.TurnMiddle, 0.12f, 0.68f, 0.52f, 0.08f, 0f);
    }

    [Fact]
    public void PhysicsCrashStillSkipsSurfaceWear()
    {
        const float entryPosition = 1.40f;
        var maxSafeSpeed = CornerTestSupport.SingleEnvelopeSpeed(
            entryPosition,
            TrackGeometry.Default,
            NeutralSurface,
            RiderSkills.Balanced,
            BikeSetup.Neutral);
        var result = RunSingle(
            SegmentType.TurnMiddle,
            Rider(1, lane: 1, entryPosition, speed: maxSafeSpeed * 1.30f),
            targetLane: 2);

        Assert.Equal(SegmentOutcome.Crash, result.Change.Outcome);
        Assert.Empty(result.Log.SurfaceChanges);
        Assert.All(CaptureSurfaces(result.TrackState), surface => Assert.Equal(NeutralSurface, surface));
    }

    [Fact]
    public void LegacySurfaceWearRemainsDiscreteOnResolvedLane()
    {
        var result = RunSingle(
            SegmentType.TurnMiddle,
            Rider(1, lane: 1, lateralPosition: 1.25f, speed: 8f),
            targetLane: 2,
            useLegacyPhysics: true);

        Assert.Equal(2, result.Change.Lane);
        for (var lane = LaneModel.MinLane; lane <= LaneModel.MaxLane; lane++)
        {
            var actual = result.TrackState.GetSurface(0, lane);
            var expectedRuts = lane == 2 ? NeutralSurface.Ruts + 0.02f : NeutralSurface.Ruts;
            var expectedGrip = lane == 2 ? NeutralSurface.Grip - 0.005f : NeutralSurface.Grip;
            Assert.Equal(expectedRuts, actual.Ruts, 5);
            Assert.Equal(expectedGrip, actual.Grip, 5);
            Assert.Equal(NeutralSurface.Moisture, actual.Moisture);
        }

        var change = Assert.Single(result.Log.SurfaceChanges);
        Assert.Equal(2, change.LineIndex);
        Assert.Equal("pass", change.Reason);
    }

    [Fact]
    public void ContinuousWearAndLogsAreIndependentOfRiderCollectionOrder()
    {
        var forward = RunMany(reverseRiders: false);
        var reversed = RunMany(reverseRiders: true);

        Assert.Equal(CaptureSurfaces(forward.TrackState), CaptureSurfaces(reversed.TrackState));
        Assert.Equal(forward.Log.Lines, reversed.Log.Lines);
        Assert.Equal(forward.Log.SurfaceChanges, reversed.Log.SurfaceChanges);
    }

    private static StepResult RunSingle(
        SegmentType segmentType,
        RiderState rider,
        int targetLane,
        bool useLegacyPhysics = false)
    {
        var trackState = new TrackState(1, LaneModel.LanesCount, NeutralSurface);
        var engine = new SimulationEngine(new FixedTargetDecisionModel(targetLane));
        return Run(engine, segmentType, trackState, new[] { rider }, useLegacyPhysics);
    }

    private static StepResult RunMany(bool reverseRiders)
    {
        var riders = new[]
        {
            Rider(1, lane: 1, lateralPosition: 1.25f, speed: 8f),
            Rider(2, lane: 3, lateralPosition: 3.25f, speed: 8f),
        };
        if (reverseRiders)
            Array.Reverse(riders);

        var trackState = new TrackState(1, LaneModel.LanesCount, NeutralSurface);
        var engine = new SimulationEngine(new PerRiderTargetDecisionModel(
            new Dictionary<int, int> { [1] = 1, [2] = 3 }));
        return Run(engine, SegmentType.TurnMiddle, trackState, riders, useLegacyPhysics: false);
    }

    private static StepResult Run(
        SimulationEngine engine,
        SegmentType segmentType,
        TrackState trackState,
        IReadOnlyList<RiderState> riders,
        bool useLegacyPhysics)
    {
        var track = new Track(new[] { new TrackSegment(0, segmentType) }, TrackGeometry.Default);
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 123,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };
        var snapshot = engine.CaptureSnapshot(
            track,
            trackState,
            riders,
            new SimulationStepContext(
                HeatId: 1,
                StepNumber: 0,
                LapIndex: 0,
                SegmentIndex: 0,
                Seed: options.Seed,
                RequiredLaps: options.Laps,
                UseLegacyPhysics: useLegacyPhysics));
        var resolved = engine.Resolve(snapshot, engine.Decide(snapshot), options);
        var log = new SimLog();

        engine.Commit(resolved, riders, trackState, log);

        return new StepResult(resolved.Changes, trackState, log);
    }

    private static RiderState Rider(
        int riderId,
        int lane,
        float lateralPosition,
        float speed)
        => new(riderId, lane)
        {
            LateralPosition = lateralPosition,
            Speed = speed,
        };

    private static void AssertAdvancedWear(
        StepResult result,
        SegmentType segmentType,
        params float[] wearWeights)
    {
        var baseRutsDelta = segmentType == SegmentType.Straight ? 0.004f : 0.015f;
        for (var lane = LaneModel.MinLane; lane <= LaneModel.MaxLane; lane++)
        {
            var actual = result.TrackState.GetSurface(0, lane);
            Assert.Equal(NeutralSurface.Ruts + baseRutsDelta * wearWeights[lane], actual.Ruts, 5);
            Assert.Equal(NeutralSurface.Grip - 0.25f * baseRutsDelta * wearWeights[lane], actual.Grip, 5);
            Assert.Equal(NeutralSurface.Moisture, actual.Moisture);
        }

        var expectedChangedLanes = wearWeights
            .Select((weight, lane) => (weight, lane))
            .Where(item => item.weight > 0f)
            .Select(item => item.lane)
            .ToArray();
        Assert.Equal(expectedChangedLanes, result.Log.SurfaceChanges.Select(change => change.LineIndex));
        Assert.Equal(expectedChangedLanes.Length, result.Log.SurfaceChanges.Select(change => change.LineIndex).Distinct().Count());
        foreach (var change in result.Log.SurfaceChanges)
        {
            Assert.Equal(-0.25f * baseRutsDelta * wearWeights[change.LineIndex], change.DeltaGrip, 5);
            Assert.Equal(baseRutsDelta * wearWeights[change.LineIndex], change.DeltaRuts, 5);
            Assert.Equal(0f, change.DeltaMoisture);
        }
    }

    private static TrackSurfaceState[] CaptureSurfaces(TrackState trackState)
        => Enumerable.Range(0, trackState.LinesCount)
            .Select(lane => trackState.GetSurface(0, lane))
            .ToArray();

    private sealed record StepResult(
        IReadOnlyList<RiderStateChange> Changes,
        TrackState TrackState,
        SimLog Log)
    {
        public RiderStateChange Change => Assert.Single(Changes);
    }
}
