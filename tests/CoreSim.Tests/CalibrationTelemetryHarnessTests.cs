using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using System.Globalization;
using Xunit;

namespace CoreSim.Tests;

public sealed class CalibrationTelemetryHarnessTests
{
    private static readonly WeatherState NeutralWeather = new(WeatherCondition.Cloudy, 0f, 0f);

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }

    private sealed class RecordingObserver(Action<ResolvedSimulationStep>? action = null)
        : ISimulationStepObserver
    {
        public List<ResolvedSimulationStep> Steps { get; } = new();

        public void OnStepResolved(ResolvedSimulationStep resolvedStep)
        {
            Steps.Add(resolvedStep);
            action?.Invoke(resolvedStep);
        }
    }

    [Fact]
    public void ObserverIsOptionalAndPreservesExistingApiBehavior()
    {
        var run = RunProduction(observer: null);
        Assert.All(run.Result.Classification, item => Assert.True(item.Finished));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
                StraightTrack(),
                TrackState.CreateDefault(StraightTrack()),
                CreateRiders(1),
                Options(laps: 1),
                heatId: 7,
                observer: new ThrowingObserver()));
        Assert.Equal("observer", exception.Message);
    }

    [Fact]
    public void ObserverReceivesExactlyOneResolvedStepPerSimulationStep()
    {
        var observer = new RecordingObserver();
        RunProduction(observer, Options(laps: 3));
        Assert.Equal(3 * Track.CreateExample().Segments.Count, observer.Steps.Count);
    }

    [Fact]
    public void ObserverReceivesResolvedStepBeforeCommitWithoutMutatingLiveState()
    {
        var track = StraightTrack();
        var rider = Assert.Single(CreateRiders(1));
        var observer = new RecordingObserver(step =>
        {
            Assert.Equal(RiderRaceStatus.NotStarted, rider.Status);
            Assert.Equal(0f, rider.DistanceMeters);
            Assert.Equal(0f, rider.ElapsedTimeSeconds);
            Assert.True(Assert.Single(step.Changes).Position.DistanceMeters > 0f);
        });

        new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track, TrackState.CreateDefault(track), new List<RiderState> { rider },
            Options(laps: 1), heatId: 8, observer);

        Assert.Equal(RiderRaceStatus.Finished, rider.Status);
    }

    [Fact]
    public void ObserverDoesNotChangeFinalHeatResult()
    {
        var without = RunProduction(observer: null);
        var with = RunProductionWithCalibrationCollector();
        Assert.Equal(without.Result.Classification, with.Result.Classification);
        Assert.Equal(without.Riders, with.Riders);
    }

    [Fact]
    public void ObserverDoesNotChangeFinalTrackState()
    {
        var without = RunProduction(observer: null);
        var with = RunProductionWithCalibrationCollector();
        Assert.Equal(without.Surfaces, with.Surfaces);
    }

    [Fact]
    public void ObserverDoesNotChangeTextLog()
    {
        var without = RunProduction(observer: null);
        var with = RunProductionWithCalibrationCollector();
        Assert.Equal(without.Result.Log.Lines, with.Result.Log.Lines);
        Assert.Equal(without.Result.Log.SurfaceChanges, with.Result.Log.SurfaceChanges);
        Assert.Equal(without.Result.Log.Overtakes, with.Result.Log.Overtakes);
        Assert.Equal(
            ProjectOrderSnapshots(without.Result.Log.OrderSnapshots),
            ProjectOrderSnapshots(with.Result.Log.OrderSnapshots));
    }

    [Fact]
    public void DiagnosticsExistForEveryResolvedRiderChange()
    {
        var resolved = ResolveStep(StraightTrack(), CreateRiders(4));
        Assert.Equal(resolved.Changes.Select(item => item.RiderId), resolved.Diagnostics.Select(item => item.RiderId));
    }

    [Fact]
    public void DiagnosticsAreOrderedByRiderId()
    {
        var riders = CreateRiders(4).AsEnumerable().Reverse().ToList();
        var resolved = ResolveStep(StraightTrack(), riders);
        Assert.Equal(new[] { 1, 2, 3, 4 }, resolved.Diagnostics.Select(item => item.RiderId));
    }

    [Fact]
    public void DiagnosticsTravelledDistanceMatchesCanonicalStateChange()
    {
        var resolved = ResolveStep(Track.CreateExample(), CreateRiders(2));
        Assert.All(resolved.Diagnostics, item =>
        {
            var before = resolved.Snapshot.Rider(item.RiderId).DistanceMeters;
            var after = resolved.Changes.Single(change => change.RiderId == item.RiderId).Position.DistanceMeters;
            Assert.Equal(after - before, item.TravelledMeters, 5);
        });
    }

    [Fact]
    public void DiagnosticsTravelTimeMatchesElapsedTimeDelta()
    {
        var resolved = ResolveStep(Track.CreateExample(), CreateRiders(2));
        Assert.All(resolved.Diagnostics, item =>
        {
            var before = resolved.Snapshot.Rider(item.RiderId).ElapsedTimeSeconds;
            var after = resolved.Changes.Single(change => change.RiderId == item.RiderId).ElapsedTimeSeconds;
            Assert.Equal(after - before, item.TravelTimeSeconds, 5);
        });
    }

    [Fact]
    public void StraightDiagnosticsExposeActualProductionProfile()
    {
        var rider = Assert.Single(CreateRiders(1));
        rider.Speed = 10f;
        var resolved = ResolveStep(StraightThenTurnEntryTrack(), new List<RiderState> { rider });
        var diagnostics = Assert.Single(resolved.Diagnostics);
        var change = Assert.Single(resolved.Changes);
        var profile = Assert.IsType<StraightSpeedProfile>(diagnostics.StraightProfile);
        Assert.Equal(profile.ExitSpeedMetersPerSecond, change.Speed);
        Assert.Equal(profile.TravelTimeSeconds, diagnostics.TravelTimeSeconds);
        Assert.Equal(profile.PeakSpeedMetersPerSecond, diagnostics.PeakSpeedMetersPerSecond);
    }

    [Fact]
    public void StraightDiagnosticsExposePeakAboveExitWhenAccelerateThenPrepareOccurs()
    {
        var rider = Assert.Single(CreateRiders(1));
        rider.Speed = 10f;
        var diagnostics = Assert.Single(ResolveStep(
            StraightThenTurnEntryTrack(), new List<RiderState> { rider }).Diagnostics);
        var profile = Assert.IsType<StraightSpeedProfile>(diagnostics.StraightProfile);
        Assert.True(profile.AccelerationDistanceMeters > 0f);
        Assert.True(profile.DecelerationDistanceMeters > 0f);
        Assert.True(diagnostics.PeakSpeedMetersPerSecond > profile.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void TurnExitDiagnosticsExposeExactAppliedNetAcceleration()
    {
        var track = SingleSegmentTrack(SegmentType.TurnExit);
        var rider = Assert.Single(CreateRiders(1));
        rider.Speed = 10f;
        var resolved = ResolveStep(track, new List<RiderState> { rider });
        var diagnostics = Assert.Single(resolved.Diagnostics);
        var snapshotRider = resolved.Snapshot.Rider(1);
        var expected = LongitudinalDynamics.CalculateTurnExitNetAccelerationMetersPerSecondSquared(
            Assert.Single(resolved.Changes).PhysicsSpeed,
            snapshotRider.Profile.Skills,
            snapshotRider.ActiveSetup,
            diagnostics.EntrySurface);
        Assert.Equal(expected, diagnostics.TurnExitNetAccelerationMetersPerSecondSquared);
    }

    [Fact]
    public void TurnExitDiagnosticsAreNullWhenPositiveDriveIsIneligible()
    {
        var track = SingleSegmentTrack(SegmentType.TurnExit);
        var rider = Assert.Single(CreateRiders(1));
        rider.Speed = 100f;
        var resolved = ResolveStep(track, new List<RiderState> { rider });
        Assert.Equal(SegmentOutcome.Crash, Assert.Single(resolved.Changes).Outcome);
        Assert.Null(Assert.Single(resolved.Diagnostics).TurnExitNetAccelerationMetersPerSecondSquared);
        Assert.Null(Assert.Single(resolved.Diagnostics).FullDriveEquilibriumSpeedMetersPerSecond);
    }

    [Fact]
    public void TurnEntryDiagnosticsExposeActualProductionScrubProfile()
    {
        var track = SingleSegmentTrack(SegmentType.TurnEntry);
        var rider = Assert.Single(CreateRiders(1));
        rider.Speed = 18f;
        var resolved = ResolveStep(track, new List<RiderState> { rider });
        var diagnostics = Assert.Single(resolved.Diagnostics);
        var profile = Assert.IsType<TurnEntryScrubProfile>(diagnostics.TurnEntryScrubProfile);
        Assert.True(profile.TravelTimeSeconds > 0f);
        Assert.Null(diagnostics.StraightProfile);
    }

    [Fact]
    public void TurnMiddleDoesNotInventStraightOrTurnExitDiagnostics()
    {
        var diagnostics = Assert.Single(ResolveStep(
            SingleSegmentTrack(SegmentType.TurnMiddle), CreateRiders(1)).Diagnostics);
        Assert.Null(diagnostics.StraightProfile);
        Assert.Null(diagnostics.TurnEntryScrubProfile);
        Assert.Null(diagnostics.TurnExitNetAccelerationMetersPerSecondSquared);
        Assert.Null(diagnostics.FullDriveEquilibriumSpeedMetersPerSecond);
    }

    [Fact]
    public void DiagnosticsDoNotRecomputeOrChangePhysicsResults()
    {
        var first = ResolveStep(Track.CreateExample(), CreateRiders(4));
        var second = ResolveStep(Track.CreateExample(), CreateRiders(4));
        Assert.Equal(first.Changes, second.Changes);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }

    [Fact]
    public void CollectorCreatesOneSamplePerResolvedRiderPerStep()
    {
        var trace = RunCalibration(StraightTrack(), CreateRiders(2), Options(laps: 3));
        Assert.Equal(6, trace.StepSamples.Count);
        Assert.Equal(Enumerable.Range(0, 3).SelectMany(step => new[] { (step, 1), (step, 2) }),
            trace.StepSamples.Select(item => (item.StepNumber, item.RiderId)));
    }

    [Fact]
    public void SampleUsesSnapshotStartTimeAndResolvedEndTime()
    {
        var trace = RunCalibration(StraightTrack(), CreateRiders(1), Options(laps: 2));
        Assert.Equal(0f, trace.StepSamples[0].StartTimeSeconds);
        Assert.Equal(trace.StepSamples[0].EndTimeSeconds, trace.StepSamples[1].StartTimeSeconds);
        Assert.Equal(
            trace.StepSamples[1].EndTimeSeconds - trace.StepSamples[1].StartTimeSeconds,
            trace.StepSamples[1].DurationSeconds);
    }

    [Fact]
    public void SampleDistanceDeltaMatchesDiagnosticsTravelledDistance()
    {
        var trace = RunCalibration(Track.CreateExample(), CreateRiders(2), Options(laps: 1));
        Assert.All(trace.StepSamples, item =>
            Assert.Equal(item.EndDistanceMeters - item.StartDistanceMeters, item.TravelledMeters, 4));
    }

    [Fact]
    public void SampleUsesEntrySurfaceFromImmutableEntryLateralPosition()
    {
        var track = StraightTrack();
        var state = TrackState.CreateDefault(track);
        state.ApplySurfaceDelta(0, 1, -0.2f, 0.1f, -0.1f, "test", 0, 0, new SimLog(false));
        state.ApplySurfaceDelta(0, 2, -0.4f, 0.3f, 0.1f, "test", 0, 0, new SimLog(false));
        var rider = Assert.Single(CreateRiders(1));
        rider.Lane = 1;
        rider.LateralPosition = 1.5f;
        var expected = state.Snapshot().SampleSurface(0, rider.LateralPosition);
        var trace = CalibrationRunner.RunHeat(
            track, state, new List<RiderState> { rider }, new HoldLaneDecisionModel(),
            Options(laps: 1, weather: NeutralWeather), 11);
        var sample = Assert.Single(trace.StepSamples);
        Assert.Equal(expected.Grip, sample.EntrySurfaceGrip);
        Assert.Equal(expected.Ruts, sample.EntrySurfaceRuts);
        Assert.Equal(expected.Moisture, sample.EntrySurfaceMoisture);
        Assert.Equal(expected.EffectiveGrip, sample.EntrySurfaceEffectiveGrip);
    }

    [Fact]
    public void SamplePreservesEntryPhysicsAndExitSpeedSeparately()
    {
        var rider = Assert.Single(CreateRiders(1));
        rider.Speed = 10f;
        var trace = RunCalibration(
            SingleSegmentTrack(SegmentType.TurnExit), new List<RiderState> { rider }, Options(laps: 1));
        var sample = Assert.Single(trace.StepSamples);
        Assert.Equal(10f, sample.EntrySpeedMetersPerSecond);
        Assert.Equal(10f, sample.PhysicsSpeedMetersPerSecond);
        Assert.True(sample.ExitSpeedMetersPerSecond > sample.PhysicsSpeedMetersPerSecond);
    }

    [Fact]
    public void SamplePeakUsesStraightProfilePeak()
    {
        var rider = Assert.Single(CreateRiders(1));
        rider.Speed = 10f;
        var trace = RunCalibration(
            StraightThenTurnEntryTrack(), new List<RiderState> { rider }, Options(laps: 1));
        var straight = trace.StepSamples.Single(item => item.SegmentType == SegmentType.Straight);
        Assert.NotNull(straight.StraightAccelerationDistanceMeters);
        Assert.NotNull(straight.StraightDecelerationDistanceMeters);
        Assert.True(straight.PeakSpeedMetersPerSecond > straight.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void LoggingDisabledStillProducesCompleteCalibrationTrace()
    {
        var options = Options(laps: 2) with { EnableLogging = false };
        var trace = RunCalibration(StraightTrack(), CreateRiders(2), options);
        Assert.Equal(4, trace.StepSamples.Count);
        Assert.Equal(4, trace.LapSummaries.Count);
        Assert.Equal(2, trace.RiderSummaries.Count);
    }

    [Fact]
    public void RiderSummaryReconcilesWithHeatClassification()
    {
        var trace = RunCalibration(Track.CreateExample(), CreateRiders(4), Options(laps: 2));
        foreach (var summary in trace.RiderSummaries)
        {
            var result = trace.Classification.Single(item => item.RiderId == summary.RiderId);
            Assert.Equal(result.Status, summary.Status);
            Assert.Equal(result.LapsCompleted, summary.LapsCompleted);
            Assert.Equal(result.TimeSeconds, summary.TotalTimeSeconds);
            Assert.Equal(result.DistanceMeters, summary.TotalDistanceMeters);
        }
    }

    [Fact]
    public void RiderSummaryMaxSpeedUsesPeakNotOnlyExitSpeed()
    {
        var rider = Assert.Single(CreateRiders(1));
        rider.Speed = 10f;
        var trace = RunCalibration(
            StraightThenTurnEntryTrack(), new List<RiderState> { rider }, Options(laps: 1));
        var summary = Assert.Single(trace.RiderSummaries);
        Assert.Equal(trace.StepSamples.Max(item => item.PeakSpeedMetersPerSecond), summary.MaxSpeedMetersPerSecond);
        Assert.True(summary.MaxSpeedMetersPerSecond > trace.StepSamples.Max(item => item.ExitSpeedMetersPerSecond));
    }

    [Fact]
    public void LapSummaryTimeReconcilesWithStepDurations()
    {
        var trace = RunCalibration(Track.CreateExample(), CreateRiders(2), Options(laps: 3));
        foreach (var rider in trace.RiderSummaries)
        {
            var lapTotal = trace.LapSummaries.Where(item => item.RiderId == rider.RiderId)
                .Sum(item => item.LapTimeSeconds);
            var stepTotal = trace.StepSamples.Where(item => item.RiderId == rider.RiderId)
                .Sum(item => item.DurationSeconds);
            Assert.Equal(stepTotal, lapTotal, 3);
        }
    }

    [Fact]
    public void LapSummaryDistanceReconcilesWithStepDistances()
    {
        var trace = RunCalibration(Track.CreateExample(), CreateRiders(2), Options(laps: 3));
        foreach (var rider in trace.RiderSummaries)
        {
            var lapTotal = trace.LapSummaries.Where(item => item.RiderId == rider.RiderId)
                .Sum(item => item.LapDistanceMeters);
            var stepTotal = trace.StepSamples.Where(item => item.RiderId == rider.RiderId)
                .Sum(item => item.TravelledMeters);
            Assert.Equal(stepTotal, lapTotal, 3);
        }
    }

    [Fact]
    public void LapSummaryDoesNotCreateFakeCompletedLapAfterCrash()
    {
        var track = new Track(new[]
        {
            new TrackSegment(10, SegmentType.TurnMiddle),
            new TrackSegment(20, SegmentType.Straight),
        });
        var rider = Assert.Single(CreateRiders(1));
        rider.Speed = 100f;
        var trace = RunCalibration(track, new List<RiderState> { rider }, Options(laps: 2));
        Assert.Equal(RiderRaceStatus.Crashed, Assert.Single(trace.RiderSummaries).Status);
        Assert.Empty(trace.LapSummaries);
    }

    [Fact]
    public void GenericLapCountIsNotHardcodedToFour()
    {
        var trace = RunCalibration(StraightTrack(), CreateRiders(1), Options(laps: 3));
        Assert.Equal(new[] { 1, 2, 3 }, trace.LapSummaries.Select(item => item.LapNumber));
    }

    [Fact]
    public void CalibrationTraceIsDeterministicForSameSeedAndInputs()
    {
        var first = RunCalibration(Track.CreateExample(), CreateRiders(4), Options(laps: 2));
        var second = RunCalibration(Track.CreateExample(), CreateRiders(4), Options(laps: 2));
        Assert.Equal(first.StepSamples, second.StepSamples);
        Assert.Equal(first.LapSummaries, second.LapSummaries);
        Assert.Equal(first.RiderSummaries, second.RiderSummaries);
        Assert.Equal(first.Classification, second.Classification);
    }

    [Fact]
    public void CalibrationTraceIsIndependentOfRiderCollectionOrder()
    {
        var first = RunCalibration(StraightTrack(), CreateRiders(4), Options(laps: 2));
        var second = RunCalibration(
            StraightTrack(), CreateRiders(4).AsEnumerable().Reverse().ToList(), Options(laps: 2));
        Assert.Equal(first.StepSamples, second.StepSamples);
        Assert.Equal(first.RiderSummaries, second.RiderSummaries);
    }

    [Fact]
    public void AllTwentyFourRiderPermutationsProduceSamePerRiderTrace()
    {
        var expected = CalibrationCsvExporter.ExportSteps(
            RunCalibration(StraightTrack(), CreateRiders(4), Options(laps: 1)));
        foreach (var permutation in Permutations(new[] { 1, 2, 3, 4 }))
        {
            var riders = permutation.Select(id => CreateRider(id)).ToList();
            var actual = CalibrationCsvExporter.ExportSteps(
                RunCalibration(StraightTrack(), riders, Options(laps: 1)));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void StepCsvUsesInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var csv = CalibrationCsvExporter.ExportSteps(
                RunCalibration(StraightTrack(), CreateRiders(1), Options(laps: 1)));
            Assert.Contains("0.5", csv);
            var rows = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var header = rows[0].Split(',');
            var values = rows[1].Split(',');
            Assert.Equal("0.5", values[Array.IndexOf(header, "Gearing")]);
            Assert.Equal("0.5", values[Array.IndexOf(header, "TractionBias")]);
            Assert.DoesNotContain('\r', csv);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void CsvUsesStableHeaderAndOrdering()
    {
        var trace = RunCalibration(
            StraightTrack(), CreateRiders(3).AsEnumerable().Reverse().ToList(), Options(laps: 2));
        var lines = CalibrationCsvExporter.ExportSteps(trace).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("HeatId,StepNumber,LapIndex,SegmentIndex,SegmentId,SegmentType,RiderId,", lines[0]);
        Assert.Equal(new[] { "0:1", "0:2", "0:3", "1:1", "1:2", "1:3" },
            lines.Skip(1).Select(line =>
            {
                var cells = line.Split(',');
                return $"{cells[1]}:{cells[6]}";
            }));
    }

    [Fact]
    public void CsvIsIdenticalAcrossEquivalentRuns()
    {
        var first = RunCalibration(Track.CreateExample(), CreateRiders(2), Options(laps: 2));
        var second = RunCalibration(Track.CreateExample(), CreateRiders(2), Options(laps: 2));
        Assert.Equal(CalibrationCsvExporter.ExportSteps(first), CalibrationCsvExporter.ExportSteps(second));
        Assert.Equal(CalibrationCsvExporter.ExportLaps(first), CalibrationCsvExporter.ExportLaps(second));
        Assert.Equal(CalibrationCsvExporter.ExportRiders(first), CalibrationCsvExporter.ExportRiders(second));
    }

    [Fact]
    public void NullableDiagnosticsExportAsEmptyFields()
    {
        var trace = RunCalibration(
            SingleSegmentTrack(SegmentType.TurnMiddle), CreateRiders(1), Options(laps: 1));
        var lines = CalibrationCsvExporter.ExportSteps(trace).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = lines[0].Split(',');
        var row = lines[1].Split(',');
        Assert.Equal(string.Empty, row[Array.IndexOf(header, "FullDriveEquilibriumSpeedMetersPerSecond")]);
        Assert.Equal(string.Empty, row[Array.IndexOf(header, "StraightProfileTravelTimeSeconds")]);
        Assert.Equal(string.Empty, row[Array.IndexOf(header, "TurnEntryScrubTravelTimeSeconds")]);
    }

    [Fact]
    public void CsvEscapesTextCorrectlyIfRequired()
    {
        Assert.Equal("plain", CalibrationCsvExporter.EscapeField("plain"));
        Assert.Equal("\"a,b\"", CalibrationCsvExporter.EscapeField("a,b"));
        Assert.Equal("\"a\"\"b\"", CalibrationCsvExporter.EscapeField("a\"b"));
        Assert.Equal("\"a\nb\"", CalibrationCsvExporter.EscapeField("a\nb"));
        Assert.Equal(string.Empty, CalibrationCsvExporter.EscapeField(null));
    }

    private sealed class ThrowingObserver : ISimulationStepObserver
    {
        public void OnStepResolved(ResolvedSimulationStep resolvedStep)
            => throw new InvalidOperationException("observer");
    }

    private static ResolvedSimulationStep ResolveStep(Track track, List<RiderState> riders)
    {
        var state = TrackState.CreateDefault(track);
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var options = Options(laps: 1);
        var snapshot = engine.CaptureSnapshot(
            track, state, riders,
            new SimulationStepContext(21, 0, 0, 0, options.Seed, options.Laps));
        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    private static CalibrationTrace RunCalibration(
        Track track,
        List<RiderState> riders,
        HeatSimulationOptions options)
        => CalibrationRunner.RunHeat(
            track,
            TrackState.CreateDefault(track),
            riders,
            new HoldLaneDecisionModel(),
            options,
            heatId: 17);

    private static ProductionRun RunProduction(
        ISimulationStepObserver? observer,
        HeatSimulationOptions? options = null)
    {
        var track = Track.CreateExample();
        var state = TrackState.CreateDefault(track);
        var riders = CreateRiders(4);
        var result = new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track, state, riders, options ?? Options(laps: 2), heatId: 13, observer);
        return new ProductionRun(
            result,
            riders.OrderBy(item => item.RiderId).Select(Project).ToArray(),
            CaptureSurfaces(state));
    }

    private static ProductionRun RunProductionWithCalibrationCollector()
    {
        var track = Track.CreateExample();
        var state = TrackState.CreateDefault(track);
        var riders = CreateRiders(4);
        var options = Options(laps: 2);
        var collector = new CalibrationTraceCollector(track, options, heatId: 13);
        var result = new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track, state, riders, options, heatId: 13, observer: collector);
        _ = collector.Complete(result);
        return new ProductionRun(
            result,
            riders.OrderBy(item => item.RiderId).Select(Project).ToArray(),
            CaptureSurfaces(state));
    }

    private static RiderProjection Project(RiderState rider)
        => new(
            rider.RiderId,
            rider.Status,
            rider.ElapsedTimeSeconds,
            rider.DistanceMeters,
            rider.Speed,
            rider.Lane,
            rider.LateralPosition,
            rider.Morale,
            rider.Risk,
            rider.LapsCompleted);

    private static TrackSurfaceState[] CaptureSurfaces(TrackState state)
        => Enumerable.Range(0, state.SegmentCount)
            .SelectMany(segment => Enumerable.Range(0, state.LinesCount)
                .Select(lane => state.GetSurface(segment, lane)))
            .ToArray();

    private static HeatSimulationOptions Options(
        int laps,
        WeatherState? weather = null)
        => new()
        {
            Laps = laps,
            Seed = 90210,
            Weather = weather ?? NeutralWeather,
            IncidentFrequency = 0f,
            EnableLogging = true,
        };

    private static Track StraightTrack()
        => SingleSegmentTrack(SegmentType.Straight);

    private static Track SingleSegmentTrack(SegmentType type)
        => new(new[] { new TrackSegment(101, type) });

    private static Track StraightThenTurnEntryTrack()
        => new(
            new[]
            {
                new TrackSegment(101, SegmentType.Straight),
                new TrackSegment(205, SegmentType.TurnEntry),
            },
            new TrackGeometry(
                straightLengthMeters: 100f,
                innerRadiusMeters: 24f,
                laneSpacingMeters: 1f,
                turnSegmentAngleRadians: MathF.PI / 3f));

    private static (int Lap, int SegmentId, int RiderId, int Position, float Gap, RiderRaceStatus Status)[]
        ProjectOrderSnapshots(IReadOnlyList<RaceOrderSnapshot> snapshots)
        => snapshots.SelectMany(snapshot => snapshot.Order.Select(item =>
            (snapshot.Lap, snapshot.SegmentId, item.RiderId, item.Position, item.GapSeconds, item.Status)))
            .ToArray();

    private static List<RiderState> CreateRiders(int count)
        => Enumerable.Range(1, count).Select(CreateRider).ToList();

    private static RiderState CreateRider(int id)
        => new(id, lane: id - 1)
        {
            Speed = 10f + id,
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
            var tail = values.Where((_, itemIndex) => itemIndex != index).ToArray();
            foreach (var permutation in Permutations(tail))
                yield return new[] { values[index] }.Concat(permutation).ToArray();
        }
    }

    private sealed record ProductionRun(
        HeatResult Result,
        RiderProjection[] Riders,
        TrackSurfaceState[] Surfaces);

    private sealed record RiderProjection(
        int RiderId,
        RiderRaceStatus Status,
        float Time,
        float Distance,
        float Speed,
        int Lane,
        float LateralPosition,
        float Morale,
        float Risk,
        int LapsCompleted);
}
