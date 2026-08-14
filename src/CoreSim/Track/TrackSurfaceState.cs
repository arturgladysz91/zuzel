namespace CoreSim;

/// <summary>Surface state for one segment and one local lane.</summary>
public readonly record struct TrackSurfaceState
{
    public float Grip { get; }
    public float Ruts { get; }
    public float Moisture { get; }

    /// <summary>
    /// Usable grip after moisture and ruts are accounted for. A slightly damp
    /// surface is useful; standing water and an over-dry surface both hurt grip.
    /// </summary>
    public float EffectiveGrip
    {
        get
        {
            const float optimalMoisture = 0.35f;
            var moistureDifference = MathF.Abs(Moisture - optimalMoisture);
            var moisturePenalty = moistureDifference * (Moisture > optimalMoisture ? 0.55f : 0.20f);
            return Clamp01(Grip * (1f - Ruts * 0.45f) - moisturePenalty);
        }
    }

    public TrackSurfaceState(float grip, float ruts, float moisture)
    {
        Grip = Clamp01(grip);
        Ruts = Clamp01(ruts);
        Moisture = Clamp01(moisture);
    }

    public static TrackSurfaceState Default => new(1.0f, 0.0f, 0.5f);

    public TrackSurfaceState WithDelta(float deltaGrip, float deltaRuts, float deltaMoisture)
        => new(Grip + deltaGrip, Ruts + deltaRuts, Moisture + deltaMoisture);

    public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}
