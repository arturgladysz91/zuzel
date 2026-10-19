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
}
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
