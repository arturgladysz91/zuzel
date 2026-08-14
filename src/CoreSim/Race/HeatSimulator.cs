using CoreSim.Decisions;
using CoreSim.Logging;
using System.Globalization;

namespace CoreSim.Race;

public sealed class HeatSimulator
{
    private readonly IRiderDecisionModel _decision;

    public HeatSimulator(IRiderDecisionModel decision)
        => _decision = decision ?? throw new ArgumentNullException(nameof(decision));

    /// <summary>
    /// Backward-compatible single pass used by early experiments and tests.
    /// It deliberately keeps the original neutral-surface physics contract.
    /// </summary>
    public SimLog Simulate(Track track, TrackState trackState, List<RiderState> riders, int heatId = 0)
    {
        ValidateInputs(track, trackState, riders);

        var log = new SimLog();
        var crashed = new bool[riders.Count];

        for (var segIndex = 0; segIndex < track.Segments.Count; segIndex++)
        {
            var segment = track.Segments[segIndex];
            for (var i = 0; i < riders.Count; i++)
            {
                if (crashed[i])
                    continue;

                var rider = riders[i];
                var before = rider.Lane;
                var decision = _decision.Decide(segment, rider);
                var targetLane = LaneModel.ClampLane(decision.TargetLane);
                var plannedLane = CalculatePlannedLane(before, targetLane);

                rider.Lane = plannedLane;
                rider.LateralPosition = plannedLane;

                var entrySpeed = rider.Speed <= 0f
                    ? SegmentPhysics.MaxSafeTurnSpeed(plannedLane)
                    : rider.Speed;
                var resolution = SegmentPhysics.Apply(segment, plannedLane, entrySpeed);

                rider.CurrentSegmentId = segment.Id;
                rider.Lane = resolution.Lane;
                rider.LateralPosition = resolution.Lane;
                rider.Speed = resolution.Speed;
                rider.Risk = ApplySurfaceRisk(segment, trackState, segIndex, rider.Lane, decision.Risk);

                log.Add(FormatSegmentLog(
                    lap: null,
                    segment,
                    rider,
                    before,
                    plannedLane,
                    decision.TargetLane,
                    entrySpeed,
                    resolution));

                if (resolution.Outcome != SegmentOutcome.Crash)
                {
                    ApplyLegacySurfaceWear(segment, trackState, segIndex, rider.Lane, heatId, segIndex, log);
                }
                else
                {
                    crashed[i] = true;
                    rider.IsCrashed = true;
                }
            }
        }

        return log;
    }

    /// <summary>
    /// Full deterministic heat: four laps by default, changing surface, weather,
    /// rider abilities, morale, setup trade-offs and rider-to-rider interaction.
    /// </summary>
    public HeatResult SimulateHeat(
        Track track,
        TrackState trackState,
        List<RiderState> riders,
        HeatSimulationOptions? options = null,
        int heatId = 0)
    {
        ValidateInputs(track, trackState, riders);
        options ??= new HeatSimulationOptions();
        options.Validate();

        if (riders.Select(r => r.RiderId).Distinct().Count() != riders.Count)
            throw new ArgumentException("Every rider in a heat must have a unique id.", nameof(riders));

        var random = new Random(options.Seed);
        var log = new SimLog();
        var tick = 0;

        for (var lap = 0; lap < options.Laps; lap++)
        {
            for (var segmentIndex = 0; segmentIndex < track.Segments.Count; segmentIndex++, tick++)
            {
                var segment = track.Segments[segmentIndex];
                TrackEvolution.ApplyWeather(track, trackState, options.Weather, heatId, tick, log);

                foreach (var rider in riders.Where(rider => !rider.IsCrashed))
                {
                    SimulateAdvancedSegment(
                        track,
                        trackState,
                        riders,
                        rider,
                        segment,
                        segmentIndex,
                        lap,
                        heatId,
                        tick,
                        options,
                        random,
                        log);
                }

                ResolveInteractions(segment, trackState, segmentIndex, riders, random, log, lap);

                if (segmentIndex == track.Segments.Count - 1)
                {
                    foreach (var rider in riders.Where(rider => !rider.IsCrashed))
                        rider.LapsCompleted = lap + 1;
                }
            }
        }

        var classification = BuildClassification(riders, options.Laps);
        ApplyMoraleConsequences(riders, classification);
        return new HeatResult(heatId, classification, log);
    }

