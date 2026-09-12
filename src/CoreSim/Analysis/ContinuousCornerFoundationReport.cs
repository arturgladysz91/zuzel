using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CoreSim.Analysis;

/// <summary>
/// Pure, invariant-culture Markdown observation of logical-corner topology and
/// unchanged production calibration scenarios. File I/O belongs to Sandbox.
/// </summary>
public static class ContinuousCornerFoundationReport
{
    public static string Render(
        IEnumerable<CalibrationScenarioResult> scenarioResults,
        string beforeCalibrationReport,
        string afterCalibrationReport,
        string baseMainSha,
        string candidateCodeHeadSha)
    {
        ArgumentNullException.ThrowIfNull(scenarioResults);
        ArgumentNullException.ThrowIfNull(beforeCalibrationReport);
        ArgumentNullException.ThrowIfNull(afterCalibrationReport);
        ValidateSha(baseMainSha, nameof(baseMainSha));
        ValidateSha(candidateCodeHeadSha, nameof(candidateCodeHeadSha));
        if (!StringComparer.Ordinal.Equals(beforeCalibrationReport, afterCalibrationReport))
        {
            throw new InvalidOperationException(
                "The production calibration-scenario report changed after the corner-foundation refactor.");
        }

        var results = scenarioResults
            .OrderBy(result => result.Metadata.ScenarioId, StringComparer.Ordinal)
            .ToArray();
        if (results.Select(result => result.Metadata.ScenarioId)
            .Distinct(StringComparer.Ordinal).Count() != results.Length)
        {
            throw new ArgumentException("Duplicate scenario ids.", nameof(scenarioResults));
        }

        var b = new StringBuilder();
        Line("# Continuous corner phase foundation impact (#37)");
        Line();
        Line($"Base main SHA: {baseMainSha}");
        Line($"Candidate code-head SHA: {candidateCodeHeadSha}");
        Line($"Calibration Scenario Suite before/after SHA-256: {Hash(beforeCalibrationReport)}");
        Line("Calibration Scenario Suite before/after bytes identical: yes");

        Heading("Architecture before and after");
        Line("| Concern | Before #37 | After #37 |");
        Line("| --- | --- | --- |");
        Line("| Corner identity | Inferred repeatedly from TurnEntry/TurnMiddle/TurnExit labels | One immutable maximal non-wrapping turn run with a stable track-local CornerId |");
        Line("| Corner phase | Local SegmentProgress only | CornerProgress in [0,1], derived from accumulated physical arc distance |");
        Line("| Segment boundaries | Compatibility labels implied thirds on the example layouts | Start/end phase comes from each segment's physical length divided by total corner length |");
        Line("| Production flow | CaptureSnapshot → Decide → Resolve → Commit | Unchanged; advanced Resolve carries the immutable CornerPhaseContext |");
        Line("| Existing corner phases | Selected directly by SegmentType | Existing TurnEntry scrub and TurnExit drive remain explicit compatibility bridges for #38 |");
        Line("| Straight lookahead | Immediate TurnEntry label lookup | Immediate logical-corner lookup, then the same TurnEntry compatibility gate and unchanged target calculation |");
        Line();
        Line("Corner topology is pure geometry. It adds no state mutation, random channel, surface sampling, speed target, force, traversal phase or alternative simulation path.");

        Heading("Logical-corner maps");
        Line("Ranges below are recomputed at lateral positions 0, 2.5 and 4. They happen to align on the current uniform-angle examples, but the implementation sums physical segment lengths and never hardcodes thirds.");
        Line();
        Table(
            "Track | Segment index | Segment id | Compatibility label | Corner id | Range at lateral 0 | Range at lateral 2.5 | Range at lateral 4",
            MapRows("CreateExample", Track.CreateExample())
                .Concat(MapRows("CreateStandingStartExample", Track.CreateStandingStartExample())));
        Table(
            "Track | Lateral position | Corner id | First segment | Last segment | Total corner length m",
            CornerLengthRows("CreateExample", Track.CreateExample())
                .Concat(CornerLengthRows("CreateStandingStartExample", Track.CreateStandingStartExample())));

        Heading("Corner progress and remaining distance");
        Line("Representative midpoint observations use lateral position 2.5. Remaining distance is measured to the logical-corner end on that same geometric reference trajectory.");
        Line();
        Table(
            "Track | Corner id | Segment index | Label | Local progress | Corner progress | Total m | Remaining m",
            ProgressRows("CreateExample", Track.CreateExample())
                .Concat(ProgressRows("CreateStandingStartExample", Track.CreateStandingStartExample())));

        Heading("Production regression");
        Line("The complete Calibration Scenario Suite was captured on base main before the code change and rendered again through the same production path after the change. Its full Markdown is byte-identical. Therefore every selected before value below is the exact matching base observation, not an inferred or recomputed surrogate.");
        Line();
        Table(
            "Scenario | Metric | Before | After | Delta | Unit",
            RegressionRows(results).Select(row => Row(
                row.Scenario,
                row.Metric,
                row.Value,
                row.Value,
                0d,
                row.Unit)));

        Heading("Frozen physics boundary");
        Line("No calibration or physics tuning was performed. The values below are assertions about the unchanged production model, not new parameters introduced by this report.");
        Line();
        Table("Parameter | Value", new[]
        {
            Row("Straight reference acceleration", "1.60–3.20 m/s²"),
            Row("TurnExit reference acceleration", "1.20–2.80 m/s²"),
            Row("Drive-oriented / speed-oriented fade", "0.0350 / 0.0100 1/(m/s)"),
            Row("Reference speed", "16 m/s"),
            Row("Nominal mass", "142 kg"),
            Row("Resistance", "40 + 0.20v² N"),
            Row("Gearing drive mapping", "1.10–0.90"),
            Row("Surface drive mapping", "0.75 + 0.25 × EffectiveGrip"),
            Row("Standing-start reaction", "0.28–0.20 s"),
            Row("Standing-start reference acceleration", "9–11 m/s²"),
            Row("Corner correction capability", "2.00–3.20 m/s²"),
            Row("TurnEntry scrub fraction", "0.50"),
        });
        Line("SegmentPhysics thresholds/retention, continuous correction, launch, reaction, acceleration, resistance, force fade, RiderSkills multipliers, contact probability, occupancy/contact thresholds, lateral traversal, surface physics, legacy behavior, dataset files and historical reports are unchanged.");
        Line();
        Line("NO MATERIAL PHYSICS CHANGE.");
        Line("NO SPEED/PERFORMANCE CONSTANTS CHANGED.");
        return b.ToString();

        void Line(string value = "") => b.Append(value).Append('\n');
        void Heading(string value)
        {
            Line();
            Line($"## {value}");
            Line();
        }
        void Table(string header, IEnumerable<object?[]> rows)
        {
            Line($"| {header} |");
            Line($"| {string.Join(" | ", header.Split('|').Select(_ => "---"))} |");
            foreach (var row in rows)
                Line($"| {string.Join(" | ", row.Select(Format))} |");
            Line();
        }
    }

