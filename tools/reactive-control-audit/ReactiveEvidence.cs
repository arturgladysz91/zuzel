using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using MidSegmentControl;

namespace ReactiveControl;

internal static class ReactiveEvidence
{
    internal static ControlCases.Context Pair(string name, bool reverse = false)
    {
        var scenarios = ContestedSpaceResponseEvidence.Scenarios();
        var input = scenarios.Single(s => s.Name == "D-inside-overlap");
        input = name switch
        {
            "A" => input with { Riders = input.Riders.Select(r => r with { Lateral = r.Id == 1 ? 1.2f : 1.6f }).ToArray() },
            "B" => scenarios.Single(s => s.Name == "edge-trapped") with { SegmentIndex = 3 },
            "C" => input with { Riders = input.Riders.Select(r => r with { Progress = .75f,
                Lateral = r.Id == 1 ? 1 : 1.31f, Attack = 99, Defense = 99, Combativeness = 1 }).ToArray() },
            "D" => input with { Riders = new[] { new ContestedRiderInput(1, 1, .125f, 21, new(1, 1, 1)),
                new ContestedRiderInput(2, 1.8f, .125f, 21, new(2, 2, 2)) } },
            _ => throw new ArgumentException("Unknown fixture.", nameof(name)),
        };
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(input, reverse);
        var intents = snapshot.Riders.Select(r => new RiderIntent(r.RiderId,
            new(input.Riders.Single(i => i.Id == r.RiderId).Intent.TargetFor(snapshot.Segment.Type)))).ToArray();
        return new(snapshot, intents, ControlCases.Prepare("straight").Engine);
    }
#if !BASELINE
    internal sealed class MissingPeerTime(ExecutedPathNode own, double peerTime) : InvalidOperationException("No accepted peer state at this observation time.")
    {
        internal ExecutedPathNode OwnPoint { get; } = own;
        internal double PeerTime { get; } = peerTime;
    }
    internal sealed record BlockedPair(string Scenario, int RiderId, string Status, double AttemptedAt, double PeerAvailableAt,
        double AcceptedCommonTime, ExecutedPathNode OwnTentativePoint, int SourceCalls);
    internal static BlockedPair AttemptPair(string name, bool reverse = false)
        => AttemptPair(Pair(name, reverse), name);
    internal static BlockedPair AttemptPair(ControlCases.Context context, string name)
    {
        var calls = 0; var riderId = name == "B" ? 1 : 2;
        // Snapshot is the ONLY published pair state. Do not inspect the first
        // rider's already computed future segment or its future intent.
        var peerTime = (double)context.Snapshot.Rider(riderId == 1 ? 2 : 1).ElapsedTimeSeconds;
        var origin = (double)context.Snapshot.Rider(riderId).ElapsedTimeSeconds;
        ScheduledControlChange? Observe(ExecutedPathNode own)
        {
            calls++;
            if (origin + own.ElapsedTimeSeconds != peerTime) throw new MissingPeerTime(own, peerTime);
            throw new InvalidOperationException("This fixture requires a genuinely new, same-time observation.");
        }
        var attempted = context with { Intents = context.Intents.Select(i => i.RiderId != riderId ? i : i with
            { Decision = i.Decision with { ControlSource = Observe } }).ToArray() };
        try { ControlCases.Run(attempted); throw new InvalidOperationException("A future-path observation was incorrectly accepted."); }
        catch (MissingPeerTime blocked)
        {
            return new(name, riderId, "BlockedBeforePairObservation", origin + blocked.OwnPoint.ElapsedTimeSeconds,
                blocked.PeerTime, 0, blocked.OwnPoint, calls);
        }
    }
    // Positive plumbing test only, NOT a threat detector or a second #56B policy.
    // No command exists until the actual own speed/position crosses this condition.
    internal static Func<ExecutedPathNode, ScheduledControlChange?> DeliverySource(ControlCases.Context context,
        bool lateral, Action<double, double>? issued = null, Action? examined = null)
    {
        var rider = context.Snapshot.Riders.Single();
        var delay = LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(rider.Profile.Skills);
        return node =>
        {
            examined?.Invoke();
            if (lateral ? node.LateralPosition >= 1.55f : node.SpeedMetersPerSecond < 21) return null;
            var observed = (double)rider.ElapsedTimeSeconds + node.ElapsedTimeSeconds;
            issued?.Invoke(observed, observed + delay);
            return new(rider.RiderId, observed + delay, lateral ? new(1) : RiderDriveControl.LiftThrottle,
                lateral ? false : null, lateral ? new InteractionLateralTarget(9) : null);
        };
    }
#endif
}
