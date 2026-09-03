using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

var track = Track.CreateExample();
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

foreach (var line in result.Log.Lines)
    Console.WriteLine(line);

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
Console.WriteLine("\nStarting-gate balance (2000 heats, riders rotated):");
foreach (var gate in balance.Gates)
{
    Console.WriteLine(
        $"Gate {gate.Gate}: win={gate.WinRate:P1} avgPos={gate.AveragePosition:F2} avgPts={gate.AveragePoints:F2} crash={gate.CrashRate:P1}");
}
Console.WriteLine($"Win-rate spread: {balance.WinRateSpread:P1}");
