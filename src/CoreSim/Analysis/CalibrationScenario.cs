using CoreSim.Setup;

namespace CoreSim.Analysis;

public enum CalibrationScenarioKind { Start, Straight, TurnEntry, TurnMiddle, TurnExit, LineGeometry, FullHeat }
public enum CalibrationStartMode { PureLaunch, FirstCornerPreparation }

/// <summary>Measurement labels, never inputs to production physics or RNG.</summary>
public sealed record CalibrationScenarioMetadata(string ScenarioId, CalibrationScenarioKind Kind, string Description);
public sealed record CalibrationScenarioFixture(RiderSkills Skills, BikeSetup Setup, TrackSurfaceState Surface);
public sealed record CalibrationSurfaceFixture(string Id, TrackSurfaceState Surface);

public abstract record CalibrationScenario(CalibrationScenarioMetadata Metadata, CalibrationScenarioFixture Fixture);
public sealed record CalibrationStartScenario(
    CalibrationScenarioMetadata Metadata, CalibrationScenarioFixture Fixture, CalibrationStartMode Mode)
    : CalibrationScenario(Metadata, Fixture);
public sealed record CalibrationStraightScenario(
    CalibrationScenarioMetadata Metadata, CalibrationScenarioFixture Fixture,
    float DistanceMeters, float EntrySpeedMetersPerSecond)
    : CalibrationScenario(Metadata, Fixture);
public sealed record CalibrationTurnScenario(
    CalibrationScenarioMetadata Metadata, CalibrationScenarioFixture Fixture, SegmentType SegmentType,
    float LateralPosition, float EntrySpeedMetersPerSecond, float SegmentProgress = 0f)
    : CalibrationScenario(Metadata, Fixture);
public sealed record CalibrationLineScenario(
    CalibrationScenarioMetadata Metadata, CalibrationScenarioFixture Fixture, int LateralPosition)
    : CalibrationScenario(Metadata, Fixture);
public sealed record CalibrationScenarioRider(int RiderId, int Lane, RiderSkills Skills);

public sealed record CalibrationHeatScenario : CalibrationScenario
{
    public IReadOnlyList<CalibrationScenarioRider> Riders { get; }

    public CalibrationHeatScenario(CalibrationScenarioMetadata metadata, CalibrationScenarioFixture fixture,
        IEnumerable<CalibrationScenarioRider> riders) : base(metadata, fixture)
    {
        ArgumentNullException.ThrowIfNull(riders);
        var copy = riders.OrderBy(rider => rider.RiderId).ToArray();
        if (copy.Length != 4 || copy.Select(rider => rider.RiderId).Distinct().Count() != 4
            || !copy.Select(rider => rider.Lane).Order().SequenceEqual(new[] { 0, 1, 2, 3 }))
            throw new ArgumentException("Four unique rider ids on lanes 0, 1, 2, 3 are required.", nameof(riders));
        Riders = Array.AsReadOnly(copy);
    }
}

public abstract record CalibrationScenarioResult(CalibrationScenarioMetadata Metadata);
public sealed record CalibrationStartResult(
    CalibrationStartScenario Scenario, float DistanceMeters, StandingStartLaunchProfile Profile,
    float ReferenceDriveForceNewtons, float ReferenceNetAccelerationMetersPerSecondSquared)
    : CalibrationScenarioResult(Scenario.Metadata);
public sealed record CalibrationStraightResult(
    CalibrationStraightScenario Scenario, StraightSpeedProfile Profile,
    float ReferenceDriveForceNewtons, float EntryNetAccelerationMetersPerSecondSquared)
    : CalibrationScenarioResult(Scenario.Metadata);

/// <summary>Transition speeds observed by calling SegmentPhysics.Apply, not reimplemented thresholds.</summary>
public sealed record CalibrationCornerCapability(
    float MaxSafeSpeedMetersPerSecond, float CorrectionDecelerationMetersPerSecondSquared,
    float FirstBrakeSpeedMetersPerSecond, float? FirstRunWideSpeedMetersPerSecond,
    float FirstCrashSpeedMetersPerSecond, float? ObservedRunWideOverspeedRetention);