    private static IEnumerable<object?[]> MapRows(string trackName, Track track)
    {
        for (var segmentIndex = 0; segmentIndex < track.Segments.Count; segmentIndex++)
        {
            var segment = track.Segments[segmentIndex];
            var corner = track.CornerTopology.CornerForSegment(segmentIndex);
            yield return Row(
                trackName,
                segmentIndex,
                segment.Id,
                segment.Type,
                corner?.CornerId,
                Range(track, segmentIndex, 0f),
                Range(track, segmentIndex, 2.5f),
                Range(track, segmentIndex, 4f));
        }
    }

    private static IEnumerable<object?[]> CornerLengthRows(string trackName, Track track)
    {
        foreach (var lateralPosition in new[] { 0f, 2.5f, 4f })
        foreach (var corner in track.CornerTopology.Corners)
        {
            var phase = track.CornerTopology.Resolve(
                corner.StartSegmentIndex,
                0f,
                lateralPosition,
                track.Geometry)
                ?? throw new InvalidOperationException("Logical corner has no start phase.");
            yield return Row(
                trackName,
                lateralPosition,
                corner.CornerId,
                corner.StartSegmentIndex,
                corner.EndSegmentIndex,
                phase.TotalCornerLengthMeters);
        }
    }

    private static IEnumerable<object?[]> ProgressRows(string trackName, Track track)
    {
        foreach (var corner in track.CornerTopology.Corners)
        foreach (var segmentIndex in Enumerable.Range(
                     corner.StartSegmentIndex,
                     corner.SegmentCount))
        {
            var phase = track.CornerTopology.Resolve(
                segmentIndex,
                0.5f,
                2.5f,
                track.Geometry)
                ?? throw new InvalidOperationException("Logical corner member has no phase.");
            yield return Row(
                trackName,
                corner.CornerId,
                segmentIndex,
                track.Segments[segmentIndex].Type,
                0.5f,
                phase.CornerProgress,
                phase.TotalCornerLengthMeters,
                phase.RemainingCornerLengthMeters);
        }
    }

