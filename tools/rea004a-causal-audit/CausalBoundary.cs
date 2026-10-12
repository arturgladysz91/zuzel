using CoreSim;
using CoreSim.Interactions;

namespace CausalAudit;

// An experiment boundary, never called by production. Inspection is not rollback.
internal sealed class UnsupportedCausalReplayException(string reason) : InvalidOperationException(reason);
internal static class CausalBoundary
{
    internal static void RequireUncommitted(SimulationSnapshot snapshot, double time)
    {
        if (!double.IsFinite(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
        var ahead = snapshot.Riders.Where(r => r.ElapsedTimeSeconds > time).Select(r => r.RiderId).ToArray();
        if (ahead.Length != 0)
            throw new UnsupportedCausalReplayException("CommittedBeyondFirstTouch: " + string.Join(",", ahead));
    }

    internal static void RequireCommonStart(SimulationSnapshot snapshot, double time)
    {
        RequireUncommitted(snapshot, time);
        if (snapshot.Riders.Any(r => r.ElapsedTimeSeconds != time))
            throw new UnsupportedCausalReplayException("PartialProductionCursorRequired");
    }
}
