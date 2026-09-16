using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

public sealed class MotoarenaMatchedVenueCalibrationTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly Lazy<RealWorldCalibrationDataset> Global = new(() =>
        RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(Path.Combine(
            Root, "data", "calibration", "pge", "v1", "pge_rider_heats.csv"))));
    private static readonly Lazy<MotoarenaMatchedVenueCalibrationResult> Result = new(() =>
        MotoarenaMatchedVenueCalibration.Run(Global.Value));

    [Fact]
    public void ProfileStoresCurrentPublishedWidthsAndSeparatesModeledApproximation()
    {
        var profile = MatchedVenueProfiles.Motoarena2026;
        Assert.Equal("pge-2026-motoarena-torun", profile.VenueId);
        Assert.Equal("Motoarena im. Mariana Rosego", profile.SourceTrackLabel);
        Assert.Equal(2026, profile.Season);
        Assert.Equal(318f, profile.PublishedTrackLengthMeters);
        Assert.Equal(62f, profile.StraightLengthMeters);
        Assert.Equal(31f, profile.ReferenceRadiusMeters);
        Assert.Equal(12f, profile.PublishedStraightWidthMeters);
        Assert.Equal(17f, profile.PublishedFirstBendWidthMeters);
        Assert.Equal(16.2f, profile.PublishedSecondBendWidthMeters);
        Assert.Equal(16.6f, profile.ModeledSymmetricTurnWidthMeters);
        Assert.Equal((profile.PublishedFirstBendWidthMeters + profile.PublishedSecondBendWidthMeters) / 2f,
            profile.ModeledSymmetricTurnWidthMeters);
        Assert.Equal(18f, profile.OlderArticleBendWidthMeters);
        Assert.Equal("DerivedSymmetricWidthApproximationFromCurrentPublishedBendWidths",
            profile.ModeledTurnWidthClassification);
        Assert.Equal("OlderArticleReferenceOnly", profile.OlderArticleBendWidthClassification);
        Assert.Equal(16.6f, profile.CreateGeometry().TurnWidthMeters);
        Assert.Equal("granite", profile.SurfaceDescription);
        Assert.Contains("MeasurementConventionNotExplicitlyVerified", profile.GeometryConfidenceNotes);
        Assert.Contains("SymmetricGeometryApproximation", profile.GeometryConfidenceNotes);
        Assert.Contains("ProvisionalStartLineSplit", profile.StartLineConfidenceNotes);
    }

    [Fact]
    public void VenueJsonMatchesTypedProfileAndKeepsContextOutOfPhysics()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            Root, "data", "calibration", "venues", "v1", "motoarena-torun.json")));
        var root = json.RootElement;
        var profile = MatchedVenueProfiles.Motoarena2026;
        Assert.Equal(profile.VenueId, root.GetProperty("venue_id").GetString());
        Assert.Equal(profile.SourceTrackLabel,
            root.GetProperty("dataset_selector").GetProperty("source_track_label").GetString());
        Assert.Equal(profile.PublishedTrackLengthMeters,
            root.GetProperty("published_geometry").GetProperty("track_length_m").GetSingle());
        var published = root.GetProperty("published_geometry");
        Assert.Equal(12f, published.GetProperty("starting_straight_width_m").GetSingle());
        Assert.Equal(12f, published.GetProperty("back_straight_width_m").GetSingle());
        Assert.Equal(17f, published.GetProperty("first_bend_width_m").GetSingle());
        Assert.Equal(16.2f, published.GetProperty("second_bend_width_m").GetSingle());
        var modeled = root.GetProperty("modeled_geometry");
        Assert.Equal(16.6f, modeled.GetProperty("symmetric_turn_width_m").GetSingle());
        Assert.Equal("DerivedSymmetricWidthApproximationFromCurrentPublishedBendWidths",
            modeled.GetProperty("classification").GetString());
        var supporting = root.GetProperty("sources").GetProperty("supporting_geometry_history");
        Assert.Equal(18f, supporting.GetProperty("older_general_bend_width_m").GetSingle());
        Assert.Equal("OlderArticleReferenceOnly",
            supporting.GetProperty("older_general_bend_width_classification").GetString());
        Assert.Equal("HistoricalContext; not a calibration target",
            root.GetProperty("historical_context").GetProperty("classification").GetString());
    }

    [Fact]
    public void GeometryUsesExistingTrackModelAndPreservesRequiredTopology()
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        Assert.Equal(62f, track.Geometry.StraightLengthMeters);
        Assert.Equal(31f, track.Geometry.InnerRadiusMeters);
        Assert.Equal(12f, track.Geometry.StraightWidthMeters);
        Assert.Equal(16.6f, track.Geometry.TurnWidthMeters);
        Assert.Equal(MathF.PI / 3f, track.Geometry.TurnSegmentAngleRadians);
        Assert.True(track.Geometry.IsWithinFIMSpeedwayWidthEnvelope());
        Assert.Equal(9, track.Segments.Count);
        Assert.True(track.Segments[0].IsStandingStartSegment);
        Assert.Single(track.Segments.Where(segment => segment.IsStandingStartSegment));
        Assert.Equal(31f, track.Segments[0].StraightLengthMetersOverride);
        Assert.Equal(31f, track.Segments[^1].StraightLengthMetersOverride);
        Assert.Null(track.Segments[4].StraightLengthMetersOverride);
        Assert.Equal(2, track.CornerTopology.Corners.Count);
        Assert.All(track.CornerTopology.Corners, corner => Assert.Equal(3, corner.SegmentCount));
        Assert.All(track.CornerTopology.Corners, corner =>
            Assert.Equal(MathF.PI, corner.SegmentCount * track.Geometry.TurnSegmentAngleRadians, 5));

        foreach (var split in new[] { 25f, 31f, 37f })
        {
            var splitTrack = MatchedVenueProfiles.Motoarena2026.CreateTrack(split);
            Assert.Equal(62f,
                splitTrack.Segments[0].StraightLengthMetersOverride!.Value
                + splitTrack.Segments[^1].StraightLengthMetersOverride!.Value);
        }
    }

    [Fact]
    public void LapDistancesAreFiniteNearPublishedAtL0AndMonotonicThroughL4()
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var distances = Enumerable.Range(0, 5)
            .Select(line => MotoarenaMatchedVenueCalibration.LapDistance(track, line)).ToArray();
        Assert.All(distances, value => Assert.True(float.IsFinite(value) && value > 0f));
        Assert.Equal(318.778748f, distances[0], 4);
        Assert.NotEqual(318f, distances[0]);
        Assert.InRange(MathF.Abs(distances[0] - 318f), 0.7f, 0.9f);
        for (var line = 1; line < distances.Length; line++) Assert.True(distances[line] > distances[line - 1]);
        Assert.Equal(new[] { 318.778748f, 341.71237f, 364.646f, 387.57962f, 410.51324f }, distances,
            new FloatToleranceComparer(0.0002f));
    }

    [Fact]
    public void ExactVenueSubsetDoesNotInferAliasesAndRebuildsDistributions()
    {
        var global = Global.Value;
        var globalVmax = global.Distributions["pge_clean_vmax"].Quantiles;
        var subset = global.FilterByExactVenue(2026, "Motoarena im. Mariana Rosego");
        Assert.Equal(456, subset.Rows.Count);
        Assert.Equal(7, subset.Rows.Select(row => row.MatchId).Distinct().Count());
        Assert.Equal(new[] { "6950", "6956", "6961", "6970", "6979", "6980", "6995" },
            subset.Rows.Select(row => row.MatchId).Distinct().Order().ToArray());
        Assert.All(subset.Rows, row =>
        {
            Assert.Equal(2026, row.Season);
            Assert.Equal("Motoarena im. Mariana Rosego", row.SourceTrackLabel);
        });
        Assert.Equal(415, subset.Rows.Count(row => row.CompleteTelemetry));
        Assert.Equal(407, subset.Rows.Count(row => row.CleanPhysics));
        Assert.Equal(8, subset.Rows.Count(row => row.Eventful));
        Assert.Equal(41, subset.Rows.Count(row => row.AuditOnly));
        Assert.Equal(93, subset.Rows.Where(row => row.CleanPhysics)
            .GroupBy(row => (row.MatchId, row.HeatUid))
            .Count(group => group.Count() == 4 && group.Select(row => row.RiderId).Distinct().Count() == 4));
        Assert.Equal(344, global.FilterByExactVenue(2025, "Toruń").Rows.Count);
        Assert.Throws<InvalidOperationException>(() => global.FilterByExactVenue(2026, "Toruń"));
        Assert.Throws<InvalidOperationException>(() => global.FilterByExactVenue(2026, "motoarena im. Mariana Rosego"));
        Assert.Equal(globalVmax, global.Distributions["pge_clean_vmax"].Quantiles);
    }

    [Fact]
    public void VenueQuantilesUseTheGlobalLinearInterpolationContract()
    {
        var d = Result.Value.VenueDataset.Distributions;
        AssertQuantiles(d["pge_clean_vmax"], 108.6, 111, 113.7, 115.5, 116.9);
        AssertQuantiles(d["pge_clean_average_speed"], 21.4725768149, 21.9194817956,
            22.3848539354, 22.8619377964, 23.3573075464, 1e-6);
        AssertQuantiles(d["pge_clean_flying_lap_median"], 14.56, 14.71, 14.87, 15.01, 15.15);
        AssertQuantiles(d["pge_clean_heat_time"], 60.24, 60.8505, 61.479, 62.068, 62.587);
        AssertQuantiles(d["pge_clean_total_distance"], 1335.6, 1352.5, 1377, 1400, 1418);
        AssertQuantiles(d["pge_four_rider_vmax_spread"], 1.7, 2.4, 4.6, 6.5, 9.1);
        AssertQuantiles(d["pge_four_rider_average_speed_spread"], 0.4117603501, 0.6383775987,
            0.8568792859, 1.2113466899, 1.4430824512, 1e-6);
        AssertQuantiles(d["pge_four_rider_heat_time_spread"], 0.9846, 1.205, 1.589, 1.878, 2.1386);
        AssertQuantiles(d["pge_four_rider_l1_spread"], 0.254, 0.33, 0.45, 0.56, 0.64);
    }

    [Fact]
    public void ProductionSimulationIsDeterministicAndCoversRequiredSweepsAndPermutations()
    {
        var first = Result.Value;
        var second = MotoarenaMatchedVenueCalibration.Run(Global.Value);
        Assert.Equal(5, first.SpeedSweep.Count);
        Assert.Equal(5, first.SlideControlSweep.Count);
        Assert.Equal(5, first.Lines.Count);
        Assert.Equal(3, first.StartSplitSensitivity.Count);
        Assert.Equal(new[] { 16.2f, 16.6f, 17f, 18f },
            first.WidthSensitivity.Select(item => item.SymmetricTurnWidthMeters).ToArray());
        Assert.Contains(first.WidthSensitivity,
            item => item.SymmetricTurnWidthMeters == 16.6f && item.Classification.EndsWith("/ PRIMARY"));
        Assert.Contains(first.WidthSensitivity,
            item => item.SymmetricTurnWidthMeters == 18f && item.Classification == "OlderArticleReferenceOnly");
        Assert.Equal(24, first.PermutationTraceSha256.Count);
        Assert.Single(first.PermutationTraceSha256.Distinct(StringComparer.Ordinal));
        Assert.Equal(
            CalibrationCsvExporter.ExportSteps(first.MotoarenaBalanced.Trace),
            CalibrationCsvExporter.ExportSteps(second.MotoarenaBalanced.Trace));
        Assert.Equal(first.PermutationTraceSha256, second.PermutationTraceSha256);
        Assert.All(first.MotoarenaBalanced.Riders, rider =>
        {
            Assert.Equal(4, rider.Performance.L1Seconds.HasValue
                ? new[] { rider.Performance.L1Seconds, rider.Performance.L2Seconds,
                    rider.Performance.L3Seconds, rider.Performance.L4Seconds }.Count(value => value.HasValue)
                : 0);
            Assert.Equal(0, rider.Performance.CrashCount);
            Assert.Equal("Straight", rider.VmaxLocation);
        });
        Assert.NotEmpty(first.FlyingLapProfile);
        Assert.Contains(first.FlyingLapProfile, point => point.CornerProgress == .5f);
    }

    [Fact]
    public void FrozenPhysicsConstantsRemainExactlyAtReviewed38Values()
    {
        Assert.Equal(19f, SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond);
        Assert.Equal(16f, SegmentPhysics.ReferenceTurnSpeedMetersPerSecond);
        Assert.Equal(.5f, ContinuousCornerEnvelope.ApexProgress);
        Assert.Equal(5f / 6f, ContinuousCornerEnvelope.FullDriveProgress);
        Assert.Equal(2f, LongitudinalDynamics.MinCornerEntryDecelerationMetersPerSecondSquared);
        Assert.Equal(3.2f, LongitudinalDynamics.MaxCornerEntryDecelerationMetersPerSecondSquared);
        Assert.Equal(1.015f, SegmentPhysics.AdvancedQuietCorrectionSpeedFactor);
        Assert.Equal(1.06f, SegmentPhysics.MinAdvancedBrakeSpeedFactor);
        Assert.Equal(1.14f, SegmentPhysics.MaxAdvancedBrakeSpeedFactor);
        Assert.Equal(1.18f, SegmentPhysics.MinAdvancedRunWideSpeedFactor);
        Assert.Equal(1.34f, SegmentPhysics.MaxAdvancedRunWideSpeedFactor);
        Assert.Equal(.35f, SegmentPhysics.MinRunWideOverspeedRetention);
        Assert.Equal(.65f, SegmentPhysics.MaxRunWideOverspeedRetention);
        Assert.Equal(1.6f, LongitudinalDynamics.MinStraightAccelerationMetersPerSecondSquared);
        Assert.Equal(3.2f, LongitudinalDynamics.MaxStraightAccelerationMetersPerSecondSquared);
        Assert.Equal(1.2f, LongitudinalDynamics.MinTurnExitAccelerationMetersPerSecondSquared);
        Assert.Equal(2.8f, LongitudinalDynamics.MaxTurnExitAccelerationMetersPerSecondSquared);
        Assert.Equal(.035f, LongitudinalDynamics.ProvisionalDriveOrientedForceFadePerMeterPerSecond);
        Assert.Equal(.010f, LongitudinalDynamics.ProvisionalSpeedOrientedForceFadePerMeterPerSecond);
        Assert.Equal(16f, LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond);
        Assert.Equal(142f, LongitudinalDynamics.ProvisionalNominalSystemMassKilograms);
        Assert.Equal(40f, LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons);
        Assert.Equal(.2f, LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient);
        Assert.Equal(1.1f, LongitudinalDynamics.LowGearingDriveMultiplier);
        Assert.Equal(.9f, LongitudinalDynamics.HighGearingDriveMultiplier);
        Assert.Equal(.75f, LongitudinalDynamics.MinSurfaceDriveMultiplier);
        Assert.Equal(.25f, LongitudinalDynamics.SurfaceDriveMultiplierRange);
        Assert.Equal(1f, LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters);
        Assert.Equal(.28f, LongitudinalDynamics.ProvisionalStandingStartSlowReactionSeconds);
        Assert.Equal(.20f, LongitudinalDynamics.ProvisionalStandingStartFastReactionSeconds);
        Assert.Equal(9f, LongitudinalDynamics.ProvisionalStandingStartMinimumReferenceAccelerationMetersPerSecondSquared);
        Assert.Equal(11f, LongitudinalDynamics.ProvisionalStandingStartMaximumReferenceAccelerationMetersPerSecondSquared);
    }

    [Fact]
    public void ProductionPhysicsFilesContainNoVenueNameDependency()
    {
        foreach (var path in new[]
        {
            "src/CoreSim/SimulationEngine.cs",
            "src/CoreSim/Track/SegmentPhysics.cs",
            "src/CoreSim/Track/ContinuousCornerEnvelope.cs",
            "src/CoreSim/Track/LongitudinalDynamics.cs",
            "src/CoreSim/Track/TrackGeometry.cs",
            "src/CoreSim/Track/LaneModel.cs",
        })
        {
            var text = File.ReadAllText(Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar)));
            Assert.DoesNotContain("Motoarena", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReportIsCommittedByteStableLfOnlyAndCultureIndependent()
    {
        var expected = File.ReadAllText(Path.Combine(Root, "docs", "calibration", "motoarena-matched-venue.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string Render(string culture)
        {
            var prior = CultureInfo.CurrentCulture;
            var priorUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                return MotoarenaMatchedVenueReport.Render(Result.Value);
            }
            finally
            {
                CultureInfo.CurrentCulture = prior;
                CultureInfo.CurrentUICulture = priorUi;
            }
        }

        var en = Render("en-US");
        var pl = Render("pl-PL");
        Assert.Equal(expected, en);
        Assert.Equal(en, pl);
        Assert.DoesNotContain('\r', en);
        Assert.DoesNotContain("NaN", en, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", en, StringComparison.Ordinal);
        Assert.Contains("NO PHYSICS CONSTANTS CHANGED", en);
        Assert.Contains("Next-model decision evidence", en);
        Assert.Contains("faster than the real P10 envelope", en);
        Assert.Contains("DerivedSymmetricWidthApproximationFromCurrentPublishedBendWidths", en);
        Assert.Contains("OlderArticleReferenceOnly", en);
        Assert.Contains("line-distance plausibility remains unresolved", en);
        Assert.Contains("cannot be attributed solely to longitudinal physics", en);
    }

    [Fact]
    public void PgeV1AndHistoricalCalibrationReportsRemainContentIdenticalAcrossCheckoutLineEndings()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["data/calibration/pge/v1/literature_targets.json"] = "0BA6F118F0FC3F8A6298302FAC59CF04E72563D8773D22D59E5E56F367FE7765",
            ["data/calibration/pge/v1/pge_matches.csv"] = "88F9450116CF1807B3884FB9A84A953763B919C0E8D03649516064391885F99B",
            ["data/calibration/pge/v1/pge_rider_heats.csv"] = "DCBFF29E5EEB11402271D083DC8AF23BCDC0D7A921001B2B8A53228690CC29DC",
            ["data/calibration/pge/v1/pge_split.csv"] = "609BC682360658F3B452E8BC71B92665CB50CAE5B084AC7B21403A886F715A2C",
            ["data/calibration/pge/v1/README.md"] = "85125FAF298604AF018491BB0A382594A053B3FB89DF80E21590E6570E601C0E",
            ["data/calibration/pge/v1/source_manifest.json"] = "F2186F38E165DB26B372623EABCA732AFC8E3070A838C56ADEEA4C8336E4B3FD",
            ["data/calibration/pge/v1/summary.json"] = "4015DF4FBE1D9A78EAD116AC2B725BE3C41721DD079558FEE27C8AB45E8431F8",
            ["docs/calibration/current-model-baseline.md"] = "8161715C2CE229A35BB4DDA418F8D17D4330F08AE90EA81D76E18102F328C7B3",
            ["docs/calibration/physical-width-impact.md"] = "B847DC14170F9C3FC16A6F8A1FC4449546839FCF5024070E4F00165E56E24A50",
            ["docs/calibration/continuous-corner-correction-impact.md"] = "2EEE82EAE3368DB4AB01AA16B3C0CCE8BE18BBF81274C204BADE4D6E462CBFB2",
            ["docs/calibration/calibration-scenarios-baseline.md"] = "FB1EBB3F70A3D45537FCD1AFDC445D1683F8B1A843FA2F9CF1D42AC3CE274850",
            ["docs/calibration/longitudinal-speed-envelope-impact.md"] = "33E96B3F90A22E5AE82C004098DE986AC1771C7A989BEF9BA487E988697A417F",
            ["docs/calibration/continuous-corner-foundation-impact.md"] = "F28FEE8DD46828DBFB3C77A63F454A152F472378461E52F4536306BA55AC9E3C",
            ["docs/calibration/continuous-corner-envelope-impact.md"] = "9F97DE671EF6656E3054BC408C2B089CDC0D225208F7A6D0C5A2D36F3489CBB9",
        };
        foreach (var (relative, sha) in expected)
        {
            var path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            var canonicalText = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText))));
        }
    }

    private static void AssertQuantiles(CalibrationDistribution distribution,
        double p10, double p25, double p50, double p75, double p90, double tolerance = 1e-9)
    {
        Assert.InRange(Math.Abs(distribution.Quantiles.P10 - p10), 0, tolerance);
        Assert.InRange(Math.Abs(distribution.Quantiles.P25 - p25), 0, tolerance);
        Assert.InRange(Math.Abs(distribution.Quantiles.P50 - p50), 0, tolerance);
        Assert.InRange(Math.Abs(distribution.Quantiles.P75 - p75), 0, tolerance);
        Assert.InRange(Math.Abs(distribution.Quantiles.P90 - p90), 0, tolerance);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SpeedwayManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class FloatToleranceComparer(float tolerance) : IEqualityComparer<float>
    {
        public bool Equals(float x, float y) => MathF.Abs(x - y) <= tolerance;
        public int GetHashCode(float obj) => 0;
    }
}
