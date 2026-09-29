using System.Text;
using CoreSim.Analysis;

internal static class FreeContinuousRacingTrajectoryGeometryReportWriter
{
    internal static void Write(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var output = Path.GetFullPath(outputPath);
        if (!StringComparer.OrdinalIgnoreCase.Equals(
                Path.GetFileName(output), "free-continuous-racing-trajectory-geometry.md"))
        {
            throw new ArgumentException("The #47 writer only writes its own report artifact.", nameof(outputPath));
        }
        var report = FreeContinuousRacingTrajectoryGeometryReport.Render(
            FreeContinuousRacingTrajectoryGeometryExperiment.Run());
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, report, new UTF8Encoding(false));
    }
}
