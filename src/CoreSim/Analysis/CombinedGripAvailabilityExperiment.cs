using System.Globalization;
using System.Text;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record CombinedGripState(
    float LateralAccelerationMetersPerSecondSquared,
    float LateralUtilization,
    float LongitudinalAvailabilityFactor,
    float LongitudinalCapacityNewtons,
    float RequestedLongitudinalForceNewtons,
    float AvailableLongitudinalForceNewtons,
    float ClippedDriveForceNewtons);

/// <summary>
/// Analysis-only positive propulsion allocation. No phase, line or lane input.
/// Effective scrub is retained, not reinterpreted as a friction brake.
/// </summary>
public static class CombinedGripAvailability
{
    public static float DeriveLateralAccelerationCapacity(TrackSurfaceState surface,
        RiderSkills skills, BikeSetup setup,
        float referenceSpeedMetersPerSecond = SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond)
    {
        var radius = SegmentPhysics.ReferenceTurnRadiusMeters;
        var settled = SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference(
            radius, referenceSpeedMetersPerSecond, surface, skills, setup);
        return settled * settled / radius;
    }

    public static void ValidateCoupling(float coupling)
    {
        if (!float.IsFinite(coupling) || coupling < 0f || coupling > 1f)
            throw new ArgumentOutOfRangeException(nameof(coupling));
    }

    public static CombinedGripState Evaluate(float speedMetersPerSecond, float curvaturePerMeter,
        float lateralAccelerationCapacityMetersPerSecondSquared,
        float fullDriveCapacityNewtons, float requestedLongitudinalForceNewtons, float coupling)
    {
        ValidateCoupling(coupling);
        if (!float.IsFinite(speedMetersPerSecond) || speedMetersPerSecond < 0f)
            throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond));
        if (!float.IsFinite(curvaturePerMeter)) throw new ArgumentOutOfRangeException(nameof(curvaturePerMeter));
        if (!float.IsFinite(lateralAccelerationCapacityMetersPerSecondSquared)
            || lateralAccelerationCapacityMetersPerSecondSquared <= 0f)
            throw new ArgumentOutOfRangeException(nameof(lateralAccelerationCapacityMetersPerSecondSquared));
        if (!float.IsFinite(fullDriveCapacityNewtons) || fullDriveCapacityNewtons < 0f)
            throw new ArgumentOutOfRangeException(nameof(fullDriveCapacityNewtons));
        if (!float.IsFinite(requestedLongitudinalForceNewtons))
            throw new ArgumentOutOfRangeException(nameof(requestedLongitudinalForceNewtons));
        var lateral = (double)speedMetersPerSecond * speedMetersPerSecond * Math.Abs(curvaturePerMeter);
        var utilization = lateral / lateralAccelerationCapacityMetersPerSecondSquared;
        var factor = coupling == 0f ? 1d : Math.Sqrt(Math.Max(0d, 1d - coupling * utilization * utilization));
        var capacity = (float)(fullDriveCapacityNewtons * factor);
        // Negative action describes existing effective scrub, not tyre braking.
        var available = requestedLongitudinalForceNewtons <= 0f
            ? requestedLongitudinalForceNewtons : MathF.Min(requestedLongitudinalForceNewtons, capacity);
        if (lateral > float.MaxValue || utilization > float.MaxValue)
            throw new OverflowException("Lateral demand exceeds the finite single-precision domain.");
        return new CombinedGripState((float)lateral, (float)utilization, (float)factor, capacity,
            requestedLongitudinalForceNewtons, available,
            MathF.Max(0f, requestedLongitudinalForceNewtons - available));
    }
}

