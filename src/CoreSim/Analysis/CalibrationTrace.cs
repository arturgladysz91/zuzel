using CoreSim.Race;
using System.Collections.ObjectModel;

namespace CoreSim.Analysis;

public sealed record CalibrationStepSample(
    int HeatId,
    int StepNumber,
    int LapIndex,
    int SegmentIndex,
    int SegmentId,
    SegmentType SegmentType,
    int RiderId,
    float StartTimeSeconds,
    float EndTimeSeconds,
    float DurationSeconds,
    float StartDistanceMeters,
    float EndDistanceMeters,
    float TravelledMeters,
    float EntrySpeedMetersPerSecond,
    float PhysicsSpeedMetersPerSecond,
    float ExitSpeedMetersPerSecond,
    float PeakSpeedMetersPerSecond,
    SegmentOutcome Outcome,
    RiderRaceStatus Status,
    int LapsCompleted,
    int BeforeLane,
    int PlannedLane,
    int TargetLane,
    int ResolvedLane,
    float EntryLateralPosition,
    float ExitLateralPosition,
    float Gearing,
    float TractionBias,
    float RiderStartSkill,
    float RiderSpeedSkill,
    float RiderSlideControlSkill,
    float RiderTrackReadingSkill,
    float RiderPairRidingSkill,
    float RiderAdaptabilitySkill,
    float EntrySurfaceGrip,
    float EntrySurfaceRuts,
    float EntrySurfaceMoisture,
    float EntrySurfaceEffectiveGrip,
    float? FullDriveEquilibriumSpeedMetersPerSecond,
    /// <summary>Signed net acceleration at the start of the TurnExit full-drive profile.</summary>
    float? TurnExitNetAccelerationMetersPerSecondSquared,
    float? TurnExitAccelerationDistanceMeters,
    float? TurnExitCruiseDistanceMeters,
    float? TurnExitProfileTravelTimeSeconds,
    float? StraightAccelerationDistanceMeters,
    float? StraightCruiseDistanceMeters,
    float? StraightDecelerationDistanceMeters,
    float? StraightProfileTravelTimeSeconds,
    float? TurnEntryScrubDecelerationDistanceMeters,
    float? TurnEntryScrubCarryDistanceMeters,
    float? TurnEntryScrubTravelTimeSeconds,
    float? StandingStartReactionTimeSeconds = null,
    float? StandingStartMovementTimeSeconds = null,
    float? StandingStartProfileTotalTimeSeconds = null,
    float? StandingStartAccelerationDistanceMeters = null,
    float? StandingStartCruiseDistanceMeters = null,
    float? StandingStartEntryNetAccelerationMetersPerSecondSquared = null,
    float? StandingStartTimeTo70KphSeconds = null,
    float? StandingStartSpeedAtTwoSecondsMetersPerSecond = null,
    float? StandingStartPreparationDistanceMeters = null,
    float? TurnExitDecelerationDistanceMeters = null,
    float? CornerCorrectionEntrySpeedMetersPerSecond = null,
    float? CornerCorrectionTargetSpeedMetersPerSecond = null,
    float? CornerCorrectionExitSpeedMetersPerSecond = null,
    float? CornerCorrectionTravelTimeSeconds = null,
    float? CornerCorrectionRequiredDistanceMeters = null,
    float? CornerCorrectionDistanceMeters = null,
    float? CornerCorrectionRemainingDistanceMeters = null,
    float? CornerCorrectionDecelerationMetersPerSecondSquared = null,
    bool? CornerCorrectionTargetReached = null,
    CornerPhaseContext? CornerPhase = null,
    ContinuousCornerTraversalProfile? ContinuousCornerProfile = null,
    float? PeakCornerProgress = null);

public sealed record CalibrationLapSummary(
    int RiderId,
    int LapNumber,
    float LapTimeSeconds,
    float LapDistanceMeters,
    float MaxSpeedMetersPerSecond);

public sealed record CalibrationRiderSummary(
    int RiderId,
    RiderRaceStatus Status,
    int LapsCompleted,
    float TotalTimeSeconds,
    float TotalDistanceMeters,
    float MaxSpeedMetersPerSecond,
    float? AverageSpeedMetersPerSecond);

/// <summary>Detached, read-only observations from one production heat.</summary>
public sealed class CalibrationTrace
{
    private readonly ReadOnlyCollection<CalibrationStepSample> _stepSamples;
    private readonly ReadOnlyCollection<CalibrationLapSummary> _lapSummaries;
    private readonly ReadOnlyCollection<CalibrationRiderSummary> _riderSummaries;
    private readonly ReadOnlyCollection<RiderHeatResult> _classification;

    public int HeatId { get; }
    public TrackGeometry TrackGeometry { get; }
    public HeatSimulationOptions Options { get; }
    public IReadOnlyList<CalibrationStepSample> StepSamples => _stepSamples;
    public IReadOnlyList<CalibrationLapSummary> LapSummaries => _lapSummaries;
    public IReadOnlyList<CalibrationRiderSummary> RiderSummaries => _riderSummaries;
    public IReadOnlyList<RiderHeatResult> Classification => _classification;

    internal CalibrationTrace(
        int heatId,
        TrackGeometry trackGeometry,
        HeatSimulationOptions options,
        IEnumerable<CalibrationStepSample> stepSamples,
        IEnumerable<CalibrationLapSummary> lapSummaries,
        IEnumerable<CalibrationRiderSummary> riderSummaries,
        IEnumerable<RiderHeatResult> classification)
    {
        HeatId = heatId;
        TrackGeometry = new TrackGeometry(
            trackGeometry.StraightLengthMeters,
            trackGeometry.InnerRadiusMeters,
            trackGeometry.StraightWidthMeters,
            trackGeometry.TurnWidthMeters,
            trackGeometry.TurnSegmentAngleRadians);
        Options = options with { };
        _stepSamples = Array.AsReadOnly(stepSamples
            .OrderBy(item => item.StepNumber)
            .ThenBy(item => item.RiderId)
            .ToArray());
        _lapSummaries = Array.AsReadOnly(lapSummaries
            .OrderBy(item => item.RiderId)
            .ThenBy(item => item.LapNumber)
            .ToArray());
        _riderSummaries = Array.AsReadOnly(riderSummaries
            .OrderBy(item => item.RiderId)
            .ToArray());
        _classification = Array.AsReadOnly(classification.ToArray());
    }
}
