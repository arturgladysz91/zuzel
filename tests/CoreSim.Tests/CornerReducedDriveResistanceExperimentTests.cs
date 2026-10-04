using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "historical-analysis")]
public sealed class CornerReducedDriveResistanceExperimentTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly Lazy<CornerReducedDriveResistanceExperimentResult> Result =
        new(CornerReducedDriveResistanceExperiment.Run);

    [Fact]
    public void CandidateMenuIsExactlyFrozenAndTraversalValidatesItsRange()
    {
        Assert.Equal(new[] { "R0", "R25", "R50", "R75", "R100" },
            CornerReducedDriveResistanceExperiment.CandidateMenu.Select(item => item.Id));
        Assert.Equal(new[] { 0f, .25f, .5f, .75f, 1f },
            CornerReducedDriveResistanceExperiment.CandidateMenu
                .Select(item => item.ReducedDriveResistanceExposure));

        foreach (var invalid in new[] { float.NegativeInfinity, -.0001f, 1.0001f, float.PositiveInfinity, float.NaN })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CornerResistanceCandidate("invalid", invalid));
    }

    [Fact]
    public void DefaultAndR0AreExactPost41ProductionAndBaselineIsReproduced()
    {
        var freeze = Result.Value.Freeze;
        Assert.True(freeze.ProductionDefaultExact);
        Assert.True(freeze.R0ExactProduction);
        Assert.True(freeze.R0ForceIdentity);
        Assert.True(freeze.FullProductionScenarioSuiteExact);

        var baseline = Primary("R0");
        Assert.Equal(1366.849609, baseline.ModeledFourLapDistanceMeters, 6);
        Assert.Equal(97.882807, baseline.VmaxKilometersPerHour, 6);
        Assert.Equal(13.909906, baseline.FlyingLapMedianSeconds, 6);
        Assert.Equal(27.098434, baseline.CornerEntrySpeedMetersPerSecond, 6);
        Assert.Equal(22.585236, baseline.ApexSpeedMetersPerSecond, 6);
        Assert.Equal(24.296925, baseline.CornerExitSpeedMetersPerSecond, 6);
        Assert.Equal(4.558926, baseline.PeakToApexAmplitudeMetersPerSecond, 6);
    }

    [Fact]
    public void HeatApexIsOnlyTheMeanCanonicalHalfProgressAndMinimumRemainsIndependent()
    {
        foreach (var observation in Result.Value.Primary)
        {
            var apexPoints = observation.ProfilePoints
                .Where(point => point.CornerProgress == ContinuousCornerEnvelope.ApexProgress)
                .OrderBy(point => point.CornerNumber)
                .ToArray();
            Assert.Equal(2, apexPoints.Length);
            Assert.Equal(apexPoints.Average(point => point.ActualSpeedMetersPerSecond),
                observation.ApexSpeedMetersPerSecond);
            Assert.Equal(observation.StraightPeakSpeedMetersPerSecond
                    - observation.ApexSpeedMetersPerSecond,
                observation.PeakToApexAmplitudeMetersPerSecond);
            Assert.Equal(observation.ApexSpeedMetersPerSecond
                    - observation.CornerEntrySpeedMetersPerSecond,
                observation.EntryToApexDeltaSpeedMetersPerSecond);
            Assert.Equal(observation.CornerExitSpeedMetersPerSecond
                    - observation.ApexSpeedMetersPerSecond,
                observation.ApexToExitDeltaSpeedMetersPerSecond);
            Assert.Equal(observation.StraightPeakSpeedMetersPerSecond
                    - observation.MinimumSpeedMetersPerSecond,
                observation.PeakToMinimumAmplitudeMetersPerSecond);
            Assert.InRange(observation.MinimumSpeedCornerProgress, 0d, 1d);
            Assert.Contains(observation.MinimumSpeedCornerNumber, new[] { 1, 2 });
        }

        var r100 = Primary("R100");
        Assert.True(r100.MinimumSpeedMetersPerSecond < r100.ApexSpeedMetersPerSecond);
        Assert.True(r100.MinimumSpeedCornerProgress > ContinuousCornerEnvelope.ApexProgress);
        Assert.NotEqual(r100.PeakToApexAmplitudeMetersPerSecond,
            r100.PeakToMinimumAmplitudeMetersPerSecond);
    }

    [Fact]
    public void HeatAndIsolatedObservationsUseTheSameCanonicalApexDefinition()
    {
        foreach (var candidate in CornerReducedDriveResistanceExperiment.CandidateMenu)
        {
            var heat = Primary(candidate.Id);
            var isolated = Isolated(candidate.Id);
            Assert.Equal(heat.ProfilePoints.Where(point => point.CornerProgress == .5f)
                    .Average(point => point.ActualSpeedMetersPerSecond),
                heat.ApexSpeedMetersPerSecond);
            Assert.Equal(isolated.ProfilePoints.Single(point => point.CornerProgress == .5f)
                    .ActualSpeedMetersPerSecond,
                isolated.ApexSpeedMetersPerSecond);
        }

        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(
            MatchedVenueProfiles.MotoarenaHistorical39StartLineToFirstCornerMeters);
        var phase = track.CornerTopology.Resolve(1, 0f,
            CornerReducedDriveResistanceExperiment.PrimaryLateralPosition, track.Geometry)!.Value;
        var envelope = ContinuousCornerEnvelope.Create(
            phase,
            CornerReducedDriveResistanceExperiment.PrimaryLateralPosition,
            track.Geometry,
            CalibrationScenarioCatalog.Baseline.Surface,
            new RiderSkills(50f, 50f, 50f, 50f, 50f, 50f),
            BikeSetup.Neutral);
        var equivalentProduction = envelope.Traverse(
            CornerReducedDriveResistanceExperiment.IsolatedEntrySpeedMetersPerSecond,
            0f,
            phase.TotalCornerLengthMeters);
        var productionApex = equivalentProduction.Nodes.Single(node =>
            node.CornerProgress == ContinuousCornerEnvelope.ApexProgress).SpeedMetersPerSecond;
        Assert.Equal(BitConverter.SingleToInt32Bits(productionApex),
            BitConverter.SingleToInt32Bits(Isolated("R0").ApexSpeedMetersPerSecond));
    }

    [Fact]
    public void ForceDecompositionUsesExistingResistanceAndPreservesFrozenLaws()
    {
        var r0 = Isolated("R0").ProfilePoints[0];
        var r100 = Isolated("R100").ProfilePoints[0];
        Assert.Equal(0f, r0.DriveAvailability);
        Assert.Equal(0f, r0.ExposedResistanceForceNewtons);
        Assert.Equal(0f, r0.NetForceNewtons);
        Assert.Equal(0f, r100.DriveAvailability);
        Assert.Equal(r100.ResistanceForceNewtons, r100.ExposedResistanceForceNewtons);
        Assert.Equal(-r100.ResistanceForceNewtons, r100.NetForceNewtons);
        Assert.Equal(40f + .20f * r100.ActualSpeedMetersPerSecond * r100.ActualSpeedMetersPerSecond,
            r100.ResistanceForceNewtons, 4);

        var freeze = Result.Value.Freeze;
        Assert.True(freeze.AvailabilityOneExact);
        Assert.True(freeze.EnvelopeExact);
        Assert.True(freeze.CorrectionExact);
        Assert.True(freeze.StandingStartExact);
        Assert.True(freeze.StraightExact);
        Assert.True(freeze.LegacyExact);
        Assert.True(freeze.SegmentPhysicsThresholdsExact);
        Assert.Equal(.24, freeze.ReactionTimeSeconds, 7);
        Assert.Equal(2.237621, freeze.TimeTo70KphSeconds, 6);
        Assert.Equal(62.332127, freeze.SpeedAtTwoSecondsKilometersPerHour, 6);
    }

    [Fact]
    public void DistanceBucketsAreExclusiveConservedAndTimeBudgetsReconcile()
    {
        foreach (var observation in Result.Value.Primary)
        {
            var d = observation.DistanceBuckets;
            Assert.True(d.Conserved);
            Assert.Equal(d.TravelledDistanceMeters,
                d.CorrectionDistanceMeters + d.PassiveResistanceDistanceMeters
                + d.PositiveDriveDistanceMeters + d.NegativeSignedDriveDistanceMeters
                + d.NeutralCarryDistanceMeters, 4);

            var t = observation.TimeBudget;
            Assert.Equal(t.FlyingLapTimeSeconds,
                t.TotalStraightTimeSeconds + t.TotalCornerTimeSeconds, 4);
            Assert.NotEqual(observation.FlyingLapMedianSeconds, t.FlyingLapTimeSeconds);
        }

        Assert.All(Result.Value.Isolated, item => Assert.True(item.DistanceBuckets.Conserved));
        Assert.True(Isolated("R0").DistanceBuckets.ZeroAvailabilityUncorrectedDistanceMeters > 0d);
        Assert.True(Isolated("R100").DistanceBuckets.PassiveResistanceDistanceMeters > 0d);
    }

    [Fact]
    public void AllObservationsRemainFinitePositiveAndOrdered()
    {
        foreach (var observation in AllHeatObservations())
        {
            Assert.All(new[]
            {
                observation.ModeledFourLapDistanceMeters, observation.VmaxKilometersPerHour,
                observation.FlyingLapMedianSeconds, observation.HeatTimeSeconds,
                observation.AverageSpeedMetersPerSecond, observation.CornerEntrySpeedMetersPerSecond,
                observation.ApexSpeedMetersPerSecond, observation.CornerExitSpeedMetersPerSecond,
                observation.MinimumSpeedMetersPerSecond, observation.MinimumSpeedCornerProgress,
            }, value => Assert.True(double.IsFinite(value) && value > 0d));
            Assert.False(observation.AnyZeroSpeedEvent);
            Assert.All(observation.ProfilePoints, point =>
            {
                Assert.True(float.IsFinite(point.ActualSpeedMetersPerSecond));
                Assert.True(point.ActualSpeedMetersPerSecond > 0f);
                Assert.True(float.IsFinite(point.NetAccelerationMetersPerSecondSquared));
            });
        }

        foreach (var candidate in new[] { "R0", "R100" })
        {
            var lines = Result.Value.LineSweep.Where(item => item.CandidateId == candidate)
                .OrderBy(item => item.LateralPosition).ToArray();
            Assert.True(lines.Zip(lines.Skip(1)).All(pair =>
                pair.Second.ModeledFourLapDistanceMeters > pair.First.ModeledFourLapDistanceMeters));

            var speeds = Result.Value.SpeedSweep.Where(item => item.CandidateId == candidate)
                .OrderBy(item => item.SkillSpeed).ToArray();
            Assert.True(speeds.Zip(speeds.Skip(1)).All(pair =>
                pair.Second.VmaxKilometersPerHour > pair.First.VmaxKilometersPerHour
                && pair.Second.FlyingLapMedianSeconds < pair.First.FlyingLapMedianSeconds));

            var slides = Result.Value.SlideControlSweep.Where(item => item.CandidateId == candidate)
                .OrderBy(item => item.SkillSlideControl).ToArray();
            Assert.True(slides.Zip(slides.Skip(1)).All(pair =>
                pair.Second.FlyingLapMedianSeconds < pair.First.FlyingLapMedianSeconds));
        }
    }

    [Fact]
    public void ResultIsDiagnosticallyClassifiedWithoutSelectingAProductionCandidate()
    {
        var baseline = Primary("R0");
        var full = Primary("R100");
        Assert.True(full.FlyingLapMedianSeconds > baseline.FlyingLapMedianSeconds);
        Assert.True(full.TimeBudget.TotalCornerTimeSeconds > baseline.TimeBudget.TotalCornerTimeSeconds);
        var pre = full.TimeBudget.FirstCornerEntryToApexSeconds
            + full.TimeBudget.SecondCornerEntryToApexSeconds
            - baseline.TimeBudget.FirstCornerEntryToApexSeconds
            - baseline.TimeBudget.SecondCornerEntryToApexSeconds;
        var post = full.TimeBudget.FirstCornerApexToExitSeconds
            + full.TimeBudget.SecondCornerApexToExitSeconds
            - baseline.TimeBudget.FirstCornerApexToExitSeconds
            - baseline.TimeBudget.SecondCornerApexToExitSeconds;
        Assert.True(post > pre * 2d);
        Assert.Equal("LossLocationMismatch", Result.Value.Classification);
        Assert.Equal("corner-specific slip-loss / scrub-drag", Result.Value.NextSubsystem);
    }

    [Fact]
    public void SameSeedIsDeterministicAndAllRiderPermutationsAreInvariant()
    {
        Assert.Equal(1, Result.Value.PermutationDistinctTraceHashes["R0"]);
        Assert.Equal(1, Result.Value.PermutationDistinctTraceHashes["R100"]);
        var second = CornerReducedDriveResistanceExperiment.Run();
        Assert.Equal(CornerReducedDriveResistanceExperimentReport.Render(Result.Value),
            CornerReducedDriveResistanceExperimentReport.Render(second));
    }

    [Fact]
    public void ExperimentPathHasNoGlobalMutableState()
    {
        var types = new[]
        {
            typeof(CornerReducedDriveResistanceExperiment),
            typeof(CornerReducedDriveResistanceExperimentReport),
        };
        foreach (var field in types.SelectMany(type => type.GetFields(
                     BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
            Assert.True(field.IsInitOnly || field.IsLiteral, $"Mutable static field: {field.DeclaringType}.{field.Name}");
    }

    [Fact]
    public void ReportIsCommittedByteStableInvariantCultureLfOnlyAndComplete()
    {
        var expected = CanonicalText(Path.Combine(
            Root, "docs", "calibration", "corner-reduced-drive-resistance-experiment.md"));
        string Render(string culture)
        {
            var prior = CultureInfo.CurrentCulture;
            var priorUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                return CornerReducedDriveResistanceExperimentReport.Render(Result.Value);
            }
            finally
            {
                CultureInfo.CurrentCulture = prior;
                CultureInfo.CurrentUICulture = priorUi;
            }
        }

        var en = Render("en-US");
        var pl = Render("pl-PL");
        HistoricalPhysicsSource.AssertArtifactUnchanged("docs/calibration/corner-reduced-drive-resistance-experiment.md");
        Assert.Equal(en, pl);
        Assert.DoesNotContain('\r', en);
        Assert.DoesNotContain("NaN", expected, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", expected, StringComparison.Ordinal);
        Assert.Contains("`Flying L2` is one modeled lap", en, StringComparison.Ordinal);
        Assert.Contains("Canonical apex means only `p = 0.50`", en, StringComparison.Ordinal);
        Assert.Contains("minimum is not relabeled as apex", en, StringComparison.Ordinal);
        foreach (var section in "ABCDEFGHIJKLMNOPQRSTUV")
            Assert.Contains($"## {section}.", en, StringComparison.Ordinal);
    }

    [Fact]
    public void PgeV1AndReports38To41RemainCanonicalContentIdentical()
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
            ["docs/calibration/continuous-corner-envelope-impact.md"] = "9F97DE671EF6656E3054BC408C2B089CDC0D225208F7A6D0C5A2D36F3489CBB9",
            ["docs/calibration/motoarena-matched-venue.md"] = "E13FD2A9D3B21C7AF5F2FB8C9BBBE89EEB6A796A4AAE4299D0D46EC04807DAF9",
            ["docs/calibration/real-start-telemetry.md"] = "916B6DDB543A70D2DFCD5110F90DCDD1C345B58562BB9A3780E6CCBBF4039913",
            ["docs/calibration/straight-drive-envelope-experiment.md"] = "AC99CCC757584E9F1DB833C6F4EF7343BD109479EC964AAC013A73A50E016BA8",
        };
        foreach (var (relative, sha) in expected)
        {
            var bytes = Encoding.UTF8.GetBytes(CanonicalText(Path.Combine(
                Root, relative.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(bytes)));
        }
    }

    private static CornerResistanceHeatObservation Primary(string id) =>
        Result.Value.Primary.Single(item => item.CandidateId == id);

    private static IsolatedCornerResistanceObservation Isolated(string id) =>
        Result.Value.Isolated.Single(item => item.CandidateId == id);

    private static IEnumerable<CornerResistanceHeatObservation> AllHeatObservations() =>
        Result.Value.Primary
            .Concat(Result.Value.DistanceMatched)
            .Concat(Result.Value.LineSweep)
            .Concat(Result.Value.SpeedSweep)
            .Concat(Result.Value.SlideControlSweep)
            .Concat(Result.Value.SurfaceSensitivity.Select(item => item.Observation))
            .Concat(Result.Value.Extreme);

    private static string CanonicalText(string path) => File.ReadAllText(path)
        .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SpeedwayManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
