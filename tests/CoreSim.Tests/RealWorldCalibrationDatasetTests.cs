using CoreSim.Analysis;
using System.Text.Json;
using Xunit;

namespace CoreSim.Tests;

public sealed class RealWorldCalibrationDatasetTests
{
    private static readonly Lazy<RealWorldCalibrationDataset> Snapshot = new(() =>
        RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "data", "calibration", "pge", "v1", "pge_rider_heats.csv"))));

    [Fact]
    public void CurrentSnapshotCountsAreExactForAppliedRules()
    {
        var rows = Snapshot.Value.Rows;
        Assert.Equal(6373, rows.Count);
        Assert.Equal(95, rows.Select(item => item.MatchId).Distinct().Count());
        Assert.Equal(5410, rows.Count(item => item.CompleteTelemetry));
        Assert.Equal(5328, rows.Count(item => item.CleanPhysics));
        Assert.Equal(82, rows.Count(item => item.Eventful));
        Assert.Equal(963, rows.Count(item => item.AuditOnly));
        Assert.Equal(97, rows.Where(item => item.CompleteTelemetry).Select(item => item.RiderId).Distinct().Count());
        Assert.Equal(1174, rows.Where(item => item.CleanPhysics)
            .GroupBy(item => (item.MatchId, item.HeatUid))
            .Count(group => group.Count() == 4 && group.Select(item => item.RiderId).Distinct().Count() == 4));
    }

    [Fact]
    public void SnapshotDistributionsMatchLinearInterpolationOracle()
    {
        AssertQuantiles("pge_clean_vmax", 109.7, 114.8, 119.4, 1e-9);
        AssertQuantiles("pge_clean_heat_time", 60.889, 64.688, 68.345, 1e-9);
        AssertQuantiles("pge_clean_average_speed", 22.02397404651, 22.98045231815, 23.96824567771, 1e-10);
        AssertQuantiles("pge_clean_flying_lap_median", 14.71, 15.66, 16.52, 1e-9);
        AssertQuantiles("pge_clean_l1_penalty", 1.82, 2.05, 2.32, 1e-9);
    }

    [Fact]
    public void WithinHeatSpreadMediansMatchSnapshot()
    {
        Assert.Equal(1.5285, Snapshot.Value.Distributions["pge_four_rider_heat_time_spread"].Quantiles.P50, 6);
        Assert.Equal(4.5, Snapshot.Value.Distributions["pge_four_rider_vmax_spread"].Quantiles.P50, 6);
        Assert.Equal(0.53, Snapshot.Value.Distributions["pge_four_rider_l1_spread"].Quantiles.P50, 6);
        Assert.Equal(0.92709361125, Snapshot.Value.Distributions["pge_four_rider_average_speed_spread"].Quantiles.P50, 9);
    }

    [Fact]
    public void FourRiderResidualsSumToZeroWithinEveryHeat()
    {
        var attempts = Snapshot.Value.Rows.Where(item => item.CleanPhysics)
            .GroupBy(item => (item.MatchId, item.HeatUid))
            .Where(group => group.Count() == 4 && group.Select(item => item.RiderId).Distinct().Count() == 4);
        foreach (var attempt in attempts)
        {
            AssertResidualSum(attempt.Select(item => item.HeatTimeSeconds!.Value));
            AssertResidualSum(attempt.Select(item => item.MaximumSpeedKph!.Value));
            AssertResidualSum(attempt.Select(item => item.L1TimeSeconds!.Value));
            AssertResidualSum(attempt.Select(item => item.AverageSpeedMetersPerSecond!.Value));
        }
    }

    [Fact]
    public void SummaryRetainsAuditedSourceCountsAndResidualSampleSize()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "data", "calibration", "pge", "v1", "summary.json")));
        var root = document.RootElement;
        var source = root.GetProperty("source_counts");
        Assert.Equal(100, source.GetProperty("downloaded_matches").GetInt32());
        Assert.Equal(6021, source.GetProperty("telemetry_detail_records").GetInt32());
        Assert.Equal(6772, source.GetProperty("heats_csv_rows").GetInt32());
        Assert.Equal(6789, source.GetProperty("telemetry_full_csv_rows").GetInt32());
        Assert.Equal(4696, root.GetProperty("rider_relative").GetProperty("observation_count").GetInt32());
    }

    [Fact]
    public void MatchLevelSplitHasNoLeakageAndNewestCeilingTwentyPercentIsFinal()
    {
        var eligible = Snapshot.Value.Rows.Where(item => item.CompleteTelemetry)
            .GroupBy(item => item.MatchId)
            .Select(group => group.First())
            .OrderBy(item => item.Date)
            .ThenBy(item => int.Parse(item.MatchId))
            .ToArray();
        Assert.Equal(93, eligible.Length);
        Assert.Equal(19, eligible.Count(item => item.DatasetSplit == "FINAL_TEST"));
        Assert.Equal(74, eligible.Count(item => item.DatasetSplit == "DEVELOPMENT"));
        var expectedFinal = eligible[^19..].Select(item => item.MatchId).ToHashSet();
        Assert.True(expectedFinal.SetEquals(
            eligible.Where(item => item.DatasetSplit == "FINAL_TEST").Select(item => item.MatchId)));

        foreach (var match in Snapshot.Value.Rows.GroupBy(item => item.MatchId))
        {
            Assert.Single(match.Select(item => item.DatasetSplit).Distinct());
            Assert.Single(match.Select(item => item.DevelopmentFold).Distinct());
        }
    }

    [Fact]
    public void DevelopmentFoldCountsAreDeterministicAndMatchLevel()
    {
        var development = Snapshot.Value.Rows.Where(item => item.DatasetSplit == "DEVELOPMENT").ToArray();
        Assert.All(development, item => Assert.NotNull(item.DevelopmentFold));
        var folds = development
            .GroupBy(item => item.DevelopmentFold!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.MatchId).Distinct().Count());
        Assert.Equal(17, folds[0]);
        Assert.Equal(14, folds[1]);
        Assert.Equal(14, folds[2]);
        Assert.Equal(15, folds[3]);
        Assert.Equal(14, folds[4]);
    }

    [Fact]
    public void AuditRowsRetainMissingValuesInsteadOfConvertingThemToZero()
    {
        var row = Snapshot.Value.Rows.First(item => item.AuditOnly && item.HeatTimeSeconds is null);
        Assert.Null(row.HeatTimeSeconds);
        Assert.False(row.CleanPhysics);
    }

    [Fact]
    public void ZeroPointFinishersRemainEligible()
    {
        Assert.Contains(Snapshot.Value.Rows, item => item.CleanPhysics && item.Points == 0);
    }

    [Fact]
    public void RestartedHeatAttemptsUseHeatUidNotProgramNumber()
    {
        var attempts = Snapshot.Value.Rows.Where(item => item.MatchId == "7734" && item.HeatNumber == 2)
            .Select(item => item.HeatUid).Distinct().Order().ToArray();
        Assert.Equal(new[] { "7734_2_0", "7734_2_1" }, attempts);
    }

    [Fact]
    public void GateRankingMetadataIsNotExposedAsPhysicalSpeed()
    {
        var row = Snapshot.Value.Rows.First(item => !string.IsNullOrEmpty(item.GateRankSpeedAtTwoSeconds));
        Assert.True(row.GateRankSpeedAtTwoSeconds is "0" or "1");
        Assert.Equal(CalibrationComparability.UnsupportedNumericByCurrentSource,
            Snapshot.Value.MetricDefinitions["pge_individual_speed_at_2s"].Comparability);
        Assert.Equal(CalibrationComparability.UnsupportedNumericByCurrentSource,
            Snapshot.Value.MetricDefinitions["pge_individual_first_curve_speed"].Comparability);
    }

    [Fact]
    public void UnitHelpersArePureRoundTripConversions()
    {
        Assert.Equal(25d, CalibrationUnits.KphToMetersPerSecond(90d), 12);
        Assert.Equal(90d, CalibrationUnits.MetersPerSecondToKph(25d), 12);
    }

    private static void AssertQuantiles(string metricId, double p10, double p50, double p90, double tolerance)
    {
        var quantiles = Snapshot.Value.Distributions[metricId].Quantiles;
        Assert.InRange(Math.Abs(quantiles.P10 - p10), 0, tolerance);
        Assert.InRange(Math.Abs(quantiles.P50 - p50), 0, tolerance);
        Assert.InRange(Math.Abs(quantiles.P90 - p90), 0, tolerance);
    }

    private static void AssertResidualSum(IEnumerable<double> values)
    {
        var materialized = values.ToArray();
        var mean = materialized.Average();
        Assert.InRange(Math.Abs(materialized.Sum(value => value - mean)), 0, 1e-10);
    }
}
