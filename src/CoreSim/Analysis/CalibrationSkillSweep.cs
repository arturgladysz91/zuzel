using System.Collections.ObjectModel;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;

namespace CoreSim.Analysis;

public sealed record CalibrationSkillScenario(string ScenarioId, RiderSkills Skills);

public sealed record CalibrationSkillRiderObservation(
    int RiderId,
    float ReactionTimeSeconds,
    float LaunchMovementTimeSeconds,
    float? TimeTo70KphSeconds,
    float? SpeedAtTwoSecondsMetersPerSecond,
    float? FirstCurveEntrySpeedMetersPerSecond,
    float? L1Seconds,
    float? L2Seconds,
    float? L3Seconds,
    float? L4Seconds,
    float? FlyingLapMedianSeconds,
    float? FirstLapPenaltySeconds,
    float MaximumSpeedMetersPerSecond,
    float TotalDistanceMeters,
    float TotalTimeSeconds,
    float? AverageSpeedMetersPerSecond,
    int RunWideCount,
    int BrakeCount,
    int CrashCount);

public sealed class CalibrationSkillSweepResult
{
    private readonly ReadOnlyCollection<CalibrationSkillRiderObservation> _riders;

    public CalibrationSkillScenario Scenario { get; }
    public IReadOnlyList<CalibrationSkillRiderObservation> Riders => _riders;

    internal CalibrationSkillSweepResult(
        CalibrationSkillScenario scenario,
        IEnumerable<CalibrationSkillRiderObservation> riders)
    {
        Scenario = scenario;
        _riders = Array.AsReadOnly(riders.OrderBy(item => item.RiderId).ToArray());
    }

    public SimulationCalibrationResult ToSimulationCalibrationResult()
    {
        var metrics = new List<SimulationMetricSeries>();
        Add("pge_clean_vmax", "km/h", Riders.Select(item => (double?)CalibrationUnits.MetersPerSecondToKph(item.MaximumSpeedMetersPerSecond)));
        Add("pge_clean_average_speed", "m/s", Riders.Select(item => (double?)item.AverageSpeedMetersPerSecond));
        Add("pge_clean_l1_penalty", "s", Riders.Select(item => (double?)item.FirstLapPenaltySeconds));
        Add("pge_clean_heat_time", "s", Riders.Select(item => (double?)item.TotalTimeSeconds));
        Add("pge_clean_l1_time", "s", Riders.Select(item => (double?)item.L1Seconds));
        Add("pge_clean_l2_time", "s", Riders.Select(item => (double?)item.L2Seconds));
        Add("pge_clean_l3_time", "s", Riders.Select(item => (double?)item.L3Seconds));
        Add("pge_clean_l4_time", "s", Riders.Select(item => (double?)item.L4Seconds));
        Add("pge_clean_flying_lap_median", "s", Riders.Select(item => (double?)item.FlyingLapMedianSeconds));
        Add("pge_clean_total_distance", "m", Riders.Select(item => (double?)item.TotalDistanceMeters));
        Add("pge_individual_reaction_time", "s", Riders.Select(item => (double?)item.ReactionTimeSeconds));
        Add("pge_individual_speed_at_2s", "km/h", Riders.Select(item => item.SpeedAtTwoSecondsMetersPerSecond is { } speed
            ? (double?)CalibrationUnits.MetersPerSecondToKph(speed)
            : null));
        Add("pge_individual_first_curve_speed", "km/h", Riders.Select(item => item.FirstCurveEntrySpeedMetersPerSecond is { } speed
            ? (double?)CalibrationUnits.MetersPerSecondToKph(speed)
            : null));

        if (Riders.Count == 4)
        {
            Add("pge_four_rider_heat_time_spread", "s", new[] { Spread(Riders.Select(item => (double?)item.TotalTimeSeconds)) });
            Add("pge_four_rider_vmax_spread", "km/h", new[] { Spread(Riders.Select(item => (double?)CalibrationUnits.MetersPerSecondToKph(item.MaximumSpeedMetersPerSecond))) });
            Add("pge_four_rider_l1_spread", "s", new[] { Spread(Riders.Select(item => (double?)item.L1Seconds)) });
            Add("pge_four_rider_average_speed_spread", "m/s", new[] { Spread(Riders.Select(item => (double?)item.AverageSpeedMetersPerSecond)) });
        }
        return new SimulationCalibrationResult(Scenario.ScenarioId, metrics);

        void Add(string id, string unit, IEnumerable<double?> values)
        {
            var actual = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
            if (actual.Length > 0)
                metrics.Add(new SimulationMetricSeries(id, unit, actual));
        }
    }

    private static double? Spread(IEnumerable<double?> values)
    {
        var materialized = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return materialized.Length == 4 ? materialized.Max() - materialized.Min() : null;
    }
}

/// <summary>Controlled diagnostics that run only through CalibrationRunner -> HeatSimulator.</summary>
public static class CalibrationSkillSweep
{
    public const int FixedSeed = 320032;

