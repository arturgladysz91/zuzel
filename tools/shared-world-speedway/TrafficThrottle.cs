using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public readonly record struct TrafficGap(int OpponentId, double GapMeters, double ClosingMetersPerSecond,
    double LateralGapMeters, double GapAfterReactionMeters, double ContactSeconds, bool CorridorOccupied);
public sealed record ThrottleObservation(double TimeSeconds, int RiderId, TrafficGap? Gap, float CurrentDrive,
    float DesiredDrive, double DesiredAcceleration, double ZeroDriveAcceleration, double FullDriveAcceleration,
    bool Emergency, string Reason, string TacticalReason = "");
public readonly record struct TrafficControlPlan(DecisionEvent? Line, DecisionEvent? Throttle, bool CancelLine, bool Emergency);

/// <summary>Current mechanical occupancy and a short own-control estimate. Never steps a body.</summary>
public static class TrafficThrottle
{
    public const double ClearanceMeters = .15;
    public const double ResponseSeconds = 1.2;
    public const float DriveDeadband = .04f, MaximumOrdinaryDriveChange = .25f;
    private const double DesiredStandstillGapMeters = .6, TimeHeadwaySeconds = .1, MaximumObservedAcceleration = 5;
    private const float PositiveDriveMinimum = .0001f;
    private const int InverseIterations = 4;
    private static readonly double BikeMassKg = new CoreSim.Interactions.PhysicalContactParameters().ReferenceBikeMassKg;

    /// <summary>Preferred spacing only; emergency feasibility is evaluated independently below.</summary>
    public static double PreferredGap(double speed, double closing, double reaction, double zeroAcceleration, WorldParameters p)
    {
        if (p.DiagnosticFollowingGap is { } diagnostic) return diagnostic.BaseMeters + diagnostic.HeadwaySeconds * speed;
        if (!p.EnableClosureAdaptiveFollowingGap) return DesiredStandstillGapMeters + TimeHeadwaySeconds * speed;
        var approach = Math.Max(0, closing);
        return DesiredStandstillGapMeters + approach * reaction + approach * approach / (2 * Math.Max(.1, -zeroAcceleration));
    }

    private static (double Rear, double Front) Extent(RiderView v, Vector2 axis)
    {
        var bike = BikeFootprint.Create(new(v.Position.X, v.Position.Y), v.Heading, SpeedwayBikeDimensions.Reference);
        var direction = new MeterPoint(axis.X, axis.Y);
        double rear = double.PositiveInfinity, front = double.NegativeInfinity;
        void Include(MechanicalCapsule c) {
            var a = MeterPoint.Dot(c.Start, direction); var b = MeterPoint.Dot(c.End, direction);
            rear = Math.Min(rear, Math.Min(a, b) - c.RadiusMeters);
            front = Math.Max(front, Math.Max(a, b) + c.RadiusMeters);
        }
        Include(bike.Chassis); Include(bike.Handlebar); return (rear, front);
    }
    public static TrafficGap Measure(RiderView own, RiderView other, float target, double reaction, WorldParameters p)
    {
        var combined = own.Road.Tangent + other.Road.Tangent;
        var axis = combined.LengthSquared() > 1e-6 ? Vector2.Normalize(combined) : own.Road.Tangent;
        var across = new Vector2(axis.Y, -axis.X);
        var a = Extent(own, axis); var b = Extent(other, axis);
        var lateralA = Extent(own, across); var lateralB = Extent(other, across);
        var lateralGap = Math.Max(lateralB.Rear - lateralA.Front, lateralA.Rear - lateralB.Front);
        var closing = Vector2.Dot(own.Velocity - other.Velocity, axis);
        var gap = b.Rear - a.Front;
        var horizon = reaction + .6;
        var q = horizon / p.LateralResponseSeconds; var decay = Math.Exp(-q);
        var outward = Vector2.Dot(own.Velocity, own.Road.Outward);
        var ownMovement = (target - own.Road.OffsetMeters) * (1 - (1 + q) * decay) + outward * horizon * decay;
        var peerMovement = Vector2.Dot(other.Velocity, other.Road.Outward) * horizon;
        var centerDifference = Vector2.Dot(other.Position - own.Position, across);
        var predictedDifference = centerDifference + peerMovement - ownMovement;
        var halfWidths = ((lateralA.Front - lateralA.Rear) + (lateralB.Front - lateralB.Rear)) / 2;
        var occupied = lateralGap <= ClearanceMeters || Math.Abs(predictedDifference) <= halfWidths + ClearanceMeters
            || centerDifference * predictedDifference < 0;
        return new(other.Id, gap, closing, lateralGap, gap - Math.Max(0, closing) * reaction,
            closing > .01 ? Math.Max(0, gap / closing) : double.PositiveInfinity, occupied);
    }

