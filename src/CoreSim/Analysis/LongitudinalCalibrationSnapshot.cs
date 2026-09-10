using System.Globalization;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record LongitudinalCalibrationConstants(
    float StraightMinimumReferenceAcceleration,
    float StraightMaximumReferenceAcceleration,
    float TurnExitMinimumReferenceAcceleration,
    float TurnExitMaximumReferenceAcceleration,
    float DriveOrientedFadeRate,
    float SpeedOrientedFadeRate,
    float ReferenceSpeed,
    float NominalMass,
    float BaseResistance,
    float QuadraticResistance,
    float IntegrationStep,
    float LowGearingDriveMultiplier,
    float HighGearingDriveMultiplier,
    float SurfaceDriveMinimum,
    float SurfaceDriveRange,
    float StartReactionSlow,
    float StartReactionFast,
    float StartAccelerationMinimum,
    float StartAccelerationMaximum,
    float CornerCorrectionMinimum,
    float CornerCorrectionMaximum);

public sealed record LongitudinalStraightObservation(
    string ScenarioId, float SpeedSkill, float Gearing, float DistanceMeters,
    float EntrySpeed, float ExitSpeed, float PeakSpeed, float TravelTime,
    float AccelerationDistance, float CruiseDistance, float DecelerationDistance,
    float ReferenceForce, float EntryNetAcceleration, float EquilibriumSpeed);

public sealed record LongitudinalForceObservation(
    float SpeedSkill, float Gearing, float Speed, float Envelope,
    float DriveForce, float ResistanceForce, float NetForce, float Acceleration,
    float EffectivePowerWatts);

public sealed record LongitudinalEquilibriumObservation(
    float SpeedSkill, float Gearing, float EquilibriumSpeed,
    float BelowSpeed, float BelowNetForce, float NearNetForce,
    float AboveSpeed, float AboveNetForce);

public sealed record LongitudinalGearingCrossoverObservation(
    float DriveForceCrossoverSpeed, float TraversalCrossoverDistance);

public sealed record LongitudinalTurnExitObservation(
    string ScenarioId, float SpeedSkill, string Outcome, float AvailableDistance,
    float IncomingSpeed, float MaxSafeSpeed, float? CorrectionTarget,
    float? CorrectionExit, float CorrectionDistance, float LegalDriveDistance,
    float? DriveEntry, float? DriveExit, float? DrivePeak, float? DriveTime,
    float? DriveEntryNetAcceleration, float? EquilibriumSpeed, float FinalExit);

public sealed record LongitudinalStartObservation(
    string ScenarioId, string Mode, float StartSkill, float ReactionTime,
    float MovementTime, float TotalTime, float? TimeTo70, float? SpeedAtTwoSeconds,
    float ExitSpeed, float PeakSpeed, float AccelerationDistance,
    float CruiseDistance, float PreparationDistance, float EntryNetAcceleration,
    float EquilibriumSpeed);

public sealed record LongitudinalHeatRiderObservation(
    string ScenarioId, float SpeedSkill, int RiderId, double VmaxKph,
    float AverageSpeed, float? L1, float? L2, float? L3, float? L4,
    float? FlyingMedian, float? L1Penalty, float HeatTime, float Distance,
    int RunWideCount, int BrakeCount, int CrashCount);

public sealed record LongitudinalWithinHeatObservation(
    string ScenarioId, double HeatTimeSpread, double VmaxSpread,
    double L1Spread, double AverageSpeedSpread);

public sealed record LongitudinalCornerObservation(
    string ScenarioId, float SpeedSkill, float SlideControl,
    float MaxSafeSpeed, float CorrectionCapability, float FirstBrakeSpeed,
    float? FirstRunWideSpeed, float FirstCrashSpeed, float? RunWideRetention);

public sealed record LongitudinalCalibrationSnapshot(
    string SourceSha,
    LongitudinalCalibrationConstants Constants,
    LongitudinalStraightObservation[] Straights,
    LongitudinalForceObservation[] ForceCurve,
    LongitudinalEquilibriumObservation[] Equilibria,
    LongitudinalGearingCrossoverObservation GearingCrossover,
    LongitudinalTurnExitObservation[] TurnExits,
    LongitudinalStartObservation[] Starts,
    LongitudinalHeatRiderObservation[] HeatRiders,
    LongitudinalWithinHeatObservation[] WithinHeat,
    LongitudinalCornerObservation[] Corners);

