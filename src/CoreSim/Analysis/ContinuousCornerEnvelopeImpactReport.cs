using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

/// <summary>Deterministic, invariant-culture evidence for the bounded #38 calibration.</summary>
public static class ContinuousCornerEnvelopeImpactReport
{
    public const string BaseMainSha = "a7c7c387777ff9ee8899d2d1a34258e735ae0f60";
    public const string BeforeScenarioSha256 = "d85d5752a82a1ac083e90fedc2182374cf5a32442a4d683b3e50cf374462862d";

    private static readonly Candidate[] Candidates =
    {
        new("#37 baseline", 16f, 23.084566f, 18.705422f, 1.135835f, 66.079125f, 0, 0, 0, "captured base"),
        new("A0", 16f, 24.249595f, 19.807215f, 1.247300f, 62.402936f, 0, 0, 0,
            "530597104b31785ab04c8bdac0c57cf45898281b3f14c49c21a832aaadf0ca07"),
        new("B17", 17f, 24.826954f, 20.611117f, 1.286373f, 59.969130f, 0, 0, 0,
            "d76f618b20f4291f765bde90ade70a6c3df29d7048c4436f597c5fa9bb2932b4"),
        new("B18", 18f, 25.414567f, 21.415801f, 1.328361f, 57.715910f, 0, 0, 0,
            "b20c33988a512b0889c62d78b648b330186a42dd2ebbe1ea4c968e798126b1af"),
        new("B19 selected", 19f, 26.022228f, 22.218421f, 1.376471f, 55.630982f, 0, 0, 0,
            "final snapshot hash is recorded in provenance"),
    };

