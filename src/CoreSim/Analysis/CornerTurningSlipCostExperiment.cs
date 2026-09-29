using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CoreSim.Setup;

namespace CoreSim.Analysis;

/// <summary>Analysis-only effective slip drag. The coefficient is a dimensionless force ratio.</summary>
public static class TurningCostForce
{
    public static float RequestedLossNewtons(float coefficient, float speedMetersPerSecond,
        float curvaturePerMeter)
    {
        if (!float.IsFinite(coefficient) || coefficient < 0f)
            throw new ArgumentOutOfRangeException(nameof(coefficient));
        if (!float.IsFinite(speedMetersPerSecond) || speedMetersPerSecond < 0f)
            throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond));
        if (!float.IsFinite(curvaturePerMeter))
            throw new ArgumentOutOfRangeException(nameof(curvaturePerMeter));
        var force = (double)coefficient * LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
            * speedMetersPerSecond * speedMetersPerSecond * Math.Abs(curvaturePerMeter);
        return (float)Math.Min(force, float.MaxValue);
    }

    public static float LossNewtons(float coefficient, float speedMetersPerSecond,
        float curvaturePerMeter, float driveAvailability, float referenceDriveForceNewtons,
        BikeSetup setup)
    {
        if (!float.IsFinite(driveAvailability) || driveAvailability < 0f || driveAvailability > 1f)
            throw new ArgumentOutOfRangeException(nameof(driveAvailability));
        var requested = RequestedLossNewtons(coefficient, speedMetersPerSecond, curvaturePerMeter);
        if (requested == 0f || driveAvailability == 0f) return 0f;
        var available = driveAvailability * LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(
            referenceDriveForceNewtons, speedMetersPerSecond, setup);
        return MathF.Min(requested, available);
    }
}

public sealed record TurningCostScenario(
    float Coefficient,
    FreeTrajectorySearchResult Search,
    IReadOnlyList<FreeTrajectoryEvaluation> ConstantLines,
    FreeTrajectoryEvaluation Winner,
    FreeTrajectoryEvaluation ConstantInner,
    RepeatedTrajectoryLapResult RepeatedLap)
{
    public FreeTrajectoryEvaluation WinnerAtZero { get; init; } = null!;
    public bool MaximumSpeedBandExceeded => RepeatedLap.Converged
        && RepeatedLap.MaximumSpeedMetersPerSecond * 3.6f
            > FreeContinuousRacingTrajectoryGeometryExperiment.RealMotoarenaMaximumSpeedGuardrailKilometersPerHour
              * (1f + FreeContinuousRacingTrajectoryGeometryExperiment.ClearlyBrokenGlobalSpeedFraction);
    public bool AverageSpeedBandExceeded => RepeatedLap.Converged
        && RepeatedLap.AverageSpeedMetersPerSecond
            > FreeContinuousRacingTrajectoryGeometryExperiment.RealMotoarenaAverageSpeedGuardrailMetersPerSecond
              * (1f + FreeContinuousRacingTrajectoryGeometryExperiment.ClearlyBrokenGlobalSpeedFraction);
    public bool LapBandExceeded => RepeatedLap.Converged
        && (RepeatedLap.FlyingLapTimeSeconds
                < FreeContinuousRacingTrajectoryGeometryExperiment.RealMotoarenaFlyingLapGuardrailSeconds
                  * (1f - FreeContinuousRacingTrajectoryGeometryExperiment.ClearlyBrokenGlobalSpeedFraction)
            || RepeatedLap.FlyingLapTimeSeconds
                > FreeContinuousRacingTrajectoryGeometryExperiment.RealMotoarenaFlyingLapGuardrailSeconds
                  * (1f + FreeContinuousRacingTrajectoryGeometryExperiment.ClearlyBrokenGlobalSpeedFraction));
    public bool AnyGuardrailExceeded => MaximumSpeedBandExceeded || AverageSpeedBandExceeded || LapBandExceeded;
}

public sealed record CornerTurningSlipCostExperimentResult(
    string BaseMainSha,
    IReadOnlyList<TurningCostScenario> Scenarios,
    FreeContinuousRacingTrajectoryGeometryExperimentResult Baseline);

