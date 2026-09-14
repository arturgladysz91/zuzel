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
    public void NaturalTurnExitDecelerationAndEquilibriumAreExportedFromProduction()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnExit) },
            new TrackGeometry(60f, 200f, 1f, 0.3f));
        var rider = Rider(1, 0, 0f);
        var force = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
            rider.Profile.Skills, rider.ActiveSetup, PerfectSurface);
        var equilibrium = LongitudinalDynamics.CalculateFullDriveEquilibriumSpeedMetersPerSecond(force, rider.ActiveSetup);
        rider.Speed = equilibrium + 5f;
        var trace = CalibrationRunner.RunHeat(track, TrackState.CreateDefault(track, PerfectSurface),
            new List<RiderState> { rider }, new HoldLaneDecisionModel(),
            Options(laps: 1) with { Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f) }, heatId: 17);
        var sample = Assert.Single(trace.StepSamples);
        Assert.Equal(equilibrium, sample.FullDriveEquilibriumSpeedMetersPerSecond);
        var p = sample.ContinuousCornerProfile!;
        Assert.True(p.Nodes[^1].NetDriveAccelerationMetersPerSecondSquared < 0f);
        Assert.True(p.CarryDistanceMeters > 0f);
        Assert.Equal(p.DriveDistanceMeters, p.DecelerationDistanceMeters, 4);
        Assert.Equal(sample.TravelledMeters, p.CarryDistanceMeters + p.DriveDistanceMeters, 4);
        var rows = CalibrationCsvExporter.ExportSteps(trace).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = rows[0].Split(',');
        var values = rows[1].Split(',');
        Assert.Equal(p.DecelerationDistanceMeters.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            values[Array.IndexOf(header, "ContinuousCornerDecelerationDistanceMeters")]);
        Assert.Equal(equilibrium.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            values[Array.IndexOf(header, "FullDriveEquilibriumSpeedMetersPerSecond")]);
    }

    [Fact]
    public void TurnExitDiagnosticsExposeActualProductionDriveProfile()
    {
        var resolved = ResolveSingle(SegmentType.TurnExit, Rider(1, 1, 12f));
        var expected = CornerTestSupport.Expected(resolved);
        Assert.Equal(expected, resolved.Diagnostics[0].ContinuousCornerProfile);
        Assert.Equal(expected.ExitSpeedMetersPerSecond, resolved.Changes[0].Speed);
    }

    [Fact]
    public void TurnExitDiagnosticsEntryAccelerationMatchesProfileStart()
    {
        var d = ResolveSingle(SegmentType.TurnExit, Rider(1, 1, 12f)).Diagnostics[0];
        Assert.Equal(0f, d.ContinuousCornerProfile!.Nodes[0].NetDriveAccelerationMetersPerSecondSquared);
        Assert.True(d.ContinuousCornerProfile.Nodes[^1].NetDriveAccelerationMetersPerSecondSquared > 0f);
        Assert.Null(d.TurnExitNetAccelerationMetersPerSecondSquared);
    }

    [Fact]
    public void TurnExitDiagnosticsPeakUsesProfilePeak()
    {
        var diagnostics = Assert.Single(
            ResolveSingle(SegmentType.TurnExit, Rider(1, 1, 12f)).Diagnostics);
        Assert.Equal(
            diagnostics.ContinuousCornerProfile!.PeakSpeedMetersPerSecond,
            diagnostics.PeakSpeedMetersPerSecond);
    }

    [Fact]
    public void TurnExitDiagnosticsProfileTimeMatchesResolvedTravelTime()
    {
        var diagnostics = Assert.Single(
            ResolveSingle(SegmentType.TurnExit, Rider(1, 1, 12f)).Diagnostics);
        Assert.Equal(
            diagnostics.ContinuousCornerProfile!.TravelTimeSeconds,
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
    public void CalibrationSampleExportsContinuousProfilePhases()
    {
        var sample = Assert.Single(RunCalibration(SingleTrack(SegmentType.TurnExit), new List<RiderState> { Rider(1, 1, 12f) }).StepSamples);
        var p = Assert.IsType<ContinuousCornerTraversalProfile>(sample.ContinuousCornerProfile);
        Assert.Equal(sample.TravelledMeters, p.CorrectionDistanceMeters + p.CarryDistanceMeters + p.DriveDistanceMeters, 4);
        Assert.Equal(sample.DurationSeconds, p.TravelTimeSeconds, 5);
        Assert.Null(sample.TurnExitAccelerationDistanceMeters);
    }

    [Fact]
    public void CalibrationCsvExportsContinuousProfileFields()
    {
        var trace = RunCalibration(SingleTrack(SegmentType.TurnExit), new List<RiderState> { Rider(1, 1, 12f) });
        var lines = CalibrationCsvExporter.ExportSteps(trace).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = lines[0].Split(',');
        var row = lines[1].Split(',');
        var p = trace.StepSamples[0].ContinuousCornerProfile!;
        Assert.Equal(p.TravelTimeSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            row[Array.IndexOf(header, "ContinuousCornerTravelTimeSeconds")]);
        Assert.Equal(p.DriveDistanceMeters.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            row[Array.IndexOf(header, "ContinuousCornerDriveDistanceMeters")]);
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
                     "TurnExitDecelerationDistanceMeters",
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
