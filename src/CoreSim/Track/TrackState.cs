// Aktualny stan nawierzchni toru (Humidity, Packing, Ruts, Shift) liczony per segment.
// Zmienny stan nawierzchni toru, liczony per segment.
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
}
