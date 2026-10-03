using CoreSim.Race;

namespace CoreSim;

internal readonly record struct LongitudinalMotionNode(float DistanceMeters, float TimeSeconds, float Speed);

/// <summary>A sample in one consumed segment's coordinate frame. SegmentProgress may end at 1;
/// CanonicalProgress is the exact topological state, including advancement to the next segment.</summary>
public sealed record RiderMotionSample(float LocalTimeSeconds, double CanonicalProgress,
    float SegmentProgress, float TravelledMeters, float LateralPosition,
    float PhysicalOffsetMeters, float SpeedMetersPerSecond, float? RadiusMeters,
    float CurvaturePerMeter)
{
    public float PhysicalOffsetFromInnerEdgeMeters =>
        TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters + PhysicalOffsetMeters;
}

/// <summary>Coordinate reinterpretation only. Never interpolate or sweep between these offsets.</summary>
public sealed record MotionBoundaryTransition(int FromSegmentId, int ToSegmentId,
    double CanonicalProgress, float LateralPosition, float FromPhysicalOffsetMeters,
    float ToPhysicalOffsetMeters)
{
    public bool HasPhysicalOffsetDiscontinuity => FromPhysicalOffsetMeters != ToPhysicalOffsetMeters;
}

public enum MotionStateTransitionKind { EntryResolution, EndpointResolution, LegacyAlignment, TerminalCrash, ExistingContact }

/// <summary>An existing coarse state event, not a traversed trajectory.</summary>
public sealed record MotionStateTransition(MotionStateTransitionKind Kind,
    RiderMotionSample Before, RiderMotionSample After);

/// <summary>
/// Immutable projection of one production resolution. Fixed and moving riders share this API.
/// Sampling only interpolates stored endpoints, in this segment's frame; it runs no physics/RNG.
/// </summary>
public sealed class ResolvedRiderMotion : IEquatable<ResolvedRiderMotion>
{
    public int RiderId { get; }
    public int SegmentId { get; }
    public int SegmentIndex { get; }
    public float StartElapsedTimeSeconds { get; }
    public float ReactionTimeSeconds { get; }
    public IReadOnlyList<RiderMotionSample> Nodes { get; }
    public IReadOnlyList<MotionStateTransition> StateTransitions { get; }
    public MotionBoundaryTransition? EntryBoundary { get; }
    public MotionBoundaryTransition? ExitBoundary { get; }
    public RiderMotionSample Initial => Nodes[0];
    public RiderMotionSample Final => Nodes[^1];
    public float TotalTimeSeconds => Final.LocalTimeSeconds;
    public float TotalDistanceMeters => Final.TravelledMeters;

    private ResolvedRiderMotion(SimulationSnapshot snapshot, RiderSnapshot rider,
        IReadOnlyList<RiderMotionSample> nodes, IReadOnlyList<MotionStateTransition> transitions,
        float reaction, RiderStateChange finalChange)
    {
        ProjectionCaptureAudit.Record(ProjectionMaterialization.RichMotion);
        RiderId = rider.RiderId;
        SegmentId = snapshot.Segment.Id;
        SegmentIndex = snapshot.Step.SegmentIndex;
        StartElapsedTimeSeconds = rider.ElapsedTimeSeconds;
        ReactionTimeSeconds = reaction;
        Nodes = Array.AsReadOnly(nodes.ToArray());
        StateTransitions = Array.AsReadOnly(transitions.ToArray());
        if (rider.SegmentProgress == 0f && rider.CanonicalProgress > 0d)
            EntryBoundary = Boundary((SegmentIndex + snapshot.Track.Segments.Count - 1) % snapshot.Track.Segments.Count,
                SegmentIndex, rider.CanonicalProgress, rider.LateralPosition);
        if (finalChange.Status == RiderRaceStatus.Racing && finalChange.Position.SegmentProgress == 0f)
            ExitBoundary = Boundary(SegmentIndex, finalChange.Position.SegmentIndex,
                Final.CanonicalProgress, Final.LateralPosition);

        if (Initial.LocalTimeSeconds != 0f || Nodes.Any(n => !float.IsFinite(n.LocalTimeSeconds)))
            throw new InvalidOperationException("Motion must start at finite local time zero.");
        for (var i = 1; i < Nodes.Count; i++)
            if (Nodes[i].LocalTimeSeconds < Nodes[i - 1].LocalTimeSeconds
                || Nodes[i].CanonicalProgress < Nodes[i - 1].CanonicalProgress
                || Nodes[i].TravelledMeters < Nodes[i - 1].TravelledMeters)
                throw new InvalidOperationException("Resolved motion must be monotonic.");

        MotionBoundaryTransition Boundary(int from, int to, double progress, float lateral)
            => new(snapshot.Track.Segments[from].Id, snapshot.Track.Segments[to].Id, progress, lateral,
                LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(lateral, snapshot.Track.Segments[from].Type, snapshot.Track.Geometry),
                LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(lateral, snapshot.Track.Segments[to].Type, snapshot.Track.Geometry));
    }

