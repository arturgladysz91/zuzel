namespace CoreSim;

/// <summary>Canonical gameplay abilities, each an integer in 1..99. No overall rating.</summary>
public sealed record RiderAbilities
{
    /// <summary>Tape reaction timing only; not launch force.</summary>
    public int Reaction { get; }
    /// <summary>Technical launch execution after reaction; not general speed.</summary>
    public int Start { get; }
    /// <summary>Balance, slide/throttle control, precision and recovery; not line choice.</summary>
    public int Technique { get; }
    /// <summary>Perception of grip, ruts, moisture and evolving paths; no physical grip/speed bonus.</summary>
    public int TrackReading { get; }
    /// <summary>Offensive maneuver quality; not willingness to fight or take risks.</summary>
    public int Attack { get; }
    /// <summary>Defensive execution quality, independent of Attack.</summary>
    public int Defense { get; }
    /// <summary>Cooperation with a teammate; not contact with opponents.</summary>
    public int PairRiding { get; }
    /// <summary>Physical control under loads/contact; not engine power, Vmax or traction.</summary>
    public int Strength { get; }

    public RiderAbilities(int reaction, int start, int technique, int trackReading,
        int attack, int defense, int pairRiding, int strength)
    {
        Reaction = Validate(reaction, nameof(reaction));
        Start = Validate(start, nameof(start));
        Technique = Validate(technique, nameof(technique));
        TrackReading = Validate(trackReading, nameof(trackReading));
        Attack = Validate(attack, nameof(attack));
        Defense = Validate(defense, nameof(defense));
        PairRiding = Validate(pairRiding, nameof(pairRiding));
        Strength = Validate(strength, nameof(strength));
    }

    public static RiderAbilities Balanced => new(50, 50, 50, 50, 50, 50, 50, 50);

    private static int Validate(int value, string parameterName)
    {
        if (value is < 1 or > 99)
            throw new ArgumentOutOfRangeException(parameterName, value, "Rider ability must be between 1 and 99.");
        return value;
    }
}
