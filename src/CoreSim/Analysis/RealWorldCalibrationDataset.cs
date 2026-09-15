using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public sealed record RealWorldRiderHeatObservation(
    string MatchId,
    DateTimeOffset? Date,
    int? Season,
    string League,
    string SourceTrackLabel,
    int? HeatNumber,
    string HeatUid,
    string RiderId,
    string Gate,
    int? Points,
    int? ResultPosition,
    string Motorbike,
    bool TelemetryPresent,
    bool ResultPresent,
    double? HeatTimeSeconds,
    double? L1TimeSeconds,
    double? L2TimeSeconds,
    double? L3TimeSeconds,
    double? L4TimeSeconds,
    double? MaximumSpeedKph,
    double? TotalDistanceMeters,
    double? FlyingLapMedianSeconds,
    double? FirstLapPenaltySeconds,
    double? AverageSpeedMetersPerSecond,
    bool CompleteTelemetry,
    bool CleanPhysics,
    bool Eventful,
    bool AuditOnly,
    string ExclusionReason,
    string DatasetSplit,
    int? DevelopmentFold,
    string GateRankPosition,
    string GateRankSpeedAtTwoSeconds,
    string GateRankCurveSpeed);

/// <summary>
/// Validated real-world observation snapshot.  Parsing accepts text so CoreSim
/// remains independent of the file system and of the Python preparation tool.
/// </summary>
public sealed class RealWorldCalibrationDataset
{
    public const string SourceName = "PGE Ekstraliga public Firestore telemetry";
    public const string SourceVersion = "pge-v1";

    private static readonly IReadOnlyDictionary<string, CalibrationMetricDefinition> Definitions =
        new ReadOnlyDictionary<string, CalibrationMetricDefinition>(
            new Dictionary<string, CalibrationMetricDefinition>(StringComparer.Ordinal)
            {
                ["pge_clean_vmax"] = Definition(
                    "pge_clean_vmax", "km/h",
                    "maximum recorded rider speed in one telemetry rider-heat record",
                    CalibrationComparability.ComparableEnvelope),
                ["pge_clean_average_speed"] = Definition(
                    "pge_clean_average_speed", "m/s",
                    "recorded total distance divided by recorded heat time",
                    CalibrationComparability.ComparableEnvelope),
                ["pge_clean_l1_penalty"] = Definition(
                    "pge_clean_l1_penalty", "s",
                    "L1 minus median(L2, L3, L4)",
                    CalibrationComparability.ComparableEnvelope),
                ["pge_clean_heat_time"] = Definition(
                    "pge_clean_heat_time", "s", "recorded four-lap rider heat time",
                    CalibrationComparability.ContextOnlyUntilTrackGeometry),
                ["pge_clean_l1_time"] = Definition(
                    "pge_clean_l1_time", "s", "recorded first-lap rider time",
                    CalibrationComparability.ContextOnlyUntilTrackGeometry),
                ["pge_clean_l2_time"] = Definition(
                    "pge_clean_l2_time", "s", "recorded second-lap rider time",
                    CalibrationComparability.ContextOnlyUntilTrackGeometry),
                ["pge_clean_l3_time"] = Definition(
                    "pge_clean_l3_time", "s", "recorded third-lap rider time",
                    CalibrationComparability.ContextOnlyUntilTrackGeometry),
                ["pge_clean_l4_time"] = Definition(
                    "pge_clean_l4_time", "s", "recorded fourth-lap rider time",
                    CalibrationComparability.ContextOnlyUntilTrackGeometry),
                ["pge_clean_flying_lap_median"] = Definition(
                    "pge_clean_flying_lap_median", "s", "median of L2, L3 and L4",
                    CalibrationComparability.ContextOnlyUntilTrackGeometry),
                ["pge_clean_total_distance"] = Definition(
                    "pge_clean_total_distance", "m", "recorded rider distance in one heat attempt",
                    CalibrationComparability.ContextOnlyUntilTrackGeometry),
                ["pge_four_rider_heat_time_spread"] = Definition(
                    "pge_four_rider_heat_time_spread", "s", "maximum minus minimum heat time in a four-rider CleanPhysics attempt",
                    CalibrationComparability.ComparableEnvelope),
                ["pge_four_rider_vmax_spread"] = Definition(
                    "pge_four_rider_vmax_spread", "km/h", "maximum minus minimum Vmax in a four-rider CleanPhysics attempt",
                    CalibrationComparability.ComparableEnvelope),
                ["pge_four_rider_l1_spread"] = Definition(
                    "pge_four_rider_l1_spread", "s", "maximum minus minimum L1 in a four-rider CleanPhysics attempt",
                    CalibrationComparability.ComparableEnvelope),
                ["pge_four_rider_average_speed_spread"] = Definition(
                    "pge_four_rider_average_speed_spread", "m/s", "maximum minus minimum average speed in a four-rider CleanPhysics attempt",
                    CalibrationComparability.ComparableEnvelope),
                ["pge_individual_reaction_time"] = Definition(
                    "pge_individual_reaction_time", "s", "individual reaction time not present in PGE telemetry",
                    CalibrationComparability.UnsupportedNumericByCurrentSource),
                ["pge_individual_speed_at_2s"] = Definition(
                    "pge_individual_speed_at_2s", "km/h", "source speed_2s is a gate-ranking flag, not a rider speed",
                    CalibrationComparability.UnsupportedNumericByCurrentSource),
                ["pge_individual_first_curve_speed"] = Definition(
                    "pge_individual_first_curve_speed", "km/h", "source curve_speed is a gate-ranking flag, not a rider speed",
                    CalibrationComparability.UnsupportedNumericByCurrentSource),
            });

