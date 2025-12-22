// Zdarzenie zmiany nawierzchni toru do logowania.
namespace CoreSim.Logging;

public sealed record TrackSurfaceChange(
    int SegmentId,
    int LineIndex,
    float DeltaGrip,
    float DeltaRuts,
    float DeltaMoisture,
    string Reason,
    int HeatId,
    int Tick);