/// <summary>
/// Deterministic calibration measurements. Traversal and corner outcomes are
/// obtained from existing production primitives and the #35 scenario suite.
/// </summary>
public static class LongitudinalCalibrationSnapshotBuilder
{
    private static readonly float[] SkillValues = { 0f, 25f, 50f, 75f, 100f };
    private static readonly float[] GearingValues = { 0f, 0.5f, 1f };
    private static readonly float[] ForceSpeeds = { 10f, 16f, 20f, 24f, 28f, 30f, 32f, 36f };
    private static readonly float[] StraightDistances = { 10f, 20f, 30f, 60f, 100f, 300f, 600f };
    private static readonly float[] GearingDistances = { 10f, 30f, 60f, 100f, 300f, 600f };

    public static LongitudinalCalibrationSnapshot Capture(string sourceSha)
    {
        ValidateSha(sourceSha);
        var baseline = CalibrationScenarioCatalog.Baseline;
        var straights = new List<LongitudinalStraightObservation>();

        foreach (var entry in new[] { 10f, 16f, 20f, 24f })
            straights.Add(Straight($"entry/{CanonicalNumber(entry, "000")}", 50f, 0.5f, 30f, entry, baseline.Surface));
        foreach (var distance in StraightDistances)
            straights.Add(Straight($"distance/{CanonicalNumber(distance, "000")}", 50f, 0.5f, distance, 16f, baseline.Surface));
        foreach (var skill in SkillValues)
            straights.Add(Straight($"speed/{CanonicalNumber(skill, "000")}", skill, 0.5f, 30f, 16f, baseline.Surface));
        foreach (var distance in GearingDistances)
        foreach (var gearing in GearingValues)
            straights.Add(Straight(
                $"gearing/{CanonicalNumber(gearing, "0.0")}/distance/{CanonicalNumber(distance, "000")}",
                50f, gearing, distance, 16f, baseline.Surface));

        var forces = new List<LongitudinalForceObservation>();
        var equilibria = new List<LongitudinalEquilibriumObservation>();
        foreach (var skill in new[] { 0f, 50f, 100f })
        foreach (var gearing in GearingValues)
        {
            var skills = Skills(speed: skill);
            var setup = new BikeSetup(gearing, 0.5f);
            var referenceForce = LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(
                skills, setup, baseline.Surface);
            foreach (var speed in ForceSpeeds)
            {
                var envelope = LongitudinalDynamics.CalculatePositiveDriveEnvelopeMultiplier(speed, setup);
                var drive = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(referenceForce, speed, setup);
                var resistance = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed);
                var net = drive - resistance;
                forces.Add(new(skill, gearing, speed, envelope, drive, resistance, net,
                    net / LongitudinalDynamics.ProvisionalNominalSystemMassKilograms,
                    drive * speed));
            }

            var equilibrium = LongitudinalDynamics.CalculateFullDriveEquilibriumSpeedMetersPerSecond(referenceForce, setup);
            var below = MathF.Max(0f, equilibrium - 0.5f);
            var above = equilibrium + 0.5f;
            equilibria.Add(new(skill, gearing, equilibrium, below,
                NetForce(below, referenceForce, setup), NetForce(equilibrium, referenceForce, setup),
                above, NetForce(above, referenceForce, setup)));
        }

        var definitions = CalibrationScenarioCatalog.RequiredScenarios();
        var turnExits = definitions.OfType<CalibrationTurnScenario>()
            .Where(item => item.Metadata.ScenarioId.StartsWith("turn_exit/speed/", StringComparison.Ordinal))
            .Select(item => (CalibrationTurnResult)CalibrationScenarioSuite.RunScenario(item))
            .Select(FlattenTurnExit).ToArray();
        var starts = definitions.OfType<CalibrationStartScenario>()
            .Where(item => item.Metadata.ScenarioId == "start/pure_launch"
                || item.Metadata.ScenarioId.StartsWith("start/start_skill/", StringComparison.Ordinal))
            .Select(item => (CalibrationStartResult)CalibrationScenarioSuite.RunScenario(item))
            .Select(FlattenStart).ToArray();
        var heatDefinitions = definitions.OfType<CalibrationHeatScenario>()
            .Where(item => item.Metadata.ScenarioId.StartsWith("full_heat/speed/", StringComparison.Ordinal)
                || item.Metadata.ScenarioId == "full_heat/baseline"
                || item.Metadata.ScenarioId.StartsWith("full_heat/within_heat/", StringComparison.Ordinal))
            .ToArray();
        var heatResults = heatDefinitions.Select(item => CalibrationScenarioSuite.RunHeat(item)).ToArray();
        var heatRiders = heatResults
            .Where(result => result.Metadata.ScenarioId.StartsWith("full_heat/speed/", StringComparison.Ordinal))
            .SelectMany(result => result.Riders.Select(rider => FlattenHeat(result, rider))).ToArray();
        var withinHeat = heatResults.Select(result => new LongitudinalWithinHeatObservation(
            result.Metadata.ScenarioId, result.Spreads.HeatTimeSeconds, result.Spreads.VmaxKph,
            result.Spreads.L1Seconds, result.Spreads.AverageSpeedMetersPerSecond)).ToArray();