    private void SimulateAdvancedSegment(
        Track track,
        TrackState trackState,
        IReadOnlyList<RiderState> riders,
        RiderState rider,
        TrackSegment segment,
        int segmentIndex,
        int lap,
        int heatId,
        int tick,
        HeatSimulationOptions options,
        Random random,
        SimLog log)
    {
        var before = rider.Lane;
        var context = new RiderDecisionContext(segment, segmentIndex, trackState, rider, riders, heatId, lap);
        var decision = _decision.Decide(context);
        var targetLane = LaneModel.ClampLane(decision.TargetLane);
        var plannedLane = CalculatePlannedLane(before, targetLane);
        var surface = trackState.GetSurface(segmentIndex, plannedLane);

        var entrySpeed = ResolveAdvancedEntrySpeed(segment, plannedLane, surface, rider, lap, segmentIndex);
        var resolution = SegmentPhysics.Apply(new SegmentPhysicsContext(
            segment,
            plannedLane,
            entrySpeed,
            surface,
            rider.Profile.Skills,
            rider.Morale,
            rider.ActiveSetup,
            decision.Risk));

        resolution = ResolveRandomIncident(resolution, segment, plannedLane, options, random);

        rider.CurrentSegmentId = segment.Id;
        rider.Lane = resolution.Lane;
        rider.LateralPosition = MoveLateralPosition(rider.LateralPosition, resolution.Lane, surface, rider);
        rider.Speed = ApplyExitDrive(segment, resolution.Speed, rider);
        rider.Risk = resolution.IncidentRisk;

        var segmentLength = LaneModel.SegmentLengthMeters(segment, rider.Lane);
        var travelled = resolution.Outcome == SegmentOutcome.Crash ? segmentLength * 0.5f : segmentLength;
        var averageSpeed = MathF.Max(1f, (entrySpeed + MathF.Max(rider.Speed, 0f)) * 0.5f);
        rider.ElapsedTimeSeconds += travelled / averageSpeed;
        rider.DistanceMeters += travelled;

        log.Add(FormatSegmentLog(
            lap + 1,
            segment,
            rider,
            before,
            plannedLane,
            decision.TargetLane,
            entrySpeed,
            resolution));

        if (resolution.Outcome == SegmentOutcome.Crash)
        {
            rider.IsCrashed = true;
            rider.Speed = 0f;
            rider.ApplyMoraleDelta(-0.04f);
            return;
        }

        ApplyAdvancedSurfaceWear(segment, trackState, segmentIndex, rider.Lane, heatId, tick, log);
    }

    private static float ResolveAdvancedEntrySpeed(
        TrackSegment segment,
        int lane,
        TrackSurfaceState surface,
        RiderState rider,
        int lap,
        int segmentIndex)
    {
        if (rider.Speed > 0f)
            return rider.Speed;

        if (segment.Type == SegmentType.Straight)
            return SegmentPhysics.MaxSafeTurnSpeed(lane) * 0.95f;

        var safeSpeed = SegmentPhysics.MaxSafeTurnSpeed(
            lane,
            surface,
            rider.Profile.Skills,
            rider.Morale,
            rider.ActiveSetup);
        var startSkill = RiderSkills.Normalize(rider.Profile.Skills.Start);
        var startMultiplier = lap == 0 && segmentIndex == 0 ? 0.94f + startSkill * 0.08f : 1f;
        return safeSpeed * startMultiplier;
    }

    private static float ApplyExitDrive(TrackSegment segment, float speed, RiderState rider)
    {
        if (speed <= 0f || segment.Type != SegmentType.TurnExit)
            return speed;

        var speedSkill = RiderSkills.Normalize(rider.Profile.Skills.Speed);
        var gearingTradeOff = (rider.ActiveSetup.Gearing - 0.5f) * 0.02f;
        var multiplier = 0.985f + speedSkill * 0.025f + gearingTradeOff;
        return speed * multiplier;
    }

    private static SegmentResolution ResolveRandomIncident(
        SegmentResolution resolution,
        TrackSegment segment,
        int plannedLane,
        HeatSimulationOptions options,
        Random random)
    {
        if (segment.Type == SegmentType.Straight || resolution.Outcome == SegmentOutcome.Crash)
            return resolution;

        var probability = resolution.IncidentRisk * 0.08f * options.IncidentFrequency;
        if (random.NextDouble() >= probability)
            return resolution;

        var severe = random.NextDouble() < resolution.IncidentRisk * 0.35f;
        if (severe || plannedLane == LaneModel.MaxLane)
            return resolution with { Outcome = SegmentOutcome.Crash, Speed = 0f };

        return resolution with
        {
            Outcome = SegmentOutcome.RunWide,
            Lane = Math.Min(plannedLane + 1, LaneModel.MaxLane),
            Speed = resolution.Speed * 0.88f,
        };
    }

