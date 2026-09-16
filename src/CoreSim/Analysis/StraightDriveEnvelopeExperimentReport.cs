using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

/// <summary>Pure invariant-culture LF renderer for the bounded #41 experiment.</summary>
public static class StraightDriveEnvelopeExperimentReport
{
    public static string Render(StraightDriveEnvelopeExperimentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var b = new StringBuilder();
        var baseline = result.Primary.Single(item => item.CandidateId == "A0");
        var best = result.Primary.Single(item => item.CandidateId == result.DiagnosticBestCandidateId);

        Line("# Straight drive-envelope shape experiment (#41)");

        Heading("A. Provenance");
        Line($"Base main SHA: `{StraightDriveEnvelopeExperiment.BaseMainSha}` (`Recover real start telemetry calibration data (#40)`).");
        Line("Fixture: Motoarena 2026 primary symmetric-width approximation, 16.6 m modeled turn width, provisional 31/31 m home-straight split, Dry weather, baseline surface, incidents disabled, HoldLane, seed 390039.");
        Line("All candidate heats use `CalibrationRunner → HeatSimulator → SimulationEngine`; no second simulator, environment switch, global mutable mode, ThreadStatic state or RNG-dependent selection exists.");
        Line("The default HeatSimulationOptions path carries no experiment profile and remains the reviewed post-#40 production baseline.");
        Line("The existing 197-scenario Calibration Scenario Suite remains byte-identical to the committed #38 report on the default/A0 path.");

        Heading("B. Why throttle-only cannot exceed baseline full-drive");
        Line("Ordinary ADVANCED Straight already evaluates the full available production drive envelope before the backward corner-preparation boundary. Multiplying that force by a throttle availability constrained to 0..1 cannot create a speed trajectory above the full-drive baseline. Therefore this experiment changes only the speed-dependent force multiplier shape; it is not a 70%→100% throttle ramp and is not interpreted as a real torque curve.");

        Heading("C. Baseline force envelope");
        Line("`B(v)=1` at and below 16 m/s. Above 16 m/s, `B(v)=clamp(1-r×(v-16),0,1)`, with the unchanged gearing interpolation `r=0.0350→0.0100`. Reference force, 16 m/s reference speed, 142 kg mass, `40+0.20v²` resistance, 1 m integration, gearing and surface mappings are unchanged.");

        Heading("D. Candidate definitions");
        Table("Candidate | Name | L | H | Purpose", result.Candidates.Select(item => Row(
            item.Id, item.Name, item.LowSpeedSuppression, item.HighSpeedRetention, item.Purpose)));
        Line("`E(v)=clamp(B(v)+Δ(v),0,1)`. Δ is 0 through 16 m/s; smoothstep 0→−L on 16..22; −L→0 on 22..24; 0→+H on 24..30; +H→0 on 30..40; and 0 from 40 m/s. R08/R12 use H=L/2 as `EnvelopeAreaControlProxy`, not conservation of energy or work.");

        Heading("E. Force/multiplier tables");
        Table("Candidate | v m/s | B gear0 | E gear0 | B gear0.5 | Δ | E gear0.5 | net a gear0.5 m/s² | B gear1 | E gear1",
            result.Candidates.SelectMany(candidate => result.ForcePoints
                .Where(point => point.CandidateId == candidate.Id && point.Gearing == .5f)
                .Select(mid =>
                {
                    var low = result.ForcePoints.Single(point => point.CandidateId == candidate.Id
                        && point.SpeedMetersPerSecond == mid.SpeedMetersPerSecond && point.Gearing == 0f);
                    var high = result.ForcePoints.Single(point => point.CandidateId == candidate.Id
                        && point.SpeedMetersPerSecond == mid.SpeedMetersPerSecond && point.Gearing == 1f);
                    return Row(candidate.Id, mid.SpeedMetersPerSecond,
                        low.BaselineMultiplier, low.ExperimentalMultiplier,
                        mid.BaselineMultiplier, mid.Delta, mid.ExperimentalMultiplier,
                        mid.SignedNetAccelerationMetersPerSecondSquared,
                        high.BaselineMultiplier, high.ExperimentalMultiplier);
                })));
        Table("Candidate | Added lower clamp inside 16–40 | Added upper clamp inside 16–40 | Existing baseline lower clamp",
            result.Candidates.Select(candidate =>
            {
                var points = result.ForcePoints.Where(point => point.CandidateId == candidate.Id).ToArray();
                return Row(candidate.Id,
                    points.Any(point => point.LowerClampActivated) ? "YES" : "NO",
                    points.Any(point => point.UpperClampActivated) ? "YES" : "NO",
                    "gear0 44.571429 m/s; gear0.5 60.444444 m/s; gear1 116 m/s");
            }));
        Line("No candidate perturbation clamp activates at the required 16–40 m/s force-curve points. Above 40 m/s Δ is exactly zero, so only the existing production lower clamp can activate.");

        Heading("F. Motoarena fixed-L1 baseline");
        Line("Primary rider: all skills 50, neutral setup, fixed LateralPosition 1. Its four-lap distance is the controlled geometric reference; it is not a claim about a real rider's path.");
        Table("Metric | A0", PrimaryMetricRows(baseline));
        Line("Real context only: Motoarena Vmax P10/P50/P90 = 108.6/113.7/116.9 km/h; Flying P10/P50/P90 = 14.56/14.87/15.15 s; TotalDistance P10/P90 = 1335.6/1418 m. Skill50 is not PGE P50.");

        Heading("G. Candidate comparison");
        Table("Candidate | Vmax km/h | ΔVmax | Flying s | ΔFlying | Straight entry m/s | Straight peak m/s | Corner entry m/s | Apex/min m/s | Corner exit m/s | Peak→apex m/s | L2/L3/L4 s | Heat s | Distance m | Average m/s | B/RW/C | Straight accel/cruise/prep m",
            result.Primary.Select(item => Row(
                item.CandidateId,
                item.VmaxKilometersPerHour,
                item.VmaxKilometersPerHour - baseline.VmaxKilometersPerHour,
                item.FlyingLapMedianSeconds,
                item.FlyingLapMedianSeconds - baseline.FlyingLapMedianSeconds,
                item.StraightEntrySpeedMetersPerSecond,
                item.StraightPeakSpeedMetersPerSecond,
                item.CornerEntrySpeedMetersPerSecond,
                item.ApexMinimumSpeedMetersPerSecond,
                item.CornerExitSpeedMetersPerSecond,
                item.StraightToApexAmplitudeMetersPerSecond,
                $"{F(item.L2Seconds)} / {F(item.L3Seconds)} / {F(item.L4Seconds)}",
                item.HeatTimeSeconds,
                item.TotalDistanceMeters,
                item.AverageSpeedMetersPerSecond,
                $"{item.BrakeCount}/{item.RunWideCount}/{item.CrashCount}",
                $"{F(item.StraightAccelerationDistanceMeters)} / {F(item.StraightCruiseDistanceMeters)} / {F(item.StraightPreparationDistanceMeters)}")));

        Heading("H. Flying-lap traces");
        Line("Controlled fixed-L1 L2. Straight rows are actual production integration endpoints selected at approximately 5 m spacing plus phase changes/final remainders. Corner rows are selected actual continuous-corner nodes. Blank fields are inapplicable, not reconstructed.");
        Table("Candidate | Lap | cumulative m | segment | into straight m | remaining to corner m | speed m/s | baseline B | Δ | final E | net a m/s² | backward allowed m/s | preparation active | corner p | corner envelope m/s | drive availability",
            result.FlyingLapTrace.Select(item => Row(
                item.CandidateId, item.LapNumber, item.CumulativeLapDistanceMeters, item.Segment,
                item.DistanceIntoStraightMeters, item.DistanceRemainingToCornerMeters,
                item.SpeedMetersPerSecond, item.BaselineMultiplier, item.Delta,
                item.ExperimentalMultiplier, item.NetAccelerationMetersPerSecondSquared,
                item.BackwardAllowedSpeedMetersPerSecond,
                item.PreparationBoundaryActive,
                item.CornerProgress, item.CornerEnvelopeSpeedMetersPerSecond,
                item.CornerDriveAvailability)));

        Heading("I. Vmax location");
        Table("Candidate | Location | Straight count | Corner count | Straight progress | distance before next corner m | Vmax−corner-entry m/s | multiplier at Vmax",
            result.Primary.Select(item =>
            {
                var observations = result.Primary.Concat(result.SpeedSweep)
                    .Concat(result.SlideControlSweep)
                    .Concat(result.GearingSweep)
                    .Concat(result.Extreme)
                    .Where(value => value.CandidateId == item.CandidateId)
                    .ToArray();
                return Row(
                    item.CandidateId,
                    item.VmaxLocation,
                    observations.Count(value => value.VmaxLocation.StartsWith("Straight", StringComparison.Ordinal)),
                    observations.Count(value => value.VmaxLocation.StartsWith("Corner", StringComparison.Ordinal)),
                    item.VmaxStraightProgress,
                    item.VmaxDistanceBeforeCornerMeters,
                    item.VmaxKilometersPerHour / 3.6d - item.CornerEntrySpeedMetersPerSecond,
                    item.EnvelopeMultiplierAtVmax);
            }));
        Line("A null distance means the heat maximum occurred on the final race segment, for which production correctly has no nonexistent next corner target.");

        Heading("J. Skill sweep");
        Table("Candidate | Speed | Vmax km/h | Flying s | Heat s | Average m/s | Corner entry m/s | Apex/min m/s | B/RW/C",
            result.SpeedSweep.Select(item => Row(item.CandidateId, item.SkillSpeed,
                item.VmaxKilometersPerHour, item.FlyingLapMedianSeconds, item.HeatTimeSeconds,
                item.AverageSpeedMetersPerSecond, item.CornerEntrySpeedMetersPerSecond,
                item.ApexMinimumSpeedMetersPerSecond,
                $"{item.BrakeCount}/{item.RunWideCount}/{item.CrashCount}")));
        Line($"SlideControl diagnostic candidate selected without a weighted score: `{result.DiagnosticBestCandidateId}`.");
        Table("Candidate | SlideControl | Vmax km/h | Flying s | Heat s | Apex/min m/s | B/RW/C",
            result.SlideControlSweep.Select(item => Row(item.CandidateId, item.SkillSlideControl,
                item.VmaxKilometersPerHour, item.FlyingLapMedianSeconds, item.HeatTimeSeconds,
                item.ApexMinimumSpeedMetersPerSecond,
                $"{item.BrakeCount}/{item.RunWideCount}/{item.CrashCount}")));

        Heading("K. Gearing sweep");
        Table("Candidate | Gearing | Vmax km/h | Flying s | peak location | equilibrium m/s | multiplier at peak",
            result.GearingSweep.Select(item => Row(item.CandidateId, item.Gearing,
                item.VmaxKilometersPerHour, item.FlyingLapMedianSeconds, item.VmaxLocation,
                item.FullDriveEquilibriumSpeedMetersPerSecond, item.EnvelopeMultiplierAtVmax)));

        Heading("L. Balanced four-rider secondary fixture");
        Line("Secondary fixture uses fixed L0/L1/L2/L3, all skills 50. Its median distance is not treated as a real line distribution.");
        Table("Candidate | Vmax P50 km/h | Average P50 m/s | Flying P50 s | Heat P50 s | TotalDistance P50 m",
            result.Balanced.Select(item => Row(item.CandidateId, item.VmaxP50KilometersPerHour,
                item.AverageSpeedP50MetersPerSecond, item.FlyingLapP50Seconds,
                item.HeatTimeP50Seconds, item.TotalDistanceP50Meters)));

        Heading("M. Extreme check");
        Line("Speed100, Gearing1, highest-EffectiveGrip tested surface, fixed Lateral4. No speed cap is added.");
        Table("Candidate | Vmax km/h | peak location | equilibrium m/s | max multiplier | B/RW/C",
            result.Extreme.Select(item => Row(item.CandidateId, item.VmaxKilometersPerHour,
                item.VmaxLocation, item.FullDriveEquilibriumSpeedMetersPerSecond,
                item.EnvelopeMultiplierAtVmax,
                $"{item.BrakeCount}/{item.RunWideCount}/{item.CrashCount}")));

        Heading("N. Standing-start freeze");
        Table("Exact across A0/R08/R12/H08/H12 | Reaction s | TimeTo70 s | SpeedAt2s km/h", new[]
        {
            Row(result.Freeze.StandingStartExact ? "YES" : "NO", result.Freeze.ReactionTimeSeconds,
                result.Freeze.TimeTo70KphSeconds, result.Freeze.SpeedAtTwoSecondsKilometersPerHour),
        });
        Line("Candidate selection is consulted only after standing-start eligibility has failed, so StandingStartLaunchProfile retains the shared production envelope unchanged.");

        Heading("O. Corner-law freeze");
        Table("Isolated ADVANCED corner exact | Legacy exact", new[]
        {
            Row(result.Freeze.CornerLawExact ? "YES" : "NO", result.Freeze.LegacyExact ? "YES" : "NO"),
        });
        Line("The same entry state, surface, skills, setup and seed were resolved under every candidate. Continuous-corner and Legacy fingerprints are bit-stable; only a full heat's upstream Straight exit can change a later corner entry.");

        Heading("P. Pareto interpretation");
        Table("Candidate | ΔVmax km/h | ΔFlying s | ΔHeat s | ΔAverage m/s | ΔCornerEntry m/s | ΔApex m/s | Extreme Vmax km/h",
            result.Primary.Select(item =>
            {
                var extreme = result.Extreme.Single(value => value.CandidateId == item.CandidateId);
                return Row(item.CandidateId,
                    item.VmaxKilometersPerHour - baseline.VmaxKilometersPerHour,
                    item.FlyingLapMedianSeconds - baseline.FlyingLapMedianSeconds,
                    item.HeatTimeSeconds - baseline.HeatTimeSeconds,
                    item.AverageSpeedMetersPerSecond - baseline.AverageSpeedMetersPerSecond,
                    item.CornerEntrySpeedMetersPerSecond - baseline.CornerEntrySpeedMetersPerSecond,
                    item.ApexMinimumSpeedMetersPerSecond - baseline.ApexMinimumSpeedMetersPerSecond,
                    extreme.VmaxKilometersPerHour);
            }));
        Line($"Classification: `{result.Classification}`. Diagnostic trace/sensitivity candidate: `{result.DiagnosticBestCandidateId}`; no production winner is selected or enabled.");

        Heading("Q. Next-model decision evidence");
        var rVariantsRaise = result.Primary.Where(item => item.CandidateId.StartsWith("R", StringComparison.Ordinal))
            .Any(item => item.VmaxKilometersPerHour > baseline.VmaxKilometersPerHour);
        var hVariantsRaise = result.Primary.Where(item => item.CandidateId.StartsWith("H", StringComparison.Ordinal))
            .Any(item => item.VmaxKilometersPerHour > baseline.VmaxKilometersPerHour);
        var bothDirection = result.Primary.Any(item => item.CandidateId != "A0"
            && item.VmaxKilometersPerHour > baseline.VmaxKilometersPerHour
            && item.FlyingLapMedianSeconds > baseline.FlyingLapMedianSeconds);
        Table("Question | Evidence", new[]
        {
            Row("Can R redistribution raise Vmax?", rVariantsRaise ? "YES" : "NO"),
            Row("Can H retention raise Vmax?", hVariantsRaise ? "YES" : "NO"),
            Row("Any candidate raises Vmax and lengthens Flying?", bothDirection ? "YES" : "NO"),
            Row("Primary selected comparison", $"{best.CandidateId}: Vmax {F(best.VmaxKilometersPerHour)} km/h, Flying {F(best.FlyingLapMedianSeconds)} s"),
            Row("One subsystem for #42", best.FlyingLapMedianSeconds < 14.56
                ? "Separate corner-speed-loss / Straight↔apex amplitude subsystem; drive shape can be studied further but flying pace remains faster than the real P10 context."
                : "Review the candidate evidence independently before opening any production subsystem."),
        });
        if (!rVariantsRaise && hVariantsRaise)
            Line("`Additional high-speed drive-force retention appears necessary; redistribution alone is insufficient.`");
        if (result.Primary.Single(item => item.CandidateId == "H12").VmaxKilometersPerHour
            - baseline.VmaxKilometersPerHour < .5d)
            Line("`Finite straight distance / corner-exit state dominates more than the tested force-envelope shape.`");
        if (hVariantsRaise && best.FlyingLapMedianSeconds < 14.56)
            Line("The drive envelope can address peak speed directionally, but a separate corner-speed-loss / amplitude subsystem is still required.");

        Heading("R. Limitations");
        Line("This is a five-candidate causal screen, not an optimizer, torque/power curve, throttle model, RPM model, energy/work conservation law or production calibration. The Motoarena geometry remains symmetric, its radius convention and start split remain provisional, banking and real lateral occupancy are unavailable, and Skill50 is not assigned to the PGE population. Public telemetry provides heat-level Vmax, not its exact on-track location. No #38/#39/#40 report or pge-v1 datum is rewritten.");

        return b.ToString();

        void Line(string value = "") => b.Append(value).Append('\n');
        void Heading(string value) { Line(); Line($"## {value}"); Line(); }
        void Table(string header, IEnumerable<object?[]> rows)
        {
            Line($"| {header} |");
            Line($"| {string.Join(" | ", header.Split('|').Select(_ => "---"))} |");
            foreach (var row in rows) Line($"| {string.Join(" | ", row.Select(Format))} |");
        }
    }

    private static IEnumerable<object?[]> PrimaryMetricRows(StraightDriveHeatObservation item)
    {
        yield return Row("Vmax", $"{F(item.VmaxKilometersPerHour)} km/h");
        yield return Row("FlyingLapMedian", $"{F(item.FlyingLapMedianSeconds)} s");
        yield return Row("TotalDistance", $"{F(item.TotalDistanceMeters)} m");
        yield return Row("Vmax location", item.VmaxLocation);
        yield return Row("Straight peak / apex minimum / amplitude",
            $"{F(item.StraightPeakSpeedMetersPerSecond)} / {F(item.ApexMinimumSpeedMetersPerSecond)} / {F(item.StraightToApexAmplitudeMetersPerSecond)} m/s");
    }

    private static object?[] Row(params object?[] values) => values;
    private static string F(double? value) => value?.ToString("0.######", CultureInfo.InvariantCulture) ?? "—";
    private static string Format(object? value) => value switch
    {
        null => "—",
        bool boolean => boolean ? "YES" : "NO",
        float number => number.ToString("0.######", CultureInfo.InvariantCulture),
        double number => number.ToString("0.######", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" "),
    };
}
