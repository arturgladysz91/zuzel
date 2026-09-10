using System.Text;
using System.Text.Json;
using CoreSim.Analysis;

internal static class LongitudinalCalibrationReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static void WriteSnapshot(string sourceSha, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var snapshot = LongitudinalCalibrationSnapshotBuilder.Capture(sourceSha);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions) + "\n";
        WriteUtf8(outputPath, json);
    }

    public static void WriteImpact(string datasetDirectory, string beforeSnapshotPath,
        string baseMainSha, string candidateHeadSha, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeSnapshotPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var datasetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(datasetDirectory));
        var output = Path.GetFullPath(outputPath);
        var protectedNames = new[]
        {
            "current-model-baseline.md", "physical-width-impact.md",
            "continuous-corner-correction-impact.md", "calibration-scenarios-baseline.md",
        };
        if (output.Equals(datasetPath, StringComparison.OrdinalIgnoreCase)
            || output.StartsWith(datasetPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || protectedNames.Contains(Path.GetFileName(output), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Impact reports must not overwrite datasets or historical reports.", nameof(outputPath));

        var before = JsonSerializer.Deserialize<LongitudinalCalibrationSnapshot>(
            File.ReadAllText(Path.GetFullPath(beforeSnapshotPath)), JsonOptions)
            ?? throw new InvalidOperationException("The before snapshot is empty.");
        var after = LongitudinalCalibrationSnapshotBuilder.Capture(candidateHeadSha);
        var dataset = RealWorldCalibrationDataset.ParseCsv(
            File.ReadAllText(Path.Combine(datasetPath, "pge_rider_heats.csv")));
        var report = LongitudinalSpeedEnvelopeReport.Render(
            before, after, dataset.Distributions, baseMainSha, candidateHeadSha);
        WriteUtf8(output, report);
    }

    private static void WriteUtf8(string outputPath, string content)
    {
        var output = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, content, new UTF8Encoding(false));
    }
}
