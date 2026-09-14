using System.Globalization;
using CoreSim.Setup;

namespace CoreSim.Analysis;

/// <summary>Explicit measurement inputs, not game balance constants or a Cartesian parameter search.</summary>
public static class CalibrationScenarioCatalog
{
    public static CalibrationScenarioFixture Baseline { get; } = new(
        RiderSkills.Balanced, BikeSetup.Neutral, new TrackSurfaceState(1f, 0f, 0.35f));

    public static IReadOnlyList<CalibrationSurfaceFixture> Surfaces { get; } = Array.AsReadOnly(new[]
    {
        new CalibrationSurfaceFixture("baseline", Baseline.Surface),
        new CalibrationSurfaceFixture("grip_080", new TrackSurfaceState(0.8f, 0f, 0.35f)),
        new CalibrationSurfaceFixture("moisture_070", new TrackSurfaceState(1f, 0f, 0.7f)),
        new CalibrationSurfaceFixture("ruts_025", new TrackSurfaceState(1f, 0.25f, 0.35f)),
    });

    public static IReadOnlyList<CalibrationScenario> RequiredScenarios()
    {
        var scenarios = new List<CalibrationScenario>();
        var skillValues = new[] { 0, 25, 50, 75, 100 };
        var gearings = new[] { 0f, 0.5f, 1f };
        var geometry = Track.CreateStandingStartExample().Geometry;
        var capability = CalibrationScenarioSuite.ObserveCornerCapability(Baseline, 1f);

        AddStart("baseline", Baseline);
        scenarios.Add(new CalibrationStartScenario(Meta("start/pure_launch", CalibrationScenarioKind.Start,
            "Pure 35 m launch without first-corner preparation; not the prepared standing-start context."),
            Baseline, CalibrationStartMode.PureLaunch));
        foreach (var value in skillValues) AddStart($"start_skill/{Id(value)}", WithSkill("start", value));
        foreach (var gearing in gearings) AddStart($"gearing/{Id(gearing * 100)}", WithGearing(gearing));
        foreach (var surface in Surfaces) AddStart($"surface/{surface.Id}", WithSurface(surface));

        AddStraight("baseline", Baseline, 30f, 16f);
        foreach (var distance in new[] { 10, 20, 30, 60 })
            AddStraight($"distance/{Id(distance)}", Baseline, distance, 16f);
        foreach (var entry in new[] { 10, 16, 20, 24 })
            AddStraight($"entry_speed/{Id(entry)}", Baseline, 30f, entry);
        foreach (var value in skillValues) AddStraight($"speed/{Id(value)}", WithSkill("speed", value), 30f, 16f);
        // Three explicitly chosen distances expose short response versus approach to equilibrium.
        foreach (var distance in new[] { 10, 60, 600 })
        foreach (var gearing in gearings)
            AddStraight($"gearing/distance_{Id(distance)}/{Id(gearing * 100)}", WithGearing(gearing), distance, 16f);
        foreach (var surface in Surfaces) AddStraight($"surface/{surface.Id}", WithSurface(surface), 30f, 16f);

        foreach (var type in new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit })
        {
            var prefix = type switch
            {
                SegmentType.TurnEntry => "turn_entry", SegmentType.TurnMiddle => "turn_middle", _ => "turn_exit",
            };
            var kind = type switch
            {
                SegmentType.TurnEntry => CalibrationScenarioKind.TurnEntry,
                SegmentType.TurnMiddle => CalibrationScenarioKind.TurnMiddle, _ => CalibrationScenarioKind.TurnExit,
            };
            var bands = new[]
            {
                ("below_max", capability.MaxSafeSpeedMetersPerSecond * 0.98f),
                ("quiet", (capability.MaxSafeSpeedMetersPerSecond + capability.FirstBrakeSpeedMetersPerSecond) * 0.5f),
                ("brake", (capability.FirstBrakeSpeedMetersPerSecond + capability.FirstRunWideSpeedMetersPerSecond!.Value) * 0.5f),
                ("run_wide", (capability.FirstRunWideSpeedMetersPerSecond!.Value + capability.FirstCrashSpeedMetersPerSecond) * 0.5f),
                // A diagnostic margin above the observed transition avoids reversing scrub at a rounding boundary.
                ("crash_above_boundary", capability.FirstCrashSpeedMetersPerSecond + capability.MaxSafeSpeedMetersPerSecond * 0.001f),
            };
            var baselineEntry = Incoming(bands[2].Item2);
            AddTurn("baseline", Baseline, 1f, baselineEntry);
            foreach (var (band, speed) in bands) AddTurn($"band/{band}", Baseline, 1f, Incoming(speed));
            AddTurn("insufficient_distance", Baseline, 1f, Incoming(bands[2].Item2, 0.99f), 0.99f);
            foreach (var lateral in Enumerable.Range(0, 5))
                AddTurn($"lateral/{Id(lateral)}", Baseline, lateral, baselineEntry);
            foreach (var value in skillValues)
            {
                AddTurn($"slide_control/{Id(value)}", WithSkill("slide_control", value), 1f, baselineEntry);
                AddTurn($"speed/{Id(value)}", WithSkill("speed", value), 1f, baselineEntry);
            }
            foreach (var surface in Surfaces) AddTurn($"surface/{surface.Id}", WithSurface(surface), 1f, baselineEntry);
            if (type == SegmentType.TurnExit)
                foreach (var gearing in gearings) AddTurn($"gearing/{Id(gearing * 100)}", WithGearing(gearing), 1f, baselineEntry);
            foreach (var bias in gearings)
                AddTurn($"traction_bias/{Id(bias * 100)}", Baseline with { Setup = new BikeSetup(0.5f, bias) }, 1f, baselineEntry);
            // Deliberate two-axis setup/surface interaction; no launch/straight traction invention.
            foreach (var bias in new[] { 0f, 1f })
                AddTurn($"traction_bias_on_moisture_070/{Id(bias * 100)}",
                    WithSurface(Surfaces.Single(surface => surface.Id == "moisture_070")) with { Setup = new BikeSetup(0.5f, bias) },
                    1f, baselineEntry);

            float Incoming(float settledBandSpeed, float progress = 0f)
            {
                var types = new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit };
                var track = new Track(types.Select((label, i) => new TrackSegment(i, label)).ToArray(), geometry);
                var phase = track.CornerTopology.Resolve(Array.IndexOf(types, type), progress, 1f, geometry)!.Value;
                var envelope = ContinuousCornerEnvelope.Create(phase, 1f, geometry, Baseline.Surface, Baseline.Skills, Baseline.Setup);
                return settledBandSpeed / capability.MaxSafeSpeedMetersPerSecond * envelope.SpeedMetersPerSecond(phase.CornerProgress);
            }
            void AddTurn(string suffix, CalibrationScenarioFixture fixture, float lateral, float entry, float progress = 0f)
                => scenarios.Add(new CalibrationTurnScenario(Meta($"{prefix}/{suffix}", kind,
                    "Production Resolve in a full logical corner. Axis sweeps hold incoming speed fixed; band factors scale the local continuous envelope."),
                    fixture, type, lateral, entry, progress));
        }

        foreach (var lateral in Enumerable.Range(0, 5))
            scenarios.Add(new CalibrationLineScenario(Meta($"line_geometry/lateral/{Id(lateral)}", CalibrationScenarioKind.LineGeometry,
                "One identical rider, requested HoldLane 0-4; production consequences and wear remain active. Observed lateral range is reported."),
                Baseline, lateral));

        AddHeat("baseline", Baseline);
        foreach (var axis in new[] { "start", "speed", "slide_control" })
        {
            foreach (var value in skillValues) AddHeat($"{axis}/{Id(value)}", WithSkill(axis, value));
            var pattern = new[] { 25, 50, 50, 75 };
            scenarios.Add(new CalibrationHeatScenario(Meta($"full_heat/within_heat/{axis}", CalibrationScenarioKind.FullHeat,
                "Synthetic skill pattern [25,50,50,75] on fixed rider ids [1,2,3,4] / lanes [0,1,2,3]; compare matched identical baseline, not skill-only attribution."),
                Baseline, Enumerable.Range(1, 4).Select(id => new CalibrationScenarioRider(id, id - 1, WithSkill(axis, pattern[id - 1]).Skills))));
        }
        foreach (var surface in Surfaces) AddHeat($"surface/{surface.Id}", WithSurface(surface));
        foreach (var gearing in gearings) AddHeat($"gearing/{Id(gearing * 100)}", WithGearing(gearing));
        var extreme = WithSkill("speed", 100f) with { Setup = new BikeSetup(1f, 0.5f) };
        scenarios.Add(new CalibrationHeatScenario(Meta("full_heat/extreme_speed100_gearing100_outer",
            CalibrationScenarioKind.FullHeat, "Maximum-skill speed, speed-oriented gearing, lanes 0-3 including outer line 3, best initial surface; actual production heat, no cap."),
            extreme, Enumerable.Range(1, 4).Select(id => new CalibrationScenarioRider(id, id - 1, extreme.Skills))));
        scenarios.Add(new CalibrationLineScenario(Meta("line_geometry/extreme_speed100_gearing100_outer4",
            CalibrationScenarioKind.LineGeometry, "Maximum-skill speed, speed-oriented gearing, outermost line 4, best initial surface; actual production free heat, no cap."), extreme, 4));
        foreach (var axis in new[] { "track_reading", "adaptability", "pair_riding" })
        foreach (var value in new[] { 0, 100 })
        {
            AddStart($"{axis}/{Id(value)}", WithSkill(axis, value));
            AddStraight($"{axis}/{Id(value)}", WithSkill(axis, value), 30f, 16f);
            scenarios.Add(new CalibrationTurnScenario(Meta($"turn_middle/{axis}/{Id(value)}", CalibrationScenarioKind.TurnMiddle,
                "Isolated production corner probe: no decisions/contact influence is fabricated."),
                WithSkill(axis, value), SegmentType.TurnMiddle, 1f,
                (capability.FirstBrakeSpeedMetersPerSecond + capability.FirstRunWideSpeedMetersPerSecond!.Value) * 0.5f));
        }
        foreach (var progress in new[] { 0f, .125f, .25f, .375f, .5f, .625f, .75f, .875f, .999999f })
        {
            var index = Math.Min(2, (int)(progress * 3f));
            var types = new[] { SegmentType.TurnEntry, SegmentType.TurnMiddle, SegmentType.TurnExit };
            var kinds = new[] { CalibrationScenarioKind.TurnEntry, CalibrationScenarioKind.TurnMiddle, CalibrationScenarioKind.TurnExit };
            var track = new Track(types.Select((type, i) => new TrackSegment(i, type)).ToArray(), geometry);
            var phase = track.CornerTopology.Resolve(index, progress * 3f - index, 1f, geometry)!.Value;
            var envelope = ContinuousCornerEnvelope.Create(phase, 1f, geometry, Baseline.Surface, Baseline.Skills, Baseline.Setup);
            scenarios.Add(new CalibrationTurnScenario(Meta(
                $"continuous_corner/progress/{progress.ToString("0.000000", CultureInfo.InvariantCulture)}", kinds[index],
                "Production Resolve within a complete logical corner; initial speed equals the local reachable envelope."),
                Baseline, types[index], 1f, envelope.SpeedMetersPerSecond(progress), progress * 3f - index));
        }
        return Array.AsReadOnly(scenarios.OrderBy(scenario => scenario.Metadata.ScenarioId, StringComparer.Ordinal).ToArray());

        void AddStart(string suffix, CalibrationScenarioFixture fixture)
            => scenarios.Add(new CalibrationStartScenario(Meta($"start/{suffix}", CalibrationScenarioKind.Start,
                "35 m standing start with production immediate first-corner preparation; lateral 1."), fixture, CalibrationStartMode.FirstCornerPreparation));
        void AddStraight(string suffix, CalibrationScenarioFixture fixture, float distance, float entry)
            => scenarios.Add(new CalibrationStraightScenario(Meta($"straight/{suffix}", CalibrationScenarioKind.Straight,
                "Isolated force-based free drive, no following corner/preparation constraint."), fixture, distance, entry));
        void AddHeat(string suffix, CalibrationScenarioFixture fixture)
            => scenarios.Add(new CalibrationHeatScenario(Meta($"full_heat/{suffix}", CalibrationScenarioKind.FullHeat,
                "Four identical riders, ids 1-4 / lanes 0-3; production heat with evolving surface."), fixture,
                Enumerable.Range(1, 4).Select(id => new CalibrationScenarioRider(id, id - 1, fixture.Skills))));
    }

    private static CalibrationScenarioMetadata Meta(string id, CalibrationScenarioKind kind, string description) => new(id, kind, description);
    private static string Id(float value) => value.ToString("000", CultureInfo.InvariantCulture);
    private static CalibrationScenarioFixture WithGearing(float gearing) => Baseline with { Setup = new BikeSetup(gearing, 0.5f) };
    private static CalibrationScenarioFixture WithSurface(CalibrationSurfaceFixture surface) => Baseline with { Surface = surface.Surface };
    private static CalibrationScenarioFixture WithSkill(string axis, float value) => Baseline with
    {
        Skills = new RiderSkills(axis == "start" ? value : 50f, axis == "speed" ? value : 50f,
            axis == "slide_control" ? value : 50f, axis == "track_reading" ? value : 50f,
            axis == "pair_riding" ? value : 50f, axis == "adaptability" ? value : 50f),
    };
}
