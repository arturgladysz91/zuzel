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
    private IReadOnlyDictionary<int, bool> _previousDnfState = new Dictionary<int, bool>();

    public void InitializeStartingGrid(IReadOnlyList<RiderState> riders)
    {
        ArgumentNullException.ThrowIfNull(riders);
        _previousOrder = riders
            .OrderBy(rider => rider.Lane)
            .ThenBy(rider => rider.RiderId)
            .Select(rider => rider.RiderId)
            .ToArray();
        _previousDnfState = riders.ToDictionary(rider => rider.RiderId, IsDnf);
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
        var currentDnfState = ordered.ToDictionary(rider => rider.RiderId, IsDnf);

        if (_previousOrder.Count > 0)
            CaptureOvertakes(lap, segmentId, ordered, currentPositions, currentDnfState, log);

        if (lapComplete)
            CaptureLapOrder(lap, segmentId, ordered, log);

        _previousOrder = ordered.Select(rider => rider.RiderId).ToArray();
        _previousDnfState = currentDnfState;
    }

    private void CaptureOvertakes(
        int lap,
        int segmentId,
        IReadOnlyList<RiderState> ordered,
        IReadOnlyDictionary<int, int> currentPositions,
        IReadOnlyDictionary<int, bool> currentDnfState,
        SimLog log)
    {
        var previousPositions = _previousOrder
            .Select((riderId, index) => new { RiderId = riderId, Position = index + 1 })
            .ToDictionary(item => item.RiderId, item => item.Position);

        foreach (var rider in ordered.Where(rider => !IsDnf(rider)))
        {
            if (!previousPositions.TryGetValue(rider.RiderId, out var oldPosition))
                continue;

            var newPosition = currentPositions[rider.RiderId];
            if (newPosition >= oldPosition
                || _previousDnfState.GetValueOrDefault(rider.RiderId))
                continue;

            foreach (var passedRiderId in _previousOrder.Take(oldPosition - 1))
            {
                if (passedRiderId == rider.RiderId
                    || _previousDnfState.GetValueOrDefault(passedRiderId)
                    || currentDnfState.GetValueOrDefault(passedRiderId)
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
                log.Add($"OVERTAKE lap={lap} seg={segmentId} rider={rider.RiderId} passed={passedRiderId} position={oldPosition}->{newPosition}");
            }
        }
    }

    private static void CaptureLapOrder(
        int lap,
        int segmentId,
        IReadOnlyList<RiderState> ordered,
        SimLog log)
    {
        var leader = ordered.FirstOrDefault(rider => !IsDnf(rider));
        var leaderTime = leader?.ElapsedTimeSeconds;
        var entries = ordered
            .Select((rider, index) => new RiderOrderEntry(
                rider.RiderId,
                index + 1,
                IsDnf(rider) || leaderTime is null
                    ? float.PositiveInfinity
                    : MathF.Max(0f, rider.ElapsedTimeSeconds - leaderTime.Value),
                IsDnf(rider)))
            .ToArray();

        var orderText = string.Join(" | ", entries.Select(entry =>
            entry.Crashed
                ? $"{entry.Position}:rider={entry.RiderId} DNF"
                : $"{entry.Position}:rider={entry.RiderId} gap={entry.GapSeconds.ToString("F2", CultureInfo.InvariantCulture)}s"));

        log.Add(new RaceOrderSnapshot(lap, segmentId, entries));
        log.Add($"LAP_ORDER lap={lap} seg={segmentId} {orderText}");
    }

    private static RiderState[] BuildRunningOrder(IReadOnlyList<RiderState> riders)
        => riders
            .OrderByDescending(rider => rider.Status == RiderRaceStatus.Finished)
            .ThenByDescending(rider => rider.CanonicalProgress)
            .ThenBy(rider => IsDnf(rider) ? float.MaxValue : rider.ElapsedTimeSeconds)
            .ThenBy(rider => rider.RiderId)
            .ToArray();

    private static bool IsDnf(RiderState rider)
        => rider.Status is RiderRaceStatus.Crashed or RiderRaceStatus.Retired;
}
