using System.Security.Cryptography;
using System.Text;

namespace CoreSim.PhysicalSpace;

/// <summary>Topology provenance, independent of the Euclidean coordinate frame.</summary>
public sealed record PoseSource(int LapIndex, int SegmentIndex, int SegmentId, SegmentType SegmentType, int? CornerId);

public sealed record TrackClosure(MeterPoint PositionResidualMeters, double HeadingResidualRadians)
{
    public double PositionErrorMeters => PositionResidualMeters.Length;
    // TrackGeometry stores float angles. Six copies of float(pi/3) do not equal double(2*pi).
    // These absolute tolerances admit that micrometre-scale residual without correcting any geometry.
    public const double PositionToleranceMeters = 2e-5;
    public const double HeadingToleranceRadians = 5e-7;
    public bool SupportsLapWrap => PositionErrorMeters <= PositionToleranceMeters
        && Math.Abs(HeadingResidualRadians) <= HeadingToleranceRadians;
}

public readonly record struct MetricTrackSample(MeterPoint Position, MeterPoint VelocityMetersPerSecond,
    double TangentHeadingRadians, double TravelHeadingRadians);

/// <summary>One exact segment of the ordered model-space metric chain; no trajectory solver.</summary>
public sealed record MetricTrackSegment(int SegmentIndex, int SegmentId, SegmentType SegmentType,
    MeterPoint StartReferencePosition, double StartTangentHeadingRadians, double StraightLengthMeters,
    double InnerRadiusMeters, double TurnAngleRadians)
{
    public MetricTrackSample Map(double progress, double physicalOffsetMeters, double progressRate = 0, double offsetRate = 0)
    {
        GeometryValidation.Unit(progress, nameof(progress));
        GeometryValidation.Nonnegative(physicalOffsetMeters, nameof(physicalOffsetMeters));
        return Prepare(progressRate, offsetRate).MapValidated(progress, physicalOffsetMeters);
    }
    internal PreparedMetricTrackSegment Prepare(double progressRate, double offsetRate) => new(this, progressRate, offsetRate);
}

/// <summary>Exact constant geometry of one immutable segment and node's fixed rates.</summary>
internal readonly struct PreparedMetricTrackSegment
{
    private readonly MetricTrackSegment segment;
    private readonly double progressRate, offsetRate, straightHeading, straightTravelHeading;
    private readonly MeterPoint centre, straightTangent, straightOutward, straightVelocity;
    internal PreparedMetricTrackSegment(MetricTrackSegment segment, double progressRate, double offsetRate)
    {
        GeometryValidation.Finite(progressRate, nameof(progressRate)); GeometryValidation.Finite(offsetRate, nameof(offsetRate));
        this = default;
        this.segment = segment; this.progressRate = progressRate; this.offsetRate = offsetRate;
        if (segment.SegmentType == SegmentType.Straight)
        {
            // Retain the original + 0, including its signed-zero behavior.
            var heading = segment.StartTangentHeadingRadians + 0;
            straightTangent = new(Math.Cos(heading), Math.Sin(heading));
            straightOutward = new(straightTangent.Y, -straightTangent.X);
            straightVelocity = straightTangent * (segment.StraightLengthMeters * progressRate) + straightOutward * offsetRate;
            straightHeading = BikeAngles.Wrap(heading);
            straightTravelHeading = straightVelocity.Length == 0 ? BikeAngles.Wrap(heading) : Math.Atan2(straightVelocity.Y, straightVelocity.X);
        }
        else
        {
            var entryLeft = new MeterPoint(-Math.Sin(segment.StartTangentHeadingRadians), Math.Cos(segment.StartTangentHeadingRadians));
            centre = segment.StartReferencePosition + entryLeft * segment.InnerRadiusMeters;
        }
    }
    internal MetricTrackSample Map(double progress, double physicalOffsetMeters)
    {
        GeometryValidation.Unit(progress, nameof(progress));
        GeometryValidation.Nonnegative(physicalOffsetMeters, nameof(physicalOffsetMeters));
        return MapValidated(progress, physicalOffsetMeters);
    }
    internal MetricTrackSample MapValidated(double progress, double physicalOffsetMeters)
    {
        if (segment.SegmentType == SegmentType.Straight)
        {
            var position = segment.StartReferencePosition + straightTangent * (segment.StraightLengthMeters * progress) + straightOutward * physicalOffsetMeters;
            return new(position, straightVelocity, straightHeading, straightTravelHeading);
        }
        var heading = segment.StartTangentHeadingRadians + segment.TurnAngleRadians * progress;
        var tangent = new MeterPoint(Math.Cos(heading), Math.Sin(heading));
        var outward = new MeterPoint(tangent.Y, -tangent.X);
        var cornerPosition = centre + outward * (segment.InnerRadiusMeters + physicalOffsetMeters);
        var velocity = tangent * ((segment.InnerRadiusMeters + physicalOffsetMeters) * segment.TurnAngleRadians * progressRate) + outward * offsetRate;
        return new(cornerPosition, velocity, BikeAngles.Wrap(heading), velocity.Length == 0 ? BikeAngles.Wrap(heading) : Math.Atan2(velocity.Y, velocity.X));
    }
}

