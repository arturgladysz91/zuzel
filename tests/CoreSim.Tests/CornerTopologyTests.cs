using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class CornerTopologyTests
{
    private sealed class HoldLane : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane, 0f);
    }

    [Fact]
    public void ExampleTracksExposeTwoSeparateLogicalCorners()
    {
        AssertCorners(
            Track.CreateExample(),
            (1, 0, 2),
            (2, 4, 6));
        AssertCorners(
            Track.CreateStandingStartExample(),
            (1, 1, 3),
            (2, 5, 7));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CornerProgressCoversZeroToOneContinuouslyAndMonotonically(bool standingStart)
    {
        var track = standingStart
            ? Track.CreateStandingStartExample()
            : Track.CreateExample();

        foreach (var corner in track.CornerTopology.Corners)
        {
            var observed = new List<float>();
            CornerPhaseContext? previous = null;
            for (var segmentIndex = corner.StartSegmentIndex;
                 segmentIndex <= corner.EndSegmentIndex;
                 segmentIndex++)
            {
                var start = Resolve(track, segmentIndex, 0f, 1.75f);
                var middle = Resolve(track, segmentIndex, 0.5f, 1.75f);
                var end = Resolve(track, segmentIndex, 1f, 1.75f);

                Assert.Equal(start.SegmentStartCornerProgress, start.CornerProgress);
                Assert.Equal(end.SegmentEndCornerProgress, end.CornerProgress);
                Assert.InRange(middle.CornerProgress, start.CornerProgress, end.CornerProgress);
                Assert.True(float.IsFinite(start.TotalCornerLengthMeters));
                Assert.True(float.IsFinite(start.RemainingCornerLengthMeters));
                Assert.True(start.TotalCornerLengthMeters > 0f);

                if (previous is { } prior)
                    Assert.Equal(prior.SegmentEndCornerProgress, start.SegmentStartCornerProgress);
                previous = end;
                observed.AddRange(new[] { start.CornerProgress, middle.CornerProgress, end.CornerProgress });
            }

            Assert.Equal(0f, observed[0]);
            Assert.Equal(1f, observed[^1]);
            Assert.True(observed.Zip(observed.Skip(1), (left, right) => left <= right).All(value => value));
        }
    }

    [Fact]
    public void ProgressUsesActualCornerSegmentCountInsteadOfHardCodedThirds()
    {
        var track = new Track(
            new[]
            {
                new TrackSegment(10, SegmentType.Straight),
                new TrackSegment(20, SegmentType.TurnEntry),
                new TrackSegment(30, SegmentType.TurnExit),
                new TrackSegment(40, SegmentType.Straight),
            },
            Track.CreateStandingStartExample().Geometry);

        var entryEnd = Resolve(track, 1, 1f, 2f);
        var exitStart = Resolve(track, 2, 0f, 2f);

        Assert.Equal(0.5f, entryEnd.CornerProgress);
        Assert.Equal(entryEnd.CornerProgress, exitStart.CornerProgress);
        Assert.Equal(2, entryEnd.SegmentCount);
    }

    [Fact]
    public void StraightsIncludingFinalStraightAreOutsideLogicalCorners()
    {
        foreach (var track in new[] { Track.CreateExample(), Track.CreateStandingStartExample() })
        {
            foreach (var index in Enumerable.Range(0, track.Segments.Count)
                         .Where(index => track.Segments[index].Type == SegmentType.Straight))
            {
                Assert.Null(track.CornerTopology.CornerForSegment(index));
                Assert.Null(track.CornerTopology.Resolve(index, 0.5f, 2f, track.Geometry));
            }

            Assert.Equal(SegmentType.Straight, track.Segments[^1].Type);
            Assert.Null(track.CornerTopology.CornerForSegment(track.Segments.Count - 1));
        }
    }

    [Fact]
    public void LapBoundaryNeverJoinsDistinctCornerRuns()
    {
        var track = new Track(new[]
        {
            new TrackSegment(10, SegmentType.TurnMiddle),
            new TrackSegment(20, SegmentType.Straight),
            new TrackSegment(30, SegmentType.TurnEntry),
            new TrackSegment(40, SegmentType.TurnExit),
        });

        AssertCorners(track, (1, 0, 0), (2, 2, 3));
        Assert.NotEqual(
            track.CornerTopology.CornerForSegment(0)!.CornerId,
            track.CornerTopology.CornerForSegment(3)!.CornerId);
        Assert.Equal(
            1,
            track.CornerTopology.ImmediateNextCorner(3, allowLapWrap: true)!.CornerId);
        Assert.Null(track.CornerTopology.ImmediateNextCorner(3, allowLapWrap: false));
    }

    [Fact]
    public void LateralPositionsPreserveNormalizedPhaseIdentityWithFiniteLengths()
    {
        var track = Track.CreateStandingStartExample();
        var contexts = new[] { 0f, 1f, 2.5f, 4f }
            .Select(lateral => Resolve(track, 2, 0.25f, lateral))
            .ToArray();

        Assert.All(contexts, context =>
        {
            Assert.Equal(1, context.CornerId);
            Assert.Equal(1, context.SegmentOffset);
            Assert.Equal(SegmentType.TurnMiddle, context.CompatibilitySegmentType);
            Assert.True(float.IsFinite(context.CornerProgress));
            Assert.True(float.IsFinite(context.TotalCornerLengthMeters));
            Assert.True(float.IsFinite(context.RemainingCornerLengthMeters));
        });
        Assert.All(contexts.Skip(1), context =>
            Assert.Equal(contexts[0].CornerProgress, context.CornerProgress, 6));
        Assert.True(contexts.Zip(contexts.Skip(1),
            (inner, outer) => inner.TotalCornerLengthMeters < outer.TotalCornerLengthMeters).All(value => value));
    }

    [Fact]
    public void AdvancedProductionDiagnosticsCarryTheSameCornerContextIndependentOfRiderOrder()
    {
        var forward = ResolveAdvanced(new[] { 1, 2 });
        var reversed = ResolveAdvanced(new[] { 2, 1 });

        var first = forward.Diagnostics.Select(item => item.CornerPhaseContext).ToArray();
        var second = reversed.Diagnostics.Select(item => item.CornerPhaseContext).ToArray();
        Assert.Equal(first, second);
        Assert.All(first, item =>
        {
            var context = Assert.IsType<CornerPhaseContext>(item);
            Assert.Equal(1, context.CornerId);
            Assert.Equal(SegmentType.TurnMiddle, context.CompatibilitySegmentType);
            Assert.Equal(1f / 3f, context.SegmentStartCornerProgress, 6);
            Assert.Equal(2f / 3f, context.SegmentEndCornerProgress, 6);
        });
    }

    [Fact]
    public void LegacyResolutionRetainsItsResultAndDoesNotInjectCornerContext()
    {
        var track = Track.CreateExample();
        var rider = Rider(1, 2);
        rider.Speed = 15f;
        rider.RestorePosition(RiderPosition.Create(1, 1, 0f, track.Segments.Count));
        var engine = new SimulationEngine(new HoldLane());
        var options = Options();
        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track),
            new[] { rider },
            new SimulationStepContext(37, 1, 0, 1, 3700, 1, UseLegacyPhysics: true));

        var resolved = engine.Resolve(snapshot, engine.Decide(snapshot), options);
        var change = Assert.Single(resolved.Changes);
        var expected = SegmentPhysics.Apply(track.Segments[1], 2, 15f, track.Geometry);

        Assert.Null(Assert.Single(resolved.Diagnostics).CornerPhaseContext);
        Assert.Equal(expected.Outcome, change.Outcome);
        Assert.Equal(expected.Lane, change.Lane);
        Assert.Equal(expected.Speed, change.Speed);
    }

    [Fact]
    public void SegmentPhysicsRejectsMismatchedSuppliedCornerContext()
    {
        var track = Track.CreateExample();
        var context = Resolve(track, 1, 0f, 1f);

        Assert.Throws<ArgumentException>(() => SegmentPhysics.Apply(new SegmentPhysicsContext(
            track.Segments[0],
            1,
            15f,
            track.Geometry,
            new TrackSurfaceState(1f, 0f, 0.35f),
            RiderSkills.Balanced,
            0.5f,
            BikeSetup.Neutral,
            LateralPosition: 1f,
            CornerPhase: context)));
    }

    [Fact]
    public void CompatibilityPhaseProfilesReceiveContextWithoutChangingNumericalResults()
    {
        var track = Track.CreateExample();
        var entry = Resolve(track, 0, 0f, 1f);
        var exit = Resolve(track, 2, 0f, 1f);
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var entryLength = LaneModel.SegmentLengthMeters(
            track.Segments[0],
            1f,
            track.Geometry);
        var exitLength = LaneModel.SegmentLengthMeters(
            track.Segments[2],
            1f,
            track.Geometry);

        Assert.Equal(
            LongitudinalDynamics.CalculateTurnEntryScrubProfile(
                19f,
                16f,
                2.6f,
                entryLength),
            LongitudinalDynamics.CalculateTurnEntryScrubProfile(
                entry,
                19f,
                16f,
                2.6f,
                entryLength));
        Assert.Equal(
            LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
                16f,
                RiderSkills.Balanced,
                BikeSetup.Neutral,
                surface,
                exitLength),
            LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
                exit,
                16f,
                RiderSkills.Balanced,
                BikeSetup.Neutral,
                surface,
                exitLength));
        Assert.Throws<ArgumentException>(() =>
            LongitudinalDynamics.CalculateTurnEntryScrubProfile(
                exit,
                19f,
                16f,
                2.6f,
                entryLength));
        Assert.Throws<ArgumentException>(() =>
            LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
                entry,
                16f,
                RiderSkills.Balanced,
                BikeSetup.Neutral,
                surface,
                exitLength));
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidLocalProgressIsRejected(float progress)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            Track.CreateExample().CornerTopology.Resolve(
                0,
                progress,
                1f,
                Track.CreateExample().Geometry));

    private static ResolvedSimulationStep ResolveAdvanced(IEnumerable<int> order)
    {
        var track = Track.CreateExample();
        var riders = order.Select(id =>
        {
            var rider = Rider(id, id);
            rider.Speed = 15f;
            rider.RestorePosition(RiderPosition.Create(1, 1, 0.25f, track.Segments.Count));
            return rider;
        }).ToArray();
        var engine = new SimulationEngine(new HoldLane());
        var options = Options();
        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track),
            riders,
            new SimulationStepContext(37, 1, 0, 1, 3700, 1));
        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    private static RiderState Rider(int id, int lane)
        => new(
            new RiderProfile(
                id,
                $"Rider {id}",
                RiderSkills.Balanced,
                RiderStyle.Balanced),
            lane)
        {
            ActiveSetup = BikeSetup.Neutral,
            LateralPosition = lane,
        };

    private static HeatSimulationOptions Options()
        => new()
        {
            Laps = 1,
            Seed = 3700,
            IncidentFrequency = 0f,
            EnableLogging = false,
            Weather = WeatherState.Dry,
        };

    private static CornerPhaseContext Resolve(
        Track track,
        int segmentIndex,
        float localProgress,
        float lateralPosition)
        => Assert.IsType<CornerPhaseContext>(
            track.CornerTopology.Resolve(
                segmentIndex,
                localProgress,
                lateralPosition,
                track.Geometry));

    private static void AssertCorners(
        Track track,
        params (int Id, int Start, int End)[] expected)
    {
        Assert.Equal(expected.Length, track.CornerTopology.Corners.Count);
        foreach (var (actual, item) in track.CornerTopology.Corners.Zip(expected))
        {
            Assert.Equal(item.Id, actual.CornerId);
            Assert.Equal(item.Start, actual.StartSegmentIndex);
            Assert.Equal(item.End, actual.EndSegmentIndex);
            Assert.Equal(item.End - item.Start + 1, actual.SegmentCount);
        }
    }
}
