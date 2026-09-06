namespace CoreSim;

/// <summary>
/// Immutable dimensions shared by every segment of one concrete track.
/// Surface condition and other values that can change during a meeting do not
/// belong here.
/// </summary>
public sealed record TrackGeometry
{
    private const float EqualWidthCompatibilityRelativeTolerance = 1e-5f;

    /// <summary>
    /// FIM measurement-line offset from the physical inner track edge.
    /// </summary>
    public const float InnerReferenceOffsetFromTrackEdgeMeters = 1.0f;

    /// <summary>
    /// Provisional game-geometry margin from the physical outer track edge.
    /// This is not an FIM regulatory measurement convention.
    /// </summary>
    public const float ProvisionalOuterReferenceOffsetFromTrackEdgeMeters = 1.0f;

    public float StraightLengthMeters { get; }

    /// <summary>
    /// Radius of the canonical inner measurement/reference trajectory, which is
    /// one metre from the inner track edge. This is not the kerb radius.
    /// </summary>
    public float InnerRadiusMeters { get; }
    public float StraightWidthMeters { get; }
    public float TurnWidthMeters { get; }
    public float TurnSegmentAngleRadians { get; }

    /// <summary>
    /// Synthetic compatibility geometry used by legacy constructors and
    /// geometry-free LaneModel overloads. Its 6 m widths preserve the former
    /// 1 m spacing between normalized reference positions; it is not a
    /// regulatory-size speedway track.
    /// </summary>
    public static TrackGeometry Default { get; } = new(
        straightLengthMeters: 60.0f,
        innerRadiusMeters: 24.0f,
        straightWidthMeters: 6.0f,
        turnWidthMeters: 6.0f,
        turnSegmentAngleRadians: MathF.PI / 3.0f);

    public TrackGeometry(
        float straightLengthMeters,
        float innerRadiusMeters,
        float straightWidthMeters,
        float turnWidthMeters,
        float turnSegmentAngleRadians)
    {
        StraightLengthMeters = ValidatePositiveFinite(straightLengthMeters, nameof(straightLengthMeters));
        InnerRadiusMeters = ValidatePositiveFinite(innerRadiusMeters, nameof(innerRadiusMeters));
        StraightWidthMeters = ValidatePhysicalWidth(straightWidthMeters, nameof(straightWidthMeters));
        TurnWidthMeters = ValidatePhysicalWidth(turnWidthMeters, nameof(turnWidthMeters));
        TurnSegmentAngleRadians = ValidatePositiveFinite(turnSegmentAngleRadians, nameof(turnSegmentAngleRadians));
    }

    /// <summary>
    /// Compatibility adapter for synthetic callers expressed in the former
    /// equal reference-spacing model. The supplied spacing is converted to
    /// equal physical straight/turn widths and is not stored canonically.
    /// </summary>
    public TrackGeometry(
        float straightLengthMeters,
        float innerRadiusMeters,
        float laneSpacingMeters,
        float turnSegmentAngleRadians)
        : this(
            straightLengthMeters,
            innerRadiusMeters,
            CompatibilityWidthFromReferenceSpacing(laneSpacingMeters),
            CompatibilityWidthFromReferenceSpacing(laneSpacingMeters),
            turnSegmentAngleRadians)
    {
    }

    public bool IsWithinFIMSpeedwayWidthEnvelope()
        => StraightWidthMeters >= 10f && TurnWidthMeters >= 14f;

    internal static void RequireEqualWidthCompatibility(TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var scale = MathF.Max(
            1f,
            MathF.Max(MathF.Abs(geometry.StraightWidthMeters), MathF.Abs(geometry.TurnWidthMeters)));
        if (MathF.Abs(geometry.StraightWidthMeters - geometry.TurnWidthMeters)
            > EqualWidthCompatibilityRelativeTolerance * scale)
        {
            throw new ArgumentException(
                "Overloads without SegmentType support only equal-width compatibility geometry. "
                + "Use the segment-aware overload for geometry with different straight and turn widths.",
                nameof(geometry));
        }
    }

    private static float ValidatePositiveFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Track geometry values must be positive and finite.");
        }

        return value;
    }

    private static float ValidatePhysicalWidth(float value, string parameterName)
    {
        var referenceOffsets = InnerReferenceOffsetFromTrackEdgeMeters
            + ProvisionalOuterReferenceOffsetFromTrackEdgeMeters;
        if (!float.IsFinite(value) || value <= referenceOffsets)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Track width must be finite and greater than the {referenceOffsets} m sum of reference-edge offsets.");
        }

        return value;
    }

    private static float CompatibilityWidthFromReferenceSpacing(float laneSpacingMeters)
    {
        var spacing = ValidatePositiveFinite(laneSpacingMeters, nameof(laneSpacingMeters));
        var width = TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters
            + TrackGeometry.ProvisionalOuterReferenceOffsetFromTrackEdgeMeters
            + LaneModel.MaxLane * spacing;
        if (!float.IsFinite(width))
            throw new ArgumentOutOfRangeException(nameof(laneSpacingMeters), laneSpacingMeters, "Derived compatibility width must be finite.");

        return width;
    }
}
