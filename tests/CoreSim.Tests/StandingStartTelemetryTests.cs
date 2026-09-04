using CoreSim;
using CoreSim.Analysis;
using CoreSim.Race;
using System.Globalization;
using Xunit;
using static CoreSim.Tests.StandingStartFixture;

namespace CoreSim.Tests;

public sealed class StandingStartTelemetryTests
{
    [Fact]
    public void StandingStartDiagnosticsExposeActualProductionLaunchProfile()
    {
        var step = Resolve();
        var d = Assert.Single(step.Diagnostics);
        var rider = step.Snapshot.Riders[0];
        var expected = LongitudinalDynamics.CalculateStandingStartLaunchProfile(rider.Profile.Skills,
            rider.ActiveSetup, d.EntrySurface, d.TravelledMeters, d.AttainableTopSpeedMetersPerSecond!.Value);
        Assert.Equal(expected, d.StandingStartLaunchProfile);
        Assert.Equal(expected.ExitSpeedMetersPerSecond, Assert.Single(step.Changes).Speed);
    }

    [Fact]
    public void StandingStartDiagnosticsPeakUsesProfilePeak()
    {
        var step = Resolve();
        Assert.Equal(Launch(step).PeakSpeedMetersPerSecond, Assert.Single(step.Diagnostics).PeakSpeedMetersPerSecond);
        Assert.True(Assert.Single(step.Diagnostics).PeakSpeedMetersPerSecond > Assert.Single(step.Changes).PhysicsSpeed);
    }

    [Fact]
    public void StandingStartDiagnosticsTravelTimeUsesProfileTotalTime()
    {
        var step = Resolve();
        Assert.Equal(Launch(step).TotalTimeSeconds, Assert.Single(step.Diagnostics).TravelTimeSeconds);
    }

    [Fact]
    public void StandingStartCalibrationSampleExportsReactionTime()
    {
        var sample = Run().Trace!.StepSamples[0];
        Assert.Equal(0.25f, sample.StandingStartReactionTimeSeconds);
    }

    [Fact]
    public void StandingStartCalibrationSampleExportsLaunchMovementTime()
    {
        var run = Run();
        var sample = run.Trace!.StepSamples[0];
        var profile = run.Steps[0].Diagnostics[0].StandingStartLaunchProfile!.Value;
        Assert.Equal(profile.MovementTimeSeconds, sample.StandingStartMovementTimeSeconds);
        Assert.Equal(profile.TotalTimeSeconds, sample.StandingStartProfileTotalTimeSeconds);
        Assert.Equal(profile.AccelerationDistanceMeters, sample.StandingStartAccelerationDistanceMeters);
        Assert.Equal(profile.CruiseDistanceMeters, sample.StandingStartCruiseDistanceMeters);
        Assert.Equal(profile.EntryNetAccelerationMetersPerSecondSquared, sample.StandingStartEntryNetAccelerationMetersPerSecondSquared);
    }

    [Fact]
    public void StandingStartCalibrationSampleExportsTimeTo70()
    {
        Assert.Null(Run().Trace!.StepSamples[0].StandingStartTimeTo70KphSeconds);
        var trace = ShortTrace(Resolve(StartTrack(100f)));
        Assert.NotNull(trace.StepSamples[0].StandingStartTimeTo70KphSeconds);
        Assert.Equal(Launch(Resolve(StartTrack(100f))).TimeTo70KphSeconds, trace.StepSamples[0].StandingStartTimeTo70KphSeconds);
    }

    [Fact]
    public void StandingStartCalibrationSampleExportsSpeedAtTwoSeconds()
    {
        var run = Run();
        var profile = run.Steps[0].Diagnostics[0].StandingStartLaunchProfile!.Value;
        Assert.NotNull(profile.SpeedAtTwoSecondsMetersPerSecond);
        Assert.Equal(profile.SpeedAtTwoSecondsMetersPerSecond, run.Trace!.StepSamples[0].StandingStartSpeedAtTwoSecondsMetersPerSecond);
    }

    [Fact]
    public void NonStartSegmentsLeaveStandingStartFieldsNull()
    {
        var samples = Run().Trace!.StepSamples.Where(s => s.StepNumber != 0);
        Assert.All(samples, s =>
        {
            Assert.Null(s.StandingStartReactionTimeSeconds);
            Assert.Null(s.StandingStartMovementTimeSeconds);
            Assert.Null(s.StandingStartProfileTotalTimeSeconds);
            Assert.Null(s.StandingStartAccelerationDistanceMeters);
            Assert.Null(s.StandingStartCruiseDistanceMeters);
            Assert.Null(s.StandingStartEntryNetAccelerationMetersPerSecondSquared);
            Assert.Null(s.StandingStartTimeTo70KphSeconds);
            Assert.Null(s.StandingStartSpeedAtTwoSecondsMetersPerSecond);
        });
    }

