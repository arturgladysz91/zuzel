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
    public double OutputSectorUlpSeconds => TurningCostZeroLimitDiagnostic.OutputUlp(Samples[0].Evaluation.SectorTimeSeconds);
    public double SectorInterceptInUlps => EstimatedSectorInterceptSeconds / OutputSectorUlpSeconds;
    public bool SectorZeroLimitPass => Math.Abs(SectorInterceptInUlps)
        <= TurningCostZeroLimitDiagnostic.MaximumInterceptUlps;
}

public static class TurningCostZeroLimitDiagnostic
{
    public const int MaximumInterceptUlps = 2;
    public static double OutputUlp(float value) => (double)MathF.BitIncrement(value) - value;

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
    public static TurningCostRobustnessResult RunTurningCostRobustness(IReadOnlyList<float>? referenceControls = null)
    {
        const float coefficient = .005f;
        var controls = referenceControls ?? ReviewedTurningCostWinnerControls;
        var innerControls = new float[ControlStationCount];
        var reference = EvaluateTurningCost(controls, coefficient);
        var inner = EvaluateTurningCost(innerControls, coefficient);
        if (!reference.IsValid || !inner.IsValid)
            throw new InvalidOperationException("Robustness reference is invalid.");
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

        var variants = controls.All(x => x == 0f) ? Array.Empty<TurningCostTrajectoryProbe>()
            : new[] { 0f, .1f, .25f, .5f, 1f }
            .Select(x => Probe(TurningCostTrajectoryVariation.WholeLineShift, x))
            .Concat(new[] { .75f, .9f, 1f, 1.1f, 1.25f }
                .Select(x => Probe(TurningCostTrajectoryVariation.ShapeAmplitude, x))).ToArray();

        var local = new List<TurningCostTrajectoryProbe>();
        var better = variants.Where(x => x.Evaluation.IsValid)
            .OrderBy(x => x.Evaluation.SectorTimeSeconds).FirstOrDefault();
        if (better is { Variation: TurningCostTrajectoryVariation.ShapeAmplitude }
            && (double)reference.SectorTimeSeconds - better.Evaluation.SectorTimeSeconds
                > TurningCostZeroLimitDiagnostic.MaximumInterceptUlps * TurningCostZeroLimitDiagnostic.OutputUlp(reference.SectorTimeSeconds))
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
            { ZeroProbe("ConstantInner", innerControls), ZeroProbe("reviewed c=.005 winner geometry", ReviewedTurningCostWinnerControls) }),
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
        L("## Fixed-geometry zero-limit validation (repair after reviewed HEAD d9eebbf49d3c6bb2bc9f8f5cc1e14522bcc08db2)");
        L();
        L("These are fixed ConstantInner and the old reviewed c=.005-winner controls, not the fresh search winner. The seven coefficients include c=1e-8 and 1e-7 to resolve a finite offset. The two-point sector intercept uses those two smallest positive coefficients; its bound is two actual output-float ULPs, not a gameplay-scale time tolerance. Float quantization can make individual deltas or delta/c nonmonotonic; neither a slope fit nor an offset is applied to the simulation.");
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
            L($"{probe.Name}: output ULP {F(probe.OutputSectorUlpSeconds, "0.000000000000000")} s; estimated sector intercept {F(probe.EstimatedSectorInterceptSeconds, "0.000000000000000")} s = {F(probe.SectorInterceptInUlps, "0.000000")} ULPs; zero-limit diagnostic {B(probe.SectorZeroLimitPass)} within ±{TurningCostZeroLimitDiagnostic.MaximumInterceptUlps} output ULPs.");
        L();
        L(result.ZeroLimitProbes.All(x => x.SectorZeroLimitPass)
            ? "No resolved finite sector-time intercept in either fixed probe. At c=1e-8 the regression also requires sector/corner time, exit speed, actual correction distance/time and every sampled profile speed within two respective float ULPs of #47, with identical profile controller flags/outcomes. Work remains positive and tends to zero. This is a resolved numerical limit check, not a proof below float resolution."
            : "Zero-limit validation FAILS: a finite sector-time intercept exceeds two output-float ULPs. Do not interpret the search as a clean continuous extension of #47.");
        L();
        L("The positive-c integrator now preserves partial correction, target crossing and remaining dissipative carry. The force law and exact c=0 #47 branch are frozen. All seven full searches are rerun with unchanged starts, refinement and closure tolerances. No coefficient threshold, fitted bridge, smoothing or production change is used.");
        L();
        L("## Physical track-space robustness at c=.005");
        L();
        L($"Reference {result.Reference.Candidate.Id}, sector {F(result.Reference.SectorTimeSeconds)} s; ConstantInner {F(result.ConstantInner.SectorTimeSeconds)} s. In the complete experiment this reference is the freshly searched c=.005 winner, not the old reviewed controls. Whole-line shift adds the same metres to all eleven controls; positive is outward. Amplitude multiplies all offsets relative to ConstantInner (zero). Maximum displacement samples the full spline at {FreeContinuousRacingTrajectoryGeometryExperiment.TurningCostDisplacementSampleDivisions + 1} equal-angle progress positions, comparing radial offsets at the same track angle, not just controls or percentage of arc length. This is a sampled maximum, not an analytical extremum. The reference occupies {F(result.MinimumReferenceOffsetMeters)}–{F(result.MaximumReferenceOffsetMeters)} m from the inside edge.");
        L();
        L("Invalid variants retain their original controls/reasons; — means no valid trajectory/performance metric. No invalid path is clamped or assigned a zero time. Only the requested outward shifts and coherent amplitudes are tested; impossible inward shifts and centimetre-scale reserve diagnostics are not rerun.");
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
        if (result.Variants.Count > 0) Table(result.Variants);
        else L("ConstantInner is the reference; no non-inner physical-width or amplitude probes are required.");
        L();
        L("### Separate local coherent-amplitude refinement");
        L();
        if (result.BestLocalAmplitude is { } best)
        {
            L($"The best fixed amplitude probe ({F(result.LocalAmplitudeRefinement[0].Parameter * 100d, "0.000000")}%) improves on the reference by more than two sector-output ULPs, so a separate one-parameter refinement starts there. Eight levels test only amplitude ±0.05, halving the step each level; retain the best valid sector time with lower-amplitude tie-breaking. {result.LocalAmplitudeRefinement.Count} recorded evaluations including the seed; no 106-start searches or independent control perturbations. Best tested amplitude {F(best.Parameter * 100d, "0.000000")}%: sector {F(best.Evaluation.SectorTimeSeconds)} s, delta reference {F((double)best.Evaluation.SectorTimeSeconds - result.Reference.SectorTimeSeconds)} s, displacement {F(best.MaximumLateralDisplacementMeters!.Value)} m. This is local one-dimensional evidence, not a replacement globally converged winner.");
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
            L(shifts.Length == 0 ? $"Within +{F(tolerance, "0.000")} s: no qualifying whole-line shift was tested."
                : $"Within +{F(tolerance, "0.000")} s of the reference: tested valid whole-line shifts {F(shifts.Min(x => x.Parameter), "0.00")} to {F(shifts.Max(x => x.Parameter), "0.00")} m. Only discrete tested points qualify; no claim that every intermediate shift qualifies or that the threshold boundary was located.");
        }
        L();
        var outward = result.Variants.Where(x => x.Variation == TurningCostTrajectoryVariation.WholeLineShift
            && x.Parameter > 0f && x.Evaluation.IsValid).ToArray();
        if (outward.Length > 0)
            L("Outward shift / measured delta: " + string.Join("; ", outward.Select(x =>
                $"+{F(x.Parameter, "0.00")} m / {F(1000d * ((double)x.Evaluation.SectorTimeSeconds - result.Reference.SectorTimeSeconds), "0.000000")} ms")) + ". These one-sided discrete probes do not establish a two-sided basin or a continuous width boundary.");
        var amplitudes = result.Variants.Where(x => x.Variation == TurningCostTrajectoryVariation.ShapeAmplitude
            && x.Evaluation.IsValid).ToArray();
        if (amplitudes.Length > 0)
            L($"Valid coherent amplitudes: {amplitudes.Length} of 5; maximum absolute delta {F(amplitudes.Max(x => Math.Abs(1000d * ((double)x.Evaluation.SectorTimeSeconds - result.Reference.SectorTimeSeconds))), "0.000000")} ms; maximum sampled physical displacement {F(amplitudes.Max(x => x.MaximumLateralDisplacementMeters!.Value))} m. This direction is distinct from whole-line translation; percentages are not physical metres.");
        L();
        L("The repaired zero-limit check and fresh search/width results are separate evidence. Passing continuity removes the controller-state discontinuity; it does not calibrate the effective slip coefficient, certify a globally optimal geometry, or justify production promotion. Any unclosed sweep coefficient remains explicitly unresolved.");
        return sb.ToString();
    }
}
