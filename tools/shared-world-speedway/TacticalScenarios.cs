using CoreSim;
using CoreSim.Setup;

namespace SharedWorldSpeedway;

public static class TacticalScenarios
{
    private static RiderSpec Rider(int id, double progress, float offset, float speed,
        int attack = 80, int defense = 15, float combativeness = .35f)
    {
        var gameplay = new RiderGameplayProfile(new(50, 50, 70, 65, attack, defense, 50, 60),
            new(65), new(combativeness, PreferredLine.Neutral, .5f));
        return new(RiderProfile.CreateCanonical(id, $"R{id}", gameplay, RiderSkills.Balanced, RiderStyle.Balanced),
            BikeSetup.Neutral, progress, offset, speed, offset);
    }
    public static Scenario A(int defense = 15)
    {
        var frame = new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var back = frame.Embedding.Segments.Take(4).Sum(s => s.SegmentType == SegmentType.Straight
            ? s.StraightLengthMeters : s.InnerRadiusMeters * s.TurnAngleRadians);
        return new("TA-straight-pass", new[] {Rider(1, back + 1, 4, 20, attack: 85),
            Rider(2, back + 13, 4, 12, attack: 50, defense: defense)}, 7.5);
    }
    public static Scenario B() => new("TB-inside-bend", new[] {
        Rider(1, 45, 5, 22, attack: 85), Rider(2, 53, 5, 17, attack: 50)}, 4.5);
    public static Scenario C() => new("TC-outside-bend", new[] {
        Rider(1, 45, 1, 20, attack: 85), Rider(2, 58, 1, 12, attack: 50)}, 4.5);
    public static Scenario D(bool good) => A(good ? 90 : 15) with { Name = good ? "TD-defense-90" : "TD-defense-15" };
    public static Scenario E()
    {
        var a = A(); var start = a.Riders[0].ReferenceMeters;
        return new("TE-no-corridor", new[] {Rider(1, start, 4, 20, attack: 85),
            Rider(2, start + 12, 4, 16, attack: 50), Rider(3, start + 11, 2.6f, 16), Rider(4, start + 11, 5.4f, 16)}, 4);
    }
    public static Scenario F() => Scenarios.G() with {Name = "TF-Motoarena-first-bend"};
    public static Scenario G() => Scenarios.F() with {Name = "TG-close-parallel"};
    public static IReadOnlyList<Scenario> Cases() => new[] {A(), B(), C(), D(false), D(true), E(), F(), G()};
}
