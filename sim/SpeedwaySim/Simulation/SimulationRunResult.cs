using System.Collections.Generic;
using System.Linq;

namespace SpeedwaySim.Simulation;

public sealed class SimulationRunResult
{
    public SimulationRunResult(IReadOnlyList<LapResult> laps)
    {
        Laps = laps;
        TotalTimeSeconds = laps.Sum(l => l.LapTimeSeconds);
    }

    public IReadOnlyList<LapResult> Laps { get; }

    public double TotalTimeSeconds { get; }
}
