namespace CoreSim;

/// <summary>Physical starting fields, ordered from the inner edge to the outer edge.</summary>
public enum StartingGate { A, B, C, D }

/// <summary>Offsets are measured from the physical inner edge, not a racing reference line.</summary>
public sealed record StartingGateBounds
{
    public StartingGate Gate { get; }
    public float LowerPhysicalOffsetMeters { get; }
    public float UpperPhysicalOffsetMeters { get; }
    public float CenterPhysicalOffsetMeters => LowerPhysicalOffsetMeters + NominalWidthMeters / 2f;
    public float NominalWidthMeters => UpperPhysicalOffsetMeters - LowerPhysicalOffsetMeters;

    internal StartingGateBounds(StartingGate gate, float lower, float upper)
        => (Gate, LowerPhysicalOffsetMeters, UpperPhysicalOffsetMeters) = (gate, lower, upper);

    // Shared edges belong to the outer field; only D includes the physical outer edge.
    public bool Contains(float physicalOffsetMeters)
        => physicalOffsetMeters >= LowerPhysicalOffsetMeters
           && (physicalOffsetMeters < UpperPhysicalOffsetMeters
               || (Gate == StartingGate.D && physicalOffsetMeters == UpperPhysicalOffsetMeters));
}

/// <summary>Four equal fields over the FULL physical starting-straight width.</summary>
public sealed class StartingGateGeometry
{
    public const int FieldCount = 4;
    public const float DividingLineWidthMeters = 0.05f;
    public const float BackwardMarkingLengthMeters = 1f;
    public float PhysicalWidthMeters { get; }
    public IReadOnlyList<StartingGateBounds> Fields { get; }
    public IReadOnlyList<float> InternalDividingLineOffsetsMeters { get; }

    public StartingGateGeometry(float physicalWidthMeters)
    {
        if (!float.IsFinite(physicalWidthMeters) || physicalWidthMeters / FieldCount <= 0f)
            throw new ArgumentOutOfRangeException(nameof(physicalWidthMeters));
        PhysicalWidthMeters = physicalWidthMeters;
        var width = physicalWidthMeters / FieldCount;
        Fields = Array.AsReadOnly(Enumerable.Range(0, FieldCount)
            .Select(index => new StartingGateBounds((StartingGate)index,
                index * width, (index + 1) * width)).ToArray());
        InternalDividingLineOffsetsMeters = Array.AsReadOnly(Fields.Skip(1)
            .Select(field => field.LowerPhysicalOffsetMeters).ToArray());
    }

    public StartingGateBounds Field(StartingGate gate)
    {
        if (!Enum.IsDefined(gate)) throw new ArgumentOutOfRangeException(nameof(gate));
        return Fields[(int)gate];
    }

    /// <summary>Explicit physical-edge -> inner-reference -> racing-coordinate transform.</summary>
    public static float CenterLateralPosition(StartingGate gate, TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var center = new StartingGateGeometry(geometry.StraightWidthMeters).Field(gate).CenterPhysicalOffsetMeters;
        // Do not clamp: narrow synthetic tracks can have centers outside the racing-reference span.
        return LaneModel.LateralPositionFromPhysicalOffsetMeters(
            center - TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters, SegmentType.Straight, geometry);
    }
}
