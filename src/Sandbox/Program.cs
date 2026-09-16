using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

if (args.Length > 0)
{
    if (args.Length == 3 && StringComparer.Ordinal.Equals(args[0], "real-start-telemetry-report"))
    {
        RealStartTelemetryReportWriter.Write(args[1], args[2]);
        Console.WriteLine($"Wrote deterministic real-start telemetry report: {args[2]}");
        return;
    }

    if (args.Length == 3 && StringComparer.Ordinal.Equals(args[0], "motoarena-matched-venue-report"))
    {
        MotoarenaMatchedVenueReportWriter.Write(args[1], args[2]);
        Console.WriteLine($"Wrote deterministic Motoarena matched-venue report: {args[2]}");
        return;
    }

    if (args.Length == 4 && StringComparer.Ordinal.Equals(args[0], "continuous-corner-envelope-impact-report"))
    {
        ContinuousCornerEnvelopeImpactReportWriter.Write(args[1], args[2], args[3]);
        Console.WriteLine($"Wrote deterministic continuous corner-envelope impact report: {args[3]}");
        return;
    }

    if (args.Length == 3 && StringComparer.Ordinal.Equals(args[0], "longitudinal-speed-envelope-snapshot"))
    {
        LongitudinalCalibrationReportWriter.WriteSnapshot(args[1], args[2]);
        Console.WriteLine($"Wrote deterministic longitudinal calibration snapshot: {args[2]}");
        return;
    }

    if (args.Length == 6 && StringComparer.Ordinal.Equals(args[0], "longitudinal-speed-envelope-impact-report"))
    {
        LongitudinalCalibrationReportWriter.WriteImpact(args[1], args[2], args[3], args[4], args[5]);
        Console.WriteLine($"Wrote deterministic longitudinal speed-envelope impact report: {args[5]}");
        return;
    }

    if (args.Length == 6 && StringComparer.Ordinal.Equals(args[0], "continuous-corner-foundation-impact-report"))
    {
        ContinuousCornerFoundationReportWriter.Write(args[1], args[2], args[3], args[4], args[5]);
        Console.WriteLine($"Wrote deterministic continuous-corner foundation impact report: {args[5]}");
        return;
    }

    if (args.Length == 4 && StringComparer.Ordinal.Equals(args[0], "calibration-scenarios-report"))
    {
        CalibrationScenarioReportWriter.Write(args[1], args[2], args[3]);
        Console.WriteLine($"Wrote deterministic calibration scenarios report: {args[3]}");
        return;
    }

    if (args.Length == 3 && StringComparer.Ordinal.Equals(args[0], "calibration-report"))
    {
        CalibrationBaselineReportWriter.Write(args[1], args[2]);
        Console.WriteLine($"Wrote deterministic calibration report: {args[2]}");
        return;
    }

    if (args.Length == 4 && StringComparer.Ordinal.Equals(args[0], "physical-width-impact-report"))
    {
        PhysicalWidthImpactReportWriter.Write(args[1], args[2], args[3]);
        Console.WriteLine($"Wrote deterministic physical-width impact report: {args[3]}");
        return;
    }

    if (args.Length == 4 && StringComparer.Ordinal.Equals(args[0], "continuous-corner-correction-impact-report"))
    {
        ContinuousCornerCorrectionImpactReportWriter.Write(args[1], args[2], args[3]);
        Console.WriteLine($"Wrote deterministic continuous corner-correction impact report: {args[3]}");
        return;
    }

    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  Sandbox motoarena-matched-venue-report <dataset-directory> <output-markdown>");
    Console.Error.WriteLine("  Sandbox continuous-corner-envelope-impact-report <dataset-directory> <after-scenario-sha256> <output-markdown>");
    Console.Error.WriteLine("  Sandbox longitudinal-speed-envelope-snapshot <source-sha> <output-json>");
    Console.Error.WriteLine("  Sandbox longitudinal-speed-envelope-impact-report <dataset-directory> <before-snapshot-json> <base-main-sha> <candidate-head-sha> <output-markdown>");
    Console.Error.WriteLine("  Sandbox continuous-corner-foundation-impact-report <dataset-directory> <before-calibration-scenarios-report> <base-main-sha> <candidate-code-head-sha> <output-markdown>");
    Console.Error.WriteLine("  Sandbox calibration-scenarios-report <dataset-directory> <baseline-main-sha> <output-markdown>");
    Console.Error.WriteLine("  Sandbox calibration-report <dataset-directory> <output-markdown>");
    Console.Error.WriteLine("  Sandbox physical-width-impact-report <dataset-directory> <historical-baseline-markdown> <output-markdown>");
    Console.Error.WriteLine("  Sandbox continuous-corner-correction-impact-report <dataset-directory> <historical-width-report-markdown> <output-markdown>");
    Environment.ExitCode = 2;
    return;
}

var track = Track.CreateStandingStartExample();
var trackState = TrackState.CreateDefault(track);

