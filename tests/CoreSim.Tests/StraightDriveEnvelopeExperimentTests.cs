using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "historical-analysis")]
public sealed class StraightDriveEnvelopeExperimentTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly Lazy<StraightDriveEnvelopeExperimentResult> Result =
        new(StraightDriveEnvelopeExperiment.Run);
    private static readonly Lazy<RealWorldCalibrationDataset> Global = new(() =>
        RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(Path.Combine(
            Root, "data", "calibration", "pge", "v1", "pge_rider_heats.csv"))));
    private static readonly Lazy<MotoarenaMatchedVenueCalibrationResult> Post40 = new(() =>
        MotoarenaMatchedVenueCalibration.Run(Global.Value));

    [Fact]
    public void CandidateMenuIsExactlyTheFrozenFiveCases()
    {
        Assert.Equal(new[] { "A0", "R08", "R12", "H08", "H12" },
            StraightDriveEnvelopeExperiment.CandidateMenu.Select(item => item.Id).ToArray());
        Assert.Equal(new[]
        {
            (0f, 0f), (.08f, .04f), (.12f, .06f), (.08f, .08f), (.12f, .12f),
        }, StraightDriveEnvelopeExperiment.CandidateMenu
            .Select(item => (item.LowSpeedSuppression, item.HighSpeedRetention)).ToArray());
        Assert.Equal(195, Result.Value.ForcePoints.Count);
    }

    [Fact]
    public void A0ExactlyEqualsProductionEnvelopeOnDenseSpeedAndGearingGrid()
    {
        var a0 = Candidate("A0");
        for (var gearingIndex = 0; gearingIndex <= 100; gearingIndex++)
        {
            var gearing = gearingIndex / 100f;
            var setup = new BikeSetup(gearing, .5f);
            for (var speedIndex = 0; speedIndex <= 15000; speedIndex++)
            {
                var speed = speedIndex / 100f;
                var expected = LongitudinalDynamics.CalculatePositiveDriveEnvelopeMultiplier(speed, setup);
                var actual = StraightDriveEnvelopeExperiment.ExperimentalMultiplier(a0, speed, gearing);
                Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
            }
        }
    }

    [Fact]
    public void DeltaHasCanonicalKnotsOutsideSupportAndSmoothJoins()
    {
        foreach (var candidate in StraightDriveEnvelopeExperiment.CandidateMenu)
        {
            foreach (var speed in new[] { 0f, 10f, 16f, 24f, 40f, 41f, 100f })
                Assert.Equal(0f, StraightDriveEnvelopeExperiment.ExperimentalDelta(candidate, speed));
            Assert.Equal(-candidate.LowSpeedSuppression,
                StraightDriveEnvelopeExperiment.ExperimentalDelta(candidate, 22f), 7);
            Assert.Equal(candidate.HighSpeedRetention,
                StraightDriveEnvelopeExperiment.ExperimentalDelta(candidate, 30f), 7);

            const float h = .001f;
            foreach (var knot in new[] { 16f, 22f, 24f, 30f, 40f })
            {
                var left = (StraightDriveEnvelopeExperiment.ExperimentalDelta(candidate, knot)
                    - StraightDriveEnvelopeExperiment.ExperimentalDelta(candidate, knot - h)) / h;
                var right = (StraightDriveEnvelopeExperiment.ExperimentalDelta(candidate, knot + h)
                    - StraightDriveEnvelopeExperiment.ExperimentalDelta(candidate, knot)) / h;
                Assert.InRange(MathF.Abs(left), 0f, .002f);
                Assert.InRange(MathF.Abs(right), 0f, .002f);
            }
        }
    }

    [Fact]
    public void CandidateMultipliersRemainFiniteAndBoundedWithoutAddedClampInRacingTable()
    {
        foreach (var candidate in StraightDriveEnvelopeExperiment.CandidateMenu)
        for (var gearingIndex = 0; gearingIndex <= 100; gearingIndex++)
        for (var speedIndex = 0; speedIndex <= 15000; speedIndex++)
        {
            var value = StraightDriveEnvelopeExperiment.ExperimentalMultiplier(
                candidate, speedIndex / 100f, gearingIndex / 100f);
            Assert.True(float.IsFinite(value));
            Assert.InRange(value, 0f, 1f);
        }

        Assert.DoesNotContain(Result.Value.ForcePoints,
            item => item.LowerClampActivated || item.UpperClampActivated);
    }

    [Fact]
    public void AreaControlCandidatesHaveBalancedSignedPerturbation()
    {
        foreach (var candidateId in new[] { "R08", "R12" })
        {
            var candidate = Candidate(candidateId);
            const double step = .001d;
            var integral = 0d;
            var previous = StraightDriveEnvelopeExperiment.ExperimentalDelta(candidate, 16f);
            for (var index = 1; index <= 24000; index++)
            {
                var current = StraightDriveEnvelopeExperiment.ExperimentalDelta(
                    candidate, (float)(16d + index * step));
                integral += (previous + current) * .5d * step;
                previous = current;
            }
            Assert.InRange(Math.Abs(integral), 0d, 1e-6d);
        }
    }

    [Fact]
    public void A0ProductionHeatIsBitIdenticalToPost40MotoarenaBaseline()
    {
        var expected = Post40.Value.MotoarenaBalanced;
        var actual = Result.Value.Primary.Single(item => item.CandidateId == "A0");
        Assert.Equal(CalibrationCsvExporter.ExportSteps(expected.Trace),
            CalibrationCsvExporter.ExportSteps(actual.Trace));
        Assert.Equal(expected.Trace.Classification, actual.Trace.Classification);
        Assert.Equal(97.882807, actual.VmaxKilometersPerHour, 6);
        Assert.Equal(13.909906, actual.FlyingLapMedianSeconds!.Value, 6);
        Assert.Equal(1366.849609, actual.TotalDistanceMeters, 6);
        Assert.Equal(98.999028, Result.Value.Balanced.Single(item => item.CandidateId == "A0")
            .VmaxP50KilometersPerHour, 6);
        Assert.Equal(14.089063, Result.Value.Balanced.Single(item => item.CandidateId == "A0")
            .FlyingLapP50Seconds, 6);
    }

    [Fact]
    public void DefaultOptionsAndFullScenarioSuiteAreExactlyTheA0ProductionBaseline()
    {
        var production = CalibrationScenarioSuite.RunRequired();
        var a0 = CalibrationScenarioSuite.RunRequiredWithProductionBaselineExperiment();

        Assert.Equal(RenderScenarioSuite(production), RenderScenarioSuite(a0));
        Assert.Equal(
            CalibrationCsvExporter.ExportSteps(
                production.OfType<CalibrationHeatResult>()
                    .Single(item => item.Metadata.ScenarioId == "full_heat/baseline").Trace),
            CalibrationCsvExporter.ExportSteps(
                a0.OfType<CalibrationHeatResult>()
                    .Single(item => item.Metadata.ScenarioId == "full_heat/baseline").Trace));
    }

    [Fact]
    public void StandingStartCornerLawAndLegacyAreExactlyFrozen()
    {
        Assert.True(Result.Value.Freeze.StandingStartExact);
        Assert.True(Result.Value.Freeze.CornerLawExact);
        Assert.True(Result.Value.Freeze.LegacyExact);
        Assert.Equal(.24, Result.Value.Freeze.ReactionTimeSeconds, 7);
        Assert.Equal(2.237621, Result.Value.Freeze.TimeTo70KphSeconds, 6);
        Assert.Equal(62.332127, Result.Value.Freeze.SpeedAtTwoSecondsKilometersPerHour, 6);
    }

    [Fact]
    public void FixedMenuShowsShapeAloneCannotResolveBothMatchedResiduals()
    {
        var baseline = Result.Value.Primary.Single(item => item.CandidateId == "A0");
        Assert.All(Result.Value.Primary.Where(item => item.CandidateId != "A0"), item =>
        {
            Assert.True(item.VmaxKilometersPerHour > baseline.VmaxKilometersPerHour);
            Assert.True(item.FlyingLapMedianSeconds < baseline.FlyingLapMedianSeconds);
            Assert.Equal(0, item.CrashCount);
        });
        Assert.Equal("H12", Result.Value.DiagnosticBestCandidateId);
        Assert.Equal("StraightEnvelopeShapeInsufficient", Result.Value.Classification);
    }

    [Fact]
    public void SkillAndGearingSweepsPreserveCapabilityOrdering()
    {
        foreach (var candidate in StraightDriveEnvelopeExperiment.CandidateMenu)
        {
            var speed = Result.Value.SpeedSweep.Where(item => item.CandidateId == candidate.Id)
                .OrderBy(item => item.SkillSpeed).ToArray();
            Assert.Equal(new[] { 0f, 25f, 50f, 75f, 100f }, speed.Select(item => item.SkillSpeed));
            for (var index = 1; index < speed.Length; index++)
                Assert.True(speed[index].VmaxKilometersPerHour > speed[index - 1].VmaxKilometersPerHour);

            var gearing = Result.Value.GearingSweep.Where(item => item.CandidateId == candidate.Id)
                .OrderBy(item => item.Gearing).ToArray();
            Assert.Equal(new[] { 0f, .5f, 1f }, gearing.Select(item => item.Gearing));
            Assert.All(gearing, item => Assert.Equal(0, item.CrashCount));
        }
    }

    [Fact]
    public void SameSeedAndRiderOrderPermutationsAreDeterministic()
    {
        var second = StraightDriveEnvelopeExperiment.Run();
        Assert.Equal(StraightDriveEnvelopeExperimentReport.Render(Result.Value),
            StraightDriveEnvelopeExperimentReport.Render(second));
        Assert.Equal(1, Result.Value.PermutationDistinctTraceHashes["A0"]);
        Assert.Equal(1, Result.Value.PermutationDistinctTraceHashes[Result.Value.DiagnosticBestCandidateId]);
    }

    [Fact]
    public void ReportIsCommittedByteStableInvariantCultureAndLfOnly()
    {
        var expected = CanonicalText(Path.Combine(
            Root, "docs", "calibration", "straight-drive-envelope-experiment.md"));
        string Render(string culture)
        {
            var prior = CultureInfo.CurrentCulture;
            var priorUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                return StraightDriveEnvelopeExperimentReport.Render(Result.Value);
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
        foreach (var section in "ABCDEFGHIJKLMNOPQR")
            Assert.Contains($"## {section}.", en, StringComparison.Ordinal);
    }

    [Fact]
    public void PgeV1AndReports38To40RemainCanonicalContentIdentical()
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
        };
        foreach (var (relative, sha) in expected)
        {
            var bytes = Encoding.UTF8.GetBytes(CanonicalText(Path.Combine(
                Root, relative.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(bytes)));
        }
    }

    private static StraightDriveEnvelopeCandidate Candidate(string id) =>
        StraightDriveEnvelopeExperiment.CandidateMenu.Single(item => item.Id == id);

    private static string RenderScenarioSuite(IReadOnlyList<CalibrationScenarioResult> results)
    {
        var baseline = results.OfType<CalibrationHeatResult>()
            .Single(item => item.Metadata.ScenarioId == "full_heat/baseline");
        var evaluation = RealWorldCalibrationEvaluator.Evaluate(
            Global.Value,
            baseline.ToSimulationCalibrationResult());
        return CalibrationScenarioReport.Render(
            results,
            evaluation,
            StraightDriveEnvelopeExperiment.BaseMainSha);
    }

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
