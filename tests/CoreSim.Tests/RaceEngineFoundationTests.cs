using CoreSim;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class RaceEngineFoundationTests
{
    private static readonly WeatherState NeutralWeather =
        new(WeatherCondition.Cloudy, 0f, 0f);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }

    private sealed class MutatingLegacyDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
        {
            rider.Lane = LaneModel.MaxLane;
            rider.Speed = 999f;
            return new RiderDecision(LaneModel.MaxLane);
        }
    }

    private sealed class SnapshotRecordingDecisionModel : IRiderDecisionModel
    {
        public List<SimulationSnapshot> SeenSnapshots { get; } = new();

        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => throw new InvalidOperationException("The pipeline should use the context overload.");

        public RiderDecision Decide(RiderDecisionContext context)
        {
            SeenSnapshots.Add(context.Snapshot);
            return new RiderDecision(context.Rider.Lane);
        }
    }

    private sealed record RiderProjection(
        int RiderId,
        RiderRaceStatus Status,
        double CanonicalProgress,
        float DistanceMeters,
        int Lane,
        float LateralPosition,
        float Speed,
        float ElapsedTimeSeconds,
        float Morale);

    [Fact]
    public void SameSeedAndInitialStateProduceIdenticalResultAndLog()
    {
        var first = RunHeat(CreateDistinctRiders());
        var second = RunHeat(CreateDistinctRiders());

        Assert.Equal(first.Result.Classification, second.Result.Classification);
        Assert.Equal(first.Result.Log.Lines, second.Result.Log.Lines);
        Assert.Equal(first.Result.Log.SurfaceChanges, second.Result.Log.SurfaceChanges);
        Assert.Equal(first.Result.Log.Overtakes, second.Result.Log.Overtakes);
        Assert.Equal(first.Riders, second.Riders);
    }

    [Fact]
    public void AllTwentyFourRiderPermutationsProduceTheSamePerRiderOutcome()
    {
        (HeatResult Result, RiderProjection[] Riders)? baseline = null;

        foreach (var permutation in Permutations(new[] { 0, 1, 2, 3 }))
        {
            var riders = CreateDistinctRiders();
            var permuted = permutation.Select(index => riders[index]).ToList();
            var current = RunHeat(permuted);

            if (baseline is null)
            {
                baseline = current;
                continue;
            }

            Assert.Equal(baseline.Value.Result.Classification, current.Result.Classification);
            Assert.Equal(baseline.Value.Result.Log.Lines, current.Result.Log.Lines);
            Assert.Equal(baseline.Value.Result.Log.SurfaceChanges, current.Result.Log.SurfaceChanges);
            Assert.Equal(baseline.Value.Result.Log.Overtakes, current.Result.Log.Overtakes);
            Assert.Equal(baseline.Value.Riders, current.Riders);
        }
    }

    [Fact]
    public void ReorderingRidersDoesNotReassignDecisionRandomness()
    {
        var track = Track.CreateExample();
        var options = Options();
        var firstRiders = CreateDistinctRiders();
        var secondRiders = CreateDistinctRiders();
        secondRiders.Reverse();
        var firstEngine = new SimulationEngine(new SimpleDecisionModel(seed: 123));
        var secondEngine = new SimulationEngine(new SimpleDecisionModel(seed: 123));
        var step = new SimulationStepContext(7, 0, 0, 0, options.Seed, options.Laps);

        var first = firstEngine.Decide(firstEngine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track),
            firstRiders,
            step));
        var second = secondEngine.Decide(secondEngine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track),
            secondRiders,
            step));

        Assert.Equal(
            first.OrderBy(intent => intent.RiderId),
            second.OrderBy(intent => intent.RiderId));
    }

    [Fact]
    public void DecideCannotModifySourceRiderState()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var rider = new RiderState(1, lane: 1) { Speed = 12f };
        var engine = new SimulationEngine(new MutatingLegacyDecisionModel());
        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track),
            new[] { rider },
            new SimulationStepContext(1, 0, 0, 0, 11, 1));

        var intents = engine.Decide(snapshot);

        Assert.Single(intents);
        Assert.Equal(1, rider.Lane);
        Assert.Equal(12f, rider.Speed);
        Assert.Equal(RiderRaceStatus.NotStarted, rider.Status);
    }

    [Fact]
    public void EveryRiderDecisionReceivesTheExactSameSnapshot()
    {
        var track = Track.CreateExample();
        var riders = CreateDistinctRiders();
        var model = new SnapshotRecordingDecisionModel();
        var engine = new SimulationEngine(model);
        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track),
            riders,
            new SimulationStepContext(2, 0, 0, 0, 22, 4));

        engine.Decide(snapshot);

        Assert.Equal(4, model.SeenSnapshots.Count);
        Assert.All(model.SeenSnapshots, seen => Assert.Same(snapshot, seen));
    }

    [Fact]
    public void RiderAndTrackChangesAreDeferredUntilCommit()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var trackState = TrackState.CreateDefault(track);
        var rider = new RiderState(1, lane: 2) { Speed = 14f };
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 8,
            Weather = NeutralWeather,
            IncidentFrequency = 0f,
        };
        var snapshot = engine.CaptureSnapshot(
            track,
            trackState,
            new[] { rider },
            new SimulationStepContext(3, 0, 0, 0, options.Seed, 1));
        var surfaceBefore = trackState.GetSurface(0, 2);

        var intents = engine.Decide(snapshot);
        var resolved = engine.Resolve(snapshot, intents, options);

        Assert.Equal(RiderRaceStatus.NotStarted, rider.Status);
        Assert.Equal(0d, rider.CanonicalProgress);
        Assert.Equal(surfaceBefore, trackState.GetSurface(0, 2));

        engine.Commit(resolved, new[] { rider }, trackState, new SimLog());

        Assert.Equal(RiderRaceStatus.Finished, rider.Status);
        Assert.Equal(1d, rider.CanonicalProgress);
        Assert.NotEqual(surfaceBefore, trackState.GetSurface(0, 2));
    }

    [Fact]
    public void CommitRejectsMismatchedTrackStateBeforeMutatingRidersLogOrTrack()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight) });
        var sourceTrackState = TrackState.CreateDefault(track);
        var wrongTrackState = new TrackState(2, LaneModel.LanesCount);
        var rider = new RiderState(1, lane: 2) { Speed = 14f };
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = 8,
            Weather = NeutralWeather,
            IncidentFrequency = 0f,
        };
        var snapshot = engine.CaptureSnapshot(
            track,
            sourceTrackState,
            new[] { rider },
            new SimulationStepContext(3, 0, 0, 0, options.Seed, 1));
        var resolved = engine.Resolve(snapshot, engine.Decide(snapshot), options);
        var riderBefore = new RiderProjection(
            rider.RiderId,
            rider.Status,
            rider.CanonicalProgress,
            rider.DistanceMeters,
            rider.Lane,
            rider.LateralPosition,
            rider.Speed,
            rider.ElapsedTimeSeconds,
            rider.Morale);
        var surfacesBefore = Enumerable.Range(0, wrongTrackState.SegmentCount)
            .SelectMany(segment => Enumerable.Range(0, wrongTrackState.LinesCount)
                .Select(lane => wrongTrackState.GetSurface(segment, lane)))
            .ToArray();
        var log = new SimLog();
        log.Add("existing");

        var exception = Assert.Throws<ArgumentException>(
            () => engine.Commit(resolved, new[] { rider }, wrongTrackState, log));

        Assert.Equal("trackState", exception.ParamName);
        Assert.Equal(riderBefore, new RiderProjection(
            rider.RiderId,
            rider.Status,
            rider.CanonicalProgress,
            rider.DistanceMeters,
            rider.Lane,
            rider.LateralPosition,
            rider.Speed,
            rider.ElapsedTimeSeconds,
            rider.Morale));
        Assert.Equal(new[] { "existing" }, log.Lines);
        Assert.Empty(log.SurfaceChanges);
        Assert.Empty(log.Overtakes);
        Assert.Empty(log.OrderSnapshots);
        Assert.Equal(surfacesBefore, Enumerable.Range(0, wrongTrackState.SegmentCount)
            .SelectMany(segment => Enumerable.Range(0, wrongTrackState.LinesCount)
                .Select(lane => wrongTrackState.GetSurface(segment, lane)))
            .ToArray());
    }

    [Fact]
    public void SnapshotIsDetachedFromLaterSourceMutations()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnMiddle) });
        var trackState = TrackState.CreateDefault(track);
        var rider = new RiderState(1, lane: 1) { Speed = 13f };
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var snapshot = engine.CaptureSnapshot(
            track,
            trackState,
            new[] { rider },
            new SimulationStepContext(4, 0, 0, 0, 44, 1));
        var capturedSurface = snapshot.TrackState.GetSurface(0, 1);

        rider.Lane = 4;
        rider.Speed = 99f;
        trackState.ApplySurfaceDelta(0, 1, -0.5f, 0.5f, 0f, "test", 4, 0, new SimLog());

        Assert.Equal(1, snapshot.Rider(1).Lane);
        Assert.Equal(13f, snapshot.Rider(1).Speed);
        Assert.Equal(capturedSurface, snapshot.TrackState.GetSurface(0, 1));
        Assert.NotEqual(trackState.GetSurface(0, 1), snapshot.TrackState.GetSurface(0, 1));
    }

    [Fact]
    public void PositionCrossesSegmentsLapsAndFinishWithConsistentDerivedValues()
    {
        var track = new Track(new[]
        {
            new TrackSegment(10, SegmentType.Straight),
            new TrackSegment(20, SegmentType.Straight),
        });
        var trackState = TrackState.CreateDefault(track);
        var rider = new RiderState(1, lane: 1) { Speed = 15f };
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var options = new HeatSimulationOptions
        {
            Laps = 2,
            Seed = 5,
            Weather = NeutralWeather,
            IncidentFrequency = 0f,
        };

        RunStep(engine, track, trackState, rider, options, stepNumber: 0, lapIndex: 0, segmentIndex: 0);
        Assert.Equal(1, rider.LapNumber);
        Assert.Equal(1, rider.SegmentIndex);
        Assert.Equal(0f, rider.SegmentProgress);
        Assert.Equal(0, rider.LapsCompleted);

        RunStep(engine, track, trackState, rider, options, stepNumber: 1, lapIndex: 0, segmentIndex: 1);
        Assert.Equal(2, rider.LapNumber);
        Assert.Equal(0, rider.SegmentIndex);
        Assert.Equal(1, rider.LapsCompleted);

        RunStep(engine, track, trackState, rider, options, stepNumber: 2, lapIndex: 1, segmentIndex: 0);
        Assert.Equal(1, rider.SegmentIndex);
        Assert.Equal(RiderRaceStatus.Racing, rider.Status);

        RunStep(engine, track, trackState, rider, options, stepNumber: 3, lapIndex: 1, segmentIndex: 1);
        Assert.Equal(RiderRaceStatus.Finished, rider.Status);
        Assert.Equal(2, rider.LapNumber);
        Assert.Equal(2, rider.LapsCompleted);
        Assert.Equal(0, rider.SegmentIndex);
        Assert.Equal(0f, rider.SegmentProgress);
        Assert.Equal(4d, rider.CanonicalProgress);
    }

    [Fact]
    public void ClassificationUsesCanonicalTrackProgressInsteadOfInputOrderOrTime()
    {
        var ahead = new RiderState(1, lane: 1) { ElapsedTimeSeconds = 100f };
        var behind = new RiderState(2, lane: 1) { ElapsedTimeSeconds = 1f };
        ahead.RestorePosition(RiderPosition.Create(2, 1, 0.6f, 4, 350f));
        behind.RestorePosition(RiderPosition.Create(2, 1, 0.2f, 4, 340f));

        var first = RaceClassification.Build(new[] { behind, ahead }, requiredLaps: 4);
        var reversed = RaceClassification.Build(new[] { ahead, behind }, requiredLaps: 4);

        Assert.Equal(new[] { 1, 2 }, first.Select(result => result.RiderId));
        Assert.Equal(first, reversed);
    }

    [Fact]
    public void SameStepEventsAreOrderedByStableRiderIdentity()
    {
        var run = RunHeat(CreateDistinctRiders().AsEnumerable().Reverse().ToList());
        var firstStepRiderLines = run.Result.Log.Lines
            .Where(line => line.StartsWith("LAP=1 SEG=0 "))
            .Take(4)
            .ToArray();

        Assert.Equal(
            new[] { 11, 22, 33, 44 },
            firstStepRiderLines.Select(ParseRiderId));
    }

    private static void RunStep(
        SimulationEngine engine,
        Track track,
        TrackState trackState,
        RiderState rider,
        HeatSimulationOptions options,
        int stepNumber,
        int lapIndex,
        int segmentIndex)
    {
        var snapshot = engine.CaptureSnapshot(
            track,
            trackState,
            new[] { rider },
            new SimulationStepContext(9, stepNumber, lapIndex, segmentIndex, options.Seed, options.Laps));
        var intents = engine.Decide(snapshot);
        var resolved = engine.Resolve(snapshot, intents, options);
        engine.Commit(resolved, new[] { rider }, trackState, new SimLog());
    }

    private static (HeatResult Result, RiderProjection[] Riders) RunHeat(List<RiderState> riders)
    {
        var result = new HeatSimulator(new AdaptiveDecisionModel(seed: 91)).SimulateHeat(
            Track.CreateExample(),
            TrackState.CreateDefault(Track.CreateExample()),
            riders,
            Options(),
            heatId: 12);
        var projections = riders
            .OrderBy(rider => rider.RiderId)
            .Select(rider => new RiderProjection(
                rider.RiderId,
                rider.Status,
                rider.CanonicalProgress,
                rider.DistanceMeters,
                rider.Lane,
                rider.LateralPosition,
                rider.Speed,
                rider.ElapsedTimeSeconds,
                rider.Morale))
            .ToArray();
        return (result, projections);
    }

    private static HeatSimulationOptions Options() => new()
    {
        Laps = 4,
        Seed = 812,
        Weather = NeutralWeather,
        IncidentFrequency = 1f,
    };

    private static List<RiderState> CreateDistinctRiders() => new()
    {
        new(new RiderProfile(11, "A", new RiderSkills(82, 72, 66, 75, 62, 70), RiderStyle.Balanced), 0),
        new(new RiderProfile(22, "B", new RiderSkills(61, 84, 79, 58, 70, 74), new RiderStyle(0.75f, 0.7f, 0.7f, 0.5f)), 1),
        new(new RiderProfile(33, "C", new RiderSkills(75, 68, 72, 88, 80, 64), new RiderStyle(0.35f, 0.4f, 0.25f, 0.5f)), 2),
        new(new RiderProfile(44, "D", new RiderSkills(69, 76, 84, 71, 74, 82), RiderStyle.Balanced), 3),
    };

    private static IEnumerable<int[]> Permutations(int[] values)
    {
        if (values.Length == 1)
        {
            yield return values;
            yield break;
        }

        for (var index = 0; index < values.Length; index++)
        {
            var head = values[index];
            var tail = values.Where((_, tailIndex) => tailIndex != index).ToArray();
            foreach (var permutation in Permutations(tail))
                yield return new[] { head }.Concat(permutation).ToArray();
        }
    }

    private static int ParseRiderId(string line)
    {
        const string marker = "rider=";
        var start = line.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = line.IndexOf(' ', start);
        return int.Parse(line[start..end]);
    }
}
