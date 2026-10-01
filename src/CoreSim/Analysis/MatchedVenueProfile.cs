namespace CoreSim.Analysis;

/// <summary>
/// Immutable calibration-only description of one real venue. Production physics
/// receives only the resulting TrackGeometry and never sees a venue identifier.
/// </summary>
public sealed class MatchedVenueProfile
{
    public string VenueId { get; }
    public string DisplayName { get; }
    public string SourceTrackLabel { get; }
    public int Season { get; }
    public float PublishedTrackLengthMeters { get; }
    public float StraightLengthMeters { get; }
    public float ReferenceRadiusMeters { get; }
    public float PublishedStraightWidthMeters { get; }
    public float PublishedFirstBendWidthMeters { get; }
    public float PublishedSecondBendWidthMeters { get; }
    public float ModeledSymmetricTurnWidthMeters { get; }
    public float OlderArticleBendWidthMeters { get; }
    public string SurfaceDescription { get; }
    public string SourceName { get; }
    public string CurrentSourceTitle { get; }
    public string SupportingSourceTitle { get; }
    public string ModeledTurnWidthClassification { get; }
    public string OlderArticleBendWidthClassification { get; }
    public string GeometryConfidenceNotes { get; }
    public string StartLineConfidenceNotes { get; }

    public MatchedVenueProfile(
        string venueId,
        string displayName,
        string sourceTrackLabel,
        int season,
        float publishedTrackLengthMeters,
        float straightLengthMeters,
        float referenceRadiusMeters,
        float publishedStraightWidthMeters,
        float publishedFirstBendWidthMeters,
        float publishedSecondBendWidthMeters,
        float modeledSymmetricTurnWidthMeters,
        float olderArticleBendWidthMeters,
        string surfaceDescription,
        string sourceName,
        string currentSourceTitle,
        string supportingSourceTitle,
        string modeledTurnWidthClassification,
        string olderArticleBendWidthClassification,
        string geometryConfidenceNotes,
        string startLineConfidenceNotes)
    {
        VenueId = Required(venueId, nameof(venueId));
        DisplayName = Required(displayName, nameof(displayName));
        SourceTrackLabel = Required(sourceTrackLabel, nameof(sourceTrackLabel));
        if (season <= 0) throw new ArgumentOutOfRangeException(nameof(season));
        Season = season;
        PublishedTrackLengthMeters = Positive(publishedTrackLengthMeters, nameof(publishedTrackLengthMeters));
        StraightLengthMeters = Positive(straightLengthMeters, nameof(straightLengthMeters));
        ReferenceRadiusMeters = Positive(referenceRadiusMeters, nameof(referenceRadiusMeters));
        PublishedStraightWidthMeters = Positive(publishedStraightWidthMeters, nameof(publishedStraightWidthMeters));
        PublishedFirstBendWidthMeters = Positive(publishedFirstBendWidthMeters, nameof(publishedFirstBendWidthMeters));
        PublishedSecondBendWidthMeters = Positive(publishedSecondBendWidthMeters, nameof(publishedSecondBendWidthMeters));
        ModeledSymmetricTurnWidthMeters = Positive(modeledSymmetricTurnWidthMeters, nameof(modeledSymmetricTurnWidthMeters));
        OlderArticleBendWidthMeters = Positive(olderArticleBendWidthMeters, nameof(olderArticleBendWidthMeters));
        SurfaceDescription = Required(surfaceDescription, nameof(surfaceDescription));
        SourceName = Required(sourceName, nameof(sourceName));
        CurrentSourceTitle = Required(currentSourceTitle, nameof(currentSourceTitle));
        SupportingSourceTitle = Required(supportingSourceTitle, nameof(supportingSourceTitle));
        ModeledTurnWidthClassification = Required(modeledTurnWidthClassification, nameof(modeledTurnWidthClassification));
        OlderArticleBendWidthClassification = Required(olderArticleBendWidthClassification, nameof(olderArticleBendWidthClassification));
        GeometryConfidenceNotes = Required(geometryConfidenceNotes, nameof(geometryConfidenceNotes));
        StartLineConfidenceNotes = Required(startLineConfidenceNotes, nameof(startLineConfidenceNotes));
    }

    public TrackGeometry CreateGeometry()
        => CreateGeometryWithSymmetricTurnWidth(ModeledSymmetricTurnWidthMeters);

    public TrackGeometry CreateGeometryWithSymmetricTurnWidth(float modeledSymmetricTurnWidthMeters)
        => new(
            straightLengthMeters: StraightLengthMeters,
            innerRadiusMeters: ReferenceRadiusMeters,
            straightWidthMeters: PublishedStraightWidthMeters,
            turnWidthMeters: Positive(modeledSymmetricTurnWidthMeters, nameof(modeledSymmetricTurnWidthMeters)),
            turnSegmentAngleRadians: MathF.PI / 3f);