    public static TrafficGap? Front(WorldSnapshot snapshot, RiderView own, float target, double reaction, WorldParameters p)
    {
        TrafficGap? best = null;
        foreach (var other in snapshot.Riders) {
            if (other.Id == own.Id || other.Activity is RiderActivity.Crashed or RiderActivity.Finished
                || other.TotalReferenceMeters <= own.TotalReferenceMeters
                || other.TotalReferenceMeters - own.TotalReferenceMeters > 30
                || Vector2.DistanceSquared(own.Position, other.Position) > 900
                || Vector2.Dot(own.Road.Tangent, other.Road.Tangent) < .85f) continue;
            var gap = Measure(own, other, target, reaction, p);
            if (gap.CorridorOccupied && (best is null || gap.GapAfterReactionMeters < best.Value.GapAfterReactionMeters)) best = gap;
        }
        return best;
    }

    public static bool CrossesOccupied(WorldSnapshot snapshot, RiderView own, float target, double reaction, WorldParameters p)
    {
        foreach (var peer in snapshot.Riders) {
            if (peer.Id == own.Id || peer.Activity is RiderActivity.Crashed or RiderActivity.Finished
                || Math.Abs(peer.TotalReferenceMeters - own.TotalReferenceMeters) > SpeedwayBikeDimensions.Reference.OverallMechanicalLengthMeters + own.Speed * reaction
                || Vector2.DistanceSquared(own.Position, peer.Position) > 225
                || Vector2.Dot(own.Road.Tangent, peer.Road.Tangent) < .85) continue;
            if ((target - own.Road.OffsetMeters) * (peer.Road.OffsetMeters - own.Road.OffsetMeters) <= .05) continue;
            if (Measure(own, peer, target, reaction, p).CorridorOccupied) return true;
        }
        return false;
    }

    public static ThrottleObservation Evaluate(WorldSnapshot snapshot, RiderView own, RiderSpec spec,
        WorldParameters p, TrackFrame frame, TrackStateSnapshot surface, TacticalMemory memory, WorldWork work)
    {
        work.ThrottleObservations++;
        var gap = Front(snapshot, own, own.TargetOffsetMeters, RiderController.ReactionSeconds(spec), p);
        if (gap is null) {
            memory.FollowingOpponentId = 0;
            return new(snapshot.TimeSeconds, own.Id, null, own.Drive,
                Math.Min(1, own.Drive + MaximumOrdinaryDriveChange), 0, 0, 0, false, "ResumeAcceleration");
        }
        var g = gap.Value; var peer = snapshot.Riders.Single(v => v.Id == g.OpponentId);
        var mass = (float)(spec.Profile.Gameplay.Physical.MassKg + BikeMassKg);
        double Acceleration(float drive) {
            work.ThrottleForceEvaluations++;
            var force = RiderController.Forces(own with {Drive = drive}, spec, frame, surface, p, snapshot.TimeSeconds, mass, 1).Force;
            return Vector2.Dot(force, own.Road.Tangent) / mass;
        }
        // This is a bounded scalar inverse of the existing instantaneous force
        // function, NOT another integrator, future path, or mutable Box2D world.
        var zero = Acceleration(0); var full = Acceleration(1);
        var elapsed = snapshot.TimeSeconds - memory.ThrottleObservedAt;
        var hasHistory = memory.FollowingOpponentId == peer.Id && elapsed > 1e-6 && elapsed <= .2;
        var feedForward = hasHistory ? Math.Clamp((peer.Speed - memory.ObservedOpponentSpeed) / elapsed,
            -MaximumObservedAcceleration, MaximumObservedAcceleration) : 0;
        memory.FollowingOpponentId = peer.Id; memory.ThrottleObservedAt = snapshot.TimeSeconds; memory.ObservedOpponentSpeed = peer.Speed;
        var desiredGap = PreferredGap(own.Speed, g.ClosingMetersPerSecond, RiderController.ReactionSeconds(spec), zero, p);
        var desired = feedForward + (g.GapAfterReactionMeters - desiredGap) / (ResponseSeconds * ResponseSeconds)
            - 2 * g.ClosingMetersPerSecond / ResponseSeconds;
        var closing = Math.Max(0, g.ClosingMetersPerSecond);
        var availableCoastDeceleration = Math.Max(.1, -zero);
        var emergency = closing > .25 && g.GapAfterReactionMeters
            <= ClearanceMeters + closing * closing / (2 * availableCoastDeceleration);
        float drive;
        if (emergency) drive = 0;
        else if (!hasHistory && Math.Abs(g.ClosingMetersPerSecond) < .1 && g.GapMeters > desiredGap * .7) drive = own.Drive;
        else if (desired >= full) drive = 1;
        else {
            var lowAcceleration = Acceleration(PositiveDriveMinimum);
            if (desired <= Math.Min(zero, lowAcceleration)) drive = zero <= lowAcceleration ? 0 : PositiveDriveMinimum;
            else if (desired < lowAcceleration) drive = zero < lowAcceleration ? 0 : 1;
            else if (full - lowAcceleration < 1e-5) drive = own.Drive; // geometry/carry owns the force; no throttle authority
            else {
                float lo = PositiveDriveMinimum, hi = 1; var aLo = lowAcceleration; var aHi = full;
                for (var n = 0; n < InverseIterations; n++) {
                    var mid = (lo + hi) / 2; var acceleration = Acceleration(mid);
                    if (acceleration < desired) {lo = mid; aLo = acceleration;} else {hi = mid; aHi = acceleration;}
                }
                drive = (float)(lo + (hi - lo) * Math.Clamp((desired - aLo) / Math.Max(1e-9, aHi - aLo), 0, 1));
            }
            drive = Math.Clamp(drive, 0, 1);
        }
        if (!emergency) drive = Math.Clamp(drive, Math.Max(0, own.Drive - MaximumOrdinaryDriveChange), Math.Min(1, own.Drive + MaximumOrdinaryDriveChange));
        if (!float.IsFinite(drive)) throw new InvalidOperationException("Non-finite throttle estimate.");
        return new(snapshot.TimeSeconds, own.Id, gap, own.Drive, drive, desired, zero, full, emergency,
            emergency ? "ImmediateThreat" : drive > own.Drive ? "ResumeAcceleration" : "Following");
    }
}

