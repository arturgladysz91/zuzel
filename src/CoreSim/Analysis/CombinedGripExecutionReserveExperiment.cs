using System.Globalization;
using System.Text;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record CombinedGripReserveCase(float Coupling, float RequiredReserveMeters,
    FreeTrajectorySearchResult Search, FreeTrajectoryEvaluation Inner,
    FreeTrajectoryEvaluation Winner, FreeTrajectoryEvaluation? BestNonConstant,
    FreeTrajectoryEvaluation OriginalWinnerWithReserve);
public sealed record CombinedGripReserveResult(float LateralCapacityMetersPerSecondSquared,
    IReadOnlyList<CombinedGripReserveCase> Cases, CombinedGripReserveCase PerturbationCase,
    IReadOnlyList<CombinedGripPerturbation> Perturbations);

public static partial class FreeContinuousRacingTrajectoryGeometryExperiment
{
    public static CombinedGripReserveResult RunCombinedGripReserveExperiment(Action<string>? progress = null)
    {
        var baseline = DynamicCornerTrajectoryGeometryExperiment.RunUniform(new TrajectoryPlan(0, 0, 0));
        var geometry = CreateTrack().Geometry;
        var cases = new List<CombinedGripReserveCase>();
        foreach (var q in new[] { .5f, .75f })
        foreach (var reserve in new[] { 0f, .02f, .05f, .10f })
        {
            progress?.Invoke($"Search q={q.ToString("R", CultureInfo.InvariantCulture)}, reserve={reserve.ToString("R", CultureInfo.InvariantCulture)} m");
            var evaluator = new ExperimentalGeometryReplay(geometry, baseline.CornerEntrySpeedMetersPerSecond,
                lateralExecutionReserveMeters: reserve, combinedGripCoupling: q);
            var diagnostic = new ExperimentalGeometryReplay(geometry, baseline.CornerEntrySpeedMetersPerSecond,
                lateralExecutionReserveMeters: reserve, combinedGripCoupling: q, captureCombinedGripIntervals: true);
            var inner = diagnostic.Evaluate(Candidate("ConstantInner", new float[ControlStationCount]));
            var search = RunSearch(evaluator, geometry);
            var winner = diagnostic.Evaluate(search.TopTwenty[0].Candidate);
            var free = search.TopTwenty.Concat(search.FamilyResults.Select(x => x.Best))
                .Where(x => x.IsValid && x.IsNonConstant).OrderBy(x => x.SectorTimeSeconds)
                .ThenBy(x => x.Candidate.Id, StringComparer.Ordinal).FirstOrDefault();
            cases.Add(new(q, reserve, search, inner, winner,
                free is null ? null : diagnostic.Evaluate(free.Candidate), reserve == 0f ? winner
                    : diagnostic.Evaluate(cases.Single(x => x.Coupling == q && x.RequiredReserveMeters == 0f).Winner.Candidate)));
            progress?.Invoke($"Completed: {winner.Candidate.Id}, starts/refined={search.StartsGenerated}/{search.RefinedStartCount}");
        }
        var robust = cases.Where(x => x.RequiredReserveMeters > 0f && x.Winner.IsNonConstant
            && x.Winner.SectorTimeSeconds < x.Inner.SectorTimeSeconds)
            .OrderBy(x => x.Winner.SectorTimeSeconds - x.Inner.SectorTimeSeconds)
            .ThenByDescending(x => x.RequiredReserveMeters).ThenBy(x => x.Coupling).FirstOrDefault();
        var subject = robust ?? cases.Single(x => x.Coupling == .75f && x.RequiredReserveMeters == 0f);
        var probes = new List<CombinedGripPerturbation>();
        foreach (var kind in new[] { "Outward translation", "Smooth exit fan" })
        foreach (var shift in new[] { -.05f, .05f })
        {
            var controls = subject.Winner.Candidate.ControlOffsetsMeters.Select((x, i) =>
                x + shift * (kind == "Smooth exit fan" ? ExitFanWeight(i) : 1f)).ToArray();
            probes.Add(new(kind, shift, EvaluateCombinedGrip(controls, subject.Coupling,
                lateralExecutionReserveMeters: subject.RequiredReserveMeters)));
        }
        return new(CombinedGripAvailability.DeriveLateralAccelerationCapacity(UniformSurface, Balanced, Neutral),
            Array.AsReadOnly(cases.ToArray()), subject, Array.AsReadOnly(probes.ToArray()));
    }
}

