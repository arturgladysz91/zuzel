using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

public sealed class FourRiderBehaviorEvidenceTests
{
    private static readonly Lazy<BehaviorAuditResult> Audit = new(() => FourRiderAuditFixture.Result);
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Compact_evidence_is_under_one_megabyte_and_limits_detailed_trace_coverage()
    {
        var result = Audit.Value;
        var text = FourRiderBehaviorReport.RenderEvidence(result);
        Assert.InRange(Encoding.UTF8.GetByteCount(text), 1, 999_999);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("FormatVersion").GetInt32());
        Assert.Contains("full deterministic suite is regenerated locally from source", root.GetProperty("StoragePolicy").GetString());
        Assert.False(root.TryGetProperty("Runs", out _));
        Assert.False(root.TryGetProperty("SkillPairs", out _));
        Assert.Equal(352, root.GetProperty("RunSummaries").GetArrayLength());
        Assert.Equal(192, root.GetProperty("SkillPairSummaries").GetArrayLength());
        var details = root.GetProperty("DetailedRuns").EnumerateArray().ToArray();
        Assert.Equal(16, details.Length);
        Assert.All(details, detail => Assert.Equal(0, detail.GetProperty("Seed").GetInt32()));
        Assert.Equal(result.Scenarios.Select(item => item.Id), details.Where(item => item.GetProperty("Kind").GetString() == "Main")
            .Select(item => item.GetProperty("Id").GetString()));
        Assert.Equal(result.DiagnosticControls.Select(item => item.Id), details.Where(item => item.GetProperty("Kind").GetString() == "Diagnostic")
            .Select(item => item.GetProperty("Id").GetString()));
        Assert.Single(details.Where(item => item.GetProperty("Kind").GetString() == "NonStochastic"));
        AssertJson(Enumerable.Range(0, 32).ToArray(), root.GetProperty("Seeds"));
        AssertJson(FourRiderBehaviorSuite.ProductionOptions(0), root.GetProperty("MainOptions"));
        Assert.All(root.GetProperty("CalibrationAdjustments").EnumerateObject(), item => Assert.Equal(JsonValueKind.Null, item.Value.ValueKind));
        AssertJson(result.Scenarios.Select(item => new { item.Id, item.Description, item.Track.Geometry,
            item.Track.Segments, item.Riders, item.LaneSurfaces }).ToArray(), root.GetProperty("Scenarios"));
        AssertJson(result.RouteProbes, root.GetProperty("RouteProbes"));
        AssertJson(result.WearHeats, root.GetProperty("WearHeats"));
        Assert.Equal(result.FractionalProgressProbe, root.GetProperty("FractionalProgressProbe").GetString());
    }

    [Fact]
    public void Main_summaries_and_aggregates_preserve_every_report_metric_for_all_seeds()
    {
        var result = Audit.Value;
        using var document = JsonDocument.Parse(FourRiderBehaviorReport.RenderEvidence(result));
        var root = document.RootElement;
        var rows = root.GetProperty("RunSummaries").EnumerateArray().ToArray();
        Assert.Equal(result.Runs.Count, rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            var run = result.Runs[index];
            var end = result.Scenarios.Single(item => item.Id == run.ScenarioId).Track.CornerTopology.Corners[0].EndSegmentIndex;
            var firstCorner = run.Contacts.Where(item => item.Lap == 1 && item.SegmentIndex <= end).ToArray();
            AssertSummary(root.GetProperty("RunSummaryColumns"), rows[index], new
            {
                run.ScenarioId, run.Seed, run.InitialOrder, run.FinalOrder,
                EstablishedInversions = run.BoundaryPasses.Count(item => !item.PreviouslyTied),
                TiedInversions = run.BoundaryPasses.Count(item => item.PreviouslyTied), ProductionOvertakes = run.ProductionOvertakes.Count,
                Contacts = run.Contacts.Count, run.LostRhythm, run.Crashes, FinishedRiders = run.Classification.Count(item => item.Finished),
                run.FirstBoundaryEventMismatchCount, TrafficChangedChoices = run.Choices.Count(item => item.TargetLane != item.NoTrafficTargetLane),
                WinnerTimeSeconds = run.Classification.Single(item => item.Position == 1).TimeSeconds,
                R2PassesR1 = run.BoundaryPasses.Any(item => item.RiderId == 2 && item.PassedRiderId == 1 && !item.PreviouslyTied),
                BothR1R2Finish = BothFinish(run), R2AheadAtFinish = BothFinish(run) && Finish(run, 2).Position < Finish(run, 1).Position,
                FirstCornerContacts = firstCorner.Length,
                FirstCornerLostRhythm = firstCorner.Count(item => item.Type == SimulationEventType.ContactLostRhythm),
                FirstCornerContactCrashes = firstCorner.Count(item => item.Type == SimulationEventType.ContactCrash),
            });
        }
        foreach (var aggregate in root.GetProperty("ScenarioAggregates").EnumerateArray())
        {
            var id = aggregate.GetProperty("ScenarioId").GetString();
            var runs = result.Runs.Where(item => item.ScenarioId == id).ToArray();
            var summaries = rows.Where(row => row[0].GetString() == id).ToArray();
            Assert.Equal(Enumerable.Range(0, 32), summaries.Select(row => row[1].GetInt32()));
            Assert.Equal(32, aggregate.GetProperty("SeedCount").GetInt32());
            AssertJson(runs[0].InitialOrder, aggregate.GetProperty("SeedZeroInitialOrder"));
            AssertJson(runs[0].FinalOrder, aggregate.GetProperty("SeedZeroFinalOrder"));
            AssertHistogram(runs.Select(item => item.InitialOrder), aggregate.GetProperty("InitialOrderHistogram"));
            AssertHistogram(runs.Select(item => item.FinalOrder), aggregate.GetProperty("FinalOrderHistogram"));
            foreach (var (name, column) in new[] { ("EstablishedInversions", 4), ("TiedInversions", 5), ("ProductionOvertakes", 6),
                ("Contacts", 7), ("LostRhythm", 8), ("Crashes", 9), ("FinishedRiders", 10), ("FirstBoundaryEventMismatchCount", 11),
                ("TrafficChangedChoices", 12), ("FirstCornerContacts", 17), ("FirstCornerLostRhythm", 18), ("FirstCornerContactCrashes", 19) })
                Assert.Equal(summaries.Sum(row => row[column].GetInt32()), aggregate.GetProperty(name).GetInt32());
            Assert.Equal(summaries.Count(row => row[14].GetBoolean()), aggregate.GetProperty("R2PassesR1Heats").GetInt32());
            Assert.Equal(summaries.Count(row => row[15].GetBoolean()), aggregate.GetProperty("BothR1R2FinishHeats").GetInt32());
            Assert.Equal(summaries.Count(row => row[16].GetBoolean()), aggregate.GetProperty("R2AheadAtFinishHeats").GetInt32());
            Assert.Equal(summaries.Average(row => row[13].GetSingle()), aggregate.GetProperty("MeanWinnerTimeSeconds").GetSingle());
            Assert.Equal(summaries.Min(row => row[13].GetSingle()), aggregate.GetProperty("MinWinnerTimeSeconds").GetSingle());
            Assert.Equal(summaries.Max(row => row[13].GetSingle()), aggregate.GetProperty("MaxWinnerTimeSeconds").GetSingle());
            Assert.Equal(runs.Sum(run => run.Contacts.Count(item => item.Type == SimulationEventType.ContactCrash)), aggregate.GetProperty("ContactCrashes").GetInt32());
            AssertJson(runs.SelectMany(run => run.Classification).GroupBy(item => item.RiderId).OrderBy(group => group.Key)
                .Select(group => new { RiderId = group.Key, Finished = group.Count(item => item.Finished), Crashed = group.Count(item => item.Crashed) }).ToArray(),
                aggregate.GetProperty("RiderFinishCounts"));
            AssertJson(runs.SelectMany(run => run.BoundaryPasses).GroupBy(item => (item.RiderId, item.PassedRiderId, item.PreviouslyTied))
                .OrderBy(group => group.Key.RiderId).ThenBy(group => group.Key.PassedRiderId).ThenBy(group => group.Key.PreviouslyTied)
                .Select(group => new { group.Key.RiderId, group.Key.PassedRiderId, group.Key.PreviouslyTied, Count = group.Count() }).ToArray(),
                aggregate.GetProperty("PairInversionHistogram"));
        }
    }

    [Fact]
    public void Skill_pair_summaries_preserve_matched_seed_deltas_contacts_choices_and_movement()
    {
        var result = Audit.Value;
        using var document = JsonDocument.Parse(FourRiderBehaviorReport.RenderEvidence(result));
        var root = document.RootElement;
        var rows = root.GetProperty("SkillPairSummaries").EnumerateArray().ToArray();
        Assert.Equal(result.SkillPairs.Count, rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            var pair = result.SkillPairs[index];
            AssertSummary(root.GetProperty("SkillPairSummaryColumns"), rows[index], new
            {
                pair.Skill, pair.Seed, LowR2Finished = Finish(pair.Low, 2).Finished, HighR2Finished = Finish(pair.High, 2).Finished,
                LowR2TimeSeconds = Finish(pair.Low, 2).TimeSeconds, HighR2TimeSeconds = Finish(pair.High, 2).TimeSeconds,
                LowR2Contacts = pair.Low.Contacts.Count(item => item.RiderId == 2), HighR2Contacts = pair.High.Contacts.Count(item => item.RiderId == 2),
                LowAllContacts = pair.Low.Contacts.Count, HighAllContacts = pair.High.Contacts.Count,
                R2TargetDifferences = pair.Low.Choices.Join(pair.High.Choices, item => (item.Step, item.RiderId), item => (item.Step, item.RiderId),
                    (low, high) => low.RiderId == 2 && low.TargetLane != high.TargetLane ? 1 : 0).Sum(),
                LowFirstStepLateralMovement = Movement(pair.Low), HighFirstStepLateralMovement = Movement(pair.High),
            });
        }
        foreach (var aggregate in root.GetProperty("SkillAggregates").EnumerateArray())
        {
            var summaries = rows.Where(row => row[0].GetString() == aggregate.GetProperty("Skill").GetString()).ToArray();
            Assert.Equal(Enumerable.Range(0, 32), summaries.Select(row => row[1].GetInt32()));
            Assert.Equal(32, aggregate.GetProperty("SeedCount").GetInt32());
            var finished = summaries.Where(row => row[2].GetBoolean() && row[3].GetBoolean()).ToArray();
            Assert.Equal(finished.Length, aggregate.GetProperty("PairedFinishedHeats").GetInt32());
            if (finished.Length == 0) Assert.Equal(JsonValueKind.Null, aggregate.GetProperty("MeanR2HighMinusLowTimeSeconds").ValueKind);
            else Assert.Equal(finished.Average(row => row[5].GetSingle() - row[4].GetSingle()), aggregate.GetProperty("MeanR2HighMinusLowTimeSeconds").GetSingle());
            foreach (var (name, column) in new[] { ("LowR2Contacts", 6), ("HighR2Contacts", 7), ("LowAllContacts", 8), ("HighAllContacts", 9), ("R2TargetDifferences", 10) })
                Assert.Equal(summaries.Sum(row => row[column].GetInt32()), aggregate.GetProperty(name).GetInt32());
            Assert.Equal(summaries.Average(row => row[11].GetSingle()), aggregate.GetProperty("MeanLowFirstStepLateralMovement").GetSingle());
            Assert.Equal(summaries.Average(row => row[12].GetSingle()), aggregate.GetProperty("MeanHighFirstStepLateralMovement").GetSingle());
        }
    }

    [Fact]
    public void Representative_rows_preserve_every_typed_step_choice_classification_and_event_value()
    {
        var result = Audit.Value;
        using var document = JsonDocument.Parse(FourRiderBehaviorReport.RenderEvidence(result));
        var root = document.RootElement;
        foreach (var detail in root.GetProperty("DetailedRuns").EnumerateArray())
        {
            var id = detail.GetProperty("Id").GetString();
            var run = detail.GetProperty("Kind").GetString() switch
            {
                "Main" => result.Runs.Single(item => item.ScenarioId == id && item.Seed == 0),
                "Diagnostic" => result.DiagnosticControls.Single(item => item.Id == id).Run,
                "NonStochastic" => result.NonStochasticControl,
                _ => throw new InvalidOperationException("Unknown trace kind."),
            };
            foreach (var name in new[] { "ScenarioId", "Seed", "InitialOrder", "FinalOrder", "Classification", "BoundaryPasses", "ProductionOvertakes", "Contacts", "FirstBoundaryEventMismatchCount" })
                AssertJson(typeof(BehaviorRun).GetProperty(name)!.GetValue(run), detail.GetProperty(name));
            AssertTypedRows(root.GetProperty("ChoiceColumns"), detail.GetProperty("Choices"), run.Choices);
            AssertTypedRows(root.GetProperty("StepColumns"), detail.GetProperty("Steps"), run.Steps);
        }
    }

    private static CoreSim.Race.RiderHeatResult Finish(BehaviorRun run, int id) => run.Classification.Single(item => item.RiderId == id);
    private static bool BothFinish(BehaviorRun run) => Finish(run, 1).Finished && Finish(run, 2).Finished;
    private static float Movement(BehaviorRun run) { var step = run.Steps.First(item => item.RiderId == 2); return MathF.Abs(step.ExitLateralPosition - step.EntryLateralPosition); }

    private static void AssertHistogram(IEnumerable<IReadOnlyList<int>> orders, JsonElement histogram)
    {
        var inputs = orders.ToArray();
        Assert.Equal(inputs.Length, histogram.EnumerateArray().Sum(item => item.GetProperty("Count").GetInt32()));
        foreach (var item in histogram.EnumerateArray())
        {
            var order = item.GetProperty("Order").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            Assert.Equal(inputs.Count(input => input.SequenceEqual(order)), item.GetProperty("Count").GetInt32());
        }
        Assert.Equal(inputs.Select(input => string.Join("/", input)).Distinct().Count(), histogram.GetArrayLength());
    }

    private static void AssertTypedRows<T>(JsonElement columns, JsonElement rows, IReadOnlyList<T> values)
    {
        var instanceProperties = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
        var flattenedProperties = typeof(T).GetProperties(instanceProperties).SelectMany(property => property.Name == "EntrySurface"
            ? typeof(TrackSurfaceState).GetProperties(instanceProperties).Select(surface => $"EntrySurface.{surface.Name}")
            : new[] { property.Name }).ToArray();
        Assert.Equal(flattenedProperties, columns.EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(values.Count, rows.GetArrayLength());
        for (var index = 0; index < values.Count; index++) AssertSummary(columns, rows[index], values[index]);
    }

    private static void AssertSummary<T>(JsonElement columns, JsonElement row, T value)
    {
        var typed = JsonSerializer.SerializeToElement(value, JsonOptions);
        Assert.Equal(columns.GetArrayLength(), row.GetArrayLength());
        var index = 0;
        foreach (var column in columns.EnumerateArray())
        {
            var expected = typed;
            foreach (var part in column.GetString()!.Split('.')) expected = expected.GetProperty(part);
            Assert.Equal(expected.GetRawText(), row[index++].GetRawText());
        }
    }

    private static void AssertJson(object? expected, JsonElement actual) => Assert.True(JsonNode.DeepEquals(
        JsonSerializer.SerializeToNode(expected, JsonOptions), JsonNode.Parse(actual.GetRawText())), actual.GetRawText());
}