    /// <summary>t=0 returns the snapshot origin; exact other node times return the last
    /// node at that time (after explicit state events). Interior samples are bounded linear
    /// interpolation. Negative, nonfinite and beyond-duration times are rejected.</summary>
    public RiderMotionSample SampleAtTime(float localTimeSeconds)
    {
        if (!float.IsFinite(localTimeSeconds) || localTimeSeconds < 0f || localTimeSeconds > TotalTimeSeconds)
            throw new ArgumentOutOfRangeException(nameof(localTimeSeconds));
        if (localTimeSeconds == 0f) return Initial;
        // Upper bound makes exact duplicate-time event semantics deterministic.
        var low = 0; var high = Nodes.Count;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (Nodes[mid].LocalTimeSeconds <= localTimeSeconds) low = mid + 1;
            else high = mid;
        }
        var a = Nodes[low - 1];
        if (a.LocalTimeSeconds == localTimeSeconds) return a;
        var b = Nodes[low];
        var fraction = ((double)localTimeSeconds - a.LocalTimeSeconds) / (b.LocalTimeSeconds - a.LocalTimeSeconds);
        float Blend(float first, float last) => (float)Math.Clamp(first + (last - (double)first) * fraction,
            Math.Min(first, last), Math.Max(first, last));
        var radius = a.RadiusMeters.HasValue ? Blend(a.RadiusMeters.Value, b.RadiusMeters!.Value) : (float?)null;
        return new(localTimeSeconds,
            Math.Clamp(a.CanonicalProgress + (b.CanonicalProgress - a.CanonicalProgress) * fraction,
                a.CanonicalProgress, b.CanonicalProgress),
            Blend(a.SegmentProgress, b.SegmentProgress), Blend(a.TravelledMeters, b.TravelledMeters),
            Blend(a.LateralPosition, b.LateralPosition), Blend(a.PhysicalOffsetMeters, b.PhysicalOffsetMeters),
            Blend(a.SpeedMetersPerSecond, b.SpeedMetersPerSecond), radius, radius.HasValue ? 1f / radius.Value : 0f);
    }

    internal static ResolvedRiderMotion Create(SimulationSnapshot snapshot, RiderSnapshot rider,
        RiderStateChange traversalChange, RiderStateChange finalChange, RiderStepDiagnostics diagnostics,
        IReadOnlyList<LongitudinalMotionNode>? longitudinalNodes)
    {
        var nodes = new List<RiderMotionSample>();
        var transitions = new List<MotionStateTransition>();
        var reaction = diagnostics.StandingStartLaunchProfile?.ReactionTimeSeconds ?? 0f;
        var duration = traversalChange.ElapsedTimeSeconds - rider.ElapsedTimeSeconds;
        var advance = traversalChange.Position.TotalSegmentProgress - rider.CanonicalProgress;
        var totalDistance = diagnostics.TravelledMeters;
        var path = diagnostics.ExecutedPath;
        var corner = diagnostics.ContinuousCornerProfile;
        var crash = traversalChange.Outcome == SegmentOutcome.Crash;
        nodes.Add(Sample(0f, 0d, 0f, rider.LateralPosition, rider.Speed));

        if (path is not null)
        {
            if (reaction > 0f) nodes.Add(nodes[0] with { LocalTimeSeconds = reaction });
            var sourceDuration = path.TravelTimeSeconds;
            for (var i = 0; i < path.Nodes.Count; i++)
            {
                var n = path.Nodes[i];
                var time = MapTime(n.ElapsedTimeSeconds, sourceDuration);
                var fraction = advance == 0d ? 0d : Math.Clamp((n.SegmentProgress - (double)rider.SegmentProgress) / advance, 0d, 1d);
                // Crash's terminal zero is an explicit endpoint event, not last-interval braking.
                var speed = crash && i == path.Nodes.Count - 1 ? path.Nodes[i - 1].SpeedMetersPerSecond : n.SpeedMetersPerSecond;
                Add(Sample(time, fraction, n.DistanceMeters, n.LateralPosition, speed));
            }
        }
        else if (longitudinalNodes is { Count: > 0 })
        {
            if (reaction > 0f) nodes.Add(nodes[0] with { LocalTimeSeconds = reaction });
            var movementTime = diagnostics.StandingStartLaunchProfile?.MovementTimeSeconds
                ?? diagnostics.StraightProfile!.Value.TravelTimeSeconds;
            foreach (var n in longitudinalNodes)
                Add(Sample(MapTime(reaction + n.TimeSeconds, reaction + movementTime),
                    totalDistance == 0f ? 0d : Math.Clamp(n.DistanceMeters / (double)totalDistance, 0d, 1d),
                    n.DistanceMeters, rider.LateralPosition, n.Speed));
        }
        else if (corner is not null)
        {
            var first = corner.Nodes[0].CornerProgress;
            var span = corner.Nodes[^1].CornerProgress - (double)first;
            foreach (var n in corner.Nodes)
            {
                var fraction = span == 0d ? 0d : Math.Clamp((n.CornerProgress - first) / span, 0d, 1d);
                Add(Sample(MapTime(n.ElapsedTimeSeconds, corner.TravelTimeSeconds), fraction,
                    (float)(totalDistance * fraction), rider.LateralPosition, n.SpeedMetersPerSecond));
            }
        }
        else
        {
            // Existing coarse crash/legacy event-time abstraction, no new integrator.
            var carriedSpeed = crash ? MathF.Max(1f, traversalChange.EntrySpeed * .5f) : traversalChange.PhysicsSpeed;
            Add(Sample(0f, 0d, 0f, rider.LateralPosition, carriedSpeed));
            Add(Sample(duration, 1d, totalDistance, rider.LateralPosition, carriedSpeed));
        }

        var endpoint = Sample(duration, 1d, totalDistance, nodes[^1].LateralPosition, nodes[^1].SpeedMetersPerSecond);
        nodes[^1] = endpoint; // Exact canonical endpoints override observation rounding only.
        if (endpoint.SpeedMetersPerSecond != traversalChange.Speed || endpoint.LateralPosition != traversalChange.LateralPosition)
        {
            var terminal = Sample(duration, 1d, totalDistance, traversalChange.LateralPosition, traversalChange.Speed);
            transitions.Add(new(crash ? MotionStateTransitionKind.TerminalCrash
                : snapshot.Step.UseLegacyPhysics ? MotionStateTransitionKind.LegacyAlignment : MotionStateTransitionKind.EndpointResolution,
                endpoint, terminal));
            nodes.Add(terminal);
        }
        if (finalChange.Speed != traversalChange.Speed || finalChange.LateralPosition != traversalChange.LateralPosition
            || finalChange.ElapsedTimeSeconds != traversalChange.ElapsedTimeSeconds)
        {
            var before = nodes[^1];
            var after = Sample(duration, 1d, totalDistance, finalChange.LateralPosition, finalChange.Speed);
            transitions.Add(new(MotionStateTransitionKind.ExistingContact, before, after));
            nodes.Add(after);
        }
        if (diagnostics.TravelTimeSeconds > duration)
            nodes.Add(nodes[^1] with { LocalTimeSeconds = diagnostics.TravelTimeSeconds });
        nodes[^1] = nodes[^1] with { LocalTimeSeconds = diagnostics.TravelTimeSeconds,
            CanonicalProgress = finalChange.Position.TotalSegmentProgress,
            LateralPosition = finalChange.LateralPosition, SpeedMetersPerSecond = finalChange.Speed };
        return new(snapshot, rider, nodes, transitions, reaction, finalChange);

        float MapTime(float time, float sourceDuration)
        {
            if (time == 0f || time == reaction) return time;
            if (time == sourceDuration) return duration;
            return (float)Math.Clamp(reaction + (time - (double)reaction)
                * (duration - reaction) / (sourceDuration - reaction), reaction, duration);
        }
        RiderMotionSample Sample(float time, double fraction, float distance, float lateral, float speed)
        {
            ProjectionCaptureAudit.Record(ProjectionMaterialization.MotionSample);
            var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(lateral, snapshot.Segment.Type, snapshot.Track.Geometry);
            var radius = snapshot.Segment.Type == SegmentType.Straight ? (float?)null : snapshot.Track.Geometry.InnerRadiusMeters + offset;
            return new(time, fraction == 1d ? traversalChange.Position.TotalSegmentProgress : rider.CanonicalProgress + advance * fraction,
                (float)(rider.SegmentProgress + advance * fraction), Math.Clamp(distance, 0f, totalDistance),
                lateral, offset, speed, radius, radius.HasValue ? 1f / radius.Value : 0f);
        }
        void Add(RiderMotionSample sample)
        {
            if (sample == nodes[^1]) return;
            if (sample.LocalTimeSeconds == 0f && sample != nodes[^1])
                transitions.Add(new(MotionStateTransitionKind.EntryResolution, nodes[^1], sample));
            nodes.Add(sample);
        }
    }

    public bool Equals(ResolvedRiderMotion? other) => other is not null && RiderId == other.RiderId
        && SegmentId == other.SegmentId && SegmentIndex == other.SegmentIndex
        && StartElapsedTimeSeconds == other.StartElapsedTimeSeconds && ReactionTimeSeconds == other.ReactionTimeSeconds
        && EntryBoundary == other.EntryBoundary && ExitBoundary == other.ExitBoundary
        && Nodes.SequenceEqual(other.Nodes) && StateTransitions.SequenceEqual(other.StateTransitions);
    public override bool Equals(object? obj) => obj is ResolvedRiderMotion other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(RiderId, SegmentId, TotalTimeSeconds, TotalDistanceMeters);
}