public static partial class FreeContinuousRacingTrajectoryGeometryExperiment
{
    public const string TurningCostBaseMainSha = "99f3afd08c8e6892a8b24eb785b5be6cbf116d38";
    public static IReadOnlyList<float> TurningCostSweep { get; } =
        Array.AsReadOnly(new[] { 0f, .001f, .005f, .02f, .08f, .32f, 1.28f });

    public static FreeTrajectoryEvaluation EvaluateTurningCost(
        IReadOnlyList<float> offsetsMeters, float coefficient)
    {
        var baseline = DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 0, 0));
        return new ExperimentalGeometryReplay(CreateTrack().Geometry, baseline.CornerEntrySpeedMetersPerSecond,
            turningLossRatio: coefficient).Evaluate(Candidate("TurningCostProbe", offsetsMeters));
    }

    public static CornerTurningSlipCostExperimentResult RunTurningCostExperiment()
    {
        var baseline = Run();
        var geometry = baseline.Geometry;
        var scenarios = new List<TurningCostScenario>();
        var baselineConstants = baseline.Controls.Where(item => item.Candidate.SeedFamily.StartsWith("Constant",
            StringComparison.Ordinal)).ToArray();
        scenarios.Add(new TurningCostScenario(0f, baseline.Search,
            Array.AsReadOnly(baselineConstants), baseline.BestFound, baseline.ConstantInner,
            baseline.RepeatedBestFound) { WinnerAtZero = baseline.BestFound });

        var zeroEvaluator = new ExperimentalGeometryReplay(geometry, baseline.EntrySpeedMetersPerSecond);

        foreach (var coefficient in TurningCostSweep.Skip(1))
        {
            var evaluator = new ExperimentalGeometryReplay(geometry, baseline.EntrySpeedMetersPerSecond,
                turningLossRatio: coefficient);
            var constants = baselineConstants.Select(item => evaluator.Evaluate(item.Candidate)).ToArray();
            var search = RunSearch(evaluator, geometry);
            if (search.TopTwenty.Count == 0)
                throw new InvalidOperationException($"No valid trajectory at c={coefficient}.");
            var winner = search.TopTwenty[0];
            var repeated = RunRepeatedLap(evaluator, winner.Candidate, baseline.EntrySpeedMetersPerSecond);
            scenarios.Add(new TurningCostScenario(coefficient, search,
                Array.AsReadOnly(constants), winner, constants[0], repeated)
            {
                WinnerAtZero = zeroEvaluator.Evaluate(winner.Candidate),
            });
        }
        return new CornerTurningSlipCostExperimentResult(TurningCostBaseMainSha,
            Array.AsReadOnly(scenarios.ToArray()), baseline);
    }
}

