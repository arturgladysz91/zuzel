using CoreSim;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;
using CoreSim.Race;

namespace CausalAudit;

// One initial exposure only. No public feature flag or HeatSimulator integration.
// A full causal heat requires a new commit watermark and resumable production cursor.
internal static class StartFrontierPrototype
{
    internal sealed record Result(ResolvedSimulationStep EndpointC, SimulationSnapshot PreImpact,
        SimulationSnapshot? PostImpact, PhysicalContactConsequencePlan? EventPlan,
        ResolvedSimulationStep Executed, ContestedSpaceReport? Verification,
        int EventFrontiers, int PartialTraversals, int PostImpactReplays)
    {
        private bool _committed;
        internal InteractionEpisodeTracker? Owner { get; init; }
        internal string? OwnerFingerprint { get; init; }
        internal void Commit(SimulationEngine engine, IReadOnlyList<RiderState> riders, TrackState surface, SimLog log)
        {
            if (_committed) throw new InvalidOperationException("Prototype already committed.");
            var current = engine.CaptureSnapshot(PreImpact.Track, surface, riders, PreImpact.Step);
            if (RaceReadiness.Exact.Hash(current) != RaceReadiness.Exact.Hash(PreImpact)
                || Owner is not null && RaceReadiness.Exact.Hash(Fixtures.State(Owner)) != OwnerFingerprint)
                throw new InvalidOperationException("Prototype resolution no longer owns its input state.");
            engine.Commit(Executed, riders, surface, log);
            _committed = true;
        }
    }

