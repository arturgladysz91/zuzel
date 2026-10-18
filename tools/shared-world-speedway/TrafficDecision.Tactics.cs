using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

// Same TrafficDecision, with separate tactical intent and immediate safety evaluation.
// All estimates are short observations of current state, never alternate world runs.
public static partial class TrafficDecision
{
    private static readonly SpeedwayBikeDimensions Bike = SpeedwayBikeDimensions.Reference;
    // Provisional decision policy, not new physical constants or calibrated abilities.
    private const double SafetyHorizonSeconds = .5;
    internal const double IntentMinimumSeconds = .8;
    private const double ClearObservationSeconds = .4;
    private const double RecoveryLookaheadSeconds = .6;
    private const double AttackTimeoutSeconds = 4;
    private const float NearbyRadiusMeters = 30;
    private const float MinimumClosingMetersPerSecond = .25f;
    private const float TargetMarginMeters = .5f;
    private const float TargetChangeMeters = .05f;
    private const float ReturnCompletionMeters = .1f;
    private const float ClearFrontGapMeters = 8;
    private const float BaseMarginMeters = .12f, CautiousMarginMeters = .22f, ExecutionMarginMeters = .06f;
    private const double AttackBaseHorizonSeconds = .8, DefenseBaseHorizonSeconds = .55, AbilityHorizonSeconds = 2.2;
    private const float InsistenceCombativeness = .85f;
    private const double InsistenceHorizonSeconds = .18;
    private const double LineScoreWeight = .03;
    private const double CrossingProjectionSquareMeters = .05;
    private const double MinimumMovementSeconds = .05, MinimumReachFraction = .05;
    private static double HalfWidth(RiderView rider, double roadHeading)
    {
        var angle = rider.Heading - roadHeading; var sin = Math.Abs(Math.Sin(angle)); var cos = Math.Abs(Math.Cos(angle));
        return Math.Max((Bike.OverallMechanicalLengthMeters - Bike.ChassisBodyWidthMeters) / 2 * sin + Bike.ChassisBodyWidthMeters / 2,
            Bike.HandlebarLongitudinalOffsetMeters * sin + (Bike.HandlebarWidthMeters - Bike.HandlebarTubeDiameterMeters) / 2 * cos
                + Bike.HandlebarTubeDiameterMeters / 2);
    }
    public static IReadOnlyList<RacingPair> ObservePairs(WorldSnapshot snapshot, WorldWork work)
    {
        var pairs = new List<RacingPair>(6); var count = 0;
        for (var a = 0; a < snapshot.Riders.Count; a++) for (var b = a + 1; b < snapshot.Riders.Count; b++)
        {
            count++; work.TacticalPairObservations++;
            if (Vector2.DistanceSquared(snapshot.Riders[a].Position, snapshot.Riders[b].Position) <= NearbyRadiusMeters * NearbyRadiusMeters)
                pairs.Add(new(a, b));
        }
        work.MaximumTacticalPairs = Math.Max(work.MaximumTacticalPairs, count);
        return pairs;
    }
    public static DecisionEvent? Observe(WorldSnapshot snapshot, RiderView own, RiderSpec spec,
        WorldParameters p, WorldWork work, TacticalMemory memory, IReadOnlyList<RacingPair> pairs,
        TrackFrame frame, TrackStateSnapshot surface, bool safetyOnly = false)
    {
        if (!spec.TrafficEnabled || own.Activity is RiderActivity.Crashed or RiderActivity.Finished) return null;
        work.TacticalObservations++;
        var now = snapshot.TimeSeconds; var road = own.Road;
        var attack = (spec.Profile.Gameplay.Abilities.Attack - 1) / 98f;
        var defense = (spec.Profile.Gameplay.Abilities.Defense - 1) / 98f;
        var contest = spec.Profile.Gameplay.InteractionStyle.Combativeness;
        var technique = (spec.Profile.Gameplay.Abilities.Technique - 1) / 98f;
        var margin = BaseMarginMeters + CautiousMarginMeters * (1 - contest) + ExecutionMarginMeters * (1 - technique);
        var maximum = frame.MinimumUsableWidthMeters - TargetMarginMeters;
        var reaction = RiderController.ReactionSeconds(spec);
        DecisionEvent Command(RiderView other, TrafficAction action, float target, float drive,
            RacingIntent intent, string reason, bool safety = false)
            => new(own.Id, other.Id, action, now, now, now + reaction, null, target, drive, intent, reason, safety);
        RiderView? opponent = null, pursuer = null; var bestGap = double.PositiveInfinity; var rearGap = double.PositiveInfinity;
        foreach (var pair in pairs)
        {
            var a = snapshot.Riders[pair.A]; var b = snapshot.Riders[pair.B];
            if (a.Id != own.Id && b.Id != own.Id) continue;
            var other = a.Id == own.Id ? b : a;
            if (other.Activity is RiderActivity.Crashed or RiderActivity.Finished) continue;
            var delta = other.Position - own.Position; var relative = other.Velocity - own.Velocity;
            var along = Vector2.Dot(delta, road.Tangent);
            var lateral = Vector2.Dot(delta, road.Outward);
            if (along > 0 && along < bestGap) { opponent = other; bestGap = along; }
            if (along < 0 && -along < rearGap) { pursuer = other; rearGap = -along; }
            var closing = -Vector2.Dot(delta, relative);
            if (closing <= .01f || relative.LengthSquared() < .01f) continue;
            var t = Math.Clamp(closing / relative.LengthSquared(), 0, (float)SafetyHorizonSeconds);
            var at = delta + relative * t;
            var futureAlong = Math.Abs(Vector2.Dot(at, road.Tangent));
            var futureAcross = Math.Abs(Vector2.Dot(at, road.Outward));
            if (futureAlong > Bike.OverallMechanicalLengthMeters + margin
                || futureAcross > HalfWidth(own, road.Heading) + HalfWidth(other, road.Heading) + margin) continue;
            if (along < -Bike.OverallMechanicalLengthMeters / 2)
            {
                // A rider being caught must not brake simply because a faster
                // bike is behind. Stop an unsafe lateral cut; otherwise hold pace.
                if ((own.TargetOffsetMeters - road.OffsetMeters) * lateral > CrossingProjectionSquareMeters)
                    return Command(other, TrafficAction.HoldLine, (float)road.OffsetMeters, own.Drive,
                        memory.Intent, "RearRiderOccupiesCrossing", true);
                continue;
            }
            // Insistence changes a planning margin, never collision response or grip.
            if (contest > InsistenceCombativeness && t > InsistenceHorizonSeconds && Math.Abs(lateral) > Bike.ChassisBodyWidthMeters + margin) continue;
            work.Threats++; memory.ClearSince = 0;
            var target = own.TargetOffsetMeters;
            // Preserve an already opening corridor; otherwise stop voluntary crossing.
            if ((target - road.OffsetMeters) * lateral > 0) target = (float)road.OffsetMeters;
            if (!safetyOnly && own.Drive == 0 && Math.Abs(target - own.TargetOffsetMeters) < TargetChangeMeters) return null;
            var stoppedCrossing = Math.Abs(target - own.TargetOffsetMeters) > TargetChangeMeters
                && memory.Intent is RacingIntent.AttackInside or RacingIntent.AttackOutside;
            return Command(other, TrafficAction.BackOut, target, 0, stoppedCrossing ? RacingIntent.None : memory.Intent,
                stoppedCrossing ? "AbortThreatenedCrossing" : "ImmediateThreat", true);
        }
        if (memory.ClearSince == 0) memory.ClearSince = now;
        if (safetyOnly) return null;

        bool Corridor(float target, double horizon, int intendedOpponent)
        {
            work.TacticalCandidates++;
            if (target < TargetMarginMeters || target > maximum) return false;
            foreach (var pair in pairs)
            {
                var a = snapshot.Riders[pair.A]; var b = snapshot.Riders[pair.B];
                if (a.Id != own.Id && b.Id != own.Id) continue;
                var other = a.Id == own.Id ? b : a; var delta = other.Position - own.Position;
                var longitudinal = Vector2.Dot(delta, road.Tangent);
                var relativeLong = Vector2.Dot(other.Velocity - own.Velocity, road.Tangent);
                var nearLong = Math.Abs(longitudinal) <= Bike.OverallMechanicalLengthMeters + margin
                    || longitudinal * (longitudinal + relativeLong * horizon) <= 0;
                if (!nearLong && Math.Abs(longitudinal + relativeLong * horizon) > Bike.OverallMechanicalLengthMeters + margin) continue;
                var across = (float)road.OffsetMeters + Vector2.Dot(delta, road.Outward);
                var endAcross = across + Vector2.Dot(other.Velocity, road.Outward) * (float)horizon;
                var separation = HalfWidth(own, road.Heading) + HalfWidth(other, road.Heading) + margin;
                // Current alongside occupancy cannot be erased by extrapolating
                // the peer out of the corridor at the end of the horizon.
                if (Math.Abs(longitudinal) <= Bike.OverallMechanicalLengthMeters + margin
                    && (target - road.OffsetMeters) * (across - road.OffsetMeters) > 0
                    && across >= Math.Min((float)road.OffsetMeters, target) - separation
                    && across <= Math.Max((float)road.OffsetMeters, target) + separation) return false;
                if (other.Id == intendedOpponent)
                {
                    if (Math.Abs(target - endAcross) < separation) return false;
                    // A rider already alongside occupies the crossed corridor too.
                    if (Math.Abs(longitudinal) < Bike.OverallMechanicalLengthMeters
                        && (across - road.OffsetMeters) * (across - target) <= 0) return false;
                }
                else if (endAcross >= Math.Min((float)road.OffsetMeters, target) - separation
                    && endAcross <= Math.Max((float)road.OffsetMeters, target) + separation) return false;
            }
            return true;
        }
        if (memory.Intent is RacingIntent.AttackInside or RacingIntent.AttackOutside)
        {
            var previous = snapshot.Riders.FirstOrDefault(v => v.Id == memory.OpponentId);
            var lead = previous.Id == 0 ? double.PositiveInfinity : Vector2.Dot(own.Position - previous.Position, road.Tangent);
            if (lead > Bike.OverallMechanicalLengthMeters + own.Speed * reaction
                && Corridor(memory.HomeOffsetMeters, reaction + RecoveryLookaheadSeconds, 0))
                return Command(previous, TrafficAction.KeepIntent, memory.HomeOffsetMeters, 1, RacingIntent.ReturnLine, "PassClearReturn");
            if ((now - memory.ActivatedAt > AttackTimeoutSeconds && lead < Bike.OverallMechanicalLengthMeters)
                || (now - memory.ActivatedAt > IntentMinimumSeconds && !Corridor(own.TargetOffsetMeters, RecoveryLookaheadSeconds, memory.OpponentId)))
                return Command(previous, TrafficAction.BackOut, (float)road.OffsetMeters, 0, RacingIntent.None, "AttackNoLongerFeasible");
            if (own.Drive == 0 && now >= memory.SafetyUntil && now - memory.ClearSince >= ClearObservationSeconds)
                return Command(previous, TrafficAction.KeepIntent, own.TargetOffsetMeters, 1, memory.Intent, "ResumeIntent");
            return null; // committed side, no 100 ms inside/outside switching
        }
        if (memory.Intent == RacingIntent.ReturnLine)
        {
            if (Math.Abs(road.OffsetMeters - memory.HomeOffsetMeters) < ReturnCompletionMeters)
                return Command(default, TrafficAction.HoldLine, memory.HomeOffsetMeters, 1, RacingIntent.None, "ReturnCompleted");
            if ((own.Drive == 0 || Math.Abs(own.TargetOffsetMeters - memory.HomeOffsetMeters) > TargetChangeMeters) && now >= memory.SafetyUntil
                && now - memory.ClearSince >= ClearObservationSeconds && Corridor(memory.HomeOffsetMeters, reaction + RecoveryLookaheadSeconds, 0))
                return Command(default, TrafficAction.KeepIntent, memory.HomeOffsetMeters, 1, RacingIntent.ReturnLine, "ReturnCorridorClear");
            return null;
        }
        if (now < memory.NextTacticAt || now < memory.SafetyUntil) return null;

        if (opponent is { } front)
        {
            var ownAlong = Vector2.Dot(own.Velocity, road.Tangent);
            var otherAlong = Vector2.Dot(front.Velocity, front.Road.Tangent);
            var closing = ownAlong - otherAlong;
            var encounter = closing > MinimumClosingMetersPerSecond ? bestGap / closing : double.PositiveInfinity;
            if (bestGap > Bike.OverallMechanicalLengthMeters && encounter <= AttackBaseHorizonSeconds + AbilityHorizonSeconds * attack)
            {
                var movement = Math.Max(MinimumMovementSeconds, encounter - reaction);
                var q = movement / p.LateralResponseSeconds;
                var reach = 1 - (1 + q) * Math.Exp(-q);
                var targetCenter = (float)front.Road.OffsetMeters;
                var separation = HalfWidth(own, road.Heading) + HalfWidth(front, road.Heading) + margin;
                var bestScore = double.PositiveInfinity; float? bestTarget = null;
                var bestAction = TrafficAction.AttackInside;
                foreach (var side in new[] {-1, 1})
                {
                    var target = (float)(road.OffsetMeters + (targetCenter + side * separation - road.OffsetMeters) / Math.Max(MinimumReachFraction, reach));
                    if (!Corridor(target, encounter, front.Id)) continue;
                    var width = LaneModel.UsableRacingWidthMeters(frame.Track.Segments[road.Segment].Type, frame.Track.Geometry);
                    var grip = surface.SampleSurface(road.Segment, Math.Clamp(target / width * 4, 0, 4));
                    var radius = frame.Track.Geometry.InnerRadiusMeters + target;
                    var safe = SegmentPhysics.MaxSafeTurnSpeedForRadius(radius, grip, spec.Profile.Skills, spec.Setup);
                    var recovery = Math.Abs(target - road.OffsetMeters) / (p.LateralResponseSeconds * p.LateralResponseSeconds);
                    var speed = road.IsTurn ? Math.Min(ownAlong, Math.Sqrt(Math.Max(0, (safe * safe / radius - recovery) * radius))) : ownAlong;
                    var gain = road.IsTurn ? speed / radius - otherAlong / front.Road.RadiusMeters : speed - otherAlong;
                    if (gain <= 0) continue;
                    var preference = spec.Profile.Gameplay.InteractionStyle.PreferredLine;
                    var score = 1 / gain + LineScoreWeight * Math.Abs(target - memory.HomeOffsetMeters)
                        + (preference == PreferredLine.Inside && side > 0 || preference == PreferredLine.Outside && side < 0 ? LineScoreWeight : 0);
                    if (score < bestScore) { bestScore = score; bestTarget = target;
                        bestAction = side < 0 ? TrafficAction.AttackInside : TrafficAction.AttackOutside; }
                }
                if (bestTarget is { } selected)
                    return Command(front, bestAction, selected, 1,
                        bestAction == TrafficAction.AttackInside ? RacingIntent.AttackInside : RacingIntent.AttackOutside, "ClosingSlowerRider");
                if (own.Drive > 0)
                    return Command(front, TrafficAction.BackOut, (float)road.OffsetMeters, 0, RacingIntent.None, "NoPassingCorridor");
            }
        }
        if (pursuer is { } rear && memory.Intent != RacingIntent.DefendLine)
        {
            var closing = Vector2.Dot(rear.Velocity - own.Velocity, road.Tangent);
            var encounter = closing > MinimumClosingMetersPerSecond ? rearGap / closing : double.PositiveInfinity;
            if (rearGap > 2 * Bike.OverallMechanicalLengthMeters && encounter < DefenseBaseHorizonSeconds + AbilityHorizonSeconds * defense)
            {
                var target = (float)Math.Max(TargetMarginMeters, Math.Min(road.OffsetMeters, rear.Road.OffsetMeters - Bike.HandlebarWidthMeters - margin));
                if (Corridor(target, reaction + IntentMinimumSeconds, 0))
                    return Command(rear, TrafficAction.HoldLine, target, 1, RacingIntent.DefendLine, "CoverInsideEarly");
            }
        }
        if (memory.Intent == RacingIntent.DefendLine && pursuer is null && now - memory.ActivatedAt > 1
            && Corridor(memory.HomeOffsetMeters, reaction + RecoveryLookaheadSeconds, 0))
            return Command(default, TrafficAction.KeepIntent, memory.HomeOffsetMeters, 1, RacingIntent.ReturnLine, "DefenseReleased");
        if (own.Drive == 0 && now - memory.ClearSince >= ClearObservationSeconds && (opponent is null || bestGap > ClearFrontGapMeters)
            && now >= memory.SafetyUntil)
            return Command(default, TrafficAction.KeepIntent, own.TargetOffsetMeters, 1, RacingIntent.None, "ClearResume");
        return null;
    }
}
