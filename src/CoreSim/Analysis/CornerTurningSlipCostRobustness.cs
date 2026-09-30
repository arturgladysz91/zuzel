using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public sealed record TurningCostZeroLimitSample(float Coefficient, FreeTrajectoryEvaluation Evaluation);

public sealed record TurningCostZeroLimitProbe(string Name, IReadOnlyList<TurningCostZeroLimitSample> Samples)
{
    public double SectorDeltaSeconds(TurningCostZeroLimitSample sample) =>
        (double)sample.Evaluation.SectorTimeSeconds - Samples[0].Evaluation.SectorTimeSeconds;

    // The two extra, smallest positive coefficients separate an O(c) response
    // from an intercept. This is a resolved numerical diagnostic, not a proof
    // about an arbitrarily small real coefficient below float resolution.
    public double EstimatedSectorInterceptSeconds => TurningCostZeroLimitDiagnostic.EstimateIntercept(
        Samples[1].Coefficient, SectorDeltaSeconds(Samples[1]),
        Samples[2].Coefficient, SectorDeltaSeconds(Samples[2]));
    public bool SectorZeroLimitPass => Math.Abs(EstimatedSectorInterceptSeconds)
        <= TurningCostZeroLimitDiagnostic.SectorResolutionSeconds;
}

public static class TurningCostZeroLimitDiagnostic
{
    // About sixteen output-float ULPs at a 6.7 s sector; 750 times smaller
    // than the reviewed 0.006022 s margin, not the old 0.01 s allowance.
    public const double SectorResolutionSeconds = .000008d;

    public static double EstimateIntercept(double lowerCoefficient, double lowerDeltaSeconds,
        double upperCoefficient, double upperDeltaSeconds)
    {
        if (!double.IsFinite(lowerCoefficient) || lowerCoefficient <= 0d)
            throw new ArgumentOutOfRangeException(nameof(lowerCoefficient));
        if (!double.IsFinite(upperCoefficient) || upperCoefficient <= lowerCoefficient)
            throw new ArgumentOutOfRangeException(nameof(upperCoefficient));
        if (!double.IsFinite(lowerDeltaSeconds)) throw new ArgumentOutOfRangeException(nameof(lowerDeltaSeconds));
        if (!double.IsFinite(upperDeltaSeconds)) throw new ArgumentOutOfRangeException(nameof(upperDeltaSeconds));
        return (upperCoefficient * lowerDeltaSeconds - lowerCoefficient * upperDeltaSeconds)
            / (upperCoefficient - lowerCoefficient);
    }
}

public enum TurningCostTrajectoryVariation { WholeLineShift, ShapeAmplitude }

public sealed record TurningCostTrajectoryProbe(TurningCostTrajectoryVariation Variation, float Parameter,
    FreeTrajectoryEvaluation Evaluation, float? MaximumLateralDisplacementMeters);

public sealed record TurningCostRobustnessResult(
    IReadOnlyList<TurningCostZeroLimitProbe> ZeroLimitProbes,
    FreeTrajectoryEvaluation Reference, FreeTrajectoryEvaluation ConstantInner,
    float MinimumReferenceOffsetMeters, float MaximumReferenceOffsetMeters,
    IReadOnlyList<TurningCostTrajectoryProbe> Variants,
    IReadOnlyList<TurningCostTrajectoryProbe> LocalAmplitudeRefinement)
{
    public TurningCostTrajectoryProbe? BestLocalAmplitude => LocalAmplitudeRefinement
        .Where(x => x.Evaluation.IsValid).OrderBy(x => x.Evaluation.SectorTimeSeconds)
        .ThenBy(x => x.Parameter).FirstOrDefault();
}

public static partial class FreeContinuousRacingTrajectoryGeometryExperiment
{
    public const int TurningCostDisplacementSampleDivisions = 4096;

