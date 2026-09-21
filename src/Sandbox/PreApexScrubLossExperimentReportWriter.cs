using System.Text;
using CoreSim.Analysis;

internal static class PreApexScrubLossExperimentReportWriter
{
    internal static void Write(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var output = Path.GetFullPath(outputPath);
        var protectedReports = new[]
        {
            "current-model-baseline.md",
            "physical-width-impact.md",
            "continuous-corner-correction-impact.md",
            "calibration-scenarios-baseline.md",
            "longitudinal-speed-envelope-impact.md",
            "continuous-corner-foundation-impact.md",
            "continuous-corner-envelope-impact.md",
            "motoarena-matched-venue.md",
            "real-start-telemetry.md",
            "straight-drive-envelope-experiment.md",
            "corner-reduced-drive-resistance-experiment.md",
        };
        if (protectedReports.Contains(Path.GetFileName(output), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("The #43 experiment must not overwrite a historical report.", nameof(outputPath));

        var report = PreApexScrubLossExperimentReport.Render(PreApexScrubLossExperiment.Run());
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, report, new UTF8Encoding(false));
    }
}
