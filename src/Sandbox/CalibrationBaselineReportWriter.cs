using System.Globalization;
using System.Text;
using System.Text.Json;
using CoreSim.Analysis;

internal static class CalibrationBaselineReportWriter
{
    private static readonly string[] DistributionOrder =
    {
        "pge_clean_vmax",
        "pge_clean_heat_time",
        "pge_clean_l1_time",
        "pge_clean_l2_time",
        "pge_clean_l3_time",
        "pge_clean_l4_time",
        "pge_clean_flying_lap_median",
        "pge_clean_l1_penalty",
        "pge_clean_average_speed",
        "pge_clean_total_distance",
    };

    private static readonly string[] SpreadOrder =
    {
        "pge_four_rider_heat_time_spread",
        "pge_four_rider_vmax_spread",
        "pge_four_rider_l1_spread",
        "pge_four_rider_average_speed_spread",
    };

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["pge_clean_vmax"] = "Vmax",
        ["pge_clean_heat_time"] = "Heat time",
        ["pge_clean_l1_time"] = "L1",
        ["pge_clean_l2_time"] = "L2",
        ["pge_clean_l3_time"] = "L3",
        ["pge_clean_l4_time"] = "L4",
        ["pge_clean_flying_lap_median"] = "Flying-lap median",
        ["pge_clean_l1_penalty"] = "L1 penalty",
        ["pge_clean_average_speed"] = "Average speed",
        ["pge_clean_total_distance"] = "Total distance",
        ["pge_four_rider_heat_time_spread"] = "Four-rider heat-time spread",
        ["pge_four_rider_vmax_spread"] = "Four-rider Vmax spread",
        ["pge_four_rider_l1_spread"] = "Four-rider L1 spread",
        ["pge_four_rider_average_speed_spread"] = "Four-rider average-speed spread",
    };

    public static void Write(string datasetDirectory, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var dataset = RealWorldCalibrationDataset.ParseCsv(
            File.ReadAllText(Path.Combine(datasetDirectory, "pge_rider_heats.csv")));
        using var summaryDocument = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(datasetDirectory, "summary.json")));
        using var literatureDocument = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(datasetDirectory, "literature_targets.json")));
        var summary = summaryDocument.RootElement;
        ValidateLiteratureProvenance(literatureDocument.RootElement);
        var sweeps = CalibrationSkillSweep.RunRequired();
        var evaluations = sweeps.ToDictionary(
            sweep => sweep.Scenario.ScenarioId,
            sweep => RealWorldCalibrationEvaluator.Evaluate(dataset, sweep.ToSimulationCalibrationResult()),
            StringComparer.Ordinal);

        var builder = new StringBuilder();
        builder.AppendLine("# Current production-model calibration baseline");
        builder.AppendLine();
        builder.AppendLine("This deterministic report measures the unchanged production model against the versioned PGEE telemetry envelope. It does not fit parameters, assign game skills to real riders, or produce an overall score.");
        builder.AppendLine();
        DatasetSection(builder, summary);
        DistributionSection(builder, dataset);
        WithinHeatSection(builder, dataset);
        RiderRelativeSection(builder, summary);
        LiteratureSection(builder);
        BalancedSection(builder, sweeps.Single(item => item.Scenario.ScenarioId == "balanced"), evaluations["balanced"]);
        SweepSection(builder, "Start sweep", sweeps.Where(item => item.Scenario.ScenarioId.StartsWith("start_", StringComparison.Ordinal)), evaluations);
        SweepSection(builder, "Speed sweep", sweeps.Where(item => item.Scenario.ScenarioId.StartsWith("speed_", StringComparison.Ordinal)), evaluations);
        SweepSection(builder, "SlideControl sweep", sweeps.Where(item => item.Scenario.ScenarioId.StartsWith("slide_control_", StringComparison.Ordinal)), evaluations);
        CombinedSection(builder, sweeps.Where(item => item.Scenario.ScenarioId.StartsWith("combined_", StringComparison.Ordinal)), evaluations);
        ModelComparisonSection(builder, evaluations["balanced"]);
        ComparabilitySection(builder);
        GapSection(builder, evaluations["balanced"]);
        builder.AppendLine("## Physics invariant");
        builder.AppendLine();
        builder.AppendLine("**NO PHYSICS CONSTANTS WERE CHANGED IN PR #32.** The sweep uses `CalibrationRunner -> HeatSimulator` with production physics. Calibration diagnostics are observational only.");
        builder.AppendLine();
        builder.AppendLine("Regenerate from the repository root with:");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine("dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- calibration-report data/calibration/pge/v1 docs/calibration/current-model-baseline.md");
        builder.AppendLine("```");

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (directory is not null)
            Directory.CreateDirectory(directory);
        File.WriteAllText(outputPath, builder.ToString().ReplaceLineEndings("\n"), new UTF8Encoding(false));
    }

    private static void DatasetSection(StringBuilder builder, JsonElement summary)
    {
        var counts = summary.GetProperty("dataset_counts");
        var source = summary.GetProperty("source_counts");
        var splits = summary.GetProperty("splits");
        builder.AppendLine("## Dataset");
        builder.AppendLine();
        builder.AppendLine("| Measure | Count |");
        builder.AppendLine("|---|---:|");
        AddCount("Downloaded source matches", source, "downloaded_matches");
        AddCount("Source telemetry details", source, "telemetry_detail_records");
        AddCount("Source heat-result rows", source, "heats_csv_rows");
        AddCount("Source telemetry_full rows", source, "telemetry_full_csv_rows");
        AddCount("PGEE matches", counts, "pgee_match_count");
        AddCount("Normalized PGEE rider-heats", counts, "normalized_row_count");
        AddCount("CompleteTelemetry", counts, "complete_telemetry_count");
        AddCount("CleanPhysics", counts, "clean_physics_count");
        AddCount("Eventful / non-steady complete", counts, "eventful_complete_count");
        AddCount("Audit-only", counts, "audit_only_count");
        AddCount("CompleteTelemetry matches", counts, "complete_match_count");
        AddCount("CompleteTelemetry riders", counts, "complete_rider_count");
        AddCount("Four-rider CleanPhysics attempts", counts, "clean_four_rider_attempt_count");
        builder.AppendLine();
        builder.AppendLine("The task's approximate regression oracle said 5,417 CompleteTelemetry and 89 Eventful rows. Applying the exact stated rule produces 5,410 and 82: six otherwise steady PGEE rows contain source points `W`, which is not in `{0,1,2,3}`, and the remaining one-row difference is already present before the points condition. The filter was not changed to force the oracle.");
        builder.AppendLine();
        builder.AppendLine("CompleteTelemetry requires both presence flags, finite positive heat/L1/L2/L3/L4/Vmax/distance, points in `{0,1,2,3}`, and `abs(heat - sum(laps)) <= 0.05 s`. CleanPhysics additionally requires each of L2-L4 within ±10% of their median. Complete rows outside that heuristic are Eventful/non-steady; this is not a crash or invalidity claim. All other rows remain AuditOnly.");
        builder.AppendLine();
        builder.AppendLine("Physical attempt identity is `match_id + heat_uid`, preserving restarts such as `7734_2_0` and `7734_2_1`. Quantiles use linear interpolation at zero-based sorted position `(n - 1) * p`.");
        builder.AppendLine();
        builder.AppendLine($"Split: {splits.GetProperty("development_match_count").GetInt32()} DEVELOPMENT matches and {splits.GetProperty("final_test_match_count").GetInt32()} newest FINAL_TEST matches (chronological `ceil(20%)`, match-level). No match is split between partitions or folds.");
        builder.AppendLine();
        builder.AppendLine("| Development fold | Matches | Clean rows |");
        builder.AppendLine("|---:|---:|---:|");
        foreach (var fold in splits.GetProperty("development_folds").EnumerateArray())
            builder.AppendLine($"| {fold.GetProperty("fold").GetInt32()} | {fold.GetProperty("match_count").GetInt32()} | {fold.GetProperty("clean_row_count").GetInt32()} |");
        builder.AppendLine();
        builder.AppendLine("Track metadata coverage is zero in this v1 snapshot; the original source label is retained without inventing aliases or geometry.");
        builder.AppendLine();
        return;

        void AddCount(string label, JsonElement element, string property)
            => builder.AppendLine($"| {label} | {element.GetProperty(property).GetInt32()} |");
    }

    private static void DistributionSection(StringBuilder builder, RealWorldCalibrationDataset dataset)
    {
        builder.AppendLine("## Real distributions");
        builder.AppendLine();
        builder.AppendLine("CleanPhysics observations define distributions/performance envelopes, never hard caps.");
        builder.AppendLine();
        builder.AppendLine("| Metric | Unit | N | P01 | P10 | P25 | P50 | P75 | P90 | P99 |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var id in DistributionOrder)
            AddDistribution(builder, dataset.Distributions[id]);
        builder.AppendLine();
    }

    private static void WithinHeatSection(StringBuilder builder, RealWorldCalibrationDataset dataset)
    {
        builder.AppendLine("## Within-heat spreads");
        builder.AppendLine();
        builder.AppendLine("Each value is max minus min within an attempt containing exactly four CleanPhysics riders. These are distribution constraints, not per-heat equalities.");
        builder.AppendLine();
        builder.AppendLine("| Metric | Unit | N | P01 | P10 | P25 | P50 | P75 | P90 | P99 |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var id in SpreadOrder)
            AddDistribution(builder, dataset.Distributions[id]);
        builder.AppendLine();
    }

    private static void AddDistribution(StringBuilder builder, CalibrationDistribution distribution)
    {
        var q = distribution.Quantiles;
        builder.AppendLine($"| {Labels[distribution.Definition.MetricId]} | {distribution.Definition.Unit} | {distribution.Observations.Count} | {F(q.P01)} | {F(q.P10)} | {F(q.P25)} | {F(q.P50)} | {F(q.P75)} | {F(q.P90)} | {F(q.P99)} |");
    }

    private static void RiderRelativeSection(StringBuilder builder, JsonElement summary)
    {
        var relative = summary.GetProperty("rider_relative");
        builder.AppendLine("## Rider-relative observations");
        builder.AppendLine();
        builder.AppendLine($"The four-rider CleanPhysics attempts yield {relative.GetProperty("observation_count").GetInt32()} residual observations across {relative.GetProperty("rider_count").GetInt32()} riders. Each residual is the rider value minus its heat mean; residuals therefore sum to approximately zero within every heat.");
        builder.AppendLine();
        builder.AppendLine("| Split-half metric | Eligible riders (>=12 observations) | Spearman rho |");
        builder.AppendLine("|---|---:|---:|");
        foreach (var property in relative.GetProperty("split_half_persistence").EnumerateObject())
        {
            var value = property.Value;
            builder.AppendLine($"| {property.Name} | {value.GetProperty("rider_sample_size").GetInt32()} | {F(value.GetProperty("spearman_rank_correlation").GetDouble())} |");
        }
        builder.AppendLine();
        builder.AppendLine("| Within-heat residual relationship | N | Spearman rho |");
        builder.AppendLine("|---|---:|---:|");
        foreach (var property in relative.GetProperty("within_heat_correlations").EnumerateObject())
        {
            var value = property.Value;
            builder.AppendLine($"| {property.Name} | {value.GetProperty("observation_count").GetInt32()} | {F(value.GetProperty("spearman_rank_correlation").GetDouble())} |");
        }
        builder.AppendLine();
        builder.AppendLine("These are empirical performance fingerprints. They are not assignments of Start, Speed, SlideControl, or any other game skill to real riders.");
        builder.AppendLine();
    }

    private static void LiteratureSection(StringBuilder builder)
    {
        builder.AppendLine("## Literature reaction context");
        builder.AppendLine();
        builder.AppendLine("Values were supplied explicitly in the task specification for Markowski M, Szczepan S, Zatoń M, Martin S, Michalik K, *The importance of reaction time to the starting signal on race results in elite motorcycle speedway racing*, PLOS ONE 18(1): e0281138 (2023), DOI 10.1371/journal.pone.0281138.");
        builder.AppendLine();
        builder.AppendLine("| Population / phase | Mean reaction time | Reported dispersion |");
        builder.AppendLine("|---|---:|---:|");
        builder.AppendLine("| Senior riders | 0.246 s | ±0.050 s |");
        builder.AppendLine("| Junior riders | 0.258 s | ±0.050 s |");
        builder.AppendLine("| Main phase | 0.255 s | ±0.048 s |");
        builder.AppendLine("| Knockout phase | 0.239 s | ±0.046 s |");
        builder.AppendLine("| Semifinals | 0.229 s | ±0.044 s |");
        builder.AppendLine();
        builder.AppendLine("Definition: time from lifting of the starting tape to the first forward movement of the motorcycle; reported measurement accuracy 0.01 s. Approximately 80 km/h in 2.4 s is context/sanity only. Approximately 0.10-0.12 s is future false-start-rule context only.");
        builder.AppendLine();
        builder.AppendLine("The PDF is not part of the repository. No values were inferred from it. These observations do not create a junior penalty, senior bonus, age modifier, gate multiplier, reaction cap, or calibration equality target.");
        builder.AppendLine();
    }

    private static void BalancedSection(
        StringBuilder builder,
        CalibrationSkillSweepResult sweep,
        CalibrationEvaluationReport evaluation)
    {
        builder.AppendLine("## Current production model: balanced fixture");
        builder.AppendLine();
        builder.AppendLine($"Production fixture: `Track.CreateStandingStartExample()`, HoldLane decisions, incident frequency 0, seed {CalibrationSkillSweep.FixedSeed}, dry weather, neutral setup (Gearing 0.5, TractionBias 0.5), perfect/neutral surface, and all six RiderSkills at 50. Skill 50 is not defined as an average PGEE rider.");
        builder.AppendLine();
        builder.AppendLine("| Rider | Reaction s | Movement s | TimeTo70 s | SpeedAt2s km/h | First curve km/h | L1 s | L2 s | L3 s | L4 s | Flying s | L1 penalty s | Vmax km/h | Distance m | Total s | Avg m/s | RunWide | Brake | Crash |");
        builder.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var rider in sweep.Riders)
        {
            builder.AppendLine($"| {rider.RiderId} | {F(rider.ReactionTimeSeconds)} | {F(rider.LaunchMovementTimeSeconds)} | {F(rider.TimeTo70KphSeconds)} | {F(NullableKph(rider.SpeedAtTwoSecondsMetersPerSecond))} | {F(NullableKph(rider.FirstCurveEntrySpeedMetersPerSecond))} | {F(rider.L1Seconds)} | {F(rider.L2Seconds)} | {F(rider.L3Seconds)} | {F(rider.L4Seconds)} | {F(rider.FlyingLapMedianSeconds)} | {F(rider.FirstLapPenaltySeconds)} | {F(Kph(rider.MaximumSpeedMetersPerSecond))} | {F(rider.TotalDistanceMeters)} | {F(rider.TotalTimeSeconds)} | {F(rider.AverageSpeedMetersPerSecond)} | {rider.RunWideCount} | {rider.BrakeCount} | {rider.CrashCount} |");
        }
        builder.AppendLine();
        AddCompactScenarioTable(builder, new[] { sweep }, new Dictionary<string, CalibrationEvaluationReport>(StringComparer.Ordinal) { [sweep.Scenario.ScenarioId] = evaluation });
    }

    private static void SweepSection(
        StringBuilder builder,
        string heading,
        IEnumerable<CalibrationSkillSweepResult> sweeps,
        IReadOnlyDictionary<string, CalibrationEvaluationReport> evaluations)
    {
        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        builder.AppendLine("The named skill varies through 0/25/50/75/100 while all other RiderSkills stay at 50. Values are four-rider medians; percentiles are positions in the real PGEE distributions.");
        builder.AppendLine();
        AddCompactScenarioTable(builder, sweeps, evaluations);
    }

    private static void CombinedSection(
        StringBuilder builder,
        IEnumerable<CalibrationSkillSweepResult> sweeps,
        IReadOnlyDictionary<string, CalibrationEvaluationReport> evaluations)
    {
        builder.AppendLine("## Combined 27-point sweep");
        builder.AppendLine();
        builder.AppendLine("Start, Speed, and SlideControl each take 25/50/75; TrackReading, PairRiding, and Adaptability remain 50.");
        builder.AppendLine();
        AddCompactScenarioTable(builder, sweeps, evaluations);
    }

    private static void AddCompactScenarioTable(
        StringBuilder builder,
        IEnumerable<CalibrationSkillSweepResult> sweeps,
        IReadOnlyDictionary<string, CalibrationEvaluationReport> evaluations)
    {
        builder.AppendLine("| Scenario | Reaction s | TimeTo70 s | Vmax km/h | Vmax pct | Avg m/s | Avg pct | L1 penalty s | L1 penalty pct | Heat spread s | Vmax spread km/h | RunWide | Brake | Crash |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var sweep in sweeps)
        {
            var riders = sweep.Riders;
            var evaluation = evaluations[sweep.Scenario.ScenarioId];
            var vmax = Median(riders.Select(item => (double?)Kph(item.MaximumSpeedMetersPerSecond)));
            var average = Median(riders.Select(item => (double?)item.AverageSpeedMetersPerSecond));
            var penalty = Median(riders.Select(item => (double?)item.FirstLapPenaltySeconds));
            builder.AppendLine($"| `{sweep.Scenario.ScenarioId}` | {F(Median(riders.Select(item => (double?)item.ReactionTimeSeconds)))} | {F(Median(riders.Select(item => (double?)item.TimeTo70KphSeconds)))} | {F(vmax)} | {F(P50Rank(evaluation, "pge_clean_vmax"))} | {F(average)} | {F(P50Rank(evaluation, "pge_clean_average_speed"))} | {F(penalty)} | {F(P50Rank(evaluation, "pge_clean_l1_penalty"))} | {F(Spread(riders.Select(item => (double?)item.TotalTimeSeconds)))} | {F(Spread(riders.Select(item => (double?)Kph(item.MaximumSpeedMetersPerSecond))))} | {riders.Sum(item => item.RunWideCount)} | {riders.Sum(item => item.BrakeCount)} | {riders.Sum(item => item.CrashCount)} |");
        }
        builder.AppendLine();
    }

    private static void ModelComparisonSection(StringBuilder builder, CalibrationEvaluationReport evaluation)
    {
        builder.AppendLine("## Model vs real percentile positions");
        builder.AppendLine();
        builder.AppendLine("Balanced-fixture P50 comparisons are component-wise. There is deliberately no combined accuracy score.");
        builder.AppendLine();
        builder.AppendLine("| Metric | Class | Unit | Real P50 | Model P50 | Signed gap | Relative gap | Model real-percentile |");
        builder.AppendLine("|---|---|---|---:|---:|---:|---:|---:|");
        foreach (var component in evaluation.Components.Where(item => item.NumericallyCompared))
        {
            var comparison = component.QuantileComparisons.Single(item => item.Percentile == 50);
            builder.AppendLine($"| {Labels[component.Definition.MetricId]} | {component.Definition.Comparability} | {component.Definition.Unit} | {F(comparison.RealValue)} | {F(comparison.SimulationValue)} | {F(comparison.SignedDifference)} | {F(comparison.RelativeDifference is { } relative ? relative * 100d : null)}% | {F(comparison.SimulationValueRealEmpiricalPercentile)} |");
        }
        builder.AppendLine();
    }

    private static void ComparabilitySection(StringBuilder builder)
    {
        builder.AppendLine("## Metrics not yet directly comparable");
        builder.AppendLine();
        builder.AppendLine("- **ComparableEnvelope:** Vmax, average speed, L1 penalty, and four-rider spreads. They are envelopes, not equality targets or caps.");
        builder.AppendLine("- **ContextOnlyUntilTrackGeometry:** absolute heat/L1/L2/L3/L4/flying times and total distance. The example simulation track is not a fitted representation of every PGEE track.");
        builder.AppendLine("- **UnsupportedNumericByCurrentSource:** individual reaction time, SpeedAt2s, and first-curve speed. Source `speed_2s` and `curve_speed` fields are gate rankings, not physical rider speeds. Reaction has separate literature context only.");
        builder.AppendLine();
    }

    private static void GapSection(StringBuilder builder, CalibrationEvaluationReport evaluation)
    {
        builder.AppendLine("## Main gaps detected");
        builder.AppendLine();
        var comparable = evaluation.Components
            .Where(item => item.NumericallyCompared && item.Definition.Comparability == CalibrationComparability.ComparableEnvelope)
            .Select(item => (item.Definition, Comparison: item.QuantileComparisons.Single(value => value.Percentile == 50)))
            .OrderBy(item => item.Comparison.SimulationValueRealEmpiricalPercentile)
            .ToArray();
        foreach (var item in comparable)
        {
            var position = item.Comparison.SimulationValueRealEmpiricalPercentile;
            var assessment = position < 10d ? "below the real P10 envelope" : position > 90d ? "above the real P90 envelope" : "inside the real P10-P90 envelope";
            builder.AppendLine($"- {Labels[item.Definition.MetricId]}: balanced model P50 {F(item.Comparison.SimulationValue)} {item.Definition.Unit}, real P50 {F(item.Comparison.RealValue)} {item.Definition.Unit}, percentile {F(position)} — {assessment}.");
        }
        builder.AppendLine();
        builder.AppendLine("This is a measurement result for review, not a recommendation or an automatically selected set of constants. Physics calibration remains future work.");
        builder.AppendLine();
    }

    private static void ValidateLiteratureProvenance(JsonElement literature)
    {
        var provenance = literature.GetProperty("observations")[0]
            .GetProperty("source").GetProperty("provenance").GetString();
        if (provenance is null
            || !provenance.Contains("supplied directly", StringComparison.Ordinal)
            || !provenance.Contains("PDF is not included", StringComparison.Ordinal))
            throw new InvalidDataException("Literature provenance must identify task-supplied values and the absent PDF.");
    }

    private static double P50Rank(CalibrationEvaluationReport report, string metricId)
        => report.Components.Single(item => item.Definition.MetricId == metricId)
            .QuantileComparisons.Single(item => item.Percentile == 50)
            .SimulationValueRealEmpiricalPercentile;

    private static double? Median(IEnumerable<double?> values)
    {
        var ordered = values.Where(value => value.HasValue).Select(value => value!.Value).Order().ToArray();
        return ordered.Length == 0 ? null : CalibrationDistribution.LinearQuantile(ordered, 0.5d);
    }

    private static double? Spread(IEnumerable<double?> values)
    {
        var actual = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return actual.Length == 0 ? null : actual.Max() - actual.Min();
    }

    private static double Kph(float metersPerSecond)
        => CalibrationUnits.MetersPerSecondToKph(metersPerSecond);

    private static double? NullableKph(float? metersPerSecond)
        => metersPerSecond is { } speed ? CalibrationUnits.MetersPerSecondToKph(speed) : null;

    private static string F(double? value)
        => value is null ? "n/a" : value.Value.ToString("0.######", CultureInfo.InvariantCulture);
}