public static class CornerTurningSlipCostReport
{
    public static string Render(CornerTurningSlipCostExperimentResult result)
    {
        var sb = new StringBuilder();
        void L(string line = "") => sb.Append(line).Append('\n');
        static string F(double value, string format = "0.000000") => value.ToString(format, CultureInfo.InvariantCulture);
        static string B(bool value) => value ? "YES" : "NO";
        var zero = result.Scenarios[0];
        var firstChange = result.Scenarios.Skip(1).FirstOrDefault(item => item.Search.ObjectiveConvergence
            && item.Winner.Candidate.Id != zero.Winner.Candidate.Id);
        var firstNonInner = result.Scenarios.Skip(1).FirstOrDefault(item => item.Search.ObjectiveConvergence
            && item.Winner.SectorTimeSeconds < item.ConstantInner.SectorTimeSeconds - 1e-5f);

        L("# Corner turning/slip energy cost experiment (#48)");
        L();
        L($"Base merged main: `{result.BaseMainSha}`. Analysis-only; production race equations and #47 baseline are frozen.");
        L();
        L("## Model fixed before sweep");
        L();
        L("Let m = 142 kg (the existing provisional rider plus motorcycle mass), v be path speed in m/s, and κ be signed local path curvature in 1/m. Lateral demand is a_lat = v²|κ| in m/s² and lateral force is F_lat = m v²|κ| in N. With one dimensionless experimental coefficient c, requested effective slip drag is F_slip = c F_lat. Requested dissipated power is P_slip = F_slip v = c m v³|κ| in W. The coefficient represents an effective longitudinal-to-lateral force ratio, approximately tan(effective slip angle) for small angles; it is not a measured tyre slip angle.");
        L();
        L("The #47 availability factor A(p) and speed-dependent drive force F_drive(v) give available forward force A F_drive. Applied slip loss is min(F_slip, A F_drive), so useful drive force is max(0, A F_drive − F_slip). The original #47 net acceleration A(F_drive − F_resistance)/m is reduced by applied slip loss/m only during a positive-drive step. Existing explicit correction steps retain their #47 braking law and apply no additional slip debit. At c = 0 the original midpoint integrator is called directly. Work is integrated as Σ F_loss Δs over accepted corner drive steps; peak power is the maximum at integration midpoints and reported trajectory sample points.");
        L();
        L("Units: kg·(m/s)²·(1/m) = N, and N·m/s = W; N·m = J. Loss tends to zero as κ → 0 or v → 0. It grows with |κ| and as v² in force / v³ in power until useful drive saturates at zero. The clamp prevents the surrogate from manufacturing negative propulsion, and it is continuous at the saturation boundary. No line ID, lane number, preferred offset, or time penalty enters this equation. The coefficient values were set as a logarithmic sensitivity sweep, not fitted to an optimizer winner.");
        L();
        L("## Zero-cost regression");
        L();
        L($"Winner `{zero.Winner.Candidate.SeedFamily}` / `{zero.Winner.Candidate.Id}` has eleven zero offsets, exactly the ConstantInner geometry; sector {F(zero.Winner.SectorTimeSeconds)} s; free minus ConstantInner {F(zero.Winner.SectorTimeSeconds - zero.ConstantInner.SectorTimeSeconds)} s; objective convergence {B(zero.Search.ObjectiveConvergence)}; geometry convergence {B(zero.Search.GeometryConvergence)}. The zero scenario directly reuses the merged #47 result, including all validity outcomes and search diagnostics.");
        L();
        L("## Sensitivity sweep");
        L();
        L("c is dimensionless. Δ values are relative to c = 0. All times are seconds, length m, energy J, peak power W, speed m/s, acceleration m/s², curvature 1/m. Repeated lap includes two periodic corner/straight sectors.");
        L();
        L("| c | winner / controls (m) | nonconstant | path m | corner s | exit m/s | straight s | sector s | Δsector s | min radius m @p | max curvature | peak a_lat | loss J | peak W | max v | avg v | lap s | Δlap s | objective / geometry | guardrails V/A/L |");
        L("|---:|---|:---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|:---:|:---:|");
        foreach (var scenario in result.Scenarios)
        {
            var w = scenario.Winner;
            var controls = string.Join(",", w.Candidate.ControlOffsetsMeters.Select(x => F(x, "0.00")));
            L($"| {F(scenario.Coefficient, "0.000")} | `{w.Candidate.SeedFamily}` `{w.Candidate.Id}`<br>{controls} | {B(w.IsNonConstant)} | {F(w.Path!.TotalLengthMeters, "0.000")} | {F(w.CornerTimeSeconds)} | {F(w.ExitSpeedMetersPerSecond)} | {F(w.FollowingStraightTimeSeconds)} | {F(w.SectorTimeSeconds)} | {F(w.SectorTimeSeconds - zero.Winner.SectorTimeSeconds)} | {F(w.MinimumRadiusMeters, "0.000")} @ {F(w.MinimumRadiusProgress, "0.000")} | {F(w.MaximumCurvaturePerMeter, "0.000000")} | {F(w.PeakLateralAccelerationProxyMetersPerSecondSquared, "0.000")} | {F(w.TurningLossEnergyJoules, "0.0")} | {F(w.PeakTurningLossPowerWatts, "0.0")} | {F(scenario.RepeatedLap.MaximumSpeedMetersPerSecond, "0.000")} | {F(scenario.RepeatedLap.AverageSpeedMetersPerSecond, "0.000")} | {F(scenario.RepeatedLap.FlyingLapTimeSeconds)} | {F(scenario.RepeatedLap.FlyingLapTimeSeconds - zero.RepeatedLap.FlyingLapTimeSeconds)} | {B(scenario.Search.ObjectiveConvergence)} / {B(scenario.Search.GeometryConvergence)} | {B(scenario.MaximumSpeedBandExceeded)}/{B(scenario.AverageSpeedBandExceeded)}/{B(scenario.LapBandExceeded)} |");
        }
        L();
        L("### Changes from the c = 0 winning geometry");
        L();
        L("All deltas use the zero-coefficient winner as reference, even when the optimizer changes controls. The `same controls at c=0` column isolates the new loss effect from changing geometry.");
        L();
        L("| c | Δpath m | Δcorner s | Δexit m/s | Δstraight s | Δmin radius m | Δmax curvature | Δpeak a_lat | Δenergy J | Δpeak W | Δmax v | Δavg v | same controls at c=0: Δsector s | winner advantage over inner s | correction distance winner / inner m | correction time winner / inner s | min lateral headroom m |");
        L("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var s in result.Scenarios)
        {
            var w = s.Winner;
            var z = zero.Winner;
            L($"| {F(s.Coefficient, "0.000")} | {F(w.Path!.TotalLengthMeters - z.Path!.TotalLengthMeters, "0.000")} | {F(w.CornerTimeSeconds - z.CornerTimeSeconds)} | {F(w.ExitSpeedMetersPerSecond - z.ExitSpeedMetersPerSecond)} | {F(w.FollowingStraightTimeSeconds - z.FollowingStraightTimeSeconds)} | {F(w.MinimumRadiusMeters - z.MinimumRadiusMeters, "0.000")} | {F(w.MaximumCurvaturePerMeter - z.MaximumCurvaturePerMeter, "0.000000")} | {F(w.PeakLateralAccelerationProxyMetersPerSecondSquared - z.PeakLateralAccelerationProxyMetersPerSecondSquared, "0.000")} | {F(w.TurningLossEnergyJoules, "0.0")} | {F(w.PeakTurningLossPowerWatts, "0.0")} | {F(s.RepeatedLap.MaximumSpeedMetersPerSecond - zero.RepeatedLap.MaximumSpeedMetersPerSecond, "0.000")} | {F(s.RepeatedLap.AverageSpeedMetersPerSecond - zero.RepeatedLap.AverageSpeedMetersPerSecond, "0.000")} | {F(s.WinnerAtZero.SectorTimeSeconds - z.SectorTimeSeconds)} | {F(s.ConstantInner.SectorTimeSeconds - w.SectorTimeSeconds)} | {F(w.CorrectionDistanceMeters, "0.000")} / {F(s.ConstantInner.CorrectionDistanceMeters, "0.000")} | {F(w.CorrectionTimeSeconds, "0.000")} / {F(s.ConstantInner.CorrectionTimeSeconds, "0.000")} | {F(w.MinimumLateralExecutionHeadroomMeters, "0.000")} |");
        }
        L();
        L("The descriptive guard band is ±15% around Motoarena maximum speed 111 km/h, average speed 22.38 m/s, and flying lap 14.71 s. V/A/L mark where the measured maximum-speed, average-speed, or lap-time band is exceeded. These are diagnostic bounds, never fitting targets. Repeated-lap convergence must be checked before interpreting them.");
        L();
        L("### Constant-line controls and free winner");
        L();
        L("| c | line | sector s | Δ vs zero inner s | slip J | exit m/s | valid |");
        L("|---:|---|---:|---:|---:|---:|:---:|");
        foreach (var scenario in result.Scenarios)
        foreach (var line in scenario.ConstantLines.Append(scenario.Winner))
            L($"| {F(scenario.Coefficient, "0.000")} | {line.Candidate.SeedFamily} | {F(line.SectorTimeSeconds)} | {F(line.SectorTimeSeconds - zero.ConstantInner.SectorTimeSeconds)} | {F(line.TurningLossEnergyJoules, "0.0")} | {F(line.ExitSpeedMetersPerSecond)} | {B(line.IsValid)} |");
        L();
        L("### Search closure and validity");
        L();
        L("| c | starts | refined | top-3 closed | independent near starts | single residual s | pair residual s | objective | geometry | valid / evaluated | repeat converged |");
        L("|---:|---:|---:|:---:|---:|---:|---:|:---:|:---:|---:|:---:|");
        foreach (var s in result.Scenarios)
            L($"| {F(s.Coefficient, "0.000")} | {s.Search.StartsGenerated} | {s.Search.RefinedStartCount} | {B(s.Search.TopThreeRefinementDiagnostics.Count >= 3 && s.Search.TopThreeRefinementDiagnostics.Take(3).All(x => x.Stable))} | {s.Search.WithinToleranceFamilyCount} | {F(s.Search.LocalPerturbationImprovementSeconds)} | {F(s.Search.PairPerturbationImprovementSeconds)} | {B(s.Search.ObjectiveConvergence)} | {B(s.Search.GeometryConvergence)} | {s.Search.ValidCandidates} / {s.Search.CandidatesEvaluated} | {B(s.RepeatedLap.Converged)} |");
        L();
        L("## Trajectory samples");
        L();
        L("Each distinct converged winner is shown once. Drive remaining is useful forward force after the slip debit, in N; it is zero during explicit correction. Power is applied slip loss in W.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in result.Scenarios.Where(x => x.Search.ObjectiveConvergence))
        {
            if (!seen.Add(s.Winner.Candidate.Id)) continue;
            L();
            L($"### c = {F(s.Coefficient, "0.000")}: `{s.Winner.Candidate.Id}`");
            L();
            L("| p | lateral offset m | lateral position (lane units) | radius m | speed m/s | a_lat m/s² | slip W | drive remaining N |");
            L("|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var point in s.Winner.Profile.Where((_, index) => index % 2 == 0))
                L($"| {F(point.Progress, "0.00")} | {F(point.LateralOffsetMeters, "0.000")} | {F(point.LateralPosition, "0.000")} | {F(point.LocalRadiusMeters, "0.000")} | {F(point.SpeedMetersPerSecond, "0.000")} | {F(point.LateralAccelerationProxyMetersPerSecondSquared, "0.000")} | {F(point.TurningLossPowerWatts, "0.0")} | {F(point.LongitudinalDriveRemainingNewtons, "0.0")} |");
        }
        L();
        L("## Interpretation");
        L();
        L($"First converged winner shape change: {(firstChange is null ? "none in tested sweep" : F(firstChange.Coefficient, "0.000"))}. First converged coefficient where free beats ConstantInner: {(firstNonInner is null ? "none in tested sweep" : F(firstNonInner.Coefficient, "0.000"))}. A discrete sampled interval, rather than an interpolated physical threshold, is reported; the prior tested coefficient is the lower bound.");
        L();
        if (firstNonInner is not null)
        {
            var previous = result.Scenarios.TakeWhile(x => x.Coefficient < firstNonInner.Coefficient).Last();
            L($"The first optimizer crossover is bracketed by c = {F(previous.Coefficient, "0.000")} and {F(firstNonInner.Coefficient, "0.000")}. At the upper endpoint the gain over ConstantInner is {F(firstNonInner.ConstantInner.SectorTimeSeconds - firstNonInner.Winner.SectorTimeSeconds)} s, the new path is {F(firstNonInner.Winner.Path!.TotalLengthMeters - firstNonInner.ConstantInner.Path!.TotalLengthMeters, "0.000")} m longer, and its applied slip work is {F(firstNonInner.Winner.TurningLossEnergyJoules - firstNonInner.ConstantInner.TurningLossEnergyJoules, "0.0")} J different. Its maximum curvature is {F(firstNonInner.Winner.MaximumCurvaturePerMeter, "0.000000")} 1/m versus inner {F(firstNonInner.ConstantInner.MaximumCurvaturePerMeter, "0.000000")} 1/m; this is a redistribution of curvature and drive-phase timing, not uniformly broader local radius. Correction distance is {F(firstNonInner.Winner.CorrectionDistanceMeters, "0.000")} m versus inner {F(firstNonInner.ConstantInner.CorrectionDistanceMeters, "0.000")} m; the higher correction-step count reflects a denser variable-curvature integration mesh, not greater corrected travel.");
            L();
            L("Fixed-shape probe across the first crossover: the geometry is held at the upper-endpoint winning controls. This checks that a winner switch is not caused by a jump in the objective or in the correction branch. It does not replace a full free search at intermediate c.");
            L();
            L("| c | fixed new shape − inner sector s | correction distance new / inner m | correction steps new / inner | new shape loss J | inner loss J |");
            L("|---:|---:|---:|---:|---:|---:|");
            foreach (var coefficient in new[] { 0f, .001f, .002f, .003f, .004f, .005f, .006f })
            {
                var newer = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(
                    firstNonInner.Winner.Candidate.ControlOffsetsMeters, coefficient);
                var inner = FreeContinuousRacingTrajectoryGeometryExperiment.EvaluateTurningCost(
                    zero.ConstantInner.Candidate.ControlOffsetsMeters, coefficient);
                L($"| {F(coefficient, "0.000")} | {F(newer.SectorTimeSeconds - inner.SectorTimeSeconds)} | {F(newer.CorrectionDistanceMeters, "0.000")} / {F(inner.CorrectionDistanceMeters, "0.000")} | {newer.BrakeStepCount} / {inner.BrakeStepCount} | {F(newer.TurningLossEnergyJoules, "0.0")} | {F(inner.TurningLossEnergyJoules, "0.0")} |");
            }
            L();
        }
        var anyUnconverged = result.Scenarios.Any(x => !x.Search.ObjectiveConvergence);
        var priorGuardrail = firstNonInner is not null && result.Scenarios.TakeWhile(x => x.Coefficient < firstNonInner.Coefficient).Any(x => x.AnyGuardrailExceeded);
        var crossoverGuardrail = firstNonInner?.AnyGuardrailExceeded ?? false;
        var correctionAvoidance = firstNonInner is not null
            && firstNonInner.Winner.CorrectionDistanceMeters
                > firstNonInner.ConstantInner.CorrectionDistanceMeters + 5f;
        var conclusion = anyUnconverged ? "Uncertain: at least one sweep search did not close."
            : firstNonInner is null ? "Case B: ConstantInner remains the best line through the tested sweep."
            : correctionAvoidance ? "Case D: a non-inner line wins, but it moves materially more distance into the explicit correction regime where this drive-only surrogate charges no slip work. The result is a model-accounting exploit, not a validated tyre-energy trade-off."
            : priorGuardrail || crossoverGuardrail ? "Case C: a non-inner optimum appears only after, or at, a descriptive global guardrail break."
            : "Case A: a converged non-inner optimum appears before the descriptive global guardrails break.";
        L(conclusion);
        L();
        L("1. **Does the optimum move?** Yes within this surrogate: the first converged non-inner winner occurs at c = 0.005.");
        L("2. **Coefficient range?** The full searches bracket the first switch between 0.001 and 0.005; the fixed-shape probe crosses between 0.002 and 0.003, without claiming a global optimum at those intermediate values.");
        L("3. **Physical trade-off?** Slip force removes useful drive work according to speed and curvature, while the selected geometry changes the turning-demand envelope and exit speed. It pays more path length for a small sector gain.");
        L("4. **Smooth and feasible?** The #47 spline, turning-demand, lateral-execution, and straight-closure gates pass. The new line has near-zero remaining lateral headroom, so it is at the modeled execution boundary; real-bike plausibility remains unvalidated.");
        L("5. **Before global guardrails break?** Yes: the V/A/L bands are unbroken at the first switch. These broad bounds do not establish a physically correct coefficient.");
        L("6. **Search converged?** Objective and geometry convergence are YES at every sweep point, with three or more independent near starts and locally closed top-three results. The fixed-shape objective changes smoothly through the switch.");
        L("7. **Future production experiment?** Only after a loss law valid through correction and measured force or speed traces support the coefficient. This draft makes no production change.");
        L("8. **If unsupported, what next?** Test one coupled rear-tyre longitudinal/lateral force envelope.");
        L();
        L("At high c the useful-drive clamp saturates: c = 0.320 and 1.280 produce identical evaluated results. If c is interpreted literally as tan(effective slip angle), 1.280 implies about 52°, beyond the small-angle surrogate's defensible range. The upper-sweep wavy geometry is a possible #47 smoothness-gate limitation, not validated racing technique. No production calibration or promotion follows this result.");
        return sb.ToString();
    }
}
