using System.Text;
using CoreSim.Analysis;

internal static class CalibrationScenarioReportWriter
{
    public static void Write(string datasetDirectory, string baselineMainSha, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var datasetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(datasetDirectory));
        var output = Path.GetFullPath(outputPath);
        var protectedNames = new[]
        {
            "current-model-baseline.md", "physical-width-impact.md", "continuous-corner-correction-impact.md",
        };
        if (output.Equals(datasetPath, StringComparison.OrdinalIgnoreCase)
            || output.StartsWith(datasetPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || protectedNames.Contains(Path.GetFileName(output), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Scenario reports must not overwrite the dataset or historical reports.", nameof(outputPath));
        var dataset = RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(Path.Combine(datasetPath, "pge_rider_heats.csv")));
        var results = CalibrationScenarioSuite.RunRequired();
        var baseline = results.OfType<CalibrationHeatResult>().Single(result => result.Metadata.ScenarioId == "full_heat/baseline");
        var evaluation = RealWorldCalibrationEvaluator.Evaluate(dataset, baseline.ToSimulationCalibrationResult());
        var report = CalibrationScenarioReport.Render(results, evaluation, baselineMainSha);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, report, new UTF8Encoding(false));
    }
}
