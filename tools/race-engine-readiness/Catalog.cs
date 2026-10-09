using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;

namespace RaceReadiness;

internal sealed record Experiment(string Id, string Population, string Description, WeatherState Weather,
    Func<(Track Track, TrackState Surface, List<RiderState> Riders)> Create, bool Sweep = false);

internal static class Catalog
{
    internal static IReadOnlyList<Experiment> All()
    {
        var rows = new List<Experiment>();
        void Grid(string id, string trackName = "example", string surface = "uniform", string riders = "balanced",
            WeatherState? weather = null, string? variable = null, float value = 50)
        {
            rows.Add(new(id, variable is null ? "ordinary standing start" : "controlled sensitivity",
                $"{trackName}; {surface}; {riders}; {variable}={value}", weather ?? WeatherState.Dry, () =>
                {
                    var track = trackName == "motoarena" ? MatchedVenueProfiles.CreateMotoarenaStandingStartTrack()
                        : Track.CreateStandingStartExample();
                    var state = new TrackState(track.Segments.Count, 5, (_, lane) => surface switch
                    {
                        "inside" => new(1 - lane * .08f, lane * .1f, 0),
                        "outside" => new(.68f + lane * .08f, .4f - lane * .1f, 0),
                        "low-grip" => new(.65f, .2f, .1f),
                        _ => TrackSurfaceState.Default,
                    });
                    // A prior production heat applies ordinary deterministic passage wear. No fabricated worn cells.
                    if (surface == "worn")
                        new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(track, state, StartingGrid.Create(track,
                            Enumerable.Range(1, 4).Select(id => new StartingGateAssignment(RiderProfile.CreateDefault(id),
                                (StartingGate)(id - 1))).ToArray()).ToList(), new() { Seed = 7 });
                    var profiles = Enumerable.Range(1, 4).Select(id => Profile(id, riders, variable, value)).ToArray();
                    var grid = StartingGrid.Create(track, profiles.Select((p, i) => new StartingGateAssignment(p,
                        (StartingGate)i)).ToArray()).ToList();
                    if (variable == "Condition") grid[0].Condition = value;
                    return (track, state, grid);
                }, variable is not null));
        }
        Grid("balanced-example"); Grid("balanced-motoarena", "motoarena");
        foreach (var kind in new[] { "overall", "starter-distance", "corner-straight", "style", "reading", "control", "same-line" })
            Grid(kind, riders: kind);
        Grid("rain-example", weather: WeatherState.LightRain);
        Grid("rain-motoarena", "motoarena", weather: WeatherState.LightRain);
        foreach (var surface in new[] { "inside", "outside", "low-grip", "worn" }) Grid(surface, "motoarena", surface);
        foreach (var fixture in FourRiderBehaviorSuite.CreateScenarios().Where(s => new[] { "A", "B", "C", "D", "E", "H" }.Contains(s.Id)))
            AddFixture(fixture, "rolling-" + fixture.Id, "ordinary rolling fixture");
        var cornerCatch = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "D");
        AddFixture(cornerCatch with { Description = "Faster follower already behind at bend entry, using existing whole-bend geometry",
            Riders = cornerCatch.Riders.Select(r => r.Id == 1 ? r with { SegmentProgress = .0625f, SpeedMetersPerSecond = 18 }
                : r.Id == 2 ? r with { SegmentProgress = 0, SpeedMetersPerSecond = 23,
                    Skills = new(50, 80, 80, 50, 50, 50) } : r).ToArray() },
            "bend-catch", "ordinary rolling fixture");
        var stress = PhysicalContactConsequenceEvidence.HeatScenarios().Single(s => s.Id == "contact-heavy");
        AddFixture(stress, "contact-heavy", "extreme stress (excluded from ordinary rates)");
        var squeeze = stress with { Riders = stress.Riders.Select((r, i) => r with
            { Lane = i == 3 ? 4 : i + 1, SpeedMetersPerSecond = 18, SegmentProgress = i == 3 ? .4f : .1f }).ToArray() };
        AddFixture(squeeze, "three-squeeze", "controlled rolling squeeze");
        var regain = squeeze with { Riders = squeeze.Riders.Select((r, i) => r with
            { Lane = i, SegmentProgress = .1f, SpeedMetersPerSecond = 18 + i }).ToArray() };
        AddFixture(regain,
            "four-close-regain", "controlled rolling close race / reattack opportunity");
        // Separate legal-input controls isolate racing behavior from the decimal-boundary blocker.
        // Keep the original .1/.4 failures in the matrix; these are different experiments, not repairs.
        AddFixture(squeeze with { Description = "Three-rider squeeze binary-fraction companion; original decimal case retained",
            Riders = squeeze.Riders.Select(r => r with { SegmentProgress = r.Id == 4 ? .375f : .125f }).ToArray() },
            "three-squeeze-binary", "controlled rolling squeeze");
        AddFixture(regain with { Description = "Four-rider close reattack binary-fraction companion; original decimal case retained",
            Riders = regain.Riders.Select(r => r with { SegmentProgress = .125f }).ToArray() },
            "four-close-regain-binary", "controlled rolling close race / reattack opportunity");
        foreach (var variable in new[] { "Start", "Speed", "SlideControl", "TrackReading", "PairRiding", "Adaptability", "Technique", "Strength", "Mass", "Condition", "Style" })
        foreach (var value in variable switch { "Mass" => new[] { 60f, 67.5f, 75f }, "Condition" => new[] { .4f, .7f, 1f },
            "Style" => new[] { .2f, .5f, .8f }, _ => new[] { 20f, 50f, 80f } })
            Grid($"sweep-{variable}-{value}", "motoarena", variable: variable, value: value);
        return rows;