    /// <summary>Fixed-trajectory diagnostics only; never calls RunSearch or changes the integrator.</summary>
    public static TurningCostRobustnessResult RunTurningCostRobustness()
    {
        const float coefficient = .005f;
        var controls = ReviewedTurningCostWinnerControls;
        var innerControls = new float[ControlStationCount];
        var reference = EvaluateTurningCost(controls, coefficient);
        var inner = EvaluateTurningCost(innerControls, coefficient);
        if (!reference.IsValid || !inner.IsValid)
            throw new InvalidOperationException("Frozen robustness reference is invalid.");
        var coefficients = new[] { 0f, .00000001f, .0000001f, .000001f, .00001f, .0001f, .001f };
        TurningCostZeroLimitProbe ZeroProbe(string name, IReadOnlyList<float> offsets) => new(name,
            Array.AsReadOnly(coefficients.Select(c =>
                new TurningCostZeroLimitSample(c, EvaluateTurningCost(offsets, c))).ToArray()));

        TurningCostTrajectoryProbe Probe(TurningCostTrajectoryVariation variation, float parameter)
        {
            var offsets = controls.Select(x => variation == TurningCostTrajectoryVariation.WholeLineShift
                ? x + parameter : x * parameter).ToArray();
            // Invalid geometry is reported unchanged: no clipping to the track.
            var evaluation = EvaluateTurningCost(offsets, coefficient);
            float? displacement = evaluation.Path is null ? null : Enumerable.Range(0,
                    TurningCostDisplacementSampleDivisions + 1)
                .Max(index =>
                {
                    var progress = (float)index / TurningCostDisplacementSampleDivisions;
                    return MathF.Abs(evaluation.Path.PointAt(progress).LateralOffsetMeters
                        - reference.Path!.PointAt(progress).LateralOffsetMeters);
                });
            return new TurningCostTrajectoryProbe(variation, parameter, evaluation, displacement);
        }

        var variants = new[] { -1f, -.5f, -.25f, -.1f, 0f, .1f, .25f, .5f, 1f }
            .Select(x => Probe(TurningCostTrajectoryVariation.WholeLineShift, x))
            .Concat(new[] { .75f, .9f, 1f, 1.1f, 1.25f }
                .Select(x => Probe(TurningCostTrajectoryVariation.ShapeAmplitude, x))).ToArray();

        var local = new List<TurningCostTrajectoryProbe>();
        var better = variants.Where(x => x.Evaluation.IsValid)
            .OrderBy(x => x.Evaluation.SectorTimeSeconds).First();
        if (better.Variation == TurningCostTrajectoryVariation.ShapeAmplitude
            && (double)reference.SectorTimeSeconds - better.Evaluation.SectorTimeSeconds
                > TurningCostZeroLimitDiagnostic.SectorResolutionSeconds)
        {
            // A separate one-dimensional, coherent-shape refinement, NOT the
            // 106-start/11-control search. One neighbour pair per halving level.
            local.Add(better);
            var step = .05f;
            for (var level = 0; level < 8; level++, step *= .5f)
            {
                var lower = Probe(TurningCostTrajectoryVariation.ShapeAmplitude, better.Parameter - step);
                var upper = Probe(TurningCostTrajectoryVariation.ShapeAmplitude, better.Parameter + step);
                local.Add(lower);
                local.Add(upper);
                better = new[] { better, lower, upper }.Where(x => x.Evaluation.IsValid)
                    .OrderBy(x => x.Evaluation.SectorTimeSeconds).ThenBy(x => x.Parameter).First();
            }
        }
        var referenceOffsets = Enumerable.Range(0, TurningCostDisplacementSampleDivisions + 1)
            .Select(index => reference.Path!.PointAt((float)index / TurningCostDisplacementSampleDivisions)
                .LateralOffsetMeters).ToArray();
        return new TurningCostRobustnessResult(Array.AsReadOnly(new[]
            { ZeroProbe("ConstantInner", innerControls), ZeroProbe("reviewed c=.005 winner", controls) }),
            reference, inner, referenceOffsets.Min(), referenceOffsets.Max(),
            Array.AsReadOnly(variants), Array.AsReadOnly(local.ToArray()));
    }
}