public static class CombinedGripIntegrator
{
    private static float Acceleration(float speed, float curvature, float lateralCapacity,
        float coupling, float referenceForce, BikeSetup setup, float availability)
    {
        if (coupling == 0f)
            return availability * LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(
                speed, referenceForce, setup);
        var engine = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(referenceForce, speed, setup);
        var state = CombinedGripAvailability.Evaluate(speed, curvature, lateralCapacity,
            engine, availability * engine, coupling);
        return (state.AvailableLongitudinalForceNewtons - availability
            * LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed))
            / LongitudinalDynamics.ProvisionalNominalSystemMassKilograms;
    }

    public static float PredictorMidpointSpeed(float speed, float distance, float curvature,
        float lateralCapacity, float coupling, float referenceForce, BikeSetup setup, float availability)
    {
        CombinedGripAvailability.ValidateCoupling(coupling);
        if (!float.IsFinite(availability) || availability < 0f || availability > 1f)
            throw new ArgumentOutOfRangeException(nameof(availability));
        var predicted = LongitudinalDynamics.ApplySignedAccelerationOverDistance(speed,
            Acceleration(speed, curvature, lateralCapacity, coupling, referenceForce, setup, availability), distance);
        return (float)(((double)speed + predicted) * .5d);
    }

    public static float DriveEndSpeed(float speed, float distance, float curvature,
        float lateralCapacity, float coupling, float referenceForce, BikeSetup setup, float availability)
    {
        CombinedGripAvailability.ValidateCoupling(coupling);
        if (coupling == 0f)
            return LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(
                speed, distance, referenceForce, setup, availability);
        var midpoint = PredictorMidpointSpeed(speed, distance, curvature, lateralCapacity,
            coupling, referenceForce, setup, availability);
        return LongitudinalDynamics.ApplySignedAccelerationOverDistance(speed,
            Acceleration(midpoint, curvature, lateralCapacity, coupling, referenceForce, setup, availability), distance);
    }
}

public sealed record CombinedGripInterval(float StartProgress, float EndProgress, float MidpointProgress,
    float DistanceMeters, float EntrySpeedMetersPerSecond, float ExitSpeedMetersPerSecond,
    float ForceEvaluationSpeedMetersPerSecond, float CurvaturePerMeter, float ProgressDriveAvailability,
    bool ControllerLimited, float CorrectionDistanceMeters, CombinedGripState Grip)
{
    public float TravelTimeSeconds { get; init; }
    public float MaximumLateralAllowedMeters { get; init; }
    public float RequiredLateralMovementMeters { get; init; }
    public float UnreservedLateralHeadroomMeters => MaximumLateralAllowedMeters - RequiredLateralMovementMeters;
    public string ControllerState => ControllerLimited
        ? CorrectionDistanceMeters > 0f ? "Correction/carry" : "Carry"
        : ProgressDriveAvailability > 0f ? "Drive" : "Neutral";
}

public sealed record CombinedGripCase(FreeTrajectoryEvaluation Evaluation, RepeatedTrajectoryLapResult RepeatedLap);
public sealed record CombinedGripScenario(float Coupling, FreeTrajectorySearchResult Search,
    IReadOnlyList<CombinedGripCase> ConstantLines, CombinedGripCase Winner,
    CombinedGripCase BestNonConstant, FreeTrajectoryEvaluation WinnerAtZero);
public sealed record CombinedGripPerturbation(string Kind, float ShiftMeters, FreeTrajectoryEvaluation Evaluation);
public sealed record CombinedGripPhaseCheck(int Intervals, int ControllerTransitions,
    int MissingCapacityIntervals, float MaximumCapacityResidualNewtons,
    float MaximumTransitionCapacityResidualNewtons, float MaximumTransitionCurvatureRatio);
public sealed record CombinedGripExperimentResult(string BaseMainSha, float EntrySpeedMetersPerSecond,
    float LateralAccelerationCapacityMetersPerSecondSquared, IReadOnlyList<CombinedGripScenario> Scenarios,
    float PerturbationCoupling, FreeTrajectoryEvaluation PerturbationReference,
    IReadOnlyList<CombinedGripPerturbation> Perturbations, bool ZeroBaselineExact);

public static partial class FreeContinuousRacingTrajectoryGeometryExperiment
{
    public const string CombinedGripBaseMainSha = "58771bac635d7d4962e11aa3ae5ad37a2d0c8d94";
    public static IReadOnlyList<float> CombinedGripSweep { get; } = Array.AsReadOnly(new[] { 0f, .25f, .5f, .75f, 1f });

