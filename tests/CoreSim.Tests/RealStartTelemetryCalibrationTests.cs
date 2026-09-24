using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

public sealed class RealStartTelemetryCalibrationTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly Lazy<RealWorldCalibrationDataset> Global = new(() =>
        RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(Path.Combine(
            Root, "data", "calibration", "pge", "v1", "pge_rider_heats.csv"))));
    private static readonly Lazy<RealStartTelemetryCalibrationResult> Result = new(() =>
        RealStartTelemetryCalibration.Run(Global.Value));

    [Fact]
    public void CoverageUsesExactVenueAndSeparatesRestartedAttempts()
    {
        var coverage = Result.Value.Coverage;
        Assert.Equal(95, coverage.GlobalMatchCount);
        Assert.Equal(1593, coverage.GlobalAttemptCount);
        Assert.Equal(6373, coverage.GlobalRiderObservationCount);
        Assert.Equal(7, coverage.MotoarenaMatchCount);
        Assert.Equal(114, coverage.MotoarenaAttemptCount);
        Assert.Equal(456, coverage.MotoarenaRiderObservationCount);
        Assert.Throws<InvalidOperationException>(() => Global.Value.FilterByExactVenue(2026, "Toruń"));
        Assert.Throws<InvalidOperationException>(() => Global.Value.FilterByExactVenue(
            2026, "motoarena im. Mariana Rosego"));
    }

    [Fact]
    public void SyntheticDiagnosticsRunThroughProductionSimulator()
    {
        Assert.Equal(new[] { 25f, 31f, 37f },
            Result.Value.Splits.Select(item => item.StartLineToFirstCornerMeters).ToArray());
        foreach (var split in Result.Value.Splits)
        {
            Assert.Equal(RealStartTelemetryCalibration.HeatId, split.Trace.HeatId);
            Assert.Equal(RealStartTelemetryCalibration.FixedSeed, split.Trace.Options.Seed);
            Assert.Equal(0f, split.Trace.Options.IncidentFrequency);
            Assert.Equal(4, split.Trace.Options.Laps);
            Assert.Equal(4, split.Trace.StepSamples.Count(item =>
                item.LapIndex == 0 && item.SegmentIndex == 0
                && item.StandingStartReactionTimeSeconds.HasValue));
            Assert.Equal(4, split.Trace.RiderSummaries.Count);
            Assert.All(split.Trace.StepSamples.Where(item => item.LapIndex == 0 && item.SegmentIndex == 0),
                sample => Assert.Equal(split.StartLineToFirstCornerMeters, sample.TravelledMeters));
        }
    }

    [Fact]
    public void PrimarySyntheticStartMetricsAreObservedAndStable()
    {
        var primary = Result.Value.Splits.Single(item => item.StartLineToFirstCornerMeters == 31f);
        Assert.Equal(.24, primary.ReactionTimeSeconds, 6);
        Assert.Equal(2.237621, primary.TimeTo70KphSeconds!.Value, 6);
        Assert.Equal(17.31448, primary.SpeedAtTwoSecondsMetersPerSecond!.Value, 5);
        Assert.Equal(23.639006, primary.FirstCornerEntrySpeedMetersPerSecond, 5);
        Assert.Equal(primary.FirstCornerEntrySpeedMetersPerSecond,
            primary.PeakSpeedBeforeFirstCornerMetersPerSecond, 6);
        Assert.Equal(2.759259, primary.TimeToFirstCornerSeconds, 6);
        Assert.Null(primary.DistanceAtTwoSecondsMeters);
    }

    [Fact]
    public void SplitSanityKeepsReactionAndSpeedAtTwoSecondsIndependent()
    {
        Assert.Equal(0d, Result.Value.ReactionTimeSplitSpanSeconds, 10);
        Assert.Equal(0d, Result.Value.SpeedAtTwoSecondsSplitSpanMetersPerSecond, 10);
        Assert.All(Result.Value.Splits, split =>
        {
            Assert.Equal(0d, split.PreparationDistanceMeters, 10);
        });
        Assert.Equal(new[] { 77.712355, 85.10042, 91.417847 },
            Result.Value.Splits.Select(item => item.FirstCornerEntrySpeedMetersPerSecond * 3.6d).ToArray(),
            new DoubleToleranceComparer(0.00001));
    }

    [Fact]
    public void DiagnosticsAreDeterministic()
    {
        var second = RealStartTelemetryCalibration.Run(Global.Value);
        Assert.Equal(RealStartTelemetryReport.Render(Result.Value), RealStartTelemetryReport.Render(second));
        Assert.Equal(
            Result.Value.Splits.Select(item => CalibrationCsvExporter.ExportSteps(item.Trace)).ToArray(),
            second.Splits.Select(item => CalibrationCsvExporter.ExportSteps(item.Trace)).ToArray());
    }

    [Fact]
    public void ReportIsCommittedLfOnlyAndCultureIndependent()
    {
        var expected = File.ReadAllText(Path.Combine(Root, "docs", "calibration", "real-start-telemetry.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string Render(string culture)
        {
            var prior = CultureInfo.CurrentCulture;
            var priorUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                return RealStartTelemetryReport.Render(Result.Value);
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
        foreach (var section in "ABCDEFGHIJKLMN")
            Assert.Contains($"## {section}.", en, StringComparison.Ordinal);
        Assert.Contains("UnsupportedNumericByAvailableSource", en);
        Assert.Contains("No recoverable 0–60 m physical speed series found", en);
        Assert.Contains("CalibrationRunner → HeatSimulator", en);
    }

    [Fact]
    public void NoFakeStartDatasetExists()
    {
        Assert.False(Directory.Exists(Path.Combine(Root, "data", "calibration", "pge-start", "v1")));
    }

    [Fact]
    public void FrozenNonExperimentPhysicsFilesRemainAtReviewedContentHashes()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/CoreSim/Track/ContinuousCornerEnvelope.cs"] = "4C86C9B232D8112150AD8FB9D5CF1E2B65941A3D132C7D82E61379FE367F748B",
            ["src/CoreSim/Track/SegmentPhysics.cs"] = "693D3C911F76CB83F60CD6DED850490F101B0FE0F6558D453DC35927DC123B16",
            ["src/CoreSim/Track/TrackGeometry.cs"] = "2DEDA3F4974FF97EE49EA63D345944BB208786AF89596044F06A70C7E92D3564",
            ["src/CoreSim/Rider.cs"] = "153FA23E1F16B7AFA0EC7D68C3081EED23FC057713AE20F914492684312A6E4E",
            ["src/CoreSim/Setup/BikeSetup.cs"] = "D36C27B91FD90F99D83891B33B73B1B404A7C288704C04C8400F15926B6E1767",
            ["src/CoreSim/DeterministicRandom.cs"] = "E8C5E48474E03FD7BA77B15F8A1C5BFF75DD4415C7C081D19166EF3EAB4014A7",
        };
        foreach (var (relative, hash) in expected)
        {
            var text = File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)))
                .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SpeedwayManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class DoubleToleranceComparer(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;
        public int GetHashCode(double obj) => 0;
    }
}
