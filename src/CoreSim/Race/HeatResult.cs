using CoreSim.Logging;

namespace CoreSim.Race;

public sealed record RiderHeatResult(
    int RiderId,
    int Position,
    int Points,
    bool Finished,
    bool Crashed,
    float TimeSeconds,
    float DistanceMeters,
    int LapsCompleted);

public sealed record HeatResult(
    int HeatId,
    IReadOnlyList<RiderHeatResult> Classification,
    SimLog Log);

public sealed record HeatSimulationOptions
{
    public int Laps { get; init; } = 4;
    public int Seed { get; init; } = 1234;
    public WeatherState Weather { get; init; } = WeatherState.Dry;
    public float IncidentFrequency { get; init; } = 1f;
    public bool EnableLogging { get; init; } = true;

    public void Validate()
    {
        if (Laps <= 0)
            throw new ArgumentOutOfRangeException(nameof(Laps));
        if (IncidentFrequency is < 0f or > 2f)
            throw new ArgumentOutOfRangeException(nameof(IncidentFrequency));
    }
}
