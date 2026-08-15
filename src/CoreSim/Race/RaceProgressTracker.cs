using CoreSim.Logging;
using System.Globalization;

namespace CoreSim.Race;

/// <summary>
/// Converts changes in running order into explicit overtakes and lap snapshots.
/// A retirement never counts as an overtake.
/// </summary>
public sealed class RaceProgressTracker
{
    private IReadOnlyList<int> _previousOrder = Array.Empty<int>();
    private IReadOnlyDictionary<int, bool> _previousCrashState = new Dictionary<int, bool>();

    public void InitializeStartingGrid(IReadOnlyList<RiderState> riders)
    {
        ArgumentNullException.ThrowIfNull(riders);
        _previousOrder = riders
            .OrderBy(rider => rider.Lane)
            .ThenBy(rider => rider.RiderId)
            .Select(rider => rider.RiderId)
            .ToArray();
        _previousCrashState = riders.ToDictionary(rider => rider.RiderId, rider => rider.IsCrashed);
    }

    public void CaptureSegment(
        int lap,
        int segmentId,
        bool lapComplete,
        IReadOnlyList<RiderState> riders,
        SimLog log)
    {
        ArgumentNullException.ThrowIfNull(riders);
        ArgumentNullException.ThrowIfNull(log);
        if (!log.Enabled)
            return;

        var ordered = BuildRunningOrder(riders);
        var currentPositions = ordered
            .Select((rider, index) => new { rider.RiderId, Position = index + 1 })
            .ToDictionary(item => item.RiderId, item => item.Position);
        var currentCrashState = ordered.ToDictionary(rider => rider.RiderId, rider => rider.IsCrashed);

        if (_previousOrder.Count > 0)
            CaptureOvertakes(lap, segmentId, ordered, currentPositions, currentCrashState, log);

        CaptureSegmentOrder(lap, segmentId, ordered, log);

        if (lapComplete)
            CaptureLapOrder(lap, segmentId, ordered, log);

        _previousOrder = ordered.Select(rider => rider.RiderId).ToArray();
        _previousCrashState = currentCrashState;
    }

    private void CaptureOvertakes(
        int lap,
        int segmentId,
        IReadOnlyList<RiderState> ordered,
        IReadOnlyDictionary<int, int> currentPositions,
        IReadOnlyDictionary<int, bool> currentCrashState,
        SimLog log)
    {
        var previousPositions = _previousOrder
            .Select((riderId, index) => new { RiderId = riderId, Position = index + 1 })
            .ToDictionary(item => item.RiderId, item => item.Position);

        foreach (var rider in ordered.Where(rider => !rider.IsCrashed))
        {
            if (!previousPositions.TryGetValue(rider.RiderId, out var oldPosition))
                continue;

            var newPosition = currentPositions[rider.RiderId];
            if (newPosition >= oldPosition
                || _previousCrashState.GetValueOrDefault(rider.RiderId))
                continue;

            foreach (var passedRiderId in _previousOrder.Take(oldPosition - 1))
            {
                if (passedRiderId == rider.RiderId
                    || _previousCrashState.GetValueOrDefault(passedRiderId)
                    || currentCrashState.GetValueOrDefault(passedRiderId)
                    || currentPositions[passedRiderId] <= newPosition)
                    continue;

                var overtake = new OvertakeEvent(
                    lap,
                    segmentId,
                    rider.RiderId,
                    passedRiderId,
                    oldPosition,
                    newPosition);
                log.Add(overtake);
                log.Add(new RaceEvent(
                    RaceEventType.Overtake,
                    lap,
                    segmentId,
                    rider.RiderId,
                    passedRiderId,
                    rider.Lane,
                    rider.GapToRiderAheadSeconds,
                    rider.Speed,
                    "running order changed after physical pass"));
                log.Add($"OVERTAKE lap={lap} seg={segmentId} rider={rider.RiderId} passed={passedRiderId} position={oldPosition}->{newPosition}");
            }
        }
    }

    private static void CaptureSegmentOrder(
        int lap,
        int segmentId,
        IReadOnlyList<RiderState> ordered,
        SimLog log)
    {
        var entries = BuildOrderEntries(ordered);
        log.Add(new SegmentOrderSnapshot(lap, segmentId, entries));
        log.Add($"SEGMENT_ORDER lap={lap} seg={segmentId} {FormatOrder(entries)}");
    }

    private static void CaptureLapOrder(
        int lap,
        int segmentId,
        IReadOnlyList<RiderState> ordered,
        SimLog log)
    {
        var entries = BuildOrderEntries(ordered);

        log.Add(new RaceOrderSnapshot(lap, segmentId, entries));
        log.Add($"LAP_ORDER lap={lap} seg={segmentId} {FormatOrder(entries)}");
    }

    private static RiderOrderEntry[] BuildOrderEntries(IReadOnlyList<RiderState> ordered)
    {
        var leader = ordered.FirstOrDefault(rider => !rider.IsCrashed);
        var leaderTime = leader?.ElapsedTimeSeconds;
        return ordered
            .Select((rider, index) => new RiderOrderEntry(
                rider.RiderId,
                index + 1,
                rider.IsCrashed || leaderTime is null
                    ? float.PositiveInfinity
                    : MathF.Max(0f, rider.ElapsedTimeSeconds - leaderTime.Value),
                rider.IsCrashed))
            .ToArray();
    }

    private static string FormatOrder(IReadOnlyList<RiderOrderEntry> entries)
        => string.Join(" | ", entries.Select(entry =>
            entry.Crashed
                ? $"{entry.Position}:rider={entry.RiderId} DNF"
                : $"{entry.Position}:rider={entry.RiderId} gap={entry.GapSeconds.ToString("F2", CultureInfo.InvariantCulture)}s"));

    private static RiderState[] BuildRunningOrder(IReadOnlyList<RiderState> riders)
        => riders
            .OrderBy(rider => rider.IsCrashed)
            .ThenBy(rider => rider.IsCrashed ? float.MaxValue : rider.ElapsedTimeSeconds)
            .ThenByDescending(rider => rider.DistanceMeters)
            .ThenBy(rider => rider.RiderId)
            .ToArray();
}
