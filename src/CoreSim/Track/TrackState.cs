// Aktualny stan nawierzchni toru (HUM, PACK, RUTS, SHIFT) w wersji MVP (globalny; per segment dodamy później).
namespace CoreSim;

public sealed class TrackState
{
    public float Humidity { get; set; }
    public float Packing { get; set; }
    public float Ruts { get; set; }
    public float Shift { get; set; }

    public TrackState(float humidity, float packing, float ruts, float shift)
    {
        Humidity = humidity;
        Packing = packing;
        Ruts = ruts;
        Shift = shift;
    }

    public static TrackState CreateDefault() => new(55f, 60f, 10f, 18f);
}
