using System.Text;
using CoreSim.Analysis;

internal static class ContinuousCornerFoundationReportWriter
{
    public static void Write(
        string datasetDirectory,
        string beforeCalibrationReportPath,
        string baseMainSha,
        string candidateCodeHeadSha,
        string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeCalibrationReportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var datasetPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(datasetDirectory));
        var beforePath = Path.GetFullPath(beforeCalibrationReportPath);
        var output = Path.GetFullPath(outputPath);
        if (output.Equals(datasetPath, StringComparison.OrdinalIgnoreCase)
            || output.StartsWith(
                datasetPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || output.Equals(beforePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The corner-foundation report must not overwrite its dataset or before capture.",
                nameof(outputPath));
        }

        var protectedHistoricalReports = new[]
        {
            "current-model-baseline.md",
            "physical-width-impact.md",
            "continuous-corner-correction-impact.md",
            "calibration-scenarios-baseline.md",
            "longitudinal-speed-envelope-impact.md",
        };
        if (protectedHistoricalReports.Contains(
                Path.GetFileName(output),
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The corner-foundation report must not overwrite a historical report.",
                nameof(outputPath));
        }

        var dataset = RealWorldCalibrationDataset.ParseCsv(
            File.ReadAllText(Path.Combine(datasetPath, "pge_rider_heats.csv")));
        var results = CalibrationScenarioSuite.RunRequired();
        var baseline = results.OfType<CalibrationHeatResult>().Single(
            result => result.Metadata.ScenarioId == "full_heat/baseline");
        var evaluation = RealWorldCalibrationEvaluator.Evaluate(
            dataset,
            baseline.ToSimulationCalibrationResult());
        var afterCalibrationReport = CalibrationScenarioReport.Render(
            results,
            evaluation,
            baseMainSha);
        var beforeCalibrationReport = File.ReadAllText(beforePath);
        var report = ContinuousCornerFoundationReport.Render(
            results,
            beforeCalibrationReport,
            afterCalibrationReport,
            baseMainSha,
            candidateCodeHeadSha);

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, report, new UTF8Encoding(false));
    }
}
