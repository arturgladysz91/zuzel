// Minimalna symulacja biegu: iteruje segmenty, wywołuje decyzje i loguje zmiany linii.
using CoreSim.Decisions;
using CoreSim.Logging;
using System.Globalization;

namespace CoreSim.Race;

public sealed class HeatSimulator
{
    private readonly IRiderDecisionModel _decision;

    public HeatSimulator(IRiderDecisionModel decision) => _decision = decision;

    public SimLog Simulate(CoreSim.Track track, CoreSim.TrackState trackState, List<CoreSim.RiderState> riders, int heatId = 0)
    {
        var log = new SimLog();
        var crashed = new bool[riders.Count];
    for (var segIndex = 0; segIndex < track.Segments.Count; segIndex++)
        {
            var seg = track.Segments[segIndex];
            for (int i = 0; i < riders.Count; i++)
            {
                    if (crashed[i])
                        continue;

                var r = riders[i];
                var before = r.Lane;


                var d = _decision.Decide(seg, r);
                var targetLane = LaneModel.ClampLane(d.TargetLane);
                var plannedLane = CalculatePlannedLane(before, targetLane);

                r.Lane = plannedLane;

                var entrySpeed = r.Speed <= 0f ? SegmentPhysics.MaxSafeTurnSpeed(plannedLane) : r.Speed;
                var resolution = SegmentPhysics.Apply(seg, plannedLane, entrySpeed);

                r.CurrentSegmentId = seg.Id;
                r.Lane = resolution.Lane;
                r.Speed = resolution.Speed;
                r.Risk = ApplySurfaceRisk(seg, trackState, segIndex, r.Lane, d.Risk);

                log.Add($"SEG={seg.Id} {seg.Type} rider={r.RiderId} lane {before}->{plannedLane}->{r.Lane} targetLane={d.TargetLane} outcome={resolution.Outcome} v_in={entrySpeed.ToString("F2", CultureInfo.InvariantCulture)} v_out={resolution.Speed.ToString("F2", CultureInfo.InvariantCulture)}");

                if (resolution.Outcome != SegmentOutcome.Crash)
                {
                    ApplySurfaceWear(seg, trackState, segIndex, r.Lane, heatId, segIndex, log);
                }
                else
                {
                    crashed[i] = true;
                }
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
    
    private static int CalculatePlannedLane(int currentLane, int targetLane)
    {
        if (targetLane > currentLane)
            return LaneModel.ClampLane(currentLane + 1);
        if (targetLane < currentLane)
            return LaneModel.ClampLane(currentLane - 1);
        return currentLane;
    }
}
