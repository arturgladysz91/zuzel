using CoreSim.PhysicalSpace;

namespace CoreSim.Interactions;

/// <summary>Immutable, bounded heat ownership; no episode or fallback lifetime dependency.</summary>
internal sealed record PhysicalPairGeneration(long Generation, bool Consumed, double LastImpactSeconds,
    double ObservedUntilSeconds, double? ClearSinceSeconds, double ArmedAtSeconds)
{
    internal static readonly PhysicalPairGeneration NeverApplied = new(0, false, double.NegativeInfinity,
        double.NegativeInfinity, null, double.NegativeInfinity);

    internal bool CanApply(double touch, double? frontierStart)
        => !Consumed && touch > LastImpactSeconds && (frontierStart ?? touch) >= ArmedAtSeconds;

    internal PhysicalPairGeneration Consume(double touch) => this with
    {
        Consumed = true, LastImpactSeconds = touch, ObservedUntilSeconds = touch, ClearSinceSeconds = null,
        ArmedAtSeconds = double.IsFinite(ArmedAtSeconds) ? ArmedAtSeconds : touch
    };
}

/// <summary>
/// Reuses one final actual #55 report. Its interval-wide lower bounds, never
/// sampled positions or forecast paths, certify continuous post-impact release.
/// </summary>
internal sealed class PhysicalPairObservation
{
    private readonly Dictionary<(int A, int B), ContestedSpaceEvent[]> _rows;
    private readonly Dictionary<(int A, int B), FrameCoverageGap[]> _gaps;
    private readonly ContestedSpaceParameters _parameters;

    internal PhysicalPairObservation(ContestedSpaceReport report, ContestedSpaceParameters parameters)
    {
        _parameters = parameters;
        _rows = report.Intervals.GroupBy(r => InteractionEpisodeTracker.PhysicalPairKey(r.RiderA, r.RiderB))
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.IntervalStartSeconds).ThenBy(r => r.IntervalEndSeconds).ToArray());
        _gaps = report.FrameCoverageGaps.GroupBy(g => InteractionEpisodeTracker.PhysicalPairKey(g.RiderA, g.RiderB))
            .ToDictionary(g => g.Key, g => g.ToArray());
    }

    internal PhysicalPairGeneration Advance((int A, int B) key, PhysicalPairGeneration state,
        double beforeSeconds = double.PositiveInfinity)
    {
        if (!state.Consumed) return state;
        var rows = _rows.GetValueOrDefault(key, Array.Empty<ContestedSpaceEvent>());
        var gaps = _gaps.GetValueOrDefault(key, Array.Empty<FrameCoverageGap>());
        var cursor = state.ObservedUntilSeconds;
        var since = state.ClearSinceSeconds;
        if (rows.Length == 0)
        {
            var gapEnd = Math.Min(beforeSeconds, gaps.Select(g => g.EndCommonTimeSeconds).DefaultIfEmpty(cursor).Max());
            return gapEnd > cursor ? state with { ObservedUntilSeconds = gapEnd, ClearSinceSeconds = null } : state;
        }
        // Sorted endpoints also make duplicate/overlapping certificates harmless.
        var end = Math.Min(beforeSeconds, Math.Max(rows.Max(r => r.IntervalEndSeconds),
            gaps.Select(g => g.EndCommonTimeSeconds).DefaultIfEmpty(cursor).Max()));
        if (end <= cursor) return state;
        var boundaries = rows.SelectMany(r => new[] { r.IntervalStartSeconds, r.IntervalEndSeconds })
            .Concat(gaps.SelectMany(g => new[] { g.StartCommonTimeSeconds, g.EndCommonTimeSeconds }))
            .Where(t => t > cursor && t < end).Append(cursor).Append(end).Distinct().Order().ToArray();
        var active = new List<ContestedSpaceEvent>();
        var nextRow = 0;
        for (var index = 0; index + 1 < boundaries.Length; index++)
        {
            var start = boundaries[index]; var finish = boundaries[index + 1];
            if (gaps.Any(g => g.StartCommonTimeSeconds == start && g.EndCommonTimeSeconds == start)) since = null;
            active.RemoveAll(r => r.IntervalEndSeconds <= start);
            while (nextRow < rows.Length && rows[nextRow].IntervalStartSeconds <= start)
            {
                if (rows[nextRow].IntervalEndSeconds > start) active.Add(rows[nextRow]);
                nextRow++;
            }
            var clear = active.Count > 0 && active.All(r => r.IntervalEndSeconds >= finish && r.NumericallyResolved
                && !r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous) && !r.HasConflict
                && r.MinimumSeparationLowerBoundMeters > _parameters.ReleaseClearanceMeters)
                && !gaps.Any(g => g.StartCommonTimeSeconds < finish && g.EndCommonTimeSeconds > start);
            if (!clear) since = null;
            else
            {
                since ??= start;
                var armedAt = since.Value + _parameters.ReleaseDelaySeconds;
                var ambiguousEndpoint = gaps.Any(g => g.StartCommonTimeSeconds == finish && g.EndCommonTimeSeconds == finish);
                if (finish - since.Value >= _parameters.ReleaseDelaySeconds && (!ambiguousEndpoint || armedAt < finish))
                    return state with { Generation = checked(state.Generation + 1), Consumed = false,
                        ClearSinceSeconds = since, ObservedUntilSeconds = finish,
                        ArmedAtSeconds = since.Value + _parameters.ReleaseDelaySeconds };
                if (ambiguousEndpoint) since = null;
            }
            cursor = finish;
        }
        return state with { ObservedUntilSeconds = cursor, ClearSinceSeconds = since };
    }
}
