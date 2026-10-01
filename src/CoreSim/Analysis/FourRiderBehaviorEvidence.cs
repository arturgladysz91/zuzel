using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim.Race;

namespace CoreSim.Analysis;

/// <summary>Persistence projection only: the suite and Markdown still consume every full run.</summary>
internal static class FourRiderBehaviorEvidence
{
    public static string Render(BehaviorAuditResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var evidence = new
        {
            FormatVersion = 2,
            StoragePolicy = "committed evidence contains aggregate results for the full seed set and detailed traces only for representative/control runs; the full deterministic suite is regenerated locally from source.",
            BaselineSha = FourRiderBehaviorSuite.BaselineSha,
            Seeds = Enumerable.Range(0, FourRiderBehaviorSuite.SeedCount).ToArray(),
            HeatId = FourRiderBehaviorSuite.HeatId,
            MainOptions = FourRiderBehaviorSuite.ProductionOptions(0),
            CalibrationAdjustments = new { StraightDriveEnvelope = (string?)null, CornerReducedDriveResistance = (string?)null,
                PreApexScrubLoss = (string?)null, ActiveCorrectionControlLoss = (string?)null },
            Definitions = new[] { "Boundary passes are pair inversions, not swept-overlap manoeuvres.",
                "PreviouslyTied marks ordering tie breaks, not established passes.",
                "InitialOrder uses canonical progress/time/id; production initially uses lane/id.",
                "Arrival gaps are different boundary crossing times, not simultaneous metres.",
                "All-seed scalar summaries and aggregates cover every main run and matched skill pair; individual events/classifications/trajectories are stored only for the listed detailed runs.",
                "Null apex means no actual apex observation; capacity is a separate field.",
                "Skill controls use IncidentFrequency=0; contacts still use addressed RNG.",
                "Fixed intent/solo probes are separate diagnostics, never main AI evidence.",
                "Row arrays follow their named Columns in order, without numeric rounding. Surface fields are flattened in StepColumns.",
                "Means use the same float LINQ Average as Markdown, not averages of rounded report cells.",
                "FirstCorner contact totals use lap 1 through the first logical corner end, including the preceding start straight." },
            Scenarios = result.Scenarios.Select(item => new { item.Id, item.Description, item.Track.Geometry,
                item.Track.Segments, item.Riders, item.LaneSurfaces }).ToArray(),
            ScenarioAggregates = result.Scenarios.Select(scenario => ScenarioAggregate(scenario,
                result.Runs.Where(run => run.ScenarioId == scenario.Id).ToArray())).ToArray(),
            RunSummaryColumns = new[] { "ScenarioId", "Seed", "InitialOrder", "FinalOrder", "EstablishedInversions",
                "TiedInversions", "ProductionOvertakes", "Contacts", "LostRhythm", "Crashes", "FinishedRiders",
                "FirstBoundaryEventMismatchCount", "TrafficChangedChoices", "WinnerTimeSeconds", "R2PassesR1",
                "BothR1R2Finish", "R2AheadAtFinish", "FirstCornerContacts", "FirstCornerLostRhythm", "FirstCornerContactCrashes" },
            RunSummaries = Rows(result.Runs.Select(run => RunSummary(run,
                result.Scenarios.Single(scenario => scenario.Id == run.ScenarioId).Track.CornerTopology.Corners[0].EndSegmentIndex))),
            SkillAggregates = result.SkillPairs.GroupBy(item => item.Skill).Select(SkillAggregate).ToArray(),
            SkillPairSummaryColumns = new[] { "Skill", "Seed", "LowR2Finished", "HighR2Finished", "LowR2TimeSeconds",
                "HighR2TimeSeconds", "LowR2Contacts", "HighR2Contacts", "LowAllContacts", "HighAllContacts",
                "R2TargetDifferences", "LowFirstStepLateralMovement", "HighFirstStepLateralMovement" },
            SkillPairSummaries = Rows(result.SkillPairs.Select(pair => new object?[] { pair.Skill, pair.Seed,
                Finish(pair.Low, 2).Finished, Finish(pair.High, 2).Finished, Finish(pair.Low, 2).TimeSeconds,
                Finish(pair.High, 2).TimeSeconds, RiderContacts(pair.Low), RiderContacts(pair.High),
                pair.Low.Contacts.Count, pair.High.Contacts.Count, TargetDifferences(pair), Movement(pair.Low), Movement(pair.High) })),
            SkillInputs = result.SkillPairs.Where(item => item.Seed == 0).Select(item => new
            {
                item.Skill, item.LowInput.Track.Geometry, item.LowInput.Track.Segments, item.LowInput.LaneSurfaces,
                LowRiders = item.LowInput.Riders, HighRiders = item.HighInput.Riders,
                Intent = item.Skill is "SlideControl" or "PairRiding" ? "fixed lane 2" : item.Skill == "Adaptability" ? "fixed lane 0" : "AdaptiveDecisionModel",
            }).ToArray(),
            result.RouteProbes, result.WearHeats, result.FractionalProgressProbe,
            DiagnosticControls = result.DiagnosticControls.Select(item => new { item.Id, item.Intent,
                item.Input.Track.Geometry, item.Input.Track.Segments, item.Input.Riders, item.Input.LaneSurfaces }).ToArray(),
            ChoiceColumns = new[] { "Step", "RiderId", "TargetLane", "NoTrafficTargetLane" },
            StepColumns = new[] { "Step", "Lap", "SegmentIndex", "SegmentType", "RiderId", "StartProgress", "EndProgress",
                "StartTimeSeconds", "EndTimeSeconds", "BoundaryArrivalGapSeconds", "BeforeLane", "TargetLane", "PlannedLane",
                "ResolvedLane", "EntryLateralPosition", "ExitLateralPosition", "EntrySpeedMetersPerSecond",
                "SegmentConstraintSpeedMetersPerSecond", "TraversalExitSpeedMetersPerSecond", "FinalSpeedMetersPerSecond",
                "PathDistanceMeters", "EntrySurface.Grip", "EntrySurface.Ruts", "EntrySurface.Moisture", "EntrySurface.EffectiveGrip",
                "Status", "Outcome", "CornerStartProgress", "CornerEndProgress", "MinimumCornerSpeedMetersPerSecond",
                "ActualSampledApexSpeedMetersPerSecond", "CornerEnvelopeApexSpeedMetersPerSecond", "StartReactionSeconds" },
            DetailedRuns = result.Runs.Where(run => run.Seed == 0).Select(run => Detail("Main", run.ScenarioId, run))
                .Concat(result.DiagnosticControls.Select(item => Detail("Diagnostic", item.Id, item.Run)))
                .Append(Detail("NonStochastic", result.NonStochasticControl.ScenarioId, result.NonStochasticControl)).ToArray(),
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new CompactRowsConverter());
        return JsonSerializer.Serialize(evidence, options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static object ScenarioAggregate(BehaviorScenario scenario, BehaviorRun[] runs)
    {
        var first = runs.Single(run => run.Seed == 0);
        var firstCorner = runs.SelectMany(run => FirstCornerContacts(run, scenario.Track.CornerTopology.Corners[0].EndSegmentIndex)).ToArray();
        return new
        {
            ScenarioId = scenario.Id, SeedCount = runs.Length,
            SeedZeroInitialOrder = first.InitialOrder, SeedZeroFinalOrder = first.FinalOrder,
            InitialOrderHistogram = OrderHistogram(runs.Select(run => run.InitialOrder)),
            FinalOrderHistogram = OrderHistogram(runs.Select(run => run.FinalOrder)),
            EstablishedInversions = runs.Sum(run => run.BoundaryPasses.Count(pass => !pass.PreviouslyTied)),
            TiedInversions = runs.Sum(run => run.BoundaryPasses.Count(pass => pass.PreviouslyTied)),
            PairInversionHistogram = runs.SelectMany(run => run.BoundaryPasses)
                .GroupBy(pass => (pass.RiderId, pass.PassedRiderId, pass.PreviouslyTied))
                .OrderBy(group => group.Key.RiderId).ThenBy(group => group.Key.PassedRiderId).ThenBy(group => group.Key.PreviouslyTied)
                .Select(group => new { group.Key.RiderId, group.Key.PassedRiderId, group.Key.PreviouslyTied, Count = group.Count() }).ToArray(),
            ProductionOvertakes = runs.Sum(run => run.ProductionOvertakes.Count),
            Contacts = runs.Sum(run => run.Contacts.Count), LostRhythm = runs.Sum(run => run.LostRhythm),
            ContactCrashes = runs.Sum(run => run.Contacts.Count(contact => contact.Type == SimulationEventType.ContactCrash)),
            Crashes = runs.Sum(run => run.Crashes), FinishedRiders = runs.Sum(run => run.Classification.Count(rider => rider.Finished)),
            RiderFinishCounts = runs.SelectMany(run => run.Classification).GroupBy(rider => rider.RiderId).OrderBy(group => group.Key)
                .Select(group => new { RiderId = group.Key, Finished = group.Count(rider => rider.Finished), Crashed = group.Count(rider => rider.Crashed) }).ToArray(),
            R2PassesR1Heats = runs.Count(R2PassesR1), BothR1R2FinishHeats = runs.Count(BothFinish),
            R2AheadAtFinishHeats = runs.Count(R2Ahead),
            FirstBoundaryEventMismatchCount = runs.Sum(run => run.FirstBoundaryEventMismatchCount),
            TrafficChangedChoices = runs.Sum(TrafficChanges),
            MeanWinnerTimeSeconds = runs.Average(WinnerTime), MinWinnerTimeSeconds = runs.Min(WinnerTime), MaxWinnerTimeSeconds = runs.Max(WinnerTime),
            FirstCornerContacts = firstCorner.Length,
            FirstCornerLostRhythm = firstCorner.Count(contact => contact.Type == SimulationEventType.ContactLostRhythm),
            FirstCornerContactCrashes = firstCorner.Count(contact => contact.Type == SimulationEventType.ContactCrash),
        };
    }

    private static object[] OrderHistogram(IEnumerable<IReadOnlyList<int>> orders) => orders
        .GroupBy(order => string.Join("/", order.Select(id => id.ToString(CultureInfo.InvariantCulture))), StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => (object)new { Order = group.First(), Count = group.Count() }).ToArray();

    private static object?[] RunSummary(BehaviorRun run, int firstCornerEnd)
    {
        var contacts = FirstCornerContacts(run, firstCornerEnd).ToArray();
        return new object?[] { run.ScenarioId, run.Seed, run.InitialOrder, run.FinalOrder,
            run.BoundaryPasses.Count(pass => !pass.PreviouslyTied), run.BoundaryPasses.Count(pass => pass.PreviouslyTied),
            run.ProductionOvertakes.Count, run.Contacts.Count, run.LostRhythm, run.Crashes,
            run.Classification.Count(rider => rider.Finished), run.FirstBoundaryEventMismatchCount, TrafficChanges(run),
            WinnerTime(run), R2PassesR1(run), BothFinish(run), R2Ahead(run), contacts.Length,
            contacts.Count(contact => contact.Type == SimulationEventType.ContactLostRhythm),
            contacts.Count(contact => contact.Type == SimulationEventType.ContactCrash) };
    }

    private static object SkillAggregate(IGrouping<string, BehaviorSkillPair> group)
    {
        var pairs = group.ToArray();
        var finished = pairs.Where(pair => Finish(pair.Low, 2).Finished && Finish(pair.High, 2).Finished).ToArray();
        return new
        {
            Skill = group.Key, SeedCount = pairs.Length, PairedFinishedHeats = finished.Length,
            MeanR2HighMinusLowTimeSeconds = finished.Length == 0 ? (float?)null
                : finished.Average(pair => Finish(pair.High, 2).TimeSeconds - Finish(pair.Low, 2).TimeSeconds),
            LowR2Contacts = pairs.Sum(pair => RiderContacts(pair.Low)), HighR2Contacts = pairs.Sum(pair => RiderContacts(pair.High)),
            LowAllContacts = pairs.Sum(pair => pair.Low.Contacts.Count), HighAllContacts = pairs.Sum(pair => pair.High.Contacts.Count),
            R2TargetDifferences = pairs.Sum(TargetDifferences),
            MeanLowFirstStepLateralMovement = pairs.Average(pair => Movement(pair.Low)),
            MeanHighFirstStepLateralMovement = pairs.Average(pair => Movement(pair.High)),
        };
    }

    private static object Detail(string kind, string id, BehaviorRun run) => new
    {
        Kind = kind, Id = id, run.ScenarioId, run.Seed, run.InitialOrder, run.FinalOrder, run.Classification,
        run.BoundaryPasses, run.ProductionOvertakes, run.Contacts, run.FirstBoundaryEventMismatchCount,
        Choices = Rows(run.Choices.Select(choice => new object?[] { choice.Step, choice.RiderId, choice.TargetLane, choice.NoTrafficTargetLane })),
        Steps = Rows(run.Steps.Select(step => new object?[] { step.Step, step.Lap, step.SegmentIndex, step.SegmentType,
            step.RiderId, step.StartProgress, step.EndProgress, step.StartTimeSeconds, step.EndTimeSeconds,
            step.BoundaryArrivalGapSeconds, step.BeforeLane, step.TargetLane, step.PlannedLane, step.ResolvedLane,
            step.EntryLateralPosition, step.ExitLateralPosition, step.EntrySpeedMetersPerSecond,
            step.SegmentConstraintSpeedMetersPerSecond, step.TraversalExitSpeedMetersPerSecond,
            step.FinalSpeedMetersPerSecond, step.PathDistanceMeters, step.EntrySurface.Grip, step.EntrySurface.Ruts,
            step.EntrySurface.Moisture, step.EntrySurface.EffectiveGrip, step.Status, step.Outcome, step.CornerStartProgress,
            step.CornerEndProgress, step.MinimumCornerSpeedMetersPerSecond, step.ActualSampledApexSpeedMetersPerSecond,
            step.CornerEnvelopeApexSpeedMetersPerSecond, step.StartReactionSeconds })),
    };

    private static RiderHeatResult Finish(BehaviorRun run, int id) => run.Classification.Single(rider => rider.RiderId == id);
    private static bool BothFinish(BehaviorRun run) => Finish(run, 1).Finished && Finish(run, 2).Finished;
    private static bool R2Ahead(BehaviorRun run) => BothFinish(run) && Finish(run, 2).Position < Finish(run, 1).Position;
    private static bool R2PassesR1(BehaviorRun run) => run.BoundaryPasses.Any(pass => pass.RiderId == 2 && pass.PassedRiderId == 1 && !pass.PreviouslyTied);
    private static int TrafficChanges(BehaviorRun run) => run.Choices.Count(choice => choice.TargetLane != choice.NoTrafficTargetLane);
    private static float WinnerTime(BehaviorRun run) => run.Classification.First(rider => rider.Position == 1).TimeSeconds;
    private static int RiderContacts(BehaviorRun run) => run.Contacts.Count(contact => contact.RiderId == 2);
    private static float Movement(BehaviorRun run) { var step = run.Steps.First(item => item.RiderId == 2); return MathF.Abs(step.ExitLateralPosition - step.EntryLateralPosition); }
    private static int TargetDifferences(BehaviorSkillPair pair) => pair.Low.Choices.Join(pair.High.Choices,
        choice => (choice.Step, choice.RiderId), choice => (choice.Step, choice.RiderId),
        (low, high) => low.RiderId == 2 && low.TargetLane != high.TargetLane ? 1 : 0).Sum();
    private static IEnumerable<BehaviorContact> FirstCornerContacts(BehaviorRun run, int end) => run.Contacts.Where(contact => contact.Lap == 1 && contact.SegmentIndex <= end);

    private static CompactRows Rows(IEnumerable<object?[]> rows) => new(rows.ToArray());
    private sealed record CompactRows(IReadOnlyList<object?[]> Values);

    // Retain a readable indented document, but avoid one line per scalar in trace/summary rows.
    // System.Text.Json still provides invariant, round-trip numeric serialization and escaping.
    private sealed class CompactRowsConverter : JsonConverter<CompactRows>
    {
        public override CompactRows Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException("Evidence rows are a write-only persistence projection.");

        public override void Write(Utf8JsonWriter writer, CompactRows value, JsonSerializerOptions options)
        {
            var rowOptions = new JsonSerializerOptions(options) { WriteIndented = false };
            writer.WriteStartArray();
            foreach (var row in value.Values)
                writer.WriteRawValue(JsonSerializer.Serialize(row, rowOptions));
            writer.WriteEndArray();
        }
    }
}
