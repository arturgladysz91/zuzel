using System.Numerics;
using CoreSim;
using CoreSim.Setup;

namespace SharedWorldSpeedway;

// New 2D tyre/controller parameters are PROVISIONAL, not a telemetry calibration.
public sealed record WorldParameters
{
    public bool EnableTacticalRacing { get; init; }
    public bool EnableSideBySideContests { get; init; }
    public bool EnableTacticalQualityScoring { get; init; }
    public bool EnableProportionalTrafficThrottle { get; init; }
    public bool EnableClosureAdaptiveFollowingGap { get; init; }
    public bool EnableAutonomousRacingLine { get; init; }
    public bool EnableSlipCoupledDynamics { get; init; }
    public bool EnableRearWheelDynamics { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableCoupledSlipInverse { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableFullContactYawFeedForward { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableTwoContactBikeDynamics { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnablePhysicalSteeringControl { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableTravelDirectionSteering {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableReachableTravelDirectionSteering {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableFeasibleCornerEntry {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnablePreparedCornerPlan {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableContinuousCornerPlanning {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnablePredictiveRiderControl {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableFreeRacing {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableFreeRacingLearning {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableDynamicAxleLoad { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableAdaptiveRiderControl {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Enable500CcEngine {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableLoadBasedTyreGrip {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Engine500Parameters? Engine500 {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LoadBasedTyreParameters? LoadBasedTyres {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? AxleLoadHeightMeters {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TwoContactBikeParameters? TwoContactBike { get; init; }
    // #102 tools-only authored controls. Historical worlds keep their original bounds.
    public bool EnableLocalOffsetLimit { get; init; }
    public RearWheelParameters? RearWheel { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ExperimentalDriveEnvelope? ExperimentalDrive {get;init;}
    public float SlipYawResponseSeconds { get; init; } = .1f;
    // Audit-only variant 2; absent from the public experiment configuration.
    internal (double BaseMeters, double HeadwaySeconds)? DiagnosticFollowingGap { get; init; }
    public double AiIntervalSeconds { get; init; } = .1;
    public float LateralResponseSeconds { get; init; } = .65f;
    public float YawResponseSeconds { get; init; } = .35f;
    public float SlideAngleRadians { get; init; } = MathF.PI / 15; // 12 degrees at full corner curvature
    public float ContactFriction { get; init; } = .2f;
    public float NearbyMaximumTravelMeters { get; init; } = (float)CoreSim.PhysicalSpace.SpeedwayBikeDimensions.Reference.HandlebarTubeDiameterMeters / 2;
    public double PredictionSeconds { get; init; } = 1.1;
    public double RecoverySeconds { get; init; } = .5;
    public int SolverSubsteps { get; init; } = 4;
}
public enum RiderActivity { Racing, Disturbed, Crashed, Finished }
public enum RacingContext { StartStraight, FirstBend, NormalRacing }
public enum TrafficAction { KeepIntent, HoldLine, YieldOutward, BackOut, AttackInside, AttackOutside }
public enum RacingIntent { None, AttackInside, AttackOutside, DefendLine, ReturnLine, ContestInside, ContestOutside, ContestHold }
public sealed class TacticalMemory
{
    public RacingIntent Intent;
    public int OpponentId;
    public float HomeOffsetMeters;
    public float AutonomousOffsetMeters;
    public AutonomousLinePhase AutonomousPhase;
    public double AutonomousReviewAt;
    public double ActivatedAt, NextTacticAt, SafetyUntil, ClearSince;
    public int FollowingOpponentId;
    public double ThrottleObservedAt, ObservedOpponentSpeed;
}
public readonly record struct RacingPair(int A, int B);
public sealed record RiderSpec(RiderProfile Profile, BikeSetup Setup, double ReferenceMeters,
    float OffsetMeters, float SpeedMetersPerSecond, float TargetOffsetMeters,
    float InitialOutwardSpeed = 0, bool TrafficEnabled = true, bool Passive = false, bool Bullet = false);
public readonly record struct RiderView(int Id, Vector2 Position, Vector2 Velocity, float Heading,
    float AngularVelocity, RoadPoint Road, double TotalReferenceMeters, double DistanceMeters,
    RiderActivity Activity, float Drive, float TargetOffsetMeters, float Impairment,
    RacingContext Context = RacingContext.NormalRacing)
{
    public float Speed => Velocity.Length();
    public float ForwardSpeed => Vector2.Dot(Velocity, new(MathF.Cos(Heading), MathF.Sin(Heading)));
    public float SideSpeed => Vector2.Dot(Velocity, new(-MathF.Sin(Heading), MathF.Cos(Heading)));
    public float SlideRadians => Speed < .01f ? 0 : (float)CoreSim.PhysicalSpace.BikeAngles.Wrap(Heading - Math.Atan2(Velocity.Y, Velocity.X));
}
public sealed record WorldSnapshot(double TimeSeconds, IReadOnlyList<RiderView> Riders)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RearWheelView>? RearWheels { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SteeringView>? Steering { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public AxleLoad? AxleLoad {get;init;}
}
public enum ControlChannels { Both, Line, Throttle }
public sealed record DecisionEvent(int RiderId, int OpponentId, TrafficAction Action,
    double ObservedAt, double DecisionAt, double RequestedActivation, double? ActualActivation,
    float TargetOffsetMeters, float Drive, RacingIntent Intent = RacingIntent.None,
    string Reason = "", bool SafetyReaction = false, double? CancelledAt = null,
    ControlChannels Channels = ControlChannels.Both);
/// <summary>Authored before the run, for controlled experiments; never a predicted state.</summary>
public sealed record ControlCommand(int RiderId, double AtSeconds, float TargetOffsetMeters, float Drive);
/// <summary>Offline authored landmark, activated on its first accepted crossing; no future body state.</summary>
public sealed record SpatialControlCommand(int RiderId, double ReferenceMeters, float TargetOffsetMeters, float Drive,
    float? LocalOuterFraction=null);
public sealed record ContactEvent(int RiderA, int RiderB, double TimeSeconds, string Kind,
    double ImpulseNewtonSeconds, double SeverityRatioA, double SeverityRatioB, double NormalClosingSpeed = 0);
public sealed class WorldWork
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int CornerPlansPrepared {get;set;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int CornerPlansActivated {get;set;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long PreparedReviews {get;set;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long PreparedContactQueries {get;set;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double PreparedMilliseconds {get;set;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long CornerEntryReviews;
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long AdaptiveReviews;
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long AdaptivePredictionForces;
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long AdaptiveContactQueries;
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double AdaptiveElapsedMilliseconds;
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long AxleLoadUpdates;
    public long PhysicsUpdates, SolverCalls, AiUpdates, PairChecks, Threats, ContactManifolds, SolvedPoints,
        BeginShapes, EndShapes, HitShapes, Episodes, Consequences, GeometryChecks, ProximityPairChecks,
        SolverSubsteps, ContactCandidateSamples, MaximumContactCandidates;
    public long RearWheelForceEvaluations,RearWheelBoundaryEvaluations,RearWheelCommits;
    public double SolverElapsedMilliseconds, ContactProcessingMilliseconds, SolverProfileMilliseconds,
        BroadPhaseProfileMilliseconds, NarrowPhaseProfileMilliseconds, ConstraintProfileMilliseconds;
    public double MinimumGapMeters = double.PositiveInfinity, MaximumSpeed, MaximumStepDisplacementMeters,
        MinimumTrackClearanceMeters = double.PositiveInfinity, MaximumPassiveEnergyIncreaseJoules;
    public long TacticalPairObservations, TacticalObservations, TacticalCandidates, TacticalDecisions,
        ControlChanges, CancelledCommands, MaximumTacticalPairs;
    public long SideObservations, SideCandidates, SideBlockedCandidates, SideHoldChoices;
    public long QualitySamples;
    public long ThrottleObservations, ThrottleForceEvaluations;
    // Observation only: existing partitions, no new solver boundaries.
    public long ProximityTicks, ProximityRequestedExtraCalls, ControlDeadlineLimitedCalls,
        ThrottleDeadlineLimitedCalls, CurvatureLimitedCalls, ActivatedThrottleCommands;
    public double ProximitySeconds;
    public long AutonomousObservations, AutonomousEvaluations, AutonomousCandidates, AutonomousBlockedCandidates,
        AutonomousCommands, AutonomousActivations;
    public double AutonomousElapsedMilliseconds;
    public double TacticalElapsedMilliseconds;
}
public sealed record RunResult(string Name, double StepSeconds, double TimeSeconds, WorldWork Work,
    IReadOnlyList<RiderView> Riders, IReadOnlyDictionary<int, double> FinishTimes,
    IReadOnlyList<DecisionEvent> Decisions, IReadOnlyList<ContactEvent> Contacts,
    string Fingerprint, bool Completed)
{
    public IReadOnlyList<FinishRow> Classification => Riders.OrderBy(v => FinishTimes.ContainsKey(v.Id) ? 0
            : v.Activity == RiderActivity.Crashed ? 2 : 1)
        .ThenBy(v => FinishTimes.GetValueOrDefault(v.Id, double.PositiveInfinity))
        .ThenByDescending(v => v.TotalReferenceMeters).ThenBy(v => v.Id)
        .Select((v, i) => new FinishRow(i + 1, v.Id, FinishTimes.TryGetValue(v.Id, out var t) ? t : null,
            FinishTimes.ContainsKey(v.Id) ? "Finished" : v.Activity.ToString())).ToArray();
}
public sealed record FinishRow(int Position, int RiderId, double? FinishTimeSeconds, string Status);
