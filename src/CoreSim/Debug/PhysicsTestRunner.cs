using System.Globalization;

namespace CoreSim.Debug;

public sealed record PhysicsTestSegmentPlan(int TargetLane, float? EntrySpeed = null, float? SpeedMultiplier = null, string? Note = null)
{
    public float ResolveEntrySpeed(float previousSpeed)
    {
        if (EntrySpeed.HasValue)
            return EntrySpeed.Value;
        if (SpeedMultiplier.HasValue)
            return previousSpeed * SpeedMultiplier.Value;
        return previousSpeed;
    }
}

public sealed record PhysicsTestScenario(
    string Name,
    Track Track,
    TrackState TrackState,
    int InitialLane,
    float InitialSpeed,
    IReadOnlyList<PhysicsTestSegmentPlan> SegmentPlans)
{
    public static PhysicsTestScenario FromSafeSpeedMultiplier(
        string name,
        Track track,
        TrackState trackState,
        int initialLane,
        float safeSpeedMultiplier,
        IReadOnlyList<PhysicsTestSegmentPlan> segmentPlans)
    {
        var initialSpeed = SegmentPhysics.MaxSafeTurnSpeed(initialLane, track.Geometry) * safeSpeedMultiplier;
        return new PhysicsTestScenario(name, track, trackState, initialLane, initialSpeed, segmentPlans);
    }
}

public sealed record PhysicsTestSegmentLog(
    int SegmentId,
    SegmentType SegmentType,
    int LaneIn,
    int TargetLane,
    int LaneOut,
    float SpeedIn,
    float SpeedOut,
    SegmentOutcome Outcome,
    TrackSurfaceState Surface,
    string? Note)
{
    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"SEG={SegmentId} {SegmentType} lane {LaneIn}->{TargetLane}->{LaneOut} v_in={SpeedIn:F2} v_out={SpeedOut:F2} surface grip={Surface.Grip:F2} ruts={Surface.Ruts:F2} moisture={Surface.Moisture:F2} outcome={Outcome}{(string.IsNullOrWhiteSpace(Note) ? string.Empty : $" note={Note}")}");
    }
}

public sealed record PhysicsTestResult(
    string ScenarioName,
    IReadOnlyList<PhysicsTestSegmentLog> Segments,
    int FinalLane,
    float FinalSpeed)
{
    public IEnumerable<string> AsLines() => Segments.Select(s => s.ToString());
}

public sealed class PhysicsTestRunner
{
    public PhysicsTestResult Run(PhysicsTestScenario scenario)
    {
        if (scenario.SegmentPlans.Count != scenario.Track.Segments.Count)
            throw new ArgumentException("Plans must match track segments count", nameof(scenario));

        var logs = new List<PhysicsTestSegmentLog>(scenario.Track.Segments.Count);
        var lane = scenario.InitialLane;
        var speed = scenario.InitialSpeed;

        for (var i = 0; i < scenario.Track.Segments.Count; i++)
        {
            var segment = scenario.Track.Segments[i];
            var plan = scenario.SegmentPlans[i];
            var entrySpeed = plan.ResolveEntrySpeed(speed);
            var resolution = SegmentPhysics.Apply(
                segment,
                plan.TargetLane,
                entrySpeed,
                scenario.Track.Geometry);
            var surface = scenario.TrackState.GetSurface(i, plan.TargetLane);

            logs.Add(new PhysicsTestSegmentLog(
                segment.Id,
                segment.Type,
                lane,
                plan.TargetLane,
                resolution.Lane,
                entrySpeed,
                resolution.Speed,
                resolution.Outcome,
                surface,
                plan.Note));

            lane = resolution.Lane;
            speed = resolution.Speed;
        }

        return new PhysicsTestResult(scenario.Name, logs, lane, speed);
    }
}
