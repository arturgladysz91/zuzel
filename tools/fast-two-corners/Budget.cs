using System.Diagnostics;
using System.Text.Json;

namespace FastTwoCorners167;

internal sealed class Budget(string directory, double prior)
{
    public const double TotalLimit = 900;
    public const double DiagnosticLimit = 40;
    public const double TrainingLimit = 650;
    public const double FinalAdmission = 890;
    public const int InitialEpisodeLimit = 200;
    public const int ValidationLimit = 8;
    public const int ComparisonLimit = 4;
    public const int DiagnosticRuns = 2;

    private static readonly JsonSerializerOptions Json = new(SharedWorldSpeedway.FreeLearningExperiment.Json) { WriteIndented = false };
    private readonly Process process = Process.GetCurrentProcess();
    public double Cpu => process.TotalProcessorTime.TotalSeconds;
    public double Used => prior + Cpu;

    public void Guard(double limit)
    {
        if (Used >= limit)
            throw new InvalidOperationException($"study167 CPU admission exhausted at {Used:F3} CPU-s");
    }

    public void Log(object value)
    {
        File.AppendAllText(Path.Combine(directory, "ledger.jsonl"), JsonSerializer.Serialize(value, Json) + "\n");
    }

    public static object Plan => new
    {
        TotalLimit, DiagnosticLimit, TrainingLimit, FinalAdmission, InitialEpisodeLimit,
        DiagnosticRuns, ValidationLimit, ComparisonLimit, ExtendedTraining = 0, Certification13By9 = 0,
        ReserveSeconds = Course.Reserve,
        Note = "CPU is charged across phases. Stop new learning before final admission. An unclosed phase is not reusable."
    };
}
