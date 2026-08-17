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

public sealed class ResolvedSimulationStep
{
    private readonly ReadOnlyCollection<RiderStateChange> _changes;
    private readonly ReadOnlyCollection<SimulationStepEvent> _events;

    public SimulationSnapshot Snapshot { get; }
    public IReadOnlyList<RiderStateChange> Changes => _changes;
    public IReadOnlyList<SimulationStepEvent> Events => _events;

    internal ResolvedSimulationStep(
        SimulationSnapshot snapshot,
        IEnumerable<RiderStateChange> changes,
        IEnumerable<SimulationStepEvent> events)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _changes = Array.AsReadOnly(changes.OrderBy(change => change.RiderId).ToArray());
        _events = Array.AsReadOnly(events
            .OrderBy(item => item.StepNumber)
            .ThenBy(item => item.Phase)
            .ThenBy(item => item.RiderId)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.OtherRiderId)
            .ToArray());
    }
}

/// <summary>
/// Executes one segment as CaptureSnapshot -> Decide -> Resolve -> Commit.
/// Only Commit is allowed to mutate rider or track state.
/// </summary>
public sealed class SimulationEngine
{
    private readonly IRiderDecisionModel _decisionModel;

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
            rider.Lane,
            rider.LateralPosition,
            rider.Speed,
            rider.Risk,
            rider.Status,
            rider.ElapsedTimeSeconds,
            rider.ActiveSetup,
            rider.Morale,
            rider.ManagerTrust));

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
        var events = new List<SimulationStepEvent>();

        foreach (var rider in snapshot.Riders.Where(rider => rider.IsActive).OrderBy(rider => rider.RiderId))
        {
            var change = ResolveRider(snapshot, rider, intentByRider[rider.RiderId].Decision, options);
            changes.Add(rider.RiderId, change);
            events.Add(new SimulationStepEvent(
                snapshot.Step.StepNumber,
                10,
                rider.RiderId,
                SimulationEventType.SegmentResolved,
                FormatSegmentLog(snapshot, change)));
        }

        if (!snapshot.Step.UseLegacyPhysics)
            ResolveExistingInteractions(snapshot, changes, events);

        return new ResolvedSimulationStep(snapshot, changes.Values, events);
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
            rider.Lane = change.Lane;
            rider.LateralPosition = change.LateralPosition;
            rider.Speed = change.Speed;
            rider.Risk = change.Risk;
            rider.ElapsedTimeSeconds = change.ElapsedTimeSeconds;
            rider.Morale = change.Morale;
            rider.CommitPosition(change.Position);
            rider.SetStatus(change.Status);
        }

        foreach (var simulationEvent in resolved.Events)
            log.Add(simulationEvent.Text);

        foreach (var change in resolved.Changes
                     .Where(change => change.ApplySurfaceWear)
                     .OrderBy(change => change.RiderId))
        {
            if (resolved.Snapshot.Step.UseLegacyPhysics)
                ApplyLegacySurfaceWear(resolved.Snapshot, trackState, change.Lane, log);
            else
                ApplyAdvancedSurfaceWear(resolved.Snapshot, trackState, change.Lane, log);
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

    private static RiderStateChange ResolveRider(
        SimulationSnapshot snapshot,
        RiderSnapshot rider,
        RiderDecision decision,
        HeatSimulationOptions options)
    {
        if (rider.SegmentIndex != snapshot.Step.SegmentIndex)
            throw new InvalidOperationException($"Rider {rider.RiderId} is not in segment {snapshot.Step.SegmentIndex}.");

        var targetLane = LaneModel.ClampLane(decision.TargetLane);
        var plannedLane = CalculatePlannedLane(rider.Lane, targetLane);
        var surface = snapshot.TrackState.GetSurface(snapshot.Step.SegmentIndex, plannedLane);
        var entrySpeed = snapshot.Step.UseLegacyPhysics
            ? ResolveLegacyEntrySpeed(plannedLane, rider)
            : ResolveAdvancedEntrySpeed(snapshot, plannedLane, surface, rider);
        var resolution = snapshot.Step.UseLegacyPhysics
            ? SegmentPhysics.Apply(snapshot.Segment, plannedLane, entrySpeed)
            : SegmentPhysics.Apply(new SegmentPhysicsContext(
                snapshot.Segment,
                plannedLane,
                entrySpeed,
                surface,
                rider.Profile.Skills,
                rider.Morale,
                rider.ActiveSetup,
                decision.Risk));

        if (!snapshot.Step.UseLegacyPhysics)
            resolution = ResolveRandomIncident(snapshot, rider, plannedLane, resolution, options);

        var speed = snapshot.Step.UseLegacyPhysics
            ? resolution.Speed
            : ApplyExitDrive(snapshot.Segment, resolution.Speed, rider);
        var risk = snapshot.Step.UseLegacyPhysics
            ? ApplySurfaceRisk(snapshot, resolution.Lane, decision.Risk)
            : resolution.IncidentRisk;
        var lateralPosition = snapshot.Step.UseLegacyPhysics
            ? resolution.Lane
            : MoveLateralPosition(rider.LateralPosition, resolution.Lane, surface, rider);

        var remainingProgress = 1f - rider.SegmentProgress;
        var canonicalAdvance = resolution.Outcome == SegmentOutcome.Crash
            ? remainingProgress * 0.5f
            : remainingProgress;
        var segmentLength = LaneModel.SegmentLengthMeters(snapshot.Segment, resolution.Lane);
        var travelled = segmentLength * canonicalAdvance;
        var averageSpeed = MathF.Max(1f, (entrySpeed + MathF.Max(speed, 0f)) * 0.5f);
        var elapsedTime = rider.ElapsedTimeSeconds + travelled / averageSpeed;
        var position = rider.Position.Advance(canonicalAdvance, travelled);
        var status = resolution.Outcome == SegmentOutcome.Crash
            ? RiderRaceStatus.Crashed
            : position.LapsCompleted >= snapshot.Step.RequiredLaps
                ? RiderRaceStatus.Finished
                : RiderRaceStatus.Racing;
        var morale = resolution.Outcome == SegmentOutcome.Crash
            ? Math.Clamp(rider.Morale - 0.04f, 0f, 1f)
            : rider.Morale;

        return new RiderStateChange(
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
            morale,
            resolution.Outcome,
            entrySpeed,
            resolution.Speed,
            ApplySurfaceWear: resolution.Outcome != SegmentOutcome.Crash);
    }

    private static void ResolveExistingInteractions(
        SimulationSnapshot snapshot,
        IDictionary<int, RiderStateChange> changes,
        ICollection<SimulationStepEvent> events)
    {
        var groups = changes.Values
            .Where(change => change.Status is RiderRaceStatus.Racing or RiderRaceStatus.Finished)
            .GroupBy(change => change.Lane)
            .OrderBy(group => group.Key)
            .Select(group => group
                .OrderBy(change => change.ElapsedTimeSeconds)
                .ThenBy(change => change.RiderId)
                .ToArray())
            .ToArray();

        foreach (var ordered in groups)
        {
            for (var index = 1; index < ordered.Length; index++)
            {
                var leader = changes[ordered[index - 1].RiderId];
                var trailing = changes[ordered[index].RiderId];
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
                        lateral = MathF.Min(lateral + 0.5f, lane);
                    }

                    trailing = trailing with
                    {
                        Speed = trailing.Speed * 0.82f,
                        ElapsedTimeSeconds = trailing.ElapsedTimeSeconds + 0.20f,
                        Lane = lane,
                        LateralPosition = lateral,
                    };
                    changes[trailing.RiderId] = trailing;
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
            lane,
            surface,
            rider.Profile.Skills,
            rider.Morale,
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
            return resolution with { Outcome = SegmentOutcome.Crash, Speed = 0f };

        return resolution with
        {
            Outcome = SegmentOutcome.RunWide,
            Lane = Math.Min(plannedLane + 1, LaneModel.MaxLane),
            Speed = resolution.Speed * 0.88f,
        };
    }

    private static float ApplyExitDrive(TrackSegment segment, float speed, RiderSnapshot rider)
    {
        if (speed <= 0f || segment.Type != SegmentType.TurnExit)
            return speed;

        var speedSkill = RiderSkills.Normalize(rider.Profile.Skills.Speed);
        var gearingTradeOff = (rider.ActiveSetup.Gearing - 0.5f) * 0.03f;
        return speed * (0.965f + speedSkill * 0.07f + gearingTradeOff);
    }

    private static float MoveLateralPosition(
        float current,
        int targetLane,
        TrackSurfaceState surface,
        RiderSnapshot rider)
    {
        var adaptability = RiderSkills.Normalize(rider.Profile.Skills.Adaptability);
        var style = rider.Profile.Style.LaneChangeTendency;
        var traction = 0.55f + surface.EffectiveGrip * 0.35f;
        var maxStep = (0.45f + adaptability * 0.25f + style * 0.20f) * traction;
        return current + Math.Clamp(targetLane - current, -maxStep, maxStep);
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

    private static void ApplyAdvancedSurfaceWear(
        SimulationSnapshot snapshot,
        TrackState trackState,
        int lane,
        SimLog log)
    {
        var rutsDelta = snapshot.Segment.Type == SegmentType.Straight ? 0.004f : 0.015f;
        var gripDelta = -0.25f * rutsDelta;
        trackState.ApplySurfaceDelta(
            snapshot.Step.SegmentIndex,
            lane,
            gripDelta,
            rutsDelta,
            0f,
            "pass",
            snapshot.Step.HeatId,
            snapshot.Step.StepNumber,
            log);

        foreach (var adjacentLane in new[] { lane - 1, lane + 1 })
        {
            if (adjacentLane < LaneModel.MinLane || adjacentLane > LaneModel.MaxLane)
                continue;
            trackState.ApplySurfaceDelta(
                snapshot.Step.SegmentIndex,
                adjacentLane,
                gripDelta * 0.20f,
                rutsDelta * 0.20f,
                0f,
                "pass-adjacent",
                snapshot.Step.HeatId,
                snapshot.Step.StepNumber,
                log);
        }
    }

    private static string FormatSegmentLog(SimulationSnapshot snapshot, RiderStateChange change)
    {
        var prefix = snapshot.Step.UseLegacyPhysics ? string.Empty : $"LAP={snapshot.Step.LapIndex + 1} ";
        return $"{prefix}SEG={snapshot.Segment.Id} {snapshot.Segment.Type} rider={change.RiderId} "
               + $"lane {change.BeforeLane}->{change.PlannedLane}->{change.Lane} targetLane={change.TargetLane} "
               + $"outcome={change.Outcome} v_in={change.EntrySpeed.ToString("F2", CultureInfo.InvariantCulture)} "
               + $"v_physics={change.PhysicsSpeed.ToString("F2", CultureInfo.InvariantCulture)} "
               + $"v_out={change.Speed.ToString("F2", CultureInfo.InvariantCulture)}";
    }

    private static int CalculatePlannedLane(int currentLane, int targetLane)
    {
        if (targetLane > currentLane)
            return LaneModel.ClampLane(currentLane + 1);
        if (targetLane < currentLane)
            return LaneModel.ClampLane(currentLane - 1);
        return currentLane;
    }
}