    private static void ResolveInteractions(
        TrackSegment segment,
        TrackState trackState,
        int segmentIndex,
        IReadOnlyList<RiderState> riders,
        Random random,
        SimLog log,
        int lap)
    {
        foreach (var laneGroup in riders
                     .Where(rider => !rider.IsCrashed)
                     .GroupBy(rider => rider.Lane))
        {
            var ordered = laneGroup.OrderBy(rider => rider.ElapsedTimeSeconds).ToArray();
            for (var i = 1; i < ordered.Length; i++)
            {
                var leader = ordered[i - 1];
                var trailing = ordered[i];
                var gap = trailing.ElapsedTimeSeconds - leader.ElapsedTimeSeconds;
                if (gap > 0.12f)
                    continue;

                var control = RiderSkills.Normalize(trailing.Profile.Skills.SlideControl);
                var pairRiding = RiderSkills.Normalize(trailing.Profile.Skills.PairRiding);
                var surface = trackState.GetSurface(segmentIndex, trailing.Lane);
                var locationRisk = segment.Type switch
                {
                    SegmentType.TurnMiddle => 0.24f,
                    SegmentType.TurnEntry or SegmentType.TurnExit => 0.12f,
                    _ => 0.025f,
                };
                var contactChance = locationRisk
                    * (1.20f - pairRiding * 0.45f)
                    * (1.15f - control * 0.35f)
                    * (1.10f + (1f - surface.EffectiveGrip) * 0.40f);

                if (random.NextDouble() >= contactChance)
                    continue;

                var crashChance = segment.Type == SegmentType.TurnMiddle ? 0.28f : 0.08f;
                crashChance *= 1.20f - control * 0.55f;
                if (random.NextDouble() < crashChance)
                {
                    trailing.IsCrashed = true;
                    trailing.Speed = 0f;
                    trailing.ApplyMoraleDelta(-0.04f);
                    log.Add($"LAP={lap + 1} SEG={segment.Id} contact rider={trailing.RiderId} with={leader.RiderId} outcome=Crash");
                }
                else
                {
                    trailing.Speed *= 0.82f;
                    trailing.ElapsedTimeSeconds += 0.20f;
                    if (segment.Type != SegmentType.Straight && trailing.Lane < LaneModel.MaxLane)
                    {
                        trailing.Lane++;
                        trailing.LateralPosition = MathF.Min(trailing.LateralPosition + 0.5f, trailing.Lane);
                    }
                    log.Add($"LAP={lap + 1} SEG={segment.Id} contact rider={trailing.RiderId} with={leader.RiderId} outcome=LostRhythm");
                }
            }
        }
    }

    private static float MoveLateralPosition(
        float current,
        int targetLane,
        TrackSurfaceState surface,
        RiderState rider)
    {
        var adaptability = RiderSkills.Normalize(rider.Profile.Skills.Adaptability);
        var style = rider.Profile.Style.LaneChangeTendency;
        var traction = 0.55f + surface.EffectiveGrip * 0.35f;
        var maxStep = (0.45f + adaptability * 0.25f + style * 0.20f) * traction;
        return current + Math.Clamp(targetLane - current, -maxStep, maxStep);
    }

    private static IReadOnlyList<RiderHeatResult> BuildClassification(
        IReadOnlyList<RiderState> riders,
        int requiredLaps)
    {
        var ordered = riders
            .OrderByDescending(rider => !rider.IsCrashed && rider.LapsCompleted == requiredLaps)
            .ThenBy(rider => rider.IsCrashed ? float.MaxValue : rider.ElapsedTimeSeconds)
            .ThenByDescending(rider => rider.DistanceMeters)
            .ThenBy(rider => rider.RiderId)
            .ToArray();

        var result = new List<RiderHeatResult>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var rider = ordered[index];
            var position = index + 1;
            var points = position switch { 1 => 3, 2 => 2, 3 => 1, _ => 0 };
            result.Add(new RiderHeatResult(
                rider.RiderId,
                position,
                points,
                !rider.IsCrashed && rider.LapsCompleted == requiredLaps,
                rider.IsCrashed,
                rider.ElapsedTimeSeconds,
                rider.DistanceMeters,
                rider.LapsCompleted));
        }

