using System.Numerics;
using CoreSim;

namespace SharedWorldSpeedway;

/// <summary>Own-control estimates only. No world execution or opponent command access.</summary>
public static class ContestScoring
{
    public const int QuadratureIntervals = 6;
    private const double MinimumWindowSeconds = .1;
    private static readonly double BikeMassKg = new CoreSim.Interactions.PhysicalContactParameters().ReferenceBikeMassKg;

    public static double HorizonSeconds(RiderView own, RiderSpec spec, WorldParameters p, TrackFrame frame, bool defending)
        => Math.Clamp(frame.DistanceToCurvatureChange(own.Road) / Math.Max(1, own.Speed)
            - RiderController.ReactionSeconds(spec), MinimumWindowSeconds, MovementSeconds(spec, p, defending));

    /// <summary>Six trapezoids of a bounded speed envelope, not an alternate bike trajectory.
    /// Analytical PD supplies offset/demand. Earlier capability dips constrain later pace;
    /// recovery is limited by existing propulsion. No position/velocity state is integrated.</summary>
    public static double Progress(RiderView own, RiderSpec spec, WorldParameters p,
        TrackFrame frame, TrackStateSnapshot surface, float target, double seconds, WorldWork? work = null)
    {
        if (seconds <= 0) return 0;
        var tau = (double)p.LateralResponseSeconds;
        var reaction = RiderController.ReactionSeconds(spec);
        var outward = Vector2.Dot(own.Velocity, own.Road.Outward);
        var delay = reaction / tau; var damping = Math.Exp(-delay);
        var error = own.Road.OffsetMeters - own.TargetOffsetMeters;
        var offset0 = own.TargetOffsetMeters + (error * (1 + delay) + outward * reaction) * damping;
        var outward0 = (outward * (1 - delay) - error * reaction / (p.LateralResponseSeconds * p.LateralResponseSeconds)) * damping;
        var speed0 = Math.Max(0, Vector2.Dot(own.Velocity, own.Road.Tangent));
        var referenceRate = Math.Max(0, frame.ReferenceSpeed(own));
        var mass = spec.Profile.Gameplay.Physical.MassKg + BikeMassKg;
        var activationRoad = frame.At(own.Road.ReferenceMeters + referenceRate * reaction, offset0).Road;
        var activationRadius = frame.Track.Geometry.InnerRadiusMeters + Math.Max(0, offset0);
        var activationWidth = LaneModel.UsableRacingWidthMeters(frame.Track.Segments[activationRoad.Segment].Type, frame.Track.Geometry);
        var activationGrip = surface.SampleSurface(activationRoad.Segment, Math.Clamp((float)offset0 / activationWidth * 4, 0, 4));
        var activationSafe = SegmentPhysics.MaxSafeTurnSpeedForRadius((float)activationRadius, activationGrip, spec.Profile.Skills, spec.Setup);
        var activationDemand = (offset0 - target) / (tau * tau) + 2 * outward0 / tau;
        var activationCap = Math.Sqrt(Math.Max(0, activationSafe * activationSafe - Math.Max(0, activationDemand) * activationRadius));
        var correctionAtActivation = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(spec.Profile.Skills, activationGrip);
        // Ideal PD assumes its requested lateral acceleration can be delivered.
        // If curvature already uses that reserve, first allow the existing finite
        // scrub capability to release it. This is a delay estimate, not a new actuator.
        var scrubSeconds = activationRoad.IsTurn ? Math.Max(0, speed0 - activationCap) / correctionAtActivation : 0;
        if (work is not null) work.QualitySamples++;
        Span<double> limits = stackalloc double[QuadratureIntervals + 1];
        var dt = seconds / QuadratureIntervals; double progress = 0, previousRate = 0;
        for (var i = 0; i <= QuadratureIntervals; i++)
        {
            var time = i * dt; var responseTime = Math.Max(0, time - scrubSeconds);
            var q = responseTime / tau; var decay = Math.Exp(-q);
            var offset = target + ((offset0 - target) * (1 + q) + outward0 * responseTime) * decay;
            var lateralVelocity = (outward0 * (1 - q) - (offset0 - target) * responseTime / (tau * tau)) * decay;
            var voluntaryInward = (offset - target) / (tau * tau) + 2 * lateralVelocity / tau;
            // Segment look-up is only the current constant reference-rate projection.
            // It knows track geometry, not future motion or another rider's commands.
            var road = frame.At(own.Road.ReferenceMeters + referenceRate * (reaction + time), offset).Road;
            var radius = frame.Track.Geometry.InnerRadiusMeters + Math.Max(0, offset);
            var width = LaneModel.UsableRacingWidthMeters(frame.Track.Segments[road.Segment].Type, frame.Track.Geometry);
            var grip = surface.SampleSurface(road.Segment, Math.Clamp((float)offset / width * 4, 0, 4));
            var safe = SegmentPhysics.MaxSafeTurnSpeedForRadius((float)radius, grip, spec.Profile.Skills, spec.Setup);
            var budget = safe * safe / radius;
            var curvature = road.IsTurn ? speed0 * speed0 / radius : 0;
            var demand = curvature + voluntaryInward;
            var fraction = Math.Min(1, Math.Abs(demand) / budget);
            var longitudinalFraction = Math.Sqrt(Math.Max(0, 1 - fraction * fraction));
            var referenceForce = road.IsTurn
                ? LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(spec.Profile.Skills, spec.Setup, grip)
                : LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(spec.Profile.Skills, spec.Setup, grip);
            var available = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(referenceForce, (float)speed0, spec.Setup);
            var resistance = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons((float)speed0);
            var driveFraction = road.IsTurn ? ContinuousCornerEnvelope.DriveAvailability((float)road.CornerProgress) : 1;
            var acceleration = (available * own.Drive * (1 - own.Impairment) - resistance) / mass * driveFraction * longitudinalFraction;
            var correction = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(spec.Profile.Skills, grip);
            var cap = road.IsTurn ? Math.Sqrt(Math.Max(0, (budget - Math.Max(0, voluntaryInward)) * radius)) : double.PositiveInfinity;
            // Cone from each earlier sample: finite scrub into its cap and finite
            // subsequent recovery. Taking a minimum retains the transient loss.
            limits[i] = Math.Max(speed0 - correction * time, Math.Min(speed0 + acceleration * time, cap));
            var speed = speed0 + acceleration * time;
            for (var j = 0; j <= i; j++) speed = Math.Min(speed, limits[j] + Math.Max(0, acceleration) * (time - j * dt));
            var rate = frame.ReferenceSpeed(road, Math.Max(0, speed), offset);
            if (i > 0) progress += (previousRate + rate) * .5 * dt;
            previousRate = rate;
            if (work is not null) work.QualitySamples++;
        }
        return progress;
    }

