using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "historical-analysis")]
public sealed class CalibrationScenarioSuiteTests
{
    private static readonly IReadOnlyList<CalibrationScenario> Definitions = CalibrationScenarioCatalog.RequiredScenarios();
    private static readonly IReadOnlyList<CalibrationScenarioResult> Results = CalibrationScenarioSuite.Run(Definitions);
    private const string Provenance = "4572ad9c5af032572f528f0ce434df9c97153594";

    [Fact]
    public void CatalogContainsEveryFamilyWithUniqueOrdinalSortedIds()
    {
        var ids = Definitions.Select(item => item.Metadata.ScenarioId).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
        Assert.Equal(Enum.GetValues<CalibrationScenarioKind>().Order(), Definitions.Select(item => item.Metadata.Kind).Distinct().Order());
        Assert.Equal(ids, Results.Select(result => result.Metadata.ScenarioId));
        Assert.Equal(6, Definitions.OfType<CalibrationLineScenario>().Count());
        Assert.Equal(3, Definitions.OfType<CalibrationHeatScenario>().Count(item => item.Metadata.ScenarioId.Contains("/within_heat/", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("start/start_skill/")]
    [InlineData("straight/speed/")]
    [InlineData("turn_entry/slide_control/")]
    [InlineData("turn_middle/slide_control/")]
    [InlineData("turn_exit/speed/")]
    [InlineData("full_heat/speed/")]
    [InlineData("full_heat/slide_control/")]
    public void RequiredSkillAxesContainFiveExplicitValues(string prefix)
        => Assert.Equal(new[] { "000", "025", "050", "075", "100" },
            Definitions.Where(item => item.Metadata.ScenarioId.StartsWith(prefix, StringComparison.Ordinal))
                .Select(item => item.Metadata.ScenarioId[prefix.Length..]));

    [Fact]
    public void EachExecutionAndReportIsDeterministicAndScenarioOrderIndependent()
    {
        var again = CalibrationScenarioSuite.Run(Definitions.Reverse());
        var first = Report(Results);
        Assert.Equal(first, Report(again));
        Assert.DoesNotContain('\r', first);
        Assert.DoesNotContain("NaN", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", first, StringComparison.Ordinal);
        foreach (var pair in Results.Zip(again))
        {
            if (pair.First is CalibrationHeatResult heat)
                Assert.Equal(heat.Riders, ((CalibrationHeatResult)pair.Second).Riders);
            else if (pair.First is CalibrationLineResult line)
                Assert.Equal(line.Rider, ((CalibrationLineResult)pair.Second).Rider);
            else Assert.Equal(pair.First, pair.Second);
        }
    }

    [Fact]
    public void ReportAndIdsUseInvariantCultureAndStableSectionOrder()
    {
        var before = CultureInfo.CurrentCulture;
        var expected = Report(Results);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal(Definitions.Select(item => item.Metadata.ScenarioId),
                CalibrationScenarioCatalog.RequiredScenarios().Select(item => item.Metadata.ScenarioId));
            Assert.Equal(expected, Report(Results.Reverse()));
        }
        finally { CultureInfo.CurrentCulture = before; }
        var sections = expected.Split('\n').Where(line => line.StartsWith("## ", StringComparison.Ordinal));
        Assert.Equal(new[] { "Provenance", "Inputs", "Start", "Straight", "TurnEntry", "TurnMiddle", "TurnExit",
            "Line Geometry", "Full Heat", "Within-Heat", "Real-world context", "Interpretation" }.Select(name => $"## {name}"), sections);
    }

    [Fact]
    public void EveryHeatIsIndependentOfInputRiderOrder()
    {
        foreach (var scenario in Definitions.OfType<CalibrationHeatScenario>())
        {
            var expected = (CalibrationHeatResult)Results.Single(result => result.Metadata.ScenarioId == scenario.Metadata.ScenarioId);
            var reversed = CalibrationScenarioSuite.RunHeat(scenario, scenario.Riders.Select(rider => rider.RiderId).Reverse());
            Assert.Equal(expected.Riders, reversed.Riders);
            Assert.Equal(expected.Spreads, reversed.Spreads);
            Assert.Equal(expected.Trace.StepSamples, reversed.Trace.StepSamples);
            Assert.Equal(expected.Trace.Classification, reversed.Trace.Classification);
        }
    }

    [Fact]
    public void FullHeatBaselineReusesTheExistingSkillSweepObservationContract()
    {
        var existing = CalibrationSkillSweep.RunScenario(new CalibrationSkillScenario("baseline", RiderSkills.Balanced));
        var current = Get<CalibrationHeatResult>("full_heat/baseline");
        Assert.Equal(existing.Riders, current.Riders.Select(rider => rider.Performance));
    }

    [Fact]
    public void AllNumericInputsProfilesAndTelemetryAreFinite()
    {
        foreach (var result in Results) Finite(result);
        static void Finite(object? value)
        {
            if (value is null or string) return;
            if (value is float single) { Assert.True(float.IsFinite(single)); return; }
            if (value is double number) { Assert.True(double.IsFinite(number)); return; }
            if (value is IEnumerable collection) { foreach (var item in collection) Finite(item); return; }
            if (value.GetType().Namespace?.StartsWith("CoreSim", StringComparison.Ordinal) == true && !value.GetType().IsEnum)
                foreach (var property in value.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(property => property.GetIndexParameters().Length == 0))
                    Finite(property.GetValue(value));
        }
    }

    [Fact]
    public void LaunchAndStraightProfilesConserveDistanceAndUseCanonicalPrimitives()
    {
        foreach (var start in Results.OfType<CalibrationStartResult>())
        {
            var p = start.Profile;
            Near(start.DistanceMeters, p.AccelerationDistanceMeters + p.CruiseDistanceMeters + p.PreparationDistanceMeters);
            Near(p.TotalTimeSeconds, p.ReactionTimeSeconds + p.MovementTimeSeconds);
        }
        foreach (var straight in Results.OfType<CalibrationStraightResult>())
        {
            var p = straight.Profile;
            var s = straight.Scenario;
            var f = s.Fixture;
            Near(s.DistanceMeters, p.AccelerationDistanceMeters + p.CruiseDistanceMeters + p.DecelerationDistanceMeters);
            Assert.Equal(LongitudinalDynamics.CalculateForceBasedStraightSpeedProfile(s.EntrySpeedMetersPerSecond,
                f.Skills, f.Setup, f.Surface,
                LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(f.Skills, f.Surface), s.DistanceMeters), p);
        }
        var pure = Get<CalibrationStartResult>("start/pure_launch");
        var fixture = pure.Scenario.Fixture;
        Assert.Equal(LongitudinalDynamics.CalculateStandingStartLaunchProfile(fixture.Skills, fixture.Setup, fixture.Surface, pure.DistanceMeters), pure.Profile);
    }

    [Fact]
    public void RecoverableCornerPhasesUseEveryPhysicalMeterExactlyOnce()
    {
        foreach (var r in Results.OfType<CalibrationTurnResult>().Where(r => r.Change.Outcome != SegmentOutcome.Crash))
        {
            var p = Assert.IsType<ContinuousCornerTraversalProfile>(r.Diagnostics.ContinuousCornerProfile);
            if (r.Diagnostics.ExecutedPath is null) Near(r.AvailableDistanceMeters, r.Diagnostics.TravelledMeters);
            else Near(r.Diagnostics.ExecutedPath.DistanceMeters, r.Diagnostics.TravelledMeters);
            Near(r.Diagnostics.TravelledMeters, p.CorrectionDistanceMeters + p.CarryDistanceMeters + p.DriveDistanceMeters);
            Near(r.Diagnostics.TravelTimeSeconds, p.CorrectionTimeSeconds + p.CarryTimeSeconds + p.DriveTimeSeconds);
            Assert.Null(r.Diagnostics.TurnEntryScrubProfile);
            Assert.Null(r.Diagnostics.TurnExitDriveProfile);
        }
    }

    [Theory]
    [InlineData("turn_entry")]
    [InlineData("turn_middle")]
    [InlineData("turn_exit")]
    public void DerivedBandsObserveTheIntendedProductionClassifications(string family)
    {
        foreach (var (band, expected) in new[] { ("below_max", SegmentOutcome.Ok), ("quiet", SegmentOutcome.Ok),
            ("brake", SegmentOutcome.Brake), ("run_wide", SegmentOutcome.RunWide), ("crash_above_boundary", SegmentOutcome.Crash) })
            Assert.Equal(expected, Get<CalibrationTurnResult>($"{family}/band/{band}").Change.Outcome);
        Assert.Null(Get<CalibrationTurnResult>($"{family}/band/below_max").Diagnostics.CornerSpeedCorrectionProfile);
        Assert.NotNull(Get<CalibrationTurnResult>($"{family}/band/quiet").Diagnostics.ContinuousCornerProfile);
    }

    [Theory]
    [InlineData("turn_entry")]
    [InlineData("turn_middle")]
    [InlineData("turn_exit")]
    public void InsufficientLegalRemainingDistanceLeavesResidualOverspeedAndNoDrive(string family)
    {
        var r = Get<CalibrationTurnResult>($"{family}/insufficient_distance");
        var c = r.Diagnostics.ContinuousCornerProfile!;
        Assert.False(c.TargetReached);
        Assert.True(c.ExitSpeedMetersPerSecond > c.EnvelopeAtExitMetersPerSecond);
        Assert.True(r.ResidualOverspeedMetersPerSecond > 0f);
        Assert.Equal(0f, c.CarryDistanceMeters);
        Assert.Equal(0f, r.DriveDistanceMeters);
        Assert.Null(r.Diagnostics.TurnExitDriveProfile);
        Near(r.Change.Speed, c.ExitSpeedMetersPerSecond);
    }

    [Fact]
    public void RunWideTurnExitNeverDrivesAndOuterLaneDoesNotInventRetention()
    {
        var r = Get<CalibrationTurnResult>("turn_exit/band/run_wide");
        Assert.Equal(SegmentOutcome.RunWide, r.Change.Outcome);
        Assert.Equal(0f, r.DriveDistanceMeters);
        Assert.Null(r.Diagnostics.TurnExitDriveProfile);
        var outer = CalibrationScenarioSuite.ObserveCornerCapability(CalibrationScenarioCatalog.Baseline, 4f);
        Assert.Null(outer.FirstRunWideSpeedMetersPerSecond);
        Assert.Null(outer.ObservedRunWideOverspeedRetention);
    }

    [Fact]
    public void SkillCapabilityMonotonicSanityDoesNotFreezePerformanceNumbers()
    {
        var starts = Axis<CalibrationStartResult>("start/start_skill/");
        Assert.All(starts.Zip(starts.Skip(1)), pair => Assert.True(pair.Second.Profile.ReactionTimeSeconds < pair.First.Profile.ReactionTimeSeconds));
        var speeds = Axis<CalibrationStraightResult>("straight/speed/");
        Assert.All(speeds.Zip(speeds.Skip(1)), pair => Assert.True(pair.Second.ReferenceDriveForceNewtons > pair.First.ReferenceDriveForceNewtons));
        var controls = Axis<CalibrationTurnResult>("turn_middle/slide_control/");
        Assert.All(controls.Zip(controls.Skip(1)), pair =>
        {
            Assert.True(pair.Second.Capability.MaxSafeSpeedMetersPerSecond > pair.First.Capability.MaxSafeSpeedMetersPerSecond);
            Assert.True(pair.Second.Capability.CorrectionDecelerationMetersPerSecondSquared > pair.First.Capability.CorrectionDecelerationMetersPerSecondSquared);
            Assert.True(pair.Second.Capability.FirstRunWideSpeedMetersPerSecond > pair.First.Capability.FirstRunWideSpeedMetersPerSecond);
            Assert.True(pair.Second.Capability.ObservedRunWideOverspeedRetention > pair.First.Capability.ObservedRunWideOverspeedRetention);
        });
        var cornerSpeeds = Axis<CalibrationTurnResult>("turn_middle/speed/");
        Assert.All(cornerSpeeds.Zip(cornerSpeeds.Skip(1)), pair => Assert.True(pair.Second.Capability.MaxSafeSpeedMetersPerSecond > pair.First.Capability.MaxSafeSpeedMetersPerSecond));
    }

    [Fact]
    public void GearingChangesResponseAndEquilibriumWithoutAUniversalWinnerAssumption()
    {
        var low = Get<CalibrationStraightResult>("straight/gearing/distance_010/000");
        var high = Get<CalibrationStraightResult>("straight/gearing/distance_010/100");
        Assert.True(low.EntryNetAccelerationMetersPerSecondSquared > high.EntryNetAccelerationMetersPerSecondSquared);
        Assert.True(low.Profile.FullDriveEquilibriumSpeedMetersPerSecond < high.Profile.FullDriveEquilibriumSpeedMetersPerSecond);
        Assert.NotEqual(low.Profile.ExitSpeedMetersPerSecond, high.Profile.ExitSpeedMetersPerSecond);
    }

    [Theory]
    [InlineData("track_reading")]
    [InlineData("adaptability")]
    [InlineData("pair_riding")]
    public void UnusedSubsystemSkillsHaveNoFabricatedEffect(string axis)
    {
        Assert.Equal(Get<CalibrationStartResult>($"start/{axis}/000").Profile, Get<CalibrationStartResult>($"start/{axis}/100").Profile);
        Assert.Equal(Get<CalibrationStraightResult>($"straight/{axis}/000").Profile, Get<CalibrationStraightResult>($"straight/{axis}/100").Profile);
        var low = Get<CalibrationTurnResult>($"turn_middle/{axis}/000");
        var high = Get<CalibrationTurnResult>($"turn_middle/{axis}/100");
        Assert.Equal(low.Capability, high.Capability);
        Assert.Equal(low.Diagnostics, high.Diagnostics);
        Assert.Equal(low.Change, high.Change);
    }

    [Fact]
    public void GeometryComesFromCanonicalLaneModelAndFreeRunObservations()
    {
        var track = Track.CreateStandingStartExample();
        foreach (var line in Results.OfType<CalibrationLineResult>())
        {
            Assert.Equal(LaneModel.TurnArcRadiusMeters(line.Scenario.LateralPosition, track.Geometry), line.RadiusMeters);
            Assert.Equal(track.Segments.Sum(segment => LaneModel.SegmentLengthMeters(segment, line.Scenario.LateralPosition, track.Geometry)), line.ReferenceLapDistanceMeters);
            Assert.Equal(line.Trace.RiderSummaries.Single().TotalDistanceMeters, line.Rider.Performance.TotalDistanceMeters);
        }
    }

    [Fact]
    public void RunningSuiteAndRendererDoesNotModifyAnyCommittedDatasetFile()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "data", "calibration", "pge", "v1");
        var paths = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var before = paths.Select(path => SHA256.HashData(File.ReadAllBytes(path))).ToArray();
        _ = Report(CalibrationScenarioSuite.RunRequired());
        for (var index = 0; index < paths.Length; index++)
            Assert.Equal(before[index], SHA256.HashData(File.ReadAllBytes(paths[index])));
    }

    [Fact]
    public void InvalidOrderDuplicateIdsAndMalformedProvenanceAreRejected()
    {
        var scenario = Definitions.OfType<CalibrationHeatScenario>().First();
        Assert.Throws<ArgumentException>(() => CalibrationScenarioSuite.RunHeat(scenario, new[] { 1, 1, 3, 4 }));
        Assert.Throws<ArgumentException>(() => CalibrationScenarioSuite.Run(new[] { scenario, scenario }));
        var evaluation = Evaluation(Results);
        Assert.Throws<ArgumentException>(() => CalibrationScenarioReport.Render(Results, evaluation, "main"));
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void IllegalPartialCornerProgressIsRejected(float progress)
    {
        var scenario = Get<CalibrationTurnResult>("turn_middle/baseline").Scenario with { SegmentProgress = progress };
        Assert.Throws<ArgumentOutOfRangeException>(() => CalibrationScenarioSuite.RunScenario(scenario));
    }

    [Fact]
    public void NonfiniteFixtureAndMismatchedMetadataAreRejected()
    {
        var scenario = Get<CalibrationStartResult>("start/baseline").Scenario;
        Assert.Throws<ArgumentException>(() => CalibrationScenarioSuite.RunScenario(scenario with
        { Fixture = scenario.Fixture with { Surface = new TrackSurfaceState(float.NaN, 0f, 0.35f) } }));
        Assert.Throws<ArgumentException>(() => CalibrationScenarioSuite.RunScenario(scenario with
        { Metadata = scenario.Metadata with { Kind = CalibrationScenarioKind.Straight } }));
    }

    [Fact]
    public void HeatDefinitionDetachesAndFreezesInputRoster()
    {
        var baseline = Get<CalibrationHeatResult>("full_heat/baseline").Scenario;
        var input = baseline.Riders.ToArray();
        var copy = new CalibrationHeatScenario(baseline.Metadata, baseline.Fixture, input);
        input[0] = input[0] with { Skills = new RiderSkills(0, 0, 0, 0, 0, 0) };
        Assert.Equal(baseline.Riders, copy.Riders);
        Assert.Throws<NotSupportedException>(() => ((IList<CalibrationScenarioRider>)copy.Riders)[0] = input[0]);
    }

    private static T Get<T>(string id) where T : CalibrationScenarioResult => (T)Results.Single(result => result.Metadata.ScenarioId == id);
    private static T[] Axis<T>(string prefix) where T : CalibrationScenarioResult
        => Results.OfType<T>().Where(result => result.Metadata.ScenarioId.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Near(float expected, float actual)
        => Assert.InRange(MathF.Abs(expected - actual), 0f, MathF.Max(0.0001f, MathF.Abs(expected) * 0.00001f));
    private static string Report(IEnumerable<CalibrationScenarioResult> results)
    {
        var materialized = results.ToArray();
        return CalibrationScenarioReport.Render(materialized, Evaluation(materialized), Provenance);
    }
    private static CalibrationEvaluationReport Evaluation(IEnumerable<CalibrationScenarioResult> results)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "calibration", "pge", "v1", "pge_rider_heats.csv");
        var dataset = RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(path));
        return RealWorldCalibrationEvaluator.Evaluate(dataset, results.OfType<CalibrationHeatResult>()
            .Single(result => result.Metadata.ScenarioId == "full_heat/baseline").ToSimulationCalibrationResult());
    }
}
