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
        // Dissipation is not engine propulsion: it remains present after the
        // nonnegative engine contribution reaches zero, including during roll-off.
        return requested;
    }
}

public sealed record TurningCostInterval(
    float StartProgress, float DistanceMeters, float EntrySpeedMetersPerSecond,
    float CurvaturePerMeter, float PassiveEndSpeedMetersPerSecond,
    float ExitSpeedMetersPerSecond, double TimeSeconds, float LossForceNewtons,
    float LossPowerWatts, float AdditionalCorrectionMetersPerSecondSquared)
{
    public bool CorrectionActive => CorrectionDistanceMeters > 0f;
    public bool ControllerLimited { get; init; }
    public float CorrectionDistanceMeters { get; init; }
    public float CorrectionTimeSeconds { get; init; }
    public float CorrectionExitSpeedMetersPerSecond { get; init; }
    public double CorrectionLossEnergyJoules { get; init; }
    public double OutsideCorrectionLossEnergyJoules { get; init; }
    public double LossEnergyJoules => CorrectionLossEnergyJoules + OutsideCorrectionLossEnergyJoules;
    public bool IsTraversable { get; init; } = true;
    public float TraversedDistanceMeters { get; init; }
    public float RemainingDistanceMeters => MathF.Max(0f, DistanceMeters - TraversedDistanceMeters);
}

