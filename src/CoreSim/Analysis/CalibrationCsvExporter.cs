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
                "ResolvedLane", "EntryLateralPosition", "ExitLateralPosition", "Gearing", "TractionBias",
                "RiderStartSkill", "RiderSpeedSkill", "RiderSlideControlSkill", "RiderTrackReadingSkill",
                "RiderPairRidingSkill", "RiderAdaptabilitySkill", "EntrySurfaceGrip",
                "EntrySurfaceRuts", "EntrySurfaceMoisture", "EntrySurfaceEffectiveGrip", "FullDriveEquilibriumSpeedMetersPerSecond",
                "TurnExitNetAccelerationMetersPerSecondSquared", "TurnExitAccelerationDistanceMeters",
                "TurnExitCruiseDistanceMeters", "TurnExitDecelerationDistanceMeters", "TurnExitProfileTravelTimeSeconds", "StraightAccelerationDistanceMeters",
                "StraightCruiseDistanceMeters", "StraightDecelerationDistanceMeters", "StraightProfileTravelTimeSeconds",
                "TurnEntryScrubDecelerationDistanceMeters", "TurnEntryScrubCarryDistanceMeters",
                "TurnEntryScrubTravelTimeSeconds",
                "StandingStartReactionTimeSeconds", "StandingStartMovementTimeSeconds",
                "StandingStartProfileTotalTimeSeconds", "StandingStartAccelerationDistanceMeters",
                "StandingStartCruiseDistanceMeters", "StandingStartEntryNetAccelerationMetersPerSecondSquared",
                "StandingStartTimeTo70KphSeconds", "StandingStartSpeedAtTwoSecondsMetersPerSecond",
                "StandingStartPreparationDistanceMeters",
                "CornerCorrectionEntrySpeedMetersPerSecond", "CornerCorrectionTargetSpeedMetersPerSecond",
                "CornerCorrectionExitSpeedMetersPerSecond", "CornerCorrectionTravelTimeSeconds",
                "CornerCorrectionRequiredDistanceMeters", "CornerCorrectionDistanceMeters",
                "CornerCorrectionRemainingDistanceMeters",
                "CornerCorrectionDecelerationMetersPerSecondSquared", "CornerCorrectionTargetReached",
                "CornerProgress",
                "PeakCornerProgress",
                "ContinuousCornerEntryEnvelopeMetersPerSecond",
                "ContinuousCornerExitEnvelopeMetersPerSecond",
                "ContinuousCornerApexSpeedMetersPerSecond",
                "ContinuousCornerMinimumSpeedMetersPerSecond",
                "ContinuousCornerMinimumProgress",
                "ContinuousCornerCorrectionDistanceMeters",
                "ContinuousCornerCarryDistanceMeters",
                "ContinuousCornerDriveDistanceMeters",
                "ContinuousCornerDecelerationDistanceMeters",
                "ContinuousCornerTravelTimeSeconds",
                "ContinuousCornerResidualOverspeedMetersPerSecond",
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
                F(item.RiderStartSkill), F(item.RiderSpeedSkill), F(item.RiderSlideControlSkill),
                F(item.RiderTrackReadingSkill), F(item.RiderPairRidingSkill), F(item.RiderAdaptabilitySkill),
                F(item.EntrySurfaceGrip), F(item.EntrySurfaceRuts), F(item.EntrySurfaceMoisture),
                F(item.EntrySurfaceEffectiveGrip), F(item.FullDriveEquilibriumSpeedMetersPerSecond),
                F(item.TurnExitNetAccelerationMetersPerSecondSquared), F(item.TurnExitAccelerationDistanceMeters),
                F(item.TurnExitCruiseDistanceMeters), F(item.TurnExitDecelerationDistanceMeters), F(item.TurnExitProfileTravelTimeSeconds),
                F(item.StraightAccelerationDistanceMeters),
                F(item.StraightCruiseDistanceMeters), F(item.StraightDecelerationDistanceMeters),
                F(item.StraightProfileTravelTimeSeconds), F(item.TurnEntryScrubDecelerationDistanceMeters),
                F(item.TurnEntryScrubCarryDistanceMeters), F(item.TurnEntryScrubTravelTimeSeconds),
                F(item.StandingStartReactionTimeSeconds), F(item.StandingStartMovementTimeSeconds),
                F(item.StandingStartProfileTotalTimeSeconds), F(item.StandingStartAccelerationDistanceMeters),
                F(item.StandingStartCruiseDistanceMeters), F(item.StandingStartEntryNetAccelerationMetersPerSecondSquared),
                F(item.StandingStartTimeTo70KphSeconds), F(item.StandingStartSpeedAtTwoSecondsMetersPerSecond),
                F(item.StandingStartPreparationDistanceMeters),
                F(item.CornerCorrectionEntrySpeedMetersPerSecond),
                F(item.CornerCorrectionTargetSpeedMetersPerSecond),
                F(item.CornerCorrectionExitSpeedMetersPerSecond),
                F(item.CornerCorrectionTravelTimeSeconds),
                F(item.CornerCorrectionRequiredDistanceMeters),
                F(item.CornerCorrectionDistanceMeters),
                F(item.CornerCorrectionRemainingDistanceMeters),
                F(item.CornerCorrectionDecelerationMetersPerSecondSquared),
                B(item.CornerCorrectionTargetReached),
                F(item.CornerPhase?.CornerProgress),
                F(item.PeakCornerProgress),
                F(item.ContinuousCornerProfile?.EnvelopeAtEntryMetersPerSecond),
                F(item.ContinuousCornerProfile?.EnvelopeAtExitMetersPerSecond),
                F(item.ContinuousCornerProfile?.ApexSpeedMetersPerSecond),
                F(item.ContinuousCornerProfile?.MinimumSpeedMetersPerSecond),
                F(item.ContinuousCornerProfile?.MinimumSpeedCornerProgress),
                F(item.ContinuousCornerProfile?.CorrectionDistanceMeters),
                F(item.ContinuousCornerProfile?.CarryDistanceMeters),
                F(item.ContinuousCornerProfile?.DriveDistanceMeters),
                F(item.ContinuousCornerProfile?.DecelerationDistanceMeters),
                F(item.ContinuousCornerProfile?.TravelTimeSeconds),
                F(item.ContinuousCornerProfile?.ResidualOverspeedMetersPerSecond),
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
    private static string? B(bool? value) => value?.ToString(Invariant);
}
