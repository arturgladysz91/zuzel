using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public enum FreeTrajectoryValidity
{
    Valid,
    TrackBoundary,
    SelfIntersection,
    NonSmoothGeometry,
    LateralExecutionConstraint,
    CornerControlPathDeparture,
    CornerControlCrash,
    StraightRepositionConstraint,
}

public sealed record FreeTrajectoryCandidate(
    string SeedFamily,
    IReadOnlyList<float> ControlOffsetsMeters)
{
    public string Id => FreeContinuousRacingTrajectoryGeometryExperiment.CandidateId(ControlOffsetsMeters);
}

public sealed record FreeTrajectoryGeometryPoint(
    float Progress,
    float X,
    float Y,
    float LateralOffsetMeters,
    float LateralPosition,
    float CurvaturePerMeter,
    float LocalRadiusMeters,
    float HeadingDeltaRadians,
    float CumulativeDistanceMeters);

public sealed record FreeTrajectoryProfilePoint(
    float Progress,
    float LateralOffsetMeters,
    float LateralPosition,
    float SpeedMetersPerSecond,
    float LocalSafeSpeedMetersPerSecond,
    float LocalRadiusMeters,
    float CurvaturePerMeter,
    float LateralAccelerationProxyMetersPerSecondSquared,
    float HeadingDeltaRadians,
    bool CorrectionActive,
    float DriveAvailability,
    float NetLongitudinalAccelerationMetersPerSecondSquared,
    float LookaheadTargetMetersPerSecond,
    SegmentOutcome CornerControlOutcome)
{
    public float TurningLossPowerWatts { get; init; }
    public float LongitudinalDriveRemainingNewtons { get; init; }
}

public sealed record TurningDemandConstraint(
    float Progress,
    float DistanceMeters,
    float LocalRadiusMeters,
    float CurvaturePerMeter,
    float SettledCapabilityMetersPerSecond)
{
    public bool IsCanonicalBaseline { get; init; }
    public bool IsBindingEnvelopePoint { get; init; }
    public float CanonicalBaselineSpeedMetersPerSecond { get; init; }
    public float LocalSettledCapabilityMetersPerSecond { get; init; }
    public float SettledCapabilitySquaredDeltaMetersSquaredPerSecondSquared { get; init; }
    public float BackwardReachableEntryTargetMetersPerSecond { get; init; }
}

public sealed record TurningDemandEnvelopeComparison(
    float Progress,
    float ReducedLookaheadTargetMetersPerSecond,
    float BruteForceLookaheadTargetMetersPerSecond);

public sealed record TurningConstraintTrace(
    float ConstraintProgress,
    float LocalRadiusMeters,
    float SettledCapabilityMetersPerSecond,
    float LookaheadTargetAtEntryMetersPerSecond,
    float ActualSpeedMetersPerSecond,
    SegmentOutcome Outcome,
    float DistanceToConstraintMeters);

public sealed class FreeTrajectoryPath
{
    private readonly NaturalCubicSpline spline;
    private readonly FreeTrajectoryGeometryPoint[] samples;

    public TrackGeometry Geometry { get; }
    public IReadOnlyList<FreeTrajectoryGeometryPoint> Samples { get; }
    public float TotalLengthMeters { get; }
    public float MaximumSampleSpacingMeters { get; }

    internal FreeTrajectoryPath(
        TrackGeometry geometry,
        NaturalCubicSpline spline,
        FreeTrajectoryGeometryPoint[] samples,
        float totalLengthMeters,
        float maximumSampleSpacingMeters)
    {
        Geometry = geometry;
        this.spline = spline;
        this.samples = samples;
        Samples = Array.AsReadOnly(samples);
        TotalLengthMeters = totalLengthMeters;
        MaximumSampleSpacingMeters = maximumSampleSpacingMeters;
    }

    public FreeTrajectoryGeometryPoint PointAt(float progress)
    {
        var raw = FreeContinuousTrajectoryGeometry.PointAt(spline, Geometry, progress);
        return raw with { CumulativeDistanceMeters = DistanceAtProgress(progress) };
    }

    public float DistanceAtProgress(float progress)
    {
        ValidateProgress(progress);
        var scaled = progress * (samples.Length - 1);
        var lower = Math.Min(samples.Length - 2, (int)MathF.Floor(scaled));
        var fraction = scaled - lower;
        return samples[lower].CumulativeDistanceMeters
            + (samples[lower + 1].CumulativeDistanceMeters - samples[lower].CumulativeDistanceMeters) * fraction;
    }

    public float ProgressAtDistance(float distanceMeters)
    {
        if (!float.IsFinite(distanceMeters) || distanceMeters < 0f || distanceMeters > TotalLengthMeters)
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        if (distanceMeters <= 0f) return 0f;
        if (distanceMeters >= TotalLengthMeters) return 1f;
        var lower = 0;
        var upper = samples.Length - 1;
        while (upper - lower > 1)
        {
            var middle = lower + (upper - lower) / 2;
            if (samples[middle].CumulativeDistanceMeters < distanceMeters) lower = middle;
            else upper = middle;
        }
        var span = samples[upper].CumulativeDistanceMeters - samples[lower].CumulativeDistanceMeters;
        var t = span <= 0f ? 0f : (distanceMeters - samples[lower].CumulativeDistanceMeters) / span;
        return (lower + t) / (samples.Length - 1);
    }

    private static void ValidateProgress(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress > 1f)
            throw new ArgumentOutOfRangeException(nameof(progress));
    }
}

public static class FreeContinuousTrajectoryGeometry
{
    // 2048 Cartesian chords keep even aggressive bounded splines comfortably below 0.5 m.
    public const int CartesianSampleDivisions = 2048;

    public static FreeTrajectoryPath Build(IReadOnlyList<float> controlOffsetsMeters, TrackGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(controlOffsetsMeters);
        ArgumentNullException.ThrowIfNull(geometry);
        if (controlOffsetsMeters.Count < FreeContinuousRacingTrajectoryGeometryExperiment.ControlStationCount)
            throw new ArgumentException("At least eleven control stations are required.", nameof(controlOffsetsMeters));
        var width = LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, geometry);
        if (controlOffsetsMeters.Any(value => !float.IsFinite(value) || value < 0f || value > width))
            throw new ArgumentOutOfRangeException(nameof(controlOffsetsMeters), "Control offsets must stay inside the usable width.");

        var spline = new NaturalCubicSpline(controlOffsetsMeters);
        var samples = new FreeTrajectoryGeometryPoint[CartesianSampleDivisions + 1];
        var total = 0f;
        var maximumSpacing = 0f;
        for (var index = 0; index <= CartesianSampleDivisions; index++)
        {
            var progress = (float)index / CartesianSampleDivisions;
            var point = PointAt(spline, geometry, progress);
            if (!float.IsFinite(point.LateralOffsetMeters)
                || point.LateralOffsetMeters < -1e-5f
                || point.LateralOffsetMeters > width + 1e-5f)
            {
                throw new InvalidDataException("TrackBoundary");
            }
            if (index > 0)
            {
                var prior = samples[index - 1];
                var spacing = MathF.Sqrt((point.X - prior.X) * (point.X - prior.X)
                    + (point.Y - prior.Y) * (point.Y - prior.Y));
                if (!float.IsFinite(spacing) || spacing <= 0f)
                    throw new InvalidDataException("NonSmoothGeometry");
                total += spacing;
                maximumSpacing = MathF.Max(maximumSpacing, spacing);
            }
            samples[index] = point with { CumulativeDistanceMeters = total };
        }
        if (maximumSpacing > .5f + 1e-4f)
            throw new InvalidDataException("NonSmoothGeometry");

        var intersectionProbe = samples.Where((_, index) => index % 16 == 0)
            .Select(point => (point.X, point.Y)).ToArray();
        if (HasSelfIntersection(intersectionProbe))
            throw new InvalidDataException("SelfIntersection");
        return new FreeTrajectoryPath(geometry, spline, samples, total, maximumSpacing);
    }

    public static float CurvatureFromThreePoints(
        (float X, float Y) first,
        (float X, float Y) second,
        (float X, float Y) third)
    {
        var a = Distance(first, second);
        var b = Distance(second, third);
        var c = Distance(third, first);
        var twiceArea = Math.Abs((double)(second.X - first.X) * (third.Y - first.Y)
            - (double)(second.Y - first.Y) * (third.X - first.X));
        var denominator = (double)a * b * c;
        return denominator <= 1e-12d ? 0f : (float)(2d * twiceArea / denominator);
    }

    public static bool HasSelfIntersection(IReadOnlyList<(float X, float Y)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        for (var first = 0; first + 1 < points.Count; first++)
        for (var second = first + 2; second + 1 < points.Count; second++)
        {
            if (Intersects(points[first], points[first + 1], points[second], points[second + 1])) return true;
        }
        return false;
    }

    internal static FreeTrajectoryGeometryPoint PointAt(
        NaturalCubicSpline spline,
        TrackGeometry geometry,
        float progress)
    {
        var value = spline.Evaluate(progress);
        var radius = geometry.InnerRadiusMeters + value.Value;
        var theta = MathF.PI * progress;
        var cosine = (float)Math.Cos(theta);
        var sine = (float)Math.Sin(theta);
        var dr = value.FirstDerivative / MathF.PI;
        var d2r = value.SecondDerivative / (MathF.PI * MathF.PI);
        var dx = dr * cosine - radius * sine;
        var dy = dr * sine + radius * cosine;
        var d2x = d2r * cosine - 2f * dr * sine - radius * cosine;
        var d2y = d2r * sine + 2f * dr * cosine - radius * sine;
        var derivativeSquared = dx * dx + dy * dy;
        var curvature = derivativeSquared <= 1e-12f
            ? 0f
            : MathF.Abs(dx * d2y - dy * d2x) /
              (float)Math.Pow(derivativeSquared, 1.5d);
        var localRadius = curvature <= 1e-6f ? 1_000_000f : 1f / curvature;
        var heading = (float)Math.Atan2(dy, dx);
        var referenceHeading = (float)Math.Atan2(cosine, -sine);
        var headingDelta = NormalizeAngle(heading - referenceHeading);
        var lateralPosition = value.Value
            / LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, geometry) * LaneModel.MaxLane;
        return new FreeTrajectoryGeometryPoint(progress, radius * cosine, radius * sine,
            value.Value, lateralPosition, curvature, localRadius, headingDelta, 0f);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= 2f * MathF.PI;
        while (angle < -MathF.PI) angle += 2f * MathF.PI;
        return angle;
    }

    private static float Distance((float X, float Y) a, (float X, float Y) b)
        => MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static bool Intersects(
        (float X, float Y) a,
        (float X, float Y) b,
        (float X, float Y) c,
        (float X, float Y) d)
    {
        static double Cross((float X, float Y) p, (float X, float Y) q, (float X, float Y) r)
            => (double)(q.X - p.X) * (r.Y - p.Y) - (double)(q.Y - p.Y) * (r.X - p.X);
        var abC = Cross(a, b, c);
        var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a);
        var cdB = Cross(c, d, b);
        return abC * abD < -1e-12d && cdA * cdB < -1e-12d;
    }
}

internal sealed class NaturalCubicSpline
{
    private readonly float[] values;
    private readonly double[] second;
    private readonly double step;

    internal NaturalCubicSpline(IReadOnlyList<float> controls)
    {
        if (controls.Count < 2) throw new ArgumentException("Two controls are required.", nameof(controls));
        values = controls.ToArray();
        second = new double[values.Length];
        step = 1d / (values.Length - 1);
        if (values.Length <= 2) return;
        var count = values.Length - 2;
        var lower = new double[count];
        var diagonal = new double[count];
        var upper = new double[count];
        var right = new double[count];
        for (var index = 0; index < count; index++)
        {
            lower[index] = index == 0 ? 0d : step;
            diagonal[index] = 4d * step;
            upper[index] = index == count - 1 ? 0d : step;
            var knot = index + 1;
            right[index] = 6d * (((double)values[knot + 1] - values[knot]) / step
                - ((double)values[knot] - values[knot - 1]) / step);
        }
        for (var index = 1; index < count; index++)
        {
            var factor = lower[index] / diagonal[index - 1];
            diagonal[index] -= factor * upper[index - 1];
            right[index] -= factor * right[index - 1];
        }
        var solved = new double[count];
        solved[^1] = right[^1] / diagonal[^1];
        for (var index = count - 2; index >= 0; index--)
            solved[index] = (right[index] - upper[index] * solved[index + 1]) / diagonal[index];
        for (var index = 0; index < count; index++) second[index + 1] = solved[index];
    }

    internal (float Value, float FirstDerivative, float SecondDerivative) Evaluate(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress > 1f)
            throw new ArgumentOutOfRangeException(nameof(progress));
        var scaled = progress * (values.Length - 1);
        var interval = Math.Min(values.Length - 2, (int)MathF.Floor(scaled));
        var local = (double)progress - interval * step;
        var a = values[interval];
        var b = ((double)values[interval + 1] - values[interval]) / step
            - step * (2d * second[interval] + second[interval + 1]) / 6d;
        var c = second[interval] / 2d;
        var d = (second[interval + 1] - second[interval]) / (6d * step);
        return ((float)(a + b * local + c * local * local + d * local * local * local),
            (float)(b + 2d * c * local + 3d * d * local * local),
            (float)(2d * c + 6d * d * local));
    }
}

public sealed record FreeTrajectoryEvaluation(
    FreeTrajectoryCandidate Candidate,
    FreeTrajectoryValidity Validity,
    string InvalidReason,
    FreeTrajectoryPath? Path,
    IReadOnlyList<FreeTrajectoryProfilePoint> Profile,
    float CornerTimeSeconds,
    float FollowingStraightTimeSeconds,
    float SectorTimeSeconds,
    float ExitSpeedMetersPerSecond,
    float SpeedTenMetersAfterCornerMetersPerSecond,
    float SpeedTwentyMetersAfterCornerMetersPerSecond,
    float SpeedFortyMetersAfterCornerMetersPerSecond,
    float EndStraightSpeedMetersPerSecond,
    float MinimumSpeedMetersPerSecond,
    float MinimumSpeedProgress,
    float CorrectionDistanceMeters,
    float CorrectionTimeSeconds,
    float PeakLateralAccelerationProxyMetersPerSecondSquared,
    float TimeWeightedMeanRadiusMeters,
    float MinimumLateralExecutionHeadroomMeters,
    float MinimumLateralPositionMeters,
    float MinimumLateralPositionProgress,
    float MaximumLateralPositionMeters,
    float MaximumCurvaturePerMeter,
    float MaximumCurvatureProgress,
    float MinimumRadiusMeters,
    float MinimumRadiusProgress,
    float MedianRadiusMeters,
    float MaximumFiniteRadiusMeters,
    float MeanAbsoluteCurvaturePerMeter,
    float MinimumHeadingDeltaRadians,
    float MaximumHeadingDeltaRadians,
    float SustainedCurvatureDeclineProgress,
    float MaterialDriveIncreaseProgress)
{
    public bool IsValid => Validity == FreeTrajectoryValidity.Valid;
    public bool IsNonConstant => Candidate.ControlOffsetsMeters.Max() - Candidate.ControlOffsetsMeters.Min() > .01f;
    public string PathFingerprint => IsValid
        ? string.Join(",", Profile.Select(point => point.LateralOffsetMeters.ToString("0.00", CultureInfo.InvariantCulture)))
        : string.Empty;
    public IReadOnlyList<TurningDemandConstraint> TurningDemandConstraints { get; init; } =
        Array.Empty<TurningDemandConstraint>();
    public IReadOnlyList<TurningConstraintTrace> TurningConstraintTrace { get; init; } =
        Array.Empty<TurningConstraintTrace>();
    public float PeakSettledOverspeedRatio { get; init; }
    public float PeakEnvelopeOverspeedRatio { get; init; }
    public float PeakSettledOverspeedProgress { get; init; }
    public float PeakEnvelopeOverspeedProgress { get; init; }
    public int BrakeStepCount { get; init; }
    public int RunWideInvalidationCount { get; init; }
    public int CrashInvalidationCount { get; init; }
    public float FirstCorrectionProgress { get; init; } = 1f;
    public float NextEntryLateralMeters { get; init; }
    public float StraightRepositionDistanceMeters { get; init; }
    public float StraightRepositionLateralHeadroomMeters { get; init; }
    public double TurningLossEnergyJoules { get; init; }
    public float PeakTurningLossPowerWatts { get; init; }
}