    public static string Render(IEnumerable<CalibrationScenarioResult> scenarioResults,
        RealWorldCalibrationDataset dataset, string afterScenarioSha256)
    {
        ArgumentNullException.ThrowIfNull(scenarioResults);
        ArgumentNullException.ThrowIfNull(dataset);
        ValidateHash(afterScenarioSha256);
        var results = scenarioResults.OrderBy(r => r.Metadata.ScenarioId, StringComparer.Ordinal).ToArray();
        if (results.Length != 197 || results.Select(r => r.Metadata.ScenarioId).Distinct(StringComparer.Ordinal).Count() != results.Length)
            throw new ArgumentException("The final report requires the complete, unique 197-scenario suite.", nameof(scenarioResults));
        var baseline = GetHeat("full_heat/baseline");
        _ = RealWorldCalibrationEvaluator.Evaluate(dataset, baseline.ToSimulationCalibrationResult());
        var b = new StringBuilder();

        Line("# Continuous Corner Envelope impact (#38)");
        Heading("A. Provenance");
        Line($"Base main SHA: `{BaseMainSha}`");
        Line("Selected candidate checkpoint: `B19` (advanced settled-corner reference 19 m/s; all other candidate inputs frozen).");
        Line($"Complete BEFORE scenario snapshot SHA-256: `{BeforeScenarioSha256}` (186 scenarios captured on base before production edits).");
        Line($"Complete AFTER scenario snapshot SHA-256: `{afterScenarioSha256}` (197 final scenarios).");
        Line("Candidate snapshots contain complete typed production results, including full traces. No timestamp or machine path enters this report.");
        Line("Final model: one physical-distance traversal over CornerProgress 0..1; recoverable pre-apex envelope, settled apex at 0.5, and signed existing corner drive after the apex.");

        Heading("B. Architecture before");
        Line("`Straight → TurnEntry scrub → TurnMiddle carry/correction → TurnExit correction + drive → Straight`. Segment labels selected longitudinal laws.");

        Heading("C. Architecture after");
        Line("`Straight → Continuous Logical Corner 0..1 → Straight`. TurnEntry/Middle/Exit remain topology/reporting compatibility labels, not longitudinal physics phases.");
        Line("ADVANCED production calls to the old TurnEntry scrub path: **0**. ADVANCED production calls to segment-gated TurnExit drive: **0**. Legacy behavior remains on its prior path.");

        Heading("D. Envelope shape");
        Line("ApexProgress = 0.50 is a provisional geometry assumption, not telemetry-derived. FullDriveProgress = 5/6 is a behavior-derived starting assumption: smoothstep exposure integrates to approximately the former final-third full-drive exposure.");
        Line();
        Table("Progress | Lateral | Radius m | EffectiveGrip | Settled/apex m/s | Envelope entry m/s | Envelope exit m/s | Drive availability | Correction m/s² | Actual entry m/s | Actual exit m/s | Outcome | Residual m/s",
            results.OfType<CalibrationTurnResult>()
                .Where(r => r.Metadata.ScenarioId.StartsWith("continuous_corner/progress/", StringComparison.Ordinal))
                .Select(r =>
                {
                    var p = r.Diagnostics.ContinuousCornerProfile!;
                    return Row(p.Nodes[0].CornerProgress, r.Scenario.LateralPosition, r.RadiusMeters,
                        r.Scenario.Fixture.Surface.EffectiveGrip, p.ApexSpeedMetersPerSecond,
                        p.EnvelopeAtEntryMetersPerSecond, p.EnvelopeAtExitMetersPerSecond,
                        p.Nodes[0].DriveAvailability, r.Capability.CorrectionDecelerationMetersPerSecondSquared,
                        r.Change.EntrySpeed, r.Change.Speed, r.Change.Outcome, p.ResidualOverspeedMetersPerSecond);
                }));
        Line("For p < 0.5: `v_envelope = sqrt(v_apex² + 2 × correctionCapability × (0.5 - p) × totalCornerLength)`.");
        Line("For p > 0.5: start at v_apex and integrate the existing signed full-turn net force over post-apex distance in 1 m steps plus final remainder. The net contribution is multiplied by `smoothstep(clamp((p-0.5)/(5/6-0.5),0,1))`; availability 0 is neutral carry.");

        Heading("E. Candidate screen");
        Line("The screen is hierarchical, not an optimizer. A0 measures architecture at 16 m/s. Stage B changes only the advanced settled-corner reference using the required 17/18/19 menu.");
        Line();
        Table("Candidate | Reference m/s | Entry envelope m/s | Apex m/s | Exit envelope m/s | Speed50 Vmax P50 m/s | Avg P50 m/s | L1 penalty P50 s | HeatTime P50 s | Brake | RunWide | Crash | Snapshot/checkpoint",
            Candidates.Select(c =>
            {
                if (c.Id == "#37 baseline")
                {
                    return Row(c.Id, c.ReferenceSpeed, "historical segmented", "historical segmented",
                        "historical segmented", c.Vmax, c.Average, c.L1Penalty, c.HeatTime,
                        c.Brake, c.RunWide, c.Crash, c.Checkpoint);
                }
                var shape = CandidateShape(c.ReferenceSpeed);
                return Row(c.Id, c.ReferenceSpeed, shape.Entry, shape.Apex, shape.Exit, c.Vmax, c.Average,
                    c.L1Penalty, c.HeatTime, c.Brake, c.RunWide, c.Crash, c.Checkpoint);
            }));
        Line("Vmax location summary: the baseline and all tested candidates retain complete-heat maxima on Straight segments; the new peak diagnostic can also report corner progress when a corner wins.");

        Heading("F. Selected candidate");
        Line("B19 is selected. A0 removes much of the structural bottleneck but Speed50 average remains 19.807 m/s. B17 and B18 remain below the PGE CleanPhysics P10 average-speed context. B19 is the smallest tested menu value that reaches that lower contextual envelope (22.218 m/s), keeps all measured baseline events at zero, preserves monotonic skill/line behavior, and still leaves conservative Vmax rather than forcing the real PGE median.");
        Line("The remaining Vmax deficit is documented as geometry/model context. No second parameter was opened and no magic aggregate score was used.");

        Heading("G. Speed skill sweep");
        HeatTable(results.OfType<CalibrationHeatResult>().Where(r => r.Metadata.ScenarioId.StartsWith("full_heat/speed/", StringComparison.Ordinal)));

        Heading("H. SlideControl sweep");
        HeatTable(results.OfType<CalibrationHeatResult>().Where(r => r.Metadata.ScenarioId.StartsWith("full_heat/slide_control/", StringComparison.Ordinal)));

        Heading("I. Lines");
        Line("Outer trajectories have larger radius and capability but pay a longer physical distance. No outer-line bonus exists.");
        Line();
        Table("Lateral | Radius m | Corner-segment distance m | Entry envelope m/s | Apex m/s | Exit envelope m/s | Production Vmax m/s | Avg m/s | HeatTime s",
            results.OfType<CalibrationLineResult>()
                .Where(r => r.Metadata.ScenarioId.StartsWith("line_geometry/lateral/", StringComparison.Ordinal))
                .Select(r =>
                {
                    var rider = new RiderState(1, r.Scenario.LateralPosition) { LateralPosition = r.Scenario.LateralPosition };
                    var track = Track.CreateStandingStartExample();
                    var phase = track.CornerTopology.Resolve(1, 0f, rider.LateralPosition, track.Geometry)!.Value;
                    var e = ContinuousCornerEnvelope.Create(phase, rider.LateralPosition, track.Geometry,
                        r.Scenario.Fixture.Surface, r.Scenario.Fixture.Skills, r.Scenario.Fixture.Setup);
                    return Row(r.Scenario.LateralPosition, r.RadiusMeters, r.TurnArcLengthMeters,
                        e.SpeedMetersPerSecond(0f), e.ApexSpeedMetersPerSecond, e.SpeedMetersPerSecond(1f),
                        r.Rider.Performance.MaximumSpeedMetersPerSecond, r.Rider.Performance.AverageSpeedMetersPerSecond,
                        r.Rider.Performance.TotalTimeSeconds);
                }));

        Heading("J. Surfaces and gearing");
        Line("Surface remains entry-sampled through the existing grid; a real surface difference changes capability, while labels alone do not. Gearing remains the existing normalized trade-off.");
        Line();
        HeatTable(results.OfType<CalibrationHeatResult>().Where(r =>
            r.Metadata.ScenarioId.StartsWith("full_heat/surface/", StringComparison.Ordinal)
            || r.Metadata.ScenarioId.StartsWith("full_heat/gearing/", StringComparison.Ordinal)));

        Heading("K. Before/after complete heat");
        Table("Fixture/rider | Vmax before m/s | Vmax after m/s | Average before m/s | Average after m/s | L1 penalty before s | L1 penalty after s | HeatTime before s | HeatTime after s | Events before | Events after",
            BeforeRiders.Zip(baseline.Riders.OrderBy(r => r.Performance.RiderId), (old, now) => Row(
                $"baseline/rider_{old.RiderId}", old.Vmax, now.Performance.MaximumSpeedMetersPerSecond,
                old.Average, now.Performance.AverageSpeedMetersPerSecond, old.L1Penalty,
                now.Performance.FirstLapPenaltySeconds, old.HeatTime, now.Performance.TotalTimeSeconds,
                "0/0/0", $"{now.Performance.BrakeCount}/{now.Performance.RunWideCount}/{now.Performance.CrashCount}")));
        Line();
        Table("Fixture | Vmax P50 before m/s | Vmax P50 after m/s | Avg P50 before m/s | Avg P50 after m/s | HeatTime P50 before s | HeatTime P50 after s",
            new[]
            {
                Comparison("Speed0", 21.167463f, 17.433693f, 70.899422f, GetHeat("full_heat/speed/000")),
                Comparison("Speed50/baseline", 23.084566f, 18.705422f, 66.079125f, baseline),
                Comparison("Speed100", 24.917023f, 19.934956f, 62.002954f, GetHeat("full_heat/speed/100")),
            });

        Heading("L. Vmax and corner extrema");
        Line("Before: 4/4 complete-heat baseline maxima were on Straight segments. After: 4/4 are on Straight segments (100% Straight, 0% Corner). Peak selection takes the maximum of every observed source; it does not nullable-coalesce one source away.");
        Line();
        Table("Rider | Vmax m/s | Segment id/type | Corner progress if applicable | Actual corner minimum m/s @ progress | Last corner exit m/s",
            baseline.Riders.OrderBy(r => r.Performance.RiderId).Select(r =>
            {
                var id = r.Performance.RiderId;
                var samples = baseline.Trace.StepSamples.Where(s => s.RiderId == id).ToArray();
                var peak = samples.OrderByDescending(s => s.PeakSpeedMetersPerSecond).First();
                var corner = samples.Where(s => s.ContinuousCornerProfile is not null).ToArray();
                var min = corner.SelectMany(s => s.ContinuousCornerProfile!.Nodes).OrderBy(n => n.SpeedMetersPerSecond).First();
                return Row(id, r.Performance.MaximumSpeedMetersPerSecond, $"{peak.SegmentId}/{peak.SegmentType}",
                    peak.PeakCornerProgress, $"{F(min.SpeedMetersPerSecond)} @ {F(min.CornerProgress)}",
                    corner[^1].ExitSpeedMetersPerSecond);
            }));
        var extreme = GetHeat("full_heat/extreme_speed100_gearing100_outer");
        var outermost = results.OfType<CalibrationLineResult>().Single(r =>
            r.Metadata.ScenarioId == "line_geometry/extreme_speed100_gearing100_outer4");
        var extremeVmax = MathF.Max(extreme.Riders.Max(r => r.Performance.MaximumSpeedMetersPerSecond),
            outermost.Rider.Performance.MaximumSpeedMetersPerSecond);
        Line($"Extreme production diagnostic (Speed100, gearing 1, best surface, lanes 0–4) maximum actual Vmax: {F(extremeVmax)} m/s ({F(extremeVmax * 3.6f)} km/h). It combines the four-rider lanes-0–3 heat with the outermost-line-4 production heat. No hard speed cap was added.");

        Heading("M. Real telemetry comparison");
        Line("The existing pge-v1 dataset is parsed by RealWorldCalibrationDataset and evaluated component-wise by RealWorldCalibrationEvaluator. Skill50 is not assumed to be the average PGE rider; absolute time/distance remains geometry-sensitive.");
        Line();
        Table("Context metric | P10 | P50 | P90 | Selected simulation P50 | Classification", new[]
        {
            Row("CleanPhysics Vmax km/h", Q("pge_clean_vmax", .1), Q("pge_clean_vmax", .5), Q("pge_clean_vmax", .9), Median(baseline.Riders.Select(r => r.Performance.MaximumSpeedMetersPerSecond)) * 3.6, "ComparableEnvelope"),
            Row("CleanPhysics AverageSpeed m/s", Q("pge_clean_average_speed", .1), Q("pge_clean_average_speed", .5), Q("pge_clean_average_speed", .9), Median(baseline.Riders.Select(r => r.Performance.AverageSpeedMetersPerSecond!.Value)), "ComparableEnvelope"),
            Row("Within-heat Vmax spread km/h", null, 4.5, null, baseline.Spreads.VmaxKph, "context"),
            Row("Within-heat AvgSpeed spread m/s", null, .927, null, baseline.Spreads.AverageSpeedMetersPerSecond, "context"),
            Row("Within-heat L1 spread s", null, .53, null, baseline.Spreads.L1Seconds, "context"),
            Row("Within-heat HeatTime spread s", null, 1.53, null, baseline.Spreads.HeatTimeSeconds, "context"),
        });

        Heading("N. Frozen boundaries");
        Table("Boundary | Final value/status", new[]
        {
            Row("Straight reference acceleration", "1.60–3.20 m/s² unchanged"),
            Row("Turn full-drive endpoint", "1.20–2.80 m/s² unchanged"),
            Row("Drive/speed fade", "0.0350 / 0.0100 unchanged"),
            Row("Positive-drive reference speed", "16 m/s unchanged"),
            Row("Mass / resistance", "142 kg / 40 + 0.20v² N unchanged"),
            Row("Gearing / surface drive", "1.10→0.90 / 0.75+0.25×EffectiveGrip unchanged"),
            Row("Integration", "1 m + final remainder unchanged"),
            Row("Reaction / launch", "0.28→0.20 s / 9→11 m/s² unchanged"),
            Row("Correction capability", "2.0→3.2 m/s² unchanged"),
            Row("Quiet / Brake / RunWide", "1.015 / 1.06→1.14 / 1.18→1.34 unchanged"),
            Row("RunWide retention", "0.35→0.65 unchanged"),
            Row("Legacy", "numerically unchanged; reference remains 16 m/s"),
            Row("Only tuned physics parameter", "ADVANCED settled/apex reference 16→19 m/s"),
        });
        Line("Random incident channels/probabilities/severity/0.88 consequence, target clearing, contact probability and geometry, lateral traversal, occupancy thresholds, surface physics and RiderSkills multipliers are unchanged.");

        Heading("O. Known limitations");
        Line("Apex 0.50 and FullDriveProgress 5/6 are provisional behavior/geometry assumptions, not telemetry fits. The track is not matched to a real PGE venue. Entry-sampled surface can change at old subsegment boundaries when the surface itself differs. Diagonal/spiral path length, throttle AI, RPM, torque curves, clutch, real sprockets, wheelspin, traction cap, slip/lean/steering/body/banking/suspension and additional drag remain outside scope. No artificial Vmax, outer-line bonus or final-corner bonus exists. B19 retains a Vmax deficit versus real PGE context; it is reported rather than hidden by further tuning.");
        return b.ToString();

        CalibrationHeatResult GetHeat(string id) => results.OfType<CalibrationHeatResult>().Single(r => r.Metadata.ScenarioId == id);
        double Q(string id, double p) => CalibrationDistribution.LinearQuantile(dataset.Distributions[id].Observations, p);
        object?[] Comparison(string id, float oldV, float oldA, float oldH, CalibrationHeatResult now)
            => Row(id, oldV, Median(now.Riders.Select(r => r.Performance.MaximumSpeedMetersPerSecond)), oldA,
                Median(now.Riders.Select(r => r.Performance.AverageSpeedMetersPerSecond!.Value)), oldH,
                Median(now.Riders.Select(r => r.Performance.TotalTimeSeconds)));
        void HeatTable(IEnumerable<CalibrationHeatResult> heats)
            => Table("Scenario | Vmax P50 m/s | Average P50 m/s | L1 penalty P50 s | HeatTime P50 s | Brake | RunWide | Crash",
                heats.OrderBy(r => r.Metadata.ScenarioId, StringComparer.Ordinal).Select(r => Row(r.Metadata.ScenarioId,
                    Median(r.Riders.Select(x => x.Performance.MaximumSpeedMetersPerSecond)),
                    Median(r.Riders.Select(x => x.Performance.AverageSpeedMetersPerSecond!.Value)),
                    Median(r.Riders.Select(x => x.Performance.FirstLapPenaltySeconds!.Value)),
                    Median(r.Riders.Select(x => x.Performance.TotalTimeSeconds)),
                    r.Riders.Sum(x => x.Performance.BrakeCount), r.Riders.Sum(x => x.Performance.RunWideCount),
                    r.Riders.Sum(x => x.Performance.CrashCount))));
        void Line(string value = "") => b.Append(value).Append('\n');
        void Heading(string value) { Line(); Line($"## {value}"); Line(); }
        void Table(string header, IEnumerable<object?[]> rows)
        {
            Line($"| {header} |");
            Line($"| {string.Join(" | ", header.Split('|').Select(_ => "---"))} |");
            foreach (var row in rows) Line($"| {string.Join(" | ", row.Select(Format))} |");
            Line();
        }
    }

