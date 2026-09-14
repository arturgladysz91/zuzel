using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;

namespace CoreSim.Tests;

internal static class CornerTestSupport
{
    internal static readonly TrackSurfaceState Surface = new(1f, 0f, .35f);
    internal static Track Track(TrackGeometry? geometry = null) => new(new[]
    {
        new TrackSegment(0, SegmentType.TurnEntry), new TrackSegment(1, SegmentType.TurnMiddle),
        new TrackSegment(2, SegmentType.TurnExit),
    }, geometry ?? TrackGeometry.Default);

    internal static ContinuousCornerEnvelope Envelope(Track track, RiderState rider,
        TrackSurfaceState? surface = null, int index = 0, float progress = 0f)
        => ContinuousCornerEnvelope.Create(track.CornerTopology.Resolve(index, progress,
            rider.LateralPosition, track.Geometry)!.Value, rider.LateralPosition, track.Geometry,
            surface ?? Surface, rider.Profile.Skills, rider.ActiveSetup);

    internal static float SingleEnvelopeSpeed(float lateral, TrackGeometry geometry,
        TrackSurfaceState surface, RiderSkills skills, CoreSim.Setup.BikeSetup setup)
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.TurnMiddle) }, geometry);
        var phase = track.CornerTopology.Resolve(0, 0f, lateral, geometry)!.Value;
        return ContinuousCornerEnvelope.Create(phase, lateral, geometry, surface, skills, setup).SpeedMetersPerSecond(0f);
    }

    internal static ContinuousCornerTraversalProfile Expected(ResolvedSimulationStep step)
    {
        var d = step.Diagnostics.Single();
        var r = step.Snapshot.Rider(d.RiderId);
        var c = step.Changes.Single();
        var phase = d.CornerPhaseContext!.Value;
        var envelope = ContinuousCornerEnvelope.Create(phase, r.LateralPosition, step.Snapshot.Track.Geometry,
            d.EntrySurface, r.Profile.Skills, r.ActiveSetup);
        var resolution = SegmentPhysics.Apply(new SegmentPhysicsContext(step.Snapshot.Segment,
            c.PlannedLane, r.Speed, step.Snapshot.Track.Geometry, d.EntrySurface, r.Profile.Skills,
            r.Morale, r.ActiveSetup, LateralPosition: r.LateralPosition, CornerPhase: phase));
        var retained = c.Outcome == SegmentOutcome.RunWide
            ? MathF.Max(0f, resolution.ContinuousCorrectionTargetSpeedMetersPerSecond.GetValueOrDefault()
                - envelope.SpeedMetersPerSecond(phase.CornerProgress)) : 0f;
        return envelope.Traverse(c.PhysicsSpeed, phase.CornerProgress, d.TravelledMeters,
            c.Outcome is SegmentOutcome.Ok or SegmentOutcome.Brake,
            !(c.Outcome == SegmentOutcome.RunWide && resolution.ContinuousCorrectionTargetSpeedMetersPerSecond is null), retained);
    }

    internal static CalibrationTurnResult Probe(float progress = 0f, float factor = 1f,
        float lateral = 1f, TrackGeometry? geometry = null)
    {
        // Standard production calibration fixture (three physical turn pieces).
        if (geometry is not null) throw new ArgumentException("Use direct envelope tests for alternate geometry.");
        var track = Track(CoreSim.Track.CreateStandingStartExample().Geometry);
        var rider = new RiderState(1, (int)lateral) { LateralPosition = lateral };
        var index = Math.Min(2, (int)(progress * 3f));
        var local = progress * 3f - index;
        var envelope = Envelope(track, rider, index: index, progress: local);
        var kinds = new[] { CalibrationScenarioKind.TurnEntry, CalibrationScenarioKind.TurnMiddle, CalibrationScenarioKind.TurnExit };
        return (CalibrationTurnResult)CalibrationScenarioSuite.RunScenario(new CalibrationTurnScenario(
            new CalibrationScenarioMetadata("test/corner", kinds[index], "Full logical corner regression"),
            CalibrationScenarioCatalog.Baseline, track.Segments[index].Type, lateral,
            envelope.SpeedMetersPerSecond(progress) * factor, local));
    }
}