public static partial class TrafficDecision
{
    public static TrafficControlPlan ObserveThrottle(WorldSnapshot snapshot, RiderView own, RiderSpec spec,
        WorldParameters p, WorldWork work, TacticalMemory memory, IReadOnlyList<RacingPair> pairs,
        TrackFrame frame, TrackStateSnapshot surface, DecisionEvent? waitingLine,
        Action<ContestObservation>? contestObserver = null, Action<ThrottleObservation>? throttleObserver = null,
        Action<AutonomousLineObservation>? lineObserver = null)
    {
        if (!spec.TrafficEnabled || own.Activity is RiderActivity.Crashed or RiderActivity.Finished) return default;
        var reaction = RiderController.ReactionSeconds(spec);
        var unsafeCrossing = TrafficThrottle.CrossesOccupied(snapshot, own, own.TargetOffsetMeters, reaction, p);
        var cancel = waitingLine is {SafetyReaction:false} && TrafficThrottle.CrossesOccupied(snapshot, own, waitingLine.TargetOffsetMeters, reaction, p);
        var evaluated = unsafeCrossing ? own with {TargetOffsetMeters = (float)own.Road.OffsetMeters} : own;
        var evaluation = TrafficThrottle.Evaluate(snapshot, evaluated, spec, p, frame, surface, memory, work);
        DecisionEvent? line = null;
        if (unsafeCrossing || cancel)
            line = new(own.Id, evaluation.Gap?.OpponentId ?? 0, TrafficAction.HoldLine, snapshot.TimeSeconds, snapshot.TimeSeconds,
                snapshot.TimeSeconds + reaction, null, (float)own.Road.OffsetMeters, own.Drive,
                RacingIntent.None, "CorridorOccupied", true, Channels: ControlChannels.Line);
        if (evaluation.Emergency) {
            work.TacticalObservations++;
            cancel |= waitingLine is {SafetyReaction:false};
            if (Math.Abs(own.TargetOffsetMeters - own.Road.OffsetMeters) > TargetChangeMeters)
                line = new(own.Id, evaluation.Gap!.Value.OpponentId, TrafficAction.HoldLine, snapshot.TimeSeconds, snapshot.TimeSeconds,
                    snapshot.TimeSeconds + RiderController.ReactionSeconds(spec), null, (float)own.Road.OffsetMeters, own.Drive,
                    RacingIntent.None, "AbortThreatenedCrossing", true, Channels: ControlChannels.Line);
        } else if (line is null) {
            line = Observe(snapshot, own, spec, p, work, memory, pairs, frame, surface, waitingLine is not null, contestObserver, lineObserver);
        }
        var reason = line?.Reason ?? (evaluation.Gap is null ? "FreeRacing" : evaluation.Reason);
        if (line?.Reason is "NoAttackGain" or "AttackCorridorUnavailable" or "AttackUnreachable"
            or "ResumeIntent" or "ResumeContest" or "ClearResume") line = null;
        else if (line is not null) line = line with {Channels = ControlChannels.Line};
        throttleObserver?.Invoke(evaluation with {TacticalReason = reason});
        DecisionEvent? throttle = null;
        if (Math.Abs(evaluation.DesiredDrive - own.Drive) >= TrafficThrottle.DriveDeadband
            || evaluation.DesiredDrive is 0 or 1 && evaluation.DesiredDrive != own.Drive)
            throttle = new(own.Id, evaluation.Gap?.OpponentId ?? 0, TrafficAction.KeepIntent, snapshot.TimeSeconds, snapshot.TimeSeconds,
                snapshot.TimeSeconds + RiderController.ReactionSeconds(spec), null, own.TargetOffsetMeters, evaluation.DesiredDrive,
                memory.Intent, evaluation.Reason, evaluation.Emergency, Channels: ControlChannels.Throttle);
        return new(line, throttle, cancel, evaluation.Emergency);
    }
}
