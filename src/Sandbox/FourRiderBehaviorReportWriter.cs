using System.Text;
using CoreSim.Analysis;

internal static class FourRiderBehaviorReportWriter
{
    internal static void Write(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var directory = Path.GetFullPath(outputDirectory);
        var result = FourRiderBehaviorSuite.RunSuite(Console.WriteLine);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, FourRiderBehaviorReport.MarkdownFileName),
            FourRiderBehaviorReport.Render(result), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, FourRiderBehaviorReport.EvidenceFileName),
            FourRiderBehaviorReport.RenderEvidence(result), new UTF8Encoding(false));
    }
}
