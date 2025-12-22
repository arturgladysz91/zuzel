// Minimalna symulacja biegu: iteruje segmenty, wywołuje decyzje i loguje zmiany linii.
using CoreSim.Decisions;
using CoreSim.Logging;

namespace CoreSim.Race;

public sealed class HeatSimulator
{
    private readonly IRiderDecisionModel _decision;

    public HeatSimulator(IRiderDecisionModel decision) => _decision = decision;

    public SimLog Simulate(CoreSim.Track track, CoreSim.TrackState trackState, List<CoreSim.RiderState> riders, int heatId = 0)
    {
        var log = new SimLog();

   for (var segIndex = 0; segIndex < track.Segments.Count; segIndex++)
        {
            var seg = track.Segments[segIndex];
            for (int i = 0; i < riders.Count; i++)
            {
                var r = riders[i];
                var before = r.Lane;

                var d = _decision.Decide(seg, r);

                r.CurrentSegmentId = seg.Id;
                r.Lane = d.TargetLane;
                r.Risk = ApplySurfaceRisk(seg, trackState, segIndex, r.Lane, d.Risk);

                log.Add($"SEG={seg.Id} {seg.Type} rider={r.RiderId} lane {before}->{r.Lane}");
                
                ApplySurfaceWear(seg, trackState, segIndex, r.Lane, heatId, segIndex, log);
            }
        }

        return log;
    }
    
    private static float ApplySurfaceRisk(TrackSegment segment, TrackState trackState, int segmentIndex, int lane, float baseRisk)
    {
        if (segment.Type == SegmentType.Straight)
            return baseRisk;

        var surface = trackState.GetSurface(segmentIndex, lane);
        var surfaceRisk = (1f - surface.Grip) * 0.7f + surface.Ruts * 0.3f;
        if (surface.Moisture > 0.5f)
            surfaceRisk += (surface.Moisture - 0.5f) * 0.2f;
        surfaceRisk = TrackSurfaceState.Clamp01(surfaceRisk);
        return TrackSurfaceState.Clamp01(baseRisk + surfaceRisk);
    }

    private static void ApplySurfaceWear(
        TrackSegment segment,
        TrackState trackState,
        int segmentIndex,
        int lane,
        int heatId,
        int tick,
        SimLog log)
    {
        var rutsDelta = segment.Type == SegmentType.Straight ? 0.005f : 0.02f;
        var gripDelta = -0.25f * rutsDelta;
        trackState.ApplySurfaceDelta(segmentIndex, lane, gripDelta, rutsDelta, 0f, "pass", heatId, tick, log);
    }
}
