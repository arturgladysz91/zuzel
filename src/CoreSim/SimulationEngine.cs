using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CoreSim;

public enum SimulationEventType
{
    SegmentResolved,
    ContactCrash,
    ContactLostRhythm,
}

public sealed record RiderIntent(int RiderId, RiderDecision Decision);

public sealed record RiderStateChange(
    int RiderId,
    int BeforeLane,
    int PlannedLane,
    int TargetLane,
    int Lane,
    float LateralPosition,
    float Speed,
    float Risk,
    RiderRaceStatus Status,
    float ElapsedTimeSeconds,
    RiderPosition Position,
    int LastResolvedSegmentId,
    float Morale,
    SegmentOutcome Outcome,
    float EntrySpeed,
    float PhysicsSpeed,
    bool ApplySurfaceWear);

public sealed record SimulationStepEvent(
    int StepNumber,
    int Phase,
    int RiderId,
    SimulationEventType Type,
    string Text,
    int? OtherRiderId = null);

/// <summary>Typed values captured from the production resolution path for one rider.</summary>
public sealed record RiderStepDiagnostics(
    int RiderId,
    float TravelledMeters,
    float TravelTimeSeconds,
    float PeakSpeedMetersPerSecond,
    float? FullDriveEquilibriumSpeedMetersPerSecond,
    float? TurnExitNetAccelerationMetersPerSecondSquared,
    TurnExitDriveProfile? TurnExitDriveProfile,
    StraightSpeedProfile? StraightProfile,
    TurnEntryScrubProfile? TurnEntryScrubProfile,
    TrackSurfaceState EntrySurface,
    StandingStartLaunchProfile? StandingStartLaunchProfile = null,
    CornerSpeedCorrectionProfile? CornerSpeedCorrectionProfile = null,
    CornerPhaseContext? CornerPhaseContext = null,
    ContinuousCornerTraversalProfile? ContinuousCornerProfile = null,
    ExecutedSegmentPath? ExecutedPath = null);

public sealed class ResolvedSimulationStep
{
    private readonly ReadOnlyCollection<RiderStateChange> _changes;
    private readonly ReadOnlyCollection<SimulationStepEvent> _events;
    private readonly ReadOnlyCollection<RiderStepDiagnostics> _diagnostics;
    private readonly ReadOnlyCollection<ResolvedRiderMotion> _motions;

    public SimulationSnapshot Snapshot { get; }
    public IReadOnlyList<RiderStateChange> Changes => _changes;
    public IReadOnlyList<SimulationStepEvent> Events => _events;
    public IReadOnlyList<RiderStepDiagnostics> Diagnostics => _diagnostics;
    /// <summary>Exactly one segment-local, time-parametrized motion per active resolved rider.</summary>
    public IReadOnlyList<ResolvedRiderMotion> Motions => _motions;

    internal ResolvedSimulationStep(
        SimulationSnapshot snapshot,
        IEnumerable<RiderStateChange> changes,
        IEnumerable<SimulationStepEvent> events,
        IEnumerable<RiderStepDiagnostics> diagnostics,
        IEnumerable<ResolvedRiderMotion> motions)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        ProjectionCaptureAudit.Record(ProjectionMaterialization.ResolvedStep);
        _changes = Array.AsReadOnly(changes.OrderBy(change => change.RiderId).ToArray());
        _events = Array.AsReadOnly(events
            .OrderBy(item => item.StepNumber)
            .ThenBy(item => item.Phase)
            .ThenBy(item => item.RiderId)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.OtherRiderId)
            .ToArray());
        _diagnostics = Array.AsReadOnly(diagnostics
            .OrderBy(item => item.RiderId)
            .ToArray());
        _motions = Array.AsReadOnly(motions.OrderBy(item => item.RiderId).ToArray());

        if (!_changes.Select(item => item.RiderId)
                .SequenceEqual(_diagnostics.Select(item => item.RiderId))
            || !_changes.Select(item => item.RiderId).SequenceEqual(_motions.Select(item => item.RiderId)))
        {
            throw new ArgumentException(
                "Diagnostics and motions must contain exactly one item for every rider change.",
                nameof(diagnostics));
        }
    }
}

/// <summary>
/// Executes one segment as CaptureSnapshot -> Decide -> Resolve -> Commit.
/// Only Commit is allowed to mutate rider or track state.
/// </summary>
public sealed class SimulationEngine
{
    private const float AdvancedAdjacentSurfaceWearFraction = 0.20f;

    /// <summary>
    /// Provisional physical outward displacement after a non-crashing contact.
    /// </summary>
    public const float ProvisionalLostRhythmOutwardDisplacementMeters = 0.50f;

    private readonly IRiderDecisionModel _decisionModel;

    private sealed record ResolvedRider(
        RiderStateChange Change,
        float TravelledMeters,
        float? FullDriveEquilibriumSpeedMetersPerSecond,
        float? TurnExitNetAccelerationMetersPerSecondSquared,
        TurnExitDriveProfile? TurnExitDriveProfile,
        StraightSpeedProfile? StraightProfile,
        TurnEntryScrubProfile? TurnEntryScrubProfile,
        TrackSurfaceState EntrySurface,
        StandingStartLaunchProfile? StandingStartLaunchProfile,
        CornerSpeedCorrectionProfile? CornerSpeedCorrectionProfile,
        CornerPhaseContext? CornerPhaseContext,
        ContinuousCornerTraversalProfile? ContinuousCornerProfile,
        ExecutedSegmentPath? ExecutedPath = null,
        IReadOnlyList<LongitudinalMotionNode>? LongitudinalNodes = null,
        ExecutedTraversalResult? ProjectionPath = null);

    public SimulationEngine(IRiderDecisionModel decisionModel)
        => _decisionModel = decisionModel ?? throw new ArgumentNullException(nameof(decisionModel));

    public SimulationSnapshot CaptureSnapshot(
        Track track,
        TrackState trackState,
        IReadOnlyList<RiderState> riders,
        SimulationStepContext step)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(trackState);
        ArgumentNullException.ThrowIfNull(riders);
        ArgumentNullException.ThrowIfNull(step);
        if (trackState.SegmentCount != track.Segments.Count || trackState.LinesCount != LaneModel.LanesCount)
            throw new ArgumentException("Track state dimensions must match the track.", nameof(trackState));
        if (step.RequiredLaps <= 0)
            throw new ArgumentOutOfRangeException(nameof(step));
        if (riders.Select(rider => rider.RiderId).Distinct().Count() != riders.Count)
            throw new ArgumentException("Every rider in a heat must have a unique id.", nameof(riders));

