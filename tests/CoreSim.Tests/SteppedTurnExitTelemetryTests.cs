using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class SteppedTurnExitTelemetryTests
{
    private static readonly TrackSurfaceState PerfectSurface = new(1f, 0f, 0.35f);

    [Fact]
    public void TurnExitDiagnosticsExposeActualProductionDriveProfile()
    {
        var resolved = ResolveSingle(SegmentType.TurnExit, Rider(1, 1, 12f));
        var change = Assert.Single(resolved.Changes);
        var diagnostics = Assert.Single(resolved.Diagnostics);
        var expected = LongitudinalDynamics.CalculateForceBasedTurnExitDriveProfile(
            change.PhysicsSpeed,
            resolved.Snapshot.Rider(1).Profile.Skills,
            resolved.Snapshot.Rider(1).ActiveSetup,
            diagnostics.EntrySurface,
            diagnostics.TravelledMeters,
            diagnostics.AttainableTopSpeedMetersPerSecond!.Value);

        Assert.Equal(expected, diagnostics.TurnExitDriveProfile);
        Assert.Equal(expected.ExitSpeedMetersPerSecond, change.Speed);
    }

    [Fact]
    public void TurnExitDiagnosticsEntryAccelerationMatchesProfileStart()
    {
        var diagnostics = Assert.Single(
            ResolveSingle(SegmentType.TurnExit, Rider(1, 1, 12f)).Diagnostics);
        var profile = Assert.IsType<TurnExitDriveProfile>(diagnostics.TurnExitDriveProfile);
        Assert.Equal(
            profile.EntryNetAccelerationMetersPerSecondSquared,
            diagnostics.TurnExitNetAccelerationMetersPerSecondSquared);
    }

    [Fact]
    public void TurnExitDiagnosticsPeakUsesProfilePeak()
    {
        var diagnostics = Assert.Single(
            ResolveSingle(SegmentType.TurnExit, Rider(1, 1, 12f)).Diagnostics);
        Assert.Equal(
            diagnostics.TurnExitDriveProfile!.Value.PeakSpeedMetersPerSecond,
            diagnostics.PeakSpeedMetersPerSecond);
    }

    [Fact]
    public void TurnExitDiagnosticsProfileTimeMatchesResolvedTravelTime()
    {
        var diagnostics = Assert.Single(
            ResolveSingle(SegmentType.TurnExit, Rider(1, 1, 12f)).Diagnostics);
        Assert.Equal(
            diagnostics.TurnExitDriveProfile!.Value.TravelTimeSeconds,
            diagnostics.TravelTimeSeconds);
    }

    [Fact]
    public void NonEligibleTurnExitHasNullDriveProfile()
    {
        var rider = Rider(1, 1, 0f);
        rider.Speed = SegmentPhysics.MaxSafeTurnSpeed(
            1f,
            TrackGeometry.Default,
            PerfectSurface,
            rider.Profile.Skills,
            rider.ActiveSetup) * 1.20f;
        Assert.Null(Assert.Single(ResolveSingle(SegmentType.TurnExit, rider).Diagnostics).TurnExitDriveProfile);
    }

    [Fact]
    public void CalibrationSampleExportsTurnExitProfilePhases()
    {
        var sample = Assert.Single(RunCalibration(SingleTrack(SegmentType.TurnExit),
            new List<RiderState> { Rider(1, 1, 12f) }).StepSamples);
        Assert.NotNull(sample.TurnExitAccelerationDistanceMeters);
        Assert.NotNull(sample.TurnExitCruiseDistanceMeters);
        Assert.NotNull(sample.TurnExitProfileTravelTimeSeconds);
        Assert.Equal(
            sample.TravelledMeters,
            sample.TurnExitAccelerationDistanceMeters + sample.TurnExitCruiseDistanceMeters);
    }

    [Fact]
    public void CalibrationCsvExportsTurnExitProfileFields()
    {
        var trace = RunCalibration(
            SingleTrack(SegmentType.TurnExit),
            new List<RiderState> { Rider(1, 1, 12f) });
        var csv = CalibrationCsvExporter.ExportSteps(trace);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("TurnExitAccelerationDistanceMeters", lines[0]);
        Assert.Contains("TurnExitCruiseDistanceMeters", lines[0]);
        Assert.Contains("TurnExitProfileTravelTimeSeconds", lines[0]);
        Assert.Contains(trace.StepSamples[0].TurnExitProfileTravelTimeSeconds!.Value.ToString(
            "R", System.Globalization.CultureInfo.InvariantCulture), lines[1]);
    }

    [Fact]
    public void CalibrationCsvLeavesTurnExitFieldsEmptyForNonTurnExit()
    {
        var trace = RunCalibration(
            SingleTrack(SegmentType.TurnMiddle),
            new List<RiderState> { Rider(1, 1, 12f) });
        var lines = CalibrationCsvExporter.ExportSteps(trace)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = lines[0].Split(',');
        var row = lines[1].Split(',');

        foreach (var name in new[]
                 {
                     "TurnExitAccelerationDistanceMeters",
                     "TurnExitCruiseDistanceMeters",
                     "TurnExitProfileTravelTimeSeconds",
                 })
        {
            Assert.Equal(string.Empty, row[Array.IndexOf(header, name)]);
        }
    }

    [Fact]
    public void HarnessStillDoesNotChangeHeatResult()
    {
        var direct = RunProduction(withCollector: false);
        var observed = RunProduction(withCollector: true);
        Assert.Equal(direct.Classification, observed.Classification);
        Assert.Equal(direct.Riders, observed.Riders);
    }

    [Fact]
    public void HarnessStillDoesNotChangeTrackState()
    {
        var direct = RunProduction(withCollector: false);
        var observed = RunProduction(withCollector: true);
        Assert.Equal(direct.Surfaces, observed.Surfaces);
    }

    [Fact]
    public void HarnessStillWorksWithLoggingDisabled()
    {
        var options = Options() with { EnableLogging = false };
        var trace = CalibrationRunner.RunHeat(
            Track.CreateExample(),
            TrackState.CreateDefault(Track.CreateExample()),
            Riders(),
            new HoldLaneDecisionModel(),
            options,
            heatId: 17);
        Assert.NotEmpty(trace.StepSamples);
        Assert.Equal(4, trace.RiderSummaries.Count);
    }

    [Fact]
    public void CalibrationTraceRemainsDeterministic()
    {
        var first = RunCalibration(Track.CreateExample(), Riders());
        var second = RunCalibration(Track.CreateExample(), Riders());
        Assert.Equal(first.StepSamples, second.StepSamples);
        Assert.Equal(first.RiderSummaries, second.RiderSummaries);
    }

    [Fact]
    public void CalibrationTraceRemainsIndependentOfRiderInputOrder()
    {
        var forward = RunCalibration(Track.CreateExample(), Riders());
        var reversed = RunCalibration(Track.CreateExample(), Riders().AsEnumerable().Reverse().ToList());
        Assert.Equal(forward.StepSamples, reversed.StepSamples);
        Assert.Equal(forward.RiderSummaries, reversed.RiderSummaries);
    }

    private static ResolvedSimulationStep ResolveSingle(SegmentType type, RiderState rider)
    {
        var track = SingleTrack(type);
        var state = TrackState.CreateDefault(track, PerfectSurface);
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var options = Options(laps: 1);
        var snapshot = engine.CaptureSnapshot(
            track,
            state,
            new[] { rider },
            new SimulationStepContext(17, 0, 0, 0, options.Seed, 1));
        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    private static CalibrationTrace RunCalibration(Track track, List<RiderState> riders)
        => CalibrationRunner.RunHeat(
            track,
            TrackState.CreateDefault(track, PerfectSurface),
            riders,
            new HoldLaneDecisionModel(),
            Options(laps: 1),
            heatId: 17);

    private static ProductionProjection RunProduction(bool withCollector)
    {
        var track = Track.CreateExample();
        var state = TrackState.CreateDefault(track, PerfectSurface);
        var riders = Riders();
        var options = Options();
        var collector = withCollector ? new CalibrationTraceCollector(track, options, 17) : null;
        var result = new HeatSimulator(new HoldLaneDecisionModel()).SimulateHeat(
            track, state, riders, options, 17, collector);
        _ = collector?.Complete(result);
        return new ProductionProjection(
            result.Classification.Select(item =>
                (item.RiderId, item.Position, item.Status, item.TimeSeconds, item.DistanceMeters)).ToArray(),
            riders.OrderBy(item => item.RiderId).Select(item =>
                (item.RiderId, item.Speed, item.ElapsedTimeSeconds, item.DistanceMeters, item.Status)).ToArray(),
            Enumerable.Range(0, state.SegmentCount)
                .SelectMany(segment => Enumerable.Range(0, state.LinesCount)
                    .Select(lane => state.GetSurface(segment, lane)))
                .ToArray());
    }

    private static HeatSimulationOptions Options(int laps = 2)
        => new()
        {
            Laps = laps,
            Seed = 90210,
            Weather = WeatherState.Dry,
            IncidentFrequency = 0f,
            EnableLogging = true,
        };

    private static Track SingleTrack(SegmentType type)
        => new(new[] { new TrackSegment(101, type) });

    private static List<RiderState> Riders()
        => Enumerable.Range(1, 4).Select(id => Rider(id, id - 1, 10f + id)).ToList();

    private static RiderState Rider(int id, int lane, float speed)
        => new(id, lane) { Speed = speed };

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane, 0f);
    }

    private sealed record ProductionProjection(
        (int RiderId, int Position, RiderRaceStatus Status, float Time, float Distance)[] Classification,
        (int RiderId, float Speed, float Time, float Distance, RiderRaceStatus Status)[] Riders,
        TrackSurfaceState[] Surfaces);
}
