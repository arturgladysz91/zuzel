using CoreSim.Decisions;
using CoreSim.Logging;
using System.Globalization;

namespace CoreSim.Race;

/// <summary>
/// Advances one decision segment in three phases: immutable snapshot, movement
/// and battle proposals, then one simultaneous commit. It intentionally models
/// race consequences rather than continuous motorcycle dynamics.
/// </summary>
internal sealed class SegmentRaceEngine
{
    private const float OccupancyWindowSeconds = 0.30f;
    private const float AttackClearanceSeconds = 0.015f;
    private readonly IRiderDecisionModel _decision;

    public SegmentRaceEngine(IRiderDecisionModel decision)
        => _decision = decision ?? throw new ArgumentNullException(nameof(decision));

    public void SimulateSegment(
        Track track,
        TrackState trackState,
        IReadOnlyList<RiderState> riders,
        int segmentIndex,
        int lap,
        int heatId,
        int tick,
        HeatSimulationOptions options,
        SimLog log)
    {
        var segment = track.Segments[segmentIndex];
        var snapshot = BuildSnapshot(trackState, riders, segment, segmentIndex, lap, heatId);
        var isRaceStart = IsRaceStart(snapshot);
        var proposals = new Dictionary<int, MoveProposal>();

        // Stable order protects callers that provide stateful legacy decision
        // models. Every model receives fresh clones from the same snapshot.
        foreach (var rider in snapshot.Riders.Where(rider => !rider.IsCrashed).OrderBy(rider => rider.RiderId))
        {
            var decision = DecideFromSnapshot(track, trackState, snapshot, rider);
            proposals.Add(
                rider.RiderId,
                BuildProposal(snapshot, rider, decision, isRaceStart, options, log));
        }

        ResolveBattles(snapshot, proposals, isRaceStart, options, log);
        ResolveImpossibleOccupancy(snapshot, proposals, options, log);
        Commit(snapshot, riders, proposals, log);
        ApplySurfaceWear(trackState, snapshot, proposals, heatId, tick, log);
    }

    private RiderDecision DecideFromSnapshot(
        Track track,
        TrackState trackState,
        SegmentRaceSnapshot snapshot,
        RiderSegmentSnapshot rider)
    {
        var decisionRiders = snapshot.Riders
            .OrderBy(item => item.RiderId)
            .Select(item => item.ToDecisionState())
            .ToArray();
        var decisionRider = decisionRiders.Single(item => item.RiderId == rider.RiderId);
        var context = new RiderDecisionContext(
            snapshot.Segment,
            snapshot.SegmentIndex,
            trackState.Snapshot(),
            decisionRider,
            decisionRiders,
            snapshot.HeatId,
            snapshot.Lap - 1,
            track);
        return _decision.Decide(context);
    }

    private static SegmentRaceSnapshot BuildSnapshot(
        TrackState trackState,
        IReadOnlyList<RiderState> riders,
        TrackSegment segment,
        int segmentIndex,
        int lap,
        int heatId)
    {
        var ordered = riders
            .OrderBy(rider => rider.IsCrashed)
            .ThenBy(rider => rider.IsCrashed ? float.MaxValue : rider.ElapsedTimeSeconds)
            .ThenByDescending(rider => rider.DistanceMeters)
            .ThenBy(rider => rider.StartingGate)
            .ThenBy(rider => rider.RiderId)
            .ToArray();
        var snapshots = new List<RiderSegmentSnapshot>(ordered.Length);

        for (var index = 0; index < ordered.Length; index++)
        {
            var rider = ordered[index];
            var leader = index == 0 ? null : ordered[index - 1];
            var gapSeconds = leader is null || rider.IsCrashed
                ? float.PositiveInfinity
                : MathF.Max(0f, rider.ElapsedTimeSeconds - leader.ElapsedTimeSeconds);
            var referenceSpeed = leader is null
                ? 0f
                : MathF.Max(1f, (rider.Speed + leader.Speed) * 0.5f);
            var occupiedLanes = ordered
                .Where(other => other.RiderId != rider.RiderId
                                && !other.IsCrashed
                                && MathF.Abs(other.ElapsedTimeSeconds - rider.ElapsedTimeSeconds) <= OccupancyWindowSeconds)
                .Select(other => other.Lane)
                .ToHashSet();

            snapshots.Add(new RiderSegmentSnapshot(
                rider.RiderId,
                rider.Profile,
                rider.ActiveSetup,
                rider.StartingGate,
                lap,
                segmentIndex,
                segment.Id,
                rider.SegmentProgressMeters,
                rider.Lane,
                rider.LateralPosition,
                rider.Speed,
                rider.CornerExitSpeed,
                rider.ElapsedTimeSeconds,
                rider.DistanceMeters,
                rider.Morale,
                rider.ManagerTrust,
                rider.IsCrashed,
                index + 1,
                gapSeconds,
                float.IsPositiveInfinity(gapSeconds) ? float.PositiveInfinity : gapSeconds * referenceSpeed,
                occupiedLanes));
        }

        var surfaces = Enumerable.Range(LaneModel.MinLane, LaneModel.LanesCount)
            .Select(lane => trackState.GetSurface(segmentIndex, lane))
            .ToArray();
        return new SegmentRaceSnapshot(heatId, lap + 1, segmentIndex, segment, surfaces, snapshots);
    }

