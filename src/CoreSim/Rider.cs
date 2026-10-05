namespace CoreSim;

/// <summary>
/// Legacy engine compatibility skills. Values use a 0..100 scale; the simulation converts
/// them to 0..1 only at the point where an ability is applied.
/// </summary>
public sealed record RiderSkills
{
    public float Start { get; }
    public float Speed { get; }
    public float SlideControl { get; }
    public float TrackReading { get; }
    public float PairRiding { get; }
    public float Adaptability { get; }

    public RiderSkills(
        float start,
        float speed,
        float slideControl,
        float trackReading,
        float pairRiding,
        float adaptability)
    {
        Start = Validate(start, nameof(start));
        Speed = Validate(speed, nameof(speed));
        SlideControl = Validate(slideControl, nameof(slideControl));
        TrackReading = Validate(trackReading, nameof(trackReading));
        PairRiding = Validate(pairRiding, nameof(pairRiding));
        Adaptability = Validate(adaptability, nameof(adaptability));
    }

    public static RiderSkills Balanced => new(50f, 50f, 50f, 50f, 50f, 50f);

    public static float Normalize(float value) => Math.Clamp(value / 100f, 0f, 1f);

    private static float Validate(float value, string parameterName)
    {
        if (value is < 0f or > 100f)
            throw new ArgumentOutOfRangeException(parameterName, value, "Rider skill must be between 0 and 100.");
        return value;
    }
}

/// <summary>
/// Legacy engine compatibility preferences. They shape decisions but never replace skills.
/// Every value is normalized to 0..1.
/// </summary>
public sealed record RiderStyle
{
    public float RiskTolerance { get; }
    public float LaneChangeTendency { get; }
    public float OutsidePreference { get; }
    public float SetupIndependence { get; }

    public RiderStyle(
        float riskTolerance,
        float laneChangeTendency,
        float outsidePreference,
        float setupIndependence)
    {
        RiskTolerance = Validate(riskTolerance, nameof(riskTolerance));
        LaneChangeTendency = Validate(laneChangeTendency, nameof(laneChangeTendency));
        OutsidePreference = Validate(outsidePreference, nameof(outsidePreference));
        SetupIndependence = Validate(setupIndependence, nameof(setupIndependence));
    }

    public static RiderStyle Balanced => new(0.5f, 0.5f, 0.5f, 0.5f);

    private static float Validate(float value, string parameterName)
    {
        if (value is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(parameterName, value, "Rider style value must be between 0 and 1.");
        return value;
    }
}

public sealed partial record RiderProfile(
    int Id,
    string Name,
    RiderSkills Skills,
    RiderStyle Style)
{
    public static RiderProfile CreateDefault(int id, string? name = null)
        => new(id, name ?? $"Rider {id}", RiderSkills.Balanced, RiderStyle.Balanced);
}
