using SharedWorldSpeedway;
namespace Efficient147;
/// <summary>Exact immutable-frame ownership for optional geometry reuse; no geometry algorithms.</summary>
public sealed class PredictionBoundary147 {
    readonly TrackFrame frame;readonly RacingLineGeometry geometry;
    public PredictionBoundary147(TrackFrame frame){this.frame=frame;geometry=new(frame);}
    public RacingLineGeometry For(TrackFrame candidate)=>ReferenceEquals(frame,candidate)?geometry:
        throw new ArgumentException("Cached boundary must belong to this exact immutable frame.");
}