    private static bool IsRaceStart(SegmentRaceSnapshot snapshot)
        => snapshot.Lap == 1
           && snapshot.SegmentIndex == 0
           && snapshot.Riders.Where(rider => !rider.IsCrashed).All(rider =>
               rider.DistanceMeters <= 0.001f
               && rider.ElapsedTimeSeconds <= 0.001f
               && rider.Speed <= 0.001f);

    private static MoveProposal BuildProposal(
        SegmentRaceSnapshot snapshot,
        RiderSegmentSnapshot rider,
        RiderDecision decision,
        bool isRaceStart,
        HeatSimulationOptions options,
        SimLog log)
    {
        var targetLane = LaneModel.ClampLane(decision.TargetLane);
        var plannedLane = CalculatePlannedLane(rider.Lane, targetLane);
        var proposal = new MoveProposal(rider, decision, plannedLane);

        if (isRaceStart)
        {
            var startSkill = RiderSkills.Normalize(rider.Profile.Skills.Start);
            var startSurface = snapshot.Surfaces[rider.StartingGate];
            var startGrip = startSurface.EffectiveGrip;
            var torqueFromGearing = (0.5f - rider.ActiveSetup.Gearing) * 0.07f;
            var launchMultiplier = 0.86f
                                   + startSkill * 0.12f
                                   + torqueFromGearing
                                   + (startGrip - 0.5f) * 0.06f;
            var safeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
                plannedLane,
                snapshot.Surfaces[plannedLane],
                rider.Profile.Skills,
                rider.Morale,
                rider.ActiveSetup);
            proposal.EntrySpeed = safeSpeed * launchMultiplier;
            var reactionJitter = DeterministicRandom.SampleSigned(
                    options.Seed,
                    snapshot.HeatId,
                    rider.RiderId,
                    rider.StartingGate,
                    301)
                * (1f - startSkill)
                * 0.012f;
            proposal.ReactionDelay = MathF.Max(
                0.055f,
                0.155f - startSkill * 0.075f + (1f - startGrip) * 0.035f + reactionJitter);
            AddEvent(
                log,
                new RaceEvent(
                    RaceEventType.Start,
                    snapshot.Lap,
                    snapshot.Segment.Id,
                    rider.RiderId,
                    Lane: rider.StartingGate,
                    Speed: proposal.EntrySpeed,
                    Detail: $"reaction={proposal.ReactionDelay.ToString("F3", CultureInfo.InvariantCulture)}s grip={startGrip.ToString("F2", CultureInfo.InvariantCulture)}"));
        }
        else
        {
            proposal.EntrySpeed = rider.Speed > 0f
                ? rider.Speed
                : SegmentPhysics.MaxSafeTurnSpeed(
                    plannedLane,
                    snapshot.Surfaces[plannedLane],
                    rider.Profile.Skills,
                    rider.Morale,
                    rider.ActiveSetup) * 0.96f;
        }