        var snapshots = riders.Select(rider => new RiderSnapshot(
            rider.RiderId,
            rider.Profile,
            rider.PositionForTrack(track.Segments.Count),
            rider.LastResolvedSegmentId,
            rider.Lane,
            rider.LateralPosition,
            rider.Speed,
            rider.Risk,
            rider.Status,
            rider.ElapsedTimeSeconds,
            rider.ActiveSetup,
            rider.Morale,
            rider.ManagerTrust) { StartingPosition = rider.StartingPosition });

        return new SimulationSnapshot(step, track, trackState.Snapshot(), snapshots);
    }

    public IReadOnlyList<RiderIntent> Decide(SimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var intents = snapshot.Riders
            .Where(rider => rider.IsActive)
            .OrderBy(rider => rider.RiderId)
            .Select(rider => new RiderIntent(
                rider.RiderId,
                _decisionModel.Decide(new RiderDecisionContext(snapshot, rider))))
            .ToArray();
        return Array.AsReadOnly(intents);
    }

    public ResolvedSimulationStep Resolve(
        SimulationSnapshot snapshot,
        IReadOnlyList<RiderIntent> intents,
        HeatSimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(intents);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var activeIds = snapshot.Riders.Where(rider => rider.IsActive).Select(rider => rider.RiderId).Order().ToArray();
        var intentIds = intents.Select(intent => intent.RiderId).Order().ToArray();
        if (!activeIds.SequenceEqual(intentIds) || intentIds.Distinct().Count() != intentIds.Length)
            throw new ArgumentException("Resolve requires exactly one intent for every active rider.", nameof(intents));

        var intentByRider = intents.ToDictionary(intent => intent.RiderId);
        var changes = new Dictionary<int, RiderStateChange>(activeIds.Length);
        var riderResolutions = new Dictionary<int, ResolvedRider>(activeIds.Length);
        var events = new List<SimulationStepEvent>();

        foreach (var rider in snapshot.Riders.Where(rider => rider.IsActive).OrderBy(rider => rider.RiderId))
        {
            var riderResolution = ResolveRiderCore(
                snapshot,
                rider,
                intentByRider[rider.RiderId].Decision,
                options);
            var change = riderResolution.Change;
            changes.Add(rider.RiderId, change);
            riderResolutions.Add(rider.RiderId, riderResolution);
            ProjectionCaptureAudit.Record(ProjectionMaterialization.Event);
            events.Add(new SimulationStepEvent(
                snapshot.Step.StepNumber,
                10,
                rider.RiderId,
                SimulationEventType.SegmentResolved,
                FormatSegmentLog(snapshot, change)));
        }

        if (!snapshot.Step.UseLegacyPhysics)
            ResolveExistingInteractions(snapshot, changes, events);

        var diagnostics = riderResolutions.Values.Select(resolution =>
        {
            var rider = snapshot.Rider(resolution.Change.RiderId);
            var finalChange = changes[resolution.Change.RiderId];
            var diagnosticTravelTimeSeconds =
                finalChange.ElapsedTimeSeconds - rider.ElapsedTimeSeconds;
            var peakSpeed = CalculateResolvedPeakSpeedMetersPerSecond(
                resolution,
                finalChange);

            if (resolution.StandingStartLaunchProfile is { } launchProfile)
            {
                ValidateEquivalent(launchProfile.ExitSpeedMetersPerSecond,
                    resolution.Change.Speed, "Standing-start profile exit speed");
                ValidateEquivalent(launchProfile.TotalTimeSeconds,
                    resolution.Change.ElapsedTimeSeconds - rider.ElapsedTimeSeconds,
                    "Standing-start profile total time");
            }

            if (resolution.TurnExitDriveProfile is { } turnExitProfile)
            {
                ValidateEquivalent(
                    turnExitProfile.ExitSpeedMetersPerSecond,
                    resolution.Change.Speed,
                    "TurnExit profile exit speed");
                ValidateEquivalent(
                    turnExitProfile.TravelTimeSeconds
                    + (resolution.CornerSpeedCorrectionProfile?.TravelTimeSeconds ?? 0f),
                    resolution.Change.ElapsedTimeSeconds - rider.ElapsedTimeSeconds,
                    "TurnExit physical phase time");
            }

            ProjectionCaptureAudit.Record(ProjectionMaterialization.RiderDiagnostics);
            return new RiderStepDiagnostics(
                finalChange.RiderId,
                resolution.TravelledMeters,
                diagnosticTravelTimeSeconds,
                peakSpeed,
                resolution.FullDriveEquilibriumSpeedMetersPerSecond,
                resolution.TurnExitNetAccelerationMetersPerSecondSquared,
                resolution.TurnExitDriveProfile,
                resolution.StraightProfile,
                resolution.TurnEntryScrubProfile,
                resolution.EntrySurface,
                resolution.StandingStartLaunchProfile,
                resolution.CornerSpeedCorrectionProfile,
                resolution.CornerPhaseContext,
                resolution.ContinuousCornerProfile,
                resolution.ExecutedPath);
        }).ToArray();
        var motions = diagnostics.Select(d =>
        {
            var resolution = riderResolutions[d.RiderId];
            return ResolvedRiderMotion.Create(snapshot, snapshot.Rider(d.RiderId), resolution.Change,
                changes[d.RiderId], d, resolution.LongitudinalNodes);
        });

        return new ResolvedSimulationStep(snapshot, changes.Values, events, diagnostics, motions);
    }

    private static float CalculateResolvedPeakSpeedMetersPerSecond(
        ResolvedRider resolution,
        RiderStateChange finalChange)
    {
        var peakSpeed = MathF.Max(
            resolution.Change.EntrySpeed,
            resolution.Change.PhysicsSpeed);
        peakSpeed = MathF.Max(peakSpeed, resolution.Change.Speed);
        peakSpeed = MathF.Max(peakSpeed, finalChange.Speed);

        if (resolution.CornerSpeedCorrectionProfile is { } correction)
        {
            peakSpeed = MathF.Max(peakSpeed, correction.EntrySpeedMetersPerSecond);
            peakSpeed = MathF.Max(peakSpeed, correction.ExitSpeedMetersPerSecond);
        }

        if (resolution.StandingStartLaunchProfile is { } launch)
            peakSpeed = MathF.Max(peakSpeed, launch.PeakSpeedMetersPerSecond);
        if (resolution.StraightProfile is { } straight)
            peakSpeed = MathF.Max(peakSpeed, straight.PeakSpeedMetersPerSecond);
        if (resolution.TurnExitDriveProfile is { } turnExit)
            peakSpeed = MathF.Max(peakSpeed, turnExit.PeakSpeedMetersPerSecond);

        if (resolution.ContinuousCornerProfile is { } corner)
            peakSpeed = MathF.Max(peakSpeed, corner.PeakSpeedMetersPerSecond);

        if (resolution.ExecutedPath is { } path)
            peakSpeed = MathF.Max(peakSpeed, path.PeakSpeedMetersPerSecond);

        return peakSpeed;
    }

    public void Commit(
        ResolvedSimulationStep resolved,
        IReadOnlyList<RiderState> riders,
        TrackState trackState,
        SimLog log)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(riders);
        ArgumentNullException.ThrowIfNull(trackState);
        ArgumentNullException.ThrowIfNull(log);

        ValidateCommitInputs(resolved, riders, trackState);
        var riderById = riders.ToDictionary(rider => rider.RiderId);

        // All rider mutations happen before any observer-visible logging or
        // surface wear. No rider can see another rider's partial commit.
        foreach (var change in resolved.Changes.OrderBy(change => change.RiderId))
        {
            var rider = riderById[change.RiderId];
            CommitRiderChange(change, rider);
        }

        foreach (var simulationEvent in resolved.Events)
            log.Add(simulationEvent.Text);

        foreach (var change in resolved.Changes
                     .Where(change => change.ApplySurfaceWear)
                     .OrderBy(change => change.RiderId))
        {
            if (resolved.Snapshot.Step.UseLegacyPhysics)
                ApplyLegacySurfaceWear(resolved.Snapshot, trackState, change.Lane, log);
            else if (resolved.Diagnostics.Single(d => d.RiderId == change.RiderId).ExecutedPath is { } path)
                ApplyExecutedSurfaceWear(resolved.Snapshot, trackState, path, log);
            else
                ApplyAdvancedSurfaceWear(
                    resolved.Snapshot,
                    trackState,
                    resolved.Snapshot.Rider(change.RiderId).LateralPosition,
                    log);
        }
    }

    internal static void CommitRiderChange(RiderStateChange change, RiderState rider)
    {
        rider.Lane = change.Lane;
        rider.LateralPosition = change.LateralPosition;
        rider.Speed = change.Speed;
        rider.Risk = change.Risk;
        rider.ElapsedTimeSeconds = change.ElapsedTimeSeconds;
        rider.Morale = change.Morale;
        rider.CommitPosition(change.Position);
        rider.SetLastResolvedSegmentId(change.LastResolvedSegmentId);
        rider.SetStatus(change.Status);
    }

    /// <summary>Same rider physics, solo materialization without events, logs or rich motion.</summary>
    internal static SoloProjectionResult ResolveSoloProjection(SimulationSnapshot snapshot,
        RiderSnapshot rider, RiderDecision decision, HeatSimulationOptions options)
    {
        var resolved = ResolveRiderCore(snapshot, rider, decision, options, captureRich: false);
        var change = resolved.Change;
        var corner = snapshot.Track.CornerTopology.CornerForSegment(snapshot.Step.SegmentIndex);
        float? apex = null;
        if (corner is not null)
        {
            var progress = (double)snapshot.Step.LapIndex * snapshot.Track.Segments.Count
                + corner.StartSegmentIndex + corner.SegmentCount * (double)ContinuousCornerEnvelope.ApexProgress;
            if (rider.CanonicalProgress <= progress && change.Position.TotalSegmentProgress >= progress)
                apex = progress == rider.CanonicalProgress ? rider.LateralPosition
                    : progress == change.Position.TotalSegmentProgress ? change.LateralPosition
                    : resolved.ProjectionPath?.ApexLateralPosition ?? rider.LateralPosition;
        }
        return new(snapshot, change, change.ElapsedTimeSeconds - rider.ElapsedTimeSeconds,
            resolved.TravelledMeters, apex,
            resolved.ProjectionPath?.Wear ?? FixedWear(rider.LateralPosition), resolved.ProjectionPath?.TimeSolveSubdivisions ?? 0);
    }

    internal static void CommitSoloProjection(SoloProjectionResult result, RiderState rider, TrackState state)
    {
        CommitRiderChange(result.Change, rider);
        CommitProjectionWear(result, state);
    }

    internal static void CommitProjectionWear(SoloProjectionResult result, TrackState state)
    {
        ProjectionCaptureAudit.Record(ProjectionMaterialization.PrivateCommit);
        if (result.Change.ApplySurfaceWear)
        {
            if (result.Snapshot.Step.UseLegacyPhysics)
                ApplyLegacySurfaceWear(result.Snapshot, state, result.Change.Lane, new SimLog(false));
            else ApplyWear(result.Snapshot, state, result.Wear, new SimLog(false));
        }
    }

    private static void ValidateCommitInputs(
        ResolvedSimulationStep resolved,
        IReadOnlyList<RiderState> riders,
        TrackState trackState)
    {
        var snapshot = resolved.Snapshot;
        if (trackState.SegmentCount != snapshot.TrackState.SegmentCount
            || trackState.LinesCount != snapshot.TrackState.LinesCount)
            throw new ArgumentException("Track state dimensions must match the resolved snapshot.", nameof(trackState));

        var snapshotIds = snapshot.Riders.Select(rider => rider.RiderId).ToArray();
        if (snapshotIds.Distinct().Count() != snapshotIds.Length)
            throw new InvalidOperationException("The resolved snapshot contains duplicate rider ids.");

        var riderIds = riders.Select(rider => rider.RiderId).ToArray();
        if (riderIds.Distinct().Count() != riderIds.Length)
            throw new ArgumentException("Commit riders must have unique ids.", nameof(riders));
        if (!snapshotIds.Order().SequenceEqual(riderIds.Order()))
            throw new ArgumentException("Commit riders do not match the resolved snapshot.", nameof(riders));

        var changeIds = resolved.Changes.Select(change => change.RiderId).ToArray();
        if (changeIds.Distinct().Count() != changeIds.Length)
            throw new InvalidOperationException("The resolved step contains duplicate rider changes.");

        var activeSnapshotIds = snapshot.Riders
            .Where(rider => rider.IsActive)
            .Select(rider => rider.RiderId)
            .Order()
            .ToArray();
        if (!activeSnapshotIds.SequenceEqual(changeIds.Order()))
            throw new InvalidOperationException("The resolved rider changes do not match the snapshot.");
    }

    private static ResolvedRider ResolveRiderCore(
        SimulationSnapshot snapshot,
        RiderSnapshot rider,
        RiderDecision decision,
        HeatSimulationOptions options, bool captureRich = true)
    {
        ProjectionCaptureAudit.Record(ProjectionMaterialization.PhysicalEvaluation);
        if (rider.SegmentIndex != snapshot.Step.SegmentIndex)
            throw new InvalidOperationException($"Rider {rider.RiderId} is not in segment {snapshot.Step.SegmentIndex}.");

        var cornerPhaseContext = snapshot.Step.UseLegacyPhysics
            ? null
            : snapshot.Track.CornerTopology.Resolve(
                snapshot.Step.SegmentIndex,
                rider.SegmentProgress,
                rider.LateralPosition,
                snapshot.Track.Geometry);
        var targetLane = LaneModel.ClampLane(decision.TargetLane);
        var plannedLane = LateralMovementModel.CalculatePlannedLane(
            rider.Lane,
            rider.LateralPosition,
            targetLane,
            snapshot.Segment.Type,
            snapshot.Track.Geometry,
            useContinuousPlanning: !snapshot.Step.UseLegacyPhysics);
        var surface = snapshot.Step.UseLegacyPhysics
            ? snapshot.TrackState.GetSurface(snapshot.Step.SegmentIndex, plannedLane)
            : snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, rider.LateralPosition);
        var remainingProgress = 1f - rider.SegmentProgress;
        var standingStartEligible = !snapshot.Step.UseLegacyPhysics
            && snapshot.Step.LapIndex == 0
            && snapshot.Step.SegmentIndex == 0
            && snapshot.Segment.IsStandingStartSegment
            && rider.Position.TotalSegmentProgress == 0d
            && rider.Position.DistanceMeters == 0f
            && rider.Status == RiderRaceStatus.NotStarted
            && rider.Speed <= 0f
            && LaneModel.SegmentLengthMeters(snapshot.Segment, rider.LateralPosition, snapshot.Track.Geometry)
                * remainingProgress > 0f;
        var entrySpeed = snapshot.Step.UseLegacyPhysics
            ? ResolveLegacyEntrySpeed(plannedLane, rider)
            : standingStartEligible ? 0f : ResolveAdvancedEntrySpeed(snapshot, plannedLane, surface, rider);
        TurnEntryScrubProfile? turnEntryScrubProfile = null;
        var speedForPhysicalResolution = entrySpeed;
        var resolution = snapshot.Step.UseLegacyPhysics
            ? SegmentPhysics.Apply(snapshot.Segment, plannedLane, speedForPhysicalResolution)
            : SegmentPhysics.Apply(new SegmentPhysicsContext(
                Segment: snapshot.Segment,
                Lane: plannedLane,
                Speed: speedForPhysicalResolution,
                Geometry: snapshot.Track.Geometry,
                Surface: surface,
                Skills: rider.Profile.Skills,
                Morale: rider.Morale,
                Setup: rider.ActiveSetup,
                DecisionRisk: decision.Risk,
                LateralPosition: rider.LateralPosition,
                CornerPhase: cornerPhaseContext));

        if (!snapshot.Step.UseLegacyPhysics)
            resolution = ResolveRandomIncident(snapshot, rider, plannedLane, resolution, options);

        var risk = snapshot.Step.UseLegacyPhysics
            ? ApplySurfaceRisk(snapshot, resolution.Lane, decision.Risk)
            : resolution.IncidentRisk;

        var canonicalAdvance = resolution.Outcome == SegmentOutcome.Crash
            ? remainingProgress * 0.5f
            : remainingProgress;
        var segmentLength = snapshot.Step.UseLegacyPhysics
            ? LaneModel.SegmentLengthMeters(
                snapshot.Segment,
                resolution.Lane,
                snapshot.Track.Geometry)
            : LaneModel.SegmentLengthMeters(
                snapshot.Segment,
                rider.LateralPosition,
                snapshot.Track.Geometry);
        var travelled = segmentLength * canonicalAdvance;
        if (!snapshot.Step.UseLegacyPhysics && !ExecutedPathTraversal.IsFixedLine(rider.LateralPosition, resolution.Lane))
            return ResolveMovingRider(snapshot, rider, decision, resolution, entrySpeed, plannedLane,
                canonicalAdvance, standingStartEligible, risk, surface, cornerPhaseContext, captureRich);
        var speed = resolution.Speed;
        StraightSpeedProfile? straightProfile = null;
        TurnExitDriveProfile? turnExitDriveProfile = null;
        CornerSpeedCorrectionProfile? cornerSpeedCorrectionProfile = null;
        StandingStartLaunchProfile? standingStartLaunchProfile = null;
        float? turnExitNetAcceleration = null;
        var longitudinalNodes = captureRich ? new List<LongitudinalMotionNode>() : null;

        ContinuousCornerTraversalProfile? continuousCornerProfile = null;
        if (cornerPhaseContext is { } phase && resolution.Outcome != SegmentOutcome.Crash)
        {
            var envelope = ContinuousCornerEnvelope.Create(phase, rider.LateralPosition,
                snapshot.Track.Geometry, surface, rider.Profile.Skills, rider.ActiveSetup);
            var allowDrive = resolution.Outcome is SegmentOutcome.Ok or SegmentOutcome.Brake;
            // Incident-created RunWide clears its target in ResolveRandomIncident.
            // Preserve that immediate consequence without applying a second penalty.
            var allowCorrection = resolution.Outcome != SegmentOutcome.RunWide
                || resolution.ContinuousCorrectionTargetSpeedMetersPerSecond.HasValue;
            var retainedOverspeed = resolution.Outcome == SegmentOutcome.RunWide
                && resolution.ContinuousCorrectionTargetSpeedMetersPerSecond is { } retainedTarget
                ? MathF.Max(0f, retainedTarget - envelope.SpeedMetersPerSecond(phase.CornerProgress))
                : 0f;
            continuousCornerProfile = options.ActiveCorrectionControlLossAdjustment is { } controlLossAdjustment
                ? envelope.TraverseWithActiveCorrectionControlLoss(
                    resolution.Speed, phase.CornerProgress, travelled, controlLossAdjustment,
                    surface.EffectiveGrip, rider.Profile.Skills.Adaptability,
                    allowDrive, allowCorrection, retainedOverspeed)
                : options.PreApexScrubLossAdjustment is { } scrubAdjustment
                ? envelope.TraverseWithPreApexScrubLoss(
                    resolution.Speed, phase.CornerProgress, travelled, scrubAdjustment,
                    allowDrive, allowCorrection, retainedOverspeed)
                : options.CornerReducedDriveResistanceAdjustment is { } adjustment
                ? envelope.TraverseWithReducedDriveResistanceExposure(
                    resolution.Speed, phase.CornerProgress, travelled, adjustment.Exposure,
                    allowDrive, allowCorrection, retainedOverspeed)
                : captureRich ? envelope.Traverse(resolution.Speed, phase.CornerProgress, travelled,
                    allowDrive, allowCorrection, retainedOverspeed)
                : envelope.TraverseProjection(resolution.Speed, phase.CornerProgress, travelled,
                    allowDrive, allowCorrection, retainedOverspeed);
            speed = continuousCornerProfile.ExitSpeedMetersPerSecond;
            ValidateDistanceComposition(travelled, "continuous corner",
                continuousCornerProfile.CorrectionDistanceMeters,
                continuousCornerProfile.CarryDistanceMeters,
                continuousCornerProfile.DriveDistanceMeters);
        }

        if (standingStartEligible)
        {
            standingStartLaunchProfile = LongitudinalDynamics.CalculateStandingStartLaunchProfile(
                rider.Profile.Skills, rider.ActiveSetup, surface, travelled,
                ResolveImmediateNextTurnApproachSpeed(snapshot, rider), longitudinalNodes);
            speed = standingStartLaunchProfile.Value.ExitSpeedMetersPerSecond;
        }
        else if (!snapshot.Step.UseLegacyPhysics
            && snapshot.Segment.Type == SegmentType.Straight)
        {
            var cornerEntryDeceleration = LongitudinalDynamics
                .CalculateCornerEntryDecelerationMetersPerSecondSquared(
                    rider.Profile.Skills,
                    surface);
            var targetExitSpeed = ResolveImmediateNextTurnApproachSpeed(snapshot, rider);
            straightProfile = LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
                    resolution.Speed,
                    rider.Profile.Skills,
                    rider.ActiveSetup,
                    surface,
                    cornerEntryDeceleration,
                    travelled,
                    targetExitSpeed, longitudinalNodes, options.StraightDriveEnvelopeAdjustment);
            speed = straightProfile.Value.ExitSpeedMetersPerSecond;
        }
        float segmentElapsedTimeSeconds;
        if (standingStartLaunchProfile is { } launchProfile)
        {
            segmentElapsedTimeSeconds = launchProfile.TotalTimeSeconds;
        }
        else if (continuousCornerProfile is { } cornerProfile)
        {
            segmentElapsedTimeSeconds = cornerProfile.TravelTimeSeconds;
        }
        else if (straightProfile is { } profile)
        {
            segmentElapsedTimeSeconds = profile.TravelTimeSeconds;
        }
        else
        {
            var averageSpeed = MathF.Max(1f, (entrySpeed + MathF.Max(speed, 0f)) * 0.5f);
            segmentElapsedTimeSeconds = travelled / averageSpeed;
        }

        var lateralMovementTimeSeconds = standingStartLaunchProfile?.MovementTimeSeconds
            ?? segmentElapsedTimeSeconds;
        var elapsedTime = rider.ElapsedTimeSeconds + segmentElapsedTimeSeconds;
        var lateralPosition = snapshot.Step.UseLegacyPhysics
            ? resolution.Lane
            : LateralMovementModel.MoveTowards(
                rider.LateralPosition,
                resolution.Lane,
                lateralMovementTimeSeconds,
                snapshot.Segment.Type,
                snapshot.Track.Geometry,
                surface,
                rider.Profile.Skills);
        var position = rider.Position.Advance(canonicalAdvance, travelled);
        var status = resolution.Outcome == SegmentOutcome.Crash
            ? RiderRaceStatus.Crashed
            : position.LapsCompleted >= snapshot.Step.RequiredLaps
                ? RiderRaceStatus.Finished
                : RiderRaceStatus.Racing;
        var morale = resolution.Outcome == SegmentOutcome.Crash
            ? Math.Clamp(rider.Morale - 0.04f, 0f, 1f)
            : rider.Morale;

        ProjectionCaptureAudit.Record(ProjectionMaterialization.LongitudinalNode, longitudinalNodes?.Count ?? 0);
        return new ResolvedRider(
            new RiderStateChange(
                rider.RiderId,
                rider.Lane,
                plannedLane,
                decision.TargetLane,
                resolution.Lane,
                lateralPosition,
                speed,
                risk,
                status,
                elapsedTime,
                position,
                snapshot.Segment.Id,
                morale,
                resolution.Outcome,
                entrySpeed,
                resolution.Speed,
                ApplySurfaceWear: resolution.Outcome != SegmentOutcome.Crash),
            travelled,
            standingStartLaunchProfile?.FullDriveEquilibriumSpeedMetersPerSecond
                ?? straightProfile?.FullDriveEquilibriumSpeedMetersPerSecond
                ?? continuousCornerProfile?.FullDriveEquilibriumSpeedMetersPerSecond,
            turnExitNetAcceleration,
            turnExitDriveProfile,
            straightProfile,
            turnEntryScrubProfile,
            surface,
            standingStartLaunchProfile,
            cornerSpeedCorrectionProfile,
            cornerPhaseContext,
            continuousCornerProfile, LongitudinalNodes: longitudinalNodes);
    }

    private static ResolvedRider ResolveMovingRider(SimulationSnapshot snapshot, RiderSnapshot rider,
        RiderDecision decision, SegmentResolution resolution, float entrySpeed, int plannedLane,
        float canonicalAdvance, bool launch, float risk, TrackSurfaceState entrySurface, CornerPhaseContext? phase,
        bool captureRich)
    {
        Dictionary<int, float?>? approachTargets = snapshot.Segment.Type == SegmentType.Straight ? new() : null;
        float? NextCorner(float lateral)
        {
            var key = BitConverter.SingleToInt32Bits(lateral);
            if (approachTargets!.TryGetValue(key, out var target)) return target;
            target = ResolveImmediateNextTurnApproachSpeed(snapshot, rider, lateral);
            approachTargets.Add(key, target); return target;
        }
        var traversal = ExecutedPathTraversal.Traverse(snapshot, rider, resolution, entrySpeed, canonicalAdvance,
            launch, NextCorner, captureRich);
        var path = traversal.RichPath;
        var position = rider.Position.Advance(canonicalAdvance, traversal.DistanceMeters);
        var status = resolution.Outcome == SegmentOutcome.Crash ? RiderRaceStatus.Crashed
            : position.LapsCompleted >= snapshot.Step.RequiredLaps ? RiderRaceStatus.Finished : RiderRaceStatus.Racing;
        StandingStartLaunchProfile? launchProfile = path is not null && launch ? path.LaunchProfile(rider.ActiveSetup) : null;
        StraightSpeedProfile? straightProfile = path is not null && !launch && snapshot.Segment.Type == SegmentType.Straight ? path.StraightProfile() : null;
        ContinuousCornerTraversalProfile? cornerProfile = path is not null && phase.HasValue && resolution.Outcome != SegmentOutcome.Crash ? path.CornerProfile() : null;
        return new(new RiderStateChange(rider.RiderId, rider.Lane, plannedLane, decision.TargetLane, resolution.Lane,
            traversal.FinalLateralPosition, traversal.ExitSpeedMetersPerSecond, risk, status,
            rider.ElapsedTimeSeconds + traversal.TravelTimeSeconds, position, snapshot.Segment.Id,
            resolution.Outcome == SegmentOutcome.Crash ? Math.Clamp(rider.Morale - .04f, 0f, 1f) : rider.Morale,
            resolution.Outcome, entrySpeed, resolution.Speed, resolution.Outcome != SegmentOutcome.Crash),
            traversal.DistanceMeters, path?.FullDriveEquilibriumSpeedMetersPerSecond, null, null, straightProfile,
            null, entrySurface, launchProfile, null, phase, cornerProfile, path, ProjectionPath: traversal);
    }

    private static float? ResolveImmediateNextTurnApproachSpeed(
        SimulationSnapshot snapshot,
        RiderSnapshot rider,
        float? actualLateralPosition = null)
    {
        var isFinalRaceSegment = snapshot.Step.LapIndex == snapshot.Step.RequiredLaps - 1
            && snapshot.Step.SegmentIndex == snapshot.Track.Segments.Count - 1;
        if (isFinalRaceSegment)
            return null;

        var allowLapWrap = snapshot.Step.SegmentIndex == snapshot.Track.Segments.Count - 1;
        var nextCorner = snapshot.Track.CornerTopology.ImmediateNextCorner(
            snapshot.Step.SegmentIndex,
            allowLapWrap);
        if (nextCorner is null)
            return null;

        var nextSegmentIndex = nextCorner.StartSegmentIndex;
        var lateral = actualLateralPosition ?? rider.LateralPosition;
        var nextSegment = snapshot.Track.Segments[nextSegmentIndex];
        var nextCornerPhase = snapshot.Track.CornerTopology.Resolve(
            nextSegmentIndex,
            0f,
            lateral,
            snapshot.Track.Geometry)
            ?? throw new InvalidOperationException("Immediate logical corner has no start phase.");
        var nextSurface = snapshot.TrackState.SampleSurface(nextSegmentIndex, lateral);
        return ContinuousCornerEnvelope.Create(nextCornerPhase, lateral,
            snapshot.Track.Geometry, nextSurface, rider.Profile.Skills, rider.ActiveSetup)
            .SpeedMetersPerSecond(0f);
    }

    private static void ValidateDistanceComposition(
        float expectedDistanceMeters,
        string name,
        params float[] phaseDistancesMeters)
    {
        if (phaseDistancesMeters.Any(distance => !float.IsFinite(distance) || distance < 0f))
            throw new InvalidOperationException($"{name} contains an invalid phase distance.");

        var actualDistanceMeters = phaseDistancesMeters.Sum();
        var tolerance = MathF.Max(1e-5f, expectedDistanceMeters * 1e-5f);
        if (MathF.Abs(actualDistanceMeters - expectedDistanceMeters) > tolerance)
        {
            throw new InvalidOperationException(
                $"{name} does not conserve travelled distance.");
        }
    }

    private static void ValidateEquivalent(float expected, float actual, string name)
    {
        var tolerance = MathF.Max(
            1e-5f,
            MathF.Max(MathF.Abs(expected), MathF.Abs(actual)) * 1e-5f);
        if (MathF.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{name} does not match production resolution.");
    }

    private static void ResolveExistingInteractions(
        SimulationSnapshot snapshot,
        IDictionary<int, RiderStateChange> changes,
        ICollection<SimulationStepEvent> events)
    {
        var ordered = changes.Values
            .Where(change => change.Status is RiderRaceStatus.Racing or RiderRaceStatus.Finished)
            .OrderBy(change => change.ElapsedTimeSeconds)
            .ThenBy(change => change.RiderId)
            .ToArray();

        // Freeze pair identities from post-ResolveRider positions before any
        // contact consequence can change time, lane, lateral position or status.
        var candidates = new List<(int LeaderRiderId, int TrailingRiderId)>();

        for (var trailingIndex = 1; trailingIndex < ordered.Length; trailingIndex++)
        {
            var trailing = ordered[trailingIndex];
            for (var leaderIndex = trailingIndex - 1; leaderIndex >= 0; leaderIndex--)
            {
                var leader = ordered[leaderIndex];
                if (!LateralSpaceModel.IsWithinProvisionalContactThreshold(
                        leader.LateralPosition,
                        trailing.LateralPosition,
                        snapshot.Segment.Type,
                        snapshot.Track.Geometry))
                    continue;

                candidates.Add((leader.RiderId, trailing.RiderId));
                break;
            }
        }

        foreach (var candidate in candidates)
        {
            var leader = changes[candidate.LeaderRiderId];
            var trailing = changes[candidate.TrailingRiderId];
            var gap = trailing.ElapsedTimeSeconds - leader.ElapsedTimeSeconds;
            if (gap > 0.12f)
                continue;

            var trailingSnapshot = snapshot.Rider(trailing.RiderId);
            var control = RiderSkills.Normalize(trailingSnapshot.Profile.Skills.SlideControl);
            var pairRiding = RiderSkills.Normalize(trailingSnapshot.Profile.Skills.PairRiding);
            var surface = snapshot.TrackState.GetSurface(snapshot.Step.SegmentIndex, trailing.Lane);
            var locationRisk = snapshot.Segment.Type switch
            {
                SegmentType.TurnMiddle => 0.24f,
                SegmentType.TurnEntry or SegmentType.TurnExit => 0.12f,
                _ => 0.025f,
            };
            var contactChance = locationRisk
                * (1.20f - pairRiding * 0.45f)
                * (1.15f - control * 0.35f)
                * (1.10f + (1f - surface.EffectiveGrip) * 0.40f);
            var contactSample = DeterministicRandom.Sample01(
                snapshot.Step.Seed,
                snapshot.Step.HeatId,
                snapshot.Step.StepNumber,
                trailing.RiderId,
                RandomChannel.ContactOccurrence,
                leader.RiderId,
                trailing.Lane);
            if (contactSample >= contactChance)
                continue;

            var crashChance = snapshot.Segment.Type == SegmentType.TurnMiddle ? 0.28f : 0.08f;
            crashChance *= 1.20f - control * 0.55f;
            var severitySample = DeterministicRandom.Sample01(
                snapshot.Step.Seed,
                snapshot.Step.HeatId,
                snapshot.Step.StepNumber,
                trailing.RiderId,
                RandomChannel.ContactSeverity,
                leader.RiderId,
                trailing.Lane);

            if (severitySample < crashChance)
            {
                trailing = trailing with
                {
                    Status = RiderRaceStatus.Crashed,
                    Speed = 0f,
                    Morale = Math.Clamp(trailing.Morale - 0.04f, 0f, 1f),
                };
                changes[trailing.RiderId] = trailing;
                ProjectionCaptureAudit.Record(ProjectionMaterialization.Event);
            events.Add(new SimulationStepEvent(
                    snapshot.Step.StepNumber,
                    20,
                    trailing.RiderId,
                    SimulationEventType.ContactCrash,
                    $"LAP={snapshot.Step.LapIndex + 1} SEG={snapshot.Segment.Id} contact rider={trailing.RiderId} with={leader.RiderId} outcome=Crash",
                    leader.RiderId));
            }
            else
            {
                var lane = trailing.Lane;
                var lateral = trailing.LateralPosition;
                if (snapshot.Segment.Type != SegmentType.Straight && lane < LaneModel.MaxLane)
                {
                    lane++;
                    lateral = LateralMovementModel.MoveOutwardByPhysicalDistance(
                        lateral,
                        ProvisionalLostRhythmOutwardDisplacementMeters,
                        lane,
                        snapshot.Segment.Type,
                        snapshot.Track.Geometry);
                }

                trailing = trailing with
                {
                    Speed = trailing.Speed * 0.82f,
                    ElapsedTimeSeconds = trailing.ElapsedTimeSeconds + 0.20f,
                    Lane = lane,
                    LateralPosition = lateral,
                };
                changes[trailing.RiderId] = trailing;
                ProjectionCaptureAudit.Record(ProjectionMaterialization.Event);
            events.Add(new SimulationStepEvent(
                    snapshot.Step.StepNumber,
                    20,
                    trailing.RiderId,
                    SimulationEventType.ContactLostRhythm,
                    $"LAP={snapshot.Step.LapIndex + 1} SEG={snapshot.Segment.Id} contact rider={trailing.RiderId} with={leader.RiderId} outcome=LostRhythm",
                    leader.RiderId));
            }
        }
    }

    private static float ResolveLegacyEntrySpeed(int plannedLane, RiderSnapshot rider)
        => rider.Speed <= 0f ? SegmentPhysics.MaxSafeTurnSpeed(plannedLane) : rider.Speed;

    private static float ResolveAdvancedEntrySpeed(
        SimulationSnapshot snapshot,
        int lane,
        TrackSurfaceState surface,
        RiderSnapshot rider)
    {
        if (rider.Speed > 0f)
            return rider.Speed;
        if (snapshot.Segment.Type == SegmentType.Straight)
            return SegmentPhysics.MaxSafeTurnSpeed(lane) * 0.95f;

        var safeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            rider.LateralPosition,
            snapshot.Track.Geometry,
            surface,
            rider.Profile.Skills,
            rider.ActiveSetup);
        var startSkill = RiderSkills.Normalize(rider.Profile.Skills.Start);
        var startMultiplier = snapshot.Step.LapIndex == 0 && snapshot.Step.SegmentIndex == 0
            ? 0.90f + startSkill * 0.16f
            : 1f;
        return safeSpeed * startMultiplier;
    }

    private static SegmentResolution ResolveRandomIncident(
        SimulationSnapshot snapshot,
        RiderSnapshot rider,
        int plannedLane,
        SegmentResolution resolution,
        HeatSimulationOptions options)
    {
        if (options.IncidentFrequency <= 0f
            || snapshot.Segment.Type == SegmentType.Straight
            || resolution.Outcome == SegmentOutcome.Crash)
            return resolution;

        var probability = resolution.IncidentRisk * 0.08f * options.IncidentFrequency;
        var occurrence = DeterministicRandom.Sample01(
            snapshot.Step.Seed,
            snapshot.Step.HeatId,
            snapshot.Step.StepNumber,
            rider.RiderId,
            RandomChannel.IncidentOccurrence,
            plannedLane);
        if (occurrence >= probability)
            return resolution;

        var severity = DeterministicRandom.Sample01(
            snapshot.Step.Seed,
            snapshot.Step.HeatId,
            snapshot.Step.StepNumber,
            rider.RiderId,
            RandomChannel.IncidentSeverity,
            plannedLane);
        if (severity < resolution.IncidentRisk * 0.35f || plannedLane == LaneModel.MaxLane)
        {
            return resolution with
            {
                Outcome = SegmentOutcome.Crash,
                Speed = 0f,
                ContinuousCorrectionTargetSpeedMetersPerSecond = null,
            };
        }

        return resolution with
        {
            Outcome = SegmentOutcome.RunWide,
            Lane = Math.Min(plannedLane + 1, LaneModel.MaxLane),
            Speed = resolution.Speed * 0.88f,
            ContinuousCorrectionTargetSpeedMetersPerSecond = null,
        };
    }

    private static float ApplySurfaceRisk(SimulationSnapshot snapshot, int lane, float baseRisk)
    {
        if (snapshot.Segment.Type == SegmentType.Straight)
            return baseRisk;

        var surface = snapshot.TrackState.GetSurface(snapshot.Step.SegmentIndex, lane);
        var surfaceRisk = (1f - surface.Grip) * 0.7f + surface.Ruts * 0.3f;
        if (surface.Moisture > 0.5f)
            surfaceRisk += (surface.Moisture - 0.5f) * 0.2f;
        return TrackSurfaceState.Clamp01(baseRisk + TrackSurfaceState.Clamp01(surfaceRisk));
    }

    private static void ApplyLegacySurfaceWear(
        SimulationSnapshot snapshot,
        TrackState trackState,
        int lane,
        SimLog log)
    {
        var rutsDelta = snapshot.Segment.Type == SegmentType.Straight ? 0.005f : 0.02f;
        trackState.ApplySurfaceDelta(
            snapshot.Step.SegmentIndex,
            lane,
            -0.25f * rutsDelta,
            rutsDelta,
            0f,
            "pass",
            snapshot.Step.HeatId,
            snapshot.Step.StepNumber,
            log);
    }

    private static void ApplyAdvancedSurfaceWear(SimulationSnapshot snapshot, TrackState state,
        float lateralPosition, SimLog log) => ApplyWear(snapshot, state, FixedWear(lateralPosition), log);

    private static ProjectionWear FixedWear(float lateralPosition)
    {
        LateralMovementModel.ValidateLateralPosition(lateralPosition, nameof(lateralPosition));
        var inner = (int)MathF.Floor(lateralPosition); var outer = (int)MathF.Ceiling(lateralPosition);
        var outerWeight = lateralPosition - inner;
        var weights = new float[LaneModel.LanesCount];
        AddAdvancedSurfaceWearKernel(weights, inner, 1f - outerWeight);
        if (outer != inner) AddAdvancedSurfaceWearKernel(weights, outer, outerWeight);
        return new(weights, 1f, false, inner, outer);
    }

    private static void ApplyExecutedSurfaceWear(SimulationSnapshot snapshot, TrackState state,
        ExecutedSegmentPath path, SimLog log) => ApplyWear(snapshot, state, ExecutedWear(path), log);

    private static ProjectionWear ExecutedWear(ExecutedSegmentPath path)
    {
        var weights = new float[LaneModel.LanesCount];
        foreach (var step in path.Steps)
            AddInterpolatedWearKernel(weights, step.SampledLateralPosition, step.DistanceMeters / path.DistanceMeters);
        return FinishExecutedWear(weights, path.Nodes[0].LateralPosition);
    }

    internal static ProjectionWear FinishExecutedWear(float[] weights, float entryLateral)
    {
        var entryWeights = new float[LaneModel.LanesCount];
        AddInterpolatedWearKernel(entryWeights, entryLateral, 1f);
        return new(weights, entryWeights.Sum() / weights.Sum(), true, 0, 0);
    }

    internal static void AddInterpolatedWearKernel(float[] values, float lateral, float weight)
    {
        var inner = (int)MathF.Floor(lateral); var outer = (int)MathF.Ceiling(lateral);
        AddAdvancedSurfaceWearKernel(values, inner, weight * (1f - (lateral - inner)));
        if (outer != inner) AddAdvancedSurfaceWearKernel(values, outer, weight * (lateral - inner));
    }

    private static void ApplyWear(SimulationSnapshot snapshot, TrackState state, ProjectionWear wear, SimLog log)
    {
        var ruts = snapshot.Segment.Type == SegmentType.Straight ? .004f : .015f;
        var gripDelta = -.25f * ruts;
        for (var lane = 0; lane < wear.Weights.Length; lane++)
        {
            var weight = wear.Weights[lane];
            if (weight <= 0f) continue;
            if (wear.Executed)
                state.ApplySurfaceDelta(snapshot.Step.SegmentIndex, lane, -.25f * ruts * weight * wear.Scale,
                    ruts * weight * wear.Scale, 0f, "pass-executed", snapshot.Step.HeatId, snapshot.Step.StepNumber, log);
            else
                state.ApplySurfaceDelta(snapshot.Step.SegmentIndex, lane, gripDelta * weight, ruts * weight, 0f,
                    wear.Inner == wear.Outer ? lane == wear.Inner ? "pass" : "pass-adjacent" : "pass-continuous",
                    snapshot.Step.HeatId, snapshot.Step.StepNumber, log);
        }
    }

    private static void AddAdvancedSurfaceWearKernel(float[] wearWeights, int centerLane, float weight)
    {
        wearWeights[centerLane] += weight;
        if (centerLane > LaneModel.MinLane)
            wearWeights[centerLane - 1] += weight * AdvancedAdjacentSurfaceWearFraction;
        if (centerLane < LaneModel.MaxLane)
            wearWeights[centerLane + 1] += weight * AdvancedAdjacentSurfaceWearFraction;
    }

    private static string FormatSegmentLog(SimulationSnapshot snapshot, RiderStateChange change)
    {
        ProjectionCaptureAudit.Record(ProjectionMaterialization.FormattedLog);
        var prefix = snapshot.Step.UseLegacyPhysics ? string.Empty : $"LAP={snapshot.Step.LapIndex + 1} ";
        var beforeLateralPosition = snapshot.Rider(change.RiderId).LateralPosition;
        return $"{prefix}SEG={snapshot.Segment.Id} {snapshot.Segment.Type} rider={change.RiderId} "
               + $"lane {change.BeforeLane}->{change.PlannedLane}->{change.Lane} targetLane={change.TargetLane} "
               + $"lateral={beforeLateralPosition.ToString("F3", CultureInfo.InvariantCulture)}"
               + $"->{change.LateralPosition.ToString("F3", CultureInfo.InvariantCulture)} "
               + $"outcome={change.Outcome} v_in={change.EntrySpeed.ToString("F2", CultureInfo.InvariantCulture)} "
               + $"v_physics={change.PhysicsSpeed.ToString("F2", CultureInfo.InvariantCulture)} "
               + $"v_out={change.Speed.ToString("F2", CultureInfo.InvariantCulture)}";
    }
}
