namespace SpeedwaySim.Models;

public sealed class RacingLine
{
    public RacingLine(int index, double distanceModifier, double baseStableSpeed, double tolerance)
    {
        Index = index;
        DistanceModifier = distanceModifier;
        BaseStableSpeed = baseStableSpeed;
        Tolerance = tolerance;
    }

    /// <summary>
    /// Numer linii (1 = najbliżej krawężnika).
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Różnica dystansu względem linii odniesienia (metry na okrążenie).
    /// </summary>
    public double DistanceModifier { get; }

    /// <summary>
    /// Bazowa stabilna prędkość (m/s) przy pełnej przyczepności.
    /// </summary>
    public double BaseStableSpeed { get; }

    /// <summary>
    /// Maksymalny akceptowalny dryf zanim nastąpi wypchnięcie.
    /// </summary>
    public double Tolerance { get; }
}
