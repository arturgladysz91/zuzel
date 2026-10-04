namespace CoreSim.PhysicalSpace;

/// <summary>Shared provisional smooth corner yaw. No profile/skill/force/RNG input.</summary>
public sealed record ReferenceBikeAttitude
{
    public double SlideBuildStartProgress { get; }
    public double PeakSlideAngleRadians { get; }
    public double PeakProgress { get; }
    public double StraighteningStartProgress { get; }
    public double StraighteningCompletionProgress { get; }
    public static ReferenceBikeAttitude Neutral { get; } = new(.05, Math.PI / 6, .35, .55, .95);
    public ReferenceBikeAttitude(double buildStart, double peakAngle, double peak, double straightenStart, double completion)
    {
        GeometryValidation.Unit(buildStart, nameof(buildStart)); GeometryValidation.Unit(peak, nameof(peak));
        GeometryValidation.Unit(straightenStart, nameof(straightenStart)); GeometryValidation.Unit(completion, nameof(completion));
        GeometryValidation.Nonnegative(peakAngle, nameof(peakAngle));
        if (!(buildStart < peak && peak <= straightenStart && straightenStart < completion) || peakAngle >= Math.PI / 2)
            throw new ArgumentException("Attitude phases must be ordered; reference yaw is in [0, pi/2).");
        SlideBuildStartProgress = buildStart; PeakSlideAngleRadians = peakAngle; PeakProgress = peak;
        StraighteningStartProgress = straightenStart; StraighteningCompletionProgress = completion;
    }
    public IReadOnlyList<double> ProgressKnots => Array.AsReadOnly(new[]
        { SlideBuildStartProgress, PeakProgress, StraighteningStartProgress, StraighteningCompletionProgress });
    public double RelativeSlideAngle(double progress)
    {
        GeometryValidation.Unit(progress, nameof(progress));
        if (progress <= SlideBuildStartProgress || progress >= StraighteningCompletionProgress) return 0;
        if (progress < PeakProgress) return PeakSlideAngleRadians * Smooth((progress - SlideBuildStartProgress) / (PeakProgress - SlideBuildStartProgress));
        if (progress <= StraighteningStartProgress) return PeakSlideAngleRadians;
        return PeakSlideAngleRadians * (1 - Smooth((progress - StraighteningStartProgress) / (StraighteningCompletionProgress - StraighteningStartProgress)));
    }
    public double MaximumSlopeRadiansPerProgress => 1.5 * PeakSlideAngleRadians /
        Math.Min(PeakProgress - SlideBuildStartProgress, StraighteningCompletionProgress - StraighteningStartProgress);
    private static double Smooth(double t) => t * t * (3 - 2 * t);
}

/// <summary>Bounds apply inside one continuous knot interval, never across a state/frame jump.</summary>
public readonly record struct PoseRateBounds(MeterPoint CenterVelocityMetersPerSecond,
    double CenterAccelerationBoundMetersPerSecondSquared, double AngularSpeedBoundRadiansPerSecond);

/// <summary>
/// Immutable continuous resolved-pose input. New attitude sources may implement this contract;
/// RateBounds must conservatively bound every time in the requested subinterval.
/// </summary>
public abstract class PhysicalPoseInterval
{
    public int RiderId { get; }
    public string FrameId { get; }
    public double StartTimeSeconds { get; }
    public double EndTimeSeconds { get; }
    public SpeedwayBikeDimensions Dimensions { get; }
    public bool StartsAtDiscontinuity { get; }
    protected PhysicalPoseInterval(int riderId, string frameId, double start, double end,
        SpeedwayBikeDimensions dimensions, bool startsAtDiscontinuity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId); ArgumentNullException.ThrowIfNull(dimensions);
        GeometryValidation.Nonnegative(start, nameof(start)); GeometryValidation.Nonnegative(end, nameof(end));
        if (end <= start) throw new ArgumentException("A continuous interval requires positive duration.");
        RiderId = riderId; FrameId = frameId; StartTimeSeconds = start; EndTimeSeconds = end;
        Dimensions = dimensions; StartsAtDiscontinuity = startsAtDiscontinuity;
    }
    protected double Fraction(double time)
    {
        GeometryValidation.Finite(time, nameof(time));
        if (time < StartTimeSeconds || time > EndTimeSeconds) throw new ArgumentOutOfRangeException(nameof(time));
        return (time - StartTimeSeconds) / (EndTimeSeconds - StartTimeSeconds);
    }
    public abstract PhysicalBikePose Sample(double commonTimeSeconds);
    public abstract PoseRateBounds RateBounds(double startTimeSeconds, double endTimeSeconds);
}