        return result;
    }

    private static void ApplyMoraleConsequences(
        IReadOnlyList<RiderState> riders,
        IReadOnlyList<RiderHeatResult> classification)
    {
        foreach (var result in classification)
        {
            var rider = riders.Single(r => r.RiderId == result.RiderId);
            var delta = result.Crashed
                ? -0.04f
                : result.Position switch
                {
                    1 => 0.04f,
                    2 => 0.015f,
                    3 => -0.01f,
                    _ => -0.025f,
                };
            rider.ApplyMoraleDelta(delta);
        }
    }

    private static float ApplySurfaceRisk(
        TrackSegment segment,
        TrackState trackState,
        int segmentIndex,
        int lane,
        float baseRisk)
    {
        if (segment.Type == SegmentType.Straight)
            return baseRisk;

        var surface = trackState.GetSurface(segmentIndex, lane);
        var surfaceRisk = (1f - surface.Grip) * 0.7f + surface.Ruts * 0.3f;
        if (surface.Moisture > 0.5f)
            surfaceRisk += (surface.Moisture - 0.5f) * 0.2f;
        return TrackSurfaceState.Clamp01(baseRisk + TrackSurfaceState.Clamp01(surfaceRisk));
    }

    private static void ApplyLegacySurfaceWear(
        TrackSegment segment,
        TrackState trackState,
        int segmentIndex,
        int lane,
        int heatId,
        int tick,
        SimLog log)
    {
        var rutsDelta = segment.Type == SegmentType.Straight ? 0.005f : 0.02f;
        var gripDelta = -0.25f * rutsDelta;
        trackState.ApplySurfaceDelta(segmentIndex, lane, gripDelta, rutsDelta, 0f, "pass", heatId, tick, log);
    }

    private static void ApplyAdvancedSurfaceWear(
        TrackSegment segment,
        TrackState trackState,
        int segmentIndex,
        int lane,
        int heatId,
        int tick,
        SimLog log)
    {
        var rutsDelta = segment.Type == SegmentType.Straight ? 0.004f : 0.015f;
        var gripDelta = -0.25f * rutsDelta;
        trackState.ApplySurfaceDelta(segmentIndex, lane, gripDelta, rutsDelta, 0f, "pass", heatId, tick, log);

        foreach (var adjacentLane in new[] { lane - 1, lane + 1 })
        {
            if (adjacentLane < LaneModel.MinLane || adjacentLane > LaneModel.MaxLane)
                continue;
            trackState.ApplySurfaceDelta(
                segmentIndex,
                adjacentLane,
                gripDelta * 0.20f,
                rutsDelta * 0.20f,
                0f,
                "pass-adjacent",
                heatId,
                tick,
                log);
        }
    }

    private static string FormatSegmentLog(
        int? lap,
        TrackSegment segment,
        RiderState rider,
        int before,
        int plannedLane,
        int targetLane,
        float entrySpeed,
        SegmentResolution resolution)
    {
        var prefix = lap.HasValue ? $"LAP={lap.Value} " : string.Empty;
        return $"{prefix}SEG={segment.Id} {segment.Type} rider={rider.RiderId} lane {before}->{plannedLane}->{rider.Lane} targetLane={targetLane} outcome={resolution.Outcome} v_in={entrySpeed.ToString("F2", CultureInfo.InvariantCulture)} v_out={rider.Speed.ToString("F2", CultureInfo.InvariantCulture)}";
    }

    private static int CalculatePlannedLane(int currentLane, int targetLane)
    {
        if (targetLane > currentLane)
            return LaneModel.ClampLane(currentLane + 1);
        if (targetLane < currentLane)
            return LaneModel.ClampLane(currentLane - 1);
        return currentLane;
    }

    private static void ValidateInputs(Track track, TrackState trackState, List<RiderState> riders)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(trackState);
        ArgumentNullException.ThrowIfNull(riders);
        if (trackState.SegmentCount != track.Segments.Count || trackState.LinesCount != LaneModel.LanesCount)
            throw new ArgumentException("Track state dimensions must match the track.", nameof(trackState));
        if (riders.Count == 0)
            throw new ArgumentException("A heat must contain at least one rider.", nameof(riders));
    }
}