    private readonly ReadOnlyCollection<RealWorldRiderHeatObservation> _rows;
    private readonly IReadOnlyDictionary<string, CalibrationDistribution> _distributions;

    public IReadOnlyList<RealWorldRiderHeatObservation> Rows => _rows;
    public IReadOnlyDictionary<string, CalibrationDistribution> Distributions => _distributions;
    public IReadOnlyDictionary<string, CalibrationMetricDefinition> MetricDefinitions => Definitions;

    private RealWorldCalibrationDataset(IEnumerable<RealWorldRiderHeatObservation> rows)
    {
        _rows = Array.AsReadOnly(rows.ToArray());
        if (_rows.Count == 0)
            throw new ArgumentException("The real-world dataset cannot be empty.", nameof(rows));
        _distributions = BuildDistributions(_rows);
    }

    public static RealWorldCalibrationDataset ParseCsv(string csv)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csv);
        var records = ParseCsvRecords(csv);
        if (records.Count < 2)
            throw new FormatException("The normalized CSV must contain a header and at least one data row.");
        var header = records[0];
        var indices = header
            .Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        foreach (var required in RequiredColumns)
        {
            if (!indices.ContainsKey(required))
                throw new FormatException($"Missing normalized dataset column '{required}'.");
        }

        string Cell(IReadOnlyList<string> record, string name)
            => indices[name] < record.Count ? record[indices[name]] : string.Empty;

        var rows = new List<RealWorldRiderHeatObservation>(records.Count - 1);
        foreach (var record in records.Skip(1))
        {
            if (record.Count == 1 && string.IsNullOrEmpty(record[0]))
                continue;
            rows.Add(new RealWorldRiderHeatObservation(
                Cell(record, "match_id"),
                ParseDate(Cell(record, "date")),
                ParseNullableInt(Cell(record, "season")),
                Cell(record, "league"),
                Cell(record, "source_track_label"),
                ParseNullableInt(Cell(record, "heat_no")),
                Cell(record, "heat_uid"),
                Cell(record, "rider_id"),
                Cell(record, "gate"),
                ParseNullableInt(Cell(record, "points")),
                ParseNullableInt(Cell(record, "result_position")),
                Cell(record, "motorbike"),
                ParseFlag(Cell(record, "telemetry_present")),
                ParseFlag(Cell(record, "result_present")),
                ParseNullableDouble(Cell(record, "heat_time_s")),
                ParseNullableDouble(Cell(record, "l1_time_s")),
                ParseNullableDouble(Cell(record, "l2_time_s")),
                ParseNullableDouble(Cell(record, "l3_time_s")),
                ParseNullableDouble(Cell(record, "l4_time_s")),
                ParseNullableDouble(Cell(record, "max_speed_kph")),
                ParseNullableDouble(Cell(record, "total_distance_m")),
                ParseNullableDouble(Cell(record, "flying_lap_median_s")),
                ParseNullableDouble(Cell(record, "l1_penalty_s")),
                ParseNullableDouble(Cell(record, "average_speed_mps")),
                ParseFlag(Cell(record, "complete_telemetry")),
                ParseFlag(Cell(record, "clean_physics")),
                ParseFlag(Cell(record, "eventful")),
                ParseFlag(Cell(record, "audit_only")),
                Cell(record, "exclusion_reason"),
                Cell(record, "dataset_split"),
                ParseNullableInt(Cell(record, "development_fold")),
                Cell(record, "gate_rank_position"),
                Cell(record, "gate_rank_speed_2s"),
                Cell(record, "gate_rank_curve_speed")));
        }

        ValidateRows(rows);
        return new RealWorldCalibrationDataset(rows);
    }

    /// <summary>
    /// Creates an independently validated immutable dataset from one exact
    /// season/source-label selector. No aliases, team inference or mutation are
    /// applied, and all distributions are rebuilt from the selected rows.
    /// </summary>
    public RealWorldCalibrationDataset FilterByExactVenue(int season, string sourceTrackLabel)
    {
        if (season <= 0)
            throw new ArgumentOutOfRangeException(nameof(season));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTrackLabel);
        var selected = _rows.Where(row => row.Season == season
            && StringComparer.Ordinal.Equals(row.SourceTrackLabel, sourceTrackLabel)).ToArray();
        if (selected.Length == 0)
        {
            throw new InvalidOperationException(
                $"No observations match exact season {season} and source_track_label '{sourceTrackLabel}'.");
        }

        ValidateRows(selected);
        return new RealWorldCalibrationDataset(selected);
    }

    private static readonly string[] RequiredColumns =
    {
        "match_id", "date", "season", "league", "source_track_label", "heat_no", "heat_uid", "rider_id",
        "gate", "points", "result_position", "motorbike", "telemetry_present", "result_present", "heat_time_s",
        "l1_time_s", "l2_time_s", "l3_time_s", "l4_time_s", "max_speed_kph", "total_distance_m",
        "flying_lap_median_s", "l1_penalty_s", "average_speed_mps", "complete_telemetry", "clean_physics",
        "eventful", "audit_only", "exclusion_reason", "dataset_split", "development_fold", "gate_rank_position",
        "gate_rank_speed_2s", "gate_rank_curve_speed",
    };

    private static CalibrationMetricDefinition Definition(
        string id,
        string unit,
        string definition,
        CalibrationComparability comparability)
        => new(
            id,
            unit,
            definition,
            SourceName,
            SourceVersion,
            id.StartsWith("pge_four_rider_", StringComparison.Ordinal)
                ? "PGEE four-rider CleanPhysics heat attempts"
                : "PGEE CleanPhysics rider-heat records",
            comparability,
            comparability == CalibrationComparability.UnsupportedNumericByCurrentSource
                ? "UnsupportedBySource"
                : "ObservedTelemetryConservativeSubset",
            "Real telemetry defines a distribution/performance envelope, not a hard limit.");

    private static IReadOnlyDictionary<string, CalibrationDistribution> BuildDistributions(
        IReadOnlyList<RealWorldRiderHeatObservation> rows)
    {
        var clean = rows.Where(row => row.CleanPhysics).ToArray();
        if (clean.Length == 0)
            throw new ArgumentException("The dataset contains no CleanPhysics observations.", nameof(rows));

        var distributions = new Dictionary<string, CalibrationDistribution>(StringComparer.Ordinal);
        Add("pge_clean_vmax", clean.Select(row => row.MaximumSpeedKph));
        Add("pge_clean_average_speed", clean.Select(row => row.AverageSpeedMetersPerSecond));
        Add("pge_clean_l1_penalty", clean.Select(row => row.FirstLapPenaltySeconds));
        Add("pge_clean_heat_time", clean.Select(row => row.HeatTimeSeconds));
        Add("pge_clean_l1_time", clean.Select(row => row.L1TimeSeconds));
        Add("pge_clean_l2_time", clean.Select(row => row.L2TimeSeconds));
        Add("pge_clean_l3_time", clean.Select(row => row.L3TimeSeconds));
        Add("pge_clean_l4_time", clean.Select(row => row.L4TimeSeconds));
        Add("pge_clean_flying_lap_median", clean.Select(row => row.FlyingLapMedianSeconds));
        Add("pge_clean_total_distance", clean.Select(row => row.TotalDistanceMeters));

        var fourRiderAttempts = clean
            .GroupBy(row => (row.MatchId, row.HeatUid))
            .Where(group => group.Count() == 4 && group.Select(row => row.RiderId).Distinct().Count() == 4)
            .Select(group => group.ToArray())
            .ToArray();
        Add("pge_four_rider_heat_time_spread", fourRiderAttempts.Select(group => Spread(group.Select(row => row.HeatTimeSeconds))));
        Add("pge_four_rider_vmax_spread", fourRiderAttempts.Select(group => Spread(group.Select(row => row.MaximumSpeedKph))));
        Add("pge_four_rider_l1_spread", fourRiderAttempts.Select(group => Spread(group.Select(row => row.L1TimeSeconds))));
        Add("pge_four_rider_average_speed_spread", fourRiderAttempts.Select(group => Spread(group.Select(row => row.AverageSpeedMetersPerSecond))));
        return new ReadOnlyDictionary<string, CalibrationDistribution>(distributions);

        void Add(string id, IEnumerable<double?> values)
        {
            var materialized = values.Select(value => value
                ?? throw new InvalidOperationException($"CleanPhysics metric '{id}' cannot be null."));
            distributions[id] = new CalibrationDistribution(Definitions[id], materialized);
        }
    }

    private static double? Spread(IEnumerable<double?> values)
    {
        var actual = values.Select(value => value
            ?? throw new InvalidOperationException("A CleanPhysics spread value cannot be null.")).ToArray();
        return actual.Max() - actual.Min();
    }

    private static void ValidateRows(IReadOnlyList<RealWorldRiderHeatObservation> rows)
    {
        foreach (var row in rows)
        {
            if (row.League != "PGEE")
                throw new FormatException("The normalized PGEE snapshot must use exact league == 'PGEE'.");
            if (string.IsNullOrWhiteSpace(row.MatchId) || string.IsNullOrWhiteSpace(row.HeatUid)
                || row.CompleteTelemetry && string.IsNullOrWhiteSpace(row.RiderId))
                throw new FormatException("match_id and heat_uid are required; complete rows also require rider_id.");
            var positive = new[]
            {
                row.HeatTimeSeconds, row.L1TimeSeconds, row.L2TimeSeconds, row.L3TimeSeconds, row.L4TimeSeconds,
                row.MaximumSpeedKph, row.TotalDistanceMeters,
            };
            var lapSum = row.L1TimeSeconds.GetValueOrDefault() + row.L2TimeSeconds.GetValueOrDefault()
                         + row.L3TimeSeconds.GetValueOrDefault() + row.L4TimeSeconds.GetValueOrDefault();
            var expectedComplete = row.TelemetryPresent && row.ResultPresent
                && positive.All(value => value is > 0d)
                && row.Points is >= 0 and <= 3
                && Math.Abs(row.HeatTimeSeconds!.Value - lapSum) <= 0.05d + 1e-9;
            if (row.CompleteTelemetry != expectedComplete)
                throw new FormatException("CompleteTelemetry does not match the required source rule.");
            var expectedClean = expectedComplete && HasSteadyFlyingLaps(row);
            if (row.CleanPhysics != expectedClean)
                throw new FormatException("CleanPhysics does not match the +/-10% flying-lap rule.");
            if (row.Eventful != (row.CompleteTelemetry && !row.CleanPhysics))
                throw new FormatException("Eventful must mean complete but outside the steady-lap subset.");
            if (row.AuditOnly == row.CompleteTelemetry)
                throw new FormatException("AuditOnly must be the inverse of CompleteTelemetry.");
            if (row.DatasetSplit == "DEVELOPMENT" && row.DevelopmentFold is not (>= 0 and <= 4)
                || row.DatasetSplit == "FINAL_TEST" && row.DevelopmentFold is not null
                || row.DatasetSplit.Length == 0 && row.DevelopmentFold is not null
                || row.DatasetSplit is not ("" or "DEVELOPMENT" or "FINAL_TEST"))
                throw new FormatException("Dataset split/fold values are inconsistent.");
            if (row.CompleteTelemetry)
                ValidateComplete(row);
        }

        foreach (var match in rows.GroupBy(row => row.MatchId))
        {
            if (match.Select(row => row.DatasetSplit).Distinct(StringComparer.Ordinal).Count() != 1)
                throw new FormatException($"Match '{match.Key}' leaks across dataset splits.");
            if (match.Select(row => row.DevelopmentFold).Distinct().Count() != 1)
                throw new FormatException($"Match '{match.Key}' leaks across development folds.");
        }
    }

    private static bool HasSteadyFlyingLaps(RealWorldRiderHeatObservation row)
    {
        var laps = new[] { row.L2TimeSeconds!.Value, row.L3TimeSeconds!.Value, row.L4TimeSeconds!.Value };
        var median = CalibrationDistribution.LinearQuantile(laps.Order().ToArray(), 0.5d);
        return laps.All(lap => Math.Abs(lap - median) / median <= 0.10d + 1e-12);
    }

    private static void ValidateComplete(RealWorldRiderHeatObservation row)
    {
        var positive = new[]
        {
            row.HeatTimeSeconds, row.L1TimeSeconds, row.L2TimeSeconds, row.L3TimeSeconds, row.L4TimeSeconds,
            row.MaximumSpeedKph, row.TotalDistanceMeters,
        };
        if (!row.TelemetryPresent || !row.ResultPresent || positive.Any(value => value is null or <= 0d)
            || row.Points is null or < 0 or > 3)
            throw new FormatException("CompleteTelemetry row violates required presence, positive metrics or points.");
        var lapSum = row.L1TimeSeconds!.Value + row.L2TimeSeconds!.Value
                     + row.L3TimeSeconds!.Value + row.L4TimeSeconds!.Value;
        if (Math.Abs(row.HeatTimeSeconds!.Value - lapSum) > 0.05d + 1e-9)
            throw new FormatException("CompleteTelemetry heat time differs from its four laps by more than 0.05 s.");
        var flying = CalibrationDistribution.LinearQuantile(
            new[] { row.L2TimeSeconds.Value, row.L3TimeSeconds.Value, row.L4TimeSeconds.Value }.Order().ToArray(),
            0.5);
        if (Math.Abs(row.FlyingLapMedianSeconds!.Value - flying) > 1e-9
            || Math.Abs(row.FirstLapPenaltySeconds!.Value - (row.L1TimeSeconds.Value - flying)) > 1e-9
            || Math.Abs(row.AverageSpeedMetersPerSecond!.Value - row.TotalDistanceMeters!.Value / row.HeatTimeSeconds!.Value) > 1e-8)
            throw new FormatException("CompleteTelemetry derived metrics are inconsistent.");
    }

    private static int? ParseNullableInt(string value)
        => string.IsNullOrEmpty(value)
            ? null
            : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new FormatException($"'{value}' is not an invariant integer.");

    private static double? ParseNullableDouble(string value)
        => string.IsNullOrEmpty(value)
            ? null
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)
                ? parsed
                : throw new FormatException($"'{value}' is not a finite invariant number.");

    private static DateTimeOffset? ParseDate(string value)
        => string.IsNullOrEmpty(value)
            ? null
            : DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : throw new FormatException($"'{value}' is not an ISO date.");

    private static bool ParseFlag(string value)
        => value switch
        {
            "0" => false,
            "1" => true,
            _ => throw new FormatException($"'{value}' is not a 0/1 flag."),
        };

    private static List<List<string>> ParseCsvRecords(string input)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < input.Length && input[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }
                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = new List<string>();
                    break;
                case '\r':
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }
        if (quoted)
            throw new FormatException("Unterminated quoted CSV field.");
        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
        return records;
    }
}
