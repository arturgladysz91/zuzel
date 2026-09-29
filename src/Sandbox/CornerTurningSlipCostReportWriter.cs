using System.Text;
using CoreSim.Analysis;

internal static class CornerTurningSlipCostReportWriter
{
    internal static void Write(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var output = Path.GetFullPath(outputPath);
        if (!StringComparer.OrdinalIgnoreCase.Equals(
                Path.GetFileName(output), "corner-turning-slip-cost.md"))
            throw new ArgumentException("The #48 writer only writes its own report.", nameof(outputPath));
        var report = CornerTurningSlipCostReport.Render(
            FreeContinuousRacingTrajectoryGeometryExperiment.RunTurningCostExperiment());
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, report, new UTF8Encoding(false));
    }
}