/// <summary>Analysis-only #47 correction-to-target, then dissipative carry; otherwise signed midpoint drive.</summary>
public static class TurningCostIntegrator
{
    public static TurningCostInterval Step(float speed, float distance, float curvature,
        float coefficient, float availability, float referenceDriveForce, BikeSetup setup,
        float? maximumEndSpeed, float correctionCapability, bool correctionRequested = false)
    {
        if (!float.IsFinite(speed) || speed <= 0f) throw new ArgumentOutOfRangeException(nameof(speed));
        if (!float.IsFinite(distance) || distance <= 0f) throw new ArgumentOutOfRangeException(nameof(distance));
        if (maximumEndSpeed is { } target && (!float.IsFinite(target) || target < 0f))
            throw new ArgumentOutOfRangeException(nameof(maximumEndSpeed));
        if (!float.IsFinite(correctionCapability) || correctionCapability <= 0f)
            throw new ArgumentOutOfRangeException(nameof(correctionCapability));
        float Acceleration(float atSpeed, out float loss)
        {
            loss = TurningCostForce.LossNewtons(coefficient, atSpeed, curvature,
                availability, referenceDriveForce, setup);
            return availability * LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(
                atSpeed, referenceDriveForce, setup)
                - loss / LongitudinalDynamics.ProvisionalNominalSystemMassKilograms;
        }
        var predicted = LongitudinalDynamics.ApplySignedAccelerationOverDistance(
            speed, Acceleration(speed, out _), distance);
        var midpoint = (float)(((double)speed + predicted) * .5d);
        var passiveAcceleration = Acceleration(midpoint, out var lossForce);
        var passive = LongitudinalDynamics.ApplySignedAccelerationOverDistance(speed, passiveAcceleration, distance);
        var limited = maximumEndSpeed is { } maximum
            && (speed > maximum || passive > maximum || correctionRequested);
        if (!limited)
        {
            var traversed = passive > 0f ? distance
                : (float)Math.Min(distance, (double)speed * speed / (-2d * passiveAcceleration));
            var work = (double)lossForce * traversed;
            return new TurningCostInterval(0f, distance, speed, curvature, passive, passive,
                2d * traversed / (speed + passive), lossForce, lossForce * midpoint, 0f)
            {
                OutsideCorrectionLossEnergyJoules = work, CorrectionExitSpeedMetersPerSecond = speed,
                IsTraversable = passive > 0f, TraversedDistanceMeters = traversed,
            };
        }

        var targetSpeed = maximumEndSpeed!.Value;
        var k = (double)coefficient * Math.Abs(curvature);
        var correctionDistance = 0f;
        var correctionExit = speed;
        var correctionTime = 0f;
        var correctionWork = 0d;
        if (speed > targetSpeed)
        {
            // Implicit distance-midpoint: v1² = v0² - 2(B + k*((v0+v1)/2)²)*ds.
            // Solve the crossing distance first, not an old full correction plus
            // a second loss debit. B is the unchanged bounded #47 control;
            // propulsion is rolled off during correction as in #47.
            var crossingMidpoint = ((double)speed + targetSpeed) * .5d;
            var required = ((double)speed * speed - (double)targetSpeed * targetSpeed)
                / (2d * (correctionCapability + k * crossingMidpoint * crossingMidpoint));
            var reached = required <= distance;
            correctionDistance = reached ? MathF.Min((float)required, distance) : distance;
            correctionExit = reached ? targetSpeed
                : DissipativeEndSpeed(speed, correctionDistance, k, correctionCapability);
            correctionTime = PhaseTime(correctionDistance, speed, correctionExit);
            var correctionMidpoint = (float)(((double)speed + correctionExit) * .5d);
            correctionWork = (double)TurningCostForce.RequestedLossNewtons(coefficient,
                correctionMidpoint, curvature) * correctionDistance;
        }

        // #47 carry is neutral, not drive. Generalize it with slip drag only.
        // The algebraic midpoint solves v1 = v0*(1-k*ds/2)/(1+k*ds/2).
        // It never raises speed, reuses correction metres, or exceeds the target.
        var requestedCarryDistance = MathF.Max(0f, distance - correctionDistance);
        // A midpoint carry reaches zero at ds=2/k. Stop there, not at the
        // requested endpoint; zero-speed carry cannot traverse extra metres.
        var carryStopped = correctionExit <= 0f || (k > 0d && 2d / k <= requestedCarryDistance);
        var carryDistance = correctionExit <= 0f ? 0f : carryStopped
            ? MathF.Min(requestedCarryDistance, (float)(2d / k)) : requestedCarryDistance;
        var exit = carryStopped ? 0f : carryDistance == 0f ? correctionExit
            : DissipativeEndSpeed(correctionExit, carryDistance, k, 0f);
        var carryTime = PhaseTime(carryDistance, correctionExit, exit);
        var carryMidpoint = (float)(((double)correctionExit + exit) * .5d);
        var carryWork = (double)TurningCostForce.RequestedLossNewtons(coefficient,
            carryMidpoint, curvature) * carryDistance;
        var workTotal = correctionWork + carryWork;
        var time = (double)correctionTime + carryTime;
        return new TurningCostInterval(0f, distance, speed, curvature, passive, exit, time,
            (float)(workTotal / ((double)correctionDistance + carryDistance)), (float)(workTotal / time),
            correctionDistance > 0f ? correctionCapability : 0f)
        {
            ControllerLimited = true,
            CorrectionDistanceMeters = correctionDistance,
            CorrectionTimeSeconds = correctionTime,
            CorrectionExitSpeedMetersPerSecond = correctionExit,
            CorrectionLossEnergyJoules = correctionWork,
            OutsideCorrectionLossEnergyJoules = carryWork,
            IsTraversable = !carryStopped && exit > 0f,
            TraversedDistanceMeters = carryStopped ? correctionDistance + carryDistance : distance,
        };
    }

    private static float DissipativeEndSpeed(float speed, float distance, double k, float control)
    {
        var halfDragDistance = .5d * k * distance;
        var denominator = 1d + halfDragDistance;
        var root = Math.Sqrt(Math.Max(0d, (double)speed * speed - 2d * control * distance * denominator));
        return (float)Math.Max(0d, (root - halfDragDistance * speed) / denominator);
    }