/// <summary>Injected pose histories, useful for controlled geometry and future attitude consumers.</summary>
public sealed class LinearBikePoseInterval : PhysicalPoseInterval
{
    private readonly PhysicalBikePose _start;
    private readonly PhysicalBikePose _end;
    public LinearBikePoseInterval(PhysicalBikePose start, PhysicalBikePose end, bool startsAtDiscontinuity = false)
        : base(start.RiderId, start.FrameId, start.CommonTimeSeconds, end.CommonTimeSeconds, start.Dimensions, startsAtDiscontinuity)
    {
        if (end.RiderId != start.RiderId || end.FrameId != start.FrameId || end.Dimensions != start.Dimensions)
            throw new ArgumentException("Pose interval identity and dimensions must be constant.");
        _start = start; _end = end;
    }
    public override PhysicalBikePose Sample(double time)
    {
        var f = Fraction(time);
        return new(RiderId, FrameId, _start.Position + (_end.Position - _start.Position) * f,
            new(time, BikeAngles.Interpolate(_start.Attitude.TravelHeadingRadians, _end.Attitude.TravelHeadingRadians, f),
                BikeAngles.Interpolate(_start.Attitude.BikeHeadingRadians, _end.Attitude.BikeHeadingRadians, f)), Dimensions,
            BikeAngles.Interpolate(_start.ReferenceTangentHeadingRadians, _end.ReferenceTangentHeadingRadians, f));
    }
    public override PoseRateBounds RateBounds(double start, double end)
    {
        Fraction(start); Fraction(end);
        var duration = EndTimeSeconds - StartTimeSeconds;
        return new((_end.Position - _start.Position) * (1 / duration), 0,
            Math.Abs(BikeAngles.Wrap(_end.Attitude.BikeHeadingRadians - _start.Attitude.BikeHeadingRadians)) / duration);
    }
}

/// <summary>Read-only geometry adapter of canonical #53 nodes; no alternate physical traversal.</summary>
public static class ResolvedBikePoses
{
    public static IReadOnlyList<PhysicalPoseInterval> FromMotion(ResolvedRiderMotion motion, Track track,
        SpeedwayBikeDimensions? dimensions = null, ReferenceBikeAttitude? attitude = null)
    {
        ArgumentNullException.ThrowIfNull(motion); ArgumentNullException.ThrowIfNull(track);
        if (motion.SegmentIndex >= track.Segments.Count || track.Segments[motion.SegmentIndex].Id != motion.SegmentId)
            throw new ArgumentException("Motion does not belong to this track segment.");
        var result = new List<PhysicalPoseInterval>();
        var lap = (int)Math.Floor(motion.Initial.CanonicalProgress / track.Segments.Count);
        var corner = track.CornerTopology.CornerForSegment(motion.SegmentIndex);
        var frame = corner is null ? $"lap:{lap}/straight:{motion.SegmentIndex}" : $"lap:{lap}/corner:{corner.CornerId}";
        var profile = attitude ?? ReferenceBikeAttitude.Neutral;
        for (var index = 1; index < motion.Nodes.Count; index++)
        {
            var a = motion.Nodes[index - 1]; var b = motion.Nodes[index];
            if (b.LocalTimeSeconds <= a.LocalTimeSeconds) continue; // Explicit zero-time events never swept.
            var discontinuity = (a.LocalTimeSeconds == 0 && motion.EntryBoundary?.HasPhysicalOffsetDiscontinuity == true)
                || motion.StateTransitions.Any(e => e.After.LocalTimeSeconds == a.LocalTimeSeconds
                    && (e.Before.PhysicalOffsetMeters != e.After.PhysicalOffsetMeters || e.Before.SegmentProgress != e.After.SegmentProgress));
            var start = (double)motion.StartElapsedTimeSeconds + a.LocalTimeSeconds;
            var end = (double)motion.StartElapsedTimeSeconds + b.LocalTimeSeconds;
            var cuts = new List<double> { start, end };
            if (corner is not null && b.SegmentProgress > a.SegmentProgress)
            {
                var p0 = (motion.SegmentIndex - corner.StartSegmentIndex + (double)a.SegmentProgress) / corner.SegmentCount;
                var p1 = (motion.SegmentIndex - corner.StartSegmentIndex + (double)b.SegmentProgress) / corner.SegmentCount;
                foreach (var p in profile.ProgressKnots)
                    if (p > p0 && p < p1) cuts.Add(start + (end - start) * (p - p0) / (p1 - p0));
            }
            cuts.Sort();
            for (var cut = 1; cut < cuts.Count; cut++) result.Add(new ProductionPoseInterval(motion, track,
                a, b, frame, cuts[cut - 1], cuts[cut], dimensions ?? SpeedwayBikeDimensions.Reference, profile,
                cut == 1 && discontinuity));
        }
        return result.AsReadOnly();
    }