    public static IReadOnlyList<CalibrationSkillScenario> RequiredScenarios()
    {
        var scenarios = new List<CalibrationSkillScenario>
        {
            new("balanced", RiderSkills.Balanced),
        };
        foreach (var value in new[] { 0f, 25f, 50f, 75f, 100f })
        {
            scenarios.Add(new($"start_{value:0}", Skills(start: value)));
            scenarios.Add(new($"speed_{value:0}", Skills(speed: value)));
            scenarios.Add(new($"slide_control_{value:0}", Skills(slideControl: value)));
        }
        foreach (var start in new[] { 25f, 50f, 75f })
        foreach (var speed in new[] { 25f, 50f, 75f })
        foreach (var slideControl in new[] { 25f, 50f, 75f })
        {
            scenarios.Add(new(
                $"combined_start_{start:0}_speed_{speed:0}_slide_{slideControl:0}",
                Skills(start, speed, slideControl)));
        }
        return Array.AsReadOnly(scenarios.ToArray());
    }

    public static IReadOnlyList<CalibrationSkillSweepResult> RunRequired()
        => Array.AsReadOnly(RequiredScenarios().Select(scenario => RunScenario(scenario)).ToArray());

    public static CalibrationSkillSweepResult RunScenario(
        CalibrationSkillScenario scenario,
        IEnumerable<int>? riderOrder = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var order = (riderOrder ?? new[] { 1, 2, 3, 4 }).ToArray();
        if (order.Length != 4 || !order.Order().SequenceEqual(new[] { 1, 2, 3, 4 }))
            throw new ArgumentException("The controlled fixture requires rider ids 1, 2, 3 and 4 exactly once.", nameof(riderOrder));

        var track = Track.CreateStandingStartExample();
        var perfectNeutralSurface = new TrackSurfaceState(grip: 1f, ruts: 0f, moisture: 0.35f);
        var trackState = TrackState.CreateDefault(track, perfectNeutralSurface);
        var riders = order.Select(riderId =>
        {
            var profile = new RiderProfile(riderId, $"Calibration Rider {riderId}", scenario.Skills, RiderStyle.Balanced);
            return new RiderState(profile, lane: riderId - 1)
            {
                ActiveSetup = BikeSetup.Neutral,
            };
        }).ToList();
        var options = new HeatSimulationOptions
        {
            Laps = 4,
            Seed = FixedSeed,
            Weather = WeatherState.Dry,
            IncidentFrequency = 0f,
            EnableLogging = false,
        };
        var trace = CalibrationRunner.RunHeat(
            track,
            trackState,
            riders,
            new HoldLaneDecisionModel(),
            options,
            heatId: 32);
        return new CalibrationSkillSweepResult(scenario, ObserveRiders(trace));
    }

    // Shared standing-start telemetry projection; existing sweep behavior and API are unchanged.
    internal static IReadOnlyList<CalibrationSkillRiderObservation> ObserveRiders(CalibrationTrace trace)
    {
        var observations = trace.RiderSummaries.Select(summary =>
        {
            var samples = trace.StepSamples.Where(item => item.RiderId == summary.RiderId).ToArray();
            var launch = samples.Single(item => item.LapIndex == 0 && item.SegmentIndex == 0);
            var firstCurve = samples.FirstOrDefault(item => item.LapIndex == 0 && item.SegmentType == SegmentType.TurnEntry);
            var laps = trace.LapSummaries.Where(item => item.RiderId == summary.RiderId)
                .OrderBy(item => item.LapNumber).ToArray();
            var flying = laps.Length >= 4
                ? Median(laps[1].LapTimeSeconds, laps[2].LapTimeSeconds, laps[3].LapTimeSeconds)
                : (float?)null;
            return new CalibrationSkillRiderObservation(
                summary.RiderId,
                launch.StandingStartReactionTimeSeconds
                    ?? throw new InvalidOperationException("Controlled fixture did not execute standing start."),
                launch.StandingStartMovementTimeSeconds
                    ?? throw new InvalidOperationException("Controlled fixture did not execute standing start movement."),
                launch.StandingStartTimeTo70KphSeconds,
                launch.StandingStartSpeedAtTwoSecondsMetersPerSecond,
                firstCurve?.EntrySpeedMetersPerSecond,
                Lap(0), Lap(1), Lap(2), Lap(3),
                flying,
                flying is { } median && laps.Length >= 1 ? laps[0].LapTimeSeconds - median : null,
                summary.MaxSpeedMetersPerSecond,
                summary.TotalDistanceMeters,
                summary.TotalTimeSeconds,
                summary.AverageSpeedMetersPerSecond,
                samples.Count(item => item.Outcome == SegmentOutcome.RunWide),
                samples.Count(item => item.Outcome == SegmentOutcome.Brake),
                samples.Count(item => item.Outcome == SegmentOutcome.Crash));

            float? Lap(int index) => laps.Length > index ? laps[index].LapTimeSeconds : null;
        });
        return Array.AsReadOnly(observations.OrderBy(item => item.RiderId).ToArray());
    }

    private static RiderSkills Skills(
        float start = 50f,
        float speed = 50f,
        float slideControl = 50f)
        => new(start, speed, slideControl, 50f, 50f, 50f);

    private static float Median(float first, float second, float third)
    {
        var values = new[] { first, second, third };
        Array.Sort(values);
        return values[1];
    }

    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(rider.Lane, 0f);

        public RiderDecision Decide(RiderDecisionContext context)
            => new(context.Rider.Lane, 0f);
    }
}
