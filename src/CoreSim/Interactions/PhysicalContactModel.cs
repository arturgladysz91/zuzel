using CoreSim.PhysicalSpace;

namespace CoreSim.Interactions;

public enum PhysicalContactDiagnosticsLevel { None, Summary, FullAudit }
public enum PhysicalContactSeverity { Brush, Disturbed, LostRhythm, MajorSave, Crash }
public enum PhysicalContactStatus { Analyzed, IneligibleContact, GeometryUnresolved, DeferredByEarlierContact }
public enum ContactNormalSource { ClosestComponentAxes, BikeCenters, RelativeVelocity, ReferenceLateralAxisUnresolved, RelativePositionAtOnset }
public enum LegacyContactOutcome { NotAuthorized, NoLegacyOccurrence, LegacyLostRhythm, LegacyCrash }

/// <summary>PROVISIONAL analytical calibration. No coefficient controls production motion.</summary>
public sealed record PhysicalContactParameters
{
    public double ReferenceBikeMassKg { get; init; } = 77;
    public double Restitution { get; init; } = 0;
    public double MaximumReferenceClosingSpeedMetersPerSecond { get; init; } = 50;
    public double ContactSimultaneityWindowSeconds { get; init; } = .04;
    public double ForwardDisturbanceWeight { get; init; } = 1;
    public double LateralDisturbanceWeight { get; init; } = 2;
    public double YawDisturbanceWeight { get; init; } = 1.5;
    public double RotationDisturbanceWeight { get; init; } = .5;
    public double BaseStabilityReserveMetersPerSecond { get; init; } = 1;
    public double TechniqueFactorMinimum { get; init; } = .75;
    public double TechniqueFactorMaximum { get; init; } = 1.25;
    public double StrengthFactorMinimum { get; init; } = .80;
    public double StrengthFactorMaximum { get; init; } = 1.20;
    public double ConditionFactorMinimum { get; init; } = .90;
    public double ConditionFactorMaximum { get; init; } = 1.05;
    public double GripFactorMinimum { get; init; } = .90;
    public double GripFactorMaximum { get; init; } = 1.10;
    public double BrushThreshold { get; init; } = .35;
    public double DisturbedThreshold { get; init; } = .70;
    public double LostRhythmThreshold { get; init; } = 1.05;
    public double MajorSaveThreshold { get; init; } = 1.55;
    public void Validate()
    {
        foreach (var value in new[] { ReferenceBikeMassKg, MaximumReferenceClosingSpeedMetersPerSecond,
            ContactSimultaneityWindowSeconds, ForwardDisturbanceWeight, LateralDisturbanceWeight,
            YawDisturbanceWeight, RotationDisturbanceWeight, BaseStabilityReserveMetersPerSecond,
            TechniqueFactorMinimum, TechniqueFactorMaximum, StrengthFactorMinimum, StrengthFactorMaximum,
            ConditionFactorMinimum, ConditionFactorMaximum, GripFactorMinimum, GripFactorMaximum,
            BrushThreshold, DisturbedThreshold, LostRhythmThreshold, MajorSaveThreshold })
            GeometryValidation.Positive(value, nameof(value));
        GeometryValidation.Unit(Restitution, nameof(Restitution));
        if (LateralDisturbanceWeight <= ForwardDisturbanceWeight
            || TechniqueFactorMaximum < TechniqueFactorMinimum || StrengthFactorMaximum < StrengthFactorMinimum
            || ConditionFactorMaximum < ConditionFactorMinimum || GripFactorMaximum < GripFactorMinimum
            || !(BrushThreshold < DisturbedThreshold && DisturbedThreshold < LostRhythmThreshold
                && LostRhythmThreshold < MajorSaveThreshold))
            throw new ArgumentException("Reserve ranges, disturbance weights and provisional thresholds must be ordered.");
    }
}

/// <summary>Canonical immutable physiology and sampled frozen surface, independent from legacy execution skills.</summary>
public sealed record PhysicalContactRiderInput(int RiderId, RiderGameplayProfile Profile, double Condition, double EffectiveGrip)
{
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Profile);
        GeometryValidation.Unit(Condition, nameof(Condition)); GeometryValidation.Unit(EffectiveGrip, nameof(EffectiveGrip));
    }
}
public sealed record RiderLegacyContactObservation(int RiderId, LegacyContactOutcome Outcome);
/// <summary>All velocities and poses describe the same verified first touch, before legacy consequences.</summary>
public sealed record PhysicalContactInput(ContestedSpaceEvent Contact, PhysicalBikePose? PoseA, PhysicalBikePose? PoseB,
    MeterPoint VelocityA, MeterPoint VelocityB, PhysicalContactRiderInput RiderA, PhysicalContactRiderInput RiderB,
    long? EpisodeId = null, IReadOnlyList<long>? OriginEpisodeIds = null, bool LegacyFallbackAuthorized = false,
    IReadOnlyList<RiderLegacyContactObservation>? LegacyResults = null);

public sealed record MechanicalContactManifold(int RiderA, int RiderB, BikeComponent ComponentA, BikeComponent ComponentB,
    MeterPoint ContactPointA, MeterPoint ContactPointB, MeterPoint MidContactPoint, MeterPoint NormalAtoB,
    MeterPoint LeverArmA, MeterPoint LeverArmB, double SignedSeparationMeters, ContactNormalSource NormalSource);
