using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

/// <summary>Pure invariant-culture renderer. It performs no file or network I/O.</summary>
public static class MotoarenaMatchedVenueReport
{
    public const string BaseMainSha = "de0dabee70e1248278f6cc5ff4d7293f1501b17c";

    public static string Render(MotoarenaMatchedVenueCalibrationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var b = new StringBuilder();
        var p = result.Profile;
        var venue = result.VenueDataset;
        var balanced = result.MotoarenaBalanced;
        var standing = result.StandingExampleBalanced;
        var geometry = p.CreateGeometry();
        var l0 = result.Lines.Single(line => line.LateralPosition == 0).LapDistanceMeters;
        var publishedDifference = l0 - p.PublishedTrackLengthMeters;
        var vmaxKph = Metric(balanced, rider => CalibrationUnits.MetersPerSecondToKph(rider.Performance.MaximumSpeedMetersPerSecond));
        var average = Metric(balanced, rider => rider.Performance.AverageSpeedMetersPerSecond!.Value);
        var flying = Metric(balanced, rider => rider.Performance.FlyingLapMedianSeconds!.Value);
        var totalDistance = Metric(balanced, rider => rider.Performance.TotalDistanceMeters);
        var realVmax = venue.Distributions["pge_clean_vmax"];
        var realAverage = venue.Distributions["pge_clean_average_speed"];
        var realFlying = venue.Distributions["pge_clean_flying_lap_median"];
        var realDistance = venue.Distributions["pge_clean_total_distance"];
        var allSynthetic = new[] { balanced }
            .Concat(result.SpeedSweep)
            .Concat(result.SlideControlSweep)
            .Concat(result.Lines.Select(line => line.Heat))
            .Concat(result.WidthSensitivity.Select(width => width.BalancedHeat))
            .SelectMany(heat => heat.Riders)
            .ToArray();
        var straightVmax = allSynthetic.Count(rider => rider.VmaxLocation == "Straight");
        var cornerVmax = allSynthetic.Length - straightVmax;
        var vmaxDeficit = vmaxKph < realVmax.Quantiles.P10;
        var averageMismatch = average < realAverage.Quantiles.P10 || average > realAverage.Quantiles.P90;
        var flyingMismatch = flying < realFlying.Quantiles.P10 || flying > realFlying.Quantiles.P90;
        var linesInsideDistanceEnvelope = result.Lines
            .Where(line => FixedLineDistance(line) >= realDistance.Quantiles.P10
                && FixedLineDistance(line) <= realDistance.Quantiles.P90)
            .Select(line => line.LateralPosition)
            .ToArray();
        var realMedianEquivalentLine = InterpolateLineForDistance(result.Lines, realDistance.Quantiles.P50);
        var widthSignalStable = result.WidthSensitivity.All(width =>
            width.VmaxKilometersPerHour < realVmax.Quantiles.P10
            && width.FlyingLapMedianSeconds < realFlying.Quantiles.P10
            && width.BalancedHeat.Riders.All(rider => rider.VmaxLocation == "Straight"));

        Line("# Motoarena matched-venue calibration foundation (#39)");

        Heading("A. Provenance");
        Line($"Base main SHA: `{BaseMainSha}`.");
        Line("Frozen production physics version: `#38 — Calibrate continuous corner speed envelope`.");
        Line($"Dataset: `{RealWorldCalibrationDataset.SourceVersion}`. Exact selector: `season == {p.Season} && league == \"PGEE\" && source_track_label == \"{p.SourceTrackLabel}\"`.");
        Line("The selector uses ordinal label equality. It does not infer the 2025 `Toruń` label, home-team aliases, or venue aliases.");
        Line($"Current 2026 primary source: {p.SourceName}, “{p.CurrentSourceTitle}”, used for track length, current straight/bend widths and the 56.500 s record. Supporting geometry/history source: {p.SourceName}, “{p.SupportingSourceTitle}”, used for 62 m straights, 31 m radius and the pre-2017 second-corner history. The task-supplied source observations are stored as calibration evidence; tests and report generation are offline.");

        Heading("B. Published geometry");
        Table("Field | Value | Evidence classification", new[]
        {
            Row("Published track length", $"{F(p.PublishedTrackLengthMeters)} m", "ExternalPublished"),
            Row("Straight length", $"{F(p.StraightLengthMeters)} m each", "ExternalPublished"),
            Row("Bend radius", $"{F(p.ReferenceRadiusMeters)} m", "ExternalPublishedRadius / MeasurementConventionNotExplicitlyVerified"),
            Row("Starting / back straight width", $"{SourceDimension(p.PublishedStraightWidthMeters)} / {SourceDimension(p.PublishedStraightWidthMeters)} m", "Current2026ExternalPublished"),
            Row("First / second bend width", $"{SourceDimension(p.PublishedFirstBendWidthMeters)} / {SourceDimension(p.PublishedSecondBendWidthMeters)} m", "Current2026ExternalPublishedAsymmetric"),
            Row("Modeled symmetric turn width", $"{SourceDimension(p.ModeledSymmetricTurnWidthMeters)} m", p.ModeledTurnWidthClassification),
            Row("Older article general bend width", $"{SourceDimension(p.OlderArticleBendWidthMeters)} m", p.OlderArticleBendWidthClassification),
            Row("Surface", p.SurfaceDescription, "ExternalPublished"),
            Row("Historical record", "56.500 s — Jack Holder, 2017", "HistoricalContext only; not a Skill100 target"),
        });
        Line(p.GeometryConfidenceNotes);
        Line("Source conflict: the older/general article states 18 m bends, while the current official 2026 venue card states 17.0 m for the first bend and 16.2 m for the second. The current 2026 values take precedence; 18 m is retained only as `OlderArticleReferenceOnly` sensitivity context.");
        Line("The 16.6 m modeled width is the arithmetic mean `(17.0 + 16.2) / 2`. For two semicircular bends at the same normalized lateral position, linear lateral-radius offset means this proxy preserves the two bends' combined full-lap path-length contribution in the current symmetric representation. It does not preserve either bend's local radius, local safe speed, banking, or first/second-corner asymmetry.");
        Line("The source reports that the second corner geometry was changed before the 2017 season and that the track length has been 318 m since that change.");
        Line("The published radius is used as the model's reference-radius approximation because two 62 m straights plus two semicircles at 31 m closely reproduce the published length. This is not a verified equality of measurement conventions or a geodetic reconstruction.");

        Heading("C. Modeled geometry");
        Line("Topology: marked Straight start-half → TurnEntry → TurnMiddle → TurnExit → 62 m back Straight → TurnEntry → TurnMiddle → TurnExit → finish-half Straight. Each logical corner is three 60° reporting subsegments and totals 180°.");
        Line($"The two corners use `SymmetricGeometryApproximation` with a primary modeled turn width of {F(p.ModeledSymmetricTurnWidthMeters)} m. The production model does not represent the current 17.0/16.2 m width asymmetry or the reported second-corner shape change. Known missing venue physics: banking.");
        Line($"Width envelope check: {(geometry.IsWithinFIMSpeedwayWidthEnvelope() ? "true" : "false")}. Modeled L0 lap = {F(l0)} m; difference to published 318 m = {Signed(publishedDifference)} m ({Signed(100d * publishedDifference / p.PublishedTrackLengthMeters)}%). The published 31 m input is not adjusted to force equality.");
        Table("Lateral reference | Modeled lap distance m | Difference vs L0 m", result.Lines.Select(line =>
            Row(line.LateralPosition, line.LapDistanceMeters, line.LapDistanceMeters - l0)));
        Line();
        Table("Symmetric turn width m | Classification | L0 m | L1 m | L2 m | L3 m | L4 m | Vmax P50 km/h | AverageSpeed P50 m/s | Flying P50 s | HeatTime P50 s | TotalDistance P50 m",
            result.WidthSensitivity.Select(width => Row(
                width.SymmetricTurnWidthMeters,
                width.Classification,
                width.LapDistancesMeters[0],
                width.LapDistancesMeters[1],
                width.LapDistancesMeters[2],
                width.LapDistancesMeters[3],
                width.LapDistancesMeters[4],
                width.VmaxKilometersPerHour,
                width.AverageSpeedMetersPerSecond,
                width.FlyingLapMedianSeconds,
                width.HeatTimeSeconds,
                width.TotalDistanceMeters)));
        Line("Width sensitivity is calibration-only. The 16.6 m row is primary; 16.2/17.0 m bound the current published bend widths, and 18.0 m is `OlderArticleReferenceOnly`.");
        Line();
        Table("Fixed line | Lap distance m | Four-lap synthetic distance m | Real P10–P90 status", result.Lines.Select(line => Row(
            line.LateralPosition,
            line.LapDistanceMeters,
            FixedLineDistance(line),
            FixedLineDistance(line) >= realDistance.Quantiles.P10 && FixedLineDistance(line) <= realDistance.Quantiles.P90
                ? "inside real P10–P90"
                : FixedLineDistance(line) < realDistance.Quantiles.P10 ? "below real P10" : "above real P90")));
        Line($"Real TotalDistance P10/P25/P50/P75/P90 = {F(realDistance.Quantiles.P10)} / {F(realDistance.Quantiles.P25)} / {F(realDistance.Quantiles.P50)} / {F(realDistance.Quantiles.P75)} / {F(realDistance.Quantiles.P90)} m. Fixed normalized line(s) inside P10–P90: {(linesInsideDistanceEnvelope.Length == 0 ? "none" : string.Join(", ", linesInsideDistanceEnvelope))}. Linear interpolation places real P50 near fixed normalized line {F(realMedianEquivalentLine)}. Real lateral-path occupancy and telemetry distance convention are unknown, so line-distance plausibility remains unresolved.");

        Heading("D. Dataset subset coverage");
        var c = result.Coverage;
        Table("Coverage item | Count/value", new[]
        {
            Row("Matches", c.MatchCount),
            Row("Exact match IDs", string.Join(", ", c.MatchIds)),
            Row("Raw rider-heat rows", c.RiderHeatRowCount),
            Row("CompleteTelemetry", c.CompleteTelemetryCount),
            Row("CleanPhysics", c.CleanPhysicsCount),
            Row("Eventful", c.EventfulCount),
            Row("AuditOnly", c.AuditOnlyCount),
            Row("Full four-rider CleanPhysics attempts", c.FourRiderCleanPhysicsAttemptCount),
        });

        Heading("E. Real Motoarena distributions");
        Table("Metric | Unit | N | P10 | P25 | P50 | P75 | P90", RealMetricRows(venue));
        Line();
        Table("Four-rider spread | Unit | N | P10 | P25 | P50 | P75 | P90", SpreadRows(venue));

        Heading("F. Synthetic fixture");
        Line($"Production path: `CalibrationRunner → HeatSimulator`; venue `{p.VenueId}`; primary modeled symmetric turn width {F(p.ModeledSymmetricTurnWidthMeters)} m; primary provisional start split 31/31 m; four riders with all six skills 50; neutral setup; fixed normalized lines 0–3; HoldLane; baseline surface `(grip=1, ruts=0, moisture=0.35)`; Dry weather; incidents off; seed {MotoarenaMatchedVenueCalibration.FixedSeed}; four laps.");
        Line("Skill50 is a game-scale fixture, not an average PGEE rider. No real rider receives synthetic skills.");
        Table("Rider | Lane | Vmax km/h | Vmax location | Average m/s | L1 s | L2 s | L3 s | L4 s | Flying median s | L1 penalty s | HeatTime s | Distance m | Brake | RunWide | Crash | Corner min m/s | Corner entry/exit m/s",
            balanced.Riders.Select(rider => Row(
                rider.Performance.RiderId,
                rider.Performance.RiderId - 1,
                CalibrationUnits.MetersPerSecondToKph(rider.Performance.MaximumSpeedMetersPerSecond),
                Location(rider),
                rider.Performance.AverageSpeedMetersPerSecond,
                rider.Performance.L1Seconds,
                rider.Performance.L2Seconds,
                rider.Performance.L3Seconds,
                rider.Performance.L4Seconds,
                rider.Performance.FlyingLapMedianSeconds,
                rider.Performance.FirstLapPenaltySeconds,
                rider.Performance.TotalTimeSeconds,
                rider.Performance.TotalDistanceMeters,
                rider.Performance.BrakeCount,
                rider.Performance.RunWideCount,
                rider.Performance.CrashCount,
                rider.MinimumCornerSpeedMetersPerSecond,
                $"{F(rider.MedianCornerEntrySpeedMetersPerSecond)} / {F(rider.MedianCornerExitSpeedMetersPerSecond)}")));
        Line($"All 24 input-order permutations were executed. Trace hashes: {result.PermutationTraceSha256.Distinct(StringComparer.Ordinal).Count()} distinct across {result.PermutationTraceSha256.Count} permutations.");

        Heading("G. Standing-start split sensitivity");
        Line(p.StartLineConfidenceNotes);
        Line("The three cases preserve the same 62 m home straight, 62 m back straight, two 180° corners and every flying-lap path length. The split is not tuned to telemetry. L1 remains `StartLineSensitiveContext`.");
        Table("Start→corner m | Finish-half m | Lap distance L1 m | L1 P50 s | Flying median P50 s | HeatTime P50 s | First-corner entry P50 m/s | Vmax P50 m/s",
            result.StartSplitSensitivity.Select(split => Row(
                split.StartLineToFirstCornerMeters,
                split.FinishStraightMeters,
                split.LapDistanceMeters,
                split.L1Seconds,
                split.FlyingLapMedianSeconds,
                split.HeatTimeSeconds,
                split.FirstCornerEntrySpeedMetersPerSecond,
                split.VmaxMetersPerSecond)));

        Heading("H. Synthetic standing example vs Motoarena geometry");
        Table("Metric | Synthetic #38 standing example | Motoarena geometry / same rider fixture | Motoarena - standing", ComparisonRows(standing, balanced));

        Heading("I. Motoarena synthetic vs Motoarena real telemetry");
        Table("Metric | Synthetic #38 standing example | Motoarena synthetic | Real Motoarena P10 | P50 | P90 | Synthetic - real P50", MatchedRows(venue, standing, balanced));
        Line("Vmax and AverageSpeed are compared as distributions, not equality targets. L2/L3/L4, FlyingLapMedian, HeatTime and TotalDistance are now venue-matched context, while L1/L1Penalty remain start-line-sensitive.");

        Heading("J. Flying-lap speed profile");
        Line("Balanced Speed50 rider on Lateral1, L2. Corner values are interpolated between actual production traversal nodes. Straight quarter points use energy-space interpolation across the actual production profile's measured acceleration/cruise/deceleration phase distances and exact entry/peak/exit speeds; this is an observation projection, not a second physics path.");
        Table("Point | Cumulative distance m | Segment | CornerProgress | Actual speed m/s | Local envelope m/s | Drive availability",
            result.FlyingLapProfile.Select(point => Row(
                point.Point,
                point.CumulativeDistanceMeters,
                point.SegmentType,
                point.CornerProgress,
                point.ActualSpeedMetersPerSecond,
                point.LocalEnvelopeMetersPerSecond,
                point.DriveAvailability)));

        Heading("K. Vmax location");
        Line($"Across {allSynthetic.Length} controlled matched-venue rider observations (balanced, Speed, SlideControl and fixed-line sweeps), {Pct(straightVmax, allSynthetic.Length)}% peak on Straight and {Pct(cornerVmax, allSynthetic.Length)}% in a Corner.");
        var cornerProgress = allSynthetic.Where(rider => rider.VmaxCornerProgress.HasValue)
            .Select(rider => rider.VmaxCornerProgress!.Value).Order().ToArray();
        Line(cornerProgress.Length == 0
            ? "Corner Vmax progress distribution: no Corner maxima observed. This is reported, not forced to match the public telemetry context."
            : $"Corner Vmax progress P10/P50/P90: {F(Q(cornerProgress, .1))} / {F(Q(cornerProgress, .5))} / {F(Q(cornerProgress, .9))}.");
        Table("Sweep | Value/line | Vmax P50 km/h | Average P50 m/s | Flying P50 s | HeatTime P50 s | Vmax location(s)", SweepRows(result));

        Heading("L. Diagnosis");
        Table("Diagnostic | Observation | Classification", new[]
        {
            Row("Vmax", $"Synthetic P50 {F(vmaxKph)} km/h vs real P10/P50/P90 {F(realVmax.Quantiles.P10)} / {F(realVmax.Quantiles.P50)} / {F(realVmax.Quantiles.P90)}", vmaxDeficit ? "general/profile peak deficit remains" : "within venue distribution context"),
            Row("AverageSpeed", $"Synthetic P50 {F(average)} m/s vs real P10/P50/P90 {F(realAverage.Quantiles.P10)} / {F(realAverage.Quantiles.P50)} / {F(realAverage.Quantiles.P90)}", averageMismatch ? "residual remains; attribution depends on lateral path and telemetry distance convention" : "within venue distribution context"),
            Row("FlyingLapMedian", $"Synthetic P50 {F(flying)} s vs real P10/P50/P90 {F(realFlying.Quantiles.P10)} / {F(realFlying.Quantiles.P50)} / {F(realFlying.Quantiles.P90)}", flyingMismatch ? "timing/profile mismatch remains" : "within venue distribution context"),
            Row("TotalDistance", $"Fixed-line distances are compared separately with real P10/P50/P90 {F(realDistance.Quantiles.P10)} / {F(realDistance.Quantiles.P50)} / {F(realDistance.Quantiles.P90)} m; real P50 interpolates near line {F(realMedianEquivalentLine)}", "line-distance plausibility unresolved without real lateral-path occupancy"),
            Row("Straight↔apex amplitude", widthSignalStable ? "Low Vmax, faster-than-P10 flying laps and Straight-only maxima persist across 16.2–18.0 m symmetric-width sensitivity" : "The signal changes across symmetric-width sensitivity", "diagnostic hypothesis; real corner minima are unavailable"),
            Row("L1", "Sensitivity table changes start preparation while published start offset remains unknown", "StartLineSensitiveContext; unsupported as a primary target"),
            Row("Venue effects", "Banking and second-corner asymmetry are absent", "known missing venue physics / geometry representation"),
        });
        Line("Residuals are not automatically assigned to physics. AverageSpeed equals TotalDistance / HeatTime, so its residual cannot be attributed solely to longitudinal physics before real lateral-path occupancy and telemetry distance convention are resolved. Rider-population mapping, symmetric geometry and missing banking remain competing explanations.");

        Heading("M. Frozen physics");
        Table("Boundary | Frozen #38 value", new[]
        {
            Row("ADVANCED settled/apex reference", "19 m/s"),
            Row("Legacy/reference compatibility", "16 m/s"),
            Row("Apex / full-drive progress", "0.50 / 5/6"),
            Row("Correction / quiet", "2.0→3.2 m/s² / 1.015"),
            Row("Brake / RunWide / retention", "1.06→1.14 / 1.18→1.34 / 0.35→0.65"),
            Row("Straight / corner full-drive", "1.60→3.20 / 1.20→2.80 m/s²"),
            Row("Force fade / reference", "0.035 / 0.010; 16 m/s"),
            Row("Mass / resistance", "142 kg; 40 + 0.20v² N"),
            Row("Gearing / surface drive", "1.10→0.90; 0.75 + 0.25×EffectiveGrip"),
            Row("Integration", "1 m + exact remainder"),
            Row("Reaction / launch", "0.28→0.20 s / 9→11 m/s²"),
            Row("Incidents, RNG, lateral, contact, surface, setup and skills", "unchanged"),
        });
        Line("NO PHYSICS CONSTANTS CHANGED. #39 measures the frozen #38 production path and does not tune it.");

        Heading("N. Known limitations");
        Line("This is a matched-venue reference-radius approximation, not a digital twin. The public 31 m radius measurement convention has not been explicitly verified against `InnerRadiusMeters`. Current bend widths are 17.0/16.2 m, but production `TrackGeometry` requires one turn width; 16.6 m is a transparent arithmetic-mean proxy. It preserves aggregate full-lap lateral contribution for a common normalized line, not local corner radii, safe speeds or asymmetry. The start-line split is provisional, and banking is absent. Surface material is provenance context; the synthetic heat uses the established baseline dry surface state rather than a granite-specific physics law. Active lateral motion still lacks diagonal/spiral path-length integration. Real telemetry supplies no lateral occupancy, corner minimum or per-point throttle trace. The historical 56.500 s record remains HistoricalContext and is not a target.");

        Heading("Next-model decision evidence");
        Table("Question | Evidence", new[]
        {
            Row("1. Did real geometry alone materially raise Vmax?", DeltaAnswer(standing, balanced, rider => CalibrationUnits.MetersPerSecondToKph(rider.Performance.MaximumSpeedMetersPerSecond), "km/h")),
            Row("2. Did AverageSpeed move in the useful direction?", DirectionAnswer(standing, balanced, rider => rider.Performance.AverageSpeedMetersPerSecond!.Value, realAverage.Quantiles.P50, "m/s")),
            Row("3. Are flying laps fast, slow or within the envelope?", flying < realFlying.Quantiles.P10 ? "Synthetic flying laps are faster than the real P10 envelope." : flying > realFlying.Quantiles.P90 ? "Synthetic flying laps are slower than the real P90 envelope." : "Synthetic flying laps are inside the real P10–P90 envelope."),
            Row("4. Is Straight↔Apex amplitude too small?", widthSignalStable ? "Likely as a width-stable diagnostic hypothesis: low Vmax, fast flying laps and Straight-only maxima persist from 16.2 to 18.0 m. Venue telemetry has no apex-speed series to prove causation." : "Not stable across the width sensitivity; no single amplitude conclusion is supported."),
            Row("5. Does Vmax remain exclusively on Straight?", cornerVmax == 0 ? "Yes for every controlled matched-venue observation." : $"No; {Pct(cornerVmax, allSynthetic.Length)}% are Corner maxima."),
            Row("6. Are L1 conclusions stable to the start split?", Range(result.StartSplitSensitivity.Select(item => item.L1Seconds), "s") + "; treat L1 as StartLineSensitiveContext."),
            Row("7. Is line distance plausible against real TotalDistance?", $"Unresolved without real lateral-path occupancy. Fixed line(s) inside real P10–P90: {(linesInsideDistanceEnvelope.Length == 0 ? "none" : string.Join(", ", linesInsideDistanceEnvelope))}; real P50 interpolates near line {F(realMedianEquivalentLine)}."),
            Row("8. One subsystem for the next experiment", widthSignalStable
                ? "Next diagnostic experiment: longitudinal drive-availability / throttle-profile shape across Straight → corner entry. Width sensitivity supports testing this subsystem, not a conclusion that its physics is proven wrong; do not change it in #39."
                : "Resolve asymmetric-width/local-corner representation before opening a production physics subsystem."),
        });
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

    private static IEnumerable<object?[]> RealMetricRows(RealWorldCalibrationDataset dataset)
    {
        var ids = new[]
        {
            "pge_clean_vmax", "pge_clean_average_speed", "pge_clean_heat_time",
            "pge_clean_l1_time", "pge_clean_l2_time", "pge_clean_l3_time", "pge_clean_l4_time",
            "pge_clean_flying_lap_median", "pge_clean_l1_penalty", "pge_clean_total_distance",
        };
        return ids.Select(id => DistributionRow(dataset.Distributions[id]));
    }

    private static IEnumerable<object?[]> SpreadRows(RealWorldCalibrationDataset dataset)
    {
        var ids = new[]
        {
            "pge_four_rider_vmax_spread", "pge_four_rider_average_speed_spread",
            "pge_four_rider_heat_time_spread", "pge_four_rider_l1_spread",
        };
        return ids.Select(id => DistributionRow(dataset.Distributions[id]));
    }

    private static object?[] DistributionRow(CalibrationDistribution d)
        => Row(d.Definition.MetricId, d.Definition.Unit, d.Observations.Count,
            d.Quantiles.P10, d.Quantiles.P25, d.Quantiles.P50, d.Quantiles.P75, d.Quantiles.P90);

    private static IEnumerable<object?[]> ComparisonRows(MatchedVenueHeatResult standing, MatchedVenueHeatResult venue)
        => SyntheticDefinitions().Select(metric => Row(metric.Label,
            Metric(standing, metric.Select), Metric(venue, metric.Select),
            Metric(venue, metric.Select) - Metric(standing, metric.Select)));

    private static IEnumerable<object?[]> MatchedRows(RealWorldCalibrationDataset real,
        MatchedVenueHeatResult standing, MatchedVenueHeatResult venue)
        => SyntheticDefinitions().Select(metric =>
        {
            var d = real.Distributions[metric.RealMetricId];
            var synthetic = Metric(venue, metric.Select);
            return Row(metric.Label, Metric(standing, metric.Select), synthetic,
                d.Quantiles.P10, d.Quantiles.P50, d.Quantiles.P90, synthetic - d.Quantiles.P50);
        });

    private static IReadOnlyList<SyntheticDefinition> SyntheticDefinitions()
        => Array.AsReadOnly(new[]
        {
            new SyntheticDefinition("Vmax km/h", "pge_clean_vmax", r => CalibrationUnits.MetersPerSecondToKph(r.Performance.MaximumSpeedMetersPerSecond)),
            new SyntheticDefinition("AverageSpeed m/s", "pge_clean_average_speed", r => r.Performance.AverageSpeedMetersPerSecond!.Value),
            new SyntheticDefinition("L2 s", "pge_clean_l2_time", r => r.Performance.L2Seconds!.Value),
            new SyntheticDefinition("L3 s", "pge_clean_l3_time", r => r.Performance.L3Seconds!.Value),
            new SyntheticDefinition("L4 s", "pge_clean_l4_time", r => r.Performance.L4Seconds!.Value),
            new SyntheticDefinition("FlyingLapMedian s", "pge_clean_flying_lap_median", r => r.Performance.FlyingLapMedianSeconds!.Value),
            new SyntheticDefinition("HeatTime s", "pge_clean_heat_time", r => r.Performance.TotalTimeSeconds),
            new SyntheticDefinition("TotalDistance m", "pge_clean_total_distance", r => r.Performance.TotalDistanceMeters),
        });

    private static IEnumerable<object?[]> SweepRows(MotoarenaMatchedVenueCalibrationResult result)
    {
        foreach (var heat in result.SpeedSweep)
            yield return HeatRow("Speed", heat.ScenarioId.Split('/')[^1], heat);
        foreach (var heat in result.SlideControlSweep)
            yield return HeatRow("SlideControl", heat.ScenarioId.Split('/')[^1], heat);
        foreach (var line in result.Lines)
            yield return HeatRow("Lateral", line.LateralPosition.ToString(CultureInfo.InvariantCulture), line.Heat);
    }

    private static object?[] HeatRow(string sweep, string value, MatchedVenueHeatResult heat)
        => Row(sweep, value,
            Metric(heat, rider => CalibrationUnits.MetersPerSecondToKph(rider.Performance.MaximumSpeedMetersPerSecond)),
            Metric(heat, rider => rider.Performance.AverageSpeedMetersPerSecond!.Value),
            Metric(heat, rider => rider.Performance.FlyingLapMedianSeconds!.Value),
            Metric(heat, rider => rider.Performance.TotalTimeSeconds),
            string.Join(", ", heat.Riders.Select(rider => rider.VmaxLocation).Distinct(StringComparer.Ordinal)));

    private static string DeltaAnswer(MatchedVenueHeatResult before, MatchedVenueHeatResult after,
        Func<MatchedVenueRiderObservation, double> select, string unit)
    {
        var first = Metric(before, select);
        var second = Metric(after, select);
        var delta = second - first;
        return $"P50 changed {F(first)} → {F(second)} {unit} ({Signed(delta)} {unit}).";
    }

    private static string DirectionAnswer(MatchedVenueHeatResult before, MatchedVenueHeatResult after,
        Func<MatchedVenueRiderObservation, double> select, double realMedian, string unit)
    {
        var first = Metric(before, select);
        var second = Metric(after, select);
        var beforeGap = Math.Abs(first - realMedian);
        var afterGap = Math.Abs(second - realMedian);
        return $"P50 changed {F(first)} → {F(second)} {unit}; the absolute gap to real P50 {(afterGap < beforeGap ? "decreased" : afterGap > beforeGap ? "increased" : "did not change")}.";
    }

    private static string Range(IEnumerable<double> values, string unit)
    {
        var materialized = values.ToArray();
        return $"P50 range {F(materialized.Min())}–{F(materialized.Max())} {unit} (span {F(materialized.Max() - materialized.Min())} {unit})";
    }

    private static double FixedLineDistance(MatchedVenueLineResult line)
        => line.Heat.Riders.Single().Performance.TotalDistanceMeters;

    private static double InterpolateLineForDistance(
        IEnumerable<MatchedVenueLineResult> lines,
        double targetDistanceMeters)
    {
        var points = lines.OrderBy(line => line.LateralPosition)
            .Select(line => (Line: (double)line.LateralPosition, Distance: FixedLineDistance(line)))
            .ToArray();
        if (targetDistanceMeters <= points[0].Distance) return points[0].Line;
        for (var index = 1; index < points.Length; index++)
        {
            if (targetDistanceMeters > points[index].Distance) continue;
            var before = points[index - 1];
            var after = points[index];
            var fraction = (targetDistanceMeters - before.Distance) / (after.Distance - before.Distance);
            return before.Line + (after.Line - before.Line) * fraction;
        }
        return points[^1].Line;
    }

    private static string Location(MatchedVenueRiderObservation rider)
        => rider.VmaxCornerProgress is { } progress
            ? $"Corner {rider.VmaxSegmentId}/{rider.VmaxSegmentType} @ {F(progress)}"
            : $"Straight {rider.VmaxSegmentId}";

    private static double Metric(MatchedVenueHeatResult heat, Func<MatchedVenueRiderObservation, double> select)
        => Q(heat.Riders.Select(select).Order().ToArray(), .5d);

    private static double Q(IReadOnlyList<double> values, double probability)
        => CalibrationDistribution.LinearQuantile(values, probability);

    private static double Q(IReadOnlyList<float> values, double probability)
        => CalibrationDistribution.LinearQuantile(values.Select(value => (double)value).ToArray(), probability);

    private static double Pct(int count, int total) => total == 0 ? 0d : 100d * count / total;
    private static object?[] Row(params object?[] values) => values;
    private static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private static string SourceDimension(float value) => value.ToString("0.0###", CultureInfo.InvariantCulture);
    private static string Signed(double value) => value.ToString("+0.######;-0.######;0", CultureInfo.InvariantCulture);
    private static string Format(object? value) => value switch
    {
        null => "—",
        float number => number.ToString("0.######", CultureInfo.InvariantCulture),
        double number => number.ToString("0.######", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" "),
    };

    private sealed record SyntheticDefinition(
        string Label,
        string RealMetricId,
        Func<MatchedVenueRiderObservation, double> Select);
}