    private sealed class ProductionPoseInterval : PhysicalPoseInterval
    {
        private readonly RiderMotionSample _a, _b;
        private readonly double _nodeStart, _duration, _length, _radius, _angle, _phaseStart, _phaseSpan;
        private readonly ReferenceBikeAttitude _profile;
        private readonly bool _straight;
        public ProductionPoseInterval(ResolvedRiderMotion motion, Track track, RiderMotionSample a, RiderMotionSample b,
            string frame, double start, double end, SpeedwayBikeDimensions dimensions, ReferenceBikeAttitude profile, bool discontinuity)
            : base(motion.RiderId, frame, start, end, dimensions, discontinuity)
        {
            _a = a; _b = b; _nodeStart = (double)motion.StartElapsedTimeSeconds + a.LocalTimeSeconds;
            _duration = (double)b.LocalTimeSeconds - a.LocalTimeSeconds; _profile = profile;
            var segment = track.Segments[motion.SegmentIndex]; _straight = segment.Type == SegmentType.Straight;
            _length = segment.StraightLengthMetersOverride ?? track.Geometry.StraightLengthMeters;
            _radius = track.Geometry.InnerRadiusMeters; _angle = track.Geometry.TurnSegmentAngleRadians;
            var corner = track.CornerTopology.CornerForSegment(motion.SegmentIndex);
            _phaseStart = corner is null ? 0 : motion.SegmentIndex - corner.StartSegmentIndex;
            _phaseSpan = corner?.SegmentCount ?? 1;
        }
        private (MeterPoint Position, MeterPoint Velocity, double Heading, double Radius, double Phase) Geometry(double time)
        {
            Fraction(time);
            var f = (time - _nodeStart) / _duration;
            var progress = _a.SegmentProgress + ((double)_b.SegmentProgress - _a.SegmentProgress) * f;
            var offset = _a.PhysicalOffsetMeters + ((double)_b.PhysicalOffsetMeters - _a.PhysicalOffsetMeters) * f;
            var dp = ((double)_b.SegmentProgress - _a.SegmentProgress) / _duration;
            var dr = ((double)_b.PhysicalOffsetMeters - _a.PhysicalOffsetMeters) / _duration;
            if (_straight)
            {
                var velocity = new MeterPoint(_length * dp, -dr);
                return (new(_length * progress, -offset), velocity, velocity.Length == 0 ? 0 : Math.Atan2(velocity.Y, velocity.X), 0, 0);
            }
            var theta = (_phaseStart + progress) * _angle; var r = _radius + offset;
            var radial = new MeterPoint(Math.Cos(theta), Math.Sin(theta)); var tangent = new MeterPoint(-radial.Y, radial.X);
            var v = radial * dr + tangent * (r * _angle * dp);
            return (radial * r, v, v.Length == 0 ? theta + Math.PI / 2 : Math.Atan2(v.Y, v.X), r,
                Math.Clamp((_phaseStart + progress) / _phaseSpan, 0, 1));
        }
        public override PhysicalBikePose Sample(double time)
        {
            var g = Geometry(time);
            var beta = _straight ? 0 : _profile.RelativeSlideAngle(g.Phase);
            var referenceTangent = _straight ? 0 : (_phaseStart + _a.SegmentProgress
                + ((double)_b.SegmentProgress - _a.SegmentProgress) * ((time - _nodeStart) / _duration)) * _angle + Math.PI / 2;
            return new(RiderId, FrameId, g.Position, new(time, g.Heading, g.Heading + beta), Dimensions, referenceTangent);
        }
        public override PoseRateBounds RateBounds(double start, double end)
        {
            var g0 = Geometry(start); var g1 = Geometry(end); var mid = Geometry((start + end) / 2);
            if (_straight) return new(mid.Velocity, 0, 0);
            var dp = Math.Abs(((double)_b.SegmentProgress - _a.SegmentProgress) / _duration);
            var dr = Math.Abs(((double)_b.PhysicalOffsetMeters - _a.PhysicalOffsetMeters) / _duration);
            var thetaRate = _angle * dp;
            // Linear radius/angle nodes: |p''| <= r*theta'^2 + 2*|r'*theta'|.
            // Heading rotates by at most 2*theta' (radial rate is constant).
            return new(mid.Velocity, Math.Max(g0.Radius, g1.Radius) * thetaRate * thetaRate + 2 * dr * thetaRate,
                2 * thetaRate + _profile.MaximumSlopeRadiansPerProgress * dp / _phaseSpan);
        }
    }
}