/// <summary>
/// Immutable composition of existing track geometry. Canonical origin/heading are arbitrary;
/// optional rigid transforms change only coordinate representation. Not surveyed venue coordinates.
/// </summary>
public sealed class TrackMetricEmbedding
{
    private readonly Track _track;
    public IReadOnlyList<MetricTrackSegment> Segments { get; }
    public string FrameId { get; }
    public TrackClosure Closure { get; }
    public TrackMetricEmbedding(Track track, MeterPoint origin = default, double initialHeadingRadians = 0)
    {
        ArgumentNullException.ThrowIfNull(track); GeometryValidation.Finite(initialHeadingRadians, nameof(initialHeadingRadians));
        _track = track;
        var segments = new List<MetricTrackSegment>(track.Segments.Count);
        var position = origin; var heading = initialHeadingRadians;
        foreach (var segment in track.Segments)
        {
            var mapped = new MetricTrackSegment(segments.Count, segment.Id, segment.Type, position, heading,
                segment.StraightLengthMetersOverride ?? track.Geometry.StraightLengthMeters,
                track.Geometry.InnerRadiusMeters, track.Geometry.TurnSegmentAngleRadians);
            segments.Add(mapped); position = mapped.Map(1, 0).Position;
            if (segment.Type != SegmentType.Straight) heading += track.Geometry.TurnSegmentAngleRadians;
        }
        Segments = segments.AsReadOnly();
        Closure = new(position - origin, BikeAngles.Wrap(heading - initialHeadingRadians));
        // Stable exact input bits, never runtime hash codes, collection order or lap number.
        var identity = new StringBuilder("track-metric-v1");
        void Bits(double value) => identity.Append('/').Append(BitConverter.DoubleToInt64Bits(value).ToString("X16", System.Globalization.CultureInfo.InvariantCulture));
        Bits(track.Geometry.StraightLengthMeters); Bits(track.Geometry.InnerRadiusMeters);
        Bits(track.Geometry.StraightWidthMeters); Bits(track.Geometry.TurnWidthMeters); Bits(track.Geometry.TurnSegmentAngleRadians);
        Bits(origin.X); Bits(origin.Y); Bits(initialHeadingRadians);
        foreach (var segment in track.Segments)
        { Bits(segment.Id); Bits((int)segment.Type); Bits(segment.StraightLengthMetersOverride ?? 0); Bits(segment.IsStandingStartSegment ? 1 : 0); }
        FrameId = "track-metric:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString())));
    }
    public void ValidateCompatible(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (ReferenceEquals(track, _track)) return;
        if (track.Geometry != _track.Geometry || track.Segments.Count != _track.Segments.Count
            || track.Segments.Where((s, i) => s.Id != _track.Segments[i].Id || s.Type != _track.Segments[i].Type
                || s.StraightLengthMetersOverride != _track.Segments[i].StraightLengthMetersOverride
                || s.IsStandingStartSegment != _track.Segments[i].IsStandingStartSegment).Any())
            throw new ArgumentException("Observer motions must use the same compatible track geometry and ordered topology.", nameof(track));
    }
}
