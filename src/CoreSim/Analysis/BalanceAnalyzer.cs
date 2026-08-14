using CoreSim.Decisions;
using CoreSim.Race;

namespace CoreSim.Analysis;

public sealed record GateBalanceStat(
    int Gate,
    int Starts,
    int Wins,
    float WinRate,
    float AveragePosition,
    float AveragePoints,
    float CrashRate);

public sealed record GateBalanceReport(
    int Simulations,
    IReadOnlyList<GateBalanceStat> Gates)
{
    public float WinRateSpread => Gates.Count == 0
        ? 0f
        : Gates.Max(gate => gate.WinRate) - Gates.Min(gate => gate.WinRate);
}

/// <summary>
/// Runs the same four-rider field repeatedly while rotating every rider through
/// every starting gate. This separates rider strength from starting-gate bias.
/// </summary>
public static class BalanceAnalyzer
{
    public static GateBalanceReport AnalyzeStartingGates(
        Track track,
        IReadOnlyList<RiderProfile> profiles,
        int simulations = 2000,
        int seed = 1234,
        float incidentFrequency = 1f)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.Count != 4)
            throw new ArgumentException("Starting-gate analysis requires exactly four riders.", nameof(profiles));
        if (profiles.Select(profile => profile.Id).Distinct().Count() != profiles.Count)
            throw new ArgumentException("Every profile must have a unique id.", nameof(profiles));
        if (simulations <= 0)
            throw new ArgumentOutOfRangeException(nameof(simulations));
        if (incidentFrequency is < 0f or > 2f)
            throw new ArgumentOutOfRangeException(nameof(incidentFrequency));

        var totals = Enumerable.Range(0, 4).Select(_ => new GateTotals()).ToArray();
        var neutralWeather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f);

        for (var simulation = 0; simulation < simulations; simulation++)
        {
            var riders = new List<RiderState>(profiles.Count);
            var gateByRider = new Dictionary<int, int>(profiles.Count);
            for (var profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
            {
                var lane = (profileIndex + simulation) % 4;
                var profile = profiles[profileIndex];
                riders.Add(new RiderState(profile, lane));
                gateByRider.Add(profile.Id, lane);
            }

            var simulator = new HeatSimulator(new AdaptiveDecisionModel(seed + simulation * 2));
            var result = simulator.SimulateHeat(
                track,
                TrackState.CreateDefault(track),
                riders,
                new HeatSimulationOptions
                {
                    Laps = 4,
                    Seed = seed + simulation * 2 + 1,
                    Weather = neutralWeather,
                    IncidentFrequency = incidentFrequency,
                    EnableLogging = false,
                },
                heatId: simulation + 1);

            foreach (var riderResult in result.Classification)
            {
                var total = totals[gateByRider[riderResult.RiderId]];
                total.Starts++;
                total.Positions += riderResult.Position;
                total.Points += riderResult.Points;
                total.Wins += riderResult.Position == 1 ? 1 : 0;
                total.Crashes += riderResult.Crashed ? 1 : 0;
            }
        }

        var gates = totals
            .Select((total, index) => new GateBalanceStat(
                Gate: index + 1,
                Starts: total.Starts,
                Wins: total.Wins,
                WinRate: total.Wins / (float)total.Starts,
                AveragePosition: total.Positions / (float)total.Starts,
                AveragePoints: total.Points / (float)total.Starts,
                CrashRate: total.Crashes / (float)total.Starts))
            .ToArray();

        return new GateBalanceReport(simulations, gates);
    }

    private sealed class GateTotals
    {
        public int Starts { get; set; }
        public int Wins { get; set; }
        public int Positions { get; set; }
        public int Points { get; set; }
        public int Crashes { get; set; }
    }
}