        void AddFixture(BehaviorScenario s, string id, string population)
            => rows.Add(new(id, population, s.Description, WeatherState.Dry,
                () => (s.Track, s.CreateSurface(), s.Riders.Select(r => r.Create(s.Track)).ToList())));
    }

    private static RiderProfile Profile(int id, string kind, string? variable, float value)
    {
        var values = Enumerable.Repeat(50f, 6).ToArray();
        var style = RiderStyle.Balanced;
        if (kind == "overall") Array.Fill(values, new[] { 20f, 40f, 60f, 80f }[id - 1]);
        if (kind == "starter-distance") { values[0] = id == 1 ? 80 : 35; values[1] = id == 1 ? 35 : id == 2 ? 80 : 50; }
        if (kind == "corner-straight") { values[2] = id == 1 ? 80 : 50; values[1] = id == 2 ? 80 : 50; }
        if (kind == "reading") values[3] = id % 2 == 0 ? 80 : 20;
        if (kind == "control") values[2] = id % 2 == 0 ? 80 : 20;
        if (kind == "style") style = new(id % 2 == 0 ? .8f : .2f, .5f, .5f, .5f);
        if (kind == "same-line") style = new(.5f, .8f, 0, .5f);
        if (id == 1 && variable is not null)
        {
            var index = Array.IndexOf(new[] { "Start", "Speed", "SlideControl", "TrackReading", "PairRiding", "Adaptability" }, variable);
            if (index >= 0) values[index] = value;
            if (variable == "Style") style = new(value, .5f, .5f, .5f);
        }
        var skills = new RiderSkills(values[0], values[1], values[2], values[3], values[4], values[5]);
        if (id == 1 && variable is "Technique" or "Strength" or "Mass")
            return RiderProfile.CreateCanonical(id, $"Audit {id}", new(new(50, 50,
                variable == "Technique" ? (int)value : 50, 50, 50, 50, 50,
                variable == "Strength" ? (int)value : 50), new(variable == "Mass" ? value : 67.5f),
                new(.5f, PreferredLine.Neutral, .5f)), skills, style);
        return new(id, $"Audit {id}", skills, style);
    }
}
