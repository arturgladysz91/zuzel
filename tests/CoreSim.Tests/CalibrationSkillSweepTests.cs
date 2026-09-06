using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

public sealed class CalibrationSkillSweepTests
{
    [Fact]
    public void RequiredSweepContainsEveryAxisAndTwentySevenCombinedPoints()
    {
        var scenarios = CalibrationSkillSweep.RequiredScenarios();
        Assert.Equal(43, scenarios.Count);
        Assert.Equal(27, scenarios.Count(item => item.ScenarioId.StartsWith("combined_", StringComparison.Ordinal)));
        foreach (var axis in new[] { "start", "speed", "slide_control" })
        foreach (var value in new[] { 0, 25, 50, 75, 100 })
            Assert.Contains(scenarios, item => item.ScenarioId == $"{axis}_{value}");
    }

    [Fact]
    public void SweepIsDeterministicForSameSeedAndInputs()
    {
        var scenario = new CalibrationSkillScenario("deterministic", RiderSkills.Balanced);
        Assert.Equal(
            CalibrationSkillSweep.RunScenario(scenario).Riders,
            CalibrationSkillSweep.RunScenario(scenario).Riders);
    }

    [Fact]
    public void InputOrderDoesNotChangePerRiderProductionObservations()
    {
        var scenario = new CalibrationSkillScenario("order", new RiderSkills(25, 75, 50, 50, 50, 50));
        var normal = CalibrationSkillSweep.RunScenario(scenario, new[] { 1, 2, 3, 4 });
        var reversed = CalibrationSkillSweep.RunScenario(scenario, new[] { 4, 3, 2, 1 });
        Assert.Equal(normal.Riders, reversed.Riders);
    }

    [Fact]
    public void StartSweepChangesReactionInExpectedDirection()
    {
        var slow = CalibrationSkillSweep.RunScenario(Scenario(start: 0));
        var fast = CalibrationSkillSweep.RunScenario(Scenario(start: 100));
        Assert.All(slow.Riders.Zip(fast.Riders), pair => Assert.True(pair.Second.ReactionTimeSeconds < pair.First.ReactionTimeSeconds));
    }

    [Fact]
    public void SpeedSweepChangesProductionPerformanceInExpectedDirection()
    {
        var slow = CalibrationSkillSweep.RunScenario(Scenario(speed: 0));
        var fast = CalibrationSkillSweep.RunScenario(Scenario(speed: 100));
        Assert.True(fast.Riders.Average(item => item.MaximumSpeedMetersPerSecond)
                    > slow.Riders.Average(item => item.MaximumSpeedMetersPerSecond));
        Assert.True(fast.Riders.Average(item => item.AverageSpeedMetersPerSecond)
                    > slow.Riders.Average(item => item.AverageSpeedMetersPerSecond));
    }

    [Fact]
    public void SlideControlImprovesControlledCornerPerformance()
    {
        var low = CalibrationSkillSweep.RunScenario(Scenario(slide: 0));
        var high = CalibrationSkillSweep.RunScenario(Scenario(slide: 100));
        Assert.True(high.Riders.Average(item => item.TotalTimeSeconds)
                    < low.Riders.Average(item => item.TotalTimeSeconds));
    }