    public static FreeTrajectoryEvaluation EvaluateCombinedGrip(IReadOnlyList<float> offsetsMeters,
        float coupling, float? entrySpeedMetersPerSecond = null, bool captureIntervals = true,
        float lateralExecutionReserveMeters = 0f)
    {
        var baseline = DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 0, 0));
        return new ExperimentalGeometryReplay(CreateTrack().Geometry, baseline.CornerEntrySpeedMetersPerSecond,
            lateralExecutionReserveMeters: lateralExecutionReserveMeters,
            combinedGripCoupling: coupling, captureCombinedGripIntervals: captureIntervals)
            .Evaluate(Candidate("CombinedGripProbe", offsetsMeters), entrySpeedMetersPerSecond);
    }

    public static CombinedGripExperimentResult RunCombinedGripExperiment()
    {
        var baseline = DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 0, 0));
        var geometry = CreateTrack().Geometry;
        var entry = baseline.CornerEntrySpeedMetersPerSecond;
        var legacy = new ExperimentalGeometryReplay(geometry, entry);
        var scenarios = new List<CombinedGripScenario>();
        var zeroExact = true;
        foreach (var coupling in CombinedGripSweep)
        {
            var evaluator = new ExperimentalGeometryReplay(geometry, entry, combinedGripCoupling: coupling);
            var diagnostic = new ExperimentalGeometryReplay(geometry, entry, combinedGripCoupling: coupling,
                captureCombinedGripIntervals: true);
            var constants = Enumerable.Range(0, 5).Select(lane =>
            {
                var name = new[] { "ConstantInner", "ConstantL1", "ConstantL2", "ConstantL3", "ConstantOuter" }[lane];
                var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(lane, SegmentType.TurnMiddle, geometry);
                return Capture(Candidate(name, Enumerable.Repeat(offset, ControlStationCount)));
            }).ToArray();
            var search = RunSearch(evaluator, geometry);
            var winner = Capture(search.TopTwenty[0].Candidate);
            var nonConstant = search.TopTwenty.FirstOrDefault(item => item.IsNonConstant)
                ?? search.FamilyResults.Select(item => item.Best).Where(item => item.IsValid && item.IsNonConstant)
                    .OrderBy(item => item.SectorTimeSeconds).ThenBy(item => item.Candidate.Id, StringComparer.Ordinal).First();
            var nonConstantCase = Capture(nonConstant.Candidate);
            var atZero = legacy.Evaluate(winner.Evaluation.Candidate);
            if (coupling == 0f)
            {
                // Separate old-path search: compare exact objective, geometry, validity and convergence.
                var oldSearch = RunSearch(legacy, geometry);
                zeroExact &= SearchSignature(oldSearch) == SearchSignature(search);
                foreach (var item in constants.Select(x => x.Evaluation).Append(winner.Evaluation).Append(nonConstantCase.Evaluation))
                    zeroExact &= BaselineSignature(legacy.Evaluate(item.Candidate)) == BaselineSignature(item);
            }
            scenarios.Add(new CombinedGripScenario(coupling, search, Array.AsReadOnly(constants), winner,
                nonConstantCase, atZero));
            CombinedGripCase Capture(FreeTrajectoryCandidate candidate) => new(diagnostic.Evaluate(candidate),
                RunRepeatedLap(evaluator, candidate, entry));
        }
        // Best found non-constant, measured as its advantage relative to inner at the same q.
        // This selects the robustness subject after the frozen sweep, never changes the search.
        var chosen = scenarios.Skip(1).OrderBy(s => s.BestNonConstant.Evaluation.SectorTimeSeconds
            - s.ConstantLines[0].Evaluation.SectorTimeSeconds).ThenBy(s => s.Coupling).First();
        var reference = chosen.BestNonConstant.Evaluation;
        var shifts = new[] { -.15f, -.10f, -.05f, 0f, .05f, .10f, .15f };
        var probes = shifts.Select(shift =>
            new CombinedGripPerturbation("Outward translation", shift, EvaluateCombinedGrip(
                reference.Candidate.ControlOffsetsMeters.Select(x => x + shift).ToArray(), chosen.Coupling)))
            .Concat(shifts.Where(shift => shift != 0f).Select(shift =>
                new CombinedGripPerturbation("Smooth exit fan", shift, EvaluateCombinedGrip(
                    reference.Candidate.ControlOffsetsMeters.Select((x, i) =>
                        x + shift * ExitFanWeight(i)).ToArray(), chosen.Coupling)))).ToArray();
        return new CombinedGripExperimentResult(CombinedGripBaseMainSha, entry,
            CombinedGripAvailability.DeriveLateralAccelerationCapacity(UniformSurface, Balanced, Neutral),
            Array.AsReadOnly(scenarios.ToArray()), chosen.Coupling, reference, Array.AsReadOnly(probes), zeroExact);
    }

    public static float ExitFanWeight(int controlIndex)
    {
        if (controlIndex < 0 || controlIndex >= ControlStationCount)
            throw new ArgumentOutOfRangeException(nameof(controlIndex));
        var t = Math.Clamp((controlIndex / 10f - .7f) / .3f, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public static string BaselineSignature(FreeTrajectoryEvaluation item)
    {
        // Round-trip formats compare actual float outputs without tolerance widening.
        var values = new[] { item.CornerTimeSeconds, item.FollowingStraightTimeSeconds, item.SectorTimeSeconds,
            item.ExitSpeedMetersPerSecond, item.CorrectionDistanceMeters, item.CorrectionTimeSeconds,
            item.MinimumLateralExecutionHeadroomMeters, item.EndStraightSpeedMetersPerSecond };
        return item.Candidate.Id + ":" + item.Validity + ":" + item.InvalidReason + ":"
            + string.Join(",", values.Select(x => x.ToString("R", CultureInfo.InvariantCulture)))
            + ":" + string.Join(";", item.Profile.Select(x => x.ToString()));
    }

    private static string SearchSignature(FreeTrajectorySearchResult search)
        => string.Join("|", search.TopTwenty.Select(BaselineSignature)) + ":" + search.CandidatesEvaluated
            + ":" + search.ValidCandidates + ":" + search.ObjectiveConvergence + ":" + search.GeometryConvergence
            + ":" + search.WithinToleranceFamilyCount;
}

public static class CombinedGripDiagnostics
{
    public static CombinedGripPhaseCheck Check(FreeTrajectoryEvaluation evaluation, float coupling, float lateralCapacity)
    {
        var intervals = evaluation.CombinedGripIntervals;
        var residual = 0f;
        var transitionResidual = 0f;
        var transitionCurvature = 1f;
        var transitions = 0;
        var missing = 0;
        for (var index = 0; index < intervals.Count; index++)
        {
            var point = intervals[index];
            var engine = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(
                LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(RiderSkills.Balanced,
                    BikeSetup.Neutral, CalibrationScenarioCatalog.Baseline.Surface),
                point.ForceEvaluationSpeedMetersPerSecond, BikeSetup.Neutral);
            var expected = CombinedGripAvailability.Evaluate(point.ForceEvaluationSpeedMetersPerSecond,
                point.CurvaturePerMeter, lateralCapacity, engine, point.Grip.RequestedLongitudinalForceNewtons, coupling);
            var error = MathF.Abs(point.Grip.LongitudinalCapacityNewtons - expected.LongitudinalCapacityNewtons);
            residual = MathF.Max(residual, error);
            if (point.Grip != expected) missing++;
            if (index == 0 || point.ControllerLimited == intervals[index - 1].ControllerLimited) continue;
            transitions++;
            transitionResidual = MathF.Max(transitionResidual, error);
            var prior = intervals[index - 1];
            transitionCurvature = MathF.Max(transitionCurvature,
                MathF.Max(point.CurvaturePerMeter / prior.CurvaturePerMeter,
                    prior.CurvaturePerMeter / point.CurvaturePerMeter));
        }
        return new CombinedGripPhaseCheck(intervals.Count, transitions, missing, residual, transitionResidual, transitionCurvature);
    }

    public static float PeakUtilization(FreeTrajectoryEvaluation evaluation, float lateralCapacity)
        => evaluation.CombinedGripIntervals.Max(x => MathF.Max(x.EntrySpeedMetersPerSecond * x.EntrySpeedMetersPerSecond,
            x.ExitSpeedMetersPerSecond * x.ExitSpeedMetersPerSecond) * MathF.Abs(x.CurvaturePerMeter) / lateralCapacity);
}

public static class CombinedGripAvailabilityReport
{
    public static string Render(CombinedGripExperimentResult result)
    {
        var sb = new StringBuilder();
        void L(string line = "") => sb.Append(line).Append('\n');
        static string F(double x) => x.ToString("0.000000000", CultureInfo.InvariantCulture);
        static string B(bool x) => x ? "YES" : "NO";
        L("# Combined longitudinal/lateral availability — analysis-only experiment"); L();
        L($"Fetched base main: `{result.BaseMainSha}`. Audit and sweep frozen in `combined-grip-audit.md` before implementation/search. Production and reference 19.0f unchanged. #48 dissipative cost is off."); L();
        L($"a_capacity = v_settled(24)²/24 = {F(result.LateralAccelerationCapacityMetersPerSecondSquared)} m/s²; m=142 kg. u=v²abs(kappa)/a_capacity; factor=sqrt(max(0,1-q*u²)); propulsion=min(A*F_engine, factor*F_engine). Existing A-scaled resistance remains. Correction/carry is the unchanged effective scrub, with zero positive request; it is not mapped to friction braking. Every interval still has the same physical propulsion capacity, regardless of controller state."); L();
        L("q = 0/.25/.5/.75/1 means disabled/quarter/half/three-quarter/full shared lateral budget; one new independent dimensionless parameter. Engine force supplies the existing longitudinal reference; it is not a measured tyre cap. No empirical q is available. F_lat is demand, not dissipative loss. q=1 can saturate during legal recoverable overspeed: this is an explicit surrogate limitation."); L();
        L($"Direct old-path baseline, separate optimizer replay: exact equality **{B(result.ZeroBaselineExact)}**. No broad numeric tolerance. Same spline, 11 controls, 2049 samples, validity and search settings."); L();
        L("## Fixed lines and free trajectory"); L();
        L("Peak u includes both interval endpoint speeds; factor/force metrics use actual force-evaluation states. Headroom is numerical lateral movement reserve in m, not tyre-grip reserve. Repeated lap uses the unchanged two-sector iteration; a non-converged lap has no valid time."); L();
        L("| q | case | path m | min R m | max k 1/m | lateral headroom m | corner s | exit m/s | straight s | sector s | flying lap s / converged | peak a_lat m/s² | peak u | min factor | max request N | max usable N | max clipped N | withheld drive work J | correction m / s |");
        L("|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var s in result.Scenarios)
        foreach (var c in s.ConstantLines.Append(s.Winner))
        {
            var e = c.Evaluation;
            if (!e.IsValid) { L($"| {F(s.Coupling)} | {e.Candidate.SeedFamily} | INVALID: {e.InvalidReason} |"); continue; }
            var p = e.CombinedGripIntervals;
            var peak = CombinedGripDiagnostics.PeakUtilization(e, result.LateralAccelerationCapacityMetersPerSecondSquared);
            L($"| {F(s.Coupling)} | {e.Candidate.SeedFamily} / {e.Candidate.Id} | {F(e.Path!.TotalLengthMeters)} | {F(e.MinimumRadiusMeters)} | {F(e.MaximumCurvaturePerMeter)} | {F(e.MinimumLateralExecutionHeadroomMeters)} | {F(e.CornerTimeSeconds)} | {F(e.ExitSpeedMetersPerSecond)} | {F(e.FollowingStraightTimeSeconds)} | {F(e.SectorTimeSeconds)} | {(c.RepeatedLap.Converged ? F(c.RepeatedLap.FlyingLapTimeSeconds) : "—")} / {B(c.RepeatedLap.Converged)} | {F(peak * result.LateralAccelerationCapacityMetersPerSecondSquared)} | {F(peak)} | {F(p.Min(x => x.Grip.LongitudinalAvailabilityFactor))} | {F(p.Max(x => x.Grip.RequestedLongitudinalForceNewtons))} | {F(p.Max(x => x.Grip.AvailableLongitudinalForceNewtons))} | {F(p.Max(x => x.Grip.ClippedDriveForceNewtons))} | {F(p.Sum(x => (double)x.Grip.ClippedDriveForceNewtons * x.DistanceMeters))} | {F(e.CorrectionDistanceMeters)} / {F(e.CorrectionTimeSeconds)} |");
        }
        L(); L("Negative request is the distance-weighted effective scrub force m*a, reported separately below. Withheld drive work is missing propulsive work, not a lateral energy debit."); L();
        L("| q | case | peak drive request/usable/clipped m/s² | peak effective scrub request/usable m/s² | scrub clipped N | controls m |");
        L("|---:|---|---|---|---:|---|");
        foreach (var s in result.Scenarios)
        foreach (var c in s.ConstantLines.Append(s.Winner).Where(c => c.Evaluation.IsValid))
        {
            var e = c.Evaluation; var p = e.CombinedGripIntervals; const float m = LongitudinalDynamics.ProvisionalNominalSystemMassKilograms;
            L($"| {F(s.Coupling)} | {e.Candidate.SeedFamily} | {F(p.Max(x => MathF.Max(0f, x.Grip.RequestedLongitudinalForceNewtons)) / m)} / {F(p.Max(x => MathF.Max(0f, x.Grip.AvailableLongitudinalForceNewtons)) / m)} / {F(p.Max(x => x.Grip.ClippedDriveForceNewtons) / m)} | {F(p.Min(x => x.Grip.RequestedLongitudinalForceNewtons) / m)} / {F(p.Min(x => x.Grip.AvailableLongitudinalForceNewtons) / m)} | 0 | {string.Join(",", e.Candidate.ControlOffsetsMeters.Select(x => x.ToString("R", CultureInfo.InvariantCulture)))} |");
        }
        L(); L("## Search evidence"); L();
        L("| q | starts/refined | objective | geometry | independent near starts | top-3 closed | single/pair residual s | valid/evaluated | boundary/self intersection/non smooth/lateral/run wide/crash/reposition/non traversable |");
        L("|---:|---|---|---|---:|---|---|---|---|");
        foreach (var s in result.Scenarios)
        {
            var x = s.Search;
            L($"| {F(s.Coupling)} | {x.StartsGenerated}/{x.RefinedStartCount} | {B(x.ObjectiveConvergence)} | {B(x.GeometryConvergence)} | {x.WithinToleranceFamilyCount} | {B(x.TopThreeStable)} | {F(x.LocalPerturbationImprovementSeconds)}/{F(x.PairPerturbationImprovementSeconds)} | {x.ValidCandidates}/{x.CandidatesEvaluated} | {x.InvalidTrackBoundaryCandidates}/{x.InvalidSelfIntersectionCandidates}/{x.InvalidNonSmoothGeometryCandidates}/{x.InvalidLateralExecutionCandidates}/{x.InvalidCornerControlDepartureCandidates}/{x.InvalidCrashCandidates}/{x.InvalidStraightRepositionCandidates}/{x.InvalidNonTraversableCandidates} |");
        }
        L(); L("## Winner versus inner: physical exit trade-off"); L();
        L("Like-progress states use interpolated speed and exact spline curvature; potential propulsion is computed independently of the controller label. Each cell is winner / inner. Lower demand need not hold over the entire corner; the powered exit is the critical comparison."); L();
        L("| q | progress | curvature 1/m | u | factor | potential usable drive N | speed m/s |");
        L("|---:|---:|---|---|---|---|---|");
        foreach (var s in result.Scenarios.Where(s => s.Coupling > 0f))
        foreach (var progress in new[] { .5f, .6f, .7f, .8f, .9f, 1f })
        {
            (CombinedGripState Grip, float Speed, float Curvature) At(FreeTrajectoryEvaluation e)
            {
                var point = e.Path!.PointAt(progress);
                var speed = e.Profile.OrderBy(p => MathF.Abs(p.Progress - progress)).First().SpeedMetersPerSecond;
                var engine = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(
                    LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(RiderSkills.Balanced,
                        BikeSetup.Neutral, CalibrationScenarioCatalog.Baseline.Surface), speed, BikeSetup.Neutral);
                return (CombinedGripAvailability.Evaluate(speed, point.CurvaturePerMeter,
                    result.LateralAccelerationCapacityMetersPerSecondSquared, engine,
                    ContinuousCornerEnvelope.DriveAvailability(progress) * engine, s.Coupling), speed, point.CurvaturePerMeter);
            }
            var w = At(s.Winner.Evaluation); var i = At(s.ConstantLines[0].Evaluation);
            L($"| {F(s.Coupling)} | {F(progress)} | {F(w.Curvature)}/{F(i.Curvature)} | {F(w.Grip.LateralUtilization)}/{F(i.Grip.LateralUtilization)} | {F(w.Grip.LongitudinalAvailabilityFactor)}/{F(i.Grip.LongitudinalAvailabilityFactor)} | {F(w.Grip.AvailableLongitudinalForceNewtons)}/{F(i.Grip.AvailableLongitudinalForceNewtons)} | {F(w.Speed)}/{F(i.Speed)} |");
        }
        L(); L("| q | geometry max curvature progress | minimum R progress | zero-replay validity | drive-only mean u / factor (winner / inner) | drive work usable J (winner / inner) | flying lap gain s |");
        L("|---:|---:|---:|---|---|---|---:|");
        foreach (var s in result.Scenarios)
        {
            var w = s.Winner.Evaluation; var i = s.ConstantLines[0].Evaluation;
            (double U, double Factor, double Work) Summary(FreeTrajectoryEvaluation e)
            {
                var drive = e.CombinedGripIntervals.Where(x => x.Grip.RequestedLongitudinalForceNewtons > 0f).ToArray();
                var distance = drive.Sum(x => (double)x.DistanceMeters);
                return (drive.Sum(x => (double)x.Grip.LateralUtilization * x.DistanceMeters) / distance,
                    drive.Sum(x => (double)x.Grip.LongitudinalAvailabilityFactor * x.DistanceMeters) / distance,
                    drive.Sum(x => (double)x.Grip.AvailableLongitudinalForceNewtons * x.DistanceMeters));
            }
            var ws = Summary(w); var ins = Summary(i);
            L($"| {F(s.Coupling)} | {F(w.MaximumCurvatureProgress)} | {F(w.MinimumRadiusProgress)} | {s.WinnerAtZero.Validity} / {s.WinnerAtZero.InvalidReason} | {F(ws.U)}/{F(ins.U)} ; {F(ws.Factor)}/{F(ins.Factor)} | {F(ws.Work)}/{F(ins.Work)} | {F(s.ConstantLines[0].RepeatedLap.FlyingLapTimeSeconds - s.Winner.RepeatedLap.FlyingLapTimeSeconds)} |");
        }
        L(); L("### Top candidates (five per q)"); L();
        L("| q | rank | ID | sector s | path m | exit m/s | controls m |"); L("|---:|---:|---|---:|---:|---:|---|");
        foreach (var s in result.Scenarios)
        for (var index = 0; index < Math.Min(5, s.Search.TopTwenty.Count); index++)
        {
            var e = s.Search.TopTwenty[index];
            L($"| {F(s.Coupling)} | {index + 1} | {e.Candidate.Id} | {F(e.SectorTimeSeconds)} | {F(e.Path!.TotalLengthMeters)} | {F(e.ExitSpeedMetersPerSecond)} | {string.Join(",", e.Candidate.ControlOffsetsMeters.Select(x => x.ToString("R", CultureInfo.InvariantCulture)))} |");
        }
        L(); L("## Phase gaming diagnostics"); L();
        L("Every actual integration interval is recomputed automatically, including Correction and Carry. A missing or phase-disabled capacity yields nonzero residual/count. The trace includes 21 distributed intervals, both neighbors of every controller transition, and maximum curvature/utilization/clipping intervals. A controller transition may change requested action; it cannot change capacity at fixed physical state. High curvature during zero positive request is ordinary force allocation, not exemption from this law."); L();
        foreach (var s in result.Scenarios)
        {
            var e = s.Winner.Evaluation; var p = e.CombinedGripIntervals;
            var check = CombinedGripDiagnostics.Check(e, s.Coupling, result.LateralAccelerationCapacityMetersPerSecondSquared);
            L($"### q={F(s.Coupling)} winner {e.Candidate.Id}"); L();
            L($"Intervals {check.Intervals}; controller transitions {check.ControllerTransitions}; missing capacity {check.MissingCapacityIntervals}; maximum capacity residual {F(check.MaximumCapacityResidualNewtons)} N; transition residual {F(check.MaximumTransitionCapacityResidualNewtons)} N; largest adjacent curvature ratio at transitions {F(check.MaximumTransitionCurvatureRatio)}. Zero replay: {s.WinnerAtZero.Validity}, sector {(s.WinnerAtZero.IsValid ? F(s.WinnerAtZero.SectorTimeSeconds) : "— / " + s.WinnerAtZero.InvalidReason)} s."); L();
            L("| progress | entry/force-state/exit m/s | k 1/m | u | factor | request N | capacity N | usable N | clipped N | controller | A |");
            L("|---:|---|---:|---:|---:|---:|---:|---:|---:|---|---:|");
            var indices = Enumerable.Range(0, 21).Select(i => i * (p.Count - 1) / 20).ToHashSet();
            indices.Add(p.ToList().FindIndex(x => x.CurvaturePerMeter == p.Max(y => y.CurvaturePerMeter)));
            indices.Add(p.ToList().FindIndex(x => x.Grip.LateralUtilization == p.Max(y => y.Grip.LateralUtilization)));
            indices.Add(p.ToList().FindIndex(x => x.Grip.ClippedDriveForceNewtons == p.Max(y => y.Grip.ClippedDriveForceNewtons)));
            for (var i = 1; i < p.Count; i++)
                if (p[i].ControllerLimited != p[i - 1].ControllerLimited)
                    foreach (var j in new[] { i - 1, i, Math.Min(p.Count - 1, i + 1) }) indices.Add(j);
            foreach (var i in indices.Order())
            {
                var x = p[i]; var g = x.Grip;
                L($"| {F(x.MidpointProgress)} | {F(x.EntrySpeedMetersPerSecond)}/{F(x.ForceEvaluationSpeedMetersPerSecond)}/{F(x.ExitSpeedMetersPerSecond)} | {F(x.CurvaturePerMeter)} | {F(g.LateralUtilization)} | {F(g.LongitudinalAvailabilityFactor)} | {F(g.RequestedLongitudinalForceNewtons)} | {F(g.LongitudinalCapacityNewtons)} | {F(g.AvailableLongitudinalForceNewtons)} | {F(g.ClippedDriveForceNewtons)} | {x.ControllerState} | {F(x.ProgressDriveAvailability)} |");
            }
            L();
        }
        L("## Physical perturbations of the best found non-constant candidate"); L();
        L($"q={F(result.PerturbationCoupling)}, reference {result.PerturbationReference.Candidate.Id}, sector {F(result.PerturbationReference.SectorTimeSeconds)} s. Translation shifts all eleven controls; exit fan adds shift*smoothstep((p-.7)/.3) at the same eleven control stations. No clipping, refinement or replacement of the search winner."); L();
        L("| kind | shift m | valid/reason | sector s | delta ms | lateral headroom m |"); L("|---|---:|---|---:|---:|---:|");
        foreach (var probe in result.Perturbations)
        {
            var e = probe.Evaluation;
            L(e.IsValid ? $"| {probe.Kind} | {F(probe.ShiftMeters)} | YES | {F(e.SectorTimeSeconds)} | {F(1000d * (e.SectorTimeSeconds - result.PerturbationReference.SectorTimeSeconds))} | {F(e.MinimumLateralExecutionHeadroomMeters)} |"
                : $"| {probe.Kind} | {F(probe.ShiftMeters)} | NO / {e.InvalidReason} | — | — | — |");
        }
        L(); L("Robustness slopes are diagnostics, not another optimum: invalid inward translations identify the track boundary. Read both signs of the local exit fan to distinguish a smooth time change from a boundary-only improvement.");
        L(); L("## Required answers"); L();
        L("1. #47 settled turning speed is an effective lateral-acceleration capability; its recoverable lookahead also includes scrub distance, not a hard instantaneous tyre limit.");
        L("2. A second independent lateral cap would duplicate it. Positive-force allocation adds a previously absent simultaneous longitudinal restriction.");
        L("3. Reuse v_settled²/R; no new lateral cap, energy loss or validity gate; do not stack #48 loss.");
        L($"4. q=0 directly executes #47; exact baseline/independent-search comparison: {B(result.ZeroBaselineExact)}.");
        L("5. Capacity is a pure state function without controller/line labels. Identical signed requests have identical available results.");
        L("6. No controller-label discontinuity or capacity disappearance was detected. However, effective correction/scrub remains outside the positive-propulsion combined-force allocation, so a physically meaningful correction-phase exploitation cannot yet be excluded. This is an unresolved interpretation limit, not a proven bug; no braking traction ellipse or scrub-force decomposition is invented.");
        foreach (var s in result.Scenarios)
        {
            var e = s.Winner.Evaluation; var inner = s.ConstantLines[0].Evaluation;
            var gain = inner.SectorTimeSeconds - e.SectorTimeSeconds;
            L($"7–9. q={F(s.Coupling)}: ConstantInner winner {B(e.Candidate.Id == inner.Candidate.Id)}; best-found gain {F(gain)} s; extra path {F(e.Path!.TotalLengthMeters - inner.Path!.TotalLengthMeters)} m; exit delta {F(e.ExitSpeedMetersPerSecond - inner.ExitSpeedMetersPerSecond)} m/s; objective convergence {B(s.Search.ObjectiveConvergence)}. A different ID alone is not evidence of a physical advantage or global optimum.");
        }
        L("10–12. Geometry extrema, numerical headroom, convergence, transition curvature and coherent perturbations are reported above. Near-zero lateral reserve or invalid inward probes limit robustness; smooth outward times alone do not certify an unconstrained physical optimum. Compare critical utilization and drive clipping at like progress, not just integrated averages.");
        L("13. One independent new parameter q; mass, capacity relation, engine envelope, progress availability and scrub are reused.");
        L("14. No q has empirical support. The existing 19 m/s reference has calibration context, which does not calibrate a combined-grip ellipse or its longitudinal engine proxy.");
        L("15. Algebraically small enough for a hidden manager mechanic, but this experiment alone cannot justify production promotion. Powered oversteer needs an effective surrogate; no real tyre/slip-angle claim. Negative or inconclusive trade-off evidence is an acceptable result.");
        return sb.ToString();
    }
}
