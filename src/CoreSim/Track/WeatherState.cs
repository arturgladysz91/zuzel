namespace CoreSim;

public enum WeatherCondition
{
    Dry,
    Cloudy,
    LightRain,
    Rain,
    HeavyRain,
}

/// <summary>Weather inputs normalized to one simulation tick.</summary>
public sealed record WeatherState
{
    public WeatherCondition Condition { get; }
    public float RainIntensity { get; }
    public float DryingRate { get; }

    public WeatherState(WeatherCondition condition, float rainIntensity, float dryingRate)
    {
        if (rainIntensity is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(rainIntensity));
        if (dryingRate is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(dryingRate));

        Condition = condition;
        RainIntensity = rainIntensity;
        DryingRate = dryingRate;
    }

    public static WeatherState Dry => new(WeatherCondition.Dry, 0f, 0.015f);
    public static WeatherState LightRain => new(WeatherCondition.LightRain, 0.25f, 0.002f);
}
