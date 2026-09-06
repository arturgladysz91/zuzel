using System.Collections.ObjectModel;

namespace CoreSim.Analysis;

public sealed record SimulationMetricSeries(
    string MetricId,
    string Unit,
    IReadOnlyList<double> Observations)
{
    public SimulationMetricSeries(string metricId, string unit, IEnumerable<double> observations)
        : this(metricId, unit, Array.AsReadOnly(observations.ToArray()))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        if (Observations.Count == 0 || Observations.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("Simulation metric observations must be non-empty and finite.", nameof(observations));
    }
}

public sealed class SimulationCalibrationResult
{
    private readonly ReadOnlyCollection<SimulationMetricSeries> _metrics;

    public string ScenarioId { get; }
    public IReadOnlyList<SimulationMetricSeries> Metrics => _metrics;

    public SimulationCalibrationResult(string scenarioId, IEnumerable<SimulationMetricSeries> metrics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ArgumentNullException.ThrowIfNull(metrics);
        ScenarioId = scenarioId;
        var materialized = metrics.OrderBy(item => item.MetricId, StringComparer.Ordinal).ToArray();
        if (materialized.Select(item => item.MetricId).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
            throw new ArgumentException("Simulation metric ids must be unique.", nameof(metrics));
        _metrics = Array.AsReadOnly(materialized);
    }
}

public sealed record CalibrationQuantileComparison(
    int Percentile,
    double RealValue,
    double SimulationValue,
    double SignedDifference,
    double? RelativeDifference,
    double SimulationValueRealEmpiricalPercentile);

public sealed record CalibrationMetricEvaluation(
    CalibrationMetricDefinition Definition,
    bool NumericallyCompared,
    IReadOnlyList<CalibrationQuantileComparison> QuantileComparisons,
    string Notes);

/// <summary>
/// A deliberately component-wise report.  There is no overall accuracy score:
/// incompatible metric families and track-context observations stay visible.
/// </summary>
public sealed class CalibrationEvaluationReport
{
    private readonly ReadOnlyCollection<CalibrationMetricEvaluation> _components;

    public string ScenarioId { get; }
    public IReadOnlyList<CalibrationMetricEvaluation> Components => _components;

    internal CalibrationEvaluationReport(
        string scenarioId,
        IEnumerable<CalibrationMetricEvaluation> components)
    {
        ScenarioId = scenarioId;
        _components = Array.AsReadOnly(components.ToArray());
    }
}

public static class RealWorldCalibrationEvaluator
{
    private static readonly (int Percentile, double Probability)[] ComparedQuantiles =
    {
        (10, 0.10), (25, 0.25), (50, 0.50), (75, 0.75), (90, 0.90),
    };

    public static CalibrationEvaluationReport Evaluate(
        RealWorldCalibrationDataset realWorld,
        SimulationCalibrationResult simulation)
    {
        ArgumentNullException.ThrowIfNull(realWorld);
        ArgumentNullException.ThrowIfNull(simulation);
        var components = new List<CalibrationMetricEvaluation>();
        foreach (var simulatedMetric in simulation.Metrics)
        {
            if (!realWorld.MetricDefinitions.TryGetValue(simulatedMetric.MetricId, out var definition))
                throw new ArgumentException($"Unknown calibration metric '{simulatedMetric.MetricId}'.", nameof(simulation));
            if (!StringComparer.Ordinal.Equals(definition.Unit, simulatedMetric.Unit))
            {
                throw new ArgumentException(
                    $"Metric '{definition.MetricId}' unit mismatch: real '{definition.Unit}', simulation '{simulatedMetric.Unit}'.",
                    nameof(simulation));
            }

            if (definition.Comparability == CalibrationComparability.UnsupportedNumericByCurrentSource)
            {
                components.Add(new CalibrationMetricEvaluation(
                    definition,
                    false,
                    Array.Empty<CalibrationQuantileComparison>(),
                    "The current source has no supported individual numeric target; source ranking flags are never parsed as speeds."));
                continue;
            }

            if (!realWorld.Distributions.TryGetValue(definition.MetricId, out var realDistribution))
            {
                components.Add(new CalibrationMetricEvaluation(
                    definition,
                    false,
                    Array.Empty<CalibrationQuantileComparison>(),
                    "No numeric real-world distribution is available in this dataset version."));
                continue;
            }

            var simulatedValues = simulatedMetric.Observations.Order().ToArray();
            var comparisons = ComparedQuantiles.Select(item =>
            {
                var realValue = CalibrationDistribution.LinearQuantile(realDistribution.Observations, item.Probability);
                var simulationValue = CalibrationDistribution.LinearQuantile(simulatedValues, item.Probability);
                return new CalibrationQuantileComparison(
                    item.Percentile,
                    realValue,
                    simulationValue,
                    simulationValue - realValue,
                    Math.Abs(realValue) > 1e-12 ? (simulationValue - realValue) / Math.Abs(realValue) : null,
                    CalibrationDistribution.EmpiricalPercentileRank(realDistribution.Observations, simulationValue));
            }).ToArray();
            components.Add(new CalibrationMetricEvaluation(
                definition,
                true,
                Array.AsReadOnly(comparisons),
                definition.Comparability == CalibrationComparability.ContextOnlyUntilTrackGeometry
                    ? "Reported as context only: absolute values depend on matching concrete track geometry."
                    : "Comparable as a distribution/envelope; this is not an automatic tuning score."));
        }

        return new CalibrationEvaluationReport(simulation.ScenarioId, components);
    }
}