        ResolvePath(snapshot, proposal, plannedLane, options);
        return proposal;
    }

    private static void ResolvePath(
        SegmentRaceSnapshot snapshot,
        MoveProposal proposal,
        int lane,
        HeatSimulationOptions options)
    {
        proposal.PlannedLane = LaneModel.ClampLane(lane);
        var surface = snapshot.Surfaces[proposal.PlannedLane];
        var resolution = SegmentPhysics.Apply(new SegmentPhysicsContext(
            snapshot.Segment,
            proposal.PlannedLane,
            proposal.EntrySpeed,
            surface,
            proposal.Source.Profile.Skills,
            proposal.Source.Morale,
            proposal.Source.ActiveSetup,
            proposal.Decision.Risk));
        resolution = ResolveIncident(snapshot, proposal.Source, proposal.PlannedLane, resolution, options);

        proposal.Outcome = resolution.Outcome;
        proposal.Lane = resolution.Lane;
        proposal.PhysicsSpeed = resolution.Speed;
        proposal.ExitSpeed = ApplyExitDrive(snapshot.Segment, resolution.Speed, proposal.Source);
        proposal.Risk = resolution.IncidentRisk;
        proposal.Crashed = resolution.Outcome == SegmentOutcome.Crash;
        proposal.SegmentLength = LaneModel.SegmentLengthMeters(snapshot.Segment, proposal.Lane);
        proposal.Travelled = proposal.Crashed ? proposal.SegmentLength * 0.5f : proposal.SegmentLength;
        var averageSpeed = MathF.Max(1f, (proposal.EntrySpeed + MathF.Max(proposal.ExitSpeed, 0f)) * 0.5f);
        var adaptability = RiderSkills.Normalize(proposal.Source.Profile.Skills.Adaptability);
        var laneChangeDistance = MathF.Abs(proposal.PlannedLane - proposal.Source.LateralPosition);
        var laneChangeCost = laneChangeDistance * (0.010f + (1f - adaptability) * 0.025f);
        proposal.BaseSegmentTime = proposal.Travelled / averageSpeed + proposal.ReactionDelay + laneChangeCost;
        proposal.SegmentProgress = proposal.Travelled;
    }

    private static SegmentResolution ResolveIncident(
        SegmentRaceSnapshot snapshot,
        RiderSegmentSnapshot rider,
        int plannedLane,
        SegmentResolution resolution,
        HeatSimulationOptions options)
    {
        if (options.IncidentFrequency <= 0f
            || snapshot.Segment.Type == SegmentType.Straight
            || resolution.Outcome == SegmentOutcome.Crash)
            return resolution;

        var probability = resolution.IncidentRisk * 0.08f * options.IncidentFrequency;
        var incidentSample = DeterministicRandom.Sample01(
            options.Seed,
            snapshot.HeatId,
            snapshot.Lap,
            snapshot.SegmentIndex,
            rider.RiderId,
            plannedLane,
            401);
        if (incidentSample >= probability)
            return resolution;

        var severity = DeterministicRandom.Sample01(
            options.Seed,
            snapshot.HeatId,
            snapshot.Lap,
            snapshot.SegmentIndex,
            rider.RiderId,
            plannedLane,
            402);
        if (severity < resolution.IncidentRisk * 0.35f || plannedLane == LaneModel.MaxLane)
            return resolution with { Outcome = SegmentOutcome.Crash, Speed = 0f };

        return resolution with
        {
            Outcome = SegmentOutcome.RunWide,
            Lane = Math.Min(plannedLane + 1, LaneModel.MaxLane),
            Speed = resolution.Speed * 0.88f,
        };
    }

    private static float ApplyExitDrive(
        TrackSegment segment,
        float speed,
        RiderSegmentSnapshot rider)
    {
        if (speed <= 0f || segment.Type != SegmentType.TurnExit)
            return speed;

        var speedSkill = RiderSkills.Normalize(rider.Profile.Skills.Speed);
        var lowGearingDrive = (0.5f - rider.ActiveSetup.Gearing) * 0.035f;
        var tractionStability = (rider.ActiveSetup.TractionBias - 0.5f) * 0.018f;
        return speed * (0.965f + speedSkill * 0.07f + lowGearingDrive + tractionStability);
    }

    private static void ResolveBattles(
        SegmentRaceSnapshot snapshot,
        IReadOnlyDictionary<int, MoveProposal> proposals,
        bool isRaceStart,
        HeatSimulationOptions options,
        SimLog log)
    {
        var activeOrder = snapshot.Riders
            .Where(rider => !rider.IsCrashed)
            .OrderBy(rider => rider.RunningPosition)
            .ToArray();

        for (var index = 1; index < activeOrder.Length; index++)
        {
            var attacker = activeOrder[index];
            var defender = activeOrder[index - 1];
            var attackerMove = proposals[attacker.RiderId];
            var defenderMove = proposals[defender.RiderId];
            if (attackerMove.Crashed || defenderMove.Crashed)
                continue;

            var aggression = attacker.Profile.Style.RiskTolerance;
            var gapLimit = 0.32f + aggression * 0.10f;
            var exitAdvantage = attacker.CornerExitSpeed - defender.CornerExitSpeed;
            var speedAdvantage = snapshot.Segment.Type == SegmentType.Straight
                ? exitAdvantage
                : attackerMove.EntrySpeed - defenderMove.EntrySpeed;
            if (isRaceStart)
                speedAdvantage = attackerMove.EntrySpeed - defenderMove.EntrySpeed;

            var canAttackOnStraight = snapshot.Segment.Type != SegmentType.Straight
                                      || isRaceStart
                                      || attacker.CornerExitSpeed > 0f && exitAdvantage > 0f;
            var requiredAdvantage = 0.45f + (1f - aggression) * 0.35f;
            if (!canAttackOnStraight
                || attacker.GapAheadSeconds > gapLimit
                || speedAdvantage < requiredAdvantage)
                continue;

            var fallbackLane = attackerMove.PlannedLane;
            var attackLane = FindAttackLane(snapshot, attacker, defender, proposals);
            AddEvent(
                log,
                new RaceEvent(
                    RaceEventType.AttackStarted,
                    snapshot.Lap,
                    snapshot.Segment.Id,
                    attacker.RiderId,
                    defender.RiderId,
                    attackLane,
                    attacker.GapAheadSeconds,
                    attackerMove.EntrySpeed,
                    $"speedAdv={speedAdvantage.ToString("F2", CultureInfo.InvariantCulture)}"));

            if (attackLane is null)
            {
                FailAttack(snapshot, attackerMove, fallbackLane, options, 0.07f, 0.95f);
                AddEvent(
                    log,
                    new RaceEvent(
                        RaceEventType.AttackBlocked,
                        snapshot.Lap,
                        snapshot.Segment.Id,
                        attacker.RiderId,
                        defender.RiderId,
                        GapSeconds: attacker.GapAheadSeconds,
                        Detail: "no free reachable lane"));
                continue;
            }

            ResolvePath(snapshot, attackerMove, attackLane.Value, options);
            if (CanCloseLane(snapshot, defender, attacker, attackLane.Value, speedAdvantage))
            {
                ResolvePath(snapshot, defenderMove, attackLane.Value, options);
                if (!defenderMove.Crashed && defenderMove.Lane == attackLane.Value)
                {
                    FailAttack(snapshot, attackerMove, fallbackLane, options, 0.18f, 0.90f);
                    AddEvent(
                        log,
                        new RaceEvent(
                            RaceEventType.Defense,
                            snapshot.Lap,
                            snapshot.Segment.Id,
                            defender.RiderId,
                            attacker.RiderId,
                            attackLane,
                            attacker.GapAheadSeconds,
                            defenderMove.ExitSpeed,
                            "closed attack lane"));
                    AddEvent(
                        log,
                        new RaceEvent(
                            RaceEventType.AttackBlocked,
                            snapshot.Lap,
                            snapshot.Segment.Id,
                            attacker.RiderId,
                            defender.RiderId,
                            attackLane,
                            attacker.GapAheadSeconds,
                            Detail: "defender closed lane"));
                    continue;
                }
            }

            var projectedMargin = defenderMove.EndTime - attackerMove.EndTime;
            if (projectedMargin > AttackClearanceSeconds)
            {
                attackerMove.SuccessfulAttack = true;
                AddEvent(
                    log,
                    new RaceEvent(
                        RaceEventType.AttackSucceeded,
                        snapshot.Lap,
                        snapshot.Segment.Id,
                        attacker.RiderId,
                        defender.RiderId,
                        attackLane,
                        attacker.GapAheadSeconds,
                        attackerMove.ExitSpeed,
                        $"projectedClearance={projectedMargin.ToString("F3", CultureInfo.InvariantCulture)}s"));
                continue;
            }

            if (TryResolveAttackContact(snapshot, attackerMove, defenderMove, options, log))
                continue;

            var failurePenalty = 0.12f + aggression * 0.10f;
            var speedRetention = 0.94f - aggression * 0.06f;
            FailAttack(snapshot, attackerMove, fallbackLane, options, failurePenalty, speedRetention);
            AddEvent(
                log,
                new RaceEvent(
                    RaceEventType.AttackFailed,
                    snapshot.Lap,
                    snapshot.Segment.Id,
                    attacker.RiderId,
                    defender.RiderId,
                    attackLane,
                    attacker.GapAheadSeconds,
                    attackerMove.ExitSpeed,
                    $"insufficient progress margin={projectedMargin.ToString("F3", CultureInfo.InvariantCulture)}s"));
        }
    }

    private static int? FindAttackLane(
        SegmentRaceSnapshot snapshot,
        RiderSegmentSnapshot attacker,
        RiderSegmentSnapshot defender,
        IReadOnlyDictionary<int, MoveProposal> proposals)
    {
        var candidates = new[] { defender.Lane - 1, defender.Lane + 1 }
            .Where(lane => lane >= LaneModel.MinLane && lane <= LaneModel.MaxLane)
            .Where(lane => MathF.Abs(lane - attacker.LateralPosition) <= 1.05f)
            .Where(lane => !IsLaneOccupiedByThirdRider(snapshot, attacker, defender, lane))
            .Select(lane => new
            {
                Lane = lane,
                Cost = LaneModel.SegmentLengthMeters(snapshot.Segment, lane)
                       / MathF.Max(1f, SegmentPhysics.MaxSafeTurnSpeed(
                           lane,
                           snapshot.Surfaces[lane],
                           attacker.Profile.Skills,
                           attacker.Morale,
                           attacker.ActiveSetup))
                       + MathF.Abs(lane - attacker.Profile.Style.OutsidePreference * LaneModel.MaxLane) * 0.01f,
            })
            .OrderBy(candidate => candidate.Cost)
            .ThenBy(candidate => candidate.Lane)
            .ToArray();

        return candidates.Length == 0 ? null : candidates[0].Lane;
    }

    private static bool IsLaneOccupiedByThirdRider(
        SegmentRaceSnapshot snapshot,
        RiderSegmentSnapshot attacker,
        RiderSegmentSnapshot defender,
        int lane)
        => snapshot.Riders.Any(other =>
            !other.IsCrashed
            && other.RiderId != attacker.RiderId
            && other.RiderId != defender.RiderId
            && MathF.Abs(other.LateralPosition - lane) < 0.55f
            && MathF.Abs(other.ElapsedTimeSeconds - attacker.ElapsedTimeSeconds) <= OccupancyWindowSeconds);

    private static bool CanCloseLane(
        SegmentRaceSnapshot snapshot,
        RiderSegmentSnapshot defender,
        RiderSegmentSnapshot attacker,
        int attackLane,
        float speedAdvantage)
    {
        if (MathF.Abs(attackLane - defender.LateralPosition) > 1.05f
            || IsLaneOccupiedByThirdRider(snapshot, attacker, defender, attackLane))
            return false;

        var pairRiding = RiderSkills.Normalize(defender.Profile.Skills.PairRiding);
        var control = RiderSkills.Normalize(defender.Profile.Skills.SlideControl);
        var reading = RiderSkills.Normalize(defender.Profile.Skills.TrackReading);
        var defenseScore = pairRiding * 0.45f + control * 0.35f + reading * 0.20f;
        var attackPressure = 0.35f
                             + attacker.Profile.Style.RiskTolerance * 0.25f
                             + Math.Clamp(speedAdvantage / 3f, 0f, 1f) * 0.40f;
        return defenseScore > attackPressure;
    }

    private static bool TryResolveAttackContact(
        SegmentRaceSnapshot snapshot,
        MoveProposal attacker,
        MoveProposal defender,
        HeatSimulationOptions options,
        SimLog log)
    {
        var timeSeparation = MathF.Abs(attacker.EndTime - defender.EndTime);
        if (timeSeparation > 0.075f || options.IncidentFrequency <= 0f)
            return false;

        var aggression = attacker.Source.Profile.Style.RiskTolerance;
        var control = RiderSkills.Normalize(attacker.Source.Profile.Skills.SlideControl);
        var locationFactor = snapshot.Segment.Type switch
        {
            SegmentType.TurnMiddle => 1.45f,
            SegmentType.TurnEntry or SegmentType.TurnExit => 1.10f,
            _ => 0.55f,
        };
        var contactRisk = aggression
                          * (1.15f - control * 0.55f)
                          * locationFactor
                          * 0.22f
                          * options.IncidentFrequency;
        var sample = DeterministicRandom.Sample01(
            options.Seed,
            snapshot.HeatId,
            snapshot.Lap,
            snapshot.SegmentIndex,
            attacker.Source.RiderId,
            defender.Source.RiderId,
            501);
        if (sample >= contactRisk)
            return false;

        var crashRisk = snapshot.Segment.Type == SegmentType.TurnMiddle
            ? 0.24f * (1.20f - control * 0.55f)
            : 0.07f * (1.20f - control * 0.55f);
        var severeSample = DeterministicRandom.Sample01(
            options.Seed,
            snapshot.HeatId,
            snapshot.Lap,
            snapshot.SegmentIndex,
            attacker.Source.RiderId,
            defender.Source.RiderId,
            502);
        if (severeSample < crashRisk)
        {
            attacker.Crashed = true;
            attacker.Outcome = SegmentOutcome.Crash;
            attacker.ExitSpeed = 0f;
            attacker.Travelled = attacker.SegmentLength * 0.5f;
            attacker.SegmentProgress = attacker.Travelled;
        }
        else
        {
            attacker.ExitSpeed *= 0.82f;
            attacker.TimePenalty += 0.20f;
        }

        AddEvent(
            log,
            new RaceEvent(
                RaceEventType.Contact,
                snapshot.Lap,
                snapshot.Segment.Id,
                attacker.Source.RiderId,
                defender.Source.RiderId,
                attacker.Lane,
                MathF.Abs(attacker.EndTime - defender.EndTime),
                attacker.ExitSpeed,
                attacker.Crashed ? "aggressive attack crash" : "aggressive attack lost rhythm"));
        return true;
    }

    private static void FailAttack(
        SegmentRaceSnapshot snapshot,
        MoveProposal attacker,
        int fallbackLane,
        HeatSimulationOptions options,
        float timePenalty,
        float speedRetention)
    {
        ResolvePath(snapshot, attacker, fallbackLane, options);
        attacker.TimePenalty += timePenalty;
        attacker.ExitSpeed *= speedRetention;
        attacker.FailedAttack = true;
    }

    private static void ResolveImpossibleOccupancy(
        SegmentRaceSnapshot snapshot,
        IReadOnlyDictionary<int, MoveProposal> proposals,
        HeatSimulationOptions options,
        SimLog log)
    {
        var minimumGapSeconds = snapshot.Segment.Type == SegmentType.Straight ? 0.055f : 0.085f;
        var minimumGapMeters = snapshot.Segment.Type == SegmentType.Straight ? 1.10f : 1.35f;

        foreach (var laneGroup in proposals.Values
                     .Where(proposal => !proposal.Crashed)
                     .GroupBy(proposal => proposal.Lane)
                     .OrderBy(group => group.Key))
        {
            var ordered = laneGroup.OrderBy(proposal => proposal.EndTime)
                .ThenBy(proposal => proposal.Source.RiderId)
                .ToArray();
            if (ordered.Length == 0)
                continue;

            ordered[0].SegmentProgress = ordered[0].SegmentLength;
            for (var index = 1; index < ordered.Length; index++)
            {
                var leader = ordered[index - 1];
                var trailing = ordered[index];
                var requiredEndTime = leader.EndTime + minimumGapSeconds;
                if (trailing.EndTime < requiredEndTime)
                {
                    trailing.TimePenalty += requiredEndTime - trailing.EndTime;
                    trailing.ExitSpeed *= 0.94f;
                    if (trailing.SuccessfulAttack)
                    {
                        trailing.SuccessfulAttack = false;
                        trailing.FailedAttack = true;
                        AddEvent(
                            log,
                            new RaceEvent(
                                RaceEventType.AttackBlocked,
                                snapshot.Lap,
                                snapshot.Segment.Id,
                                trailing.Source.RiderId,
                                leader.Source.RiderId,
                                trailing.Lane,
                                Detail: "insufficient physical clearance"));
                    }
                }

                trailing.SegmentProgress = MathF.Max(0f, leader.SegmentProgress - minimumGapMeters);
            }
        }
    }

    private static void Commit(
        SegmentRaceSnapshot snapshot,
        IReadOnlyList<RiderState> riders,
        IReadOnlyDictionary<int, MoveProposal> proposals,
        SimLog log)
    {
        var statesById = riders.ToDictionary(rider => rider.RiderId);
        foreach (var proposal in proposals.Values.OrderBy(proposal => proposal.Source.RiderId))
        {
            var rider = statesById[proposal.Source.RiderId];
            rider.CurrentLap = snapshot.Lap;
            rider.CurrentSegmentIndex = snapshot.SegmentIndex;
            rider.CurrentSegmentId = snapshot.Segment.Id;
            rider.SegmentProgressMeters = proposal.SegmentProgress;
            rider.Lane = proposal.Lane;
            rider.LateralPosition = MoveLateralPosition(
                proposal.Source.LateralPosition,
                proposal.Lane,
                snapshot.Surfaces[proposal.Lane],
                proposal.Source);
            rider.Speed = proposal.Crashed ? 0f : proposal.ExitSpeed;
            rider.Risk = proposal.Risk;
            rider.ElapsedTimeSeconds = proposal.EndTime;
            rider.DistanceMeters = proposal.Source.DistanceMeters + proposal.Travelled;
            rider.IsCrashed = proposal.Crashed;
            rider.CornerExitSpeed = snapshot.Segment.Type switch
            {
                SegmentType.TurnExit => rider.Speed,
                SegmentType.Straight => proposal.Source.CornerExitSpeed,
                _ => 0f,
            };

            if (proposal.Crashed)
                rider.ApplyMoraleDelta(-0.04f);

            log.Add(FormatSegmentLog(snapshot, proposal, rider));
            if (proposal.Outcome == SegmentOutcome.RunWide)
            {
                AddEvent(
                    log,
                    new RaceEvent(
                        RaceEventType.RunWide,
                        snapshot.Lap,
                        snapshot.Segment.Id,
                        rider.RiderId,
                        Lane: rider.Lane,
                        Speed: rider.Speed,
                        Detail: "corner speed exceeded available radius/grip"));
            }

            if (rider.Lane != proposal.Source.Lane)
            {
                AddEvent(
                    log,
                    new RaceEvent(
                        RaceEventType.LaneChanged,
                        snapshot.Lap,
                        snapshot.Segment.Id,
                        rider.RiderId,
                        Lane: rider.Lane,
                        Speed: rider.Speed,
                        Detail: $"{proposal.Source.Lane}->{rider.Lane}"));
            }
        }

        UpdateRelativePositions(riders);
    }

    private static void UpdateRelativePositions(IReadOnlyList<RiderState> riders)
    {
        var active = riders.Where(rider => !rider.IsCrashed)
            .OrderBy(rider => rider.ElapsedTimeSeconds)
            .ThenByDescending(rider => rider.DistanceMeters)
            .ThenBy(rider => rider.RiderId)
            .ToArray();

        for (var index = 0; index < active.Length; index++)
        {
            var rider = active[index];
            if (index == 0)
            {
                rider.GapToRiderAheadSeconds = float.PositiveInfinity;
                rider.GapToRiderAheadMeters = float.PositiveInfinity;
            }
            else
            {
                var leader = active[index - 1];
                rider.GapToRiderAheadSeconds = MathF.Max(0f, rider.ElapsedTimeSeconds - leader.ElapsedTimeSeconds);
                rider.GapToRiderAheadMeters = rider.GapToRiderAheadSeconds
                                               * MathF.Max(1f, (rider.Speed + leader.Speed) * 0.5f);
            }

            rider.OccupiedLanes = active
                .Where(other => other.RiderId != rider.RiderId
                                && MathF.Abs(other.ElapsedTimeSeconds - rider.ElapsedTimeSeconds) <= OccupancyWindowSeconds)
                .Select(other => other.Lane)
                .Distinct()
                .OrderBy(lane => lane)
                .ToArray();
        }
    }

    private static void ApplySurfaceWear(
        TrackState trackState,
        SegmentRaceSnapshot snapshot,
        IReadOnlyDictionary<int, MoveProposal> proposals,
        int heatId,
        int tick,
        SimLog log)
    {
        foreach (var proposal in proposals.Values
                     .Where(proposal => !proposal.Crashed)
                     .OrderBy(proposal => proposal.Source.RiderId))
        {
            var rutsDelta = snapshot.Segment.Type == SegmentType.Straight ? 0.004f : 0.015f;
            var gripDelta = -0.25f * rutsDelta;
            trackState.ApplySurfaceDelta(
                snapshot.SegmentIndex,
                proposal.Lane,
                gripDelta,
                rutsDelta,
                0f,
                "pass",
                heatId,
                tick,
                log);

            foreach (var adjacentLane in new[] { proposal.Lane - 1, proposal.Lane + 1 })
            {
                if (adjacentLane < LaneModel.MinLane || adjacentLane > LaneModel.MaxLane)
                    continue;
                trackState.ApplySurfaceDelta(
                    snapshot.SegmentIndex,
                    adjacentLane,
                    gripDelta * 0.20f,
                    rutsDelta * 0.20f,
                    0f,
                    "pass-adjacent",
                    heatId,
                    tick,
                    log);
            }
        }
    }

    private static float MoveLateralPosition(
        float current,
        int targetLane,
        TrackSurfaceState surface,
        RiderSegmentSnapshot rider)
    {
        var adaptability = RiderSkills.Normalize(rider.Profile.Skills.Adaptability);
        var style = rider.Profile.Style.LaneChangeTendency;
        var traction = 0.55f + surface.EffectiveGrip * 0.35f;
        var maxStep = (0.45f + adaptability * 0.25f + style * 0.20f) * traction;
        return current + Math.Clamp(targetLane - current, -maxStep, maxStep);
    }

    private static int CalculatePlannedLane(int currentLane, int targetLane)
    {
        if (targetLane > currentLane)
            return LaneModel.ClampLane(currentLane + 1);
        if (targetLane < currentLane)
            return LaneModel.ClampLane(currentLane - 1);
        return currentLane;
    }

    private static string FormatSegmentLog(
        SegmentRaceSnapshot snapshot,
        MoveProposal proposal,
        RiderState rider)
        => $"LAP={snapshot.Lap} SEG={snapshot.Segment.Id} {snapshot.Segment.Type} rider={rider.RiderId} "
           + $"lane {proposal.Source.Lane}->{proposal.PlannedLane}->{rider.Lane} targetLane={proposal.Decision.TargetLane} "
           + $"outcome={proposal.Outcome} v_in={proposal.EntrySpeed.ToString("F2", CultureInfo.InvariantCulture)} "
           + $"v_physics={proposal.PhysicsSpeed.ToString("F2", CultureInfo.InvariantCulture)} "
           + $"v_out={rider.Speed.ToString("F2", CultureInfo.InvariantCulture)} "
           + $"progress={rider.SegmentProgressMeters.ToString("F2", CultureInfo.InvariantCulture)}m "
           + $"gap={FormatGap(rider.GapToRiderAheadSeconds)}";

    private static string FormatGap(float value)
        => float.IsPositiveInfinity(value)
            ? "leader"
            : value.ToString("F3", CultureInfo.InvariantCulture) + "s";

    private static void AddEvent(SimLog log, RaceEvent raceEvent)
    {
        log.Add(raceEvent);
        var other = raceEvent.OtherRiderId.HasValue ? $" other={raceEvent.OtherRiderId.Value}" : string.Empty;
        var lane = raceEvent.Lane.HasValue ? $" lane={raceEvent.Lane.Value}" : string.Empty;
        var detail = string.IsNullOrWhiteSpace(raceEvent.Detail) ? string.Empty : $" detail={raceEvent.Detail}";
        log.Add($"RACE_EVENT type={raceEvent.Type} lap={raceEvent.Lap} seg={raceEvent.SegmentId} rider={raceEvent.RiderId}{other}{lane}{detail}");
    }

    private sealed class MoveProposal
    {
        public MoveProposal(RiderSegmentSnapshot source, RiderDecision decision, int plannedLane)
        {
            Source = source;
            Decision = decision;
            PlannedLane = plannedLane;
            Lane = plannedLane;
        }

        public RiderSegmentSnapshot Source { get; }
        public RiderDecision Decision { get; }
        public int PlannedLane { get; set; }
        public int Lane { get; set; }
        public float EntrySpeed { get; set; }
        public float PhysicsSpeed { get; set; }
        public float ExitSpeed { get; set; }
        public float Risk { get; set; }
        public SegmentOutcome Outcome { get; set; }
        public float ReactionDelay { get; set; }
        public float BaseSegmentTime { get; set; }
        public float TimePenalty { get; set; }
        public float SegmentLength { get; set; }
        public float SegmentProgress { get; set; }
        public float Travelled { get; set; }
        public bool Crashed { get; set; }
        public bool SuccessfulAttack { get; set; }
        public bool FailedAttack { get; set; }
        public float EndTime => Source.ElapsedTimeSeconds + BaseSegmentTime + TimePenalty;
    }
}
