namespace CoreSim.Setup;

/// <summary>
/// Setup is a trade-off, not a purchasable performance tier. Values are 0..1:
/// lower gearing favours corner drive, higher gearing favours carrying speed;
/// traction bias trades stability for a freer motorcycle.
/// </summary>
public sealed record BikeSetup
{
    public float Gearing { get; }
    public float TractionBias { get; }

    public BikeSetup(float gearing, float tractionBias)
    {
        Gearing = Validate(gearing, nameof(gearing));
        TractionBias = Validate(tractionBias, nameof(tractionBias));
    }

    public static BikeSetup Neutral => new(0.5f, 0.5f);

    public static BikeSetup Blend(BikeSetup first, BikeSetup second, float secondWeight)
    {
        var weight = Math.Clamp(secondWeight, 0f, 1f);
        return new BikeSetup(
            first.Gearing * (1f - weight) + second.Gearing * weight,
            first.TractionBias * (1f - weight) + second.TractionBias * weight);
    }

    private static float Validate(float value, string parameterName)
    {
        if (value is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(parameterName, value, "Setup value must be between 0 and 1.");
        return value;
    }
}
