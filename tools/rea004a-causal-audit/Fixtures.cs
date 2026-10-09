using CoreSim;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using CoreSim.Setup;
using RaceReadiness;

namespace CausalAudit;

internal static class Fixtures
{
    internal sealed class Hold : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane); }
    internal static HeatSimulationOptions Options => new() { Seed = 19, EnableLogging = false, IncidentFrequency = 0,
        EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = true,
        PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit, InteractionDiagnostics = InteractionDiagnosticsLevel.FullAudit };
    internal static SimulationSnapshot Rear(float closing = 1, float ahead = .105f, int count = 2, float lateralGap = 0)
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight, 20), new TrackSegment(1, SegmentType.Straight, 20) }, TrackGeometry.Default);
        var riders = Enumerable.Range(1, count).Select(id => new RiderSnapshot(id, RiderProfile.CreateDefault(id),
            RiderPosition.Create(1, 0, ahead * (id - 1), 2), -1, 2, 2 + lateralGap * (id - 1),
            15 + closing * (count - id), 0, RiderRaceStatus.Racing, 0, BikeSetup.Neutral, .5f, .5f));
        return new(new(91, 0, 0, 0, 19, 4), track, new TrackState(2, 5, (_, _) => new(1, 0, 0)).Snapshot(), riders);
    }
    internal static object State(InteractionEpisodeTracker tracker) => new { Pairs = tracker.PhysicalPairs.OrderBy(p => p.Key).Select(p => new { p.Key.A, p.Key.B, p.Value }).ToArray(),
        History = tracker.History.Select(p => new { p.RiderId, p.StartTimeSeconds, p.EndTimeSeconds }).ToArray(), tracker.EpisodeCount };
}

internal sealed record ContactTiming(int Step, int RiderId, int OtherRiderId, double FirstTouchSeconds, double FrontierSeconds,
    float CurrentSegmentStartSeconds, float CurrentEndpointSeconds, float? AppliedAtSeconds, double? ApplicationDelaySeconds, bool HistoricalParticipant,
    int EventSegmentIndex, SegmentType EventSegmentType, RiderMotionSample EventSample, PhysicalBikePose EventPose,
    MeterPoint DetectorVelocityMetersPerSecond, double EventSegmentStartSeconds, double EventSegmentEndSeconds, double RemainingTimeSeconds, double RemainingDistanceMeters,
    PhysicalContactPairAnalysis Pair, RiderContactConsequence? Consequence, float? NextRecoveryDurationSeconds);

// Captures actual C Resolve/Commit. Samples are observation of #53/#55 nodes; never a replay prefix.
internal sealed class TimingObserver : ISimulationStepObserver
{
    internal readonly List<ResolvedSimulationStep> Steps = new();
    internal readonly List<ContactTiming> Contacts = new();
    internal readonly List<object> Frontiers = new();
    internal readonly List<object> VerifiedContacts = new();
    public void OnStepResolved(ResolvedSimulationStep step)
    {
        Steps.Add(step);
        foreach (var input in step.Interaction?.PhysicalContactAnalysis?.SourceInputs ?? Array.Empty<PhysicalContactInput>())
            VerifiedContacts.Add(new { Step = step.Snapshot.Step.StepNumber, input.Contact.RiderA, input.Contact.RiderB,
                input.Contact.FirstTouchCommonTimeSeconds, input.Contact.IntervalStartSeconds, input.Contact.IntervalEndSeconds,
                input.Contact.Kind, input.Contact.NumericallyResolved });
        var plan = step.Interaction?.PhysicalContactConsequences;
        if (plan is null || plan.AppliedPairs.Count == 0) return;
        var allMotions = Steps.SelectMany(s => s.Motions).ToArray();
        var sources = step.Interaction!.PhysicalContactAnalysis!.SourceInputs;
        foreach (var frontier in plan.AppliedPairs.GroupBy(p => (p.FrontierStartTimeSeconds, Riders: string.Join(",", p.FrontierRiderIds))))
        {
            var time = frontier.Key.FrontierStartTimeSeconds!.Value;
            string? refusal = null;
            try { CausalBoundary.RequireCommonStart(step.Snapshot, time); }
            catch (UnsupportedCausalReplayException e) { refusal = e.Message; }
            Frontiers.Add(new { Step = step.Snapshot.Step.StepNumber, Time = time, Participants = frontier.First().FrontierRiderIds,
                PairCount = frontier.Count(), Refusal = refusal, CommittedClocks = step.Snapshot.Riders.Select(r => new { r.RiderId, r.ElapsedTimeSeconds }).ToArray() });
        }
        foreach (var pair in plan.AppliedPairs)
        {
            var time = pair.FirstTouchCommonTimeSeconds;
            var source = sources.Single(i => i.Contact.RiderA == pair.RiderA && i.Contact.RiderB == pair.RiderB);
            foreach (var id in new[] { pair.RiderA, pair.RiderB })
            {
                var motion = allMotions.Where(m => m.RiderId == id && m.StartElapsedTimeSeconds <= time
                    && (double)m.StartElapsedTimeSeconds + m.TotalTimeSeconds >= time).OrderByDescending(m => m.StartElapsedTimeSeconds).First();
                var sample = motion.SampleAtTime((float)Math.Clamp(time - motion.StartElapsedTimeSeconds, 0, motion.TotalTimeSeconds));
                var current = step.Snapshot.Rider(id);
                var endpoint = step.Changes.Single(c => c.RiderId == id).ElapsedTimeSeconds;
                var consequence = plan.Riders.SingleOrDefault(c => c.RiderId == id);
                var applied = consequence is null ? (float?)null : endpoint;
                Contacts.Add(new(step.Snapshot.Step.StepNumber, id, id == pair.RiderA ? pair.RiderB : pair.RiderA, time,
                    pair.FrontierStartTimeSeconds!.Value, current.ElapsedTimeSeconds, endpoint, applied, applied - time,
                    time < current.ElapsedTimeSeconds, motion.SegmentIndex, step.Snapshot.Track.Segments[motion.SegmentIndex].Type,
                    sample, id == pair.RiderA ? source.PoseA! : source.PoseB!, id == pair.RiderA ? source.VelocityA : source.VelocityB, motion.StartElapsedTimeSeconds,
                    (double)motion.StartElapsedTimeSeconds + motion.TotalTimeSeconds,
                    (double)motion.StartElapsedTimeSeconds + motion.TotalTimeSeconds - time,
                    motion.TotalDistanceMeters - (double)sample.TravelledMeters, pair, consequence, null));
            }
        }
    }
    internal ContactTiming[] Complete() => Contacts.Select(c => c with { NextRecoveryDurationSeconds = Steps
        .Where(s => s.Snapshot.Step.StepNumber > c.Step && s.Snapshot.Riders.Any(r => r.RiderId == c.RiderId && r.ContactRecovery?.SourceFrontierTime == c.FrontierSeconds))
        .SelectMany(s => s.Motions.Where(m => m.RiderId == c.RiderId)).Select(m => (float?)m.TotalTimeSeconds).FirstOrDefault() }).ToArray();
}
