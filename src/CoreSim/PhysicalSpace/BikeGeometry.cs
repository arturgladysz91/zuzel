namespace CoreSim.PhysicalSpace;

/// <summary>Euclidean metres; x/y are axes of the explicitly identified metric frame.</summary>
public readonly record struct MeterPoint
{
    public double X { get; }
    public double Y { get; }
    public MeterPoint(double x, double y)
    {
        GeometryValidation.Finite(x, nameof(x)); GeometryValidation.Finite(y, nameof(y)); X = x; Y = y;
    }
    public double Length => Math.Sqrt(X * X + Y * Y);
    public static MeterPoint operator +(MeterPoint a, MeterPoint b) => new(a.X + b.X, a.Y + b.Y);
    public static MeterPoint operator -(MeterPoint a, MeterPoint b) => new(a.X - b.X, a.Y - b.Y);
    public static MeterPoint operator *(MeterPoint a, double s) => new(a.X * s, a.Y * s);
    public static double Dot(MeterPoint a, MeterPoint b) => a.X * b.X + a.Y * b.Y;
    public static double Cross(MeterPoint a, MeterPoint b) => a.X * b.Y - a.Y * b.X;
}

public static class BikeAngles
{
    /// <summary>Counterclockwise positive, normalized to [-pi, pi).</summary>
    public static double Wrap(double radians)
    {
        GeometryValidation.Finite(radians, nameof(radians));
        var angle = radians % Math.Tau;
        if (angle >= Math.PI) angle -= Math.Tau;
        if (angle < -Math.PI) angle += Math.Tau;
        return angle;
    }
    public static double Interpolate(double from, double to, double fraction)
    {
        GeometryValidation.Unit(fraction, nameof(fraction));
        return Wrap(Wrap(from) + Wrap(to - from) * fraction);
    }
}

/// <summary>Yaw relative to travel, not lean, tyre slip ratio or a force-derived angle.</summary>
public sealed record BikeAttitudeSample
{
    public double CommonTimeSeconds { get; }
    public double TravelHeadingRadians { get; }
    public double BikeHeadingRadians { get; }
    public double RelativeSlideAngleRadians => BikeAngles.Wrap(BikeHeadingRadians - TravelHeadingRadians);
    public BikeAttitudeSample(double commonTimeSeconds, double travelHeadingRadians, double bikeHeadingRadians)
    {
        GeometryValidation.Nonnegative(commonTimeSeconds, nameof(commonTimeSeconds));
        CommonTimeSeconds = commonTimeSeconds;
        TravelHeadingRadians = BikeAngles.Wrap(travelHeadingRadians);
        BikeHeadingRadians = BikeAngles.Wrap(bikeHeadingRadians);
    }
}

/// <summary>Mechanical dimensions only. Defaults other than the legal bar range are PROVISIONAL.</summary>
public sealed record SpeedwayBikeDimensions
{
    public double OverallMechanicalLengthMeters { get; }
    public double ChassisBodyWidthMeters { get; }
    public double HandlebarWidthMeters { get; }
    public double HandlebarLongitudinalOffsetMeters { get; }
    public double HandlebarTubeDiameterMeters { get; }
    public static SpeedwayBikeDimensions Reference { get; } = new(2.10, .30, .80, .65, .04);
    public SpeedwayBikeDimensions(double length, double bodyWidth, double barWidth, double barOffset, double tubeDiameter)
    {
        GeometryValidation.Positive(length, nameof(length)); GeometryValidation.Positive(bodyWidth, nameof(bodyWidth));
        GeometryValidation.Positive(barWidth, nameof(barWidth)); GeometryValidation.Positive(tubeDiameter, nameof(tubeDiameter));
        GeometryValidation.Finite(barOffset, nameof(barOffset));
        if (bodyWidth > length || tubeDiameter > barWidth || Math.Abs(barOffset) + tubeDiameter / 2 > length / 2)
            throw new ArgumentException("Capsule dimensions or handlebar location are inconsistent.");
        OverallMechanicalLengthMeters = length; ChassisBodyWidthMeters = bodyWidth;
        HandlebarWidthMeters = barWidth; HandlebarLongitudinalOffsetMeters = barOffset; HandlebarTubeDiameterMeters = tubeDiameter;
    }
    public double BoundingRadiusMeters => Math.Max(OverallMechanicalLengthMeters / 2,
        Math.Sqrt(HandlebarLongitudinalOffsetMeters * HandlebarLongitudinalOffsetMeters
            + Math.Pow((HandlebarWidthMeters - HandlebarTubeDiameterMeters) / 2, 2)) + HandlebarTubeDiameterMeters / 2);
}

public enum BikeComponent { Chassis, Handlebar }
public readonly record struct MechanicalCapsule(MeterPoint Start, MeterPoint End, double RadiusMeters);
public readonly record struct BikeFootprint(MechanicalCapsule Chassis, MechanicalCapsule Handlebar)
{
    public MechanicalCapsule Component(BikeComponent component) => component == BikeComponent.Chassis ? Chassis : Handlebar;
    public static BikeFootprint Create(MeterPoint position, double bikeHeadingRadians, SpeedwayBikeDimensions dimensions)
        => Create(position,bikeHeadingRadians,dimensions,false);
    internal static BikeFootprint Create(MeterPoint position, double bikeHeadingRadians, SpeedwayBikeDimensions dimensions, bool deterministicArithmetic)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        var heading = BikeAngles.Wrap(bikeHeadingRadians);
        var forward = ContactFrameArithmetic.Direction(heading,deterministicArithmetic);
        var left = new MeterPoint(-forward.Y, forward.X);
        var bodyHalf = (dimensions.OverallMechanicalLengthMeters - dimensions.ChassisBodyWidthMeters) / 2;
        var barHalf = (dimensions.HandlebarWidthMeters - dimensions.HandlebarTubeDiameterMeters) / 2;
        var barCenter = position + forward * dimensions.HandlebarLongitudinalOffsetMeters;
        return new(new(position - forward * bodyHalf, position + forward * bodyHalf, dimensions.ChassisBodyWidthMeters / 2),
            new(barCenter - left * barHalf, barCenter + left * barHalf, dimensions.HandlebarTubeDiameterMeters / 2));
    }
}