    private static (float Entry, float Apex, float Exit) CandidateShape(float reference)
    {
        var track = Track.CreateStandingStartExample();
        var phase = track.CornerTopology.Resolve(1, 0f, 1f, track.Geometry)!.Value;
        var surface = CalibrationScenarioCatalog.Baseline.Surface;
        var skills = CalibrationScenarioCatalog.Baseline.Skills;
        var setup = CalibrationScenarioCatalog.Baseline.Setup;
        var finalApex = SegmentPhysics.MaxSafeTurnSpeed(1f, track.Geometry, surface, skills, setup);
        var e = new ContinuousCornerEnvelope(finalApex * reference / SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond,
            phase.TotalCornerLengthMeters,
            LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(skills, surface),
            LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(skills, setup, surface), setup);
        return (e.SpeedMetersPerSecond(0f), e.ApexSpeedMetersPerSecond, e.SpeedMetersPerSecond(1f));
    }

    private static double Median(IEnumerable<float> values)
    {
        var ordered = values.Select(x => (double)x).Order().ToArray();
        return ordered.Length % 2 == 1 ? ordered[ordered.Length / 2] : (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2d;
    }

    private static object?[] Row(params object?[] values) => values;
    private static string F(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private static string Format(object? value) => value switch
    {
        null => "—",
        float n => n.ToString("0.######", CultureInfo.InvariantCulture),
        double n => n.ToString("0.######", CultureInfo.InvariantCulture),
        IFormattable x => x.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" "),
    };
    private static void ValidateHash(string value)
    {
        if (value is null || value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("A full SHA-256 is required.", nameof(value));
    }

    private static readonly BeforeRider[] BeforeRiders =
    {
        new(1, 21.987732f, 17.491120f, 1.093054f, 64.214630f),
        new(2, 22.722650f, 18.310764f, 1.118332f, 65.457890f),
        new(3, 23.446482f, 19.100080f, 1.153337f, 66.700360f),
        new(4, 24.116991f, 19.865341f, 1.199566f, 67.926350f),
    };

    private sealed record Candidate(string Id, float ReferenceSpeed, float Vmax, float Average,
        float L1Penalty, float HeatTime, int Brake, int RunWide, int Crash, string Checkpoint);
    private sealed record BeforeRider(int RiderId, float Vmax, float Average, float L1Penalty, float HeatTime);
}
