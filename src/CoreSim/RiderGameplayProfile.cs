namespace CoreSim;

/// <summary>Real rider mass in kilograms, independent of sporting ability ratings.</summary>
public sealed record RiderPhysicalProfile
{
    public float MassKg { get; }

    public RiderPhysicalProfile(float massKg)
    {
        if (!float.IsFinite(massKg) || massKg <= 0f)
            throw new ArgumentOutOfRangeException(nameof(massKg), massKg, "Rider mass must be positive and finite kilograms.");
        MassKg = massKg;
    }
}

public enum PreferredLine { Inside, Neutral, Outside }

/// <summary>Canonical preferences; higher values do not always mean better performance.</summary>
public sealed record RiderInteractionStyle
{
    /// <summary>Willingness to contest a position; independent of offensive quality (Attack).</summary>
    public float Combativeness { get; }
    /// <summary>Natural line preference; never a speed bonus.</summary>
    public PreferredLine PreferredLine { get; }
    /// <summary>Willingness to insist on one's own setup; not technical knowledge.</summary>
    public float SetupIndependence { get; }

    public RiderInteractionStyle(float combativeness, PreferredLine preferredLine, float setupIndependence)
    {
        Combativeness = Validate(combativeness, nameof(combativeness));
        if (!Enum.IsDefined(preferredLine))
            throw new ArgumentOutOfRangeException(nameof(preferredLine));
        PreferredLine = preferredLine;
        SetupIndependence = Validate(setupIndependence, nameof(setupIndependence));
    }

    private static float Validate(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(parameterName, value, "Rider preference must be finite and between 0 and 1.");
        return value;
    }
}

/// <summary>Canonical domain data. PR #56A does not feed this into production physics.</summary>
public sealed record RiderGameplayProfile
{
    public RiderAbilities Abilities { get; }
    public RiderPhysicalProfile Physical { get; }
    public RiderInteractionStyle InteractionStyle { get; }

    public RiderGameplayProfile(RiderAbilities abilities, RiderPhysicalProfile physical,
        RiderInteractionStyle interactionStyle)
    {
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        Physical = physical ?? throw new ArgumentNullException(nameof(physical));
        InteractionStyle = interactionStyle ?? throw new ArgumentNullException(nameof(interactionStyle));
    }
}
