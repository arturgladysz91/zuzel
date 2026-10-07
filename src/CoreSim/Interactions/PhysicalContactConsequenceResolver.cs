using CoreSim.PhysicalSpace;
using CoreSim.Race;

namespace CoreSim.Interactions;

/// <summary>Applied design calibration; no independent impulse or ability model.</summary>
public sealed record PhysicalContactConsequenceParameters
{
    public double MaximumRecoverableControlLoss01 { get; init; } = .8;
    public double ControlLossExponent => 2;

    public void Validate()
    {
        GeometryValidation.Unit(MaximumRecoverableControlLoss01, nameof(MaximumRecoverableControlLoss01));
    }

    public double ControlLoss(double ratio, PhysicalContactParameters scale)
    {
        Validate(); scale.Validate(); GeometryValidation.Nonnegative(ratio, nameof(ratio));
        var normalized = Math.Min(1, ratio / scale.MajorSaveThreshold);
        // Fixed quadratic arithmetic avoids platform-specific libm power evaluation.
        return MaximumRecoverableControlLoss01 * (normalized * normalized);
    }
}

/// <summary>Immutable transient state, consumed by exactly one active production step.</summary>
public sealed record ContactRecoveryState
{
    public long? SourceEpisodeId { get; }
    public double SourceFrontierTime { get; }
    public double SeverityRatio { get; }
    public PhysicalContactSeverity Severity { get; }
    public double ControlLoss01 { get; }
    public double DriveAvailability01 => 1 - ControlLoss01;
    public double LateralAuthority01 => 1 - ControlLoss01;
    public int RemainingSteps => 1;

    public ContactRecoveryState(long? sourceEpisodeId, double sourceFrontierTime, double severityRatio,
        PhysicalContactSeverity severity, double controlLoss01)
    {
        GeometryValidation.Nonnegative(sourceFrontierTime, nameof(sourceFrontierTime));
        GeometryValidation.Nonnegative(severityRatio, nameof(severityRatio));
        GeometryValidation.Unit(controlLoss01, nameof(controlLoss01));
        if (severity is not (PhysicalContactSeverity.Disturbed or PhysicalContactSeverity.LostRhythm or PhysicalContactSeverity.MajorSave))
            throw new ArgumentOutOfRangeException(nameof(severity));
        SourceEpisodeId = sourceEpisodeId; SourceFrontierTime = sourceFrontierTime;
        SeverityRatio = severityRatio; Severity = severity; ControlLoss01 = controlLoss01;
    }
}

public sealed record RiderContactConsequence(int RiderId, IReadOnlyList<int> ContactRiderIds,
    IReadOnlyList<long> SourceEpisodeIds, IReadOnlyList<long> OriginEpisodeIds, double FrontierTimeSeconds,
    double SeverityRatio, PhysicalContactSeverity Severity, MeterPoint NetDeltaVelocityMetersPerSecond,
    double DeltaForwardMetersPerSecond, float PreContactSpeed, float PostContactSpeed,
    double ControlLoss01, ContactRecoveryState? Recovery);

public sealed record PhysicalContactConsequencePlan(IReadOnlyList<RiderContactConsequence> Riders,
    IReadOnlyList<PhysicalContactPairAnalysis> AppliedPairs, int RepeatedOverlapSuppressions)
{
    internal static readonly PhysicalContactConsequencePlan Empty = new(Array.Empty<RiderContactConsequence>(),
        Array.Empty<PhysicalContactPairAnalysis>(), 0);
}

/// <summary>Builds a simultaneous state-change plan from #56C1 outputs. No RNG or live state mutation.</summary>
public static class PhysicalContactConsequenceResolver
{
    internal static PhysicalContactConsequencePlan Build(PhysicalContactAnalysis analysis,
        IReadOnlyList<RiderContactAnalysis> applicableRiders, IReadOnlyList<PhysicalContactPairAnalysis> applicablePairs,
        SimulationSnapshot snapshot, IReadOnlyList<RiderStateChange> normalChanges,
        IReadOnlyDictionary<int, MeterPoint> forwardDirections, PhysicalContactParameters scale,
        PhysicalContactConsequenceParameters parameters, int suppressed = 0)
    {
        scale.Validate(); parameters.Validate();
        if (applicablePairs.Any(p => p.Status != PhysicalContactStatus.Analyzed || !analysis.AuditPairs.Contains(p)))
            throw new ArgumentException("Only analyzed causal-frontier contacts may be applied.");
        var changes = normalChanges.ToDictionary(c => c.RiderId);
        var result = new List<RiderContactConsequence>();
        foreach (var rider in applicableRiders.OrderBy(r => r.RiderId))
        {
            if (!changes.TryGetValue(rider.RiderId, out var change) || change.Status is RiderRaceStatus.Crashed or RiderRaceStatus.Retired)
                continue; // Solo terminal outcomes remain authoritative.
            var pairs = applicablePairs.Where(p => p.RiderA == rider.RiderId || p.RiderB == rider.RiderId).ToArray();
            if (pairs.Length == 0) continue;
            var forward = forwardDirections[rider.RiderId];
            if (Math.Abs(forward.Length - 1) > 1e-9) throw new ArgumentException("Travel direction must be a unit vector.");
            var severity = PhysicalContactAnalyzer.Classify(rider.SeverityRatio, scale);
            var loss = parameters.ControlLoss(rider.SeverityRatio, scale);
            var delta = MeterPoint.Dot(rider.NetDeltaVelocityMetersPerSecond, forward);
            var time = pairs.Min(p => p.FrontierStartTimeSeconds ?? p.FirstTouchCommonTimeSeconds);
            var episodes = pairs.Where(p => p.EpisodeId.HasValue).Select(p => p.EpisodeId!.Value).Distinct().Order().ToArray();
            ContactRecoveryState? recovery = severity is PhysicalContactSeverity.Brush or PhysicalContactSeverity.Crash
                || change.Status == RiderRaceStatus.Finished ? null
                : new(episodes.Length == 0 ? null : episodes[0], time, rider.SeverityRatio, severity, loss);
            if (recovery is not null && snapshot.Rider(rider.RiderId).ContactRecovery is { } prior && prior.ControlLoss01 > recovery.ControlLoss01)
                recovery = prior; // Bounded maximum; never multiply impairment factors.
            result.Add(new(rider.RiderId, pairs.SelectMany(p => p.FrontierRiderIds.Count == 0
                    ? new[] { p.RiderA, p.RiderB } : p.FrontierRiderIds).Distinct().Order().ToArray(),
                episodes, pairs.SelectMany(p => p.OriginEpisodeIds).Distinct().Order().ToArray(), time,
                rider.SeverityRatio, severity, rider.NetDeltaVelocityMetersPerSecond, delta, change.Speed,
                severity == PhysicalContactSeverity.Crash ? 0f : (float)Math.Max(0, change.Speed + delta), loss, recovery));
        }
        return new(result.AsReadOnly(), applicablePairs.ToArray(), suppressed);
    }

    internal static RiderStateChange Apply(RiderStateChange normal, RiderContactConsequence consequence)
    {
        if (normal.RiderId != consequence.RiderId || normal.Speed != consequence.PreContactSpeed)
            throw new InvalidOperationException("Consequence plan must materialize the same frozen production endpoint.");
        return normal with { Speed = consequence.PostContactSpeed,
            Status = consequence.Severity == PhysicalContactSeverity.Crash ? RiderRaceStatus.Crashed : normal.Status,
            ContactRecovery = consequence.Recovery };
    }
}
