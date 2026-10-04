using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class RealWorldCalibrationEvaluatorTests
{
    private static RealWorldCalibrationDataset Snapshot()
        => RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "data", "calibration", "pge", "v1", "pge_rider_heats.csv")));

    [Fact]
    public void LinearQuantileIsDeterministicInterpolation()
    {
        Assert.Equal(2.5, CalibrationDistribution.LinearQuantile(new[] { 0d, 10d }, 0.25), 12);
        Assert.Equal(5, CalibrationDistribution.LinearQuantile(new[] { 0d, 10d }, 0.50), 12);
    }

    [Fact]
    public void DistributionRejectsEmptyDataset()
    {
        Assert.Throws<ArgumentException>(() => new CalibrationDistribution(Definition(), Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => CalibrationDistribution.EmpiricalPercentileRank(Array.Empty<double>(), 1));
    }

    [Fact]
    public void OneValueAndOutsidePercentileRanksAreDefined()
    {
        Assert.Equal(50, CalibrationDistribution.EmpiricalPercentileRank(new[] { 7d }, 7), 12);
        Assert.Equal(0, CalibrationDistribution.EmpiricalPercentileRank(new[] { 7d }, 6), 12);
        Assert.Equal(100, CalibrationDistribution.EmpiricalPercentileRank(new[] { 7d }, 8), 12);
    }

    [Fact]
    public void DuplicateValuesUseMidrankPercentile()
    {
        Assert.Equal(50, CalibrationDistribution.EmpiricalPercentileRank(new[] { 1d, 2d, 2d, 4d }, 2), 12);
    }

    [Fact]
    public void EvaluatorReportsFiveQuantilesAndSignedDifferences()
    {
        var report = RealWorldCalibrationEvaluator.Evaluate(
            Snapshot(),
            new SimulationCalibrationResult("test", new[]
            {
                new SimulationMetricSeries("pge_clean_vmax", "km/h", new[] { 90d, 115d, 125d }),
            }));
        var component = Assert.Single(report.Components);
        Assert.True(component.NumericallyCompared);
        Assert.Equal(new[] { 10, 25, 50, 75, 90 }, component.QuantileComparisons.Select(item => item.Percentile));
        Assert.All(component.QuantileComparisons, item =>
            Assert.Equal(item.SimulationValue - item.RealValue, item.SignedDifference, 12));
    }

    [Fact]
    public void UnitMismatchIsRejected()
    {
        var simulation = new SimulationCalibrationResult("bad-unit", new[]
        {
            new SimulationMetricSeries("pge_clean_vmax", "m/s", new[] { 30d }),
        });
        Assert.Throws<ArgumentException>(() => RealWorldCalibrationEvaluator.Evaluate(Snapshot(), simulation));
    }

    [Fact]
    public void UnsupportedMetricsAreLabelledAndNotCompared()
    {
        var report = RealWorldCalibrationEvaluator.Evaluate(
            Snapshot(),
            new SimulationCalibrationResult("unsupported", new[]
            {
                new SimulationMetricSeries("pge_individual_speed_at_2s", "km/h", new[] { 80d }),
            }));
        var component = Assert.Single(report.Components);
        Assert.Equal(CalibrationComparability.UnsupportedNumericByCurrentSource, component.Definition.Comparability);
        Assert.False(component.NumericallyCompared);
        Assert.Empty(component.QuantileComparisons);
    }

    [Fact]
    public void AbsoluteHeatTimeIsClearlyContextOnly()
    {
        var report = RealWorldCalibrationEvaluator.Evaluate(
            Snapshot(),
            new SimulationCalibrationResult("context", new[]
            {
                new SimulationMetricSeries("pge_clean_heat_time", "s", new[] { 64d }),
            }));
        var component = Assert.Single(report.Components);
        Assert.Equal(CalibrationComparability.ContextOnlyUntilTrackGeometry, component.Definition.Comparability);
        Assert.Contains("context only", component.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportHasNoMagicOverallScore()
    {
        Assert.DoesNotContain(typeof(CalibrationEvaluationReport).GetProperties(), property =>
            property.Name.Contains("Overall", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Accuracy", StringComparison.OrdinalIgnoreCase));
    }

    private static CalibrationMetricDefinition Definition()
        => new("test", "s", "test", "test", "v1", "test",
            CalibrationComparability.ComparableEnvelope, "test", "test");
}