    internal static Result Resolve(SimulationEngine engine, SimulationSnapshot snapshot,
        IReadOnlyList<RiderIntent> intents, HeatSimulationOptions options, InteractionEpisodeTracker owner)
    {
        options.Validate();
        if (!options.EnableContestedSpaceResponses || !options.EnablePhysicalContactConsequences || snapshot.Step.UseLegacyPhysics)
            throw new UnsupportedCausalReplayException("RequiresPhysicalContestedExecution");
        // C resolves on its own clone; abandoning either result never commits a forecast.
        var endpoint = engine.Resolve(snapshot, intents, options, owner);
        var plan = endpoint.Interaction?.PhysicalContactConsequences;
        if (plan is null || plan.AppliedPairs.Count == 0)
        {
            if (endpoint.Interaction?.Episodes.Any(e => e.UnresolvedMechanicalContacts.Count != 0) == true)
                throw new UnsupportedCausalReplayException("UnappliedContactIsNotNoContact");
            return new(endpoint, snapshot, null, null, endpoint, null, 0, 0, 0)
                { Owner = owner, OwnerFingerprint = RaceReadiness.Exact.Hash(Fixtures.State(owner)) };
        }
        var time = plan.AppliedPairs.Min(p => p.FirstTouchCommonTimeSeconds);
        CausalBoundary.RequireCommonStart(snapshot, time);
        if (owner.History.Count != 0 || owner.PhysicalPairs.Count != 0 || owner.EpisodeCount != 0)
            throw new UnsupportedCausalReplayException("InitialExposureOnly");
        if (snapshot.Riders.Count is < 2 or > 4 || snapshot.Riders.Any(r => !r.IsActive || r.Speed <= 0 || r.ContactRecovery is not null)
            || snapshot.Segment.Type != SegmentType.Straight || snapshot.Segment.IsStandingStartSegment
            || options.IncidentFrequency != 0)
            throw new UnsupportedCausalReplayException("RequiresRollingStraightWithoutIncomingRecoveryOrIncidents");
        if (plan.AppliedPairs.Any(p => p.FirstTouchCommonTimeSeconds != time || p.FrontierStartTimeSeconds != time)
            || plan.AppliedPairs.Select(p => string.Join(",", p.FrontierRiderIds)).Distinct().Count() != 1)
            throw new UnsupportedCausalReplayException("RequiresOneExactCommonTimeFrontier");
        var analysis = endpoint.Interaction!.PhysicalContactAnalysis
            ?? throw new UnsupportedCausalReplayException("RequiresRetainedAnalysis");
        var inputs = analysis.SourceInputs.Where(i => plan.AppliedPairs.Any(p => p.RiderA == i.Contact.RiderA && p.RiderB == i.Contact.RiderB)).ToArray();
        if (inputs.Length != plan.AppliedPairs.Count || inputs.Any(i => i.PoseA is null || i.PoseB is null
                || !i.PoseA.DeterministicArithmetic || !i.PoseB.DeterministicArithmetic)
            || plan.AppliedPairs.Any(p => p.Status != PhysicalContactStatus.Analyzed || p.Manifold!.SignedSeparationMeters < -GeometryNumerics.ContactDistanceMeters
                || p.DemandA!.LateralDemand != 0 || p.DemandB!.LateralDemand != 0 || p.DemandA.YawDemand != 0 || p.DemandB.YawDemand != 0))
            throw new UnsupportedCausalReplayException("ScalarLongitudinalTouchOnly");
        var selected = snapshot.Riders.Select(r => endpoint.Interaction.Episodes.SelectMany(e => e.SelectedResponses)
            .LastOrDefault(a => a.RiderId == r.RiderId)
            ?? throw new UnsupportedCausalReplayException("MissingSelectedProductionRequest")).ToArray();
        if (selected.Any(a => !a.HoldLateralPosition || a.Intent.EntryTarget != snapshot.Rider(a.RiderId).Lane
            || a.Intent.ApexTarget != a.Intent.EntryTarget || a.Intent.ExitTarget != a.Intent.EntryTarget
            || snapshot.Rider(a.RiderId).LateralPosition != a.Intent.EntryTarget))
            throw new UnsupportedCausalReplayException("FixedHeldStraightOnly");
        if (plan.AppliedPairs[0].FrontierRiderIds.Count != snapshot.Riders.Count)
            throw new UnsupportedCausalReplayException("CompleteFrontierParticipantsRequired");
        var directions = snapshot.Riders.ToDictionary(r => r.RiderId, r =>
        {
            var input = inputs.First(i => i.Contact.RiderA == r.RiderId || i.Contact.RiderB == r.RiderId);
            var pose = input.Contact.RiderA == r.RiderId ? input.PoseA! : input.PoseB!;
            return ContactFrameArithmetic.Direction(pose.Attitude.TravelHeadingRadians, true);
        });
        // Build the accepted resolver plan on zero-distance/time event states, not C endpoints.
        var eventChanges = snapshot.Riders.Select(r => new RiderStateChange(r.RiderId, r.Lane, r.Lane, r.Lane, r.Lane,
            r.LateralPosition, r.Speed, r.Risk, r.Status, r.ElapsedTimeSeconds, r.Position, r.LastResolvedSegmentId,
            r.Morale, SegmentOutcome.Ok, r.Speed, r.Speed, false)).ToArray();
        var eventPlan = PhysicalContactConsequenceResolver.Build(analysis, analysis.ApplicationRiders, plan.AppliedPairs,
            snapshot, eventChanges, directions, options.PhysicalContactParameters, options.PhysicalContactConsequenceParameters);
        if (eventPlan.Riders.Count != snapshot.Riders.Count)
            throw new UnsupportedCausalReplayException("CompleteFrontierParticipantsRequired");
        var post = new SimulationSnapshot(snapshot.Step, snapshot.Track, snapshot.TrackState, snapshot.Riders.Select(r =>
        {
            var c = eventPlan.Riders.Single(c => c.RiderId == r.RiderId);
            if (c.Severity != PhysicalContactSeverity.Crash && c.PostContactSpeed <= 0)
                throw new UnsupportedCausalReplayException("StoppedRiderContinuationRequired");
            return r with { Speed = c.PostContactSpeed, Status = c.Severity == PhysicalContactSeverity.Crash ? RiderRaceStatus.Crashed : r.Status,
                ContactRecovery = c.Recovery };
        }));
        var replayIntents = selected.Where(a => post.Rider(a.RiderId).IsActive).Select(a => new RiderIntent(a.RiderId,
            new RiderDecision(a.Intent.EntryTarget) { Trajectory = a.Intent, DriveControl = a.DriveControl, HoldLateralPosition = true })).ToArray();
        var replay = engine.ResolveProduction(post, replayIntents, options, legacyContacts: false);
        var changes = new List<RiderStateChange>(); var diagnostics = new List<RiderStepDiagnostics>(); var motions = new List<ResolvedRiderMotion>();
        foreach (var rider in snapshot.Riders)
        {
            var c = eventPlan.Riders.Single(c => c.RiderId == rider.RiderId);
            RiderStateChange change; RiderStepDiagnostics d;
            if (c.Severity == PhysicalContactSeverity.Crash)
            {
                change = eventChanges.Single(r => r.RiderId == rider.RiderId) with { Speed = 0, PhysicsSpeed = 0,
                    Status = RiderRaceStatus.Crashed, Outcome = SegmentOutcome.Crash, ApplySurfaceWear = false };
                d = new(rider.RiderId, 0, 0, rider.Speed, null, null, null, null, null,
                    snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, rider.LateralPosition));
                motions.Add(ResolvedRiderMotion.Create(snapshot, rider, change, change, d, null));
            }
            else
            {
                change = replay.Changes.Single(r => r.RiderId == rider.RiderId);
                // Remainder impairment was consumed by production. Old one-next-active-step mapping is retained separately.
                change = change with { ContactRecovery = change.Status == RiderRaceStatus.Racing ? c.Recovery : null };
                d = replay.Diagnostics.Single(r => r.RiderId == rider.RiderId);
                motions.Add(replay.Motions.Single(r => r.RiderId == rider.RiderId));
            }
            changes.Add(change); diagnostics.Add(d with { PhysicalContactConsequence = c, FinalSpeedMetersPerSecond = change.Speed, FinalStatus = change.Status });
        }
        var staged = owner.Clone(); staged.Bind(snapshot, true);
        var episode = staged.Engage(snapshot.Riders.Select(r => r.RiderId).ToArray(), time, InteractionContext.MechanicalConflict);
        if (eventPlan.AppliedPairs.Any(p => p.EpisodeId != episode.Id))
            throw new UnsupportedCausalReplayException("InitialEpisodeIdentityRequired");
        var poses = motions.SelectMany(m => ResolvedBikePoses.FromMotion(m, snapshot.Track, embedding: staged.Embedding)).ToArray();
        if (poses.Length != 0 && !ContestedSpaceInteractionCoordinator.WithinTrack(poses, snapshot.Track, staged.Embedding!))
            throw new UnsupportedCausalReplayException("PostImpactFootprintNotCertified");
        var verified = CommonTimePoseHistory.Observe(poses);
        if (verified.FrameCoverageGaps.Count != 0 || verified.Intervals.Any(i => !i.NumericallyResolved || i.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)))
            throw new UnsupportedCausalReplayException("PostImpactCoverageNotCertified");
        staged.ConsumePhysical(eventPlan);
        var observation = new PhysicalPairObservation(verified, options.ContestedSpaceParameters);
        // #55 reports overlap in multiple node pieces. Only certified separation can rearm;
        // a later observation of the consumed persistent touch is not a new event.
        foreach (var contact in verified.Intervals.Where(i => i.EligibleForFutureInteraction))
        {
            var pair = eventPlan.AppliedPairs.SingleOrDefault(p => p.RiderA == contact.RiderA && p.RiderB == contact.RiderB);
            if (pair is null || contact.FirstTouchCommonTimeSeconds > time && staged.CanApplyPhysical(pair with
                { FirstTouchCommonTimeSeconds = contact.FirstTouchCommonTimeSeconds!.Value,
                    FrontierStartTimeSeconds = contact.FirstTouchCommonTimeSeconds }, observation))
                throw new UnsupportedCausalReplayException("LaterContactRequiresRecomputedEventLoop");
        }
        staged.ObservePhysicalClearance(verified, options.ContestedSpaceParameters);
        var events = replay.Events.Concat(eventPlan.Riders.Select(c => new SimulationStepEvent(snapshot.Step.StepNumber, 0, c.RiderId,
            c.Severity == PhysicalContactSeverity.Crash ? SimulationEventType.ContactCrash : c.Severity == PhysicalContactSeverity.Brush
                ? SimulationEventType.ContactBrush : c.Severity == PhysicalContactSeverity.Disturbed ? SimulationEventType.ContactDisturbed
                : c.Severity == PhysicalContactSeverity.LostRhythm ? SimulationEventType.ContactLostRhythm : SimulationEventType.ContactMajorSave,
            "Initial-frontier prototype") { PhysicalContactConsequence = c }));
        var executed = new ResolvedSimulationStep(snapshot, changes, events, diagnostics, motions);
        staged.RetainVerified(executed, poses, verified);
        executed = new ResolvedSimulationStep(snapshot, changes, events, diagnostics, motions)
        {
            CommitInteractionState = () => owner.CommitFrom(staged),
        };
        return new(endpoint, snapshot, post, eventPlan, executed, verified, 1, 0, 1)
            { Owner = owner, OwnerFingerprint = RaceReadiness.Exact.Hash(Fixtures.State(owner)) };
    }
}