        var corners = new List<LongitudinalCornerObservation>();
        AddCorner("baseline", 50f, 50f);
        foreach (var skill in new[] { 0f, 50f, 100f })
            AddCorner($"speed/{CanonicalNumber(skill, "000")}", skill, 50f);
        foreach (var control in new[] { 0f, 50f, 100f })
            AddCorner($"slide/{CanonicalNumber(control, "000")}", 50f, control);

        return new LongitudinalCalibrationSnapshot(sourceSha, Constants(),
            straights.OrderBy(item => item.ScenarioId, StringComparer.Ordinal).ToArray(),
            forces.ToArray(), equilibria.ToArray(), ObserveGearingCrossover(baseline.Surface),
            turnExits, starts, heatRiders, withinHeat, corners.ToArray());

        void AddCorner(string id, float speed, float slide)
        {
            var fixture = baseline with { Skills = Skills(speed: speed, slide: slide) };
            var capability = CalibrationScenarioSuite.ObserveCornerCapability(fixture, 1f);
            corners.Add(new(id, speed, slide, capability.MaxSafeSpeedMetersPerSecond,
                capability.CorrectionDecelerationMetersPerSecondSquared,
                capability.FirstBrakeSpeedMetersPerSecond, capability.FirstRunWideSpeedMetersPerSecond,
                capability.FirstCrashSpeedMetersPerSecond, capability.ObservedRunWideOverspeedRetention));
        }
    }

    private static LongitudinalStraightObservation Straight(string id, float speedSkill, float gearing,
        float distance, float entry, TrackSurfaceState surface)
    {
        var skills = Skills(speed: speedSkill);
        var setup = new BikeSetup(gearing, 0.5f);
        var profile = LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(entry, skills, setup, surface,
            LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(skills, surface), distance);
        var referenceForce = LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(skills, setup, surface);
        return new(id, speedSkill, gearing, distance, entry, profile.ExitSpeedMetersPerSecond,
            profile.PeakSpeedMetersPerSecond, profile.TravelTimeSeconds, profile.AccelerationDistanceMeters,
            profile.CruiseDistanceMeters, profile.DecelerationDistanceMeters, referenceForce,
            LongitudinalDynamics.CalculateStraightNetAccelerationMetersPerSecondSquared(entry, skills, setup, surface),
            profile.FullDriveEquilibriumSpeedMetersPerSecond!.Value);
    }

    private static LongitudinalTurnExitObservation FlattenTurnExit(CalibrationTurnResult result)
    {
        var correction = result.Diagnostics.CornerSpeedCorrectionProfile;
        var drive = result.Diagnostics.TurnExitDriveProfile;
        var speedSkill = result.Scenario.Fixture.Skills.Speed;
        return new(result.Metadata.ScenarioId, speedSkill, result.Change.Outcome.ToString(), result.AvailableDistanceMeters,
            result.Change.EntrySpeed, result.Capability.MaxSafeSpeedMetersPerSecond, correction?.TargetSpeedMetersPerSecond,
            correction?.ExitSpeedMetersPerSecond, correction?.CorrectionDistanceMeters ?? 0f,
            correction?.RemainingDistanceMeters ?? (drive is null ? 0f : result.Diagnostics.TravelledMeters),
            drive is null ? null : correction?.ExitSpeedMetersPerSecond ?? result.Change.PhysicsSpeed,
            drive?.ExitSpeedMetersPerSecond, drive?.PeakSpeedMetersPerSecond, drive?.TravelTimeSeconds,
            drive?.EntryNetAccelerationMetersPerSecondSquared, drive?.FullDriveEquilibriumSpeedMetersPerSecond,
            result.Change.Speed);
    }

    private static LongitudinalStartObservation FlattenStart(CalibrationStartResult result)
    {
        var profile = result.Profile;
        return new(result.Metadata.ScenarioId, result.Scenario.Mode.ToString(), result.Scenario.Fixture.Skills.Start,
            profile.ReactionTimeSeconds, profile.MovementTimeSeconds, profile.TotalTimeSeconds,
            profile.TimeTo70KphSeconds, profile.SpeedAtTwoSecondsMetersPerSecond,
            profile.ExitSpeedMetersPerSecond, profile.PeakSpeedMetersPerSecond,
            profile.AccelerationDistanceMeters, profile.CruiseDistanceMeters, profile.PreparationDistanceMeters,
            profile.EntryNetAccelerationMetersPerSecondSquared, profile.FullDriveEquilibriumSpeedMetersPerSecond);
    }

    private static LongitudinalHeatRiderObservation FlattenHeat(
        CalibrationHeatResult result, CalibrationHeatRiderObservation rider)
    {
        var performance = rider.Performance;
        return new(result.Metadata.ScenarioId, result.Scenario.Fixture.Skills.Speed, performance.RiderId,
            CalibrationUnits.MetersPerSecondToKph(performance.MaximumSpeedMetersPerSecond),
            performance.AverageSpeedMetersPerSecond!.Value, performance.L1Seconds, performance.L2Seconds,
            performance.L3Seconds, performance.L4Seconds, performance.FlyingLapMedianSeconds,
            performance.FirstLapPenaltySeconds, performance.TotalTimeSeconds, performance.TotalDistanceMeters,
            performance.RunWideCount, performance.BrakeCount, performance.CrashCount);
    }

    private static LongitudinalGearingCrossoverObservation ObserveGearingCrossover(TrackSurfaceState surface)
    {
        var skills = Skills(speed: 50f);
        var low = new BikeSetup(0f, 0.5f);
        var high = new BikeSetup(1f, 0.5f);
        var lowForce = LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(skills, low, surface);
        var highForce = LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(skills, high, surface);
        var lowerSpeed = LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond;
        var upperSpeed = 60f;
        for (var iteration = 0; iteration < 64 && upperSpeed - lowerSpeed > 1e-6f; iteration++)
        {
            var middle = (lowerSpeed + upperSpeed) * 0.5f;
            var difference = LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(lowForce, middle, low)
                - LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(highForce, middle, high);
            if (difference > 0f) lowerSpeed = middle; else upperSpeed = middle;
        }

        var lowerDistance = 1f;
        var upperDistance = 600f;
        for (var iteration = 0; iteration < 64 && upperDistance - lowerDistance > 1e-4f; iteration++)
        {
            var middle = (lowerDistance + upperDistance) * 0.5f;
            var lowExit = Straight("low", 50f, 0f, middle, 16f, surface).ExitSpeed;
            var highExit = Straight("high", 50f, 1f, middle, 16f, surface).ExitSpeed;
            if (lowExit > highExit) lowerDistance = middle; else upperDistance = middle;
        }
        return new((lowerSpeed + upperSpeed) * 0.5f, (lowerDistance + upperDistance) * 0.5f);
    }

    private static float NetForce(float speed, float referenceForce, BikeSetup setup)
        => LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(referenceForce, speed, setup)
            - LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed);

    private static RiderSkills Skills(float speed = 50f, float slide = 50f)
        => new(50f, speed, slide, 50f, 50f, 50f);

    private static string CanonicalNumber(float value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);

    private static LongitudinalCalibrationConstants Constants() => new(
        LongitudinalDynamics.MinStraightAccelerationMetersPerSecondSquared,
        LongitudinalDynamics.MaxStraightAccelerationMetersPerSecondSquared,
        LongitudinalDynamics.MinTurnExitAccelerationMetersPerSecondSquared,
        LongitudinalDynamics.MaxTurnExitAccelerationMetersPerSecondSquared,
        LongitudinalDynamics.ProvisionalDriveOrientedForceFadePerMeterPerSecond,
        LongitudinalDynamics.ProvisionalSpeedOrientedForceFadePerMeterPerSecond,
        LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond,
        LongitudinalDynamics.ProvisionalNominalSystemMassKilograms,
        LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons,
        LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient,
        LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters,
        LongitudinalDynamics.LowGearingDriveMultiplier,
        LongitudinalDynamics.HighGearingDriveMultiplier,
        LongitudinalDynamics.MinSurfaceDriveMultiplier,
        LongitudinalDynamics.SurfaceDriveMultiplierRange,
        LongitudinalDynamics.ProvisionalStandingStartSlowReactionSeconds,
        LongitudinalDynamics.ProvisionalStandingStartFastReactionSeconds,
        LongitudinalDynamics.ProvisionalStandingStartMinimumReferenceAccelerationMetersPerSecondSquared,
        LongitudinalDynamics.ProvisionalStandingStartMaximumReferenceAccelerationMetersPerSecondSquared,
        LongitudinalDynamics.MinCornerEntryDecelerationMetersPerSecondSquared,
        LongitudinalDynamics.MaxCornerEntryDecelerationMetersPerSecondSquared);

    private static void ValidateSha(string sha)
    {
        if (sha is null || sha.Length != 40 || sha.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("A full 40-character source SHA is required.", nameof(sha));
    }
}
