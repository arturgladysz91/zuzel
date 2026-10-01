using System.Globalization;
using System.Text.Json;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class FourRiderBehaviorSuiteTests
{
    private static readonly Lazy<BehaviorAuditResult> Audit = new(() => FourRiderBehaviorSuite.RunSuite());

    public static IEnumerable<object[]> Cases() => FourRiderBehaviorSuite.CreateScenarios()
        .SelectMany(item => new[] { 0, 7, 31 }.Select(seed => new object[] { item.Id, seed }));

    [Fact]
    public void Main_cases_cover_all_inputs_and_exact_stable_seed_set()
    {
        var result = Audit.Value;
        Assert.Equal(new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J-tight", "J-wide" },
            result.Scenarios.Select(item => item.Id));
        Assert.Equal(11 * 32, result.Runs.Count);
        foreach (var scenario in result.Scenarios)
        {
            Assert.Equal(4, scenario.Riders.Count);
            Assert.Equal(new[] { 1, 2, 3, 4 }, scenario.Riders.Select(item => item.Id));
            Assert.Equal(Enumerable.Range(0, 32), result.Runs.Where(item => item.ScenarioId == scenario.Id).Select(item => item.Seed));
        }
        Assert.All(result.Runs, run => Assert.Equal(4, run.Classification.Count));
    }

    [Fact]
    public void Production_options_never_enable_calibration_or_legacy_physics()
    {
        var options = FourRiderBehaviorSuite.ProductionOptions(31);
        Assert.Equal(4, options.Laps);
        Assert.Equal(1f, options.IncidentFrequency);
        Assert.Null(options.StraightDriveEnvelopeAdjustment);
        Assert.Null(options.CornerReducedDriveResistanceAdjustment);
        Assert.Null(options.PreApexScrubLossAdjustment);
        Assert.Null(options.ActiveCorrectionControlLossAdjustment);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Repeated_and_reversed_rider_collections_have_identical_typed_evidence(string id, int seed)
    {
        var scenario = FourRiderBehaviorSuite.CreateScenarios().Single(item => item.Id == id);
        var first = FourRiderBehaviorSuite.Run(scenario, seed);
        var repeated = FourRiderBehaviorSuite.Run(scenario, seed);
        var reversed = FourRiderBehaviorSuite.Run(scenario, seed, reverseRiders: true);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(repeated));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(reversed));
    }

    [Fact]
    public void Read_only_instrumentation_matches_plain_production_simulator_and_surface()
    {
        foreach (var scenario in FourRiderBehaviorSuite.CreateScenarios())
        {
            var observedSurface = scenario.CreateSurface();
            var observed = FourRiderBehaviorSuite.Run(scenario, 7, surface: observedSurface);
            var plainSurface = scenario.CreateSurface();
            var riders = scenario.Riders.Select(item => item.Create(scenario.Track)).ToList();
            var plain = new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track, plainSurface, riders,
                FourRiderBehaviorSuite.ProductionOptions(7), FourRiderBehaviorSuite.HeatId);
            Assert.Equal(plain.Classification, observed.Classification);
            Assert.Equal(plain.Log.Overtakes, observed.ProductionOvertakes);
            for (var segment = 0; segment < plainSurface.SegmentCount; segment++)
            for (var lane = 0; lane < plainSurface.LinesCount; lane++)
                Assert.Equal(plainSurface.GetSurface(segment, lane), observedSurface.GetSurface(segment, lane));
        }
    }

    [Fact]
    public void Actual_runs_and_rendering_are_invariant_under_en_US_and_pl_PL()
    {
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            string[] RunCulture(string name)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                return FourRiderBehaviorSuite.CreateScenarios().Select(item =>
                    JsonSerializer.Serialize(FourRiderBehaviorSuite.Run(item, 7))).ToArray();
            }
            Assert.Equal(RunCulture("en-US"), RunCulture("pl-PL"));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var markdown = FourRiderBehaviorReport.Render(Audit.Value);
            var evidence = FourRiderBehaviorReport.RenderEvidence(Audit.Value);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal(markdown, FourRiderBehaviorReport.Render(Audit.Value));
            Assert.Equal(evidence, FourRiderBehaviorReport.RenderEvidence(Audit.Value));
            Assert.DoesNotContain('\r', markdown);
            Assert.DoesNotContain('\r', evidence);
            Assert.EndsWith("\n", markdown);
            Assert.EndsWith("\n", evidence);
            using var json = JsonDocument.Parse(evidence);
            Assert.Equal(FourRiderBehaviorSuite.BaselineSha, json.RootElement.GetProperty("BaselineSha").GetString());
        }
        finally { CultureInfo.CurrentCulture = original; CultureInfo.CurrentUICulture = originalUi; }
    }

    [Fact]
    public void Mid_segment_inputs_represent_real_initial_gap_and_post_apex_progress()
    {
        var scenarios = FourRiderBehaviorSuite.CreateScenarios();
        var chase = scenarios.Single(item => item.Id == "A");
        Assert.Equal(15f, chase.Riders[0].Create(chase.Track).DistanceMeters);
        Assert.Equal(0f, chase.Riders[1].Create(chase.Track).DistanceMeters);
        var exit = scenarios.Single(item => item.Id == "H");
        Assert.Single(exit.Track.CornerTopology.Corners.Where(item => item.ContainsSegment(0)));
        var phase = exit.Track.CornerTopology.Resolve(0, exit.Riders[1].SegmentProgress, 4f, exit.Track.Geometry);
        Assert.NotNull(phase);
        Assert.Equal(.6f, phase.Value.CornerProgress);
        var run = Audit.Value.Runs.Single(item => item.ScenarioId == "H" && item.Seed == 0);
        Assert.All(run.Steps.Where(item => item.Step == 0 && item.RiderId <= 2),
            item => Assert.Null(item.ActualSampledApexSpeedMetersPerSecond));
    }

    [Fact]
    public void Pair_inversions_have_observed_boundaries_not_fabricated_crossing_times()
    {
        foreach (var run in Audit.Value.Runs)
        foreach (var pass in run.BoundaryPasses)
        {
            var sample = run.Steps.Single(item => item.Step == pass.Step && item.RiderId == pass.RiderId);
            var passed = run.Steps.Single(item => item.Step == pass.Step && item.RiderId == pass.PassedRiderId);
            Assert.Equal(sample.EndProgress, pass.ObservedBoundaryProgress);
            Assert.True(sample.Status is RiderRaceStatus.Racing or RiderRaceStatus.Finished);
            Assert.True(passed.Status is RiderRaceStatus.Racing or RiderRaceStatus.Finished);
            Assert.True(sample.EndTimeSeconds <= passed.EndTimeSeconds);
        }
    }

    [Fact]
    public void All_histories_reconcile_with_actual_decisions_and_entry_path_geometry()
    {
        foreach (var run in Audit.Value.Runs)
        {
            var scenario = Audit.Value.Scenarios.Single(item => item.Id == run.ScenarioId);
            var choices = run.Choices.ToDictionary(item => (item.Step, item.RiderId));
            foreach (var step in run.Steps)
            {
                Assert.Equal(choices[(step.Step, step.RiderId)].TargetLane, step.TargetLane);
                Assert.InRange(step.ExitLateralPosition, 0f, 4f);
                Assert.True(step.EndTimeSeconds >= step.StartTimeSeconds);
                var expectedDistance = LaneModel.SegmentLengthMeters(scenario.Track.Segments[step.SegmentIndex],
                    step.EntryLateralPosition, scenario.Track.Geometry) * (float)(step.EndProgress - step.StartProgress);
                Assert.InRange(MathF.Abs(expectedDistance - step.PathDistanceMeters), 0f, .001f);
                if (step.CornerStartProgress is { } start && step.CornerEndProgress is { } end)
                    Assert.True(end >= start && end <= 1.00001f);
            }
        }
    }

    [Fact]
    public void Matched_skill_controls_change_exactly_one_skill_of_exactly_one_rider()
    {
        foreach (var pair in Audit.Value.SkillPairs)
        {
            Assert.Equal(pair.Low.Seed, pair.High.Seed);
            foreach (var low in pair.LowInput.Riders)
            {
                var high = pair.HighInput.Riders.Single(item => item.Id == low.Id);
                if (low.Id != 2) { Assert.Equal(low, high); continue; }
                Assert.Equal(low with { Skills = high.Skills }, high);
                var changed = typeof(RiderSkills).GetProperties().Where(property => property.PropertyType == typeof(float))
                    .Where(property => !Equals(property.GetValue(low.Skills), property.GetValue(high.Skills))).ToArray();
                Assert.Single(changed);
                Assert.Equal(pair.Skill, changed[0].Name);
                Assert.Equal(20f, changed[0].GetValue(low.Skills));
                Assert.Equal(80f, changed[0].GetValue(high.Skills));
            }
        }
    }

    [Fact]
    public void Single_seed_control_has_no_stochastic_outcome_and_is_seed_independent()
    {
        var first = FourRiderBehaviorSuite.RunNonStochasticControl(0);
        var other = FourRiderBehaviorSuite.RunNonStochasticControl(31);
        Assert.Empty(first.Contacts);
        Assert.Empty(first.BoundaryPasses);
        Assert.Equal(0, first.Crashes);
        Assert.Equal(JsonSerializer.Serialize(first with { Seed = 31 }), JsonSerializer.Serialize(other));
    }

    [Fact]
    public void Fixed_mechanics_controls_are_separate_and_have_reproducible_inputs()
    {
        Assert.Equal(new[] { "crossing", "entry-hold", "entry-inward", "equal-start" }, Audit.Value.DiagnosticControls.Select(item => item.Id));
        foreach (var control in Audit.Value.DiagnosticControls)
        {
            IRiderDecisionModel model = new DiagnosticDecision(control.Id);
            var repeated = FourRiderBehaviorSuite.Run(control.Input, 31, model, incidentFrequency: 0f,
                onlyRiderId: control.Id.StartsWith("entry-", StringComparison.Ordinal) ? 2 : null);
            Assert.Equal(JsonSerializer.Serialize(control.Run with { Seed = 31 }), JsonSerializer.Serialize(repeated));
        }
    }

    [Fact]
    public void Wear_feedback_compares_identical_first_heat_and_retains_surface_afterwards()
    {
        var heats = Audit.Value.WearHeats;
        Assert.Equal(6, heats.Count);
        Assert.Equal(0, heats[0].DecisionsDifferentFromFresh);
        Assert.Equal(heats[0].TargetHistogram, heats[0].FreshTargetHistogram);
        Assert.All(heats, heat => Assert.True(heat.MeanRutsAfter >= heat.MeanRutsBefore));
        Assert.All(heats, heat => Assert.True(heat.MeanGripAfter <= heat.MeanGripBefore));
        for (var index = 1; index < heats.Count; index++)
        {
            Assert.Equal(heats[index - 1].MeanGripAfter, heats[index].MeanGripBefore);
            Assert.Equal(heats[index - 1].MeanRutsAfter, heats[index].MeanRutsBefore);
        }
    }

    private sealed class DiagnosticDecision(string id) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(Target(rider.RiderId, rider.Lane), 0f);
        public RiderDecision Decide(RiderDecisionContext context) => new(Target(context.Rider.RiderId, context.Rider.Lane), 0f);
        private int Target(int riderId, int lane) => id switch
        {
            "crossing" => riderId == 1 ? 1 : riderId == 2 ? 0 : lane,
            "entry-hold" => 3,
            "entry-inward" => 0,
            _ => lane,
        };
    }
}
