using System.Globalization;
using System.Text;
using System.Text.Json;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record TrajectoryIntent(string Name, int EntryLateral, int EntryTarget, int MiddleTarget, int ExitTarget);
public sealed record TrajectorySegmentObservation(int SegmentIndex, int RequestedTarget, int ResolvedTarget,
    float EntryLateral, float ExitLateral, float DistanceMeters, float TimeSeconds, int PathNodes);
public sealed record EntryAuditControl(string Name, float EntryLateral, float ExitLateral,
    float DistanceMeters, float TimeSeconds, float ExitSpeedMetersPerSecond,
    float MinimumRadiusMeters, float MaximumRadiusMeters, int PathNodes);
public sealed record SingleRiderTrajectoryResult(string Name, float EntrySpeedMetersPerSecond,
    float EntryLateral, float ApexLateral, float ExitLateral, float CornerDistanceMeters,
    float DistanceVsInsideMeters, float CornerTimeSeconds, float MinimumSpeedMetersPerSecond,
    float ApexSpeedMetersPerSecond, float ExitSpeedMetersPerSecond, float MinimumRadiusMeters,
    float ApexRadiusMeters, float MaximumRadiusMeters, float MaximumCurvaturePerMeter,
    float FollowingStraightTimeSeconds, float FollowingStraightExitSpeedMetersPerSecond,
    float CombinedTimeSeconds, float ApexSafeCapabilityMetersPerSecond,
    IReadOnlyList<TrajectorySegmentObservation> Segments);

/// <summary>21 named controls, no search. Resolves and commits actual production steps.</summary>
public static class SingleRiderTrajectoryBenchmark
{
    public const string BaselineHead = "83a67616973e5d4fbbc8060525ec5ffc629da734";
    public static IReadOnlyList<TrajectoryIntent> Intents { get; } = Array.AsReadOnly(new[]
    {
        new TrajectoryIntent("inside-hold", 0, 0, 0, 0), new TrajectoryIntent("mid-hold", 2, 2, 2, 2),
        new TrajectoryIntent("outside-hold", 4, 4, 4, 4), new TrajectoryIntent("inside-open", 0, 0, 0, 4),
        new TrajectoryIntent("mid-open", 2, 2, 1, 4), new TrajectoryIntent("wide-tight", 4, 1, 1, 0),
        new TrajectoryIntent("wide-tight-open", 3, 1, 1, 4),
    });

    public static IReadOnlyList<SingleRiderTrajectoryResult> Run()
    {
        var results = new List<SingleRiderTrajectoryResult>();
        foreach (var speed in new[] { 19f, 22f, 25f })
        {
            var group = Intents.Select(intent => Run(intent, speed)).ToArray();
            var inside = group.Single(r => r.Name == "inside-hold").CornerDistanceMeters;
            results.AddRange(group.Select(r => r with { DistanceVsInsideMeters = r.CornerDistanceMeters - inside }));
        }
        return results;
    }

