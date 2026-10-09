using CoreSim.Analysis;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;

namespace CoreSim.Tests;

// Shared with the executable audit, including its build against the pre-fix assembly.
internal static class ContactDecisionGeometryFixtures
{
    internal sealed record Edge(string Name, Track Track, TrackMetricEmbedding Embedding,
        PhysicalPoseInterval Interval, double Margin, bool Outer);
    internal sealed record Tactical(string Name, PhysicalBikePose A, PhysicalBikePose B,
        PoseRateBounds RateA, PoseRateBounds RateB, ContestedSpaceEvent Space);

    internal static IEnumerable<Edge> Edges()
    {
        var basis = ContestedSpaceResponseEvidence.CreateTrack(false);
        foreach (var angle in new[] {MathF.PI / 3, .7f})
        {
            var track = new Track(basis.Segments, new TrackGeometry(60,24,14,14,angle));
            var embedding = new TrackMetricEmbedding(track, true);
            foreach (var index in new[] {0,1,2,3})
            foreach (var progress in new[] {0d,.5,1d})
            foreach (var yaw in new[] {0d,-Math.PI/6,Math.PI/2})
            foreach (var outer in new[] {false,true})
            foreach (var margin in new[] {-1e-8,0d,1e-8})
            {
                var segment = embedding.Segments[index];
                var sample = segment.Map(progress,0);
                var forward = ContactFrameArithmetic.Direction(sample.TangentHeadingRadians,true);
                var outward = new MeterPoint(forward.Y,-forward.X);
                var dimensions = SpeedwayBikeDimensions.Reference;
                var support = InteractionGeometryModel.Project(BikeFootprint.Create(default,
                    sample.TangentHeadingRadians+yaw,dimensions,true),outward);
                var bulge = segment.SegmentType == SegmentType.Straight ? 0
                    : dimensions.BoundingRadiusMeters*dimensions.BoundingRadiusMeters
                        /(2*Math.Max(1,track.Geometry.InnerRadiusMeters-dimensions.BoundingRadiusMeters));
                var offset = outer ? LaneModel.UsableRacingWidthMeters(segment.SegmentType,track.Geometry)
                    + TrackGeometry.ProvisionalOuterReferenceOffsetFromTrackEdgeMeters-support.Max-bulge-margin
                    : -TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters-support.Min+margin;
                var source = new PoseSource(0,index,segment.SegmentId,segment.SegmentType,track.CornerTopology.CornerForSegment(index)?.CornerId);
                PhysicalBikePose Pose(double time) => new(1,embedding.FrameId,sample.Position+outward*offset,
                    new(time,sample.TangentHeadingRadians,sample.TangentHeadingRadians+yaw),dimensions,sample.TangentHeadingRadians,source)
                    {DeterministicArithmetic=true};
                yield return new($"{angle:R}/{index}/{progress:R}/{yaw:R}/{outer}/{margin:R}",track,embedding,
                    new LinearBikePoseInterval(Pose(0),Pose(1)),margin,outer);
            }
        }
    }

    internal static IEnumerable<Tactical> Tactics()
    {
        foreach (var angle in new[] {0d,.3,.7,2.9,-2.9})
        foreach (var longitudinal in new[] {-2.10000001,-2.09999999,-5e-10,0d,5e-10,2.09999999,2.10000001})
        foreach (var closing in new[] {-5e-10,0d,5e-10})
        foreach (var lateral in new[] {-.5,.5})
        {
            var forward = ContactFrameArithmetic.Direction(angle,true);
            var outward = new MeterPoint(forward.Y,-forward.X);
            var a = Pose(1,default,angle);
            var b = Pose(2,forward*longitudinal+outward*lateral,angle);
            yield return new($"{angle:R}/{longitudinal:R}/{closing:R}/{lateral:R}",a,b,
                new(default,0,0),new(forward*closing,0,0),Space(a,b));
        }
        // Basis deltas expose each deterministic direction component directly.
        foreach (var angle in new[] {.3,.7,2.9,-2.9})
        foreach (var delta in new[] {new MeterPoint(1,0),new MeterPoint(0,1)})
        {
            var a=Pose(1,default,angle);var b=Pose(2,delta,angle);
            yield return new($"axis/{angle:R}/{delta.X:R}/{delta.Y:R}",a,b,new(default,0,0),new(delta,0,0),Space(a,b));
        }
    }
    internal static PhysicalBikePose Pose(int id,MeterPoint position,double heading,bool deterministic=true,string frame="decision-fixture")
        => new(id,frame,position,new(0,heading,heading),SpeedwayBikeDimensions.Reference,heading)
            {DeterministicArithmetic=deterministic};
    internal static ContestedSpaceEvent Space(PhysicalBikePose a,PhysicalBikePose b)
        => new(a.RiderId,b.RiderId,a.FrameId,0,1,null,.01,0,BikeComponent.Chassis,BikeComponent.Chassis,
            SpaceConflictKind.ParallelOverlap,new(0,0,0,0,0,0),b.Position-a.Position,default,true,.01);
    internal static object EdgeResult(Edge edge) => new {edge.Name,edge.Margin,edge.Outer,
        Start=edge.Interval.Sample(0),End=edge.Interval.Sample(1),
        WithinTrack=ContestedSpaceInteractionCoordinator.WithinTrack(new[]{edge.Interval},edge.Track,edge.Embedding)};
    internal static object TacticalResult(Tactical t)
    {
        var geometry=InteractionGeometryModel.Describe(t.Space,t.A,t.B,t.RateA,t.RateB);
        return new {t.Name,t.A,t.B,t.RateA,t.RateB,Geometry=geometry,
            RoleA=InteractionGeometryModel.Role(1,geometry),RoleB=InteractionGeometryModel.Role(2,geometry)};
    }
}