public static class CombinedGripExecutionReserveReport
{
    public static string Render(CombinedGripReserveResult result)
    {
        var text = new StringBuilder();
        void L(string value = "") => text.Append(value).Append('\n');
        static string F(double value) => value.ToString("0.000000000", CultureInfo.InvariantCulture);
        static string B(bool value) => value ? "YES" : "NO";
        static bool Survives(CombinedGripReserveCase c) => c.Winner.IsNonConstant
            && c.Winner.SectorTimeSeconds < c.Inner.SectorTimeSeconds;
        L("# Combined-grip execution-reserve follow-up — analysis only"); L();
        L("Same Draft PR #50; reviewed starting HEAD `cea5ab20bb04796b81048e36a5d509771c355024`, main `58771bac635d7d4962e11aa3ae5ad37a2d0c8d94`. No production physics/calibration change. The q law, correction, envelope, spline, sampling, objective and search tolerances are frozen."); L();
        L("## Reserve semantics and limits"); L();
        L("This uses the existing per-replay-interval execution reserve exactly: remaining budget = max(0, lateralCapacity(stepTime) - reserve) - required lateral movement; invalid below -0.0001 m. Straight reposition uses the same rule. Actual headroom below is capacity minus movement BEFORE subtracting reserve. It is not the adjusted remaining budget or a tyre-grip margin."); L();
        L("The clamp preserves zero-motion feasibility when an interval capacity is smaller than reserve. Short binding-constraint intervals and final remainders make this test mesh-dependent. In particular, a valid ConstantInner can have actual minimum headroom below the requested reserve. Thus ‘survives N cm’ refers to this existing withheld-budget gate, not a guaranteed mesh-independent N cm physical margin. Do not infer that a return to inner disproves combined-grip physics. The search requests 106 starts / up to 48 valid diverse starts; fewer eligible starts are reported, without changing the search."); L();
        L("## Eight-case decision table"); L();
        L("Free-inner delta is winner sector minus inner sector (negative is faster); exit delta is winner minus inner. Winner is the best unrestricted candidate, including constant lines."); L();
        L("| q | required reserve m | winner | free-inner delta s | extra path m | exit delta m/s | actual min headroom m | interpretation |");
        L("|---:|---:|---|---:|---:|---:|---:|---|");
        foreach (var c in result.Cases)
        {
            var w = c.Winner; var i = c.Inner;
            var interpretation = Survives(c)
                ? c.RequiredReserveMeters == 0f ? "zero-reserve crossover" : $"survives {F(100 * c.RequiredReserveMeters)} cm budget gate"
                : "returns to ConstantInner; boundary-dependent";
            if (c.Search.SearchConvergenceUncertain) interpretation += "; search uncertain";
            L($"| {F(c.Coupling)} | {F(c.RequiredReserveMeters)} | {(w.IsNonConstant ? w.Candidate.Id : "ConstantInner / " + w.Candidate.Id)} | {F(w.SectorTimeSeconds-i.SectorTimeSeconds)} | {F(w.Path!.TotalLengthMeters-i.Path!.TotalLengthMeters)} | {F(w.ExitSpeedMetersPerSecond-i.ExitSpeedMetersPerSecond)} | {F(w.MinimumUnreservedLateralExecutionHeadroomMeters)} | {interpretation} |");
        }
        L(); L("## Timing, geometry and actual margins"); L();
        L("| q | reserve m | inner sector/corner/exit s,s,m/s | winner sector/corner/exit s,s,m/s | min R m | max curvature 1/m | actual / adjusted min headroom m | shortest replay step m | best nonconstant delta s | original zero-reserve winner validity |");
        L("|---:|---:|---|---|---:|---:|---|---:|---|---|");
        foreach (var c in result.Cases)
        {
            var w = c.Winner; var i = c.Inner;
            L($"| {F(c.Coupling)} | {F(c.RequiredReserveMeters)} | {F(i.SectorTimeSeconds)}/{F(i.CornerTimeSeconds)}/{F(i.ExitSpeedMetersPerSecond)} | {F(w.SectorTimeSeconds)}/{F(w.CornerTimeSeconds)}/{F(w.ExitSpeedMetersPerSecond)} | {F(w.MinimumRadiusMeters)} | {F(w.MaximumCurvaturePerMeter)} | {F(w.MinimumUnreservedLateralExecutionHeadroomMeters)}/{F(w.MinimumLateralExecutionHeadroomMeters)} | {F(w.CombinedGripIntervals.Min(x => x.DistanceMeters))} | {(c.BestNonConstant is { } n ? F(n.SectorTimeSeconds-i.SectorTimeSeconds) : "none found")} | {c.OriginalWinnerWithReserve.Validity} |");
        }
        L(); L("| q | reserve m | winner controls m |"); L("|---:|---:|---|");
        foreach (var c in result.Cases)
            L($"| {F(c.Coupling)} | {F(c.RequiredReserveMeters)} | `{string.Join(",", c.Winner.Candidate.ControlOffsetsMeters.Select(x => x.ToString("R", CultureInfo.InvariantCulture)))}` |");
        L(); L("### Is the small raw margin an almost exhausted execution budget?"); L();
        L("For the zero-reserve winners, show the interval with smallest raw headroom and the maximum used fraction across ALL corner intervals. Used fraction = required lateral movement / allowed movement. This is read-only diagnosis of the existing samples, not a new reserve gate or normalized physical parameter."); L();
        L("| q | min-headroom progress | step distance/time m/s | allowed/required/headroom m | used fraction at min headroom | maximum used fraction | max-use progress |");
        L("|---:|---:|---|---|---:|---:|---:|");
        foreach (var c in result.Cases.Where(x => x.RequiredReserveMeters == 0f))
        {
            var minimum = c.Winner.CombinedGripIntervals.MinBy(x => x.UnreservedLateralHeadroomMeters)!;
            var mostUsed = c.Winner.CombinedGripIntervals.MaxBy(x => x.RequiredLateralMovementMeters / x.MaximumLateralAllowedMeters)!;
            L($"| {F(c.Coupling)} | {F(minimum.MidpointProgress)} | {F(minimum.DistanceMeters)}/{F(minimum.TravelTimeSeconds)} | {F(minimum.MaximumLateralAllowedMeters)}/{F(minimum.RequiredLateralMovementMeters)}/{F(minimum.UnreservedLateralHeadroomMeters)} | {F(minimum.RequiredLateralMovementMeters/minimum.MaximumLateralAllowedMeters)} | {F(mostUsed.RequiredLateralMovementMeters/mostUsed.MaximumLateralAllowedMeters)} | {F(mostUsed.MidpointProgress)} |");
        }
        L(); L("## Search evidence"); L();
        L("| q | reserve m | starts/refined (initial valid) | objective | geometry | independent near starts | top-three closed | single/pair residual s | valid/evaluated | boundary/self intersection/non smooth/lateral/run wide/crash/reposition/non traversable |");
        L("|---:|---:|---|---|---|---:|---|---|---|---|");
        foreach (var c in result.Cases)
        {
            var s = c.Search;
            L($"| {F(c.Coupling)} | {F(c.RequiredReserveMeters)} | {s.StartsGenerated}/{s.RefinedStartCount} ({s.ValidInitialStarts}) | {B(s.ObjectiveConvergence)} | {B(s.GeometryConvergence)} | {s.WithinToleranceFamilyCount} | {B(s.TopThreeStable)} | {F(s.LocalPerturbationImprovementSeconds)}/{F(s.PairPerturbationImprovementSeconds)} | {s.ValidCandidates}/{s.CandidatesEvaluated} | {s.InvalidTrackBoundaryCandidates}/{s.InvalidSelfIntersectionCandidates}/{s.InvalidNonSmoothGeometryCandidates}/{s.InvalidLateralExecutionCandidates}/{s.InvalidCornerControlDepartureCandidates}/{s.InvalidCrashCandidates}/{s.InvalidStraightRepositionCandidates}/{s.InvalidNonTraversableCandidates} |");
        }
        L(); L("Top-three closure details (unchanged 0.002 s tolerance):"); L();
        L("| q | reserve m | family | closure moves/passes | single/expanded single/pair residual s | stable |");
        L("|---:|---:|---|---|---|---|");
        foreach (var c in result.Cases)
        foreach (var d in c.Search.TopThreeRefinementDiagnostics)
            L($"| {F(c.Coupling)} | {F(c.RequiredReserveMeters)} | {d.Family} | {d.AdditionalClosureMoves}/{d.ClosurePasses} | {F(d.ResidualSingleCoordinateImprovementSeconds)}/{F(d.ResidualExpandedSingleCoordinateImprovementSeconds)}/{F(d.ResidualAdjacentPairImprovementSeconds)} | {B(d.Stable)} |");
        L(); L("## Powered exit: like-progress winner / inner"); L();
        L("Shown for every faster nonconstant winner. Potential positive propulsion uses exact spline curvature and profile speed at the stated progress, independently of controller label. It is distinct from an actual negative scrub request; controller state is reported alongside it."); L();
        L("| q | reserve m | progress | speed m/s | curvature 1/m | u | factor | requested positive N | usable positive N | controller winner/inner |");
        L("|---:|---:|---:|---|---|---|---|---|---|---|");
        foreach (var c in result.Cases.Where(Survives))
        foreach (var p in new[] { .6f, .7f, .8f, .9f, 1f })
        {
            var w = At(c.Winner, p, c.Coupling, result.LateralCapacityMetersPerSecondSquared);
            var i = At(c.Inner, p, c.Coupling, result.LateralCapacityMetersPerSecondSquared);
            L($"| {F(c.Coupling)} | {F(c.RequiredReserveMeters)} | {F(p)} | {F(w.Speed)}/{F(i.Speed)} | {F(w.Curvature)}/{F(i.Curvature)} | {F(w.Grip.LateralUtilization)}/{F(i.Grip.LateralUtilization)} | {F(w.Grip.LongitudinalAvailabilityFactor)}/{F(i.Grip.LongitudinalAvailabilityFactor)} | {F(w.Grip.RequestedLongitudinalForceNewtons)}/{F(i.Grip.RequestedLongitudinalForceNewtons)} | {F(w.Grip.AvailableLongitudinalForceNewtons)}/{F(i.Grip.AvailableLongitudinalForceNewtons)} | {w.Controller}/{i.Controller} |");
        }
        L(); L("## Maximum-curvature correction diagnostic"); L();
        L("For each q choose the largest reserve retaining a faster nonconstant winner; otherwise show the 10 cm inner winner plus the zero-reserve free winner. Peak is the exact maximum among existing geometry samples. u/factor/request use the actual containing replay interval's force-evaluation state; its midpoint curvature is also shown, avoiding attribution of a controller label to a different force sample."); L();
        L("| q | reserve m | subject | peak progress / geometry k | interval progress / k | controller | u | factor | signed actual request / usable N |");
        L("|---:|---:|---|---|---|---|---:|---:|---|");
        foreach (var group in result.Cases.GroupBy(x => x.Coupling))
        {
            var chosen = group.Where(Survives).OrderByDescending(x => x.RequiredReserveMeters).First();
            var subjects = chosen.RequiredReserveMeters > 0f ? new[] { chosen } : new[] { group.Last(), chosen };
            foreach (var c in subjects)
            {
                var peak = c.Winner.Path!.Samples.MaxBy(x => x.CurvaturePerMeter)!;
                var interval = c.Winner.CombinedGripIntervals.First(x => x.EndProgress >= peak.Progress);
                L($"| {F(c.Coupling)} | {F(c.RequiredReserveMeters)} | {(c.Winner.IsNonConstant ? "free winner" : "ConstantInner")} | {F(peak.Progress)}/{F(peak.CurvaturePerMeter)} | {F(interval.MidpointProgress)}/{F(interval.CurvaturePerMeter)} | {interval.ControllerState} | {F(interval.Grip.LateralUtilization)} | {F(interval.Grip.LongitudinalAvailabilityFactor)} | {F(interval.Grip.RequestedLongitudinalForceNewtons)}/{F(interval.Grip.AvailableLongitudinalForceNewtons)} |");
            }
        }
        L(); L("No controller-label discontinuity or capacity disappearance was detected. However, effective correction/scrub remains outside the positive-propulsion combined-force allocation, so a physically meaningful correction-phase exploitation cannot yet be excluded. ‘Tighter while scrubbing → opens before powered exit’ may be valid speedway behaviour; it is not proven physically sound by this allocation model. No braking traction ellipse or scrub force decomposition is added."); L();
        foreach (var c in result.Cases)
        {
            var check = CombinedGripDiagnostics.Check(c.Winner, c.Coupling, result.LateralCapacityMetersPerSecondSquared);
            L($"Capacity check q={F(c.Coupling)}, reserve={F(c.RequiredReserveMeters)}: {check.Intervals} intervals, missing {check.MissingCapacityIntervals}, maximum/transition residual {F(check.MaximumCapacityResidualNewtons)}/{F(check.MaximumTransitionCapacityResidualNewtons)} N.");
        }
        L(); L("The zero-reserve free winners peak during Correction/carry with zero positive capacity and an unchanged negative scrub request. Their geometry therefore still uses tighter while scrubbing, then opens before powered exit. Positive-reserve inner winners have essentially uniform curvature; the reported sample peak there is float rounding, not a physical apex."); L();
        L("## Four small perturbations"); L();
        var subject = result.PerturbationCase;
        L($"Subject q={F(subject.Coupling)}, reserve={F(subject.RequiredReserveMeters)}, {subject.Winner.Candidate.Id}. {(subject.RequiredReserveMeters > 0f ? "Surviving positive-reserve winner." : "No faster positive-reserve nonconstant winner: zero-reserve q=.75 fallback, not a robust winner.")} No optimization or clipping of controls."); L();
        L("| probe | shift m | validity | sector delta s | actual min headroom m |");
        L("|---|---:|---|---:|---:|");
        foreach (var probe in result.Perturbations)
            L($"| {probe.Kind} | {F(probe.ShiftMeters)} | {probe.Evaluation.Validity} | {(probe.Evaluation.IsValid ? F(probe.Evaluation.SectorTimeSeconds-subject.Winner.SectorTimeSeconds) : "—")} | {(probe.Evaluation.IsValid ? F(probe.Evaluation.MinimumUnreservedLateralExecutionHeadroomMeters) : "—")} |");
        L(); L("## Required answers"); L();
        foreach (var reserve in new[] { .02f, .05f, .10f })
        {
            var c = result.Cases.Single(x => x.Coupling == .5f && x.RequiredReserveMeters == reserve);
            var number = reserve == .02f ? 1 : reserve == .05f ? 2 : 3;
            L($"{number}. q=.5 crossover at {F(reserve*100)} cm existing budget reserve: {B(Survives(c))}. {(c.Search.SearchConvergenceUncertain ? "Only five starts are eligible; objective convergence is uncertain." : "Search meets unchanged convergence criteria.")} This is not a certificate of the same actual physical margin.");
        }
        L($"4. q=.75 crossover at 2/5/10 cm existing budget reserve: {string.Join("; ", result.Cases.Where(x => x.Coupling == .75f && x.RequiredReserveMeters > 0f).Select(c => $"{F(c.RequiredReserveMeters*100)} cm: {B(Survives(c))}"))}. Positive-reserve searches have only five eligible starts and uncertain objective convergence.");
        var any = result.Cases.Any(x => x.RequiredReserveMeters > 0f && Survives(x));
        L(any ? "5. Some positive-reserve signal survives the existing gate; examine powered-exit tables and convergence before attributing it to physics. Actual raw margins determine whether centimetres of real reserve were achieved."
            : "5. Current non-constant optimum depends materially on the lateral-execution boundary under the existing reserve gate. The longer-path/lower-powered-utilization/more-propulsion/higher-exit chain is visible at zero reserve, but no surviving positive-reserve free optimum demonstrates it away from that boundary.");
        L("6. Effect size/continuity: zero-reserve gains are approximately 9.53/22.16 ms; the valid +/-5 cm probes change time by at most about 3.3 ms. These are finite, locally small changes, but do not establish a robust positive-reserve effect or smoothness across an invalid boundary. Inward translation is invalid.");
        L("7. Structural promise: the simple positive-propulsion coupling remains a plausible hypothesis, but this follow-up does not establish a physically robust line optimum. Mesh-dependent reserve semantics limit that inference.");
        L("8. No empirical basis selects q=.5, .75 or any other q. These are structural probes, not calibrated values.");
        L("9. Correction remains an unresolved interpretation limit: effective negative scrub is outside the shared positive-propulsion allocation. A physical correction-phase exploit is neither proven nor excluded.");
        L("10. Recommendation: further analysis of this same subsystem, first with an explicitly specified mesh-independent execution-margin definition and correction data/interpretation; do not promote the current nonconstant optimum to production or reject the propulsion law solely from this mesh-dependent test. Keep Draft PR #50 for independent review.");
        return text.ToString();
    }

    private static (float Speed, float Curvature, CombinedGripState Grip, string Controller) At(
        FreeTrajectoryEvaluation e, float progress, float q, float capacity)
    {
        var speed = e.Profile.MinBy(x => MathF.Abs(x.Progress-progress))!.SpeedMetersPerSecond;
        var curvature = e.Path!.PointAt(progress).CurvaturePerMeter;
        var engine = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(
            LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(RiderSkills.Balanced,
                BikeSetup.Neutral, CalibrationScenarioCatalog.Baseline.Surface), speed, BikeSetup.Neutral);
        var grip = CombinedGripAvailability.Evaluate(speed, curvature, capacity, engine,
            ContinuousCornerEnvelope.DriveAvailability(progress)*engine, q);
        return (speed, curvature, grip, e.CombinedGripIntervals.First(x => x.EndProgress >= progress).ControllerState);
    }
}
