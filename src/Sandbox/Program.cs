// Aplikacja testowa do uruchamiania symulacji i wypisywania logów na konsolę.
// Uruchamia minimalny bieg i wypisuje log z decyzji linii per segment.
using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;

var track = Track.CreateExample();
var trackState = TrackState.CreateDefault(track);

var riders = new List<RiderState>
{
    RiderState.CreateDefault(0, 0),
    RiderState.CreateDefault(1, 1),
    RiderState.CreateDefault(2, 2),
    RiderState.CreateDefault(3, 3),
};

var sim = new HeatSimulator(new SimpleDecisionModel(seed: 1234));
var log = sim.Simulate(track, trackState, riders);

foreach (var line in log.Lines)
    Console.WriteLine(line);
