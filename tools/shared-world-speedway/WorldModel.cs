using System.Numerics;
using CoreSim;
using CoreSim.Setup;

namespace SharedWorldSpeedway;

// New 2D tyre/controller parameters are PROVISIONAL, not a telemetry calibration.
public sealed record WorldParameters
{
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
public enum TrafficAction { KeepIntent, HoldLine, YieldOutward, BackOut }
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
public sealed record WorldSnapshot(double TimeSeconds, IReadOnlyList<RiderView> Riders);
public sealed record DecisionEvent(int RiderId, int OpponentId, TrafficAction Action,
    double ObservedAt, double DecisionAt, double RequestedActivation, double? ActualActivation,
    float TargetOffsetMeters, float Drive);
public sealed record ContactEvent(int RiderA, int RiderB, double TimeSeconds, string Kind,
    double ImpulseNewtonSeconds, double SeverityRatioA, double SeverityRatioB, double NormalClosingSpeed = 0);
public sealed class WorldWork
{
    public long PhysicsUpdates, SolverCalls, AiUpdates, PairChecks, Threats, ContactManifolds, SolvedPoints,
        BeginShapes, EndShapes, HitShapes, Episodes, Consequences, GeometryChecks, ProximityPairChecks,
        SolverSubsteps, ContactCandidateSamples, MaximumContactCandidates;
    public double SolverElapsedMilliseconds, ContactProcessingMilliseconds, SolverProfileMilliseconds,
        BroadPhaseProfileMilliseconds, NarrowPhaseProfileMilliseconds, ConstraintProfileMilliseconds;
    public double MinimumGapMeters = double.PositiveInfinity, MaximumSpeed, MaximumStepDisplacementMeters,
        MinimumTrackClearanceMeters = double.PositiveInfinity, MaximumPassiveEnergyIncreaseJoules;
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