/// <summary>Original production profiles and state change, plus arithmetic accounting of their phases.</summary>
public sealed record CalibrationTurnResult(
    CalibrationTurnScenario Scenario, float RadiusMeters, float AvailableDistanceMeters,
    CalibrationCornerCapability Capability, RiderStepDiagnostics Diagnostics, RiderStateChange Change)
    : CalibrationScenarioResult(Scenario.Metadata)
{
    public float ScrubDistanceMeters => Diagnostics.TurnEntryScrubProfile is { } scrub
        ? scrub.DecelerationDistanceMeters + scrub.CarryDistanceMeters : 0f;
    public float CorrectionDistanceMeters => Diagnostics.ContinuousCornerProfile?.CorrectionDistanceMeters
        ?? Diagnostics.CornerSpeedCorrectionProfile?.CorrectionDistanceMeters ?? 0f;
    public float DriveDistanceMeters => Diagnostics.ContinuousCornerProfile?.DriveDistanceMeters
        ?? (Diagnostics.TurnExitDriveProfile is { } drive
        ? drive.AccelerationDistanceMeters + drive.CruiseDistanceMeters + drive.DecelerationDistanceMeters : 0f);
    public float CarryDistanceMeters => Diagnostics.TravelledMeters - ScrubDistanceMeters
        - CorrectionDistanceMeters - DriveDistanceMeters;
    public float CarryTimeSeconds => Diagnostics.ContinuousCornerProfile?.CarryTimeSeconds ?? (Diagnostics.TravelTimeSeconds
        - (Diagnostics.TurnEntryScrubProfile?.TravelTimeSeconds ?? 0f)
        - (Diagnostics.CornerSpeedCorrectionProfile?.TravelTimeSeconds ?? 0f)
        - (Diagnostics.TurnExitDriveProfile?.TravelTimeSeconds ?? 0f));
    // Residual is observed at correction exit, not after legitimate TurnExit drive.
    public float ResidualOverspeedMetersPerSecond => Diagnostics.ContinuousCornerProfile?.ResidualOverspeedMetersPerSecond
        ?? (Diagnostics.CornerSpeedCorrectionProfile is { } correction
        ? MathF.Max(0f, correction.ExitSpeedMetersPerSecond - correction.TargetSpeedMetersPerSecond) : 0f);
}

public sealed record CalibrationHeatRiderObservation(
    CalibrationSkillRiderObservation Performance, int CorrectionCount, int ResidualOverspeedCount,
    float MaximumResidualOverspeedMetersPerSecond, float MinimumObservedLateralPosition,
    float MaximumObservedLateralPosition);
public sealed record CalibrationWithinHeatSpreads(
    double HeatTimeSeconds, double VmaxKph, double L1Seconds, double AverageSpeedMetersPerSecond);

public sealed record CalibrationHeatResult(
    CalibrationHeatScenario Scenario, IReadOnlyList<CalibrationHeatRiderObservation> Riders,
    CalibrationWithinHeatSpreads Spreads, CalibrationTrace Trace)
    : CalibrationScenarioResult(Scenario.Metadata)
{
    public SimulationCalibrationResult ToSimulationCalibrationResult()
        => new CalibrationSkillSweepResult(new CalibrationSkillScenario(Metadata.ScenarioId, Scenario.Fixture.Skills),
            Riders.Select(rider => rider.Performance)).ToSimulationCalibrationResult();
}

public sealed record CalibrationLineResult(
    CalibrationLineScenario Scenario, float NormalizedLateralFraction,
    float StraightOffsetFromInnerReferenceMeters, float TurnOffsetFromInnerReferenceMeters,
    float RadiusMeters, float TurnArcLengthMeters, float ReferenceLapDistanceMeters,
    float MaxSafeSpeedMetersPerSecond, CalibrationHeatRiderObservation Rider, CalibrationTrace Trace)
    : CalibrationScenarioResult(Scenario.Metadata);
