// Minimalna symulacja biegu: iteruje segmenty, wywołuje decyzje i loguje zmiany linii.
using CoreSim.Decisions;
using CoreSim.Logging;

namespace CoreSim.Race;

public sealed class HeatSimulator
{
    private readonly IRiderDecisionModel _decision;

    public HeatSimulator(IRiderDecisionModel decision) => _decision = decision;

    public SimLog Simulate(CoreSim.Track track, CoreSim.TrackState trackState, List<CoreSim.RiderState> riders)
    {
        var log = new SimLog();

        foreach (var seg in track.Segments)
        {
            for (int i = 0; i < riders.Count; i++)
            {
                var r = riders[i];
                var before = r.Lane;

                var d = _decision.Decide(seg, r);

                r.CurrentSegmentId = seg.Id;
                r.Lane = d.TargetLane;
                r.Risk = d.Risk;

                log.Add($"SEG={seg.Id} {seg.Type} rider={r.RiderId} lane {before}->{r.Lane}");
            }
        }

        return log;
    }
}
