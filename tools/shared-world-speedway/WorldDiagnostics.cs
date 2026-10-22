using System.Numerics;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Optional read-only instrumentation. No sampling, decisions or runtime state changes.</summary>
public sealed class WorldDiagnostics
{
    public Action<ForceObservation>? Force { get; init; }
    public Action<ContactPointObservation>? ContactPoint { get; init; }
    public Action<ConsequenceObservation>? Consequence { get; init; }
    public Action<ContestObservation>? Contest { get; init; }
    public Action<ThrottleObservation>? Throttle { get; init; }
    public Action<AutonomousLineObservation>? AutonomousLine { get; init; }
    public Action<SlipObservation>? Slip { get; init; }
    public Action<YawLimitObservation>? YawLimit { get; init; }
    public Action<AppliedForceObservation>? AppliedForce { get; init; }
    public Action<RearWheelObservation>? RearWheel { get; init; }
    public Action<CoupledSlipInverseObservation>? CoupledSlipInverse { get; init; }
    public Action<TwoContactObservation>? TwoContact { get; init; }
    public Action<AdaptiveRiderDecision>? AdaptiveRider {get;init;}
    public Action<CornerEntryDecision>? CornerEntry {get;init;}
    public Action<PreparedCornerReview>? PreparedCorner {get;init;}
}
/// <summary>One accepted wheel interval; Before/After are actual Box2D poses.</summary>
public readonly record struct RearWheelObservation(double TimeSeconds,int RiderId,RiderView Before,RiderView After,RearWheelStep Step);
/// <summary>Read-only stages of the existing ON actuator; sampled at the pre-step accepted state.</summary>
public readonly record struct YawLimitObservation(double TimeSeconds, int RiderId,
    float RequestedNewtonMeters, float AfterAuthorityNewtonMeters, float AfterEnergyNewtonMeters,
    float OrientationErrorRadians, float AngularVelocity, float InertiaKgMetersSquared,
    double DissipationCreditWatts);
/// <summary>Applied wrench: direct center force/torque OFF; summed point forces and their r×F ON.
/// In the point variant this torque is observation only, never an additional ApplyTorque.</summary>
public readonly record struct AppliedForceObservation(double TimeSeconds, int RiderId,
    Vector2 ForceNewtons, float TorqueNewtonMeters);
public readonly record struct SlipObservation(double TimeSeconds, int RiderId, float SpeedMetersPerSecond,
    float ActualSlipDegrees, float TargetSlipDegrees, float HeadingRadians, float TravelHeadingRadians,
    float DriveForceNewtons, float LateralForceNewtons, float GripBudgetNewtons, float GripUtilization,
    float Drive, double OffsetMeters, float TargetOffsetMeters, float TorqueNewtonMeters,
    float LateralLossWatts, float ResistanceLossWatts, float TranslationalPowerWatts, float YawPowerWatts);
public readonly record struct ContestObservation(double TimeSeconds, int RiderId, int OpponentId,
    float TargetMeters, bool Available, double BaselineReferenceSpeed, double? CandidateReferenceSpeed,
    double OwnReferenceSpeed, double OpponentReferenceSpeed, double RequiredGain);
public readonly record struct ForceObservation(double TimeSeconds, RiderView Rider, Vector2 ForceNewtons,
    float TorqueNewtonMeters, float DesiredInwardNewtons, float AppliedInwardNewtons,
    float? GripBudgetNewtons, float TangentialNewtons, string Phase);
public readonly record struct ContactPointObservation(double TimeSeconds, float StepSeconds, int RiderA, int RiderB,
    BikeComponent ComponentA, BikeComponent ComponentB, Vector2 PointMeters, Vector2 NormalAtoB,
    float SeparationMeters, float NormalVelocityMetersPerSecond, float NormalImpulseNewtonSeconds,
    float TotalNormalImpulseNewtonSeconds, float TangentImpulseNewtonSeconds, bool Persisted,
    double PreStepPointNormalVelocityMetersPerSecond);
public readonly record struct ConsequenceObservation(double TimeSeconds, int RiderId, int OpponentId,
    double ClosingSpeed, double ImpulseNewtonSeconds, double Reserve, double SeverityRatio,
    PhysicalContactSeverity Severity);
