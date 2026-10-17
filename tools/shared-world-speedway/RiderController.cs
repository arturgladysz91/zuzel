using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public static class RiderController
{
    public static double ReactionSeconds(RiderSpec spec)
        // PROVISIONAL racing-response proxy from the existing legacy start timing.
        // Canonical #56A Reaction retains its tape-only meaning.
        // Canonicalize this experimental deadline to microseconds; do not create
        // nanosecond Box2D steps from the binary representation of 0.20/0.28f.
        => Math.Round(LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(spec.Profile.Skills), 6);
    public static double TapeReactionSeconds(RiderSpec spec)
        => Math.Round(LongitudinalDynamics.ProvisionalStandingStartSlowReactionSeconds
           + (LongitudinalDynamics.ProvisionalStandingStartFastReactionSeconds
             - LongitudinalDynamics.ProvisionalStandingStartSlowReactionSeconds)
             * (spec.Profile.Gameplay.Abilities.Reaction - 1) / 98d, 6);

    public static (Vector2 Force, float Torque) Forces(RiderView v, RiderSpec spec, TrackFrame frame,
        TrackStateSnapshot surface, WorldParameters p, double time, float mass, float inertia,
        Action<ForceObservation>? observe = null)
    {
        if (spec.Passive)
        { observe?.Invoke(new(time, v, Vector2.Zero, 0, 0, 0, null, 0, "passive")); return (Vector2.Zero, 0); }
        if (spec.SpeedMetersPerSecond == 0 && v.TotalReferenceMeters < frame.Embedding.Segments[0].StraightLengthMeters && time + 1e-12 < TapeReactionSeconds(spec))
        { observe?.Invoke(new(time, v, Vector2.Zero, 0, 0, 0, null, 0, "reaction")); return (Vector2.Zero, 0); }
        var road = v.Road; var skills = spec.Profile.Skills; var setup = spec.Setup;
        var lateral = Math.Clamp((float)road.OffsetMeters / LaneModel.UsableRacingWidthMeters(
            frame.Track.Segments[road.Segment].Type, frame.Track.Geometry) * 4, 0, 4);
        var grip = surface.SampleSurface(road.Segment, lateral);
        var speed = v.Speed;
        // The PD error, damping and actuator use the SAME finite road frame.
        // Normalizing velocity at launch made the first integration impulse
        // rotate propulsion into its own lateral error (and into later impact kicks).
        var tangent = road.Tangent;
        var left = new Vector2(-tangent.Y, tangent.X);
        if (v.Activity == RiderActivity.Crashed)
        {
            var travel = speed > .01f ? v.Velocity / speed : tangent;
            var drag = -travel * Math.Min(mass * speed / .1f, LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed));
            observe?.Invoke(new(time, v, drag, -inertia * v.AngularVelocity, 0, 0, null, 0, "crashed"));
            return (drag, -inertia * v.AngularVelocity);
        }
        var launch = spec.SpeedMetersPerSecond == 0 && v.TotalReferenceMeters < frame.Embedding.Segments[0].StraightLengthMeters && !road.IsTurn;
        var reference = launch ? LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(skills, setup, grip)
            : road.IsTurn ? LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(skills, setup, grip)
            : LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(skills, setup, grip);
        var available = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(reference, Math.Max(0, v.ForwardSpeed), setup);
        var resistance = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed);
        var driveAvailability = road.IsTurn ? ContinuousCornerEnvelope.DriveAvailability((float)road.CornerProgress) : 1;
        // Preserve the old neutral-carry convention; a deliberate lift exposes physical resistance.
        var along = v.Drive > 0 ? driveAvailability * (available * v.Drive * (1 - v.Impairment) - resistance) : -resistance;
        var correction = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(skills, grip);
        var nextCorner = frame.Track.CornerTopology.CornerForSegment(road.Segment);
        double distanceToCorner = 0;
        if (!road.IsTurn)
        {
            var next = (road.Segment + 1) % frame.Track.Segments.Count;
            distanceToCorner = (1 - road.Progress) * frame.Embedding.Segments[road.Segment].StraightLengthMeters;
            if (frame.Track.Segments[next].Type == SegmentType.Straight)
            { distanceToCorner += frame.Embedding.Segments[next].StraightLengthMeters; next = (next + 1) % frame.Track.Segments.Count; }
            nextCorner = frame.Track.CornerTopology.CornerForSegment(next);
        }
        var radius = (float)(frame.Track.Geometry.InnerRadiusMeters + Math.Max(0, road.OffsetMeters));
        var safe = SegmentPhysics.MaxSafeTurnSpeedForRadius(radius, grip, skills, setup);
        if (nextCorner is not null && (!road.IsTurn || road.CornerProgress <= .5))
        {
            // A force-limited 2D body needs centripetal grip on entry too. The old
            // recoverable envelope permits above-settled entry while it scrubs over
            // half a corner; that promise alone cannot supply the missing tyre force.
            // Prepare to the existing settled capability of the REQUESTED radius.
            var target = SegmentPhysics.MaxSafeTurnSpeedForRadius(
                frame.Track.Geometry.InnerRadiusMeters + v.TargetOffsetMeters, grip, skills, setup);
            if (!road.IsTurn) target = LongitudinalDynamics.CalculateMaximumTurnEntryApproachSpeedMetersPerSecond(
                target, correction, (float)distanceToCorner);
            if (speed > target) along = Math.Min(along, -mass * Math.Min(correction, (speed - target) / .1f));
        }
        var skillAuthority = .75f + .25f * (spec.Profile.Gameplay.Abilities.Technique - 1) / 98f;
        var response = p.LateralResponseSeconds;
        var outwardVelocity = Vector2.Dot(v.Velocity, road.Outward);
        var longitudinalSpeed = Vector2.Dot(v.Velocity, tangent);
        var inwardAcceleration = (road.IsTurn ? longitudinalSpeed * longitudinalSpeed / (float)Math.Max(1, road.RadiusMeters) : 0)
            + ((float)road.OffsetMeters - v.TargetOffsetMeters) / (response * response)
            + 2 * outwardVelocity / response;
        var requestedInward = mass * inwardAcceleration;
        var maxLateral = safe * safe / radius;
        if (road.IsTurn && inwardAcceleration > maxLateral)
        {
            // Spend the finite tyre budget on curvature AND recovery. Above the
            // available force, roll off/scrub through the existing capability;
            // increasing the radius alone must not authorize more propulsion.
            var recoveryAcceleration = inwardAcceleration - longitudinalSpeed * longitudinalSpeed / radius;
            var feasibleSpeed = MathF.Sqrt(Math.Max(0, (maxLateral - Math.Max(0, recoveryAcceleration)) * radius));
            along = Math.Min(along, -mass * Math.Min(correction, Math.Max(0, speed - feasibleSpeed) / .1f));
        }
        inwardAcceleration = Math.Clamp(inwardAcceleration, -maxLateral, maxLateral);
        var force = tangent * along + left * (mass * inwardAcceleration * (1 - v.Impairment));
        // A friction circle bounds the combined request, not just one component.
        var budget = mass * maxLateral;
        var scale = force.Length() > budget ? budget / force.Length() : 1;
        force *= scale;
        var desiredHeading = road.Heading + (road.IsTurn ? p.SlideAngleRadians : 0);
        var error = (float)BikeAngles.Wrap(desiredHeading - v.Heading);
        var yawAcceleration = error / (p.YawResponseSeconds * p.YawResponseSeconds) - 2 * v.AngularVelocity / p.YawResponseSeconds;
        var torque = Math.Clamp(inertia * yawAcceleration, -mass * maxLateral / 2, mass * maxLateral / 2);
        observe?.Invoke(new(time, v, force, torque * skillAuthority * (1 - v.Impairment),
            requestedInward, mass * inwardAcceleration * (1 - v.Impairment) * scale, budget, along * scale, "racing"));
        return (force, torque * skillAuthority * (1 - v.Impairment));
    }
}

