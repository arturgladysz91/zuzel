using CoreSim.Decisions;
using CoreSim.Race;

namespace CoreSim.Analysis;

public static class CalibrationRunner
{
    /// <summary>
    /// Runs the existing production heat loop with a typed observer. As with
    /// HeatSimulator, the supplied rider and track state objects are mutated.
    /// </summary>
    public static CalibrationTrace RunHeat(
        Track track,
        TrackState trackState,
        List<RiderState> riders,
        IRiderDecisionModel decisionModel,
        HeatSimulationOptions? options = null,
        int heatId = 0)
    {
        ArgumentNullException.ThrowIfNull(decisionModel);
        options ??= new HeatSimulationOptions();
        var collector = new CalibrationTraceCollector(track, options, heatId);
        var result = new HeatSimulator(decisionModel).SimulateHeat(
            track,
            trackState,
            riders,
            options,
            heatId,
            collector);
        return collector.Complete(result);
    }
}
