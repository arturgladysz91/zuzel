using CoreSim;
using CoreSim.Setup;

namespace SharedWorldSpeedway;

public static class SideBySideScenarios
{
    private static RiderSpec Rider(int id, double reference, float offset, float speed, float? target = null,
        float combativeness = .5f, float slide = 70)
    {
        var gameplay = new RiderGameplayProfile(new(50, 50, 70, 65, 80, 75, 50, 60), new(65),
            new(combativeness, PreferredLine.Neutral, .5f));
        return new(RiderProfile.CreateCanonical(id, $"R{id}", gameplay, new(50, 50, slide, 50, 50, 50), RiderStyle.Balanced),
            BikeSetup.Neutral, reference, offset, speed, target ?? offset);
    }
    public static Scenario A() => new("SA-entry-parallel", new[] {
        Rider(1, 34, 3, 19.5f), Rider(2, 34, 6, 20.5f)}, 4);
    public static Scenario BInside() => new("SB-inner-rate", new[] {
        Rider(1, 45, 3, 19), Rider(2, 45, 6, 20)}, 3);
    public static Scenario BOutside() => new("SB-outer-rate", new[] {
        Rider(1, 45, 5, 17), Rider(2, 45, 6.2f, 21.5f, slide: 50)}, 3);
    public static Scenario BHoldInside() => new("SB-inner-hold", new[] {
        Rider(1, 45, .5f, 20, slide: 50), Rider(2, 45, 3.5f, 21.5f, slide: 50)}, 3);
    public static Scenario BNoGain() => new("SB-no-gain", new[] {
        Rider(1, 45, .5f, 20, slide: 50), Rider(2, 45, 1.65f, 20 * 32.65f / 31.5f, slide: 50)}, 3);
    public static Scenario C() => new("SC-occupied", new[] {
        Rider(1, 45, 3, 20, combativeness: .95f), Rider(2, 45.4, 4.05f, 20.5f, target: 3.1f, combativeness: .95f)}, 2);
    public static Scenario CRisk() {
        var c = C(); return c with {Name = "SC-late-reaction", Riders = new[] {c.Riders[0], c.Riders[1] with {InitialOutwardSpeed = -2}}};
    }
    public static Scenario D(bool innerFirst) => new(innerFirst ? "SD-inner-turn" : "SD-outer-turn", new[] {
        Rider(1, innerFirst ? 34 : 32.7, 3, 19), Rider(2, innerFirst ? 32.7 : 34, 6, 20)}, 3);
    public static Scenario E() => Scenarios.G() with {Name = "SE-original-Motoarena"};
    public static Scenario F()
    {
        var gates = Scenarios.G();
        // One declared field: modest existing skill differences, not a sweep
        // for a forced outcome. Gate poses/targets/setups are copied unchanged.
        var reactions = new[] {55, 65, 45, 60}; var starts = new[] {45f, 60f, 50f, 55f};
        var speeds = new[] {50f, 55f, 60f, 65f}; var slides = new[] {60f, 65f, 70f, 65f};
        var attacks = new[] {70, 85, 80, 75}; var defenses = new[] {80, 70, 75, 65};
        var techniques = new[] {70, 60, 75, 65}; var combativeness = new[] {.45f, .8f, .55f, .6f};
        var riders = gates.Riders.Select((r, i) => {
            var gameplay = new RiderGameplayProfile(new(reactions[i], 50, techniques[i], 65, attacks[i], defenses[i], 50, 60),
                r.Profile.Gameplay.Physical, new(combativeness[i], PreferredLine.Neutral, .5f));
            var skills = new RiderSkills(starts[i], speeds[i], slides[i], 50, 50, 50);
            return r with {Profile = RiderProfile.CreateCanonical(r.Profile.Id, r.Profile.Name, gameplay, skills, r.Profile.Style)};
        }).ToArray();
        return gates with {Name = "SF-varied-Motoarena", Riders = riders};
    }
    public static Scenario G() => Scenarios.F() with {Name = "SG-F-close"};
    public static IReadOnlyList<Scenario> PairCases() => new[] {A(), BInside(), BOutside(), BHoldInside(), BNoGain(), C(), CRisk(), D(true), D(false)};
    public static IReadOnlyList<Scenario> FieldCases() => new[] {E(), F(), G()};
    public static IReadOnlyList<Scenario> Cases() => PairCases().Concat(FieldCases()).ToArray();
}