    public static double MovementSeconds(RiderSpec spec, WorldParameters p, bool defending)
    {
        var quality = ((defending ? spec.Profile.Gameplay.Abilities.Defense : spec.Profile.Gameplay.Abilities.Attack) - 1) / 98d;
        return 3 * p.LateralResponseSeconds * (.8 + .2 * quality);
    }

    // Exact #90 formula, extracted before freezing the diagnostic set.
    public static double LegacyRate(RiderView own, RiderSpec spec, WorldParameters p,
        TrackFrame frame, TrackStateSnapshot surface, float target, double movement)
    {
        var road = own.Road;
        var along = Vector2.Dot(own.Velocity, road.Tangent);
        var outward = Vector2.Dot(own.Velocity, road.Outward);
        var reaction = RiderController.ReactionSeconds(spec);
        var q = movement / p.LateralResponseSeconds;
        var delay = reaction / p.LateralResponseSeconds; var damping = Math.Exp(-delay);
        var error = road.OffsetMeters - own.TargetOffsetMeters;
        var atActivation = own.TargetOffsetMeters + (error * (1 + delay) + outward * reaction) * damping;
        var outwardAtActivation = (outward * (1 - delay) - error * reaction
            / (p.LateralResponseSeconds * p.LateralResponseSeconds)) * damping;
        var offset = target + ((atActivation - target) * (1 + q) + outwardAtActivation * movement) * Math.Exp(-q);
        var radius = frame.Track.Geometry.InnerRadiusMeters + offset;
        var width = LaneModel.UsableRacingWidthMeters(frame.Track.Segments[road.Segment].Type, frame.Track.Geometry);
        var grip = surface.SampleSurface(road.Segment, Math.Clamp((float)offset / width * 4, 0, 4));
        var safe = SegmentPhysics.MaxSafeTurnSpeedForRadius((float)radius, grip, spec.Profile.Skills, spec.Setup);
        var outwardAtEnd = (outwardAtActivation * (1 - q) - (atActivation - target) * movement
            / (p.LateralResponseSeconds * p.LateralResponseSeconds)) * Math.Exp(-q);
        var recovery = (outwardAtActivation - outwardAtEnd) / movement;
        var possible = Math.Min(along, Math.Sqrt(Math.Max(0, (safe * safe / radius - Math.Max(0, recovery)) * radius)));
        return frame.ReferenceSpeed(road, possible, offset);
    }
}