public sealed record PhysicalBikePose
{
    internal bool DeterministicArithmetic { get; init; }
    public int RiderId { get; }
    public string FrameId { get; }
    public MeterPoint Position { get; }
    public BikeAttitudeSample Attitude { get; }
    public SpeedwayBikeDimensions Dimensions { get; }
    public PoseSource? Source { get; }
    /// <summary>Local reference-track tangent, solely for removing shared forward transport in diagnostics.</summary>
    public double ReferenceTangentHeadingRadians { get; }
    public double CommonTimeSeconds => Attitude.CommonTimeSeconds;
    public BikeFootprint Footprint => BikeFootprint.Create(Position, Attitude.BikeHeadingRadians, Dimensions, DeterministicArithmetic);
    public PhysicalBikePose(int riderId, string frameId, MeterPoint position, BikeAttitudeSample attitude, SpeedwayBikeDimensions dimensions,
        double? referenceTangentHeadingRadians = null, PoseSource? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId); ArgumentNullException.ThrowIfNull(attitude);
        ArgumentNullException.ThrowIfNull(dimensions);
        RiderId = riderId; FrameId = frameId; Position = position; Attitude = attitude; Dimensions = dimensions;
        Source = source;
        ReferenceTangentHeadingRadians = BikeAngles.Wrap(referenceTangentHeadingRadians ?? attitude.TravelHeadingRadians);
    }
    public PhysicalBikePose Repose(MeterPoint position, double heading, double time) => new(RiderId, FrameId, position,
        new(time, Attitude.TravelHeadingRadians, heading), Dimensions, ReferenceTangentHeadingRadians, Source)
        { DeterministicArithmetic = DeterministicArithmetic };
}

public readonly record struct FootprintSeparation(double SignedMeters, BikeComponent ComponentA, BikeComponent ComponentB)
{
    public double PenetrationMeters => Math.Max(0, -SignedMeters);
}

public static class MechanicalSeparation
{
    public static FootprintSeparation Between(PhysicalBikePose a, PhysicalBikePose b)
    {
        ArgumentNullException.ThrowIfNull(a); ArgumentNullException.ThrowIfNull(b);
        if (a.FrameId != b.FrameId) throw new ArgumentException("Cannot compare unrelated local coordinate frames.");
        if (Math.Abs(a.CommonTimeSeconds - b.CommonTimeSeconds) > GeometryNumerics.TimeToleranceSeconds)
            throw new ArgumentException("Physical poses must share heat time.");
        var fa = a.Footprint; var fb = b.Footprint;
        return Between(fa, fb);
    }
    internal static FootprintSeparation Between(BikeFootprint fa, BikeFootprint fb)
    {
        var best = new FootprintSeparation(double.PositiveInfinity, BikeComponent.Chassis, BikeComponent.Chassis);
        for (var i = 0; i < 2; i++) for (var j = 0; j < 2; j++)
        {
            var ca = fa.Component((BikeComponent)i); var cb = fb.Component((BikeComponent)j);
            var gap = SegmentDistance(ca.Start, ca.End, cb.Start, cb.End) - ca.RadiusMeters - cb.RadiusMeters;
            if (gap < best.SignedMeters) best = new(gap, (BikeComponent)i, (BikeComponent)j);
        }
        return best;
    }
    private static double SegmentDistance(MeterPoint a, MeterPoint b, MeterPoint c, MeterPoint d)
    {
        var u = b - a; var v = d - c; var w = c - a;
        var cross = MeterPoint.Cross(u, v);
        if (cross != 0)
        {
            var s = MeterPoint.Cross(w, v) / cross; var t = MeterPoint.Cross(w, u) / cross;
            if (s >= 0 && s <= 1 && t >= 0 && t <= 1) return 0;
        }
        return Math.Min(Math.Min(PointDistance(a, c, d), PointDistance(b, c, d)),
            Math.Min(PointDistance(c, a, b), PointDistance(d, a, b)));
    }
    private static double PointDistance(MeterPoint p, MeterPoint a, MeterPoint b)
    {
        var line = b - a; var square = MeterPoint.Dot(line, line);
        return (p - (a + line * (square == 0 ? 0 : Math.Clamp(MeterPoint.Dot(p - a, line) / square, 0, 1)))).Length;
    }
}

public static class GeometryNumerics
{
    public const double ContactDistanceMeters = 1e-6;
    public const double MinimumSeparationToleranceMeters = 1e-4;
    public const double TimeToleranceSeconds = 1e-7;
    public const double AngleToleranceRadians = 1e-9;
    public const double BroadPhasePaddingMeters = 1e-8;
    public const int MaximumSubdivisionDepth = 30;
    public const int MaximumEvaluationsPerInterval = 4096;
}

internal static class GeometryValidation
{
    internal static void Finite(double value, string name) { if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name); }
    internal static void Nonnegative(double value, string name) { Finite(value, name); if (value < 0) throw new ArgumentOutOfRangeException(name); }
    internal static void Positive(double value, string name) { Finite(value, name); if (value <= 0) throw new ArgumentOutOfRangeException(name); }
    internal static void Unit(double value, string name) { Finite(value, name); if (value < 0 || value > 1) throw new ArgumentOutOfRangeException(name); }
}
