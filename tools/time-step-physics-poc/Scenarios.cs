using CoreSim;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using RaceReadiness;

namespace TimeStepPhysics;

internal static class Scenarios
{
    internal static readonly string[] Names = { "straight", "corner", "side", "encroach", "rear", "near-miss",
        "light-straight", "bend-contact", "strong-no-room", "real-R3-R4" };
    internal enum Context { START_STRAIGHT, FIRST_BEND, NORMAL_RACING }
    internal static Context ContextFor(string name) => name == "light-straight" ? Context.START_STRAIGHT
        : name == "bend-contact" ? Context.FIRST_BEND : Context.NORMAL_RACING;
    internal sealed record RealOrigin(FourRiderCausalCoordinator Coordinator, FrozenHeatContext Heat, CausalPublication First);
    internal static RealOrigin CaptureReal()
    {
        var (track, surface, riders) = Catalog.All().Single(s => s.Id == "control").Create();
        var options = new HeatSimulationOptions { Seed = 19, EnableLogging = false,
            EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = true };
        var heat = new FrozenHeatContext(track, surface, options, 91);
        var first = heat.Capture(riders, new(91, 0, 0, 0, 19, options.Laps));
        var coordinator = new FourRiderCausalCoordinator(new(new AdaptiveDecisionModel()), heat, first);
        var result = coordinator.Advance();
        if (result.Status != CausalRunStatus.ContactResolved || (result.Publication.Contact!.RiderA, result.Publication.Contact.RiderB) != (3, 4))
            throw new InvalidOperationException(result.Refusal ?? "Real #81 origin changed.");
        return new(coordinator, heat, result.Publication);
    }
    internal static TimeStepRace Import(RealOrigin source, bool reverse = false)
    {
        var map = new TrackMetricEmbedding(source.Heat.Track, true);
        var time = source.First.Riders.Max(r => r.ExecutedThroughSeconds);
        var bikes = source.First.Riders.Select(s =>
        {
            var boundary = s.Execution!; var a = boundary.ExecutionEndpoints![^2]; var b = boundary.ExecutionEndpoints[^1];
            var start = boundary with { CanonicalProgress = a.CanonicalProgress, TimeSeconds = a.TimeSeconds,
                DistanceMeters = a.DistanceMeters, Speed = a.Speed, LateralPosition = a.LateralPosition };
            var span = ResolvedBikePoses.FromExecutedStep(s.Origin!, start, b, map, true).Last();
            var pose = span.Sample(s.ExecutedThroughSeconds);
            var previous = span.Sample(span.StartTimeSeconds);
            var omega = BikeAngles.Wrap(pose.Attitude.BikeHeadingRadians - previous.Attitude.BikeHeadingRadians) / (span.EndTimeSeconds - span.StartTimeSeconds);
            var target = s.Integration!.HoldLateralPosition ? s.Rider.LateralPosition
                : s.Integration.InteractionTargetPosition ?? s.Intent!.Decision.TargetLane;
            var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(target, s.Origin!.Segment.Type, source.Heat.Track.Geometry);
            var remaining = (boundary.EndCanonicalProgress - boundary.CanonicalProgress)
                * LaneModel.SegmentLengthMeters(s.Origin.Segment, s.Rider.LateralPosition, source.Heat.Track.Geometry) / Math.Max(1, s.Rider.Speed);
            return new DynamicBike(s.Rider, pose.Position, TimeStepRace.Direction(pose.Attitude.TravelHeadingRadians) * s.Rider.Speed,
                pose.Attitude.BikeHeadingRadians, omega, new(offset, s.Integration!.PositiveDriveFraction),
                s.Rider.ContactRecovery?.ControlLoss01 ?? 0, time + remaining, s.Rider.Status, s.Rider.DistanceMeters);
        }).ToArray();
        return new(source.Heat.Track, source.Heat.Surface, reverse ? bikes.Reverse() : bikes, time, reverse, consumed: (3, 4));
    }
    internal static TimeStepRace Create(string name, RealOrigin? real = null, bool reverse = false)
    {
        if (name == "real-R3-R4") return Import(real ?? CaptureReal(), reverse);
        if (name == "bend-contact") return BendContact(reverse);
        var track = name == "corner" ? Track.CreateStandingStartExample()
            : new Track(new[] { new TrackSegment(0, SegmentType.Straight, 1000), new TrackSegment(1, SegmentType.Straight, 1000) }, TrackGeometry.Default);
        var map = new TrackMetricEmbedding(track, true); var surface = TrackState.CreateDefault(track).Snapshot();
        var bikes = Enumerable.Range(1, 4).Select(id =>
        {
            var segment = name == "corner" ? 1 : 0;
            var offset = name == "corner" ? .8 + (id - 1) * 3 : id < 3 ? 2 + (id - 1) * .83 : id == 3 ? 0 : 4;
            var sample = map.Segments[segment].Map(name == "corner" ? .4 : (id < 3 ? .02 : .06 + id * .01), offset);
            var speed = name == "corner" ? 12 : 18d;
            var heading = sample.TangentHeadingRadians;
            if (name == "corner") heading += ReferenceBikeAttitude.Neutral.RelativeSlideAngle(.4 / 3);
            var rider = new RiderSnapshot(id, RiderProfile.CreateDefault(id), RiderPosition.Create(1, segment, 0, track.Segments.Count),
                -1, 2, 2, (float)speed, 0, RiderRaceStatus.Racing, 0, CoreSim.Setup.BikeSetup.Neutral, .5f, .5f);
            return new DynamicBike(rider, sample.Position, TimeStepRace.Direction(sample.TangentHeadingRadians) * speed,
                heading, name == "corner" ? speed / (track.Geometry.InnerRadiusMeters + offset) : 0, new(offset));
        }).ToArray();
        if (name == "straight") bikes[1] = bikes[1] with { Position = bikes[1].Position + new MeterPoint(6, 0) };
        if (name is "side" or "encroach" or "light-straight" or "strong-no-room")
        {
            var closure = name == "light-straight" ? .08 : name == "strong-no-room" ? 3 : .8;
            bikes[0] = bikes[0] with { Velocity = new(18, -closure) };
            bikes[1] = bikes[1] with { Velocity = new(18, closure) };
            if (name == "light-straight") bikes[1] = bikes[1] with
            { Position = bikes[1].Position + new MeterPoint(0, .025), Control = new(2.805) };
            if (name == "encroach") bikes[0] = bikes[0] with { Position = bikes[0].Position + new MeterPoint(-.5, 0), Control = new(4) };
            if (name == "strong-no-room")
            {
                foreach (var i in new[] { 0, 1 }) bikes[i] = bikes[i] with
                { Position = bikes[i].Position + new MeterPoint(0, -1), Control = new(bikes[i].Control.TargetOffset + 1) };
                var slippery = new TrackState(track.Segments.Count, 5, (_, _) => new(.4f, .2f, .1f)).Snapshot();
                return new(track, slippery, reverse ? bikes.Reverse() : bikes, reverse: reverse);
            }
        }
        if (name == "rear")
        {
            bikes[0] = bikes[0] with { Velocity = new(25, 0) };
            bikes[1] = bikes[1] with { Position = bikes[0].Position + new MeterPoint(3, 0), Velocity = new(15, 0), Control = new(2) };
        }
        if (name == "near-miss") bikes[1] = bikes[1] with
        { Position = bikes[0].Position + new MeterPoint(3, -.801), Velocity = new(15, 0), Control = new(2.801) };
        return new(track, surface, reverse ? bikes.Reverse() : bikes, reverse: reverse);
    }
    private static TimeStepRace BendContact(bool reverse)
    {
        var track = Track.CreateStandingStartExample(); var map = new TrackMetricEmbedding(track, true);
        var template = Create("corner").Bikes;
        var inner = map.Segments[1].Map(.4, 3); var outward = new MeterPoint(Math.Sin(inner.TangentHeadingRadians), -Math.Cos(inner.TangentHeadingRadians));
        var heading = inner.TangentHeadingRadians + ReferenceBikeAttitude.Neutral.RelativeSlideAngle(.4 / 3);
        // Only fixture construction varies initial offset to get a 3 mm gap
        // between actual #55 capsules, independent of their corner slide angle.
        double Gap(double offset) => MechanicalSeparation.Between(
            BikeFootprint.Create(inner.Position, heading, SpeedwayBikeDimensions.Reference),
            BikeFootprint.Create(inner.Position + outward * offset, heading, SpeedwayBikeDimensions.Reference)).SignedMeters;
        var lo = 0d; var hi = 3d;
        for (var n = 0; n < 40; n++) { var mid = (lo + hi) * .5; if (Gap(mid) < .003) lo = mid; else hi = mid; }
        for (var i = 0; i < 2; i++)
        {
            var offset = i == 0 ? 3 : 3 + hi;
            var p = map.Segments[1].Map(.4, offset);
            template[i] = template[i] with { Position = p.Position, Heading = heading,
                Velocity = TimeStepRace.Direction(p.TangentHeadingRadians) * 12 + outward * (i == 0 ? .4 : -.4),
                AngularVelocity = 12 / (track.Geometry.InnerRadiusMeters + offset), Control = new(offset) };
        }
        return new(track, TrackState.CreateDefault(track).Snapshot(), reverse ? template.Reverse() : template, reverse: reverse);
    }
}