    /// <summary>
    /// Creates the existing symmetric nine-segment standing-start topology.
    /// The split is an explicit calibration assumption, never published venue data.
    /// </summary>
    public Track CreateTrack(float startLineToFirstCornerMeters, float? modeledSymmetricTurnWidthMeters = null)
    {
        if (!float.IsFinite(startLineToFirstCornerMeters)
            || startLineToFirstCornerMeters <= 0f
            || startLineToFirstCornerMeters >= StraightLengthMeters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startLineToFirstCornerMeters),
                "The provisional start split must place the start line strictly inside the home straight.");
        }

        var finishStraightMeters = StraightLengthMeters - startLineToFirstCornerMeters;
        return new Track(new TrackSegment[]
        {
            new(0, SegmentType.Straight, startLineToFirstCornerMeters, isStandingStartSegment: true),
            new(1, SegmentType.TurnEntry),
            new(2, SegmentType.TurnMiddle),
            new(3, SegmentType.TurnExit),
            new(4, SegmentType.Straight),
            new(5, SegmentType.TurnEntry),
            new(6, SegmentType.TurnMiddle),
            new(7, SegmentType.TurnExit),
            new(8, SegmentType.Straight, finishStraightMeters),
        }, modeledSymmetricTurnWidthMeters.HasValue
            ? CreateGeometryWithSymmetricTurnWidth(modeledSymmetricTurnWidthMeters.Value)
            : CreateGeometry());
    }

    private static string Required(string value, string name)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A non-empty value is required.", name) : value;

    private static float Positive(float value, string name)
        => !float.IsFinite(value) || value <= 0f ? throw new ArgumentOutOfRangeException(name) : value;
}

public static class MatchedVenueProfiles
{
    public const float MotoarenaPrimaryStartLineToFirstCornerMeters = 35f;
    /// <summary>Frozen input for historical #39–#51 experiments, NOT the current start baseline.</summary>
    public const float MotoarenaHistorical39StartLineToFirstCornerMeters = 31f;
    public const float MotoarenaPublishedStraightWidthMeters = 12f;
    public const float MotoarenaPublishedFirstBendWidthMeters = 17f;
    public const float MotoarenaPublishedSecondBendWidthMeters = 16.2f;
    public const float MotoarenaModeledSymmetricTurnWidthMeters =
        (MotoarenaPublishedFirstBendWidthMeters + MotoarenaPublishedSecondBendWidthMeters) / 2f;
    public const float MotoarenaOlderArticleBendWidthMeters = 18f;

    public static MatchedVenueProfile Motoarena2026 { get; } = CreateMotoarenaProfile(
        "FIMConstrainedStartLineBaseline / ExactMotoarenaOffsetNotPubliclyVerified: "
        + "35 m to the first bend and 27 m from the second bend; not a measured offset or a telemetry fit.");

    /// <summary>Current geometry-only baseline. Pair with explicit StartingGrid assignments.</summary>
    public static Track CreateMotoarenaStandingStartTrack()
        => Motoarena2026.CreateTrack(MotoarenaPrimaryStartLineToFirstCornerMeters);

    internal static MatchedVenueProfile MotoarenaHistorical39 { get; } = CreateMotoarenaProfile(
        "ProvisionalStartLineSplit: no verified start-line offset is available; the primary fixture uses 31 m / 31 m and reports 25/37 and 37/25 sensitivity cases.");

    private static MatchedVenueProfile CreateMotoarenaProfile(string startLineConfidenceNotes) => new(
        venueId: "pge-2026-motoarena-torun",
        displayName: "Motoarena im. Mariana Rosego — Toruń",
        sourceTrackLabel: "Motoarena im. Mariana Rosego",
        season: 2026,
        publishedTrackLengthMeters: 318f,
        straightLengthMeters: 62f,
        referenceRadiusMeters: 31f,
        publishedStraightWidthMeters: MotoarenaPublishedStraightWidthMeters,
        publishedFirstBendWidthMeters: MotoarenaPublishedFirstBendWidthMeters,
        publishedSecondBendWidthMeters: MotoarenaPublishedSecondBendWidthMeters,
        modeledSymmetricTurnWidthMeters: MotoarenaModeledSymmetricTurnWidthMeters,
        olderArticleBendWidthMeters: MotoarenaOlderArticleBendWidthMeters,
        surfaceDescription: "granite",
        sourceName: "Speedway Ekstraliga",
        currentSourceTitle: "PRES GRUPA DEWELOPERSKA Toruń — info — 2026",
        supportingSourceTitle: "Czy podczas PGE IMME im. Zenona Plecha padnie nowy rekord toru?",
        modeledTurnWidthClassification: "DerivedSymmetricWidthApproximationFromCurrentPublishedBendWidths",
        olderArticleBendWidthClassification: "OlderArticleReferenceOnly",
        geometryConfidenceNotes:
            "Current 2026 published widths are 12 m on both straights and 17.0/16.2 m on the first/second bends. "
            + "The model uses their 16.6 m arithmetic mean as a DerivedSymmetricWidthApproximationFromCurrentPublishedBendWidths. "
            + "This is a SymmetricGeometryApproximation. "
            + "The 31 m radius remains an ExternalPublishedRadius / MeasurementConventionNotExplicitlyVerified reference-radius approximation. "
            + "This preserves aggregate full-lap lateral path contribution in the symmetric model, not local corner radius, safe speed, banking, or asymmetry.",
        startLineConfidenceNotes: startLineConfidenceNotes);
}