var profiles = new[]
{
    new RiderProfile(1, "Kowalski", new RiderSkills(78, 72, 70, 82, 65, 75), RiderStyle.Balanced),
    new RiderProfile(2, "Nowak", new RiderSkills(64, 80, 76, 62, 72, 68), new RiderStyle(0.7f, 0.7f, 0.8f, 0.6f)),
    new RiderProfile(3, "Wiśniewski", new RiderSkills(84, 65, 58, 70, 60, 72), new RiderStyle(0.4f, 0.4f, 0.3f, 0.4f)),
    new RiderProfile(4, "Wójcik", new RiderSkills(69, 74, 82, 76, 80, 66), RiderStyle.Balanced),
};
var riders = profiles.Select((profile, lane) => new RiderState(profile, lane)).ToList();

var setupResolver = new SetupResolver(seed: 11);
foreach (var rider in riders)
{
    var resolution = setupResolver.Resolve(
        rider,
        new ManagerSetupSuggestion(new BikeSetup(0.55f, 0.65f), Confidence: 0.8f),
        BikeSetup.Neutral);
    Console.WriteLine($"Setup rider={rider.RiderId}: {resolution.Response}");
}

var preparationLog = new CoreSim.Logging.SimLog();
TrackEvolution.ApplyTrackWork(
    track,
    trackState,
    new TrackWorkAction(TrackWorkType.Grade, 0.6f, Lanes: new[] { 0, 1, 2 }),
    heatId: 1,
    tick: -1,
    preparationLog);

var simulator = new HeatSimulator(new AdaptiveDecisionModel(seed: 1234));
var options = new HeatSimulationOptions
{
    Laps = 4,
    Seed = 2026,
    Weather = WeatherState.LightRain,
    EnableLogging = false,
};
var calibrationCollector = new CalibrationTraceCollector(track, options, heatId: 1);
var result = simulator.SimulateHeat(
    track,
    trackState,
    riders,
    options,
    heatId: 1,
    observer: calibrationCollector);
var calibrationTrace = calibrationCollector.Complete(result);

var launch = calibrationTrace.StepSamples.First(sample => sample.RiderId == 1);
var firstTurn = calibrationTrace.StepSamples.FirstOrDefault(sample =>
    sample.RiderId == 1 && sample.SegmentType == SegmentType.TurnEntry);
Console.WriteLine($"\nProvisional standing start rider=1: reaction={launch.StandingStartReactionTimeSeconds:F3}s "
    + $"movement={launch.StandingStartMovementTimeSeconds:F3}s total={launch.StandingStartProfileTotalTimeSeconds:F3}s "
    + $"peak={launch.PeakSpeedMetersPerSecond:F3}m/s exit={launch.ExitSpeedMetersPerSecond:F3}m/s "
    + $"preparation={launch.StandingStartPreparationDistanceMeters:F3}m");
Console.WriteLine($"TimeTo70={launch.StandingStartTimeTo70KphSeconds?.ToString("F3") ?? "not reached"}; "
    + $"SpeedAt2s={launch.StandingStartSpeedAtTwoSecondsMetersPerSecond?.ToString("F3") ?? "not available"}; "
    + $"first TurnEntry entry={firstTurn?.EntrySpeedMetersPerSecond.ToString("F3") ?? "not available"}m/s");

Console.WriteLine("\nClassification:");
foreach (var rider in result.Classification)
    Console.WriteLine($"{rider.Position}. rider={rider.RiderId} points={rider.Points} crashed={rider.Crashed} time={rider.TimeSeconds:F2}");

Console.WriteLine($"\nCalibration summary: samples={calibrationTrace.StepSamples.Count}");
foreach (var rider in calibrationTrace.RiderSummaries)
{
    Console.WriteLine(
        $"rider={rider.RiderId} time={rider.TotalTimeSeconds:F3} distance={rider.TotalDistanceMeters:F3} vmax={rider.MaxSpeedMetersPerSecond:F3}");
}
foreach (var lap in calibrationTrace.LapSummaries)
{
    Console.WriteLine(
        $"lap rider={lap.RiderId} number={lap.LapNumber} time={lap.LapTimeSeconds:F3} distance={lap.LapDistanceMeters:F3} vmax={lap.MaxSpeedMetersPerSecond:F3}");
}

Console.WriteLine("\nCalibration CSV sample:");
foreach (var row in CalibrationCsvExporter.ExportSteps(calibrationTrace)
             .Split('\n', StringSplitOptions.RemoveEmptyEntries)
             .Take(4))
{
    Console.WriteLine(row);
}

var balance = BalanceAnalyzer.AnalyzeStartingGates(track, profiles, simulations: 2000, seed: 2026);
Console.WriteLine("\nCompatibility start-position balance (2000 heats, riders rotated):");
Console.WriteLine("Physical A/B/C/D gate geometry is not modeled; this is not final gate-advantage calibration.");
foreach (var gate in balance.Gates)
{
    Console.WriteLine(
        $"Gate {gate.Gate}: win={gate.WinRate:P1} avgPos={gate.AveragePosition:F2} avgPts={gate.AveragePoints:F2} crash={gate.CrashRate:P1}");
}
Console.WriteLine($"Win-rate spread: {balance.WinRateSpread:P1}");
