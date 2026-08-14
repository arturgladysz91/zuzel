using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

var track = Track.CreateExample();
var trackState = TrackState.CreateDefault(track);

var riders = new List<RiderState>
{
    new(new RiderProfile(1, "Kowalski", new RiderSkills(78, 72, 70, 82, 65, 75), RiderStyle.Balanced), 0),
    new(new RiderProfile(2, "Nowak", new RiderSkills(64, 80, 76, 62, 72, 68), new RiderStyle(0.7f, 0.7f, 0.8f, 0.6f)), 1),
    new(new RiderProfile(3, "Wiśniewski", new RiderSkills(84, 65, 58, 70, 60, 72), new RiderStyle(0.4f, 0.4f, 0.3f, 0.4f)), 2),
    new(new RiderProfile(4, "Wójcik", new RiderSkills(69, 74, 82, 76, 80, 66), RiderStyle.Balanced), 3),
};

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
var result = simulator.SimulateHeat(
    track,
    trackState,
    riders,
    new HeatSimulationOptions
    {
        Laps = 4,
        Seed = 2026,
        Weather = WeatherState.LightRain,
    },
    heatId: 1);

foreach (var line in result.Log.Lines)
    Console.WriteLine(line);

Console.WriteLine("\nClassification:");
foreach (var rider in result.Classification)
    Console.WriteLine($"{rider.Position}. rider={rider.RiderId} points={rider.Points} crashed={rider.Crashed} time={rider.TimeSeconds:F2}");
