using System.Text;
using CoreSim.Analysis;

internal static class ContinuousCornerEnvelopeImpactReportWriter
{
    internal static void Write(string datasetDirectory, string afterScenarioSha256, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var dataset = Path.TrimEndingDirectorySeparator(Path.GetFullPath(datasetDirectory));
        var output = Path.GetFullPath(outputPath);
        if (output.Equals(dataset, StringComparison.OrdinalIgnoreCase)
            || output.StartsWith(dataset + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The impact report must not overwrite calibration data.", nameof(outputPath));
        var protectedReports = new[] { "current-model-baseline.md", "physical-width-impact.md",
            "continuous-corner-correction-impact.md", "calibration-scenarios-baseline.md",
            "longitudinal-speed-envelope-impact.md", "continuous-corner-foundation-impact.md" };
        if (protectedReports.Contains(Path.GetFileName(output), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("The impact report must not overwrite historical reports.", nameof(outputPath));
        var real = RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(Path.Combine(dataset, "pge_rider_heats.csv")));
        var report = ContinuousCornerEnvelopeImpactReport.Render(CalibrationScenarioSuite.RunRequired(), real, afterScenarioSha256);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, report, new UTF8Encoding(false));
    }
}
