namespace CoreSim.Decisions;

/// <summary>Manager-scale phase targets. Actual continuous motion may never reach them.</summary>
public readonly record struct TrajectoryIntent
{
    public int EntryTarget { get; }
    public int ApexTarget { get; }
    public int ExitTarget { get; }
    public TrajectoryIntent(int entryTarget, int apexTarget, int exitTarget)
    {
        LaneModel.ValidateLane(entryTarget);
        LaneModel.ValidateLane(apexTarget);
        LaneModel.ValidateLane(exitTarget);
        EntryTarget = entryTarget; ApexTarget = apexTarget; ExitTarget = exitTarget;
    }
    public int TargetFor(SegmentType type) => type switch
    {
        SegmentType.TurnMiddle => ApexTarget,
        SegmentType.TurnExit => ExitTarget,
        _ => EntryTarget,
    };
    public override string ToString() => $"E{EntryTarget}-A{ApexTarget}-X{ExitTarget}";
}

public static class TrajectoryCandidates
{
    public const int FullPlanMaximum = 35;
    public const int MiddlePlanMaximum = 13;
    public const int ExitPlanMaximum = 5;
    /// <summary>Canonical tuples; completed phases are collapsed, never fictitious history.</summary>
    public static IReadOnlyList<TrajectoryIntent> Generate(SegmentType currentPhase)
    {
        var candidates = new List<TrajectoryIntent>();
        for (var entry = 0; entry <= LaneModel.MaxLane; entry++)
        {
            if (currentPhase == SegmentType.TurnExit)
            {
                candidates.Add(new(entry, entry, entry)); continue;
            }
            if (currentPhase == SegmentType.TurnMiddle)
            {
                foreach (var exit in Adjacent(entry)) candidates.Add(new(entry, entry, exit));
                continue;
            }
            foreach (var apex in Adjacent(entry))
            foreach (var exit in Adjacent(apex)) candidates.Add(new(entry, apex, exit));
        }
        return candidates.AsReadOnly();
    }
    private static IEnumerable<int> Adjacent(int anchor)
    {
        for (var lane = Math.Max(0, anchor - 1); lane <= Math.Min(4, anchor + 1); lane++) yield return lane;
    }
}
