using CoreSim;
using CoreSim.Setup;

namespace SharedWorldSpeedway;

public sealed record Scenario(string Name, RiderSpec[] Riders, double DurationSeconds, int Laps = 0);
public static class Scenarios
{
    private static RiderSpec Rider(int id, double s, float offset, float speed, float? target = null,
        float outward = 0, bool traffic = true, bool passive = false, bool aggressive = false)
    {
        var gameplay = new RiderGameplayProfile(new(50, 50, aggressive ? 35 : 70, 65, aggressive ? 30 : 70, 65, 50, 60),
            new(65), new(aggressive ? .95f : .3f, PreferredLine.Neutral, .5f));
        var profile = RiderProfile.CreateCanonical(id, $"R{id}", gameplay, RiderSkills.Balanced, RiderStyle.Balanced);
        return new(profile, BikeSetup.Neutral, s, offset, speed, target ?? offset, outward, traffic, passive);
    }
    private static float[] GateOffsets()
    {
        var track = CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        return Enum.GetValues<StartingGate>().Select(g => LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(
            StartingGateGeometry.CenterLateralPosition(g, track.Geometry), SegmentType.Straight, track.Geometry)).ToArray();
    }
    public static Scenario A(int laps = 1) => new(laps == 1 ? "A-lap" : "A-heat",
        GateOffsets().Select((offset, i) => Rider(i + 1, 0, offset, 0, traffic: false)).ToArray(), 120, laps);
    public static Scenario B(bool traffic = true) => new(traffic ? "B-yield" : "B-counterfactual",
        new[] { Rider(1, 5, 2, 20, target: 4, traffic: traffic), Rider(2, 5, 4, 20, traffic: false) }, 3);
    public static Scenario C() => new("C-light", new[] {
        Rider(1, 5, 3, 20, outward: .2f, traffic: false), Rider(2, 5, 3.84f, 20, traffic: false) }, 2);
    public static Scenario Support() => new("C-support", new[] {
        Rider(1, 5, 3, 20, target: 3.2f, outward: .2f, traffic: false), Rider(2, 5, 3.84f, 20, traffic: false) }, 2);
    public static Scenario D() => new("D-bend", new[] {
        Rider(1, 48, 3, 20, target: 5, outward: 4, traffic: false),
        Rider(2, 48, 3.95f, 20, traffic: false) }, 2);
    public static Scenario E(bool bulletA = false, bool bulletB = false) => new("E-fast", new[] {
        Rider(1, 10, 2, 40, outward: 40, traffic: false, passive: true) with { Bullet = bulletA },
        Rider(2, 10.8, 2.8f, 40, outward: -40, traffic: false, passive: true) with { Bullet = bulletB } }, .1);
    public static Scenario F()
    {
        var track = CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var back = 35 + track.Geometry.InnerRadiusMeters * track.Geometry.TurnSegmentAngleRadians * 3 + 5;
        return new("F-close", new[] {
            Rider(1, back, 3, 20, passive: true), Rider(2, back, 3.9f, 20, passive: true) }, 2.5);
    }
    public static Scenario G()
    {
        var offsets = GateOffsets();
        return new("G-first-bend", new[] {
            Rider(1, 0, offsets[0], 0, target: 1.6f), Rider(2, 0, offsets[1], 0, target: 3.2f, aggressive: true),
            Rider(3, 0, offsets[2], 0, target: 5.6f), Rider(4, 0, offsets[3], 0, target: 8.3f) }, 12);
    }
    // Keep the original deliberately squeezed start as adverse evidence. It is
    // more sensitive to uncalibrated yaw/tyre control; do not silently discard it.
    public static Scenario DenseStart() => new("G-dense-stress", new[] {
        Rider(1, 0, 1, 0, target: 2.8f), Rider(2, 0, 3, 0, target: 2.6f, aggressive: true),
        Rider(3, 0, 5, 0, target: 3.2f), Rider(4, 0, 7, 0, target: 4) }, 12);
    public static RunResult Run(Scenario s, double step, WorldParameters? p = null, bool reverse = false)
    {
        using var world = new WorldRunner(reverse ? s.Riders.Reverse() : s.Riders, parameters: p);
        return world.Run(s.Name, step, s.DurationSeconds, s.Laps);
    }
}
