using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public static class CalibrationCsvExporter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string ExportSteps(CalibrationTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "HeatId", "StepNumber", "LapIndex", "SegmentIndex", "SegmentId", "SegmentType", "RiderId",
                "StartTimeSeconds", "EndTimeSeconds", "DurationSeconds", "StartDistanceMeters", "EndDistanceMeters",
                "TravelledMeters", "EntrySpeedMetersPerSecond", "PhysicsSpeedMetersPerSecond", "ExitSpeedMetersPerSecond",
                "PeakSpeedMetersPerSecond", "Outcome", "Status", "LapsCompleted", "BeforeLane", "PlannedLane", "TargetLane",
                "ResolvedLane", "EntryLateralPosition", "ExitLateralPosition", "Gearing", "TractionBias", "EntrySurfaceGrip",
                "EntrySurfaceRuts", "EntrySurfaceMoisture", "EntrySurfaceEffectiveGrip", "AttainableTopSpeedMetersPerSecond",
                "TurnExitNetAccelerationMetersPerSecondSquared", "StraightAccelerationDistanceMeters",
                "StraightCruiseDistanceMeters", "StraightDecelerationDistanceMeters", "StraightProfileTravelTimeSeconds",
                "TurnEntryScrubDecelerationDistanceMeters", "TurnEntryScrubCarryDistanceMeters",
                "TurnEntryScrubTravelTimeSeconds",
            },
        };

        rows.AddRange(trace.StepSamples
            .OrderBy(item => item.StepNumber)
            .ThenBy(item => item.RiderId)
            .Select(item => (IReadOnlyList<string?>)new[]
            {
                I(item.HeatId), I(item.StepNumber), I(item.LapIndex), I(item.SegmentIndex), I(item.SegmentId),
                item.SegmentType.ToString(), I(item.RiderId), F(item.StartTimeSeconds), F(item.EndTimeSeconds),
                F(item.DurationSeconds), F(item.StartDistanceMeters), F(item.EndDistanceMeters), F(item.TravelledMeters),
                F(item.EntrySpeedMetersPerSecond), F(item.PhysicsSpeedMetersPerSecond), F(item.ExitSpeedMetersPerSecond),
                F(item.PeakSpeedMetersPerSecond), item.Outcome.ToString(), item.Status.ToString(), I(item.LapsCompleted),
                I(item.BeforeLane), I(item.PlannedLane), I(item.TargetLane), I(item.ResolvedLane),
                F(item.EntryLateralPosition), F(item.ExitLateralPosition), F(item.Gearing), F(item.TractionBias),
                F(item.EntrySurfaceGrip), F(item.EntrySurfaceRuts), F(item.EntrySurfaceMoisture),
                F(item.EntrySurfaceEffectiveGrip), F(item.AttainableTopSpeedMetersPerSecond),
                F(item.TurnExitNetAccelerationMetersPerSecondSquared), F(item.StraightAccelerationDistanceMeters),
                F(item.StraightCruiseDistanceMeters), F(item.StraightDecelerationDistanceMeters),
                F(item.StraightProfileTravelTimeSeconds), F(item.TurnEntryScrubDecelerationDistanceMeters),
                F(item.TurnEntryScrubCarryDistanceMeters), F(item.TurnEntryScrubTravelTimeSeconds),
            }));

        return Write(rows);
    }

    public static string ExportLaps(CalibrationTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "RiderId", "LapNumber", "LapTimeSeconds", "LapDistanceMeters", "MaxSpeedMetersPerSecond" },
        };
        rows.AddRange(trace.LapSummaries
            .OrderBy(item => item.RiderId)
            .ThenBy(item => item.LapNumber)
            .Select(item => (IReadOnlyList<string?>)new[]
            {
                I(item.RiderId), I(item.LapNumber), F(item.LapTimeSeconds), F(item.LapDistanceMeters),
                F(item.MaxSpeedMetersPerSecond),
            }));
        return Write(rows);
    }

    public static string ExportRiders(CalibrationTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "RiderId", "Status", "LapsCompleted", "TotalTimeSeconds", "TotalDistanceMeters",
                "MaxSpeedMetersPerSecond", "AverageSpeedMetersPerSecond",
            },
        };
        rows.AddRange(trace.RiderSummaries
            .OrderBy(item => item.RiderId)
            .Select(item => (IReadOnlyList<string?>)new[]
            {
                I(item.RiderId), item.Status.ToString(), I(item.LapsCompleted), F(item.TotalTimeSeconds),
                F(item.TotalDistanceMeters), F(item.MaxSpeedMetersPerSecond), F(item.AverageSpeedMetersPerSecond),
            }));
        return Write(rows);
    }

    /// <summary>Applies RFC-style quoting for one CSV field.</summary>
    public static string EscapeField(string? value)
    {
        if (value is null)
            return string.Empty;
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string Write(IEnumerable<IReadOnlyList<string?>> rows)
    {
        var output = new StringBuilder();
        foreach (var row in rows)
            output.AppendJoin(',', row.Select(EscapeField)).Append('\n');
        return output.ToString();
    }

    private static string I(int value) => value.ToString(Invariant);
    private static string F(float value) => value.ToString("R", Invariant);
    private static string? F(float? value) => value?.ToString("R", Invariant);
}
