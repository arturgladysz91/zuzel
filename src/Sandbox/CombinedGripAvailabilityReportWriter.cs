using System.Text;
using CoreSim.Analysis;

internal static class CombinedGripAvailabilityReportWriter
{
    internal static void Write(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var output = Path.GetFullPath(outputPath);
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(output), "combined-grip-availability.md"))
            throw new ArgumentException("The combined-grip writer only writes its own report.", nameof(outputPath));
        var report = CombinedGripAvailabilityReport.Render(
            FreeContinuousRacingTrajectoryGeometryExperiment.RunCombinedGripExperiment());
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, report, new UTF8Encoding(false));
    }
}