/// <summary>Short constant-velocity observation only. No opponent target, future path or #54 call.</summary>
public static class TrafficDecision
{
    public static DecisionEvent? Observe(WorldSnapshot snapshot, RiderView own, RiderSpec spec,
        WorldParameters p, WorldWork work, float maximumOffset)
    {
        if (!spec.TrafficEnabled || own.Activity == RiderActivity.Crashed) return null;
        foreach (var other in snapshot.Riders)
        {
            if (other.Id == own.Id) continue;
            work.PairChecks++;
            var delta = other.Position - own.Position; var relative = other.Velocity - own.Velocity;
            if (delta.LengthSquared() > 900 || relative.LengthSquared() < .01f
                || Vector2.Dot(delta, relative) >= -.01f) continue;
            var closest = Math.Clamp(-Vector2.Dot(delta, relative) / relative.LengthSquared(), 0, (float)p.PredictionSeconds);
            var separation = delta + relative * closest;
            var longitudinal = Math.Abs(Vector2.Dot(separation, own.Road.Tangent));
            var lateral = Math.Abs(Vector2.Dot(separation, own.Road.Outward));
            if (longitudinal > 2.3 || lateral > 1.1) continue;
            work.Threats++;
            // Aggressiveness is willingness, not extra grip. A combative weak attacker may insist.
            var gameplay = spec.Profile.Gameplay;
            if (gameplay.InteractionStyle.Combativeness > .85f && gameplay.Abilities.Attack < 45) return null;
            // Yield by opening the actual occupied corridor, otherwise roll off. No brake hardware.
            var target = Math.Min(maximumOffset, Math.Max(own.TargetOffsetMeters, (float)own.Road.OffsetMeters + 1.6f));
            var free = target - own.Road.OffsetMeters > .5;
            foreach (var peer in snapshot.Riders)
                if (peer.Id != own.Id && Math.Abs(Vector2.Dot(peer.Position - own.Position, own.Road.Tangent)) < 4
                    && peer.Road.OffsetMeters > own.Road.OffsetMeters && peer.Road.OffsetMeters < target + 1) free = false;
            var action = free ? TrafficAction.YieldOutward : TrafficAction.BackOut;
            return new(own.Id, other.Id, action, snapshot.TimeSeconds, snapshot.TimeSeconds,
                snapshot.TimeSeconds + RiderController.ReactionSeconds(spec), null,
                free ? target : (float)own.Road.OffsetMeters, 0);
        }
        return null;
    }
}
