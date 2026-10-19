using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public readonly record struct RoadPoint(int Segment, double Progress, double ReferenceMeters,
    double OffsetMeters, double Heading, double RadiusMeters, double CornerProgress)
{
    public Vector2 Tangent => new((float)Math.Cos(Heading), (float)Math.Sin(Heading));
    public Vector2 Outward => new(Tangent.Y, -Tangent.X);
    public bool IsTurn => RadiusMeters > 0;
}

/// <summary>Read-only inverse of the existing metric chain. Never moves a body.</summary>
public sealed class TrackFrame
{
    public Track Track { get; }
    public TrackMetricEmbedding Embedding { get; }
    private readonly double[] starts;
    public double LapMeters { get; }
    public float MinimumUsableWidthMeters => Math.Min(LaneModel.UsableRacingWidthMeters(SegmentType.Straight, Track.Geometry),
        LaneModel.UsableRacingWidthMeters(SegmentType.TurnEntry, Track.Geometry));
    public RacingContext Context(RoadPoint road, double total)
    {
        var first = Track.CornerTopology.Corners[0];
        if (total < starts[first.StartSegmentIndex]) return RacingContext.StartStraight;
        return total < LapMeters && first.ContainsSegment(road.Segment) ? RacingContext.FirstBend : RacingContext.NormalRacing;
    }
    public TrackFrame(Track track)
    {
        Track = track; Embedding = new(track);
        if (!Embedding.Closure.SupportsLapWrap) throw new ArgumentException("A closed metric track is required.");
        starts = new double[track.Segments.Count + 1];
        for (var i = 0; i < track.Segments.Count; i++)
        {
            var s = Embedding.Segments[i];
            starts[i + 1] = starts[i] + (s.SegmentType == SegmentType.Straight
                ? s.StraightLengthMeters : s.InnerRadiusMeters * s.TurnAngleRadians);
        }
        LapMeters = starts[^1];
    }
    public (Vector2 Position, RoadPoint Road) At(double referenceMeters, double offset)
    {
        var s = ((referenceMeters % LapMeters) + LapMeters) % LapMeters;
        var i = Array.FindLastIndex(starts, x => x <= s);
        i = Math.Min(i, starts.Length - 2);
        var p = (s - starts[i]) / (starts[i + 1] - starts[i]);
        var sample = Embedding.Segments[i].Map(p, offset);
        return (new((float)sample.Position.X, (float)sample.Position.Y), Point(i, p, offset));
    }
    private RoadPoint Point(int i, double p, double offset)
    {
        var s = Embedding.Segments[i];
        var turn = s.SegmentType != SegmentType.Straight;
        var corner = Track.CornerTopology.CornerForSegment(i);
        var cp = corner is null ? 0 : (i - corner.StartSegmentIndex + p) / corner.SegmentCount;
        return new(i, p, starts[i] + p * (starts[i + 1] - starts[i]), offset,
            s.StartTangentHeadingRadians + (turn ? p * s.TurnAngleRadians : 0),
            turn ? s.InnerRadiusMeters + offset : 0, cp);
    }
    /// <summary>Allocation-free static-road preview for the slip controller; same chain as At, no body.</summary>
    public RoadPoint RoadAt(double referenceMeters,double offset)
    {
        var s=((referenceMeters%LapMeters)+LapMeters)%LapMeters;var i=0;
        while(i<starts.Length-2 && starts[i+1]<=s)i++;
        return Point(i,(s-starts[i])/(starts[i+1]-starts[i]),offset);
    }
    public RoadPoint Project(Vector2 position)
    {
        var best = double.PositiveInfinity; RoadPoint result = default;
        for (var i = 0; i < Embedding.Segments.Count; i++)
        {
            var s = Embedding.Segments[i];
            var t = new Vector2((float)Math.Cos(s.StartTangentHeadingRadians), (float)Math.Sin(s.StartTangentHeadingRadians));
            var origin = new Vector2((float)s.StartReferencePosition.X, (float)s.StartReferencePosition.Y);
            double p;
            if (s.SegmentType == SegmentType.Straight)
                p = Math.Clamp(Vector2.Dot(position - origin, t) / s.StraightLengthMeters, 0, 1);
            else
            {
                var center = origin + new Vector2(-t.Y, t.X) * (float)s.InnerRadiusMeters;
                var radial = position - center;
                var angle = BikeAngles.Wrap(Math.Atan2(radial.Y, radial.X) + Math.PI / 2 - s.StartTangentHeadingRadians);
                p = Math.Clamp(angle / s.TurnAngleRadians, 0, 1);
            }
            var q = s.Map(p, 0);
            var delta = position - new Vector2((float)q.Position.X, (float)q.Position.Y);
            // Accumulate in double: float squared distances hid millimetre-scale
            // progress past a tangent boundary behind the much larger lane offset.
            var score = (double)delta.X * delta.X + (double)delta.Y * delta.Y;
            if (score >= best) continue;
            var outward = new Vector2((float)Math.Sin(q.TangentHeadingRadians), -(float)Math.Cos(q.TangentHeadingRadians));
            best = score; result = Point(i, p, Vector2.Dot(delta, outward));
        }
        return result;
    }
    public double UnwrapDelta(double from, double to)
    {
        var delta = to - from;
        if (delta < -LapMeters / 2) delta += LapMeters;
        if (delta > LapMeters / 2) delta -= LapMeters;
        return delta;
    }
    /// <summary>Instantaneous metres along the existing INNER reference per second.
    /// Straight and arc rates share units, including when riders straddle a boundary.</summary>
    public double ReferenceSpeed(RoadPoint road, double tangentSpeed, double? candidateOffsetMeters = null)
    {
        if (!double.IsFinite(tangentSpeed)) throw new ArgumentOutOfRangeException(nameof(tangentSpeed));
        if (!road.IsTurn) return tangentSpeed;
        var inner = Embedding.Segments[road.Segment].InnerRadiusMeters;
        var radius = inner + (candidateOffsetMeters ?? road.OffsetMeters);
        if (radius <= 0 || !double.IsFinite(radius) || !double.IsFinite(tangentSpeed))
            throw new ArgumentOutOfRangeException(nameof(candidateOffsetMeters));
        return tangentSpeed * inner / radius;
    }
    public double ReferenceSpeed(RiderView rider)
        => ReferenceSpeed(rider.Road, Vector2.Dot(rider.Velocity, rider.Road.Tangent));
    public double DistanceToCurvatureChange(RoadPoint road)
    {
        var length = starts[road.Segment + 1] - starts[road.Segment];
        var remaining = (1 - road.Progress) * length;
        var next = (road.Segment + 1) % Track.Segments.Count;
        var visited = 0;
        while ((Track.Segments[next].Type != SegmentType.Straight) == road.IsTurn)
        {
            if (++visited >= Track.Segments.Count) return double.PositiveInfinity;
            remaining += starts[next + 1] - starts[next];
            next = (next + 1) % Track.Segments.Count;
        }
        return road.IsTurn ? remaining * road.RadiusMeters / Embedding.Segments[road.Segment].InnerRadiusMeters : remaining;
    }
}