    private static float PhaseTime(float distance, float start, float end)
    {
        if (distance == 0f) return 0f;
        if (start + end <= 0f) throw new InvalidOperationException("Cannot traverse positive distance at zero speed.");
        // Same float phase-time convention as #47, including its c=0 limit.
        return 2f * distance / (start + end);
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
    public bool LateralExecutionBoundarySensitive => Winner.MinimumLateralExecutionHeadroomMeters <= .01f;
    public IReadOnlyList<FreeTrajectoryEvaluation> TightenedWinnerDiagnostics { get; init; } =
        Array.Empty<FreeTrajectoryEvaluation>();
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
    FreeContinuousRacingTrajectoryGeometryExperimentResult Baseline)
{
    public FreeTrajectoryEvaluation ReviewedWinnerProbe { get; init; } = null!;
    public TurningCostPhaseBoundaryProbe PhaseBoundaryProbe { get; init; } = null!;
    public TurningCostRobustnessResult Robustness { get; init; } = null!;
}

public sealed record TurningCostPhaseBoundaryProbe(FreeTrajectoryEvaluation Below,
    FreeTrajectoryEvaluation Above);

public static partial class FreeContinuousRacingTrajectoryGeometryExperiment
{
    public const string TurningCostBaseMainSha = "99f3afd08c8e6892a8b24eb785b5be6cbf116d38";
    public static IReadOnlyList<float> TurningCostSweep { get; } =
        Array.AsReadOnly(new[] { 0f, .001f, .005f, .02f, .08f, .32f, 1.28f });
    public static IReadOnlyList<float> ReviewedTurningCostWinnerControls { get; } = Array.AsReadOnly(new[]
    {
        .522111535f, .617457867f, .604532838f, .488273501f, .313086927f,
        .145888388f, .050542116f, .063467115f, .179726511f, .354912996f, .584611535f,
    });

