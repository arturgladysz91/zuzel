using System.Numerics;
using CoreSim;

namespace SharedWorldSpeedway;

public static partial class TrafficDecision
{
    private const double ContestRadiusMeters = 8, ContestLineStepMeters = 1;
    private const double ContestMinimumSpeedMetersPerSecond = 8;
    private const double ContestRateDifferenceMetersPerSecond = .15;
    private const double ContestBaseGainMetersPerSecond = .12, ContestQualityMarginMetersPerSecond = .15;
    private const double ContestResponseTimeConstants = 3;
    private const double ContestMinimumHorizonFraction = .8, ContestQualityHorizonFraction = .2;

    private static DecisionEvent? ObserveContest(WorldSnapshot snapshot, RiderView own, RiderSpec spec,
        WorldParameters p, WorldWork work, TacticalMemory memory, IReadOnlyList<RacingPair> pairs,
        TrackFrame frame, TrackStateSnapshot surface, Func<float, double, int, bool> corridor,
        Action<ContestObservation>? observe)
    {
        var active = memory.Intent is RacingIntent.ContestInside or RacingIntent.ContestOutside or RacingIntent.ContestHold;
        var attack = (spec.Profile.Gameplay.Abilities.Attack - 1) / 98d;
        var reaction = RiderController.ReactionSeconds(spec);
        var horizon = reaction + ContestResponseTimeConstants * p.LateralResponseSeconds
            * (ContestMinimumHorizonFraction + ContestQualityHorizonFraction * attack);
        DecisionEvent Command(RiderView peer, TrafficAction action, float target, float drive, RacingIntent intent, string reason)
            => new(own.Id, peer.Id, action, snapshot.TimeSeconds, snapshot.TimeSeconds,
                snapshot.TimeSeconds + reaction, null, target, drive, intent, reason);
        if (active)
        {
            var peer = snapshot.Riders.FirstOrDefault(v => v.Id == memory.OpponentId);
            var hold = memory.Intent == RacingIntent.ContestHold;
            var clearDistance = Bike.OverallMechanicalLengthMeters + (hold ? 0 : Math.Abs(frame.ReferenceSpeed(own)) * reaction);
            if (peer.Id == 0 || Math.Abs(own.TotalReferenceMeters - peer.TotalReferenceMeters)
                > clearDistance)
                return Command(peer, TrafficAction.KeepIntent, own.TargetOffsetMeters, own.Drive, RacingIntent.None, "ContestResolved");
            if (snapshot.TimeSeconds < memory.NextTacticAt) return null;
            if (memory.Intent != RacingIntent.ContestHold && !corridor(own.TargetOffsetMeters, horizon, peer.Id))
                return Command(peer, TrafficAction.BackOut, (float)own.Road.OffsetMeters, 0, RacingIntent.None, "ContestCorridorOccupied");
            if (own.Drive == 0 && snapshot.TimeSeconds >= memory.SafetyUntil
                && snapshot.TimeSeconds - memory.ClearSince >= ClearObservationSeconds)
                return Command(peer, TrafficAction.KeepIntent, own.TargetOffsetMeters, 1, memory.Intent, "ResumeContest");
            if (!hold) return null; // committed side; Hold may reassess after hysteresis
            memory.NextTacticAt = snapshot.TimeSeconds + IntentMinimumSeconds;
        }
        if ((!active && snapshot.TimeSeconds < memory.NextTacticAt) || snapshot.TimeSeconds < memory.SafetyUntil
            || own.Speed < ContestMinimumSpeedMetersPerSecond || !own.Road.IsTurn) return null;
        var opponent = default(RiderView); var nearest = double.PositiveInfinity;
        foreach (var pair in pairs)
        {
            var a = snapshot.Riders[pair.A]; var b = snapshot.Riders[pair.B];
            if (a.Id != own.Id && b.Id != own.Id) continue;
            var peer = a.Id == own.Id ? b : a;
            if (peer.Activity is RiderActivity.Crashed or RiderActivity.Finished) continue;
            var distance = Vector2.DistanceSquared(own.Position, peer.Position);
            if (distance > ContestRadiusMeters * ContestRadiusMeters
                || Math.Abs(peer.TotalReferenceMeters - own.TotalReferenceMeters) > Bike.OverallMechanicalLengthMeters) continue;
            if (distance < nearest) {nearest = distance; opponent = peer;}
        }
        if (opponent.Id == 0) return null;
        work.SideObservations++;
        var road = own.Road;
        var along = Vector2.Dot(own.Velocity, road.Tangent);
        var outward = Vector2.Dot(own.Velocity, road.Outward);
        var defending = own.TotalReferenceMeters >= opponent.TotalReferenceMeters;
        var quality = ((defending ? spec.Profile.Gameplay.Abilities.Defense : spec.Profile.Gameplay.Abilities.Attack) - 1) / 98d;
        horizon = reaction + ContestResponseTimeConstants * p.LateralResponseSeconds
            * (ContestMinimumHorizonFraction + ContestQualityHorizonFraction * quality);
        var movement = horizon - reaction;
        var q = movement / p.LateralResponseSeconds;
        // Own accepted command remains in force throughout reaction. Estimate
        // that delay before comparing the three bounded PD responses.
        var delay = reaction / p.LateralResponseSeconds; var damping = Math.Exp(-delay);
        var error = road.OffsetMeters - own.TargetOffsetMeters;
        var atActivation = own.TargetOffsetMeters + (error * (1 + delay) + outward * reaction) * damping;
        var outwardAtActivation = (outward * (1 - delay) - error * reaction
            / (p.LateralResponseSeconds * p.LateralResponseSeconds)) * damping;
        double Rate(float target)
        {
            var offset = target + ((atActivation - target) * (1 + q) + outwardAtActivation * movement) * Math.Exp(-q);
            var radius = frame.Track.Geometry.InnerRadiusMeters + offset;
            var width = LaneModel.UsableRacingWidthMeters(frame.Track.Segments[road.Segment].Type, frame.Track.Geometry);
            var grip = surface.SampleSurface(road.Segment, Math.Clamp((float)offset / width * 4, 0, 4));
            var safe = SegmentPhysics.MaxSafeTurnSpeedForRadius((float)radius, grip, spec.Profile.Skills, spec.Setup);
            var outwardAtEnd = (outwardAtActivation * (1 - q) - (atActivation - target) * movement
                / (p.LateralResponseSeconds * p.LateralResponseSeconds)) * Math.Exp(-q);
            // Mean voluntary acceleration over the bounded response, not an
            // instantaneous jump to the peak cap held for the entire horizon.
            var recovery = (outwardAtActivation - outwardAtEnd) / movement;
            var possible = Math.Min(along, Math.Sqrt(Math.Max(0, (safe * safe / radius - Math.Max(0, recovery)) * radius)));
            return frame.ReferenceSpeed(road, possible, offset);
        }
        var baseline = Rate(own.TargetOffsetMeters); var best = baseline;
        var targetLine = own.TargetOffsetMeters; var gainNeeded = ContestBaseGainMetersPerSecond + ContestQualityMarginMetersPerSecond * (1 - quality);
        var alternative = false;
        foreach (var direction in new[] {0, -1, 1})
        {
            var target = (float)(road.OffsetMeters + direction * ContestLineStepMeters);
            if (Math.Abs(target - own.TargetOffsetMeters) < TargetChangeMeters) continue;
            work.SideCandidates++;
            if (!corridor(target, horizon, opponent.Id)) {
                work.SideBlockedCandidates++;
                observe?.Invoke(new(snapshot.TimeSeconds, own.Id, opponent.Id, target, false, baseline, null,
                    frame.ReferenceSpeed(own), frame.ReferenceSpeed(opponent), gainNeeded)); continue;
            }
            alternative = true;
            var rate = Rate(target);
            observe?.Invoke(new(snapshot.TimeSeconds, own.Id, opponent.Id, target, true, baseline, rate,
                frame.ReferenceSpeed(own), frame.ReferenceSpeed(opponent), gainNeeded));
            if (rate > baseline + gainNeeded && rate > best) {best = rate; targetLine = target;}
        }
        if (targetLine != own.TargetOffsetMeters)
        {
            if (Math.Abs(targetLine - road.OffsetMeters) < TargetChangeMeters)
                return Command(opponent, TrafficAction.HoldLine, targetLine, own.Drive, RacingIntent.ContestHold, "ContestHoldPosition");
            var inside = targetLine < road.OffsetMeters;
            return Command(opponent, inside ? TrafficAction.AttackInside : TrafficAction.AttackOutside, targetLine, 1,
                inside ? RacingIntent.ContestInside : RacingIntent.ContestOutside, inside ? "ContestInside" : "ContestOutside");
        }
        if (!active && alternative && Math.Abs(frame.ReferenceSpeed(own) - frame.ReferenceSpeed(opponent)) >= ContestRateDifferenceMetersPerSecond)
        {
            work.SideHoldChoices++;
            return Command(opponent, TrafficAction.HoldLine, own.TargetOffsetMeters, own.Drive, RacingIntent.ContestHold, "ContestHoldAdvantage");
        }
        return null;
    }
}
