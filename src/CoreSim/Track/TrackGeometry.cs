namespace CoreSim;

/// <summary>
/// Immutable dimensions shared by every segment of one concrete track.
/// Surface condition and other values that can change during a meeting do not
/// belong here.
/// </summary>
public sealed record TrackGeometry
{
    public float StraightLengthMeters { get; }
    public float InnerRadiusMeters { get; }
    public float LaneSpacingMeters { get; }
    public float TurnSegmentAngleRadians { get; }

    /// <summary>Geometry used by compatibility constructors and LaneModel overloads.</summary>
    public static TrackGeometry Default { get; } = new(
        straightLengthMeters: 60.0f,
        innerRadiusMeters: 24.0f,
        laneSpacingMeters: 1.0f,
        turnSegmentAngleRadians: MathF.PI / 3.0f);

    public TrackGeometry(
        float straightLengthMeters,
        float innerRadiusMeters,
        float laneSpacingMeters,
        float turnSegmentAngleRadians)
    {
        StraightLengthMeters = ValidatePositiveFinite(straightLengthMeters, nameof(straightLengthMeters));
        InnerRadiusMeters = ValidatePositiveFinite(innerRadiusMeters, nameof(innerRadiusMeters));
        LaneSpacingMeters = ValidatePositiveFinite(laneSpacingMeters, nameof(laneSpacingMeters));
        TurnSegmentAngleRadians = ValidatePositiveFinite(turnSegmentAngleRadians, nameof(turnSegmentAngleRadians));
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
}