    public static FreeTrajectoryEvaluation EvaluateTurningCost(
        IReadOnlyList<float> offsetsMeters, float coefficient, float? entrySpeedMetersPerSecond = null,
        bool captureIntervals = false, float lateralExecutionReserveMeters = 0f)
    {
        var baseline = DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 0, 0));
        return new ExperimentalGeometryReplay(CreateTrack().Geometry, baseline.CornerEntrySpeedMetersPerSecond,
            turningLossRatio: coefficient, captureTurningIntervals: captureIntervals,
            lateralExecutionReserveMeters: lateralExecutionReserveMeters)
            .Evaluate(Candidate("TurningCostProbe", offsetsMeters), entrySpeedMetersPerSecond);
    }

    public static CornerTurningSlipCostExperimentResult RunTurningCostExperiment()
    {
        var baseline = Run();
        // Freeze and evaluate reviewed geometry before searching or interpreting a new winner.
        var reviewedProbe = EvaluateTurningCost(ReviewedTurningCostWinnerControls, .005f,
            captureIntervals: true);
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
            Array.AsReadOnly(scenarios.ToArray()), baseline)
        {
            ReviewedWinnerProbe = reviewedProbe,
            PhaseBoundaryProbe = RunTurningCostPhaseBoundaryProbe(),
            Robustness = RunTurningCostRobustness(scenarios.Single(x => x.Coefficient == .005f).Winner.Candidate.ControlOffsetsMeters),
        };
    }

    public static TurningCostPhaseBoundaryProbe RunTurningCostPhaseBoundaryProbe()
    {
        var controls = new float[ControlStationCount];
        const float coefficient = .005f;
        var original = EvaluateTurningCost(controls, coefficient, captureIntervals: true);
        var first = original.TurningCostIntervals[0];
        var capability = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
            Balanced, UniformSurface);
        var force = LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(Balanced, Neutral, UniformSurface);
        var target = ReducedLookaheadTarget(ExtractTurningDemandConstraints(controls),
            first.DistanceMeters, capability)!.Value;
        // Locate the repaired correction-entry boundary with the frozen target.
        var lower = target - 1f;
        var upper = target + 1f;
        for (var index = 0; index < 40; index++)
        {
            var middle = (lower + upper) * .5f;
            var step = TurningCostIntegrator.Step(middle, first.DistanceMeters, first.CurvaturePerMeter,
                coefficient, 0f, force, Neutral, target, capability);
            if (step.CorrectionActive) upper = middle;
            else lower = middle;
        }
        var boundary = (lower + upper) * .5f;
        const float perturbationMetersPerSecond = .00005f;
        return new TurningCostPhaseBoundaryProbe(
            EvaluateTurningCost(controls, coefficient, boundary - perturbationMetersPerSecond, captureIntervals: true),
            EvaluateTurningCost(controls, coefficient, boundary + perturbationMetersPerSecond, captureIntervals: true));
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
        L("# Corner turning/slip energy cost experiment (#48)");
        L();
        L($"Base merged main: {result.BaseMainSha}. Analysis-only; production physics, geometry, #47 envelope/search/lateral rules and historical artifacts are frozen.");
        L();
        L("## Continuous dissipative model");
        L();
        L("The frozen law is F_turn = c m v²|κ|, with m = 142 kg, speed v in m/s, signed path curvature κ in 1/m and dimensionless coefficient c. Power is F_turn v (W); work is Σ F_turn Δs (J). Force vanishes only at zero coefficient, speed or curvature, not at a controller phase boundary. The coefficients are a sensitivity sweep, not a fitted tyre model.");
        L();
        L("For c > 0 a free-drive interval retains the signed distance-midpoint acceleration A(p)(F_drive(v) − F_resistance(v))/m − F_turn/m. A controller-limited interval instead preserves #47 topology: correct only while entry speed exceeds the maximum target, reach that target if possible, then process only the remaining distance. With k=c|κ| and unchanged bounded control B, distance-midpoint correction solves v1²=v0²−2[B+k((v0+v1)/2)²]ds. The target-crossing distance is (v0²−target²)/[2(B+k((v0+target)/2)²)]. If unreachable, solve the same quadratic over the accepted distance. This is an algebraic crossing, not a coefficient threshold, smoothing or a fitted offset; slip is not appended to a full old correction.");
        L();
        L("During correction propulsion is rolled off, as in #47. After reaching the target, remaining carry has zero propulsion and slip drag only: v1=v0(1−k ds/2)/(1+k ds/2), bounded below by zero. It never raises speed to the maximum target, creates energy, exceeds the target or reuses correction metres. At c→0 the same crossing and carry reduce to #47 partial correction and neutral constant-speed carry. Each subphase uses #47's float time 2ds/(v0+v1). Work is accumulated once per physical subphase at its midpoint speed; interval force/power diagnostics are work/distance and work/time, rather than an additional force debit.");
        L();
        L("A candidate that stops before completing the physical corner is explicitly invalid (NonTraversable), not rescued by a positive epsilon, fabricated propulsion, reduced loss or zero-time carry. A free-drive midpoint that reaches zero records its stopping distance v0²/(−2a); neutral midpoint carry stops at ds=2/k. Only distance/time/work up to that stop are evaluated, with finite nonnegative diagnostics and explicit remaining metres. No NaN/Infinity sentinel is used. Counts are separate from geometry, lateral and corner-control failures; coefficients/search tolerances are not tuned to keep stopped candidates alive.");
        L();
        L("Engine forward force remains nonnegative. The displayed useful-drive remainder is max(0, A F_drive − F_turn); excess turning loss remains passive drag rather than negative engine propulsion. In particular, A = 0 and correction-active intervals still dissipate energy. Unlike the rejected drive-only model, large coefficients do not saturate total dissipation at available engine drive. The #47 availability-scaled resistance abstraction is deliberately unchanged.");
        L();
        L("At c = 0 the original #47 midpoint/correction/carry branches are called directly, including partial correction distance/time. The zero scenario reuses the complete merged #47 search and repeated-lap objects. For c > 0 correction distance/time denote the physical correction subphase, not a whole controller-limited interval. Profile controller flags retain #47's limiter/carry classification; they are not used to charge a whole interval to correction. Outside-correction (Drive) work includes both actual drive and zero-drive carry/coast. Positive-c mode distances partition the physical subphases; c=0 legacy mode counters retain their old whole-interval classification, with no energy, while actual correction distance/time are comparable at every coefficient.");
        L();
        L("## Zero-cost invariant");
        L();
        L($"Winner {zero.Winner.Candidate.Id}; eleven zero controls; ConstantInner sector {F(zero.ConstantInner.SectorTimeSeconds)} s; winner sector {F(zero.Winner.SectorTimeSeconds)} s; objective {B(zero.Search.ObjectiveConvergence)}; geometry {B(zero.Search.GeometryConvergence)}; repeated lap {F(zero.RepeatedLap.FlyingLapTimeSeconds)} s. Direct-replay regressions also compare validity, full profiles and exact correction distance/time.");
        L();
        L("## Frozen independent-review exploit regression");
        L();
        var frozen = result.ReviewedWinnerProbe;
        L($"Reviewed c = 0.005 geometry: {frozen.Candidate.Id}. Controls (m): {string.Join(",", frozen.Candidate.ControlOffsetsMeters.Select(x => F(x, "0.000000000")))}.");
        L();
        L("| trajectory | total J | correction J | outside correction J | correction m | correction s | corner s | exit m/s | sector s | headroom m |");
        L("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        L($"| frozen reviewed winner | {F(frozen.TotalTurningLossEnergyJoules)} | {F(frozen.TurningLossEnergyDuringCorrectionJoules)} | {F(frozen.TurningLossEnergyDuringDriveJoules)} | {F(frozen.CorrectionDistanceMeters)} | {F(frozen.CorrectionTimeSeconds)} | {F(frozen.CornerTimeSeconds)} | {F(frozen.ExitSpeedMetersPerSecond)} | {F(frozen.SectorTimeSeconds)} | {F(frozen.MinimumLateralExecutionHeadroomMeters, "0.000000000")} |");
        L();
        L($"All accepted frozen-trajectory intervals with positive speed/nonzero curvature pay positive force, power and energy: {B(frozen.TurningCostIntervals.Count > 0 && frozen.TurningCostIntervals.All(x => x.LossForceNewtons > 0f && x.LossPowerWatts > 0f && x.LossEnergyJoules > 0d))}. The reviewed drive-only implementation had zero correction-phase work and fails this invariant.");
        L();
        L("## Phase-boundary continuity regression");
        L();
        L("Fixed ConstantInner geometry and c = 0.005. Locate the first interval's physical correction-subphase false→true boundary (entry speed equals the unmodified target) by deterministic bisection, then perturb only entry speed by ±0.00005 m/s. This constructs the repaired phase boundary; it changes neither coefficients nor the law and performs no smoothing. The whole corner and following straight are replayed. Displayed force/power are interval averages of the same subphase work.");
        L();
        L("| state | entry m/s | curvature 1/m | correction active | first force N | first power W | total J | sector s |");
        L("|---|---:|---:|:---:|---:|---:|---:|---:|");
        foreach (var item in new[] { ("below", result.PhaseBoundaryProbe.Below), ("above", result.PhaseBoundaryProbe.Above) })
        {
            var interval = item.Item2.TurningCostIntervals[0];
            L($"| {item.Item1} | {F(interval.EntrySpeedMetersPerSecond, "0.000000000")} | {F(interval.CurvaturePerMeter, "0.000000000")} | {B(interval.CorrectionActive)} | {F(interval.LossForceNewtons, "0.000000000")} | {F(interval.LossPowerWatts, "0.000000000")} | {F(item.Item2.TotalTurningLossEnergyJoules, "0.000000000")} | {F(item.Item2.SectorTimeSeconds, "0.000000000")} |");
        }
        var low = result.PhaseBoundaryProbe.Below;
        var high = result.PhaseBoundaryProbe.Above;
        L();
        L($"Absolute whole-sector change {F(Math.Abs(high.SectorTimeSeconds - low.SectorTimeSeconds), "0.000000000")} s; total-work change {F(Math.Abs(high.TotalTurningLossEnergyJoules - low.TotalTurningLossEnergyJoules), "0.000000000")} J. Regression bounds are 0.0001 s / 0.1 J, with positive force and power on both sides.");
        L();
        L("## Complete freshly rerun sensitivity sweep (unchanged coefficient grid/search settings)");
        L();
        L("| c | winner / controls m | inner sector s | winner sector s | advantage s | path m | min radius m | max curvature 1/m | corner s | exit m/s | straight s | headroom m | boundary sensitive | objective / geometry |");
        L("|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---:|:---:|");
        foreach (var s in result.Scenarios)
        {
            var w = s.Winner;
            L($"| {F(s.Coefficient, "0.000")} | {w.Candidate.SeedFamily} / {w.Candidate.Id}<br>{string.Join(",", w.Candidate.ControlOffsetsMeters.Select(x => F(x, "0.000000000")))} | {F(s.ConstantInner.SectorTimeSeconds)} | {F(w.SectorTimeSeconds)} | {F(s.ConstantInner.SectorTimeSeconds - w.SectorTimeSeconds)} | {F(w.Path!.TotalLengthMeters)} | {F(w.MinimumRadiusMeters)} | {F(w.MaximumCurvaturePerMeter)} | {F(w.CornerTimeSeconds)} | {F(w.ExitSpeedMetersPerSecond)} | {F(w.FollowingStraightTimeSeconds)} | {F(w.MinimumLateralExecutionHeadroomMeters, "0.000000000")} | {B(s.LateralExecutionBoundarySensitive)} | {B(s.Search.ObjectiveConvergence)} / {B(s.Search.GeometryConvergence)} |");
        }
        L();
        L("### Phase energy and distance accounting: controls and winners");
        L();
        L("Drive means physical distance outside active correction, including zero-drive carry/coast. Positive-c energies are independently accumulated per physical subphase; correction + drive must equal total within 1e-7 J. Average force = phase work / phase-mode distance. Correction m/s are actual active-control distance/time, and positive-c mode metres partition those subphases across the entire path. At c=0 only the legacy mode counters use whole-interval classification; all work is zero.");
        L();
        L("| c | line | valid / reason | sector s | total J | correction J | drive J | split error J | correction mode m | outside mode m | correction m | correction s | avg correction N | avg outside N |");
        L("|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var s in result.Scenarios)
        foreach (var w in s.ConstantLines.Append(s.Winner))
            L(w.IsValid
                ? $"| {F(s.Coefficient, "0.000")} | {w.Candidate.SeedFamily} | YES | {F(w.SectorTimeSeconds)} | {F(w.TotalTurningLossEnergyJoules)} | {F(w.TurningLossEnergyDuringCorrectionJoules)} | {F(w.TurningLossEnergyDuringDriveJoules)} | {F(w.TotalTurningLossEnergyJoules - w.TurningLossEnergyDuringCorrectionJoules - w.TurningLossEnergyDuringDriveJoules, "0.000000000")} | {F(w.CorrectionModeDistanceMeters)} | {F(w.DriveModeDistanceMeters)} | {F(w.CorrectionDistanceMeters)} | {F(w.CorrectionTimeSeconds)} | {F(w.AverageTurningLossForceDuringCorrectionNewtons)} | {F(w.AverageTurningLossForceDuringDriveNewtons)} |"
                : $"| {F(s.Coefficient, "0.000")} | {w.Candidate.SeedFamily} | NO / {w.InvalidReason} | — | — | — | — | — | — | — | — | — | — | — |");
        L();
        L("### Every winner versus ConstantInner: integrated trade-off");
        L();
        L("Values are winner / inner at the same coefficient, not evidence of a uniformly wider line. Integrated demand uses midpoint speed and |κ| over physical ds; time-weighted demand and radius use actual interval time.");
        L();
        L("| c | min R m | max κ 1/m | mean abs κ 1/m | time-weighted R m | time-weighted a_lat m/s² | integral v²abs(κ) ds m²/s² | total work J | drive work J | exit m/s | extra path m | lower demand / drive work / total work / better exit / longer path |");
        L("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---:|");
        foreach (var s in result.Scenarios)
        {
            var w = s.Winner;
            var i = s.ConstantInner;
            L($"| {F(s.Coefficient, "0.000")} | {F(w.MinimumRadiusMeters)} / {F(i.MinimumRadiusMeters)} | {F(w.MaximumCurvaturePerMeter)} / {F(i.MaximumCurvaturePerMeter)} | {F(w.MeanAbsoluteCurvaturePerMeter)} / {F(i.MeanAbsoluteCurvaturePerMeter)} | {F(w.TimeWeightedMeanRadiusMeters)} / {F(i.TimeWeightedMeanRadiusMeters)} | {F(w.TimeWeightedLateralDemandMetersPerSecondSquared)} / {F(i.TimeWeightedLateralDemandMetersPerSecondSquared)} | {F(w.IntegratedLateralDemandMetersSquaredPerSecondSquared)} / {F(i.IntegratedLateralDemandMetersSquaredPerSecondSquared)} | {F(w.TotalTurningLossEnergyJoules)} / {F(i.TotalTurningLossEnergyJoules)} | {F(w.TurningLossEnergyDuringDriveJoules)} / {F(i.TurningLossEnergyDuringDriveJoules)} | {F(w.ExitSpeedMetersPerSecond)} / {F(i.ExitSpeedMetersPerSecond)} | {F(w.Path!.TotalLengthMeters - i.Path!.TotalLengthMeters)} | {B(w.TimeWeightedLateralDemandMetersPerSecondSquared < i.TimeWeightedLateralDemandMetersPerSecondSquared)} / {B(w.TurningLossEnergyDuringDriveJoules < i.TurningLossEnergyDuringDriveJoules)} / {B(w.TotalTurningLossEnergyJoules < i.TotalTurningLossEnergyJoules)} / {B(w.ExitSpeedMetersPerSecond > i.ExitSpeedMetersPerSecond)} / {B(w.Path.TotalLengthMeters > i.Path.TotalLengthMeters)} |");
        }
        L();
        L("### LateralExecutionBoundarySensitive diagnostic");
        L();
        L("The unchanged YES flag means minimum numerical interval headroom ≤ 0.01 m. It is a mesh-dependent classification, not a calibrated physical margin. No centimetre-scale lateral-reserve replays are run in this repair; no old reserve results are retained. The requested physical outward/amplitude probes around the fresh c=.005 winner are reported below.");
        L();
        L("### Search closure and repeated-lap diagnostics");
        L();
        L("| c | starts | refined | top-3 closed | independent near starts | single residual s | pair residual s | objective | geometry | valid / evaluated | longitudinal stalls | repeat converged | max v m/s | avg v m/s | lap s | guardrail V/A/L |");
        L("|---:|---:|---:|:---:|---:|---:|---:|:---:|:---:|---:|---:|:---:|---:|---:|---:|:---:|");
        foreach (var s in result.Scenarios)
            L($"| {F(s.Coefficient, "0.000")} | {s.Search.StartsGenerated} | {s.Search.RefinedStartCount} | {B(s.Search.TopThreeRefinementDiagnostics.Count >= 3 && s.Search.TopThreeRefinementDiagnostics.Take(3).All(x => x.Stable))} | {s.Search.WithinToleranceFamilyCount} | {F(s.Search.LocalPerturbationImprovementSeconds)} | {F(s.Search.PairPerturbationImprovementSeconds)} | {B(s.Search.ObjectiveConvergence)} | {B(s.Search.GeometryConvergence)} | {s.Search.ValidCandidates} / {s.Search.CandidatesEvaluated} | {s.Search.InvalidNonTraversableCandidates} | {B(s.RepeatedLap.Converged)} | {F(s.RepeatedLap.MaximumSpeedMetersPerSecond)} | {F(s.RepeatedLap.AverageSpeedMetersPerSecond)} | {F(s.RepeatedLap.FlyingLapTimeSeconds)} | {B(s.MaximumSpeedBandExceeded)}/{B(s.AverageSpeedBandExceeded)}/{B(s.LapBandExceeded)} |");
        L();
        L("The unchanged descriptive ±15% guardrails reference maximum speed 111 km/h, average speed 22.38 m/s and flying lap 14.71 s. They are not optimization targets. Objective convergence and geometry convergence are separate; no conclusion is promoted from an unclosed search.");
        L();
        L("### Candidate validity counts by reason");
        L();
        L("| c | evaluated | valid | TrackBoundary | SelfIntersection | NonSmoothGeometry | LateralExecutionConstraint | RunWide | Crash | StraightRepositionConstraint | NonTraversable | initially NonTraversable |");
        L("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var s in result.Scenarios)
            L($"| {F(s.Coefficient, "0.000")} | {s.Search.CandidatesEvaluated} | {s.Search.ValidCandidates} | {s.Search.InvalidTrackBoundaryCandidates} | {s.Search.InvalidSelfIntersectionCandidates} | {s.Search.InvalidNonSmoothGeometryCandidates} | {s.Search.InvalidLateralExecutionCandidates} | {s.Search.InvalidCornerControlDepartureCandidates} | {s.Search.InvalidCrashCandidates} | {s.Search.InvalidStraightRepositionCandidates} | {s.Search.InvalidNonTraversableCandidates} | {s.Search.InvalidInitialNonTraversable} |");
        L();
        L("Strong dissipation can legitimately invalidate high-c trajectories. NonTraversable counts must be read alongside closure and Motoarena guardrails: ObjectiveConvergence=NO is not a verified optimum, and broken global guardrails are not calibration evidence.");
        L();
        L("## Interpretation after repair");
        L();
        var atFive = result.Scenarios.Single(x => x.Coefficient == .005f);
        L(atFive.Search.ObjectiveConvergence
            ? atFive.Winner.Candidate.Id == zero.Winner.Candidate.Id
                ? "At c = 0.005 ConstantInner is best in the fresh search: no old non-inner conclusion is preserved by assumption."
                : "At c = 0.005 the fresh search finds a non-inner candidate paying turning loss through the whole corner, with #47 partial-correction topology. Its integrated trade-offs and numerical lateral-boundary dependence are reported above; controller continuity does not automatically validate the physical model."
            : "At c = 0.005 the search is not objectively converged; no winner interpretation is justified.");
        L();
        var first = result.Scenarios.Skip(1).FirstOrDefault(x => x.Search.ObjectiveConvergence
            && x.Winner.SectorTimeSeconds < x.ConstantInner.SectorTimeSeconds - 1e-5f);
        L(first is null ? "No objectively converged non-inner advantage in the sampled sweep."
            : $"First objectively converged sampled non-inner advantage: c = {F(first.Coefficient, "0.000")}; gain {F(first.ConstantInner.SectorTimeSeconds - first.Winner.SectorTimeSeconds)} s. This is a sampled result, not a fitted continuous threshold.");
        L();
        L("Any surviving non-inner result must be judged by the full-corner energy/demand comparison, exit speed, extra path and lateral-boundary diagnostic, not by the name 'wider'. No controller state receives free dissipation. Large c remains an uncalibrated effective-slip surrogate; c = 1.280 is far outside a small-angle interpretation. Unclosed searches and broken global guardrails remain explicit uncertainty. No production promotion follows; a coupled longitudinal/lateral tyre-force envelope is a separate future hypothesis, not part of this repair.");
        L();
        sb.Append(TurningCostRobustnessReport.Render(result.Robustness));
        return sb.ToString();
    }
}
