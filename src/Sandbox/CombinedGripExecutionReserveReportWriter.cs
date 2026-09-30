using System.Text;
using CoreSim.Analysis;

internal static class CombinedGripExecutionReserveReportWriter
{
    internal static void Write(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var output = Path.GetFullPath(outputPath);
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(output), "combined-grip-execution-reserve.md"))
            throw new ArgumentException("The reserve writer only writes its own report.", nameof(outputPath));
        var result = FreeContinuousRacingTrajectoryGeometryExperiment.RunCombinedGripReserveExperiment(Console.WriteLine);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, CombinedGripExecutionReserveReport.Render(result), new UTF8Encoding(false));
    }
}