public sealed record PhysicalContactImpulse(double MassAKg, double MassBKg, double ReducedMassKg,
    MeterPoint RelativeVelocityMetersPerSecond, double NormalClosingSpeedMetersPerSecond,
    double ReferenceClosingSpeedMetersPerSecond, double RotationalClosureEquivalentMetersPerSecond,
    double RotationOnsetIntervalSeconds, bool RotationOnsetAvailable, double ImpulseMagnitudeNewtonSeconds,
    MeterPoint ImpulseOnANewtonSeconds, MeterPoint ImpulseOnBNewtonSeconds,
    MeterPoint DeltaVAMetersPerSecond, MeterPoint DeltaVBMetersPerSecond,
    double PreNormalKineticEnergyJoules, double PostNormalKineticEnergyJoules,
    double PreNormalMomentumKgMetersPerSecond, double PostNormalMomentumKgMetersPerSecond);
public sealed record RiderContactDemand(int RiderId, double ForwardDeltaVelocityMetersPerSecond,
    double LateralDeltaVelocityMetersPerSecond, double NormalizedYawLever,
    double YawEquivalentMetersPerSecond, double ForwardDemand, double LateralDemand,
    double YawDemand, double RotationDemand, double PairDemandSquared, double PairDemand);
public sealed record RiderStabilityReserve(double TechniqueFactor, double StrengthFactor, double ConditionFactor,
    double GripFactor, double StabilityReserveMetersPerSecond);
public sealed record PhysicalContactPairAnalysis(int RiderA, int RiderB, long? EpisodeId, IReadOnlyList<long> OriginEpisodeIds,
    double FirstTouchCommonTimeSeconds, PhysicalContactStatus Status, string Reason, bool LegacyFallbackAuthorized,
    IReadOnlyList<RiderLegacyContactObservation> LegacyResults, MechanicalContactManifold? Manifold = null,
    PhysicalContactImpulse? Impulse = null, RiderContactDemand? DemandA = null, RiderContactDemand? DemandB = null)
{
    public bool DeferredByEarlierContact => Status == PhysicalContactStatus.DeferredByEarlierContact;
    // Internal frontier provenance does not extend frozen #56C1 JSON/IEEE contracts.
    internal double? FrontierStartTimeSeconds { get; init; }
    internal IReadOnlyList<int> FrontierRiderIds { get; init; } = Array.Empty<int>();
}
public sealed record RiderContactAnalysis(int RiderId, double TotalMassKg, MeterPoint NetImpulseNewtonSeconds,
    MeterPoint NetDeltaVelocityMetersPerSecond, double CombinedStabilityDemand, RiderStabilityReserve Reserve,
    double SeverityRatio, PhysicalContactSeverity ProvisionalSeverity);
public sealed record PhysicalContactPairSummary(int RiderA, int RiderB, long? EpisodeId, double FirstTouchCommonTimeSeconds,
    PhysicalContactStatus Status, string Reason, bool LegacyFallbackAuthorized, double? PairDemandA, double? PairDemandB,
    double? SeverityRatioA, double? SeverityRatioB, PhysicalContactSeverity? SeverityA, PhysicalContactSeverity? SeverityB,
    IReadOnlyList<RiderLegacyContactObservation> LegacyResults);
public sealed record RiderContactSummary(int RiderId, double CombinedStabilityDemand, double StabilityReserve,
    double SeverityRatio, PhysicalContactSeverity ProvisionalSeverity);
/// <summary>ContactGroups counts causal eligible frontiers submitted to analysis, including unresolved manifolds.</summary>
public sealed record PhysicalContactWork(int InputContacts, int UniquePairs, int ContactGroups,
    int ManifoldCalculations, int AnalyzedPairs, int DeferredContacts, int RejectedContacts);
public sealed record PhysicalContactAnalysis(PhysicalContactDiagnosticsLevel Level,
    IReadOnlyList<PhysicalContactPairSummary> Pairs, IReadOnlyList<RiderContactSummary> Riders,
    IReadOnlyList<PhysicalContactPairAnalysis> AuditPairs, IReadOnlyList<RiderContactAnalysis> AuditRiders, PhysicalContactWork Work)
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal IReadOnlyList<RiderContactAnalysis> ApplicationRiders { get; init; } = Array.Empty<RiderContactAnalysis>();
    [System.Text.Json.Serialization.JsonIgnore]
    internal IReadOnlyList<PhysicalContactInput> SourceInputs { get; init; } = Array.Empty<PhysicalContactInput>();

    internal static PhysicalContactAnalysis Empty(PhysicalContactDiagnosticsLevel level) => level switch
    { PhysicalContactDiagnosticsLevel.FullAudit => EmptyAudit, PhysicalContactDiagnosticsLevel.Summary => EmptySummary, _ => EmptyNone };
    private static readonly PhysicalContactAnalysis EmptyNone = CreateEmpty(PhysicalContactDiagnosticsLevel.None);
    private static readonly PhysicalContactAnalysis EmptySummary = CreateEmpty(PhysicalContactDiagnosticsLevel.Summary);
    private static readonly PhysicalContactAnalysis EmptyAudit = CreateEmpty(PhysicalContactDiagnosticsLevel.FullAudit);
    private static PhysicalContactAnalysis CreateEmpty(PhysicalContactDiagnosticsLevel level)
        => new(level, Array.Empty<PhysicalContactPairSummary>(), Array.Empty<RiderContactSummary>(),
            Array.Empty<PhysicalContactPairAnalysis>(), Array.Empty<RiderContactAnalysis>(), new(0,0,0,0,0,0,0));
}
