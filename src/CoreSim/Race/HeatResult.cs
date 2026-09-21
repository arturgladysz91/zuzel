using CoreSim.Logging;

namespace CoreSim.Race;

public sealed record RiderHeatResult(
    int RiderId,
    int Position,
    int Points,
    RiderRaceStatus Status,
    float TimeSeconds,
    float DistanceMeters,
    int LapsCompleted)
{
    public RiderHeatResult(
        int RiderId,
        int Position,
        int Points,
        bool Finished,
        bool Crashed,
        float TimeSeconds,
        float DistanceMeters,
        int LapsCompleted)
        : this(
            RiderId,
            Position,
            Points,
            ResolveLegacyStatus(Finished, Crashed),
            TimeSeconds,
            DistanceMeters,
            LapsCompleted)
    {
    }

    public bool Finished => Status == RiderRaceStatus.Finished;
    public bool Crashed => Status == RiderRaceStatus.Crashed;
    public bool Retired => Status == RiderRaceStatus.Retired;
    public bool Dnf => Crashed || Retired;

    public void Deconstruct(
        out int RiderId,
        out int Position,
        out int Points,
        out bool Finished,
        out bool Crashed,
        out float TimeSeconds,
        out float DistanceMeters,
        out int LapsCompleted)
    {
        RiderId = this.RiderId;
        Position = this.Position;
        Points = this.Points;
        Finished = this.Finished;
        Crashed = this.Crashed;
        TimeSeconds = this.TimeSeconds;
        DistanceMeters = this.DistanceMeters;
        LapsCompleted = this.LapsCompleted;
    }

    private static RiderRaceStatus ResolveLegacyStatus(bool finished, bool crashed)
    {
        if (finished && crashed)
            throw new ArgumentException("A rider heat result cannot be both finished and crashed.");

        return finished
            ? RiderRaceStatus.Finished
            : crashed
                ? RiderRaceStatus.Crashed
                : RiderRaceStatus.Racing;
    }
}

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

    // Calibration-only; deliberately unavailable to gameplay callers outside
    // CoreSim. Null preserves the reviewed production path exactly.
    internal StraightDriveEnvelopeAdjustment? StraightDriveEnvelopeAdjustment { get; init; }

    // Calibration-only #42 corner-force experiment. Null preserves the
    // reviewed post-#41 production path exactly and is not a gameplay option.
    internal CornerReducedDriveResistanceAdjustment? CornerReducedDriveResistanceAdjustment { get; init; }

    // Calibration-only #43 diagnostic. Null preserves reviewed production
    // physics exactly and is unavailable to gameplay callers outside CoreSim.
    internal PreApexScrubLossAdjustment? PreApexScrubLossAdjustment { get; init; }

    public void Validate()
    {
        if (Laps <= 0)
            throw new ArgumentOutOfRangeException(nameof(Laps));
        if (IncidentFrequency is < 0f or > 2f)
            throw new ArgumentOutOfRangeException(nameof(IncidentFrequency));
        if (PreApexScrubLossAdjustment is not null
            && (StraightDriveEnvelopeAdjustment is not null
                || CornerReducedDriveResistanceAdjustment is not null))
            throw new InvalidOperationException(
                "The pre-apex scrub diagnostic cannot be combined with another calibration experiment.");
    }
}