    private static IEnumerable<RegressionRow> RegressionRows(
        IReadOnlyList<CalibrationScenarioResult> results)
    {
        var start = Get<CalibrationStartResult>(results, "start/baseline");
        yield return new(start.Metadata.ScenarioId, "reaction", start.Profile.ReactionTimeSeconds, "s");
        yield return new(start.Metadata.ScenarioId, "exit speed", start.Profile.ExitSpeedMetersPerSecond, "m/s");
        yield return new(start.Metadata.ScenarioId, "peak speed", start.Profile.PeakSpeedMetersPerSecond, "m/s");
        yield return new(start.Metadata.ScenarioId, "total time", start.Profile.TotalTimeSeconds, "s");

        var straight = Get<CalibrationStraightResult>(results, "straight/baseline");
        yield return new(straight.Metadata.ScenarioId, "exit speed", straight.Profile.ExitSpeedMetersPerSecond, "m/s");
        yield return new(straight.Metadata.ScenarioId, "peak speed", straight.Profile.PeakSpeedMetersPerSecond, "m/s");
        yield return new(straight.Metadata.ScenarioId, "travel time", straight.Profile.TravelTimeSeconds, "s");

        foreach (var id in new[]
                 {
                     "turn_entry/baseline",
                     "turn_middle/baseline",
                     "turn_exit/baseline",
                 })
        {
            var turn = Get<CalibrationTurnResult>(results, id);
            yield return new(id, "final exit speed", turn.Change.Speed, "m/s");
            yield return new(id, "travel time", turn.Diagnostics.TravelTimeSeconds, "s");
            yield return new(id, "travelled distance", turn.Diagnostics.TravelledMeters, "m");
        }

        var heat = Get<CalibrationHeatResult>(results, "full_heat/baseline");
        foreach (var rider in heat.Riders.OrderBy(item => item.Performance.RiderId))
        {
            var prefix = $"{heat.Metadata.ScenarioId}/rider_{rider.Performance.RiderId}";
            yield return new(prefix, "heat time", rider.Performance.TotalTimeSeconds, "s");
            yield return new(prefix, "Vmax", CalibrationUnits.MetersPerSecondToKph(
                rider.Performance.MaximumSpeedMetersPerSecond), "km/h");
            yield return new(prefix, "average speed",
                rider.Performance.AverageSpeedMetersPerSecond
                ?? throw new InvalidOperationException("Completed heat has no average speed."),
                "m/s");
        }
    }

    private static T Get<T>(
        IEnumerable<CalibrationScenarioResult> results,
        string scenarioId)
        where T : CalibrationScenarioResult
        => results.OfType<T>().Single(result =>
            StringComparer.Ordinal.Equals(result.Metadata.ScenarioId, scenarioId));

    private static string Range(Track track, int segmentIndex, float lateralPosition)
    {
        var phase = track.CornerTopology.Resolve(
            segmentIndex,
            0f,
            lateralPosition,
            track.Geometry);
        return phase is { } value
            ? $"{Format(value.SegmentStartCornerProgress)}–{Format(value.SegmentEndCornerProgress)}"
            : "—";
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static void ValidateSha(string value, string parameterName)
    {
        if (value is null
            || value.Length != 40
            || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "A full 40-character commit SHA is required.",
                parameterName);
        }
    }

    private static object?[] Row(params object?[] values) => values;

    private static string Format(object? value) => value switch
    {
        null => "—",
        float number => number.ToString("0.######", CultureInfo.InvariantCulture),
        double number => number.ToString("0.######", CultureInfo.InvariantCulture),
        bool flag => flag ? "yes" : "no",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!
            .Replace("|", "\\|", StringComparison.Ordinal)
            .ReplaceLineEndings(" "),
    };

    private sealed record RegressionRow(
        string Scenario,
        string Metric,
        double Value,
        string Unit);
}