    [Fact]
    public void SweepDoesNotModifyCommittedSourceDataset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "calibration", "pge", "v1", "pge_rider_heats.csv");
        var before = File.ReadAllBytes(path);
        _ = CalibrationSkillSweep.RunScenario(Scenario(speed: 75));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void CalibrationSampleCarriesExactRiderSkillsAndStableCsvOrder()
    {
        var skills = new RiderSkills(11, 22, 33, 44, 55, 66);
        var trace = RunSingleRider(skills, enableLogging: false);
        var sample = trace.StepSamples[0];
        Assert.Equal(11, sample.RiderStartSkill);
        Assert.Equal(22, sample.RiderSpeedSkill);
        Assert.Equal(33, sample.RiderSlideControlSkill);
        Assert.Equal(44, sample.RiderTrackReadingSkill);
        Assert.Equal(55, sample.RiderPairRidingSkill);
        Assert.Equal(66, sample.RiderAdaptabilitySkill);

        var rows = CalibrationCsvExporter.ExportSteps(trace).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = rows[0].Split(',');
        var expected = new[]
        {
            "RiderStartSkill", "RiderSpeedSkill", "RiderSlideControlSkill",
            "RiderTrackReadingSkill", "RiderPairRidingSkill", "RiderAdaptabilitySkill",
        };
        var start = Array.IndexOf(header, "RiderStartSkill");
        Assert.Equal(expected, header.Skip(start).Take(expected.Length));
        var values = rows[1].Split(',');
        Assert.Equal(new[] { "11", "22", "33", "44", "55", "66" }, values.Skip(start).Take(6));
    }

    [Fact]
    public void ObserverAndLoggingDoNotChangeProductionResultsOrTrackState()
    {
        var track = Track.CreateStandingStartExample();
        var surface = new TrackSurfaceState(1f, 0f, 0.35f);
        var withoutTrack = TrackState.CreateDefault(track, surface);
        var withTrack = TrackState.CreateDefault(track, surface);
        var withoutRiders = CreateRiders();
        var withRiders = CreateRiders();
        var withoutOptions = Options(enableLogging: true);
        var withOptions = Options(enableLogging: false);
        var decision = new HoldLane();
        var result = new HeatSimulator(decision).SimulateHeat(track, withoutTrack, withoutRiders, withoutOptions, 32);
        var trace = CalibrationRunner.RunHeat(track, withTrack, withRiders, decision, withOptions, 32);

        Assert.Equal(result.Classification, trace.Classification);
        Assert.Equal(withoutRiders.Select(RiderStateValue), withRiders.Select(RiderStateValue));
        for (var segment = 0; segment < track.Segments.Count; segment++)
        for (var lane = 0; lane < TrackSegment.LanesCount; lane++)
            Assert.Equal(withoutTrack.GetSurface(segment, lane), withTrack.GetSurface(segment, lane));
    }

    private static CalibrationSkillScenario Scenario(float start = 50, float speed = 50, float slide = 50)
        => new("test", new RiderSkills(start, speed, slide, 50, 50, 50));

    private static CalibrationTrace RunSingleRider(RiderSkills skills, bool enableLogging)
    {
        var track = Track.CreateStandingStartExample();
        var rider = new RiderState(new RiderProfile(1, "test", skills, RiderStyle.Balanced), 0)
        {
            ActiveSetup = BikeSetup.Neutral,
        };
        return CalibrationRunner.RunHeat(
            track,
            TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, 0.35f)),
            new List<RiderState> { rider },
            new HoldLane(),
            Options(enableLogging),
            32);
    }

    private static List<RiderState> CreateRiders()
        => Enumerable.Range(1, 4).Select(id => new RiderState(
            new RiderProfile(id, $"Rider {id}", RiderSkills.Balanced, RiderStyle.Balanced), id - 1)
        {
            ActiveSetup = BikeSetup.Neutral,
        }).ToList();

    private static HeatSimulationOptions Options(bool enableLogging)
        => new()
        {
            Laps = 4,
            Seed = CalibrationSkillSweep.FixedSeed,
            Weather = WeatherState.Dry,
            IncidentFrequency = 0,
            EnableLogging = enableLogging,
        };

    private static object RiderStateValue(RiderState rider)
        => new
        {
            rider.RiderId,
            rider.Position,
            rider.LastResolvedSegmentId,
            rider.Lane,
            rider.LateralPosition,
            rider.Speed,
            rider.Status,
            rider.ElapsedTimeSeconds,
            rider.Morale,
        };

    private sealed class HoldLane : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane, 0);
        public RiderDecision Decide(RiderDecisionContext context) => new(context.Rider.Lane, 0);
    }
}
