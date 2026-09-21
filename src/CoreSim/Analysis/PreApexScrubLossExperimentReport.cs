using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public static class PreApexScrubLossExperimentReport
{
    public static string Render(PreApexScrubLossExperimentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var output = new StringBuilder();
        void Line(string value = "") => output.Append(value).Append('\n');
        var s0 = Primary("S0");
        var s100 = Primary("S100");

        Line("# Pre-apex scrub-loss diagnostic experiment");
        Line();
        Line("## A. Hypothesis");
        Line();
        Line("Test whether a corner-specific dissipative loss confined to the pre-apex window can lower true-apex speed and lengthen the flying lap while preserving normal production recovery after the apex.");
        Line();
        Line("This is `DiagnosticPreApexScrubLoss`, not a production tyre model and not a fitted real-world coefficient.");
        Line();
        Line("## B. Evidence from #41 and #42");
        Line();
        Line("#41 classified the straight-only intervention as `StraightEnvelopeShapeInsufficient`: more drive raised Vmax and shortened an already-fast modeled lap. #42 classified ordinary reduced-drive resistance exposure as `LossLocationMismatch`: R100 added 0.246408 s to FlyingMedian but left true-apex speed effectively unchanged, moved the minimum to `p=0.646983`, and imposed a 0.583672 m/s exit penalty.");
        Line();
        Line("## C. Why #42 was location-mismatched");
        Line();
        Line("The #42 force acts primarily where drive availability is already rising after the apex. Its direct time delta was therefore post-apex dominated. #43 leaves that production R0/default force semantics untouched and isolates a separate loss before canonical apex `p=0.50`.");
        Line();
        Line("## D. Scrub-loss diagnostic model");
        Line();
        Line("For peak parameter `A`, `a_scrub(p)=A*W(p)` and `F_scrub=142*a_scrub`. The force is non-negative in magnitude and opposes motion. On scrub-owned distance the existing deterministic midpoint integration uses `a_total=a_production_signed_drive-a_scrub`; when production availability is zero, `a_total=-a_scrub`.");
        Line();
        Line("Explicit correction keeps first claim on each physical step. Scrub can consume only the unconsumed remainder; no metre is integrated twice.");
        Line();
        Line("## E. Scrub progress window");
        Line();
        Line("`W=0` through `p=0.10`; canonical smoothstep rise on `(0.10,0.30)`; `W=1` on `[0.30,0.40]`; smoothstep fall on `(0.40,0.50)`; and `W=0` at and after `p=0.50`. The window is continuous with zero derivative at its knots.");
        Line();
        Line("| p | W(p) |");
        Line("|---:|---:|");
        foreach (var p in new[] { 0f, .10f, .20f, .30f, .40f, .50f, .625f, .75f, .875f, 1f })
            Line($"| {N(p)} | {N(PreApexScrubLossExperiment.ScrubWindow(p))} |");
        Line();
        Line("## F. Candidate definitions");
        Line();
        Line("| Candidate | Peak scrub deceleration (m/s^2) |");
        Line("|---|---:|");
        foreach (var candidate in result.Candidates)
            Line($"| {candidate.Id} | {N(candidate.PeakScrubDecelerationMetersPerSecondSquared)} |");
        Line();
        Line("All candidates explicitly keep `StraightDriveEnvelopeAdjustment=null` and `CornerReducedDriveResistanceAdjustment=null`; no candidate is selected for production.");
        Line();
        Line("## G. Production equality S0");
        Line();
        Line($"Production default exact: **{Yes(result.Freeze.ProductionDefaultExact)}**. S0 complete trace exact: **{Yes(result.Freeze.S0ExactProduction)}**. Zero-force experimental path exact: **{Yes(result.Freeze.ZeroForceExperimentalPathExact)}**. Full production scenario suite exact: **{Yes(result.Freeze.FullProductionScenarioSuiteExact)}**.");
        Line();
        Line("### Numerical partition control");
        Line();
        Line("Scrub-window knots do not split the canonical production step before correction evaluation. Production first chooses the unchanged one-metre/apex-bounded step, endpoint target, and correction result; only its physical remainder can receive midpoint-sampled scrub. `W(p)` is mathematical window context, while `Scrub applied` means force was actually integrated over nonzero distance.");
        Line();
        Line("| Candidate | Corner | Last raw p<=.10 | Speed | First raw W>0 p | Speed | First scrub p | First scrub m | Correction before scrub m |");
        Line("|---|---:|---:|---:|---:|---:|---|---|---:|");
        foreach (var item in result.Primary)
        foreach (var control in item.PartitionControl)
            Line($"| {item.CandidateId} | {control.CornerNumber} | {N(control.LastRawNodeAtOrBeforeWindowStartProgress)} | {N(control.LastRawNodeAtOrBeforeWindowStartSpeedMetersPerSecond)} | {N(control.FirstRawNodeWithPositiveWindowProgress)} | {N(control.FirstRawNodeWithPositiveWindowSpeedMetersPerSecond)} | {Maybe(control.FirstActualScrubProgress)} | {Maybe(control.FirstActualScrubDistanceMeters)} | {N(control.CorrectionDistanceBeforeFirstScrubMeters)} |");
        Line();
        Line("## H. Motoarena L1 primary");
        Line();
        Line("Fixture: Motoarena 2026, width 16.6 m, 31/31 m start split, Dry baseline surface, zero incidents, neutral setup, all skills 50, HoldLane, seed 390039, controlled-distance L1.");
        Line();
        Line("| Candidate | Vmax km/h | FlyingMedian s | FlyingL2 s | Heat s | Avg m/s | StraightPeak | Entry | TrueApex | Minimum | Min p | Exit | Peak->Apex | Peak->Min | Entry->Apex dv | Apex->Exit dv | B/RW/C |");
        Line("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var item in result.Primary)
            Line($"| {item.CandidateId} | {N(item.VmaxKilometersPerHour)} | {N(item.FlyingLapMedianSeconds)} | {N(item.TimeBudget.FlyingLapTimeSeconds)} | {N(item.HeatTimeSeconds)} | {N(item.AverageSpeedMetersPerSecond)} | {N(item.StraightPeakSpeedMetersPerSecond)} | {N(item.CornerEntrySpeedMetersPerSecond)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond)} | {N(item.MinimumSpeedCornerProgress)} | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.PeakToTrueApexAmplitudeMetersPerSecond)} | {N(item.PeakToMinimumAmplitudeMetersPerSecond)} | {N(item.EntryToTrueApexDeltaSpeedMetersPerSecond)} | {N(item.TrueApexToExitDeltaSpeedMetersPerSecond)} | {item.BrakeCount}/{item.RunWideCount}/{item.CrashCount} |");
        Line();
        Line("## I. True apex vs minimum");
        Line();
        Line("Canonical apex means only `p = 0.50`; the minimum is the lowest actual node anywhere in a logical corner and is not relabeled as apex.");
        Line();
        Line("| Candidate | TrueApex m/s | Minimum m/s | Minimum p | Corner |");
        Line("|---|---:|---:|---:|---:|");
        foreach (var item in result.Primary)
            Line($"| {item.CandidateId} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond)} | {N(item.MinimumSpeedCornerProgress)} | {item.MinimumSpeedCornerNumber} |");
        Line();
        Line("## J. Flying-lap time budget");
        Line();
        Line("`Flying L2` is one modeled lap and is intentionally distinct from the median of completed flying laps.");
        Line();
        Line("| Candidate | Straight total | C1 pre | C1 post | C1 total | C2 pre | C2 post | C2 total | Corner total | FlyingL2 | Straight+Corner conserved |");
        Line("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var item in result.Primary)
        {
            var b = item.TimeBudget;
            Line($"| {item.CandidateId} | {N(b.TotalStraightTimeSeconds)} | {N(b.FirstCornerEntryToApexSeconds)} | {N(b.FirstCornerApexToExitSeconds)} | {N(b.FirstCornerTotalSeconds)} | {N(b.SecondCornerEntryToApexSeconds)} | {N(b.SecondCornerApexToExitSeconds)} | {N(b.SecondCornerTotalSeconds)} | {N(b.TotalCornerTimeSeconds)} | {N(b.FlyingLapTimeSeconds)} | {Yes(Close(b.TotalStraightTimeSeconds + b.TotalCornerTimeSeconds, b.FlyingLapTimeSeconds))} |");
        }
        Line();
        Line("## K. Pre vs post apex deltas");
        Line();
        Line("| Candidate | dFlyingMedian | dFlyingL2 | dPreApex | dPostApex | dStraight | dTrueApex | dMinimum | Min p | dExit | dVmax | Scrub work J |");
        Line("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var item in result.Primary)
            Line($"| {item.CandidateId} | {N(item.FlyingLapMedianSeconds-s0.FlyingLapMedianSeconds)} | {N(item.TimeBudget.FlyingLapTimeSeconds-s0.TimeBudget.FlyingLapTimeSeconds)} | {N(item.TimeBudget.TotalPreApexTimeSeconds-s0.TimeBudget.TotalPreApexTimeSeconds)} | {N(item.TimeBudget.TotalPostApexTimeSeconds-s0.TimeBudget.TotalPostApexTimeSeconds)} | {N(item.TimeBudget.TotalStraightTimeSeconds-s0.TimeBudget.TotalStraightTimeSeconds)} | {N(item.TrueApexSpeedMetersPerSecond-s0.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond-s0.MinimumSpeedMetersPerSecond)} | {N(item.MinimumSpeedCornerProgress)} | {N(item.CornerExitSpeedMetersPerSecond-s0.CornerExitSpeedMetersPerSecond)} | {N(item.VmaxKilometersPerHour-s0.VmaxKilometersPerHour)} | {N(item.Energy.ScrubWorkJoules)} |");
        Line();
        Line("## L. Profile grid");
        Line();
        Line("| Candidate | Corner | p | Actual m/s | Envelope m/s | Drive availability | Scrub window | Scrub applied | Phase |");
        Line("|---|---:|---:|---:|---:|---:|---:|---|---|");
        foreach (var point in result.Primary.SelectMany(item => item.ProfilePoints))
            Line($"| {point.CandidateId} | {point.CornerNumber} | {N(point.CornerProgress)} | {N(point.ActualSpeedMetersPerSecond)} | {N(point.EnvelopeSpeedMetersPerSecond)} | {N(point.DriveAvailability)} | {N(point.ScrubWindow)} | {Yes(point.ScrubApplied)} | {point.PhaseClassification} |");
        Line();
        Line("## M. Force decomposition");
        Line();
        Line("| Candidate | Corner | p | Scrub applied | Scrub a m/s^2 | Scrub force N | Production signed a m/s^2 | Final a m/s^2 |");
        Line("|---|---:|---:|---|---:|---:|---:|---:|");
        foreach (var point in result.Primary.SelectMany(item => item.ProfilePoints))
            Line($"| {point.CandidateId} | {point.CornerNumber} | {N(point.CornerProgress)} | {Yes(point.ScrubApplied)} | {N(point.ScrubAccelerationMetersPerSecondSquared)} | {N(point.ScrubForceNewtons)} | {N(point.ProductionSignedDriveAccelerationMetersPerSecondSquared)} | {N(point.FinalAccelerationMetersPerSecondSquared)} |");
        Line();
        Line("## N. Distance buckets");
        Line();
        Line("Buckets `Correction + Scrub + PositiveDrive + NegativeSignedDrive + NeutralCarry` are exclusive. The two scrub-by-drive columns are sub-observations only.");
        Line();
        Line("| Candidate | Travelled | Correction | Scrub | Positive | Negative | Neutral | Scrub drive=0 | Scrub drive>0 | Conserved | Correction p | Scrub p |");
        Line("|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|---|");
        foreach (var item in result.Primary)
        {
            var d = item.DistanceBuckets;
            Line($"| {item.CandidateId} | {N(d.TravelledDistanceMeters)} | {N(d.CorrectionDistanceMeters)} | {N(d.ScrubDistanceMeters)} | {N(d.PositiveDriveDistanceMeters)} | {N(d.NegativeSignedDriveDistanceMeters)} | {N(d.NeutralCarryDistanceMeters)} | {N(d.ScrubWhileDriveZeroDistanceMeters)} | {N(d.ScrubWhileDrivePositiveDistanceMeters)} | {Yes(d.Conserved)} | {Range(item.FirstCorrectionProgress,item.LastCorrectionProgress)} | {Range(item.FirstScrubProgress,item.LastScrubProgress)} |");
        }
        Line();
        Line("## O. Scrub work / kinetic energy");
        Line();
        Line("Diagnostics only; this is not a second energy simulator.");
        Line();
        Line("| Candidate | Scrub work J | Entry KE J | True-apex KE J | Minimum KE J | Exit KE J |");
        Line("|---|---:|---:|---:|---:|---:|");
        foreach (var item in result.Primary)
            Line($"| {item.CandidateId} | {N(item.Energy.ScrubWorkJoules)} | {N(item.Energy.EntryKineticEnergyJoules)} | {N(item.Energy.TrueApexKineticEnergyJoules)} | {N(item.Energy.MinimumKineticEnergyJoules)} | {N(item.Energy.ExitKineticEnergyJoules)} |");
        Line();
        Line("## P. Isolated corner");
        Line();
        Line($"Fixed entry `{N(PreApexScrubLossExperiment.IsolatedEntrySpeedMetersPerSecond)} m/s`, L1, skills 50, neutral setup, baseline surface, production Motoarena geometry; envelope is exact across candidates: **{Yes(result.Freeze.EnvelopeExact)}**.");
        Line();
        Line("| Candidate | Entry | TrueApex | Minimum | Min p | Exit | Pre s | Post s | Correction m | Scrub m | Work J | Conserved |");
        Line("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var item in result.Isolated)
            Line($"| {item.CandidateId} | {N(item.EntrySpeedMetersPerSecond)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond)} | {N(item.MinimumSpeedCornerProgress)} | {N(item.ExitSpeedMetersPerSecond)} | {N(item.EntryToApexTimeSeconds)} | {N(item.ApexToExitTimeSeconds)} | {N(item.DistanceBuckets.CorrectionDistanceMeters)} | {N(item.DistanceBuckets.ScrubDistanceMeters)} | {N(item.Energy.ScrubWorkJoules)} | {Yes(item.DistanceBuckets.Conserved)} |");
        Line();
        Line("## Q. Distance-matched proxy");
        Line();
        HeatTable(result.DistanceMatched, includeWork: false);
        Line("The fractional LateralPosition 1.11065 is a modeled-distance proxy near 1377 m, not a real racing line.");
        Line();
        Line("## R. Line sweep");
        Line();
        HeatTable(result.LineSweep, includeWork: true);
        Line();
        Line("## S. Speed sweep");
        Line();
        HeatTable(result.SpeedSweep, includeWork: false);
        Line();
        Line("## T. SlideControl sweep");
        Line();
        Line("| Candidate | SlideControl | Correction capability m/s^2 | Correction m | Scrub m | TrueApex | Minimum/p | Exit | FlyingMedian |");
        Line("|---|---:|---:|---:|---:|---:|---|---:|---:|");
        foreach (var item in result.SlideControlSweep)
        {
            var capability = 2f + 1.2f * item.SkillSlideControl / 100f;
            Line($"| {item.CandidateId} | {N(item.SkillSlideControl)} | {N(capability)} | {N(item.DistanceBuckets.CorrectionDistanceMeters)} | {N(item.DistanceBuckets.ScrubDistanceMeters)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond)}/{N(item.MinimumSpeedCornerProgress)} | {N(item.CornerExitSpeedMetersPerSecond)} | {N(item.FlyingLapMedianSeconds)} |");
        }
        Line();
        Line("## U. Surface sensitivity");
        Line();
        Line("Peak scrub is not scaled by surface; the raw interaction with the existing surface model is observed.");
        Line();
        Line("| Candidate | Surface | Vmax | FlyingMedian | Entry | TrueApex | Minimum/p | Exit | Scrub work J | B/RW/C |");
        Line("|---|---|---:|---:|---:|---:|---|---:|---:|---:|");
        foreach (var item in result.SurfaceSensitivity)
        {
            var o = item.Observation;
            Line($"| {item.CandidateId} | {item.SurfaceId} | {N(o.VmaxKilometersPerHour)} | {N(o.FlyingLapMedianSeconds)} | {N(o.CornerEntrySpeedMetersPerSecond)} | {N(o.TrueApexSpeedMetersPerSecond)} | {N(o.MinimumSpeedMetersPerSecond)}/{N(o.MinimumSpeedCornerProgress)} | {N(o.CornerExitSpeedMetersPerSecond)} | {N(o.Energy.ScrubWorkJoules)} | {o.BrakeCount}/{o.RunWideCount}/{o.CrashCount} |");
        }
        Line();
        Line("## V. Extreme");
        Line();
        HeatTable(result.Extreme, includeWork: true);
        Line($"Finite and positive in both extreme runs: **{Yes(result.Extreme.All(FinitePositive))}**.");
        Line();
        Line("## W. Frozen systems");
        Line();
        Line($"StandingStart exact: **{Yes(result.Freeze.StandingStartExact)}** (`Reaction={N(result.Freeze.ReactionTimeSeconds)} s`, `TimeTo70={N(result.Freeze.TimeTo70KphSeconds)} s`, `SpeedAt2s={N(result.Freeze.SpeedAtTwoSecondsKilometersPerHour)} km/h`).");
        Line($"Isolated straight exact: **{Yes(result.Freeze.StraightExact)}**. #42 adjustment absent: **{Yes(result.Freeze.CornerResistanceExperimentAbsent)}**. Legacy exact: **{Yes(result.Freeze.LegacyExact)}**. SegmentPhysics thresholds exact: **{Yes(result.Freeze.SegmentPhysicsThresholdsExact)}**. Correction capability/target exact: **{Yes(result.Freeze.CorrectionCapabilityExact && result.Freeze.CorrectionTargetExact)}**.");
        Line($"All {PreApexScrubLossExperiment.RiderPermutationCount} rider-order permutations are trace-invariant for S0: **{Yes(result.PermutationDistinctTraceHashes["S0"] == 1)}** and S100: **{Yes(result.PermutationDistinctTraceHashes["S100"] == 1)}**.");
        Line();
        Line("## X. Pareto interpretation");
        Line();
        foreach (var item in result.Primary)
            Line($"- `{item.CandidateId}`: dFlyingMedian {Signed(item.FlyingLapMedianSeconds-s0.FlyingLapMedianSeconds)} s; dTrueApex {Signed(item.TrueApexSpeedMetersPerSecond-s0.TrueApexSpeedMetersPerSecond)} m/s; dExit {Signed(item.CornerExitSpeedMetersPerSecond-s0.CornerExitSpeedMetersPerSecond)} m/s; minimum at p={N(item.MinimumSpeedCornerProgress)}; scrub work {N(item.Energy.ScrubWorkJoules)} J.");
        Line();
        Line("No weighted score and no PGE P50 fit are used.");
        Line();
        Line("## Y. Next-model decision");
        Line();
        var preDominant = s100.TimeBudget.TotalPreApexTimeSeconds-s0.TimeBudget.TotalPreApexTimeSeconds
            > s100.TimeBudget.TotalPostApexTimeSeconds-s0.TimeBudget.TotalPostApexTimeSeconds;
        var nearApex = Math.Abs(s100.MinimumSpeedCornerProgress-.5d) <= .10d;
        var smallerExit = s0.CornerExitSpeedMetersPerSecond-s100.CornerExitSpeedMetersPerSecond
            < PreApexScrubLossExperiment.CornerResistanceR100ExitPenaltyMetersPerSecond;
        Line($"Classification: `{result.Classification}`. Direct corner time effect mainly pre-apex: **{Yes(preDominant)}**. Minimum remains near apex: **{Yes(nearApex)}**. Exit penalty is smaller than #42 R100: **{Yes(smallerExit)}**.");
        Line($"Directionally better than ordinary resistance exposure: **{Yes(result.Classification == "PreApexScrubLossPlausiblyRelevant")}**; under conservative distance ownership the proposed force has too little primary-fixture distance to establish the desired mechanism.");
        Line();
        Line($"Recommended single subsystem for #44: **{result.NextSubsystem}**. No #43 candidate is promoted to production.");
        Line();
        Line("## Z. Limitations");
        Line();
        Line("- No real corner-speed or apex-speed trace.");
        Line("- No slip angle, throttle trace, wheelspin, tyre-force model, or lean/banking physics.");
        Line("- The scrub window is a provisional diagnostic shape and scrub strength is not a real-world coefficient.");
        Line("- Motoarena geometry remains a symmetric approximation; L1 is not a real trajectory.");
        Line("- The experiment identifies direction and location only; it does not calibrate Skill50 to PGE P50.");

        return output.ToString();

        PreApexScrubHeatObservation Primary(string id) =>
            result.Primary.Single(item => item.CandidateId == id);

        void HeatTable(IEnumerable<PreApexScrubHeatObservation> observations, bool includeWork)
        {
            Line(includeWork
                ? "| Candidate | Scenario | Distance m | Vmax | Flying | Entry | TrueApex | Minimum/p | Exit | Scrub work J | B/RW/C |"
                : "| Candidate | Scenario | Distance m | Vmax | Flying | Entry | TrueApex | Minimum/p | Exit | B/RW/C |");
            Line(includeWork
                ? "|---|---|---:|---:|---:|---:|---:|---|---:|---:|---:|"
                : "|---|---|---:|---:|---:|---:|---:|---|---:|---:|");
            foreach (var item in observations)
            {
                var prefix = $"| {item.CandidateId} | {item.ScenarioId} | {N(item.ModeledFourLapDistanceMeters)} | {N(item.VmaxKilometersPerHour)} | {N(item.FlyingLapMedianSeconds)} | {N(item.CornerEntrySpeedMetersPerSecond)} | {N(item.TrueApexSpeedMetersPerSecond)} | {N(item.MinimumSpeedMetersPerSecond)}/{N(item.MinimumSpeedCornerProgress)} | {N(item.CornerExitSpeedMetersPerSecond)} |";
                Line(includeWork
                    ? $"{prefix} {N(item.Energy.ScrubWorkJoules)} | {item.BrakeCount}/{item.RunWideCount}/{item.CrashCount} |"
                    : $"{prefix} {item.BrakeCount}/{item.RunWideCount}/{item.CrashCount} |");
            }
        }
    }

    private static string N(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
    private static string Maybe(float? value) => value.HasValue ? N(value.Value) : "none";
    private static string Yes(bool value) => value ? "YES" : "NO";
    private static string Signed(double value) => value.ToString("+0.000000;-0.000000;0.000000", CultureInfo.InvariantCulture);
    private static bool Close(double left, double right) => Math.Abs(left-right) <= Math.Max(1e-6d, Math.Abs(right)*1e-6d);
    private static string Range(float? first, float? last) => first.HasValue && last.HasValue
        ? $"{N(first.Value)}..{N(last.Value)}" : "none";
    private static bool FinitePositive(PreApexScrubHeatObservation item) =>
        double.IsFinite(item.VmaxKilometersPerHour)
        && double.IsFinite(item.FlyingLapMedianSeconds)
        && double.IsFinite(item.TrueApexSpeedMetersPerSecond)
        && double.IsFinite(item.MinimumSpeedMetersPerSecond)
        && double.IsFinite(item.CornerExitSpeedMetersPerSecond)
        && item.MinimumSpeedMetersPerSecond > 0d
        && item.CornerExitSpeedMetersPerSecond > 0d;
}
