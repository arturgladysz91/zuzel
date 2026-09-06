using System.Collections.ObjectModel;

namespace CoreSim.Analysis;

public enum CalibrationComparability
{
    ComparableEnvelope,
    ContextOnlyUntilTrackGeometry,
    UnsupportedNumericByCurrentSource,
    LiteratureContext,
}

public sealed record CalibrationMetricDefinition(
    string MetricId,
    string Unit,
    string Definition,
    string Source,
    string SourceVersion,
    string Population,
    CalibrationComparability Comparability,
    string EvidenceClass,
    string Notes);

public sealed record CalibrationQuantiles(
    double P01,
    double P10,
    double P25,
    double P50,
    double P75,
    double P90,
    double P99);

public sealed class CalibrationDistribution
{
    private readonly ReadOnlyCollection<double> _observations;

    public CalibrationMetricDefinition Definition { get; }
    public IReadOnlyList<double> Observations => _observations;
    public CalibrationQuantiles Quantiles { get; }

    public CalibrationDistribution(
        CalibrationMetricDefinition definition,
        IEnumerable<double> observations)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(observations);
        var ordered = observations.Order().ToArray();
        if (ordered.Length == 0)
            throw new ArgumentException("A calibration distribution needs at least one observation.", nameof(observations));
        if (ordered.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("Calibration observations must be finite.", nameof(observations));

        Definition = definition;
        _observations = Array.AsReadOnly(ordered);
        Quantiles = new CalibrationQuantiles(
            LinearQuantile(ordered, 0.01),
            LinearQuantile(ordered, 0.10),
            LinearQuantile(ordered, 0.25),
            LinearQuantile(ordered, 0.50),
            LinearQuantile(ordered, 0.75),
            LinearQuantile(ordered, 0.90),
            LinearQuantile(ordered, 0.99));
    }

    /// <summary>
    /// Linear interpolation at the zero-based sorted position (n - 1) * p.
    /// This is the sole quantile definition used by the calibration pipeline.
    /// </summary>
    public static double LinearQuantile(IReadOnlyList<double> sortedObservations, double probability)
    {
        ArgumentNullException.ThrowIfNull(sortedObservations);
        if (sortedObservations.Count == 0)
            throw new ArgumentException("At least one observation is required.", nameof(sortedObservations));
        if (probability is < 0d or > 1d || !double.IsFinite(probability))
            throw new ArgumentOutOfRangeException(nameof(probability));

        var position = (sortedObservations.Count - 1) * probability;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
            return sortedObservations[lower];
        var fraction = position - lower;
        return sortedObservations[lower]
               + (sortedObservations[upper] - sortedObservations[lower]) * fraction;
    }

    /// <summary>
    /// Midrank empirical percentile: values below contribute one, ties one half.
    /// Results are in 0..100; below/above the observed range return 0/100.
    /// </summary>
    public static double EmpiricalPercentileRank(
        IReadOnlyList<double> sortedObservations,
        double simulatedValue)
    {
        ArgumentNullException.ThrowIfNull(sortedObservations);
        if (sortedObservations.Count == 0)
            throw new ArgumentException("At least one observation is required.", nameof(sortedObservations));
        if (!double.IsFinite(simulatedValue))
            throw new ArgumentOutOfRangeException(nameof(simulatedValue));

        var lower = LowerBound(sortedObservations, simulatedValue);
        var upper = UpperBound(sortedObservations, simulatedValue);
        return 100d * (lower + (upper - lower) * 0.5d) / sortedObservations.Count;
    }

    private static int LowerBound(IReadOnlyList<double> values, double target)
    {
        var low = 0;
        var high = values.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (values[middle] < target)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private static int UpperBound(IReadOnlyList<double> values, double target)
    {
        var low = 0;
        var high = values.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (values[middle] <= target)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }
}

public static class CalibrationUnits
{
    public static double KphToMetersPerSecond(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        return value / 3.6d;
    }

    public static double MetersPerSecondToKph(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        return value * 3.6d;
    }
}