public static class TurningCostRobustnessReport
{
    public static string Render(TurningCostRobustnessResult result)
    {
        var sb = new StringBuilder();
        void L(string line = "") => sb.Append(line).Append('\n');
        static string F(double value, string format = "0.000000000") => value.ToString(format, CultureInfo.InvariantCulture);
        static string B(bool value) => value ? "PASS" : "FAIL";
        L("## Fixed-geometry zero-limit validation (reviewed HEAD 3b1a1a8995e8a4f7a79edb7388012bc2223b65c3)");
        L();
        L("These are fixed ConstantInner and reviewed c=.005-winner controls, not fresh searches. The requested sequence is augmented with c=1e-8 and 1e-7 to resolve a finite offset. Delta/c should remain bounded with a zero intercept for an O(c) response. The two-point intercept uses only those two smallest positive coefficients; it is a numerical diagnostic, not an extrapolated physical calibration.");
        L();
        L("| geometry | c | sector s | delta vs c=0 s | delta/c s | corner s | exit m/s | correction m | correction s | work J |");
        L("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var probe in result.ZeroLimitProbes)
        foreach (var sample in probe.Samples)
        {
            var e = sample.Evaluation;
            var delta = probe.SectorDeltaSeconds(sample);
            L($"| {probe.Name} | {F(sample.Coefficient, "0.00000000")} | {F(e.SectorTimeSeconds)} | {F(delta)} | {(sample.Coefficient == 0f ? "—" : F(delta / sample.Coefficient, "0.000000"))} | {F(e.CornerTimeSeconds)} | {F(e.ExitSpeedMetersPerSecond)} | {F(e.CorrectionDistanceMeters)} | {F(e.CorrectionTimeSeconds)} | {F(e.TotalTurningLossEnergyJoules)} |");
        }
        L();
        foreach (var probe in result.ZeroLimitProbes)
            L($"{probe.Name}: estimated sector intercept {F(probe.EstimatedSectorInterceptSeconds)} s; zero-limit diagnostic {B(probe.SectorZeroLimitPass)} at ±{F(TurningCostZeroLimitDiagnostic.SectorResolutionSeconds)} s resolution (about sixteen sector-output float ULPs). This is far stricter than 0.01 s and the 0.006022 s winning margin.");
        L();
        L(result.ZeroLimitProbes.All(x => x.SectorZeroLimitPass)
            ? "No resolved finite sector-time intercept in these probes. This alone cannot rule out offsets below the diagnostic resolution."
            : "Finite c=0 discontinuity detected: the positive-c deltas plateau rather than scale to zero. Thus the requested clean zero-limit validation FAILS. At zero, #47 uses partial bounded correction followed by constant-speed carry; positive c uses midpoint passive integration plus additional control over a whole interval and endpoint-average time. Correction distance/time also change definition, and tiny positive loss can activate many negligible whole-interval corrections. Energy tends to zero, but that does not establish controller/time continuity. The regression tests verify that this failure is detected; green software tests do NOT certify zero-limit continuity.");
        L();
        L("The integrator, force law, exact c=0 #47 branch, seven-coefficient search and convergence diagnostics are intentionally unchanged in this validation-only iteration. No fitted bridge, smoothing, tolerance relaxation or production change hides the failure.");
        L();
        L("## Physical track-space robustness at c=.005");
        L();
        L($"Frozen reference sector {F(result.Reference.SectorTimeSeconds)} s; ConstantInner {F(result.ConstantInner.SectorTimeSeconds)} s. Whole-line shift adds the same signed metres to all eleven controls; positive is outward. Amplitude multiplies all offsets relative to ConstantInner (zero). Maximum displacement samples the full spline at {FreeContinuousRacingTrajectoryGeometryExperiment.TurningCostDisplacementSampleDivisions + 1} equal-angle progress positions, comparing radial offsets at the same track angle, not just controls or percentage of arc length. This is a sampled maximum, not an analytical extremum. The reference occupies {F(result.MinimumReferenceOffsetMeters)}–{F(result.MaximumReferenceOffsetMeters)} m from the inside edge.");
        L();
        L("Invalid variants retain their original controls/reasons; — means no valid trajectory/performance metric. No invalid path is clamped or assigned a zero time. The inward shifts cross the physical inner boundary, not the numerical lateral-execution gate.");
        L();
        void Table(IReadOnlyList<TurningCostTrajectoryProbe> probes)
        {
            L("| mode / parameter | controls m | validity / reason | max displacement m | sector s | delta winner s | delta inner s | corner s | exit m/s | path m | min R m | max κ 1/m | mean abs κ 1/m | total J | correction J | outside correction J |");
            L("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var probe in probes)
            {
                var e = probe.Evaluation;
                var label = probe.Variation == TurningCostTrajectoryVariation.WholeLineShift
                    ? $"shift {F(probe.Parameter, "+0.000000;-0.000000;0.000000")} m"
                    : $"amplitude {F(probe.Parameter * 100d, "0.000000")}%";
                var prefix = $"| {label} | {string.Join(",", e.Candidate.ControlOffsetsMeters.Select(x => F(x)))} | {(e.IsValid ? "Valid" : e.InvalidReason)} | {(probe.MaximumLateralDisplacementMeters is { } d ? F(d) : "—")} |";
                L(e.IsValid
                    ? $"{prefix} {F(e.SectorTimeSeconds)} | {F((double)e.SectorTimeSeconds - result.Reference.SectorTimeSeconds)} | {F((double)e.SectorTimeSeconds - result.ConstantInner.SectorTimeSeconds)} | {F(e.CornerTimeSeconds)} | {F(e.ExitSpeedMetersPerSecond)} | {F(e.Path!.TotalLengthMeters)} | {F(e.MinimumRadiusMeters)} | {F(e.MaximumCurvaturePerMeter)} | {F(e.MeanAbsoluteCurvaturePerMeter)} | {F(e.TotalTurningLossEnergyJoules)} | {F(e.TurningLossEnergyDuringCorrectionJoules)} | {F(e.TurningLossEnergyDuringDriveJoules)} |"
                    : $"{prefix} — | — | — | — | — | — | — | — | — | — | — | — |");
            }
        }
        Table(result.Variants);
        L();
        L("### Separate local coherent-amplitude refinement");
        L();
        if (result.BestLocalAmplitude is { } best)
        {
            L($"The fixed 90% probe improves on the frozen reference beyond the 8 µs numerical resolution, so a separate one-parameter refinement starts there. Eight levels test only amplitude ±0.05, halving the step each level; retain the best valid sector time with lower-amplitude tie-breaking. {result.LocalAmplitudeRefinement.Count} recorded evaluations including the seed; no 106-start searches or independent control perturbations. Best tested amplitude {F(best.Parameter * 100d, "0.000000")}%: sector {F(best.Evaluation.SectorTimeSeconds)} s, delta reference {F((double)best.Evaluation.SectorTimeSeconds - result.Reference.SectorTimeSeconds)} s, displacement {F(best.MaximumLateralDisplacementMeters!.Value)} m. This is local one-dimensional evidence, not a replacement globally converged winner.");
            L();
            Table(result.LocalAmplitudeRefinement);
        }
        else L("No clearly faster coherent-amplitude seed was found; no local refinement was run.");
        L();
        L("### Tested-only width and interpretation");
        L();
        foreach (var tolerance in new[] { .005d, .010d, .020d })
        {
            var shifts = result.Variants.Where(x => x.Variation == TurningCostTrajectoryVariation.WholeLineShift
                && x.Evaluation.IsValid && (double)x.Evaluation.SectorTimeSeconds
                    - result.Reference.SectorTimeSeconds <= tolerance).ToArray();
            L($"Within +{F(tolerance, "0.000")} s of the frozen reference: tested valid whole-line shifts {F(shifts.Min(x => x.Parameter), "0.00")} to {F(shifts.Max(x => x.Parameter), "0.00")} m. Only discrete tested points qualify; no claim that every intermediate shift qualifies or that the threshold boundary was located.");
        }
        L();
        L("The measured basin is anisotropic and one-sided, not a millimetre needle. Outward shifts of 0.10/0.25/0.50/1.00 m lose about 4.61/11.61/23.39/47.46 ms; 0.10 m preserves a small advantage over ConstantInner, whereas 0.25 m loses that advantage. The 0.25–0.50 m whole-line direction is moderate rather than broad (losses are tens, not just a few, milliseconds). All five 75–125% amplitude probes remain valid within about 1.11 ms of the reference while displacing the full spline by up to 0.157 m; this is a comparatively flat coherent-shape direction. Inward shifts of 0.10 m or more are unavailable because the line already approaches the physical inner edge. These probes do not show validity dominated by the numerical gate, nor do they establish a two-sided 0.25–0.50 m basin.");
        L();
        L("The c=.005 non-inner advantage remains a reproducible exploratory result across a family of shapes; the tiny numerical headroom alone does not imply needle positioning. However, zero-limit validation has failed, so the model is NOT yet validated for production or as a clean physics baseline for the next experiment. Resolve the analysis integrator's c=0/controller semantics in a separately authorized iteration before using a small advantage as physical evidence. This iteration neither repairs that split nor changes the frozen full-search result.");
        return sb.ToString();
    }
}
