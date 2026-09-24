using System.Text;
using CoreSim.Analysis;

internal static class DynamicCornerTrajectoryGeometryReportWriter
{
    internal static void Write(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var output = Path.GetFullPath(outputPath);
        var protectedReports = Directory.Exists(Path.GetDirectoryName(output))
            ? Directory.GetFiles(Path.GetDirectoryName(output)!, "*.md")
                .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(path),
                    "dynamic-corner-trajectory-geometry.md"))
                .Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (protectedReports.Contains(output))
            throw new ArgumentException("The #46 experiment must not overwrite a historical report.",
                nameof(outputPath));

        var report = DynamicCornerTrajectoryGeometryReport.Render(
            DynamicCornerTrajectoryGeometryExperiment.Run());
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, report, new UTF8Encoding(false));
    }
}