public sealed record VariableCurvatureSanityControl(
    string Name,
    FreeTrajectoryEvaluation Evaluation,
    bool SemanticsPassed);

public sealed record ConstantPathReplayComparison(
    string Id,
    int Lane,
    float ProductionCornerTimeSeconds,
    float ExperimentalCornerTimeSeconds,
    float ProductionStraightTimeSeconds,
    float ExperimentalStraightTimeSeconds,
    float ProductionSectorTimeSeconds,
    float ExperimentalSectorTimeSeconds,
    float SectorDeltaSeconds,
    float ExitSpeedDeltaMetersPerSecond,
    float MaximumCornerSpeedProfileDeltaMetersPerSecond,
    bool Passed);

public sealed record FreeTrajectorySeedFamilyResult(
    string Family,
    FreeTrajectoryEvaluation Best);

public sealed record FreeTrajectoryRefinedStartDiagnostic(
    string Family,
    FreeTrajectoryEvaluation Final,
    IReadOnlyList<float> RefinementStepImprovementsSeconds,
    float LastRoundImprovementSeconds,
    float ResidualSingleCoordinateImprovementSeconds,
    float ResidualExpandedSingleCoordinateImprovementSeconds,
    float ResidualAdjacentPairImprovementSeconds,
    bool ChangedInLastRound,
    int AdditionalClosureMoves,
    int ClosurePasses,
    bool Stable);

public sealed record FreeTrajectorySearchResult(
    IReadOnlyList<FreeTrajectoryEvaluation> TopTwenty,
    IReadOnlyList<FreeTrajectorySeedFamilyResult> FamilyResults,
    int CandidatesEvaluated,
    int ValidCandidates,
    int InvalidGeometryCandidates,
    int InvalidLateralExecutionCandidates,
    int RefinementRounds,
    int SameBasinFamilyCount,
    int WithinToleranceFamilyCount,
    int DistinctTopShapeCount,
    bool SearchConvergenceUncertain)
{
    public int StructuredStartCount { get; init; }
    public int LowDiscrepancyStartCount { get; init; }
    public int StartsGenerated { get; init; }
    public int ValidInitialStarts { get; init; }
    public int InvalidInitialGeometry { get; init; }
    public int InvalidInitialLateral { get; init; }
    public int InvalidInitialCornerControl { get; init; }
    public int InvalidInitialStraightReposition { get; init; }
    public int InvalidCornerControlDepartureCandidates { get; init; }
    public int InvalidCrashCandidates { get; init; }
    public int InvalidStraightRepositionCandidates { get; init; }
    public IReadOnlyList<float> FinalRefinementStepsMeters { get; init; } = Array.Empty<float>();
    public bool ObjectiveConvergence { get; init; }
    public bool GeometryConvergence { get; init; }
    public bool MultipleNearOptimalBasins { get; init; }
    public bool LocalSearchConverged { get; init; }
    public float LocalPerturbationImprovementSeconds { get; init; }
    public float PairPerturbationImprovementSeconds { get; init; }
    public int RefinedStartCount { get; init; }
    public FreeTrajectoryEvaluation ArchivedUnclosedBest { get; init; } = null!;
    public float InitialLocalPerturbationImprovementSeconds { get; init; }
    public float InitialPairPerturbationImprovementSeconds { get; init; }
    public int AdditionalAcceptedLocalMoves { get; init; }
    public IReadOnlyList<FreeTrajectoryRefinedStartDiagnostic> TopThreeRefinementDiagnostics { get; init; }
        = Array.Empty<FreeTrajectoryRefinedStartDiagnostic>();
    public IReadOnlyList<FreeTrajectoryRefinedStartDiagnostic> ScheduledTopThreeRefinementDiagnostics
        { get; init; } = Array.Empty<FreeTrajectoryRefinedStartDiagnostic>();
    public bool ScheduledTopThreeStableByLastRound { get; init; }
    public bool TopThreeStable { get; init; }
}

public sealed record RepeatedTrajectoryLapResult(
    float FlyingLapTimeSeconds,
    float StableCornerEntrySpeedMetersPerSecond,
    int Iterations,
    bool Converged)
{
    public float MaximumSpeedMetersPerSecond { get; init; }
    public float AverageSpeedMetersPerSecond { get; init; }
}

public sealed record CornerSpeedSensitivityControl(
    string Name,
    string ShapeDescription,
    bool IsDynamic,
    FreeTrajectoryCandidate Candidate);

public sealed record CornerSpeedSensitivityObservation(
    string Name,
    string ShapeDescription,
    bool IsDynamic,
    FreeTrajectoryEvaluation Evaluation);

public sealed record CornerSpeedSensitivityPoint(
    float ReferenceTurnSpeedMetersPerSecond,
    float SettledCapabilityAt31MetersPerSecond,
    IReadOnlyList<CornerSpeedSensitivityObservation> Observations,
    CornerSpeedSensitivityObservation BestConstant,
    CornerSpeedSensitivityObservation E0A1X1,
    CornerSpeedSensitivityObservation BestControlledDynamic,
    RepeatedTrajectoryLapResult ConstantInnerRepeatedLap);

public sealed record CornerSpeedCapabilitySensitivityResult(
    IReadOnlyList<float> ReferenceSweepMetersPerSecond,
    IReadOnlyList<CornerSpeedSensitivityControl> Controls,
    IReadOnlyList<CornerSpeedSensitivityPoint> Points,
    float? ApproximateCrossoverReferenceSpeedMetersPerSecond,
    bool GlobalSpeedGuardrailBreaksAtCrossover,
    float? FirstGlobalSpeedGuardrailBreakReferenceMetersPerSecond,
    bool GlobalSpeedGuardrailBreaksBeforeCrossover,
    string Classification,
    string Interpretation,
    bool MateriallyControlsLineEconomics,
    string RaisingCapabilityAloneLooksLikeCorrectFix);

public sealed record TurningDemandContinuityObservation(
    string Name,
    float TargetCurvatureRangePerMeter,
    float PerturbationScaleMeters,
    IReadOnlyList<float> ControlOffsetsMeters,
    float ActualCurvatureRangePerMeter,
    FreeTrajectoryEvaluation Evaluation);

public sealed record TurningDemandContinuityResult(
    IReadOnlyList<TurningDemandContinuityObservation> Observations,
    float OldTolerancePerMeter,
    float BelowAboveSectorDeltaSeconds,
    float BelowAboveMaximumSpeedProfileDeltaMetersPerSecond,
    float SmallestPerturbationCornerDeltaFromConstantSeconds,
    float SmallestPerturbationSectorDeltaFromConstantSeconds,
    float SmallestPerturbationExitSpeedDeltaFromConstantMetersPerSecond,
    float SmallestPerturbationMaximumSpeedProfileDeltaFromConstantMetersPerSecond,
    float MaximumNearConstantSectorDeltaFromConstantSeconds,
    float TwiceToleranceSupplementalRestrictionMetersPerSecond,
    bool CanonicalBaselineAlwaysPresent,
    bool NearConstantPenaltyGatePassed,
    bool DifferentialRestrictionConverges,
    bool ConvergesToConstant,
    bool OldThresholdContinuityPassed);

public sealed class FreeContinuousRacingTrajectoryGeometryExperimentResult
{
    public string BaseSha { get; init; } = string.Empty;
    public TrackGeometry Geometry { get; init; } = TrackGeometry.Default;
    public float EntrySpeedMetersPerSecond { get; init; }
    public double ConstantInnerProductionFlyingLapSeconds { get; init; }
    public double E0A1X0ProductionFlyingLapSeconds { get; init; }
    public double E0A1X1ProductionFlyingLapSeconds { get; init; }
    public bool Reproduces46 { get; init; }
    public IReadOnlyList<ConstantPathReplayComparison> ConstantReplay { get; init; } = Array.Empty<ConstantPathReplayComparison>();
    public bool ConstantReplayValidationPassed { get; init; }
    public IReadOnlyList<FreeTrajectoryEvaluation> Controls { get; init; } = Array.Empty<FreeTrajectoryEvaluation>();
    public IReadOnlyList<VariableCurvatureSanityControl> SanityControls { get; init; } =
        Array.Empty<VariableCurvatureSanityControl>();
    public FreeTrajectoryEvaluation OldWinnerAfterRepair { get; init; } = null!;
    public FreeTrajectorySearchResult Search { get; init; } = null!;
    public FreeTrajectoryEvaluation BestFound { get; init; } = null!;
    public FreeTrajectoryEvaluation BestConstant { get; init; } = null!;
    public FreeTrajectoryEvaluation ConstantInner { get; init; } = null!;
    public RepeatedTrajectoryLapResult RepeatedBestFound { get; init; } = null!;
    public RepeatedTrajectoryLapResult RepeatedBestConstant { get; init; } = null!;
    public CornerSpeedCapabilitySensitivityResult CornerSpeedSensitivity { get; init; } = null!;
    public TurningDemandContinuityResult TurningDemandContinuity { get; init; } = null!;
    public FreeTrajectoryEvaluation PriorReviewedWinnerAfterContinuityRepair { get; init; } = null!;
    public FreeTrajectoryEvaluation PriorReviewedBroadLateWinnerAfterEnvelopeRepair { get; init; } = null!;
    public bool FreeTrajectoryBeatsBestConstant { get; init; }
    public bool FreeTrajectoryTiesBestConstant { get; init; }
    public bool ExitSpeedTradeoffObserved { get; init; }
    public bool ContinuousCurvatureChangesRanking { get; init; }
    public bool LateralExecutionConstraintBinding { get; init; }
    public bool VariableCurvatureEnvelopeConsistent { get; init; }
    public bool LocalCornerControlConstraintActive { get; init; }
    public bool OldWinnerStillValidAfterRepair { get; init; }
    public bool OldWinnerRejectedAfterRepair { get; init; }
    public bool StraightRepositionConstraintActive { get; init; }
    public bool VariableCurvatureReplayHealthy { get; init; }
    public bool FreeTrajectoryGeometrySignalHealthy { get; init; }
    public string Classification { get; init; } = string.Empty;
    public string FirstActualBottleneck { get; init; } = string.Empty;
    public string RecommendedSubsystem { get; init; } = string.Empty;
    public string DecisionCase { get; init; } = string.Empty;
}

/// <summary>
/// Analysis-only #47 search and geometry replay. It does not participate in HeatSimulator.
/// </summary>
public static partial class FreeContinuousRacingTrajectoryGeometryExperiment
{
    private sealed record RefinedStartState(
        string Family,
        FreeTrajectoryEvaluation Evaluation,
        IReadOnlyList<float> StepImprovements,
        float LastRoundImprovement,
        bool ChangedInLastRound,
        int AdditionalClosureMoves = 0,
        int ClosurePasses = 0);

    public const string BaseMainSha = "4abc9700dbc794802e5afb9364a718bb7ec57bd6";
    public const int ControlStationCount = 11;
    public const float PathSampleResolutionLimitMeters = .5f;
    public const float ConstantReplayToleranceSeconds = .005f;
    public const float ConstantReplaySpeedToleranceMetersPerSecond = .005f;
    public const float GameplayTieToleranceSeconds = .02f;
    public const int StructuredStartCount = 10;
    public const int LowDiscrepancyStartCount = 96;
    public const int RefinedStartCount = 48;
    public const int RefinementRounds = 6;
    public const int CoordinateSweepsPerRound = 2;
    public const float LocalSearchImprovementToleranceSeconds = .002f;
    public const float ConstantCurvatureTolerancePerMeter = 1e-5f;
    public const float RealMotoarenaMaximumSpeedGuardrailKilometersPerHour = 111f;
    public const float RealMotoarenaAverageSpeedGuardrailMetersPerSecond = 22.38f;
    public const float RealMotoarenaFlyingLapGuardrailSeconds = 14.71f;
    public const float ClearlyBrokenGlobalSpeedFraction = .15f;
    public static IReadOnlyList<float> CornerSpeedSensitivityReferenceSweepMetersPerSecond { get; } =
        Array.AsReadOnly(new[] { 19f, 20f, 21f, 22f, 23f });
    public static IReadOnlyList<float> RefinementStepsMeters { get; } =
        Array.AsReadOnly(new[] { 2f, 1f, .5f, .25f, .125f, .0625f });

    private static readonly TrackSurfaceState UniformSurface = CalibrationScenarioCatalog.Baseline.Surface;
    private static readonly RiderSkills Balanced = RiderSkills.Balanced;
    private static readonly BikeSetup Neutral = BikeSetup.Neutral;