    public static SingleRiderTrajectoryResult Run(TrajectoryIntent intent, float entrySpeed)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = new RiderState(RiderProfile.CreateDefault(1), intent.EntryLateral)
            { Speed = entrySpeed, ActiveSetup = BikeSetup.Neutral };
        rider.RestorePosition(RiderPosition.Create(1, 1, 0f, track.Segments.Count));
        var targets = new[] { intent.EntryTarget, intent.MiddleTarget, intent.ExitTarget, intent.ExitTarget };
        var engine = new SimulationEngine(new PhaseTargets(targets));
        var options = new HeatSimulationOptions { Laps = 1, IncidentFrequency = 0f, EnableLogging = false };
        var segments = new List<TrajectorySegmentObservation>();
        float distance = 0f, minimumSpeed = entrySpeed, minimumRadius = float.MaxValue, maximumRadius = 0f;
        float apexLateral = float.NaN, apexSpeed = float.NaN, apexRadius = float.NaN, apexSafe = float.NaN;
        float cornerTime = 0f, exitSpeed = 0f, exitLateral = 0f, straightTime = 0f;
        for (var index = 1; index <= 4; index++)
        {
            // Independent clean snapshot each step: controlled geometry mechanism, no weather/wear drift.
            var state = TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, .35f));
            var snapshot = engine.CaptureSnapshot(track, state, new[] { rider }, new SimulationStepContext(53, index - 1, 0, index, 0, 1));
            var resolved = engine.Resolve(snapshot, engine.Decide(snapshot), options);
            var change = resolved.Changes.Single(); var d = resolved.Diagnostics.Single();
            if (change.Status == RiderRaceStatus.Crashed) throw new InvalidOperationException("Controlled benchmark crashed.");
            segments.Add(new(index, targets[index - 1], change.Lane, rider.LateralPosition, change.LateralPosition,
                d.TravelledMeters, d.TravelTimeSeconds, d.ExecutedPath?.Nodes.Count ?? d.ContinuousCornerProfile?.Nodes.Count ?? 0));
            if (index <= 3)
            {
                distance += d.TravelledMeters; cornerTime += d.TravelTimeSeconds;
                minimumSpeed = MathF.Min(minimumSpeed, d.ContinuousCornerProfile!.MinimumSpeedMetersPerSecond);
                if (d.ExecutedPath is { } path)
                {
                    minimumRadius = MathF.Min(minimumRadius, path.MinimumRadiusMeters!.Value);
                    maximumRadius = MathF.Max(maximumRadius, path.MaximumRadiusMeters!.Value);
                    if (path.ApexNode is { } apex)
                    { apexLateral = apex.LateralPosition; apexRadius = apex.RadiusMeters!.Value; apexSpeed = apex.SpeedMetersPerSecond; apexSafe = apex.LocalSafeSpeedMetersPerSecond!.Value; }
                }
                else
                {
                    var radius = LaneModel.TurnArcRadiusMeters(rider.LateralPosition, track.Geometry);
                    minimumRadius = MathF.Min(minimumRadius, radius); maximumRadius = MathF.Max(maximumRadius, radius);
                    var apex = d.ContinuousCornerProfile.Nodes.FirstOrDefault(n => n.CornerProgress == .5f);
                    if (apex is not null)
                    { apexLateral = rider.LateralPosition; apexRadius = radius; apexSpeed = apex.SpeedMetersPerSecond;
                        apexSafe = SegmentPhysics.MaxSafeTurnSpeed(rider.LateralPosition, track.Geometry, d.EntrySurface, rider.Profile.Skills, rider.ActiveSetup); }
                }
                exitSpeed = change.Speed; exitLateral = change.LateralPosition;
            }
            else straightTime = d.TravelTimeSeconds;
            engine.Commit(resolved, new[] { rider }, state, new SimLog());
        }
        return new(intent.Name, entrySpeed, intent.EntryLateral, apexLateral, exitLateral, distance, 0f,
            cornerTime, minimumSpeed, apexSpeed, exitSpeed, minimumRadius, apexRadius, maximumRadius,
            1f / minimumRadius, straightTime, rider.Speed, cornerTime + straightTime, apexSafe, segments);
    }

    public static string Evidence(IReadOnlyList<SingleRiderTrajectoryResult> results)
        => JsonSerializer.Serialize(new { BaselineHead, Audit51 = RunAudit51(), Results = results }, new JsonSerializerOptions { WriteIndented = true }) + "\n";

    // Exact first-segment fixture from #51 D, isolated to rider 2 with incidents OFF.
    internal static ResolvedSimulationStep ResolveAudit51(int target)
    {
        var scenario = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "D");
        var rider = scenario.Riders.Single(r => r.Id == 2).Create(scenario.Track);
        var engine = new SimulationEngine(new AuditTarget(target));
        var snapshot = engine.CaptureSnapshot(scenario.Track, scenario.CreateSurface(), new[] { rider },
            new SimulationStepContext(53, 0, 0, 0, 0, 1));
        return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions { Laps = 1, IncidentFrequency = 0f });
    }

    public static IReadOnlyList<EntryAuditControl> RunAudit51()
        => new[] { ("entry-hold", 3), ("entry-inward", 0) }.Select(control =>
        {
            var step = ResolveAudit51(control.Item2); var d = step.Diagnostics.Single(); var change = step.Changes.Single();
            var radius = LaneModel.TurnArcRadiusMeters(3f, step.Snapshot.Track.Geometry);
            return new EntryAuditControl(control.Item1, 3f, change.LateralPosition, d.TravelledMeters,
                d.TravelTimeSeconds, change.Speed, d.ExecutedPath?.MinimumRadiusMeters ?? radius,
                d.ExecutedPath?.MaximumRadiusMeters ?? radius, d.ExecutedPath?.Nodes.Count ?? d.ContinuousCornerProfile!.Nodes.Count);
        }).ToArray();

    public static string Render(IReadOnlyList<SingleRiderTrajectoryResult> results)
    {
        var text = new StringBuilder("# Single-rider executed trajectory — Motoarena\n\n");
        text.Append("Base: `" + BaselineHead + "` after #52. Analysis only; no coefficients changed.\n\n");
        text.Append("One rider, neutral setup, six default skills, clean uniform surface reset before each segment, incidents OFF, traffic OFF. Actual SimulationEngine CaptureSnapshot → Decide → Resolve → Commit. Corner segments 1–3 and the 62 m following straight; its immediate next corner preparation remains active. The following straight requests the exit target. Requested integer targets are subject to the unchanged nearest-reference planner and bounded movement law.\n\n");
        text.Append("| intent | actual entry | entry/middle/exit requests |\n| --- | ---: | --- |\n");
        foreach (var intent in Intents) text.Append(CultureInfo.InvariantCulture, $"| {intent.Name} | {intent.EntryLateral} | {intent.EntryTarget}/{intent.MiddleTarget}/{intent.ExitTarget} |\n");
        foreach (var group in results.GroupBy(r => r.EntrySpeedMetersPerSecond))
        {
            text.Append(CultureInfo.InvariantCulture, $"\n## Entry {group.Key:F0} m/s\n\n");
            text.Append("| trajectory | actual entry/apex/exit | path m | Δ vs inside m | corner s | min/apex/exit m/s | radius min/apex/max m | max curvature 1/m | next straight s | next exit m/s | combined s |\n| --- | --- | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: |\n");
            foreach (var r in group) text.Append(CultureInfo.InvariantCulture,
                $"| {r.Name} | {r.EntryLateral:F3}/{r.ApexLateral:F3}/{r.ExitLateral:F3} | {r.CornerDistanceMeters:F3} | {r.DistanceVsInsideMeters:F3} | {r.CornerTimeSeconds:F4} | {r.MinimumSpeedMetersPerSecond:F3}/{r.ApexSpeedMetersPerSecond:F3}/{r.ExitSpeedMetersPerSecond:F3} | {r.MinimumRadiusMeters:F3}/{r.ApexRadiusMeters:F3}/{r.MaximumRadiusMeters:F3} | {r.MaximumCurvaturePerMeter:F6} | {r.FollowingStraightTimeSeconds:F4} | {r.FollowingStraightExitSpeedMetersPerSecond:F3} | {r.CombinedTimeSeconds:F4} |\n");
            var winner = group.MinBy(r => r.CombinedTimeSeconds)!;
            text.Append(CultureInfo.InvariantCulture, $"\nFastest combined control: **{winner.Name}**, {winner.CombinedTimeSeconds:F4} s.\n");
            foreach (var pair in new[] { ("inside-open", "inside-hold"), ("mid-open", "mid-hold") })
            {
                var opening = group.Single(r => r.Name == pair.Item1); var held = group.Single(r => r.Name == pair.Item2);
                text.Append(CultureInfo.InvariantCulture, $"\n{pair.Item1} vs {pair.Item2}: exit speed Δ {opening.ExitSpeedMetersPerSecond - held.ExitSpeedMetersPerSecond:+0.000;-0.000;0.000} m/s; combined time Δ {opening.CombinedTimeSeconds - held.CombinedTimeSeconds:+0.0000;-0.0000;0.0000} s. Actual apex local safe capability: {opening.ApexSafeCapabilityMetersPerSecond:F3} m/s.\n");
            }
        }
        text.Append("\n## Exact #51 first-segment control\n\nScenario D, rider 2 alone, 21 m/s, balanced skills, original 24 m inner radius and 14 m turn width, original uniform surface, incidents OFF. Target 3 vs 0; nearest-reference planning is unchanged. The historical report remains frozen; this is a new current-production replay.\n\n| control | actual entry/exit | distance m | time s | exit m/s | radius min/max m | nodes |\n| --- | --- | ---: | ---: | ---: | --- | ---: |\n");
        foreach (var r in RunAudit51()) text.Append(CultureInfo.InvariantCulture,
            $"| {r.Name} | {r.EntryLateral:F6}/{r.ExitLateral:F6} | {r.DistanceMeters:F6} | {r.TimeSeconds:F6} | {r.ExitSpeedMetersPerSecond:F6} | {r.MinimumRadiusMeters:F6}/{r.MaximumRadiusMeters:F6} | {r.PathNodes} |\n");
        text.Append("\n## Mechanism and limits\n\nChanging executed trajectories change physical distance, local radius/curvature, sampled surface and the consumed longitudinal steps. Inside-hold retains the shortest path. Opening is bounded: requested targets are not achieved trajectories. The JSON appendix retains every segment's requested/resolved target and actual endpoint, plus actual apex capability. Nonuniform surface-following and wear-budget controls are tested separately.\n\n");
        if (results.GroupBy(r => r.EntrySpeedMetersPerSecond).All(g => g.MinBy(r => r.CombinedTimeSeconds)!.Name == "inside-hold"))
            text.Append("**geometry/motion consistency alone does not produce an opening-line advantage** in these controls. No additional experiments or tuning were applied.\n\n");
        text.Append("The local moving envelope uses current radius/surface as a pointwise fixed-line continuation forecast. It shares ContinuousCornerEnvelope and LongitudinalDynamics primitives; it never substitutes an entry or mean radius for executed geometry. Future intent is not predicted. Apex stays at 0.5. Width mapping is segment-local (12/16.6 m); boundary offset changes add no teleport distance and no width-transition spline. Contact/common-time interactions and AdaptiveDecisionModel remain separate future work. Historical #47–#51 reports are frozen.\n");
        return text.ToString();
    }

    private sealed class PhaseTargets(int[] targets) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(targets[rider.SegmentIndex - 1], 0f);
    }

    private sealed class AuditTarget(int target) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(target, 0f);
    }
}
