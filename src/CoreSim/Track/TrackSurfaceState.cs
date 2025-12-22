// Stan nawierzchni per segment i linia.
namespace CoreSim;

public readonly record struct TrackSurfaceState
{
    public float Grip { get; }
    public float Ruts { get; }
    public float Moisture { get; }

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