    public static FreeContinuousRacingTrajectoryGeometryExperimentResult Run()
    {
        var track = CreateTrack();
        var constantInner46 = DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 0, 0));
        var e0a1x0 = DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 1, 0));
        var e0a1x1 = DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 1, 1));
        var reproduces46 = Math.Abs(constantInner46.FlyingLapMedianSeconds - 13.397707d) <= 1e-5d
            && Math.Abs(e0a1x0.FlyingLapMedianSeconds - 13.507465d) <= 1e-5d
            && Math.Abs(e0a1x1.FlyingLapMedianSeconds - 13.581989d) <= 1e-5d;
        if (!reproduces46)
            throw new InvalidOperationException("The merged #46 production controls were not reproduced.");

        var entrySpeed = constantInner46.CornerEntrySpeedMetersPerSecond;
        var evaluator = new ExperimentalGeometryReplay(track.Geometry, entrySpeed);
        var constantReplay = Enumerable.Range(0, LaneModel.LanesCount)
            .Select(lane => CompareConstantPath(evaluator, track.Geometry, lane, entrySpeed)).ToArray();
        var replayPassed = constantReplay.All(item => item.Passed);
        if (!replayPassed)
            throw new InvalidOperationException("ConstantReplayMismatch");

        var controls = BuildControls(evaluator, track.Geometry, e0a1x0, e0a1x1);
        var sanityControls = BuildSanityControls(evaluator);
        var variableEnvelopeConsistent = sanityControls.All(item => item.SemanticsPassed);
        var continuity = RunTurningDemandContinuityDiagnostic(track.Geometry, entrySpeed);
        var oldWinnerBase = .25f * LaneModel.UsableRacingWidthMeters(
            SegmentType.TurnMiddle, track.Geometry) - 2f - 1f;
        var oldWinner = evaluator.Evaluate(Candidate("Pre-repair winner", new[]
        {
            oldWinnerBase, oldWinnerBase - .5f, oldWinnerBase - .5f, oldWinnerBase,
            oldWinnerBase - .5f, oldWinnerBase - .5f, oldWinnerBase, oldWinnerBase - .25f,
            oldWinnerBase - .5f, oldWinnerBase - .5f, oldWinnerBase - .25f,
        }));
        var priorReviewedWinner = evaluator.Evaluate(PriorReviewedWinnerCandidate(track.Geometry));
        var priorReviewedBroadLateWinner = evaluator.Evaluate(PriorReviewedBroadLateWinnerCandidate());
        var search = RunSearch(evaluator, track.Geometry);
        if (search.TopTwenty.Count == 0)
            throw new InvalidOperationException("No valid free trajectory was found.");
        var best = search.TopTwenty[0];
        var constants = controls.Where(item => item.Candidate.SeedFamily.StartsWith("Constant", StringComparison.Ordinal))
            .OrderBy(item => item.SectorTimeSeconds).ThenBy(item => item.Candidate.Id, StringComparer.Ordinal).ToArray();
        var bestConstant = constants[0];
        var constantInner = controls.Single(item => item.Candidate.SeedFamily == "ConstantInner");
        var delta = bestConstant.SectorTimeSeconds - best.SectorTimeSeconds;
        var beats = delta > GameplayTieToleranceSeconds;
        var ties = MathF.Abs(delta) <= GameplayTieToleranceSeconds;
        var exitTradeoff = best.ExitSpeedMetersPerSecond > bestConstant.ExitSpeedMetersPerSecond + 1e-5f
            && best.CornerTimeSeconds > bestConstant.CornerTimeSeconds + 1e-5f;
        var changesRanking = best.IsNonConstant && best.SectorTimeSeconds < bestConstant.SectorTimeSeconds;
        var lateralBinding = search.InvalidLateralExecutionCandidates > 0;
        var localCornerControlBinding = search.InvalidCornerControlDepartureCandidates > 0
            || search.InvalidCrashCandidates > 0 || best.BrakeStepCount > 0;
        var straightRepositionBinding = search.InvalidStraightRepositionCandidates > 0
            || best.StraightRepositionDistanceMeters > track.Geometry.StraightLengthMeters + 1e-5f;
        var variableReplayHealthy = replayPassed && variableEnvelopeConsistent && best.IsValid
            && best.RunWideInvalidationCount == 0 && best.CrashInvalidationCount == 0;
        var geometrySignalHealthy = variableReplayHealthy && search.ObjectiveConvergence;
        var repeatedBest = RunRepeatedLap(evaluator, best.Candidate, entrySpeed);
        var repeatedConstant = RunRepeatedLap(evaluator, bestConstant.Candidate, entrySpeed);
        var cornerSpeedSensitivity = RunCornerSpeedSensitivity(
            track.Geometry,
            entrySpeed,
            e0a1x0,
            e0a1x1);

        string decisionCase;
        string recommendation;
        string bottleneck;
        if (!variableReplayHealthy || !search.ObjectiveConvergence)
        {
            decisionCase = "D";
            recommendation = "trajectory evaluator/search repair";
            bottleneck = !replayPassed ? "constant replay equivalence"
                : !variableEnvelopeConsistent ? "variable-curvature evaluator semantics"
                : "search convergence";
        }
        else if (search.InvalidLateralExecutionCandidates
                 + search.InvalidStraightRepositionCandidates > search.CandidatesEvaluated * .75f)
        {
            decisionCase = "E";
            recommendation = "continuous trajectory execution/planning experiment";
            bottleneck = "lateral execution feasibility";
        }
        else if (beats && best.IsNonConstant)
        {
            decisionCase = "A";
            recommendation = "production continuous trajectory geometry integration experiment";
            bottleneck = "production segment-entry trajectory sampling";
        }
        else
        {
            decisionCase = changesRanking ? "B" : "C";
            recommendation = "corner turning/slip cost experiment";
            bottleneck = "corner trajectory physics economics after continuous geometry";
        }

        var flags = new List<string>
        {
            beats ? "FreeTrajectoryBeatsBestConstant" : ties
                ? "FreeTrajectoryTiesBestConstant" : "BestConstantRemainsFaster",
            best.IsNonConstant ? "BestFoundTrajectoryIsNonConstant" : "BestFoundTrajectoryIsConstant",
        };
        if (exitTradeoff) flags.Add("ExitSpeedTradeoffObserved");
        if (changesRanking) flags.Add("ContinuousCurvatureChangesRanking");
        if (lateralBinding) flags.Add("LateralExecutionConstraintBinding");
        if (variableEnvelopeConsistent) flags.Add("VariableCurvatureEnvelopeConsistent");
        if (localCornerControlBinding) flags.Add("LocalCornerControlConstraintActive");
        flags.Add(oldWinner.IsValid ? "OldWinnerStillValidAfterRepair" : "OldWinnerRejectedAfterRepair");
        if (straightRepositionBinding) flags.Add("StraightRepositionConstraintActive");
        if (search.ObjectiveConvergence) flags.Add("ObjectiveConvergence");
        if (search.GeometryConvergence) flags.Add("GeometryConvergence");
        if (search.MultipleNearOptimalBasins) flags.Add("MultipleNearOptimalBasins");
        if (search.LocalSearchConverged) flags.Add("LocalSearchConverged");
        if (variableReplayHealthy) flags.Add("VariableCurvatureReplayHealthy");
        if (search.SearchConvergenceUncertain) flags.Add("SearchConvergenceUncertain");
        if (!replayPassed) flags.Add("ConstantReplayMismatch");
        if (geometrySignalHealthy) flags.Add("FreeTrajectoryGeometrySignalHealthy");

        return new FreeContinuousRacingTrajectoryGeometryExperimentResult
        {
            BaseSha = BaseMainSha,
            Geometry = track.Geometry,
            EntrySpeedMetersPerSecond = entrySpeed,
            ConstantInnerProductionFlyingLapSeconds = constantInner46.FlyingLapMedianSeconds,
            E0A1X0ProductionFlyingLapSeconds = e0a1x0.FlyingLapMedianSeconds,
            E0A1X1ProductionFlyingLapSeconds = e0a1x1.FlyingLapMedianSeconds,
            Reproduces46 = reproduces46,
            ConstantReplay = Array.AsReadOnly(constantReplay),
            ConstantReplayValidationPassed = replayPassed,
            Controls = Array.AsReadOnly(controls),
            SanityControls = Array.AsReadOnly(sanityControls),
            OldWinnerAfterRepair = oldWinner,
            Search = search,
            BestFound = best,
            BestConstant = bestConstant,
            ConstantInner = constantInner,
            RepeatedBestFound = repeatedBest,
            RepeatedBestConstant = repeatedConstant,
            CornerSpeedSensitivity = cornerSpeedSensitivity,
            TurningDemandContinuity = continuity,
            PriorReviewedWinnerAfterContinuityRepair = priorReviewedWinner,
            PriorReviewedBroadLateWinnerAfterEnvelopeRepair = priorReviewedBroadLateWinner,
            FreeTrajectoryBeatsBestConstant = beats,
            FreeTrajectoryTiesBestConstant = ties,
            ExitSpeedTradeoffObserved = exitTradeoff,
            ContinuousCurvatureChangesRanking = changesRanking,
            LateralExecutionConstraintBinding = lateralBinding,
            VariableCurvatureEnvelopeConsistent = variableEnvelopeConsistent,
            LocalCornerControlConstraintActive = localCornerControlBinding,
            OldWinnerStillValidAfterRepair = oldWinner.IsValid,
            OldWinnerRejectedAfterRepair = !oldWinner.IsValid,
            StraightRepositionConstraintActive = straightRepositionBinding,
            VariableCurvatureReplayHealthy = variableReplayHealthy,
            FreeTrajectoryGeometrySignalHealthy = geometrySignalHealthy,
            Classification = string.Join(", ", flags),
            FirstActualBottleneck = bottleneck,
            RecommendedSubsystem = recommendation,
            DecisionCase = decisionCase,
        };
    }

    public static FreeTrajectoryEvaluation Evaluate(
        IReadOnlyList<float> controlOffsetsMeters,
        float? entrySpeedMetersPerSecond = null,
        string seedFamily = "Direct")
    {
        var track = CreateTrack();
        var entry = entrySpeedMetersPerSecond
            ?? DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 0, 0))
                .CornerEntrySpeedMetersPerSecond;
        return new ExperimentalGeometryReplay(track.Geometry, entry)
            .Evaluate(new FreeTrajectoryCandidate(seedFamily, Array.AsReadOnly(controlOffsetsMeters.ToArray())), entry);
    }

    public static FreeTrajectoryEvaluation EvaluateAtReferenceTurnSpeed(
        IReadOnlyList<float> controlOffsetsMeters,
        float referenceTurnSpeedMetersPerSecond,
        float? entrySpeedMetersPerSecond = null,
        string seedFamily = "Direct sensitivity")
    {
        var track = CreateTrack();
        var entry = entrySpeedMetersPerSecond
            ?? DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 0, 0))
                .CornerEntrySpeedMetersPerSecond;
        return new ExperimentalGeometryReplay(
                track.Geometry,
                entry,
                referenceTurnSpeedMetersPerSecond)
            .Evaluate(new FreeTrajectoryCandidate(
                seedFamily,
                Array.AsReadOnly(controlOffsetsMeters.ToArray())), entry);
    }

    public static IReadOnlyList<TurningDemandConstraint> ExtractTurningDemandConstraints(
        IReadOnlyList<float> controlOffsetsMeters)
    {
        var geometry = CreateTrack().Geometry;
        return BuildTurningDemandConstraints(
            FreeContinuousTrajectoryGeometry.Build(controlOffsetsMeters, geometry),
            SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond);
    }

    public static IReadOnlyList<TurningDemandEnvelopeComparison> CompareTurningDemandEnvelope(
        IReadOnlyList<float> controlOffsetsMeters,
        IReadOnlyList<float> progressPoints)
    {
        ArgumentNullException.ThrowIfNull(controlOffsetsMeters);
        ArgumentNullException.ThrowIfNull(progressPoints);
        var geometry = CreateTrack().Geometry;
        var path = FreeContinuousTrajectoryGeometry.Build(controlOffsetsMeters, geometry);
        var correctionCapability = LongitudinalDynamics
            .CalculateCornerCorrectionDecelerationMetersPerSecondSquared(Balanced, UniformSurface);
        var sampled = BuildSampledTurningDemandConstraints(
            path,
            SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond);
        var reduced = ReduceToBindingSuffixEnvelope(sampled, correctionCapability);
        return Array.AsReadOnly(progressPoints.Select(progress =>
        {
            var distance = path.DistanceAtProgress(progress);
            var reducedTarget = ReducedLookaheadTarget(reduced, distance, correctionCapability)
                ?? throw new InvalidOperationException("Reduced turning-demand envelope ended before the path.");
            var bruteTarget = BruteForceLookaheadTarget(sampled, distance, correctionCapability)
                ?? throw new InvalidOperationException("Sampled turning-demand envelope ended before the path.");
            return new TurningDemandEnvelopeComparison(progress, reducedTarget, bruteTarget);
        }).ToArray());
    }

    private static TurningDemandContinuityResult RunTurningDemandContinuityDiagnostic(
        TrackGeometry geometry,
        float entrySpeedMetersPerSecond)
    {
        var evaluator = new ExperimentalGeometryReplay(geometry, entrySpeedMetersPerSecond);
        var priorWinner = PriorReviewedWinnerCandidate(geometry);
        var maximumControl = priorWinner.ControlOffsetsMeters.Max();
        var shape = priorWinner.ControlOffsetsMeters.Select(value => value / maximumControl).ToArray();
        var targets = new (string Name, float Multiple)[]
        {
            ("Constant", 0f),
            ("Approach-0.001x", .001f),
            ("Approach-0.01x", .01f),
            ("Approach-0.10x", .10f),
            ("HalfTolerance", .50f),
            ("NineTenthsTolerance", .90f),
            ("JustBelowTolerance", .99f),
            ("JustAboveTolerance", 1.01f),
            ("ElevenTenthsTolerance", 1.10f),
            ("TwiceTolerance", 2f),
        };
        var observations = targets.Select(target =>
        {
            var targetRange = target.Multiple * ConstantCurvatureTolerancePerMeter;
            var scale = targetRange <= 0f
                ? 0f
                : FindPerturbationScaleForCurvatureRange(geometry, shape, targetRange);
            var controls = shape.Select(value => value * scale).ToArray();
            var path = FreeContinuousTrajectoryGeometry.Build(controls, geometry);
            var actualRange = CurvatureRange(path);
            var evaluation = evaluator.Evaluate(Candidate($"Continuity-{target.Name}", controls));
            if (!evaluation.IsValid)
                throw new InvalidOperationException($"Continuity diagnostic {target.Name} is invalid: {evaluation.InvalidReason}.");
            return new TurningDemandContinuityObservation(
                target.Name,
                targetRange,
                scale,
                Array.AsReadOnly(controls),
                actualRange,
                evaluation);
        }).ToArray();

        var constant = observations[0].Evaluation;
        var smallest = observations[1].Evaluation;
        var below = observations.Single(item => item.Name == "JustBelowTolerance").Evaluation;
        var above = observations.Single(item => item.Name == "JustAboveTolerance").Evaluation;
        var belowAboveSectorDelta = MathF.Abs(above.SectorTimeSeconds - below.SectorTimeSeconds);
        var belowAboveSpeedDelta = MaximumProfileSpeedDelta(below, above);
        var smallestCornerDelta = MathF.Abs(smallest.CornerTimeSeconds - constant.CornerTimeSeconds);
        var smallestSectorDelta = MathF.Abs(smallest.SectorTimeSeconds - constant.SectorTimeSeconds);
        var smallestExitSpeedDelta = MathF.Abs(
            smallest.ExitSpeedMetersPerSecond - constant.ExitSpeedMetersPerSecond);
        var smallestSpeedDelta = MaximumProfileSpeedDelta(smallest, constant);
        var canonicalAlwaysPresent = observations.All(item =>
            item.Evaluation.TurningDemandConstraints.Count(constraint =>
                constraint.IsCanonicalBaseline && constraint.Progress == .5f) == 1);
        var maximumNearConstantSectorDelta = observations.Skip(1).Max(item =>
            MathF.Abs(item.Evaluation.SectorTimeSeconds - constant.SectorTimeSeconds));
        var supplementalRestrictions = observations.Skip(1).Select(item =>
            item.Evaluation.TurningDemandConstraints
                .Where(constraint => !constraint.IsCanonicalBaseline)
                .Select(constraint => constraint.CanonicalBaselineSpeedMetersPerSecond
                    - constraint.SettledCapabilityMetersPerSecond)
                .DefaultIfEmpty(0f)
                .Max()).ToArray();
        var twiceSupplementalRestriction = supplementalRestrictions[^1];
        var differentialRestrictionConverges = supplementalRestrictions[0] <= .0001f
            && supplementalRestrictions.Zip(supplementalRestrictions.Skip(1))
                .All(pair => pair.First <= pair.Second + 1e-5f);
        var nearConstantPenaltyGate = maximumNearConstantSectorDelta <= .005f
            && twiceSupplementalRestriction > 0f
            && twiceSupplementalRestriction <= .01f;
        var convergence = smallestCornerDelta <= .005f
            && smallestSectorDelta <= .005f
            && smallestExitSpeedDelta <= .005f
            && smallestSpeedDelta <= .005f;
        var thresholdContinuity = belowAboveSectorDelta <= .005f && belowAboveSpeedDelta <= .005f;
        return new TurningDemandContinuityResult(
            Array.AsReadOnly(observations),
            ConstantCurvatureTolerancePerMeter,
            belowAboveSectorDelta,
            belowAboveSpeedDelta,
            smallestCornerDelta,
            smallestSectorDelta,
            smallestExitSpeedDelta,
            smallestSpeedDelta,
            maximumNearConstantSectorDelta,
            twiceSupplementalRestriction,
            canonicalAlwaysPresent,
            nearConstantPenaltyGate,
            differentialRestrictionConverges,
            convergence,
            thresholdContinuity);
    }

    private static float FindPerturbationScaleForCurvatureRange(
        TrackGeometry geometry,
        IReadOnlyList<float> shape,
        float targetRange)
    {
        var lower = 0f;
        var upper = .001f;
        while (CurvatureRange(FreeContinuousTrajectoryGeometry.Build(
                   shape.Select(value => value * upper).ToArray(), geometry)) < targetRange)
        {
            upper *= 2f;
            if (upper > 2f)
                throw new InvalidOperationException("Unable to bracket the requested continuity curvature range.");
        }
        for (var iteration = 0; iteration < 56; iteration++)
        {
            var middle = (lower + upper) * .5f;
            var range = CurvatureRange(FreeContinuousTrajectoryGeometry.Build(
                shape.Select(value => value * middle).ToArray(), geometry));
            if (range < targetRange) lower = middle;
            else upper = middle;
        }
        var lowerRange = CurvatureRange(FreeContinuousTrajectoryGeometry.Build(
            shape.Select(value => value * lower).ToArray(), geometry));
        var upperRange = CurvatureRange(FreeContinuousTrajectoryGeometry.Build(
            shape.Select(value => value * upper).ToArray(), geometry));
        return MathF.Abs(lowerRange - targetRange) <= MathF.Abs(upperRange - targetRange)
            ? lower
            : upper;
    }

    private static float CurvatureRange(FreeTrajectoryPath path)
        => path.Samples.Max(item => item.CurvaturePerMeter)
            - path.Samples.Min(item => item.CurvaturePerMeter);

    private static float MaximumProfileSpeedDelta(
        FreeTrajectoryEvaluation first,
        FreeTrajectoryEvaluation second)
        => first.Profile.Zip(second.Profile)
            .Max(pair => MathF.Abs(pair.First.SpeedMetersPerSecond - pair.Second.SpeedMetersPerSecond));

    public static string CandidateId(IReadOnlyList<float> offsets)
    {
        var canonical = string.Join(",", offsets.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
        return "FCT-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..12];
    }

    private static FreeTrajectoryEvaluation[] BuildControls(
        ExperimentalGeometryReplay evaluator,
        TrackGeometry geometry,
        TrajectoryObservation e0a1x0,
        TrajectoryObservation e0a1x1)
    {
        var controls = new List<FreeTrajectoryEvaluation>();
        var names = new[] { "ConstantInner", "ConstantL1", "ConstantL2", "ConstantL3", "ConstantOuter" };
        for (var lane = 0; lane < LaneModel.LanesCount; lane++)
        {
            var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
                lane, SegmentType.TurnMiddle, geometry);
            controls.Add(evaluator.Evaluate(Candidate(names[lane], Enumerable.Repeat(offset, ControlStationCount))));
        }
        controls.Add(evaluator.Evaluate(Candidate("#46 E0-A1-X0", ControlVectorFrom46(e0a1x0, geometry))));
        controls.Add(evaluator.Evaluate(Candidate("#46 E0-A1-X1", ControlVectorFrom46(e0a1x1, geometry))));
        return controls.ToArray();
    }

    private static VariableCurvatureSanityControl[] BuildSanityControls(
        ExperimentalGeometryReplay evaluator)
    {
        var fixtures = new[]
        {
            Candidate("SmoothSinglePeak", new[]
                { .64f, .54769f, .48581f, .44434f, .41653f, .39789f,
                    .38540f, .37703f, .37141f, .36765f, .36513f }),
            Candidate("SmoothTwoPeak", new[]
                { 10.04f, 9.96f, 9.96f, 10.04f, 9.96f, 9.96f,
                    10.04f, 10.00f, 9.96f, 9.96f, 10.00f }),
            Candidate("EarlyTightThenOpen", new[]
                { .98f, .79f, .92f, 1.06f, 1.05f, .96f, .91f, .88f, .85f, .82f, .81f }),
            Candidate("LateTight", new[]
                { .34f, .24769f, .18581f, .14434f, .11653f, .09789f,
                    .08540f, .07703f, .07141f, .06765f, .06513f }),
        };
        return fixtures.Select(candidate =>
        {
            var evaluation = evaluator.Evaluate(candidate);
            var constraints = evaluation.TurningDemandConstraints;
            var restrictiveBindings = constraints.Where(item =>
                !item.IsCanonicalBaseline
                && item.IsBindingEnvelopePoint
                && item.SettledCapabilitySquaredDeltaMetersSquaredPerSecondSquared < 0f).ToArray();
            var noDeparture = evaluation.IsValid
                && evaluation.TurningConstraintTrace.All(item =>
                    item.Outcome is not SegmentOutcome.RunWide and not SegmentOutcome.Crash);
            var semanticsPassed = candidate.SeedFamily switch
            {
                "SmoothSinglePeak" => noDeparture
                    && restrictiveBindings.Length > 0,
                "SmoothTwoPeak" => noDeparture
                    && restrictiveBindings.Any(item => item.Progress < .5f)
                    && restrictiveBindings.Any(item => item.Progress > .5f),
                "EarlyTightThenOpen" => noDeparture
                    && restrictiveBindings.Any(item => item.Progress is > 0f and < .5f)
                    && evaluation.FirstCorrectionProgress
                        < restrictiveBindings.Where(item => item.Progress is > 0f and < .5f)
                            .OrderByDescending(item => item.CanonicalBaselineSpeedMetersPerSecond
                                - item.SettledCapabilityMetersPerSecond).First().Progress,
                "LateTight" => noDeparture
                    && restrictiveBindings.Any(item => item.Progress > .9f)
                    && evaluation.FirstCorrectionProgress
                        < restrictiveBindings.Last(item => item.Progress > .9f).Progress,
                _ => false,
            };
            return new VariableCurvatureSanityControl(candidate.SeedFamily, evaluation, semanticsPassed);
        }).ToArray();
    }

    private static IReadOnlyList<float> ControlVectorFrom46(TrajectoryObservation observation, TrackGeometry geometry)
    {
        var points = new[]
        {
            (0f, observation.EntryLateralPosition),
            (1f / 3f, observation.Corners.Average(item => item.OneThirdLateralPosition)),
            (.5f, observation.ApexLateralPosition),
            (2f / 3f, observation.Corners.Average(item => item.TwoThirdsLateralPosition)),
            (1f, observation.ExitLateralPosition),
        };
        var values = new float[ControlStationCount];
        for (var index = 0; index < values.Length; index++)
        {
            var progress = (float)index / (values.Length - 1);
            var upper = 1;
            while (upper < points.Length - 1 && progress > points[upper].Item1) upper++;
            var lower = upper - 1;
            var t = (progress - points[lower].Item1) / (points[upper].Item1 - points[lower].Item1);
            var lateral = points[lower].Item2 + (points[upper].Item2 - points[lower].Item2) * t;
            values[index] = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
                lateral, SegmentType.TurnMiddle, geometry);
        }
        return Array.AsReadOnly(values);
    }

    private static ConstantPathReplayComparison CompareConstantPath(
        ExperimentalGeometryReplay evaluator,
        TrackGeometry geometry,
        int lane,
        float entrySpeed)
    {
        var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(lane, SegmentType.TurnMiddle, geometry);
        var candidate = Candidate($"ConstantL{lane}", Enumerable.Repeat(offset, ControlStationCount));
        var experimental = evaluator.Evaluate(candidate, entrySpeed);
        var production = ProductionConstantReplay(geometry, lane, entrySpeed);
        var speedDelta = experimental.Profile.Max(point => MathF.Abs(point.SpeedMetersPerSecond
            - InterpolateProductionSpeed(production.CornerNodes, point.Progress)));
        var sectorDelta = experimental.SectorTimeSeconds - production.SectorTimeSeconds;
        var exitDelta = experimental.ExitSpeedMetersPerSecond - production.ExitSpeedMetersPerSecond;
        return new ConstantPathReplayComparison(
            lane == 0 ? "ConstantInner" : lane == 4 ? "ConstantOuter" : $"ConstantL{lane}", lane,
            production.CornerTimeSeconds, experimental.CornerTimeSeconds,
            production.StraightTimeSeconds, experimental.FollowingStraightTimeSeconds,
            production.SectorTimeSeconds, experimental.SectorTimeSeconds,
            sectorDelta, exitDelta, speedDelta,
            experimental.IsValid
                && MathF.Abs(sectorDelta) <= ConstantReplayToleranceSeconds
                && MathF.Abs(exitDelta) <= ConstantReplaySpeedToleranceMetersPerSecond
                && speedDelta <= ConstantReplaySpeedToleranceMetersPerSecond);
    }

    private static ProductionSectorObservation ProductionConstantReplay(
        TrackGeometry geometry,
        int lane,
        float entrySpeed)
    {
        var radius = LaneModel.TurnArcRadiusMeters(lane, geometry);
        var segmentLength = radius * geometry.TurnSegmentAngleRadians;
        var totalLength = 3f * segmentLength;
        var safe = SegmentPhysics.MaxSafeTurnSpeed(lane, geometry, UniformSurface, Balanced, Neutral);
        var correction = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
            Balanced, UniformSurface);
        var force = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(Balanced, Neutral, UniformSurface);
        var envelope = new ContinuousCornerEnvelope(safe, totalLength, correction, force, Neutral);
        var speed = entrySpeed;
        var time = 0f;
        var nodes = new List<ContinuousCornerNode>();
        for (var segment = 0; segment < 3; segment++)
        {
            var profile = envelope.Traverse(speed, segment / 3f, segmentLength);
            if (segment == 0) nodes.AddRange(profile.Nodes);
            else nodes.AddRange(profile.Nodes.Skip(1));
            speed = profile.ExitSpeedMetersPerSecond;
            time += profile.TravelTimeSeconds;
        }
        var straight = StraightProfile(speed, geometry.StraightLengthMeters);
        return new ProductionSectorObservation(time, straight.TravelTimeSeconds,
            time + straight.TravelTimeSeconds, speed, Array.AsReadOnly(nodes.ToArray()));
    }

    private static float InterpolateProductionSpeed(IReadOnlyList<ContinuousCornerNode> nodes, float progress)
    {
        if (progress <= nodes[0].CornerProgress) return nodes[0].SpeedMetersPerSecond;
        for (var index = 1; index < nodes.Count; index++)
        {
            if (progress > nodes[index].CornerProgress + 1e-6f) continue;
            var span = nodes[index].CornerProgress - nodes[index - 1].CornerProgress;
            var t = span <= 0f ? 0f : (progress - nodes[index - 1].CornerProgress) / span;
            return nodes[index - 1].SpeedMetersPerSecond
                + (nodes[index].SpeedMetersPerSecond - nodes[index - 1].SpeedMetersPerSecond) * t;
        }
        return nodes[^1].SpeedMetersPerSecond;
    }

    private static FreeTrajectorySearchResult RunSearch(
        ExperimentalGeometryReplay evaluator,
        TrackGeometry geometry)
    {
        var structured = StructuredSeedFamilies(geometry).ToArray();
        var lowDiscrepancy = LowDiscrepancySeedFamilies(geometry).ToArray();
        var families = structured.Concat(lowDiscrepancy).ToArray();
        var evaluations = new List<FreeTrajectoryEvaluation>();
        var cache = new Dictionary<string, FreeTrajectoryEvaluation>(StringComparer.Ordinal);
        var evaluated = 0;
        var valid = 0;
        var invalidGeometry = 0;
        var invalidLateral = 0;
        var invalidDeparture = 0;
        var invalidCrash = 0;
        var invalidStraight = 0;

        FreeTrajectoryEvaluation Test(FreeTrajectoryCandidate candidate)
        {
            if (cache.TryGetValue(candidate.Id, out var cached)) return cached;
            evaluated++;
            var result = evaluator.Evaluate(candidate);
            cache.Add(candidate.Id, result);
            if (result.IsValid)
            {
                valid++;
                evaluations.Add(result);
            }
            else if (result.Validity == FreeTrajectoryValidity.LateralExecutionConstraint) invalidLateral++;
            else if (result.Validity == FreeTrajectoryValidity.CornerControlPathDeparture) invalidDeparture++;
            else if (result.Validity == FreeTrajectoryValidity.CornerControlCrash) invalidCrash++;
            else if (result.Validity == FreeTrajectoryValidity.StraightRepositionConstraint) invalidStraight++;
            else invalidGeometry++;
            return result;
        }

        var initial = families.Select(Test).ToArray();
        var initialValid = initial.Where(item => item.IsValid)
            .OrderBy(item => item.SectorTimeSeconds)
            .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal).ToArray();
        var initialGeometry = initial.Count(item => item.Validity is FreeTrajectoryValidity.TrackBoundary
            or FreeTrajectoryValidity.SelfIntersection or FreeTrajectoryValidity.NonSmoothGeometry);
        var initialLateral = initial.Count(item => item.Validity == FreeTrajectoryValidity.LateralExecutionConstraint);
        var initialCornerControl = initial.Count(item => item.Validity is
            FreeTrajectoryValidity.CornerControlPathDeparture or FreeTrajectoryValidity.CornerControlCrash);
        var initialStraight = initial.Count(item =>
            item.Validity == FreeTrajectoryValidity.StraightRepositionConstraint);
        var selected = SelectGeometryDiverse(initialValid, RefinedStartCount);
        var finalStates = new List<RefinedStartState>();

        foreach (var seed in selected)
        {
            var currentControls = seed.ControlOffsetsMeters.ToArray();
            var current = Test(seed);
            var lastRoundImprovement = 0f;
            var stepImprovements = new List<float>();
            var changedInLastRound = false;
            foreach (var step in RefinementStepsMeters)
            {
                var before = current.SectorTimeSeconds;
                var beforeId = current.Candidate.Id;
                var globalChoices = new List<FreeTrajectoryEvaluation>();
                if (current.IsValid) globalChoices.Add(current);
                foreach (var direction in new[] { -1f, 1f })
                {
                    var shifted = currentControls.Select(value => value + direction * step).ToArray();
                    globalChoices.Add(Test(Candidate(seed.SeedFamily, shifted)));
                }
                var globalBest = globalChoices.Where(item => item.IsValid)
                    .OrderBy(item => item.SectorTimeSeconds)
                    .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (globalBest is not null)
                {
                    current = globalBest;
                    currentControls = current.Candidate.ControlOffsetsMeters.ToArray();
                }
                for (var sweep = 0; sweep < CoordinateSweepsPerRound; sweep++)
                for (var station = 0; station < ControlStationCount; station++)
                {
                    var choices = new List<FreeTrajectoryEvaluation>();
                    if (current.IsValid) choices.Add(current);
                    foreach (var direction in new[] { -1f, 1f })
                    {
                        var trial = currentControls.ToArray();
                        trial[station] += direction * step;
                        choices.Add(Test(Candidate(seed.SeedFamily, trial)));
                    }
                    var bestChoice = choices.Where(item => item.IsValid)
                        .OrderBy(item => item.SectorTimeSeconds)
                        .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (bestChoice is null) continue;
                    current = bestChoice;
                    currentControls = current.Candidate.ControlOffsetsMeters.ToArray();
                }
                lastRoundImprovement = before - current.SectorTimeSeconds;
                stepImprovements.Add(lastRoundImprovement);
                changedInLastRound = beforeId != current.Candidate.Id;
            }
            if (current.IsValid)
            {
                finalStates.Add(new RefinedStartState(seed.SeedFamily, current,
                    Array.AsReadOnly(stepImprovements.ToArray()),
                    lastRoundImprovement, changedInLastRound));
            }
        }

        FreeTrajectoryEvaluation[] RankedTop() => evaluations
            .GroupBy(item => item.PathFingerprint, StringComparer.Ordinal)
            .Select(group => group.OrderBy(item => item.SectorTimeSeconds)
                .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal).First())
            .OrderBy(item => item.SectorTimeSeconds).ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
            .Take(20).ToArray();

        FreeTrajectoryEvaluation BestSingleCoordinateMove(
            FreeTrajectoryEvaluation origin, IReadOnlyList<float>? steps = null)
        {
            var choices = new List<FreeTrajectoryEvaluation> { origin };
            foreach (var step in steps ?? new[] { .125f, .0625f })
            for (var station = 0; station < ControlStationCount; station++)
            foreach (var direction in new[] { -1f, 1f })
            {
                var controls = origin.Candidate.ControlOffsetsMeters.ToArray();
                controls[station] += direction * step;
                choices.Add(Test(Candidate("Final-local-check", controls)));
            }
            return choices.Where(item => item.IsValid)
                .OrderBy(item => item.SectorTimeSeconds)
                .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
                .First();
        }

        FreeTrajectoryEvaluation BestAdjacentPairMove(IEnumerable<FreeTrajectoryEvaluation> origins)
        {
            var choices = new List<FreeTrajectoryEvaluation>();
            foreach (var origin in origins)
            {
                choices.Add(origin);
                for (var station = 0; station < ControlStationCount - 1; station++)
                foreach (var firstDirection in new[] { -1f, 1f })
                foreach (var secondDirection in new[] { -1f, 1f })
                {
                    var controls = origin.Candidate.ControlOffsetsMeters.ToArray();
                    controls[station] += firstDirection * .0625f;
                    controls[station + 1] += secondDirection * .0625f;
                    choices.Add(Test(Candidate("Final-pair-check", controls)));
                }
            }
            return choices.Where(item => item.IsValid)
                .OrderBy(item => item.SectorTimeSeconds)
                .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
                .First();
        }

        (FreeTrajectoryEvaluation Final, int Moves, int Passes) CloseLocalNeighborhood(
            FreeTrajectoryEvaluation origin)
        {
            var current = origin;
            var moves = 0;
            var passes = 0;
            while (true)
            {
                passes++;
                var single = BestSingleCoordinateMove(current);
                var pair = BestAdjacentPairMove(new[] { current });
                var next = new[] { current, single, pair }
                    .OrderBy(item => item.SectorTimeSeconds)
                    .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
                    .First();
                if (current.SectorTimeSeconds - next.SectorTimeSeconds
                    <= LocalSearchImprovementToleranceSeconds)
                    return (current, moves, passes);
                current = next;
                moves++;
            }
        }

        FreeTrajectoryRefinedStartDiagnostic DiagnoseStart(
            RefinedStartState item, bool useScheduledRoundMetric)
        {
            var single = BestSingleCoordinateMove(item.Evaluation, new[] { .0625f });
            var expandedSingle = BestSingleCoordinateMove(item.Evaluation);
            var pair = BestAdjacentPairMove(new[] { item.Evaluation });
            var singleResidual = MathF.Max(0f,
                item.Evaluation.SectorTimeSeconds - single.SectorTimeSeconds);
            var expandedSingleResidual = MathF.Max(0f,
                item.Evaluation.SectorTimeSeconds - expandedSingle.SectorTimeSeconds);
            var pairResidual = MathF.Max(0f,
                item.Evaluation.SectorTimeSeconds - pair.SectorTimeSeconds);
            var stable = useScheduledRoundMetric
                ? item.LastRoundImprovement <= LocalSearchImprovementToleranceSeconds
                : item.ClosurePasses > 0
                  && expandedSingleResidual <= LocalSearchImprovementToleranceSeconds
                  && pairResidual <= LocalSearchImprovementToleranceSeconds;
            return new FreeTrajectoryRefinedStartDiagnostic(
                item.Family, item.Evaluation, item.StepImprovements, item.LastRoundImprovement,
                singleResidual, expandedSingleResidual, pairResidual,
                item.ChangedInLastRound, item.AdditionalClosureMoves, item.ClosurePasses, stable);
        }

        var archivedTop = RankedTop();
        var archivedBest = archivedTop[0];
        var initialSingle = BestSingleCoordinateMove(archivedBest);
        var initialLocalImprovement = MathF.Max(0f,
            archivedBest.SectorTimeSeconds - initialSingle.SectorTimeSeconds);
        var initialPair = BestAdjacentPairMove(archivedTop.Take(3));
        var initialPairImprovement = MathF.Max(0f,
            archivedBest.SectorTimeSeconds - initialPair.SectorTimeSeconds);
        var scheduledTopThreeDiagnostics = finalStates
            .OrderBy(item => item.Evaluation.SectorTimeSeconds)
            .ThenBy(item => item.Evaluation.Candidate.Id, StringComparer.Ordinal)
            .Take(3).Select(item => DiagnoseStart(item, useScheduledRoundMetric: true)).ToArray();
        var closureBest = archivedBest;
        var additionalAcceptedMoves = 0;
        var initialBest = new[] { archivedBest, initialSingle, initialPair }
            .OrderBy(item => item.SectorTimeSeconds)
            .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
            .First();
        if (archivedBest.SectorTimeSeconds - initialBest.SectorTimeSeconds
            > LocalSearchImprovementToleranceSeconds)
        {
            closureBest = initialBest;
            additionalAcceptedMoves++;
        }
        var globalClosure = CloseLocalNeighborhood(closureBest);
        closureBest = globalClosure.Final;
        additionalAcceptedMoves += globalClosure.Moves;

        // A last scheduled round can make material progress even when its stored final
        // candidate is already locally closed. Check the final neighborhood itself.
        // If closure changes the ranking, close any newly promoted top-three start too.
        var closedFamilies = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var next = finalStates.OrderBy(item => item.Evaluation.SectorTimeSeconds)
                .ThenBy(item => item.Evaluation.Candidate.Id, StringComparer.Ordinal)
                .Take(3).FirstOrDefault(item => !closedFamilies.Contains(item.Family));
            if (next is null) break;
            var closed = CloseLocalNeighborhood(next.Evaluation);
            var index = finalStates.FindIndex(item => item.Family == next.Family);
            finalStates[index] = next with
            {
                Evaluation = closed.Final,
                AdditionalClosureMoves = closed.Moves,
                ClosurePasses = closed.Passes,
            };
            closedFamilies.Add(next.Family);
        }

        var familyResults = finalStates.Select(item =>
            new FreeTrajectorySeedFamilyResult(item.Family, item.Evaluation)).ToArray();
        var top = archivedTop.Concat(finalStates.Select(item => item.Evaluation)).Append(closureBest)
            .GroupBy(item => item.PathFingerprint, StringComparer.Ordinal)
            .Select(group => group.OrderBy(item => item.SectorTimeSeconds)
                .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal).First())
            .OrderBy(item => item.SectorTimeSeconds).ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
            .Take(20).ToArray();
        var best = top[0];
        var residualSingle = BestSingleCoordinateMove(best);
        var residualPair = BestAdjacentPairMove(new[] { best });
        var localImprovement = MathF.Max(0f,
            best.SectorTimeSeconds - residualSingle.SectorTimeSeconds);
        var pairImprovement = MathF.Max(0f,
            best.SectorTimeSeconds - residualPair.SectorTimeSeconds);
        var localConverged = localImprovement <= LocalSearchImprovementToleranceSeconds
            && pairImprovement <= LocalSearchImprovementToleranceSeconds;
        var within = familyResults.Where(item =>
            item.Best.SectorTimeSeconds - best.SectorTimeSeconds <= GameplayTieToleranceSeconds).ToArray();
        var sameBasin = within.Count(item => RootMeanSquareDifference(
            item.Best.Candidate.ControlOffsetsMeters, best.Candidate.ControlOffsetsMeters) <= .50f);
        var shapeRepresentatives = new List<FreeTrajectoryEvaluation>();
        foreach (var item in within.OrderBy(item => item.Best.Candidate.Id, StringComparer.Ordinal))
        {
            if (shapeRepresentatives.All(existing => RootMeanSquareDifference(
                    existing.Candidate.ControlOffsetsMeters, item.Best.Candidate.ControlOffsetsMeters) > .50f))
                shapeRepresentatives.Add(item.Best);
        }
        var topThreeDiagnostics = finalStates.OrderBy(item => item.Evaluation.SectorTimeSeconds)
            .ThenBy(item => item.Evaluation.Candidate.Id, StringComparer.Ordinal).Take(3)
            .Select(item => DiagnoseStart(item, useScheduledRoundMetric: false)).ToArray();
        var topThreeStable = topThreeDiagnostics.All(item => item.Stable);
        var objectiveConvergence = within.Length >= 3 && localConverged && topThreeStable;
        var geometryConvergence = within.Length > 0 && within.All(item => RootMeanSquareDifference(
            item.Best.Candidate.ControlOffsetsMeters, best.Candidate.ControlOffsetsMeters) <= .50f);
        var multipleNearOptimalBasins = objectiveConvergence && !geometryConvergence;
        var uncertain = !objectiveConvergence || within.Length < 3;
        return new FreeTrajectorySearchResult(
            Array.AsReadOnly(top), Array.AsReadOnly(familyResults.ToArray()), evaluated, valid,
            invalidGeometry, invalidLateral, RefinementRounds, sameBasin, within.Length,
            shapeRepresentatives.Count, uncertain)
        {
            StructuredStartCount = structured.Length,
            LowDiscrepancyStartCount = lowDiscrepancy.Length,
            StartsGenerated = families.Length,
            ValidInitialStarts = initialValid.Length,
            InvalidInitialGeometry = initialGeometry,
            InvalidInitialLateral = initialLateral,
            InvalidInitialCornerControl = initialCornerControl,
            InvalidInitialStraightReposition = initialStraight,
            InvalidCornerControlDepartureCandidates = invalidDeparture,
            InvalidCrashCandidates = invalidCrash,
            InvalidStraightRepositionCandidates = invalidStraight,
            FinalRefinementStepsMeters = RefinementStepsMeters,
            ObjectiveConvergence = objectiveConvergence,
            GeometryConvergence = geometryConvergence,
            MultipleNearOptimalBasins = multipleNearOptimalBasins,
            LocalSearchConverged = localConverged,
            LocalPerturbationImprovementSeconds = localImprovement,
            PairPerturbationImprovementSeconds = pairImprovement,
            RefinedStartCount = selected.Count,
            ArchivedUnclosedBest = archivedBest,
            InitialLocalPerturbationImprovementSeconds = initialLocalImprovement,
            InitialPairPerturbationImprovementSeconds = initialPairImprovement,
            AdditionalAcceptedLocalMoves = additionalAcceptedMoves,
            TopThreeRefinementDiagnostics = Array.AsReadOnly(topThreeDiagnostics),
            ScheduledTopThreeRefinementDiagnostics = Array.AsReadOnly(scheduledTopThreeDiagnostics),
            ScheduledTopThreeStableByLastRound = scheduledTopThreeDiagnostics.All(item => item.Stable),
            TopThreeStable = topThreeStable,
        };
    }

    private static IReadOnlyList<FreeTrajectoryCandidate> SelectGeometryDiverse(
        IReadOnlyList<FreeTrajectoryEvaluation> candidates,
        int count)
    {
        var selected = new List<FreeTrajectoryCandidate>();
        foreach (var item in candidates)
        {
            if (selected.All(existing => RootMeanSquareDifference(
                    existing.ControlOffsetsMeters, item.Candidate.ControlOffsetsMeters) > .25f))
                selected.Add(item.Candidate);
            if (selected.Count == count) return Array.AsReadOnly(selected.ToArray());
        }
        foreach (var item in candidates)
        {
            if (selected.All(existing => existing.Id != item.Candidate.Id)) selected.Add(item.Candidate);
            if (selected.Count == count) break;
        }
        return Array.AsReadOnly(selected.ToArray());
    }

    private static IEnumerable<FreeTrajectoryCandidate> StructuredSeedFamilies(TrackGeometry geometry)
    {
        var width = LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, geometry);
        yield return Candidate("Flat-0", Enumerable.Repeat(0f, ControlStationCount));
        yield return Candidate("Flat-25", Enumerable.Repeat(.25f * width, ControlStationCount));
        yield return Candidate("Flat-50", Enumerable.Repeat(.50f * width, ControlStationCount));
        yield return Candidate("Flat-75", Enumerable.Repeat(.75f * width, ControlStationCount));
        yield return Candidate("Flat-100", Enumerable.Repeat(width, ControlStationCount));
        yield return Candidate("Linear-inward", Stations(p => width * (.75f - .50f * p)));
        yield return Candidate("Linear-outward", Stations(p => width * (.25f + .50f * p)));
        yield return Candidate("Symmetric-trough", Stations(p => width * (.25f + .50f * MathF.Abs(2f * p - 1f))));
        yield return Candidate("Low-trough", Stations(p => width * (.02f + .05f * MathF.Abs(2f * p - 1f))));
        yield return Candidate("Low-phase-neutral", Stations(p => width * (.04f + .02f * (float)Math.Sin(2f * MathF.PI * p + .7f))));
    }

    private static IEnumerable<FreeTrajectoryCandidate> LowDiscrepancySeedFamilies(TrackGeometry geometry)
    {
        var width = LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, geometry);
        var primes = new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31 };
        for (var start = 1; start <= LowDiscrepancyStartCount; start++)
        {
            var center = .05f + .90f * Halton(start, 37);
            var controls = new float[ControlStationCount];
            for (var station = 0; station < controls.Length; station++)
            {
                var local = center + .04f * (Halton(start, primes[station]) - .5f);
                controls[station] = width * Math.Clamp(local, .025f, .975f);
            }
            var smoothed = controls.ToArray();
            for (var station = 1; station < controls.Length - 1; station++)
                smoothed[station] = (controls[station - 1] + 2f * controls[station] + controls[station + 1]) / 4f;
            yield return Candidate($"Halton-{start:000}", smoothed);
        }
    }

    private static FreeTrajectoryCandidate PriorReviewedWinnerCandidate(TrackGeometry geometry)
    {
        var haltonOne = LowDiscrepancySeedFamilies(geometry).First();
        return Candidate(
            "Reviewed HEAD 841ecee winner",
            haltonOne.ControlOffsetsMeters.Select(value => value - .75f));
    }

    private static FreeTrajectoryCandidate PriorReviewedBroadLateWinnerCandidate() => Candidate(
        "Reviewed HEAD 142b638 broad-late winner",
        new[]
        {
            2.4562569f, 1.8950047f, 1.9517355f, 1.938034f, 1.7392473f, 1.6958838f,
            1.8788095f, 1.9487152f, 1.8226433f, 1.704401f, 1.9246602f,
        });

    private static float Halton(int index, int basis)
    {
        var fraction = 1d;
        var value = 0d;
        while (index > 0)
        {
            fraction /= basis;
            value += fraction * (index % basis);
            index /= basis;
        }
        return (float)value;
    }

    private static IReadOnlyList<float> Stations(Func<float, float> selector)
        => Array.AsReadOnly(Enumerable.Range(0, ControlStationCount)
            .Select(index => selector((float)index / (ControlStationCount - 1))).ToArray());

    private static FreeTrajectoryCandidate Candidate(string family, IEnumerable<float> controls)
        => new(family, Array.AsReadOnly(controls.ToArray()));

    private static float RootMeanSquareDifference(IReadOnlyList<float> first, IReadOnlyList<float> second)
    {
        var sum = 0d;
        for (var index = 0; index < first.Count; index++)
        {
            var difference = first[index] - second[index];
            sum += difference * difference;
        }
        return (float)Math.Sqrt(sum / first.Count);
    }

    private static IReadOnlyList<TurningDemandConstraint> BuildTurningDemandConstraints(
        FreeTrajectoryPath path,
        float referenceTurnSpeedMetersPerSecond)
    {
        var points = path.Samples;
        var canonical = ConstraintAt(path, .5f, referenceTurnSpeedMetersPerSecond, isCanonicalBaseline: true);
        var correctionCapability = LongitudinalDynamics
            .CalculateCornerCorrectionDecelerationMetersPerSecondSquared(Balanced, UniformSurface);
        if (points.All(item => item.LateralOffsetMeters == points[0].LateralOffsetMeters))
            return Array.AsReadOnly(new[] { WithBindingMetadata(canonical, correctionCapability) });

        return ReduceToBindingSuffixEnvelope(
            BuildSampledTurningDemandConstraints(path, referenceTurnSpeedMetersPerSecond),
            correctionCapability);
    }

    private static IReadOnlyList<TurningDemandConstraint> BuildSampledTurningDemandConstraints(
        FreeTrajectoryPath path,
        float referenceTurnSpeedMetersPerSecond)
    {
        var canonical = ConstraintAt(path, .5f, referenceTurnSpeedMetersPerSecond, isCanonicalBaseline: true);
        var correctionCapability = LongitudinalDynamics
            .CalculateCornerCorrectionDecelerationMetersPerSecondSquared(Balanced, UniformSurface);
        var referenceDriveForce = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
            Balanced,
            Neutral,
            UniformSurface);
        var sampled = new TurningDemandConstraint[path.Samples.Count];
        for (var index = 0; index < path.Samples.Count; index++)
        {
            var point = path.Samples[index];
            if (point.Progress == canonical.Progress)
            {
                sampled[index] = canonical;
                continue;
            }
            var localSettled = SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
                point.LocalRadiusMeters,
                referenceTurnSpeedMetersPerSecond,
                UniformSurface,
                Balanced,
                Neutral);
            var canonicalBaseline = CanonicalBaselineSpeedAt(
                path,
                canonical,
                point.Progress,
                correctionCapability,
                referenceDriveForce);
            var capabilitySquaredDelta = localSettled * localSettled
                - canonical.LocalSettledCapabilityMetersPerSecond
                    * canonical.LocalSettledCapabilityMetersPerSecond;
            sampled[index] = new TurningDemandConstraint(
                point.Progress,
                point.CumulativeDistanceMeters,
                point.LocalRadiusMeters,
                point.CurvaturePerMeter,
                DifferentialSupplementalCapabilityMetersPerSecond(
                    canonicalBaseline,
                    canonical.LocalSettledCapabilityMetersPerSecond,
                    localSettled))
            {
                CanonicalBaselineSpeedMetersPerSecond = canonicalBaseline,
                LocalSettledCapabilityMetersPerSecond = localSettled,
                SettledCapabilitySquaredDeltaMetersSquaredPerSecondSquared = capabilitySquaredDelta,
            };
        }
        return Array.AsReadOnly(sampled);
    }

    private static IReadOnlyList<TurningDemandConstraint> ReduceToBindingSuffixEnvelope(
        IReadOnlyList<TurningDemandConstraint> sampled,
        float correctionCapability)
    {
        var retained = new bool[sampled.Count];
        var suffixMinimum = double.PositiveInfinity;
        for (var index = sampled.Count - 1; index >= 0; index--)
        {
            var intercept = EnvelopeIntercept(sampled[index], correctionCapability);
            if (intercept >= suffixMinimum) continue;
            retained[index] = true;
            suffixMinimum = intercept;
        }

        var reduced = new List<TurningDemandConstraint>();
        for (var index = 0; index < sampled.Count; index++)
        {
            if (!retained[index] && !sampled[index].IsCanonicalBaseline) continue;
            reduced.Add(retained[index]
                ? WithBindingMetadata(sampled[index], correctionCapability)
                : sampled[index]);
        }
        return Array.AsReadOnly(reduced.ToArray());
    }

    private static TurningDemandConstraint WithBindingMetadata(
        TurningDemandConstraint constraint,
        float correctionCapability)
        => constraint with
        {
            IsBindingEnvelopePoint = true,
            BackwardReachableEntryTargetMetersPerSecond = (float)Math.Sqrt(Math.Max(0d,
                EnvelopeIntercept(constraint, correctionCapability))),
        };

    private static double EnvelopeIntercept(
        TurningDemandConstraint constraint,
        float correctionCapability)
        => (double)constraint.SettledCapabilityMetersPerSecond
            * constraint.SettledCapabilityMetersPerSecond
            + 2d * correctionCapability * constraint.DistanceMeters;

    private static float? ReducedLookaheadTarget(
        IReadOnlyList<TurningDemandConstraint> constraints,
        float distanceMeters,
        float correctionCapability)
    {
        var lower = 0;
        var upper = constraints.Count;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            if (constraints[middle].DistanceMeters < distanceMeters - 1e-4f) lower = middle + 1;
            else upper = middle;
        }
        for (var index = lower; index < constraints.Count; index++)
        {
            var constraint = constraints[index];
            if (!constraint.IsBindingEnvelopePoint) continue;
            return (float)Math.Sqrt(Math.Max(0d,
                EnvelopeIntercept(constraint, correctionCapability)
                - 2d * correctionCapability * distanceMeters));
        }
        return null;
    }

    private static float? BruteForceLookaheadTarget(
        IReadOnlyList<TurningDemandConstraint> sampled,
        float distanceMeters,
        float correctionCapability)
    {
        var minimumIntercept = double.PositiveInfinity;
        foreach (var constraint in sampled)
        {
            if (constraint.DistanceMeters < distanceMeters - 1e-4f) continue;
            minimumIntercept = Math.Min(minimumIntercept, EnvelopeIntercept(constraint, correctionCapability));
        }
        return double.IsPositiveInfinity(minimumIntercept) ? null : (float)Math.Sqrt(Math.Max(0d,
            minimumIntercept - 2d * correctionCapability * distanceMeters));
    }

    private static TurningDemandConstraint ConstraintAt(
        FreeTrajectoryPath path,
        float progress,
        float referenceTurnSpeedMetersPerSecond,
        bool isCanonicalBaseline = false)
    {
        var point = path.PointAt(progress);
        var localCapability = SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
            point.LocalRadiusMeters,
            referenceTurnSpeedMetersPerSecond,
            UniformSurface,
            Balanced,
            Neutral);
        return new TurningDemandConstraint(
            progress,
            path.DistanceAtProgress(progress),
            point.LocalRadiusMeters,
            point.CurvaturePerMeter,
            localCapability)
        {
            IsCanonicalBaseline = isCanonicalBaseline,
            CanonicalBaselineSpeedMetersPerSecond = localCapability,
            LocalSettledCapabilityMetersPerSecond = localCapability,
            SettledCapabilitySquaredDeltaMetersSquaredPerSecondSquared = 0f,
        };
    }

    public static float DifferentialSupplementalCapabilityMetersPerSecond(
        float canonicalBaselineSpeedMetersPerSecond,
        float canonicalSettledCapabilityMetersPerSecond,
        float localSettledCapabilityMetersPerSecond)
    {
        var capabilitySquaredDelta = localSettledCapabilityMetersPerSecond
                * localSettledCapabilityMetersPerSecond
            - canonicalSettledCapabilityMetersPerSecond
                * canonicalSettledCapabilityMetersPerSecond;
        if (capabilitySquaredDelta >= 0f)
            return canonicalBaselineSpeedMetersPerSecond;
        return MathF.Sqrt(MathF.Max(0f,
            canonicalBaselineSpeedMetersPerSecond * canonicalBaselineSpeedMetersPerSecond
            + capabilitySquaredDelta));
    }

    private static float CanonicalBaselineSpeedAt(
        FreeTrajectoryPath path,
        TurningDemandConstraint canonical,
        float progress,
        float correctionCapability,
        float referenceDriveForce)
    {
        var distance = path.DistanceAtProgress(progress);
        if (progress <= canonical.Progress)
            return (float)Math.Sqrt((double)canonical.SettledCapabilityMetersPerSecond
                * canonical.SettledCapabilityMetersPerSecond
                + 2d * correctionCapability * MathF.Max(0f, canonical.DistanceMeters - distance));

        var speed = canonical.SettledCapabilityMetersPerSecond;
        var traversed = canonical.DistanceMeters;
        var step = LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters;
        while (traversed + step < distance - 1e-6f)
        {
            var midpoint = path.ProgressAtDistance(traversed + step * .5f);
            speed = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
                speed,
                step,
                referenceDriveForce,
                Neutral,
                ContinuousCornerEnvelope.DriveAvailability(midpoint));
            traversed += step;
        }
        var remainder = distance - traversed;
        if (remainder > 1e-6f)
        {
            var midpoint = path.ProgressAtDistance(traversed + remainder * .5f);
            speed = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
                speed,
                remainder,
                referenceDriveForce,
                Neutral,
                ContinuousCornerEnvelope.DriveAvailability(midpoint));
        }
        return speed;
    }

    private static CornerSpeedCapabilitySensitivityResult RunCornerSpeedSensitivity(
        TrackGeometry geometry,
        float entrySpeedMetersPerSecond,
        TrajectoryObservation e0a1x0,
        TrajectoryObservation e0a1x1)
    {
        var controls = BuildCornerSpeedSensitivityControls(geometry, e0a1x0, e0a1x1);
        var points = CornerSpeedSensitivityReferenceSweepMetersPerSecond.Select(reference =>
        {
            var evaluator = new ExperimentalGeometryReplay(
                geometry,
                entrySpeedMetersPerSecond,
                reference,
                useCanonicalSensitivityConstraint: true);
            var observations = controls.Select(control => new CornerSpeedSensitivityObservation(
                control.Name,
                control.ShapeDescription,
                control.IsDynamic,
                evaluator.Evaluate(control.Candidate, entrySpeedMetersPerSecond))).ToArray();
            var invalid = observations.FirstOrDefault(item => !item.Evaluation.IsValid);
            if (invalid is not null)
                throw new InvalidOperationException(
                    $"Sensitivity control {invalid.Name} is invalid at {reference}: "
                    + invalid.Evaluation.InvalidReason);
            var bestConstant = observations.Where(item => !item.IsDynamic)
                .OrderBy(item => item.Evaluation.SectorTimeSeconds)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .First();
            var e0a1x1 = observations.Single(item => item.Name == "#46 E0-A1-X1");
            var bestDynamic = observations.Where(item => item.IsDynamic)
                .OrderBy(item => item.Evaluation.SectorTimeSeconds)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .First();
            var constantInner = observations.Single(item => item.Name == "ConstantInner");
            var repeated = RunRepeatedLap(
                evaluator,
                constantInner.Evaluation.Candidate,
                entrySpeedMetersPerSecond);
            if (!repeated.Converged)
                throw new InvalidOperationException(
                    $"ConstantInner sensitivity lap did not converge at {reference} m/s.");
            var settled = SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
                31f,
                reference,
                UniformSurface,
                Balanced,
                Neutral);
            return new CornerSpeedSensitivityPoint(
                reference,
                settled,
                Array.AsReadOnly(observations),
                bestConstant,
                e0a1x1,
                bestDynamic,
                repeated);
        }).ToArray();

        var fine = Enumerable.Range(0, 17).Select(index => 19f + index * .25f)
            .Select(reference =>
            {
                var evaluator = new ExperimentalGeometryReplay(
                    geometry,
                    entrySpeedMetersPerSecond,
                    reference,
                    useCanonicalSensitivityConstraint: true);
                var observations = controls.Select(control => new CornerSpeedSensitivityObservation(
                    control.Name,
                    control.ShapeDescription,
                    control.IsDynamic,
                    evaluator.Evaluate(control.Candidate, entrySpeedMetersPerSecond))).ToArray();
                var bestConstant = observations.Where(item => !item.IsDynamic && item.Evaluation.IsValid)
                    .MinBy(item => item.Evaluation.SectorTimeSeconds)!;
                var bestDynamic = observations.Where(item => item.IsDynamic && item.Evaluation.IsValid)
                    .MinBy(item => item.Evaluation.SectorTimeSeconds)!;
                return (Reference: reference,
                    Delta: bestDynamic.Evaluation.SectorTimeSeconds
                           - bestConstant.Evaluation.SectorTimeSeconds);
        }).ToArray();
        float? crossover = null;
        if (MathF.Abs(fine[0].Delta) <= 1e-6f)
        {
            crossover = fine[0].Reference;
        }
        else
        {
            for (var index = 1; index < fine.Length; index++)
            {
                if (MathF.Abs(fine[index].Delta) <= 1e-6f)
                {
                    crossover = fine[index].Reference;
                    break;
                }
                var previous = fine[index - 1];
                var current = fine[index];
                if (MathF.Sign(previous.Delta) == MathF.Sign(current.Delta)) continue;
                var totalMagnitude = MathF.Abs(previous.Delta) + MathF.Abs(current.Delta);
                crossover = totalMagnitude <= 1e-9f
                    ? current.Reference
                    : previous.Reference + .25f * MathF.Abs(previous.Delta) / totalMagnitude;
                break;
            }
        }

        var improvement = fine[0].Delta - fine[^1].Delta;
        var globalSpeedBreaksAtCrossover = false;
        var constantInnerControl = controls.Single(item => item.Name == "ConstantInner");
        bool BreaksGlobalSpeedGuardrail(float reference)
        {
            var evaluator = new ExperimentalGeometryReplay(
                geometry,
                entrySpeedMetersPerSecond,
                reference,
                useCanonicalSensitivityConstraint: true);
            var lap = RunRepeatedLap(
                evaluator,
                constantInnerControl.Candidate,
                entrySpeedMetersPerSecond);
            return lap.Converged
                && (lap.MaximumSpeedMetersPerSecond * 3.6f
                        > RealMotoarenaMaximumSpeedGuardrailKilometersPerHour
                           * (1f + ClearlyBrokenGlobalSpeedFraction)
                    || lap.AverageSpeedMetersPerSecond
                        > RealMotoarenaAverageSpeedGuardrailMetersPerSecond
                          * (1f + ClearlyBrokenGlobalSpeedFraction)
                    || lap.FlyingLapTimeSeconds
                        < RealMotoarenaFlyingLapGuardrailSeconds
                          * (1f - ClearlyBrokenGlobalSpeedFraction));
        }

        float? firstGlobalSpeedGuardrailBreak = null;
        foreach (var item in fine)
        {
            if (!BreaksGlobalSpeedGuardrail(item.Reference)) continue;
            firstGlobalSpeedGuardrailBreak = item.Reference;
            break;
        }
        if (crossover is { } crossoverReference)
            globalSpeedBreaksAtCrossover = BreaksGlobalSpeedGuardrail(crossoverReference);

        var materialChange = MathF.Abs(improvement) >= .02f;
        var globalSpeedGuardrailBreaksBeforeCrossover = firstGlobalSpeedGuardrailBreak is not null
            && crossover is not null
            && firstGlobalSpeedGuardrailBreak.Value < crossover.Value;
        var classification = globalSpeedGuardrailBreaksBeforeCrossover ? "S4"
            : !materialChange ? "S3"
            : crossover is not null ? "S1" : "S2";
        var interpretation = classification switch
        {
            "S1" => "Strong sensitivity — corner-speed capability materially controls racing-line economics",
            "S2" => "Moderate sensitivity — corner-speed capability contributes, but is not sufficient",
            "S3" => "Weak sensitivity — corner-speed capability is not the primary cause",
            _ => "Global speed breaks first — do not solve line economics by simply raising corner-speed capability",
        };
        var material = materialChange;
        var raisingLooksCorrect = classification == "S3" ? "NO"
            : classification == "S4" ? "NO" : "UNCERTAIN";
        return new CornerSpeedCapabilitySensitivityResult(
            CornerSpeedSensitivityReferenceSweepMetersPerSecond,
            controls,
            Array.AsReadOnly(points),
            crossover,
            globalSpeedBreaksAtCrossover,
            firstGlobalSpeedGuardrailBreak,
            globalSpeedGuardrailBreaksBeforeCrossover,
            classification,
            interpretation,
            material,
            raisingLooksCorrect);
    }

    private static IReadOnlyList<CornerSpeedSensitivityControl> BuildCornerSpeedSensitivityControls(
        TrackGeometry geometry,
        TrajectoryObservation e0a1x0,
        TrajectoryObservation e0a1x1)
    {
        var width = LaneModel.UsableRacingWidthMeters(SegmentType.TurnMiddle, geometry);
        var result = new List<CornerSpeedSensitivityControl>();
        var names = new[] { "ConstantInner", "ConstantL1", "ConstantL2", "ConstantL3", "ConstantOuter" };
        for (var lane = 0; lane < LaneModel.LanesCount; lane++)
        {
            var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
                lane,
                SegmentType.TurnMiddle,
                geometry);
            result.Add(new CornerSpeedSensitivityControl(
                names[lane],
                $"constant lane {lane} physical offset",
                false,
                Candidate(names[lane], Enumerable.Repeat(offset, ControlStationCount))));
        }

        AddPhysicalDynamic("#46 E0-A1-X0",
            "fixed 65%-amplitude C2 projection through the averaged #46 p=0,1/3,1/2,2/3,1 observed waypoints, plus 0.05 m inner-boundary clearance",
            ControlVectorFrom46(e0a1x0, geometry).Select(value => .05f + .65f * value));
        AddPhysicalDynamic("#46 E0-A1-X1",
            "fixed 65%-amplitude C2 projection through the averaged #46 p=0,1/3,1/2,2/3,1 observed waypoints, plus 0.05 m inner-boundary clearance",
            ControlVectorFrom46(e0a1x1, geometry).Select(value => .05f + .65f * value));
        AddDynamic("MildDynamic",
            "manual symmetric mild radius release; width fractions .04 + .04*sin(pi*p)^2 at p=0,.1,...,1",
            Stations(progress => .04f + .04f * MathF.Pow(MathF.Sin(MathF.PI * progress), 2f)));
        AddDynamic("ModerateDynamic",
            "manual symmetric moderate radius release; width fractions .05 + .12*sin(pi*p)^2 at p=0,.1,...,1",
            Stations(progress => .05f + .12f * MathF.Pow(MathF.Sin(MathF.PI * progress), 2f)));
        AddDynamic("WideEntryEarlyTightRelease",
            "manual early inward transition and gradual release; width fractions [.12,.105,.09,.075,.06,.05,.05,.06,.075,.09,.10]",
            new[] { .12f, .105f, .09f, .075f, .06f, .05f, .05f, .06f, .075f, .09f, .10f });
        return Array.AsReadOnly(result.ToArray());

        void AddDynamic(string name, string description, IEnumerable<float> widthFractions)
        {
            AddPhysicalDynamic(name, description, widthFractions.Select(value => value * width));
        }


        void AddPhysicalDynamic(string name, string description, IEnumerable<float> offsetsMeters)
            => result.Add(new CornerSpeedSensitivityControl(
                name,
                description,
                true,
                Candidate(name, offsetsMeters)));
    }

    private static RepeatedTrajectoryLapResult RunRepeatedLap(
        ExperimentalGeometryReplay evaluator,
        FreeTrajectoryCandidate candidate,
        float initialEntrySpeed)
    {
        var entry = initialEntrySpeed;
        var lastLap = 0f;
        for (var iteration = 1; iteration <= 20; iteration++)
        {
            var first = evaluator.Evaluate(candidate, entry);
            if (!first.IsValid) return new RepeatedTrajectoryLapResult(0f, entry, iteration, false);
            var second = evaluator.Evaluate(candidate, first.EndStraightSpeedMetersPerSecond);
            if (!second.IsValid) return new RepeatedTrajectoryLapResult(0f, entry, iteration, false);
            lastLap = first.SectorTimeSeconds + second.SectorTimeSeconds;
            var nextEntry = second.EndStraightSpeedMetersPerSecond;
            if (MathF.Abs(nextEntry - entry) <= .01f)
            {
                var maximumSpeed = new[]
                {
                    entry,
                    first.Profile.Max(item => item.SpeedMetersPerSecond),
                    first.EndStraightSpeedMetersPerSecond,
                    second.Profile.Max(item => item.SpeedMetersPerSecond),
                    second.EndStraightSpeedMetersPerSecond,
                }.Max();
                var distance = first.Path!.TotalLengthMeters + first.StraightRepositionDistanceMeters
                    + second.Path!.TotalLengthMeters + second.StraightRepositionDistanceMeters;
                return new RepeatedTrajectoryLapResult(lastLap, nextEntry, iteration, true)
                {
                    MaximumSpeedMetersPerSecond = maximumSpeed,
                    AverageSpeedMetersPerSecond = distance / lastLap,
                };
            }
            entry = nextEntry;
        }
        return new RepeatedTrajectoryLapResult(lastLap, entry, 20, false);
    }

    private static StraightSpeedProfile StraightProfile(float entrySpeed, float distance)
        => LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(
            entrySpeed, Balanced, Neutral, UniformSurface,
            LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
                Balanced, UniformSurface), distance);

    private static Track CreateTrack() => MatchedVenueProfiles.Motoarena2026.CreateTrack(
        MatchedVenueProfiles.MotoarenaPrimaryStartLineToFirstCornerMeters);

    private sealed record ProductionSectorObservation(
        float CornerTimeSeconds,
        float StraightTimeSeconds,
        float SectorTimeSeconds,
        float ExitSpeedMetersPerSecond,
        IReadOnlyList<ContinuousCornerNode> CornerNodes);

    private sealed class ExperimentalGeometryReplay
    {
        private readonly TrackGeometry geometry;
        private readonly float defaultEntrySpeed;
        private readonly float correctionCapability;
        private readonly float referenceDriveForce;
        private readonly float referenceTurnSpeedMetersPerSecond;
        private readonly bool useCanonicalSensitivityConstraint;
        private readonly float turningLossRatio;

        internal ExperimentalGeometryReplay(
            TrackGeometry geometry,
            float defaultEntrySpeed,
            float referenceTurnSpeedMetersPerSecond =
                SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond,
            bool useCanonicalSensitivityConstraint = false,
            float turningLossRatio = 0f)
        {
            this.geometry = geometry;
            this.defaultEntrySpeed = defaultEntrySpeed;
            if (!float.IsFinite(referenceTurnSpeedMetersPerSecond)
                || referenceTurnSpeedMetersPerSecond <= 0f)
                throw new ArgumentOutOfRangeException(nameof(referenceTurnSpeedMetersPerSecond));
            this.referenceTurnSpeedMetersPerSecond = referenceTurnSpeedMetersPerSecond;
            this.useCanonicalSensitivityConstraint = useCanonicalSensitivityConstraint;
            if (!float.IsFinite(turningLossRatio) || turningLossRatio < 0f)
                throw new ArgumentOutOfRangeException(nameof(turningLossRatio));
            this.turningLossRatio = turningLossRatio;
            correctionCapability = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
                Balanced, UniformSurface);
            referenceDriveForce = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(
                Balanced, Neutral, UniformSurface);
        }

        internal FreeTrajectoryEvaluation Evaluate(FreeTrajectoryCandidate candidate, float? entrySpeed = null)
        {
            FreeTrajectoryPath path;
            try
            {
                path = FreeContinuousTrajectoryGeometry.Build(candidate.ControlOffsetsMeters, geometry);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Invalid(candidate, FreeTrajectoryValidity.TrackBoundary, "TrackBoundary");
            }
            catch (InvalidDataException exception) when (exception.Message == "TrackBoundary")
            {
                return Invalid(candidate, FreeTrajectoryValidity.TrackBoundary, exception.Message);
            }
            catch (InvalidDataException exception) when (exception.Message == "SelfIntersection")
            {
                return Invalid(candidate, FreeTrajectoryValidity.SelfIntersection, exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return Invalid(candidate, FreeTrajectoryValidity.NonSmoothGeometry, exception.Message);
            }

            var speed = entrySpeed ?? defaultEntrySpeed;
            if (!float.IsFinite(speed) || speed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(entrySpeed));
            var constraints = useCanonicalSensitivityConstraint
                ? Array.AsReadOnly(new[]
                {
                    WithBindingMetadata(
                        ConstraintAt(path, .5f, referenceTurnSpeedMetersPerSecond, isCanonicalBaseline: true),
                        correctionCapability),
                })
                : BuildTurningDemandConstraints(path, referenceTurnSpeedMetersPerSecond);
            var integrationProgress = IntegrationProgress(path, constraints);
            var cornerTime = 0d;
            var correctionDistance = 0d;
            var correctionTime = 0d;
            var minimumHeadroom = float.PositiveInfinity;
            var peakProxy = 0f;
            var weightedRadius = 0d;
            var weightedRadiusTime = 0d;
            var peakSettledRatio = 0f;
            var peakEnvelopeRatio = 0f;
            var peakSettledProgress = 0f;
            var peakEnvelopeProgress = 0f;
            var brakeSteps = 0;
            var firstCorrectionProgress = 1f;
            var turningLossEnergy = 0d;
            var peakTurningLossPower = 0f;

            void Observe(float progress, float observedSpeed, FreeTrajectoryGeometryPoint point, float? target)
            {
                var settled = SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
                    point.LocalRadiusMeters,
                    referenceTurnSpeedMetersPerSecond,
                    UniformSurface,
                    Balanced,
                    Neutral);
                var settledRatio = observedSpeed / settled;
                if (settledRatio > peakSettledRatio)
                {
                    peakSettledRatio = settledRatio;
                    peakSettledProgress = progress;
                }
                if (target is not { } envelopeTarget || envelopeTarget <= 0f) return;
                var envelopeRatio = observedSpeed / envelopeTarget;
                if (envelopeRatio > peakEnvelopeRatio)
                {
                    peakEnvelopeRatio = envelopeRatio;
                    peakEnvelopeProgress = progress;
                }
            }

            var firstPoint = path.PointAt(0f);
            var firstTarget = LookaheadTarget(constraints, 0f);
            var firstOutcome = ResolveOutcome(firstPoint, speed, firstTarget);
            Observe(0f, speed, firstPoint, firstTarget);
            if (firstOutcome.Outcome is SegmentOutcome.RunWide or SegmentOutcome.Crash)
                return Invalid(candidate,
                    firstOutcome.Outcome == SegmentOutcome.RunWide
                        ? FreeTrajectoryValidity.CornerControlPathDeparture
                        : FreeTrajectoryValidity.CornerControlCrash,
                    firstOutcome.Outcome == SegmentOutcome.RunWide
                        ? "CornerControlPathDeparture"
                        : "CornerControlCrash",
                    path, constraints, peakSettledRatio, peakEnvelopeRatio,
                    peakSettledProgress, peakEnvelopeProgress,
                    runWideInvalidations: firstOutcome.Outcome == SegmentOutcome.RunWide ? 1 : 0,
                    crashInvalidations: firstOutcome.Outcome == SegmentOutcome.Crash ? 1 : 0);

            var nodes = new List<ReplayNode>
            {
                new(0f, 0f, speed, false, 0f, 0f, firstTarget ?? 0f, firstOutcome.Outcome),
            };
            for (var index = 1; index < integrationProgress.Count; index++)
            {
                var startProgress = integrationProgress[index - 1];
                var endProgress = integrationProgress[index];
                var startDistance = path.DistanceAtProgress(startProgress);
                var endDistance = path.DistanceAtProgress(endProgress);
                var stepDistance = endDistance - startDistance;
                var midpointProgress = path.ProgressAtDistance((startDistance + endDistance) * .5f);
                var startPoint = path.PointAt(startProgress);
                var endPoint = path.PointAt(endProgress);
                var midpointPoint = path.PointAt(midpointProgress);
                var startTarget = LookaheadTarget(constraints, startDistance);
                var endTarget = LookaheadTarget(constraints, endDistance);
                var startOutcome = ResolveOutcome(startPoint, speed, startTarget);
                Observe(startProgress, speed, startPoint, startTarget);
                if (startOutcome.Outcome is SegmentOutcome.RunWide or SegmentOutcome.Crash)
                    return Invalid(candidate,
                        startOutcome.Outcome == SegmentOutcome.RunWide
                            ? FreeTrajectoryValidity.CornerControlPathDeparture
                            : FreeTrajectoryValidity.CornerControlCrash,
                        startOutcome.Outcome == SegmentOutcome.RunWide
                            ? "CornerControlPathDeparture"
                            : "CornerControlCrash",
                        path, constraints, peakSettledRatio, peakEnvelopeRatio,
                        peakSettledProgress, peakEnvelopeProgress, brakeSteps, firstCorrectionProgress,
                        runWideInvalidations: startOutcome.Outcome == SegmentOutcome.RunWide ? 1 : 0,
                        crashInvalidations: startOutcome.Outcome == SegmentOutcome.Crash ? 1 : 0);
                var availability = ContinuousCornerEnvelope.DriveAvailability(midpointProgress);
                var candidateSpeed = turningLossRatio == 0f
                    ? LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
                        speed, stepDistance, referenceDriveForce, Neutral, availability)
                    : TurningCostMidpointEndSpeed(speed, stepDistance, midpointPoint.CurvaturePerMeter,
                        availability);
                var correctionActive = endTarget is { } target
                    && (speed > target
                        || candidateSpeed > target
                        || startOutcome.ContinuousCorrectionTargetSpeedMetersPerSecond is not null);
                float nextSpeed;
                double stepTime;
                float netAcceleration;
                if (correctionActive)
                {
                    var correction = LongitudinalDynamics.CalculateCornerSpeedCorrectionProfile(
                        speed, endTarget!.Value, correctionCapability, stepDistance);
                    nextSpeed = correction.ExitSpeedMetersPerSecond;
                    var carry = MathF.Max(0f, stepDistance - correction.CorrectionDistanceMeters);
                    var carryTime = nextSpeed <= 0f ? 0d : carry / nextSpeed;
                    stepTime = correction.TravelTimeSeconds + carryTime;
                    correctionDistance += correction.CorrectionDistanceMeters;
                    correctionTime += correction.TravelTimeSeconds;
                    netAcceleration = stepDistance <= 0f ? 0f
                        : (nextSpeed * nextSpeed - speed * speed) / (2f * stepDistance);
                    brakeSteps++;
                    firstCorrectionProgress = MathF.Min(firstCorrectionProgress, startProgress);
                }
                else
                {
                    nextSpeed = candidateSpeed;
                    stepTime = 2d * stepDistance / (speed + nextSpeed);
                    var midpointSpeed = (speed + nextSpeed) * .5f;
                    netAcceleration = availability * LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(
                        midpointSpeed, referenceDriveForce, Neutral);
                    if (turningLossRatio > 0f)
                    {
                        var loss = TurningCostForce.LossNewtons(turningLossRatio, midpointSpeed,
                            midpointPoint.CurvaturePerMeter, availability, referenceDriveForce, Neutral);
                        netAcceleration -= loss / LongitudinalDynamics.ProvisionalNominalSystemMassKilograms;
                        turningLossEnergy += (double)loss * stepDistance;
                        peakTurningLossPower = MathF.Max(peakTurningLossPower, loss * midpointSpeed);
                    }
                }
                var endOutcome = ResolveOutcome(endPoint, nextSpeed, endTarget);
                Observe(endProgress, nextSpeed, endPoint, endTarget);
                if (endOutcome.Outcome is SegmentOutcome.RunWide or SegmentOutcome.Crash)
                    return Invalid(candidate,
                        endOutcome.Outcome == SegmentOutcome.RunWide
                            ? FreeTrajectoryValidity.CornerControlPathDeparture
                            : FreeTrajectoryValidity.CornerControlCrash,
                        endOutcome.Outcome == SegmentOutcome.RunWide
                            ? "CornerControlPathDeparture"
                            : "CornerControlCrash",
                        path, constraints, peakSettledRatio, peakEnvelopeRatio,
                        peakSettledProgress, peakEnvelopeProgress, brakeSteps, firstCorrectionProgress,
                        runWideInvalidations: endOutcome.Outcome == SegmentOutcome.RunWide ? 1 : 0,
                        crashInvalidations: endOutcome.Outcome == SegmentOutcome.Crash ? 1 : 0);
                var requiredLateral = MathF.Abs(endPoint.LateralOffsetMeters
                    - startPoint.LateralOffsetMeters);
                var maximumLateralAllowed = LateralMovementModel.CalculateMaxLateralDistanceMeters(
                    (float)stepTime, SegmentType.TurnMiddle, geometry, UniformSurface, Balanced);
                var headroom = maximumLateralAllowed - requiredLateral;
                minimumHeadroom = MathF.Min(minimumHeadroom, headroom);
                if (headroom < -1e-4f)
                    return Invalid(candidate, FreeTrajectoryValidity.LateralExecutionConstraint,
                        "LateralExecutionConstraint", path, constraints,
                        peakSettledRatio, peakEnvelopeRatio,
                        peakSettledProgress, peakEnvelopeProgress, brakeSteps, firstCorrectionProgress);
                cornerTime += stepTime;
                var midpointSpeedForProxy = (speed + nextSpeed) * .5f;
                var proxy = midpointSpeedForProxy * midpointSpeedForProxy / midpointPoint.LocalRadiusMeters;
                peakProxy = MathF.Max(peakProxy, proxy);
                weightedRadius += midpointPoint.LocalRadiusMeters * stepTime;
                weightedRadiusTime += stepTime;
                speed = nextSpeed;
                nodes.Add(new ReplayNode(endProgress, endDistance, speed, correctionActive,
                    availability, netAcceleration, endTarget ?? 0f, endOutcome.Outcome));
            }

            var exitSpeed = speed;
            var nextEntryLateral = candidate.ControlOffsetsMeters[0];
            var exitLateral = candidate.ControlOffsetsMeters[^1];
            var repositionLateral = MathF.Abs(nextEntryLateral - exitLateral);
            var straightDistance = (float)Math.Sqrt(
                (double)geometry.StraightLengthMeters * geometry.StraightLengthMeters
                + (double)repositionLateral * repositionLateral);
            var straight10 = StraightProfile(exitSpeed, 10f);
            var straight20 = StraightProfile(exitSpeed, 20f);
            var straight40 = StraightProfile(exitSpeed, 40f);
            var straightFull = StraightProfile(exitSpeed, straightDistance);
            var straightLateralCapacity = LateralMovementModel.CalculateMaxLateralDistanceMeters(
                straightFull.TravelTimeSeconds, SegmentType.Straight, geometry, UniformSurface, Balanced);
            var straightHeadroom = straightLateralCapacity - repositionLateral;
            minimumHeadroom = MathF.Min(minimumHeadroom, straightHeadroom);
            if (straightHeadroom < -1e-4f)
                return Invalid(candidate, FreeTrajectoryValidity.StraightRepositionConstraint,
                    "StraightRepositionConstraint", path, constraints,
                    peakSettledRatio, peakEnvelopeRatio,
                    peakSettledProgress, peakEnvelopeProgress, brakeSteps, firstCorrectionProgress,
                    nextEntryLateral: nextEntryLateral,
                    straightRepositionDistance: straightDistance,
                    straightHeadroom: straightHeadroom);
            var profile = Enumerable.Range(0, 21).Select(index =>
            {
                var progress = index * .05f;
                var point = path.PointAt(progress);
                var node = Interpolate(nodes, path.DistanceAtProgress(progress));
                var safe = SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
                    point.LocalRadiusMeters,
                    referenceTurnSpeedMetersPerSecond,
                    UniformSurface,
                    Balanced,
                    Neutral);
                var availableDrive = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(
                    referenceDriveForce, node.SpeedMetersPerSecond, Neutral)
                    * ContinuousCornerEnvelope.DriveAvailability(progress);
                var profileLoss = node.CorrectionActive ? 0f : TurningCostForce.LossNewtons(
                    turningLossRatio, node.SpeedMetersPerSecond, point.CurvaturePerMeter,
                    ContinuousCornerEnvelope.DriveAvailability(progress), referenceDriveForce, Neutral);
                return new FreeTrajectoryProfilePoint(
                    progress, point.LateralOffsetMeters, point.LateralPosition,
                    node.SpeedMetersPerSecond, safe, point.LocalRadiusMeters,
                    point.CurvaturePerMeter,
                    node.SpeedMetersPerSecond * node.SpeedMetersPerSecond / point.LocalRadiusMeters,
                    point.HeadingDeltaRadians, node.CorrectionActive,
                    ContinuousCornerEnvelope.DriveAvailability(progress),
                    node.NetAccelerationMetersPerSecondSquared,
                    node.LookaheadTargetMetersPerSecond,
                    node.CornerControlOutcome)
                {
                    TurningLossPowerWatts = profileLoss * node.SpeedMetersPerSecond,
                    LongitudinalDriveRemainingNewtons = node.CorrectionActive ? 0f
                        : MathF.Max(0f, availableDrive - profileLoss),
                };
            }).ToArray();
            var allGeometry = path.Samples;
            var minimumLateral = allGeometry.MinBy(item => item.LateralOffsetMeters)!;
            var maximumLateral = allGeometry.MaxBy(item => item.LateralOffsetMeters)!;
            var maximumCurvatureValue = allGeometry.Max(item => item.CurvaturePerMeter);
            var maximumCurvature = allGeometry.First(item =>
                maximumCurvatureValue - item.CurvaturePerMeter <= 1e-6f);
            var minimumRadiusValue = allGeometry.Min(item => item.LocalRadiusMeters);
            var minimumRadius = allGeometry.First(item =>
                item.LocalRadiusMeters - minimumRadiusValue <= 1e-3f);
            var finiteRadii = allGeometry.Where(item => item.LocalRadiusMeters < 999_999f)
                .Select(item => item.LocalRadiusMeters).Order().ToArray();
            var medianRadius = finiteRadii.Length == 0 ? 1_000_000f
                : finiteRadii[finiteRadii.Length / 2];
            var minimumSpeed = nodes.MinBy(item => item.SpeedMetersPerSecond)!;
            var decline = SustainedDeclineProgress(allGeometry, maximumCurvature.Progress);
            var materialDrive = profile.First(item => item.DriveAvailability >= .05f).Progress;
            var trace = constraints.Select(constraint =>
            {
                var node = Interpolate(nodes, constraint.DistanceMeters);
                var target = LookaheadTarget(constraints, constraint.DistanceMeters)
                    ?? constraint.SettledCapabilityMetersPerSecond;
                return new TurningConstraintTrace(
                    constraint.Progress,
                    constraint.LocalRadiusMeters,
                    constraint.SettledCapabilityMetersPerSecond,
                    ConstraintReachabilityAtEntry(constraint),
                    node.SpeedMetersPerSecond,
                    ResolveOutcome(path.PointAt(constraint.Progress), node.SpeedMetersPerSecond, target).Outcome,
                    constraint.DistanceMeters);
            }).ToArray();
            return new FreeTrajectoryEvaluation(
                candidate, FreeTrajectoryValidity.Valid, string.Empty, path, Array.AsReadOnly(profile),
                (float)cornerTime, straightFull.TravelTimeSeconds,
                (float)cornerTime + straightFull.TravelTimeSeconds,
                exitSpeed, straight10.ExitSpeedMetersPerSecond, straight20.ExitSpeedMetersPerSecond,
                straight40.ExitSpeedMetersPerSecond, straightFull.ExitSpeedMetersPerSecond,
                minimumSpeed.SpeedMetersPerSecond, minimumSpeed.Progress,
                (float)correctionDistance, (float)correctionTime, peakProxy,
                weightedRadiusTime <= 0d ? minimumRadius.LocalRadiusMeters : (float)(weightedRadius / weightedRadiusTime),
                minimumHeadroom, minimumLateral.LateralOffsetMeters, minimumLateral.Progress,
                maximumLateral.LateralOffsetMeters, maximumCurvature.CurvaturePerMeter,
                maximumCurvature.Progress, minimumRadius.LocalRadiusMeters, minimumRadius.Progress,
                medianRadius, finiteRadii.Length == 0 ? 1_000_000f : finiteRadii[^1],
                allGeometry.Average(item => item.CurvaturePerMeter),
                allGeometry.Min(item => item.HeadingDeltaRadians),
                allGeometry.Max(item => item.HeadingDeltaRadians), decline, materialDrive)
            {
                TurningDemandConstraints = constraints,
                TurningConstraintTrace = Array.AsReadOnly(trace),
                PeakSettledOverspeedRatio = peakSettledRatio,
                PeakEnvelopeOverspeedRatio = peakEnvelopeRatio,
                PeakSettledOverspeedProgress = peakSettledProgress,
                PeakEnvelopeOverspeedProgress = peakEnvelopeProgress,
                BrakeStepCount = brakeSteps,
                FirstCorrectionProgress = firstCorrectionProgress,
                NextEntryLateralMeters = nextEntryLateral,
                StraightRepositionDistanceMeters = straightDistance,
                StraightRepositionLateralHeadroomMeters = straightHeadroom,
                TurningLossEnergyJoules = turningLossEnergy,
                PeakTurningLossPowerWatts = MathF.Max(peakTurningLossPower,
                    profile.Max(item => item.TurningLossPowerWatts)),
            };

            float ConstraintReachabilityAtEntry(TurningDemandConstraint constraint) =>
                (float)Math.Sqrt((double)constraint.SettledCapabilityMetersPerSecond
                    * constraint.SettledCapabilityMetersPerSecond
                    + 2d * correctionCapability * constraint.DistanceMeters);
        }

        private float TurningCostMidpointEndSpeed(float speed, float distance, float curvature,
            float availability)
        {
            float Acceleration(float atSpeed)
            {
                var baseline = availability * LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(
                    atSpeed, referenceDriveForce, Neutral);
                var loss = TurningCostForce.LossNewtons(turningLossRatio, atSpeed, curvature,
                    availability, referenceDriveForce, Neutral);
                return baseline - loss / LongitudinalDynamics.ProvisionalNominalSystemMassKilograms;
            }

            var predicted = LongitudinalDynamics.ApplySignedAccelerationOverDistance(
                speed, Acceleration(speed), distance);
            var midpoint = (float)(((double)speed + predicted) * .5d);
            return LongitudinalDynamics.ApplySignedAccelerationOverDistance(
                speed, Acceleration(midpoint), distance);
        }

        private float? LookaheadTarget(
            IReadOnlyList<TurningDemandConstraint> constraints,
            float distanceMeters)
            => ReducedLookaheadTarget(constraints, distanceMeters, correctionCapability);

        private static SegmentResolution ResolveOutcome(
            FreeTrajectoryGeometryPoint point,
            float speed,
            float? target)
            => target is not { } explicitTarget
                ? new SegmentResolution(SegmentOutcome.Ok, LaneFor(point), speed)
                : SegmentPhysics.ResolveAdvancedForExplicitTarget(
                    LaneFor(point), speed, explicitTarget, UniformSurface, Balanced, 1f);

        private static int LaneFor(FreeTrajectoryGeometryPoint point)
            => LaneModel.ClampLane((int)MathF.Round(point.LateralPosition));

        private static IReadOnlyList<float> IntegrationProgress(
            FreeTrajectoryPath path,
            IReadOnlyList<TurningDemandConstraint> constraints)
        {
            var result = new List<float> { 0f };
            foreach (var interval in new[] { (0f, 1f / 3f), (1f / 3f, .5f), (.5f, 2f / 3f), (2f / 3f, 1f) })
            {
                var startDistance = path.DistanceAtProgress(interval.Item1);
                var endDistance = path.DistanceAtProgress(interval.Item2);
                for (var distance = startDistance + LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters;
                     distance < endDistance - 1e-6f;
                     distance += LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters)
                    result.Add(path.ProgressAtDistance(distance));
                result.Add(interval.Item2);
            }
            result.AddRange(constraints.Select(item => item.Progress));
            return Array.AsReadOnly(result.Distinct().Order().ToArray());
        }

        private static ReplayNode Interpolate(IReadOnlyList<ReplayNode> nodes, float distance)
        {
            if (distance <= nodes[0].DistanceMeters) return nodes[0];
            for (var index = 1; index < nodes.Count; index++)
            {
                if (distance > nodes[index].DistanceMeters + 1e-6f) continue;
                var span = nodes[index].DistanceMeters - nodes[index - 1].DistanceMeters;
                var t = span <= 0f ? 0f : (distance - nodes[index - 1].DistanceMeters) / span;
                return new ReplayNode(
                    nodes[index - 1].Progress + (nodes[index].Progress - nodes[index - 1].Progress) * t,
                    distance,
                    nodes[index - 1].SpeedMetersPerSecond
                        + (nodes[index].SpeedMetersPerSecond - nodes[index - 1].SpeedMetersPerSecond) * t,
                    nodes[index].CorrectionActive,
                    nodes[index - 1].DriveAvailability
                        + (nodes[index].DriveAvailability - nodes[index - 1].DriveAvailability) * t,
                    nodes[index - 1].NetAccelerationMetersPerSecondSquared
                        + (nodes[index].NetAccelerationMetersPerSecondSquared
                           - nodes[index - 1].NetAccelerationMetersPerSecondSquared) * t,
                    nodes[index].LookaheadTargetMetersPerSecond,
                    nodes[index].CornerControlOutcome);
            }
            return nodes[^1];
        }

        private static float SustainedDeclineProgress(
            IReadOnlyList<FreeTrajectoryGeometryPoint> points,
            float maximumProgress)
        {
            if (points.Max(item => item.CurvaturePerMeter)
                - points.Min(item => item.CurvaturePerMeter) <= 1e-6f)
                return 1f;
            var start = Math.Max(0, (int)(maximumProgress * (points.Count - 1)));
            const int window = 20;
            for (var index = start; index + window < points.Count; index++)
            {
                var sustained = true;
                for (var offset = 1; offset <= window; offset++)
                {
                    if (points[index + offset].CurvaturePerMeter
                        > points[index + offset - 1].CurvaturePerMeter + 1e-5f)
                    {
                        sustained = false;
                        break;
                    }
                }
                if (sustained) return points[index].Progress;
            }
            return 1f;
        }

        private static FreeTrajectoryEvaluation Invalid(
            FreeTrajectoryCandidate candidate,
            FreeTrajectoryValidity validity,
            string reason,
            FreeTrajectoryPath? path = null,
            IReadOnlyList<TurningDemandConstraint>? constraints = null,
            float peakSettledRatio = 0f,
            float peakEnvelopeRatio = 0f,
            float peakSettledProgress = 0f,
            float peakEnvelopeProgress = 0f,
            int brakeSteps = 0,
            float firstCorrectionProgress = 1f,
            int runWideInvalidations = 0,
            int crashInvalidations = 0,
            float nextEntryLateral = 0f,
            float straightRepositionDistance = 0f,
            float straightHeadroom = 0f)
            => new FreeTrajectoryEvaluation(
                candidate, validity, reason, path, Array.Empty<FreeTrajectoryProfilePoint>(),
                CornerTimeSeconds: 0f,
                FollowingStraightTimeSeconds: 0f,
                SectorTimeSeconds: 0f,
                ExitSpeedMetersPerSecond: 0f,
                SpeedTenMetersAfterCornerMetersPerSecond: 0f,
                SpeedTwentyMetersAfterCornerMetersPerSecond: 0f,
                SpeedFortyMetersAfterCornerMetersPerSecond: 0f,
                EndStraightSpeedMetersPerSecond: 0f,
                MinimumSpeedMetersPerSecond: 0f,
                MinimumSpeedProgress: 0f,
                CorrectionDistanceMeters: 0f,
                CorrectionTimeSeconds: 0f,
                PeakLateralAccelerationProxyMetersPerSecondSquared: 0f,
                TimeWeightedMeanRadiusMeters: 0f,
                MinimumLateralExecutionHeadroomMeters: 0f,
                MinimumLateralPositionMeters: 0f,
                MinimumLateralPositionProgress: 0f,
                MaximumLateralPositionMeters: 0f,
                MaximumCurvaturePerMeter: 0f,
                MaximumCurvatureProgress: 0f,
                MinimumRadiusMeters: 0f,
                MinimumRadiusProgress: 0f,
                MedianRadiusMeters: 0f,
                MaximumFiniteRadiusMeters: 0f,
                MeanAbsoluteCurvaturePerMeter: 0f,
                MinimumHeadingDeltaRadians: 0f,
                MaximumHeadingDeltaRadians: 0f,
                SustainedCurvatureDeclineProgress: 0f,
                MaterialDriveIncreaseProgress: 0f)
            {
                TurningDemandConstraints = constraints ?? Array.Empty<TurningDemandConstraint>(),
                PeakSettledOverspeedRatio = peakSettledRatio,
                PeakEnvelopeOverspeedRatio = peakEnvelopeRatio,
                PeakSettledOverspeedProgress = peakSettledProgress,
                PeakEnvelopeOverspeedProgress = peakEnvelopeProgress,
                BrakeStepCount = brakeSteps,
                FirstCorrectionProgress = firstCorrectionProgress,
                RunWideInvalidationCount = runWideInvalidations,
                CrashInvalidationCount = crashInvalidations,
                NextEntryLateralMeters = nextEntryLateral,
                StraightRepositionDistanceMeters = straightRepositionDistance,
                StraightRepositionLateralHeadroomMeters = straightHeadroom,
            };

        private sealed record ReplayNode(
            float Progress,
            float DistanceMeters,
            float SpeedMetersPerSecond,
            bool CorrectionActive,
            float DriveAvailability,
            float NetAccelerationMetersPerSecondSquared,
            float LookaheadTargetMetersPerSecond,
            SegmentOutcome CornerControlOutcome);
    }
}