    [Fact]
    public void StandingStartCsvUsesInvariantCulture()
    {
        var trace = Run().Trace!;
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var polish = CalibrationCsvExporter.ExportSteps(trace);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(polish, CalibrationCsvExporter.ExportSteps(trace));
            Assert.DoesNotContain('\r', polish);
            var rows = polish.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var header = rows[0].Split(',');
            Assert.All(rows, row => Assert.Equal(header.Length, row.Split(',').Length));
            Assert.Equal("0.25", rows[1].Split(',')[Array.IndexOf(header, "StandingStartReactionTimeSeconds")]);
            Assert.Equal(trace.StepSamples[0].StandingStartSpeedAtTwoSecondsMetersPerSecond!.Value.ToString("R", CultureInfo.InvariantCulture),
                rows[1].Split(',')[Array.IndexOf(header, "StandingStartSpeedAtTwoSecondsMetersPerSecond")]);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void StandingStartCsvLeavesNullableMetricsEmpty()
    {
        var rows = CalibrationCsvExporter.ExportSteps(ShortTrace(Resolve(StartTrack(1f))))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = rows[0].Split(',');
        var values = rows[1].Split(',');
        Assert.Equal(string.Empty, values[Array.IndexOf(header, "StandingStartTimeTo70KphSeconds")]);
        Assert.Equal(string.Empty, values[Array.IndexOf(header, "StandingStartSpeedAtTwoSecondsMetersPerSecond")]);
        Assert.Equal("0.25", values[Array.IndexOf(header, "StandingStartReactionTimeSeconds")]);
    }

    [Fact]
    public void CalibrationTraceWorksWithStandingStartAndLoggingDisabled()
    {
        var run = Run();
        Assert.Empty(run.Result.Log.Lines);
        Assert.Equal(144, run.Trace!.StepSamples.Count);
        Assert.Equal(Run(logging: true).Trace!.StepSamples, run.Trace.StepSamples);
    }

    [Fact]
    public void StandingStartHarnessDoesNotChangeProductionResult()
    {
        var direct = Run(observe: false);
        var observed = Run();
        Assert.Equal(direct.Result.Classification, observed.Result.Classification);
        Assert.Equal(direct.Riders.OrderBy(r => r.RiderId).Select(r => (r.Position, r.Speed, r.ElapsedTimeSeconds, r.Status, r.LateralPosition)),
            observed.Riders.OrderBy(r => r.RiderId).Select(r => (r.Position, r.Speed, r.ElapsedTimeSeconds, r.Status, r.LateralPosition)));
    }

    [Fact]
    public void StandingStartHarnessDoesNotChangeTrackState()
    {
        var direct = Run(observe: false);
        var observed = Run();
        for (var segment = 0; segment < direct.State.SegmentCount; segment++)
        for (var lane = 0; lane < direct.State.LinesCount; lane++)
            Assert.Equal(direct.State.GetSurface(segment, lane), observed.State.GetSurface(segment, lane));
    }

    [Fact]
    public void StandingStartTraceIsDeterministic()
    {
        var first = Run().Trace!;
        var second = Run().Trace!;
        Assert.Equal(first.StepSamples, second.StepSamples);
        Assert.Equal(first.LapSummaries, second.LapSummaries);
        Assert.Equal(CalibrationCsvExporter.ExportSteps(first), CalibrationCsvExporter.ExportSteps(second));
    }

    [Fact]
    public void StandingStartTraceIsRiderOrderIndependent()
    {
        var first = Run().Trace!;
        var reverse = Run(order: new[] { 4, 3, 2, 1 }).Trace!;
        Assert.Equal(first.StepSamples, reverse.StepSamples);
        Assert.Equal(first.LapSummaries, reverse.LapSummaries);
        Assert.Equal(first.RiderSummaries, reverse.RiderSummaries);
        Assert.Equal(CalibrationCsvExporter.ExportSteps(first), CalibrationCsvExporter.ExportSteps(reverse));
    }

    [Fact]
    public void StandingStartContactPreservesPreContactProfileAndFinalDuration()
    {
        // A reproducible close pair still goes through the existing contact layer.
        var step = Resolve(riders: new[] { Rider(1, 2), Rider(2, 2) }, seed: 0);
        var contact = Assert.Single(step.Events.Where(e => e.Type == SimulationEventType.ContactLostRhythm));
        var d = step.Diagnostics.Single(d => d.RiderId == contact.RiderId);
        var change = step.Changes.Single(c => c.RiderId == contact.RiderId);
        var profile = d.StandingStartLaunchProfile!.Value;
        Assert.Equal(profile.TotalTimeSeconds, d.TravelTimeSeconds);
        Assert.True(change.ElapsedTimeSeconds > profile.TotalTimeSeconds);
        Assert.True(change.Speed < profile.ExitSpeedMetersPerSecond);
        Assert.Equal(profile.PeakSpeedMetersPerSecond, d.PeakSpeedMetersPerSecond);
        var sample = ShortTrace(step).StepSamples.Single(s => s.RiderId == contact.RiderId);
        Assert.Equal(change.ElapsedTimeSeconds, sample.DurationSeconds);
        Assert.Equal(profile.TotalTimeSeconds, sample.StandingStartProfileTotalTimeSeconds);
    }

    private static CalibrationTrace ShortTrace(ResolvedSimulationStep step)
    {
        var collector = new CalibrationTraceCollector(step.Snapshot.Track, Options(), 30);
        collector.OnStepResolved(step);
        // Classification is irrelevant for a standalone resolved-step observation.
        return collector.Complete(new HeatResult(30, Array.Empty<RiderHeatResult>(), new CoreSim.Logging.SimLog()));
    }
}
